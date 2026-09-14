using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260705143000_AddCashTransactionJournalEntryBackReference")]
    public partial class AddCashTransactionJournalEntryBackReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "JournalEntryId",
                table: "CashTransaction",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashTransaction_JournalEntryId",
                table: "CashTransaction",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_CashTransaction_TenantId_JournalEntryId",
                table: "CashTransaction",
                columns: new[] { "TenantId", "JournalEntryId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CashTransaction_JournalEntries_JournalEntryId",
                table: "CashTransaction",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashTransaction_JournalEntries_JournalEntryId",
                table: "CashTransaction");

            migrationBuilder.DropIndex(
                name: "IX_CashTransaction_JournalEntryId",
                table: "CashTransaction");

            migrationBuilder.DropIndex(
                name: "IX_CashTransaction_TenantId_JournalEntryId",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "CashTransaction");
        }
    }
}
