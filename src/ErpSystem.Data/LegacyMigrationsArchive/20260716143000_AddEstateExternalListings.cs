using System;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260716143000_AddEstateExternalListings")]
    public partial class AddEstateExternalListings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalListingCurrency",
                table: "EstateManagedAssets",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "GHS");

            migrationBuilder.AddColumn<string>(
                name: "ExternalListingNotes",
                table: "EstateManagedAssets",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExternalListingPrice",
                table: "EstateManagedAssets",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalListingStatus",
                table: "EstateManagedAssets",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<string>(
                name: "ExternalListingType",
                table: "EstateManagedAssets",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExternalPublishedAt",
                table: "EstateManagedAssets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublishedToExternalPortal",
                table: "EstateManagedAssets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsListingImage",
                table: "EstateManagedAssetDocuments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPrimaryListingImage",
                table: "EstateManagedAssetDocuments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_EstateManagedAssets_TenantId_IsPublishedToExternalPortal_ExternalListingStatus",
                table: "EstateManagedAssets",
                columns: new[] { "TenantId", "IsPublishedToExternalPortal", "ExternalListingStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_EstateManagedAssetDocuments_TenantId_EstateManagedAssetId_IsListingImage",
                table: "EstateManagedAssetDocuments",
                columns: new[] { "TenantId", "EstateManagedAssetId", "IsListingImage" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EstateManagedAssets_TenantId_IsPublishedToExternalPortal_ExternalListingStatus",
                table: "EstateManagedAssets");

            migrationBuilder.DropIndex(
                name: "IX_EstateManagedAssetDocuments_TenantId_EstateManagedAssetId_IsListingImage",
                table: "EstateManagedAssetDocuments");

            migrationBuilder.DropColumn(
                name: "ExternalListingCurrency",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "ExternalListingNotes",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "ExternalListingPrice",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "ExternalListingStatus",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "ExternalListingType",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "ExternalPublishedAt",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "IsPublishedToExternalPortal",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "IsListingImage",
                table: "EstateManagedAssetDocuments");

            migrationBuilder.DropColumn(
                name: "IsPrimaryListingImage",
                table: "EstateManagedAssetDocuments");
        }
    }
}
