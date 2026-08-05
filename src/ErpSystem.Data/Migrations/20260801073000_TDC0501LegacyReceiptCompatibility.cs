using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ErpSystem.Data.ApplicationDbContext))]
[Migration("20260801073000_TDC0501LegacyReceiptCompatibility")]
public sealed class TDC0501LegacyReceiptCompatibility : Migration
{
    private const string OriginalSourceTypeGuard =
        "po.ProcurementSourceType NOT BETWEEN 0 AND 4";

    private const string HistoricalUpdateSourceTypeGuard =
        "(po.ProcurementSourceType NOT BETWEEN 0 AND 4 AND NOT (d.Id IS NOT NULL AND po.ProcurementSourceType = 5 AND JSON_VALUE(i.ReceiptSourceSnapshotJson, '$.sourceType') = N'HistoricalMigration'))";

    private const string CapacityPurchaseOrderJoin =
        "INNER JOIN PurchaseOrderItems poi WITH (UPDLOCK, HOLDLOCK)";

    private const string CapacityPurchaseOrderJoinWithLegacyRow =
        "LEFT JOIN deleted legacyRow ON legacyRow.Id = i.Id\n        INNER JOIN PurchaseOrderItems poi WITH (UPDLOCK, HOLDLOCK)";

    private const string PurchaseReceiptCapacityGuard =
        "OR i.ReceivedQuantity > i.RemainingQuantityBeforeReceiptSnapshot";

    private const string HistoricalPurchaseReceiptCapacityGuard =
        "OR ((legacyRow.Id IS NULL OR ISNULL(JSON_VALUE(r.ReceiptSourceSnapshotJson, '$.sourceType'), N'') <> N'HistoricalMigration') AND i.ReceivedQuantity > i.RemainingQuantityBeforeReceiptSnapshot)";

    private const string GoodsReceiptCapacityGuard =
        "OR i.ReceivedQuantity > i.RemainingQuantityBeforeReceiptSnapshot))";

    private const string HistoricalGoodsReceiptCapacityGuard =
        "OR ((legacyRow.Id IS NULL OR ISNULL(JSON_VALUE(grn.ReceiptSourceSnapshotJson, '$.sourceType'), N'') <> N'HistoricalMigration') AND i.ReceivedQuantity > i.RemainingQuantityBeforeReceiptSnapshot)))";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        Patch(migrationBuilder, "TR_PurchaseOrderReceipts_GovernedSource",
            OriginalSourceTypeGuard, HistoricalUpdateSourceTypeGuard, 51631);
        Patch(migrationBuilder, "TR_GoodsReceiptNotes_GovernedSource",
            OriginalSourceTypeGuard, HistoricalUpdateSourceTypeGuard, 51632);
        Patch(migrationBuilder, "TR_PurchaseOrderReceiptItems_GovernedCapacity",
            CapacityPurchaseOrderJoin,
            CapacityPurchaseOrderJoinWithLegacyRow, 51633);
        Patch(migrationBuilder, "TR_PurchaseOrderReceiptItems_GovernedCapacity",
            PurchaseReceiptCapacityGuard,
            HistoricalPurchaseReceiptCapacityGuard, 51634);
        Patch(migrationBuilder, "TR_GoodsReceiptNoteItems_GovernedCapacity",
            CapacityPurchaseOrderJoin,
            CapacityPurchaseOrderJoinWithLegacyRow, 51635);
        Patch(migrationBuilder, "TR_GoodsReceiptNoteItems_GovernedCapacity",
            GoodsReceiptCapacityGuard,
            HistoricalGoodsReceiptCapacityGuard, 51636);

