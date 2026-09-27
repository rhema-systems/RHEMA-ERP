using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

internal static class ProcurementSodTriggerSql
{
    internal static void Apply(MigrationBuilder migrationBuilder, bool enabled)
    {
        migrationBuilder.Sql(enabled ? ApplyPolicy(Trigger0, Replacements0) : Trigger0);
        migrationBuilder.Sql(enabled ? ApplyPolicy(Trigger1, Replacements1) : Trigger1);
        migrationBuilder.Sql(enabled ? ApplyPolicy(Trigger2, Replacements2) : Trigger2);
        migrationBuilder.Sql(enabled ? ApplyPolicy(Trigger3, Replacements3) : Trigger3);
        migrationBuilder.Sql(enabled ? ApplyPolicy(Trigger4, Replacements4) : Trigger4);
        migrationBuilder.Sql(enabled ? ApplyPolicy(Trigger5, Replacements5) : Trigger5);
        migrationBuilder.Sql(enabled ? ApplyPolicy(Trigger6, Replacements6) : Trigger6);
        migrationBuilder.Sql(enabled ? ApplyPolicy(Trigger7, Replacements7) : Trigger7);
        migrationBuilder.Sql(enabled ? ApplyPolicy(Trigger8, Replacements8) : Trigger8);
        migrationBuilder.Sql(enabled ? ApplyPolicy(Trigger9, Replacements9) : Trigger9);
        migrationBuilder.Sql(enabled ? ApplyPolicy(Trigger10, Replacements10) : Trigger10);
        migrationBuilder.Sql(enabled ? ApplyPolicy(Trigger11, Replacements11) : Trigger11);
        migrationBuilder.Sql(enabled ? ApplyPolicy(Trigger12, Replacements12) : Trigger12);
        migrationBuilder.Sql(enabled ? ApplyPolicy(Trigger13, Replacements13) : Trigger13);
        migrationBuilder.Sql(enabled ? ApplyPolicy(Trigger14, Replacements14) : Trigger14);
    }

    private static string ApplyPolicy(string sql, (string Before, string After)[] replacements)
    {
        sql = sql.Replace("\r\n", "\n", StringComparison.Ordinal);
        foreach (var (before, after) in replacements)
        {
            if (!sql.Contains(before, StringComparison.Ordinal))
                throw new InvalidOperationException("The frozen procurement SOD trigger does not contain its expected predicate.");
            sql = sql.Replace(before, after, StringComparison.Ordinal);
        }
        return sql;
    }

    // TR_GoodsReceiptNotes_TDC0503SodHardStop: preserve workflow, tenant, source, and audit predicates.
    private static readonly (string Before, string After)[] Replacements0 =
    [
        ("grn.ReceivedById = purchaseOrder.CreatedById", "(grn.ReceivedById = purchaseOrder.CreatedById AND dbo.ProcurementSodRequired(grn.TenantId) = 1)"),
        ("grn.LastModifiedById = purchaseOrder.CreatedById", "(grn.LastModifiedById = purchaseOrder.CreatedById AND dbo.ProcurementSodRequired(grn.TenantId) = 1)"),
    ];
    private const string Trigger0 = """
        CREATE OR ALTER TRIGGER [dbo].[TR_GoodsReceiptNotes_TDC0503SodHardStop]
        ON [dbo].[GoodsReceiptNotes]
        AFTER INSERT, UPDATE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS (
                SELECT 1
                FROM inserted grn
                LEFT JOIN PurchaseOrders purchaseOrder
                  ON purchaseOrder.Id = grn.PurchaseOrderId
                 AND purchaseOrder.TenantId = grn.TenantId
                 AND purchaseOrder.IsDeleted = 0
                WHERE grn.IsDeleted = 0
                  AND grn.PurchaseOrderId IS NOT NULL
                  AND (
                       purchaseOrder.Id IS NULL
                    OR purchaseOrder.CreatedById IS NULL
                    OR grn.ReceivedById IS NULL
                    OR grn.ReceivedById = purchaseOrder.CreatedById
                  ))
                THROW 51561, 'RCV_GRN_SOD_BLOCKED: a goods receipt requires a receiver distinct from the purchase-order creator.', 1;

            IF EXISTS (
                SELECT 1
                FROM inserted grn
                JOIN deleted previous ON previous.Id = grn.Id
                LEFT JOIN PurchaseOrders purchaseOrder
                  ON purchaseOrder.Id = grn.PurchaseOrderId
                 AND purchaseOrder.TenantId = grn.TenantId
                 AND purchaseOrder.IsDeleted = 0
                WHERE grn.IsDeleted = 0
                  AND grn.PurchaseOrderId IS NOT NULL
                  AND previous.StockUpdated = 0
                  AND grn.StockUpdated = 1
                  AND (
                       purchaseOrder.Id IS NULL
                    OR purchaseOrder.CreatedById IS NULL
                    OR grn.LastModifiedById IS NULL
                    OR grn.LastModifiedById = purchaseOrder.CreatedById
                  ))
                THROW 51562, 'RCV_GRN_STOCK_SOD_BLOCKED: GRN stock confirmation requires an actor distinct from the purchase-order creator.', 1;
        END
        """;

    // TR_PurchaseOrderReceipts_SodHardStop: preserve workflow, tenant, source, and audit predicates.
    private static readonly (string Before, string After)[] Replacements1 =
    [
        ("receipt.ReceivedById = purchaseOrder.CreatedById", "(receipt.ReceivedById = purchaseOrder.CreatedById AND dbo.ProcurementSodRequired(receipt.TenantId) = 1)"),
    ];
    private const string Trigger1 = """
        CREATE OR ALTER TRIGGER [TR_PurchaseOrderReceipts_SodHardStop]
        ON [PurchaseOrderReceipts]
        AFTER INSERT, UPDATE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS (
                SELECT 1
                FROM inserted receipt
                LEFT JOIN PurchaseOrders purchaseOrder
                  ON purchaseOrder.Id = receipt.PurchaseOrderId
                 AND purchaseOrder.TenantId = receipt.TenantId
                 AND purchaseOrder.IsDeleted = 0
                WHERE receipt.IsDeleted = 0
                  AND (
                       purchaseOrder.Id IS NULL
                    OR receipt.ReceivedById IS NULL
                    OR purchaseOrder.CreatedById IS NULL
                    OR receipt.ReceivedById = purchaseOrder.CreatedById
                  ))
                THROW 51261, 'A purchase-order receipt requires a confirmer distinct from the purchase-order creator.', 1;
        END
        """;

