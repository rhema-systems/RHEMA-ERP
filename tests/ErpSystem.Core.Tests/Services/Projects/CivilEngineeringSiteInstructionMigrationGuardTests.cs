using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringSiteInstructionMigrationGuardTests
{
    private const string MigrationId = "20260820170000_AddCivilEngineeringSiteInstructionRouting";
    private const string LifecycleHardeningMigrationId = "20260822002000_HardenCivilEngineeringSiteInstructionLifecycle";

    [Fact]
    public void Migration_adds_only_the_governance_envelope_and_protects_its_lineage_and_history()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", MigrationId + ".cs"));
        var preflight = File.ReadAllText(Path.Combine(root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));

        migration.Should().Contain("CREATE TABLE ProjectCivilSiteInstructionRoutings")
            .And.Contain("CREATE TABLE ProjectCivilSiteInstructionEvidence")
            .And.Contain("CREATE TABLE ProjectCivilSiteInstructionResponses")
            .And.Contain("CREATE TABLE ProjectCivilSiteInstructionRevisions")
            .And.Contain("ProjectSiteInstructions(Id)")
            .And.Contain("ProjectCivilProjectEngineerAssignments(Id)")
            .And.Contain("CentralDocumentRecords(Id)")
            .And.Contain("CentralDocumentVersions(Id)")
            .And.Contain("TR_ProjectCivilSiteInstructionRoutings_Lineage")
            .And.Contain("TR_ProjectCivilSiteInstructionRoutings_Lifecycle")
            .And.Contain("TR_ProjectCivilSiteInstructionResponses_AppendOnly")
            .And.Contain("TR_ProjectCivilSiteInstructionRevisions_AppendOnly")
            .And.Contain("CIV-CFG-005")
            .And.Contain($"[Migration(\"{MigrationId}\")]");
        migration.Should().NotContain("ALTER TABLE Projects")
            .And.NotContain("DROP TABLE Projects")
            .And.NotContain("UPDATE ProjectSiteInstructions")
            .And.NotContain("CREATE OR ALTER TRIGGER");
        preflight.Should().Contain($"GUARD_COVERAGE|{MigrationId}");
    }

    [Fact]
    public void Runtime_reuses_projects_dms_workflow_and_external_partner_owners_without_a_bypass_path()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Services", "CivilEngineeringSiteInstructionService.cs"));
        var genericProjectService = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "Projects", "ProjectService.DesignSiteControls.cs"));
        var panel = File.ReadAllText(Path.Combine(root, "frontend", "src", "components", "projects", "civil-engineering", "CivilEngineeringSiteInstructionsPanel.tsx"));

        service.Should().Contain("IsolationLevel.Serializable")
            .And.Contain("CentralDocumentEvidenceRules.CurrentPublished")
            .And.Contain("CivilEngineeringWorkflowBindingRegistry.SiteInstruction")
            .And.Contain("RequireExternalContractorAsync")
            .And.Contain("ProjectExternalAccessPolicies")
            .And.Contain("db.AuditLogs.Add")
            .And.Contain("CryptographicOperations.FixedTimeEquals")
            .And.Contain("sql.Number is >= 52011 and <= 52077")
            .And.NotContain("new WorkflowInstance");
        genericProjectService.Should().Contain("ProjectSiteInstructionTypes.EngineerInstruction")
            .And.Contain("Use the governed Civil Engineering site-instruction route for Engineer Instructions.");
        panel.Should().Contain("Current Published DMS evidence")
            .And.Contain("Project Manager workflow action")
            .And.Contain("Immutable contractor and engineering history")
            .And.NotContain("Contractor business-partner ID");
    }

    [Fact]
    public void Lifecycle_hardening_keeps_the_existing_owner_and_adds_scoped_evidence_version_review_follow_up_and_closure_guards()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", LifecycleHardeningMigrationId + ".cs"));
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Services", "CivilEngineeringSiteInstructionService.cs"));
        var preflight = File.ReadAllText(Path.Combine(root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));

        migration.Should().Contain("InstructionVersion")
            .And.Contain("SupersedesRoutingId")
            .And.Contain("AwaitingEngineeringReview")
            .And.Contain("AwaitingEngineeringFollowUp")
            .And.Contain("TR_ProjectCivilSiteInstructionResponses_Lineage")
            .And.Contain("CentralDocumentRecords")
            .And.Contain("Contracts(Id)")
            .And.NotContain("CREATE TABLE ProjectSiteInstructions")
            .And.NotContain("CREATE TABLE Contracts")
            .And.NotContain("CREATE TABLE CentralDocument");
        service.Should().Contain("ReviewContractorResponseAsync")
            .And.Contain("RecordEngineeringFollowUpAsync")
            .And.Contain("EnsureCurrentCommercialRouteAsync")
            .And.Contain("DocumentRecord.SourceRecordId == projectId")
            .And.Contain("ApplyRowVersion(routing, request.RowVersion)")
            .And.Contain("SupersedeSiteInstruction");
        preflight.Should().Contain($"GUARD_COVERAGE|{LifecycleHardeningMigrationId}");
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
