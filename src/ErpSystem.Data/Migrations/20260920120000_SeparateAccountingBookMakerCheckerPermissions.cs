using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260920120000_SeparateAccountingBookMakerCheckerPermissions")]
    /// <inheritdoc />
    public sealed class SeparateAccountingBookMakerCheckerPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE rp
                FROM [RolePermissions] rp
                INNER JOIN [AspNetRoles] r ON r.[Id] = rp.[RoleId]
                INNER JOIN [Permissions] p ON p.[Id] = rp.[PermissionId]
                WHERE r.[NormalizedName] = N'FINANCIAL CONTROLLER'
                  AND p.[Name] IN
                  (
                      N'Finance.AccountingBooks.Manage',
                      N'Finance.AccountingBooks.Transitions.Request',
                      N'Finance.AccountingBooks.Periods.Manage',
                      N'Finance.AccountingBooks.Initialization.Manage',
                      N'Finance.AccountingBooks.ApplicabilityPolicy.Manage'
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO [RolePermissions] ([RoleId], [PermissionId], [GrantedAt], [GrantedBy])
                SELECT r.[Id], p.[Id], SYSUTCDATETIME(), N'System'
                FROM [AspNetRoles] r
                CROSS JOIN [Permissions] p
                WHERE r.[NormalizedName] = N'FINANCIAL CONTROLLER'
                  AND p.[Name] IN
                  (
                      N'Finance.AccountingBooks.Manage',
                      N'Finance.AccountingBooks.Transitions.Request',
                      N'Finance.AccountingBooks.Periods.Manage',
                      N'Finance.AccountingBooks.Initialization.Manage',
                      N'Finance.AccountingBooks.ApplicabilityPolicy.Manage'
                  )
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM [RolePermissions] existing
                      WHERE existing.[RoleId] = r.[Id]
                        AND existing.[PermissionId] = p.[Id]
                  );
                """);
        }
    }
}
