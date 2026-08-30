using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class ProjectAssetLinkFixedAssetReconciliationMigrationGuardTests
{
    [Fact]
    public void Migration_extends_the_existing_project_link_with_tenant_safe_fixed_asset_lineage_only()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", "20260822003500_AddProjectAssetLinkFixedAssetReconciliation.cs"));
        var metadata = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", "FastBuildMigrationMetadata.cs"));
        var preflight = File.ReadAllText(Path.Combine(root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));

        migration.Should().Contain("ProjectAssetLinks")
            .And.Contain("FixedAssetId")
            .And.Contain("ReconciliationKey")
            .And.Contain("FixedAssets(Id)")
            .And.Contain("TR_ProjectAssetLinks_ReconciliationLineage")
            .And.Contain("52155")
            .And.NotContain("CREATE TABLE dbo.FixedAssets")
            .And.NotContain("FinanceJournal")
            .And.NotContain("GeneralLedger");
        metadata.Should().Contain("20260822003500_AddProjectAssetLinkFixedAssetReconciliation");
        preflight.Should().Contain("GUARD_COVERAGE|20260822003500_AddProjectAssetLinkFixedAssetReconciliation");
    }

    [Fact]
    public void Existing_project_workspace_uses_a_finance_fixed_asset_selector_and_the_service_uses_retry_safe_tenant_validation()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "Projects", "ProjectServices.cs"));
        var panel = File.ReadAllText(Path.Combine(root, "frontend", "src", "app", "development", "projects", "[id]", "components", "ProjectAccessTab.tsx"));

        service.Should().Contain("BuildAssetLinkReconciliationKey")
            .And.Contain("The selected fixed asset is not available in this tenant")
            .And.Contain("The selected job card does not belong to the selected maintenance asset")
            .And.Contain("HasFixedAsset");
        panel.Should().Contain("Finance Fixed Asset")
            .And.Contain("fixedAssetId")
            .And.NotContain("Label>Company Asset");
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
