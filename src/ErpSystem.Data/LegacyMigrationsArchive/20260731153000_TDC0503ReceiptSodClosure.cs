using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ErpSystem.Data.ApplicationDbContext))]
[Migration("20260731153000_TDC0503ReceiptSodClosure")]
public sealed class TDC0503ReceiptSodClosure : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
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
            """);

        migrationBuilder.Sql(
            """
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
            """);

        migrationBuilder.Sql(
            """
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
            """);

        migrationBuilder.Sql(
            """
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
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DROP TRIGGER IF EXISTS [dbo].[TR_StockMovements_TDC0503ReceiptSod];");
        migrationBuilder.Sql(
            "DROP TRIGGER IF EXISTS [dbo].[TR_InventoryMovements_TDC0503ReceiptSod];");
        migrationBuilder.Sql(
            "DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementReceiptInspectionActions_TDC0503SodHardStop];");
        migrationBuilder.Sql(
            "DROP TRIGGER IF EXISTS [dbo].[TR_GoodsReceiptNotes_TDC0503SodHardStop];");
    }
}
