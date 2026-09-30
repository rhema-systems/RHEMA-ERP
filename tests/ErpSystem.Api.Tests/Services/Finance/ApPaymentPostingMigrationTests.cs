using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Api.Services.Finance.Taxation;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

#pragma warning disable CS0618 // Regression tests intentionally assert that obsolete legacy posting paths are not used.

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed partial class ApPaymentPostingMigrationTests
{
    [Theory]
    [InlineData("missing-profile", "AP_PROFILE_REQUIRED")]
    [InlineData("draft-profile", "AP_PROFILE_REQUIRED")]
    [InlineData("expired-profile", "AP_PROFILE_REQUIRED")]
    [InlineData("future-profile", "AP_PROFILE_REQUIRED")]
    [InlineData("cross-tenant-profile", "AP_PROFILE_REQUIRED")]
    [InlineData("wrong-role-profile", "AP_PROFILE_REQUIRED")]
    [InlineData("inactive-role", "AP_ROLE_INACTIVE")]
    [InlineData("wrong-role-type", "AP_ROLE_REQUIRED")]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    public async Task FirstPosting_RequiresTheCapturedApprovedApProfile_ForTheDocumentDate(string defect, string code)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        var document = fixture.Payment;
        var role = await db.Set<BusinessPartnerRole>().SingleAsync(x => x.Id == document.BusinessPartnerRoleId);
        var profile = await db.Set<BusinessPartnerApProfileVersion>().SingleAsync(x => x.Id == document.BusinessPartnerApProfileVersionId);
        // An unrelated approved replacement must not silently replace captured authority.
        db.Set<BusinessPartnerApProfileVersion>().Add(new BusinessPartnerApProfileVersion
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BusinessPartnerRoleId = role.Id,
            VersionNumber = 99, Status = BusinessPartnerFinanceProfileStatus.Approved,
            EffectiveFrom = new DateTime(2020, 1, 1)
        });
        switch (defect)
        {
            case "missing-profile": document.BusinessPartnerApProfileVersionId = Guid.NewGuid(); break;
            case "draft-profile": profile.Status = BusinessPartnerFinanceProfileStatus.Draft; break;
            case "expired-profile": profile.EffectiveTo = document.PaymentDate.AddDays(-1); break;
            case "future-profile": profile.EffectiveFrom = document.PaymentDate.AddDays(1); break;
            case "cross-tenant-profile": profile.TenantId = Guid.NewGuid(); break;
            case "wrong-role-profile": profile.BusinessPartnerRoleId = Guid.NewGuid(); break;
            case "inactive-role": role.Status = BusinessPartnerRoleStatus.Inactive; break;
            case "wrong-role-type": role.RoleType = BusinessPartnerRoleType.Customer; break;
        }
        await db.SaveChangesAsync();
        var beforeJournals = await db.JournalEntries.CountAsync();
        var beforeTransactions = await db.AccountTransactions.CountAsync();
        var (service, _) = CreateService(db, tenantId);

        Func<Task> post = () => service.PostAsync(document.Id);
        await post.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(code + ":*");

        (await db.JournalEntries.CountAsync()).Should().Be(beforeJournals);
        (await db.AccountTransactions.CountAsync()).Should().Be(beforeTransactions);
        (await db.FinancePostingEvents.CountAsync(x => x.SourceDocumentType == "VendorPayment" && x.SourceDocumentId == document.Id))
            .Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task InvoiceChosenRate_ShouldSurvivePaymentCalculationAndPosting(decimal chosenRate)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId, allocationAmount: 100m - chosenRate);
        var tax = await SeedPaymentWithholdingTaxAsync(db, fixture, null);
        fixture.Invoice.ApplySupplierWithholdingDefaults = true;
        fixture.Invoice.WithholdingTaxId = tax.Id;
        fixture.Invoice.WithholdingTaxRate = chosenRate;
        fixture.Invoice.WithholdingTaxRateOverride = chosenRate;
        fixture.Invoice.WithholdingTaxAccountId = tax.TaxPayableAccountId;
        fixture.Allocation.WithholdingTaxAmount = chosenRate;
        fixture.Payment.WithholdingTaxAmount = chosenRate;
        fixture.Payment.WithholdingTaxId = null; // Inherit the invoice even if a payment client omitted it.
        await db.SaveChangesAsync();
        var calculator = new WithholdingTaxCertificateService(db, CreateCurrentUser(tenantId).Object);
        var (service, _) = CreateService(db, tenantId, withholdingTaxService: calculator);

        var result = await service.PostAsync(fixture.Payment.Id);

        result.WithholdingTaxRate.Should().Be(chosenRate);
        result.WithholdingTaxAmount.Should().Be(chosenRate);
        result.WithholdingTaxId.Should().Be(tax.Id);
        tax.Rate.Should().Be(10m, "the invoice choice must not change the tax master");
        var lines = await db.AccountTransactions.Where(line => line.JournalEntryId == result.JournalEntryId).ToListAsync();
        lines.Single(line => line.TransactionTag == "AP-Control").DebitAmount.Should().Be(100m);
        lines.Single(line => line.TransactionTag == "AP-Bank").CreditAmount.Should().Be(100m - chosenRate);
        lines.Where(line => line.TransactionTag == "AP-WHT").Sum(line => line.CreditAmount).Should().Be(chosenRate);
    }

    [Fact]
    public async Task InvoiceNoWithholding_ShouldNotBeReappliedDuringPaymentPosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId,
            configureInvoice: invoice => invoice.ApplySupplierWithholdingDefaults = false);
        var (service, _) = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.Payment.Id);

        result.WithholdingTaxId.Should().BeNull();
        result.WithholdingTaxAmount.Should().Be(0m);
        (await db.AccountTransactions.Where(line => line.JournalEntryId == result.JournalEntryId).ToListAsync())
            .Should().NotContain(line => line.TransactionTag == "AP-WHT");
    }

    [Fact]
    public async Task InvoiceNoWithholding_ShouldRejectAConflictingPaymentTax()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId,
            configureInvoice: invoice => invoice.ApplySupplierWithholdingDefaults = false);
        await SeedPaymentWithholdingTaxAsync(db, fixture, null);
        var calculator = new WithholdingTaxCertificateService(db, CreateCurrentUser(tenantId).Object);
        var (service, _) = CreateService(db, tenantId, withholdingTaxService: calculator);

        var post = () => service.PostAsync(fixture.Payment.Id);

        await post.Should().ThrowAsync<InvalidOperationException>().WithMessage("*disabled on this invoice*");
        (await db.FinancePostingEvents.CountAsync(value => value.SourceDocumentId == fixture.Payment.Id)).Should().Be(0);
    }

    [Fact]
    public void MixedInvoiceChoicesOrRates_ShouldRequireSeparatePayments()
    {
        var taxId = Guid.NewGuid();
        var taxed = new VendorInvoice { Id = Guid.NewGuid(), ApplySupplierWithholdingDefaults = true, WithholdingTaxId = taxId, WithholdingTaxRate = 5m };
        var declined = new VendorInvoice { Id = Guid.NewGuid(), ApplySupplierWithholdingDefaults = false };
        var otherRate = new VendorInvoice { Id = Guid.NewGuid(), ApplySupplierWithholdingDefaults = true, WithholdingTaxId = taxId, WithholdingTaxRate = 10m };

        var mixedChoice = () => ApInvoiceWithholdingPolicy.Resolve(new[] { taxed, declined }, taxId);
        var mixedRate = () => ApInvoiceWithholdingPolicy.Resolve(new[] { taxed, otherRate }, taxId);

        mixedChoice.Should().Throw<InvalidOperationException>().WithMessage("*separate payments*");
        mixedRate.Should().Throw<InvalidOperationException>().WithMessage("*separate payments*");
    }

    [Fact]
    public async Task Calculation_ShouldNotUseAnotherSuppliersInvoiceRateOverride()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        var tax = await SeedPaymentWithholdingTaxAsync(db, fixture, null);
        var otherSupplier = SeedSupplier(db, tenantId, fixture.ApAccount.Id);
        await db.SaveChangesAsync();
        var calculator = new WithholdingTaxCertificateService(db, CreateCurrentUser(tenantId).Object);

        var calculate = () => calculator.CalculateApWithholdingAsync(new WhtCalculationRequestDto
        {
            BusinessPartnerId = otherSupplier.Id, TaxId = tax.Id, PaymentDate = fixture.Payment.PaymentDate,
            TaxableBase = 100m, VendorInvoiceIds = new() { fixture.Invoice.Id },
            ContractReference = fixture.Invoice.WithholdingContractReference,
            SupplyCategory = fixture.Invoice.WithholdingSupplyCategory
        });

        await calculate.Should().ThrowAsync<InvalidOperationException>().WithMessage("*does not belong to this supplier and tenant*");
    }

    [Fact]
    public async Task BelowThresholdPayment_ShouldPreserveItsBaseAndExcludeItselfDuringPosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        var tax = await SeedPaymentWithholdingTaxAsync(db, fixture, 150m);
        var calculator = new WithholdingTaxCertificateService(db, CreateCurrentUser(tenantId).Object);
        var (service, _) = CreateService(db, tenantId, withholdingTaxService: calculator);

        var result = await service.PostAsync(fixture.Payment.Id);

        result.WithholdingTaxAmount.Should().Be(0m);
        result.WithholdingTaxBaseAmount.Should().Be(100m);
        result.WithholdingTaxRate.Should().Be(10m);
        result.WithholdingTaxCumulativeBefore.Should().Be(0m);
        result.WithholdingTaxThresholdAmount.Should().Be(150m);
        result.WithholdingTaxThresholdApplied.Should().BeFalse();
        result.WithholdingTaxAccountId.Should().Be(tax.TaxPayableAccountId);
        result.WithholdingTaxCalculationNote.Should().NotBeNullOrWhiteSpace();
        var journalLines = await db.AccountTransactions.Where(line => line.JournalEntryId == result.JournalEntryId).ToListAsync();
        journalLines.Should().NotContain(line => line.TransactionTag == "AP-WHT");

        var nextPayment = await calculator.CalculateApWithholdingAsync(new WhtCalculationRequestDto
        {
            TaxId = tax.Id, BusinessPartnerId = fixture.Supplier.Id,
            PaymentDate = fixture.Payment.PaymentDate.AddDays(1), TaxableBase = 51m,
            ContractReference = fixture.Invoice.WithholdingContractReference,
            SupplyCategory = fixture.Invoice.WithholdingSupplyCategory
        });
        nextPayment.CumulativeBefore.Should().Be(100m);
        nextPayment.ThresholdApplied.Should().BeTrue();
        nextPayment.WithholdingAmount.Should().Be(15.1m);
        nextPayment.CatchUpWithholdingAmount.Should().Be(10m);
    }

    [Fact]
    public async Task ConfiguredPayment_ShouldRejectOmittedDeductionWhenItsThresholdRequiresWithholding()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        await SeedPaymentWithholdingTaxAsync(db, fixture, null);
        var calculator = new WithholdingTaxCertificateService(db, CreateCurrentUser(tenantId).Object);
        var (service, _) = CreateService(db, tenantId, withholdingTaxService: calculator);

        var post = () => service.PostAsync(fixture.Payment.Id);

        await post.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WHT allocations*requires*");
        (await db.FinancePostingEvents.CountAsync(value => value.SourceDocumentId == fixture.Payment.Id)).Should().Be(0);
    }

    [Fact]
    public async Task WithholdingPayment_ShouldCatchUpThresholdAndExcludeItsSavedBaseOnReplay()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId, allocationAmount: 85m);
        var tax = await SeedPaymentWithholdingTaxAsync(db, fixture, 149m);
        fixture.Allocation.WithholdingTaxAmount = 15m;
        fixture.Payment.WithholdingTaxAmount = 15m;
        var book = await db.AccountingBooks.SingleAsync(value =>
            value.TenantId == tenantId && value.BookType == AccountingBookType.PrimaryFull);
        var priorJournal = new JournalEntry
        {
            Id = Guid.NewGuid(), TenantId = tenantId, JournalEntryNumber = "JE-VP-WHT-PRIOR",
            JournalType = "System Generated", EntryDate = fixture.Payment.PaymentDate.AddDays(-1),
            Description = "Posted prior AP payment", SourceModule = "AP", SourceDocumentType = "VendorPayment",
            PostingStatus = "Posted", AccountingBookId = book.Id, BookClassification = book.Code
        };
        var priorInvoice = new VendorInvoice
        {
            TenantId = tenantId, InvoiceNumber = "VI-WHT-PRIOR", SupplierInvoiceNumber = "VI-WHT-PRIOR",
            BusinessPartnerId = fixture.Supplier.Id, BusinessPartnerRoleId = fixture.Invoice.BusinessPartnerRoleId,
            BusinessPartnerApProfileVersionId = fixture.Invoice.BusinessPartnerApProfileVersionId,
            SupplierName = fixture.Supplier.PartnerName, InvoiceDate = fixture.Payment.PaymentDate.AddDays(-2),
            ReceivedDate = fixture.Payment.PaymentDate.AddDays(-2), DueDate = fixture.Payment.PaymentDate.AddDays(28),
            SubTotal = 50m, TotalAmount = 50m, CurrencyCode = "GHS", ExchangeRate = 1m,
            BaseCurrencyAmount = 50m, Status = VendorInvoiceStatus.Approved, ApprovalStatus = "Approved",
            ApAccountId = fixture.ApAccount.Id, WithholdingContractReference = fixture.Invoice.WithholdingContractReference,
            WithholdingSupplyCategory = fixture.Invoice.WithholdingSupplyCategory
        };
        var priorPayment = new VendorPayment
        {
            TenantId = tenantId, PaymentNumber = "VP-WHT-PRIOR", BusinessPartnerId = fixture.Supplier.Id,
            PaymentDate = fixture.Payment.PaymentDate.AddDays(-1), TotalAmount = 50m,
            CurrencyCode = "GHS", ExchangeRate = 1m, Status = VendorPaymentStatus.Processed,
            WithholdingTaxId = tax.Id, WithholdingTaxRate = 10m, WithholdingTaxBaseAmount = 50m, JournalEntryId = priorJournal.Id
        };
        priorJournal.SourceDocumentId = priorPayment.Id;
        db.JournalEntries.Add(priorJournal);
        db.VendorInvoices.Add(priorInvoice);
        db.Set<VendorPayment>().Add(priorPayment);
        db.Set<VendorPaymentAllocation>().Add(new VendorPaymentAllocation
        {
            TenantId = tenantId, VendorPaymentId = priorPayment.Id, VendorPayment = priorPayment,
            VendorInvoiceId = priorInvoice.Id, VendorInvoice = priorInvoice,
            AllocatedAmount = 50m, PaymentCurrencyAmount = 50m, SettlementFunctionalAmount = 50m,
            WithholdingTaxBaseFunctionalAmount = 50m,
            WithholdingTaxAmount = 0m, WithholdingTaxFunctionalAmount = 0m,
            AllocationDate = priorPayment.PaymentDate
        });
        await db.SaveChangesAsync();
        var calculator = new WithholdingTaxCertificateService(db, CreateCurrentUser(tenantId).Object);
        var (service, _) = CreateService(db, tenantId, withholdingTaxService: calculator);

        var result = await service.PostAsync(fixture.Payment.Id);
        var replay = await service.PostAsync(fixture.Payment.Id);

        result.WithholdingTaxCumulativeBefore.Should().Be(50m);
        result.WithholdingTaxBaseAmount.Should().Be(150m, "the certificate includes the prior50 catch-up plus current100 net base");
        result.WithholdingTaxAmount.Should().Be(15m);
        replay.JournalEntryId.Should().Be(result.JournalEntryId);
        var lines = await db.AccountTransactions.Where(line => line.JournalEntryId == result.JournalEntryId).ToListAsync();
        lines.Single(line => line.TransactionTag == "AP-Control").DebitAmount.Should().Be(100m);
        lines.Single(line => line.TransactionTag == "AP-Bank").CreditAmount.Should().Be(85m);
        lines.Single(line => line.TransactionTag == "AP-WHT").CreditAmount.Should().Be(15m);
        (await db.FinancePostingEvents.CountAsync(value => value.SourceDocumentId == fixture.Payment.Id && value.PostingAction == "Post")).Should().Be(1);
    }

    private static async Task<Tax> SeedPaymentWithholdingTaxAsync(ApplicationDbContext db, ApPaymentFixture fixture, decimal? threshold)
    {
        var payableAccount = (await db.FinanceSettings.SingleAsync(value => value.TenantId == fixture.Payment.TenantId)).ControlAccountTaxId;
        var tax = new Tax
        {
            TenantId = fixture.Payment.TenantId, Code = "WHT-PAYMENT-COMPLIANCE", Name = "Configured payment withholding",
            Category = TaxCategory.Withholding, Applicability = TaxApplicability.Purchases,
            IsActive = true, Rate = 10m, EffectiveFrom = new DateTime(2026, 1, 1),
            ThresholdAmount = threshold, TaxPayableAccountId = payableAccount
        };
        db.Taxes.Add(tax);
        fixture.Payment.WithholdingTaxId = tax.Id;
        fixture.Payment.WithholdingTaxRate = 10m;
        fixture.Payment.WithholdingTaxBaseAmount = 100m;
        fixture.Payment.WithholdingTaxAccountId = payableAccount;
        // These payment-posting regressions exercise the legacy payment-configured path.
        // Tests for invoice-owned WHT decisions set the invoice tax/rate explicitly.
        fixture.Invoice.WithholdingContractReference = "CONTRACT-PAYMENT-COMPLIANCE";
        fixture.Invoice.WithholdingSupplyCategory = WhtSupplyCategory.Services;
        await db.SaveChangesAsync();
        return tax;
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task ApprovedApPayment_ShouldPostThroughFinancePostingEngineAndCreateAuditEvent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        var (service, subledgerPostingMock) = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.Payment.Id);

        result.JournalEntryId.Should().NotBeNull();
        subledgerPostingMock.Verify(x => x.PostApPaymentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        var postingEvent = await db.FinancePostingEvents.SingleAsync(e =>
            e.TenantId == tenantId &&
            e.SourceModule == "AP" &&
            e.SourceDocumentType == "VendorPayment" &&
            e.SourceDocumentId == fixture.Payment.Id &&
            e.PostingAction == "Post");
        postingEvent.JournalEntryId.Should().Be(result.JournalEntryId);

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == result.JournalEntryId);
        journal.PostingStatus.Should().Be("Posted");
        journal.SourceModule.Should().Be("AP");
        journal.SourceDocumentType.Should().Be("VendorPayment");
        journal.Transactions.Should().HaveCount(2);
        journal.Transactions.Single(t => t.AccountId == fixture.ApAccount.Id).DebitAmount.Should().Be(100m);
        journal.Transactions.Single(t => t.AccountId == fixture.BankGlAccount.Id).CreditAmount.Should().Be(100m);

        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.ApPaymentPosted && a.TenantId == tenantId)).Should().Be(1);
        (await db.AccountBalances.SingleAsync(x => x.AccountId == fixture.ApAccount.Id)).ClosingBalance.Should().Be(100m);
        (await db.AccountBalances.SingleAsync(x => x.AccountId == fixture.BankGlAccount.Id)).ClosingBalance.Should().Be(-100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task PartialCashAllocationPlusResidualAdvance_ShouldRemainUnsupported()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(
            db,
            tenantId,
            payment => payment.TotalAmount = 100m,
            allocationAmount: 60m);
        var (service, _) = CreateService(db, tenantId);

        var post = () => service.PostAsync(fixture.Payment.Id);

        var error = await post.Should().ThrowAsync<VendorPaymentControlException>();
        error.Which.Code.Should().Be("AP_PAYMENT_ALLOCATION_TOTAL_MISMATCH");
        (await db.FinancePostingEvents.CountAsync(item =>
            item.SourceDocumentType == "VendorPayment" &&
            item.SourceDocumentId == fixture.Payment.Id)).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task NonCashSettlementComponents_ShouldNotReplaceAllocatedPaymentCash()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(
            db,
            tenantId,
            payment => payment.TotalAmount = 100m,
            allocationAmount: 60m);
        fixture.Allocation.DiscountAmount = 40m;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var post = () => service.PostAsync(fixture.Payment.Id);

        var error = await post.Should().ThrowAsync<VendorPaymentControlException>();
        error.Which.Code.Should().Be("AP_PAYMENT_ALLOCATION_TOTAL_MISMATCH");
        (await db.FinancePostingEvents.CountAsync(item =>
            item.SourceDocumentType == "VendorPayment" &&
            item.SourceDocumentId == fixture.Payment.Id)).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CrossCurrencyDeductions")]
    [Trait("Category", "AccountsPayable")]
    public async Task CrossCurrencyApPayment_WithWht_ShouldRequireStatutoryEvidenceBeforePosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        var withholdingAccount = SeedAccount(
            db,
            tenantId,
            "2310",
            AccountType.Liability,
            isControlAccount: true,
            allowDirectPosting: false);
        var discountAccountId = (await db.Set<FinanceSettings>().SingleAsync()).DiscountReceivedAccountId!.Value;
        var discountAccount = await db.Accounts.SingleAsync(account => account.Id == discountAccountId);
        EnableCurrencyForAccounts(db, tenantId, "USD", fixture.ApAccount, discountAccount, withholdingAccount);
        var taxId = Guid.NewGuid();
        var settlementRate = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrencyCode = "GHS",
            TargetCurrencyCode = "USD",
            Rate = 0.08m,
            InverseRate = 12.5m,
            EffectiveDate = new DateTime(2026, 7, 5),
            RateType = ExchangeRateType.Daily,
            RateSource = "Regression fixture",
            IsActive = true,
            ApprovalStatus = RateApprovalStatus.Approved,
            CreatedByUserId = Guid.NewGuid(),
            CreatedDate = DateTime.UtcNow
        };
        db.ExchangeRates.Add(settlementRate);

        // Cash is paid in functional currency while the supplier invoice and every deduction
        // remain denominated in USD. The persisted fields below model the immutable evidence a
        // production allocation captures when it is created; posting must not recalculate them
        // from the payment header or combine native amounts from different currencies.
        fixture.Payment.TotalAmount = 937.50m;
        fixture.Payment.AllocatedAmount = 937.50m;
        fixture.Payment.CurrencyCode = "GHS";
        fixture.Payment.ExchangeRate = 1m;
        fixture.Payment.WithholdingTaxId = taxId;
        fixture.Payment.WithholdingTaxAccountId = withholdingAccount.Id;
        fixture.Payment.WithholdingTaxAmount = 37.50m;
        fixture.Invoice.CurrencyCode = "USD";
        fixture.Invoice.ExchangeRate = 10m;
        fixture.Invoice.SubTotal = 80m;
        fixture.Invoice.TaxAmount = 0m;
        fixture.Invoice.TotalAmount = 80m;
        fixture.Invoice.BaseCurrencyAmount = 800m;
        fixture.Invoice.WithholdingTaxId = taxId;
        fixture.Invoice.WithholdingTaxRate = 3.75m;
        fixture.Invoice.WithholdingContractReference = "CONTRACT-CROSS-CURRENCY-001";
        fixture.Invoice.WithholdingSupplyCategory = WhtSupplyCategory.Services;
        fixture.Allocation.AllocatedAmount = 75m;
        fixture.Allocation.DiscountAmount = 2m;
        fixture.Allocation.WithholdingTaxAmount = 3m;
        fixture.Allocation.PaymentCurrencyCode = "GHS";
        fixture.Allocation.InvoiceCurrencyCode = "USD";
        fixture.Allocation.PaymentCurrencyAmount = 937.50m;
        fixture.Allocation.PaymentExchangeRate = 1m;
        fixture.Allocation.PaymentFunctionalAmount = 937.50m;
        fixture.Allocation.InvoiceSettlementExchangeRate = 12.5m;
        fixture.Allocation.InvoiceSettlementExchangeRateId = settlementRate.Id;
        fixture.Allocation.DiscountFunctionalAmount = 25m;
        fixture.Allocation.WithholdingTaxFunctionalAmount = 37.50m;
        fixture.Allocation.SettlementFunctionalAmount = 1_000m;
        fixture.Allocation.IsCrossCurrency = true;
        await db.SaveChangesAsync();

        var fx = new Mock<IFxAccountingService>();
        fx.Setup(service => service.PostRealizedFxForApPaymentAsync(
                fixture.Payment.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FxRealizedSettlement>());
        var wht = new Mock<IWithholdingTaxCertificateService>();
        wht.Setup(service => service.CalculateApWithholdingAsync(
                It.Is<WhtCalculationRequestDto>(request =>
                    request.TaxId == taxId &&
                    request.TaxableBase == 1_000m &&
                    request.ContractReference == "CONTRACT-CROSS-CURRENCY-001" &&
                    request.SupplyCategory == WhtSupplyCategory.Services),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhtCalculationResultDto
            {
                TaxId = taxId,
                TaxCode = "WHT-SERVICES",
                TaxName = "Services withholding tax",
                TaxRate = 3.75m,
                TaxableBase = 1_000m,
                WithholdingAmount = 37.50m,
                TaxPayableAccountId = withholdingAccount.Id,
                ContractReference = "CONTRACT-CROSS-CURRENCY-001",
                SupplyCategory = WhtSupplyCategory.Services,
                CalculationNote = "Regression fixture"
            });
        var (service, _) = CreateService(db, tenantId, fx.Object, wht.Object);

        var act = () => service.PostAsync(fixture.Payment.Id);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*statutory conversion evidence*");
        wht.Verify(service => service.CalculateApWithholdingAsync(It.IsAny<WhtCalculationRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
        (await db.FinancePostingEvents.CountAsync(row => row.SourceDocumentId == fixture.Payment.Id)).Should().Be(0);
    }

    [Fact]
    public async Task ForeignPayment_ForGhsInvoiceWithWht_ShouldNotBypassStatutoryCurrencyGuard()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        await SeedPaymentWithholdingTaxAsync(db, fixture, null);
        fixture.Payment.CurrencyCode = "USD";
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);
        var act = () => service.PostAsync(fixture.Payment.Id);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*statutory conversion evidence*");
        (await db.FinancePostingEvents.CountAsync(row => row.SourceDocumentId == fixture.Payment.Id)).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task SupplierAdvance_ShouldPostAndApplyThroughFinancePostingEngine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        var supplierAdvanceAccount = SeedAccount(db, tenantId, "1500", AccountType.Asset);
        var settings = await db.Set<FinanceSettings>().SingleAsync(s => s.TenantId == tenantId);
        settings.SupplierAdvanceAccountId = supplierAdvanceAccount.Id;
        db.Remove(fixture.Allocation);
        fixture.Payment.AllocatedAmount = 0m;
        fixture.Invoice.PaidAmount = 0m;
        fixture.Invoice.Status = VendorInvoiceStatus.Approved;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var (service, subledgerPostingMock) = CreateService(db, tenantId);

        var initialPosting = await service.PostAsync(fixture.Payment.Id);
        // Advance application is a new accounting event dated when the application occurs. Keep
        // this regression test calendar-independent by opening the current month when it differs
        // from the seeded July source-document period.
        var today = DateTime.UtcNow.Date;
        if (today < new DateTime(2026, 7, 1) || today > new DateTime(2026, 7, 31))
        {
            db.FiscalPeriods.Add(new FiscalPeriod
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                FiscalYearId = Guid.NewGuid(),
                PeriodName = today.ToString("MMMM yyyy"),
                PeriodCode = today.ToString("yyyy-MM"),
                PeriodNumber = today.Month,
                PeriodType = PeriodType.Monthly,
                StartDate = new DateTime(today.Year, today.Month, 1),
                EndDate = new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1),
                PeriodDays = DateTime.DaysInMonth(today.Year, today.Month),
                PeriodStatus = "Open",
                IsOpen = true,
                IsClosed = false,
                IsLocked = false
            });
            await db.SaveChangesAsync();
        }
        var application = await service.AllocatePaymentAsync(fixture.Payment.Id, new List<VendorPaymentAllocationCreateDto>
        {
            new() { VendorInvoiceId = fixture.Invoice.Id, AllocatedAmount = 100m }
        });

        (await db.Set<VendorPayment>().SingleAsync(p => p.Id == fixture.Payment.Id)).IsSupplierAdvance.Should().BeTrue();
        (await db.JournalEntries.SingleAsync(j => j.Id == initialPosting.JournalEntryId)).SourceDocumentType.Should().Be("VendorPayment");
        var allocation = await db.Set<VendorPaymentAllocation>().SingleAsync(a => a.Id == application.Allocations.Single().Id);
        allocation.ApplicationJournalEntryId.Should().NotBeNull();
        allocation.ApplicationPostingEventId.Should().NotBeNull();
        var applicationJournal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == allocation.ApplicationJournalEntryId);
        applicationJournal.SourceDocumentType.Should().Be("VendorPaymentAdvanceApplication");
        applicationJournal.Transactions.Single(t => t.AccountId == fixture.ApAccount.Id).DebitAmount.Should().Be(100m);
        applicationJournal.Transactions.Single(t => t.AccountId == supplierAdvanceAccount.Id).CreditAmount.Should().Be(100m);
        subledgerPostingMock.Verify(x => x.PostApPaymentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task ForeignSupplierAdvance_ShouldApplyAtFrozenLotAndCurrentInvoiceRatesThenReverseImmutably()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        var supplierAdvanceAccount = SeedAccount(db, tenantId, "1500", AccountType.Asset);
        var realizedFxGainAccount = SeedAccount(db, tenantId, "4700", AccountType.Revenue);
        var settings = await db.Set<FinanceSettings>().SingleAsync(s => s.TenantId == tenantId);
        settings.SupplierAdvanceAccountId = supplierAdvanceAccount.Id;
        settings.RealizedFxGainAccountId = realizedFxGainAccount.Id;

        var originRate = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrencyCode = "GHS",
            TargetCurrencyCode = "USD",
            Rate = 0.1m,
            InverseRate = 10m,
            EffectiveDate = new DateTime(2026, 7, 5),
            RateType = ExchangeRateType.Daily,
            RateSource = "Regression fixture",
            IsActive = true,
            ApprovalStatus = RateApprovalStatus.Approved,
            CreatedByUserId = Guid.NewGuid(),
            CreatedDate = DateTime.UtcNow
        };
        var applicationRate = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrencyCode = "GHS",
            TargetCurrencyCode = "EUR",
            Rate = 1m / 13m,
            InverseRate = 13m,
            EffectiveDate = new DateTime(2026, 7, 1),
            RateType = ExchangeRateType.Daily,
            RateSource = "Regression fixture",
            IsActive = true,
            ApprovalStatus = RateApprovalStatus.Approved,
            CreatedByUserId = Guid.NewGuid(),
            CreatedDate = DateTime.UtcNow
        };
        db.ExchangeRates.AddRange(originRate, applicationRate);

        // The payment is the USD advance lot (USD 10 at GHS 10). The invoice is reduced by
        // EUR 8 at the approved application rate of GHS 13, producing a GHS 4 realized gain.
        db.Remove(fixture.Allocation);
        fixture.Payment.TotalAmount = 10m;
        fixture.Payment.AllocatedAmount = 0m;
        fixture.Payment.CurrencyCode = "USD";
        fixture.Payment.ExchangeRate = originRate.InverseRate;
        fixture.Payment.ExchangeRateId = originRate.Id;
        fixture.BankAccount.Currency = "USD";
        fixture.Invoice.TotalAmount = 8m;
        fixture.Invoice.PaidAmount = 0m;
        fixture.Invoice.CurrencyCode = "EUR";
        fixture.Invoice.ExchangeRate = applicationRate.InverseRate;
        fixture.Invoice.BaseCurrencyAmount = 104m;
        fixture.Invoice.Status = VendorInvoiceStatus.Approved;
        EnableCurrencyForAccounts(db, tenantId, "USD", fixture.BankGlAccount, supplierAdvanceAccount);
        EnableCurrencyForAccounts(db, tenantId, "EUR", fixture.ApAccount);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var exchangeRates = new Mock<IExchangeRateService>();
        exchangeRates
            .Setup(service => service.GetExchangeRateByIdAsync(applicationRate.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRateDto
            {
                Id = applicationRate.Id,
                TenantId = tenantId,
                BaseCurrencyCode = "GHS",
                TargetCurrencyCode = "EUR",
                Rate = applicationRate.Rate,
                InverseRate = applicationRate.InverseRate,
                EffectiveDate = applicationRate.EffectiveDate,
                RateType = "Daily",
                QuoteSide = "Mid",
                IsActive = true,
                ApprovalStatus = "Approved"
            });
        var (service, _) = CreateService(db, tenantId, exchangeRateService: exchangeRates.Object);

        await service.PostAsync(fixture.Payment.Id);
        var application = await service.AllocatePaymentAsync(fixture.Payment.Id, new List<VendorPaymentAllocationCreateDto>
        {
            new()
            {
                VendorInvoiceId = fixture.Invoice.Id,
                AllocatedAmount = 8m,
                PaymentCurrencyAmount = 10m,
                InvoiceSettlementExchangeRateId = applicationRate.Id
            }
        });

        var allocation = await db.Set<VendorPaymentAllocation>()
            .SingleAsync(item => item.Id == application.Allocations.Single().Id);
        allocation.PaymentCurrencyCode.Should().Be("USD");
        allocation.PaymentCurrencyAmount.Should().Be(10m);
        allocation.PaymentExchangeRate.Should().Be(10m);
        allocation.PaymentFunctionalAmount.Should().Be(100m);
        allocation.InvoiceCurrencyCode.Should().Be("EUR");
        allocation.AllocatedAmount.Should().Be(8m);
        allocation.InvoiceSettlementExchangeRate.Should().Be(13m);
        allocation.SettlementFunctionalAmount.Should().Be(104m);
        var applicationJournal = await db.JournalEntries
            .Include(entry => entry.Transactions)
            .SingleAsync(entry => entry.Id == allocation.ApplicationJournalEntryId);
        applicationJournal.Transactions.Single(line => line.AccountId == fixture.ApAccount.Id).DebitAmount.Should().Be(104m);
        applicationJournal.Transactions.Single(line => line.AccountId == supplierAdvanceAccount.Id).CreditAmount.Should().Be(100m);
        applicationJournal.Transactions.Single(line => line.AccountId == realizedFxGainAccount.Id).CreditAmount.Should().Be(4m);

        await service.ReverseAllocationAsync(
            allocation.Id,
            "Supplier advance application entered against the wrong invoice");

        var allocationFacts = await db.Set<VendorPaymentAllocation>()
            .Where(item => item.VendorPaymentId == fixture.Payment.Id)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync();
        allocationFacts.Should().HaveCount(2);
        allocationFacts.Single(item => item.IsReversal).Should().Match<VendorPaymentAllocation>(item =>
            item.OriginalAllocationId == allocation.Id &&
            item.AllocatedAmount == -8m &&
            item.PaymentCurrencyAmount == -10m &&
            item.PaymentFunctionalAmount == -100m &&
            item.SettlementFunctionalAmount == -104m);
        (await db.Set<VendorPayment>().SingleAsync(item => item.Id == fixture.Payment.Id)).AllocatedAmount.Should().Be(0m);
        (await db.Set<VendorInvoice>().SingleAsync(item => item.Id == fixture.Invoice.Id)).PaidAmount.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task UnapprovedApPayment_ShouldNotPost()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId, payment => payment.Status = VendorPaymentStatus.PendingAuthorization);
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AP payment workflow approval is not complete.");
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentType == "VendorPayment")).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task PaymentAgainstUnpostedApInvoice_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId, configureInvoice: invoice => invoice.JournalEntryId = null, seedInvoicePostingEvent: false);
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"AP payment cannot settle unposted invoice '{fixture.Invoice.InvoiceNumber}'.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task OverSettledApInvoice_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId, payment => payment.TotalAmount = 125m, allocationAmount: 125m);
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"AP payment would over-settle invoice '{fixture.Invoice.InvoiceNumber}'.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task CrossTenantSupplier_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherSupplier = SeedSupplier(db, otherTenantId, fixture.ApAccount.Id);
        fixture.Payment.BusinessPartnerId = otherSupplier.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The AP payment Business Partner was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task CrossTenantInvoiceSettlement_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherInvoice = SeedPostedInvoice(db, otherTenantId, fixture.Supplier, fixture.ApAccount, "VI-OTHER", new DateTime(2026, 7, 5), 100m);
        fixture.Allocation.VendorInvoiceId = otherInvoice.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Vendor invoice with Id '{otherInvoice.Id}' not found.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task LegacySupplierApControlOverride_ShouldBeIgnoredInFavorOfFinanceSettings()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherApAccount = SeedAccount(db, otherTenantId, "2000", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        fixture.Supplier.DefaultApAccountId = otherApAccount.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.Payment.Id);

        result.Status.Should().Be(VendorPaymentStatus.Processed);
        var journal = await db.JournalEntries.Include(x => x.Transactions).SingleAsync(x => x.Id == result.JournalEntryId);
        journal.Transactions.Should().Contain(line => line.AccountId == fixture.ApAccount.Id && line.DebitAmount > 0m);
        journal.Transactions.Should().NotContain(line => line.AccountId == otherApAccount.Id);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task CrossTenantBankAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherBankGl = SeedAccount(db, otherTenantId, "1100", AccountType.Asset);
        var otherBank = SeedBankAccount(db, otherTenantId, otherBankGl.Id);
        fixture.Payment.BankAccountId = otherBank.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AP payment bank account was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task InactiveBankGlAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        fixture.BankGlAccount.Status = AccountStatus.Inactive;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AP payment posting bank/cash account account '1100' is not active.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task ClosedPeriod_ShouldBeRejectedByPostingEngine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId, periodIsOpen: false, periodIsClosed: true);
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posting period is not open.*");
        fixture.Payment.JournalEntryId.Should().BeNull();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task DuplicateApPaymentPosting_ShouldReturnExistingPostingAndAuditDuplicate()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);

        var first = await service.PostAsync(fixture.Payment.Id);
        var capturedProfile = await db.Set<BusinessPartnerApProfileVersion>()
            .SingleAsync(x => x.Id == fixture.Payment.BusinessPartnerApProfileVersionId);
        capturedProfile.Status = BusinessPartnerFinanceProfileStatus.Superseded;
        await db.SaveChangesAsync();
        var second = await service.PostAsync(fixture.Payment.Id);

        second.JournalEntryId.Should().Be(first.JournalEntryId);
        (await db.JournalEntries.CountAsync(j => j.SourceDocumentType == "VendorPayment")).Should().Be(1);
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentType == "VendorPayment")).Should().Be(1);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.ApPaymentDuplicatePostingAttempt)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task PostedApPayment_ShouldUseBalancedControlledVoidInsteadOfMutation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);
        await service.PostAsync(fixture.Payment.Id);

        var allocate = () => service.AllocatePaymentAsync(fixture.Payment.Id, new List<VendorPaymentAllocationCreateDto>
        {
            new() { VendorInvoiceId = fixture.Invoice.Id, AllocatedAmount = 1m }
        });
        var reverse = () => service.ReverseAllocationAsync(fixture.Allocation.Id, "test reversal");

        await allocate.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posted vendor payments cannot be allocated. Use a reversal, void, or adjustment workflow.");
        await reverse.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posted vendor payment allocations cannot be reversed by mutation. Use a reversal, void, or adjustment workflow.");
        // Void is a controlled, idempotent compensating posting. It must preserve the original
        // journal and allocation evidence while restoring the supplier invoice settlement.
        var firstVoid = await service.VoidPaymentAsync(fixture.Payment.Id, "test void");
        var secondVoid = await service.VoidPaymentAsync(fixture.Payment.Id, "idempotent retry");

        firstVoid.Status.Should().Be(VendorPaymentStatus.Voided);
        secondVoid.Status.Should().Be(VendorPaymentStatus.Voided);
        var original = await db.JournalEntries
            .Include(item => item.ReversalJournalEntry)
                .ThenInclude(item => item!.Transactions)
            .SingleAsync(item => item.Id == firstVoid.JournalEntryId);
        original.IsReversed.Should().BeTrue();
        original.ReversalJournalEntryId.Should().NotBeNull();
        original.ReversalJournalEntry!.IsBalanced.Should().BeTrue();
        original.ReversalJournalEntry.TotalDebitAmount.Should().Be(original.TotalCreditAmount);
        original.ReversalJournalEntry.TotalCreditAmount.Should().Be(original.TotalDebitAmount);
        (await db.FinancePostingEvents.CountAsync(item =>
            item.SourceDocumentType == "VendorPayment" && item.PostingAction == "Reverse")).Should().Be(1);
        (await db.Set<VendorPaymentAllocation>().CountAsync(item =>
            item.IsReversal && item.OriginalAllocationId == fixture.Allocation.Id)).Should().Be(1);
        var restoredInvoice = await db.Set<VendorInvoice>().SingleAsync(item => item.Id == fixture.Invoice.Id);
        restoredInvoice.PaidAmount.Should().Be(0m);
        restoredInvoice.Status.Should().Be(VendorInvoiceStatus.Approved);
        (await db.AuditLogs.CountAsync(item => item.Action == FinanceAuditEvents.ApPaymentReversed)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task LeaseInvoiceSettlementAndReversal_ShouldReuseExactNonIfrsBookAuthority()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId, accountingBookCode: "LEASE_PRIMARY");
        var book = await db.AccountingBooks.SingleAsync(item => item.TenantId == tenantId);
        fixture.Invoice.LeaseScheduleLineId = Guid.NewGuid();
        fixture.Invoice.LeaseAccountingBookId = book.Id;
        fixture.Invoice.LeaseAccountingBookCode = book.Code;
        fixture.Invoice.LeaseFunctionalCurrencyCode = "GHS";
        var invoiceJournal = await db.JournalEntries.SingleAsync(item => item.Id == fixture.Invoice.JournalEntryId);
        invoiceJournal.BookClassification = book.Code;
        var invoiceEvent = await db.FinancePostingEvents.SingleAsync(item =>
            item.SourceDocumentType == "VendorInvoice" && item.SourceDocumentId == fixture.Invoice.Id);
        invoiceEvent.BookClassification = book.Code;
        await db.SaveChangesAsync();
        var authorityCurrentUser = CreateCurrentUser(tenantId);
        var sourceBookAuthorities = new FinanceSourceBookAuthorityService(db, authorityCurrentUser.Object);
        var invoiceAuthority = await sourceBookAuthorities.RetainExistingPostedOriginalAsync(
            new FinanceSourceBookAuthorityFreezeRequest
            {
                OriginModuleCode = "FIN",
                SourceDocumentType = "VendorInvoice",
                SourceDocumentId = fixture.Invoice.Id,
                PostingAction = "Post",
                EffectiveDate = fixture.Invoice.InvoiceDate,
                TransactionCurrencyCode = "GHS",
                FreezeStage = FinanceSourceBookAuthorityFreezeStages.LegacyPosted
            },
            invoiceJournal.Id,
            invoiceEvent.Id);
        fixture.Invoice.SourceBookAuthorityId = invoiceAuthority.AuthorityId;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId, sourceBookAuthorities: sourceBookAuthorities);

        var posted = await service.PostAsync(fixture.Payment.Id);
        var reversed = await service.ReversePaymentAsync(fixture.Payment.Id, new ReverseVendorPaymentDto
        {
            Reason = "Lease instalment settlement correction.",
            ReversalDate = DateTime.UtcNow.Date
        });

        posted.AccountingBookId.Should().Be(book.Id);
        posted.AccountingBookCode.Should().Be("LEASE_PRIMARY");
        posted.FunctionalCurrencyCode.Should().Be("GHS");
        (await db.JournalEntries.SingleAsync(item => item.Id == posted.JournalEntryId))
            .BookClassification.Should().Be("LEASE_PRIMARY");
        (await db.JournalEntries.SingleAsync(item => item.Id == reversed.ReversalJournalEntryId))
            .BookClassification.Should().Be("LEASE_PRIMARY");
        (await db.VendorInvoices.SingleAsync(item => item.Id == fixture.Invoice.Id)).Status
            .Should().Be(VendorInvoiceStatus.Approved);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task LeaseInvoiceSettlementWithoutBoundAuthority_ShouldFailBeforePaymentJournal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId, accountingBookCode: "LEASE_PRIMARY");
        var book = await db.AccountingBooks.SingleAsync(item => item.TenantId == tenantId);
        fixture.Invoice.LeaseScheduleLineId = Guid.NewGuid();
        fixture.Invoice.LeaseAccountingBookId = book.Id;
        fixture.Invoice.LeaseAccountingBookCode = book.Code;
        fixture.Invoice.LeaseFunctionalCurrencyCode = "GHS";
        var invoiceJournal = await db.JournalEntries.SingleAsync(item => item.Id == fixture.Invoice.JournalEntryId);
        invoiceJournal.BookClassification = book.Code;
        var invoiceEvent = await db.FinancePostingEvents.SingleAsync(item =>
            item.SourceDocumentType == "VendorInvoice" && item.SourceDocumentId == fixture.Invoice.Id);
        invoiceEvent.BookClassification = book.Code;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId,
            sourceBookAuthorities: new FinanceSourceBookAuthorityService(db, CreateCurrentUser(tenantId).Object));

        var post = () => service.PostAsync(fixture.Payment.Id);

        await post.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("LEASE_AP_SOURCE_BOOK_AUTHORITY_REQUIRED:*");
        (await db.FinancePostingEvents.CountAsync(item =>
            item.SourceDocumentType == "VendorPayment" && item.SourceDocumentId == fixture.Payment.Id))
            .Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task PaymentAcrossDifferentInvoiceBooks_ShouldFailBeforeAnyPaymentJournal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        var periodId = await db.FiscalPeriods.Where(item => item.TenantId == tenantId)
            .Select(item => item.Id).FirstAsync();
        var secondBook = new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "LOCAL", Name = "Local Full",
            Purpose = "Statutory", BookType = AccountingBookType.ParallelFull,
            LifecycleStatus = AccountingBookLifecycleStatus.Active, FunctionalCurrencyCode = "GHS",
            IsActive = true, AllowsPosting = true, BaseAccountingBookId =
                (await db.AccountingBooks.SingleAsync(item => item.TenantId == tenantId)).Id,
            ReplicationStartDate = new DateTime(2026, 1, 1)
        };
        db.AccountingBooks.Add(secondBook);
        var secondInvoice = SeedPostedInvoice(db, tenantId, fixture.Supplier, fixture.ApAccount,
            "VI-2026-LOCAL", fixture.Payment.PaymentDate, 100m, periodId);
        var secondJournal = db.JournalEntries.Local.Single(item => item.Id == secondInvoice.JournalEntryId);
        secondJournal.AccountingBookId = secondBook.Id;
        secondJournal.BookClassification = secondBook.Code;
        var secondEvent = db.FinancePostingEvents.Local.Single(item => item.SourceDocumentId == secondInvoice.Id);
        secondEvent.AccountingBookId = secondBook.Id;
        secondEvent.BookClassification = secondBook.Code;
        fixture.Payment.TotalAmount = 200m;
        fixture.Payment.AllocatedAmount = 200m;
        db.Set<VendorPaymentAllocation>().Add(new VendorPaymentAllocation
        {
            TenantId = tenantId, VendorPaymentId = fixture.Payment.Id, VendorInvoiceId = secondInvoice.Id,
            AllocatedAmount = 100m, AllocationDate = fixture.Payment.PaymentDate, CreatedBy = "seed"
        });
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var post = () => service.PostAsync(fixture.Payment.Id);

        await post.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AP_PAYMENT_MIXED_BOOKS:*");
        (await db.FinancePostingEvents.CountAsync(item =>
            item.SourceDocumentType == "VendorPayment" && item.SourceDocumentId == fixture.Payment.Id))
            .Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task PostedApPayment_ShouldReverseThroughPostingEngineAndRestoreInvoiceSettlement()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);
        var posted = await service.PostAsync(fixture.Payment.Id);
        // The default TDC policy posts corrections in the current open period. Keep this test
        // calendar-independent so it validates policy behavior instead of expiring monthly.
        var reversalDate = DateTime.UtcNow.Date;
        const string reversalReason = "Duplicate supplier payment identified during AP review.";

        var reversed = await service.ReversePaymentAsync(fixture.Payment.Id, new ReverseVendorPaymentDto
        {
            Reason = reversalReason,
            ReversalDate = reversalDate
        });

        reversed.Status.Should().Be(VendorPaymentStatus.Reversed);
        reversed.JournalEntryId.Should().Be(posted.JournalEntryId);
        reversed.ReversalJournalEntryId.Should().NotBeNull();
        reversed.ReversalPostingEventId.Should().NotBeNull();
        reversed.ReversalDate.Should().Be(reversalDate);
        reversed.ReversalReason.Should().Be(reversalReason);
        reversed.AllocatedAmount.Should().Be(0m);

        var originalJournal = await db.JournalEntries
            .Include(journal => journal.Transactions)
            .SingleAsync(journal => journal.Id == posted.JournalEntryId);
        var reversalJournal = await db.JournalEntries
            .Include(journal => journal.Transactions)
            .SingleAsync(journal => journal.Id == reversed.ReversalJournalEntryId);

        // The reversal is a linked compensating journal; the original evidence is retained and
        // every line is inverted rather than being edited or deleted.
        originalJournal.IsReversed.Should().BeTrue();
        originalJournal.ReversalJournalEntryId.Should().Be(reversalJournal.Id);
        reversalJournal.OriginalJournalEntryId.Should().Be(originalJournal.Id);
        reversalJournal.Transactions.Should().HaveCount(originalJournal.Transactions.Count);
        foreach (var originalLine in originalJournal.Transactions)
        {
            var reversedLine = reversalJournal.Transactions.Single(line =>
                line.OriginalTransactionId == originalLine.Id);
            reversedLine.DebitAmount.Should().Be(originalLine.CreditAmount);
            reversedLine.CreditAmount.Should().Be(originalLine.DebitAmount);
        }

        var invoice = await db.Set<VendorInvoice>().SingleAsync(item => item.Id == fixture.Invoice.Id);
        invoice.PaidAmount.Should().Be(0m);
        invoice.Status.Should().Be(VendorInvoiceStatus.Approved);

        var allocations = await db.Set<VendorPaymentAllocation>()
            .Where(item => item.VendorPaymentId == fixture.Payment.Id)
            .OrderBy(item => item.IsReversal)
            .ToListAsync();
        allocations.Should().HaveCount(2);
        allocations.Single(item => item.IsReversal).OriginalAllocationId.Should().Be(fixture.Allocation.Id);
        allocations.Single(item => item.IsReversal).AllocatedAmount.Should().Be(-fixture.Allocation.AllocatedAmount);

        (await db.AuditLogs.CountAsync(a =>
            a.Action == FinanceAuditEvents.ApPaymentReversed &&
            a.TenantId == tenantId)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task DuplicateApPaymentReversal_ShouldReturnExistingCompensatingPosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);
        await service.PostAsync(fixture.Payment.Id);
        var command = new ReverseVendorPaymentDto
        {
            Reason = "Duplicate supplier payment identified during AP review.",
            // See the reversal test above: the default policy deliberately rejects prior periods.
            ReversalDate = DateTime.UtcNow.Date
        };

        var first = await service.ReversePaymentAsync(fixture.Payment.Id, command);
        var second = await service.ReversePaymentAsync(fixture.Payment.Id, command);

        second.ReversalJournalEntryId.Should().Be(first.ReversalJournalEntryId);
        second.ReversalPostingEventId.Should().Be(first.ReversalPostingEventId);
        (await db.FinancePostingEvents.CountAsync(item =>
            item.SourceDocumentType == "VendorPayment" &&
            item.SourceDocumentId == fixture.Payment.Id &&
            item.PostingAction == "Reverse")).Should().Be(1);
        (await db.Set<VendorPaymentAllocation>().CountAsync(item =>
            item.VendorPaymentId == fixture.Payment.Id && item.IsReversal)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task ReversedAllocation_ShouldBlockManualPaymentAuthorization()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(
            db,
            tenantId,
            payment => payment.Status = VendorPaymentStatus.Draft);
        var (service, _) = CreateService(db, tenantId);
        await service.ReverseAllocationAsync(fixture.Allocation.Id, "replace incorrect allocation");

        var submit = () => service.SubmitForAuthorizationAsync(fixture.Payment.Id);

        var error = await submit.Should().ThrowAsync<VendorPaymentControlException>();
        error.Which.Code.Should().Be("AP_PAYMENT_ALLOCATION_TOTAL_MISMATCH");
        (await db.Set<VendorPayment>().SingleAsync(item => item.Id == fixture.Payment.Id))
            .Status.Should().Be(VendorPaymentStatus.Draft);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task ReversedAllocation_ShouldNotPostOrSettleItsOriginalInvoice()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);
        await service.ReverseAllocationAsync(fixture.Allocation.Id, "allocation withdrawn");

        var post = () => service.PostAsync(fixture.Payment.Id);

        var error = await post.Should().ThrowAsync<VendorPaymentControlException>();
        error.Which.Code.Should().Be("AP_PAYMENT_ALLOCATION_TOTAL_MISMATCH");
        (await db.Set<VendorInvoice>().SingleAsync(item => item.Id == fixture.Invoice.Id))
            .PaidAmount.Should().Be(0m);
        (await db.FinancePostingEvents.CountAsync(item =>
            item.SourceDocumentType == "VendorPayment" && item.SourceDocumentId == fixture.Payment.Id))
            .Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task AllocationReversal_ShouldBeSingleUseAndRecomputeEffectiveDiscount()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(
            db,
            tenantId,
            payment =>
            {
                payment.Status = VendorPaymentStatus.Draft;
                payment.DiscountTaken = 13m;
            });
        fixture.Allocation.DiscountAmount = 10m;
        db.Set<VendorPaymentAllocation>().Add(new VendorPaymentAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VendorPaymentId = fixture.Payment.Id,
            VendorInvoiceId = fixture.Invoice.Id,
            AllocatedAmount = 0m,
            DiscountAmount = 3m,
            AllocationDate = fixture.Payment.PaymentDate,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        await service.ReverseAllocationAsync(fixture.Allocation.Id, "replace first allocation");
        var duplicate = () => service.ReverseAllocationAsync(
            fixture.Allocation.Id,
            "duplicate reversal attempt");

        var exception = await duplicate.Should().ThrowAsync<VendorPaymentControlException>();
        exception.Which.Code.Should().Be("AP_PAYMENT_ALLOCATION_ALREADY_REVERSED");
        (await db.Set<VendorPaymentAllocation>().CountAsync(item =>
            item.IsReversal && item.OriginalAllocationId == fixture.Allocation.Id)).Should().Be(1);
        (await db.Set<VendorPayment>().SingleAsync(item => item.Id == fixture.Payment.Id))
            .DiscountTaken.Should().Be(3m);

        var uniqueness = db.Model.FindEntityType(typeof(VendorPaymentAllocation))!
            .GetIndexes()
            .Single(index => index.GetDatabaseName() ==
                "UX_VendorPaymentAllocation_TenantId_OriginalAllocationId_Reversal");
        uniqueness.IsUnique.Should().BeTrue();
        uniqueness.GetFilter().Should().Be(
            "[IsReversal] = 1 AND [OriginalAllocationId] IS NOT NULL");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task ApprovedBatchOwnedAllocation_ShouldRejectDirectReversal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var batch = new PaymentBatch
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BatchNumber = "PB-LOCKED-001",
            Status = PaymentBatchStatus.Approved,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        var fixture = await SeedApprovedApPaymentAsync(
            db,
            tenantId,
            payment =>
            {
                payment.Status = VendorPaymentStatus.Authorized;
                payment.PaymentBatchId = batch.Id;
                payment.PaymentBatch = batch;
            });
        var (service, _) = CreateService(db, tenantId);

        var reverse = () => service.ReverseAllocationAsync(
            fixture.Allocation.Id,
            "direct batch mutation");

        var exception = await reverse.Should().ThrowAsync<VendorPaymentControlException>();
        exception.Which.Code.Should().Be("AP_PAYMENT_BATCH_ALLOCATION_FROZEN");
        (await db.Set<VendorPaymentAllocation>().CountAsync(item => item.IsReversal))
            .Should().Be(0);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ap-payment-posting-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static (VendorPaymentService Service, Mock<ISubledgerPostingService> SubledgerPostingMock) CreateService(
        ApplicationDbContext db,
        Guid tenantId,
        IFxAccountingService? fxAccountingService = null,
        IWithholdingTaxCertificateService? withholdingTaxService = null,
        IExchangeRateService? exchangeRateService = null,
        IWorkflowService? workflowService = null,
        IWorkflowApprovalPolicyResolver? approvalPolicyResolver = null,
        bool useRealPaymentSod = false,
        ICentralDocumentRepositoryFileService? paymentEvidenceFiles = null,
        IControlledFileUploadService? paymentEvidenceUploader = null,
        IFinanceAccessScopeService? paymentEvidenceAccess = null,
        IFinanceSourceBookAuthorityService? sourceBookAuthorities = null)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var auditService = new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-ap-payment-posting" }
            });
        var postingEngine = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            auditService);
        var subledgerPostingMock = new Mock<ISubledgerPostingService>();
        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");
        var invoicePaymentSod = new Mock<IProcurementInvoicePaymentSodService>();
        invoicePaymentSod
            .Setup(x => x.RevalidatePaymentAuthorizationAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var procurementControlEvents = new Mock<IProcurementControlEventService>();
        procurementControlEvents
            .Setup(x => x.RecordAsync(
                It.IsAny<ProcurementControlEventWriteRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementControlEventWriteRequest request, CancellationToken _) =>
                new ProcurementControlEventDto
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    EventKey = request.EventKey,
                    EventType = request.EventType,
                    Action = request.Action,
                    Result = request.Result,
                    SourceType = request.SourceType,
                    SourceId = request.SourceId,
                    SourceReference = request.SourceReference ?? string.Empty,
                    CorrelationId = request.CorrelationId,
                    OccurredAtUtc = request.OccurredAtUtc == default
                        ? DateTime.UtcNow
                        : request.OccurredAtUtc,
                    RecordedAtUtc = DateTime.UtcNow,
                    IntegrityValid = true
                });

        var numbering = new Mock<IDocumentNumberingService>();
        numbering.Setup(value => value.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(),
            It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"VP-{Guid.NewGuid():N}");
        var service = new VendorPaymentService(
            new UnitOfWork(db),
            currentUser.Object,
            tenantSettings.Object,
            Mock.Of<ILogger<VendorPaymentService>>(),
            numbering.Object,
            workflowService ?? Mock.Of<IWorkflowService>(),
            // Existing posting tests run with access-scope enforcement disabled. The no-op mock
            // isolates those posting assertions while dedicated scope tests exercise fail-closed
            // enforcement separately.
            paymentEvidenceAccess ?? Mock.Of<IFinanceAccessScopeService>(),
            new FinanceReversalPolicyService(db, currentUser.Object),
            postingEngine,
            auditService,
            fxAccountingService: fxAccountingService,
            approvalPolicyResolver: approvalPolicyResolver,
            withholdingTaxService: withholdingTaxService,
            procurementControlEvents: procurementControlEvents.Object,
            invoicePaymentSod: useRealPaymentSod
                ? new ProcurementInvoicePaymentSodService(new UnitOfWork(db), currentUser.Object,
                    Mock.Of<IProcurementSodGuardService>(), procurementControlEvents.Object,
                    Mock.Of<ILogger<ProcurementInvoicePaymentSodService>>())
                : invoicePaymentSod.Object,
            exchangeRateService: exchangeRateService,
            controlledFiles: paymentEvidenceUploader,
            centralDocuments: paymentEvidenceFiles,
            sourceBookAuthorities: sourceBookAuthorities);

        return (service, subledgerPostingMock);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId)
    {
        var userId = Guid.NewGuid().ToString();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(userId);
        currentUser.SetupGet(x => x.UserName).Returns("ap.payment.poster");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("ap-payment-posting-tests");
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        return currentUser;
    }

    private static async Task<ApPaymentFixture> SeedApprovedApPaymentAsync(
        ApplicationDbContext db,
        Guid tenantId,
        Action<VendorPayment>? configurePayment = null,
        Action<VendorInvoice>? configureInvoice = null,
        decimal allocationAmount = 100m,
        bool seedInvoicePostingEvent = true,
        bool periodIsOpen = true,
        bool periodIsClosed = false,
        string accountingBookCode = "IFRS")
    {
        SeedTenant(db, tenantId, accountingBookCode: accountingBookCode);
        var period = SeedOpenPeriod(db, tenantId, periodIsOpen, periodIsClosed, accountingBookCode);
        SeedCurrentOperationalPeriodWhenNeeded(db, tenantId, period, accountingBookCode);
        var apAccount = SeedAccount(db, tenantId, "2000", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        var bankGlAccount = SeedAccount(db, tenantId, "1100", AccountType.Asset);
        var expenseAccount = SeedAccount(db, tenantId, "6000", AccountType.Expense);
        var taxAccount = SeedAccount(db, tenantId, "2300", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        var discountAccount = SeedAccount(db, tenantId, "5100", AccountType.Revenue);
        var supplier = SeedSupplier(db, tenantId, apAccount.Id);
        var supplierRole = db.Set<BusinessPartnerRole>().Local.Single(role =>
            role.TenantId == tenantId && role.BusinessPartnerId == supplier.Id &&
            role.RoleType == BusinessPartnerRoleType.Supplier);
        var supplierProfile = db.Set<BusinessPartnerApProfileVersion>().Local.Single(profile =>
            profile.TenantId == tenantId && profile.BusinessPartnerRoleId == supplierRole.Id);
        var bankAccount = SeedBankAccount(db, tenantId, bankGlAccount.Id);

        db.Set<FinanceSettings>().Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            ControlAccountApId = apAccount.Id,
            ControlAccountTaxId = taxAccount.Id,
            DiscountReceivedAccountId = discountAccount.Id,
            DefaultBankAccountId = bankAccount.Id
        });

        var invoice = SeedPostedInvoice(db, tenantId, supplier, apAccount, "VI-2026-00001", new DateTime(2026, 7, 5), 100m, period.Id, seedInvoicePostingEvent);
        invoice.ExpenseAccountId = expenseAccount.Id;
        configureInvoice?.Invoke(invoice);

        var payment = new VendorPayment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PaymentNumber = "VP-2026-00001",
            BusinessPartnerId = supplier.Id,
            BusinessPartnerRoleId = supplierRole.Id,
            BusinessPartnerApProfileVersionId = supplierProfile.Id,
            BusinessPartnerCode = supplier.PartnerCode,
            BusinessPartnerName = supplier.PartnerName,
            BusinessPartnerLegalName = supplier.LegalName,
            BusinessPartnerTaxIdentificationNumber = supplier.TaxIdentificationNumber,
            PaymentDate = new DateTime(2026, 7, 5),
            TotalAmount = allocationAmount,
            AllocatedAmount = allocationAmount,
            PaymentMethod = VendorPaymentMethod.BankTransfer,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BankAccountId = bankAccount.Id,
            Status = VendorPaymentStatus.Authorized,
            AuthorizedById = Guid.NewGuid(),
            AuthorizedDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        configurePayment?.Invoke(payment);

        var allocation = new VendorPaymentAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VendorPaymentId = payment.Id,
            VendorInvoiceId = invoice.Id,
            AllocatedAmount = allocationAmount,
            AllocationDate = payment.PaymentDate,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        invoice.PaidAmount = 0m;
        invoice.Status = VendorInvoiceStatus.Approved;

        db.Set<VendorPayment>().Add(payment);
        db.Set<VendorPaymentAllocation>().Add(allocation);
        await db.SaveChangesAsync();

        return new ApPaymentFixture(payment, allocation, invoice, supplier, apAccount, bankGlAccount, bankAccount);
    }

    private static void SeedTenant(
        ApplicationDbContext db,
        Guid tenantId,
        string code = "TEN",
        string accountingBookCode = "IFRS")
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {code}",
            Code = code,
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });
        db.AccountingBooks.Add(new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = accountingBookCode, Name = $"{accountingBookCode} Primary",
            Purpose = "Primary", BookType = AccountingBookType.PrimaryFull,
            LifecycleStatus = AccountingBookLifecycleStatus.Active, FunctionalCurrencyCode = "GHS",
            IsDefault = true, IsActive = true, AllowsPosting = true
        });
    }

    private static FiscalPeriod SeedOpenPeriod(
        ApplicationDbContext db,
        Guid tenantId,
        bool isOpen = true,
        bool isClosed = false,
        string accountingBookCode = "IFRS")
    {
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = Guid.NewGuid(),
            PeriodName = "July 2026",
            PeriodCode = "2026-07",
            PeriodNumber = 7,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31),
            PeriodDays = 31,
            PeriodStatus = isClosed ? "Closed" : isOpen ? "Open" : "Future",
            IsOpen = isOpen,
            IsClosed = isClosed,
            IsLocked = false
        };

        db.FiscalPeriods.Add(period);
        FinancePostingAuthorityFixture.SeedExactBookPeriod(db, tenantId, period, accountingBookCode);
        return period;
    }

    private static void SeedCurrentOperationalPeriodWhenNeeded(
        ApplicationDbContext db,
        Guid tenantId,
        FiscalPeriod seededPeriod,
        string accountingBookCode = "IFRS")
    {
        var today = DateTime.UtcNow.Date;
        if (today >= seededPeriod.StartDate.Date && today <= seededPeriod.EndDate.Date)
            return;

        var start = new DateTime(today.Year, today.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var currentPeriod = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = seededPeriod.FiscalYearId,
            PeriodName = start.ToString("MMMM yyyy"),
            PeriodCode = start.ToString("yyyy-MM"),
            PeriodNumber = start.Month,
            PeriodType = PeriodType.Monthly,
            StartDate = start,
            EndDate = end,
            PeriodDays = (end - start).Days + 1,
            PeriodStatus = seededPeriod.IsClosed ? "Closed" : seededPeriod.IsOpen ? "Open" : "Future",
            IsOpen = seededPeriod.IsOpen,
            IsClosed = seededPeriod.IsClosed,
            IsLocked = seededPeriod.IsLocked
        };
        db.FiscalPeriods.Add(currentPeriod);
        FinancePostingAuthorityFixture.SeedExactBookPeriod(db, tenantId, currentPeriod, accountingBookCode);
    }

    private static Account SeedAccount(
        ApplicationDbContext db,
        Guid tenantId,
        string accountNumber,
        AccountType accountType,
        AccountStatus status = AccountStatus.Active,
        bool isControlAccount = false,
        bool allowDirectPosting = true)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = accountNumber,
            AccountNumber = accountNumber,
            AccountName = $"Account {accountNumber}",
            AccountType = accountType,
            Status = status,
            CurrencyCode = "GHS",
            IsControlAccount = isControlAccount,
            AllowDirectPosting = allowDirectPosting
        };

        db.Accounts.Add(account);
        var book = db.AccountingBooks.Local.Single(item => item.TenantId == tenantId && item.IsDefault);
        FinancePostingAuthorityFixture.SeedEnabledBookMappings(db, tenantId, book, account);
        return account;
    }

    private static void EnableCurrencyForAccounts(
        ApplicationDbContext db,
        Guid tenantId,
        string currencyCode,
        params Account[] accounts)
    {
        foreach (var account in accounts)
        {
            // Subledger control and deduction accounts may receive several invoice currencies.
            // Model that production configuration explicitly instead of weakening posting-engine
            // currency validation for this regression fixture.
            account.IsMultiCurrency = true;
            db.AccountCurrencyLinks.Add(new AccountCurrencyLink
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountId = account.Id,
                LinkedCurrencyCode = currencyCode,
                TransactionRateType = "Daily",
                IsActive = true,
                EffectiveDate = new DateTime(2026, 1, 1),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed"
            });
        }
    }

    private static BusinessPartner SeedSupplier(ApplicationDbContext db, Guid tenantId, Guid apAccountId)
    {
        var supplier = new BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerCode = $"SUP-{tenantId.ToString("N")[..6]}",
            PartnerName = "Test Supplier",
            LegalName = "Test Supplier Limited",
            TaxIdentificationNumber = $"TIN-{tenantId.ToString("N")[..8]}",
            PartnerType = "Supplier",
            RegistrationStatus = "Approved",
            ApprovalStatus = "Approved",
            Currency = "GHS",
            IsActive = true,
            DefaultApAccountId = apAccountId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        var role = new BusinessPartnerRole
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BusinessPartnerId = supplier.Id,
            RoleType = BusinessPartnerRoleType.Supplier, Status = BusinessPartnerRoleStatus.Active,
            ActiveFromUtc = new DateTime(2025, 1, 1), CreatedAt = DateTime.UtcNow, CreatedBy = "seed"
        };
        var profile = new BusinessPartnerApProfileVersion
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BusinessPartnerRoleId = role.Id,
            VersionNumber = 1, Status = BusinessPartnerFinanceProfileStatus.Approved,
            EffectiveFrom = new DateTime(2025, 1, 1), ApprovedAtUtc = DateTime.UtcNow,
            ApprovedById = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, CreatedBy = "seed"
        };

        db.BusinessPartners.Add(supplier);
        db.Set<BusinessPartnerRole>().Add(role);
        db.Set<BusinessPartnerApProfileVersion>().Add(profile);
        return supplier;
    }

    private static BankAccount SeedBankAccount(ApplicationDbContext db, Guid tenantId, Guid glAccountId)
    {
        var bankAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountNumber = $"BANK-{tenantId.ToString("N")[..6]}",
            AccountName = "Operating Bank",
            BankName = "Test Bank",
            Currency = "GHS",
            AccountType = BankAccountType.Checking,
            GLAccountId = glAccountId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        db.BankAccounts.Add(bankAccount);
        return bankAccount;
    }

    private static VendorInvoice SeedPostedInvoice(
        ApplicationDbContext db,
        Guid tenantId,
        BusinessPartner supplier,
        Account apAccount,
        string invoiceNumber,
        DateTime invoiceDate,
        decimal amount,
        Guid? fiscalPeriodId = null,
        bool seedPostingEvent = true)
    {
        var book = db.AccountingBooks.Local.Single(item => item.TenantId == tenantId && item.IsDefault);
        var role = db.Set<BusinessPartnerRole>().Local.FirstOrDefault(item =>
            item.TenantId == tenantId && item.BusinessPartnerId == supplier.Id &&
            item.RoleType == BusinessPartnerRoleType.Supplier);
        var profile = role == null ? null : db.Set<BusinessPartnerApProfileVersion>().Local.FirstOrDefault(item =>
            item.TenantId == tenantId && item.BusinessPartnerRoleId == role.Id);
        var invoice = new VendorInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = invoiceNumber,
            SupplierInvoiceNumber = invoiceNumber,
            BusinessPartnerId = supplier.Id,
            BusinessPartnerRoleId = role?.Id,
            BusinessPartnerApProfileVersionId = profile?.Id,
            SupplierName = supplier.PartnerName,
            InvoiceDate = invoiceDate,
            ReceivedDate = invoiceDate,
            DueDate = invoiceDate.AddDays(30),
            SubTotal = amount,
            TotalAmount = amount,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BaseCurrencyAmount = amount,
            Status = VendorInvoiceStatus.Approved,
            ApprovalStatus = "Approved",
            ApprovedById = Guid.NewGuid(),
            ApprovedDate = DateTime.UtcNow,
            ApAccountId = apAccount.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var invoiceJournal = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JournalEntryNumber = $"JE-{invoiceNumber}",
            JournalType = "AP Invoice",
            EntryDate = invoiceDate,
            Description = $"Posted invoice {invoiceNumber}",
            ReferenceNumber = invoiceNumber,
            SourceModule = "AP",
            SourceDocumentId = invoice.Id,
            SourceDocumentType = "VendorInvoice",
            TotalDebitAmount = amount,
            TotalCreditAmount = amount,
            IsBalanced = true,
            FiscalPeriodId = fiscalPeriodId ?? Guid.NewGuid(),
            AccountingBookId = book.Id,
            PostingStatus = "Posted",
            ApprovalStatus = "Approved",
            PostingDate = invoiceDate,
            BookClassification = book.Code,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        invoice.JournalEntryId = invoiceJournal.Id;

        db.VendorInvoices.Add(invoice);
        db.JournalEntries.Add(invoiceJournal);

        if (seedPostingEvent)
        {
            db.FinancePostingEvents.Add(new FinancePostingEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SourceModule = "AP",
                SourceDocumentType = "VendorInvoice",
                SourceDocumentId = invoice.Id,
                PostingAction = "Post",
                SourceDocumentReference = invoice.InvoiceNumber,
                IdempotencyKey = $"AP:VendorInvoice:{tenantId:N}:{invoice.Id:N}:Post",
                JournalEntryId = invoiceJournal.Id,
                AccountingBookId = book.Id,
                PostingStatus = "Posted",
                PostingDate = invoiceDate,
                RequestedAt = DateTime.UtcNow,
                PostedAt = DateTime.UtcNow,
                TotalDebitAmount = amount,
                TotalCreditAmount = amount,
                FunctionalCurrencyCode = "GHS",
                BookClassification = book.Code,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed"
            });
        }

        return invoice;
    }

    private sealed record ApPaymentFixture(
        VendorPayment Payment,
        VendorPaymentAllocation Allocation,
        VendorInvoice Invoice,
        BusinessPartner Supplier,
        Account ApAccount,
        Account BankGlAccount,
        BankAccount BankAccount);
}
