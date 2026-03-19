BEGIN TRANSACTION;
GO

ALTER TABLE [InspectionTemplates] ADD [AllowPhotos] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [InspectionTemplates] ADD [AssetTypes] nvarchar(max) NOT NULL DEFAULT N'';
GO

ALTER TABLE [InspectionTemplates] ADD [Code] nvarchar(20) NOT NULL DEFAULT N'';
GO

ALTER TABLE [InspectionTemplates] ADD [EstimatedDuration] int NOT NULL DEFAULT 0;
GO

ALTER TABLE [InspectionTemplates] ADD [Frequency] nvarchar(50) NOT NULL DEFAULT N'';
GO

ALTER TABLE [InspectionTemplates] ADD [InspectorRoles] nvarchar(max) NOT NULL DEFAULT N'';
GO

ALTER TABLE [InspectionTemplates] ADD [Priority] nvarchar(20) NOT NULL DEFAULT N'';
GO

ALTER TABLE [InspectionTemplates] ADD [RequiresSignature] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [InspectionTemplates] ADD [Version] nvarchar(10) NOT NULL DEFAULT N'';
GO

CREATE TABLE [DistributedLocks] (
    [Id] uniqueidentifier NOT NULL,
    [LockName] nvarchar(200) NOT NULL,
    [AcquiredBy] nvarchar(200) NULL,
    [AcquiredAtUtc] datetime2 NULL,
    [LeaseUntilUtc] datetime2 NOT NULL,
    [LastHeartbeatUtc] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_DistributedLocks] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [FleetBatteries] (
    [Id] uniqueidentifier NOT NULL,
    [VehicleAssetId] uniqueidentifier NOT NULL,
    [SerialNumber] nvarchar(100) NOT NULL,
    [Brand] nvarchar(100) NULL,
    [Spec] nvarchar(50) NULL,
    [Position] nvarchar(30) NULL,
    [InstalledAtUtc] datetime2 NOT NULL,
    [RemovedAtUtc] datetime2 NULL,
    [Status] nvarchar(20) NOT NULL,
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
    CONSTRAINT [PK_FleetBatteries] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FleetBatteries_MaintenanceAssets_VehicleAssetId] FOREIGN KEY ([VehicleAssetId]) REFERENCES [MaintenanceAssets] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_FleetBatteries_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [FleetExternalRepairs] (
    [Id] uniqueidentifier NOT NULL,
    [VehicleAssetId] uniqueidentifier NOT NULL,
    [VendorBusinessPartnerId] uniqueidentifier NULL,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(4000) NULL,
    [Status] nvarchar(20) NOT NULL,
    [EstimatedCost] decimal(18,2) NULL,
    [CurrencyCode] nvarchar(10) NULL,
    [RequestedAtUtc] datetime2 NOT NULL,
    [ApprovedAtUtc] datetime2 NULL,
    [CompletedAtUtc] datetime2 NULL,
    [WorkOrderId] uniqueidentifier NULL,
    [AdditionalData] nvarchar(max) NULL,
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
    CONSTRAINT [PK_FleetExternalRepairs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FleetExternalRepairs_BusinessPartners_VendorBusinessPartnerId] FOREIGN KEY ([VendorBusinessPartnerId]) REFERENCES [BusinessPartners] ([Id]),
    CONSTRAINT [FK_FleetExternalRepairs_MaintenanceAssets_VehicleAssetId] FOREIGN KEY ([VehicleAssetId]) REFERENCES [MaintenanceAssets] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_FleetExternalRepairs_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_FleetExternalRepairs_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id])
);
GO

