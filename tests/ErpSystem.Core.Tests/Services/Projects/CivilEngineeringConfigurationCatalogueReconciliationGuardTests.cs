using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringConfigurationCatalogueReconciliationGuardTests
{
    [Fact]
    public void Forward_repair_is_draft_only_audited_and_registered_for_deployment()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", "20260822003600_ReconcileCivilEngineeringConfigurationDecisionCatalogue.cs"));
        var seeder = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Seeders", "CivilEngineeringConfigurationProfileSeeder.cs"));
        var preflight = File.ReadAllText(Path.Combine(root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));

        migration.Should().Contain("CIV-CFG-014")
            .And.Contain("profile.LifecycleStatus = 0")
            .And.Contain("CivilEngineeringConfigurationRevisions")
            .And.Contain("unapproved draft")
            .And.Contain("THROW 52161")
            .And.NotContain("profile.LifecycleStatus = 1");
        seeder.Should().Contain("CivilEngineeringConfigurationDecisionRegistry.Definitions")
            .And.Contain("IgnoreQueryFilters")
            .And.Contain("SupersedesProfileId = latest?.Id")
            .And.Contain("independent reapproval required");
        migration.Should().Contain("[Migration(\"20260822003600_ReconcileCivilEngineeringConfigurationDecisionCatalogue\")]");
        preflight.Should().Contain("GUARD_COVERAGE|20260822003600_ReconcileCivilEngineeringConfigurationDecisionCatalogue");
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
