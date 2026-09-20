using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementAccessControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementCommittees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CommitteeType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequiredQuorum = table.Column<int>(type: "int", nullable: false),
                    RequiredRoleName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementCommittees", x => x.Id);
                    table.CheckConstraint("CK_ProcurementCommittees_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_ProcurementCommittees_Quorum", "[RequiredQuorum] BETWEEN 1 AND 50");
                    table.CheckConstraint("CK_ProcurementCommittees_Status", "[Status] IN (0, 1, 2)");
                    table.CheckConstraint("CK_ProcurementCommittees_Type", "[CommitteeType] IN (0, 1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_ProcurementCommittees_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementResponsibilityAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    WarehouseScopeMode = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementResponsibilityAssignments", x => x.Id);
                    table.CheckConstraint("CK_ProcurementResponsibilityAssignments_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_ProcurementResponsibilityAssignments_WarehouseScope", "[WarehouseScopeMode] IN (0, 1, 2)");
                    table.ForeignKey(
                        name: "FK_ProcurementResponsibilityAssignments_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementResponsibilityAssignments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementResponsibilityAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementCommitteeMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommitteeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberKind = table.Column<int>(type: "int", nullable: false),
                    IsVoting = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementCommitteeMembers", x => x.Id);
                    table.CheckConstraint("CK_ProcurementCommitteeMembers_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_ProcurementCommitteeMembers_Kind", "[MemberKind] IN (0, 1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_ProcurementCommitteeMembers_ProcurementCommittees_CommitteeId",
                        column: x => x.CommitteeId,
                        principalTable: "ProcurementCommittees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementCommitteeMembers_ProcurementResponsibilityAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "ProcurementResponsibilityAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementCommitteeMembers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementResponsibilityWarehouses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementResponsibilityWarehouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementResponsibilityWarehouses_ProcurementResponsibilityAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "ProcurementResponsibilityAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementResponsibilityWarehouses_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementResponsibilityWarehouses_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCommitteeMembers_AssignmentId",
                table: "ProcurementCommitteeMembers",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCommitteeMembers_CommitteeId",
                table: "ProcurementCommitteeMembers",
                column: "CommitteeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCommitteeMembers_TenantId_AssignmentId_IsActive",
                table: "ProcurementCommitteeMembers",
                columns: new[] { "TenantId", "AssignmentId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCommitteeMembers_TenantId_CommitteeId_AssignmentId",
                table: "ProcurementCommitteeMembers",
                columns: new[] { "TenantId", "CommitteeId", "AssignmentId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCommittees_TenantId_Code",
                table: "ProcurementCommittees",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCommittees_TenantId_CommitteeType_Status",
                table: "ProcurementCommittees",
                columns: new[] { "TenantId", "CommitteeType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementResponsibilityAssignments_RoleId",
                table: "ProcurementResponsibilityAssignments",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementResponsibilityAssignments_TenantId_RoleId_IsActive",
                table: "ProcurementResponsibilityAssignments",
                columns: new[] { "TenantId", "RoleId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementResponsibilityAssignments_TenantId_UserId_EffectiveFrom",
                table: "ProcurementResponsibilityAssignments",
                columns: new[] { "TenantId", "UserId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementResponsibilityAssignments_TenantId_UserId_RoleName",
                table: "ProcurementResponsibilityAssignments",
                columns: new[] { "TenantId", "UserId", "RoleName" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementResponsibilityAssignments_UserId",
                table: "ProcurementResponsibilityAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementResponsibilityWarehouses_AssignmentId",
                table: "ProcurementResponsibilityWarehouses",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementResponsibilityWarehouses_TenantId_AssignmentId_WarehouseId",
                table: "ProcurementResponsibilityWarehouses",
                columns: new[] { "TenantId", "AssignmentId", "WarehouseId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementResponsibilityWarehouses_TenantId_WarehouseId",
                table: "ProcurementResponsibilityWarehouses",
                columns: new[] { "TenantId", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementResponsibilityWarehouses_WarehouseId",
                table: "ProcurementResponsibilityWarehouses",
                column: "WarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcurementCommitteeMembers");

            migrationBuilder.DropTable(
                name: "ProcurementResponsibilityWarehouses");

            migrationBuilder.DropTable(
                name: "ProcurementCommittees");

            migrationBuilder.DropTable(
                name: "ProcurementResponsibilityAssignments");
        }
    }
}
