using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Persists the explicit Finance book selected by Inventory's governed opening-stock schedule.
/// Existing and ordinary stock adjustments retain their established IFRS default.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260820100000_AddInventoryOpeningStockBook")]
public sealed class AddInventoryOpeningStockBook : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BookClassification",
            table: "StockAdjustments",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "IFRS");

        migrationBuilder.Sql(AdjustmentLifecycleTrigger(governOpeningStock: true));
        migrationBuilder.Sql(AdjustmentLineTrigger(governOpeningStock: true));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Restore the pre-opening-stock trigger contracts before removing the column they no longer reference.
        migrationBuilder.Sql(AdjustmentLifecycleTrigger(governOpeningStock: false));
        migrationBuilder.Sql(AdjustmentLineTrigger(governOpeningStock: false));

        migrationBuilder.DropColumn(
            name: "BookClassification",
            table: "StockAdjustments");
    }

    private static string AdjustmentLifecycleTrigger(bool governOpeningStock) =>
        """
        CREATE OR ALTER TRIGGER [dbo].[TR_StockAdjustments_ControlledLifecycle]
        ON [dbo].[StockAdjustments]
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                THROW 51681, 'INV_ADJUSTMENT_DELETE_BLOCKED: use the controlled Draft soft-delete operation.', 1;

            /*OPENING_STOCK_CONVERSION_GUARD*/

            /*OPENING_STOCK_SOURCE_IMMUTABILITY*/

            IF EXISTS (
                SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id = i.Id
                WHERE d.Id IS NULL AND
                    (i.Status <> N'Draft' OR i.IsDeleted = 1 OR i.RequestedById = '00000000-0000-0000-0000-000000000000'
                     OR LEN(LTRIM(RTRIM(i.AdjustmentNumber))) = 0 OR LEN(LTRIM(RTRIM(i.ReasonCode))) = 0
                     OR LEN(LTRIM(RTRIM(i.Description))) = 0 OR LEN(LTRIM(RTRIM(i.IdempotencyKey))) = 0
                     OR LEN(i.PayloadHash) <> 64 OR LEN(i.IntegrityHash) <> 64 OR LEN(LTRIM(RTRIM(i.CorrelationId))) = 0))
                THROW 51682, 'INV_ADJUSTMENT_INITIAL_STATE_INVALID: adjustments must enter as attributable complete Drafts.', 1;

            /*OPENING_STOCK_INSERT_GUARD*/

            IF EXISTS (
                SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                WHERE i.IsDeleted = 1 AND (d.Status <> N'Draft' OR i.Status <> N'Draft'))
                THROW 51683, 'INV_ADJUSTMENT_DELETE_STATE: only a Draft adjustment may be soft deleted.', 1;

            IF EXISTS (
                SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                WHERE d.Status <> N'Draft' AND
                    (i.TenantId <> d.TenantId OR i.AdjustmentNumber <> d.AdjustmentNumber OR i.WarehouseId <> d.WarehouseId
                     OR i.Reference <> d.Reference OR i.AdjustmentDate <> d.AdjustmentDate OR i.ReasonCode <> d.ReasonCode
                     /*OPENING_STOCK_BOOK_IMMUTABLE*/
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
                 OR (d.Status = N'Approved' AND i.Status = N'Cancelled' AND EXISTS (
                     SELECT 1 FROM PhysicalCounts c
                     WHERE c.TenantId = i.TenantId AND c.StockAdjustmentId = i.Id AND c.IsDeleted = 0
                       AND c.Status IN (N'PendingFinanceApproval', N'PendingAuditAttestation')))
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
                      AND f.SourceDocumentId = i.Id /*OPENING_STOCK_POSTING_ACTION*/
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
        """
        .Replace(
            "/*OPENING_STOCK_CONVERSION_GUARD*/",
            governOpeningStock
                ? """
                  IF EXISTS (
                      SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                      WHERE d.ReasonCode <> N'INITIAL_STOCK' AND i.ReasonCode = N'INITIAL_STOCK')
                      THROW 51690, 'INV_OPENING_STOCK_CONVERSION_BLOCKED: ordinary Draft adjustments cannot become governed opening stock.', 1;
                  """
                : string.Empty,
            StringComparison.Ordinal)
        .Replace(
            "/*OPENING_STOCK_BOOK_IMMUTABLE*/",
            governOpeningStock
                ? "OR ISNULL(i.BookClassification, N'') <> ISNULL(d.BookClassification, N'')"
                : string.Empty,
            StringComparison.Ordinal)
        .Replace(
            "/*OPENING_STOCK_INSERT_GUARD*/",
            governOpeningStock
                ? """
                  IF EXISTS (
                      SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id = i.Id
                      WHERE d.Id IS NULL AND i.ReasonCode = N'INITIAL_STOCK' AND
                          (LEN(LTRIM(RTRIM(ISNULL(i.BookClassification, N'')))) = 0
                           OR UPPER(LTRIM(RTRIM(i.BookClassification))) = N'ALL_ACTIVE_BOOKS'
                           OR LEN(LTRIM(RTRIM(ISNULL(i.Reference, N'')))) = 0
                           OR LEN(LTRIM(RTRIM(ISNULL(i.Description, N'')))) = 0
                           OR ISNULL(i.IdempotencyKey, N'') NOT LIKE N'OPENING-STOCK:%'
                           OR LEN(ISNULL(i.PayloadHash, N'')) <> 64 OR LEN(ISNULL(i.IntegrityHash, N'')) <> 64
                           OR ISNULL(i.CorrelationId, N'') NOT LIKE N'opening-stock:%'))
                      THROW 51695, 'INV_OPENING_STOCK_INSERT_INVALID: opening stock requires an explicit book, source schedule and dedicated immutable identity.', 1;
                  """
                : string.Empty,
            StringComparison.Ordinal)
        .Replace(
            "/*OPENING_STOCK_SOURCE_IMMUTABILITY*/",
            governOpeningStock
                ? """
                  IF EXISTS (
                      SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                      WHERE d.ReasonCode = N'INITIAL_STOCK' AND
                          (i.TenantId <> d.TenantId OR i.AdjustmentNumber <> d.AdjustmentNumber OR i.WarehouseId <> d.WarehouseId
                           OR i.Reference <> d.Reference OR i.AdjustmentDate <> d.AdjustmentDate OR i.ReasonCode <> d.ReasonCode
                           OR ISNULL(i.BookClassification, N'') <> ISNULL(d.BookClassification, N'')
                           OR ISNULL(i.Description, N'') <> ISNULL(d.Description, N'') OR i.TotalAdjustmentValue <> d.TotalAdjustmentValue
                           OR i.RequestedById <> d.RequestedById
                           OR ISNULL(i.RelatedIssueVoucherId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.RelatedIssueVoucherId, '00000000-0000-0000-0000-000000000000')
                           OR ISNULL(i.IdempotencyKey, N'') <> ISNULL(d.IdempotencyKey, N'')
                           OR ISNULL(i.PayloadHash, N'') <> ISNULL(d.PayloadHash, N'') OR ISNULL(i.CorrelationId, N'') <> ISNULL(d.CorrelationId, N'')
                           OR i.IsDeleted <> d.IsDeleted))
                      THROW 51694, 'INV_OPENING_STOCK_SOURCE_IMMUTABLE: governed opening-stock source evidence cannot change after creation.', 1;
                  """
                : string.Empty,
            StringComparison.Ordinal)
        .Replace(
            "/*OPENING_STOCK_POSTING_ACTION*/",
            governOpeningStock
                ? """
                  AND ((i.ReasonCode = N'INITIAL_STOCK' AND f.PostingAction = N'PostOpeningStock')
                       OR (i.ReasonCode <> N'INITIAL_STOCK' AND f.PostingAction = N'PostStockAdjustment'))
                  """
                : "AND f.PostingAction = N'PostStockAdjustment'",
            StringComparison.Ordinal);

    private static string AdjustmentLineTrigger(bool governOpeningStock) =>
        """
        CREATE OR ALTER TRIGGER [dbo].[TR_StockAdjustmentItems_ControlledMutation]
        ON [dbo].[StockAdjustmentItems]
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;

            /*OPENING_STOCK_LINE_IMMUTABILITY*/

            IF EXISTS (
                SELECT 1 FROM deleted d
                LEFT JOIN StockAdjustments a ON a.Id = d.AdjustmentId AND a.TenantId = d.TenantId
                WHERE a.Id IS NULL OR a.Status <> N'Draft')
                THROW 51691, 'INV_ADJUSTMENT_LINE_IMMUTABLE: only Draft adjustment lines can be changed or deleted.', 1;

            IF EXISTS (
                SELECT 1 FROM inserted i
                LEFT JOIN StockAdjustments a ON a.Id = i.AdjustmentId AND a.TenantId = i.TenantId AND a.IsDeleted = 0
                LEFT JOIN InventoryItems item ON item.Id = i.InventoryItemId AND item.TenantId = i.TenantId AND item.IsDeleted = 0
                LEFT JOIN WarehouseLocations l ON l.Id = i.LocationId AND l.TenantId = i.TenantId AND l.IsDeleted = 0 AND l.IsActive = 1
                /*OPENING_STOCK_WAREHOUSE_JOIN*/
                WHERE a.Id IS NULL OR a.Status <> N'Draft' OR item.Id IS NULL OR i.LocationId IS NULL
                   OR l.Id IS NULL OR l.WarehouseId <> a.WarehouseId OR i.AdjustmentQuantity = 0
                   OR i.PhysicalQuantity <> i.SystemQuantity + i.AdjustmentQuantity
                   OR i.UnitCost <= 0 /*OPENING_STOCK_COST_RULE*/
                   OR i.AdjustmentValue <> ROUND(i.AdjustmentQuantity * i.UnitCost, 2))
                THROW 51692, 'INV_ADJUSTMENT_LINE_INVALID: lines require a non-zero server-valued delta at an exact active location.', 1;
        END
        """
        .Replace(
            "/*OPENING_STOCK_LINE_IMMUTABILITY*/",
            governOpeningStock
                ? """
                  IF EXISTS (
                      SELECT 1 FROM deleted d
                      LEFT JOIN inserted i ON i.Id = d.Id
                      LEFT JOIN StockAdjustments sourceAdjustment
                        ON sourceAdjustment.Id = d.AdjustmentId AND sourceAdjustment.TenantId = d.TenantId
                      LEFT JOIN StockAdjustments targetAdjustment
                        ON targetAdjustment.Id = i.AdjustmentId AND targetAdjustment.TenantId = i.TenantId
                      WHERE sourceAdjustment.ReasonCode = N'INITIAL_STOCK'
                         OR targetAdjustment.ReasonCode = N'INITIAL_STOCK')
                      THROW 51693, 'INV_OPENING_STOCK_LINE_IMMUTABLE: governed opening-stock evidence cannot be changed or deleted after insert.', 1;
                  """
                : string.Empty,
            StringComparison.Ordinal)
        .Replace(
            "/*OPENING_STOCK_WAREHOUSE_JOIN*/",
            governOpeningStock
                ? "LEFT JOIN Warehouses w ON w.Id = a.WarehouseId AND w.TenantId = i.TenantId AND w.IsDeleted = 0 AND w.IsActive = 1"
                : string.Empty,
            StringComparison.Ordinal)
        .Replace(
            "/*OPENING_STOCK_COST_RULE*/",
            governOpeningStock
                ? """
                  OR (a.ReasonCode = N'INITIAL_STOCK' AND i.AdjustmentQuantity <= 0)
                     OR (a.ReasonCode = N'INITIAL_STOCK' AND
                         (i.SystemQuantity <> 0 OR i.PhysicalQuantity <> i.AdjustmentQuantity
                          OR w.Id IS NULL OR w.IsConsignmentWarehouse = 1 OR l.IsConsignmentBin = 1))
                     OR (a.ReasonCode <> N'INITIAL_STOCK'
                         AND i.UnitCost <> CASE WHEN item.AverageCost > 0 THEN item.AverageCost
                              WHEN item.StandardCost > 0 THEN item.StandardCost ELSE item.LastPurchaseCost END)
                  """
                : """
                  OR i.UnitCost <> CASE WHEN item.AverageCost > 0 THEN item.AverageCost
                       WHEN item.StandardCost > 0 THEN item.StandardCost ELSE item.LastPurchaseCost END
                  """,
            StringComparison.Ordinal);
}
