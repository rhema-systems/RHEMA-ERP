using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260729130000_AddTaxConfigurationVersionsAndDirectionalFxPolicy")]
public partial class AddTaxConfigurationVersionsAndDirectionalFxPolicy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ExchangeRates_TenantId_BaseCurrencyCode_TargetCurrencyCode_RateType_EffectiveDate",
            table: "ExchangeRates");

        migrationBuilder.DropIndex(
            name: "IX_ExchangeRates_TenantId_BaseCurrencyCode_TargetCurrencyCode_RateType_IsActive",
            table: "ExchangeRates");

        migrationBuilder.AddColumn<int>(
            name: "QuoteSide",
            table: "ExchangeRates",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "TransactionQuoteSide",
            table: "AccountCurrencyLinks",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "RevaluationQuoteSide",
            table: "AccountCurrencyLinks",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<bool>(
            name: "DirectionalExchangeRatePolicyEnabled",
            table: "FinanceSettings",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "DefaultTransactionQuoteSide",
            table: "FinanceSettings",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "ArInvoiceQuoteSide",
            table: "FinanceSettings",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "ArSettlementQuoteSide",
            table: "FinanceSettings",
            type: "int",
            nullable: false,
            defaultValue: 2);

        migrationBuilder.AddColumn<int>(
            name: "ApInvoiceQuoteSide",
            table: "FinanceSettings",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "ApSettlementQuoteSide",
            table: "FinanceSettings",
            type: "int",
            nullable: false,
            defaultValue: 3);

        migrationBuilder.AddColumn<int>(
            name: "ClosingQuoteSide",
            table: "FinanceSettings",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<bool>(
            name: "RequireExchangeRateOverrideApproval",
            table: "FinanceSettings",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.CreateTable(
            name: "TaxConfigurationVersions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TaxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VersionNumber = table.Column<int>(type: "int", nullable: false),
                Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Rate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                Applicability = table.Column<int>(type: "int", nullable: false),
                Category = table.Column<int>(type: "int", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                IsInputTaxDeductible = table.Column<bool>(type: "bit", nullable: false),
                ThresholdAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                TaxPayableAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                TaxReceivableAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                ValidTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                ChangeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TaxConfigurationVersions", x => x.Id);
                table.ForeignKey(
                    name: "FK_TaxConfigurationVersions_Taxes_TaxId",
                    column: x => x.TaxId,
                    principalTable: "Taxes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_TaxConfigurationVersions_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ExchangeRates_TenantId_BaseCurrencyCode_TargetCurrencyCode_RateType_QuoteSide_EffectiveDate",
            table: "ExchangeRates",
            columns: new[] { "TenantId", "BaseCurrencyCode", "TargetCurrencyCode", "RateType", "QuoteSide", "EffectiveDate" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_ExchangeRates_TenantId_BaseCurrencyCode_TargetCurrencyCode_RateType_QuoteSide_IsActive",
            table: "ExchangeRates",
            columns: new[] { "TenantId", "BaseCurrencyCode", "TargetCurrencyCode", "RateType", "QuoteSide", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_TaxConfigurationVersions_TaxId",
            table: "TaxConfigurationVersions",
            column: "TaxId");

        migrationBuilder.CreateIndex(
            name: "IX_TaxConfigurationVersions_TenantId",
            table: "TaxConfigurationVersions",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_TaxConfigurationVersions_TenantId_TaxId_VersionNumber",
            table: "TaxConfigurationVersions",
            columns: new[] { "TenantId", "TaxId", "VersionNumber" },
            unique: true,
            filter: "[IsDeleted] = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "TaxConfigurationVersions");

        migrationBuilder.DropIndex(
            name: "IX_ExchangeRates_TenantId_BaseCurrencyCode_TargetCurrencyCode_RateType_QuoteSide_EffectiveDate",
            table: "ExchangeRates");

        migrationBuilder.DropIndex(
            name: "IX_ExchangeRates_TenantId_BaseCurrencyCode_TargetCurrencyCode_RateType_QuoteSide_IsActive",
            table: "ExchangeRates");

        migrationBuilder.DropColumn(name: "QuoteSide", table: "ExchangeRates");
        migrationBuilder.DropColumn(name: "TransactionQuoteSide", table: "AccountCurrencyLinks");
        migrationBuilder.DropColumn(name: "RevaluationQuoteSide", table: "AccountCurrencyLinks");
        migrationBuilder.DropColumn(name: "DirectionalExchangeRatePolicyEnabled", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "DefaultTransactionQuoteSide", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "ArInvoiceQuoteSide", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "ArSettlementQuoteSide", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "ApInvoiceQuoteSide", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "ApSettlementQuoteSide", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "ClosingQuoteSide", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "RequireExchangeRateOverrideApproval", table: "FinanceSettings");

        migrationBuilder.CreateIndex(
            name: "IX_ExchangeRates_TenantId_BaseCurrencyCode_TargetCurrencyCode_RateType_EffectiveDate",
            table: "ExchangeRates",
            columns: new[] { "TenantId", "BaseCurrencyCode", "TargetCurrencyCode", "RateType", "EffectiveDate" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_ExchangeRates_TenantId_BaseCurrencyCode_TargetCurrencyCode_RateType_IsActive",
            table: "ExchangeRates",
            columns: new[] { "TenantId", "BaseCurrencyCode", "TargetCurrencyCode", "RateType", "IsActive" });
    }
}
