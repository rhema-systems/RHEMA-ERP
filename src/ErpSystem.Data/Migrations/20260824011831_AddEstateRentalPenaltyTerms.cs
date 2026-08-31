using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260824011831_AddEstateRentalPenaltyTerms")]
public partial class AddEstateRentalPenaltyTerms : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "RentGracePeriodDays",
            table: "EstateManagedAssets",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "RentPenaltyMethod",
            table: "EstateManagedAssets",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "None");

        migrationBuilder.AddColumn<decimal>(
            name: "RentPenaltyValue",
            table: "EstateManagedAssets",
            type: "decimal(18,4)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "RentPenaltyCapAmount",
            table: "EstateManagedAssets",
            type: "decimal(18,2)",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "LastRentPenaltyInvoiceId",
            table: "EstateManagedAssets",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LastRentPenaltyInvoiceNumber",
            table: "EstateManagedAssets",
            type: "nvarchar(80)",
            maxLength: 80,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "LastRentPenaltySourceInvoiceId",
            table: "EstateManagedAssets",
            type: "uniqueidentifier",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RentGracePeriodDays",
            table: "EstateManagedAssets");

        migrationBuilder.DropColumn(
            name: "RentPenaltyMethod",
            table: "EstateManagedAssets");

        migrationBuilder.DropColumn(
            name: "RentPenaltyValue",
            table: "EstateManagedAssets");

        migrationBuilder.DropColumn(
            name: "RentPenaltyCapAmount",
            table: "EstateManagedAssets");

        migrationBuilder.DropColumn(
            name: "LastRentPenaltyInvoiceId",
            table: "EstateManagedAssets");

        migrationBuilder.DropColumn(
            name: "LastRentPenaltyInvoiceNumber",
            table: "EstateManagedAssets");

        migrationBuilder.DropColumn(
            name: "LastRentPenaltySourceInvoiceId",
            table: "EstateManagedAssets");
    }
}
