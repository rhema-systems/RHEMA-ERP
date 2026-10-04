using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261004103000_AddEstateListingPremiumChargeAmount")]
    public partial class AddEstateListingPremiumChargeAmount : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExternalPremiumChargeAmount",
                table: "EstateManagedAssets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExternalPremiumChargeAmount",
                table: "EstateLandDemarcations",
                type: "decimal(18,2)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ExternalPremiumChargeAmount", table: "EstateManagedAssets");
            migrationBuilder.DropColumn(name: "ExternalPremiumChargeAmount", table: "EstateLandDemarcations");
        }
    }
}
