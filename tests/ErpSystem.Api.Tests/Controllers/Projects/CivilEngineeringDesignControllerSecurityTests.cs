using System.Reflection;
using ErpSystem.Api.Controllers.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Projects;

public sealed class CivilEngineeringDesignControllerSecurityTests
{
    [Fact]
    public void Controller_is_authenticated_and_every_route_has_an_explicit_permission()
    {
        var type = typeof(CivilEngineeringDesignCasesController);
        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        var actions = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any())
            .ToArray();

        actions.Should().NotBeEmpty();
        actions.Select(method => method.GetCustomAttribute<AuthorizeAttribute>()?.Policy)
            .Should().OnlyContain(policy => !string.IsNullOrWhiteSpace(policy));
    }

    [Theory]
    [InlineData(nameof(CivilEngineeringDesignCasesController.Get), CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.CommercialReadiness), CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.Create), CivilEngineeringAccessControlRegistry.DesignManage)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.Transition), CivilEngineeringAccessControlRegistry.DesignManage)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.Decide), CivilEngineeringAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.History), CivilEngineeringAccessControlRegistry.AuditRead)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.ListReconnaissance), CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.CreateReconnaissance), CivilEngineeringAccessControlRegistry.DesignManage)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.UpdateReconnaissance), CivilEngineeringAccessControlRegistry.DesignManage)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.CompleteReconnaissance), CivilEngineeringAccessControlRegistry.DesignManage)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.ListInformationRequests), CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.ListAssignedInformationRequests), CivilEngineeringAccessControlRegistry.DesignInputRespond)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.CreateInformationRequest), CivilEngineeringAccessControlRegistry.DesignManage)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.SubmitInformationResponse), CivilEngineeringAccessControlRegistry.DesignInputRespond)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.ReviewInformationResponse), CivilEngineeringAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.DocumentLookups), CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.ListDocuments), CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.CreateDocument), CivilEngineeringAccessControlRegistry.DocumentsManage)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.SubmitDocument), CivilEngineeringAccessControlRegistry.DocumentsManage)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.ReviewDocument), CivilEngineeringAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.PlanningGisLookups), CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.ListPlanningGis), CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.CreatePlanningGis), CivilEngineeringAccessControlRegistry.PermittingManage)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.SubmitPlanningGis), CivilEngineeringAccessControlRegistry.PermittingManage)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.DecidePlanningGis), CivilEngineeringAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(CivilEngineeringDesignCasesController.PlanningGisHistory), CivilEngineeringAccessControlRegistry.AuditRead)]
    public void Sensitive_routes_use_the_expected_central_permission(string action, string permission)
    {
        typeof(CivilEngineeringDesignCasesController).GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(permission);
    }
}
