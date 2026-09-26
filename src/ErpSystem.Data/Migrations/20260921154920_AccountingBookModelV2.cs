using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AccountingBookModelV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_TenantId_AccountingBookId",
                table: "JournalEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountingBooks_BaseShape",
                table: "AccountingBooks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountingBooks_PostingLifecycle",
                table: "AccountingBooks");

            migrationBuilder.AddColumn<Guid>(
                name: "ReplicatedFromJournalEntryId",
                table: "JournalEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReplicationExchangeRate",
                table: "JournalEntries",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplicationExchangeRateId",
                table: "JournalEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReplicationRateDate",
                table: "JournalEntries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplicationRateSource",
                table: "JournalEntries",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrencyRoundingAccountId",
                table: "AccountingBooks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrencyTranslationReserveAccountId",
                table: "AccountingBooks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParallelOpeningMode",
                table: "AccountingBooks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParallelTranslationMethod",
                table: "AccountingBooks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReplicationStartDate",
                table: "AccountingBooks",
                type: "date",
                nullable: true);

            // This release intentionally replaces the former IFRS/Local/Management book model.
            // Do not guess how to mutate live ledgers: existing tenants must be reset or migrated
            // by an explicit, separately reviewed data-conversion plan before this schema is applied.
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [AccountingBooks] WHERE [IsDeleted] = 0)
                    THROW 51000, 'AccountingBookModelV2 requires a fresh database or an approved accounting-book data migration. Existing live accounting books were not modified.', 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_ReplicationExchangeRateId",
                table: "JournalEntries",
                column: "ReplicationExchangeRateId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_TenantId_AccountingBookId_ReplicatedFromJournalEntryId",
                table: "JournalEntries",
                columns: new[] { "TenantId", "AccountingBookId", "ReplicatedFromJournalEntryId" },
                unique: true,
                filter: "[ReplicatedFromJournalEntryId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_TenantId_ReplicatedFromJournalEntryId",
                table: "JournalEntries",
                columns: new[] { "TenantId", "ReplicatedFromJournalEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBooks_TenantId_CurrencyRoundingAccountId",
                table: "AccountingBooks",
                columns: new[] { "TenantId", "CurrencyRoundingAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBooks_TenantId_CurrencyTranslationReserveAccountId",
                table: "AccountingBooks",
                columns: new[] { "TenantId", "CurrencyTranslationReserveAccountId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountingBooks_BaseShape",
                table: "AccountingBooks",
                sql: "[IsDeleted] = 1 OR ([BookType] = 1 AND [BaseAccountingBookId] IS NULL AND [FunctionalCurrencyCode] IS NOT NULL AND [EffectiveFromUtc] IS NULL AND [EffectiveToUtc] IS NULL AND [ReplicationStartDate] IS NULL AND [ParallelOpeningMode] IS NULL AND [ParallelTranslationMethod] IS NULL) OR ([BookType] = 2 AND [BaseAccountingBookId] IS NOT NULL AND [FunctionalCurrencyCode] IS NOT NULL AND [EffectiveFromUtc] IS NULL AND [EffectiveToUtc] IS NULL AND [ReplicationStartDate] IS NOT NULL AND [ParallelOpeningMode] IS NOT NULL) OR ([BookType] = 3 AND [BaseAccountingBookId] IS NOT NULL AND [FunctionalCurrencyCode] IS NULL AND [ReplicationStartDate] IS NULL AND [ParallelOpeningMode] IS NULL AND [ParallelTranslationMethod] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountingBooks_ParallelTranslationMethod",
                table: "AccountingBooks",
                sql: "[IsDeleted] = 1 OR [BookType] <> 2 OR ([ParallelOpeningMode] = 2 AND [ParallelTranslationMethod] IN (1, 2)) OR ([ParallelOpeningMode] IN (1, 3) AND [ParallelTranslationMethod] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountingBooks_PostingLifecycle",
                table: "AccountingBooks",
                sql: "[IsDeleted] = 1 OR ([BookType] = 1 AND [LifecycleStatus] = 4 AND [IsActive] = 1 AND [AllowsPosting] = 1) OR ([BookType] <> 1 AND [LifecycleStatus] = 4 AND [IsActive] = 1 AND [AllowsPosting] = 1) OR ([BookType] <> 1 AND [LifecycleStatus] <> 4 AND [IsActive] = 0 AND [AllowsPosting] = 0)");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountingBooks_Accounts_TenantId_CurrencyRoundingAccountId",
                table: "AccountingBooks",
                columns: new[] { "TenantId", "CurrencyRoundingAccountId" },
                principalTable: "Accounts",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountingBooks_Accounts_TenantId_CurrencyTranslationReserveAccountId",
                table: "AccountingBooks",
                columns: new[] { "TenantId", "CurrencyTranslationReserveAccountId" },
                principalTable: "Accounts",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntries_ExchangeRates_ReplicationExchangeRateId",
                table: "JournalEntries",
                column: "ReplicationExchangeRateId",
                principalTable: "ExchangeRates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntries_JournalEntries_TenantId_ReplicatedFromJournalEntryId",
                table: "JournalEntries",
                columns: new[] { "TenantId", "ReplicatedFromJournalEntryId" },
                principalTable: "JournalEntries",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountingBooks_Accounts_TenantId_CurrencyRoundingAccountId",
                table: "AccountingBooks");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountingBooks_Accounts_TenantId_CurrencyTranslationReserveAccountId",
                table: "AccountingBooks");

            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntries_ExchangeRates_ReplicationExchangeRateId",
                table: "JournalEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntries_JournalEntries_TenantId_ReplicatedFromJournalEntryId",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_ReplicationExchangeRateId",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_TenantId_AccountingBookId_ReplicatedFromJournalEntryId",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_TenantId_ReplicatedFromJournalEntryId",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_AccountingBooks_TenantId_CurrencyRoundingAccountId",
                table: "AccountingBooks");

            migrationBuilder.DropIndex(
                name: "IX_AccountingBooks_TenantId_CurrencyTranslationReserveAccountId",
                table: "AccountingBooks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountingBooks_BaseShape",
                table: "AccountingBooks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountingBooks_ParallelTranslationMethod",
                table: "AccountingBooks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountingBooks_PostingLifecycle",
                table: "AccountingBooks");

            migrationBuilder.DropColumn(
                name: "ReplicatedFromJournalEntryId",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "ReplicationExchangeRate",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "ReplicationExchangeRateId",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "ReplicationRateDate",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "ReplicationRateSource",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "CurrencyRoundingAccountId",
                table: "AccountingBooks");

            migrationBuilder.DropColumn(
                name: "CurrencyTranslationReserveAccountId",
                table: "AccountingBooks");

            migrationBuilder.DropColumn(
                name: "ParallelOpeningMode",
                table: "AccountingBooks");

            migrationBuilder.DropColumn(
                name: "ParallelTranslationMethod",
                table: "AccountingBooks");

            migrationBuilder.DropColumn(
                name: "ReplicationStartDate",
                table: "AccountingBooks");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_TenantId_AccountingBookId",
                table: "JournalEntries",
                columns: new[] { "TenantId", "AccountingBookId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountingBooks_BaseShape",
                table: "AccountingBooks",
                sql: "[IsDeleted] = 1 OR ([BookType] = 3 AND [BaseAccountingBookId] IS NOT NULL AND [FunctionalCurrencyCode] IS NULL) OR ([BookType] IN (1, 2) AND [BaseAccountingBookId] IS NULL AND [FunctionalCurrencyCode] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountingBooks_PostingLifecycle",
                table: "AccountingBooks",
                sql: "[IsDeleted] = 1 OR ([LifecycleStatus] = 4 AND [IsActive] = 1 AND [AllowsPosting] = 1) OR ([LifecycleStatus] <> 4 AND [IsActive] = 0 AND [AllowsPosting] = 0)");
        }
    }
}
