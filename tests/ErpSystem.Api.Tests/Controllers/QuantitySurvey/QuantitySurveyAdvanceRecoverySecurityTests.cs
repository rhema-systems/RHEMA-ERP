using System.Reflection;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveyAdvanceRecoverySecurityTests
{
    [Theory]
    [InlineData(nameof(QuantitySurveyAdvanceRecoveriesController.Workspace), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyAdvanceRecoveriesController.Create), QuantitySurveyAccessControlRegistry.ValuationsManage)]
    [InlineData(nameof(QuantitySurveyAdvanceRecoveriesController.Submit), QuantitySurveyAccessControlRegistry.ValuationsManage)]
    [InlineData(nameof(QuantitySurveyAdvanceRecoveriesController.Approve), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyAdvanceRecoveriesController.Reject), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyAdvanceRecoveriesController.History), QuantitySurveyAccessControlRegistry.AuditRead)]
    public void Routes_require_the_expected_shared_permission(string action, string permission)
    {
        typeof(QuantitySurveyAdvanceRecoveriesController).GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(permission);
    }

    [Fact]
    public void Controller_is_authenticated_tenant_safe_and_returns_bounded_problem_details()
    {
        typeof(QuantitySurveyAdvanceRecoveriesController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        typeof(QuantitySurveyAdvanceRecoveriesController).GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("api/quantity-survey/advance-recoveries");

        var controller = Source("src", "ErpSystem.Api", "Controllers", "QuantitySurvey",
            "QuantitySurveyAdvanceRecoveriesController.cs");
        var service = Source("src", "ErpSystem.Api", "Services", "QuantitySurvey",
            "QuantitySurveyAdvanceRecoveryService.cs");
        controller.Should().Contain("correlationId")
            .And.Contain("ProblemDetails")
            .And.NotContain("catch (Exception");
        service.Should().Contain("value.TenantId == TenantId")
            .And.Contain("IsolationLevel.Serializable")
            .And.Contain("IVendorPaymentService")
            .And.Contain("QuantitySurveyAuditEventMap")
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
