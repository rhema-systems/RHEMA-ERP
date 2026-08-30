using System.Reflection;
using ErpSystem.Api.Controllers.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Projects;

public sealed class CivilEngineeringProjectSupervisionControllerSecurityTests
{
    [Fact]
    public void Controller_is_authenticated_and_every_route_has_an_explicit_permission()
    {
        var type = typeof(CivilEngineeringProjectSupervisionController);
        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any())
            .Select(method => method.GetCustomAttribute<AuthorizeAttribute>()?.Policy)
            .Should().OnlyContain(policy => !string.IsNullOrWhiteSpace(policy));
    }

    [Theory]
    [InlineData(nameof(CivilEngineeringProjectSupervisionController.Lookups), CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(CivilEngineeringProjectSupervisionController.List), CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(CivilEngineeringProjectSupervisionController.Assign), CivilEngineeringAccessControlRegistry.AssignmentsManage)]
    [InlineData(nameof(CivilEngineeringProjectSupervisionController.End), CivilEngineeringAccessControlRegistry.AssignmentsManage)]
    [InlineData(nameof(CivilEngineeringProjectSupervisionController.History), CivilEngineeringAccessControlRegistry.AuditRead)]
    public void Sensitive_routes_use_the_expected_central_permission(string action, string permission)
    {
        typeof(CivilEngineeringProjectSupervisionController).GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(permission);
    }
}
