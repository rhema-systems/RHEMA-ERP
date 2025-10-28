-- Script to seed technician IDs into existing task template records
-- This script assigns technicians from the Employees table to task templates

USE [RHEMA-ERP];
GO

DECLARE @TechnicianId UNIQUEIDENTIFIER;
DECLARE @TechnicianId2 UNIQUEIDENTIFIER;
DECLARE @RowCount INT;

-- Get the first two technicians
SELECT TOP 1 @TechnicianId = Id FROM Employees WHERE IsTechnician = 1 ORDER BY CreatedAt;

SELECT @TechnicianId2 = Id 
FROM Employees 
WHERE IsTechnician = 1 AND Id != @TechnicianId 
ORDER BY CreatedAt 
OFFSET 0 ROWS FETCH NEXT 1 ROWS ONLY;

-- If no technician found, get any employee
IF @TechnicianId IS NULL
BEGIN
    SELECT TOP 1 @TechnicianId = Id FROM Employees ORDER BY CreatedAt;
    PRINT 'Warning: No technicians found. Using first employee.';
END
ELSE
BEGIN
    PRINT 'Primary Technician ID: ' + CAST(@TechnicianId AS NVARCHAR(50));
END

IF @TechnicianId2 IS NULL
BEGIN
    SET @TechnicianId2 = @TechnicianId;
    PRINT 'Secondary Technician ID: Same as primary (only one technician available)';
END
ELSE
BEGIN
    PRINT 'Secondary Technician ID: ' + CAST(@TechnicianId2 AS NVARCHAR(50));
END

-- Update MaintenanceTaskTemplates (alternate between two technicians based on sequence)
UPDATE MaintenanceTaskTemplates 
SET AssignedTechnicianId = CASE 
    WHEN Sequence % 2 = 1 THEN @TechnicianId 
    ELSE @TechnicianId2 
END,
UpdatedAt = GETUTCDATE()
WHERE AssignedTechnicianId IS NULL AND @TechnicianId IS NOT NULL;

SET @RowCount = @@ROWCOUNT;
PRINT 'Updated ' + CAST(@RowCount AS NVARCHAR(10)) + ' MaintenanceTaskTemplates';

-- Update AssetTypeTaskTemplates
UPDATE AssetTypeTaskTemplates 
SET AssignedTechnicianId = @TechnicianId,
UpdatedAt = GETUTCDATE()
WHERE AssignedTechnicianId IS NULL AND @TechnicianId IS NOT NULL;

SET @RowCount = @@ROWCOUNT;
PRINT 'Updated ' + CAST(@RowCount AS NVARCHAR(10)) + ' AssetTypeTaskTemplates';

-- Update AssetTaskTemplates
UPDATE AssetTaskTemplates 
SET AssignedTechnicianId = @TechnicianId,
UpdatedAt = GETUTCDATE()
WHERE AssignedTechnicianId IS NULL AND @TechnicianId IS NOT NULL;

SET @RowCount = @@ROWCOUNT;
PRINT 'Updated ' + CAST(@RowCount AS NVARCHAR(10)) + ' AssetTaskTemplates';

-- Show summary
PRINT '';
PRINT 'Summary:';
SELECT 
    'MaintenanceTaskTemplates' AS TableName,
    COUNT(*) AS TotalRecords,
    SUM(CASE WHEN AssignedTechnicianId IS NOT NULL THEN 1 ELSE 0 END) AS RecordsWithTechnician
FROM MaintenanceTaskTemplates
UNION ALL
SELECT 
    'AssetTypeTaskTemplates',
    COUNT(*),
    SUM(CASE WHEN AssignedTechnicianId IS NOT NULL THEN 1 ELSE 0 END)
FROM AssetTypeTaskTemplates
UNION ALL
SELECT 
    'AssetTaskTemplates',
    COUNT(*),
    SUM(CASE WHEN AssignedTechnicianId IS NOT NULL THEN 1 ELSE 0 END)
FROM AssetTaskTemplates;

PRINT 'Script completed successfully!';
GO
