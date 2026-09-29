using System;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext)), Migration("20260929102006_JournalBatchStableAccountingBook")]
    public partial class JournalBatchStableAccountingBook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccountingBookId",
                table: "JournalBatches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE batch
                SET batch.AccountingBookId = matches.AccountingBookId
                FROM JournalBatches AS batch
                CROSS APPLY
                (
                    SELECT TOP (1) book.Id AS AccountingBookId
                    FROM AccountingBooks AS book
                    WHERE book.TenantId = batch.TenantId
                      AND book.Code = batch.BookClassification
                    ORDER BY book.Id
                ) AS matches
                WHERE 1 =
                (
                    SELECT COUNT_BIG(*)
                    FROM AccountingBooks AS candidate
                    WHERE candidate.TenantId = batch.TenantId
                      AND candidate.Code = batch.BookClassification
                );

                IF EXISTS
                (
                    SELECT 1
                    FROM JournalBatches
                    WHERE AccountingBookId IS NULL
                )
                BEGIN
                    THROW 51000, 'Journal batch accounting-book backfill failed. Every legacy BookClassification must resolve to exactly one same-tenant AccountingBooks.Code before this migration can continue.', 1;
                END;

                IF EXISTS
                (
                    SELECT 1
                    FROM JournalBatchItems AS item
                    INNER JOIN JournalBatches AS batch ON batch.Id = item.JournalBatchId
                    INNER JOIN JournalEntries AS journalEntry ON journalEntry.Id = item.JournalEntryId
                    WHERE item.IsDeleted = 0
                      AND batch.IsDeleted = 0
                      AND journalEntry.IsDeleted = 0
                      AND journalEntry.AccountingBookId <> batch.AccountingBookId
                )
                BEGIN
                    THROW 51001, 'Journal batch accounting-book backfill found an attached journal in a different accounting book. Repair the legacy batch membership before this migration can continue.', 1;
                END;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "AccountingBookId",
                table: "JournalBatches",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatches_AccountingBookId",
                table: "JournalBatches",
                column: "AccountingBookId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalBatches_TenantId_AccountingBookId_FiscalPeriodId",
                table: "JournalBatches",
                columns: new[] { "TenantId", "AccountingBookId", "FiscalPeriodId" });

            migrationBuilder.AddForeignKey(
                name: "FK_JournalBatches_AccountingBooks_AccountingBookId",
                table: "JournalBatches",
                column: "AccountingBookId",
                principalTable: "AccountingBooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JournalBatches_AccountingBooks_AccountingBookId",
                table: "JournalBatches");

            migrationBuilder.DropIndex(
                name: "IX_JournalBatches_AccountingBookId",
                table: "JournalBatches");

            migrationBuilder.DropIndex(
                name: "IX_JournalBatches_TenantId_AccountingBookId_FiscalPeriodId",
                table: "JournalBatches");

            migrationBuilder.DropColumn(
                name: "AccountingBookId",
                table: "JournalBatches");
        }
    }
}
