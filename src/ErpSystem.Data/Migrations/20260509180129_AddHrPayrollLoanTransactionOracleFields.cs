using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHrPayrollLoanTransactionOracleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GeneralRemarks",
                table: "PayrollLoans",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InterestRepaymentAmount",
                table: "PayrollLoans",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "LoanTypeCode",
                table: "PayrollLoans",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PeriodOfSuspension",
                table: "PayrollLoans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepaymentMode",
                table: "PayrollLoans",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Amount");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PayrollLoans",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspensionEndDate",
                table: "PayrollLoans",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspensionNarration",
                table: "PayrollLoans",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspensionStartDate",
                table: "PayrollLoans",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalInterest",
                table: "PayrollLoans",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLoans_TenantId_Status_PaymentStartDate",
                table: "PayrollLoans",
                columns: new[] { "TenantId", "Status", "PaymentStartDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollLoans_TenantId_Status_PaymentStartDate",
                table: "PayrollLoans");

            migrationBuilder.DropColumn(
                name: "GeneralRemarks",
                table: "PayrollLoans");

            migrationBuilder.DropColumn(
                name: "InterestRepaymentAmount",
                table: "PayrollLoans");

            migrationBuilder.DropColumn(
                name: "LoanTypeCode",
                table: "PayrollLoans");

            migrationBuilder.DropColumn(
                name: "PeriodOfSuspension",
                table: "PayrollLoans");

            migrationBuilder.DropColumn(
                name: "RepaymentMode",
                table: "PayrollLoans");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PayrollLoans");

            migrationBuilder.DropColumn(
                name: "SuspensionEndDate",
                table: "PayrollLoans");

            migrationBuilder.DropColumn(
                name: "SuspensionNarration",
                table: "PayrollLoans");

            migrationBuilder.DropColumn(
                name: "SuspensionStartDate",
                table: "PayrollLoans");

            migrationBuilder.DropColumn(
                name: "TotalInterest",
                table: "PayrollLoans");
        }
    }
}
