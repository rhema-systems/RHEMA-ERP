IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [Roles] (
    [Id] uniqueidentifier NOT NULL,
    [Description] nvarchar(500) NULL,
    [IsSystemRole] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Tenants] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Description] nvarchar(500) NULL,
    [Status] int NOT NULL,
    [Domain] nvarchar(200) NULL,
    [LogoUrl] nvarchar(500) NULL,
    [ContactEmail] nvarchar(200) NULL,
    [ContactPhone] nvarchar(50) NULL,
    [Address] nvarchar(500) NULL,
    [SubscriptionStartDate] datetime2 NULL,
    [SubscriptionEndDate] datetime2 NULL,
    [LdapServer] nvarchar(max) NULL,
    [LdapPort] int NULL,
    [LdapBaseDn] nvarchar(max) NULL,
    [LdapBindDn] nvarchar(max) NULL,
    [LdapBindPassword] nvarchar(max) NULL,
    [LdapEnabled] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] uniqueidentifier NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [TenantModules] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [ModuleName] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [Status] int NOT NULL,
    [EnabledDate] datetime2 NULL,
    [DisabledDate] datetime2 NULL,
    [Configuration] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_TenantModules] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TenantModules_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Users] (
    [Id] uniqueidentifier NOT NULL,
    [FirstName] nvarchar(100) NOT NULL,
    [LastName] nvarchar(100) NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [AuthenticationProvider] int NOT NULL,
    [LdapDn] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [LastLoginDate] datetime2 NULL,
    [ProfilePictureUrl] nvarchar(max) NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Users_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] uniqueidentifier NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserTokens] (
    [UserId] uniqueidentifier NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [UserRoles] (
    [UserId] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'CreatedAt', N'CreatedBy', N'Description', N'IsSystemRole', N'Name', N'NormalizedName') AND [object_id] = OBJECT_ID(N'[Roles]'))
    SET IDENTITY_INSERT [Roles] ON;
INSERT INTO [Roles] ([Id], [ConcurrencyStamp], [CreatedAt], [CreatedBy], [Description], [IsSystemRole], [Name], [NormalizedName])
VALUES ('00000000-0000-0000-0000-000000000001', NULL, '2025-09-17T20:42:06.9669224Z', NULL, NULL, CAST(1 AS bit), N'SuperAdmin', N'SUPERADMIN'),
('00000000-0000-0000-0000-000000000002', NULL, '2025-09-17T20:42:06.9669267Z', NULL, NULL, CAST(1 AS bit), N'TenantAdmin', N'TENANTADMIN'),
('00000000-0000-0000-0000-000000000003', NULL, '2025-09-17T20:42:06.9669270Z', NULL, NULL, CAST(1 AS bit), N'Manager', N'MANAGER'),
('00000000-0000-0000-0000-000000000004', NULL, '2025-09-17T20:42:06.9669272Z', NULL, NULL, CAST(1 AS bit), N'Employee', N'EMPLOYEE');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'CreatedAt', N'CreatedBy', N'Description', N'IsSystemRole', N'Name', N'NormalizedName') AND [object_id] = OBJECT_ID(N'[Roles]'))
    SET IDENTITY_INSERT [Roles] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Address', N'Code', N'ContactEmail', N'ContactPhone', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'Domain', N'IsDeleted', N'LdapBaseDn', N'LdapBindDn', N'LdapBindPassword', N'LdapEnabled', N'LdapPort', N'LdapServer', N'LogoUrl', N'Name', N'Status', N'SubscriptionEndDate', N'SubscriptionStartDate', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Tenants]'))
    SET IDENTITY_INSERT [Tenants] ON;
INSERT INTO [Tenants] ([Id], [Address], [Code], [ContactEmail], [ContactPhone], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [Domain], [IsDeleted], [LdapBaseDn], [LdapBindDn], [LdapBindPassword], [LdapEnabled], [LdapPort], [LdapServer], [LogoUrl], [Name], [Status], [SubscriptionEndDate], [SubscriptionStartDate], [UpdatedAt], [UpdatedBy])
VALUES ('00000000-0000-0000-0000-000000000001', NULL, N'DEFAULT', NULL, NULL, '2025-09-17T20:42:06.9669069Z', NULL, NULL, NULL, N'Default system tenant', NULL, CAST(0 AS bit), NULL, NULL, NULL, CAST(0 AS bit), NULL, NULL, NULL, N'Default Tenant', 1, NULL, NULL, NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Address', N'Code', N'ContactEmail', N'ContactPhone', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'Domain', N'IsDeleted', N'LdapBaseDn', N'LdapBindDn', N'LdapBindPassword', N'LdapEnabled', N'LdapPort', N'LdapServer', N'LogoUrl', N'Name', N'Status', N'SubscriptionEndDate', N'SubscriptionStartDate', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Tenants]'))
    SET IDENTITY_INSERT [Tenants] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('0b0e4d1d-ad38-47bf-b283-60654f64bd01', NULL, '2025-09-17T20:42:06.9669350Z', NULL, NULL, NULL, NULL, NULL, '2025-09-17T20:42:06.9669349Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('22cc6d74-6998-49b1-bfb1-e38a5620b591', NULL, '2025-09-17T20:42:06.9669334Z', NULL, NULL, NULL, NULL, NULL, '2025-09-17T20:42:06.9669330Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('7d65cccf-7a63-439f-b893-2103d811bb0f', NULL, '2025-09-17T20:42:06.9669404Z', NULL, NULL, NULL, NULL, NULL, '2025-09-17T20:42:06.9669403Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('891adcc2-b5ab-44f3-8604-999ae10791c9', NULL, '2025-09-17T20:42:06.9669476Z', NULL, NULL, NULL, NULL, NULL, '2025-09-17T20:42:06.9669476Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('cd1f5db3-26cb-4b61-ae05-ac9e4ae05479', NULL, '2025-09-17T20:42:06.9669364Z', NULL, NULL, NULL, NULL, NULL, '2025-09-17T20:42:06.9669363Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('d8db69d5-988e-44db-b3bd-d539edd394cc', NULL, '2025-09-17T20:42:06.9669389Z', NULL, NULL, NULL, NULL, NULL, '2025-09-17T20:42:06.9669389Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('f96c8ce4-308d-440f-b2e6-f3a59379c027', NULL, '2025-09-17T20:42:06.9669376Z', NULL, NULL, NULL, NULL, NULL, '2025-09-17T20:42:06.9669375Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
GO

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
GO

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
GO

CREATE UNIQUE INDEX [RoleNameIndex] ON [Roles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
GO

CREATE UNIQUE INDEX [IX_TenantModules_TenantId_ModuleName] ON [TenantModules] ([TenantId], [ModuleName]);
GO

CREATE UNIQUE INDEX [IX_Tenants_Code] ON [Tenants] ([Code]);
GO

CREATE UNIQUE INDEX [IX_Tenants_Name] ON [Tenants] ([Name]);
GO

CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
GO

CREATE INDEX [EmailIndex] ON [Users] ([NormalizedEmail]);
GO

CREATE UNIQUE INDEX [IX_Users_TenantId_UserName] ON [Users] ([TenantId], [UserName]) WHERE [UserName] IS NOT NULL;
GO

CREATE UNIQUE INDEX [UserNameIndex] ON [Users] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250917204207_InitialCreate', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '0b0e4d1d-ad38-47bf-b283-60654f64bd01';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '22cc6d74-6998-49b1-bfb1-e38a5620b591';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '7d65cccf-7a63-439f-b893-2103d811bb0f';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '891adcc2-b5ab-44f3-8604-999ae10791c9';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'cd1f5db3-26cb-4b61-ae05-ac9e4ae05479';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'd8db69d5-988e-44db-b3bd-d539edd394cc';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'f96c8ce4-308d-440f-b2e6-f3a59379c027';
SELECT @@ROWCOUNT;

GO

ALTER TABLE [Roles] ADD [UpdatedAt] datetime2 NULL;
GO

ALTER TABLE [Roles] ADD [UpdatedBy] nvarchar(max) NULL;
GO

CREATE TABLE [AuditLogs] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Username] nvarchar(255) NOT NULL,
    [Action] nvarchar(100) NOT NULL,
    [Resource] nvarchar(255) NOT NULL,
    [ResourceId] nvarchar(100) NULL,
    [OldValues] nvarchar(max) NULL,
    [NewValues] nvarchar(max) NULL,
    [IpAddress] nvarchar(45) NOT NULL,
    [UserAgent] nvarchar(500) NULL,
    [Timestamp] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AuditLogs_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AuditLogs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [EmailSettings] (
    [Id] uniqueidentifier NOT NULL,
    [SmtpHost] nvarchar(255) NOT NULL,
    [SmtpPort] int NOT NULL,
    [SmtpUsername] nvarchar(255) NOT NULL,
    [SmtpPassword] nvarchar(255) NOT NULL,
    [UseTLS] bit NOT NULL,
    [FromAddress] nvarchar(255) NOT NULL,
    [FromName] nvarchar(255) NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_EmailSettings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmailSettings_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [PasswordPolicies] (
    [Id] uniqueidentifier NOT NULL,
    [MinLength] int NOT NULL,
    [RequireUppercase] bit NOT NULL,
    [RequireLowercase] bit NOT NULL,
    [RequireDigits] bit NOT NULL,
    [RequireSpecialChars] bit NOT NULL,
    [MaxAge] int NULL,
    [PreventReuse] int NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_PasswordPolicies] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PasswordPolicies_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [SecurityLogs] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NULL,
    [Username] nvarchar(255) NULL,
    [Action] nvarchar(100) NOT NULL,
    [IpAddress] nvarchar(45) NOT NULL,
    [UserAgent] nvarchar(500) NULL,
    [Success] bit NOT NULL,
    [Details] nvarchar(1000) NULL,
    [FailureReason] nvarchar(255) NULL,
    [Timestamp] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_SecurityLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SecurityLogs_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SecurityLogs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [SystemSettings] (
    [Id] uniqueidentifier NOT NULL,
    [Key] nvarchar(100) NOT NULL,
    [Value] nvarchar(max) NOT NULL,
    [Description] nvarchar(500) NULL,
    [IsEncrypted] bit NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_SystemSettings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SystemSettings_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
);
GO

UPDATE [Roles] SET [CreatedAt] = '2025-09-18T17:13:30.7722251Z', [UpdatedAt] = NULL, [UpdatedBy] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Roles] SET [CreatedAt] = '2025-09-18T17:13:30.7722301Z', [UpdatedAt] = NULL, [UpdatedBy] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Roles] SET [CreatedAt] = '2025-09-18T17:13:30.7722386Z', [UpdatedAt] = NULL, [UpdatedBy] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Roles] SET [CreatedAt] = '2025-09-18T17:13:30.7722389Z', [UpdatedAt] = NULL, [UpdatedBy] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('20998378-e35d-4997-b875-f58a88e04a9b', NULL, '2025-09-18T17:13:30.7722513Z', NULL, NULL, NULL, NULL, NULL, '2025-09-18T17:13:30.7722512Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('569ad24e-ebe5-4db9-af4f-7953e5b9ced2', NULL, '2025-09-18T17:13:30.7722486Z', NULL, NULL, NULL, NULL, NULL, '2025-09-18T17:13:30.7722485Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('5e175eaa-fb74-4dba-8018-c70143ec4cea', NULL, '2025-09-18T17:13:30.7722527Z', NULL, NULL, NULL, NULL, NULL, '2025-09-18T17:13:30.7722527Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('5f01e2f4-9d65-4766-bf69-107b0a405d67', NULL, '2025-09-18T17:13:30.7722500Z', NULL, NULL, NULL, NULL, NULL, '2025-09-18T17:13:30.7722500Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('5ffb814f-b4e7-4f6a-b6d5-79015f2ffff2', NULL, '2025-09-18T17:13:30.7722453Z', NULL, NULL, NULL, NULL, NULL, '2025-09-18T17:13:30.7722449Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('ac4bff78-09e3-451c-9999-00f2c45aef65', NULL, '2025-09-18T17:13:30.7722540Z', NULL, NULL, NULL, NULL, NULL, '2025-09-18T17:13:30.7722539Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('ba99b2b2-fe9c-4d0b-9ec8-cc9a19c09c72', NULL, '2025-09-18T17:13:30.7722470Z', NULL, NULL, NULL, NULL, NULL, '2025-09-18T17:13:30.7722469Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-09-18T17:13:30.7722057Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_AuditLogs_Resource] ON [AuditLogs] ([Resource]);
GO

CREATE INDEX [IX_AuditLogs_TenantId] ON [AuditLogs] ([TenantId]);
GO

CREATE INDEX [IX_AuditLogs_Timestamp] ON [AuditLogs] ([Timestamp]);
GO

CREATE INDEX [IX_AuditLogs_UserId] ON [AuditLogs] ([UserId]);
GO

CREATE INDEX [IX_EmailSettings_TenantId] ON [EmailSettings] ([TenantId]);
GO

CREATE INDEX [IX_PasswordPolicies_TenantId] ON [PasswordPolicies] ([TenantId]);
GO

CREATE INDEX [IX_SecurityLogs_Action] ON [SecurityLogs] ([Action]);
GO

CREATE INDEX [IX_SecurityLogs_IpAddress] ON [SecurityLogs] ([IpAddress]);
GO

CREATE INDEX [IX_SecurityLogs_TenantId] ON [SecurityLogs] ([TenantId]);
GO

CREATE INDEX [IX_SecurityLogs_Timestamp] ON [SecurityLogs] ([Timestamp]);
GO

CREATE INDEX [IX_SecurityLogs_UserId] ON [SecurityLogs] ([UserId]);
GO

CREATE UNIQUE INDEX [IX_SystemSettings_TenantId_Key] ON [SystemSettings] ([TenantId], [Key]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250918171331_AddSettingsAndLogEntities', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [Users] DROP CONSTRAINT [FK_Users_Tenants_TenantId];
GO

DROP INDEX [IX_Users_TenantId_UserName] ON [Users];
GO

DELETE FROM [TenantModules]
WHERE [Id] = '20998378-e35d-4997-b875-f58a88e04a9b';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '569ad24e-ebe5-4db9-af4f-7953e5b9ced2';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '5e175eaa-fb74-4dba-8018-c70143ec4cea';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '5f01e2f4-9d65-4766-bf69-107b0a405d67';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '5ffb814f-b4e7-4f6a-b6d5-79015f2ffff2';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'ac4bff78-09e3-451c-9999-00f2c45aef65';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'ba99b2b2-fe9c-4d0b-9ec8-cc9a19c09c72';
SELECT @@ROWCOUNT;

GO

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'TenantId');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [Users] DROP COLUMN [TenantId];
GO

CREATE TABLE [ApplicationUserTenant] (
    [AccessibleTenantsId] uniqueidentifier NOT NULL,
    [UsersId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ApplicationUserTenant] PRIMARY KEY ([AccessibleTenantsId], [UsersId]),
    CONSTRAINT [FK_ApplicationUserTenant_Tenants_AccessibleTenantsId] FOREIGN KEY ([AccessibleTenantsId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ApplicationUserTenant_Users_UsersId] FOREIGN KEY ([UsersId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [UserTenants] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [AccessLevel] int NOT NULL,
    [IsDefault] bit NOT NULL,
    [GrantedAt] datetime2 NOT NULL,
    [ExpiresAt] datetime2 NULL,
    [GrantedBy] nvarchar(max) NULL,
    [Notes] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_UserTenants] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserTenants_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserTenants_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

UPDATE [Roles] SET [CreatedAt] = '2025-09-19T20:14:55.9838206Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Roles] SET [CreatedAt] = '2025-09-19T20:14:55.9838242Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Roles] SET [CreatedAt] = '2025-09-19T20:14:55.9838244Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Roles] SET [CreatedAt] = '2025-09-19T20:14:55.9838246Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('0e337286-7ab4-4c72-a560-97ec2b5c6c0b', NULL, '2025-09-19T20:14:55.9838317Z', NULL, NULL, NULL, NULL, NULL, '2025-09-19T20:14:55.9838317Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('4dc88d64-defd-4318-99a1-e761d8eacdbe', NULL, '2025-09-19T20:14:55.9838300Z', NULL, NULL, NULL, NULL, NULL, '2025-09-19T20:14:55.9838297Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('52899860-3157-41ba-b39a-6eb144d215bd', NULL, '2025-09-19T20:14:55.9838366Z', NULL, NULL, NULL, NULL, NULL, '2025-09-19T20:14:55.9838366Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('7be66c35-082a-4a42-bb94-6365b1fcb4d9', NULL, '2025-09-19T20:14:55.9838378Z', NULL, NULL, NULL, NULL, NULL, '2025-09-19T20:14:55.9838378Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('8b9f8059-d463-4f01-8b7e-8cc80112aea2', NULL, '2025-09-19T20:14:55.9838341Z', NULL, NULL, NULL, NULL, NULL, '2025-09-19T20:14:55.9838341Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('a140e87e-8615-40b7-a8f6-c894294c5c68', NULL, '2025-09-19T20:14:55.9838354Z', NULL, NULL, NULL, NULL, NULL, '2025-09-19T20:14:55.9838353Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('d6acc58b-1e8f-44f4-b44b-64dceaa7f1e9', NULL, '2025-09-19T20:14:55.9838329Z', NULL, NULL, NULL, NULL, NULL, '2025-09-19T20:14:55.9838329Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-09-19T20:14:55.9838053Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

CREATE UNIQUE INDEX [IX_Users_UserName] ON [Users] ([UserName]) WHERE [UserName] IS NOT NULL;
GO

CREATE INDEX [IX_ApplicationUserTenant_UsersId] ON [ApplicationUserTenant] ([UsersId]);
GO

CREATE INDEX [IX_UserTenants_IsDefault] ON [UserTenants] ([IsDefault]);
GO

CREATE INDEX [IX_UserTenants_TenantId] ON [UserTenants] ([TenantId]);
GO

CREATE INDEX [IX_UserTenants_UserId] ON [UserTenants] ([UserId]);
GO

CREATE INDEX [IX_UserTenants_UserId_IsDefault] ON [UserTenants] ([UserId], [IsDefault]);
GO

CREATE UNIQUE INDEX [IX_UserTenants_UserId_TenantId] ON [UserTenants] ([UserId], [TenantId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250919201456_UpdateUserTenantRelationship', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

UPDATE [Roles] SET [CreatedAt] = '2025-09-22T01:58:58.4761680Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Roles] SET [CreatedAt] = '2025-09-22T01:58:58.4761746Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Roles] SET [CreatedAt] = '2025-09-22T01:58:58.4761751Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Roles] SET [CreatedAt] = '2025-09-22T01:58:58.4761756Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Tenants] SET [CreatedAt] = '2025-09-22T01:58:58.4761275Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250922015859_AddPreventConcurrentLoginToSecurity', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

UPDATE [Roles] SET [CreatedAt] = '2025-09-22T02:07:25.8824292Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Roles] SET [CreatedAt] = '2025-09-22T02:07:25.8824330Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Roles] SET [CreatedAt] = '2025-09-22T02:07:25.8824332Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Roles] SET [CreatedAt] = '2025-09-22T02:07:25.8824335Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Tenants] SET [CreatedAt] = '2025-09-22T02:07:25.8824084Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250922020726_UpdatePreventConcurrentLoginEnumValues', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [AspNetRoleClaims] DROP CONSTRAINT [FK_AspNetRoleClaims_Roles_RoleId];
GO

ALTER TABLE [UserRoles] DROP CONSTRAINT [FK_UserRoles_Roles_RoleId];
GO

ALTER TABLE [UserTenants] DROP CONSTRAINT [FK_UserTenants_Tenants_TenantId];
GO

ALTER TABLE [UserTenants] DROP CONSTRAINT [FK_UserTenants_Users_UserId];
GO

DROP TABLE [ApplicationUserTenant];
GO

DROP INDEX [IX_UserTenants_TenantId] ON [UserTenants];
GO

DROP INDEX [IX_UserTenants_UserId] ON [UserTenants];
GO

DROP INDEX [IX_UserTenants_UserId_IsDefault] ON [UserTenants];
GO

DROP INDEX [IX_Tenants_Name] ON [Tenants];
GO

ALTER TABLE [Roles] DROP CONSTRAINT [PK_Roles];
GO

DELETE FROM [TenantModules]
WHERE [Id] = '0e337286-7ab4-4c72-a560-97ec2b5c6c0b';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '4dc88d64-defd-4318-99a1-e761d8eacdbe';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '52899860-3157-41ba-b39a-6eb144d215bd';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '7be66c35-082a-4a42-bb94-6365b1fcb4d9';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '8b9f8059-d463-4f01-8b7e-8cc80112aea2';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'a140e87e-8615-40b7-a8f6-c894294c5c68';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'd6acc58b-1e8f-44f4-b44b-64dceaa7f1e9';
SELECT @@ROWCOUNT;

GO

EXEC sp_rename N'[Roles]', N'AspNetRoles';
GO

ALTER TABLE [UserTenants] ADD [ReactivatedAt] datetime2 NULL;
GO

ALTER TABLE [UserTenants] ADD [Status] int NOT NULL DEFAULT 0;
GO

ALTER TABLE [UserTenants] ADD [StatusChangedBy] nvarchar(max) NULL;
GO

ALTER TABLE [UserTenants] ADD [SuspendedAt] datetime2 NULL;
GO

ALTER TABLE [Users] ADD [TenantId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
GO

ALTER TABLE [Users] ADD [TenantId1] uniqueidentifier NULL;
GO

ALTER TABLE [Tenants] ADD [AllowSelfRegistration] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [Tenants] ADD [DefaultPriority] int NOT NULL DEFAULT 0;
GO

ALTER TABLE [Tenants] ADD [EnableAutoSelection] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [Tenants] ADD [IsDefaultForInternalUsers] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [Tenants] ADD [IsDefaultForPublicUsers] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [Tenants] ADD [PublicRegistrationDomains] nvarchar(max) NULL;
GO

ALTER TABLE [Tenants] ADD [RequireEmailVerification] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [Tenants] ADD [UserAudience] int NOT NULL DEFAULT 0;
GO

ALTER TABLE [Tenants] ADD [WelcomeMessage] nvarchar(max) NULL;
GO

ALTER TABLE [AspNetRoles] ADD CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id]);
GO

CREATE TABLE [Securities] (
    [Id] uniqueidentifier NOT NULL,
    [PasswordMinLength] int NOT NULL,
    [PasswordRequireUppercase] bit NOT NULL,
    [PasswordRequireLowercase] bit NOT NULL,
    [PasswordRequireDigits] bit NOT NULL,
    [PasswordRequireSpecialChars] bit NOT NULL,
    [PasswordMaxAge] int NULL,
    [PasswordPreventReuse] int NULL,
    [CaptchaEnabled] bit NOT NULL,
    [CaptchaProvider] nvarchar(20) NOT NULL,
    [RecaptchaSiteKey] nvarchar(255) NULL,
    [RecaptchaSecretKey] nvarchar(255) NULL,
    [HCaptchaSiteKey] nvarchar(255) NULL,
    [HCaptchaSecretKey] nvarchar(255) NULL,
    [RateLimitLoginMaxAttempts] int NOT NULL,
    [RateLimitLoginWindowMinutes] int NOT NULL,
    [RateLimitLoginBlockDurationMinutes] int NOT NULL,
    [RateLimitRegisterMaxAttempts] int NOT NULL,
    [RateLimitRegisterWindowMinutes] int NOT NULL,
    [RateLimitRegisterBlockDurationMinutes] int NOT NULL,
    [RateLimitForgotPasswordMaxAttempts] int NOT NULL,
    [RateLimitForgotPasswordWindowMinutes] int NOT NULL,
    [RateLimitForgotPasswordBlockDurationMinutes] int NOT NULL,
    [SessionTimeoutMinutes] int NOT NULL,
    [JwtTokenLifetimeMinutes] int NOT NULL,
    [MaxFailedLoginAttempts] int NOT NULL,
    [AccountLockoutMinutes] int NOT NULL,
    [PreventConcurrentLogin] int NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_Securities] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Securities_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
);
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-23T12:51:42.0232125Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-23T12:51:42.0232165Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-23T12:51:42.0232168Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-23T12:51:42.0232170Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('5b3f921d-2398-4f29-823e-a3b2e8599967', NULL, '2025-09-23T12:51:42.0232305Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T12:51:42.0232304Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('89bf11b7-396a-4ad4-8a1b-edf0415b92db', NULL, '2025-09-23T12:51:42.0232265Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T12:51:42.0232264Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('c849318a-78b5-4352-8cc0-e54051706710', NULL, '2025-09-23T12:51:42.0232249Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T12:51:42.0232248Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('c8b67e30-c868-4102-bf56-9d8b0656769d', NULL, '2025-09-23T12:51:42.0232316Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T12:51:42.0232316Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('cd04d659-03db-43fe-bdfd-67d44972bca3', NULL, '2025-09-23T12:51:42.0232232Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T12:51:42.0232228Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('ea3115bb-a58d-4c0d-92f3-1eebaf5280bd', NULL, '2025-09-23T12:51:42.0232278Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T12:51:42.0232278Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('fdfcf06f-57df-47d2-abae-83d44d1b9a97', NULL, '2025-09-23T12:51:42.0232291Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T12:51:42.0232291Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [AllowSelfRegistration] = CAST(0 AS bit), [CreatedAt] = '2025-09-23T12:51:42.0231929Z', [DefaultPriority] = 10, [EnableAutoSelection] = CAST(0 AS bit), [IsDefaultForInternalUsers] = CAST(0 AS bit), [IsDefaultForPublicUsers] = CAST(0 AS bit), [PublicRegistrationDomains] = NULL, [RequireEmailVerification] = CAST(0 AS bit), [UserAudience] = 2, [WelcomeMessage] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_UserTenants_TenantId_Status] ON [UserTenants] ([TenantId], [Status]);
GO

CREATE INDEX [IX_Users_TenantId] ON [Users] ([TenantId]);
GO

CREATE INDEX [IX_Users_TenantId1] ON [Users] ([TenantId1]);
GO

CREATE UNIQUE INDEX [IX_Tenants_Domain] ON [Tenants] ([Domain]) WHERE [Domain] IS NOT NULL;
GO

CREATE INDEX [IX_Securities_TenantId] ON [Securities] ([TenantId]);
GO

ALTER TABLE [AspNetRoleClaims] ADD CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [UserRoles] ADD CONSTRAINT [FK_UserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [Users] ADD CONSTRAINT [FK_Users_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [Users] ADD CONSTRAINT [FK_Users_Tenants_TenantId1] FOREIGN KEY ([TenantId1]) REFERENCES [Tenants] ([Id]);
GO

ALTER TABLE [UserTenants] ADD CONSTRAINT [FK_UserTenants_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [UserTenants] ADD CONSTRAINT [FK_UserTenants_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250923125142_CreateSecurityTable', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '5b3f921d-2398-4f29-823e-a3b2e8599967';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '89bf11b7-396a-4ad4-8a1b-edf0415b92db';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'c849318a-78b5-4352-8cc0-e54051706710';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'c8b67e30-c868-4102-bf56-9d8b0656769d';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'cd04d659-03db-43fe-bdfd-67d44972bca3';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'ea3115bb-a58d-4c0d-92f3-1eebaf5280bd';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'fdfcf06f-57df-47d2-abae-83d44d1b9a97';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-23T16:10:06.2290477Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-23T16:10:06.2290546Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-23T16:10:06.2290549Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-23T16:10:06.2290552Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('1da24716-fd56-44b0-8d2c-b4e7d6850c9b', NULL, '2025-09-23T16:10:06.2290671Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T16:10:06.2290671Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('4643d21c-6433-49b7-9026-66c7039d7351', NULL, '2025-09-23T16:10:06.2290854Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T16:10:06.2290854Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('a7673475-7689-4253-81ec-62894c352cb9', NULL, '2025-09-23T16:10:06.2290649Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T16:10:06.2290644Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('ac1534db-0b3a-4f1a-8da4-600e8d3609ae', NULL, '2025-09-23T16:10:06.2290874Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T16:10:06.2290873Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('acdad383-4d21-4f5b-9fc3-2e16220d7102', NULL, '2025-09-23T16:10:06.2290710Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T16:10:06.2290709Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('c5f28f1e-92ea-43c7-883c-03ed0485b45b', NULL, '2025-09-23T16:10:06.2290689Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T16:10:06.2290689Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('df39c1d7-5c64-415d-9485-fe50d7c4f821', NULL, '2025-09-23T16:10:06.2290891Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T16:10:06.2290891Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Address', N'AllowSelfRegistration', N'Code', N'ContactEmail', N'ContactPhone', N'CreatedAt', N'CreatedBy', N'DefaultPriority', N'DeletedAt', N'DeletedBy', N'Description', N'Domain', N'EnableAutoSelection', N'IsDefaultForInternalUsers', N'IsDefaultForPublicUsers', N'IsDeleted', N'LdapBaseDn', N'LdapBindDn', N'LdapBindPassword', N'LdapEnabled', N'LdapPort', N'LdapServer', N'LogoUrl', N'Name', N'PublicRegistrationDomains', N'RequireEmailVerification', N'Status', N'SubscriptionEndDate', N'SubscriptionStartDate', N'UpdatedAt', N'UpdatedBy', N'UserAudience', N'WelcomeMessage') AND [object_id] = OBJECT_ID(N'[Tenants]'))
    SET IDENTITY_INSERT [Tenants] ON;
INSERT INTO [Tenants] ([Id], [Address], [AllowSelfRegistration], [Code], [ContactEmail], [ContactPhone], [CreatedAt], [CreatedBy], [DefaultPriority], [DeletedAt], [DeletedBy], [Description], [Domain], [EnableAutoSelection], [IsDefaultForInternalUsers], [IsDefaultForPublicUsers], [IsDeleted], [LdapBaseDn], [LdapBindDn], [LdapBindPassword], [LdapEnabled], [LdapPort], [LdapServer], [LogoUrl], [Name], [PublicRegistrationDomains], [RequireEmailVerification], [Status], [SubscriptionEndDate], [SubscriptionStartDate], [UpdatedAt], [UpdatedBy], [UserAudience], [WelcomeMessage])
VALUES ('00000000-0000-0000-0000-000000000002', N'456 Acme Ave, Business City, BC 67890', CAST(0 AS bit), N'ACME', N'admin@acme.com', N'+1-555-0200', '2025-09-23T16:10:06.2290207Z', N'System', 10, NULL, NULL, N'Acme Corporation - Multi-division enterprise', N'acme.com', CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, NULL, CAST(0 AS bit), NULL, NULL, NULL, N'Acme Corporation', NULL, CAST(0 AS bit), 1, '2026-09-23T16:10:06.2290207Z', '2025-09-23T16:10:06.2290207Z', NULL, NULL, 2, NULL),
('00000000-0000-0000-0000-000000000003', N'789 Tech Blvd, Innovation City, IC 12345', CAST(0 AS bit), N'TECHSTART', N'admin@techstart.com', N'+1-555-0300', '2025-09-23T16:10:06.2290207Z', N'System', 10, NULL, NULL, N'TechStart Inc - Growing technology startup', N'techstart.com', CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, NULL, CAST(0 AS bit), NULL, NULL, NULL, N'TechStart Inc', NULL, CAST(0 AS bit), 1, '2026-09-23T16:10:06.2290207Z', '2025-09-23T16:10:06.2290207Z', NULL, NULL, 2, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Address', N'AllowSelfRegistration', N'Code', N'ContactEmail', N'ContactPhone', N'CreatedAt', N'CreatedBy', N'DefaultPriority', N'DeletedAt', N'DeletedBy', N'Description', N'Domain', N'EnableAutoSelection', N'IsDefaultForInternalUsers', N'IsDefaultForPublicUsers', N'IsDeleted', N'LdapBaseDn', N'LdapBindDn', N'LdapBindPassword', N'LdapEnabled', N'LdapPort', N'LdapServer', N'LogoUrl', N'Name', N'PublicRegistrationDomains', N'RequireEmailVerification', N'Status', N'SubscriptionEndDate', N'SubscriptionStartDate', N'UpdatedAt', N'UpdatedBy', N'UserAudience', N'WelcomeMessage') AND [object_id] = OBJECT_ID(N'[Tenants]'))
    SET IDENTITY_INSERT [Tenants] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('10000000-0000-0000-0000-000000000001', NULL, '2025-09-23T16:10:06.2290644Z', N'System', NULL, NULL, NULL, NULL, '2025-09-23T16:10:06.2290644Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000002', NULL, NULL),
('10000000-0000-0000-0000-000000000002', NULL, '2025-09-23T16:10:06.2290671Z', N'System', NULL, NULL, NULL, NULL, '2025-09-23T16:10:06.2290671Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000002', NULL, NULL),
('10000000-0000-0000-0000-000000000003', NULL, '2025-09-23T16:10:06.2290689Z', N'System', NULL, NULL, NULL, NULL, '2025-09-23T16:10:06.2290689Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000002', NULL, NULL),
('10000000-0000-0000-0000-000000000004', NULL, '2025-09-23T16:10:06.2290709Z', N'System', NULL, NULL, NULL, NULL, '2025-09-23T16:10:06.2290709Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000002', NULL, NULL),
('10000000-0000-0000-0000-000000000005', NULL, '2025-09-23T16:10:06.2290854Z', N'System', NULL, NULL, NULL, NULL, '2025-09-23T16:10:06.2290854Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000002', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('20000000-0000-0000-0000-000000000001', NULL, '2025-09-23T16:10:06.2290644Z', N'System', NULL, NULL, NULL, NULL, '2025-09-23T16:10:06.2290644Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000003', NULL, NULL),
('20000000-0000-0000-0000-000000000002', NULL, '2025-09-23T16:10:06.2290689Z', N'System', NULL, NULL, NULL, NULL, '2025-09-23T16:10:06.2290689Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000003', NULL, NULL),
('20000000-0000-0000-0000-000000000003', NULL, '2025-09-23T16:10:06.2290874Z', N'System', NULL, NULL, NULL, NULL, '2025-09-23T16:10:06.2290873Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000003', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-09-23T16:10:06.2290207Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

                -- Insert UserTenant mappings for admin user to all tenants
                DECLARE @AdminUserId UNIQUEIDENTIFIER;
                SELECT @AdminUserId = Id FROM Users WHERE UserName = 'admin';
                IF @AdminUserId IS NOT NULL
                BEGIN
                    -- Grant admin access to DEFAULT tenant (set as default)
                    IF NOT EXISTS (SELECT 1 FROM UserTenants WHERE UserId = @AdminUserId AND TenantId = '00000000-0000-0000-0000-000000000001')
                    BEGIN
                        INSERT INTO UserTenants (Id, UserId, TenantId, AccessLevel, Status, IsDefault, GrantedAt, GrantedBy, CreatedAt, CreatedBy, IsDeleted)
                        VALUES (NEWID(), @AdminUserId, '00000000-0000-0000-0000-000000000001', 2, 0, 1, GETUTCDATE(), 'System', GETUTCDATE(), 'System', 0);
                    END
                    -- Grant admin access to ACME tenant
                    IF NOT EXISTS (SELECT 1 FROM UserTenants WHERE UserId = @AdminUserId AND TenantId = '00000000-0000-0000-0000-000000000002')
                    BEGIN
                        INSERT INTO UserTenants (Id, UserId, TenantId, AccessLevel, Status, IsDefault, GrantedAt, GrantedBy, CreatedAt, CreatedBy, IsDeleted)
                        VALUES (NEWID(), @AdminUserId, '00000000-0000-0000-0000-000000000002', 2, 0, 0, GETUTCDATE(), 'System', GETUTCDATE(), 'System', 0);
                    END
                    -- Grant admin access to TECHSTART tenant
                    IF NOT EXISTS (SELECT 1 FROM UserTenants WHERE UserId = @AdminUserId AND TenantId = '00000000-0000-0000-0000-000000000003')
                    BEGIN
                        INSERT INTO UserTenants (Id, UserId, TenantId, AccessLevel, Status, IsDefault, GrantedAt, GrantedBy, CreatedAt, CreatedBy, IsDeleted)
                        VALUES (NEWID(), @AdminUserId, '00000000-0000-0000-0000-000000000003', 2, 0, 0, GETUTCDATE(), 'System', GETUTCDATE(), 'System', 0);
                    END
                END
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250923161008_SeedAdditionalTenantsAndUserMappings', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [Users] DROP CONSTRAINT [FK_Users_Tenants_TenantId1];
GO

DROP INDEX [IX_Users_TenantId1] ON [Users];
GO

DELETE FROM [TenantModules]
WHERE [Id] = '1da24716-fd56-44b0-8d2c-b4e7d6850c9b';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '4643d21c-6433-49b7-9026-66c7039d7351';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'a7673475-7689-4253-81ec-62894c352cb9';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'ac1534db-0b3a-4f1a-8da4-600e8d3609ae';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'acdad383-4d21-4f5b-9fc3-2e16220d7102';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'c5f28f1e-92ea-43c7-883c-03ed0485b45b';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'df39c1d7-5c64-415d-9485-fe50d7c4f821';
SELECT @@ROWCOUNT;

GO

DECLARE @var1 sysname;
SELECT @var1 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'TenantId1');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var1 + '];');
ALTER TABLE [Users] DROP COLUMN [TenantId1];
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-23T16:21:53.0847629Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-23T16:21:53.0847713Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-23T16:21:53.0847719Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-23T16:21:53.0847725Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('36ca7275-9ce5-4f00-a912-aa63425f0ac2', NULL, '2025-09-23T16:21:53.0847972Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T16:21:53.0847965Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('44b5c7b7-464e-457f-bbf6-6edf2fb04053', NULL, '2025-09-23T16:21:53.0848163Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T16:21:53.0848163Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('64b55479-2ed0-4613-9dcc-4c3db0d38fcb', NULL, '2025-09-23T16:21:53.0848137Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T16:21:53.0848136Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('6ad704b8-2d8f-4894-a2ac-7aaf8b61c21e', NULL, '2025-09-23T16:21:53.0848103Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T16:21:53.0848102Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('a73e51bb-eb0f-42e7-ba22-ac8d50a7fe8c', NULL, '2025-09-23T16:21:53.0848077Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T16:21:53.0848076Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('c59f5e2c-a983-444c-ad52-613f0444fe98', NULL, '2025-09-23T16:21:53.0848047Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T16:21:53.0848046Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('db83ef08-fe98-4236-8cf4-1834cfa077d9', NULL, '2025-09-23T16:21:53.0848010Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T16:21:53.0848009Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-09-23T16:21:53.0847267Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250923162154_FixTenantEntityConfiguration', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '36ca7275-9ce5-4f00-a912-aa63425f0ac2';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '44b5c7b7-464e-457f-bbf6-6edf2fb04053';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '64b55479-2ed0-4613-9dcc-4c3db0d38fcb';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '6ad704b8-2d8f-4894-a2ac-7aaf8b61c21e';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'a73e51bb-eb0f-42e7-ba22-ac8d50a7fe8c';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'c59f5e2c-a983-444c-ad52-613f0444fe98';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'db83ef08-fe98-4236-8cf4-1834cfa077d9';
SELECT @@ROWCOUNT;

GO

ALTER TABLE [Tenants] ADD [CoverImageUrl] nvarchar(500) NULL;
GO

ALTER TABLE [Tenants] ADD [FaviconUrl] nvarchar(500) NULL;
GO

ALTER TABLE [Tenants] ADD [PrimaryColor] nvarchar(7) NULL;
GO

ALTER TABLE [Tenants] ADD [SecondaryColor] nvarchar(7) NULL;
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-23T19:19:55.2772528Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-23T19:19:55.2772568Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-23T19:19:55.2772570Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-23T19:19:55.2772573Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('01c2efeb-4436-47cc-8901-ff0fd9e99cd4', NULL, '2025-09-23T19:19:55.2772640Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T19:19:55.2772640Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('21f83ba1-8c0d-4cb2-a261-d816817913f1', NULL, '2025-09-23T19:19:55.2772679Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T19:19:55.2772679Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('3906592d-eda5-4422-882c-6333b48b303a', NULL, '2025-09-23T19:19:55.2772623Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T19:19:55.2772620Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('73ca1e48-49d7-4db4-9e9c-cf4b1bbbaef2', NULL, '2025-09-23T19:19:55.2772667Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T19:19:55.2772667Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('8e9dcf6c-5654-4c8f-8cf8-7bf8545930e8', NULL, '2025-09-23T19:19:55.2772705Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T19:19:55.2772704Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('b04f99fc-9f67-4b17-8b1e-1c8a3e10d7b3', NULL, '2025-09-23T19:19:55.2772693Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T19:19:55.2772692Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('bbe225a4-f21a-4664-b352-fb56e377dd0b', NULL, '2025-09-23T19:19:55.2772654Z', NULL, NULL, NULL, NULL, NULL, '2025-09-23T19:19:55.2772653Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CoverImageUrl] = NULL, [CreatedAt] = '2025-09-23T19:19:55.2772385Z', [FaviconUrl] = NULL, [PrimaryColor] = NULL, [SecondaryColor] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250923191956_AddTenantBrandingColumns', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '01c2efeb-4436-47cc-8901-ff0fd9e99cd4';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '21f83ba1-8c0d-4cb2-a261-d816817913f1';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '3906592d-eda5-4422-882c-6333b48b303a';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '73ca1e48-49d7-4db4-9e9c-cf4b1bbbaef2';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '8e9dcf6c-5654-4c8f-8cf8-7bf8545930e8';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'b04f99fc-9f67-4b17-8b1e-1c8a3e10d7b3';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'bbe225a4-f21a-4664-b352-fb56e377dd0b';
SELECT @@ROWCOUNT;

GO

CREATE TABLE [Permissions] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [DisplayName] nvarchar(200) NOT NULL,
    [Description] nvarchar(500) NULL,
    [Category] nvarchar(100) NOT NULL,
    [IsSystemPermission] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [RolePermissions] (
    [RoleId] uniqueidentifier NOT NULL,
    [PermissionId] uniqueidentifier NOT NULL,
    [GrantedAt] datetime2 NOT NULL,
    [GrantedBy] nvarchar(100) NULL,
    CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([RoleId], [PermissionId]),
    CONSTRAINT [FK_RolePermissions_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]) ON DELETE CASCADE
);
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-25T22:27:33.0725186Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-25T22:27:33.0725224Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-25T22:27:33.0725227Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-25T22:27:33.0725229Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Category', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisplayName', N'IsDeleted', N'IsSystemPermission', N'Name', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Permissions]'))
    SET IDENTITY_INSERT [Permissions] ON;
INSERT INTO [Permissions] ([Id], [Category], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisplayName], [IsDeleted], [IsSystemPermission], [Name], [UpdatedAt], [UpdatedBy])
VALUES ('00000000-0000-0000-0000-000000000001', N'User Management', '2025-09-25T22:27:33.0725639Z', NULL, NULL, NULL, N'View user accounts and details', N'View Users', CAST(0 AS bit), CAST(1 AS bit), N'users.read', NULL, NULL),
('00000000-0000-0000-0000-000000000002', N'User Management', '2025-09-25T22:27:33.0725648Z', NULL, NULL, NULL, N'Create new user accounts', N'Create Users', CAST(0 AS bit), CAST(1 AS bit), N'users.create', NULL, NULL),
('00000000-0000-0000-0000-000000000003', N'User Management', '2025-09-25T22:27:33.0725653Z', NULL, NULL, NULL, N'Edit existing user accounts', N'Update Users', CAST(0 AS bit), CAST(1 AS bit), N'users.update', NULL, NULL),
('00000000-0000-0000-0000-000000000004', N'User Management', '2025-09-25T22:27:33.0725657Z', NULL, NULL, NULL, N'Delete user accounts', N'Delete Users', CAST(0 AS bit), CAST(1 AS bit), N'users.delete', NULL, NULL),
('00000000-0000-0000-0000-000000000005', N'Role Management', '2025-09-25T22:27:33.0725667Z', NULL, NULL, NULL, N'View role definitions', N'View Roles', CAST(0 AS bit), CAST(1 AS bit), N'roles.read', NULL, NULL),
('00000000-0000-0000-0000-000000000006', N'Role Management', '2025-09-25T22:27:33.0725675Z', NULL, NULL, NULL, N'Create new roles', N'Create Roles', CAST(0 AS bit), CAST(1 AS bit), N'roles.create', NULL, NULL),
('00000000-0000-0000-0000-000000000007', N'Role Management', '2025-09-25T22:27:33.0725688Z', NULL, NULL, NULL, N'Edit existing roles', N'Update Roles', CAST(0 AS bit), CAST(1 AS bit), N'roles.update', NULL, NULL),
('00000000-0000-0000-0000-000000000008', N'Role Management', '2025-09-25T22:27:33.0725693Z', NULL, NULL, NULL, N'Delete roles', N'Delete Roles', CAST(0 AS bit), CAST(1 AS bit), N'roles.delete', NULL, NULL),
('00000000-0000-0000-0000-000000000009', N'Dashboard & Reports', '2025-09-25T22:27:33.0725702Z', NULL, NULL, NULL, N'Access main dashboard', N'View Dashboard', CAST(0 AS bit), CAST(1 AS bit), N'dashboard.read', NULL, NULL),
('00000000-0000-0000-0000-000000000010', N'Dashboard & Reports', '2025-09-25T22:27:33.0725709Z', NULL, NULL, NULL, N'Access reporting features', N'View Reports', CAST(0 AS bit), CAST(1 AS bit), N'reports.read', NULL, NULL),
('00000000-0000-0000-0000-000000000011', N'Dashboard & Reports', '2025-09-25T22:27:33.0725715Z', NULL, NULL, NULL, N'Generate custom reports', N'Create Reports', CAST(0 AS bit), CAST(1 AS bit), N'reports.create', NULL, NULL),
('00000000-0000-0000-0000-000000000012', N'Dashboard & Reports', '2025-09-25T22:27:33.0725725Z', NULL, NULL, NULL, N'Access analytics data', N'View Analytics', CAST(0 AS bit), CAST(1 AS bit), N'analytics.read', NULL, NULL),
('00000000-0000-0000-0000-000000000013', N'System Administration', '2025-09-25T22:27:33.0725733Z', NULL, NULL, NULL, N'Access admin interface', N'View Admin', CAST(0 AS bit), CAST(1 AS bit), N'admin.read', NULL, NULL),
('00000000-0000-0000-0000-000000000014', N'System Administration', '2025-09-25T22:27:33.0725748Z', NULL, NULL, NULL, N'View system settings', N'View Settings', CAST(0 AS bit), CAST(1 AS bit), N'settings.read', NULL, NULL),
('00000000-0000-0000-0000-000000000015', N'System Administration', '2025-09-25T22:27:33.0725753Z', NULL, NULL, NULL, N'Modify system settings', N'Update Settings', CAST(0 AS bit), CAST(1 AS bit), N'settings.update', NULL, NULL),
('00000000-0000-0000-0000-000000000016', N'System Administration', '2025-09-25T22:27:33.0725757Z', NULL, NULL, NULL, N'Access audit trail', N'View Audit Logs', CAST(0 AS bit), CAST(1 AS bit), N'audit.read', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Category', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisplayName', N'IsDeleted', N'IsSystemPermission', N'Name', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Permissions]'))
    SET IDENTITY_INSERT [Permissions] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('0eb1e162-c834-4090-9ddd-c63b918689b5', NULL, '2025-09-25T22:27:33.0725443Z', NULL, NULL, NULL, NULL, NULL, '2025-09-25T22:27:33.0725438Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('21db1bb8-c582-402b-9030-d460d9b2c364', NULL, '2025-09-25T22:27:33.0725526Z', NULL, NULL, NULL, NULL, NULL, '2025-09-25T22:27:33.0725525Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('40580bfa-bd22-40b5-9e7c-2ec0eb767d16', NULL, '2025-09-25T22:27:33.0725543Z', NULL, NULL, NULL, NULL, NULL, '2025-09-25T22:27:33.0725542Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('5db01044-f9b1-4eaa-9c60-179f968ea9d6', NULL, '2025-09-25T22:27:33.0725557Z', NULL, NULL, NULL, NULL, NULL, '2025-09-25T22:27:33.0725557Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('7a949ede-0f99-41bd-be45-48e62669c0d1', NULL, '2025-09-25T22:27:33.0725475Z', NULL, NULL, NULL, NULL, NULL, '2025-09-25T22:27:33.0725475Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('7c9c9cc5-be45-469e-a215-5b05030d2c2c', NULL, '2025-09-25T22:27:33.0725492Z', NULL, NULL, NULL, NULL, NULL, '2025-09-25T22:27:33.0725492Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('9068b023-0439-43d1-8bc5-56b0b1848e13', NULL, '2025-09-25T22:27:33.0725509Z', NULL, NULL, NULL, NULL, NULL, '2025-09-25T22:27:33.0725508Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-09-25T22:27:33.0725048Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PermissionId', N'RoleId', N'GrantedAt', N'GrantedBy') AND [object_id] = OBJECT_ID(N'[RolePermissions]'))
    SET IDENTITY_INSERT [RolePermissions] ON;
INSERT INTO [RolePermissions] ([PermissionId], [RoleId], [GrantedAt], [GrantedBy])
VALUES ('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000001', '2025-09-25T22:27:33.0725802Z', N'System'),
('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', '2025-09-25T22:27:33.0725804Z', N'System'),
('00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000001', '2025-09-25T22:27:33.0725805Z', N'System'),
('00000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000001', '2025-09-25T22:27:33.0725806Z', N'System'),
('00000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000001', '2025-09-25T22:27:33.0725807Z', N'System'),
('00000000-0000-0000-0000-000000000006', '00000000-0000-0000-0000-000000000001', '2025-09-25T22:27:33.0725809Z', N'System'),
('00000000-0000-0000-0000-000000000007', '00000000-0000-0000-0000-000000000001', '2025-09-25T22:27:33.0725810Z', N'System'),
('00000000-0000-0000-0000-000000000008', '00000000-0000-0000-0000-000000000001', '2025-09-25T22:27:33.0725811Z', N'System'),
('00000000-0000-0000-0000-000000000009', '00000000-0000-0000-0000-000000000001', '2025-09-25T22:27:33.0725811Z', N'System'),
('00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000001', '2025-09-25T22:27:33.0725813Z', N'System'),
('00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000001', '2025-09-25T22:27:33.0725814Z', N'System'),
('00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000001', '2025-09-25T22:27:33.0725815Z', N'System'),
('00000000-0000-0000-0000-000000000013', '00000000-0000-0000-0000-000000000001', '2025-09-25T22:27:33.0725816Z', N'System'),
('00000000-0000-0000-0000-000000000014', '00000000-0000-0000-0000-000000000001', '2025-09-25T22:27:33.0725816Z', N'System'),
('00000000-0000-0000-0000-000000000015', '00000000-0000-0000-0000-000000000001', '2025-09-25T22:27:33.0725817Z', N'System'),
('00000000-0000-0000-0000-000000000016', '00000000-0000-0000-0000-000000000001', '2025-09-25T22:27:33.0725818Z', N'System'),
('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000002', '2025-09-25T22:27:33.0725851Z', N'System'),
('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000002', '2025-09-25T22:27:33.0725854Z', N'System'),
('00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000002', '2025-09-25T22:27:33.0725855Z', N'System'),
('00000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000002', '2025-09-25T22:27:33.0725855Z', N'System'),
('00000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000002', '2025-09-25T22:27:33.0725856Z', N'System'),
('00000000-0000-0000-0000-000000000006', '00000000-0000-0000-0000-000000000002', '2025-09-25T22:27:33.0725857Z', N'System'),
('00000000-0000-0000-0000-000000000007', '00000000-0000-0000-0000-000000000002', '2025-09-25T22:27:33.0725858Z', N'System'),
('00000000-0000-0000-0000-000000000008', '00000000-0000-0000-0000-000000000002', '2025-09-25T22:27:33.0725858Z', N'System'),
('00000000-0000-0000-0000-000000000009', '00000000-0000-0000-0000-000000000002', '2025-09-25T22:27:33.0725859Z', N'System'),
('00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000002', '2025-09-25T22:27:33.0725860Z', N'System'),
('00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000002', '2025-09-25T22:27:33.0725861Z', N'System'),
('00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000002', '2025-09-25T22:27:33.0725861Z', N'System'),
('00000000-0000-0000-0000-000000000014', '00000000-0000-0000-0000-000000000002', '2025-09-25T22:27:33.0725862Z', N'System'),
('00000000-0000-0000-0000-000000000015', '00000000-0000-0000-0000-000000000002', '2025-09-25T22:27:33.0725863Z', N'System'),
('00000000-0000-0000-0000-000000000016', '00000000-0000-0000-0000-000000000002', '2025-09-25T22:27:33.0725864Z', N'System'),
('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000003', '2025-09-25T22:27:33.0725896Z', N'System'),
('00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000003', '2025-09-25T22:27:33.0725897Z', N'System'),
('00000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000003', '2025-09-25T22:27:33.0725904Z', N'System'),
('00000000-0000-0000-0000-000000000009', '00000000-0000-0000-0000-000000000003', '2025-09-25T22:27:33.0725905Z', N'System'),
('00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000003', '2025-09-25T22:27:33.0725906Z', N'System'),
('00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000003', '2025-09-25T22:27:33.0725907Z', N'System'),
('00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000003', '2025-09-25T22:27:33.0725907Z', N'System'),
('00000000-0000-0000-0000-000000000013', '00000000-0000-0000-0000-000000000003', '2025-09-25T22:27:33.0725908Z', N'System'),
('00000000-0000-0000-0000-000000000014', '00000000-0000-0000-0000-000000000003', '2025-09-25T22:27:33.0725909Z', N'System'),
('00000000-0000-0000-0000-000000000016', '00000000-0000-0000-0000-000000000003', '2025-09-25T22:27:33.0725910Z', N'System'),
('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000004', '2025-09-25T22:27:33.0725923Z', N'System');
INSERT INTO [RolePermissions] ([PermissionId], [RoleId], [GrantedAt], [GrantedBy])
VALUES ('00000000-0000-0000-0000-000000000009', '00000000-0000-0000-0000-000000000004', '2025-09-25T22:27:33.0725924Z', N'System'),
('00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000004', '2025-09-25T22:27:33.0725925Z', N'System');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PermissionId', N'RoleId', N'GrantedAt', N'GrantedBy') AND [object_id] = OBJECT_ID(N'[RolePermissions]'))
    SET IDENTITY_INSERT [RolePermissions] OFF;
GO

CREATE INDEX [IX_Permissions_Category] ON [Permissions] ([Category]);
GO

CREATE UNIQUE INDEX [IX_Permissions_Name] ON [Permissions] ([Name]);
GO

CREATE INDEX [IX_RolePermissions_PermissionId] ON [RolePermissions] ([PermissionId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250925222734_AddPermissionEntities', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '0eb1e162-c834-4090-9ddd-c63b918689b5';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '21db1bb8-c582-402b-9030-d460d9b2c364';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '40580bfa-bd22-40b5-9e7c-2ec0eb767d16';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '5db01044-f9b1-4eaa-9c60-179f968ea9d6';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '7a949ede-0f99-41bd-be45-48e62669c0d1';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '7c9c9cc5-be45-469e-a215-5b05030d2c2c';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '9068b023-0439-43d1-8bc5-56b0b1848e13';
SELECT @@ROWCOUNT;

GO

EXEC sp_rename N'[Users].[IX_Users_UserName]', N'IX_ApplicationUser_UserName_Unique', N'INDEX';
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-27T11:44:42.5013808Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-27T11:44:42.5013846Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-27T11:44:42.5013857Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-27T11:44:42.5013859Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-27T11:44:42.5014071Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-27T11:44:42.5014080Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-27T11:44:42.5014085Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-27T11:44:42.5014089Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-27T11:44:42.5014101Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-27T11:44:42.5014107Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-27T11:44:42.5014112Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-27T11:44:42.5014116Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-27T11:44:42.5014124Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-27T11:44:42.5014132Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-27T11:44:42.5014142Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-27T11:44:42.5014146Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-27T11:44:42.5014156Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-27T11:44:42.5014161Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-27T11:44:42.5014165Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-27T11:44:42.5014168Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014209Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014211Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014212Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014213Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014214Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014216Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014217Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014218Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014219Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014221Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014222Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014222Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014223Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014224Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014225Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014225Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014256Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014259Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014260Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014261Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014262Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014263Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014263Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014264Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014265Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014266Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014266Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014267Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014268Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014269Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014269Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014304Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014305Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014307Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014308Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014309Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014310Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014310Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014311Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014312Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014313Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014327Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014328Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-27T11:44:42.5014329Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('13a31a76-13db-4f5b-98aa-97d166bc44c6', NULL, '2025-09-27T11:44:42.5013964Z', NULL, NULL, NULL, NULL, NULL, '2025-09-27T11:44:42.5013964Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('34707133-62c2-4b25-b604-686f6b767234', NULL, '2025-09-27T11:44:42.5013978Z', NULL, NULL, NULL, NULL, NULL, '2025-09-27T11:44:42.5013977Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('3cf13e57-43f3-4237-8ed9-363d038b8c72', NULL, '2025-09-27T11:44:42.5013919Z', NULL, NULL, NULL, NULL, NULL, '2025-09-27T11:44:42.5013916Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('3dd19062-607f-4cbf-84e8-f1e75968b9fd', NULL, '2025-09-27T11:44:42.5013935Z', NULL, NULL, NULL, NULL, NULL, '2025-09-27T11:44:42.5013935Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('4b811d7a-9e07-4e5e-b3f3-e753b2abd20d', NULL, '2025-09-27T11:44:42.5013950Z', NULL, NULL, NULL, NULL, NULL, '2025-09-27T11:44:42.5013949Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('ee1b4fe4-3fdf-4ea2-a4f8-85ab31bd9ed8', NULL, '2025-09-27T11:44:42.5014002Z', NULL, NULL, NULL, NULL, NULL, '2025-09-27T11:44:42.5014002Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('fbee9da2-a19a-4f08-a03d-9dd29a5471bf', NULL, '2025-09-27T11:44:42.5013990Z', NULL, NULL, NULL, NULL, NULL, '2025-09-27T11:44:42.5013990Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-09-27T11:44:42.5013629Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

CREATE UNIQUE INDEX [IX_ApplicationUser_Email_Unique] ON [Users] ([Email]) WHERE [Email] IS NOT NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250927114443_AddUniqueConstraintsToUser', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '13a31a76-13db-4f5b-98aa-97d166bc44c6';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '34707133-62c2-4b25-b604-686f6b767234';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '3cf13e57-43f3-4237-8ed9-363d038b8c72';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '3dd19062-607f-4cbf-84e8-f1e75968b9fd';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '4b811d7a-9e07-4e5e-b3f3-e753b2abd20d';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'ee1b4fe4-3fdf-4ea2-a4f8-85ab31bd9ed8';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'fbee9da2-a19a-4f08-a03d-9dd29a5471bf';
SELECT @@ROWCOUNT;

GO

CREATE TABLE [BlacklistedTokens] (
    [Id] uniqueidentifier NOT NULL,
    [Jti] nvarchar(100) NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [BlacklistedAt] datetime2 NOT NULL,
    [Reason] nvarchar(200) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_BlacklistedTokens] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BlacklistedTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [RefreshTokens] (
    [Id] uniqueidentifier NOT NULL,
    [TokenHash] nvarchar(255) NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [IsRevoked] bit NOT NULL,
    [RevokedAt] datetime2 NULL,
    [RevokedBy] uniqueidentifier NULL,
    [RevocationReason] nvarchar(200) NULL,
    [LastUsedAt] datetime2 NULL,
    [UsageCount] int NOT NULL,
    [MaxUsageCount] int NOT NULL,
    [IpAddress] nvarchar(45) NULL,
    [UserAgent] nvarchar(500) NULL,
    [DeviceId] nvarchar(100) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RefreshTokens_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-28T02:04:44.6077324Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-28T02:04:44.6077374Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-28T02:04:44.6077377Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-28T02:04:44.6077379Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:04:44.6077831Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:04:44.6077845Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:04:44.6077853Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:04:44.6077861Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:04:44.6077873Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:04:44.6077881Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:04:44.6077888Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:04:44.6077896Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:04:44.6077907Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:04:44.6077916Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:04:44.6077923Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:04:44.6077930Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:04:44.6077940Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:04:44.6078012Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:04:44.6078019Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:04:44.6078027Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078081Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078085Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078087Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078088Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078088Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078091Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078092Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078092Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078094Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078095Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078096Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078097Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078098Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078098Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078099Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078100Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078185Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078188Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078189Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078190Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078191Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078192Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078193Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078194Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078195Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078195Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078196Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078197Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078198Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078199Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078199Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078250Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078251Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078253Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078254Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078255Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078255Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078256Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078257Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078258Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078259Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078275Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078276Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:04:44.6078277Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('1479f2f1-ac8f-42bd-a4cf-714836335095', NULL, '2025-09-28T02:04:44.6077697Z', NULL, NULL, NULL, NULL, NULL, '2025-09-28T02:04:44.6077696Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('334333c4-505b-491a-891d-ff04b2e9a404', NULL, '2025-09-28T02:04:44.6077742Z', NULL, NULL, NULL, NULL, NULL, '2025-09-28T02:04:44.6077742Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('4aa20dec-29b5-4da1-8d70-92d3fc5a64c3', NULL, '2025-09-28T02:04:44.6077684Z', NULL, NULL, NULL, NULL, NULL, '2025-09-28T02:04:44.6077684Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('4f4a8a82-a550-4ca5-ad6a-0671d7ebb2c5', NULL, '2025-09-28T02:04:44.6077667Z', NULL, NULL, NULL, NULL, NULL, '2025-09-28T02:04:44.6077667Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('87c81065-5b98-423b-8e70-767ab76c4584', NULL, '2025-09-28T02:04:44.6077633Z', NULL, NULL, NULL, NULL, NULL, '2025-09-28T02:04:44.6077629Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('c09a5fe8-3602-4a34-8b0b-95ad547ac175', NULL, '2025-09-28T02:04:44.6077711Z', NULL, NULL, NULL, NULL, NULL, '2025-09-28T02:04:44.6077710Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('d94a9efd-6fc8-4b30-9cfc-f056894bf877', NULL, '2025-09-28T02:04:44.6077727Z', NULL, NULL, NULL, NULL, NULL, '2025-09-28T02:04:44.6077726Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-09-28T02:04:44.6076994Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_BlacklistedTokens_ExpiresAt] ON [BlacklistedTokens] ([ExpiresAt]);
GO

CREATE UNIQUE INDEX [IX_BlacklistedTokens_Jti] ON [BlacklistedTokens] ([Jti]);
GO

CREATE INDEX [IX_BlacklistedTokens_UserId] ON [BlacklistedTokens] ([UserId]);
GO

CREATE INDEX [IX_RefreshTokens_ExpiresAt] ON [RefreshTokens] ([ExpiresAt]);
GO

CREATE INDEX [IX_RefreshTokens_TenantId] ON [RefreshTokens] ([TenantId]);
GO

CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);
GO

CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250928020446_AddTokenManagementTables', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '1479f2f1-ac8f-42bd-a4cf-714836335095';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '334333c4-505b-491a-891d-ff04b2e9a404';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '4aa20dec-29b5-4da1-8d70-92d3fc5a64c3';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '4f4a8a82-a550-4ca5-ad6a-0671d7ebb2c5';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '87c81065-5b98-423b-8e70-767ab76c4584';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'c09a5fe8-3602-4a34-8b0b-95ad547ac175';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'd94a9efd-6fc8-4b30-9cfc-f056894bf877';
SELECT @@ROWCOUNT;

GO

ALTER TABLE [Securities] ADD [PrivacyPolicyUrl] nvarchar(2048) NULL;
GO

ALTER TABLE [Securities] ADD [TermsOfServiceUrl] nvarchar(2048) NULL;
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-28T02:36:02.7460266Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-28T02:36:02.7460384Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-28T02:36:02.7460387Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-28T02:36:02.7460390Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:36:02.7460622Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:36:02.7460633Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:36:02.7460638Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:36:02.7460644Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:36:02.7460654Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:36:02.7460661Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:36:02.7460666Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:36:02.7460670Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:36:02.7460684Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:36:02.7460691Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:36:02.7460696Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:36:02.7460798Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:36:02.7460807Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:36:02.7460812Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:36:02.7460816Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-28T02:36:02.7460821Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460867Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460870Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460871Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460872Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460873Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460875Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460876Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460877Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460878Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460879Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460880Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460881Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460882Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460883Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460884Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460885Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460925Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460928Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460929Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460929Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460930Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460931Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460932Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460933Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460933Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460934Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460935Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460936Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460936Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460937Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460938Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460976Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460978Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460980Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460981Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460982Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460982Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460983Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460984Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460985Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7460985Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7461000Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7461001Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-28T02:36:02.7461002Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('06afacad-7cd9-4052-8203-769e515b44b7', NULL, '2025-09-28T02:36:02.7460531Z', NULL, NULL, NULL, NULL, NULL, '2025-09-28T02:36:02.7460531Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('12887005-fc64-4eb4-9c4f-9fa9f79ec4ea', NULL, '2025-09-28T02:36:02.7460517Z', NULL, NULL, NULL, NULL, NULL, '2025-09-28T02:36:02.7460516Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('1907a25d-41cb-4e79-a9b2-165e7a790aee', NULL, '2025-09-28T02:36:02.7460544Z', NULL, NULL, NULL, NULL, NULL, '2025-09-28T02:36:02.7460543Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('69a1c93e-81de-4fb9-9e68-06ba591ce85c', NULL, '2025-09-28T02:36:02.7460457Z', NULL, NULL, NULL, NULL, NULL, '2025-09-28T02:36:02.7460453Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('836f633a-72c7-4760-8b74-aa4f62d499fc', NULL, '2025-09-28T02:36:02.7460504Z', NULL, NULL, NULL, NULL, NULL, '2025-09-28T02:36:02.7460503Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('ccd31a12-4a8b-41e6-b056-86188b6fa0b0', NULL, '2025-09-28T02:36:02.7460475Z', NULL, NULL, NULL, NULL, NULL, '2025-09-28T02:36:02.7460474Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('f97e9245-90ef-4486-bb29-f1864dd32542', NULL, '2025-09-28T02:36:02.7460491Z', NULL, NULL, NULL, NULL, NULL, '2025-09-28T02:36:02.7460491Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-09-28T02:36:02.7460048Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250928023603_AddLegalUrlsToSecuritySettings', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '06afacad-7cd9-4052-8203-769e515b44b7';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '12887005-fc64-4eb4-9c4f-9fa9f79ec4ea';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '1907a25d-41cb-4e79-a9b2-165e7a790aee';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '69a1c93e-81de-4fb9-9e68-06ba591ce85c';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '836f633a-72c7-4760-8b74-aa4f62d499fc';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'ccd31a12-4a8b-41e6-b056-86188b6fa0b0';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'f97e9245-90ef-4486-bb29-f1864dd32542';
SELECT @@ROWCOUNT;

GO

CREATE TABLE [EmailTemplates] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Module] nvarchar(50) NOT NULL,
    [TableName] nvarchar(100) NULL,
    [Subject] nvarchar(200) NOT NULL,
    [HtmlBody] nvarchar(max) NOT NULL,
    [PlainTextBody] nvarchar(max) NULL,
    [SelectedFields] nvarchar(max) NULL,
    [TemplateVariables] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [Description] nvarchar(500) NULL,
    [Category] nvarchar(50) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmailTemplates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmailTemplates_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
);
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-29T04:51:05.9662513Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-29T04:51:05.9662554Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-29T04:51:05.9662557Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-29T04:51:05.9662560Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T04:51:05.9662853Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T04:51:05.9662861Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T04:51:05.9662867Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T04:51:05.9662871Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T04:51:05.9662882Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T04:51:05.9662888Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T04:51:05.9662892Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T04:51:05.9662896Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T04:51:05.9662904Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T04:51:05.9662911Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T04:51:05.9662915Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T04:51:05.9662920Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T04:51:05.9662930Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T04:51:05.9662935Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T04:51:05.9662939Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T04:51:05.9662942Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9662985Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9662988Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9662989Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9662990Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9662991Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9662993Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9662993Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9662994Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9662995Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9662997Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9662998Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9662999Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663000Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663000Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663001Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663002Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663036Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663075Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663076Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663077Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663078Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663079Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663080Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663081Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663081Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663082Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663083Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663084Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663084Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663085Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663086Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663127Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663128Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663130Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663131Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663132Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663133Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663133Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663134Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663135Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663136Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663149Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663151Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T04:51:05.9663151Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('24d00e00-a6dc-4073-87bd-8072655d3ac7', NULL, '2025-09-29T04:51:05.9662622Z', NULL, NULL, NULL, NULL, NULL, '2025-09-29T04:51:05.9662619Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('a66407ba-6798-4307-834a-2ab53d67c7cd', NULL, '2025-09-29T04:51:05.9662638Z', NULL, NULL, NULL, NULL, NULL, '2025-09-29T04:51:05.9662638Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('a7a8cc46-5c4f-4a35-a0c6-654ddd46f155', NULL, '2025-09-29T04:51:05.9662691Z', NULL, NULL, NULL, NULL, NULL, '2025-09-29T04:51:05.9662691Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('c2305568-0a1f-457c-abd2-23e3416bd3ab', NULL, '2025-09-29T04:51:05.9662679Z', NULL, NULL, NULL, NULL, NULL, '2025-09-29T04:51:05.9662678Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('dddfbb12-3f56-45ee-b0e2-b06f42c146d0', NULL, '2025-09-29T04:51:05.9662666Z', NULL, NULL, NULL, NULL, NULL, '2025-09-29T04:51:05.9662666Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('de7669d4-5e28-4536-9fb1-4094a0fe8658', NULL, '2025-09-29T04:51:05.9662652Z', NULL, NULL, NULL, NULL, NULL, '2025-09-29T04:51:05.9662651Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('ebdb2469-9cc7-49b6-b995-8ffcae60c738', NULL, '2025-09-29T04:51:05.9662702Z', NULL, NULL, NULL, NULL, NULL, '2025-09-29T04:51:05.9662702Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-09-29T04:51:05.9662336Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_EmailTemplates_Category] ON [EmailTemplates] ([Category]);
GO

CREATE INDEX [IX_EmailTemplates_Module] ON [EmailTemplates] ([Module]);
GO

CREATE UNIQUE INDEX [IX_EmailTemplates_TenantId_Name] ON [EmailTemplates] ([TenantId], [Name]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250929045106_AddEmailTemplates', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '24d00e00-a6dc-4073-87bd-8072655d3ac7';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'a66407ba-6798-4307-834a-2ab53d67c7cd';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'a7a8cc46-5c4f-4a35-a0c6-654ddd46f155';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'c2305568-0a1f-457c-abd2-23e3416bd3ab';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'dddfbb12-3f56-45ee-b0e2-b06f42c146d0';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'de7669d4-5e28-4536-9fb1-4094a0fe8658';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'ebdb2469-9cc7-49b6-b995-8ffcae60c738';
SELECT @@ROWCOUNT;

GO

CREATE TABLE [UserSessions] (
    [SessionId] nvarchar(450) NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [LoginTime] datetime2 NOT NULL,
    [LastActivityTime] datetime2 NOT NULL,
    [LogoutTime] datetime2 NULL,
    [IpAddress] nvarchar(500) NOT NULL,
    [UserAgent] nvarchar(1000) NOT NULL,
    [DeviceFingerprint] nvarchar(100) NOT NULL,
    [DeviceType] nvarchar(50) NOT NULL,
    [Browser] nvarchar(100) NOT NULL,
    [OperatingSystem] nvarchar(100) NOT NULL,
    [Location] nvarchar(200) NOT NULL,
    [IsActive] bit NOT NULL,
    [WasTerminatedByConcurrentLogin] bit NOT NULL,
    [TerminationReason] nvarchar(500) NOT NULL,
    CONSTRAINT [PK_UserSessions] PRIMARY KEY ([SessionId]),
    CONSTRAINT [FK_UserSessions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserSessions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-29T20:51:13.9043329Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-29T20:51:13.9043376Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-29T20:51:13.9043378Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-29T20:51:13.9043381Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T20:51:13.9043651Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T20:51:13.9043660Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T20:51:13.9043666Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T20:51:13.9043670Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T20:51:13.9043679Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T20:51:13.9043687Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T20:51:13.9043692Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T20:51:13.9043695Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T20:51:13.9043704Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T20:51:13.9043710Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T20:51:13.9043715Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T20:51:13.9043719Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T20:51:13.9043727Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T20:51:13.9043733Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T20:51:13.9043737Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T20:51:13.9043741Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043783Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043812Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043814Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043815Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043816Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043818Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043819Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043820Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043821Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043822Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043823Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043824Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043825Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043826Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043826Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043827Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043861Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043864Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043865Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043866Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043867Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043868Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043869Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043869Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043870Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043871Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043872Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043873Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043873Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043874Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043875Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043909Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043911Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043913Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043914Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043914Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043915Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043916Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043917Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043917Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043918Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043932Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043934Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T20:51:13.9043935Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('49750739-a7d0-4028-bf2d-3d984630c651', NULL, '2025-09-29T20:51:13.9043537Z', NULL, NULL, NULL, NULL, NULL, '2025-09-29T20:51:13.9043537Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('769c9eed-ff61-436d-92b6-37227e5a53c9', NULL, '2025-09-29T20:51:13.9043577Z', NULL, NULL, NULL, NULL, NULL, '2025-09-29T20:51:13.9043576Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('9815fa1a-1273-4641-ab39-7afd09e6df4e', NULL, '2025-09-29T20:51:13.9043470Z', NULL, NULL, NULL, NULL, NULL, '2025-09-29T20:51:13.9043469Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('d620b646-297a-4c13-b05f-85e3ccebdd76', NULL, '2025-09-29T20:51:13.9043524Z', NULL, NULL, NULL, NULL, NULL, '2025-09-29T20:51:13.9043524Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('d8999830-573f-4541-ada0-119ba0724fb2', NULL, '2025-09-29T20:51:13.9043565Z', NULL, NULL, NULL, NULL, NULL, '2025-09-29T20:51:13.9043565Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('e7208f91-5a6b-4399-86b9-3c6c0f0cfc4f', NULL, '2025-09-29T20:51:13.9043451Z', NULL, NULL, NULL, NULL, NULL, '2025-09-29T20:51:13.9043447Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('ebd9a0d1-6ff1-41a9-945b-e7dd92dd29cd', NULL, '2025-09-29T20:51:13.9043552Z', NULL, NULL, NULL, NULL, NULL, '2025-09-29T20:51:13.9043551Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-09-29T20:51:13.9043167Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_UserSessions_TenantId] ON [UserSessions] ([TenantId]);
GO

CREATE INDEX [IX_UserSessions_UserId] ON [UserSessions] ([UserId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250929205114_AddUserSessions', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '49750739-a7d0-4028-bf2d-3d984630c651';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '769c9eed-ff61-436d-92b6-37227e5a53c9';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '9815fa1a-1273-4641-ab39-7afd09e6df4e';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'd620b646-297a-4c13-b05f-85e3ccebdd76';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'd8999830-573f-4541-ada0-119ba0724fb2';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'e7208f91-5a6b-4399-86b9-3c6c0f0cfc4f';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'ebd9a0d1-6ff1-41a9-945b-e7dd92dd29cd';
SELECT @@ROWCOUNT;

GO

ALTER TABLE [UserSessions] ADD [JwtTokenId] nvarchar(100) NULL;
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-29T21:24:22.3057737Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-29T21:24:22.3057792Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-29T21:24:22.3057795Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-09-29T21:24:22.3057797Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T21:24:22.3058031Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T21:24:22.3058040Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T21:24:22.3058046Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T21:24:22.3058050Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T21:24:22.3058138Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T21:24:22.3058147Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T21:24:22.3058151Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T21:24:22.3058156Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T21:24:22.3058165Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T21:24:22.3058173Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T21:24:22.3058178Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T21:24:22.3058182Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T21:24:22.3058190Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T21:24:22.3058197Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T21:24:22.3058201Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-09-29T21:24:22.3058205Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058251Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058253Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058254Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058255Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058256Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058258Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058260Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058261Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058262Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058263Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058264Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058265Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058266Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058267Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058268Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058268Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058325Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058327Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058328Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058329Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058330Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058330Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058331Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058332Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058333Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058333Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058334Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058335Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058336Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058336Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058337Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058424Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058426Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058428Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058429Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058430Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058431Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058431Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058432Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058433Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058434Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058450Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058452Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-09-29T21:24:22.3058453Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('129b951c-6265-4991-9b5b-7395b4466500', NULL, '2025-09-29T21:24:22.3057895Z', NULL, NULL, NULL, NULL, NULL, '2025-09-29T21:24:22.3057895Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('2ad60ad0-cf1f-4e43-8c50-311edab48fe2', NULL, '2025-09-29T21:24:22.3057923Z', NULL, NULL, NULL, NULL, NULL, '2025-09-29T21:24:22.3057922Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('5a2684bf-8a26-42ef-aba8-a936d8b60ffc', NULL, '2025-09-29T21:24:22.3057948Z', NULL, NULL, NULL, NULL, NULL, '2025-09-29T21:24:22.3057948Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('88bf987e-f61b-4d58-9b19-adb9a9dd5300', NULL, '2025-09-29T21:24:22.3057881Z', NULL, NULL, NULL, NULL, NULL, '2025-09-29T21:24:22.3057881Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('935ed42d-9039-47d0-bbb4-47268f96d740', NULL, '2025-09-29T21:24:22.3057864Z', NULL, NULL, NULL, NULL, NULL, '2025-09-29T21:24:22.3057859Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('9dafb854-65e7-401a-9c87-4eb7db52ae2b', NULL, '2025-09-29T21:24:22.3057908Z', NULL, NULL, NULL, NULL, NULL, '2025-09-29T21:24:22.3057908Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('a3f10821-aba0-4956-b29b-8fc07395e884', NULL, '2025-09-29T21:24:22.3057936Z', NULL, NULL, NULL, NULL, NULL, '2025-09-29T21:24:22.3057936Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-09-29T21:24:22.3057511Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250929212423_AddJwtTokenIdToUserSession', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '129b951c-6265-4991-9b5b-7395b4466500';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '2ad60ad0-cf1f-4e43-8c50-311edab48fe2';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '5a2684bf-8a26-42ef-aba8-a936d8b60ffc';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '88bf987e-f61b-4d58-9b19-adb9a9dd5300';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '935ed42d-9039-47d0-bbb4-47268f96d740';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '9dafb854-65e7-401a-9c87-4eb7db52ae2b';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'a3f10821-aba0-4956-b29b-8fc07395e884';
SELECT @@ROWCOUNT;

GO

CREATE TABLE [SecurityAlerts] (
    [Id] uniqueidentifier NOT NULL,
    [Type] nvarchar(50) NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Message] nvarchar(1000) NOT NULL,
    [Timestamp] datetime2 NOT NULL,
    [Dismissed] bit NOT NULL,
    [DismissedAt] datetime2 NULL,
    [DismissedBy] nvarchar(max) NULL,
    [Severity] int NOT NULL,
    [Category] nvarchar(50) NOT NULL,
    [Source] nvarchar(100) NOT NULL,
    [AffectedUser] nvarchar(100) NULL,
    [IpAddress] nvarchar(45) NULL,
    [Location] nvarchar(200) NULL,
    [Metadata] nvarchar(2048) NULL,
    [AffectedUserId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_SecurityAlerts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SecurityAlerts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SecurityAlerts_Users_AffectedUserId] FOREIGN KEY ([AffectedUserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
);
GO

CREATE TABLE [SecurityMetricsSet] (
    [Id] uniqueidentifier NOT NULL,
    [MetricDate] datetime2 NOT NULL,
    [TwoFactorAdoptionRate] int NOT NULL,
    [FailedLoginAttempts] int NOT NULL,
    [ActiveSessions] int NOT NULL,
    [SecurityIncidents] int NOT NULL,
    [PasswordCompliance] int NOT NULL,
    [AuditEventsToday] int NOT NULL,
    [TotalUsers] int NOT NULL,
    [UsersWithTwoFactorEnabled] int NOT NULL,
    [LockedAccounts] int NOT NULL,
    [PasswordExpiringSoon] int NOT NULL,
    [SuspiciousActivityCount] int NOT NULL,
    [TwoFactorAdoptionTrend] int NOT NULL,
    [FailedLoginAttemptsTrend] int NOT NULL,
    [ActiveSessionsTrend] int NOT NULL,
    [SecurityIncidentsTrend] int NOT NULL,
    [PasswordComplianceTrend] int NOT NULL,
    [AuditEventsTrend] int NOT NULL,
    [LastUpdated] datetime2 NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_SecurityMetricsSet] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SecurityMetricsSet_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
);
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-01T20:30:01.2864927Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-01T20:30:01.2864975Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-01T20:30:01.2864978Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-01T20:30:01.2864980Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:30:01.2865256Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:30:01.2865266Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:30:01.2865271Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:30:01.2865275Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:30:01.2865284Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:30:01.2865290Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:30:01.2865295Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:30:01.2865301Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:30:01.2865309Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:30:01.2865316Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:30:01.2865321Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:30:01.2865325Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:30:01.2865334Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:30:01.2865372Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:30:01.2865376Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:30:01.2865382Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865433Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865436Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865437Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865438Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865439Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865441Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865441Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865442Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865443Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865445Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865445Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865446Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865447Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865448Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865449Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865449Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865488Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865491Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865492Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865492Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865493Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865494Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865495Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865495Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865496Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865497Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865498Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865498Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865499Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865500Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865501Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865537Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865538Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865540Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865541Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865542Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865543Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865544Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865544Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865545Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865546Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865560Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865561Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:30:01.2865562Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('01914adb-e9de-498c-9c1d-9e35d84fdc76', NULL, '2025-10-01T20:30:01.2865124Z', NULL, NULL, NULL, NULL, NULL, '2025-10-01T20:30:01.2865123Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('16abadf8-d042-42ee-b052-f6d4e9662949', NULL, '2025-10-01T20:30:01.2865136Z', NULL, NULL, NULL, NULL, NULL, '2025-10-01T20:30:01.2865136Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('842a7e2d-fa5e-4d4e-9478-f3ccf8be7524', NULL, '2025-10-01T20:30:01.2865179Z', NULL, NULL, NULL, NULL, NULL, '2025-10-01T20:30:01.2865178Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('a5d24c7a-3e97-4da3-96f5-4af212fdc8ca', NULL, '2025-10-01T20:30:01.2865150Z', NULL, NULL, NULL, NULL, NULL, '2025-10-01T20:30:01.2865149Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('a9898a9e-38f3-4e88-881f-a6f7cf2d6035', NULL, '2025-10-01T20:30:01.2865109Z', NULL, NULL, NULL, NULL, NULL, '2025-10-01T20:30:01.2865108Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('b3a63f1b-5e50-41c1-af0d-99ec166ca7f3', NULL, '2025-10-01T20:30:01.2865091Z', NULL, NULL, NULL, NULL, NULL, '2025-10-01T20:30:01.2865087Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('db708e3a-c347-4fb3-93b1-47a148da6d4e', NULL, '2025-10-01T20:30:01.2865163Z', NULL, NULL, NULL, NULL, NULL, '2025-10-01T20:30:01.2865163Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-10-01T20:30:01.2864757Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_SecurityAlerts_AffectedUserId] ON [SecurityAlerts] ([AffectedUserId]);
GO

CREATE INDEX [IX_SecurityAlerts_Category] ON [SecurityAlerts] ([Category]);
GO

CREATE INDEX [IX_SecurityAlerts_Dismissed] ON [SecurityAlerts] ([Dismissed]);
GO

CREATE INDEX [IX_SecurityAlerts_TenantId] ON [SecurityAlerts] ([TenantId]);
GO

CREATE INDEX [IX_SecurityAlerts_Timestamp] ON [SecurityAlerts] ([Timestamp]);
GO

CREATE INDEX [IX_SecurityAlerts_Type] ON [SecurityAlerts] ([Type]);
GO

CREATE INDEX [IX_SecurityMetricsSet_MetricDate] ON [SecurityMetricsSet] ([MetricDate]);
GO

CREATE UNIQUE INDEX [IX_SecurityMetricsSet_TenantId_MetricDate] ON [SecurityMetricsSet] ([TenantId], [MetricDate]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251001203002_AddSecurityEntities', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '01914adb-e9de-498c-9c1d-9e35d84fdc76';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '16abadf8-d042-42ee-b052-f6d4e9662949';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '842a7e2d-fa5e-4d4e-9478-f3ccf8be7524';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'a5d24c7a-3e97-4da3-96f5-4af212fdc8ca';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'a9898a9e-38f3-4e88-881f-a6f7cf2d6035';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'b3a63f1b-5e50-41c1-af0d-99ec166ca7f3';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'db708e3a-c347-4fb3-93b1-47a148da6d4e';
SELECT @@ROWCOUNT;

GO

CREATE TABLE [SecurityPolicies] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [Type] int NOT NULL,
    [IsActive] bit NOT NULL,
    [IsEnforced] bit NOT NULL,
    [EffectiveFrom] datetime2 NULL,
    [EffectiveTo] datetime2 NULL,
    [PolicyRules] nvarchar(max) NOT NULL,
    [Exceptions] nvarchar(2000) NULL,
    [Priority] int NOT NULL,
    [LastEvaluated] datetime2 NOT NULL,
    [ViolationCount] int NOT NULL,
    [LastViolation] datetime2 NULL,
    [NotificationSettings] nvarchar(1000) NULL,
    [RequiresApproval] bit NOT NULL,
    [ApprovedBy] nvarchar(100) NULL,
    [ApprovedAt] datetime2 NULL,
    [ApprovalNotes] nvarchar(500) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_SecurityPolicies] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SecurityPolicies_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ThreatDetections] (
    [Id] uniqueidentifier NOT NULL,
    [ThreatType] nvarchar(100) NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NOT NULL,
    [Severity] int NOT NULL,
    [Status] int NOT NULL,
    [IpAddress] nvarchar(45) NULL,
    [UserAgent] nvarchar(200) NULL,
    [Location] nvarchar(200) NULL,
    [AffectedUserId] uniqueidentifier NULL,
    [AffectedUserName] nvarchar(100) NULL,
    [Metadata] nvarchar(2048) NULL,
    [DetectedAt] datetime2 NOT NULL,
    [ResolvedAt] datetime2 NULL,
    [Resolution] nvarchar(1000) NULL,
    [ResolvedBy] nvarchar(100) NULL,
    [RiskScore] int NOT NULL,
    [IsBlocked] bit NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_ThreatDetections] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ThreatDetections_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ThreatDetections_Users_AffectedUserId] FOREIGN KEY ([AffectedUserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
);
GO

CREATE TABLE [SecurityPolicyViolations] (
    [Id] uniqueidentifier NOT NULL,
    [SecurityPolicyId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NULL,
    [UserName] nvarchar(100) NULL,
    [ViolationType] nvarchar(100) NOT NULL,
    [Description] nvarchar(1000) NOT NULL,
    [Severity] int NOT NULL,
    [IpAddress] nvarchar(45) NULL,
    [UserAgent] nvarchar(200) NULL,
    [Location] nvarchar(200) NULL,
    [Metadata] nvarchar(2048) NULL,
    [DetectedAt] datetime2 NOT NULL,
    [IsResolved] bit NOT NULL,
    [ResolvedAt] datetime2 NULL,
    [ResolvedBy] nvarchar(100) NULL,
    [Resolution] nvarchar(1000) NULL,
    [ActionTaken] bit NOT NULL,
    [ActionDescription] nvarchar(500) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_SecurityPolicyViolations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SecurityPolicyViolations_SecurityPolicies_SecurityPolicyId] FOREIGN KEY ([SecurityPolicyId]) REFERENCES [SecurityPolicies] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SecurityPolicyViolations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]),
    CONSTRAINT [FK_SecurityPolicyViolations_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
);
GO

CREATE TABLE [ThreatIndicators] (
    [Id] uniqueidentifier NOT NULL,
    [IndicatorType] nvarchar(100) NOT NULL,
    [Value] nvarchar(500) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Confidence] int NOT NULL,
    [IsActive] bit NOT NULL,
    [FirstSeen] datetime2 NOT NULL,
    [LastSeen] datetime2 NOT NULL,
    [HitCount] int NOT NULL,
    [ThreatDetectionId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_ThreatIndicators] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ThreatIndicators_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]),
    CONSTRAINT [FK_ThreatIndicators_ThreatDetections_ThreatDetectionId] FOREIGN KEY ([ThreatDetectionId]) REFERENCES [ThreatDetections] ([Id]) ON DELETE CASCADE
);
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-01T20:42:40.8574424Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-01T20:42:40.8574490Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-01T20:42:40.8574564Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-01T20:42:40.8574568Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:42:40.8574959Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:42:40.8574986Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:42:40.8574997Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:42:40.8575003Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:42:40.8575017Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:42:40.8575027Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:42:40.8575033Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:42:40.8575039Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:42:40.8575051Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:42:40.8575061Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:42:40.8575070Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:42:40.8575192Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:42:40.8575203Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:42:40.8575210Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:42:40.8575227Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T20:42:40.8575233Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575353Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575356Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575358Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575359Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575360Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575363Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575364Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575366Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575367Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575369Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575370Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575371Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575372Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575373Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575374Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575375Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575459Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575462Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575463Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575465Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575466Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575467Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575468Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575469Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575470Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575471Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575472Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575473Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575474Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575475Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575476Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575539Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575542Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575544Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575545Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575546Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575547Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575548Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575550Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575550Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575551Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575573Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575574Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T20:42:40.8575575Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('3d728721-f194-4808-9ed6-85b87956c42b', NULL, '2025-10-01T20:42:40.8574804Z', NULL, NULL, NULL, NULL, NULL, '2025-10-01T20:42:40.8574803Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('67580196-366a-44e4-931c-c878e57856e7', NULL, '2025-10-01T20:42:40.8574707Z', NULL, NULL, NULL, NULL, NULL, '2025-10-01T20:42:40.8574707Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('8b46b14a-8c01-4360-b2ea-ca1cff471af5', NULL, '2025-10-01T20:42:40.8574746Z', NULL, NULL, NULL, NULL, NULL, '2025-10-01T20:42:40.8574746Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('90819eab-0679-4d35-8a8e-3e1d4c9624cb', NULL, '2025-10-01T20:42:40.8574728Z', NULL, NULL, NULL, NULL, NULL, '2025-10-01T20:42:40.8574727Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('aa2fb2cd-5ddb-4959-85f3-bd6f30553f12', NULL, '2025-10-01T20:42:40.8574785Z', NULL, NULL, NULL, NULL, NULL, '2025-10-01T20:42:40.8574785Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('d7aee52f-901c-4ade-884e-f3ee5eec9d3f', NULL, '2025-10-01T20:42:40.8574765Z', NULL, NULL, NULL, NULL, NULL, '2025-10-01T20:42:40.8574765Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('e2d325f2-fc40-47e2-9a37-4fd249d88430', NULL, '2025-10-01T20:42:40.8574676Z', NULL, NULL, NULL, NULL, NULL, '2025-10-01T20:42:40.8574671Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-10-01T20:42:40.8574145Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_SecurityPolicies_IsActive] ON [SecurityPolicies] ([IsActive]);
GO

CREATE INDEX [IX_SecurityPolicies_Name] ON [SecurityPolicies] ([Name]);
GO

CREATE INDEX [IX_SecurityPolicies_Priority] ON [SecurityPolicies] ([Priority]);
GO

CREATE INDEX [IX_SecurityPolicies_TenantId] ON [SecurityPolicies] ([TenantId]);
GO

CREATE INDEX [IX_SecurityPolicies_Type] ON [SecurityPolicies] ([Type]);
GO

CREATE INDEX [IX_SecurityPolicyViolations_DetectedAt] ON [SecurityPolicyViolations] ([DetectedAt]);
GO

CREATE INDEX [IX_SecurityPolicyViolations_IsResolved] ON [SecurityPolicyViolations] ([IsResolved]);
GO

CREATE INDEX [IX_SecurityPolicyViolations_SecurityPolicyId] ON [SecurityPolicyViolations] ([SecurityPolicyId]);
GO

CREATE INDEX [IX_SecurityPolicyViolations_Severity] ON [SecurityPolicyViolations] ([Severity]);
GO

CREATE INDEX [IX_SecurityPolicyViolations_TenantId] ON [SecurityPolicyViolations] ([TenantId]);
GO

CREATE INDEX [IX_SecurityPolicyViolations_UserId] ON [SecurityPolicyViolations] ([UserId]);
GO

CREATE INDEX [IX_SecurityPolicyViolations_ViolationType] ON [SecurityPolicyViolations] ([ViolationType]);
GO

CREATE INDEX [IX_ThreatDetections_AffectedUserId] ON [ThreatDetections] ([AffectedUserId]);
GO

CREATE INDEX [IX_ThreatDetections_DetectedAt] ON [ThreatDetections] ([DetectedAt]);
GO

CREATE INDEX [IX_ThreatDetections_IpAddress] ON [ThreatDetections] ([IpAddress]);
GO

CREATE INDEX [IX_ThreatDetections_Severity] ON [ThreatDetections] ([Severity]);
GO

CREATE INDEX [IX_ThreatDetections_Status] ON [ThreatDetections] ([Status]);
GO

CREATE INDEX [IX_ThreatDetections_TenantId] ON [ThreatDetections] ([TenantId]);
GO

CREATE INDEX [IX_ThreatDetections_ThreatType] ON [ThreatDetections] ([ThreatType]);
GO

CREATE INDEX [IX_ThreatIndicators_IndicatorType] ON [ThreatIndicators] ([IndicatorType]);
GO

CREATE INDEX [IX_ThreatIndicators_IsActive] ON [ThreatIndicators] ([IsActive]);
GO

CREATE INDEX [IX_ThreatIndicators_TenantId] ON [ThreatIndicators] ([TenantId]);
GO

CREATE INDEX [IX_ThreatIndicators_ThreatDetectionId] ON [ThreatIndicators] ([ThreatDetectionId]);
GO

CREATE INDEX [IX_ThreatIndicators_Value] ON [ThreatIndicators] ([Value]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251001204241_AddThreatDetectionAndSecurityPolicyEntities', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '3d728721-f194-4808-9ed6-85b87956c42b';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '67580196-366a-44e4-931c-c878e57856e7';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '8b46b14a-8c01-4360-b2ea-ca1cff471af5';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '90819eab-0679-4d35-8a8e-3e1d4c9624cb';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'aa2fb2cd-5ddb-4959-85f3-bd6f30553f12';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'd7aee52f-901c-4ade-884e-f3ee5eec9d3f';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'e2d325f2-fc40-47e2-9a37-4fd249d88430';
SELECT @@ROWCOUNT;

GO

ALTER TABLE [Users] ADD [AuthenticatorKey] nvarchar(32) NULL;
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-01T21:01:37.1766685Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-01T21:01:37.1766740Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-01T21:01:37.1766743Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-01T21:01:37.1766745Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T21:01:37.1767003Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T21:01:37.1767018Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T21:01:37.1767023Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T21:01:37.1767027Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T21:01:37.1767036Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T21:01:37.1767043Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T21:01:37.1767047Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T21:01:37.1767103Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T21:01:37.1767112Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T21:01:37.1767119Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T21:01:37.1767125Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T21:01:37.1767129Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T21:01:37.1767138Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T21:01:37.1767143Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T21:01:37.1767155Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-01T21:01:37.1767161Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767213Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767215Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767217Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767218Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767218Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767220Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767221Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767222Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767223Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767225Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767226Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767227Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767228Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767229Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767229Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767230Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767272Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767275Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767276Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767276Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767277Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767278Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767279Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767280Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767281Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767281Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767282Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767283Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767284Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767285Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767285Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767338Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767339Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767341Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767342Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767379Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767381Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767382Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767382Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767383Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767384Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767401Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767402Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-01T21:01:37.1767403Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('047b571c-3126-4e37-8940-165823043a55', NULL, '2025-10-01T21:01:37.1766918Z', NULL, NULL, NULL, NULL, NULL, '2025-10-01T21:01:37.1766917Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('197f57ee-b139-4ea0-8e55-73fbbeba2159', NULL, '2025-10-01T21:01:37.1766837Z', NULL, NULL, NULL, NULL, NULL, '2025-10-01T21:01:37.1766837Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('2c656462-da78-4758-9cf0-4ceadfe4bbd3', NULL, '2025-10-01T21:01:37.1766885Z', NULL, NULL, NULL, NULL, NULL, '2025-10-01T21:01:37.1766885Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('407c6f73-64ad-4aae-9c65-fc05b33ee0ae', NULL, '2025-10-01T21:01:37.1766901Z', NULL, NULL, NULL, NULL, NULL, '2025-10-01T21:01:37.1766901Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('4fc6625d-b7ae-4f0a-af46-6f37d99ee0a3', NULL, '2025-10-01T21:01:37.1766854Z', NULL, NULL, NULL, NULL, NULL, '2025-10-01T21:01:37.1766854Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('70d8bf36-fbf6-449a-a0e4-f379fad503a9', NULL, '2025-10-01T21:01:37.1766870Z', NULL, NULL, NULL, NULL, NULL, '2025-10-01T21:01:37.1766870Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('ff6747a2-bbfb-4ad7-a996-1e8997a3a633', NULL, '2025-10-01T21:01:37.1766816Z', NULL, NULL, NULL, NULL, NULL, '2025-10-01T21:01:37.1766812Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-10-01T21:01:37.1766515Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251001210138_AddAuthenticatorKeyToApplicationUser', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DROP TABLE [PasswordPolicies];
GO

DELETE FROM [TenantModules]
WHERE [Id] = '047b571c-3126-4e37-8940-165823043a55';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '197f57ee-b139-4ea0-8e55-73fbbeba2159';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '2c656462-da78-4758-9cf0-4ceadfe4bbd3';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '407c6f73-64ad-4aae-9c65-fc05b33ee0ae';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '4fc6625d-b7ae-4f0a-af46-6f37d99ee0a3';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '70d8bf36-fbf6-449a-a0e4-f379fad503a9';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'ff6747a2-bbfb-4ad7-a996-1e8997a3a633';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-02T01:49:05.9607535Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-02T01:49:05.9607592Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-02T01:49:05.9607596Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-02T01:49:05.9607599Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-02T01:49:05.9608007Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-02T01:49:05.9608023Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-02T01:49:05.9608030Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-02T01:49:05.9608035Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-02T01:49:05.9608054Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-02T01:49:05.9608062Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-02T01:49:05.9608068Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-02T01:49:05.9608073Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-02T01:49:05.9608086Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-02T01:49:05.9608095Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-02T01:49:05.9608100Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-02T01:49:05.9608105Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-02T01:49:05.9608116Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-02T01:49:05.9608136Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-02T01:49:05.9608141Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-02T01:49:05.9608193Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608259Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608263Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608264Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608265Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608266Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608269Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608270Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608271Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608272Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608275Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608276Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608277Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608278Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608279Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608280Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608281Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608345Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608348Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608350Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608351Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608352Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608353Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608354Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608354Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608355Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608356Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608357Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608358Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608359Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608360Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608361Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608409Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608411Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608413Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608415Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608415Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608416Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608417Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608418Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608419Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608420Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608440Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608442Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-02T01:49:05.9608443Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('3251b506-3326-4eb7-9191-3705d9c386a6', NULL, '2025-10-02T01:49:05.9607870Z', NULL, NULL, NULL, NULL, NULL, '2025-10-02T01:49:05.9607869Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('4037cf38-d531-41b7-994d-e421cd4e7cb7', NULL, '2025-10-02T01:49:05.9607809Z', NULL, NULL, NULL, NULL, NULL, '2025-10-02T01:49:05.9607809Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('4853a9eb-2bb6-4cff-a0b0-59734978268f', NULL, '2025-10-02T01:49:05.9607889Z', NULL, NULL, NULL, NULL, NULL, '2025-10-02T01:49:05.9607888Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('4c7bdb91-12a6-46f0-9482-c79bb7aa9c6f', NULL, '2025-10-02T01:49:05.9607905Z', NULL, NULL, NULL, NULL, NULL, '2025-10-02T01:49:05.9607905Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('90500ccf-bfaa-4f3b-b450-9eaf81122b23', NULL, '2025-10-02T01:49:05.9607829Z', NULL, NULL, NULL, NULL, NULL, '2025-10-02T01:49:05.9607828Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('af1d3f37-5744-4c3e-bbdb-b170ba6e018a', NULL, '2025-10-02T01:49:05.9607851Z', NULL, NULL, NULL, NULL, NULL, '2025-10-02T01:49:05.9607844Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('d3154180-6d26-4f60-b05e-6e00eb0e3548', NULL, '2025-10-02T01:49:05.9607781Z', NULL, NULL, NULL, NULL, NULL, '2025-10-02T01:49:05.9607774Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-10-02T01:49:05.9607199Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251002014907_DropPasswordPoliciesTable', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '3251b506-3326-4eb7-9191-3705d9c386a6';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '4037cf38-d531-41b7-994d-e421cd4e7cb7';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '4853a9eb-2bb6-4cff-a0b0-59734978268f';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '4c7bdb91-12a6-46f0-9482-c79bb7aa9c6f';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '90500ccf-bfaa-4f3b-b450-9eaf81122b23';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'af1d3f37-5744-4c3e-bbdb-b170ba6e018a';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'd3154180-6d26-4f60-b05e-6e00eb0e3548';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T01:37:52.2547881Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T01:37:52.2547908Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T01:37:52.2547910Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T01:37:52.2547912Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:37:52.2548138Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:37:52.2548145Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:37:52.2548148Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:37:52.2548151Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:37:52.2548158Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:37:52.2548162Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:37:52.2548165Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:37:52.2548168Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:37:52.2548176Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:37:52.2548181Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:37:52.2548185Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:37:52.2548188Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:37:52.2548194Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:37:52.2548197Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:37:52.2548200Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:37:52.2548202Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548234Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548235Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548236Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548237Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548262Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548264Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548264Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548265Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548266Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548267Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548268Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548268Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548269Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548270Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548270Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548271Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548296Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548297Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548298Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548298Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548299Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548299Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548300Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548300Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548301Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548301Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548302Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548302Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548303Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548303Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548304Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548336Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548337Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548338Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548339Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548339Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548340Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548340Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548341Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548341Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548342Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548354Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548354Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:37:52.2548355Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('01161dc9-56bf-4cc7-a73d-93910712d089', NULL, '2025-10-08T01:37:52.2547977Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T01:37:52.2547977Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('0eafc39c-2d6f-4de5-a312-a9210dd7fc02', NULL, '2025-10-08T01:37:52.2548065Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T01:37:52.2548065Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('315433c4-0dd6-4c62-8cb1-02573cf77774', NULL, '2025-10-08T01:37:52.2548075Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T01:37:52.2548074Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('63a3ef5d-b4a9-4dc3-b183-5afeaa928e42', NULL, '2025-10-08T01:37:52.2547967Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T01:37:52.2547967Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('9d95d543-1e7b-46c1-aae7-09063d382975', NULL, '2025-10-08T01:37:52.2548056Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T01:37:52.2548056Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('dda691ce-783a-436f-b8a0-485376bc53b5', NULL, '2025-10-08T01:37:52.2547955Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T01:37:52.2547953Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('fa728d45-d9c8-4d0f-b209-a56e878d9fc7', NULL, '2025-10-08T01:37:52.2548046Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T01:37:52.2548046Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-10-08T01:37:52.2547781Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251008013754_UpdateDatabase', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

                CREATE NONCLUSTERED INDEX [IX_AuditLogs_TenantId_Timestamp_Action] 
                ON [AuditLogs] ([TenantId] ASC, [Timestamp] DESC, [Action] ASC)
                INCLUDE ([UserId], [Username], [Resource], [ResourceId], [IpAddress])
GO

                CREATE NONCLUSTERED INDEX [IX_AuditLogs_UserId_Timestamp] 
                ON [AuditLogs] ([UserId] ASC, [Timestamp] DESC)
                INCLUDE ([Action], [Resource], [ResourceId])
GO

                CREATE NONCLUSTERED INDEX [IX_AuditLogs_Resource_Timestamp] 
                ON [AuditLogs] ([Resource] ASC, [Timestamp] DESC)
                INCLUDE ([UserId], [Username], [Action], [ResourceId])
GO

                CREATE NONCLUSTERED INDEX [IX_AuditLogs_Timestamp_TenantId] 
                ON [AuditLogs] ([Timestamp] DESC, [TenantId] ASC)
                INCLUDE ([UserId], [Username], [Action], [Resource])
GO

                CREATE NONCLUSTERED INDEX [IX_AuditLogs_IpAddress_Timestamp] 
                ON [AuditLogs] ([IpAddress] ASC, [Timestamp] DESC)
                INCLUDE ([UserId], [Action], [Resource])
GO

                CREATE NONCLUSTERED INDEX [IX_SecurityLogs_TenantId_Action_Timestamp] 
                ON [SecurityLogs] ([TenantId] ASC, [Action] ASC, [Timestamp] DESC)
                INCLUDE ([UserId], [Username], [IpAddress], [Success], [Details])
GO

                CREATE NONCLUSTERED INDEX [IX_SecurityLogs_Success_IpAddress_Timestamp] 
                ON [SecurityLogs] ([Success] ASC, [IpAddress] ASC, [Timestamp] DESC)
                INCLUDE ([Action], [Username], [Details])
GO

                CREATE NONCLUSTERED INDEX [IX_SecurityLogs_UserId_Timestamp] 
                ON [SecurityLogs] ([UserId] ASC, [Timestamp] DESC)
                INCLUDE ([Action], [Success], [IpAddress])
GO

                CREATE NONCLUSTERED INDEX [IX_Users_Email_IsActive] 
                ON [Users] ([Email] ASC, [IsActive] ASC)
                INCLUDE ([UserName], [TenantId], [FirstName], [LastName])
GO

                CREATE NONCLUSTERED INDEX [IX_Users_UserName_IsActive] 
                ON [Users] ([UserName] ASC, [IsActive] ASC)
                INCLUDE ([Email], [TenantId], [FirstName], [LastName])
GO

                CREATE NONCLUSTERED INDEX [IX_Users_TenantId_IsActive] 
                ON [Users] ([TenantId] ASC, [IsActive] ASC)
                INCLUDE ([UserName], [Email], [FirstName], [LastName], [LastLoginDate])
GO

                CREATE NONCLUSTERED INDEX [IX_Users_LastLoginDate_IsActive] 
                ON [Users] ([LastLoginDate] DESC, [IsActive] ASC)
                INCLUDE ([UserName], [Email], [TenantId])
GO

                CREATE NONCLUSTERED INDEX [IX_UserTenants_UserId_Status_ExpiresAt] 
                ON [UserTenants] ([UserId] ASC, [Status] ASC, [ExpiresAt] ASC)
                INCLUDE ([TenantId], [AccessLevel], [IsDefault], [GrantedAt])
GO

                CREATE NONCLUSTERED INDEX [IX_UserTenants_TenantId_Status_IsDeleted] 
                ON [UserTenants] ([TenantId] ASC, [Status] ASC, [IsDeleted] ASC)
                INCLUDE ([UserId], [AccessLevel], [IsDefault], [GrantedAt])
GO

                CREATE NONCLUSTERED INDEX [IX_UserTenants_UserId_IsDefault_Status] 
                ON [UserTenants] ([UserId] ASC, [IsDefault] ASC, [Status] ASC)
                INCLUDE ([TenantId], [AccessLevel])
GO

                CREATE NONCLUSTERED INDEX [IX_Tenants_Status_IsDeleted] 
                ON [Tenants] ([Status] ASC, [IsDeleted] ASC)
                INCLUDE ([Name], [Code], [Domain])
GO

                CREATE NONCLUSTERED INDEX [IX_Tenants_Domain_Status] 
                ON [Tenants] ([Domain] ASC, [Status] ASC)
                INCLUDE ([Name], [Code])
GO

                CREATE NONCLUSTERED INDEX [IX_Tenants_Code_Status] 
                ON [Tenants] ([Code] ASC, [Status] ASC)
                INCLUDE ([Name], [Domain])
GO

                CREATE NONCLUSTERED INDEX [IX_UserSessions_UserId_IsActive_LastActivityTime] 
                ON [UserSessions] ([UserId] ASC, [IsActive] ASC, [LastActivityTime] DESC)
                INCLUDE ([JwtTokenId], [IpAddress], [UserAgent], [LoginTime])
GO

                CREATE NONCLUSTERED INDEX [IX_UserSessions_LastActivityTime_IsActive] 
                ON [UserSessions] ([LastActivityTime] ASC, [IsActive] ASC)
                INCLUDE ([UserId], [JwtTokenId])
GO

                CREATE NONCLUSTERED INDEX [IX_UserSessions_JwtTokenId_IsActive] 
                ON [UserSessions] ([JwtTokenId] ASC, [IsActive] ASC)
                INCLUDE ([UserId], [LastActivityTime], [IpAddress])
GO

                CREATE NONCLUSTERED INDEX [IX_UserSessions_IpAddress_LoginTime] 
                ON [UserSessions] ([IpAddress] ASC, [LoginTime] DESC)
                INCLUDE ([UserId], [UserAgent], [IsActive])
GO

                CREATE NONCLUSTERED INDEX [IX_RefreshTokens_TokenHash_IsRevoked_ExpiresAt] 
                ON [RefreshTokens] ([TokenHash] ASC, [IsRevoked] ASC, [ExpiresAt] DESC)
                INCLUDE ([UserId], [UsageCount])
GO

                CREATE NONCLUSTERED INDEX [IX_RefreshTokens_UserId_ExpiresAt_IsRevoked] 
                ON [RefreshTokens] ([UserId] ASC, [ExpiresAt] DESC, [IsRevoked] ASC)
                INCLUDE ([TokenHash], [UsageCount])
GO

                CREATE NONCLUSTERED INDEX [IX_RefreshTokens_ExpiresAt_IsRevoked] 
                ON [RefreshTokens] ([ExpiresAt] ASC, [IsRevoked] ASC)
                INCLUDE ([UserId], [TokenHash])
GO

                CREATE NONCLUSTERED INDEX [IX_BlacklistedTokens_Jti_ExpiresAt] 
                ON [BlacklistedTokens] ([Jti] ASC, [ExpiresAt] DESC)
                INCLUDE ([Reason])
GO

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BlacklistedTokens_ExpiresAt' AND object_id = OBJECT_ID('BlacklistedTokens'))
                CREATE NONCLUSTERED INDEX [IX_BlacklistedTokens_ExpiresAt] 
                ON [BlacklistedTokens] ([ExpiresAt] ASC)
GO

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SystemSettings_TenantId_Key' AND object_id = OBJECT_ID('SystemSettings'))
                CREATE NONCLUSTERED INDEX [IX_SystemSettings_TenantId_Key] 
                ON [SystemSettings] ([TenantId] ASC, [Key] ASC)
                INCLUDE ([Value], [Description], [IsEncrypted])
GO

                CREATE NONCLUSTERED INDEX [IX_SystemSettings_TenantId_IsEncrypted] 
                ON [SystemSettings] ([TenantId] ASC, [IsEncrypted] ASC)
                INCLUDE ([Key], [Value], [Description])
GO

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmailTemplates_TenantId_Name' AND object_id = OBJECT_ID('EmailTemplates'))
                CREATE NONCLUSTERED INDEX [IX_EmailTemplates_TenantId_Name] 
                ON [EmailTemplates] ([TenantId] ASC, [Name] ASC)
                INCLUDE ([Subject], [HtmlBody], [IsActive])
GO

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmailTemplates_Module_Category_IsActive' AND object_id = OBJECT_ID('EmailTemplates'))
                CREATE NONCLUSTERED INDEX [IX_EmailTemplates_Module_Category_IsActive] 
                ON [EmailTemplates] ([Module] ASC, [Category] ASC, [IsActive] ASC)
                INCLUDE ([TenantId], [Name], [Subject])
GO

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RolePermissions_RoleId' AND object_id = OBJECT_ID('RolePermissions'))
                CREATE NONCLUSTERED INDEX [IX_RolePermissions_RoleId] 
                ON [RolePermissions] ([RoleId] ASC)
                INCLUDE ([PermissionId])
GO

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RolePermissions_PermissionId' AND object_id = OBJECT_ID('RolePermissions'))
                CREATE NONCLUSTERED INDEX [IX_RolePermissions_PermissionId] 
                ON [RolePermissions] ([PermissionId] ASC)
                INCLUDE ([RoleId])
GO

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserRoles_UserId' AND object_id = OBJECT_ID('UserRoles'))
                CREATE NONCLUSTERED INDEX [IX_UserRoles_UserId] 
                ON [UserRoles] ([UserId] ASC)
                INCLUDE ([RoleId])
GO

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserRoles_RoleId' AND object_id = OBJECT_ID('UserRoles'))
                CREATE NONCLUSTERED INDEX [IX_UserRoles_RoleId] 
                ON [UserRoles] ([RoleId] ASC)
                INCLUDE ([UserId])
GO

                CREATE NONCLUSTERED INDEX [IX_AuditLogs_CreatedAt_TenantId] 
                ON [AuditLogs] ([CreatedAt] ASC, [TenantId] ASC)
GO

                CREATE NONCLUSTERED INDEX [IX_SecurityLogs_CreatedAt_TenantId] 
                ON [SecurityLogs] ([CreatedAt] ASC, [TenantId] ASC)
GO

                CREATE NONCLUSTERED INDEX [IX_AuditLogs_TenantId_UserId_Timestamp] 
                ON [AuditLogs] ([TenantId] ASC, [UserId] ASC, [Timestamp] DESC)
                INCLUDE ([Action], [Resource])
GO

                CREATE NONCLUSTERED INDEX [IX_AuditLogs_TenantId_Resource_Action] 
                ON [AuditLogs] ([TenantId] ASC, [Resource] ASC, [Action] ASC)
                INCLUDE ([Timestamp], [UserId])
GO

                CREATE NONCLUSTERED INDEX [IX_SecurityLogs_TenantId_Timestamp_Action] 
                ON [SecurityLogs] ([TenantId] ASC, [Timestamp] DESC, [Action] ASC)
                INCLUDE ([Success], [IpAddress])
GO

DELETE FROM [TenantModules]
WHERE [Id] = '01161dc9-56bf-4cc7-a73d-93910712d089';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '0eafc39c-2d6f-4de5-a312-a9210dd7fc02';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '315433c4-0dd6-4c62-8cb1-02573cf77774';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '63a3ef5d-b4a9-4dc3-b183-5afeaa928e42';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '9d95d543-1e7b-46c1-aae7-09063d382975';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'dda691ce-783a-436f-b8a0-485376bc53b5';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'fa728d45-d9c8-4d0f-b209-a56e878d9fc7';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T01:41:15.8857358Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T01:41:15.8857391Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T01:41:15.8857442Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T01:41:15.8857444Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:41:15.8857604Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:41:15.8857612Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:41:15.8857616Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:41:15.8857619Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:41:15.8857628Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:41:15.8857632Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:41:15.8857635Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:41:15.8857638Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:41:15.8857645Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:41:15.8857652Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:41:15.8857655Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:41:15.8857658Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:41:15.8857697Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:41:15.8857701Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:41:15.8857703Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T01:41:15.8857706Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857741Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857746Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857747Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857748Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857749Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857750Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857751Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857752Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857752Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857753Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857754Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857755Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857756Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857756Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857757Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857757Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857782Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857784Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857784Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857785Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857786Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857786Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857787Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857787Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857788Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857788Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857789Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857790Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857790Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857791Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857791Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857820Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857822Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857823Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857823Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857824Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857825Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857825Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857826Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857826Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857827Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857837Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857839Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T01:41:15.8857840Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('300cf359-158b-43ff-8bc4-894112586878', NULL, '2025-10-08T01:41:15.8857487Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T01:41:15.8857484Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('3a4235d0-33ed-4b72-b360-77206198c1e2', NULL, '2025-10-08T01:41:15.8857545Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T01:41:15.8857545Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('6ea6924e-4945-421e-a152-877837aa0f84', NULL, '2025-10-08T01:41:15.8857526Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T01:41:15.8857526Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('795d9774-8433-44bd-9376-04be70e80e3b', NULL, '2025-10-08T01:41:15.8857537Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T01:41:15.8857536Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('c5e44028-33d7-4ce2-ba6a-3b919ab840f0', NULL, '2025-10-08T01:41:15.8857499Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T01:41:15.8857499Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('d8ed6a60-c1ec-4f5e-bd7e-40492f0ef242', NULL, '2025-10-08T01:41:15.8857518Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T01:41:15.8857517Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('e2f14b95-99e8-4be5-8fe5-56758b0d5fa3', NULL, '2025-10-08T01:41:15.8857509Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T01:41:15.8857508Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-10-08T01:41:15.8857243Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251008014116_ApplyCriticalPerformanceIndexes', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '300cf359-158b-43ff-8bc4-894112586878';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '3a4235d0-33ed-4b72-b360-77206198c1e2';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '6ea6924e-4945-421e-a152-877837aa0f84';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '795d9774-8433-44bd-9376-04be70e80e3b';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'c5e44028-33d7-4ce2-ba6a-3b919ab840f0';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'd8ed6a60-c1ec-4f5e-bd7e-40492f0ef242';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'e2f14b95-99e8-4be5-8fe5-56758b0d5fa3';
SELECT @@ROWCOUNT;

GO

CREATE TABLE [Reports] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NOT NULL,
    [Type] nvarchar(50) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [Query] nvarchar(max) NULL,
    [Parameters] nvarchar(max) NULL,
    [Columns] nvarchar(max) NULL,
    [Visualization] nvarchar(max) NULL,
    [Tags] nvarchar(max) NULL,
    [LastRun] datetime2 NULL,
    [NextRun] datetime2 NULL,
    [IsScheduled] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Reports] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Reports_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ReportTemplates] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NOT NULL,
    [Category] nvarchar(50) NOT NULL,
    [Type] nvarchar(20) NOT NULL,
    [ChartType] nvarchar(20) NULL,
    [IsCustom] bit NOT NULL,
    [LastUsed] datetime2 NULL,
    [UsageCount] int NOT NULL,
    [Tags] nvarchar(max) NULL,
    [PreviewImage] nvarchar(500) NULL,
    [Configuration] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ReportTemplates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReportTemplates_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ReportExecutions] (
    [Id] uniqueidentifier NOT NULL,
    [ReportId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [ExecutedAt] datetime2 NOT NULL,
    [ExecutionTime] time NOT NULL,
    [TotalRows] int NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [ErrorMessage] nvarchar(1000) NULL,
    [Parameters] nvarchar(max) NULL,
    [ResultMetadata] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ReportExecutions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReportExecutions_Reports_ReportId] FOREIGN KEY ([ReportId]) REFERENCES [Reports] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ReportExecutions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]),
    CONSTRAINT [FK_ReportExecutions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ReportExports] (
    [Id] uniqueidentifier NOT NULL,
    [ReportId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Format] nvarchar(10) NOT NULL,
    [FileName] nvarchar(500) NOT NULL,
    [FileSize] bigint NOT NULL,
    [ExportedAt] datetime2 NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [ErrorMessage] nvarchar(1000) NULL,
    [Parameters] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ReportExports] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReportExports_Reports_ReportId] FOREIGN KEY ([ReportId]) REFERENCES [Reports] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ReportExports_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]),
    CONSTRAINT [FK_ReportExports_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ReportSchedules] (
    [Id] uniqueidentifier NOT NULL,
    [ReportId] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Frequency] nvarchar(20) NOT NULL,
    [TimeOfDay] time NOT NULL,
    [DayOfWeek] int NULL,
    [DayOfMonth] int NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NULL,
    [NextExecutionDate] datetime2 NULL,
    [LastExecutionDate] datetime2 NULL,
    [EmailRecipients] nvarchar(max) NULL,
    [ExportFormat] nvarchar(10) NULL,
    [Parameters] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ReportSchedules] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReportSchedules_Reports_ReportId] FOREIGN KEY ([ReportId]) REFERENCES [Reports] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ReportSchedules_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id])
);
GO

CREATE TABLE [UserReportFavorites] (
    [ReportId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [FavoritedAt] datetime2 NOT NULL,
    [Id] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_UserReportFavorites] PRIMARY KEY ([ReportId], [UserId]),
    CONSTRAINT [FK_UserReportFavorites_Reports_ReportId] FOREIGN KEY ([ReportId]) REFERENCES [Reports] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserReportFavorites_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]),
    CONSTRAINT [FK_UserReportFavorites_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T02:38:17.8793234Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T02:38:17.8793271Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T02:38:17.8793273Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T02:38:17.8793274Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T02:38:17.8793442Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T02:38:17.8793451Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T02:38:17.8793455Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T02:38:17.8793458Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T02:38:17.8793472Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T02:38:17.8793477Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T02:38:17.8793480Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T02:38:17.8793488Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T02:38:17.8793494Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T02:38:17.8793552Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T02:38:17.8793556Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T02:38:17.8793559Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T02:38:17.8793565Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T02:38:17.8793569Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T02:38:17.8793572Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T02:38:17.8793575Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793611Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793613Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793614Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793614Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793615Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793616Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793617Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793618Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793619Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793620Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793620Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793621Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793622Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793622Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793623Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793623Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793654Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793656Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793656Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793657Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793657Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793658Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793658Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793659Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793660Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793660Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793661Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793661Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793662Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793662Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793663Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793695Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793696Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793697Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793698Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793698Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793699Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793699Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793700Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793700Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793701Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793713Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793714Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T02:38:17.8793766Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('177ec3dd-eca6-46d0-95a8-8b0c97f8f375', NULL, '2025-10-08T02:38:17.8793361Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T02:38:17.8793360Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('1d840ba8-7745-4225-918b-cd1b7f763bfc', NULL, '2025-10-08T02:38:17.8793341Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T02:38:17.8793341Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('2bb288bf-2f59-4eec-9e96-657f21e101cc', NULL, '2025-10-08T02:38:17.8793351Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T02:38:17.8793351Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('3581bed0-497f-46e0-9166-f169e3bdaea1', NULL, '2025-10-08T02:38:17.8793381Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T02:38:17.8793380Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('45be6e66-80aa-4a76-aff7-8f6f955d6cf6', NULL, '2025-10-08T02:38:17.8793317Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T02:38:17.8793313Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('4646a9bc-c4c4-4e6d-80d0-96ec88190c07', NULL, '2025-10-08T02:38:17.8793330Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T02:38:17.8793330Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('fcc91443-df84-4f6d-a0f8-02b95b4b7c5f', NULL, '2025-10-08T02:38:17.8793371Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T02:38:17.8793371Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-10-08T02:38:17.8793014Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_ReportExecutions_ExecutedAt] ON [ReportExecutions] ([ExecutedAt]);
GO

CREATE INDEX [IX_ReportExecutions_ReportId] ON [ReportExecutions] ([ReportId]);
GO

CREATE INDEX [IX_ReportExecutions_Status] ON [ReportExecutions] ([Status]);
GO

CREATE INDEX [IX_ReportExecutions_TenantId] ON [ReportExecutions] ([TenantId]);
GO

CREATE INDEX [IX_ReportExecutions_UserId] ON [ReportExecutions] ([UserId]);
GO

CREATE INDEX [IX_ReportExports_ExportedAt] ON [ReportExports] ([ExportedAt]);
GO

CREATE INDEX [IX_ReportExports_Format] ON [ReportExports] ([Format]);
GO

CREATE INDEX [IX_ReportExports_ReportId] ON [ReportExports] ([ReportId]);
GO

CREATE INDEX [IX_ReportExports_TenantId] ON [ReportExports] ([TenantId]);
GO

CREATE INDEX [IX_ReportExports_UserId] ON [ReportExports] ([UserId]);
GO

CREATE INDEX [IX_Reports_IsScheduled] ON [Reports] ([IsScheduled]);
GO

CREATE INDEX [IX_Reports_LastRun] ON [Reports] ([LastRun]);
GO

CREATE INDEX [IX_Reports_Status] ON [Reports] ([Status]);
GO

CREATE INDEX [IX_Reports_TenantId] ON [Reports] ([TenantId]);
GO

CREATE INDEX [IX_Reports_Type] ON [Reports] ([Type]);
GO

CREATE INDEX [IX_ReportSchedules_Frequency] ON [ReportSchedules] ([Frequency]);
GO

CREATE INDEX [IX_ReportSchedules_IsActive] ON [ReportSchedules] ([IsActive]);
GO

CREATE INDEX [IX_ReportSchedules_NextExecutionDate] ON [ReportSchedules] ([NextExecutionDate]);
GO

CREATE INDEX [IX_ReportSchedules_ReportId] ON [ReportSchedules] ([ReportId]);
GO

CREATE INDEX [IX_ReportSchedules_TenantId] ON [ReportSchedules] ([TenantId]);
GO

CREATE INDEX [IX_ReportTemplates_Category] ON [ReportTemplates] ([Category]);
GO

CREATE INDEX [IX_ReportTemplates_TenantId] ON [ReportTemplates] ([TenantId]);
GO

CREATE INDEX [IX_ReportTemplates_Type] ON [ReportTemplates] ([Type]);
GO

CREATE INDEX [IX_ReportTemplates_UsageCount] ON [ReportTemplates] ([UsageCount]);
GO

CREATE INDEX [IX_UserReportFavorites_FavoritedAt] ON [UserReportFavorites] ([FavoritedAt]);
GO

CREATE INDEX [IX_UserReportFavorites_TenantId] ON [UserReportFavorites] ([TenantId]);
GO

CREATE INDEX [IX_UserReportFavorites_UserId] ON [UserReportFavorites] ([UserId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251008023818_AddReportingTables', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '177ec3dd-eca6-46d0-95a8-8b0c97f8f375';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '1d840ba8-7745-4225-918b-cd1b7f763bfc';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '2bb288bf-2f59-4eec-9e96-657f21e101cc';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '3581bed0-497f-46e0-9166-f169e3bdaea1';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '45be6e66-80aa-4a76-aff7-8f6f955d6cf6';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '4646a9bc-c4c4-4e6d-80d0-96ec88190c07';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'fcc91443-df84-4f6d-a0f8-02b95b4b7c5f';
SELECT @@ROWCOUNT;

GO

CREATE TABLE [DataSources] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Type] int NOT NULL,
    [Host] nvarchar(100) NULL,
    [Port] int NULL,
    [DatabaseName] nvarchar(200) NULL,
    [Username] nvarchar(200) NULL,
    [EncryptedPassword] nvarchar(1000) NULL,
    [AdditionalSettings] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [LastConnectionTest] datetime2 NULL,
    [LastConnectionSuccess] bit NULL,
    [LastConnectionError] nvarchar(1000) NULL,
    [LastUsed] datetime2 NULL,
    [UsageCount] int NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedByUserId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_DataSources] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DataSources_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [DataSourceUsageLogs] (
    [Id] uniqueidentifier NOT NULL,
    [DataSourceId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [AccessedAt] datetime2 NOT NULL,
    [OperationType] nvarchar(100) NULL,
    [ExecutionTime] time NULL,
    [QueryExecuted] nvarchar(2000) NULL,
    [RecordsReturned] int NULL,
    [Success] bit NOT NULL,
    [ErrorMessage] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_DataSourceUsageLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DataSourceUsageLogs_DataSources_DataSourceId] FOREIGN KEY ([DataSourceId]) REFERENCES [DataSources] ([Id]) ON DELETE CASCADE
);
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T03:30:50.7725281Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T03:30:50.7725332Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T03:30:50.7725334Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T03:30:50.7725336Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T03:30:50.7725692Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T03:30:50.7725768Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T03:30:50.7725774Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T03:30:50.7725784Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T03:30:50.7725793Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T03:30:50.7725806Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T03:30:50.7725811Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T03:30:50.7725817Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T03:30:50.7725826Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T03:30:50.7725834Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T03:30:50.7725840Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T03:30:50.7725845Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T03:30:50.7725853Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T03:30:50.7725858Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T03:30:50.7725869Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T03:30:50.7725874Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7725919Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7725921Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7725922Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7725923Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7725923Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7725925Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7725926Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7725926Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7725927Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7725928Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7725929Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7725930Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7725930Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7725931Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7725932Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7725932Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7725979Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7725981Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7725982Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7725983Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7725983Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7726027Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7726028Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7726029Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7726029Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7726030Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7726031Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7726031Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7726032Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7726033Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7726033Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7726161Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7726163Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7726167Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7726168Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7726169Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7726169Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7726170Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7726171Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7726171Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7726172Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7726192Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7726193Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T03:30:50.7726194Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('24ba4dff-6e1d-4d8c-8c48-9418623192e9', NULL, '2025-10-08T03:30:50.7725613Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T03:30:50.7725612Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('362e283e-77ae-4fc2-8944-da2277cc3ef6', NULL, '2025-10-08T03:30:50.7725548Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T03:30:50.7725548Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('7583e5a4-c339-4754-92bc-e5104221e664', NULL, '2025-10-08T03:30:50.7725601Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T03:30:50.7725600Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('85837a5e-79bd-46dd-af8d-b53270d8b279', NULL, '2025-10-08T03:30:50.7725588Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T03:30:50.7725587Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('be0fd170-67a9-461b-904f-32d3735d68f6', NULL, '2025-10-08T03:30:50.7725574Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T03:30:50.7725574Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('c3bf06b5-8f92-4f97-9d5b-ada7235494f9', NULL, '2025-10-08T03:30:50.7725519Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T03:30:50.7725517Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('d5a224dd-7b8f-4b82-986f-fc59f3fd976d', NULL, '2025-10-08T03:30:50.7725562Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T03:30:50.7725562Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-10-08T03:30:50.7725093Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_DataSources_CreatedAt] ON [DataSources] ([CreatedAt]);
GO

CREATE INDEX [IX_DataSources_CreatedByUserId] ON [DataSources] ([CreatedByUserId]);
GO

CREATE INDEX [IX_DataSources_IsActive] ON [DataSources] ([IsActive]);
GO

CREATE INDEX [IX_DataSources_LastUsed] ON [DataSources] ([LastUsed]);
GO

CREATE INDEX [IX_DataSources_Name] ON [DataSources] ([Name]);
GO

CREATE INDEX [IX_DataSources_TenantId] ON [DataSources] ([TenantId]);
GO

CREATE INDEX [IX_DataSources_Type] ON [DataSources] ([Type]);
GO

CREATE INDEX [IX_DataSourceUsageLogs_AccessedAt] ON [DataSourceUsageLogs] ([AccessedAt]);
GO

CREATE INDEX [IX_DataSourceUsageLogs_DataSourceId] ON [DataSourceUsageLogs] ([DataSourceId]);
GO

CREATE INDEX [IX_DataSourceUsageLogs_OperationType] ON [DataSourceUsageLogs] ([OperationType]);
GO

CREATE INDEX [IX_DataSourceUsageLogs_Success] ON [DataSourceUsageLogs] ([Success]);
GO

CREATE INDEX [IX_DataSourceUsageLogs_TenantId] ON [DataSourceUsageLogs] ([TenantId]);
GO

CREATE INDEX [IX_DataSourceUsageLogs_UserId] ON [DataSourceUsageLogs] ([UserId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251008033051_AddDataSourceEntities', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [DataSources] DROP CONSTRAINT [FK_DataSources_Users_CreatedByUserId];
GO

ALTER TABLE [DataSourceUsageLogs] DROP CONSTRAINT [FK_DataSourceUsageLogs_DataSources_DataSourceId];
GO

ALTER TABLE [DataSourceUsageLogs] DROP CONSTRAINT [PK_DataSourceUsageLogs];
GO

ALTER TABLE [DataSources] DROP CONSTRAINT [PK_DataSources];
GO

DELETE FROM [TenantModules]
WHERE [Id] = '24ba4dff-6e1d-4d8c-8c48-9418623192e9';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '362e283e-77ae-4fc2-8944-da2277cc3ef6';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '7583e5a4-c339-4754-92bc-e5104221e664';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '85837a5e-79bd-46dd-af8d-b53270d8b279';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'be0fd170-67a9-461b-904f-32d3735d68f6';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'c3bf06b5-8f92-4f97-9d5b-ada7235494f9';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'd5a224dd-7b8f-4b82-986f-fc59f3fd976d';
SELECT @@ROWCOUNT;

GO

EXEC sp_rename N'[DataSourceUsageLogs]', N'ReportDataSourceUsageLogs';
GO

EXEC sp_rename N'[DataSources]', N'ReportDataSources';
GO

EXEC sp_rename N'[ReportDataSourceUsageLogs].[IX_DataSourceUsageLogs_UserId]', N'IX_ReportDataSourceUsageLogs_UserId', N'INDEX';
GO

EXEC sp_rename N'[ReportDataSourceUsageLogs].[IX_DataSourceUsageLogs_TenantId]', N'IX_ReportDataSourceUsageLogs_TenantId', N'INDEX';
GO

EXEC sp_rename N'[ReportDataSourceUsageLogs].[IX_DataSourceUsageLogs_Success]', N'IX_ReportDataSourceUsageLogs_Success', N'INDEX';
GO

EXEC sp_rename N'[ReportDataSourceUsageLogs].[IX_DataSourceUsageLogs_OperationType]', N'IX_ReportDataSourceUsageLogs_OperationType', N'INDEX';
GO

EXEC sp_rename N'[ReportDataSourceUsageLogs].[IX_DataSourceUsageLogs_DataSourceId]', N'IX_ReportDataSourceUsageLogs_DataSourceId', N'INDEX';
GO

EXEC sp_rename N'[ReportDataSourceUsageLogs].[IX_DataSourceUsageLogs_AccessedAt]', N'IX_ReportDataSourceUsageLogs_AccessedAt', N'INDEX';
GO

EXEC sp_rename N'[ReportDataSources].[IX_DataSources_Type]', N'IX_ReportDataSources_Type', N'INDEX';
GO

EXEC sp_rename N'[ReportDataSources].[IX_DataSources_TenantId]', N'IX_ReportDataSources_TenantId', N'INDEX';
GO

EXEC sp_rename N'[ReportDataSources].[IX_DataSources_Name]', N'IX_ReportDataSources_Name', N'INDEX';
GO

EXEC sp_rename N'[ReportDataSources].[IX_DataSources_LastUsed]', N'IX_ReportDataSources_LastUsed', N'INDEX';
GO

EXEC sp_rename N'[ReportDataSources].[IX_DataSources_IsActive]', N'IX_ReportDataSources_IsActive', N'INDEX';
GO

EXEC sp_rename N'[ReportDataSources].[IX_DataSources_CreatedByUserId]', N'IX_ReportDataSources_CreatedByUserId', N'INDEX';
GO

EXEC sp_rename N'[ReportDataSources].[IX_DataSources_CreatedAt]', N'IX_ReportDataSources_CreatedAt', N'INDEX';
GO

ALTER TABLE [ReportDataSourceUsageLogs] ADD CONSTRAINT [PK_ReportDataSourceUsageLogs] PRIMARY KEY ([Id]);
GO

ALTER TABLE [ReportDataSources] ADD CONSTRAINT [PK_ReportDataSources] PRIMARY KEY ([Id]);
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T04:09:28.2537558Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T04:09:28.2537592Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T04:09:28.2537594Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T04:09:28.2537595Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T04:09:28.2537767Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T04:09:28.2537774Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T04:09:28.2537806Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T04:09:28.2537811Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T04:09:28.2537824Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T04:09:28.2537829Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T04:09:28.2537832Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T04:09:28.2537838Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T04:09:28.2537845Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T04:09:28.2537852Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T04:09:28.2537856Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T04:09:28.2537860Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T04:09:28.2537866Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T04:09:28.2537869Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T04:09:28.2537872Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T04:09:28.2537874Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537907Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537909Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537910Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537910Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537911Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537913Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537913Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537914Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537915Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537916Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537916Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537917Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537917Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537918Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537919Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537919Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537946Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537948Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537949Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537949Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537950Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537950Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537951Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537952Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537952Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537953Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537953Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537954Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537978Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537979Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2537979Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2538008Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2538009Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2538010Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2538011Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2538011Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2538012Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2538013Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2538013Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2538014Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2538014Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2538026Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2538027Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T04:09:28.2538027Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('30a3e6c3-87ed-46e7-a197-6ec766bc99d0', NULL, '2025-10-08T04:09:28.2537709Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T04:09:28.2537708Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('445c5e8b-44e6-420d-b9ef-43ab9768b10f', NULL, '2025-10-08T04:09:28.2537655Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T04:09:28.2537654Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('49ab0eaf-963c-4e4c-a837-9a5a7bb90c2d', NULL, '2025-10-08T04:09:28.2537641Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T04:09:28.2537639Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('53aaeb39-7aff-4874-8283-2aff8ccf32ed', NULL, '2025-10-08T04:09:28.2537689Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T04:09:28.2537688Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('78fa0448-3fc8-48bb-9f9b-ed667d0bcd5c', NULL, '2025-10-08T04:09:28.2537678Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T04:09:28.2537678Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('b6914bd5-8e5a-4d15-94a0-a7f3b9246838', NULL, '2025-10-08T04:09:28.2537700Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T04:09:28.2537699Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('c748e9f7-570b-43f4-a006-14cc95c134c0', NULL, '2025-10-08T04:09:28.2537667Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T04:09:28.2537667Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-10-08T04:09:28.2537419Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

ALTER TABLE [ReportDataSources] ADD CONSTRAINT [FK_ReportDataSources_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [ReportDataSourceUsageLogs] ADD CONSTRAINT [FK_ReportDataSourceUsageLogs_ReportDataSources_DataSourceId] FOREIGN KEY ([DataSourceId]) REFERENCES [ReportDataSources] ([Id]) ON DELETE CASCADE;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251008040928_RenameDataSourcesToReportDataSources', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '30a3e6c3-87ed-46e7-a197-6ec766bc99d0';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '445c5e8b-44e6-420d-b9ef-43ab9768b10f';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '49ab0eaf-963c-4e4c-a837-9a5a7bb90c2d';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '53aaeb39-7aff-4874-8283-2aff8ccf32ed';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '78fa0448-3fc8-48bb-9f9b-ed667d0bcd5c';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'b6914bd5-8e5a-4d15-94a0-a7f3b9246838';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'c748e9f7-570b-43f4-a006-14cc95c134c0';
SELECT @@ROWCOUNT;

GO

CREATE TABLE [ReportRoleAssignments] (
    [Id] uniqueidentifier NOT NULL,
    [ReportId] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    [CanRead] bit NOT NULL,
    [CanExecute] bit NOT NULL,
    [CanExport] bit NOT NULL,
    [CanEdit] bit NOT NULL,
    [CanSchedule] bit NOT NULL,
    [AssignedAt] datetime2 NOT NULL,
    [AssignedBy] nvarchar(200) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ReportRoleAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReportRoleAssignments_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ReportRoleAssignments_Reports_ReportId] FOREIGN KEY ([ReportId]) REFERENCES [Reports] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ReportRoleAssignments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id])
);
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T20:23:29.7975077Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T20:23:29.7975114Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T20:23:29.7975116Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T20:23:29.7975118Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T20:23:29.7975316Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T20:23:29.7975326Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T20:23:29.7975332Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T20:23:29.7975338Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T20:23:29.7975346Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T20:23:29.7975352Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T20:23:29.7975357Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T20:23:29.7975362Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T20:23:29.7975370Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T20:23:29.7975377Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T20:23:29.7975383Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T20:23:29.7975396Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T20:23:29.7975404Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T20:23:29.7975417Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T20:23:29.7975422Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T20:23:29.7975432Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975465Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975466Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975467Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975468Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975469Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975471Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975471Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975472Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975473Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975474Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975475Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975475Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975476Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975477Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975477Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975478Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975507Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975509Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975510Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975511Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975511Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975512Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975513Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975514Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975514Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975515Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975515Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975516Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975517Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975518Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975518Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975546Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975547Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975549Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975549Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975550Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975551Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975551Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975552Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975553Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975553Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975564Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975565Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T20:23:29.7975566Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('18ac5fa6-113f-40ec-96d0-4cdd48ad7c18', NULL, '2025-10-08T20:23:29.7975191Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T20:23:29.7975191Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('83b4d0fe-a076-4600-8917-51fe15b0552a', NULL, '2025-10-08T20:23:29.7975235Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T20:23:29.7975235Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('890f2658-1bfe-487c-a43a-7fd560554a45', NULL, '2025-10-08T20:23:29.7975203Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T20:23:29.7975203Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('9c85e375-fc8b-40b7-9291-cb64d8d519d4', NULL, '2025-10-08T20:23:29.7975214Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T20:23:29.7975214Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('b8c15fcc-d6fb-4871-a984-25b00f8d4503', NULL, '2025-10-08T20:23:29.7975171Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T20:23:29.7975168Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('cc562f95-7632-4c56-bcfe-64876f25f3db', NULL, '2025-10-08T20:23:29.7975224Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T20:23:29.7975224Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('d13ce4ad-3633-48fb-b388-fab96692edfc', NULL, '2025-10-08T20:23:29.7975246Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T20:23:29.7975245Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-10-08T20:23:29.7974973Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_ReportRoleAssignments_AssignedAt] ON [ReportRoleAssignments] ([AssignedAt]);
GO

CREATE INDEX [IX_ReportRoleAssignments_ReportId] ON [ReportRoleAssignments] ([ReportId]);
GO

CREATE UNIQUE INDEX [IX_ReportRoleAssignments_ReportId_RoleId] ON [ReportRoleAssignments] ([ReportId], [RoleId]);
GO

CREATE INDEX [IX_ReportRoleAssignments_RoleId] ON [ReportRoleAssignments] ([RoleId]);
GO

CREATE INDEX [IX_ReportRoleAssignments_TenantId] ON [ReportRoleAssignments] ([TenantId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251008202332_AddReportRoleAssignment', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '18ac5fa6-113f-40ec-96d0-4cdd48ad7c18';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '83b4d0fe-a076-4600-8917-51fe15b0552a';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '890f2658-1bfe-487c-a43a-7fd560554a45';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '9c85e375-fc8b-40b7-9291-cb64d8d519d4';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'b8c15fcc-d6fb-4871-a984-25b00f8d4503';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'cc562f95-7632-4c56-bcfe-64876f25f3db';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'd13ce4ad-3633-48fb-b388-fab96692edfc';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T22:48:11.9716045Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T22:48:11.9716086Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T22:48:11.9716088Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T22:48:11.9716090Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:48:11.9716397Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:48:11.9716404Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:48:11.9716410Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:48:11.9716413Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:48:11.9716420Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:48:11.9716424Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:48:11.9716428Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:48:11.9716431Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:48:11.9716438Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:48:11.9716443Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:48:11.9716448Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:48:11.9716451Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:48:11.9716457Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:48:11.9716461Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:48:11.9716463Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:48:11.9716466Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716506Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716508Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716508Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716509Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716516Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716517Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716518Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716519Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716519Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716521Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716521Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716522Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716522Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716523Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716524Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716524Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716566Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716568Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716569Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716570Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716571Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716573Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716573Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716574Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716575Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716576Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716576Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716577Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716578Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716579Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716579Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716662Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716663Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716664Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716665Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716666Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716666Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716667Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716667Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716668Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716669Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716683Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716684Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:48:11.9716685Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('08f34d57-8a41-4f6e-a243-332e5592635a', NULL, '2025-10-08T22:48:11.9716320Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T22:48:11.9716320Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('0f439efc-2e6e-4cb7-8b2d-8051ccd74b10', NULL, '2025-10-08T22:48:11.9716171Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T22:48:11.9716170Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('26cc9720-3e49-441a-9b71-92bd3b5eb8ff', NULL, '2025-10-08T22:48:11.9716141Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T22:48:11.9716138Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('4feef84a-a6ff-4a41-8896-213860af15fb', NULL, '2025-10-08T22:48:11.9716160Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T22:48:11.9716160Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('5fda67ad-10b4-4143-9fd9-eb5cada2a427', NULL, '2025-10-08T22:48:11.9716308Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T22:48:11.9716307Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('c803f35a-b5e0-4831-8991-b20fb07f2835', NULL, '2025-10-08T22:48:11.9716330Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T22:48:11.9716329Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('efcd8d7e-055c-462b-98d6-5e6657ddf274', NULL, '2025-10-08T22:48:11.9716294Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T22:48:11.9716293Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-10-08T22:48:11.9715825Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251008224812_AddModuleIdToReports', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '08f34d57-8a41-4f6e-a243-332e5592635a';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '0f439efc-2e6e-4cb7-8b2d-8051ccd74b10';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '26cc9720-3e49-441a-9b71-92bd3b5eb8ff';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '4feef84a-a6ff-4a41-8896-213860af15fb';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '5fda67ad-10b4-4143-9fd9-eb5cada2a427';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'c803f35a-b5e0-4831-8991-b20fb07f2835';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'efcd8d7e-055c-462b-98d6-5e6657ddf274';
SELECT @@ROWCOUNT;

GO

ALTER TABLE [Reports] ADD [ModuleId] uniqueidentifier NULL;
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T22:50:52.4857367Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T22:50:52.4857399Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T22:50:52.4857401Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-08T22:50:52.4857402Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:50:52.4857564Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:50:52.4857572Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:50:52.4857575Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:50:52.4857578Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:50:52.4857586Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:50:52.4857591Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:50:52.4857594Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:50:52.4857635Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:50:52.4857641Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:50:52.4857647Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:50:52.4857650Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:50:52.4857653Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:50:52.4857660Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:50:52.4857663Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:50:52.4857666Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-08T22:50:52.4857669Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857702Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857708Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857708Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857709Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857710Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857711Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857712Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857713Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857713Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857714Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857715Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857715Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857716Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857717Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857717Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857718Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857744Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857746Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857746Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857747Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857748Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857748Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857749Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857749Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857750Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857750Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857751Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857751Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857752Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857752Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857753Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857791Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857792Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857809Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857810Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857811Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857811Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857812Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857812Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857813Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857814Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857824Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857825Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-08T22:50:52.4857825Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('373f98d9-f7f9-4f5c-ac18-ad5a2c0b5bc8', NULL, '2025-10-08T22:50:52.4857506Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T22:50:52.4857506Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('465cd212-b089-4e3f-90bb-6b520f0b765b', NULL, '2025-10-08T22:50:52.4857497Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T22:50:52.4857497Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('ec91dbc4-54d4-494c-8437-3742a899c097', NULL, '2025-10-08T22:50:52.4857479Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T22:50:52.4857479Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('f0f54f0d-81c1-4450-8f87-06d829faaab4', NULL, '2025-10-08T22:50:52.4857459Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T22:50:52.4857458Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('f22f6bfb-aa6f-4450-add9-a8e55904eade', NULL, '2025-10-08T22:50:52.4857447Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T22:50:52.4857444Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('f2f2d485-fd35-4a64-a9a3-11da759feb49', NULL, '2025-10-08T22:50:52.4857468Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T22:50:52.4857468Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('f8fce369-9a14-4b75-9cdf-e51417fa4df8', NULL, '2025-10-08T22:50:52.4857487Z', NULL, NULL, NULL, NULL, NULL, '2025-10-08T22:50:52.4857487Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-10-08T22:50:52.4857229Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_Reports_ModuleId] ON [Reports] ([ModuleId]);
GO

ALTER TABLE [Reports] ADD CONSTRAINT [FK_Reports_TenantModules_ModuleId] FOREIGN KEY ([ModuleId]) REFERENCES [TenantModules] ([Id]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251008225053_AddReportModuleRelationship', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [EmailTemplates] DROP CONSTRAINT [FK_EmailTemplates_Tenants_TenantId];
GO

ALTER TABLE [ReportDataSources] DROP CONSTRAINT [FK_ReportDataSources_Users_CreatedByUserId];
GO

ALTER TABLE [ReportExecutions] DROP CONSTRAINT [FK_ReportExecutions_Tenants_TenantId];
GO

ALTER TABLE [ReportExecutions] DROP CONSTRAINT [FK_ReportExecutions_Users_UserId];
GO

ALTER TABLE [ReportExports] DROP CONSTRAINT [FK_ReportExports_Tenants_TenantId];
GO

ALTER TABLE [ReportExports] DROP CONSTRAINT [FK_ReportExports_Users_UserId];
GO

ALTER TABLE [ReportRoleAssignments] DROP CONSTRAINT [FK_ReportRoleAssignments_Tenants_TenantId];
GO

ALTER TABLE [Reports] DROP CONSTRAINT [FK_Reports_TenantModules_ModuleId];
GO

ALTER TABLE [Reports] DROP CONSTRAINT [FK_Reports_Tenants_TenantId];
GO

ALTER TABLE [ReportSchedules] DROP CONSTRAINT [FK_ReportSchedules_Tenants_TenantId];
GO

ALTER TABLE [ReportTemplates] DROP CONSTRAINT [FK_ReportTemplates_Tenants_TenantId];
GO

ALTER TABLE [SecurityAlerts] DROP CONSTRAINT [FK_SecurityAlerts_Tenants_TenantId];
GO

ALTER TABLE [SecurityAlerts] DROP CONSTRAINT [FK_SecurityAlerts_Users_AffectedUserId];
GO

ALTER TABLE [SecurityMetricsSet] DROP CONSTRAINT [FK_SecurityMetricsSet_Tenants_TenantId];
GO

ALTER TABLE [SecurityPolicyViolations] DROP CONSTRAINT [FK_SecurityPolicyViolations_Users_UserId];
GO

ALTER TABLE [ThreatDetections] DROP CONSTRAINT [FK_ThreatDetections_Users_AffectedUserId];
GO

ALTER TABLE [UserReportFavorites] DROP CONSTRAINT [FK_UserReportFavorites_Tenants_TenantId];
GO

DELETE FROM [TenantModules]
WHERE [Id] = '373f98d9-f7f9-4f5c-ac18-ad5a2c0b5bc8';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '465cd212-b089-4e3f-90bb-6b520f0b765b';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'ec91dbc4-54d4-494c-8437-3742a899c097';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'f0f54f0d-81c1-4450-8f87-06d829faaab4';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'f22f6bfb-aa6f-4450-add9-a8e55904eade';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'f2f2d485-fd35-4a64-a9a3-11da759feb49';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'f8fce369-9a14-4b75-9cdf-e51417fa4df8';
SELECT @@ROWCOUNT;

GO

CREATE TABLE [AssetTypes] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Description] nvarchar(500) NULL,
    [Color] nvarchar(7) NULL,
    [Icon] nvarchar(50) NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [RequiresLocation] bit NOT NULL,
    [RequiresOperatingHours] bit NOT NULL,
    [RequiresMileageTracking] bit NOT NULL,
    [RequiresLicensing] bit NOT NULL,
    [RequiresInspections] bit NOT NULL,
    [SupportsHierarchy] bit NOT NULL,
    [RequiresSpecializedFields] bit NOT NULL,
    [DefaultMaintenanceIntervalDays] int NOT NULL DEFAULT 30,
    [RequiresPreventiveMaintenance] bit NOT NULL,
    [RequiresConditionMonitoring] bit NOT NULL,
    [RequiresSafetyChecks] bit NOT NULL,
    [RequiresLockoutTagout] bit NOT NULL,
    [RequiresPermits] bit NOT NULL,
    [DefaultWorkOrderPriority] int NOT NULL,
    [DefaultEstimatedHours] float NOT NULL,
    [DefaultWorkInstructions] nvarchar(2000) NULL,
    [CustomFieldsConfig] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [UpdatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AssetTypes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssetTypes_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Countries] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(3) NOT NULL,
    [Alpha2Code] nvarchar(2) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Countries] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Countries_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [InspectionTemplates] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Category] nvarchar(100) NULL,
    [InspectionType] nvarchar(50) NOT NULL,
    [IsActive] bit NOT NULL,
    [ChecklistItems] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_InspectionTemplates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InspectionTemplates_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [InventoryCategories] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [ParentCategoryId] uniqueidentifier NULL,
    [Color] nvarchar(7) NULL,
    [Icon] nvarchar(50) NULL,
    [IsActive] bit NOT NULL,
    [DefaultUnitOfMeasure] nvarchar(20) NULL,
    [DefaultSerialTracking] bit NOT NULL,
    [DefaultLotTracking] bit NOT NULL,
    [DefaultRequiresInspection] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_InventoryCategories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InventoryCategories_InventoryCategories_ParentCategoryId] FOREIGN KEY ([ParentCategoryId]) REFERENCES [InventoryCategories] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InventoryCategories_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [MaintenanceAssetCategories] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Code] nvarchar(50) NULL,
    [Color] nvarchar(7) NULL,
    [Icon] nvarchar(50) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MaintenanceAssetCategories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceAssetCategories_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [MaintenanceTypes] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Category] nvarchar(20) NOT NULL,
    [Color] nvarchar(7) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MaintenanceTypes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceTypes_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [PriorityLevels] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(50) NOT NULL,
    [Level] int NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Color] nvarchar(7) NULL,
    [ResponseTimeHours] int NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PriorityLevels] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PriorityLevels_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Shifts] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [StartTime] time NOT NULL,
    [EndTime] time NOT NULL,
    [IsActive] bit NOT NULL,
    [Color] nvarchar(7) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Shifts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Shifts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Skill] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Category] nvarchar(100) NULL,
    [IsActive] bit NOT NULL,
    [RequiresCertification] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Skill] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Skill_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [TechnicianShifts] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [StartTime] time NOT NULL,
    [EndTime] time NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Color] nvarchar(7) NULL,
    [IsActive] bit NOT NULL,
    [DaysOfWeek] nvarchar(20) NOT NULL,
    [ScheduledHours] float NOT NULL,
    [BreakMinutes] float NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TechnicianShifts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TechnicianShifts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [TechnicianSkills] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Category] nvarchar(100) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TechnicianSkills] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TechnicianSkills_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Warehouses] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Address] nvarchar(500) NULL,
    [City] nvarchar(100) NULL,
    [State] nvarchar(50) NULL,
    [ZipCode] nvarchar(20) NULL,
    [Country] nvarchar(50) NULL,
    [IsActive] bit NOT NULL,
    [IsDefault] bit NOT NULL,
    [WarehouseType] nvarchar(20) NOT NULL,
    [ContactPerson] nvarchar(100) NULL,
    [Phone] nvarchar(50) NULL,
    [Email] nvarchar(100) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Warehouses] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Warehouses_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [WorkOrderTypes] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Color] nvarchar(7) NULL,
    [Icon] nvarchar(50) NULL,
    [IsActive] bit NOT NULL,
    [RequiresApproval] bit NOT NULL,
    [DefaultPriority] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkOrderTypes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderTypes_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [AssetTypeFields] (
    [Id] uniqueidentifier NOT NULL,
    [AssetTypeId] uniqueidentifier NOT NULL,
    [FieldName] nvarchar(100) NOT NULL,
    [DisplayName] nvarchar(150) NOT NULL,
    [FieldType] nvarchar(50) NOT NULL,
    [IsRequired] bit NOT NULL DEFAULT CAST(0 AS bit),
    [DisplayOrder] int NOT NULL,
    [ValidationRules] nvarchar(max) NULL,
    [Options] nvarchar(max) NULL,
    [DefaultValue] nvarchar(500) NULL,
    [HelpText] nvarchar(500) NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AssetTypeFields] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssetTypeFields_AssetTypes_AssetTypeId] FOREIGN KEY ([AssetTypeId]) REFERENCES [AssetTypes] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AssetTypeFields_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [InventoryItems] (
    [Id] uniqueidentifier NOT NULL,
    [ItemCode] nvarchar(100) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [CategoryId] uniqueidentifier NOT NULL,
    [Brand] nvarchar(50) NULL,
    [Manufacturer] nvarchar(50) NULL,
    [Model] nvarchar(100) NULL,
    [UnitOfMeasure] nvarchar(20) NOT NULL,
    [StandardCost] decimal(18,2) NOT NULL,
    [AverageCost] decimal(18,2) NOT NULL,
    [LastPurchaseCost] decimal(18,2) NOT NULL,
    [SalePrice] decimal(18,2) NOT NULL,
    [CurrentStock] decimal(18,4) NOT NULL,
    [AvailableStock] decimal(18,4) NOT NULL,
    [AllocatedStock] decimal(18,4) NOT NULL,
    [OnOrderStock] decimal(18,4) NOT NULL,
    [MinimumLevel] decimal(18,4) NOT NULL,
    [MaximumLevel] decimal(18,4) NOT NULL,
    [ReorderLevel] decimal(18,4) NOT NULL,
    [ReorderQuantity] decimal(18,4) NOT NULL,
    [SafetyStock] decimal(18,4) NOT NULL,
    [LeadTimeDays] int NOT NULL,
    [SafetyLeadTimeDays] int NOT NULL,
    [ItemType] int NOT NULL,
    [ABCClass] nvarchar(20) NOT NULL,
    [Status] int NOT NULL,
    [Weight] decimal(18,4) NULL,
    [Length] decimal(18,4) NULL,
    [Width] decimal(18,4) NULL,
    [Height] decimal(18,4) NULL,
    [Volume] decimal(18,4) NULL,
    [IsSerialTracked] bit NOT NULL,
    [IsLotTracked] bit NOT NULL,
    [IsExpirationTracked] bit NOT NULL,
    [IsLocationTracked] bit NOT NULL,
    [RequiresInspection] bit NOT NULL,
    [ShelfLifeDays] int NULL,
    [PrimarySupplier] nvarchar(200) NULL,
    [SupplierItemCode] nvarchar(100) NULL,
    [LastStockDate] datetime2 NULL,
    [LastPurchaseDate] datetime2 NULL,
    [LastSaleDate] datetime2 NULL,
    [LastCountDate] datetime2 NULL,
    [CustomFields] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_InventoryItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InventoryItems_InventoryCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [InventoryCategories] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InventoryItems_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [PurchaseOrders] (
    [Id] uniqueidentifier NOT NULL,
    [OrderNumber] nvarchar(50) NOT NULL,
    [SupplierName] nvarchar(200) NOT NULL,
    [SupplierAddress] nvarchar(500) NULL,
    [ContactPerson] nvarchar(100) NULL,
    [ContactPhone] nvarchar(50) NULL,
    [ContactEmail] nvarchar(100) NULL,
    [OrderDate] datetime2 NOT NULL,
    [RequiredDate] datetime2 NULL,
    [PromisedDate] datetime2 NULL,
    [ReceivedDate] datetime2 NULL,
    [Status] nvarchar(20) NOT NULL,
    [RequestedById] uniqueidentifier NULL,
    [ApprovedById] uniqueidentifier NULL,
    [ApprovedAt] datetime2 NULL,
    [SubTotal] decimal(18,2) NOT NULL,
    [TaxAmount] decimal(18,2) NOT NULL,
    [ShippingCost] decimal(18,2) NOT NULL,
    [DiscountAmount] decimal(18,2) NOT NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [PaymentTerms] nvarchar(100) NULL,
    [ShippingTerms] nvarchar(100) NULL,
    [Notes] nvarchar(2000) NULL,
    [DeliveryWarehouseId] uniqueidentifier NULL,
    [DeliveryAddress] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PurchaseOrders] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PurchaseOrders_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PurchaseOrders_Users_ApprovedById] FOREIGN KEY ([ApprovedById]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_PurchaseOrders_Users_RequestedById] FOREIGN KEY ([RequestedById]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_PurchaseOrders_Warehouses_DeliveryWarehouseId] FOREIGN KEY ([DeliveryWarehouseId]) REFERENCES [Warehouses] ([Id])
);
GO

CREATE TABLE [WarehouseLocations] (
    [Id] uniqueidentifier NOT NULL,
    [WarehouseId] uniqueidentifier NOT NULL,
    [LocationCode] nvarchar(100) NOT NULL,
    [Name] nvarchar(200) NULL,
    [Description] nvarchar(1000) NULL,
    [LocationType] nvarchar(50) NOT NULL,
    [ParentLocationId] uniqueidentifier NULL,
    [IsActive] bit NOT NULL,
    [IsPickingLocation] bit NOT NULL,
    [IsReceivingLocation] bit NOT NULL,
    [MaxWeight] decimal(18,4) NULL,
    [MaxVolume] decimal(18,4) NULL,
    [MaxItems] int NULL,
    [CurrentWeight] decimal(18,4) NOT NULL,
    [CurrentVolume] decimal(18,4) NOT NULL,
    [CurrentItemCount] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WarehouseLocations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WarehouseLocations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WarehouseLocations_WarehouseLocations_ParentLocationId] FOREIGN KEY ([ParentLocationId]) REFERENCES [WarehouseLocations] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WarehouseLocations_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [StockAdjustments] (
    [Id] uniqueidentifier NOT NULL,
    [AdjustmentNumber] nvarchar(50) NOT NULL,
    [AdjustmentDate] datetime2 NOT NULL,
    [ReasonCode] nvarchar(50) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Status] nvarchar(20) NOT NULL,
    [ApprovedById] uniqueidentifier NULL,
    [ApprovedAt] datetime2 NULL,
    [TotalAdjustmentValue] decimal(18,2) NOT NULL,
    [InventoryItemId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_StockAdjustments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StockAdjustments_InventoryItems_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [InventoryItems] ([Id]),
    CONSTRAINT [FK_StockAdjustments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StockAdjustments_Users_ApprovedById] FOREIGN KEY ([ApprovedById]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [PurchaseOrderItems] (
    [Id] uniqueidentifier NOT NULL,
    [PurchaseOrderId] uniqueidentifier NOT NULL,
    [InventoryItemId] uniqueidentifier NOT NULL,
    [SupplierItemCode] nvarchar(100) NULL,
    [OrderedQuantity] decimal(18,4) NOT NULL,
    [ReceivedQuantity] decimal(18,4) NOT NULL,
    [RemainingQuantity] decimal(18,4) NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [LineTotal] decimal(18,2) NOT NULL,
    [ExpectedDeliveryDate] datetime2 NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PurchaseOrderItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PurchaseOrderItems_InventoryItems_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [InventoryItems] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PurchaseOrderItems_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [PurchaseOrders] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PurchaseOrderItems_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [PurchaseOrderReceipts] (
    [Id] uniqueidentifier NOT NULL,
    [PurchaseOrderId] uniqueidentifier NOT NULL,
    [ReceiptNumber] nvarchar(50) NOT NULL,
    [ReceiptDate] datetime2 NOT NULL,
    [DeliveryNote] nvarchar(100) NULL,
    [CarrierName] nvarchar(100) NULL,
    [TrackingNumber] nvarchar(100) NULL,
    [ReceivedById] uniqueidentifier NULL,
    [InspectedById] uniqueidentifier NULL,
    [Status] nvarchar(20) NOT NULL,
    [Notes] nvarchar(2000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PurchaseOrderReceipts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PurchaseOrderReceipts_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [PurchaseOrders] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PurchaseOrderReceipts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PurchaseOrderReceipts_Users_InspectedById] FOREIGN KEY ([InspectedById]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_PurchaseOrderReceipts_Users_ReceivedById] FOREIGN KEY ([ReceivedById]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [InventoryAllocations] (
    [Id] uniqueidentifier NOT NULL,
    [InventoryItemId] uniqueidentifier NOT NULL,
    [LocationId] uniqueidentifier NOT NULL,
    [AllocationType] nvarchar(50) NOT NULL,
    [ReferenceNumber] nvarchar(50) NULL,
    [ReferenceId] uniqueidentifier NULL,
    [AllocatedQuantity] decimal(18,4) NOT NULL,
    [ConsumedQuantity] decimal(18,4) NOT NULL,
    [RemainingQuantity] decimal(18,4) NOT NULL,
    [AllocationDate] datetime2 NOT NULL,
    [RequiredDate] datetime2 NULL,
    [ExpirationDate] datetime2 NULL,
    [Status] nvarchar(20) NOT NULL,
    [SerialNumber] nvarchar(100) NULL,
    [LotNumber] nvarchar(100) NULL,
    [Notes] nvarchar(1000) NULL,
    [AllocatedById] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_InventoryAllocations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InventoryAllocations_InventoryItems_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [InventoryItems] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_InventoryAllocations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InventoryAllocations_Users_AllocatedById] FOREIGN KEY ([AllocatedById]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_InventoryAllocations_WarehouseLocations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [WarehouseLocations] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [InventoryLocations] (
    [Id] uniqueidentifier NOT NULL,
    [InventoryItemId] uniqueidentifier NOT NULL,
    [LocationId] uniqueidentifier NOT NULL,
    [Quantity] decimal(18,4) NOT NULL,
    [AllocatedQuantity] decimal(18,4) NOT NULL,
    [AvailableQuantity] decimal(18,4) NOT NULL,
    [AverageCost] decimal(18,2) NOT NULL,
    [LastMovementDate] datetime2 NULL,
    [LastCountDate] datetime2 NULL,
    [NextCountDate] datetime2 NULL,
    [CountFrequencyDays] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_InventoryLocations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InventoryLocations_InventoryItems_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [InventoryItems] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_InventoryLocations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InventoryLocations_WarehouseLocations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [WarehouseLocations] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [StockMovements] (
    [Id] uniqueidentifier NOT NULL,
    [InventoryItemId] uniqueidentifier NOT NULL,
    [MovementType] nvarchar(50) NOT NULL,
    [Quantity] decimal(18,4) NOT NULL,
    [UnitCost] decimal(18,2) NOT NULL,
    [TotalValue] decimal(18,2) NOT NULL,
    [MovementDate] datetime2 NOT NULL,
    [ReferenceType] int NOT NULL,
    [ReferenceNumber] nvarchar(50) NULL,
    [ReferenceId] uniqueidentifier NULL,
    [LocationId] uniqueidentifier NULL,
    [Notes] nvarchar(1000) NULL,
    [SerialNumber] nvarchar(100) NULL,
    [LotNumber] nvarchar(100) NULL,
    [ExpirationDate] datetime2 NULL,
    [RunningBalance] decimal(18,4) NOT NULL,
    [RunningValue] decimal(18,4) NOT NULL,
    [ProcessedById] uniqueidentifier NULL,
    [ApprovedById] uniqueidentifier NULL,
    [ApprovedAt] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_StockMovements] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StockMovements_InventoryItems_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [InventoryItems] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StockMovements_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StockMovements_Users_ApprovedById] FOREIGN KEY ([ApprovedById]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_StockMovements_Users_ProcessedById] FOREIGN KEY ([ProcessedById]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_StockMovements_WarehouseLocations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [WarehouseLocations] ([Id]) ON DELETE SET NULL
);
GO

CREATE TABLE [StockAdjustmentItems] (
    [Id] uniqueidentifier NOT NULL,
    [AdjustmentId] uniqueidentifier NOT NULL,
    [InventoryItemId] uniqueidentifier NOT NULL,
    [LocationId] uniqueidentifier NULL,
    [SerialNumber] nvarchar(100) NULL,
    [LotNumber] nvarchar(100) NULL,
    [SystemQuantity] decimal(18,4) NOT NULL,
    [PhysicalQuantity] decimal(18,4) NOT NULL,
    [AdjustmentQuantity] decimal(18,4) NOT NULL,
    [UnitCost] decimal(18,2) NOT NULL,
    [AdjustmentValue] decimal(18,4) NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_StockAdjustmentItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StockAdjustmentItems_InventoryItems_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [InventoryItems] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StockAdjustmentItems_StockAdjustments_AdjustmentId] FOREIGN KEY ([AdjustmentId]) REFERENCES [StockAdjustments] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_StockAdjustmentItems_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StockAdjustmentItems_WarehouseLocations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [WarehouseLocations] ([Id]) ON DELETE SET NULL
);
GO

CREATE TABLE [PurchaseOrderReceiptItems] (
    [Id] uniqueidentifier NOT NULL,
    [ReceiptId] uniqueidentifier NOT NULL,
    [PurchaseOrderItemId] uniqueidentifier NOT NULL,
    [ReceivedQuantity] decimal(18,4) NOT NULL,
    [AcceptedQuantity] decimal(18,4) NOT NULL,
    [RejectedQuantity] decimal(18,4) NOT NULL,
    [LocationId] uniqueidentifier NULL,
    [SerialNumber] nvarchar(100) NULL,
    [LotNumber] nvarchar(100) NULL,
    [ExpirationDate] datetime2 NULL,
    [Notes] nvarchar(1000) NULL,
    [RejectionReason] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PurchaseOrderReceiptItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PurchaseOrderReceiptItems_PurchaseOrderItems_PurchaseOrderItemId] FOREIGN KEY ([PurchaseOrderItemId]) REFERENCES [PurchaseOrderItems] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PurchaseOrderReceiptItems_PurchaseOrderReceipts_ReceiptId] FOREIGN KEY ([ReceiptId]) REFERENCES [PurchaseOrderReceipts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PurchaseOrderReceiptItems_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PurchaseOrderReceiptItems_WarehouseLocations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [WarehouseLocations] ([Id]) ON DELETE SET NULL
);
GO

CREATE TABLE [AssetDowntimes] (
    [Id] uniqueidentifier NOT NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NULL,
    [StartTime] datetime2 NOT NULL,
    [EndTime] datetime2 NULL,
    [DowntimeHours] float NULL,
    [Reason] nvarchar(50) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [EstimatedCostImpact] decimal(18,2) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AssetDowntimes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssetDowntimes_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [AssetInspections] (
    [Id] uniqueidentifier NOT NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [InspectionTemplateId] uniqueidentifier NOT NULL,
    [InspectorId] uniqueidentifier NOT NULL,
    [InspectionDate] datetime2 NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [OverallResult] nvarchar(20) NULL,
    [InspectionData] nvarchar(max) NOT NULL,
    [Notes] nvarchar(2000) NULL,
    [RecommendedActions] nvarchar(2000) NULL,
    [NextInspectionDue] datetime2 NULL,
    [IsRegulatoryRequired] bit NOT NULL,
    [RegulatoryStandard] nvarchar(100) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AssetInspections] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssetInspections_InspectionTemplates_InspectionTemplateId] FOREIGN KEY ([InspectionTemplateId]) REFERENCES [InspectionTemplates] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AssetInspections_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [InspectionDocuments] (
    [Id] uniqueidentifier NOT NULL,
    [InspectionId] uniqueidentifier NOT NULL,
    [FileName] nvarchar(200) NOT NULL,
    [FilePath] nvarchar(500) NOT NULL,
    [FileType] nvarchar(50) NULL,
    [FileSize] bigint NOT NULL,
    [DocumentType] nvarchar(50) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_InspectionDocuments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InspectionDocuments_AssetInspections_InspectionId] FOREIGN KEY ([InspectionId]) REFERENCES [AssetInspections] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_InspectionDocuments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [AttendanceRecords] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Date] date NOT NULL,
    [CheckInTime] time NULL,
    [CheckOutTime] time NULL,
    [WorkedHours] float NULL,
    [OvertimeHours] float NULL,
    [Status] nvarchar(50) NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AttendanceRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AttendanceRecords_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Departments] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(20) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [DepartmentType] int NOT NULL,
    [ParentDepartmentId] uniqueidentifier NULL,
    [DepartmentHeadId] uniqueidentifier NULL,
    [Budget] decimal(18,4) NULL,
    [IsActive] bit NOT NULL,
    [Color] nvarchar(7) NULL,
    [Icon] nvarchar(50) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Departments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Departments_Departments_ParentDepartmentId] FOREIGN KEY ([ParentDepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Departments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [EmployeePositions] (
    [Id] uniqueidentifier NOT NULL,
    [Title] nvarchar(100) NOT NULL,
    [Code] nvarchar(20) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [DepartmentId] uniqueidentifier NOT NULL,
    [Level] int NOT NULL,
    [MinSalary] decimal(18,2) NULL,
    [MaxSalary] decimal(18,2) NULL,
    [RequiresCertification] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [Responsibilities] nvarchar(2000) NULL,
    [Requirements] nvarchar(2000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeePositions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeePositions_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EmployeePositions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [WorkStations] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(20) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Location] nvarchar(500) NULL,
    [DepartmentId] uniqueidentifier NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkStations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkStations_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]),
    CONSTRAINT [FK_WorkStations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [PositionSkillRequirement] (
    [Id] uniqueidentifier NOT NULL,
    [PositionId] uniqueidentifier NOT NULL,
    [SkillId] uniqueidentifier NOT NULL,
    [RequiredLevel] int NOT NULL,
    [IsRequired] bit NOT NULL,
    [Priority] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PositionSkillRequirement] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PositionSkillRequirement_EmployeePositions_PositionId] FOREIGN KEY ([PositionId]) REFERENCES [EmployeePositions] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PositionSkillRequirement_Skill_SkillId] FOREIGN KEY ([SkillId]) REFERENCES [Skill] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PositionSkillRequirement_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [EmployeeBiometrics] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [BiometricType] nvarchar(50) NOT NULL,
    [BiometricData] nvarchar(500) NOT NULL,
    [DeviceId] nvarchar(100) NULL,
    [EnrolledDate] datetime2 NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeBiometrics] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeBiometrics_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [EmployeeContractDetails] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [ContractNumber] nvarchar(50) NOT NULL,
    [ContractType] int NOT NULL,
    [StartDate] date NOT NULL,
    [EndDate] date NULL,
    [Salary] decimal(18,2) NOT NULL,
    [PayFrequency] nvarchar(50) NOT NULL,
    [WorkingHoursPerWeek] int NOT NULL,
    [VacationDaysPerYear] int NOT NULL,
    [SickDaysPerYear] int NOT NULL,
    [Terms] nvarchar(1000) NULL,
    [IsActive] bit NOT NULL,
    [ContractPath] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeContractDetails] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeContractDetails_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [EmployeeDependents] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [FirstName] nvarchar(100) NOT NULL,
    [LastName] nvarchar(100) NULL,
    [Relationship] nvarchar(50) NOT NULL,
    [DateOfBirth] date NULL,
    [Gender] int NULL,
    [Occupation] nvarchar(100) NULL,
    [IsStudentDependent] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeDependents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeDependents_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [EmployeeEmergencyContacts] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [FirstName] nvarchar(100) NOT NULL,
    [LastName] nvarchar(100) NULL,
    [Relationship] nvarchar(50) NOT NULL,
    [PhoneNumber] nvarchar(50) NOT NULL,
    [AlternatePhoneNumber] nvarchar(50) NULL,
    [EmailAddress] nvarchar(200) NULL,
    [Address] nvarchar(500) NULL,
    [IsPrimary] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeEmergencyContacts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeEmergencyContacts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [EmployeeIdentificationCards] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [DocumentType] nvarchar(100) NOT NULL,
    [DocumentNumber] nvarchar(100) NOT NULL,
    [IssueDate] date NULL,
    [ExpiryDate] date NULL,
    [IssuingAuthority] nvarchar(200) NULL,
    [DocumentPath] nvarchar(500) NULL,
    [IsVerified] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeIdentificationCards] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeIdentificationCards_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [EmployeeQualifications] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [QualificationName] nvarchar(200) NOT NULL,
    [Institution] nvarchar(200) NOT NULL,
    [FieldOfStudy] nvarchar(100) NULL,
    [StartDate] date NULL,
    [CompletionDate] date NULL,
    [Grade] nvarchar(50) NULL,
    [Description] nvarchar(500) NULL,
    [IsVerified] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeQualifications] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeQualifications_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Employees] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeNumber] nvarchar(50) NOT NULL,
    [CorporateEmployeeID] nvarchar(50) NULL,
    [FirstName] nvarchar(100) NOT NULL,
    [MiddleName] nvarchar(100) NULL,
    [LastName] nvarchar(100) NOT NULL,
    [Title] nvarchar(100) NULL,
    [Gender] int NULL,
    [DateOfBirth] date NULL,
    [MaritalStatus] int NULL,
    [Religion] nvarchar(50) NULL,
    [IsFullTime] bit NOT NULL,
    [DateEmployed] date NULL,
    [Address] nvarchar(500) NULL,
    [City] nvarchar(100) NULL,
    [State] nvarchar(50) NULL,
    [PostalCode] nvarchar(20) NULL,
    [CountryId] uniqueidentifier NULL,
    [EmailAddress] nvarchar(200) NOT NULL,
    [TelephoneNumber] nvarchar(50) NULL,
    [BusinessNumber] nvarchar(50) NULL,
    [MobileNumber] nvarchar(50) NULL,
    [Extension] nvarchar(20) NULL,
    [ContractType] int NOT NULL,
    [ProbationPeriodDays] int NOT NULL,
    [ConfirmationDate] date NULL,
    [RetirementDate] date NULL,
    [PicturePath] nvarchar(500) NULL,
    [DepartmentId] uniqueidentifier NOT NULL,
    [SectionId] uniqueidentifier NULL,
    [PositionId] uniqueidentifier NOT NULL,
    [StaffStatus] int NOT NULL,
    [StationId] uniqueidentifier NULL,
    [TaxNumber] nvarchar(50) NULL,
    [BloodType] int NULL,
    [IsActive] bit NOT NULL,
    [ShiftId] uniqueidentifier NULL,
    [ManagerId] uniqueidentifier NULL,
    [Salary] decimal(18,2) NULL,
    [BadgeNumber] nvarchar(50) NULL,
    [LastPromotionDate] datetime2 NULL,
    [LastReviewDate] datetime2 NULL,
    [NextReviewDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Employees] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Employees_Countries_CountryId] FOREIGN KEY ([CountryId]) REFERENCES [Countries] ([Id]),
    CONSTRAINT [FK_Employees_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Employees_EmployeePositions_PositionId] FOREIGN KEY ([PositionId]) REFERENCES [EmployeePositions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Employees_Employees_ManagerId] FOREIGN KEY ([ManagerId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Employees_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id]),
    CONSTRAINT [FK_Employees_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Employees_WorkStations_StationId] FOREIGN KEY ([StationId]) REFERENCES [WorkStations] ([Id])
);
GO

CREATE TABLE [EmployeeShiftPreferences] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [ShiftId] uniqueidentifier NOT NULL,
    [PreferenceLevel] int NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeShiftPreferences] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeShiftPreferences_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EmployeeShiftPreferences_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EmployeeShiftPreferences_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [EmployeeSkills] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [SkillId] uniqueidentifier NOT NULL,
    [SkillLevel] int NOT NULL,
    [AcquiredDate] date NULL,
    [CertificationDate] date NULL,
    [CertificationExpiryDate] date NULL,
    [CertificationNumber] nvarchar(200) NULL,
    [CertifyingBody] nvarchar(200) NULL,
    [Notes] nvarchar(1000) NULL,
    [IsVerified] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeSkills] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeSkills_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EmployeeSkills_Skill_SkillId] FOREIGN KEY ([SkillId]) REFERENCES [Skill] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EmployeeSkills_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [EmployeeWorkHistories] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [CompanyName] nvarchar(200) NOT NULL,
    [JobTitle] nvarchar(100) NOT NULL,
    [JobDescription] nvarchar(1000) NULL,
    [StartDate] date NOT NULL,
    [EndDate] date NULL,
    [Salary] decimal(18,2) NULL,
    [ReasonForLeaving] nvarchar(1000) NULL,
    [SupervisorName] nvarchar(200) NULL,
    [SupervisorPhone] nvarchar(50) NULL,
    [CanContact] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeWorkHistories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeWorkHistories_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EmployeeWorkHistories_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [MaintenanceAssets] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [AssetNumber] nvarchar(50) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [AssetCategoryId] uniqueidentifier NOT NULL,
    [AssetTypeId] uniqueidentifier NOT NULL,
    [Manufacturer] nvarchar(100) NULL,
    [Model] nvarchar(100) NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [SerialNumber] nvarchar(50) NULL,
    [PurchaseDate] datetime2 NULL,
    [PurchasePrice] decimal(18,2) NULL,
    [CurrentValue] decimal(18,4) NULL,
    [Location] nvarchar(500) NULL,
    [Building] nvarchar(100) NULL,
    [Floor] nvarchar(100) NULL,
    [Room] nvarchar(100) NULL,
    [Status] int NOT NULL,
    [Criticality] int NOT NULL,
    [WarrantyStartDate] datetime2 NULL,
    [WarrantyEndDate] datetime2 NULL,
    [WarrantyProvider] nvarchar(200) NULL,
    [ParentAssetId] uniqueidentifier NULL,
    [Specifications] nvarchar(max) NULL,
    [DocumentLinks] nvarchar(max) NULL,
    [Images] nvarchar(max) NULL,
    [OperatingHours] float NULL,
    [LastOperatingHoursUpdate] datetime2 NULL,
    [Mileage] float NULL,
    [LastMileageUpdate] datetime2 NULL,
    [LicensePlate] nvarchar(50) NULL,
    [VIN] nvarchar(50) NULL,
    [LastServiceDate] datetime2 NULL,
    [NextServiceDue] datetime2 NULL,
    [FloorArea] float NULL,
    [EnergyRating] nvarchar(50) NULL,
    [Capacity] float NULL,
    [CapacityUnit] nvarchar(50) NULL,
    [PowerRating] float NULL,
    [PowerUnit] nvarchar(20) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MaintenanceAssets] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceAssets_AssetTypes_AssetTypeId] FOREIGN KEY ([AssetTypeId]) REFERENCES [AssetTypes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_MaintenanceAssets_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_MaintenanceAssets_MaintenanceAssetCategories_AssetCategoryId] FOREIGN KEY ([AssetCategoryId]) REFERENCES [MaintenanceAssetCategories] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_MaintenanceAssets_MaintenanceAssets_ParentAssetId] FOREIGN KEY ([ParentAssetId]) REFERENCES [MaintenanceAssets] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_MaintenanceAssets_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Sections] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(20) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [DepartmentId] uniqueidentifier NOT NULL,
    [SectionHeadId] uniqueidentifier NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Sections] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Sections_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Sections_Employees_SectionHeadId] FOREIGN KEY ([SectionHeadId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_Sections_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ShiftAssignments] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [ShiftId] uniqueidentifier NOT NULL,
    [StartDate] date NOT NULL,
    [EndDate] date NULL,
    [IsActive] bit NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ShiftAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ShiftAssignments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ShiftAssignments_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ShiftAssignments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [TechnicianAvailabilities] (
    [Id] uniqueidentifier NOT NULL,
    [TechnicianId] uniqueidentifier NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [AvailabilityType] nvarchar(20) NOT NULL,
    [Reason] nvarchar(50) NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [IsRecurring] bit NOT NULL,
    [RecurrencePattern] nvarchar(100) NULL,
    [AvailableHours] float NULL,
    [CapacityPercentage] float NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TechnicianAvailabilities] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TechnicianAvailabilities_Employees_TechnicianId] FOREIGN KEY ([TechnicianId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TechnicianAvailabilities_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [TechnicianTeams] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [TeamLeaderId] uniqueidentifier NULL,
    [Status] nvarchar(20) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TechnicianTeams] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TechnicianTeams_Employees_TeamLeaderId] FOREIGN KEY ([TeamLeaderId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_TechnicianTeams_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [UserTechnicianSkills] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [SkillId] uniqueidentifier NOT NULL,
    [ProficiencyLevel] int NOT NULL,
    [CertificationDate] datetime2 NULL,
    [CertificationExpiry] datetime2 NULL,
    [CertifyingBody] nvarchar(200) NULL,
    [CertificationNumber] nvarchar(100) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_UserTechnicianSkills] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserTechnicianSkills_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserTechnicianSkills_TechnicianSkills_SkillId] FOREIGN KEY ([SkillId]) REFERENCES [TechnicianSkills] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserTechnicianSkills_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [MaintenanceSchedules] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [MaintenanceTypeId] uniqueidentifier NOT NULL,
    [ScheduleType] nvarchar(20) NOT NULL,
    [Frequency] nvarchar(20) NOT NULL,
    [IntervalValue] int NOT NULL,
    [PreferredTime] time NULL,
    [DayOfWeek] int NULL,
    [DayOfMonth] int NULL,
    [UsageInterval] float NULL,
    [UsageUnit] nvarchar(20) NULL,
    [ConditionParameters] nvarchar(max) NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NULL,
    [LastGeneratedDate] datetime2 NULL,
    [NextDueDate] datetime2 NULL,
    [LeadTimeDays] int NOT NULL,
    [IsActive] bit NOT NULL,
    [EstimatedHours] float NOT NULL,
    [EstimatedCost] decimal(18,2) NOT NULL,
    [WorkOrderTitle] nvarchar(200) NULL,
    [WorkOrderDescription] nvarchar(2000) NULL,
    [DefaultTechnicianId] uniqueidentifier NULL,
    [DefaultTeamId] uniqueidentifier NULL,
    [PriorityLevelId] uniqueidentifier NULL,
    [TaskTemplate] nvarchar(max) NULL,
    [PartsTemplate] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MaintenanceSchedules] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceSchedules_Employees_DefaultTechnicianId] FOREIGN KEY ([DefaultTechnicianId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_MaintenanceSchedules_MaintenanceAssets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [MaintenanceAssets] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_MaintenanceSchedules_MaintenanceTypes_MaintenanceTypeId] FOREIGN KEY ([MaintenanceTypeId]) REFERENCES [MaintenanceTypes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_MaintenanceSchedules_PriorityLevels_PriorityLevelId] FOREIGN KEY ([PriorityLevelId]) REFERENCES [PriorityLevels] ([Id]),
    CONSTRAINT [FK_MaintenanceSchedules_TechnicianTeams_DefaultTeamId] FOREIGN KEY ([DefaultTeamId]) REFERENCES [TechnicianTeams] ([Id]),
    CONSTRAINT [FK_MaintenanceSchedules_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [TechnicianTeamMembers] (
    [Id] uniqueidentifier NOT NULL,
    [TeamId] uniqueidentifier NOT NULL,
    [TechnicianId] uniqueidentifier NOT NULL,
    [Role] nvarchar(50) NOT NULL,
    [JoinedDate] datetime2 NOT NULL,
    [LeftDate] datetime2 NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TechnicianTeamMembers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TechnicianTeamMembers_Employees_TechnicianId] FOREIGN KEY ([TechnicianId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TechnicianTeamMembers_TechnicianTeams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [TechnicianTeams] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TechnicianTeamMembers_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [WorkOrders] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderNumber] nvarchar(50) NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(2000) NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [WorkOrderTypeId] uniqueidentifier NOT NULL,
    [MaintenanceTypeId] uniqueidentifier NOT NULL,
    [PriorityLevelId] uniqueidentifier NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [AssignedTechnicianId] uniqueidentifier NULL,
    [AssignedTeamId] uniqueidentifier NULL,
    [RequestedStartDate] datetime2 NULL,
    [RequestedCompletionDate] datetime2 NULL,
    [ActualStartDate] datetime2 NULL,
    [ActualCompletionDate] datetime2 NULL,
    [EstimatedCost] decimal(18,2) NOT NULL,
    [ActualCost] decimal(18,2) NOT NULL,
    [EstimatedHours] float NOT NULL,
    [ActualHours] float NOT NULL,
    [RequestedById] uniqueidentifier NULL,
    [ApprovedById] uniqueidentifier NULL,
    [ApprovedAt] datetime2 NULL,
    [CompletionNotes] nvarchar(1000) NULL,
    [FailureCode] nvarchar(50) NULL,
    [CauseCode] nvarchar(50) NULL,
    [ActionCode] nvarchar(50) NULL,
    [SafetyRequirements] nvarchar(500) NULL,
    [RequiresPermit] bit NOT NULL,
    [RequiresLockout] bit NOT NULL,
    [RequiresConfinedSpaceEntry] bit NOT NULL,
    [ParentWorkOrderId] uniqueidentifier NULL,
    [MaintenanceScheduleId] uniqueidentifier NULL,
    [IsRecurring] bit NOT NULL,
    [CustomFields] nvarchar(max) NULL,
    [CustomFieldValues] nvarchar(max) NULL,
    [EmployeeId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkOrders] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrders_Employees_ApprovedById] FOREIGN KEY ([ApprovedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrders_Employees_AssignedTechnicianId] FOREIGN KEY ([AssignedTechnicianId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrders_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrders_Employees_RequestedById] FOREIGN KEY ([RequestedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrders_MaintenanceAssets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [MaintenanceAssets] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrders_MaintenanceSchedules_MaintenanceScheduleId] FOREIGN KEY ([MaintenanceScheduleId]) REFERENCES [MaintenanceSchedules] ([Id]),
    CONSTRAINT [FK_WorkOrders_MaintenanceTypes_MaintenanceTypeId] FOREIGN KEY ([MaintenanceTypeId]) REFERENCES [MaintenanceTypes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrders_PriorityLevels_PriorityLevelId] FOREIGN KEY ([PriorityLevelId]) REFERENCES [PriorityLevels] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrders_TechnicianTeams_AssignedTeamId] FOREIGN KEY ([AssignedTeamId]) REFERENCES [TechnicianTeams] ([Id]),
    CONSTRAINT [FK_WorkOrders_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrders_WorkOrderTypes_WorkOrderTypeId] FOREIGN KEY ([WorkOrderTypeId]) REFERENCES [WorkOrderTypes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrders_WorkOrders_ParentWorkOrderId] FOREIGN KEY ([ParentWorkOrderId]) REFERENCES [WorkOrders] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [TechnicianSchedules] (
    [Id] uniqueidentifier NOT NULL,
    [TechnicianId] uniqueidentifier NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [ScheduleType] nvarchar(50) NOT NULL,
    [WorkOrderId] uniqueidentifier NULL,
    [ShiftId] uniqueidentifier NULL,
    [Title] nvarchar(200) NULL,
    [Description] nvarchar(1000) NULL,
    [Location] nvarchar(500) NULL,
    [Status] nvarchar(20) NOT NULL,
    [Priority] nvarchar(20) NOT NULL,
    [IsAllDay] bit NOT NULL,
    [IsRecurring] bit NOT NULL,
    [RecurrencePattern] nvarchar(100) NULL,
    [Notes] nvarchar(1000) NULL,
    [EstimatedHours] float NOT NULL,
    [ActualHours] float NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TechnicianSchedules] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TechnicianSchedules_Employees_TechnicianId] FOREIGN KEY ([TechnicianId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TechnicianSchedules_TechnicianShifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [TechnicianShifts] ([Id]),
    CONSTRAINT [FK_TechnicianSchedules_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TechnicianSchedules_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id])
);
GO

CREATE TABLE [WorkOrderComments] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Comment] nvarchar(2000) NOT NULL,
    [CommentType] nvarchar(20) NOT NULL,
    [IsInternal] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkOrderComments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderComments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrderComments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrderComments_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WorkOrderDocuments] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [FileName] nvarchar(200) NOT NULL,
    [FilePath] nvarchar(500) NOT NULL,
    [FileType] nvarchar(50) NULL,
    [FileSize] bigint NOT NULL,
    [Description] nvarchar(1000) NULL,
    [DocumentType] nvarchar(50) NOT NULL,
    [UploadedById] uniqueidentifier NOT NULL,
    [UploadedAt] datetime2 NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkOrderDocuments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderDocuments_Employees_UploadedById] FOREIGN KEY ([UploadedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrderDocuments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrderDocuments_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WorkOrderLabor] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [TechnicianId] uniqueidentifier NOT NULL,
    [StartTime] datetime2 NOT NULL,
    [EndTime] datetime2 NULL,
    [Hours] float NOT NULL,
    [HourlyRate] decimal(18,4) NOT NULL,
    [TotalCost] decimal(18,2) NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [LaborType] nvarchar(50) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkOrderLabor] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderLabor_Employees_TechnicianId] FOREIGN KEY ([TechnicianId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrderLabor_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrderLabor_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WorkOrderParts] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [InventoryItemId] uniqueidentifier NOT NULL,
    [ItemCode] nvarchar(100) NOT NULL,
    [ItemName] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [QuantityRequired] decimal(18,4) NOT NULL,
    [QuantityAllocated] decimal(18,4) NOT NULL,
    [QuantityUsed] decimal(18,4) NOT NULL,
    [QuantityReturned] decimal(18,4) NOT NULL,
    [UnitCost] decimal(18,2) NOT NULL,
    [TotalCost] decimal(18,2) NOT NULL,
    [WarehouseLocationId] uniqueidentifier NULL,
    [SerialNumber] nvarchar(100) NULL,
    [LotNumber] nvarchar(100) NULL,
    [Status] nvarchar(20) NOT NULL,
    [AllocationId] uniqueidentifier NULL,
    [AllocatedAt] datetime2 NULL,
    [PickedAt] datetime2 NULL,
    [UsedAt] datetime2 NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkOrderParts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderParts_InventoryAllocations_AllocationId] FOREIGN KEY ([AllocationId]) REFERENCES [InventoryAllocations] ([Id]),
    CONSTRAINT [FK_WorkOrderParts_InventoryItems_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [InventoryItems] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_WorkOrderParts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrderParts_WarehouseLocations_WarehouseLocationId] FOREIGN KEY ([WarehouseLocationId]) REFERENCES [WarehouseLocations] ([Id]),
    CONSTRAINT [FK_WorkOrderParts_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WorkOrderTasks] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [TaskName] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Sequence] int NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [EstimatedHours] float NOT NULL,
    [ActualHours] float NOT NULL,
    [AssignedTechnicianId] uniqueidentifier NULL,
    [StartedAt] datetime2 NULL,
    [CompletedAt] datetime2 NULL,
    [CompletionNotes] nvarchar(1000) NULL,
    [IsRequired] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkOrderTasks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderTasks_Employees_AssignedTechnicianId] FOREIGN KEY ([AssignedTechnicianId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrderTasks_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrderTasks_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]) ON DELETE CASCADE
);
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-10T04:11:15.4037229Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-10T04:11:15.4037267Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-10T04:11:15.4037269Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-10T04:11:15.4037271Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T04:11:15.4037498Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T04:11:15.4037513Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T04:11:15.4037520Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T04:11:15.4037528Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T04:11:15.4037538Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T04:11:15.4037545Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T04:11:15.4037551Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T04:11:15.4037557Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T04:11:15.4037566Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T04:11:15.4037575Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T04:11:15.4037581Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T04:11:15.4037588Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T04:11:15.4037597Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T04:11:15.4037608Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T04:11:15.4037620Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T04:11:15.4037626Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037671Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037673Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037674Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037675Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037676Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037677Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037678Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037679Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037680Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037681Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037682Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037682Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037683Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037683Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037684Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037685Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037717Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037719Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037719Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037720Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037721Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037721Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037722Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037722Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037723Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037724Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037724Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037725Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037726Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037726Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037727Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037767Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037768Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037770Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037771Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037771Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037772Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037772Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037773Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037774Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037774Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037787Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037788Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T04:11:15.4037789Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('1f33cb93-dce0-453c-b7ed-9efd20a90d05', NULL, '2025-10-10T04:11:15.4037332Z', NULL, NULL, NULL, NULL, NULL, '2025-10-10T04:11:15.4037330Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('4f37828e-cf46-4daf-8e6d-7235bb99f404', NULL, '2025-10-10T04:11:15.4037400Z', NULL, NULL, NULL, NULL, NULL, '2025-10-10T04:11:15.4037400Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('4fdd066f-98ff-42ea-9a53-ba29abd712b8', NULL, '2025-10-10T04:11:15.4037371Z', NULL, NULL, NULL, NULL, NULL, '2025-10-10T04:11:15.4037371Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('5502c101-06bd-4cbd-88db-3290d6371a34', NULL, '2025-10-10T04:11:15.4037386Z', NULL, NULL, NULL, NULL, NULL, '2025-10-10T04:11:15.4037386Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('6bcfab55-7e1a-4848-bf86-de7cc2754a03', NULL, '2025-10-10T04:11:15.4037416Z', NULL, NULL, NULL, NULL, NULL, '2025-10-10T04:11:15.4037416Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('a6769cc2-0256-4a35-93f4-8452fdb87cb6', NULL, '2025-10-10T04:11:15.4037349Z', NULL, NULL, NULL, NULL, NULL, '2025-10-10T04:11:15.4037349Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('e24d1390-2e88-4e91-a7d4-03ef7e877164', NULL, '2025-10-10T04:11:15.4037431Z', NULL, NULL, NULL, NULL, NULL, '2025-10-10T04:11:15.4037430Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-10-10T04:11:15.4037040Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_AssetDowntimes_AssetId] ON [AssetDowntimes] ([AssetId]);
GO

CREATE INDEX [IX_AssetDowntimes_Reason] ON [AssetDowntimes] ([Reason]);
GO

CREATE INDEX [IX_AssetDowntimes_StartTime] ON [AssetDowntimes] ([StartTime]);
GO

CREATE INDEX [IX_AssetDowntimes_Status] ON [AssetDowntimes] ([Status]);
GO

CREATE INDEX [IX_AssetDowntimes_TenantId] ON [AssetDowntimes] ([TenantId]);
GO

CREATE INDEX [IX_AssetDowntimes_WorkOrderId] ON [AssetDowntimes] ([WorkOrderId]);
GO

CREATE INDEX [IX_AssetInspections_AssetId] ON [AssetInspections] ([AssetId]);
GO

CREATE INDEX [IX_AssetInspections_InspectionDate] ON [AssetInspections] ([InspectionDate]);
GO

CREATE INDEX [IX_AssetInspections_InspectionTemplateId] ON [AssetInspections] ([InspectionTemplateId]);
GO

CREATE INDEX [IX_AssetInspections_InspectorId] ON [AssetInspections] ([InspectorId]);
GO

CREATE INDEX [IX_AssetInspections_IsRegulatoryRequired] ON [AssetInspections] ([IsRegulatoryRequired]);
GO

CREATE INDEX [IX_AssetInspections_NextInspectionDue] ON [AssetInspections] ([NextInspectionDue]);
GO

CREATE INDEX [IX_AssetInspections_OverallResult] ON [AssetInspections] ([OverallResult]);
GO

CREATE INDEX [IX_AssetInspections_Status] ON [AssetInspections] ([Status]);
GO

CREATE INDEX [IX_AssetInspections_TenantId] ON [AssetInspections] ([TenantId]);
GO

CREATE UNIQUE INDEX [IX_AssetTypeFields_AssetTypeId_FieldName] ON [AssetTypeFields] ([AssetTypeId], [FieldName]);
GO

CREATE INDEX [IX_AssetTypeFields_DisplayOrder] ON [AssetTypeFields] ([DisplayOrder]);
GO

CREATE INDEX [IX_AssetTypeFields_IsActive] ON [AssetTypeFields] ([IsActive]);
GO

CREATE INDEX [IX_AssetTypeFields_TenantId] ON [AssetTypeFields] ([TenantId]);
GO

CREATE INDEX [IX_AssetTypes_Code] ON [AssetTypes] ([Code]);
GO

CREATE INDEX [IX_AssetTypes_IsActive] ON [AssetTypes] ([IsActive]);
GO

CREATE UNIQUE INDEX [IX_AssetTypes_TenantId_Name] ON [AssetTypes] ([TenantId], [Name]);
GO

CREATE INDEX [IX_AttendanceRecords_EmployeeId] ON [AttendanceRecords] ([EmployeeId]);
GO

CREATE INDEX [IX_AttendanceRecords_TenantId] ON [AttendanceRecords] ([TenantId]);
GO

CREATE INDEX [IX_Countries_TenantId] ON [Countries] ([TenantId]);
GO

CREATE UNIQUE INDEX [IX_Departments_Code] ON [Departments] ([Code]);
GO

CREATE INDEX [IX_Departments_DepartmentHeadId] ON [Departments] ([DepartmentHeadId]);
GO

CREATE INDEX [IX_Departments_DepartmentType] ON [Departments] ([DepartmentType]);
GO

CREATE INDEX [IX_Departments_IsActive] ON [Departments] ([IsActive]);
GO

CREATE INDEX [IX_Departments_Name] ON [Departments] ([Name]);
GO

CREATE INDEX [IX_Departments_ParentDepartmentId] ON [Departments] ([ParentDepartmentId]);
GO

CREATE INDEX [IX_Departments_TenantId] ON [Departments] ([TenantId]);
GO

CREATE INDEX [IX_EmployeeBiometrics_EmployeeId] ON [EmployeeBiometrics] ([EmployeeId]);
GO

CREATE INDEX [IX_EmployeeBiometrics_TenantId] ON [EmployeeBiometrics] ([TenantId]);
GO

CREATE INDEX [IX_EmployeeContractDetails_EmployeeId] ON [EmployeeContractDetails] ([EmployeeId]);
GO

CREATE INDEX [IX_EmployeeContractDetails_TenantId] ON [EmployeeContractDetails] ([TenantId]);
GO

CREATE INDEX [IX_EmployeeDependents_EmployeeId] ON [EmployeeDependents] ([EmployeeId]);
GO

CREATE INDEX [IX_EmployeeDependents_TenantId] ON [EmployeeDependents] ([TenantId]);
GO

CREATE INDEX [IX_EmployeeEmergencyContacts_EmployeeId] ON [EmployeeEmergencyContacts] ([EmployeeId]);
GO

CREATE INDEX [IX_EmployeeEmergencyContacts_TenantId] ON [EmployeeEmergencyContacts] ([TenantId]);
GO

CREATE INDEX [IX_EmployeeIdentificationCards_EmployeeId] ON [EmployeeIdentificationCards] ([EmployeeId]);
GO

CREATE INDEX [IX_EmployeeIdentificationCards_TenantId] ON [EmployeeIdentificationCards] ([TenantId]);
GO

CREATE INDEX [IX_EmployeePositions_DepartmentId] ON [EmployeePositions] ([DepartmentId]);
GO

CREATE INDEX [IX_EmployeePositions_TenantId] ON [EmployeePositions] ([TenantId]);
GO

CREATE INDEX [IX_EmployeeQualifications_EmployeeId] ON [EmployeeQualifications] ([EmployeeId]);
GO

CREATE INDEX [IX_EmployeeQualifications_TenantId] ON [EmployeeQualifications] ([TenantId]);
GO

CREATE INDEX [IX_Employees_CorporateEmployeeID] ON [Employees] ([CorporateEmployeeID]);
GO

CREATE INDEX [IX_Employees_CountryId] ON [Employees] ([CountryId]);
GO

CREATE INDEX [IX_Employees_DateEmployed] ON [Employees] ([DateEmployed]);
GO

CREATE INDEX [IX_Employees_DepartmentId] ON [Employees] ([DepartmentId]);
GO

CREATE UNIQUE INDEX [IX_Employees_EmailAddress] ON [Employees] ([EmailAddress]);
GO

CREATE UNIQUE INDEX [IX_Employees_EmployeeNumber] ON [Employees] ([EmployeeNumber]);
GO

CREATE INDEX [IX_Employees_IsActive] ON [Employees] ([IsActive]);
GO

CREATE INDEX [IX_Employees_ManagerId] ON [Employees] ([ManagerId]);
GO

CREATE INDEX [IX_Employees_PositionId] ON [Employees] ([PositionId]);
GO

CREATE INDEX [IX_Employees_SectionId] ON [Employees] ([SectionId]);
GO

CREATE INDEX [IX_Employees_ShiftId] ON [Employees] ([ShiftId]);
GO

CREATE INDEX [IX_Employees_StaffStatus] ON [Employees] ([StaffStatus]);
GO

CREATE INDEX [IX_Employees_StationId] ON [Employees] ([StationId]);
GO

CREATE INDEX [IX_Employees_TenantId] ON [Employees] ([TenantId]);
GO

CREATE INDEX [IX_EmployeeShiftPreferences_EmployeeId] ON [EmployeeShiftPreferences] ([EmployeeId]);
GO

CREATE INDEX [IX_EmployeeShiftPreferences_ShiftId] ON [EmployeeShiftPreferences] ([ShiftId]);
GO

CREATE INDEX [IX_EmployeeShiftPreferences_TenantId] ON [EmployeeShiftPreferences] ([TenantId]);
GO

CREATE INDEX [IX_EmployeeSkills_EmployeeId] ON [EmployeeSkills] ([EmployeeId]);
GO

CREATE INDEX [IX_EmployeeSkills_SkillId] ON [EmployeeSkills] ([SkillId]);
GO

CREATE INDEX [IX_EmployeeSkills_TenantId] ON [EmployeeSkills] ([TenantId]);
GO

CREATE INDEX [IX_EmployeeWorkHistories_EmployeeId] ON [EmployeeWorkHistories] ([EmployeeId]);
GO

CREATE INDEX [IX_EmployeeWorkHistories_TenantId] ON [EmployeeWorkHistories] ([TenantId]);
GO

CREATE INDEX [IX_InspectionDocuments_DocumentType] ON [InspectionDocuments] ([DocumentType]);
GO

CREATE INDEX [IX_InspectionDocuments_InspectionId] ON [InspectionDocuments] ([InspectionId]);
GO

CREATE INDEX [IX_InspectionDocuments_TenantId] ON [InspectionDocuments] ([TenantId]);
GO

CREATE INDEX [IX_InspectionTemplates_Category] ON [InspectionTemplates] ([Category]);
GO

CREATE INDEX [IX_InspectionTemplates_InspectionType] ON [InspectionTemplates] ([InspectionType]);
GO

CREATE INDEX [IX_InspectionTemplates_IsActive] ON [InspectionTemplates] ([IsActive]);
GO

CREATE INDEX [IX_InspectionTemplates_TenantId] ON [InspectionTemplates] ([TenantId]);
GO

CREATE INDEX [IX_InventoryAllocations_AllocatedById] ON [InventoryAllocations] ([AllocatedById]);
GO

CREATE INDEX [IX_InventoryAllocations_InventoryItemId] ON [InventoryAllocations] ([InventoryItemId]);
GO

CREATE INDEX [IX_InventoryAllocations_LocationId] ON [InventoryAllocations] ([LocationId]);
GO

CREATE INDEX [IX_InventoryAllocations_TenantId] ON [InventoryAllocations] ([TenantId]);
GO

CREATE INDEX [IX_InventoryCategories_Code] ON [InventoryCategories] ([Code]);
GO

CREATE INDEX [IX_InventoryCategories_IsActive] ON [InventoryCategories] ([IsActive]);
GO

CREATE INDEX [IX_InventoryCategories_ParentCategoryId] ON [InventoryCategories] ([ParentCategoryId]);
GO

CREATE INDEX [IX_InventoryCategories_TenantId] ON [InventoryCategories] ([TenantId]);
GO

CREATE INDEX [IX_InventoryItems_ABCClass] ON [InventoryItems] ([ABCClass]);
GO

CREATE INDEX [IX_InventoryItems_CategoryId] ON [InventoryItems] ([CategoryId]);
GO

CREATE INDEX [IX_InventoryItems_IsLotTracked] ON [InventoryItems] ([IsLotTracked]);
GO

CREATE INDEX [IX_InventoryItems_IsSerialTracked] ON [InventoryItems] ([IsSerialTracked]);
GO

CREATE UNIQUE INDEX [IX_InventoryItems_ItemCode] ON [InventoryItems] ([ItemCode]);
GO

CREATE INDEX [IX_InventoryItems_Status] ON [InventoryItems] ([Status]);
GO

CREATE INDEX [IX_InventoryItems_TenantId] ON [InventoryItems] ([TenantId]);
GO

CREATE INDEX [IX_InventoryLocations_InventoryItemId] ON [InventoryLocations] ([InventoryItemId]);
GO

CREATE INDEX [IX_InventoryLocations_LocationId] ON [InventoryLocations] ([LocationId]);
GO

CREATE INDEX [IX_InventoryLocations_TenantId] ON [InventoryLocations] ([TenantId]);
GO

CREATE INDEX [IX_MaintenanceAssetCategories_Code] ON [MaintenanceAssetCategories] ([Code]);
GO

CREATE INDEX [IX_MaintenanceAssetCategories_IsActive] ON [MaintenanceAssetCategories] ([IsActive]);
GO

CREATE INDEX [IX_MaintenanceAssetCategories_TenantId] ON [MaintenanceAssetCategories] ([TenantId]);
GO

CREATE INDEX [IX_MaintenanceAssets_AssetCategoryId] ON [MaintenanceAssets] ([AssetCategoryId]);
GO

CREATE INDEX [IX_MaintenanceAssets_AssetNumber] ON [MaintenanceAssets] ([AssetNumber]);
GO

CREATE INDEX [IX_MaintenanceAssets_AssetTypeId] ON [MaintenanceAssets] ([AssetTypeId]);
GO

CREATE INDEX [IX_MaintenanceAssets_Criticality] ON [MaintenanceAssets] ([Criticality]);
GO

CREATE INDEX [IX_MaintenanceAssets_EmployeeId] ON [MaintenanceAssets] ([EmployeeId]);
GO

CREATE INDEX [IX_MaintenanceAssets_ParentAssetId] ON [MaintenanceAssets] ([ParentAssetId]);
GO

CREATE INDEX [IX_MaintenanceAssets_SerialNumber] ON [MaintenanceAssets] ([SerialNumber]);
GO

CREATE INDEX [IX_MaintenanceAssets_Status] ON [MaintenanceAssets] ([Status]);
GO

CREATE INDEX [IX_MaintenanceAssets_TenantId] ON [MaintenanceAssets] ([TenantId]);
GO

CREATE INDEX [IX_MaintenanceSchedules_AssetId] ON [MaintenanceSchedules] ([AssetId]);
GO

CREATE INDEX [IX_MaintenanceSchedules_DefaultTeamId] ON [MaintenanceSchedules] ([DefaultTeamId]);
GO

CREATE INDEX [IX_MaintenanceSchedules_DefaultTechnicianId] ON [MaintenanceSchedules] ([DefaultTechnicianId]);
GO

CREATE INDEX [IX_MaintenanceSchedules_IsActive] ON [MaintenanceSchedules] ([IsActive]);
GO

CREATE INDEX [IX_MaintenanceSchedules_LastGeneratedDate] ON [MaintenanceSchedules] ([LastGeneratedDate]);
GO

CREATE INDEX [IX_MaintenanceSchedules_MaintenanceTypeId] ON [MaintenanceSchedules] ([MaintenanceTypeId]);
GO

CREATE INDEX [IX_MaintenanceSchedules_NextDueDate] ON [MaintenanceSchedules] ([NextDueDate]);
GO

CREATE INDEX [IX_MaintenanceSchedules_PriorityLevelId] ON [MaintenanceSchedules] ([PriorityLevelId]);
GO

CREATE INDEX [IX_MaintenanceSchedules_ScheduleType] ON [MaintenanceSchedules] ([ScheduleType]);
GO

CREATE INDEX [IX_MaintenanceSchedules_TenantId] ON [MaintenanceSchedules] ([TenantId]);
GO

CREATE INDEX [IX_MaintenanceTypes_Category] ON [MaintenanceTypes] ([Category]);
GO

CREATE INDEX [IX_MaintenanceTypes_Code] ON [MaintenanceTypes] ([Code]);
GO

CREATE INDEX [IX_MaintenanceTypes_IsActive] ON [MaintenanceTypes] ([IsActive]);
GO

CREATE INDEX [IX_MaintenanceTypes_TenantId] ON [MaintenanceTypes] ([TenantId]);
GO

CREATE INDEX [IX_PositionSkillRequirement_PositionId] ON [PositionSkillRequirement] ([PositionId]);
GO

CREATE INDEX [IX_PositionSkillRequirement_SkillId] ON [PositionSkillRequirement] ([SkillId]);
GO

CREATE INDEX [IX_PositionSkillRequirement_TenantId] ON [PositionSkillRequirement] ([TenantId]);
GO

CREATE INDEX [IX_PriorityLevels_IsActive] ON [PriorityLevels] ([IsActive]);
GO

CREATE UNIQUE INDEX [IX_PriorityLevels_Level] ON [PriorityLevels] ([Level]);
GO

CREATE INDEX [IX_PriorityLevels_TenantId] ON [PriorityLevels] ([TenantId]);
GO

CREATE INDEX [IX_PurchaseOrderItems_InventoryItemId] ON [PurchaseOrderItems] ([InventoryItemId]);
GO

CREATE INDEX [IX_PurchaseOrderItems_PurchaseOrderId] ON [PurchaseOrderItems] ([PurchaseOrderId]);
GO

CREATE INDEX [IX_PurchaseOrderItems_TenantId] ON [PurchaseOrderItems] ([TenantId]);
GO

CREATE INDEX [IX_PurchaseOrderReceiptItems_LocationId] ON [PurchaseOrderReceiptItems] ([LocationId]);
GO

CREATE INDEX [IX_PurchaseOrderReceiptItems_PurchaseOrderItemId] ON [PurchaseOrderReceiptItems] ([PurchaseOrderItemId]);
GO

CREATE INDEX [IX_PurchaseOrderReceiptItems_ReceiptId] ON [PurchaseOrderReceiptItems] ([ReceiptId]);
GO

CREATE INDEX [IX_PurchaseOrderReceiptItems_TenantId] ON [PurchaseOrderReceiptItems] ([TenantId]);
GO

CREATE INDEX [IX_PurchaseOrderReceipts_InspectedById] ON [PurchaseOrderReceipts] ([InspectedById]);
GO

CREATE INDEX [IX_PurchaseOrderReceipts_PurchaseOrderId] ON [PurchaseOrderReceipts] ([PurchaseOrderId]);
GO

CREATE INDEX [IX_PurchaseOrderReceipts_ReceiptDate] ON [PurchaseOrderReceipts] ([ReceiptDate]);
GO

CREATE UNIQUE INDEX [IX_PurchaseOrderReceipts_ReceiptNumber] ON [PurchaseOrderReceipts] ([ReceiptNumber]);
GO

CREATE INDEX [IX_PurchaseOrderReceipts_ReceivedById] ON [PurchaseOrderReceipts] ([ReceivedById]);
GO

CREATE INDEX [IX_PurchaseOrderReceipts_Status] ON [PurchaseOrderReceipts] ([Status]);
GO

CREATE INDEX [IX_PurchaseOrderReceipts_TenantId] ON [PurchaseOrderReceipts] ([TenantId]);
GO

CREATE INDEX [IX_PurchaseOrders_ApprovedById] ON [PurchaseOrders] ([ApprovedById]);
GO

CREATE INDEX [IX_PurchaseOrders_DeliveryWarehouseId] ON [PurchaseOrders] ([DeliveryWarehouseId]);
GO

CREATE INDEX [IX_PurchaseOrders_OrderDate] ON [PurchaseOrders] ([OrderDate]);
GO

CREATE UNIQUE INDEX [IX_PurchaseOrders_OrderNumber] ON [PurchaseOrders] ([OrderNumber]);
GO

CREATE INDEX [IX_PurchaseOrders_RequestedById] ON [PurchaseOrders] ([RequestedById]);
GO

CREATE INDEX [IX_PurchaseOrders_Status] ON [PurchaseOrders] ([Status]);
GO

CREATE INDEX [IX_PurchaseOrders_TenantId] ON [PurchaseOrders] ([TenantId]);
GO

CREATE INDEX [IX_Sections_Code] ON [Sections] ([Code]);
GO

CREATE INDEX [IX_Sections_DepartmentId] ON [Sections] ([DepartmentId]);
GO

CREATE INDEX [IX_Sections_IsActive] ON [Sections] ([IsActive]);
GO

CREATE INDEX [IX_Sections_SectionHeadId] ON [Sections] ([SectionHeadId]);
GO

CREATE INDEX [IX_Sections_TenantId] ON [Sections] ([TenantId]);
GO

CREATE INDEX [IX_ShiftAssignments_EmployeeId] ON [ShiftAssignments] ([EmployeeId]);
GO

CREATE INDEX [IX_ShiftAssignments_ShiftId] ON [ShiftAssignments] ([ShiftId]);
GO

CREATE INDEX [IX_ShiftAssignments_TenantId] ON [ShiftAssignments] ([TenantId]);
GO

CREATE INDEX [IX_Shifts_TenantId] ON [Shifts] ([TenantId]);
GO

CREATE INDEX [IX_Skill_TenantId] ON [Skill] ([TenantId]);
GO

CREATE INDEX [IX_StockAdjustmentItems_AdjustmentId] ON [StockAdjustmentItems] ([AdjustmentId]);
GO

CREATE INDEX [IX_StockAdjustmentItems_InventoryItemId] ON [StockAdjustmentItems] ([InventoryItemId]);
GO

CREATE INDEX [IX_StockAdjustmentItems_LocationId] ON [StockAdjustmentItems] ([LocationId]);
GO

CREATE INDEX [IX_StockAdjustmentItems_TenantId] ON [StockAdjustmentItems] ([TenantId]);
GO

CREATE INDEX [IX_StockAdjustments_AdjustmentDate] ON [StockAdjustments] ([AdjustmentDate]);
GO

CREATE UNIQUE INDEX [IX_StockAdjustments_AdjustmentNumber] ON [StockAdjustments] ([AdjustmentNumber]);
GO

CREATE INDEX [IX_StockAdjustments_ApprovedById] ON [StockAdjustments] ([ApprovedById]);
GO

CREATE INDEX [IX_StockAdjustments_InventoryItemId] ON [StockAdjustments] ([InventoryItemId]);
GO

CREATE INDEX [IX_StockAdjustments_ReasonCode] ON [StockAdjustments] ([ReasonCode]);
GO

CREATE INDEX [IX_StockAdjustments_Status] ON [StockAdjustments] ([Status]);
GO

CREATE INDEX [IX_StockAdjustments_TenantId] ON [StockAdjustments] ([TenantId]);
GO

CREATE INDEX [IX_StockMovements_ApprovedById] ON [StockMovements] ([ApprovedById]);
GO

CREATE INDEX [IX_StockMovements_InventoryItemId] ON [StockMovements] ([InventoryItemId]);
GO

CREATE INDEX [IX_StockMovements_LocationId] ON [StockMovements] ([LocationId]);
GO

CREATE INDEX [IX_StockMovements_MovementDate] ON [StockMovements] ([MovementDate]);
GO

CREATE INDEX [IX_StockMovements_MovementType] ON [StockMovements] ([MovementType]);
GO

CREATE INDEX [IX_StockMovements_ProcessedById] ON [StockMovements] ([ProcessedById]);
GO

CREATE INDEX [IX_StockMovements_ReferenceNumber] ON [StockMovements] ([ReferenceNumber]);
GO

CREATE INDEX [IX_StockMovements_ReferenceType] ON [StockMovements] ([ReferenceType]);
GO

CREATE INDEX [IX_StockMovements_TenantId] ON [StockMovements] ([TenantId]);
GO

CREATE INDEX [IX_TechnicianAvailabilities_TechnicianId] ON [TechnicianAvailabilities] ([TechnicianId]);
GO

CREATE INDEX [IX_TechnicianAvailabilities_TenantId] ON [TechnicianAvailabilities] ([TenantId]);
GO

CREATE INDEX [IX_TechnicianSchedules_ShiftId] ON [TechnicianSchedules] ([ShiftId]);
GO

CREATE INDEX [IX_TechnicianSchedules_TechnicianId] ON [TechnicianSchedules] ([TechnicianId]);
GO

CREATE INDEX [IX_TechnicianSchedules_TenantId] ON [TechnicianSchedules] ([TenantId]);
GO

CREATE INDEX [IX_TechnicianSchedules_WorkOrderId] ON [TechnicianSchedules] ([WorkOrderId]);
GO

CREATE INDEX [IX_TechnicianShifts_TenantId] ON [TechnicianShifts] ([TenantId]);
GO

CREATE INDEX [IX_TechnicianSkills_Category] ON [TechnicianSkills] ([Category]);
GO

CREATE INDEX [IX_TechnicianSkills_IsActive] ON [TechnicianSkills] ([IsActive]);
GO

CREATE INDEX [IX_TechnicianSkills_TenantId] ON [TechnicianSkills] ([TenantId]);
GO

CREATE INDEX [IX_TechnicianTeamMembers_IsActive] ON [TechnicianTeamMembers] ([IsActive]);
GO

CREATE INDEX [IX_TechnicianTeamMembers_TeamId] ON [TechnicianTeamMembers] ([TeamId]);
GO

CREATE INDEX [IX_TechnicianTeamMembers_TeamId_TechnicianId] ON [TechnicianTeamMembers] ([TeamId], [TechnicianId]);
GO

CREATE INDEX [IX_TechnicianTeamMembers_TechnicianId] ON [TechnicianTeamMembers] ([TechnicianId]);
GO

CREATE INDEX [IX_TechnicianTeamMembers_TenantId] ON [TechnicianTeamMembers] ([TenantId]);
GO

CREATE INDEX [IX_TechnicianTeams_Status] ON [TechnicianTeams] ([Status]);
GO

CREATE INDEX [IX_TechnicianTeams_TeamLeaderId] ON [TechnicianTeams] ([TeamLeaderId]);
GO

CREATE INDEX [IX_TechnicianTeams_TenantId] ON [TechnicianTeams] ([TenantId]);
GO

CREATE INDEX [IX_UserTechnicianSkills_CertificationExpiry] ON [UserTechnicianSkills] ([CertificationExpiry]);
GO

CREATE INDEX [IX_UserTechnicianSkills_EmployeeId] ON [UserTechnicianSkills] ([EmployeeId]);
GO

CREATE UNIQUE INDEX [IX_UserTechnicianSkills_EmployeeId_SkillId] ON [UserTechnicianSkills] ([EmployeeId], [SkillId]);
GO

CREATE INDEX [IX_UserTechnicianSkills_ProficiencyLevel] ON [UserTechnicianSkills] ([ProficiencyLevel]);
GO

CREATE INDEX [IX_UserTechnicianSkills_SkillId] ON [UserTechnicianSkills] ([SkillId]);
GO

CREATE INDEX [IX_UserTechnicianSkills_TenantId] ON [UserTechnicianSkills] ([TenantId]);
GO

CREATE INDEX [IX_WarehouseLocations_IsActive] ON [WarehouseLocations] ([IsActive]);
GO

CREATE INDEX [IX_WarehouseLocations_LocationCode] ON [WarehouseLocations] ([LocationCode]);
GO

CREATE INDEX [IX_WarehouseLocations_ParentLocationId] ON [WarehouseLocations] ([ParentLocationId]);
GO

CREATE INDEX [IX_WarehouseLocations_TenantId] ON [WarehouseLocations] ([TenantId]);
GO

CREATE INDEX [IX_WarehouseLocations_WarehouseId] ON [WarehouseLocations] ([WarehouseId]);
GO

CREATE UNIQUE INDEX [IX_Warehouses_Code] ON [Warehouses] ([Code]);
GO

CREATE INDEX [IX_Warehouses_IsActive] ON [Warehouses] ([IsActive]);
GO

CREATE INDEX [IX_Warehouses_TenantId] ON [Warehouses] ([TenantId]);
GO

CREATE INDEX [IX_Warehouses_WarehouseType] ON [Warehouses] ([WarehouseType]);
GO

CREATE INDEX [IX_WorkOrderComments_CommentType] ON [WorkOrderComments] ([CommentType]);
GO

CREATE INDEX [IX_WorkOrderComments_CreatedAt] ON [WorkOrderComments] ([CreatedAt]);
GO

CREATE INDEX [IX_WorkOrderComments_EmployeeId] ON [WorkOrderComments] ([EmployeeId]);
GO

CREATE INDEX [IX_WorkOrderComments_TenantId] ON [WorkOrderComments] ([TenantId]);
GO

CREATE INDEX [IX_WorkOrderComments_WorkOrderId] ON [WorkOrderComments] ([WorkOrderId]);
GO

CREATE INDEX [IX_WorkOrderDocuments_DocumentType] ON [WorkOrderDocuments] ([DocumentType]);
GO

CREATE INDEX [IX_WorkOrderDocuments_TenantId] ON [WorkOrderDocuments] ([TenantId]);
GO

CREATE INDEX [IX_WorkOrderDocuments_UploadedAt] ON [WorkOrderDocuments] ([UploadedAt]);
GO

CREATE INDEX [IX_WorkOrderDocuments_UploadedById] ON [WorkOrderDocuments] ([UploadedById]);
GO

CREATE INDEX [IX_WorkOrderDocuments_WorkOrderId] ON [WorkOrderDocuments] ([WorkOrderId]);
GO

CREATE INDEX [IX_WorkOrderLabor_LaborType] ON [WorkOrderLabor] ([LaborType]);
GO

CREATE INDEX [IX_WorkOrderLabor_StartTime] ON [WorkOrderLabor] ([StartTime]);
GO

CREATE INDEX [IX_WorkOrderLabor_TechnicianId] ON [WorkOrderLabor] ([TechnicianId]);
GO

CREATE INDEX [IX_WorkOrderLabor_TenantId] ON [WorkOrderLabor] ([TenantId]);
GO

CREATE INDEX [IX_WorkOrderLabor_WorkOrderId] ON [WorkOrderLabor] ([WorkOrderId]);
GO

CREATE INDEX [IX_WorkOrderParts_AllocationId] ON [WorkOrderParts] ([AllocationId]);
GO

CREATE INDEX [IX_WorkOrderParts_InventoryItemId] ON [WorkOrderParts] ([InventoryItemId]);
GO

CREATE INDEX [IX_WorkOrderParts_ItemCode] ON [WorkOrderParts] ([ItemCode]);
GO

CREATE INDEX [IX_WorkOrderParts_Status] ON [WorkOrderParts] ([Status]);
GO

CREATE INDEX [IX_WorkOrderParts_TenantId] ON [WorkOrderParts] ([TenantId]);
GO

CREATE INDEX [IX_WorkOrderParts_WarehouseLocationId] ON [WorkOrderParts] ([WarehouseLocationId]);
GO

CREATE INDEX [IX_WorkOrderParts_WorkOrderId] ON [WorkOrderParts] ([WorkOrderId]);
GO

CREATE INDEX [IX_WorkOrders_ApprovedById] ON [WorkOrders] ([ApprovedById]);
GO

CREATE INDEX [IX_WorkOrders_AssetId] ON [WorkOrders] ([AssetId]);
GO

CREATE INDEX [IX_WorkOrders_AssignedTeamId] ON [WorkOrders] ([AssignedTeamId]);
GO

CREATE INDEX [IX_WorkOrders_AssignedTechnicianId] ON [WorkOrders] ([AssignedTechnicianId]);
GO

CREATE INDEX [IX_WorkOrders_EmployeeId] ON [WorkOrders] ([EmployeeId]);
GO

CREATE INDEX [IX_WorkOrders_MaintenanceScheduleId] ON [WorkOrders] ([MaintenanceScheduleId]);
GO

CREATE INDEX [IX_WorkOrders_MaintenanceTypeId] ON [WorkOrders] ([MaintenanceTypeId]);
GO

CREATE INDEX [IX_WorkOrders_ParentWorkOrderId] ON [WorkOrders] ([ParentWorkOrderId]);
GO

CREATE INDEX [IX_WorkOrders_PriorityLevelId] ON [WorkOrders] ([PriorityLevelId]);
GO

CREATE INDEX [IX_WorkOrders_RequestedById] ON [WorkOrders] ([RequestedById]);
GO

CREATE INDEX [IX_WorkOrders_RequestedCompletionDate] ON [WorkOrders] ([RequestedCompletionDate]);
GO

CREATE INDEX [IX_WorkOrders_RequestedStartDate] ON [WorkOrders] ([RequestedStartDate]);
GO

CREATE INDEX [IX_WorkOrders_Status] ON [WorkOrders] ([Status]);
GO

CREATE INDEX [IX_WorkOrders_TenantId] ON [WorkOrders] ([TenantId]);
GO

CREATE UNIQUE INDEX [IX_WorkOrders_WorkOrderNumber] ON [WorkOrders] ([WorkOrderNumber]);
GO

CREATE INDEX [IX_WorkOrders_WorkOrderTypeId] ON [WorkOrders] ([WorkOrderTypeId]);
GO

CREATE INDEX [IX_WorkOrderTasks_AssignedTechnicianId] ON [WorkOrderTasks] ([AssignedTechnicianId]);
GO

CREATE INDEX [IX_WorkOrderTasks_Sequence] ON [WorkOrderTasks] ([Sequence]);
GO

CREATE INDEX [IX_WorkOrderTasks_Status] ON [WorkOrderTasks] ([Status]);
GO

CREATE INDEX [IX_WorkOrderTasks_TenantId] ON [WorkOrderTasks] ([TenantId]);
GO

CREATE INDEX [IX_WorkOrderTasks_WorkOrderId] ON [WorkOrderTasks] ([WorkOrderId]);
GO

CREATE INDEX [IX_WorkOrderTypes_Code] ON [WorkOrderTypes] ([Code]);
GO

CREATE INDEX [IX_WorkOrderTypes_IsActive] ON [WorkOrderTypes] ([IsActive]);
GO

CREATE INDEX [IX_WorkOrderTypes_TenantId] ON [WorkOrderTypes] ([TenantId]);
GO

CREATE INDEX [IX_WorkStations_DepartmentId] ON [WorkStations] ([DepartmentId]);
GO

CREATE INDEX [IX_WorkStations_TenantId] ON [WorkStations] ([TenantId]);
GO

ALTER TABLE [EmailTemplates] ADD CONSTRAINT [FK_EmailTemplates_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [ReportDataSources] ADD CONSTRAINT [FK_ReportDataSources_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]);
GO

ALTER TABLE [ReportExecutions] ADD CONSTRAINT [FK_ReportExecutions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [ReportExecutions] ADD CONSTRAINT [FK_ReportExecutions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]);
GO

ALTER TABLE [ReportExports] ADD CONSTRAINT [FK_ReportExports_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [ReportExports] ADD CONSTRAINT [FK_ReportExports_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]);
GO

ALTER TABLE [ReportRoleAssignments] ADD CONSTRAINT [FK_ReportRoleAssignments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [Reports] ADD CONSTRAINT [FK_Reports_TenantModules_ModuleId] FOREIGN KEY ([ModuleId]) REFERENCES [TenantModules] ([Id]);
GO

ALTER TABLE [Reports] ADD CONSTRAINT [FK_Reports_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [ReportSchedules] ADD CONSTRAINT [FK_ReportSchedules_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [ReportTemplates] ADD CONSTRAINT [FK_ReportTemplates_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [SecurityAlerts] ADD CONSTRAINT [FK_SecurityAlerts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [SecurityAlerts] ADD CONSTRAINT [FK_SecurityAlerts_Users_AffectedUserId] FOREIGN KEY ([AffectedUserId]) REFERENCES [Users] ([Id]);
GO

ALTER TABLE [SecurityMetricsSet] ADD CONSTRAINT [FK_SecurityMetricsSet_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [SecurityPolicyViolations] ADD CONSTRAINT [FK_SecurityPolicyViolations_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]);
GO

ALTER TABLE [ThreatDetections] ADD CONSTRAINT [FK_ThreatDetections_Users_AffectedUserId] FOREIGN KEY ([AffectedUserId]) REFERENCES [Users] ([Id]);
GO

ALTER TABLE [UserReportFavorites] ADD CONSTRAINT [FK_UserReportFavorites_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [AssetDowntimes] ADD CONSTRAINT [FK_AssetDowntimes_MaintenanceAssets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [MaintenanceAssets] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [AssetDowntimes] ADD CONSTRAINT [FK_AssetDowntimes_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]);
GO

ALTER TABLE [AssetInspections] ADD CONSTRAINT [FK_AssetInspections_Employees_InspectorId] FOREIGN KEY ([InspectorId]) REFERENCES [Employees] ([Id]);
GO

ALTER TABLE [AssetInspections] ADD CONSTRAINT [FK_AssetInspections_MaintenanceAssets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [MaintenanceAssets] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [AttendanceRecords] ADD CONSTRAINT [FK_AttendanceRecords_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [Departments] ADD CONSTRAINT [FK_Departments_Employees_DepartmentHeadId] FOREIGN KEY ([DepartmentHeadId]) REFERENCES [Employees] ([Id]);
GO

ALTER TABLE [EmployeeBiometrics] ADD CONSTRAINT [FK_EmployeeBiometrics_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [EmployeeContractDetails] ADD CONSTRAINT [FK_EmployeeContractDetails_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [EmployeeDependents] ADD CONSTRAINT [FK_EmployeeDependents_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [EmployeeEmergencyContacts] ADD CONSTRAINT [FK_EmployeeEmergencyContacts_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [EmployeeIdentificationCards] ADD CONSTRAINT [FK_EmployeeIdentificationCards_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [EmployeeQualifications] ADD CONSTRAINT [FK_EmployeeQualifications_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [Employees] ADD CONSTRAINT [FK_Employees_Sections_SectionId] FOREIGN KEY ([SectionId]) REFERENCES [Sections] ([Id]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251010041116_AddBusinessModulesCompleteConfiguration', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [PurchaseOrderItems] DROP CONSTRAINT [FK_PurchaseOrderItems_InventoryItems_InventoryItemId];
GO

ALTER TABLE [PurchaseOrderReceiptItems] DROP CONSTRAINT [FK_PurchaseOrderReceiptItems_WarehouseLocations_LocationId];
GO

ALTER TABLE [PurchaseOrders] DROP CONSTRAINT [FK_PurchaseOrders_Warehouses_DeliveryWarehouseId];
GO

DROP INDEX [IX_PurchaseOrders_DeliveryWarehouseId] ON [PurchaseOrders];
GO

DROP INDEX [IX_AssetTypes_Code] ON [AssetTypes];
GO

DELETE FROM [TenantModules]
WHERE [Id] = '1f33cb93-dce0-453c-b7ed-9efd20a90d05';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '4f37828e-cf46-4daf-8e6d-7235bb99f404';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '4fdd066f-98ff-42ea-9a53-ba29abd712b8';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '5502c101-06bd-4cbd-88db-3290d6371a34';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '6bcfab55-7e1a-4848-bf86-de7cc2754a03';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'a6769cc2-0256-4a35-93f4-8452fdb87cb6';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'e24d1390-2e88-4e91-a7d4-03ef7e877164';
SELECT @@ROWCOUNT;

GO

DECLARE @var2 sysname;
SELECT @var2 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrders]') AND [c].[name] = N'ContactPhone');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrders] DROP CONSTRAINT [' + @var2 + '];');
ALTER TABLE [PurchaseOrders] DROP COLUMN [ContactPhone];
GO

DECLARE @var3 sysname;
SELECT @var3 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrders]') AND [c].[name] = N'SupplierAddress');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrders] DROP CONSTRAINT [' + @var3 + '];');
ALTER TABLE [PurchaseOrders] DROP COLUMN [SupplierAddress];
GO

DECLARE @var4 sysname;
SELECT @var4 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrders]') AND [c].[name] = N'SupplierName');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrders] DROP CONSTRAINT [' + @var4 + '];');
ALTER TABLE [PurchaseOrders] DROP COLUMN [SupplierName];
GO

EXEC sp_rename N'[PurchaseOrders].[ContactPerson]', N'SupplierOrderNumber', N'COLUMN';
GO

EXEC sp_rename N'[PurchaseOrders].[ContactEmail]', N'ReferenceNumber', N'COLUMN';
GO

ALTER TABLE [TechnicianSchedules] ADD [EmployeeId] uniqueidentifier NULL;
GO

ALTER TABLE [TechnicianAvailabilities] ADD [EmployeeId] uniqueidentifier NULL;
GO

ALTER TABLE [PurchaseOrders] ADD [DeliveryInstructions] nvarchar(2000) NULL;
GO

ALTER TABLE [PurchaseOrders] ADD [SupplierId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
GO

ALTER TABLE [PurchaseOrders] ADD [Terms] nvarchar(2000) NULL;
GO

ALTER TABLE [PurchaseOrderReceipts] ADD [InspectionDate] datetime2 NULL;
GO

ALTER TABLE [PurchaseOrderReceipts] ADD [InspectionNotes] nvarchar(2000) NULL;
GO

ALTER TABLE [PurchaseOrderReceipts] ADD [InspectionResult] nvarchar(50) NULL;
GO

ALTER TABLE [PurchaseOrderReceipts] ADD [RequiresInspection] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [PurchaseOrderReceiptItems] ADD [QualityNotes] nvarchar(1000) NULL;
GO

ALTER TABLE [PurchaseOrderReceiptItems] ADD [QualityStatus] nvarchar(50) NULL;
GO

ALTER TABLE [PurchaseOrderItems] ADD [ItemDescription] nvarchar(200) NULL;
GO

CREATE TABLE [MaintenanceAttachments] (
    [Id] uniqueidentifier NOT NULL,
    [FileName] nvarchar(100) NOT NULL,
    [FilePath] nvarchar(500) NOT NULL,
    [ContentType] nvarchar(100) NOT NULL,
    [FileSizeBytes] bigint NOT NULL,
    [Description] nvarchar(200) NULL,
    [AttachmentType] int NOT NULL,
    [EntityType] int NOT NULL,
    [EntityId] uniqueidentifier NOT NULL,
    [UploadedDate] datetime2 NOT NULL,
    [UploadedByUserId] uniqueidentifier NOT NULL,
    [IsMainImage] bit NOT NULL,
    [ImageWidth] int NULL,
    [ImageHeight] int NULL,
    [ThumbnailPath] nvarchar(500) NULL,
    [Latitude] float NULL,
    [Longitude] float NULL,
    [LocationDescription] nvarchar(200) NULL,
    [DocumentVersion] nvarchar(50) NULL,
    [IsArchived] bit NOT NULL,
    [ArchivedDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_MaintenanceAttachments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceAttachments_Employees_UploadedByUserId] FOREIGN KEY ([UploadedByUserId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [PurchaseRequisitions] (
    [Id] uniqueidentifier NOT NULL,
    [RequisitionNumber] nvarchar(50) NOT NULL,
    [RequisitionDate] datetime2 NOT NULL,
    [RequestedById] uniqueidentifier NOT NULL,
    [RequiredDate] datetime2 NULL,
    [Status] nvarchar(20) NOT NULL,
    [Priority] nvarchar(50) NOT NULL,
    [Department] nvarchar(100) NULL,
    [CostCenter] nvarchar(100) NULL,
    [Justification] nvarchar(2000) NULL,
    [Notes] nvarchar(2000) NULL,
    [ApprovedById] uniqueidentifier NULL,
    [ApprovedAt] datetime2 NULL,
    [RejectionReason] nvarchar(2000) NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PurchaseRequisitions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PurchaseRequisitions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PurchaseRequisitions_Users_ApprovedById] FOREIGN KEY ([ApprovedById]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_PurchaseRequisitions_Users_RequestedById] FOREIGN KEY ([RequestedById]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Suppliers] (
    [Id] uniqueidentifier NOT NULL,
    [SupplierCode] nvarchar(100) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [SupplierType] nvarchar(50) NOT NULL,
    [Address] nvarchar(500) NULL,
    [City] nvarchar(100) NULL,
    [State] nvarchar(50) NULL,
    [ZipCode] nvarchar(20) NULL,
    [Country] nvarchar(50) NULL,
    [Phone] nvarchar(50) NULL,
    [Email] nvarchar(100) NULL,
    [Website] nvarchar(200) NULL,
    [PrimaryContactName] nvarchar(100) NULL,
    [PrimaryContactTitle] nvarchar(100) NULL,
    [PrimaryContactPhone] nvarchar(50) NULL,
    [PrimaryContactEmail] nvarchar(100) NULL,
    [TaxId] nvarchar(50) NULL,
    [PaymentTerms] nvarchar(100) NULL,
    [ShippingTerms] nvarchar(100) NULL,
    [CreditLimit] decimal(18,4) NULL,
    [LeadTimeDays] int NOT NULL,
    [IsActive] bit NOT NULL,
    [IsPreferred] bit NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [Rating] int NULL,
    [Notes] nvarchar(2000) NULL,
    [LastOrderDate] datetime2 NULL,
    [ContractStartDate] datetime2 NULL,
    [ContractEndDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Suppliers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Suppliers_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [MaintenanceAttachmentTag] (
    [Id] uniqueidentifier NOT NULL,
    [AttachmentId] uniqueidentifier NOT NULL,
    [TagName] nvarchar(50) NOT NULL,
    [TagValue] nvarchar(100) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_MaintenanceAttachmentTag] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceAttachmentTag_MaintenanceAttachments_AttachmentId] FOREIGN KEY ([AttachmentId]) REFERENCES [MaintenanceAttachments] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [PurchaseRequisitionItems] (
    [Id] uniqueidentifier NOT NULL,
    [RequisitionId] uniqueidentifier NOT NULL,
    [InventoryItemId] uniqueidentifier NULL,
    [ItemDescription] nvarchar(200) NOT NULL,
    [Quantity] decimal(18,4) NOT NULL,
    [UnitOfMeasure] nvarchar(20) NOT NULL,
    [EstimatedUnitPrice] decimal(18,2) NOT NULL,
    [LineTotal] decimal(18,2) NOT NULL,
    [RequiredDate] datetime2 NULL,
    [PreferredSupplierId] uniqueidentifier NULL,
    [Notes] nvarchar(1000) NULL,
    [Specifications] nvarchar(1000) NULL,
    [Status] nvarchar(20) NOT NULL,
    [PurchaseOrderId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PurchaseRequisitionItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PurchaseRequisitionItems_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [PurchaseOrders] ([Id]),
    CONSTRAINT [FK_PurchaseRequisitionItems_PurchaseRequisitions_RequisitionId] FOREIGN KEY ([RequisitionId]) REFERENCES [PurchaseRequisitions] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PurchaseRequisitionItems_Suppliers_PreferredSupplierId] FOREIGN KEY ([PreferredSupplierId]) REFERENCES [Suppliers] ([Id]),
    CONSTRAINT [FK_PurchaseRequisitionItems_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [SupplierContacts] (
    [Id] uniqueidentifier NOT NULL,
    [SupplierId] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Title] nvarchar(100) NULL,
    [Department] nvarchar(100) NULL,
    [Phone] nvarchar(50) NULL,
    [Email] nvarchar(100) NULL,
    [IsPrimary] bit NOT NULL,
    [ContactType] nvarchar(50) NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_SupplierContacts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SupplierContacts_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SupplierContacts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [SupplierItemCatalogs] (
    [Id] uniqueidentifier NOT NULL,
    [SupplierId] uniqueidentifier NOT NULL,
    [InventoryItemId] uniqueidentifier NOT NULL,
    [SupplierItemCode] nvarchar(100) NULL,
    [SupplierItemName] nvarchar(200) NULL,
    [Description] nvarchar(1000) NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [UnitOfMeasure] nvarchar(20) NULL,
    [MinimumOrderQuantity] decimal(18,4) NOT NULL,
    [LeadTimeDays] int NOT NULL,
    [IsPreferred] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [EffectiveDate] datetime2 NULL,
    [ExpiryDate] datetime2 NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_SupplierItemCatalogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SupplierItemCatalogs_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SupplierItemCatalogs_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-10T05:56:24.8695870Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-10T05:56:24.8695919Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-10T05:56:24.8695921Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-10T05:56:24.8695923Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T05:56:24.8696163Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T05:56:24.8696191Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T05:56:24.8696198Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T05:56:24.8696206Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T05:56:24.8696214Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T05:56:24.8696220Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T05:56:24.8696225Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T05:56:24.8696237Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T05:56:24.8696245Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T05:56:24.8696253Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T05:56:24.8696259Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T05:56:24.8696265Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T05:56:24.8696273Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T05:56:24.8696286Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T05:56:24.8696298Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-10T05:56:24.8696303Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696347Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696355Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696356Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696357Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696358Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696359Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696360Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696361Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696362Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696363Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696363Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696364Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696365Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696365Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696366Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696367Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696405Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696407Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696408Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696409Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696409Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696410Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696410Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696411Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696412Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696412Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696413Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696414Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696414Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696415Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696415Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696458Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696459Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696461Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696461Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696468Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696469Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696469Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696470Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696471Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696471Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696488Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696489Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-10T05:56:24.8696490Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('207cdcc2-4870-43bb-99bf-6784e1f91224', NULL, '2025-10-10T05:56:24.8695994Z', NULL, NULL, NULL, NULL, NULL, '2025-10-10T05:56:24.8695991Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('5ffa0e52-148a-48a7-9fd0-a8cf69d859a9', NULL, '2025-10-10T05:56:24.8696026Z', NULL, NULL, NULL, NULL, NULL, '2025-10-10T05:56:24.8696025Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('754862ee-42c7-41a9-84d8-753b8fbe188a', NULL, '2025-10-10T05:56:24.8696064Z', NULL, NULL, NULL, NULL, NULL, '2025-10-10T05:56:24.8696063Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('b02de857-b9fa-4438-8d5d-d20e2e5ec2e3', NULL, '2025-10-10T05:56:24.8696051Z', NULL, NULL, NULL, NULL, NULL, '2025-10-10T05:56:24.8696051Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('b940483b-800c-4d4e-86f0-093055414b7a', NULL, '2025-10-10T05:56:24.8696039Z', NULL, NULL, NULL, NULL, NULL, '2025-10-10T05:56:24.8696039Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('c6094034-7737-4ec7-9a28-55f53ed7c186', NULL, '2025-10-10T05:56:24.8696076Z', NULL, NULL, NULL, NULL, NULL, '2025-10-10T05:56:24.8696076Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('e12d49c9-9fc9-475c-9057-8de080a832db', NULL, '2025-10-10T05:56:24.8696009Z', NULL, NULL, NULL, NULL, NULL, '2025-10-10T05:56:24.8696009Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-10-10T05:56:24.8695627Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_TechnicianShifts_IsActive] ON [TechnicianShifts] ([IsActive]);
GO

CREATE INDEX [IX_TechnicianShifts_StartTime] ON [TechnicianShifts] ([StartTime]);
GO

CREATE INDEX [IX_TechnicianSchedules_EmployeeId] ON [TechnicianSchedules] ([EmployeeId]);
GO

CREATE INDEX [IX_TechnicianSchedules_ScheduleType] ON [TechnicianSchedules] ([ScheduleType]);
GO

CREATE INDEX [IX_TechnicianSchedules_StartDate] ON [TechnicianSchedules] ([StartDate]);
GO

CREATE INDEX [IX_TechnicianSchedules_Status] ON [TechnicianSchedules] ([Status]);
GO

CREATE INDEX [IX_TechnicianAvailabilities_AvailabilityType] ON [TechnicianAvailabilities] ([AvailabilityType]);
GO

CREATE INDEX [IX_TechnicianAvailabilities_EmployeeId] ON [TechnicianAvailabilities] ([EmployeeId]);
GO

CREATE INDEX [IX_TechnicianAvailabilities_Reason] ON [TechnicianAvailabilities] ([Reason]);
GO

CREATE INDEX [IX_TechnicianAvailabilities_StartDate] ON [TechnicianAvailabilities] ([StartDate]);
GO

CREATE INDEX [IX_PurchaseOrders_SupplierId] ON [PurchaseOrders] ([SupplierId]);
GO

CREATE UNIQUE INDEX [IX_AssetTypes_Code] ON [AssetTypes] ([Code]);
GO

CREATE INDEX [IX_AssetTypeFields_AssetTypeId] ON [AssetTypeFields] ([AssetTypeId]);
GO

CREATE INDEX [IX_AssetTypeFields_FieldName] ON [AssetTypeFields] ([FieldName]);
GO

CREATE INDEX [IX_MaintenanceAttachments_UploadedByUserId] ON [MaintenanceAttachments] ([UploadedByUserId]);
GO

CREATE INDEX [IX_MaintenanceAttachmentTag_AttachmentId] ON [MaintenanceAttachmentTag] ([AttachmentId]);
GO

CREATE INDEX [IX_PurchaseRequisitionItems_PreferredSupplierId] ON [PurchaseRequisitionItems] ([PreferredSupplierId]);
GO

CREATE INDEX [IX_PurchaseRequisitionItems_PurchaseOrderId] ON [PurchaseRequisitionItems] ([PurchaseOrderId]);
GO

CREATE INDEX [IX_PurchaseRequisitionItems_RequisitionId] ON [PurchaseRequisitionItems] ([RequisitionId]);
GO

CREATE INDEX [IX_PurchaseRequisitionItems_TenantId] ON [PurchaseRequisitionItems] ([TenantId]);
GO

CREATE INDEX [IX_PurchaseRequisitions_ApprovedById] ON [PurchaseRequisitions] ([ApprovedById]);
GO

CREATE INDEX [IX_PurchaseRequisitions_RequestedById] ON [PurchaseRequisitions] ([RequestedById]);
GO

CREATE INDEX [IX_PurchaseRequisitions_TenantId] ON [PurchaseRequisitions] ([TenantId]);
GO

CREATE INDEX [IX_SupplierContacts_SupplierId] ON [SupplierContacts] ([SupplierId]);
GO

CREATE INDEX [IX_SupplierContacts_TenantId] ON [SupplierContacts] ([TenantId]);
GO

CREATE INDEX [IX_SupplierItemCatalogs_SupplierId] ON [SupplierItemCatalogs] ([SupplierId]);
GO

CREATE INDEX [IX_SupplierItemCatalogs_TenantId] ON [SupplierItemCatalogs] ([TenantId]);
GO

CREATE INDEX [IX_Suppliers_TenantId] ON [Suppliers] ([TenantId]);
GO

ALTER TABLE [PurchaseOrders] ADD CONSTRAINT [FK_PurchaseOrders_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [TechnicianAvailabilities] ADD CONSTRAINT [FK_TechnicianAvailabilities_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]);
GO

ALTER TABLE [TechnicianSchedules] ADD CONSTRAINT [FK_TechnicianSchedules_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251010055626_MaintenancePropertyFixes', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '207cdcc2-4870-43bb-99bf-6784e1f91224';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '5ffa0e52-148a-48a7-9fd0-a8cf69d859a9';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '754862ee-42c7-41a9-84d8-753b8fbe188a';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'b02de857-b9fa-4438-8d5d-d20e2e5ec2e3';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'b940483b-800c-4d4e-86f0-093055414b7a';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'c6094034-7737-4ec7-9a28-55f53ed7c186';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'e12d49c9-9fc9-475c-9057-8de080a832db';
SELECT @@ROWCOUNT;

GO

ALTER TABLE [MaintenanceAssetCategories] ADD [MaintenanceFrequency] nvarchar(50) NULL;
GO

ALTER TABLE [MaintenanceAssetCategories] ADD [MaintenanceScheduleType] nvarchar(20) NOT NULL DEFAULT N'';
GO

ALTER TABLE [MaintenanceAssetCategories] ADD [MaintenanceType] nvarchar(20) NOT NULL DEFAULT N'';
GO

ALTER TABLE [MaintenanceAssetCategories] ADD [MaintenanceUnit] nvarchar(20) NULL;
GO

ALTER TABLE [MaintenanceAssetCategories] ADD [MaintenanceValue] float NULL;
GO

ALTER TABLE [MaintenanceAssetCategories] ADD [ParentCategoryId] uniqueidentifier NULL;
GO

ALTER TABLE [MaintenanceAssetCategories] ADD [SecondaryMaintenanceFrequency] nvarchar(50) NULL;
GO

ALTER TABLE [MaintenanceAssetCategories] ADD [SecondaryMaintenanceType] nvarchar(20) NULL;
GO

ALTER TABLE [MaintenanceAssetCategories] ADD [SecondaryMaintenanceUnit] nvarchar(20) NULL;
GO

ALTER TABLE [MaintenanceAssetCategories] ADD [SecondaryMaintenanceValue] float NULL;
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-12T02:33:03.8392664Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-12T02:33:03.8392721Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-12T02:33:03.8392723Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-12T02:33:03.8392725Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:33:03.8392940Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:33:03.8392953Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:33:03.8392959Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:33:03.8392963Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:33:03.8392977Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:33:03.8392984Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:33:03.8392988Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:33:03.8392993Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:33:03.8393006Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:33:03.8393013Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:33:03.8393018Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:33:03.8393023Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:33:03.8393031Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:33:03.8393036Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:33:03.8393040Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:33:03.8393045Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393081Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393089Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393090Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393091Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393092Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393093Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393094Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393095Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393095Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393097Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393097Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393098Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393099Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393099Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393100Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393100Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393169Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393171Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393172Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393172Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393173Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393174Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393174Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393175Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393176Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393176Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393177Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393177Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393178Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393178Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393179Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393225Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393226Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393227Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393228Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393228Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393229Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393230Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393230Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393259Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393260Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393270Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393271Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:33:03.8393272Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('1a513e6f-1c49-4608-b05c-5cb090b9c950', NULL, '2025-10-12T02:33:03.8392819Z', NULL, NULL, NULL, NULL, NULL, '2025-10-12T02:33:03.8392819Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('2d4fd1f2-2ea5-4b40-9991-8b343020efb2', NULL, '2025-10-12T02:33:03.8392858Z', NULL, NULL, NULL, NULL, NULL, '2025-10-12T02:33:03.8392858Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('35d4a336-968d-44b5-a660-95a99a48383b', NULL, '2025-10-12T02:33:03.8392833Z', NULL, NULL, NULL, NULL, NULL, '2025-10-12T02:33:03.8392833Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('68dda6e2-b571-4809-8fc1-b63b064be5c9', NULL, '2025-10-12T02:33:03.8392806Z', NULL, NULL, NULL, NULL, NULL, '2025-10-12T02:33:03.8392806Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('7dda26dd-0572-4a62-b1d5-8a0f51f9ed86', NULL, '2025-10-12T02:33:03.8392846Z', NULL, NULL, NULL, NULL, NULL, '2025-10-12T02:33:03.8392846Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('958d4b01-45e5-4218-92c3-bd3fa4aff025', NULL, '2025-10-12T02:33:03.8392787Z', NULL, NULL, NULL, NULL, NULL, '2025-10-12T02:33:03.8392785Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('c5ae771a-3007-4860-8943-af38671e6be1', NULL, '2025-10-12T02:33:03.8392870Z', NULL, NULL, NULL, NULL, NULL, '2025-10-12T02:33:03.8392870Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-10-12T02:33:03.8392465Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_SupplierItemCatalogs_InventoryItemId] ON [SupplierItemCatalogs] ([InventoryItemId]);
GO

CREATE INDEX [IX_PurchaseRequisitionItems_InventoryItemId] ON [PurchaseRequisitionItems] ([InventoryItemId]);
GO

CREATE INDEX [IX_MaintenanceAssetCategories_ParentCategoryId] ON [MaintenanceAssetCategories] ([ParentCategoryId]);
GO

ALTER TABLE [MaintenanceAssetCategories] ADD CONSTRAINT [FK_MaintenanceAssetCategories_MaintenanceAssetCategories_ParentCategoryId] FOREIGN KEY ([ParentCategoryId]) REFERENCES [MaintenanceAssetCategories] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [PurchaseOrderItems] ADD CONSTRAINT [FK_PurchaseOrderItems_InventoryItems_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [InventoryItems] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [PurchaseRequisitionItems] ADD CONSTRAINT [FK_PurchaseRequisitionItems_InventoryItems_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [InventoryItems] ([Id]);
GO

ALTER TABLE [SupplierItemCatalogs] ADD CONSTRAINT [FK_SupplierItemCatalogs_InventoryItems_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [InventoryItems] ([Id]) ON DELETE CASCADE;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251012023305_AddMaintenanceAssetCategoryMultiCriteriaFields', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '1a513e6f-1c49-4608-b05c-5cb090b9c950';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '2d4fd1f2-2ea5-4b40-9991-8b343020efb2';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '35d4a336-968d-44b5-a660-95a99a48383b';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '68dda6e2-b571-4809-8fc1-b63b064be5c9';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '7dda26dd-0572-4a62-b1d5-8a0f51f9ed86';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '958d4b01-45e5-4218-92c3-bd3fa4aff025';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'c5ae771a-3007-4860-8943-af38671e6be1';
SELECT @@ROWCOUNT;

GO

CREATE TABLE [WorkflowDefinitions] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [EntityType] nvarchar(50) NOT NULL,
    [Version] int NOT NULL,
    [IsActive] bit NOT NULL,
    [Configuration] nvarchar(max) NULL,
    [CreatedDate] datetime2 NOT NULL,
    [LastModifiedDate] datetime2 NULL,
    [CreatedById] uniqueidentifier NOT NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkflowDefinitions] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [WorkflowSteps] (
    [Id] uniqueidentifier NOT NULL,
    [WorkflowDefinitionId] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [StepType] nvarchar(50) NOT NULL,
    [Order] int NOT NULL,
    [IsRequired] bit NOT NULL,
    [RequiredRole] nvarchar(100) NULL,
    [EstimatedHours] float NULL,
    [Configuration] nvarchar(max) NULL,
    [CreatedDate] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkflowSteps] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowSteps_WorkflowDefinitions_WorkflowDefinitionId] FOREIGN KEY ([WorkflowDefinitionId]) REFERENCES [WorkflowDefinitions] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WorkflowInstances] (
    [Id] uniqueidentifier NOT NULL,
    [WorkflowDefinitionId] uniqueidentifier NOT NULL,
    [EntityId] uniqueidentifier NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [CurrentStepId] uniqueidentifier NULL,
    [InitiatedById] uniqueidentifier NOT NULL,
    [StartedDate] datetime2 NOT NULL,
    [CompletedDate] datetime2 NULL,
    [LastActivityDate] datetime2 NOT NULL,
    [DataContext] nvarchar(max) NULL,
    [Notes] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkflowInstances] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowInstances_WorkflowDefinitions_WorkflowDefinitionId] FOREIGN KEY ([WorkflowDefinitionId]) REFERENCES [WorkflowDefinitions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkflowInstances_WorkflowSteps_CurrentStepId] FOREIGN KEY ([CurrentStepId]) REFERENCES [WorkflowSteps] ([Id])
);
GO

CREATE TABLE [WorkflowTransitions] (
    [Id] uniqueidentifier NOT NULL,
    [FromStepId] uniqueidentifier NOT NULL,
    [ToStepId] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [Condition] nvarchar(max) NULL,
    [IsDefault] bit NOT NULL,
    [Priority] int NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkflowTransitions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowTransitions_WorkflowSteps_FromStepId] FOREIGN KEY ([FromStepId]) REFERENCES [WorkflowSteps] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkflowTransitions_WorkflowSteps_ToStepId] FOREIGN KEY ([ToStepId]) REFERENCES [WorkflowSteps] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [WorkflowStepInstances] (
    [Id] uniqueidentifier NOT NULL,
    [WorkflowInstanceId] uniqueidentifier NOT NULL,
    [WorkflowStepId] uniqueidentifier NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [AssignedToId] uniqueidentifier NULL,
    [CreatedDate] datetime2 NOT NULL,
    [StartedDate] datetime2 NULL,
    [CompletedDate] datetime2 NULL,
    [DueDate] datetime2 NULL,
    [ResultData] nvarchar(max) NULL,
    [Comments] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkflowStepInstances] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowStepInstances_WorkflowInstances_WorkflowInstanceId] FOREIGN KEY ([WorkflowInstanceId]) REFERENCES [WorkflowInstances] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_WorkflowStepInstances_WorkflowSteps_WorkflowStepId] FOREIGN KEY ([WorkflowStepId]) REFERENCES [WorkflowSteps] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [WorkflowActivityLogs] (
    [Id] uniqueidentifier NOT NULL,
    [WorkflowInstanceId] uniqueidentifier NOT NULL,
    [WorkflowStepInstanceId] uniqueidentifier NULL,
    [ActivityType] nvarchar(50) NOT NULL,
    [Description] nvarchar(500) NOT NULL,
    [UserId] uniqueidentifier NULL,
    [Timestamp] datetime2 NOT NULL,
    [ActivityData] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkflowActivityLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowActivityLogs_WorkflowInstances_WorkflowInstanceId] FOREIGN KEY ([WorkflowInstanceId]) REFERENCES [WorkflowInstances] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_WorkflowActivityLogs_WorkflowStepInstances_WorkflowStepInstanceId] FOREIGN KEY ([WorkflowStepInstanceId]) REFERENCES [WorkflowStepInstances] ([Id])
);
GO

CREATE TABLE [WorkflowApprovals] (
    [Id] uniqueidentifier NOT NULL,
    [WorkflowStepInstanceId] uniqueidentifier NOT NULL,
    [ApproverId] uniqueidentifier NOT NULL,
    [ApproverRole] nvarchar(100) NULL,
    [Status] nvarchar(50) NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    [ResponseDate] datetime2 NULL,
    [DueDate] datetime2 NULL,
    [Comments] nvarchar(1000) NULL,
    [Priority] int NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkflowApprovals] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowApprovals_WorkflowStepInstances_WorkflowStepInstanceId] FOREIGN KEY ([WorkflowStepInstanceId]) REFERENCES [WorkflowStepInstances] ([Id]) ON DELETE CASCADE
);
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-12T02:53:51.0971902Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-12T02:53:51.0971960Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-12T02:53:51.0971962Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-12T02:53:51.0971964Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:53:51.0972199Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:53:51.0972216Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:53:51.0972221Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:53:51.0972226Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:53:51.0972240Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:53:51.0972246Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:53:51.0972251Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:53:51.0972257Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:53:51.0972264Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:53:51.0972274Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:53:51.0972279Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:53:51.0972283Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:53:51.0972290Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:53:51.0972295Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:53:51.0972300Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-12T02:53:51.0972305Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972345Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972349Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972350Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972351Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972351Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972353Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972354Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972354Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972361Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972362Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972363Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972364Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972364Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972365Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972365Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972366Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972434Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972436Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972437Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972438Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972438Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972439Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972440Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972440Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972441Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972442Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972442Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972443Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972443Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972444Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972445Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972505Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972506Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972507Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972508Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972509Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972509Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972510Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972510Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972511Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972512Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972523Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972524Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-12T02:53:51.0972524Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('0aeb65ae-ec96-4dc9-8a71-73fd19561d06', NULL, '2025-10-12T02:53:51.0972056Z', NULL, NULL, NULL, NULL, NULL, '2025-10-12T02:53:51.0972056Z', CAST(0 AS bit), N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('7ad80716-15a1-418b-915c-abd613c8288c', NULL, '2025-10-12T02:53:51.0972102Z', NULL, NULL, NULL, NULL, NULL, '2025-10-12T02:53:51.0972101Z', CAST(0 AS bit), N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('880503f7-e1da-4d09-8b8a-fe6830020a52', NULL, '2025-10-12T02:53:51.0972114Z', NULL, NULL, NULL, NULL, NULL, '2025-10-12T02:53:51.0972113Z', CAST(0 AS bit), N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('a5675b88-0d25-4565-93c1-0aecb492df72', NULL, '2025-10-12T02:53:51.0972069Z', NULL, NULL, NULL, NULL, NULL, '2025-10-12T02:53:51.0972068Z', CAST(0 AS bit), N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('c350d359-7c4d-42f4-bcac-6a312702b626', NULL, '2025-10-12T02:53:51.0972042Z', NULL, NULL, NULL, NULL, NULL, '2025-10-12T02:53:51.0972042Z', CAST(0 AS bit), N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('d6eb8bdc-02f3-403d-b08e-8e32c700ddbb', NULL, '2025-10-12T02:53:51.0972028Z', NULL, NULL, NULL, NULL, NULL, '2025-10-12T02:53:51.0972025Z', CAST(0 AS bit), N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('dd947ba9-556f-4356-9fa9-9f95fb7b1638', NULL, '2025-10-12T02:53:51.0972080Z', NULL, NULL, NULL, NULL, NULL, '2025-10-12T02:53:51.0972080Z', CAST(0 AS bit), N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-10-12T02:53:51.0971666Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_WorkflowActivityLogs_WorkflowInstanceId] ON [WorkflowActivityLogs] ([WorkflowInstanceId]);
GO

CREATE INDEX [IX_WorkflowActivityLogs_WorkflowStepInstanceId] ON [WorkflowActivityLogs] ([WorkflowStepInstanceId]);
GO

CREATE INDEX [IX_WorkflowApprovals_WorkflowStepInstanceId] ON [WorkflowApprovals] ([WorkflowStepInstanceId]);
GO

CREATE INDEX [IX_WorkflowInstances_CurrentStepId] ON [WorkflowInstances] ([CurrentStepId]);
GO

CREATE INDEX [IX_WorkflowInstances_WorkflowDefinitionId] ON [WorkflowInstances] ([WorkflowDefinitionId]);
GO

CREATE INDEX [IX_WorkflowStepInstances_Status] ON [WorkflowStepInstances] ([Status]);
GO

CREATE INDEX [IX_WorkflowStepInstances_WorkflowInstanceId] ON [WorkflowStepInstances] ([WorkflowInstanceId]);
GO

CREATE INDEX [IX_WorkflowStepInstances_WorkflowStepId] ON [WorkflowStepInstances] ([WorkflowStepId]);
GO

CREATE INDEX [IX_WorkflowSteps_WorkflowDefinitionId] ON [WorkflowSteps] ([WorkflowDefinitionId]);
GO

CREATE INDEX [IX_WorkflowTransitions_FromStepId_ToStepId] ON [WorkflowTransitions] ([FromStepId], [ToStepId]);
GO

CREATE INDEX [IX_WorkflowTransitions_IsDefault] ON [WorkflowTransitions] ([IsDefault]);
GO

CREATE INDEX [IX_WorkflowTransitions_Priority] ON [WorkflowTransitions] ([Priority]);
GO

CREATE INDEX [IX_WorkflowTransitions_ToStepId] ON [WorkflowTransitions] ([ToStepId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251012025352_AddWorkflowTables', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [WorkflowActivityLogs] DROP CONSTRAINT [FK_WorkflowActivityLogs_WorkflowStepInstances_WorkflowStepInstanceId];
GO

ALTER TABLE [WorkflowApprovals] DROP CONSTRAINT [FK_WorkflowApprovals_WorkflowStepInstances_WorkflowStepInstanceId];
GO

DELETE FROM [TenantModules]
WHERE [Id] = '0aeb65ae-ec96-4dc9-8a71-73fd19561d06';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '7ad80716-15a1-418b-915c-abd613c8288c';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '880503f7-e1da-4d09-8b8a-fe6830020a52';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'a5675b88-0d25-4565-93c1-0aecb492df72';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'c350d359-7c4d-42f4-bcac-6a312702b626';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'd6eb8bdc-02f3-403d-b08e-8e32c700ddbb';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'dd947ba9-556f-4356-9fa9-9f95fb7b1638';
SELECT @@ROWCOUNT;

GO

DECLARE @var5 sysname;
SELECT @var5 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkflowDefinitions]') AND [c].[name] = N'EntityType');
IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [WorkflowDefinitions] DROP CONSTRAINT [' + @var5 + '];');
ALTER TABLE [WorkflowDefinitions] DROP COLUMN [EntityType];
GO

DECLARE @var6 sysname;
SELECT @var6 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceSchedules]') AND [c].[name] = N'ConditionParameters');
IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceSchedules] DROP CONSTRAINT [' + @var6 + '];');
ALTER TABLE [MaintenanceSchedules] DROP COLUMN [ConditionParameters];
GO

DECLARE @var7 sysname;
SELECT @var7 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceSchedules]') AND [c].[name] = N'DayOfMonth');
IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceSchedules] DROP CONSTRAINT [' + @var7 + '];');
ALTER TABLE [MaintenanceSchedules] DROP COLUMN [DayOfMonth];
GO

DECLARE @var8 sysname;
SELECT @var8 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceSchedules]') AND [c].[name] = N'IntervalValue');
IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceSchedules] DROP CONSTRAINT [' + @var8 + '];');
ALTER TABLE [MaintenanceSchedules] DROP COLUMN [IntervalValue];
GO

DECLARE @var9 sysname;
SELECT @var9 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceSchedules]') AND [c].[name] = N'PartsTemplate');
IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceSchedules] DROP CONSTRAINT [' + @var9 + '];');
ALTER TABLE [MaintenanceSchedules] DROP COLUMN [PartsTemplate];
GO

DECLARE @var10 sysname;
SELECT @var10 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceSchedules]') AND [c].[name] = N'PreferredTime');
IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceSchedules] DROP CONSTRAINT [' + @var10 + '];');
ALTER TABLE [MaintenanceSchedules] DROP COLUMN [PreferredTime];
GO

DECLARE @var11 sysname;
SELECT @var11 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceSchedules]') AND [c].[name] = N'StartDate');
IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceSchedules] DROP CONSTRAINT [' + @var11 + '];');
ALTER TABLE [MaintenanceSchedules] DROP COLUMN [StartDate];
GO

DECLARE @var12 sysname;
SELECT @var12 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceSchedules]') AND [c].[name] = N'TaskTemplate');
IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceSchedules] DROP CONSTRAINT [' + @var12 + '];');
ALTER TABLE [MaintenanceSchedules] DROP COLUMN [TaskTemplate];
GO

DECLARE @var13 sysname;
SELECT @var13 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceSchedules]') AND [c].[name] = N'UsageInterval');
IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceSchedules] DROP CONSTRAINT [' + @var13 + '];');
ALTER TABLE [MaintenanceSchedules] DROP COLUMN [UsageInterval];
GO

DECLARE @var14 sysname;
SELECT @var14 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceSchedules]') AND [c].[name] = N'UsageUnit');
IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceSchedules] DROP CONSTRAINT [' + @var14 + '];');
ALTER TABLE [MaintenanceSchedules] DROP COLUMN [UsageUnit];
GO

DECLARE @var15 sysname;
SELECT @var15 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceSchedules]') AND [c].[name] = N'WorkOrderDescription');
IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceSchedules] DROP CONSTRAINT [' + @var15 + '];');
ALTER TABLE [MaintenanceSchedules] DROP COLUMN [WorkOrderDescription];
GO

DECLARE @var16 sysname;
SELECT @var16 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceSchedules]') AND [c].[name] = N'WorkOrderTitle');
IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceSchedules] DROP CONSTRAINT [' + @var16 + '];');
ALTER TABLE [MaintenanceSchedules] DROP COLUMN [WorkOrderTitle];
GO

EXEC sp_rename N'[WorkflowTransitions].[CreatedDate]', N'CreatedAt', N'COLUMN';
GO

EXEC sp_rename N'[WorkflowSteps].[CreatedDate]', N'CreatedAt', N'COLUMN';
GO

EXEC sp_rename N'[WorkflowInstances].[LastActivityDate]', N'CreatedDate', N'COLUMN';
GO

EXEC sp_rename N'[WorkflowDefinitions].[LastModifiedDate]', N'UpdatedAt', N'COLUMN';
GO

EXEC sp_rename N'[WorkflowDefinitions].[CreatedDate]', N'CreatedAt', N'COLUMN';
GO

EXEC sp_rename N'[WorkflowApprovals].[WorkflowStepInstanceId]', N'StepInstanceId', N'COLUMN';
GO

EXEC sp_rename N'[WorkflowApprovals].[ResponseDate]', N'UpdatedAt', N'COLUMN';
GO

EXEC sp_rename N'[WorkflowApprovals].[CreatedDate]', N'RequestedDate', N'COLUMN';
GO

EXEC sp_rename N'[WorkflowApprovals].[IX_WorkflowApprovals_WorkflowStepInstanceId]', N'IX_WorkflowApprovals_StepInstanceId', N'INDEX';
GO

EXEC sp_rename N'[WorkflowActivityLogs].[WorkflowStepInstanceId]', N'StepInstanceId', N'COLUMN';
GO

EXEC sp_rename N'[WorkflowActivityLogs].[UserId]', N'PerformedById', N'COLUMN';
GO

EXEC sp_rename N'[WorkflowActivityLogs].[Timestamp]', N'CreatedAt', N'COLUMN';
GO

EXEC sp_rename N'[WorkflowActivityLogs].[ActivityData]', N'UpdatedBy', N'COLUMN';
GO

EXEC sp_rename N'[WorkflowActivityLogs].[IX_WorkflowActivityLogs_WorkflowStepInstanceId]', N'IX_WorkflowActivityLogs_StepInstanceId', N'INDEX';
GO

EXEC sp_rename N'[MaintenanceSchedules].[LeadTimeDays]', N'FrequencyValue', N'COLUMN';
GO

EXEC sp_rename N'[MaintenanceSchedules].[EndDate]', N'LastProcessedDate', N'COLUMN';
GO

EXEC sp_rename N'[MaintenanceSchedules].[DayOfWeek]', N'AdvanceNotificationDays', N'COLUMN';
GO

ALTER TABLE [WorkStations] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkStations] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkOrderTypes] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkOrderTypes] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkOrderTasks] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkOrderTasks] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkOrders] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkOrders] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkOrders] ADD [TechnicianId] uniqueidentifier NULL;
GO

ALTER TABLE [WorkOrderParts] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkOrderParts] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkOrderLabor] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkOrderLabor] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkOrderDocuments] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkOrderDocuments] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkOrderComments] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkOrderComments] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkflowTransitions] ADD [CreatedBy] nvarchar(max) NULL;
GO

ALTER TABLE [WorkflowTransitions] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkflowTransitions] ADD [DeletedAt] datetime2 NULL;
GO

ALTER TABLE [WorkflowTransitions] ADD [DeletedBy] nvarchar(max) NULL;
GO

ALTER TABLE [WorkflowTransitions] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [WorkflowTransitions] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkflowTransitions] ADD [UpdatedAt] datetime2 NULL;
GO

ALTER TABLE [WorkflowTransitions] ADD [UpdatedBy] nvarchar(max) NULL;
GO

ALTER TABLE [WorkflowTransitions] ADD [WorkflowDefinitionId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
GO

DECLARE @var17 sysname;
SELECT @var17 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkflowSteps]') AND [c].[name] = N'StepType');
IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [WorkflowSteps] DROP CONSTRAINT [' + @var17 + '];');
ALTER TABLE [WorkflowSteps] ALTER COLUMN [StepType] int NOT NULL;
GO

DECLARE @var18 sysname;
SELECT @var18 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkflowSteps]') AND [c].[name] = N'EstimatedHours');
IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [WorkflowSteps] DROP CONSTRAINT [' + @var18 + '];');
ALTER TABLE [WorkflowSteps] ALTER COLUMN [EstimatedHours] decimal(5,2) NULL;
GO

ALTER TABLE [WorkflowSteps] ADD [AssignmentConfiguration] nvarchar(max) NULL;
GO

ALTER TABLE [WorkflowSteps] ADD [AssignmentType] nvarchar(50) NULL;
GO

ALTER TABLE [WorkflowSteps] ADD [CreatedBy] nvarchar(max) NULL;
GO

ALTER TABLE [WorkflowSteps] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkflowSteps] ADD [DeletedAt] datetime2 NULL;
GO

ALTER TABLE [WorkflowSteps] ADD [DeletedBy] nvarchar(max) NULL;
GO

ALTER TABLE [WorkflowSteps] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [WorkflowSteps] ADD [IsEndStep] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [WorkflowSteps] ADD [IsStartStep] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [WorkflowSteps] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkflowSteps] ADD [UpdatedAt] datetime2 NULL;
GO

ALTER TABLE [WorkflowSteps] ADD [UpdatedBy] nvarchar(max) NULL;
GO

DROP INDEX [IX_WorkflowStepInstances_Status] ON [WorkflowStepInstances];
DECLARE @var19 sysname;
SELECT @var19 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkflowStepInstances]') AND [c].[name] = N'Status');
IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [WorkflowStepInstances] DROP CONSTRAINT [' + @var19 + '];');
ALTER TABLE [WorkflowStepInstances] ALTER COLUMN [Status] int NOT NULL;
CREATE INDEX [IX_WorkflowStepInstances_Status] ON [WorkflowStepInstances] ([Status]);
GO

ALTER TABLE [WorkflowStepInstances] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
GO

ALTER TABLE [WorkflowStepInstances] ADD [CreatedBy] nvarchar(max) NULL;
GO

ALTER TABLE [WorkflowStepInstances] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkflowStepInstances] ADD [DeletedAt] datetime2 NULL;
GO

ALTER TABLE [WorkflowStepInstances] ADD [DeletedBy] nvarchar(max) NULL;
GO

ALTER TABLE [WorkflowStepInstances] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [WorkflowStepInstances] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkflowStepInstances] ADD [RetryCount] int NOT NULL DEFAULT 0;
GO

ALTER TABLE [WorkflowStepInstances] ADD [UpdatedAt] datetime2 NULL;
GO

ALTER TABLE [WorkflowStepInstances] ADD [UpdatedBy] nvarchar(max) NULL;
GO

DECLARE @var20 sysname;
SELECT @var20 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkflowInstances]') AND [c].[name] = N'Status');
IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [WorkflowInstances] DROP CONSTRAINT [' + @var20 + '];');
ALTER TABLE [WorkflowInstances] ALTER COLUMN [Status] int NOT NULL;
GO

DECLARE @var21 sysname;
SELECT @var21 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkflowInstances]') AND [c].[name] = N'StartedDate');
IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [WorkflowInstances] DROP CONSTRAINT [' + @var21 + '];');
ALTER TABLE [WorkflowInstances] ALTER COLUMN [StartedDate] datetime2 NULL;
GO

ALTER TABLE [WorkflowInstances] ADD [CancelledDate] datetime2 NULL;
GO

ALTER TABLE [WorkflowInstances] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
GO

ALTER TABLE [WorkflowInstances] ADD [CreatedBy] nvarchar(max) NULL;
GO

ALTER TABLE [WorkflowInstances] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkflowInstances] ADD [Data] nvarchar(max) NULL;
GO

ALTER TABLE [WorkflowInstances] ADD [DeletedAt] datetime2 NULL;
GO

ALTER TABLE [WorkflowInstances] ADD [DeletedBy] nvarchar(max) NULL;
GO

ALTER TABLE [WorkflowInstances] ADD [EntityTypeId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
GO

ALTER TABLE [WorkflowInstances] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [WorkflowInstances] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkflowInstances] ADD [Priority] int NOT NULL DEFAULT 0;
GO

ALTER TABLE [WorkflowInstances] ADD [StartedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkflowInstances] ADD [UpdatedAt] datetime2 NULL;
GO

ALTER TABLE [WorkflowInstances] ADD [UpdatedBy] nvarchar(max) NULL;
GO

DECLARE @var22 sysname;
SELECT @var22 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkflowDefinitions]') AND [c].[name] = N'CreatedById');
IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [WorkflowDefinitions] DROP CONSTRAINT [' + @var22 + '];');
ALTER TABLE [WorkflowDefinitions] ALTER COLUMN [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkflowDefinitions] ADD [CreatedBy] nvarchar(max) NULL;
GO

ALTER TABLE [WorkflowDefinitions] ADD [DeletedAt] datetime2 NULL;
GO

ALTER TABLE [WorkflowDefinitions] ADD [DeletedBy] nvarchar(max) NULL;
GO

ALTER TABLE [WorkflowDefinitions] ADD [EntityTypeId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
GO

ALTER TABLE [WorkflowDefinitions] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [WorkflowDefinitions] ADD [UpdatedBy] nvarchar(max) NULL;
GO

DECLARE @var23 sysname;
SELECT @var23 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkflowApprovals]') AND [c].[name] = N'Status');
IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [WorkflowApprovals] DROP CONSTRAINT [' + @var23 + '];');
ALTER TABLE [WorkflowApprovals] ALTER COLUMN [Status] int NOT NULL;
GO

DECLARE @var24 sysname;
SELECT @var24 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkflowApprovals]') AND [c].[name] = N'Comments');
IF @var24 IS NOT NULL EXEC(N'ALTER TABLE [WorkflowApprovals] DROP CONSTRAINT [' + @var24 + '];');
ALTER TABLE [WorkflowApprovals] ALTER COLUMN [Comments] nvarchar(max) NULL;
GO

DECLARE @var25 sysname;
SELECT @var25 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkflowApprovals]') AND [c].[name] = N'ApproverId');
IF @var25 IS NOT NULL EXEC(N'ALTER TABLE [WorkflowApprovals] DROP CONSTRAINT [' + @var25 + '];');
ALTER TABLE [WorkflowApprovals] ALTER COLUMN [ApproverId] uniqueidentifier NULL;
GO

ALTER TABLE [WorkflowApprovals] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
GO

ALTER TABLE [WorkflowApprovals] ADD [CreatedBy] nvarchar(max) NULL;
GO

ALTER TABLE [WorkflowApprovals] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkflowApprovals] ADD [DeletedAt] datetime2 NULL;
GO

ALTER TABLE [WorkflowApprovals] ADD [DeletedBy] nvarchar(max) NULL;
GO

ALTER TABLE [WorkflowApprovals] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [WorkflowApprovals] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkflowApprovals] ADD [ProcessedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkflowApprovals] ADD [ProcessedDate] datetime2 NULL;
GO

ALTER TABLE [WorkflowApprovals] ADD [UpdatedBy] nvarchar(max) NULL;
GO

DECLARE @var26 sysname;
SELECT @var26 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkflowActivityLogs]') AND [c].[name] = N'Description');
IF @var26 IS NOT NULL EXEC(N'ALTER TABLE [WorkflowActivityLogs] DROP CONSTRAINT [' + @var26 + '];');
ALTER TABLE [WorkflowActivityLogs] ALTER COLUMN [Description] nvarchar(max) NULL;
GO

DECLARE @var27 sysname;
SELECT @var27 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkflowActivityLogs]') AND [c].[name] = N'ActivityType');
IF @var27 IS NOT NULL EXEC(N'ALTER TABLE [WorkflowActivityLogs] DROP CONSTRAINT [' + @var27 + '];');
ALTER TABLE [WorkflowActivityLogs] ALTER COLUMN [ActivityType] int NOT NULL;
GO

ALTER TABLE [WorkflowActivityLogs] ADD [ActivityDate] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
GO

ALTER TABLE [WorkflowActivityLogs] ADD [CreatedBy] nvarchar(max) NULL;
GO

ALTER TABLE [WorkflowActivityLogs] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkflowActivityLogs] ADD [Data] nvarchar(max) NULL;
GO

ALTER TABLE [WorkflowActivityLogs] ADD [DeletedAt] datetime2 NULL;
GO

ALTER TABLE [WorkflowActivityLogs] ADD [DeletedBy] nvarchar(max) NULL;
GO

ALTER TABLE [WorkflowActivityLogs] ADD [IpAddress] nvarchar(45) NULL;
GO

ALTER TABLE [WorkflowActivityLogs] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [WorkflowActivityLogs] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkflowActivityLogs] ADD [Title] nvarchar(200) NOT NULL DEFAULT N'';
GO

ALTER TABLE [WorkflowActivityLogs] ADD [UpdatedAt] datetime2 NULL;
GO

ALTER TABLE [WorkflowActivityLogs] ADD [UserAgent] nvarchar(500) NULL;
GO

ALTER TABLE [Warehouses] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [Warehouses] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [WarehouseLocations] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [WarehouseLocations] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [UserTenants] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [UserTenants] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [UserTechnicianSkills] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [UserTechnicianSkills] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [UserReportFavorites] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [UserReportFavorites] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [ThreatIndicators] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [ThreatIndicators] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [ThreatDetections] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [ThreatDetections] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [Tenants] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [Tenants] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [TenantModules] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [TenantModules] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [TechnicianTeams] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [TechnicianTeams] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [TechnicianTeams] ADD [TechnicianId] uniqueidentifier NULL;
GO

ALTER TABLE [TechnicianTeamMembers] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [TechnicianTeamMembers] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [TechnicianTeamMembers] ADD [TechnicianId1] uniqueidentifier NULL;
GO

ALTER TABLE [TechnicianSkills] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [TechnicianSkills] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [TechnicianShifts] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [TechnicianShifts] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [TechnicianSchedules] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [TechnicianSchedules] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [TechnicianSchedules] ADD [TechnicianId1] uniqueidentifier NULL;
GO

ALTER TABLE [TechnicianAvailabilities] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [TechnicianAvailabilities] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [TechnicianAvailabilities] ADD [TechnicianId1] uniqueidentifier NULL;
GO

ALTER TABLE [SystemSettings] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [SystemSettings] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [Suppliers] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [Suppliers] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [SupplierItemCatalogs] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [SupplierItemCatalogs] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [SupplierContacts] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [SupplierContacts] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [StockMovements] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [StockMovements] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [StockAdjustments] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [StockAdjustments] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [StockAdjustmentItems] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [StockAdjustmentItems] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [Skill] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [Skill] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [Shifts] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [Shifts] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [ShiftAssignments] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [ShiftAssignments] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [SecurityPolicyViolations] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [SecurityPolicyViolations] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [SecurityPolicies] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [SecurityPolicies] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [SecurityMetricsSet] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [SecurityMetricsSet] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [SecurityLogs] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [SecurityLogs] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [SecurityAlerts] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [SecurityAlerts] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [Securities] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [Securities] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [Sections] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [Sections] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [ReportTemplates] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [ReportTemplates] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [ReportSchedules] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [ReportSchedules] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [Reports] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [Reports] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [ReportRoleAssignments] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [ReportRoleAssignments] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [ReportExports] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [ReportExports] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [ReportExecutions] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [ReportExecutions] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [ReportDataSourceUsageLogs] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [ReportDataSourceUsageLogs] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [ReportDataSources] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [ReportDataSources] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [RefreshTokens] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [RefreshTokens] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [PurchaseRequisitions] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [PurchaseRequisitions] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [PurchaseRequisitionItems] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [PurchaseRequisitionItems] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [PurchaseOrders] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [PurchaseOrders] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [PurchaseOrderReceipts] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [PurchaseOrderReceipts] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [PurchaseOrderReceiptItems] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [PurchaseOrderReceiptItems] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [PurchaseOrderItems] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [PurchaseOrderItems] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [PriorityLevels] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [PriorityLevels] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [PositionSkillRequirement] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [PositionSkillRequirement] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [Permissions] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [Permissions] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [MaintenanceTypes] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [MaintenanceTypes] ADD [LastModifiedById] uniqueidentifier NULL;
GO

DROP INDEX [IX_MaintenanceSchedules_ScheduleType] ON [MaintenanceSchedules];
DECLARE @var28 sysname;
SELECT @var28 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceSchedules]') AND [c].[name] = N'ScheduleType');
IF @var28 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceSchedules] DROP CONSTRAINT [' + @var28 + '];');
ALTER TABLE [MaintenanceSchedules] ALTER COLUMN [ScheduleType] nvarchar(50) NOT NULL;
CREATE INDEX [IX_MaintenanceSchedules_ScheduleType] ON [MaintenanceSchedules] ([ScheduleType]);
GO

DROP INDEX [IX_MaintenanceSchedules_NextDueDate] ON [MaintenanceSchedules];
DECLARE @var29 sysname;
SELECT @var29 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceSchedules]') AND [c].[name] = N'NextDueDate');
IF @var29 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceSchedules] DROP CONSTRAINT [' + @var29 + '];');
UPDATE [MaintenanceSchedules] SET [NextDueDate] = '0001-01-01T00:00:00.0000000' WHERE [NextDueDate] IS NULL;
ALTER TABLE [MaintenanceSchedules] ALTER COLUMN [NextDueDate] datetime2 NOT NULL;
ALTER TABLE [MaintenanceSchedules] ADD DEFAULT '0001-01-01T00:00:00.0000000' FOR [NextDueDate];
CREATE INDEX [IX_MaintenanceSchedules_NextDueDate] ON [MaintenanceSchedules] ([NextDueDate]);
GO

DECLARE @var30 sysname;
SELECT @var30 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceSchedules]') AND [c].[name] = N'Name');
IF @var30 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceSchedules] DROP CONSTRAINT [' + @var30 + '];');
ALTER TABLE [MaintenanceSchedules] ALTER COLUMN [Name] nvarchar(100) NOT NULL;
GO

DECLARE @var31 sysname;
SELECT @var31 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceSchedules]') AND [c].[name] = N'EstimatedHours');
IF @var31 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceSchedules] DROP CONSTRAINT [' + @var31 + '];');
ALTER TABLE [MaintenanceSchedules] ALTER COLUMN [EstimatedHours] decimal(18,4) NOT NULL;
GO

DECLARE @var32 sysname;
SELECT @var32 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceSchedules]') AND [c].[name] = N'Description');
IF @var32 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceSchedules] DROP CONSTRAINT [' + @var32 + '];');
UPDATE [MaintenanceSchedules] SET [Description] = N'' WHERE [Description] IS NULL;
ALTER TABLE [MaintenanceSchedules] ALTER COLUMN [Description] nvarchar(500) NOT NULL;
ALTER TABLE [MaintenanceSchedules] ADD DEFAULT N'' FOR [Description];
GO

ALTER TABLE [MaintenanceSchedules] ADD [AssignedTeamId] uniqueidentifier NULL;
GO

ALTER TABLE [MaintenanceSchedules] ADD [AssignedTechnicianId] uniqueidentifier NULL;
GO

ALTER TABLE [MaintenanceSchedules] ADD [AutoGenerateWorkOrders] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [MaintenanceSchedules] ADD [Code] nvarchar(20) NOT NULL DEFAULT N'';
GO

ALTER TABLE [MaintenanceSchedules] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [MaintenanceSchedules] ADD [FrequencyUnit] nvarchar(20) NOT NULL DEFAULT N'';
GO

ALTER TABLE [MaintenanceSchedules] ADD [Instructions] nvarchar(2000) NOT NULL DEFAULT N'';
GO

ALTER TABLE [MaintenanceSchedules] ADD [LastCompletedDate] datetime2 NULL;
GO

ALTER TABLE [MaintenanceSchedules] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [MaintenanceSchedules] ADD [NotificationRecipients] nvarchar(500) NULL;
GO

ALTER TABLE [MaintenanceSchedules] ADD [Priority] nvarchar(20) NOT NULL DEFAULT N'';
GO

ALTER TABLE [MaintenanceSchedules] ADD [RequiredParts] nvarchar(max) NOT NULL DEFAULT N'';
GO

ALTER TABLE [MaintenanceSchedules] ADD [RequiredSkills] nvarchar(max) NOT NULL DEFAULT N'';
GO

ALTER TABLE [MaintenanceSchedules] ADD [RequiredTools] nvarchar(max) NOT NULL DEFAULT N'';
GO

ALTER TABLE [MaintenanceSchedules] ADD [SafetyNotes] nvarchar(1000) NOT NULL DEFAULT N'';
GO

ALTER TABLE [MaintenanceAttachmentTag] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [MaintenanceAttachmentTag] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [MaintenanceAttachments] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [MaintenanceAttachments] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [MaintenanceAssets] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [MaintenanceAssets] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [MaintenanceAssetCategories] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [MaintenanceAssetCategories] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [InventoryLocations] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [InventoryLocations] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [InventoryItems] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [InventoryItems] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [InventoryCategories] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [InventoryCategories] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [InventoryAllocations] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [InventoryAllocations] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [InspectionTemplates] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [InspectionTemplates] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [InspectionDocuments] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [InspectionDocuments] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [EmployeeWorkHistories] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [EmployeeWorkHistories] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [EmployeeSkills] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [EmployeeSkills] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [EmployeeShiftPreferences] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [EmployeeShiftPreferences] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [Employees] ADD [CertificationLevel] nvarchar(50) NULL;
GO

ALTER TABLE [Employees] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [Employees] ADD [CurrentWorkload] decimal(18,4) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [Employees] ADD [ExperienceLevel] nvarchar(50) NULL;
GO

ALTER TABLE [Employees] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [Employees] ADD [LastSyncDate] datetime2 NULL;
GO

ALTER TABLE [Employees] ADD [MaxWorkload] decimal(18,4) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [Employees] ADD [Notes] nvarchar(2000) NULL;
GO

ALTER TABLE [Employees] ADD [Specialization] nvarchar(100) NULL;
GO

ALTER TABLE [EmployeeQualifications] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [EmployeeQualifications] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [EmployeePositions] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [EmployeePositions] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [EmployeeIdentificationCards] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [EmployeeIdentificationCards] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [EmployeeEmergencyContacts] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [EmployeeEmergencyContacts] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [EmployeeDependents] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [EmployeeDependents] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [EmployeeContractDetails] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [EmployeeContractDetails] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [EmployeeBiometrics] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [EmployeeBiometrics] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [EmailTemplates] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [EmailTemplates] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [EmailSettings] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [EmailSettings] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [Departments] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [Departments] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [Countries] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [Countries] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [BlacklistedTokens] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [BlacklistedTokens] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [AuditLogs] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [AuditLogs] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [AttendanceRecords] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [AttendanceRecords] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [AssetTypes] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [AssetTypes] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [AssetTypeFields] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [AssetTypeFields] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [AssetInspections] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [AssetInspections] ADD [LastModifiedById] uniqueidentifier NULL;
GO

ALTER TABLE [AssetDowntimes] ADD [CreatedById] uniqueidentifier NULL;
GO

ALTER TABLE [AssetDowntimes] ADD [LastModifiedById] uniqueidentifier NULL;
GO

CREATE TABLE [SafetyAudits] (
    [Id] uniqueidentifier NOT NULL,
    [AuditName] nvarchar(100) NOT NULL,
    [AuditDate] datetime2 NOT NULL,
    [LeadAuditor] nvarchar(100) NOT NULL,
    [AuditTeam] nvarchar(max) NOT NULL,
    [AuditScope] nvarchar(1000) NOT NULL,
    [OverallScore] int NOT NULL,
    [Findings] nvarchar(2000) NOT NULL,
    [Recommendations] nvarchar(2000) NOT NULL,
    [AuditStatus] nvarchar(20) NOT NULL,
    [FollowUpDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_SafetyAudits] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [SafetyProtocols] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(20) NOT NULL,
    [Description] nvarchar(500) NOT NULL,
    [Category] nvarchar(50) NOT NULL,
    [Severity] nvarchar(20) NOT NULL,
    [RegulatoryStandard] nvarchar(100) NOT NULL,
    [Procedures] nvarchar(max) NOT NULL,
    [RequiredEquipment] nvarchar(max) NOT NULL,
    [RequiredTraining] nvarchar(max) NOT NULL,
    [RequiredCertifications] nvarchar(max) NOT NULL,
    [EmergencyProcedures] nvarchar(2000) NOT NULL,
    [PreventiveMeasures] nvarchar(2000) NOT NULL,
    [ApplicableMaintenanceTypes] nvarchar(max) NOT NULL,
    [ApplicableAssetTypes] nvarchar(max) NOT NULL,
    [IsMandatory] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [ReviewDate] datetime2 NULL,
    [NextReviewDate] datetime2 NULL,
    [ReviewFrequencyMonths] int NOT NULL,
    [MinimumTrainingLevel] nvarchar(50) NOT NULL,
    [EffectiveDate] datetime2 NULL,
    [ExpirationDate] datetime2 NULL,
    [ReviewedBy] nvarchar(100) NOT NULL,
    [IsRegulatory] bit NOT NULL,
    [ComplianceCheckpoints] nvarchar(max) NOT NULL,
    [RegulatorySources] nvarchar(max) NOT NULL,
    [ApplicableEnvironments] nvarchar(max) NOT NULL,
    [ApprovalStatus] nvarchar(20) NOT NULL,
    [ApprovedBy] nvarchar(100) NOT NULL,
    [ApprovalDate] datetime2 NULL,
    [Version] nvarchar(10) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_SafetyProtocols] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SafetyProtocols_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ScheduledWorkOrder] (
    [Id] uniqueidentifier NOT NULL,
    [ScheduleId] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [GeneratedDate] datetime2 NOT NULL,
    [ScheduledCompletionDate] datetime2 NOT NULL,
    [ActualCompletionDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_ScheduledWorkOrder] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ScheduledWorkOrder_MaintenanceSchedules_ScheduleId] FOREIGN KEY ([ScheduleId]) REFERENCES [MaintenanceSchedules] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [TechnicalSkills] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(20) NOT NULL,
    [Description] nvarchar(500) NOT NULL,
    [Category] nvarchar(50) NOT NULL,
    [SkillLevel] nvarchar(20) NOT NULL,
    [Complexity] nvarchar(20) NOT NULL,
    [RiskLevel] nvarchar(20) NOT NULL,
    [Prerequisites] nvarchar(max) NOT NULL,
    [Certifications] nvarchar(max) NOT NULL,
    [EstimatedLearningHours] int NOT NULL,
    [ToolsRequired] nvarchar(max) NOT NULL,
    [SafetyRequirements] nvarchar(1000) NOT NULL,
    [CompetencyAreas] nvarchar(max) NOT NULL,
    [RelatedMaintenanceTypes] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    [IsFromHRModule] bit NOT NULL,
    [LastSyncDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TechnicalSkills] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TechnicalSkills_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [TechnicianCertifications] (
    [Id] uniqueidentifier NOT NULL,
    [TechnicianId] uniqueidentifier NOT NULL,
    [CertificationName] nvarchar(200) NOT NULL,
    [CertificationNumber] nvarchar(100) NOT NULL,
    [IssuingOrganization] nvarchar(200) NOT NULL,
    [IssueDate] datetime2 NOT NULL,
    [ExpirationDate] datetime2 NULL,
    [Status] nvarchar(20) NOT NULL,
    [CertificationLevel] nvarchar(50) NOT NULL,
    [Category] nvarchar(100) NOT NULL,
    [Description] nvarchar(1000) NOT NULL,
    [Requirements] nvarchar(500) NOT NULL,
    [RenewalRequirements] nvarchar(500) NOT NULL,
    [Cost] decimal(18,2) NULL,
    [IsMandatory] bit NOT NULL,
    [IsVerified] bit NOT NULL,
    [VerifiedBy] nvarchar(100) NOT NULL,
    [VerificationDate] datetime2 NULL,
    [Notes] nvarchar(1000) NOT NULL,
    [DocumentPath] nvarchar(500) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TechnicianCertifications] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TechnicianCertifications_Employees_TechnicianId] FOREIGN KEY ([TechnicianId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TechnicianCertifications_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Technicians] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [EmployeeNumber] nvarchar(50) NOT NULL,
    [FirstName] nvarchar(100) NOT NULL,
    [LastName] nvarchar(100) NOT NULL,
    [Email] nvarchar(200) NULL,
    [Phone] nvarchar(20) NULL,
    [Department] nvarchar(100) NOT NULL,
    [Position] nvarchar(100) NOT NULL,
    [Specialization] nvarchar(100) NOT NULL,
    [CertificationLevel] nvarchar(50) NOT NULL,
    [ExperienceLevel] nvarchar(50) NOT NULL,
    [HireDate] datetime2 NOT NULL,
    [IsActive] bit NOT NULL,
    [CurrentWorkload] decimal(18,4) NOT NULL,
    [MaxWorkload] decimal(18,4) NOT NULL,
    [AverageRating] decimal(18,4) NOT NULL,
    [CompletedWorkOrders] int NOT NULL,
    [LastSyncDate] datetime2 NULL,
    [Notes] nvarchar(2000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Technicians] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Technicians_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Technicians_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [WorkflowEntityTypes] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [EntityClassName] nvarchar(200) NULL,
    [PropertySchema] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [DisplayOrder] int NOT NULL,
    [Icon] nvarchar(50) NULL,
    [ColorCode] nvarchar(7) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkflowEntityTypes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowEntityTypes_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ProtocolAdherences] (
    [Id] uniqueidentifier NOT NULL,
    [ProtocolId] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [TechnicianId] uniqueidentifier NOT NULL,
    [AdherenceDate] datetime2 NOT NULL,
    [WasFollowed] bit NOT NULL,
    [ComplianceScore] int NOT NULL,
    [AdherenceLevel] nvarchar(20) NOT NULL,
    [Notes] nvarchar(1000) NOT NULL,
    [VerifiedBy] nvarchar(100) NOT NULL,
    [VerificationDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ProtocolAdherences] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProtocolAdherences_SafetyProtocols_ProtocolId] FOREIGN KEY ([ProtocolId]) REFERENCES [SafetyProtocols] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProtocolAdherences_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ProtocolAuditDetails] (
    [Id] uniqueidentifier NOT NULL,
    [AuditId] uniqueidentifier NOT NULL,
    [ProtocolId] uniqueidentifier NOT NULL,
    [ComplianceScore] int NOT NULL,
    [Passed] bit NOT NULL,
    [ProtocolFindings] nvarchar(1000) NOT NULL,
    [ProtocolRecommendations] nvarchar(1000) NOT NULL,
    [Priority] nvarchar(20) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_ProtocolAuditDetails] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProtocolAuditDetails_SafetyAudits_AuditId] FOREIGN KEY ([AuditId]) REFERENCES [SafetyAudits] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProtocolAuditDetails_SafetyProtocols_ProtocolId] FOREIGN KEY ([ProtocolId]) REFERENCES [SafetyProtocols] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ProtocolTrainings] (
    [Id] uniqueidentifier NOT NULL,
    [ProtocolId] uniqueidentifier NOT NULL,
    [TechnicianId] uniqueidentifier NOT NULL,
    [TrainingDate] datetime2 NOT NULL,
    [TrainingMethod] nvarchar(50) NOT NULL,
    [TrainerName] nvarchar(100) NOT NULL,
    [TrainingHours] decimal(18,4) NOT NULL,
    [TestScore] int NULL,
    [Completed] bit NOT NULL,
    [CompletionStatus] nvarchar(20) NOT NULL,
    [CertificationIssued] bit NOT NULL,
    [ExpirationDate] datetime2 NULL,
    [Notes] nvarchar(500) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ProtocolTrainings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProtocolTrainings_SafetyProtocols_ProtocolId] FOREIGN KEY ([ProtocolId]) REFERENCES [SafetyProtocols] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProtocolTrainings_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ProtocolViolations] (
    [Id] uniqueidentifier NOT NULL,
    [ProtocolId] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NULL,
    [TechnicianId] uniqueidentifier NOT NULL,
    [ViolationDate] datetime2 NOT NULL,
    [ViolationType] nvarchar(50) NOT NULL,
    [Severity] nvarchar(20) NOT NULL,
    [ViolationSeverity] nvarchar(20) NOT NULL,
    [Description] nvarchar(1000) NOT NULL,
    [RootCause] nvarchar(500) NOT NULL,
    [ImmediateActions] nvarchar(1000) NOT NULL,
    [PreventiveActions] nvarchar(1000) NOT NULL,
    [InjuryOccurred] bit NOT NULL,
    [PropertyDamageOccurred] bit NOT NULL,
    [EstimatedCost] decimal(18,2) NOT NULL,
    [InvestigationStatus] nvarchar(20) NOT NULL,
    [InvestigatorAssigned] nvarchar(100) NOT NULL,
    [TargetCompletionDate] datetime2 NULL,
    [ActualCompletionDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ProtocolViolations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProtocolViolations_SafetyProtocols_ProtocolId] FOREIGN KEY ([ProtocolId]) REFERENCES [SafetyProtocols] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProtocolViolations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [SafetyComplianceRecords] (
    [Id] uniqueidentifier NOT NULL,
    [SafetyProtocolId] uniqueidentifier NOT NULL,
    [ProtocolId] uniqueidentifier NOT NULL,
    [TechnicianId] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NULL,
    [ComplianceDate] datetime2 NOT NULL,
    [CheckDate] datetime2 NOT NULL,
    [ComplianceStatus] nvarchar(20) NOT NULL,
    [Notes] nvarchar(2000) NULL,
    [IsCompliant] bit NOT NULL,
    [ViolationType] nvarchar(50) NULL,
    [CorrectiveActions] nvarchar(2000) NULL,
    [CorrectiveActionDueDate] datetime2 NULL,
    [ChecklistItems] nvarchar(max) NULL,
    [Violations] nvarchar(max) NULL,
    [InspectorId] uniqueidentifier NULL,
    [InspectorNotes] nvarchar(2000) NULL,
    [VerifiedById] uniqueidentifier NULL,
    [VerifiedDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_SafetyComplianceRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SafetyComplianceRecords_Employees_InspectorId] FOREIGN KEY ([InspectorId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_SafetyComplianceRecords_Employees_TechnicianId] FOREIGN KEY ([TechnicianId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SafetyComplianceRecords_Employees_VerifiedById] FOREIGN KEY ([VerifiedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_SafetyComplianceRecords_SafetyProtocols_SafetyProtocolId] FOREIGN KEY ([SafetyProtocolId]) REFERENCES [SafetyProtocols] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SafetyComplianceRecords_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SafetyComplianceRecords_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id])
);
GO

CREATE TABLE [TechnicianSkillAssignments] (
    [Id] uniqueidentifier NOT NULL,
    [TechnicianId] uniqueidentifier NOT NULL,
    [SkillId] uniqueidentifier NOT NULL,
    [ProficiencyLevel] int NOT NULL,
    [ProficiencyDescription] nvarchar(100) NOT NULL,
    [AcquiredDate] datetime2 NOT NULL,
    [ExpirationDate] datetime2 NULL,
    [IsVerified] bit NOT NULL,
    [VerifiedBy] nvarchar(100) NOT NULL,
    [LastAssessmentDate] datetime2 NULL,
    [Notes] nvarchar(500) NOT NULL,
    [TechnicianId1] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TechnicianSkillAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TechnicianSkillAssignments_Employees_TechnicianId] FOREIGN KEY ([TechnicianId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TechnicianSkillAssignments_TechnicalSkills_SkillId] FOREIGN KEY ([SkillId]) REFERENCES [TechnicalSkills] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TechnicianSkillAssignments_Technicians_TechnicianId1] FOREIGN KEY ([TechnicianId1]) REFERENCES [Technicians] ([Id]),
    CONSTRAINT [FK_TechnicianSkillAssignments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-14T17:21:11.2384370Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-14T17:21:11.2384423Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-14T17:21:11.2384425Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-14T17:21:11.2384427Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-14T17:21:11.2384649Z', [CreatedById] = NULL, [LastModifiedById] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-14T17:21:11.2384666Z', [CreatedById] = NULL, [LastModifiedById] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-14T17:21:11.2384671Z', [CreatedById] = NULL, [LastModifiedById] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-14T17:21:11.2384676Z', [CreatedById] = NULL, [LastModifiedById] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-14T17:21:11.2384684Z', [CreatedById] = NULL, [LastModifiedById] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-14T17:21:11.2384692Z', [CreatedById] = NULL, [LastModifiedById] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-14T17:21:11.2384697Z', [CreatedById] = NULL, [LastModifiedById] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-14T17:21:11.2384701Z', [CreatedById] = NULL, [LastModifiedById] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-14T17:21:11.2384709Z', [CreatedById] = NULL, [LastModifiedById] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-14T17:21:11.2384715Z', [CreatedById] = NULL, [LastModifiedById] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-14T17:21:11.2384721Z', [CreatedById] = NULL, [LastModifiedById] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-14T17:21:11.2384725Z', [CreatedById] = NULL, [LastModifiedById] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-14T17:21:11.2384732Z', [CreatedById] = NULL, [LastModifiedById] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-14T17:21:11.2384745Z', [CreatedById] = NULL, [LastModifiedById] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-14T17:21:11.2384755Z', [CreatedById] = NULL, [LastModifiedById] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-14T17:21:11.2384759Z', [CreatedById] = NULL, [LastModifiedById] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384802Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384807Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384808Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384809Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384809Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384811Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384811Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384812Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384813Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384814Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384815Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384815Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384816Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384817Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384817Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384818Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384858Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384859Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384860Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384861Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384861Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384862Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384862Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384863Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384864Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384864Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384865Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384865Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384866Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384866Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384867Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384898Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384899Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384900Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384901Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384901Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384902Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384902Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384903Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384904Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384904Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384918Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384919Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-14T17:21:11.2384920Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'CreatedById', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'LastModifiedById', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [CreatedById], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [LastModifiedById], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('1fd11209-dc35-4750-8e23-3198f1ed133f', NULL, '2025-10-14T17:21:11.2384537Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-14T17:21:11.2384536Z', CAST(0 AS bit), NULL, N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('588b6773-6a6a-4aa8-ba0c-1e8ead7f4ec4', NULL, '2025-10-14T17:21:11.2384506Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-14T17:21:11.2384506Z', CAST(0 AS bit), NULL, N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('6b13348a-36d4-4d6b-99d3-14d8a377bc4f', NULL, '2025-10-14T17:21:11.2384561Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-14T17:21:11.2384561Z', CAST(0 AS bit), NULL, N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('752d4b26-a221-4c6d-af27-fdaeca22eaad', NULL, '2025-10-14T17:21:11.2384550Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-14T17:21:11.2384549Z', CAST(0 AS bit), NULL, N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('d68e0df1-7fbf-424e-995e-b09d433ad1e3', NULL, '2025-10-14T17:21:11.2384526Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-14T17:21:11.2384525Z', CAST(0 AS bit), NULL, N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('ef24feea-c93b-462e-b089-42362142da3a', NULL, '2025-10-14T17:21:11.2384572Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-14T17:21:11.2384572Z', CAST(0 AS bit), NULL, N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('fd7656a2-1f6a-442f-a6bf-f15c8542c0dd', NULL, '2025-10-14T17:21:11.2384490Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-14T17:21:11.2384488Z', CAST(0 AS bit), NULL, N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'CreatedById', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'LastModifiedById', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-10-14T17:21:11.2384205Z', [CreatedById] = NULL, [LastModifiedById] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_WorkOrders_TechnicianId] ON [WorkOrders] ([TechnicianId]);
GO

CREATE INDEX [IX_WorkflowTransitions_FromStepId] ON [WorkflowTransitions] ([FromStepId]);
GO

CREATE INDEX [IX_WorkflowTransitions_TenantId] ON [WorkflowTransitions] ([TenantId]);
GO

CREATE INDEX [IX_WorkflowTransitions_WorkflowDefinitionId] ON [WorkflowTransitions] ([WorkflowDefinitionId]);
GO

CREATE INDEX [IX_WorkflowSteps_IsEndStep] ON [WorkflowSteps] ([IsEndStep]);
GO

CREATE INDEX [IX_WorkflowSteps_IsStartStep] ON [WorkflowSteps] ([IsStartStep]);
GO

CREATE INDEX [IX_WorkflowSteps_Name] ON [WorkflowSteps] ([Name]);
GO

CREATE INDEX [IX_WorkflowSteps_Order] ON [WorkflowSteps] ([Order]);
GO

CREATE INDEX [IX_WorkflowSteps_StepType] ON [WorkflowSteps] ([StepType]);
GO

CREATE INDEX [IX_WorkflowSteps_TenantId] ON [WorkflowSteps] ([TenantId]);
GO

CREATE INDEX [IX_WorkflowStepInstances_AssignedToId] ON [WorkflowStepInstances] ([AssignedToId]);
GO

CREATE INDEX [IX_WorkflowStepInstances_DueDate] ON [WorkflowStepInstances] ([DueDate]);
GO

CREATE INDEX [IX_WorkflowStepInstances_StartedDate] ON [WorkflowStepInstances] ([StartedDate]);
GO

CREATE INDEX [IX_WorkflowStepInstances_TenantId] ON [WorkflowStepInstances] ([TenantId]);
GO

CREATE INDEX [IX_WorkflowInstances_EntityId] ON [WorkflowInstances] ([EntityId]);
GO

CREATE INDEX [IX_WorkflowInstances_EntityTypeId] ON [WorkflowInstances] ([EntityTypeId]);
GO

CREATE INDEX [IX_WorkflowInstances_EntityTypeId_EntityId] ON [WorkflowInstances] ([EntityTypeId], [EntityId]);
GO

CREATE INDEX [IX_WorkflowInstances_InitiatedById] ON [WorkflowInstances] ([InitiatedById]);
GO

CREATE INDEX [IX_WorkflowInstances_Priority] ON [WorkflowInstances] ([Priority]);
GO

CREATE INDEX [IX_WorkflowInstances_StartedById] ON [WorkflowInstances] ([StartedById]);
GO

CREATE INDEX [IX_WorkflowInstances_StartedDate] ON [WorkflowInstances] ([StartedDate]);
GO

CREATE INDEX [IX_WorkflowInstances_Status] ON [WorkflowInstances] ([Status]);
GO

CREATE INDEX [IX_WorkflowInstances_TenantId] ON [WorkflowInstances] ([TenantId]);
GO

CREATE INDEX [IX_WorkflowDefinitions_EntityTypeId] ON [WorkflowDefinitions] ([EntityTypeId]);
GO

CREATE INDEX [IX_WorkflowDefinitions_IsActive] ON [WorkflowDefinitions] ([IsActive]);
GO

CREATE INDEX [IX_WorkflowDefinitions_Name] ON [WorkflowDefinitions] ([Name]);
GO

CREATE UNIQUE INDEX [IX_WorkflowDefinitions_TenantId_Name] ON [WorkflowDefinitions] ([TenantId], [Name]);
GO

CREATE INDEX [IX_WorkflowApprovals_ApproverId] ON [WorkflowApprovals] ([ApproverId]);
GO

CREATE INDEX [IX_WorkflowApprovals_DueDate] ON [WorkflowApprovals] ([DueDate]);
GO

CREATE INDEX [IX_WorkflowApprovals_ProcessedById] ON [WorkflowApprovals] ([ProcessedById]);
GO

CREATE INDEX [IX_WorkflowApprovals_RequestedDate] ON [WorkflowApprovals] ([RequestedDate]);
GO

CREATE INDEX [IX_WorkflowApprovals_Status] ON [WorkflowApprovals] ([Status]);
GO

CREATE INDEX [IX_WorkflowApprovals_TenantId] ON [WorkflowApprovals] ([TenantId]);
GO

CREATE INDEX [IX_WorkflowActivityLogs_ActivityDate] ON [WorkflowActivityLogs] ([ActivityDate]);
GO

CREATE INDEX [IX_WorkflowActivityLogs_ActivityType] ON [WorkflowActivityLogs] ([ActivityType]);
GO

CREATE INDEX [IX_WorkflowActivityLogs_PerformedById] ON [WorkflowActivityLogs] ([PerformedById]);
GO

CREATE INDEX [IX_WorkflowActivityLogs_TenantId] ON [WorkflowActivityLogs] ([TenantId]);
GO

CREATE INDEX [IX_TechnicianTeams_TechnicianId] ON [TechnicianTeams] ([TechnicianId]);
GO

CREATE INDEX [IX_TechnicianTeamMembers_TechnicianId1] ON [TechnicianTeamMembers] ([TechnicianId1]);
GO

CREATE INDEX [IX_TechnicianSchedules_TechnicianId1] ON [TechnicianSchedules] ([TechnicianId1]);
GO

CREATE INDEX [IX_TechnicianAvailabilities_TechnicianId1] ON [TechnicianAvailabilities] ([TechnicianId1]);
GO

CREATE INDEX [IX_ProtocolAdherences_ProtocolId] ON [ProtocolAdherences] ([ProtocolId]);
GO

CREATE INDEX [IX_ProtocolAdherences_TenantId] ON [ProtocolAdherences] ([TenantId]);
GO

CREATE INDEX [IX_ProtocolAuditDetails_AuditId] ON [ProtocolAuditDetails] ([AuditId]);
GO

CREATE INDEX [IX_ProtocolAuditDetails_ProtocolId] ON [ProtocolAuditDetails] ([ProtocolId]);
GO

CREATE INDEX [IX_ProtocolTrainings_ProtocolId] ON [ProtocolTrainings] ([ProtocolId]);
GO

CREATE INDEX [IX_ProtocolTrainings_TenantId] ON [ProtocolTrainings] ([TenantId]);
GO

CREATE INDEX [IX_ProtocolViolations_ProtocolId] ON [ProtocolViolations] ([ProtocolId]);
GO

CREATE INDEX [IX_ProtocolViolations_TenantId] ON [ProtocolViolations] ([TenantId]);
GO

CREATE INDEX [IX_SafetyComplianceRecords_ComplianceDate] ON [SafetyComplianceRecords] ([ComplianceDate]);
GO

CREATE INDEX [IX_SafetyComplianceRecords_ComplianceStatus] ON [SafetyComplianceRecords] ([ComplianceStatus]);
GO

CREATE INDEX [IX_SafetyComplianceRecords_InspectorId] ON [SafetyComplianceRecords] ([InspectorId]);
GO

CREATE INDEX [IX_SafetyComplianceRecords_SafetyProtocolId] ON [SafetyComplianceRecords] ([SafetyProtocolId]);
GO

CREATE INDEX [IX_SafetyComplianceRecords_TechnicianId] ON [SafetyComplianceRecords] ([TechnicianId]);
GO

CREATE INDEX [IX_SafetyComplianceRecords_TenantId] ON [SafetyComplianceRecords] ([TenantId]);
GO

CREATE INDEX [IX_SafetyComplianceRecords_VerifiedById] ON [SafetyComplianceRecords] ([VerifiedById]);
GO

CREATE INDEX [IX_SafetyComplianceRecords_WorkOrderId] ON [SafetyComplianceRecords] ([WorkOrderId]);
GO

CREATE INDEX [IX_SafetyProtocols_Category] ON [SafetyProtocols] ([Category]);
GO

CREATE UNIQUE INDEX [IX_SafetyProtocols_Code] ON [SafetyProtocols] ([Code]);
GO

CREATE INDEX [IX_SafetyProtocols_EffectiveDate] ON [SafetyProtocols] ([EffectiveDate]);
GO

CREATE INDEX [IX_SafetyProtocols_IsActive] ON [SafetyProtocols] ([IsActive]);
GO

CREATE INDEX [IX_SafetyProtocols_IsMandatory] ON [SafetyProtocols] ([IsMandatory]);
GO

CREATE INDEX [IX_SafetyProtocols_Name] ON [SafetyProtocols] ([Name]);
GO

CREATE INDEX [IX_SafetyProtocols_NextReviewDate] ON [SafetyProtocols] ([NextReviewDate]);
GO

CREATE INDEX [IX_SafetyProtocols_Severity] ON [SafetyProtocols] ([Severity]);
GO

CREATE INDEX [IX_SafetyProtocols_TenantId] ON [SafetyProtocols] ([TenantId]);
GO

CREATE INDEX [IX_ScheduledWorkOrder_ScheduleId] ON [ScheduledWorkOrder] ([ScheduleId]);
GO

CREATE INDEX [IX_TechnicalSkills_Category] ON [TechnicalSkills] ([Category]);
GO

CREATE UNIQUE INDEX [IX_TechnicalSkills_Code] ON [TechnicalSkills] ([Code]);
GO

CREATE INDEX [IX_TechnicalSkills_Complexity] ON [TechnicalSkills] ([Complexity]);
GO

CREATE INDEX [IX_TechnicalSkills_IsActive] ON [TechnicalSkills] ([IsActive]);
GO

CREATE INDEX [IX_TechnicalSkills_IsFromHRModule] ON [TechnicalSkills] ([IsFromHRModule]);
GO

CREATE INDEX [IX_TechnicalSkills_LastSyncDate] ON [TechnicalSkills] ([LastSyncDate]);
GO

CREATE INDEX [IX_TechnicalSkills_Name] ON [TechnicalSkills] ([Name]);
GO

CREATE INDEX [IX_TechnicalSkills_RiskLevel] ON [TechnicalSkills] ([RiskLevel]);
GO

CREATE INDEX [IX_TechnicalSkills_SkillLevel] ON [TechnicalSkills] ([SkillLevel]);
GO

CREATE INDEX [IX_TechnicalSkills_TenantId] ON [TechnicalSkills] ([TenantId]);
GO

CREATE INDEX [IX_TechnicianCertifications_TechnicianId] ON [TechnicianCertifications] ([TechnicianId]);
GO

CREATE INDEX [IX_TechnicianCertifications_TenantId] ON [TechnicianCertifications] ([TenantId]);
GO

CREATE INDEX [IX_Technicians_EmployeeId] ON [Technicians] ([EmployeeId]);
GO

CREATE INDEX [IX_Technicians_TenantId] ON [Technicians] ([TenantId]);
GO

CREATE INDEX [IX_TechnicianSkillAssignments_ExpirationDate] ON [TechnicianSkillAssignments] ([ExpirationDate]);
GO

CREATE INDEX [IX_TechnicianSkillAssignments_IsVerified] ON [TechnicianSkillAssignments] ([IsVerified]);
GO

CREATE INDEX [IX_TechnicianSkillAssignments_LastAssessmentDate] ON [TechnicianSkillAssignments] ([LastAssessmentDate]);
GO

CREATE INDEX [IX_TechnicianSkillAssignments_ProficiencyLevel] ON [TechnicianSkillAssignments] ([ProficiencyLevel]);
GO

CREATE INDEX [IX_TechnicianSkillAssignments_SkillId] ON [TechnicianSkillAssignments] ([SkillId]);
GO

CREATE INDEX [IX_TechnicianSkillAssignments_TechnicianId] ON [TechnicianSkillAssignments] ([TechnicianId]);
GO

CREATE UNIQUE INDEX [IX_TechnicianSkillAssignments_TechnicianId_SkillId] ON [TechnicianSkillAssignments] ([TechnicianId], [SkillId]);
GO

CREATE INDEX [IX_TechnicianSkillAssignments_TechnicianId1] ON [TechnicianSkillAssignments] ([TechnicianId1]);
GO

CREATE INDEX [IX_TechnicianSkillAssignments_TenantId] ON [TechnicianSkillAssignments] ([TenantId]);
GO

CREATE INDEX [IX_WorkflowEntityTypes_IsActive] ON [WorkflowEntityTypes] ([IsActive]);
GO

CREATE INDEX [IX_WorkflowEntityTypes_Name] ON [WorkflowEntityTypes] ([Name]);
GO

CREATE UNIQUE INDEX [IX_WorkflowEntityTypes_TenantId_Name] ON [WorkflowEntityTypes] ([TenantId], [Name]);
GO

ALTER TABLE [TechnicianAvailabilities] ADD CONSTRAINT [FK_TechnicianAvailabilities_Technicians_TechnicianId1] FOREIGN KEY ([TechnicianId1]) REFERENCES [Technicians] ([Id]);
GO

ALTER TABLE [TechnicianSchedules] ADD CONSTRAINT [FK_TechnicianSchedules_Technicians_TechnicianId1] FOREIGN KEY ([TechnicianId1]) REFERENCES [Technicians] ([Id]);
GO

ALTER TABLE [TechnicianTeamMembers] ADD CONSTRAINT [FK_TechnicianTeamMembers_Technicians_TechnicianId1] FOREIGN KEY ([TechnicianId1]) REFERENCES [Technicians] ([Id]);
GO

ALTER TABLE [TechnicianTeams] ADD CONSTRAINT [FK_TechnicianTeams_Technicians_TechnicianId] FOREIGN KEY ([TechnicianId]) REFERENCES [Technicians] ([Id]);
GO

ALTER TABLE [WorkflowActivityLogs] ADD CONSTRAINT [FK_WorkflowActivityLogs_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [WorkflowActivityLogs] ADD CONSTRAINT [FK_WorkflowActivityLogs_Users_PerformedById] FOREIGN KEY ([PerformedById]) REFERENCES [Users] ([Id]);
GO

ALTER TABLE [WorkflowActivityLogs] ADD CONSTRAINT [FK_WorkflowActivityLogs_WorkflowStepInstances_StepInstanceId] FOREIGN KEY ([StepInstanceId]) REFERENCES [WorkflowStepInstances] ([Id]);
GO

ALTER TABLE [WorkflowApprovals] ADD CONSTRAINT [FK_WorkflowApprovals_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [WorkflowApprovals] ADD CONSTRAINT [FK_WorkflowApprovals_Users_ApproverId] FOREIGN KEY ([ApproverId]) REFERENCES [Users] ([Id]);
GO

ALTER TABLE [WorkflowApprovals] ADD CONSTRAINT [FK_WorkflowApprovals_Users_ProcessedById] FOREIGN KEY ([ProcessedById]) REFERENCES [Users] ([Id]);
GO

ALTER TABLE [WorkflowApprovals] ADD CONSTRAINT [FK_WorkflowApprovals_WorkflowStepInstances_StepInstanceId] FOREIGN KEY ([StepInstanceId]) REFERENCES [WorkflowStepInstances] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [WorkflowDefinitions] ADD CONSTRAINT [FK_WorkflowDefinitions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [WorkflowDefinitions] ADD CONSTRAINT [FK_WorkflowDefinitions_WorkflowEntityTypes_EntityTypeId] FOREIGN KEY ([EntityTypeId]) REFERENCES [WorkflowEntityTypes] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [WorkflowInstances] ADD CONSTRAINT [FK_WorkflowInstances_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [WorkflowInstances] ADD CONSTRAINT [FK_WorkflowInstances_Users_InitiatedById] FOREIGN KEY ([InitiatedById]) REFERENCES [Users] ([Id]);
GO

ALTER TABLE [WorkflowInstances] ADD CONSTRAINT [FK_WorkflowInstances_WorkflowEntityTypes_EntityTypeId] FOREIGN KEY ([EntityTypeId]) REFERENCES [WorkflowEntityTypes] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [WorkflowStepInstances] ADD CONSTRAINT [FK_WorkflowStepInstances_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [WorkflowStepInstances] ADD CONSTRAINT [FK_WorkflowStepInstances_Users_AssignedToId] FOREIGN KEY ([AssignedToId]) REFERENCES [Users] ([Id]);
GO

ALTER TABLE [WorkflowSteps] ADD CONSTRAINT [FK_WorkflowSteps_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [WorkflowTransitions] ADD CONSTRAINT [FK_WorkflowTransitions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [WorkflowTransitions] ADD CONSTRAINT [FK_WorkflowTransitions_WorkflowDefinitions_WorkflowDefinitionId] FOREIGN KEY ([WorkflowDefinitionId]) REFERENCES [WorkflowDefinitions] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [WorkOrders] ADD CONSTRAINT [FK_WorkOrders_Technicians_TechnicianId] FOREIGN KEY ([TechnicianId]) REFERENCES [Technicians] ([Id]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251014172112_AddTechnicianCertificationAndSafetyEntities', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [WorkOrders] DROP CONSTRAINT [FK_WorkOrders_Employees_EmployeeId];
GO

DELETE FROM [TenantModules]
WHERE [Id] = '1fd11209-dc35-4750-8e23-3198f1ed133f';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '588b6773-6a6a-4aa8-ba0c-1e8ead7f4ec4';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '6b13348a-36d4-4d6b-99d3-14d8a377bc4f';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '752d4b26-a221-4c6d-af27-fdaeca22eaad';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'd68e0df1-7fbf-424e-995e-b09d433ad1e3';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'ef24feea-c93b-462e-b089-42362142da3a';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'fd7656a2-1f6a-442f-a6bf-f15c8542c0dd';
SELECT @@ROWCOUNT;

GO

EXEC sp_rename N'[WorkOrders].[EmployeeId]', N'SupervisorId', N'COLUMN';
GO

EXEC sp_rename N'[WorkOrders].[IX_WorkOrders_EmployeeId]', N'IX_WorkOrders_SupervisorId', N'INDEX';
GO

ALTER TABLE [WorkOrders] ADD [CompletedById] uniqueidentifier NULL;
GO

ALTER TABLE [WorkOrders] ADD [ContractorId] uniqueidentifier NULL;
GO

ALTER TABLE [WorkOrders] ADD [QualityCheckedById] uniqueidentifier NULL;
GO

DROP INDEX [IX_MaintenanceTypes_Category] ON [MaintenanceTypes];
DECLARE @var33 sysname;
SELECT @var33 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceTypes]') AND [c].[name] = N'Category');
IF @var33 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceTypes] DROP CONSTRAINT [' + @var33 + '];');
ALTER TABLE [MaintenanceTypes] ALTER COLUMN [Category] nvarchar(30) NOT NULL;
CREATE INDEX [IX_MaintenanceTypes_Category] ON [MaintenanceTypes] ([Category]);
GO

ALTER TABLE [MaintenanceTypes] ADD [ApplicableAssetTypes] nvarchar(max) NULL;
GO

ALTER TABLE [MaintenanceTypes] ADD [ApprovalLevels] int NOT NULL DEFAULT 0;
GO

ALTER TABLE [MaintenanceTypes] ADD [AverageCompletionHours] float NOT NULL DEFAULT 0.0E0;
GO

ALTER TABLE [MaintenanceTypes] ADD [AverageCost] decimal(18,2) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [MaintenanceTypes] ADD [ChecklistTemplate] nvarchar(max) NULL;
GO

ALTER TABLE [MaintenanceTypes] ADD [ConditionCriteria] nvarchar(max) NULL;
GO

ALTER TABLE [MaintenanceTypes] ADD [Criticality] nvarchar(20) NOT NULL DEFAULT N'';
GO

ALTER TABLE [MaintenanceTypes] ADD [CycleTrigger] decimal(18,4) NULL;
GO

ALTER TABLE [MaintenanceTypes] ADD [DefaultPriority] int NOT NULL DEFAULT 0;
GO

ALTER TABLE [MaintenanceTypes] ADD [DowntimeMinutes] int NOT NULL DEFAULT 0;
GO

ALTER TABLE [MaintenanceTypes] ADD [EstimatedCost] decimal(18,2) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [MaintenanceTypes] ADD [EstimatedHours] float NOT NULL DEFAULT 0.0E0;
GO

ALTER TABLE [MaintenanceTypes] ADD [FrequencyDays] int NULL;
GO

ALTER TABLE [MaintenanceTypes] ADD [FrequencyMonths] int NULL;
GO

ALTER TABLE [MaintenanceTypes] ADD [FrequencyWeeks] int NULL;
GO

ALTER TABLE [MaintenanceTypes] ADD [HoursTrigger] decimal(18,4) NULL;
GO

ALTER TABLE [MaintenanceTypes] ADD [Icon] nvarchar(50) NULL;
GO

ALTER TABLE [MaintenanceTypes] ADD [IsConditionBased] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [MaintenanceTypes] ADD [IsTimeBased] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [MaintenanceTypes] ADD [IsUsageBased] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [MaintenanceTypes] ADD [LastPerformanceUpdate] datetime2 NULL;
GO

ALTER TABLE [MaintenanceTypes] ADD [LeadTimeDays] int NOT NULL DEFAULT 0;
GO

ALTER TABLE [MaintenanceTypes] ADD [Location] nvarchar(20) NOT NULL DEFAULT N'';
GO

ALTER TABLE [MaintenanceTypes] ADD [MaintenanceClass] nvarchar(30) NOT NULL DEFAULT N'';
GO

ALTER TABLE [MaintenanceTypes] ADD [MileageTrigger] decimal(18,4) NULL;
GO

ALTER TABLE [MaintenanceTypes] ADD [PartsTemplate] nvarchar(max) NULL;
GO

ALTER TABLE [MaintenanceTypes] ADD [RequiredSkills] nvarchar(max) NULL;
GO

ALTER TABLE [MaintenanceTypes] ADD [RequiredTools] nvarchar(max) NULL;
GO

ALTER TABLE [MaintenanceTypes] ADD [RequiresApproval] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [MaintenanceTypes] ADD [RequiresCertification] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [MaintenanceTypes] ADD [RequiresDocumentation] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [MaintenanceTypes] ADD [RequiresQualityCheck] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [MaintenanceTypes] ADD [RequiresSafetyPermit] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [MaintenanceTypes] ADD [RequiresShutdown] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [MaintenanceTypes] ADD [RequiresSpecialTraining] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [MaintenanceTypes] ADD [SafetyRequirements] nvarchar(1000) NULL;
GO

ALTER TABLE [MaintenanceTypes] ADD [SchedulingRules] nvarchar(max) NULL;
GO

ALTER TABLE [MaintenanceTypes] ADD [SortOrder] int NOT NULL DEFAULT 0;
GO

ALTER TABLE [MaintenanceTypes] ADD [TaskTemplate] nvarchar(max) NULL;
GO

ALTER TABLE [MaintenanceSchedules] ADD [ConditionCriteria] nvarchar(max) NULL;
GO

ALTER TABLE [MaintenanceSchedules] ADD [ConditionDataSources] nvarchar(max) NULL;
GO

ALTER TABLE [MaintenanceSchedules] ADD [CycleTrigger] decimal(18,4) NULL;
GO

ALTER TABLE [MaintenanceSchedules] ADD [LastConditionCheckDate] datetime2 NULL;
GO

ALTER TABLE [MaintenanceSchedules] ADD [LastConditionCheckResult] bit NULL;
GO

ALTER TABLE [MaintenanceSchedules] ADD [LastUsageValue] decimal(18,4) NULL;
GO

ALTER TABLE [MaintenanceSchedules] ADD [MaintenanceTypeId1] uniqueidentifier NULL;
GO

ALTER TABLE [MaintenanceSchedules] ADD [MileageTrigger] decimal(18,4) NULL;
GO

ALTER TABLE [MaintenanceSchedules] ADD [OperatingHoursTrigger] decimal(18,4) NULL;
GO

ALTER TABLE [MaintenanceSchedules] ADD [PrimaryTriggerType] nvarchar(20) NOT NULL DEFAULT N'';
GO

ALTER TABLE [MaintenanceSchedules] ADD [SecondaryTriggerType] nvarchar(20) NULL;
GO

ALTER TABLE [MaintenanceSchedules] ADD [StartDate] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
GO

ALTER TABLE [MaintenanceSchedules] ADD [TriggerLogic] nvarchar(10) NOT NULL DEFAULT N'';
GO

ALTER TABLE [MaintenanceSchedules] ADD [UsageUnit] nvarchar(20) NULL;
GO

CREATE TABLE [MaintenanceContractor] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [ContractorCode] nvarchar(50) NULL,
    [Description] nvarchar(1000) NULL,
    [ContactInfo] nvarchar(max) NOT NULL,
    [Capabilities] nvarchar(max) NOT NULL,
    [ServiceAreas] nvarchar(max) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [Rating] float NULL,
    [LicenseInfo] nvarchar(max) NULL,
    [InsuranceInfo] nvarchar(max) NULL,
    [CreatedDate] datetime2 NOT NULL,
    [LastModifiedDate] datetime2 NULL,
    [CreatedById] uniqueidentifier NOT NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MaintenanceContractor] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [ContractorInvoice] (
    [Id] uniqueidentifier NOT NULL,
    [ContractorId] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NULL,
    [InvoiceNumber] nvarchar(50) NOT NULL,
    [InvoiceDate] datetime2 NOT NULL,
    [DueDate] datetime2 NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [TaxAmount] decimal(18,2) NULL,
    [Status] nvarchar(20) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [LineItems] nvarchar(max) NOT NULL,
    [AttachmentPaths] nvarchar(max) NULL,
    [CreatedDate] datetime2 NOT NULL,
    [ApprovedDate] datetime2 NULL,
    [PaidDate] datetime2 NULL,
    [ApprovedById] uniqueidentifier NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [MaintenanceContractorId] uniqueidentifier NULL,
    CONSTRAINT [PK_ContractorInvoice] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ContractorInvoice_MaintenanceContractor_MaintenanceContractorId] FOREIGN KEY ([MaintenanceContractorId]) REFERENCES [MaintenanceContractor] ([Id])
);
GO

CREATE TABLE [ContractorPerformanceReview] (
    [Id] uniqueidentifier NOT NULL,
    [ContractorId] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NULL,
    [ReviewDate] datetime2 NOT NULL,
    [ReviewedById] uniqueidentifier NOT NULL,
    [OverallRating] int NOT NULL,
    [QualityRating] int NULL,
    [TimelinessRating] int NULL,
    [CommunicationRating] int NULL,
    [CostRating] int NULL,
    [Comments] nvarchar(2000) NULL,
    [Recommendations] nvarchar(1000) NULL,
    [WouldRecommend] bit NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ContractorPerformanceReview] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ContractorPerformanceReview_MaintenanceContractor_ContractorId] FOREIGN KEY ([ContractorId]) REFERENCES [MaintenanceContractor] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ContractorWorkOrder] (
    [Id] uniqueidentifier NOT NULL,
    [ContractorId] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [AssignedDate] datetime2 NOT NULL,
    [StartedDate] datetime2 NULL,
    [CompletedDate] datetime2 NULL,
    [Status] nvarchar(20) NOT NULL,
    [EstimatedCost] decimal(18,2) NULL,
    [ActualCost] decimal(18,2) NULL,
    [WorkPerformed] nvarchar(2000) NULL,
    [PartsUsed] nvarchar(max) NULL,
    [QualityRating] int NULL,
    [Notes] nvarchar(1000) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ContractorWorkOrder] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ContractorWorkOrder_MaintenanceContractor_ContractorId] FOREIGN KEY ([ContractorId]) REFERENCES [MaintenanceContractor] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ContractorWorkOrder_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [JobCard] (
    [Id] uniqueidentifier NOT NULL,
    [JobCardNumber] nvarchar(50) NOT NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [MaintenanceTypeId] uniqueidentifier NOT NULL,
    [PriorityLevelId] uniqueidentifier NOT NULL,
    [Title] nvarchar(500) NOT NULL,
    [Description] nvarchar(2000) NULL,
    [ProblemDescription] nvarchar(2000) NULL,
    [MaintenanceLocation] nvarchar(20) NOT NULL,
    [RequestedById] uniqueidentifier NOT NULL,
    [RequestedDate] datetime2 NOT NULL,
    [RequiredCompletionDate] datetime2 NULL,
    [EstimatedHours] float NOT NULL,
    [EstimatedCost] decimal(18,2) NOT NULL,
    [PreferredTechnicianId] uniqueidentifier NULL,
    [PreferredTeamId] uniqueidentifier NULL,
    [ContractorId] uniqueidentifier NULL,
    [RequiresSpecialTools] bit NOT NULL,
    [RequiresShutdown] bit NOT NULL,
    [RequiresSafetyPermit] bit NOT NULL,
    [SpecialInstructions] nvarchar(1000) NULL,
    [SafetyRequirements] nvarchar(1000) NULL,
    [JobCardStatus] nvarchar(20) NOT NULL,
    [ApprovalStatus] nvarchar(50) NOT NULL,
    [SubmittedById] uniqueidentifier NULL,
    [SubmittedDate] datetime2 NULL,
    [ApprovedById] uniqueidentifier NULL,
    [ApprovedDate] datetime2 NULL,
    [ApprovalComments] nvarchar(1000) NULL,
    [WorkflowInstanceId] uniqueidentifier NULL,
    [PlannedStartDate] datetime2 NULL,
    [PlannedEndDate] datetime2 NULL,
    [AssignedTechnicianId] uniqueidentifier NULL,
    [AssignedTeamId] uniqueidentifier NULL,
    [GeneratedWorkOrderId] uniqueidentifier NULL,
    [WorkOrderGeneratedAt] datetime2 NULL,
    [AttachmentPaths] nvarchar(max) NULL,
    [CustomFieldValues] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_JobCard] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_JobCard_Employees_AssignedTechnicianId] FOREIGN KEY ([AssignedTechnicianId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_JobCard_Employees_PreferredTechnicianId] FOREIGN KEY ([PreferredTechnicianId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_JobCard_Employees_RequestedById] FOREIGN KEY ([RequestedById]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_JobCard_MaintenanceAssets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [MaintenanceAssets] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_JobCard_MaintenanceContractor_ContractorId] FOREIGN KEY ([ContractorId]) REFERENCES [MaintenanceContractor] ([Id]),
    CONSTRAINT [FK_JobCard_MaintenanceTypes_MaintenanceTypeId] FOREIGN KEY ([MaintenanceTypeId]) REFERENCES [MaintenanceTypes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_JobCard_PriorityLevels_PriorityLevelId] FOREIGN KEY ([PriorityLevelId]) REFERENCES [PriorityLevels] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_JobCard_TechnicianTeams_AssignedTeamId] FOREIGN KEY ([AssignedTeamId]) REFERENCES [TechnicianTeams] ([Id]),
    CONSTRAINT [FK_JobCard_TechnicianTeams_PreferredTeamId] FOREIGN KEY ([PreferredTeamId]) REFERENCES [TechnicianTeams] ([Id]),
    CONSTRAINT [FK_JobCard_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_JobCard_WorkOrders_GeneratedWorkOrderId] FOREIGN KEY ([GeneratedWorkOrderId]) REFERENCES [WorkOrders] ([Id])
);
GO

CREATE TABLE [JobCardApprovalStep] (
    [Id] uniqueidentifier NOT NULL,
    [JobCardId] uniqueidentifier NOT NULL,
    [StepOrder] int NOT NULL,
    [StepName] nvarchar(100) NOT NULL,
    [ApproverId] uniqueidentifier NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [ActionDate] datetime2 NULL,
    [Comments] nvarchar(1000) NULL,
    [IsRequired] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_JobCardApprovalStep] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_JobCardApprovalStep_Employees_ApproverId] FOREIGN KEY ([ApproverId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_JobCardApprovalStep_JobCard_JobCardId] FOREIGN KEY ([JobCardId]) REFERENCES [JobCard] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_JobCardApprovalStep_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [JobCardComment] (
    [Id] uniqueidentifier NOT NULL,
    [JobCardId] uniqueidentifier NOT NULL,
    [CommentById] uniqueidentifier NOT NULL,
    [Comment] nvarchar(2000) NOT NULL,
    [CommentType] nvarchar(50) NOT NULL,
    [IsInternal] bit NOT NULL,
    [CommentDate] datetime2 NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_JobCardComment] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_JobCardComment_Employees_CommentById] FOREIGN KEY ([CommentById]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_JobCardComment_JobCard_JobCardId] FOREIGN KEY ([JobCardId]) REFERENCES [JobCard] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_JobCardComment_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [JobCardDocument] (
    [Id] uniqueidentifier NOT NULL,
    [JobCardId] uniqueidentifier NOT NULL,
    [FileName] nvarchar(255) NOT NULL,
    [FilePath] nvarchar(500) NOT NULL,
    [ContentType] nvarchar(100) NULL,
    [FileSize] bigint NOT NULL,
    [DocumentType] nvarchar(50) NOT NULL,
    [Description] nvarchar(500) NULL,
    [UploadedById] uniqueidentifier NOT NULL,
    [UploadedDate] datetime2 NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_JobCardDocument] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_JobCardDocument_Employees_UploadedById] FOREIGN KEY ([UploadedById]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_JobCardDocument_JobCard_JobCardId] FOREIGN KEY ([JobCardId]) REFERENCES [JobCard] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_JobCardDocument_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-17T00:38:54.3114886Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-17T00:38:54.3114944Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-17T00:38:54.3114946Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-17T00:38:54.3114948Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T00:38:54.3115178Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T00:38:54.3115197Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T00:38:54.3115203Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T00:38:54.3115209Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T00:38:54.3115218Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T00:38:54.3115224Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T00:38:54.3115229Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T00:38:54.3115234Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T00:38:54.3115242Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T00:38:54.3115251Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T00:38:54.3115257Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T00:38:54.3115263Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T00:38:54.3115271Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T00:38:54.3115283Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T00:38:54.3115288Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T00:38:54.3115292Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115331Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115337Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115338Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115339Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115339Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115341Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115341Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115342Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115343Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115351Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115352Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115352Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115353Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115354Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115354Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115355Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115450Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115452Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115453Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115453Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115454Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115454Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115455Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115456Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115456Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115457Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115457Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115458Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115459Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115459Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115460Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115528Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115529Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115531Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115531Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115532Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115533Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115533Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115534Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115535Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115535Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115546Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115547Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T00:38:54.3115548Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'CreatedById', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'LastModifiedById', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [CreatedById], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [LastModifiedById], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('2a1f0e53-06fe-4f38-9286-c7dd91e03270', NULL, '2025-10-17T00:38:54.3115072Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T00:38:54.3115071Z', CAST(0 AS bit), NULL, N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('2de78176-bfb7-44a8-9f32-ab77caf5aa2d', NULL, '2025-10-17T00:38:54.3115043Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T00:38:54.3115042Z', CAST(0 AS bit), NULL, N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('3879bc0c-dd3c-4587-95b7-a2c6dcf67b1d', NULL, '2025-10-17T00:38:54.3115013Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T00:38:54.3115010Z', CAST(0 AS bit), NULL, N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('5b4fdd71-a460-4191-815f-6168898fb903', NULL, '2025-10-17T00:38:54.3115057Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T00:38:54.3115056Z', CAST(0 AS bit), NULL, N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('6f6897aa-738f-4029-8ed1-3ae2b505c198', NULL, '2025-10-17T00:38:54.3115029Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T00:38:54.3115028Z', CAST(0 AS bit), NULL, N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('ee9d425c-b6ba-4f22-8db7-4d0e17078e38', NULL, '2025-10-17T00:38:54.3115086Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T00:38:54.3115085Z', CAST(0 AS bit), NULL, N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('fc0244e5-d779-4da3-8f0d-5b73d3bdfa1e', NULL, '2025-10-17T00:38:54.3115108Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T00:38:54.3115107Z', CAST(0 AS bit), NULL, N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'CreatedById', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'LastModifiedById', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-10-17T00:38:54.3114664Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_WorkOrders_CompletedById] ON [WorkOrders] ([CompletedById]);
GO

CREATE INDEX [IX_WorkOrders_ContractorId] ON [WorkOrders] ([ContractorId]);
GO

CREATE INDEX [IX_WorkOrders_QualityCheckedById] ON [WorkOrders] ([QualityCheckedById]);
GO

CREATE INDEX [IX_MaintenanceSchedules_MaintenanceTypeId1] ON [MaintenanceSchedules] ([MaintenanceTypeId1]);
GO

CREATE INDEX [IX_ContractorInvoice_MaintenanceContractorId] ON [ContractorInvoice] ([MaintenanceContractorId]);
GO

CREATE INDEX [IX_ContractorPerformanceReview_ContractorId] ON [ContractorPerformanceReview] ([ContractorId]);
GO

CREATE INDEX [IX_ContractorWorkOrder_ContractorId] ON [ContractorWorkOrder] ([ContractorId]);
GO

CREATE UNIQUE INDEX [IX_ContractorWorkOrder_WorkOrderId] ON [ContractorWorkOrder] ([WorkOrderId]);
GO

CREATE INDEX [IX_JobCard_AssetId] ON [JobCard] ([AssetId]);
GO

CREATE INDEX [IX_JobCard_AssignedTeamId] ON [JobCard] ([AssignedTeamId]);
GO

CREATE INDEX [IX_JobCard_AssignedTechnicianId] ON [JobCard] ([AssignedTechnicianId]);
GO

CREATE INDEX [IX_JobCard_ContractorId] ON [JobCard] ([ContractorId]);
GO

CREATE UNIQUE INDEX [IX_JobCard_GeneratedWorkOrderId] ON [JobCard] ([GeneratedWorkOrderId]) WHERE [GeneratedWorkOrderId] IS NOT NULL;
GO

CREATE INDEX [IX_JobCard_MaintenanceTypeId] ON [JobCard] ([MaintenanceTypeId]);
GO

CREATE INDEX [IX_JobCard_PreferredTeamId] ON [JobCard] ([PreferredTeamId]);
GO

CREATE INDEX [IX_JobCard_PreferredTechnicianId] ON [JobCard] ([PreferredTechnicianId]);
GO

CREATE INDEX [IX_JobCard_PriorityLevelId] ON [JobCard] ([PriorityLevelId]);
GO

CREATE INDEX [IX_JobCard_RequestedById] ON [JobCard] ([RequestedById]);
GO

CREATE INDEX [IX_JobCard_TenantId] ON [JobCard] ([TenantId]);
GO

CREATE INDEX [IX_JobCardApprovalStep_ApproverId] ON [JobCardApprovalStep] ([ApproverId]);
GO

CREATE INDEX [IX_JobCardApprovalStep_JobCardId] ON [JobCardApprovalStep] ([JobCardId]);
GO

CREATE INDEX [IX_JobCardApprovalStep_TenantId] ON [JobCardApprovalStep] ([TenantId]);
GO

CREATE INDEX [IX_JobCardComment_CommentById] ON [JobCardComment] ([CommentById]);
GO

CREATE INDEX [IX_JobCardComment_JobCardId] ON [JobCardComment] ([JobCardId]);
GO

CREATE INDEX [IX_JobCardComment_TenantId] ON [JobCardComment] ([TenantId]);
GO

CREATE INDEX [IX_JobCardDocument_JobCardId] ON [JobCardDocument] ([JobCardId]);
GO

CREATE INDEX [IX_JobCardDocument_TenantId] ON [JobCardDocument] ([TenantId]);
GO

CREATE INDEX [IX_JobCardDocument_UploadedById] ON [JobCardDocument] ([UploadedById]);
GO

ALTER TABLE [MaintenanceSchedules] ADD CONSTRAINT [FK_MaintenanceSchedules_MaintenanceTypes_MaintenanceTypeId1] FOREIGN KEY ([MaintenanceTypeId1]) REFERENCES [MaintenanceTypes] ([Id]);
GO

ALTER TABLE [WorkOrders] ADD CONSTRAINT [FK_WorkOrders_Employees_CompletedById] FOREIGN KEY ([CompletedById]) REFERENCES [Employees] ([Id]);
GO

ALTER TABLE [WorkOrders] ADD CONSTRAINT [FK_WorkOrders_Employees_QualityCheckedById] FOREIGN KEY ([QualityCheckedById]) REFERENCES [Employees] ([Id]);
GO

ALTER TABLE [WorkOrders] ADD CONSTRAINT [FK_WorkOrders_Employees_SupervisorId] FOREIGN KEY ([SupervisorId]) REFERENCES [Employees] ([Id]);
GO

ALTER TABLE [WorkOrders] ADD CONSTRAINT [FK_WorkOrders_MaintenanceContractor_ContractorId] FOREIGN KEY ([ContractorId]) REFERENCES [MaintenanceContractor] ([Id]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251017003857_AddContractorIdToWorkOrder', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '2a1f0e53-06fe-4f38-9286-c7dd91e03270';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '2de78176-bfb7-44a8-9f32-ab77caf5aa2d';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '3879bc0c-dd3c-4587-95b7-a2c6dcf67b1d';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '5b4fdd71-a460-4191-815f-6168898fb903';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '6f6897aa-738f-4029-8ed1-3ae2b505c198';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'ee9d425c-b6ba-4f22-8db7-4d0e17078e38';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'fc0244e5-d779-4da3-8f0d-5b73d3bdfa1e';
SELECT @@ROWCOUNT;

GO

CREATE TABLE [InspectionApprovals] (
    [Id] uniqueidentifier NOT NULL,
    [InspectionId] uniqueidentifier NOT NULL,
    [ApprovalLevel] int NOT NULL,
    [ApproverId] uniqueidentifier NOT NULL,
    [ApproverRole] nvarchar(100) NULL,
    [Status] nvarchar(20) NOT NULL,
    [RequestedDate] datetime2 NOT NULL,
    [DueDate] datetime2 NULL,
    [ApprovedDate] datetime2 NULL,
    [Comments] nvarchar(2000) NULL,
    [Conditions] nvarchar(1000) NULL,
    [Priority] int NOT NULL,
    [CanDelegate] bit NOT NULL,
    [DelegatedToId] uniqueidentifier NULL,
    [DelegatedDate] datetime2 NULL,
    [DelegationReason] nvarchar(500) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_InspectionApprovals] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InspectionApprovals_AssetInspections_InspectionId] FOREIGN KEY ([InspectionId]) REFERENCES [AssetInspections] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_InspectionApprovals_Employees_ApproverId] FOREIGN KEY ([ApproverId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_InspectionApprovals_Employees_DelegatedToId] FOREIGN KEY ([DelegatedToId]) REFERENCES [Employees] ([Id])
);
GO

CREATE TABLE [InspectionChecklistTemplates] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Category] nvarchar(50) NOT NULL,
    [ApplicableAssetTypes] nvarchar(max) NULL,
    [ApplicableMaintenanceTypes] nvarchar(max) NULL,
    [ApplicableWorkOrderTypes] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [IsDefault] bit NOT NULL,
    [SortOrder] int NOT NULL,
    [Version] int NOT NULL,
    [VersionNotes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_InspectionChecklistTemplates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InspectionChecklistTemplates_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [QualityControlChecklists] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [WorkOrderType] nvarchar(50) NULL,
    [AssetCategory] nvarchar(50) NULL,
    [MaintenanceType] nvarchar(50) NULL,
    [IsMandatory] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [ChecklistItems] nvarchar(max) NOT NULL,
    [MinimumPassingScore] int NOT NULL,
    [Version] int NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    [LastModifiedDate] datetime2 NULL,
    [CreatedById] uniqueidentifier NOT NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_QualityControlChecklists] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [QualityMetrics] (
    [Id] uniqueidentifier NOT NULL,
    [MetricsDate] datetime2 NOT NULL,
    [TechnicianId] uniqueidentifier NULL,
    [TeamId] uniqueidentifier NULL,
    [AssetCategory] nvarchar(50) NULL,
    [TotalWorkOrders] int NOT NULL,
    [FirstTimePassCount] int NOT NULL,
    [ReworkCount] int NOT NULL,
    [RejectedCount] int NOT NULL,
    [FirstTimeFixRate] float NOT NULL,
    [AverageQualityScore] float NOT NULL,
    [CustomerSatisfactionScore] float NULL,
    [AverageInspectionTime] float NOT NULL,
    [SafetyViolations] int NOT NULL,
    [CompliancePercentage] float NOT NULL,
    [CalculatedDate] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_QualityMetrics] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [WorkOrderQualitySignOffs] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [SignOffLevel] int NOT NULL,
    [SignOffRole] nvarchar(100) NOT NULL,
    [SignOffById] uniqueidentifier NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [SignOffDate] datetime2 NULL,
    [Comments] nvarchar(2000) NULL,
    [Conditions] nvarchar(1000) NULL,
    [RejectionReason] nvarchar(1000) NULL,
    [QualityRating] int NULL,
    [SafetyCompliant] bit NOT NULL,
    [WorkmanshipSatisfactory] bit NOT NULL,
    [MaterialsAcceptable] bit NOT NULL,
    [TestingComplete] bit NOT NULL,
    [DocumentationComplete] bit NOT NULL,
    [DelegatedToId] uniqueidentifier NULL,
    [DelegatedDate] datetime2 NULL,
    [DelegationReason] nvarchar(500) NULL,
    [PhotoPaths] nvarchar(max) NULL,
    [DocumentPaths] nvarchar(max) NULL,
    [RequiresFollowUp] bit NOT NULL,
    [FollowUpDate] datetime2 NULL,
    [FollowUpInstructions] nvarchar(1000) NULL,
    [IsRequired] bit NOT NULL,
    [SortOrder] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkOrderQualitySignOffs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderQualitySignOffs_Employees_DelegatedToId] FOREIGN KEY ([DelegatedToId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrderQualitySignOffs_Employees_SignOffById] FOREIGN KEY ([SignOffById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrderQualitySignOffs_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrderQualitySignOffs_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WorkOrderRejections] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [RejectedById] uniqueidentifier NOT NULL,
    [RejectedDate] datetime2 NOT NULL,
    [RejectionType] nvarchar(50) NOT NULL,
    [RejectionReason] nvarchar(2000) NOT NULL,
    [Severity] nvarchar(20) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [ReworkAssignedToId] uniqueidentifier NULL,
    [ReworkAssignedDate] datetime2 NULL,
    [ReworkDueDate] datetime2 NULL,
    [ReworkCompletedDate] datetime2 NULL,
    [EstimatedReworkCost] decimal(18,2) NOT NULL,
    [ActualReworkCost] decimal(18,2) NOT NULL,
    [EstimatedReworkHours] float NOT NULL,
    [ActualReworkHours] float NOT NULL,
    [CustomerNotified] bit NOT NULL,
    [CustomerNotifiedDate] datetime2 NULL,
    [AffectsDelivery] bit NOT NULL,
    [RevisedDeliveryDate] datetime2 NULL,
    [ResolutionNotes] nvarchar(2000) NULL,
    [ResolvedById] uniqueidentifier NULL,
    [ResolvedDate] datetime2 NULL,
    [RequiresReinspection] bit NOT NULL,
    [ReinspectedById] uniqueidentifier NULL,
    [ReinspectedDate] datetime2 NULL,
    [ReinspectionResult] nvarchar(20) NULL,
    [PhotoPaths] nvarchar(max) NULL,
    [DocumentPaths] nvarchar(max) NULL,
    [IsEscalated] bit NOT NULL,
    [EscalatedToId] uniqueidentifier NULL,
    [EscalatedDate] datetime2 NULL,
    [EscalationReason] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkOrderRejections] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderRejections_Employees_EscalatedToId] FOREIGN KEY ([EscalatedToId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrderRejections_Employees_ReinspectedById] FOREIGN KEY ([ReinspectedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrderRejections_Employees_RejectedById] FOREIGN KEY ([RejectedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrderRejections_Employees_ResolvedById] FOREIGN KEY ([ResolvedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrderRejections_Employees_ReworkAssignedToId] FOREIGN KEY ([ReworkAssignedToId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrderRejections_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrderRejections_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [InspectionChecklistItems] (
    [Id] uniqueidentifier NOT NULL,
    [TemplateId] uniqueidentifier NOT NULL,
    [ItemText] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Category] nvarchar(50) NOT NULL,
    [ItemType] nvarchar(20) NOT NULL,
    [IsRequired] bit NOT NULL,
    [IsCritical] bit NOT NULL,
    [SortOrder] int NOT NULL,
    [ValidationRules] nvarchar(max) NULL,
    [ChoiceOptions] nvarchar(max) NULL,
    [DefaultValue] nvarchar(500) NULL,
    [HelpText] nvarchar(1000) NULL,
    [RequiresPhoto] bit NOT NULL,
    [MinPhotos] int NOT NULL,
    [MaxPhotos] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_InspectionChecklistItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InspectionChecklistItems_InspectionChecklistTemplates_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [InspectionChecklistTemplates] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_InspectionChecklistItems_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [WorkOrderQualityChecks] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [ChecklistId] uniqueidentifier NOT NULL,
    [InspectorId] uniqueidentifier NOT NULL,
    [InspectionDate] datetime2 NOT NULL,
    [OverallResult] nvarchar(20) NOT NULL,
    [Score] int NOT NULL,
    [CheckResults] nvarchar(max) NOT NULL,
    [Notes] nvarchar(2000) NULL,
    [CorrectiveActions] nvarchar(2000) NULL,
    [RequiresFollowUp] bit NOT NULL,
    [FollowUpDueDate] datetime2 NULL,
    [AttachmentPaths] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkOrderQualityChecks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderQualityChecks_QualityControlChecklists_ChecklistId] FOREIGN KEY ([ChecklistId]) REFERENCES [QualityControlChecklists] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [QualitySignOffChecklists] (
    [Id] uniqueidentifier NOT NULL,
    [SignOffId] uniqueidentifier NOT NULL,
    [CheckItem] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Category] nvarchar(50) NOT NULL,
    [CheckType] nvarchar(20) NOT NULL,
    [IsRequired] bit NOT NULL,
    [SortOrder] int NOT NULL,
    [BooleanResult] bit NULL,
    [RatingResult] int NULL,
    [MeasurementResult] decimal(18,4) NULL,
    [MeasurementUnit] nvarchar(50) NULL,
    [TextResult] nvarchar(1000) NULL,
    [Notes] nvarchar(1000) NULL,
    [PhotoPaths] nvarchar(max) NULL,
    [CheckedDate] datetime2 NULL,
    [CheckedById] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_QualitySignOffChecklists] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_QualitySignOffChecklists_Employees_CheckedById] FOREIGN KEY ([CheckedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_QualitySignOffChecklists_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_QualitySignOffChecklists_WorkOrderQualitySignOffs_SignOffId] FOREIGN KEY ([SignOffId]) REFERENCES [WorkOrderQualitySignOffs] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [RejectionFollowUps] (
    [Id] uniqueidentifier NOT NULL,
    [RejectionId] uniqueidentifier NOT NULL,
    [FollowUpAction] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [AssignedToId] uniqueidentifier NOT NULL,
    [DueDate] datetime2 NULL,
    [Status] nvarchar(20) NOT NULL,
    [CompletedDate] datetime2 NULL,
    [CompletedById] uniqueidentifier NULL,
    [CompletionNotes] nvarchar(1000) NULL,
    [Priority] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_RejectionFollowUps] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RejectionFollowUps_Employees_AssignedToId] FOREIGN KEY ([AssignedToId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_RejectionFollowUps_Employees_CompletedById] FOREIGN KEY ([CompletedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_RejectionFollowUps_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_RejectionFollowUps_WorkOrderRejections_RejectionId] FOREIGN KEY ([RejectionId]) REFERENCES [WorkOrderRejections] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WorkOrderReworks] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [InspectorId] uniqueidentifier NOT NULL,
    [ReworkReason] nvarchar(50) NOT NULL,
    [Description] nvarchar(2000) NOT NULL,
    [Severity] nvarchar(20) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [AssignedTechnicianId] uniqueidentifier NULL,
    [IdentifiedDate] datetime2 NOT NULL,
    [TargetCompletionDate] datetime2 NULL,
    [StartedDate] datetime2 NULL,
    [CompletedDate] datetime2 NULL,
    [EstimatedHours] float NULL,
    [ActualHours] float NULL,
    [AdditionalCost] decimal(18,2) NULL,
    [Notes] nvarchar(2000) NULL,
    [AttachmentPaths] nvarchar(max) NULL,
    [RequiresCustomerNotification] bit NOT NULL,
    [AffectsWarranty] bit NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [WorkOrderQualityCheckId] uniqueidentifier NULL,
    CONSTRAINT [PK_WorkOrderReworks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderReworks_WorkOrderQualityChecks_WorkOrderQualityCheckId] FOREIGN KEY ([WorkOrderQualityCheckId]) REFERENCES [WorkOrderQualityChecks] ([Id]),
    CONSTRAINT [FK_WorkOrderReworks_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WorkOrderReworkTasks] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderReworkId] uniqueidentifier NOT NULL,
    [TaskDescription] nvarchar(200) NOT NULL,
    [Sequence] int NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [EstimatedHours] float NULL,
    [ActualHours] float NULL,
    [CompletedDate] datetime2 NULL,
    [CompletedById] uniqueidentifier NULL,
    [Notes] nvarchar(1000) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkOrderReworkTasks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderReworkTasks_WorkOrderReworks_WorkOrderReworkId] FOREIGN KEY ([WorkOrderReworkId]) REFERENCES [WorkOrderReworks] ([Id]) ON DELETE CASCADE
);
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-17T06:11:58.2205569Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-17T06:11:58.2205610Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-17T06:11:58.2205613Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-17T06:11:58.2205615Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T06:11:58.2205850Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T06:11:58.2205858Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T06:11:58.2205862Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T06:11:58.2205867Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T06:11:58.2205882Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T06:11:58.2205888Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T06:11:58.2205893Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T06:11:58.2205898Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T06:11:58.2205905Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T06:11:58.2205914Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T06:11:58.2205920Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T06:11:58.2205924Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T06:11:58.2205932Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T06:11:58.2205937Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T06:11:58.2205946Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T06:11:58.2205952Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2205984Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2205987Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2205988Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2205988Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2205989Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2205990Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2205991Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2205992Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2205992Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2205994Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2205994Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2205995Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2205995Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2205996Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2205997Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2205997Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206083Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206085Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206086Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206086Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206087Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206087Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206088Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206088Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206089Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206089Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206090Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206091Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206091Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206092Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206092Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206144Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206145Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206146Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206147Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206147Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206148Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206148Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206149Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206150Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206150Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206160Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206161Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T06:11:58.2206161Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'CreatedById', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'LastModifiedById', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [CreatedById], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [LastModifiedById], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('0f23e62f-7ddd-4a4c-82d3-24e34c7ea1fb', NULL, '2025-10-17T06:11:58.2205692Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T06:11:58.2205691Z', CAST(0 AS bit), NULL, N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('1f5997e9-22e8-4045-b1e6-a0daf9007ee7', NULL, '2025-10-17T06:11:58.2205760Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T06:11:58.2205759Z', CAST(0 AS bit), NULL, N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('2d999c82-127d-4d5f-9373-baebd4205cfc', NULL, '2025-10-17T06:11:58.2205729Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T06:11:58.2205729Z', CAST(0 AS bit), NULL, N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('45d9777e-569e-4482-8181-a808ec6157e1', NULL, '2025-10-17T06:11:58.2205673Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T06:11:58.2205670Z', CAST(0 AS bit), NULL, N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('d56f42c2-ecfe-4b59-b077-27def1898e71', NULL, '2025-10-17T06:11:58.2205744Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T06:11:58.2205744Z', CAST(0 AS bit), NULL, N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('e7317ef8-a2f0-4d87-a46b-3d8371c5367f', NULL, '2025-10-17T06:11:58.2205713Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T06:11:58.2205713Z', CAST(0 AS bit), NULL, N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('f947323f-ef70-4e4e-998a-25bbb496aeda', NULL, '2025-10-17T06:11:58.2205777Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T06:11:58.2205777Z', CAST(0 AS bit), NULL, N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'CreatedById', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'LastModifiedById', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-10-17T06:11:58.2205394Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_InspectionApprovals_ApprovalLevel] ON [InspectionApprovals] ([ApprovalLevel]);
GO

CREATE INDEX [IX_InspectionApprovals_ApprovedDate] ON [InspectionApprovals] ([ApprovedDate]);
GO

CREATE INDEX [IX_InspectionApprovals_ApproverId] ON [InspectionApprovals] ([ApproverId]);
GO

CREATE INDEX [IX_InspectionApprovals_DelegatedToId] ON [InspectionApprovals] ([DelegatedToId]);
GO

CREATE INDEX [IX_InspectionApprovals_DueDate] ON [InspectionApprovals] ([DueDate]);
GO

CREATE INDEX [IX_InspectionApprovals_InspectionId] ON [InspectionApprovals] ([InspectionId]);
GO

CREATE INDEX [IX_InspectionApprovals_Priority] ON [InspectionApprovals] ([Priority]);
GO

CREATE INDEX [IX_InspectionApprovals_RequestedDate] ON [InspectionApprovals] ([RequestedDate]);
GO

CREATE INDEX [IX_InspectionApprovals_Status] ON [InspectionApprovals] ([Status]);
GO

CREATE INDEX [IX_InspectionChecklistItems_Category] ON [InspectionChecklistItems] ([Category]);
GO

CREATE INDEX [IX_InspectionChecklistItems_IsCritical] ON [InspectionChecklistItems] ([IsCritical]);
GO

CREATE INDEX [IX_InspectionChecklistItems_IsRequired] ON [InspectionChecklistItems] ([IsRequired]);
GO

CREATE INDEX [IX_InspectionChecklistItems_ItemType] ON [InspectionChecklistItems] ([ItemType]);
GO

CREATE INDEX [IX_InspectionChecklistItems_RequiresPhoto] ON [InspectionChecklistItems] ([RequiresPhoto]);
GO

CREATE INDEX [IX_InspectionChecklistItems_SortOrder] ON [InspectionChecklistItems] ([SortOrder]);
GO

CREATE INDEX [IX_InspectionChecklistItems_TemplateId] ON [InspectionChecklistItems] ([TemplateId]);
GO

CREATE INDEX [IX_InspectionChecklistItems_TenantId] ON [InspectionChecklistItems] ([TenantId]);
GO

CREATE INDEX [IX_InspectionChecklistTemplates_Category] ON [InspectionChecklistTemplates] ([Category]);
GO

CREATE INDEX [IX_InspectionChecklistTemplates_IsActive] ON [InspectionChecklistTemplates] ([IsActive]);
GO

CREATE INDEX [IX_InspectionChecklistTemplates_IsDefault] ON [InspectionChecklistTemplates] ([IsDefault]);
GO

CREATE INDEX [IX_InspectionChecklistTemplates_Name] ON [InspectionChecklistTemplates] ([Name]);
GO

CREATE INDEX [IX_InspectionChecklistTemplates_SortOrder] ON [InspectionChecklistTemplates] ([SortOrder]);
GO

CREATE INDEX [IX_InspectionChecklistTemplates_TenantId] ON [InspectionChecklistTemplates] ([TenantId]);
GO

CREATE INDEX [IX_InspectionChecklistTemplates_Version] ON [InspectionChecklistTemplates] ([Version]);
GO

CREATE INDEX [IX_QualityControlChecklists_AssetCategory] ON [QualityControlChecklists] ([AssetCategory]);
GO

CREATE INDEX [IX_QualityControlChecklists_IsActive] ON [QualityControlChecklists] ([IsActive]);
GO

CREATE INDEX [IX_QualityControlChecklists_IsMandatory] ON [QualityControlChecklists] ([IsMandatory]);
GO

CREATE INDEX [IX_QualityControlChecklists_MaintenanceType] ON [QualityControlChecklists] ([MaintenanceType]);
GO

CREATE INDEX [IX_QualityControlChecklists_Name] ON [QualityControlChecklists] ([Name]);
GO

CREATE INDEX [IX_QualityControlChecklists_Version] ON [QualityControlChecklists] ([Version]);
GO

CREATE INDEX [IX_QualityControlChecklists_WorkOrderType] ON [QualityControlChecklists] ([WorkOrderType]);
GO

CREATE INDEX [IX_QualityMetrics_AssetCategory] ON [QualityMetrics] ([AssetCategory]);
GO

CREATE INDEX [IX_QualityMetrics_CalculatedDate] ON [QualityMetrics] ([CalculatedDate]);
GO

CREATE INDEX [IX_QualityMetrics_MetricsDate] ON [QualityMetrics] ([MetricsDate]);
GO

CREATE INDEX [IX_QualityMetrics_TeamId] ON [QualityMetrics] ([TeamId]);
GO

CREATE INDEX [IX_QualityMetrics_TechnicianId] ON [QualityMetrics] ([TechnicianId]);
GO

CREATE INDEX [IX_QualitySignOffChecklists_Category] ON [QualitySignOffChecklists] ([Category]);
GO

CREATE INDEX [IX_QualitySignOffChecklists_CheckedById] ON [QualitySignOffChecklists] ([CheckedById]);
GO

CREATE INDEX [IX_QualitySignOffChecklists_CheckedDate] ON [QualitySignOffChecklists] ([CheckedDate]);
GO

CREATE INDEX [IX_QualitySignOffChecklists_CheckType] ON [QualitySignOffChecklists] ([CheckType]);
GO

CREATE INDEX [IX_QualitySignOffChecklists_IsRequired] ON [QualitySignOffChecklists] ([IsRequired]);
GO

CREATE INDEX [IX_QualitySignOffChecklists_SignOffId] ON [QualitySignOffChecklists] ([SignOffId]);
GO

CREATE INDEX [IX_QualitySignOffChecklists_SortOrder] ON [QualitySignOffChecklists] ([SortOrder]);
GO

CREATE INDEX [IX_QualitySignOffChecklists_TenantId] ON [QualitySignOffChecklists] ([TenantId]);
GO

CREATE INDEX [IX_RejectionFollowUps_AssignedToId] ON [RejectionFollowUps] ([AssignedToId]);
GO

CREATE INDEX [IX_RejectionFollowUps_CompletedById] ON [RejectionFollowUps] ([CompletedById]);
GO

CREATE INDEX [IX_RejectionFollowUps_CompletedDate] ON [RejectionFollowUps] ([CompletedDate]);
GO

CREATE INDEX [IX_RejectionFollowUps_DueDate] ON [RejectionFollowUps] ([DueDate]);
GO

CREATE INDEX [IX_RejectionFollowUps_Priority] ON [RejectionFollowUps] ([Priority]);
GO

CREATE INDEX [IX_RejectionFollowUps_RejectionId] ON [RejectionFollowUps] ([RejectionId]);
GO

CREATE INDEX [IX_RejectionFollowUps_Status] ON [RejectionFollowUps] ([Status]);
GO

CREATE INDEX [IX_RejectionFollowUps_TenantId] ON [RejectionFollowUps] ([TenantId]);
GO

CREATE INDEX [IX_WorkOrderQualityChecks_ChecklistId] ON [WorkOrderQualityChecks] ([ChecklistId]);
GO

CREATE INDEX [IX_WorkOrderQualityChecks_InspectionDate] ON [WorkOrderQualityChecks] ([InspectionDate]);
GO

CREATE INDEX [IX_WorkOrderQualityChecks_InspectorId] ON [WorkOrderQualityChecks] ([InspectorId]);
GO

CREATE INDEX [IX_WorkOrderQualityChecks_OverallResult] ON [WorkOrderQualityChecks] ([OverallResult]);
GO

CREATE INDEX [IX_WorkOrderQualityChecks_RequiresFollowUp] ON [WorkOrderQualityChecks] ([RequiresFollowUp]);
GO

CREATE INDEX [IX_WorkOrderQualityChecks_Score] ON [WorkOrderQualityChecks] ([Score]);
GO

CREATE INDEX [IX_WorkOrderQualityChecks_WorkOrderId] ON [WorkOrderQualityChecks] ([WorkOrderId]);
GO

CREATE INDEX [IX_WorkOrderQualitySignOffs_DelegatedToId] ON [WorkOrderQualitySignOffs] ([DelegatedToId]);
GO

CREATE INDEX [IX_WorkOrderQualitySignOffs_IsRequired] ON [WorkOrderQualitySignOffs] ([IsRequired]);
GO

CREATE INDEX [IX_WorkOrderQualitySignOffs_SignOffById] ON [WorkOrderQualitySignOffs] ([SignOffById]);
GO

CREATE INDEX [IX_WorkOrderQualitySignOffs_SignOffDate] ON [WorkOrderQualitySignOffs] ([SignOffDate]);
GO

CREATE INDEX [IX_WorkOrderQualitySignOffs_SignOffLevel] ON [WorkOrderQualitySignOffs] ([SignOffLevel]);
GO

CREATE INDEX [IX_WorkOrderQualitySignOffs_SignOffRole] ON [WorkOrderQualitySignOffs] ([SignOffRole]);
GO

CREATE INDEX [IX_WorkOrderQualitySignOffs_SortOrder] ON [WorkOrderQualitySignOffs] ([SortOrder]);
GO

CREATE INDEX [IX_WorkOrderQualitySignOffs_Status] ON [WorkOrderQualitySignOffs] ([Status]);
GO

CREATE INDEX [IX_WorkOrderQualitySignOffs_TenantId] ON [WorkOrderQualitySignOffs] ([TenantId]);
GO

CREATE INDEX [IX_WorkOrderQualitySignOffs_WorkOrderId] ON [WorkOrderQualitySignOffs] ([WorkOrderId]);
GO

CREATE INDEX [IX_WorkOrderRejections_EscalatedToId] ON [WorkOrderRejections] ([EscalatedToId]);
GO

CREATE INDEX [IX_WorkOrderRejections_IsEscalated] ON [WorkOrderRejections] ([IsEscalated]);
GO

CREATE INDEX [IX_WorkOrderRejections_ReinspectedById] ON [WorkOrderRejections] ([ReinspectedById]);
GO

CREATE INDEX [IX_WorkOrderRejections_RejectedById] ON [WorkOrderRejections] ([RejectedById]);
GO

CREATE INDEX [IX_WorkOrderRejections_RejectedDate] ON [WorkOrderRejections] ([RejectedDate]);
GO

CREATE INDEX [IX_WorkOrderRejections_RejectionType] ON [WorkOrderRejections] ([RejectionType]);
GO

CREATE INDEX [IX_WorkOrderRejections_RequiresReinspection] ON [WorkOrderRejections] ([RequiresReinspection]);
GO

CREATE INDEX [IX_WorkOrderRejections_ResolvedById] ON [WorkOrderRejections] ([ResolvedById]);
GO

CREATE INDEX [IX_WorkOrderRejections_ReworkAssignedToId] ON [WorkOrderRejections] ([ReworkAssignedToId]);
GO

CREATE INDEX [IX_WorkOrderRejections_Severity] ON [WorkOrderRejections] ([Severity]);
GO

CREATE INDEX [IX_WorkOrderRejections_Status] ON [WorkOrderRejections] ([Status]);
GO

CREATE INDEX [IX_WorkOrderRejections_TenantId] ON [WorkOrderRejections] ([TenantId]);
GO

CREATE INDEX [IX_WorkOrderRejections_WorkOrderId] ON [WorkOrderRejections] ([WorkOrderId]);
GO

CREATE INDEX [IX_WorkOrderReworks_AssignedTechnicianId] ON [WorkOrderReworks] ([AssignedTechnicianId]);
GO

CREATE INDEX [IX_WorkOrderReworks_IdentifiedDate] ON [WorkOrderReworks] ([IdentifiedDate]);
GO

CREATE INDEX [IX_WorkOrderReworks_InspectorId] ON [WorkOrderReworks] ([InspectorId]);
GO

CREATE INDEX [IX_WorkOrderReworks_ReworkReason] ON [WorkOrderReworks] ([ReworkReason]);
GO

CREATE INDEX [IX_WorkOrderReworks_Severity] ON [WorkOrderReworks] ([Severity]);
GO

CREATE INDEX [IX_WorkOrderReworks_Status] ON [WorkOrderReworks] ([Status]);
GO

CREATE INDEX [IX_WorkOrderReworks_TargetCompletionDate] ON [WorkOrderReworks] ([TargetCompletionDate]);
GO

CREATE INDEX [IX_WorkOrderReworks_WorkOrderId] ON [WorkOrderReworks] ([WorkOrderId]);
GO

CREATE INDEX [IX_WorkOrderReworks_WorkOrderQualityCheckId] ON [WorkOrderReworks] ([WorkOrderQualityCheckId]);
GO

CREATE INDEX [IX_WorkOrderReworkTasks_CompletedById] ON [WorkOrderReworkTasks] ([CompletedById]);
GO

CREATE INDEX [IX_WorkOrderReworkTasks_CompletedDate] ON [WorkOrderReworkTasks] ([CompletedDate]);
GO

CREATE INDEX [IX_WorkOrderReworkTasks_Sequence] ON [WorkOrderReworkTasks] ([Sequence]);
GO

CREATE INDEX [IX_WorkOrderReworkTasks_Status] ON [WorkOrderReworkTasks] ([Status]);
GO

CREATE INDEX [IX_WorkOrderReworkTasks_WorkOrderReworkId] ON [WorkOrderReworkTasks] ([WorkOrderReworkId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251017061200_AddQualityControlTables', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [MaintenanceAssets] DROP CONSTRAINT [FK_MaintenanceAssets_AssetTypes_AssetTypeId];
GO

DELETE FROM [TenantModules]
WHERE [Id] = '0f23e62f-7ddd-4a4c-82d3-24e34c7ea1fb';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '1f5997e9-22e8-4045-b1e6-a0daf9007ee7';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '2d999c82-127d-4d5f-9373-baebd4205cfc';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '45d9777e-569e-4482-8181-a808ec6157e1';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'd56f42c2-ecfe-4b59-b077-27def1898e71';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'e7317ef8-a2f0-4d87-a46b-3d8371c5367f';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'f947323f-ef70-4e4e-998a-25bbb496aeda';
SELECT @@ROWCOUNT;

GO

DECLARE @var34 sysname;
SELECT @var34 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceAssets]') AND [c].[name] = N'AssetTypeId');
IF @var34 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceAssets] DROP CONSTRAINT [' + @var34 + '];');
ALTER TABLE [MaintenanceAssets] ALTER COLUMN [AssetTypeId] uniqueidentifier NULL;
GO

ALTER TABLE [MaintenanceAssetCategories] ADD [AssetType] nvarchar(50) NULL;
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-17T17:59:32.2375154Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-17T17:59:32.2375199Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-17T17:59:32.2375202Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-17T17:59:32.2375204Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T17:59:32.2375409Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T17:59:32.2375425Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T17:59:32.2375430Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T17:59:32.2375435Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T17:59:32.2375445Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T17:59:32.2375452Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T17:59:32.2375456Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T17:59:32.2375460Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T17:59:32.2375468Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T17:59:32.2375474Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T17:59:32.2375479Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T17:59:32.2375484Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T17:59:32.2375491Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T17:59:32.2375507Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T17:59:32.2375511Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T17:59:32.2375516Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375550Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375554Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375555Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375556Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375557Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375558Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375565Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375565Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375566Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375567Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375568Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375568Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375569Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375570Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375570Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375571Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375627Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375628Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375629Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375630Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375630Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375631Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375632Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375632Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375633Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375633Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375634Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375634Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375635Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375636Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375636Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375664Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375666Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375667Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375668Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375668Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375669Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375670Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375670Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375671Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375671Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375684Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375685Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T17:59:32.2375686Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'CreatedById', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'LastModifiedById', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [CreatedById], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [LastModifiedById], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('012a12c5-3277-4175-83d0-42ca04114d3e', NULL, '2025-10-17T17:59:32.2375300Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T17:59:32.2375300Z', CAST(0 AS bit), NULL, N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('11f22aea-4c28-478b-9c63-b585f661c335', NULL, '2025-10-17T17:59:32.2375262Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T17:59:32.2375260Z', CAST(0 AS bit), NULL, N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('2b2654ef-bd6c-4061-a93f-139e683edbd5', NULL, '2025-10-17T17:59:32.2375277Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T17:59:32.2375276Z', CAST(0 AS bit), NULL, N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('8a0fac4e-7869-4d05-9152-62c534332b5e', NULL, '2025-10-17T17:59:32.2375289Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T17:59:32.2375288Z', CAST(0 AS bit), NULL, N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('9e3cc6eb-1e7c-4b7f-a8e0-e708f5ccf0f6', NULL, '2025-10-17T17:59:32.2375325Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T17:59:32.2375324Z', CAST(0 AS bit), NULL, N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('b7f0807f-f456-4706-9b66-9fa150135a73', NULL, '2025-10-17T17:59:32.2375312Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T17:59:32.2375312Z', CAST(0 AS bit), NULL, N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('b807024f-a4ac-4db8-95df-cc1c8bba6aaa', NULL, '2025-10-17T17:59:32.2375342Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T17:59:32.2375342Z', CAST(0 AS bit), NULL, N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'CreatedById', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'LastModifiedById', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-10-17T17:59:32.2374979Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

ALTER TABLE [MaintenanceAssets] ADD CONSTRAINT [FK_MaintenanceAssets_AssetTypes_AssetTypeId] FOREIGN KEY ([AssetTypeId]) REFERENCES [AssetTypes] ([Id]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251017175934_RemoveAssetTypeIdFromMaintenanceAssets', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [TenantModules]
WHERE [Id] = '012a12c5-3277-4175-83d0-42ca04114d3e';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '11f22aea-4c28-478b-9c63-b585f661c335';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '2b2654ef-bd6c-4061-a93f-139e683edbd5';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '8a0fac4e-7869-4d05-9152-62c534332b5e';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = '9e3cc6eb-1e7c-4b7f-a8e0-e708f5ccf0f6';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'b7f0807f-f456-4706-9b66-9fa150135a73';
SELECT @@ROWCOUNT;

GO

DELETE FROM [TenantModules]
WHERE [Id] = 'b807024f-a4ac-4db8-95df-cc1c8bba6aaa';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-17T18:01:54.4860132Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-17T18:01:54.4860202Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-17T18:01:54.4860205Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2025-10-17T18:01:54.4860208Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T18:01:54.4860509Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T18:01:54.4860522Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T18:01:54.4860528Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T18:01:54.4860537Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T18:01:54.4860547Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T18:01:54.4860555Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T18:01:54.4860561Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T18:01:54.4860566Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T18:01:54.4860576Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T18:01:54.4860584Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T18:01:54.4860590Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T18:01:54.4860597Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T18:01:54.4860607Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T18:01:54.4860621Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T18:01:54.4860625Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2025-10-17T18:01:54.4860630Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860679Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860682Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860683Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860684Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860684Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860686Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860687Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860688Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860694Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860695Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860696Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860697Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860698Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860699Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860699Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860700Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860906Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860909Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860910Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860910Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860912Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860912Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860913Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860914Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860915Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860916Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860917Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860918Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860918Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860919Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860920Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860987Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860989Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860991Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860991Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860992Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860993Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860994Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860994Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860995Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4860996Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4861010Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4861012Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2025-10-17T18:01:54.4861012Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'CreatedById', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'LastModifiedById', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [CreatedById], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [LastModifiedById], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('069bffb4-6224-45ee-a12c-6141a5b14ea4', NULL, '2025-10-17T18:01:54.4860354Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T18:01:54.4860354Z', CAST(0 AS bit), NULL, N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('64cf8c3c-6623-45ef-b388-bcc603294ee2', NULL, '2025-10-17T18:01:54.4860419Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T18:01:54.4860419Z', CAST(0 AS bit), NULL, N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('8be9e988-8e32-4a29-9ae6-fb61e3f9af8f', NULL, '2025-10-17T18:01:54.4860335Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T18:01:54.4860335Z', CAST(0 AS bit), NULL, N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('cf97c6d0-8c99-477e-bf33-89595b0e3f51', NULL, '2025-10-17T18:01:54.4860373Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T18:01:54.4860373Z', CAST(0 AS bit), NULL, N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('e5aaa75d-6dc3-4e22-8e9b-a906d9f26376', NULL, '2025-10-17T18:01:54.4860313Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T18:01:54.4860312Z', CAST(0 AS bit), NULL, N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('f2118aff-7602-44c0-b34a-26eddffe1253', NULL, '2025-10-17T18:01:54.4860393Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T18:01:54.4860393Z', CAST(0 AS bit), NULL, N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('f283b132-c8d4-487f-aeed-7f9b976f38d1', NULL, '2025-10-17T18:01:54.4860287Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-10-17T18:01:54.4860283Z', CAST(0 AS bit), NULL, N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'CreatedById', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'LastModifiedById', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

UPDATE [Tenants] SET [CreatedAt] = '2025-10-17T18:01:54.4859830Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251017180156_RefactorAssetClassification', N'8.0.0');
GO

COMMIT;
GO

