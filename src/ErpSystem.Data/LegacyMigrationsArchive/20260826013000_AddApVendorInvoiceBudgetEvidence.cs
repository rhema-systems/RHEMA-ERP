using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260826013000_AddApVendorInvoiceBudgetEvidence")]
public sealed class AddApVendorInvoiceBudgetEvidence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "BudgetEntryId",
            table: "VendorInvoiceLineItem",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_VendorInvoiceLineItem_BudgetEntryId",
            table: "VendorInvoiceLineItem",
            column: "BudgetEntryId");

        migrationBuilder.CreateIndex(
            name: "IX_VendorInvoiceLineItem_TenantId_BudgetEntryId",
            table: "VendorInvoiceLineItem",
            columns: new[] { "TenantId", "BudgetEntryId" });

        migrationBuilder.AddForeignKey(
            name: "FK_VendorInvoiceLineItem_BudgetEntries_BudgetEntryId",
            table: "VendorInvoiceLineItem",
            column: "BudgetEntryId",
            principalTable: "BudgetEntries",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_VendorInvoiceLineItem_BudgetEntries_BudgetEntryId",
            table: "VendorInvoiceLineItem");

        migrationBuilder.DropIndex(
            name: "IX_VendorInvoiceLineItem_BudgetEntryId",
            table: "VendorInvoiceLineItem");

        migrationBuilder.DropIndex(
            name: "IX_VendorInvoiceLineItem_TenantId_BudgetEntryId",
            table: "VendorInvoiceLineItem");

        migrationBuilder.DropColumn(
            name: "BudgetEntryId",
            table: "VendorInvoiceLineItem");
    }
}
