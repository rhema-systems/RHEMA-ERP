using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260801220000_TDC0502ReceiptInspectionActionFingerprints")]
public sealed class TDC0502ReceiptInspectionActionFingerprints : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RequestFingerprint",
            table: "ProcurementReceiptInspectionActions",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_ProcurementReceiptInspectionActions_RequestFingerprint",
            table: "ProcurementReceiptInspectionActions",
            sql: "[RequestFingerprint] IS NULL OR LEN([RequestFingerprint]) = 64");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_ProcurementReceiptInspectionActions_RequestFingerprint",
            table: "ProcurementReceiptInspectionActions");

        migrationBuilder.DropColumn(
            name: "RequestFingerprint",
            table: "ProcurementReceiptInspectionActions");
    }
}
