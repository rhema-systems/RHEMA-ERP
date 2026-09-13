using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260721093000_AddFinanceUnitAccountingTables")]
public partial class AddFinanceUnitAccountingTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[UnitTypes]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UnitTypes] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [DecimalPlaces] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_UnitTypes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UnitTypes_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_UnitTypes_IsActive] ON [dbo].[UnitTypes] ([IsActive]);
    CREATE INDEX [IX_UnitTypes_TenantId] ON [dbo].[UnitTypes] ([TenantId]);
    CREATE UNIQUE INDEX [IX_UnitTypes_TenantId_Code] ON [dbo].[UnitTypes] ([TenantId], [Code]);
END;

IF OBJECT_ID(N'[dbo].[UnitAccounts]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UnitAccounts] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [AccountNumber] nvarchar(20) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [UnitTypeId] uniqueidentifier NOT NULL,
        [ParentAccountId] uniqueidentifier NULL,
        [AccountLevel] int NOT NULL,
        [IsPostingAccount] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [CurrentBalance] decimal(18,6) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_UnitAccounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UnitAccounts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UnitAccounts_UnitTypes_UnitTypeId] FOREIGN KEY ([UnitTypeId]) REFERENCES [dbo].[UnitTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UnitAccounts_UnitAccounts_ParentAccountId] FOREIGN KEY ([ParentAccountId]) REFERENCES [dbo].[UnitAccounts] ([Id]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_UnitAccounts_IsActive] ON [dbo].[UnitAccounts] ([IsActive]);
    CREATE INDEX [IX_UnitAccounts_IsPostingAccount] ON [dbo].[UnitAccounts] ([IsPostingAccount]);
    CREATE INDEX [IX_UnitAccounts_ParentAccountId] ON [dbo].[UnitAccounts] ([ParentAccountId]);
    CREATE INDEX [IX_UnitAccounts_TenantId] ON [dbo].[UnitAccounts] ([TenantId]);
    CREATE INDEX [IX_UnitAccounts_UnitTypeId] ON [dbo].[UnitAccounts] ([UnitTypeId]);
    CREATE UNIQUE INDEX [IX_UnitAccounts_TenantId_AccountNumber] ON [dbo].[UnitAccounts] ([TenantId], [AccountNumber]);
END;

IF OBJECT_ID(N'[dbo].[UnitJournalEntries]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UnitJournalEntries] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [EntryNumber] nvarchar(50) NOT NULL,
        [EntryDate] datetime2 NOT NULL,
        [Description] nvarchar(500) NULL,
        [FiscalYearId] uniqueidentifier NOT NULL,
        [FiscalPeriodId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [SourceDocument] nvarchar(200) NULL,
        [ApprovedAt] datetime2 NULL,
        [ApprovedBy] uniqueidentifier NULL,
        [ApprovedByName] nvarchar(100) NULL,
        [RejectionReason] nvarchar(500) NULL,
        [PostedAt] datetime2 NULL,
        [PostedBy] uniqueidentifier NULL,
        [PostedByName] nvarchar(100) NULL,
        [IsReversal] bit NOT NULL,
        [ReversedEntryId] uniqueidentifier NULL,
        [ReversalEntryId] uniqueidentifier NULL,
        [ReversalReason] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_UnitJournalEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UnitJournalEntries_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UnitJournalEntries_FiscalYears_FiscalYearId] FOREIGN KEY ([FiscalYearId]) REFERENCES [dbo].[FiscalYears] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UnitJournalEntries_FiscalPeriods_FiscalPeriodId] FOREIGN KEY ([FiscalPeriodId]) REFERENCES [dbo].[FiscalPeriods] ([Id]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_UnitJournalEntries_EntryDate] ON [dbo].[UnitJournalEntries] ([EntryDate]);
    CREATE INDEX [IX_UnitJournalEntries_FiscalPeriodId] ON [dbo].[UnitJournalEntries] ([FiscalPeriodId]);
    CREATE INDEX [IX_UnitJournalEntries_FiscalYearId] ON [dbo].[UnitJournalEntries] ([FiscalYearId]);
    CREATE INDEX [IX_UnitJournalEntries_Status] ON [dbo].[UnitJournalEntries] ([Status]);
    CREATE INDEX [IX_UnitJournalEntries_TenantId] ON [dbo].[UnitJournalEntries] ([TenantId]);
    CREATE UNIQUE INDEX [IX_UnitJournalEntries_TenantId_EntryNumber] ON [dbo].[UnitJournalEntries] ([TenantId], [EntryNumber]);
END;

