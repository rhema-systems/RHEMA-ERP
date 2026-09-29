using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 4, lane K-a — the orientation & onboarding reminder engine, the first HR sweep that
    /// delivers (an in-app notification and an email) rather than only logging.
    /// </summary>
    /// <remarks>
    /// <para><b>Three things.</b> Four reminder windows on <c>CompanyHrPolicySettings</c>
    /// (<c>OnboardingTaskDueLeadDays</c> 3, <c>OrientationDueLeadDays</c> 7,
    /// <c>OrientationCertificateExpiryLeadDays</c> 30, <c>OrientationChaseAfterDays</c> 3); the run
    /// table; and the dispatch log, whose unique (TenantId, DedupeKey) index is the send-once
    /// guarantee.</para>
    ///
    /// <para><b>⚠ The scaffold's two traps, both removed.</b> It added the four columns with
    /// <c>defaultValue: 0</c> — every tenant's windows closed, the sweep reminding nobody — and then
    /// repaired only the one seeded row by id with an <c>UpdateData</c>, which the fast EF build cannot
    /// even run (it resolves column types from target models the build strips). Here each column is
    /// added <c>NOT NULL</c> with its real <c>DEFAULT</c>, which fills every existing row, whichever
    /// tenant it belongs to. No data statement is needed.</para>
    ///
    /// <para><b>⚠ Guarded SQL throughout</b>, as on every HR migration: <c>rebuild-db</c> and the UAT
    /// builder create the schema from the EF model, so a rebuilt database already has all of this and
    /// a bare <c>AddColumn</c>/<c>CreateTable</c> would stop the chain for everyone. Each step is its
    /// own batch, so a table exists before the indexes that name it.</para>
    /// </remarks>
    public partial class AddOnboardingOrientationReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1 — the reminder windows, with their real defaults.
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.CompanyHrPolicySettings', 'OnboardingTaskDueLeadDays') IS NULL
    ALTER TABLE [dbo].[CompanyHrPolicySettings] ADD [OnboardingTaskDueLeadDays] int NOT NULL
        CONSTRAINT [DF_CompanyHrPolicySettings_OnboardingTaskDueLeadDays] DEFAULT (3);
IF COL_LENGTH('dbo.CompanyHrPolicySettings', 'OrientationDueLeadDays') IS NULL
    ALTER TABLE [dbo].[CompanyHrPolicySettings] ADD [OrientationDueLeadDays] int NOT NULL
        CONSTRAINT [DF_CompanyHrPolicySettings_OrientationDueLeadDays] DEFAULT (7);
IF COL_LENGTH('dbo.CompanyHrPolicySettings', 'OrientationCertificateExpiryLeadDays') IS NULL
    ALTER TABLE [dbo].[CompanyHrPolicySettings] ADD [OrientationCertificateExpiryLeadDays] int NOT NULL
        CONSTRAINT [DF_CompanyHrPolicySettings_OrientationCertificateExpiryLeadDays] DEFAULT (30);
IF COL_LENGTH('dbo.CompanyHrPolicySettings', 'OrientationChaseAfterDays') IS NULL
    ALTER TABLE [dbo].[CompanyHrPolicySettings] ADD [OrientationChaseAfterDays] int NOT NULL
        CONSTRAINT [DF_CompanyHrPolicySettings_OrientationChaseAfterDays] DEFAULT (3);");

            // 2 — the run table.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.OnboardingOrientationReminderRuns', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OnboardingOrientationReminderRuns] (
        [Id] uniqueidentifier NOT NULL,
        [StartedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        [Trigger] nvarchar(20) NOT NULL,
        [TriggeredByUserId] uniqueidentifier NULL,
        [RemindersQueued] int NOT NULL,
        [NotificationsDelivered] int NOT NULL,
        [EmailsSent] int NOT NULL,
        [EmailsNotSent] int NOT NULL,
        [Unrouted] int NOT NULL,
        [MailServerConfigured] bit NOT NULL,
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
        CONSTRAINT [PK_OnboardingOrientationReminderRuns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OnboardingOrientationReminderRuns_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id])
    );
END");

            // 3 — the dispatch log: one row per reminder claimed, and what became of it.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.OnboardingOrientationReminderDispatchLogs', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OnboardingOrientationReminderDispatchLogs] (
        [Id] uniqueidentifier NOT NULL,
        [RunId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(60) NOT NULL,
        [ItemType] nvarchar(100) NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [OnboardingPlanId] uniqueidentifier NULL,
        [EmployeeOrientationId] uniqueidentifier NULL,
        [Reference] nvarchar(300) NOT NULL,
        [DueDate] datetime2 NULL,
        [DaysRemaining] int NOT NULL,
        [EscalationTier] int NOT NULL,
        [RoutedToEmployeeId] uniqueidentifier NULL,
        [NotificationId] uniqueidentifier NULL,
        [EmailOutcome] nvarchar(20) NOT NULL,
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
        CONSTRAINT [PK_OnboardingOrientationReminderDispatchLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OnboardingOrientationReminderDispatchLogs_OnboardingOrientationReminderRuns_RunId]
            FOREIGN KEY ([RunId]) REFERENCES [dbo].[OnboardingOrientationReminderRuns] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_OnboardingOrientationReminderDispatchLogs_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id])
    );
