using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookAwareBalanceFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AccountBalances is an older dormant projection. Resolve its immutable book snapshot only
            // when byte-exact same-tenant book, account, period and functional-currency evidence is
            // unambiguous. This preflight precedes every schema/data mutation so retained development
            // databases fail closed rather than silently laundering legacy evidence through CI collation.
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM [AccountBalances] ab
    WHERE ab.[BookClassification] IS NULL OR LEN(ab.[BookClassification]) = 0
       OR ab.[BookClassification] = 'ALL_ACTIVE_BOOKS'
       OR ab.[Currency] IS NULL OR DATALENGTH(ab.[Currency]) <> 6)
    THROW 51000, 'C2_ACCOUNT_BALANCE_PREFLIGHT: blank/pseudo book or invalid functional currency evidence.', 1;

IF EXISTS (
    SELECT a.[TenantId]
    FROM [Accounts] a
    WHERE a.[IsDeleted] = 0
    GROUP BY a.[TenantId]
    HAVING (SELECT COUNT_BIG(*) FROM [AccountingBooks] b
        WHERE b.[TenantId] = a.[TenantId] AND b.[IsDeleted] = 0
          AND b.[IsDefault] = 1 AND b.[IsActive] = 1 AND b.[AllowsPosting] = 1) <> 1)
    THROW 51000, 'C2_PRIMARY_BOOK_PREFLIGHT: each Finance tenant requires exactly one active default posting book.', 1;

IF EXISTS (
    SELECT 1 FROM [AccountBalances] ab
    JOIN [Tenants] t ON t.[Id] = ab.[TenantId] AND t.[IsDeleted] = 0
    OUTER APPLY (SELECT COUNT_BIG(*) MatchCount, MAX(fs.[BaseCurrency]) BaseCurrency
        FROM [FinanceSettings] fs WHERE fs.[TenantId] = ab.[TenantId] AND fs.[IsDeleted] = 0) configured
    CROSS APPLY (SELECT CASE WHEN configured.MatchCount = 1 THEN configured.BaseCurrency ELSE t.[BaseCurrency] END FunctionalCurrency) authority
    WHERE configured.MatchCount > 1 OR authority.FunctionalCurrency IS NULL
       OR DATALENGTH(authority.FunctionalCurrency) <> 6
       OR ab.[Currency] COLLATE Latin1_General_100_BIN2 <> authority.FunctionalCurrency COLLATE Latin1_General_100_BIN2
       OR DATALENGTH(ab.[Currency]) <> DATALENGTH(authority.FunctionalCurrency))
    THROW 51000, 'C2_ACCOUNT_BALANCE_PREFLIGHT: balance currency does not exactly match tenant functional-currency authority.', 1;

IF EXISTS (
    SELECT 1 FROM [AccountBalances] ab
    OUTER APPLY (SELECT COUNT_BIG(*) MatchCount FROM [AccountingBooks] b
        WHERE b.[TenantId] = ab.[TenantId] AND b.[IsDeleted] = 0
          AND b.[Code] COLLATE Latin1_General_100_BIN2 = ab.[BookClassification] COLLATE Latin1_General_100_BIN2
          AND DATALENGTH(b.[Code]) = DATALENGTH(ab.[BookClassification])) exactBook
    WHERE exactBook.MatchCount <> 1
       OR NOT EXISTS (SELECT 1 FROM [Accounts] a WHERE a.[TenantId] = ab.[TenantId] AND a.[Id] = ab.[AccountId])
       OR NOT EXISTS (SELECT 1 FROM [FiscalPeriods] p WHERE p.[TenantId] = ab.[TenantId] AND p.[Id] = ab.[FiscalPeriodId]))
    THROW 51000, 'C2_ACCOUNT_BALANCE_PREFLIGHT: book/account/period evidence is unknown, ambiguous, noncanonical, or cross-tenant.', 1;

IF EXISTS (
    SELECT 1 FROM [AccountBalances] ab
    JOIN [AccountingBooks] b ON b.[TenantId] = ab.[TenantId] AND b.[IsDeleted] = 0
      AND b.[Code] COLLATE Latin1_General_100_BIN2 = ab.[BookClassification] COLLATE Latin1_General_100_BIN2
      AND DATALENGTH(b.[Code]) = DATALENGTH(ab.[BookClassification])
    GROUP BY ab.[TenantId], ab.[AccountId], b.[Id], ab.[FiscalPeriodId], ab.[Currency]
    HAVING COUNT_BIG(*) > 1)
    THROW 51000, 'C2_ACCOUNT_BALANCE_PREFLIGHT: duplicate rows collide at the exact book balance grain.', 1;

