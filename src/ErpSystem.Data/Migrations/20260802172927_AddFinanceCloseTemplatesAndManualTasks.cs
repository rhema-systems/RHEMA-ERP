using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    // Debug builds intentionally omit generated migration designers to keep this very large
    // solution responsive. Keep discovery metadata on the executable migration class so EF can
    // still list and apply this Finance control change in normal developer builds.
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260802172927_AddFinanceCloseTemplatesAndManualTasks")]
    /// <inheritdoc />
    public partial class AddFinanceCloseTemplatesAndManualTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedToUserName",
                table: "FinanceCloseTasks",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CloseType",
                table: "FinanceCloseCycles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "MonthEnd");

            migrationBuilder.AddColumn<Guid>(
                name: "FinanceCloseTemplateId",
                table: "FinanceCloseCycles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateCode",
                table: "FinanceCloseCycles",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "TDC-MONTH-END");

            migrationBuilder.CreateTable(
                name: "FinanceCloseTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CloseType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsSystemDefault = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedByUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalDeclaration = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SupersededAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_FinanceCloseTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceCloseTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceCloseTemplateTaskDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinanceCloseTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DependsOnTaskCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    CheckCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    IsAutomated = table.Column<bool>(type: "bit", nullable: false),
                    DueDaysAfterPeriodEnd = table.Column<int>(type: "int", nullable: false),
                    DefaultAssigneeUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Instructions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_FinanceCloseTemplateTaskDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceCloseTemplateTaskDefinitions_FinanceCloseTemplates_FinanceCloseTemplateId",
                        column: x => x.FinanceCloseTemplateId,
                        principalTable: "FinanceCloseTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FinanceCloseTemplateTaskDefinitions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseCycles_FinanceCloseTemplateId",
                table: "FinanceCloseCycles",
                column: "FinanceCloseTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseTemplates_TenantId_CloseType_IsActive",
                table: "FinanceCloseTemplates",
                columns: new[] { "TenantId", "CloseType", "IsActive" },
                unique: true,
                filter: "[IsActive] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseTemplates_TenantId_TemplateCode_Version",
                table: "FinanceCloseTemplates",
                columns: new[] { "TenantId", "TemplateCode", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseTemplateTaskDefinitions_FinanceCloseTemplateId",
                table: "FinanceCloseTemplateTaskDefinitions",
                column: "FinanceCloseTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseTemplateTaskDefinitions_TenantId_FinanceCloseTemplateId_Sequence",
                table: "FinanceCloseTemplateTaskDefinitions",
                columns: new[] { "TenantId", "FinanceCloseTemplateId", "Sequence" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseTemplateTaskDefinitions_TenantId_FinanceCloseTemplateId_TaskCode",
                table: "FinanceCloseTemplateTaskDefinitions",
                columns: new[] { "TenantId", "FinanceCloseTemplateId", "TaskCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_FinanceCloseCycles_FinanceCloseTemplates_FinanceCloseTemplateId",
                table: "FinanceCloseCycles",
                column: "FinanceCloseTemplateId",
                principalTable: "FinanceCloseTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinanceCloseCycles_FinanceCloseTemplates_FinanceCloseTemplateId",
                table: "FinanceCloseCycles");

            migrationBuilder.DropTable(
                name: "FinanceCloseTemplateTaskDefinitions");

            migrationBuilder.DropTable(
                name: "FinanceCloseTemplates");

            migrationBuilder.DropIndex(
                name: "IX_FinanceCloseCycles_FinanceCloseTemplateId",
                table: "FinanceCloseCycles");

            migrationBuilder.DropColumn(
                name: "AssignedToUserName",
                table: "FinanceCloseTasks");

            migrationBuilder.DropColumn(
                name: "CloseType",
                table: "FinanceCloseCycles");

            migrationBuilder.DropColumn(
                name: "FinanceCloseTemplateId",
                table: "FinanceCloseCycles");

            migrationBuilder.DropColumn(
                name: "TemplateCode",
                table: "FinanceCloseCycles");
        }
    }
}
