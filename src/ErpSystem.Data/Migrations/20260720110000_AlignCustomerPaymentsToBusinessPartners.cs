using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260720110000_AlignCustomerPaymentsToBusinessPartners")]
public partial class AlignCustomerPaymentsToBusinessPartners : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'[dbo].[BusinessPartners]', N'U') IS NULL
    BEGIN
        THROW 51000, 'Cannot align AR receipts: BusinessPartners table is missing.', 1;
    END;

    -- CustomerPayment.CustomerId is retained as an API-compatible property name, but its
    -- canonical identity is now BusinessPartner.Id. Upgrade receipt-only legacy customers
    -- before replacing the physical FK so existing posted AR evidence remains attributable.
    IF OBJECT_ID(N'[dbo].[Customers]', N'U') IS NOT NULL
    BEGIN
        INSERT INTO [dbo].[BusinessPartners] (
            [Id], [PartnerCode], [PartnerName], [PartnerType], [TaxIdentificationNumber],
            [PrimaryContactName], [PrimaryEmail], [PrimaryPhone], [PhysicalAddress],
            [PhysicalCity], [PhysicalState], [PhysicalCountry], [CustomerAccountNumber],
            [CustomerType], [CreditLimit], [OutstandingBalance], [PaymentTermId], [Currency],
            [DefaultApAccountId], [DefaultArAccountId], [DefaultExpenseAccountId],
            [RegistrationStatus], [IsPreferred], [IsActive], [IsBlacklisted],
            [IsVatWithholdingAgent], [TaxTreatment], [IsTaxExempt], [IsOnCreditHold], [Notes],
            [TenantId], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy], [CreatedById],
            [LastModifiedById], [IsDeleted], [DeletedAt], [DeletedBy])
        SELECT DISTINCT
            c.[Id],
            LEFT(CONCAT(N'AR-CUST-', CONVERT(nvarchar(36), c.[Id])), 50),
            LEFT(COALESCE(NULLIF(c.[CustomerName], N''), N'Legacy Customer'), 200),
            N'Customer', c.[TaxId], c.[ContactPerson], c.[Email], c.[Phone], c.[Address],
            c.[City], c.[State], c.[Country],
            LEFT(COALESCE(NULLIF(c.[CustomerCode], N''), CONCAT(N'CUST-', CONVERT(nvarchar(36), c.[Id]))), 50),
            LEFT(COALESCE(NULLIF(c.[CustomerType], N''), N'Customer'), 50),
            c.[CreditLimit], c.[OutstandingBalance], c.[PaymentTermId], c.[CurrencyCode],
            c.[DefaultApAccountId], c.[DefaultArAccountId], c.[DefaultExpenseAccountId],
            N'Approved', CAST(0 AS bit), c.[IsActive], CAST(0 AS bit), CAST(0 AS bit),
            1, CAST(0 AS bit), CAST(0 AS bit), c.[Notes], c.[TenantId],
            COALESCE(c.[CreatedAt], SYSUTCDATETIME()), c.[UpdatedAt], c.[CreatedBy], c.[UpdatedBy],
            c.[CreatedById], c.[LastModifiedById], c.[IsDeleted], c.[DeletedAt], c.[DeletedBy]
        FROM [dbo].[CustomerPayment] cp
        INNER JOIN [dbo].[Customers] c
            ON c.[Id] = cp.[CustomerId]
           AND c.[TenantId] = cp.[TenantId]
        LEFT JOIN [dbo].[BusinessPartners] bp
            ON bp.[Id] = c.[Id]
        WHERE bp.[Id] IS NULL;
    END;

    IF EXISTS (
        SELECT 1
        FROM [dbo].[CustomerPayment] cp
        LEFT JOIN [dbo].[BusinessPartners] bp
            ON bp.[Id] = cp.[CustomerId]
           AND bp.[TenantId] = cp.[TenantId]
           AND bp.[IsDeleted] = CAST(0 AS bit)
        WHERE bp.[Id] IS NULL)
    BEGIN
        THROW 51000, 'Cannot align AR receipts: existing payments could not be mapped to same-tenant active BusinessPartners.', 1;
    END;

    DECLARE @legacyCustomerFk sysname;
    SELECT TOP (1) @legacyCustomerFk = fk.[name]
    FROM sys.foreign_keys fk
    INNER JOIN sys.foreign_key_columns fkc ON fkc.[constraint_object_id] = fk.[object_id]
    INNER JOIN sys.columns pc
        ON pc.[object_id] = fkc.[parent_object_id]
       AND pc.[column_id] = fkc.[parent_column_id]
    WHERE fk.[parent_object_id] = OBJECT_ID(N'[dbo].[CustomerPayment]')
      AND fk.[referenced_object_id] = OBJECT_ID(N'[dbo].[Customers]')
      AND pc.[name] = N'CustomerId';

    IF @legacyCustomerFk IS NOT NULL
    BEGIN
        DECLARE @dropLegacyCustomerFkSql nvarchar(max) =
            N'ALTER TABLE [dbo].[CustomerPayment] DROP CONSTRAINT ' + QUOTENAME(@legacyCustomerFk);
        EXEC sp_executesql @dropLegacyCustomerFkSql;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE [name] = N'FK_CustomerPayment_BusinessPartners_CustomerId'
          AND [parent_object_id] = OBJECT_ID(N'[dbo].[CustomerPayment]'))
    BEGIN
        ALTER TABLE [dbo].[CustomerPayment] WITH CHECK
        ADD CONSTRAINT [FK_CustomerPayment_BusinessPartners_CustomerId]
        FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[BusinessPartners] ([Id]) ON DELETE NO ACTION;

        ALTER TABLE [dbo].[CustomerPayment]
        CHECK CONSTRAINT [FK_CustomerPayment_BusinessPartners_CustomerId];
    END;
END;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'[dbo].[Customers]', N'U') IS NULL
       OR EXISTS (
            SELECT 1
            FROM [dbo].[CustomerPayment] cp
            LEFT JOIN [dbo].[Customers] c
                ON c.[Id] = cp.[CustomerId]
               AND c.[TenantId] = cp.[TenantId]
            WHERE c.[Id] IS NULL)
    BEGIN
        THROW 51000, 'Cannot roll back AR receipt identity: current payments do not all map to same-tenant legacy Customers.', 1;
    END;

    IF EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE [name] = N'FK_CustomerPayment_BusinessPartners_CustomerId'
          AND [parent_object_id] = OBJECT_ID(N'[dbo].[CustomerPayment]'))
    BEGIN
        ALTER TABLE [dbo].[CustomerPayment]
        DROP CONSTRAINT [FK_CustomerPayment_BusinessPartners_CustomerId];
    END;

    IF NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE [name] = N'FK_CustomerPayment_Customers_CustomerId'
          AND [parent_object_id] = OBJECT_ID(N'[dbo].[CustomerPayment]'))
    BEGIN
        ALTER TABLE [dbo].[CustomerPayment] WITH CHECK
        ADD CONSTRAINT [FK_CustomerPayment_Customers_CustomerId]
        FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customers] ([Id]) ON DELETE NO ACTION;
    END;
END;
""");
    }
}
