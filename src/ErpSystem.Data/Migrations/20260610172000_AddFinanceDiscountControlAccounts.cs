using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260610172000_AddFinanceDiscountControlAccounts")]
    public partial class AddFinanceDiscountControlAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[FinanceSettings]', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH(N'dbo.FinanceSettings', N'DiscountAllowedAccountId') IS NULL
                        ALTER TABLE [dbo].[FinanceSettings] ADD [DiscountAllowedAccountId] uniqueidentifier NULL;

                    IF COL_LENGTH(N'dbo.FinanceSettings', N'DiscountReceivedAccountId') IS NULL
                        ALTER TABLE [dbo].[FinanceSettings] ADD [DiscountReceivedAccountId] uniqueidentifier NULL;

                    IF NOT EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE [object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
                          AND [name] = N'IX_FinanceSettings_DiscountAllowedAccountId')
                        CREATE INDEX [IX_FinanceSettings_DiscountAllowedAccountId]
                            ON [dbo].[FinanceSettings] ([DiscountAllowedAccountId]);

                    IF NOT EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE [object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
                          AND [name] = N'IX_FinanceSettings_DiscountReceivedAccountId')
                        CREATE INDEX [IX_FinanceSettings_DiscountReceivedAccountId]
                            ON [dbo].[FinanceSettings] ([DiscountReceivedAccountId]);

                    IF OBJECT_ID(N'[dbo].[Accounts]', N'U') IS NOT NULL
                       AND NOT EXISTS (
                           SELECT 1 FROM sys.foreign_keys
                           WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
                             AND [name] = N'FK_FinanceSettings_Accounts_DiscountAllowedAccountId')
                        ALTER TABLE [dbo].[FinanceSettings]
                            ADD CONSTRAINT [FK_FinanceSettings_Accounts_DiscountAllowedAccountId]
                            FOREIGN KEY ([DiscountAllowedAccountId]) REFERENCES [dbo].[Accounts] ([Id]);

                    IF OBJECT_ID(N'[dbo].[Accounts]', N'U') IS NOT NULL
                       AND NOT EXISTS (
                           SELECT 1 FROM sys.foreign_keys
                           WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
                             AND [name] = N'FK_FinanceSettings_Accounts_DiscountReceivedAccountId')
                        ALTER TABLE [dbo].[FinanceSettings]
                            ADD CONSTRAINT [FK_FinanceSettings_Accounts_DiscountReceivedAccountId]
                            FOREIGN KEY ([DiscountReceivedAccountId]) REFERENCES [dbo].[Accounts] ([Id]);
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[FinanceSettings]', N'U') IS NOT NULL
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM sys.foreign_keys
                        WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
                          AND [name] = N'FK_FinanceSettings_Accounts_DiscountAllowedAccountId')
                        ALTER TABLE [dbo].[FinanceSettings]
                            DROP CONSTRAINT [FK_FinanceSettings_Accounts_DiscountAllowedAccountId];

                    IF EXISTS (
                        SELECT 1 FROM sys.foreign_keys
                        WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
                          AND [name] = N'FK_FinanceSettings_Accounts_DiscountReceivedAccountId')
                        ALTER TABLE [dbo].[FinanceSettings]
                            DROP CONSTRAINT [FK_FinanceSettings_Accounts_DiscountReceivedAccountId];

                    IF EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE [object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
                          AND [name] = N'IX_FinanceSettings_DiscountAllowedAccountId')
                        DROP INDEX [IX_FinanceSettings_DiscountAllowedAccountId]
                            ON [dbo].[FinanceSettings];

                    IF EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE [object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
                          AND [name] = N'IX_FinanceSettings_DiscountReceivedAccountId')
                        DROP INDEX [IX_FinanceSettings_DiscountReceivedAccountId]
                            ON [dbo].[FinanceSettings];

                    IF COL_LENGTH(N'dbo.FinanceSettings', N'DiscountAllowedAccountId') IS NOT NULL
                        ALTER TABLE [dbo].[FinanceSettings] DROP COLUMN [DiscountAllowedAccountId];

                    IF COL_LENGTH(N'dbo.FinanceSettings', N'DiscountReceivedAccountId') IS NOT NULL
                        ALTER TABLE [dbo].[FinanceSettings] DROP COLUMN [DiscountReceivedAccountId];
                END;
                """);
        }
    }
}
