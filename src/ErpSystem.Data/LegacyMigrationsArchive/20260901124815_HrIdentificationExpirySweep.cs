using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Lane 3b — the identification-expiry sweep: a run header plus one dispatch row per reminder.
    /// </summary>
    /// <remarks>
    /// <para>Sixth area to carry its own run/dispatch pair, following separation, probation,
    /// discipline, staff movement and assets. A shared reminder store would be better; retrofitting
    /// five working sweeps is a different job from adding a sixth to the same shape.</para>
    ///
    /// <para><b>The index that matters is <c>(TenantId, DedupeKey)</c>.</b> Every sweep looks up
    /// every candidate against it, so it decides whether a nightly pass over a whole workforce is
    /// cheap or not.</para>
    ///
    /// <para>Written as guarded SQL rather than the scaffolded builder calls, per the house
    /// convention: this must be safe to apply to a database already rebuilt from the EF model.</para>
    /// </remarks>
    public partial class HrIdentificationExpirySweep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[IdentificationExpiryReminderRuns]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[IdentificationExpiryReminderRuns](
        [Id] uniqueidentifier NOT NULL,
        [StartedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        [Trigger] nvarchar(30) NOT NULL,
        [TriggeredByUserId] uniqueidentifier NULL,
        [RemindersQueued] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_IdentificationExpiryReminderRuns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_IdentificationExpiryReminderRuns_Tenants_TenantId] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IdentificationExpiryRun_StartedAt' AND object_id = OBJECT_ID(N'[dbo].[IdentificationExpiryReminderRuns]'))
    CREATE INDEX [IX_IdentificationExpiryRun_StartedAt] ON [dbo].[IdentificationExpiryReminderRuns]([StartedAt]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IdentificationExpiryRun_Tenant_StartedAt' AND object_id = OBJECT_ID(N'[dbo].[IdentificationExpiryReminderRuns]'))
    CREATE INDEX [IX_IdentificationExpiryRun_Tenant_StartedAt] ON [dbo].[IdentificationExpiryReminderRuns]([TenantId], [StartedAt]);
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[IdentificationExpiryDispatchLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[IdentificationExpiryDispatchLogs](
        [Id] uniqueidentifier NOT NULL,
        [RunId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(60) NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [EmployeeIdentificationCardId] uniqueidentifier NOT NULL,
        [IdentificationTypeId] uniqueidentifier NOT NULL,
        [Reference] nvarchar(200) NULL,
        [DueDate] date NULL,
        [DaysRemaining] int NOT NULL,
        [EscalationTier] int NOT NULL,
        [RoutedToEmployeeId] uniqueidentifier NULL,
        [DedupeKey] nvarchar(200) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_IdentificationExpiryDispatchLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_IdentificationExpiryDispatchLogs_IdentificationExpiryReminderRuns_RunId]
            FOREIGN KEY ([RunId]) REFERENCES [dbo].[IdentificationExpiryReminderRuns]([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_IdentificationExpiryDispatchLogs_Tenants_TenantId] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IdentificationExpiryDispatch_RunId' AND object_id = OBJECT_ID(N'[dbo].[IdentificationExpiryDispatchLogs]'))
    CREATE INDEX [IX_IdentificationExpiryDispatch_RunId] ON [dbo].[IdentificationExpiryDispatchLogs]([RunId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IdentificationExpiryDispatch_EmployeeId' AND object_id = OBJECT_ID(N'[dbo].[IdentificationExpiryDispatchLogs]'))
    CREATE INDEX [IX_IdentificationExpiryDispatch_EmployeeId] ON [dbo].[IdentificationExpiryDispatchLogs]([EmployeeId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IdentificationExpiryDispatch_Kind' AND object_id = OBJECT_ID(N'[dbo].[IdentificationExpiryDispatchLogs]'))
    CREATE INDEX [IX_IdentificationExpiryDispatch_Kind] ON [dbo].[IdentificationExpiryDispatchLogs]([Kind]);

-- Looked up on every sweep for every candidate. This is the index that decides whether a nightly
-- pass over a whole workforce is cheap.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IdentificationExpiryDispatch_Tenant_DedupeKey' AND object_id = OBJECT_ID(N'[dbo].[IdentificationExpiryDispatchLogs]'))
    CREATE INDEX [IX_IdentificationExpiryDispatch_Tenant_DedupeKey] ON [dbo].[IdentificationExpiryDispatchLogs]([TenantId], [DedupeKey]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Children before parents: the dispatch log's FK to the run is Restrict, so dropping
            // the run table first would fail.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[IdentificationExpiryDispatchLogs]', N'U') IS NOT NULL DROP TABLE [dbo].[IdentificationExpiryDispatchLogs];
IF OBJECT_ID(N'[dbo].[IdentificationExpiryReminderRuns]', N'U') IS NOT NULL DROP TABLE [dbo].[IdentificationExpiryReminderRuns];
");
        }
    }
}
