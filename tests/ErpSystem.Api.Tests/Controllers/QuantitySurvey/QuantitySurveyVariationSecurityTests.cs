using System.Reflection;
using ErpSystem.Api.Controllers.Projects;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveyVariationSecurityTests
{
    [Theory]
    [InlineData(nameof(QuantitySurveyVariationsController.Workspace), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyVariationsController.Get), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyVariationsController.Save), QuantitySurveyAccessControlRegistry.VariationsManage)]
    [InlineData(nameof(QuantitySurveyVariationsController.Evidence), QuantitySurveyAccessControlRegistry.VariationsManage)]
    [InlineData(nameof(QuantitySurveyVariationsController.Submit), QuantitySurveyAccessControlRegistry.VariationsManage)]
    [InlineData(nameof(QuantitySurveyVariationsController.Approve), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyVariationsController.Reject), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyVariationsController.Apply), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyVariationsController.History), QuantitySurveyAccessControlRegistry.AuditRead)]
    public void Routes_require_the_expected_shared_permission(string action, string permission) =>
        typeof(QuantitySurveyVariationsController).GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(permission);

    [Fact]
    public void Service_uses_tenant_scoped_module_owners_and_atomic_idempotent_mutations()
    {
        var source = Source("src", "ErpSystem.Api", "Services", "QuantitySurvey", "QuantitySurveyVariationService.cs");
        source.Should().Contain("value.TenantId == TenantId")
            .And.Contain("IsolationLevel.Serializable")
            .And.Contain("QuantitySurveyVariationRevisions.AsNoTracking()")
            .And.Contain("IWorkflowIntegrationService")
            .And.Contain("IControlledFileUploadService")
            .And.Contain("ICentralDocumentRepositoryFileService")
            .And.Contain("SourcePurchaseRequisition.ProjectId == projectId")
            .And.Contain("SourcePurchaseRequisition?.ProjectId != projectId")
            .And.NotContain("project.BusinessPartnerId")
            .And.NotContain("exception.ToString()");
    }

    [Fact]
    public void Sql_guard_uses_procurement_requisition_project_lineage_not_the_project_customer()
    {
        var source = Source("src", "ErpSystem.Data", "Migrations", "20260810234912_AddQuantitySurveyVariationLifecycle.cs");
        source.Should().Contain("LEFT JOIN dbo.PurchaseRequisitions pr")
            .And.Contain("pr.ProjectId = i.ProjectId")
            .And.NotContain("p.BusinessPartnerId <> i.ContractorBusinessPartnerId");
    }

    [Fact]
    public void Legacy_variation_mutations_are_denied_when_qs_governance_is_effective()
    {
        var source = Source("src", "ErpSystem.Api", "Controllers", "Projects", "ProjectsController.cs");
        source.Should().Contain("_quantitySurveyVariations.IsGovernedAsync")
            .And.Contain("Use the governed Quantity Survey variation workspace");
    }

    [Fact]
    public void Controller_returns_safe_problem_details_and_bubbles_unexpected_errors()
    {
        var source = Source("src", "ErpSystem.Api", "Controllers", "QuantitySurvey", "QuantitySurveyVariationsController.cs");
        source.Should().Contain("correlationId")
            .And.Contain("ProblemDetails")
            .And.NotContain("catch (Exception");
    }

    [Fact]
    public void Approved_application_reuses_owned_commercial_records_and_requires_the_sql_capability()
    {
        var service = Source("src", "ErpSystem.Api", "Services", "QuantitySurvey", "QuantitySurveyVariationService.cs");
        var migration = Source("src", "ErpSystem.Data", "Migrations", "20260811022852_AddQuantitySurveyVariationApplications.cs");
        service.Should().Contain("ContractAmendments.Add")
            .And.Contain("ProjectBudgetRevisions.Add")
            .And.Contain("ProjectForecastVersions.Add")
            .And.Contain("ProjectBoqVersions.Add")
            .And.Contain("IsolationLevel.Serializable")
            .And.NotContain("new VariationBudget")
            .And.NotContain("new VariationContract");
        migration.Should().Contain("qs_variation_application_id")
            .And.Contain("TR_QS0511_VariationApplication_Governance")
            .And.Contain("THROW 51931")
            .And.Contain("THROW 51932");
    }

    private static string Source(params string[] path) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), Path.Combine(path)));

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        foreach (var start in new[] { Environment.CurrentDirectory, Path.GetDirectoryName(sourceFile) })
            for (var directory = string.IsNullOrWhiteSpace(start) ? null : new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
