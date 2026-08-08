using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [Migration("20260610172000_AddFinanceDiscountControlAccounts")]
    public partial class AddFinanceDiscountControlAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DiscountAllowedAccountId",
                table: "FinanceSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DiscountReceivedAccountId",
                table: "FinanceSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_DiscountAllowedAccountId",
                table: "FinanceSettings",
                column: "DiscountAllowedAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_DiscountReceivedAccountId",
                table: "FinanceSettings",
                column: "DiscountReceivedAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_FinanceSettings_Accounts_DiscountAllowedAccountId",
                table: "FinanceSettings",
                column: "DiscountAllowedAccountId",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FinanceSettings_Accounts_DiscountReceivedAccountId",
                table: "FinanceSettings",
                column: "DiscountReceivedAccountId",
                principalTable: "Accounts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinanceSettings_Accounts_DiscountAllowedAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_FinanceSettings_Accounts_DiscountReceivedAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropIndex(
                name: "IX_FinanceSettings_DiscountAllowedAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropIndex(
                name: "IX_FinanceSettings_DiscountReceivedAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "DiscountAllowedAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "DiscountReceivedAccountId",
                table: "FinanceSettings");
        }
    }
}
