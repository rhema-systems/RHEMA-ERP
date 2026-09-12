using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Lane 3b — reference data that should be a dimension, and staff numbering that should be
    /// configuration.
    /// </summary>
    /// <remarks>
    /// <para><b>Three tables.</b> <c>QualificationLevels</c> is the ordered ladder
    /// <c>QualificationType</c> could never be — a category cannot answer "is a Master's above a
    /// Diploma". <c>CertifyingBodies</c> replaces a free-text certifier, where "ICAG" and
    /// "I.C.A.G." were two different bodies to every report. <c>StaffNumberFormats</c> holds one
    /// numbering rule per employee register per tenant, because permanent and contract staff are
    /// numbered differently and every client differs again.</para>
    ///
    /// <para><b>Every operation is guarded.</b> Written as idempotent SQL rather than the scaffolded
    /// builder calls, following the house convention: this migration must be safe to apply to a
    /// database that already has some of it, which is the situation on any environment that has been
    /// rebuilt from the EF model rather than migrated.</para>
    ///
    /// <para>⚠ <b>Both unique indexes are FILTERED on <c>IsDeleted = 0</c></b>, and the staff-number
    /// one on <c>IsActive = 1</c> as well. An unfiltered unique index keeps a retired row's name
    /// reserved for ever, because the delete is a soft delete — the shape this module has met nine
    /// times. Retiring a numbering rule must free its register for the replacement.</para>
    /// </remarks>
    public partial class HrReferenceDataDimensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── new columns on existing tables ──────────────────────────────
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[dbo].[Qualifications]', N'QualificationLevelId') IS NULL
    ALTER TABLE [dbo].[Qualifications] ADD [QualificationLevelId] uniqueidentifier NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[dbo].[IdentificationTypes]', N'ExpiryNotificationLeadDays') IS NULL
    ALTER TABLE [dbo].[IdentificationTypes] ADD [ExpiryNotificationLeadDays] int NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[dbo].[EmployeeSkills]', N'CertifyingBodyId') IS NULL
    ALTER TABLE [dbo].[EmployeeSkills] ADD [CertifyingBodyId] uniqueidentifier NULL;
