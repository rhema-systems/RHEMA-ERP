-- Check the actual structure of BusinessPartnerRegistrations table
USE RhemaERP;
GO

-- Check if table exists
SELECT TABLE_NAME, TABLE_SCHEMA 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_NAME = 'BusinessPartnerRegistrations';
GO

-- Get column structure
SELECT 
    COLUMN_NAME, 
    DATA_TYPE, 
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'BusinessPartnerRegistrations' 
ORDER BY ORDINAL_POSITION;
GO

-- Check for audit columns specifically
SELECT 
    COLUMN_NAME, 
    DATA_TYPE, 
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'BusinessPartnerRegistrations' 
AND COLUMN_NAME IN ('CreatedAt', 'CreatedBy', 'CreatedById', 'UpdatedAt', 'UpdatedBy', 'LastModifiedById', 'IsDeleted', 'DeletedAt', 'DeletedBy')
ORDER BY COLUMN_NAME;
GO