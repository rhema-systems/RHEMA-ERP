using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds controlled GL mappings to tender fees and immutable central Finance
/// posting lineage to supplier tender-fee payments. All columns are nullable
/// so existing tender and payment records remain readable after upgrade.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260831203000_AddTenderFeeFinancePostingLineage")]
public sealed class AddTenderFeeFinancePostingLineage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ReceivingAccountId",
            table: "TenderFees",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "RevenueAccountId",
            table: "TenderFees",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "PostingEventId",
            table: "TenderPayments",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "JournalEntryId",
            table: "TenderPayments",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "PostedAtUtc",
            table: "TenderPayments",
            type: "datetime2",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_TenderFees_ReceivingAccountId",
            table: "TenderFees",
            column: "ReceivingAccountId");

        migrationBuilder.CreateIndex(
            name: "IX_TenderFees_RevenueAccountId",
            table: "TenderFees",
            column: "RevenueAccountId");

        migrationBuilder.CreateIndex(
            name: "IX_TenderPayments_PostingEventId",
            table: "TenderPayments",
            column: "PostingEventId",
            unique: true,
            filter: "[PostingEventId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_TenderPayments_JournalEntryId",
            table: "TenderPayments",
            column: "JournalEntryId",
            unique: true,
            filter: "[JournalEntryId] IS NOT NULL");

        migrationBuilder.AddForeignKey(
            name: "FK_TenderFees_Accounts_ReceivingAccountId",
            table: "TenderFees",
            column: "ReceivingAccountId",
            principalTable: "Accounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_TenderFees_Accounts_RevenueAccountId",
            table: "TenderFees",
            column: "RevenueAccountId",
            principalTable: "Accounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_TenderPayments_FinancePostingEvents_PostingEventId",
            table: "TenderPayments",
            column: "PostingEventId",
            principalTable: "FinancePostingEvents",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_TenderPayments_JournalEntries_JournalEntryId",
            table: "TenderPayments",
            column: "JournalEntryId",
            principalTable: "JournalEntries",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_TenderFees_Accounts_ReceivingAccountId",
            table: "TenderFees");

        migrationBuilder.DropForeignKey(
            name: "FK_TenderFees_Accounts_RevenueAccountId",
            table: "TenderFees");

        migrationBuilder.DropForeignKey(
            name: "FK_TenderPayments_FinancePostingEvents_PostingEventId",
            table: "TenderPayments");

        migrationBuilder.DropForeignKey(
            name: "FK_TenderPayments_JournalEntries_JournalEntryId",
            table: "TenderPayments");

        migrationBuilder.DropIndex(
            name: "IX_TenderFees_ReceivingAccountId",
            table: "TenderFees");

        migrationBuilder.DropIndex(
            name: "IX_TenderFees_RevenueAccountId",
            table: "TenderFees");

        migrationBuilder.DropIndex(
            name: "IX_TenderPayments_PostingEventId",
            table: "TenderPayments");

        migrationBuilder.DropIndex(
            name: "IX_TenderPayments_JournalEntryId",
            table: "TenderPayments");

        migrationBuilder.DropColumn(
            name: "ReceivingAccountId",
            table: "TenderFees");

        migrationBuilder.DropColumn(
            name: "RevenueAccountId",
            table: "TenderFees");

        migrationBuilder.DropColumn(
            name: "PostingEventId",
            table: "TenderPayments");

        migrationBuilder.DropColumn(
            name: "JournalEntryId",
            table: "TenderPayments");

        migrationBuilder.DropColumn(
            name: "PostedAtUtc",
            table: "TenderPayments");
    }
}