    // TR_PurchaseOrders_SodHardStop: preserve workflow, tenant, source, and audit predicates.
    private static readonly (string Before, string After)[] Replacements2 =
    [
        ("purchaseOrder.ApprovedById =\n               purchaseOrder.RequestedById", "(purchaseOrder.ApprovedById =\n               purchaseOrder.RequestedById AND dbo.ProcurementSodRequired(purchaseOrder.TenantId) = 1)"),
        ("purchaseOrder.ApprovedById =\n               purchaseOrder.CreatedById", "(purchaseOrder.ApprovedById =\n               purchaseOrder.CreatedById AND dbo.ProcurementSodRequired(purchaseOrder.TenantId) = 1)"),
    ];
    private const string Trigger2 = """
        CREATE OR ALTER TRIGGER [TR_PurchaseOrders_SodHardStop]
        ON [PurchaseOrders]
        AFTER INSERT, UPDATE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS (
                SELECT 1
                FROM inserted purchaseOrder
                WHERE purchaseOrder.IsDeleted = 0
                  AND UPPER(LTRIM(RTRIM(purchaseOrder.Status))) = 'APPROVED'
                  AND (
                       purchaseOrder.ApprovedById IS NULL
                    OR purchaseOrder.RequestedById IS NULL
                    OR purchaseOrder.CreatedById IS NULL
                    OR purchaseOrder.ApprovedById =
                       purchaseOrder.RequestedById
                    OR purchaseOrder.ApprovedById =
                       purchaseOrder.CreatedById
                  ))
                THROW 51260, 'An Approved purchase order requires an independent approver distinct from its requester and creator.', 1;
        END
        """;

    // TR_InventoryMovements_TDC0503ReceiptSod: preserve workflow, tenant, source, and audit predicates.
    private static readonly (string Before, string After)[] Replacements3 =
    [
        ("movement.CreatedById = purchaseOrder.CreatedById", "(movement.CreatedById = purchaseOrder.CreatedById AND dbo.ProcurementSodRequired(movement.TenantId) = 1)"),
    ];
    private const string Trigger3 = """
        CREATE OR ALTER TRIGGER [dbo].[TR_InventoryMovements_TDC0503ReceiptSod]
        ON [dbo].[InventoryMovements]
        AFTER INSERT
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS (
                SELECT 1
                FROM inserted movement
                LEFT JOIN PurchaseOrderReceipts receipt
                  ON receipt.Id = movement.ReferenceId
                 AND receipt.TenantId = movement.TenantId
                 AND receipt.IsDeleted = 0
                LEFT JOIN PurchaseOrders purchaseOrder
                  ON purchaseOrder.Id = receipt.PurchaseOrderId
                 AND purchaseOrder.TenantId = movement.TenantId
                 AND purchaseOrder.IsDeleted = 0
                WHERE movement.IsDeleted = 0
                  AND movement.MovementType = 1
                  AND movement.Direction = 1
                  AND movement.ReferenceType = 1
                  AND (
                       receipt.Id IS NULL
                    OR purchaseOrder.Id IS NULL
                    OR purchaseOrder.CreatedById IS NULL
                    OR movement.CreatedById IS NULL
                    OR movement.CreatedById = purchaseOrder.CreatedById
                  ))
                THROW 51564, 'RCV_INVENTORY_SOD_BLOCKED: purchase-receipt inventory posting requires an actor distinct from the purchase-order creator.', 1;
        END
        """;

    // TR_StockMovements_TDC0503ReceiptSod: preserve workflow, tenant, source, and audit predicates.
    private static readonly (string Before, string After)[] Replacements4 =
    [
        ("movement.ProcessedById = purchaseOrder.CreatedById", "(movement.ProcessedById = purchaseOrder.CreatedById AND dbo.ProcurementSodRequired(movement.TenantId) = 1)"),
    ];
    private const string Trigger4 = """
        CREATE OR ALTER TRIGGER [dbo].[TR_StockMovements_TDC0503ReceiptSod]
        ON [dbo].[StockMovements]
        AFTER INSERT
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS (
                SELECT 1
                FROM inserted movement
                LEFT JOIN GoodsReceiptNotes grn
                  ON grn.Id = movement.ReferenceId
                 AND grn.TenantId = movement.TenantId
                 AND grn.IsDeleted = 0
                LEFT JOIN PurchaseOrders purchaseOrder
                  ON purchaseOrder.Id = grn.PurchaseOrderId
                 AND purchaseOrder.TenantId = movement.TenantId
                 AND purchaseOrder.IsDeleted = 0
                WHERE movement.IsDeleted = 0
                  AND movement.MovementType = N'Receipt'
                  AND movement.ReferenceType = 1
                  AND grn.PurchaseOrderId IS NOT NULL
                  AND (
                       grn.Id IS NULL
                    OR purchaseOrder.Id IS NULL
                    OR purchaseOrder.CreatedById IS NULL
                    OR movement.ProcessedById IS NULL
                    OR movement.ProcessedById = purchaseOrder.CreatedById
                  ))
                THROW 51565, 'RCV_STOCK_SOD_BLOCKED: purchase GRN stock posting requires an actor distinct from the purchase-order creator.', 1;
        END
        """;

    // TR_ProcurementReceiptInspectionActions_TDC0503SodHardStop: preserve workflow, tenant, source, and audit predicates.
    private static readonly (string Before, string After)[] Replacements5 =
    [
        ("action.ActorUserId = purchaseOrder.CreatedById", "(action.ActorUserId = purchaseOrder.CreatedById AND dbo.ProcurementSodRequired(action.TenantId) = 1)"),
    ];
    private const string Trigger5 = """
        CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementReceiptInspectionActions_TDC0503SodHardStop]
        ON [dbo].[ProcurementReceiptInspectionActions]
        AFTER INSERT
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS (
                SELECT 1
                FROM inserted action
                LEFT JOIN ProcurementReceiptInspectionCases inspection
                  ON inspection.Id = action.InspectionCaseId
                 AND inspection.TenantId = action.TenantId
                 AND inspection.IsDeleted = 0
                LEFT JOIN PurchaseOrderReceipts receipt
                  ON receipt.Id = inspection.PurchaseOrderReceiptId
                 AND receipt.TenantId = action.TenantId
                 AND receipt.IsDeleted = 0
                LEFT JOIN PurchaseOrders purchaseOrder
                  ON purchaseOrder.Id = receipt.PurchaseOrderId
                 AND purchaseOrder.TenantId = action.TenantId
                 AND purchaseOrder.IsDeleted = 0
                WHERE action.IsDeleted = 0
                  AND action.ActionType IN (3, 11, 12)
                  AND (
                       inspection.Id IS NULL
                    OR receipt.Id IS NULL
                    OR purchaseOrder.Id IS NULL
                    OR purchaseOrder.CreatedById IS NULL
                    OR action.ActorUserId = purchaseOrder.CreatedById
                  ))
                THROW 51563, 'RCV_INSPECTION_SOD_BLOCKED: positive inspection, replacement receipt, and closure actions require an actor distinct from the purchase-order creator.', 1;
        END
        """;

