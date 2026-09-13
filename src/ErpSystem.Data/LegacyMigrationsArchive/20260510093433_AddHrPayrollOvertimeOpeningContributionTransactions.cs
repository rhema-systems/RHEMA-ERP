using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHrPayrollOvertimeOpeningContributionTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "PayrollTimesheetSummaries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeName",
                table: "PayrollTimesheetSummaries",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmployeeNumber",
                table: "PayrollTimesheetSummaries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "PayrollTimesheetSummaries",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LegacyCompanyCode",
                table: "PayrollTimesheetSummaries",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyEmployeeId",
                table: "PayrollTimesheetSummaries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordSource",
                table: "PayrollTimesheetSummaries",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "SaturdayHours",
                table: "PayrollTimesheetSummaries",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SundayHours",
                table: "PayrollTimesheetSummaries",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "PayrollContributionOpeningBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmployeeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LegacyEmployeeId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ContributionCodeType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContributionCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ContributionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BalanceAsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_PayrollContributionOpeningBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollContributionOpeningBalances_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollContributionOpeningBalances_PayrollEmployeeProfiles_EmployeeProfileId",
                        column: x => x.EmployeeProfileId,
                        principalTable: "PayrollEmployeeProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollContributionOpeningBalances_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollContributionTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmployeeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LegacyEmployeeId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ContributionCodeType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContributionCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ContributionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_PayrollContributionTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollContributionTransactions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollContributionTransactions_PayrollEmployeeProfiles_EmployeeProfileId",
                        column: x => x.EmployeeProfileId,
                        principalTable: "PayrollEmployeeProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollContributionTransactions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollTimesheetSummaries_EmployeeId",
                table: "PayrollTimesheetSummaries",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollTimesheetSummaries_TenantId_EmployeeNumber_PayPeriod",
                table: "PayrollTimesheetSummaries",
                columns: new[] { "TenantId", "EmployeeNumber", "PayPeriod" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollTimesheetSummaries_TenantId_IsActive_PayPeriod",
                table: "PayrollTimesheetSummaries",
                columns: new[] { "TenantId", "IsActive", "PayPeriod" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollContributionOpeningBalances_EmployeeId",
                table: "PayrollContributionOpeningBalances",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollContributionOpeningBalances_EmployeeProfileId",
                table: "PayrollContributionOpeningBalances",
                column: "EmployeeProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollContributionOpeningBalances_TenantId_ContributionCodeType_ContributionCode_EmployeeProfileId",
                table: "PayrollContributionOpeningBalances",
                columns: new[] { "TenantId", "ContributionCodeType", "ContributionCode", "EmployeeProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollContributionOpeningBalances_TenantId_ContributionCodeType_ContributionCode_IsActive",
                table: "PayrollContributionOpeningBalances",
                columns: new[] { "TenantId", "ContributionCodeType", "ContributionCode", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollContributionOpeningBalances_TenantId_EmployeeNumber",
                table: "PayrollContributionOpeningBalances",
                columns: new[] { "TenantId", "EmployeeNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollContributionTransactions_EmployeeId",
                table: "PayrollContributionTransactions",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollContributionTransactions_EmployeeProfileId",
                table: "PayrollContributionTransactions",
                column: "EmployeeProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollContributionTransactions_TenantId_ContributionCodeType_ContributionCode_TransactionType_EffectiveDate",
                table: "PayrollContributionTransactions",
                columns: new[] { "TenantId", "ContributionCodeType", "ContributionCode", "TransactionType", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollContributionTransactions_TenantId_EmployeeProfileId_ContributionCode_EffectiveDate",
                table: "PayrollContributionTransactions",
                columns: new[] { "TenantId", "EmployeeProfileId", "ContributionCode", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollContributionTransactions_TenantId_IsActive_EffectiveDate",
                table: "PayrollContributionTransactions",
                columns: new[] { "TenantId", "IsActive", "EffectiveDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollTimesheetSummaries_Employees_EmployeeId",
                table: "PayrollTimesheetSummaries",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayrollTimesheetSummaries_Employees_EmployeeId",
                table: "PayrollTimesheetSummaries");

            migrationBuilder.DropTable(
                name: "PayrollContributionOpeningBalances");

            migrationBuilder.DropTable(
                name: "PayrollContributionTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PayrollTimesheetSummaries_EmployeeId",
                table: "PayrollTimesheetSummaries");

            migrationBuilder.DropIndex(
                name: "IX_PayrollTimesheetSummaries_TenantId_EmployeeNumber_PayPeriod",
                table: "PayrollTimesheetSummaries");

            migrationBuilder.DropIndex(
                name: "IX_PayrollTimesheetSummaries_TenantId_IsActive_PayPeriod",
                table: "PayrollTimesheetSummaries");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "PayrollTimesheetSummaries");

            migrationBuilder.DropColumn(
                name: "EmployeeName",
                table: "PayrollTimesheetSummaries");

            migrationBuilder.DropColumn(
                name: "EmployeeNumber",
                table: "PayrollTimesheetSummaries");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "PayrollTimesheetSummaries");

            migrationBuilder.DropColumn(
                name: "LegacyCompanyCode",
                table: "PayrollTimesheetSummaries");

            migrationBuilder.DropColumn(
                name: "LegacyEmployeeId",
                table: "PayrollTimesheetSummaries");

            migrationBuilder.DropColumn(
                name: "RecordSource",
                table: "PayrollTimesheetSummaries");

            migrationBuilder.DropColumn(
                name: "SaturdayHours",
                table: "PayrollTimesheetSummaries");

            migrationBuilder.DropColumn(
                name: "SundayHours",
                table: "PayrollTimesheetSummaries");
        }
    }
}
