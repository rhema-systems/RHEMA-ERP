using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260731140309_RepairMissingFinanceSettingsColumns")]
public partial class RepairMissingFinanceSettingsColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'dbo.FinanceSettings', N'U') IS NULL
                THROW 51000, 'Cannot repair FinanceSettings because dbo.FinanceSettings does not exist.', 1;

            IF COL_LENGTH(N'dbo.FinanceSettings', N'MigrationClearingAccountId') IS NULL
                ALTER TABLE [dbo].[FinanceSettings] ADD [MigrationClearingAccountId] uniqueidentifier NULL;

            IF COL_LENGTH(N'dbo.FinanceSettings', N'RealizedFxGainAccountId') IS NULL
                ALTER TABLE [dbo].[FinanceSettings] ADD [RealizedFxGainAccountId] uniqueidentifier NULL;

            IF COL_LENGTH(N'dbo.FinanceSettings', N'RealizedFxLossAccountId') IS NULL
                ALTER TABLE [dbo].[FinanceSettings] ADD [RealizedFxLossAccountId] uniqueidentifier NULL;

            IF COL_LENGTH(N'dbo.FinanceSettings', N'SegmentClearingAccountId') IS NULL
                ALTER TABLE [dbo].[FinanceSettings] ADD [SegmentClearingAccountId] uniqueidentifier NULL;

            IF COL_LENGTH(N'dbo.FinanceSettings', N'SubledgerPostingMode') IS NULL
                ALTER TABLE [dbo].[FinanceSettings] ADD [SubledgerPostingMode] nvarchar(30) NULL;

            UPDATE [dbo].[FinanceSettings]
            SET [SubledgerPostingMode] = N'IFRS'
            WHERE [SubledgerPostingMode] IS NULL
               OR NULLIF(LTRIM(RTRIM([SubledgerPostingMode])), N'') IS NULL;

            ALTER TABLE [dbo].[FinanceSettings]
                ALTER COLUMN [SubledgerPostingMode] nvarchar(30) NOT NULL;

            IF COL_LENGTH(N'dbo.FinanceSettings', N'WriteOffExpenseAccountId') IS NULL
                ALTER TABLE [dbo].[FinanceSettings] ADD [WriteOffExpenseAccountId] uniqueidentifier NULL;

            IF COL_LENGTH(N'dbo.FinanceSettings', N'WriteOffRecoveryAccountId') IS NULL
                ALTER TABLE [dbo].[FinanceSettings] ADD [WriteOffRecoveryAccountId] uniqueidentifier NULL;

            IF COL_LENGTH(N'dbo.FinanceSettings', N'RequireSubledgerJournalApproval') IS NULL
                ALTER TABLE [dbo].[FinanceSettings] ADD [RequireSubledgerJournalApproval] bit NULL;

            UPDATE [dbo].[FinanceSettings]
            SET [RequireSubledgerJournalApproval] = 0
            WHERE [RequireSubledgerJournalApproval] IS NULL;

            ALTER TABLE [dbo].[FinanceSettings]
                ALTER COLUMN [RequireSubledgerJournalApproval] bit NOT NULL;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [object_id] = OBJECT_ID(N'dbo.FinanceSettings')
                  AND [name] = N'IX_FinanceSettings_MigrationClearingAccountId')
                CREATE INDEX [IX_FinanceSettings_MigrationClearingAccountId]
                    ON [dbo].[FinanceSettings] ([MigrationClearingAccountId]);

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [object_id] = OBJECT_ID(N'dbo.FinanceSettings')
                  AND [name] = N'IX_FinanceSettings_RealizedFxGainAccountId')
                CREATE INDEX [IX_FinanceSettings_RealizedFxGainAccountId]
                    ON [dbo].[FinanceSettings] ([RealizedFxGainAccountId]);

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [object_id] = OBJECT_ID(N'dbo.FinanceSettings')
                  AND [name] = N'IX_FinanceSettings_RealizedFxLossAccountId')
                CREATE INDEX [IX_FinanceSettings_RealizedFxLossAccountId]
                    ON [dbo].[FinanceSettings] ([RealizedFxLossAccountId]);

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [object_id] = OBJECT_ID(N'dbo.FinanceSettings')
                  AND [name] = N'IX_FinanceSettings_WriteOffExpenseAccountId')
                CREATE INDEX [IX_FinanceSettings_WriteOffExpenseAccountId]
                    ON [dbo].[FinanceSettings] ([WriteOffExpenseAccountId]);

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE [object_id] = OBJECT_ID(N'dbo.FinanceSettings')
                  AND [name] = N'IX_FinanceSettings_WriteOffRecoveryAccountId')
                CREATE INDEX [IX_FinanceSettings_WriteOffRecoveryAccountId]
                    ON [dbo].[FinanceSettings] ([WriteOffRecoveryAccountId]);

            IF NOT EXISTS (
                SELECT 1
                FROM sys.foreign_keys
                WHERE [parent_object_id] = OBJECT_ID(N'dbo.FinanceSettings')
                  AND [name] = N'FK_FinanceSettings_Accounts_MigrationClearingAccountId')
            BEGIN
                ALTER TABLE [dbo].[FinanceSettings] WITH CHECK
                    ADD CONSTRAINT [FK_FinanceSettings_Accounts_MigrationClearingAccountId]
                    FOREIGN KEY ([MigrationClearingAccountId]) REFERENCES [dbo].[Accounts] ([Id]);
                ALTER TABLE [dbo].[FinanceSettings]
                    CHECK CONSTRAINT [FK_FinanceSettings_Accounts_MigrationClearingAccountId];
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.foreign_keys
                WHERE [parent_object_id] = OBJECT_ID(N'dbo.FinanceSettings')
                  AND [name] = N'FK_FinanceSettings_Accounts_RealizedFxGainAccountId')
            BEGIN
                ALTER TABLE [dbo].[FinanceSettings] WITH CHECK
                    ADD CONSTRAINT [FK_FinanceSettings_Accounts_RealizedFxGainAccountId]
                    FOREIGN KEY ([RealizedFxGainAccountId]) REFERENCES [dbo].[Accounts] ([Id]);
                ALTER TABLE [dbo].[FinanceSettings]
                    CHECK CONSTRAINT [FK_FinanceSettings_Accounts_RealizedFxGainAccountId];
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.foreign_keys
                WHERE [parent_object_id] = OBJECT_ID(N'dbo.FinanceSettings')
                  AND [name] = N'FK_FinanceSettings_Accounts_RealizedFxLossAccountId')
            BEGIN
                ALTER TABLE [dbo].[FinanceSettings] WITH CHECK
                    ADD CONSTRAINT [FK_FinanceSettings_Accounts_RealizedFxLossAccountId]
                    FOREIGN KEY ([RealizedFxLossAccountId]) REFERENCES [dbo].[Accounts] ([Id]);
                ALTER TABLE [dbo].[FinanceSettings]
                    CHECK CONSTRAINT [FK_FinanceSettings_Accounts_RealizedFxLossAccountId];
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.foreign_keys
                WHERE [parent_object_id] = OBJECT_ID(N'dbo.FinanceSettings')
                  AND [name] = N'FK_FinanceSettings_Accounts_WriteOffExpenseAccountId')
            BEGIN
                ALTER TABLE [dbo].[FinanceSettings] WITH CHECK
                    ADD CONSTRAINT [FK_FinanceSettings_Accounts_WriteOffExpenseAccountId]
                    FOREIGN KEY ([WriteOffExpenseAccountId]) REFERENCES [dbo].[Accounts] ([Id]);
                ALTER TABLE [dbo].[FinanceSettings]
                    CHECK CONSTRAINT [FK_FinanceSettings_Accounts_WriteOffExpenseAccountId];
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.foreign_keys
                WHERE [parent_object_id] = OBJECT_ID(N'dbo.FinanceSettings')
                  AND [name] = N'FK_FinanceSettings_Accounts_WriteOffRecoveryAccountId')
            BEGIN
                ALTER TABLE [dbo].[FinanceSettings] WITH CHECK
                    ADD CONSTRAINT [FK_FinanceSettings_Accounts_WriteOffRecoveryAccountId]
                    FOREIGN KEY ([WriteOffRecoveryAccountId]) REFERENCES [dbo].[Accounts] ([Id]);
                ALTER TABLE [dbo].[FinanceSettings]
                    CHECK CONSTRAINT [FK_FinanceSettings_Accounts_WriteOffRecoveryAccountId];
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'dbo.FinanceSettings', N'U') IS NULL
                RETURN;

            IF EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE [parent_object_id] = OBJECT_ID(N'dbo.FinanceSettings')
                  AND [name] = N'FK_FinanceSettings_Accounts_MigrationClearingAccountId')
                ALTER TABLE [dbo].[FinanceSettings]
                    DROP CONSTRAINT [FK_FinanceSettings_Accounts_MigrationClearingAccountId];

            IF EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE [parent_object_id] = OBJECT_ID(N'dbo.FinanceSettings')
                  AND [name] = N'FK_FinanceSettings_Accounts_RealizedFxGainAccountId')
                ALTER TABLE [dbo].[FinanceSettings]
                    DROP CONSTRAINT [FK_FinanceSettings_Accounts_RealizedFxGainAccountId];

            IF EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE [parent_object_id] = OBJECT_ID(N'dbo.FinanceSettings')
                  AND [name] = N'FK_FinanceSettings_Accounts_RealizedFxLossAccountId')
                ALTER TABLE [dbo].[FinanceSettings]
                    DROP CONSTRAINT [FK_FinanceSettings_Accounts_RealizedFxLossAccountId];

            IF EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE [parent_object_id] = OBJECT_ID(N'dbo.FinanceSettings')
                  AND [name] = N'FK_FinanceSettings_Accounts_WriteOffExpenseAccountId')
                ALTER TABLE [dbo].[FinanceSettings]
                    DROP CONSTRAINT [FK_FinanceSettings_Accounts_WriteOffExpenseAccountId];

            IF EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE [parent_object_id] = OBJECT_ID(N'dbo.FinanceSettings')
                  AND [name] = N'FK_FinanceSettings_Accounts_WriteOffRecoveryAccountId')
                ALTER TABLE [dbo].[FinanceSettings]
                    DROP CONSTRAINT [FK_FinanceSettings_Accounts_WriteOffRecoveryAccountId];

            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE [object_id] = OBJECT_ID(N'dbo.FinanceSettings')
                  AND [name] = N'IX_FinanceSettings_MigrationClearingAccountId')
                DROP INDEX [IX_FinanceSettings_MigrationClearingAccountId] ON [dbo].[FinanceSettings];

            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE [object_id] = OBJECT_ID(N'dbo.FinanceSettings')
                  AND [name] = N'IX_FinanceSettings_RealizedFxGainAccountId')
                DROP INDEX [IX_FinanceSettings_RealizedFxGainAccountId] ON [dbo].[FinanceSettings];

            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE [object_id] = OBJECT_ID(N'dbo.FinanceSettings')
                  AND [name] = N'IX_FinanceSettings_RealizedFxLossAccountId')
                DROP INDEX [IX_FinanceSettings_RealizedFxLossAccountId] ON [dbo].[FinanceSettings];

            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE [object_id] = OBJECT_ID(N'dbo.FinanceSettings')
                  AND [name] = N'IX_FinanceSettings_WriteOffExpenseAccountId')
                DROP INDEX [IX_FinanceSettings_WriteOffExpenseAccountId] ON [dbo].[FinanceSettings];

            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE [object_id] = OBJECT_ID(N'dbo.FinanceSettings')
                  AND [name] = N'IX_FinanceSettings_WriteOffRecoveryAccountId')
                DROP INDEX [IX_FinanceSettings_WriteOffRecoveryAccountId] ON [dbo].[FinanceSettings];

            IF COL_LENGTH(N'dbo.FinanceSettings', N'MigrationClearingAccountId') IS NOT NULL
                ALTER TABLE [dbo].[FinanceSettings] DROP COLUMN [MigrationClearingAccountId];

            IF COL_LENGTH(N'dbo.FinanceSettings', N'RealizedFxGainAccountId') IS NOT NULL
                ALTER TABLE [dbo].[FinanceSettings] DROP COLUMN [RealizedFxGainAccountId];

            IF COL_LENGTH(N'dbo.FinanceSettings', N'RealizedFxLossAccountId') IS NOT NULL
                ALTER TABLE [dbo].[FinanceSettings] DROP COLUMN [RealizedFxLossAccountId];

            IF COL_LENGTH(N'dbo.FinanceSettings', N'SegmentClearingAccountId') IS NOT NULL
                ALTER TABLE [dbo].[FinanceSettings] DROP COLUMN [SegmentClearingAccountId];

            IF COL_LENGTH(N'dbo.FinanceSettings', N'SubledgerPostingMode') IS NOT NULL
                ALTER TABLE [dbo].[FinanceSettings] DROP COLUMN [SubledgerPostingMode];

            IF COL_LENGTH(N'dbo.FinanceSettings', N'WriteOffExpenseAccountId') IS NOT NULL
                ALTER TABLE [dbo].[FinanceSettings] DROP COLUMN [WriteOffExpenseAccountId];

            IF COL_LENGTH(N'dbo.FinanceSettings', N'WriteOffRecoveryAccountId') IS NOT NULL
                ALTER TABLE [dbo].[FinanceSettings] DROP COLUMN [WriteOffRecoveryAccountId];

            IF COL_LENGTH(N'dbo.FinanceSettings', N'RequireSubledgerJournalApproval') IS NOT NULL
                ALTER TABLE [dbo].[FinanceSettings] DROP COLUMN [RequireSubledgerJournalApproval];
            """);
    }
}
