using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260914151100_NormalizeEstatePropertyListingEnquirySource")]
public sealed class NormalizeEstatePropertyListingEnquirySource : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE EhcTickets
            SET PropertyListingContextJson=JSON_MODIFY(PropertyListingContextJson, '$.Source', 'estate-public-listing')
            WHERE PropertyListingContextJson IS NOT NULL
              AND ISJSON(PropertyListingContextJson)=1
              AND JSON_VALUE(PropertyListingContextJson, '$.Source')=N'state-public-listing';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep the corrected Estate source in operational enquiry snapshots.
    }
}
