using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHrPayrollPromotionArrears : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollPromotionArrears",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmployeeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LegacyEmployeeId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PayPeriod = table.Column<int>(type: "int", nullable: false),
                    PayPeriodFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PayPeriodTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LegacyCompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    WorkingDays = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    BasicSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
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
                    table.PrimaryKey("PK_PayrollPromotionArrears", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollPromotionArrears_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollPromotionArrears_PayrollEmployeeProfiles_EmployeeProfileId",
                        column: x => x.EmployeeProfileId,
                        principalTable: "PayrollEmployeeProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollPromotionArrears_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPromotionArrears_EmployeeId",
                table: "PayrollPromotionArrears",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPromotionArrears_EmployeeProfileId",
                table: "PayrollPromotionArrears",
                column: "EmployeeProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPromotionArrears_TenantId_EmployeeNumber",
                table: "PayrollPromotionArrears",
                columns: new[] { "TenantId", "EmployeeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPromotionArrears_TenantId_EmployeeProfileId",
                table: "PayrollPromotionArrears",
                columns: new[] { "TenantId", "EmployeeProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPromotionArrears_TenantId_IsActive_EffectiveDate",
                table: "PayrollPromotionArrears",
                columns: new[] { "TenantId", "IsActive", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPromotionArrears_TenantId_PayPeriod_LegacyCompanyCode",
                table: "PayrollPromotionArrears",
                columns: new[] { "TenantId", "PayPeriod", "LegacyCompanyCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollPromotionArrears");
        }
    }
}