IF OBJECT_ID(N'[dbo].[UnitJournalEntryLines]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UnitJournalEntryLines] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [UnitJournalEntryId] uniqueidentifier NOT NULL,
        [LineNumber] int NOT NULL,
        [UnitAccountId] uniqueidentifier NOT NULL,
        [Quantity] decimal(18,6) NOT NULL,
        [Description] nvarchar(300) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_UnitJournalEntryLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UnitJournalEntryLines_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UnitJournalEntryLines_UnitJournalEntries_UnitJournalEntryId] FOREIGN KEY ([UnitJournalEntryId]) REFERENCES [dbo].[UnitJournalEntries] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UnitJournalEntryLines_UnitAccounts_UnitAccountId] FOREIGN KEY ([UnitAccountId]) REFERENCES [dbo].[UnitAccounts] ([Id]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_UnitJournalEntryLines_TenantId] ON [dbo].[UnitJournalEntryLines] ([TenantId]);
    CREATE INDEX [IX_UnitJournalEntryLines_UnitAccountId] ON [dbo].[UnitJournalEntryLines] ([UnitAccountId]);
    CREATE INDEX [IX_UnitJournalEntryLines_UnitJournalEntryId] ON [dbo].[UnitJournalEntryLines] ([UnitJournalEntryId]);
END;

IF OBJECT_ID(N'[dbo].[UnitAccountBalances]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UnitAccountBalances] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [UnitAccountId] uniqueidentifier NOT NULL,
        [FiscalYearId] uniqueidentifier NOT NULL,
        [FiscalPeriodId] uniqueidentifier NOT NULL,
        [OpeningBalance] decimal(18,6) NOT NULL,
        [PeriodActivity] decimal(18,6) NOT NULL,
        [ClosingBalance] decimal(18,6) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_UnitAccountBalances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UnitAccountBalances_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UnitAccountBalances_UnitAccounts_UnitAccountId] FOREIGN KEY ([UnitAccountId]) REFERENCES [dbo].[UnitAccounts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UnitAccountBalances_FiscalYears_FiscalYearId] FOREIGN KEY ([FiscalYearId]) REFERENCES [dbo].[FiscalYears] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UnitAccountBalances_FiscalPeriods_FiscalPeriodId] FOREIGN KEY ([FiscalPeriodId]) REFERENCES [dbo].[FiscalPeriods] ([Id]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_UnitAccountBalances_FiscalPeriodId] ON [dbo].[UnitAccountBalances] ([FiscalPeriodId]);
    CREATE INDEX [IX_UnitAccountBalances_FiscalYearId] ON [dbo].[UnitAccountBalances] ([FiscalYearId]);
    CREATE INDEX [IX_UnitAccountBalances_TenantId] ON [dbo].[UnitAccountBalances] ([TenantId]);
    CREATE UNIQUE INDEX [IX_UnitAccountBalances_UnitAccountId_FiscalPeriodId] ON [dbo].[UnitAccountBalances] ([UnitAccountId], [FiscalPeriodId]);
END;

IF OBJECT_ID(N'[dbo].[RatioDefinitions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[RatioDefinitions] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [NumeratorType] int NOT NULL,
        [NumeratorAccountId] uniqueidentifier NULL,
        [NumeratorConstant] decimal(18,6) NULL,
        [DenominatorType] int NOT NULL,
        [DenominatorAccountId] uniqueidentifier NULL,
        [DenominatorConstant] decimal(18,6) NULL,
        [ResultFormat] int NOT NULL,
        [DecimalPlaces] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_RatioDefinitions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RatioDefinitions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_RatioDefinitions_IsActive] ON [dbo].[RatioDefinitions] ([IsActive]);
    CREATE INDEX [IX_RatioDefinitions_TenantId] ON [dbo].[RatioDefinitions] ([TenantId]);
    CREATE UNIQUE INDEX [IX_RatioDefinitions_TenantId_Code] ON [dbo].[RatioDefinitions] ([TenantId], [Code]);
END;

IF OBJECT_ID(N'[dbo].[UnitAccountBudgets]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UnitAccountBudgets] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [UnitAccountId] uniqueidentifier NOT NULL,
        [FiscalYearId] uniqueidentifier NOT NULL,
        [FiscalPeriodId] uniqueidentifier NOT NULL,
        [BudgetQuantity] decimal(18,6) NOT NULL,
        [Notes] nvarchar(500) NULL,
        [BudgetVersion] nvarchar(50) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_UnitAccountBudgets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UnitAccountBudgets_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UnitAccountBudgets_UnitAccounts_UnitAccountId] FOREIGN KEY ([UnitAccountId]) REFERENCES [dbo].[UnitAccounts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UnitAccountBudgets_FiscalYears_FiscalYearId] FOREIGN KEY ([FiscalYearId]) REFERENCES [dbo].[FiscalYears] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UnitAccountBudgets_FiscalPeriods_FiscalPeriodId] FOREIGN KEY ([FiscalPeriodId]) REFERENCES [dbo].[FiscalPeriods] ([Id]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_UnitAccountBudgets_FiscalPeriodId] ON [dbo].[UnitAccountBudgets] ([FiscalPeriodId]);
    CREATE INDEX [IX_UnitAccountBudgets_FiscalYearId] ON [dbo].[UnitAccountBudgets] ([FiscalYearId]);
    CREATE INDEX [IX_UnitAccountBudgets_IsActive] ON [dbo].[UnitAccountBudgets] ([IsActive]);
    CREATE INDEX [IX_UnitAccountBudgets_TenantId] ON [dbo].[UnitAccountBudgets] ([TenantId]);
    CREATE UNIQUE INDEX [IX_UnitAccountBudgets_UnitAccountId_FiscalPeriodId_BudgetVersion] ON [dbo].[UnitAccountBudgets] ([UnitAccountId], [FiscalPeriodId], [BudgetVersion]);
