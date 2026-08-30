using System.Reflection;
using ErpSystem.Api.Controllers.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Projects;

public sealed class CivilEngineeringDevelopmentApprovalFilesControllerSecurityTests
{
    [Fact]
    public void Controller_uses_existing_Civil_permissions_for_register_write_read_and_audit_history()
    {
        var type = typeof(CivilEngineeringDevelopmentApprovalFilesController);
        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetMethod(nameof(CivilEngineeringDevelopmentApprovalFilesController.Lookups))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.PermittingManage);
        type.GetMethod(nameof(CivilEngineeringDevelopmentApprovalFilesController.List))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.WorkspaceRead);
        type.GetMethod(nameof(CivilEngineeringDevelopmentApprovalFilesController.Create))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.PermittingManage);
        type.GetMethod(nameof(CivilEngineeringDevelopmentApprovalFilesController.RecordSiteInspection))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.PermittingManage);
        type.GetMethod(nameof(CivilEngineeringDevelopmentApprovalFilesController.History))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.AuditRead);
    }
}
