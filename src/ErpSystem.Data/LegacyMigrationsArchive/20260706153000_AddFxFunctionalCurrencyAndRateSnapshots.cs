using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260706153000_AddFxFunctionalCurrencyAndRateSnapshots")]
    public partial class AddFxFunctionalCurrencyAndRateSnapshots : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [FinanceSettings]
                SET [BaseCurrency] = CASE
                    WHEN [BaseCurrency] IS NULL OR LTRIM(RTRIM([BaseCurrency])) = N'' THEN N'GHS'
                    ELSE UPPER(LEFT(LTRIM(RTRIM([BaseCurrency])), 3))
                END;
                """);

            migrationBuilder.DropIndex(
                name: "IX_FinanceSettings_TenantId",
                table: "FinanceSettings");

            migrationBuilder.AlterColumn<string>(
                name: "BaseCurrency",
                table: "FinanceSettings",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<bool>(
                name: "FunctionalCurrencyLocked",
                table: "FinanceSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FunctionalCurrencyLockedAt",
                table: "FinanceSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FunctionalCurrencyLockedReason",
                table: "FinanceSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasForeignCurrencyLines",
                table: "FinancePostingEvents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PrimaryExchangeRate",
                table: "FinancePostingEvents",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PrimaryExchangeRateDate",
                table: "FinancePostingEvents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryExchangeRateId",
                table: "FinancePostingEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryTransactionCurrencyCode",
                table: "FinancePostingEvents",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ExchangeRate",
                table: "AccountTransactions",
                type: "decimal(18,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExchangeRateId",
                table: "AccountTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FunctionalCurrencyCode",
                table: "AccountTransactions",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "GHS");

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionCreditAmount",
                table: "AccountTransactions",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionDebitAmount",
                table: "AccountTransactions",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_TenantId",
                table: "FinanceSettings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancePostingEvents_PrimaryExchangeRateId",
                table: "FinancePostingEvents",
                column: "PrimaryExchangeRateId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePostingEvents_TenantId_HasForeignCurrencyLines",
                table: "FinancePostingEvents",
                columns: new[] { "TenantId", "HasForeignCurrencyLines" });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_HasBeenUsedInTransactions",
                table: "ExchangeRates",
                column: "HasBeenUsedInTransactions");

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

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_ExchangeRateId",
                table: "AccountTransactions",
                column: "ExchangeRateId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_TenantId_TransactionCurrency_ExchangeRateId",
                table: "AccountTransactions",
                columns: new[] { "TenantId", "TransactionCurrency", "ExchangeRateId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AccountTransactions_ExchangeRates_ExchangeRateId",
                table: "AccountTransactions",
                column: "ExchangeRateId",
                principalTable: "ExchangeRates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FinancePostingEvents_ExchangeRates_PrimaryExchangeRateId",
                table: "FinancePostingEvents",
                column: "PrimaryExchangeRateId",
                principalTable: "ExchangeRates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountTransactions_ExchangeRates_ExchangeRateId",
                table: "AccountTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_FinancePostingEvents_ExchangeRates_PrimaryExchangeRateId",
                table: "FinancePostingEvents");

            migrationBuilder.DropIndex(
                name: "IX_FinanceSettings_TenantId",
                table: "FinanceSettings");

            migrationBuilder.DropIndex(
                name: "IX_FinancePostingEvents_PrimaryExchangeRateId",
                table: "FinancePostingEvents");

            migrationBuilder.DropIndex(
                name: "IX_FinancePostingEvents_TenantId_HasForeignCurrencyLines",
                table: "FinancePostingEvents");

            migrationBuilder.DropIndex(
                name: "IX_ExchangeRates_HasBeenUsedInTransactions",
                table: "ExchangeRates");

            migrationBuilder.DropIndex(
                name: "IX_ExchangeRates_TenantId_BaseCurrencyCode_TargetCurrencyCode_RateType_EffectiveDate",
                table: "ExchangeRates");

            migrationBuilder.DropIndex(
                name: "IX_ExchangeRates_TenantId_BaseCurrencyCode_TargetCurrencyCode_RateType_IsActive",
                table: "ExchangeRates");

            migrationBuilder.DropIndex(
                name: "IX_AccountTransactions_ExchangeRateId",
                table: "AccountTransactions");

            migrationBuilder.DropIndex(
                name: "IX_AccountTransactions_TenantId_TransactionCurrency_ExchangeRateId",
                table: "AccountTransactions");

            migrationBuilder.DropColumn(
                name: "FunctionalCurrencyLocked",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "FunctionalCurrencyLockedAt",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "FunctionalCurrencyLockedReason",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "HasForeignCurrencyLines",
                table: "FinancePostingEvents");

            migrationBuilder.DropColumn(
                name: "PrimaryExchangeRate",
                table: "FinancePostingEvents");

            migrationBuilder.DropColumn(
                name: "PrimaryExchangeRateDate",
                table: "FinancePostingEvents");

            migrationBuilder.DropColumn(
                name: "PrimaryExchangeRateId",
                table: "FinancePostingEvents");

            migrationBuilder.DropColumn(
                name: "PrimaryTransactionCurrencyCode",
                table: "FinancePostingEvents");

            migrationBuilder.DropColumn(
                name: "ExchangeRateId",
                table: "AccountTransactions");

            migrationBuilder.DropColumn(
                name: "FunctionalCurrencyCode",
                table: "AccountTransactions");

            migrationBuilder.DropColumn(
                name: "TransactionCreditAmount",
                table: "AccountTransactions");

            migrationBuilder.DropColumn(
                name: "TransactionDebitAmount",
                table: "AccountTransactions");

            migrationBuilder.AlterColumn<string>(
                name: "BaseCurrency",
                table: "FinanceSettings",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "ExchangeRate",
                table: "AccountTransactions",
                type: "decimal(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_TenantId",
                table: "FinanceSettings",
                column: "TenantId");
        }
    }
}
