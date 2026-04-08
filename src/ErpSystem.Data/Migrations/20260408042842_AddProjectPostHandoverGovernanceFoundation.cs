using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectPostHandoverGovernanceFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FirstResponseDate",
                table: "ProjectDefectLiabilityCases",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResolutionSlaDays",
                table: "ProjectDefectLiabilityCases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResponseSlaDays",
                table: "ProjectDefectLiabilityCases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarrantyCategory",
                table: "ProjectDefectLiabilityCases",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDefectLiabilityCases_ProjectId_FirstResponseDate",
                table: "ProjectDefectLiabilityCases",
                columns: new[] { "ProjectId", "FirstResponseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDefectLiabilityCases_ProjectId_WarrantyExpiryDate",
                table: "ProjectDefectLiabilityCases",
                columns: new[] { "ProjectId", "WarrantyExpiryDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectDefectLiabilityCases_ProjectId_FirstResponseDate",
                table: "ProjectDefectLiabilityCases");

            migrationBuilder.DropIndex(
                name: "IX_ProjectDefectLiabilityCases_ProjectId_WarrantyExpiryDate",
                table: "ProjectDefectLiabilityCases");

            migrationBuilder.DropColumn(
                name: "FirstResponseDate",
                table: "ProjectDefectLiabilityCases");

            migrationBuilder.DropColumn(
                name: "ResolutionSlaDays",
                table: "ProjectDefectLiabilityCases");

            migrationBuilder.DropColumn(
                name: "ResponseSlaDays",
                table: "ProjectDefectLiabilityCases");

            migrationBuilder.DropColumn(
                name: "WarrantyCategory",
                table: "ProjectDefectLiabilityCases");
        }
    }
}
