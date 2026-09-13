using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHrPayrollOracleParameterFormFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FemaleRetireAge",
                table: "PayrollParameterSets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaveClassification",
                table: "PayrollParameterSets",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaleRetireAge",
                table: "PayrollParameterSets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PictureDirectory",
                table: "PayrollParameterSets",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeSheetMode",
                table: "PayrollParameterSets",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAllowanceTaxCeiling",
                table: "PayrollParameterSets",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FemaleRetireAge",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "LeaveClassification",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "MaleRetireAge",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "PictureDirectory",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "TimeSheetMode",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "TotalAllowanceTaxCeiling",
                table: "PayrollParameterSets");
        }
    }
}
