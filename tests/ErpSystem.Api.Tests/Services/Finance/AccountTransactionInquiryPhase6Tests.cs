using System.Reflection;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class AccountTransactionInquiryPhase6Tests
{
    [Fact]
    public void Endpoint_RequiresFinanceRead()
    {
        var action = typeof(AccountController).GetMethod(nameof(AccountController.GetAccountTransactions));

        action.Should().NotBeNull();
        action!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(FinancePermissions.ViewFinance);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Inquiry_RejectsOutOfRangePages(int page, int pageSize)
    {
        await using var db = CreateContext();
        using var unitOfWork = new UnitOfWork(db);
        var service = CreateService(unitOfWork, Guid.NewGuid());

        var action = () => service.GetTransactionsAsync(Guid.NewGuid(), "BOOK", page, pageSize);

        await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Inquiry_FailsClosedForCrossTenantAccountAndDisabledMapping()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var (_, book, account) = SeedAuthority(db, tenantId, enabled: false);
        var foreignAccount = NewAccount(otherTenantId, "FOREIGN");
        db.Accounts.Add(foreignAccount);
        await db.SaveChangesAsync();
        using var unitOfWork = new UnitOfWork(db);
        var service = CreateService(unitOfWork, tenantId);

        await FluentActions.Invoking(() => service.GetTransactionsAsync(foreignAccount.Id, book.Code))
            .Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Invoking(() => service.GetTransactionsAsync(account.Id, book.Code))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*disabled*");
    }

    [Fact]
    public async Task Inquiry_FailsClosedForUnknownOrInactiveBook()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var (_, book, account) = SeedAuthority(db, tenantId, enabled: true);
        book.IsActive = false;
        await db.SaveChangesAsync();
        using var unitOfWork = new UnitOfWork(db);
        var service = CreateService(unitOfWork, tenantId);

        await FluentActions.Invoking(() => service.GetTransactionsAsync(account.Id, "UNKNOWN"))
            .Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Invoking(() => service.GetTransactionsAsync(account.Id, book.Code))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*inactive*");
    }

    [Fact]
    public async Task Inquiry_ReturnsARealEmptyPageForAnEnabledExactBook()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var (_, book, account) = SeedAuthority(db, tenantId, enabled: true);
        await db.SaveChangesAsync();
        using var unitOfWork = new UnitOfWork(db);
        var service = CreateService(unitOfWork, tenantId);

        var result = await service.GetTransactionsAsync(account.Id, book.Code);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
        result.AccountingBookCode.Should().Be(book.Code);
    }

    [Fact]
    public async Task Inquiry_ReturnsOnlyPostedExactBookLinesInStablePages()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var (_, book, account) = SeedAuthority(db, tenantId, enabled: true);
        var otherBook = new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "LOCAL", Name = "Local",
            IsActive = true, AllowsPosting = true
        };
        db.AccountingBooks.Add(otherBook);
        db.AccountAccountingBooks.Add(new AccountAccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountId = account.Id,
            AccountingBookId = otherBook.Id, IsEnabled = true
        });
        var date = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc);
        AddLine(db, tenantId, account, book.Code, "JE-003", 1, "Posted", date);
        AddLine(db, tenantId, account, book.Code, "JE-002", 2, "Posted", date);
        AddLine(db, tenantId, account, book.Code, "JE-002", 1, "Posted", date);
        AddLine(db, tenantId, account, book.Code, "JE-DRAFT", 9, "Draft", date.AddDays(1));
        AddLine(db, tenantId, account, otherBook.Code, "JE-LOCAL", 9, "Posted", date.AddDays(2));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        using var unitOfWork = new UnitOfWork(db);
        var service = CreateService(unitOfWork, tenantId);

        var first = await service.GetTransactionsAsync(account.Id, book.Code, 1, 2);
        var second = await service.GetTransactionsAsync(account.Id, book.Code, 2, 2);

        first.TotalCount.Should().Be(3);
        first.TotalPages.Should().Be(2);
        first.Items.Select(item => (item.JournalEntryNumber, item.LineNumber))
            .Should().Equal(("JE-003", 1), ("JE-002", 2));
        second.Items.Select(item => (item.JournalEntryNumber, item.LineNumber))
            .Should().Equal(("JE-002", 1));
        first.Items.Concat(second.Items).Select(item => item.Id).Should().OnlyHaveUniqueItems();
        first.Items.Should().OnlyContain(item => item.AccountingBookCode == book.Code
            && item.FunctionalCurrencyCode == "GHS");
        first.Items.Should().OnlyContain(item => item.TransactionCurrencyCode == "USD"
            && item.TransactionDebitAmount == 2m
            && item.ForeignAmount == 2m
            && item.ExchangeRate == 5m
            && item.Dimensions.Count == 0);
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("set")]
    [InlineData("item")]
    [InlineData("definition")]
    [InlineData("value")]
    public async Task Inquiry_FailsClosedWithoutDisclosureForCrossTenantDimensionEvidence(string corruption)
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();
        var (_, book, account) = SeedAuthority(db, tenantId, enabled: true);
        var transaction = AddLine(db, tenantId, account, book.Code, "JE-DIM", 1, "Posted", DateTime.UtcNow);
        var definition = new FinanceDimensionDefinition
        {
            Id = Guid.NewGuid(), TenantId = corruption == "definition" ? foreignTenantId : tenantId,
            Code = "DEPT", Name = "Department"
        };
        var value = new FinanceDimensionValue
        {
            Id = Guid.NewGuid(), TenantId = corruption == "value" ? foreignTenantId : tenantId,
            FinanceDimensionDefinitionId = definition.Id, Code = "OPS", Name = "Operations"
        };
        var set = new FinanceDimensionSet
        {
            Id = Guid.NewGuid(), TenantId = corruption == "set" ? foreignTenantId : tenantId,
            CombinationHash = "hash", DisplayValue = "DEPT=OPS"
        };
        var snapshot = new FinanceDimensionSnapshot
        {
            Id = Guid.NewGuid(), TenantId = corruption == "snapshot" ? foreignTenantId : tenantId,
            FinanceDimensionSetId = set.Id, CombinationHashSnapshot = "hash", DisplayValueSnapshot = "DEPT=OPS"
        };
        set.Items.Add(new FinanceDimensionSetItem
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FinanceDimensionSetId = set.Id,
            FinanceDimensionDefinitionId = definition.Id, FinanceDimensionValueId = value.Id,
            DimensionCodeSnapshot = "DEPT", DimensionNameSnapshot = "Department",
            DimensionValueCodeSnapshot = "OPS", DimensionValueNameSnapshot = "Operations"
        });
        snapshot.Items.Add(new FinanceDimensionSnapshotItem
        {
            Id = Guid.NewGuid(), TenantId = corruption == "item" ? foreignTenantId : tenantId,
            FinanceDimensionSnapshotId = snapshot.Id, FinanceDimensionDefinitionId = definition.Id,
            FinanceDimensionValueId = value.Id, DimensionCodeSnapshot = "DEPT",
            DimensionNameSnapshot = "Department", DimensionValueCodeSnapshot = "OPS",
            DimensionValueNameSnapshot = "Operations"
        });
        transaction.FinanceDimensionSetId = set.Id;
        transaction.FinanceDimensionSet = set;
        transaction.FinanceDimensionSnapshotId = snapshot.Id;
        transaction.FinanceDimensionSnapshot = snapshot;
        db.AddRange(definition, value, set, snapshot);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        using var unitOfWork = new UnitOfWork(db);

        await FluentActions.Invoking(() => CreateService(unitOfWork, tenantId)
                .GetTransactionsAsync(account.Id, book.Code))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*integrity*");
    }

    [Theory]
    [InlineData("OTHER", "Posted")]
    [InlineData("PRIMARY", "Draft")]
    public async Task Inquiry_FailsClosedForJournalBookOrPostedStatusMismatch(string journalBook, string journalStatus)
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var (_, book, account) = SeedAuthority(db, tenantId, enabled: true);
        var transaction = AddLine(db, tenantId, account, book.Code, "JE-CORRUPT", 1, "Posted", DateTime.UtcNow);
        transaction.JournalEntry.BookClassification = journalBook;
        transaction.JournalEntry.PostingStatus = journalStatus;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        using var unitOfWork = new UnitOfWork(db);

        await FluentActions.Invoking(() => CreateService(unitOfWork, tenantId)
                .GetTransactionsAsync(account.Id, book.Code))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*journal evidence*");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inquiry_FailsClosedForMismatchedOrLaterOtherBookPostingEvent(bool addValidFirst)
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var (_, book, account) = SeedAuthority(db, tenantId, enabled: true);
        var transaction = AddLine(db, tenantId, account, book.Code, "JE-EVENT", 1, "Posted", DateTime.UtcNow);
        if (addValidFirst)
            db.FinancePostingEvents.Add(NewPostingEvent(tenantId, transaction.JournalEntryId, book.Code));
        db.FinancePostingEvents.Add(NewPostingEvent(tenantId, transaction.JournalEntryId, "OTHER"));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        using var unitOfWork = new UnitOfWork(db);

        await FluentActions.Invoking(() => CreateService(unitOfWork, tenantId)
                .GetTransactionsAsync(account.Id, book.Code))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Posting-event evidence*");
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("status")]
    [InlineData("source")]
    public async Task Inquiry_FailsClosedForPostingEventTenantStatusOrSourceMismatch(string corruption)
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var (_, book, account) = SeedAuthority(db, tenantId, enabled: true);
        var transaction = AddLine(db, tenantId, account, book.Code, "JE-EVENT-INTEGRITY", 1, "Posted", DateTime.UtcNow);
        var postingEvent = NewPostingEvent(
            corruption == "tenant" ? Guid.NewGuid() : tenantId,
            transaction.JournalEntryId,
            book.Code);
        if (corruption == "status")
            postingEvent.PostingStatus = "Failed";
        if (corruption == "source")
            transaction.SourceModule = "AR";
        db.FinancePostingEvents.Add(postingEvent);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        using var unitOfWork = new UnitOfWork(db);

        await FluentActions.Invoking(() => CreateService(unitOfWork, tenantId)
                .GetTransactionsAsync(account.Id, book.Code))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Posting-event*");
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"account-inquiry-phase6-{Guid.NewGuid():N}").Options);

    private static AccountService CreateService(UnitOfWork unitOfWork, Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserName).Returns("finance.reader");
        return new AccountService(unitOfWork, currentUser.Object, Mock.Of<IAccountingBookService>(),
            NullLogger<AccountService>.Instance);
    }

    private static (AccountAccountingBook Mapping, AccountingBook Book, Account Account) SeedAuthority(
        ApplicationDbContext db, Guid tenantId, bool enabled)
    {
        var account = NewAccount(tenantId, "CASH");
        var book = new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "PRIMARY", Name = "Primary book",
            IsActive = true, AllowsPosting = true
        };
        var mapping = new AccountAccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountId = account.Id,
            AccountingBookId = book.Id, IsEnabled = enabled
        };
        db.AddRange(account, book, mapping);
        return (mapping, book, account);
    }

    private static Account NewAccount(Guid tenantId, string code) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, AccountCode = code, AccountNumber = code,
        AccountName = code, AccountType = AccountType.Asset, CurrencyCode = "GHS",
        Status = AccountStatus.Active
    };

    private static AccountTransaction AddLine(ApplicationDbContext db, Guid tenantId, Account account, string bookCode,
        string journalNumber, int lineNumber, string status, DateTime date)
    {
        var journal = new JournalEntry
        {
            Id = Guid.NewGuid(), TenantId = tenantId, JournalEntryNumber = journalNumber,
            Description = journalNumber, EntryDate = date, FiscalPeriodId = Guid.NewGuid(),
            BookClassification = bookCode, PostingStatus = status
        };
        db.JournalEntries.Add(journal);
        var transaction = new AccountTransaction
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountId = account.Id,
            JournalEntryId = journal.Id, Account = account, JournalEntry = journal,
            FiscalPeriodId = journal.FiscalPeriodId, TransactionDate = date, PostedDate = date,
            DebitAmount = 10m, FunctionalCurrencyCode = "GHS", BookClassification = bookCode,
            TransactionCurrency = "USD", TransactionDebitAmount = 2m,
            ForeignCurrencyAmount = 2m, ExchangeRate = 5m,
            PostingStatus = status, LineNumber = lineNumber
        };
        db.AccountTransactions.Add(transaction);
        return transaction;
    }

    private static FinancePostingEvent NewPostingEvent(Guid tenantId, Guid journalId, string bookCode) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, JournalEntryId = journalId,
        SourceModule = "AP", SourceDocumentType = "Invoice", SourceDocumentId = Guid.NewGuid(),
        PostingAction = "Post", PostingStatus = "Posted", PostingDate = DateTime.UtcNow,
        FunctionalCurrencyCode = "GHS", BookClassification = bookCode
    };
}