    // TR_ProcurementReceiptInspectionCases_TDC0502Protected: preserve workflow, tenant, source, and audit predicates.
    private static readonly (string Before, string After)[] Replacements6 =
    [
        ("i.DecidedByUserId = i.SubmittedByUserId", "(i.DecidedByUserId = i.SubmittedByUserId AND dbo.ProcurementSodRequired(i.TenantId) = 1)"),
        ("i.DecidedByUserId = i.CreatedByUserId", "(i.DecidedByUserId = i.CreatedByUserId AND dbo.ProcurementSodRequired(i.TenantId) = 1)"),
    ];
    private const string Trigger6 = """
        CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementReceiptInspectionCases_TDC0502Protected]
        ON [dbo].[ProcurementReceiptInspectionCases]
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                THROW 51501, 'RCV_INSPECTION_DELETE_FORBIDDEN: receipt-inspection cases cannot be deleted.', 1;

            IF EXISTS (
                SELECT 1 FROM inserted i
                LEFT JOIN PurchaseOrderReceipts r ON r.Id = i.PurchaseOrderReceiptId AND r.TenantId = i.TenantId AND r.IsDeleted = 0
                LEFT JOIN WorkflowDefinitions w ON w.Id = i.WorkflowDefinitionId AND w.TenantId = i.TenantId AND w.IsDeleted = 0
                WHERE i.IsDeleted = 0 AND (r.Id IS NULL OR w.Id IS NULL))
                THROW 51502, 'RCV_INSPECTION_TENANT_MISMATCH: receipt and workflow must belong to the inspection tenant.', 1;

            IF EXISTS (
                SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                WHERE i.TenantId <> d.TenantId
                   OR i.PurchaseOrderReceiptId <> d.PurchaseOrderReceiptId
                   OR i.Sequence <> d.Sequence
                   OR i.ConfigurationProfileId <> d.ConfigurationProfileId
                   OR i.ConfigurationProfileVersion <> d.ConfigurationProfileVersion
                   OR i.PolicySetId <> d.PolicySetId
                   OR i.PolicyVersion <> d.PolicyVersion
                   OR i.AuthorityRuleId <> d.AuthorityRuleId
                   OR i.WorkflowDefinitionId <> d.WorkflowDefinitionId
                   OR i.CreatedByUserId <> d.CreatedByUserId
                   OR i.IdempotencyKey <> d.IdempotencyKey
                   OR i.SourceSnapshotJson <> d.SourceSnapshotJson
                   OR i.SourceSnapshotHash <> d.SourceSnapshotHash)
                THROW 51503, 'RCV_INSPECTION_LINEAGE_IMMUTABLE: source and governance lineage are immutable.', 1;

            IF EXISTS (
                SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                WHERE i.Status <> d.Status AND NOT (
                    (d.Status = 0 AND i.Status = 1) OR
                    (d.Status = 1 AND i.Status IN (3,4,8,9)) OR
                    (d.Status = 4 AND i.Status IN (5,6)) OR
                    (d.Status = 5 AND i.Status = 7) OR
                    (d.Status = 6 AND i.Status = 7) OR
                    (d.Status = 7 AND i.Status = 8)))
                THROW 51504, 'RCV_INSPECTION_TRANSITION_INVALID: receipt-inspection status transition is not allowed.', 1;

            IF EXISTS (
                SELECT 1
                FROM inserted i
                JOIN deleted d ON d.Id = i.Id
                LEFT JOIN WorkflowInstances wi
                  ON wi.Id = i.WorkflowInstanceId
                 AND wi.TenantId = i.TenantId
                 AND wi.EntityId = i.Id
                 AND wi.WorkflowDefinitionId = i.WorkflowDefinitionId
                WHERE d.Status = 1 AND i.Status IN (4,8)
                  AND (TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT(N'TDC0502_RECEIPT_INSPECTION_CASE_ID')) <> i.Id
                       OR wi.Id IS NULL OR wi.Status <> 2
                       OR i.DecidedByUserId IS NULL
                       OR i.DecidedByUserId = i.SubmittedByUserId
                       OR i.DecidedByUserId = i.CreatedByUserId))
                THROW 51505, 'RCV_INSPECTION_APPROVAL_FORBIDDEN: final acceptance requires the exact completed workflow, independent approver, and protected transaction context.', 1;

            IF EXISTS (
                SELECT 1
                FROM inserted i JOIN deleted d ON d.Id = i.Id
                LEFT JOIN WorkflowInstances wi
                  ON wi.Id = i.WorkflowInstanceId
                 AND wi.TenantId = i.TenantId
                 AND wi.EntityId = i.Id
                WHERE d.Status = 1 AND i.Status = 3
                  AND (wi.Id IS NULL OR wi.Status NOT IN (3,4)))
                THROW 51506, 'RCV_INSPECTION_REJECTION_FORBIDDEN: only a rejected shared-workflow outcome may reject an inspection.', 1;
        END
        """;

    // TR_ProcurementWorksCloseoutActions_TDC0409Protected: preserve workflow, tenant, source, and audit predicates.
    private static readonly (string Before, string After)[] Replacements7 =
    [
        ("i.[DecidedById] = i.[SubmittedById]", "(i.[DecidedById] = i.[SubmittedById] AND dbo.ProcurementSodRequired(i.[TenantId]) = 1)"),
        ("i.[DecidedById] = c.[CreatedById]", "(i.[DecidedById] = c.[CreatedById] AND dbo.ProcurementSodRequired(i.[TenantId]) = 1)"),
    ];
    private const string Trigger7 = """
        CREATE OR ALTER TRIGGER [TR_ProcurementWorksCloseoutActions_TDC0409Protected]
        ON [ProcurementWorksCloseoutActions]
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS (SELECT 1 FROM deleted) AND NOT EXISTS (SELECT 1 FROM inserted)
                THROW 54091, 'TDC-0409 Works closeout actions are immutable and cannot be deleted.', 1;

            IF EXISTS
            (
                SELECT 1
                FROM inserted i
                LEFT JOIN [Contracts] c ON c.[Id] = i.[ContractId] AND c.[TenantId] = i.[TenantId] AND c.[IsDeleted] = 0
                LEFT JOIN [Projects] p ON p.[Id] = i.[ProjectId] AND p.[TenantId] = i.[TenantId] AND p.[IsDeleted] = 0
                WHERE c.[Id] IS NULL OR UPPER(LTRIM(RTRIM(c.[ContractType]))) <> 'WORKS'
                   OR p.[Id] IS NULL OR p.[ContractId] <> i.[ContractId]
                   OR i.[AmountAutoPosted] <> 0
            )
                THROW 54092, 'TDC-0409 requires a same-tenant Works contract/project and forbids automatic Finance posting.', 1;

            IF EXISTS
            (
                SELECT 1
                FROM inserted i
                JOIN deleted d ON d.[Id] = i.[Id]
                WHERE i.[TenantId] <> d.[TenantId]
                   OR i.[ContractId] <> d.[ContractId]
                   OR i.[ProjectId] <> d.[ProjectId]
                   OR i.[Sequence] <> d.[Sequence]
                   OR i.[ActionType] <> d.[ActionType]
                   OR i.[ConfigurationProfileId] <> d.[ConfigurationProfileId]
                   OR i.[ConfigurationProfileVersion] <> d.[ConfigurationProfileVersion]
                   OR i.[PolicySetId] <> d.[PolicySetId]
                   OR i.[PolicyVersion] <> d.[PolicyVersion]
                   OR i.[AuthorityRuleId] <> d.[AuthorityRuleId]
                   OR i.[WorkflowDefinitionId] <> d.[WorkflowDefinitionId]
                   OR i.[SubmittedById] <> d.[SubmittedById]
                   OR i.[SubmittedAtUtc] <> d.[SubmittedAtUtc]
                   OR i.[IdempotencyKey] <> d.[IdempotencyKey]
                   OR i.[CreatedAt] <> d.[CreatedAt]
                   OR i.[IsDeleted] <> 0
            )
                THROW 54093, 'TDC-0409 immutable action identity or control lineage cannot be changed.', 1;

            IF EXISTS
            (
                SELECT 1
                FROM inserted i
                JOIN deleted d ON d.[Id] = i.[Id]
                WHERE NOT
                (
                    (d.[Status] = 0 AND i.[Status] IN (0,1,2,3))
                    OR (d.[Status] = 3 AND i.[Status] IN (1,3))
                )
            )
                THROW 54094, 'TDC-0409 Works closeout status transition is not allowed.', 1;

            IF EXISTS
            (
                SELECT 1
                FROM inserted i
                JOIN deleted d ON d.[Id] = i.[Id]
                JOIN [Contracts] c ON c.[Id] = i.[ContractId] AND c.[TenantId] = i.[TenantId]
                LEFT JOIN [WorkflowInstances] wi ON wi.[Id] = i.[WorkflowInstanceId]
                WHERE d.[Status] <> 1 AND i.[Status] = 1
                  AND
                  (
                      TRY_CONVERT(uniqueidentifier,
                          SESSION_CONTEXT(N'TDC0409_WORKS_CLOSEOUT_ACTION_ID')) IS NULL
                      OR TRY_CONVERT(uniqueidentifier,
                          SESSION_CONTEXT(N'TDC0409_WORKS_CLOSEOUT_ACTION_ID')) <> i.[Id]
                      OR wi.[Id] IS NULL
                      OR wi.[TenantId] <> i.[TenantId]
                      OR wi.[EntityId] <> i.[Id]
                      OR wi.[WorkflowDefinitionId] <> i.[WorkflowDefinitionId]
                      OR wi.[Status] <> 2
                      OR i.[DecidedById] IS NULL
                      OR i.[DecidedById] = i.[SubmittedById]
                      OR i.[DecidedById] = c.[CreatedById]
                  )
            )
                THROW 54095, 'TDC-0409 approval requires the exact completed workflow, an independent actor, and the protected transaction context.', 1;
        END
        """;

