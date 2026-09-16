using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260914152000_EhcPropertyEnquiryEstateHandoff")]
public sealed class EhcPropertyEnquiryEstateHandoff : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "EstateListingApplicationCaseId",
            table: "EhcTickets",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EstateListingApplicationReference",
            table: "EhcTickets",
            type: "nvarchar(80)",
            maxLength: 80,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "EstateListingApplicationHandedOffAt",
            table: "EhcTickets",
            type: "datetime2",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_EhcTickets_TenantId_EstateListingApplicationCaseId",
            table: "EhcTickets",
            columns: new[] { "TenantId", "EstateListingApplicationCaseId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_EhcTickets_TenantId_EstateListingApplicationCaseId",
            table: "EhcTickets");
        migrationBuilder.DropColumn(name: "EstateListingApplicationCaseId", table: "EhcTickets");
        migrationBuilder.DropColumn(name: "EstateListingApplicationReference", table: "EhcTickets");
        migrationBuilder.DropColumn(name: "EstateListingApplicationHandedOffAt", table: "EhcTickets");
    }
}
