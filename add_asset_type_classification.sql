-- Add AssetType classification column to MaintenanceAssetCategories table

PRINT 'Adding AssetType column to MaintenanceAssetCategories table...';

-- Add the AssetType column (only if it doesn't exist)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[MaintenanceAssetCategories]') AND name = 'AssetType')
BEGIN
    ALTER TABLE [dbo].[MaintenanceAssetCategories] 
    ADD [AssetType] NVARCHAR(50) NULL;
    PRINT 'Added AssetType column';
END
ELSE
    PRINT 'AssetType column already exists';

-- Update existing categories with default values based on common names
UPDATE [dbo].[MaintenanceAssetCategories] 
SET [AssetType] = 
    CASE 
        WHEN [Name] LIKE '%HVAC%' OR [Name] LIKE '%Air%' OR [Name] LIKE '%Heating%' OR [Name] LIKE '%Cooling%' THEN 'Equipment'
        WHEN [Name] LIKE '%Electrical%' OR [Name] LIKE '%Electric%' OR [Name] LIKE '%Power%' THEN 'Equipment'
        WHEN [Name] LIKE '%Mechanical%' OR [Name] LIKE '%Machine%' OR [Name] LIKE '%Engine%' THEN 'Equipment'
        WHEN [Name] LIKE '%Vehicle%' OR [Name] LIKE '%Car%' OR [Name] LIKE '%Truck%' OR [Name] LIKE '%Van%' THEN 'Vehicle'
        WHEN [Name] LIKE '%Building%' OR [Name] LIKE '%Facility%' OR [Name] LIKE '%Structure%' THEN 'Building'
        WHEN [Name] LIKE '%Safety%' OR [Name] LIKE '%Security%' OR [Name] LIKE '%Fire%' THEN 'Safety'
        WHEN [Name] LIKE '%IT%' OR [Name] LIKE '%Computer%' OR [Name] LIKE '%Network%' OR [Name] LIKE '%Software%' THEN 'ITAsset'
        WHEN [Name] LIKE '%Tool%' OR [Name] LIKE '%Hand%' OR [Name] LIKE '%Portable%' THEN 'Tool'
        WHEN [Name] LIKE '%Furniture%' OR [Name] LIKE '%Desk%' OR [Name] LIKE '%Chair%' THEN 'Furniture'
        ELSE 'Equipment' -- Default fallback
    END;

-- Create index for better performance (only if it doesn't exist)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MaintenanceAssetCategories_AssetType' AND object_id = OBJECT_ID('[dbo].[MaintenanceAssetCategories]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_MaintenanceAssetCategories_AssetType] 
    ON [dbo].[MaintenanceAssetCategories] ([AssetType]);
    PRINT 'Created index IX_MaintenanceAssetCategories_AssetType';
END
ELSE
    PRINT 'Index IX_MaintenanceAssetCategories_AssetType already exists';

PRINT 'Successfully added AssetType column to MaintenanceAssetCategories table with default values';
