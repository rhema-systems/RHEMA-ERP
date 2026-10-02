using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261002153000_AddCommercialUomQuantityGovernance")]
public partial class AddCommercialUomQuantityGovernance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "DecimalPlaces", table: "UnitsOfMeasure", type: "int", nullable: false, defaultValue: 4);
        migrationBuilder.AddColumn<decimal>(name: "RoundingIncrement", table: "UnitsOfMeasure", type: "decimal(18,6)", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DecimalPlaces", table: "UnitsOfMeasure");
        migrationBuilder.DropColumn(name: "RoundingIncrement", table: "UnitsOfMeasure");
    }
}
