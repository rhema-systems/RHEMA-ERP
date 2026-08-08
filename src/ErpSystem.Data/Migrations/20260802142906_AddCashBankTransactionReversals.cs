using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Adds immutable correction lineage to the cash/bank operational subledger. Posted source
    /// rows remain intact; the nullable links point to the compensating operational row, journal,
    /// and posting event created by the controlled reversal command.
    /// </summary>
    // Debug builds omit most generated Designer files. Keeping discovery metadata on the main
    // class follows the repository's manual-migration pattern and protects development startup.
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260802142906_AddCashBankTransactionReversals")]
    public partial class AddCashBankTransactionReversals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IsReversed is a read-side state only. It never replaces IsPosted because the
            // original movement and its correction must both remain visible to reconciliation.
            migrationBuilder.AddColumn<bool>(
                name: "IsReversed",
                table: "CashTransaction",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversalCashTransactionId",
                table: "CashTransaction",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReversalDate",
                table: "CashTransaction",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversalJournalEntryId",
                table: "CashTransaction",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversalOfCashTransactionId",
                table: "CashTransaction",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversalPostingEventId",
                table: "CashTransaction",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalReason",
                table: "CashTransaction",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReversedAt",
                table: "CashTransaction",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversedById",
                table: "CashTransaction",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashTransaction_ReversalCashTransactionId",
                table: "CashTransaction",
                column: "ReversalCashTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_CashTransaction_ReversalJournalEntryId",
                table: "CashTransaction",
                column: "ReversalJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_CashTransaction_ReversalOfCashTransactionId",
                table: "CashTransaction",
                column: "ReversalOfCashTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_CashTransaction_ReversalPostingEventId",
                table: "CashTransaction",
                column: "ReversalPostingEventId");

            migrationBuilder.CreateIndex(
                name: "IX_CashTransaction_TenantId_ReversalCashTransactionId",
                table: "CashTransaction",
                columns: new[] { "TenantId", "ReversalCashTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CashTransaction_TenantId_ReversalJournalEntryId",
                table: "CashTransaction",
                columns: new[] { "TenantId", "ReversalJournalEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_CashTransaction_TenantId_ReversalOfCashTransactionId",
                table: "CashTransaction",
                columns: new[] { "TenantId", "ReversalOfCashTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CashTransaction_TenantId_ReversalPostingEventId",
                table: "CashTransaction",
                columns: new[] { "TenantId", "ReversalPostingEventId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CashTransaction_CashTransaction_ReversalCashTransactionId",
                table: "CashTransaction",
                column: "ReversalCashTransactionId",
                principalTable: "CashTransaction",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Restrict deletion of every correction link so no maintenance operation can remove
            // one side of the source-to-operational-to-ledger evidence chain.

            migrationBuilder.AddForeignKey(
                name: "FK_CashTransaction_CashTransaction_ReversalOfCashTransactionId",
                table: "CashTransaction",
                column: "ReversalOfCashTransactionId",
                principalTable: "CashTransaction",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashTransaction_FinancePostingEvents_ReversalPostingEventId",
                table: "CashTransaction",
                column: "ReversalPostingEventId",
                principalTable: "FinancePostingEvents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashTransaction_JournalEntries_ReversalJournalEntryId",
                table: "CashTransaction",
                column: "ReversalJournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashTransaction_CashTransaction_ReversalCashTransactionId",
                table: "CashTransaction");

            migrationBuilder.DropForeignKey(
                name: "FK_CashTransaction_CashTransaction_ReversalOfCashTransactionId",
                table: "CashTransaction");

            migrationBuilder.DropForeignKey(
                name: "FK_CashTransaction_FinancePostingEvents_ReversalPostingEventId",
                table: "CashTransaction");

            migrationBuilder.DropForeignKey(
                name: "FK_CashTransaction_JournalEntries_ReversalJournalEntryId",
                table: "CashTransaction");

            migrationBuilder.DropIndex(
                name: "IX_CashTransaction_ReversalCashTransactionId",
                table: "CashTransaction");

            migrationBuilder.DropIndex(
                name: "IX_CashTransaction_ReversalJournalEntryId",
                table: "CashTransaction");

            migrationBuilder.DropIndex(
                name: "IX_CashTransaction_ReversalOfCashTransactionId",
                table: "CashTransaction");

            migrationBuilder.DropIndex(
                name: "IX_CashTransaction_ReversalPostingEventId",
                table: "CashTransaction");

            migrationBuilder.DropIndex(
                name: "IX_CashTransaction_TenantId_ReversalCashTransactionId",
                table: "CashTransaction");

            migrationBuilder.DropIndex(
                name: "IX_CashTransaction_TenantId_ReversalJournalEntryId",
                table: "CashTransaction");

            migrationBuilder.DropIndex(
                name: "IX_CashTransaction_TenantId_ReversalOfCashTransactionId",
                table: "CashTransaction");

            migrationBuilder.DropIndex(
                name: "IX_CashTransaction_TenantId_ReversalPostingEventId",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "IsReversed",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "ReversalCashTransactionId",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "ReversalDate",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "ReversalJournalEntryId",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "ReversalOfCashTransactionId",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "ReversalPostingEventId",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "ReversalReason",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "ReversedAt",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "ReversedById",
                table: "CashTransaction");
        }
    }
}
