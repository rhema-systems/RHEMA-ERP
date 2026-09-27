using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260924190000_ReceiptItemWeightSnapshots")]
public sealed class ReceiptItemWeightSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<decimal>(name: "Weight", table: "InventoryItems", type: "decimal(22,6)", nullable: true,
            oldClrType: typeof(decimal), oldType: "decimal(18,4)", oldNullable: true);
        // Do not guess the unit of historical item weights or backfill receipts from today's master.
        migrationBuilder.AddColumn<string>(name: "WeightUnit", table: "InventoryItems", type: "nvarchar(2)", maxLength: 2, nullable: true);
        foreach (var table in new[] { "PurchaseOrderReceiptItems", "GoodsReceiptNoteItems" })
        {
            migrationBuilder.AddColumn<decimal>(name: "UnitWeightKg", table: table, type: "decimal(22,6)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "WeightStockUom", table: table, type: "nvarchar(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<bool>(name: "WeightOverridden", table: table, type: "bit", nullable: false, defaultValue: false);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM [InventoryItems] WHERE [Weight] <> ROUND([Weight], 4) OR ABS([Weight]) > 99999999999999.9999)
                THROW 51710, 'Item weights exceed the previous precision. Export and reconcile these values before rollback.', 1;
            """);
        foreach (var table in new[] { "PurchaseOrderReceiptItems", "GoodsReceiptNoteItems" })
        {
            migrationBuilder.DropColumn(name: "UnitWeightKg", table: table);
            migrationBuilder.DropColumn(name: "WeightStockUom", table: table);
            migrationBuilder.DropColumn(name: "WeightOverridden", table: table);
        }
        migrationBuilder.DropColumn(name: "WeightUnit", table: "InventoryItems");
        migrationBuilder.AlterColumn<decimal>(name: "Weight", table: "InventoryItems", type: "decimal(18,4)", nullable: true,
            oldClrType: typeof(decimal), oldType: "decimal(22,6)", oldNullable: true);
    }
}
