using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// HR area 10 slice 13 — the SHE reminder engine's two tables: sweep runs and the
    /// per-reminder dispatch log whose unique (TenantId, DedupeKey) index is the
    /// engine's send-once guarantee.
    ///
    /// The scaffolded CreateTable/CreateIndex bodies are replaced with guarded SQL
    /// (repo convention): local dev DBs are built from the EF model by rebuild-db, so
    /// a DB can already carry these tables without this migration being stamped —
    /// every operation checks before it acts. The generated Designer and the
    /// regenerated snapshot are kept as scaffolded.
    /// </summary>
    public partial class AddSheReminderEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheReminderRuns]', N'U') IS NULL
BEGIN
    CREATE TABLE [SheReminderRuns] (
        [Id] uniqueidentifier NOT NULL,
        [StartedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        [Trigger] nvarchar(20) NOT NULL,
        [TriggeredByUserId] uniqueidentifier NULL,
        [RemindersQueued] int NOT NULL,
        [PermitsExpired] int NOT NULL,
        [RiskAssessmentsExpired] int NOT NULL,
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
        CONSTRAINT [PK_SheReminderRuns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SheReminderRuns_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheReminderDispatchLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [SheReminderDispatchLogs] (
        [Id] uniqueidentifier NOT NULL,
        [RunId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(60) NOT NULL,
        [ItemType] nvarchar(100) NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [Reference] nvarchar(250) NOT NULL,
        [DueDate] datetime2 NULL,
        [DaysRemaining] int NOT NULL,
        [EscalationTier] int NOT NULL,
        [DedupeKey] nvarchar(300) NOT NULL,
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
        CONSTRAINT [PK_SheReminderDispatchLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SheReminderDispatchLogs_SheReminderRuns_RunId] FOREIGN KEY ([RunId]) REFERENCES [SheReminderRuns] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheReminderDispatchLogs_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SheReminderDispatchLogs_RunId' AND [object_id] = OBJECT_ID(N'[SheReminderDispatchLogs]'))
    CREATE INDEX [IX_SheReminderDispatchLogs_RunId] ON [SheReminderDispatchLogs] ([RunId]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SheReminderDispatchLogs_TenantId_CreatedAt' AND [object_id] = OBJECT_ID(N'[SheReminderDispatchLogs]'))
    CREATE INDEX [IX_SheReminderDispatchLogs_TenantId_CreatedAt] ON [SheReminderDispatchLogs] ([TenantId], [CreatedAt]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SheReminderDispatchLogs_TenantId_DedupeKey' AND [object_id] = OBJECT_ID(N'[SheReminderDispatchLogs]'))
    CREATE UNIQUE INDEX [IX_SheReminderDispatchLogs_TenantId_DedupeKey] ON [SheReminderDispatchLogs] ([TenantId], [DedupeKey]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SheReminderRuns_TenantId_StartedAt' AND [object_id] = OBJECT_ID(N'[SheReminderRuns]'))
    CREATE INDEX [IX_SheReminderRuns_TenantId_StartedAt] ON [SheReminderRuns] ([TenantId], [StartedAt]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheReminderDispatchLogs]', N'U') IS NOT NULL
    DROP TABLE [SheReminderDispatchLogs];
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheReminderRuns]', N'U') IS NOT NULL
    DROP TABLE [SheReminderRuns];
");
        }
    }
}
