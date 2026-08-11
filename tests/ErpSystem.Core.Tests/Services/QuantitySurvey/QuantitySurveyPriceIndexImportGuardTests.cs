using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyPriceIndexImportGuardTests
{
    [Fact]
    public void Service_reuses_central_controls_and_blocks_unsupported_manual_API_imports()
    {
        var source = ServiceSource();

        source.Should().Contain("IControlledFileUploadService");
        source.Should().Contain("ICentralDocumentRepositoryFileService");
        source.Should().Contain("IWorkflowIntegrationService");
        source.Should().Contain("QuantitySurveyWorkflowBindingRegistry.Escalation");
        source.Should().Contain("configured API import format requires a provider adapter");
        source.Should().Contain("FileVirusScanStatus.Clean");
        source.Should().Contain("upload.ChecksumSha256");
        source.Should().Contain("value.Status == \"Validated\"");
        source.Should().Contain("value.DocumentRecord.CurrentVersion == value.VersionNumber");
        source.Should().Contain("value.DocumentRecord.VersionStatus == \"Validated\"");
        source.Should().Contain("Document type: PriceIndexSource");
    }

    [Fact]
    public void Service_enforces_tenant_policy_idempotency_integrity_and_maker_checker()
    {
        var source = ServiceSource();

        source.Should().Contain("value.TenantId == TenantId");
        source.Should().Contain("value.ClientRequestId == request.ClientRequestId");
        source.Should().Contain("StageRequestMatchesAsync");
        source.Should().Contain("QuantitySurveyAuditEventMap.StagePriceIndexImport");
        source.Should().Contain("FixedEquals(entity.NormalizedPayloadHash");
        source.Should().Contain("entity.PreparedById == UserId");
        source.Should().Contain("preparer from rejecting it");
        source.Should().Contain("RequireAuthorityRole(entity)");
        source.Should().Contain("IsolationLevel.Serializable");
        source.Should().Contain("await transaction.CommitAsync(cancellationToken)");
        source.Should().Contain("Retire current rows first inside the same serializable transaction");
        source.Should().Contain("Persist the parent terminal state before its SQL-guarded child");
        source.Should().Contain("Persist the parent terminal state before the SQL-guarded value");
        source.Should().Contain("if (current.Count > 0)");
        source.Should().Contain("await SaveAsync(cancellationToken)");
        source.Should().Contain("Another approved index revision became current");
        source.Should().Contain("WorkflowInstanceStatus.Completed");
        source.Should().Contain("WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed");
        source.Should().Contain("entity.RejectionReason = reason");
    }

    [Fact]
    public void Invalid_source_rows_are_reported_without_persisting_database_placeholders()
    {
        var source = ServiceSource();

        source.Should().Contain(".Where(value => IsPersistable(value.Row))");
        source.Should().Contain(".GroupBy(value => value.Row.IndexPeriod)");
        source.Should().Contain("FirstDataRow + MaximumRows - 1");
        source.Should().Contain("PERIOD_INVALID");
        source.Should().Contain("VALUE_INVALID");
        source.Should().Contain("VALUE_OUT_OF_RANGE");
        source.Should().Contain("VALUE_PRECISION");
        source.Should().Contain("PUBLICATION_DATE_INVALID");
        source.Should().Contain("PERIOD_FUTURE");
        source.Should().Contain("PUBLICATION_DATE_BEFORE_PERIOD");
        source.Should().Contain("SOURCE_REFERENCE_INVALID");
        source.Should().Contain("PERIOD_DUPLICATE");
        source.Should().Contain("CSV_QUOTE_INVALID");
        source.Should().Contain("CSV source must use valid UTF-8 text encoding");
        source.Should().Contain("Source file name is required and cannot exceed 255 characters");
        source.Should().Contain("controlled source file could not be read");
    }

    [Fact]
    public void Migration_is_QS_only_discoverable_and_contains_database_hard_stops()
    {
        var root = FindRepositoryRoot();
        var migrationId = "20260809165157_AddQuantitySurveyPriceIndexImportWorkflow";
        var source = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Data", "Migrations", migrationId + ".cs"));
        var preflight = File.ReadAllText(Path.Combine(
            root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));

        source.Should().Contain($"[Migration(\"{migrationId}\")]");
        source.Should().Contain("TR_QsPriceIndexImportBatches_Guard");
        source.Should().Contain("TR_QsPriceIndexValues_Guard");
        source.Should().Contain("TR_QsPriceIndexImportRevisions_AppendOnly");
        source.Should().Contain("TR_QsPriceIndexFamilies_ImportInUseGuard");
        source.Should().Contain("CK_QsPriceIndexValues_Publication");
        source.Should().Contain("QS-DEC-001");
        source.Should().Contain("QS-DEC-006");
        source.Should().Contain("QS_ESCALATION");
        source.Should().Contain("FileUploadRecords");
        source.Should().Contain("VirusScanStatus");
        source.Should().Contain("dv.[Status] = 'Validated'");
        source.Should().Contain("i.[Status] = 'Invalid' AND dv.[Status] = 'Validation failed'");
        source.Should().Contain("dr.[CurrentVersion] = dv.[VersionNumber]");
        source.Should().Contain("i.[Status] <> 'Rejected' AND (");
        source.Should().Contain("i.[Status] IN ('PendingApproval','Approved') THEN CONVERT(date, SYSUTCDATETIME())");
        source.Should().Contain("dr.[Notes] LIKE 'Document type: PriceIndexSource%'");
        source.Should().Contain("i.[LastModifiedById] = i.[PreparedById]");
        source.Should().Contain("i.[ApprovedById] <> i.[LastModifiedById]");
        source.Should().Contain("NULLIF(LTRIM(RTRIM(i.[RejectionReason])), '') IS NULL");
        source.Should().Contain("i.[IsDeleted] = 1 OR NOT");
        source.Should().NotContain("EstateManagedAssets");
        source.Should().NotContain("FinanceSettings");
        preflight.Should().Contain($"GUARD_COVERAGE|{migrationId}");
    }

    private static string ServiceSource() => File.ReadAllText(Path.Combine(
        FindRepositoryRoot(), "src", "ErpSystem.Api", "Services", "QuantitySurvey",
        "QuantitySurveyPriceIndexImportService.cs"));

    private static string FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory;
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
