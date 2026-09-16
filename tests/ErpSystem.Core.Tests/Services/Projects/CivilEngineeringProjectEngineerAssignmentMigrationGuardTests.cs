using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringProjectEngineerAssignmentMigrationGuardTests
{
    private const string MigrationId = "20260820150000_AddCivilEngineeringProjectEngineerAssignments";

    [Fact]
    public void Migration_is_scoped_and_protects_tenant_policy_lifecycle_and_append_only_history()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "LegacyMigrationsArchive", MigrationId + ".cs"));
        var preflight = File.ReadAllText(Path.Combine(root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));

        migration.Split("migrationBuilder.CreateTable(").Length.Should().Be(3);
        migration.Should().Contain("name: \"ProjectCivilProjectEngineerAssignments\"")
            .And.Contain("name: \"ProjectCivilProjectEngineerAssignmentRevisions\"")
            .And.Contain("\"Projects\", \"Id\"")
            .And.Contain("\"ProjectMembers\", \"Id\"")
            .And.Contain("\"Users\", \"Id\"")
            .And.Contain("CivilEngineeringConfigurationDecisions")
            .And.Contain("[IsDeleted] = 0 AND [IsActive] = 1")
            .And.Contain("TR_ProjectCivilProjectEngineerAssignments_Lineage")
            .And.Contain("TR_ProjectCivilProjectEngineerAssignments_Lifecycle")
            .And.Contain("TR_ProjectCivilProjectEngineerAssignmentRevisions_AppendOnly")
            .And.Contain("CIV-CFG-005")
            .And.Contain("profile.LifecycleStatus <> 1")
            .And.Contain("decision.Status <> 2")
            .And.Contain("decision.ApprovalStatus <> 1")
            .And.Contain("decision.EvidenceStatus <> 2")
            .And.Contain($"[Migration(\"{MigrationId}\")]");
        migration.Should().NotContain("AddColumn")
            .And.NotContain("DropColumn")
            .And.NotContain("File.WriteAllBytes");
        preflight.Should().Contain($"GUARD_COVERAGE|{MigrationId}");
    }

    [Fact]
    public void Runtime_reuses_projects_hr_configuration_audit_and_serializable_idempotency_controls()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Services", "CivilEngineeringSupervisionService.cs"));
        var panel = File.ReadAllText(Path.Combine(root, "frontend", "src", "components", "projects", "civil-engineering", "CivilEngineeringProjectEngineerAssignmentsPanel.tsx"));

        source.Should().Contain("IsolationLevel.Serializable")
            .And.Contain("ClientRequestId")
            .And.Contain("CryptographicOperations.FixedTimeEquals")
            .And.Contain("RequireProjectAsync")
            .And.Contain("ResolvePolicyAsync")
            .And.Contain("CivilEngineeringAccessControlRegistry.ProjectEngineerRole")
            .And.Contain("db.AuditLogs.Add")
            .And.Contain("DeactivateProjectEngineerMembershipAsync")
            .And.Contain("sql.Number is >= 52001 and <= 52005")
            .And.NotContain("new WorkflowInstance");
        panel.Should().Contain("Select an eligible project member")
            .And.Contain("Appointment authority")
            .And.Contain("Replacement reason")
            .And.NotContain("type=\"text\"");
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