IF EXISTS (
    SELECT 1
    FROM [AccountTransactions] tx
    JOIN [JournalEntries] j ON j.[Id] = tx.[JournalEntryId]
    LEFT JOIN [AccountingBooks] b ON b.[Id] = tx.[AccountingBookId] AND b.[TenantId] = tx.[TenantId]
    WHERE tx.[IsDeleted] = 0 AND (tx.[PostingStatus] = N'Posted' OR j.[PostingStatus] = N'Posted')
      AND (b.[Id] IS NULL OR j.[TenantId] <> tx.[TenantId]
       OR j.[AccountingBookId] <> tx.[AccountingBookId]
       OR tx.[BookClassification] COLLATE Latin1_General_100_BIN2 <> b.[Code] COLLATE Latin1_General_100_BIN2
       OR DATALENGTH(tx.[BookClassification]) <> DATALENGTH(b.[Code])
       OR j.[BookClassification] COLLATE Latin1_General_100_BIN2 <> b.[Code] COLLATE Latin1_General_100_BIN2
       OR DATALENGTH(j.[BookClassification]) <> DATALENGTH(b.[Code])
       OR tx.[PostingStatus] <> N'Posted' OR j.[PostingStatus] <> N'Posted'))
    THROW 51000, 'C2_PRIMARY_BALANCE_PREFLIGHT: posted journal line book/status evidence is inconsistent.', 1;");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountBalances_Accounts_AccountId",
                table: "AccountBalances");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountBalances_FiscalPeriods_FiscalPeriodId",
                table: "AccountBalances");

            migrationBuilder.DropIndex(
                name: "IX_FiscalPeriods_TenantId",
                table: "FiscalPeriods");

            migrationBuilder.DropIndex(
                name: "IX_AccountBalances_AccountId",
                table: "AccountBalances");

            migrationBuilder.DropIndex(
                name: "IX_AccountBalances_FiscalPeriodId",
                table: "AccountBalances");

            migrationBuilder.DropIndex(
                name: "IX_AccountBalances_TenantId_AccountId_FiscalPeriodId_BookClassification_Currency",
                table: "AccountBalances");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "AccountBalances",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AccountingBookId",
                table: "AccountBalances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE ab
