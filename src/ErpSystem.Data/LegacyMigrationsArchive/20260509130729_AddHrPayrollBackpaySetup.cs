using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHrPayrollBackpaySetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollBackpayExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EmployeeProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EmployeeName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CalculationType = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Applicable = table.Column<bool>(type: "bit", nullable: false),
                    LegacyCompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
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
                    table.PrimaryKey("PK_PayrollBackpayExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollBackpayExceptions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollBackpayPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CalculationType = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NumberOfMonths = table.Column<int>(type: "int", nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApplyTax = table.Column<bool>(type: "bit", nullable: false),
                    ApplySsf = table.Column<bool>(type: "bit", nullable: false),
                    MinimumServiceMode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    MinimumServiceValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MinimumServiceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CategoryType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LegacyCompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
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
                    table.PrimaryKey("PK_PayrollBackpayPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollBackpayPolicies_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollBackpayRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CategoryType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CategoryCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CategoryName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CalculationType = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Applicable = table.Column<bool>(type: "bit", nullable: false),
                    LegacyCompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
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
                    table.PrimaryKey("PK_PayrollBackpayRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollBackpayRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBackpayExceptions_TenantId_OperationType",
                table: "PayrollBackpayExceptions",
                columns: new[] { "TenantId", "OperationType" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBackpayExceptions_TenantId_OperationType_EmployeeNumber",
                table: "PayrollBackpayExceptions",
                columns: new[] { "TenantId", "OperationType", "EmployeeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBackpayPolicies_TenantId_OperationType",
                table: "PayrollBackpayPolicies",
                columns: new[] { "TenantId", "OperationType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBackpayRules_TenantId_OperationType_CategoryType",
                table: "PayrollBackpayRules",
                columns: new[] { "TenantId", "OperationType", "CategoryType" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBackpayRules_TenantId_OperationType_CategoryType_CategoryCode",
                table: "PayrollBackpayRules",
                columns: new[] { "TenantId", "OperationType", "CategoryType", "CategoryCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollBackpayExceptions");

            migrationBuilder.DropTable(
                name: "PayrollBackpayPolicies");

            migrationBuilder.DropTable(
                name: "PayrollBackpayRules");
        }
    }
}
