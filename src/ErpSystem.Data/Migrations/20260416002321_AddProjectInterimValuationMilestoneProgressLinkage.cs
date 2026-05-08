using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectInterimValuationMilestoneProgressLinkage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectPackageId",
                table: "ProjectWorkItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectMilestoneId",
                table: "ProjectInterimValuations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArtifactId",
                table: "ProjectDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArtifactType",
                table: "ProjectDocuments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Project");

            migrationBuilder.CreateTable(
                name: "ProjectInterimValuationPackageCompletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectInterimValuationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectPackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_ProjectInterimValuationPackageCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectInterimValuationPackageCompletions_ProjectInterimValuations_ProjectInterimValuationId",
                        column: x => x.ProjectInterimValuationId,
                        principalTable: "ProjectInterimValuations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectInterimValuationPackageCompletions_ProjectPackages_ProjectPackageId",
                        column: x => x.ProjectPackageId,
                        principalTable: "ProjectPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectInterimValuationPackageCompletions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectWorkItems_ProjectId_ProjectPackageId",
                table: "ProjectWorkItems",
                columns: new[] { "ProjectId", "ProjectPackageId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectWorkItems_ProjectPackageId",
                table: "ProjectWorkItems",
                column: "ProjectPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInterimValuations_ProjectId_ProjectMilestoneId",
                table: "ProjectInterimValuations",
                columns: new[] { "ProjectId", "ProjectMilestoneId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInterimValuations_ProjectMilestoneId",
                table: "ProjectInterimValuations",
                column: "ProjectMilestoneId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDocuments_ProjectId_ArtifactType_ArtifactId",
                table: "ProjectDocuments",
                columns: new[] { "ProjectId", "ArtifactType", "ArtifactId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInterimValuationPackageCompletions_ProjectInterimValuationId",
                table: "ProjectInterimValuationPackageCompletions",
                column: "ProjectInterimValuationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInterimValuationPackageCompletions_ProjectInterimValuationId_ProjectPackageId",
                table: "ProjectInterimValuationPackageCompletions",
                columns: new[] { "ProjectInterimValuationId", "ProjectPackageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInterimValuationPackageCompletions_ProjectPackageId",
                table: "ProjectInterimValuationPackageCompletions",
                column: "ProjectPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInterimValuationPackageCompletions_TenantId",
                table: "ProjectInterimValuationPackageCompletions",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectInterimValuations_ProjectMilestones_ProjectMilestoneId",
                table: "ProjectInterimValuations",
                column: "ProjectMilestoneId",
                principalTable: "ProjectMilestones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectWorkItems_ProjectPackages_ProjectPackageId",
                table: "ProjectWorkItems",
                column: "ProjectPackageId",
                principalTable: "ProjectPackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectInterimValuations_ProjectMilestones_ProjectMilestoneId",
                table: "ProjectInterimValuations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectWorkItems_ProjectPackages_ProjectPackageId",
                table: "ProjectWorkItems");

            migrationBuilder.DropTable(
                name: "ProjectInterimValuationPackageCompletions");

            migrationBuilder.DropIndex(
                name: "IX_ProjectWorkItems_ProjectId_ProjectPackageId",
                table: "ProjectWorkItems");

            migrationBuilder.DropIndex(
                name: "IX_ProjectWorkItems_ProjectPackageId",
                table: "ProjectWorkItems");

            migrationBuilder.DropIndex(
                name: "IX_ProjectInterimValuations_ProjectId_ProjectMilestoneId",
                table: "ProjectInterimValuations");

            migrationBuilder.DropIndex(
                name: "IX_ProjectInterimValuations_ProjectMilestoneId",
                table: "ProjectInterimValuations");

            migrationBuilder.DropIndex(
                name: "IX_ProjectDocuments_ProjectId_ArtifactType_ArtifactId",
                table: "ProjectDocuments");

            migrationBuilder.DropColumn(
                name: "ProjectPackageId",
                table: "ProjectWorkItems");

            migrationBuilder.DropColumn(
                name: "ProjectMilestoneId",
                table: "ProjectInterimValuations");

            migrationBuilder.DropColumn(
                name: "ArtifactId",
                table: "ProjectDocuments");

            migrationBuilder.DropColumn(
                name: "ArtifactType",
                table: "ProjectDocuments");
        }
    }
}
