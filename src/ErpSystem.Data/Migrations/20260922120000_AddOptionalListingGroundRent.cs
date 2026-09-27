using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260922120000_AddOptionalListingGroundRent")]
    public partial class AddOptionalListingGroundRent : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExternalGroundRentRequired",
                table: "EstateManagedAssets",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExternalGroundRentRequired",
                table: "EstateLandDemarcations",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExternalPremiumChargeRequired",
                table: "EstateManagedAssets",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExternalPremiumChargeRequired",
                table: "EstateLandDemarcations",
                type: "bit",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ExternalGroundRentRequired", table: "EstateManagedAssets");
            migrationBuilder.DropColumn(name: "ExternalGroundRentRequired", table: "EstateLandDemarcations");
            migrationBuilder.DropColumn(name: "ExternalPremiumChargeRequired", table: "EstateManagedAssets");
            migrationBuilder.DropColumn(name: "ExternalPremiumChargeRequired", table: "EstateLandDemarcations");
        }
    }
}
