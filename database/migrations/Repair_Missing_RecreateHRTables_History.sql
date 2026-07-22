-- Repairs an out-of-order EF migration-history gap for local/dev databases.
--
-- Cause:
--   20260304155434_RecreateHRTables was introduced with an older timestamp than
--   migrations already applied to this database. The database schema already has
--   that migration's end-state, but __EFMigrationsHistory is missing the row.
--   Without this repair, `dotnet ef database update` tries to rerun the old
--   rename/drop migration and fails on missing legacy objects such as
--   dbo.PositionSkillRequirement.
--
-- This script only records the migration as applied when key schema evidence
-- confirms the migration's end-state already exists.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @MigrationId nvarchar(150) = N'20260304155434_RecreateHRTables';
DECLARE @ProductVersion nvarchar(32) = N'8.0.0';

BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = @MigrationId)
BEGIN
    PRINT 'Migration history already contains 20260304155434_RecreateHRTables. No repair needed.';
    COMMIT TRANSACTION;
    RETURN;
END;

IF OBJECT_ID(N'dbo.PositionSkillRequirements', N'U') IS NULL
BEGIN
    THROW 51001, 'Cannot repair migration history: dbo.PositionSkillRequirements does not exist.', 1;
END;

IF OBJECT_ID(N'dbo.PositionSkillRequirement', N'U') IS NOT NULL
BEGIN
    THROW 51002, 'Cannot repair migration history: legacy dbo.PositionSkillRequirement still exists.', 1;
END;

IF COL_LENGTH(N'dbo.Employees', N'PayTax') IS NULL
    OR COL_LENGTH(N'dbo.Employees', N'SSFund') IS NULL
    OR COL_LENGTH(N'dbo.Employees', N'GrossUp') IS NULL
    OR COL_LENGTH(N'dbo.Employees', N'Tier2Only') IS NULL
    OR COL_LENGTH(N'dbo.Employees', N'Overtime') IS NULL
    OR COL_LENGTH(N'dbo.Employees', N'SocialSecurityNumber') IS NULL
    OR COL_LENGTH(N'dbo.Employees', N'TINNumber') IS NULL
    OR COL_LENGTH(N'dbo.EmployeePositions', N'ExpectedHeadcount') IS NULL
    OR COL_LENGTH(N'dbo.EmployeePositions', N'MinimumExperienceYears') IS NULL
    OR COL_LENGTH(N'dbo.EmployeeIdentificationCards', N'IdentificationTypeId') IS NULL
    OR COL_LENGTH(N'dbo.EmployeeEmergencyContacts', N'ContactType') IS NULL
BEGIN
    THROW 51003, 'Cannot repair migration history: one or more RecreateHRTables columns are missing.', 1;
END;

IF COL_LENGTH(N'dbo.EmployeeQualifications', N'QualificationName') IS NOT NULL
BEGIN
    THROW 51004, 'Cannot repair migration history: EmployeeQualifications.QualificationName still exists.', 1;
END;

INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
VALUES (@MigrationId, @ProductVersion);

COMMIT TRANSACTION;

PRINT 'Inserted missing migration-history row for 20260304155434_RecreateHRTables.';
