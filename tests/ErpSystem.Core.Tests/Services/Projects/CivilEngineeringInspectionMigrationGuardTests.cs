using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringInspectionMigrationGuardTests
{
    private const string MigrationId = "20260822003000_AddCivilEngineeringInspectionControls";
    private const string HardeningMigrationId = "20260822003100_HardenCivilEngineeringInspectionAuditActions";
    private const string WorkflowMigrationId = "20260822003700_GovernCivilInspectionPlanWorkflow";

    [Fact]
    public void Migration_extends_the_authoritative_projects_quality_and_non_conformance_owners_without_backfilling_them()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", MigrationId + ".cs"));
        var hardeningMigration = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", HardeningMigrationId + ".cs"));
        var workflowMigration = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", WorkflowMigrationId + ".cs"));
        var preflight = File.ReadAllText(Path.Combine(root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));

        migration.Should().Contain("CREATE TABLE dbo.ProjectCivilInspectionControls")
            .And.Contain("ProjectQualityCheckpoints(Id)")
            .And.Contain("ProjectNonConformances(Id)")
            .And.Contain("ProjectCivilPlanningGisValidations(Id)")
            .And.Contain("CentralDocumentRecords(Id)")
            .And.Contain("CentralDocumentVersions(Id)")
            .And.Contain("TR_ProjectCivilInspectionControls_Lineage")
            .And.Contain("TR_ProjectCivilInspectionControls_Lifecycle")
            .And.Contain("TR_ProjectCivilInspectionRevisions_AppendOnly")
            .And.Contain("CIV-CFG-005")
            .And.Contain("CIV-CFG-011")
            .And.NotContain("UPDATE dbo.ProjectQualityCheckpoints")
            .And.NotContain("UPDATE dbo.ProjectNonConformances")
            .And.NotContain("CREATE TABLE dbo.Projects");
        hardeningMigration.Should().Contain("CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilInspectionRevisions_Lineage")
            .And.Contain("SubmitCivilDefectReinspection")
            .And.Contain("CloseCivilInspection")
            .And.NotContain("UPDATE dbo.ProjectCivilInspectionControls");
        workflowMigration.Should().Contain("WorkflowDefinitionId")
            .And.Contain("WorkflowInstanceId")
            .And.Contain("PROJECT_QUALITY_TEST")
            .And.Contain("TR_ProjectCivilInspectionControls_Workflow")
            .And.Contain("ApproveCivilInspectionPlan")
            .And.Contain("RejectCivilInspectionPlan")
            .And.Contain("OPENJSON(decision.ValueJson,'$.reviewerRoleIds')")
            .And.Contain("Rollback is blocked")
            .And.NotContain("CREATE TABLE dbo.Workflow")
            .And.NotContain("CREATE TABLE dbo.Notifications");
        preflight.Should().Contain($"GUARD_COVERAGE|{MigrationId}")
            .And.Contain($"GUARD_COVERAGE|{HardeningMigrationId}")
            .And.Contain($"GUARD_COVERAGE|{WorkflowMigrationId}");
    }

    [Fact]
    public void Runtime_uses_controlled_selectors_idempotency_immutable_history_and_blocks_generic_bypass_paths()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Services", "CivilEngineeringInspectionControlService.cs"));
        var projectService = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "Projects", "ProjectServices.cs"));
        var panel = File.ReadAllText(Path.Combine(root, "frontend", "src", "components", "projects", "civil-engineering", "CivilEngineeringInspectionControlsPanel.tsx"));

        service.Should().Contain("IsolationLevel.Serializable")
            .And.Contain("CentralDocumentEvidenceRules.CurrentPublished")
            .And.Contain("RequirePlanningValidationAsync")
            .And.Contain("RequireInspectorAsync")
            .And.Contain("RequireIndependentCloserAsync")
            .And.Contain("workflow.SubmitAsync(CivilEngineeringWorkflowBindingRegistry.QualityTest")
            .And.Contain("workflow.CanUserApproveAsync(CivilEngineeringWorkflowBindingRegistry.QualityTest")
            .And.Contain("workflow.ProcessApprovalAsync(CivilEngineeringWorkflowBindingRegistry.QualityTest")
            .And.Contain("db.Notifications.Add")
            .And.Contain("CryptographicOperations.FixedTimeEquals")
            .And.Contain("db.AuditLogs.Add")
            .And.Contain("sql.Number is >= 52078 and <= 52084");
        projectService.Should().Contain("governed by a Civil inspection control")
            .And.Contain("independent passed reinspection");
        panel.Should().Contain("Approved Planning/GIS site")
            .And.Contain("Qualified inspector")
            .And.Contain("Current Published DMS")
            .And.Contain("Approve plan")
            .And.Contain("Reject plan")
            .And.NotContain("Inspector user ID");
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
