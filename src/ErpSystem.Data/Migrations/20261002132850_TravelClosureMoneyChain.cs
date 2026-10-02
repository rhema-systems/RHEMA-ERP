using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Staff travel final closure, lane 3 (the money chain): the columns batch 1 did not add, and the data step batch 1
    /// handed to this lane (<c>docs/HR/areas/travel/HR-STAFF-TRAVEL-FINAL-CLOSURE-PLAN.md</c> § 1 D-14, § 5).
    /// </summary>
    /// <remarks>
    /// <para><b>Added:</b> on an advance, who withdrew it before any money went out, when and why (status Cancelled — by
    /// the desk, or with its trip); on an expense claim, the reviewer's words on the outcome, which the review accepted and
    /// dropped (lane 3, N4, N5).</para>
    ///
    /// <para><b>Corrected</b> (live rows that disagree): an advance no money has left for — Requested, Approved, Rejected
    /// or Cancelled — has nothing outstanding. Creation wrote the requested amount as unsettled and approval the approved
    /// amount, so a merely requested advance read as money owed by the traveller, and as overdue once it had a deadline
    /// (B8). Lane 3's code sets the unsettled amount at disbursement, which is why this step ships with it and not with
    /// batch 1: under the old code an Approved advance zeroed here would have been disbursed with nothing to recover.</para>
    ///
    /// <para><b>Filled</b> (live rows without one): a settlement deadline on every advance whose cash is out — Disbursed,
    /// PartiallySettled or Overdue — as lane 3's code defaults it: the trip's end plus the claim window of the approved
    /// policy the trip was checked against, or 30 days. Without a deadline the overdue sweep never sees an advance (O-8).</para>
    ///
    /// <para><b>Down</b> drops the two sets of columns (the cancellation's who and why, like batch 1's rejection columns,
    /// are text and an actor and go without refusal; a Cancelled advance keeps its status). It puts back the unsettled
    /// amount the previous code expects on a live Requested or Approved advance, because that code recovers a claim
    /// against it after disbursement. A filled deadline stays: it is right under either code.</para>
    ///
    /// <para>Guarded SQL, as on every HR migration: each statement checks for the object it changes, so a database built
    /// from the model skips what it already has.</para>
    /// </remarks>
    public partial class TravelClosureMoneyChain : Migration
    {
        private const string Claims = "StaffTravelExpenseClaims";
        private const string Advances = "StaffTravelAdvances";

        // TravelAdvanceStatus, int-stored: Requested 1, Approved 2, Disbursed 3, PartiallySettled 4, Overdue 6,
        // Rejected 8, Cancelled 9.
        private const string NoMoneyOut = "1, 2, 8, 9";
        private const string CashOut = "3, 4, 6";

        /// <summary>The default deadline lane 3's code gives an advance: the trip's end plus its approved policy's claim
        /// window, or 30 days when the trip was checked against no approved policy or the policy sets no window.</summary>
        private static readonly string FillDeadlinesSql = $@"
IF OBJECT_ID('dbo.{Advances}', 'U') IS NOT NULL
    UPDATE a SET a.[SettlementDeadline] = DATEADD(day,
            CASE WHEN p.[ApprovedById] IS NOT NULL AND p.[ExpenseSubmissionDays] > 0 THEN p.[ExpenseSubmissionDays] ELSE 30 END,
            r.[TravelEndDate])
    FROM [dbo].[{Advances}] a
    JOIN [dbo].[StaffTravelRequests] r ON r.[Id] = a.[StaffTravelRequestId]
    LEFT JOIN [dbo].[StaffTravelPolicies] p ON p.[Id] = r.[PolicyId]
    WHERE a.[IsDeleted] = 0 AND a.[SettlementDeadline] IS NULL AND a.[Status] IN ({CashOut});";

        private static readonly string ZeroUndisbursedSql = $@"
IF OBJECT_ID('dbo.{Advances}', 'U') IS NOT NULL
    UPDATE [dbo].[{Advances}] SET [UnsettledAmount] = 0
    WHERE [IsDeleted] = 0 AND [UnsettledAmount] <> 0 AND [Status] IN ({NoMoneyOut});";

        private static readonly string RestoreUndisbursedSql = $@"
IF OBJECT_ID('dbo.{Advances}', 'U') IS NOT NULL
    UPDATE [dbo].[{Advances}] SET [UnsettledAmount] = COALESCE([ApprovedAmount], [RequestedAmount]) - [SettledAmount]
    WHERE [IsDeleted] = 0 AND [UnsettledAmount] = 0 AND [Status] IN (1, 2);";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Claims: the reviewer's words on the outcome (N4) ─────────────────────────────────
            migrationBuilder.Sql(AddColumnSql(Claims, "ReviewNotes", "nvarchar(2000) NULL"));

            // ── 2. Advances: who withdrew one before money went out, when and why (N5) ───────────────
            migrationBuilder.Sql(AddColumnSql(Advances, "CancelledAt", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumnSql(Advances, "CancelledById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumnSql(Advances, "CancellationReason", "nvarchar(1000) NULL"));
            migrationBuilder.Sql(CreateIndexSql(Advances, $"IX_{Advances}_CancelledById", "[CancelledById]"));
            migrationBuilder.Sql(AddForeignKeySql(Advances, $"FK_{Advances}_Employees_CancelledById", "CancelledById", "Employees"));

            // ── 3. Data: nothing outstanding before disbursement (B8); a deadline on cash out (O-8) ─────
            migrationBuilder.Sql(ZeroUndisbursedSql);
            migrationBuilder.Sql(FillDeadlinesSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RestoreUndisbursedSql);

            migrationBuilder.Sql(DropForeignKeySql(Advances, $"FK_{Advances}_Employees_CancelledById"));
            migrationBuilder.Sql(DropIndexSql(Advances, $"IX_{Advances}_CancelledById"));
            foreach (var column in new[] { "CancelledAt", "CancelledById", "CancellationReason" })
                migrationBuilder.Sql(DropColumnSql(Advances, column));

            migrationBuilder.Sql(DropColumnSql(Claims, "ReviewNotes"));
        }

        private static string AddColumnSql(string table, string column, string definition) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL AND COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        private static string CreateIndexSql(string table, string index, string columns) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{index}] ON [dbo].[{table}] ({columns});";

        private static string DropIndexSql(string table, string index) => $@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    DROP INDEX [{index}] ON [dbo].[{table}];";

        private static string AddForeignKeySql(string table, string name, string column, string principal) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL AND OBJECT_ID('dbo.{name}', 'F') IS NULL
    ALTER TABLE [dbo].[{table}] ADD CONSTRAINT [{name}]
        FOREIGN KEY ([{column}]) REFERENCES [dbo].[{principal}] ([Id]);";

        private static string DropForeignKeySql(string table, string name) => $@"
IF OBJECT_ID('dbo.{name}', 'F') IS NOT NULL
    ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [{name}];";

        /// <summary>None of this migration's columns carries a default, so the column goes on its own.</summary>
        private static string DropColumnSql(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];";
    }
}
