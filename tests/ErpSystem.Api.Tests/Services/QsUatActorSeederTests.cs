using ErpSystem.Api.Services;
using ErpSystem.Core.Entities;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public sealed class QsUatActorSeederTests
{
    [Theory]
    [InlineData("RhemaERP_VpsTest_123", "RhemaERP_VpsTest_123", "Test", true, true)]
    [InlineData("RhemaERP_QsUatVerify_123", "RhemaERP_QsUatVerify_123", "Development", true, true)]
    [InlineData("RhemaERP_VpsTest_123", "RhemaERP_VpsTest_123", "Production", true, false)]
    [InlineData("RhemaERP_VpsTest_123", "RhemaERP_VpsTest_123", "Test", false, false)]
    [InlineData("RhemaERP_VpsTest_123", "RhemaERP_VpsTest_456", "Test", true, false)]
    [InlineData("RhemaERP", "RhemaERP", "Test", true, false)]
    [InlineData("master", "master", "Test", true, false)]
    public void TargetGuard_RequiresExactOptedInTestDatabase(string actual, string expected,
        string environment, bool enabled, bool allowed)
    {
        Action action = () => QsUatActorSeeder.ValidateTarget(actual, expected, environment, enabled);
        if (allowed) action.Should().NotThrow(); else action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SeedIdentity_IsUninitializedUntilBoundAndCannotSwitchTenant()
    {
        var context = new QsUatSeedContext();
        context.IsAuthenticated.Should().BeFalse();
        context.Roles.Should().BeEmpty();
        var tenant = new Tenant { Id = Guid.NewGuid(), Code = "DEFAULT", Name = "Test" };
        context.Initialize(tenant, Guid.NewGuid());
        context.IsAuthenticated.Should().BeTrue();
        context.UserName.Should().Be("qs.uat.bootstrap");
        context.FullName.Should().Be("QS UAT bootstrap");
        Action changeTenant = () => context.SetCurrentTenant(Guid.NewGuid());
        changeTenant.Should().Throw<InvalidOperationException>();
        Action initializeAgain = () => context.Initialize(tenant, Guid.NewGuid());
        initializeAgain.Should().Throw<InvalidOperationException>();
    }
}
