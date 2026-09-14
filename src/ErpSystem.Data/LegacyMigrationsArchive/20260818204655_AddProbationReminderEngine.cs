using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// HR area 15b slice 7 — the probation reminder engine: ProbationReminderRuns (one row per
    /// sweep) and ProbationReminderDispatchLogs (one row per reminder actually sent).
    ///
    /// The unique (TenantId, DedupeKey) index is the engine's send-once guarantee, not an
    /// optimisation: a sweep claims its keys in the same SaveChanges that records the run, before it
    /// publishes anything, so the daily host and the run-now endpoint cannot double-send even if
    /// they overlap. A key encodes the item, the reminder kind, the DUE date and the escalation
    /// tier — so moving a probation's end date re-arms the ladder, which is the behaviour we want,
    /// and previewing a future date claims nothing, which is why the preview endpoint is safe.
    ///
    /// The scaffolded CreateTable/CreateIndex bodies are replaced with guarded SQL (repo
    /// convention): local dev DBs are built from the EF model by rebuild-db, so a database can
    /// already carry these objects without this migration being stamped — every operation checks
    /// before it acts. The generated Designer and the regenerated snapshot are kept as scaffolded.
    /// </summary>
    public partial class AddProbationReminderEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[ProbationReminderRuns]', N'U') IS NULL
BEGIN
    CREATE TABLE [ProbationReminderRuns] (
        [Id] uniqueidentifier NOT NULL,
        [StartedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        [Trigger] nvarchar(20) NOT NULL,
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
        CONSTRAINT [PK_ProbationReminderRuns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProbationReminderRuns_Tenants_TenantId] FOREIGN KEY ([TenantId])
            REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[ProbationReminderDispatchLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [ProbationReminderDispatchLogs] (
        [Id] uniqueidentifier NOT NULL,
        [RunId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(60) NOT NULL,
        [ItemType] nvarchar(100) NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [ProbationPeriodId] uniqueidentifier NOT NULL,
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
        CONSTRAINT [PK_ProbationReminderDispatchLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProbationReminderDispatchLogs_ProbationReminderRuns_RunId] FOREIGN KEY ([RunId])
            REFERENCES [ProbationReminderRuns] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProbationReminderDispatchLogs_Tenants_TenantId] FOREIGN KEY ([TenantId])
            REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[ProbationReminderDispatchLogs]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_ProbationReminderDispatchLogs_ProbationPeriodId'
                     AND object_id = OBJECT_ID(N'[ProbationReminderDispatchLogs]'))
BEGIN
    CREATE INDEX [IX_ProbationReminderDispatchLogs_ProbationPeriodId]
        ON [ProbationReminderDispatchLogs] ([ProbationPeriodId]);
END;
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[ProbationReminderDispatchLogs]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_ProbationReminderDispatchLogs_RunId'
                     AND object_id = OBJECT_ID(N'[ProbationReminderDispatchLogs]'))
BEGIN
    CREATE INDEX [IX_ProbationReminderDispatchLogs_RunId]
        ON [ProbationReminderDispatchLogs] ([RunId]);
END;
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[ProbationReminderDispatchLogs]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_ProbationReminderDispatchLogs_TenantId_CreatedAt'
                     AND object_id = OBJECT_ID(N'[ProbationReminderDispatchLogs]'))
BEGIN
    CREATE INDEX [IX_ProbationReminderDispatchLogs_TenantId_CreatedAt]
        ON [ProbationReminderDispatchLogs] ([TenantId], [CreatedAt]);
END;
");

            // The send-once guarantee. Created unguarded-by-name only, never IF NOT EXISTS on the
            // data: if a database somehow already holds duplicate keys this must fail loudly rather
            // than silently skip, because a missing unique index here means silent double-sending.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[ProbationReminderDispatchLogs]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_ProbationReminderDispatchLogs_TenantId_DedupeKey'
                     AND object_id = OBJECT_ID(N'[ProbationReminderDispatchLogs]'))
BEGIN
    CREATE UNIQUE INDEX [IX_ProbationReminderDispatchLogs_TenantId_DedupeKey]
        ON [ProbationReminderDispatchLogs] ([TenantId], [DedupeKey]);
END;
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[ProbationReminderRuns]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_ProbationReminderRuns_TenantId_StartedAt'
                     AND object_id = OBJECT_ID(N'[ProbationReminderRuns]'))
BEGIN
    CREATE INDEX [IX_ProbationReminderRuns_TenantId_StartedAt]
        ON [ProbationReminderRuns] ([TenantId], [StartedAt]);
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[ProbationReminderDispatchLogs]', N'U') IS NOT NULL
    DROP TABLE [ProbationReminderDispatchLogs];
");
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[ProbationReminderRuns]', N'U') IS NOT NULL
    DROP TABLE [ProbationReminderRuns];
");
        }
    }
}
