using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [Migration("20260614103000_EnsureFinanceDiscountControlAccounts")]
    public partial class EnsureFinanceDiscountControlAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[FinanceSettings]', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH(N'[dbo].[FinanceSettings]', N'DiscountAllowedAccountId') IS NULL
                    BEGIN
                        ALTER TABLE [dbo].[FinanceSettings] ADD [DiscountAllowedAccountId] uniqueidentifier NULL;
                    END

                    IF COL_LENGTH(N'[dbo].[FinanceSettings]', N'DiscountReceivedAccountId') IS NULL
                    BEGIN
                        ALTER TABLE [dbo].[FinanceSettings] ADD [DiscountReceivedAccountId] uniqueidentifier NULL;
                    END

                    IF COL_LENGTH(N'[dbo].[FinanceSettings]', N'DiscountAllowedAccountId') IS NOT NULL
                        AND NOT EXISTS (
                            SELECT 1
                            FROM sys.indexes
                            WHERE [name] = N'IX_FinanceSettings_DiscountAllowedAccountId'
                                AND [object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
                        )
                    BEGIN
                        CREATE INDEX [IX_FinanceSettings_DiscountAllowedAccountId]
                            ON [dbo].[FinanceSettings] ([DiscountAllowedAccountId]);
                    END

                    IF COL_LENGTH(N'[dbo].[FinanceSettings]', N'DiscountReceivedAccountId') IS NOT NULL
                        AND NOT EXISTS (
                            SELECT 1
                            FROM sys.indexes
                            WHERE [name] = N'IX_FinanceSettings_DiscountReceivedAccountId'
                                AND [object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
                        )
                    BEGIN
                        CREATE INDEX [IX_FinanceSettings_DiscountReceivedAccountId]
                            ON [dbo].[FinanceSettings] ([DiscountReceivedAccountId]);
                    END

                    IF OBJECT_ID(N'[dbo].[Accounts]', N'U') IS NOT NULL
                        AND COL_LENGTH(N'[dbo].[FinanceSettings]', N'DiscountAllowedAccountId') IS NOT NULL
                        AND NOT EXISTS (
                            SELECT 1
                            FROM sys.foreign_keys
                            WHERE [name] = N'FK_FinanceSettings_Accounts_DiscountAllowedAccountId'
                                AND [parent_object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
                        )
                    BEGIN
                        ALTER TABLE [dbo].[FinanceSettings]
                            ADD CONSTRAINT [FK_FinanceSettings_Accounts_DiscountAllowedAccountId]
                            FOREIGN KEY ([DiscountAllowedAccountId]) REFERENCES [dbo].[Accounts] ([Id]);
                    END

                    IF OBJECT_ID(N'[dbo].[Accounts]', N'U') IS NOT NULL
                        AND COL_LENGTH(N'[dbo].[FinanceSettings]', N'DiscountReceivedAccountId') IS NOT NULL
                        AND NOT EXISTS (
                            SELECT 1
                            FROM sys.foreign_keys
                            WHERE [name] = N'FK_FinanceSettings_Accounts_DiscountReceivedAccountId'
                                AND [parent_object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
                        )
                    BEGIN
                        ALTER TABLE [dbo].[FinanceSettings]
                            ADD CONSTRAINT [FK_FinanceSettings_Accounts_DiscountReceivedAccountId]
                            FOREIGN KEY ([DiscountReceivedAccountId]) REFERENCES [dbo].[Accounts] ([Id]);
                    END
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[FinanceSettings]', N'U') IS NOT NULL
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM sys.foreign_keys
                        WHERE [name] = N'FK_FinanceSettings_Accounts_DiscountAllowedAccountId'
                            AND [parent_object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
                    )
                    BEGIN
                        ALTER TABLE [dbo].[FinanceSettings]
                            DROP CONSTRAINT [FK_FinanceSettings_Accounts_DiscountAllowedAccountId];
                    END

                    IF EXISTS (
                        SELECT 1
                        FROM sys.foreign_keys
                        WHERE [name] = N'FK_FinanceSettings_Accounts_DiscountReceivedAccountId'
                            AND [parent_object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
                    )
                    BEGIN
                        ALTER TABLE [dbo].[FinanceSettings]
                            DROP CONSTRAINT [FK_FinanceSettings_Accounts_DiscountReceivedAccountId];
                    END

                    IF EXISTS (
                        SELECT 1
                        FROM sys.indexes
                        WHERE [name] = N'IX_FinanceSettings_DiscountAllowedAccountId'
                            AND [object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
                    )
                    BEGIN
                        DROP INDEX [IX_FinanceSettings_DiscountAllowedAccountId]
                            ON [dbo].[FinanceSettings];
                    END

                    IF EXISTS (
                        SELECT 1
                        FROM sys.indexes
                        WHERE [name] = N'IX_FinanceSettings_DiscountReceivedAccountId'
                            AND [object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
                    )
                    BEGIN
                        DROP INDEX [IX_FinanceSettings_DiscountReceivedAccountId]
                            ON [dbo].[FinanceSettings];
                    END

                    IF COL_LENGTH(N'[dbo].[FinanceSettings]', N'DiscountAllowedAccountId') IS NOT NULL
                    BEGIN
                        ALTER TABLE [dbo].[FinanceSettings] DROP COLUMN [DiscountAllowedAccountId];
                    END

                    IF COL_LENGTH(N'[dbo].[FinanceSettings]', N'DiscountReceivedAccountId') IS NOT NULL
                    BEGIN
                        ALTER TABLE [dbo].[FinanceSettings] DROP COLUMN [DiscountReceivedAccountId];
                    END
                END
                """);
        }
    }
}
