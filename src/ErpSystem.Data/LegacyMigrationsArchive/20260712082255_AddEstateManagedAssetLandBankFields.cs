using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEstateManagedAssetLandBankFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BoundaryCoordinates",
                table: "EstateManagedAssets",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BoundaryVerified",
                table: "EstateManagedAssets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GisLayerReference",
                table: "EstateManagedAssets",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MapSheetNumber",
                table: "EstateManagedAssets",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanningComplianceStatus",
                table: "EstateManagedAssets",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "EstateManagedAssets",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SurveyPlanNumber",
                table: "EstateManagedAssets",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZoningClassification",
                table: "EstateManagedAssets",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BoundaryCoordinates",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "BoundaryVerified",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "GisLayerReference",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "MapSheetNumber",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "PlanningComplianceStatus",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "SurveyPlanNumber",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "ZoningClassification",
                table: "EstateManagedAssets");
        }
    }
}
