using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260801123000_AddPurchaseOrderCancellationTimestamp")]
public sealed class AddPurchaseOrderCancellationTimestamp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "CancelledAtUtc",
            table: "PurchaseOrders",
            type: "datetime2",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE [dbo].[PurchaseOrders]
            SET [CancelledAtUtc] = COALESCE([UpdatedAt], [CreatedAt], [OrderDate])
            WHERE [Status] = N'Cancelled' AND [CancelledAtUtc] IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CancelledAtUtc",
            table: "PurchaseOrders");
    }
}
