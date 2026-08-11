using System.Reflection;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveyEscalationCalculationSecurityTests
{
    [Theory]
    [InlineData(nameof(QuantitySurveyEscalationCalculationsController.Lookups), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyEscalationCalculationsController.ImpactTargets), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyEscalationCalculationsController.List), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyEscalationCalculationsController.Get), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyEscalationCalculationsController.Calculate), QuantitySurveyAccessControlRegistry.RatesManage)]
    [InlineData(nameof(QuantitySurveyEscalationCalculationsController.Submit), QuantitySurveyAccessControlRegistry.RatesManage)]
    [InlineData(nameof(QuantitySurveyEscalationCalculationsController.ReviewAdjustment), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyEscalationCalculationsController.Approve), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyEscalationCalculationsController.Reject), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyEscalationCalculationsController.History), QuantitySurveyAccessControlRegistry.AuditRead)]
    public void Routes_require_the_expected_central_permission(string action, string permission)
    {
        typeof(QuantitySurveyEscalationCalculationsController)
            .GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!
            .Policy.Should().Be(permission);
    }

    [Fact]
    public void Controller_is_authenticated_and_uses_the_shared_QS_route_family()
    {
        typeof(QuantitySurveyEscalationCalculationsController)
            .GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        typeof(QuantitySurveyEscalationCalculationsController)
            .GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("api/quantity-survey/escalation-calculations");
    }

    [Fact]
    public void Controller_returns_safe_correlation_bearing_problem_details()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "ErpSystem.Api", "Controllers",
            "QuantitySurvey", "QuantitySurveyEscalationCalculationsController.cs"));

        source.Should().Contain("StatusCodes.Status403Forbidden");
        source.Should().Contain("QS escalation calculation access forbidden");
        source.Should().Contain("correlationId");
        source.Should().NotContain("exception.ToString()");
    }

    private static string FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory;
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
