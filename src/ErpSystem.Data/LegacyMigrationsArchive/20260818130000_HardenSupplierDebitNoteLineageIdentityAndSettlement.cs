using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds the immutable Finance-owned source-line, tax, AP-supplier identity and settlement
/// evidence required by supplier debit notes. This is deliberately additive and guarded because
/// 20260818103000 may already be present in shared developer and UAT databases. It never writes
/// Procurement/Inventory return records; FIN-INT-012/013 remain quarantined.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260818130000_HardenSupplierDebitNoteLineageIdentityAndSettlement")]
public class HardenSupplierDebitNoteLineageIdentityAndSettlement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
/* Immutable journal and tax lineage. */
IF COL_LENGTH(N'dbo.AccountTransactions', N'SourceDocumentLineId') IS NULL
    ALTER TABLE [dbo].[AccountTransactions] ADD [SourceDocumentLineId] uniqueidentifier NULL;

IF COL_LENGTH(N'dbo.TaxCalculations', N'DocumentLineId') IS NULL
    ALTER TABLE [dbo].[TaxCalculations] ADD [DocumentLineId] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.TaxCalculations', N'PostingAccountId') IS NULL
    ALTER TABLE [dbo].[TaxCalculations] ADD [PostingAccountId] uniqueidentifier NULL;

/* Canonical AP supplier identity on Finance's debit-note document. */
IF COL_LENGTH(N'dbo.SupplierDebitNotes', N'SupplierId') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD [SupplierId] uniqueidentifier NULL;

/* Frozen source-account evidence per debit-note line. */
IF COL_LENGTH(N'dbo.SupplierDebitNoteLineItems', N'ResolvedCreditAccountId') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNoteLineItems] ADD [ResolvedCreditAccountId] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.SupplierDebitNoteLineItems', N'OriginalAccountTransactionId') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNoteLineItems] ADD [OriginalAccountTransactionId] uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.SupplierDebitNoteLineItems', N'LineItemType') IS NULL
    ALTER TABLE [dbo].[SupplierDebitNoteLineItems] ADD [LineItemType] nvarchar(20) NULL;

