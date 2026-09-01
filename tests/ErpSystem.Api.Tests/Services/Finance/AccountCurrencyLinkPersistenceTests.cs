using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
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
            RevaluationRequired = true,
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
