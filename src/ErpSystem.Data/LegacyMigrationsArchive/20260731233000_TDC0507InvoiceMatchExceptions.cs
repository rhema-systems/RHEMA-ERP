using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ErpSystem.Data.ApplicationDbContext))]
[Migration("20260731233000_TDC0507InvoiceMatchExceptions")]
public sealed class TDC0507InvoiceMatchExceptions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE [dbo].[VendorInvoiceMatchException] (
                [Id] uniqueidentifier NOT NULL,
                [VendorInvoiceId] uniqueidentifier NOT NULL,
                [PurchaseOrderId] uniqueidentifier NOT NULL,
                [Sequence] int NOT NULL,
                [Status] int NOT NULL,
                [VarianceType] nvarchar(200) NOT NULL,
                [PriceTolerancePercent] decimal(5,2) NOT NULL,
                [QuantityTolerancePercent] decimal(5,2) NOT NULL,
                [RootCauseCategory] nvarchar(100) NOT NULL,
                [RootCauseDescription] nvarchar(2000) NOT NULL,
                [Justification] nvarchar(2000) NOT NULL,
                [CorrectiveAction] nvarchar(2000) NOT NULL,
                [CorrectiveActionOwnerId] uniqueidentifier NOT NULL,
                [CorrectiveActionOwnerName] nvarchar(300) NOT NULL,
                [CorrectiveActionDueAtUtc] datetime2 NOT NULL,
                [CorrectiveActionStatus] int NOT NULL,
                [CorrectiveActionCompletedAtUtc] datetime2 NULL,
                [CorrectiveActionCompletedById] uniqueidentifier NULL,
                [CorrectiveActionCompletionNote] nvarchar(2000) NULL,
                [ExpiresAtUtc] datetime2 NOT NULL,
                [InvoiceSnapshotHash] nvarchar(64) NOT NULL,
                [VarianceSnapshotJson] nvarchar(max) NOT NULL,
                [ConfigurationProfileId] uniqueidentifier NULL,
                [ConfigurationProfileVersion] int NULL,
                [WorkflowInstanceId] uniqueidentifier NULL,
                [RequestedById] uniqueidentifier NOT NULL,
                [RequestedByName] nvarchar(300) NOT NULL,
                [RequestedAtUtc] datetime2 NOT NULL,
                [FinalApprovedById] uniqueidentifier NULL,
                [FinalApprovedByName] nvarchar(300) NULL,
                [FinalApprovedAtUtc] datetime2 NULL,
                [RejectedById] uniqueidentifier NULL,
                [RejectedByName] nvarchar(300) NULL,
                [RejectedAtUtc] datetime2 NULL,
                [DecisionComment] nvarchar(2000) NULL,
                [ApprovalControlEventId] uniqueidentifier NULL,
                [IdempotencyKey] nvarchar(100) NOT NULL,
                [CorrelationId] nvarchar(100) NOT NULL,
                [IntegrityHash] nvarchar(64) NOT NULL,
                [RowVersion] rowversion NOT NULL,
                [CreatedAt] datetime2 NOT NULL,
                [UpdatedAt] datetime2 NULL,
                [CreatedBy] nvarchar(max) NULL,
                [UpdatedBy] nvarchar(max) NULL,
                [CreatedById] uniqueidentifier NULL,
                [LastModifiedById] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAt] datetime2 NULL,
                [DeletedBy] nvarchar(max) NULL,
                [TenantId] uniqueidentifier NOT NULL,
                CONSTRAINT [PK_VendorInvoiceMatchException] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_VendorInvoiceMatchException_TDC0507Status] CHECK ([Status] BETWEEN 1 AND 5),
                CONSTRAINT [CK_VendorInvoiceMatchException_TDC0507CorrectiveStatus] CHECK ([CorrectiveActionStatus] BETWEEN 1 AND 2),
                CONSTRAINT [CK_VendorInvoiceMatchException_TDC0507Sequence] CHECK ([Sequence] >= 1),
                CONSTRAINT [CK_VendorInvoiceMatchException_TDC0507Tolerances] CHECK ([PriceTolerancePercent] BETWEEN 0 AND 100 AND [QuantityTolerancePercent] BETWEEN 0 AND 100),
                CONSTRAINT [CK_VendorInvoiceMatchException_TDC0507Dates] CHECK ([ExpiresAtUtc] > [RequestedAtUtc] AND [CorrectiveActionDueAtUtc] > [RequestedAtUtc]),
                CONSTRAINT [CK_VendorInvoiceMatchException_TDC0507Hashes] CHECK (LEN([InvoiceSnapshotHash]) = 64 AND LEN([IntegrityHash]) = 64 AND ISJSON([VarianceSnapshotJson]) = 1),
                CONSTRAINT [CK_VendorInvoiceMatchException_TDC0507Approval] CHECK (([Status] <> 2) OR ([WorkflowInstanceId] IS NOT NULL AND [ApprovalControlEventId] IS NOT NULL AND [FinalApprovedById] IS NOT NULL AND [FinalApprovedAtUtc] IS NOT NULL)),
                CONSTRAINT [CK_VendorInvoiceMatchException_TDC0507CorrectiveCompletion] CHECK (([CorrectiveActionStatus] = 1 AND [CorrectiveActionCompletedAtUtc] IS NULL AND [CorrectiveActionCompletedById] IS NULL) OR ([CorrectiveActionStatus] = 2 AND [CorrectiveActionCompletedAtUtc] IS NOT NULL AND [CorrectiveActionCompletedById] IS NOT NULL)),
                CONSTRAINT [FK_VendorInvoiceMatchException_VendorInvoice_VendorInvoiceId] FOREIGN KEY ([VendorInvoiceId]) REFERENCES [dbo].[VendorInvoice] ([Id]),
                CONSTRAINT [FK_VendorInvoiceMatchException_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [dbo].[PurchaseOrders] ([Id]),
                CONSTRAINT [FK_VendorInvoiceMatchException_WorkflowInstances_WorkflowInstanceId] FOREIGN KEY ([WorkflowInstanceId]) REFERENCES [dbo].[WorkflowInstances] ([Id]),
                CONSTRAINT [FK_VendorInvoiceMatchException_ProcurementControlEvents_ApprovalControlEventId] FOREIGN KEY ([ApprovalControlEventId]) REFERENCES [dbo].[ProcurementControlEvents] ([Id]),
                CONSTRAINT [FK_VendorInvoiceMatchException_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id])
            );

            CREATE UNIQUE INDEX [IX_VendorInvoiceMatchException_TenantId_VendorInvoiceId_Sequence]
                ON [dbo].[VendorInvoiceMatchException] ([TenantId], [VendorInvoiceId], [Sequence]);
            CREATE UNIQUE INDEX [IX_VendorInvoiceMatchException_TenantId_IdempotencyKey]
                ON [dbo].[VendorInvoiceMatchException] ([TenantId], [IdempotencyKey]);
            CREATE INDEX [IX_VendorInvoiceMatchException_TenantId_Status_ExpiresAtUtc]
                ON [dbo].[VendorInvoiceMatchException] ([TenantId], [Status], [ExpiresAtUtc]);
            CREATE INDEX [IX_VendorInvoiceMatchException_TenantId_CorrectiveActionStatus_CorrectiveActionDueAtUtc]
                ON [dbo].[VendorInvoiceMatchException] ([TenantId], [CorrectiveActionStatus], [CorrectiveActionDueAtUtc]);
            CREATE INDEX [IX_VendorInvoiceMatchException_VendorInvoiceId] ON [dbo].[VendorInvoiceMatchException] ([VendorInvoiceId]);
            CREATE INDEX [IX_VendorInvoiceMatchException_PurchaseOrderId] ON [dbo].[VendorInvoiceMatchException] ([PurchaseOrderId]);
            CREATE INDEX [IX_VendorInvoiceMatchException_WorkflowInstanceId] ON [dbo].[VendorInvoiceMatchException] ([WorkflowInstanceId]);
            CREATE INDEX [IX_VendorInvoiceMatchException_ApprovalControlEventId] ON [dbo].[VendorInvoiceMatchException] ([ApprovalControlEventId]);

            CREATE TABLE [dbo].[VendorInvoiceMatchExceptionVariance] (
                [Id] uniqueidentifier NOT NULL,
                [MatchExceptionId] uniqueidentifier NOT NULL,
                [VarianceType] nvarchar(100) NOT NULL,
                [ItemDescription] nvarchar(500) NOT NULL,
                [ActualValue] decimal(18,4) NOT NULL,
                [ExpectedValue] decimal(18,4) NOT NULL,
                [Variance] decimal(18,4) NOT NULL,
                [VariancePercentage] decimal(9,4) NOT NULL,
                [ConfiguredTolerancePercent] decimal(5,2) NOT NULL,
                [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NULL,
                [CreatedBy] nvarchar(max) NULL, [UpdatedBy] nvarchar(max) NULL,
                [CreatedById] uniqueidentifier NULL, [LastModifiedById] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL, [DeletedAt] datetime2 NULL, [DeletedBy] nvarchar(max) NULL,
                [TenantId] uniqueidentifier NOT NULL,
                CONSTRAINT [PK_VendorInvoiceMatchExceptionVariance] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_VendorInvoiceMatchExceptionVariance_TDC0507Tolerance] CHECK ([ConfiguredTolerancePercent] BETWEEN 0 AND 100),
                CONSTRAINT [FK_VendorInvoiceMatchExceptionVariance_VendorInvoiceMatchException_MatchExceptionId] FOREIGN KEY ([MatchExceptionId]) REFERENCES [dbo].[VendorInvoiceMatchException] ([Id]),
                CONSTRAINT [FK_VendorInvoiceMatchExceptionVariance_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id])
            );
            CREATE INDEX [IX_VendorInvoiceMatchExceptionVariance_TenantId_MatchExceptionId]
                ON [dbo].[VendorInvoiceMatchExceptionVariance] ([TenantId], [MatchExceptionId]);

            CREATE TABLE [dbo].[VendorInvoiceMatchExceptionEvidence] (
                [Id] uniqueidentifier NOT NULL,
                [MatchExceptionId] uniqueidentifier NOT NULL,
                [RequirementKey] nvarchar(100) NOT NULL,
                [ReferenceKind] int NOT NULL,
                [WorkflowEvidenceDocumentId] uniqueidentifier NULL,
                [FileUploadRecordId] uniqueidentifier NULL,
                [EvidenceReference] nvarchar(1000) NOT NULL,
                [EvidenceHash] nvarchar(64) NOT NULL,
                [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NULL,
                [CreatedBy] nvarchar(max) NULL, [UpdatedBy] nvarchar(max) NULL,
                [CreatedById] uniqueidentifier NULL, [LastModifiedById] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL, [DeletedAt] datetime2 NULL, [DeletedBy] nvarchar(max) NULL,
                [TenantId] uniqueidentifier NOT NULL,
                CONSTRAINT [PK_VendorInvoiceMatchExceptionEvidence] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_VendorInvoiceMatchExceptionEvidence_TDC0507Reference] CHECK (([ReferenceKind] = 1 AND [WorkflowEvidenceDocumentId] IS NOT NULL AND [FileUploadRecordId] IS NULL) OR ([ReferenceKind] = 2 AND [WorkflowEvidenceDocumentId] IS NULL AND [FileUploadRecordId] IS NOT NULL)),
                CONSTRAINT [CK_VendorInvoiceMatchExceptionEvidence_TDC0507Hash] CHECK (LEN([EvidenceHash]) = 64),
                CONSTRAINT [FK_VendorInvoiceMatchExceptionEvidence_VendorInvoiceMatchException_MatchExceptionId] FOREIGN KEY ([MatchExceptionId]) REFERENCES [dbo].[VendorInvoiceMatchException] ([Id]),
                CONSTRAINT [FK_VendorInvoiceMatchExceptionEvidence_WorkflowEvidenceDocuments_WorkflowEvidenceDocumentId] FOREIGN KEY ([WorkflowEvidenceDocumentId]) REFERENCES [dbo].[WorkflowEvidenceDocuments] ([Id]),
                CONSTRAINT [FK_VendorInvoiceMatchExceptionEvidence_FileUploadRecords_FileUploadRecordId] FOREIGN KEY ([FileUploadRecordId]) REFERENCES [dbo].[FileUploadRecords] ([Id]),
                CONSTRAINT [FK_VendorInvoiceMatchExceptionEvidence_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id])
            );
            CREATE UNIQUE INDEX [IX_VendorInvoiceMatchExceptionEvidence_TenantId_MatchExceptionId_RequirementKey]
                ON [dbo].[VendorInvoiceMatchExceptionEvidence] ([TenantId], [MatchExceptionId], [RequirementKey]);
            CREATE INDEX [IX_VendorInvoiceMatchExceptionEvidence_WorkflowEvidenceDocumentId]
                ON [dbo].[VendorInvoiceMatchExceptionEvidence] ([WorkflowEvidenceDocumentId]);
            CREATE INDEX [IX_VendorInvoiceMatchExceptionEvidence_FileUploadRecordId]
                ON [dbo].[VendorInvoiceMatchExceptionEvidence] ([FileUploadRecordId]);

            CREATE TABLE [dbo].[VendorInvoiceMatchExceptionAction] (
                [Id] uniqueidentifier NOT NULL,
                [MatchExceptionId] uniqueidentifier NOT NULL,
                [Sequence] int NOT NULL,
                [Action] nvarchar(100) NOT NULL,
                [FromStatus] int NOT NULL,
                [ToStatus] int NOT NULL,
                [ActorUserId] uniqueidentifier NOT NULL,
                [ActorName] nvarchar(300) NOT NULL,
                [Comment] nvarchar(2000) NOT NULL,
                [OccurredAtUtc] datetime2 NOT NULL,
                [IntegrityHash] nvarchar(64) NOT NULL,
                [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NULL,
                [CreatedBy] nvarchar(max) NULL, [UpdatedBy] nvarchar(max) NULL,
                [CreatedById] uniqueidentifier NULL, [LastModifiedById] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL, [DeletedAt] datetime2 NULL, [DeletedBy] nvarchar(max) NULL,
                [TenantId] uniqueidentifier NOT NULL,
                CONSTRAINT [PK_VendorInvoiceMatchExceptionAction] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_VendorInvoiceMatchExceptionAction_TDC0507Sequence] CHECK ([Sequence] >= 1),
                CONSTRAINT [CK_VendorInvoiceMatchExceptionAction_TDC0507Statuses] CHECK ([FromStatus] BETWEEN 1 AND 5 AND [ToStatus] BETWEEN 1 AND 5),
                CONSTRAINT [CK_VendorInvoiceMatchExceptionAction_TDC0507Hash] CHECK (LEN([IntegrityHash]) = 64),
                CONSTRAINT [FK_VendorInvoiceMatchExceptionAction_VendorInvoiceMatchException_MatchExceptionId] FOREIGN KEY ([MatchExceptionId]) REFERENCES [dbo].[VendorInvoiceMatchException] ([Id]),
                CONSTRAINT [FK_VendorInvoiceMatchExceptionAction_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id])
            );
            CREATE UNIQUE INDEX [IX_VendorInvoiceMatchExceptionAction_TenantId_MatchExceptionId_Sequence]
                ON [dbo].[VendorInvoiceMatchExceptionAction] ([TenantId], [MatchExceptionId], [Sequence]);
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_VendorInvoiceMatchException_TDC0507Protected]
            ON [dbo].[VendorInvoiceMatchException]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                    THROW 51651, 'AP_MATCH_EXCEPTION_DELETE_FORBIDDEN: AP-006 exception records cannot be deleted.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    LEFT JOIN VendorInvoice invoice ON invoice.Id = i.VendorInvoiceId AND invoice.TenantId = i.TenantId AND invoice.IsDeleted = 0
                    LEFT JOIN PurchaseOrders po ON po.Id = i.PurchaseOrderId AND po.TenantId = i.TenantId AND po.IsDeleted = 0
                    LEFT JOIN WorkflowInstances workflow ON workflow.Id = i.WorkflowInstanceId AND workflow.TenantId = i.TenantId AND workflow.EntityId = i.Id AND workflow.IsDeleted = 0
                    LEFT JOIN ProcurementControlEvents approvalEvent ON approvalEvent.Id = i.ApprovalControlEventId AND approvalEvent.TenantId = i.TenantId AND approvalEvent.IsDeleted = 0
                    WHERE i.IsDeleted = 0 AND (
                        invoice.Id IS NULL OR invoice.PurchaseOrderId <> i.PurchaseOrderId OR po.Id IS NULL OR
                        (i.WorkflowInstanceId IS NOT NULL AND workflow.Id IS NULL) OR
                        (i.ApprovalControlEventId IS NOT NULL AND approvalEvent.Id IS NULL)))
                    THROW 51652, 'AP_MATCH_EXCEPTION_TENANT_MISMATCH: invoice, PO, workflow and event lineage must belong to one tenant.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id = i.Id
                    WHERE d.Id IS NULL AND (i.Status <> 1 OR i.ApprovalControlEventId IS NOT NULL OR i.FinalApprovedById IS NOT NULL))
                    THROW 51653, 'AP_MATCH_EXCEPTION_INSERT_INVALID: new AP-006 requests must start pending without approval lineage.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE i.TenantId <> d.TenantId OR i.VendorInvoiceId <> d.VendorInvoiceId OR
                          i.PurchaseOrderId <> d.PurchaseOrderId OR i.Sequence <> d.Sequence OR
                          i.VarianceType <> d.VarianceType OR i.PriceTolerancePercent <> d.PriceTolerancePercent OR
                          i.QuantityTolerancePercent <> d.QuantityTolerancePercent OR i.RootCauseCategory <> d.RootCauseCategory OR
                          i.RootCauseDescription <> d.RootCauseDescription OR i.Justification <> d.Justification OR
                          i.CorrectiveAction <> d.CorrectiveAction OR i.CorrectiveActionOwnerId <> d.CorrectiveActionOwnerId OR
                          i.CorrectiveActionDueAtUtc <> d.CorrectiveActionDueAtUtc OR i.ExpiresAtUtc <> d.ExpiresAtUtc OR
                          i.InvoiceSnapshotHash <> d.InvoiceSnapshotHash OR i.VarianceSnapshotJson <> d.VarianceSnapshotJson OR
                          ISNULL(i.ConfigurationProfileId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.ConfigurationProfileId, '00000000-0000-0000-0000-000000000000') OR
                          ISNULL(i.ConfigurationProfileVersion, -1) <> ISNULL(d.ConfigurationProfileVersion, -1) OR
                          i.RequestedById <> d.RequestedById OR i.RequestedAtUtc <> d.RequestedAtUtc OR
                          i.IdempotencyKey <> d.IdempotencyKey)
                    THROW 51654, 'AP_MATCH_EXCEPTION_LINEAGE_IMMUTABLE: request, snapshot and governance lineage are immutable.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE i.Status <> d.Status AND NOT (d.Status = 1 AND i.Status IN (2,3,4)))
                    THROW 51655, 'AP_MATCH_EXCEPTION_TRANSITION_INVALID: the AP-006 status transition is not allowed.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    JOIN deleted d ON d.Id = i.Id
                    JOIN VendorInvoice invoice ON invoice.Id = i.VendorInvoiceId AND invoice.TenantId = i.TenantId
                    LEFT JOIN ProcurementControlEvents evt
                      ON evt.Id = i.ApprovalControlEventId
                     AND evt.TenantId = i.TenantId
                     AND evt.EventType = 'VendorInvoiceMatchException'
                     AND evt.Action = 'InvoiceMatchExceptionApproved'
                     AND evt.Result = 2
                     AND evt.RuleCode = 'AP-006'
                     AND evt.RuleVersion = 'TDC-0507'
                     AND evt.SourceType = 'VendorInvoice'
                     AND evt.SourceId = i.VendorInvoiceId
                     AND evt.ActorUserId = i.FinalApprovedById
                     AND evt.IsDeleted = 0
                    LEFT JOIN WorkflowInstances workflow
                      ON workflow.Id = i.WorkflowInstanceId
                     AND workflow.TenantId = i.TenantId
                     AND workflow.EntityId = i.Id
                     AND workflow.Status = 2
                     AND workflow.IsDeleted = 0
                    WHERE d.Status = 1 AND i.Status = 2 AND (
                        evt.Id IS NULL OR workflow.Id IS NULL OR workflow.InitiatedById <> i.RequestedById OR
                        i.FinalApprovedById = i.RequestedById OR i.FinalApprovedById = invoice.SubmittedById OR
                        TRY_CONVERT(uniqueidentifier, JSON_VALUE(evt.ResultValuesJson, '$.purchaseOrderId')) <> i.PurchaseOrderId OR
                        TRY_CONVERT(uniqueidentifier, JSON_VALUE(evt.ResultValuesJson, '$.workflowInstanceId')) <> i.WorkflowInstanceId OR
                        JSON_VALUE(evt.ResultValuesJson, '$.invoiceSnapshotHash') <> i.InvoiceSnapshotHash OR
                        TRY_CONVERT(datetime2, JSON_VALUE(evt.ResultValuesJson, '$.expiresAtUtc'), 127) <> i.ExpiresAtUtc OR
                        NOT EXISTS (SELECT 1 FROM ProcurementControlEventEvidenceLinks link WHERE link.ControlEventId = evt.Id AND link.TenantId = i.TenantId AND link.IsDeleted = 0) OR
                        (SELECT COUNT(DISTINCT approval.ProcessedById)
                         FROM WorkflowApprovals approval
                         JOIN WorkflowStepInstances stepInstance ON stepInstance.Id = approval.StepInstanceId
                         WHERE stepInstance.WorkflowInstanceId = i.WorkflowInstanceId
                           AND approval.TenantId = i.TenantId AND approval.Status = 1
                           AND approval.ProcessedById IS NOT NULL AND approval.IsDeleted = 0) < 2 OR
                        EXISTS (
                            SELECT 1 FROM WorkflowApprovals approval
                            JOIN WorkflowStepInstances stepInstance ON stepInstance.Id = approval.StepInstanceId
                            WHERE stepInstance.WorkflowInstanceId = i.WorkflowInstanceId
                              AND approval.TenantId = i.TenantId AND approval.Status = 1
                              AND approval.ProcessedById IN (i.RequestedById, invoice.SubmittedById))))
                    THROW 51656, 'AP_MATCH_EXCEPTION_APPROVAL_INVALID: exact AP-006 event, completed workflow and two independent approvers are required.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE d.CorrectiveActionStatus = 2 AND (
                        i.CorrectiveActionStatus <> d.CorrectiveActionStatus OR
                        ISNULL(i.CorrectiveActionCompletedAtUtc, '19000101') <> ISNULL(d.CorrectiveActionCompletedAtUtc, '19000101') OR
                        ISNULL(i.CorrectiveActionCompletedById, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.CorrectiveActionCompletedById, '00000000-0000-0000-0000-000000000000') OR
                        ISNULL(i.CorrectiveActionCompletionNote, '') <> ISNULL(d.CorrectiveActionCompletionNote, '')))
                    THROW 51657, 'AP_MATCH_EXCEPTION_CORRECTIVE_IMMUTABLE: completed corrective-action evidence cannot be changed.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE i.CorrectiveActionStatus <> d.CorrectiveActionStatus AND
                          NOT (d.CorrectiveActionStatus = 1 AND i.CorrectiveActionStatus = 2 AND d.Status = 2))
                    THROW 51658, 'AP_MATCH_EXCEPTION_CORRECTIVE_TRANSITION_INVALID: corrective action can close only from an approved exception.', 1;
            END;
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_VendorInvoiceMatchExceptionVariance_TDC0507Immutable]
            ON [dbo].[VendorInvoiceMatchExceptionVariance]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted) THROW 51659, 'AP_MATCH_EXCEPTION_VARIANCE_IMMUTABLE: variance snapshots are append-only.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i LEFT JOIN VendorInvoiceMatchException parent ON parent.Id = i.MatchExceptionId AND parent.TenantId = i.TenantId
                    WHERE parent.Id IS NULL)
                    THROW 51660, 'AP_MATCH_EXCEPTION_VARIANCE_TENANT_MISMATCH: variance and exception tenants must match.', 1;
            END;
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_VendorInvoiceMatchExceptionEvidence_TDC0507Immutable]
            ON [dbo].[VendorInvoiceMatchExceptionEvidence]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted) THROW 51661, 'AP_MATCH_EXCEPTION_EVIDENCE_IMMUTABLE: controlled evidence is append-only.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i
                    LEFT JOIN VendorInvoiceMatchException parent ON parent.Id = i.MatchExceptionId AND parent.TenantId = i.TenantId
                    LEFT JOIN WorkflowEvidenceDocuments workflowEvidence ON workflowEvidence.Id = i.WorkflowEvidenceDocumentId AND workflowEvidence.TenantId = i.TenantId AND workflowEvidence.IsDeleted = 0
                    LEFT JOIN FileUploadRecords upload ON upload.Id = i.FileUploadRecordId AND upload.TenantId = i.TenantId AND upload.IsDeleted = 0
                    WHERE parent.Id IS NULL OR
                          (i.ReferenceKind = 1 AND workflowEvidence.Id IS NULL) OR
                          (i.ReferenceKind = 2 AND (upload.Id IS NULL OR upload.VirusScanStatus <> 2)))
                    THROW 51662, 'AP_MATCH_EXCEPTION_EVIDENCE_INVALID: evidence must be tenant-owned and malware-clean.', 1;
            END;
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_VendorInvoiceMatchExceptionAction_TDC0507Immutable]
            ON [dbo].[VendorInvoiceMatchExceptionAction]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted) THROW 51663, 'AP_MATCH_EXCEPTION_ACTION_IMMUTABLE: AP-006 action history is append-only.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i LEFT JOIN VendorInvoiceMatchException parent ON parent.Id = i.MatchExceptionId AND parent.TenantId = i.TenantId
                    WHERE parent.Id IS NULL)
                    THROW 51664, 'AP_MATCH_EXCEPTION_ACTION_TENANT_MISMATCH: action and exception tenants must match.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS [dbo].[TR_VendorInvoiceMatchExceptionAction_TDC0507Immutable];
            DROP TRIGGER IF EXISTS [dbo].[TR_VendorInvoiceMatchExceptionEvidence_TDC0507Immutable];
            DROP TRIGGER IF EXISTS [dbo].[TR_VendorInvoiceMatchExceptionVariance_TDC0507Immutable];
            DROP TRIGGER IF EXISTS [dbo].[TR_VendorInvoiceMatchException_TDC0507Protected];
            DROP TABLE IF EXISTS [dbo].[VendorInvoiceMatchExceptionAction];
            DROP TABLE IF EXISTS [dbo].[VendorInvoiceMatchExceptionEvidence];
            DROP TABLE IF EXISTS [dbo].[VendorInvoiceMatchExceptionVariance];
            DROP TABLE IF EXISTS [dbo].[VendorInvoiceMatchException];
            """);
    }
}
