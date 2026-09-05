using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStablePostingAccountingBookIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration can already be recorded on development databases once integrated.
            // Do not simplify or remove this preflight: the old code snapshot was the only book
            // authority, so every retained row must resolve losslessly before any schema mutation.
            migrationBuilder.Sql("""
IF EXISTS (
    SELECT 1 FROM JournalEntries j
    WHERE
        NULLIF(LTRIM(RTRIM(j.BookClassification)), '') IS NULL OR
        UPPER(LTRIM(RTRIM(j.BookClassification))) IN ('ALL_ACTIVE_BOOKS', 'ALL_CLASSIFIED_BOOKS', 'ALLCLASSIFIEDBOOKS', 'ALL') OR
        (SELECT COUNT_BIG(*) FROM AccountingBooks b
         WHERE b.TenantId = j.TenantId AND b.IsDeleted = 0 AND b.Code = j.BookClassification) <> 1)
    THROW 51000, 'C1_BOOK_ID_PREFLIGHT_JOURNAL: retained journal book code is blank, pseudo, unknown, ambiguous, or cross-tenant.', 1;

IF EXISTS (
    SELECT 1 FROM AccountTransactions t
    LEFT JOIN JournalEntries j ON j.Id = t.JournalEntryId
    WHERE
        NULLIF(LTRIM(RTRIM(t.BookClassification)), '') IS NULL OR
        UPPER(LTRIM(RTRIM(t.BookClassification))) IN ('ALL_ACTIVE_BOOKS', 'ALL_CLASSIFIED_BOOKS', 'ALLCLASSIFIEDBOOKS', 'ALL') OR
        (SELECT COUNT_BIG(*) FROM AccountingBooks b
         WHERE b.TenantId = t.TenantId AND b.IsDeleted = 0 AND b.Code = t.BookClassification) <> 1 OR
        j.Id IS NULL OR j.TenantId <> t.TenantId OR j.BookClassification <> t.BookClassification)
    THROW 51000, 'C1_BOOK_ID_PREFLIGHT_TRANSACTION: retained transaction book or journal lineage is inconsistent.', 1;

IF EXISTS (
    SELECT 1 FROM FinancePostingEvents e
    LEFT JOIN JournalEntries j ON j.Id = e.JournalEntryId
    WHERE
        NULLIF(LTRIM(RTRIM(e.BookClassification)), '') IS NULL OR
        UPPER(LTRIM(RTRIM(e.BookClassification))) IN ('ALL_ACTIVE_BOOKS', 'ALL_CLASSIFIED_BOOKS', 'ALLCLASSIFIEDBOOKS', 'ALL') OR
        (SELECT COUNT_BIG(*) FROM AccountingBooks b
         WHERE b.TenantId = e.TenantId AND b.IsDeleted = 0 AND b.Code = e.BookClassification) <> 1 OR
        (e.JournalEntryId IS NOT NULL AND
            (j.Id IS NULL OR j.TenantId <> e.TenantId OR j.BookClassification <> e.BookClassification))
    THROW 51000, 'C1_BOOK_ID_PREFLIGHT_EVENT: retained posting-event book or journal lineage is inconsistent.', 1;
""");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountTransactions_JournalEntries_JournalEntryId",
                table: "AccountTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_FinancePostingEvents_JournalEntries_JournalEntryId",
                table: "FinancePostingEvents");

            migrationBuilder.DropIndex(
                name: "IX_FinancePostingEvents_TenantId_IdempotencyKey",
                table: "FinancePostingEvents");

            migrationBuilder.DropIndex(
                name: "IX_FinancePostingEvents_TenantId_SourceDocumentType_SourceDocumentId_PostingAction",
                table: "FinancePostingEvents");

            migrationBuilder.DropIndex(
                name: "IX_FinancePostingEvents_TenantId_SourceModule_SourceDocumentType_SourceDocumentId_PostingAction",
                table: "FinancePostingEvents");

            migrationBuilder.DropIndex(
                name: "IX_AccountTransactions_JournalEntryId",
                table: "AccountTransactions");

            migrationBuilder.DropIndex(
                name: "IX_AccountTransactions_TenantId_BookClassification_TransactionDate_AccountId",
                table: "AccountTransactions");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountingBookId",
                table: "JournalEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AccountingBookId",
                table: "FinancePostingEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AccountingBookId",
                table: "AccountTransactions",
                type: "uniqueidentifier",
                nullable: true);

            // Preserve the immutable code snapshots while adding their stable relational identities.
            // The preflight above guarantees each join is same-tenant and exactly one-to-one.
            migrationBuilder.Sql("""
UPDATE j SET AccountingBookId = b.Id
FROM JournalEntries j
JOIN AccountingBooks b ON b.TenantId = j.TenantId AND b.Code = j.BookClassification AND b.IsDeleted = 0;

UPDATE t SET AccountingBookId = b.Id
FROM AccountTransactions t
JOIN AccountingBooks b ON b.TenantId = t.TenantId AND b.Code = t.BookClassification AND b.IsDeleted = 0;

UPDATE e SET AccountingBookId = b.Id
FROM FinancePostingEvents e
JOIN AccountingBooks b ON b.TenantId = e.TenantId AND b.Code = e.BookClassification AND b.IsDeleted = 0;

IF EXISTS (SELECT 1 FROM JournalEntries WHERE AccountingBookId IS NULL)
 OR EXISTS (SELECT 1 FROM AccountTransactions WHERE AccountingBookId IS NULL)
 OR EXISTS (SELECT 1 FROM FinancePostingEvents WHERE AccountingBookId IS NULL)
    THROW 51000, 'C1_BOOK_ID_BACKFILL_INCOMPLETE: all retained and soft-deleted posting evidence must resolve before book identity becomes required.', 1;
""");

            migrationBuilder.AlterColumn<Guid>(
                name: "AccountingBookId",
                table: "JournalEntries",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AccountingBookId",
                table: "FinancePostingEvents",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AccountingBookId",
                table: "AccountTransactions",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_JournalEntries_TenantId_Id_AccountingBookId",
                table: "JournalEntries",
                columns: new[] { "TenantId", "Id", "AccountingBookId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_AccountingBooks_TenantId_Id",
                table: "AccountingBooks",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_TenantId_AccountingBookId",
                table: "JournalEntries",
                columns: new[] { "TenantId", "AccountingBookId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancePostingEvents_TenantId_AccountingBookId_IdempotencyKey",
                table: "FinancePostingEvents",
                columns: new[] { "TenantId", "AccountingBookId", "IdempotencyKey" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePostingEvents_TenantId_AccountingBookId_SourceDocumentType_SourceDocumentId_PostingAction",
                table: "FinancePostingEvents",
                columns: new[] { "TenantId", "AccountingBookId", "SourceDocumentType", "SourceDocumentId", "PostingAction" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePostingEvents_TenantId_AccountingBookId_SourceModule_SourceDocumentType_SourceDocumentId_PostingAction",
                table: "FinancePostingEvents",
                columns: new[] { "TenantId", "AccountingBookId", "SourceModule", "SourceDocumentType", "SourceDocumentId", "PostingAction" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePostingEvents_TenantId_JournalEntryId_AccountingBookId",
                table: "FinancePostingEvents",
                columns: new[] { "TenantId", "JournalEntryId", "AccountingBookId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_TenantId_AccountingBookId_TransactionDate_AccountId",
                table: "AccountTransactions",
                columns: new[] { "TenantId", "AccountingBookId", "TransactionDate", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_TenantId_JournalEntryId_AccountingBookId",
                table: "AccountTransactions",
                columns: new[] { "TenantId", "JournalEntryId", "AccountingBookId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AccountTransactions_AccountingBooks_TenantId_AccountingBookId",
                table: "AccountTransactions",
                columns: new[] { "TenantId", "AccountingBookId" },
                principalTable: "AccountingBooks",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountTransactions_JournalEntries_TenantId_JournalEntryId_AccountingBookId",
                table: "AccountTransactions",
                columns: new[] { "TenantId", "JournalEntryId", "AccountingBookId" },
                principalTable: "JournalEntries",
                principalColumns: new[] { "TenantId", "Id", "AccountingBookId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FinancePostingEvents_AccountingBooks_TenantId_AccountingBookId",
                table: "FinancePostingEvents",
                columns: new[] { "TenantId", "AccountingBookId" },
                principalTable: "AccountingBooks",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FinancePostingEvents_JournalEntries_TenantId_JournalEntryId_AccountingBookId",
                table: "FinancePostingEvents",
                columns: new[] { "TenantId", "JournalEntryId", "AccountingBookId" },
                principalTable: "JournalEntries",
                principalColumns: new[] { "TenantId", "Id", "AccountingBookId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntries_AccountingBooks_TenantId_AccountingBookId",
                table: "JournalEntries",
                columns: new[] { "TenantId", "AccountingBookId" },
                principalTable: "AccountingBooks",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // C1 permits book-qualified identities at the schema level. Downgrade is bounded to
            // databases that still satisfy the predecessor's tenant-global uniqueness contract;
            // otherwise dropping book identity would conflate retained representations.
            migrationBuilder.Sql("""
IF EXISTS (
    SELECT 1 FROM FinancePostingEvents
    WHERE IsDeleted = 0
    GROUP BY TenantId, SourceDocumentType, SourceDocumentId, PostingAction
    HAVING COUNT_BIG(*) > 1)
 OR EXISTS (
    SELECT 1 FROM FinancePostingEvents
    WHERE IsDeleted = 0 AND IdempotencyKey IS NOT NULL
    GROUP BY TenantId, IdempotencyKey
    HAVING COUNT_BIG(*) > 1)
    THROW 51000, 'C1_BOOK_ID_DOWN_BLOCKED: book-qualified posting identities cannot be collapsed into the predecessor uniqueness contract.', 1;
""");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountTransactions_AccountingBooks_TenantId_AccountingBookId",
                table: "AccountTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountTransactions_JournalEntries_TenantId_JournalEntryId_AccountingBookId",
                table: "AccountTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_FinancePostingEvents_AccountingBooks_TenantId_AccountingBookId",
                table: "FinancePostingEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_FinancePostingEvents_JournalEntries_TenantId_JournalEntryId_AccountingBookId",
                table: "FinancePostingEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntries_AccountingBooks_TenantId_AccountingBookId",
                table: "JournalEntries");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_JournalEntries_TenantId_Id_AccountingBookId",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_TenantId_AccountingBookId",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_FinancePostingEvents_TenantId_AccountingBookId_IdempotencyKey",
                table: "FinancePostingEvents");

            migrationBuilder.DropIndex(
                name: "IX_FinancePostingEvents_TenantId_AccountingBookId_SourceDocumentType_SourceDocumentId_PostingAction",
                table: "FinancePostingEvents");

            migrationBuilder.DropIndex(
                name: "IX_FinancePostingEvents_TenantId_AccountingBookId_SourceModule_SourceDocumentType_SourceDocumentId_PostingAction",
                table: "FinancePostingEvents");

            migrationBuilder.DropIndex(
                name: "IX_FinancePostingEvents_TenantId_JournalEntryId_AccountingBookId",
                table: "FinancePostingEvents");

            migrationBuilder.DropIndex(
                name: "IX_AccountTransactions_TenantId_AccountingBookId_TransactionDate_AccountId",
                table: "AccountTransactions");

            migrationBuilder.DropIndex(
                name: "IX_AccountTransactions_TenantId_JournalEntryId_AccountingBookId",
                table: "AccountTransactions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_AccountingBooks_TenantId_Id",
                table: "AccountingBooks");

            migrationBuilder.DropColumn(
                name: "AccountingBookId",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "AccountingBookId",
                table: "FinancePostingEvents");

            migrationBuilder.DropColumn(
                name: "AccountingBookId",
                table: "AccountTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePostingEvents_TenantId_IdempotencyKey",
                table: "FinancePostingEvents",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePostingEvents_TenantId_SourceDocumentType_SourceDocumentId_PostingAction",
                table: "FinancePostingEvents",
                columns: new[] { "TenantId", "SourceDocumentType", "SourceDocumentId", "PostingAction" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePostingEvents_TenantId_SourceModule_SourceDocumentType_SourceDocumentId_PostingAction",
                table: "FinancePostingEvents",
                columns: new[] { "TenantId", "SourceModule", "SourceDocumentType", "SourceDocumentId", "PostingAction" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_JournalEntryId",
                table: "AccountTransactions",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_TenantId_BookClassification_TransactionDate_AccountId",
                table: "AccountTransactions",
                columns: new[] { "TenantId", "BookClassification", "TransactionDate", "AccountId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AccountTransactions_JournalEntries_JournalEntryId",
                table: "AccountTransactions",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FinancePostingEvents_JournalEntries_JournalEntryId",
                table: "FinancePostingEvents",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
