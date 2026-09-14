using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Completes the Finance-owned AP supplier debit-note lifecycle and adds the immutable
/// debit-note-to-payment/invoice application ledger. The guarded table creation also repairs
/// installations where the historical AddSupplierReturns migration was recorded but its two
/// debit-note tables were not physically present.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260818103000_AddSupplierDebitNoteLifecycleAndApplications")]
public class AddSupplierDebitNoteLifecycleAndApplications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[SupplierDebitNotes]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SupplierDebitNotes]
    (
        [Id] uniqueidentifier NOT NULL,
        [DebitNoteNumber] nvarchar(50) NOT NULL,
        [VendorId] uniqueidentifier NOT NULL,
        [SupplierReturnId] uniqueidentifier NULL,
        [OriginalVendorInvoiceId] uniqueidentifier NULL,
        [DebitNoteDate] datetime2 NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL,
        [ExchangeRate] decimal(18,6) NOT NULL,
        [SubTotal] decimal(18,2) NOT NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [BaseCurrencyAmount] decimal(18,2) NOT NULL,
        [Status] int NOT NULL,
        [JournalEntryId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_SupplierDebitNotes_IsDeleted] DEFAULT (0),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SupplierDebitNotes] PRIMARY KEY ([Id])
    );
END;

IF COL_LENGTH(N'dbo.SupplierDebitNotes', N'SupplierCreditNoteReference') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD [SupplierCreditNoteReference] nvarchar(100) NULL;
IF COL_LENGTH(N'dbo.SupplierDebitNotes', N'Reason') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD [Reason] nvarchar(500) NULL;
IF COL_LENGTH(N'dbo.SupplierDebitNotes', N'Notes') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD [Notes] nvarchar(1000) NULL;
IF COL_LENGTH(N'dbo.SupplierDebitNotes', N'DiscountAmount') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD [DiscountAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_SupplierDebitNotes_DiscountAmount] DEFAULT (0);
IF COL_LENGTH(N'dbo.SupplierDebitNotes', N'PostingEventId') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD [PostingEventId] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.SupplierDebitNotes', N'SubmittedById') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD [SubmittedById] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.SupplierDebitNotes', N'SubmittedAt') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD [SubmittedAt] datetime2 NULL;
IF COL_LENGTH(N'dbo.SupplierDebitNotes', N'WorkflowInstanceId') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD [WorkflowInstanceId] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.SupplierDebitNotes', N'ApprovedById') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD [ApprovedById] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.SupplierDebitNotes', N'ApprovedAt') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD [ApprovedAt] datetime2 NULL;
IF COL_LENGTH(N'dbo.SupplierDebitNotes', N'RejectedById') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD [RejectedById] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.SupplierDebitNotes', N'RejectedAt') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD [RejectedAt] datetime2 NULL;
IF COL_LENGTH(N'dbo.SupplierDebitNotes', N'RejectionReason') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD [RejectionReason] nvarchar(1000) NULL;
IF COL_LENGTH(N'dbo.SupplierDebitNotes', N'ApprovalSource') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD [ApprovalSource] nvarchar(40) NOT NULL CONSTRAINT [DF_SupplierDebitNotes_ApprovalSource] DEFAULT (N'SupplierDebitNoteWorkflow');
IF COL_LENGTH(N'dbo.SupplierDebitNotes', N'ReversalJournalEntryId') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD [ReversalJournalEntryId] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.SupplierDebitNotes', N'ReversalPostingEventId') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD [ReversalPostingEventId] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.SupplierDebitNotes', N'ReversedAt') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD [ReversedAt] datetime2 NULL;
IF COL_LENGTH(N'dbo.SupplierDebitNotes', N'ReversedById') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD [ReversedById] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.SupplierDebitNotes', N'ReversalReason') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD [ReversalReason] nvarchar(1000) NULL;
IF COL_LENGTH(N'dbo.SupplierDebitNotes', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD [RowVersion] rowversion NOT NULL;

-- The approved Daily rate master retains six decimal places. Widen any historical legacy
-- table before the lifecycle starts comparing its immutable snapshot to that evidence.
IF EXISTS
(
    SELECT 1
    FROM sys.columns c
    JOIN sys.types t ON t.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID(N'[dbo].[SupplierDebitNotes]')
      AND c.[name] = N'ExchangeRate'
      AND t.[name] IN (N'decimal', N'numeric')
      AND c.[scale] < 6
)
    ALTER TABLE [dbo].[SupplierDebitNotes] ALTER COLUMN [ExchangeRate] decimal(18,6) NOT NULL;

IF OBJECT_ID(N'[dbo].[SupplierDebitNoteLineItems]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SupplierDebitNoteLineItems]
    (
        [Id] uniqueidentifier NOT NULL,
        [SupplierDebitNoteId] uniqueidentifier NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [TaxGroupId] uniqueidentifier NULL,
        [TaxRate] decimal(18,4) NOT NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        [LineTotal] decimal(18,2) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_SupplierDebitNoteLineItems_IsDeleted] DEFAULT (0),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SupplierDebitNoteLineItems] PRIMARY KEY ([Id])
    );
