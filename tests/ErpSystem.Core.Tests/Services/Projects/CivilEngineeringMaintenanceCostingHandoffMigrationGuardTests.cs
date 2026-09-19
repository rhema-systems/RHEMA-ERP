using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringMaintenanceCostingHandoffMigrationGuardTests
{
    [Fact]
    public void Migration_enforces_cross_owner_lineage_lifecycle_and_append_only_history()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "LegacyMigrationsArchive", "20260821053000_AddCivilEngineeringMaintenanceCostingHandoffs.cs"));
        migration.Should().Contain("QuantitySurveyEstimateVersions")
            .And.Contain("ProjectBudgetRevisions")
            .And.Contain("PurchaseRequisitions")
            .And.Contain("Contracts")
            .And.Contain("TR_CivilEngineeringMaintenanceCostingHandoffs_Lineage")
            .And.Contain("TR_CivilEngineeringMaintenanceCostingHandoffs_Lifecycle")
            .And.Contain("TR_CivilEngineeringMaintenanceCostingHandoffRevisions_AppendOnly")
            .And.Contain("ClientRequestId");
        migration.Should().Contain("[Migration(\"20260821053000_AddCivilEngineeringMaintenanceCostingHandoffs\")]");
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