    // TR_ProcurementEvaluationScoreRecalls_Lifecycle: preserve workflow, tenant, source, and audit predicates.
    private static readonly (string Before, string After)[] Replacements8 =
    [
        ("i.DecidedByUserId IN (i.RequestedByUserId, s.SubmittedByUserId)", "(i.DecidedByUserId IN (i.RequestedByUserId, s.SubmittedByUserId) AND dbo.ProcurementSodRequired(i.TenantId) = 1)"),
        ("ISNULL(wa.ProcessedById, wa.ApproverId) IN (i.RequestedByUserId, s.SubmittedByUserId)", "(ISNULL(wa.ProcessedById, wa.ApproverId) IN (i.RequestedByUserId, s.SubmittedByUserId) AND dbo.ProcurementSodRequired(i.TenantId) = 1)"),
    ];
    private const string Trigger8 = """
        CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementEvaluationScoreRecalls_Lifecycle]
        ON [dbo].[ProcurementEvaluationScoreRecalls]
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                THROW 51360, 'Evaluation score recalls cannot be deleted.', 1;

            IF EXISTS (
                SELECT 1
                FROM inserted i
                LEFT JOIN [dbo].[ProcurementEvaluationScoreSheets] s
                  ON s.Id = i.ScoreSheetId AND s.TenantId = i.TenantId AND s.IsDeleted = 0
                LEFT JOIN [dbo].[WorkflowDefinitions] wd
                  ON wd.Id = i.WorkflowDefinitionId AND wd.TenantId = i.TenantId AND wd.IsDeleted = 0
                LEFT JOIN [dbo].[WorkflowInstances] wi
                  ON wi.Id = i.WorkflowInstanceId AND wi.TenantId = i.TenantId AND wi.IsDeleted = 0
                LEFT JOIN [dbo].[WorkflowEvidenceDocuments] we
                  ON we.Id = i.WorkflowEvidenceDocumentId AND we.TenantId = i.TenantId AND we.IsDeleted = 0
                LEFT JOIN [dbo].[FileUploadRecords] fu
                  ON fu.Id = i.FileUploadRecordId AND fu.TenantId = i.TenantId AND fu.IsDeleted = 0
                WHERE i.IsDeleted = 1 OR s.Id IS NULL OR wd.Id IS NULL
                   OR wd.LifecycleStatus <> 1 OR wd.IsActive = 0
                   OR i.RequestedByUserId <> s.SubmittedByUserId
                   OR (i.WorkflowInstanceId IS NOT NULL AND
                       (wi.Id IS NULL OR wi.WorkflowDefinitionId <> i.WorkflowDefinitionId
                        OR wi.EntityId <> i.Id OR wi.EntityTypeId <> wd.EntityTypeId))
                   OR (i.WorkflowEvidenceDocumentId IS NOT NULL AND we.Id IS NULL)
                   OR (i.FileUploadRecordId IS NOT NULL AND fu.Id IS NULL))
                THROW 51361, 'Evaluation score recall sheet, requester, workflow, evidence, or tenant lineage is invalid.', 1;

            IF EXISTS (
                SELECT 1
                FROM inserted i
                JOIN deleted d ON d.Id = i.Id
                WHERE i.TenantId <> d.TenantId OR i.ScoreSheetId <> d.ScoreSheetId
                   OR i.Reason <> d.Reason OR i.EvidenceReference <> d.EvidenceReference
                   OR ISNULL(i.WorkflowEvidenceDocumentId, '00000000-0000-0000-0000-000000000000')
                      <> ISNULL(d.WorkflowEvidenceDocumentId, '00000000-0000-0000-0000-000000000000')
                   OR ISNULL(i.FileUploadRecordId, '00000000-0000-0000-0000-000000000000')
                      <> ISNULL(d.FileUploadRecordId, '00000000-0000-0000-0000-000000000000')
                   OR i.WorkflowDefinitionId <> d.WorkflowDefinitionId
                   OR i.RequestedByUserId <> d.RequestedByUserId OR i.RequestedByName <> d.RequestedByName
                   OR i.RequestedAtUtc <> d.RequestedAtUtc OR i.IdempotencyKey <> d.IdempotencyKey
                   OR i.CreatedAt <> d.CreatedAt OR i.IsDeleted <> d.IsDeleted
                   OR NOT (
                       (d.Status = 0 AND i.Status = 0
                        AND d.WorkflowInstanceId IS NULL AND i.WorkflowInstanceId IS NOT NULL)
                       OR (d.Status = 0 AND i.Status IN (1, 2))
                       OR (d.Status IN (1, 2) AND i.Status = d.Status))
                   OR (d.Status = 0 AND i.Status = 0 AND
                       (i.DecidedByUserId IS NOT NULL OR i.DecidedAtUtc IS NOT NULL
                        OR i.DecisionReference IS NOT NULL OR i.DecisionEvidenceReference IS NOT NULL
                        OR i.DecisionIdempotencyKey IS NOT NULL OR i.AuthorizedNewAttempt IS NOT NULL))
                   OR (d.Status IN (1, 2) AND
                       (ISNULL(i.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000')
                          <> ISNULL(d.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000')
                        OR ISNULL(i.DecidedByUserId, '00000000-0000-0000-0000-000000000000')
                          <> ISNULL(d.DecidedByUserId, '00000000-0000-0000-0000-000000000000')
                        OR ISNULL(i.DecidedAtUtc, '19000101') <> ISNULL(d.DecidedAtUtc, '19000101')
                        OR ISNULL(i.DecisionReference, '') <> ISNULL(d.DecisionReference, '')
                        OR ISNULL(i.DecisionEvidenceReference, '') <> ISNULL(d.DecisionEvidenceReference, '')
                        OR ISNULL(i.DecisionIdempotencyKey, '') <> ISNULL(d.DecisionIdempotencyKey, '')
                        OR ISNULL(i.AuthorizedNewAttempt, 0) <> ISNULL(d.AuthorizedNewAttempt, 0)
                        OR i.SnapshotJson <> d.SnapshotJson OR i.IntegrityHash <> d.IntegrityHash)))
                THROW 51362, 'Evaluation score recall immutable lineage or lifecycle transition is invalid.', 1;

            IF EXISTS (
                SELECT 1
                FROM inserted i
                JOIN deleted d ON d.Id = i.Id
                JOIN [dbo].[ProcurementEvaluationScoreSheets] s
                  ON s.Id = i.ScoreSheetId AND s.TenantId = i.TenantId
                LEFT JOIN [dbo].[WorkflowInstances] wi
                  ON wi.Id = i.WorkflowInstanceId AND wi.TenantId = i.TenantId AND wi.IsDeleted = 0
                WHERE d.Status = 0 AND i.Status IN (1, 2)
                  AND (
                      wi.Id IS NULL
                      OR (i.Status = 1 AND wi.Status <> 2)
                      OR (i.Status = 2 AND wi.Status NOT IN (3, 4))
                      OR i.DecidedByUserId IN (i.RequestedByUserId, s.SubmittedByUserId)
                      OR (i.Status = 1 AND i.AuthorizedNewAttempt <> s.Attempt + 1)
                      OR (i.Status = 2 AND i.AuthorizedNewAttempt IS NOT NULL)
                      OR NOT EXISTS (
                          SELECT 1
                          FROM [dbo].[WorkflowStepInstances] wsi
                          JOIN [dbo].[WorkflowApprovals] wa
                            ON wa.StepInstanceId = wsi.Id AND wa.TenantId = i.TenantId AND wa.IsDeleted = 0
                          WHERE wsi.WorkflowInstanceId = wi.Id AND wsi.TenantId = i.TenantId AND wsi.IsDeleted = 0
                            AND wa.ProcessedDate IS NOT NULL
                            AND wa.Status NOT IN (0, 6))
                      OR EXISTS (
                          SELECT 1
                          FROM [dbo].[WorkflowStepInstances] wsi
                          JOIN [dbo].[WorkflowApprovals] wa
                            ON wa.StepInstanceId = wsi.Id AND wa.TenantId = i.TenantId AND wa.IsDeleted = 0
                          WHERE wsi.WorkflowInstanceId = wi.Id AND wsi.TenantId = i.TenantId AND wsi.IsDeleted = 0
                            AND wa.ProcessedDate IS NOT NULL
                            AND ISNULL(wa.ProcessedById, wa.ApproverId) IN (i.RequestedByUserId, s.SubmittedByUserId))))
                THROW 51363, 'Evaluation score recall outcome is not supported by the exact independent shared-workflow decision.', 1;
        END
        """;

