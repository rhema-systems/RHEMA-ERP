using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Leave closure plan, wave C (slices C2 and C4): sending a leave REQUEST back with dates of the
    /// approver's own, moving an already-approved request without losing its number, and recording
    /// that leave is still going ahead.
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
    /// <b>Purely additive, and every existing row keeps working.</b> Eleven columns on
    /// <c>LeaveRequests</c>, ten of them nullable and the eleventh (<c>RescheduleCount</c>)
    /// defaulted to 0 — which is exactly what a request that has never been moved should say. No
    /// row is rewritten and nothing is back-filled: a request raised before this migration is
    /// simply one that was never sent back and never moved.
    /// </para>
    ///
    /// <para>
    /// <b>Three groups.</b> <c>Suggested*</c> / <c>ManagerSuggestionNotes</c> mirror the three
    /// fields <c>LeavePlans</c> has carried since the port, so TDC's <i>"sending back for correction
    /// with suggested dates"</i> can happen on the record that actually books the days (R-3).
    /// <c>Original*</c> / <c>Reschedule*</c> record a move of already-approved dates, keeping the
    /// request's number, its approval history and the reason it moved (R-8). <c>ObservanceConfirmed*</c>
    /// records the answer to "is this still going ahead?" (R-7).
    /// </para>
    ///
    /// <para>
    /// <b>⚠ No foreign keys and no indexes, deliberately.</b> <c>RescheduledById</c> and
    /// <c>ObservanceConfirmedById</c> name an employee, and the obvious move would be an FK to
    /// <c>Employees</c> with a navigation property beside it. Two reasons not to. First,
    /// <c>ApprovedById</c> on this very table is already a bare <c>Guid</c> with no FK and no
    /// navigation — adding a differently-shaped actor column next to it would make the table
    /// inconsistent with itself. Second, an unpaired navigation to <c>Employee</c> mints a shadow
    /// <c>EmployeeId1</c> column on the other side of the relationship; the model was written
    /// without navigations precisely so the scaffold would not produce one, and it did not. Names
    /// are resolved by the reads that display them.
    /// </para>
    /// </remarks>
    public partial class AddLeaveRequestSuggestionAndReschedule : Migration
    {
        private static string AddColumn(string table, string column, string definition) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        /// <remarks>
        /// ⚠ The default constraint is looked up rather than named: on a model-built database the
        /// name is server-generated, so guessing it would leave the column undroppable and the
        /// <c>Down</c> broken. <c>RescheduleCount</c> is the one column here that has one, and this
        /// is what makes its <c>Down</c> work on both a migrated and a model-built database.
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
            // Sending a request back with the approver's own dates (status ChangesSuggested).
            migrationBuilder.Sql(AddColumn(Table, "SuggestedStartDate", "date NULL"));
            migrationBuilder.Sql(AddColumn(Table, "SuggestedEndDate", "date NULL"));
            migrationBuilder.Sql(AddColumn(Table, "ManagerSuggestionNotes", "nvarchar(1000) NULL"));

            // Moving an approved request: what it used to say, who moved it, when, why, how often.
            migrationBuilder.Sql(AddColumn(Table, "OriginalStartDate", "date NULL"));
            migrationBuilder.Sql(AddColumn(Table, "OriginalEndDate", "date NULL"));
            migrationBuilder.Sql(AddColumn(Table, "RescheduledDate", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumn(Table, "RescheduledById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn(Table, "RescheduleReason", "nvarchar(500) NULL"));

            // NOT NULL with a default: a request that has never moved has moved zero times, which is
            // true of every row that exists today and needs no back-fill.
            migrationBuilder.Sql(AddColumn(Table, "RescheduleCount", "int NOT NULL CONSTRAINT [DF_LeaveRequests_RescheduleCount] DEFAULT 0"));

            // "Yes, this is still going ahead."
            migrationBuilder.Sql(AddColumn(Table, "ObservanceConfirmedDate", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumn(Table, "ObservanceConfirmedById", "uniqueidentifier NULL"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropColumn(Table, "SuggestedStartDate"));
            migrationBuilder.Sql(DropColumn(Table, "SuggestedEndDate"));
            migrationBuilder.Sql(DropColumn(Table, "ManagerSuggestionNotes"));

            migrationBuilder.Sql(DropColumn(Table, "OriginalStartDate"));
            migrationBuilder.Sql(DropColumn(Table, "OriginalEndDate"));
            migrationBuilder.Sql(DropColumn(Table, "RescheduledDate"));
            migrationBuilder.Sql(DropColumn(Table, "RescheduledById"));
            migrationBuilder.Sql(DropColumn(Table, "RescheduleReason"));
            migrationBuilder.Sql(DropColumn(Table, "RescheduleCount"));

            migrationBuilder.Sql(DropColumn(Table, "ObservanceConfirmedDate"));
            migrationBuilder.Sql(DropColumn(Table, "ObservanceConfirmedById"));
        }
    }
}