CREATE TABLE [FleetTripInspections] (
    [Id] uniqueidentifier NOT NULL,
    [FleetTripId] uniqueidentifier NOT NULL,
    [InspectionTemplateId] uniqueidentifier NOT NULL,
    [InspectorEmployeeId] uniqueidentifier NULL,
    [InspectionKind] nvarchar(20) NOT NULL,
    [StartedAtUtc] datetime2 NOT NULL,
    [CompletedAtUtc] datetime2 NULL,
    [Status] nvarchar(20) NOT NULL,
    [OverallResult] nvarchar(20) NULL,
    [InspectionData] nvarchar(max) NOT NULL,
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
    CONSTRAINT [PK_FleetTripInspections] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FleetTripInspections_Employees_InspectorEmployeeId] FOREIGN KEY ([InspectorEmployeeId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_FleetTripInspections_FleetTrips_FleetTripId] FOREIGN KEY ([FleetTripId]) REFERENCES [FleetTrips] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_FleetTripInspections_InspectionTemplates_InspectionTemplateId] FOREIGN KEY ([InspectionTemplateId]) REFERENCES [InspectionTemplates] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_FleetTripInspections_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [FleetTyres] (
    [Id] uniqueidentifier NOT NULL,
    [VehicleAssetId] uniqueidentifier NOT NULL,
    [SerialNumber] nvarchar(100) NOT NULL,
    [Brand] nvarchar(100) NULL,
    [Size] nvarchar(50) NULL,
    [Position] nvarchar(30) NULL,
    [TreadDepthMm] decimal(18,4) NULL,
    [InstalledAtUtc] datetime2 NOT NULL,
    [RemovedAtUtc] datetime2 NULL,
    [Status] nvarchar(20) NOT NULL,
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
    CONSTRAINT [PK_FleetTyres] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FleetTyres_MaintenanceAssets_VehicleAssetId] FOREIGN KEY ([VehicleAssetId]) REFERENCES [MaintenanceAssets] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_FleetTyres_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [FleetVehicleAssignments] (
    [Id] uniqueidentifier NOT NULL,
    [VehicleAssetId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [AssignedFromUtc] datetime2 NOT NULL,
    [AssignedToUtc] datetime2 NULL,
    [AssignmentType] nvarchar(20) NOT NULL,
    [IsActive] bit NOT NULL,
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
    CONSTRAINT [PK_FleetVehicleAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FleetVehicleAssignments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_FleetVehicleAssignments_MaintenanceAssets_VehicleAssetId] FOREIGN KEY ([VehicleAssetId]) REFERENCES [MaintenanceAssets] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_FleetVehicleAssignments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [FleetCostEntries] (
    [Id] uniqueidentifier NOT NULL,
    [VehicleAssetId] uniqueidentifier NOT NULL,
    [FleetTripId] uniqueidentifier NULL,
    [WorkOrderId] uniqueidentifier NULL,
    [FleetExternalRepairId] uniqueidentifier NULL,
    [FleetFuelTransactionId] uniqueidentifier NULL,
    [CostDateUtc] datetime2 NOT NULL,
    [CostType] nvarchar(50) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [CurrencyCode] nvarchar(10) NULL,
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
    CONSTRAINT [PK_FleetCostEntries] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FleetCostEntries_FleetExternalRepairs_FleetExternalRepairId] FOREIGN KEY ([FleetExternalRepairId]) REFERENCES [FleetExternalRepairs] ([Id]),
    CONSTRAINT [FK_FleetCostEntries_FleetFuelTransactions_FleetFuelTransactionId] FOREIGN KEY ([FleetFuelTransactionId]) REFERENCES [FleetFuelTransactions] ([Id]),
    CONSTRAINT [FK_FleetCostEntries_FleetTrips_FleetTripId] FOREIGN KEY ([FleetTripId]) REFERENCES [FleetTrips] ([Id]),
    CONSTRAINT [FK_FleetCostEntries_MaintenanceAssets_VehicleAssetId] FOREIGN KEY ([VehicleAssetId]) REFERENCES [MaintenanceAssets] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_FleetCostEntries_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_FleetCostEntries_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id])
);
GO

