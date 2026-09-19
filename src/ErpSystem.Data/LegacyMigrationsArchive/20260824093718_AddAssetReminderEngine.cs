using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 16 slice 9. The asset reminder engine — <b>AST-1</b>, "if an asset requires maintenance,
    /// there must be a way to monitor it". Two tables, and one column linking an HR asset to its
    /// counterpart in the Maintenance module's register.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The unique index is the feature, not a tuning decision.</b>
    /// <c>IX_AssetReminderDispatchLogs_TenantId_DedupeKey</c> is what makes a reminder send once: a
    /// sweep claims its keys by inserting the log rows in the same <c>SaveChanges</c> that records
    /// the run, and only publishes after that commits. The daily background host and the run-now
    /// endpoint therefore cannot double-send even if they overlap. Drop the uniqueness and the
    /// engine still appears to work — it simply reminds everybody about everything, every day, until
    /// they stop reading it.
    /// </para>
    /// <para>
    /// <b>A key encodes the DUE DATE and the tier</b>, not just the asset, so rescheduling an asset
    /// re-arms the ladder. That is deliberate: a maintenance date that has been corrected is
    /// genuinely a new thing to chase. It is also why the preview endpoint is safe — previewing a
    /// future date claims no key.
    /// </para>
    /// <para>
    /// <b><c>CompanyAssets.MaintenanceAssetId</c> is a handle, not an abdication.</b> Everything in
    /// the Maintenance module targets an asset by <c>MaintenanceAsset.Id</c> — an admission, a job
    /// card, a work order — so without this column HR has no way to name the thing it wants worked
    /// on. Slice 9b pushes on it. A linked asset is <i>still</i> scheduled and chased by HR: an
    /// earlier draft stood HR down on link, which would have meant that sending a laptop out for a
    /// one-off repair silently switched off its servicing reminders. The FK is <c>NO ACTION</c> and
    /// the index is <b>not</b> unique, for the reason slice 2b recorded about the Finance link —
    /// every delete in this area is a soft delete, so a unique index would hold a deleted row's slot
    /// for ever.
    /// </para>
    /// <para>
    /// ⚠ <b>Every create is guarded and every drop is conditional</b>, so this is safe to re-run
    /// against a database at either state — including one built from the EF model rather than from
    /// the chain, which is how this repo's <c>rebuild-db</c> works. The scaffold was correct as
    /// generated: two tables, one column, one FK, five indexes, nothing inferred and nothing
    /// renamed. (Worth confirming rather than assuming — EF inferred a wrong <c>RENAME</c> in this
    /// area at slice 3.)
    /// </para>
    /// </remarks>
    public partial class AddAssetReminderEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── the run header ─────────────────────────────────────────────────────────────
            //
            // Exists so "the sweep ran and found nothing" is distinguishable from "the sweep never
            // ran". For a register of physical property that difference matters more than it does
            // elsewhere: a quiet maintenance list is either an estate in good order or an engine
            // that stopped, and without a run history nobody can tell which.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.AssetReminderRuns', 'U') IS NULL
