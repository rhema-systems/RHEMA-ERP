using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260801114500_TDC0509IssuedSnapshotTransition")]
public sealed class TDC0509IssuedSnapshotTransition : Migration
{
    private const string IssuedSnapshotMutationPredicate =
        "((i.SourceSnapshotJson <> d.SourceSnapshotJson OR i.SourceIntegrityHash <> d.SourceIntegrityHash) " +
        "AND NOT (d.Status IN (0,1) AND i.Status = 2))";

    private const string ImmutableSnapshotMutationPredicate =
        "(i.SourceSnapshotJson <> d.SourceSnapshotJson OR i.SourceIntegrityHash <> d.SourceIntegrityHash)";

    protected override void Up(MigrationBuilder migrationBuilder) =>
        CreateProtectionTrigger(migrationBuilder, IssuedSnapshotMutationPredicate);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        CreateProtectionTrigger(migrationBuilder, ImmutableSnapshotMutationPredicate);

    private static void CreateProtectionTrigger(
        MigrationBuilder migrationBuilder,
        string sourceMutationPredicate)
    {
        migrationBuilder.Sql(
            $$"""
            CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementReceiptDocuments_TDC0509Protected]
            ON [dbo].[ProcurementReceiptDocuments]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                    THROW 51670, 'RCV_DOCUMENT_DELETE_DENIED: receipt-document register entries are immutable history.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i
                    LEFT JOIN PurchaseOrderReceipts r ON r.Id = i.PurchaseOrderReceiptId AND r.TenantId = i.TenantId AND r.IsDeleted = 0
                    LEFT JOIN GoodsReceiptNotes g ON g.Id = i.GoodsReceiptNoteId AND g.TenantId = i.TenantId AND g.PurchaseOrderReceiptId = i.PurchaseOrderReceiptId AND g.IsDeleted = 0
                    LEFT JOIN ProcurementConfigurationProfiles p ON p.Id = i.ConfigurationProfileId AND p.TenantId = i.TenantId AND p.Version = i.ConfigurationProfileVersion AND p.IsDeleted = 0
                    LEFT JOIN ProcurementConfigurationDecisions c ON c.Id = i.ConfigurationDecisionId AND c.TenantId = i.TenantId AND c.ProfileId = p.Id AND c.DecisionKey = 'DEC-013' AND c.IsDeleted = 0
                    WHERE r.Id IS NULL OR p.Id IS NULL OR c.Id IS NULL OR (i.GoodsReceiptNoteId IS NOT NULL AND g.Id IS NULL) OR
                          (NOT EXISTS (SELECT 1 FROM deleted d WHERE d.Id = i.Id) AND
                           (p.LifecycleStatus <> 1 OR c.Status <> 2 OR c.ApprovalStatus <> 1 OR c.EvidenceStatus = 0 OR c.ValueJson <> i.DecisionSnapshotJson)))
                    THROW 51671, 'RCV_DOCUMENT_TENANT_LINEAGE_INVALID: receipt, GRN and DEC-013 lineage must belong to one tenant.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE i.PurchaseOrderReceiptId <> d.PurchaseOrderReceiptId OR
                          ISNULL(i.GoodsReceiptNoteId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.GoodsReceiptNoteId, '00000000-0000-0000-0000-000000000000') OR
                          i.DocumentKind <> d.DocumentKind OR i.DocumentNumber <> d.DocumentNumber OR i.TemplateCode <> d.TemplateCode OR
                          i.ConfigurationProfileId <> d.ConfigurationProfileId OR i.ConfigurationProfileVersion <> d.ConfigurationProfileVersion OR
                          i.ConfigurationDecisionId <> d.ConfigurationDecisionId OR i.DecisionKeysJson <> d.DecisionKeysJson OR
                          i.DecisionSnapshotJson <> d.DecisionSnapshotJson OR
                          {{sourceMutationPredicate}} OR
                          i.PreparedByUserId <> d.PreparedByUserId OR i.PreparedAtUtc <> d.PreparedAtUtc OR
                          i.IsDeleted <> d.IsDeleted)
                    THROW 51672, 'RCV_DOCUMENT_LINEAGE_IMMUTABLE: number, source and configuration snapshots cannot be changed.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE i.Status <> d.Status AND NOT (
                        (d.Status IN (0,1) AND i.Status IN (1,2,3)) OR
                        (d.Status = 2 AND i.Status = 3)))
                    THROW 51673, 'RCV_DOCUMENT_STATUS_INVALID: the requested lifecycle transition is not allowed.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i
                    LEFT JOIN CentralDocumentRecords r ON r.Id = i.CentralDocumentRecordId AND r.TenantId = i.TenantId AND r.IsDeleted = 0
                    LEFT JOIN CentralDocumentVersions v ON v.Id = i.CentralDocumentVersionId AND v.TenantId = i.TenantId AND v.DocumentRecordId = r.Id AND v.IsDeleted = 0
                    WHERE (i.Status = 2 AND (i.IssuedByUserId IS NULL OR i.IssuedAtUtc IS NULL OR r.Id IS NULL OR v.Id IS NULL)) OR
                          (i.Status = 3 AND (i.CancelledByUserId IS NULL OR i.CancelledAtUtc IS NULL OR NULLIF(LTRIM(RTRIM(i.CancellationReason)), '') IS NULL)))
                    THROW 51674, 'RCV_DOCUMENT_TERMINAL_STATE_INVALID: issue and cancellation require complete actor and DMS lineage.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i
                    OUTER APPLY (
                        SELECT TOP (1) c.Id, c.Status
                        FROM ProcurementReceiptInspectionCases c
                        WHERE c.TenantId = i.TenantId AND c.PurchaseOrderReceiptId = i.PurchaseOrderReceiptId
                          AND c.IsDeleted = 0 AND c.Status <> 10
                        ORDER BY c.Sequence DESC) inspection
                    WHERE i.Status = 2 AND (inspection.Id IS NULL OR inspection.Status NOT IN (2,8)))
                    THROW 51680, 'RCV_DOCUMENT_ISSUE_BLOCKED: approved inspection, DEC-013 evidence/signatures and sequence are mandatory.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i
                    CROSS APPLY OPENJSON(i.DecisionSnapshotJson, '$.signatureRequirements') required
                    WHERE i.Status = 2 AND NOT EXISTS (
                        SELECT 1 FROM ProcurementReceiptDocumentSignatures signature
                        WHERE signature.TenantId = i.TenantId AND signature.ReceiptDocumentId = i.Id
                          AND signature.IsDeleted = 0
                          AND LTRIM(RTRIM(signature.RequiredRole)) = LTRIM(RTRIM(CONVERT(nvarchar(200), required.[value])))))
                    THROW 51680, 'RCV_DOCUMENT_ISSUE_BLOCKED: approved inspection, DEC-013 evidence/signatures and sequence are mandatory.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i
                    CROSS APPLY OPENJSON(i.DecisionSnapshotJson, '$.evidenceRequirements') required
                    OUTER APPLY (
                        SELECT TOP (1) c.Id
                        FROM ProcurementReceiptInspectionCases c
                        WHERE c.TenantId = i.TenantId AND c.PurchaseOrderReceiptId = i.PurchaseOrderReceiptId
                          AND c.IsDeleted = 0 AND c.Status <> 10
                        ORDER BY c.Sequence DESC) inspection
                    WHERE i.Status = 2 AND NOT EXISTS (
                        SELECT 1 FROM ProcurementReceiptInspectionEvidence evidence
                        WHERE evidence.TenantId = i.TenantId AND evidence.InspectionCaseId = inspection.Id
                          AND evidence.IsDeleted = 0
                          AND LTRIM(RTRIM(evidence.RequirementKey)) = LTRIM(RTRIM(CONVERT(nvarchar(200), required.[value])))))
                    THROW 51680, 'RCV_DOCUMENT_ISSUE_BLOCKED: approved inspection, DEC-013 evidence/signatures and sequence are mandatory.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i
                    WHERE i.Status = 2 AND i.DocumentKind = 1
                      AND JSON_VALUE(i.DecisionSnapshotJson, '$.coexistenceRule') IN ('sequentialDocuments','SequentialDocuments','2')
                      AND NOT EXISTS (
                        SELECT 1 FROM ProcurementReceiptDocuments grn
                        WHERE grn.TenantId = i.TenantId AND grn.PurchaseOrderReceiptId = i.PurchaseOrderReceiptId
                          AND grn.DocumentKind = 0 AND grn.Status = 2 AND grn.IsDeleted = 0))
                    THROW 51680, 'RCV_DOCUMENT_ISSUE_BLOCKED: approved inspection, DEC-013 evidence/signatures and sequence are mandatory.', 1;
            END;
            """);
    }
}
