using System.Reflection;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveyDayworkSecurityTests
{
    [Theory]
    [InlineData(nameof(QuantitySurveyDayworksController.Workspace), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyDayworksController.Evidence), QuantitySurveyAccessControlRegistry.VariationsManage)]
    [InlineData(nameof(QuantitySurveyDayworksController.OpenEvidence), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyDayworksController.Verify), QuantitySurveyAccessControlRegistry.VariationsManage)]
    [InlineData(nameof(QuantitySurveyDayworksController.Reject), QuantitySurveyAccessControlRegistry.VariationsManage)]
    public void Internal_routes_require_the_expected_shared_permission(string action, string permission) =>
        typeof(QuantitySurveyDayworksController).GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(permission);

    [Fact]
    public void External_routes_are_authenticated_and_scoped_by_partner_project_policy()
    {
        typeof(ExternalProjectDayworksController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        var source = Source("src", "ErpSystem.Api", "Services", "QuantitySurvey", "QuantitySurveyDayworkService.cs");
        source.Should().Contain("BusinessPartnerUsers.IgnoreQueryFilters()")
            .And.Contain("ProjectExternalAccessPolicies")
            .And.Contain("value.BusinessPartnerId == link.BusinessPartnerId")
            .And.Contain("SourcePurchaseRequisition.ProjectId == projectId")
            .And.Contain("IsolationLevel.Serializable")
            .And.Contain("IControlledFileUploadService")
            .And.Contain("ICentralDocumentRepositoryFileService")
            .And.Contain("QuantitySurveyDayworkRevisions.AsNoTracking()")
            .And.Contain("value.TenantId == TenantId")
            .And.Contain("QS-DEC-011")
            .And.NotContain("catch (Exception");
    }

    [Fact]
    public void Controllers_return_safe_problem_details_and_bubble_unexpected_errors()
    {
        var source = Source("src", "ErpSystem.Api", "Controllers", "QuantitySurvey", "QuantitySurveyDayworksController.cs");
        source.Should().Contain("ProblemDetails").And.Contain("correlationId").And.NotContain("catch (Exception");
    }

    private static string Source(params string[] path) => File.ReadAllText(Path.Combine(FindRepositoryRoot(), Path.Combine(path)));
    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        foreach (var start in new[] { Environment.CurrentDirectory, Path.GetDirectoryName(sourceFile) })
            for (var directory = string.IsNullOrWhiteSpace(start) ? null : new DirectoryInfo(start); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
