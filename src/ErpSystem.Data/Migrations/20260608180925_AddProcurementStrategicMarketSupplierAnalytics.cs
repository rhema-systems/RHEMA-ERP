using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementStrategicMarketSupplierAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "PriceChangePercent",
                table: "MarketAnalyses",
                type: "decimal(8,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "HistoricalAveragePrice",
                table: "MarketAnalyses",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "ForecastedPrice",
                table: "MarketAnalyses",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentMarketPrice",
                table: "MarketAnalyses",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<Guid>(
                name: "MarketAnalysisId",
                table: "ProcurementPlanItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InflationImpactPercent",
                table: "MarketAnalyses",
                type: "decimal(8,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "LeadTimeDays",
                table: "MarketAnalyses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MarketAvailability",
                table: "MarketAnalyses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Medium");

            migrationBuilder.AddColumn<decimal>(
                name: "PreviousPrice",
                table: "MarketAnalyses",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceVariancePercent",
                table: "MarketAnalyses",
                type: "decimal(8,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RecommendedBudgetAdjustmentPercent",
                table: "MarketAnalyses",
                type: "decimal(8,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SupplyRiskLevel",
                table: "MarketAnalyses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Medium");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlanItems_MarketAnalysisId",
                table: "ProcurementPlanItems",
                column: "MarketAnalysisId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementPlanItems_MarketAnalyses_MarketAnalysisId",
                table: "ProcurementPlanItems",
                column: "MarketAnalysisId",
                principalTable: "MarketAnalyses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementPlanItems_MarketAnalyses_MarketAnalysisId",
                table: "ProcurementPlanItems");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementPlanItems_MarketAnalysisId",
                table: "ProcurementPlanItems");

            migrationBuilder.DropColumn(
                name: "MarketAnalysisId",
                table: "ProcurementPlanItems");

            migrationBuilder.DropColumn(
                name: "InflationImpactPercent",
                table: "MarketAnalyses");

            migrationBuilder.DropColumn(
                name: "LeadTimeDays",
                table: "MarketAnalyses");

            migrationBuilder.DropColumn(
                name: "MarketAvailability",
                table: "MarketAnalyses");

            migrationBuilder.DropColumn(
                name: "PreviousPrice",
                table: "MarketAnalyses");

            migrationBuilder.DropColumn(
                name: "PriceVariancePercent",
                table: "MarketAnalyses");

            migrationBuilder.DropColumn(
                name: "RecommendedBudgetAdjustmentPercent",
                table: "MarketAnalyses");

            migrationBuilder.DropColumn(
                name: "SupplyRiskLevel",
                table: "MarketAnalyses");

            migrationBuilder.AlterColumn<decimal>(
                name: "PriceChangePercent",
                table: "MarketAnalyses",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(8,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "HistoricalAveragePrice",
                table: "MarketAnalyses",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "ForecastedPrice",
                table: "MarketAnalyses",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentMarketPrice",
                table: "MarketAnalyses",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");
        }
    }
}
