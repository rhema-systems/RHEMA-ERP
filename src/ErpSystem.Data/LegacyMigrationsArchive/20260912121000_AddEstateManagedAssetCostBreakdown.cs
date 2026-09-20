using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260912121000_AddEstateManagedAssetCostBreakdown")]
    public partial class AddEstateManagedAssetCostBreakdown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OwnerConsiderationCost",
                table: "EstateManagedAssets",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExternalSurveyorCost",
                table: "EstateManagedAssets",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StampDutyCost",
                table: "EstateManagedAssets",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OtherAcquisitionCost",
                table: "EstateManagedAssets",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCapitalizedCost",
                table: "EstateManagedAssets",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwnerConsiderationCost",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "ExternalSurveyorCost",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "StampDutyCost",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "OtherAcquisitionCost",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "TotalCapitalizedCost",
                table: "EstateManagedAssets");
        }
    }
}
