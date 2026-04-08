using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectUnitHierarchyAndPhasedHandoverFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectBuildingId",
                table: "ProjectUnits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectFloorId",
                table: "ProjectUnits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectUnitReleaseBatchId",
                table: "ProjectUnits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectUnitHandoverBatchId",
                table: "ProjectHandoverItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectBuildings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ProjectBuildings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectBuildings_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectBuildings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectFloors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectBuildingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LevelNumber = table.Column<int>(type: "int", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ProjectFloors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectFloors_ProjectBuildings_ProjectBuildingId",
                        column: x => x.ProjectBuildingId,
                        principalTable: "ProjectBuildings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectFloors_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectFloors_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectUnitHandoverBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectBuildingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectFloorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PlannedHandoverDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualHandoverDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ProjectUnitHandoverBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectUnitHandoverBatches_ProjectBuildings_ProjectBuildingId",
                        column: x => x.ProjectBuildingId,
                        principalTable: "ProjectBuildings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectUnitHandoverBatches_ProjectFloors_ProjectFloorId",
                        column: x => x.ProjectFloorId,
                        principalTable: "ProjectFloors",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectUnitHandoverBatches_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectUnitHandoverBatches_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectUnitReleaseBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectBuildingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectFloorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PlannedReleaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualReleaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ProjectUnitReleaseBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectUnitReleaseBatches_ProjectBuildings_ProjectBuildingId",
                        column: x => x.ProjectBuildingId,
                        principalTable: "ProjectBuildings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectUnitReleaseBatches_ProjectFloors_ProjectFloorId",
                        column: x => x.ProjectFloorId,
                        principalTable: "ProjectFloors",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectUnitReleaseBatches_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectUnitReleaseBatches_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_ProjectBuildingId",
                table: "ProjectUnits",
                column: "ProjectBuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_ProjectFloorId",
                table: "ProjectUnits",
                column: "ProjectFloorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_ProjectId_ProjectBuildingId",
                table: "ProjectUnits",
                columns: new[] { "ProjectId", "ProjectBuildingId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_ProjectId_ProjectFloorId",
                table: "ProjectUnits",
                columns: new[] { "ProjectId", "ProjectFloorId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_ProjectId_ProjectUnitReleaseBatchId",
                table: "ProjectUnits",
                columns: new[] { "ProjectId", "ProjectUnitReleaseBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_ProjectUnitReleaseBatchId",
                table: "ProjectUnits",
                column: "ProjectUnitReleaseBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHandoverItems_ProjectId_ProjectUnitHandoverBatchId",
                table: "ProjectHandoverItems",
                columns: new[] { "ProjectId", "ProjectUnitHandoverBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHandoverItems_ProjectUnitHandoverBatchId",
                table: "ProjectHandoverItems",
                column: "ProjectUnitHandoverBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBuildings_ProjectId_Code",
                table: "ProjectBuildings",
                columns: new[] { "ProjectId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBuildings_ProjectId_SortOrder",
                table: "ProjectBuildings",
                columns: new[] { "ProjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBuildings_TenantId",
                table: "ProjectBuildings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFloors_ProjectBuildingId",
                table: "ProjectFloors",
                column: "ProjectBuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFloors_ProjectId_Code",
                table: "ProjectFloors",
                columns: new[] { "ProjectId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFloors_ProjectId_ProjectBuildingId",
                table: "ProjectFloors",
                columns: new[] { "ProjectId", "ProjectBuildingId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFloors_ProjectId_SortOrder",
                table: "ProjectFloors",
                columns: new[] { "ProjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFloors_TenantId",
                table: "ProjectFloors",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitHandoverBatches_ProjectBuildingId",
                table: "ProjectUnitHandoverBatches",
                column: "ProjectBuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitHandoverBatches_ProjectFloorId",
                table: "ProjectUnitHandoverBatches",
                column: "ProjectFloorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitHandoverBatches_ProjectId_Code",
                table: "ProjectUnitHandoverBatches",
                columns: new[] { "ProjectId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitHandoverBatches_ProjectId_ProjectBuildingId",
                table: "ProjectUnitHandoverBatches",
                columns: new[] { "ProjectId", "ProjectBuildingId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitHandoverBatches_ProjectId_ProjectFloorId",
                table: "ProjectUnitHandoverBatches",
                columns: new[] { "ProjectId", "ProjectFloorId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitHandoverBatches_ProjectId_SortOrder",
                table: "ProjectUnitHandoverBatches",
                columns: new[] { "ProjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitHandoverBatches_ProjectId_Status",
                table: "ProjectUnitHandoverBatches",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitHandoverBatches_TenantId",
                table: "ProjectUnitHandoverBatches",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitReleaseBatches_ProjectBuildingId",
                table: "ProjectUnitReleaseBatches",
                column: "ProjectBuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitReleaseBatches_ProjectFloorId",
                table: "ProjectUnitReleaseBatches",
                column: "ProjectFloorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitReleaseBatches_ProjectId_Code",
                table: "ProjectUnitReleaseBatches",
                columns: new[] { "ProjectId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitReleaseBatches_ProjectId_ProjectBuildingId",
                table: "ProjectUnitReleaseBatches",
                columns: new[] { "ProjectId", "ProjectBuildingId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitReleaseBatches_ProjectId_ProjectFloorId",
                table: "ProjectUnitReleaseBatches",
                columns: new[] { "ProjectId", "ProjectFloorId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitReleaseBatches_ProjectId_SortOrder",
                table: "ProjectUnitReleaseBatches",
                columns: new[] { "ProjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitReleaseBatches_ProjectId_Status",
                table: "ProjectUnitReleaseBatches",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitReleaseBatches_TenantId",
                table: "ProjectUnitReleaseBatches",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectHandoverItems_ProjectUnitHandoverBatches_ProjectUnitHandoverBatchId",
                table: "ProjectHandoverItems",
                column: "ProjectUnitHandoverBatchId",
                principalTable: "ProjectUnitHandoverBatches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectUnits_ProjectBuildings_ProjectBuildingId",
                table: "ProjectUnits",
                column: "ProjectBuildingId",
                principalTable: "ProjectBuildings",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectUnits_ProjectFloors_ProjectFloorId",
                table: "ProjectUnits",
                column: "ProjectFloorId",
                principalTable: "ProjectFloors",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectUnits_ProjectUnitReleaseBatches_ProjectUnitReleaseBatchId",
                table: "ProjectUnits",
                column: "ProjectUnitReleaseBatchId",
                principalTable: "ProjectUnitReleaseBatches",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectHandoverItems_ProjectUnitHandoverBatches_ProjectUnitHandoverBatchId",
                table: "ProjectHandoverItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectUnits_ProjectBuildings_ProjectBuildingId",
                table: "ProjectUnits");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectUnits_ProjectFloors_ProjectFloorId",
                table: "ProjectUnits");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectUnits_ProjectUnitReleaseBatches_ProjectUnitReleaseBatchId",
                table: "ProjectUnits");

            migrationBuilder.DropTable(
                name: "ProjectUnitHandoverBatches");

            migrationBuilder.DropTable(
                name: "ProjectUnitReleaseBatches");

            migrationBuilder.DropTable(
                name: "ProjectFloors");

            migrationBuilder.DropTable(
                name: "ProjectBuildings");

            migrationBuilder.DropIndex(
                name: "IX_ProjectUnits_ProjectBuildingId",
                table: "ProjectUnits");

            migrationBuilder.DropIndex(
                name: "IX_ProjectUnits_ProjectFloorId",
                table: "ProjectUnits");

            migrationBuilder.DropIndex(
                name: "IX_ProjectUnits_ProjectId_ProjectBuildingId",
                table: "ProjectUnits");

            migrationBuilder.DropIndex(
                name: "IX_ProjectUnits_ProjectId_ProjectFloorId",
                table: "ProjectUnits");

            migrationBuilder.DropIndex(
                name: "IX_ProjectUnits_ProjectId_ProjectUnitReleaseBatchId",
                table: "ProjectUnits");

            migrationBuilder.DropIndex(
                name: "IX_ProjectUnits_ProjectUnitReleaseBatchId",
                table: "ProjectUnits");

            migrationBuilder.DropIndex(
                name: "IX_ProjectHandoverItems_ProjectId_ProjectUnitHandoverBatchId",
                table: "ProjectHandoverItems");

            migrationBuilder.DropIndex(
                name: "IX_ProjectHandoverItems_ProjectUnitHandoverBatchId",
                table: "ProjectHandoverItems");

            migrationBuilder.DropColumn(
                name: "ProjectBuildingId",
                table: "ProjectUnits");

            migrationBuilder.DropColumn(
                name: "ProjectFloorId",
                table: "ProjectUnits");

            migrationBuilder.DropColumn(
                name: "ProjectUnitReleaseBatchId",
                table: "ProjectUnits");

            migrationBuilder.DropColumn(
                name: "ProjectUnitHandoverBatchId",
                table: "ProjectHandoverItems");
        }
    }
}