END;

IF COL_LENGTH(N'dbo.SupplierDebitNoteLineItems', N'OriginalVendorInvoiceLineItemId') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNoteLineItems] ADD [OriginalVendorInvoiceLineItemId] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.SupplierDebitNoteLineItems', N'OriginalFinancePurchaseOrderItemId') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNoteLineItems] ADD [OriginalFinancePurchaseOrderItemId] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.SupplierDebitNoteLineItems', N'GLAccountId') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNoteLineItems] ADD [GLAccountId] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.SupplierDebitNoteLineItems', N'DiscountPercentage') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNoteLineItems] ADD [DiscountPercentage] decimal(18,4) NOT NULL CONSTRAINT [DF_SupplierDebitNoteLineItems_DiscountPercentage] DEFAULT (0);
IF COL_LENGTH(N'dbo.SupplierDebitNoteLineItems', N'DiscountAmount') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNoteLineItems] ADD [DiscountAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_SupplierDebitNoteLineItems_DiscountAmount] DEFAULT (0);

IF OBJECT_ID(N'[dbo].[SupplierDebitNoteApplications]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SupplierDebitNoteApplications]
    (
        [Id] uniqueidentifier NOT NULL,
        [SupplierDebitNoteId] uniqueidentifier NOT NULL,
        [VendorPaymentId] uniqueidentifier NOT NULL,
        [VendorInvoiceId] uniqueidentifier NOT NULL,
        [ApplicationAmount] decimal(18,2) NOT NULL,
        [FunctionalAmount] decimal(18,2) NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL,
        [ExchangeRate] decimal(18,6) NOT NULL,
        [ApplicationDate] datetime2 NOT NULL,
        [Notes] nvarchar(500) NULL,
        [IsReversal] bit NOT NULL,
        [OriginalApplicationId] uniqueidentifier NULL,
        [PaymentPostingEventId] uniqueidentifier NULL,
        [PaymentJournalEntryId] uniqueidentifier NULL,
        [AppliedAt] datetime2 NULL,
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
        CONSTRAINT [PK_SupplierDebitNoteApplications] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_SupplierDebitNoteApplications_Amount]
            CHECK (([IsReversal] = 0 AND [ApplicationAmount] > 0) OR ([IsReversal] = 1 AND [ApplicationAmount] < 0))
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNotes_BusinessPartners_VendorId')
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD CONSTRAINT [FK_SupplierDebitNotes_BusinessPartners_VendorId]
        FOREIGN KEY ([VendorId]) REFERENCES [dbo].[BusinessPartners] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNotes_Tenants_TenantId')
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD CONSTRAINT [FK_SupplierDebitNotes_Tenants_TenantId]
        FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]);
