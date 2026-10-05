using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Company-schedule final closure, lane 2e-3 — the calendar file's change counter on an event (D-14) and the
    /// overdue chase's stamp on a task (F-34) (<c>docs/HR/areas/company-schedule/HR-COMPANY-SCHEDULE-FINAL-CLOSURE-PLAN.md</c>,
    /// lane 2 State).
    /// </summary>
    /// <remarks>
    /// <para><b>What it adds.</b> <c>CompanyEvents.CalendarSequence</c>: the SEQUENCE a calendar file carries, raised
    /// each time the event's calendar entry changes for its guests, so a mail client replaces the entry it holds. And
    /// <c>EventTasks.OverdueChasedAt</c>: when the hourly sweep chased the assignee about an overdue task. The chase is
    /// sent once (the user's ruling), and this stamp is what keeps the sweep from sending it again.</para>
    ///
    /// <para><b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration: <c>rebuild-db</c>
    /// builds from the EF model, so a rebuilt database already has both columns, and a bare <c>AddColumn</c> stops
    /// the chain. The scaffold carried these two operations and nothing else; the snapshot gained only them.</para>
    ///
    /// <para><b>No data steps.</b> Every existing event starts at sequence 0. No calendar file was ever sent before
    /// this lane, so no mail client holds a higher one. No task is stamped as chased: a task already overdue when this
    /// lands is chased once by the first sweep, which is the feature working (the user's ruling, 2026-10-05). On UAT
    /// that is one task, scenario 110's board pack.</para>
    ///
    /// <para><b>Down</b> drops both. Nothing refuses: what is lost is a counter and a stamp. Up again would restart
    /// the sequence at 0, so a mail client holding a higher one would ignore the next update; and the sweep would chase
    /// once more any overdue task it had chased.</para>
    /// </remarks>
    public partial class CompanyScheduleInvitesAndTaskChase : Migration
    {
        private const string Events = "CompanyEvents";
        private const string Tasks = "EventTasks";

        private static string AddColumn(string table, string column, string definition) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        /// <remarks>
        /// ⚠ The default constraint is looked up rather than named: on a model-built database the name is
        /// server-generated, so guessing it would leave the column undroppable.
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

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // D-14: 0 for every existing event — none has ever been sent a calendar file.
            migrationBuilder.Sql(AddColumn(Events, "CalendarSequence",
                "int NOT NULL CONSTRAINT [DF_CompanyEvents_CalendarSequence] DEFAULT (0)"));

            // F-34: null for every task — one already overdue is chased once by the first sweep.
            migrationBuilder.Sql(AddColumn(Tasks, "OverdueChasedAt", "datetime2 NULL"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropColumn(Tasks, "OverdueChasedAt"));
            migrationBuilder.Sql(DropColumn(Events, "CalendarSequence"));
        }
    }
}
