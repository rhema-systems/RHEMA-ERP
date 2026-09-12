using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// The staff vote: ballots, and the electorate that may cast them (area 14 slice 5).
    /// </summary>
    /// <remarks>
    /// <para><b>The centre of TDC's note, and nothing modelled it.</b> <i>"HR will setup the
    /// eligibility criteria, then employees or management will do the nomination, and then staff can
    /// vote for who is supposed to win"</i>. Before this migration a search for <c>AwardVote</c>,
    /// <c>Ballot</c> or <c>CastVote</c> across the solution returned nothing at all.</para>
    ///
    /// <para><b>One ballot per voter per cycle, enforced by a unique index rather than by a service
    /// check.</b> The question the note asks is "who is supposed to win" — a single choice among the
    /// nominees, not approval of each in turn. The index is filtered on <c>IsDeleted = 0</c> so that
    /// withdrawing a ballot leaves room for a replacement; without the filter a soft-deleted row
    /// would keep the voter permanently locked out of their own vote.</para>
    ///
    /// <para><b>Why the voter is recorded at all.</b> One-vote-per-person cannot be enforced without
    /// knowing who voted, and a disputed result has to be auditable. No read surface returns who
    /// voted for whom — the tally is a count — but the ballot is not anonymous to the database and
    /// should not be described to employees as if it were.</para>
    ///
    /// <para><b><c>AwardTypeTargets.Purpose</c> lets one table scope two different questions.</b>
    /// The note asks for both — who may win (<i>"it will qualify some employees"</i>) and who may
    /// vote (<i>"a section of the employees or all of them"</i>) — and they are not the same set: a
    /// department might nominate from its own staff while the whole company votes. They share the
    /// table because they share the shape, and because sharing it keeps the matching logic in one
    /// place instead of two that can drift.</para>
    ///
    /// <para>The default is <c>1</c> (<c>Eligibility</c>), which is what every existing row means:
    /// those targets were written before voting existed and scoped who could win. ⚠ The eligibility
    /// evaluator was changed in the same slice to filter on this column — without that filter, a
    /// rule saying "the whole company votes" would silently have become a rule about who may
    /// win.</para>
    ///
    /// <para>Scaffolded by the user, rewritten here into guarded SQL so it is safe on a model-built
    /// database as well as a migrated one, and listed in <c>FastBuildMigrationMetadata.cs</c>.</para>
    /// </remarks>
    public partial class AddAwardVoting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'Purpose' AND object_id = OBJECT_ID('dbo.AwardTypeTargets'))
    ALTER TABLE [dbo].[AwardTypeTargets] ADD [Purpose] int NOT NULL
        CONSTRAINT [DF_AwardTypeTargets_Purpose] DEFAULT 1;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AwardTypeTargets_AwardTypeId_Purpose' AND object_id = OBJECT_ID('dbo.AwardTypeTargets'))
    CREATE INDEX [IX_AwardTypeTargets_AwardTypeId_Purpose] ON [dbo].[AwardTypeTargets] ([AwardTypeId], [Purpose]);");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.AwardVotes', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AwardVotes] (
        [Id]                uniqueidentifier NOT NULL,
        [AwardCycleId]      uniqueidentifier NOT NULL,
        [AwardNominationId] uniqueidentifier NOT NULL,
        [VoterId]           uniqueidentifier NOT NULL,
        [CastOn]            datetime2        NOT NULL,
        [Justification]     nvarchar(2000)   NULL,
        [CreatedAt]         datetime2        NOT NULL,
        [UpdatedAt]         datetime2        NULL,
        [CreatedBy]         nvarchar(max)    NULL,
        [UpdatedBy]         nvarchar(max)    NULL,
        [CreatedById]       uniqueidentifier NULL,
        [LastModifiedById]  uniqueidentifier NULL,
        [IsDeleted]         bit              NOT NULL,
        [DeletedAt]         datetime2        NULL,
        [DeletedBy]         nvarchar(max)    NULL,
        [TenantId]          uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AwardVotes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AwardVotes_AwardCycles_AwardCycleId] FOREIGN KEY ([AwardCycleId])
            REFERENCES [dbo].[AwardCycles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AwardVotes_AwardNominations_AwardNominationId] FOREIGN KEY ([AwardNominationId])
            REFERENCES [dbo].[AwardNominations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AwardVotes_Employees_VoterId] FOREIGN KEY ([VoterId])
            REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AwardVotes_Tenants_TenantId] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
    );
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AwardVotes_AwardCycleId' AND object_id = OBJECT_ID('dbo.AwardVotes'))
    CREATE INDEX [IX_AwardVotes_AwardCycleId] ON [dbo].[AwardVotes] ([AwardCycleId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AwardVotes_AwardNominationId' AND object_id = OBJECT_ID('dbo.AwardVotes'))
    CREATE INDEX [IX_AwardVotes_AwardNominationId] ON [dbo].[AwardVotes] ([AwardNominationId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AwardVotes_VoterId' AND object_id = OBJECT_ID('dbo.AwardVotes'))
    CREATE INDEX [IX_AwardVotes_VoterId] ON [dbo].[AwardVotes] ([VoterId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AwardVotes_TenantId' AND object_id = OBJECT_ID('dbo.AwardVotes'))
    CREATE INDEX [IX_AwardVotes_TenantId] ON [dbo].[AwardVotes] ([TenantId]);");

            // One ballot per voter per cycle. Filtered on IsDeleted so a withdrawn ballot does not
            // lock the voter out of casting a replacement.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AwardVotes_AwardCycleId_VoterId' AND object_id = OBJECT_ID('dbo.AwardVotes'))
    CREATE UNIQUE INDEX [IX_AwardVotes_AwardCycleId_VoterId] ON [dbo].[AwardVotes] ([AwardCycleId], [VoterId])
        WHERE [IsDeleted] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.AwardVotes', 'U') IS NOT NULL
    DROP TABLE [dbo].[AwardVotes];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AwardTypeTargets_AwardTypeId_Purpose' AND object_id = OBJECT_ID('dbo.AwardTypeTargets'))
    DROP INDEX [IX_AwardTypeTargets_AwardTypeId_Purpose] ON [dbo].[AwardTypeTargets];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'Purpose' AND object_id = OBJECT_ID('dbo.AwardTypeTargets'))
BEGIN
    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_AwardTypeTargets_Purpose')
        ALTER TABLE [dbo].[AwardTypeTargets] DROP CONSTRAINT [DF_AwardTypeTargets_Purpose];
    ALTER TABLE [dbo].[AwardTypeTargets] DROP COLUMN [Purpose];
END");
        }
    }
}
