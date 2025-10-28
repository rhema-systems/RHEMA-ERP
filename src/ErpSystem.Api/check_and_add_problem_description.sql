-- Check if ProblemDescription column exists in JobCard table
IF NOT EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'JobCard' 
    AND COLUMN_NAME = 'ProblemDescription'
)
BEGIN
    PRINT 'ProblemDescription column does not exist. Adding column...'
    
    ALTER TABLE JobCard
    ADD ProblemDescription nvarchar(2000) NULL
    
    PRINT 'ProblemDescription column added successfully.'
END
ELSE
BEGIN
    PRINT 'ProblemDescription column already exists.'
END
GO

-- Verify the column
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'JobCard' 
AND COLUMN_NAME = 'ProblemDescription'
GO
