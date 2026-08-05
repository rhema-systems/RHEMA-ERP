using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    // Normal Debug/deployment builds intentionally omit the very large generated migration
    // designers. Keep discovery metadata on the executable migration as well so this Finance
    // control remains visible to startup migration checks and database-update commands.
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260803213749_AddCrossCurrencyBankTransfers")]
    /// <inheritdoc />
    public partial class AddCrossCurrencyBankTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CashTransaction previously inherited the legacy global four-place decimal rule.
            // Transfer rate evidence must retain the same six-place precision as ExchangeRates.
            migrationBuilder.AlterColumn<decimal>(
                name: "ExchangeRate",
                table: "CashTransaction",
                type: "decimal(18,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExchangeRateDate",
                table: "CashTransaction",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExchangeRateId",
                table: "CashTransaction",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExchangeRateQuoteSide",
                table: "CashTransaction",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExchangeRateSource",
                table: "CashTransaction",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransferCrossRate",
                table: "CashTransaction",
                type: "decimal(18,8)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransferFxGainLossBaseAmount",
                table: "CashTransaction",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TransferLeg",
                table: "CashTransaction",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransferPairId",
                table: "CashTransaction",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashTransaction_ExchangeRateId",
                table: "CashTransaction",
                column: "ExchangeRateId");

            migrationBuilder.CreateIndex(
                name: "IX_CashTransaction_TenantId_ExchangeRateId",
                table: "CashTransaction",
                columns: new[] { "TenantId", "ExchangeRateId" });

            migrationBuilder.CreateIndex(
                name: "IX_CashTransaction_TenantId_TransferPairId_TransferLeg",
                table: "CashTransaction",
                columns: new[] { "TenantId", "TransferPairId", "TransferLeg" },
                unique: true,
                filter: "[TransferPairId] IS NOT NULL AND [TransferLeg] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_CashTransaction_ExchangeRates_ExchangeRateId",
                table: "CashTransaction",
                column: "ExchangeRateId",
                principalTable: "ExchangeRates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashTransaction_ExchangeRates_ExchangeRateId",
                table: "CashTransaction");

            migrationBuilder.DropIndex(
                name: "IX_CashTransaction_ExchangeRateId",
                table: "CashTransaction");

            migrationBuilder.DropIndex(
                name: "IX_CashTransaction_TenantId_ExchangeRateId",
                table: "CashTransaction");

            migrationBuilder.DropIndex(
                name: "IX_CashTransaction_TenantId_TransferPairId_TransferLeg",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "ExchangeRateDate",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "ExchangeRateId",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "ExchangeRateQuoteSide",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "ExchangeRateSource",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "TransferCrossRate",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "TransferFxGainLossBaseAmount",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "TransferLeg",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "TransferPairId",
                table: "CashTransaction");

            migrationBuilder.AlterColumn<decimal>(
                name: "ExchangeRate",
                table: "CashTransaction",
                type: "decimal(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldNullable: true);
        }
    }
}