        migrationBuilder.Sql(BackfillSql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        Patch(migrationBuilder, "TR_GoodsReceiptNoteItems_GovernedCapacity",
            HistoricalGoodsReceiptCapacityGuard,
            GoodsReceiptCapacityGuard, 51641);
        Patch(migrationBuilder, "TR_GoodsReceiptNoteItems_GovernedCapacity",
            CapacityPurchaseOrderJoinWithLegacyRow,
            CapacityPurchaseOrderJoin, 51642);
        Patch(migrationBuilder, "TR_PurchaseOrderReceiptItems_GovernedCapacity",
            HistoricalPurchaseReceiptCapacityGuard,
            PurchaseReceiptCapacityGuard, 51643);
        Patch(migrationBuilder, "TR_PurchaseOrderReceiptItems_GovernedCapacity",
            CapacityPurchaseOrderJoinWithLegacyRow,
            CapacityPurchaseOrderJoin, 51644);
        Patch(migrationBuilder, "TR_GoodsReceiptNotes_GovernedSource",
            HistoricalUpdateSourceTypeGuard, OriginalSourceTypeGuard, 51645);
        Patch(migrationBuilder, "TR_PurchaseOrderReceipts_GovernedSource",
            HistoricalUpdateSourceTypeGuard, OriginalSourceTypeGuard, 51646);
    }

    private static void Patch(
        MigrationBuilder migrationBuilder,
        string triggerName,
        string expected,
        string replacement,
        int errorNumber) =>
        migrationBuilder.Sql(PatchTriggerSql(
            triggerName, expected, replacement, errorNumber));

    private static string PatchTriggerSql(
        string triggerName,
        string expected,
        string replacement,
        int errorNumber)
    {
        var expectedLiteral = expected.Replace("'", "''", StringComparison.Ordinal);
        var replacementLiteral = replacement.Replace("'", "''", StringComparison.Ordinal);
        return $$"""
        DECLARE @definition nvarchar(max) =
            OBJECT_DEFINITION(OBJECT_ID(N'dbo.{{triggerName}}'));
        DECLARE @expected nvarchar(max) = N'{{expectedLiteral}}';
        DECLARE @replacement nvarchar(max) = N'{{replacementLiteral}}';
        DECLARE @triggerKeywordIndex int;
        DECLARE @createKeywordIndex int;
        DECLARE @alterKeywordIndex int;

        SET @definition = REPLACE(
            @definition,
            NCHAR(13) + NCHAR(10),
            NCHAR(10));

        IF @definition IS NULL
            THROW {{errorNumber}}, 'TDC0501_LEGACY_TRIGGER_MISSING: required receipt trigger is missing.', 1;

        IF CHARINDEX(@replacement, @definition) = 0
        BEGIN
            IF CHARINDEX(@expected, @definition) = 0
                THROW {{errorNumber}}, 'TDC0501_LEGACY_TRIGGER_PATCH_FAILED: expected receipt guard was not found.', 1;

            SET @definition = REPLACE(@definition, @expected, @replacement);
            SET @triggerKeywordIndex = CHARINDEX(N'TRIGGER', UPPER(@definition));
            SET @createKeywordIndex = CHARINDEX(N'CREATE', UPPER(@definition));
            SET @alterKeywordIndex = CHARINDEX(N'ALTER', UPPER(@definition));
            IF @createKeywordIndex > 0 AND @createKeywordIndex < @triggerKeywordIndex
            BEGIN
                IF @alterKeywordIndex > @createKeywordIndex AND
                   @alterKeywordIndex < @triggerKeywordIndex
                    SET @definition = STUFF(
                        @definition,
                        @createKeywordIndex,
                        @alterKeywordIndex - @createKeywordIndex,
                        N'');
                ELSE
                    SET @definition = STUFF(
                        @definition,
                        @createKeywordIndex,
                        LEN(N'CREATE'),
                        N'ALTER');
            END
            ELSE IF @alterKeywordIndex = 0 OR
                    @alterKeywordIndex > @triggerKeywordIndex
                THROW {{errorNumber}}, 'TDC0501_LEGACY_TRIGGER_PATCH_FAILED: trigger definition cannot be altered safely.', 1;

            EXEC sys.sp_executesql @definition;
        END;
        """;
    }

