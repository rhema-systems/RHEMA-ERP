using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Award cycles: the run of an award and the windows in which people may take part
    /// (area 14 slice 3, decision D-4).
    /// </summary>
    /// <remarks>
    /// <para><b>Why a table and not more columns on the nomination.</b> TDC's note requires awards
    /// to be <i>"listed for people to nominate before the voting takes place"</i>. A nomination
    /// carried only <c>Year</c>, <c>Quarter</c> and <c>Month</c>, which say which period the award
    /// is <i>for</i> and cannot say when people may take part in it. The windows belong to the run,
    /// not to each nomination inside it — putting them on the nomination would repeat the same four
    /// dates on every row and let two nominations in one cycle disagree about when it closes.</para>
    ///
    /// <para><b>Every window is nullable, and which ones are required is a service rule, not a
    /// column constraint.</b> An award decided by a staff vote must carry a voting window; one
    /// decided by a committee must not, because a voting window on an award nobody votes on
    /// describes a ballot that will never be held. An award taken by direct management selection has
    /// no nomination stage and so carries neither. That depends on the award type's selection model
    /// — two columns on another table — which is beyond what a CHECK constraint can see, so
    /// <c>AwardCycleService.ValidateWindows</c> owns it and states which field to change.</para>
    ///
    /// <para><b>Status carries no "open" member on purpose.</b> Whether nominations are open right
    /// now is <c>Status == Published</c> and the clock inside the window. Storing it as well would
    /// be two facts about one thing, free to disagree the moment the close date passes — the defect
    /// shape this area has produced four times.</para>
    ///
    /// <para><b><c>AwardNominations.AwardCycleId</c> is nullable</b>, which is also the back-fill
    /// answer: nominations raised before cycles existed belong to no cycle, and that is the truth
    /// about them rather than a gap to be filled with a guess. An award taken by direct management
    /// selection never has one either.</para>
    ///
    /// <para>Scaffolded by the user, rewritten here into guarded SQL so it is safe on a model-built
    /// database as well as a migrated one, and listed in <c>FastBuildMigrationMetadata.cs</c>.</para>
    /// </remarks>
    public partial class AddAwardCycles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.AwardCycles', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AwardCycles] (
        [Id]                 uniqueidentifier NOT NULL,
        [CycleCode]          nvarchar(50)     NOT NULL,
        [Name]               nvarchar(150)    NOT NULL,
        [AwardTypeId]        uniqueidentifier NOT NULL,
        [Year]               int              NOT NULL,
        [Quarter]            int              NULL,
        [Month]              int              NULL,
        [NominationOpensOn]  datetime2        NULL,
        [NominationClosesOn] datetime2        NULL,
        [VotingOpensOn]      datetime2        NULL,
        [VotingClosesOn]     datetime2        NULL,
        [Status]             int              NOT NULL,
        [Notes]              nvarchar(1000)   NULL,
        [CreatedAt]          datetime2        NOT NULL,
        [UpdatedAt]          datetime2        NULL,
        [CreatedBy]          nvarchar(max)    NULL,
        [UpdatedBy]          nvarchar(max)    NULL,
        [CreatedById]        uniqueidentifier NULL,
        [LastModifiedById]   uniqueidentifier NULL,
        [IsDeleted]          bit              NOT NULL,
        [DeletedAt]          datetime2        NULL,
        [DeletedBy]          nvarchar(max)    NULL,
        [TenantId]           uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AwardCycles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AwardCycles_AwardTypes_AwardTypeId] FOREIGN KEY ([AwardTypeId])
            REFERENCES [dbo].[AwardTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AwardCycles_Tenants_TenantId] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
    );
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AwardCycles_AwardTypeId' AND object_id = OBJECT_ID('dbo.AwardCycles'))
    CREATE INDEX [IX_AwardCycles_AwardTypeId] ON [dbo].[AwardCycles] ([AwardTypeId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AwardCycles_AwardTypeId_Year_Quarter_Month' AND object_id = OBJECT_ID('dbo.AwardCycles'))
    CREATE INDEX [IX_AwardCycles_AwardTypeId_Year_Quarter_Month] ON [dbo].[AwardCycles] ([AwardTypeId], [Year], [Quarter], [Month]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AwardCycles_CycleCode' AND object_id = OBJECT_ID('dbo.AwardCycles'))
    CREATE INDEX [IX_AwardCycles_CycleCode] ON [dbo].[AwardCycles] ([CycleCode]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AwardCycles_Status' AND object_id = OBJECT_ID('dbo.AwardCycles'))
    CREATE INDEX [IX_AwardCycles_Status] ON [dbo].[AwardCycles] ([Status]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AwardCycles_TenantId' AND object_id = OBJECT_ID('dbo.AwardCycles'))
    CREATE INDEX [IX_AwardCycles_TenantId] ON [dbo].[AwardCycles] ([TenantId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'AwardCycleId' AND object_id = OBJECT_ID('dbo.AwardNominations'))
    ALTER TABLE [dbo].[AwardNominations] ADD [AwardCycleId] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AwardNominations_AwardCycleId' AND object_id = OBJECT_ID('dbo.AwardNominations'))
    CREATE INDEX [IX_AwardNominations_AwardCycleId] ON [dbo].[AwardNominations] ([AwardCycleId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AwardNominations_AwardCycles_AwardCycleId')
    ALTER TABLE [dbo].[AwardNominations] ADD CONSTRAINT [FK_AwardNominations_AwardCycles_AwardCycleId]
        FOREIGN KEY ([AwardCycleId]) REFERENCES [dbo].[AwardCycles] ([Id]) ON DELETE NO ACTION;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AwardNominations_AwardCycles_AwardCycleId')
    ALTER TABLE [dbo].[AwardNominations] DROP CONSTRAINT [FK_AwardNominations_AwardCycles_AwardCycleId];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AwardNominations_AwardCycleId' AND object_id = OBJECT_ID('dbo.AwardNominations'))
    DROP INDEX [IX_AwardNominations_AwardCycleId] ON [dbo].[AwardNominations];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'AwardCycleId' AND object_id = OBJECT_ID('dbo.AwardNominations'))
    ALTER TABLE [dbo].[AwardNominations] DROP COLUMN [AwardCycleId];");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.AwardCycles', 'U') IS NOT NULL
    DROP TABLE [dbo].[AwardCycles];");
        }
    }
}
