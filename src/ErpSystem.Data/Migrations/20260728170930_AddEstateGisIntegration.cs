using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEstateGisIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GisFeatureId",
                table: "EstateManagedAssets",
                type: "nvarchar(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GisLastSyncedAt",
                table: "EstateManagedAssets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GisProvider",
                table: "EstateManagedAssets",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "GeoServer");

            migrationBuilder.AddColumn<string>(
                name: "GisSourceCrs",
                table: "EstateManagedAssets",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GisSyncStatus",
                table: "EstateManagedAssets",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "NotLinked");

            migrationBuilder.CreateTable(
                name: "EstateGisConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SqlServerHost = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SqlServerPort = table.Column<int>(type: "int", nullable: true),
                    SqlServerDatabase = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    SqlServerSchema = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    GeoServerBaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GeoServerWorkspace = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    DefaultFeatureLayer = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    ArcGisFeatureServiceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BaseMapTileUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SourceCrs = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DisplayCrs = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ProtectedCredentials = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConnectionStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LastConnectionTestAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastConnectionMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_EstateGisConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EstateGisConfigurations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EstateGisConfigurations_TenantId",
                table: "EstateGisConfigurations",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EstateGisConfigurations");

            migrationBuilder.DropColumn(
                name: "GisFeatureId",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "GisLastSyncedAt",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "GisProvider",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "GisSourceCrs",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "GisSyncStatus",
                table: "EstateManagedAssets");

        }
    }
}
