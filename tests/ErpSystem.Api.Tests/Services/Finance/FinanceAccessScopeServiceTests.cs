using ErpSystem.Api.Services.Finance.Security;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

/// <summary>
/// Regression tests for FIN-LIM-0014. Identity permissions are tested elsewhere; these tests
/// specifically prove that Finance data scope is opt-in, fail-closed once enabled, level-aware,
/// and recoverable by tenant administrators.
/// </summary>
public sealed class FinanceAccessScopeServiceTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-AccessScopes")]
    public async Task EnforcementDisabled_ShouldPreserveTenantWideFinanceAccess()
    {
        var fixture = await CreateFixtureAsync(enforcementEnabled: false);

        var permitted = await fixture.Service.GetPermittedBankAccountIdsAsync(FinanceAccessLevel.Approve);
        var ensure = () => fixture.Service.EnsureBankAccountAccessAsync(
            fixture.BankAccountId,
            FinanceAccessLevel.Approve);

        permitted.Should().BeNull("null is the service contract for unrestricted tenant access");
        await ensure.Should().NotThrowAsync();
        await fixture.Context.DisposeAsync();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-AccessScopes")]
    public async Task EnforcementEnabledWithoutGrant_ShouldFailClosed()
    {
        var fixture = await CreateFixtureAsync(enforcementEnabled: true);

        var permitted = await fixture.Service.GetPermittedBankAccountIdsAsync(FinanceAccessLevel.Read);
        var ensure = () => fixture.Service.EnsureBankAccountAccessAsync(
            fixture.BankAccountId,
            FinanceAccessLevel.Read);

        permitted.Should().NotBeNull().And.BeEmpty();
        await ensure.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*does not have the required Finance scope*");
        await fixture.Context.DisposeAsync();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-AccessScopes")]
    public async Task BankGrant_ShouldPermitOnlyItsBankAndAssignedAccessLevel()
    {
        var fixture = await CreateFixtureAsync(enforcementEnabled: true);
        fixture.Context.Set<FinanceAccessScopeGrant>().Add(new FinanceAccessScopeGrant
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            UserId = fixture.UserId,
            ScopeType = FinanceAccessScopeType.BankAccount,
            ScopeValue = fixture.BankAccountId.ToString("N"),
            AccessLevel = FinanceAccessLevel.Operate,
            EffectiveFrom = DateTime.UtcNow.AddDays(-1),
            IsActive = true,
            Reason = "Assign AP payment operations for the main collection bank."
        });
        await fixture.Context.SaveChangesAsync();

        var operate = () => fixture.Service.EnsureBankAccountAccessAsync(
            fixture.BankAccountId,
            FinanceAccessLevel.Operate);
        var otherBank = () => fixture.Service.EnsureBankAccountAccessAsync(
            Guid.NewGuid(),
            FinanceAccessLevel.Operate);
        var approve = () => fixture.Service.EnsureBankAccountAccessAsync(
            fixture.BankAccountId,
            FinanceAccessLevel.Approve);
        var mappedGrant = (await fixture.Service.GetGrantsAsync(fixture.UserId)).Single();

        await operate.Should().NotThrowAsync();
        await otherBank.Should().ThrowAsync<UnauthorizedAccessException>();
        await approve.Should().ThrowAsync<UnauthorizedAccessException>();
        mappedGrant.ScopeValue.Should().Be(
            fixture.BankAccountId.ToString(),
            "the API should expose a standard dashed GUID even though persistence uses canonical N form");
        await fixture.Context.DisposeAsync();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-AccessScopes")]
    public async Task TenantAdministrator_ShouldRetainRecoveryAccessWhenScopesAreEnabled()
    {
        var fixture = await CreateFixtureAsync(
            enforcementEnabled: true,
            roles: new[] { Constants.Roles.TenantAdmin });

        var permitted = await fixture.Service.GetPermittedBankAccountIdsAsync(FinanceAccessLevel.Administer);
        var ensure = () => fixture.Service.EnsureBankAccountAccessAsync(
            fixture.BankAccountId,
            FinanceAccessLevel.Administer);

        permitted.Should().BeNull();
        await ensure.Should().NotThrowAsync();
        await fixture.Context.DisposeAsync();
    }

    private static async Task<ScopeFixture> CreateFixtureAsync(
        bool enforcementEnabled,
        IReadOnlyCollection<string>? roles = null)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var bankAccountId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"finance-access-scopes-{Guid.NewGuid():N}")
            .Options;
        var context = new ApplicationDbContext(options);
        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Tema Development Corporation",
            Code = "TDC",
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });
        // Grant queries include the assigned user so the administration DTO can display identity
        // evidence. Seed that required relationship instead of relying on the in-memory provider's
        // relaxed foreign-key behavior used by the authorization-only assertions.
        context.Users.Add(new ApplicationUser
        {
            Id = userId,
            TenantId = tenantId,
            UserName = "finance.scope.test",
            FirstName = "Finance",
            LastName = "Scope Test",
            IsActive = true
        });
        context.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            EnforceFinanceAccessScopes = enforcementEnabled
        });
        context.BankAccounts.Add(new BankAccount
        {
            Id = bankAccountId,
            TenantId = tenantId,
            AccountNumber = "TDC-OPERATING-001",
            AccountName = "TDC Main Operating Account",
            BankName = "Test Bank",
            Currency = "GHS",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(userId.ToString());
        currentUser.SetupGet(item => item.UserName).Returns("finance.scope.test");
        currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
        currentUser.SetupGet(item => item.Roles).Returns(roles ?? Array.Empty<string>());
        currentUser.SetupGet(item => item.Claims).Returns(new Dictionary<string, string>());

        var service = new FinanceAccessScopeService(
            context,
            currentUser.Object,
            Mock.Of<ILogger<FinanceAccessScopeService>>());
        return new ScopeFixture(context, service, tenantId, userId, bankAccountId);
    }

    private sealed record ScopeFixture(
        ApplicationDbContext Context,
        FinanceAccessScopeService Service,
        Guid TenantId,
        Guid UserId,
        Guid BankAccountId);
}
