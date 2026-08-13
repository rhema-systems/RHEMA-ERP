using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Resolves FIN-LIM-0040 by retaining the canonical customer, statutory sales-tax decision and
/// linked AR/Cash settlement documents on each fixed-asset sale. This is a focused manual
/// migration because the shared multi-module snapshot currently contains unrelated model drift;
/// generating from that snapshot would incorrectly attempt to recreate existing application tables.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260813103000_AddFixedAssetDisposalSettlement")]
public class AddFixedAssetDisposalSettlement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("BuyerBusinessPartnerId", "AssetDisposals", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<int>("SettlementMode", "AssetDisposals", "int", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>("SettlementStatus", "AssetDisposals", "int", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<Guid>("SaleTaxGroupId", "AssetDisposals", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<int>("SaleTaxTreatment", "AssetDisposals", "int", nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<Guid>("SettlementPaymentTermId", "AssetDisposals", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("SettlementPaymentMethodId", "AssetDisposals", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("SettlementBankAccountId", "AssetDisposals", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("SettlementLiquidityAccountId", "AssetDisposals", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>("SettlementReference", "AssetDisposals", "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<Guid>("CustomerInvoiceId", "AssetDisposals", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("CustomerPaymentId", "AssetDisposals", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<decimal>("SettlementInvoiceAmount", "AssetDisposals", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("SettlementTaxAmount", "AssetDisposals", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<DateTime>("SettlementCompletedAt", "AssetDisposals", "datetime2", nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_AssetDisposals_TenantId_BuyerBusinessPartnerId",
            table: "AssetDisposals",
            columns: new[] { "TenantId", "BuyerBusinessPartnerId" });
        migrationBuilder.CreateIndex("IX_AssetDisposals_BuyerBusinessPartnerId", "AssetDisposals", "BuyerBusinessPartnerId");
        migrationBuilder.CreateIndex(
            name: "IX_AssetDisposals_TenantId_CustomerInvoiceId",
            table: "AssetDisposals",
            columns: new[] { "TenantId", "CustomerInvoiceId" },
            unique: true,
            filter: "[CustomerInvoiceId] IS NOT NULL");
        migrationBuilder.CreateIndex(
            name: "IX_AssetDisposals_TenantId_CustomerPaymentId",
            table: "AssetDisposals",
            columns: new[] { "TenantId", "CustomerPaymentId" },
            unique: true,
            filter: "[CustomerPaymentId] IS NOT NULL");
        // The tenant-scoped unique indexes protect document ownership. These single-column indexes
        // additionally support SQL Server's FK maintenance paths, matching EF's relationship-index
        // convention without sacrificing the tenant-aware lookup shapes used by Finance screens.
        migrationBuilder.CreateIndex("IX_AssetDisposals_CustomerInvoiceId", "AssetDisposals", "CustomerInvoiceId");
        migrationBuilder.CreateIndex("IX_AssetDisposals_CustomerPaymentId", "AssetDisposals", "CustomerPaymentId");
        migrationBuilder.CreateIndex("IX_AssetDisposals_SaleTaxGroupId", "AssetDisposals", "SaleTaxGroupId");
        migrationBuilder.CreateIndex("IX_AssetDisposals_SettlementPaymentTermId", "AssetDisposals", "SettlementPaymentTermId");
        migrationBuilder.CreateIndex("IX_AssetDisposals_SettlementPaymentMethodId", "AssetDisposals", "SettlementPaymentMethodId");
        migrationBuilder.CreateIndex("IX_AssetDisposals_SettlementBankAccountId", "AssetDisposals", "SettlementBankAccountId");
        migrationBuilder.CreateIndex("IX_AssetDisposals_SettlementLiquidityAccountId", "AssetDisposals", "SettlementLiquidityAccountId");

        AddRestrictedForeignKey(migrationBuilder, "BuyerBusinessPartnerId", "BusinessPartners");
        AddRestrictedForeignKey(migrationBuilder, "SaleTaxGroupId", "TaxGroups");
        AddRestrictedForeignKey(migrationBuilder, "SettlementPaymentTermId", "PaymentTerms");
        AddRestrictedForeignKey(migrationBuilder, "SettlementPaymentMethodId", "PaymentMethods");
        AddRestrictedForeignKey(migrationBuilder, "SettlementBankAccountId", "BankAccounts");
        AddRestrictedForeignKey(migrationBuilder, "SettlementLiquidityAccountId", "LiquidityAccounts");
        AddRestrictedForeignKey(migrationBuilder, "CustomerInvoiceId", "Invoices");
        AddRestrictedForeignKey(migrationBuilder, "CustomerPaymentId", "CustomerPayments");

        // Persisted state rules prevent direct SQL or a future code path from linking both a bank
        // and a holding account, or from marking a non-sale disposal as settled without an invoice.
        migrationBuilder.AddCheckConstraint(
            name: "CK_AssetDisposals_SettlementDestination",
            table: "AssetDisposals",
            sql: "[SettlementBankAccountId] IS NULL OR [SettlementLiquidityAccountId] IS NULL");
        migrationBuilder.AddCheckConstraint(
            name: "CK_AssetDisposals_SettlementDocumentState",
            table: "AssetDisposals",
            sql: "([SettlementStatus] IN (0,1,4)) OR ([SettlementStatus] IN (2,3) AND [CustomerInvoiceId] IS NOT NULL)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_AssetDisposals_SettlementDocumentState", "AssetDisposals");
        migrationBuilder.DropCheckConstraint("CK_AssetDisposals_SettlementDestination", "AssetDisposals");

        foreach (var column in ForeignKeyColumns)
        {
            migrationBuilder.DropForeignKey($"FK_AssetDisposals_{PrincipalTables[column]}_{column}", "AssetDisposals");
        }

        migrationBuilder.DropIndex("IX_AssetDisposals_TenantId_BuyerBusinessPartnerId", "AssetDisposals");
        migrationBuilder.DropIndex("IX_AssetDisposals_TenantId_CustomerInvoiceId", "AssetDisposals");
        migrationBuilder.DropIndex("IX_AssetDisposals_TenantId_CustomerPaymentId", "AssetDisposals");
        migrationBuilder.DropIndex("IX_AssetDisposals_BuyerBusinessPartnerId", "AssetDisposals");
        migrationBuilder.DropIndex("IX_AssetDisposals_CustomerInvoiceId", "AssetDisposals");
        migrationBuilder.DropIndex("IX_AssetDisposals_CustomerPaymentId", "AssetDisposals");
        migrationBuilder.DropIndex("IX_AssetDisposals_SaleTaxGroupId", "AssetDisposals");
        migrationBuilder.DropIndex("IX_AssetDisposals_SettlementPaymentTermId", "AssetDisposals");
        migrationBuilder.DropIndex("IX_AssetDisposals_SettlementPaymentMethodId", "AssetDisposals");
        migrationBuilder.DropIndex("IX_AssetDisposals_SettlementBankAccountId", "AssetDisposals");
        migrationBuilder.DropIndex("IX_AssetDisposals_SettlementLiquidityAccountId", "AssetDisposals");

        foreach (var column in NewColumns)
        {
            migrationBuilder.DropColumn(column, "AssetDisposals");
        }
    }

    private static void AddRestrictedForeignKey(MigrationBuilder migrationBuilder, string column, string principalTable)
        => migrationBuilder.AddForeignKey(
            name: $"FK_AssetDisposals_{principalTable}_{column}",
            table: "AssetDisposals",
            column: column,
            principalTable: principalTable,
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

    private static readonly string[] ForeignKeyColumns =
    {
        "BuyerBusinessPartnerId", "SaleTaxGroupId", "SettlementPaymentTermId", "SettlementPaymentMethodId",
        "SettlementBankAccountId", "SettlementLiquidityAccountId", "CustomerInvoiceId", "CustomerPaymentId"
    };

    private static readonly IReadOnlyDictionary<string, string> PrincipalTables = new Dictionary<string, string>
    {
        ["BuyerBusinessPartnerId"] = "BusinessPartners",
        ["SaleTaxGroupId"] = "TaxGroups",
        ["SettlementPaymentTermId"] = "PaymentTerms",
        ["SettlementPaymentMethodId"] = "PaymentMethods",
        ["SettlementBankAccountId"] = "BankAccounts",
        ["SettlementLiquidityAccountId"] = "LiquidityAccounts",
        ["CustomerInvoiceId"] = "Invoices",
        ["CustomerPaymentId"] = "CustomerPayments"
    };

    private static readonly string[] NewColumns =
    {
        "BuyerBusinessPartnerId", "SettlementMode", "SettlementStatus", "SaleTaxGroupId", "SaleTaxTreatment",
        "SettlementPaymentTermId", "SettlementPaymentMethodId", "SettlementBankAccountId",
        "SettlementLiquidityAccountId", "SettlementReference", "CustomerInvoiceId", "CustomerPaymentId",
        "SettlementInvoiceAmount", "SettlementTaxAmount", "SettlementCompletedAt"
    };
}
