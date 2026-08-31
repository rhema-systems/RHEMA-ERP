using ErpSystem.Api.Services;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Api.Services.Finance.MultiCurrency;
using ErpSystem.Api.Services.Finance.Settings;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FxFunctionalCurrencyGovernanceTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task DirectionalPolicyActivationRequiresApprovedDailyRatesForActiveCurrencies()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedCurrency(db, tenantId, "GHS", isBase: true);
        SeedCurrency(db, tenantId, "USD", isBase: false);
        SeedFinanceSettings(db, tenantId, "GHS");
        await db.SaveChangesAsync();

        var service = CreateFinanceSettingsService(db, tenantId);

        await service.Invoking(item => item.UpdateSettingsAsync(new UpdateFinanceSettingsDto
            {
                DirectionalExchangeRatePolicyEnabled = true,
                ArInvoiceQuoteSide = "Buying",
                ArSettlementQuoteSide = "Buying",
                ApInvoiceQuoteSide = "Selling",
                ApSettlementQuoteSide = "Selling"
            }))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*USD Buying*USD Selling*");

        (await db.FinanceSettings.SingleAsync(item => item.TenantId == tenantId))
            .DirectionalExchangeRatePolicyEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData("Budget")]
    [InlineData("Spot")]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task UnsupportedRateTypesCannotBeCreated(string rateType)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedCurrency(db, tenantId, "GHS", isBase: true);
        SeedCurrency(db, tenantId, "USD", isBase: false);
        await db.SaveChangesAsync();

        var service = CreateExchangeRateService(db, tenantId);
        await service.Invoking(item => item.CreateExchangeRateAsync(new CreateExchangeRateDto
            {
                BaseCurrencyCode = "GHS",
                TargetCurrencyCode = "USD",
                Rate = 15m,
                EffectiveDate = new DateTime(2026, 8, 31),
                RateType = rateType,
                RateSource = "Manual"
            }))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*reserved for future governed workflows*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task TenantCanConfigureFunctionalCurrencyBeforeAccountingActivity()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedCurrency(db, tenantId, "GHS", isBase: true);
        SeedCurrency(db, tenantId, "USD", isBase: false);
        SeedFinanceSettings(db, tenantId, "GHS");
        await db.SaveChangesAsync();

        var service = CreateFinanceSettingsService(db, tenantId);

        var result = await service.UpdateSettingsAsync(new UpdateFinanceSettingsDto { BaseCurrency = "usd" });

        result.BaseCurrency.Should().Be("USD");
        result.FunctionalCurrencyLocked.Should().BeFalse();
        (await db.Currencies.SingleAsync(c => c.TenantId == tenantId && c.CurrencyCode == "USD")).IsBaseCurrency.Should().BeTrue();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task FunctionalCurrencyChangeAfterAccountingActivityIsRejectedAndAudited()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedCurrency(db, tenantId, "GHS", isBase: true);
        SeedCurrency(db, tenantId, "USD", isBase: false);
        var settings = SeedFinanceSettings(db, tenantId, "GHS");
        db.FinancePostingEvents.Add(new FinancePostingEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceModule = "TEST",
            SourceDocumentType = "Activity",
            SourceDocumentId = Guid.NewGuid(),
            PostingAction = "Post",
            PostingStatus = "Posted",
            PostingDate = new DateTime(2026, 7, 1),
            FunctionalCurrencyCode = "GHS",
            BookClassification = "IFRS"
        });
        await db.SaveChangesAsync();

        var audit = new CapturingFinanceAuditService();
        var service = CreateFinanceSettingsService(db, tenantId, audit);

        await service.Invoking(s => s.UpdateSettingsAsync(new UpdateFinanceSettingsDto { BaseCurrency = "USD" }))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot change functional currency*");

        settings.FunctionalCurrencyLocked.Should().BeTrue();
        settings.FunctionalCurrencyLockedAt.Should().NotBeNull();
        audit.Events.Should().Contain(e => e.EventType == FinanceAuditEvents.FunctionalCurrencyChangeRejected);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task ForeignCurrencyPostingWithoutFunctionalCurrencyConfigurationIsRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "1000", AccountType.Asset, isMultiCurrency: true);
        var credit = SeedAccount(db, tenantId, "4000", AccountType.Revenue, isMultiCurrency: true);
        SeedExchangeRate(db, tenantId, "GHS", "USD", 15m, new DateTime(2026, 7, 1));
        await db.SaveChangesAsync();

        var service = CreatePostingEngine(db, tenantId);
        var request = CreateForeignPostingRequest(tenantId, debit.Id, credit.Id, 10m, 15m);

        await service.Invoking(s => s.PostAsync(request))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*functional currency must be configured*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task SameCurrencyPostingDoesNotRequireExchangeRateLookup()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var credit = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var service = CreatePostingEngine(db, tenantId);
        var request = CreateSameCurrencyPostingRequest(tenantId, debit.Id, credit.Id);

        var result = await service.PostAsync(request);

        result.FunctionalCurrencyCode.Should().Be("GHS");
        (await db.AccountTransactions.CountAsync(t => t.ExchangeRateId != null)).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task ForeignCurrencyPostingRequiresEffectiveTenantExchangeRate()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedFinanceSettings(db, tenantId, "GHS");
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "1000", AccountType.Asset, isMultiCurrency: true);
        var credit = SeedAccount(db, tenantId, "4000", AccountType.Revenue, isMultiCurrency: true);
        await db.SaveChangesAsync();

        var service = CreatePostingEngine(db, tenantId);

        await service.Invoking(s => s.PostAsync(CreateForeignPostingRequest(tenantId, debit.Id, credit.Id, 10m, 15m)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*No effective exchange rate exists*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task CrossTenantExchangeRateIsRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedTenant(db, otherTenantId, "GHS");
        SeedFinanceSettings(db, tenantId, "GHS");
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "1000", AccountType.Asset, isMultiCurrency: true);
        var credit = SeedAccount(db, tenantId, "4000", AccountType.Revenue, isMultiCurrency: true);
        var otherRate = SeedExchangeRate(db, otherTenantId, "GHS", "USD", 15m, new DateTime(2026, 7, 1));
        await db.SaveChangesAsync();

        var request = CreateForeignPostingRequest(tenantId, debit.Id, credit.Id, 10m, 15m);
        foreach (var line in request.Lines)
        {
            line.ExchangeRateId = otherRate.Id;
        }

        var service = CreatePostingEngine(db, tenantId);

        await service.Invoking(s => s.PostAsync(request))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*No effective exchange rate exists*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task ExchangeRateServiceRejectsZeroNegativeAndOverlappingRates()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedCurrency(db, tenantId, "GHS", isBase: true);
        SeedCurrency(db, tenantId, "USD", isBase: false);
        SeedExchangeRate(db, tenantId, "GHS", "USD", 15m, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        await db.SaveChangesAsync();

        var service = CreateExchangeRateService(db, tenantId);

        await service.Invoking(s => s.CreateExchangeRateAsync(new CreateExchangeRateDto
            {
                BaseCurrencyCode = "GHS",
                TargetCurrencyCode = "USD",
                Rate = 0m,
                EffectiveDate = new DateTime(2027, 1, 1),
                RateType = ExchangeRateType.Daily.ToString(),
                RateSource = "Manual"
            }))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*greater than zero*");

        await service.Invoking(s => s.CreateExchangeRateAsync(new CreateExchangeRateDto
            {
                BaseCurrencyCode = "GHS",
                TargetCurrencyCode = "USD",
                Rate = 16m,
                EffectiveDate = new DateTime(2026, 6, 1),
                ExpiryDate = new DateTime(2026, 6, 30),
                RateType = ExchangeRateType.Daily.ToString(),
                RateSource = "Manual"
            }))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*overlaps*");
    }

    [Fact]
    [Trait("Batch", "FinanceWorkflowApprovalHardening")]
    [Trait("Category", "Workflow")]
    public async Task ExchangeRateRequiresWorkflowApprovalBeforeUse()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedCurrency(db, tenantId, "GHS", isBase: true);
        SeedCurrency(db, tenantId, "USD", isBase: false);
        await db.SaveChangesAsync();
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(x => x.StartApprovalWorkflowAsync("ExchangeRate", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = Guid.NewGuid()
            });
        var service = CreateExchangeRateService(db, tenantId, workflow.Object);

        var created = await service.CreateExchangeRateAsync(new CreateExchangeRateDto
        {
            BaseCurrencyCode = "GHS",
            TargetCurrencyCode = "USD",
            Rate = 15m,
            EffectiveDate = new DateTime(2026, 7, 1),
            RateType = ExchangeRateType.Daily.ToString(),
            RateSource = "Manual",
            ApprovalStatus = RateApprovalStatus.Approved.ToString()
        });

        created.ApprovalStatus.Should().Be(RateApprovalStatus.Pending.ToString());
        (await service.GetCurrentRateAsync("USD", "GHS", new DateTime(2026, 7, 5))).Should().BeNull();

        var stored = await db.ExchangeRates.SingleAsync(r => r.Id == created.Id);
        stored.ApprovalStatus = RateApprovalStatus.Approved;
        stored.ApprovalDate = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var approved = await service.GetCurrentRateAsync("USD", "GHS", new DateTime(2026, 7, 5));

        approved.Should().NotBeNull();
        approved!.Id.Should().Be(created.Id);
        workflow.Verify(x => x.StartApprovalWorkflowAsync("ExchangeRate", created.Id), Times.Once);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task PostingCapturesRateSnapshotLocksRateAndEmitsAudit()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedFinanceSettings(db, tenantId, "GHS");
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "1000", AccountType.Asset, isMultiCurrency: true);
        var credit = SeedAccount(db, tenantId, "4000", AccountType.Revenue, isMultiCurrency: true);
        var rate = SeedExchangeRate(db, tenantId, "GHS", "USD", 15m, new DateTime(2026, 7, 1));
        await db.SaveChangesAsync();

        var audit = new CapturingFinanceAuditService();
        var service = CreatePostingEngine(db, tenantId, audit);

        var result = await service.PostAsync(CreateForeignPostingRequest(tenantId, debit.Id, credit.Id, 10m, 15m));

        var lines = await db.AccountTransactions
            .Where(t => t.JournalEntryId == result.JournalEntryId)
            .OrderBy(t => t.LineNumber)
            .ToListAsync();
        lines.Should().OnlyContain(t => t.FunctionalCurrencyCode == "GHS");
        lines.Should().OnlyContain(t => t.TransactionCurrency == "USD");
        lines.Should().OnlyContain(t => t.ExchangeRateId == rate.Id);
        lines.Should().OnlyContain(t => t.ExchangeRate == 15m);
        lines.Sum(t => t.DebitAmount).Should().Be(lines.Sum(t => t.CreditAmount));
        lines.Sum(t => t.TransactionDebitAmount ?? 0m).Should().Be(10m);
        lines.Sum(t => t.TransactionCreditAmount ?? 0m).Should().Be(10m);

        var postingEvent = await db.FinancePostingEvents.SingleAsync(e => e.Id == result.PostingEventId);
        postingEvent.HasForeignCurrencyLines.Should().BeTrue();
        postingEvent.PrimaryTransactionCurrencyCode.Should().Be("USD");
        postingEvent.PrimaryExchangeRateId.Should().Be(rate.Id);
        postingEvent.PrimaryExchangeRate.Should().Be(15m);

        rate.HasBeenUsedInTransactions.Should().BeTrue();
        rate.TransactionCount.Should().Be(2);
        audit.Events.Should().Contain(e => e.EventType == FinanceAuditEvents.ExchangeRateUsedInPosting);
        audit.Events.Should().Contain(e => e.EventType == FinanceAuditEvents.CurrencySnapshotCapturedInPosting);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task UsedExchangeRateCannotBeEditedOrDeletedAndSnapshotRemainsStable()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedCurrency(db, tenantId, "GHS", isBase: true);
        SeedCurrency(db, tenantId, "USD", isBase: false);
        SeedFinanceSettings(db, tenantId, "GHS");
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "1000", AccountType.Asset, isMultiCurrency: true);
        var credit = SeedAccount(db, tenantId, "4000", AccountType.Revenue, isMultiCurrency: true);
        var rate = SeedExchangeRate(db, tenantId, "GHS", "USD", 15m, new DateTime(2026, 7, 1));
        await db.SaveChangesAsync();

        await CreatePostingEngine(db, tenantId).PostAsync(CreateForeignPostingRequest(tenantId, debit.Id, credit.Id, 10m, 15m));
        var service = CreateExchangeRateService(db, tenantId);

        await service.Invoking(s => s.UpdateExchangeRateAsync(rate.Id, new UpdateExchangeRateDto
            {
                Rate = 16m,
                RateType = ExchangeRateType.Daily.ToString(),
                RateSource = "Manual"
            }))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*used in transactions*");

        await service.Invoking(s => s.DeleteExchangeRateAsync(rate.Id))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*used in transactions*");

        (await db.AccountTransactions.Where(t => t.ExchangeRateId == rate.Id).Select(t => t.ExchangeRate).Distinct().SingleAsync())
            .Should()
            .Be(15m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task DeterministicRateSelectionUsesEffectiveRateForPostingDate()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedFinanceSettings(db, tenantId, "GHS");
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "1000", AccountType.Asset, isMultiCurrency: true);
        var credit = SeedAccount(db, tenantId, "4000", AccountType.Revenue, isMultiCurrency: true);
        var oldRate = SeedExchangeRate(db, tenantId, "GHS", "USD", 12m, new DateTime(2026, 1, 1), new DateTime(2026, 6, 30));
        SeedExchangeRate(db, tenantId, "GHS", "USD", 15m, new DateTime(2026, 7, 1));
        await db.SaveChangesAsync();

        var request = CreateForeignPostingRequest(tenantId, debit.Id, credit.Id, 10m, 12m, postingDate: new DateTime(2026, 5, 15));

        var result = await CreatePostingEngine(db, tenantId).PostAsync(request);

        var lines = await db.AccountTransactions.Where(t => t.JournalEntryId == result.JournalEntryId).ToListAsync();
        lines.Should().OnlyContain(t => t.ExchangeRateId == oldRate.Id);
        lines.Should().OnlyContain(t => t.ExchangeRate == 12m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task DirectionalPolicy_ShouldUseBuyingForArSettlementAndSellingForApSettlement()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        var settings = SeedFinanceSettings(db, tenantId, "GHS");
        settings.DirectionalExchangeRatePolicyEnabled = true;
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "1010", AccountType.Asset, isMultiCurrency: true);
        var credit = SeedAccount(db, tenantId, "2010", AccountType.Liability, isMultiCurrency: true);
        SeedExchangeRate(db, tenantId, "GHS", "USD", 15m, new DateTime(2026, 7, 1), quoteSide: ExchangeRateQuoteSide.Mid);
        var buying = SeedExchangeRate(db, tenantId, "GHS", "USD", 14.8m, new DateTime(2026, 7, 1), quoteSide: ExchangeRateQuoteSide.Buying);
        var selling = SeedExchangeRate(db, tenantId, "GHS", "USD", 15.2m, new DateTime(2026, 7, 1), quoteSide: ExchangeRateQuoteSide.Selling);
        await db.SaveChangesAsync();

        var arRequest = CreateForeignPostingRequest(tenantId, debit.Id, credit.Id, 10m, buying.Rate);
        arRequest.SourceModule = "AR";
        arRequest.SourceDocumentType = "CustomerPayment";
        var arResult = await CreatePostingEngine(db, tenantId).PostAsync(arRequest);

        var apRequest = CreateForeignPostingRequest(tenantId, debit.Id, credit.Id, 10m, selling.Rate);
        apRequest.SourceModule = "AP";
        apRequest.SourceDocumentType = "VendorPayment";
        var apResult = await CreatePostingEngine(db, tenantId).PostAsync(apRequest);

        (await db.AccountTransactions
                .Where(t => t.JournalEntryId == arResult.JournalEntryId)
                .Select(t => t.ExchangeRateId)
                .Distinct()
                .SingleAsync())
            .Should()
            .Be(buying.Id);
        (await db.AccountTransactions
                .Where(t => t.JournalEntryId == apResult.JournalEntryId)
                .Select(t => t.ExchangeRateId)
                .Distinct()
                .SingleAsync())
            .Should()
            .Be(selling.Id);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task DirectionalRateOverride_ShouldRequireReasonApprovalAndEmitAudit()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        var settings = SeedFinanceSettings(db, tenantId, "GHS");
        settings.DirectionalExchangeRatePolicyEnabled = true;
        settings.RequireExchangeRateOverrideApproval = true;
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "1020", AccountType.Asset, isMultiCurrency: true);
        var credit = SeedAccount(db, tenantId, "2020", AccountType.Liability, isMultiCurrency: true);
        var mid = SeedExchangeRate(db, tenantId, "GHS", "USD", 15m, new DateTime(2026, 7, 1), quoteSide: ExchangeRateQuoteSide.Mid);
        SeedExchangeRate(db, tenantId, "GHS", "USD", 14.8m, new DateTime(2026, 7, 1), quoteSide: ExchangeRateQuoteSide.Buying);
        await db.SaveChangesAsync();

        var request = CreateForeignPostingRequest(tenantId, debit.Id, credit.Id, 10m, mid.Rate);
        request.SourceModule = "AR";
        request.SourceDocumentType = "CustomerPayment";
        foreach (var line in request.Lines)
        {
            line.ExchangeRateId = mid.Id;
        }

        var audit = new CapturingFinanceAuditService();
        var service = CreatePostingEngine(db, tenantId, audit);

        await service.Invoking(s => s.PostAsync(request))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*requires a reason*");

        request.ExchangeRateOverrideReason = "Actual bank settlement rate";
        request.ExchangeRateOverrideApprovedByUserId = Guid.NewGuid();
        request.ExchangeRateOverrideApprovedAt = DateTime.UtcNow;

        var result = await service.PostAsync(request);

        (await db.AccountTransactions
                .Where(t => t.JournalEntryId == result.JournalEntryId)
                .Select(t => t.ExchangeRateId)
                .Distinct()
                .SingleAsync())
            .Should()
            .Be(mid.Id);
        audit.Events.Should().Contain(e =>
            e.EventType == FinanceAuditEvents.ExchangeRatePolicyOverrideUsed
            && e.SourceDocumentId == request.SourceDocumentId);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task ClosedPeriodForeignCurrencyPostingIsRejectedThroughPostingEngine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedFinanceSettings(db, tenantId, "GHS");
        SeedOpenPeriod(db, tenantId, isOpen: false, isClosed: true);
        var debit = SeedAccount(db, tenantId, "1000", AccountType.Asset, isMultiCurrency: true);
        var credit = SeedAccount(db, tenantId, "4000", AccountType.Revenue, isMultiCurrency: true);
        SeedExchangeRate(db, tenantId, "GHS", "USD", 15m, new DateTime(2026, 7, 1));
        await db.SaveChangesAsync();

        await CreatePostingEngine(db, tenantId)
            .Invoking(s => s.PostAsync(CreateForeignPostingRequest(tenantId, debit.Id, credit.Id, 10m, 15m)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*period is not open*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task AccountCurrencyRestrictionRejectsForeignPostingToSingleCurrencyAccount()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedFinanceSettings(db, tenantId, "GHS");
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "1000", AccountType.Asset, isMultiCurrency: false, currencyCode: "GHS");
        var credit = SeedAccount(db, tenantId, "4000", AccountType.Revenue, isMultiCurrency: true);
        SeedExchangeRate(db, tenantId, "GHS", "USD", 15m, new DateTime(2026, 7, 1));
        await db.SaveChangesAsync();

        await CreatePostingEngine(db, tenantId)
            .Invoking(s => s.PostAsync(CreateForeignPostingRequest(tenantId, debit.Id, credit.Id, 10m, 15m)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*only accepts GHS transactions*");
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"fx-foundation-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static FinancePostingEngine CreatePostingEngine(
        ApplicationDbContext db,
        Guid tenantId,
        IFinanceAuditService? auditService = null)
    {
        return new FinancePostingEngine(db, CreateCurrentUser(tenantId).Object, Mock.Of<ILogger<FinancePostingEngine>>(), auditService);
    }

    private static FinanceSettingsService CreateFinanceSettingsService(
        ApplicationDbContext db,
        Guid tenantId,
        IFinanceAuditService? auditService = null)
    {
        var currentUser = CreateCurrentUser(tenantId).Object;
        return new FinanceSettingsService(db, currentUser, new TenantSettingsService(db, currentUser), auditService);
    }

    private static ExchangeRateService CreateExchangeRateService(ApplicationDbContext db, Guid tenantId, IWorkflowService? workflowService = null)
    {
        var currentUser = CreateCurrentUser(tenantId).Object;
        return new ExchangeRateService(
            new UnitOfWork(db),
            currentUser,
            new TenantSettingsService(db, currentUser),
            Mock.Of<ILogger<ExchangeRateService>>(),
            workflowService: workflowService);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(x => x.UserName).Returns("fx.tester");
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("fx-foundation-tests");
        return currentUser;
    }

    private static void SeedTenant(ApplicationDbContext db, Guid tenantId, string baseCurrency)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {tenantId:N}"[..20],
            Code = tenantId.ToString("N")[..6].ToUpperInvariant(),
            Status = TenantStatus.Active,
            BaseCurrency = baseCurrency,
            BaseCurrencyName = baseCurrency,
            CurrencySymbol = baseCurrency,
            CurrencyDecimalPlaces = 2,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        });
    }

    private static FinanceSettings SeedFinanceSettings(ApplicationDbContext db, Guid tenantId, string baseCurrency)
    {
        var settings = new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CoaType = "Segmented",
            BaseCurrency = baseCurrency,
            AccountSeparator = "-",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        };
        db.FinanceSettings.Add(settings);
        return settings;
    }

    private static Currency SeedCurrency(ApplicationDbContext db, Guid tenantId, string code, bool isBase)
    {
        var currency = new Currency
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CurrencyCode = code,
            NumericCode = code,
            CurrencyName = code,
            CurrencySymbol = code,
            DecimalPlaces = 2,
            IsBaseCurrency = isBase,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        };
        db.Currencies.Add(currency);
        return currency;
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
            PeriodName = "FY2026",
            PeriodCode = "2026",
            PeriodNumber = 1,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            PeriodDays = 365,
            PeriodStatus = isClosed ? "Closed" : "Open",
            IsOpen = isOpen,
            IsClosed = isClosed,
            IsLocked = false
        };
        db.FiscalPeriods.Add(period);
        return period;
    }

    private static Account SeedAccount(
        ApplicationDbContext db,
        Guid tenantId,
        string accountNumber,
        AccountType accountType,
        bool isMultiCurrency = false,
        string currencyCode = "GHS")
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = accountNumber,
            AccountNumber = accountNumber,
            AccountName = $"Account {accountNumber}",
            AccountType = accountType,
            Status = AccountStatus.Active,
            CurrencyCode = currencyCode,
            IsMultiCurrency = isMultiCurrency,
            AllowDirectPosting = true
        };
        db.Accounts.Add(account);
        return account;
    }

    private static ExchangeRate SeedExchangeRate(
        ApplicationDbContext db,
        Guid tenantId,
        string baseCurrency,
        string targetCurrency,
        decimal rate,
        DateTime effectiveDate,
        DateTime? endDate = null,
        ExchangeRateQuoteSide quoteSide = ExchangeRateQuoteSide.Mid)
    {
        var exchangeRate = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrencyCode = baseCurrency,
            TargetCurrencyCode = targetCurrency,
            Rate = rate,
            InverseRate = 1 / rate,
            EffectiveDate = effectiveDate.Date,
            EndDate = endDate?.Date,
            RateType = ExchangeRateType.Daily,
            QuoteSide = quoteSide,
            RateSource = "Bank of Ghana",
            ApprovalStatus = RateApprovalStatus.Approved,
            IsActive = true,
            CreatedByUserId = Guid.NewGuid(),
            CreatedDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        };
        db.ExchangeRates.Add(exchangeRate);
        return exchangeRate;
    }

    private static FinancePostingRequestDto CreateSameCurrencyPostingRequest(Guid tenantId, Guid debitAccountId, Guid creditAccountId)
    {
        return new FinancePostingRequestDto
        {
            SourceModule = "FXTEST",
            SourceDocumentType = "SameCurrency",
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentTenantId = tenantId,
            PostingAction = "Post",
            SourceDocumentReference = "FX-SAME",
            Description = "Same currency posting",
            PostingDate = new DateTime(2026, 7, 4),
            JournalType = "System Generated",
            BookClassification = "IFRS",
            FunctionalCurrencyCode = "GHS",
            Lines = new[]
            {
                new FinancePostingLineDto { AccountId = debitAccountId, Description = "Cash", DebitAmount = 100m },
                new FinancePostingLineDto { AccountId = creditAccountId, Description = "Revenue", CreditAmount = 100m }
            }
        };
    }

    private static FinancePostingRequestDto CreateForeignPostingRequest(
        Guid tenantId,
        Guid debitAccountId,
        Guid creditAccountId,
        decimal transactionAmount,
        decimal rate,
        DateTime? postingDate = null)
    {
        var functionalAmount = decimal.Round(transactionAmount * rate, 2, MidpointRounding.AwayFromZero);
        return new FinancePostingRequestDto
        {
            SourceModule = "FXTEST",
            SourceDocumentType = "ForeignCurrency",
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentTenantId = tenantId,
            PostingAction = "Post",
            SourceDocumentReference = "FX-USD",
            Description = "Foreign currency posting",
            PostingDate = postingDate ?? new DateTime(2026, 7, 4),
            JournalType = "System Generated",
            BookClassification = "IFRS",
            FunctionalCurrencyCode = "GHS",
            Lines = new[]
            {
                new FinancePostingLineDto
                {
                    AccountId = debitAccountId,
                    Description = "USD cash",
                    DebitAmount = functionalAmount,
                    TransactionCurrency = "USD",
                    ForeignCurrencyAmount = transactionAmount,
                    ExchangeRate = rate
                },
                new FinancePostingLineDto
                {
                    AccountId = creditAccountId,
                    Description = "USD revenue",
                    CreditAmount = functionalAmount,
                    TransactionCurrency = "USD",
                    ForeignCurrencyAmount = transactionAmount,
                    ExchangeRate = rate
                }
            }
        };
    }

    private sealed class CapturingFinanceAuditService : IFinanceAuditService
    {
        public List<FinanceAuditEventDto> Events { get; } = new();

        public Task<AuditLog> RecordAsync(FinanceAuditEventDto auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.FromResult(new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = auditEvent.TenantId,
                Action = auditEvent.EventType,
                Resource = auditEvent.Resource ?? "Finance",
                ResourceId = auditEvent.ResourceId,
                Timestamp = DateTime.UtcNow
            });
        }

        public Task<IReadOnlyList<AuditLog>> GetAuditTrailAsync(
            Guid tenantId,
            string resource,
            string resourceId,
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AuditLog>>(Array.Empty<AuditLog>());
        }
    }
}
