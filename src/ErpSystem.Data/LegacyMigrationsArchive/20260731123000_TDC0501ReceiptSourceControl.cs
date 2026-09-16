using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260731123000_TDC0501ReceiptSourceControl")]
public sealed class TDC0501ReceiptSourceControl : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        AddReceiptColumns(migrationBuilder);
        AddReceiptIndexes(migrationBuilder);
        AddReceiptTriggers(migrationBuilder);
        AddInventoryPostingTriggers(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS [dbo].[TR_InventoryMovements_GovernedPurchaseReceipt];
            DROP TRIGGER IF EXISTS [dbo].[TR_StockMovements_GovernedPurchaseReceipt];
            DROP TRIGGER IF EXISTS [dbo].[TR_GoodsReceiptNoteItems_GovernedCapacity];
            DROP TRIGGER IF EXISTS [dbo].[TR_GoodsReceiptNotes_GovernedSource];
            DROP TRIGGER IF EXISTS [dbo].[TR_PurchaseOrderReceiptItems_GovernedCapacity];
            DROP TRIGGER IF EXISTS [dbo].[TR_PurchaseOrderReceipts_GovernedSource];
            """);

        migrationBuilder.DropIndex(
            name: "IX_GoodsReceiptNotes_TenantId_PurchaseOrderReceiptId",
            table: "GoodsReceiptNotes");
        migrationBuilder.DropIndex(
            name: "IX_GoodsReceiptNotes_TenantId_PurchaseOrderId_IdempotencyKey",
            table: "GoodsReceiptNotes");
        migrationBuilder.DropIndex(
            name: "IX_GoodsReceiptNoteItems_TenantId_PurchaseOrderItemId_GoodsReceiptNoteId",
            table: "GoodsReceiptNoteItems");
        migrationBuilder.DropIndex(
            name: "IX_PurchaseOrderReceipts_TenantId_PurchaseOrderId_IdempotencyKey",
            table: "PurchaseOrderReceipts");
        migrationBuilder.DropIndex(
            name: "IX_PurchaseOrderReceiptItems_TenantId_PurchaseOrderItemId_ReceiptId",
            table: "PurchaseOrderReceiptItems");

        migrationBuilder.DropColumn(name: "CorrelationId", table: "GoodsReceiptNotes");
        migrationBuilder.DropColumn(name: "IdempotencyKey", table: "GoodsReceiptNotes");
        migrationBuilder.DropColumn(name: "ReceiptSourceIntegrityHash", table: "GoodsReceiptNotes");
        migrationBuilder.DropColumn(name: "ReceiptSourceSnapshotJson", table: "GoodsReceiptNotes");
        migrationBuilder.DropColumn(name: "ReceiptSourceValidatedAtUtc", table: "GoodsReceiptNotes");
        migrationBuilder.DropColumn(name: "ReceiptTolerancePercent", table: "GoodsReceiptNotes");
        migrationBuilder.DropColumn(name: "RowVersion", table: "GoodsReceiptNotes");

        migrationBuilder.DropColumn(name: "MaximumReceivableQuantitySnapshot", table: "GoodsReceiptNoteItems");
        migrationBuilder.DropColumn(name: "PreviouslyReceiptedQuantitySnapshot", table: "GoodsReceiptNoteItems");
        migrationBuilder.DropColumn(name: "ReceiptLineIntegrityHash", table: "GoodsReceiptNoteItems");
        migrationBuilder.DropColumn(name: "RemainingQuantityBeforeReceiptSnapshot", table: "GoodsReceiptNoteItems");
        migrationBuilder.DropColumn(name: "ToleranceQuantitySnapshot", table: "GoodsReceiptNoteItems");

        migrationBuilder.DropColumn(name: "CorrelationId", table: "PurchaseOrderReceipts");
        migrationBuilder.DropColumn(name: "IdempotencyKey", table: "PurchaseOrderReceipts");
        migrationBuilder.DropColumn(name: "ReceiptSourceIntegrityHash", table: "PurchaseOrderReceipts");
        migrationBuilder.DropColumn(name: "ReceiptSourceSnapshotJson", table: "PurchaseOrderReceipts");
        migrationBuilder.DropColumn(name: "ReceiptSourceValidatedAtUtc", table: "PurchaseOrderReceipts");
        migrationBuilder.DropColumn(name: "ReceiptTolerancePercent", table: "PurchaseOrderReceipts");
        migrationBuilder.DropColumn(name: "RowVersion", table: "PurchaseOrderReceipts");

        migrationBuilder.DropColumn(name: "MaximumReceivableQuantitySnapshot", table: "PurchaseOrderReceiptItems");
        migrationBuilder.DropColumn(name: "OrderedQuantitySnapshot", table: "PurchaseOrderReceiptItems");
        migrationBuilder.DropColumn(name: "PreviouslyReceiptedQuantitySnapshot", table: "PurchaseOrderReceiptItems");
        migrationBuilder.DropColumn(name: "ReceiptLineIntegrityHash", table: "PurchaseOrderReceiptItems");
        migrationBuilder.DropColumn(name: "RemainingQuantityBeforeReceiptSnapshot", table: "PurchaseOrderReceiptItems");
        migrationBuilder.DropColumn(name: "ToleranceQuantitySnapshot", table: "PurchaseOrderReceiptItems");
    }

    private static void AddReceiptColumns(MigrationBuilder migrationBuilder)
    {
        AddHeaderColumns(migrationBuilder, "PurchaseOrderReceipts");
        AddHeaderColumns(migrationBuilder, "GoodsReceiptNotes");

        migrationBuilder.AddColumn<decimal>(
            name: "OrderedQuantitySnapshot",
            table: "PurchaseOrderReceiptItems",
            type: "decimal(18,4)",
            nullable: false,
            defaultValue: 0m);
        AddLineColumns(migrationBuilder, "PurchaseOrderReceiptItems");
        AddLineColumns(migrationBuilder, "GoodsReceiptNoteItems");
    }

    private static void AddHeaderColumns(
        MigrationBuilder migrationBuilder,
        string table)
    {
        migrationBuilder.AddColumn<string>(
            name: "CorrelationId",
            table: table,
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "IdempotencyKey",
            table: table,
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "ReceiptSourceIntegrityHash",
            table: table,
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "ReceiptSourceSnapshotJson",
            table: table,
            type: "nvarchar(max)",
            nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "ReceiptSourceValidatedAtUtc",
            table: table,
            type: "datetime2",
            nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "ReceiptTolerancePercent",
            table: table,
            type: "decimal(5,2)",
            nullable: false,
            defaultValue: 0m);
        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            table: table,
            type: "rowversion",
            rowVersion: true,
            nullable: false);
    }

    private static void AddLineColumns(
        MigrationBuilder migrationBuilder,
        string table)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "MaximumReceivableQuantitySnapshot",
            table: table,
            type: "decimal(18,4)",
            nullable: false,
            defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>(
            name: "PreviouslyReceiptedQuantitySnapshot",
            table: table,
            type: "decimal(18,4)",
            nullable: false,
            defaultValue: 0m);
        migrationBuilder.AddColumn<string>(
            name: "ReceiptLineIntegrityHash",
            table: table,
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "RemainingQuantityBeforeReceiptSnapshot",
            table: table,
            type: "decimal(18,4)",
            nullable: false,
            defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>(
            name: "ToleranceQuantitySnapshot",
            table: table,
            type: "decimal(18,4)",
            nullable: false,
            defaultValue: 0m);
    }

    private static void AddReceiptIndexes(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_PurchaseOrderReceipts_TenantId_PurchaseOrderId_IdempotencyKey",
            table: "PurchaseOrderReceipts",
            columns: new[] { "TenantId", "PurchaseOrderId", "IdempotencyKey" },
            unique: true,
            filter: "[IdempotencyKey] IS NOT NULL AND [IsDeleted] = 0");
        migrationBuilder.CreateIndex(
            name: "IX_PurchaseOrderReceiptItems_TenantId_PurchaseOrderItemId_ReceiptId",
            table: "PurchaseOrderReceiptItems",
            columns: new[] { "TenantId", "PurchaseOrderItemId", "ReceiptId" },
            unique: true,
            filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex(
            name: "IX_GoodsReceiptNotes_TenantId_PurchaseOrderId_IdempotencyKey",
            table: "GoodsReceiptNotes",
            columns: new[] { "TenantId", "PurchaseOrderId", "IdempotencyKey" },
            unique: true,
            filter: "[IdempotencyKey] IS NOT NULL AND [IsDeleted] = 0");
        migrationBuilder.CreateIndex(
            name: "IX_GoodsReceiptNotes_TenantId_PurchaseOrderReceiptId",
            table: "GoodsReceiptNotes",
            columns: new[] { "TenantId", "PurchaseOrderReceiptId" },
            unique: true,
            filter: "[PurchaseOrderReceiptId] IS NOT NULL AND [IsDeleted] = 0");
        migrationBuilder.CreateIndex(
            name: "IX_GoodsReceiptNoteItems_TenantId_PurchaseOrderItemId_GoodsReceiptNoteId",
            table: "GoodsReceiptNoteItems",
            columns: new[] { "TenantId", "PurchaseOrderItemId", "GoodsReceiptNoteId" },
            unique: true,
            filter: "[PurchaseOrderItemId] IS NOT NULL AND [IsDeleted] = 0");
    }

    private static void AddReceiptTriggers(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_PurchaseOrderReceipts_GovernedSource]
            ON [dbo].[PurchaseOrderReceipts]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    INNER JOIN deleted d ON d.Id = i.Id
                    WHERE i.TenantId <> d.TenantId
                       OR i.PurchaseOrderId <> d.PurchaseOrderId
                       OR ISNULL(i.IdempotencyKey, N'') <> ISNULL(d.IdempotencyKey, N''))
                    THROW 51101, 'RCV_SOURCE_IMMUTABLE: receipt tenant, purchase-order source, and idempotency key are immutable.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    INNER JOIN PurchaseOrders po WITH (UPDLOCK, HOLDLOCK)
                        ON po.Id = i.PurchaseOrderId
                       AND po.TenantId = i.TenantId
                       AND po.IsDeleted = 0
                    LEFT JOIN deleted d ON d.Id = i.Id
                    WHERE i.IsDeleted = 0
                      AND (
                          (d.Id IS NULL AND po.Status NOT IN (N'Approved', N'Partially Received'))
                          OR (d.Id IS NOT NULL AND po.Status NOT IN (N'Approved', N'Partially Received', N'Received'))
                          OR po.ProcurementSourceType IS NULL
                          OR po.ProcurementSourceType NOT BETWEEN 0 AND 4
                          OR po.ProcurementSourceId IS NULL
                          OR NULLIF(LTRIM(RTRIM(po.ProcurementSourceReference)), N'') IS NULL
                          OR ISNULL(ISJSON(po.SourceSnapshotJson), 0) <> 1
                          OR ISNULL(LEN(po.SourceIntegrityHash), 0) <> 64
                          OR po.SourceValidatedAtUtc IS NULL
                          OR i.ReceiptTolerancePercent < 0
                          OR i.ReceiptTolerancePercent > 100
                          OR i.ReceiptTolerancePercent <> COALESCE(po.TolerancePercent, 0)
                          OR ISNULL(ISJSON(i.ReceiptSourceSnapshotJson), 0) <> 1
                          OR ISNULL(LEN(i.ReceiptSourceIntegrityHash), 0) <> 64
                          OR i.ReceiptSourceValidatedAtUtc IS NULL))
                    THROW 51102, 'RCV_SOURCE_INVALID: a receipt requires an effective approved purchase-order source snapshot and valid tolerance.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN PurchaseOrders po
                        ON po.Id = i.PurchaseOrderId
                       AND po.TenantId = i.TenantId
                       AND po.IsDeleted = 0
                    WHERE i.IsDeleted = 0
                      AND po.Id IS NULL)
                    THROW 51103, 'RCV_SOURCE_TENANT_MISMATCH: purchase-order receipt source is not in the same tenant.', 1;
            END
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_PurchaseOrderReceiptItems_GovernedCapacity]
            ON [dbo].[PurchaseOrderReceiptItems]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    INNER JOIN deleted d ON d.Id = i.Id
                    WHERE i.TenantId <> d.TenantId
                       OR i.ReceiptId <> d.ReceiptId
                       OR i.PurchaseOrderItemId <> d.PurchaseOrderItemId
                       OR i.ReceivedQuantity <> d.ReceivedQuantity
                       OR i.OrderedQuantitySnapshot <> d.OrderedQuantitySnapshot
                       OR i.PreviouslyReceiptedQuantitySnapshot <> d.PreviouslyReceiptedQuantitySnapshot
                       OR i.ToleranceQuantitySnapshot <> d.ToleranceQuantitySnapshot
                       OR i.MaximumReceivableQuantitySnapshot <> d.MaximumReceivableQuantitySnapshot
                       OR i.RemainingQuantityBeforeReceiptSnapshot <> d.RemainingQuantityBeforeReceiptSnapshot
                       OR ISNULL(i.ReceiptLineIntegrityHash, N'') <> ISNULL(d.ReceiptLineIntegrityHash, N''))
                    THROW 51111, 'RCV_LINE_SOURCE_IMMUTABLE: receipt line source, quantity, and capacity snapshot are immutable.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    INNER JOIN PurchaseOrderReceipts r WITH (UPDLOCK, HOLDLOCK)
                        ON r.Id = i.ReceiptId
                       AND r.TenantId = i.TenantId
                       AND r.IsDeleted = 0
                    INNER JOIN PurchaseOrderItems poi WITH (UPDLOCK, HOLDLOCK)
                        ON poi.Id = i.PurchaseOrderItemId
                       AND poi.PurchaseOrderId = r.PurchaseOrderId
                       AND poi.TenantId = i.TenantId
                       AND poi.IsDeleted = 0
                    OUTER APPLY (
                        SELECT COALESCE(SUM(otherLine.ReceivedQuantity), 0) AS Quantity
                        FROM PurchaseOrderReceiptItems otherLine WITH (UPDLOCK, HOLDLOCK)
                        INNER JOIN PurchaseOrderReceipts otherReceipt
                            ON otherReceipt.Id = otherLine.ReceiptId
                           AND otherReceipt.TenantId = i.TenantId
                           AND otherReceipt.IsDeleted = 0
                           AND otherReceipt.Status NOT IN (N'Rejected', N'Cancelled')
                        WHERE otherLine.TenantId = i.TenantId
                          AND otherLine.PurchaseOrderItemId = i.PurchaseOrderItemId
                          AND otherLine.Id <> i.Id
                          AND otherLine.IsDeleted = 0
                    ) priorPurchase
                    OUTER APPLY (
                        SELECT COALESCE(SUM(grnLine.ReceivedQuantity), 0) AS Quantity
                        FROM GoodsReceiptNoteItems grnLine WITH (UPDLOCK, HOLDLOCK)
                        INNER JOIN GoodsReceiptNotes grn
                            ON grn.Id = grnLine.GoodsReceiptNoteId
                           AND grn.TenantId = i.TenantId
                           AND grn.IsDeleted = 0
                           AND grn.PurchaseOrderReceiptId IS NULL
                           AND grn.Status NOT IN (6, 8)
                        WHERE grnLine.TenantId = i.TenantId
                          AND grnLine.PurchaseOrderItemId = i.PurchaseOrderItemId
                          AND grnLine.IsDeleted = 0
                    ) priorDirectGrn
                    WHERE i.IsDeleted = 0
                      AND (
                          i.ReceivedQuantity <= 0
                          OR i.AcceptedQuantity < 0
                          OR i.RejectedQuantity < 0
                          OR i.AcceptedQuantity + i.RejectedQuantity > i.ReceivedQuantity
                          OR i.OrderedQuantitySnapshot <> poi.OrderedQuantity
                          OR i.PreviouslyReceiptedQuantitySnapshot <> priorPurchase.Quantity + priorDirectGrn.Quantity
                          OR i.ToleranceQuantitySnapshot <> ROUND(poi.OrderedQuantity * r.ReceiptTolerancePercent / 100.0, 4)
                          OR i.MaximumReceivableQuantitySnapshot <> poi.OrderedQuantity + ROUND(poi.OrderedQuantity * r.ReceiptTolerancePercent / 100.0, 4)
                          OR i.RemainingQuantityBeforeReceiptSnapshot <>
                             CASE
                                 WHEN poi.OrderedQuantity + ROUND(poi.OrderedQuantity * r.ReceiptTolerancePercent / 100.0, 4)
                                      - priorPurchase.Quantity - priorDirectGrn.Quantity > 0
                                 THEN poi.OrderedQuantity + ROUND(poi.OrderedQuantity * r.ReceiptTolerancePercent / 100.0, 4)
                                      - priorPurchase.Quantity - priorDirectGrn.Quantity
                                 ELSE 0
                             END
                          OR i.ReceivedQuantity > i.RemainingQuantityBeforeReceiptSnapshot
                          OR ISNULL(LEN(i.ReceiptLineIntegrityHash), 0) <> 64))
                    THROW 51112, 'RCV_CAPACITY_EXCEEDED: receipt line is not governed by the exact PO line or exceeds remaining quantity including tolerance.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN PurchaseOrderReceipts r
                        ON r.Id = i.ReceiptId
                       AND r.TenantId = i.TenantId
                       AND r.IsDeleted = 0
                    LEFT JOIN PurchaseOrderItems poi
                        ON poi.Id = i.PurchaseOrderItemId
                       AND poi.PurchaseOrderId = r.PurchaseOrderId
                       AND poi.TenantId = i.TenantId
                       AND poi.IsDeleted = 0
                    WHERE i.IsDeleted = 0
                      AND (r.Id IS NULL OR poi.Id IS NULL))
                    THROW 51113, 'RCV_LINE_TENANT_MISMATCH: receipt line source is not in the same tenant and purchase order.', 1;
            END
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_GoodsReceiptNotes_GovernedSource]
            ON [dbo].[GoodsReceiptNotes]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    INNER JOIN deleted d ON d.Id = i.Id
                    WHERE i.TenantId <> d.TenantId
                       OR ISNULL(i.PurchaseOrderId, '00000000-0000-0000-0000-000000000000') <>
                          ISNULL(d.PurchaseOrderId, '00000000-0000-0000-0000-000000000000')
                       OR ISNULL(i.PurchaseOrderReceiptId, '00000000-0000-0000-0000-000000000000') <>
                          ISNULL(d.PurchaseOrderReceiptId, '00000000-0000-0000-0000-000000000000')
                       OR ISNULL(i.IdempotencyKey, N'') <> ISNULL(d.IdempotencyKey, N''))
                    THROW 51121, 'RCV_GRN_SOURCE_IMMUTABLE: GRN tenant, purchase-order source, and idempotency key are immutable.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    INNER JOIN PurchaseOrders po WITH (UPDLOCK, HOLDLOCK)
                        ON po.Id = i.PurchaseOrderId
                       AND po.TenantId = i.TenantId
                       AND po.IsDeleted = 0
                    LEFT JOIN deleted d ON d.Id = i.Id
                    LEFT JOIN PurchaseOrderReceipts por
                        ON por.Id = i.PurchaseOrderReceiptId
                       AND por.PurchaseOrderId = i.PurchaseOrderId
                       AND por.TenantId = i.TenantId
                       AND por.IsDeleted = 0
                    LEFT JOIN Warehouses warehouse
                        ON warehouse.Id = i.WarehouseId
                       AND warehouse.TenantId = i.TenantId
                       AND warehouse.IsDeleted = 0
                    WHERE i.IsDeleted = 0
                      AND (
                          i.PurchaseOrderId IS NULL
                          OR i.SupplierId IS NULL
                          OR i.SupplierId <> po.BusinessPartnerId
                          OR warehouse.Id IS NULL
                          OR (d.Id IS NULL AND i.PurchaseOrderReceiptId IS NULL AND po.Status NOT IN (N'Approved', N'Partially Received'))
                          OR (d.Id IS NOT NULL AND po.Status NOT IN (N'Approved', N'Partially Received', N'Received'))
                          OR (i.PurchaseOrderReceiptId IS NOT NULL AND por.Id IS NULL)
                          OR po.ProcurementSourceType IS NULL
                          OR po.ProcurementSourceType NOT BETWEEN 0 AND 4
                          OR po.ProcurementSourceId IS NULL
                          OR NULLIF(LTRIM(RTRIM(po.ProcurementSourceReference)), N'') IS NULL
                          OR ISNULL(ISJSON(po.SourceSnapshotJson), 0) <> 1
                          OR ISNULL(LEN(po.SourceIntegrityHash), 0) <> 64
                          OR po.SourceValidatedAtUtc IS NULL
                          OR i.ReceiptTolerancePercent < 0
                          OR i.ReceiptTolerancePercent > 100
                          OR i.ReceiptTolerancePercent <> COALESCE(po.TolerancePercent, 0)
                          OR ISNULL(ISJSON(i.ReceiptSourceSnapshotJson), 0) <> 1
                          OR ISNULL(LEN(i.ReceiptSourceIntegrityHash), 0) <> 64
                          OR i.ReceiptSourceValidatedAtUtc IS NULL))
                    THROW 51122, 'RCV_GRN_SOURCE_INVALID: a GRN requires an effective governed purchase order, supplier, warehouse, and source snapshot.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN PurchaseOrders po
                        ON po.Id = i.PurchaseOrderId
                       AND po.TenantId = i.TenantId
                       AND po.IsDeleted = 0
                    WHERE i.IsDeleted = 0
                      AND po.Id IS NULL)
                    THROW 51123, 'RCV_GRN_TENANT_MISMATCH: GRN purchase-order source is not in the same tenant.', 1;
            END
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_GoodsReceiptNoteItems_GovernedCapacity]
            ON [dbo].[GoodsReceiptNoteItems]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    INNER JOIN deleted d ON d.Id = i.Id
                    WHERE i.TenantId <> d.TenantId
                       OR i.GoodsReceiptNoteId <> d.GoodsReceiptNoteId
                       OR ISNULL(i.PurchaseOrderItemId, '00000000-0000-0000-0000-000000000000') <>
                          ISNULL(d.PurchaseOrderItemId, '00000000-0000-0000-0000-000000000000')
                       OR i.InventoryItemId <> d.InventoryItemId
                       OR i.ReceivedQuantity <> d.ReceivedQuantity
                       OR i.PreviouslyReceiptedQuantitySnapshot <> d.PreviouslyReceiptedQuantitySnapshot
                       OR i.ToleranceQuantitySnapshot <> d.ToleranceQuantitySnapshot
                       OR i.MaximumReceivableQuantitySnapshot <> d.MaximumReceivableQuantitySnapshot
                       OR i.RemainingQuantityBeforeReceiptSnapshot <> d.RemainingQuantityBeforeReceiptSnapshot
                       OR ISNULL(i.ReceiptLineIntegrityHash, N'') <> ISNULL(d.ReceiptLineIntegrityHash, N''))
                    THROW 51131, 'RCV_GRN_LINE_SOURCE_IMMUTABLE: GRN line source, quantity, and capacity snapshot are immutable.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    INNER JOIN GoodsReceiptNotes grn WITH (UPDLOCK, HOLDLOCK)
                        ON grn.Id = i.GoodsReceiptNoteId
                       AND grn.TenantId = i.TenantId
                       AND grn.IsDeleted = 0
                    INNER JOIN PurchaseOrderItems poi WITH (UPDLOCK, HOLDLOCK)
                        ON poi.Id = i.PurchaseOrderItemId
                       AND poi.PurchaseOrderId = grn.PurchaseOrderId
                       AND poi.InventoryItemId = i.InventoryItemId
                       AND poi.TenantId = i.TenantId
                       AND poi.IsDeleted = 0
                    OUTER APPLY (
                        SELECT COALESCE(SUM(porLine.ReceivedQuantity), 0) AS Quantity
                        FROM PurchaseOrderReceiptItems porLine WITH (UPDLOCK, HOLDLOCK)
                        INNER JOIN PurchaseOrderReceipts por
                            ON por.Id = porLine.ReceiptId
                           AND por.TenantId = i.TenantId
                           AND por.IsDeleted = 0
                           AND por.Status NOT IN (N'Rejected', N'Cancelled')
                        WHERE porLine.TenantId = i.TenantId
                          AND porLine.PurchaseOrderItemId = i.PurchaseOrderItemId
                          AND porLine.IsDeleted = 0
                    ) priorPurchase
                    OUTER APPLY (
                        SELECT COALESCE(SUM(otherLine.ReceivedQuantity), 0) AS Quantity
                        FROM GoodsReceiptNoteItems otherLine WITH (UPDLOCK, HOLDLOCK)
                        INNER JOIN GoodsReceiptNotes otherGrn
                            ON otherGrn.Id = otherLine.GoodsReceiptNoteId
                           AND otherGrn.TenantId = i.TenantId
                           AND otherGrn.IsDeleted = 0
                           AND otherGrn.PurchaseOrderReceiptId IS NULL
                           AND otherGrn.Status NOT IN (6, 8)
                        WHERE otherLine.TenantId = i.TenantId
                          AND otherLine.PurchaseOrderItemId = i.PurchaseOrderItemId
                          AND otherLine.Id <> i.Id
                          AND otherLine.IsDeleted = 0
                    ) priorDirectGrn
                    LEFT JOIN PurchaseOrderReceiptItems sourceLine
                        ON sourceLine.ReceiptId = grn.PurchaseOrderReceiptId
                       AND sourceLine.PurchaseOrderItemId = i.PurchaseOrderItemId
                       AND sourceLine.TenantId = i.TenantId
                       AND sourceLine.IsDeleted = 0
                    LEFT JOIN ItemUnitsOfMeasure sourceUom
                        ON sourceUom.Id = sourceLine.ItemUnitOfMeasureId
                       AND sourceUom.TenantId = i.TenantId
                       AND sourceUom.IsDeleted = 0
                    WHERE i.IsDeleted = 0
                      AND (
                          i.PurchaseOrderItemId IS NULL
                          OR i.ReceivedQuantity <= 0
                          OR i.AcceptedQuantity < 0
                          OR i.RejectedQuantity < 0
                          OR i.AcceptedQuantity + i.RejectedQuantity > i.ReceivedQuantity
                          OR ISNULL(LEN(i.ReceiptLineIntegrityHash), 0) <> 64
                          OR (
                              grn.PurchaseOrderReceiptId IS NULL
                              AND (
                                  i.OrderedQuantity <> poi.OrderedQuantity
                                  OR i.PreviouslyReceiptedQuantitySnapshot <> priorPurchase.Quantity + priorDirectGrn.Quantity
                                  OR i.ToleranceQuantitySnapshot <> ROUND(poi.OrderedQuantity * grn.ReceiptTolerancePercent / 100.0, 4)
                                  OR i.MaximumReceivableQuantitySnapshot <> poi.OrderedQuantity + ROUND(poi.OrderedQuantity * grn.ReceiptTolerancePercent / 100.0, 4)
                                  OR i.RemainingQuantityBeforeReceiptSnapshot <>
                                     CASE
                                         WHEN poi.OrderedQuantity + ROUND(poi.OrderedQuantity * grn.ReceiptTolerancePercent / 100.0, 4)
                                              - priorPurchase.Quantity - priorDirectGrn.Quantity > 0
                                         THEN poi.OrderedQuantity + ROUND(poi.OrderedQuantity * grn.ReceiptTolerancePercent / 100.0, 4)
                                              - priorPurchase.Quantity - priorDirectGrn.Quantity
                                         ELSE 0
                                     END
                                  OR i.ReceivedQuantity > i.RemainingQuantityBeforeReceiptSnapshot))
                          OR (
                              grn.PurchaseOrderReceiptId IS NOT NULL
                              AND (
                                  sourceLine.Id IS NULL
                                  OR i.ReceivedQuantity <> sourceLine.ReceivedQuantity * COALESCE(NULLIF(sourceUom.ConversionToBase, 0), 1)
                                  OR i.AcceptedQuantity <> sourceLine.AcceptedQuantity * COALESCE(NULLIF(sourceUom.ConversionToBase, 0), 1)
                                  OR i.RejectedQuantity <> sourceLine.RejectedQuantity * COALESCE(NULLIF(sourceUom.ConversionToBase, 0), 1))))
                      )
                    THROW 51132, 'RCV_GRN_CAPACITY_EXCEEDED: GRN line is not the exact PO/receipt line or exceeds remaining quantity including tolerance.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN GoodsReceiptNotes grn
                        ON grn.Id = i.GoodsReceiptNoteId
                       AND grn.TenantId = i.TenantId
                       AND grn.IsDeleted = 0
                    LEFT JOIN PurchaseOrderItems poi
                        ON poi.Id = i.PurchaseOrderItemId
                       AND poi.PurchaseOrderId = grn.PurchaseOrderId
                       AND poi.InventoryItemId = i.InventoryItemId
                       AND poi.TenantId = i.TenantId
                       AND poi.IsDeleted = 0
                    WHERE i.IsDeleted = 0
                      AND (grn.Id IS NULL OR poi.Id IS NULL))
                    THROW 51133, 'RCV_GRN_LINE_TENANT_MISMATCH: GRN line is not in the same tenant and purchase order.', 1;
            END
            """);
    }

    private static void AddInventoryPostingTriggers(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_InventoryMovements_GovernedPurchaseReceipt]
            ON [dbo].[InventoryMovements]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    INNER JOIN deleted d ON d.Id = i.Id
                    WHERE (i.MovementType = 1 OR d.MovementType = 1)
                      AND (
                          i.TenantId <> d.TenantId
                          OR i.InventoryItemId <> d.InventoryItemId
                          OR i.MovementType <> d.MovementType
                          OR i.Direction <> d.Direction
                          OR i.Quantity <> d.Quantity
                          OR i.ReferenceType <> d.ReferenceType
                          OR ISNULL(i.ReferenceId, '00000000-0000-0000-0000-000000000000') <>
                             ISNULL(d.ReferenceId, '00000000-0000-0000-0000-000000000000')
                          OR i.IsReversal <> d.IsReversal))
                    THROW 51141, 'RCV_STOCK_SOURCE_IMMUTABLE: purchase receipt inventory movement source and quantity are immutable.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    OUTER APPLY (
                        SELECT SUM(line.AcceptedQuantity * COALESCE(NULLIF(itemUom.ConversionToBase, 0), 1)) AS Quantity
                        FROM PurchaseOrderReceipts receipt
                        INNER JOIN PurchaseOrderReceiptItems line
                            ON line.ReceiptId = receipt.Id
                           AND line.TenantId = i.TenantId
                           AND line.IsDeleted = 0
                        INNER JOIN PurchaseOrderItems poLine
                            ON poLine.Id = line.PurchaseOrderItemId
                           AND poLine.InventoryItemId = i.InventoryItemId
                           AND poLine.TenantId = i.TenantId
                           AND poLine.IsDeleted = 0
                        LEFT JOIN ItemUnitsOfMeasure itemUom
                            ON itemUom.Id = line.ItemUnitOfMeasureId
                           AND itemUom.TenantId = i.TenantId
                           AND itemUom.IsDeleted = 0
                        WHERE receipt.Id = i.ReferenceId
                          AND receipt.TenantId = i.TenantId
                          AND receipt.IsDeleted = 0
                          AND ISJSON(receipt.ReceiptSourceSnapshotJson) = 1
                          AND LEN(receipt.ReceiptSourceIntegrityHash) = 64
                    ) porAllowed
                    OUTER APPLY (
                        SELECT SUM(line.AcceptedQuantity) AS Quantity
                        FROM GoodsReceiptNotes grn
                        INNER JOIN GoodsReceiptNoteItems line
                            ON line.GoodsReceiptNoteId = grn.Id
                           AND line.TenantId = i.TenantId
                           AND line.InventoryItemId = i.InventoryItemId
                           AND line.IsDeleted = 0
                        WHERE grn.Id = i.ReferenceId
                          AND grn.TenantId = i.TenantId
                          AND grn.IsDeleted = 0
                          AND ISJSON(grn.ReceiptSourceSnapshotJson) = 1
                          AND LEN(grn.ReceiptSourceIntegrityHash) = 64
                    ) grnAllowed
                    OUTER APPLY (
                        SELECT COALESCE(SUM(
                            CASE
                                WHEN movement.IsReversal = 1 OR movement.Direction = 2
                                THEN -movement.Quantity
                                ELSE movement.Quantity
                            END), 0) AS Quantity
                        FROM InventoryMovements movement WITH (UPDLOCK, HOLDLOCK)
                        WHERE movement.TenantId = i.TenantId
                          AND movement.ReferenceId = i.ReferenceId
                          AND movement.InventoryItemId = i.InventoryItemId
                          AND movement.MovementType = 1
                          AND movement.ReferenceType = 1
                          AND movement.IsDeleted = 0
                    ) posted
                    WHERE i.IsDeleted = 0
                      AND i.MovementType = 1
                      AND (
                          i.ReferenceType <> 1
                          OR i.ReferenceId IS NULL
                          OR i.Quantity <= 0
                          OR NOT ((i.IsReversal = 0 AND i.Direction = 1) OR (i.IsReversal = 1 AND i.Direction = 2))
                          OR COALESCE(porAllowed.Quantity, grnAllowed.Quantity) IS NULL
                          OR posted.Quantity > COALESCE(porAllowed.Quantity, grnAllowed.Quantity)))
                    THROW 51142, 'RCV_PHANTOM_STOCK_BLOCKED: purchase receipt inventory movement requires an authorized governed receipt and accepted quantity.', 1;
            END
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_StockMovements_GovernedPurchaseReceipt]
            ON [dbo].[StockMovements]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    INNER JOIN deleted d ON d.Id = i.Id
                    WHERE (i.MovementType = N'Receipt' OR d.MovementType = N'Receipt')
                      AND (
                          i.TenantId <> d.TenantId
                          OR i.InventoryItemId <> d.InventoryItemId
                          OR i.MovementType <> d.MovementType
                          OR i.Quantity <> d.Quantity
                          OR i.ReferenceType <> d.ReferenceType
                          OR ISNULL(i.ReferenceId, '00000000-0000-0000-0000-000000000000') <>
                             ISNULL(d.ReferenceId, '00000000-0000-0000-0000-000000000000')))
                    THROW 51151, 'RCV_STOCK_SOURCE_IMMUTABLE: stock receipt source and quantity are immutable.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    OUTER APPLY (
                        SELECT SUM(line.AcceptedQuantity * COALESCE(NULLIF(itemUom.ConversionToBase, 0), 1)) AS Quantity
                        FROM PurchaseOrderReceipts receipt
                        INNER JOIN PurchaseOrderReceiptItems line
                            ON line.ReceiptId = receipt.Id
                           AND line.TenantId = i.TenantId
                           AND line.IsDeleted = 0
                        INNER JOIN PurchaseOrderItems poLine
                            ON poLine.Id = line.PurchaseOrderItemId
                           AND poLine.InventoryItemId = i.InventoryItemId
                           AND poLine.TenantId = i.TenantId
                           AND poLine.IsDeleted = 0
                        LEFT JOIN ItemUnitsOfMeasure itemUom
                            ON itemUom.Id = line.ItemUnitOfMeasureId
                           AND itemUom.TenantId = i.TenantId
                           AND itemUom.IsDeleted = 0
                        WHERE receipt.Id = i.ReferenceId
                          AND receipt.TenantId = i.TenantId
                          AND receipt.IsDeleted = 0
                          AND ISJSON(receipt.ReceiptSourceSnapshotJson) = 1
                          AND LEN(receipt.ReceiptSourceIntegrityHash) = 64
                    ) porAllowed
                    OUTER APPLY (
                        SELECT SUM(line.AcceptedQuantity) AS Quantity
                        FROM GoodsReceiptNotes grn
                        INNER JOIN GoodsReceiptNoteItems line
                            ON line.GoodsReceiptNoteId = grn.Id
                           AND line.TenantId = i.TenantId
                           AND line.InventoryItemId = i.InventoryItemId
                           AND line.IsDeleted = 0
                        WHERE grn.Id = i.ReferenceId
                          AND grn.TenantId = i.TenantId
                          AND grn.IsDeleted = 0
                          AND ISJSON(grn.ReceiptSourceSnapshotJson) = 1
                          AND LEN(grn.ReceiptSourceIntegrityHash) = 64
                    ) grnAllowed
                    OUTER APPLY (
                        SELECT COALESCE(SUM(movement.Quantity), 0) AS Quantity
                        FROM StockMovements movement WITH (UPDLOCK, HOLDLOCK)
                        WHERE movement.TenantId = i.TenantId
                          AND movement.ReferenceId = i.ReferenceId
                          AND movement.InventoryItemId = i.InventoryItemId
                          AND movement.MovementType = N'Receipt'
                          AND movement.ReferenceType = 1
                          AND movement.IsDeleted = 0
                    ) posted
                    WHERE i.IsDeleted = 0
                      AND i.MovementType = N'Receipt'
                      AND (
                          i.ReferenceType <> 1
                          OR i.ReferenceId IS NULL
                          OR i.Quantity <= 0
                          OR COALESCE(porAllowed.Quantity, grnAllowed.Quantity) IS NULL
                          OR posted.Quantity > COALESCE(porAllowed.Quantity, grnAllowed.Quantity)))
                    THROW 51152, 'RCV_PHANTOM_STOCK_BLOCKED: stock receipt requires an authorized governed receipt and accepted quantity.', 1;
            END
            """);
    }
}
