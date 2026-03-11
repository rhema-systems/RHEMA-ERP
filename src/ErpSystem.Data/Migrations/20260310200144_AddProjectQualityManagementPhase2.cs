using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectQualityManagementPhase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectQualityCheckpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeliverableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QaOwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequiresQaSignOff = table.Column<bool>(type: "bit", nullable: false),
                    SignedOffAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedOffById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SignOffNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectQualityCheckpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectQualityCheckpoints_ProjectDeliverables_DeliverableId",
                        column: x => x.DeliverableId,
                        principalTable: "ProjectDeliverables",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectQualityCheckpoints_ProjectWorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "ProjectWorkItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectQualityCheckpoints_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectQualityCheckpoints_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectNonConformances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QualityCheckpointId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeliverableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TargetResolutionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CorrectiveAction = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PreventiveAction = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectNonConformances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectNonConformances_ProjectDeliverables_DeliverableId",
                        column: x => x.DeliverableId,
                        principalTable: "ProjectDeliverables",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectNonConformances_ProjectQualityCheckpoints_QualityCheckpointId",
                        column: x => x.QualityCheckpointId,
                        principalTable: "ProjectQualityCheckpoints",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectNonConformances_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectNonConformances_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectNonConformances_DeliverableId",
                table: "ProjectNonConformances",
                column: "DeliverableId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectNonConformances_ProjectId",
                table: "ProjectNonConformances",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectNonConformances_QualityCheckpointId",
                table: "ProjectNonConformances",
                column: "QualityCheckpointId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectNonConformances_TenantId",
                table: "ProjectNonConformances",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectQualityCheckpoints_DeliverableId",
                table: "ProjectQualityCheckpoints",
                column: "DeliverableId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectQualityCheckpoints_ProjectId",
                table: "ProjectQualityCheckpoints",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectQualityCheckpoints_TenantId",
                table: "ProjectQualityCheckpoints",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectQualityCheckpoints_WorkItemId",
                table: "ProjectQualityCheckpoints",
                column: "WorkItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectNonConformances");

            migrationBuilder.DropTable(
                name: "ProjectQualityCheckpoints");
        }
    }
}
