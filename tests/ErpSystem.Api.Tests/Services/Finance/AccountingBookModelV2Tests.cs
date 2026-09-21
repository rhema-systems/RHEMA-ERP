using ErpSystem.Api.Services.Finance.Settings;
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
        await db.SaveChangesAsync();

        var service = new AccountingBookInitializationService(
            db, User(tenantId).Object, Mock.Of<IWorkflowService>(), Audit().Object);
        await service.EnsureDeltaStructureAsync(parallel.Id);

        parallel.CurrencyTranslationReserveAccountId.Should().NotBeNull();
        parallel.CurrencyRoundingAccountId.Should().NotBeNull();
        var protectedAccounts = await db.Accounts.Where(item =>
            item.Id == parallel.CurrencyTranslationReserveAccountId
            || item.Id == parallel.CurrencyRoundingAccountId).ToListAsync();
        protectedAccounts.Should().HaveCount(2).And.OnlyContain(item => item.IsSystemAccount && !item.AllowDirectPosting);
        (await db.AccountAccountingBooks.CountAsync(item => item.AccountingBookId == parallel.Id)).Should().Be(5);
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
