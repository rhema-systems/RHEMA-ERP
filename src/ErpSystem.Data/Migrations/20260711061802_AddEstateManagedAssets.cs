using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEstateManagedAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EstateManagedAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BlockName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    FloorLabel = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    AssetType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LandAcquisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ProjectTitle = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    ProjectUnitCode = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UnitType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    AreaSquareMeters = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ValuationAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IsAvailableForLease = table.Column<bool>(type: "bit", nullable: false),
                    IsAvailableForSale = table.Column<bool>(type: "bit", nullable: false),
                    IsPublishedFromProject = table.Column<bool>(type: "bit", nullable: false),
                    PublishedFromProjectAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_EstateManagedAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EstateManagedAssets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EstateManagedAssets_TenantId_AssetCode",
                table: "EstateManagedAssets",
                columns: new[] { "TenantId", "AssetCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EstateManagedAssets_TenantId_AssetType_Status",
                table: "EstateManagedAssets",
                columns: new[] { "TenantId", "AssetType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EstateManagedAssets_TenantId_LandAcquisitionId",
                table: "EstateManagedAssets",
                columns: new[] { "TenantId", "LandAcquisitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_EstateManagedAssets_TenantId_ProjectUnitId",
                table: "EstateManagedAssets",
                columns: new[] { "TenantId", "ProjectUnitId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EstateManagedAssets");
        }
    }
}
