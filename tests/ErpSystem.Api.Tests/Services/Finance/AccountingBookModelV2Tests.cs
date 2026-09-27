using ErpSystem.Api.Services.Finance.Settings;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class AccountingBookModelV2Tests
{
    [Fact]
    public async Task Primary_CannotBeCreatedManually()
    {
        await using var db = Context();
        var tenantId = Guid.NewGuid();
        var service = new AccountingBookService(db, User(tenantId).Object);

        var action = () => service.CreateAsync(new CreateAccountingBookDto
        {
            Code = "ANOTHER_BASE", Name = "Another Base", Purpose = "Primary",
            BookType = nameof(AccountingBookType.PrimaryFull), FunctionalCurrencyCode = "GHS"
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*system-provisioned*");
    }

    [Fact]
    public async Task MultiDeltaReport_AggregatesSelectedLayersOverOneSharedBase()
    {
        await using var db = Context();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Code = "V2", Name = "V2", BaseCurrency = "GHS", Status = TenantStatus.Active });
        var baseBook = Book(tenantId, "BASE", AccountingBookType.PrimaryFull, null, AccountingBookLifecycleStatus.Active);
        var ifrs = Book(tenantId, "IFRS_ADJUSTMENTS", AccountingBookType.Delta, baseBook.Id, AccountingBookLifecycleStatus.Active);
        var audit = Book(tenantId, "AUDIT_ADJUSTMENTS", AccountingBookType.Delta, baseBook.Id, AccountingBookLifecycleStatus.Retired);
        db.AccountingBooks.AddRange(baseBook, ifrs, audit);
        var account = new Account
        {
            TenantId = tenantId, AccountCode = "1000", AccountNumber = "1000", AccountName = "Cash",
            AccountType = AccountType.Asset, CurrencyCode = "GHS", Status = AccountStatus.Active
        };
        db.Accounts.Add(account);
        foreach (var book in new[] { baseBook, ifrs, audit })
        {
            var classification = new AccountClassification
            {
                TenantId = tenantId, AccountingBookId = book.Id, Code = "ASSET", Name = "Assets",
                CoreAccountType = AccountType.Asset, Status = AccountClassificationStatus.Active,
                IsPostingClassification = true
            };
            db.AccountClassifications.Add(classification);
            db.AccountAccountingBooks.Add(new AccountAccountingBook
            {
                TenantId = tenantId, AccountingBookId = book.Id, AccountId = account.Id,
                AccountClassificationId = classification.Id, IsEnabled = true
            });
        }
        db.AccountTransactions.AddRange(
            Transaction(tenantId, baseBook.Id, account.Id, 100m, 0m),
            Transaction(tenantId, ifrs.Id, account.Id, 25m, 0m),
            Transaction(tenantId, audit.Id, account.Id, 0m, 10m));
        await db.SaveChangesAsync();

        var report = await new AccountingBookService(db, User(tenantId).Object)
            .GetDeltaCombinedReportAsync(new[] { ifrs.Id, audit.Id }, new DateTime(2026, 9, 21));

        report.BaseTotal.Should().Be(100m);
        report.DeltaTotal.Should().Be(15m);
        report.CombinedTotal.Should().Be(115m);
        report.DeltaAccountingBookCodes.Should().Equal("IFRS_ADJUSTMENTS", "AUDIT_ADJUSTMENTS");
        report.Lines.Single().CombinedSignedBalance.Should().Be(115m);
    }

    [Fact]
    public async Task DeltaLedger_LabelsLiveBaseAndAdjustmentEntriesWithoutDuplicatingBase()
    {
        await using var db = Context();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Code = "V2", Name = "V2", BaseCurrency = "GHS", Status = TenantStatus.Active });
        var baseBook = Book(tenantId, "BASE", AccountingBookType.PrimaryFull, null, AccountingBookLifecycleStatus.Active);
        var delta = Book(tenantId, "IFRS_ADJUSTMENTS", AccountingBookType.Delta, baseBook.Id, AccountingBookLifecycleStatus.Retired);
        db.AccountingBooks.AddRange(baseBook, delta);
        db.JournalEntries.AddRange(
            Journal(tenantId, baseBook.Id, "BASE-001", new DateTime(2026, 9, 20), "Posted"),
            Journal(tenantId, delta.Id, "DELTA-001", new DateTime(2026, 9, 21), "Posted"),
            Journal(tenantId, delta.Id, "DELTA-DRAFT", new DateTime(2026, 9, 21), "Draft"));
        await db.SaveChangesAsync();

        var inquiry = await new AccountingBookService(db, User(tenantId).Object)
            .GetDeltaLedgerAsync(delta.Id, new DateTime(2026, 9, 1), new DateTime(2026, 9, 30));

        inquiry.Entries.Should().HaveCount(2);
        inquiry.Entries.Select(item => item.Layer).Should().Equal("Adjustment", "Inherited");
        inquiry.Entries.Single(item => item.Layer == "Inherited").SourceBookCode.Should().Be("BASE");
        inquiry.Entries.Single(item => item.Layer == "Adjustment").SourceBookCode.Should().Be("IFRS_ADJUSTMENTS");
    }

    [Fact]
    public async Task ParallelStructure_AutoProvisionsProtectedReserveAndRoundingAccounts()
    {
        await using var db = Context();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Code = "V2", Name = "V2", BaseCurrency = "GHS", Status = TenantStatus.Active });
        var primary = Book(tenantId, "BASE", AccountingBookType.PrimaryFull, null, AccountingBookLifecycleStatus.Active);
        var parallel = Book(tenantId, "USD_PARALLEL", AccountingBookType.ParallelFull, primary.Id, AccountingBookLifecycleStatus.Configuring);
        parallel.FunctionalCurrencyCode = "USD";
        parallel.ReplicationStartDate = new DateTime(2026, 1, 1);
        parallel.ParallelOpeningMode = ParallelBookOpeningMode.ZeroOpening;
        db.AccountingBooks.AddRange(primary, parallel);
        var asset = Account(tenantId, "1000", AccountType.Asset);
        var equity = Account(tenantId, "3000", AccountType.Equity);
        var expense = Account(tenantId, "6000", AccountType.Expense);
        db.Accounts.AddRange(asset, equity, expense);
        AddPrimaryMapping(db, tenantId, primary, asset, "ASSET");
        AddPrimaryMapping(db, tenantId, primary, equity, "EQUITY");
        AddPrimaryMapping(db, tenantId, primary, expense, "OTHER_EXPENSE");
        db.FiscalPeriods.Add(new FiscalPeriod
        {
            TenantId = tenantId, FiscalYearId = Guid.NewGuid(), PeriodName = "December 2025",
            PeriodCode = "2025-12", PeriodNumber = 12, PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2025, 12, 1), EndDate = new DateTime(2025, 12, 31),
            PeriodDays = 31, PeriodStatus = "Closed", IsClosed = true
        });
        await db.SaveChangesAsync();

        var service = new AccountingBookInitializationService(
            db, User(tenantId).Object, Mock.Of<IWorkflowService>(), Audit().Object,
            new BookBalanceReadModelService(db));
        await service.EnsureDeltaStructureAsync(parallel.Id);

        parallel.CurrencyTranslationReserveAccountId.Should().NotBeNull();
        parallel.CurrencyRoundingAccountId.Should().NotBeNull();
        var protectedAccounts = await db.Accounts.Where(item =>
            item.Id == parallel.CurrencyTranslationReserveAccountId
            || item.Id == parallel.CurrencyRoundingAccountId).ToListAsync();
        protectedAccounts.Should().HaveCount(2).And.OnlyContain(item => item.IsSystemAccount && !item.AllowDirectPosting);
        (await db.AccountAccountingBooks.CountAsync(item => item.AccountingBookId == parallel.Id)).Should().Be(5);

        var preparation = await service.PrepareAsync(parallel.Id, nameof(AccountingBookInitializationMode.IndependentOpeningBalances),
            new DateTime(2025, 12, 31), null);
        preparation.FunctionalCurrencyCode.Should().Be("USD");
        preparation.Accounts.Should().HaveCount(5).And.OnlyContain(item => item.AuthoritativeSignedBalance == 0m);
    }

    [Fact]
    public async Task ParallelGovernedOpening_TranslatesAndSnapshotsApprovedRateEvidence()
    {
        await using var db = Context();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Code = "V2FX", Name = "V2 FX", BaseCurrency = "GHS", Status = TenantStatus.Active });
        var primary = Book(tenantId, "BASE", AccountingBookType.PrimaryFull, null, AccountingBookLifecycleStatus.Active);
        var parallel = Book(tenantId, "USD_PARALLEL", AccountingBookType.ParallelFull, primary.Id, AccountingBookLifecycleStatus.Configuring);
        parallel.FunctionalCurrencyCode = "USD";
        parallel.ReplicationStartDate = new DateTime(2026, 1, 1);
        parallel.ParallelOpeningMode = ParallelBookOpeningMode.GovernedOpeningConversion;
        parallel.ParallelTranslationMethod = ParallelBookTranslationMethod.SingleApprovedRate;
        db.AccountingBooks.AddRange(primary, parallel);
        var asset = Account(tenantId, "1000", AccountType.Asset);
        var equity = Account(tenantId, "3000", AccountType.Equity);
        var expense = Account(tenantId, "6000", AccountType.Expense);
        db.Accounts.AddRange(asset, equity, expense);
        AddPrimaryMapping(db, tenantId, primary, asset, "ASSET");
        AddPrimaryMapping(db, tenantId, primary, equity, "EQUITY");
        AddPrimaryMapping(db, tenantId, primary, expense, "OTHER_EXPENSE");
        var period = new FiscalPeriod
        {
            TenantId = tenantId, FiscalYearId = Guid.NewGuid(), PeriodName = "December 2025",
            PeriodCode = "2025-12", PeriodNumber = 12, PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2025, 12, 1), EndDate = new DateTime(2025, 12, 31),
            PeriodDays = 31, PeriodStatus = "Closed", IsClosed = true
        };
        db.FiscalPeriods.Add(period);
        db.AccountBalances.AddRange(
            new AccountBalance { TenantId = tenantId, AccountingBookId = primary.Id, AccountId = asset.Id,
                FiscalPeriodId = period.Id, ClosingBalance = 1000m },
            new AccountBalance { TenantId = tenantId, AccountingBookId = primary.Id, AccountId = equity.Id,
                FiscalPeriodId = period.Id, ClosingBalance = -1000m });
        var rate = new ExchangeRate
        {
            TenantId = tenantId, BaseCurrencyCode = "GHS", TargetCurrencyCode = "USD",
            Rate = 0.08m, InverseRate = 12.5m, EffectiveDate = new DateTime(2025, 12, 31),
            RateType = ExchangeRateType.YearEnd, QuoteSide = ExchangeRateQuoteSide.Mid,
            RateSource = "Bank of Ghana", ApprovalStatus = RateApprovalStatus.Approved,
            CreatedByUserId = Guid.NewGuid(), CreatedDate = DateTime.UtcNow
        };
        db.ExchangeRates.Add(rate);
        await db.SaveChangesAsync();

        var service = new AccountingBookInitializationService(
            db, User(tenantId).Object, Mock.Of<IWorkflowService>(), Audit().Object,
            new BookBalanceReadModelService(db));
        await service.EnsureDeltaStructureAsync(parallel.Id);
        var preparation = await service.PrepareAsync(parallel.Id,
            nameof(AccountingBookInitializationMode.BaseBookCopyAtCutoff), period.EndDate, primary.Id);

        preparation.TranslationMethod.Should().Be(nameof(ParallelBookTranslationMethod.SingleApprovedRate));
        preparation.FunctionalCurrencyCode.Should().Be("USD");
        preparation.Accounts.Single(item => item.AccountId == asset.Id).Should().Match<AccountingBookInitializationPreparationLineDto>(item =>
            item.SourceSignedBalance == 1000m && item.AuthoritativeSignedBalance == 80m
            && item.TranslationExchangeRateId == rate.Id && item.TranslationRate == 0.08m);
        preparation.Accounts.Single(item => item.AccountId == equity.Id).AuthoritativeSignedBalance.Should().Be(-80m);

        var configured = await service.ConfigureAsync(parallel.Id, new ConfigureAccountingBookInitializationDto
        {
            Mode = nameof(AccountingBookInitializationMode.BaseBookCopyAtCutoff),
            CutoffDate = period.EndDate, CutoffFiscalPeriodId = period.Id,
            CutoffFiscalPeriodCode = period.PeriodCode, SourceAccountingBookId = primary.Id,
            SourceAccountingBookCode = primary.Code, IdempotencyKey = "usd-opening-2025",
            Reason = "Establish translated opening balances",
            Lines = preparation.Accounts.Select(item => new AccountingBookInitializationLineDto
            {
                AccountId = item.AccountId, AccountNumber = item.AccountNumber, AccountName = item.AccountName,
                CurrencyCode = preparation.FunctionalCurrencyCode,
                OpeningDebit = item.AuthoritativeSignedBalance > 0m ? item.AuthoritativeSignedBalance : 0m,
                OpeningCredit = item.AuthoritativeSignedBalance < 0m ? Math.Abs(item.AuthoritativeSignedBalance) : 0m,
                BaseBookSignedBalance = item.SourceSignedBalance,
                TranslationExchangeRateId = item.TranslationExchangeRateId,
                TranslationRate = item.TranslationRate, TranslationRateDate = item.TranslationRateDate,
                TranslationRateType = item.TranslationRateType, TranslationRateSource = item.TranslationRateSource
            }).ToArray()
        });
        var initialization = await db.AccountingBookInitializations.Include(item => item.Lines)
            .SingleAsync(item => item.Id == configured.Id);
        initialization.InitializationStatus = AccountingBookInitializationStatus.Approved;
        initialization.ApprovedByUserId = Guid.NewGuid();
        initialization.ApprovedAtUtc = DateTime.UtcNow;
        parallel.LifecycleStatus = AccountingBookLifecycleStatus.Initializing;
        await db.SaveChangesAsync();

        (await service.ApplyGovernedParallelOpeningAsync(parallel.Id)).Should().Be(1);
        (await service.ApplyGovernedParallelOpeningAsync(parallel.Id)).Should().Be(0);
        var openingJournal = await db.JournalEntries.Include(item => item.Transactions).SingleAsync(item =>
            item.AccountingBookId == parallel.Id && item.SourceDocumentId == initialization.Id);
        openingJournal.TotalDebitAmount.Should().Be(80m);
        openingJournal.TotalCreditAmount.Should().Be(80m);
        openingJournal.Transactions.Should().OnlyContain(item => item.FunctionalCurrencyCode == "USD");
        (await db.AccountBalances.SingleAsync(item => item.AccountingBookId == parallel.Id
            && item.AccountId == asset.Id)).ClosingBalance.Should().Be(80m);
        (await db.AccountBalances.SingleAsync(item => item.AccountingBookId == parallel.Id
            && item.AccountId == equity.Id)).ClosingBalance.Should().Be(-80m);
        (await db.FinancePostingEvents.CountAsync(item => item.AccountingBookId == parallel.Id
            && item.PostingAction == "GovernedOpeningConversion")).Should().Be(1);
    }

    [Fact]
    public async Task ParallelOpening_RejectsAnyGapBetweenCutoffAndLiveReplication()
    {
        await using var db = Context();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Code = "V2GAP", Name = "V2 Gap", BaseCurrency = "GHS", Status = TenantStatus.Active });
        var primary = Book(tenantId, "BASE", AccountingBookType.PrimaryFull, null, AccountingBookLifecycleStatus.Active);
        var parallel = Book(tenantId, "USD_PARALLEL", AccountingBookType.ParallelFull, primary.Id, AccountingBookLifecycleStatus.Configuring);
        parallel.FunctionalCurrencyCode = "USD";
        parallel.ReplicationStartDate = new DateTime(2026, 1, 2);
        parallel.ParallelOpeningMode = ParallelBookOpeningMode.ZeroOpening;
        db.AccountingBooks.AddRange(primary, parallel);
        var asset = Account(tenantId, "1000", AccountType.Asset);
        db.Accounts.Add(asset);
        AddPrimaryMapping(db, tenantId, primary, asset, "ASSET");
        db.FiscalPeriods.Add(new FiscalPeriod
        {
            TenantId = tenantId, FiscalYearId = Guid.NewGuid(), PeriodName = "December 2025",
            PeriodCode = "2025-12", PeriodNumber = 12, PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2025, 12, 1), EndDate = new DateTime(2025, 12, 31),
            PeriodDays = 31, PeriodStatus = "Closed", IsClosed = true
        });
        await db.SaveChangesAsync();
        var service = new AccountingBookInitializationService(
            db, User(tenantId).Object, Mock.Of<IWorkflowService>(), Audit().Object);

        var action = () => service.PrepareAsync(parallel.Id,
            nameof(AccountingBookInitializationMode.IndependentOpeningBalances),
            new DateTime(2025, 12, 31), null);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("PARALLEL_OPENING_CUTOFF_GAP:*");
    }

    [Fact]
    public async Task ParallelClassificationDrivenOpening_UsesClosingHistoricalAndCtaTreatments()
    {
        await using var db = Context();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Code = "V2CLASSFX", Name = "V2 Classification FX", BaseCurrency = "GHS", Status = TenantStatus.Active });
        var primary = Book(tenantId, "BASE", AccountingBookType.PrimaryFull, null, AccountingBookLifecycleStatus.Active);
        var parallel = Book(tenantId, "USD_PARALLEL", AccountingBookType.ParallelFull, primary.Id, AccountingBookLifecycleStatus.Configuring);
        parallel.FunctionalCurrencyCode = "USD";
        parallel.ReplicationStartDate = new DateTime(2026, 1, 1);
        parallel.ParallelOpeningMode = ParallelBookOpeningMode.GovernedOpeningConversion;
        parallel.ParallelTranslationMethod = ParallelBookTranslationMethod.ClassificationDriven;
        db.AccountingBooks.AddRange(primary, parallel);
        var asset = Account(tenantId, "1000", AccountType.Asset);
        var equity = Account(tenantId, "3000", AccountType.Equity);
        var expense = Account(tenantId, "6000", AccountType.Expense);
        db.Accounts.AddRange(asset, equity, expense);
        AddPrimaryMapping(db, tenantId, primary, asset, "ASSET");
        AddPrimaryMapping(db, tenantId, primary, equity, "EQUITY");
        AddPrimaryMapping(db, tenantId, primary, expense, "OTHER_EXPENSE");
        var period = new FiscalPeriod
        {
            TenantId = tenantId, FiscalYearId = Guid.NewGuid(), PeriodName = "December 2025",
            PeriodCode = "2025-12", PeriodNumber = 12, PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2025, 12, 1), EndDate = new DateTime(2025, 12, 31),
            PeriodDays = 31, PeriodStatus = "Closed", IsClosed = true
        };
        db.FiscalPeriods.Add(period);
        db.AccountBalances.AddRange(
            new AccountBalance { TenantId = tenantId, AccountingBookId = primary.Id, AccountId = asset.Id,
                FiscalPeriodId = period.Id, ClosingBalance = 1000m },
            new AccountBalance { TenantId = tenantId, AccountingBookId = primary.Id, AccountId = equity.Id,
                FiscalPeriodId = period.Id, ClosingBalance = -1000m });
        var equityOrigin = new DateTime(2024, 1, 1);
        db.AccountTransactions.Add(new AccountTransaction
        {
            TenantId = tenantId, AccountingBookId = primary.Id, AccountId = equity.Id,
            JournalEntryId = Guid.NewGuid(), FiscalPeriodId = period.Id, TransactionDate = equityOrigin,
            CreditAmount = 1000m, FunctionalCurrencyCode = "GHS", PostingStatus = "Posted", LineNumber = 1
        });
        var historicalRate = new ExchangeRate
        {
            TenantId = tenantId, BaseCurrencyCode = "GHS", TargetCurrencyCode = "USD",
            Rate = 0.05m, InverseRate = 20m, EffectiveDate = equityOrigin,
            RateType = ExchangeRateType.Fixed, QuoteSide = ExchangeRateQuoteSide.Mid,
            RateSource = "Historical capital rate", ApprovalStatus = RateApprovalStatus.Approved,
            CreatedByUserId = Guid.NewGuid(), CreatedDate = DateTime.UtcNow
        };
        var closingRate = new ExchangeRate
        {
            TenantId = tenantId, BaseCurrencyCode = "GHS", TargetCurrencyCode = "USD",
            Rate = 0.08m, InverseRate = 12.5m, EffectiveDate = period.EndDate,
            RateType = ExchangeRateType.YearEnd, QuoteSide = ExchangeRateQuoteSide.Mid,
            RateSource = "Closing rate", ApprovalStatus = RateApprovalStatus.Approved,
            CreatedByUserId = Guid.NewGuid(), CreatedDate = DateTime.UtcNow
        };
        db.ExchangeRates.AddRange(historicalRate, closingRate);
        await db.SaveChangesAsync();

        var service = new AccountingBookInitializationService(
            db, User(tenantId).Object, Mock.Of<IWorkflowService>(), Audit().Object);
        await service.EnsureDeltaStructureAsync(parallel.Id);
        var preparation = await service.PrepareAsync(parallel.Id,
            nameof(AccountingBookInitializationMode.BaseBookCopyAtCutoff), period.EndDate, primary.Id);

        preparation.TranslationMethod.Should().Be(nameof(ParallelBookTranslationMethod.ClassificationDriven));
        preparation.Accounts.Single(item => item.AccountId == asset.Id).Should().Match<AccountingBookInitializationPreparationLineDto>(item =>
            item.AuthoritativeSignedBalance == 80m && item.TranslationExchangeRateId == closingRate.Id
            && item.TranslationRateType == nameof(ExchangeRateType.YearEnd));
        preparation.Accounts.Single(item => item.AccountId == equity.Id).Should().Match<AccountingBookInitializationPreparationLineDto>(item =>
            item.AuthoritativeSignedBalance == -50m && item.TranslationExchangeRateId == historicalRate.Id
            && item.TranslationRateType == nameof(ExchangeRateType.Fixed));
        preparation.Accounts.Single(item => item.AccountId == parallel.CurrencyTranslationReserveAccountId).Should()
            .Match<AccountingBookInitializationPreparationLineDto>(item =>
                item.AuthoritativeSignedBalance == -30m && item.TranslationExchangeRateId == null);
        preparation.Accounts.Sum(item => item.AuthoritativeSignedBalance).Should().Be(0m);
    }

    [Fact]
    public async Task ParallelHistoricalReplay_CreatesIdempotentRateSnapshottedReplica()
    {
        await using var db = Context();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Code = "V2REPLAY", Name = "V2 Replay", BaseCurrency = "GHS", Status = TenantStatus.Active });
        var primary = Book(tenantId, "BASE", AccountingBookType.PrimaryFull, null, AccountingBookLifecycleStatus.Active);
        var parallel = Book(tenantId, "USD_PARALLEL", AccountingBookType.ParallelFull, primary.Id, AccountingBookLifecycleStatus.Configuring);
        parallel.FunctionalCurrencyCode = "USD";
        parallel.ReplicationStartDate = new DateTime(2026, 1, 1);
        parallel.ParallelOpeningMode = ParallelBookOpeningMode.HistoricalReplay;
        db.AccountingBooks.AddRange(primary, parallel);
        var cash = Account(tenantId, "1000", AccountType.Asset);
        var equity = Account(tenantId, "3000", AccountType.Equity);
        var expense = Account(tenantId, "6000", AccountType.Expense);
        db.Accounts.AddRange(cash, equity, expense);
        AddPrimaryMapping(db, tenantId, primary, cash, "ASSET");
        AddPrimaryMapping(db, tenantId, primary, equity, "EQUITY");
        AddPrimaryMapping(db, tenantId, primary, expense, "OTHER_EXPENSE");
        var period = new FiscalPeriod
        {
            TenantId = tenantId, FiscalYearId = Guid.NewGuid(), PeriodName = "December 2025",
            PeriodCode = "2025-12", PeriodNumber = 12, PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2025, 12, 1), EndDate = new DateTime(2025, 12, 31),
            PeriodDays = 31, PeriodStatus = "Closed", IsClosed = true
        };
        db.FiscalPeriods.Add(period);
        var source = Journal(tenantId, primary.Id, "BASE-REPLAY-001", new DateTime(2025, 12, 20), "Posted");
        source.FiscalPeriodId = period.Id;
        source.SourceModule = "GL";
        source.SourceDocumentType = "ManualJournal";
        source.SourceDocumentId = Guid.NewGuid();
        source.Transactions = new List<AccountTransaction>
        {
            new() { TenantId = tenantId, JournalEntryId = source.Id, AccountId = cash.Id,
                AccountingBookId = primary.Id, BookClassification = primary.Code, FiscalPeriodId = period.Id,
                TransactionDate = source.EntryDate, DebitAmount = 125m, FunctionalCurrencyCode = "GHS",
                PostingStatus = "Posted", LineNumber = 1 },
            new() { TenantId = tenantId, JournalEntryId = source.Id, AccountId = equity.Id,
                AccountingBookId = primary.Id, BookClassification = primary.Code, FiscalPeriodId = period.Id,
                TransactionDate = source.EntryDate, CreditAmount = 125m, FunctionalCurrencyCode = "GHS",
                PostingStatus = "Posted", LineNumber = 2 }
        };
        db.JournalEntries.Add(source);
        var rate = new ExchangeRate
        {
            TenantId = tenantId, BaseCurrencyCode = "GHS", TargetCurrencyCode = "USD",
            Rate = 0.08m, InverseRate = 12.5m, EffectiveDate = source.EntryDate,
            RateType = ExchangeRateType.Daily, QuoteSide = ExchangeRateQuoteSide.Mid,
            RateSource = "Bank of Ghana", ApprovalStatus = RateApprovalStatus.Approved,
            CreatedByUserId = Guid.NewGuid(), CreatedDate = DateTime.UtcNow
        };
        db.ExchangeRates.Add(rate);
        await db.SaveChangesAsync();

        var user = User(tenantId);
        var service = new AccountingBookInitializationService(db, user.Object,
            Mock.Of<IWorkflowService>(), Audit().Object, new BookBalanceReadModelService(db));
        await service.EnsureDeltaStructureAsync(parallel.Id);
        parallel.LifecycleStatus = AccountingBookLifecycleStatus.Initializing;
        db.AccountingBookInitializations.Add(new AccountingBookInitialization
        {
            TenantId = tenantId, AccountingBookId = parallel.Id, Version = 1,
            Mode = AccountingBookInitializationMode.IndependentOpeningBalances,
            InitializationStatus = AccountingBookInitializationStatus.Approved,
            CutoffDate = period.EndDate, CutoffFiscalPeriodId = period.Id,
            SourceAccountingBookId = primary.Id, IdempotencyKey = "replay-init",
            Reason = "Historical replay", EvidenceFingerprint = new string('A', 64),
            ReconciliationFingerprint = new string('B', 64), PreparedByUserId = Guid.NewGuid(),
            PreparedAtUtc = DateTime.UtcNow, ApprovedByUserId = Guid.NewGuid(), ApprovedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        (await service.ReplayHistoricalParallelTransactionsAsync(parallel.Id)).Should().Be(1);
        (await service.ReplayHistoricalParallelTransactionsAsync(parallel.Id)).Should().Be(0);

        var replica = await db.JournalEntries.Include(item => item.Transactions).SingleAsync(item =>
            item.AccountingBookId == parallel.Id && item.ReplicatedFromJournalEntryId == source.Id);
        replica.TotalDebitAmount.Should().Be(10m);
        replica.TotalCreditAmount.Should().Be(10m);
        replica.ReplicationExchangeRateId.Should().Be(rate.Id);
        replica.ReplicationExchangeRate.Should().Be(0.08m);
        replica.Transactions.Should().OnlyContain(item => item.ExchangeRateId == rate.Id && item.ExchangeRate == 0.08m);
        (await db.FinancePostingEvents.CountAsync(item => item.AccountingBookId == parallel.Id
            && item.PostingAction == "HistoricalReplay")).Should().Be(1);
    }

    private static AccountingBook Book(Guid tenantId, string code, AccountingBookType type, Guid? baseId,
        AccountingBookLifecycleStatus status) => new()
    {
        TenantId = tenantId, Code = code, Name = code, Purpose = code, BookType = type,
        BaseAccountingBookId = baseId, LifecycleStatus = status,
        FunctionalCurrencyCode = type == AccountingBookType.Delta ? null : "GHS",
        IsDefault = type == AccountingBookType.PrimaryFull,
        IsActive = status == AccountingBookLifecycleStatus.Active,
        AllowsPosting = status == AccountingBookLifecycleStatus.Active
    };

    private static Account Account(Guid tenantId, string code, AccountType type) => new()
    {
        TenantId = tenantId, AccountCode = code, AccountNumber = code, AccountName = code,
        AccountType = type, CurrencyCode = "GHS", Status = AccountStatus.Active
    };

    private static void AddPrimaryMapping(ApplicationDbContext db, Guid tenantId, AccountingBook book,
        Account account, string code)
    {
        var classification = new AccountClassification
        {
            TenantId = tenantId, AccountingBookId = book.Id, Code = code, Name = code,
            CoreAccountType = account.AccountType, Status = AccountClassificationStatus.Active,
            IsPostingClassification = true
        };
        db.AccountClassifications.Add(classification);
        db.AccountAccountingBooks.Add(new AccountAccountingBook
        {
            TenantId = tenantId, AccountingBookId = book.Id, AccountId = account.Id,
            AccountClassificationId = classification.Id, IsEnabled = true
        });
    }

    private static AccountTransaction Transaction(Guid tenantId, Guid bookId, Guid accountId,
        decimal debit, decimal credit) => new()
    {
        TenantId = tenantId, AccountingBookId = bookId, AccountId = accountId,
        JournalEntryId = Guid.NewGuid(), FiscalPeriodId = Guid.NewGuid(), TransactionDate = new DateTime(2026, 9, 20),
        DebitAmount = debit, CreditAmount = credit, FunctionalCurrencyCode = "GHS", PostingStatus = "Posted"
    };

    private static JournalEntry Journal(Guid tenantId, Guid bookId, string number, DateTime date, string status) => new()
    {
        TenantId = tenantId,
        AccountingBookId = bookId,
        BookClassification = number.StartsWith("BASE", StringComparison.Ordinal) ? "BASE" : "IFRS_ADJUSTMENTS",
        JournalEntryNumber = number,
        JournalType = "Manual",
        EntryDate = date,
        PostingDate = date.AddHours(12),
        Description = number,
        PostingStatus = status,
        TotalDebitAmount = 100m,
        TotalCreditAmount = 100m,
        IsBalanced = true
    };

    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase($"accounting-book-v2-{Guid.NewGuid():N}").Options);

    private static Mock<ICurrentUserService> User(Guid tenantId)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.SetupGet(item => item.TenantId).Returns(tenantId);
        mock.SetupGet(item => item.UserId).Returns(Guid.NewGuid().ToString());
        mock.SetupGet(item => item.UserName).Returns("accounting.book.v2.test");
        return mock;
    }

    private static Mock<IFinanceAuditService> Audit()
    {
        var mock = new Mock<IFinanceAuditService>();
        mock.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditLog());
        return mock;
    }
}
