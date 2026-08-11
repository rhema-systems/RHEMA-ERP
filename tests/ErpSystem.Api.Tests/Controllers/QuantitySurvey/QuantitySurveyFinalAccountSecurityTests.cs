using System.Reflection;
using ErpSystem.Api.Controllers.Projects;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveyFinalAccountSecurityTests
{
    [Theory]
    [InlineData(nameof(QuantitySurveyFinalAccountsController.Workspace), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyFinalAccountsController.Prepare), QuantitySurveyAccessControlRegistry.FinalAccountsManage)]
    [InlineData(nameof(QuantitySurveyFinalAccountsController.Submit), QuantitySurveyAccessControlRegistry.FinalAccountsManage)]
    [InlineData(nameof(QuantitySurveyFinalAccountsController.Approve), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyFinalAccountsController.Reject), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyFinalAccountsController.Close), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyFinalAccountsController.History), QuantitySurveyAccessControlRegistry.AuditRead)]
    public void Routes_require_the_expected_shared_permissions(string action, string permission)
    {
        typeof(QuantitySurveyFinalAccountsController).GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(permission);
    }

    [Fact]
    public void Controller_is_authenticated_and_returns_bounded_problem_details()
    {
        typeof(QuantitySurveyFinalAccountsController)
            .GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        typeof(QuantitySurveyFinalAccountsController)
            .GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("api/quantity-survey/final-accounts");

        var controller = Source("src", "ErpSystem.Api", "Controllers", "QuantitySurvey",
            "QuantitySurveyFinalAccountsController.cs");
        var service = Source("src", "ErpSystem.Api", "Services", "QuantitySurvey",
            "QuantitySurveyFinalAccountService.cs");
        controller.Should().Contain("correlationId")
            .And.Contain("ProblemDetails")
            .And.NotContain("catch (Exception");
        service.Should().Contain("value.TenantId == TenantId")
            .And.Contain("IsolationLevel.Serializable")
            .And.Contain("IVendorInvoiceService")
            .And.Contain("QuantitySurveyAuditEventMap")
            .And.Contain("QS-DEC-012")
            .And.NotContain("exception.ToString()");
    }

    [Fact]
    public void Legacy_projects_write_route_is_locked_and_the_service_rejects_direct_mutation()
    {
        typeof(ProjectsController).GetMethod(nameof(ProjectsController.UpsertProjectFinalAccount))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy
            .Should().Be(QuantitySurveyAccessControlRegistry.FinalAccountsManage);

        Source("src", "ErpSystem.Core", "Services", "Projects",
                "ProjectService.CommercialAdministration.cs")
            .Should().Contain("Use the governed Quantity Survey final-account workspace")
            .And.Contain("InvalidOperationException");
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