    // TR_ProcurementReceiptDocumentSignatures_TDC0509Immutable: preserve workflow, tenant, source, and audit predicates.
    private static readonly (string Before, string After)[] Replacements9 =
    [
        ("AND i.Id <> s.Id)", "AND i.Id <> s.Id\n        WHERE dbo.ProcurementSodRequired(i.TenantId) = 1)"),
    ];
    private const string Trigger9 = """
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
        """;

    // TR_ProcurementGhanepsExchangeAcknowledgements_Immutable: preserve workflow, tenant, source, and audit predicates.
    private static readonly (string Before, string After)[] Replacements10 =
    [
        ("e.PreparedByUserId = i.AcknowledgedByUserId", "(e.PreparedByUserId = i.AcknowledgedByUserId AND dbo.ProcurementSodRequired(i.TenantId) = 1)"),
        ("p.RecordedByUserId = i.AcknowledgedByUserId", "(p.RecordedByUserId = i.AcknowledgedByUserId AND dbo.ProcurementSodRequired(i.TenantId) = 1)"),
        ("a.AttemptedByUserId = i.AcknowledgedByUserId", "(a.AttemptedByUserId = i.AcknowledgedByUserId AND dbo.ProcurementSodRequired(i.TenantId) = 1)"),
        ("priorActor.AcknowledgedByUserId = i.AcknowledgedByUserId", "(priorActor.AcknowledgedByUserId = i.AcknowledgedByUserId AND dbo.ProcurementSodRequired(i.TenantId) = 1)"),
    ];
    private const string Trigger10 = """
        CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementGhanepsExchangeAcknowledgements_Immutable]
        ON [dbo].[ProcurementGhanepsExchangeAcknowledgements]
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS (SELECT 1 FROM deleted)
                THROW 51630, 'GHANEPS exchange acknowledgements are append-only.', 1;

            IF EXISTS (
                SELECT 1
                FROM inserted i
                WHERE i.IsDeleted = 1
                   OR i.AcknowledgedByUserId = '00000000-0000-0000-0000-000000000000'
                   OR NULLIF(LTRIM(RTRIM(i.AcknowledgedByName)), '') IS NULL
                   OR NULLIF(LTRIM(RTRIM(i.AcknowledgementReference)), '') IS NULL
                   OR NULLIF(LTRIM(RTRIM(i.ContentType)), '') IS NULL
                   OR NULLIF(LTRIM(RTRIM(i.EvidenceReference)), '') IS NULL
                   OR NULLIF(LTRIM(RTRIM(i.IdempotencyKey)), '') IS NULL
                   OR i.AcknowledgementChecksumSha256 COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                   OR i.RequestFingerprint COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                   OR i.IntegrityHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                   OR NOT EXISTS (
                       SELECT 1
                       FROM [dbo].[ProcurementGhanepsExchangeEvents] e
                       JOIN [dbo].[ProcurementGhanepsExchangeAttempts] a
                         ON a.Id = i.AttemptId
                        AND a.ExchangeEventId = e.Id
                        AND a.TenantId = e.TenantId
                       JOIN [dbo].[ProcurementGhanepsExchangePayloads] p
                         ON p.Id = i.PayloadId
                        AND p.ExchangeEventId = e.Id
                        AND p.TenantId = e.TenantId
                       WHERE e.Id = i.ExchangeEventId
                         AND e.TenantId = i.TenantId
                         AND e.AcknowledgementRequired = 1
                          AND e.AcknowledgementContentType = i.ContentType
                          AND a.PayloadId = p.Id
                          AND a.Outcome = 0
                          AND a.AttemptNumber = (
                              SELECT MAX(latest.AttemptNumber)
                              FROM [dbo].[ProcurementGhanepsExchangeAttempts] latest
                              WHERE latest.TenantId = i.TenantId
                                AND latest.ExchangeEventId = i.ExchangeEventId
                                AND latest.IsDeleted = 0
                          )
                          AND a.AttemptedAtUtc <= i.AcknowledgedAtUtc
                          AND (
                              e.Status = 2
                              OR (i.Outcome = 0 AND e.Status = 3)
                              OR (i.Outcome = 1 AND e.Status = 4)
                          )
                         AND e.IsDeleted = 0
                         AND a.IsDeleted = 0
                         AND p.IsDeleted = 0
                   )
                   OR i.Sequence <> (
                       SELECT COUNT(*)
                       FROM [dbo].[ProcurementGhanepsExchangeAcknowledgements] x
                       WHERE x.TenantId = i.TenantId
                         AND x.ExchangeEventId = i.ExchangeEventId
                         AND x.Sequence <= i.Sequence
                         AND x.IsDeleted = 0
                   )
                   OR EXISTS (
                       SELECT 1
                       FROM [dbo].[ProcurementGhanepsExchangeEvents] e
                       WHERE e.Id = i.ExchangeEventId
                         AND e.TenantId = i.TenantId
                         AND e.PreparedByUserId = i.AcknowledgedByUserId
                   )
                   OR EXISTS (
                       SELECT 1
                       FROM [dbo].[ProcurementGhanepsExchangePayloads] p
                       WHERE p.ExchangeEventId = i.ExchangeEventId
                         AND p.TenantId = i.TenantId
                         AND p.RecordedByUserId = i.AcknowledgedByUserId
                   )
                   OR EXISTS (
                       SELECT 1
                       FROM [dbo].[ProcurementGhanepsExchangeAttempts] a
                       WHERE a.ExchangeEventId = i.ExchangeEventId
                         AND a.TenantId = i.TenantId
                         AND a.AttemptedByUserId = i.AcknowledgedByUserId
                   )
                   OR EXISTS (
                       SELECT 1
                       FROM [dbo].[ProcurementGhanepsExchangeAcknowledgements] priorActor
                       WHERE priorActor.ExchangeEventId = i.ExchangeEventId
                         AND priorActor.TenantId = i.TenantId
                         AND priorActor.Id <> i.Id
                         AND priorActor.AcknowledgedByUserId = i.AcknowledgedByUserId
                         AND priorActor.IsDeleted = 0
                   )
                   OR EXISTS (
                       SELECT 1
                       FROM [dbo].[ProcurementGhanepsExchangeAcknowledgements] prior
                       WHERE prior.ExchangeEventId = i.ExchangeEventId
                         AND prior.TenantId = i.TenantId
                         AND prior.Id <> i.Id
                         AND prior.Outcome = 0
                         AND prior.IsDeleted = 0
                   )
            )
                THROW 51631, 'GHANEPS acknowledgement event, attempt, payload, sequence, SOD, actor, or hash lineage is invalid.', 1;
        END
        """;

