using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AccountingBookTranslationEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Normalize legacy target-to-base rows to the canonical public/storage direction:
            // 1 BaseCurrencyCode = Rate TargetCurrencyCode.
            migrationBuilder.Sql("UPDATE [ExchangeRates] SET [Rate] = [InverseRate], [InverseRate] = [Rate] WHERE [Rate] > 0 AND [InverseRate] > 0;");

            migrationBuilder.AddColumn<int>(
                name: "TranslationMethod",
                table: "AccountingBookInitializations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TranslationExchangeRateId",
                table: "AccountingBookInitializationLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TranslationRate",
                table: "AccountingBookInitializationLines",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TranslationRateDate",
                table: "AccountingBookInitializationLines",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranslationRateSource",
                table: "AccountingBookInitializationLines",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranslationRateType",
                table: "AccountingBookInitializationLines",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ExchangeRates_TenantId_Id",
                table: "ExchangeRates",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountingBookInitializations_TranslationMethod",
                table: "AccountingBookInitializations",
                sql: "[TranslationMethod] IS NULL OR [TranslationMethod] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookInitializationLines_TenantId_TranslationExchangeRateId",
                table: "AccountingBookInitializationLines",
                columns: new[] { "TenantId", "TranslationExchangeRateId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountingBookInitializationLines_TranslationEvidence",
                table: "AccountingBookInitializationLines",
                sql: "([TranslationExchangeRateId] IS NULL AND [TranslationRate] IS NULL AND [TranslationRateDate] IS NULL AND [TranslationRateType] IS NULL AND [TranslationRateSource] IS NULL) OR ([TranslationExchangeRateId] IS NOT NULL AND [TranslationRate] > 0 AND [TranslationRateDate] IS NOT NULL AND [TranslationRateType] IS NOT NULL AND [TranslationRateSource] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountingBookInitializationLines_ExchangeRates_TenantId_TranslationExchangeRateId",
                table: "AccountingBookInitializationLines",
                columns: new[] { "TenantId", "TranslationExchangeRateId" },
                principalTable: "ExchangeRates",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountingBookInitializationLines_ExchangeRates_TenantId_TranslationExchangeRateId",
                table: "AccountingBookInitializationLines");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ExchangeRates_TenantId_Id",
                table: "ExchangeRates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountingBookInitializations_TranslationMethod",
                table: "AccountingBookInitializations");

            migrationBuilder.DropIndex(
                name: "IX_AccountingBookInitializationLines_TenantId_TranslationExchangeRateId",
                table: "AccountingBookInitializationLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountingBookInitializationLines_TranslationEvidence",
                table: "AccountingBookInitializationLines");

            migrationBuilder.DropColumn(
                name: "TranslationMethod",
                table: "AccountingBookInitializations");

            migrationBuilder.DropColumn(
                name: "TranslationExchangeRateId",
                table: "AccountingBookInitializationLines");

            migrationBuilder.DropColumn(
                name: "TranslationRate",
                table: "AccountingBookInitializationLines");

            migrationBuilder.DropColumn(
                name: "TranslationRateDate",
                table: "AccountingBookInitializationLines");

            migrationBuilder.DropColumn(
                name: "TranslationRateSource",
                table: "AccountingBookInitializationLines");

            migrationBuilder.DropColumn(
                name: "TranslationRateType",
                table: "AccountingBookInitializationLines");

            migrationBuilder.Sql("UPDATE [ExchangeRates] SET [Rate] = [InverseRate], [InverseRate] = [Rate] WHERE [Rate] > 0 AND [InverseRate] > 0;");
        }
    }
}
