using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// An award type declares where its candidates come from and how its winner is chosen
    /// (area 14 slice 2, decision D-3).
    /// </summary>
    /// <remarks>
    /// <para><b>Why two columns and not one.</b> TDC's <i>Staff Awards Changes</i> note varies the
    /// two independently. <i>"HR will setup the eligibility criteria, then employees or management
    /// will do the nomination, and then staff can vote"</i> pairs open nomination with a vote;
    /// <i>"some might not have to go through the employee vote since management will decide and
    /// award"</i> pairs the same open nomination with a management decision; and the committee
    /// section pairs it with scoring. A single column would have forced a fixed menu of
    /// combinations and lost the ones the note actually describes.</para>
    ///
    /// <para><b>Why the defaults are what they are.</b> Both columns are NOT NULL with a default,
    /// so every row written before they existed acquires a value — and the value has to be the
    /// safest description of a row nobody classified. <c>NominationSource = 1</c>
    /// (<c>OpenNomination</c>) says candidates were nominated, which is what the old model assumed.
    /// <c>WinnerDecision = 2</c> (<c>CommitteeScore</c>) says a committee decided — deliberately
    /// <b>not</b> <c>StaffVote</c>, because claiming a ballot that never happened would be a
    /// statement about the past rather than an absence of one.</para>
    ///
    /// <para><b>Why <c>AutoGenerateNominees</c> is dropped rather than kept.</b> It was a boolean
    /// mapped through all three DTOs and read by no logic anywhere in the solution — checked before
    /// removing it. It means exactly what <c>NominationSource = PerformanceTriggered</c> now means,
    /// and two fields carrying one fact are free to disagree. That is the same defect this area's
    /// slice 1 fixed for the team-nominee employee id.</para>
    ///
    /// <para>The drop is guarded on the column's existence <b>and</b> on its default constraint:
    /// SQL Server refuses to drop a column while a default constraint is bound to it, and the
    /// constraint's name is server-generated, so it has to be looked up rather than named.</para>
    ///
    /// <para>Scaffolded by the user, rewritten here into guarded SQL so it is safe on a
    /// model-built database as well as a migrated one, and listed in
    /// <c>FastBuildMigrationMetadata.cs</c>.</para>
    /// </remarks>
    public partial class AddAwardSelectionModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'NominationSource' AND object_id = OBJECT_ID('dbo.AwardTypes'))
    ALTER TABLE [dbo].[AwardTypes] ADD [NominationSource] int NOT NULL CONSTRAINT [DF_AwardTypes_NominationSource] DEFAULT 1;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'WinnerDecision' AND object_id = OBJECT_ID('dbo.AwardTypes'))
    ALTER TABLE [dbo].[AwardTypes] ADD [WinnerDecision] int NOT NULL CONSTRAINT [DF_AwardTypes_WinnerDecision] DEFAULT 2;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AwardTypes_NominationSource' AND object_id = OBJECT_ID('dbo.AwardTypes'))
    CREATE INDEX [IX_AwardTypes_NominationSource] ON [dbo].[AwardTypes] ([NominationSource]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AwardTypes_WinnerDecision' AND object_id = OBJECT_ID('dbo.AwardTypes'))
    CREATE INDEX [IX_AwardTypes_WinnerDecision] ON [dbo].[AwardTypes] ([WinnerDecision]);");

            // The default constraint's name is server-generated on a model-built database, so it is
            // resolved from sys.default_constraints rather than named. Without this the DROP COLUMN
            // fails with "The object 'DF__AwardType__AutoG__…' is dependent on column …".
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'AutoGenerateNominees' AND object_id = OBJECT_ID('dbo.AwardTypes'))
BEGIN
    DECLARE @constraint sysname;
    SELECT @constraint = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.AwardTypes') AND c.name = 'AutoGenerateNominees';

    IF @constraint IS NOT NULL
        EXEC('ALTER TABLE [dbo].[AwardTypes] DROP CONSTRAINT [' + @constraint + ']');

    ALTER TABLE [dbo].[AwardTypes] DROP COLUMN [AutoGenerateNominees];
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'AutoGenerateNominees' AND object_id = OBJECT_ID('dbo.AwardTypes'))
    ALTER TABLE [dbo].[AwardTypes] ADD [AutoGenerateNominees] bit NOT NULL CONSTRAINT [DF_AwardTypes_AutoGenerateNominees] DEFAULT 0;");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AwardTypes_WinnerDecision' AND object_id = OBJECT_ID('dbo.AwardTypes'))
    DROP INDEX [IX_AwardTypes_WinnerDecision] ON [dbo].[AwardTypes];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AwardTypes_NominationSource' AND object_id = OBJECT_ID('dbo.AwardTypes'))
    DROP INDEX [IX_AwardTypes_NominationSource] ON [dbo].[AwardTypes];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'WinnerDecision' AND object_id = OBJECT_ID('dbo.AwardTypes'))
BEGIN
    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_AwardTypes_WinnerDecision')
        ALTER TABLE [dbo].[AwardTypes] DROP CONSTRAINT [DF_AwardTypes_WinnerDecision];
    ALTER TABLE [dbo].[AwardTypes] DROP COLUMN [WinnerDecision];
END");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'NominationSource' AND object_id = OBJECT_ID('dbo.AwardTypes'))
BEGIN
    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_AwardTypes_NominationSource')
        ALTER TABLE [dbo].[AwardTypes] DROP CONSTRAINT [DF_AwardTypes_NominationSource];
    ALTER TABLE [dbo].[AwardTypes] DROP COLUMN [NominationSource];
END");
        }
    }
}