    private const string BackfillSql =
        """
        DISABLE TRIGGER [dbo].[TR_PurchaseOrderReceipts_GovernedSource]
            ON [dbo].[PurchaseOrderReceipts];
        DISABLE TRIGGER [dbo].[TR_PurchaseOrderReceipts_SodHardStop]
            ON [dbo].[PurchaseOrderReceipts];
        DISABLE TRIGGER [dbo].[TR_PurchaseOrderReceiptItems_GovernedCapacity]
            ON [dbo].[PurchaseOrderReceiptItems];
        DISABLE TRIGGER [dbo].[TR_PurchaseOrderReceiptItems_TDC0502AcceptanceProtected]
            ON [dbo].[PurchaseOrderReceiptItems];
        DISABLE TRIGGER [dbo].[TR_GoodsReceiptNotes_GovernedSource]
            ON [dbo].[GoodsReceiptNotes];
        DISABLE TRIGGER [dbo].[TR_GoodsReceiptNotes_TDC0503SodHardStop]
            ON [dbo].[GoodsReceiptNotes];
        DISABLE TRIGGER [dbo].[TR_GoodsReceiptNoteItems_GovernedCapacity]
            ON [dbo].[GoodsReceiptNoteItems];
        DISABLE TRIGGER [dbo].[TR_GoodsReceiptNoteItems_TDC0502AcceptanceProtected]
            ON [dbo].[GoodsReceiptNoteItems];

        UPDATE receipt
        SET receipt.ReceiptTolerancePercent =
                CASE
                    WHEN COALESCE(po.TolerancePercent, 0) < 0 THEN 0
                    WHEN COALESCE(po.TolerancePercent, 0) > 100 THEN 100
                    ELSE COALESCE(po.TolerancePercent, 0)
                END,
            receipt.ReceiptSourceSnapshotJson =
                CONCAT('{"schema":"TDC-0501","sourceType":"HistoricalMigration","receiptId":"',
                       CONVERT(varchar(36), receipt.Id),
                       '","purchaseOrderId":"',
                       CONVERT(varchar(36), receipt.PurchaseOrderId), '"}'),
            receipt.ReceiptSourceIntegrityHash =
                LOWER(CONVERT(varchar(64), HASHBYTES('SHA2_256',
                    CONCAT('TDC-0501|HistoricalMigration|PurchaseOrderReceipt|',
                           CONVERT(varchar(36), receipt.Id), '|',
                           CONVERT(varchar(36), receipt.PurchaseOrderId))), 2)),
            receipt.ReceiptSourceValidatedAtUtc =
                COALESCE(receipt.ReceiptSourceValidatedAtUtc,
                         receipt.CreatedAt,
                         po.SourceValidatedAtUtc,
                         SYSUTCDATETIME())
        FROM PurchaseOrderReceipts receipt
        INNER JOIN PurchaseOrders po
            ON po.Id = receipt.PurchaseOrderId
           AND po.TenantId = receipt.TenantId
           AND po.IsDeleted = 0
        WHERE receipt.IsDeleted = 0
          AND po.ProcurementSourceType = 5;

        UPDATE grn
        SET grn.ReceiptTolerancePercent =
                CASE
                    WHEN COALESCE(po.TolerancePercent, 0) < 0 THEN 0
                    WHEN COALESCE(po.TolerancePercent, 0) > 100 THEN 100
                    ELSE COALESCE(po.TolerancePercent, 0)
                END,
            grn.ReceiptSourceSnapshotJson =
                CONCAT('{"schema":"TDC-0501","sourceType":"HistoricalMigration","goodsReceiptNoteId":"',
                       CONVERT(varchar(36), grn.Id),
                       '","purchaseOrderId":"',
                       CONVERT(varchar(36), grn.PurchaseOrderId), '"}'),
            grn.ReceiptSourceIntegrityHash =
                LOWER(CONVERT(varchar(64), HASHBYTES('SHA2_256',
                    CONCAT('TDC-0501|HistoricalMigration|GoodsReceiptNote|',
                           CONVERT(varchar(36), grn.Id), '|',
                           CONVERT(varchar(36), grn.PurchaseOrderId))), 2)),
            grn.ReceiptSourceValidatedAtUtc =
                COALESCE(grn.ReceiptSourceValidatedAtUtc,
                         grn.CreatedAt,
                         po.SourceValidatedAtUtc,
                         SYSUTCDATETIME())
        FROM GoodsReceiptNotes grn
        INNER JOIN PurchaseOrders po
            ON po.Id = grn.PurchaseOrderId
           AND po.TenantId = grn.TenantId
           AND po.IsDeleted = 0
        WHERE grn.IsDeleted = 0
          AND po.ProcurementSourceType = 5;

        UPDATE receiptLine
        SET receiptLine.OrderedQuantitySnapshot = poLine.OrderedQuantity,
            receiptLine.PreviouslyReceiptedQuantitySnapshot =
                priorPurchase.Quantity + priorDirectGrn.Quantity,
            receiptLine.ToleranceQuantitySnapshot =
                ROUND(poLine.OrderedQuantity * receipt.ReceiptTolerancePercent / 100.0, 4),
            receiptLine.MaximumReceivableQuantitySnapshot =
                poLine.OrderedQuantity +
                ROUND(poLine.OrderedQuantity * receipt.ReceiptTolerancePercent / 100.0, 4),
            receiptLine.RemainingQuantityBeforeReceiptSnapshot =
                CASE
                    WHEN poLine.OrderedQuantity +
                         ROUND(poLine.OrderedQuantity * receipt.ReceiptTolerancePercent / 100.0, 4) -
                         priorPurchase.Quantity - priorDirectGrn.Quantity > 0
                    THEN poLine.OrderedQuantity +
                         ROUND(poLine.OrderedQuantity * receipt.ReceiptTolerancePercent / 100.0, 4) -
                         priorPurchase.Quantity - priorDirectGrn.Quantity
                    ELSE 0
                END,
            receiptLine.ReceiptLineIntegrityHash =
                LOWER(CONVERT(varchar(64), HASHBYTES('SHA2_256',
                    CONCAT('TDC-0501|HistoricalMigration|PurchaseOrderReceiptItem|',
                           CONVERT(varchar(36), receiptLine.Id), '|',
                           CONVERT(varchar(36), receiptLine.PurchaseOrderItemId), '|',
                           CONVERT(varchar(50), receiptLine.ReceivedQuantity))), 2))
        FROM PurchaseOrderReceiptItems receiptLine
        INNER JOIN PurchaseOrderReceipts receipt
            ON receipt.Id = receiptLine.ReceiptId
           AND receipt.TenantId = receiptLine.TenantId
           AND receipt.IsDeleted = 0
        INNER JOIN PurchaseOrderItems poLine
            ON poLine.Id = receiptLine.PurchaseOrderItemId
           AND poLine.PurchaseOrderId = receipt.PurchaseOrderId
           AND poLine.TenantId = receiptLine.TenantId
           AND poLine.IsDeleted = 0
        OUTER APPLY (
            SELECT COALESCE(SUM(otherLine.ReceivedQuantity), 0) Quantity
            FROM PurchaseOrderReceiptItems otherLine
            INNER JOIN PurchaseOrderReceipts otherReceipt
                ON otherReceipt.Id = otherLine.ReceiptId
               AND otherReceipt.TenantId = receiptLine.TenantId
               AND otherReceipt.IsDeleted = 0
               AND otherReceipt.Status NOT IN (N'Rejected', N'Cancelled')
            WHERE otherLine.TenantId = receiptLine.TenantId
              AND otherLine.PurchaseOrderItemId = receiptLine.PurchaseOrderItemId
              AND otherLine.Id <> receiptLine.Id
              AND otherLine.IsDeleted = 0
        ) priorPurchase
        OUTER APPLY (
            SELECT COALESCE(SUM(grnLine.ReceivedQuantity), 0) Quantity
            FROM GoodsReceiptNoteItems grnLine
            INNER JOIN GoodsReceiptNotes directGrn
                ON directGrn.Id = grnLine.GoodsReceiptNoteId
               AND directGrn.TenantId = receiptLine.TenantId
               AND directGrn.IsDeleted = 0
               AND directGrn.PurchaseOrderReceiptId IS NULL
               AND directGrn.Status NOT IN (6, 8)
            WHERE grnLine.TenantId = receiptLine.TenantId
              AND grnLine.PurchaseOrderItemId = receiptLine.PurchaseOrderItemId
              AND grnLine.IsDeleted = 0
        ) priorDirectGrn
        WHERE receiptLine.IsDeleted = 0
          AND JSON_VALUE(receipt.ReceiptSourceSnapshotJson, '$.sourceType') =
              N'HistoricalMigration';

        ;WITH legacyLineCandidates AS
        (
            SELECT grnLine.Id,
                   MIN(poLine.Id) AS PurchaseOrderItemId,
                   COUNT_BIG(*) AS CandidateCount
            FROM GoodsReceiptNoteItems grnLine
            INNER JOIN GoodsReceiptNotes grn
                ON grn.Id = grnLine.GoodsReceiptNoteId
               AND grn.TenantId = grnLine.TenantId
               AND grn.IsDeleted = 0
            INNER JOIN PurchaseOrderItems poLine
                ON poLine.PurchaseOrderId = grn.PurchaseOrderId
               AND poLine.InventoryItemId = grnLine.InventoryItemId
               AND poLine.TenantId = grnLine.TenantId
               AND poLine.IsDeleted = 0
            WHERE grnLine.IsDeleted = 0
              AND grnLine.PurchaseOrderItemId IS NULL
              AND JSON_VALUE(grn.ReceiptSourceSnapshotJson, '$.sourceType') =
                  N'HistoricalMigration'
            GROUP BY grnLine.Id
        )
        UPDATE grnLine
        SET grnLine.PurchaseOrderItemId = candidate.PurchaseOrderItemId
        FROM GoodsReceiptNoteItems grnLine
        INNER JOIN legacyLineCandidates candidate
            ON candidate.Id = grnLine.Id
           AND candidate.CandidateCount = 1;

        IF EXISTS
        (
            SELECT 1
            FROM GoodsReceiptNoteItems grnLine
            INNER JOIN GoodsReceiptNotes grn
                ON grn.Id = grnLine.GoodsReceiptNoteId
               AND grn.TenantId = grnLine.TenantId
               AND grn.IsDeleted = 0
            WHERE grnLine.IsDeleted = 0
              AND JSON_VALUE(grn.ReceiptSourceSnapshotJson, '$.sourceType') =
                  N'HistoricalMigration'
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM PurchaseOrderItems poLine
                  WHERE poLine.Id = grnLine.PurchaseOrderItemId
                    AND poLine.PurchaseOrderId = grn.PurchaseOrderId
                    AND poLine.InventoryItemId = grnLine.InventoryItemId
                    AND poLine.TenantId = grnLine.TenantId
                    AND poLine.IsDeleted = 0
              )
        )
            THROW 51637, 'TDC0501_LEGACY_GRN_LINE_UNRESOLVED: repair ambiguous or missing purchase-order line linkage before enabling governed receipt capacity.', 1;

        UPDATE grnLine
        SET grnLine.OrderedQuantity = poLine.OrderedQuantity,
            grnLine.PreviouslyReceiptedQuantitySnapshot =
                CASE WHEN sourceLine.Id IS NOT NULL
                     THEN sourceLine.PreviouslyReceiptedQuantitySnapshot
                     ELSE priorPurchase.Quantity + priorDirectGrn.Quantity END,
            grnLine.ToleranceQuantitySnapshot =
                CASE WHEN sourceLine.Id IS NOT NULL
                     THEN sourceLine.ToleranceQuantitySnapshot
                     ELSE ROUND(poLine.OrderedQuantity * grn.ReceiptTolerancePercent / 100.0, 4) END,
            grnLine.MaximumReceivableQuantitySnapshot =
                CASE WHEN sourceLine.Id IS NOT NULL
                     THEN sourceLine.MaximumReceivableQuantitySnapshot
                     ELSE poLine.OrderedQuantity +
                          ROUND(poLine.OrderedQuantity * grn.ReceiptTolerancePercent / 100.0, 4) END,
            grnLine.RemainingQuantityBeforeReceiptSnapshot =
                CASE WHEN sourceLine.Id IS NOT NULL
                     THEN sourceLine.RemainingQuantityBeforeReceiptSnapshot
                     WHEN poLine.OrderedQuantity +
                          ROUND(poLine.OrderedQuantity * grn.ReceiptTolerancePercent / 100.0, 4) -
                          priorPurchase.Quantity - priorDirectGrn.Quantity > 0
                     THEN poLine.OrderedQuantity +
                          ROUND(poLine.OrderedQuantity * grn.ReceiptTolerancePercent / 100.0, 4) -
                          priorPurchase.Quantity - priorDirectGrn.Quantity
                     ELSE 0 END,
            grnLine.ReceiptLineIntegrityHash =
                LOWER(CONVERT(varchar(64), HASHBYTES('SHA2_256',
                    CONCAT('TDC-0501|HistoricalMigration|GoodsReceiptNoteItem|',
                           CONVERT(varchar(36), grnLine.Id), '|',
                           CONVERT(varchar(36), grnLine.PurchaseOrderItemId), '|',
                           CONVERT(varchar(50), grnLine.ReceivedQuantity))), 2))
        FROM GoodsReceiptNoteItems grnLine
        INNER JOIN GoodsReceiptNotes grn
            ON grn.Id = grnLine.GoodsReceiptNoteId
           AND grn.TenantId = grnLine.TenantId
           AND grn.IsDeleted = 0
        INNER JOIN PurchaseOrderItems poLine
            ON poLine.Id = grnLine.PurchaseOrderItemId
           AND poLine.PurchaseOrderId = grn.PurchaseOrderId
           AND poLine.InventoryItemId = grnLine.InventoryItemId
           AND poLine.TenantId = grnLine.TenantId
           AND poLine.IsDeleted = 0
        LEFT JOIN PurchaseOrderReceiptItems sourceLine
            ON sourceLine.ReceiptId = grn.PurchaseOrderReceiptId
           AND sourceLine.PurchaseOrderItemId = grnLine.PurchaseOrderItemId
           AND sourceLine.TenantId = grnLine.TenantId
           AND sourceLine.IsDeleted = 0
        OUTER APPLY (
            SELECT COALESCE(SUM(receiptLine.ReceivedQuantity), 0) Quantity
            FROM PurchaseOrderReceiptItems receiptLine
            INNER JOIN PurchaseOrderReceipts receipt
                ON receipt.Id = receiptLine.ReceiptId
               AND receipt.TenantId = grnLine.TenantId
               AND receipt.IsDeleted = 0
               AND receipt.Status NOT IN (N'Rejected', N'Cancelled')
            WHERE receiptLine.TenantId = grnLine.TenantId
              AND receiptLine.PurchaseOrderItemId = grnLine.PurchaseOrderItemId
              AND receiptLine.IsDeleted = 0
        ) priorPurchase
        OUTER APPLY (
            SELECT COALESCE(SUM(otherLine.ReceivedQuantity), 0) Quantity
            FROM GoodsReceiptNoteItems otherLine
            INNER JOIN GoodsReceiptNotes otherGrn
                ON otherGrn.Id = otherLine.GoodsReceiptNoteId
               AND otherGrn.TenantId = grnLine.TenantId
               AND otherGrn.IsDeleted = 0
               AND otherGrn.PurchaseOrderReceiptId IS NULL
               AND otherGrn.Status NOT IN (6, 8)
            WHERE otherLine.TenantId = grnLine.TenantId
              AND otherLine.PurchaseOrderItemId = grnLine.PurchaseOrderItemId
              AND otherLine.Id <> grnLine.Id
              AND otherLine.IsDeleted = 0
        ) priorDirectGrn
        WHERE grnLine.IsDeleted = 0
          AND JSON_VALUE(grn.ReceiptSourceSnapshotJson, '$.sourceType') =
              N'HistoricalMigration';

        ENABLE TRIGGER [dbo].[TR_PurchaseOrderReceipts_GovernedSource]
            ON [dbo].[PurchaseOrderReceipts];
        ENABLE TRIGGER [dbo].[TR_PurchaseOrderReceipts_SodHardStop]
            ON [dbo].[PurchaseOrderReceipts];
        ENABLE TRIGGER [dbo].[TR_PurchaseOrderReceiptItems_GovernedCapacity]
            ON [dbo].[PurchaseOrderReceiptItems];
        ENABLE TRIGGER [dbo].[TR_PurchaseOrderReceiptItems_TDC0502AcceptanceProtected]
            ON [dbo].[PurchaseOrderReceiptItems];
        ENABLE TRIGGER [dbo].[TR_GoodsReceiptNotes_GovernedSource]
            ON [dbo].[GoodsReceiptNotes];
        ENABLE TRIGGER [dbo].[TR_GoodsReceiptNotes_TDC0503SodHardStop]
            ON [dbo].[GoodsReceiptNotes];
        ENABLE TRIGGER [dbo].[TR_GoodsReceiptNoteItems_GovernedCapacity]
            ON [dbo].[GoodsReceiptNoteItems];
        ENABLE TRIGGER [dbo].[TR_GoodsReceiptNoteItems_TDC0502AcceptanceProtected]
            ON [dbo].[GoodsReceiptNoteItems];
        """;
}
