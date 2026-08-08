using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260801140000_TDC0502ReceiptReplayAndReplacementLineage")]
public sealed class TDC0502ReceiptReplayAndReplacementLineage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "IdempotencyRequestHash",
            table: "GoodsReceiptNotes",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ReplacementInspectionCaseId",
            table: "ProcurementReceiptInspectionCases",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ReplacementLinkedAtUtc",
            table: "ProcurementReceiptInspectionCases",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ReplacementPurchaseOrderReceiptId",
            table: "ProcurementReceiptInspectionCases",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_GoodsReceiptNotes_IdempotencyRequestHash",
            table: "GoodsReceiptNotes",
            sql: "[IdempotencyRequestHash] IS NULL OR LEN([IdempotencyRequestHash]) = 64");

        migrationBuilder.AddCheckConstraint(
            name: "CK_ProcurementReceiptInspectionCases_ReplacementLineage",
            table: "ProcurementReceiptInspectionCases",
            sql: "([ReplacementPurchaseOrderReceiptId] IS NULL AND [ReplacementInspectionCaseId] IS NULL AND [ReplacementLinkedAtUtc] IS NULL) OR ([ReplacementPurchaseOrderReceiptId] IS NOT NULL AND [ReplacementInspectionCaseId] IS NOT NULL AND [ReplacementLinkedAtUtc] IS NOT NULL)");

        migrationBuilder.CreateIndex(
            name: "IX_ProcurementReceiptInspectionCases_ReplacementInspectionCaseId",
            table: "ProcurementReceiptInspectionCases",
            column: "ReplacementInspectionCaseId");

        migrationBuilder.CreateIndex(
            name: "IX_ProcurementReceiptInspectionCases_ReplacementPurchaseOrderReceiptId",
            table: "ProcurementReceiptInspectionCases",
            column: "ReplacementPurchaseOrderReceiptId");

        migrationBuilder.CreateIndex(
            name: "IX_ProcurementReceiptInspectionCases_TenantId_ReplacementPurchaseOrderReceiptId",
            table: "ProcurementReceiptInspectionCases",
            columns: new[] { "TenantId", "ReplacementPurchaseOrderReceiptId" },
            unique: true,
            filter: "[ReplacementPurchaseOrderReceiptId] IS NOT NULL");

        migrationBuilder.AddForeignKey(
            name: "FK_ProcurementReceiptInspectionCases_ProcurementReceiptInspectionCases_ReplacementInspectionCaseId",
            table: "ProcurementReceiptInspectionCases",
            column: "ReplacementInspectionCaseId",
            principalTable: "ProcurementReceiptInspectionCases",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_ProcurementReceiptInspectionCases_PurchaseOrderReceipts_ReplacementPurchaseOrderReceiptId",
            table: "ProcurementReceiptInspectionCases",
            column: "ReplacementPurchaseOrderReceiptId",
            principalTable: "PurchaseOrderReceipts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_TDC0501_GoodsReceiptIdempotencyFingerprint]
            ON [dbo].[GoodsReceiptNotes]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN deleted d ON d.Id = i.Id
                    WHERE d.Id IS NULL
                      AND i.IsDeleted = 0
                      AND i.IdempotencyKey IS NOT NULL
                      AND i.IdempotencyRequestHash IS NULL)
                    THROW 51920, 'RCV_IDEMPOTENCY_FINGERPRINT_REQUIRED: a governed GRN idempotency key requires its request fingerprint.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    JOIN deleted d ON d.Id = i.Id
                    WHERE d.IdempotencyRequestHash IS NOT NULL
                      AND (i.IdempotencyRequestHash IS NULL OR
                           i.IdempotencyRequestHash <> d.IdempotencyRequestHash))
                    THROW 51921, 'RCV_IDEMPOTENCY_FINGERPRINT_IMMUTABLE: a governed GRN request fingerprint cannot change.', 1;
            END
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_TDC0502_ReceiptReplacementLineage]
            ON [dbo].[ProcurementReceiptInspectionCases]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    JOIN deleted d ON d.Id = i.Id
                    WHERE d.ReplacementPurchaseOrderReceiptId IS NOT NULL
                      AND (i.ReplacementPurchaseOrderReceiptId IS NULL OR
                           i.ReplacementPurchaseOrderReceiptId <> d.ReplacementPurchaseOrderReceiptId OR
                           i.ReplacementInspectionCaseId IS NULL OR
                           i.ReplacementInspectionCaseId <> d.ReplacementInspectionCaseId OR
                           i.ReplacementLinkedAtUtc IS NULL OR
                           i.ReplacementLinkedAtUtc <> d.ReplacementLinkedAtUtc))
                    THROW 51922, 'RCV_REPLACEMENT_LINEAGE_IMMUTABLE: replacement-to-rejection lineage cannot be removed or changed.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    JOIN PurchaseOrderReceipts sourceReceipt
                      ON sourceReceipt.Id = i.PurchaseOrderReceiptId
                     AND sourceReceipt.TenantId = i.TenantId
                     AND sourceReceipt.IsDeleted = 0
                    LEFT JOIN PurchaseOrderReceipts replacementReceipt
                      ON replacementReceipt.Id = i.ReplacementPurchaseOrderReceiptId
                     AND replacementReceipt.TenantId = i.TenantId
                     AND replacementReceipt.IsDeleted = 0
                    LEFT JOIN ProcurementReceiptInspectionCases replacementCase
                      ON replacementCase.Id = i.ReplacementInspectionCaseId
                     AND replacementCase.TenantId = i.TenantId
                     AND replacementCase.IsDeleted = 0
                    OUTER APPLY (
                        SELECT TOP (1) action.OccurredAtUtc
                        FROM ProcurementReceiptInspectionActions action
                        WHERE action.TenantId = i.TenantId
                          AND action.InspectionCaseId = i.Id
                          AND action.ActionType = 10
                          AND action.IsDeleted = 0
                        ORDER BY action.Sequence DESC
                    ) replacementRequest
                    WHERE i.ReplacementPurchaseOrderReceiptId IS NOT NULL
                      AND (replacementReceipt.Id IS NULL OR
                           replacementReceipt.Id = sourceReceipt.Id OR
                           replacementReceipt.PurchaseOrderId <> sourceReceipt.PurchaseOrderId OR
                           replacementRequest.OccurredAtUtc IS NULL OR
                           replacementReceipt.CreatedAt < replacementRequest.OccurredAtUtc OR
                           replacementCase.Id IS NULL OR
                           replacementCase.PurchaseOrderReceiptId <> replacementReceipt.Id OR
                           replacementCase.Status <> 8 OR
                           replacementReceipt.CreatedAt > i.ReplacementLinkedAtUtc))
                    THROW 51923, 'RCV_REPLACEMENT_LINEAGE_INVALID: the replacement must be a later, closed, same-tenant receipt for the same purchase order.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_TDC0502_ReceiptReplacementLineage];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_TDC0501_GoodsReceiptIdempotencyFingerprint];");

        migrationBuilder.DropForeignKey(
            name: "FK_ProcurementReceiptInspectionCases_ProcurementReceiptInspectionCases_ReplacementInspectionCaseId",
            table: "ProcurementReceiptInspectionCases");

        migrationBuilder.DropForeignKey(
            name: "FK_ProcurementReceiptInspectionCases_PurchaseOrderReceipts_ReplacementPurchaseOrderReceiptId",
            table: "ProcurementReceiptInspectionCases");

        migrationBuilder.DropCheckConstraint(
            name: "CK_GoodsReceiptNotes_IdempotencyRequestHash",
            table: "GoodsReceiptNotes");

        migrationBuilder.DropCheckConstraint(
            name: "CK_ProcurementReceiptInspectionCases_ReplacementLineage",
            table: "ProcurementReceiptInspectionCases");

        migrationBuilder.DropIndex(
            name: "IX_ProcurementReceiptInspectionCases_ReplacementInspectionCaseId",
            table: "ProcurementReceiptInspectionCases");

        migrationBuilder.DropIndex(
            name: "IX_ProcurementReceiptInspectionCases_ReplacementPurchaseOrderReceiptId",
            table: "ProcurementReceiptInspectionCases");

        migrationBuilder.DropIndex(
            name: "IX_ProcurementReceiptInspectionCases_TenantId_ReplacementPurchaseOrderReceiptId",
            table: "ProcurementReceiptInspectionCases");

        migrationBuilder.DropColumn(
            name: "IdempotencyRequestHash",
            table: "GoodsReceiptNotes");

        migrationBuilder.DropColumn(
            name: "ReplacementInspectionCaseId",
            table: "ProcurementReceiptInspectionCases");

        migrationBuilder.DropColumn(
            name: "ReplacementLinkedAtUtc",
            table: "ProcurementReceiptInspectionCases");

        migrationBuilder.DropColumn(
            name: "ReplacementPurchaseOrderReceiptId",
            table: "ProcurementReceiptInspectionCases");
    }
}