/* Server-derived, immutable tax components. */
IF OBJECT_ID(N'[dbo].[SupplierDebitNoteTaxComponents]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SupplierDebitNoteTaxComponents]
    (
        [Id] uniqueidentifier NOT NULL,
        [SupplierDebitNoteLineItemId] uniqueidentifier NOT NULL,
        [TaxId] uniqueidentifier NOT NULL,
        [TaxGroupId] uniqueidentifier NULL,
        [OriginalTaxCalculationId] uniqueidentifier NULL,
        [OriginalAccountTransactionId] uniqueidentifier NULL,
        [ResolvedCreditAccountId] uniqueidentifier NOT NULL,
        [BaseAmount] decimal(18,2) NOT NULL,
        [TaxableAmount] decimal(18,2) NOT NULL,
        [TaxRate] decimal(18,4) NOT NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        [CompoundBasis] int NOT NULL,
        [CalculationOrder] int NOT NULL,
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
        CONSTRAINT [PK_SupplierDebitNoteTaxComponents] PRIMARY KEY ([Id])
    );
END;

/* Finance-owned bridge only; the two source masters remain owned by their modules. */
IF OBJECT_ID(N'[dbo].[ApSupplierIdentityLinks]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ApSupplierIdentityLinks]
    (
        [Id] uniqueidentifier NOT NULL,
        [BusinessPartnerId] uniqueidentifier NOT NULL,
        [SupplierId] uniqueidentifier NOT NULL,
        [MappingSource] nvarchar(40) NOT NULL,
        [IsVerified] bit NOT NULL,
        [VerifiedAtUtc] datetime2 NULL,
        [VerifiedById] uniqueidentifier NULL,
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
        CONSTRAINT [PK_ApSupplierIdentityLinks] PRIMARY KEY ([Id])
    );
END;
""", suppressTransaction: false);

        migrationBuilder.Sql("""
/*
 * SQL Server compiles a raw-SQL migration operation as one batch. Keep every use of the
 * columns/tables added above in this later operation so fresh and historical databases see
 * the new metadata before foreign keys, backfills and indexes are compiled.
 *
 * A legacy line without immutable classification is deliberately left NULL. Finance cannot
 * infer Inventory versus Expense after the fact; the debit-note lifecycle fails closed until
 * that evidence is explicitly repaired. Empty/current tables can safely enforce the model's
 * NOT NULL constraint.
 */
IF OBJECT_ID(N'[dbo].[SupplierDebitNoteLineItems]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.SupplierDebitNoteLineItems', N'LineItemType') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM [dbo].[SupplierDebitNoteLineItems]
       WHERE [LineItemType] IS NULL
   )
    ALTER TABLE [dbo].[SupplierDebitNoteLineItems] ALTER COLUMN [LineItemType] nvarchar(20) NOT NULL;

/* Foreign keys are guarded for databases that partially applied an earlier development build. */
IF EXISTS
(
    SELECT 1 FROM sys.foreign_keys
    WHERE [name] = N'FK_SupplierDebitNotes_BusinessPartners_VendorId'
      AND [delete_referential_action] <> 0
)
    ALTER TABLE [dbo].[SupplierDebitNotes]
        DROP CONSTRAINT [FK_SupplierDebitNotes_BusinessPartners_VendorId];
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNotes_BusinessPartners_VendorId')
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD CONSTRAINT [FK_SupplierDebitNotes_BusinessPartners_VendorId]
        FOREIGN KEY ([VendorId]) REFERENCES [dbo].[BusinessPartners] ([Id]);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_TaxCalculations_Accounts_PostingAccountId')
    ALTER TABLE [dbo].[TaxCalculations] ADD CONSTRAINT [FK_TaxCalculations_Accounts_PostingAccountId]
        FOREIGN KEY ([PostingAccountId]) REFERENCES [dbo].[Accounts] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNotes_Suppliers_SupplierId')
    ALTER TABLE [dbo].[SupplierDebitNotes] ADD CONSTRAINT [FK_SupplierDebitNotes_Suppliers_SupplierId]
        FOREIGN KEY ([SupplierId]) REFERENCES [dbo].[Suppliers] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNoteLineItems_Accounts_ResolvedCreditAccountId')
    ALTER TABLE [dbo].[SupplierDebitNoteLineItems] ADD CONSTRAINT [FK_SupplierDebitNoteLineItems_Accounts_ResolvedCreditAccountId]
        FOREIGN KEY ([ResolvedCreditAccountId]) REFERENCES [dbo].[Accounts] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNoteLineItems_AccountTransactions_OriginalAccountTransactionId')
    ALTER TABLE [dbo].[SupplierDebitNoteLineItems] ADD CONSTRAINT [FK_SupplierDebitNoteLineItems_AccountTransactions_OriginalAccountTransactionId]
        FOREIGN KEY ([OriginalAccountTransactionId]) REFERENCES [dbo].[AccountTransactions] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNoteLineItems_VendorInvoiceLineItem_OriginalVendorInvoiceLineItemId')
    ALTER TABLE [dbo].[SupplierDebitNoteLineItems] ADD CONSTRAINT [FK_SupplierDebitNoteLineItems_VendorInvoiceLineItem_OriginalVendorInvoiceLineItemId]
        FOREIGN KEY ([OriginalVendorInvoiceLineItemId]) REFERENCES [dbo].[VendorInvoiceLineItem] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNoteLineItems_FinancePurchaseOrderItems_OriginalFinancePurchaseOrderItemId')
    ALTER TABLE [dbo].[SupplierDebitNoteLineItems] ADD CONSTRAINT [FK_SupplierDebitNoteLineItems_FinancePurchaseOrderItems_OriginalFinancePurchaseOrderItemId]
        FOREIGN KEY ([OriginalFinancePurchaseOrderItemId]) REFERENCES [dbo].[FinancePurchaseOrderItems] ([Id]);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNoteTaxComponents_SupplierDebitNoteLineItems_SupplierDebitNoteLineItemId')
    ALTER TABLE [dbo].[SupplierDebitNoteTaxComponents] ADD CONSTRAINT [FK_SupplierDebitNoteTaxComponents_SupplierDebitNoteLineItems_SupplierDebitNoteLineItemId]
        FOREIGN KEY ([SupplierDebitNoteLineItemId]) REFERENCES [dbo].[SupplierDebitNoteLineItems] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNoteTaxComponents_Taxes_TaxId')
    ALTER TABLE [dbo].[SupplierDebitNoteTaxComponents] ADD CONSTRAINT [FK_SupplierDebitNoteTaxComponents_Taxes_TaxId]
        FOREIGN KEY ([TaxId]) REFERENCES [dbo].[Taxes] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNoteTaxComponents_TaxGroups_TaxGroupId')
    ALTER TABLE [dbo].[SupplierDebitNoteTaxComponents] ADD CONSTRAINT [FK_SupplierDebitNoteTaxComponents_TaxGroups_TaxGroupId]
        FOREIGN KEY ([TaxGroupId]) REFERENCES [dbo].[TaxGroups] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNoteTaxComponents_TaxCalculations_OriginalTaxCalculationId')
    ALTER TABLE [dbo].[SupplierDebitNoteTaxComponents] ADD CONSTRAINT [FK_SupplierDebitNoteTaxComponents_TaxCalculations_OriginalTaxCalculationId]
        FOREIGN KEY ([OriginalTaxCalculationId]) REFERENCES [dbo].[TaxCalculations] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNoteTaxComponents_AccountTransactions_OriginalAccountTransactionId')
    ALTER TABLE [dbo].[SupplierDebitNoteTaxComponents] ADD CONSTRAINT [FK_SupplierDebitNoteTaxComponents_AccountTransactions_OriginalAccountTransactionId]
        FOREIGN KEY ([OriginalAccountTransactionId]) REFERENCES [dbo].[AccountTransactions] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNoteTaxComponents_Accounts_ResolvedCreditAccountId')
    ALTER TABLE [dbo].[SupplierDebitNoteTaxComponents] ADD CONSTRAINT [FK_SupplierDebitNoteTaxComponents_Accounts_ResolvedCreditAccountId]
        FOREIGN KEY ([ResolvedCreditAccountId]) REFERENCES [dbo].[Accounts] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_SupplierDebitNoteTaxComponents_Tenants_TenantId')
    ALTER TABLE [dbo].[SupplierDebitNoteTaxComponents] ADD CONSTRAINT [FK_SupplierDebitNoteTaxComponents_Tenants_TenantId]
        FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_ApSupplierIdentityLinks_BusinessPartners_BusinessPartnerId')
    ALTER TABLE [dbo].[ApSupplierIdentityLinks] ADD CONSTRAINT [FK_ApSupplierIdentityLinks_BusinessPartners_BusinessPartnerId]
        FOREIGN KEY ([BusinessPartnerId]) REFERENCES [dbo].[BusinessPartners] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_ApSupplierIdentityLinks_Suppliers_SupplierId')
    ALTER TABLE [dbo].[ApSupplierIdentityLinks] ADD CONSTRAINT [FK_ApSupplierIdentityLinks_Suppliers_SupplierId]
        FOREIGN KEY ([SupplierId]) REFERENCES [dbo].[Suppliers] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_ApSupplierIdentityLinks_Tenants_TenantId')
    ALTER TABLE [dbo].[ApSupplierIdentityLinks] ADD CONSTRAINT [FK_ApSupplierIdentityLinks_Tenants_TenantId]
        FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]);

