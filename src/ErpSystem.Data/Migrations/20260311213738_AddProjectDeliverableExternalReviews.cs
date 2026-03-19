using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectDeliverableExternalReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectDeliverableExternalReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliverableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Decision = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StatusSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
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
                    table.PrimaryKey("PK_ProjectDeliverableExternalReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectDeliverableExternalReviews_ProjectDeliverables_DeliverableId",
                        column: x => x.DeliverableId,
                        principalTable: "ProjectDeliverables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectDeliverableExternalReviews_ProjectDocuments_SubmittedDocumentId",
                        column: x => x.SubmittedDocumentId,
                        principalTable: "ProjectDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectDeliverableExternalReviews_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectDeliverableExternalReviews_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeliverableExternalReviews_DeliverableId",
                table: "ProjectDeliverableExternalReviews",
                column: "DeliverableId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeliverableExternalReviews_ProjectId_Decision_StatusSnapshot",
                table: "ProjectDeliverableExternalReviews",
                columns: new[] { "ProjectId", "Decision", "StatusSnapshot" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeliverableExternalReviews_ProjectId_DeliverableId_ReviewDate",
                table: "ProjectDeliverableExternalReviews",
                columns: new[] { "ProjectId", "DeliverableId", "ReviewDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeliverableExternalReviews_SubmittedDocumentId",
                table: "ProjectDeliverableExternalReviews",
                column: "SubmittedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeliverableExternalReviews_TenantId",
                table: "ProjectDeliverableExternalReviews",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectDeliverableExternalReviews");
        }
    }
}
