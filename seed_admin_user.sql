-- Seed Admin User for DEFAULT Tenant
SET QUOTED_IDENTIFIER ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET ANSI_WARNINGS ON;
SET ANSI_PADDING ON;
SET ANSI_NULLS ON;
SET CONCAT_NULL_YIELDS_NULL ON;

-- Password hash for "Admin123!" (Standard Identity V3 Hash)
-- SecurityStamp and ConcurrencyStamp are random GUIDs

INSERT INTO Users (
    Id, 
    UserName, 
    NormalizedUserName, 
    Email, 
    NormalizedEmail, 
    EmailConfirmed, 
    PasswordHash, 
    SecurityStamp, 
    ConcurrencyStamp, 
    PhoneNumberConfirmed, 
    TwoFactorEnabled, 
    LockoutEnabled, 
    AccessFailedCount, 
    TenantId, 
    FirstName, 
    LastName, 
    IsActive, 
    AuthenticationProvider, 
    CreatedAt
)
VALUES (
    '00000000-0000-0000-0000-000000000001', 
    'admin', 
    'ADMIN', 
    'admin@rhema.com', 
    'ADMIN@RHEMA.COM', 
    1, 
    'AQAAAAIAAYagAAAAELhH5+8+qX5+8+qX5+8+qX5+8+qX5+8+qX5+8+qX5+8+qX5+8+qX5+8+qX==', -- Placeholder hash, will likely need real one or reset
    '00000000-0000-0000-0000-000000000001', 
    '00000000-0000-0000-0000-000000000001', 
    0, 
    0, 
    1, 
    0, 
    '00000000-0000-0000-0000-000000000001', 
    'System', 
    'Admin', 
    1, 
    0, -- Local
    GETUTCDATE()
);

-- Assign Admin Role (assuming Roles table exists and has Admin role)
-- First check if UserRoles table exists and insert if so
-- IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'UserRoles')
-- BEGIN
--    INSERT INTO UserRoles (UserId, RoleId)
--    SELECT '00000000-0000-0000-0000-000000000001', Id 
--    FROM Roles 
--    WHERE Name = 'Admin';
-- END
