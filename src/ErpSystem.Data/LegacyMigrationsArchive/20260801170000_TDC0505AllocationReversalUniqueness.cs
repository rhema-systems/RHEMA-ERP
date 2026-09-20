using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ErpSystem.Data.ApplicationDbContext))]
[Migration("20260801170000_TDC0505AllocationReversalUniqueness")]
public sealed class TDC0505AllocationReversalUniqueness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS (
                SELECT 1
                FROM dbo.VendorPaymentAllocation
                WHERE IsReversal = 1 AND OriginalAllocationId IS NOT NULL
                GROUP BY TenantId, OriginalAllocationId
                HAVING COUNT_BIG(*) > 1)
                THROW 51628, 'AP_PAYMENT_ALLOCATION_DUPLICATE_REVERSAL: duplicate allocation reversals must be resolved before applying the uniqueness guard.', 1;
            """);

        migrationBuilder.CreateIndex(
            name: "UX_VendorPaymentAllocation_TenantId_OriginalAllocationId_Reversal",
            table: "VendorPaymentAllocation",
            columns: new[] { "TenantId", "OriginalAllocationId" },
            unique: true,
            filter: "[IsReversal] = 1 AND [OriginalAllocationId] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "UX_VendorPaymentAllocation_TenantId_OriginalAllocationId_Reversal",
            table: "VendorPaymentAllocation");
    }
}
