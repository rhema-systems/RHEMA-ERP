using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 4, lane F4: a scorecard records how it reached the system, and who put it there.
    /// </summary>
    /// <remarks>
    /// <para><b>Why.</b> A panel that scored on paper hands HR a signed sheet, and HR types it in.
    /// The row that came out was indistinguishable from one the panelist typed themselves — same
    /// panelist id, same marks, same recommendation. The scorecard is the panelist's verdict either
    /// way, but the keystrokes were somebody else's, and an audit trail that cannot tell the
    /// difference is not an audit trail.</para>
    ///
    /// <para><b>⚠ Neither column is written by the client.</b> <c>CreateJobInterviewScoreSummaryDto</c>
    /// deliberately carries no <c>ScoreSource</c> field. Provenance is derived in
    /// <c>EnsureCanScoreAsAsync</c> from the one fact that settles it — whether the caller <i>is</i>
    /// the panelist — because a source a caller could assert is worth nothing in an audit, and a DTO
    /// field the server ignores reads to the next developer as a control that exists when it does
    /// not.</para>
    ///
    /// <para><b>⚠ <c>ScoreSource</c> is NULLABLE, with no default, and that is the design.</b> Null
    /// means "recorded before this was tracked", which is <i>not</i> the same claim as
    /// <c>Online</c>. Backfilling the existing rows to 1 would assert of every historical scorecard
    /// that the panelist typed it themselves — a fact nobody checked, written into the very trail
    /// this column exists to make trustworthy. This is the recurring trap in this module in its
    /// sharpest form: EF's scaffolded default for a value type is zero, zero is almost never the
    /// real default, and here even a <i>plausible</i> default would be a fabrication.</para>
    ///
    /// <para><b>⚠ <c>FiledByHrOnBehalfOfEmployeeId</c> is an EMPLOYEE id, and the name says so.</b>
    /// The round 4 plan called it <c>...UserId</c>, but the value that reaches the service is
    /// <c>_currentUser.EmployeeId</c> — passed into a parameter the controller family misleadingly
    /// calls <c>createdByUserId</c>. A column named <c>...UserId</c> holding an employee id is a
    /// trap that only surfaces the day somebody joins it to <c>AspNetUsers</c> and gets nothing
    /// back. HR's actor columns are employee references throughout, so this follows the house
    /// convention rather than inventing a second one.</para>
    ///
    /// <para><b>Why a column at all, when <c>CreatedBy</c> exists.</b> A scorecard is
    /// <b>upserted</b>: a correction rewrites <c>UpdatedBy</c> and leaves <c>CreatedBy</c> pointing
    /// at whoever happened to be first. Provenance inferred from those two is wrong in exactly the
    /// case that matters — the card HR filed and the panelist later corrected.</para>
    ///
    /// <para><b>The foreign key is NO ACTION</b>, the EF default for an optional reference. Deleting
    /// an employee must not cascade into somebody else's interview record, and the alternative —
    /// nulling the column — would quietly erase who filed a card while leaving the card still
    /// claiming it was filed on the panelist's behalf, by nobody.</para>
    ///
    /// <para><b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration
    /// since <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder
    /// create the schema from the EF model, so a rebuilt database already has these columns, this
    /// index and this constraint — and a bare <c>AddColumn</c> stops the chain for everyone.</para>
    /// </remarks>
    public partial class AddInterviewScoreProvenance : Migration
    {
        private static string AddColumn(string table, string column, string definition) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        private static string CreateIndex(string table, string index, string columns) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{index}] ON [dbo].[{table}] ({columns});";

        private static string DropIndex(string table, string index) => $@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    DROP INDEX [{index}] ON [dbo].[{table}];";

        private static string AddForeignKey(
            string table, string fk, string column, string principalTable, string onDelete = "NO ACTION") => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
   AND OBJECT_ID('dbo.{principalTable}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{fk}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] WITH CHECK
        ADD CONSTRAINT [{fk}] FOREIGN KEY ([{column}]) REFERENCES [dbo].[{principalTable}] ([Id]) ON DELETE {onDelete};";

        private static string DropForeignKey(string table, string fk) => $@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{fk}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [{fk}];";

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

        private const string Summaries = "JobInterviewScoreSummaries";
        private const string FiledBy = "FiledByHrOnBehalfOfEmployeeId";
        private const string FiledByIndex = "IX_JobInterviewScoreSummaries_FiledByHrOnBehalfOfEmployeeId";
        private const string FiledByFk = "FK_JobInterviewScoreSummaries_Employees_FiledByHrOnBehalfOfEmployeeId";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AddColumn(Summaries, "ScoreSource", "int NULL"));
            migrationBuilder.Sql(AddColumn(Summaries, FiledBy, "uniqueidentifier NULL"));
            migrationBuilder.Sql(CreateIndex(Summaries, FiledByIndex, $"[{FiledBy}]"));
            migrationBuilder.Sql(AddForeignKey(Summaries, FiledByFk, FiledBy, "Employees"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropForeignKey(Summaries, FiledByFk));
            migrationBuilder.Sql(DropIndex(Summaries, FiledByIndex));
            migrationBuilder.Sql(DropColumn(Summaries, FiledBy));
            migrationBuilder.Sql(DropColumn(Summaries, "ScoreSource"));
        }
    }
}
