using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260829160000_AddQuantitySurveyEstimateSourceSnapshots")]
public partial class AddQuantitySurveyEstimateSourceSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "FundingSourceSnapshot",
            table: "QuantitySurveyEstimateVersions",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PropertyReferenceSnapshot",
            table: "QuantitySurveyEstimateVersions",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "SourceSnapshotSchemaVersion",
            table: "QuantitySurveyEstimateVersions",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddCheckConstraint(
            name: "CK_QsEstimateVersions_SourceSnapshotSchema",
            table: "QuantitySurveyEstimateVersions",
            sql: "[SourceSnapshotSchemaVersion] IN (0,1)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_QsEstimateVersions_SourceSnapshotSchema",
            table: "QuantitySurveyEstimateVersions");

        migrationBuilder.DropColumn(name: "FundingSourceSnapshot", table: "QuantitySurveyEstimateVersions");
        migrationBuilder.DropColumn(name: "PropertyReferenceSnapshot", table: "QuantitySurveyEstimateVersions");
        migrationBuilder.DropColumn(name: "SourceSnapshotSchemaVersion", table: "QuantitySurveyEstimateVersions");
    }
}
