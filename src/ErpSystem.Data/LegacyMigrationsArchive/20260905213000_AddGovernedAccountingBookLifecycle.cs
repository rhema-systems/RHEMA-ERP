using System;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260905213000_AddGovernedAccountingBookLifecycle")]
public partial class AddGovernedAccountingBookLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // This must remain the first operation. C1 made the relational book ID authoritative, so C3
        // may classify legacy rows only after proving their retained code, tenant, currency and posting
        // evidence are byte-exact. Do not replace BIN2/length checks with database-default equality.
        migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM [AccountingBooks] b
    WHERE b.[IsDeleted] = 0 AND (
        b.[Code] IS NULL OR LEN(b.[Code]) = 0
        OR b.[Code] COLLATE Latin1_General_100_BIN2
           <> UPPER(LTRIM(RTRIM(b.[Code]))) COLLATE Latin1_General_100_BIN2
        OR DATALENGTH(b.[Code]) <> DATALENGTH(UPPER(LTRIM(RTRIM(b.[Code])))
        OR LEFT(b.[Code], 1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
        OR b.[Code] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_]%'
        OR b.[Code] COLLATE Latin1_General_100_BIN2 IN (
            N'ALL' COLLATE Latin1_General_100_BIN2,
            N'ALL_ACTIVE_BOOKS' COLLATE Latin1_General_100_BIN2,
            N'ALL_CLASSIFIED_BOOKS' COLLATE Latin1_General_100_BIN2,
            N'ALLCLASSIFIEDBOOKS' COLLATE Latin1_General_100_BIN2)))
    THROW 51000, 'C3_BOOK_PREFLIGHT: every retained accounting-book code must be one canonical concrete code.', 1;

IF EXISTS (
    SELECT b.[TenantId]
    FROM [AccountingBooks] b
    WHERE b.[IsDeleted] = 0
    GROUP BY b.[TenantId]
    HAVING SUM(CASE WHEN b.[IsDefault] = 1 THEN 1 ELSE 0 END) <> 1)
    THROW 51000, 'C3_PRIMARY_PREFLIGHT: each tenant with accounting books must have exactly one unambiguous default/primary row.', 1;

-- Up preserves the predecessor posting flags byte-for-byte. A mismatched pair has no truthful C3
-- lifecycle equivalent and cannot be normalized without making Down lossy.
IF EXISTS (
    SELECT 1 FROM [AccountingBooks]
    WHERE [IsDeleted] = 0 AND [IsActive] <> [AllowsPosting])
    THROW 51000, 'C3_LIFECYCLE_PREFLIGHT: legacy active/posting flags are inconsistent and require remediation.', 1;