");

            // ── CertifyingBodies ────────────────────────────────────────────
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[CertifyingBodies]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CertifyingBodies](
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Abbreviation] nvarchar(50) NULL,
        [Description] nvarchar(500) NULL,
        [CountryId] uniqueidentifier NULL,
        [Website] nvarchar(255) NULL,
        [IsActive] bit NOT NULL,
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
        CONSTRAINT [PK_CertifyingBodies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CertifyingBodies_Countries_CountryId] FOREIGN KEY ([CountryId])
            REFERENCES [dbo].[Countries]([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CertifyingBodies_Tenants_TenantId] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CertifyingBodies_CountryId' AND object_id = OBJECT_ID(N'[dbo].[CertifyingBodies]'))
    CREATE INDEX [IX_CertifyingBodies_CountryId] ON [dbo].[CertifyingBodies]([CountryId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CertifyingBodies_IsActive' AND object_id = OBJECT_ID(N'[dbo].[CertifyingBodies]'))
    CREATE INDEX [IX_CertifyingBodies_IsActive] ON [dbo].[CertifyingBodies]([IsActive]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CertifyingBodies_Name' AND object_id = OBJECT_ID(N'[dbo].[CertifyingBodies]'))
    CREATE INDEX [IX_CertifyingBodies_Name] ON [dbo].[CertifyingBodies]([Name]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CertifyingBodies_TenantId_Name' AND object_id = OBJECT_ID(N'[dbo].[CertifyingBodies]'))
    CREATE UNIQUE INDEX [IX_CertifyingBodies_TenantId_Name] ON [dbo].[CertifyingBodies]([TenantId], [Name]) WHERE [IsDeleted] = 0;
");

            // ── QualificationLevels ─────────────────────────────────────────
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[QualificationLevels]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[QualificationLevels](
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Code] nvarchar(20) NULL,
        [Description] nvarchar(500) NULL,
        [Rank] int NOT NULL,
        [IsActive] bit NOT NULL,
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
        CONSTRAINT [PK_QualificationLevels] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_QualificationLevels_Tenants_TenantId] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QualificationLevels_IsActive' AND object_id = OBJECT_ID(N'[dbo].[QualificationLevels]'))
    CREATE INDEX [IX_QualificationLevels_IsActive] ON [dbo].[QualificationLevels]([IsActive]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QualificationLevels_Name' AND object_id = OBJECT_ID(N'[dbo].[QualificationLevels]'))
    CREATE INDEX [IX_QualificationLevels_Name] ON [dbo].[QualificationLevels]([Name]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QualificationLevels_TenantId_Rank' AND object_id = OBJECT_ID(N'[dbo].[QualificationLevels]'))
    CREATE INDEX [IX_QualificationLevels_TenantId_Rank] ON [dbo].[QualificationLevels]([TenantId], [Rank]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QualificationLevels_TenantId_Name' AND object_id = OBJECT_ID(N'[dbo].[QualificationLevels]'))
    CREATE UNIQUE INDEX [IX_QualificationLevels_TenantId_Name] ON [dbo].[QualificationLevels]([TenantId], [Name]) WHERE [IsDeleted] = 0;
");

            // ── StaffNumberFormats ──────────────────────────────────────────
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[StaffNumberFormats]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[StaffNumberFormats](
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [AppliesToEmploymentType] int NULL,
        [Prefix] nvarchar(10) NOT NULL,
        [Separator] nvarchar(3) NOT NULL,
        [IncludeYear] bit NOT NULL,
        [YearDigits] int NOT NULL,
        [SequenceDigits] int NOT NULL,
        [Suffix] nvarchar(10) NOT NULL,
        [AutoGenerate] bit NOT NULL,
        [SequenceKey] nvarchar(30) NOT NULL,
        [IsActive] bit NOT NULL,
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
        CONSTRAINT [PK_StaffNumberFormats] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StaffNumberFormats_Tenants_TenantId] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StaffNumberFormats_IsActive' AND object_id = OBJECT_ID(N'[dbo].[StaffNumberFormats]'))
    CREATE INDEX [IX_StaffNumberFormats_IsActive] ON [dbo].[StaffNumberFormats]([IsActive]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StaffNumberFormats_TenantId_SequenceKey' AND object_id = OBJECT_ID(N'[dbo].[StaffNumberFormats]'))
    CREATE INDEX [IX_StaffNumberFormats_TenantId_SequenceKey] ON [dbo].[StaffNumberFormats]([TenantId], [SequenceKey]);

-- ⚠ Filtered on IsActive as well as IsDeleted. A RETIRED rule must release its register at once,
-- so a tenant can replace one numbering series with another; without the IsActive leg the old rule
-- would hold the register until it was hard-deleted, and a soft delete would never free it.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StaffNumberFormats_TenantId_AppliesToEmploymentType' AND object_id = OBJECT_ID(N'[dbo].[StaffNumberFormats]'))
    CREATE UNIQUE INDEX [IX_StaffNumberFormats_TenantId_AppliesToEmploymentType] ON [dbo].[StaffNumberFormats]([TenantId], [AppliesToEmploymentType]) WHERE [IsDeleted] = 0 AND [IsActive] = 1;
");

            // ── indexes and FKs on the existing tables ──────────────────────
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Qualifications_QualificationLevelId' AND object_id = OBJECT_ID(N'[dbo].[Qualifications]'))
    CREATE INDEX [IX_Qualifications_QualificationLevelId] ON [dbo].[Qualifications]([QualificationLevelId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EmployeeSkills_CertifyingBodyId' AND object_id = OBJECT_ID(N'[dbo].[EmployeeSkills]'))
    CREATE INDEX [IX_EmployeeSkills_CertifyingBodyId] ON [dbo].[EmployeeSkills]([CertifyingBodyId]);

-- Restrict, not cascade: retiring a level must not delete every qualification sitting on it, and
-- retiring a certifying body must not delete the skills it certified. The lookup is a label on the
-- row, not its owner.
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Qualifications_QualificationLevels_QualificationLevelId')
    ALTER TABLE [dbo].[Qualifications] ADD CONSTRAINT [FK_Qualifications_QualificationLevels_QualificationLevelId]
        FOREIGN KEY ([QualificationLevelId]) REFERENCES [dbo].[QualificationLevels]([Id]) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_EmployeeSkills_CertifyingBodies_CertifyingBodyId')
    ALTER TABLE [dbo].[EmployeeSkills] ADD CONSTRAINT [FK_EmployeeSkills_CertifyingBodies_CertifyingBodyId]
        FOREIGN KEY ([CertifyingBodyId]) REFERENCES [dbo].[CertifyingBodies]([Id]) ON DELETE NO ACTION;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_EmployeeSkills_CertifyingBodies_CertifyingBodyId')
    ALTER TABLE [dbo].[EmployeeSkills] DROP CONSTRAINT [FK_EmployeeSkills_CertifyingBodies_CertifyingBodyId];

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Qualifications_QualificationLevels_QualificationLevelId')
    ALTER TABLE [dbo].[Qualifications] DROP CONSTRAINT [FK_Qualifications_QualificationLevels_QualificationLevelId];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EmployeeSkills_CertifyingBodyId' AND object_id = OBJECT_ID(N'[dbo].[EmployeeSkills]'))
    DROP INDEX [IX_EmployeeSkills_CertifyingBodyId] ON [dbo].[EmployeeSkills];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Qualifications_QualificationLevelId' AND object_id = OBJECT_ID(N'[dbo].[Qualifications]'))
    DROP INDEX [IX_Qualifications_QualificationLevelId] ON [dbo].[Qualifications];

IF OBJECT_ID(N'[dbo].[StaffNumberFormats]', N'U') IS NOT NULL DROP TABLE [dbo].[StaffNumberFormats];
IF OBJECT_ID(N'[dbo].[QualificationLevels]', N'U') IS NOT NULL DROP TABLE [dbo].[QualificationLevels];
IF OBJECT_ID(N'[dbo].[CertifyingBodies]', N'U') IS NOT NULL DROP TABLE [dbo].[CertifyingBodies];

IF COL_LENGTH(N'[dbo].[EmployeeSkills]', N'CertifyingBodyId') IS NOT NULL
    ALTER TABLE [dbo].[EmployeeSkills] DROP COLUMN [CertifyingBodyId];

IF COL_LENGTH(N'[dbo].[IdentificationTypes]', N'ExpiryNotificationLeadDays') IS NOT NULL
    ALTER TABLE [dbo].[IdentificationTypes] DROP COLUMN [ExpiryNotificationLeadDays];

IF COL_LENGTH(N'[dbo].[Qualifications]', N'QualificationLevelId') IS NOT NULL
    ALTER TABLE [dbo].[Qualifications] DROP COLUMN [QualificationLevelId];
");
        }
    }
}
