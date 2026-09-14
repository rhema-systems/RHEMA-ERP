using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.AR;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
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

public sealed class ArInvoicePostingMigrationTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task SentArInvoice_ShouldPostThroughFinancePostingEngineAndCreateAuditEvent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        var (service, subledgerPostingMock) = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.Invoice.Id);

        result.JournalEntryId.Should().NotBeNull();
        subledgerPostingMock.Verify(x => x.PostArInvoiceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        var postingEvent = await db.FinancePostingEvents.SingleAsync(e =>
            e.TenantId == tenantId &&
            e.SourceModule == "AR" &&
            e.SourceDocumentType == "CustomerInvoice" &&
            e.SourceDocumentId == fixture.Invoice.Id &&
            e.PostingAction == "Post");
        postingEvent.JournalEntryId.Should().Be(result.JournalEntryId);

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == result.JournalEntryId);
        journal.PostingStatus.Should().Be("Posted");
        journal.SourceModule.Should().Be("AR");
        journal.SourceDocumentType.Should().Be("CustomerInvoice");
        journal.Transactions.Should().HaveCount(2);
        journal.Transactions.Single(t => t.AccountId == fixture.ArAccount.Id).DebitAmount.Should().Be(100m);
        journal.Transactions.Single(t => t.AccountId == fixture.RevenueAccount.Id).CreditAmount.Should().Be(100m);

        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.ArInvoicePosted && a.TenantId == tenantId)).Should().Be(1);
        // The posting engine keeps Account.Balance as a read-side snapshot for legacy balance APIs.
        fixture.ArAccount.Balance.Should().Be(100m);
        fixture.RevenueAccount.Balance.Should().Be(100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task InvoiceTradeDiscounts_ShouldReduceRevenueWithoutDiscountAllowedAccount()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, invoice =>
        {
            var line = invoice.LineItems.Single();
            line.DiscountPercentage = 10m;
            line.DiscountAmount = 10m;
            invoice.SubTotal = 90m;
            invoice.DiscountAmount = 5m;
            invoice.TotalAmount = 85m;
            invoice.BaseCurrencyAmount = 85m;
        });
        var settings = await db.FinanceSettings.SingleAsync(item => item.TenantId == tenantId);
        settings.DiscountAllowedAccountId = null;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.Invoice.Id);

        var journal = await db.JournalEntries.Include(item => item.Transactions)
            .SingleAsync(item => item.Id == result.JournalEntryId);
        journal.Transactions.Should().HaveCount(2);
        journal.Transactions.Single(item => item.AccountId == fixture.ArAccount.Id)
            .DebitAmount.Should().Be(85m);
        journal.Transactions.Single(item => item.AccountId == fixture.RevenueAccount.Id)
            .CreditAmount.Should().Be(85m);
        journal.Transactions.Should().NotContain(item => item.AccountId == fixture.DiscountAccount.Id);
    }

    [Fact]
    public async Task CreateInvoice_ShouldApplyLineAndDocumentTradeDiscountBeforeTax()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        TaxCalculationRequestDto? capturedTaxRequest = null;
        var taxEngine = new Mock<ITaxCalculationEngine>();
        taxEngine.Setup(engine => engine.CalculateTaxesAsync(
                It.IsAny<TaxCalculationRequestDto>(),
                It.IsAny<CancellationToken>()))
            .Callback<TaxCalculationRequestDto, CancellationToken>((request, _) => capturedTaxRequest = request)
            .ReturnsAsync((TaxCalculationRequestDto request, CancellationToken _) => new TaxCalculationResultDto
            {
                TotalTaxAmount = decimal.Round(request.BaseAmount * 0.15m, 2, MidpointRounding.AwayFromZero)
            });
        var (service, _) = CreateService(db, tenantId, taxEngine: taxEngine.Object);

        var created = await service.CreateAsync(new InvoiceCreateDto
        {
            CustomerId = fixture.Customer.Id,
            InvoiceDate = new DateTime(2026, 7, 6),
            DueDate = new DateTime(2026, 8, 5),
            CurrencyCode = "GHS",
            DiscountAmount = 10m,
            LineItems = new List<InvoiceLineItemCreateDto>
            {
                new()
                {
                    LineItemType = "GLAccount",
                    GLAccountId = fixture.RevenueAccount.Id,
                    Description = "Discounted service",
                    Quantity = 1m,
                    UnitPrice = 100m,
                    DiscountPercentage = 10m,
                    TaxGroupId = Guid.NewGuid(),
                    TaxTreatment = TaxTreatment.Standard
                }
            }
        });

        capturedTaxRequest.Should().NotBeNull();
        capturedTaxRequest!.BaseAmount.Should().Be(80m);
        created.SubTotal.Should().Be(90m);
        created.DiscountAmount.Should().Be(10m);
        created.TaxAmount.Should().Be(12m);
        created.TotalAmount.Should().Be(92m);
    }

    [Theory]
    [InlineData(101, 0)]
    [InlineData(0, 101)]
    public async Task CreateInvoice_ShouldRejectInvalidTradeDiscounts(decimal linePercentage, decimal documentDiscount)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);

        var action = () => service.CreateAsync(new InvoiceCreateDto
        {
            CustomerId = fixture.Customer.Id,
            InvoiceDate = new DateTime(2026, 7, 6),
            DueDate = new DateTime(2026, 8, 5),
            CurrencyCode = "GHS",
            DiscountAmount = documentDiscount,
            LineItems = new List<InvoiceLineItemCreateDto>
            {
                new()
                {
                    LineItemType = "GLAccount",
                    GLAccountId = fixture.RevenueAccount.Id,
                    Description = "Invalid discount",
                    Quantity = 1m,
                    UnitPrice = 100m,
                    DiscountPercentage = linePercentage,
                    TaxTreatment = TaxTreatment.OutOfScope
                }
            }
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*trade discount*");
        (await db.Invoices.CountAsync()).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task OpeningBalanceArInvoice_ShouldPostControlAgainstMigrationClearing()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, invoice =>
        {
            invoice.IsOpeningBalance = true;
            invoice.LineItems.Single().GLAccountId = Guid.NewGuid();
        });
        var clearingAccount = SeedAccount(db, tenantId, "3999", AccountType.Equity);
        var settings = await db.FinanceSettings.SingleAsync(s => s.TenantId == tenantId);
        settings.MigrationClearingAccountId = clearingAccount.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.Invoice.Id);

        result.JournalEntryId.Should().NotBeNull();

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == result.JournalEntryId);
        journal.JournalType.Should().Be("AR Opening Balance");
        journal.SourceModule.Should().Be("AR");
        journal.SourceDocumentType.Should().Be("CustomerInvoice");
        journal.Transactions.Should().HaveCount(2);
        journal.Transactions.Single(t => t.AccountId == fixture.ArAccount.Id).DebitAmount.Should().Be(100m);
        journal.Transactions.Single(t => t.AccountId == clearingAccount.Id).CreditAmount.Should().Be(100m);
        journal.Transactions.Should().NotContain(t => t.AccountId == fixture.RevenueAccount.Id);

        (await db.FinancePostingEvents.CountAsync(e =>
            e.TenantId == tenantId &&
            e.SourceModule == "AR" &&
            e.SourceDocumentType == "CustomerInvoice" &&
            e.SourceDocumentId == fixture.Invoice.Id)).Should().Be(1);
        (await db.Set<TaxCalculation>().CountAsync()).Should().Be(0);
        fixture.RevenueAccount.Balance.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ForeignOpeningBalanceArInvoice_ShouldRetainApprovedRateAndKeepClearingFunctional()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, invoice =>
        {
            invoice.IsOpeningBalance = true;
            invoice.CurrencyCode = "USD";
            invoice.ExchangeRate = 12.5m;
            invoice.BaseCurrencyAmount = 1250m;
        });
        var rate = SeedApprovedDailyRate(db, tenantId, "USD", 12.5m, ExchangeRateQuoteSide.Buying);
        fixture.Invoice.ExchangeRateId = rate.Id;
        EnableCurrencyForAccounts(db, tenantId, "USD", fixture.ArAccount);
        var clearingAccount = SeedAccount(db, tenantId, "3999", AccountType.Equity);
        var settings = await db.FinanceSettings.SingleAsync(s => s.TenantId == tenantId);
        settings.MigrationClearingAccountId = clearingAccount.Id;
        settings.DirectionalExchangeRatePolicyEnabled = true;
        settings.ArInvoiceQuoteSide = ExchangeRateQuoteSide.Buying;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.Invoice.Id);

        var journal = await db.JournalEntries.Include(item => item.Transactions)
            .SingleAsync(item => item.Id == result.JournalEntryId);
        var control = journal.Transactions.Single(item => item.AccountId == fixture.ArAccount.Id);
        control.DebitAmount.Should().Be(1250m);
        control.TransactionCurrency.Should().Be("USD");
        control.TransactionDebitAmount.Should().Be(100m);
        control.ExchangeRate.Should().Be(12.5m);
        control.ExchangeRateId.Should().Be(rate.Id);
        control.ExchangeRateSource.Should().Be("Regression approved rate");

        var clearing = journal.Transactions.Single(item => item.AccountId == clearingAccount.Id);
        clearing.CreditAmount.Should().Be(1250m);
        clearing.TransactionCurrency.Should().Be("GHS");
        clearing.TransactionCreditAmount.Should().Be(1250m);
        clearing.ExchangeRateId.Should().BeNull();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ForeignOpeningBalanceArInvoice_ShouldRejectRateDriftBeforePosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, invoice =>
        {
            invoice.IsOpeningBalance = true;
            invoice.CurrencyCode = "USD";
            invoice.ExchangeRate = 14m;
        });
        var rate = SeedApprovedDailyRate(db, tenantId, "USD", 15m);
        fixture.Invoice.ExchangeRateId = rate.Id;
        var settings = await db.FinanceSettings.SingleAsync(s => s.TenantId == tenantId);
        settings.MigrationClearingAccountId = SeedAccount(db, tenantId, "3999", AccountType.Equity).Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var action = () => service.PostAsync(fixture.Invoice.Id);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not match the approved rate record*");
        (await db.FinancePostingEvents.AnyAsync()).Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ForeignArInvoice_ShouldRejectCreateWithoutRateEvidence(bool isOpeningBalance)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);

        var action = () => service.CreateAsync(new InvoiceCreateDto
        {
            CustomerId = fixture.Customer.Id,
            InvoiceDate = new DateTime(2026, 7, 5),
            DueDate = new DateTime(2026, 8, 4),
            CurrencyCode = "USD",
            ExchangeRate = 15m,
            IsOpeningBalance = isOpeningBalance,
            LineItems = new List<InvoiceLineItemCreateDto>
            {
                new()
                {
                    LineItemType = "GLAccount",
                    GLAccountId = fixture.RevenueAccount.Id,
                    Description = "Opening customer balance",
                    Quantity = 1m,
                    UnitPrice = 100m,
                    TaxTreatment = TaxTreatment.OutOfScope
                }
            }
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*require an approved exchange-rate record*");
        (await db.Invoices.CountAsync()).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task RepeatedArInvoiceCreateWithSameLineIdentity_ShouldReturnCommittedInvoice()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);
        var lineId = Guid.NewGuid();
        var request = new InvoiceCreateDto
        {
            CustomerId = fixture.Customer.Id,
            InvoiceDate = new DateTime(2026, 7, 6),
            DueDate = new DateTime(2026, 8, 5),
            CurrencyCode = "GHS",
            LineItems = new List<InvoiceLineItemCreateDto>
            {
                new()
                {
                    Id = lineId,
                    LineItemType = "GLAccount",
                    GLAccountId = fixture.RevenueAccount.Id,
                    Description = "Retry-safe consulting invoice",
                    Quantity = 1m,
                    UnitPrice = 15000m,
                    TaxTreatment = TaxTreatment.OutOfScope
                }
            }
        };

        var first = await service.CreateAsync(request);
        var replay = await service.CreateAsync(request);

        replay.Id.Should().Be(first.Id);
        replay.LineItems.Should().ContainSingle(line => line.Id == lineId);
        (await db.Invoices.CountAsync()).Should().Be(2);
        (await db.Set<InvoiceLineItem>().CountAsync(line => line.Id == lineId)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ReusedArInvoiceLineIdentityWithChangedPayload_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);
        var request = new InvoiceCreateDto
        {
            CustomerId = fixture.Customer.Id,
            InvoiceDate = new DateTime(2026, 7, 6),
            DueDate = new DateTime(2026, 8, 5),
            CurrencyCode = "GHS",
            LineItems = new List<InvoiceLineItemCreateDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    LineItemType = "GLAccount",
                    GLAccountId = fixture.RevenueAccount.Id,
                    Description = "Original request",
                    Quantity = 1m,
                    UnitPrice = 100m,
                    TaxTreatment = TaxTreatment.OutOfScope
                }
            }
        };
        await service.CreateAsync(request);
        request.LineItems[0].UnitPrice = 200m;

        var action = () => service.CreateAsync(request);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already bound to a different request*");
        (await db.Invoices.CountAsync()).Should().Be(2);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task DraftOpeningBalanceArInvoice_ShouldSendAndPostControlAgainstMigrationClearing()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, invoice =>
        {
            invoice.Status = InvoiceStatus.Draft;
            invoice.IsOpeningBalance = true;
            invoice.LineItems.Single().GLAccountId = Guid.NewGuid();
        });
        var clearingAccount = SeedAccount(db, tenantId, "3999", AccountType.Equity);
        var settings = await db.FinanceSettings.SingleAsync(s => s.TenantId == tenantId);
        settings.MigrationClearingAccountId = clearingAccount.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var result = await service.SendInvoiceAsync(fixture.Invoice.Id);

        result.Status.Should().Be(nameof(InvoiceStatus.Sent));
        result.JournalEntryId.Should().NotBeNull();

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == result.JournalEntryId);
        journal.JournalType.Should().Be("AR Opening Balance");
        journal.Transactions.Single(t => t.AccountId == fixture.ArAccount.Id).DebitAmount.Should().Be(100m);
        journal.Transactions.Single(t => t.AccountId == clearingAccount.Id).CreditAmount.Should().Be(100m);
        journal.Transactions.Should().NotContain(t => t.AccountId == fixture.RevenueAccount.Id);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task PendingApprovalArInvoice_ShouldPostWhenFinalApprovalReleasesIt()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, invoice =>
        {
            invoice.Status = InvoiceStatus.PendingApproval;
        });
        await SeedCompletedInvoiceWorkflowAsync(db, fixture.Invoice);
        var (service, _) = CreateService(db, tenantId);

        var result = await service.SendInvoiceAsync(fixture.Invoice.Id);

        result.Status.Should().Be(nameof(InvoiceStatus.Sent));
        result.JournalEntryId.Should().NotBeNull();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "AccountsReceivable")]
    public async Task FixedAssetDisposalDeduction_ShouldDebitClearingOnlyForMatchingCompletedDisposal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        const string disposalReference = "DSP-2026-0001";
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, invoice =>
        {
            invoice.Reference = $"FA-DISPOSAL:{disposalReference}";
            invoice.SubTotal = 80m;
            invoice.TotalAmount = 80m;
            invoice.BaseCurrencyAmount = 80m;
            invoice.LineItems.Add(new InvoiceLineItem
            {
                Id = Guid.NewGuid(), TenantId = tenantId, InvoiceId = invoice.Id,
                LineItemType = LineItemType.FixedAssetDisposalAdjustment,
                GLAccountId = invoice.LineItems.Single().GLAccountId,
                Description = "Auctioneer deduction", Quantity = 1m, UnitPrice = -20m,
                TaxTreatment = TaxTreatment.OutOfScope, DiscountPercentage = 0m, DiscountAmount = 0m,
                TaxAmount = 0m, CreatedAt = DateTime.UtcNow, CreatedBy = "seed"
            });
        });
        db.AssetDisposals.Add(new AssetDisposal
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FixedAssetId = Guid.NewGuid(),
            DisposalDate = fixture.Invoice.InvoiceDate, DisposalType = DisposalType.Sale,
            Status = AssetDisposalStatus.Completed, SaleProceeds = 100m, DisposalCost = 20m,
            BuyerBusinessPartnerId = fixture.Customer.Id, ReferenceNumber = disposalReference,
            Reason = "Approved sale", CreatedAt = DateTime.UtcNow, CreatedBy = "seed"
        });
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.Invoice.Id);

        var journal = await db.JournalEntries.Include(item => item.Transactions)
            .SingleAsync(item => item.Id == result.JournalEntryId);
        journal.Transactions.Single(line => line.AccountId == fixture.ArAccount.Id).DebitAmount.Should().Be(80m);
        journal.Transactions.Where(line => line.AccountId == fixture.RevenueAccount.Id)
            .Sum(line => line.CreditAmount - line.DebitAmount).Should().Be(80m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "AccountsReceivable")]
    public async Task FixedAssetDisposalDeduction_ShouldRejectSpoofedPublicInvoiceReference()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, invoice =>
        {
            invoice.Reference = "FA-DISPOSAL:NOT-APPROVED";
            invoice.SubTotal = 80m;
            invoice.TotalAmount = 80m;
            invoice.BaseCurrencyAmount = 80m;
            invoice.LineItems.Add(new InvoiceLineItem
            {
                Id = Guid.NewGuid(), TenantId = tenantId, InvoiceId = invoice.Id,
                LineItemType = LineItemType.FixedAssetDisposalAdjustment,
                GLAccountId = invoice.LineItems.Single().GLAccountId,
                Description = "Spoofed deduction", Quantity = 1m, UnitPrice = -20m,
                TaxTreatment = TaxTreatment.OutOfScope, DiscountPercentage = 0m, DiscountAmount = 0m,
                TaxAmount = 0m, CreatedAt = DateTime.UtcNow, CreatedBy = "seed"
            });
        });
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*restricted to non-taxable fixed-asset disposal adjustments*");
        (await db.FinancePostingEvents.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task DraftArInvoice_ShouldNotPost()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, invoice => invoice.Status = InvoiceStatus.Draft);
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Only sent AR invoices can be posted.");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
        (await db.JournalEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task UnbalancedArInvoice_ShouldFailBeforeLedgerPosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, invoice =>
        {
            invoice.TotalAmount = 125m;
            invoice.BaseCurrencyAmount = 125m;
        });
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR invoice amount does not match posting line totals.");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.ArInvoicePostingFailed)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task CrossTenantCustomer_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherCustomer = SeedCustomer(db, otherTenantId, fixture.ArAccount.Id);
        fixture.Invoice.BusinessPartnerId = otherCustomer.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR invoice customer was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task CrossTenantRevenueAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherRevenue = SeedAccount(db, otherTenantId, "4000", AccountType.Revenue);
        fixture.Invoice.LineItems.Single().GLAccountId = otherRevenue.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR posting revenue account was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task CrossTenantArControlAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherArAccount = SeedAccount(db, otherTenantId, "1200", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        fixture.Customer.DefaultArAccountId = otherArAccount.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR posting AR control account was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task NonPostableRevenueAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        fixture.RevenueAccount.AllowDirectPosting = false;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR posting revenue account account '4000' does not allow direct posting.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ClosedPeriod_ShouldBeRejectedByPostingEngine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, periodIsOpen: false, periodIsClosed: true);
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posting period is not open.");
        fixture.Invoice.JournalEntryId.Should().BeNull();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task DuplicateArInvoicePosting_ShouldReturnExistingPostingAndAuditDuplicate()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);

        var first = await service.PostAsync(fixture.Invoice.Id);
        var second = await service.PostAsync(fixture.Invoice.Id);

        second.JournalEntryId.Should().Be(first.JournalEntryId);
        (await db.JournalEntries.CountAsync()).Should().Be(1);
        (await db.FinancePostingEvents.CountAsync()).Should().Be(1);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.ArInvoiceDuplicatePostingAttempt)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task PostedArInvoice_ShouldNotBeEditedOrDeleted()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);
        await service.PostAsync(fixture.Invoice.Id);

        var update = () => service.UpdateAsync(new InvoiceUpdateDto
        {
            Id = fixture.Invoice.Id,
            InvoiceDate = fixture.Invoice.InvoiceDate,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            LineItems = new List<InvoiceLineItemUpdateDto>()
        });
        var delete = () => service.DeleteAsync(fixture.Invoice.Id);

        await update.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posted customer invoices cannot be updated. Use a reversal, credit note, or adjustment.");
        await delete.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posted customer invoices cannot be deleted. Use a reversal, credit note, or adjustment.");
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ar-invoice-posting-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static (InvoiceService Service, Mock<ISubledgerPostingService> SubledgerPostingMock) CreateService(
        ApplicationDbContext db,
        Guid tenantId,
        Mock<IWorkflowIntegrationService>? workflow = null,
        ITaxCalculationEngine? taxEngine = null)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var auditService = new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-ar-invoice-posting" }
            });
        var postingEngine = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            auditService);
        var subledgerPostingMock = new Mock<ISubledgerPostingService>();
        var numbering = new Mock<IDocumentNumberingService>();
        numbering.Setup(service => service.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.ARInvoice,
                tenantId,
                It.IsAny<DateTime?>(),
                nameof(Invoice),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"INV-TEST-{Guid.NewGuid():N}");

        var service = new InvoiceService(
            new UnitOfWork(db),
            currentUser.Object,
            taxEngine ?? Mock.Of<ITaxCalculationEngine>(),
            Mock.Of<IInventoryValuationService>(),
            Mock.Of<ILogger<InvoiceService>>(),
            numbering.Object,
            postingEngine,
            auditService,
            workflowIntegration: (workflow ?? DirectWorkflow()).Object);

        return (service, subledgerPostingMock);
    }

    private static Mock<IWorkflowIntegrationService> DirectWorkflow()
    {
        var workflow = new Mock<IWorkflowIntegrationService>();
        workflow.Setup(x => x.SubmitAsync("Invoice", It.IsAny<Guid>())).ReturnsAsync(new WorkflowIntegrationResult(
            new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed }, WorkflowOutcome.Approved, false));
        return workflow;
    }

    private static async Task SeedCompletedInvoiceWorkflowAsync(ApplicationDbContext db, Invoice invoice)
    {
        var type = new WorkflowEntityType { Id = Guid.NewGuid(), TenantId = invoice.TenantId,
            Name = "Customer Invoice", Code = "INVOICE" };
        var instance = new WorkflowInstance { Id = Guid.NewGuid(), TenantId = invoice.TenantId,
            EntityId = invoice.Id, EntityTypeId = type.Id, EntityType = type, WorkflowDefinitionId = Guid.NewGuid(),
            InitiatedById = Guid.NewGuid(), Status = WorkflowInstanceStatus.Completed, CompletedDate = DateTime.UtcNow };
        db.Add(instance);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task OptionalArSubmission_PreparesWithoutPostingOrIncreasingCustomerDebt()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, invoice => invoice.Status = InvoiceStatus.Draft);
        var balance = fixture.Customer.OutstandingBalance;
        var (service, _) = CreateService(db, tenantId);
        var result = await service.SubmitAsync(fixture.Invoice.Id, new FinancePostingProducerContext(FinanceDimensionRouteId.FinanceArCustomerInvoice));
        result.Status.Should().Be("ReadyToPost");
        result.ApprovalRequired.Should().BeFalse();
        result.WorkflowInstanceId.Should().BeNull();
        result.JournalEntryId.Should().BeNull();
        fixture.Customer.OutstandingBalance.Should().Be(balance);
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        (await db.WorkflowInstances.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OptionalArSubmission_ActiveDefinitionOrRetainedInstanceKeepsPending(bool retainedInstance)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, invoice => invoice.Status = InvoiceStatus.Draft);
        var workflow = DirectWorkflow();
        workflow.Setup(x => x.HasActiveApprovalInstanceAsync("Invoice", fixture.Invoice.Id)).ReturnsAsync(retainedInstance);
        workflow.Setup(x => x.HasActiveApprovalWorkflowAsync("Invoice")).ReturnsAsync(!retainedInstance);
        var instanceId = Guid.NewGuid();
        workflow.Setup(x => x.SubmitAsync("Invoice", fixture.Invoice.Id)).ReturnsAsync(new WorkflowIntegrationResult(
            new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.InProgress, WorkflowInstanceId = instanceId }, WorkflowOutcome.Pending));
        var (service, _) = CreateService(db, tenantId, workflow);
        var result = await service.SubmitAsync(fixture.Invoice.Id, new FinancePostingProducerContext(FinanceDimensionRouteId.FinanceArCustomerInvoice));
        result.Status.Should().Be("PendingApproval");
        result.ApprovalRequired.Should().BeTrue();
        result.WorkflowInstanceId.Should().Be(instanceId);
        (await db.JournalEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OptionalArPost_UsesCanonicalReleaseAndIsIdempotent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, invoice => { invoice.Status = InvoiceStatus.ReadyToPost; invoice.ApprovalRequired = false; });
        var balance = fixture.Customer.OutstandingBalance ?? 0m;
        var (service, _) = CreateService(db, tenantId);
        var first = await service.PostAsync(fixture.Invoice.Id);
        var second = await service.PostAsync(fixture.Invoice.Id);
        second.JournalEntryId.Should().Be(first.JournalEntryId);
        second.Status.Should().Be("Sent");
        fixture.Customer.OutstandingBalance.Should().Be(balance + fixture.Invoice.BaseCurrencyAmount);
        (await db.JournalEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task OptionalArPost_DoesNotTreatReadyStatusAloneAsDirectApproval()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, invoice => invoice.Status = InvoiceStatus.ReadyToPost);
        var (service, _) = CreateService(db, tenantId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PostAsync(fixture.Invoice.Id));
        (await db.JournalEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OptionalArRelease_RetiredWorkflowCannotReleaseAnUnapprovedPendingInvoice()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, invoice => invoice.Status = InvoiceStatus.PendingApproval);
        var (service, _) = CreateService(db, tenantId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendInvoiceAsync(fixture.Invoice.Id));
        (await db.JournalEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OptionalArInternalProducer_CannotBypassAnActiveInvoiceProcess()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, invoice => invoice.Status = InvoiceStatus.Draft);
        var workflow = DirectWorkflow();
        workflow.Setup(x => x.HasActiveApprovalWorkflowAsync("Invoice")).ReturnsAsync(true);
        var (service, _) = CreateService(db, tenantId, workflow);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendInvoiceAsync(fixture.Invoice.Id));
        workflow.Verify(x => x.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
        (await db.JournalEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OptionalArSubmission_ConfigurationLookupFailureDoesNotFinalize()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, invoice => invoice.Status = InvoiceStatus.Draft);
        var workflow = DirectWorkflow();
        workflow.Setup(x => x.HasActiveApprovalWorkflowAsync("Invoice")).ThrowsAsync(new InvalidOperationException("Unavailable"));
        var (service, _) = CreateService(db, tenantId, workflow);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitAsync(fixture.Invoice.Id,
            new FinancePostingProducerContext(FinanceDimensionRouteId.FinanceArCustomerInvoice)));
        workflow.Verify(x => x.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
        (await db.JournalEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OptionalArSubmission_RejectsModeDriftAndDoesNotPost()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, invoice => invoice.Status = InvoiceStatus.Draft);
        var workflow = DirectWorkflow();
        workflow.Setup(x => x.SubmitAsync("Invoice", fixture.Invoice.Id)).ReturnsAsync(new WorkflowIntegrationResult(
            new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.InProgress, WorkflowInstanceId = Guid.NewGuid() }, WorkflowOutcome.Pending));
        var (service, _) = CreateService(db, tenantId, workflow);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitAsync(fixture.Invoice.Id,
            new FinancePostingProducerContext(FinanceDimensionRouteId.FinanceArCustomerInvoice)));
        (await db.JournalEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OptionalArSubmission_RejectsForeignTenantTrackedInvoice()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, invoice => invoice.Status = InvoiceStatus.Draft);
        var workflow = DirectWorkflow();
        var (service, _) = CreateService(db, Guid.NewGuid(), workflow);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SubmitAsync(fixture.Invoice.Id,
            new FinancePostingProducerContext(FinanceDimensionRouteId.FinanceArCustomerInvoice)));
        workflow.Verify(x => x.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task OptionalArPost_ClosedPeriodStillBlocksPosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId,
            invoice => { invoice.Status = InvoiceStatus.ReadyToPost; invoice.ApprovalRequired = false; },
            periodIsOpen: false, periodIsClosed: true);
        var (service, _) = CreateService(db, tenantId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PostAsync(fixture.Invoice.Id));
        (await db.JournalEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OptionalArSubmission_CompletedActiveProcessRequiresProofAndWaitsForExplicitPost()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, invoice => invoice.Status = InvoiceStatus.Draft);
        await SeedCompletedInvoiceWorkflowAsync(db, fixture.Invoice);
        var instance = await db.WorkflowInstances.SingleAsync();
        var workflow = DirectWorkflow();
        workflow.Setup(x => x.HasActiveApprovalWorkflowAsync("Invoice")).ReturnsAsync(true);
        workflow.Setup(x => x.SubmitAsync("Invoice", fixture.Invoice.Id)).ReturnsAsync(new WorkflowIntegrationResult(
            new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed, WorkflowInstanceId = instance.Id }, WorkflowOutcome.Approved));
        var (service, _) = CreateService(db, tenantId, workflow);
        var result = await service.SubmitAsync(fixture.Invoice.Id, new FinancePostingProducerContext(FinanceDimensionRouteId.FinanceArCustomerInvoice));
        result.Status.Should().Be("Approved");
        result.ApprovalRequired.Should().BeTrue();
        result.JournalEntryId.Should().BeNull();
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        var posted = await service.PostAsync(fixture.Invoice.Id);
        posted.Status.Should().Be("Sent");
        posted.JournalEntryId.Should().NotBeNull();
    }

    [Fact]
    public async Task OptionalArSubmission_CompletedResultWithoutSavedWorkflowProofIsRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, invoice => invoice.Status = InvoiceStatus.Draft);
        var workflow = DirectWorkflow();
        workflow.Setup(x => x.HasActiveApprovalWorkflowAsync("Invoice")).ReturnsAsync(true);
        workflow.Setup(x => x.SubmitAsync("Invoice", fixture.Invoice.Id)).ReturnsAsync(new WorkflowIntegrationResult(
            new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed, WorkflowInstanceId = Guid.NewGuid() }, WorkflowOutcome.Approved));
        var (service, _) = CreateService(db, tenantId, workflow);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitAsync(fixture.Invoice.Id,
            new FinancePostingProducerContext(FinanceDimensionRouteId.FinanceArCustomerInvoice)));
        (await db.JournalEntries.CountAsync()).Should().Be(0);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId)
    {
        var userId = Guid.NewGuid().ToString();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(userId);
        currentUser.SetupGet(x => x.UserName).Returns("ar.invoice.poster");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("ar-invoice-posting-tests");
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        return currentUser;
    }

    private static async Task<ArInvoiceFixture> SeedSentArInvoiceAsync(
        ApplicationDbContext db,
        Guid tenantId,
        Action<Invoice>? configureInvoice = null,
        bool periodIsOpen = true,
        bool periodIsClosed = false)
    {
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId, periodIsOpen, periodIsClosed);
        var arAccount = SeedAccount(db, tenantId, "1200", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var revenueAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        var taxAccount = SeedAccount(db, tenantId, "2200", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        var discountAccount = SeedAccount(db, tenantId, "5200", AccountType.Expense);
        var customer = SeedCustomer(db, tenantId, arAccount.Id);

        db.Set<FinanceSettings>().Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            ControlAccountArId = arAccount.Id,
            ControlAccountTaxId = taxAccount.Id,
            DiscountAllowedAccountId = discountAccount.Id
        });

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "INV-2026-00001",
            BusinessPartnerId = customer.Id,
            CustomerName = customer.PartnerName,
            CustomerAddress = customer.PhysicalAddress,
            InvoiceDate = new DateTime(2026, 7, 5),
            DueDate = new DateTime(2026, 8, 4),
            SubTotal = 100m,
            TaxAmount = 0m,
            DiscountAmount = 0m,
            TotalAmount = 100m,
            PaidAmount = 0m,
            CreditedAmount = 0m,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BaseCurrencyAmount = 100m,
            PaymentTermsDays = 30,
            Status = InvoiceStatus.Sent,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

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
            TaxRate = 0m,
            TaxAmount = 0m,
            DiscountPercentage = 0m,
            DiscountAmount = 0m,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });

        configureInvoice?.Invoke(invoice);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        return new ArInvoiceFixture(invoice, customer, revenueAccount, arAccount, taxAccount, discountAccount);
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

    private static ExchangeRate SeedApprovedDailyRate(
        ApplicationDbContext db,
        Guid tenantId,
        string targetCurrency,
        decimal rate,
        ExchangeRateQuoteSide quoteSide = ExchangeRateQuoteSide.Mid)
    {
        var exchangeRate = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrencyCode = "GHS",
            TargetCurrencyCode = targetCurrency,
            Rate = rate,
            InverseRate = decimal.Round(1m / rate, 6),
            EffectiveDate = new DateTime(2026, 7, 5),
            RateType = ExchangeRateType.Daily,
            QuoteSide = quoteSide,
            RateSource = "Regression approved rate",
            IsActive = true,
            ApprovalStatus = RateApprovalStatus.Approved,
            CreatedByUserId = Guid.NewGuid(),
            CreatedDate = DateTime.UtcNow
        };
        db.ExchangeRates.Add(exchangeRate);
        return exchangeRate;
    }

    private static void EnableCurrencyForAccounts(
        ApplicationDbContext db,
        Guid tenantId,
        string currencyCode,
        params Account[] accounts)
    {
        foreach (var account in accounts)
        {
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

    private sealed record ArInvoiceFixture(
        Invoice Invoice,
        BusinessPartner Customer,
        Account RevenueAccount,
        Account ArAccount,
        Account TaxAccount,
        Account DiscountAccount);
}
