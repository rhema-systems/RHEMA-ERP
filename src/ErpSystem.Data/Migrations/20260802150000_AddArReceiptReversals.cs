using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds immutable lineage from an AR customer receipt to the compensating posting and the
/// operational cash/liquidity entry created by a controlled reversal. The original receipt and
/// allocations remain intact; these nullable links are populated only after the entire reversal
/// command commits successfully.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260802150000_AddArReceiptReversals")]
public partial class AddArReceiptReversals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ReversalCashTransactionId",
            table: "CustomerPayment",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ReversalDate",
            table: "CustomerPayment",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ReversalJournalEntryId",
            table: "CustomerPayment",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ReversalLiquidityAccountEntryId",
            table: "CustomerPayment",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ReversalPostingEventId",
            table: "CustomerPayment",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ReversalReason",
            table: "CustomerPayment",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ReversedAt",
            table: "CustomerPayment",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ReversedById",
            table: "CustomerPayment",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_CustomerPayment_ReversalCashTransactionId",
            table: "CustomerPayment",
            column: "ReversalCashTransactionId");

        migrationBuilder.CreateIndex(
            name: "IX_CustomerPayment_ReversalJournalEntryId",
            table: "CustomerPayment",
            column: "ReversalJournalEntryId");

        migrationBuilder.CreateIndex(
            name: "IX_CustomerPayment_ReversalLiquidityAccountEntryId",
            table: "CustomerPayment",
            column: "ReversalLiquidityAccountEntryId");

        migrationBuilder.CreateIndex(
            name: "IX_CustomerPayment_ReversalPostingEventId",
            table: "CustomerPayment",
            column: "ReversalPostingEventId");

        migrationBuilder.AddForeignKey(
            name: "FK_CustomerPayment_CashTransactions_ReversalCashTransactionId",
            table: "CustomerPayment",
            column: "ReversalCashTransactionId",
            principalTable: "CashTransaction",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_CustomerPayment_JournalEntries_ReversalJournalEntryId",
            table: "CustomerPayment",
            column: "ReversalJournalEntryId",
            principalTable: "JournalEntries",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_CustomerPayment_LiquidityAccountEntries_ReversalLiquidityAccountEntryId",
            table: "CustomerPayment",
            column: "ReversalLiquidityAccountEntryId",
            principalTable: "LiquidityAccountEntries",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_CustomerPayment_FinancePostingEvents_ReversalPostingEventId",
            table: "CustomerPayment",
            column: "ReversalPostingEventId",
            principalTable: "FinancePostingEvents",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_CustomerPayment_CashTransactions_ReversalCashTransactionId",
            table: "CustomerPayment");
        migrationBuilder.DropForeignKey(
            name: "FK_CustomerPayment_JournalEntries_ReversalJournalEntryId",
            table: "CustomerPayment");
        migrationBuilder.DropForeignKey(
            name: "FK_CustomerPayment_LiquidityAccountEntries_ReversalLiquidityAccountEntryId",
            table: "CustomerPayment");
        migrationBuilder.DropForeignKey(
            name: "FK_CustomerPayment_FinancePostingEvents_ReversalPostingEventId",
            table: "CustomerPayment");

        migrationBuilder.DropIndex(name: "IX_CustomerPayment_ReversalCashTransactionId", table: "CustomerPayment");
        migrationBuilder.DropIndex(name: "IX_CustomerPayment_ReversalJournalEntryId", table: "CustomerPayment");
        migrationBuilder.DropIndex(name: "IX_CustomerPayment_ReversalLiquidityAccountEntryId", table: "CustomerPayment");
        migrationBuilder.DropIndex(name: "IX_CustomerPayment_ReversalPostingEventId", table: "CustomerPayment");

        migrationBuilder.DropColumn(name: "ReversalCashTransactionId", table: "CustomerPayment");
        migrationBuilder.DropColumn(name: "ReversalDate", table: "CustomerPayment");
        migrationBuilder.DropColumn(name: "ReversalJournalEntryId", table: "CustomerPayment");
        migrationBuilder.DropColumn(name: "ReversalLiquidityAccountEntryId", table: "CustomerPayment");
        migrationBuilder.DropColumn(name: "ReversalPostingEventId", table: "CustomerPayment");
        migrationBuilder.DropColumn(name: "ReversalReason", table: "CustomerPayment");
        migrationBuilder.DropColumn(name: "ReversedAt", table: "CustomerPayment");
        migrationBuilder.DropColumn(name: "ReversedById", table: "CustomerPayment");
    }
}
