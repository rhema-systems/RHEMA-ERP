using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHrPayrollLegacySetupNextSlices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollOvertimePolicies_TenantId_Code",
                table: "PayrollOvertimePolicies");

            migrationBuilder.DropIndex(
                name: "IX_PayrollLoanPolicies_TenantId_Code",
                table: "PayrollLoanPolicies");

            migrationBuilder.AddColumn<decimal>(
                name: "LeaveRecallRate",
                table: "PayrollOvertimePolicies",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyCompanyCode",
                table: "PayrollOvertimePolicies",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxOvertimeAmount",
                table: "PayrollOvertimePolicies",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MaxOvertimeIsPercent",
                table: "PayrollOvertimePolicies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxOvertimeSeparateTax",
                table: "PayrollOvertimePolicies",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaxOvertimeType",
                table: "PayrollOvertimePolicies",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumBasicForSeparateTax",
                table: "PayrollOvertimePolicies",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumSeparateOvertimePercent",
                table: "PayrollOvertimePolicies",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NormalHours",
                table: "PayrollOvertimePolicies",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayPeriod",
                table: "PayrollOvertimePolicies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PayPeriodFrom",
                table: "PayrollOvertimePolicies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PayPeriodTo",
                table: "PayrollOvertimePolicies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ApplyInterest",
                table: "PayrollLoanPolicies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LegacyCompanyCode",
                table: "PayrollLoanPolicies",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PayrollBankBranches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchSetupCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    BankCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    BranchCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    BranchDescription = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SortCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AccountCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
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
                    table.PrimaryKey("PK_PayrollBankBranches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollBankBranches_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollLeaveSetups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CategoryDetail = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
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
                    table.PrimaryKey("PK_PayrollLeaveSetups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollLeaveSetups_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollLegacyMenuUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UserGroup = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    UserNo = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    LegacyPasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LoginEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Locked = table.Column<bool>(type: "bit", nullable: false),
                    PasswordChangeRequired = table.Column<bool>(type: "bit", nullable: false),
                    PasswordExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PasswordFailureCount = table.Column<int>(type: "int", nullable: false),
                    CompanyId = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    BusinessUnitId = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    LegacyCompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    CanViewSalary = table.Column<bool>(type: "bit", nullable: false),
                    CanApprove = table.Column<bool>(type: "bit", nullable: false),
                    RefreshToken = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastPasswordChange = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK_PayrollLegacyMenuUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollLegacyMenuUsers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollOvertimeRanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollOvertimePolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MinRange = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    MaxRange = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_PayrollOvertimeRanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollOvertimeRanges_PayrollOvertimePolicies_PayrollOvertimePolicyId",
                        column: x => x.PayrollOvertimePolicyId,
                        principalTable: "PayrollOvertimePolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollOvertimeRanges_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollLeaveSetupDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollLeaveSetupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CategoryDetail = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Days = table.Column<int>(type: "int", nullable: true),
                    ServiceFrom = table.Column<int>(type: "int", nullable: true),
                    ServiceTo = table.Column<int>(type: "int", nullable: true),
                    SequenceNo = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_PayrollLeaveSetupDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollLeaveSetupDetails_PayrollLeaveSetups_PayrollLeaveSetupId",
                        column: x => x.PayrollLeaveSetupId,
                        principalTable: "PayrollLeaveSetups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollLeaveSetupDetails_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollOvertimePolicies_TenantId_Code_LegacyCompanyCode",
                table: "PayrollOvertimePolicies",
                columns: new[] { "TenantId", "Code", "LegacyCompanyCode" },
                unique: true,
                filter: "[LegacyCompanyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLoanPolicies_TenantId_Code_LegacyCompanyCode",
                table: "PayrollLoanPolicies",
                columns: new[] { "TenantId", "Code", "LegacyCompanyCode" },
                unique: true,
                filter: "[LegacyCompanyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBankBranches_TenantId_BankCode",
                table: "PayrollBankBranches",
                columns: new[] { "TenantId", "BankCode" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBankBranches_TenantId_BankCode_BranchCode_LegacyCompanyCode",
                table: "PayrollBankBranches",
                columns: new[] { "TenantId", "BankCode", "BranchCode", "LegacyCompanyCode" },
                unique: true,
                filter: "[LegacyCompanyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLeaveSetupDetails_PayrollLeaveSetupId",
                table: "PayrollLeaveSetupDetails",
                column: "PayrollLeaveSetupId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLeaveSetupDetails_TenantId_Category_CategoryDetail_LegacyCompanyCode",
                table: "PayrollLeaveSetupDetails",
                columns: new[] { "TenantId", "Category", "CategoryDetail", "LegacyCompanyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLeaveSetupDetails_TenantId_PayrollLeaveSetupId_SequenceNo",
                table: "PayrollLeaveSetupDetails",
                columns: new[] { "TenantId", "PayrollLeaveSetupId", "SequenceNo" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLeaveSetups_TenantId_Category_CategoryDetail_LegacyCompanyCode",
                table: "PayrollLeaveSetups",
                columns: new[] { "TenantId", "Category", "CategoryDetail", "LegacyCompanyCode" },
                unique: true,
                filter: "[LegacyCompanyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLegacyMenuUsers_TenantId_LoginEnabled_Locked",
                table: "PayrollLegacyMenuUsers",
                columns: new[] { "TenantId", "LoginEnabled", "Locked" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLegacyMenuUsers_TenantId_UserGroup",
                table: "PayrollLegacyMenuUsers",
                columns: new[] { "TenantId", "UserGroup" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLegacyMenuUsers_TenantId_UserName_LegacyCompanyCode",
                table: "PayrollLegacyMenuUsers",
                columns: new[] { "TenantId", "UserName", "LegacyCompanyCode" },
                unique: true,
                filter: "[LegacyCompanyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollOvertimeRanges_PayrollOvertimePolicyId",
                table: "PayrollOvertimeRanges",
                column: "PayrollOvertimePolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollOvertimeRanges_TenantId_PayrollOvertimePolicyId_MinRange_MaxRange",
                table: "PayrollOvertimeRanges",
                columns: new[] { "TenantId", "PayrollOvertimePolicyId", "MinRange", "MaxRange" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollBankBranches");

            migrationBuilder.DropTable(
                name: "PayrollLeaveSetupDetails");

            migrationBuilder.DropTable(
                name: "PayrollLegacyMenuUsers");

            migrationBuilder.DropTable(
                name: "PayrollOvertimeRanges");

            migrationBuilder.DropTable(
                name: "PayrollLeaveSetups");

            migrationBuilder.DropIndex(
                name: "IX_PayrollOvertimePolicies_TenantId_Code_LegacyCompanyCode",
                table: "PayrollOvertimePolicies");

            migrationBuilder.DropIndex(
                name: "IX_PayrollLoanPolicies_TenantId_Code_LegacyCompanyCode",
                table: "PayrollLoanPolicies");

            migrationBuilder.DropColumn(
                name: "LeaveRecallRate",
                table: "PayrollOvertimePolicies");

            migrationBuilder.DropColumn(
                name: "LegacyCompanyCode",
                table: "PayrollOvertimePolicies");

            migrationBuilder.DropColumn(
                name: "MaxOvertimeAmount",
                table: "PayrollOvertimePolicies");

            migrationBuilder.DropColumn(
                name: "MaxOvertimeIsPercent",
                table: "PayrollOvertimePolicies");

            migrationBuilder.DropColumn(
                name: "MaxOvertimeSeparateTax",
                table: "PayrollOvertimePolicies");

            migrationBuilder.DropColumn(
                name: "MaxOvertimeType",
                table: "PayrollOvertimePolicies");

            migrationBuilder.DropColumn(
                name: "MinimumBasicForSeparateTax",
                table: "PayrollOvertimePolicies");

            migrationBuilder.DropColumn(
                name: "MinimumSeparateOvertimePercent",
                table: "PayrollOvertimePolicies");

            migrationBuilder.DropColumn(
                name: "NormalHours",
                table: "PayrollOvertimePolicies");

            migrationBuilder.DropColumn(
                name: "PayPeriod",
                table: "PayrollOvertimePolicies");

            migrationBuilder.DropColumn(
                name: "PayPeriodFrom",
                table: "PayrollOvertimePolicies");

            migrationBuilder.DropColumn(
                name: "PayPeriodTo",
                table: "PayrollOvertimePolicies");

            migrationBuilder.DropColumn(
                name: "ApplyInterest",
                table: "PayrollLoanPolicies");

            migrationBuilder.DropColumn(
                name: "LegacyCompanyCode",
                table: "PayrollLoanPolicies");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollOvertimePolicies_TenantId_Code",
                table: "PayrollOvertimePolicies",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLoanPolicies_TenantId_Code",
                table: "PayrollLoanPolicies",
                columns: new[] { "TenantId", "Code" },
                unique: true);
        }
    }
}
