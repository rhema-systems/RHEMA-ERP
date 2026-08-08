using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    // Normal Debug/deployment builds intentionally omit the very large generated migration
    // designers. Keep discovery metadata on the executable migration as well so this Finance
    // control remains visible to startup migration checks and database-update commands.
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260803213749_AddCrossCurrencyBankTransfers")]
    /// <inheritdoc />
    public partial class AddCrossCurrencyBankTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CashTransaction previously inherited the legacy global four-place decimal rule.
            // Transfer rate evidence must retain the same six-place precision as ExchangeRates.
            migrationBuilder.AlterColumn<decimal>(
                name: "ExchangeRate",
                table: "CashTransaction",
                type: "decimal(18,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldNullable: true);

            // Earlier Finance snapshot reconciliation builds may already contain
            // part of this column set without the migration-history row. Add only
            // the missing members so the migration is safe on both clean and
            // partially reconciled databases.
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'dbo.CashTransaction', N'ExchangeRateDate') IS NULL
                    ALTER TABLE [dbo].[CashTransaction] ADD [ExchangeRateDate] datetime2 NULL;

                IF COL_LENGTH(N'dbo.CashTransaction', N'ExchangeRateId') IS NULL
                    ALTER TABLE [dbo].[CashTransaction] ADD [ExchangeRateId] uniqueidentifier NULL;

                IF COL_LENGTH(N'dbo.CashTransaction', N'ExchangeRateQuoteSide') IS NULL
                    ALTER TABLE [dbo].[CashTransaction] ADD [ExchangeRateQuoteSide] int NULL;

                IF COL_LENGTH(N'dbo.CashTransaction', N'ExchangeRateSource') IS NULL
                    ALTER TABLE [dbo].[CashTransaction] ADD [ExchangeRateSource] nvarchar(100) NULL;

                IF COL_LENGTH(N'dbo.CashTransaction', N'TransferCrossRate') IS NULL
                    ALTER TABLE [dbo].[CashTransaction] ADD [TransferCrossRate] decimal(18,8) NULL;

                IF COL_LENGTH(N'dbo.CashTransaction', N'TransferFxGainLossBaseAmount') IS NULL
                    ALTER TABLE [dbo].[CashTransaction] ADD [TransferFxGainLossBaseAmount] decimal(18,2) NOT NULL
                        CONSTRAINT [DF_CashTransaction_TransferFxGainLossBaseAmount] DEFAULT (0);

                IF COL_LENGTH(N'dbo.CashTransaction', N'TransferLeg') IS NULL
                    ALTER TABLE [dbo].[CashTransaction] ADD [TransferLeg] int NULL;

                IF COL_LENGTH(N'dbo.CashTransaction', N'TransferPairId') IS NULL
                    ALTER TABLE [dbo].[CashTransaction] ADD [TransferPairId] uniqueidentifier NULL;
                """);

            migrationBuilder.Sql(
                """
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE [object_id] = OBJECT_ID(N'dbo.CashTransaction')
                      AND [name] = N'IX_CashTransaction_ExchangeRateId')
                    CREATE INDEX [IX_CashTransaction_ExchangeRateId]
                        ON [dbo].[CashTransaction] ([ExchangeRateId]);

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE [object_id] = OBJECT_ID(N'dbo.CashTransaction')
                      AND [name] = N'IX_CashTransaction_TenantId_ExchangeRateId')
                    CREATE INDEX [IX_CashTransaction_TenantId_ExchangeRateId]
                        ON [dbo].[CashTransaction] ([TenantId], [ExchangeRateId]);

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE [object_id] = OBJECT_ID(N'dbo.CashTransaction')
                      AND [name] = N'IX_CashTransaction_TenantId_TransferPairId_TransferLeg')
                    CREATE UNIQUE INDEX [IX_CashTransaction_TenantId_TransferPairId_TransferLeg]
                        ON [dbo].[CashTransaction] ([TenantId], [TransferPairId], [TransferLeg])
                        WHERE [TransferPairId] IS NOT NULL AND [TransferLeg] IS NOT NULL;

                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE [parent_object_id] = OBJECT_ID(N'dbo.CashTransaction')
                      AND [name] = N'FK_CashTransaction_ExchangeRates_ExchangeRateId')
                BEGIN
                    ALTER TABLE [dbo].[CashTransaction] WITH CHECK
                        ADD CONSTRAINT [FK_CashTransaction_ExchangeRates_ExchangeRateId]
                        FOREIGN KEY ([ExchangeRateId]) REFERENCES [dbo].[ExchangeRates] ([Id]);
                    ALTER TABLE [dbo].[CashTransaction]
                        CHECK CONSTRAINT [FK_CashTransaction_ExchangeRates_ExchangeRateId];
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashTransaction_ExchangeRates_ExchangeRateId",
                table: "CashTransaction");

            migrationBuilder.DropIndex(
                name: "IX_CashTransaction_ExchangeRateId",
                table: "CashTransaction");

            migrationBuilder.DropIndex(
                name: "IX_CashTransaction_TenantId_ExchangeRateId",
                table: "CashTransaction");

            migrationBuilder.DropIndex(
                name: "IX_CashTransaction_TenantId_TransferPairId_TransferLeg",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "ExchangeRateDate",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "ExchangeRateId",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "ExchangeRateQuoteSide",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "ExchangeRateSource",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "TransferCrossRate",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "TransferFxGainLossBaseAmount",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "TransferLeg",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "TransferPairId",
                table: "CashTransaction");

            migrationBuilder.AlterColumn<decimal>(
                name: "ExchangeRate",
                table: "CashTransaction",
                type: "decimal(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldNullable: true);
        }
    }
}
