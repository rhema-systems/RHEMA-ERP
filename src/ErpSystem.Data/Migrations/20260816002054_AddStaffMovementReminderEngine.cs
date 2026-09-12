using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// HR area 8 slice 5 — the staff-movement reminder engine: StaffMovementReminderRuns (one row
    /// per sweep) and StaffMovementReminderDispatchLogs (one row per reminder actually sent).
    ///
    /// The unique (TenantId, DedupeKey) index is the engine's send-once guarantee, not an
    /// optimisation: a sweep claims its keys in the same SaveChanges that records the run, before it
    /// publishes anything, so the daily host and the run-now endpoint cannot double-send even if
    /// they overlap.
    ///
    /// The scaffolded CreateTable/CreateIndex bodies are replaced with guarded SQL (repo
    /// convention): local dev DBs are built from the EF model by rebuild-db, so a database can
    /// already carry these objects without this migration being stamped — every operation checks
    /// before it acts. The generated Designer and the regenerated snapshot are kept as scaffolded.
    /// </summary>
    public partial class AddStaffMovementReminderEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[StaffMovementReminderRuns]', N'U') IS NULL
BEGIN
    CREATE TABLE [StaffMovementReminderRuns] (
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
        CONSTRAINT [PK_StaffMovementReminderRuns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StaffMovementReminderRuns_Tenants_TenantId] FOREIGN KEY ([TenantId])
            REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[StaffMovementReminderDispatchLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [StaffMovementReminderDispatchLogs] (
        [Id] uniqueidentifier NOT NULL,
        [RunId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(60) NOT NULL,
        [ItemType] nvarchar(100) NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [Reference] nvarchar(250) NOT NULL,
        [DueDate] datetime2 NULL,
        [DaysRemaining] int NOT NULL,
        [EscalationTier] int NOT NULL,
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
        CONSTRAINT [PK_StaffMovementReminderDispatchLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StaffMovementReminderDispatchLogs_StaffMovementReminderRuns_RunId] FOREIGN KEY ([RunId])
            REFERENCES [StaffMovementReminderRuns] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_StaffMovementReminderDispatchLogs_Tenants_TenantId] FOREIGN KEY ([TenantId])
            REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[StaffMovementReminderDispatchLogs]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_StaffMovementReminderDispatchLogs_RunId'
                     AND object_id = OBJECT_ID(N'[StaffMovementReminderDispatchLogs]'))
    CREATE INDEX [IX_StaffMovementReminderDispatchLogs_RunId]
        ON [StaffMovementReminderDispatchLogs] ([RunId]);
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[StaffMovementReminderDispatchLogs]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_StaffMovementReminderDispatchLogs_TenantId_CreatedAt'
                     AND object_id = OBJECT_ID(N'[StaffMovementReminderDispatchLogs]'))
    CREATE INDEX [IX_StaffMovementReminderDispatchLogs_TenantId_CreatedAt]
        ON [StaffMovementReminderDispatchLogs] ([TenantId], [CreatedAt]);
");

            // The send-once guarantee. Unique, and claimed before anything is published.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[StaffMovementReminderDispatchLogs]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_StaffMovementReminderDispatchLogs_TenantId_DedupeKey'
                     AND object_id = OBJECT_ID(N'[StaffMovementReminderDispatchLogs]'))
    CREATE UNIQUE INDEX [IX_StaffMovementReminderDispatchLogs_TenantId_DedupeKey]
        ON [StaffMovementReminderDispatchLogs] ([TenantId], [DedupeKey]);
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[StaffMovementReminderRuns]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_StaffMovementReminderRuns_TenantId_StartedAt'
                     AND object_id = OBJECT_ID(N'[StaffMovementReminderRuns]'))
    CREATE INDEX [IX_StaffMovementReminderRuns_TenantId_StartedAt]
        ON [StaffMovementReminderRuns] ([TenantId], [StartedAt]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dispatch logs first — they cascade from the runs, and dropping the parent while the
            // child table exists would fail on the foreign key.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[StaffMovementReminderDispatchLogs]', N'U') IS NOT NULL
    DROP TABLE [StaffMovementReminderDispatchLogs];
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[StaffMovementReminderRuns]', N'U') IS NOT NULL
    DROP TABLE [StaffMovementReminderRuns];
");
        }
    }
}
