using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260720033000_LinkApArPaymentsToConfiguredPaymentMethods")]
public partial class LinkApArPaymentsToConfiguredPaymentMethods : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[VendorPayment]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.VendorPayment', N'PaymentMethodId') IS NULL
BEGIN
    ALTER TABLE [dbo].[VendorPayment] ADD [PaymentMethodId] uniqueidentifier NULL;
END;
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[PaymentBatch]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.PaymentBatch', N'PaymentMethodId') IS NULL
BEGIN
    ALTER TABLE [dbo].[PaymentBatch] ADD [PaymentMethodId] uniqueidentifier NULL;
END;
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.CustomerPayment', N'PaymentMethodId') IS NULL
BEGIN
    ALTER TABLE [dbo].[CustomerPayment] ADD [PaymentMethodId] uniqueidentifier NULL;
END;
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[VendorPayment]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[PaymentMethod]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.VendorPayment', N'PaymentMethodId') IS NOT NULL
BEGIN
    UPDATE vp
    SET [PaymentMethodId] = selected.[Id]
    FROM [dbo].[VendorPayment] vp
    CROSS APPLY
    (
        SELECT TOP (1) pm.[Id]
        FROM [dbo].[PaymentMethod] pm
        WHERE pm.[TenantId] = vp.[TenantId]
          AND pm.[IsDeleted] = 0
          AND pm.[IsActive] = 1
          AND (
                (vp.[PaymentMethod] = 1 AND pm.[Type] IN (8, 3)) OR
                (vp.[PaymentMethod] = 2 AND pm.[Type] = 2) OR
                (vp.[PaymentMethod] = 3 AND pm.[Type] = 1) OR
                (vp.[PaymentMethod] = 4 AND pm.[Type] IN (3, 8)) OR
                (vp.[PaymentMethod] = 5 AND pm.[Type] = 5) OR
                (vp.[PaymentMethod] = 6 AND pm.[Type] = 6)
              )
        ORDER BY
            CASE
                WHEN vp.[PaymentMethod] = 1 AND pm.[Type] = 8 THEN 0
                WHEN vp.[PaymentMethod] = 4 AND pm.[Type] = 3 THEN 0
                ELSE 1
            END,
            pm.[Name]
    ) selected
    WHERE vp.[PaymentMethodId] IS NULL;
END;
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[PaymentBatch]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[PaymentMethod]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.PaymentBatch', N'PaymentMethodId') IS NOT NULL
BEGIN
    UPDATE pb
    SET [PaymentMethodId] = selected.[Id]
    FROM [dbo].[PaymentBatch] pb
    CROSS APPLY
    (
        SELECT TOP (1) pm.[Id]
        FROM [dbo].[PaymentMethod] pm
        WHERE pm.[TenantId] = pb.[TenantId]
          AND pm.[IsDeleted] = 0
          AND pm.[IsActive] = 1
          AND (
                (pb.[PaymentMethod] = 1 AND pm.[Type] IN (8, 3)) OR
                (pb.[PaymentMethod] = 2 AND pm.[Type] = 2) OR
                (pb.[PaymentMethod] = 3 AND pm.[Type] = 1) OR
                (pb.[PaymentMethod] = 4 AND pm.[Type] IN (3, 8)) OR
                (pb.[PaymentMethod] = 5 AND pm.[Type] = 5) OR
                (pb.[PaymentMethod] = 6 AND pm.[Type] = 6)
              )
        ORDER BY
            CASE
                WHEN pb.[PaymentMethod] = 1 AND pm.[Type] = 8 THEN 0
                WHEN pb.[PaymentMethod] = 4 AND pm.[Type] = 3 THEN 0
                ELSE 1
            END,
            pm.[Name]
    ) selected
    WHERE pb.[PaymentMethodId] IS NULL;
END;
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[PaymentMethod]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.CustomerPayment', N'PaymentMethodId') IS NOT NULL
BEGIN
    UPDATE cp
    SET [PaymentMethodId] = selected.[Id]
    FROM [dbo].[CustomerPayment] cp
    CROSS APPLY
    (
        SELECT TOP (1) pm.[Id]
        FROM [dbo].[PaymentMethod] pm
        CROSS APPLY
        (
            SELECT LOWER(REPLACE(REPLACE(REPLACE(ISNULL(cp.[PaymentMethod], N''), N' ', N''), N'-', N''), N'_', N'')) AS [MethodKey]
        ) normalized
        WHERE pm.[TenantId] = cp.[TenantId]
          AND pm.[IsDeleted] = 0
          AND pm.[IsActive] = 1
          AND cp.[IsCreditNote] = 0
          AND (
                (normalized.[MethodKey] = N'cash' AND pm.[Type] = 1) OR
                (normalized.[MethodKey] IN (N'cheque', N'check') AND pm.[Type] = 2) OR
                (normalized.[MethodKey] IN (N'eft', N'electronicfundstransfer') AND pm.[Type] = 3) OR
                (normalized.[MethodKey] IN (N'card', N'creditcard', N'debitcard') AND pm.[Type] = 4) OR
                (normalized.[MethodKey] IN (N'mobilemoney', N'momo') AND pm.[Type] = 5) OR
                (normalized.[MethodKey] = N'directdebit' AND pm.[Type] = 6) OR
                (normalized.[MethodKey] = N'standingorder' AND pm.[Type] = 7) OR
                (normalized.[MethodKey] = N'banktransfer' AND pm.[Type] IN (8, 3))
              )
        ORDER BY
            CASE
                WHEN normalized.[MethodKey] = N'banktransfer' AND pm.[Type] = 8 THEN 0
                ELSE 1
            END,
            pm.[Name]
    ) selected
    WHERE cp.[PaymentMethodId] IS NULL;
