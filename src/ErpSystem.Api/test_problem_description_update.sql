-- Step 1: Check if column exists
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'JobCard' 
AND COLUMN_NAME = 'ProblemDescription';

-- Step 2: If exists, try direct UPDATE
-- Replace 'YOUR-JOB-CARD-ID' with an actual job card ID from your database
DECLARE @JobCardId UNIQUEIDENTIFIER = 'ce642aee-678a-4982-a30e-b40feed4aeeb'; -- Use your actual ID

-- Show current value
SELECT 
    Id,
    JobCardNumber,
    Title,
    ProblemDescription
FROM JobCard
WHERE Id = @JobCardId;

-- Try direct update
UPDATE JobCard
SET ProblemDescription = 'TEST: This is a problem description updated via SQL'
WHERE Id = @JobCardId;

-- Show after update
SELECT 
    Id,
    JobCardNumber,
    Title,
    ProblemDescription
FROM JobCard
WHERE Id = @JobCardId;
