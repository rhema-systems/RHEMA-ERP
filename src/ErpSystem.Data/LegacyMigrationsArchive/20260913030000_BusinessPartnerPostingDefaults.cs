using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260913030000_BusinessPartnerPostingDefaults")]
public sealed class BusinessPartnerPostingDefaults : Migration
{
    private static readonly string[] AccountColumns =
    {
        "DefaultCashAccountId",
        "DefaultTermsDiscountsAvailableAccountId",
        "DefaultTermsDiscountsTakenAccountId",
        "DefaultFinanceChargesAccountId",
        "DefaultTradeDiscountAccountId",
        "DefaultMiscellaneousAccountId",
        "DefaultFreightAccountId",
        "DefaultTaxAccountId",
        "DefaultWriteoffAccountId",
        "DefaultAccruedPurchasesAccountId",
        "DefaultPurchasePriceVarianceAccountId",
    };

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("SubjectToWithholdingDeduction", "BusinessPartners", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<decimal>("WithholdingTaxRate", "BusinessPartners", type: "decimal(18,4)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<string>("CashAccountSource", "BusinessPartners", type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Chequebook");
        migrationBuilder.AddColumn<string>("SupplierDefaultsSnapshotJson", "PurchaseOrders", type: "nvarchar(max)", nullable: true);
        AddLink(migrationBuilder, "DefaultTaxGroupId", "TaxGroups");
        AddLink(migrationBuilder, "DefaultBankAccountId", "BankAccounts");
        foreach (var column in AccountColumns) AddLink(migrationBuilder, column, "Accounts");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var column in AccountColumns) DropLink(migrationBuilder, column, "Accounts");
        DropLink(migrationBuilder, "DefaultTaxGroupId", "TaxGroups");
        DropLink(migrationBuilder, "DefaultBankAccountId", "BankAccounts");
        migrationBuilder.DropColumn("SupplierDefaultsSnapshotJson", "PurchaseOrders");
        migrationBuilder.DropColumn("SubjectToWithholdingDeduction", "BusinessPartners");
        migrationBuilder.DropColumn("WithholdingTaxRate", "BusinessPartners");
        migrationBuilder.DropColumn("CashAccountSource", "BusinessPartners");
    }

    private static void AddLink(MigrationBuilder migrationBuilder, string column, string principal)
    {
        migrationBuilder.AddColumn<Guid>(column, "BusinessPartners", nullable: true);
        migrationBuilder.CreateIndex($"IX_BusinessPartners_{column}", "BusinessPartners", column);
        migrationBuilder.AddForeignKey($"FK_BusinessPartners_{principal}_{column}", "BusinessPartners", column,
            principal, principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    private static void DropLink(MigrationBuilder migrationBuilder, string column, string principal)
    {
        migrationBuilder.DropForeignKey($"FK_BusinessPartners_{principal}_{column}", "BusinessPartners");
        migrationBuilder.DropIndex($"IX_BusinessPartners_{column}", "BusinessPartners");
        migrationBuilder.DropColumn(column, "BusinessPartners");
    }
}