    // TR_ProcurementGhanepsExchangeReconciliations_Immutable: preserve workflow, tenant, source, and audit predicates.
    private static readonly (string Before, string After)[] Replacements11 =
    [
        ("e.PreparedByUserId = i.ReconciledByUserId", "(e.PreparedByUserId = i.ReconciledByUserId AND dbo.ProcurementSodRequired(i.TenantId) = 1)"),
        ("p.RecordedByUserId = i.ReconciledByUserId", "(p.RecordedByUserId = i.ReconciledByUserId AND dbo.ProcurementSodRequired(i.TenantId) = 1)"),
        ("a.AttemptedByUserId = i.ReconciledByUserId", "(a.AttemptedByUserId = i.ReconciledByUserId AND dbo.ProcurementSodRequired(i.TenantId) = 1)"),
        ("priorActor.ReconciledByUserId = i.ReconciledByUserId", "(priorActor.ReconciledByUserId = i.ReconciledByUserId AND dbo.ProcurementSodRequired(i.TenantId) = 1)"),
        ("ack.AcknowledgedByUserId = i.ReconciledByUserId", "(ack.AcknowledgedByUserId = i.ReconciledByUserId AND dbo.ProcurementSodRequired(i.TenantId) = 1)"),
        ("prior.ReconciledByUserId <> i.ReconciledByUserId", "(prior.ReconciledByUserId <> i.ReconciledByUserId OR dbo.ProcurementSodRequired(i.TenantId) = 0)"),
    ];
    private const string Trigger11 = """
        CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementGhanepsExchangeReconciliations_Immutable]
        ON [dbo].[ProcurementGhanepsExchangeReconciliations]
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS (SELECT 1 FROM deleted)
                THROW 51640, 'GHANEPS exchange reconciliations are append-only.', 1;

            IF EXISTS (
                SELECT 1
                FROM inserted i
                WHERE i.IsDeleted = 1
                   OR i.ReconciledByUserId = '00000000-0000-0000-0000-000000000000'
                   OR NULLIF(LTRIM(RTRIM(i.ReconciledByName)), '') IS NULL
                   OR NULLIF(LTRIM(RTRIM(i.ActualReference)), '') IS NULL
                   OR NULLIF(LTRIM(RTRIM(i.ActualChecksumSha256)), '') IS NULL
                   OR NULLIF(LTRIM(RTRIM(i.EvidenceReference)), '') IS NULL
                   OR NULLIF(LTRIM(RTRIM(i.IdempotencyKey)), '') IS NULL
                   OR i.ExpectedChecksumSha256 COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                   OR i.ActualChecksumSha256 COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                   OR i.RequestFingerprint COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                   OR i.IntegrityHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                   OR NOT EXISTS (
                       SELECT 1
                       FROM [dbo].[ProcurementGhanepsExchangeEvents] e
                       JOIN [dbo].[ProcurementGhanepsExchangeAttempts] a
                         ON a.Id = i.AttemptId
                        AND a.ExchangeEventId = e.Id
                        AND a.TenantId = e.TenantId
                       JOIN [dbo].[ProcurementGhanepsExchangePayloads] p
                         ON p.Id = i.PayloadId
                        AND p.ExchangeEventId = e.Id
                        AND p.TenantId = e.TenantId
                       WHERE e.Id = i.ExchangeEventId
                         AND e.TenantId = i.TenantId
                          AND e.ReconciliationRequired = 1
                          AND a.PayloadId = p.Id
                          AND a.Outcome = 0
                          AND (
                              (i.Outcome IN (0, 1)
                               AND (
                                   e.Status IN (1, 3)
                                   OR (i.Outcome = 0 AND e.Status = 5)
                                   OR (i.Outcome = 1 AND e.Status = 6)
                               ))
                              OR (i.Outcome = 2
                                  AND (
                                      e.Status IN (5, 6)
                                      OR (e.Status IN (1, 3)
                                          AND EXISTS (
                                              SELECT 1
                                              FROM inserted batchMismatch
                                              WHERE batchMismatch.TenantId = i.TenantId
                                                AND batchMismatch.ExchangeEventId =
                                                    i.ExchangeEventId
                                                AND batchMismatch.Sequence = i.Sequence - 1
                                                AND batchMismatch.Outcome = 1
                                          ))
                                  ))
                          )
                          AND i.ExpectedReference = e.EventReference
                         AND i.ExpectedChecksumSha256 = p.PayloadChecksumSha256
                         AND a.AttemptNumber = (
                             SELECT MAX(latest.AttemptNumber)
                             FROM [dbo].[ProcurementGhanepsExchangeAttempts] latest
                             WHERE latest.TenantId = i.TenantId
                               AND latest.ExchangeEventId = i.ExchangeEventId
                               AND latest.Outcome = 0
                               AND latest.IsDeleted = 0
                         )
                         AND (
                             e.AcknowledgementRequired = 0
                             OR EXISTS (
                                 SELECT 1
                                 FROM [dbo].[ProcurementGhanepsExchangeAcknowledgements] ack
                                 WHERE ack.TenantId = i.TenantId
                                   AND ack.ExchangeEventId = i.ExchangeEventId
                                   AND ack.AttemptId = i.AttemptId
                                   AND ack.PayloadId = i.PayloadId
                                   AND ack.Outcome = 0
                                   AND ack.IsDeleted = 0
                             )
                         )
                         AND e.IsDeleted = 0
                         AND a.IsDeleted = 0
                         AND p.IsDeleted = 0
                   )
                   OR i.Sequence <> (
                       SELECT COUNT(*)
                       FROM [dbo].[ProcurementGhanepsExchangeReconciliations] x
                       WHERE x.TenantId = i.TenantId
                         AND x.ExchangeEventId = i.ExchangeEventId
                         AND x.Sequence <= i.Sequence
                         AND x.IsDeleted = 0
                   )
                   OR (i.Outcome = 0 AND (
                       i.Sequence <> 1
                       OR i.ActualReference <> i.ExpectedReference
                       OR i.ActualChecksumSha256 <> i.ExpectedChecksumSha256
                   ))
                   OR (i.Outcome = 1 AND (
                       i.Sequence <> 1
                       OR (i.ActualReference = i.ExpectedReference
                           AND i.ActualChecksumSha256 = i.ExpectedChecksumSha256)
                   ))
                   OR (i.Outcome = 2 AND (
                       i.Sequence <= 1
                       OR NOT EXISTS (
                           SELECT 1
                           FROM [dbo].[ProcurementGhanepsExchangeReconciliations] prior
                           WHERE prior.TenantId = i.TenantId
                             AND prior.ExchangeEventId = i.ExchangeEventId
                             AND prior.Sequence = i.Sequence - 1
                             AND prior.Outcome = 1
                             AND prior.ReconciledByUserId <> i.ReconciledByUserId
                             AND prior.IsDeleted = 0
                       )
                   ))
                   OR EXISTS (
                       SELECT 1
                       FROM [dbo].[ProcurementGhanepsExchangeReconciliations] terminal
                       WHERE terminal.TenantId = i.TenantId
                         AND terminal.ExchangeEventId = i.ExchangeEventId
                          AND terminal.Id <> i.Id
                          AND terminal.Sequence < i.Sequence
                          AND terminal.Outcome IN (0, 2)
                         AND terminal.IsDeleted = 0
                   )
                   OR EXISTS (
                       SELECT 1
                       FROM [dbo].[ProcurementGhanepsExchangeEvents] e
                       WHERE e.Id = i.ExchangeEventId
                         AND e.TenantId = i.TenantId
                         AND e.PreparedByUserId = i.ReconciledByUserId
                   )
                   OR EXISTS (
                       SELECT 1
                       FROM [dbo].[ProcurementGhanepsExchangePayloads] p
                       WHERE p.ExchangeEventId = i.ExchangeEventId
                         AND p.TenantId = i.TenantId
                         AND p.RecordedByUserId = i.ReconciledByUserId
                   )
                   OR EXISTS (
                       SELECT 1
                       FROM [dbo].[ProcurementGhanepsExchangeAttempts] a
                       WHERE a.ExchangeEventId = i.ExchangeEventId
                         AND a.TenantId = i.TenantId
                         AND a.AttemptedByUserId = i.ReconciledByUserId
                   )
                   OR EXISTS (
                       SELECT 1
                       FROM [dbo].[ProcurementGhanepsExchangeAcknowledgements] ack
                       WHERE ack.ExchangeEventId = i.ExchangeEventId
                         AND ack.TenantId = i.TenantId
                         AND ack.AcknowledgedByUserId = i.ReconciledByUserId
                   )
                   OR EXISTS (
                       SELECT 1
                       FROM [dbo].[ProcurementGhanepsExchangeReconciliations] priorActor
                       WHERE priorActor.ExchangeEventId = i.ExchangeEventId
                         AND priorActor.TenantId = i.TenantId
                         AND priorActor.Id <> i.Id
                         AND priorActor.ReconciledByUserId = i.ReconciledByUserId
                         AND priorActor.IsDeleted = 0
                   )
            )
                THROW 51641, 'GHANEPS reconciliation authoritative lineage, outcome, sequence, terminal state, SOD, actor, or hash is invalid.', 1;
        END
        """;

