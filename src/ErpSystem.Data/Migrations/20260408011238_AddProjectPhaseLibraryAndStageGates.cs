using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectPhaseLibraryAndStageGates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectCommissioningItems_ProjectUnits_ProjectUnitId",
                table: "ProjectCommissioningItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectCustomerVariations_ProjectUnits_ProjectUnitId",
                table: "ProjectCustomerVariations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectDefectLiabilityCases_ProjectUnits_ProjectUnitId",
                table: "ProjectDefectLiabilityCases");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectHandoverItems_ProjectUnits_ProjectUnitId",
                table: "ProjectHandoverItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectSnagItems_ProjectUnits_ProjectUnitId",
                table: "ProjectSnagItems");

            migrationBuilder.CreateTable(
                name: "ProjectPhaseTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentPhaseTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DefaultStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsOptional = table.Column<bool>(type: "bit", nullable: false),
                    IsStageGateRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AppliesToDeliveryStructure = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AppliesToDevelopmentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_ProjectPhaseTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectPhaseTemplates_ProjectPhaseTemplates_ParentPhaseTemplateId",
                        column: x => x.ParentPhaseTemplateId,
                        principalTable: "ProjectPhaseTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPhaseTemplates_ProjectTypes_ProjectTypeId",
                        column: x => x.ProjectTypeId,
                        principalTable: "ProjectTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPhaseTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectStageGateRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectPhaseTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RequirementType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MinimumCount = table.Column<int>(type: "int", nullable: true),
                    MaximumCount = table.Column<int>(type: "int", nullable: true),
                    IsBlocking = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ProjectStageGateRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectStageGateRules_ProjectPhaseTemplates_ProjectPhaseTemplateId",
                        column: x => x.ProjectPhaseTemplateId,
                        principalTable: "ProjectPhaseTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectStageGateRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPhaseTemplates_ParentPhaseTemplateId",
                table: "ProjectPhaseTemplates",
                column: "ParentPhaseTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPhaseTemplates_ProjectTypeId",
                table: "ProjectPhaseTemplates",
                column: "ProjectTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPhaseTemplates_TenantId_ParentPhaseTemplateId_SortOrder",
                table: "ProjectPhaseTemplates",
                columns: new[] { "TenantId", "ParentPhaseTemplateId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPhaseTemplates_TenantId_ProjectTypeId_Code",
                table: "ProjectPhaseTemplates",
                columns: new[] { "TenantId", "ProjectTypeId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPhaseTemplates_TenantId_ProjectTypeId_SortOrder",
                table: "ProjectPhaseTemplates",
                columns: new[] { "TenantId", "ProjectTypeId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStageGateRules_ProjectPhaseTemplateId",
                table: "ProjectStageGateRules",
                column: "ProjectPhaseTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStageGateRules_TenantId_ProjectPhaseTemplateId_Code",
                table: "ProjectStageGateRules",
                columns: new[] { "TenantId", "ProjectPhaseTemplateId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStageGateRules_TenantId_ProjectPhaseTemplateId_SortOrder",
                table: "ProjectStageGateRules",
                columns: new[] { "TenantId", "ProjectPhaseTemplateId", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectCommissioningItems_ProjectUnits_ProjectUnitId",
                table: "ProjectCommissioningItems",
                column: "ProjectUnitId",
                principalTable: "ProjectUnits",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectCustomerVariations_ProjectUnits_ProjectUnitId",
                table: "ProjectCustomerVariations",
                column: "ProjectUnitId",
                principalTable: "ProjectUnits",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectDefectLiabilityCases_ProjectUnits_ProjectUnitId",
                table: "ProjectDefectLiabilityCases",
                column: "ProjectUnitId",
                principalTable: "ProjectUnits",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectHandoverItems_ProjectUnits_ProjectUnitId",
                table: "ProjectHandoverItems",
                column: "ProjectUnitId",
                principalTable: "ProjectUnits",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectSnagItems_ProjectUnits_ProjectUnitId",
                table: "ProjectSnagItems",
                column: "ProjectUnitId",
                principalTable: "ProjectUnits",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectCommissioningItems_ProjectUnits_ProjectUnitId",
                table: "ProjectCommissioningItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectCustomerVariations_ProjectUnits_ProjectUnitId",
                table: "ProjectCustomerVariations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectDefectLiabilityCases_ProjectUnits_ProjectUnitId",
                table: "ProjectDefectLiabilityCases");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectHandoverItems_ProjectUnits_ProjectUnitId",
                table: "ProjectHandoverItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectSnagItems_ProjectUnits_ProjectUnitId",
                table: "ProjectSnagItems");

            migrationBuilder.DropTable(
                name: "ProjectStageGateRules");

            migrationBuilder.DropTable(
                name: "ProjectPhaseTemplates");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectCommissioningItems_ProjectUnits_ProjectUnitId",
                table: "ProjectCommissioningItems",
                column: "ProjectUnitId",
                principalTable: "ProjectUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectCustomerVariations_ProjectUnits_ProjectUnitId",
                table: "ProjectCustomerVariations",
                column: "ProjectUnitId",
                principalTable: "ProjectUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectDefectLiabilityCases_ProjectUnits_ProjectUnitId",
                table: "ProjectDefectLiabilityCases",
                column: "ProjectUnitId",
                principalTable: "ProjectUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectHandoverItems_ProjectUnits_ProjectUnitId",
                table: "ProjectHandoverItems",
                column: "ProjectUnitId",
                principalTable: "ProjectUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectSnagItems_ProjectUnits_ProjectUnitId",
                table: "ProjectSnagItems",
                column: "ProjectUnitId",
                principalTable: "ProjectUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
