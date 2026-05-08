using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectUnitTypeTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectUnitTypeTemplateId",
                table: "ProjectUnits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectUnitAmenities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AmenityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ProjectUnitAmenities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectUnitAmenities_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectUnitAmenities_ProjectUnits_ProjectUnitId",
                        column: x => x.ProjectUnitId,
                        principalTable: "ProjectUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectUnitAmenities_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectUnitTypeTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DefaultProjectUnitType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
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
                    table.PrimaryKey("PK_ProjectUnitTypeTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectUnitTypeTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectUnitTypeTemplateAmenities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectUnitTypeTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AmenityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ProjectUnitTypeTemplateAmenities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectUnitTypeTemplateAmenities_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectUnitTypeTemplateAmenities_ProjectUnitTypeTemplates_ProjectUnitTypeTemplateId",
                        column: x => x.ProjectUnitTypeTemplateId,
                        principalTable: "ProjectUnitTypeTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectUnitTypeTemplateAmenities_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_ProjectId_ProjectUnitTypeTemplateId",
                table: "ProjectUnits",
                columns: new[] { "ProjectId", "ProjectUnitTypeTemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_ProjectUnitTypeTemplateId",
                table: "ProjectUnits",
                column: "ProjectUnitTypeTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitAmenities_InventoryItemId",
                table: "ProjectUnitAmenities",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitAmenities_ProjectUnitId",
                table: "ProjectUnitAmenities",
                column: "ProjectUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitAmenities_ProjectUnitId_SortOrder",
                table: "ProjectUnitAmenities",
                columns: new[] { "ProjectUnitId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitAmenities_TenantId",
                table: "ProjectUnitAmenities",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitTypeTemplateAmenities_InventoryItemId",
                table: "ProjectUnitTypeTemplateAmenities",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitTypeTemplateAmenities_ProjectUnitTypeTemplateId",
                table: "ProjectUnitTypeTemplateAmenities",
                column: "ProjectUnitTypeTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitTypeTemplateAmenities_ProjectUnitTypeTemplateId_InventoryItemId_SortOrder",
                table: "ProjectUnitTypeTemplateAmenities",
                columns: new[] { "ProjectUnitTypeTemplateId", "InventoryItemId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitTypeTemplateAmenities_TenantId",
                table: "ProjectUnitTypeTemplateAmenities",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitTypeTemplates_TenantId_Code",
                table: "ProjectUnitTypeTemplates",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitTypeTemplates_TenantId_IsActive_SortOrder",
                table: "ProjectUnitTypeTemplates",
                columns: new[] { "TenantId", "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitTypeTemplates_TenantId_Name",
                table: "ProjectUnitTypeTemplates",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectUnits_ProjectUnitTypeTemplates_ProjectUnitTypeTemplateId",
                table: "ProjectUnits",
                column: "ProjectUnitTypeTemplateId",
                principalTable: "ProjectUnitTypeTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectUnits_ProjectUnitTypeTemplates_ProjectUnitTypeTemplateId",
                table: "ProjectUnits");

            migrationBuilder.DropTable(
                name: "ProjectUnitAmenities");

            migrationBuilder.DropTable(
                name: "ProjectUnitTypeTemplateAmenities");

            migrationBuilder.DropTable(
                name: "ProjectUnitTypeTemplates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectUnits_ProjectId_ProjectUnitTypeTemplateId",
                table: "ProjectUnits");

            migrationBuilder.DropIndex(
                name: "IX_ProjectUnits_ProjectUnitTypeTemplateId",
                table: "ProjectUnits");

            migrationBuilder.DropColumn(
                name: "ProjectUnitTypeTemplateId",
                table: "ProjectUnits");
        }
    }
}
