using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <inheritdoc />
public partial class AddUomSnapshotToPurchaseOrderReceiptItems : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ItemUnitOfMeasureId",
            table: "PurchaseOrderReceiptItems",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "UnitOfMeasure",
            table: "PurchaseOrderReceiptItems",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ItemUnitOfMeasureId",
            table: "PurchaseOrderReceiptItems");

        migrationBuilder.DropColumn(
            name: "UnitOfMeasure",
            table: "PurchaseOrderReceiptItems");
    }
}

