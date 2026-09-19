using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// The separation reminder sweep — a run header and one dispatch row per reminder
    /// (area 9b slice 10, FR-HR-111).
    /// </summary>
    /// <remarks>
    /// <para>FR-HR-111 asks for alerts ahead of a due event; retirement and contract expiry are this
    /// area's two. The sweep uses the tenant's configured lead days — 365 for retirement, 60 for
    /// contract expiry — both comfortably wider than the 30 days the requirement names, so the rule
    /// is met from policy rather than from a second hard-coded threshold that could drift from it.</para>
    ///
    /// <para>Three further kinds come from the pipeline this area built: a clearance with mandatory
    /// lines unanswered, a settlement sitting with Internal Audit, and — the one worth having — a
    /// settlement <b>passed and never completed</b>, which means the employee is still on strength.
    /// That last is the shape of the defect this whole area was opened on, turned into a reminder
    /// the next morning instead of a discovery years later.</para>
    ///
    /// <para><c>DedupeKey</c> is what stops a daily pass becoming noise: kind + subject + due date +
    /// escalation tier. Running the sweep twice in one morning re-raises nothing. Its index is
    /// <c>(TenantId, DedupeKey)</c> because that lookup happens once per candidate per sweep, and it
    /// is the one index that decides whether a daily pass over a whole workforce is cheap.</para>
    ///
    /// <para>Shape follows <c>ProbationReminderRun</c> and <c>DisciplineReminderRun</c> deliberately.
    /// Scaffolded by the user, rewritten here into guarded SQL, listed in
    /// <c>FastBuildMigrationMetadata.cs</c>.</para>
    /// </remarks>
    public partial class AddSeparationReminderEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SeparationReminderRuns'))