/* Exact-code, one-to-one backfill only. Name matching is deliberately prohibited. */
;WITH UniquePartners AS
(
    SELECT [TenantId], [PartnerCode], MIN([Id]) AS [Id]
    FROM [dbo].[BusinessPartners]
    WHERE [IsDeleted] = 0
      AND [IsActive] = 1
      AND [PartnerType] IN (N'Supplier', N'Contractor', N'Both')
      AND NULLIF(LTRIM(RTRIM([PartnerCode])), N'') IS NOT NULL
    GROUP BY [TenantId], [PartnerCode]
    HAVING COUNT_BIG(*) = 1
),
UniqueSuppliers AS
(
    SELECT [TenantId], [SupplierCode], MIN([Id]) AS [Id]
    FROM [dbo].[Suppliers]
    WHERE [IsDeleted] = 0
      AND [IsActive] = 1
      AND NULLIF(LTRIM(RTRIM([SupplierCode])), N'') IS NOT NULL
    GROUP BY [TenantId], [SupplierCode]
    HAVING COUNT_BIG(*) = 1
)
INSERT INTO [dbo].[ApSupplierIdentityLinks]
(
    [Id], [BusinessPartnerId], [SupplierId], [MappingSource], [IsVerified],
    [VerifiedAtUtc], [VerifiedById], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy],
    [CreatedById], [LastModifiedById], [IsDeleted], [DeletedAt], [DeletedBy], [TenantId]
)
SELECT NEWID(), bp.[Id], supplier.[Id], N'ExactCodeBackfill', 0,
       NULL, NULL, SYSUTCDATETIME(), NULL, N'Finance supplier identity migration', NULL,
       NULL, NULL, 0, NULL, NULL, bp.[TenantId]