SET ab.[AccountingBookId] = b.[Id]
FROM [AccountBalances] ab
JOIN [AccountingBooks] b ON b.[TenantId] = ab.[TenantId] AND b.[IsDeleted] = 0
  AND b.[Code] COLLATE Latin1_General_100_BIN2 = ab.[BookClassification] COLLATE Latin1_General_100_BIN2
  AND DATALENGTH(b.[Code]) = DATALENGTH(ab.[BookClassification]);");

            // Account.Balance is retained only as primary-book compatibility. Recompute from immutable
            // posted header/line evidence so pre-C2 alternative-book amounts cannot remain commingled.
            migrationBuilder.Sql(@"
UPDATE a
SET a.[Balance] = CASE WHEN a.[AccountType] IN (2,3,4)
    THEN -COALESCE(primaryEvidence.[SignedBalance], 0)
    ELSE COALESCE(primaryEvidence.[SignedBalance], 0) END
FROM [Accounts] a
JOIN [AccountingBooks] primaryBook ON primaryBook.[TenantId] = a.[TenantId]
  AND primaryBook.[IsDefault] = 1 AND primaryBook.[IsActive] = 1
  AND primaryBook.[AllowsPosting] = 1 AND primaryBook.[IsDeleted] = 0
OUTER APPLY (
    SELECT SUM(tx.[DebitAmount] - tx.[CreditAmount]) SignedBalance
    FROM [AccountTransactions] tx
    JOIN [JournalEntries] j ON j.[Id] = tx.[JournalEntryId]
      AND j.[TenantId] = tx.[TenantId] AND j.[AccountingBookId] = tx.[AccountingBookId]
      AND j.[PostingStatus] = N'Posted' AND j.[IsDeleted] = 0
    WHERE tx.[TenantId] = a.[TenantId] AND tx.[AccountId] = a.[Id]
      AND tx.[AccountingBookId] = primaryBook.[Id]
      AND tx.[PostingStatus] = N'Posted' AND tx.[IsDeleted] = 0
) primaryEvidence
WHERE a.[IsDeleted] = 0;");

            migrationBuilder.AlterColumn<Guid>(
                name: "AccountingBookId",
                table: "AccountBalances",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_FiscalPeriods_TenantId_Id",
                table: "FiscalPeriods",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "AccountCurrencyExposures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountingBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountingBookCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FunctionalCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TransactionCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SignedForeignBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SignedFunctionalBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionCount = table.Column<int>(type: "int", nullable: false),
                    FirstTransactionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastTransactionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRebuiltAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountCurrencyExposures", x => x.Id);
                    table.CheckConstraint("CK_AccountCurrencyExposures_DistinctCurrency", "[TransactionCurrencyCode] <> [FunctionalCurrencyCode]");
                    table.CheckConstraint("CK_AccountCurrencyExposures_TransactionCount", "[TransactionCount] >= 0");
                    table.ForeignKey(
                        name: "FK_AccountCurrencyExposures_AccountingBooks_TenantId_AccountingBookId",
                        columns: x => new { x.TenantId, x.AccountingBookId },
                        principalTable: "AccountingBooks",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountCurrencyExposures_Accounts_TenantId_AccountId",
                        columns: x => new { x.TenantId, x.AccountId },
                        principalTable: "Accounts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountCurrencyExposures_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceBalanceRebuildRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountingBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountingBookCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SourceFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CommandFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BalanceRows = table.Column<int>(type: "int", nullable: false),
                    ExposureRows = table.Column<int>(type: "int", nullable: false),
                    AbsoluteDrift = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceBalanceRebuildRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceBalanceRebuildRuns_AccountingBooks_TenantId_AccountingBookId",
                        columns: x => new { x.TenantId, x.AccountingBookId },
                        principalTable: "AccountingBooks",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceBalanceRebuildRuns_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalances_TenantId_AccountId_AccountingBookId_FiscalPeriodId_Currency",
                table: "AccountBalances",
                columns: new[] { "TenantId", "AccountId", "AccountingBookId", "FiscalPeriodId", "Currency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalances_TenantId_AccountingBookId",
                table: "AccountBalances",
                columns: new[] { "TenantId", "AccountingBookId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountCurrencyExposures_TenantId_AccountId_AccountingBookId_TransactionCurrencyCode",
                table: "AccountCurrencyExposures",
                columns: new[] { "TenantId", "AccountId", "AccountingBookId", "TransactionCurrencyCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AccountCurrencyExposures_TenantId_AccountingBookId",
                table: "AccountCurrencyExposures",
                columns: new[] { "TenantId", "AccountingBookId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBalanceRebuildRuns_TenantId_AccountingBookId_IdempotencyKey",
                table: "FinanceBalanceRebuildRuns",
                columns: new[] { "TenantId", "AccountingBookId", "IdempotencyKey" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountBalances_AccountingBooks_TenantId_AccountingBookId",
                table: "AccountBalances",
                columns: new[] { "TenantId", "AccountingBookId" },
                principalTable: "AccountingBooks",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountBalances_Accounts_TenantId_AccountId",
                table: "AccountBalances",
                columns: new[] { "TenantId", "AccountId" },
                principalTable: "Accounts",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountBalances_FiscalPeriods_TenantId_FiscalPeriodId",
                table: "AccountBalances",
                columns: new[] { "TenantId", "FiscalPeriodId" },
                principalTable: "FiscalPeriods",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The predecessor Account.Balance contract combined every posted representation. Restore
            // that exact compatibility shape on downgrade; do not leave a primary-only value under
            // predecessor code that still assumes the historical combined coordinate.
            migrationBuilder.Sql(@"
UPDATE a
SET a.[Balance] = CASE WHEN a.[AccountType] IN (2,3,4)
    THEN -COALESCE(allEvidence.[SignedBalance], 0)
    ELSE COALESCE(allEvidence.[SignedBalance], 0) END
FROM [Accounts] a
OUTER APPLY (
    SELECT SUM(tx.[DebitAmount] - tx.[CreditAmount]) SignedBalance
    FROM [AccountTransactions] tx
    JOIN [JournalEntries] j ON j.[Id] = tx.[JournalEntryId]
      AND j.[TenantId] = tx.[TenantId] AND j.[AccountingBookId] = tx.[AccountingBookId]
      AND j.[PostingStatus] = N'Posted' AND j.[IsDeleted] = 0
    WHERE tx.[TenantId] = a.[TenantId] AND tx.[AccountId] = a.[Id]
      AND tx.[PostingStatus] = N'Posted' AND tx.[IsDeleted] = 0
) allEvidence
WHERE a.[IsDeleted] = 0;");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountBalances_AccountingBooks_TenantId_AccountingBookId",
                table: "AccountBalances");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountBalances_Accounts_TenantId_AccountId",
                table: "AccountBalances");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountBalances_FiscalPeriods_TenantId_FiscalPeriodId",
                table: "AccountBalances");

            migrationBuilder.DropTable(
                name: "AccountCurrencyExposures");

            migrationBuilder.DropTable(
                name: "FinanceBalanceRebuildRuns");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_FiscalPeriods_TenantId_Id",
                table: "FiscalPeriods");

            migrationBuilder.DropIndex(
                name: "IX_AccountBalances_TenantId_AccountId_AccountingBookId_FiscalPeriodId_Currency",
                table: "AccountBalances");

            migrationBuilder.DropIndex(
                name: "IX_AccountBalances_TenantId_AccountingBookId",
                table: "AccountBalances");

            migrationBuilder.DropColumn(
                name: "AccountingBookId",
                table: "AccountBalances");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "AccountBalances",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3);

            migrationBuilder.CreateIndex(
                name: "IX_FiscalPeriods_TenantId",
                table: "FiscalPeriods",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalances_AccountId",
                table: "AccountBalances",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalances_FiscalPeriodId",
                table: "AccountBalances",
                column: "FiscalPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalances_TenantId_AccountId_FiscalPeriodId_BookClassification_Currency",
                table: "AccountBalances",
                columns: new[] { "TenantId", "AccountId", "FiscalPeriodId", "BookClassification", "Currency" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountBalances_Accounts_AccountId",
                table: "AccountBalances",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountBalances_FiscalPeriods_FiscalPeriodId",
                table: "AccountBalances",
                column: "FiscalPeriodId",
                principalTable: "FiscalPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
