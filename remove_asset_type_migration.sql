-- Remove AssetTypeId foreign key constraint and column from MaintenanceAssets table
-- This should be run as a database migration

PRINT 'Starting removal of AssetTypeId dependencies...';

-- Step 1: Drop the foreign key constraint
IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_MaintenanceAssets_AssetTypes_AssetTypeId')
BEGIN
    ALTER TABLE [dbo].[MaintenanceAssets] DROP CONSTRAINT [FK_MaintenanceAssets_AssetTypes_AssetTypeId];
    PRINT 'Dropped foreign key constraint FK_MaintenanceAssets_AssetTypes_AssetTypeId';
END
ELSE
    PRINT 'Foreign key constraint FK_MaintenanceAssets_AssetTypes_AssetTypeId not found';

-- Step 2: Drop any indexes that depend on AssetTypeId column
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MaintenanceAssets_AssetTypeId' AND object_id = OBJECT_ID('[dbo].[MaintenanceAssets]'))
BEGIN
    DROP INDEX [IX_MaintenanceAssets_AssetTypeId] ON [dbo].[MaintenanceAssets];
    PRINT 'Dropped index IX_MaintenanceAssets_AssetTypeId';
END
ELSE
    PRINT 'Index IX_MaintenanceAssets_AssetTypeId not found';

-- Step 3: Check for other indexes that might include AssetTypeId
DECLARE @sql NVARCHAR(MAX) = '';
SELECT @sql = @sql + 'DROP INDEX [' + i.name + '] ON [dbo].[MaintenanceAssets];' + CHAR(13)
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE i.object_id = OBJECT_ID('[dbo].[MaintenanceAssets]')
  AND c.name = 'AssetTypeId'
  AND i.name != 'IX_MaintenanceAssets_AssetTypeId'; -- We already handled this one

IF @sql != ''
BEGIN
    PRINT 'Dropping additional indexes that reference AssetTypeId:';
    PRINT @sql;
    EXEC sp_executesql @sql;
END

-- Step 4: Drop the AssetTypeId column
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[MaintenanceAssets]') AND name = 'AssetTypeId')
BEGIN
    ALTER TABLE [dbo].[MaintenanceAssets] DROP COLUMN [AssetTypeId];
    PRINT 'Dropped AssetTypeId column from MaintenanceAssets table';
END
ELSE
    PRINT 'AssetTypeId column not found in MaintenanceAssets table';

-- Optional: If you want to remove the entire AssetTypes table (since it's redundant)
-- Uncomment the lines below only if you're sure AssetTypes is not used elsewhere
/*
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'AssetTypes')
BEGIN
    DROP TABLE [dbo].[AssetTypes];
    PRINT 'Dropped AssetTypes table';
END
*/

PRINT 'Successfully completed removal of AssetTypeId dependencies from MaintenanceAssets table';
