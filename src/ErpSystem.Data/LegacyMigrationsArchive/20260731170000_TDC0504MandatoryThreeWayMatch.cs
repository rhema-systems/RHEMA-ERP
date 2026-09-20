using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ErpSystem.Data.ApplicationDbContext))]
[Migration("20260731170000_TDC0504MandatoryThreeWayMatch")]
public sealed class TDC0504MandatoryThreeWayMatch : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "ApInvoicePriceTolerancePercent",
            table: "FinanceSettings",
            type: "decimal(5,2)",
            nullable: false,
            defaultValue: 1m);
        migrationBuilder.AddColumn<decimal>(
            name: "ApInvoiceQuantityTolerancePercent",
            table: "FinanceSettings",
            type: "decimal(5,2)",
            nullable: false,
            defaultValue: 1m);

        migrationBuilder.AddColumn<Guid>(
            name: "MatchingControlEventId",
            table: "VendorInvoice",
            type: "uniqueidentifier",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "MatchingSnapshotHash",
            table: "VendorInvoice",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "MatchingEvaluatedAtUtc",
            table: "VendorInvoice",
            type: "datetime2",
            nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "MatchingPriceTolerancePercent",
            table: "VendorInvoice",
            type: "decimal(5,2)",
            nullable: false,
            defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>(
            name: "MatchingQuantityTolerancePercent",
            table: "VendorInvoice",
            type: "decimal(5,2)",
            nullable: false,
            defaultValue: 0m);
        migrationBuilder.AddColumn<Guid>(
            name: "MatchExceptionControlEventId",
            table: "VendorInvoice",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_FinanceSettings_TDC0504ApMatchTolerances",
            table: "FinanceSettings",
            sql: "[ApInvoicePriceTolerancePercent] BETWEEN 0 AND 100 AND [ApInvoiceQuantityTolerancePercent] BETWEEN 0 AND 100");
        migrationBuilder.AddCheckConstraint(
            name: "CK_VendorInvoice_TDC0504MatchingTolerances",
            table: "VendorInvoice",
            sql: "[MatchingPriceTolerancePercent] BETWEEN 0 AND 100 AND [MatchingQuantityTolerancePercent] BETWEEN 0 AND 100");
        migrationBuilder.AddCheckConstraint(
            name: "CK_VendorInvoice_TDC0504SnapshotHash",
            table: "VendorInvoice",
            sql: "[MatchingSnapshotHash] IS NULL OR LEN([MatchingSnapshotHash]) = 64");

        migrationBuilder.CreateIndex(
            name: "IX_VendorInvoice_TenantId_MatchingControlEventId",
            table: "VendorInvoice",
            columns: new[] { "TenantId", "MatchingControlEventId" });
        migrationBuilder.CreateIndex(
            name: "IX_VendorInvoice_TenantId_MatchExceptionControlEventId",
            table: "VendorInvoice",
            columns: new[] { "TenantId", "MatchExceptionControlEventId" });
        migrationBuilder.AddForeignKey(
            name: "FK_VendorInvoice_ProcurementControlEvents_MatchingControlEventId",
            table: "VendorInvoice",
            column: "MatchingControlEventId",
            principalTable: "ProcurementControlEvents",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_VendorInvoice_ProcurementControlEvents_MatchExceptionControlEventId",
            table: "VendorInvoice",
            column: "MatchExceptionControlEventId",
            principalTable: "ProcurementControlEvents",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_VendorInvoice_TDC0504MandatoryMatch]
            ON [dbo].[VendorInvoice]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted currentRow
                    JOIN deleted priorRow ON priorRow.Id = currentRow.Id
                    WHERE currentRow.IsDeleted = 0
                      AND currentRow.Status NOT IN (1, 8)
                      AND (
                           ISNULL(CONVERT(nvarchar(36), currentRow.PurchaseOrderId), '') <> ISNULL(CONVERT(nvarchar(36), priorRow.PurchaseOrderId), '')
                        OR currentRow.SupplierId <> priorRow.SupplierId
                        OR currentRow.CurrencyCode <> priorRow.CurrencyCode
                        OR currentRow.SubTotal <> priorRow.SubTotal
                        OR currentRow.TaxAmount <> priorRow.TaxAmount
                        OR currentRow.DiscountAmount <> priorRow.DiscountAmount
                        OR currentRow.TotalAmount <> priorRow.TotalAmount
                        OR currentRow.IsOpeningBalance <> priorRow.IsOpeningBalance
                      ))
                    THROW 51600, 'AP_MATCH_IMMUTABLE: approved or submitted invoice matching inputs cannot be changed.', 1;

                UPDATE invoice
                   SET MatchingStatus = 0,
                       MatchingNotes = N'Matching invalidated because an invoice matching input changed.',
                       MatchingControlEventId = NULL,
                       MatchingSnapshotHash = NULL,
                       MatchingEvaluatedAtUtc = NULL,
                       MatchingPriceTolerancePercent = 0,
                       MatchingQuantityTolerancePercent = 0,
                       MatchExceptionControlEventId = NULL
                FROM VendorInvoice invoice
                JOIN inserted currentRow ON currentRow.Id = invoice.Id
                JOIN deleted priorRow ON priorRow.Id = currentRow.Id
                WHERE currentRow.IsDeleted = 0
                  AND currentRow.Status IN (1, 8)
                  AND (
                       ISNULL(CONVERT(nvarchar(36), currentRow.PurchaseOrderId), '') <> ISNULL(CONVERT(nvarchar(36), priorRow.PurchaseOrderId), '')
                    OR currentRow.SupplierId <> priorRow.SupplierId
                    OR currentRow.CurrencyCode <> priorRow.CurrencyCode
                    OR currentRow.SubTotal <> priorRow.SubTotal
                    OR currentRow.TaxAmount <> priorRow.TaxAmount
                    OR currentRow.DiscountAmount <> priorRow.DiscountAmount
                    OR currentRow.TotalAmount <> priorRow.TotalAmount
                    OR currentRow.IsOpeningBalance <> priorRow.IsOpeningBalance
                  );

                IF EXISTS (
                    SELECT 1
                    FROM inserted invoice
                    LEFT JOIN ProcurementControlEvents evaluation
                      ON evaluation.Id = invoice.MatchingControlEventId
                     AND evaluation.TenantId = invoice.TenantId
                     AND evaluation.SourceId = invoice.Id
                     AND evaluation.SourceType = 'VendorInvoice'
                     AND evaluation.EventType = 'InvoiceThreeWayMatching'
                     AND evaluation.Action = 'InvoiceThreeWayMatchEvaluated'
                     AND evaluation.RuleCode = 'AP-002'
                     AND evaluation.RuleVersion = 'TDC-0504'
                     AND evaluation.Result = 2
                     AND evaluation.IsDeleted = 0
                    WHERE invoice.IsDeleted = 0
                      AND invoice.PurchaseOrderId IS NOT NULL
                      AND invoice.IsOpeningBalance = 0
                      AND invoice.Status IN (2, 3)
                      AND (
                           invoice.MatchingType <> 2
                        OR invoice.MatchingControlEventId IS NULL
                        OR invoice.MatchingSnapshotHash IS NULL
                        OR invoice.MatchingEvaluatedAtUtc IS NULL
                        OR evaluation.Id IS NULL
                        OR evaluation.OccurredAtUtc <> invoice.MatchingEvaluatedAtUtc
                        OR ISJSON(evaluation.ResultValuesJson) <> 1
                        OR JSON_VALUE(evaluation.ResultValuesJson, '$.approvalReady') <> 'true'
                        OR JSON_VALUE(evaluation.ResultValuesJson, '$.snapshotHash') <> invoice.MatchingSnapshotHash
                        OR EXISTS (
                            SELECT required.DecisionKey
                            FROM (VALUES
                                ('DEC-001'), ('DEC-002'), ('DEC-003'), ('DEC-004'),
                                ('DEC-005'), ('DEC-006'), ('DEC-007'), ('DEC-008'),
                                ('DEC-009'), ('DEC-010'), ('DEC-011'), ('DEC-012'),
                                ('DEC-013'), ('DEC-014')) required(DecisionKey)
                            WHERE NOT EXISTS (
                                SELECT 1
                                FROM OPENJSON(CASE WHEN ISJSON(evaluation.DecisionKeysJson) = 1
                                                   THEN evaluation.DecisionKeysJson ELSE '[]' END) decisionKey
                                WHERE decisionKey.[value] = required.DecisionKey))
                        OR (
                            invoice.MatchingStatus <> 2
                            AND NOT EXISTS (
                                SELECT 1
                                FROM ProcurementControlEvents exceptionEvent
                                JOIN WorkflowInstances workflow
                                  ON workflow.Id = TRY_CONVERT(uniqueidentifier,
                                      JSON_VALUE(exceptionEvent.ResultValuesJson, '$.workflowInstanceId'))
                                 AND workflow.TenantId = invoice.TenantId
                                 AND workflow.Status = 2
                                 AND workflow.IsDeleted = 0
                                WHERE exceptionEvent.Id = invoice.MatchExceptionControlEventId
                                  AND exceptionEvent.TenantId = invoice.TenantId
                                  AND exceptionEvent.SourceId = invoice.Id
                                  AND exceptionEvent.SourceType = 'VendorInvoice'
                                  AND exceptionEvent.Action = 'InvoiceMatchExceptionApproved'
                                  AND exceptionEvent.RuleCode = 'AP-006'
                                  AND exceptionEvent.RuleVersion = 'TDC-0507'
                                  AND exceptionEvent.Result IN (0, 2)
                                  AND exceptionEvent.IsDeleted = 0
                                  AND ISJSON(exceptionEvent.ResultValuesJson) = 1
                                  AND TRY_CONVERT(uniqueidentifier,
                                      JSON_VALUE(exceptionEvent.ResultValuesJson, '$.purchaseOrderId')) = invoice.PurchaseOrderId
                                  AND JSON_VALUE(exceptionEvent.ResultValuesJson, '$.invoiceSnapshotHash') = invoice.MatchingSnapshotHash
                                  AND TRY_CONVERT(datetime2,
                                      JSON_VALUE(exceptionEvent.ResultValuesJson, '$.expiresAtUtc')) > SYSUTCDATETIME()
                                  AND workflow.InitiatedById <> exceptionEvent.ActorUserId
                                  AND (invoice.SubmittedById IS NULL OR invoice.SubmittedById <> exceptionEvent.ActorUserId)
                                  AND EXISTS (
                                      SELECT 1 FROM ProcurementControlEventEvidenceLinks exceptionEvidence
                                      WHERE exceptionEvidence.ControlEventId = exceptionEvent.Id
                                        AND exceptionEvidence.TenantId = invoice.TenantId
                                        AND exceptionEvidence.IsDeleted = 0)
                                  AND EXISTS (
                                      SELECT 1 FROM ProcurementControlEventEvidenceLinks evaluationEvidence
                                      WHERE evaluationEvidence.ControlEventId = evaluation.Id
                                        AND evaluationEvidence.TenantId = invoice.TenantId
                                        AND evaluationEvidence.RequirementKey = 'AP-006'
                                        AND evaluationEvidence.Reference = CONVERT(nvarchar(36), exceptionEvent.Id)
                                        AND evaluationEvidence.IsDeleted = 0)
                            )
                        )
                      ))
                    THROW 51601, 'AP_THREE_WAY_MATCH_APPROVAL_BLOCKED: a PO-linked invoice requires a current AP-002/TDC-0504 three-way match or independently approved AP-006/TDC-0507 exception.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted invoice
                    WHERE invoice.IsDeleted = 0
                      AND invoice.PurchaseOrderId IS NOT NULL
                      AND invoice.IsOpeningBalance = 0
                      AND invoice.Status IN (2, 3)
                      AND (
                           EXISTS (SELECT 1 FROM PurchaseOrders purchaseOrder
                                   WHERE purchaseOrder.Id = invoice.PurchaseOrderId
                                     AND purchaseOrder.TenantId = invoice.TenantId
                                     AND COALESCE(purchaseOrder.DeletedAt, purchaseOrder.UpdatedAt, purchaseOrder.CreatedAt) > invoice.MatchingEvaluatedAtUtc)
                        OR EXISTS (SELECT 1 FROM PurchaseOrderItems orderLine
                                   WHERE orderLine.PurchaseOrderId = invoice.PurchaseOrderId
                                     AND orderLine.TenantId = invoice.TenantId
                                     AND COALESCE(orderLine.DeletedAt, orderLine.UpdatedAt, orderLine.CreatedAt) > invoice.MatchingEvaluatedAtUtc)
                        OR EXISTS (SELECT 1 FROM PurchaseOrderReceipts receipt
                                   WHERE receipt.PurchaseOrderId = invoice.PurchaseOrderId
                                     AND receipt.TenantId = invoice.TenantId
                                     AND COALESCE(receipt.DeletedAt, receipt.UpdatedAt, receipt.CreatedAt) > invoice.MatchingEvaluatedAtUtc)
                        OR EXISTS (SELECT 1 FROM PurchaseOrderReceiptItems receiptLine
                                   JOIN PurchaseOrderReceipts receipt ON receipt.Id = receiptLine.ReceiptId
                                   WHERE receipt.PurchaseOrderId = invoice.PurchaseOrderId
                                     AND receiptLine.TenantId = invoice.TenantId
                                     AND COALESCE(receiptLine.DeletedAt, receiptLine.UpdatedAt, receiptLine.CreatedAt) > invoice.MatchingEvaluatedAtUtc)
                        OR EXISTS (SELECT 1 FROM ProcurementReceiptInspectionCases inspection
                                   JOIN PurchaseOrderReceipts receipt ON receipt.Id = inspection.PurchaseOrderReceiptId
                                   WHERE receipt.PurchaseOrderId = invoice.PurchaseOrderId
                                     AND inspection.TenantId = invoice.TenantId
                                     AND COALESCE(inspection.DeletedAt, inspection.UpdatedAt, inspection.CreatedAt) > invoice.MatchingEvaluatedAtUtc)
                        OR EXISTS (SELECT 1 FROM ProcurementReceiptInspectionLines inspectionLine
                                   JOIN ProcurementReceiptInspectionCases inspection ON inspection.Id = inspectionLine.InspectionCaseId
                                   JOIN PurchaseOrderReceipts receipt ON receipt.Id = inspection.PurchaseOrderReceiptId
                                   WHERE receipt.PurchaseOrderId = invoice.PurchaseOrderId
                                     AND inspectionLine.TenantId = invoice.TenantId
                                     AND COALESCE(inspectionLine.DeletedAt, inspectionLine.UpdatedAt, inspectionLine.CreatedAt) > invoice.MatchingEvaluatedAtUtc)
                      ))
                    THROW 51603, 'AP_THREE_WAY_MATCH_STALE: PO, receipt, or independent inspection evidence changed after the invoice match evaluation.', 1;
            END
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_VendorInvoiceLineItem_TDC0504MatchIntegrity]
            ON [dbo].[VendorInvoiceLineItem]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                DECLARE @parents TABLE (InvoiceId uniqueidentifier PRIMARY KEY);
                INSERT INTO @parents (InvoiceId)
                SELECT VendorInvoiceId FROM inserted
                UNION
                SELECT VendorInvoiceId FROM deleted;

                IF EXISTS (
                    SELECT 1
                    FROM @parents parent
                    JOIN VendorInvoice invoice ON invoice.Id = parent.InvoiceId
                    WHERE invoice.IsDeleted = 0 AND invoice.Status NOT IN (1, 8))
                    THROW 51602, 'AP_MATCH_LINE_IMMUTABLE: submitted or approved invoice lines cannot be changed.', 1;

                UPDATE invoice
                   SET MatchingStatus = 0,
                       MatchingNotes = N'Matching invalidated because an invoice line changed.',
                       MatchingControlEventId = NULL,
                       MatchingSnapshotHash = NULL,
                       MatchingEvaluatedAtUtc = NULL,
                       MatchingPriceTolerancePercent = 0,
                       MatchingQuantityTolerancePercent = 0,
                       MatchExceptionControlEventId = NULL
                FROM VendorInvoice invoice
                JOIN @parents parent ON parent.InvoiceId = invoice.Id
                WHERE invoice.Status IN (1, 8);
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_VendorInvoiceLineItem_TDC0504MatchIntegrity];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_VendorInvoice_TDC0504MandatoryMatch];");
        migrationBuilder.DropForeignKey(
            name: "FK_VendorInvoice_ProcurementControlEvents_MatchExceptionControlEventId",
            table: "VendorInvoice");
        migrationBuilder.DropForeignKey(
            name: "FK_VendorInvoice_ProcurementControlEvents_MatchingControlEventId",
            table: "VendorInvoice");
        migrationBuilder.DropIndex(
            name: "IX_VendorInvoice_TenantId_MatchExceptionControlEventId",
            table: "VendorInvoice");
        migrationBuilder.DropIndex(
            name: "IX_VendorInvoice_TenantId_MatchingControlEventId",
            table: "VendorInvoice");
        migrationBuilder.DropCheckConstraint(
            name: "CK_VendorInvoice_TDC0504SnapshotHash",
            table: "VendorInvoice");
        migrationBuilder.DropCheckConstraint(
            name: "CK_VendorInvoice_TDC0504MatchingTolerances",
            table: "VendorInvoice");
        migrationBuilder.DropCheckConstraint(
            name: "CK_FinanceSettings_TDC0504ApMatchTolerances",
            table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "MatchExceptionControlEventId", table: "VendorInvoice");
        migrationBuilder.DropColumn(name: "MatchingControlEventId", table: "VendorInvoice");
        migrationBuilder.DropColumn(name: "MatchingEvaluatedAtUtc", table: "VendorInvoice");
        migrationBuilder.DropColumn(name: "MatchingPriceTolerancePercent", table: "VendorInvoice");
        migrationBuilder.DropColumn(name: "MatchingQuantityTolerancePercent", table: "VendorInvoice");
        migrationBuilder.DropColumn(name: "MatchingSnapshotHash", table: "VendorInvoice");
        migrationBuilder.DropColumn(name: "ApInvoicePriceTolerancePercent", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "ApInvoiceQuantityTolerancePercent", table: "FinanceSettings");
    }
}
