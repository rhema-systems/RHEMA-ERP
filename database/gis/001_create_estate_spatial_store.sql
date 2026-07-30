/*
    TDC Estate GIS spatial store

    Run this script in SQL Server Management Studio as a database administrator.
    GeoServer should connect with a separate least-privilege SQL login that has
    SELECT, INSERT, UPDATE, and DELETE rights only on the estate schema.
*/

IF DB_ID(N'RhemaERP_GIS') IS NULL
BEGIN
    CREATE DATABASE [RhemaERP_GIS];
END;
GO

USE [RhemaERP_GIS];
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'estate')
BEGIN
    EXEC(N'CREATE SCHEMA [estate] AUTHORIZATION [dbo]');
END;
GO

IF OBJECT_ID(N'estate.LandParcels', N'U') IS NULL
BEGIN
    CREATE TABLE [estate].[LandParcels]
    (
        [Id] uniqueidentifier NOT NULL
            CONSTRAINT [DF_EstateLandParcels_Id] DEFAULT NEWSEQUENTIALID(),
        [TenantId] uniqueidentifier NOT NULL,
        [EstateManagedAssetId] uniqueidentifier NULL,
        [AssetCode] nvarchar(100) NOT NULL,
        [Name] nvarchar(240) NOT NULL,
        [Location] nvarchar(500) NULL,
        [Region] nvarchar(120) NULL,
        [District] nvarchar(160) NULL,
        [SourceSrid] int NOT NULL,
        [Boundary] geometry NOT NULL,
        [AreaSquareFeet] AS ([Boundary].[STArea]()),
        [Status] nvarchar(40) NOT NULL
            CONSTRAINT [DF_EstateLandParcels_Status] DEFAULT N'Active',
        [CreatedAtUtc] datetime2(7) NOT NULL
            CONSTRAINT [DF_EstateLandParcels_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
        [UpdatedAtUtc] datetime2(7) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_EstateLandParcels] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [CK_EstateLandParcels_ValidBoundary]
            CHECK ([Boundary].[STIsValid]() = 1),
        CONSTRAINT [CK_EstateLandParcels_SourceSrid]
            CHECK ([Boundary].[STSrid] = [SourceSrid])
    );

    CREATE UNIQUE INDEX [UX_EstateLandParcels_Tenant_AssetCode]
        ON [estate].[LandParcels] ([TenantId], [AssetCode]);

    CREATE UNIQUE INDEX [UX_EstateLandParcels_Tenant_ManagedAsset]
        ON [estate].[LandParcels] ([TenantId], [EstateManagedAssetId])
        WHERE [EstateManagedAssetId] IS NOT NULL;

    CREATE INDEX [IX_EstateLandParcels_Tenant_Status]
        ON [estate].[LandParcels] ([TenantId], [Status]);
END;
GO

IF OBJECT_ID(N'estate.GisSyncLog', N'U') IS NULL
BEGIN
    CREATE TABLE [estate].[GisSyncLog]
    (
        [Id] bigint IDENTITY(1, 1) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [EstateManagedAssetId] uniqueidentifier NULL,
        [Provider] nvarchar(40) NOT NULL,
        [LayerReference] nvarchar(240) NULL,
        [FeatureId] nvarchar(240) NULL,
        [Operation] nvarchar(40) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [Message] nvarchar(1000) NULL,
        [OccurredAtUtc] datetime2(7) NOT NULL
            CONSTRAINT [DF_EstateGisSyncLog_OccurredAtUtc] DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_EstateGisSyncLog] PRIMARY KEY CLUSTERED ([Id])
    );

    CREATE INDEX [IX_EstateGisSyncLog_Tenant_Asset_Time]
        ON [estate].[GisSyncLog] ([TenantId], [EstateManagedAssetId], [OccurredAtUtc] DESC);
END;
GO

/*
    Add a spatial index after the survey CRS and its actual coordinate extent
    have been confirmed. For geometry, the bounding box must cover all TDC
    parcel coordinates. Replace the values below before running the statement.

    CREATE SPATIAL INDEX [SIX_EstateLandParcels_Boundary]
        ON [estate].[LandParcels] ([Boundary])
        USING GEOMETRY_AUTO_GRID
        WITH (BOUNDING_BOX = (xmin, ymin, xmax, ymax));
*/
