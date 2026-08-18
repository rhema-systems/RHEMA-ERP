using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyRemeasurementGuardTests
{
    [Fact]
    public void Migration_is_narrow_discoverable_and_guards_immutable_measurement_lineage()
    {
        var root = FindRepositoryRoot();
        const string migrationId = "20260810040000_AddProjectBoqRemeasurementWorkflow";
        var migration = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", migrationId + ".cs"));
        var preflight = File.ReadAllText(Path.Combine(root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));
        migration.Should().Contain($"[Migration(\"{migrationId}\")] ".Trim());
        Count(migration, "CREATE TABLE [ProjectBoqRemeasurement").Should().Be(3);
        migration.Should().Contain("TR_ProjectBoqRemeasurementRevisions_Guard");
        migration.Should().Contain("TR_ProjectBoqRemeasurementLines_Guard");
        migration.Should().Contain("TR_ProjectBoqRemeasurementSources_Guard");
        migration.Should().Contain("Finalized remeasurement counts, deltas and measured quantities must reconcile exactly.");
        migration.Should().Contain("[VersionType]=4");
        migration.Should().Contain("[Status]='Recorded'");
        migration.Should().NotContain("FinanceSettings");
        migration.Should().NotContain("ProcurementSupplier");
        preflight.Should().Contain($"GUARD_COVERAGE|{migrationId}");
    }

    [Fact]
    public void Service_reuses_project_boq_workflow_publication_and_safe_retry_controls()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "Projects",
            "ProjectService.BoqRemeasurements.cs"));
        var approval = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "Projects",
            "ProjectService.BoqApproval.cs"));
        service.Should().Contain("ValidateBoqVersionCreation(QuantitySurveyBoqVersionType.Remeasurement");
        service.Should().Contain("IsolationLevel.Serializable");
        service.Should().Contain("ClientRequestId");
        service.Should().Contain("RequestHash");
        service.Should().Contain("ValidateRemeasurementVersionAsync");
        service.Should().Contain("ApplyApprovedRemeasurementToWorkingBoqAsync");
        service.Should().NotContain("SubmitAsync(");
        approval.Should().Contain("ApplyApprovedRemeasurementToWorkingBoqAsync(candidate, correlationId)");
        approval.Should().Contain("QuantitySurveyWorkflowBindingRegistry.Boq");
    }

    [Fact]
    public void Frontend_uses_controlled_recorded_sheet_selection_and_stable_retry_identity()
    {
        var frontend = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "frontend", "src", "components",
            "quantity-survey", "QuantitySurveyBoqVersionDialog.tsx"));
        frontend.Should().Contain("eligibleMeasurements.map");
        frontend.Should().Contain("<Checkbox");
        frontend.Should().Contain("remeasurementRequestIds.current.get(fingerprint)");
        frontend.Should().Contain("measurementSheetIds: selected");
        frontend.Should().Contain("routed through the existing BoQ");
        frontend.Should().Contain("approval and publication workflow");
        frontend.Should().NotContain("measurementSheetId\" type=\"text");
    }

    private static int Count(string value, string token) => value.Split(token, StringSplitOptions.None).Length - 1;

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
