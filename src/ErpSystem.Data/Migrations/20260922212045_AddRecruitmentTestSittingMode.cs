using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 4, lane E6: how a recruitment test was sat — online, or on paper and entered by HR.
    /// </summary>
    /// <remarks>
    /// <para><b>Why a column and not an inference.</b> A script the candidate submitted in the portal
    /// and one HR typed in from paper are different kinds of evidence, and a recruitment decision can
    /// be challenged. The record says which, rather than leaving a reader to reason it out of which
    /// other columns happen to be null (a paper sitting has no access token, but "no token" is not a
    /// definition anybody should have to know).</para>
    ///
    /// <para><b>The default is 1 (Online), and it is real.</b> Every sitting that existed before this
    /// column was sat online — paper sittings could not be recorded until this lane — so a
    /// <c>DEFAULT 1</c> is what gives existing rows the true value. This time the scaffold agreed,
    /// because the model declares <c>HasDefaultValue(Online)</c>; left to EF's own convention it would
    /// have written <c>0</c>, which is not a member of the enum — the trap this module has hit five
    /// times before (see <c>20260922084439_AddOfferValidityDays</c>). The repair statement below
    /// is the belt to that brace: keyed on the VALUE, idempotent, and a no-op on any database this
    /// migration has already reached.</para>
    ///
    /// <para><b>⚠ The scaffolded <c>AddColumn</c> was replaced with guarded SQL</b>, as on every HR
    /// migration since <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT
    /// builder create the schema from the EF model, so a rebuilt database already has this column and
    /// a bare <c>AddColumn</c> stops the chain for everyone.</para>
    ///
    /// <para><b>The constraint is named here and found by column in <c>Down</c>.</b> A database built
    /// from the model gets a default constraint with a name SQL Server invents, so dropping it by the
    /// name this migration uses would fail there.</para>
    /// </remarks>
    public partial class AddRecruitmentTestSittingMode : Migration
    {
        private const string Table = "RecruitmentTestSittings";
        private const string Column = "Mode";
        private const int Online = 1;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Table}', '{Column}') IS NULL
    ALTER TABLE [dbo].[{Table}] ADD [{Column}] int NOT NULL
        CONSTRAINT [DF_{Table}_{Column}] DEFAULT ({Online});");

            // Any sitting holding a value that is not a member — Online (1) or Paper (2) — predates
            // paper sittings and was therefore sat online.
            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Table}', '{Column}') IS NOT NULL
    UPDATE [dbo].[{Table}] SET [{Column}] = {Online} WHERE [{Column}] NOT IN (1, 2);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Table}', '{Column}') IS NOT NULL
BEGIN
    DECLARE @df sysname;
    SELECT @df = dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.{Table}') AND c.name = '{Column}';
    IF @df IS NOT NULL
        EXEC('ALTER TABLE [dbo].[{Table}] DROP CONSTRAINT [' + @df + ']');
    ALTER TABLE [dbo].[{Table}] DROP COLUMN [{Column}];
END");
        }
    }
}
