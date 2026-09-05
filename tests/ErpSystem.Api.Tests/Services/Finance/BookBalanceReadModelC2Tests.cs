using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class BookBalanceReadModelC2Tests
{
    [Theory]
    [InlineData(AccountType.Asset, 125, 0, 125)]
    [InlineData(AccountType.Liability, 0, 125, -125)]
    [InlineData(AccountType.Equity, 125, 25, 100)]
    [InlineData(AccountType.Revenue, 25, 125, -100)]
    [InlineData(AccountType.Expense, 80, 125, -45)]
    public async Task SignedCoordinate_IsAlwaysDebitMinusCredit_ForEveryCoreType(
        AccountType accountType, decimal debit, decimal credit, decimal expected)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Debit.AccountType = accountType;

        await fixture.Service.ApplyPostingAsync(fixture.TenantId, fixture.Primary.Id, fixture.Primary.Code,
            fixture.Period.Id, "GHS", fixture.Lines(debit, credit, "USD", debit, credit),
            true, DateTime.UtcNow, Guid.NewGuid());
        await fixture.Db.SaveChangesAsync();

        (await fixture.Db.AccountBalances.SingleAsync()).ClosingBalance.Should().Be(expected);
        (await fixture.Db.AccountCurrencyExposures.SingleAsync()).SignedForeignBalance.Should().Be(expected);
    }

    [Fact]
    public async Task Posting_UsesSignedExactBookGrains_AndDoesNotMutateCurrencyLinkConfiguration()
    {
        await using var fixture = await Fixture.CreateAsync();
        var lines = fixture.Lines(100m, 0m, "USD", 8m, 0m);

        await fixture.Service.ApplyPostingAsync(fixture.TenantId, fixture.Primary.Id, fixture.Primary.Code,
            fixture.Period.Id, "GHS", lines, true, DateTime.UtcNow, Guid.NewGuid());
        await fixture.Db.SaveChangesAsync();

        var balance = await fixture.Db.AccountBalances.SingleAsync();
        balance.PeriodDebits.Should().Be(100m);
        balance.PeriodCredits.Should().Be(0m);
        balance.ClosingBalance.Should().Be(100m);
        (await fixture.Db.AccountCurrencyExposures.SingleAsync()).Should().Match<AccountCurrencyExposure>(x =>
            x.SignedForeignBalance == 8m && x.SignedFunctionalBalance == 100m && x.AccountingBookId == fixture.Primary.Id);
        (await fixture.Db.Accounts.SingleAsync(x => x.Id == fixture.Debit.Id)).Balance.Should().Be(100m);
        (await fixture.Db.AccountCurrencyLinks.SingleAsync()).ForeignCurrencyBalance.Should().Be(0m);
    }

    [Fact]
    public async Task NonDefaultBook_UpdatesOnlyBookProjection_NotLegacyAccountBalance()
    {
        await using var fixture = await Fixture.CreateAsync();
        var lines = fixture.Lines(0m, 75m);
        foreach (var line in lines) { line.AccountingBookId = fixture.Parallel.Id; line.BookClassification = fixture.Parallel.Code; }
        await fixture.Service.ApplyPostingAsync(fixture.TenantId, fixture.Parallel.Id, fixture.Parallel.Code,
            fixture.Period.Id, "GHS", lines, false, DateTime.UtcNow, null);
        await fixture.Db.SaveChangesAsync();

        (await fixture.Db.AccountBalances.SingleAsync()).ClosingBalance.Should().Be(-75m);
        (await fixture.Db.Accounts.SingleAsync(x => x.Id == fixture.Debit.Id)).Balance.Should().Be(0m);
    }

    [Fact]
    public async Task AlternativeBooks_RemainIsolated_AndPrimaryCompatibilityNeverSumsThem()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.ApplyPostingAsync(fixture.TenantId, fixture.Primary.Id, fixture.Primary.Code,
            fixture.Period.Id, "GHS", fixture.Lines(1000m, 0m), true, DateTime.UtcNow, null);
        var local = fixture.Lines(1000m, 0m);
        foreach (var line in local) { line.AccountingBookId = fixture.Parallel.Id; line.BookClassification = fixture.Parallel.Code; }
        await fixture.Service.ApplyPostingAsync(fixture.TenantId, fixture.Parallel.Id, fixture.Parallel.Code,
            fixture.Period.Id, "GHS", local, false, DateTime.UtcNow, null);
        await fixture.Db.SaveChangesAsync();

        var rows = await fixture.Db.AccountBalances.OrderBy(item => item.BookClassification).ToListAsync();
        rows.Should().HaveCount(2).And.OnlyContain(item => item.ClosingBalance == 1000m);
        fixture.Debit.Balance.Should().Be(1000m);
    }

    [Fact]
    public async Task ExactBookInquiry_IsTenantAndMappingScoped()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.ApplyPostingAsync(fixture.TenantId, fixture.Primary.Id, fixture.Primary.Code,
            fixture.Period.Id, "GHS", fixture.Lines(25m, 0m), true, DateTime.UtcNow, null);
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.GetAsync(fixture.TenantId, fixture.Debit.Id, "IFRS");
        result.Balances.Single().ClosingSignedBalance.Should().Be(25m);
        await fixture.Service.Invoking(service => service.GetAsync(Guid.NewGuid(), fixture.Debit.Id, "IFRS"))
            .Should().ThrowAsync<KeyNotFoundException>();
        await fixture.Service.Invoking(service => service.GetAsync(fixture.TenantId, fixture.Debit.Id, "LOCAL_STATUTORY"))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task Reconciliation_DryRunDoesNotMutate_ApprovedRebuildIsIdempotent()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.AddPostedEvidence(fixture.Lines(40m, 0m), fixture.Primary);
        fixture.Debit.Balance = 999m;
        fixture.Db.AccountBalances.Add(new AccountBalance { TenantId = fixture.TenantId, AccountId = fixture.Debit.Id,
            AccountingBookId = fixture.Primary.Id, BookClassification = "IFRS", FiscalPeriodId = fixture.Period.Id,
            Currency = "GHS", ClosingBalance = 999m });
        await fixture.Db.SaveChangesAsync();

        var dry = await fixture.Service.ReconcileAsync(fixture.TenantId,
            new("IFRS", false, null, null, null), Guid.NewGuid());
        dry.BalanceDriftCount.Should().BeGreaterThan(0);
        dry.PrimaryCompatibilityDriftCount.Should().Be(1);
        (await fixture.Db.AccountBalances.SingleAsync()).ClosingBalance.Should().Be(999m);

        var maker = Guid.NewGuid(); var checker = Guid.NewGuid();
        var request = new BookBalanceReconciliationRequestDto("IFRS", true, "Reviewed C2 drift", "rebuild-1", checker);
        var applied = await fixture.Service.ReconcileAsync(fixture.TenantId, request, maker);
        (await fixture.Db.AccountBalances.SingleAsync()).ClosingBalance.Should().Be(40m);
        fixture.Debit.Balance.Should().Be(40m);
        var repeated = await fixture.Service.ReconcileAsync(fixture.TenantId, request, maker);
        repeated.RebuildRunId.Should().Be(applied.RebuildRunId);
        (await fixture.Db.FinanceBalanceRebuildRuns.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData(10, -10, 0, true, false)]
    [InlineData(-5, 10, 5, false, false)]
    [InlineData(5, -10, -5, false, true)]
    public async Task BackdatedPosting_RefreshesLaterDerivedFlags(
        decimal laterClosing, decimal delta, decimal expected, bool isZero, bool isNegative)
    {
        await using var fixture = await Fixture.CreateAsync();
        var later = fixture.AddPeriod(10);
        fixture.Db.AccountBalances.Add(new AccountBalance
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, AccountId = fixture.Debit.Id,
            AccountingBookId = fixture.Primary.Id, BookClassification = fixture.Primary.Code,
            FiscalPeriodId = later.Id, FiscalPeriod = later, Currency = "GHS",
            OpeningBalance = laterClosing, ClosingBalance = laterClosing,
            IsZeroBalance = laterClosing == 0m, IsNegativeBalance = laterClosing < 0m
        });
        await fixture.Db.SaveChangesAsync();
        var line = delta >= 0 ? fixture.Lines(delta, 0m) : fixture.Lines(0m, -delta);

        await fixture.Service.ApplyPostingAsync(fixture.TenantId, fixture.Primary.Id, fixture.Primary.Code,
            fixture.Period.Id, "GHS", line, true, DateTime.UtcNow, null);
        await fixture.Db.SaveChangesAsync();

        var refreshed = await fixture.Db.AccountBalances.SingleAsync(item => item.FiscalPeriodId == later.Id);
        refreshed.ClosingBalance.Should().Be(expected);
        refreshed.IsZeroBalance.Should().Be(isZero);
        refreshed.IsNegativeBalance.Should().Be(isNegative);
        refreshed.IsReconciled.Should().BeTrue();
    }

    [Fact]
    public async Task Rebuild_PrimaryCompatibilityExcludesAlternativeBookEvidence()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Debit.Balance = 2000m;
        fixture.AddPostedEvidence(fixture.Lines(1000m, 0m), fixture.Primary);
        var local = fixture.Lines(1000m, 0m);
        foreach (var line in local)
        {
            line.AccountingBookId = fixture.Parallel.Id;
            line.BookClassification = fixture.Parallel.Code;
        }
        fixture.AddPostedEvidence(local, fixture.Parallel);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.ReconcileAsync(fixture.TenantId,
            new("IFRS", true, "Repair primary compatibility", "repair-primary", Guid.NewGuid()), Guid.NewGuid());

        fixture.Debit.Balance.Should().Be(1000m);
        (await fixture.Db.AccountTransactions.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task ReconciliationFingerprint_CoversTransactionDateAndFiscalPlacement()
    {
        await using var fixture = await Fixture.CreateAsync();
        var lines = fixture.Lines(20m, 0m);
        fixture.AddPostedEvidence(lines, fixture.Primary);
        await fixture.Db.SaveChangesAsync();
        var first = await fixture.Service.ReconcileAsync(fixture.TenantId,
            new("IFRS", false, null, null, null), Guid.NewGuid());

        lines[0].TransactionDate = lines[0].TransactionDate.AddDays(1);
        await fixture.Db.SaveChangesAsync();
        var dateChanged = await fixture.Service.ReconcileAsync(fixture.TenantId,
            new("IFRS", false, null, null, null), Guid.NewGuid());
        dateChanged.SourceFingerprint.Should().NotBe(first.SourceFingerprint);

        fixture.Period.PeriodNumber++;
        await fixture.Db.SaveChangesAsync();
        var periodChanged = await fixture.Service.ReconcileAsync(fixture.TenantId,
            new("IFRS", false, null, null, null), Guid.NewGuid());
        periodChanged.SourceFingerprint.Should().NotBe(dateChanged.SourceFingerprint);
    }

    [Fact]
    public async Task SamePeriodBackdatedPosting_DoesNotRegressLastTransactionDate()
    {
        await using var fixture = await Fixture.CreateAsync();
        var recent = fixture.Lines(10m, 0m);
        recent[0].TransactionDate = new DateTime(2026, 9, 25);
        await fixture.Service.ApplyPostingAsync(fixture.TenantId, fixture.Primary.Id, fixture.Primary.Code,
            fixture.Period.Id, "GHS", recent, true, DateTime.UtcNow, null);
        await fixture.Db.SaveChangesAsync();
        var older = fixture.Lines(5m, 0m);
        older[0].TransactionDate = new DateTime(2026, 9, 5);
        await fixture.Service.ApplyPostingAsync(fixture.TenantId, fixture.Primary.Id, fixture.Primary.Code,
            fixture.Period.Id, "GHS", older, true, DateTime.UtcNow, null);
        await fixture.Db.SaveChangesAsync();

        (await fixture.Db.AccountBalances.SingleAsync()).LastTransactionDate
            .Should().Be(new DateTime(2026, 9, 25));
    }

    [Fact]
    public async Task RebuildIdempotency_BindsReasonMakerAndChecker()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.AddPostedEvidence(fixture.Lines(10m, 0m), fixture.Primary);
        await fixture.Db.SaveChangesAsync();
        var maker = Guid.NewGuid();
        var checker = Guid.NewGuid();
        await fixture.Service.ReconcileAsync(fixture.TenantId,
            new("IFRS", true, "Approved reason", "governed-key", checker), maker);

        await fixture.Service.Invoking(service => service.ReconcileAsync(fixture.TenantId,
                new("IFRS", true, "Changed reason", "governed-key", checker), maker))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*governance evidence*");
        await fixture.Service.Invoking(service => service.ReconcileAsync(fixture.TenantId,
                new("IFRS", true, "Approved reason", "governed-key", checker), Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*governance evidence*");
        await fixture.Service.Invoking(service => service.ReconcileAsync(fixture.TenantId,
                new("IFRS", true, "Approved reason", "governed-key", Guid.NewGuid()), maker))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*governance evidence*");
    }

    [Theory]
    [InlineData("account-code")]
    [InlineData("account-number")]
    [InlineData("account-type")]
    [InlineData("mapping-identity")]
    [InlineData("mapping-disabled")]
    [InlineData("mapping-book")]
    [InlineData("classification-id")]
    [InlineData("classification-code")]
    [InlineData("classification-core-type")]
    [InlineData("classification-status")]
    [InlineData("classification-posting")]
    public async Task RebuildIdempotency_BindsPrimaryAccountMappingAndClassificationAuthority(string mutation)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.AddPostedEvidence(fixture.Lines(10m, 0m), fixture.Primary);
        await fixture.Db.SaveChangesAsync();
        var maker = Guid.NewGuid();
        var checker = Guid.NewGuid();
        var command = new BookBalanceReconciliationRequestDto(
            "IFRS", true, "Governed rebuild", "authority-key", checker);
        await fixture.Service.ReconcileAsync(fixture.TenantId, command, maker);

        await fixture.MutatePrimaryAuthorityAsync(mutation);

        await fixture.Service.Invoking(service => service.ReconcileAsync(fixture.TenantId, command, maker))
            .Should().ThrowAsync<InvalidOperationException>(
                "changed derivation authority must never reuse the prior rebuild as a false no-op");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reconciliation_RejectsActivePostedLineWithDeletedHeaderBeforePreviewOrApply(bool apply)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Debit.Balance = 77m;
        fixture.AddPostedEvidence(fixture.Lines(10m, 0m), fixture.Primary);
        await fixture.Db.SaveChangesAsync();
        var header = await fixture.Db.JournalEntries.IgnoreQueryFilters().SingleAsync();
        header.IsDeleted = true;
        await fixture.Db.SaveChangesAsync();

        var action = () => fixture.Service.ReconcileAsync(fixture.TenantId,
            new("IFRS", apply, apply ? "Corrupt evidence must fail" : null,
                apply ? "deleted-header" : null, apply ? Guid.NewGuid() : null), Guid.NewGuid());

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*journal/header book evidence*");
        fixture.Debit.Balance.Should().Be(77m);
        (await fixture.Db.AccountBalances.CountAsync()).Should().Be(0);
        (await fixture.Db.FinanceBalanceRebuildRuns.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ReconciliationDrift_CoversDerivedFlagsDatesTotalsAndExposureFingerprint()
    {
        await using var fixture = await Fixture.CreateAsync();
        var lines = fixture.Lines(20m, 0m, "USD", 2m, 0m);
        fixture.AddPostedEvidence(lines, fixture.Primary);
        await fixture.Db.SaveChangesAsync();
        await fixture.Service.ReconcileAsync(fixture.TenantId,
            new("IFRS", true, "Build projections", "full-drift-1", Guid.NewGuid()), Guid.NewGuid());

        var balance = await fixture.Db.AccountBalances.SingleAsync();
        balance.YearToDateDebits++;
        balance.TransactionCount++;
        balance.LastTransactionDate = balance.LastTransactionDate!.Value.AddDays(1);
        balance.HasActivity = false;
        balance.IsZeroBalance = true;
        balance.IsNegativeBalance = true;
        var exposure = await fixture.Db.AccountCurrencyExposures.SingleAsync();
        exposure.TransactionCount++;
        exposure.FirstTransactionDate = exposure.FirstTransactionDate!.Value.AddDays(1);
        exposure.SourceFingerprint = new string('A', 64);
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.ReconcileAsync(fixture.TenantId,
            new("IFRS", false, null, null, null), Guid.NewGuid());
        result.BalanceDriftCount.Should().BeGreaterThan(0);
        result.ExposureDriftCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BaseCurrencyLine_DoesNotFabricateForeignExposure()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.ApplyPostingAsync(fixture.TenantId, fixture.Primary.Id, "IFRS", fixture.Period.Id,
            "GHS", fixture.Lines(10m, 0m, "GHS", 10m, 0m), true, DateTime.UtcNow, null);
        await fixture.Db.SaveChangesAsync();
        (await fixture.Db.AccountCurrencyExposures.CountAsync()).Should().Be(0);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public ApplicationDbContext Db { get; }
        public BookBalanceReadModelService Service { get; }
        public AccountingBook Primary { get; private set; } = null!;
        public AccountingBook Parallel { get; private set; } = null!;
        public Account Debit { get; private set; } = null!;
        public AccountAccountingBook PrimaryMapping { get; private set; } = null!;
        public AccountClassification PrimaryClassification { get; private set; } = null!;
        public FiscalPeriod Period { get; private set; } = null!;
        private FiscalYear _year = null!;

        private Fixture(ApplicationDbContext db) { Db = db; Service = new(db); }
        public static async Task<Fixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"c2-book-balances-{Guid.NewGuid():N}")
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
            var f = new Fixture(new ApplicationDbContext(options));
            f.Db.Tenants.Add(new Tenant { Id = f.TenantId, Name = "C2", Code = "C2" });
            f.Primary = new AccountingBook { Id = Guid.NewGuid(), TenantId = f.TenantId, Code = "IFRS", Name = "IFRS", IsDefault = true };
            f.Parallel = new AccountingBook { Id = Guid.NewGuid(), TenantId = f.TenantId, Code = "LOCAL_STATUTORY", Name = "Local" };
            f.Debit = new Account { Id = Guid.NewGuid(), TenantId = f.TenantId, AccountCode = "1000", AccountNumber = "1000",
                AccountName = "Cash", AccountType = AccountType.Asset, CurrencyCode = "GHS", Status = AccountStatus.Active };
            f.PrimaryClassification = new AccountClassification
            {
                Id = Guid.NewGuid(), TenantId = f.TenantId, AccountingBookId = f.Primary.Id,
                Code = "CASH", Name = "Cash", CoreAccountType = AccountType.Asset,
                Status = AccountClassificationStatus.Active, IsPostingClassification = true
            };
            var parallelClassification = new AccountClassification
            {
                Id = Guid.NewGuid(), TenantId = f.TenantId, AccountingBookId = f.Parallel.Id,
                Code = "CASH", Name = "Cash", CoreAccountType = AccountType.Asset,
                Status = AccountClassificationStatus.Active, IsPostingClassification = true
            };
            f.PrimaryMapping = new AccountAccountingBook
            {
                Id = Guid.NewGuid(), TenantId = f.TenantId, AccountId = f.Debit.Id,
                AccountingBookId = f.Primary.Id, AccountClassificationId = f.PrimaryClassification.Id,
                AccountClassification = f.PrimaryClassification, IsEnabled = true
            };
            var year = new FiscalYear { Id = Guid.NewGuid(), TenantId = f.TenantId, Year = 2026,
                FiscalYearName = "2026", FiscalYearCode = "FY2026",
                StartDate = new(2026, 1, 1), EndDate = new(2026, 12, 31) };
            f.Period = new FiscalPeriod { Id = Guid.NewGuid(), TenantId = f.TenantId, FiscalYearId = year.Id,
                FiscalYear = year, PeriodName = "Sep", PeriodCode = "2026-09", PeriodNumber = 9,
                StartDate = new(2026, 9, 1), EndDate = new(2026, 9, 30), IsOpen = true };
            f._year = year;
            f.Db.AddRange(f.Primary, f.Parallel, f.Debit, f.PrimaryClassification, parallelClassification,
                year, f.Period, f.PrimaryMapping,
                new AccountAccountingBook { Id = Guid.NewGuid(), TenantId = f.TenantId, AccountId = f.Debit.Id,
                    AccountingBookId = f.Parallel.Id, AccountClassificationId = parallelClassification.Id,
                    AccountClassification = parallelClassification, IsEnabled = true },
                new AccountCurrencyLink { TenantId = f.TenantId, AccountId = f.Debit.Id, LinkedCurrencyCode = "USD", IsActive = true });
            await f.Db.SaveChangesAsync(); return f;
        }
        public async Task MutatePrimaryAuthorityAsync(string mutation)
        {
            switch (mutation)
            {
                case "account-code": Debit.AccountCode = "1001"; break;
                case "account-number": Debit.AccountNumber = "01-1001"; break;
                case "account-type": Debit.AccountType = AccountType.Liability; break;
                case "mapping-disabled": PrimaryMapping.IsEnabled = false; break;
                case "mapping-book": PrimaryMapping.AccountingBookId = Parallel.Id; break;
                case "mapping-identity":
                    Db.AccountAccountingBooks.Remove(PrimaryMapping);
                    PrimaryMapping = new AccountAccountingBook
                    {
                        Id = Guid.NewGuid(), TenantId = TenantId, AccountId = Debit.Id,
                        AccountingBookId = Primary.Id, AccountClassificationId = PrimaryClassification.Id,
                        AccountClassification = PrimaryClassification, IsEnabled = true
                    };
                    Db.AccountAccountingBooks.Add(PrimaryMapping);
                    break;
                case "classification-id":
                    var replacement = new AccountClassification
                    {
                        Id = Guid.NewGuid(), TenantId = TenantId, AccountingBookId = Primary.Id,
                        Code = "CASH_REPLACEMENT", Name = "Replacement cash", CoreAccountType = AccountType.Asset,
                        Status = AccountClassificationStatus.Active, IsPostingClassification = true
                    };
                    Db.AccountClassifications.Add(replacement);
                    PrimaryMapping.AccountClassificationId = replacement.Id;
                    PrimaryMapping.AccountClassification = replacement;
                    break;
                case "classification-code": PrimaryClassification.Code = "CASH_CHANGED"; break;
                case "classification-core-type": PrimaryClassification.CoreAccountType = AccountType.Liability; break;
                case "classification-status": PrimaryClassification.Status = AccountClassificationStatus.Retired; break;
                case "classification-posting": PrimaryClassification.IsPostingClassification = false; break;
                default: throw new ArgumentOutOfRangeException(nameof(mutation));
            }
            await Db.SaveChangesAsync();
        }
        public FiscalPeriod AddPeriod(int number)
        {
            var period = new FiscalPeriod
            {
                Id = Guid.NewGuid(), TenantId = TenantId, FiscalYearId = _year.Id, FiscalYear = _year,
                PeriodName = $"P{number}", PeriodCode = $"2026-{number:00}", PeriodNumber = number,
                StartDate = new DateTime(2026, number, 1), EndDate = new DateTime(2026, number, 1).AddMonths(1).AddDays(-1),
                IsOpen = true
            };
            Db.FiscalPeriods.Add(period);
            return period;
        }
        public void AddPostedEvidence(IEnumerable<AccountTransaction> evidence, AccountingBook book)
        {
            foreach (var line in evidence)
            {
                var journal = new JournalEntry
                {
                    Id = line.JournalEntryId, TenantId = TenantId, JournalEntryNumber = $"J-{line.Id:N}",
                    EntryDate = line.TransactionDate, PostingDate = line.TransactionDate,
                    Description = "C2 evidence", JournalType = "General", PostingStatus = "Posted",
                    AccountingBookId = book.Id, BookClassification = book.Code
                };
                line.JournalEntry = journal;
                Db.JournalEntries.Add(journal);
                Db.AccountTransactions.Add(line);
            }
        }
        public AccountTransaction[] Lines(decimal debit, decimal credit, string currency = "GHS", decimal? txDebit = null, decimal? txCredit = null) =>
            new[] { new AccountTransaction { Id = Guid.NewGuid(), TenantId = TenantId, AccountId = Debit.Id,
                JournalEntryId = Guid.NewGuid(), TransactionDate = new(2026, 9, 5), DebitAmount = debit, CreditAmount = credit,
                FunctionalCurrencyCode = "GHS", TransactionCurrency = currency,
                TransactionDebitAmount = txDebit ?? debit, TransactionCreditAmount = txCredit ?? credit,
                BookClassification = "IFRS", AccountingBookId = Primary.Id, FiscalPeriodId = Period.Id,
                FiscalPeriod = Period, PostingStatus = "Posted", LineNumber = 1 } };
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
