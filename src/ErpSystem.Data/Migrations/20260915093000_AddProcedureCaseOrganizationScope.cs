using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260915093000_AddProcedureCaseOrganizationScope")]
public sealed class AddProcedureCaseOrganizationScope : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[ProcedureCases]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[dbo].[ProcedureCases]', N'OrganizationUnitId') IS NULL
BEGIN
    ALTER TABLE [dbo].[ProcedureCases] ADD [OrganizationLevelId] uniqueidentifier NULL;
    ALTER TABLE [dbo].[ProcedureCases] ADD [OrganizationUnitId] uniqueidentifier NULL;

    CREATE INDEX [IX_ProcedureCases_OrganizationLevelId]
        ON [dbo].[ProcedureCases] ([OrganizationLevelId]);

    CREATE INDEX [IX_ProcedureCases_OrganizationUnitId]
        ON [dbo].[ProcedureCases] ([OrganizationUnitId]);

    CREATE INDEX [IX_ProcedureCases_TenantId_OrganizationUnitId]
        ON [dbo].[ProcedureCases] ([TenantId], [OrganizationUnitId]);

    ALTER TABLE [dbo].[ProcedureCases]
        ADD CONSTRAINT [FK_ProcedureCases_OrganizationLevels_OrganizationLevelId]
        FOREIGN KEY ([OrganizationLevelId])
        REFERENCES [dbo].[OrganizationLevels] ([Id])
        ON DELETE NO ACTION;

    ALTER TABLE [dbo].[ProcedureCases]
        ADD CONSTRAINT [FK_ProcedureCases_OrganizationUnits_OrganizationUnitId]
        FOREIGN KEY ([OrganizationUnitId])
        REFERENCES [dbo].[OrganizationUnits] ([Id])
        ON DELETE NO ACTION;
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[ProcedureCases]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[dbo].[ProcedureCases]', N'OrganizationUnitId') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'[dbo].[FK_ProcedureCases_OrganizationLevels_OrganizationLevelId]', N'F') IS NOT NULL
        ALTER TABLE [dbo].[ProcedureCases] DROP CONSTRAINT [FK_ProcedureCases_OrganizationLevels_OrganizationLevelId];

    IF OBJECT_ID(N'[dbo].[FK_ProcedureCases_OrganizationUnits_OrganizationUnitId]', N'F') IS NOT NULL
        ALTER TABLE [dbo].[ProcedureCases] DROP CONSTRAINT [FK_ProcedureCases_OrganizationUnits_OrganizationUnitId];

    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE [name] = N'IX_ProcedureCases_TenantId_OrganizationUnitId'
          AND [object_id] = OBJECT_ID(N'[dbo].[ProcedureCases]'))
        DROP INDEX [IX_ProcedureCases_TenantId_OrganizationUnitId] ON [dbo].[ProcedureCases];

    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE [name] = N'IX_ProcedureCases_OrganizationUnitId'
          AND [object_id] = OBJECT_ID(N'[dbo].[ProcedureCases]'))
        DROP INDEX [IX_ProcedureCases_OrganizationUnitId] ON [dbo].[ProcedureCases];

    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE [name] = N'IX_ProcedureCases_OrganizationLevelId'
          AND [object_id] = OBJECT_ID(N'[dbo].[ProcedureCases]'))
        DROP INDEX [IX_ProcedureCases_OrganizationLevelId] ON [dbo].[ProcedureCases];

    ALTER TABLE [dbo].[ProcedureCases] DROP COLUMN [OrganizationUnitId];
    ALTER TABLE [dbo].[ProcedureCases] DROP COLUMN [OrganizationLevelId];
END
""");
    }
}
