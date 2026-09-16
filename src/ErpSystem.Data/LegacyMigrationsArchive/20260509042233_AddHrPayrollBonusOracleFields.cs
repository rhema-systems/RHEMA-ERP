using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHrPayrollBonusOracleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AfterTaxContribution",
                table: "PayrollComponents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "PayrollBonusPolicies",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cycle",
                table: "PayrollBonusPolicies",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastPayPeriod",
                table: "PayrollBonusPolicies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumMonths",
                table: "PayrollBonusPolicies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NextPayPeriod",
                table: "PayrollBonusPolicies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaySeparate",
                table: "PayrollBonusPolicies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PerAnnual",
                table: "PayrollBonusPolicies",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AfterTaxContribution",
                table: "PayrollComponents");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "PayrollBonusPolicies");

            migrationBuilder.DropColumn(
                name: "Cycle",
                table: "PayrollBonusPolicies");

            migrationBuilder.DropColumn(
                name: "LastPayPeriod",
                table: "PayrollBonusPolicies");

            migrationBuilder.DropColumn(
                name: "MinimumMonths",
                table: "PayrollBonusPolicies");

            migrationBuilder.DropColumn(
                name: "NextPayPeriod",
                table: "PayrollBonusPolicies");

            migrationBuilder.DropColumn(
                name: "PaySeparate",
                table: "PayrollBonusPolicies");

            migrationBuilder.DropColumn(
                name: "PerAnnual",
                table: "PayrollBonusPolicies");
        }
    }
}
