using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringWorksCaseInitiationMigrationGuardTests
{
    private const string MigrationId = "20260821230000_AddCivilEngineeringWorksCaseInitiation";

    [Fact]
    public void Migration_extends_the_existing_design_case_owner_with_legacy_safe_source_lineage_guards()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "LegacyMigrationsArchive", MigrationId + ".cs"));
        var preflight = File.ReadAllText(Path.Combine(root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));

        migration.Should().Contain("ALTER TABLE dbo.ProjectCivilDesignCases ADD")
            .And.Contain("CK_ProjectCivilDesignCases_InitiationShape")
            .And.Contain("IX_ProjectCivilDesignCases_TenantId_InitiationSource_InitiationSourceId")
            .And.Contain("Status <> 'Approved' AND Status <> 'Rejected' AND Status <> 'Cancelled'")
            .And.Contain("TR_ProjectCivilDesignCases_InitiationLineage")
            .And.Contain("CapitalProjects")
            .And.Contain("CivilEngineeringMaintenanceIntakes")
            .And.Contain("EstateManagedAssets")
            .And.Contain("ProjectCivilDevelopmentApprovalFiles")
            .And.Contain("CentralDocumentRecords")
            .And.Contain("CentralDocumentVersions")
            .And.Contain("ProjectDefectLiabilityCases")
            .And.Contain("ProjectIssues")
            .And.Contain("source, property/site, category, classification and assessment lineage is immutable")
            .And.NotContain("CREATE TABLE ProjectCivilEngineeringCases")
            .And.NotContain("INSERT INTO dbo.ProjectCivilDesignCases");
        migration.Should().Contain($"[Migration(\"{MigrationId}\")]");
        preflight.Should().Contain($"GUARD_COVERAGE|{MigrationId}");
    }

    [Fact]
    public void Runtime_and_shared_panel_reuse_existing_owners_and_controlled_selectors()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Services", "CivilEngineeringDesignService.cs"));
        var panel = File.ReadAllText(Path.Combine(root, "frontend", "src", "components", "projects", "civil-engineering", "CivilEngineeringDesignWorkflowPanel.tsx"));

        source.Should().Contain("db.CapitalProjects")
            .And.Contain("db.CivilEngineeringMaintenanceIntakes")
            .And.Contain("db.EstateManagedAssets")
            .And.Contain("db.ProjectCivilDevelopmentApprovalFiles")
            .And.Contain("db.CentralDocumentVersions")
            .And.Contain("db.ProjectDefectLiabilityCases")
            .And.Contain("db.ProjectIssues")
            .And.Contain("CentralDocumentEvidenceRules.CurrentPublished()")
            .And.Contain("RequireInitiationSourceAsync")
            .And.Contain("RequireEngineeringCategoryAsync")
            .And.Contain("CryptographicOperations.FixedTimeEquals")
            .And.Contain("db.AuditLogs.Add")
            .And.NotContain("File.WriteAllBytes")
            .And.NotContain("new WorkflowInstance");
        panel.Should().Contain("Authoritative source")
            .And.Contain("Property / site")
            .And.Contain("Engineering category")
            .And.Contain("Work classification")
            .And.Contain("Published management directive")
            .And.NotContain("type=\"text\"");
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
