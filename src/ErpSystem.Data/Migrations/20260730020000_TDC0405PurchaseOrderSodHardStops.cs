using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ErpSystem.Data.ApplicationDbContext))]
[Migration("20260730020000_TDC0405PurchaseOrderSodHardStops")]
public partial class TDC0405PurchaseOrderSodHardStops : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [TR_PurchaseOrders_SodHardStop]
            ON [PurchaseOrders]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted purchaseOrder
                    WHERE purchaseOrder.IsDeleted = 0
                      AND UPPER(LTRIM(RTRIM(purchaseOrder.Status))) = 'APPROVED'
                      AND (
                           purchaseOrder.ApprovedById IS NULL
                        OR purchaseOrder.RequestedById IS NULL
                        OR purchaseOrder.CreatedById IS NULL
                        OR purchaseOrder.ApprovedById =
                           purchaseOrder.RequestedById
                        OR purchaseOrder.ApprovedById =
                           purchaseOrder.CreatedById
                      ))
                    THROW 51260, 'An Approved purchase order requires an independent approver distinct from its requester and creator.', 1;
            END
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [TR_PurchaseOrderReceipts_SodHardStop]
            ON [PurchaseOrderReceipts]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted receipt
                    LEFT JOIN PurchaseOrders purchaseOrder
                      ON purchaseOrder.Id = receipt.PurchaseOrderId
                     AND purchaseOrder.TenantId = receipt.TenantId
                     AND purchaseOrder.IsDeleted = 0
                    WHERE receipt.IsDeleted = 0
                      AND (
                           purchaseOrder.Id IS NULL
                        OR receipt.ReceivedById IS NULL
                        OR purchaseOrder.CreatedById IS NULL
                        OR receipt.ReceivedById = purchaseOrder.CreatedById
                      ))
                    THROW 51261, 'A purchase-order receipt requires a confirmer distinct from the purchase-order creator.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DROP TRIGGER IF EXISTS [TR_PurchaseOrderReceipts_SodHardStop];");
        migrationBuilder.Sql(
            "DROP TRIGGER IF EXISTS [TR_PurchaseOrders_SodHardStop];");
    }
}
