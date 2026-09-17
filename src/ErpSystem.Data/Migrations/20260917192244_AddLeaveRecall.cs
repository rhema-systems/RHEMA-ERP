using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Leave residue plan, slice G1: recall from leave — calling an employee back before their
    /// end date, and keeping the record honest about it afterwards.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration since
    /// <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder create the
    /// schema from the EF model, so a rebuilt database already has these columns and a bare
    /// <c>AddColumn</c> stops the chain.
    /// </para>
    ///
    /// <para>
    /// <b>Purely additive, and every existing row keeps working.</b> Six columns on
    /// <c>LeaveRequests</c>, <b>all six nullable and none defaulted</b> — which is why this
    /// migration needs no default-constraint handling at all, unlike
    /// <c>20260917155507_AddLeaveRequestSuggestionAndReschedule</c> beside it. Nothing is
    /// back-filled: a request raised before this migration is simply one nobody was recalled from.
    /// </para>
    ///
    /// <para>
    /// <b>Why curtailment needs its own columns rather than reusing the reschedule ones.</b>
    /// <c>OriginalStartDate</c> / <c>OriginalEndDate</c> mean "the dates before the first move".
    /// A recall is not a move — the leave did not go somewhere else, it was interrupted — and a
    /// request can be rescheduled <i>and later</i> recalled, so both facts have to survive
    /// independently. Conflating them would make each unreadable.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ No foreign key and no index on <c>RecalledById</c>, deliberately.</b> Same reasoning as
    /// <c>RescheduledById</c> and <c>ApprovedById</c> on this table, which are also bare
    /// <c>uniqueidentifier</c> columns: an unpaired navigation to <c>Employees</c> mints a shadow
    /// <c>EmployeeId1</c> column on the other side of the relationship. The model was written
    /// without navigations precisely so the scaffold would not produce one, and it did not. The name
    /// is resolved by the read that displays it.
    /// </para>
    ///
    /// <para>
    /// <c>DaysRestored</c> is <c>decimal(18,4)</c> to match <c>TotalDays</c>, because it is derived
    /// from it — half-day leave has to subtract correctly.
    /// </para>
    /// </remarks>
    public partial class AddLeaveRecall : Migration
    {
        private static string AddColumn(string table, string column, string definition) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        /// <remarks>
        /// No column here carries a default, so a plain drop would do. The constraint lookup is kept
        /// anyway so this helper stays identical to the one beside it — a <c>Down</c> that is subtly
        /// different from its neighbour is the one somebody copies wrongly later.
        /// </remarks>
        private static string DropColumn(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
BEGIN
    DECLARE @df_{table}_{column} sysname;
    SELECT @df_{table}_{column} = dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.{table}') AND c.name = '{column}';
    IF @df_{table}_{column} IS NOT NULL
        EXEC('ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [' + @df_{table}_{column} + ']');
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
END";

        private const string Table = "LeaveRequests";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The recall itself: when they are due back, and what the request used to end on. Both
            // are needed — showing only the new end date makes the leave read as though it was
            // always this short, and the recall becomes invisible on the record.
            migrationBuilder.Sql(AddColumn(Table, "RecallEffectiveDate", "date NULL"));
            migrationBuilder.Sql(AddColumn(Table, "PreRecallEndDate", "date NULL"));

            // Who recorded it, when, and why. The reason is required by the service, not by the
            // schema: existing rows have none and are not in breach of anything.
            migrationBuilder.Sql(AddColumn(Table, "RecalledDate", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumn(Table, "RecalledById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn(Table, "RecallReason", "nvarchar(500) NULL"));

            // Cumulative across repeated recalls, so a second one does not overwrite what the first
            // gave back. The balance re-derives itself from TotalDays regardless; this records the
            // event, which nothing else preserves once the dates are truncated.
            migrationBuilder.Sql(AddColumn(Table, "DaysRestored", "decimal(18,4) NULL"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropColumn(Table, "RecallEffectiveDate"));
            migrationBuilder.Sql(DropColumn(Table, "PreRecallEndDate"));
            migrationBuilder.Sql(DropColumn(Table, "RecalledDate"));
            migrationBuilder.Sql(DropColumn(Table, "RecalledById"));
            migrationBuilder.Sql(DropColumn(Table, "RecallReason"));
            migrationBuilder.Sql(DropColumn(Table, "DaysRestored"));
        }
    }
}
