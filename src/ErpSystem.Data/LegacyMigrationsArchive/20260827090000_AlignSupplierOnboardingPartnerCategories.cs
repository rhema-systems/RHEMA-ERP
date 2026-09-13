using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Makes supplier categories tenant-safe, provisions the three categories shown
/// during onboarding, and repairs approved suppliers created before approval
/// began assigning their selected registration category.
/// </summary>
public partial class AlignSupplierOnboardingPartnerCategories : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_PartnerCategories_CategoryCode",
            table: "PartnerCategories");

        migrationBuilder.CreateIndex(
            name: "IX_PartnerCategories_TenantId_CategoryCode",
            table: "PartnerCategories",
            columns: new[] { "TenantId", "CategoryCode" },
            unique: true);

        migrationBuilder.Sql("""
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            UPDATE categories
            SET categories.IsActive = 1,
                categories.IsDeleted = 0,
                categories.DeletedAt = NULL,
                categories.DeletedBy = NULL,
                categories.UpdatedAt = @Now,
                categories.UpdatedBy = N'System'
            FROM PartnerCategories AS categories
            WHERE categories.CategoryCode IN (N'GOODS', N'WORKS', N'SERVICES');

            INSERT INTO PartnerCategories
                (Id, CategoryCode, CategoryName, CategoryType, Description,
                 ParentCategoryId, IsActive, CreatedAt, UpdatedAt, CreatedBy,
                 UpdatedBy, CreatedById, LastModifiedById, IsDeleted, DeletedAt,
                 DeletedBy, TenantId)
            SELECT NEWID(), definitions.CategoryCode, definitions.CategoryName,
                   N'Supplier', definitions.Description, NULL, 1, @Now, NULL,
                   N'System', NULL, NULL, NULL, 0, NULL, NULL, tenants.Id
            FROM Tenants AS tenants
            CROSS JOIN (VALUES
                (N'GOODS', N'Goods', N'Suppliers approved through the Goods registration category.'),
                (N'WORKS', N'Works', N'Suppliers and contractors approved through the Works registration category.'),
                (N'SERVICES', N'Services', N'Suppliers approved through the Services registration category.')
            ) AS definitions(CategoryCode, CategoryName, Description)
            WHERE tenants.IsDeleted = 0
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM PartnerCategories AS existing
                  WHERE existing.TenantId = tenants.Id
                    AND existing.CategoryCode = definitions.CategoryCode
              );

            INSERT INTO BusinessPartnerCategories
                (Id, BusinessPartnerId, CategoryId, IsPrimary)
            SELECT NEWID(), registrations.BusinessPartnerId, categories.Id,
                   CASE WHEN EXISTS
                   (
                       SELECT 1
                       FROM BusinessPartnerCategories AS assigned
                       WHERE assigned.BusinessPartnerId = registrations.BusinessPartnerId
                   ) THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END
            FROM BusinessPartnerRegistrations AS registrations
            INNER JOIN BusinessPartners AS partners
                ON partners.Id = registrations.BusinessPartnerId
               AND partners.TenantId = registrations.TenantId
               AND partners.IsDeleted = 0
            INNER JOIN PartnerCategories AS categories
                ON categories.TenantId = registrations.TenantId
               AND categories.CategoryCode = CASE registrations.RegistrationCategory
                    WHEN 0 THEN N'GOODS'
                    WHEN 1 THEN N'WORKS'
                    WHEN 2 THEN N'SERVICES'
                    ELSE N''
               END
               AND categories.IsActive = 1
               AND categories.IsDeleted = 0
            WHERE registrations.IsDeleted = 0
              AND registrations.Status = N'Approved'
              AND registrations.BusinessPartnerId IS NOT NULL
              AND registrations.RegistrationCategory IN (0, 1, 2)
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM BusinessPartnerCategories AS existing
                  WHERE existing.BusinessPartnerId = registrations.BusinessPartnerId
                    AND existing.CategoryId = categories.Id
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The migration creates tenant-owned classification data and supplier
        // assignments. Automatically deleting those records would corrupt
        // approved supplier history, so this data migration is intentionally
        // one-way and must be reversed only through a reviewed repair script.
        migrationBuilder.Sql("""
            THROW 51000, 'AlignSupplierOnboardingPartnerCategories cannot be rolled back automatically because it creates tenant-owned supplier classification data.', 1;
            """);
    }
}