FROM UniquePartners bp
JOIN UniqueSuppliers supplier
  ON supplier.[TenantId] = bp.[TenantId]
 AND supplier.[SupplierCode] = bp.[PartnerCode]
WHERE NOT EXISTS
(
    SELECT 1 FROM [dbo].[ApSupplierIdentityLinks] existing
    WHERE existing.[TenantId] = bp.[TenantId]
      AND (existing.[BusinessPartnerId] = bp.[Id] OR existing.[SupplierId] = supplier.[Id])
);

UPDATE note
   SET [SupplierId] = link.[SupplierId]
FROM [dbo].[SupplierDebitNotes] note
JOIN [dbo].[ApSupplierIdentityLinks] link
  ON link.[TenantId] = note.[TenantId]
 AND link.[BusinessPartnerId] = note.[VendorId]
 AND link.[IsDeleted] = 0
WHERE note.[SupplierId] IS NULL;

/* Runtime-model indexes, including every FK index omitted by the raw lifecycle migration. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[AccountTransactions]') AND [name] = N'IX_AccountTransactions_TenantId_SourceDocumentId_SourceDocumentLineId')
    CREATE INDEX [IX_AccountTransactions_TenantId_SourceDocumentId_SourceDocumentLineId]
        ON [dbo].[AccountTransactions] ([TenantId], [SourceDocumentId], [SourceDocumentLineId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[TaxCalculations]') AND [name] = N'IX_TaxCalculations_PostingAccountId')
    CREATE INDEX [IX_TaxCalculations_PostingAccountId] ON [dbo].[TaxCalculations] ([PostingAccountId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[TaxCalculations]') AND [name] = N'IX_TaxCalculations_Document_Line_Tax')
    CREATE INDEX [IX_TaxCalculations_Document_Line_Tax]
        ON [dbo].[TaxCalculations] ([TenantId], [DocumentType], [DocumentId], [DocumentLineId], [TaxId], [TaxGroupId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNotes]') AND [name] = N'IX_SupplierDebitNotes_SupplierId')
    CREATE INDEX [IX_SupplierDebitNotes_SupplierId] ON [dbo].[SupplierDebitNotes] ([SupplierId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNotes]') AND [name] = N'IX_SupplierDebitNotes_JournalEntryId')
    CREATE INDEX [IX_SupplierDebitNotes_JournalEntryId] ON [dbo].[SupplierDebitNotes] ([JournalEntryId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNotes]') AND [name] = N'IX_SupplierDebitNotes_OriginalVendorInvoiceId')
    CREATE INDEX [IX_SupplierDebitNotes_OriginalVendorInvoiceId] ON [dbo].[SupplierDebitNotes] ([OriginalVendorInvoiceId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNotes]') AND [name] = N'IX_SupplierDebitNotes_PostingEventId')
    CREATE INDEX [IX_SupplierDebitNotes_PostingEventId] ON [dbo].[SupplierDebitNotes] ([PostingEventId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNotes]') AND [name] = N'IX_SupplierDebitNotes_ReversalJournalEntryId')
    CREATE INDEX [IX_SupplierDebitNotes_ReversalJournalEntryId] ON [dbo].[SupplierDebitNotes] ([ReversalJournalEntryId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNotes]') AND [name] = N'IX_SupplierDebitNotes_ReversalPostingEventId')
    CREATE INDEX [IX_SupplierDebitNotes_ReversalPostingEventId] ON [dbo].[SupplierDebitNotes] ([ReversalPostingEventId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNotes]') AND [name] = N'IX_SupplierDebitNotes_SupplierReturnId')
    CREATE INDEX [IX_SupplierDebitNotes_SupplierReturnId] ON [dbo].[SupplierDebitNotes] ([SupplierReturnId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNotes]') AND [name] = N'IX_SupplierDebitNotes_TenantId')
    CREATE INDEX [IX_SupplierDebitNotes_TenantId] ON [dbo].[SupplierDebitNotes] ([TenantId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNotes]') AND [name] = N'IX_SupplierDebitNotes_VendorId')
    CREATE INDEX [IX_SupplierDebitNotes_VendorId] ON [dbo].[SupplierDebitNotes] ([VendorId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNotes]') AND [name] = N'IX_SupplierDebitNotes_WorkflowInstanceId')
    CREATE INDEX [IX_SupplierDebitNotes_WorkflowInstanceId] ON [dbo].[SupplierDebitNotes] ([WorkflowInstanceId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNoteLineItems]') AND [name] = N'IX_SupplierDebitNoteLineItems_OriginalAccountTransactionId')
    CREATE INDEX [IX_SupplierDebitNoteLineItems_OriginalAccountTransactionId] ON [dbo].[SupplierDebitNoteLineItems] ([OriginalAccountTransactionId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNoteLineItems]') AND [name] = N'IX_SupplierDebitNoteLineItems_OriginalFinancePurchaseOrderItemId')
    CREATE INDEX [IX_SupplierDebitNoteLineItems_OriginalFinancePurchaseOrderItemId] ON [dbo].[SupplierDebitNoteLineItems] ([OriginalFinancePurchaseOrderItemId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNoteLineItems]') AND [name] = N'IX_SupplierDebitNoteLineItems_OriginalVendorInvoiceLineItemId')
    CREATE INDEX [IX_SupplierDebitNoteLineItems_OriginalVendorInvoiceLineItemId] ON [dbo].[SupplierDebitNoteLineItems] ([OriginalVendorInvoiceLineItemId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNoteLineItems]') AND [name] = N'IX_SupplierDebitNoteLineItems_ResolvedCreditAccountId')
    CREATE INDEX [IX_SupplierDebitNoteLineItems_ResolvedCreditAccountId] ON [dbo].[SupplierDebitNoteLineItems] ([ResolvedCreditAccountId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNoteLineItems]') AND [name] = N'IX_SupplierDebitNoteLineItems_SupplierDebitNoteId')
    CREATE INDEX [IX_SupplierDebitNoteLineItems_SupplierDebitNoteId] ON [dbo].[SupplierDebitNoteLineItems] ([SupplierDebitNoteId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNoteLineItems]') AND [name] = N'IX_SupplierDebitNoteLineItems_TenantId')
    CREATE INDEX [IX_SupplierDebitNoteLineItems_TenantId] ON [dbo].[SupplierDebitNoteLineItems] ([TenantId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNoteTaxComponents]') AND [name] = N'IX_SupplierDebitNoteTaxComponents_OriginalAccountTransactionId')
    CREATE INDEX [IX_SupplierDebitNoteTaxComponents_OriginalAccountTransactionId] ON [dbo].[SupplierDebitNoteTaxComponents] ([OriginalAccountTransactionId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNoteTaxComponents]') AND [name] = N'IX_SupplierDebitNoteTaxComponents_OriginalTaxCalculationId')
    CREATE INDEX [IX_SupplierDebitNoteTaxComponents_OriginalTaxCalculationId] ON [dbo].[SupplierDebitNoteTaxComponents] ([OriginalTaxCalculationId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNoteTaxComponents]') AND [name] = N'IX_SupplierDebitNoteTaxComponents_ResolvedCreditAccountId')
    CREATE INDEX [IX_SupplierDebitNoteTaxComponents_ResolvedCreditAccountId] ON [dbo].[SupplierDebitNoteTaxComponents] ([ResolvedCreditAccountId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNoteTaxComponents]') AND [name] = N'IX_SupplierDebitNoteTaxComponents_SupplierDebitNoteLineItemId')
    CREATE INDEX [IX_SupplierDebitNoteTaxComponents_SupplierDebitNoteLineItemId] ON [dbo].[SupplierDebitNoteTaxComponents] ([SupplierDebitNoteLineItemId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNoteTaxComponents]') AND [name] = N'IX_SupplierDebitNoteTaxComponents_TaxGroupId')
    CREATE INDEX [IX_SupplierDebitNoteTaxComponents_TaxGroupId] ON [dbo].[SupplierDebitNoteTaxComponents] ([TaxGroupId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNoteTaxComponents]') AND [name] = N'IX_SupplierDebitNoteTaxComponents_TaxId')
    CREATE INDEX [IX_SupplierDebitNoteTaxComponents_TaxId] ON [dbo].[SupplierDebitNoteTaxComponents] ([TaxId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNoteTaxComponents]') AND [name] = N'IX_SupplierDebitNoteTaxComponents_TenantId')
    CREATE INDEX [IX_SupplierDebitNoteTaxComponents_TenantId] ON [dbo].[SupplierDebitNoteTaxComponents] ([TenantId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNoteTaxComponents]') AND [name] = N'IX_SupplierDebitNoteTaxComponents_TenantId_SupplierDebitNoteLineItemId_CalculationOrder_TaxId')
    CREATE UNIQUE INDEX [IX_SupplierDebitNoteTaxComponents_TenantId_SupplierDebitNoteLineItemId_CalculationOrder_TaxId]
        ON [dbo].[SupplierDebitNoteTaxComponents] ([TenantId], [SupplierDebitNoteLineItemId], [CalculationOrder], [TaxId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[ApSupplierIdentityLinks]') AND [name] = N'IX_ApSupplierIdentityLinks_BusinessPartnerId')
    CREATE INDEX [IX_ApSupplierIdentityLinks_BusinessPartnerId] ON [dbo].[ApSupplierIdentityLinks] ([BusinessPartnerId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[ApSupplierIdentityLinks]') AND [name] = N'IX_ApSupplierIdentityLinks_SupplierId')
    CREATE INDEX [IX_ApSupplierIdentityLinks_SupplierId] ON [dbo].[ApSupplierIdentityLinks] ([SupplierId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[ApSupplierIdentityLinks]') AND [name] = N'IX_ApSupplierIdentityLinks_TenantId')
    CREATE INDEX [IX_ApSupplierIdentityLinks_TenantId] ON [dbo].[ApSupplierIdentityLinks] ([TenantId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[ApSupplierIdentityLinks]') AND [name] = N'UX_ApSupplierIdentityLinks_Tenant_BusinessPartner')
    CREATE UNIQUE INDEX [UX_ApSupplierIdentityLinks_Tenant_BusinessPartner]
        ON [dbo].[ApSupplierIdentityLinks] ([TenantId], [BusinessPartnerId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[ApSupplierIdentityLinks]') AND [name] = N'UX_ApSupplierIdentityLinks_Tenant_Supplier')
    CREATE UNIQUE INDEX [UX_ApSupplierIdentityLinks_Tenant_Supplier]
        ON [dbo].[ApSupplierIdentityLinks] ([TenantId], [SupplierId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNoteApplications]') AND [name] = N'IX_SupplierDebitNoteApplications_PaymentJournalEntryId')
    CREATE INDEX [IX_SupplierDebitNoteApplications_PaymentJournalEntryId] ON [dbo].[SupplierDebitNoteApplications] ([PaymentJournalEntryId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNoteApplications]') AND [name] = N'IX_SupplierDebitNoteApplications_PaymentPostingEventId')
    CREATE INDEX [IX_SupplierDebitNoteApplications_PaymentPostingEventId] ON [dbo].[SupplierDebitNoteApplications] ([PaymentPostingEventId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNoteApplications]') AND [name] = N'IX_SupplierDebitNoteApplications_SupplierDebitNoteId')
    CREATE INDEX [IX_SupplierDebitNoteApplications_SupplierDebitNoteId] ON [dbo].[SupplierDebitNoteApplications] ([SupplierDebitNoteId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNoteApplications]') AND [name] = N'IX_SupplierDebitNoteApplications_TenantId')
    CREATE INDEX [IX_SupplierDebitNoteApplications_TenantId] ON [dbo].[SupplierDebitNoteApplications] ([TenantId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNoteApplications]') AND [name] = N'IX_SupplierDebitNoteApplications_VendorInvoiceId')
    CREATE INDEX [IX_SupplierDebitNoteApplications_VendorInvoiceId] ON [dbo].[SupplierDebitNoteApplications] ([VendorInvoiceId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[SupplierDebitNoteApplications]') AND [name] = N'IX_SupplierDebitNoteApplications_VendorPaymentId')
    CREATE INDEX [IX_SupplierDebitNoteApplications_VendorPaymentId] ON [dbo].[SupplierDebitNoteApplications] ([VendorPaymentId]);
""", suppressTransaction: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException(
            "Supplier debit-note lineage, tax and supplier-identity evidence cannot be removed by downgrade.");
    }
}
