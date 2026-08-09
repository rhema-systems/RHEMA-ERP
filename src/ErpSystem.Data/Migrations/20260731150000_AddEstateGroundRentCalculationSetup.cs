using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260731150000_AddEstateGroundRentCalculationSetup")]
public partial class AddEstateGroundRentCalculationSetup : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "GroundRentComputed",
            table: "EstateManagedAssets",
            type: "decimal(18,3)",
            precision: 18,
            scale: 3,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "GroundRentRatePerAcre",
            table: "EstateManagedAssets",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "GroundRentComputed",
            table: "EstateManagedAssets");

        migrationBuilder.DropColumn(
            name: "GroundRentRatePerAcre",
            table: "EstateManagedAssets");
    }
}
