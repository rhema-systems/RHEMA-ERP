using FluentAssertions;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Services.Projects;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringExtensionOfTimeMigrationGuardTests
{
    [Fact]
    public void Migration_reuses_projects_eot_qs_variation_contract_workflow_and_central_dms_without_financial_duplication()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "LegacyMigrationsArchive", "20260822003400_AddCivilEngineeringExtensionOfTimeControls.cs"));
        var preflight = File.ReadAllText(Path.Combine(root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));

        migration.Should().Contain("ProjectExtensionOfTimeRequests(Id)")
            .And.Contain("ProjectVariationOrders(Id)")
            .And.Contain("Contracts(Id)")
            .And.Contain("CentralDocumentRecords(Id)")
            .And.Contain("CentralDocumentVersions(Id)")
            .And.Contain("PROJECT_CIVIL_EXTENSION_OF_TIME")
            .And.Contain("CIV-CFG-014")
            .And.Contain("TR_ProjectCivilExtensionOfTimeControls_Lineage")
            .And.Contain("TR_ProjectCivilExtensionOfTimeControls_Lifecycle")
            .And.Contain("TR_ProjectCivilExtensionOfTimeRevisions_AppendOnly")
            .And.Contain("TR_ProjectExtensionOfTimeRequests_CivilControl")
            .And.Contain("52153")
            .And.Contain("[Migration(\"20260822003400_AddCivilEngineeringExtensionOfTimeControls\")]")
            .And.Contain("[DbContext(typeof(ApplicationDbContext))]")
            .And.NotContain("FinanceJournal")
            .And.NotContain("GeneralLedger");
        preflight.Should().Contain("GUARD_COVERAGE|20260822003400_AddCivilEngineeringExtensionOfTimeControls");
    }

    [Fact]
    public void Runtime_uses_controlled_selectors_shared_workflow_replay_protection_and_blocks_legacy_works_eot_bypass()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Services", "CivilEngineeringExtensionOfTimeService.cs"));
        var projectService = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "Projects", "ProjectService.CommercialAdministration.cs"));
        var panel = File.ReadAllText(Path.Combine(root, "frontend", "src", "components", "projects", "civil-engineering", "CivilEngineeringExtensionOfTimePanel.tsx"));
        var commercialPanel = File.ReadAllText(Path.Combine(root, "frontend", "src", "app", "development", "projects", "[id]", "components", "ProjectCommercialAdminTab.tsx"));

        service.Should().Contain("IsolationLevel.Serializable")
            .And.Contain("RequireProjectWorksContractAsync")
            .And.Contain("RequireVariationAsync")
            .And.Contain("CentralDocumentEvidenceRules.CurrentPublished")
            .And.Contain("workflow.SubmitAsync")
            .And.Contain("workflow.ProcessApprovalAsync")
            .And.Contain("CryptographicOperations.FixedTimeEquals")
            .And.Contain("db.AuditLogs.Add");
        projectService.Should().Contain("EnsureLegacyExtensionOfTimeIsNotWorks")
            .And.Contain("governed Civil workflow");
        panel.Should().Contain("Works contract")
            .And.Contain("applied QS variation")
            .And.Contain("Published DMS evidence")
            .And.NotContain("Contract ID");
        commercialPanel.Should().Contain("Works extensions of time are governed")
            .And.Contain("Site controls → Civil variation and extension of time");
    }

    [Fact]
    public void Legacy_works_eot_create_is_blocked_by_the_runtime_guard()
    {
        var action = () => ProjectService.EnsureLegacyExtensionOfTimeIsNotWorks(
            new Contract { ContractType = "Works" });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*governed Civil workflow*");
    }

    [Fact]
    public void Works_variation_create_remains_available_while_only_legacy_eot_create_is_guarded()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "Projects", "ProjectService.CommercialAdministration.cs"));
        var variation = MethodBody(source, "AddProjectVariationOrderAsync", "UpdateProjectVariationOrderAsync");
        var eot = MethodBody(source, "AddProjectExtensionOfTimeRequestAsync", "UpdateProjectExtensionOfTimeRequestAsync");

        variation.Should().NotContain("EnsureLegacyExtensionOfTimeIsNotWorks");
        eot.Should().Contain("EnsureLegacyExtensionOfTimeIsNotWorks(contract)");
    }

    private static string MethodBody(string source, string method, string nextMethod)
    {
        var start = source.IndexOf(method, StringComparison.Ordinal);
        var end = source.IndexOf(nextMethod, start, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);
        end.Should().BeGreaterThan(start);
        return source[start..end];
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
