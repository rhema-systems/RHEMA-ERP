using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds the immutable FIN-LIM-0042 allocation evidence to the established asset-disposal table.
/// The migration remains deliberately narrow because the shared model snapshot contains unrelated
/// module drift; whole-asset history is safely identified as 100 percent by the defaults below.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260810160000_AddFixedAssetPartialComponentDisposal")]
public partial class AddFixedAssetPartialComponentDisposal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("DisposalScope", "AssetDisposals", "int", nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<decimal>("DisposedPortionPercent", "AssetDisposals", "decimal(18,4)", nullable: false, defaultValue: 100m);
        migrationBuilder.AddColumn<string>("ComponentReference", "AssetDisposals", "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>("ComponentDescription", "AssetDisposals", "nvarchar(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<string>("AllocationEvidenceReference", "AssetDisposals", "nvarchar(200)", maxLength: 200, nullable: true);
        migrationBuilder.AddColumn<string>("AllocationEvidenceNotes", "AssetDisposals", "nvarchar(1000)", maxLength: 1000, nullable: true);
        migrationBuilder.AddColumn<decimal>("AcquisitionCostAllocated", "AssetDisposals", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("RevaluationAdjustmentAllocated", "AssetDisposals", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("ResidualValueAllocated", "AssetDisposals", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("ProductionCapacityAllocated", "AssetDisposals", "decimal(18,4)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("AccumulatedProductionUnitsAllocated", "AssetDisposals", "decimal(18,4)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("RemainingAcquisitionCostAfterDisposal", "AssetDisposals", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("RemainingAccumulatedDepreciationAfterDisposal", "AssetDisposals", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("RemainingNetBookValueAfterDisposal", "AssetDisposals", "decimal(18,2)", nullable: false, defaultValue: 0m);

        // The database guard mirrors the API rule so imports and future integrations cannot label
        // a partial request as whole or accidentally derecognise 100% while retaining the asset.
        migrationBuilder.AddCheckConstraint(
            name: "CK_AssetDisposals_ScopePercentage",
            table: "AssetDisposals",
            sql: "([DisposalScope] = 1 AND [DisposedPortionPercent] = 100) OR ([DisposalScope] IN (2, 3) AND [DisposedPortionPercent] > 0 AND [DisposedPortionPercent] < 100)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_AssetDisposals_ScopePercentage", "AssetDisposals");
        migrationBuilder.DropColumn("DisposalScope", "AssetDisposals");
        migrationBuilder.DropColumn("DisposedPortionPercent", "AssetDisposals");
        migrationBuilder.DropColumn("ComponentReference", "AssetDisposals");
        migrationBuilder.DropColumn("ComponentDescription", "AssetDisposals");
        migrationBuilder.DropColumn("AllocationEvidenceReference", "AssetDisposals");
        migrationBuilder.DropColumn("AllocationEvidenceNotes", "AssetDisposals");
        migrationBuilder.DropColumn("AcquisitionCostAllocated", "AssetDisposals");
        migrationBuilder.DropColumn("RevaluationAdjustmentAllocated", "AssetDisposals");
        migrationBuilder.DropColumn("ResidualValueAllocated", "AssetDisposals");
        migrationBuilder.DropColumn("ProductionCapacityAllocated", "AssetDisposals");
        migrationBuilder.DropColumn("AccumulatedProductionUnitsAllocated", "AssetDisposals");
        migrationBuilder.DropColumn("RemainingAcquisitionCostAfterDisposal", "AssetDisposals");
        migrationBuilder.DropColumn("RemainingAccumulatedDepreciationAfterDisposal", "AssetDisposals");
        migrationBuilder.DropColumn("RemainingNetBookValueAfterDisposal", "AssetDisposals");
    }
}
