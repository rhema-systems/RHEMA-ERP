using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// HR area 15b slice 8a — the confirming-authority map (FR-HR-032, decision D-2), plus the
    /// RoutedToEmployeeId column that lets a dispatched confirmation form record who it went to.
    ///
    /// ⚠ The unique (TenantId, OrganizationUnitId, StaffLevelId) index is FILTERED ON
    /// [IsDeleted] = 0, and the filter is load-bearing rather than tidy: a soft delete does not
    /// release a unique index, so without it one deleted rule would hold a (unit, level) slot that
    /// no live rule could ever occupy — the defect that showed five separate faces in area 13.
    /// SQL Server treats NULLs as equal for uniqueness, which is what makes "one tenant-wide
    /// default" and "one rule per unit" fall out of the same index.
    ///
    /// The scaffolded CreateTable/CreateIndex/AddColumn bodies are replaced with guarded SQL (repo
    /// convention): local dev DBs are built from the EF model by rebuild-db, so a database can
    /// already carry these objects without this migration being stamped — every operation checks
    /// before it acts. The generated Designer and the regenerated snapshot are kept as scaffolded.
    /// </summary>
    public partial class AddProbationConfirmingAuthority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[ProbationReminderDispatchLogs]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[ProbationReminderDispatchLogs]', N'RoutedToEmployeeId') IS NULL
BEGIN
    ALTER TABLE [ProbationReminderDispatchLogs] ADD [RoutedToEmployeeId] uniqueidentifier NULL;
END;
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[ProbationConfirmingAuthorities]', N'U') IS NULL
BEGIN
    CREATE TABLE [ProbationConfirmingAuthorities] (
        [Id] uniqueidentifier NOT NULL,
        [OrganizationUnitId] uniqueidentifier NULL,
        [StaffLevelId] uniqueidentifier NULL,
        [AuthorityEmployeeId] uniqueidentifier NOT NULL,
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(500) NULL,
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
        CONSTRAINT [PK_ProbationConfirmingAuthorities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProbationConfirmingAuthorities_Employees_AuthorityEmployeeId] FOREIGN KEY ([AuthorityEmployeeId])
            REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProbationConfirmingAuthorities_OrganizationUnits_OrganizationUnitId] FOREIGN KEY ([OrganizationUnitId])
            REFERENCES [OrganizationUnits] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProbationConfirmingAuthorities_StaffLevels_StaffLevelId] FOREIGN KEY ([StaffLevelId])
            REFERENCES [StaffLevels] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProbationConfirmingAuthorities_Tenants_TenantId] FOREIGN KEY ([TenantId])
            REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[ProbationConfirmingAuthorities]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_ProbationConfirmingAuthorities_AuthorityEmployeeId'
                     AND object_id = OBJECT_ID(N'[ProbationConfirmingAuthorities]'))
BEGIN
    CREATE INDEX [IX_ProbationConfirmingAuthorities_AuthorityEmployeeId]
        ON [ProbationConfirmingAuthorities] ([AuthorityEmployeeId]);
END;
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[ProbationConfirmingAuthorities]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_ProbationConfirmingAuthorities_OrganizationUnitId'
                     AND object_id = OBJECT_ID(N'[ProbationConfirmingAuthorities]'))
BEGIN
    CREATE INDEX [IX_ProbationConfirmingAuthorities_OrganizationUnitId]
        ON [ProbationConfirmingAuthorities] ([OrganizationUnitId]);
END;
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[ProbationConfirmingAuthorities]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_ProbationConfirmingAuthorities_StaffLevelId'
                     AND object_id = OBJECT_ID(N'[ProbationConfirmingAuthorities]'))
BEGIN
    CREATE INDEX [IX_ProbationConfirmingAuthorities_StaffLevelId]
        ON [ProbationConfirmingAuthorities] ([StaffLevelId]);
END;
");

            // ⚠ The [IsDeleted] = 0 filter must survive any hand-edit of this file. Dropping it
            // would make a soft-deleted rule occupy its slot permanently.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[ProbationConfirmingAuthorities]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_ProbationConfirmingAuthorities_TenantId_OrganizationUnitId_StaffLevelId'
                     AND object_id = OBJECT_ID(N'[ProbationConfirmingAuthorities]'))
BEGIN
    CREATE UNIQUE INDEX [IX_ProbationConfirmingAuthorities_TenantId_OrganizationUnitId_StaffLevelId]
        ON [ProbationConfirmingAuthorities] ([TenantId], [OrganizationUnitId], [StaffLevelId])
        WHERE [IsDeleted] = 0;
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[ProbationConfirmingAuthorities]', N'U') IS NOT NULL
    DROP TABLE [ProbationConfirmingAuthorities];
");
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[ProbationReminderDispatchLogs]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[ProbationReminderDispatchLogs]', N'RoutedToEmployeeId') IS NOT NULL
BEGIN
    ALTER TABLE [ProbationReminderDispatchLogs] DROP COLUMN [RoutedToEmployeeId];
END;
");
        }
    }
}