-- SupplierReturns belongs to the quarantined legacy return path. Some historical Finance-only
-- databases legitimately do not contain that optional table even though SupplierDebitNotes does.
-- Do not synthesize or couple the return boundary here; add the FK only when its mapped table is
-- actually present. FIN-INT-012/013 remain Planned.
IF OBJECT_ID(N'[dbo].[SupplierReturns]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNotes_SupplierReturns_SupplierReturnId')
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD CONSTRAINT [FK_SupplierDebitNotes_SupplierReturns_SupplierReturnId]
        FOREIGN KEY ([SupplierReturnId]) REFERENCES [dbo].[SupplierReturns] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNotes_VendorInvoice_OriginalVendorInvoiceId')
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD CONSTRAINT [FK_SupplierDebitNotes_VendorInvoice_OriginalVendorInvoiceId]
        FOREIGN KEY ([OriginalVendorInvoiceId]) REFERENCES [dbo].[VendorInvoice] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNotes_JournalEntries_JournalEntryId')
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD CONSTRAINT [FK_SupplierDebitNotes_JournalEntries_JournalEntryId]
        FOREIGN KEY ([JournalEntryId]) REFERENCES [dbo].[JournalEntries] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNotes_FinancePostingEvents_PostingEventId')
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD CONSTRAINT [FK_SupplierDebitNotes_FinancePostingEvents_PostingEventId]
        FOREIGN KEY ([PostingEventId]) REFERENCES [dbo].[FinancePostingEvents] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNotes_WorkflowInstances_WorkflowInstanceId')
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD CONSTRAINT [FK_SupplierDebitNotes_WorkflowInstances_WorkflowInstanceId]
        FOREIGN KEY ([WorkflowInstanceId]) REFERENCES [dbo].[WorkflowInstances] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNotes_JournalEntries_ReversalJournalEntryId')
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD CONSTRAINT [FK_SupplierDebitNotes_JournalEntries_ReversalJournalEntryId]
        FOREIGN KEY ([ReversalJournalEntryId]) REFERENCES [dbo].[JournalEntries] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNotes_FinancePostingEvents_ReversalPostingEventId')
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD CONSTRAINT [FK_SupplierDebitNotes_FinancePostingEvents_ReversalPostingEventId]
        FOREIGN KEY ([ReversalPostingEventId]) REFERENCES [dbo].[FinancePostingEvents] ([Id]);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNoteLineItems_SupplierDebitNotes_SupplierDebitNoteId')
    ALTER TABLE [dbo].[SupplierDebitNoteLineItems] ADD CONSTRAINT [FK_SupplierDebitNoteLineItems_SupplierDebitNotes_SupplierDebitNoteId]
        FOREIGN KEY ([SupplierDebitNoteId]) REFERENCES [dbo].[SupplierDebitNotes] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNoteLineItems_Accounts_GLAccountId')
    ALTER TABLE [dbo].[SupplierDebitNoteLineItems] ADD CONSTRAINT [FK_SupplierDebitNoteLineItems_Accounts_GLAccountId]
        FOREIGN KEY ([GLAccountId]) REFERENCES [dbo].[Accounts] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNoteLineItems_Tenants_TenantId')
    ALTER TABLE [dbo].[SupplierDebitNoteLineItems] ADD CONSTRAINT [FK_SupplierDebitNoteLineItems_Tenants_TenantId]
        FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNoteApplications_SupplierDebitNotes_SupplierDebitNoteId')
    ALTER TABLE [dbo].[SupplierDebitNoteApplications] ADD CONSTRAINT [FK_SupplierDebitNoteApplications_SupplierDebitNotes_SupplierDebitNoteId]
        FOREIGN KEY ([SupplierDebitNoteId]) REFERENCES [dbo].[SupplierDebitNotes] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNoteApplications_VendorPayment_VendorPaymentId')
    ALTER TABLE [dbo].[SupplierDebitNoteApplications] ADD CONSTRAINT [FK_SupplierDebitNoteApplications_VendorPayment_VendorPaymentId]
        FOREIGN KEY ([VendorPaymentId]) REFERENCES [dbo].[VendorPayment] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNoteApplications_VendorInvoice_VendorInvoiceId')
    ALTER TABLE [dbo].[SupplierDebitNoteApplications] ADD CONSTRAINT [FK_SupplierDebitNoteApplications_VendorInvoice_VendorInvoiceId]
        FOREIGN KEY ([VendorInvoiceId]) REFERENCES [dbo].[VendorInvoice] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNoteApplications_FinancePostingEvents_PaymentPostingEventId')
    ALTER TABLE [dbo].[SupplierDebitNoteApplications] ADD CONSTRAINT [FK_SupplierDebitNoteApplications_FinancePostingEvents_PaymentPostingEventId]
        FOREIGN KEY ([PaymentPostingEventId]) REFERENCES [dbo].[FinancePostingEvents] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNoteApplications_JournalEntries_PaymentJournalEntryId')
    ALTER TABLE [dbo].[SupplierDebitNoteApplications] ADD CONSTRAINT [FK_SupplierDebitNoteApplications_JournalEntries_PaymentJournalEntryId]
        FOREIGN KEY ([PaymentJournalEntryId]) REFERENCES [dbo].[JournalEntries] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNoteApplications_Tenants_TenantId')
    ALTER TABLE [dbo].[SupplierDebitNoteApplications] ADD CONSTRAINT [FK_SupplierDebitNoteApplications_Tenants_TenantId]
        FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'UX_SupplierDebitNotes_Tenant_DebitNoteNumber')
    EXEC(N'CREATE UNIQUE INDEX [UX_SupplierDebitNotes_Tenant_DebitNoteNumber]
        ON [dbo].[SupplierDebitNotes] ([TenantId], [DebitNoteNumber]);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'UX_SupplierDebitNotes_Tenant_Vendor_SupplierReference')
    EXEC(N'CREATE UNIQUE INDEX [UX_SupplierDebitNotes_Tenant_Vendor_SupplierReference]
        ON [dbo].[SupplierDebitNotes] ([TenantId], [VendorId], [SupplierCreditNoteReference])
        WHERE [SupplierCreditNoteReference] IS NOT NULL AND [IsDeleted] = 0;');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SupplierDebitNotes_Tenant_Status_Date')
    EXEC(N'CREATE INDEX [IX_SupplierDebitNotes_Tenant_Status_Date]
        ON [dbo].[SupplierDebitNotes] ([TenantId], [Status], [DebitNoteDate]);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SupplierDebitNoteLineItems_Tenant_Note')
    EXEC(N'CREATE INDEX [IX_SupplierDebitNoteLineItems_Tenant_Note]
        ON [dbo].[SupplierDebitNoteLineItems] ([TenantId], [SupplierDebitNoteId]);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SupplierDebitNoteLineItems_GLAccountId')
    EXEC(N'CREATE INDEX [IX_SupplierDebitNoteLineItems_GLAccountId]
        ON [dbo].[SupplierDebitNoteLineItems] ([GLAccountId]);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SupplierDebitNoteApplications_Tenant_Note_Payment_Invoice')
    EXEC(N'CREATE INDEX [IX_SupplierDebitNoteApplications_Tenant_Note_Payment_Invoice]
        ON [dbo].[SupplierDebitNoteApplications] ([TenantId], [SupplierDebitNoteId], [VendorPaymentId], [VendorInvoiceId]);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'UX_SupplierDebitNoteApplication_Tenant_Original_Reversal')
    EXEC(N'CREATE UNIQUE INDEX [UX_SupplierDebitNoteApplication_Tenant_Original_Reversal]
        ON [dbo].[SupplierDebitNoteApplications] ([TenantId], [OriginalApplicationId])
        WHERE [IsReversal] = 1 AND [OriginalApplicationId] IS NOT NULL;');
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        RejectDowngrade();

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[SupplierDebitNoteApplications]', N'U') IS NOT NULL
    DROP TABLE [dbo].[SupplierDebitNoteApplications];

IF OBJECT_ID(N'[dbo].[SupplierDebitNoteLineItems]', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNoteLineItems_Accounts_GLAccountId')
        ALTER TABLE [dbo].[SupplierDebitNoteLineItems] DROP CONSTRAINT [FK_SupplierDebitNoteLineItems_Accounts_GLAccountId];
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SupplierDebitNoteLineItems_GLAccountId')
        DROP INDEX [IX_SupplierDebitNoteLineItems_GLAccountId] ON [dbo].[SupplierDebitNoteLineItems];
    DECLARE @lineDefault sysname;
    SELECT @lineDefault = dc.[name]
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.[object_id] = dc.[parent_object_id] AND c.[column_id] = dc.[parent_column_id]
    WHERE dc.[parent_object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNoteLineItems]')
      AND c.[name] = N'DiscountPercentage';
    IF @lineDefault IS NOT NULL EXEC(N'ALTER TABLE [dbo].[SupplierDebitNoteLineItems] DROP CONSTRAINT [' + @lineDefault + N']');
    SET @lineDefault = NULL;
    SELECT @lineDefault = dc.[name]
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.[object_id] = dc.[parent_object_id] AND c.[column_id] = dc.[parent_column_id]
    WHERE dc.[parent_object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNoteLineItems]')
      AND c.[name] = N'DiscountAmount';
    IF @lineDefault IS NOT NULL EXEC(N'ALTER TABLE [dbo].[SupplierDebitNoteLineItems] DROP CONSTRAINT [' + @lineDefault + N']');
    IF COL_LENGTH(N'dbo.SupplierDebitNoteLineItems', N'OriginalVendorInvoiceLineItemId') IS NOT NULL
        ALTER TABLE [dbo].[SupplierDebitNoteLineItems] DROP COLUMN [OriginalVendorInvoiceLineItemId];
    IF COL_LENGTH(N'dbo.SupplierDebitNoteLineItems', N'OriginalFinancePurchaseOrderItemId') IS NOT NULL
        ALTER TABLE [dbo].[SupplierDebitNoteLineItems] DROP COLUMN [OriginalFinancePurchaseOrderItemId];
    IF COL_LENGTH(N'dbo.SupplierDebitNoteLineItems', N'GLAccountId') IS NOT NULL
        ALTER TABLE [dbo].[SupplierDebitNoteLineItems] DROP COLUMN [GLAccountId];
    IF COL_LENGTH(N'dbo.SupplierDebitNoteLineItems', N'DiscountPercentage') IS NOT NULL
        ALTER TABLE [dbo].[SupplierDebitNoteLineItems] DROP COLUMN [DiscountPercentage];
    IF COL_LENGTH(N'dbo.SupplierDebitNoteLineItems', N'DiscountAmount') IS NOT NULL
        ALTER TABLE [dbo].[SupplierDebitNoteLineItems] DROP COLUMN [DiscountAmount];
END;

IF OBJECT_ID(N'[dbo].[SupplierDebitNotes]', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'UX_SupplierDebitNotes_Tenant_Vendor_SupplierReference')
        DROP INDEX [UX_SupplierDebitNotes_Tenant_Vendor_SupplierReference] ON [dbo].[SupplierDebitNotes];
    DECLARE @constraints TABLE ([Name] sysname);
    INSERT INTO @constraints ([Name])
    SELECT [name] FROM sys.foreign_keys
    WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNotes]')
      AND [name] IN
      (
        N'FK_SupplierDebitNotes_JournalEntries_JournalEntryId',
        N'FK_SupplierDebitNotes_FinancePostingEvents_PostingEventId',
        N'FK_SupplierDebitNotes_WorkflowInstances_WorkflowInstanceId',
        N'FK_SupplierDebitNotes_JournalEntries_ReversalJournalEntryId',
        N'FK_SupplierDebitNotes_FinancePostingEvents_ReversalPostingEventId'
      );
    DECLARE @name sysname;
    WHILE EXISTS (SELECT 1 FROM @constraints)
    BEGIN
        SELECT TOP (1) @name = [Name] FROM @constraints;
        EXEC(N'ALTER TABLE [dbo].[SupplierDebitNotes] DROP CONSTRAINT [' + @name + N']');
        DELETE FROM @constraints WHERE [Name] = @name;
    END;

    DECLARE @columns TABLE ([Name] sysname);
    INSERT INTO @columns ([Name]) VALUES
        (N'SupplierCreditNoteReference'), (N'Reason'), (N'Notes'), (N'DiscountAmount'),
        (N'PostingEventId'), (N'SubmittedById'), (N'SubmittedAt'), (N'WorkflowInstanceId'),
        (N'ApprovedById'), (N'ApprovedAt'), (N'RejectedById'), (N'RejectedAt'),
        (N'RejectionReason'), (N'ApprovalSource'), (N'ReversalJournalEntryId'),
        (N'ReversalPostingEventId'), (N'ReversedAt'), (N'ReversedById'),
        (N'ReversalReason'), (N'RowVersion');
    WHILE EXISTS (SELECT 1 FROM @columns)
    BEGIN
        SELECT TOP (1) @name = [Name] FROM @columns;
        IF COL_LENGTH(N'dbo.SupplierDebitNotes', @name) IS NOT NULL
        BEGIN
            DECLARE @defaultName sysname = NULL;
            SELECT @defaultName = dc.[name]
            FROM sys.default_constraints dc
            JOIN sys.columns c ON c.[object_id] = dc.[parent_object_id] AND c.[column_id] = dc.[parent_column_id]
            WHERE dc.[parent_object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNotes]')
              AND c.[name] = @name;
            IF @defaultName IS NOT NULL
                EXEC(N'ALTER TABLE [dbo].[SupplierDebitNotes] DROP CONSTRAINT [' + @defaultName + N']');
            EXEC(N'ALTER TABLE [dbo].[SupplierDebitNotes] DROP COLUMN [' + @name + N']');
        END;
        DELETE FROM @columns WHERE [Name] = @name;
    END;
END;
""");
    }

    private static void RejectDowngrade() => throw new NotSupportedException(
        "Supplier debit-note lifecycle downgrades are blocked because they would delete immutable approval, posting and settlement evidence.");
}
