using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds the method assumptions and immutable calculation evidence required to resolve
/// FIN-LIM-0031. No historical rows are invented: existing development data remains
/// straight-line with zero method-specific values, while new diminishing-balance and
/// units-of-production assets must supply validated configuration before capitalization.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260809170000_AddStandardsAlignedDepreciationMethods")]
public sealed class AddStandardsAlignedDepreciationMethods : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "DefaultDiminishingBalanceRatePercent",
            table: "FixedAssetCategories",
            type: "decimal(18,4)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "DefaultLifetimeProductionCapacity",
            table: "FixedAssetCategories",
            type: "decimal(18,4)",
            nullable: false,
            defaultValue: 0m);

        AddMethodConfigurationColumns(migrationBuilder, "FixedAssets");
        AddMethodConfigurationColumns(migrationBuilder, "FixedAssetBookValues");

        migrationBuilder.AddColumn<decimal>(
            name: "DiminishingBalanceRatePercentSnapshot",
            table: "AssetDepreciationSchedules",
            type: "decimal(18,4)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "LifetimeProductionCapacitySnapshot",
            table: "AssetDepreciationSchedules",
            type: "decimal(18,4)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "PeriodProductionUnits",
            table: "AssetDepreciationSchedules",
            type: "decimal(18,4)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "CumulativeProductionUnitsBefore",
            table: "AssetDepreciationSchedules",
            type: "decimal(18,4)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "CumulativeProductionUnitsAfter",
            table: "AssetDepreciationSchedules",
            type: "decimal(18,4)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<string>(
            name: "ProductionEvidenceReference",
            table: "AssetDepreciationSchedules",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProductionEvidenceNotes",
            table: "AssetDepreciationSchedules",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("DefaultDiminishingBalanceRatePercent", "FixedAssetCategories");
        migrationBuilder.DropColumn("DefaultLifetimeProductionCapacity", "FixedAssetCategories");

        DropMethodConfigurationColumns(migrationBuilder, "FixedAssets");
        DropMethodConfigurationColumns(migrationBuilder, "FixedAssetBookValues");

        migrationBuilder.DropColumn("DiminishingBalanceRatePercentSnapshot", "AssetDepreciationSchedules");
        migrationBuilder.DropColumn("LifetimeProductionCapacitySnapshot", "AssetDepreciationSchedules");
        migrationBuilder.DropColumn("PeriodProductionUnits", "AssetDepreciationSchedules");
        migrationBuilder.DropColumn("CumulativeProductionUnitsBefore", "AssetDepreciationSchedules");
        migrationBuilder.DropColumn("CumulativeProductionUnitsAfter", "AssetDepreciationSchedules");
        migrationBuilder.DropColumn("ProductionEvidenceReference", "AssetDepreciationSchedules");
        migrationBuilder.DropColumn("ProductionEvidenceNotes", "AssetDepreciationSchedules");
    }

    private static void AddMethodConfigurationColumns(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "DiminishingBalanceRatePercent",
            table: table,
            type: "decimal(18,4)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "LifetimeProductionCapacity",
            table: table,
            type: "decimal(18,4)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "AccumulatedProductionUnits",
            table: table,
            type: "decimal(18,4)",
            nullable: false,
            defaultValue: 0m);
    }

    private static void DropMethodConfigurationColumns(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.DropColumn("DiminishingBalanceRatePercent", table);
        migrationBuilder.DropColumn("LifetimeProductionCapacity", table);
        migrationBuilder.DropColumn("AccumulatedProductionUnits", table);
    }
}
