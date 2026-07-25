using ErpSystem.Api.Services.Finance.Cash;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class BankAccountTenantIsolationTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-3")]
    [Trait("Category", "TenantIsolation")]
    public async Task GetAllAsync_ShouldExcludeOtherTenantBankAccounts()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        db.BankAccounts.Add(CreateBankAccount(tenantId, "BANK-001", "Current Account"));
        db.BankAccounts.Add(CreateBankAccount(otherTenantId, "BANK-002", "Other Tenant Account"));
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);

        var accounts = (await service.GetAllAsync()).ToList();

        accounts.Should().ContainSingle();
        accounts[0].AccountNumber.Should().Be("BANK-001");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-3")]
    [Trait("Category", "TenantIsolation")]
    public async Task GetByIdAsync_ShouldReturnNull_ForOtherTenantBankAccount()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var otherTenantAccount = CreateBankAccount(otherTenantId, "BANK-002", "Other Tenant Account");
        await using var db = CreateContext();
        db.BankAccounts.Add(otherTenantAccount);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);

        var account = await service.GetByIdAsync(otherTenantAccount.Id);

        account.Should().BeNull();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-3")]
    [Trait("Category", "TenantIsolation")]
    public async Task CreateAsync_ShouldRejectOtherTenantGlAccount()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var otherTenantGlAccountId = Guid.NewGuid();
        await using var db = CreateContext();
        db.Accounts.Add(new Account
        {
            Id = otherTenantGlAccountId,
            TenantId = otherTenantId,
            AccountCode = "CASH-OTHER",
            AccountNumber = "1000",
            AccountName = "Other Tenant Cash",
            AccountType = AccountType.Asset,
            Status = AccountStatus.Active
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var dto = new CreateBankAccountDto
        {
            AccountNumber = "BANK-001",
            AccountName = "Tenant Bank",
            BankName = "Bank",
            Currency = "GHS",
            AccountType = BankAccountType.Checking,
            GLAccountId = otherTenantGlAccountId,
            OpeningDate = DateTime.UtcNow
        };

        var act = () => service.CreateAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The bank GL account was not found for this tenant.");
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"bank-account-tenant-isolation-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static BankAccountService CreateService(ApplicationDbContext db, Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserName).Returns("finance.test");

        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");

        return new BankAccountService(
            db,
            tenantSettings.Object,
            currentUser.Object);
    }

    private static BankAccount CreateBankAccount(Guid tenantId, string number, string name)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountNumber = number,
            AccountName = name,
            BankName = "Test Bank",
            Currency = "GHS",
            AccountType = BankAccountType.Checking,
            IsActive = true,
            OpeningDate = DateTime.UtcNow
        };
}
