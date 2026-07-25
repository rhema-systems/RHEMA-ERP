using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260720090000_GrantFirstStageFinanceWorkflowPermissions")]
public partial class GrantFirstStageFinanceWorkflowPermissions : Migration
{
    private const string GrantMarker = "Migration:20260720090000_GrantFirstStageFinanceWorkflowPermissions";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Upgrade existing databases as well as fresh seeds. Endpoint permission is
        // intentionally combined with the workflow engine's per-record assignment check.
        migrationBuilder.Sql($$"""
INSERT INTO [dbo].[RolePermissions] ([RoleId], [PermissionId], [GrantedAt], [GrantedBy])
SELECT roles.[Id], permissions.[Id], SYSUTCDATETIME(), '{{GrantMarker}}'
FROM [dbo].[AspNetRoles] roles
CROSS JOIN [dbo].[Permissions] permissions
WHERE roles.[Name] IN (N'Accounts Officer', N'Senior Accountant')
  AND permissions.[Name] IN (N'Finance.Workflow.Approve', N'Finance.Workflow.Reject')
  AND NOT EXISTS
  (
      SELECT 1
      FROM [dbo].[RolePermissions] existing
      WHERE existing.[RoleId] = roles.[Id]
        AND existing.[PermissionId] = permissions.[Id]
  );
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Remove only grants created by this migration, preserving any pre-existing or
        // administrator-assigned permissions.
        migrationBuilder.Sql($$"""
DELETE rolePermissions
FROM [dbo].[RolePermissions] rolePermissions
WHERE rolePermissions.[GrantedBy] = '{{GrantMarker}}';
""");
    }
}
