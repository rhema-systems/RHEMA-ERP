using System.Reflection;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveyContractCommercialTermsSecurityTests
{
    [Theory]
    [InlineData(nameof(QuantitySurveyContractCommercialTermsController.Workspace), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyContractCommercialTermsController.Configure), QuantitySurveyAccessControlRegistry.FinalAccountsManage)]
    public void Routes_require_existing_quantity_survey_permissions(string action, string permission) =>
        typeof(QuantitySurveyContractCommercialTermsController).GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(permission);

    [Fact]
    public void Service_reuses_canonical_contract_project_finance_dms_and_audit_owners()
    {
        var source = Source("src", "ErpSystem.Api", "Services", "QuantitySurvey",
            "QuantitySurveyContractCommercialTermsService.cs");

        source.Should().Contain("db.Contracts")
            .And.Contain("projectService.HasProjectAccessAsync")
            .And.NotContain("projectService.GetProjectByIdAsync")
            .And.Contain("db.PaymentTerms")
            .And.Contain("GovernedDocumentQuery")
            .And.Contain("FileVirusScanStatus.Clean")
            .And.Contain("IsolationLevel.Serializable")
            .And.Contain("CommercialTermsClientRequestId")
            .And.Contain("db.AuditLogs.Add")
            .And.Contain("value.TenantId == TenantId")
            .And.NotContain("catch (Exception");
    }

    [Fact]
    public void Existing_contract_activation_is_the_downstream_approval_gate()
    {
        var source = Source("src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementContractActivationService.cs");

        source.Should().Contain("EvaluateQuantitySurveyCommercialTermsAsync")
            .And.Contain("CONTRACT_ACTIVATION_QS_TERMS_BLOCKED")
            .And.Contain("QuantitySurveyContractCommercialTermsRules.Validate")
            .And.Contain("CommercialTermsContractDocumentId");
    }

    [Fact]
    public void Migration_enforces_relational_and_post_draft_controls()
    {
        var source = Source("src", "ErpSystem.Data", "Migrations",
            "20260811043000_ExtendWorksContractCommercialTerms.cs");

        source.Should().Contain("FK_Contracts_PaymentTerms_PaymentTermId")
            .And.Contain("FK_Contracts_ContractDocuments_CommercialTermsContractDocumentId")
            .And.Contain("FK_Contracts_QuantitySurveyConfigurationProfiles")
            .And.Contain("CK_Contracts_QS0520_Lineage")
            .And.Contain("TR_Contracts_QS0520CommercialTerms")
            .And.Contain("d.Status <> 'Draft'")
            .And.Contain("SESSION_CONTEXT(N'qs_contract_terms_id')")
            .And.Contain("reclassified to or from Works")
            .And.Contain("governed QS service")
            .And.Contain("u.IsActive = 1")
            .And.Contain("VirusScanStatus = 2")
            .And.Contain("THROW 51942");
    }

    private static string Source(params string[] path) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), Path.Combine(path)));

    private static string FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        foreach (var start in new[] { Environment.CurrentDirectory, Path.GetDirectoryName(sourceFile) })
            for (var directory = string.IsNullOrWhiteSpace(start) ? null : new DirectoryInfo(start);
                 directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
                    return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
