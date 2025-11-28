-- Update admin user's TenantId to match DEFAULT tenant
SET QUOTED_IDENTIFIER ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET ANSI_WARNINGS ON;
SET ANSI_PADDING ON;
SET ANSI_NULLS ON;
SET CONCAT_NULL_YIELDS_NULL ON;

-- Get the DEFAULT tenant ID
DECLARE @DefaultTenantId UNIQUEIDENTIFIER;
SELECT @DefaultTenantId = Id FROM Tenants WHERE Code = 'DEFAULT';

-- Update admin user's TenantId
UPDATE Users 
SET TenantId = @DefaultTenantId
WHERE UserName = 'admin';

-- Verify the update
SELECT 
    u.Id, 
    u.UserName, 
    u.Email, 
    u.TenantId,
    t.Code AS TenantCode,
    t.Name AS TenantName
FROM Users u
LEFT JOIN Tenants t ON u.TenantId = t.Id
WHERE u.UserName = 'admin';
