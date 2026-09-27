using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260924170000_TenderBidItemDocuments")]
public sealed class TenderBidItemDocuments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(name: "TenderBidItemId", table: "TenderBidDocuments",
            type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddUniqueConstraint(name: "AK_TenderBidItems_TenantId_TenderBidId_Id",
            table: "TenderBidItems", columns: new[] { "TenantId", "TenderBidId", "Id" });
        migrationBuilder.CreateIndex(name: "IX_TenderBidDocuments_TenantId_TenderBidId_TenderBidItemId",
            table: "TenderBidDocuments", columns: new[] { "TenantId", "TenderBidId", "TenderBidItemId" });
        migrationBuilder.AddForeignKey(name: "FK_TenderBidDocuments_TenderBidItems_TenantId_TenderBidId_TenderBidItemId",
            table: "TenderBidDocuments", columns: new[] { "TenantId", "TenderBidId", "TenderBidItemId" },
            principalTable: "TenderBidItems", principalColumns: new[] { "TenantId", "TenderBidId", "Id" },
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_TenderBidDocuments_TenderBidItems_TenantId_TenderBidId_TenderBidItemId", table: "TenderBidDocuments");
        migrationBuilder.DropIndex(name: "IX_TenderBidDocuments_TenantId_TenderBidId_TenderBidItemId", table: "TenderBidDocuments");
        migrationBuilder.DropUniqueConstraint(name: "AK_TenderBidItems_TenantId_TenderBidId_Id", table: "TenderBidItems");
        migrationBuilder.DropColumn(name: "TenderBidItemId", table: "TenderBidDocuments");
    }
}
