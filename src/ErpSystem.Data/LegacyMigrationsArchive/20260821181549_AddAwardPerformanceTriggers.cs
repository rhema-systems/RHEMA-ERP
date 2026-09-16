using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// What puts an employee forward for an award automatically (area 14 slice 7, AWD-08).
    /// </summary>
    /// <remarks>
    /// <para><b>Why two columns.</b> TDC's <i>Staff Awards Changes</i> note says <i>"some of the
    /// nomination will be due to performance or target reached"</i> — two different triggers, so two
    /// fields. <c>MinPerformanceScore</c> reads the appraisal store, <c>MinGoalsAchieved</c> reads
    /// completed goals, and setting both means a candidate must satisfy both: an award asking for
    /// performance <i>and</i> targets should not settle for one of them.</para>
    ///
    /// <para><b>Both nullable, and null means "no trigger".</b> An award that takes its candidates
    /// from performance but has neither set generates nobody and says why, rather than quietly
    /// finding nothing. There is no sensible default here: a threshold is a policy TDC has not
    /// stated, and defaulting to any number would invent one that looks authoritative.</para>
    ///
    /// <para>⚠ <b>What the data can support, measured 2026-08-21.</b> The live store holds
    /// <b>4,328</b> appraisals of which <b>18</b> carry an <c>OverallScore</c>, and <b>16</b>
    /// employees have a goal recorded at 100%. So a real generation run finds a handful of people
    /// out of 5,579 — the same unmaintained-column shape as <c>DateEmployed</c> (38% populated) and
    /// <c>ExpectedHeadcount</c>. The rule is correct whether or not the data has caught up, and the
    /// generation result reports how many records it examined so that "nobody qualified" can be told
    /// apart from "nobody has been appraised".</para>
    ///
    /// <para><c>decimal(5,2)</c> matches how the appraisal module stores <c>OverallScore</c>, so a
    /// threshold and the value it is compared against cannot disagree by rounding.</para>
    ///
    /// <para>Scaffolded by the user, rewritten here into guarded SQL so it is safe on a model-built
    /// database as well as a migrated one, and listed in <c>FastBuildMigrationMetadata.cs</c>.</para>
    /// </remarks>
    public partial class AddAwardPerformanceTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'MinPerformanceScore' AND object_id = OBJECT_ID('dbo.AwardTypes'))
    ALTER TABLE [dbo].[AwardTypes] ADD [MinPerformanceScore] decimal(5,2) NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'MinGoalsAchieved' AND object_id = OBJECT_ID('dbo.AwardTypes'))
    ALTER TABLE [dbo].[AwardTypes] ADD [MinGoalsAchieved] int NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'MinGoalsAchieved' AND object_id = OBJECT_ID('dbo.AwardTypes'))
    ALTER TABLE [dbo].[AwardTypes] DROP COLUMN [MinGoalsAchieved];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'MinPerformanceScore' AND object_id = OBJECT_ID('dbo.AwardTypes'))
    ALTER TABLE [dbo].[AwardTypes] DROP COLUMN [MinPerformanceScore];");
        }
    }
}
