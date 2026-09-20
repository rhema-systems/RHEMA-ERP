using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringDesignMigrationGuardTests
{
    private const string MigrationId = "20260814234022_AddCivilEngineeringDesignWorkflow";

    [Fact]
    public void Migration_is_narrow_discoverable_and_has_relational_lifecycle_guards()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Data", "LegacyMigrationsArchive", MigrationId + ".cs"));
        var metadata = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Data", "LegacyMigrationsArchive", "FastBuildMigrationMetadata.cs"));
        var preflight = File.ReadAllText(Path.Combine(
            root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));

        migration.Split("migrationBuilder.CreateTable(").Length.Should().Be(4);
        migration.Should().Contain("name: \"ProjectCivilDesignCases\"")
            .And.Contain("name: \"ProjectCivilDesignEvidence\"")
            .And.Contain("name: \"ProjectCivilDesignRevisions\"");
        migration.Should().NotContain("AddColumn")
            .And.NotContain("DropColumn")
            .And.NotContain("RenameTable")
            .And.NotContain("name: \"Projects\",");
        migration.Should().Contain("TR_ProjectCivilDesignCases_Lifecycle")
            .And.Contain("TR_ProjectCivilDesignEvidence_AppendOnly")
            .And.Contain("TR_ProjectCivilDesignRevisions_AppendOnly")
            .And.Contain("Invalid Civil design workflow transition")
            .And.Contain("PROJECT_DESIGN_REVIEW")
            .And.Contain("CIV-CFG-003");
        metadata.Should().Contain($"Migration(\"{MigrationId}\")");
        preflight.Should().Contain($"GUARD_COVERAGE|{MigrationId}");
    }

    [Fact]
    public void Runtime_service_reuses_project_workflow_dms_and_central_audit_owners()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Data", "Services", "CivilEngineeringDesignService.cs"));

        source.Should().Contain("projectService.GetProjectByIdAsync")
            .And.Contain("CivilEngineeringWorkflowBindingRegistry.DesignReview")
            .And.Contain("workflow.SubmitAsync")
            .And.Contain("workflow.ProcessApprovalAsync")
            .And.Contain("CentralDocumentEvidenceRules.CurrentPublished()")
            .And.Contain("db.AuditLogs.Add")
            .And.Contain("ClientRequestId")
            .And.Contain("CryptographicOperations.FixedTimeEquals");
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
