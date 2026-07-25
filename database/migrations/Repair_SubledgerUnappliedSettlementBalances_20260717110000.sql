SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF COL_LENGTH('dbo.FinanceSettings', 'SupplierAdvanceAccountId') IS NULL
    ALTER TABLE dbo.FinanceSettings ADD SupplierAdvanceAccountId uniqueidentifier NULL;

IF COL_LENGTH('dbo.FinanceSettings', 'CustomerAdvanceAccountId') IS NULL
    ALTER TABLE dbo.FinanceSettings ADD CustomerAdvanceAccountId uniqueidentifier NULL;

IF COL_LENGTH('dbo.VendorPayment', 'IsSupplierAdvance') IS NULL
    ALTER TABLE dbo.VendorPayment
        ADD IsSupplierAdvance bit NOT NULL
            CONSTRAINT DF_VendorPayment_IsSupplierAdvance DEFAULT (CONVERT(bit, 0)) WITH VALUES;

IF COL_LENGTH('dbo.CustomerPayment', 'IsCustomerAdvance') IS NULL
    ALTER TABLE dbo.CustomerPayment
        ADD IsCustomerAdvance bit NOT NULL
            CONSTRAINT DF_CustomerPayment_IsCustomerAdvance DEFAULT (CONVERT(bit, 0)) WITH VALUES;

IF COL_LENGTH('dbo.VendorPaymentAllocation', 'ApplicationJournalEntryId') IS NULL
    ALTER TABLE dbo.VendorPaymentAllocation ADD ApplicationJournalEntryId uniqueidentifier NULL;

IF COL_LENGTH('dbo.VendorPaymentAllocation', 'ApplicationPostingEventId') IS NULL
    ALTER TABLE dbo.VendorPaymentAllocation ADD ApplicationPostingEventId uniqueidentifier NULL;

IF COL_LENGTH('dbo.PaymentAllocation', 'ApplicationJournalEntryId') IS NULL
    ALTER TABLE dbo.PaymentAllocation ADD ApplicationJournalEntryId uniqueidentifier NULL;

IF COL_LENGTH('dbo.PaymentAllocation', 'ApplicationPostingEventId') IS NULL
    ALTER TABLE dbo.PaymentAllocation ADD ApplicationPostingEventId uniqueidentifier NULL;

IF COL_LENGTH('dbo.CreditNotes', 'ReversalJournalEntryId') IS NULL
    ALTER TABLE dbo.CreditNotes ADD ReversalJournalEntryId uniqueidentifier NULL;

IF COL_LENGTH('dbo.CreditNotes', 'ReversalPostingEventId') IS NULL
    ALTER TABLE dbo.CreditNotes ADD ReversalPostingEventId uniqueidentifier NULL;

IF COL_LENGTH('dbo.CreditNotes', 'ReversedAt') IS NULL
    ALTER TABLE dbo.CreditNotes ADD ReversedAt datetime2 NULL;

IF COL_LENGTH('dbo.CreditNotes', 'ReversalReason') IS NULL
    ALTER TABLE dbo.CreditNotes ADD ReversalReason nvarchar(500) NULL;

