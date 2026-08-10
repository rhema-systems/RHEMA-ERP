using System;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds the immutable FIN-LIM-0039 evidence used when depreciation is calculated through an
/// asset's disposal date. This migration is intentionally narrow because the repository's
/// design-time snapshot has unrelated drift; generating it automatically attempts to recreate
/// unrelated module tables. Existing development disposals receive zero/null evidence as expected.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260810143000_AddFixedAssetDisposalDateDepreciation")]
public partial class AddFixedAssetDisposalDateDepreciation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("AssetDisposalId", "AssetDepreciationSchedules", "uniqueidentifier", nullable: true);

        migrationBuilder.AddColumn<decimal>("FinalDepreciationAmount", "AssetDisposals", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<string>("FinalDepreciationEvidenceNotes", "AssetDisposals", "nvarchar(1000)", maxLength: 1000, nullable: true);
        migrationBuilder.AddColumn<string>("FinalDepreciationEvidenceReference", "AssetDisposals", "nvarchar(200)", maxLength: 200, nullable: true);
        migrationBuilder.AddColumn<int>("FinalDepreciationEligibleDays", "AssetDisposals", "int", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<decimal>("FinalDepreciationDiminishingRatePercent", "AssetDisposals", "decimal(18,4)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("FinalDepreciationLifetimeProductionCapacity", "AssetDisposals", "decimal(18,4)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("FinalDepreciationCumulativeProductionUnitsBefore", "AssetDisposals", "decimal(18,4)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("FinalDepreciationCumulativeProductionUnitsAfter", "AssetDisposals", "decimal(18,4)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<DateTime>("FinalDepreciationFromDate", "AssetDisposals", "datetime2", nullable: true);
        migrationBuilder.AddColumn<int>("FinalDepreciationMethodSnapshot", "AssetDisposals", "int", nullable: true);
        migrationBuilder.AddColumn<int>("FinalDepreciationPeriodDays", "AssetDisposals", "int", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<decimal>("FinalDepreciationProductionUnits", "AssetDisposals", "decimal(18,4)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<string>("FinalDepreciationProrationBasis", "AssetDisposals", "nvarchar(30)", maxLength: 30, nullable: true);
        migrationBuilder.AddColumn<Guid>("FinalDepreciationScheduleId", "AssetDisposals", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<DateTime>("FinalDepreciationToDate", "AssetDisposals", "datetime2", nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_AssetDepreciationSchedules_TenantId_AssetDisposalId",
            table: "AssetDepreciationSchedules",
            columns: new[] { "TenantId", "AssetDisposalId" },
            unique: true,
            filter: "[AssetDisposalId] IS NOT NULL");

        // Restrict deletion because a posted depreciation schedule is permanent evidence supporting
        // the disposal journal and must not disappear if a development cleanup removes a request.
        migrationBuilder.AddForeignKey(
            name: "FK_AssetDepreciationSchedules_AssetDisposals_AssetDisposalId",
            table: "AssetDepreciationSchedules",
            column: "AssetDisposalId",
            principalTable: "AssetDisposals",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_AssetDepreciationSchedules_AssetDisposals_AssetDisposalId", "AssetDepreciationSchedules");
        migrationBuilder.DropIndex("IX_AssetDepreciationSchedules_TenantId_AssetDisposalId", "AssetDepreciationSchedules");
        migrationBuilder.DropColumn("AssetDisposalId", "AssetDepreciationSchedules");
        migrationBuilder.DropColumn("FinalDepreciationAmount", "AssetDisposals");
        migrationBuilder.DropColumn("FinalDepreciationEvidenceNotes", "AssetDisposals");
        migrationBuilder.DropColumn("FinalDepreciationEvidenceReference", "AssetDisposals");
        migrationBuilder.DropColumn("FinalDepreciationEligibleDays", "AssetDisposals");
        migrationBuilder.DropColumn("FinalDepreciationDiminishingRatePercent", "AssetDisposals");
        migrationBuilder.DropColumn("FinalDepreciationLifetimeProductionCapacity", "AssetDisposals");
        migrationBuilder.DropColumn("FinalDepreciationCumulativeProductionUnitsBefore", "AssetDisposals");
        migrationBuilder.DropColumn("FinalDepreciationCumulativeProductionUnitsAfter", "AssetDisposals");
        migrationBuilder.DropColumn("FinalDepreciationFromDate", "AssetDisposals");
        migrationBuilder.DropColumn("FinalDepreciationMethodSnapshot", "AssetDisposals");
        migrationBuilder.DropColumn("FinalDepreciationPeriodDays", "AssetDisposals");
        migrationBuilder.DropColumn("FinalDepreciationProductionUnits", "AssetDisposals");
        migrationBuilder.DropColumn("FinalDepreciationProrationBasis", "AssetDisposals");
        migrationBuilder.DropColumn("FinalDepreciationScheduleId", "AssetDisposals");
        migrationBuilder.DropColumn("FinalDepreciationToDate", "AssetDisposals");
    }
}
