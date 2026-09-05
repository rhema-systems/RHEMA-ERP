using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Whether an award accepts a nomination from the nominee themselves (area 14 slice 4).
    /// </summary>
    /// <remarks>
    /// <para><b>Why it is a setting and not a rule.</b> TDC's <i>Staff Awards Changes</i> note has
    /// employees nominating each other and explicitly nominating managers, but says nothing about
    /// putting one's own name forward. Enterprise practice does not treat that as one question:
    /// peer or manager nomination is the default, and self-nomination is granted per award —
    /// normally to innovation, suggestion and improvement awards, where the achievement is something
    /// the nominee can evidence, and not to employee-of-the-month or values awards, where being
    /// chosen by somebody else is the substance of the award. An award decided by a staff vote is
    /// the strongest case for barring it.</para>
    ///
    /// <para><b>Why the default is false.</b> The awards the note actually describes — a best
    /// employee award, staff voting, nominating managers — are the kind practice bars. Defaulting to
    /// true would ship the more surprising behaviour to every award nobody had configured, and it
    /// would do so silently. False makes the permissive case something HR turns on deliberately, per
    /// award, and makes TDC's eventual answer a data change rather than a code change.</para>
    ///
    /// <para>Scaffolded by the user, rewritten here into guarded SQL so it is safe on a model-built
    /// database as well as a migrated one, and listed in <c>FastBuildMigrationMetadata.cs</c>.</para>
    /// </remarks>
    public partial class AddAwardSelfNomination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'AllowSelfNomination' AND object_id = OBJECT_ID('dbo.AwardTypes'))
    ALTER TABLE [dbo].[AwardTypes] ADD [AllowSelfNomination] bit NOT NULL
        CONSTRAINT [DF_AwardTypes_AllowSelfNomination] DEFAULT 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'AllowSelfNomination' AND object_id = OBJECT_ID('dbo.AwardTypes'))
BEGIN
    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_AwardTypes_AllowSelfNomination')
        ALTER TABLE [dbo].[AwardTypes] DROP CONSTRAINT [DF_AwardTypes_AllowSelfNomination];
    ALTER TABLE [dbo].[AwardTypes] DROP COLUMN [AllowSelfNomination];
END");
        }
    }
}
