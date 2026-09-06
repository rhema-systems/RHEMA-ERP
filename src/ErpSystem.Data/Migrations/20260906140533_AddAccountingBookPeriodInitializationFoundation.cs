using System;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260906140533_AddAccountingBookPeriodInitializationFoundation")]
    /// <inheritdoc />
    public partial class AddAccountingBookPeriodInitializationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // C4 cannot manufacture initialization or exact-book period evidence for a retained
            // postable book. Keep this complete predecessor audit first so a stamped, drifted or
            // partially-created development database fails before any C4 table is created.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[AccountingBookPeriods]', N'U') IS NOT NULL
   OR OBJECT_ID(N'[AccountingBookInitializations]', N'U') IS NOT NULL
   OR OBJECT_ID(N'[AccountingBookInitializationLines]', N'U') IS NOT NULL
    THROW 51000, 'C4_SCHEMA_PREFLIGHT: C4 authority tables already exist without this migration; reconcile migration history before retrying.', 1;

IF EXISTS (
    SELECT 1 FROM [AccountingBooks] b
    LEFT JOIN [Tenants] t ON t.[Id] = b.[TenantId] AND t.[IsDeleted] = 0
    WHERE b.[IsDeleted] = 0 AND (
        t.[Id] IS NULL
        OR b.[Code] IS NULL OR LEN(b.[Code]) = 0
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
    THROW 51000, 'C4_BOOK_PREFLIGHT: each retained book must have exact tenant lineage and one canonical concrete code.', 1;

IF EXISTS (
    SELECT b.[TenantId]
    FROM [AccountingBooks] b
    WHERE b.[IsDeleted] = 0
    GROUP BY b.[TenantId]
    HAVING SUM(CASE WHEN b.[BookType] = 1 AND b.[IsDefault] = 1 THEN 1 ELSE 0 END) <> 1)
    THROW 51000, 'C4_PRIMARY_PREFLIGHT: each tenant with accounting books requires exactly one primary/default full book.', 1;

IF EXISTS (
    SELECT 1 FROM [AccountingBooks] b
    WHERE b.[IsDeleted] = 0 AND (
        b.[LifecycleStatus] = 4 OR b.[IsActive] = 1 OR b.[AllowsPosting] = 1
        OR b.[PendingLifecycleStatus] = 4))
    THROW 51000, 'C4_READINESS_PREFLIGHT: an Active/postable or pending-Active book has no truthful C4 initialization and period evidence; use the reviewed reset/remediation path.', 1;

IF EXISTS (
    SELECT 1
    FROM [FiscalPeriods] p
    LEFT JOIN [Tenants] t ON t.[Id] = p.[TenantId] AND t.[IsDeleted] = 0
    LEFT JOIN [FiscalYears] y ON y.[Id] = p.[FiscalYearId]
       AND y.[TenantId] = p.[TenantId] AND y.[IsDeleted] = 0
    WHERE p.[IsDeleted] = 0 AND (t.[Id] IS NULL OR y.[Id] IS NULL))
    THROW 51000, 'C4_PERIOD_PREFLIGHT: every live fiscal period must belong to a live fiscal year in the same tenant.', 1;

IF EXISTS (
    SELECT 1
    FROM [AccountingBooks] b
    LEFT JOIN [AccountingBooks] base ON base.[TenantId] = b.[TenantId]
       AND base.[Id] = b.[BaseAccountingBookId] AND base.[IsDeleted] = 0
    WHERE b.[IsDeleted] = 0 AND (
        b.[BookType] NOT IN (1, 2, 3)
        OR b.[LifecycleStatus] NOT IN (1, 2, 3, 4, 5, 6)
        OR (b.[BookType] IN (1, 2) AND (
            b.[BaseAccountingBookId] IS NOT NULL OR b.[FunctionalCurrencyCode] IS NULL
            OR DATALENGTH(b.[FunctionalCurrencyCode]) <> 6
            OR b.[FunctionalCurrencyCode] COLLATE Latin1_General_100_BIN2
               <> UPPER(LTRIM(RTRIM(b.[FunctionalCurrencyCode]))) COLLATE Latin1_General_100_BIN2
            OR b.[FunctionalCurrencyCode] COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z][A-Z][A-Z]'))
        OR (b.[BookType] = 3 AND (b.[BaseAccountingBookId] IS NULL OR base.[Id] IS NULL))))
    THROW 51000, 'C4_LINEAGE_PREFLIGHT: retained accounting-book type, lifecycle, currency or base-book lineage is invalid.', 1;
");

            migrationBuilder.CreateTable(
                name: "AccountingBookInitializations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountingBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SupersedesInitializationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    InitializationStatus = table.Column<int>(type: "int", nullable: false),
                    CutoffDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceAccountingBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TotalDebits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCredits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RequiredAccountCount = table.Column<int>(type: "int", nullable: false),
                    CoveredAccountCount = table.Column<int>(type: "int", nullable: false),
                    EvidenceFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReconciliationFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PreparedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreparedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingBookInitializations", x => x.Id);
                    table.UniqueConstraint("AK_AccountingBookInitializations_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_AccountingBookInitializations_ApprovalShape", "([InitializationStatus] IN (1, 2, 4)) OR ([InitializationStatus] = 3 AND [ApprovedByUserId] IS NOT NULL AND [ApprovedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_AccountingBookInitializations_Balanced", "[IsDeleted] = 1 OR [TotalDebits] = [TotalCredits]");
                    table.CheckConstraint("CK_AccountingBookInitializations_Coverage", "[RequiredAccountCount] >= 0 AND [CoveredAccountCount] >= 0 AND [CoveredAccountCount] <= [RequiredAccountCount]");
                    table.CheckConstraint("CK_AccountingBookInitializations_EvidenceFingerprint", "DATALENGTH([EvidenceFingerprint]) = 64 AND [EvidenceFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                    table.CheckConstraint("CK_AccountingBookInitializations_MakerChecker", "[ApprovedByUserId] IS NULL OR [ApprovedByUserId] <> [PreparedByUserId]");
                    table.CheckConstraint("CK_AccountingBookInitializations_Mode", "[IsDeleted] = 1 OR [Mode] IN (1, 2, 3)");
                    table.CheckConstraint("CK_AccountingBookInitializations_NoDelete", "[IsDeleted] = 0");
                    table.CheckConstraint("CK_AccountingBookInitializations_ReconciliationFingerprint", "DATALENGTH([ReconciliationFingerprint]) = 64 AND [ReconciliationFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                    table.CheckConstraint("CK_AccountingBookInitializations_SourceShape", "([Mode] = 1 AND [SourceAccountingBookId] IS NULL) OR ([Mode] IN (2, 3) AND [SourceAccountingBookId] IS NOT NULL AND [SourceAccountingBookId] <> [AccountingBookId])");
                    table.CheckConstraint("CK_AccountingBookInitializations_Status", "[IsDeleted] = 1 OR [InitializationStatus] IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_AccountingBookInitializations_AccountingBookInitializations_TenantId_SupersedesInitializationId",
                        columns: x => new { x.TenantId, x.SupersedesInitializationId },
                        principalTable: "AccountingBookInitializations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingBookInitializations_AccountingBooks_TenantId_AccountingBookId",
                        columns: x => new { x.TenantId, x.AccountingBookId },
                        principalTable: "AccountingBooks",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingBookInitializations_AccountingBooks_TenantId_SourceAccountingBookId",
                        columns: x => new { x.TenantId, x.SourceAccountingBookId },
                        principalTable: "AccountingBooks",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingBookInitializations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountingBookPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountingBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodStatus = table.Column<int>(type: "int", nullable: false),
                    PendingStatus = table.Column<int>(type: "int", nullable: true),
                    PendingReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingBookPeriods", x => x.Id);
                    table.CheckConstraint("CK_AccountingBookPeriods_NoDelete", "[IsDeleted] = 0");
                    table.CheckConstraint("CK_AccountingBookPeriods_PendingStatus", "[PendingStatus] IS NULL OR [PendingStatus] IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_AccountingBookPeriods_Status", "[IsDeleted] = 1 OR [PeriodStatus] IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_AccountingBookPeriods_AccountingBooks_TenantId_AccountingBookId",
                        columns: x => new { x.TenantId, x.AccountingBookId },
                        principalTable: "AccountingBooks",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingBookPeriods_FiscalPeriods_TenantId_FiscalPeriodId",
                        columns: x => new { x.TenantId, x.FiscalPeriodId },
                        principalTable: "FiscalPeriods",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingBookPeriods_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountingBookInitializationLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountingBookInitializationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    OpeningDebit = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OpeningCredit = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BaseBookSignedBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OpeningAdjustment = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingBookInitializationLines", x => x.Id);
                    table.CheckConstraint("CK_AccountingBookInitializationLines_Amounts", "[OpeningDebit] >= 0 AND [OpeningCredit] >= 0 AND NOT ([OpeningDebit] > 0 AND [OpeningCredit] > 0)");
                    table.CheckConstraint("CK_AccountingBookInitializationLines_Currency", "DATALENGTH([CurrencyCode]) = 3 AND [CurrencyCode] COLLATE Latin1_General_100_BIN2 LIKE '[A-Z][A-Z][A-Z]'");
                    table.CheckConstraint("CK_AccountingBookInitializationLines_NoDelete", "[IsDeleted] = 0");
                    table.ForeignKey(
                        name: "FK_AccountingBookInitializationLines_AccountingBookInitializations_TenantId_AccountingBookInitializationId",
                        columns: x => new { x.TenantId, x.AccountingBookInitializationId },
                        principalTable: "AccountingBookInitializations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingBookInitializationLines_Accounts_TenantId_AccountId",
                        columns: x => new { x.TenantId, x.AccountId },
                        principalTable: "Accounts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingBookInitializationLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookInitializationLines_TenantId_AccountId",
                table: "AccountingBookInitializationLines",
                columns: new[] { "TenantId", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookInitializationLines_TenantId_AccountingBookInitializationId_AccountId",
                table: "AccountingBookInitializationLines",
                columns: new[] { "TenantId", "AccountingBookInitializationId", "AccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookInitializations_TenantId_AccountingBookId_InitializationStatus",
                table: "AccountingBookInitializations",
                columns: new[] { "TenantId", "AccountingBookId", "InitializationStatus" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [InitializationStatus] = 3");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookInitializations_TenantId_AccountingBookId_Version",
                table: "AccountingBookInitializations",
                columns: new[] { "TenantId", "AccountingBookId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookInitializations_TenantId_IdempotencyKey",
                table: "AccountingBookInitializations",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookInitializations_TenantId_SourceAccountingBookId",
                table: "AccountingBookInitializations",
                columns: new[] { "TenantId", "SourceAccountingBookId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookInitializations_TenantId_SupersedesInitializationId",
                table: "AccountingBookInitializations",
                columns: new[] { "TenantId", "SupersedesInitializationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookPeriods_TenantId_AccountingBookId_FiscalPeriodId",
                table: "AccountingBookPeriods",
                columns: new[] { "TenantId", "AccountingBookId", "FiscalPeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookPeriods_TenantId_FiscalPeriodId",
                table: "AccountingBookPeriods",
                columns: new[] { "TenantId", "FiscalPeriodId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The predecessor has nowhere to retain period locks or approved opening authority.
            // Downgrade is lossless only before C4 governance evidence has been created.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [AccountingBookInitializationLines])
   OR EXISTS (SELECT 1 FROM [AccountingBookInitializations])
   OR EXISTS (SELECT 1 FROM [AccountingBookPeriods])
    THROW 51000, 'C4_DOWN_GUARD: book-period or initialization evidence cannot be represented by the predecessor schema.', 1;
");

            migrationBuilder.DropTable(
                name: "AccountingBookInitializationLines");

            migrationBuilder.DropTable(
                name: "AccountingBookPeriods");

            migrationBuilder.DropTable(
                name: "AccountingBookInitializations");
        }
    }
}
