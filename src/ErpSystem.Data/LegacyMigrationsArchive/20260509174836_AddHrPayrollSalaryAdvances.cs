using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHrPayrollSalaryAdvances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollSalaryAdvances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmployeeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AdvanceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdvanceAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LegacyCompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollSalaryAdvances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollSalaryAdvances_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollSalaryAdvances_PayrollEmployeeProfiles_EmployeeProfileId",
                        column: x => x.EmployeeProfileId,
                        principalTable: "PayrollEmployeeProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollSalaryAdvances_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollSalaryAdvances_EmployeeId",
                table: "PayrollSalaryAdvances",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollSalaryAdvances_EmployeeProfileId",
                table: "PayrollSalaryAdvances",
                column: "EmployeeProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollSalaryAdvances_TenantId_EmployeeNumber_AdvanceDate",
                table: "PayrollSalaryAdvances",
                columns: new[] { "TenantId", "EmployeeNumber", "AdvanceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollSalaryAdvances_TenantId_EmployeeProfileId_AdvanceDate",
                table: "PayrollSalaryAdvances",
                columns: new[] { "TenantId", "EmployeeProfileId", "AdvanceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollSalaryAdvances_TenantId_IsActive_AdvanceDate",
                table: "PayrollSalaryAdvances",
                columns: new[] { "TenantId", "IsActive", "AdvanceDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollSalaryAdvances");
        }
    }
}
