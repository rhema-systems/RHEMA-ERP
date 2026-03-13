using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class _20260225_AddConsignmentBinsToWarehouseLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConsignmentWarehouseId",
                table: "WarehouseLocations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsConsignmentBin",
                table: "WarehouseLocations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLocations_ConsignmentWarehouseId",
                table: "WarehouseLocations",
                column: "ConsignmentWarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseLocations_Warehouses_ConsignmentWarehouseId",
                table: "WarehouseLocations",
                column: "ConsignmentWarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseLocations_Warehouses_ConsignmentWarehouseId",
                table: "WarehouseLocations");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseLocations_ConsignmentWarehouseId",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "ConsignmentWarehouseId",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "IsConsignmentBin",
                table: "WarehouseLocations");
        }
    }
}
