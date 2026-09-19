using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ErpSystem.Data.ApplicationDbContext))]
[Migration("20260731193000_TDC0505PaymentReadiness")]
public sealed class TDC0505PaymentReadiness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "PaymentReadinessControlEventId",
            table: "VendorPaymentAllocation",
            type: "uniqueidentifier",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "PaymentReadinessSnapshotHash",
            table: "VendorPaymentAllocation",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "PaymentReadinessEvaluatedAtUtc",
            table: "VendorPaymentAllocation",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_VendorPaymentAllocation_TDC0505Snapshot",
            table: "VendorPaymentAllocation",
            sql: "[PaymentReadinessSnapshotHash] IS NULL OR LEN([PaymentReadinessSnapshotHash]) = 64");
        migrationBuilder.CreateIndex(
            name: "IX_VendorPaymentAllocation_TenantId_PaymentReadinessControlEventId",
            table: "VendorPaymentAllocation",
            columns: new[] { "TenantId", "PaymentReadinessControlEventId" });
        migrationBuilder.AddForeignKey(
            name: "FK_VendorPaymentAllocation_ProcurementControlEvents_PaymentReadinessControlEventId",
            table: "VendorPaymentAllocation",
            column: "PaymentReadinessControlEventId",
            principalTable: "ProcurementControlEvents",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.CreateTable(
            name: "PaymentBatchInvoice",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PaymentBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PaymentBatchItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VendorPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VendorInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                PaymentReadinessControlEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PaymentReadinessSnapshotHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                PaymentReadinessEvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PaymentBatchInvoice", x => x.Id);
                table.CheckConstraint(
                    "CK_PaymentBatchInvoice_TDC0505Amount",
                    "[Amount] > 0");
                table.CheckConstraint(
                    "CK_PaymentBatchInvoice_TDC0505Snapshot",
                    "LEN([PaymentReadinessSnapshotHash]) = 64");
                table.CheckConstraint(
                    "CK_PaymentBatchInvoice_TDC0505Status",
                    "[Status] IN ('Pending','Processed','Failed')");
                table.ForeignKey(
                    name: "FK_PaymentBatchInvoice_PaymentBatch_PaymentBatchId",
                    column: x => x.PaymentBatchId,
                    principalTable: "PaymentBatch",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PaymentBatchInvoice_PaymentBatchItem_PaymentBatchItemId",
                    column: x => x.PaymentBatchItemId,
                    principalTable: "PaymentBatchItem",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PaymentBatchInvoice_VendorPayment_VendorPaymentId",
                    column: x => x.VendorPaymentId,
                    principalTable: "VendorPayment",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PaymentBatchInvoice_VendorInvoice_VendorInvoiceId",
                    column: x => x.VendorInvoiceId,
                    principalTable: "VendorInvoice",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PaymentBatchInvoice_ProcurementControlEvents_PaymentReadinessControlEventId",
                    column: x => x.PaymentReadinessControlEventId,
                    principalTable: "ProcurementControlEvents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PaymentBatchInvoice_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PaymentBatchInvoice_TenantId_PaymentBatchId_VendorInvoiceId",
            table: "PaymentBatchInvoice",
            columns: new[] { "TenantId", "PaymentBatchId", "VendorInvoiceId" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_PaymentBatchInvoice_PaymentBatchItemId",
            table: "PaymentBatchInvoice",
            column: "PaymentBatchItemId");
        migrationBuilder.CreateIndex(
            name: "IX_PaymentBatchInvoice_VendorInvoiceId",
            table: "PaymentBatchInvoice",
            column: "VendorInvoiceId");
        migrationBuilder.CreateIndex(
            name: "IX_PaymentBatchInvoice_TenantId_VendorPaymentId",
            table: "PaymentBatchInvoice",
            columns: new[] { "TenantId", "VendorPaymentId" });
        migrationBuilder.CreateIndex(
            name: "IX_PaymentBatchInvoice_TenantId_PaymentReadinessControlEventId",
            table: "PaymentBatchInvoice",
            columns: new[] { "TenantId", "PaymentReadinessControlEventId" });

        migrationBuilder.Sql(PaymentReadinessValidationSql(
            "TR_VendorPaymentAllocation_TDC0505PaymentReadiness",
            "VendorPaymentAllocation",
            "allocation",
            "allocation.VendorInvoiceId",
            "allocation.PaymentReadinessControlEventId",
            "allocation.PaymentReadinessSnapshotHash",
            "allocation.PaymentReadinessEvaluatedAtUtc",
            "allocation.IsDeleted = 0 AND allocation.IsReversal = 0 AND (allocation.AllocatedAmount > 0 OR allocation.DiscountAmount > 0 OR allocation.WithholdingTaxAmount > 0)",
            "paymentEvent.Action IN ('PaymentAllocationAuthorized','SupplierAdvanceApplicationAuthorized','PaymentPostingAuthorized')",
            "(paymentEvent.Action = 'PaymentPostingAuthorized' AND invoice.Status IN (3,4,5,6)) OR (paymentEvent.Action <> 'PaymentPostingAuthorized' AND invoice.Status IN (3,4,6))",
            51621,
            "AP_PAYMENT_READINESS_BLOCKED"));

        migrationBuilder.Sql(PaymentReadinessValidationSql(
            "TR_PaymentBatchInvoice_TDC0505PaymentReadiness",
            "PaymentBatchInvoice",
            "selection",
            "selection.VendorInvoiceId",
            "selection.PaymentReadinessControlEventId",
            "selection.PaymentReadinessSnapshotHash",
            "selection.PaymentReadinessEvaluatedAtUtc",
            "selection.IsDeleted = 0",
            "paymentEvent.Action IN ('PaymentBatchInvoiceSelected','PaymentBatchInvoiceApproved','PaymentBatchInvoiceProcessed')",
            "(selection.Status IN ('Processed','Failed') AND invoice.Status IN (3,4,5,6)) OR (selection.Status = 'Pending' AND invoice.Status IN (3,4,6))",
            51622,
            "AP_PAYMENT_BATCH_INVOICE_BLOCKED",
            additionalValidation:
                "OR selection.Amount <= 0 OR (selection.Status = 'Pending' AND selection.Amount > invoice.TotalAmount - invoice.PaidAmount)"));

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_PaymentBatch_TDC0505Readiness]
            ON [dbo].[PaymentBatch]
            AFTER UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted batch
                    JOIN deleted priorBatch ON priorBatch.Id = batch.Id
                    WHERE batch.IsDeleted = 0
                      AND batch.Status IN (3,4,5,6)
                      AND batch.Status <> priorBatch.Status
                      AND (
                           batch.PaymentCount <= 0
                        OR batch.PaymentCount <> (SELECT COUNT(*) FROM PaymentBatchItem item WHERE item.PaymentBatchId = batch.Id AND item.TenantId = batch.TenantId AND item.IsDeleted = 0)
                        OR batch.TotalAmount <> (SELECT ISNULL(SUM(item.Amount), 0) FROM PaymentBatchItem item WHERE item.PaymentBatchId = batch.Id AND item.TenantId = batch.TenantId AND item.IsDeleted = 0)
                        OR EXISTS (
                            SELECT 1
                            FROM PaymentBatchItem item
                            LEFT JOIN VendorPayment payment ON payment.Id = item.VendorPaymentId AND payment.TenantId = batch.TenantId AND payment.IsDeleted = 0
                            WHERE item.PaymentBatchId = batch.Id AND item.TenantId = batch.TenantId AND item.IsDeleted = 0
                              AND (payment.Id IS NULL OR payment.PaymentBatchId <> batch.Id OR payment.TotalAmount <> item.Amount
                                   OR NOT EXISTS (SELECT 1 FROM PaymentBatchInvoice selection WHERE selection.PaymentBatchItemId = item.Id AND selection.PaymentBatchId = batch.Id AND selection.TenantId = batch.TenantId AND selection.IsDeleted = 0)
                                   OR item.Amount <> (SELECT ISNULL(SUM(selection.Amount), 0) FROM PaymentBatchInvoice selection WHERE selection.PaymentBatchItemId = item.Id AND selection.PaymentBatchId = batch.Id AND selection.TenantId = batch.TenantId AND selection.IsDeleted = 0))
                        )
                        OR EXISTS (
                            SELECT 1
                            FROM PaymentBatchInvoice selection
                            LEFT JOIN ProcurementControlEvents paymentEvent
                              ON paymentEvent.Id = selection.PaymentReadinessControlEventId
                             AND paymentEvent.TenantId = batch.TenantId
                             AND paymentEvent.SourceId = selection.VendorInvoiceId
                             AND paymentEvent.SourceType = 'VendorInvoice'
                             AND paymentEvent.EventType = 'VendorInvoicePaymentReadiness'
                             AND paymentEvent.RuleCode = 'AP-003'
                             AND paymentEvent.RuleVersion = 'TDC-0505'
                             AND paymentEvent.Result = 2
                             AND paymentEvent.IsDeleted = 0
                            WHERE selection.PaymentBatchId = batch.Id AND selection.TenantId = batch.TenantId AND selection.IsDeleted = 0
                              AND (paymentEvent.Id IS NULL
                                   OR paymentEvent.Action <> CASE WHEN batch.Status = 3 THEN 'PaymentBatchInvoiceApproved' ELSE 'PaymentBatchInvoiceProcessed' END
                                   OR paymentEvent.OccurredAtUtc <> selection.PaymentReadinessEvaluatedAtUtc
                                   OR ISJSON(paymentEvent.ResultValuesJson) <> 1
                                   OR JSON_VALUE(paymentEvent.ResultValuesJson, '$.isPaymentReady') <> 'true'
                                   OR JSON_VALUE(paymentEvent.ResultValuesJson, '$.snapshotHash') <> selection.PaymentReadinessSnapshotHash)
                        )
                        OR (batch.Status = 5 AND EXISTS (SELECT 1 FROM PaymentBatchInvoice selection WHERE selection.PaymentBatchId = batch.Id AND selection.TenantId = batch.TenantId AND selection.IsDeleted = 0 AND selection.Status <> 'Processed'))
                        OR (batch.Status = 6 AND (NOT EXISTS (SELECT 1 FROM PaymentBatchInvoice selection WHERE selection.PaymentBatchId = batch.Id AND selection.TenantId = batch.TenantId AND selection.IsDeleted = 0 AND selection.Status = 'Processed')
                                                  OR NOT EXISTS (SELECT 1 FROM PaymentBatchInvoice selection WHERE selection.PaymentBatchId = batch.Id AND selection.TenantId = batch.TenantId AND selection.IsDeleted = 0 AND selection.Status = 'Failed')))
                        OR (batch.Status = 3 AND NOT EXISTS (
                            SELECT 1 FROM WorkflowInstances workflow
                            JOIN WorkflowEntityTypes entityType ON entityType.Id = workflow.EntityTypeId AND entityType.TenantId = batch.TenantId AND entityType.IsDeleted = 0
                            WHERE workflow.EntityId = batch.Id AND workflow.TenantId = batch.TenantId AND workflow.Status = 2 AND workflow.IsDeleted = 0
                              AND (entityType.Code = 'PaymentBatch' OR entityType.Name = 'Payment Batch')))
                      ))
                    THROW 51623, 'AP_PAYMENT_BATCH_READINESS_BLOCKED: batch workflow and exact invoice readiness must be current before approval or processing.', 1;
            END;
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_PaymentBatchInvoice_TDC0505ImmutableSelection]
            ON [dbo].[PaymentBatchInvoice]
            AFTER UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM deleted priorRow
                    LEFT JOIN inserted currentRow ON currentRow.Id = priorRow.Id
                    WHERE currentRow.Id IS NULL
                       OR currentRow.TenantId <> priorRow.TenantId
                       OR currentRow.PaymentBatchId <> priorRow.PaymentBatchId
                       OR currentRow.PaymentBatchItemId <> priorRow.PaymentBatchItemId
                       OR currentRow.VendorPaymentId <> priorRow.VendorPaymentId
                       OR currentRow.VendorInvoiceId <> priorRow.VendorInvoiceId
                       OR currentRow.Amount <> priorRow.Amount
                       OR currentRow.IsDeleted <> priorRow.IsDeleted)
                    THROW 51624, 'AP_PAYMENT_BATCH_SELECTION_IMMUTABLE: exact batch invoice selections cannot be changed or deleted.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_PaymentBatchInvoice_TDC0505ImmutableSelection];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_PaymentBatch_TDC0505Readiness];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_PaymentBatchInvoice_TDC0505PaymentReadiness];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_VendorPaymentAllocation_TDC0505PaymentReadiness];");
        migrationBuilder.DropTable(name: "PaymentBatchInvoice");
        migrationBuilder.DropForeignKey(
            name: "FK_VendorPaymentAllocation_ProcurementControlEvents_PaymentReadinessControlEventId",
            table: "VendorPaymentAllocation");
        migrationBuilder.DropIndex(
            name: "IX_VendorPaymentAllocation_TenantId_PaymentReadinessControlEventId",
            table: "VendorPaymentAllocation");
        migrationBuilder.DropCheckConstraint(
            name: "CK_VendorPaymentAllocation_TDC0505Snapshot",
            table: "VendorPaymentAllocation");
        migrationBuilder.DropColumn(name: "PaymentReadinessControlEventId", table: "VendorPaymentAllocation");
        migrationBuilder.DropColumn(name: "PaymentReadinessSnapshotHash", table: "VendorPaymentAllocation");
        migrationBuilder.DropColumn(name: "PaymentReadinessEvaluatedAtUtc", table: "VendorPaymentAllocation");
    }

    private static string PaymentReadinessValidationSql(
        string triggerName,
        string tableName,
        string alias,
        string invoiceIdExpression,
        string eventIdExpression,
        string snapshotHashExpression,
        string evaluatedAtExpression,
        string governedPredicate,
        string actionPredicate,
        string invoiceStatePredicate,
        int errorNumber,
        string errorCode,
        string additionalValidation = "") =>
        $$"""
        CREATE OR ALTER TRIGGER [dbo].[{{triggerName}}]
        ON [dbo].[{{tableName}}]
        AFTER INSERT, UPDATE
        AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (
                SELECT 1
                FROM inserted {{alias}}
                LEFT JOIN VendorInvoice invoice ON invoice.Id = {{invoiceIdExpression}} AND invoice.TenantId = {{alias}}.TenantId AND invoice.IsDeleted = 0
                LEFT JOIN ProcurementControlEvents paymentEvent
                  ON paymentEvent.Id = {{eventIdExpression}}
                 AND paymentEvent.TenantId = {{alias}}.TenantId
                 AND paymentEvent.SourceId = {{invoiceIdExpression}}
                 AND paymentEvent.SourceType = 'VendorInvoice'
                 AND paymentEvent.EventType = 'VendorInvoicePaymentReadiness'
                 AND paymentEvent.RuleCode = 'AP-003'
                 AND paymentEvent.RuleVersion = 'TDC-0505'
                 AND paymentEvent.Result = 2
                 AND paymentEvent.IsDeleted = 0
                LEFT JOIN ProcurementControlEvents matchEvent
                  ON matchEvent.Id = invoice.MatchingControlEventId
                 AND matchEvent.TenantId = invoice.TenantId
                 AND matchEvent.SourceId = invoice.Id
                 AND matchEvent.SourceType = 'VendorInvoice'
                 AND matchEvent.EventType = 'InvoiceThreeWayMatching'
                 AND matchEvent.Action = 'InvoiceThreeWayMatchEvaluated'
                 AND matchEvent.RuleCode = 'AP-002'
                 AND matchEvent.RuleVersion = 'TDC-0504'
                 AND matchEvent.Result = 2
                 AND matchEvent.IsDeleted = 0
                WHERE {{governedPredicate}}
                  AND (invoice.Id IS NULL
                    OR NOT ({{invoiceStatePredicate}})
                    OR paymentEvent.Id IS NULL
                    OR NOT ({{actionPredicate}})
                    OR {{snapshotHashExpression}} IS NULL OR LEN({{snapshotHashExpression}}) <> 64
                    OR {{evaluatedAtExpression}} IS NULL OR paymentEvent.OccurredAtUtc <> {{evaluatedAtExpression}}
                    OR ISJSON(paymentEvent.ResultValuesJson) <> 1
                    OR JSON_VALUE(paymentEvent.ResultValuesJson, '$.isPaymentReady') <> 'true'
                    OR JSON_VALUE(paymentEvent.ResultValuesJson, '$.snapshotHash') <> {{snapshotHashExpression}}
                    OR EXISTS (
                        SELECT required.DecisionKey
                        FROM (VALUES ('DEC-001'),('DEC-002'),('DEC-003'),('DEC-004'),('DEC-005'),('DEC-006'),('DEC-007'),('DEC-008'),('DEC-009'),('DEC-010'),('DEC-011'),('DEC-012'),('DEC-013'),('DEC-014')) required(DecisionKey)
                        WHERE NOT EXISTS (
                            SELECT 1 FROM OPENJSON(CASE WHEN ISJSON(paymentEvent.DecisionKeysJson) = 1 THEN paymentEvent.DecisionKeysJson ELSE '[]' END) decisionKey
                            WHERE decisionKey.[value] = required.DecisionKey))
                    OR (invoice.PurchaseOrderId IS NOT NULL AND invoice.IsOpeningBalance = 0 AND (
                           invoice.MatchingType <> 2
                        OR invoice.MatchingControlEventId IS NULL
                        OR invoice.MatchingSnapshotHash IS NULL
                        OR invoice.MatchingEvaluatedAtUtc IS NULL
                        OR matchEvent.Id IS NULL
                        OR matchEvent.OccurredAtUtc <> invoice.MatchingEvaluatedAtUtc
                        OR ISJSON(matchEvent.ResultValuesJson) <> 1
                        OR JSON_VALUE(matchEvent.ResultValuesJson, '$.approvalReady') <> 'true'
                        OR JSON_VALUE(matchEvent.ResultValuesJson, '$.snapshotHash') <> invoice.MatchingSnapshotHash
                        OR NOT EXISTS (SELECT 1 FROM PurchaseOrderReceipts receipt WHERE receipt.TenantId = invoice.TenantId AND receipt.PurchaseOrderId = invoice.PurchaseOrderId AND receipt.IsDeleted = 0)
                        OR EXISTS (
                            SELECT 1 FROM PurchaseOrderReceipts receipt
                            OUTER APPLY (
                                SELECT TOP (1) inspection.Status, inspection.PendingQuantity, inspection.ApEligibleQuantity
                                FROM ProcurementReceiptInspectionCases inspection
                                WHERE inspection.TenantId = invoice.TenantId AND inspection.PurchaseOrderReceiptId = receipt.Id AND inspection.IsDeleted = 0
                                ORDER BY inspection.Sequence DESC) latestInspection
                            WHERE receipt.TenantId = invoice.TenantId AND receipt.PurchaseOrderId = invoice.PurchaseOrderId AND receipt.IsDeleted = 0
                              AND (latestInspection.Status IS NULL OR latestInspection.Status NOT IN (2,4,5,6,7,8) OR latestInspection.PendingQuantity <> 0 OR latestInspection.ApEligibleQuantity <= 0))
                        OR (invoice.MatchingStatus <> 2 AND NOT EXISTS (
                            SELECT 1
                            FROM ProcurementControlEvents exceptionEvent
                            JOIN WorkflowInstances workflow
                              ON workflow.Id = TRY_CONVERT(uniqueidentifier, JSON_VALUE(exceptionEvent.ResultValuesJson, '$.workflowInstanceId'))
                             AND workflow.TenantId = invoice.TenantId AND workflow.Status = 2 AND workflow.IsDeleted = 0
                            WHERE exceptionEvent.Id = invoice.MatchExceptionControlEventId
                              AND exceptionEvent.TenantId = invoice.TenantId
                              AND exceptionEvent.SourceId = invoice.Id
                              AND exceptionEvent.SourceType = 'VendorInvoice'
                              AND exceptionEvent.Action = 'InvoiceMatchExceptionApproved'
                              AND exceptionEvent.RuleCode = 'AP-006'
                              AND exceptionEvent.RuleVersion = 'TDC-0507'
                              AND exceptionEvent.Result IN (0,2)
                              AND exceptionEvent.IsDeleted = 0
                              AND ISJSON(exceptionEvent.ResultValuesJson) = 1
                              AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(exceptionEvent.ResultValuesJson, '$.purchaseOrderId')) = invoice.PurchaseOrderId
                              AND JSON_VALUE(exceptionEvent.ResultValuesJson, '$.invoiceSnapshotHash') = invoice.MatchingSnapshotHash
                              AND TRY_CONVERT(datetime2, JSON_VALUE(exceptionEvent.ResultValuesJson, '$.expiresAtUtc')) > SYSUTCDATETIME()
                              AND workflow.InitiatedById <> exceptionEvent.ActorUserId
                              AND (invoice.SubmittedById IS NULL OR invoice.SubmittedById <> exceptionEvent.ActorUserId)
                              AND EXISTS (SELECT 1 FROM ProcurementControlEventEvidenceLinks evidence WHERE evidence.ControlEventId = exceptionEvent.Id AND evidence.TenantId = invoice.TenantId AND evidence.IsDeleted = 0)))
                    ))
                    {{additionalValidation}}
                  ))
                THROW {{errorNumber}}, '{{errorCode}}: current invoice state, match, receipt inspection, exception and AP-003 decision are required.', 1;
        END;
        """;
}