CREATE TABLE [FleetDefects] (
    [Id] uniqueidentifier NOT NULL,
    [VehicleAssetId] uniqueidentifier NOT NULL,
    [FleetTripId] uniqueidentifier NULL,
    [FleetTripInspectionId] uniqueidentifier NULL,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(4000) NULL,
    [Severity] nvarchar(20) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [ReportedAtUtc] datetime2 NOT NULL,
    [ReportedByEmployeeId] uniqueidentifier NULL,
    [WorkOrderId] uniqueidentifier NULL,
    [AdditionalData] nvarchar(max) NULL,
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
    CONSTRAINT [PK_FleetDefects] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FleetDefects_Employees_ReportedByEmployeeId] FOREIGN KEY ([ReportedByEmployeeId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_FleetDefects_FleetTripInspections_FleetTripInspectionId] FOREIGN KEY ([FleetTripInspectionId]) REFERENCES [FleetTripInspections] ([Id]),
    CONSTRAINT [FK_FleetDefects_FleetTrips_FleetTripId] FOREIGN KEY ([FleetTripId]) REFERENCES [FleetTrips] ([Id]),
    CONSTRAINT [FK_FleetDefects_MaintenanceAssets_VehicleAssetId] FOREIGN KEY ([VehicleAssetId]) REFERENCES [MaintenanceAssets] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_FleetDefects_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_FleetDefects_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id])
);
GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2026-02-09T00:40:45.3474076Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2026-02-09T00:40:45.3474151Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2026-02-09T00:40:45.3474154Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [AspNetRoles] SET [CreatedAt] = '2026-02-09T00:40:45.3474156Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2026-02-09T00:40:45.3474441Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2026-02-09T00:40:45.3474464Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2026-02-09T00:40:45.3474472Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2026-02-09T00:40:45.3474479Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2026-02-09T00:40:45.3474496Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000005';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2026-02-09T00:40:45.3474507Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000006';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2026-02-09T00:40:45.3474514Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000007';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2026-02-09T00:40:45.3474522Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000008';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2026-02-09T00:40:45.3474533Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000009';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2026-02-09T00:40:45.3474545Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000010';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2026-02-09T00:40:45.3474553Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000011';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2026-02-09T00:40:45.3474561Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000012';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2026-02-09T00:40:45.3474573Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000013';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2026-02-09T00:40:45.3474593Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000014';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2026-02-09T00:40:45.3474612Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000015';
SELECT @@ROWCOUNT;

GO

UPDATE [Permissions] SET [CreatedAt] = '2026-02-09T00:40:45.3474619Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000016';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474689Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474691Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474699Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474701Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474702Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474703Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474704Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474705Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474706Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474708Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474708Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474709Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474710Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474711Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474712Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474713Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474781Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474784Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000002' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474785Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474786Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000004' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474787Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474788Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000006' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474789Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000007' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474789Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000008' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474790Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474791Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474792Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474793Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474794Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474795Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000015' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474796Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000002';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474868Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474869Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000003' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474871Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000005' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474872Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474873Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474874Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000011' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474875Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000012' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474876Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000013' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474876Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000014' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474877Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000016' AND [RoleId] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474891Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000001' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474892Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000009' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [RolePermissions] SET [GrantedAt] = '2026-02-09T00:40:45.3474893Z'
WHERE [PermissionId] = '00000000-0000-0000-0000-000000000010' AND [RoleId] = '00000000-0000-0000-0000-000000000004';
SELECT @@ROWCOUNT;

GO

UPDATE [Tenants] SET [CreatedAt] = '2026-02-09T00:40:45.3473741Z'
WHERE [Id] = '00000000-0000-0000-0000-000000000001';
SELECT @@ROWCOUNT;

GO

CREATE UNIQUE INDEX [IX_DistributedLocks_LockName] ON [DistributedLocks] ([LockName]);
GO

CREATE INDEX [IX_FleetBatteries_TenantId_SerialNumber_IsDeleted] ON [FleetBatteries] ([TenantId], [SerialNumber], [IsDeleted]);
GO

CREATE INDEX [IX_FleetBatteries_TenantId_VehicleAssetId_Status_IsDeleted] ON [FleetBatteries] ([TenantId], [VehicleAssetId], [Status], [IsDeleted]);
GO

CREATE INDEX [IX_FleetBatteries_VehicleAssetId] ON [FleetBatteries] ([VehicleAssetId]);
GO

CREATE INDEX [IX_FleetCostEntries_FleetExternalRepairId] ON [FleetCostEntries] ([FleetExternalRepairId]);
GO

CREATE INDEX [IX_FleetCostEntries_FleetFuelTransactionId] ON [FleetCostEntries] ([FleetFuelTransactionId]);
GO

CREATE INDEX [IX_FleetCostEntries_FleetTripId] ON [FleetCostEntries] ([FleetTripId]);
GO

CREATE INDEX [IX_FleetCostEntries_TenantId_CostType_IsDeleted] ON [FleetCostEntries] ([TenantId], [CostType], [IsDeleted]);
GO

CREATE INDEX [IX_FleetCostEntries_TenantId_VehicleAssetId_CostDateUtc_IsDeleted] ON [FleetCostEntries] ([TenantId], [VehicleAssetId], [CostDateUtc], [IsDeleted]);
GO

CREATE INDEX [IX_FleetCostEntries_VehicleAssetId] ON [FleetCostEntries] ([VehicleAssetId]);
GO

CREATE INDEX [IX_FleetCostEntries_WorkOrderId] ON [FleetCostEntries] ([WorkOrderId]);
GO

