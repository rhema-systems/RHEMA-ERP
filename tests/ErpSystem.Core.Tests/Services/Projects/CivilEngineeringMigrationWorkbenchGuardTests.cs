using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringMigrationWorkbenchGuardTests
{
    private const string MigrationId = "20260821210000_AddCivilEngineeringMigrationWorkbench";

    [Fact]
    public void Migration_creates_only_governed_staging_registers_with_immutable_lineage()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", MigrationId + ".cs"));
        var preflight = File.ReadAllText(Path.Combine(root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));
        var upMigration = migration[..migration.IndexOf("protected override void Down", StringComparison.Ordinal)];
        var sqlBlocks = upMigration.Split("migrationBuilder.Sql(\"\"\"", StringSplitOptions.None).Skip(1).ToList();

        migration.Should().Contain("ProjectCivilMigrationBatches")
            .And.Contain("ProjectCivilMigrationRecords")
            .And.Contain("ProjectCivilMigrationValidationIssues")
            .And.Contain("ProjectCivilMigrationRevisions")
            .And.Contain("CIV-CFG-013")
            .And.Contain("CentralDocumentRecords")
            .And.Contain("CentralDocumentVersions")
            .And.Contain("TR_ProjectCivilMigrationBatches_Lineage")
            .And.Contain("TR_ProjectCivilMigrationBatches_Lifecycle")
            .And.Contain("TR_ProjectCivilMigrationRecords_Lineage")
            .And.Contain("TR_ProjectCivilMigrationRecords_AppendOnly")
            .And.Contain("TR_ProjectCivilMigrationValidationIssues_Lineage")
            .And.Contain("TR_ProjectCivilMigrationValidationIssues_AppendOnly")
            .And.Contain("TR_ProjectCivilMigrationRevisions_Lineage")
            .And.Contain("TR_ProjectCivilMigrationRevisions_AppendOnly")
            .And.NotContain("INSERT INTO ProjectCivilDesign")
            .And.NotContain("INSERT INTO ProjectPermits")
            .And.NotContain("INSERT INTO Maintenance");
        sqlBlocks.Should().HaveCount(9);
        sqlBlocks.Where(value => value.Contains("CREATE TRIGGER", StringComparison.Ordinal))
            .Should().OnlyContain(value => Count(value, "CREATE TRIGGER") == 1,
                "SQL Server requires CREATE TRIGGER to be the first statement in its migration batch");
        migration.Should().Contain($"[Migration(\"{MigrationId}\")]");
        preflight.Should().Contain($"GUARD_COVERAGE|{MigrationId}");
    }

    [Fact]
    public void Runtime_reuses_project_dms_security_and_audit_owners_without_posting_to_owner_modules()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Services", "CivilEngineeringMigrationService.cs"));

        source.Should().Contain("IProjectService")
            .And.Contain("CentralDocumentEvidenceRules.CurrentPublished()")
            .And.Contain("db.UserRoles")
            .And.Contain("db.ProjectMembers")
            .And.Contain("db.AuditLogs.Add")
            .And.Contain("CIV-CFG-013")
            .And.Contain("CryptographicOperations.FixedTimeEquals")
            .And.Contain("ClientRequestId");
        source.Should().NotContain("File.WriteAllBytes")
            .And.NotContain("new WorkflowInstance")
            .And.NotContain("PostAsync")
            .And.NotContain("Finance");
    }

    [Fact]
    public void Api_and_frontend_keep_the_workbench_project_scoped_and_selector_based()
    {
        var root = FindRepositoryRoot();
        var controller = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Controllers", "Projects", "CivilEngineeringMigrationBatchesController.cs"));
        var page = File.ReadAllText(Path.Combine(root, "frontend", "src", "app", "development", "civil-engineering", "migration-batches", "page.tsx"));
        var service = File.ReadAllText(Path.Combine(root, "frontend", "src", "services", "civil-engineering-migration.service.ts"));

        controller.Should().Contain("[Route(\"api/projects/{projectId:guid}/civil-engineering/migration-batches\")]")
            .And.Contain("CivilEngineeringAccessControlRegistry.MigrationManage")
            .And.Contain("CivilEngineeringAccessControlRegistry.WorkspaceRead")
            .And.Contain("CivilEngineeringAccessControlRegistry.AuditRead")
            .And.NotContain("PostAsync");
        page.Should().Contain("Select permitted project")
            .And.Contain("Current Published central-DMS file")
            .And.Contain("Reconciliation DMS evidence")
            .And.Contain("lookups.recordTypes")
            .And.NotContain("owner-post");
        service.Should().Contain("/projects/${projectId}/civil-engineering/migration-batches")
            .And.NotContain("/post");
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

    private static int Count(string source, string value) => source.Split(value, StringSplitOptions.None).Length - 1;
}
