using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// The long-service ladder, and the disciplinary exemption (area 14 slice 9, AWD-14 / AWD-15).
    /// </summary>
    /// <remarks>
    /// <para><b>The ladder is a table, not a constant.</b> TDC's note asks us to <i>"define the basis
    /// for the long service awards"</i> — an instruction to build the mechanism rather than a
    /// statement of the policy. So the milestones are rows HR owns, one per number of years per
    /// award, carrying what that rung is worth. Decision <b>D-8</b>: configurable, seeded at
    /// 10/15/20/25/30, and only the years are seeded — money and leave are left empty because TDC
    /// has not said what a twenty-year award is worth and a seeded figure would look approved.</para>
    ///
    /// <para><b>The unique index is filtered on <c>IsDeleted</c>, and that is the point of it.</b>
    /// Area 13 spent five faces of one defect learning that a soft delete does not release a unique
    /// index: a rung HR retires must free its year for a replacement, and an unfiltered index would
    /// refuse the replacement with a constraint violation nobody could act on.</para>
    ///
    /// <para><b>The two <c>AwardTypes</c> columns are the exemption TDC asked for</b> — <i>"any
    /// negative records such as disciplinary action, then you are exempted"</i>. The flag defaults to
    /// <b>false</b> because they stated the rule under their Long Service heading, and applying it to
    /// an Employee of the Month award would be extending a policy they did not write. The months
    /// column is <b>nullable</b> because null is the note read literally — no horizon — so a value
    /// there relaxes the rule rather than tightening it, and an unconfigured award behaves as the
    /// requirement is written rather than granting an amnesty nobody approved.</para>
    ///
    /// <para>Scaffolded by the user, rewritten here into guarded SQL so it is safe on a model-built
    /// database as well as a migrated one, and listed in <c>FastBuildMigrationMetadata.cs</c> —
    /// without which it never runs at all.</para>
    /// </remarks>
    public partial class AddLongServiceMilestoneAndDisciplinaryCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── AwardTypes: the disciplinary exemption (AWD-15) ──────────────────────────────────
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'DisqualifyOnDisciplinaryRecord' AND object_id = OBJECT_ID('dbo.AwardTypes'))
    ALTER TABLE [dbo].[AwardTypes] ADD [DisqualifyOnDisciplinaryRecord] bit NOT NULL CONSTRAINT [DF_AwardTypes_DisqualifyOnDisciplinaryRecord] DEFAULT 0;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'DisqualifyingDisciplineMonths' AND object_id = OBJECT_ID('dbo.AwardTypes'))
    ALTER TABLE [dbo].[AwardTypes] ADD [DisqualifyingDisciplineMonths] int NULL;");

            // ── LongServiceMilestones: the ladder (AWD-14) ───────────────────────────────────────
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LongServiceMilestones' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[LongServiceMilestones] (
        [Id]                uniqueidentifier NOT NULL,
        [AwardTypeId]       uniqueidentifier NOT NULL,
        [Years]             int              NOT NULL,
        [MonetaryAmount]    decimal(18,2)    NULL,
        [LeaveDaysBonus]    int              NULL,
        [Benefits]          nvarchar(1000)   NULL,
        [Name]              nvarchar(200)    NULL,
        [IsActive]          bit              NOT NULL,
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
        CONSTRAINT [PK_LongServiceMilestones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LongServiceMilestones_AwardTypes_AwardTypeId]
            FOREIGN KEY ([AwardTypeId]) REFERENCES [dbo].[AwardTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LongServiceMilestones_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
    );
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LongServiceMilestones_AwardTypeId' AND object_id = OBJECT_ID('dbo.LongServiceMilestones'))
    CREATE INDEX [IX_LongServiceMilestones_AwardTypeId] ON [dbo].[LongServiceMilestones] ([AwardTypeId]);");

            // ⚠ Filtered on IsDeleted on purpose. A retired rung must release its year so a
            // replacement can take it — the area-13 lesson, which cost five faces of one defect.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LongServiceMilestones_AwardTypeId_Years' AND object_id = OBJECT_ID('dbo.LongServiceMilestones'))
    CREATE UNIQUE INDEX [IX_LongServiceMilestones_AwardTypeId_Years]
        ON [dbo].[LongServiceMilestones] ([AwardTypeId], [Years]) WHERE [IsDeleted] = 0;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LongServiceMilestones_TenantId' AND object_id = OBJECT_ID('dbo.LongServiceMilestones'))
    CREATE INDEX [IX_LongServiceMilestones_TenantId] ON [dbo].[LongServiceMilestones] ([TenantId]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LongServiceMilestones' AND schema_id = SCHEMA_ID('dbo'))
    DROP TABLE [dbo].[LongServiceMilestones];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_AwardTypes_DisqualifyOnDisciplinaryRecord')
    ALTER TABLE [dbo].[AwardTypes] DROP CONSTRAINT [DF_AwardTypes_DisqualifyOnDisciplinaryRecord];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'DisqualifyOnDisciplinaryRecord' AND object_id = OBJECT_ID('dbo.AwardTypes'))
    ALTER TABLE [dbo].[AwardTypes] DROP COLUMN [DisqualifyOnDisciplinaryRecord];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'DisqualifyingDisciplineMonths' AND object_id = OBJECT_ID('dbo.AwardTypes'))
    ALTER TABLE [dbo].[AwardTypes] DROP COLUMN [DisqualifyingDisciplineMonths];");
        }
    }
}
