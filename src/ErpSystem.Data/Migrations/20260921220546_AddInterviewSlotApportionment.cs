using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 4, lane C: an interview remembers how its day was divided.
    /// </summary>
    /// <remarks>
    /// <para><b>Why.</b> <c>JobInterviewee.SlotStartTime</c> and <c>SlotEndTime</c> have existed
    /// since the port, and <c>CreateJobInterviewDto</c> has accepted per-candidate slots all along —
    /// but nothing ever computed one, validated one, or noticed when a reschedule left them behind.
    /// These three columns are what let the day be laid out again rather than re-asked.</para>
    ///
    /// <para><b>The defect they close</b> (round 4 § 3 defect 23): <c>RescheduleAsync</c> moved the
    /// session window and left every candidate's slot where it was, then emailed each candidate
    /// their <i>original</i> time against the <i>new</i> date, with a fresh confirmation token
    /// inviting them to confirm it. Move a 09:00–11:00 session to 14:00–16:00 and everyone was told
    /// to arrive at 09:20.</para>
    ///
    /// <para><b>⚠ All three are NULLABLE, with no default, and that is the design.</b> Null means
    /// "this day was never apportioned" — slots were typed by hand, or there are none — and the
    /// reschedule path reads it as exactly that, clearing stale slots rather than shifting a layout
    /// that never existed. A <c>DEFAULT 0</c> here would be the recurring trap this module has hit
    /// four times: EF's scaffolded default for a value type is zero, and zero is almost never the
    /// real default. A zero-minute slot is not a shorter interview, it is a broken one.</para>
    ///
    /// <para><b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration
    /// since <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder
    /// create the schema from the EF model, so a rebuilt database already has these columns and a
    /// bare <c>AddColumn</c> stops the chain for everyone.</para>
    ///
    /// <para><b>Why <c>BreaksJson</c> is a column and not a table.</b> A break has no identity
    /// anyone refers to, nothing points at one, and the set is only ever read whole while laying out
    /// one day. A child table would buy cascade deletes and an id nobody needs, at the cost of a
    /// join on every read of an interview. Same call as the appraisal breakdown and the application
    /// profile snapshot.</para>
    /// </remarks>
    public partial class AddInterviewSlotApportionment : Migration
    {
        private static string AddColumn(string table, string column, string definition) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        /// <remarks>
        /// The default constraint is looked up rather than named, so this works whether the column
        /// was created by this migration or by the EF model on a rebuilt database, where the name is
        /// server-generated. Guessing it would leave the column undroppable.
        /// </remarks>
        private static string DropColumn(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
BEGIN
    DECLARE @df_{column} sysname;
    SELECT @df_{column} = dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.{table}') AND c.name = '{column}';
    IF @df_{column} IS NOT NULL
        EXEC('ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [' + @df_{column} + ']');
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
END";

        private const string Interviews = "JobInterviews";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AddColumn(Interviews, "SlotMinutes", "int NULL"));
            migrationBuilder.Sql(AddColumn(Interviews, "SlotBufferMinutes", "int NULL"));
            migrationBuilder.Sql(AddColumn(Interviews, "BreaksJson", "nvarchar(max) NULL"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropColumn(Interviews, "BreaksJson"));
            migrationBuilder.Sql(DropColumn(Interviews, "SlotBufferMinutes"));
            migrationBuilder.Sql(DropColumn(Interviews, "SlotMinutes"));
        }
    }
}
