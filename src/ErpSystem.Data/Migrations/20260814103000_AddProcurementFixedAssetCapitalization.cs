using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Persists FIN-INT-007 evidence separately from Procurement workflow tables. This is a focused
/// manual migration because the shared multi-module snapshot contains unrelated model drift; the
/// runtime model is configured in ApplicationDbContext and migration behavior is regression tested.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260814103000_AddProcurementFixedAssetCapitalization")]
public class AddProcurementFixedAssetCapitalization : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProcurementFixedAssetCapitalizations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FixedAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AcceptedSupplyKind = table.Column<int>(type: "int", nullable: false),
                AcceptedSupplySourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AcceptedSupplyReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PurchaseOrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CapitalizedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                SourceCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                SourceTransactionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                FunctionalCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                FunctionalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                SourceIntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                SourceSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ReceiptPostingEvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CapitalizationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                PostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ReversalPostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReversalJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReversedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                FailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProcurementFixedAssetCapitalizations", value => value.Id);
                table.CheckConstraint("CK_ProcurementFixedAssetCapitalizations_Quantity", "[CapitalizedQuantity] > 0");
                table.CheckConstraint("CK_ProcurementFixedAssetCapitalizations_Amounts", "[SourceTransactionAmount] >= 0 AND [FunctionalAmount] > 0");
                table.ForeignKey("FK_ProcurementFixedAssetCapitalizations_Tenants_TenantId", value => value.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementFixedAssetCapitalizations_FixedAssets_FixedAssetId", value => value.FixedAssetId, "FixedAssets", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementFixedAssetCapitalizations_PurchaseOrders_PurchaseOrderId", value => value.PurchaseOrderId, "PurchaseOrders", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementFixedAssetCapitalizations_PurchaseOrderItems_PurchaseOrderItemId", value => value.PurchaseOrderItemId, "PurchaseOrderItems", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementFixedAssetCapitalizations_InventoryItems_InventoryItemId", value => value.InventoryItemId, "InventoryItems", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementFixedAssetCapitalizations_FinancePostingEvents_PostingEventId", value => value.PostingEventId, "FinancePostingEvents", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementFixedAssetCapitalizations_JournalEntries_JournalEntryId", value => value.JournalEntryId, "JournalEntries", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementFixedAssetCapitalizations_FinancePostingEvents_ReversalPostingEventId", value => value.ReversalPostingEventId, "FinancePostingEvents", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementFixedAssetCapitalizations_JournalEntries_ReversalJournalEntryId", value => value.ReversalJournalEntryId, "JournalEntries", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProcurementFixedAssetCapitalizations_TenantId_IdempotencyKey",
            table: "ProcurementFixedAssetCapitalizations",
            columns: new[] { "TenantId", "IdempotencyKey" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_ProcurementFixedAssetCapitalizations_TenantId_PurchaseOrderItemId_Status",
            table: "ProcurementFixedAssetCapitalizations",
            columns: new[] { "TenantId", "PurchaseOrderItemId", "Status" });
        migrationBuilder.CreateIndex(
            name: "IX_ProcurementFixedAssetCapitalizations_TenantId_FixedAssetId_Status",
            table: "ProcurementFixedAssetCapitalizations",
            columns: new[] { "TenantId", "FixedAssetId", "Status" });
        migrationBuilder.CreateIndex(
            name: "IX_ProcurementFixedAssetCapitalizations_TenantId_PostingEventId",
            table: "ProcurementFixedAssetCapitalizations",
            columns: new[] { "TenantId", "PostingEventId" });
        migrationBuilder.CreateIndex(
            name: "IX_ProcurementFixedAssetCapitalizations_TenantId_JournalEntryId",
            table: "ProcurementFixedAssetCapitalizations",
            columns: new[] { "TenantId", "JournalEntryId" });

        foreach (var column in ForeignKeyColumns)
            migrationBuilder.CreateIndex($"IX_ProcurementFixedAssetCapitalizations_{column}", "ProcurementFixedAssetCapitalizations", column);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable("ProcurementFixedAssetCapitalizations");

    private static readonly string[] ForeignKeyColumns =
    [
        "FixedAssetId", "PurchaseOrderId", "PurchaseOrderItemId", "InventoryItemId", "PostingEventId",
        "JournalEntryId", "ReversalPostingEventId", "ReversalJournalEntryId"
    ];
}
