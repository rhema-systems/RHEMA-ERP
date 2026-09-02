using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringMaintenanceAssessmentMigrationGuardTests
{
    [Fact]
    public void Migration_enforces_frozen_intake_lineage_workflow_stages_and_append_only_history()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", "20260821050000_AddCivilEngineeringMaintenanceAssessments.cs"));

        migration.Should().Contain("CK_CivilEngineeringMaintenanceAssessments_Assignment")
            .And.Contain("TR_CivilEngineeringMaintenanceAssessments_Lineage")
            .And.Contain("TR_CivilEngineeringMaintenanceAssessments_Lifecycle")
            .And.Contain("CentralDocumentVersions")
            .And.Contain("CivilEngineeringMaintenanceIntakes")
            .And.Contain("TR_CivilEngineeringMaintenanceAssessmentRevisions_AppendOnly")
            .And.Contain("ClientRequestId");
        migration.Should().Contain("[Migration(\"20260821050000_AddCivilEngineeringMaintenanceAssessments\")]");
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
