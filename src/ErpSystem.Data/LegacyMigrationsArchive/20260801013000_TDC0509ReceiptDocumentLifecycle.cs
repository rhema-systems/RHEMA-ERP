using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ErpSystem.Data.ApplicationDbContext))]
[Migration("20260801013000_TDC0509ReceiptDocumentLifecycle")]
public sealed class TDC0509ReceiptDocumentLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE [dbo].[ProcurementReceiptDocuments] (
                [Id] uniqueidentifier NOT NULL,
                [PurchaseOrderReceiptId] uniqueidentifier NOT NULL,
                [GoodsReceiptNoteId] uniqueidentifier NULL,
                [DocumentKind] int NOT NULL,
                [DocumentNumber] nvarchar(100) NOT NULL,
                [TemplateCode] nvarchar(80) NOT NULL,
                [Status] int NOT NULL,
                [ReconciliationStatus] int NOT NULL,
                [ReconciliationMessage] nvarchar(1000) NULL,
                [ReconciledAtUtc] datetime2 NULL,
                [ConfigurationProfileId] uniqueidentifier NOT NULL,
                [ConfigurationProfileVersion] int NOT NULL,
                [ConfigurationDecisionId] uniqueidentifier NOT NULL,
                [DecisionKeysJson] nvarchar(max) NOT NULL,
                [DecisionSnapshotJson] nvarchar(max) NOT NULL,
                [SourceSnapshotJson] nvarchar(max) NOT NULL,
                [SourceIntegrityHash] nvarchar(64) NOT NULL,
                [CorrelationId] nvarchar(100) NOT NULL,
                [PreparedByUserId] uniqueidentifier NOT NULL,
                [PreparedByName] nvarchar(300) NOT NULL,
                [PreparedAtUtc] datetime2 NOT NULL,
                [IssuedByUserId] uniqueidentifier NULL,
                [IssuedByName] nvarchar(300) NULL,
                [IssuedAtUtc] datetime2 NULL,
                [CancelledByUserId] uniqueidentifier NULL,
                [CancelledByName] nvarchar(300) NULL,
                [CancelledAtUtc] datetime2 NULL,
                [CancellationReason] nvarchar(1000) NULL,
                [CentralDocumentRecordId] uniqueidentifier NULL,
                [CentralDocumentVersionId] uniqueidentifier NULL,
                [RowVersion] rowversion NOT NULL,
                [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NULL,
                [CreatedBy] nvarchar(max) NULL, [UpdatedBy] nvarchar(max) NULL,
                [CreatedById] uniqueidentifier NULL, [LastModifiedById] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL, [DeletedAt] datetime2 NULL, [DeletedBy] nvarchar(max) NULL,
                [TenantId] uniqueidentifier NOT NULL,
                CONSTRAINT [PK_ProcurementReceiptDocuments] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_ProcurementReceiptDocuments_Kind] CHECK ([DocumentKind] BETWEEN 0 AND 1),
                CONSTRAINT [CK_ProcurementReceiptDocuments_Status] CHECK ([Status] BETWEEN 0 AND 3 AND [ReconciliationStatus] BETWEEN 0 AND 3),
                CONSTRAINT [CK_ProcurementReceiptDocuments_Snapshots] CHECK (ISJSON([DecisionKeysJson]) = 1 AND ISJSON([DecisionSnapshotJson]) = 1 AND ISJSON([SourceSnapshotJson]) = 1 AND LEN([SourceIntegrityHash]) = 64),
                CONSTRAINT [FK_ProcurementReceiptDocuments_PurchaseOrderReceipts_PurchaseOrderReceiptId] FOREIGN KEY ([PurchaseOrderReceiptId]) REFERENCES [dbo].[PurchaseOrderReceipts] ([Id]),
                CONSTRAINT [FK_ProcurementReceiptDocuments_GoodsReceiptNotes_GoodsReceiptNoteId] FOREIGN KEY ([GoodsReceiptNoteId]) REFERENCES [dbo].[GoodsReceiptNotes] ([Id]),
                CONSTRAINT [FK_ProcurementReceiptDocuments_CentralDocumentRecords_CentralDocumentRecordId] FOREIGN KEY ([CentralDocumentRecordId]) REFERENCES [dbo].[CentralDocumentRecords] ([Id]),
                CONSTRAINT [FK_ProcurementReceiptDocuments_CentralDocumentVersions_CentralDocumentVersionId] FOREIGN KEY ([CentralDocumentVersionId]) REFERENCES [dbo].[CentralDocumentVersions] ([Id]),
                CONSTRAINT [FK_ProcurementReceiptDocuments_ProcurementConfigurationProfiles_ConfigurationProfileId] FOREIGN KEY ([ConfigurationProfileId]) REFERENCES [dbo].[ProcurementConfigurationProfiles] ([Id]),
                CONSTRAINT [FK_ProcurementReceiptDocuments_ProcurementConfigurationDecisions_ConfigurationDecisionId] FOREIGN KEY ([ConfigurationDecisionId]) REFERENCES [dbo].[ProcurementConfigurationDecisions] ([Id]),
                CONSTRAINT [FK_ProcurementReceiptDocuments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id])
            );
            CREATE UNIQUE INDEX [IX_ProcurementReceiptDocuments_TenantId_PurchaseOrderReceiptId_DocumentKind]
                ON [dbo].[ProcurementReceiptDocuments] ([TenantId], [PurchaseOrderReceiptId], [DocumentKind]) WHERE [IsDeleted] = 0;
            CREATE UNIQUE INDEX [IX_ProcurementReceiptDocuments_TenantId_DocumentNumber]
                ON [dbo].[ProcurementReceiptDocuments] ([TenantId], [DocumentNumber]);
            CREATE INDEX [IX_ProcurementReceiptDocuments_TenantId_Status_ReconciliationStatus]
                ON [dbo].[ProcurementReceiptDocuments] ([TenantId], [Status], [ReconciliationStatus]);
            CREATE INDEX [IX_ProcurementReceiptDocuments_PurchaseOrderReceiptId] ON [dbo].[ProcurementReceiptDocuments] ([PurchaseOrderReceiptId]);
            CREATE INDEX [IX_ProcurementReceiptDocuments_GoodsReceiptNoteId] ON [dbo].[ProcurementReceiptDocuments] ([GoodsReceiptNoteId]);
            CREATE INDEX [IX_ProcurementReceiptDocuments_CentralDocumentRecordId] ON [dbo].[ProcurementReceiptDocuments] ([CentralDocumentRecordId]);
            CREATE INDEX [IX_ProcurementReceiptDocuments_CentralDocumentVersionId] ON [dbo].[ProcurementReceiptDocuments] ([CentralDocumentVersionId]);
            CREATE INDEX [IX_ProcurementReceiptDocuments_ConfigurationProfileId] ON [dbo].[ProcurementReceiptDocuments] ([ConfigurationProfileId]);
            CREATE INDEX [IX_ProcurementReceiptDocuments_ConfigurationDecisionId] ON [dbo].[ProcurementReceiptDocuments] ([ConfigurationDecisionId]);

            CREATE TABLE [dbo].[ProcurementReceiptDocumentSignatures] (
                [Id] uniqueidentifier NOT NULL,
                [ReceiptDocumentId] uniqueidentifier NOT NULL,
                [RequiredRole] nvarchar(200) NOT NULL,
                [SignedByUserId] uniqueidentifier NOT NULL,
                [SignedByName] nvarchar(300) NOT NULL,
                [SignedAtUtc] datetime2 NOT NULL,
                [Comment] nvarchar(1000) NULL,
                [IntegrityHash] nvarchar(64) NOT NULL,
                [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NULL,
                [CreatedBy] nvarchar(max) NULL, [UpdatedBy] nvarchar(max) NULL,
                [CreatedById] uniqueidentifier NULL, [LastModifiedById] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL, [DeletedAt] datetime2 NULL, [DeletedBy] nvarchar(max) NULL,
                [TenantId] uniqueidentifier NOT NULL,
                CONSTRAINT [PK_ProcurementReceiptDocumentSignatures] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_ProcurementReceiptDocumentSignatures_Hash] CHECK (LEN([IntegrityHash]) = 64),
                CONSTRAINT [FK_ProcurementReceiptDocumentSignatures_ProcurementReceiptDocuments_ReceiptDocumentId] FOREIGN KEY ([ReceiptDocumentId]) REFERENCES [dbo].[ProcurementReceiptDocuments] ([Id]),
                CONSTRAINT [FK_ProcurementReceiptDocumentSignatures_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id])
            );
            CREATE UNIQUE INDEX [IX_ProcurementReceiptDocumentSignatures_TenantId_ReceiptDocumentId_RequiredRole]
                ON [dbo].[ProcurementReceiptDocumentSignatures] ([TenantId], [ReceiptDocumentId], [RequiredRole]);
            CREATE INDEX [IX_ProcurementReceiptDocumentSignatures_ReceiptDocumentId] ON [dbo].[ProcurementReceiptDocumentSignatures] ([ReceiptDocumentId]);

            CREATE TABLE [dbo].[ProcurementReceiptDocumentActions] (
                [Id] uniqueidentifier NOT NULL,
                [ReceiptDocumentId] uniqueidentifier NOT NULL,
                [Action] nvarchar(100) NOT NULL,
                [FromStatus] nvarchar(100) NOT NULL,
                [ToStatus] nvarchar(100) NOT NULL,
                [ActorUserId] uniqueidentifier NOT NULL,
                [ActorName] nvarchar(300) NOT NULL,
                [OccurredAtUtc] datetime2 NOT NULL,
                [CorrelationId] nvarchar(100) NOT NULL,
                [Reason] nvarchar(1000) NULL,
                [DetailsJson] nvarchar(max) NOT NULL,
                [IntegrityHash] nvarchar(64) NOT NULL,
                [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NULL,
                [CreatedBy] nvarchar(max) NULL, [UpdatedBy] nvarchar(max) NULL,
                [CreatedById] uniqueidentifier NULL, [LastModifiedById] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL, [DeletedAt] datetime2 NULL, [DeletedBy] nvarchar(max) NULL,
                [TenantId] uniqueidentifier NOT NULL,
                CONSTRAINT [PK_ProcurementReceiptDocumentActions] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_ProcurementReceiptDocumentActions_Details] CHECK (ISJSON([DetailsJson]) = 1 AND LEN([IntegrityHash]) = 64),
                CONSTRAINT [FK_ProcurementReceiptDocumentActions_ProcurementReceiptDocuments_ReceiptDocumentId] FOREIGN KEY ([ReceiptDocumentId]) REFERENCES [dbo].[ProcurementReceiptDocuments] ([Id]),
                CONSTRAINT [FK_ProcurementReceiptDocumentActions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id])
            );
            CREATE INDEX [IX_ProcurementReceiptDocumentActions_TenantId_ReceiptDocumentId_OccurredAtUtc]
                ON [dbo].[ProcurementReceiptDocumentActions] ([TenantId], [ReceiptDocumentId], [OccurredAtUtc]);
            CREATE INDEX [IX_ProcurementReceiptDocumentActions_ReceiptDocumentId] ON [dbo].[ProcurementReceiptDocumentActions] ([ReceiptDocumentId]);
            """);

        migrationBuilder.Sql(
            """
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
                          i.DecisionSnapshotJson <> d.DecisionSnapshotJson OR i.SourceSnapshotJson <> d.SourceSnapshotJson OR
                          i.SourceIntegrityHash <> d.SourceIntegrityHash OR i.PreparedByUserId <> d.PreparedByUserId OR i.PreparedAtUtc <> d.PreparedAtUtc OR
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

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementReceiptDocumentSignatures_TDC0509Immutable]
            ON [dbo].[ProcurementReceiptDocumentSignatures]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted)
                    THROW 51675, 'RCV_DOCUMENT_SIGNATURE_IMMUTABLE: signatory evidence is append-only.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i
                    LEFT JOIN ProcurementReceiptDocuments d ON d.Id = i.ReceiptDocumentId AND d.TenantId = i.TenantId AND d.IsDeleted = 0
                    WHERE d.Id IS NULL OR d.Status IN (2,3) OR NOT EXISTS (
                        SELECT 1 FROM OPENJSON(d.DecisionSnapshotJson, '$.signatureRequirements') j
                        WHERE LTRIM(RTRIM(CONVERT(nvarchar(200), j.[value]))) = LTRIM(RTRIM(i.RequiredRole))))
                    THROW 51676, 'RCV_DOCUMENT_SIGNATURE_INVALID: signature must match an open document and configured DEC-013 role.', 1;
                IF EXISTS (
                    SELECT 1 FROM ProcurementReceiptDocumentSignatures s
                    JOIN inserted i ON i.ReceiptDocumentId = s.ReceiptDocumentId AND i.TenantId = s.TenantId AND i.SignedByUserId = s.SignedByUserId AND i.Id <> s.Id)
                    THROW 51677, 'RCV_DOCUMENT_SIGNATORY_SOD: one actor cannot satisfy multiple signatory roles.', 1;
            END;
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementReceiptDocumentActions_TDC0509Immutable]
            ON [dbo].[ProcurementReceiptDocumentActions]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted)
                    THROW 51678, 'RCV_DOCUMENT_ACTION_IMMUTABLE: receipt-document audit history is append-only.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i LEFT JOIN ProcurementReceiptDocuments d ON d.Id = i.ReceiptDocumentId AND d.TenantId = i.TenantId
                    WHERE d.Id IS NULL)
                    THROW 51679, 'RCV_DOCUMENT_ACTION_TENANT_MISMATCH: action and receipt-document tenants must match.', 1;
            END;
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO CentralDocumentMetadataTemplates
                (Id, Module, DocumentType, TemplateCode, SourceLabel, RequiredFieldsJson, RelationshipsJson, RetentionRule, AccessProfile, IsActive, PublishedAt, CreatedAt, CreatedBy, IsDeleted, TenantId)
            SELECT NEWID(), 'Procurement', v.DocumentType, v.TemplateCode, 'Purchase receipt',
                   '["receiptNumber","purchaseOrderNumber","supplierName","inspectionStatus","signatories"]',
                   '["PurchaseOrderReceipt","PurchaseOrder","ProcurementReceiptInspectionCase"]',
                   'Procurement receipt retention policy', 'Procurement receipt restricted', 1, SYSUTCDATETIME(), SYSUTCDATETIME(), 'TDC-0509 migration', 0, t.Id
            FROM Tenants t
            CROSS APPLY (VALUES ('GoodsReceiptNote','TDC-GRN'), ('MaterialReceiptNote','TDC-MRN')) v(DocumentType, TemplateCode)
            WHERE t.IsDeleted = 0 AND NOT EXISTS (
                SELECT 1 FROM CentralDocumentMetadataTemplates m
                WHERE m.TenantId = t.Id AND m.TemplateCode = v.TemplateCode AND m.IsDeleted = 0);

            INSERT INTO CentralDocumentGenerationTemplates
                (Id, TemplateCode, Title, TitleTemplate, Module, SourceLabel, DocumentType, MetadataTemplateCode,
                 AccessProfile, MergeFieldsJson, Body, IsActive, RequiresApproval, ApprovalRole, SignatureRole,
                 CreatedAt, CreatedBy, IsDeleted, TenantId)
            SELECT NEWID(), v.TemplateCode, v.Title, v.Title + ' {{documentNumber}}', 'Procurement', 'Purchase receipt',
                   v.DocumentType, v.TemplateCode, 'Procurement receipt restricted',
                   '["documentNumber","receiptNumber","purchaseOrderNumber","supplierName","inspectionStatus","signatories"]',
                   v.Body, 1, 1, 'Procurement Approver', 'Receipt Signatory', SYSUTCDATETIME(), 'TDC-0509 migration', 0, t.Id
            FROM Tenants t
            CROSS APPLY (VALUES
                ('GoodsReceiptNote','TDC-GRN','Goods Receipt Note','Formal record of goods received against the governed purchase order.'),
                ('MaterialReceiptNote','TDC-MRN','Material Receipt Note','Formal record of materials received and accepted through governed inspection.'))
                v(DocumentType, TemplateCode, Title, Body)
            WHERE t.IsDeleted = 0 AND NOT EXISTS (
                SELECT 1 FROM CentralDocumentGenerationTemplates g
                WHERE g.TenantId = t.Id AND g.TemplateCode = v.TemplateCode AND g.IsDeleted = 0);
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER VIEW [dbo].[vw_ProcurementReceiptDocumentReconciliation]
            AS
            SELECT r.TenantId, r.Id AS PurchaseOrderReceiptId, r.ReceiptNumber, r.PurchaseOrderId,
                   po.OrderNumber AS PurchaseOrderNumber, r.Status AS ReceiptStatus,
                   d.Id AS ReceiptDocumentId, d.DocumentKind, d.DocumentNumber, d.TemplateCode,
                   d.Status AS DocumentStatus, d.ReconciliationStatus, d.ReconciliationMessage,
                   d.ConfigurationProfileId, d.ConfigurationProfileVersion,
                   d.CentralDocumentRecordId, d.CentralDocumentVersionId,
                   CASE WHEN d.Id IS NULL THEN 'Missing register entry'
                        WHEN d.Status = 2 AND (d.CentralDocumentRecordId IS NULL OR d.CentralDocumentVersionId IS NULL) THEN 'Issued without DMS lineage'
                        WHEN d.ReconciliationStatus = 1 THEN 'Reconciled'
                        ELSE 'Pending or exception' END AS ReconciliationResult
            FROM PurchaseOrderReceipts r
            JOIN PurchaseOrders po ON po.Id = r.PurchaseOrderId AND po.TenantId = r.TenantId
            LEFT JOIN ProcurementReceiptDocuments d ON d.PurchaseOrderReceiptId = r.Id AND d.TenantId = r.TenantId AND d.IsDeleted = 0
            WHERE r.IsDeleted = 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP VIEW IF EXISTS [dbo].[vw_ProcurementReceiptDocumentReconciliation];
            DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementReceiptDocumentActions_TDC0509Immutable];
            DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementReceiptDocumentSignatures_TDC0509Immutable];
            DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementReceiptDocuments_TDC0509Protected];
            DROP TABLE IF EXISTS [dbo].[ProcurementReceiptDocumentActions];
            DROP TABLE IF EXISTS [dbo].[ProcurementReceiptDocumentSignatures];
            DROP TABLE IF EXISTS [dbo].[ProcurementReceiptDocuments];
            DELETE FROM [dbo].[CentralDocumentGenerationTemplates]
            WHERE [TemplateCode] IN ('TDC-GRN','TDC-MRN') AND [CreatedBy] = 'TDC-0509 migration';
            DELETE FROM [dbo].[CentralDocumentMetadataTemplates]
            WHERE [TemplateCode] IN ('TDC-GRN','TDC-MRN') AND [CreatedBy] = 'TDC-0509 migration';
            """);
    }
}
