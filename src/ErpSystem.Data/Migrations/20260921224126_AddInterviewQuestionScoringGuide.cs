using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 4, lane F: an interview question can say what a good answer looks like.
    /// </summary>
    /// <remarks>
    /// <para><b>Why</b> (round 4 § 3 defect 20). <c>JobInterviewQuestionDetail</c> holds a question,
    /// a weight and a score band — and no note on what earns the top of that band. On screen that
    /// gap is invisible, because the person scoring is usually the person who wrote the question.
    /// On paper it is the whole problem: a printed sheet is handed to a panelist who did not write
    /// it, and a bare "Score ___ / 10" against a question they are reading for the first time is an
    /// invitation to score it by feel.</para>
    ///
    /// <para><b>⚠ Nullable, no default, and that is the design.</b> Null means "nobody wrote one",
    /// and the sheet prints the question without a guide rather than an empty guide box. An empty
    /// string would be a guide that says nothing, which prints as a ruled box the panelist assumes
    /// they were meant to receive filled in.</para>
    ///
    /// <para><b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration
    /// since <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder
    /// create the schema from the EF model, so a rebuilt database already has this column and a
    /// bare <c>AddColumn</c> stops the chain for everyone.</para>
    /// </remarks>
    public partial class AddInterviewQuestionScoringGuide : Migration
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

        private const string QuestionDetails = "JobInterviewQuestionDetails";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AddColumn(QuestionDetails, "ScoringGuide", "nvarchar(2000) NULL"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropColumn(QuestionDetails, "ScoringGuide"));
        }
    }
}
