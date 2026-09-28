using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 4, lane K-b — the orientation notice made its own email outbox: the notice is written
    /// with the event it reports, and its email is sent afterwards by a dispatcher.
    /// </summary>
    /// <remarks>
    /// <para><b>Five columns on <c>OrientationNotifications</c></b> — the catalogue event the email
    /// renders, its tokens as they stood at the event, what the email did (null: never meant to be
    /// emailed), how many attempts, and when the last was — and two indexes: by tenant and date for
    /// HR's "who was told what", and one FILTERED to the queued rows, the only question the
    /// dispatcher asks every minute.</para>
    ///
    /// <para><b>The old <c>IX_OrientationNotifications_TenantId</c> goes</b>, as the scaffold had it:
    /// the new (TenantId, SentAt) index leads with the same column and serves everything it did.</para>
    ///
    /// <para><b>One data step</b>: the reminder sweep's digests written before this (lane K-a) get the
    /// outcome their dispatch log already recorded, so every notice answers "what did its email do?"
    /// the same way. Keyed on the log, never on row ids, and only where the notice has no event key.</para>
    ///
    /// <para>⚠ Guarded SQL throughout, as on every HR migration: <c>rebuild-db</c> and the UAT builder
    /// create the schema from the EF model, so a rebuilt database already has all of this. Each step is
    /// its own batch, so the columns exist before the statements that name them.</para>
    /// </remarks>
    public partial class AddOrientationNoticeEmailOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1 — the columns. EmailAttempts is NOT NULL with a real default, which fills existing rows.
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.OrientationNotifications', 'EmailEventKey') IS NULL
    ALTER TABLE [dbo].[OrientationNotifications] ADD [EmailEventKey] nvarchar(100) NULL;
IF COL_LENGTH('dbo.OrientationNotifications', 'EmailTokens') IS NULL
    ALTER TABLE [dbo].[OrientationNotifications] ADD [EmailTokens] nvarchar(max) NULL;
IF COL_LENGTH('dbo.OrientationNotifications', 'EmailStatus') IS NULL
    ALTER TABLE [dbo].[OrientationNotifications] ADD [EmailStatus] nvarchar(20) NULL;
IF COL_LENGTH('dbo.OrientationNotifications', 'EmailAttempts') IS NULL
    ALTER TABLE [dbo].[OrientationNotifications] ADD [EmailAttempts] int NOT NULL
        CONSTRAINT [DF_OrientationNotifications_EmailAttempts] DEFAULT (0);
IF COL_LENGTH('dbo.OrientationNotifications', 'EmailLastAttemptAt') IS NULL
    ALTER TABLE [dbo].[OrientationNotifications] ADD [EmailLastAttemptAt] datetime2 NULL;");

            // 2 — the sweep's earlier digests say what their emails did, from their own dispatch log.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.OnboardingOrientationReminderDispatchLogs', 'U') IS NOT NULL
    UPDATE n
    SET n.[EmailEventKey] = N'OrientationReminderDigest',
        n.[EmailStatus] = d.[EmailOutcome],
        n.[EmailAttempts] = CASE WHEN d.[EmailOutcome] IN (N'Sent', N'Failed', N'TimedOut') THEN 1 ELSE 0 END
    FROM [dbo].[OrientationNotifications] n
    CROSS APPLY (
        SELECT TOP (1) l.[EmailOutcome]
        FROM [dbo].[OnboardingOrientationReminderDispatchLogs] l
        WHERE l.[NotificationId] = n.[Id] AND l.[EmailOutcome] <> N'Pending'
        ORDER BY l.[CreatedAt] DESC
    ) d
    WHERE n.[EmailEventKey] IS NULL;");

            // 3 — the indexes: the tenant-only one gives way to (TenantId, SentAt); the queue is filtered.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrientationNotifications_TenantId'
           AND object_id = OBJECT_ID('dbo.OrientationNotifications'))
    DROP INDEX [IX_OrientationNotifications_TenantId] ON [dbo].[OrientationNotifications];
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrientationNotifications_TenantId_SentAt'
               AND object_id = OBJECT_ID('dbo.OrientationNotifications'))
    CREATE INDEX [IX_OrientationNotifications_TenantId_SentAt]
        ON [dbo].[OrientationNotifications] ([TenantId], [SentAt]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrientationNotifications_EmailQueue'
               AND object_id = OBJECT_ID('dbo.OrientationNotifications'))
    CREATE INDEX [IX_OrientationNotifications_EmailQueue]
        ON [dbo].[OrientationNotifications] ([TenantId], [SentAt])
        WHERE [EmailStatus] = N'Queued';");
        }

        /// <inheritdoc />
        /// <remarks>
        /// ⚠ The default constraint goes before its column — SQL Server refuses to drop a column a
        /// constraint still names — and on a model-built database its name is server-generated, so it
        /// is looked up by column rather than assumed.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrientationNotifications_EmailQueue'
           AND object_id = OBJECT_ID('dbo.OrientationNotifications'))
    DROP INDEX [IX_OrientationNotifications_EmailQueue] ON [dbo].[OrientationNotifications];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrientationNotifications_TenantId_SentAt'
           AND object_id = OBJECT_ID('dbo.OrientationNotifications'))
    DROP INDEX [IX_OrientationNotifications_TenantId_SentAt] ON [dbo].[OrientationNotifications];
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrientationNotifications_TenantId'
               AND object_id = OBJECT_ID('dbo.OrientationNotifications'))
    CREATE INDEX [IX_OrientationNotifications_TenantId]
        ON [dbo].[OrientationNotifications] ([TenantId]);");

            migrationBuilder.Sql(@"
DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'ALTER TABLE [dbo].[OrientationNotifications] DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';'
FROM sys.default_constraints dc
WHERE dc.parent_object_id = OBJECT_ID('dbo.OrientationNotifications')
  AND COL_NAME(dc.parent_object_id, dc.parent_column_id) = 'EmailAttempts';
IF LEN(@sql) > 0 EXEC sp_executesql @sql;");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.OrientationNotifications', 'EmailEventKey') IS NOT NULL
    ALTER TABLE [dbo].[OrientationNotifications] DROP COLUMN [EmailEventKey];
IF COL_LENGTH('dbo.OrientationNotifications', 'EmailTokens') IS NOT NULL
    ALTER TABLE [dbo].[OrientationNotifications] DROP COLUMN [EmailTokens];
IF COL_LENGTH('dbo.OrientationNotifications', 'EmailStatus') IS NOT NULL
    ALTER TABLE [dbo].[OrientationNotifications] DROP COLUMN [EmailStatus];
IF COL_LENGTH('dbo.OrientationNotifications', 'EmailAttempts') IS NOT NULL
    ALTER TABLE [dbo].[OrientationNotifications] DROP COLUMN [EmailAttempts];
IF COL_LENGTH('dbo.OrientationNotifications', 'EmailLastAttemptAt') IS NOT NULL
    ALTER TABLE [dbo].[OrientationNotifications] DROP COLUMN [EmailLastAttemptAt];");
        }
    }
}
