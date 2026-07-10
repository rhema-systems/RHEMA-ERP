/*
    Seed active HR Location master records for maintenance/fleet testing.

    Usage in SQL Server Management Studio:
      1. Connect to the target SQL Server.
      2. Select the RhemaERP database.
      3. Run this script.

    The script is idempotent. It reuses the default tenant, creates a simple
    Maintenance Site Structure and Site level if needed, then inserts sample
    sites only when their codes do not already exist.
*/

USE [RhemaERP];
GO

SET NOCOUNT ON;

DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000001';
DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @StructureId uniqueidentifier;
DECLARE @LevelId uniqueidentifier;

IF EXISTS (SELECT 1 FROM dbo.Tenants WHERE Id = @TenantId)
BEGIN
    SELECT @StructureId = Id
    FROM dbo.LocationStructures
    WHERE TenantId = @TenantId
      AND Code = 'MAINT-SITES'
      AND IsDeleted = 0;

    IF @StructureId IS NULL
    BEGIN
        SET @StructureId = NEWID();

        INSERT INTO dbo.LocationStructures
        (
            Id, TenantId, Name, Code, IsDefault, Description, IsActive,
            CreatedAt, CreatedBy, IsDeleted
        )
        VALUES
        (
            @StructureId, @TenantId, 'Maintenance Site Structure', 'MAINT-SITES', 0,
            'Operational locations used for maintenance assets and technician assignments.',
            1, @Now, 'seed-maintenance-locations.sql', 0
        );
    END;

    SELECT @LevelId = Id
    FROM dbo.LocationLevels
    WHERE TenantId = @TenantId
      AND StructureId = @StructureId
      AND Code = 'SITE'
      AND IsDeleted = 0;

    IF @LevelId IS NULL
    BEGIN
        SET @LevelId = NEWID();

        INSERT INTO dbo.LocationLevels
        (
            Id, TenantId, Name, Code, Description, LevelNumber,
            RequiresAddress, RequiresContactInfo, AllowsEmployeeAssignment,
            IsLocked, IsActive, StructureId, CreatedAt, CreatedBy, IsDeleted
        )
        VALUES
        (
            @LevelId, @TenantId, 'Site', 'SITE',
            'Maintenance and fleet operational site.',
            1, 1, 0, 1, 0, 1, @StructureId, @Now,
            'seed-maintenance-locations.sql', 0
        );
    END;

    DECLARE @Sites TABLE
    (
        Code nvarchar(50) NOT NULL,
        Name nvarchar(200) NOT NULL,
        Description nvarchar(1000) NULL,
        AddressLine1 nvarchar(500) NULL,
        City nvarchar(100) NULL,
        Sequence int NOT NULL
    );

    INSERT INTO @Sites (Code, Name, Description, AddressLine1, City, Sequence)
    VALUES
        ('MAIN-YARD', 'Main Maintenance Yard', 'Central maintenance workshop and asset yard.', 'Main yard', 'Accra', 1),
        ('NORTH-SITE', 'North Project Site', 'Northern operational site for fleet and field assets.', 'North site', 'Tamale', 2),
        ('SOUTH-SITE', 'South Project Site', 'Southern operational site for field maintenance.', 'South site', 'Takoradi', 3),
        ('FIELD-OPS', 'Field Operations Base', 'Mobile field operations and inspection base.', 'Field base', 'Kumasi', 4);

    INSERT INTO dbo.Locations
    (
        Id, TenantId, Name, Code, Description, StructureId, LocationLevelId,
        ParentLocationId, AddressLine1, City, Sequence, Path, IsActive,
        CreatedAt, CreatedBy, IsDeleted
    )
    SELECT
        NEWID(), @TenantId, s.Name, s.Code, s.Description, @StructureId, @LevelId,
        NULL, s.AddressLine1, s.City, s.Sequence, '/' + s.Code, 1,
        @Now, 'seed-maintenance-locations.sql', 0
    FROM @Sites s
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.Locations existing
        WHERE existing.TenantId = @TenantId
          AND existing.StructureId = @StructureId
          AND existing.Code = s.Code
          AND existing.IsDeleted = 0
    );

    SELECT
        Code,
        Name,
        IsActive
    FROM dbo.Locations
    WHERE TenantId = @TenantId
      AND StructureId = @StructureId
      AND Code IN ('MAIN-YARD', 'NORTH-SITE', 'SOUTH-SITE', 'FIELD-OPS')
      AND IsDeleted = 0
    ORDER BY Sequence;
END
ELSE
BEGIN
    THROW 51000, 'Default tenant was not found. Update @TenantId before running this script.', 1;
END;
GO
