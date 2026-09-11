using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeSalaryChangeRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Round 3, lane S (D-1). ⚠ The scaffold wrote `false`; the entity initialiser says true and
            // the policy is ON by default for every tenant that already has a settings row.
            migrationBuilder.AddColumn<bool>(
                name: "SalaryChangeRequiresApproval",
                table: "CompanyHrPolicySettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "EmployeeSalaryChangeRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentPayBasis = table.Column<int>(type: "int", nullable: false),
                    CurrentGradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentNotchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ProposedPayBasis = table.Column<int>(type: "int", nullable: true),
                    ProposedGradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProposedLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProposedNotchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProposedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ProposedCurrencyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceProposalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    HrAppliedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppliedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppliedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppliedPlacementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApplyFailure = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_EmployeeSalaryChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeSalaryChangeRequests_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeSalaryChangeRequests_Employees_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeSalaryChangeRequests_SalaryGrades_ProposedGradeId",
                        column: x => x.ProposedGradeId,
                        principalTable: "SalaryGrades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeSalaryChangeRequests_SalaryLevels_ProposedLevelId",
                        column: x => x.ProposedLevelId,
                        principalTable: "SalaryLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeSalaryChangeRequests_SalaryNotches_ProposedNotchId",
                        column: x => x.ProposedNotchId,
                        principalTable: "SalaryNotches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeSalaryChangeRequests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ⚠ Scaffolded as UpdateData, which needs the migration's model to infer column types — and the

            // fast Debug build strips migration models, so applying failed with "no entity type mapped to

            // the table". Plain SQL, idempotent; the DEFAULT above already covers every existing row.

            migrationBuilder.Sql("UPDATE [CompanyHrPolicySettings] SET [SalaryChangeRequiresApproval] = 1 WHERE [Id] = 'b2c3d4e5-0000-0000-0000-000000000001';");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaryChangeRequests_EmployeeId",
                table: "EmployeeSalaryChangeRequests",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaryChangeRequests_EmployeeId_Status",
                table: "EmployeeSalaryChangeRequests",
                columns: new[] { "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaryChangeRequests_ProposedGradeId",
                table: "EmployeeSalaryChangeRequests",
                column: "ProposedGradeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaryChangeRequests_ProposedLevelId",
                table: "EmployeeSalaryChangeRequests",
                column: "ProposedLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaryChangeRequests_ProposedNotchId",
                table: "EmployeeSalaryChangeRequests",
                column: "ProposedNotchId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaryChangeRequests_RequestedById",
                table: "EmployeeSalaryChangeRequests",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaryChangeRequests_Status",
                table: "EmployeeSalaryChangeRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaryChangeRequests_TenantId",
                table: "EmployeeSalaryChangeRequests",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeSalaryChangeRequests");

            migrationBuilder.DropColumn(
                name: "SalaryChangeRequiresApproval",
                table: "CompanyHrPolicySettings");
        }
    }
}
