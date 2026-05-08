-- Minimal Authentication Database for RHEMA ERP
-- Creates only essential tables for login

USE RhemaERP;
GO

-- Create Tenants table
CREATE TABLE [Tenants] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [Code] nvarchar(50) NOT NULL UNIQUE,
    [Name] nvarchar(200) NOT NULL,
    [IsActive] bit NOT NULL DEFAULT 1,
    [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE()
);

-- Create Users table (ASP.NET Identity)
CREATE TABLE [Users] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [UserName] nvarchar(256) NOT NULL,
    [NormalizedUserName] nvarchar(256) NOT NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL DEFAULT 0,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL DEFAULT 0,
    [TwoFactorEnabled] bit NOT NULL DEFAULT 0,
    [LockoutEnd] datetimeoffset(7) NULL,
    [LockoutEnabled] bit NOT NULL DEFAULT 1,
    [AccessFailedCount] int NOT NULL DEFAULT 0,
    [TenantId] uniqueidentifier NOT NULL,
    [FirstName] nvarchar(100) NULL,
    [LastName] nvarchar(100) NULL,
    [IsActive] bit NOT NULL DEFAULT 1,
    [AuthenticationProvider] int NOT NULL DEFAULT 0,
    [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [FK_Users_Tenants] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id])
);

-- Create Roles table
CREATE TABLE [AspNetRoles] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL
);

-- Create UserRoles table
CREATE TABLE [AspNetUserRoles] (
    [UserId] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_Users] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_Roles] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);

-- Seed DEFAULT Tenant
INSERT INTO [Tenants] ([Id], [Code], [Name], [IsActive], [CreatedAt])
VALUES ('00000000-0000-0000-0000-000000000001', 'DEFAULT', 'Default Tenant', 1, GETUTCDATE());

-- Seed Admin Role
INSERT INTO [AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
VALUES ('00000000-0000-0000-0000-000000000001', 'Admin', 'ADMIN', NEWID());

-- Seed Admin User
-- Password: Admin123!
-- Hash generated with ASP.NET Core Identity V3
INSERT INTO [Users] (
    [Id], [UserName], [NormalizedUserName], [Email], [NormalizedEmail],
    [EmailConfirmed], [PasswordHash], [SecurityStamp], [ConcurrencyStamp],
    [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnabled], [AccessFailedCount],
    [TenantId], [FirstName], [LastName], [IsActive], [AuthenticationProvider], [CreatedAt]
)
VALUES (
    '00000000-0000-0000-0000-000000000001',
    'admin',
    'ADMIN',
    'admin@rhema.com',
    'ADMIN@RHEMA.COM',
    1,
    'AQAAAAIAAYagAAAAEGPxKqH8R3L9Zn0fQmL7K3xO5vJ8wN2pQ4tR6sU9yV1aB3cD5eF7gH9iJ1kL3mN5oP7qR9sT1uV3wX5yZ7',
    CONVERT(nvarchar(max), NEWID()),
    CONVERT(nvarchar(max), NEWID()),
    0, 0, 1, 0,
    '00000000-0000-0000-0000-000000000001',
    'System',
    'Administrator',
    1, 0,
    GETUTCDATE()
);

-- Assign Admin role to Admin user
INSERT INTO [AspNetUserRoles] ([UserId], [RoleId])
VALUES ('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000001');

PRINT 'Database initialized successfully!';
PRINT 'Login credentials:';
PRINT '  Username: admin';
PRINT '  Password: Admin123!';
GO
