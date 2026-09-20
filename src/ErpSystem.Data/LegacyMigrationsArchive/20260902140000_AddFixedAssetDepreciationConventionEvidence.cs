using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260902140000_AddFixedAssetDepreciationConventionEvidence")]
public sealed class AddFixedAssetDepreciationConventionEvidence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "DepreciationConventionSnapshot", table: "AssetDepreciationSchedules", type: "int", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<decimal>(name: "ConventionFactor", table: "AssetDepreciationSchedules", type: "decimal(18,8)", nullable: false, defaultValue: 1m);
        migrationBuilder.AddColumn<string>(name: "ConventionBasis", table: "AssetDepreciationSchedules", type: "nvarchar(80)", maxLength: 80, nullable: false, defaultValue: "LegacyUnprorated");
        migrationBuilder.AddColumn<DateTime>(name: "ConventionEligibleFromDate", table: "AssetDepreciationSchedules", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "ConventionEligibleToDate", table: "AssetDepreciationSchedules", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "FiscalPeriodStartDateSnapshot", table: "AssetDepreciationSchedules", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "FiscalPeriodEndDateSnapshot", table: "AssetDepreciationSchedules", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "FiscalYearStartDateSnapshot", table: "AssetDepreciationSchedules", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "FiscalYearEndDateSnapshot", table: "AssetDepreciationSchedules", type: "datetime2", nullable: true);

        migrationBuilder.AddColumn<int>(name: "FinalDepreciationConventionSnapshot", table: "AssetDisposals", type: "int", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "FinalDepreciationConventionFactor", table: "AssetDisposals", type: "decimal(18,8)", nullable: false, defaultValue: 0m);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DepreciationConventionSnapshot", table: "AssetDepreciationSchedules");
        migrationBuilder.DropColumn(name: "ConventionFactor", table: "AssetDepreciationSchedules");
        migrationBuilder.DropColumn(name: "ConventionBasis", table: "AssetDepreciationSchedules");
        migrationBuilder.DropColumn(name: "ConventionEligibleFromDate", table: "AssetDepreciationSchedules");
        migrationBuilder.DropColumn(name: "ConventionEligibleToDate", table: "AssetDepreciationSchedules");
        migrationBuilder.DropColumn(name: "FiscalPeriodStartDateSnapshot", table: "AssetDepreciationSchedules");
        migrationBuilder.DropColumn(name: "FiscalPeriodEndDateSnapshot", table: "AssetDepreciationSchedules");
        migrationBuilder.DropColumn(name: "FiscalYearStartDateSnapshot", table: "AssetDepreciationSchedules");
        migrationBuilder.DropColumn(name: "FiscalYearEndDateSnapshot", table: "AssetDepreciationSchedules");
        migrationBuilder.DropColumn(name: "FinalDepreciationConventionSnapshot", table: "AssetDisposals");
        migrationBuilder.DropColumn(name: "FinalDepreciationConventionFactor", table: "AssetDisposals");
    }
}