CREATE TABLE [dbo].[AssetReminderRuns] (
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
    CONSTRAINT [PK_AssetReminderRuns] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssetReminderRuns_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            // ── one row per reminder actually dispatched ───────────────────────────────────
            //
            // ⚠ Nothing here carries a serial number, a location, a value or a holder's name. A
            // reminder travels further than the record it is about — into notification lists and,
            // one day, email — and an asset register is a shopping list for anybody who can read
            // one. It names the asset and a date and makes the reader open the register.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.AssetReminderDispatchLogs', 'U') IS NULL
CREATE TABLE [dbo].[AssetReminderDispatchLogs] (
    [Id]                 uniqueidentifier NOT NULL,
    [RunId]              uniqueidentifier NOT NULL,
    [Kind]               nvarchar(60)     NOT NULL,
    [ItemType]           nvarchar(100)    NOT NULL,
    [EntityId]           uniqueidentifier NOT NULL,
    [AssetId]            uniqueidentifier NOT NULL,
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
    CONSTRAINT [PK_AssetReminderDispatchLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssetReminderDispatchLogs_AssetReminderRuns_RunId] FOREIGN KEY ([RunId])
        REFERENCES [dbo].[AssetReminderRuns] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AssetReminderDispatchLogs_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            // ── indexes ────────────────────────────────────────────────────────────────────
            //
            // ⚠ The unique one is the send-once guarantee. See the remarks on this class.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssetReminderDispatchLogs_TenantId_DedupeKey' AND object_id = OBJECT_ID('dbo.AssetReminderDispatchLogs'))
    CREATE UNIQUE INDEX [IX_AssetReminderDispatchLogs_TenantId_DedupeKey] ON [dbo].[AssetReminderDispatchLogs] ([TenantId], [DedupeKey]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssetReminderDispatchLogs_TenantId_CreatedAt' AND object_id = OBJECT_ID('dbo.AssetReminderDispatchLogs'))
    CREATE INDEX [IX_AssetReminderDispatchLogs_TenantId_CreatedAt] ON [dbo].[AssetReminderDispatchLogs] ([TenantId], [CreatedAt]);");

            // Grouping the log by asset across kinds — the question a screen asks first, once
            // slice 11 adds insurance expiry and overdue returns to the same engine.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssetReminderDispatchLogs_TenantId_AssetId' AND object_id = OBJECT_ID('dbo.AssetReminderDispatchLogs'))
    CREATE INDEX [IX_AssetReminderDispatchLogs_TenantId_AssetId] ON [dbo].[AssetReminderDispatchLogs] ([TenantId], [AssetId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssetReminderDispatchLogs_RunId' AND object_id = OBJECT_ID('dbo.AssetReminderDispatchLogs'))
    CREATE INDEX [IX_AssetReminderDispatchLogs_RunId] ON [dbo].[AssetReminderDispatchLogs] ([RunId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssetReminderRuns_TenantId_StartedAt' AND object_id = OBJECT_ID('dbo.AssetReminderRuns'))
    CREATE INDEX [IX_AssetReminderRuns_TenantId_StartedAt] ON [dbo].[AssetReminderRuns] ([TenantId], [StartedAt]);");

            // ── the handle into the Maintenance module ─────────────────────────────────────
            //
            // Nullable with no default: null is the truthful description of every asset that
            // existed before this column, because none of them had a counterpart over there.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'MaintenanceAssetId' AND object_id = OBJECT_ID('dbo.CompanyAssets'))
    ALTER TABLE [dbo].[CompanyAssets] ADD [MaintenanceAssetId] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CompanyAssets_MaintenanceAssetId' AND object_id = OBJECT_ID('dbo.CompanyAssets'))
    CREATE INDEX [IX_CompanyAssets_MaintenanceAssetId] ON [dbo].[CompanyAssets] ([MaintenanceAssetId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CompanyAssets_MaintenanceAssets_MaintenanceAssetId' AND parent_object_id = OBJECT_ID('dbo.CompanyAssets'))
    ALTER TABLE [dbo].[CompanyAssets] ADD CONSTRAINT [FK_CompanyAssets_MaintenanceAssets_MaintenanceAssetId]
        FOREIGN KEY ([MaintenanceAssetId]) REFERENCES [dbo].[MaintenanceAssets] ([Id]) ON DELETE NO ACTION;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ Dropping the dispatch log discards the record of what has already been sent, so a
            // re-applied engine will re-announce every outstanding maintenance date once — every
            // dedupe key it had claimed is gone. Noisy rather than wrong, but worth knowing before
            // reversing this on a live tenant.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CompanyAssets_MaintenanceAssets_MaintenanceAssetId' AND parent_object_id = OBJECT_ID('dbo.CompanyAssets'))
    ALTER TABLE [dbo].[CompanyAssets] DROP CONSTRAINT [FK_CompanyAssets_MaintenanceAssets_MaintenanceAssetId];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CompanyAssets_MaintenanceAssetId' AND object_id = OBJECT_ID('dbo.CompanyAssets'))
    DROP INDEX [IX_CompanyAssets_MaintenanceAssetId] ON [dbo].[CompanyAssets];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'MaintenanceAssetId' AND object_id = OBJECT_ID('dbo.CompanyAssets'))
    ALTER TABLE [dbo].[CompanyAssets] DROP COLUMN [MaintenanceAssetId];");

            // The dispatch log goes first — its FK to the run table is CASCADE, but the table must
            // still be dropped before the principal it references.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.AssetReminderDispatchLogs', 'U') IS NOT NULL
    DROP TABLE [dbo].[AssetReminderDispatchLogs];");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.AssetReminderRuns', 'U') IS NOT NULL
    DROP TABLE [dbo].[AssetReminderRuns];");
        }
    }
}
