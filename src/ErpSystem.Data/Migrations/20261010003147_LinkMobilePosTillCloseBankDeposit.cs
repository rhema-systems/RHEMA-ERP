using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkMobilePosTillCloseBankDeposit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BankDepositBatchId",
                table: "MobilePosTillCloseSubmissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BankDepositProposedAtUtc",
                table: "MobilePosTillCloseSubmissions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BankDepositProposedByUserId",
                table: "MobilePosTillCloseSubmissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosTillCloseSubmissions_BankDepositBatchId",
                table: "MobilePosTillCloseSubmissions",
                column: "BankDepositBatchId");

            migrationBuilder.AddForeignKey(
                name: "FK_MobilePosTillCloseSubmissions_BankDepositBatches_BankDepositBatchId",
                table: "MobilePosTillCloseSubmissions",
                column: "BankDepositBatchId",
                principalTable: "BankDepositBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MobilePosTillCloseSubmissions_BankDepositBatches_BankDepositBatchId",
                table: "MobilePosTillCloseSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_MobilePosTillCloseSubmissions_BankDepositBatchId",
                table: "MobilePosTillCloseSubmissions");

            migrationBuilder.DropColumn(
                name: "BankDepositBatchId",
                table: "MobilePosTillCloseSubmissions");

            migrationBuilder.DropColumn(
                name: "BankDepositProposedAtUtc",
                table: "MobilePosTillCloseSubmissions");

            migrationBuilder.DropColumn(
                name: "BankDepositProposedByUserId",
                table: "MobilePosTillCloseSubmissions");
        }
    }
}