END;
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[VendorPayment]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.VendorPayment', N'PaymentMethodId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_VendorPayment_TenantId_PaymentMethodId' AND [object_id] = OBJECT_ID(N'[dbo].[VendorPayment]'))
BEGIN
    CREATE INDEX [IX_VendorPayment_TenantId_PaymentMethodId] ON [dbo].[VendorPayment] ([TenantId], [PaymentMethodId]);
END;
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[PaymentBatch]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.PaymentBatch', N'PaymentMethodId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_PaymentBatch_TenantId_PaymentMethodId' AND [object_id] = OBJECT_ID(N'[dbo].[PaymentBatch]'))
BEGIN
    CREATE INDEX [IX_PaymentBatch_TenantId_PaymentMethodId] ON [dbo].[PaymentBatch] ([TenantId], [PaymentMethodId]);
END;
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.CustomerPayment', N'PaymentMethodId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CustomerPayment_TenantId_PaymentMethodId' AND [object_id] = OBJECT_ID(N'[dbo].[CustomerPayment]'))
BEGIN
    CREATE INDEX [IX_CustomerPayment_TenantId_PaymentMethodId] ON [dbo].[CustomerPayment] ([TenantId], [PaymentMethodId]);
END;
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[VendorPayment]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[PaymentMethod]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.VendorPayment', N'PaymentMethodId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_VendorPayment_PaymentMethod_PaymentMethodId')
BEGIN
    ALTER TABLE [dbo].[VendorPayment] WITH CHECK
        ADD CONSTRAINT [FK_VendorPayment_PaymentMethod_PaymentMethodId]
        FOREIGN KEY ([PaymentMethodId]) REFERENCES [dbo].[PaymentMethod] ([Id]);
END;
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[PaymentBatch]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[PaymentMethod]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.PaymentBatch', N'PaymentMethodId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_PaymentBatch_PaymentMethod_PaymentMethodId')
BEGIN
    ALTER TABLE [dbo].[PaymentBatch] WITH CHECK
        ADD CONSTRAINT [FK_PaymentBatch_PaymentMethod_PaymentMethodId]
        FOREIGN KEY ([PaymentMethodId]) REFERENCES [dbo].[PaymentMethod] ([Id]);
END;
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[PaymentMethod]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.CustomerPayment', N'PaymentMethodId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_CustomerPayment_PaymentMethod_PaymentMethodId')
BEGIN
    ALTER TABLE [dbo].[CustomerPayment] WITH CHECK
        ADD CONSTRAINT [FK_CustomerPayment_PaymentMethod_PaymentMethodId]
        FOREIGN KEY ([PaymentMethodId]) REFERENCES [dbo].[PaymentMethod] ([Id]);
END;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_CustomerPayment_PaymentMethod_PaymentMethodId')
    ALTER TABLE [dbo].[CustomerPayment] DROP CONSTRAINT [FK_CustomerPayment_PaymentMethod_PaymentMethodId];

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_PaymentBatch_PaymentMethod_PaymentMethodId')
    ALTER TABLE [dbo].[PaymentBatch] DROP CONSTRAINT [FK_PaymentBatch_PaymentMethod_PaymentMethodId];

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_VendorPayment_PaymentMethod_PaymentMethodId')
    ALTER TABLE [dbo].[VendorPayment] DROP CONSTRAINT [FK_VendorPayment_PaymentMethod_PaymentMethodId];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CustomerPayment_TenantId_PaymentMethodId' AND [object_id] = OBJECT_ID(N'[dbo].[CustomerPayment]'))
    DROP INDEX [IX_CustomerPayment_TenantId_PaymentMethodId] ON [dbo].[CustomerPayment];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_PaymentBatch_TenantId_PaymentMethodId' AND [object_id] = OBJECT_ID(N'[dbo].[PaymentBatch]'))
    DROP INDEX [IX_PaymentBatch_TenantId_PaymentMethodId] ON [dbo].[PaymentBatch];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_VendorPayment_TenantId_PaymentMethodId' AND [object_id] = OBJECT_ID(N'[dbo].[VendorPayment]'))
    DROP INDEX [IX_VendorPayment_TenantId_PaymentMethodId] ON [dbo].[VendorPayment];

IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.CustomerPayment', N'PaymentMethodId') IS NOT NULL
    ALTER TABLE [dbo].[CustomerPayment] DROP COLUMN [PaymentMethodId];

IF OBJECT_ID(N'[dbo].[PaymentBatch]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.PaymentBatch', N'PaymentMethodId') IS NOT NULL
    ALTER TABLE [dbo].[PaymentBatch] DROP COLUMN [PaymentMethodId];

IF OBJECT_ID(N'[dbo].[VendorPayment]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.VendorPayment', N'PaymentMethodId') IS NOT NULL
    ALTER TABLE [dbo].[VendorPayment] DROP COLUMN [PaymentMethodId];
""");
    }
}
