using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds controlled AP/AR advance application references and an immutable Sales credit-note
/// reversal trail. The unapplied balance table is rebuildable reporting state, not source GL.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260717110000_AddAdvanceSettlementAndCreditNoteReversalFoundation")]
public partial class AddAdvanceSettlementAndCreditNoteReversalFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'[dbo].[FinanceSettings]', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.FinanceSettings', N'SupplierAdvanceAccountId') IS NULL
                    ALTER TABLE [dbo].[FinanceSettings] ADD [SupplierAdvanceAccountId] uniqueidentifier NULL;
                IF COL_LENGTH(N'dbo.FinanceSettings', N'CustomerAdvanceAccountId') IS NULL
                    ALTER TABLE [dbo].[FinanceSettings] ADD [CustomerAdvanceAccountId] uniqueidentifier NULL;
            END;

            IF OBJECT_ID(N'[dbo].[VendorPayment]', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.VendorPayment', N'IsSupplierAdvance') IS NULL
                ALTER TABLE [dbo].[VendorPayment]
                    ADD [IsSupplierAdvance] bit NOT NULL
                        CONSTRAINT [DF_VendorPayment_IsSupplierAdvance] DEFAULT 0;

            IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.CustomerPayment', N'IsCustomerAdvance') IS NULL
                ALTER TABLE [dbo].[CustomerPayment]
                    ADD [IsCustomerAdvance] bit NOT NULL
                        CONSTRAINT [DF_CustomerPayment_IsCustomerAdvance] DEFAULT 0;

            IF OBJECT_ID(N'[dbo].[VendorPaymentAllocation]', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.VendorPaymentAllocation', N'ApplicationJournalEntryId') IS NULL
                    ALTER TABLE [dbo].[VendorPaymentAllocation] ADD [ApplicationJournalEntryId] uniqueidentifier NULL;
                IF COL_LENGTH(N'dbo.VendorPaymentAllocation', N'ApplicationPostingEventId') IS NULL
                    ALTER TABLE [dbo].[VendorPaymentAllocation] ADD [ApplicationPostingEventId] uniqueidentifier NULL;
            END;

            IF OBJECT_ID(N'[dbo].[PaymentAllocation]', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.PaymentAllocation', N'ApplicationJournalEntryId') IS NULL
                    ALTER TABLE [dbo].[PaymentAllocation] ADD [ApplicationJournalEntryId] uniqueidentifier NULL;
                IF COL_LENGTH(N'dbo.PaymentAllocation', N'ApplicationPostingEventId') IS NULL
                    ALTER TABLE [dbo].[PaymentAllocation] ADD [ApplicationPostingEventId] uniqueidentifier NULL;
            END;

            IF OBJECT_ID(N'[dbo].[CreditNotes]', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.CreditNotes', N'ReversalJournalEntryId') IS NULL
                    ALTER TABLE [dbo].[CreditNotes] ADD [ReversalJournalEntryId] uniqueidentifier NULL;
                IF COL_LENGTH(N'dbo.CreditNotes', N'ReversalPostingEventId') IS NULL
                    ALTER TABLE [dbo].[CreditNotes] ADD [ReversalPostingEventId] uniqueidentifier NULL;
                IF COL_LENGTH(N'dbo.CreditNotes', N'ReversedAt') IS NULL
                    ALTER TABLE [dbo].[CreditNotes] ADD [ReversedAt] datetime2 NULL;
                IF COL_LENGTH(N'dbo.CreditNotes', N'ReversalReason') IS NULL
                    ALTER TABLE [dbo].[CreditNotes] ADD [ReversalReason] nvarchar(500) NULL;
            END;

            IF OBJECT_ID(N'[dbo].[SubledgerUnappliedSettlementBalances]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[SubledgerUnappliedSettlementBalances]
                (
                    [Id] uniqueidentifier NOT NULL,
                    [TenantId] uniqueidentifier NOT NULL,
                    [SourceModule] nvarchar(10) NOT NULL,
                    [CounterpartyId] uniqueidentifier NOT NULL,
                    [SettlementSourceType] nvarchar(50) NOT NULL,
                    [SettlementSourceId] uniqueidentifier NOT NULL,
                    [SettlementSourceNumber] nvarchar(100) NOT NULL,
                    [Classification] nvarchar(50) NOT NULL,
                    [SettlementPostingEventId] uniqueidentifier NULL,
                    [SettlementJournalEntryId] uniqueidentifier NULL,
                    [SettlementDate] datetime2 NOT NULL,
                    [DocumentCurrencyCode] nvarchar(3) NOT NULL,
                    [FunctionalCurrencyCode] nvarchar(3) NOT NULL,
                    [OriginalAmount] decimal(18,2) NOT NULL,
                    [AppliedAmount] decimal(18,2) NOT NULL,
                    [UnappliedAmount] decimal(18,2) NOT NULL,
                    [RebuildBatchId] uniqueidentifier NOT NULL,
                    [LastRebuiltAt] datetime2 NOT NULL,
                    [HasDiagnostics] bit NOT NULL,
                    [DiagnosticFlags] nvarchar(1000) NULL,
                    [ReferenceNumber] nvarchar(50) NOT NULL,
                    [Status] nvarchar(50) NOT NULL,
                    [EffectiveDate] datetime2 NULL,
                    [ExpirationDate] datetime2 NULL,
                    [Metadata] nvarchar(max) NULL,
                    [Tags] nvarchar(500) NULL,
                    [Priority] int NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    [CreatedBy] nvarchar(max) NULL,
                    [UpdatedBy] nvarchar(max) NULL,
                    [CreatedById] uniqueidentifier NULL,
                    [LastModifiedById] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] nvarchar(max) NULL,
                    CONSTRAINT [PK_SubledgerUnappliedSettlementBalances] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_SubledgerUnappliedSettlementBalances_Tenants_TenantId]
                        FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]),
                    CONSTRAINT [FK_SubledgerUnappliedSettlementBalances_FinancePostingEvents_SettlementPostingEventId]
                        FOREIGN KEY ([SettlementPostingEventId]) REFERENCES [dbo].[FinancePostingEvents] ([Id]),
                    CONSTRAINT [FK_SubledgerUnappliedSettlementBalances_JournalEntries_SettlementJournalEntryId]
                        FOREIGN KEY ([SettlementJournalEntryId]) REFERENCES [dbo].[JournalEntries] ([Id])
                );
            END;

            IF OBJECT_ID(N'[dbo].[SubledgerUnappliedSettlementBalances]', N'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE [object_id] = OBJECT_ID(N'[dbo].[SubledgerUnappliedSettlementBalances]')
                      AND [name] = N'IX_SubledgerUnappliedSettlementBalances_TenantId_SourceModule_SettlementSourceType_SettlementSourceId')
                    CREATE UNIQUE INDEX [IX_SubledgerUnappliedSettlementBalances_TenantId_SourceModule_SettlementSourceType_SettlementSourceId]
                        ON [dbo].[SubledgerUnappliedSettlementBalances]
                            ([TenantId], [SourceModule], [SettlementSourceType], [SettlementSourceId])
                        WHERE [IsDeleted] = 0;

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE [object_id] = OBJECT_ID(N'[dbo].[SubledgerUnappliedSettlementBalances]')
                      AND [name] = N'IX_SubledgerUnappliedSettlementBalances_TenantId_SourceModule_CounterpartyId_SettlementDate')
                    CREATE INDEX [IX_SubledgerUnappliedSettlementBalances_TenantId_SourceModule_CounterpartyId_SettlementDate]
                        ON [dbo].[SubledgerUnappliedSettlementBalances]
                            ([TenantId], [SourceModule], [CounterpartyId], [SettlementDate]);

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE [object_id] = OBJECT_ID(N'[dbo].[SubledgerUnappliedSettlementBalances]')
                      AND [name] = N'IX_SubledgerUnappliedSettlementBalances_TenantId_SettlementPostingEventId')
                    CREATE INDEX [IX_SubledgerUnappliedSettlementBalances_TenantId_SettlementPostingEventId]
                        ON [dbo].[SubledgerUnappliedSettlementBalances]
                            ([TenantId], [SettlementPostingEventId]);

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE [object_id] = OBJECT_ID(N'[dbo].[SubledgerUnappliedSettlementBalances]')
                      AND [name] = N'IX_SubledgerUnappliedSettlementBalances_TenantId_SettlementJournalEntryId')
                    CREATE INDEX [IX_SubledgerUnappliedSettlementBalances_TenantId_SettlementJournalEntryId]
                        ON [dbo].[SubledgerUnappliedSettlementBalances]
                            ([TenantId], [SettlementJournalEntryId]);
            END;

            IF OBJECT_ID(N'[dbo].[VendorPaymentAllocation]', N'U') IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1 FROM sys.indexes
                   WHERE [object_id] = OBJECT_ID(N'[dbo].[VendorPaymentAllocation]')
                     AND [name] = N'IX_VendorPaymentAllocation_TenantId_ApplicationPostingEventId')
                CREATE INDEX [IX_VendorPaymentAllocation_TenantId_ApplicationPostingEventId]
                    ON [dbo].[VendorPaymentAllocation] ([TenantId], [ApplicationPostingEventId]);

            IF OBJECT_ID(N'[dbo].[PaymentAllocation]', N'U') IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1 FROM sys.indexes
                   WHERE [object_id] = OBJECT_ID(N'[dbo].[PaymentAllocation]')
                     AND [name] = N'IX_PaymentAllocation_TenantId_ApplicationPostingEventId')
                CREATE INDEX [IX_PaymentAllocation_TenantId_ApplicationPostingEventId]
                    ON [dbo].[PaymentAllocation] ([TenantId], [ApplicationPostingEventId]);

            IF OBJECT_ID(N'[dbo].[CreditNotes]', N'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE [object_id] = OBJECT_ID(N'[dbo].[CreditNotes]')
                      AND [name] = N'IX_CreditNotes_TenantId_ReversalJournalEntryId')
                    CREATE INDEX [IX_CreditNotes_TenantId_ReversalJournalEntryId]
                        ON [dbo].[CreditNotes] ([TenantId], [ReversalJournalEntryId]);
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE [object_id] = OBJECT_ID(N'[dbo].[CreditNotes]')
                      AND [name] = N'IX_CreditNotes_TenantId_ReversalPostingEventId')
                    CREATE INDEX [IX_CreditNotes_TenantId_ReversalPostingEventId]
                        ON [dbo].[CreditNotes] ([TenantId], [ReversalPostingEventId]);
            END;

            IF OBJECT_ID(N'[dbo].[FinanceSettings]', N'U') IS NOT NULL
               AND OBJECT_ID(N'[dbo].[Accounts]', N'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
                      AND [name] = N'FK_FinanceSettings_Accounts_SupplierAdvanceAccountId')
                    ALTER TABLE [dbo].[FinanceSettings]
                        ADD CONSTRAINT [FK_FinanceSettings_Accounts_SupplierAdvanceAccountId]
                        FOREIGN KEY ([SupplierAdvanceAccountId]) REFERENCES [dbo].[Accounts] ([Id]);

                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
                      AND [name] = N'FK_FinanceSettings_Accounts_CustomerAdvanceAccountId')
                    ALTER TABLE [dbo].[FinanceSettings]
                        ADD CONSTRAINT [FK_FinanceSettings_Accounts_CustomerAdvanceAccountId]
                        FOREIGN KEY ([CustomerAdvanceAccountId]) REFERENCES [dbo].[Accounts] ([Id]);
            END;

            IF OBJECT_ID(N'[dbo].[VendorPaymentAllocation]', N'U') IS NOT NULL
            BEGIN
                IF OBJECT_ID(N'[dbo].[JournalEntries]', N'U') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM sys.foreign_keys
                       WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[VendorPaymentAllocation]')
                         AND [name] = N'FK_VendorPaymentAllocation_JournalEntries_ApplicationJournalEntryId')
                    ALTER TABLE [dbo].[VendorPaymentAllocation]
                        ADD CONSTRAINT [FK_VendorPaymentAllocation_JournalEntries_ApplicationJournalEntryId]
                        FOREIGN KEY ([ApplicationJournalEntryId]) REFERENCES [dbo].[JournalEntries] ([Id]);

                IF OBJECT_ID(N'[dbo].[FinancePostingEvents]', N'U') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM sys.foreign_keys
                       WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[VendorPaymentAllocation]')
                         AND [name] = N'FK_VendorPaymentAllocation_FinancePostingEvents_ApplicationPostingEventId')
                    ALTER TABLE [dbo].[VendorPaymentAllocation]
                        ADD CONSTRAINT [FK_VendorPaymentAllocation_FinancePostingEvents_ApplicationPostingEventId]
                        FOREIGN KEY ([ApplicationPostingEventId]) REFERENCES [dbo].[FinancePostingEvents] ([Id]);
            END;

            IF OBJECT_ID(N'[dbo].[PaymentAllocation]', N'U') IS NOT NULL
            BEGIN
                IF OBJECT_ID(N'[dbo].[JournalEntries]', N'U') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM sys.foreign_keys
                       WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[PaymentAllocation]')
                         AND [name] = N'FK_PaymentAllocation_JournalEntries_ApplicationJournalEntryId')
                    ALTER TABLE [dbo].[PaymentAllocation]
                        ADD CONSTRAINT [FK_PaymentAllocation_JournalEntries_ApplicationJournalEntryId]
                        FOREIGN KEY ([ApplicationJournalEntryId]) REFERENCES [dbo].[JournalEntries] ([Id]);

                IF OBJECT_ID(N'[dbo].[FinancePostingEvents]', N'U') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM sys.foreign_keys
                       WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[PaymentAllocation]')
                         AND [name] = N'FK_PaymentAllocation_FinancePostingEvents_ApplicationPostingEventId')
                    ALTER TABLE [dbo].[PaymentAllocation]
                        ADD CONSTRAINT [FK_PaymentAllocation_FinancePostingEvents_ApplicationPostingEventId]
                        FOREIGN KEY ([ApplicationPostingEventId]) REFERENCES [dbo].[FinancePostingEvents] ([Id]);
            END;

            IF OBJECT_ID(N'[dbo].[CreditNotes]', N'U') IS NOT NULL
            BEGIN
                IF OBJECT_ID(N'[dbo].[JournalEntries]', N'U') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM sys.foreign_keys
                       WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[CreditNotes]')
                         AND [name] = N'FK_CreditNotes_JournalEntries_ReversalJournalEntryId')
                    ALTER TABLE [dbo].[CreditNotes]
                        ADD CONSTRAINT [FK_CreditNotes_JournalEntries_ReversalJournalEntryId]
                        FOREIGN KEY ([ReversalJournalEntryId]) REFERENCES [dbo].[JournalEntries] ([Id]);

                IF OBJECT_ID(N'[dbo].[FinancePostingEvents]', N'U') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM sys.foreign_keys
                       WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[CreditNotes]')
                         AND [name] = N'FK_CreditNotes_FinancePostingEvents_ReversalPostingEventId')
                    ALTER TABLE [dbo].[CreditNotes]
                        ADD CONSTRAINT [FK_CreditNotes_FinancePostingEvents_ReversalPostingEventId]
                        FOREIGN KEY ([ReversalPostingEventId]) REFERENCES [dbo].[FinancePostingEvents] ([Id]);
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SubledgerUnappliedSettlementBalances");
        migrationBuilder.DropForeignKey(name: "FK_FinanceSettings_Accounts_SupplierAdvanceAccountId", table: "FinanceSettings");
        migrationBuilder.DropForeignKey(name: "FK_FinanceSettings_Accounts_CustomerAdvanceAccountId", table: "FinanceSettings");
        migrationBuilder.DropForeignKey(name: "FK_VendorPaymentAllocation_JournalEntries_ApplicationJournalEntryId", table: "VendorPaymentAllocation");
        migrationBuilder.DropForeignKey(name: "FK_VendorPaymentAllocation_FinancePostingEvents_ApplicationPostingEventId", table: "VendorPaymentAllocation");
        migrationBuilder.DropForeignKey(name: "FK_PaymentAllocation_JournalEntries_ApplicationJournalEntryId", table: "PaymentAllocation");
        migrationBuilder.DropForeignKey(name: "FK_PaymentAllocation_FinancePostingEvents_ApplicationPostingEventId", table: "PaymentAllocation");
        migrationBuilder.DropForeignKey(name: "FK_CreditNotes_JournalEntries_ReversalJournalEntryId", table: "CreditNotes");
        migrationBuilder.DropForeignKey(name: "FK_CreditNotes_FinancePostingEvents_ReversalPostingEventId", table: "CreditNotes");
        migrationBuilder.DropIndex(name: "IX_VendorPaymentAllocation_TenantId_ApplicationPostingEventId", table: "VendorPaymentAllocation");
        migrationBuilder.DropIndex(name: "IX_PaymentAllocation_TenantId_ApplicationPostingEventId", table: "PaymentAllocation");
        migrationBuilder.DropIndex(name: "IX_CreditNotes_TenantId_ReversalJournalEntryId", table: "CreditNotes");
        migrationBuilder.DropIndex(name: "IX_CreditNotes_TenantId_ReversalPostingEventId", table: "CreditNotes");
        migrationBuilder.DropColumn(name: "SupplierAdvanceAccountId", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "CustomerAdvanceAccountId", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "IsSupplierAdvance", table: "VendorPayment");
        migrationBuilder.DropColumn(name: "IsCustomerAdvance", table: "CustomerPayment");
        migrationBuilder.DropColumn(name: "ApplicationJournalEntryId", table: "VendorPaymentAllocation");
        migrationBuilder.DropColumn(name: "ApplicationPostingEventId", table: "VendorPaymentAllocation");
        migrationBuilder.DropColumn(name: "ApplicationJournalEntryId", table: "PaymentAllocation");
        migrationBuilder.DropColumn(name: "ApplicationPostingEventId", table: "PaymentAllocation");
        migrationBuilder.DropColumn(name: "ReversalJournalEntryId", table: "CreditNotes");
        migrationBuilder.DropColumn(name: "ReversalPostingEventId", table: "CreditNotes");
        migrationBuilder.DropColumn(name: "ReversedAt", table: "CreditNotes");
        migrationBuilder.DropColumn(name: "ReversalReason", table: "CreditNotes");
    }
}
