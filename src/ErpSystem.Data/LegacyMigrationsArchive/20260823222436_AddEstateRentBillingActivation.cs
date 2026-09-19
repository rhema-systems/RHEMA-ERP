using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260823222436_AddEstateRentBillingActivation")]
public partial class AddEstateRentBillingActivation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "AutoGenerateRentInvoices",
            table: "EstateManagedAssets",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<Guid>(
            name: "LastRentInvoiceId",
            table: "EstateManagedAssets",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LastRentInvoiceNumber",
            table: "EstateManagedAssets",
            type: "nvarchar(80)",
            maxLength: 80,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "NextRentBillingDate",
            table: "EstateManagedAssets",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "RentBillingActivatedAt",
            table: "EstateManagedAssets",
            type: "datetime2",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AutoGenerateRentInvoices",
            table: "EstateManagedAssets");

        migrationBuilder.DropColumn(
            name: "LastRentInvoiceId",
            table: "EstateManagedAssets");

        migrationBuilder.DropColumn(
            name: "LastRentInvoiceNumber",
            table: "EstateManagedAssets");

        migrationBuilder.DropColumn(
            name: "NextRentBillingDate",
            table: "EstateManagedAssets");

        migrationBuilder.DropColumn(
            name: "RentBillingActivatedAt",
            table: "EstateManagedAssets");
    }
}
