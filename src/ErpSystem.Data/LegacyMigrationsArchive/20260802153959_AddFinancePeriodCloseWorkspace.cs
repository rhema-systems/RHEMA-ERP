using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    // Debug builds omit most generated Designer files. Keep discovery metadata on the main class
    // so the close workspace migration is visible without compiling all historical target models.
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260802153959_AddFinancePeriodCloseWorkspace")]
    public partial class AddFinancePeriodCloseWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequireDepreciationBeforePeriodClose",
                table: "FinanceSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "FinanceCloseCycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CycleNumber = table.Column<int>(type: "int", nullable: false),
                    TemplateVersion = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EvaluationCount = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartedByUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastEvaluatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PreparedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReopenedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReopenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReopenReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_FinanceCloseCycles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceCloseCycles_FiscalPeriods_FiscalPeriodId",
                        column: x => x.FiscalPeriodId,
                        principalTable: "FiscalPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceCloseCycles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceCloseCertifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinanceCloseCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreparedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreparedByUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PreparedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PreparerDeclaration = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedByUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewerDeclaration = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedByUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsSuperseded = table.Column<bool>(type: "bit", nullable: false),
                    SupersededAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupersededReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_FinanceCloseCertifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceCloseCertifications_FinanceCloseCycles_FinanceCloseCycleId",
                        column: x => x.FinanceCloseCycleId,
                        principalTable: "FinanceCloseCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FinanceCloseCertifications_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceCloseCheckSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinanceCloseCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluationNumber = table.Column<int>(type: "int", nullable: false),
                    CheckCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ResultSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ExceptionCount = table.Column<int>(type: "int", nullable: false),
                    ExceptionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EvaluatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EvaluatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EvaluatedByUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_FinanceCloseCheckSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceCloseCheckSnapshots_FinanceCloseCycles_FinanceCloseCycleId",
                        column: x => x.FinanceCloseCycleId,
                        principalTable: "FinanceCloseCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FinanceCloseCheckSnapshots_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceCloseTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinanceCloseCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DependsOnTaskCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    CheckCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    IsAutomated = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedByUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EvidenceSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_FinanceCloseTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceCloseTasks_FinanceCloseCycles_FinanceCloseCycleId",
                        column: x => x.FinanceCloseCycleId,
                        principalTable: "FinanceCloseCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FinanceCloseTasks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseCertifications_FinanceCloseCycleId",
                table: "FinanceCloseCertifications",
                column: "FinanceCloseCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseCertifications_TenantId_FinanceCloseCycleId",
                table: "FinanceCloseCertifications",
                columns: new[] { "TenantId", "FinanceCloseCycleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseCheckSnapshots_FinanceCloseCycleId",
                table: "FinanceCloseCheckSnapshots",
                column: "FinanceCloseCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseCheckSnapshots_TenantId_FinanceCloseCycleId_EvaluationNumber_CheckCode",
                table: "FinanceCloseCheckSnapshots",
                columns: new[] { "TenantId", "FinanceCloseCycleId", "EvaluationNumber", "CheckCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseCheckSnapshots_TenantId_Status_EvaluatedAt",
                table: "FinanceCloseCheckSnapshots",
                columns: new[] { "TenantId", "Status", "EvaluatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseCycles_FiscalPeriodId",
                table: "FinanceCloseCycles",
                column: "FiscalPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseCycles_TenantId_FiscalPeriodId_CycleNumber",
                table: "FinanceCloseCycles",
                columns: new[] { "TenantId", "FiscalPeriodId", "CycleNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseCycles_TenantId_Status_StartedAt",
                table: "FinanceCloseCycles",
                columns: new[] { "TenantId", "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseTasks_FinanceCloseCycleId",
                table: "FinanceCloseTasks",
                column: "FinanceCloseCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseTasks_TenantId_FinanceCloseCycleId_TaskCode",
                table: "FinanceCloseTasks",
                columns: new[] { "TenantId", "FinanceCloseCycleId", "TaskCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinanceCloseCertifications");

            migrationBuilder.DropTable(
                name: "FinanceCloseCheckSnapshots");

            migrationBuilder.DropTable(
                name: "FinanceCloseTasks");

            migrationBuilder.DropTable(
                name: "FinanceCloseCycles");

            migrationBuilder.DropColumn(
                name: "RequireDepreciationBeforePeriodClose",
                table: "FinanceSettings");
        }
    }
}
