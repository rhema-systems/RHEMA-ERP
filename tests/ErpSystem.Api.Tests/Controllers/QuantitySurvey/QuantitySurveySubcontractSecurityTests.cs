using System.Reflection;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveySubcontractSecurityTests
{
    [Theory]
    [InlineData(nameof(QuantitySurveySubcontractsController.Workspace), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveySubcontractsController.Save), QuantitySurveyAccessControlRegistry.FinalAccountsManage)]
    [InlineData(nameof(QuantitySurveySubcontractsController.Submit), QuantitySurveyAccessControlRegistry.FinalAccountsManage)]
    [InlineData(nameof(QuantitySurveySubcontractsController.Approve), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveySubcontractsController.SaveValuation), QuantitySurveyAccessControlRegistry.CertificatesManage)]
    [InlineData(nameof(QuantitySurveySubcontractsController.Assess), QuantitySurveyAccessControlRegistry.CertificatesManage)]
    [InlineData(nameof(QuantitySurveySubcontractsController.ApproveValuation), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveySubcontractsController.Handoff), QuantitySurveyAccessControlRegistry.CertificatesManage)]
    [InlineData(nameof(QuantitySurveySubcontractsController.RefreshPayment), QuantitySurveyAccessControlRegistry.CertificatesManage)]
    [InlineData(nameof(QuantitySurveySubcontractsController.Close), QuantitySurveyAccessControlRegistry.FinalAccountsManage)]
    [InlineData(nameof(QuantitySurveySubcontractsController.History), QuantitySurveyAccessControlRegistry.AuditRead)]
    public void Internal_routes_require_the_expected_shared_permission(string action, string permission) =>
        typeof(QuantitySurveySubcontractsController).GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(permission);

    [Fact]
    public void External_portal_is_authenticated_and_controller_errors_are_bounded()
    {
        typeof(ExternalProjectSubcontractsController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        typeof(ExternalProjectSubcontractsController).GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("api/projects/external/my-projects/{projectId:guid}/subcontracts");

        var source = Source("src", "ErpSystem.Api", "Controllers", "QuantitySurvey",
            "QuantitySurveySubcontractsController.cs");
        source.Should().Contain("ProblemDetails")
            .And.Contain("correlationId")
            .And.NotContain("catch (Exception");
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
