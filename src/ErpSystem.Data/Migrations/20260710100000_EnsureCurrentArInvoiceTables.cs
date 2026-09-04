using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260710100000_EnsureCurrentArInvoiceTables")]
    public partial class EnsureCurrentArInvoiceTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep the compatibility column addition in its own SQL batch. SQL Server compiles
            // the following repair batch before executing it, so references to BusinessPartnerId
            // fail on legacy/clean-baseline Invoices tables when ALTER TABLE ADD appears in that
            // same batch.
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'[dbo].[Invoices]', N'BusinessPartnerId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Invoices] ADD [BusinessPartnerId] uniqueidentifier NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Invoices] (
                        [Id] uniqueidentifier NOT NULL,
                        [InvoiceNumber] nvarchar(50) NOT NULL,
                        [BusinessPartnerId] uniqueidentifier NOT NULL,
                        [CustomerName] nvarchar(200) NOT NULL,
                        [CustomerAddress] nvarchar(500) NULL,
                        [InvoiceDate] datetime2 NOT NULL,
                        [DueDate] datetime2 NULL,
                        [SubTotal] decimal(18,2) NOT NULL CONSTRAINT [DF_Invoices_SubTotal] DEFAULT 0,
                        [TaxAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_Invoices_TaxAmount] DEFAULT 0,
                        [DiscountAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_Invoices_DiscountAmount] DEFAULT 0,
                        [TotalAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_Invoices_TotalAmount] DEFAULT 0,
                        [PaidAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_Invoices_PaidAmount] DEFAULT 0,
                        [CreditedAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_Invoices_CreditedAmount] DEFAULT 0,
                        [Status] int NOT NULL CONSTRAINT [DF_Invoices_Status] DEFAULT 1,
                        [Notes] nvarchar(500) NULL,
                        [Reference] nvarchar(100) NULL,
                        [IsOpeningBalance] bit NOT NULL CONSTRAINT [DF_Invoices_IsOpeningBalance] DEFAULT 0,
                        [CurrencyCode] nvarchar(3) NOT NULL CONSTRAINT [DF_Invoices_CurrencyCode] DEFAULT N'GHS',
                        [ExchangeRate] decimal(18,4) NOT NULL CONSTRAINT [DF_Invoices_ExchangeRate] DEFAULT 1,
                        [BaseCurrencyAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_Invoices_BaseCurrencyAmount] DEFAULT 0,
                        [PaymentTermsDays] int NOT NULL CONSTRAINT [DF_Invoices_PaymentTermsDays] DEFAULT 30,
                        [PaymentTermId] uniqueidentifier NULL,
                        [EarlyPaymentDiscountPercentage] decimal(18,4) NOT NULL CONSTRAINT [DF_Invoices_EarlyPaymentDiscountPercentage] DEFAULT 0,
                        [EarlyPaymentDiscountDueDate] datetime2 NULL,
                        [EarlyPaymentDiscountAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_Invoices_EarlyPaymentDiscountAmount] DEFAULT 0,
                        [TaxGroupId] uniqueidentifier NULL,
                        [JournalEntryId] uniqueidentifier NULL,
                        [TenantId] uniqueidentifier NOT NULL,
                        [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_Invoices_CreatedAt] DEFAULT SYSUTCDATETIME(),
                        [UpdatedAt] datetime2 NULL,
                        [CreatedBy] nvarchar(max) NULL,
                        [UpdatedBy] nvarchar(max) NULL,
                        [CreatedById] uniqueidentifier NULL,
                        [LastModifiedById] uniqueidentifier NULL,
                        [IsDeleted] bit NOT NULL CONSTRAINT [DF_Invoices_IsDeleted] DEFAULT 0,
                        [DeletedAt] datetime2 NULL,
                        [DeletedBy] nvarchar(max) NULL,
                        CONSTRAINT [PK_Invoices] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_Invoices_BusinessPartners_BusinessPartnerId] FOREIGN KEY ([BusinessPartnerId]) REFERENCES [dbo].[BusinessPartners] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_Invoices_PaymentTerms_PaymentTermId] FOREIGN KEY ([PaymentTermId]) REFERENCES [dbo].[PaymentTerms] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_Invoices_TaxGroups_TaxGroupId] FOREIGN KEY ([TaxGroupId]) REFERENCES [dbo].[TaxGroups] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_Invoices_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
                    );

                    CREATE INDEX [IX_Invoices_BusinessPartnerId] ON [dbo].[Invoices] ([BusinessPartnerId]);
                    CREATE INDEX [IX_Invoices_PaymentTermId] ON [dbo].[Invoices] ([PaymentTermId]);
                    CREATE INDEX [IX_Invoices_TaxGroupId] ON [dbo].[Invoices] ([TaxGroupId]);
                    CREATE INDEX [IX_Invoices_TenantId] ON [dbo].[Invoices] ([TenantId]);
                END

                -- Upgrade databases that already had the older AR Invoices table. The current
                -- Invoice model ignores legacy CustomerId and uses BusinessPartnerId for AR.
                IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH(N'[dbo].[Invoices]', N'BusinessPartnerId') IS NULL
                    BEGIN
                        ALTER TABLE [dbo].[Invoices] ADD [BusinessPartnerId] uniqueidentifier NULL;
                    END

                    IF COL_LENGTH(N'[dbo].[Invoices]', N'CustomerId') IS NOT NULL
                       AND OBJECT_ID(N'[dbo].[Customers]', N'U') IS NOT NULL
                    BEGIN
                        UPDATE i
                        SET [BusinessPartnerId] = bp.[Id]
                        FROM [dbo].[Invoices] i
                        INNER JOIN [dbo].[BusinessPartners] bp
                            ON bp.[Id] = i.[CustomerId]
                           AND bp.[TenantId] = i.[TenantId]
                           AND bp.[IsDeleted] = CAST(0 AS bit)
                        WHERE i.[BusinessPartnerId] IS NULL;

                        INSERT INTO [dbo].[BusinessPartners] (
                            [Id],
                            [PartnerCode],
                            [PartnerName],
                            [PartnerType],
                            [TaxIdentificationNumber],
                            [PrimaryContactName],
                            [PrimaryEmail],
                            [PrimaryPhone],
                            [PhysicalAddress],
                            [PhysicalCity],
                            [PhysicalState],
                            [PhysicalCountry],
                            [CustomerAccountNumber],
                            [CustomerType],
                            [CreditLimit],
                            [OutstandingBalance],
                            [PaymentTermId],
                            [Currency],
                            [DefaultApAccountId],
                            [DefaultArAccountId],
                            [DefaultExpenseAccountId],
                            [RegistrationStatus],
                            [IsPreferred],
                            [IsActive],
                            [IsBlacklisted],
                            [IsVatWithholdingAgent],
                            [TaxTreatment],
                            [IsTaxExempt],
                            [IsOnCreditHold],
                            [Notes],
                            [TenantId],
                            [CreatedAt],
                            [UpdatedAt],
                            [CreatedBy],
                            [UpdatedBy],
                            [CreatedById],
                            [LastModifiedById],
                            [IsDeleted],
                            [DeletedAt],
                            [DeletedBy])
                        SELECT DISTINCT
                            c.[Id],
                            -- PartnerCode is globally unique in the current BusinessPartner model.
                            -- Keep the legacy customer code on CustomerAccountNumber and use a deterministic
                            -- ID-based partner code so this repair cannot collide with existing suppliers.
                            LEFT(CONCAT(N'AR-CUST-', CONVERT(nvarchar(36), c.[Id])), 50),
                            LEFT(COALESCE(NULLIF(c.[CustomerName], N''), i.[CustomerName], N'Legacy Customer'), 200),
                            N'Customer',
                            c.[TaxId],
                            c.[ContactPerson],
                            c.[Email],
                            c.[Phone],
                            c.[Address],
                            c.[City],
                            c.[State],
                            c.[Country],
                            LEFT(COALESCE(NULLIF(c.[CustomerCode], N''), CONCAT(N'CUST-', CONVERT(nvarchar(36), c.[Id]))), 50),
                            LEFT(COALESCE(NULLIF(c.[CustomerType], N''), N'Customer'), 50),
                            c.[CreditLimit],
                            c.[OutstandingBalance],
                            c.[PaymentTermId],
                            c.[CurrencyCode],
                            c.[DefaultApAccountId],
                            c.[DefaultArAccountId],
                            c.[DefaultExpenseAccountId],
                            N'Approved',
                            CAST(0 AS bit),
                            c.[IsActive],
                            CAST(0 AS bit),
                            CAST(0 AS bit),
                            1,
                            CAST(0 AS bit),
                            CAST(0 AS bit),
                            c.[Notes],
                            c.[TenantId],
                            COALESCE(c.[CreatedAt], SYSUTCDATETIME()),
                            c.[UpdatedAt],
                            c.[CreatedBy],
                            c.[UpdatedBy],
                            c.[CreatedById],
                            c.[LastModifiedById],
                            c.[IsDeleted],
                            c.[DeletedAt],
                            c.[DeletedBy]
                        FROM [dbo].[Invoices] i
                        INNER JOIN [dbo].[Customers] c
                            ON c.[Id] = i.[CustomerId]
                           AND c.[TenantId] = i.[TenantId]
                        LEFT JOIN [dbo].[BusinessPartners] bp
                            ON bp.[Id] = c.[Id]
                        WHERE i.[BusinessPartnerId] IS NULL
                          AND bp.[Id] IS NULL;

                        UPDATE i
                        SET [BusinessPartnerId] = c.[Id]
                        FROM [dbo].[Invoices] i
                        INNER JOIN [dbo].[Customers] c
                            ON c.[Id] = i.[CustomerId]
                           AND c.[TenantId] = i.[TenantId]
                        WHERE i.[BusinessPartnerId] IS NULL
                          AND EXISTS (
                              SELECT 1
                              FROM [dbo].[BusinessPartners] bp
                              WHERE bp.[Id] = c.[Id]
                                AND bp.[TenantId] = i.[TenantId]
                                AND bp.[IsDeleted] = CAST(0 AS bit)
                          );
                    END

                    IF EXISTS (SELECT 1 FROM [dbo].[Invoices] WHERE [BusinessPartnerId] IS NULL)
                    BEGIN
                        THROW 51000, 'Cannot align AR Invoices: existing rows could not be mapped to same-tenant BusinessPartners.', 1;
                    END

                    IF EXISTS (
                        SELECT 1
                        FROM sys.columns
                        WHERE [object_id] = OBJECT_ID(N'[dbo].[Invoices]')
                          AND [name] = N'BusinessPartnerId'
                          AND [is_nullable] = 1
                    )
                    BEGIN
                        ALTER TABLE [dbo].[Invoices] ALTER COLUMN [BusinessPartnerId] uniqueidentifier NOT NULL;
                    END

                    IF NOT EXISTS (
                        SELECT 1
                        FROM sys.indexes
                        WHERE [name] = N'IX_Invoices_BusinessPartnerId'
                          AND [object_id] = OBJECT_ID(N'[dbo].[Invoices]')
                    )
                    BEGIN
                        CREATE INDEX [IX_Invoices_BusinessPartnerId] ON [dbo].[Invoices] ([BusinessPartnerId]);
                    END

                    IF NOT EXISTS (
                        SELECT 1
                        FROM sys.foreign_keys
                        WHERE [name] = N'FK_Invoices_BusinessPartners_BusinessPartnerId'
                          AND [parent_object_id] = OBJECT_ID(N'[dbo].[Invoices]')
                    )
                    BEGIN
                        ALTER TABLE [dbo].[Invoices] WITH CHECK
                        ADD CONSTRAINT [FK_Invoices_BusinessPartners_BusinessPartnerId]
                        FOREIGN KEY ([BusinessPartnerId]) REFERENCES [dbo].[BusinessPartners] ([Id]) ON DELETE NO ACTION;
                    END
                END

                IF OBJECT_ID(N'[dbo].[InvoiceLineItem]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[InvoiceLineItem] (
                        [Id] uniqueidentifier NOT NULL,
                        [InvoiceId] uniqueidentifier NOT NULL,
                        [LineItemType] int NOT NULL CONSTRAINT [DF_InvoiceLineItem_LineItemType] DEFAULT 1,
                        [ProductId] uniqueidentifier NULL,
                        [InventoryItemId] uniqueidentifier NULL,
                        [WarehouseId] uniqueidentifier NULL,
                        [LocationId] uniqueidentifier NULL,
                        [SerialNumber] nvarchar(100) NULL,
                        [LotNumber] nvarchar(100) NULL,
                        [ExpirationDate] datetime2 NULL,
                        [UnitCost] decimal(18,2) NULL,
                        [CostTotal] decimal(18,2) NULL,
                        [GLAccountId] uniqueidentifier NULL,
                        [Description] nvarchar(200) NOT NULL,
                        [Quantity] decimal(18,4) NOT NULL CONSTRAINT [DF_InvoiceLineItem_Quantity] DEFAULT 1,
                        [UnitPrice] decimal(18,2) NOT NULL CONSTRAINT [DF_InvoiceLineItem_UnitPrice] DEFAULT 0,
                        [TaxGroupId] uniqueidentifier NULL,
                        [TaxTreatment] int NOT NULL CONSTRAINT [DF_InvoiceLineItem_TaxTreatment] DEFAULT 1,
                        [TaxRate] decimal(18,4) NOT NULL CONSTRAINT [DF_InvoiceLineItem_TaxRate] DEFAULT 0,
                        [TaxAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_InvoiceLineItem_TaxAmount] DEFAULT 0,
                        [TaxCode] nvarchar(50) NULL,
                        [Unit] nvarchar(50) NULL,
                        [DiscountPercentage] decimal(18,4) NOT NULL CONSTRAINT [DF_InvoiceLineItem_DiscountPercentage] DEFAULT 0,
                        [DiscountAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_InvoiceLineItem_DiscountAmount] DEFAULT 0,
                        [TenantId] uniqueidentifier NOT NULL,
                        [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_InvoiceLineItem_CreatedAt] DEFAULT SYSUTCDATETIME(),
                        [UpdatedAt] datetime2 NULL,
                        [CreatedBy] nvarchar(max) NULL,
                        [UpdatedBy] nvarchar(max) NULL,
                        [CreatedById] uniqueidentifier NULL,
                        [LastModifiedById] uniqueidentifier NULL,
                        [IsDeleted] bit NOT NULL CONSTRAINT [DF_InvoiceLineItem_IsDeleted] DEFAULT 0,
                        [DeletedAt] datetime2 NULL,
                        [DeletedBy] nvarchar(max) NULL,
                        CONSTRAINT [PK_InvoiceLineItem] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_InvoiceLineItem_Accounts_GLAccountId] FOREIGN KEY ([GLAccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_InvoiceLineItem_InventoryItems_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [dbo].[InventoryItems] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_InvoiceLineItem_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [dbo].[Invoices] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_InvoiceLineItem_TaxGroups_TaxGroupId] FOREIGN KEY ([TaxGroupId]) REFERENCES [dbo].[TaxGroups] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_InvoiceLineItem_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_InvoiceLineItem_WarehouseLocations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [dbo].[WarehouseLocations] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_InvoiceLineItem_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [dbo].[Warehouses] ([Id]) ON DELETE NO ACTION
                    );

                    CREATE INDEX [IX_InvoiceLineItem_GLAccountId] ON [dbo].[InvoiceLineItem] ([GLAccountId]);
                    CREATE INDEX [IX_InvoiceLineItem_InventoryItemId] ON [dbo].[InvoiceLineItem] ([InventoryItemId]);
                    CREATE INDEX [IX_InvoiceLineItem_InvoiceId] ON [dbo].[InvoiceLineItem] ([InvoiceId]);
                    CREATE INDEX [IX_InvoiceLineItem_LocationId] ON [dbo].[InvoiceLineItem] ([LocationId]);
                    CREATE INDEX [IX_InvoiceLineItem_TaxGroupId] ON [dbo].[InvoiceLineItem] ([TaxGroupId]);
                    CREATE INDEX [IX_InvoiceLineItem_TenantId] ON [dbo].[InvoiceLineItem] ([TenantId]);
                    CREATE INDEX [IX_InvoiceLineItem_WarehouseId] ON [dbo].[InvoiceLineItem] ([WarehouseId]);
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- This migration is an idempotent current-schema UAT alignment repair.
                -- Down intentionally does not drop AR source tables to avoid destructive rollback of invoice data.
                """);
        }
    }
}
