using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext)), Migration("20260930000700_GhanaStatutoryWhtFxEvidence")]
public partial class GhanaStatutoryWhtFxEvidence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "WithholdingTaxStatutoryExchangeRateId",
            table: "VendorPaymentAllocation",
            type: "uniqueidentifier",
            nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "WithholdingTaxStatutoryExchangeRate",
            table: "VendorPaymentAllocation",
            type: "decimal(18,6)",
            nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "WithholdingTaxStatutoryExchangeRateDate",
            table: "VendorPaymentAllocation",
            type: "datetime2",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "WithholdingTaxStatutoryExchangeRateSource",
            table: "VendorPaymentAllocation",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "WithholdingTaxStatutoryExchangeRateReference",
            table: "VendorPaymentAllocation",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.Sql("""
            SET ANSI_NULLS ON;
            SET QUOTED_IDENTIFIER ON;
            SET ANSI_PADDING ON;
            SET ANSI_WARNINGS ON;
            SET ARITHABORT ON;
            SET CONCAT_NULL_YIELDS_NULL ON;
            SET NUMERIC_ROUNDABORT OFF;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_VendorPaymentAllocation_TenantId_WithholdingTaxStatutoryExchangeRateId",
            table: "VendorPaymentAllocation",
            columns: new[] { "TenantId", "WithholdingTaxStatutoryExchangeRateId" },
            filter: "[WithholdingTaxStatutoryExchangeRateId] IS NOT NULL");
        migrationBuilder.AddForeignKey(
            name: "FK_VendorPaymentAllocation_ExchangeRates_WithholdingTaxStatutoryExchangeRateId",
            table: "VendorPaymentAllocation",
            column: "WithholdingTaxStatutoryExchangeRateId",
            principalTable: "ExchangeRates",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (
                SELECT 1
                FROM [dbo].[VendorPaymentAllocation]
                WHERE [WithholdingTaxStatutoryExchangeRateId] IS NOT NULL)
                THROW 51064, 'GHANA_STATUTORY_WHT_FX_DOWN_BLOCKED: retained statutory FX evidence exists.', 1;
            """);
        migrationBuilder.DropForeignKey(
            "FK_VendorPaymentAllocation_ExchangeRates_WithholdingTaxStatutoryExchangeRateId",
            "VendorPaymentAllocation");
        migrationBuilder.DropIndex(
            "IX_VendorPaymentAllocation_TenantId_WithholdingTaxStatutoryExchangeRateId",
            "VendorPaymentAllocation");
        migrationBuilder.DropColumn("WithholdingTaxStatutoryExchangeRateReference", "VendorPaymentAllocation");
        migrationBuilder.DropColumn("WithholdingTaxStatutoryExchangeRateSource", "VendorPaymentAllocation");
        migrationBuilder.DropColumn("WithholdingTaxStatutoryExchangeRateDate", "VendorPaymentAllocation");
        migrationBuilder.DropColumn("WithholdingTaxStatutoryExchangeRate", "VendorPaymentAllocation");
        migrationBuilder.DropColumn("WithholdingTaxStatutoryExchangeRateId", "VendorPaymentAllocation");
    }
}
