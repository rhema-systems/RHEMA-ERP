using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyMeasurementGuardTests
{
    [Fact]
    public void Migration_is_narrow_discoverable_and_guards_full_lineage()
    {
        var root = FindRepositoryRoot();
        const string migrationId = "20260810023000_AddQuantitySurveyMeasurementSheets";
        var migration = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", migrationId + ".cs"));
        var preflight = File.ReadAllText(Path.Combine(root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));
        migration.Should().Contain($"[Migration(\"{migrationId}\")] ".Trim());
        Count(migration, "CREATE TABLE [QuantitySurveyMeasurement").Should().Be(4);
        migration.Should().Contain("TR_QsMeasurementSheets_Guard");
        migration.Should().Contain("TR_QsMeasurementLines_Guard");
        migration.Should().Contain("TR_QsMeasurementAttachments_Guard");
        migration.Should().Contain("TR_QsMeasurementRevisions_Guard");
        migration.Should().Contain("ProjectBoqVersions");
        migration.Should().Contain("QS-DEC-007");
        migration.Should().Contain("CentralDocumentMetadataTemplates");
        migration.Should().Contain("quantity-survey-measurement-evidence");
        migration.Should().Contain("exact server-calculated typed formula");
        migration.Should().Contain("revision history is append-only");
        migration.Should().NotContain("FinanceSettings");
        migration.Should().NotContain("ProcurementSupplier");
        preflight.Should().Contain($"GUARD_COVERAGE|{migrationId}");
    }

    [Fact]
    public void Service_reuses_projects_policy_scan_dms_audit_and_retry_controls()
    {
        var service = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "ErpSystem.Api", "Services",
            "QuantitySurvey", "QuantitySurveyMeasurementService.cs"));
        service.Should().Contain("IProjectService");
        service.Should().Contain("IControlledFileUploadService");
        service.Should().Contain("ICentralDocumentRepositoryFileService");
        service.Should().Contain("IsolationLevel.Serializable");
        service.Should().Contain("ProjectBoqVersionStatuses.Approved");
        service.Should().Contain("ProjectDrawingStatuses.ApprovedForConstruction");
        service.Should().Contain("DecisionKey == \"QS-DEC-007\"");
        service.Should().Contain("FileVirusScanStatus.Clean");
        service.Should().Contain("RequirePublishedGovernance = true");
        service.Should().Contain("DocumentType = sheet.EvidenceMetadataTemplate.DocumentType");
        service.Should().NotContain("DocumentType = request.EvidenceType.ToString()");
        service.Should().Contain("ApplyRowVersion(entity, request.RowVersion)");
        service.Should().Contain("FixedEquals(existing.RequestHash, requestHash)");
        service.Should().NotContain("new Project(");
        service.Should().NotContain("new ProjectBoqVersion");
        service.Should().NotContain("new CentralDocumentRecord");
    }

    [Fact]
    public void Frontend_uses_controlled_selectors_and_stable_retry_ids()
    {
        var frontend = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "frontend", "src", "components",
            "quantity-survey", "QuantitySurveyMeasurementsDialog.tsx"));
        frontend.Should().Contain("approvedBoqLines.map");
        frontend.Should().Contain("approvedDrawings.map");
        frontend.Should().Contain("quantity-survey.measurements.manage");
        frontend.Should().Contain("quantity-survey.audit.read");
        frontend.Should().Contain("current?.fingerprint === fingerprint");
        frontend.Should().Contain("completeRequest(key)");
        frontend.Should().NotContain("ProjectBoqVersionLineId\" type=\"text");
    }

    private static int Count(string value, string token) =>
        value.Split(token, StringSplitOptions.None).Length - 1;

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
