using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FiscalYearCloseTests
{
    [Fact]
    [Trait("Batch", "FinanceReviewHardening")]
    [Trait("Category", "YearEndClose")]
    public async Task CloseFiscalYearAsync_ShouldPostClosingEntryThroughEngineAndZeroIncomeStatementAccounts()
    {
        var fixture = await FixtureWithPostedActivityAsync();

        var result = await fixture.GlService.CloseFiscalYearAsync(new YearEndCloseRequestDto
        {
            FiscalYearId = fixture.FiscalYear.Id,
            RetainedEarningsAccountId = fixture.RetainedEarnings.Id,
            ClosingNotes = "FY2026 close"
        });

        result.Success.Should().BeTrue();

        var year = await fixture.Db.FiscalYears.SingleAsync(y => y.Id == fixture.FiscalYear.Id);
        year.IsClosed.Should().BeTrue();
        year.Status.Should().Be("Closed");
        year.ClosingJournalEntryId.Should().NotBeNull();
        year.NetIncomeTransferred.Should().Be(600m);
        year.RetainedEarningsTransferComplete.Should().BeTrue();

        var closingEntry = await fixture.Db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == year.ClosingJournalEntryId!.Value);
        closingEntry.PostingStatus.Should().Be("Posted");
        closingEntry.Transactions.Should().HaveCount(3);
        closingEntry.Transactions.Single(t => t.AccountId == fixture.Revenue.Id).DebitAmount.Should().Be(1000m);
        closingEntry.Transactions.Single(t => t.AccountId == fixture.Expense.Id).CreditAmount.Should().Be(400m);
        closingEntry.Transactions.Single(t => t.AccountId == fixture.RetainedEarnings.Id).CreditAmount.Should().Be(600m);

        // Posting-event back-reference proves the engine (not direct GL writes) posted the close.
        var postingEvent = await fixture.Db.FinancePostingEvents
            .SingleAsync(e => e.SourceDocumentType == "YearEndClose" && e.SourceDocumentId == fixture.FiscalYear.Id);
        postingEvent.JournalEntryId.Should().Be(closingEntry.Id);

        // Engine balance snapshots: income statement accounts zeroed, net income in equity.
        fixture.Revenue.Balance.Should().Be(0m);
        fixture.Expense.Balance.Should().Be(0m);
        fixture.RetainedEarnings.Balance.Should().Be(600m);
    }

    [Fact]
    [Trait("Batch", "FinanceReviewHardening")]
    [Trait("Category", "YearEndClose")]
    public async Task CloseFiscalYearAsync_ShouldFailWhilePeriodsRemainOpen()
    {
        var fixture = await FixtureWithPostedActivityAsync(closePeriod: false);

        var result = await fixture.GlService.CloseFiscalYearAsync(new YearEndCloseRequestDto
        {
            FiscalYearId = fixture.FiscalYear.Id,
            RetainedEarningsAccountId = fixture.RetainedEarnings.Id
        });

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("still open"));
        (await fixture.Db.FiscalYears.SingleAsync(y => y.Id == fixture.FiscalYear.Id)).IsClosed.Should().BeFalse();
    }

    [Fact]
    [Trait("Batch", "FinanceReviewHardening")]
    [Trait("Category", "YearEndClose")]
    public async Task CloseFiscalYearAsync_ShouldFailWhenDraftJournalRemainsInClosedPeriod()
    {
        var fixture = await FixtureWithPostedActivityAsync();
        var periodId = await fixture.Db.FiscalPeriods
            .Where(p => p.FiscalYearId == fixture.FiscalYear.Id)
            .Select(p => p.Id)
            .SingleAsync();

        fixture.Db.JournalEntries.Add(new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.FiscalYear.TenantId,
            JournalEntryNumber = "JE-DRAFT-001",
            Description = "Unposted year-end adjustment",
            EntryDate = new DateTime(2026, 12, 31),
            FiscalPeriodId = periodId,
            PostingStatus = "Draft",
            ApprovalStatus = "Draft"
        });
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.GlService.CloseFiscalYearAsync(new YearEndCloseRequestDto
        {
            FiscalYearId = fixture.FiscalYear.Id,
            RetainedEarningsAccountId = fixture.RetainedEarnings.Id
        });

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("unposted journal entries");
        result.Errors.Should().ContainSingle(e => e.Contains("1 journal entry is not posted"));
        (await fixture.Db.FiscalYears.SingleAsync(y => y.Id == fixture.FiscalYear.Id))
            .IsClosed.Should().BeFalse();
    }

    [Fact]
    [Trait("Batch", "FinanceReviewHardening")]
    [Trait("Category", "YearEndClose")]
    public async Task CloseFiscalYearAsync_ShouldFailWhenUnpostedTransactionLineRemains()
    {
        var fixture = await FixtureWithPostedActivityAsync();
        var periodId = await fixture.Db.FiscalPeriods
            .Where(p => p.FiscalYearId == fixture.FiscalYear.Id)
            .Select(p => p.Id)
            .SingleAsync();

        fixture.Db.AccountTransactions.Add(new AccountTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.FiscalYear.TenantId,
            AccountId = fixture.Expense.Id,
            JournalEntryId = Guid.NewGuid(),
            FiscalPeriodId = periodId,
            TransactionDate = new DateTime(2026, 12, 31),
            PostingStatus = "Draft",
            FunctionalCurrencyCode = "GHS",
            DebitAmount = 25m
        });
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.GlService.CloseFiscalYearAsync(new YearEndCloseRequestDto
        {
            FiscalYearId = fixture.FiscalYear.Id,
            RetainedEarningsAccountId = fixture.RetainedEarnings.Id
        });

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("unposted transaction lines");
        result.Errors.Should().ContainSingle(e => e.Contains("1 account transaction line is not posted"));
        (await fixture.Db.FiscalYears.SingleAsync(y => y.Id == fixture.FiscalYear.Id))
            .IsClosed.Should().BeFalse();
    }

    [Fact]
    [Trait("Batch", "FinanceReviewHardening")]
    [Trait("Category", "YearEndClose")]
    public async Task CloseFiscalYearAsync_ShouldRejectSecondCloseWithoutReopen()
    {
        var fixture = await FixtureWithPostedActivityAsync();

        var first = await fixture.GlService.CloseFiscalYearAsync(new YearEndCloseRequestDto
        {
            FiscalYearId = fixture.FiscalYear.Id,
            RetainedEarningsAccountId = fixture.RetainedEarnings.Id
        });
        first.Success.Should().BeTrue();
        var closingJournalEntryId = (await fixture.Db.FiscalYears.SingleAsync(y => y.Id == fixture.FiscalYear.Id)).ClosingJournalEntryId;

        var second = await fixture.GlService.CloseFiscalYearAsync(new YearEndCloseRequestDto
        {
            FiscalYearId = fixture.FiscalYear.Id,
            RetainedEarningsAccountId = fixture.RetainedEarnings.Id
        });

        second.Success.Should().BeFalse();
        second.Message.Should().Contain("already closed");
        (await fixture.Db.FiscalYears.SingleAsync(y => y.Id == fixture.FiscalYear.Id))
            .ClosingJournalEntryId.Should().Be(closingJournalEntryId);
    }

    [Fact]
    [Trait("Batch", "FinanceReviewHardening")]
    [Trait("Category", "YearEndClose")]
    public async Task ReopenFiscalYearAsync_ShouldReverseClosingEntryAndRestoreBalances()
    {
        var fixture = await FixtureWithPostedActivityAsync();
        await fixture.GlService.CloseFiscalYearAsync(new YearEndCloseRequestDto
        {
            FiscalYearId = fixture.FiscalYear.Id,
            RetainedEarningsAccountId = fixture.RetainedEarnings.Id
        });

        var result = await fixture.GlService.ReopenFiscalYearAsync(fixture.FiscalYear.Id, "Late supplier invoices for December");

        result.Success.Should().BeTrue();

        var year = await fixture.Db.FiscalYears.SingleAsync(y => y.Id == fixture.FiscalYear.Id);
        year.IsClosed.Should().BeFalse();
        year.Status.Should().Be("Open");
        year.ClosingJournalEntryId.Should().BeNull();
        year.RetainedEarningsTransferComplete.Should().BeFalse();
        year.YearEndClosingNotes.Should().Contain("Late supplier invoices for December");

        var reversalEvent = await fixture.Db.FinancePostingEvents
            .SingleAsync(e => e.SourceDocumentType == "YearEndCloseReversal" && e.SourceDocumentId == fixture.FiscalYear.Id);
        reversalEvent.JournalEntryId.Should().NotBeNull();

        fixture.Revenue.Balance.Should().Be(1000m);
        fixture.Expense.Balance.Should().Be(400m);
        fixture.RetainedEarnings.Balance.Should().Be(0m);
    }

    private sealed record Fixture(
        ApplicationDbContext Db,
        GeneralLedgerService GlService,
        FiscalYear FiscalYear,
        Account Revenue,
        Account Expense,
        Account RetainedEarnings);

    private static async Task<Fixture> FixtureWithPostedActivityAsync(bool closePeriod = true)
    {
        var tenantId = Guid.NewGuid();
        var db = CreateContext();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Year Close Tenant",
            Code = "YEC",
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });

        var fiscalYear = new FiscalYear
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearName = "Fiscal Year 2026",
            FiscalYearCode = "FY2026",
            Year = 2026,
            FiscalYearType = "Calendar",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            Status = "Open",
            IsActive = true
        };
        db.FiscalYears.Add(fiscalYear);

        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = fiscalYear.Id,
            PeriodName = "FY2026",
            PeriodCode = "2026",
            PeriodNumber = 1,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            PeriodDays = 365,
            PeriodStatus = "Open",
            IsOpen = true,
            IsClosed = false
        };
        db.FiscalPeriods.Add(period);

        var cash = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var revenue = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        var expense = SeedAccount(db, tenantId, "5000", AccountType.Expense);
        var retainedEarnings = SeedAccount(db, tenantId, "3900", AccountType.Equity);
        await db.SaveChangesAsync();

        var currentUser = CreateCurrentUser(tenantId);
        var engine = new FinancePostingEngine(db, currentUser.Object, Mock.Of<ILogger<FinancePostingEngine>>());

        // Post real activity through the engine: revenue 1000, expense 400 (net income 600).
        await engine.PostAsync(ActivityRequest(tenantId, "REV-1", new[]
        {
            new FinancePostingLineDto { AccountId = cash.Id, Description = "Cash in", DebitAmount = 1000m },
            new FinancePostingLineDto { AccountId = revenue.Id, Description = "Sales", CreditAmount = 1000m }
        }));
        await engine.PostAsync(ActivityRequest(tenantId, "EXP-1", new[]
        {
            new FinancePostingLineDto { AccountId = expense.Id, Description = "Rent", DebitAmount = 400m },
            new FinancePostingLineDto { AccountId = cash.Id, Description = "Cash out", CreditAmount = 400m }
        }));

        if (closePeriod)
        {
            period.IsOpen = false;
            period.IsClosed = true;
            period.PeriodStatus = "Closed";
            await db.SaveChangesAsync();
        }

        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");

        var reportingOptions = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase($"fiscal-year-close-read-{Guid.NewGuid()}")
            .Options;

        var glService = new GeneralLedgerService(
            db,
            new ReportingDbContext(reportingOptions),
            currentUser.Object,
            tenantSettings.Object,
            Mock.Of<IFiscalPeriodService>(),
            Mock.Of<IDocumentNumberingService>(),
            Mock.Of<IAccountingBookService>(),
            engine);

        return new Fixture(db, glService, fiscalYear, revenue, expense, retainedEarnings);
    }

    private static FinancePostingRequestDto ActivityRequest(Guid tenantId, string reference, FinancePostingLineDto[] lines)
    {
        return new FinancePostingRequestDto
        {
            SourceModule = "TEST",
            SourceDocumentType = "YearActivity",
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentTenantId = tenantId,
            PostingAction = "Post",
            SourceDocumentReference = reference,
            Description = $"Activity {reference}",
            PostingDate = new DateTime(2026, 6, 15),
            JournalType = "System Generated",
            BookClassification = "IFRS",
            FunctionalCurrencyCode = "GHS",
            Lines = lines
        };
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"fiscal-year-close-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(x => x.UserName).Returns("year.closer");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("fiscal-year-close-tests");
        return currentUser;
    }

    private static Account SeedAccount(ApplicationDbContext db, Guid tenantId, string accountNumber, AccountType accountType)
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
            CurrencyCode = "GHS",
            AllowDirectPosting = true
        };

        db.Accounts.Add(account);
        return account;
    }
}
