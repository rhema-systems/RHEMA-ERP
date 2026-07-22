using System;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260514154500_AddHrPayrollBudgetAnalysisRows")]
    public partial class AddHrPayrollBudgetAnalysisRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollBudgetAnalysisRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderField = table.Column<int>(type: "int", nullable: false),
                    PayPeriod = table.Column<int>(type: "int", nullable: false),
                    PayPeriodFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PayPeriodTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    ActualTransaction = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Percentage = table.Column<bool>(type: "bit", nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Amount1 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Include1 = table.Column<bool>(type: "bit", nullable: false),
                    NewAmount1 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Amount2 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Include2 = table.Column<bool>(type: "bit", nullable: false),
                    NewAmount2 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Amount3 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Include3 = table.Column<bool>(type: "bit", nullable: false),
                    NewAmount3 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
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
                    table.PrimaryKey("PK_PayrollBudgetAnalysisRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollBudgetAnalysisRows_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBudgetAnalysisRows_TenantId",
                table: "PayrollBudgetAnalysisRows",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBudgetAnalysisRows_TenantId_CompanyCode_PayPeriod",
                table: "PayrollBudgetAnalysisRows",
                columns: new[] { "TenantId", "CompanyCode", "PayPeriod" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBudgetAnalysisRows_TenantId_CompanyCode_PayPeriod_OrderField_TransactionType_ActualTransaction",
                table: "PayrollBudgetAnalysisRows",
                columns: new[] { "TenantId", "CompanyCode", "PayPeriod", "OrderField", "TransactionType", "ActualTransaction" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollBudgetAnalysisRows");
        }
    }
}
