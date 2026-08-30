using System.Reflection;
using ErpSystem.Api.Controllers.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Projects;

public sealed class CivilEngineeringDevelopmentApprovalFileHandoffsControllerSecurityTests
{
    [Fact]
    public void Controller_uses_existing_permitting_and_workspace_policies()
    {
        var type = typeof(CivilEngineeringDevelopmentApprovalFileHandoffsController);
        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetMethod(nameof(CivilEngineeringDevelopmentApprovalFileHandoffsController.Lookups))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.PermittingManage);
        type.GetMethod(nameof(CivilEngineeringDevelopmentApprovalFileHandoffsController.List))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.WorkspaceRead);
        type.GetMethod(nameof(CivilEngineeringDevelopmentApprovalFileHandoffsController.Create))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.PermittingManage);
    }
}
