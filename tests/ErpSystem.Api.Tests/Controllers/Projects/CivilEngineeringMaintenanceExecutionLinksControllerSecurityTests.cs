using System.Reflection;
using ErpSystem.Api.Controllers.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Projects;

public sealed class CivilEngineeringMaintenanceExecutionLinksControllerSecurityTests
{
    [Fact]
    public void Controller_reuses_existing_civil_maintenance_workspace_and_audit_permissions()
    {
        var type = typeof(CivilEngineeringMaintenanceExecutionLinksController);
        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetMethod(nameof(CivilEngineeringMaintenanceExecutionLinksController.Lookups))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.WorkspaceRead);
        type.GetMethod(nameof(CivilEngineeringMaintenanceExecutionLinksController.List))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.WorkspaceRead);
        type.GetMethod(nameof(CivilEngineeringMaintenanceExecutionLinksController.Create))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.MaintenanceManage);
        type.GetMethod(nameof(CivilEngineeringMaintenanceExecutionLinksController.Refresh))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.MaintenanceManage);
        type.GetMethod(nameof(CivilEngineeringMaintenanceExecutionLinksController.History))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.AuditRead);
    }
}
