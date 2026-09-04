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

    private static void AddLine(ApplicationDbContext db, Guid tenantId, Account account, string bookCode,
        string journalNumber, int lineNumber, string status, DateTime date)
    {
        var journal = new JournalEntry
        {
            Id = Guid.NewGuid(), TenantId = tenantId, JournalEntryNumber = journalNumber,
            Description = journalNumber, EntryDate = date, FiscalPeriodId = Guid.NewGuid(),
            BookClassification = bookCode, PostingStatus = status
        };
        db.JournalEntries.Add(journal);
        db.AccountTransactions.Add(new AccountTransaction
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountId = account.Id,
            JournalEntryId = journal.Id, Account = account, JournalEntry = journal,
            FiscalPeriodId = journal.FiscalPeriodId, TransactionDate = date, PostedDate = date,
            DebitAmount = 10m, FunctionalCurrencyCode = "GHS", BookClassification = bookCode,
            PostingStatus = status, LineNumber = lineNumber
        });
    }
}
