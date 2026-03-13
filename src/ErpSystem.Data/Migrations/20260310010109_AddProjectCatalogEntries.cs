using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectCatalogEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectCatalogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ProjectCatalogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectCatalogEntries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectClosures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FinalBudget = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    FinalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DeliverablesAccepted = table.Column<bool>(type: "bit", nullable: false),
                    TasksCompletedOrWaived = table.Column<bool>(type: "bit", nullable: false),
                    AssetsReconciled = table.Column<bool>(type: "bit", nullable: false),
                    OpenItemsDisposed = table.Column<bool>(type: "bit", nullable: false),
                    ClosureChecklistJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OpenItemsDisposition = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AssetReconciliationNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LessonsLearnedSummary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PostImplementationReview = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OverrideReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectClosures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectClosures_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectClosures_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DecisionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Rationale = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AlternativesConsidered = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ImpactSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ProjectDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectDecisions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectDecisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectLessonsLearned",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Recommendation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AppliedPhase = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Visibility = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
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
                    table.PrimaryKey("PK_ProjectLessonsLearned", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectLessonsLearned_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectLessonsLearned_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectMeetingMinutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MeetingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FacilitatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MeetingType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Minutes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AttendeesJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectMeetingMinutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectMeetingMinutes_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectMeetingMinutes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectActionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingMinuteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
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
                    table.PrimaryKey("PK_ProjectActionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectActionItems_ProjectMeetingMinutes_MeetingMinuteId",
                        column: x => x.MeetingMinuteId,
                        principalTable: "ProjectMeetingMinutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectActionItems_ProjectWorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "ProjectWorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectActionItems_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectActionItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectActionItems_MeetingMinuteId",
                table: "ProjectActionItems",
                column: "MeetingMinuteId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectActionItems_ProjectId_Status_DueDate",
                table: "ProjectActionItems",
                columns: new[] { "ProjectId", "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectActionItems_TenantId",
                table: "ProjectActionItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectActionItems_WorkItemId",
                table: "ProjectActionItems",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCatalogEntries_TenantId_CatalogType_Code",
                table: "ProjectCatalogEntries",
                columns: new[] { "TenantId", "CatalogType", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCatalogEntries_TenantId_CatalogType_IsActive",
                table: "ProjectCatalogEntries",
                columns: new[] { "TenantId", "CatalogType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCatalogEntries_TenantId_CatalogType_SortOrder",
                table: "ProjectCatalogEntries",
                columns: new[] { "TenantId", "CatalogType", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectClosures_ProjectId",
                table: "ProjectClosures",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectClosures_ProjectId_Status",
                table: "ProjectClosures",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectClosures_TenantId",
                table: "ProjectClosures",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDecisions_ProjectId_DecisionDate",
                table: "ProjectDecisions",
                columns: new[] { "ProjectId", "DecisionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDecisions_ProjectId_Status",
                table: "ProjectDecisions",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDecisions_TenantId",
                table: "ProjectDecisions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLessonsLearned_ProjectId_Category",
                table: "ProjectLessonsLearned",
                columns: new[] { "ProjectId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLessonsLearned_TenantId",
                table: "ProjectLessonsLearned",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMeetingMinutes_ProjectId_MeetingDate",
                table: "ProjectMeetingMinutes",
                columns: new[] { "ProjectId", "MeetingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMeetingMinutes_TenantId",
                table: "ProjectMeetingMinutes",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectActionItems");

            migrationBuilder.DropTable(
                name: "ProjectCatalogEntries");

            migrationBuilder.DropTable(
                name: "ProjectClosures");

            migrationBuilder.DropTable(
                name: "ProjectDecisions");

            migrationBuilder.DropTable(
                name: "ProjectLessonsLearned");

            migrationBuilder.DropTable(
                name: "ProjectMeetingMinutes");
        }
    }
}
