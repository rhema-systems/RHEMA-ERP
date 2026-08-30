using System.Reflection;
using ErpSystem.Api.Controllers.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Projects;

public sealed class CivilEngineeringMaintenanceIntakesControllerSecurityTests
{
    [Fact]
    public void Controller_is_authenticated_and_mutation_uses_existing_maintenance_permission()
    {
        var type = typeof(CivilEngineeringMaintenanceIntakesController);
        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetMethod(nameof(CivilEngineeringMaintenanceIntakesController.Lookups))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.MaintenanceManage);
        type.GetMethod(nameof(CivilEngineeringMaintenanceIntakesController.List))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.WorkspaceRead);
        type.GetMethod(nameof(CivilEngineeringMaintenanceIntakesController.Create))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.MaintenanceManage);
        type.GetMethod(nameof(CivilEngineeringMaintenanceIntakesController.History))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.WorkspaceRead);
    }
}
