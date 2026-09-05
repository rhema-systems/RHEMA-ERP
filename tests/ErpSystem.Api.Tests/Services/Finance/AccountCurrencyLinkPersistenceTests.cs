using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public class AccountCurrencyLinkPersistenceTests
{
    [Fact]
    public async Task CurrencyLinkInquiry_DoesNotExposeStaleBalances_ExactBookExposureIsAuthoritative()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var account = new Account
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountCode = "1000-USD", AccountNumber = "1000-USD",
            AccountName = "USD cash", AccountType = AccountType.Asset, CurrencyCode = "GHS",
            IsMultiCurrency = true, Status = AccountStatus.Active
        };
        var book = new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "IFRS", Name = "IFRS",
            IsDefault = true, IsActive = true, AllowsPosting = true
        };
        var year = new FiscalYear
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Year = 2026, FiscalYearName = "2026",
            FiscalYearCode = "FY2026", StartDate = new(2026, 1, 1), EndDate = new(2026, 12, 31)
        };
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FiscalYearId = year.Id, FiscalYear = year,
            PeriodName = "Sep", PeriodCode = "2026-09", PeriodNumber = 9,
            StartDate = new(2026, 9, 1), EndDate = new(2026, 9, 30), IsOpen = true
        };
        var link = new AccountCurrencyLink
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountId = account.Id, LinkedCurrencyCode = "USD",
            IsActive = true, ForeignCurrencyBalance = 999m, BaseCurrencyEquivalent = 9999m
        };
        db.AddRange(new Tenant { Id = tenantId, Code = "FX", Name = "FX", BaseCurrency = "GHS" },
            account, book, year, period, link,
            new AccountAccountingBook
            {
                Id = Guid.NewGuid(), TenantId = tenantId, AccountId = account.Id,
                AccountingBookId = book.Id, IsEnabled = true
            });
        await db.SaveChangesAsync();
        var line = new AccountTransaction
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountId = account.Id, JournalEntryId = Guid.NewGuid(),
            AccountingBookId = book.Id, BookClassification = book.Code, FiscalPeriodId = period.Id,
            FiscalPeriod = period, PostingStatus = "Posted", TransactionDate = new(2026, 9, 5),
            FunctionalCurrencyCode = "GHS", TransactionCurrency = "USD", DebitAmount = 100m,
            TransactionDebitAmount = 10m, LineNumber = 1
        };
        var balances = new BookBalanceReadModelService(db);
        await balances.ApplyPostingAsync(tenantId, book.Id, book.Code, period.Id, "GHS",
            new[] { line }, true, DateTime.UtcNow, null);
        await db.SaveChangesAsync();

        using var unit = new UnitOfWork(db);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.TenantId).Returns(tenantId);
        currentUser.SetupGet(user => user.UserName).Returns("finance.test");
        var accounts = new AccountService(unit, currentUser.Object, Mock.Of<IAccountingBookService>(),
            Mock.Of<ILogger<AccountService>>());
        var config = (await accounts.GetAccountCurrencyLinksAsync(account.Id)).Single();

#pragma warning disable CS0618
        config.CurrentBalance.Should().BeNull();
        config.CurrentBalanceBaseCurrency.Should().BeNull();
        config.ForeignCurrencyBalance.Should().BeNull();
        config.BaseCurrencyBalance.Should().BeNull();
#pragma warning restore CS0618
        config.HasAuthoritativeCurrentBalance.Should().BeFalse();
        config.CurrentBalanceAuthority.Should().Be("ExactBookExposureRequired");
        var inquiry = await balances.GetAsync(tenantId, account.Id, book.Code);
        inquiry.Exposures.Single().SignedForeignBalance.Should().Be(10m);
        inquiry.Exposures.Single().SignedFunctionalBalance.Should().Be(100m);
    }

    [Fact]
    public async Task AddCurrencyLinkAsync_PersistsNewApplicationIdentifiedLink()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = "1000-USD",
            AccountNumber = "1000-USD",
            AccountName = "Multi-currency cash",
            AccountType = AccountType.Asset,
            CurrencyCode = "GHS",
            IsMultiCurrency = true,
            Status = AccountStatus.Active,
            AllowDirectPosting = true
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        using var unitOfWork = new UnitOfWork(db);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.TenantId).Returns(tenantId);
        currentUser.SetupGet(user => user.UserName).Returns("finance.test");
        currentUser.SetupGet(user => user.UserId).Returns(Guid.NewGuid().ToString());

        var service = new AccountService(
            unitOfWork,
            currentUser.Object,
            Mock.Of<IAccountingBookService>(),
            Mock.Of<ILogger<AccountService>>());

        var result = await service.AddCurrencyLinkAsync(new AddCurrencyLinkDto
        {
            AccountId = account.Id,
            LinkedCurrencyCode = "USD",
            RevaluationFrequency = "Monthly",
            TransactionRateType = "Daily",
            TransactionQuoteSide = "Mid",
            RevaluationRateType = "Month End",
            RevaluationQuoteSide = "Mid"
        });

        result.LinkedCurrencyCode.Should().Be("USD");
        var persisted = await db.AccountCurrencyLinks.SingleAsync();
        persisted.Id.Should().Be(result.Id);
        persisted.AccountId.Should().Be(account.Id);
        persisted.TenantId.Should().Be(tenantId);
        persisted.LinkedCurrencyCode.Should().Be("USD");
    }

    [Theory]
    [InlineData("Average")]
    [InlineData("Spot")]
    [InlineData("Budget")]
    public async Task AddCurrencyLinkAsync_RejectsNonGovernedTransactionRateTypes(string rateType)
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = "1010-FX",
            AccountNumber = "1010-FX",
            AccountName = "Foreign currency cash",
            AccountType = AccountType.Asset,
            CurrencyCode = "GHS",
            IsMultiCurrency = true,
            Status = AccountStatus.Active,
            AllowDirectPosting = true
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        using var unitOfWork = new UnitOfWork(db);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.TenantId).Returns(tenantId);
        currentUser.SetupGet(user => user.UserName).Returns("finance.test");
        var service = new AccountService(
            unitOfWork,
            currentUser.Object,
            Mock.Of<IAccountingBookService>(),
            Mock.Of<ILogger<AccountService>>());

        var action = () => service.AddCurrencyLinkAsync(new AddCurrencyLinkDto
        {
            AccountId = account.Id,
            LinkedCurrencyCode = "USD",
            TransactionRateType = rateType,
            RevaluationRateType = "MonthEnd"
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Daily or Fixed*");
        (await db.AccountCurrencyLinks.CountAsync()).Should().Be(0);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"account-currency-link-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }
}
