using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// HR area 12 slice 5a — the staff-travel reminder engine: StaffTravelReminderRuns (one row per
    /// sweep) and StaffTravelReminderDispatchLogs (one row per reminder actually sent).
    ///
    /// <para>Travel was full of dates that mattered and nothing watched any of them. The endpoints
    /// already existed — expiring documents, expiring visas, overdue advance settlements, upcoming
    /// departures — and returned their rows to nobody. Three other HR areas had a sweep; travel did
    /// not, which is how an expired passport could sit unnoticed until somebody was turned away at
    /// a gate.</para>
    ///
    /// <para>The unique (TenantId, DedupeKey) index is the engine's send-once guarantee, not an
    /// optimisation: a sweep claims its keys in the same SaveChanges that records the run, before it
    /// publishes anything, so the daily host and the run-now endpoint cannot double-send even if
    /// they overlap. A key encodes the item, the reminder kind, the date and the escalation tier —
    /// so moving a date re-arms the ladder, which is the behaviour we want, and previewing a future
    /// date claims nothing, which is why the preview endpoint is safe.</para>
    ///
    /// <para>The scaffolded CreateTable/CreateIndex bodies are replaced with guarded SQL (repo
    /// convention): local dev DBs are built from the EF model by rebuild-db, so a database can
    /// already carry these objects without this migration being stamped — every operation checks
    /// before it acts. The generated Designer and the regenerated snapshot are kept as scaffolded.</para>
    /// </summary>
    public partial class AddStaffTravelReminderEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[StaffTravelReminderRuns]', N'U') IS NULL
BEGIN
    CREATE TABLE [StaffTravelReminderRuns] (
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
        CONSTRAINT [PK_StaffTravelReminderRuns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StaffTravelReminderRuns_Tenants_TenantId] FOREIGN KEY ([TenantId])
            REFERENCES [Tenants] ([Id])
    );
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[StaffTravelReminderDispatchLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [StaffTravelReminderDispatchLogs] (
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
        CONSTRAINT [PK_StaffTravelReminderDispatchLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StaffTravelReminderDispatchLogs_StaffTravelReminderRuns_RunId] FOREIGN KEY ([RunId])
            REFERENCES [StaffTravelReminderRuns] ([Id]),
        CONSTRAINT [FK_StaffTravelReminderDispatchLogs_Tenants_TenantId] FOREIGN KEY ([TenantId])
            REFERENCES [Tenants] ([Id])
    );
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_StaffTravelReminderRuns_TenantId_StartedAt'
                 AND object_id = OBJECT_ID(N'[StaffTravelReminderRuns]'))
    CREATE INDEX [IX_StaffTravelReminderRuns_TenantId_StartedAt]
        ON [StaffTravelReminderRuns] ([TenantId], [StartedAt]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_StaffTravelReminderDispatchLogs_RunId'
                 AND object_id = OBJECT_ID(N'[StaffTravelReminderDispatchLogs]'))
    CREATE INDEX [IX_StaffTravelReminderDispatchLogs_RunId]
        ON [StaffTravelReminderDispatchLogs] ([RunId]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_StaffTravelReminderDispatchLogs_TenantId_CreatedAt'
                 AND object_id = OBJECT_ID(N'[StaffTravelReminderDispatchLogs]'))
    CREATE INDEX [IX_StaffTravelReminderDispatchLogs_TenantId_CreatedAt]
        ON [StaffTravelReminderDispatchLogs] ([TenantId], [CreatedAt]);
");

            // The send-once guarantee. Everything else here is an access path; this one is a rule.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_StaffTravelReminderDispatchLogs_TenantId_DedupeKey'
                 AND object_id = OBJECT_ID(N'[StaffTravelReminderDispatchLogs]'))
    CREATE UNIQUE INDEX [IX_StaffTravelReminderDispatchLogs_TenantId_DedupeKey]
        ON [StaffTravelReminderDispatchLogs] ([TenantId], [DedupeKey]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[StaffTravelReminderDispatchLogs]', N'U') IS NOT NULL
    DROP TABLE [StaffTravelReminderDispatchLogs];
");
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[StaffTravelReminderRuns]', N'U') IS NOT NULL
    DROP TABLE [StaffTravelReminderRuns];
");
        }
    }
}
