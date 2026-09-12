-- Exact additive migration only. Never run this rehearsal script against the main UAT database.
SET XACT_ABORT ON;
IF DB_NAME() <> N'RhemaERP_PO_Rehearsal_20260909'
    THROW 51000, 'This script only supports the isolated PO rehearsal database.', 1;
BEGIN TRANSACTION;
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20260909213000_LinkLandedCostsToSupplierInvoiceLines')
BEGIN
    IF COL_LENGTH('dbo.VendorInvoiceLineItem', 'LandedCostItemId') IS NOT NULL
        THROW 51001, 'Unexpected existing column without its migration record; review schema before proceeding.', 1;
    ALTER TABLE dbo.VendorInvoiceLineItem ADD LandedCostItemId uniqueidentifier NULL;
    EXEC(N'CREATE UNIQUE INDEX IX_VendorInvoiceLineItem_LandedCostItemId ON dbo.VendorInvoiceLineItem(LandedCostItemId) WHERE LandedCostItemId IS NOT NULL AND IsDeleted = 0');
    ALTER TABLE dbo.VendorInvoiceLineItem WITH CHECK ADD CONSTRAINT FK_VendorInvoiceLineItem_LandedCostItems_LandedCostItemId
        FOREIGN KEY(LandedCostItemId) REFERENCES dbo.LandedCostItems(Id);
    DECLARE @version nvarchar(32) = (SELECT TOP (1) ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId DESC);
    IF @version IS NULL THROW 51002, 'Existing EF migration version is required.', 1;
    INSERT dbo.__EFMigrationsHistory(MigrationId, ProductVersion)
    VALUES(N'20260909213000_LinkLandedCostsToSupplierInvoiceLines', @version);
END;
COMMIT;
SELECT DB_NAME() AS DatabaseName, COL_LENGTH('dbo.VendorInvoiceLineItem','LandedCostItemId') AS LinkColumnBytes;
SELECT name, is_unique, filter_definition FROM sys.indexes
WHERE object_id = OBJECT_ID('dbo.VendorInvoiceLineItem') AND name = 'IX_VendorInvoiceLineItem_LandedCostItemId';
SELECT name, is_disabled, is_not_trusted FROM sys.foreign_keys
WHERE name = 'FK_VendorInvoiceLineItem_LandedCostItems_LandedCostItemId';
