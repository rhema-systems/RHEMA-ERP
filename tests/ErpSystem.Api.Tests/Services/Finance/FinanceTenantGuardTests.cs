using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using FluentAssertions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceTenantGuardTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-3")]
    [Trait("Category", "TenantIsolation")]
    public void GetRequiredFinanceTenantId_ShouldUseTenantClaim_WhenClaimsArePresent()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = MockCurrentUser(null, new Dictionary<string, string>
        {
            [Constants.Claims.TenantId] = tenantId.ToString(),
            ["sub"] = Guid.NewGuid().ToString()
        });

        var resolvedTenantId = currentUser.Object.GetRequiredFinanceTenantId();

        resolvedTenantId.Should().Be(tenantId);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-3")]
    [Trait("Category", "TenantIsolation")]
    public void GetRequiredFinanceTenantId_ShouldRejectAuthenticatedClaimsWithoutTenantClaim()
    {
        var fallbackTenantId = Guid.NewGuid();
        var currentUser = MockCurrentUser(fallbackTenantId, new Dictionary<string, string>
        {
            ["sub"] = Guid.NewGuid().ToString()
        });

        var act = () => currentUser.Object.GetRequiredFinanceTenantId();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Finance tenant context is required.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-3")]
    [Trait("Category", "TenantIsolation")]
    public void GetRequiredFinanceTenantId_ShouldAllowExplicitTestTenant_WhenNoClaimsArePresent()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = MockCurrentUser(tenantId, new Dictionary<string, string>());

        var resolvedTenantId = currentUser.Object.GetRequiredFinanceTenantId();

        resolvedTenantId.Should().Be(tenantId);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-3")]
    [Trait("Category", "TenantIsolation")]
    public void GetRequiredFinanceTenantId_ShouldRejectEmptyTenant()
    {
        var currentUser = MockCurrentUser(Guid.Empty, new Dictionary<string, string>());

        var act = () => currentUser.Object.GetRequiredFinanceTenantId();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Finance tenant context is required.");
    }

    private static Mock<ICurrentUserService> MockCurrentUser(Guid? tenantId, IDictionary<string, string> claims)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(claims);
        return currentUser;
    }
}