END");

            // 4 — the indexes, the send-once one unique.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OnboardingOrientationReminderRuns_TenantId_StartedAt'
               AND object_id = OBJECT_ID('dbo.OnboardingOrientationReminderRuns'))
    CREATE INDEX [IX_OnboardingOrientationReminderRuns_TenantId_StartedAt]
        ON [dbo].[OnboardingOrientationReminderRuns] ([TenantId], [StartedAt]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OnboardingOrientationReminderDispatchLogs_TenantId_DedupeKey'
               AND object_id = OBJECT_ID('dbo.OnboardingOrientationReminderDispatchLogs'))
    CREATE UNIQUE INDEX [IX_OnboardingOrientationReminderDispatchLogs_TenantId_DedupeKey]
        ON [dbo].[OnboardingOrientationReminderDispatchLogs] ([TenantId], [DedupeKey]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OnboardingOrientationReminderDispatchLogs_TenantId_CreatedAt'
               AND object_id = OBJECT_ID('dbo.OnboardingOrientationReminderDispatchLogs'))
    CREATE INDEX [IX_OnboardingOrientationReminderDispatchLogs_TenantId_CreatedAt]
        ON [dbo].[OnboardingOrientationReminderDispatchLogs] ([TenantId], [CreatedAt]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OnboardingOrientationReminderDispatchLogs_RunId'
               AND object_id = OBJECT_ID('dbo.OnboardingOrientationReminderDispatchLogs'))
    CREATE INDEX [IX_OnboardingOrientationReminderDispatchLogs_RunId]
        ON [dbo].[OnboardingOrientationReminderDispatchLogs] ([RunId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OnboardingOrientationReminderDispatchLogs_RoutedToEmployeeId'
               AND object_id = OBJECT_ID('dbo.OnboardingOrientationReminderDispatchLogs'))
    CREATE INDEX [IX_OnboardingOrientationReminderDispatchLogs_RoutedToEmployeeId]
        ON [dbo].[OnboardingOrientationReminderDispatchLogs] ([RoutedToEmployeeId]);");
        }

        /// <inheritdoc />
        /// <remarks>
        /// ⚠ A column's default constraint must go before the column — SQL Server refuses to drop a
        /// column a constraint still names — and on a model-built database its name is
        /// server-generated, so it is looked up by column rather than assumed.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.OnboardingOrientationReminderDispatchLogs', 'U') IS NOT NULL
    DROP TABLE [dbo].[OnboardingOrientationReminderDispatchLogs];
IF OBJECT_ID('dbo.OnboardingOrientationReminderRuns', 'U') IS NOT NULL
    DROP TABLE [dbo].[OnboardingOrientationReminderRuns];");

            migrationBuilder.Sql(@"
DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'ALTER TABLE [dbo].[CompanyHrPolicySettings] DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';'
FROM sys.default_constraints dc
WHERE dc.parent_object_id = OBJECT_ID('dbo.CompanyHrPolicySettings')
  AND COL_NAME(dc.parent_object_id, dc.parent_column_id) IN
      ('OnboardingTaskDueLeadDays', 'OrientationDueLeadDays', 'OrientationCertificateExpiryLeadDays', 'OrientationChaseAfterDays');
IF LEN(@sql) > 0 EXEC sp_executesql @sql;");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.CompanyHrPolicySettings', 'OnboardingTaskDueLeadDays') IS NOT NULL
    ALTER TABLE [dbo].[CompanyHrPolicySettings] DROP COLUMN [OnboardingTaskDueLeadDays];
IF COL_LENGTH('dbo.CompanyHrPolicySettings', 'OrientationDueLeadDays') IS NOT NULL
    ALTER TABLE [dbo].[CompanyHrPolicySettings] DROP COLUMN [OrientationDueLeadDays];
IF COL_LENGTH('dbo.CompanyHrPolicySettings', 'OrientationCertificateExpiryLeadDays') IS NOT NULL
    ALTER TABLE [dbo].[CompanyHrPolicySettings] DROP COLUMN [OrientationCertificateExpiryLeadDays];
IF COL_LENGTH('dbo.CompanyHrPolicySettings', 'OrientationChaseAfterDays') IS NOT NULL
    ALTER TABLE [dbo].[CompanyHrPolicySettings] DROP COLUMN [OrientationChaseAfterDays];");
        }
    }
}
