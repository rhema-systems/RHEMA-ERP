-- Migration: Add UOM and Price List fields to PurchaseOrderItems
-- Date: 2026-01-27
-- Description: Adds Unit of Measure and Price List tracking to Purchase Order Items

-- Add UnitOfMeasure column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PurchaseOrderItems]') AND name = 'UnitOfMeasure')
BEGIN
    ALTER TABLE [dbo].[PurchaseOrderItems]
    ADD [UnitOfMeasure] NVARCHAR(20) NOT NULL DEFAULT 'EA';
END
GO

-- Add ItemUnitOfMeasureId column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PurchaseOrderItems]') AND name = 'ItemUnitOfMeasureId')
BEGIN
    ALTER TABLE [dbo].[PurchaseOrderItems]
    ADD [ItemUnitOfMeasureId] UNIQUEIDENTIFIER NULL;
END
GO

-- Add PriceListLineId column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PurchaseOrderItems]') AND name = 'PriceListLineId')
BEGIN
    ALTER TABLE [dbo].[PurchaseOrderItems]
    ADD [PriceListLineId] UNIQUEIDENTIFIER NULL;
END
GO

-- Add foreign key constraint for ItemUnitOfMeasureId
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PurchaseOrderItems_ItemUnitOfMeasures_ItemUnitOfMeasureId')
BEGIN
    ALTER TABLE [dbo].[PurchaseOrderItems]
    ADD CONSTRAINT [FK_PurchaseOrderItems_ItemUnitOfMeasures_ItemUnitOfMeasureId]
    FOREIGN KEY ([ItemUnitOfMeasureId])
    REFERENCES [dbo].[ItemUnitOfMeasures]([Id])
    ON DELETE SET NULL;
END
GO

-- Add foreign key constraint for PriceListLineId
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PurchaseOrderItems_PriceListLines_PriceListLineId')
BEGIN
    ALTER TABLE [dbo].[PurchaseOrderItems]
    ADD CONSTRAINT [FK_PurchaseOrderItems_PriceListLines_PriceListLineId]
    FOREIGN KEY ([PriceListLineId])
    REFERENCES [dbo].[PriceListLines]([Id])
    ON DELETE SET NULL;
END
GO

-- Create index for ItemUnitOfMeasureId
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PurchaseOrderItems_ItemUnitOfMeasureId' AND object_id = OBJECT_ID(N'[dbo].[PurchaseOrderItems]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PurchaseOrderItems_ItemUnitOfMeasureId]
    ON [dbo].[PurchaseOrderItems] ([ItemUnitOfMeasureId]);
END
GO

-- Create index for PriceListLineId
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PurchaseOrderItems_PriceListLineId' AND object_id = OBJECT_ID(N'[dbo].[PurchaseOrderItems]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PurchaseOrderItems_PriceListLineId]
    ON [dbo].[PurchaseOrderItems] ([PriceListLineId]);
END
GO

-- Update existing records to use the inventory item's UOM if available
UPDATE poi
SET poi.UnitOfMeasure = COALESCE(ii.UnitOfMeasure, 'EA')
FROM [dbo].[PurchaseOrderItems] poi
INNER JOIN [dbo].[InventoryItems] ii ON poi.InventoryItemId = ii.Id
WHERE poi.UnitOfMeasure = 'EA' AND ii.UnitOfMeasure IS NOT NULL;
GO

PRINT 'Migration completed: Added UOM and Price List fields to PurchaseOrderItems';
GO
