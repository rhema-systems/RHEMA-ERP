using System.Reflection;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveyEscalationFormulaSecurityTests
{
    [Theory]
    [InlineData(nameof(QuantitySurveyEscalationFormulasController.Lookups), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyEscalationFormulasController.IndexFamilies), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyEscalationFormulasController.CreateIndexFamily), QuantitySurveyAccessControlRegistry.RatesManage)]
    [InlineData(nameof(QuantitySurveyEscalationFormulasController.UpdateIndexFamily), QuantitySurveyAccessControlRegistry.RatesManage)]
    [InlineData(nameof(QuantitySurveyEscalationFormulasController.List), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyEscalationFormulasController.Get), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyEscalationFormulasController.Create), QuantitySurveyAccessControlRegistry.RatesManage)]
    [InlineData(nameof(QuantitySurveyEscalationFormulasController.Update), QuantitySurveyAccessControlRegistry.RatesManage)]
    [InlineData(nameof(QuantitySurveyEscalationFormulasController.Submit), QuantitySurveyAccessControlRegistry.RatesManage)]
    [InlineData(nameof(QuantitySurveyEscalationFormulasController.Approve), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyEscalationFormulasController.Reject), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyEscalationFormulasController.Retire), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyEscalationFormulasController.History), QuantitySurveyAccessControlRegistry.AuditRead)]
    public void Routes_require_the_expected_central_permission(string action, string permission)
    {
        typeof(QuantitySurveyEscalationFormulasController)
            .GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!
            .Policy.Should().Be(permission);
    }

    [Fact]
    public void Controller_is_authenticated_and_uses_the_shared_QS_route_family()
    {
        typeof(QuantitySurveyEscalationFormulasController)
            .GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        typeof(QuantitySurveyEscalationFormulasController)
            .GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("api/quantity-survey/escalation-formulas");
    }

    [Fact]
    public void Controller_returns_safe_problem_details_for_service_authorization_failures()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ErpSystem.Api",
            "Controllers",
            "QuantitySurvey",
            "QuantitySurveyEscalationFormulasController.cs"));

        source.Should().Contain("StatusCodes.Status403Forbidden");
        source.Should().Contain("QS escalation access forbidden");
        source.Should().Contain("correlationId");
    }

    private static string FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory;
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
