using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Leave settings audit 2, slice A: pay is Finance's. HR records the days; Finance puts the money on them.
    /// </summary>
    /// <remarks>
    /// <para><b>A settlement line records its days and who valued it</b> — three nullable columns on
    /// <c>SeparationSettlementLines</c>: <c>Days</c> (notice paid in lieu, annual leave owed),
    /// <c>ValuedByEmployeeId</c> (Restrict to <c>Employees</c>) and <c>ValuedOn</c>, set by Finance's step
    /// (<c>HR.Pay.Value</c>, computation state <c>ValuedByFinance</c> = 4).</para>
    ///
    /// <para><b>HR's rate settings go</b> (L-73, L-75): the leave type's <c>EncashmentRateBasis</c>,
    /// <c>EncashmentRatePerDay</c> and <c>EncashmentWorkingDaysPerMonth</c>; the company's
    /// <c>SettlementDaysPerYear</c> and <c>EncashmentWorkingDaysPerMonth</c>; and the
    /// <c>LeaveTypeAllowances</c> table (31 links on UAT, 2026-09-27), which described HR's daily rate.
    /// <b>The dead sub-type column goes</b> (L-76): <c>LeaveCategoryAllocations.LeaveSubTypeId</c>,
    /// retired in round 5 lane N and used by 0 rows, with its foreign key and both indexes; the
    /// allocation index is recreated without it.</para>
    ///
    /// <para><b>The data step</b>: on every statement still with HR — its separation awaiting a
    /// settlement (status 6), which includes one Internal Audit has returned (a returned statement
    /// keeps its <c>FinalisedOn</c>, so that is not the test) — a pay line carrying a figure HR put on
    /// it (Computed or ManuallyEntered, non-zero) is turned back to unvalued: no amount, HR's source
    /// named in the basis, and its days read from the description where HR wrote them
    /// ("… — 56.00 day(s)"). Measured on UAT 2026-09-27: one statement, three lines (SEP-2026-00002).
    /// A notice or leave line already unvalued gets its days read off the description the same way
    /// (UAT: three lines on SEP-2026-00013 and -00014), since the days are what Finance's queue shows.
    /// A stated zero is a fact, not a valuation, and stays. Statements with Internal Audit are not
    /// touched here — the code refuses to approve one carrying HR's figures and clears them when it is
    /// returned — and released statements are history.</para>
    ///
    /// <para>⚠ <c>Days</c> is <c>decimal(18,4)</c>: <c>ConfigureDecimalPrecision</c> sets every decimal.
    /// Guarded SQL throughout, as on every HR migration; the scaffold's <c>UpdateData</c> in the Down
    /// is replaced by column defaults (it cannot run under the fast EF build).</para>
    /// </remarks>
    public partial class PayValuedByFinance : Migration
    {
        private const string Lines = "SeparationSettlementLines";
        private const string Allocations = "LeaveCategoryAllocations";
        private const string LeaveTypes = "LeaveTypes";
        private const string Settings = "CompanyHrPolicySettings";
        private const string Allowances = "LeaveTypeAllowances";

        private const string ValuedByFk = "FK_SeparationSettlementLines_Employees_ValuedByEmployeeId";
        private const string ValuedByIndex = "IX_SeparationSettlementLines_ValuedByEmployeeId";
        private const string SubTypeFk = "FK_LeaveCategoryAllocations_LeaveSubTypes_LeaveSubTypeId";
        private const string SubTypeIndex = "IX_LeaveCategoryAllocations_LeaveSubTypeId";
        private const string OldAllocationIndex = "IX_LeaveCategoryAllocations_LeaveTypeId_LeaveSubTypeId_StaffLevelId_EffectiveFrom";
        private const string NewAllocationIndex = "IX_LeaveCategoryAllocations_LeaveTypeId_StaffLevelId_EffectiveFrom";

        /// <summary>
        /// The pay categories — <c>SeparationService.IsPayLine</c>: UnpaidSalary 1, NoticePay 2,
        /// LeaveEncashment 3, GratuityOrEndOfService 4, PensionRelated 6, TaxDeduction 54.
        /// </summary>
        private const string PayCategories = "1, 2, 3, 4, 6, 54";

        /// <summary>
        /// The days HR wrote into a notice (2) or leave (3) line's description — the number just before
        /// " day(s)", whichever dash precedes it ("… – 56.00 day(s)", "… — 16 day(s) not served") — as
        /// <c>d.[Days]</c>; NULL when there is none.
        /// </summary>
        private const string DaysFromDescription = @"
CROSS APPLY (SELECT CHARINDEX(N' day(s)', l.[Description]) AS [At]) p
CROSS APPLY (SELECT CASE WHEN p.[At] > 1 THEN LEFT(l.[Description], p.[At] - 1) END AS [Before]) b
CROSS APPLY (SELECT CASE
        WHEN l.[Category] IN (2, 3) AND CHARINDEX(N' ', REVERSE(b.[Before])) > 1
        THEN TRY_CAST(RIGHT(b.[Before], CHARINDEX(N' ', REVERSE(b.[Before])) - 1) AS decimal(18,4))
    END AS [Days]) d";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── The settlement line: its days, and who valued it ───────────────────────────────────
            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Lines}', 'Days') IS NULL
    ALTER TABLE [dbo].[{Lines}] ADD [Days] decimal(18,4) NULL;
