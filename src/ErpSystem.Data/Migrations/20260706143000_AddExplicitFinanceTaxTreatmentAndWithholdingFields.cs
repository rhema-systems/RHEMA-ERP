using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260706143000_AddExplicitFinanceTaxTreatmentAndWithholdingFields")]
    public partial class AddExplicitFinanceTaxTreatmentAndWithholdingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[BusinessPartners]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[BusinessPartners]', N'IsVatWithholdingAgent') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[BusinessPartners] ADD [IsVatWithholdingAgent] bit NOT NULL CONSTRAINT [DF_BusinessPartners_IsVatWithholdingAgent] DEFAULT CAST(0 AS bit);');

                IF OBJECT_ID(N'[dbo].[BusinessPartners]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[BusinessPartners]', N'TaxTreatment') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[BusinessPartners] ADD [TaxTreatment] int NOT NULL CONSTRAINT [DF_BusinessPartners_TaxTreatment] DEFAULT 1;');

                IF OBJECT_ID(N'[dbo].[Suppliers]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[Suppliers]', N'IsWithholdingTaxApplicable') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[Suppliers] ADD [IsWithholdingTaxApplicable] bit NOT NULL CONSTRAINT [DF_Suppliers_IsWithholdingTaxApplicable] DEFAULT CAST(0 AS bit);');

                IF OBJECT_ID(N'[dbo].[Suppliers]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[Suppliers]', N'TaxTreatment') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[Suppliers] ADD [TaxTreatment] int NOT NULL CONSTRAINT [DF_Suppliers_TaxTreatment] DEFAULT 1;');

                IF OBJECT_ID(N'[dbo].[InvoiceLineItem]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[InvoiceLineItem]', N'TaxTreatment') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[InvoiceLineItem] ADD [TaxTreatment] int NOT NULL CONSTRAINT [DF_InvoiceLineItem_TaxTreatment] DEFAULT 1;');

                IF OBJECT_ID(N'[dbo].[InvoiceLineItems]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[InvoiceLineItems]', N'TaxTreatment') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[InvoiceLineItems] ADD [TaxTreatment] int NOT NULL CONSTRAINT [DF_InvoiceLineItems_TaxTreatment] DEFAULT 1;');

                IF OBJECT_ID(N'[dbo].[VendorInvoiceLineItem]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[VendorInvoiceLineItem]', N'TaxGroupId') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[VendorInvoiceLineItem] ADD [TaxGroupId] uniqueidentifier NULL;');

                IF OBJECT_ID(N'[dbo].[VendorInvoiceLineItem]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[VendorInvoiceLineItem]', N'TaxTreatment') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[VendorInvoiceLineItem] ADD [TaxTreatment] int NOT NULL CONSTRAINT [DF_VendorInvoiceLineItem_TaxTreatment] DEFAULT 1;');

                IF OBJECT_ID(N'[dbo].[VendorInvoice]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[VendorInvoice]', N'WithholdingTaxId') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[VendorInvoice] ADD [WithholdingTaxId] uniqueidentifier NULL;');

                IF OBJECT_ID(N'[dbo].[VendorInvoice]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[VendorInvoice]', N'WithholdingTaxAccountId') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[VendorInvoice] ADD [WithholdingTaxAccountId] uniqueidentifier NULL;');

                IF OBJECT_ID(N'[dbo].[VendorInvoice]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[VendorInvoice]', N'WithholdingCertificateNumber') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[VendorInvoice] ADD [WithholdingCertificateNumber] nvarchar(100) NULL;');

                IF OBJECT_ID(N'[dbo].[VendorInvoice]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[VendorInvoice]', N'WithholdingCertificateDate') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[VendorInvoice] ADD [WithholdingCertificateDate] datetime2 NULL;');

                IF OBJECT_ID(N'[dbo].[VendorPayment]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[VendorPayment]', N'WithholdingTaxId') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[VendorPayment] ADD [WithholdingTaxId] uniqueidentifier NULL;');

                IF OBJECT_ID(N'[dbo].[VendorPayment]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[VendorPayment]', N'WithholdingTaxAccountId') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[VendorPayment] ADD [WithholdingTaxAccountId] uniqueidentifier NULL;');

                IF OBJECT_ID(N'[dbo].[VendorPayment]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[VendorPayment]', N'WithholdingCertificateNumber') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[VendorPayment] ADD [WithholdingCertificateNumber] nvarchar(100) NULL;');

                IF OBJECT_ID(N'[dbo].[VendorPayment]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[VendorPayment]', N'WithholdingCertificateDate') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[VendorPayment] ADD [WithholdingCertificateDate] datetime2 NULL;');

                IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[CustomerPayment]', N'WithholdingTaxId') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[CustomerPayment] ADD [WithholdingTaxId] uniqueidentifier NULL;');

                IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[CustomerPayment]', N'WithholdingTaxAccountId') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[CustomerPayment] ADD [WithholdingTaxAccountId] uniqueidentifier NULL;');

                IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[CustomerPayment]', N'WithholdingTaxAmount') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[CustomerPayment] ADD [WithholdingTaxAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_CustomerPayment_WithholdingTaxAmount] DEFAULT 0;');

                IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[CustomerPayment]', N'VatWithholdingTaxId') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[CustomerPayment] ADD [VatWithholdingTaxId] uniqueidentifier NULL;');

                IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[CustomerPayment]', N'VatWithholdingAccountId') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[CustomerPayment] ADD [VatWithholdingAccountId] uniqueidentifier NULL;');

                IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[CustomerPayment]', N'VatWithholdingAmount') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[CustomerPayment] ADD [VatWithholdingAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_CustomerPayment_VatWithholdingAmount] DEFAULT 0;');

                IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[CustomerPayment]', N'WithholdingCertificateNumber') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[CustomerPayment] ADD [WithholdingCertificateNumber] nvarchar(100) NULL;');

                IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[CustomerPayment]', N'WithholdingCertificateDate') IS NULL
                    EXEC(N'ALTER TABLE [dbo].[CustomerPayment] ADD [WithholdingCertificateDate] datetime2 NULL;');

                IF OBJECT_ID(N'[dbo].[VendorInvoiceLineItem]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[VendorInvoiceLineItem]', N'TaxGroupId') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_VendorInvoiceLineItem_TaxGroupId' AND [object_id] = OBJECT_ID(N'[dbo].[VendorInvoiceLineItem]'))
                    EXEC(N'CREATE INDEX [IX_VendorInvoiceLineItem_TaxGroupId] ON [dbo].[VendorInvoiceLineItem] ([TaxGroupId]);');

                IF OBJECT_ID(N'[dbo].[VendorInvoice]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[VendorInvoice]', N'WithholdingTaxId') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_VendorInvoice_WithholdingTaxId' AND [object_id] = OBJECT_ID(N'[dbo].[VendorInvoice]'))
                    EXEC(N'CREATE INDEX [IX_VendorInvoice_WithholdingTaxId] ON [dbo].[VendorInvoice] ([WithholdingTaxId]);');

                IF OBJECT_ID(N'[dbo].[VendorInvoice]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[VendorInvoice]', N'WithholdingTaxAccountId') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_VendorInvoice_WithholdingTaxAccountId' AND [object_id] = OBJECT_ID(N'[dbo].[VendorInvoice]'))
                    EXEC(N'CREATE INDEX [IX_VendorInvoice_WithholdingTaxAccountId] ON [dbo].[VendorInvoice] ([WithholdingTaxAccountId]);');

                IF OBJECT_ID(N'[dbo].[VendorPayment]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[VendorPayment]', N'WithholdingTaxId') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_VendorPayment_WithholdingTaxId' AND [object_id] = OBJECT_ID(N'[dbo].[VendorPayment]'))
                    EXEC(N'CREATE INDEX [IX_VendorPayment_WithholdingTaxId] ON [dbo].[VendorPayment] ([WithholdingTaxId]);');

                IF OBJECT_ID(N'[dbo].[VendorPayment]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[VendorPayment]', N'WithholdingTaxAccountId') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_VendorPayment_WithholdingTaxAccountId' AND [object_id] = OBJECT_ID(N'[dbo].[VendorPayment]'))
                    EXEC(N'CREATE INDEX [IX_VendorPayment_WithholdingTaxAccountId] ON [dbo].[VendorPayment] ([WithholdingTaxAccountId]);');

                IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[CustomerPayment]', N'WithholdingTaxId') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CustomerPayment_WithholdingTaxId' AND [object_id] = OBJECT_ID(N'[dbo].[CustomerPayment]'))
                    EXEC(N'CREATE INDEX [IX_CustomerPayment_WithholdingTaxId] ON [dbo].[CustomerPayment] ([WithholdingTaxId]);');

                IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[CustomerPayment]', N'WithholdingTaxAccountId') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CustomerPayment_WithholdingTaxAccountId' AND [object_id] = OBJECT_ID(N'[dbo].[CustomerPayment]'))
                    EXEC(N'CREATE INDEX [IX_CustomerPayment_WithholdingTaxAccountId] ON [dbo].[CustomerPayment] ([WithholdingTaxAccountId]);');

                IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[CustomerPayment]', N'VatWithholdingTaxId') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CustomerPayment_VatWithholdingTaxId' AND [object_id] = OBJECT_ID(N'[dbo].[CustomerPayment]'))
                    EXEC(N'CREATE INDEX [IX_CustomerPayment_VatWithholdingTaxId] ON [dbo].[CustomerPayment] ([VatWithholdingTaxId]);');

                IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[CustomerPayment]', N'VatWithholdingAccountId') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CustomerPayment_VatWithholdingAccountId' AND [object_id] = OBJECT_ID(N'[dbo].[CustomerPayment]'))
                    EXEC(N'CREATE INDEX [IX_CustomerPayment_VatWithholdingAccountId] ON [dbo].[CustomerPayment] ([VatWithholdingAccountId]);');

                IF OBJECT_ID(N'[dbo].[VendorInvoiceLineItem]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[TaxGroups]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[VendorInvoiceLineItem]', N'TaxGroupId') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_VendorInvoiceLineItem_TaxGroups_TaxGroupId')
                    EXEC(N'ALTER TABLE [dbo].[VendorInvoiceLineItem] ADD CONSTRAINT [FK_VendorInvoiceLineItem_TaxGroups_TaxGroupId] FOREIGN KEY ([TaxGroupId]) REFERENCES [dbo].[TaxGroups] ([Id]) ON DELETE NO ACTION;');

                IF OBJECT_ID(N'[dbo].[VendorInvoice]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[Taxes]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[VendorInvoice]', N'WithholdingTaxId') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_VendorInvoice_Taxes_WithholdingTaxId')
                    EXEC(N'ALTER TABLE [dbo].[VendorInvoice] ADD CONSTRAINT [FK_VendorInvoice_Taxes_WithholdingTaxId] FOREIGN KEY ([WithholdingTaxId]) REFERENCES [dbo].[Taxes] ([Id]) ON DELETE NO ACTION;');

                IF OBJECT_ID(N'[dbo].[VendorInvoice]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[Accounts]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[VendorInvoice]', N'WithholdingTaxAccountId') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_VendorInvoice_Accounts_WithholdingTaxAccountId')
                    EXEC(N'ALTER TABLE [dbo].[VendorInvoice] ADD CONSTRAINT [FK_VendorInvoice_Accounts_WithholdingTaxAccountId] FOREIGN KEY ([WithholdingTaxAccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION;');

                IF OBJECT_ID(N'[dbo].[VendorPayment]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[Taxes]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[VendorPayment]', N'WithholdingTaxId') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_VendorPayment_Taxes_WithholdingTaxId')
                    EXEC(N'ALTER TABLE [dbo].[VendorPayment] ADD CONSTRAINT [FK_VendorPayment_Taxes_WithholdingTaxId] FOREIGN KEY ([WithholdingTaxId]) REFERENCES [dbo].[Taxes] ([Id]) ON DELETE NO ACTION;');

                IF OBJECT_ID(N'[dbo].[VendorPayment]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[Accounts]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[VendorPayment]', N'WithholdingTaxAccountId') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_VendorPayment_Accounts_WithholdingTaxAccountId')
                    EXEC(N'ALTER TABLE [dbo].[VendorPayment] ADD CONSTRAINT [FK_VendorPayment_Accounts_WithholdingTaxAccountId] FOREIGN KEY ([WithholdingTaxAccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION;');

                IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[Taxes]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[CustomerPayment]', N'WithholdingTaxId') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_CustomerPayment_Taxes_WithholdingTaxId')
                    EXEC(N'ALTER TABLE [dbo].[CustomerPayment] ADD CONSTRAINT [FK_CustomerPayment_Taxes_WithholdingTaxId] FOREIGN KEY ([WithholdingTaxId]) REFERENCES [dbo].[Taxes] ([Id]) ON DELETE NO ACTION;');

                IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[Accounts]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[CustomerPayment]', N'WithholdingTaxAccountId') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_CustomerPayment_Accounts_WithholdingTaxAccountId')
                    EXEC(N'ALTER TABLE [dbo].[CustomerPayment] ADD CONSTRAINT [FK_CustomerPayment_Accounts_WithholdingTaxAccountId] FOREIGN KEY ([WithholdingTaxAccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION;');

                IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[Taxes]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[CustomerPayment]', N'VatWithholdingTaxId') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_CustomerPayment_Taxes_VatWithholdingTaxId')
                    EXEC(N'ALTER TABLE [dbo].[CustomerPayment] ADD CONSTRAINT [FK_CustomerPayment_Taxes_VatWithholdingTaxId] FOREIGN KEY ([VatWithholdingTaxId]) REFERENCES [dbo].[Taxes] ([Id]) ON DELETE NO ACTION;');

                IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[Accounts]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[CustomerPayment]', N'VatWithholdingAccountId') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_CustomerPayment_Accounts_VatWithholdingAccountId')
                    EXEC(N'ALTER TABLE [dbo].[CustomerPayment] ADD CONSTRAINT [FK_CustomerPayment_Accounts_VatWithholdingAccountId] FOREIGN KEY ([VatWithholdingAccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION;');
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey("FK_CustomerPayment_Accounts_VatWithholdingAccountId", "CustomerPayment");
            migrationBuilder.DropForeignKey("FK_CustomerPayment_Taxes_VatWithholdingTaxId", "CustomerPayment");
            migrationBuilder.DropForeignKey("FK_CustomerPayment_Accounts_WithholdingTaxAccountId", "CustomerPayment");
            migrationBuilder.DropForeignKey("FK_CustomerPayment_Taxes_WithholdingTaxId", "CustomerPayment");
            migrationBuilder.DropForeignKey("FK_VendorPayment_Accounts_WithholdingTaxAccountId", "VendorPayment");
            migrationBuilder.DropForeignKey("FK_VendorPayment_Taxes_WithholdingTaxId", "VendorPayment");
            migrationBuilder.DropForeignKey("FK_VendorInvoice_Accounts_WithholdingTaxAccountId", "VendorInvoice");
            migrationBuilder.DropForeignKey("FK_VendorInvoice_Taxes_WithholdingTaxId", "VendorInvoice");
            migrationBuilder.DropForeignKey("FK_VendorInvoiceLineItem_TaxGroups_TaxGroupId", "VendorInvoiceLineItem");

            migrationBuilder.DropIndex("IX_CustomerPayment_VatWithholdingAccountId", "CustomerPayment");
            migrationBuilder.DropIndex("IX_CustomerPayment_VatWithholdingTaxId", "CustomerPayment");
            migrationBuilder.DropIndex("IX_CustomerPayment_WithholdingTaxAccountId", "CustomerPayment");
            migrationBuilder.DropIndex("IX_CustomerPayment_WithholdingTaxId", "CustomerPayment");
            migrationBuilder.DropIndex("IX_VendorPayment_WithholdingTaxAccountId", "VendorPayment");
            migrationBuilder.DropIndex("IX_VendorPayment_WithholdingTaxId", "VendorPayment");
            migrationBuilder.DropIndex("IX_VendorInvoice_WithholdingTaxAccountId", "VendorInvoice");
            migrationBuilder.DropIndex("IX_VendorInvoice_WithholdingTaxId", "VendorInvoice");
            migrationBuilder.DropIndex("IX_VendorInvoiceLineItem_TaxGroupId", "VendorInvoiceLineItem");

            migrationBuilder.DropColumn("WithholdingCertificateDate", "CustomerPayment");
            migrationBuilder.DropColumn("WithholdingCertificateNumber", "CustomerPayment");
            migrationBuilder.DropColumn("VatWithholdingAmount", "CustomerPayment");
            migrationBuilder.DropColumn("VatWithholdingAccountId", "CustomerPayment");
            migrationBuilder.DropColumn("VatWithholdingTaxId", "CustomerPayment");
            migrationBuilder.DropColumn("WithholdingTaxAmount", "CustomerPayment");
            migrationBuilder.DropColumn("WithholdingTaxAccountId", "CustomerPayment");
            migrationBuilder.DropColumn("WithholdingTaxId", "CustomerPayment");
            migrationBuilder.DropColumn("WithholdingCertificateDate", "VendorPayment");
            migrationBuilder.DropColumn("WithholdingCertificateNumber", "VendorPayment");
            migrationBuilder.DropColumn("WithholdingTaxAccountId", "VendorPayment");
            migrationBuilder.DropColumn("WithholdingTaxId", "VendorPayment");
            migrationBuilder.DropColumn("WithholdingCertificateDate", "VendorInvoice");
            migrationBuilder.DropColumn("WithholdingCertificateNumber", "VendorInvoice");
            migrationBuilder.DropColumn("WithholdingTaxAccountId", "VendorInvoice");
            migrationBuilder.DropColumn("WithholdingTaxId", "VendorInvoice");
            migrationBuilder.DropColumn("TaxTreatment", "VendorInvoiceLineItem");
            migrationBuilder.DropColumn("TaxGroupId", "VendorInvoiceLineItem");
            migrationBuilder.DropColumn("TaxTreatment", "InvoiceLineItem");
            migrationBuilder.DropColumn("TaxTreatment", "Suppliers");
            migrationBuilder.DropColumn("IsWithholdingTaxApplicable", "Suppliers");
            migrationBuilder.DropColumn("TaxTreatment", "BusinessPartners");
            migrationBuilder.DropColumn("IsVatWithholdingAgent", "BusinessPartners");
        }
    }
}
