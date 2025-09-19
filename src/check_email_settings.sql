-- Check if EmailSettings table exists and has data
SELECT 
    TABLE_NAME,
    TABLE_TYPE
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_NAME = 'EmailSettings';

-- Check all records in EmailSettings table
SELECT * FROM EmailSettings WHERE IsDeleted = 0;

-- Check if any records exist (including soft deleted)
SELECT COUNT(*) as TotalRecords FROM EmailSettings;

-- Check what TenantIds exist in the system
SELECT Id, Name, Code FROM Tenants WHERE IsDeleted = 0;

-- Check current user and tenant from Users table
SELECT TOP 5 Id, UserName, Email, TenantId, IsActive FROM Users WHERE IsDeleted = 0;