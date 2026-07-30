using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ErpSystem.Data.ApplicationDbContext))]
[Migration("20260730024500_TDC0401FrameworkRevisionIndexes")]
public partial class TDC0401FrameworkRevisionIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ProcurementFrameworkAgreements_TenantId_AgreementNumber",
            table: "ProcurementFrameworkAgreements");

        migrationBuilder.CreateIndex(
            name: "IX_ProcurementFrameworkAgreements_TenantId_AgreementNumber_Version",
            table: "ProcurementFrameworkAgreements",
            columns: new[] { "TenantId", "AgreementNumber", "Version" },
            unique: true);

        RecreateUniqueIndex(
            migrationBuilder,
            "ProcurementFrameworkAgreementCategories",
            "IX_ProcurementFrameworkAgreementCategories_TenantId_AgreementId_PartnerCategoryId",
            new[] { "TenantId", "AgreementId", "PartnerCategoryId" },
            filterDeleted: true);

        RecreateUniqueIndex(
            migrationBuilder,
            "ProcurementFrameworkPriceListLines",
            "IX_ProcurementFrameworkPriceListLines_TenantId_AgreementId_InventoryItemId",
            new[] { "TenantId", "AgreementId", "InventoryItemId" },
            filterDeleted: true);

        RecreateUniqueIndex(
            migrationBuilder,
            "ProcurementFrameworkCallOffAuthorities",
            "IX_ProcurementFrameworkCallOffAuthorities_TenantId_AgreementId_AuthorityKind_AuthorityValue",
            new[] { "TenantId", "AgreementId", "AuthorityKind", "AuthorityValue" },
            filterDeleted: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        RecreateUniqueIndex(
            migrationBuilder,
            "ProcurementFrameworkAgreementCategories",
            "IX_ProcurementFrameworkAgreementCategories_TenantId_AgreementId_PartnerCategoryId",
            new[] { "TenantId", "AgreementId", "PartnerCategoryId" },
            filterDeleted: false);

        RecreateUniqueIndex(
            migrationBuilder,
            "ProcurementFrameworkPriceListLines",
            "IX_ProcurementFrameworkPriceListLines_TenantId_AgreementId_InventoryItemId",
            new[] { "TenantId", "AgreementId", "InventoryItemId" },
            filterDeleted: false);

        RecreateUniqueIndex(
            migrationBuilder,
            "ProcurementFrameworkCallOffAuthorities",
            "IX_ProcurementFrameworkCallOffAuthorities_TenantId_AgreementId_AuthorityKind_AuthorityValue",
            new[] { "TenantId", "AgreementId", "AuthorityKind", "AuthorityValue" },
            filterDeleted: false);

        migrationBuilder.DropIndex(
            name: "IX_ProcurementFrameworkAgreements_TenantId_AgreementNumber_Version",
            table: "ProcurementFrameworkAgreements");

        migrationBuilder.CreateIndex(
            name: "IX_ProcurementFrameworkAgreements_TenantId_AgreementNumber",
            table: "ProcurementFrameworkAgreements",
            columns: new[] { "TenantId", "AgreementNumber" },
            unique: true);
    }

    private static void RecreateUniqueIndex(
        MigrationBuilder migrationBuilder,
        string table,
        string index,
        string[] columns,
        bool filterDeleted)
    {
        migrationBuilder.DropIndex(name: index, table: table);
        migrationBuilder.CreateIndex(
            name: index,
            table: table,
            columns: columns,
            unique: true,
            filter: filterDeleted ? "[IsDeleted] = 0" : null);
    }
}
