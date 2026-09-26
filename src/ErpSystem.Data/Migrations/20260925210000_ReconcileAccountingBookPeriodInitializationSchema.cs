using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Repairs the missing C4 schema on baseline-stamped databases without manufacturing
/// initialization, approval or open-period authority. Existing books and ledger rows are untouched.
/// The application continues to reject posting until exact-book period authority is approved.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260925210000_ReconcileAccountingBookPeriodInitializationSchema")]
public sealed class ReconcileAccountingBookPeriodInitializationSchema : Migration
{
    public const string ReconciliationSql = """
        DECLARE @C4TableCount int = (SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo') AND name IN (N'AccountingBookInitializations', N'AccountingBookPeriods', N'AccountingBookInitializationLines'));
        IF @C4TableCount NOT IN (0, 3)
            THROW 51000, 'C4_RECONCILE_PARTIAL_SCHEMA: incomplete authority schema requires explicit repair; no objects were changed.', 1;
        IF OBJECT_ID(N'dbo.AccountingBooks', N'U') IS NULL OR OBJECT_ID(N'dbo.FiscalPeriods', N'U') IS NULL
           OR OBJECT_ID(N'dbo.Accounts', N'U') IS NULL OR OBJECT_ID(N'dbo.Tenants', N'U') IS NULL
           OR OBJECT_ID(N'dbo.ExchangeRates', N'U') IS NULL
            THROW 51000, 'C4_RECONCILE_PREDECESSOR: required Finance predecessor tables are missing.', 1;

        IF @C4TableCount = 0
        BEGIN
            -- Legacy stamped databases can also lack the tenant candidate keys introduced
            -- with C4. Adding uniqueness never changes Finance authority or ledger data.
            IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.FiscalPeriods') AND name=N'AK_FiscalPeriods_TenantId_Id')
                ALTER TABLE dbo.FiscalPeriods ADD CONSTRAINT AK_FiscalPeriods_TenantId_Id UNIQUE (TenantId, Id);
            IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.Accounts') AND name=N'AK_Accounts_TenantId_Id')
                ALTER TABLE dbo.Accounts ADD CONSTRAINT AK_Accounts_TenantId_Id UNIQUE (TenantId, Id);
            IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.ExchangeRates') AND name=N'AK_ExchangeRates_TenantId_Id')
                ALTER TABLE dbo.ExchangeRates ADD CONSTRAINT AK_ExchangeRates_TenantId_Id UNIQUE (TenantId, Id);
            CREATE TABLE [dbo].[AccountingBookInitializations] (
                [Id] uniqueidentifier NOT NULL,
                [AccountingBookId] uniqueidentifier NOT NULL,
                [Version] int NOT NULL,
                [SupersedesInitializationId] uniqueidentifier NULL,
                [Mode] int NOT NULL,
                [TranslationMethod] int NULL,
                [InitializationStatus] int NOT NULL,
                [CutoffDate] datetime2 NOT NULL,
                [CutoffFiscalPeriodId] uniqueidentifier NOT NULL,
                [SourceAccountingBookId] uniqueidentifier NULL,
                [IdempotencyKey] nvarchar(100) NOT NULL,
                [Reason] nvarchar(500) NOT NULL,
                [TotalDebits] decimal(18,2) NOT NULL,
                [TotalCredits] decimal(18,2) NOT NULL,
                [RequiredAccountCount] int NOT NULL,
                [CoveredAccountCount] int NOT NULL,
                [EvidenceFingerprint] nvarchar(64) NOT NULL,
                [ReconciliationFingerprint] nvarchar(64) NOT NULL,
                [PreparedByUserId] uniqueidentifier NOT NULL,
                [PreparedAtUtc] datetime2 NOT NULL,
                [WorkflowInstanceId] uniqueidentifier NULL,
                [ApprovedByUserId] uniqueidentifier NULL,
                [ApprovedAtUtc] datetime2 NULL,
                [RejectedByUserId] uniqueidentifier NULL,
                [RejectedAtUtc] datetime2 NULL,
                [DecidedByUserId] uniqueidentifier NULL,
                [DecidedAtUtc] datetime2 NULL,
                [DecisionReason] nvarchar(500) NULL,
                [RowVersion] rowversion NOT NULL,
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
                CONSTRAINT [PK_AccountingBookInitializations] PRIMARY KEY ([Id]),
                CONSTRAINT [AK_AccountingBookInitializations_TenantId_Id] UNIQUE ([TenantId], [Id]),
                CONSTRAINT [AK_AccountingBookInitializations_TenantId_AccountingBookId_Id] UNIQUE ([TenantId], [AccountingBookId], [Id]),
                CONSTRAINT [CK_AccountingBookInitializations_ApprovalShape] CHECK (([InitializationStatus] IN (1, 2) AND [ApprovedByUserId] IS NULL AND [ApprovedAtUtc] IS NULL AND [RejectedByUserId] IS NULL AND [RejectedAtUtc] IS NULL) OR ([InitializationStatus] = 3 AND [ApprovedByUserId] IS NOT NULL AND [ApprovedAtUtc] IS NOT NULL AND [RejectedByUserId] IS NULL AND [RejectedAtUtc] IS NULL) OR ([InitializationStatus] = 4 AND [ApprovedByUserId] IS NULL AND [ApprovedAtUtc] IS NULL AND [RejectedByUserId] IS NOT NULL AND [RejectedAtUtc] IS NOT NULL)),
                CONSTRAINT [CK_AccountingBookInitializations_Balanced] CHECK ([IsDeleted] = 1 OR [TotalDebits] = [TotalCredits]),
                CONSTRAINT [CK_AccountingBookInitializations_Coverage] CHECK ([RequiredAccountCount] >= 0 AND [CoveredAccountCount] >= 0 AND [CoveredAccountCount] <= [RequiredAccountCount]),
                CONSTRAINT [CK_AccountingBookInitializations_DecisionMakerChecker] CHECK ([DecidedByUserId] IS NULL OR [DecidedByUserId] <> [PreparedByUserId]),
                CONSTRAINT [CK_AccountingBookInitializations_EvidenceFingerprint] CHECK (LEN([EvidenceFingerprint]) = 64 AND [EvidenceFingerprint] = RTRIM([EvidenceFingerprint]) AND [EvidenceFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'),
                CONSTRAINT [CK_AccountingBookInitializations_MakerChecker] CHECK ([ApprovedByUserId] IS NULL OR [ApprovedByUserId] <> [PreparedByUserId]),
                CONSTRAINT [CK_AccountingBookInitializations_Mode] CHECK ([IsDeleted] = 1 OR [Mode] IN (1, 2, 3)),
                CONSTRAINT [CK_AccountingBookInitializations_NoDelete] CHECK ([IsDeleted] = 0),
                CONSTRAINT [CK_AccountingBookInitializations_ReconciliationFingerprint] CHECK (LEN([ReconciliationFingerprint]) = 64 AND [ReconciliationFingerprint] = RTRIM([ReconciliationFingerprint]) AND [ReconciliationFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'),
                CONSTRAINT [CK_AccountingBookInitializations_SourceShape] CHECK (([Mode] = 1 AND [SourceAccountingBookId] IS NULL) OR ([Mode] IN (2, 3) AND [SourceAccountingBookId] IS NOT NULL AND [SourceAccountingBookId] <> [AccountingBookId])),
                CONSTRAINT [CK_AccountingBookInitializations_Status] CHECK ([IsDeleted] = 1 OR [InitializationStatus] IN (1, 2, 3, 4)),
                CONSTRAINT [CK_AccountingBookInitializations_TranslationMethod] CHECK ([TranslationMethod] IS NULL OR [TranslationMethod] IN (1, 2)),
                CONSTRAINT [FK_AccountingBookInitializations_AccountingBookInitializations_TenantId_AccountingBookId_SupersedesInitializationId] FOREIGN KEY ([TenantId], [AccountingBookId], [SupersedesInitializationId]) REFERENCES [dbo].[AccountingBookInitializations] ([TenantId], [AccountingBookId], [Id]) ON DELETE NO ACTION,
                CONSTRAINT [FK_AccountingBookInitializations_AccountingBooks_TenantId_AccountingBookId] FOREIGN KEY ([TenantId], [AccountingBookId]) REFERENCES [dbo].[AccountingBooks] ([TenantId], [Id]) ON DELETE NO ACTION,
                CONSTRAINT [FK_AccountingBookInitializations_AccountingBooks_TenantId_SourceAccountingBookId] FOREIGN KEY ([TenantId], [SourceAccountingBookId]) REFERENCES [dbo].[AccountingBooks] ([TenantId], [Id]) ON DELETE NO ACTION,
                CONSTRAINT [FK_AccountingBookInitializations_FiscalPeriods_TenantId_CutoffFiscalPeriodId] FOREIGN KEY ([TenantId], [CutoffFiscalPeriodId]) REFERENCES [dbo].[FiscalPeriods] ([TenantId], [Id]) ON DELETE NO ACTION,
                CONSTRAINT [FK_AccountingBookInitializations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
            );

            CREATE TABLE [dbo].[AccountingBookPeriods] (
                [Id] uniqueidentifier NOT NULL,
                [AccountingBookId] uniqueidentifier NOT NULL,
                [FiscalPeriodId] uniqueidentifier NOT NULL,
                [PeriodStatus] int NOT NULL,
                [PendingStatus] int NULL,
                [PendingReason] nvarchar(500) NULL,
                [RequestedByUserId] uniqueidentifier NULL,
                [RequestedAtUtc] datetime2 NULL,
                [WorkflowInstanceId] uniqueidentifier NULL,
                [DecidedByUserId] uniqueidentifier NULL,
                [DecidedAtUtc] datetime2 NULL,
                [DecisionReason] nvarchar(500) NULL,
                [RowVersion] rowversion NOT NULL,
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
                CONSTRAINT [PK_AccountingBookPeriods] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_AccountingBookPeriods_NoDelete] CHECK ([IsDeleted] = 0),
                CONSTRAINT [CK_AccountingBookPeriods_PendingStatus] CHECK ([PendingStatus] IS NULL OR [PendingStatus] IN (1, 2, 3, 4)),
                CONSTRAINT [CK_AccountingBookPeriods_Status] CHECK ([IsDeleted] = 1 OR [PeriodStatus] IN (1, 2, 3, 4)),
                CONSTRAINT [FK_AccountingBookPeriods_AccountingBooks_TenantId_AccountingBookId] FOREIGN KEY ([TenantId], [AccountingBookId]) REFERENCES [dbo].[AccountingBooks] ([TenantId], [Id]) ON DELETE NO ACTION,
                CONSTRAINT [FK_AccountingBookPeriods_FiscalPeriods_TenantId_FiscalPeriodId] FOREIGN KEY ([TenantId], [FiscalPeriodId]) REFERENCES [dbo].[FiscalPeriods] ([TenantId], [Id]) ON DELETE NO ACTION,
                CONSTRAINT [FK_AccountingBookPeriods_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
            );

            CREATE TABLE [dbo].[AccountingBookInitializationLines] (
                [Id] uniqueidentifier NOT NULL,
                [AccountingBookInitializationId] uniqueidentifier NOT NULL,
                [AccountId] uniqueidentifier NOT NULL,
                [CurrencyCode] nvarchar(3) NOT NULL,
                [OpeningDebit] decimal(18,4) NOT NULL,
                [OpeningCredit] decimal(18,4) NOT NULL,
                [BaseBookSignedBalance] decimal(18,4) NOT NULL,
                [OpeningAdjustment] decimal(18,4) NOT NULL,
                [TranslationExchangeRateId] uniqueidentifier NULL,
                [TranslationRate] decimal(18,6) NULL,
                [TranslationRateDate] datetime2 NULL,
                [TranslationRateSource] nvarchar(100) NULL,
                [TranslationRateType] nvarchar(30) NULL,
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
                CONSTRAINT [PK_AccountingBookInitializationLines] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_AccountingBookInitializationLines_Amounts] CHECK ([OpeningDebit] >= 0 AND [OpeningCredit] >= 0 AND NOT ([OpeningDebit] > 0 AND [OpeningCredit] > 0)),
                CONSTRAINT [CK_AccountingBookInitializationLines_Currency] CHECK (LEN([CurrencyCode]) = 3 AND [CurrencyCode] = RTRIM([CurrencyCode]) AND [CurrencyCode] COLLATE Latin1_General_100_BIN2 LIKE '[A-Z][A-Z][A-Z]'),
                CONSTRAINT [CK_AccountingBookInitializationLines_NoDelete] CHECK ([IsDeleted] = 0),
                CONSTRAINT [CK_AccountingBookInitializationLines_TranslationEvidence] CHECK (([TranslationExchangeRateId] IS NULL AND [TranslationRate] IS NULL AND [TranslationRateDate] IS NULL AND [TranslationRateType] IS NULL AND [TranslationRateSource] IS NULL) OR ([TranslationExchangeRateId] IS NOT NULL AND [TranslationRate] > 0 AND [TranslationRateDate] IS NOT NULL AND [TranslationRateType] IS NOT NULL AND [TranslationRateSource] IS NOT NULL)),
                CONSTRAINT [FK_AccountingBookInitializationLines_AccountingBookInitializations_TenantId_AccountingBookInitializationId] FOREIGN KEY ([TenantId], [AccountingBookInitializationId]) REFERENCES [dbo].[AccountingBookInitializations] ([TenantId], [Id]) ON DELETE NO ACTION,
                CONSTRAINT [FK_AccountingBookInitializationLines_Accounts_TenantId_AccountId] FOREIGN KEY ([TenantId], [AccountId]) REFERENCES [dbo].[Accounts] ([TenantId], [Id]) ON DELETE NO ACTION,
                CONSTRAINT [FK_AccountingBookInitializationLines_ExchangeRates_TenantId_TranslationExchangeRateId] FOREIGN KEY ([TenantId], [TranslationExchangeRateId]) REFERENCES [dbo].[ExchangeRates] ([TenantId], [Id]) ON DELETE NO ACTION,
                CONSTRAINT [FK_AccountingBookInitializationLines_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
            );

            CREATE INDEX [IX_AccountingBookInitializationLines_TenantId_AccountId] ON [dbo].[AccountingBookInitializationLines] ([TenantId], [AccountId]);
            CREATE INDEX [IX_AccountingBookInitializationLines_TenantId_TranslationExchangeRateId] ON [dbo].[AccountingBookInitializationLines] ([TenantId], [TranslationExchangeRateId]);
            CREATE UNIQUE INDEX [IX_AccountingBookInitializationLines_TenantId_AccountingBookInitializationId_AccountId] ON [dbo].[AccountingBookInitializationLines] ([TenantId], [AccountingBookInitializationId], [AccountId]);
            CREATE UNIQUE INDEX [IX_AccountingBookInitializations_TenantId_AccountingBookId_InitializationStatus] ON [dbo].[AccountingBookInitializations] ([TenantId], [AccountingBookId], [InitializationStatus]) WHERE [IsDeleted] = 0 AND [InitializationStatus] = 3;
            CREATE UNIQUE INDEX [IX_AccountingBookInitializations_TenantId_AccountingBookId_Version] ON [dbo].[AccountingBookInitializations] ([TenantId], [AccountingBookId], [Version]);
            CREATE UNIQUE INDEX [IX_AccountingBookInitializations_TenantId_IdempotencyKey] ON [dbo].[AccountingBookInitializations] ([TenantId], [IdempotencyKey]);
            CREATE INDEX [IX_AccountingBookInitializations_TenantId_SourceAccountingBookId] ON [dbo].[AccountingBookInitializations] ([TenantId], [SourceAccountingBookId]);
            CREATE INDEX [IX_AccountingBookInitializations_TenantId_AccountingBookId_SupersedesInitializationId] ON [dbo].[AccountingBookInitializations] ([TenantId], [AccountingBookId], [SupersedesInitializationId]);
            CREATE INDEX [IX_AccountingBookInitializations_TenantId_CutoffFiscalPeriodId] ON [dbo].[AccountingBookInitializations] ([TenantId], [CutoffFiscalPeriodId]);
            CREATE UNIQUE INDEX [IX_AccountingBookPeriods_TenantId_AccountingBookId_FiscalPeriodId] ON [dbo].[AccountingBookPeriods] ([TenantId], [AccountingBookId], [FiscalPeriodId]);
            CREATE INDEX [IX_AccountingBookPeriods_TenantId_FiscalPeriodId] ON [dbo].[AccountingBookPeriods] ([TenantId], [FiscalPeriodId]);
        END;

        DECLARE @ExpectedColumns TABLE (TableName sysname, ColumnName sysname, TypeName sysname, MaxLength smallint NULL, [Precision] tinyint NULL, Scale tinyint NULL, IsNullable bit);
        INSERT @ExpectedColumns VALUES
        (N'AccountingBookInitializations',N'Id',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'AccountingBookId',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'Version',N'int',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'SupersedesInitializationId',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'Mode',N'int',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'TranslationMethod',N'int',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'InitializationStatus',N'int',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'CutoffDate',N'datetime2',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'CutoffFiscalPeriodId',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'SourceAccountingBookId',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'IdempotencyKey',N'nvarchar',200,NULL,NULL,0),
        (N'AccountingBookInitializations',N'Reason',N'nvarchar',1000,NULL,NULL,0),
        (N'AccountingBookInitializations',N'TotalDebits',N'decimal',NULL,18,2,0),
        (N'AccountingBookInitializations',N'TotalCredits',N'decimal',NULL,18,2,0),
        (N'AccountingBookInitializations',N'RequiredAccountCount',N'int',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'CoveredAccountCount',N'int',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'EvidenceFingerprint',N'nvarchar',128,NULL,NULL,0),
        (N'AccountingBookInitializations',N'ReconciliationFingerprint',N'nvarchar',128,NULL,NULL,0),
        (N'AccountingBookInitializations',N'PreparedByUserId',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'PreparedAtUtc',N'datetime2',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'WorkflowInstanceId',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'ApprovedByUserId',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'ApprovedAtUtc',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'RejectedByUserId',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'RejectedAtUtc',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'DecidedByUserId',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'DecidedAtUtc',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'DecisionReason',N'nvarchar',1000,NULL,NULL,1),
        (N'AccountingBookInitializations',N'RowVersion',N'timestamp',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'CreatedAt',N'datetime2',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'UpdatedAt',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'CreatedBy',N'nvarchar',-1,NULL,NULL,1),
        (N'AccountingBookInitializations',N'UpdatedBy',N'nvarchar',-1,NULL,NULL,1),
        (N'AccountingBookInitializations',N'CreatedById',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'LastModifiedById',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'IsDeleted',N'bit',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'DeletedAt',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'DeletedBy',N'nvarchar',-1,NULL,NULL,1),
        (N'AccountingBookInitializations',N'TenantId',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookPeriods',N'Id',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookPeriods',N'AccountingBookId',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookPeriods',N'FiscalPeriodId',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookPeriods',N'PeriodStatus',N'int',NULL,NULL,NULL,0),
        (N'AccountingBookPeriods',N'PendingStatus',N'int',NULL,NULL,NULL,1),
        (N'AccountingBookPeriods',N'PendingReason',N'nvarchar',1000,NULL,NULL,1),
        (N'AccountingBookPeriods',N'RequestedByUserId',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookPeriods',N'RequestedAtUtc',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookPeriods',N'WorkflowInstanceId',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookPeriods',N'DecidedByUserId',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookPeriods',N'DecidedAtUtc',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookPeriods',N'DecisionReason',N'nvarchar',1000,NULL,NULL,1),
        (N'AccountingBookPeriods',N'RowVersion',N'timestamp',NULL,NULL,NULL,0),
        (N'AccountingBookPeriods',N'CreatedAt',N'datetime2',NULL,NULL,NULL,0),
        (N'AccountingBookPeriods',N'UpdatedAt',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookPeriods',N'CreatedBy',N'nvarchar',-1,NULL,NULL,1),
        (N'AccountingBookPeriods',N'UpdatedBy',N'nvarchar',-1,NULL,NULL,1),
        (N'AccountingBookPeriods',N'CreatedById',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookPeriods',N'LastModifiedById',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookPeriods',N'IsDeleted',N'bit',NULL,NULL,NULL,0),
        (N'AccountingBookPeriods',N'DeletedAt',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookPeriods',N'DeletedBy',N'nvarchar',-1,NULL,NULL,1),
        (N'AccountingBookPeriods',N'TenantId',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookInitializationLines',N'Id',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookInitializationLines',N'AccountingBookInitializationId',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookInitializationLines',N'AccountId',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookInitializationLines',N'CurrencyCode',N'nvarchar',6,NULL,NULL,0),
        (N'AccountingBookInitializationLines',N'OpeningDebit',N'decimal',NULL,18,4,0),
        (N'AccountingBookInitializationLines',N'OpeningCredit',N'decimal',NULL,18,4,0),
        (N'AccountingBookInitializationLines',N'BaseBookSignedBalance',N'decimal',NULL,18,4,0),
        (N'AccountingBookInitializationLines',N'OpeningAdjustment',N'decimal',NULL,18,4,0),
        (N'AccountingBookInitializationLines',N'TranslationExchangeRateId',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookInitializationLines',N'TranslationRate',N'decimal',NULL,18,6,1),
        (N'AccountingBookInitializationLines',N'TranslationRateDate',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookInitializationLines',N'TranslationRateSource',N'nvarchar',200,NULL,NULL,1),
        (N'AccountingBookInitializationLines',N'TranslationRateType',N'nvarchar',60,NULL,NULL,1),
        (N'AccountingBookInitializationLines',N'CreatedAt',N'datetime2',NULL,NULL,NULL,0),
        (N'AccountingBookInitializationLines',N'UpdatedAt',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookInitializationLines',N'CreatedBy',N'nvarchar',-1,NULL,NULL,1),
        (N'AccountingBookInitializationLines',N'UpdatedBy',N'nvarchar',-1,NULL,NULL,1),
        (N'AccountingBookInitializationLines',N'CreatedById',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookInitializationLines',N'LastModifiedById',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookInitializationLines',N'IsDeleted',N'bit',NULL,NULL,NULL,0),
        (N'AccountingBookInitializationLines',N'DeletedAt',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookInitializationLines',N'DeletedBy',N'nvarchar',-1,NULL,NULL,1),
        (N'AccountingBookInitializationLines',N'TenantId',N'uniqueidentifier',NULL,NULL,NULL,0);
        IF EXISTS (SELECT 1 FROM @ExpectedColumns e LEFT JOIN sys.columns c ON c.object_id = OBJECT_ID(N'dbo.' + e.TableName) AND c.name = e.ColumnName
            LEFT JOIN sys.types ty ON ty.user_type_id = c.user_type_id
            WHERE c.column_id IS NULL OR ty.name <> e.TypeName OR c.is_nullable <> e.IsNullable
               OR (e.MaxLength IS NOT NULL AND c.max_length <> e.MaxLength)
               OR (e.[Precision] IS NOT NULL AND c.precision <> e.[Precision]) OR (e.Scale IS NOT NULL AND c.scale <> e.Scale))
            THROW 51000, 'C4_RECONCILE_COLUMN_DRIFT: existing C4 columns do not match the governed model.', 1;

        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND i.name = N'IX_AccountingBookInitializationLines_TenantId_TranslationExchangeRateId' AND i.is_unique = 0 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 2
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'TranslationExchangeRateId')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: IX_AccountingBookInitializationLines_TenantId_TranslationExchangeRateId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND f.name=N'FK_AccountingBookInitializationLines_ExchangeRates_TenantId_TranslationExchangeRateId' AND f.referenced_object_id=OBJECT_ID(N'dbo.ExchangeRates') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=2
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=2 AND pc.name=N'TranslationExchangeRateId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookInitializationLines_ExchangeRates_TenantId_TranslationExchangeRateId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_TranslationMethod' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([TranslationMethod] IS NULL OR ([TranslationMethod]=(2) OR [TranslationMethod]=(1)))' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_TranslationMethod', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND c.name=N'CK_AccountingBookInitializationLines_TranslationEvidence' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([TranslationExchangeRateId] IS NULL AND [TranslationRate] IS NULL AND [TranslationRateDate] IS NULL AND [TranslationRateType] IS NULL AND [TranslationRateSource] IS NULL OR [TranslationExchangeRateId] IS NOT NULL AND [TranslationRate]>(0) AND [TranslationRateDate] IS NOT NULL AND [TranslationRateType] IS NOT NULL AND [TranslationRateSource] IS NOT NULL)' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializationLines_TranslationEvidence', 1;

        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializations') AND i.name = N'PK_AccountingBookInitializations' AND i.is_unique = 1 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 1
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'Id')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: PK_AccountingBookInitializations', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializations') AND i.name = N'AK_AccountingBookInitializations_TenantId_Id' AND i.is_unique = 1 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 2
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'Id')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: AK_AccountingBookInitializations_TenantId_Id', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializations') AND i.name = N'AK_AccountingBookInitializations_TenantId_AccountingBookId_Id' AND i.is_unique = 1 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 3
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'AccountingBookId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=3 AND c.name=N'Id')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: AK_AccountingBookInitializations_TenantId_AccountingBookId_Id', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookPeriods') AND i.name = N'PK_AccountingBookPeriods' AND i.is_unique = 1 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 1
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'Id')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: PK_AccountingBookPeriods', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND i.name = N'PK_AccountingBookInitializationLines' AND i.is_unique = 1 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 1
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'Id')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: PK_AccountingBookInitializationLines', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND i.name = N'IX_AccountingBookInitializationLines_TenantId_AccountId' AND i.is_unique = 0 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 2
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'AccountId')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: IX_AccountingBookInitializationLines_TenantId_AccountId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND i.name = N'IX_AccountingBookInitializationLines_TenantId_AccountingBookInitializationId_AccountId' AND i.is_unique = 1 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 3
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'AccountingBookInitializationId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=3 AND c.name=N'AccountId')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: IX_AccountingBookInitializationLines_TenantId_AccountingBookInitializationId_AccountId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializations') AND i.name = N'IX_AccountingBookInitializations_TenantId_AccountingBookId_InitializationStatus' AND i.is_unique = 1 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 3
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'AccountingBookId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=3 AND c.name=N'InitializationStatus')
            AND i.has_filter = 1 AND REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(i.filter_definition, N'(', N''), N')', N''), N'[', N''), N']', N''), N' ', N''), NCHAR(13), N''), NCHAR(10), N''), NCHAR(9), N'') COLLATE Latin1_General_100_BIN2 = REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(N'[IsDeleted] = 0 AND [InitializationStatus] = 3', N'(', N''), N')', N''), N'[', N''), N']', N''), N' ', N''), NCHAR(13), N''), NCHAR(10), N''), NCHAR(9), N'') COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: IX_AccountingBookInitializations_TenantId_AccountingBookId_InitializationStatus', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializations') AND i.name = N'IX_AccountingBookInitializations_TenantId_AccountingBookId_Version' AND i.is_unique = 1 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 3
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'AccountingBookId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=3 AND c.name=N'Version')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: IX_AccountingBookInitializations_TenantId_AccountingBookId_Version', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializations') AND i.name = N'IX_AccountingBookInitializations_TenantId_IdempotencyKey' AND i.is_unique = 1 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 2
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'IdempotencyKey')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: IX_AccountingBookInitializations_TenantId_IdempotencyKey', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializations') AND i.name = N'IX_AccountingBookInitializations_TenantId_SourceAccountingBookId' AND i.is_unique = 0 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 2
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'SourceAccountingBookId')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: IX_AccountingBookInitializations_TenantId_SourceAccountingBookId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializations') AND i.name = N'IX_AccountingBookInitializations_TenantId_AccountingBookId_SupersedesInitializationId' AND i.is_unique = 0 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 3
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'AccountingBookId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=3 AND c.name=N'SupersedesInitializationId')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: IX_AccountingBookInitializations_TenantId_AccountingBookId_SupersedesInitializationId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializations') AND i.name = N'IX_AccountingBookInitializations_TenantId_CutoffFiscalPeriodId' AND i.is_unique = 0 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 2
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'CutoffFiscalPeriodId')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: IX_AccountingBookInitializations_TenantId_CutoffFiscalPeriodId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookPeriods') AND i.name = N'IX_AccountingBookPeriods_TenantId_AccountingBookId_FiscalPeriodId' AND i.is_unique = 1 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 3
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'AccountingBookId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=3 AND c.name=N'FiscalPeriodId')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: IX_AccountingBookPeriods_TenantId_AccountingBookId_FiscalPeriodId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookPeriods') AND i.name = N'IX_AccountingBookPeriods_TenantId_FiscalPeriodId' AND i.is_unique = 0 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 2
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'FiscalPeriodId')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: IX_AccountingBookPeriods_TenantId_FiscalPeriodId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND f.name=N'FK_AccountingBookInitializations_AccountingBookInitializations_TenantId_AccountingBookId_SupersedesInitializationId' AND f.referenced_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=3
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=2 AND pc.name=N'AccountingBookId' AND rc.name=N'AccountingBookId')
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=3 AND pc.name=N'SupersedesInitializationId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookInitializations_AccountingBookInitializations_TenantId_AccountingBookId_SupersedesInitializationId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND f.name=N'FK_AccountingBookInitializations_AccountingBooks_TenantId_AccountingBookId' AND f.referenced_object_id=OBJECT_ID(N'dbo.AccountingBooks') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=2
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=2 AND pc.name=N'AccountingBookId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookInitializations_AccountingBooks_TenantId_AccountingBookId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND f.name=N'FK_AccountingBookInitializations_AccountingBooks_TenantId_SourceAccountingBookId' AND f.referenced_object_id=OBJECT_ID(N'dbo.AccountingBooks') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=2
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=2 AND pc.name=N'SourceAccountingBookId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookInitializations_AccountingBooks_TenantId_SourceAccountingBookId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND f.name=N'FK_AccountingBookInitializations_FiscalPeriods_TenantId_CutoffFiscalPeriodId' AND f.referenced_object_id=OBJECT_ID(N'dbo.FiscalPeriods') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=2
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=2 AND pc.name=N'CutoffFiscalPeriodId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookInitializations_FiscalPeriods_TenantId_CutoffFiscalPeriodId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND f.name=N'FK_AccountingBookInitializations_Tenants_TenantId' AND f.referenced_object_id=OBJECT_ID(N'dbo.Tenants') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=1
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookInitializations_Tenants_TenantId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookPeriods') AND f.name=N'FK_AccountingBookPeriods_AccountingBooks_TenantId_AccountingBookId' AND f.referenced_object_id=OBJECT_ID(N'dbo.AccountingBooks') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=2
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=2 AND pc.name=N'AccountingBookId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookPeriods_AccountingBooks_TenantId_AccountingBookId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookPeriods') AND f.name=N'FK_AccountingBookPeriods_FiscalPeriods_TenantId_FiscalPeriodId' AND f.referenced_object_id=OBJECT_ID(N'dbo.FiscalPeriods') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=2
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=2 AND pc.name=N'FiscalPeriodId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookPeriods_FiscalPeriods_TenantId_FiscalPeriodId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookPeriods') AND f.name=N'FK_AccountingBookPeriods_Tenants_TenantId' AND f.referenced_object_id=OBJECT_ID(N'dbo.Tenants') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=1
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookPeriods_Tenants_TenantId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND f.name=N'FK_AccountingBookInitializationLines_AccountingBookInitializations_TenantId_AccountingBookInitializationId' AND f.referenced_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=2
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=2 AND pc.name=N'AccountingBookInitializationId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookInitializationLines_AccountingBookInitializations_TenantId_AccountingBookInitializationId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND f.name=N'FK_AccountingBookInitializationLines_Accounts_TenantId_AccountId' AND f.referenced_object_id=OBJECT_ID(N'dbo.Accounts') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=2
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=2 AND pc.name=N'AccountId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookInitializationLines_Accounts_TenantId_AccountId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND f.name=N'FK_AccountingBookInitializationLines_Tenants_TenantId' AND f.referenced_object_id=OBJECT_ID(N'dbo.Tenants') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=1
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookInitializationLines_Tenants_TenantId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_ApprovalShape' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'(([InitializationStatus]=(2) OR [InitializationStatus]=(1)) AND [ApprovedByUserId] IS NULL AND [ApprovedAtUtc] IS NULL AND [RejectedByUserId] IS NULL AND [RejectedAtUtc] IS NULL OR [InitializationStatus]=(3) AND [ApprovedByUserId] IS NOT NULL AND [ApprovedAtUtc] IS NOT NULL AND [RejectedByUserId] IS NULL AND [RejectedAtUtc] IS NULL OR [InitializationStatus]=(4) AND [ApprovedByUserId] IS NULL AND [ApprovedAtUtc] IS NULL AND [RejectedByUserId] IS NOT NULL AND [RejectedAtUtc] IS NOT NULL)' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_ApprovalShape', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_Balanced' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([IsDeleted]=(1) OR [TotalDebits]=[TotalCredits])' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_Balanced', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_Coverage' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([RequiredAccountCount]>=(0) AND [CoveredAccountCount]>=(0) AND [CoveredAccountCount]<=[RequiredAccountCount])' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_Coverage', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_DecisionMakerChecker' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([DecidedByUserId] IS NULL OR [DecidedByUserId]<>[PreparedByUserId])' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_DecisionMakerChecker', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_EvidenceFingerprint' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'(len([EvidenceFingerprint])=(64) AND [EvidenceFingerprint]=rtrim([EvidenceFingerprint]) AND NOT ([EvidenceFingerprint]) collate Latin1_General_100_BIN2 like ''%[^0-9A-F]%'')' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_EvidenceFingerprint', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_MakerChecker' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([ApprovedByUserId] IS NULL OR [ApprovedByUserId]<>[PreparedByUserId])' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_MakerChecker', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_Mode' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([IsDeleted]=(1) OR ([Mode]=(3) OR [Mode]=(2) OR [Mode]=(1)))' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_Mode', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_NoDelete' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([IsDeleted]=(0))' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_NoDelete', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_ReconciliationFingerprint' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'(len([ReconciliationFingerprint])=(64) AND [ReconciliationFingerprint]=rtrim([ReconciliationFingerprint]) AND NOT ([ReconciliationFingerprint]) collate Latin1_General_100_BIN2 like ''%[^0-9A-F]%'')' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_ReconciliationFingerprint', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_SourceShape' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([Mode]=(1) AND [SourceAccountingBookId] IS NULL OR ([Mode]=(3) OR [Mode]=(2)) AND [SourceAccountingBookId] IS NOT NULL AND [SourceAccountingBookId]<>[AccountingBookId])' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_SourceShape', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_Status' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([IsDeleted]=(1) OR ([InitializationStatus]=(4) OR [InitializationStatus]=(3) OR [InitializationStatus]=(2) OR [InitializationStatus]=(1)))' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_Status', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookPeriods') AND c.name=N'CK_AccountingBookPeriods_NoDelete' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([IsDeleted]=(0))' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookPeriods_NoDelete', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookPeriods') AND c.name=N'CK_AccountingBookPeriods_PendingStatus' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([PendingStatus] IS NULL OR ([PendingStatus]=(4) OR [PendingStatus]=(3) OR [PendingStatus]=(2) OR [PendingStatus]=(1)))' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookPeriods_PendingStatus', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookPeriods') AND c.name=N'CK_AccountingBookPeriods_Status' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([IsDeleted]=(1) OR ([PeriodStatus]=(4) OR [PeriodStatus]=(3) OR [PeriodStatus]=(2) OR [PeriodStatus]=(1)))' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookPeriods_Status', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND c.name=N'CK_AccountingBookInitializationLines_Amounts' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([OpeningDebit]>=(0) AND [OpeningCredit]>=(0) AND NOT ([OpeningDebit]>(0) AND [OpeningCredit]>(0)))' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializationLines_Amounts', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND c.name=N'CK_AccountingBookInitializationLines_Currency' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'(len([CurrencyCode])=(3) AND [CurrencyCode]=rtrim([CurrencyCode]) AND ([CurrencyCode]) collate Latin1_General_100_BIN2 like ''[A-Z][A-Z][A-Z]'')' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializationLines_Currency', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND c.name=N'CK_AccountingBookInitializationLines_NoDelete' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([IsDeleted]=(0))' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializationLines_NoDelete', 1;
        """;

    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(ReconciliationSql);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // This is an additive repair of schema already owned by the baseline. Preserve
        // all tables and governance evidence when reverting only this repair migration.
    }
}
