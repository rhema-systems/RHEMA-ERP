using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260808215325_AddQuantitySurveyHistoricalRatePromotion")]
    /// <inheritdoc />
    public partial class AddQuantitySurveyHistoricalRatePromotion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HistoricalProjectCodeSnapshot",
                table: "QuantitySurveyRateLibraryRates",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HistoricalProjectId",
                table: "QuantitySurveyRateLibraryRates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HistoricalQuantity",
                table: "QuantitySurveyRateLibraryRates",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HistoricalSourceHash",
                table: "QuantitySurveyRateLibraryRates",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HistoricalSourceId",
                table: "QuantitySurveyRateLibraryRates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HistoricalSourceLabelSnapshot",
                table: "QuantitySurveyRateLibraryRates",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HistoricalSourceType",
                table: "QuantitySurveyRateLibraryRates",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HistoricalTotalAmount",
                table: "QuantitySurveyRateLibraryRates",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HistoricalUnitOfMeasureSnapshot",
                table: "QuantitySurveyRateLibraryRates",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryRates_HistoricalProjectId",
                table: "QuantitySurveyRateLibraryRates",
                column: "HistoricalProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryRates_TenantId_HistoricalProjectId_HistoricalSourceType",
                table: "QuantitySurveyRateLibraryRates",
                columns: new[] { "TenantId", "HistoricalProjectId", "HistoricalSourceType" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryRates_TenantId_HistoricalSourceType_HistoricalSourceId",
                table: "QuantitySurveyRateLibraryRates",
                columns: new[] { "TenantId", "HistoricalSourceType", "HistoricalSourceId" },
                unique: true,
                filter: "[HistoricalSourceType] IS NOT NULL AND [HistoricalSourceId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_QsRateLibraryRates_HistoricalLineage",
                table: "QuantitySurveyRateLibraryRates",
                sql: "([SourceType] <> 8 AND [HistoricalSourceType] IS NULL AND [HistoricalSourceId] IS NULL AND [HistoricalProjectId] IS NULL AND [HistoricalProjectCodeSnapshot] IS NULL AND [HistoricalSourceLabelSnapshot] IS NULL AND [HistoricalUnitOfMeasureSnapshot] IS NULL AND [HistoricalQuantity] IS NULL AND [HistoricalTotalAmount] IS NULL AND [HistoricalSourceHash] IS NULL) OR ([SourceType] = 8 AND (([HistoricalSourceType] IS NULL AND [HistoricalSourceId] IS NULL AND [HistoricalProjectId] IS NULL AND [HistoricalProjectCodeSnapshot] IS NULL AND [HistoricalSourceLabelSnapshot] IS NULL AND [HistoricalUnitOfMeasureSnapshot] IS NULL AND [HistoricalQuantity] IS NULL AND [HistoricalTotalAmount] IS NULL AND [HistoricalSourceHash] IS NULL) OR ([HistoricalSourceType] IN (0, 1, 2, 3) AND [HistoricalSourceId] IS NOT NULL AND [HistoricalProjectId] IS NOT NULL AND [HistoricalProjectCodeSnapshot] IS NOT NULL AND [HistoricalSourceLabelSnapshot] IS NOT NULL AND [HistoricalUnitOfMeasureSnapshot] IS NOT NULL AND [HistoricalQuantity] > 0 AND [HistoricalTotalAmount] > 0 AND [HistoricalSourceHash] IS NOT NULL)))");

            migrationBuilder.AddForeignKey(
                name: "FK_QuantitySurveyRateLibraryRates_Projects_HistoricalProjectId",
                table: "QuantitySurveyRateLibraryRates",
                column: "HistoricalProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuantitySurveyRateLibraryRates_Projects_HistoricalProjectId",
                table: "QuantitySurveyRateLibraryRates");

            migrationBuilder.DropIndex(
                name: "IX_QuantitySurveyRateLibraryRates_HistoricalProjectId",
                table: "QuantitySurveyRateLibraryRates");

            migrationBuilder.DropIndex(
                name: "IX_QuantitySurveyRateLibraryRates_TenantId_HistoricalProjectId_HistoricalSourceType",
                table: "QuantitySurveyRateLibraryRates");

            migrationBuilder.DropIndex(
                name: "IX_QuantitySurveyRateLibraryRates_TenantId_HistoricalSourceType_HistoricalSourceId",
                table: "QuantitySurveyRateLibraryRates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_QsRateLibraryRates_HistoricalLineage",
                table: "QuantitySurveyRateLibraryRates");

            migrationBuilder.DropColumn(
                name: "HistoricalProjectCodeSnapshot",
                table: "QuantitySurveyRateLibraryRates");

            migrationBuilder.DropColumn(
                name: "HistoricalProjectId",
                table: "QuantitySurveyRateLibraryRates");

            migrationBuilder.DropColumn(
                name: "HistoricalQuantity",
                table: "QuantitySurveyRateLibraryRates");

            migrationBuilder.DropColumn(
                name: "HistoricalSourceHash",
                table: "QuantitySurveyRateLibraryRates");

            migrationBuilder.DropColumn(
                name: "HistoricalSourceId",
                table: "QuantitySurveyRateLibraryRates");

            migrationBuilder.DropColumn(
                name: "HistoricalSourceLabelSnapshot",
                table: "QuantitySurveyRateLibraryRates");

            migrationBuilder.DropColumn(
                name: "HistoricalSourceType",
                table: "QuantitySurveyRateLibraryRates");

            migrationBuilder.DropColumn(
                name: "HistoricalTotalAmount",
                table: "QuantitySurveyRateLibraryRates");

            migrationBuilder.DropColumn(
                name: "HistoricalUnitOfMeasureSnapshot",
                table: "QuantitySurveyRateLibraryRates");
        }
    }
}
