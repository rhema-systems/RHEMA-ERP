using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Staff travel final closure, migration batch 1: the columns and indexes lanes 1–8 build on
    /// (<c>docs/HR/areas/travel/HR-STAFF-TRAVEL-FINAL-CLOSURE-PLAN.md</c> § 5).
    /// </summary>
    /// <remarks>
    /// <para><b>Added:</b> on a travel request, who approved it, who returned it for revision, who closed it and who
    /// asked for a change after approval, each with when and why (lanes 1, 2; D-6, D-9); on an expense claim, who paid
    /// it, why a payment went ahead past an unlinked advance, and a voided payment (lane 3; D-2, O-2, T-39); on a claim
    /// line, the fleet trip and fuel transaction a fuel line refers to (lane 6; D-11 — bare ids, Fleet owns the rows);
    /// on an advance, its rejection, write-off and refund (lane 3; O-8); on a flight and a hotel booking, the state of
    /// an above-cap exception and who requested and authorised it (lane 4; D-8); on a ground leg, the driver's own
    /// request (lane 6; D-11); on a policy, the currency of its money caps (lane 4; C3); on a reminder dispatch, when it
    /// was published (lane 8; F2).</para>
    ///
    /// <para><b>Filled:</b> every existing policy's currency with its tenant's base currency, and every existing
    /// dispatch's published time with the time it was written — the sweep treated those rows as sent, and lane 8 must
    /// not send them again. The two new state columns arrive as 0: a refunded amount of nothing, and no exception.</para>
    ///
    /// <para><b>Corrected</b> (once, on the rows that disagree): a request's <c>IsInternational</c> to what its two
    /// countries say (A5 — the flag was a payload answer, and a domestic trip declared international bought the higher
    /// caps); a claim's currency to its tenant's base, which is the currency its totals were always kept in (B11).
    /// Neither correction is reverted by Down.</para>
    ///
    /// <para>⚠ <b>Not here:</b> the plan's fourth data step, zeroing the unsettled amount of Requested and Approved
    /// advances, moved to lane 3. Today's disbursement does not recompute that amount — only approval sets it — so an
    /// Approved advance zeroed now would be disbursed with nothing outstanding, and the claim that should recover it
    /// would recover nothing. It ships with the code that sets the amount at disbursement.</para>
    ///
    /// <para><b>Indexes:</b> a claim number and an advance number are unique among LIVE rows (B9) — the unfiltered
    /// indexes are replaced, so a soft-deleted row no longer holds its number. A policy's version number is unique per
    /// policy name among live policies (C3, T-50); EF counts that index as covering the tenant foreign key, so the
    /// single-column tenant index goes. A unique index refuses rather than choosing: duplicates stop the migration with
    /// a count.</para>
    ///
    /// <para>⚠ <b>Down refuses</b> while any advance records a refund, any flight or hotel booking carries an exception
    /// state, or any claim records a voided payment — the previous shape cannot hold those, and dropping them would
    /// misstate money or authority. It also refuses to restore the unfiltered number indexes while a soft-deleted claim or
    /// advance shares a number with another row.</para>
    ///
    /// <para>Guarded SQL, as on every HR migration: each statement checks for the object it changes, so a database
    /// built from the model skips what it already has. A statement that names a column added in the same batch runs as
    /// dynamic SQL, because SQL Server compiles a batch before running it.</para>
    /// </remarks>
    public partial class TravelClosureBatch1 : Migration
    {
        private const string Requests = "StaffTravelRequests";
        private const string Claims = "StaffTravelExpenseClaims";
        private const string Lines = "StaffTravelExpenseClaimLines";
        private const string Advances = "StaffTravelAdvances";
        private const string Flights = "StaffTravelFlightBookings";
        private const string Hotels = "StaffTravelHotelBookings";
        private const string Ground = "StaffTravelGroundTransports";
        private const string Policies = "StaffTravelPolicies";
        private const string Dispatches = "StaffTravelReminderDispatchLogs";

        private const string ClaimNumberIndex = "IX_StaffTravelExpenseClaims_TenantId_ClaimNumber";
        private const string AdvanceNumberIndex = "IX_StaffTravelAdvances_TenantId_AdvanceNumber";
        private const string PolicyVersionIndex = "IX_StaffTravelPolicies_TenantId_PolicyName_VersionNumber";
        private const string PolicyTenantIndex = "IX_StaffTravelPolicies_TenantId";

        /// <summary>The tenant's base currency, as Finance resolves it (<c>CurrencyService.GetBaseCurrencyAsync</c>).</summary>
        private const string BaseCurrencyFor = @"
    SELECT TOP 1 c.[CurrencyCode] FROM [dbo].[Currencies] c
    WHERE c.[TenantId] = {0}.[TenantId] AND c.[IsBaseCurrency] = 1 AND c.[IsActive] = 1 AND c.[IsDeleted] = 0
    ORDER BY c.[CreatedAt]";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Requests: who approved, returned, closed, and asked for a change (lanes 1, 2) ──────
            migrationBuilder.Sql(AddColumnSql(Requests, "ApprovedById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumnSql(Requests, "ReturnedAt", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumnSql(Requests, "ReturnedById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumnSql(Requests, "ReturnReason", "nvarchar(1000) NULL"));
            migrationBuilder.Sql(AddColumnSql(Requests, "ClosedAt", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumnSql(Requests, "ClosedById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumnSql(Requests, "ChangeRequestedAt", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumnSql(Requests, "ChangeRequestedById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumnSql(Requests, "ChangeReason", "nvarchar(1000) NULL"));
            foreach (var column in new[] { "ApprovedById", "ReturnedById", "ClosedById", "ChangeRequestedById" })
                AddEmployeeReference(migrationBuilder, Requests, column);

            // ── 2. Claims: the payer, the advance waiver, a voided payment (lane 3) ─────────────────
            migrationBuilder.Sql(AddColumnSql(Claims, "PaidById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumnSql(Claims, "AdvanceWaiverReason", "nvarchar(1000) NULL"));
            migrationBuilder.Sql(AddColumnSql(Claims, "PaymentVoidedAt", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumnSql(Claims, "PaymentVoidedById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumnSql(Claims, "PaymentVoidReason", "nvarchar(1000) NULL"));
            foreach (var column in new[] { "PaidById", "PaymentVoidedById" })
                AddEmployeeReference(migrationBuilder, Claims, column);

            // ── 3. Claim lines: a fuel line's fleet trip and fuel transaction (lane 6; bare ids) ─────
            migrationBuilder.Sql(AddColumnSql(Lines, "FleetTripId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumnSql(Lines, "FuelQuantity", "decimal(18,4) NULL"));
            migrationBuilder.Sql(AddColumnSql(Lines, "FleetFuelTransactionId", "uniqueidentifier NULL"));

            // ── 4. Advances: rejection, write-off, refund (lane 3) ──────────────────────────────────
            migrationBuilder.Sql(AddColumnSql(Advances, "RejectedAt", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumnSql(Advances, "RejectedById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumnSql(Advances, "RejectionReason", "nvarchar(1000) NULL"));
            migrationBuilder.Sql(AddColumnSql(Advances, "WrittenOffAt", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumnSql(Advances, "WrittenOffById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumnSql(Advances, "WriteOffReason", "nvarchar(1000) NULL"));
            migrationBuilder.Sql(AddColumnSql(Advances, "RefundedAmount",
                $"decimal(18,2) NOT NULL CONSTRAINT [DF_{Advances}_RefundedAmount] DEFAULT (0)"));
            migrationBuilder.Sql(AddColumnSql(Advances, "RefundedAt", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumnSql(Advances, "RefundedById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumnSql(Advances, "RefundReference", "nvarchar(100) NULL"));
            foreach (var column in new[] { "RejectedById", "WrittenOffById", "RefundedById" })
                AddEmployeeReference(migrationBuilder, Advances, column);

            // ── 5. Flight and hotel bookings: an above-cap exception and its authoriser (lane 4) ─────
            foreach (var table in new[] { Flights, Hotels })
            {
                migrationBuilder.Sql(AddColumnSql(table, "ExceptionState",
                    $"int NOT NULL CONSTRAINT [DF_{table}_ExceptionState] DEFAULT (0)"));
                migrationBuilder.Sql(AddColumnSql(table, "ExceptionRequestedById", "uniqueidentifier NULL"));
                migrationBuilder.Sql(AddColumnSql(table, "ExceptionAuthorisedById", "uniqueidentifier NULL"));
                migrationBuilder.Sql(AddColumnSql(table, "ExceptionAuthorisedAt", "datetime2 NULL"));
                foreach (var column in new[] { "ExceptionRequestedById", "ExceptionAuthorisedById" })
                    AddEmployeeReference(migrationBuilder, table, column);
            }

            // ── 6. Ground legs: the driver's own travel request (lane 6) ────────────────────────────
            migrationBuilder.Sql(AddColumnSql(Ground, "DriverTravelRequestId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(CreateIndexSql(Ground, $"IX_{Ground}_DriverTravelRequestId", "[DriverTravelRequestId]"));
            migrationBuilder.Sql(AddForeignKeySql(Ground, $"FK_{Ground}_{Requests}_DriverTravelRequestId", "DriverTravelRequestId", Requests));

            // ── 7. Policies: the currency of the caps, and one version number per name (lane 4) ─────
            migrationBuilder.Sql(AddColumnWithBackfillSql(Policies, "CurrencyCode", "char(3) NULL", $@"
IF OBJECT_ID('dbo.Currencies', 'U') IS NOT NULL
UPDATE p SET [CurrencyCode] = b.[CurrencyCode]
FROM [dbo].[{Policies}] p
CROSS APPLY ({string.Format(BaseCurrencyFor, "p")}) b
WHERE p.[CurrencyCode] IS NULL;"));
            migrationBuilder.Sql(DropIndexSql(Policies, PolicyTenantIndex));
            migrationBuilder.Sql(CreateUniqueIndexRefusingDuplicatesSql(Policies, PolicyVersionIndex,
                "[TenantId], [PolicyName], [VersionNumber]", "[IsDeleted] = 0",
                "policy version(s) are held by more than one live policy of the same name. Renumber the extra versions"));

            // ── 8. Reminder dispatches: published, not merely claimed (lane 8) ──────────────────────
            migrationBuilder.Sql(AddColumnWithBackfillSql(Dispatches, "PublishedAt", "datetime2 NULL", $@"
UPDATE [dbo].[{Dispatches}] SET [PublishedAt] = [CreatedAt] WHERE [PublishedAt] IS NULL;"));

            // ── 9. Claim and advance numbers: unique among live rows (B9) ───────────────────────────
            migrationBuilder.Sql(DropUnfilteredIndexSql(Claims, ClaimNumberIndex));
            migrationBuilder.Sql(CreateUniqueIndexRefusingDuplicatesSql(Claims, ClaimNumberIndex,
                "[TenantId], [ClaimNumber]", "[IsDeleted] = 0",
                "claim number(s) are held by more than one live claim in a tenant. Renumber the extra claims"));
            migrationBuilder.Sql(DropUnfilteredIndexSql(Advances, AdvanceNumberIndex));
            migrationBuilder.Sql(CreateUniqueIndexRefusingDuplicatesSql(Advances, AdvanceNumberIndex,
                "[TenantId], [AdvanceNumber]", "[IsDeleted] = 0",
                "advance number(s) are held by more than one live advance in a tenant. Renumber the extra advances"));

            // ── 10. Corrections, once, on the rows that disagree ─────────────────────────────────────
            // A5: a request's IsInternational is what its two countries say.
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Requests}', 'U') IS NOT NULL
UPDATE [dbo].[{Requests}]
SET [IsInternational] = CASE WHEN [OriginCountryId] <> [DestinationCountryId] THEN 1 ELSE 0 END
WHERE [IsDeleted] = 0
  AND [IsInternational] <> CASE WHEN [OriginCountryId] <> [DestinationCountryId] THEN 1 ELSE 0 END;");

            // B11: a claim's totals were always kept in the tenant's base currency; its label now says so.
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Claims}', 'U') IS NOT NULL AND OBJECT_ID('dbo.Currencies', 'U') IS NOT NULL
UPDATE cl SET [CurrencyCode] = b.[CurrencyCode]
FROM [dbo].[{Claims}] cl
CROSS APPLY ({string.Format(BaseCurrencyFor, "cl")}) b
WHERE cl.[IsDeleted] = 0 AND cl.[CurrencyCode] <> b.[CurrencyCode];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── 1. Refuse what the previous shape cannot hold ───────────────────────────────────────
            migrationBuilder.Sql(RefuseDownIfAnySql(Advances, "RefundedAmount", "[RefundedAmount] <> 0 OR [RefundedAt] IS NOT NULL",
                "advance(s) record a refund"));
            migrationBuilder.Sql(RefuseDownIfAnySql(Flights, "ExceptionState", "[ExceptionState] <> 0",
                "flight booking(s) carry an above-cap exception state"));
            migrationBuilder.Sql(RefuseDownIfAnySql(Hotels, "ExceptionState", "[ExceptionState] <> 0",
                "hotel booking(s) carry an above-cap exception state"));
            migrationBuilder.Sql(RefuseDownIfAnySql(Claims, "PaymentVoidedAt", "[PaymentVoidedAt] IS NOT NULL",
                "claim(s) record a voided payment"));

            // ── 2. The number indexes go back to unfiltered, if no number is held twice ─────────────
            migrationBuilder.Sql(DropIndexSql(Claims, ClaimNumberIndex));
            migrationBuilder.Sql(CreateUniqueIndexRefusingDuplicatesSql(Claims, ClaimNumberIndex,
                "[TenantId], [ClaimNumber]", null,
                "claim number(s) are held by more than one claim, deleted ones included. Renumber the deleted claims"));
            migrationBuilder.Sql(DropIndexSql(Advances, AdvanceNumberIndex));
            migrationBuilder.Sql(CreateUniqueIndexRefusingDuplicatesSql(Advances, AdvanceNumberIndex,
                "[TenantId], [AdvanceNumber]", null,
                "advance number(s) are held by more than one advance, deleted ones included. Renumber the deleted advances"));

            // ── 3. Policies: the version index and the currency go; the tenant index returns ────────
            migrationBuilder.Sql(DropIndexSql(Policies, PolicyVersionIndex));
            migrationBuilder.Sql(CreateIndexSql(Policies, PolicyTenantIndex, "[TenantId]"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(Policies, "CurrencyCode"));

            // ── 4. The dispatch log's published time ────────────────────────────────────────────────
            migrationBuilder.Sql(DropColumnWithDefaultSql(Dispatches, "PublishedAt"));

            // ── 5. Ground legs ──────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(DropForeignKeySql(Ground, $"FK_{Ground}_{Requests}_DriverTravelRequestId"));
            migrationBuilder.Sql(DropIndexSql(Ground, $"IX_{Ground}_DriverTravelRequestId"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(Ground, "DriverTravelRequestId"));

            // ── 6. Flight and hotel bookings ────────────────────────────────────────────────────────
            foreach (var table in new[] { Flights, Hotels })
            {
                foreach (var column in new[] { "ExceptionRequestedById", "ExceptionAuthorisedById" })
                    DropEmployeeReference(migrationBuilder, table, column);
                foreach (var column in new[] { "ExceptionState", "ExceptionRequestedById", "ExceptionAuthorisedById", "ExceptionAuthorisedAt" })
                    migrationBuilder.Sql(DropColumnWithDefaultSql(table, column));
            }

            // ── 7. Advances ─────────────────────────────────────────────────────────────────────────
            foreach (var column in new[] { "RejectedById", "WrittenOffById", "RefundedById" })
                DropEmployeeReference(migrationBuilder, Advances, column);
            foreach (var column in new[] { "RejectedAt", "RejectedById", "RejectionReason", "WrittenOffAt", "WrittenOffById",
                         "WriteOffReason", "RefundedAmount", "RefundedAt", "RefundedById", "RefundReference" })
                migrationBuilder.Sql(DropColumnWithDefaultSql(Advances, column));

            // ── 8. Claim lines ──────────────────────────────────────────────────────────────────────
            foreach (var column in new[] { "FleetTripId", "FuelQuantity", "FleetFuelTransactionId" })
                migrationBuilder.Sql(DropColumnWithDefaultSql(Lines, column));

            // ── 9. Claims ───────────────────────────────────────────────────────────────────────────
            foreach (var column in new[] { "PaidById", "PaymentVoidedById" })
                DropEmployeeReference(migrationBuilder, Claims, column);
            foreach (var column in new[] { "PaidById", "AdvanceWaiverReason", "PaymentVoidedAt", "PaymentVoidedById", "PaymentVoidReason" })
                migrationBuilder.Sql(DropColumnWithDefaultSql(Claims, column));

            // ── 10. Requests ────────────────────────────────────────────────────────────────────────
            foreach (var column in new[] { "ApprovedById", "ReturnedById", "ClosedById", "ChangeRequestedById" })
                DropEmployeeReference(migrationBuilder, Requests, column);
            foreach (var column in new[] { "ApprovedById", "ReturnedAt", "ReturnedById", "ReturnReason", "ClosedAt", "ClosedById",
                         "ChangeRequestedAt", "ChangeRequestedById", "ChangeReason" })
                migrationBuilder.Sql(DropColumnWithDefaultSql(Requests, column));
        }

        /// <summary>The index and the Restrict foreign key every new Employee reference carries, named as EF names them.</summary>
        private static void AddEmployeeReference(MigrationBuilder migrationBuilder, string table, string column)
        {
            migrationBuilder.Sql(CreateIndexSql(table, $"IX_{table}_{column}", $"[{column}]"));
            migrationBuilder.Sql(AddForeignKeySql(table, $"FK_{table}_Employees_{column}", column, "Employees"));
        }

        private static void DropEmployeeReference(MigrationBuilder migrationBuilder, string table, string column)
        {
            migrationBuilder.Sql(DropForeignKeySql(table, $"FK_{table}_Employees_{column}"));
            migrationBuilder.Sql(DropIndexSql(table, $"IX_{table}_{column}"));
        }

        private static string AddColumnSql(string table, string column, string definition) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL AND COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        /// <summary>
        /// Adds a column and fills it in the same step, so the fill runs exactly once — when the column arrives — and
        /// never on a database that already had it. The fill is dynamic because it names the column being added.
        /// </summary>
        private static string AddColumnWithBackfillSql(string table, string column, string definition, string backfill) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL AND COL_LENGTH('dbo.{table}', '{column}') IS NULL
BEGIN
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};
    EXEC sp_executesql N'{backfill.Replace("'", "''")}';
END";

        private static string CreateIndexSql(string table, string index, string columns) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{index}] ON [dbo].[{table}] ({columns});";

        /// <summary>
        /// Creates a unique index — filtered when <paramref name="filter"/> is given — first counting the rows it would
        /// refuse. Duplicates stop the migration with a message, because choosing which row keeps a value is not a
        /// schema change.
        /// </summary>
        private static string CreateUniqueIndexRefusingDuplicatesSql(string table, string index, string columns, string filter, string duplicatesAre)
        {
            var where = filter is null ? string.Empty : $" WHERE {filter}";
            var scope = filter is null ? "all rows" : filter;
            return $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
BEGIN
    DECLARE @duplicates int = (
        SELECT COUNT(*) FROM (
            SELECT 1 AS [Found] FROM [dbo].[{table}]{where} GROUP BY {columns} HAVING COUNT(*) > 1) d);
    IF @duplicates > 0
    BEGIN
        DECLARE @message nvarchar(1000) = CONCAT(@duplicates,
            N' {duplicatesAre} ({table}: {columns}, {scope}) before the unique index {index} can be created.');
        THROW 50001, @message, 1;
    END
    CREATE UNIQUE INDEX [{index}] ON [dbo].[{table}] ({columns}){where};
END";
        }

        private static string DropIndexSql(string table, string index) => $@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    DROP INDEX [{index}] ON [dbo].[{table}];";

        /// <summary>Drops the index only while it is still the unfiltered one, so a model-built database keeps its filter.</summary>
        private static string DropUnfilteredIndexSql(string table, string index) => $@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}') AND has_filter = 0)
    DROP INDEX [{index}] ON [dbo].[{table}];";

        private static string AddForeignKeySql(string table, string name, string column, string principal) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL AND OBJECT_ID('dbo.{name}', 'F') IS NULL
    ALTER TABLE [dbo].[{table}] ADD CONSTRAINT [{name}]
        FOREIGN KEY ([{column}]) REFERENCES [dbo].[{principal}] ([Id]);";

        private static string DropForeignKeySql(string table, string name) => $@"
IF OBJECT_ID('dbo.{name}', 'F') IS NOT NULL
    ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [{name}];";

        /// <summary>
        /// Stops a Down that would drop something the previous shape cannot hold. The count is dynamic because it names
        /// a column this migration added, and it runs only while that column is there.
        /// </summary>
        private static string RefuseDownIfAnySql(string table, string column, string condition, string rowsAre) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL AND COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
BEGIN
    DECLARE @unrepresentable int;
    EXEC sp_executesql N'SELECT @n = COUNT(*) FROM [dbo].[{table}] WHERE {condition.Replace("'", "''")};',
        N'@n int OUTPUT', @n = @unrepresentable OUTPUT;
    IF @unrepresentable > 0
    BEGIN
        DECLARE @message nvarchar(600) = CONCAT(@unrepresentable,
            N' {rowsAre}. The previous shape cannot hold them, so this migration cannot be reverted without losing them.');
        THROW 50001, @message, 1;
    END
END";

        /// <summary>
        /// Drops a column that may carry a default: the default constraint first (named here, named by SQL Server, or
        /// none on a model-built database), then the column.
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
