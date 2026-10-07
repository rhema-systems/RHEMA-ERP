using ErpSystem.Api.Services;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Api.Services.Finance.Fiscal;
using ErpSystem.Api.Services.Finance.MultiCurrency;
using ErpSystem.Api.Services.Finance.Settings;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using ErpSystem.Web.Services;
using FluentAssertions;
using System.Collections;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FxFunctionalCurrencyGovernanceTests
{
    [Fact]
    public async Task ExchangeRateContract_StoresAndReturnsSourceToTargetDirection()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedCurrency(db, tenantId, "GHS", isBase: true);
        SeedCurrency(db, tenantId, "USD", isBase: false);
        await db.SaveChangesAsync();

        var dto = await CreateExchangeRateService(db, tenantId).CreateExchangeRateAsync(new CreateExchangeRateDto
        {
            BaseCurrencyCode = "GHS", TargetCurrencyCode = "USD", Rate = 0.08m,
            EffectiveDate = new DateTime(2026, 9, 21), RateType = "Daily",
            QuoteSide = "Mid", RateSource = "Bank of Ghana"
        });

        dto.Rate.Should().Be(0.08m, "the public contract is 1 source/base unit = Rate target units");
        dto.InverseRate.Should().Be(12.5m);
        var stored = await db.ExchangeRates.SingleAsync(item => item.Id == dto.Id);
        stored.Rate.Should().Be(0.08m);
        stored.InverseRate.Should().Be(12.5m);
    }

    [Theory]
    [InlineData(52, 13, true)]
    [InlineData(52, 26, true)]
    [InlineData(52, 39, true)]
    [InlineData(52, 52, true)]
    [InlineData(53, 52, false)]
    [InlineData(53, 53, true)]
    [InlineData(52, 12, false)]
    public void WeeklyFiscalCalendar_ShouldRecognizeControlledQuarterBoundaries(
        int fiscalYearPeriodCount,
        int periodNumber,
        bool expected)
    {
        FiscalCalendarBoundaryPolicy.IsQuarterEnd(
                PeriodType.Weekly,
                periodNumber,
                fiscalYearPeriodCount)
            .Should().Be(expected);
    }

    [Fact]
    public async Task QuarterEndRate_ShouldAcceptConfiguredWeeklyQuarterBoundary()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedCurrency(db, tenantId, "GHS", isBase: true);
        SeedCurrency(db, tenantId, "USD", isBase: false);
        var fiscalYear = new FiscalYear
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearName = "FY2026 52-week",
            FiscalYearCode = "FY2026-W",
            Year = 2026,
            FiscalYearType = "52-53 Week",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 30),
            TotalDays = 364,
            NumberOfPeriods = 52
        };
        db.FiscalYears.Add(fiscalYear);
        db.FiscalPeriods.Add(new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = fiscalYear.Id,
            PeriodName = "Week 13",
            PeriodCode = "FY2026-W13",
            PeriodNumber = 13,
            PeriodType = PeriodType.Weekly,
            StartDate = new DateTime(2026, 3, 26),
            EndDate = new DateTime(2026, 4, 1),
            PeriodDays = 7
        });
        await db.SaveChangesAsync();

        var rate = await CreateExchangeRateService(db, tenantId).CreateExchangeRateAsync(new CreateExchangeRateDto
        {
            BaseCurrencyCode = "GHS",
            TargetCurrencyCode = "USD",
            Rate = 15m,
            EffectiveDate = new DateTime(2026, 4, 1),
            RateType = ExchangeRateType.QuarterEnd.ToString(),
            RateSource = "Manual"
        });

        rate.RateType.Should().Be(ExchangeRateType.QuarterEnd.ToString());
    }

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

    [Theory]
    [InlineData("Average")]
    [InlineData("MonthEnd")]
    [InlineData("QuarterEnd")]
    [InlineData("YearEnd")]
    [InlineData("Fixed")]
    [InlineData("GhanaStatutory")]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task NeutralAccountingRateTypesRejectBuyingAndSellingQuotes(string rateType)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedCurrency(db, tenantId, "GHS", isBase: true);
        SeedCurrency(db, tenantId, "USD", isBase: false);
        await db.SaveChangesAsync();

        var action = () => CreateExchangeRateService(db, tenantId).CreateExchangeRateAsync(new CreateExchangeRateDto
        {
            BaseCurrencyCode = "GHS",
            TargetCurrencyCode = "USD",
            Rate = 0.08m,
            EffectiveDate = new DateTime(2026, 9, 30),
            RateType = rateType,
            QuoteSide = ExchangeRateQuoteSide.Buying.ToString(),
            RateSource = "Bank of Ghana",
            SourceReference = "BoG regression evidence"
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must use the Mid / Reference quote side*");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void FinanceWorkflowCatalogueIncludesExchangeRateApproval()
    {
        var seedMethod = typeof(DatabaseSeedingService).GetMethod(
            "GetFinanceWorkflowSeedSpecs",
            BindingFlags.Static | BindingFlags.NonPublic);

        seedMethod.Should().NotBeNull();
        var entityCodes = ((IEnumerable)seedMethod!.Invoke(null, null)!)
            .Cast<object>()
            .Select(spec => spec.GetType().GetProperty("EntityCode")!.GetValue(spec)?.ToString());

        entityCodes.Should().Contain("ExchangeRate");
    }

    [Theory]
    [InlineData("MonthEnd", "2026-08-15", "actual calendar month-end")]
    [InlineData("QuarterEnd", "2026-08-31", "configured fiscal quarter-end")]
    [InlineData("YearEnd", "2026-08-31", "configured fiscal year-end")]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task ClosingRateTypesRejectDatesWithoutTheirCalendarMeaning(
        string rateType,
        string effectiveDate,
        string expectedMessage)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedCurrency(db, tenantId, "GHS", isBase: true);
        SeedCurrency(db, tenantId, "USD", isBase: false);
        await db.SaveChangesAsync();

        var act = () => CreateExchangeRateService(db, tenantId).CreateExchangeRateAsync(new CreateExchangeRateDto
        {
            BaseCurrencyCode = "GHS",
            TargetCurrencyCode = "USD",
            Rate = 15m,
            EffectiveDate = DateTime.Parse(effectiveDate),
            RateType = rateType,
            RateSource = "Manual"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{expectedMessage}*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task ClosingRatesAcceptConfiguredQuarterAndYearEnds()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedCurrency(db, tenantId, "GHS", isBase: true);
        SeedCurrency(db, tenantId, "USD", isBase: false);
        var fiscalYear = new FiscalYear
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearName = "FY2026",
            FiscalYearCode = "FY2026",
            Year = 2026,
            FiscalYearType = "Calendar",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            TotalDays = 365,
            NumberOfPeriods = 12
        };
        db.FiscalYears.Add(fiscalYear);
        db.FiscalPeriods.Add(new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = fiscalYear.Id,
            PeriodName = "September 2026",
            PeriodCode = "2026-09",
            PeriodNumber = 9,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 9, 30),
            PeriodDays = 30
        });
        await db.SaveChangesAsync();
        var service = CreateExchangeRateService(db, tenantId);

        var quarter = await service.CreateExchangeRateAsync(new CreateExchangeRateDto
        {
            BaseCurrencyCode = "GHS",
            TargetCurrencyCode = "USD",
            Rate = 15m,
            EffectiveDate = new DateTime(2026, 9, 30),
            RateType = ExchangeRateType.QuarterEnd.ToString(),
            RateSource = "Manual"
        });
        var year = await service.CreateExchangeRateAsync(new CreateExchangeRateDto
        {
            BaseCurrencyCode = "GHS",
            TargetCurrencyCode = "USD",
            Rate = 16m,
            EffectiveDate = new DateTime(2026, 12, 31),
            RateType = ExchangeRateType.YearEnd.ToString(),
            RateSource = "Manual"
        });

        quarter.RateType.Should().Be(ExchangeRateType.QuarterEnd.ToString());
        year.RateType.Should().Be(ExchangeRateType.YearEnd.ToString());
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
        var accountingBook = new AccountingBook
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "IFRS",
            Name = "IFRS Primary",
            Purpose = "Primary",
            BookType = AccountingBookType.PrimaryFull,
            LifecycleStatus = AccountingBookLifecycleStatus.Active,
            FunctionalCurrencyCode = "GHS",
            IsDefault = true,
            IsActive = true,
            AllowsPosting = true
        };
        db.AccountingBooks.Add(accountingBook);
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
            BookClassification = accountingBook.Code,
            AccountingBookId = accountingBook.Id
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
    public async Task ThreeDecimalFunctionalPosting_PreservesPostingEventAndReplaysIdempotently()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "KWD");
        SeedCurrency(db, tenantId, "KWD", isBase: true);
        SeedFinanceSettings(db, tenantId, "KWD");
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "1000", AccountType.Asset, currencyCode: "KWD");
        var credit = SeedAccount(db, tenantId, "4000", AccountType.Revenue, currencyCode: "KWD");
        db.AccountingBooks.Add(new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "IFRS", Name = "KWD Primary",
            BookType = AccountingBookType.PrimaryFull,
            LifecycleStatus = AccountingBookLifecycleStatus.Active,
            FunctionalCurrencyCode = "KWD",
            IsDefault = true, IsActive = true, AllowsPosting = true
        });
        await db.SaveChangesAsync();

        var request = new FinancePostingRequestV2Dto
        {
            SourceModule = "FXTEST",
            SourceDocumentType = "ThreeDecimalReplay",
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentTenantId = tenantId,
            PostingAction = "Post",
            SourceDocumentReference = "FX-KWD-REPLAY",
            Description = "KWD precision replay",
            PostingDate = new DateTime(2026, 7, 4),
            JournalType = "System Generated",
            AccountingBookCode = "IFRS",
            FunctionalCurrencyCode = "KWD",
            Lines = new[]
            {
                new FinancePostingLineDto { AccountId = debit.Id, Description = "Cash", DebitAmount = 123.457m, TransactionCurrency = "KWD" },
                new FinancePostingLineDto { AccountId = credit.Id, Description = "Revenue", CreditAmount = 123.457m, TransactionCurrency = "KWD" }
            }
        };
        var service = CreatePostingEngine(db, tenantId);

        var first = await service.PostAsync(request);
        var replay = await service.PostAsync(request);

        replay.JournalEntryId.Should().Be(first.JournalEntryId);
        (await db.FinancePostingEvents.SingleAsync()).TotalDebitAmount.Should().Be(123.457m);
        (await db.FinancePostingEvents.SingleAsync()).TotalCreditAmount.Should().Be(123.457m);
        (await db.AccountTransactions.Where(item => item.JournalEntryId == first.JournalEntryId)
            .Select(item => item.DebitAmount + item.CreditAmount).ToListAsync())
            .Should().OnlyContain(amount => amount == 123.457m);
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
    [Trait("Batch", "FinanceWorkflowApprovalHardening")]
    [Trait("Category", "Workflow")]
    public async Task ExchangeRateCreationRollsBackWhenWorkflowCannotStart()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedCurrency(db, tenantId, "GHS", isBase: true);
        SeedCurrency(db, tenantId, "USD", isBase: false);
        await db.SaveChangesAsync();
        var innerUnitOfWork = new UnitOfWork(db);
        var unitOfWork = new Mock<IUnitOfWork>();
        var transactionActive = false;
        unitOfWork.SetupGet(item => item.HasActiveTransaction).Returns(() => transactionActive);
        unitOfWork.Setup(item => item.Repository<ExchangeRate>())
            .Returns(innerUnitOfWork.Repository<ExchangeRate>());
        unitOfWork.Setup(item => item.Repository<Currency>())
            .Returns(innerUnitOfWork.Repository<Currency>());
        unitOfWork.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken token) => db.SaveChangesAsync(token));
        unitOfWork.Setup(item => item.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Callback(() => transactionActive = true)
            .Returns(Task.CompletedTask);
        unitOfWork.Setup(item => item.RollbackAsync(It.IsAny<CancellationToken>()))
            .Callback(() => transactionActive = false)
            .Returns(Task.CompletedTask);
        unitOfWork.Setup(item => item.ExecuteInStrategyAsync(
                It.IsAny<Func<Task<ExchangeRateDto>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<Task<ExchangeRateDto>> operation, CancellationToken _) => operation());
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(x => x.StartApprovalWorkflowAsync("ExchangeRate", It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("Workflow entity type 'ExchangeRate' is not configured"));
        var currentUser = CreateCurrentUser(tenantId).Object;
        var service = new ExchangeRateService(
            unitOfWork.Object,
            currentUser,
            new TenantSettingsService(db, currentUser),
            Mock.Of<ILogger<ExchangeRateService>>(),
            workflowService: workflow.Object);

        await service.Invoking(item => item.CreateExchangeRateAsync(new CreateExchangeRateDto
            {
                BaseCurrencyCode = "GHS",
                TargetCurrencyCode = "USD",
                Rate = 15m,
                EffectiveDate = new DateTime(2026, 10, 5),
                RateType = ExchangeRateType.Daily.ToString(),
                QuoteSide = ExchangeRateQuoteSide.Mid.ToString(),
                RateSource = "Bank of Ghana"
            }))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not configured*");

        unitOfWork.Verify(item => item.RollbackAsync(CancellationToken.None), Times.Once,
            "a failed workflow start must roll back the pending submission");
        transactionActive.Should().BeFalse();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ExchangeRates")]
    [Trait("Category", "Workflow")]
    public async Task ExchangeRateSubmissionShouldNotChangeApprovedSchedule()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedCurrency(db, tenantId, "GHS", isBase: true);
        SeedCurrency(db, tenantId, "USD", isBase: false);
        var approved = SeedExchangeRate(
            db,
            tenantId,
            "GHS",
            "USD",
            12.5m,
            new DateTime(2024, 12, 15));
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

        var submitted = await service.CreateExchangeRateAsync(new CreateExchangeRateDto
        {
            BaseCurrencyCode = "GHS",
            TargetCurrencyCode = "USD",
            Rate = 11.2m,
            EffectiveDate = new DateTime(2026, 9, 1),
            RateType = ExchangeRateType.Daily.ToString(),
            QuoteSide = ExchangeRateQuoteSide.Mid.ToString(),
            RateSource = "Bank of Ghana"
        });

        submitted.ApprovalStatus.Should().Be(RateApprovalStatus.Pending.ToString());
        approved.EndDate.Should().BeNull();
        approved.ApprovalStatus.Should().Be(RateApprovalStatus.Approved);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task LegacyCurrencyQuickRateUpdateFailsClosedInsteadOfReportingFalseSuccess()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedCurrency(db, tenantId, "GHS", isBase: true);
        var usd = SeedCurrency(db, tenantId, "USD", isBase: false);
        await db.SaveChangesAsync();

        var service = CreateCurrencyService(db, tenantId);

        await service.Invoking(item => item.UpdateExchangeRateAsync(usd.Id, 15m))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*legacy currency quick-rate endpoint is retired*");
        (await db.ExchangeRates.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task CurrencyCreationUsesIsoMinorUnitsAndPersistsCountryMetadata()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedCurrency(db, tenantId, "GHS", isBase: true);
        await db.SaveChangesAsync();

        var result = await CreateCurrencyService(db, tenantId).CreateCurrencyAsync(new CreateCurrencyDto
        {
            CurrencyCode = "JPY",
            NumericCode = "392",
            CurrencyName = "Japanese Yen",
            CurrencySymbol = "JPY",
            DecimalPlaces = 0,
            CountryCode = "jp",
            CountryName = "Japan"
        });

        result.DecimalPlaces.Should().Be(0);
        result.RoundingPrecision.Should().Be(1m);
        result.CountryCode.Should().Be("JP");
        result.CountryName.Should().Be("Japan");
    }

    [Theory]
    [InlineData("JPY", 123.5, 124)]
    [InlineData("GHS", 123.455, 123.46)]
    [InlineData("KWD", 123.4565, 123.457)]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public void CurrencyMinorUnitPolicy_RoundsAtTheCurrencyAccountingBoundary(
        string currencyCode,
        decimal amount,
        decimal expected)
    {
        var decimalPlaces = CurrencyMinorUnitPolicy.ExpectedDecimalPlaces(currencyCode);

        decimalPlaces.Should().NotBeNull();
        CurrencyMinorUnitPolicy.Round(amount, decimalPlaces!.Value).Should().Be(expected);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public void CoreLedgerStorage_RetainsUpToFourCurrencyDecimalPlaces()
    {
        using var db = CreateContext();

        db.Model.FindEntityType(typeof(AccountTransaction))!
            .FindProperty(nameof(AccountTransaction.TransactionDebitAmount))!
            .FindAnnotation("Relational:ColumnType")!.Value.Should().Be("decimal(20,4)");
        db.Model.FindEntityType(typeof(AccountTransaction))!
            .FindProperty(nameof(AccountTransaction.DebitAmount))!
            .FindAnnotation("Relational:ColumnType")!.Value.Should().Be("decimal(20,4)");
        db.Model.FindEntityType(typeof(JournalEntry))!
            .FindProperty(nameof(JournalEntry.TotalDebitAmount))!
            .FindAnnotation("Relational:ColumnType")!.Value.Should().Be("decimal(20,4)");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task CurrencyCreationRejectsNonIsoMinorUnitsWithoutPersistingCurrency()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedCurrency(db, tenantId, "GHS", isBase: true);
        await db.SaveChangesAsync();

        var service = CreateCurrencyService(db, tenantId);
        await service.Invoking(item => item.CreateCurrencyAsync(new CreateCurrencyDto
            {
                CurrencyCode = "JPY",
                NumericCode = "392",
                CurrencyName = "Japanese Yen",
                DecimalPlaces = 2
            }))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*JPY uses 0 decimal place(s) under ISO 4217*");

        (await db.Currencies.CountAsync(item => item.TenantId == tenantId && item.CurrencyCode == "JPY"))
            .Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task CurrencyRegisterRejectsInitialRateShortcutWithoutPersistingPartialData()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedCurrency(db, tenantId, "GHS", isBase: true);
        await db.SaveChangesAsync();

        var create = () => CreateCurrencyService(db, tenantId).CreateCurrencyAsync(new CreateCurrencyDto
        {
            CurrencyCode = "USD",
            NumericCode = "840",
            CurrencyName = "US Dollar",
            CurrencySymbol = "$",
            DecimalPlaces = 2,
            CreateInitialExchangeRate = true,
            InitialExchangeRate = 15.25m,
            InitialExchangeRateDate = new DateTime(2026, 9, 30),
            InitialExchangeRateType = "Daily",
            InitialExchangeRateSource = "Manual Entry",
            InitialExchangeRateSourceReference = "UAT-2026-09-30"
        });

        await create.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Finance > Exchange Rates*");

        (await db.Currencies.CountAsync(item => item.TenantId == tenantId && item.CurrencyCode == "USD"))
            .Should().Be(0);
        (await db.ExchangeRates.CountAsync(item => item.TenantId == tenantId))
            .Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task BulkRateValidationIsAllOrNothing()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedCurrency(db, tenantId, "GHS", isBase: true);
        SeedCurrency(db, tenantId, "USD", isBase: false);
        await db.SaveChangesAsync();

        var service = CreateExchangeRateService(db, tenantId);
        var upload = () => service.BulkUploadRatesAsync(new List<CreateExchangeRateDto>
        {
            new()
            {
                BaseCurrencyCode = "GHS", TargetCurrencyCode = "USD", Rate = 15m,
                EffectiveDate = new DateTime(2026, 9, 29), RateType = "Daily", QuoteSide = "Mid"
            },
            new()
            {
                BaseCurrencyCode = "GHS", TargetCurrencyCode = "EUR", Rate = 18m,
                EffectiveDate = new DateTime(2026, 9, 29), RateType = "Daily", QuoteSide = "Mid"
            }
        });

        await upload.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No exchange rates were imported*EUR*not active*");
        (await db.ExchangeRates.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task CurrencyConversionFailsClosedWhenNoApprovedDailyMidRateExists()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedCurrency(db, tenantId, "GHS", isBase: true);
        SeedCurrency(db, tenantId, "USD", isBase: false);
        var pending = SeedExchangeRate(db, tenantId, "GHS", "USD", 15m, DateTime.UtcNow.Date.AddDays(-1));
        pending.ApprovalStatus = RateApprovalStatus.Pending;
        await db.SaveChangesAsync();

        var service = CreateCurrencyService(db, tenantId);

        await service.Invoking(item => item.ConvertAsync(100m, "USD", "GHS"))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No active approved Daily/Mid exchange-rate path exists*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXFoundation")]
    [Trait("Category", "FX")]
    public async Task CurrencyConversionUsesApprovedDirectAndFunctionalTriangulatedRates()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId, "GHS");
        SeedCurrency(db, tenantId, "GHS", isBase: true);
        SeedCurrency(db, tenantId, "USD", isBase: false);
        SeedCurrency(db, tenantId, "EUR", isBase: false);
        SeedExchangeRate(db, tenantId, "GHS", "USD", 15m, DateTime.UtcNow.Date.AddDays(-1));
        SeedExchangeRate(db, tenantId, "GHS", "EUR", 18m, DateTime.UtcNow.Date.AddDays(-1));
        await db.SaveChangesAsync();

        var service = CreateCurrencyService(db, tenantId);

        (await service.ConvertAsync(10m, "USD", "GHS")).Should().Be(150m);
        (await service.ConvertAsync(180m, "GHS", "EUR")).Should().Be(10m);
        (await service.ConvertAsync(10m, "EUR", "USD")).Should().Be(12m);
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
            .WithMessage("*immutable*");

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

        var arRequest = CreateForeignPostingRequest(tenantId, debit.Id, credit.Id, 10m, buying.InverseRate);
        arRequest.SourceModule = "AR";
        arRequest.SourceDocumentType = "CustomerPayment";
        var arResult = await CreatePostingEngine(db, tenantId).PostAsync(arRequest);

        var apRequest = CreateForeignPostingRequest(tenantId, debit.Id, credit.Id, 10m, selling.InverseRate);
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

        var request = CreateForeignPostingRequest(tenantId, debit.Id, credit.Id, 10m, mid.InverseRate);
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
        EnsureCanonicalPostingBookFixture(db, tenantId);
        return new FinancePostingEngine(db, CreateCurrentUser(tenantId).Object, Mock.Of<ILogger<FinancePostingEngine>>(), auditService);
    }

    private static void EnsureCanonicalPostingBookFixture(ApplicationDbContext db, Guid tenantId)
    {
        var book = db.AccountingBooks.Local.FirstOrDefault(item => item.TenantId == tenantId && item.Code == "IFRS")
            ?? db.AccountingBooks.FirstOrDefault(item => item.TenantId == tenantId && item.Code == "IFRS");
        if (book == null)
        {
            book = new AccountingBook
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = "IFRS", Name = "IFRS Primary",
                BookType = AccountingBookType.PrimaryFull,
                LifecycleStatus = AccountingBookLifecycleStatus.Active,
                FunctionalCurrencyCode = "GHS",
                IsDefault = true, IsActive = true, AllowsPosting = true
            };
            db.AccountingBooks.Add(book);
        }

        var accounts = db.Accounts.Local.Where(item => item.TenantId == tenantId)
            .Concat(db.Accounts.Where(item => item.TenantId == tenantId).AsEnumerable())
            .DistinctBy(item => item.Id)
            .ToArray();
        foreach (var group in accounts.GroupBy(item => item.AccountType))
        {
            var classification = db.AccountClassifications.Local.FirstOrDefault(item =>
                    item.TenantId == tenantId && item.AccountingBookId == book.Id && item.CoreAccountType == group.Key)
                ?? db.AccountClassifications.FirstOrDefault(item =>
                    item.TenantId == tenantId && item.AccountingBookId == book.Id && item.CoreAccountType == group.Key);
            if (classification == null)
            {
                classification = new AccountClassification
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, AccountingBookId = book.Id,
                    Code = $"FX_{group.Key.ToString().ToUpperInvariant()}", Name = $"FX {group.Key}",
                    CoreAccountType = group.Key, IsPostingClassification = true,
                    Status = AccountClassificationStatus.Active
                };
                db.AccountClassifications.Add(classification);
            }

            foreach (var account in group)
            {
                if (!db.AccountAccountingBooks.Local.Any(item => item.AccountId == account.Id && item.AccountingBookId == book.Id)
                    && !db.AccountAccountingBooks.Any(item => item.AccountId == account.Id && item.AccountingBookId == book.Id))
                {
                    db.AccountAccountingBooks.Add(new AccountAccountingBook
                    {
                        Id = Guid.NewGuid(), TenantId = tenantId, AccountId = account.Id,
                        AccountingBookId = book.Id, AccountClassificationId = classification.Id, IsEnabled = true
                    });
                }

                if (account.IsMultiCurrency
                    && !db.AccountCurrencyLinks.Local.Any(item => item.AccountId == account.Id && item.LinkedCurrencyCode == "USD")
                    && !db.AccountCurrencyLinks.Any(item => item.AccountId == account.Id && item.LinkedCurrencyCode == "USD"))
                {
                    db.AccountCurrencyLinks.Add(new AccountCurrencyLink
                    {
                        Id = Guid.NewGuid(), TenantId = tenantId, AccountId = account.Id,
                        LinkedCurrencyCode = "USD", TransactionRateType = "Daily",
                        RevaluationRateType = "Month-End", IsActive = true,
                        EffectiveDate = new DateTime(2026, 1, 1), CreatedAt = DateTime.UtcNow,
                        CreatedBy = "Tests"
                    });
                }
            }
        }
        foreach (var period in db.FiscalPeriods.Local.Where(item => item.TenantId == tenantId))
            FinancePostingAuthorityFixture.SeedExactBookPeriod(db, tenantId, period, book.Code);
        db.SaveChanges();
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

    private static CurrencyService CreateCurrencyService(ApplicationDbContext db, Guid tenantId)
    {
        var currentUser = CreateCurrentUser(tenantId).Object;
        var tenantSettings = new TenantSettingsService(db, currentUser);
        var exchangeRates = new ExchangeRateService(
            new UnitOfWork(db),
            currentUser,
            tenantSettings,
            Mock.Of<ILogger<ExchangeRateService>>());
        return new CurrencyService(
            new UnitOfWork(db),
            currentUser,
            tenantSettings,
            exchangeRates,
            Mock.Of<ILogger<CurrencyService>>());
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
            CurrencyDecimalPlaces = CurrencyMinorUnitPolicy.ExpectedDecimalPlaces(baseCurrency) ?? 2,
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
            DecimalPlaces = CurrencyMinorUnitPolicy.ExpectedDecimalPlaces(code) ?? 2,
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
            Rate = 1 / rate,
            InverseRate = rate,
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

    private static FinancePostingRequestV2Dto CreateSameCurrencyPostingRequest(Guid tenantId, Guid debitAccountId, Guid creditAccountId)
    {
        return new FinancePostingRequestV2Dto
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
            AccountingBookCode = "IFRS",
            FunctionalCurrencyCode = "GHS",
            Lines = new[]
            {
                new FinancePostingLineDto { AccountId = debitAccountId, Description = "Cash", DebitAmount = 100m },
                new FinancePostingLineDto { AccountId = creditAccountId, Description = "Revenue", CreditAmount = 100m }
            }
        };
    }

    private static FinancePostingRequestV2Dto CreateForeignPostingRequest(
        Guid tenantId,
        Guid debitAccountId,
        Guid creditAccountId,
        decimal transactionAmount,
        decimal rate,
        DateTime? postingDate = null)
    {
        var functionalAmount = decimal.Round(transactionAmount * rate, 2, MidpointRounding.AwayFromZero);
        return new FinancePostingRequestV2Dto
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
            AccountingBookCode = "IFRS",
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
