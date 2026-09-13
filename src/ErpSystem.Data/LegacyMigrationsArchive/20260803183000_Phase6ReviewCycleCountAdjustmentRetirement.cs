using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260803183000_Phase6ReviewCycleCountAdjustmentRetirement")]
public partial class Phase6ReviewCycleCountAdjustmentRetirement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(LifecycleTrigger(allowControlledRetirement: true));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(LifecycleTrigger(allowControlledRetirement: false));

    private static string LifecycleTrigger(bool allowControlledRetirement) =>
        """
        CREATE OR ALTER TRIGGER [dbo].[TR_StockAdjustments_ControlledLifecycle]
        ON [dbo].[StockAdjustments]
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                THROW 51681, 'INV_ADJUSTMENT_DELETE_BLOCKED: use the controlled Draft soft-delete operation.', 1;

            IF EXISTS (
                SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id = i.Id
                WHERE d.Id IS NULL AND
                    (i.Status <> N'Draft' OR i.IsDeleted = 1 OR i.RequestedById = '00000000-0000-0000-0000-000000000000'
                     OR LEN(LTRIM(RTRIM(i.AdjustmentNumber))) = 0 OR LEN(LTRIM(RTRIM(i.ReasonCode))) = 0
                     OR LEN(LTRIM(RTRIM(i.Description))) = 0 OR LEN(LTRIM(RTRIM(i.IdempotencyKey))) = 0
                     OR LEN(i.PayloadHash) <> 64 OR LEN(i.IntegrityHash) <> 64 OR LEN(LTRIM(RTRIM(i.CorrelationId))) = 0))
                THROW 51682, 'INV_ADJUSTMENT_INITIAL_STATE_INVALID: adjustments must enter as attributable complete Drafts.', 1;

            IF EXISTS (
                SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                WHERE i.IsDeleted = 1 AND (d.Status <> N'Draft' OR i.Status <> N'Draft'))
                THROW 51683, 'INV_ADJUSTMENT_DELETE_STATE: only a Draft adjustment may be soft deleted.', 1;

            IF EXISTS (
                SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                WHERE d.Status <> N'Draft' AND
                    (i.TenantId <> d.TenantId OR i.AdjustmentNumber <> d.AdjustmentNumber OR i.WarehouseId <> d.WarehouseId
                     OR i.Reference <> d.Reference OR i.AdjustmentDate <> d.AdjustmentDate OR i.ReasonCode <> d.ReasonCode
                     OR ISNULL(i.Description, N'') <> ISNULL(d.Description, N'') OR i.TotalAdjustmentValue <> d.TotalAdjustmentValue
                     OR i.RequestedById <> d.RequestedById
                     OR ISNULL(i.RelatedIssueVoucherId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.RelatedIssueVoucherId, '00000000-0000-0000-0000-000000000000')
                     OR ISNULL(i.IdempotencyKey, N'') <> ISNULL(d.IdempotencyKey, N'')
                     OR ISNULL(i.PayloadHash, N'') <> ISNULL(d.PayloadHash, N'') OR ISNULL(i.CorrelationId, N'') <> ISNULL(d.CorrelationId, N'')
                     OR i.IsDeleted <> d.IsDeleted))
                THROW 51684, 'INV_ADJUSTMENT_SOURCE_IMMUTABLE: submitted adjustment source and value cannot change.', 1;

            IF EXISTS (
                SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                WHERE i.Status <> d.Status AND NOT (
                    (d.Status = N'Draft' AND i.Status = N'PendingApproval'
                     AND i.WorkflowInstanceId IS NOT NULL AND i.SubmittedById IS NOT NULL AND i.SubmittedAtUtc IS NOT NULL)
                 OR (d.Status = N'Draft' AND i.Status = N'Cancelled')
                 OR (d.Status = N'PendingApproval' AND i.Status = N'Approved'
                     AND i.ApprovedById IS NOT NULL AND i.ApprovedAt IS NOT NULL AND i.ApprovedById <> i.RequestedById)
                 OR (d.Status = N'PendingApproval' AND i.Status = N'Rejected'
                     AND i.RejectedById IS NOT NULL AND i.RejectedAtUtc IS NOT NULL
                     AND LEN(LTRIM(RTRIM(i.RejectionReason))) > 0 AND i.RejectedById <> i.RequestedById)
                 /*CONTROLLED_RECOUNT_RETIREMENT*/
                 OR (d.Status = N'Approved' AND i.Status = N'Posted'
                     AND i.PostedById IS NOT NULL AND i.PostedAtUtc IS NOT NULL AND i.PostedById <> i.RequestedById
                     AND i.FinancePostingEventId IS NOT NULL AND i.FinanceJournalEntryId IS NOT NULL)
                 OR (d.Status = N'Posted' AND i.Status = N'Reversed'
                     AND i.ReversedById IS NOT NULL AND i.ReversedAtUtc IS NOT NULL AND i.ReversedById <> i.RequestedById
                     AND LEN(LTRIM(RTRIM(i.ReversalReason))) > 0
                     AND i.ReversalFinancePostingEventId IS NOT NULL AND i.ReversalFinanceJournalEntryId IS NOT NULL)))
                THROW 51685, 'INV_ADJUSTMENT_TRANSITION_INVALID: submit, independent decision, controlled recount retirement, finance-backed post and reversal are required.', 1;

            IF EXISTS (
                SELECT 1 FROM inserted i
                LEFT JOIN Warehouses w ON w.Id = i.WarehouseId AND w.TenantId = i.TenantId AND w.IsDeleted = 0 AND w.IsActive = 1
                WHERE w.Id IS NULL OR i.Status NOT IN (N'Draft', N'PendingApproval', N'Approved', N'Rejected', N'Posted', N'Reversed', N'Cancelled'))
                THROW 51686, 'INV_ADJUSTMENT_SCOPE_INVALID: adjustment requires an active tenant warehouse and controlled status.', 1;

            IF EXISTS (
                SELECT 1 FROM inserted i CROSS APPLY (VALUES
                    (i.RequestedById), (i.SubmittedById), (i.ApprovedById), (i.RejectedById), (i.PostedById), (i.ReversedById)) actor(UserId)
                WHERE actor.UserId IS NOT NULL AND NOT EXISTS (
                    SELECT 1 FROM UserTenants ut INNER JOIN Users u ON u.Id = ut.UserId AND u.IsActive = 1
                    WHERE ut.TenantId = i.TenantId AND ut.UserId = actor.UserId AND ut.IsDeleted = 0
                      AND ut.Status = 0 AND (ut.ExpiresAt IS NULL OR ut.ExpiresAt > SYSUTCDATETIME())))
                THROW 51687, 'INV_ADJUSTMENT_ACTOR_INVALID: every adjustment actor must be active in the same tenant.', 1;

            IF EXISTS (
                SELECT 1 FROM inserted i
                WHERE i.Status = N'Posted' AND NOT EXISTS (
                    SELECT 1 FROM FinancePostingEvents f
                    WHERE f.Id = i.FinancePostingEventId AND f.TenantId = i.TenantId AND f.IsDeleted = 0
                      AND f.SourceModule = N'Inventory' AND f.SourceDocumentType = N'StockAdjustment'
                      AND f.SourceDocumentId = i.Id AND f.PostingAction = N'PostStockAdjustment'
                      AND f.PostingStatus = N'Posted' AND f.JournalEntryId = i.FinanceJournalEntryId))
                THROW 51688, 'INV_ADJUSTMENT_FINANCE_LINEAGE: posted adjustment must reference its successful Finance posting and journal.', 1;

            IF EXISTS (
                SELECT 1 FROM inserted i
                WHERE i.Status = N'Reversed' AND NOT EXISTS (
                    SELECT 1 FROM FinancePostingEvents f
                    WHERE f.Id = i.ReversalFinancePostingEventId AND f.TenantId = i.TenantId AND f.IsDeleted = 0
                      AND f.SourceModule = N'Inventory' AND f.SourceDocumentType = N'StockAdjustment'
                      AND f.SourceDocumentId = i.Id AND f.PostingAction = N'ReverseStockAdjustment'
                      AND f.PostingStatus = N'Posted' AND f.JournalEntryId = i.ReversalFinanceJournalEntryId))
                THROW 51689, 'INV_ADJUSTMENT_REVERSAL_FINANCE_LINEAGE: reversal must reference its successful Finance posting and journal.', 1;
        END
        """.Replace(
            "/*CONTROLLED_RECOUNT_RETIREMENT*/",
            allowControlledRetirement
                ? """
                  OR (d.Status = N'Approved' AND i.Status = N'Cancelled' AND EXISTS (
                      SELECT 1 FROM PhysicalCounts c
                      WHERE c.TenantId = i.TenantId AND c.StockAdjustmentId = i.Id AND c.IsDeleted = 0
                        AND c.Status IN (N'PendingFinanceApproval', N'PendingAuditAttestation')))
                  """
                : string.Empty,
            StringComparison.Ordinal);
}
