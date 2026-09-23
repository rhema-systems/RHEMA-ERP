using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260923120000_PreserveGroundRentAccountsAcrossOccupants")]
    public partial class PreserveGroundRentAccountsAcrossOccupants : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EstateGroundRentAccounts_TenantId_EstateManagedAssetId",
                table: "EstateGroundRentAccounts");

            migrationBuilder.CreateIndex(
                name: "IX_EstateGroundRentAccounts_TenantId_EstateManagedAssetId",
                table: "EstateGroundRentAccounts",
                columns: new[] { "TenantId", "EstateManagedAssetId" },
                unique: true,
                filter: "[Status] <> 'Closed' AND [IsDeleted] = 0");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EstateGroundRentAccounts_TenantId_EstateManagedAssetId",
                table: "EstateGroundRentAccounts");

            migrationBuilder.CreateIndex(
                name: "IX_EstateGroundRentAccounts_TenantId_EstateManagedAssetId",
                table: "EstateGroundRentAccounts",
                columns: new[] { "TenantId", "EstateManagedAssetId" },
                unique: true);
        }
    }
}
