using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringMaintenanceIntakeMigrationGuardTests
{
    [Fact]
    public void Migration_enforces_tenant_bound_source_dms_workflow_and_append_only_history()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "LegacyMigrationsArchive", "20260821033000_AddCivilEngineeringMaintenanceIntakes.cs"));

        migration.Should().Contain("CK_CivilEngineeringMaintenanceIntakes_Target")
            .And.Contain("CK_CivilEngineeringMaintenanceIntakes_SourceLink")
            .And.Contain("TR_CivilEngineeringMaintenanceIntakes_Lineage")
            .And.Contain("WorkflowEntityTypes")
            .And.Contain("CentralDocumentVersions")
            .And.Contain("TR_CivilEngineeringMaintenanceIntakeRevisions_AppendOnly")
            .And.Contain("ClientRequestId");
        migration.Should().Contain("[Migration(\"20260821033000_AddCivilEngineeringMaintenanceIntakes\")]");
    }

    private static string FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
