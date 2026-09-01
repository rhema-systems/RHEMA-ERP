using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringPlanningGisValidationMigrationGuardTests
{
    private const string MigrationId = "20260822000000_AddCivilEngineeringPlanningGisValidation";
    private const string EvidenceBindingMigrationId = "20260822001000_HardenCivilEngineeringPlanningGisEvidenceBinding";

    [Fact]
    public void Migration_adds_a_tenant_safe_child_gate_without_creating_parallel_estate_permitting_or_dms_owners()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", MigrationId + ".cs"));
        var evidenceBinding = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", EvidenceBindingMigrationId + ".cs"));
        var preflight = File.ReadAllText(Path.Combine(root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));

        migration.Should().Contain("ProjectCivilPlanningGisValidations")
            .And.Contain("ProjectCivilPlanningGisValidationRevisions")
            .And.Contain("ProjectCivilDesignCases")
            .And.Contain("EstateManagedAssets")
            .And.Contain("ProjectCivilDevelopmentApprovalFiles")
            .And.Contain("CentralDocumentRecords")
            .And.Contain("CentralDocumentVersions")
            .And.Contain("civil-planning-conditions")
            .And.Contain("civil-development-constraints")
            .And.Contain("civil-land-use-impacts")
            .And.Contain("TR_ProjectCivilPlanningGisValidations_Lineage")
            .And.Contain("TR_ProjectCivilPlanningGisValidations_Lifecycle")
            .And.Contain("TR_ProjectCivilDesignCases_PlanningGisGate")
            .And.Contain("ReviewedByUserId <> i.PreparedByUserId")
            .And.NotContain("CREATE TABLE dbo.EstateManagedAssets")
            .And.NotContain("CREATE TABLE dbo.ProjectCivilDevelopmentApprovalFiles")
            .And.NotContain("CREATE TABLE dbo.CentralDocumentRecords");
        migration.Should().Contain($"[Migration(\"{MigrationId}\")]");
        evidenceBinding.Should().Contain("ProjectCivilDevelopmentApprovalEvidence")
            .And.Contain("CentralDocumentRecordId = i.CentralDocumentRecordId")
            .And.Contain("CentralDocumentVersionId = i.CentralDocumentVersionId")
            .And.NotContain("CREATE TABLE");
        evidenceBinding.Should().Contain($"[Migration(\"{EvidenceBindingMigrationId}\")]");
        preflight.Should().Contain($"GUARD_COVERAGE|{MigrationId}");
        preflight.Should().Contain($"GUARD_COVERAGE|{EvidenceBindingMigrationId}");
    }

    [Fact]
    public void Service_reuses_controlled_catalogue_estate_permitting_dms_and_shared_audit_controls()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Services", "CivilEngineeringDesignService.PlanningGis.cs"));
        var baseService = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Services", "CivilEngineeringDesignService.cs"));
        var controller = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Controllers", "Projects", "CivilEngineeringDesignCasesController.cs"));
        var panel = File.ReadAllText(Path.Combine(root, "frontend", "src", "components", "projects", "civil-engineering", "CivilEngineeringPlanningGisPanel.tsx"));

        service.Should().Contain("db.EstateManagedAssets")
            .And.Contain("db.ProjectCivilDevelopmentApprovalFiles")
            .And.Contain("db.CentralDocumentVersions")
            .And.Contain("CentralDocumentEvidenceRules.CurrentPublished()")
            .And.Contain("ProjectCivilDevelopmentApprovalEvidence")
            .And.Contain("ProjectCatalogDefaults.CivilPlanningConditions")
            .And.Contain("ProjectCatalogDefaults.CivilDevelopmentConstraints")
            .And.Contain("ProjectCatalogDefaults.CivilLandUseImpacts")
            .And.Contain("PreparedByUserId == UserId")
            .And.Contain("PreparedByUserId != UserId")
            .And.Contain("db.AuditLogs.Add")
            .And.NotContain("File.WriteAllBytes")
            .And.NotContain("new WorkflowInstance");
        baseService.Should().Contain("CryptographicOperations.FixedTimeEquals");
        controller.Should().Contain("PermittingManage")
            .And.Contain("TransactionsApprove")
            .And.Contain("AuditRead");
        panel.Should().Contain("Development approval file")
            .And.Contain("Planning condition")
            .And.Contain("Current Published DMS evidence")
            .And.NotContain("<Input");
    }

    private static string FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory;
             directory is not null;
             directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