-- These are copied onto governed full books. Validate every live source row before adding
-- constraints, including tenants not yet using Finance, so Up cannot partially mutate.
IF EXISTS (
    SELECT 1 FROM [Tenants] t
    WHERE t.[IsDeleted] = 0 AND (t.[BaseCurrency] IS NULL OR DATALENGTH(t.[BaseCurrency]) <> 6
       OR t.[BaseCurrency] COLLATE Latin1_General_100_BIN2
          <> UPPER(LTRIM(RTRIM(t.[BaseCurrency]))) COLLATE Latin1_General_100_BIN2
       OR t.[BaseCurrency] COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z][A-Z][A-Z]'))
    THROW 51000, 'C3_CURRENCY_PREFLIGHT: tenant functional currency must be exactly three uppercase ASCII letters.', 1;

IF EXISTS (
    SELECT 1 FROM [FinanceSettings] fs
    WHERE fs.[IsDeleted] = 0 AND (fs.[BaseCurrency] IS NULL OR DATALENGTH(fs.[BaseCurrency]) <> 6
       OR fs.[BaseCurrency] COLLATE Latin1_General_100_BIN2
          <> UPPER(LTRIM(RTRIM(fs.[BaseCurrency]))) COLLATE Latin1_General_100_BIN2
       OR fs.[BaseCurrency] COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z][A-Z][A-Z]'))
    THROW 51000, 'C3_CURRENCY_PREFLIGHT: Finance settings currency must be exactly three uppercase ASCII letters.', 1;

IF EXISTS (
    SELECT 1
    FROM [AccountingBooks] b
    LEFT JOIN [Tenants] t ON t.[Id] = b.[TenantId] AND t.[IsDeleted] = 0
    OUTER APPLY (
        SELECT COUNT_BIG(*) AS MatchCount, MAX(fs.[BaseCurrency]) AS BaseCurrency
        FROM [FinanceSettings] fs
        WHERE fs.[TenantId] = b.[TenantId] AND fs.[IsDeleted] = 0) configured
    WHERE b.[IsDeleted] = 0 AND (
        t.[Id] IS NULL OR t.[BaseCurrency] IS NULL OR DATALENGTH(t.[BaseCurrency]) <> 6
        OR t.[BaseCurrency] COLLATE Latin1_General_100_BIN2
           <> UPPER(LTRIM(RTRIM(t.[BaseCurrency]))) COLLATE Latin1_General_100_BIN2
        OR DATALENGTH(t.[BaseCurrency]) <> DATALENGTH(UPPER(LTRIM(RTRIM(t.[BaseCurrency])))
        OR configured.MatchCount <> 1
        OR (configured.MatchCount = 1 AND (
            configured.BaseCurrency IS NULL OR DATALENGTH(configured.BaseCurrency) <> 6
            OR configured.BaseCurrency COLLATE Latin1_General_100_BIN2
               <> UPPER(LTRIM(RTRIM(configured.BaseCurrency))) COLLATE Latin1_General_100_BIN2
            OR configured.BaseCurrency COLLATE Latin1_General_100_BIN2
               <> t.[BaseCurrency] COLLATE Latin1_General_100_BIN2
            OR configured.BaseCurrency COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z][A-Z][A-Z]'
            OR DATALENGTH(configured.BaseCurrency) <> DATALENGTH(t.[BaseCurrency])))))
    THROW 51000, 'C3_CURRENCY_PREFLIGHT: full-book functional currency authority must be canonical and agree with Finance settings.', 1;

IF EXISTS (
    SELECT 1
    FROM [JournalEntries] j
    LEFT JOIN [AccountingBooks] b ON b.[TenantId] = j.[TenantId] AND b.[Id] = j.[AccountingBookId] AND b.[IsDeleted] = 0
    WHERE j.[IsDeleted] = 0 AND (
        b.[Id] IS NULL
        OR j.[BookClassification] COLLATE Latin1_General_100_BIN2 <> b.[Code] COLLATE Latin1_General_100_BIN2
        OR DATALENGTH(j.[BookClassification]) <> DATALENGTH(b.[Code])))
    THROW 51000, 'C3_USE_PREFLIGHT: journal book identity is unknown, cross-tenant or not byte-exact.', 1;

IF EXISTS (
    SELECT 1
    FROM [AccountTransactions] tx
    LEFT JOIN [AccountingBooks] b ON b.[TenantId] = tx.[TenantId] AND b.[Id] = tx.[AccountingBookId] AND b.[IsDeleted] = 0
    LEFT JOIN [JournalEntries] j ON j.[TenantId] = tx.[TenantId] AND j.[Id] = tx.[JournalEntryId]
       AND j.[AccountingBookId] = tx.[AccountingBookId]
    WHERE tx.[IsDeleted] = 0 AND (
        b.[Id] IS NULL OR j.[Id] IS NULL
        OR tx.[BookClassification] COLLATE Latin1_General_100_BIN2 <> b.[Code] COLLATE Latin1_General_100_BIN2
        OR DATALENGTH(tx.[BookClassification]) <> DATALENGTH(b.[Code])
        OR j.[BookClassification] COLLATE Latin1_General_100_BIN2 <> b.[Code] COLLATE Latin1_General_100_BIN2
        OR DATALENGTH(j.[BookClassification]) <> DATALENGTH(b.[Code])))
    THROW 51000, 'C3_USE_PREFLIGHT: transaction/header/book lineage is unknown, cross-tenant or not byte-exact.', 1;

IF EXISTS (
    SELECT 1
    FROM [FinancePostingEvents] e
    LEFT JOIN [AccountingBooks] b ON b.[TenantId] = e.[TenantId] AND b.[Id] = e.[AccountingBookId] AND b.[IsDeleted] = 0
    LEFT JOIN [JournalEntries] j ON j.[TenantId] = e.[TenantId] AND j.[Id] = e.[JournalEntryId]
       AND j.[AccountingBookId] = e.[AccountingBookId]
    WHERE e.[IsDeleted] = 0 AND (
        b.[Id] IS NULL OR j.[Id] IS NULL
        OR e.[BookClassification] COLLATE Latin1_General_100_BIN2 <> b.[Code] COLLATE Latin1_General_100_BIN2
        OR DATALENGTH(e.[BookClassification]) <> DATALENGTH(b.[Code])
        OR j.[BookClassification] COLLATE Latin1_General_100_BIN2 <> b.[Code] COLLATE Latin1_General_100_BIN2
        OR DATALENGTH(j.[BookClassification]) <> DATALENGTH(b.[Code])))
    THROW 51000, 'C3_USE_PREFLIGHT: posting-event/header/book lineage is unknown, cross-tenant or not byte-exact.', 1;
");

        migrationBuilder.AddColumn<Guid>(name: "BaseAccountingBookId", table: "AccountingBooks", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<int>(name: "BookType", table: "AccountingBooks", type: "int", nullable: false, defaultValue: 2);
        migrationBuilder.AddColumn<DateTime>(name: "EffectiveFromUtc", table: "AccountingBooks", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "EffectiveToUtc", table: "AccountingBooks", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<string>(name: "FunctionalCurrencyCode", table: "AccountingBooks", type: "nvarchar(3)", maxLength: 3, nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "InitializationStartedAtUtc", table: "AccountingBooks", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<int>(name: "LifecycleStatus", table: "AccountingBooks", type: "int", nullable: false, defaultValue: 2);
        migrationBuilder.AddColumn<int>(name: "PendingLifecycleStatus", table: "AccountingBooks", type: "int", nullable: true);
        migrationBuilder.AddColumn<string>(name: "PendingTransitionReason", table: "AccountingBooks", type: "nvarchar(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "AccountingBooks", type: "rowversion", rowVersion: true, nullable: false);
        migrationBuilder.AddColumn<DateTime>(name: "TransitionDecidedAtUtc", table: "AccountingBooks", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "TransitionDecidedByUserId", table: "AccountingBooks", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>(name: "TransitionDecisionReason", table: "AccountingBooks", type: "nvarchar(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "TransitionRequestedAtUtc", table: "AccountingBooks", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "TransitionRequestedByUserId", table: "AccountingBooks", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "TransitionWorkflowInstanceId", table: "AccountingBooks", type: "uniqueidentifier", nullable: true);

        // Existing postable rows are grandfathered as Active solely to preserve the C1 leaf executor.
        // New seeded/configured rows start non-posting, and C3 exposes no path that can satisfy C4 readiness.
        migrationBuilder.Sql(@"
UPDATE b
SET b.[BookType] = CASE WHEN b.[IsDefault] = 1 THEN 1 ELSE 2 END,
    b.[LifecycleStatus] = CASE
        WHEN b.[IsActive] = 1 AND b.[AllowsPosting] = 1 THEN 4
        WHEN EXISTS (SELECT 1 FROM [JournalEntries] j WHERE j.[TenantId] = b.[TenantId] AND j.[AccountingBookId] = b.[Id] AND j.[IsDeleted] = 0) THEN 5
        ELSE 2 END,
    b.[FunctionalCurrencyCode] = t.[BaseCurrency],
    b.[EffectiveFromUtc] = b.[CreatedAt],
    b.[IsActive] = CASE WHEN b.[IsActive] = 1 AND b.[AllowsPosting] = 1 THEN 1 ELSE 0 END,
    b.[AllowsPosting] = CASE WHEN b.[IsActive] = 1 AND b.[AllowsPosting] = 1 THEN 1 ELSE 0 END
FROM [AccountingBooks] b
JOIN [Tenants] t ON t.[Id] = b.[TenantId]
WHERE b.[IsDeleted] = 0;
");

        migrationBuilder.AddCheckConstraint("CK_AccountingBooks_BookType", "AccountingBooks", "[IsDeleted] = 1 OR [BookType] IN (1, 2, 3)");
        migrationBuilder.AddCheckConstraint("CK_AccountingBooks_LifecycleStatus", "AccountingBooks", "[IsDeleted] = 1 OR [LifecycleStatus] IN (1, 2, 3, 4, 5, 6)");
        migrationBuilder.AddCheckConstraint("CK_AccountingBooks_EffectiveDates", "AccountingBooks", "[IsDeleted] = 1 OR [EffectiveToUtc] IS NULL OR [EffectiveFromUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
        migrationBuilder.AddCheckConstraint("CK_AccountingBooks_BaseShape", "AccountingBooks", "[IsDeleted] = 1 OR ([BookType] = 3 AND [BaseAccountingBookId] IS NOT NULL AND [FunctionalCurrencyCode] IS NULL) OR ([BookType] IN (1, 2) AND [BaseAccountingBookId] IS NULL AND [FunctionalCurrencyCode] IS NOT NULL)");
        migrationBuilder.AddCheckConstraint("CK_AccountingBooks_DefaultType", "AccountingBooks", "[IsDeleted] = 1 OR ([BookType] = 1 AND [IsDefault] = 1) OR ([BookType] <> 1 AND [IsDefault] = 0)");
        migrationBuilder.AddCheckConstraint("CK_AccountingBooks_NoSelfBase", "AccountingBooks", "[IsDeleted] = 1 OR [BaseAccountingBookId] IS NULL OR [BaseAccountingBookId] <> [Id]");
        migrationBuilder.AddCheckConstraint("CK_AccountingBooks_PostingLifecycle", "AccountingBooks", "[IsDeleted] = 1 OR ([LifecycleStatus] = 4 AND [IsActive] = 1 AND [AllowsPosting] = 1) OR ([LifecycleStatus] <> 4 AND [IsActive] = 0 AND [AllowsPosting] = 0)");
        migrationBuilder.AddCheckConstraint("CK_AccountingBooks_CodeCanonical", "AccountingBooks", "[IsDeleted] = 1 OR ([Code] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([Code]))) COLLATE Latin1_General_100_BIN2 AND DATALENGTH([Code]) = DATALENGTH(UPPER(LTRIM(RTRIM([Code])))) AND LEFT([Code], 1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [Code] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_]%' AND [Code] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL', N'ALL_ACTIVE_BOOKS', N'ALL_CLASSIFIED_BOOKS', N'ALLCLASSIFIEDBOOKS'))");
        migrationBuilder.AddCheckConstraint("CK_AccountingBooks_FunctionalCurrencyCanonical", "AccountingBooks", "[IsDeleted] = 1 OR [FunctionalCurrencyCode] IS NULL OR (DATALENGTH([FunctionalCurrencyCode]) = 6 AND [FunctionalCurrencyCode] COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z][A-Z][A-Z]')");
        migrationBuilder.AddCheckConstraint("CK_Tenants_BaseCurrencyCanonical_C3", "Tenants", "[IsDeleted] = 1 OR (DATALENGTH([BaseCurrency]) = 6 AND [BaseCurrency] COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z][A-Z][A-Z]')");
        migrationBuilder.AddCheckConstraint("CK_FinanceSettings_BaseCurrencyCanonical_C3", "FinanceSettings", "[IsDeleted] = 1 OR (DATALENGTH([BaseCurrency]) = 6 AND [BaseCurrency] COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z][A-Z][A-Z]')");

        migrationBuilder.CreateIndex(name: "IX_AccountingBooks_TenantId_BaseAccountingBookId", table: "AccountingBooks", columns: new[] { "TenantId", "BaseAccountingBookId" });
        migrationBuilder.CreateIndex(name: "IX_AccountingBooks_TenantId_IsDefault", table: "AccountingBooks", columns: new[] { "TenantId", "IsDefault" }, unique: true, filter: "[IsDeleted] = 0 AND [IsDefault] = 1");
        migrationBuilder.AddForeignKey(name: "FK_AccountingBooks_AccountingBooks_TenantId_BaseAccountingBookId", table: "AccountingBooks", columns: new[] { "TenantId", "BaseAccountingBookId" }, principalTable: "AccountingBooks", principalColumns: new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Delta ancestry and in-flight maker/checker evidence cannot be represented by the predecessor.
        // Refuse lossy downgrade rather than silently converting them into unrelated full-book rows.
        migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [AccountingBooks] WHERE [IsDeleted] = 0 AND ([BookType] = 3 OR [BaseAccountingBookId] IS NOT NULL))
    THROW 51000, 'C3_DOWN_GUARD: Delta/base-book structure cannot be represented by the predecessor schema.', 1;
IF EXISTS (SELECT 1 FROM [AccountingBooks] WHERE [PendingLifecycleStatus] IS NOT NULL OR [TransitionWorkflowInstanceId] IS NOT NULL)
    THROW 51000, 'C3_DOWN_GUARD: pending lifecycle governance evidence must be resolved before downgrade.', 1;
");

        migrationBuilder.DropForeignKey("FK_AccountingBooks_AccountingBooks_TenantId_BaseAccountingBookId", "AccountingBooks");
        migrationBuilder.DropCheckConstraint("CK_AccountingBooks_BaseShape", "AccountingBooks");
        migrationBuilder.DropCheckConstraint("CK_AccountingBooks_BookType", "AccountingBooks");
        migrationBuilder.DropCheckConstraint("CK_AccountingBooks_DefaultType", "AccountingBooks");
        migrationBuilder.DropCheckConstraint("CK_AccountingBooks_EffectiveDates", "AccountingBooks");
        migrationBuilder.DropCheckConstraint("CK_AccountingBooks_LifecycleStatus", "AccountingBooks");
        migrationBuilder.DropCheckConstraint("CK_AccountingBooks_NoSelfBase", "AccountingBooks");
        migrationBuilder.DropCheckConstraint("CK_AccountingBooks_PostingLifecycle", "AccountingBooks");
        migrationBuilder.DropCheckConstraint("CK_AccountingBooks_CodeCanonical", "AccountingBooks");
        migrationBuilder.DropCheckConstraint("CK_AccountingBooks_FunctionalCurrencyCanonical", "AccountingBooks");
        migrationBuilder.DropCheckConstraint("CK_Tenants_BaseCurrencyCanonical_C3", "Tenants");
        migrationBuilder.DropCheckConstraint("CK_FinanceSettings_BaseCurrencyCanonical_C3", "FinanceSettings");
        migrationBuilder.DropIndex("IX_AccountingBooks_TenantId_BaseAccountingBookId", "AccountingBooks");
        migrationBuilder.DropIndex("IX_AccountingBooks_TenantId_IsDefault", "AccountingBooks");

        migrationBuilder.DropColumn("BaseAccountingBookId", "AccountingBooks");
        migrationBuilder.DropColumn("BookType", "AccountingBooks");
        migrationBuilder.DropColumn("EffectiveFromUtc", "AccountingBooks");
        migrationBuilder.DropColumn("EffectiveToUtc", "AccountingBooks");
        migrationBuilder.DropColumn("FunctionalCurrencyCode", "AccountingBooks");
        migrationBuilder.DropColumn("InitializationStartedAtUtc", "AccountingBooks");
        migrationBuilder.DropColumn("LifecycleStatus", "AccountingBooks");
        migrationBuilder.DropColumn("PendingLifecycleStatus", "AccountingBooks");
        migrationBuilder.DropColumn("PendingTransitionReason", "AccountingBooks");
        migrationBuilder.DropColumn("RowVersion", "AccountingBooks");
        migrationBuilder.DropColumn("TransitionDecidedAtUtc", "AccountingBooks");
        migrationBuilder.DropColumn("TransitionDecidedByUserId", "AccountingBooks");
        migrationBuilder.DropColumn("TransitionDecisionReason", "AccountingBooks");
        migrationBuilder.DropColumn("TransitionRequestedAtUtc", "AccountingBooks");
        migrationBuilder.DropColumn("TransitionRequestedByUserId", "AccountingBooks");
        migrationBuilder.DropColumn("TransitionWorkflowInstanceId", "AccountingBooks");
    }
}
