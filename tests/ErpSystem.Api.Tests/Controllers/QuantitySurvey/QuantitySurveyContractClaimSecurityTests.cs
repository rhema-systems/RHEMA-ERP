using System.Reflection;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveyContractClaimSecurityTests
{
    [Theory]
    [InlineData(nameof(QuantitySurveyContractClaimsController.Workspace), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyContractClaimsController.Get), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyContractClaimsController.Evidence), QuantitySurveyAccessControlRegistry.ClaimsManage)]
    [InlineData(nameof(QuantitySurveyContractClaimsController.Vet), QuantitySurveyAccessControlRegistry.ClaimsManage)]
    [InlineData(nameof(QuantitySurveyContractClaimsController.SubmitApproval), QuantitySurveyAccessControlRegistry.ClaimsManage)]
    [InlineData(nameof(QuantitySurveyContractClaimsController.Approve), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyContractClaimsController.Reject), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyContractClaimsController.AcceptDispute), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyContractClaimsController.RejectDispute), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyContractClaimsController.Settle), QuantitySurveyAccessControlRegistry.ClaimsManage)]
    [InlineData(nameof(QuantitySurveyContractClaimsController.History), QuantitySurveyAccessControlRegistry.AuditRead)]
    public void Internal_routes_require_the_expected_shared_permission(string action, string permission) =>
        typeof(QuantitySurveyContractClaimsController).GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(permission);

    [Fact]
    public void External_routes_are_authenticated_and_service_scoped_to_partner_project_policy()
    {
        typeof(ExternalProjectContractClaimsController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        var source = Source("src", "ErpSystem.Api", "Services", "QuantitySurvey", "QuantitySurveyContractClaimService.cs");
        source.Should().Contain("BusinessPartnerUsers.IgnoreQueryFilters()")
            .And.Contain("ProjectExternalAccessPolicies")
            .And.Contain("value.BusinessPartnerId == link.BusinessPartnerId")
            .And.Contain("SourcePurchaseRequisition.ProjectId == projectId")
            .And.NotContain("project.BusinessPartnerId == actor.BusinessPartnerId");
    }

    [Fact]
    public void Service_reuses_shared_owners_and_atomic_idempotent_mutations()
    {
        var source = Source("src", "ErpSystem.Api", "Services", "QuantitySurvey", "QuantitySurveyContractClaimService.cs");
        source.Should().Contain("IsolationLevel.Serializable")
            .And.Contain("IWorkflowIntegrationService")
            .And.Contain("IControlledFileUploadService")
            .And.Contain("ICentralDocumentRepositoryFileService")
            .And.Contain("QuantitySurveyContractClaimRevisions.AsNoTracking()")
            .And.Contain("value.TenantId == TenantId")
            .And.Contain("QS-DEC-011")
            .And.NotContain("catch (Exception");
    }

    [Fact]
    public void Controllers_return_safe_problem_details_and_bubble_unexpected_errors()
    {
        var source = Source("src", "ErpSystem.Api", "Controllers", "QuantitySurvey", "QuantitySurveyContractClaimsController.cs");
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
