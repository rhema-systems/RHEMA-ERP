-- Check if EmailSettings table exists and show structure
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'EmailSettings'
ORDER BY ORDINAL_POSITION;

-- Check ALL records in EmailSettings table (including soft deleted)
SELECT 
    Id,
    SmtpHost,
    SmtpPort,
    SmtpUsername,
    UseTLS,
    FromAddress,
    FromName,
    TenantId,
    IsDeleted,
    CreatedAt,
    CreatedBy
FROM EmailSettings;

-- Check count of records
SELECT 
    COUNT(*) as TotalRecords,
    COUNT(CASE WHEN IsDeleted = 0 THEN 1 END) as ActiveRecords,
    COUNT(CASE WHEN IsDeleted = 1 THEN 1 END) as DeletedRecords
FROM EmailSettings;

-- Check what TenantId should match
SELECT Id, Name, Code FROM Tenants WHERE Id = '00000000-0000-0000-0000-000000000001';