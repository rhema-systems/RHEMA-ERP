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
        fixture.Db.AccountTransactions.AddRange(fixture.Lines(40m, 0m));
        fixture.Db.AccountBalances.Add(new AccountBalance { TenantId = fixture.TenantId, AccountId = fixture.Debit.Id,
            AccountingBookId = fixture.Primary.Id, BookClassification = "IFRS", FiscalPeriodId = fixture.Period.Id,
            Currency = "GHS", ClosingBalance = 999m });
        await fixture.Db.SaveChangesAsync();

        var dry = await fixture.Service.ReconcileAsync(fixture.TenantId,
            new("IFRS", false, null, null, null), Guid.NewGuid());
        dry.BalanceDriftCount.Should().BeGreaterThan(0);
        (await fixture.Db.AccountBalances.SingleAsync()).ClosingBalance.Should().Be(999m);

        var maker = Guid.NewGuid(); var checker = Guid.NewGuid();
        var request = new BookBalanceReconciliationRequestDto("IFRS", true, "Reviewed C2 drift", "rebuild-1", checker);
        var applied = await fixture.Service.ReconcileAsync(fixture.TenantId, request, maker);
        (await fixture.Db.AccountBalances.SingleAsync()).ClosingBalance.Should().Be(40m);
        var repeated = await fixture.Service.ReconcileAsync(fixture.TenantId, request, maker);
        repeated.RebuildRunId.Should().Be(applied.RebuildRunId);
        (await fixture.Db.FinanceBalanceRebuildRuns.CountAsync()).Should().Be(1);
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
        public FiscalPeriod Period { get; private set; } = null!;

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
            var year = new FiscalYear { Id = Guid.NewGuid(), TenantId = f.TenantId, Year = 2026,
                FiscalYearName = "2026", FiscalYearCode = "FY2026",
                StartDate = new(2026, 1, 1), EndDate = new(2026, 12, 31) };
            f.Period = new FiscalPeriod { Id = Guid.NewGuid(), TenantId = f.TenantId, FiscalYearId = year.Id,
                FiscalYear = year, PeriodName = "Sep", PeriodCode = "2026-09", PeriodNumber = 9,
                StartDate = new(2026, 9, 1), EndDate = new(2026, 9, 30), IsOpen = true };
            f.Db.AddRange(f.Primary, f.Parallel, f.Debit, year, f.Period,
                new AccountAccountingBook { TenantId = f.TenantId, AccountId = f.Debit.Id, AccountingBookId = f.Primary.Id, IsEnabled = true },
                new AccountAccountingBook { TenantId = f.TenantId, AccountId = f.Debit.Id, AccountingBookId = f.Parallel.Id, IsEnabled = true },
                new AccountCurrencyLink { TenantId = f.TenantId, AccountId = f.Debit.Id, LinkedCurrencyCode = "USD", IsActive = true });
            await f.Db.SaveChangesAsync(); return f;
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