IF COL_LENGTH('dbo.{Lines}', 'ValuedByEmployeeId') IS NULL
    ALTER TABLE [dbo].[{Lines}] ADD [ValuedByEmployeeId] uniqueidentifier NULL;
IF COL_LENGTH('dbo.{Lines}', 'ValuedOn') IS NULL
    ALTER TABLE [dbo].[{Lines}] ADD [ValuedOn] datetime2 NULL;");

            migrationBuilder.Sql(CreateIndex(Lines, ValuedByIndex, "[ValuedByEmployeeId]"));

            migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{ValuedByFk}' AND parent_object_id = OBJECT_ID('dbo.{Lines}'))
    ALTER TABLE [dbo].[{Lines}] ADD CONSTRAINT [{ValuedByFk}] FOREIGN KEY ([ValuedByEmployeeId])
        REFERENCES [dbo].[Employees] ([Id]);");

            // ── HR's figures on statements still with HR, turned back to unvalued ─────────────────
            // A separate batch: the Days column above must exist when this one compiles. The
            // predicate makes a second run a no-op — a cleared line has no amount to match.
            migrationBuilder.Sql($@"
UPDATE l
SET l.[Days] = COALESCE(l.[Days], d.[Days]),
    l.[Basis] = CASE WHEN LEN(n.[Note]) > 500 THEN LEFT(n.[Note], 499) + NCHAR(8230) ELSE n.[Note] END,
    l.[Amount] = NULL,
    l.[Computation] = 3,
    l.[SourceReference] = NULL,
    l.[ValuedByEmployeeId] = NULL,
    l.[ValuedOn] = NULL,
    l.[UpdatedAt] = SYSUTCDATETIME(),
    l.[UpdatedBy] = N'Migration PayValuedByFinance'
FROM [dbo].[{Lines}] l
JOIN [dbo].[SeparationSettlements] s ON s.[Id] = l.[SettlementId]
JOIN [dbo].[EmployeeSeparations] sep ON sep.[Id] = s.[SeparationId]{DaysFromDescription}
CROSS APPLY (SELECT
        N'HR''s figure was cleared on 27 Sep 2026: pay is valued by Finance now (leave settings audit 2). '
        + N'To be valued by Finance: the amount and its source are entered in Finance''s step (Pay to value).'
        + CASE WHEN l.[SourceReference] IS NOT NULL THEN N' HR had taken it from: ' + l.[SourceReference] ELSE N'' END
    AS [Note]) n
WHERE l.[IsDeleted] = 0
  AND s.[IsDeleted] = 0
  AND sep.[Status] = 6
  AND l.[Category] IN ({PayCategories})
  AND l.[Computation] IN (1, 2)
  AND l.[Amount] IS NOT NULL AND l.[Amount] <> 0;");

            // A statement prepared before this migration with no daily rate on record already had its
            // notice and leave lines unvalued — but wrote their days only into the description.
            // Finance's queue shows a line's Days, and the days are what HR hands over, so they are
            // read off here too. Measured on UAT 2026-09-27: three lines (SEP-2026-00013, -00014).
            migrationBuilder.Sql($@"
UPDATE l
SET l.[Days] = d.[Days],
    l.[UpdatedAt] = SYSUTCDATETIME(),
    l.[UpdatedBy] = N'Migration PayValuedByFinance'
FROM [dbo].[{Lines}] l
JOIN [dbo].[SeparationSettlements] s ON s.[Id] = l.[SettlementId]
JOIN [dbo].[EmployeeSeparations] sep ON sep.[Id] = s.[SeparationId]{DaysFromDescription}
WHERE l.[IsDeleted] = 0
  AND s.[IsDeleted] = 0
  AND sep.[Status] = 6
  AND l.[Category] IN (2, 3)
  AND l.[Days] IS NULL
  AND d.[Days] IS NOT NULL;");

            // ── The dead sub-type column on allocations (L-76) ──────────────────────────────────────
            migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{SubTypeFk}' AND parent_object_id = OBJECT_ID('dbo.{Allocations}'))
    ALTER TABLE [dbo].[{Allocations}] DROP CONSTRAINT [{SubTypeFk}];");
            migrationBuilder.Sql(DropIndex(Allocations, SubTypeIndex));
            migrationBuilder.Sql(DropIndex(Allocations, OldAllocationIndex));
            migrationBuilder.Sql(DropColumnWithDefaultSql(Allocations, "LeaveSubTypeId"));
            migrationBuilder.Sql(CreateIndex(Allocations, NewAllocationIndex, "[LeaveTypeId], [StaffLevelId], [EffectiveFrom]"));

            // ── HR's rate settings (L-73, L-75) ────────────────────────────────────────────────────
            // ⚠ The allowance links go with their table: they said which allowances HR's daily rate
            // included, and HR no longer works out a rate.
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Allowances}', 'U') IS NOT NULL
    DROP TABLE [dbo].[{Allowances}];");

            migrationBuilder.Sql(DropColumnWithDefaultSql(LeaveTypes, "EncashmentRateBasis"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(LeaveTypes, "EncashmentRatePerDay"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(LeaveTypes, "EncashmentWorkingDaysPerMonth"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(Settings, "EncashmentWorkingDaysPerMonth"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(Settings, "SettlementDaysPerYear"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ What a Down cannot give back: the 31 allowance links (the table returns empty), the
            // rates any leave type held (the columns return at their defaults), HR's figures the Up
            // cleared, and who valued which line. The code a Down returns to prices pay in HR again.
            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Settings}', 'SettlementDaysPerYear') IS NULL
    ALTER TABLE [dbo].[{Settings}] ADD [SettlementDaysPerYear] int NOT NULL DEFAULT (365);
IF COL_LENGTH('dbo.{Settings}', 'EncashmentWorkingDaysPerMonth') IS NULL
    ALTER TABLE [dbo].[{Settings}] ADD [EncashmentWorkingDaysPerMonth] int NOT NULL DEFAULT (22);
IF COL_LENGTH('dbo.{LeaveTypes}', 'EncashmentWorkingDaysPerMonth') IS NULL
    ALTER TABLE [dbo].[{LeaveTypes}] ADD [EncashmentWorkingDaysPerMonth] int NOT NULL DEFAULT (22);
IF COL_LENGTH('dbo.{LeaveTypes}', 'EncashmentRatePerDay') IS NULL
    ALTER TABLE [dbo].[{LeaveTypes}] ADD [EncashmentRatePerDay] decimal(18,4) NULL;
IF COL_LENGTH('dbo.{LeaveTypes}', 'EncashmentRateBasis') IS NULL
    ALTER TABLE [dbo].[{LeaveTypes}] ADD [EncashmentRateBasis] int NOT NULL DEFAULT (0);");

            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Allowances}', 'U') IS NULL
CREATE TABLE [dbo].[{Allowances}] (
    [Id]               uniqueidentifier NOT NULL,
    [LeaveTypeId]      uniqueidentifier NOT NULL,
    [PayComponentId]   uniqueidentifier NOT NULL,
    [TenantId]         uniqueidentifier NOT NULL,
    [CreatedAt]        datetime2        NOT NULL,
    [CreatedBy]        nvarchar(max)    NULL,
    [CreatedById]      uniqueidentifier NULL,
    [DeletedAt]        datetime2        NULL,
    [DeletedBy]        nvarchar(max)    NULL,
    [IsDeleted]        bit              NOT NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [UpdatedAt]        datetime2        NULL,
    [UpdatedBy]        nvarchar(max)    NULL,
    CONSTRAINT [PK_{Allowances}] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_{Allowances}_LeaveTypes_LeaveTypeId] FOREIGN KEY ([LeaveTypeId])
        REFERENCES [dbo].[{LeaveTypes}] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_{Allowances}_PayComponents_PayComponentId] FOREIGN KEY ([PayComponentId])
        REFERENCES [dbo].[PayComponents] ([Id]),
    CONSTRAINT [FK_{Allowances}_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id])
);");
            migrationBuilder.Sql(CreateIndex(Allowances, "IX_LeaveTypeAllowance_Tenant_LeaveType_Component",
                "[TenantId], [LeaveTypeId], [PayComponentId]", unique: true));
            migrationBuilder.Sql(CreateIndex(Allowances, "IX_LeaveTypeAllowances_LeaveTypeId", "[LeaveTypeId]"));
            migrationBuilder.Sql(CreateIndex(Allowances, "IX_LeaveTypeAllowances_PayComponentId", "[PayComponentId]"));

            migrationBuilder.Sql(DropIndex(Allocations, NewAllocationIndex));
            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Allocations}', 'LeaveSubTypeId') IS NULL
    ALTER TABLE [dbo].[{Allocations}] ADD [LeaveSubTypeId] uniqueidentifier NULL;");
            migrationBuilder.Sql(CreateIndex(Allocations, SubTypeIndex, "[LeaveSubTypeId]"));
            migrationBuilder.Sql(CreateIndex(Allocations, OldAllocationIndex,
                "[LeaveTypeId], [LeaveSubTypeId], [StaffLevelId], [EffectiveFrom]"));
            migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{SubTypeFk}' AND parent_object_id = OBJECT_ID('dbo.{Allocations}'))
    ALTER TABLE [dbo].[{Allocations}] ADD CONSTRAINT [{SubTypeFk}] FOREIGN KEY ([LeaveSubTypeId])
        REFERENCES [dbo].[LeaveSubTypes] ([Id]);");

            migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{ValuedByFk}' AND parent_object_id = OBJECT_ID('dbo.{Lines}'))
    ALTER TABLE [dbo].[{Lines}] DROP CONSTRAINT [{ValuedByFk}];");
            migrationBuilder.Sql(DropIndex(Lines, ValuedByIndex));
            migrationBuilder.Sql(DropColumnWithDefaultSql(Lines, "ValuedOn"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(Lines, "ValuedByEmployeeId"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(Lines, "Days"));
        }

        private static string CreateIndex(string table, string index, string columns, bool unique = false)
        {
            var uniqueness = unique ? "UNIQUE " : string.Empty;
            return $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE {uniqueness}INDEX [{index}] ON [dbo].[{table}] ({columns});";
        }

        private static string DropIndex(string table, string index) => $@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    DROP INDEX [{index}] ON [dbo].[{table}];";

        /// <summary>
        /// Drops a column that may carry an unnamed default: the default constraint first, then the column.
        /// </summary>
        private static string DropColumnWithDefaultSql(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
BEGIN
    DECLARE @default sysname;
    SELECT @default = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.{table}') AND c.name = '{column}';
    IF @default IS NOT NULL
    BEGIN
        DECLARE @drop nvarchar(400) = N'ALTER TABLE [dbo].[{table}] DROP CONSTRAINT ' + QUOTENAME(@default);
        EXEC sp_executesql @drop;
    END
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
END";
    }
}
