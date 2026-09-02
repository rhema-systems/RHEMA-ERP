using System.Reflection;
using ErpSystem.Api.Controllers.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Projects;

public sealed class CivilEngineeringPermittingHodDecisionsControllerSecurityTests
{
    [Fact]
    public void Controller_requires_the_existing_permitting_management_policy()
    {
        var type = typeof(CivilEngineeringPermittingHodDecisionsController);
        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetMethod(nameof(CivilEngineeringPermittingHodDecisionsController.Pending))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.PermittingManage);
        type.GetMethod(nameof(CivilEngineeringPermittingHodDecisionsController.Decide))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.PermittingManage);
    }
}
