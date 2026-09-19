using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260808023000_ExtendProjectBoqQuantitySurveyClassifications")]
public partial class ExtendProjectBoqQuantitySurveyClassifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "DefaultUnitOfMeasure", table: "ProjectCatalogEntries", type: "nvarchar(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "EffectiveFrom", table: "ProjectCatalogEntries", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "EffectiveTo", table: "ProjectCatalogEntries", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<string>(name: "MeasurementRule", table: "ProjectCatalogEntries", type: "nvarchar(2000)", maxLength: 2000, nullable: true);
        migrationBuilder.AddColumn<string>(name: "StandardCode", table: "ProjectCatalogEntries", type: "nvarchar(30)", maxLength: 30, nullable: true);

        migrationBuilder.AddColumn<Guid>(name: "SectionCatalogEntryId", table: "ProjectBoqItems", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>(name: "SectionCode", table: "ProjectBoqItems", type: "nvarchar(50)", maxLength: 50, nullable: true);
        migrationBuilder.AddColumn<string>(name: "SectionName", table: "ProjectBoqItems", type: "nvarchar(150)", maxLength: 150, nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "TradeCatalogEntryId", table: "ProjectBoqItems", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>(name: "TradeCode", table: "ProjectBoqItems", type: "nvarchar(50)", maxLength: 50, nullable: true);
        migrationBuilder.AddColumn<string>(name: "TradeName", table: "ProjectBoqItems", type: "nvarchar(150)", maxLength: 150, nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "CostCodeCatalogEntryId", table: "ProjectBoqItems", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>(name: "CostCode", table: "ProjectBoqItems", type: "nvarchar(50)", maxLength: 50, nullable: true);
        migrationBuilder.AddColumn<string>(name: "CostCodeName", table: "ProjectBoqItems", type: "nvarchar(150)", maxLength: 150, nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "MeasurementCodeCatalogEntryId", table: "ProjectBoqItems", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>(name: "MeasurementCode", table: "ProjectBoqItems", type: "nvarchar(50)", maxLength: 50, nullable: true);
        migrationBuilder.AddColumn<string>(name: "MeasurementRule", table: "ProjectBoqItems", type: "nvarchar(2000)", maxLength: 2000, nullable: true);
        migrationBuilder.AddColumn<string>(name: "MeasurementStandard", table: "ProjectBoqItems", type: "nvarchar(30)", maxLength: 30, nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_ProjectCatalogEntries_EffectivePeriod",
            table: "ProjectCatalogEntries",
            sql: "[EffectiveTo] IS NULL OR [EffectiveFrom] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectCatalogEntries_TenantId_CatalogType_EffectiveFrom_EffectiveTo",
            table: "ProjectCatalogEntries",
            columns: new[] { "TenantId", "CatalogType", "EffectiveFrom", "EffectiveTo" });
        migrationBuilder.CreateIndex(name: "IX_ProjectBoqItems_TenantId_SectionCatalogEntryId", table: "ProjectBoqItems", columns: new[] { "TenantId", "SectionCatalogEntryId" });
        migrationBuilder.CreateIndex(name: "IX_ProjectBoqItems_TenantId_TradeCatalogEntryId", table: "ProjectBoqItems", columns: new[] { "TenantId", "TradeCatalogEntryId" });
        migrationBuilder.CreateIndex(name: "IX_ProjectBoqItems_TenantId_CostCodeCatalogEntryId", table: "ProjectBoqItems", columns: new[] { "TenantId", "CostCodeCatalogEntryId" });
        migrationBuilder.CreateIndex(name: "IX_ProjectBoqItems_TenantId_MeasurementCodeCatalogEntryId", table: "ProjectBoqItems", columns: new[] { "TenantId", "MeasurementCodeCatalogEntryId" });

        migrationBuilder.AddForeignKey(name: "FK_ProjectBoqItems_ProjectCatalogEntries_SectionCatalogEntryId", table: "ProjectBoqItems", column: "SectionCatalogEntryId", principalTable: "ProjectCatalogEntries", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_ProjectBoqItems_ProjectCatalogEntries_TradeCatalogEntryId", table: "ProjectBoqItems", column: "TradeCatalogEntryId", principalTable: "ProjectCatalogEntries", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_ProjectBoqItems_ProjectCatalogEntries_CostCodeCatalogEntryId", table: "ProjectBoqItems", column: "CostCodeCatalogEntryId", principalTable: "ProjectCatalogEntries", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_ProjectBoqItems_ProjectCatalogEntries_MeasurementCodeCatalogEntryId", table: "ProjectBoqItems", column: "MeasurementCodeCatalogEntryId", principalTable: "ProjectCatalogEntries", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_ProjectBoqItems_ProjectCatalogEntries_SectionCatalogEntryId", table: "ProjectBoqItems");
        migrationBuilder.DropForeignKey(name: "FK_ProjectBoqItems_ProjectCatalogEntries_TradeCatalogEntryId", table: "ProjectBoqItems");
        migrationBuilder.DropForeignKey(name: "FK_ProjectBoqItems_ProjectCatalogEntries_CostCodeCatalogEntryId", table: "ProjectBoqItems");
        migrationBuilder.DropForeignKey(name: "FK_ProjectBoqItems_ProjectCatalogEntries_MeasurementCodeCatalogEntryId", table: "ProjectBoqItems");

        migrationBuilder.DropIndex(name: "IX_ProjectCatalogEntries_TenantId_CatalogType_EffectiveFrom_EffectiveTo", table: "ProjectCatalogEntries");
        migrationBuilder.DropIndex(name: "IX_ProjectBoqItems_TenantId_SectionCatalogEntryId", table: "ProjectBoqItems");
        migrationBuilder.DropIndex(name: "IX_ProjectBoqItems_TenantId_TradeCatalogEntryId", table: "ProjectBoqItems");
        migrationBuilder.DropIndex(name: "IX_ProjectBoqItems_TenantId_CostCodeCatalogEntryId", table: "ProjectBoqItems");
        migrationBuilder.DropIndex(name: "IX_ProjectBoqItems_TenantId_MeasurementCodeCatalogEntryId", table: "ProjectBoqItems");
        migrationBuilder.DropCheckConstraint(name: "CK_ProjectCatalogEntries_EffectivePeriod", table: "ProjectCatalogEntries");

        migrationBuilder.DropColumn(name: "DefaultUnitOfMeasure", table: "ProjectCatalogEntries");
        migrationBuilder.DropColumn(name: "EffectiveFrom", table: "ProjectCatalogEntries");
        migrationBuilder.DropColumn(name: "EffectiveTo", table: "ProjectCatalogEntries");
        migrationBuilder.DropColumn(name: "MeasurementRule", table: "ProjectCatalogEntries");
        migrationBuilder.DropColumn(name: "StandardCode", table: "ProjectCatalogEntries");

        migrationBuilder.DropColumn(name: "SectionCatalogEntryId", table: "ProjectBoqItems");
        migrationBuilder.DropColumn(name: "SectionCode", table: "ProjectBoqItems");
        migrationBuilder.DropColumn(name: "SectionName", table: "ProjectBoqItems");
        migrationBuilder.DropColumn(name: "TradeCatalogEntryId", table: "ProjectBoqItems");
        migrationBuilder.DropColumn(name: "TradeCode", table: "ProjectBoqItems");
        migrationBuilder.DropColumn(name: "TradeName", table: "ProjectBoqItems");
        migrationBuilder.DropColumn(name: "CostCodeCatalogEntryId", table: "ProjectBoqItems");
        migrationBuilder.DropColumn(name: "CostCode", table: "ProjectBoqItems");
        migrationBuilder.DropColumn(name: "CostCodeName", table: "ProjectBoqItems");
        migrationBuilder.DropColumn(name: "MeasurementCodeCatalogEntryId", table: "ProjectBoqItems");
        migrationBuilder.DropColumn(name: "MeasurementCode", table: "ProjectBoqItems");
        migrationBuilder.DropColumn(name: "MeasurementRule", table: "ProjectBoqItems");
        migrationBuilder.DropColumn(name: "MeasurementStandard", table: "ProjectBoqItems");
    }
}
