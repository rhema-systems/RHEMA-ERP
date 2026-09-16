using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260808172100_AddQuantitySurveyMarketSurveyLineage")]
public partial class AddQuantitySurveyMarketSurveyLineage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "MarketAnalysisId",
            table: "QuantitySurveyRateLibraryRates",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MarketAnalysisCodeSnapshot",
            table: "QuantitySurveyRateLibraryRates",
            type: "varchar(50)",
            unicode: false,
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "MarketSurveyQuoteCount",
            table: "QuantitySurveyRateLibraryRates",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "NextReviewDueAt",
            table: "QuantitySurveyRateLibraryRates",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PreviousCurrencyCodeSnapshot",
            table: "QuantitySurveyRateLibraryRates",
            type: "varchar(3)",
            unicode: false,
            maxLength: 3,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "PreviousRateId",
            table: "QuantitySurveyRateLibraryRates",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "PreviousUnitRate",
            table: "QuantitySurveyRateLibraryRates",
            type: "decimal(18,4)",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_QsRateLibraryRates_MarketSurveyLineage",
            table: "QuantitySurveyRateLibraryRates",
            sql: "([SourceType] <> 11 AND [MarketAnalysisId] IS NULL AND [MarketAnalysisCodeSnapshot] IS NULL AND [MarketSurveyQuoteCount] IS NULL AND [NextReviewDueAt] IS NULL) OR ([SourceType] = 11 AND (([MarketAnalysisId] IS NULL AND [MarketAnalysisCodeSnapshot] IS NULL AND [MarketSurveyQuoteCount] IS NULL AND [NextReviewDueAt] IS NULL) OR ([MarketAnalysisId] IS NOT NULL AND [MarketAnalysisCodeSnapshot] IS NOT NULL AND [MarketSurveyQuoteCount] > 0 AND [NextReviewDueAt] IS NOT NULL)))");

        migrationBuilder.AddCheckConstraint(
            name: "CK_QsRateLibraryRates_PreviousRateSnapshot",
            table: "QuantitySurveyRateLibraryRates",
            sql: "([PreviousRateId] IS NULL AND [PreviousUnitRate] IS NULL AND [PreviousCurrencyCodeSnapshot] IS NULL) OR ([PreviousRateId] IS NOT NULL AND [PreviousUnitRate] IS NOT NULL AND [PreviousCurrencyCodeSnapshot] IS NOT NULL)");

        migrationBuilder.CreateIndex(
            name: "IX_QuantitySurveyRateLibraryRates_MarketAnalysisId",
            table: "QuantitySurveyRateLibraryRates",
            column: "MarketAnalysisId");

        migrationBuilder.CreateIndex(
            name: "IX_QuantitySurveyRateLibraryRates_PreviousRateId",
            table: "QuantitySurveyRateLibraryRates",
            column: "PreviousRateId");

        migrationBuilder.CreateIndex(
            name: "IX_QuantitySurveyRateLibraryRates_TenantId_MarketAnalysisId",
            table: "QuantitySurveyRateLibraryRates",
            columns: new[] { "TenantId", "MarketAnalysisId" });

        migrationBuilder.CreateIndex(
            name: "IX_QuantitySurveyRateLibraryRates_TenantId_NextReviewDueAt",
            table: "QuantitySurveyRateLibraryRates",
            columns: new[] { "TenantId", "NextReviewDueAt" });

        migrationBuilder.AddForeignKey(
            name: "FK_QuantitySurveyRateLibraryRates_MarketAnalyses_MarketAnalysisId",
            table: "QuantitySurveyRateLibraryRates",
            column: "MarketAnalysisId",
            principalTable: "MarketAnalyses",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_QuantitySurveyRateLibraryRates_QuantitySurveyRateLibraryRates_PreviousRateId",
            table: "QuantitySurveyRateLibraryRates",
            column: "PreviousRateId",
            principalTable: "QuantitySurveyRateLibraryRates",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_QuantitySurveyRateLibraryRates_MarketAnalyses_MarketAnalysisId",
            table: "QuantitySurveyRateLibraryRates");

        migrationBuilder.DropForeignKey(
            name: "FK_QuantitySurveyRateLibraryRates_QuantitySurveyRateLibraryRates_PreviousRateId",
            table: "QuantitySurveyRateLibraryRates");

        migrationBuilder.DropCheckConstraint(
            name: "CK_QsRateLibraryRates_MarketSurveyLineage",
            table: "QuantitySurveyRateLibraryRates");

        migrationBuilder.DropCheckConstraint(
            name: "CK_QsRateLibraryRates_PreviousRateSnapshot",
            table: "QuantitySurveyRateLibraryRates");

        migrationBuilder.DropIndex(
            name: "IX_QuantitySurveyRateLibraryRates_MarketAnalysisId",
            table: "QuantitySurveyRateLibraryRates");

        migrationBuilder.DropIndex(
            name: "IX_QuantitySurveyRateLibraryRates_PreviousRateId",
            table: "QuantitySurveyRateLibraryRates");

        migrationBuilder.DropIndex(
            name: "IX_QuantitySurveyRateLibraryRates_TenantId_MarketAnalysisId",
            table: "QuantitySurveyRateLibraryRates");

        migrationBuilder.DropIndex(
            name: "IX_QuantitySurveyRateLibraryRates_TenantId_NextReviewDueAt",
            table: "QuantitySurveyRateLibraryRates");

        migrationBuilder.DropColumn(name: "MarketAnalysisCodeSnapshot", table: "QuantitySurveyRateLibraryRates");
        migrationBuilder.DropColumn(name: "MarketAnalysisId", table: "QuantitySurveyRateLibraryRates");
        migrationBuilder.DropColumn(name: "MarketSurveyQuoteCount", table: "QuantitySurveyRateLibraryRates");
        migrationBuilder.DropColumn(name: "NextReviewDueAt", table: "QuantitySurveyRateLibraryRates");
        migrationBuilder.DropColumn(name: "PreviousCurrencyCodeSnapshot", table: "QuantitySurveyRateLibraryRates");
        migrationBuilder.DropColumn(name: "PreviousRateId", table: "QuantitySurveyRateLibraryRates");
        migrationBuilder.DropColumn(name: "PreviousUnitRate", table: "QuantitySurveyRateLibraryRates");
    }
}
