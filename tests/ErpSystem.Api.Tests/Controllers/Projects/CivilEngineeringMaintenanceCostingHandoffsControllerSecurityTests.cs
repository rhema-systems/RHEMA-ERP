using System.Reflection;
using ErpSystem.Api.Controllers.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Projects;

public sealed class CivilEngineeringMaintenanceCostingHandoffsControllerSecurityTests
{
    [Fact]
    public void Controller_reuses_existing_civil_maintenance_and_audit_permissions()
    {
        var type = typeof(CivilEngineeringMaintenanceCostingHandoffsController);
        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetMethod(nameof(CivilEngineeringMaintenanceCostingHandoffsController.Lookups))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.WorkspaceRead);
        type.GetMethod(nameof(CivilEngineeringMaintenanceCostingHandoffsController.List))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.WorkspaceRead);
        type.GetMethod(nameof(CivilEngineeringMaintenanceCostingHandoffsController.Create))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.MaintenanceManage);
        type.GetMethod(nameof(CivilEngineeringMaintenanceCostingHandoffsController.Act))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.MaintenanceManage);
        type.GetMethod(nameof(CivilEngineeringMaintenanceCostingHandoffsController.History))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.AuditRead);
    }
}
