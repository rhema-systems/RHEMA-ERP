using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260916160000_AllowDraftInventoryDisposalEdits")]
public sealed class AllowDraftInventoryDisposalEdits : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
CREATE OR ALTER TRIGGER [dbo].[TR_InventoryDisposalLines_Immutable]
ON [dbo].[InventoryDisposalLines]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    -- Lines are immutable after the draft stage. While a draft is still being
    -- edited, the service may retire an active line by setting IsDeleted.
    IF EXISTS (
        SELECT 1
        FROM deleted d
        LEFT JOIN inserted i ON i.Id = d.Id
        LEFT JOIN dbo.InventoryDisposalCases c ON c.Id = d.InventoryDisposalCaseId AND c.TenantId = d.TenantId
        WHERE i.Id IS NULL OR c.Id IS NULL OR c.Status <> 1
           OR d.IsDeleted <> 0 OR i.IsDeleted <> 1
           OR i.TenantId <> d.TenantId OR i.InventoryDisposalCaseId <> d.InventoryDisposalCaseId
           OR i.InventoryItemId <> d.InventoryItemId OR i.LocationId <> d.LocationId
           OR i.Quantity <> d.Quantity OR i.UnitCost <> d.UnitCost OR i.TotalValue <> d.TotalValue
           OR ISNULL(i.LotNumber, '') <> ISNULL(d.LotNumber, '')
           OR ISNULL(i.BatchNumber, '') <> ISNULL(d.BatchNumber, '')
           OR ISNULL(i.SerialNumber, '') <> ISNULL(d.SerialNumber, '')
           OR ISNULL(i.ConditionNotes, '') <> ISNULL(d.ConditionNotes, '')
           OR i.IntegrityHash <> d.IntegrityHash)
        THROW 51104, 'INV_DISPOSAL_LINE_IMMUTABLE', 1;
    IF EXISTS (
        SELECT 1 FROM inserted i
        LEFT JOIN dbo.InventoryDisposalCases c ON c.Id = i.InventoryDisposalCaseId AND c.TenantId = i.TenantId
        LEFT JOIN dbo.InventoryItems p ON p.Id = i.InventoryItemId AND p.TenantId = i.TenantId
        LEFT JOIN dbo.WarehouseLocations l ON l.Id = i.LocationId AND l.TenantId = i.TenantId
            AND ((l.IsConsignmentBin = 1 AND l.ConsignmentWarehouseId = c.WarehouseId)
                OR (l.IsConsignmentBin = 0 AND l.WarehouseId = c.WarehouseId))
        WHERE c.Id IS NULL OR c.Status <> 1 OR c.IsDeleted = 1 OR p.Id IS NULL OR l.Id IS NULL)
        THROW 51105, 'INV_DISPOSAL_LINE_TENANT_LOCATION_OR_DRAFT_INVALID', 1;
END;
""");

        migrationBuilder.Sql("""
