using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEstateStageControlsManualLandAndDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WorkspaceDataJson",
                table: "LandAcquisitions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AreaUnit",
                table: "EstateManagedAssets",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AreaValue",
                table: "EstateManagedAssets",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BeaconCount",
                table: "EstateManagedAssets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CadastreDescription",
                table: "EstateManagedAssets",
                type: "nvarchar(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "EstateManagedAssets",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsReadyForProjectManagement",
                table: "EstateManagedAssets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OwnershipHistoryJson",
                table: "EstateManagedAssets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "EstateManagedAssets",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SurveyDate",
                table: "EstateManagedAssets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SurveyorName",
                table: "EstateManagedAssets",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Town",
                table: "EstateManagedAssets",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EstateManagedAssetDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EstateManagedAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DocumentName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_EstateManagedAssetDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EstateManagedAssetDocuments_EstateManagedAssets_EstateManagedAssetId",
                        column: x => x.EstateManagedAssetId,
                        principalTable: "EstateManagedAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EstateManagedAssetDocuments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EstateManagedAssetDocuments_EstateManagedAssetId",
                table: "EstateManagedAssetDocuments",
                column: "EstateManagedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_EstateManagedAssetDocuments_TenantId_EstateManagedAssetId",
                table: "EstateManagedAssetDocuments",
                columns: new[] { "TenantId", "EstateManagedAssetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EstateManagedAssetDocuments");

            migrationBuilder.DropColumn(
                name: "WorkspaceDataJson",
                table: "LandAcquisitions");

            migrationBuilder.DropColumn(
                name: "AreaUnit",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "AreaValue",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "BeaconCount",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "CadastreDescription",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "District",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "IsReadyForProjectManagement",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "OwnershipHistoryJson",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "SurveyDate",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "SurveyorName",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "Town",
                table: "EstateManagedAssets");
        }
    }
}
