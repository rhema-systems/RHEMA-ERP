using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Aligns existing deployments with the Finance role catalogue: Chief Accountants may export
/// controlled Finance reports they are responsible for reviewing. The API remains protected by
/// the dedicated Finance.Reports.Export permission.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260824223000_GrantFinanceReportExportToChiefAccountant")]
public sealed class GrantFinanceReportExportToChiefAccountant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO [RolePermissions] ([RoleId], [PermissionId], [GrantedAt], [GrantedBy])
            SELECT [role].[Id], [permission].[Id], SYSUTCDATETIME(),
                   N'Chief Accountant report export migration'
            FROM [AspNetRoles] AS [role]
            CROSS JOIN [Permissions] AS [permission]
            WHERE [role].[Name] = N'Chief Accountant'
              AND [permission].[Name] = N'Finance.Reports.Export'
              AND [permission].[IsDeleted] = 0
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [RolePermissions] AS [existing]
                  WHERE [existing].[RoleId] = [role].[Id]
                    AND [existing].[PermissionId] = [permission].[Id]
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE [rolePermission]
            FROM [RolePermissions] AS [rolePermission]
            INNER JOIN [AspNetRoles] AS [role]
                ON [role].[Id] = [rolePermission].[RoleId]
            INNER JOIN [Permissions] AS [permission]
                ON [permission].[Id] = [rolePermission].[PermissionId]
            WHERE [role].[Name] = N'Chief Accountant'
              AND [permission].[Name] = N'Finance.Reports.Export'
              AND [rolePermission].[GrantedBy] = N'Chief Accountant report export migration';
            """);
    }
}
