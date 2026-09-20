using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260920173000_RepairEhcTicketOrganizationUnitAssignment")]
public sealed class RepairEhcTicketOrganizationUnitAssignment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[EhcTickets]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[dbo].[EhcTickets]', N'AssignedOrganizationUnitId') IS NULL
BEGIN
    ALTER TABLE [dbo].[EhcTickets] ADD [AssignedOrganizationUnitId] uniqueidentifier NULL;
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[EhcTickets]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[dbo].[EhcTickets]', N'AssignedOrganizationUnitId') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE [name] = N'IX_EhcTickets_AssignedOrganizationUnitId'
          AND [object_id] = OBJECT_ID(N'[dbo].[EhcTickets]'))
BEGIN
    CREATE INDEX [IX_EhcTickets_AssignedOrganizationUnitId]
        ON [dbo].[EhcTickets] ([AssignedOrganizationUnitId]);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[EhcTickets]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[dbo].[EhcTickets]', N'AssignedOrganizationUnitId') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE [name] = N'IX_EhcTickets_TenantId_AssignedOrganizationUnitId'
          AND [object_id] = OBJECT_ID(N'[dbo].[EhcTickets]'))
BEGIN
    CREATE INDEX [IX_EhcTickets_TenantId_AssignedOrganizationUnitId]
        ON [dbo].[EhcTickets] ([TenantId], [AssignedOrganizationUnitId]);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[EhcTickets]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[OrganizationUnits]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[dbo].[EhcTickets]', N'AssignedOrganizationUnitId') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[FK_EhcTickets_OrganizationUnits_AssignedOrganizationUnitId]', N'F') IS NULL
BEGIN
    ALTER TABLE [dbo].[EhcTickets]
        ADD CONSTRAINT [FK_EhcTickets_OrganizationUnits_AssignedOrganizationUnitId]
        FOREIGN KEY ([AssignedOrganizationUnitId])
        REFERENCES [dbo].[OrganizationUnits] ([Id])
        ON DELETE NO ACTION;
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[EhcTickets]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[Departments]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[OrganizationUnits]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[dbo].[EhcTickets]', N'AssignedOrganizationUnitId') IS NOT NULL
BEGIN
    UPDATE ticket
    SET [AssignedOrganizationUnitId] = salesAndMarketingUnit.[Id]
    FROM [dbo].[EhcTickets] ticket
    INNER JOIN [dbo].[Departments] legacyDepartment
        ON legacyDepartment.[Id] = ticket.[AssignedDepartmentId]
    CROSS APPLY (
        SELECT TOP (1) organizationUnit.[Id]
        FROM [dbo].[OrganizationUnits] organizationUnit
        WHERE organizationUnit.[TenantId] = ticket.[TenantId]
          AND organizationUnit.[Code] = N'UNIT-MKT'
          AND organizationUnit.[IsDeleted] = 0
          AND organizationUnit.[IsActive] = 1
        ORDER BY organizationUnit.[Sequence], organizationUnit.[Id]
    ) salesAndMarketingUnit
    WHERE ticket.[AssignedOrganizationUnitId] IS NULL
      AND ticket.[PropertyListingContextJson] IS NOT NULL
      AND ticket.[IsDeleted] = 0
      AND legacyDepartment.[TenantId] = ticket.[TenantId]
      AND legacyDepartment.[DepartmentType] = 11
      AND legacyDepartment.[IsDeleted] = 0
      AND legacyDepartment.[IsActive] = 1;
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[EhcTickets]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[dbo].[EhcTickets]', N'AssignedOrganizationUnitId') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'[dbo].[FK_EhcTickets_OrganizationUnits_AssignedOrganizationUnitId]', N'F') IS NOT NULL
        ALTER TABLE [dbo].[EhcTickets] DROP CONSTRAINT [FK_EhcTickets_OrganizationUnits_AssignedOrganizationUnitId];

    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE [name] = N'IX_EhcTickets_TenantId_AssignedOrganizationUnitId'
          AND [object_id] = OBJECT_ID(N'[dbo].[EhcTickets]'))
        DROP INDEX [IX_EhcTickets_TenantId_AssignedOrganizationUnitId] ON [dbo].[EhcTickets];

    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE [name] = N'IX_EhcTickets_AssignedOrganizationUnitId'
          AND [object_id] = OBJECT_ID(N'[dbo].[EhcTickets]'))
        DROP INDEX [IX_EhcTickets_AssignedOrganizationUnitId] ON [dbo].[EhcTickets];

    ALTER TABLE [dbo].[EhcTickets] DROP COLUMN [AssignedOrganizationUnitId];
END
""");
    }
}