CREATE OR ALTER TRIGGER [dbo].[TR_InventoryDisposalCases_Guard]
ON [dbo].[InventoryDisposalCases]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
        THROW 51109, 'INV_DISPOSAL_CASE_DELETE_PROHIBITED', 1;
    IF EXISTS (
        SELECT 1 FROM inserted i
        LEFT JOIN dbo.Warehouses w ON w.Id = i.WarehouseId AND w.TenantId = i.TenantId
        LEFT JOIN dbo.Users u ON u.Id = i.RequestedById AND u.TenantId = i.TenantId
        LEFT JOIN dbo.StockAdjustments a ON a.Id = i.StockAdjustmentId AND a.TenantId = i.TenantId
        WHERE w.Id IS NULL OR u.Id IS NULL OR (i.StockAdjustmentId IS NOT NULL AND a.Id IS NULL))
        THROW 51110, 'INV_DISPOSAL_CASE_TENANT_LINEAGE_INVALID', 1;
    IF EXISTS (
        SELECT 1 FROM inserted i
        LEFT JOIN deleted d ON d.Id = i.Id
        WHERE d.Id IS NULL AND (i.Status <> 1 OR i.ApprovalRequired <> 1 OR i.AuditVerifiedById IS NOT NULL OR i.WorkflowInstanceId IS NOT NULL
           OR i.ApprovedById IS NOT NULL OR i.StockAdjustmentId IS NOT NULL OR i.CompletedById IS NOT NULL))
        THROW 51111, 'INV_DISPOSAL_CASE_INITIAL_STATE_INVALID', 1;
    -- Only an identified draft may change its editable identification fields.
    -- Tenant, identity, warehouse, numbering, lineage and soft-delete fields
    -- remain immutable for the lifetime of the case.
    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
        WHERE i.TenantId <> d.TenantId OR i.DisposalNumber <> d.DisposalNumber OR i.WarehouseId <> d.WarehouseId
           OR i.RequestedById <> d.RequestedById OR i.RequestedAtUtc <> d.RequestedAtUtc
           OR i.IdempotencyKey <> d.IdempotencyKey OR i.PayloadHash <> d.PayloadHash OR i.CorrelationId <> d.CorrelationId
           OR i.IsDeleted <> d.IsDeleted
           OR ((d.Status <> 1 OR i.Status <> 1) AND (
                i.Method <> d.Method OR i.Reason <> d.Reason OR i.IdentificationDetails <> d.IdentificationDetails
                OR i.TotalQuantity <> d.TotalQuantity OR i.TotalValue <> d.TotalValue))
           OR (i.ApprovalRequired <> d.ApprovalRequired AND NOT (
                d.Status IN (1, 2, 3, 4) AND i.Status = 11
                AND d.ApprovalRequired = 1 AND i.ApprovalRequired = 0
                AND d.WorkflowInstanceId IS NULL AND d.ApprovedById IS NULL
                AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId, N'InventoryDisposal', i.Id) = 0)))
        THROW 51112, 'INV_DISPOSAL_CASE_CORE_IMMUTABLE', 1;
    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
        WHERE i.Status <> d.Status AND NOT (
               (d.Status = 1 AND i.Status IN (2, 5, 9, 10, 11))
            OR (d.Status = 2 AND i.Status IN (3, 5, 10, 11))
            OR (d.Status = 3 AND i.Status IN (4, 5, 9, 10, 11))
            OR (d.Status = 4 AND i.Status IN (5, 10, 11))
            OR (d.Status = 5 AND i.Status IN (6, 9, 10))
            OR (d.Status IN (6, 11) AND i.Status IN (7, 10))
            OR (d.Status = 7 AND i.Status = 8)))
        THROW 51113, 'INV_DISPOSAL_CASE_TRANSITION_INVALID', 1;
    IF EXISTS (SELECT 1 FROM inserted WHERE Status = 5 AND (WorkflowInstanceId IS NULL OR ApprovalRequired <> 1))
        THROW 51114, 'INV_DISPOSAL_WORKFLOW_LINEAGE_REQUIRED', 1;
    IF EXISTS (SELECT 1 FROM inserted WHERE
        (ApprovalRequired = 1 AND Status IN (6, 7, 8) AND (WorkflowInstanceId IS NULL OR ApprovedById IS NULL OR ApprovedAtUtc IS NULL))
        OR (Status = 11 AND ApprovalRequired <> 0)
        OR (ApprovalRequired = 0 AND (Status NOT IN (7, 8, 10, 11) OR WorkflowInstanceId IS NOT NULL OR ApprovedById IS NOT NULL OR ApprovedAtUtc IS NOT NULL)))
        THROW 51115, 'INV_DISPOSAL_APPROVAL_LINEAGE_REQUIRED', 1;
    IF EXISTS (SELECT 1 FROM inserted WHERE Status IN (7, 8) AND (StockAdjustmentId IS NULL OR ExecutionReference IS NULL))
        THROW 51116, 'INV_DISPOSAL_ADJUSTMENT_LINEAGE_REQUIRED', 1;
    IF EXISTS (
        SELECT 1 FROM inserted i LEFT JOIN dbo.StockAdjustments a ON a.Id = i.StockAdjustmentId AND a.TenantId = i.TenantId
        WHERE i.Status = 8 AND (
            i.CompletedById IS NULL OR i.CompletedAtUtc IS NULL OR a.Status <> 'Posted'
            OR (i.Method IN (1, 2) AND (i.ProceedsAmount <= 0 OR i.ProceedsAccountId IS NULL OR i.BuyerOrRecipient IS NULL
                OR i.ProceedsPostingEventId IS NULL OR i.ProceedsJournalEntryId IS NULL))
            OR (i.Method NOT IN (1, 2) AND i.ProceedsAmount <> 0)))
        THROW 51117, 'INV_DISPOSAL_COMPLETION_LINEAGE_INVALID', 1;
END;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("THROW 51997, 'Inventory disposal draft-edit guard history requires a reviewed forward migration; automatic downgrade is not supported.', 1;");
    }
}
