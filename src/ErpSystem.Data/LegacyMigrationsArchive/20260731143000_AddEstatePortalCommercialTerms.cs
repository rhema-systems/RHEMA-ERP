using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260731143000_AddEstatePortalCommercialTerms")]
public partial class AddEstatePortalCommercialTerms : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ExternalLeaseTermMonths",
            table: "EstateManagedAssets",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "ExternalMonthlyRent",
            table: "EstateManagedAssets",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "ExternalSalePrice",
            table: "EstateManagedAssets",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE EstateManagedAssets
            SET ExternalSalePrice = ExternalListingPrice
            WHERE ExternalListingType IN ('Sale', 'SaleAndRent')
              AND ExternalListingPrice IS NOT NULL;

            UPDATE EstateManagedAssets
            SET ExternalMonthlyRent = ExternalListingPrice
            WHERE ExternalListingType = 'Rent'
              AND ExternalListingPrice IS NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ExternalLeaseTermMonths",
            table: "EstateManagedAssets");

        migrationBuilder.DropColumn(
            name: "ExternalMonthlyRent",
            table: "EstateManagedAssets");

        migrationBuilder.DropColumn(
            name: "ExternalSalePrice",
            table: "EstateManagedAssets");
    }
}
