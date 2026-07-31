using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEstatePropertyRegisterFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerBusinessPartnerId",
                table: "EstateManagedAssets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfTenancy",
                table: "EstateManagedAssets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GroundRentPayable",
                table: "EstateManagedAssets",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeaseTermYears",
                table: "EstateManagedAssets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LesseeAddress",
                table: "EstateManagedAssets",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LesseeName",
                table: "EstateManagedAssets",
                type: "nvarchar(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PropertyFileReference",
                table: "EstateManagedAssets",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RightOfEntryDate",
                table: "EstateManagedAssets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EstateManagedAssets_TenantId_CustomerBusinessPartnerId",
                table: "EstateManagedAssets",
                columns: new[] { "TenantId", "CustomerBusinessPartnerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EstateManagedAssets_TenantId_CustomerBusinessPartnerId",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "CustomerBusinessPartnerId",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "DateOfTenancy",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "GroundRentPayable",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "LeaseTermYears",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "LesseeAddress",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "LesseeName",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "PropertyFileReference",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "RightOfEntryDate",
                table: "EstateManagedAssets");

        }
    }
}
