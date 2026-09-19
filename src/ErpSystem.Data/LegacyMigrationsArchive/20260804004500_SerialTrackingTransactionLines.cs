using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260804004500_SerialTrackingTransactionLines")]
public sealed class SerialTrackingTransactionLines : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_InventoryIssueVoucherLines_TenantId_InventoryIssueVoucherId_InventoryRequisitionItemId",
            table: "InventoryIssueVoucherLines");

        migrationBuilder.DropIndex(
            name: "IX_InventoryReturnVoucherLines_InventoryReturnVoucherId_InventoryRequisitionItemId",
            table: "InventoryReturnVoucherLines");

        // SQL Server conventionally filters unique indexes containing nullable columns.
        // These indexes must remain unfiltered so an exact duplicate tracking line is
        // rejected even when one or more optional identity values are null.
        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX [UX_InventoryIssueVoucherLines_Tracking]
            ON [InventoryIssueVoucherLines]
            ([TenantId], [InventoryIssueVoucherId], [InventoryRequisitionItemId], [LocationId], [LotNumber], [BatchNumber], [SerialNumber]);
            """);

        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX [UX_InventoryReturnVoucherLines_Tracking]
            ON [InventoryReturnVoucherLines]
            ([InventoryReturnVoucherId], [InventoryRequisitionItemId], [LocationId], [LotNumber], [BatchNumber], [SerialNumber]);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "UX_InventoryIssueVoucherLines_Tracking",
            table: "InventoryIssueVoucherLines");

        migrationBuilder.DropIndex(
            name: "UX_InventoryReturnVoucherLines_Tracking",
            table: "InventoryReturnVoucherLines");

        migrationBuilder.CreateIndex(
            name: "IX_InventoryIssueVoucherLines_TenantId_InventoryIssueVoucherId_InventoryRequisitionItemId",
            table: "InventoryIssueVoucherLines",
            columns: new[] { "TenantId", "InventoryIssueVoucherId", "InventoryRequisitionItemId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_InventoryReturnVoucherLines_InventoryReturnVoucherId_InventoryRequisitionItemId",
            table: "InventoryReturnVoucherLines",
            columns: new[] { "InventoryReturnVoucherId", "InventoryRequisitionItemId" },
            unique: true);
    }
}
