using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
public partial class AddPurchaseOrderReceiptNumberFormatToProcurementSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PurchaseOrderReceiptNumberFormat",
            table: "ProcurementSettings",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PurchaseOrderReceiptNumberFormat",
            table: "ProcurementSettings");
    }
}
}