    // TR_PaymentBatch_TDC0506InvoiceProcessorSod: preserve workflow, tenant, source, and audit predicates.
    private static readonly (string Before, string After)[] Replacements12 =
    [
        ("invoice.SubmittedById = batch.ApprovedById", "(invoice.SubmittedById = batch.ApprovedById AND dbo.ProcurementApSodRequired(batch.TenantId, 'PaymentBatch', batch.Id) = 1)"),
    ];
    private const string Trigger12 = """
        CREATE OR ALTER TRIGGER [dbo].[TR_PaymentBatch_TDC0506InvoiceProcessorSod]
        ON [dbo].[PaymentBatch]
        AFTER UPDATE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS (
                SELECT 1
                FROM inserted batch
                JOIN deleted priorBatch ON priorBatch.Id = batch.Id
                LEFT JOIN ProcurementControlEvents sodEvent
                  ON sodEvent.Id = batch.InvoicePaymentSodControlEventId
                 AND sodEvent.TenantId = batch.TenantId
                 AND sodEvent.EventType = 'ProcurementInvoicePaymentSod'
                 AND sodEvent.Action = 'ApprovePayment'
                 AND sodEvent.Result = 2
                 AND sodEvent.RuleCode = 'AP-004'
                 AND sodEvent.RuleVersion = 'TDC-0506'
                 AND sodEvent.SourceType = 'PaymentBatch'
                 AND sodEvent.SourceId = batch.Id
                 AND sodEvent.ActorUserId = batch.ApprovedById
                 AND sodEvent.IsDeleted = 0
                WHERE batch.IsDeleted = 0
                  AND batch.Status IN (3,4,5,6)
                  AND (batch.Status <> priorBatch.Status
                       OR ISNULL(batch.ApprovedById, '00000000-0000-0000-0000-000000000000') <> ISNULL(priorBatch.ApprovedById, '00000000-0000-0000-0000-000000000000')
                       OR ISNULL(batch.InvoicePaymentSodControlEventId, '00000000-0000-0000-0000-000000000000') <> ISNULL(priorBatch.InvoicePaymentSodControlEventId, '00000000-0000-0000-0000-000000000000'))
                  AND (
                       batch.ApprovedById IS NULL
                    OR sodEvent.Id IS NULL
                    OR EXISTS (
                        SELECT 1
                        FROM PaymentBatchInvoice selection
                        LEFT JOIN VendorInvoice invoice
                          ON invoice.Id = selection.VendorInvoiceId
                         AND invoice.TenantId = batch.TenantId
                         AND invoice.IsDeleted = 0
                        WHERE selection.PaymentBatchId = batch.Id
                          AND selection.TenantId = batch.TenantId
                          AND selection.IsDeleted = 0
                          AND (invoice.Id IS NULL OR invoice.SubmittedById IS NULL OR invoice.SubmittedById = batch.ApprovedById)
                    )
                  ))
                THROW 51641, 'AP_PAYMENT_BATCH_SOD_BLOCKED: the batch approver must be independent of every selected invoice processor and retain AP-004 evidence.', 1;
        END;
        """;

