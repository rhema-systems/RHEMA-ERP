using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260728170400_AddAllocationRunBatches")]
public partial class AddAllocationRunBatches : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[AllocationRunBatches]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AllocationRunBatches] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [BatchNumber] nvarchar(50) NOT NULL,
        [AllocationRuleId] uniqueidentifier NOT NULL,
        [FiscalPeriodId] uniqueidentifier NOT NULL,
        [AllocationDate] datetime2 NOT NULL,
        [Description] nvarchar(500) NULL,
        [Status] int NOT NULL,
        [SourceAccountId] uniqueidentifier NOT NULL,
        [SourcePeriodBalance] decimal(18,2) NOT NULL,
        [TotalAllocated] decimal(18,2) NOT NULL,
        [AllocationType] nvarchar(30) NOT NULL,
        [BookClassification] nvarchar(20) NOT NULL,
        [FunctionalCurrencyCode] nvarchar(10) NOT NULL,
        [WorkflowInstanceId] uniqueidentifier NULL,
        [JournalEntryId] uniqueidentifier NULL,
        [JournalEntryNumber] nvarchar(50) NULL,
        [SubmittedAt] datetime2 NULL,
        [SubmittedBy] uniqueidentifier NULL,
        [SubmittedByName] nvarchar(100) NULL,
        [ApprovedAt] datetime2 NULL,
        [ApprovedBy] uniqueidentifier NULL,
        [ApprovedByName] nvarchar(100) NULL,
        [PostedAt] datetime2 NULL,
        [PostedBy] uniqueidentifier NULL,
        [PostedByName] nvarchar(100) NULL,
        [RejectionReason] nvarchar(500) NULL,
        [IdempotencyKey] nvarchar(200) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AllocationRunBatches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AllocationRunBatches_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AllocationRunBatches_AllocationRules_AllocationRuleId] FOREIGN KEY ([AllocationRuleId]) REFERENCES [dbo].[AllocationRules] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AllocationRunBatches_FiscalPeriods_FiscalPeriodId] FOREIGN KEY ([FiscalPeriodId]) REFERENCES [dbo].[FiscalPeriods] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AllocationRunBatches_Accounts_SourceAccountId] FOREIGN KEY ([SourceAccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION
    );
END;

IF OBJECT_ID(N'[dbo].[AllocationRunBatchLines]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AllocationRunBatchLines] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [AllocationRunBatchId] uniqueidentifier NOT NULL,
        [LineNumber] int NOT NULL,
        [TargetAccountId] uniqueidentifier NOT NULL,
        [TargetDriverUnitAccountId] uniqueidentifier NULL,
        [AllocationBasis] decimal(18,6) NOT NULL,
        [AllocationPercent] decimal(9,4) NOT NULL,
        [AllocatedAmount] decimal(18,2) NOT NULL,
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
        CONSTRAINT [PK_AllocationRunBatchLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AllocationRunBatchLines_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AllocationRunBatchLines_AllocationRunBatches_AllocationRunBatchId] FOREIGN KEY ([AllocationRunBatchId]) REFERENCES [dbo].[AllocationRunBatches] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AllocationRunBatchLines_Accounts_TargetAccountId] FOREIGN KEY ([TargetAccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AllocationRunBatchLines_UnitAccounts_TargetDriverUnitAccountId] FOREIGN KEY ([TargetDriverUnitAccountId]) REFERENCES [dbo].[UnitAccounts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AllocationRunBatches_TenantId_BatchNumber' AND object_id = OBJECT_ID(N'[dbo].[AllocationRunBatches]'))
    CREATE UNIQUE INDEX [IX_AllocationRunBatches_TenantId_BatchNumber] ON [dbo].[AllocationRunBatches] ([TenantId], [BatchNumber]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AllocationRunBatches_TenantId_AllocationRuleId_FiscalPeriodId_Status' AND object_id = OBJECT_ID(N'[dbo].[AllocationRunBatches]'))
    CREATE INDEX [IX_AllocationRunBatches_TenantId_AllocationRuleId_FiscalPeriodId_Status] ON [dbo].[AllocationRunBatches] ([TenantId], [AllocationRuleId], [FiscalPeriodId], [Status]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AllocationRunBatches_WorkflowInstanceId' AND object_id = OBJECT_ID(N'[dbo].[AllocationRunBatches]'))
    CREATE INDEX [IX_AllocationRunBatches_WorkflowInstanceId] ON [dbo].[AllocationRunBatches] ([WorkflowInstanceId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AllocationRunBatches_JournalEntryId' AND object_id = OBJECT_ID(N'[dbo].[AllocationRunBatches]'))
    CREATE INDEX [IX_AllocationRunBatches_JournalEntryId] ON [dbo].[AllocationRunBatches] ([JournalEntryId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AllocationRunBatches_IdempotencyKey' AND object_id = OBJECT_ID(N'[dbo].[AllocationRunBatches]'))
    CREATE UNIQUE INDEX [IX_AllocationRunBatches_IdempotencyKey] ON [dbo].[AllocationRunBatches] ([IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AllocationRunBatches_TenantId' AND object_id = OBJECT_ID(N'[dbo].[AllocationRunBatches]'))
    CREATE INDEX [IX_AllocationRunBatches_TenantId] ON [dbo].[AllocationRunBatches] ([TenantId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AllocationRunBatches_AllocationRuleId' AND object_id = OBJECT_ID(N'[dbo].[AllocationRunBatches]'))
    CREATE INDEX [IX_AllocationRunBatches_AllocationRuleId] ON [dbo].[AllocationRunBatches] ([AllocationRuleId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AllocationRunBatches_FiscalPeriodId' AND object_id = OBJECT_ID(N'[dbo].[AllocationRunBatches]'))
    CREATE INDEX [IX_AllocationRunBatches_FiscalPeriodId] ON [dbo].[AllocationRunBatches] ([FiscalPeriodId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AllocationRunBatches_SourceAccountId' AND object_id = OBJECT_ID(N'[dbo].[AllocationRunBatches]'))
    CREATE INDEX [IX_AllocationRunBatches_SourceAccountId] ON [dbo].[AllocationRunBatches] ([SourceAccountId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AllocationRunBatchLines_AllocationRunBatchId' AND object_id = OBJECT_ID(N'[dbo].[AllocationRunBatchLines]'))
    CREATE INDEX [IX_AllocationRunBatchLines_AllocationRunBatchId] ON [dbo].[AllocationRunBatchLines] ([AllocationRunBatchId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AllocationRunBatchLines_TargetAccountId' AND object_id = OBJECT_ID(N'[dbo].[AllocationRunBatchLines]'))
    CREATE INDEX [IX_AllocationRunBatchLines_TargetAccountId] ON [dbo].[AllocationRunBatchLines] ([TargetAccountId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AllocationRunBatchLines_TargetDriverUnitAccountId' AND object_id = OBJECT_ID(N'[dbo].[AllocationRunBatchLines]'))
    CREATE INDEX [IX_AllocationRunBatchLines_TargetDriverUnitAccountId] ON [dbo].[AllocationRunBatchLines] ([TargetDriverUnitAccountId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AllocationRunBatchLines_TenantId' AND object_id = OBJECT_ID(N'[dbo].[AllocationRunBatchLines]'))
    CREATE INDEX [IX_AllocationRunBatchLines_TenantId] ON [dbo].[AllocationRunBatchLines] ([TenantId]);
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[AllocationRunBatchLines]', N'U') IS NOT NULL DROP TABLE [dbo].[AllocationRunBatchLines];
IF OBJECT_ID(N'[dbo].[AllocationRunBatches]', N'U') IS NOT NULL DROP TABLE [dbo].[AllocationRunBatches];
""");
    }
}
