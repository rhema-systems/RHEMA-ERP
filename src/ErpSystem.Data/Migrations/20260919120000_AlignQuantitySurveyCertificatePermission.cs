using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

// Data-only reconciliation: no QS records, policy values or custom-role grants are removed.
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260919120000_AlignQuantitySurveyCertificatePermission")]
public sealed class AlignQuantitySurveyCertificatePermission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Name = N'quantity-survey.certificates.manage')
                INSERT dbo.Permissions
                    (Id, Name, DisplayName, Description, Category, IsSystemPermission, IsDeleted, CreatedAt, CreatedBy)
                VALUES (NEWID(), N'quantity-survey.certificates.manage', N'Manage QS certificates',
                    N'Prepare and submit payment certificates, route approved certificates to Finance, and manage retention and deductions.',
                    N'Quantity Survey', 1, 0, SYSUTCDATETIME(), N'QS architecture alignment');

            UPDATE dbo.Permissions
            SET Description = N'Prepare and submit payment certificates, route approved certificates to Finance, and manage retention and deductions.'
            WHERE Name = N'quantity-survey.certificates.manage' AND IsDeleted = 0;

            INSERT dbo.RolePermissions (RoleId, PermissionId, GrantedAt, GrantedBy)
            SELECT r.Id, p.Id, SYSUTCDATETIME(), N'QS architecture alignment'
            FROM dbo.AspNetRoles r
            JOIN dbo.Permissions p ON p.Name = N'quantity-survey.certificates.manage' AND p.IsDeleted = 0
            WHERE r.NormalizedName IN (N'TDC_QUANTITY_SURVEYOR', N'TDC_SUPERVISING_QUANTITY_SURVEYOR')
              AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Existing and reconciled grants are indistinguishable. Retain them on rollback;
        // removing them could revoke permissions that predate this migration.
    }
}
