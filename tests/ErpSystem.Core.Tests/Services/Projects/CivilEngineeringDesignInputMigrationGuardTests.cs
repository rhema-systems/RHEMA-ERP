using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringDesignInputMigrationGuardTests
{
    private const string MigrationId = "20260815031044_AddCivilEngineeringDesignInputRequests";

    [Fact]
    public void Migration_extends_project_rfi_and_protects_section_dms_and_lifecycle_lineage()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Data", "LegacyMigrationsArchive", MigrationId + ".cs"));
        var metadata = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Data", "LegacyMigrationsArchive", "FastBuildMigrationMetadata.cs"));
        var preflight = File.ReadAllText(Path.Combine(
            root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));

        migration.Split("migrationBuilder.CreateTable(").Length.Should().Be(2);
        migration.Should().Contain("name: \"ProjectCivilDesignInputResponses\"")
            .And.Contain("table: \"ProjectRfis\"")
            .And.Contain("principalTable: \"ProjectCivilDesignCases\"")
            .And.Contain("principalTable: \"Sections\"")
            .And.Contain("principalTable: \"CentralDocumentRecords\"")
            .And.Contain("principalTable: \"CentralDocumentVersions\"");
        migration.Should().NotContain("name: \"Projects\",")
            .And.NotContain("RenameTable");
        migration.Should().Contain("TR_ProjectRfis_CivilDesignInputLifecycle")
            .And.Contain("TR_ProjectCivilDesignInputResponses_Lineage")
            .And.Contain("TR_ProjectCivilDesignInputResponses_AppendOnly")
            .And.Contain("current Published DMS lineage")
            .And.Contain("requested HR section")
            .And.Contain("Invalid Civil design-input request lifecycle transition");
        metadata.Should().Contain($"Migration(\"{MigrationId}\")");
        preflight.Should().Contain($"GUARD_COVERAGE|{MigrationId}");
    }

    [Fact]
    public void Runtime_reuses_project_rfi_hr_dms_and_central_audit_owners()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Data", "Services",
            "CivilEngineeringDesignService.InformationRequests.cs"));

        source.Should().Contain("db.ProjectRfis")
            .And.Contain("db.Sections")
            .And.Contain("db.Employees")
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
