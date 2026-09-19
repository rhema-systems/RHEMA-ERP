using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementReceiptInspectionReplayAndQueueTests
{
    [Fact]
    public void Save_fingerprint_is_stable_but_binds_the_complete_line_payload()
    {
        var firstLineId = Guid.NewGuid();
        var secondLineId = Guid.NewGuid();
        var first = SaveRequest("AQID", firstLineId, secondLineId, 7m);
        var reorderedRetry = SaveRequest("different-stale-token", firstLineId,
            secondLineId, 7m);
        reorderedRetry.Lines.Reverse();

        var originalFingerprint =
            ProcurementReceiptInspectionService.SaveActionFingerprint(first);

        ProcurementReceiptInspectionService.SaveActionFingerprint(reorderedRetry)
            .Should().Be(originalFingerprint,
                "row version and collection order are not business payload");

        var changed = SaveRequest("AQID", firstLineId, secondLineId, 6m);
        ProcurementReceiptInspectionService.SaveActionFingerprint(changed)
            .Should().NotBe(originalFingerprint,
                "a changed line quantity must conflict under the same idempotency key");
    }

    [Fact]
    public void Lifecycle_fingerprint_binds_reference_comment_resolution_and_evidence()
    {
        var evidenceId = Guid.NewGuid();
        var original = ResolutionRequest(evidenceId, "DMS-0502");
        var sameRetry = ResolutionRequest(evidenceId, "DMS-0502");
        sameRetry.RowVersion = "stale-after-first-commit";
        var changedEvidence = ResolutionRequest(evidenceId, "DMS-CHANGED");

        var fingerprint = ProcurementReceiptInspectionService
            .ResolutionActionFingerprint("resolve", original);

        ProcurementReceiptInspectionService
            .ResolutionActionFingerprint("resolve", sameRetry)
            .Should().Be(fingerprint);
        ProcurementReceiptInspectionService
            .ResolutionActionFingerprint("resolve", changedEvidence)
            .Should().NotBe(fingerprint);
        ProcurementReceiptInspectionService
            .ResolutionActionFingerprint("close", original)
            .Should().NotBe(fingerprint,
                "the same client key cannot cross lifecycle operations");
    }

    [Fact]
    public void Replay_requires_the_same_case_action_and_payload_fingerprint()
    {
        var caseId = Guid.NewGuid();
        var action = new ProcurementReceiptInspectionAction
        {
            InspectionCaseId = caseId,
            ActionType = ProcurementReceiptInspectionActionType.Saved,
            RequestFingerprint = new string('a', 64)
        };

        var identical = () => ProcurementReceiptInspectionService
            .EnsureActionReplayMatches(action, caseId,
                ProcurementReceiptInspectionActionType.Saved, new string('a', 64));
        identical.Should().NotThrow();

        var mismatch = () => ProcurementReceiptInspectionService
            .EnsureActionReplayMatches(action, caseId,
                ProcurementReceiptInspectionActionType.Saved, new string('b', 64));
        mismatch.Should().Throw<ProcurementReceiptInspectionConflictException>()
            .Where(exception =>
                exception.Code == "RCV_ACTION_IDEMPOTENCY_PAYLOAD_MISMATCH");
    }

    [Fact]
    public void Submission_requires_the_exact_configured_evidence_set()
    {
        var required = new[] { "INSPECTION_REPORT", "DELIVERY_NOTE" };
        var valid = EvidenceRequests(
            ("SubmitReceiptInspection", "INSPECTION_REPORT"),
            ("SubmitReceiptInspection", "DELIVERY_NOTE"));

        var accepted = () => ProcurementReceiptInspectionService
            .EnsureConfiguredEvidenceRequirements(
                "SubmitReceiptInspection", required, valid);
        accepted.Should().NotThrow();

        var missing = () => ProcurementReceiptInspectionService
            .EnsureConfiguredEvidenceRequirements(
                "SubmitReceiptInspection", required,
                EvidenceRequests(("SubmitReceiptInspection", "INSPECTION_REPORT")));
        missing.Should().Throw<ProcurementReceiptInspectionValidationException>()
            .Where(exception => exception.Code == "RCV_EVIDENCE_REQUIREMENTS_MISMATCH");

        var unexpected = () => ProcurementReceiptInspectionService
            .EnsureConfiguredEvidenceRequirements(
                "SubmitReceiptInspection", required,
                EvidenceRequests(
                    ("SubmitReceiptInspection", "INSPECTION_REPORT"),
                    ("SubmitReceiptInspection", "PACKING_LIST")));
        unexpected.Should().Throw<ProcurementReceiptInspectionValidationException>()
            .Where(exception => exception.Code == "RCV_EVIDENCE_REQUIREMENTS_MISMATCH");

        var wrongAction = () => ProcurementReceiptInspectionService
            .EnsureConfiguredEvidenceRequirements(
                "SubmitReceiptInspection", required,
                EvidenceRequests(
                    ("OtherAction", "INSPECTION_REPORT"),
                    ("OtherAction", "DELIVERY_NOTE")));
        wrongAction.Should().Throw<ProcurementReceiptInspectionValidationException>()
            .Where(exception => exception.Code == "RCV_EVIDENCE_REQUIREMENTS_MISMATCH");
    }

    [Theory]
    [InlineData(ProcurementReceiptResolutionKind.Return,
        ProcurementReceiptResolutionStatus.Required, "ReturnAuthorization")]
    [InlineData(ProcurementReceiptResolutionKind.Return,
        ProcurementReceiptResolutionStatus.Authorized, "ReturnDispatch")]
    [InlineData(ProcurementReceiptResolutionKind.Replacement,
        ProcurementReceiptResolutionStatus.Required, "ReplacementRequest")]
    [InlineData(ProcurementReceiptResolutionKind.Replacement,
        ProcurementReceiptResolutionStatus.ReplacementRequested, "ReplacementReceipt")]
    public void Resolution_evidence_action_key_is_bound_to_the_actual_stage(
        ProcurementReceiptResolutionKind kind,
        ProcurementReceiptResolutionStatus status,
        string expected)
    {
        ProcurementReceiptInspectionService.ResolutionEvidenceActionKey(kind, status)
            .Should().Be(expected);

        var wrongAction = () => ProcurementReceiptInspectionService
            .EnsureEvidenceActionKey(expected,
                EvidenceRequests(("ResolutionEvidence", "INSPECTION_REPORT")));
        wrongAction.Should().Throw<ProcurementReceiptInspectionValidationException>()
            .Where(exception => exception.Code == "RCV_EVIDENCE_ACTION_MISMATCH");
    }

    [Fact]
    public void Supplier_queue_is_bounded_and_set_loaded_without_overview_n_plus_one()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementReceiptInspectionService.cs");
        var start = source.IndexOf("GetSupplierOverviewAsync(",
            StringComparison.Ordinal);
        var end = source.IndexOf("public async Task<ProcurementReceiptInspectionDto> InitializeAsync",
            start, StringComparison.Ordinal);
        var queue = source[start..end];

        queue.Should().Contain("Math.Clamp(pageSize, 1, 50)");
        queue.Should().Contain(".GroupBy(item => item.PurchaseOrderReceiptId)");
        queue.Should().Contain(".Skip(skip)");
        queue.Should().Contain(".Take(boundedPageSize)");
        queue.Should().Contain(".AsSplitQuery()");
        queue.Should().Contain("profileIds.Contains(item.ProfileId)");
        queue.Should().NotContain("GetOverviewAsync(receiptId",
            "the page must not execute a full overview query per receipt");
    }

    [Fact]
    public void Fingerprint_migration_preserves_legacy_rows_and_immutable_action_protection()
    {
        var migration = ReadRepositoryFile(
            "src", "ErpSystem.Data", "LegacyMigrationsArchive",
            "20260801220000_TDC0502ReceiptInspectionActionFingerprints.cs");
        var originalLifecycleMigration = ReadRepositoryFile(
            "src", "ErpSystem.Data", "LegacyMigrationsArchive",
            "20260731140000_TDC0502ReceiptInspectionClosure.cs");

        migration.Should().Contain("name: \"RequestFingerprint\"");
        migration.Should().Contain("nullable: true");
        migration.Should().Contain(
            "[RequestFingerprint] IS NULL OR LEN([RequestFingerprint]) = 64");
        originalLifecycleMigration.Should().Contain(
            "TR_ProcurementReceiptInspectionActions_TDC0502Immutable");
        originalLifecycleMigration.Should().Contain(
            "IF EXISTS (SELECT 1 FROM deleted)",
            "the whole-row trigger rejects every action update, including fingerprint changes");
    }

    private static SaveProcurementReceiptInspectionRequest SaveRequest(
        string rowVersion,
        Guid firstLineId,
        Guid secondLineId,
        decimal firstAcceptedQuantity) => new()
    {
        Comment = "  inspected  ",
        IdempotencyKey = "save-0502",
        RowVersion = rowVersion,
        Lines =
        [
            new ProcurementReceiptInspectionLineRequest
            {
                PurchaseOrderReceiptItemId = firstLineId,
                AcceptedQuantity = firstAcceptedQuantity,
                RejectedQuantity = 3m,
                RejectionReason = "  damaged  ",
                InspectionNotes = "  checked  ",
                QuarantineLocationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
            },
            new ProcurementReceiptInspectionLineRequest
            {
                PurchaseOrderReceiptItemId = secondLineId,
                AcceptedQuantity = 5m,
                RejectedQuantity = 0m
            }
        ]
    };

    private static ProcurementReceiptResolutionRequest ResolutionRequest(
        Guid evidenceId,
        string evidenceReference) => new()
    {
        ResolutionKind = ProcurementReceiptResolutionKind.Return,
        Reference = " RETURN-0502 ",
        Comment = " Dispatch returned goods. ",
        IdempotencyKey = "resolve-0502",
        RowVersion = "AQID",
        Evidence =
        [
            new ProcurementReceiptInspectionEvidenceRequest
            {
                ActionKey = " ReturnDispatch ",
                RequirementKey = " DELIVERY_NOTE ",
                ReferenceKind = ProcurementReceiptInspectionEvidenceKind.CentralDocumentUpload,
                FileUploadRecordId = evidenceId,
                EvidenceReference = evidenceReference
            }
        ]
    };

    private static List<ProcurementReceiptInspectionEvidenceRequest> EvidenceRequests(
        params (string ActionKey, string RequirementKey)[] rows) => rows
        .Select((row, index) => new ProcurementReceiptInspectionEvidenceRequest
        {
            ActionKey = row.ActionKey,
            RequirementKey = row.RequirementKey,
            ReferenceKind = ProcurementReceiptInspectionEvidenceKind.CentralDocumentUpload,
            FileUploadRecordId = Guid.NewGuid(),
            EvidenceReference = $"DMS-{index + 1}"
        })
        .ToList();

    private static string ReadRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
            directory = directory.Parent;
        directory.Should().NotBeNull("the test must run inside the ERP repository");
        return File.ReadAllText(Path.Combine([directory!.FullName, .. parts]));
    }
}
