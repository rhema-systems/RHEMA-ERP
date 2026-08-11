using System.Reflection;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveyMaterialReconciliationSecurityTests
{
    [Theory]
    [InlineData(nameof(QuantitySurveyMaterialReconciliationsController.Workspace), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyMaterialReconciliationsController.Get), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyMaterialReconciliationsController.Save), QuantitySurveyAccessControlRegistry.ValuationsManage)]
    [InlineData(nameof(QuantitySurveyMaterialReconciliationsController.Submit), QuantitySurveyAccessControlRegistry.ValuationsManage)]
    [InlineData(nameof(QuantitySurveyMaterialReconciliationsController.Approve), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyMaterialReconciliationsController.Reject), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyMaterialReconciliationsController.History), QuantitySurveyAccessControlRegistry.AuditRead)]
    public void Internal_routes_require_the_expected_shared_permission(string action, string permission)
    {
        typeof(QuantitySurveyMaterialReconciliationsController).GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(permission);
    }

    [Fact]
    public void Internal_and_external_controllers_are_authenticated_and_return_safe_problem_details()
    {
        typeof(QuantitySurveyMaterialReconciliationsController)
            .GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        typeof(ExternalProjectMaterialReconciliationsController)
            .GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        typeof(ExternalProjectMaterialReconciliationsController)
            .GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("api/projects/external/my-projects/{projectId:guid}/material-reconciliations");

        var controller = Source("src", "ErpSystem.Api", "Controllers", "QuantitySurvey",
            "QuantitySurveyMaterialReconciliationsController.cs");
        var service = Source("src", "ErpSystem.Api", "Services", "QuantitySurvey",
            "QuantitySurveyMaterialReconciliationService.cs");
        controller.Should().Contain("correlationId")
            .And.Contain("ProblemDetails")
            .And.NotContain("catch (Exception");
        service.Should().Contain("value.TenantId == TenantId")
            .And.Contain("RequireExternalProjectAsync")
            .And.Contain("BusinessPartnerUsers")
            .And.Contain("ExternalPortalAccessEnabled")
            .And.NotContain("exception.ToString()");
    }

    private static string Source(params string[] path) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), Path.Combine(path)));

    private static string FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        foreach (var start in new[] { Environment.CurrentDirectory, Path.GetDirectoryName(sourceFile) })
            for (var directory = string.IsNullOrWhiteSpace(start) ? null : new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
