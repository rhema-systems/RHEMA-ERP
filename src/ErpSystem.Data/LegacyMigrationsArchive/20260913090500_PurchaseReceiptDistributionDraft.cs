using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260913090500_PurchaseReceiptDistributionDraft")]
public sealed class PurchaseReceiptDistributionDraft : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>("DistributionDraftJson", "PurchaseOrderReceipts", type: "nvarchar(max)", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn("DistributionDraftJson", "PurchaseOrderReceipts");
}