IF OBJECT_ID(N'dbo.SubledgerUnappliedSettlementBalances', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SubledgerUnappliedSettlementBalances
    (
        Id uniqueidentifier NOT NULL,
        TenantId uniqueidentifier NOT NULL,
        SourceModule nvarchar(10) NOT NULL,
        CounterpartyId uniqueidentifier NOT NULL,
        SettlementSourceType nvarchar(50) NOT NULL,
        SettlementSourceId uniqueidentifier NOT NULL,
        SettlementSourceNumber nvarchar(100) NOT NULL,
        Classification nvarchar(50) NOT NULL,
        SettlementPostingEventId uniqueidentifier NULL,
        SettlementJournalEntryId uniqueidentifier NULL,
        SettlementDate datetime2 NOT NULL,
        DocumentCurrencyCode nvarchar(3) NOT NULL,
        FunctionalCurrencyCode nvarchar(3) NOT NULL,
        OriginalAmount decimal(18,2) NOT NULL,
        AppliedAmount decimal(18,2) NOT NULL,
        UnappliedAmount decimal(18,2) NOT NULL,
        RebuildBatchId uniqueidentifier NOT NULL,
        LastRebuiltAt datetime2 NOT NULL,
        HasDiagnostics bit NOT NULL,
        DiagnosticFlags nvarchar(1000) NULL,
        ReferenceNumber nvarchar(50) NOT NULL,
        Status nvarchar(50) NOT NULL,
        EffectiveDate datetime2 NULL,
        ExpirationDate datetime2 NULL,
        Metadata nvarchar(max) NULL,
        Tags nvarchar(500) NULL,
        Priority int NOT NULL,
        CreatedAt datetime2 NOT NULL,
        UpdatedAt datetime2 NULL,
        CreatedBy nvarchar(max) NULL,
        UpdatedBy nvarchar(max) NULL,
        CreatedById uniqueidentifier NULL,
        LastModifiedById uniqueidentifier NULL,
        IsDeleted bit NOT NULL,
        DeletedAt datetime2 NULL,
        DeletedBy nvarchar(max) NULL,
        CONSTRAINT PK_SubledgerUnappliedSettlementBalances PRIMARY KEY (Id)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubledgerUnappliedSettlementBalances_TenantId' AND object_id = OBJECT_ID(N'dbo.SubledgerUnappliedSettlementBalances'))
    CREATE INDEX IX_SubledgerUnappliedSettlementBalances_TenantId
        ON dbo.SubledgerUnappliedSettlementBalances (TenantId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubledgerUnappliedSettlementBalances_TenantId_SourceModule_SettlementSourceType_SettlementSourceId' AND object_id = OBJECT_ID(N'dbo.SubledgerUnappliedSettlementBalances'))
    CREATE UNIQUE INDEX IX_SubledgerUnappliedSettlementBalances_TenantId_SourceModule_SettlementSourceType_SettlementSourceId
        ON dbo.SubledgerUnappliedSettlementBalances (TenantId, SourceModule, SettlementSourceType, SettlementSourceId)
        WHERE IsDeleted = 0;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubledgerUnappliedSettlementBalances_TenantId_SourceModule_CounterpartyId_SettlementDate' AND object_id = OBJECT_ID(N'dbo.SubledgerUnappliedSettlementBalances'))
    CREATE INDEX IX_SubledgerUnappliedSettlementBalances_TenantId_SourceModule_CounterpartyId_SettlementDate
        ON dbo.SubledgerUnappliedSettlementBalances (TenantId, SourceModule, CounterpartyId, SettlementDate);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubledgerUnappliedSettlementBalances_TenantId_SettlementPostingEventId' AND object_id = OBJECT_ID(N'dbo.SubledgerUnappliedSettlementBalances'))
    CREATE INDEX IX_SubledgerUnappliedSettlementBalances_TenantId_SettlementPostingEventId
        ON dbo.SubledgerUnappliedSettlementBalances (TenantId, SettlementPostingEventId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubledgerUnappliedSettlementBalances_TenantId_SettlementJournalEntryId' AND object_id = OBJECT_ID(N'dbo.SubledgerUnappliedSettlementBalances'))
    CREATE INDEX IX_SubledgerUnappliedSettlementBalances_TenantId_SettlementJournalEntryId
        ON dbo.SubledgerUnappliedSettlementBalances (TenantId, SettlementJournalEntryId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VendorPaymentAllocation_TenantId_ApplicationPostingEventId' AND object_id = OBJECT_ID(N'dbo.VendorPaymentAllocation'))
    CREATE INDEX IX_VendorPaymentAllocation_TenantId_ApplicationPostingEventId
        ON dbo.VendorPaymentAllocation (TenantId, ApplicationPostingEventId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PaymentAllocation_TenantId_ApplicationPostingEventId' AND object_id = OBJECT_ID(N'dbo.PaymentAllocation'))
    CREATE INDEX IX_PaymentAllocation_TenantId_ApplicationPostingEventId
        ON dbo.PaymentAllocation (TenantId, ApplicationPostingEventId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CreditNotes_TenantId_ReversalJournalEntryId' AND object_id = OBJECT_ID(N'dbo.CreditNotes'))
    CREATE INDEX IX_CreditNotes_TenantId_ReversalJournalEntryId
        ON dbo.CreditNotes (TenantId, ReversalJournalEntryId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CreditNotes_TenantId_ReversalPostingEventId' AND object_id = OBJECT_ID(N'dbo.CreditNotes'))
    CREATE INDEX IX_CreditNotes_TenantId_ReversalPostingEventId
        ON dbo.CreditNotes (TenantId, ReversalPostingEventId);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FinanceSettings_Accounts_SupplierAdvanceAccountId')
    ALTER TABLE dbo.FinanceSettings WITH CHECK ADD CONSTRAINT FK_FinanceSettings_Accounts_SupplierAdvanceAccountId
        FOREIGN KEY (SupplierAdvanceAccountId) REFERENCES dbo.Accounts (Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FinanceSettings_Accounts_CustomerAdvanceAccountId')
    ALTER TABLE dbo.FinanceSettings WITH CHECK ADD CONSTRAINT FK_FinanceSettings_Accounts_CustomerAdvanceAccountId
        FOREIGN KEY (CustomerAdvanceAccountId) REFERENCES dbo.Accounts (Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_VendorPaymentAllocation_JournalEntries_ApplicationJournalEntryId')
    ALTER TABLE dbo.VendorPaymentAllocation WITH CHECK ADD CONSTRAINT FK_VendorPaymentAllocation_JournalEntries_ApplicationJournalEntryId
        FOREIGN KEY (ApplicationJournalEntryId) REFERENCES dbo.JournalEntries (Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_VendorPaymentAllocation_FinancePostingEvents_ApplicationPostingEventId')
    ALTER TABLE dbo.VendorPaymentAllocation WITH CHECK ADD CONSTRAINT FK_VendorPaymentAllocation_FinancePostingEvents_ApplicationPostingEventId
        FOREIGN KEY (ApplicationPostingEventId) REFERENCES dbo.FinancePostingEvents (Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PaymentAllocation_JournalEntries_ApplicationJournalEntryId')
    ALTER TABLE dbo.PaymentAllocation WITH CHECK ADD CONSTRAINT FK_PaymentAllocation_JournalEntries_ApplicationJournalEntryId
        FOREIGN KEY (ApplicationJournalEntryId) REFERENCES dbo.JournalEntries (Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PaymentAllocation_FinancePostingEvents_ApplicationPostingEventId')
    ALTER TABLE dbo.PaymentAllocation WITH CHECK ADD CONSTRAINT FK_PaymentAllocation_FinancePostingEvents_ApplicationPostingEventId
        FOREIGN KEY (ApplicationPostingEventId) REFERENCES dbo.FinancePostingEvents (Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CreditNotes_JournalEntries_ReversalJournalEntryId')
    ALTER TABLE dbo.CreditNotes WITH CHECK ADD CONSTRAINT FK_CreditNotes_JournalEntries_ReversalJournalEntryId
        FOREIGN KEY (ReversalJournalEntryId) REFERENCES dbo.JournalEntries (Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CreditNotes_FinancePostingEvents_ReversalPostingEventId')
    ALTER TABLE dbo.CreditNotes WITH CHECK ADD CONSTRAINT FK_CreditNotes_FinancePostingEvents_ReversalPostingEventId
        FOREIGN KEY (ReversalPostingEventId) REFERENCES dbo.FinancePostingEvents (Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SubledgerUnappliedSettlementBalances_Tenants_TenantId')
    ALTER TABLE dbo.SubledgerUnappliedSettlementBalances WITH CHECK ADD CONSTRAINT FK_SubledgerUnappliedSettlementBalances_Tenants_TenantId
        FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SubledgerUnappliedSettlementBalances_FinancePostingEvents_SettlementPostingEventId')
    ALTER TABLE dbo.SubledgerUnappliedSettlementBalances WITH CHECK ADD CONSTRAINT FK_SubledgerUnappliedSettlementBalances_FinancePostingEvents_SettlementPostingEventId
        FOREIGN KEY (SettlementPostingEventId) REFERENCES dbo.FinancePostingEvents (Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SubledgerUnappliedSettlementBalances_JournalEntries_SettlementJournalEntryId')
    ALTER TABLE dbo.SubledgerUnappliedSettlementBalances WITH CHECK ADD CONSTRAINT FK_SubledgerUnappliedSettlementBalances_JournalEntries_SettlementJournalEntryId
        FOREIGN KEY (SettlementJournalEntryId) REFERENCES dbo.JournalEntries (Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20260717110000_AddAdvanceSettlementAndCreditNoteReversalFoundation')
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20260717110000_AddAdvanceSettlementAndCreditNoteReversalFoundation', N'8.0.0');

COMMIT TRANSACTION;
