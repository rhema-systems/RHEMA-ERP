-- Seed UserTenant for Admin User and DEFAULT Tenant
SET QUOTED_IDENTIFIER ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET ANSI_WARNINGS ON;
SET ANSI_PADDING ON;
SET ANSI_NULLS ON;
SET CONCAT_NULL_YIELDS_NULL ON;

INSERT INTO UserTenants (
    Id,
    UserId,
    TenantId,
    IsDefault,
    AccessLevel,
    CreatedAt,
    GrantedAt,
    IsDeleted
)
VALUES (
    NEWID(),
    '00000000-0000-0000-0000-000000000001', -- Admin User
    '00000000-0000-0000-0000-000000000001', -- DEFAULT Tenant
    1, -- IsDefault
    0, -- AccessLevel (assuming 0 is Admin/Full or similar enum value)
    GETUTCDATE(),
    GETUTCDATE(),
    0
);
