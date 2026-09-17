using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Leave closure plan, wave E slice E2 — the leave reminder engine: <c>LeaveReminderRuns</c>
    /// (one row per sweep) and <c>LeaveReminderDispatchLogs</c> (one row per reminder actually sent).
    /// </summary>
    /// <remarks>
    /// <para>HR had eleven of these engines — assets, certifications, discipline, ID expiry,
    /// probation, separation, SHE, movements, travel, teams, attendance — and leave, the module
    /// carrying more dates that matter than any of them, had none. Nothing was raised when leave was
    /// about to start, when approved leave ran past its end date and was never closed, when
    /// mandatory leave went untaken, when carry-over was about to lapse, or when a request sat
    /// undecided (closure plan R-6 / R-10 / L-23).</para>
    ///
    /// <para><b>The unique (TenantId, DedupeKey) index is the engine's send-once guarantee, not an
    /// optimisation.</b> A sweep claims its keys in the same SaveChanges that records the run,
    /// before it publishes anything, so the daily host and the run-now endpoint cannot double-send
    /// even if they overlap. A key encodes the record, the reminder kind, the date and the
    /// escalation tier — so moving a date re-arms the ladder, which is the behaviour we want, and
    /// previewing a future date claims nothing, which is why the preview endpoint is safe.</para>
    ///
    /// <para><b>⚠ The scaffolded CreateTable/CreateIndex bodies were replaced with guarded SQL</b>,
    /// the repo convention since <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and
    /// the UAT builder create the schema from the EF model, so a database can already carry these
    /// objects without this migration being stamped. Every operation checks before it acts. The
    /// generated Designer and the regenerated snapshot are kept as scaffolded.</para>
    ///
    /// <para><b>Purely additive.</b> Two new tables, nothing altered, nothing back-filled. The FK to
    /// <c>LeaveReminderRuns</c> cascades so deleting a run takes its own log rows with it; the two
    /// tenant FKs restrict, like every other tenant-scoped table.</para>
    /// </remarks>
    public partial class AddLeaveReminderEngine : Migration
    {
        private const string Runs = "LeaveReminderRuns";
        private const string Logs = "LeaveReminderDispatchLogs";

        private static string CreateIndex(string table, string index, string columns, bool unique = false) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE {(unique ? "UNIQUE " : string.Empty)}INDEX [{index}] ON [dbo].[{table}] ({columns});";

        private static string DropIndex(string table, string index) => $@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    DROP INDEX [{index}] ON [dbo].[{table}];";

        private static string DropTable(string table) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
    DROP TABLE [dbo].[{table}];";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Runs}', 'U') IS NULL
CREATE TABLE [dbo].[{Runs}] (
    [Id]                 uniqueidentifier NOT NULL,
    [StartedAt]          datetime2        NOT NULL,
    [CompletedAt]        datetime2        NULL,
    [Trigger]            nvarchar(20)     NOT NULL,
    [TriggeredByUserId]  uniqueidentifier NULL,
    [RemindersQueued]    int              NOT NULL,
    [CreatedAt]          datetime2        NOT NULL,
    [UpdatedAt]          datetime2        NULL,
    [CreatedBy]          nvarchar(max)    NULL,
    [UpdatedBy]          nvarchar(max)    NULL,
    [CreatedById]        uniqueidentifier NULL,
    [LastModifiedById]   uniqueidentifier NULL,
    [IsDeleted]          bit              NOT NULL,
    [DeletedAt]          datetime2        NULL,
    [DeletedBy]          nvarchar(max)    NULL,
    [TenantId]           uniqueidentifier NOT NULL,
    CONSTRAINT [PK_{Runs}] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_{Runs}_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Logs}', 'U') IS NULL
CREATE TABLE [dbo].[{Logs}] (
    [Id]                 uniqueidentifier NOT NULL,
    [RunId]              uniqueidentifier NOT NULL,
    [Kind]               nvarchar(60)     NOT NULL,
    [ItemType]           nvarchar(100)    NOT NULL,
    [EntityId]           uniqueidentifier NOT NULL,
    [EmployeeId]         uniqueidentifier NULL,
    [Reference]          nvarchar(250)    NOT NULL,
    [DueDate]            datetime2        NULL,
    [DaysRemaining]      int              NOT NULL,
    [EscalationTier]     int              NOT NULL,
    [DedupeKey]          nvarchar(300)    NOT NULL,
    [CreatedAt]          datetime2        NOT NULL,
    [UpdatedAt]          datetime2        NULL,
    [CreatedBy]          nvarchar(max)    NULL,
    [UpdatedBy]          nvarchar(max)    NULL,
    [CreatedById]        uniqueidentifier NULL,
    [LastModifiedById]   uniqueidentifier NULL,
    [IsDeleted]          bit              NOT NULL,
    [DeletedAt]          datetime2        NULL,
    [DeletedBy]          nvarchar(max)    NULL,
    [TenantId]           uniqueidentifier NOT NULL,
    CONSTRAINT [PK_{Logs}] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_{Logs}_{Runs}_RunId] FOREIGN KEY ([RunId])
        REFERENCES [dbo].[{Runs}] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_{Logs}_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            // Each index in its own batch: SQL Server compiles a batch before running it, so
            // creating a table and indexing it in one batch fails with "Invalid object name".
            migrationBuilder.Sql(CreateIndex(Logs, $"IX_{Logs}_RunId", "[RunId]"));
            migrationBuilder.Sql(CreateIndex(Logs, $"IX_{Logs}_TenantId_CreatedAt", "[TenantId], [CreatedAt]"));

            // ⚠ The send-once guarantee. If this index is ever dropped the engine silently starts
            // re-sending every reminder on every sweep, for ever.
            migrationBuilder.Sql(CreateIndex(Logs, $"IX_{Logs}_TenantId_DedupeKey", "[TenantId], [DedupeKey]", unique: true));

            migrationBuilder.Sql(CreateIndex(Logs, $"IX_{Logs}_TenantId_EmployeeId", "[TenantId], [EmployeeId]"));
            migrationBuilder.Sql(CreateIndex(Runs, $"IX_{Runs}_TenantId", "[TenantId]"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropIndex(Logs, $"IX_{Logs}_RunId"));
            migrationBuilder.Sql(DropIndex(Logs, $"IX_{Logs}_TenantId_CreatedAt"));
            migrationBuilder.Sql(DropIndex(Logs, $"IX_{Logs}_TenantId_DedupeKey"));
            migrationBuilder.Sql(DropIndex(Logs, $"IX_{Logs}_TenantId_EmployeeId"));
            migrationBuilder.Sql(DropIndex(Runs, $"IX_{Runs}_TenantId"));

            // Logs first: the FK to Runs cascades, but the table still has to go before its parent.
            migrationBuilder.Sql(DropTable(Logs));
            migrationBuilder.Sql(DropTable(Runs));
        }
    }
}
