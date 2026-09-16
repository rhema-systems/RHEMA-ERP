using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260906230000_AlignStoreReturnWithOriginalIssueValuation")]
public sealed class AlignStoreReturnWithOriginalIssueValuation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(Trigger(true));
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(Trigger(false));

    private static string Trigger(bool originalIssue) => $$"""
        CREATE OR ALTER TRIGGER [dbo].[TR_InventoryReturnVoucherLines_AppendOnly]
        ON [dbo].[InventoryReturnVoucherLines]
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM deleted)
                THROW 51651, 'INV_RETURN_LINE_IMMUTABLE: return voucher lines cannot be changed or deleted.', 1;
            IF EXISTS (
                SELECT 1 FROM inserted i
                LEFT JOIN InventoryReturnVouchers v ON v.Id = i.InventoryReturnVoucherId AND v.TenantId = i.TenantId AND v.IsDeleted = 0
                LEFT JOIN InventoryRequisitionItems r ON r.Id = i.InventoryRequisitionItemId AND r.TenantId = i.TenantId AND r.IsDeleted = 0
                LEFT JOIN InventoryItems item ON item.Id = i.InventoryItemId AND item.TenantId = i.TenantId AND item.IsDeleted = 0
                LEFT JOIN WarehouseLocations l ON l.Id = i.LocationId AND l.TenantId = i.TenantId AND l.IsDeleted = 0 AND l.IsActive = 1
                OUTER APPLY (
                    SELECT COALESCE(SUM(line.Quantity), 0) Quantity
                    FROM InventoryReturnVoucherLines line
                    INNER JOIN InventoryReturnVouchers existingVoucher ON existingVoucher.Id = line.InventoryReturnVoucherId AND existingVoucher.TenantId = line.TenantId
                    WHERE line.TenantId = i.TenantId AND line.InventoryRequisitionItemId = i.InventoryRequisitionItemId
                      AND line.IsDeleted = 0 AND existingVoucher.IsDeleted = 0 AND existingVoucher.Status IN ({{(originalIssue ? "1,2" : "1,2,4")}})) reserved
                {{(originalIssue ? IssueValue : "")}}
                WHERE v.Id IS NULL OR v.Status <> 1 OR r.Id IS NULL OR item.Id IS NULL
                   OR r.InventoryRequisitionId <> v.InventoryRequisitionId OR r.InventoryItemId <> i.InventoryItemId
                   OR i.LocationId IS NULL OR l.Id IS NULL OR l.WarehouseId <> v.WarehouseId
                   OR i.Quantity <= 0 OR i.UnitCost <= 0
                   {{(originalIssue ? "OR cost.Quantity < i.Quantity OR cost.TotalValue IS NULL OR i.TotalValue <> cost.TotalValue OR i.UnitCost <> ROUND(cost.TotalValue / NULLIF(i.Quantity, 0), 4)" : "OR i.UnitCost <> r.UnitCost OR i.TotalValue <> ROUND(i.Quantity * i.UnitCost, 2)")}}
                   OR LEN(i.IntegrityHash) <> 64 OR reserved.Quantity > r.IssuedQuantity)
                THROW 51652, 'INV_RETURN_LINE_SOURCE_INVALID: lines must match original issued quantity, value, item and exact active location.', 1;
        END
        """;

    // Same deterministic allocation order as the existing Finance return bridge.
    // Posted returns already reduce both requisition.IssuedQuantity and lineage availability;
    // only pending/approved returns reserve additional quantity.
    private const string IssueValue = """
        OUTER APPLY (
            SELECT SUM(issued.Available) Quantity,
                   SUM(ROUND(allocated.Quantity * issued.IssuedValue / NULLIF(issued.IssuedQuantity, 0), 2)) TotalValue
            FROM (
                SELECT f.IssuedQuantity, f.IssuedValue, f.IssuedQuantity - f.ReturnedQuantity Available,
                       COALESCE(SUM(f.IssuedQuantity - f.ReturnedQuantity) OVER (
                           ORDER BY iv.IssuedAtUtc, f.CreatedAt, f.Id ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING), 0) BeforeQuantity
                FROM InventoryIssueFinanceLineages f
                INNER JOIN InventoryIssueVoucherLines il ON il.Id = f.InventoryIssueVoucherLineId AND il.TenantId = f.TenantId AND il.IsDeleted = 0
                INNER JOIN InventoryIssueVouchers iv ON iv.Id = il.InventoryIssueVoucherId AND iv.TenantId = f.TenantId AND iv.IsDeleted = 0
                WHERE f.TenantId = i.TenantId AND f.IsDeleted = 0 AND f.ReturnedQuantity < f.IssuedQuantity
                  AND iv.InventoryRequisitionId = v.InventoryRequisitionId AND il.InventoryRequisitionItemId = i.InventoryRequisitionItemId
                  AND (UPPER(LTRIM(RTRIM(il.SerialNumber))) = UPPER(LTRIM(RTRIM(i.SerialNumber))) OR (il.SerialNumber IS NULL AND i.SerialNumber IS NULL))
                  AND (UPPER(LTRIM(RTRIM(il.LotNumber))) = UPPER(LTRIM(RTRIM(i.LotNumber))) OR (il.LotNumber IS NULL AND i.LotNumber IS NULL))
                  AND (UPPER(LTRIM(RTRIM(il.BatchNumber))) = UPPER(LTRIM(RTRIM(i.BatchNumber))) OR (il.BatchNumber IS NULL AND i.BatchNumber IS NULL))
            ) issued
            CROSS APPLY (SELECT CASE WHEN i.Quantity <= issued.BeforeQuantity THEN 0
                              WHEN i.Quantity - issued.BeforeQuantity >= issued.Available THEN issued.Available
                              ELSE i.Quantity - issued.BeforeQuantity END Quantity) allocated
        ) cost
        """;
}
