using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectPhaseTemplateCompletionWeight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SlackMonths",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CompletionWeightPercent",
                table: "ProjectPhaseTemplates",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CompletionWeightPercent",
                table: "ProjectPhases",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CompletionWeightPercent",
                table: "ProjectPackages",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlannedEndDate",
                table: "ProjectPackages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlannedStartDate",
                table: "ProjectPackages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectMilestonePhases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectMilestoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectPhaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_ProjectMilestonePhases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectMilestonePhases_ProjectMilestones_ProjectMilestoneId",
                        column: x => x.ProjectMilestoneId,
                        principalTable: "ProjectMilestones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectMilestonePhases_ProjectPhases_ProjectPhaseId",
                        column: x => x.ProjectPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectMilestonePhases_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMilestonePhases_ProjectMilestoneId_ProjectPhaseId",
                table: "ProjectMilestonePhases",
                columns: new[] { "ProjectMilestoneId", "ProjectPhaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMilestonePhases_ProjectPhaseId",
                table: "ProjectMilestonePhases",
                column: "ProjectPhaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMilestonePhases_TenantId",
                table: "ProjectMilestonePhases",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectMilestonePhases");

            migrationBuilder.DropColumn(
                name: "SlackMonths",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "CompletionWeightPercent",
                table: "ProjectPhaseTemplates");

            migrationBuilder.DropColumn(
                name: "CompletionWeightPercent",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "CompletionWeightPercent",
                table: "ProjectPackages");

            migrationBuilder.DropColumn(
                name: "PlannedEndDate",
                table: "ProjectPackages");

            migrationBuilder.DropColumn(
                name: "PlannedStartDate",
                table: "ProjectPackages");
        }
    }
}