CREATE INDEX [IX_FleetDefects_FleetTripId] ON [FleetDefects] ([FleetTripId]);
GO

CREATE INDEX [IX_FleetDefects_FleetTripInspectionId] ON [FleetDefects] ([FleetTripInspectionId]);
GO

CREATE INDEX [IX_FleetDefects_ReportedByEmployeeId] ON [FleetDefects] ([ReportedByEmployeeId]);
GO

CREATE INDEX [IX_FleetDefects_TenantId_FleetTripId_IsDeleted] ON [FleetDefects] ([TenantId], [FleetTripId], [IsDeleted]);
GO

CREATE INDEX [IX_FleetDefects_TenantId_VehicleAssetId_Status_IsDeleted] ON [FleetDefects] ([TenantId], [VehicleAssetId], [Status], [IsDeleted]);
GO

CREATE INDEX [IX_FleetDefects_VehicleAssetId] ON [FleetDefects] ([VehicleAssetId]);
GO

CREATE INDEX [IX_FleetDefects_WorkOrderId] ON [FleetDefects] ([WorkOrderId]);
GO

CREATE INDEX [IX_FleetExternalRepairs_TenantId_VehicleAssetId_Status_IsDeleted] ON [FleetExternalRepairs] ([TenantId], [VehicleAssetId], [Status], [IsDeleted]);
GO

CREATE INDEX [IX_FleetExternalRepairs_TenantId_VendorBusinessPartnerId_IsDeleted] ON [FleetExternalRepairs] ([TenantId], [VendorBusinessPartnerId], [IsDeleted]);
GO

CREATE INDEX [IX_FleetExternalRepairs_VehicleAssetId] ON [FleetExternalRepairs] ([VehicleAssetId]);
GO

CREATE INDEX [IX_FleetExternalRepairs_VendorBusinessPartnerId] ON [FleetExternalRepairs] ([VendorBusinessPartnerId]);
GO

CREATE INDEX [IX_FleetExternalRepairs_WorkOrderId] ON [FleetExternalRepairs] ([WorkOrderId]);
GO

CREATE INDEX [IX_FleetTripInspections_FleetTripId] ON [FleetTripInspections] ([FleetTripId]);
GO

CREATE INDEX [IX_FleetTripInspections_InspectionTemplateId] ON [FleetTripInspections] ([InspectionTemplateId]);
GO

CREATE INDEX [IX_FleetTripInspections_InspectorEmployeeId] ON [FleetTripInspections] ([InspectorEmployeeId]);
GO

CREATE INDEX [IX_FleetTripInspections_TenantId_FleetTripId_InspectionKind_IsDeleted] ON [FleetTripInspections] ([TenantId], [FleetTripId], [InspectionKind], [IsDeleted]);
GO

CREATE INDEX [IX_FleetTripInspections_TenantId_InspectionTemplateId_IsDeleted] ON [FleetTripInspections] ([TenantId], [InspectionTemplateId], [IsDeleted]);
GO

CREATE INDEX [IX_FleetTyres_TenantId_SerialNumber_IsDeleted] ON [FleetTyres] ([TenantId], [SerialNumber], [IsDeleted]);
GO

CREATE INDEX [IX_FleetTyres_TenantId_VehicleAssetId_Status_IsDeleted] ON [FleetTyres] ([TenantId], [VehicleAssetId], [Status], [IsDeleted]);
GO

CREATE INDEX [IX_FleetTyres_VehicleAssetId] ON [FleetTyres] ([VehicleAssetId]);
GO

CREATE INDEX [IX_FleetVehicleAssignments_EmployeeId] ON [FleetVehicleAssignments] ([EmployeeId]);
GO

CREATE INDEX [IX_FleetVehicleAssignments_TenantId_EmployeeId_IsActive] ON [FleetVehicleAssignments] ([TenantId], [EmployeeId], [IsActive]);
GO

CREATE INDEX [IX_FleetVehicleAssignments_TenantId_VehicleAssetId_IsActive] ON [FleetVehicleAssignments] ([TenantId], [VehicleAssetId], [IsActive]);
GO

CREATE INDEX [IX_FleetVehicleAssignments_VehicleAssetId] ON [FleetVehicleAssignments] ([VehicleAssetId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260209004050_202602090001_AddFleetPhase2AndDistributedLocksAndInspectionTemplateFields', N'8.0.0');
GO

COMMIT;
GO

