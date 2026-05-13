using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHrPayrollComponentOracleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ApplyToBenefit",
                table: "PayrollComponents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "PayrollComponents",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cycle",
                table: "PayrollComponents",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EmployerAmount",
                table: "PayrollComponents",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPayDate",
                table: "PayrollComponents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxBenefitToTax",
                table: "PayrollComponents",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NextPayPeriod",
                table: "PayrollComponents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SeparateTaxPercent",
                table: "PayrollComponents",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxFreeCeiling",
                table: "PayrollComponents",
                type: "decimal(18,4)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplyToBenefit",
                table: "PayrollComponents");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "PayrollComponents");

            migrationBuilder.DropColumn(
                name: "Cycle",
                table: "PayrollComponents");

            migrationBuilder.DropColumn(
                name: "EmployerAmount",
                table: "PayrollComponents");

            migrationBuilder.DropColumn(
                name: "LastPayDate",
                table: "PayrollComponents");

            migrationBuilder.DropColumn(
                name: "MaxBenefitToTax",
                table: "PayrollComponents");

            migrationBuilder.DropColumn(
                name: "NextPayPeriod",
                table: "PayrollComponents");

            migrationBuilder.DropColumn(
                name: "SeparateTaxPercent",
                table: "PayrollComponents");

            migrationBuilder.DropColumn(
                name: "TaxFreeCeiling",
                table: "PayrollComponents");
        }
    }
}
