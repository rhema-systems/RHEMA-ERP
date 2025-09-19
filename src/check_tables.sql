-- Check if the new tables have been created
SELECT 
    TABLE_NAME,
    TABLE_TYPE
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_NAME IN (
    'AuditLogs',
    'SecurityLogs', 
    'EmailSettings',
    'PasswordPolicies',
    'SystemSettings'
)
ORDER BY TABLE_NAME;

-- Count rows in each table (should be 0 for new tables)
SELECT 'AuditLogs' as TableName, COUNT(*) as RowCount FROM AuditLogs
UNION ALL
SELECT 'SecurityLogs' as TableName, COUNT(*) as RowCount FROM SecurityLogs
UNION ALL
SELECT 'EmailSettings' as TableName, COUNT(*) as RowCount FROM EmailSettings
UNION ALL
SELECT 'PasswordPolicies' as TableName, COUNT(*) as RowCount FROM PasswordPolicies
UNION ALL
SELECT 'SystemSettings' as TableName, COUNT(*) as RowCount FROM SystemSettings;

-- Show the structure of one of the new tables
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'AuditLogs'
ORDER BY ORDINAL_POSITION;