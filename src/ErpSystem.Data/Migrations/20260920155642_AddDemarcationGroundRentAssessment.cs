using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDemarcationGroundRentAssessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GroundRentComputed",
                table: "EstateLandDemarcations",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GroundRentPayable",
                table: "EstateLandDemarcations",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GroundRentRatePerAcre",
                table: "EstateLandDemarcations",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroundRentComputed",
                table: "EstateLandDemarcations");

            migrationBuilder.DropColumn(
                name: "GroundRentPayable",
                table: "EstateLandDemarcations");

            migrationBuilder.DropColumn(
                name: "GroundRentRatePerAcre",
                table: "EstateLandDemarcations");
        }
    }
}
