BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '01424a6f-cd1a-417d-a470-d94111460dc7';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '40bf4967-2216-4121-9e5b-d518c4c13f65';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '47ffa269-7878-4916-a25a-2fbfb327c395';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '816673e8-7979-45a4-854f-c8c008795a8f';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'a5d7828a-429f-412e-a135-e0c15a224e2f';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'b2b5e343-1da3-470b-a9d6-881a0bd3f611';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'd9d20505-1ff1-4048-8856-cb9066c16588';
SELECT @@ROWCOUNT;

GO

ALTER TABLE [Tenants] ADD [BaseCurrency] nvarchar(3) NOT NULL DEFAULT N'';
GO

ALTER TABLE [Tenants] ADD [BaseCurrencyName] nvarchar(50) NULL;
GO

ALTER TABLE [Tenants] ADD [CurrencyDecimalPlaces] int NOT NULL DEFAULT 0;
GO

ALTER TABLE [Tenants] ADD [CurrencySymbol] nvarchar(5) NULL;
GO

ALTER TABLE [BusinessPartnerRegistrationStatusHistories] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
GO

ALTER TABLE [BusinessPartnerRegistrationStatusHistories] ADD [CreatedBy] nvarchar(max) NULL;
GO

ALTER TABLE [BusinessPartnerRegistrationStatusHistories] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [BusinessPartnerRegistrationStatusHistories] ADD [DeletedAt] datetime2 NULL;
GO

ALTER TABLE [BusinessPartnerRegistrationStatusHistories] ADD [DeletedBy] nvarchar(max) NULL;
GO

ALTER TABLE [BusinessPartnerRegistrationStatusHistories] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [BusinessPartnerRegistrationStatusHistories] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [BusinessPartnerRegistrationStatusHistories] ADD [UpdatedAt] datetime2 NULL;
GO

ALTER TABLE [BusinessPartnerRegistrationStatusHistories] ADD [UpdatedBy] nvarchar(max) NULL;
GO

