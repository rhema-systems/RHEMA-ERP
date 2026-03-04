using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryLayerLinkToLandedCostAllocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InventoryLayerId",
                table: "LandedCostAllocations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostAllocations_InventoryLayerId",
                table: "LandedCostAllocations",
                column: "InventoryLayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_LandedCostAllocations_InventoryLayers_InventoryLayerId",
                table: "LandedCostAllocations",
                column: "InventoryLayerId",
                principalTable: "InventoryLayers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LandedCostAllocations_InventoryLayers_InventoryLayerId",
                table: "LandedCostAllocations");

            migrationBuilder.DropIndex(
                name: "IX_LandedCostAllocations_InventoryLayerId",
                table: "LandedCostAllocations");

            migrationBuilder.DropColumn(
                name: "InventoryLayerId",
                table: "LandedCostAllocations");
        }
    }
}
