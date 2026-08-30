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
            .And.Contain("They are fixture configuration")
            .And.Contain("N'TDC_QS_REVIEWER'")
            .And.Contain("N'TDC_PROJECT_ENGINEER'")
            .And.Contain("N'TDC_FINANCE_REVIEWER'")
            .And.Contain("N'TDC_HEAD_OF_PROCUREMENT'");
    }

    [Fact]
    public void E2e_fixture_preserves_the_formal_works_contract_budget_lifecycle_before_qs_certification()
    {
        var source = Source("scripts", "quantity-survey", "seed-quantity-survey-e2e.sql");

        source.Should().Contain("N'QS-E2E-BUDGET-001'")
            .And.Contain("N'QS-E2E-PR-001'")
            .And.Contain("N'QS-E2E-RES-001'")
            .And.Contain("N'QS-E2E-SRL-001'")
            .And.Contain("INSERT dbo.ProcurementBudgetCommitments")
            .And.Contain("INSERT dbo.ProcurementBudgetCommitmentLedgerEntries")
            .And.Contain("N'Contract',@ContractId,N'QS-E2E-WORKS-001'")
            .And.Contain("SourcePurchaseRequisitionId=@SourceRequisitionId")
            .And.Contain("SourcingReleaseId=@SourcingReleaseId")
            .And.Contain("DECLARE @TenderGuardWasEnabled bit=CASE WHEN EXISTS")
            .And.Contain("IF @TenderGuardWasEnabled=1")
            .And.Contain("The QS E2E formal contract commitment lineage is inconsistent.");
    }

    [Fact]
    public void E2e_bootstrap_uses_the_explicit_disposable_connection_and_tenant_scoped_actor_meaning()
    {
        var launcher = Source("scripts", "quantity-survey", "Invoke-QuantitySurveyE2ESeed.ps1");
        launcher.Should().Contain("ConnectionStrings__DefaultConnection")
            .And.Contain("ConnectionStrings:DefaultConnection is not configured in the environment or API user-secrets");

        var fixture = Source("scripts", "quantity-survey", "seed-quantity-survey-e2e.sql");
        fixture.Should().Contain("TenantId = @TenantId AND UserName = N'admin' AND IsActive = 1")
            .And.Contain("TenantId = @TenantId AND UserName = N'employee' AND IsActive = 1")
            .And.Contain("TenantId = @TenantId AND UserName = N'manager' AND IsActive = 1")
            .And.Contain("TenantId = @TenantId AND UserName = N'helpdesk.supervisor' AND IsActive = 1")
            .And.Contain("TenantId = @TenantId AND UserName = N'helpdesk.manager' AND IsActive = 1")
            .And.Contain("TenantId = @TenantId AND UserName = N'finance.manager' AND IsActive = 1")
            .And.NotContain("58cafd8b-42ce-4f67-0dbb-08de862e82ee")
            .And.NotContain("77af28cf-66d3-49c0-0dbd-08de862e82ee")
            .And.NotContain("9e475ce9-34ad-4a6d-0dbc-08de862e82ee");

        var apiProgram = Source("src", "ErpSystem.Api", "Program.cs");
        apiProgram.Should().Contain("args[0] == \"seed-hr-all\"")
            .And.Contain("tempBuilder.Services.AddHttpContextAccessor();")
            .And.Contain("tempBuilder.Services.AddErpSystemIdentity();");

        var sharedSeeder = Source("src", "ErpSystem.Api", "Services", "DatabaseSeedingService.cs");
        sharedSeeder.Should().Contain("await _quantitySurveyAccessControlSeeder.SeedAsync();")
            .And.Contain("await _quantitySurveyConfigurationProfileSeeder.SeedAsync();");
    }

    [Fact]
    public void Authenticated_lifecycle_keeps_every_architecture_approval_actor_distinct()
    {
        var source = Source("e2e-tests", "tests", "qs-phases-0-6-lifecycle.spec.ts");
        foreach (var variable in new[]
                 {
                     "QS_ACCEPTANCE_MAKER_USERNAME",
                     "QS_ACCEPTANCE_REVIEWER_USERNAME",
                     "QS_ACCEPTANCE_ENGINEER_USERNAME",
                     "QS_ACCEPTANCE_FINANCE_VALIDATOR_USERNAME",
                     "QS_ACCEPTANCE_CHECKER_USERNAME"
                 })
            source.Should().Contain(variable);

        source.Should().Contain("headers: reviewer.headers")
            .And.Contain("'QS vetting'")
            .And.Contain("actor: workflowReviewer")
            .And.Contain("actor: engineer")
            .And.Contain("actor: financeValidator")
            .And.Contain("actor: approver")
            .And.Contain("complete valuation ${stage.label}")
            .And.Contain("complete payment-certificate ${stage.label}")
            .And.NotContain("actor: reviewer")
            .And.NotContain("ids.worksheetApprove1")
            .And.NotContain("ids.certificateApprove1");

        var uat = Source("docs", "QUANTITY_SURVEY_END_TO_END_UAT.md");
        uat.Should().Contain("Automated harness boundary and actor matrix")
            .And.Contain("This is not evidence that the automated spec created and approved the estimate")
            .And.Contain("leave their checklist rows open");
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
