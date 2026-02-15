using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260203200000_AddPurchaseOrderMiscellaneousCostAndAllocationFlag")]
    public partial class AddPurchaseOrderMiscellaneousCostAndAllocationFlag : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CostsAllocated",
                table: "PurchaseOrders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MiscellaneousCost",
                table: "PurchaseOrders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAdditionalCost",
                table: "PurchaseOrders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostsAllocated",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "MiscellaneousCost",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "TotalAdditionalCost",
                table: "PurchaseOrders");
        }
    }
}
