using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260708133000_AddSubledgerSettlementReadModels")]
    public partial class AddSubledgerSettlementReadModels : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[SubledgerSettlementBalances]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[SubledgerSettlementBalances] (
                        [Id] uniqueidentifier NOT NULL,
                        [TenantId] uniqueidentifier NOT NULL,
                        [SourceModule] nvarchar(10) NOT NULL,
                        [CounterpartyId] uniqueidentifier NOT NULL,
                        [SourceDocumentType] nvarchar(50) NOT NULL,
                        [SourceDocumentId] uniqueidentifier NOT NULL,
                        [SourceDocumentNumber] nvarchar(100) NOT NULL,
                        [SourcePostingEventId] uniqueidentifier NULL,
                        [SourceJournalEntryId] uniqueidentifier NULL,
                        [TransactionDate] datetime2 NOT NULL,
                        [DueDate] datetime2 NULL,
                        [DocumentCurrencyCode] nvarchar(3) NOT NULL,
                        [FunctionalCurrencyCode] nvarchar(3) NOT NULL,
                        [OriginalDocumentAmount] decimal(18,2) NOT NULL,
                        [OriginalFunctionalAmount] decimal(18,2) NOT NULL,
                        [SettledAmount] decimal(18,2) NOT NULL,
                        [CreditedAmount] decimal(18,2) NOT NULL,
                        [WithheldAmount] decimal(18,2) NOT NULL,
                        [OutstandingAmount] decimal(18,2) NOT NULL,
                        [SettlementStatus] nvarchar(30) NOT NULL,
                        [RebuildBatchId] uniqueidentifier NOT NULL,
                        [LastRebuiltAt] datetime2 NOT NULL,
                        [HasDiagnostics] bit NOT NULL,
                        [DiagnosticFlags] nvarchar(1000) NULL,
                        [OperationalPaidAmountSnapshot] decimal(18,2) NOT NULL,
                        [OperationalCreditedAmountSnapshot] decimal(18,2) NOT NULL,
                        [OperationalOutstandingSnapshot] decimal(18,2) NOT NULL,
                        [OperationalVariance] decimal(18,2) NOT NULL,
                        [ReferenceNumber] nvarchar(50) NOT NULL CONSTRAINT [DF_SubledgerSettlementBalances_ReferenceNumber] DEFAULT N'',
                        [Status] nvarchar(50) NOT NULL CONSTRAINT [DF_SubledgerSettlementBalances_Status] DEFAULT N'Active',
                        [EffectiveDate] datetime2 NULL,
                        [ExpirationDate] datetime2 NULL,
                        [Metadata] nvarchar(max) NULL,
                        [Tags] nvarchar(500) NULL,
                        [Priority] int NOT NULL CONSTRAINT [DF_SubledgerSettlementBalances_Priority] DEFAULT 5,
                        [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_SubledgerSettlementBalances_CreatedAt] DEFAULT SYSUTCDATETIME(),
                        [UpdatedAt] datetime2 NULL,
                        [CreatedBy] nvarchar(max) NULL,
                        [UpdatedBy] nvarchar(max) NULL,
                        [CreatedById] uniqueidentifier NULL,
                        [LastModifiedById] uniqueidentifier NULL,
                        [IsDeleted] bit NOT NULL CONSTRAINT [DF_SubledgerSettlementBalances_IsDeleted] DEFAULT 0,
                        [DeletedAt] datetime2 NULL,
                        [DeletedBy] nvarchar(max) NULL,
                        CONSTRAINT [PK_SubledgerSettlementBalances] PRIMARY KEY ([Id])
                    );
                END

                IF OBJECT_ID(N'[dbo].[SubledgerSettlementApplications]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[SubledgerSettlementApplications] (
                        [Id] uniqueidentifier NOT NULL,
                        [TenantId] uniqueidentifier NOT NULL,
                        [SubledgerSettlementBalanceId] uniqueidentifier NOT NULL,
                        [SourceModule] nvarchar(10) NOT NULL,
                        [CounterpartyId] uniqueidentifier NOT NULL,
                        [SourceDocumentType] nvarchar(50) NOT NULL,
                        [SourceDocumentId] uniqueidentifier NOT NULL,
                        [SettlementSourceType] nvarchar(50) NOT NULL,
                        [SettlementSourceId] uniqueidentifier NOT NULL,
                        [SettlementAllocationId] uniqueidentifier NULL,
                        [SettlementPostingEventId] uniqueidentifier NULL,
                        [SettlementJournalEntryId] uniqueidentifier NULL,
                        [SettlementDate] datetime2 NOT NULL,
                        [DocumentCurrencyCode] nvarchar(3) NOT NULL,
                        [FunctionalCurrencyCode] nvarchar(3) NOT NULL,
                        [SettledAmount] decimal(18,2) NOT NULL,
                        [CreditedAmount] decimal(18,2) NOT NULL,
                        [WithheldAmount] decimal(18,2) NOT NULL,
                        [FxRealizedSettlementId] uniqueidentifier NULL,
                        [RebuildBatchId] uniqueidentifier NOT NULL,
                        [Notes] nvarchar(1000) NULL,
                        [ReferenceNumber] nvarchar(50) NOT NULL CONSTRAINT [DF_SubledgerSettlementApplications_ReferenceNumber] DEFAULT N'',
                        [Status] nvarchar(50) NOT NULL CONSTRAINT [DF_SubledgerSettlementApplications_Status] DEFAULT N'Active',
                        [EffectiveDate] datetime2 NULL,
                        [ExpirationDate] datetime2 NULL,
                        [Metadata] nvarchar(max) NULL,
                        [Tags] nvarchar(500) NULL,
                        [Priority] int NOT NULL CONSTRAINT [DF_SubledgerSettlementApplications_Priority] DEFAULT 5,
                        [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_SubledgerSettlementApplications_CreatedAt] DEFAULT SYSUTCDATETIME(),
                        [UpdatedAt] datetime2 NULL,
                        [CreatedBy] nvarchar(max) NULL,
                        [UpdatedBy] nvarchar(max) NULL,
                        [CreatedById] uniqueidentifier NULL,
                        [LastModifiedById] uniqueidentifier NULL,
                        [IsDeleted] bit NOT NULL CONSTRAINT [DF_SubledgerSettlementApplications_IsDeleted] DEFAULT 0,
                        [DeletedAt] datetime2 NULL,
                        [DeletedBy] nvarchar(max) NULL,
                        CONSTRAINT [PK_SubledgerSettlementApplications] PRIMARY KEY ([Id])
                    );
                END

                IF OBJECT_ID(N'[dbo].[SubledgerSettlementBalances]', N'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SubledgerSettlementBalances_TenantId_SourceModule_SourceDocumentType_SourceDocumentId' AND [object_id] = OBJECT_ID(N'[dbo].[SubledgerSettlementBalances]'))
                        CREATE UNIQUE INDEX [IX_SubledgerSettlementBalances_TenantId_SourceModule_SourceDocumentType_SourceDocumentId] ON [dbo].[SubledgerSettlementBalances] ([TenantId], [SourceModule], [SourceDocumentType], [SourceDocumentId]) WHERE [IsDeleted] = 0;
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SubledgerSettlementBalances_TenantId_SourceModule_CounterpartyId_DueDate' AND [object_id] = OBJECT_ID(N'[dbo].[SubledgerSettlementBalances]'))
                        CREATE INDEX [IX_SubledgerSettlementBalances_TenantId_SourceModule_CounterpartyId_DueDate] ON [dbo].[SubledgerSettlementBalances] ([TenantId], [SourceModule], [CounterpartyId], [DueDate]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SubledgerSettlementBalances_TenantId_SourcePostingEventId' AND [object_id] = OBJECT_ID(N'[dbo].[SubledgerSettlementBalances]'))
                        CREATE INDEX [IX_SubledgerSettlementBalances_TenantId_SourcePostingEventId] ON [dbo].[SubledgerSettlementBalances] ([TenantId], [SourcePostingEventId]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SubledgerSettlementBalances_TenantId_SourceJournalEntryId' AND [object_id] = OBJECT_ID(N'[dbo].[SubledgerSettlementBalances]'))
                        CREATE INDEX [IX_SubledgerSettlementBalances_TenantId_SourceJournalEntryId] ON [dbo].[SubledgerSettlementBalances] ([TenantId], [SourceJournalEntryId]);
                END

                IF OBJECT_ID(N'[dbo].[SubledgerSettlementApplications]', N'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SubledgerSettlementApplications_TenantId_SourceModule_SourceDocumentType_SourceDocumentId' AND [object_id] = OBJECT_ID(N'[dbo].[SubledgerSettlementApplications]'))
                        CREATE INDEX [IX_SubledgerSettlementApplications_TenantId_SourceModule_SourceDocumentType_SourceDocumentId] ON [dbo].[SubledgerSettlementApplications] ([TenantId], [SourceModule], [SourceDocumentType], [SourceDocumentId]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SubledgerSettlementApplications_TenantId_SettlementSourceType_SettlementSourceId_SettlementAllocationId' AND [object_id] = OBJECT_ID(N'[dbo].[SubledgerSettlementApplications]'))
                        CREATE INDEX [IX_SubledgerSettlementApplications_TenantId_SettlementSourceType_SettlementSourceId_SettlementAllocationId] ON [dbo].[SubledgerSettlementApplications] ([TenantId], [SettlementSourceType], [SettlementSourceId], [SettlementAllocationId]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SubledgerSettlementApplications_TenantId_SettlementPostingEventId' AND [object_id] = OBJECT_ID(N'[dbo].[SubledgerSettlementApplications]'))
                        CREATE INDEX [IX_SubledgerSettlementApplications_TenantId_SettlementPostingEventId] ON [dbo].[SubledgerSettlementApplications] ([TenantId], [SettlementPostingEventId]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SubledgerSettlementApplications_TenantId_SettlementJournalEntryId' AND [object_id] = OBJECT_ID(N'[dbo].[SubledgerSettlementApplications]'))
                        CREATE INDEX [IX_SubledgerSettlementApplications_TenantId_SettlementJournalEntryId] ON [dbo].[SubledgerSettlementApplications] ([TenantId], [SettlementJournalEntryId]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SubledgerSettlementApplications_SubledgerSettlementBalanceId' AND [object_id] = OBJECT_ID(N'[dbo].[SubledgerSettlementApplications]'))
                        CREATE INDEX [IX_SubledgerSettlementApplications_SubledgerSettlementBalanceId] ON [dbo].[SubledgerSettlementApplications] ([SubledgerSettlementBalanceId]);
                END

                IF OBJECT_ID(N'[dbo].[SubledgerSettlementBalances]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[Tenants]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SubledgerSettlementBalances_Tenants_TenantId')
                    ALTER TABLE [dbo].[SubledgerSettlementBalances] ADD CONSTRAINT [FK_SubledgerSettlementBalances_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION;
                IF OBJECT_ID(N'[dbo].[SubledgerSettlementBalances]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[FinancePostingEvents]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SubledgerSettlementBalances_FinancePostingEvents_SourcePostingEventId')
                    ALTER TABLE [dbo].[SubledgerSettlementBalances] ADD CONSTRAINT [FK_SubledgerSettlementBalances_FinancePostingEvents_SourcePostingEventId] FOREIGN KEY ([SourcePostingEventId]) REFERENCES [dbo].[FinancePostingEvents] ([Id]) ON DELETE NO ACTION;
                IF OBJECT_ID(N'[dbo].[SubledgerSettlementBalances]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[JournalEntries]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SubledgerSettlementBalances_JournalEntries_SourceJournalEntryId')
                    ALTER TABLE [dbo].[SubledgerSettlementBalances] ADD CONSTRAINT [FK_SubledgerSettlementBalances_JournalEntries_SourceJournalEntryId] FOREIGN KEY ([SourceJournalEntryId]) REFERENCES [dbo].[JournalEntries] ([Id]) ON DELETE NO ACTION;

                IF OBJECT_ID(N'[dbo].[SubledgerSettlementApplications]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[SubledgerSettlementBalances]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SubledgerSettlementApplications_SubledgerSettlementBalances_SubledgerSettlementBalanceId')
                    ALTER TABLE [dbo].[SubledgerSettlementApplications] ADD CONSTRAINT [FK_SubledgerSettlementApplications_SubledgerSettlementBalances_SubledgerSettlementBalanceId] FOREIGN KEY ([SubledgerSettlementBalanceId]) REFERENCES [dbo].[SubledgerSettlementBalances] ([Id]) ON DELETE CASCADE;
                IF OBJECT_ID(N'[dbo].[SubledgerSettlementApplications]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[Tenants]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SubledgerSettlementApplications_Tenants_TenantId')
                    ALTER TABLE [dbo].[SubledgerSettlementApplications] ADD CONSTRAINT [FK_SubledgerSettlementApplications_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION;
                IF OBJECT_ID(N'[dbo].[SubledgerSettlementApplications]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[FinancePostingEvents]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SubledgerSettlementApplications_FinancePostingEvents_SettlementPostingEventId')
                    ALTER TABLE [dbo].[SubledgerSettlementApplications] ADD CONSTRAINT [FK_SubledgerSettlementApplications_FinancePostingEvents_SettlementPostingEventId] FOREIGN KEY ([SettlementPostingEventId]) REFERENCES [dbo].[FinancePostingEvents] ([Id]) ON DELETE NO ACTION;
                IF OBJECT_ID(N'[dbo].[SubledgerSettlementApplications]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[JournalEntries]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SubledgerSettlementApplications_JournalEntries_SettlementJournalEntryId')
                    ALTER TABLE [dbo].[SubledgerSettlementApplications] ADD CONSTRAINT [FK_SubledgerSettlementApplications_JournalEntries_SettlementJournalEntryId] FOREIGN KEY ([SettlementJournalEntryId]) REFERENCES [dbo].[JournalEntries] ([Id]) ON DELETE NO ACTION;
                IF OBJECT_ID(N'[dbo].[SubledgerSettlementApplications]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[FxRealizedSettlements]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SubledgerSettlementApplications_FxRealizedSettlements_FxRealizedSettlementId')
                    ALTER TABLE [dbo].[SubledgerSettlementApplications] ADD CONSTRAINT [FK_SubledgerSettlementApplications_FxRealizedSettlements_FxRealizedSettlementId] FOREIGN KEY ([FxRealizedSettlementId]) REFERENCES [dbo].[FxRealizedSettlements] ([Id]) ON DELETE NO ACTION;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SubledgerSettlementApplications_FxRealizedSettlements_FxRealizedSettlementId')
                    ALTER TABLE [dbo].[SubledgerSettlementApplications] DROP CONSTRAINT [FK_SubledgerSettlementApplications_FxRealizedSettlements_FxRealizedSettlementId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SubledgerSettlementApplications_JournalEntries_SettlementJournalEntryId')
                    ALTER TABLE [dbo].[SubledgerSettlementApplications] DROP CONSTRAINT [FK_SubledgerSettlementApplications_JournalEntries_SettlementJournalEntryId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SubledgerSettlementApplications_FinancePostingEvents_SettlementPostingEventId')
                    ALTER TABLE [dbo].[SubledgerSettlementApplications] DROP CONSTRAINT [FK_SubledgerSettlementApplications_FinancePostingEvents_SettlementPostingEventId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SubledgerSettlementApplications_Tenants_TenantId')
                    ALTER TABLE [dbo].[SubledgerSettlementApplications] DROP CONSTRAINT [FK_SubledgerSettlementApplications_Tenants_TenantId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SubledgerSettlementApplications_SubledgerSettlementBalances_SubledgerSettlementBalanceId')
                    ALTER TABLE [dbo].[SubledgerSettlementApplications] DROP CONSTRAINT [FK_SubledgerSettlementApplications_SubledgerSettlementBalances_SubledgerSettlementBalanceId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SubledgerSettlementBalances_JournalEntries_SourceJournalEntryId')
                    ALTER TABLE [dbo].[SubledgerSettlementBalances] DROP CONSTRAINT [FK_SubledgerSettlementBalances_JournalEntries_SourceJournalEntryId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SubledgerSettlementBalances_FinancePostingEvents_SourcePostingEventId')
                    ALTER TABLE [dbo].[SubledgerSettlementBalances] DROP CONSTRAINT [FK_SubledgerSettlementBalances_FinancePostingEvents_SourcePostingEventId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SubledgerSettlementBalances_Tenants_TenantId')
                    ALTER TABLE [dbo].[SubledgerSettlementBalances] DROP CONSTRAINT [FK_SubledgerSettlementBalances_Tenants_TenantId];

                IF OBJECT_ID(N'[dbo].[SubledgerSettlementApplications]', N'U') IS NOT NULL
                    DROP TABLE [dbo].[SubledgerSettlementApplications];
                IF OBJECT_ID(N'[dbo].[SubledgerSettlementBalances]', N'U') IS NOT NULL
                    DROP TABLE [dbo].[SubledgerSettlementBalances];
                """);
        }
    }
}
