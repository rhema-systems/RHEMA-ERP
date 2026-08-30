using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringReconnaissanceMigrationGuardTests
{
    private const string MigrationId = "20260815005453_AddCivilEngineeringReconnaissance";

    [Fact]
    public void Migration_is_scoped_discoverable_and_protects_relational_lineage()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Data", "Migrations", MigrationId + ".cs"));
        var metadata = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Data", "Migrations", "FastBuildMigrationMetadata.cs"));
        var preflight = File.ReadAllText(Path.Combine(
            root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));

        migration.Split("migrationBuilder.CreateTable(").Length.Should().Be(4);
        migration.Should().Contain("name: \"ProjectCivilReconnaissanceReports\"")
            .And.Contain("name: \"ProjectCivilReconnaissanceItems\"")
            .And.Contain("name: \"ProjectCivilReconnaissanceRevisions\"")
            .And.Contain("principalTable: \"ProjectCivilDesignCases\"")
            .And.Contain("principalTable: \"CentralDocumentRecords\"")
            .And.Contain("principalTable: \"CentralDocumentVersions\"")
            .And.Contain("principalTable: \"Sections\"");
        migration.Should().NotContain("AddColumn")
            .And.NotContain("DropColumn")
            .And.NotContain("RenameTable")
            .And.NotContain("name: \"Projects\",");
        migration.Should().Contain("TR_ProjectCivilReconnaissanceReports_Lifecycle")
            .And.Contain("TR_ProjectCivilReconnaissanceReports_Delete")
            .And.Contain("TR_ProjectCivilReconnaissanceItems_Lineage")
            .And.Contain("TR_ProjectCivilReconnaissanceRevisions_AppendOnly")
            .And.Contain("current Published DMS lineage")
            .And.Contain("Completed Civil reconnaissance reports are immutable")
            .And.Contain("Items on completed Civil reconnaissance reports are immutable");
        metadata.Should().Contain($"Migration(\"{MigrationId}\")");
        preflight.Should().Contain($"GUARD_COVERAGE|{MigrationId}");
    }

    [Fact]
    public void Runtime_reuses_projects_hr_sections_central_dms_and_shared_audit()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Data", "Services",
            "CivilEngineeringDesignService.Reconnaissance.cs"));

        source.Should().Contain("RequiredAsync(designCaseId")
            .And.Contain("RequireProjectAsync")
            .And.Contain("db.Sections")
            .And.Contain("CentralDocumentEvidenceRules.CurrentPublished()")
            .And.Contain("db.AuditLogs.Add")
            .And.Contain("IsolationLevel.Serializable")
            .And.Contain("CryptographicOperations.FixedTimeEquals")
            .And.Contain("ClientRequestId");
        source.Should().NotContain("File.WriteAllBytes")
            .And.NotContain("new WorkflowInstance")
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
