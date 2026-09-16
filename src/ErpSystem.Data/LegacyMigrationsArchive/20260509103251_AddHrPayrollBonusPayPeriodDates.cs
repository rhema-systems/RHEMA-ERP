using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHrPayrollBonusPayPeriodDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastPayPeriodDate",
                table: "PayrollBonusPolicies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextPayPeriodDate",
                table: "PayrollBonusPolicies",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastPayPeriodDate",
                table: "PayrollBonusPolicies");

            migrationBuilder.DropColumn(
                name: "NextPayPeriodDate",
                table: "PayrollBonusPolicies");
        }
    }
}
