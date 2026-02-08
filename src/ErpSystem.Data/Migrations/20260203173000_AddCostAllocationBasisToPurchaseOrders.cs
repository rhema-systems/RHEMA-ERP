using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260203173000_AddCostAllocationBasisToPurchaseOrders")]
    public partial class AddCostAllocationBasisToPurchaseOrders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CostAllocationMethod",
                table: "PurchaseOrders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "SpreadToItemCost");

            migrationBuilder.AddColumn<string>(
                name: "CostApportionmentBasis",
                table: "PurchaseOrders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Value");

            migrationBuilder.AddColumn<string>(
                name: "ExpenseGLAccount",
                table: "PurchaseOrders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AllocatedAdditionalCost",
                table: "PurchaseOrderItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AllocatedCostPerUnit",
                table: "PurchaseOrderItems",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LandedUnitCost",
                table: "PurchaseOrderItems",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CostApportionmentBasis",
                table: "InventoryTransfers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Value");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostAllocationMethod",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "CostApportionmentBasis",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ExpenseGLAccount",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "AllocatedAdditionalCost",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "AllocatedCostPerUnit",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "LandedUnitCost",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "CostApportionmentBasis",
                table: "InventoryTransfers");
        }
    }
}
