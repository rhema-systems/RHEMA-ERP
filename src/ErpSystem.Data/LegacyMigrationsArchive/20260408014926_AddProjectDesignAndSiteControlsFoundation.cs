using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectDesignAndSiteControlsFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectDrawings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectPhaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DrawingNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Discipline = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Revision = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IssuedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewDueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsAsBuilt = table.Column<bool>(type: "bit", nullable: false),
                    ResponsibleParty = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_ProjectDrawings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectDrawings_ProjectPhases_ProjectPhaseId",
                        column: x => x.ProjectPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectDrawings_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectDrawings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectRfis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectPhaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectPackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Question = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RaisedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResponseDueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RespondedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RaisedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RespondedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ImpactSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Response = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectRfis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectRfis_ProjectPackages_ProjectPackageId",
                        column: x => x.ProjectPackageId,
                        principalTable: "ProjectPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectRfis_ProjectPhases_ProjectPhaseId",
                        column: x => x.ProjectPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectRfis_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectRfis_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectSiteInstructions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectPhaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectPackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InstructionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IssuedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IssuedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResponsibleParty = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EstimatedCostImpact = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ScheduleImpactDays = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_ProjectSiteInstructions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectSiteInstructions_ProjectPackages_ProjectPackageId",
                        column: x => x.ProjectPackageId,
                        principalTable: "ProjectPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectSiteInstructions_ProjectPhases_ProjectPhaseId",
                        column: x => x.ProjectPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectSiteInstructions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectSiteInstructions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectSubmittals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectPhaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectPackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittalType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponseDueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RespondedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReviewedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResponsibleParty = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_ProjectSubmittals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectSubmittals_ProjectPackages_ProjectPackageId",
                        column: x => x.ProjectPackageId,
                        principalTable: "ProjectPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectSubmittals_ProjectPhases_ProjectPhaseId",
                        column: x => x.ProjectPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectSubmittals_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectSubmittals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDrawings_ProjectId_DrawingNumber",
                table: "ProjectDrawings",
                columns: new[] { "ProjectId", "DrawingNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDrawings_ProjectId_ProjectPhaseId_Discipline",
                table: "ProjectDrawings",
                columns: new[] { "ProjectId", "ProjectPhaseId", "Discipline" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDrawings_ProjectId_Status",
                table: "ProjectDrawings",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDrawings_ProjectPhaseId",
                table: "ProjectDrawings",
                column: "ProjectPhaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDrawings_TenantId",
                table: "ProjectDrawings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRfis_ProjectId_ProjectPhaseId_ProjectPackageId",
                table: "ProjectRfis",
                columns: new[] { "ProjectId", "ProjectPhaseId", "ProjectPackageId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRfis_ProjectId_ReferenceNumber",
                table: "ProjectRfis",
                columns: new[] { "ProjectId", "ReferenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRfis_ProjectId_Status_Priority",
                table: "ProjectRfis",
                columns: new[] { "ProjectId", "Status", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRfis_ProjectPackageId",
                table: "ProjectRfis",
                column: "ProjectPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRfis_ProjectPhaseId",
                table: "ProjectRfis",
                column: "ProjectPhaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRfis_TenantId",
                table: "ProjectRfis",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSiteInstructions_ProjectId_ProjectPhaseId_ProjectPackageId",
                table: "ProjectSiteInstructions",
                columns: new[] { "ProjectId", "ProjectPhaseId", "ProjectPackageId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSiteInstructions_ProjectId_ReferenceNumber",
                table: "ProjectSiteInstructions",
                columns: new[] { "ProjectId", "ReferenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSiteInstructions_ProjectId_Status_InstructionType",
                table: "ProjectSiteInstructions",
                columns: new[] { "ProjectId", "Status", "InstructionType" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSiteInstructions_ProjectPackageId",
                table: "ProjectSiteInstructions",
                column: "ProjectPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSiteInstructions_ProjectPhaseId",
                table: "ProjectSiteInstructions",
                column: "ProjectPhaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSiteInstructions_TenantId",
                table: "ProjectSiteInstructions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSubmittals_ProjectId_ProjectPhaseId_ProjectPackageId",
                table: "ProjectSubmittals",
                columns: new[] { "ProjectId", "ProjectPhaseId", "ProjectPackageId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSubmittals_ProjectId_ReferenceNumber",
                table: "ProjectSubmittals",
                columns: new[] { "ProjectId", "ReferenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSubmittals_ProjectId_Status_SubmittalType",
                table: "ProjectSubmittals",
                columns: new[] { "ProjectId", "Status", "SubmittalType" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSubmittals_ProjectPackageId",
                table: "ProjectSubmittals",
                column: "ProjectPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSubmittals_ProjectPhaseId",
                table: "ProjectSubmittals",
                column: "ProjectPhaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSubmittals_TenantId",
                table: "ProjectSubmittals",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectDrawings");

            migrationBuilder.DropTable(
                name: "ProjectRfis");

            migrationBuilder.DropTable(
                name: "ProjectSiteInstructions");

            migrationBuilder.DropTable(
                name: "ProjectSubmittals");
        }
    }
}
