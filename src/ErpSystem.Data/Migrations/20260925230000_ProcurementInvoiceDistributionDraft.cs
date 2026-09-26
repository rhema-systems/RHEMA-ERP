using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260925230000_ProcurementInvoiceDistributionDraft")]
public sealed class ProcurementInvoiceDistributionDraft : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>("DistributionDraftJson", "VendorInvoice", "nvarchar(max)", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM dbo.VendorInvoice WHERE DistributionDraftJson IS NOT NULL) THROW 51731, 'Preserve saved invoice distributions before rollback.', 1;");
        migrationBuilder.DropColumn("DistributionDraftJson", "VendorInvoice");
    }
}
