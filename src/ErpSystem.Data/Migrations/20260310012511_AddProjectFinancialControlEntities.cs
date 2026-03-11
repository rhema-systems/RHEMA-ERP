using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectFinancialControlEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectBudgetRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    RevisionName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RevisionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EstimatedBudget = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ApprovedBudget = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CommittedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ForecastCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ThresholdWarningPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ThresholdCriticalPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectBudgetRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectBudgetRevisions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectBudgetRevisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectForecastVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    VersionName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AsOfDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ForecastCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EstimateAtCompletion = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ForecastRevenue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ForecastMargin = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectForecastVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectForecastVersions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectForecastVersions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBudgetRevisions_ProjectId_EffectiveDate",
                table: "ProjectBudgetRevisions",
                columns: new[] { "ProjectId", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBudgetRevisions_ProjectId_Status",
                table: "ProjectBudgetRevisions",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBudgetRevisions_ProjectId_VersionNumber",
                table: "ProjectBudgetRevisions",
                columns: new[] { "ProjectId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBudgetRevisions_TenantId",
                table: "ProjectBudgetRevisions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectForecastVersions_ProjectId_AsOfDate",
                table: "ProjectForecastVersions",
                columns: new[] { "ProjectId", "AsOfDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectForecastVersions_ProjectId_IsActive",
                table: "ProjectForecastVersions",
                columns: new[] { "ProjectId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectForecastVersions_ProjectId_VersionNumber",
                table: "ProjectForecastVersions",
                columns: new[] { "ProjectId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectForecastVersions_TenantId",
                table: "ProjectForecastVersions",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectBudgetRevisions");

            migrationBuilder.DropTable(
                name: "ProjectForecastVersions");
        }
    }
}
