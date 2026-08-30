using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Aligns market-survey supplier references with the central BusinessPartner
/// master used by the UI and the rest of governed procurement. Existing legacy
/// supplier names are retained as immutable quote snapshots.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260822235500_AlignMarketSurveySuppliersToBusinessPartners")]
public sealed class AlignMarketSurveySuppliersToBusinessPartners : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE history
               SET history.SupplierName = legacy.Name
            FROM dbo.PriceHistories history
            INNER JOIN dbo.Suppliers legacy
                ON legacy.Id = history.SupplierId
               AND legacy.TenantId = history.TenantId
            WHERE history.SupplierId IS NOT NULL
              AND (history.SupplierName IS NULL OR LTRIM(RTRIM(history.SupplierName)) = N'');
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_PriceHistories_Suppliers_SupplierId",
            table: "PriceHistories");

        migrationBuilder.Sql(
            """
            UPDATE history
               SET history.SupplierId = NULL
            FROM dbo.PriceHistories history
            WHERE history.SupplierId IS NOT NULL
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.BusinessPartners partner
                  WHERE partner.Id = history.SupplierId
                    AND partner.TenantId = history.TenantId
              );
            """);

        migrationBuilder.AddForeignKey(
            name: "FK_PriceHistories_BusinessPartners_SupplierId",
            table: "PriceHistories",
            column: "SupplierId",
            principalTable: "BusinessPartners",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE history
               SET history.SupplierName = partner.PartnerName
            FROM dbo.PriceHistories history
            INNER JOIN dbo.BusinessPartners partner
                ON partner.Id = history.SupplierId
               AND partner.TenantId = history.TenantId
            WHERE history.SupplierId IS NOT NULL
              AND (history.SupplierName IS NULL OR LTRIM(RTRIM(history.SupplierName)) = N'');
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_PriceHistories_BusinessPartners_SupplierId",
            table: "PriceHistories");

        migrationBuilder.Sql(
            """
            UPDATE history
               SET history.SupplierId = NULL
            FROM dbo.PriceHistories history
            WHERE history.SupplierId IS NOT NULL
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.Suppliers legacy
                  WHERE legacy.Id = history.SupplierId
                    AND legacy.TenantId = history.TenantId
              );
            """);

        migrationBuilder.AddForeignKey(
            name: "FK_PriceHistories_Suppliers_SupplierId",
            table: "PriceHistories",
            column: "SupplierId",
            principalTable: "Suppliers",
            principalColumn: "Id");
    }
}
