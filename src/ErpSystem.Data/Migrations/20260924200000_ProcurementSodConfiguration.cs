using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260924200000_ProcurementSodConfiguration")]
public sealed class ProcurementSodConfiguration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "EnforceSegregationOfDuties", table: "ProcurementSettings", type: "bit", nullable: false, defaultValue: true);
        migrationBuilder.Sql(TenantPolicyFunction);
        migrationBuilder.Sql(ApPolicyFunction);
        ProcurementSodTriggerSql.Apply(migrationBuilder, enabled: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ProcurementSodTriggerSql.Apply(migrationBuilder, enabled: false);
        migrationBuilder.Sql("DROP FUNCTION [dbo].[ProcurementApSodRequired]; DROP FUNCTION [dbo].[ProcurementSodRequired];");
        migrationBuilder.DropColumn(name: "EnforceSegregationOfDuties", table: "ProcurementSettings");
    }

    internal const string TenantPolicyFunction = """
        CREATE OR ALTER FUNCTION [dbo].[ProcurementSodRequired](@tenantId uniqueidentifier)
        RETURNS bit
        AS
        BEGIN
            IF (SELECT COUNT(*) FROM dbo.ProcurementSettings WHERE TenantId = @tenantId AND IsDeleted = 0) <> 1 RETURN 1;
            DECLARE @required bit = 1;
            SELECT @required = EnforceSegregationOfDuties FROM dbo.ProcurementSettings
            WHERE TenantId = @tenantId AND IsDeleted = 0;
            RETURN ISNULL(@required, 1);
        END
        """;

    internal const string ApPolicyFunction = """
        CREATE OR ALTER FUNCTION [dbo].[ProcurementApSodRequired]
            (@tenantId uniqueidentifier, @sourceType varchar(32), @sourceId uniqueidentifier)
        RETURNS bit
        AS
        BEGIN
            IF dbo.ProcurementSodRequired(@tenantId) = 1 RETURN 1;
            DECLARE @invoices TABLE (Id uniqueidentifier PRIMARY KEY);
            IF @sourceType = 'VendorInvoice'
                INSERT @invoices VALUES (@sourceId);
            ELSE IF @sourceType = 'PaymentBatch'
                INSERT @invoices SELECT DISTINCT VendorInvoiceId FROM dbo.PaymentBatchInvoice
                    WHERE TenantId = @tenantId AND PaymentBatchId = @sourceId AND IsDeleted = 0;
            ELSE IF @sourceType = 'VendorPayment'
                INSERT @invoices SELECT DISTINCT a.VendorInvoiceId FROM dbo.VendorPaymentAllocation a
                    WHERE a.TenantId = @tenantId AND a.VendorPaymentId = @sourceId AND a.IsDeleted = 0 AND a.IsReversal = 0
                      AND NOT EXISTS (SELECT 1 FROM dbo.VendorPaymentAllocation r
                        WHERE r.TenantId = a.TenantId AND r.VendorPaymentId = a.VendorPaymentId AND r.IsDeleted = 0
                          AND r.IsReversal = 1 AND r.OriginalAllocationId = a.Id);
            ELSE RETURN 1;
            IF NOT EXISTS (SELECT 1 FROM @invoices) RETURN 1;
            IF EXISTS (
                SELECT 1 FROM @invoices selected
                LEFT JOIN dbo.VendorInvoice invoice ON invoice.Id = selected.Id AND invoice.TenantId = @tenantId AND invoice.IsDeleted = 0
                WHERE invoice.Id IS NULL OR (
                    invoice.AcceptedSupplyKind IS NULL AND
                    NOT EXISTS (SELECT 1 FROM dbo.PurchaseOrders po WHERE po.Id = invoice.PurchaseOrderId AND po.TenantId = @tenantId AND po.IsDeleted = 0) AND
                    NOT EXISTS (SELECT 1 FROM dbo.VendorInvoiceLineItem line WHERE line.VendorInvoiceId = invoice.Id AND line.TenantId = @tenantId AND line.IsDeleted = 0 AND line.LandedCostItemId IS NOT NULL)))
                RETURN 1;
            RETURN 0;
        END
        """;
}
