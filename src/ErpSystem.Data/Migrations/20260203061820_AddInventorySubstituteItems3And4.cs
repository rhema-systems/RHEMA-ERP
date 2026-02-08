using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInventorySubstituteItems3And4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SubstituteItem3Id",
                table: "InventoryItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubstituteItem4Id",
                table: "InventoryItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_SubstituteItem3Id",
                table: "InventoryItems",
                column: "SubstituteItem3Id");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_SubstituteItem4Id",
                table: "InventoryItems",
                column: "SubstituteItem4Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_InventoryItems_SubstituteItem3Id",
                table: "InventoryItems",
                column: "SubstituteItem3Id",
                principalTable: "InventoryItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_InventoryItems_SubstituteItem4Id",
                table: "InventoryItems",
                column: "SubstituteItem4Id",
                principalTable: "InventoryItems",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_InventoryItems_SubstituteItem3Id",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_InventoryItems_SubstituteItem4Id",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_SubstituteItem3Id",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_SubstituteItem4Id",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "SubstituteItem3Id",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "SubstituteItem4Id",
                table: "InventoryItems");
        }
    }
}
