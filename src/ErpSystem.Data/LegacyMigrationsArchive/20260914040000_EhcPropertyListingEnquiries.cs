using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ErpSystem.Data.Seeders;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260914040000_EhcPropertyListingEnquiries")]
public sealed class EhcPropertyListingEnquiries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("PropertyListingContextJson", "EhcTickets", type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<Guid>("ExternalSubmissionId", "EhcTickets", type: "uniqueidentifier", nullable: true);
        migrationBuilder.CreateIndex("IX_EhcTickets_TenantId_RequesterUserId_ExternalSubmissionId", "EhcTickets",
            new[] { "TenantId", "RequesterUserId", "ExternalSubmissionId" }, unique: true, filter: "[ExternalSubmissionId] IS NOT NULL");
        migrationBuilder.Sql(EhcPropertyEnquiryConfiguration.Sql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_EhcTickets_TenantId_RequesterUserId_ExternalSubmissionId", "EhcTickets");
        migrationBuilder.DropColumn("ExternalSubmissionId", "EhcTickets");
        migrationBuilder.DropColumn("PropertyListingContextJson", "EhcTickets");
        // Retain operational configuration and any users' ticket references.
    }
}
