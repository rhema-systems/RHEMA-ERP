using System.Reflection;
using ErpSystem.Api.Controllers.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Projects;

public sealed class CivilEngineeringConfigurationControllerSecurityTests
{
    [Fact]
    public void ControllerIsAuthenticatedAndEveryHttpActionHasAnExplicitPermissionPolicy()
    {
        var type = typeof(CivilEngineeringConfigurationProfilesController);
        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();

        var actions = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any())
            .ToArray();

        actions.Should().NotBeEmpty();
        actions.Select(method => method.GetCustomAttribute<AuthorizeAttribute>()?.Policy)
            .Should().OnlyContain(policy => !string.IsNullOrWhiteSpace(policy));
    }

    [Theory]
    [InlineData(nameof(CivilEngineeringConfigurationProfilesController.Get), CivilEngineeringAccessControlRegistry.ConfigurationRead)]
    [InlineData(nameof(CivilEngineeringConfigurationProfilesController.Create), CivilEngineeringAccessControlRegistry.ConfigurationManage)]
    [InlineData(nameof(CivilEngineeringConfigurationProfilesController.Approve), CivilEngineeringAccessControlRegistry.ConfigurationApprove)]
    [InlineData(nameof(CivilEngineeringConfigurationProfilesController.Publish), CivilEngineeringAccessControlRegistry.ConfigurationApprove)]
    [InlineData(nameof(CivilEngineeringConfigurationProfilesController.History), CivilEngineeringAccessControlRegistry.AuditRead)]
    public void SensitiveRoutesUseTheExpectedCentralPermission(string action, string permission)
    {
        typeof(CivilEngineeringConfigurationProfilesController).GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(permission);
    }
}
