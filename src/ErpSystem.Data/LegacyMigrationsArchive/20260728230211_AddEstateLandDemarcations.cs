using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEstateLandDemarcations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EstateLandDemarcations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EstateManagedAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DemarcationNumber = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    BeaconCount = table.Column<int>(type: "int", nullable: false),
                    BoundaryCoordinates = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AreaSquareFeet = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    BoundaryVerified = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_EstateLandDemarcations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EstateLandDemarcations_EstateManagedAssets_EstateManagedAssetId",
                        column: x => x.EstateManagedAssetId,
                        principalTable: "EstateManagedAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EstateLandDemarcations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EstateLandDemarcations_EstateManagedAssetId",
                table: "EstateLandDemarcations",
                column: "EstateManagedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_EstateLandDemarcations_TenantId_EstateManagedAssetId_DemarcationNumber",
                table: "EstateLandDemarcations",
                columns: new[] { "TenantId", "EstateManagedAssetId", "DemarcationNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EstateLandDemarcations");
        }
    }
}
