using System.Reflection;
using ErpSystem.Api.Controllers.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Projects;

public sealed class CivilEngineeringMaintenanceAssessmentsControllerSecurityTests
{
    [Fact]
    public void Controller_reuses_existing_maintenance_and_audit_permissions()
    {
        var type = typeof(CivilEngineeringMaintenanceAssessmentsController);
        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetMethod(nameof(CivilEngineeringMaintenanceAssessmentsController.Lookups))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.WorkspaceRead);
        type.GetMethod(nameof(CivilEngineeringMaintenanceAssessmentsController.List))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.WorkspaceRead);
        type.GetMethod(nameof(CivilEngineeringMaintenanceAssessmentsController.Start))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.MaintenanceManage);
        type.GetMethod(nameof(CivilEngineeringMaintenanceAssessmentsController.Transition))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.MaintenanceManage);
        type.GetMethod(nameof(CivilEngineeringMaintenanceAssessmentsController.History))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.AuditRead);
    }
}