    // TR_VendorPayment_TDC0506InvoiceProcessorSod: preserve workflow, tenant, source, and audit predicates.
    private static readonly (string Before, string After)[] Replacements13 =
    [
        ("invoice.SubmittedById = payment.AuthorizedById", "(invoice.SubmittedById = payment.AuthorizedById AND dbo.ProcurementApSodRequired(payment.TenantId, CASE WHEN payment.PaymentBatchId IS NULL THEN 'VendorPayment' ELSE 'PaymentBatch' END, ISNULL(payment.PaymentBatchId, payment.Id)) = 1)"),
    ];
    private const string Trigger13 = """
        CREATE OR ALTER TRIGGER [dbo].[TR_VendorPayment_TDC0506InvoiceProcessorSod]
        ON [dbo].[VendorPayment]
        AFTER UPDATE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS (
                SELECT 1
                FROM inserted payment
                JOIN deleted priorPayment ON priorPayment.Id = payment.Id
                LEFT JOIN ProcurementControlEvents sodEvent
                  ON sodEvent.Id = payment.InvoicePaymentSodControlEventId
                 AND sodEvent.TenantId = payment.TenantId
                 AND sodEvent.EventType = 'ProcurementInvoicePaymentSod'
                 AND sodEvent.Action = 'ApprovePayment'
                 AND sodEvent.Result = 2
                 AND sodEvent.RuleCode = 'AP-004'
                 AND sodEvent.RuleVersion = 'TDC-0506'
                 AND sodEvent.SourceType = CASE WHEN payment.PaymentBatchId IS NULL THEN 'VendorPayment' ELSE 'PaymentBatch' END
                 AND sodEvent.SourceId = ISNULL(payment.PaymentBatchId, payment.Id)
                 AND sodEvent.ActorUserId = payment.AuthorizedById
                 AND sodEvent.IsDeleted = 0
                WHERE payment.IsDeleted = 0
                  AND (payment.Status IN (3,4,5,8) OR payment.JournalEntryId IS NOT NULL)
                  AND (payment.Status <> priorPayment.Status
                       OR ISNULL(payment.JournalEntryId, '00000000-0000-0000-0000-000000000000') <> ISNULL(priorPayment.JournalEntryId, '00000000-0000-0000-0000-000000000000')
                       OR ISNULL(payment.AuthorizedById, '00000000-0000-0000-0000-000000000000') <> ISNULL(priorPayment.AuthorizedById, '00000000-0000-0000-0000-000000000000')
                       OR ISNULL(payment.InvoicePaymentSodControlEventId, '00000000-0000-0000-0000-000000000000') <> ISNULL(priorPayment.InvoicePaymentSodControlEventId, '00000000-0000-0000-0000-000000000000'))
                  AND NOT (
                       priorPayment.JournalEntryId IS NOT NULL
                   AND priorPayment.InvoicePaymentSodControlEventId IS NULL
                   AND payment.JournalEntryId = priorPayment.JournalEntryId
                   AND payment.InvoicePaymentSodControlEventId IS NULL
                   AND ISNULL(payment.AuthorizedById, '00000000-0000-0000-0000-000000000000') = ISNULL(priorPayment.AuthorizedById, '00000000-0000-0000-0000-000000000000')
                  )
                  AND (
                       payment.AuthorizedById IS NULL
                    OR sodEvent.Id IS NULL
                    OR EXISTS (
                        SELECT 1
                        FROM VendorPaymentAllocation allocation
                        LEFT JOIN VendorInvoice invoice
                          ON invoice.Id = allocation.VendorInvoiceId
                         AND invoice.TenantId = payment.TenantId
                         AND invoice.IsDeleted = 0
                        WHERE allocation.VendorPaymentId = payment.Id
                          AND allocation.TenantId = payment.TenantId
                          AND allocation.IsDeleted = 0
                          AND allocation.IsReversal = 0
                              AND NOT EXISTS (
                                  SELECT 1
                                  FROM VendorPaymentAllocation reversal
                                  WHERE reversal.TenantId = allocation.TenantId
                                    AND reversal.VendorPaymentId = allocation.VendorPaymentId
                                    AND reversal.IsDeleted = 0
                                    AND reversal.IsReversal = 1
                                    AND reversal.OriginalAllocationId = allocation.Id
                              )
                          AND (invoice.Id IS NULL OR invoice.SubmittedById IS NULL OR invoice.SubmittedById = payment.AuthorizedById)
                    )
                    OR (payment.PaymentBatchId IS NOT NULL AND EXISTS (
                        SELECT 1
                        FROM PaymentBatchInvoice selection
                        LEFT JOIN VendorInvoice invoice
                          ON invoice.Id = selection.VendorInvoiceId
                         AND invoice.TenantId = payment.TenantId
                         AND invoice.IsDeleted = 0
                        WHERE selection.VendorPaymentId = payment.Id
                          AND selection.PaymentBatchId = payment.PaymentBatchId
                          AND selection.TenantId = payment.TenantId
                          AND selection.IsDeleted = 0
                          AND (invoice.Id IS NULL OR invoice.SubmittedById IS NULL OR invoice.SubmittedById = payment.AuthorizedById)
                    ))
                  ))
                THROW 51642, 'AP_PAYMENT_SOD_BLOCKED: the payment approver must be independent of every invoice processor and retain AP-004 evidence.', 1;
        END;
        """;

    // TR_VendorInvoiceMatchException_TDC0507Protected: preserve workflow, tenant, source, and audit predicates.
    private static readonly (string Before, string After)[] Replacements14 =
    [
        ("i.FinalApprovedById = i.RequestedById", "(i.FinalApprovedById = i.RequestedById AND dbo.ProcurementApSodRequired(i.TenantId, 'VendorInvoice', i.VendorInvoiceId) = 1)"),
        ("i.FinalApprovedById = invoice.SubmittedById", "(i.FinalApprovedById = invoice.SubmittedById AND dbo.ProcurementApSodRequired(i.TenantId, 'VendorInvoice', i.VendorInvoiceId) = 1)"),
        ("approval.ProcessedById IN (i.RequestedById, invoice.SubmittedById)", "(approval.ProcessedById IN (i.RequestedById, invoice.SubmittedById) AND dbo.ProcurementApSodRequired(i.TenantId, 'VendorInvoice', i.VendorInvoiceId) = 1)"),
        ("(SELECT COUNT(DISTINCT approval.ProcessedById)\n             FROM WorkflowApprovals approval\n             JOIN WorkflowStepInstances stepInstance ON stepInstance.Id = approval.StepInstanceId\n             WHERE stepInstance.WorkflowInstanceId = i.WorkflowInstanceId\n               AND approval.TenantId = i.TenantId AND approval.Status = 1\n               AND approval.ProcessedById IS NOT NULL AND approval.IsDeleted = 0) < 2", "((SELECT COUNT(DISTINCT approval.ProcessedById)\n             FROM WorkflowApprovals approval\n             JOIN WorkflowStepInstances stepInstance ON stepInstance.Id = approval.StepInstanceId\n             WHERE stepInstance.WorkflowInstanceId = i.WorkflowInstanceId\n               AND approval.TenantId = i.TenantId AND approval.Status = 1\n               AND approval.ProcessedById IS NOT NULL AND approval.IsDeleted = 0) < 2 AND dbo.ProcurementApSodRequired(i.TenantId, 'VendorInvoice', i.VendorInvoiceId) = 1)"),
    ];
    private const string Trigger14 = """
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
        """;
}
