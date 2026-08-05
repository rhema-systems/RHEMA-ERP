using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260802033000_TDC0506EffectivePaymentAllocations")]
public sealed class TDC0506EffectivePaymentAllocations : Migration
{
    private const string EffectiveAllocationPredicate =
        """
                              AND NOT EXISTS (
                                  SELECT 1
                                  FROM VendorPaymentAllocation reversal
                                  WHERE reversal.TenantId = allocation.TenantId
                                    AND reversal.VendorPaymentId = allocation.VendorPaymentId
                                    AND reversal.IsDeleted = 0
                                    AND reversal.IsReversal = 1
                                    AND reversal.OriginalAllocationId = allocation.Id
                              )
        """;

    protected override void Up(MigrationBuilder migrationBuilder) =>
        CreateTrigger(migrationBuilder, EffectiveAllocationPredicate);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        CreateTrigger(migrationBuilder, string.Empty);

    private static void CreateTrigger(
        MigrationBuilder migrationBuilder,
        string effectiveAllocationPredicate)
    {
        migrationBuilder.Sql(
            $$"""
            CREATE OR ALTER TRIGGER [dbo].[TR_VendorPayment_TDC0506InvoiceProcessorSod]
            ON [dbo].[VendorPayment]
            AFTER UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted payment
                    JOIN deleted priorPayment ON priorPayment.Id = payment.Id
                    LEFT JOIN ProcurementControlEvents sodEvent
                      ON sodEvent.Id = payment.InvoicePaymentSodControlEventId
                     AND sodEvent.TenantId = payment.TenantId
                     AND sodEvent.EventType = 'ProcurementInvoicePaymentSod'
                     AND sodEvent.Action = 'ApprovePayment'
                     AND sodEvent.Result = 2
                     AND sodEvent.RuleCode = 'AP-004'
                     AND sodEvent.RuleVersion = 'TDC-0506'
                     AND sodEvent.SourceType = CASE WHEN payment.PaymentBatchId IS NULL THEN 'VendorPayment' ELSE 'PaymentBatch' END
                     AND sodEvent.SourceId = ISNULL(payment.PaymentBatchId, payment.Id)
                     AND sodEvent.ActorUserId = payment.AuthorizedById
                     AND sodEvent.IsDeleted = 0
                    WHERE payment.IsDeleted = 0
                      AND (payment.Status IN (3,4,5,8) OR payment.JournalEntryId IS NOT NULL)
                      AND (payment.Status <> priorPayment.Status
                           OR ISNULL(payment.JournalEntryId, '00000000-0000-0000-0000-000000000000') <> ISNULL(priorPayment.JournalEntryId, '00000000-0000-0000-0000-000000000000')
                           OR ISNULL(payment.AuthorizedById, '00000000-0000-0000-0000-000000000000') <> ISNULL(priorPayment.AuthorizedById, '00000000-0000-0000-0000-000000000000')
                           OR ISNULL(payment.InvoicePaymentSodControlEventId, '00000000-0000-0000-0000-000000000000') <> ISNULL(priorPayment.InvoicePaymentSodControlEventId, '00000000-0000-0000-0000-000000000000'))
                      AND NOT (
                           priorPayment.JournalEntryId IS NOT NULL
                       AND priorPayment.InvoicePaymentSodControlEventId IS NULL
                       AND payment.JournalEntryId = priorPayment.JournalEntryId
                       AND payment.InvoicePaymentSodControlEventId IS NULL
                       AND ISNULL(payment.AuthorizedById, '00000000-0000-0000-0000-000000000000') = ISNULL(priorPayment.AuthorizedById, '00000000-0000-0000-0000-000000000000')
                      )
                      AND (
                           payment.AuthorizedById IS NULL
                        OR sodEvent.Id IS NULL
                        OR EXISTS (
                            SELECT 1
                            FROM VendorPaymentAllocation allocation
                            LEFT JOIN VendorInvoice invoice
                              ON invoice.Id = allocation.VendorInvoiceId
                             AND invoice.TenantId = payment.TenantId
                             AND invoice.IsDeleted = 0
                            WHERE allocation.VendorPaymentId = payment.Id
                              AND allocation.TenantId = payment.TenantId
                              AND allocation.IsDeleted = 0
                              AND allocation.IsReversal = 0
            {{effectiveAllocationPredicate}}
                              AND (invoice.Id IS NULL OR invoice.SubmittedById IS NULL OR invoice.SubmittedById = payment.AuthorizedById)
                        )
                        OR (payment.PaymentBatchId IS NOT NULL AND EXISTS (
                            SELECT 1
                            FROM PaymentBatchInvoice selection
                            LEFT JOIN VendorInvoice invoice
                              ON invoice.Id = selection.VendorInvoiceId
                             AND invoice.TenantId = payment.TenantId
                             AND invoice.IsDeleted = 0
                            WHERE selection.VendorPaymentId = payment.Id
                              AND selection.PaymentBatchId = payment.PaymentBatchId
                              AND selection.TenantId = payment.TenantId
                              AND selection.IsDeleted = 0
                              AND (invoice.Id IS NULL OR invoice.SubmittedById IS NULL OR invoice.SubmittedById = payment.AuthorizedById)
                        ))
                      ))
                    THROW 51642, 'AP_PAYMENT_SOD_BLOCKED: the payment approver must be independent of every invoice processor and retain AP-004 evidence.', 1;
            END;
            """);
    }
}
