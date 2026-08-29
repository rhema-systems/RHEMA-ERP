using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyContractPageRegressionGuardTests
{
    [Fact]
    public void E2e_fixture_uses_the_canonical_procurement_profile_and_application_policy_hash_shape()
    {
        var source = Source("scripts", "quantity-survey", "seed-quantity-survey-e2e.sql");

        source.Should().Contain("p.ProfileCode=N'TDC-PROCUREMENT'")
            .And.Contain("d.Status=2 AND d.ApprovalStatus=1")
            .And.Contain("EvidenceStatus=2")
            .And.Contain("All fourteen Procurement decisions must be complete")
            .And.Contain("@ContractPolicyJson")
            .And.Contain("retentionDecisionId")
            .And.Contain("retentionValueJson")
            .And.Contain("controlsDecisionId")
            .And.Contain("controlsValueJson")
            .And.Contain("Status=N'Draft'")
            .And.NotContain("DECLARE @ProcProfile uniqueidentifier=(SELECT TOP(1) Id FROM dbo.ProcurementConfigurationProfiles WHERE TenantId=@TenantId AND LifecycleStatus=1");

        for (var index = 1; index <= 14; index++)
            source.Should().Contain($"WHEN N'DEC-{index:000}'");
    }

    [Fact]
    public void Contract_views_do_not_render_internal_tracker_or_diagnostic_identifiers()
    {
        var activation = Source("frontend", "src", "components", "procurement", "ContractActivationGate.tsx");
        activation.Should().NotContain("check.code")
            .And.NotContain("decisionKeys.map")
            .And.Contain("Contract activation setup is not available");

        var closeout = Source("frontend", "src", "components", "procurement", "WorksCloseoutWorkspace.tsx");
        closeout.Should().NotContain("check.code")
            .And.NotContain("decisionKeys.map")
            .And.NotContain("TDC-0409")
            .And.NotContain("DEC-001 through DEC-014")
            .And.Contain("Works closeout setup is not available");

        var commercial = Source("frontend", "src", "components", "quantity-survey",
            "QuantitySurveyContractCommercialTermsPanel.tsx");
        commercial.Should().NotContain("QS-0520")
            .And.Contain("Commercial terms on record")
            .And.Contain("Commercial terms could not be loaded");
    }

    [Fact]
    public void Project_workspace_keeps_authorized_qs_tabs_available_without_admin_setup_privileges()
    {
        var workspace = Source("src", "ErpSystem.Core", "Services", "Projects", "ProjectService.Workspace.cs");
        workspace.Should().Contain("ProjectFinancialControlSummaryDto? financialSummary = null")
            .And.Contain("ProjectGovernanceSummaryDto? governanceSummary = null")
            .And.Contain("catch (UnauthorizedAccessException)")
            .And.Contain("FinancialSummary = financialSummary")
            .And.Contain("GovernanceSummary = governanceSummary");

        var page = Source("frontend", "src", "app", "development", "projects", "[id]",
            "ProjectWorkspacePage.tsx");
        foreach (var lookup in new[]
                 {
                     "getProjectTypes", "getProjectPriorities", "getProjectTemplates", "getPortfolios"
                 })
            page.Should().Contain($"projectService.{lookup}().catch(() => [])");
    }

    [Fact]
    public void E2e_fixture_demonstrates_the_tdc_architecture_workflow_stages_as_test_configuration()
    {
        var source = Source("scripts", "quantity-survey", "seed-quantity-survey-e2e.sql");

        source.Should().Contain("\"architectureStages\":\"tdc-16.4-v1\"")
            .And.Contain("N'Finance and budget validation'")
            .And.Contain("N'Engineering and project confirmation'")
            .And.Contain("N'Finance validation'")
            .And.Contain("N'Engineer source confirmation'")
            .And.Contain("N'QS valuation'")
            .And.Contain("N'Procurement contract review'")
            .And.Contain("N'Finance budget validation'")
            .And.Contain("N'Final authority approval'")
            .And.Contain("It is fixture configuration, not a runtime role name")
            .And.Contain("N'TDC_PROJECT_ENGINEER'")
            .And.Contain("N'TDC_FINANCE_REVIEWER'")
            .And.Contain("N'TDC_HEAD_OF_PROCUREMENT'");
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
