using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260709120000_AddOpeningBalancePostingFoundation")]
    public partial class AddOpeningBalancePostingFoundation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[OpeningBalanceBatches]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[OpeningBalanceBatches] (
                        [Id] uniqueidentifier NOT NULL,
                        [TenantId] uniqueidentifier NOT NULL,
                        [BatchNumber] nvarchar(50) NOT NULL,
                        [SourceReference] nvarchar(100) NULL,
                        [Description] nvarchar(500) NULL,
                        [OpeningDate] datetime2 NOT NULL,
                        [FiscalPeriodId] uniqueidentifier NOT NULL,
                        [BookClassification] nvarchar(30) NOT NULL,
                        [IdempotencyKey] nvarchar(120) NOT NULL,
                        [TotalDebit] decimal(18,2) NOT NULL,
                        [TotalCredit] decimal(18,2) NOT NULL,
                        [Difference] decimal(18,2) NOT NULL,
                        [JournalEntryId] uniqueidentifier NULL,
                        [PostingEventId] uniqueidentifier NULL,
                        [WorkflowInstanceId] uniqueidentifier NULL,
                        [ValidatedAt] datetime2 NULL,
                        [SubmittedAt] datetime2 NULL,
                        [ApprovedAt] datetime2 NULL,
                        [PostedAt] datetime2 NULL,
                        [FailedAt] datetime2 NULL,
                        [FailureReason] nvarchar(1000) NULL,
                        [ReferenceNumber] nvarchar(50) NOT NULL CONSTRAINT [DF_OpeningBalanceBatches_ReferenceNumber] DEFAULT N'',
                        [Status] nvarchar(50) NOT NULL CONSTRAINT [DF_OpeningBalanceBatches_Status] DEFAULT N'Draft',
                        [EffectiveDate] datetime2 NULL,
                        [ExpirationDate] datetime2 NULL,
                        [Metadata] nvarchar(max) NULL,
                        [Tags] nvarchar(500) NULL,
                        [Priority] int NOT NULL CONSTRAINT [DF_OpeningBalanceBatches_Priority] DEFAULT 5,
                        [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_OpeningBalanceBatches_CreatedAt] DEFAULT SYSUTCDATETIME(),
                        [UpdatedAt] datetime2 NULL,
                        [CreatedBy] nvarchar(max) NULL,
                        [UpdatedBy] nvarchar(max) NULL,
                        [CreatedById] uniqueidentifier NULL,
                        [LastModifiedById] uniqueidentifier NULL,
                        [IsDeleted] bit NOT NULL CONSTRAINT [DF_OpeningBalanceBatches_IsDeleted] DEFAULT 0,
                        [DeletedAt] datetime2 NULL,
                        [DeletedBy] nvarchar(max) NULL,
                        CONSTRAINT [PK_OpeningBalanceBatches] PRIMARY KEY ([Id])
                    );
                END

                IF OBJECT_ID(N'[dbo].[OpeningBalanceLines]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[OpeningBalanceLines] (
                        [Id] uniqueidentifier NOT NULL,
                        [TenantId] uniqueidentifier NOT NULL,
                        [OpeningBalanceBatchId] uniqueidentifier NOT NULL,
                        [LineNumber] int NOT NULL,
                        [AccountId] uniqueidentifier NOT NULL,
                        [DebitAmount] decimal(18,2) NOT NULL,
                        [CreditAmount] decimal(18,2) NOT NULL,
                        [TransactionCurrencyCode] nvarchar(3) NOT NULL,
                        [FunctionalCurrencyCode] nvarchar(3) NOT NULL,
                        [ExchangeRateId] uniqueidentifier NULL,
                        [ExchangeRateDate] datetime2 NULL,
                        [SegmentString] nvarchar(500) NULL,
                        [BankAccountId] uniqueidentifier NULL,
                        [CounterpartyType] nvarchar(30) NULL,
                        [CounterpartyId] uniqueidentifier NULL,
                        [SourceReference] nvarchar(100) NULL,
                        [Notes] nvarchar(500) NULL,
                        [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_OpeningBalanceLines_CreatedAt] DEFAULT SYSUTCDATETIME(),
                        [UpdatedAt] datetime2 NULL,
                        [CreatedBy] nvarchar(max) NULL,
                        [UpdatedBy] nvarchar(max) NULL,
                        [CreatedById] uniqueidentifier NULL,
                        [LastModifiedById] uniqueidentifier NULL,
                        [IsDeleted] bit NOT NULL CONSTRAINT [DF_OpeningBalanceLines_IsDeleted] DEFAULT 0,
                        [DeletedAt] datetime2 NULL,
                        [DeletedBy] nvarchar(max) NULL,
                        CONSTRAINT [PK_OpeningBalanceLines] PRIMARY KEY ([Id])
                    );
                END

                IF OBJECT_ID(N'[dbo].[OpeningBalanceBatches]', N'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_OpeningBalanceBatches_TenantId_BatchNumber' AND [object_id] = OBJECT_ID(N'[dbo].[OpeningBalanceBatches]'))
                        CREATE UNIQUE INDEX [IX_OpeningBalanceBatches_TenantId_BatchNumber] ON [dbo].[OpeningBalanceBatches] ([TenantId], [BatchNumber]) WHERE [IsDeleted] = 0;
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_OpeningBalanceBatches_TenantId_IdempotencyKey' AND [object_id] = OBJECT_ID(N'[dbo].[OpeningBalanceBatches]'))
                        CREATE UNIQUE INDEX [IX_OpeningBalanceBatches_TenantId_IdempotencyKey] ON [dbo].[OpeningBalanceBatches] ([TenantId], [IdempotencyKey]) WHERE [IsDeleted] = 0;
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_OpeningBalanceBatches_TenantId_Status' AND [object_id] = OBJECT_ID(N'[dbo].[OpeningBalanceBatches]'))
                        CREATE INDEX [IX_OpeningBalanceBatches_TenantId_Status] ON [dbo].[OpeningBalanceBatches] ([TenantId], [Status]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_OpeningBalanceBatches_TenantId_PostingEventId' AND [object_id] = OBJECT_ID(N'[dbo].[OpeningBalanceBatches]'))
                        CREATE INDEX [IX_OpeningBalanceBatches_TenantId_PostingEventId] ON [dbo].[OpeningBalanceBatches] ([TenantId], [PostingEventId]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_OpeningBalanceBatches_TenantId_JournalEntryId' AND [object_id] = OBJECT_ID(N'[dbo].[OpeningBalanceBatches]'))
                        CREATE INDEX [IX_OpeningBalanceBatches_TenantId_JournalEntryId] ON [dbo].[OpeningBalanceBatches] ([TenantId], [JournalEntryId]);
                END

                IF OBJECT_ID(N'[dbo].[OpeningBalanceLines]', N'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_OpeningBalanceLines_TenantId_OpeningBalanceBatchId_LineNumber' AND [object_id] = OBJECT_ID(N'[dbo].[OpeningBalanceLines]'))
                        CREATE UNIQUE INDEX [IX_OpeningBalanceLines_TenantId_OpeningBalanceBatchId_LineNumber] ON [dbo].[OpeningBalanceLines] ([TenantId], [OpeningBalanceBatchId], [LineNumber]) WHERE [IsDeleted] = 0;
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_OpeningBalanceLines_TenantId_AccountId' AND [object_id] = OBJECT_ID(N'[dbo].[OpeningBalanceLines]'))
                        CREATE INDEX [IX_OpeningBalanceLines_TenantId_AccountId] ON [dbo].[OpeningBalanceLines] ([TenantId], [AccountId]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_OpeningBalanceLines_TenantId_BankAccountId' AND [object_id] = OBJECT_ID(N'[dbo].[OpeningBalanceLines]'))
                        CREATE INDEX [IX_OpeningBalanceLines_TenantId_BankAccountId] ON [dbo].[OpeningBalanceLines] ([TenantId], [BankAccountId]);
                END

                IF OBJECT_ID(N'[dbo].[OpeningBalanceBatches]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[Tenants]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_OpeningBalanceBatches_Tenants_TenantId')
                    ALTER TABLE [dbo].[OpeningBalanceBatches] ADD CONSTRAINT [FK_OpeningBalanceBatches_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION;
                IF OBJECT_ID(N'[dbo].[OpeningBalanceBatches]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[FiscalPeriods]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_OpeningBalanceBatches_FiscalPeriods_FiscalPeriodId')
                    ALTER TABLE [dbo].[OpeningBalanceBatches] ADD CONSTRAINT [FK_OpeningBalanceBatches_FiscalPeriods_FiscalPeriodId] FOREIGN KEY ([FiscalPeriodId]) REFERENCES [dbo].[FiscalPeriods] ([Id]) ON DELETE NO ACTION;
                IF OBJECT_ID(N'[dbo].[OpeningBalanceBatches]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[JournalEntries]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_OpeningBalanceBatches_JournalEntries_JournalEntryId')
                    ALTER TABLE [dbo].[OpeningBalanceBatches] ADD CONSTRAINT [FK_OpeningBalanceBatches_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [dbo].[JournalEntries] ([Id]) ON DELETE NO ACTION;
                IF OBJECT_ID(N'[dbo].[OpeningBalanceBatches]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[FinancePostingEvents]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_OpeningBalanceBatches_FinancePostingEvents_PostingEventId')
                    ALTER TABLE [dbo].[OpeningBalanceBatches] ADD CONSTRAINT [FK_OpeningBalanceBatches_FinancePostingEvents_PostingEventId] FOREIGN KEY ([PostingEventId]) REFERENCES [dbo].[FinancePostingEvents] ([Id]) ON DELETE NO ACTION;

                IF OBJECT_ID(N'[dbo].[OpeningBalanceLines]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[OpeningBalanceBatches]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_OpeningBalanceLines_OpeningBalanceBatches_OpeningBalanceBatchId')
                    ALTER TABLE [dbo].[OpeningBalanceLines] ADD CONSTRAINT [FK_OpeningBalanceLines_OpeningBalanceBatches_OpeningBalanceBatchId] FOREIGN KEY ([OpeningBalanceBatchId]) REFERENCES [dbo].[OpeningBalanceBatches] ([Id]) ON DELETE CASCADE;
                IF OBJECT_ID(N'[dbo].[OpeningBalanceLines]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[Tenants]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_OpeningBalanceLines_Tenants_TenantId')
                    ALTER TABLE [dbo].[OpeningBalanceLines] ADD CONSTRAINT [FK_OpeningBalanceLines_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION;
                IF OBJECT_ID(N'[dbo].[OpeningBalanceLines]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[Accounts]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_OpeningBalanceLines_Accounts_AccountId')
                    ALTER TABLE [dbo].[OpeningBalanceLines] ADD CONSTRAINT [FK_OpeningBalanceLines_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION;
                IF OBJECT_ID(N'[dbo].[OpeningBalanceLines]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[ExchangeRates]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_OpeningBalanceLines_ExchangeRates_ExchangeRateId')
                    ALTER TABLE [dbo].[OpeningBalanceLines] ADD CONSTRAINT [FK_OpeningBalanceLines_ExchangeRates_ExchangeRateId] FOREIGN KEY ([ExchangeRateId]) REFERENCES [dbo].[ExchangeRates] ([Id]) ON DELETE NO ACTION;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_OpeningBalanceLines_ExchangeRates_ExchangeRateId')
                    ALTER TABLE [dbo].[OpeningBalanceLines] DROP CONSTRAINT [FK_OpeningBalanceLines_ExchangeRates_ExchangeRateId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_OpeningBalanceLines_Accounts_AccountId')
                    ALTER TABLE [dbo].[OpeningBalanceLines] DROP CONSTRAINT [FK_OpeningBalanceLines_Accounts_AccountId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_OpeningBalanceLines_Tenants_TenantId')
                    ALTER TABLE [dbo].[OpeningBalanceLines] DROP CONSTRAINT [FK_OpeningBalanceLines_Tenants_TenantId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_OpeningBalanceLines_OpeningBalanceBatches_OpeningBalanceBatchId')
                    ALTER TABLE [dbo].[OpeningBalanceLines] DROP CONSTRAINT [FK_OpeningBalanceLines_OpeningBalanceBatches_OpeningBalanceBatchId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_OpeningBalanceBatches_FinancePostingEvents_PostingEventId')
                    ALTER TABLE [dbo].[OpeningBalanceBatches] DROP CONSTRAINT [FK_OpeningBalanceBatches_FinancePostingEvents_PostingEventId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_OpeningBalanceBatches_JournalEntries_JournalEntryId')
                    ALTER TABLE [dbo].[OpeningBalanceBatches] DROP CONSTRAINT [FK_OpeningBalanceBatches_JournalEntries_JournalEntryId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_OpeningBalanceBatches_FiscalPeriods_FiscalPeriodId')
                    ALTER TABLE [dbo].[OpeningBalanceBatches] DROP CONSTRAINT [FK_OpeningBalanceBatches_FiscalPeriods_FiscalPeriodId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_OpeningBalanceBatches_Tenants_TenantId')
                    ALTER TABLE [dbo].[OpeningBalanceBatches] DROP CONSTRAINT [FK_OpeningBalanceBatches_Tenants_TenantId];

                IF OBJECT_ID(N'[dbo].[OpeningBalanceLines]', N'U') IS NOT NULL
                    DROP TABLE [dbo].[OpeningBalanceLines];
                IF OBJECT_ID(N'[dbo].[OpeningBalanceBatches]', N'U') IS NOT NULL
                    DROP TABLE [dbo].[OpeningBalanceBatches];
                """);
        }
    }
}
