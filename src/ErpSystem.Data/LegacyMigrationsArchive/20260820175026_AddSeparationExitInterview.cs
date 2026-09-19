using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// The exit interview — what the leaver said on the way out (area 9b slice 12).
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>Not an FRD requirement.</b> "Exit interview" appears nowhere in the specification,
    /// and this area declined to invent it. Added at the user's explicit request, and it is a real
    /// part of an exit process regardless: without it, the only record of why somebody left is the
    /// reason the <i>organisation</i> wrote down.</para>
    ///
    /// <para><b><c>PrimaryReason</c> is its own enum, not <c>TerminationReason</c>,</b> and that is
    /// the point of asking. The organisation records "resignation"; the employee says whether it was
    /// the pay or the manager. Same termination reason, completely different facts, and only the
    /// second tells anybody what to fix.</para>
    ///
    /// <para><b>Every rating is nullable and 1–5.</b> Null means not asked; zero would mean the worst
    /// possible answer, and a half-finished form must not read as a damning one. Likewise
    /// <c>WasDeclined</c> is a first-class outcome — people leave angry or in a hurry, and a record
    /// that can only express a completed interview forces an invented one or a blank file.</para>
    ///
    /// <para>The unique index on <c>SeparationId</c> is filtered on <c>IsDeleted</c>: deletes here
    /// are soft, and a soft-deleted record must not block a replacement being taken.</para>
    ///
    /// <para>Scaffolded by the user, rewritten here into guarded SQL, listed in
    /// <c>FastBuildMigrationMetadata.cs</c>.</para>
    /// </remarks>
    public partial class AddSeparationExitInterview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SeparationExitInterviews'))
BEGIN
    CREATE TABLE [dbo].[SeparationExitInterviews] (
        [Id]                       uniqueidentifier NOT NULL,
        [SeparationId]             uniqueidentifier NOT NULL,
        [WasDeclined]              bit              NOT NULL CONSTRAINT [DF_SeparationExitInterviews_WasDeclined] DEFAULT (0),
        [DeclinedReason]           nvarchar(500)    NULL,
        [ConductedOn]              date             NULL,
        [ConductedById]            uniqueidentifier NULL,
        [ConductedByName]          nvarchar(200)    NULL,
        [PrimaryReason]            int              NULL,
        [PrimaryReasonDetail]      nvarchar(2000)   NULL,
        [OverallExperienceRating]  int              NULL,
        [ManagementRating]         int              NULL,
        [PayAndBenefitsRating]     int              NULL,
        [CareerDevelopmentRating]  int              NULL,
        [WouldRecommendEmployer]   bit              NULL,
        [WouldConsiderReturning]   bit              NULL,
        [WhatWorkedWell]           nvarchar(2000)   NULL,
        [WhatShouldChange]         nvarchar(2000)   NULL,
        [AdditionalComments]       nvarchar(2000)   NULL,
        [RecordedById]             uniqueidentifier NULL,
        [RecordedOn]               datetime2        NOT NULL,
        [CreatedAt]                datetime2        NOT NULL,
        [UpdatedAt]                datetime2        NULL,
        [CreatedBy]                nvarchar(max)    NULL,
        [UpdatedBy]                nvarchar(max)    NULL,
        [CreatedById]              uniqueidentifier NULL,
        [LastModifiedById]         uniqueidentifier NULL,
        [IsDeleted]                bit              NOT NULL CONSTRAINT [DF_SeparationExitInterviews_IsDeleted] DEFAULT (0),
        [DeletedAt]                datetime2        NULL,
        [DeletedBy]                nvarchar(max)    NULL,
        [TenantId]                 uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SeparationExitInterviews] PRIMARY KEY ([Id])
    );
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SeparationExitInterviews_EmployeeSeparations_SeparationId')
    ALTER TABLE [dbo].[SeparationExitInterviews] ADD CONSTRAINT [FK_SeparationExitInterviews_EmployeeSeparations_SeparationId]
        FOREIGN KEY ([SeparationId]) REFERENCES [dbo].[EmployeeSeparations] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SeparationExitInterviews_Employees_ConductedById')
    ALTER TABLE [dbo].[SeparationExitInterviews] ADD CONSTRAINT [FK_SeparationExitInterviews_Employees_ConductedById]
        FOREIGN KEY ([ConductedById]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SeparationExitInterviews_Employees_RecordedById')
    ALTER TABLE [dbo].[SeparationExitInterviews] ADD CONSTRAINT [FK_SeparationExitInterviews_Employees_RecordedById]
        FOREIGN KEY ([RecordedById]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SeparationExitInterviews_Tenants_TenantId')
    ALTER TABLE [dbo].[SeparationExitInterviews] ADD CONSTRAINT [FK_SeparationExitInterviews_Tenants_TenantId]
        FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SeparationExitInterview_SeparationId' AND object_id = OBJECT_ID('dbo.SeparationExitInterviews'))
    CREATE UNIQUE INDEX [UX_SeparationExitInterview_SeparationId] ON [dbo].[SeparationExitInterviews] ([SeparationId]) WHERE [IsDeleted] = 0;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationExitInterview_PrimaryReason' AND object_id = OBJECT_ID('dbo.SeparationExitInterviews'))
    CREATE INDEX [IX_SeparationExitInterview_PrimaryReason] ON [dbo].[SeparationExitInterviews] ([PrimaryReason]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationExitInterviews_ConductedById' AND object_id = OBJECT_ID('dbo.SeparationExitInterviews'))
    CREATE INDEX [IX_SeparationExitInterviews_ConductedById] ON [dbo].[SeparationExitInterviews] ([ConductedById]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationExitInterviews_RecordedById' AND object_id = OBJECT_ID('dbo.SeparationExitInterviews'))
    CREATE INDEX [IX_SeparationExitInterviews_RecordedById] ON [dbo].[SeparationExitInterviews] ([RecordedById]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationExitInterviews_TenantId' AND object_id = OBJECT_ID('dbo.SeparationExitInterviews'))
    CREATE INDEX [IX_SeparationExitInterviews_TenantId] ON [dbo].[SeparationExitInterviews] ([TenantId]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SeparationExitInterviews'))
    DROP TABLE [dbo].[SeparationExitInterviews];");
        }
    }
}
