using System.Reflection;
using ErpSystem.Api.Controllers.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Projects;

public sealed class CivilEngineeringDirectTasksControllerSecurityTests
{
    [Fact]
    public void Controller_uses_existing_assignment_and_workspace_policies()
    {
        var type = typeof(CivilEngineeringDirectTasksController);
        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetMethod(nameof(CivilEngineeringDirectTasksController.Lookups))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.AssignmentsManage);
        type.GetMethod(nameof(CivilEngineeringDirectTasksController.List))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.WorkspaceRead);
        type.GetMethod(nameof(CivilEngineeringDirectTasksController.Create))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.AssignmentsManage);
        type.GetMethod(nameof(CivilEngineeringDirectTasksController.EscalateUrgent))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.AssignmentsManage);
        type.GetMethod(nameof(CivilEngineeringDirectTasksController.FeedbackLookups))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.AssignedWorkManage);
        type.GetMethod(nameof(CivilEngineeringDirectTasksController.Feedback))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.WorkspaceRead);
        type.GetMethod(nameof(CivilEngineeringDirectTasksController.AssigneeFeedback))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.AssignedWorkManage);
        type.GetMethod(nameof(CivilEngineeringDirectTasksController.ReviewFeedback))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.AssignmentsManage);
    }
}
