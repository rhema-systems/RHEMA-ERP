using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.AR;
using ErpSystem.Api.Services.Finance.Cash;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
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

public sealed partial class ArReceiptPostingMigrationTests
{
    [Theory]
    [InlineData(InvoiceStatus.Draft)]
    [InlineData(InvoiceStatus.PendingApproval)]
    [InlineData(InvoiceStatus.Approved)]
    [InlineData(InvoiceStatus.Rejected)]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public void ReceiptAllocation_ShouldRejectInvoiceBeforeItBecomesCollectible(InvoiceStatus status)
    {
        var invoice = new Invoice
        {
            InvoiceNumber = "INV-REVIEW-001",
            Status = status,
            TotalAmount = 100m
        };

        var action = () => PaymentService.EnsureInvoiceCollectibleForReceipt(invoice);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"*not collectible*{status}*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public void OpeningReceipt_ShouldRequirePostingEvidenceButRemainCollectibleAfterPosting()
    {
        var invoice = new Invoice
        {
            InvoiceNumber = "INV-OPEN-001",
            Status = InvoiceStatus.Sent,
            IsOpeningBalance = true,
            TotalAmount = 100m
        };

        var unpostedAction = () => PaymentService.EnsureInvoiceCollectibleForReceipt(invoice);
        unpostedAction.Should().Throw<InvalidOperationException>()
            .WithMessage("*no posting evidence*");

        invoice.JournalEntryId = Guid.NewGuid();
        var postedAction = () => PaymentService.EnsureInvoiceCollectibleForReceipt(invoice);
        postedAction.Should().NotThrow();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ApprovedArReceipt_ShouldPostThroughFinancePostingEngineAndCreateAuditEvent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        var (service, subledgerPostingMock) = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.Payment.Id);

        result.JournalEntryId.Should().NotBeNull();
        subledgerPostingMock.Verify(x => x.PostArPaymentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        var postingEvent = await db.FinancePostingEvents.SingleAsync(e =>
            e.TenantId == tenantId &&
            e.SourceModule == "AR" &&
            e.SourceDocumentType == "CustomerPayment" &&
            e.SourceDocumentId == fixture.Payment.Id &&
            e.PostingAction == "Post");
        postingEvent.JournalEntryId.Should().Be(result.JournalEntryId);
        postingEvent.OriginModuleCode.Should().Be(FinanceModuleLockCatalog.Finance);

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == result.JournalEntryId);
        journal.PostingStatus.Should().Be("Posted");
        journal.SourceModule.Should().Be("AR");
        journal.SourceDocumentType.Should().Be("CustomerPayment");
        journal.Transactions.Should().HaveCount(2);
        journal.Transactions.Single(t => t.AccountId == fixture.BankGlAccount.Id).DebitAmount.Should().Be(100m);
        journal.Transactions.Single(t => t.AccountId == fixture.ArAccount.Id).CreditAmount.Should().Be(100m);

        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.ArReceiptPosted && a.TenantId == tenantId)).Should().Be(1);
        (await db.AccountBalances.SingleAsync(x => x.AccountId == fixture.ArAccount.Id)).ClosingBalance.Should().Be(-100m);
        (await db.AccountBalances.SingleAsync(x => x.AccountId == fixture.BankGlAccount.Id)).ClosingBalance.Should().Be(100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CrossCurrencyDeductions")]
    [Trait("Category", "AccountsReceivable")]
    public async Task CrossCurrencyArReceipt_ShouldPostEveryDeductionAtItsFrozenFunctionalValue()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        var withholdingReceivable = SeedAccount(
            db,
            tenantId,
            "1301",
            AccountType.Asset,
            isControlAccount: true,
            allowDirectPosting: false);
        var vatWithholdingReceivable = SeedAccount(
            db,
            tenantId,
            "1302",
            AccountType.Asset,
            isControlAccount: true,
            allowDirectPosting: false);
        var discountAccountId = (await db.Set<FinanceSettings>().SingleAsync()).DiscountAllowedAccountId!.Value;
        var discountAccount = await db.Accounts.SingleAsync(account => account.Id == discountAccountId);
        EnableCurrencyForAccounts(
            db,
            tenantId,
            "USD",
            fixture.ArAccount,
            discountAccount,
            withholdingReceivable,
            vatWithholdingReceivable);
        var settlementRate = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrencyCode = "GHS",
            TargetCurrencyCode = "USD",
            // Tenant rates are stored as 1 functional-currency unit = N transaction-currency
            // units. Posting snapshots use the reciprocal transaction-to-functional multiplier.
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

        // A GHS receipt settles a USD invoice. Native USD deductions drive the customer aging
        // reduction, while the frozen functional snapshots drive the four-account GHS journal.
        fixture.Payment.TotalAmount = 875m;
        fixture.Payment.AllocatedAmount = 875m;
        fixture.Payment.CurrencyCode = "GHS";
        fixture.Payment.ExchangeRate = 1m;
        fixture.Payment.WithholdingTaxAccountId = withholdingReceivable.Id;
        fixture.Payment.VatWithholdingAccountId = vatWithholdingReceivable.Id;
        fixture.Invoice.CurrencyCode = "USD";
        fixture.Invoice.ExchangeRate = 10m;
        fixture.Invoice.TotalAmount = 80m;
        fixture.Invoice.BaseCurrencyAmount = 800m;
        fixture.Invoice.PaidAmount = 80m;
        fixture.Allocation.AllocatedAmount = 70m;
        fixture.Allocation.DiscountAmount = 2m;
        fixture.Allocation.WithholdingTaxAmount = 3m;
        fixture.Allocation.VatWithholdingAmount = 5m;
        fixture.Allocation.PaymentCurrencyCode = "GHS";
        fixture.Allocation.InvoiceCurrencyCode = "USD";
        fixture.Allocation.PaymentCurrencyAmount = 875m;
        fixture.Allocation.PaymentExchangeRate = 1m;
        fixture.Allocation.PaymentFunctionalAmount = 875m;
        fixture.Allocation.InvoiceSettlementExchangeRate = 12.5m;
        fixture.Allocation.InvoiceSettlementExchangeRateId = settlementRate.Id;
        fixture.Allocation.DiscountFunctionalAmount = 25m;
        fixture.Allocation.WithholdingTaxFunctionalAmount = 37.50m;
        fixture.Allocation.VatWithholdingFunctionalAmount = 62.50m;
        fixture.Allocation.SettlementFunctionalAmount = 1_000m;
        fixture.Allocation.IsCrossCurrency = true;
        await db.SaveChangesAsync();

        var fx = new Mock<IFxAccountingService>();
        fx.Setup(service => service.PostRealizedFxForArReceiptAsync(
                fixture.Payment.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FxRealizedSettlement>());
        var (service, _) = CreateService(db, tenantId, fx.Object);

        var result = await service.PostAsync(fixture.Payment.Id);

        var journal = await db.JournalEntries
            .Include(entry => entry.Transactions)
            .SingleAsync(entry => entry.Id == result.JournalEntryId);
        journal.Transactions.Single(line => line.AccountId == fixture.ArAccount.Id).CreditAmount.Should().Be(1_000m);
        journal.Transactions.Single(line => line.TransactionTag == "AR-Discount").Should().Match<AccountTransaction>(line =>
            line.AccountId == discountAccountId && line.DebitAmount == 25m &&
            line.TransactionCurrency == "USD" && line.TransactionDebitAmount == 2m);
        journal.Transactions.Single(line => line.TransactionTag == "AR-WHT").DebitAmount.Should().Be(37.50m);
        journal.Transactions.Single(line => line.TransactionTag == "AR-VAT-WHT").DebitAmount.Should().Be(62.50m);
        journal.Transactions.Single(line => line.AccountId == fixture.BankGlAccount.Id).DebitAmount.Should().Be(875m);
        journal.TotalDebitAmount.Should().Be(1_000m);
        journal.TotalCreditAmount.Should().Be(1_000m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task CustomerAdvance_ShouldPostAndApplyThroughFinancePostingEngine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        var customerAdvanceAccount = SeedAccount(db, tenantId, "2301", AccountType.Liability);
        var settings = await db.Set<FinanceSettings>().SingleAsync(s => s.TenantId == tenantId);
        settings.CustomerAdvanceAccountId = customerAdvanceAccount.Id;
        db.Remove(fixture.Allocation);
        fixture.Payment.AllocatedAmount = 0m;
        fixture.Invoice.PaidAmount = 0m;
        fixture.Invoice.Status = InvoiceStatus.Sent;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var (service, subledgerPostingMock) = CreateService(db, tenantId);

        var initialPosting = await service.PostAsync(fixture.Payment.Id);
        var application = await service.AllocatePaymentAsync(new PaymentAllocation_CreateDto
        {
            CustomerPaymentId = fixture.Payment.Id,
            Allocations = new List<InvoiceAllocationDto>
            {
                new() { InvoiceId = fixture.Invoice.Id, AllocatedAmount = 100m }
            }
        });

        (await db.Set<CustomerPayment>().SingleAsync(p => p.Id == fixture.Payment.Id)).IsCustomerAdvance.Should().BeTrue();
        (await db.JournalEntries.SingleAsync(j => j.Id == initialPosting.JournalEntryId)).SourceDocumentType.Should().Be("CustomerPayment");
        var allocation = await db.Set<PaymentAllocation>().SingleAsync(a => a.Id == application.Allocations.Single().Id);
        allocation.ApplicationJournalEntryId.Should().NotBeNull();
        allocation.ApplicationPostingEventId.Should().NotBeNull();
        var applicationJournal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == allocation.ApplicationJournalEntryId);
        applicationJournal.SourceDocumentType.Should().Be("CustomerPaymentAdvanceApplication");
        applicationJournal.Transactions.Single(t => t.AccountId == customerAdvanceAccount.Id).DebitAmount.Should().Be(100m);
        applicationJournal.Transactions.Single(t => t.AccountId == fixture.ArAccount.Id).CreditAmount.Should().Be(100m);
        subledgerPostingMock.Verify(x => x.PostArPaymentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ForeignCustomerAdvance_ShouldApplyAtFrozenLotAndCurrentInvoiceRatesThenReverseImmutably()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        var customerAdvanceAccount = SeedAccount(db, tenantId, "2301", AccountType.Liability);
        var realizedFxLossAccount = SeedAccount(db, tenantId, "6700", AccountType.Expense);
        var settings = await db.Set<FinanceSettings>().SingleAsync(s => s.TenantId == tenantId);
        settings.CustomerAdvanceAccountId = customerAdvanceAccount.Id;
        settings.RealizedFxLossAccountId = realizedFxLossAccount.Id;

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

        // The receipt is a USD 10 liability lot at GHS 10. Applying it to EUR 8 at GHS 13
        // requires a GHS 4 realized loss so the AR control credit remains fully balanced.
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
        fixture.Invoice.Status = InvoiceStatus.Sent;
        EnableCurrencyForAccounts(db, tenantId, "USD", fixture.BankGlAccount, customerAdvanceAccount);
        EnableCurrencyForAccounts(db, tenantId, "EUR", fixture.ArAccount);
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

        // Tax deductions are distinct accounting documents and must never disappear into the
        // cash-only advance reclassification. This guards the service boundary as well as the UI.
        var taxDeductionAttempt = () => service.AllocatePaymentAsync(new PaymentAllocation_CreateDto
        {
            CustomerPaymentId = fixture.Payment.Id,
            Allocations = new List<InvoiceAllocationDto>
            {
                new()
                {
                    InvoiceId = fixture.Invoice.Id,
                    AllocatedAmount = 8m,
                    PaymentCurrencyAmount = 10m,
                    WithholdingTaxAmount = 1m,
                    InvoiceSettlementExchangeRateId = applicationRate.Id
                }
            }
        });
        await taxDeductionAttempt.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cash allocations only*");

        var application = await service.AllocatePaymentAsync(new PaymentAllocation_CreateDto
        {
            CustomerPaymentId = fixture.Payment.Id,
            Allocations = new List<InvoiceAllocationDto>
            {
                new()
                {
                    InvoiceId = fixture.Invoice.Id,
                    AllocatedAmount = 8m,
                    PaymentCurrencyAmount = 10m,
                    InvoiceSettlementExchangeRateId = applicationRate.Id
                }
            }
        });

        var allocation = await db.Set<PaymentAllocation>()
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
        applicationJournal.Transactions.Single(line => line.AccountId == customerAdvanceAccount.Id).DebitAmount.Should().Be(100m);
        applicationJournal.Transactions.Single(line => line.AccountId == realizedFxLossAccount.Id).DebitAmount.Should().Be(4m);
        applicationJournal.Transactions.Single(line => line.AccountId == fixture.ArAccount.Id).CreditAmount.Should().Be(104m);

        await service.ReverseAllocationAsync(
            allocation.Id,
            "Customer advance application entered against the wrong invoice");

        var allocationFacts = await db.Set<PaymentAllocation>()
            .Where(item => item.CustomerPaymentId == fixture.Payment.Id)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync();
        allocationFacts.Should().HaveCount(2);
        allocationFacts.Single(item => item.IsReversal).Should().Match<PaymentAllocation>(item =>
            item.OriginalAllocationId == allocation.Id &&
            item.AllocatedAmount == -8m &&
            item.PaymentCurrencyAmount == -10m &&
            item.PaymentFunctionalAmount == -100m &&
            item.SettlementFunctionalAmount == -104m);
        (await db.Set<CustomerPayment>().SingleAsync(item => item.Id == fixture.Payment.Id)).AllocatedAmount.Should().Be(0m);
        (await db.Set<Invoice>().SingleAsync(item => item.Id == fixture.Invoice.Id)).PaidAmount.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task UnapprovedArReceipt_ShouldNotPost()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId, payment => payment.Status = "PendingApproval");
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR receipt workflow approval is not complete.");
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentType == "CustomerPayment")).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ReceiptAgainstUnpostedArInvoice_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId, configureInvoice: invoice => invoice.JournalEntryId = null, seedInvoicePostingEvent: false);
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"AR receipt cannot settle unposted invoice '{fixture.Invoice.InvoiceNumber}'.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task OverSettledArInvoice_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId, payment => payment.TotalAmount = 125m, allocationAmount: 125m);
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"AR receipt would over-settle invoice '{fixture.Invoice.InvoiceNumber}'.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ReceiptAndPostedSalesCreditNoteCannotOverSettleAnInvoice()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId, allocationAmount: 50m);
        SeedPostedAppliedSalesCreditNote(db, tenantId, fixture.Invoice.Id, amount: 60m);
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"AR receipt would over-settle invoice '{fixture.Invoice.InvoiceNumber}'.");
        (await db.FinancePostingEvents.CountAsync(e =>
            e.SourceDocumentType == "CustomerPayment" &&
            e.SourceDocumentId == fixture.Payment.Id)).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task CrossTenantCustomer_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherArAccount = SeedAccount(db, otherTenantId, "1200", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var otherCustomer = SeedCustomer(db, otherTenantId, otherArAccount.Id);
        fixture.Payment.BusinessPartnerId = otherCustomer.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR receipt customer was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task CrossTenantInvoiceSettlement_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherArAccount = SeedAccount(db, otherTenantId, "1200", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var otherCustomer = SeedCustomer(db, otherTenantId, otherArAccount.Id);
        var otherInvoice = SeedPostedInvoice(db, otherTenantId, otherCustomer, otherArAccount, "INV-OTHER", new DateTime(2026, 7, 5), 100m);
        fixture.Allocation.InvoiceId = otherInvoice.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR receipt allocation references an invoice from another tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task MissingInvoiceRequestedForAllocation_ShouldBeRejectedInsteadOfBecomingAdvance()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        db.Remove(fixture.Allocation);
        fixture.Payment.AllocatedAmount = 0m;
        fixture.Invoice.PaidAmount = 0m;
        fixture.Invoice.Status = InvoiceStatus.Sent;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);
        var missingInvoiceId = Guid.NewGuid();

        var act = () => service.AllocatePaymentAsync(new PaymentAllocation_CreateDto
        {
            CustomerPaymentId = fixture.Payment.Id,
            Allocations = new List<InvoiceAllocationDto>
            {
                new() { InvoiceId = missingInvoiceId, AllocatedAmount = 100m }
            }
        });

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Customer receipt allocation invoice(s) were not found for this tenant: {missingInvoiceId}.");
        (await db.Set<PaymentAllocation>().CountAsync(a => a.CustomerPaymentId == fixture.Payment.Id)).Should().Be(0);
        (await db.Set<CustomerPayment>().SingleAsync(p => p.Id == fixture.Payment.Id)).AllocatedAmount.Should().Be(0m);
        (await db.Invoices.SingleAsync(i => i.Id == fixture.Invoice.Id)).PaidAmount.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task DifferentCustomerInvoiceRequestedForAllocation_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        db.Remove(fixture.Allocation);
        fixture.Payment.AllocatedAmount = 0m;
        fixture.Invoice.PaidAmount = 0m;
        fixture.Invoice.Status = InvoiceStatus.Sent;

        var otherCustomer = new BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerCode = $"CUS-OTHER-{tenantId.ToString("N")[..6]}",
            PartnerName = "Other Customer",
            PartnerType = "Customer",
            RegistrationStatus = "Approved",
            IsActive = true,
            DefaultArAccountId = fixture.ArAccount.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.Set<BusinessPartner>().Add(otherCustomer);
        var otherInvoice = SeedPostedInvoice(
            db,
            tenantId,
            otherCustomer,
            fixture.ArAccount,
            "INV-OTHER-CUSTOMER",
            fixture.Payment.PaymentDate,
            100m);
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.AllocatePaymentAsync(new PaymentAllocation_CreateDto
        {
            CustomerPaymentId = fixture.Payment.Id,
            Allocations = new List<InvoiceAllocationDto>
            {
                new() { InvoiceId = otherInvoice.Id, AllocatedAmount = 100m }
            }
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Invoice '{otherInvoice.InvoiceNumber}' does not belong to the customer selected for this receipt.");
        (await db.Set<PaymentAllocation>().CountAsync(a => a.CustomerPaymentId == fixture.Payment.Id)).Should().Be(0);
        (await db.Set<CustomerPayment>().SingleAsync(p => p.Id == fixture.Payment.Id)).AllocatedAmount.Should().Be(0m);
        (await db.Invoices.SingleAsync(i => i.Id == otherInvoice.Id)).PaidAmount.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task CrossTenantArControlAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherArAccount = SeedAccount(db, otherTenantId, "1200", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var settings = await db.FinanceSettings.SingleAsync(item => item.TenantId == tenantId);
        settings.ControlAccountArId = otherArAccount.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR receipt posting AR control account was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task CrossTenantBankAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherBankGl = SeedAccount(db, otherTenantId, "1100", AccountType.Asset);
        var otherBank = SeedBankAccount(db, otherTenantId, otherBankGl.Id);
        fixture.Payment.BankAccountId = otherBank.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR receipt bank account was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task InactiveBankGlAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        fixture.BankGlAccount.Status = AccountStatus.Inactive;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR receipt posting bank account account '1100' is not active.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ClosedPeriod_ShouldBeRejectedByPostingEngine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId, periodIsOpen: false, periodIsClosed: true);
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posting period is not open.");
        fixture.Payment.JournalEntryId.Should().BeNull();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task DuplicateArReceiptPosting_ShouldReturnExistingPostingAndAuditDuplicate()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);

        var first = await service.PostAsync(fixture.Payment.Id);
        var second = await service.PostAsync(fixture.Payment.Id);

        second.JournalEntryId.Should().Be(first.JournalEntryId);
        (await db.JournalEntries.CountAsync(j => j.SourceDocumentType == "CustomerPayment")).Should().Be(1);
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentType == "CustomerPayment")).Should().Be(1);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.ArReceiptDuplicatePostingAttempt)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task PostedArReceipt_ShouldNotBeMutated()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);
        await service.PostAsync(fixture.Payment.Id);

        var update = () => service.UpdateAsync(new PaymentUpdateDto
        {
            Id = fixture.Payment.Id,
            PaymentDate = fixture.Payment.PaymentDate,
            TotalAmount = fixture.Payment.TotalAmount,
            PaymentMethod = fixture.Payment.PaymentMethod
        });
        var allocate = () => service.AllocatePaymentAsync(new PaymentAllocation_CreateDto
        {
            CustomerPaymentId = fixture.Payment.Id,
            Allocations = new List<InvoiceAllocationDto>
            {
                new() { InvoiceId = fixture.Invoice.Id, AllocatedAmount = 1m }
            }
        });
        var reverse = () => service.ReverseAllocationAsync(fixture.Allocation.Id, "test reversal");
        var clear = () => service.ClearPaymentAsync(fixture.Payment.Id, fixture.Payment.PaymentDate);
        var bounce = () => service.BouncedPaymentAsync(fixture.Payment.Id, "test bounce");

        await update.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posted customer payments cannot be updated. Use a reversal, void, or adjustment workflow.");
        await allocate.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posted customer payments cannot be allocated. Use a reversal, void, or adjustment workflow.");
        await reverse.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posted customer payment allocations cannot be reversed by mutation. Use a reversal, void, or adjustment workflow.");
        await clear.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posted customer payments cannot be cleared by mutation until bank reconciliation integration is migrated to the posting engine.");
        await bounce.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posted receipts cannot be bounced by mutation. Use the posted receipt reversal workflow, or the returned-cheque workflow after deposit.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptReversal")]
    [Trait("Category", "AccountsReceivable")]
    public async Task PostedArReceipt_ShouldReverseLedgerAllocationAndBankFootprintsWithoutMutation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);
        var posted = await service.PostAsync(fixture.Payment.Id);

        var reversed = await service.ReversePaymentAsync(fixture.Payment.Id, new ReverseCustomerPaymentDto
        {
            Reason = "Customer transfer was recorded against the wrong receipt.",
            ReversalDate = new DateTime(2026, 7, 6)
        });

        reversed.Status.Should().Be("Reversed");
        reversed.JournalEntryId.Should().Be(posted.JournalEntryId);
        reversed.ReversalJournalEntryId.Should().NotBeNull();
        reversed.ReversalPostingEventId.Should().NotBeNull();
        reversed.ReversalCashTransactionId.Should().NotBeNull();
        reversed.ReversalDate.Should().Be(new DateTime(2026, 7, 6));

        // The source payment/allocation stay in place and are offset by explicit linked evidence.
        var allocations = await db.Set<PaymentAllocation>()
            .Where(item => item.CustomerPaymentId == fixture.Payment.Id)
            .OrderBy(item => item.IsReversal)
            .ToListAsync();
        allocations.Should().HaveCount(2);
        allocations.Single(item => !item.IsReversal).AllocatedAmount.Should().Be(100m);
        var correction = allocations.Single(item => item.IsReversal);
        correction.OriginalAllocationId.Should().Be(fixture.Allocation.Id);
        correction.AllocatedAmount.Should().Be(-100m);

        var journal = await db.JournalEntries
            .Include(item => item.Transactions)
            .SingleAsync(item => item.Id == reversed.ReversalJournalEntryId);
        journal.OriginalJournalEntryId.Should().Be(posted.JournalEntryId);
        journal.Transactions.Single(item => item.AccountId == fixture.BankGlAccount.Id).CreditAmount.Should().Be(100m);
        journal.Transactions.Single(item => item.AccountId == fixture.ArAccount.Id).DebitAmount.Should().Be(100m);

        var cashEntries = await db.Set<CashTransaction>()
            .Where(item => item.ReferenceNumber == fixture.Payment.PaymentNumber ||
                           item.Id == reversed.ReversalCashTransactionId)
            .ToListAsync();
        cashEntries.Should().ContainSingle(item => item.TransactionType == CashTransactionType.Receipt);
        cashEntries.Should().ContainSingle(item => item.TransactionType == CashTransactionType.Payment);
        (await db.BankAccounts.SingleAsync(item => item.Id == fixture.BankAccount.Id)).CurrentBalance.Should().Be(0m);
        (await db.Invoices.SingleAsync(item => item.Id == fixture.Invoice.Id)).PaidAmount.Should().Be(0m);
        (await db.Set<BusinessPartner>().SingleAsync(item => item.Id == fixture.Customer.Id))
            .OutstandingBalance.Should().Be(100m);
        (await db.AuditLogs.CountAsync(item => item.Action == FinanceAuditEvents.ArReceiptReversed))
            .Should().Be(1);

        var trace = await service.GetTraceAsync(fixture.Payment.Id);
        trace.Should().NotBeNull();
        trace!.Postings.Should().Contain(item => item.PostingAction == "Post");
        trace.Postings.Should().Contain(item => item.PostingAction == "Reverse");
        trace.OperationalEntries.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptReversal")]
    [Trait("Category", "AccountsReceivable")]
    public async Task RepeatingArReceiptReversal_ShouldReturnExistingCorrection()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);
        await service.PostAsync(fixture.Payment.Id);
        var command = new ReverseCustomerPaymentDto
        {
            Reason = "Customer transfer was recorded against the wrong receipt.",
            ReversalDate = new DateTime(2026, 7, 6)
        };

        var first = await service.ReversePaymentAsync(fixture.Payment.Id, command);
        var second = await service.ReversePaymentAsync(fixture.Payment.Id, command);

        second.ReversalJournalEntryId.Should().Be(first.ReversalJournalEntryId);
        (await db.FinancePostingEvents.CountAsync(item =>
            item.SourceDocumentType == "CustomerPayment" &&
            item.SourceDocumentId == fixture.Payment.Id &&
            item.PostingAction == "Reverse")).Should().Be(1);
        (await db.Set<PaymentAllocation>().CountAsync(item =>
            item.CustomerPaymentId == fixture.Payment.Id && item.IsReversal)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptReversal")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ReconciledArReceipt_ShouldRequireReconciliationRemovalBeforeReversal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);
        await service.PostAsync(fixture.Payment.Id);
        var cashReceipt = await db.Set<CashTransaction>().SingleAsync(item =>
            item.TransactionType == CashTransactionType.Receipt &&
            item.ReferenceNumber == fixture.Payment.PaymentNumber);
        cashReceipt.IsReconciled = true;
        await db.SaveChangesAsync();

        var act = () => service.ReversePaymentAsync(fixture.Payment.Id, new ReverseCustomerPaymentDto
        {
            Reason = "Customer transfer was recorded against the wrong receipt.",
            ReversalDate = new DateTime(2026, 7, 6)
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("This receipt is bank-reconciled. Remove it from the reconciliation before reversal.");
        (await db.FinancePostingEvents.CountAsync(item => item.PostingAction == "Reverse")).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptReversal")]
    [Trait("Category", "AccountsReceivable")]
    public async Task LiquidityHeldArReceipt_ShouldCreateLinkedDecreaseEntryOnReversal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        var holdingGl = SeedAccount(db, tenantId, "1110", AccountType.Asset);
        var holding = SeedLiquidityAccount(db, tenantId, holdingGl.Id);
        fixture.Payment.PaymentMethod = "Cash";
        fixture.Payment.BankAccountId = null;
        fixture.Payment.LiquidityAccountId = holding.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);
        await service.PostAsync(fixture.Payment.Id);

        var reversed = await service.ReversePaymentAsync(fixture.Payment.Id, new ReverseCustomerPaymentDto
        {
            Reason = "Cash receipt was recorded for the incorrect customer account.",
            ReversalDate = new DateTime(2026, 7, 6)
        });

        reversed.ReversalLiquidityAccountEntryId.Should().NotBeNull();
        reversed.ReversalCashTransactionId.Should().BeNull();
        var entries = await db.LiquidityAccountEntries
            .Where(item => item.SourceDocumentId == fixture.Payment.Id)
            .ToListAsync();
        var original = entries.Single(item => item.EntryType == LiquidityEntryType.CustomerReceipt);
        var correction = entries.Single(item => item.EntryType == LiquidityEntryType.Reversal);
        original.IsReversed.Should().BeTrue();
        correction.Direction.Should().Be(LiquidityEntryDirection.Decrease);
        correction.ReversalOfEntryId.Should().Be(original.Id);
        correction.Amount.Should().Be(original.Amount);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptReversal")]
    [Trait("Category", "AccountsReceivable")]
    public async Task DepositedLiquidityReceipt_ShouldRequireBankingReversalFirst()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        var holdingGl = SeedAccount(db, tenantId, "1110", AccountType.Asset);
        var holding = SeedLiquidityAccount(db, tenantId, holdingGl.Id);
        fixture.Payment.PaymentMethod = "Cash";
        fixture.Payment.BankAccountId = null;
        fixture.Payment.LiquidityAccountId = holding.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);
        await service.PostAsync(fixture.Payment.Id);
        var entry = await db.LiquidityAccountEntries.SingleAsync(item =>
            item.SourceDocumentId == fixture.Payment.Id &&
            item.EntryType == LiquidityEntryType.CustomerReceipt);
        // AllocatedAmount is the operational proof that Banking & Settlement has consumed part
        // of this holding entry. The receipt workflow must not bypass that downstream owner.
        entry.AllocatedAmount = 25m;
        await db.SaveChangesAsync();

        var act = () => service.ReversePaymentAsync(fixture.Payment.Id, new ReverseCustomerPaymentDto
        {
            Reason = "Cash receipt was recorded for the incorrect customer account.",
            ReversalDate = new DateTime(2026, 7, 6)
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("This receipt has already been included in a bank deposit or settlement. Reverse that banking transaction first.");
        (await db.FinancePostingEvents.CountAsync(item => item.PostingAction == "Reverse")).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BankingSettlement")]
    [Trait("Category", "CashBank")]
    public async Task PostedCashReceipt_ShouldEnterHoldingAccountAndSupportPartialDeposit()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        var holdingGl = SeedAccount(db, tenantId, "1110", AccountType.Asset);
        var holding = new LiquidityAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "UNDEPOSITED-CASH",
            Name = "Undeposited Cash",
            AccountType = LiquidityAccountType.UndepositedCash,
            Currency = "GHS",
            GLAccountId = holdingGl.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.LiquidityAccounts.Add(holding);
        fixture.Payment.PaymentMethod = "Cash";
        fixture.Payment.BankAccountId = null;
        fixture.Payment.LiquidityAccountId = holding.Id;
        await db.SaveChangesAsync();
        var (receiptService, _) = CreateService(db, tenantId);

        await receiptService.PostAsync(fixture.Payment.Id);

        var holdingEntry = await db.LiquidityAccountEntries.SingleAsync(entry =>
            entry.TenantId == tenantId &&
            entry.SourceDocumentType == nameof(CustomerPayment) &&
            entry.SourceDocumentId == fixture.Payment.Id);
        holdingEntry.Direction.Should().Be(LiquidityEntryDirection.Increase);
        holdingEntry.EntryType.Should().Be(LiquidityEntryType.CustomerReceipt);
        holdingEntry.Amount.Should().Be(100m);
        var receiptJournal = await db.JournalEntries
            .Include(entry => entry.Transactions)
            .SingleAsync(entry => entry.Id == fixture.Payment.JournalEntryId);
        receiptJournal.Transactions.Single(line => line.AccountId == holdingGl.Id).DebitAmount.Should().Be(100m);

        var currentUser = CreateCurrentUser(tenantId);
        var numbering = new Mock<IDocumentNumberingService>();
        numbering.Setup(service => service.GenerateAsync(
                DocumentNumberingModules.Finance,
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("BD-2026-0001");
        var bankingService = new BankingSettlementService(
            db,
            currentUser.Object,
            numbering.Object,
            Mock.Of<IWorkflowIntegrationService>(),
            Mock.Of<IFinancePostingEngine>());

        var deposit = await bankingService.CreateDepositAsync(new CreateBankDepositDto
        {
            BankAccountId = fixture.BankAccount.Id,
            DepositDate = new DateTime(2026, 7, 6),
            DepositReference = "SLIP-PARTIAL-001",
            Allocations =
            {
                new BankDepositAllocationRequestDto
                {
                    LiquidityAccountEntryId = holdingEntry.Id,
                    AllocationType = BankDepositAllocationType.Receipt,
                    Amount = 40m
                }
            }
        });

        deposit.TotalReceipts.Should().Be(40m);
        deposit.NetAmount.Should().Be(40m);
        deposit.Allocations.Should().ContainSingle();
        (await db.LiquidityAccountEntries.SingleAsync(entry => entry.Id == holdingEntry.Id))
            .AllocatedAmount.Should().Be(40m);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ar-receipt-posting-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static (PaymentService Service, Mock<ISubledgerPostingService> SubledgerPostingMock) CreateService(
        ApplicationDbContext db,
        Guid tenantId,
        IFxAccountingService? fxAccountingService = null,
        IExchangeRateService? exchangeRateService = null)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var auditService = new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-ar-receipt-posting" }
            });
        var postingEngine = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            auditService);
        var subledgerPostingMock = new Mock<ISubledgerPostingService>();
        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");
        var documentNumbering = new Mock<IDocumentNumberingService>();
        documentNumbering
            .Setup(x => x.GenerateAsync(
                DocumentNumberingModules.Finance,
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"LQE-{Guid.NewGuid():N}"[..24]);

        var service = new PaymentService(
            new UnitOfWork(db),
            currentUser.Object,
            tenantSettings.Object,
            Mock.Of<ILogger<PaymentService>>(),
            documentNumbering.Object,
            // Posting fixtures run tenant-wide. Reversal tests use the real shared policy so
            // date/reason assertions exercise the same control used in production.
            Mock.Of<IFinanceAccessScopeService>(),
            new FinanceReversalPolicyService(db, currentUser.Object),
            postingEngine,
            auditService,
            fxAccountingService: fxAccountingService,
            exchangeRateService: exchangeRateService);

        return (service, subledgerPostingMock);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId)
    {
        var userId = Guid.NewGuid().ToString();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(userId);
        currentUser.SetupGet(x => x.UserName).Returns("ar.receipt.poster");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("ar-receipt-posting-tests");
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        return currentUser;
    }

    private static async Task<ArReceiptFixture> SeedApprovedArReceiptAsync(
        ApplicationDbContext db,
        Guid tenantId,
        Action<CustomerPayment>? configurePayment = null,
        Action<Invoice>? configureInvoice = null,
        decimal allocationAmount = 100m,
        bool seedInvoicePostingEvent = true,
        bool periodIsOpen = true,
        bool periodIsClosed = false)
    {
        SeedTenant(db, tenantId);
        var period = SeedOpenPeriod(db, tenantId, periodIsOpen, periodIsClosed);
        var arAccount = SeedAccount(db, tenantId, "1200", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var bankGlAccount = SeedAccount(db, tenantId, "1100", AccountType.Asset);
        var revenueAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        var taxAccount = SeedAccount(db, tenantId, "2200", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        var discountAccount = SeedAccount(db, tenantId, "5200", AccountType.Expense);
        var customer = SeedCustomer(db, tenantId, arAccount.Id);
        var customerRole = new BusinessPartnerRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = customer.Id,
            RoleType = BusinessPartnerRoleType.Customer,
            Status = BusinessPartnerRoleStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        var arProfile = new BusinessPartnerArProfileVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerRoleId = customerRole.Id,
            VersionNumber = 1,
            Status = BusinessPartnerFinanceProfileStatus.Approved,
            EffectiveFrom = new DateTime(2026, 1, 1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.BusinessPartnerRoles.Add(customerRole);
        db.BusinessPartnerArProfileVersions.Add(arProfile);
        var bankAccount = SeedBankAccount(db, tenantId, bankGlAccount.Id);

        db.Set<FinanceSettings>().Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            ControlAccountArId = arAccount.Id,
            ControlAccountTaxId = taxAccount.Id,
            DiscountAllowedAccountId = discountAccount.Id,
            DefaultBankAccountId = bankAccount.Id
        });

        var invoice = SeedPostedInvoice(db, tenantId, customer, arAccount, "INV-2026-00001", new DateTime(2026, 7, 5), 100m, period.Id, seedInvoicePostingEvent);
        invoice.LineItems.Add(new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceId = invoice.Id,
            LineItemType = LineItemType.GLAccount,
            GLAccountId = revenueAccount.Id,
            Description = "Consulting services",
            Quantity = 1m,
            UnitPrice = 100m,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        configureInvoice?.Invoke(invoice);

        var payment = new CustomerPayment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PaymentNumber = "CP-2026-00001",
            BusinessPartnerId = customer.Id,
            BusinessPartnerRoleId = customerRole.Id,
            BusinessPartnerArProfileVersionId = arProfile.Id,
            BusinessPartnerCode = customer.PartnerCode,
            BusinessPartnerName = customer.PartnerName,
            BusinessPartnerLegalName = customer.LegalName,
            BusinessPartnerTaxIdentificationNumber = customer.TaxIdentificationNumber,
            PaymentDate = new DateTime(2026, 7, 5),
            TotalAmount = allocationAmount,
            AllocatedAmount = allocationAmount,
            PaymentMethod = "BankTransfer",
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BankAccountId = bankAccount.Id,
            Status = "Approved",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        configurePayment?.Invoke(payment);

        var allocation = new PaymentAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerPaymentId = payment.Id,
            InvoiceId = invoice.Id,
            AllocatedAmount = allocationAmount,
            AllocationDate = payment.PaymentDate,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        invoice.PaidAmount = allocationAmount;
        invoice.Status = allocationAmount >= invoice.TotalAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;

        db.Set<CustomerPayment>().Add(payment);
        db.Set<PaymentAllocation>().Add(allocation);
        await db.SaveChangesAsync();

        return new ArReceiptFixture(payment, allocation, invoice, customer, arAccount, bankGlAccount, bankAccount);
    }

    private static void SeedPostedAppliedSalesCreditNote(
        ApplicationDbContext db,
        Guid tenantId,
        Guid invoiceId,
        decimal amount)
    {
        var book = db.AccountingBooks.Local.Single(item => item.TenantId == tenantId && item.Code == "IFRS");
        var creditNote = new CreditNote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = db.Invoices.Single(i => i.Id == invoiceId).BusinessPartnerId,
            OriginalInvoiceId = invoiceId,
            AppliedToInvoiceId = invoiceId,
            CreditNoteStatus = CreditNoteStatus.Applied,
            DocumentNumber = "SCN-2026-00001",
            DocumentDate = new DateTime(2026, 7, 5),
            AppliedDate = new DateTime(2026, 7, 5),
            TotalAmount = amount,
            Currency = "GHS",
            ExchangeRate = 1m,
            JournalEntryId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.CreditNotes.Add(creditNote);
        db.FinancePostingEvents.Add(new FinancePostingEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceModule = "AR",
            SourceDocumentType = "SalesCreditNote",
            SourceDocumentId = creditNote.Id,
            PostingAction = "Post",
            SourceDocumentReference = creditNote.DocumentNumber,
            IdempotencyKey = $"AR:SalesCreditNote:{tenantId:N}:{creditNote.Id:N}:Post",
            JournalEntryId = creditNote.JournalEntryId,
            AccountingBookId = book.Id,
            PostingStatus = "Posted",
            PostingDate = creditNote.DocumentDate,
            RequestedAt = DateTime.UtcNow,
            PostedAt = DateTime.UtcNow,
            TotalDebitAmount = amount,
            TotalCreditAmount = amount,
            FunctionalCurrencyCode = "GHS",
            BookClassification = "IFRS",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
    }

    private static void SeedTenant(ApplicationDbContext db, Guid tenantId, string code = "TEN")
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
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "IFRS", Name = "IFRS Primary",
            Purpose = "Primary", BookType = AccountingBookType.PrimaryFull,
            LifecycleStatus = AccountingBookLifecycleStatus.Active, FunctionalCurrencyCode = "GHS",
            IsDefault = true, IsActive = true, AllowsPosting = true
        });
    }

    private static FiscalPeriod SeedOpenPeriod(
        ApplicationDbContext db,
        Guid tenantId,
        bool isOpen = true,
        bool isClosed = false)
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
        FinancePostingAuthorityFixture.SeedExactBookPeriod(db, tenantId, period);
        return period;
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
        var book = db.AccountingBooks.Local.Single(item => item.TenantId == tenantId && item.Code == "IFRS");
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
            // AR control, discount and statutory receivable accounts are deliberately configured
            // as multi-currency accounts so the test exercises the same guarded path as a tenant
            // that accepts invoices in USD while keeping GHS as functional currency.
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

    private static BusinessPartner SeedCustomer(ApplicationDbContext db, Guid tenantId, Guid arAccountId)
    {
        var customer = new BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerCode = $"CUS-{tenantId.ToString("N")[..6]}",
            PartnerName = "Test Customer",
            PartnerType = "Customer",
            RegistrationStatus = "Approved",
            IsActive = true,
            IsBlacklisted = false,
            DefaultArAccountId = arAccountId,
            CreditLimit = 10000m,
            OutstandingBalance = 0m,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        db.Set<BusinessPartner>().Add(customer);
        return customer;
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

    private static LiquidityAccount SeedLiquidityAccount(
        ApplicationDbContext db,
        Guid tenantId,
        Guid glAccountId)
    {
        var account = new LiquidityAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = $"UNDEPOSITED-{tenantId.ToString("N")[..6]}",
            Name = "Undeposited Cash",
            AccountType = LiquidityAccountType.UndepositedCash,
            Currency = "GHS",
            GLAccountId = glAccountId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.LiquidityAccounts.Add(account);
        return account;
    }

    private static Invoice SeedPostedInvoice(
        ApplicationDbContext db,
        Guid tenantId,
        BusinessPartner customer,
        Account arAccount,
        string invoiceNumber,
        DateTime invoiceDate,
        decimal amount,
        Guid? fiscalPeriodId = null,
        bool seedPostingEvent = true)
    {
        var book = db.AccountingBooks.Local.Single(item => item.TenantId == tenantId && item.Code == "IFRS");
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = invoiceNumber,
            BusinessPartnerId = customer.Id,
            CustomerName = customer.PartnerName,
            InvoiceDate = invoiceDate,
            DueDate = invoiceDate.AddDays(30),
            SubTotal = amount,
            TotalAmount = amount,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BaseCurrencyAmount = amount,
            Status = InvoiceStatus.Sent,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var invoiceJournal = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JournalEntryNumber = $"JE-{invoiceNumber}",
            JournalType = "AR Invoice",
            EntryDate = invoiceDate,
            Description = $"Posted invoice {invoiceNumber}",
            ReferenceNumber = invoiceNumber,
            SourceModule = "AR",
            SourceDocumentId = invoice.Id,
            SourceDocumentType = "CustomerInvoice",
            TotalDebitAmount = amount,
            TotalCreditAmount = amount,
            IsBalanced = true,
            FiscalPeriodId = fiscalPeriodId ?? Guid.NewGuid(),
            AccountingBookId = book.Id,
            PostingStatus = "Posted",
            ApprovalStatus = "Approved",
            PostingDate = invoiceDate,
            BookClassification = "IFRS",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        invoice.JournalEntryId = invoiceJournal.Id;

        db.Invoices.Add(invoice);
        db.JournalEntries.Add(invoiceJournal);

        if (seedPostingEvent)
        {
            db.FinancePostingEvents.Add(new FinancePostingEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SourceModule = "AR",
                SourceDocumentType = "CustomerInvoice",
                SourceDocumentId = invoice.Id,
                PostingAction = "Post",
                SourceDocumentReference = invoice.InvoiceNumber,
                IdempotencyKey = $"AR:CustomerInvoice:{tenantId:N}:{invoice.Id:N}:Post",
                JournalEntryId = invoiceJournal.Id,
                AccountingBookId = book.Id,
                PostingStatus = "Posted",
                PostingDate = invoiceDate,
                RequestedAt = DateTime.UtcNow,
                PostedAt = DateTime.UtcNow,
                TotalDebitAmount = amount,
                TotalCreditAmount = amount,
                FunctionalCurrencyCode = "GHS",
                BookClassification = "IFRS",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed"
            });
        }

        return invoice;
    }

    private sealed record ArReceiptFixture(
        CustomerPayment Payment,
        PaymentAllocation Allocation,
        Invoice Invoice,
        BusinessPartner Customer,
        Account ArAccount,
        Account BankGlAccount,
        BankAccount BankAccount);
}
