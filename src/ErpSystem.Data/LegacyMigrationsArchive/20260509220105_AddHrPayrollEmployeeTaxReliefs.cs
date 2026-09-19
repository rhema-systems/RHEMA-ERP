using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHrPayrollEmployeeTaxReliefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollEmployeeTaxReliefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmployeeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReliefCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReliefName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CalculationType = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Factor = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_PayrollEmployeeTaxReliefs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollEmployeeTaxReliefs_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollEmployeeTaxReliefs_PayrollEmployeeProfiles_EmployeeProfileId",
                        column: x => x.EmployeeProfileId,
                        principalTable: "PayrollEmployeeProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollEmployeeTaxReliefs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployeeTaxReliefs_EmployeeId",
                table: "PayrollEmployeeTaxReliefs",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployeeTaxReliefs_EmployeeProfileId",
                table: "PayrollEmployeeTaxReliefs",
                column: "EmployeeProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployeeTaxReliefs_TenantId_EmployeeProfileId_IsActive",
                table: "PayrollEmployeeTaxReliefs",
                columns: new[] { "TenantId", "EmployeeProfileId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployeeTaxReliefs_TenantId_ReliefCode_EmployeeProfileId",
                table: "PayrollEmployeeTaxReliefs",
                columns: new[] { "TenantId", "ReliefCode", "EmployeeProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployeeTaxReliefs_TenantId_ReliefCode_IsActive",
                table: "PayrollEmployeeTaxReliefs",
                columns: new[] { "TenantId", "ReliefCode", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollEmployeeTaxReliefs");
        }
    }
}