BEGIN
    CREATE TABLE [dbo].[SeparationReminderRuns] (
        [Id]                uniqueidentifier NOT NULL,
        [StartedAt]         datetime2        NOT NULL,
        [CompletedAt]       datetime2        NULL,
        [Trigger]           nvarchar(30)     NOT NULL CONSTRAINT [DF_SeparationReminderRuns_Trigger] DEFAULT ('Scheduled'),
        [TriggeredByUserId] uniqueidentifier NULL,
        [RemindersQueued]   int              NOT NULL CONSTRAINT [DF_SeparationReminderRuns_RemindersQueued] DEFAULT (0),
        [CreatedAt]         datetime2        NOT NULL,
        [UpdatedAt]         datetime2        NULL,
        [CreatedBy]         nvarchar(max)    NULL,
        [UpdatedBy]         nvarchar(max)    NULL,
        [CreatedById]       uniqueidentifier NULL,
        [LastModifiedById]  uniqueidentifier NULL,
        [IsDeleted]         bit              NOT NULL CONSTRAINT [DF_SeparationReminderRuns_IsDeleted] DEFAULT (0),
        [DeletedAt]         datetime2        NULL,
        [DeletedBy]         nvarchar(max)    NULL,
        [TenantId]          uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SeparationReminderRuns] PRIMARY KEY ([Id])
    );
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SeparationReminderRuns_Tenants_TenantId')
    ALTER TABLE [dbo].[SeparationReminderRuns] ADD CONSTRAINT [FK_SeparationReminderRuns_Tenants_TenantId]
        FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationReminderRun_StartedAt' AND object_id = OBJECT_ID('dbo.SeparationReminderRuns'))
    CREATE INDEX [IX_SeparationReminderRun_StartedAt] ON [dbo].[SeparationReminderRuns] ([StartedAt]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationReminderRuns_TenantId' AND object_id = OBJECT_ID('dbo.SeparationReminderRuns'))
    CREATE INDEX [IX_SeparationReminderRuns_TenantId] ON [dbo].[SeparationReminderRuns] ([TenantId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SeparationReminderDispatchLogs'))
BEGIN
    CREATE TABLE [dbo].[SeparationReminderDispatchLogs] (
        [Id]                 uniqueidentifier NOT NULL,
        [RunId]              uniqueidentifier NOT NULL,
        [Kind]               nvarchar(60)     NOT NULL,
        [EmployeeId]         uniqueidentifier NOT NULL,
        [SeparationId]       uniqueidentifier NULL,
        [Reference]          nvarchar(60)     NULL,
        [DueDate]            date             NULL,
        [DaysRemaining]      int              NOT NULL CONSTRAINT [DF_SeparationReminderDispatchLogs_DaysRemaining] DEFAULT (0),
        [EscalationTier]     int              NOT NULL CONSTRAINT [DF_SeparationReminderDispatchLogs_EscalationTier] DEFAULT (0),
        [RoutedToEmployeeId] uniqueidentifier NULL,
        [DedupeKey]          nvarchar(200)    NOT NULL,
        [CreatedAt]          datetime2        NOT NULL,
        [UpdatedAt]          datetime2        NULL,
        [CreatedBy]          nvarchar(max)    NULL,
        [UpdatedBy]          nvarchar(max)    NULL,
        [CreatedById]        uniqueidentifier NULL,
        [LastModifiedById]   uniqueidentifier NULL,
        [IsDeleted]          bit              NOT NULL CONSTRAINT [DF_SeparationReminderDispatchLogs_IsDeleted] DEFAULT (0),
        [DeletedAt]          datetime2        NULL,
        [DeletedBy]          nvarchar(max)    NULL,
        [TenantId]           uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SeparationReminderDispatchLogs] PRIMARY KEY ([Id])
    );
END");

            // ⚠ No foreign key on EmployeeId, SeparationId or RoutedToEmployeeId — a dispatch log is
            // a record of what was sent, and it must survive the thing it was about being tidied
            // away. Same reasoning as the settlement's provenance columns.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SeparationReminderDispatchLogs_SeparationReminderRuns_RunId')
    ALTER TABLE [dbo].[SeparationReminderDispatchLogs] ADD CONSTRAINT [FK_SeparationReminderDispatchLogs_SeparationReminderRuns_RunId]
        FOREIGN KEY ([RunId]) REFERENCES [dbo].[SeparationReminderRuns] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SeparationReminderDispatchLogs_Tenants_TenantId')
    ALTER TABLE [dbo].[SeparationReminderDispatchLogs] ADD CONSTRAINT [FK_SeparationReminderDispatchLogs_Tenants_TenantId]
        FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationReminderDispatch_RunId' AND object_id = OBJECT_ID('dbo.SeparationReminderDispatchLogs'))
    CREATE INDEX [IX_SeparationReminderDispatch_RunId] ON [dbo].[SeparationReminderDispatchLogs] ([RunId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationReminderDispatch_EmployeeId' AND object_id = OBJECT_ID('dbo.SeparationReminderDispatchLogs'))
    CREATE INDEX [IX_SeparationReminderDispatch_EmployeeId] ON [dbo].[SeparationReminderDispatchLogs] ([EmployeeId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationReminderDispatch_Kind' AND object_id = OBJECT_ID('dbo.SeparationReminderDispatchLogs'))
    CREATE INDEX [IX_SeparationReminderDispatch_Kind] ON [dbo].[SeparationReminderDispatchLogs] ([Kind]);");

            // The one the sweep leans on: looked up once per candidate, every pass.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationReminderDispatch_Tenant_DedupeKey' AND object_id = OBJECT_ID('dbo.SeparationReminderDispatchLogs'))
    CREATE INDEX [IX_SeparationReminderDispatch_Tenant_DedupeKey] ON [dbo].[SeparationReminderDispatchLogs] ([TenantId], [DedupeKey]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationReminderDispatchLogs_TenantId' AND object_id = OBJECT_ID('dbo.SeparationReminderDispatchLogs'))
    CREATE INDEX [IX_SeparationReminderDispatchLogs_TenantId] ON [dbo].[SeparationReminderDispatchLogs] ([TenantId]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SeparationReminderDispatchLogs'))
    DROP TABLE [dbo].[SeparationReminderDispatchLogs];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SeparationReminderRuns'))
    DROP TABLE [dbo].[SeparationReminderRuns];");
        }
    }
}
