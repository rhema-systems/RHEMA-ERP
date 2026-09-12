using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// HR area 9 slice 8 — the discipline reminder engine: DisciplineReminderRuns (one row per sweep)
    /// and DisciplineReminderDispatchLogs (one row per reminder actually sent).
    ///
    /// The unique (TenantId, DedupeKey) index is the engine's send-once guarantee, not an
    /// optimisation: a sweep claims its keys in the same SaveChanges that records the run, before it
    /// publishes anything, so the daily host and the run-now endpoint cannot double-send even if they
    /// overlap. A key encodes the item, the reminder kind, the DUE date and the rung or escalation
    /// tier — so moving a deadline re-arms the ladder, which is the behaviour we want, and previewing
    /// a future date claims nothing, which is why the preview endpoint is safe.
    ///
    /// The scaffolded CreateTable/CreateIndex bodies are replaced with guarded SQL (repo convention):
    /// local dev DBs are built from the EF model by rebuild-db, so a database can already carry these
    /// objects without this migration being stamped — every operation checks before it acts. The
    /// generated Designer and the regenerated snapshot are kept as scaffolded.
    /// </summary>
    public partial class AddDisciplineReminderEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[DisciplineReminderRuns]', N'U') IS NULL
BEGIN
    CREATE TABLE [DisciplineReminderRuns] (
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
        CONSTRAINT [PK_DisciplineReminderRuns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DisciplineReminderRuns_Tenants_TenantId] FOREIGN KEY ([TenantId])
            REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[DisciplineReminderDispatchLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [DisciplineReminderDispatchLogs] (
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
        CONSTRAINT [PK_DisciplineReminderDispatchLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DisciplineReminderDispatchLogs_DisciplineReminderRuns_RunId] FOREIGN KEY ([RunId])
            REFERENCES [DisciplineReminderRuns] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DisciplineReminderDispatchLogs_Tenants_TenantId] FOREIGN KEY ([TenantId])
            REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[DisciplineReminderDispatchLogs]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_DisciplineReminderDispatchLogs_RunId'
                     AND object_id = OBJECT_ID(N'[DisciplineReminderDispatchLogs]'))
    CREATE INDEX [IX_DisciplineReminderDispatchLogs_RunId]
        ON [DisciplineReminderDispatchLogs] ([RunId]);
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[DisciplineReminderDispatchLogs]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_DisciplineReminderDispatchLogs_TenantId_CreatedAt'
                     AND object_id = OBJECT_ID(N'[DisciplineReminderDispatchLogs]'))
    CREATE INDEX [IX_DisciplineReminderDispatchLogs_TenantId_CreatedAt]
        ON [DisciplineReminderDispatchLogs] ([TenantId], [CreatedAt]);
");

            // The send-once guarantee. Unique, and claimed before anything is published.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[DisciplineReminderDispatchLogs]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_DisciplineReminderDispatchLogs_TenantId_DedupeKey'
                     AND object_id = OBJECT_ID(N'[DisciplineReminderDispatchLogs]'))
    CREATE UNIQUE INDEX [IX_DisciplineReminderDispatchLogs_TenantId_DedupeKey]
        ON [DisciplineReminderDispatchLogs] ([TenantId], [DedupeKey]);
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[DisciplineReminderRuns]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_DisciplineReminderRuns_TenantId_StartedAt'
                     AND object_id = OBJECT_ID(N'[DisciplineReminderRuns]'))
    CREATE INDEX [IX_DisciplineReminderRuns_TenantId_StartedAt]
        ON [DisciplineReminderRuns] ([TenantId], [StartedAt]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dispatch logs first — they cascade from the runs, and dropping the parent while the
            // child table exists would fail on the foreign key.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[DisciplineReminderDispatchLogs]', N'U') IS NOT NULL
    DROP TABLE [DisciplineReminderDispatchLogs];
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[DisciplineReminderRuns]', N'U') IS NOT NULL
    DROP TABLE [DisciplineReminderRuns];
");
        }
    }
}
