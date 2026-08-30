using System.Reflection;
using ErpSystem.Api.Controllers.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Projects;

public sealed class CivilEngineeringPermittingEngineeringReviewsControllerSecurityTests
{
    [Fact]
    public void Controller_uses_existing_permitting_and_workspace_policies()
    {
        var type = typeof(CivilEngineeringPermittingEngineeringReviewsController);
        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetMethod(nameof(CivilEngineeringPermittingEngineeringReviewsController.Lookups))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.PermittingManage);
        type.GetMethod(nameof(CivilEngineeringPermittingEngineeringReviewsController.List))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.WorkspaceRead);
        type.GetMethod(nameof(CivilEngineeringPermittingEngineeringReviewsController.Submit))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.PermittingManage);
    }
}
