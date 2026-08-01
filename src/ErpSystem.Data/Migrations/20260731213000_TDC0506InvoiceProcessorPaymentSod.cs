using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ErpSystem.Data.ApplicationDbContext))]
[Migration("20260731213000_TDC0506InvoiceProcessorPaymentSod")]
public sealed class TDC0506InvoiceProcessorPaymentSod : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "InvoicePaymentSodControlEventId",
            table: "VendorPayment",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "InvoicePaymentSodControlEventId",
            table: "PaymentBatch",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_VendorPayment_TenantId_InvoicePaymentSodControlEventId",
            table: "VendorPayment",
            columns: new[] { "TenantId", "InvoicePaymentSodControlEventId" });

        migrationBuilder.CreateIndex(
            name: "IX_PaymentBatch_TenantId_InvoicePaymentSodControlEventId",
            table: "PaymentBatch",
            columns: new[] { "TenantId", "InvoicePaymentSodControlEventId" });

        migrationBuilder.AddForeignKey(
            name: "FK_VendorPayment_ProcurementControlEvents_InvoicePaymentSodControlEventId",
            table: "VendorPayment",
            column: "InvoicePaymentSodControlEventId",
            principalTable: "ProcurementControlEvents",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_PaymentBatch_ProcurementControlEvents_InvoicePaymentSodControlEventId",
            table: "PaymentBatch",
            column: "InvoicePaymentSodControlEventId",
            principalTable: "ProcurementControlEvents",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_PaymentBatch_TDC0506InvoiceProcessorSod]
            ON [dbo].[PaymentBatch]
            AFTER UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted batch
                    JOIN deleted priorBatch ON priorBatch.Id = batch.Id
                    LEFT JOIN ProcurementControlEvents sodEvent
                      ON sodEvent.Id = batch.InvoicePaymentSodControlEventId
                     AND sodEvent.TenantId = batch.TenantId
                     AND sodEvent.EventType = 'ProcurementInvoicePaymentSod'
                     AND sodEvent.Action = 'ApprovePayment'
                     AND sodEvent.Result = 2
                     AND sodEvent.RuleCode = 'AP-004'
                     AND sodEvent.RuleVersion = 'TDC-0506'
                     AND sodEvent.SourceType = 'PaymentBatch'
                     AND sodEvent.SourceId = batch.Id
                     AND sodEvent.ActorUserId = batch.ApprovedById
                     AND sodEvent.IsDeleted = 0
                    WHERE batch.IsDeleted = 0
                      AND batch.Status IN (3,4,5,6)
                      AND (batch.Status <> priorBatch.Status
                           OR ISNULL(batch.ApprovedById, '00000000-0000-0000-0000-000000000000') <> ISNULL(priorBatch.ApprovedById, '00000000-0000-0000-0000-000000000000')
                           OR ISNULL(batch.InvoicePaymentSodControlEventId, '00000000-0000-0000-0000-000000000000') <> ISNULL(priorBatch.InvoicePaymentSodControlEventId, '00000000-0000-0000-0000-000000000000'))
                      AND (
                           batch.ApprovedById IS NULL
                        OR sodEvent.Id IS NULL
                        OR EXISTS (
                            SELECT 1
                            FROM PaymentBatchInvoice selection
                            LEFT JOIN VendorInvoice invoice
                              ON invoice.Id = selection.VendorInvoiceId
                             AND invoice.TenantId = batch.TenantId
                             AND invoice.IsDeleted = 0
                            WHERE selection.PaymentBatchId = batch.Id
                              AND selection.TenantId = batch.TenantId
                              AND selection.IsDeleted = 0
                              AND (invoice.Id IS NULL OR invoice.SubmittedById IS NULL OR invoice.SubmittedById = batch.ApprovedById)
                        )
                      ))
                    THROW 51641, 'AP_PAYMENT_BATCH_SOD_BLOCKED: the batch approver must be independent of every selected invoice processor and retain AP-004 evidence.', 1;
            END;
            """);

        migrationBuilder.Sql(
            """
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

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_VendorPayment_TDC0506InvoiceProcessorSod];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_PaymentBatch_TDC0506InvoiceProcessorSod];");

        migrationBuilder.DropForeignKey(
            name: "FK_VendorPayment_ProcurementControlEvents_InvoicePaymentSodControlEventId",
            table: "VendorPayment");
        migrationBuilder.DropForeignKey(
            name: "FK_PaymentBatch_ProcurementControlEvents_InvoicePaymentSodControlEventId",
            table: "PaymentBatch");
        migrationBuilder.DropIndex(
            name: "IX_VendorPayment_TenantId_InvoicePaymentSodControlEventId",
            table: "VendorPayment");
        migrationBuilder.DropIndex(
            name: "IX_PaymentBatch_TenantId_InvoicePaymentSodControlEventId",
            table: "PaymentBatch");
        migrationBuilder.DropColumn(name: "InvoicePaymentSodControlEventId", table: "VendorPayment");
        migrationBuilder.DropColumn(name: "InvoicePaymentSodControlEventId", table: "PaymentBatch");
    }
}
