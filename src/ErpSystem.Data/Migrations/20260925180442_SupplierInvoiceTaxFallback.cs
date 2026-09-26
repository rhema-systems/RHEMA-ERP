using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class SupplierInvoiceTaxFallback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SupplierTaxFallbackAccountId",
                table: "VendorInvoice",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoice_SupplierTaxFallbackAccountId",
                table: "VendorInvoice",
                column: "SupplierTaxFallbackAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_VendorInvoice_Accounts_SupplierTaxFallbackAccountId",
                table: "VendorInvoice",
                column: "SupplierTaxFallbackAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM dbo.VendorInvoice WHERE SupplierTaxFallbackAccountId IS NOT NULL)
                    THROW 51729, 'Supplier invoice tax snapshots are in use; preserve them before rolling back this migration.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_VendorInvoice_Accounts_SupplierTaxFallbackAccountId",
                table: "VendorInvoice");

            migrationBuilder.DropIndex(
                name: "IX_VendorInvoice_SupplierTaxFallbackAccountId",
                table: "VendorInvoice");

            migrationBuilder.DropColumn(
                name: "SupplierTaxFallbackAccountId",
                table: "VendorInvoice");
        }
    }
}
