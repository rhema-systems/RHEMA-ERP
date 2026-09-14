using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEstateDocumentCentralDmsPublication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CentralDocumentRecordId",
                table: "EstateManagedAssetDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CentralDocumentReference",
                table: "EstateManagedAssetDocuments",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedToCentralDmsAt",
                table: "EstateManagedAssetDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EstateManagedAssetDocuments_TenantId_CentralDocumentRecordId",
                table: "EstateManagedAssetDocuments",
                columns: new[] { "TenantId", "CentralDocumentRecordId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EstateManagedAssetDocuments_TenantId_CentralDocumentRecordId",
                table: "EstateManagedAssetDocuments");

            migrationBuilder.DropColumn(
                name: "CentralDocumentRecordId",
                table: "EstateManagedAssetDocuments");

            migrationBuilder.DropColumn(
                name: "CentralDocumentReference",
                table: "EstateManagedAssetDocuments");

            migrationBuilder.DropColumn(
                name: "PublishedToCentralDmsAt",
                table: "EstateManagedAssetDocuments");
        }
    }
}
