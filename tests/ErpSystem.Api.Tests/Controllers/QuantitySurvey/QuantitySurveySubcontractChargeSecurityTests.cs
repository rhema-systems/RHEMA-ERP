using System.Reflection;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveySubcontractChargeSecurityTests
{
    [Theory]
    [InlineData(nameof(QuantitySurveySubcontractChargesController.Get), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveySubcontractChargesController.Save), QuantitySurveyAccessControlRegistry.CertificatesManage)]
    [InlineData(nameof(QuantitySurveySubcontractChargesController.Issue), QuantitySurveyAccessControlRegistry.CertificatesManage)]
    [InlineData(nameof(QuantitySurveySubcontractChargesController.Submit), QuantitySurveyAccessControlRegistry.CertificatesManage)]
    [InlineData(nameof(QuantitySurveySubcontractChargesController.Approve), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveySubcontractChargesController.Reject), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveySubcontractChargesController.History), QuantitySurveyAccessControlRegistry.AuditRead)]
    public void Internal_routes_require_shared_QS_permissions(string action, string permission) =>
        typeof(QuantitySurveySubcontractChargesController).GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(permission);

    [Fact]
    public void External_routes_are_authenticated_and_unexpected_errors_reach_central_middleware()
    {
        typeof(ExternalProjectSubcontractChargesController)
            .GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        typeof(ExternalProjectSubcontractChargesController)
            .GetCustomAttribute<RouteAttribute>()!.Template.Should()
            .Be("api/projects/external/my-projects/{projectId:guid}/subcontracts/{subcontractId:guid}/charge-notices");

        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "ErpSystem.Api",
            "Controllers", "QuantitySurvey", "QuantitySurveySubcontractChargesController.cs"));
        source.Should().Contain("correlationId").And.NotContain("catch (Exception");
    }

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
