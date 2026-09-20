using ErpSystem.Data.Seeders;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260914151000_EhcPropertyEnquiryNotificationHandoff")]
public sealed class EhcPropertyEnquiryNotificationHandoff : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(EhcPropertyEnquiryNotificationHandoffConfiguration.Sql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Retain operational notification history and administrator configuration.
    }
}