END;

IF OBJECT_ID(N'[dbo].[AllocationRules]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AllocationRules] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [SourceAccountId] uniqueidentifier NOT NULL,
        [AllocationType] int NOT NULL,
        [DriverUnitAccountId] uniqueidentifier NULL,
        [IsActive] bit NOT NULL,
        [ApprovalStatus] nvarchar(30) NOT NULL,
        [AutoReverse] bit NOT NULL,
        [LastRunDate] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AllocationRules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AllocationRules_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AllocationRules_Accounts_SourceAccountId] FOREIGN KEY ([SourceAccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AllocationRules_UnitAccounts_DriverUnitAccountId] FOREIGN KEY ([DriverUnitAccountId]) REFERENCES [dbo].[UnitAccounts] ([Id]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_AllocationRules_DriverUnitAccountId] ON [dbo].[AllocationRules] ([DriverUnitAccountId]);
    CREATE INDEX [IX_AllocationRules_IsActive] ON [dbo].[AllocationRules] ([IsActive]);
    CREATE INDEX [IX_AllocationRules_SourceAccountId] ON [dbo].[AllocationRules] ([SourceAccountId]);
    CREATE INDEX [IX_AllocationRules_TenantId] ON [dbo].[AllocationRules] ([TenantId]);
    CREATE UNIQUE INDEX [IX_AllocationRules_TenantId_Code] ON [dbo].[AllocationRules] ([TenantId], [Code]);
END;

IF OBJECT_ID(N'[dbo].[AllocationTargets]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AllocationTargets] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [AllocationRuleId] uniqueidentifier NOT NULL,
        [TargetAccountId] uniqueidentifier NOT NULL,
        [FixedPercentage] decimal(5,2) NULL,
        [TargetDriverUnitAccountId] uniqueidentifier NULL,
        [CostCenterCode] nvarchar(50) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AllocationTargets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AllocationTargets_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AllocationTargets_AllocationRules_AllocationRuleId] FOREIGN KEY ([AllocationRuleId]) REFERENCES [dbo].[AllocationRules] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AllocationTargets_Accounts_TargetAccountId] FOREIGN KEY ([TargetAccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AllocationTargets_UnitAccounts_TargetDriverUnitAccountId] FOREIGN KEY ([TargetDriverUnitAccountId]) REFERENCES [dbo].[UnitAccounts] ([Id]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_AllocationTargets_AllocationRuleId] ON [dbo].[AllocationTargets] ([AllocationRuleId]);
    CREATE INDEX [IX_AllocationTargets_TargetAccountId] ON [dbo].[AllocationTargets] ([TargetAccountId]);
    CREATE INDEX [IX_AllocationTargets_TargetDriverUnitAccountId] ON [dbo].[AllocationTargets] ([TargetDriverUnitAccountId]);
    CREATE INDEX [IX_AllocationTargets_TenantId] ON [dbo].[AllocationTargets] ([TenantId]);
END;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[AllocationTargets]', N'U') IS NOT NULL DROP TABLE [dbo].[AllocationTargets];
IF OBJECT_ID(N'[dbo].[AllocationRules]', N'U') IS NOT NULL DROP TABLE [dbo].[AllocationRules];
IF OBJECT_ID(N'[dbo].[UnitAccountBudgets]', N'U') IS NOT NULL DROP TABLE [dbo].[UnitAccountBudgets];
IF OBJECT_ID(N'[dbo].[UnitJournalEntryLines]', N'U') IS NOT NULL DROP TABLE [dbo].[UnitJournalEntryLines];
IF OBJECT_ID(N'[dbo].[UnitJournalEntries]', N'U') IS NOT NULL DROP TABLE [dbo].[UnitJournalEntries];
IF OBJECT_ID(N'[dbo].[RatioDefinitions]', N'U') IS NOT NULL DROP TABLE [dbo].[RatioDefinitions];
IF OBJECT_ID(N'[dbo].[UnitAccountBalances]', N'U') IS NOT NULL DROP TABLE [dbo].[UnitAccountBalances];
IF OBJECT_ID(N'[dbo].[UnitAccounts]', N'U') IS NOT NULL DROP TABLE [dbo].[UnitAccounts];
IF OBJECT_ID(N'[dbo].[UnitTypes]', N'U') IS NOT NULL DROP TABLE [dbo].[UnitTypes];
""");
    }
}
