using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ErpSystem.Data.ApplicationDbContext))]
[Migration("20260729171500_TDC0403SourceCapacityEnforcement")]
public partial class TDC0403SourceCapacityEnforcement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS (
                SELECT 1
                FROM [PurchaseOrders]
                WHERE [ProcurementSourceType] IN (0, 1, 3)
                GROUP BY
                    [TenantId],
                    [ProcurementSourceType],
                    [ProcurementSourceId],
                    [BusinessPartnerId]
                HAVING COUNT_BIG(*) > 1)
            BEGIN
                THROW 51205, 'Duplicate one-time approved-source purchase orders must be remediated before capacity enforcement can be enabled.', 1;
            END;
            """);

        migrationBuilder.CreateIndex(
            name: "UX_PurchaseOrders_OneTimeApprovedSource",
            table: "PurchaseOrders",
            columns: new[]
            {
                "TenantId",
                "ProcurementSourceType",
                "ProcurementSourceId",
                "BusinessPartnerId"
            },
            unique: true,
            filter: "[ProcurementSourceType] IN (0, 1, 3)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "UX_PurchaseOrders_OneTimeApprovedSource",
            table: "PurchaseOrders");
    }
}
