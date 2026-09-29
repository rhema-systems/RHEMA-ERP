using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260925000002_AddEstateFacilityDutyInventoryIssue")]
public partial class AddEstateFacilityDutyInventoryIssue : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "InventoryIssueVoucherId", table: "EstateFacilityDutyRosters",
            type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "InventoryIssueVoucherNumber", table: "EstateFacilityDutyRosters",
            type: "nvarchar(50)", maxLength: 50, nullable: true);
        migrationBuilder.CreateIndex(
            name: "IX_EstateFacilityDutyRosters_TenantId_InventoryIssueVoucherId",
            table: "EstateFacilityDutyRosters",
            columns: new[] { "TenantId", "InventoryIssueVoucherId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_EstateFacilityDutyRosters_TenantId_InventoryIssueVoucherId",
            table: "EstateFacilityDutyRosters");
        migrationBuilder.DropColumn(name: "InventoryIssueVoucherId", table: "EstateFacilityDutyRosters");
        migrationBuilder.DropColumn(name: "InventoryIssueVoucherNumber", table: "EstateFacilityDutyRosters");
    }
}
