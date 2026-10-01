using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinancePrecisionRoundingGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RoundingIncrement",
                table: "UnitTypes",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExchangeRateDisplayDecimalPlaces",
                table: "FinanceSettings",
                type: "int",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<int>(
                name: "ExchangeRateInputDecimalPlaces",
                table: "FinanceSettings",
                type: "int",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<bool>(
                name: "InvoiceRoundingEnabled",
                table: "FinanceSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceRoundingGainAccountId",
                table: "FinanceSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InvoiceRoundingIncrement",
                table: "FinanceSettings",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceRoundingLossAccountId",
                table: "FinanceSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvoiceRoundingMethod",
                table: "FinanceSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReportDisplayDecimalPlaces",
                table: "FinanceSettings",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<decimal>(
                name: "SettlementToleranceAmount",
                table: "FinanceSettings",
                type: "decimal(20,4)",
                precision: 20,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SettlementTolerancePercentage",
                table: "FinanceSettings",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TaxPercentageDecimalPlaces",
                table: "FinanceSettings",
                type: "int",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRoundingIncrement",
                table: "FinanceSettings",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaxRoundingMethod",
                table: "FinanceSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TaxRoundingScope",
                table: "FinanceSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UnitPriceDecimalPlaces",
                table: "FinanceSettings",
                type: "int",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_InvoiceRoundingGainAccountId",
                table: "FinanceSettings",
                column: "InvoiceRoundingGainAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_InvoiceRoundingLossAccountId",
                table: "FinanceSettings",
                column: "InvoiceRoundingLossAccountId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinanceSettings_InvoiceRoundingReadiness",
                table: "FinanceSettings",
                sql: "[InvoiceRoundingEnabled] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinanceSettings_PrecisionGovernance",
                table: "FinanceSettings",
                sql: "[UnitPriceDecimalPlaces] BETWEEN 0 AND 6 AND [ExchangeRateInputDecimalPlaces] BETWEEN 6 AND 10 AND [ExchangeRateDisplayDecimalPlaces] BETWEEN 6 AND 10 AND [TaxPercentageDecimalPlaces] BETWEEN 0 AND 4 AND [ReportDisplayDecimalPlaces] BETWEEN 0 AND 4 AND [TaxRoundingMethod] IN (0, 1, 2) AND [TaxRoundingScope] = 0 AND [InvoiceRoundingMethod] IN (0, 1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinanceSettings_RoundingIncrements",
                table: "FinanceSettings",
                sql: "([TaxRoundingIncrement] IS NULL OR [TaxRoundingIncrement] > 0) AND ([InvoiceRoundingIncrement] IS NULL OR [InvoiceRoundingIncrement] > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinanceSettings_SettlementTolerance",
                table: "FinanceSettings",
                sql: "[SettlementToleranceAmount] >= 0 AND [SettlementTolerancePercentage] BETWEEN 0 AND 100");

            migrationBuilder.AddForeignKey(
                name: "FK_FinanceSettings_Accounts_InvoiceRoundingGainAccountId",
                table: "FinanceSettings",
                column: "InvoiceRoundingGainAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FinanceSettings_Accounts_InvoiceRoundingLossAccountId",
                table: "FinanceSettings",
                column: "InvoiceRoundingLossAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinanceSettings_Accounts_InvoiceRoundingGainAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_FinanceSettings_Accounts_InvoiceRoundingLossAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropIndex(
                name: "IX_FinanceSettings_InvoiceRoundingGainAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropIndex(
                name: "IX_FinanceSettings_InvoiceRoundingLossAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinanceSettings_InvoiceRoundingReadiness",
                table: "FinanceSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinanceSettings_PrecisionGovernance",
                table: "FinanceSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinanceSettings_RoundingIncrements",
                table: "FinanceSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinanceSettings_SettlementTolerance",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "RoundingIncrement",
                table: "UnitTypes");

            migrationBuilder.DropColumn(
                name: "ExchangeRateDisplayDecimalPlaces",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "ExchangeRateInputDecimalPlaces",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "InvoiceRoundingEnabled",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "InvoiceRoundingGainAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "InvoiceRoundingIncrement",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "InvoiceRoundingLossAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "InvoiceRoundingMethod",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "ReportDisplayDecimalPlaces",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "SettlementToleranceAmount",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "SettlementTolerancePercentage",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "TaxPercentageDecimalPlaces",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "TaxRoundingIncrement",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "TaxRoundingMethod",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "TaxRoundingScope",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "UnitPriceDecimalPlaces",
                table: "FinanceSettings");
        }
    }
}
