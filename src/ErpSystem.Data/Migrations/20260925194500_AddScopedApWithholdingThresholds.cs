using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext)), Migration("20260925194500_AddScopedApWithholdingThresholds")]
public partial class AddScopedApWithholdingThresholds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "WhtStatutoryYearStartMonth",
            table: "FinanceSettings",
            type: "int",
            nullable: false,
            defaultValue: 1);
        migrationBuilder.AddColumn<int>(
            name: "WhtStatutoryYearStartDay",
            table: "FinanceSettings",
            type: "int",
            nullable: false,
            defaultValue: 1);
        migrationBuilder.AddColumn<string>(
            name: "WithholdingContractReference",
            table: "VendorInvoice",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "WithholdingSupplyCategory",
            table: "VendorInvoice",
            type: "int",
            nullable: true);

        // Existing WHT invoices receive an isolated, auditable legacy scope. This avoids silently
        // combining unrelated historical engagements; Finance can correct draft invoices normally.
        migrationBuilder.Sql(@"
UPDATE [VendorInvoice]
SET [WithholdingContractReference] = UPPER(COALESCE(NULLIF(LTRIM(RTRIM([Reference])), ''), NULLIF(LTRIM(RTRIM([SupplierInvoiceNumber])), ''), [InvoiceNumber])),
    [WithholdingSupplyCategory] = CASE [AcceptedSupplyKind] WHEN 1 THEN 0 WHEN 3 THEN 1 ELSE 2 END
WHERE [WithholdingTaxId] IS NOT NULL;");

        migrationBuilder.AddCheckConstraint(
            name: "CK_FinanceSettings_WhtStatutoryYearStart",
            table: "FinanceSettings",
            sql: "[WhtStatutoryYearStartMonth] BETWEEN 1 AND 12 AND [WhtStatutoryYearStartDay] BETWEEN 1 AND DAY(EOMONTH(DATEFROMPARTS(2001, [WhtStatutoryYearStartMonth], 1)))");
        migrationBuilder.AddCheckConstraint(
            name: "CK_VendorInvoice_WhtScopeCoherent",
            table: "VendorInvoice",
            sql: "([WithholdingTaxId] IS NULL AND [WithholdingContractReference] IS NULL AND [WithholdingSupplyCategory] IS NULL) OR ([WithholdingTaxId] IS NOT NULL AND LEN([WithholdingContractReference]) BETWEEN 1 AND 100 AND [WithholdingSupplyCategory] BETWEEN 0 AND 2)");
        migrationBuilder.CreateIndex(
            name: "IX_VendorInvoice_WhtStatutoryScope",
            table: "VendorInvoice",
            columns: new[] { "TenantId", "BusinessPartnerId", "WithholdingTaxId", "WithholdingContractReference", "WithholdingSupplyCategory" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_VendorInvoice_WhtStatutoryScope", table: "VendorInvoice");
        migrationBuilder.DropCheckConstraint(name: "CK_VendorInvoice_WhtScopeCoherent", table: "VendorInvoice");
        migrationBuilder.DropCheckConstraint(name: "CK_FinanceSettings_WhtStatutoryYearStart", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "WithholdingContractReference", table: "VendorInvoice");
        migrationBuilder.DropColumn(name: "WithholdingSupplyCategory", table: "VendorInvoice");
        migrationBuilder.DropColumn(name: "WhtStatutoryYearStartMonth", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "WhtStatutoryYearStartDay", table: "FinanceSettings");
    }
}
