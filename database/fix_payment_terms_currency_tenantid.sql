-- Fix Payment Terms and Currencies TenantId
-- This script updates records that were saved with empty TenantId (00000000-0000-0000-0000-000000000000)
-- to use the correct TenantId from the Tenants table

-- First, let's see what tenants exist
SELECT Id, Name, Code FROM Tenants WHERE IsDeleted = 0;

-- Check current state of PaymentTerms
SELECT Id, Code, Name, TenantId FROM PaymentTerms WHERE IsDeleted = 0;

-- Check current state of Currencies
SELECT Id, Code, Name, TenantId FROM Currencies WHERE IsDeleted = 0;

-- IMPORTANT: Replace 'YOUR_TENANT_ID_HERE' with the actual TenantId from the Tenants table
-- You can get this from the first query above

-- Update PaymentTerms with empty TenantId
-- UPDATE PaymentTerms 
-- SET TenantId = 'YOUR_TENANT_ID_HERE'
-- WHERE TenantId = '00000000-0000-0000-0000-000000000000' AND IsDeleted = 0;

-- Update Currencies with empty TenantId
-- UPDATE Currencies 
-- SET TenantId = 'YOUR_TENANT_ID_HERE'
-- WHERE TenantId = '00000000-0000-0000-0000-000000000000' AND IsDeleted = 0;

-- Alternative: Update to use the first active tenant (if you only have one tenant)
-- Uncomment and run these if you want to automatically use the first tenant

-- For SQL Server:
DECLARE @TenantId UNIQUEIDENTIFIER;
SELECT TOP 1 @TenantId = Id FROM Tenants WHERE IsDeleted = 0 ORDER BY CreatedAt;

IF @TenantId IS NOT NULL
BEGIN
    PRINT 'Updating PaymentTerms with TenantId: ' + CAST(@TenantId AS NVARCHAR(50));
    
    UPDATE PaymentTerms 
    SET TenantId = @TenantId
    WHERE TenantId = '00000000-0000-0000-0000-000000000000' AND IsDeleted = 0;
    
    PRINT 'Updated ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' PaymentTerms records';
    
    UPDATE Currencies 
    SET TenantId = @TenantId
    WHERE TenantId = '00000000-0000-0000-0000-000000000000' AND IsDeleted = 0;
    
    PRINT 'Updated ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' Currencies records';
END
ELSE
BEGIN
    PRINT 'No active tenant found!';
END

-- Verify the updates
SELECT Id, Code, Name, TenantId FROM PaymentTerms WHERE IsDeleted = 0;
SELECT Id, Code, Name, TenantId FROM Currencies WHERE IsDeleted = 0;
