using System.Reflection;
using ErpSystem.Api.Controllers.Projects;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class ProjectBoqQuantitySurveySecurityTests
{
    [Theory]
    [InlineData(nameof(ProjectsController.GetProjectBoqItems), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(ProjectsController.GetProjectBoqClassifications), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(ProjectsController.AddProjectBoqItem), QuantitySurveyAccessControlRegistry.BoqManage)]
    [InlineData(nameof(ProjectsController.UpdateProjectBoqItem), QuantitySurveyAccessControlRegistry.BoqManage)]
    [InlineData(nameof(ProjectsController.DeleteProjectBoqItem), QuantitySurveyAccessControlRegistry.BoqManage)]
    [InlineData(nameof(ProjectsController.GetProjectBoqVersions), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(ProjectsController.GetPublishedProjectBoqVersion), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(ProjectsController.GetProjectBoqVersion), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(ProjectsController.CreateProjectBoqVersion), QuantitySurveyAccessControlRegistry.BoqManage)]
    [InlineData(nameof(ProjectsController.SubmitProjectBoqVersion), QuantitySurveyAccessControlRegistry.BoqManage)]
    [InlineData(nameof(ProjectsController.ApproveProjectBoqVersion), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(ProjectsController.RejectProjectBoqVersion), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(ProjectsController.RecallProjectBoqVersion), QuantitySurveyAccessControlRegistry.BoqManage)]
    [InlineData(nameof(ProjectsController.CompareProjectBoqVersions), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(ProjectsController.GetQuantitySurveyEstimates), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(ProjectsController.GetQuantitySurveyEstimate), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(ProjectsController.CreateQuantitySurveyEstimate), QuantitySurveyAccessControlRegistry.EstimatesManage)]
    [InlineData(nameof(ProjectsController.SubmitQuantitySurveyEstimate), QuantitySurveyAccessControlRegistry.EstimatesManage)]
    [InlineData(nameof(ProjectsController.ApproveQuantitySurveyEstimate), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(ProjectsController.RejectQuantitySurveyEstimate), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public void BoqRoutesRequireTheExpectedCentralQuantitySurveyPermission(string action, string permission)
    {
        typeof(ProjectsController).GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(permission);
    }
}
