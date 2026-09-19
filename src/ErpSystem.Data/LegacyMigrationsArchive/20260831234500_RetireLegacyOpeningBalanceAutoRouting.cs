using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260831234500_RetireLegacyOpeningBalanceAutoRouting")]
public sealed class RetireLegacyOpeningBalanceAutoRouting : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "OpeningBalanceAutoRoutingEnabled",
            table: "FinanceSettings");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "OpeningBalanceAutoRoutingEnabled",
            table: "FinanceSettings",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }
}