CREATE TABLE [Accounts] (
    [Id] uniqueidentifier NOT NULL,
    [AccountCode] nvarchar(50) NOT NULL,
    [AccountNumber] nvarchar(100) NOT NULL,
    [AccountName] nvarchar(200) NOT NULL,
    [AccountType] int NOT NULL,
    [AccountCategory] nvarchar(100) NULL,
    [AccountSubCategory] nvarchar(100) NULL,
    [Description] nvarchar(1000) NULL,
    [ParentAccountId] uniqueidentifier NULL,
    [IsSegmented] bit NOT NULL,
    [CurrencyCode] nvarchar(3) NOT NULL,
    [IsMultiCurrency] bit NOT NULL,
    [IsIFRSClassified] bit NOT NULL,
    [IsBaseClassified] bit NOT NULL,
    [IsLocalClassified] bit NOT NULL,
    [IFRSLineItem] nvarchar(100) NULL,
    [BaseLineItem] nvarchar(100) NULL,
    [LocalLineItem] nvarchar(100) NULL,
    [AllowDirectPosting] bit NOT NULL,
    [IsControlAccount] bit NOT NULL,
    [RequireDepartmentCode] bit NOT NULL,
    [RequireProjectCode] bit NOT NULL,
    [BudgetTrackingEnabled] bit NOT NULL,
    [Status] int NOT NULL,
    [Balance] decimal(18,4) NOT NULL,
    [DebitBalance] decimal(18,4) NOT NULL,
    [CreditBalance] decimal(18,4) NOT NULL,
    [OpeningBalance] decimal(18,4) NOT NULL,
    [LastTransactionDate] datetime2 NULL,
    [EstateModuleLinkId] uniqueidentifier NULL,
    [PayrollModuleLinkId] uniqueidentifier NULL,
    [ProcurementModuleLinkId] uniqueidentifier NULL,
    [TaxReportingCategory] nvarchar(50) NULL,
    [CashFlowClassification] nvarchar(50) NULL,
    [IsSystemAccount] bit NOT NULL,
    [InactivatedDate] datetime2 NULL,
    [InactivationReason] nvarchar(500) NULL,
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
    [ReferenceNumber] nvarchar(50) NOT NULL,
    [EffectiveDate] datetime2 NULL,
    [ExpirationDate] datetime2 NULL,
    [Metadata] nvarchar(max) NULL,
    [Tags] nvarchar(500) NULL,
    [Priority] int NOT NULL,
    CONSTRAINT [PK_Accounts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Accounts_Accounts_ParentAccountId] FOREIGN KEY ([ParentAccountId]) REFERENCES [Accounts] ([Id]),
    CONSTRAINT [FK_Accounts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [AccountSegmentStructures] (
    [Id] uniqueidentifier NOT NULL,
    [SegmentName] nvarchar(100) NOT NULL,
    [SegmentCode] nvarchar(20) NOT NULL,
    [SegmentPosition] int NOT NULL,
    [SegmentLength] int NOT NULL,
    [DataType] nvarchar(20) NOT NULL,
    [SeparatorCharacter] nvarchar(1) NULL,
    [LookupTableRequired] bit NOT NULL,
    [IsMandatory] bit NOT NULL,
    [IsReportingDimension] bit NOT NULL,
    [IsNaturalAccount] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [Description] nvarchar(500) NULL,
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
    CONSTRAINT [PK_AccountSegmentStructures] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AccountSegmentStructures_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ExchangeRates] (
    [Id] uniqueidentifier NOT NULL,
    [BaseCurrencyCode] nvarchar(3) NOT NULL,
    [TargetCurrencyCode] nvarchar(3) NOT NULL,
    [Rate] decimal(18,4) NOT NULL,
    [InverseRate] decimal(18,4) NOT NULL,
    [EffectiveDate] datetime2 NOT NULL,
    [EndDate] datetime2 NULL,
    [RateType] int NOT NULL,
    [IsActive] bit NOT NULL,
    [Priority] int NOT NULL,
    [RateSource] nvarchar(100) NOT NULL,
    [IsManualEntry] bit NOT NULL,
    [APIEndpoint] nvarchar(500) NULL,
    [APIResponseMetadata] nvarchar(1000) NULL,
    [HasBeenUsedInTransactions] bit NOT NULL,
    [TransactionCount] int NOT NULL,
    [FirstUsedDate] datetime2 NULL,
    [LastUsedDate] datetime2 NULL,
    [RateChangePercentage] decimal(18,4) NULL,
    [RateChangeAmount] decimal(18,2) NULL,
    [PreviousRateId] uniqueidentifier NULL,
    [ExceedsVarianceThreshold] bit NOT NULL,
    [ApprovalStatus] int NOT NULL,
    [ApprovedByUserId] uniqueidentifier NULL,
    [ApprovalDate] datetime2 NULL,
    [Comments] nvarchar(1000) NULL,
    [CreatedByUserId] uniqueidentifier NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    [ModifiedByUserId] uniqueidentifier NULL,
    [ModifiedDate] datetime2 NULL,
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
    [ReferenceNumber] nvarchar(50) NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [ExpirationDate] datetime2 NULL,
    [Metadata] nvarchar(max) NULL,
    [Tags] nvarchar(500) NULL,
    CONSTRAINT [PK_ExchangeRates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ExchangeRates_ExchangeRates_PreviousRateId] FOREIGN KEY ([PreviousRateId]) REFERENCES [ExchangeRates] ([Id]),
    CONSTRAINT [FK_ExchangeRates_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [AccountCurrencyLinks] (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [LinkedCurrencyCode] nvarchar(3) NOT NULL,
    [RevaluationRequired] bit NOT NULL,
    [RevaluationFrequency] int NOT NULL,
    [TransactionRateType] nvarchar(20) NOT NULL,
    [RevaluationRateType] nvarchar(20) NOT NULL,
    [IsActive] bit NOT NULL,
    [EffectiveDate] datetime2 NOT NULL,
    [EffectiveEndDate] datetime2 NULL,
    [InactivationReason] nvarchar(500) NULL,
    [HasTransactionHistory] bit NOT NULL,
    [TransactionCount] int NOT NULL,
    [FirstTransactionDate] datetime2 NULL,
    [LastTransactionDate] datetime2 NULL,
    [ForeignCurrencyBalance] decimal(18,4) NOT NULL,
    [BaseCurrencyEquivalent] decimal(18,4) NOT NULL,
    [CurrentExchangeRate] decimal(18,4) NOT NULL,
    [RateEffectiveDate] datetime2 NULL,
    [LastRevaluationDate] datetime2 NULL,
    [LastRevaluationAdjustment] decimal(18,4) NOT NULL,
    [CumulativeRevaluationAdjustment] decimal(18,4) NOT NULL,
    [CreatedByUserId] uniqueidentifier NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    [ModifiedByUserId] uniqueidentifier NULL,
    [ModifiedDate] datetime2 NULL,
    [Notes] nvarchar(1000) NULL,
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
    [ReferenceNumber] nvarchar(50) NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [ExpirationDate] datetime2 NULL,
    [Metadata] nvarchar(max) NULL,
    [Tags] nvarchar(500) NULL,
    [Priority] int NOT NULL,
    CONSTRAINT [PK_AccountCurrencyLinks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AccountCurrencyLinks_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AccountCurrencyLinks_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [SegmentLookupValues] (
    [Id] uniqueidentifier NOT NULL,
    [SegmentStructureId] uniqueidentifier NOT NULL,
    [SegmentValue] nvarchar(10) NOT NULL,
    [Description] nvarchar(200) NOT NULL,
    [ParentValueId] uniqueidentifier NULL,
    [EffectiveDate] datetime2 NOT NULL,
    [ExpiryDate] datetime2 NULL,
    [IsActive] bit NOT NULL,
    [DisplayOrder] int NOT NULL,
    [Notes] nvarchar(1000) NULL,
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
    CONSTRAINT [PK_SegmentLookupValues] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SegmentLookupValues_AccountSegmentStructures_SegmentStructureId] FOREIGN KEY ([SegmentStructureId]) REFERENCES [AccountSegmentStructures] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SegmentLookupValues_SegmentLookupValues_ParentValueId] FOREIGN KEY ([ParentValueId]) REFERENCES [SegmentLookupValues] ([Id]),
    CONSTRAINT [FK_SegmentLookupValues_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [AccountSegmentValues] (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [SegmentStructureId] uniqueidentifier NOT NULL,
    [SegmentValue] nvarchar(10) NOT NULL,
    [SegmentLookupValueId] uniqueidentifier NULL,
    [SegmentValueDescription] nvarchar(200) NULL,
    [SegmentPosition] int NOT NULL,
    [IsLocked] bit NOT NULL,
    [EffectiveDate] datetime2 NOT NULL,
    [EndDate] datetime2 NULL,
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
    CONSTRAINT [PK_AccountSegmentValues] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AccountSegmentValues_AccountSegmentStructures_SegmentStructureId] FOREIGN KEY ([SegmentStructureId]) REFERENCES [AccountSegmentStructures] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AccountSegmentValues_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AccountSegmentValues_SegmentLookupValues_SegmentLookupValueId] FOREIGN KEY ([SegmentLookupValueId]) REFERENCES [SegmentLookupValues] ([Id]),
    CONSTRAINT [FK_AccountSegmentValues_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [AccountBalances] (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [FiscalPeriodId] uniqueidentifier NOT NULL,
    [BookClassification] nvarchar(20) NOT NULL,
    [Currency] nvarchar(3) NULL,
    [OpeningBalance] decimal(18,4) NOT NULL,
    [OpeningBalanceType] nvarchar(2) NOT NULL,
    [PeriodDebits] decimal(18,4) NOT NULL,
    [PeriodCredits] decimal(18,4) NOT NULL,
    [PeriodNetMovement] decimal(18,4) NOT NULL,
    [ClosingBalance] decimal(18,4) NOT NULL,
    [ClosingBalanceType] nvarchar(2) NOT NULL,
    [YearToDateDebits] decimal(18,4) NOT NULL,
    [YearToDateCredits] decimal(18,4) NOT NULL,
    [YearToDateNetMovement] decimal(18,4) NOT NULL,
    [SegmentString] nvarchar(200) NULL,
    [DepartmentSegment] nvarchar(20) NULL,
    [CostCenterSegment] nvarchar(20) NULL,
    [ProjectSegment] nvarchar(20) NULL,
    [LocationSegment] nvarchar(20) NULL,
    [ExchangeRate] decimal(18,4) NULL,
    [BaseCurrencyEquivalent] decimal(18,4) NULL,
    [UnrealizedGainLoss] decimal(18,4) NULL,
    [TransactionCount] int NOT NULL,
    [LastTransactionDate] datetime2 NULL,
    [LastTransactionUserId] uniqueidentifier NULL,
    [LastUpdated] datetime2 NOT NULL,
    [IsReconciled] bit NOT NULL,
    [LastReconciledDate] datetime2 NULL,
    [ReconciliationDiscrepancy] decimal(18,4) NULL,
    [IsLocked] bit NOT NULL,
    [LockedDate] datetime2 NULL,
    [LockedByUserId] uniqueidentifier NULL,
    [HasActivity] bit NOT NULL,
    [IsZeroBalance] bit NOT NULL,
    [IsNegativeBalance] bit NOT NULL,
    [Notes] nvarchar(1000) NULL,
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
    [ReferenceNumber] nvarchar(50) NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [EffectiveDate] datetime2 NULL,
    [ExpirationDate] datetime2 NULL,
    [Metadata] nvarchar(max) NULL,
    [Tags] nvarchar(500) NULL,
    [Priority] int NOT NULL,
    CONSTRAINT [PK_AccountBalances] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AccountBalances_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AccountBalances_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [AccountTransactions] (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [JournalEntryId] uniqueidentifier NOT NULL,
    [TransactionDate] datetime2 NOT NULL,
    [Description] nvarchar(500) NULL,
    [DebitAmount] decimal(18,2) NOT NULL,
    [CreditAmount] decimal(18,2) NOT NULL,
    [TransactionCurrency] nvarchar(3) NULL,
    [ForeignCurrencyAmount] decimal(18,2) NULL,
    [ExchangeRate] decimal(18,4) NULL,
    [ExchangeRateSource] nvarchar(100) NULL,
    [ExchangeRateDate] datetime2 NULL,
    [SourceModule] nvarchar(50) NULL,
    [SourceDocumentId] uniqueidentifier NULL,
    [SourceDocumentType] nvarchar(100) NULL,
    [SourceReferenceNumber] nvarchar(100) NULL,
    [BookClassification] nvarchar(20) NOT NULL,
    [FiscalPeriodId] uniqueidentifier NOT NULL,
    [PostedDate] datetime2 NULL,
    [PostingStatus] nvarchar(20) NOT NULL,
    [IsReversed] bit NOT NULL,
    [ReversalDate] datetime2 NULL,
    [ReversalTransactionId] uniqueidentifier NULL,
    [OriginalTransactionId] uniqueidentifier NULL,
    [ReversalType] nvarchar(20) NULL,
    [ReversalReason] nvarchar(500) NULL,
    [SegmentString] nvarchar(200) NULL,
    [IsRevaluationEntry] bit NOT NULL,
    [RevaluationBatchNumber] nvarchar(50) NULL,
    [RevaluationType] nvarchar(20) NULL,
    [LineNumber] int NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [TransactionTag] nvarchar(50) NULL,
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
    [ReferenceNumber] nvarchar(50) NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [EffectiveDate] datetime2 NULL,
    [ExpirationDate] datetime2 NULL,
    [Metadata] nvarchar(max) NULL,
    [Tags] nvarchar(500) NULL,
    [Priority] int NOT NULL,
    CONSTRAINT [PK_AccountTransactions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AccountTransactions_AccountTransactions_OriginalTransactionId] FOREIGN KEY ([OriginalTransactionId]) REFERENCES [AccountTransactions] ([Id]),
    CONSTRAINT [FK_AccountTransactions_AccountTransactions_ReversalTransactionId] FOREIGN KEY ([ReversalTransactionId]) REFERENCES [AccountTransactions] ([Id]),
    CONSTRAINT [FK_AccountTransactions_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AccountTransactions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [FiscalPeriods] (
    [Id] uniqueidentifier NOT NULL,
    [FiscalYearId] uniqueidentifier NOT NULL,
    [PeriodName] nvarchar(50) NOT NULL,
    [PeriodCode] nvarchar(20) NOT NULL,
    [PeriodNumber] int NOT NULL,
    [PeriodType] nvarchar(20) NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [PeriodDays] int NOT NULL,
    [PeriodStatus] nvarchar(20) NOT NULL,
    [IsOpen] bit NOT NULL,
    [IsLocked] bit NOT NULL,
    [LockedDate] datetime2 NULL,
    [LockedByUserId] uniqueidentifier NULL,
    [LockReason] nvarchar(500) NULL,
    [IsCloseInitiated] bit NOT NULL,
    [CloseInitiatedDate] datetime2 NULL,
    [CloseInitiatedByUserId] uniqueidentifier NULL,
    [IsClosed] bit NOT NULL,
    [ClosedDate] datetime2 NULL,
    [ClosedByUserId] uniqueidentifier NULL,
    [TrialBalanceValidated] bit NOT NULL,
    [TrialBalanceValidatedDate] datetime2 NULL,
    [BankReconciliationComplete] bit NOT NULL,
    [BankReconciliationCompletedDate] datetime2 NULL,
    [CurrencyRevaluationComplete] bit NOT NULL,
    [CurrencyRevaluationDate] datetime2 NULL,
    [DepreciationComplete] bit NOT NULL,
    [DepreciationCompletedDate] datetime2 NULL,
    [InventoryValuationComplete] bit NOT NULL,
    [InventoryValuationDate] datetime2 NULL,
    [AccrualsComplete] bit NOT NULL,
    [AccrualsCompletedDate] datetime2 NULL,
    [HasBeenReopened] bit NOT NULL,
    [ReopenCount] int NOT NULL,
    [LastReopenedDate] datetime2 NULL,
    [LastReopenedByUserId] uniqueidentifier NULL,
    [ReopenReason] nvarchar(1000) NULL,
    [IsYearEnd] bit NOT NULL,
    [YearEndCloseComplete] bit NOT NULL,
    [YearEndCloseDate] datetime2 NULL,
    [YearEndClosedByUserId] uniqueidentifier NULL,
    [TotalJournalEntries] int NOT NULL,
    [TotalTransactionLines] int NOT NULL,
    [TotalDebits] decimal(18,2) NOT NULL,
    [TotalCredits] decimal(18,2) NOT NULL,
    [BalanceDifference] decimal(18,4) NOT NULL,
    [ClosingNotes] nvarchar(2000) NULL,
    [AllowBackdating] bit NOT NULL,
    [AllowFutureDating] bit NOT NULL,
    [MaxTransactionAmount] decimal(18,2) NULL,
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
    [ReferenceNumber] nvarchar(50) NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [EffectiveDate] datetime2 NULL,
    [ExpirationDate] datetime2 NULL,
    [Metadata] nvarchar(max) NULL,
    [Tags] nvarchar(500) NULL,
    [Priority] int NOT NULL,
    CONSTRAINT [PK_FiscalPeriods] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FiscalPeriods_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [JournalEntries] (
    [Id] uniqueidentifier NOT NULL,
    [JournalEntryNumber] nvarchar(50) NOT NULL,
    [JournalType] nvarchar(50) NOT NULL,
    [EntryDate] datetime2 NOT NULL,
    [Description] nvarchar(500) NOT NULL,
    [ReferenceNumber] nvarchar(100) NULL,
    [SourceModule] nvarchar(50) NULL,
    [SourceDocumentId] uniqueidentifier NULL,
    [SourceDocumentType] nvarchar(100) NULL,
    [TotalDebitAmount] decimal(18,2) NOT NULL,
    [TotalCreditAmount] decimal(18,2) NOT NULL,
    [BalanceDifference] decimal(18,4) NOT NULL,
    [IsBalanced] bit NOT NULL,
    [IsMultiCurrency] bit NOT NULL,
    [PrimaryCurrency] nvarchar(3) NULL,
    [BookClassification] nvarchar(20) NOT NULL,
    [FiscalPeriodId] uniqueidentifier NOT NULL,
    [PostingDate] datetime2 NULL,
    [PostedByUserId] uniqueidentifier NULL,
    [PostingStatus] nvarchar(50) NOT NULL,
    [RequiresApproval] bit NOT NULL,
    [ApprovalStatus] nvarchar(50) NULL,
    [ApprovalWorkflowId] nvarchar(100) NULL,
    [ApprovedByUserId] uniqueidentifier NULL,
    [ApprovedDate] datetime2 NULL,
    [RejectionReason] nvarchar(1000) NULL,
    [IsReversed] bit NOT NULL,
    [ReversalDate] datetime2 NULL,
    [ReversalJournalEntryId] uniqueidentifier NULL,
    [OriginalJournalEntryId] uniqueidentifier NULL,
    [ReversalType] nvarchar(20) NULL,
    [ReversalReason] nvarchar(500) NULL,
    [IsRecurring] bit NOT NULL,
    [RecurringTemplateId] uniqueidentifier NULL,
    [RecurrenceFrequency] nvarchar(20) NULL,
    [NextRecurrenceDate] datetime2 NULL,
    [IsRevaluationEntry] bit NOT NULL,
    [RevaluationBatchNumber] nvarchar(50) NULL,
    [RevaluationType] nvarchar(20) NULL,
    [IsAutoReversalEntry] bit NOT NULL,
    [IsImported] bit NOT NULL,
    [ImportBatchReference] nvarchar(100) NULL,
    [Notes] nvarchar(2000) NULL,
    [EntryTag] nvarchar(100) NULL,
    [Priority] int NOT NULL,
    [HasAttachments] bit NOT NULL,
    [AttachmentCount] int NOT NULL,
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
    [Status] nvarchar(50) NOT NULL,
    [EffectiveDate] datetime2 NULL,
    [ExpirationDate] datetime2 NULL,
    [Metadata] nvarchar(max) NULL,
    [Tags] nvarchar(500) NULL,
    CONSTRAINT [PK_JournalEntries] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_JournalEntries_FiscalPeriods_FiscalPeriodId] FOREIGN KEY ([FiscalPeriodId]) REFERENCES [FiscalPeriods] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_JournalEntries_JournalEntries_OriginalJournalEntryId] FOREIGN KEY ([OriginalJournalEntryId]) REFERENCES [JournalEntries] ([Id]),
    CONSTRAINT [FK_JournalEntries_JournalEntries_ReversalJournalEntryId] FOREIGN KEY ([ReversalJournalEntryId]) REFERENCES [JournalEntries] ([Id]),
    CONSTRAINT [FK_JournalEntries_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [FiscalYears] (
    [Id] uniqueidentifier NOT NULL,
    [FiscalYearName] nvarchar(100) NOT NULL,
    [FiscalYearCode] nvarchar(20) NOT NULL,
    [Year] int NOT NULL,
    [FiscalYearType] nvarchar(20) NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [TotalDays] int NOT NULL,
    [NumberOfPeriods] int NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [IsActive] bit NOT NULL,
    [IsLocked] bit NOT NULL,
    [LockedDate] datetime2 NULL,
    [LockedByUserId] uniqueidentifier NULL,
    [LockReason] nvarchar(500) NULL,
    [IsCloseInitiated] bit NOT NULL,
    [CloseInitiatedDate] datetime2 NULL,
    [CloseInitiatedByUserId] uniqueidentifier NULL,
    [IsClosed] bit NOT NULL,
    [ClosedDate] datetime2 NULL,
    [ClosedByUserId] uniqueidentifier NULL,
    [AllPeriodsClosedValidated] bit NOT NULL,
    [AllPeriodsClosedValidatedDate] datetime2 NULL,
    [FinalDepreciationComplete] bit NOT NULL,
    [FinalDepreciationDate] datetime2 NULL,
    [YearEndRevaluationComplete] bit NOT NULL,
    [YearEndRevaluationDate] datetime2 NULL,
    [YearEndInventoryComplete] bit NOT NULL,
    [YearEndInventoryDate] datetime2 NULL,
    [YearEndAccrualsComplete] bit NOT NULL,
    [YearEndAccrualsDate] datetime2 NULL,
    [YearEndTrialBalanceValidated] bit NOT NULL,
    [YearEndTrialBalanceDate] datetime2 NULL,
    [RetainedEarningsTransferComplete] bit NOT NULL,
    [RetainedEarningsTransferDate] datetime2 NULL,
    [ClosingJournalEntryId] uniqueidentifier NULL,
    [NetIncomeTransferred] decimal(18,4) NULL,
    [OpeningBalancesGenerated] bit NOT NULL,
    [OpeningBalancesGeneratedDate] datetime2 NULL,
    [NextFiscalYearId] uniqueidentifier NULL,
    [OpeningBalanceJournalEntryId] uniqueidentifier NULL,
    [HasBeenReopened] bit NOT NULL,
    [ReopenCount] int NOT NULL,
    [LastReopenedDate] datetime2 NULL,
    [LastReopenedByUserId] uniqueidentifier NULL,
    [ReopenReason] nvarchar(1000) NULL,
    [ReportingFramework] nvarchar(50) NOT NULL,
    [BaseCurrency] nvarchar(3) NOT NULL,
    [TotalJournalEntries] int NOT NULL,
    [TotalTransactionLines] int NOT NULL,
    [TotalDebits] decimal(18,2) NOT NULL,
    [TotalCredits] decimal(18,2) NOT NULL,
    [BalanceDifference] decimal(18,4) NOT NULL,
    [TotalRevenue] decimal(18,2) NOT NULL,
    [TotalExpenses] decimal(18,2) NOT NULL,
    [NetIncome] decimal(18,4) NOT NULL,
    [IsBudgetApproved] bit NOT NULL,
    [BudgetApprovedDate] datetime2 NULL,
    [BudgetApprovedByUserId] uniqueidentifier NULL,
    [BudgetedRevenue] decimal(18,4) NULL,
    [BudgetedExpenses] decimal(18,4) NULL,
    [BudgetedNetIncome] decimal(18,4) NULL,
    [IsAuditComplete] bit NOT NULL,
    [AuditCompletedDate] datetime2 NULL,
    [AuditFirm] nvarchar(200) NULL,
    [AuditOpinion] nvarchar(50) NULL,
    [AuditReportReference] nvarchar(100) NULL,
    [Notes] nvarchar(2000) NULL,
    [YearEndClosingNotes] nvarchar(2000) NULL,
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
    [ReferenceNumber] nvarchar(50) NOT NULL,
    [EffectiveDate] datetime2 NULL,
    [ExpirationDate] datetime2 NULL,
    [Metadata] nvarchar(max) NULL,
    [Tags] nvarchar(500) NULL,
    [Priority] int NOT NULL,
    CONSTRAINT [PK_FiscalYears] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FiscalYears_FiscalYears_NextFiscalYearId] FOREIGN KEY ([NextFiscalYearId]) REFERENCES [FiscalYears] ([Id]),
    CONSTRAINT [FK_FiscalYears_JournalEntries_ClosingJournalEntryId] FOREIGN KEY ([ClosingJournalEntryId]) REFERENCES [JournalEntries] ([Id]),
    CONSTRAINT [FK_FiscalYears_JournalEntries_OpeningBalanceJournalEntryId] FOREIGN KEY ([OpeningBalanceJournalEntryId]) REFERENCES [JournalEntries] ([Id]),
    CONSTRAINT [FK_FiscalYears_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-11-26T18:18:11.7509745Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-11-26T18:18:11.7509798Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-11-26T18:18:11.7509801Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-11-26T18:18:11.7509803Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-11-26T18:18:11.7510082Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-11-26T18:18:11.7510100Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-11-26T18:18:11.7510108Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-11-26T18:18:11.7510114Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-11-26T18:18:11.7510128Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-11-26T18:18:11.7510136Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-11-26T18:18:11.7510143Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-11-26T18:18:11.7510151Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-11-26T18:18:11.7510162Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-11-26T18:18:11.7510171Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-11-26T18:18:11.7510183Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-11-26T18:18:11.7510199Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-11-26T18:18:11.7510217Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-11-26T18:18:11.7510224Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-11-26T18:18:11.7510235Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-11-26T18:18:11.7510243Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510303Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510305Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510306Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510307Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510308Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510310Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510311Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510312Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510312Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510314Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510315Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510316Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510317Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510317Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510318Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510319Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510370Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510373Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510374Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510375Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510375Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510376Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510377Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510378Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510379Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510380Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510380Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510381Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510382Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510383Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510384Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510452Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510454Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510456Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510457Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510458Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510459Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510459Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510460Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510461Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510462Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510477Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510479Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-11-26T18:18:11.7510480Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'CreatedById', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'LastModifiedById', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [CreatedById], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [LastModifiedById], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('0f2f7c4c-fb83-44c5-8106-d5256939db62', NULL, '2025-11-26T18:18:11.7509979Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-11-26T18:18:11.7509978Z', CAST(0 AS bit), NULL, N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('1921fba9-bfce-48a5-801d-879278410adc', NULL, '2025-11-26T18:18:11.7509962Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-11-26T18:18:11.7509961Z', CAST(0 AS bit), NULL, N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('2401dea8-721f-4d08-ab67-cf274389c668', NULL, '2025-11-26T18:18:11.7509947Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-11-26T18:18:11.7509946Z', CAST(0 AS bit), NULL, N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('8330a062-f0c2-40a8-8afb-00dc59a41052', NULL, '2025-11-26T18:18:11.7509900Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-11-26T18:18:11.7509900Z', CAST(0 AS bit), NULL, N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('be877047-515b-4c98-b25b-aedf5a007b18', NULL, '2025-11-26T18:18:11.7509932Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-11-26T18:18:11.7509931Z', CAST(0 AS bit), NULL, N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('c08bbe23-8102-4b0c-bd7e-1a3269330df0', NULL, '2025-11-26T18:18:11.7509880Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-11-26T18:18:11.7509876Z', CAST(0 AS bit), NULL, N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('e20e0b14-93fd-43aa-862d-db16f58adce2', NULL, '2025-11-26T18:18:11.7509916Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-11-26T18:18:11.7509916Z', CAST(0 AS bit), NULL, N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'CreatedById', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'LastModifiedById', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [BaseCurrency] = N'GHS', [BaseCurrencyName] = NULL, [CreatedAt] = '2025-11-26T18:18:11.7509484Z', [CurrencyDecimalPlaces] = 2, [CurrencySymbol] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_AccountBalances_AccountId] ON [AccountBalances] ([AccountId]);
GO

CREATE INDEX [IX_AccountBalances_FiscalPeriodId] ON [AccountBalances] ([FiscalPeriodId]);
GO

CREATE INDEX [IX_AccountBalances_TenantId] ON [AccountBalances] ([TenantId]);
GO

CREATE INDEX [IX_AccountCurrencyLinks_AccountId] ON [AccountCurrencyLinks] ([AccountId]);
GO

CREATE INDEX [IX_AccountCurrencyLinks_TenantId] ON [AccountCurrencyLinks] ([TenantId]);
GO

CREATE INDEX [IX_Accounts_ParentAccountId] ON [Accounts] ([ParentAccountId]);
GO

CREATE INDEX [IX_Accounts_TenantId] ON [Accounts] ([TenantId]);
GO

CREATE INDEX [IX_AccountSegmentStructures_TenantId] ON [AccountSegmentStructures] ([TenantId]);
GO

CREATE INDEX [IX_AccountSegmentValues_AccountId] ON [AccountSegmentValues] ([AccountId]);
GO

CREATE INDEX [IX_AccountSegmentValues_SegmentLookupValueId] ON [AccountSegmentValues] ([SegmentLookupValueId]);
GO

CREATE INDEX [IX_AccountSegmentValues_SegmentStructureId] ON [AccountSegmentValues] ([SegmentStructureId]);
GO

CREATE INDEX [IX_AccountSegmentValues_TenantId] ON [AccountSegmentValues] ([TenantId]);
GO

CREATE INDEX [IX_AccountTransactions_AccountId] ON [AccountTransactions] ([AccountId]);
GO

CREATE INDEX [IX_AccountTransactions_FiscalPeriodId] ON [AccountTransactions] ([FiscalPeriodId]);
GO

CREATE INDEX [IX_AccountTransactions_JournalEntryId] ON [AccountTransactions] ([JournalEntryId]);
GO

CREATE INDEX [IX_AccountTransactions_OriginalTransactionId] ON [AccountTransactions] ([OriginalTransactionId]);
GO

CREATE INDEX [IX_AccountTransactions_ReversalTransactionId] ON [AccountTransactions] ([ReversalTransactionId]);
GO

CREATE INDEX [IX_AccountTransactions_TenantId] ON [AccountTransactions] ([TenantId]);
GO

CREATE UNIQUE INDEX [IX_ExchangeRates_PreviousRateId] ON [ExchangeRates] ([PreviousRateId]) WHERE [PreviousRateId] IS NOT NULL;
GO

CREATE INDEX [IX_ExchangeRates_TenantId] ON [ExchangeRates] ([TenantId]);
GO

CREATE INDEX [IX_FiscalPeriods_FiscalYearId] ON [FiscalPeriods] ([FiscalYearId]);
GO

CREATE INDEX [IX_FiscalPeriods_TenantId] ON [FiscalPeriods] ([TenantId]);
GO

CREATE INDEX [IX_FiscalYears_ClosingJournalEntryId] ON [FiscalYears] ([ClosingJournalEntryId]);
GO

CREATE UNIQUE INDEX [IX_FiscalYears_NextFiscalYearId] ON [FiscalYears] ([NextFiscalYearId]) WHERE [NextFiscalYearId] IS NOT NULL;
GO

CREATE INDEX [IX_FiscalYears_OpeningBalanceJournalEntryId] ON [FiscalYears] ([OpeningBalanceJournalEntryId]);
GO

CREATE INDEX [IX_FiscalYears_TenantId] ON [FiscalYears] ([TenantId]);
GO

CREATE INDEX [IX_JournalEntries_FiscalPeriodId] ON [JournalEntries] ([FiscalPeriodId]);
GO

CREATE INDEX [IX_JournalEntries_OriginalJournalEntryId] ON [JournalEntries] ([OriginalJournalEntryId]);
GO

CREATE INDEX [IX_JournalEntries_ReversalJournalEntryId] ON [JournalEntries] ([ReversalJournalEntryId]);
GO

CREATE INDEX [IX_JournalEntries_TenantId] ON [JournalEntries] ([TenantId]);
GO

CREATE INDEX [IX_SegmentLookupValues_ParentValueId] ON [SegmentLookupValues] ([ParentValueId]);
GO

CREATE INDEX [IX_SegmentLookupValues_SegmentStructureId] ON [SegmentLookupValues] ([SegmentStructureId]);
GO

CREATE INDEX [IX_SegmentLookupValues_TenantId] ON [SegmentLookupValues] ([TenantId]);
GO

ALTER TABLE [AccountBalances] ADD CONSTRAINT [FK_AccountBalances_FiscalPeriods_FiscalPeriodId] FOREIGN KEY ([FiscalPeriodId]) REFERENCES [FiscalPeriods] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [AccountTransactions] ADD CONSTRAINT [FK_AccountTransactions_FiscalPeriods_FiscalPeriodId] FOREIGN KEY ([FiscalPeriodId]) REFERENCES [FiscalPeriods] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [AccountTransactions] ADD CONSTRAINT [FK_AccountTransactions_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [JournalEntries] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [FiscalPeriods] ADD CONSTRAINT [FK_FiscalPeriods_FiscalYears_FiscalYearId] FOREIGN KEY ([FiscalYearId]) REFERENCES [FiscalYears] ([Id]) ON DELETE CASCADE;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251126181813_AddFinanceModuleTablesAndTenantCurrency', N'8.0.0');
GO

COMMIT;
GO

