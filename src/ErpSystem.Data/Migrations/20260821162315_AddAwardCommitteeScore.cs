using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// A committee review becomes a score instead of a verdict (area 14 slice 6, decision D-2).
    /// </summary>
    /// <remarks>
    /// <para><b>Why.</b> TDC's <i>Staff Awards Changes</i> note is explicit: <i>"the committee
    /// members will score, and the winner will be the one with the highest average score"</i>. The
    /// column was <c>bool? Approved</c>, which cannot rank anything — two nominations approved by
    /// everybody were indistinguishable, so the award could not be decided from the data the
    /// committee had entered.</para>
    ///
    /// <para><b>The score was already being discarded.</b> <c>SubmitCommitteeReviewDto</c> carried
    /// <c>[Range(1, 100)] int? Score</c> before this migration, no column existed to hold it, and the
    /// read mapper contained the literal <c>Score = null</c>. A committee member could submit 87,
    /// receive 200, and the system kept nothing and reported <c>null</c> back. That is where the
    /// 1–100 scale comes from: it was already chosen, just never stored.</para>
    ///
    /// <para><b>Existing verdicts are retired, not converted — and this is the important part.</b>
    /// Measured before writing this migration, the reference database held <b>9</b> review rows, all
    /// of them <c>Approved = 1</c> and all of them fixtures left by this area's own slice-1 harness.
    /// The scaffolded migration would have given every one of them <c>Score = 0</c>.</para>
    ///
    /// <para>That is not a lossless default, it is an <b>inversion</b>: a committee member who
    /// approved a nomination would be recorded as having scored it zero out of a hundred, and the
    /// scoring service counts rows — so those zeros would drag the average of the very nominations
    /// their authors supported. An approve/reject cannot be translated into a rank without inventing
    /// a number, and inventing one is exactly what this area has spent six slices removing.</para>
    ///
    /// <para>So the rows are soft-deleted instead. A retired verdict is recoverable and says
    /// truthfully that nobody has scored that nomination yet; a fabricated 0 would say something
    /// false and be indistinguishable from a real score. ⚠ Were this ever run against a database
    /// holding genuine committee verdicts, the same reasoning applies but the loss would be real:
    /// export them first, and have the committee re-score.</para>
    ///
    /// <para><b>NOT NULL with a default of 0 is safe because the retirement above runs first</b>, so
    /// no surviving row ever takes the default. A review row means "this member has scored"; the
    /// scoring service counts rows to test <c>MinRequiredReviewers</c>, so a row with no real score
    /// would count as a reviewer while contributing a meaningless value to the mean. A member who has
    /// not scored has no row — which is also why <c>AwardDataSeeder</c>'s placeholder "pending
    /// review" rows were removed in the same slice.</para>
    ///
    /// <para>Scaffolded by the user, rewritten here into guarded SQL so it is safe on a model-built
    /// database as well as a migrated one, and listed in <c>FastBuildMigrationMetadata.cs</c>.</para>
    /// </remarks>
    public partial class AddAwardCommitteeScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Retire any existing verdict BEFORE the score column exists, so no row is ever
            // simultaneously live and carrying a fabricated 0. See the remarks above.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'Approved' AND object_id = OBJECT_ID('dbo.AwardNominationReviews'))
    UPDATE [dbo].[AwardNominationReviews]
    SET [IsDeleted] = 1,
        [DeletedAt] = SYSUTCDATETIME(),
        [DeletedBy] = 'AddAwardCommitteeScore: approve/reject retired, it cannot be converted to a score'
    WHERE [IsDeleted] = 0;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'Score' AND object_id = OBJECT_ID('dbo.AwardNominationReviews'))
    ALTER TABLE [dbo].[AwardNominationReviews] ADD [Score] int NOT NULL
        CONSTRAINT [DF_AwardNominationReviews_Score] DEFAULT 0;");

            // The old default constraint's name is server-generated on a model-built database, so it
            // is resolved rather than named. Without this the DROP COLUMN fails with
            // "The object 'DF__AwardNomi__Appro__…' is dependent on column 'Approved'."
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'Approved' AND object_id = OBJECT_ID('dbo.AwardNominationReviews'))
BEGIN
    DECLARE @constraint sysname;
    SELECT @constraint = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.AwardNominationReviews') AND c.name = 'Approved';

    IF @constraint IS NOT NULL
        EXEC('ALTER TABLE [dbo].[AwardNominationReviews] DROP CONSTRAINT [' + @constraint + ']');

    ALTER TABLE [dbo].[AwardNominationReviews] DROP COLUMN [Approved];
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'Approved' AND object_id = OBJECT_ID('dbo.AwardNominationReviews'))
    ALTER TABLE [dbo].[AwardNominationReviews] ADD [Approved] bit NULL;");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'Score' AND object_id = OBJECT_ID('dbo.AwardNominationReviews'))
BEGIN
    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_AwardNominationReviews_Score')
        ALTER TABLE [dbo].[AwardNominationReviews] DROP CONSTRAINT [DF_AwardNominationReviews_Score];
    ALTER TABLE [dbo].[AwardNominationReviews] DROP COLUMN [Score];
END");
        }
    }
}
