using System;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260509224500_AddHrPayrollComponentExceptionOverrides")]
    public partial class AddHrPayrollComponentExceptionOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrencyCodeOverride",
                table: "PayrollEmployeeComponents",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EmployerAmountOverride",
                table: "PayrollEmployeeComponents",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmployerTaxableOverride",
                table: "PayrollEmployeeComponents",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GrossUpOverride",
                table: "PayrollEmployeeComponents",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxFreeCeilingOverride",
                table: "PayrollEmployeeComponents",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TaxableOverride",
                table: "PayrollEmployeeComponents",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrencyCodeOverride",
                table: "PayrollEmployeeComponents");

            migrationBuilder.DropColumn(
                name: "EmployerAmountOverride",
                table: "PayrollEmployeeComponents");

            migrationBuilder.DropColumn(
                name: "EmployerTaxableOverride",
                table: "PayrollEmployeeComponents");

            migrationBuilder.DropColumn(
                name: "GrossUpOverride",
                table: "PayrollEmployeeComponents");

            migrationBuilder.DropColumn(
                name: "TaxFreeCeilingOverride",
                table: "PayrollEmployeeComponents");

            migrationBuilder.DropColumn(
                name: "TaxableOverride",
                table: "PayrollEmployeeComponents");
        }
    }
}
