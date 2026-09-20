using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class TDC0604InventoryAccessLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LocationScopeMode",
                table: "ProcurementResponsibilityAssignments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE [ProcurementResponsibilityAssignments]
                SET [LocationScopeMode] = CASE
                    WHEN [WarehouseScopeMode] IN (1, 2) THEN 1
                    ELSE 0
                END;
                """);

            migrationBuilder.CreateTable(
                name: "ProcurementResponsibilityLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementResponsibilityLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementResponsibilityLocations_ProcurementResponsibilityAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "ProcurementResponsibilityAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementResponsibilityLocations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementResponsibilityLocations_WarehouseLocations_WarehouseLocationId",
                        column: x => x.WarehouseLocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementResponsibilityLocations_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProcurementResponsibilityAssignments_LocationScope",
                table: "ProcurementResponsibilityAssignments",
                sql: "[LocationScopeMode] IN (0, 1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementResponsibilityLocations_AssignmentId",
                table: "ProcurementResponsibilityLocations",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementResponsibilityLocations_TenantId_AssignmentId_WarehouseLocationId",
                table: "ProcurementResponsibilityLocations",
                columns: new[] { "TenantId", "AssignmentId", "WarehouseLocationId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementResponsibilityLocations_TenantId_WarehouseId_WarehouseLocationId",
                table: "ProcurementResponsibilityLocations",
                columns: new[] { "TenantId", "WarehouseId", "WarehouseLocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementResponsibilityLocations_WarehouseId",
                table: "ProcurementResponsibilityLocations",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementResponsibilityLocations_WarehouseLocationId",
                table: "ProcurementResponsibilityLocations",
                column: "WarehouseLocationId");

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementResponsibilityLocations_ValidateScope]
                ON [dbo].[ProcurementResponsibilityLocations]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted AS scope
                        INNER JOIN [dbo].[ProcurementResponsibilityAssignments] AS assignment
                            ON assignment.[Id] = scope.[AssignmentId]
                        INNER JOIN [dbo].[Warehouses] AS warehouse
                            ON warehouse.[Id] = scope.[WarehouseId]
                        INNER JOIN [dbo].[WarehouseLocations] AS location
                            ON location.[Id] = scope.[WarehouseLocationId]
                        WHERE scope.[TenantId] <> assignment.[TenantId]
                           OR scope.[TenantId] <> warehouse.[TenantId]
                           OR scope.[TenantId] <> location.[TenantId]
                           OR scope.[WarehouseId] <> location.[WarehouseId]
                           OR (scope.[IsDeleted] = 0 AND assignment.[LocationScopeMode] <> 2)
                    )
                    BEGIN
                        THROW 51004, 'TDC-0604 location assignments must match the tenant, warehouse, location, and restricted assignment scope.', 1;
                    END;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementResponsibilityLocations_ValidateScope];");

            migrationBuilder.DropTable(
                name: "ProcurementResponsibilityLocations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProcurementResponsibilityAssignments_LocationScope",
                table: "ProcurementResponsibilityAssignments");

            migrationBuilder.DropColumn(
                name: "LocationScopeMode",
                table: "ProcurementResponsibilityAssignments");
        }
    }
}
