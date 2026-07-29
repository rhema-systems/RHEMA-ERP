using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcedureCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcedureCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Module = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ApplicantName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SourceDepartment = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReceivedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CurrentStageIndex = table.Column<int>(type: "int", nullable: false),
                    CurrentStageName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CurrentStageOwner = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CurrentAssignedRole = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkflowStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastActionById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ProcedureCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcedureCases_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcedureCaseActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcedureCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StageName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PerformedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_ProcedureCaseActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcedureCaseActivities_ProcedureCases_ProcedureCaseId",
                        column: x => x.ProcedureCaseId,
                        principalTable: "ProcedureCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcedureCaseActivities_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcedureCaseChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcedureCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageIndex = table.Column<int>(type: "int", nullable: false),
                    StageName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CompletedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ProcedureCaseChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcedureCaseChecklistItems_ProcedureCases_ProcedureCaseId",
                        column: x => x.ProcedureCaseId,
                        principalTable: "ProcedureCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcedureCaseChecklistItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcedureCaseDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcedureCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    RequiredFrom = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    FileUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UploadedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ProcedureCaseDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcedureCaseDocuments_ProcedureCases_ProcedureCaseId",
                        column: x => x.ProcedureCaseId,
                        principalTable: "ProcedureCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcedureCaseDocuments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcedureCaseFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcedureCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FieldType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_ProcedureCaseFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcedureCaseFields_ProcedureCases_ProcedureCaseId",
                        column: x => x.ProcedureCaseId,
                        principalTable: "ProcedureCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcedureCaseFields_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureCaseActivities_ProcedureCaseId",
                table: "ProcedureCaseActivities",
                column: "ProcedureCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureCaseActivities_TenantId_ProcedureCaseId_PerformedAt",
                table: "ProcedureCaseActivities",
                columns: new[] { "TenantId", "ProcedureCaseId", "PerformedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureCaseChecklistItems_ProcedureCaseId",
                table: "ProcedureCaseChecklistItems",
                column: "ProcedureCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureCaseChecklistItems_TenantId_ProcedureCaseId_StageIndex",
                table: "ProcedureCaseChecklistItems",
                columns: new[] { "TenantId", "ProcedureCaseId", "StageIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureCaseDocuments_ProcedureCaseId",
                table: "ProcedureCaseDocuments",
                column: "ProcedureCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureCaseDocuments_TenantId_ProcedureCaseId_IsMandatory",
                table: "ProcedureCaseDocuments",
                columns: new[] { "TenantId", "ProcedureCaseId", "IsMandatory" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureCaseFields_ProcedureCaseId",
                table: "ProcedureCaseFields",
                column: "ProcedureCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureCaseFields_TenantId_ProcedureCaseId_Key",
                table: "ProcedureCaseFields",
                columns: new[] { "TenantId", "ProcedureCaseId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureCases_TenantId_CurrentAssignedRole",
                table: "ProcedureCases",
                columns: new[] { "TenantId", "CurrentAssignedRole" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureCases_TenantId_Module_EntityType_Status",
                table: "ProcedureCases",
                columns: new[] { "TenantId", "Module", "EntityType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureCases_TenantId_ReferenceNumber",
                table: "ProcedureCases",
                columns: new[] { "TenantId", "ReferenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureCases_WorkflowDefinitionId",
                table: "ProcedureCases",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureCases_WorkflowStepId",
                table: "ProcedureCases",
                column: "WorkflowStepId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcedureCaseActivities");

            migrationBuilder.DropTable(
                name: "ProcedureCaseChecklistItems");

            migrationBuilder.DropTable(
                name: "ProcedureCaseDocuments");

            migrationBuilder.DropTable(
                name: "ProcedureCaseFields");

            migrationBuilder.DropTable(
                name: "ProcedureCases");
        }
    }
}
