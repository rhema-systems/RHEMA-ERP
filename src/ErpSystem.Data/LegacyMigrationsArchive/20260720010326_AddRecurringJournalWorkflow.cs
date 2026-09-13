using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

public partial class AddRecurringJournalWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[RecurringJournalTemplates]', N'U') IS NULL
BEGIN
    CREATE TABLE [RecurringJournalTemplates] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [TemplateNumber] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [JournalType] nvarchar(50) NOT NULL,
        [BookClassification] nvarchar(20) NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL,
        [ReferencePattern] nvarchar(200) NULL,
        [Notes] nvarchar(2000) NULL,
        [Status] int NOT NULL,
        [Version] int NOT NULL,
        [DefinitionKey] uniqueidentifier NOT NULL,
        [SupersedesTemplateId] uniqueidentifier NULL,
        [EffectiveFrom] date NOT NULL,
        [EndDate] date NULL,
        [MaximumOccurrences] int NULL,
        [GeneratedOccurrenceCount] int NOT NULL,
        [TimeZoneId] nvarchar(100) NOT NULL,
        [Frequency] int NOT NULL,
        [Interval] int NOT NULL,
        [RecurrenceRuleJson] nvarchar(max) NOT NULL,
        [BusinessDayConvention] int NOT NULL,
        [NextDueDate] date NULL,
        [LastGeneratedDueDate] date NULL,
        [AutoReverse] bit NOT NULL,
        [ReversalRule] int NOT NULL,
        [ReversalDayOffset] int NULL,
        [OwnerUserId] uniqueidentifier NULL,
        [ActivatedAt] datetime2 NULL,
        [ActivatedByUserId] uniqueidentifier NULL,
        [ConsecutiveFailureCount] int NOT NULL,
        [LastFailure] nvarchar(2000) NULL,
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
        CONSTRAINT [PK_RecurringJournalTemplates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RecurringJournalTemplates_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id])
    );
    CREATE UNIQUE INDEX [IX_RecurringJournalTemplates_TenantId_TemplateNumber] ON [RecurringJournalTemplates] ([TenantId], [TemplateNumber]) WHERE [IsDeleted] = 0;
    CREATE UNIQUE INDEX [IX_RecurringJournalTemplates_TenantId_DefinitionKey_Version] ON [RecurringJournalTemplates] ([TenantId], [DefinitionKey], [Version]) WHERE [IsDeleted] = 0;
    CREATE INDEX [IX_RecurringJournalTemplates_TenantId_Status_NextDueDate] ON [RecurringJournalTemplates] ([TenantId], [Status], [NextDueDate]);
END;

IF OBJECT_ID(N'[RecurringJournalTemplateLines]', N'U') IS NULL
BEGIN
    CREATE TABLE [RecurringJournalTemplateLines] (
        [Id] uniqueidentifier NOT NULL, [TenantId] uniqueidentifier NOT NULL,
        [TemplateId] uniqueidentifier NOT NULL, [LineNumber] int NOT NULL,
        [AccountId] uniqueidentifier NOT NULL, [IsDebit] bit NOT NULL,
        [FixedAmount] decimal(18,2) NOT NULL, [AllocationPercentage] decimal(9,6) NULL,
        [IsRoundingResidualLine] bit NOT NULL, [Description] nvarchar(500) NULL,
        [DimensionValuesJson] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL, [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL, [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL, [DeletedAt] datetime2 NULL, [DeletedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_RecurringJournalTemplateLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RecurringJournalTemplateLines_RecurringJournalTemplates_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [RecurringJournalTemplates] ([Id]),
        CONSTRAINT [FK_RecurringJournalTemplateLines_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]),
        CONSTRAINT [FK_RecurringJournalTemplateLines_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id])
    );
    CREATE UNIQUE INDEX [IX_RecurringJournalTemplateLines_TenantId_TemplateId_LineNumber] ON [RecurringJournalTemplateLines] ([TenantId], [TemplateId], [LineNumber]) WHERE [IsDeleted] = 0;
    CREATE INDEX [IX_RecurringJournalTemplateLines_AccountId] ON [RecurringJournalTemplateLines] ([AccountId]);
    CREATE INDEX [IX_RecurringJournalTemplateLines_TemplateId] ON [RecurringJournalTemplateLines] ([TemplateId]);
END;

IF OBJECT_ID(N'[RecurringJournalOccurrences]', N'U') IS NULL
BEGIN
    CREATE TABLE [RecurringJournalOccurrences] (
        [Id] uniqueidentifier NOT NULL, [TenantId] uniqueidentifier NOT NULL,
        [TemplateId] uniqueidentifier NOT NULL, [TemplateVersion] int NOT NULL,
        [SequenceNumber] int NOT NULL, [ScheduledDate] date NOT NULL, [EffectiveDate] date NOT NULL,
        [Status] int NOT NULL, [JournalEntryId] uniqueidentifier NULL, [ReversalJournalEntryId] uniqueidentifier NULL,
        [WorkflowInstanceId] uniqueidentifier NULL, [AttemptCount] int NOT NULL, [GeneratedAt] datetime2 NULL,
        [ErrorMessage] nvarchar(2000) NULL, [AdjustmentExplanation] nvarchar(1000) NULL,
        [TemplateSnapshotJson] nvarchar(max) NOT NULL, [WaivedAt] datetime2 NULL,
        [WaivedByUserId] uniqueidentifier NULL, [WaiverReason] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL, [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL, [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL, [DeletedAt] datetime2 NULL, [DeletedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_RecurringJournalOccurrences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RecurringJournalOccurrences_RecurringJournalTemplates_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [RecurringJournalTemplates] ([Id]),
        CONSTRAINT [FK_RecurringJournalOccurrences_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [JournalEntries] ([Id]),
        CONSTRAINT [FK_RecurringJournalOccurrences_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id])
    );
    CREATE UNIQUE INDEX [IX_RecurringJournalOccurrences_TenantId_TemplateId_ScheduledDate] ON [RecurringJournalOccurrences] ([TenantId], [TemplateId], [ScheduledDate]) WHERE [IsDeleted] = 0;
    CREATE INDEX [IX_RecurringJournalOccurrences_TenantId_Status_EffectiveDate] ON [RecurringJournalOccurrences] ([TenantId], [Status], [EffectiveDate]);
    CREATE UNIQUE INDEX [IX_RecurringJournalOccurrences_JournalEntryId] ON [RecurringJournalOccurrences] ([JournalEntryId]) WHERE [JournalEntryId] IS NOT NULL;
END;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "RecurringJournalOccurrences");
        migrationBuilder.DropTable(name: "RecurringJournalTemplateLines");
        migrationBuilder.DropTable(name: "RecurringJournalTemplates");
    }
}
