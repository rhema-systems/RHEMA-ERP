using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class _20260225_AddFleetComplianceTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpiryDate",
                table: "FleetComplianceItems",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateItemId",
                table: "FleetComplianceItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FleetComplianceTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_FleetComplianceTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetComplianceTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FleetComplianceTemplateItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComplianceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsCritical = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_FleetComplianceTemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetComplianceTemplateItems_FleetComplianceTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "FleetComplianceTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FleetComplianceTemplateItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FleetVehicleComplianceTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AppliedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_FleetVehicleComplianceTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetVehicleComplianceTemplates_FleetComplianceTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "FleetComplianceTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FleetVehicleComplianceTemplates_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FleetVehicleComplianceTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FleetComplianceItems_TemplateItemId",
                table: "FleetComplianceItems",
                column: "TemplateItemId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetComplianceItems_TenantId_VehicleAssetId_TemplateItemId",
                table: "FleetComplianceItems",
                columns: new[] { "TenantId", "VehicleAssetId", "TemplateItemId" },
                unique: true,
                filter: "[TemplateItemId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FleetComplianceTemplateItems_TemplateId",
                table: "FleetComplianceTemplateItems",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetComplianceTemplateItems_TenantId_TemplateId",
                table: "FleetComplianceTemplateItems",
                columns: new[] { "TenantId", "TemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetComplianceTemplateItems_TenantId_TemplateId_ComplianceType",
                table: "FleetComplianceTemplateItems",
                columns: new[] { "TenantId", "TemplateId", "ComplianceType" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FleetComplianceTemplates_TenantId_Name",
                table: "FleetComplianceTemplates",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FleetVehicleComplianceTemplates_TemplateId",
                table: "FleetVehicleComplianceTemplates",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetVehicleComplianceTemplates_TenantId_TemplateId",
                table: "FleetVehicleComplianceTemplates",
                columns: new[] { "TenantId", "TemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetVehicleComplianceTemplates_TenantId_VehicleAssetId",
                table: "FleetVehicleComplianceTemplates",
                columns: new[] { "TenantId", "VehicleAssetId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FleetVehicleComplianceTemplates_VehicleAssetId",
                table: "FleetVehicleComplianceTemplates",
                column: "VehicleAssetId");

            migrationBuilder.AddForeignKey(
                name: "FK_FleetComplianceItems_FleetComplianceTemplateItems_TemplateItemId",
                table: "FleetComplianceItems",
                column: "TemplateItemId",
                principalTable: "FleetComplianceTemplateItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FleetComplianceItems_FleetComplianceTemplateItems_TemplateItemId",
                table: "FleetComplianceItems");

            migrationBuilder.DropTable(
                name: "FleetComplianceTemplateItems");

            migrationBuilder.DropTable(
                name: "FleetVehicleComplianceTemplates");

            migrationBuilder.DropTable(
                name: "FleetComplianceTemplates");

            migrationBuilder.DropIndex(
                name: "IX_FleetComplianceItems_TemplateItemId",
                table: "FleetComplianceItems");

            migrationBuilder.DropIndex(
                name: "IX_FleetComplianceItems_TenantId_VehicleAssetId_TemplateItemId",
                table: "FleetComplianceItems");

            migrationBuilder.DropColumn(
                name: "TemplateItemId",
                table: "FleetComplianceItems");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpiryDate",
                table: "FleetComplianceItems",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }
    }
}
