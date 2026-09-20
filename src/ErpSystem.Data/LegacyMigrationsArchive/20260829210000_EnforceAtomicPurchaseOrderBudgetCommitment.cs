using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260829210000_EnforceAtomicPurchaseOrderBudgetCommitment")]
public partial class EnforceAtomicPurchaseOrderBudgetCommitment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS [IX_PurchaseOrders_Status] ON [dbo].[PurchaseOrders];
            ALTER TABLE [dbo].[PurchaseOrders]
              ALTER COLUMN [Status] nvarchar(50) NOT NULL;
            CREATE INDEX [IX_PurchaseOrders_Status]
              ON [dbo].[PurchaseOrders] ([Status]);
            """);
        migrationBuilder.Sql(FinalExposureTriggerSql);
        migrationBuilder.Sql(FinalCommitmentAmountConstraintSql);
        migrationBuilder.Sql(FinalCommitmentLifecycleTriggerSql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(PreviousTriggerSql);
        migrationBuilder.Sql(PreviousCommitmentLifecycleTriggerSql);
        migrationBuilder.Sql(PreviousCommitmentAmountConstraintSql);
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS [IX_PurchaseOrders_Status] ON [dbo].[PurchaseOrders];
            ALTER TABLE [dbo].[PurchaseOrders]
              ALTER COLUMN [Status] nvarchar(20) NOT NULL;
            CREATE INDEX [IX_PurchaseOrders_Status]
              ON [dbo].[PurchaseOrders] ([Status]);
            """);
    }

    private const string FinalCommitmentAmountConstraintSql = """
        ALTER TABLE [dbo].[ProcurementBudgetCommitments]
          DROP CONSTRAINT [CK_ProcurementBudgetCommitments_Amount];
        ALTER TABLE [dbo].[ProcurementBudgetCommitments] WITH CHECK
          ADD CONSTRAINT [CK_ProcurementBudgetCommitments_Amount]
          CHECK (([Status] = 2 AND [ReservedAmount] >= 0)
              OR ([Status] IN (1, 3) AND [ReservedAmount] > 0));
        ALTER TABLE [dbo].[ProcurementBudgetCommitments]
          CHECK CONSTRAINT [CK_ProcurementBudgetCommitments_Amount];
        """;

    private const string FinalCommitmentLifecycleTriggerSql = """
        CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementBudgetCommitments_LifecycleGuard]
        ON [dbo].[ProcurementBudgetCommitments] AFTER UPDATE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS (
            SELECT 1
            FROM inserted i
            JOIN deleted d ON d.Id = i.Id
            OUTER APPLY
            (
              SELECT TOP (1) CAST(1 AS bit) AS IsAuthorized
              FROM dbo.ProcurementPurchaseOrderAmendments amendment
              JOIN dbo.PurchaseOrders purchaseOrder
                ON purchaseOrder.TenantId = amendment.TenantId
               AND purchaseOrder.Id = amendment.PurchaseOrderId
               AND purchaseOrder.SourceRequisitionId = i.PurchaseRequisitionId
               AND purchaseOrder.TotalAmount = amendment.BeforeTotalAmount
               AND purchaseOrder.Currency = i.Currency
               AND purchaseOrder.Status = N'Amendment Pending Approval'
               AND amendment.PurchaseOrderStatusBefore IN
                   (N'Approved', N'Open', N'Sent', N'Acknowledged',
                    N'Partially Received', N'PartiallyReceived', N'Received')
               AND purchaseOrder.IsDeleted = 0
              JOIN dbo.ProcurementBudgetCommitmentLedgerEntries formal
                ON formal.TenantId = amendment.TenantId
               AND formal.ProcurementBudgetCommitmentId = i.Id
               AND formal.ProcurementBudgetId = i.ProcurementBudgetId
               AND formal.PurchaseRequisitionId = i.PurchaseRequisitionId
               AND formal.EntryType = 1
               AND formal.SourceType = N'PurchaseOrder'
               AND formal.SourceId = amendment.PurchaseOrderId
               AND formal.IsDeleted = 0
              WHERE amendment.Id = TRY_CONVERT(uniqueidentifier,
                    SESSION_CONTEXT(N'TDC0406_PO_AMENDMENT_ID'))
                AND i.UtilizedAmount = d.UtilizedAmount
                AND amendment.TenantId = i.TenantId
                AND amendment.ProposedSourceRequisitionId = i.PurchaseRequisitionId
                AND amendment.Status = 2
                AND amendment.IsDeleted = 0
                AND amendment.WorkflowDefinitionId IS NOT NULL
                AND amendment.WorkflowInstanceId IS NOT NULL
                AND amendment.DecidedAtUtc IS NOT NULL
                AND amendment.DecidedById IS NOT NULL
                AND amendment.AppliedAtUtc IS NOT NULL
                AND amendment.AppliedById IS NOT NULL
                AND NULLIF(LTRIM(RTRIM(
                      amendment.ApprovalEvidenceReference)), N'') IS NOT NULL
                AND amendment.Currency = i.Currency
                AND amendment.CommitmentDelta =
                    i.FormallyCommittedAmount - d.FormallyCommittedAmount
                AND amendment.CommitmentDelta =
                    i.ReservedAmount - d.ReservedAmount
                AND amendment.CommitmentDelta =
                    amendment.ProposedTotalAmount - amendment.BeforeTotalAmount
                AND d.ReservedAmount - d.FormallyCommittedAmount =
                    i.ReservedAmount - i.FormallyCommittedAmount
                AND amendment.BeforeTotalAmount = formal.Amount + COALESCE(
                    (
                      SELECT SUM(prior.DeltaAmount)
                      FROM dbo.ProcurementPurchaseOrderCommitmentAdjustments prior
                      WHERE prior.TenantId = amendment.TenantId
                        AND prior.PurchaseOrderId = amendment.PurchaseOrderId
                        AND prior.PurchaseRequisitionId = i.PurchaseRequisitionId
                        AND prior.ProcurementBudgetId = i.ProcurementBudgetId
                        AND prior.BudgetCommitmentId = i.Id
                        AND prior.IsDeleted = 0
                    ), 0)
                AND d.FormallyCommittedAmount = COALESCE(
                    (
                      SELECT SUM(exposure.Amount)
                      FROM dbo.ProcurementBudgetCommitmentLedgerEntries exposure
                      WHERE exposure.TenantId = i.TenantId
                        AND exposure.ProcurementBudgetCommitmentId = i.Id
                        AND exposure.EntryType = 1
                        AND exposure.IsDeleted = 0
                    ), 0) - COALESCE(
                    (
                      SELECT SUM(released.Amount)
                      FROM dbo.ProcurementBudgetCommitmentLedgerEntries released
                      WHERE released.TenantId = i.TenantId
                        AND released.ProcurementBudgetCommitmentId = i.Id
                        AND released.EntryType = 3
                        AND released.FormalCommitmentEntryId IS NOT NULL
                        AND released.IsDeleted = 0
                    ), 0) + COALESCE(
                    (
                      SELECT SUM(priorDirect.DeltaAmount)
                      FROM dbo.ProcurementPurchaseOrderCommitmentAdjustments priorDirect
                      WHERE priorDirect.TenantId = i.TenantId
                        AND priorDirect.PurchaseRequisitionId = i.PurchaseRequisitionId
                        AND priorDirect.ProcurementBudgetId = i.ProcurementBudgetId
                        AND priorDirect.BudgetCommitmentId = i.Id
                        AND priorDirect.IsDeleted = 0
                        AND EXISTS
                        (
                          SELECT 1
                          FROM dbo.ProcurementBudgetCommitmentLedgerEntries directFormal
                          WHERE directFormal.TenantId = priorDirect.TenantId
                            AND directFormal.ProcurementBudgetCommitmentId = i.Id
                            AND directFormal.EntryType = 1
                            AND directFormal.SourceType = N'PurchaseOrder'
                            AND directFormal.SourceId = priorDirect.PurchaseOrderId
                            AND directFormal.IsDeleted = 0
                        )
                    ), 0)
            ) poAmendment
            OUTER APPLY
            (
              SELECT CAST(1 AS bit) AS IsAuthorized
              WHERE d.Status = 1
                AND i.Status = 1
                AND i.FormallyCommittedAmount = d.FormallyCommittedAmount
                AND i.UtilizedAmount = d.UtilizedAmount
                AND i.ReservedAmount >= i.FormallyCommittedAmount
                AND TRY_CONVERT(uniqueidentifier,
                      SESSION_CONTEXT(N'PROCUREMENT_DOWNSTREAM_RESERVATION_TENANT_ID')) = i.TenantId
                AND TRY_CONVERT(uniqueidentifier,
                      SESSION_CONTEXT(N'PROCUREMENT_DOWNSTREAM_RESERVATION_REQUISITION_ID')) = i.PurchaseRequisitionId
                AND TRY_CONVERT(uniqueidentifier,
                      SESSION_CONTEXT(N'PROCUREMENT_DOWNSTREAM_RESERVATION_COMMITMENT_ID')) = i.Id
                AND TRY_CONVERT(decimal(18, 2),
                      SESSION_CONTEXT(N'PROCUREMENT_DOWNSTREAM_RESERVATION_AMOUNT_BEFORE')) = d.ReservedAmount
                AND TRY_CONVERT(decimal(18, 2),
                      SESSION_CONTEXT(N'PROCUREMENT_DOWNSTREAM_RESERVATION_AMOUNT_AFTER')) = i.ReservedAmount
                AND TRY_CONVERT(int,
                      SESSION_CONTEXT(N'PROCUREMENT_DOWNSTREAM_RESERVATION_SEQUENCE_BEFORE')) = d.ReservationSequence
                AND TRY_CONVERT(int,
                      SESSION_CONTEXT(N'PROCUREMENT_DOWNSTREAM_RESERVATION_SEQUENCE_AFTER')) = i.ReservationSequence
                AND TRY_CONVERT(nvarchar(100),
                      SESSION_CONTEXT(N'PROCUREMENT_DOWNSTREAM_RESERVATION_CORRELATION_ID')) = i.CorrelationId
            ) downstreamReservation
            OUTER APPLY
            (
              SELECT CAST(1 AS bit) AS IsAuthorized
              WHERE d.Status = 1
                AND i.Status = 2
                AND d.FormallyCommittedAmount = 0
                AND i.FormallyCommittedAmount = 0
                AND d.UtilizedAmount = 0
                AND i.UtilizedAmount = 0
                AND d.ReservedAmount > 0
                AND i.ReservedAmount = 0
                AND i.ReservationSequence = d.ReservationSequence
                AND TRY_CONVERT(uniqueidentifier,
                      SESSION_CONTEXT(N'PROCUREMENT_REQUISITION_RELEASE_TENANT_ID')) = i.TenantId
                AND TRY_CONVERT(uniqueidentifier,
                      SESSION_CONTEXT(N'PROCUREMENT_REQUISITION_RELEASE_REQUISITION_ID')) = i.PurchaseRequisitionId
                AND TRY_CONVERT(uniqueidentifier,
                      SESSION_CONTEXT(N'PROCUREMENT_REQUISITION_RELEASE_COMMITMENT_ID')) = i.Id
                AND TRY_CONVERT(decimal(18, 2),
                      SESSION_CONTEXT(N'PROCUREMENT_REQUISITION_RELEASE_AMOUNT_BEFORE')) = d.ReservedAmount
                AND TRY_CONVERT(int,
                      SESSION_CONTEXT(N'PROCUREMENT_REQUISITION_RELEASE_SEQUENCE')) = d.ReservationSequence
                AND TRY_CONVERT(nvarchar(100),
                      SESSION_CONTEXT(N'PROCUREMENT_REQUISITION_RELEASE_CORRELATION_ID')) = i.CorrelationId
                AND NOT EXISTS
                (
                  SELECT 1
                  FROM dbo.ProcurementBudgetCommitmentLedgerEntries exposure
                  WHERE exposure.TenantId = i.TenantId
                    AND exposure.ProcurementBudgetCommitmentId = i.Id
                    AND exposure.EntryType IN (1, 4)
                    AND exposure.IsDeleted = 0
                )
            ) requisitionRelease
            OUTER APPLY
            (
              SELECT CAST(1 AS bit) AS IsAuthorized
              WHERE d.Status = 1
                AND i.Status = 3
                AND i.ReservedAmount = d.ReservedAmount
                AND i.FormallyCommittedAmount = d.FormallyCommittedAmount
                AND i.UtilizedAmount = d.UtilizedAmount
                AND i.ReservedAmount > 0
                AND i.UtilizedAmount >= i.ReservedAmount
                AND i.ReservationSequence = d.ReservationSequence
                AND EXISTS
                (
                  SELECT 1
                  FROM dbo.ProcurementBudgetCommitmentLedgerEntries formal
                  WHERE formal.TenantId = i.TenantId
                    AND formal.ProcurementBudgetCommitmentId = i.Id
                    AND formal.EntryType = 1
                    AND formal.SourceType = N'Contract'
                    AND formal.SourceId = TRY_CONVERT(uniqueidentifier,
                          SESSION_CONTEXT(N'PROCUREMENT_CONTRACT_RELEASE_ID'))
                    AND formal.IsDeleted = 0
                    AND formal.Amount <= COALESCE(
                    (
                      SELECT SUM(utilization.Amount)
                      FROM dbo.ProcurementBudgetCommitmentLedgerEntries utilization
                      WHERE utilization.TenantId = formal.TenantId
                        AND utilization.FormalCommitmentEntryId = formal.Id
                        AND utilization.EntryType = 2
                        AND utilization.IsDeleted = 0
                    ), 0)
                )
            ) fullyUtilizedContractClose
            OUTER APPLY
            (
              SELECT CAST(1 AS bit) AS IsAuthorized
              WHERE d.Status = 1
                AND i.Status = 1
                AND i.ReservedAmount = d.ReservedAmount
                AND i.UtilizedAmount = d.UtilizedAmount
                AND i.ReservationSequence = d.ReservationSequence
                AND i.FormallyCommittedAmount > d.FormallyCommittedAmount
                AND TRY_CONVERT(uniqueidentifier,
                      SESSION_CONTEXT(N'PROCUREMENT_FORMAL_TENANT_ID')) = i.TenantId
                AND TRY_CONVERT(uniqueidentifier,
                      SESSION_CONTEXT(N'PROCUREMENT_FORMAL_COMMITMENT_ID')) = i.Id
                AND TRY_CONVERT(decimal(18, 2),
                      SESSION_CONTEXT(N'PROCUREMENT_FORMAL_AMOUNT_BEFORE')) = d.FormallyCommittedAmount
                AND TRY_CONVERT(decimal(18, 2),
                      SESSION_CONTEXT(N'PROCUREMENT_FORMAL_AMOUNT_AFTER')) = i.FormallyCommittedAmount
                AND TRY_CONVERT(nvarchar(100),
                      SESSION_CONTEXT(N'PROCUREMENT_FORMAL_CORRELATION_ID')) = i.CorrelationId
                AND EXISTS
                (
                  SELECT 1
                  FROM dbo.ProcurementBudgetCommitmentLedgerEntries formalProof
                  WHERE formalProof.Id = TRY_CONVERT(uniqueidentifier,
                            SESSION_CONTEXT(N'PROCUREMENT_FORMAL_LEDGER_ENTRY_ID'))
                    AND formalProof.TenantId = i.TenantId
                    AND formalProof.ProcurementBudgetCommitmentId = i.Id
                    AND formalProof.EntryType = 1
                    AND formalProof.SourceType = TRY_CONVERT(nvarchar(50),
                          SESSION_CONTEXT(N'PROCUREMENT_FORMAL_SOURCE_TYPE'))
                    AND formalProof.SourceId = TRY_CONVERT(uniqueidentifier,
                          SESSION_CONTEXT(N'PROCUREMENT_FORMAL_SOURCE_ID'))
                    AND formalProof.Amount = i.FormallyCommittedAmount - d.FormallyCommittedAmount
                    AND formalProof.CorrelationId = i.CorrelationId
                    AND formalProof.IsDeleted = 0
                )
            ) formalPromotion
            OUTER APPLY
            (
              SELECT CAST(1 AS bit) AS IsAuthorized
              WHERE d.Status = 1
                AND i.Status = 1
                AND i.ReservedAmount = d.ReservedAmount
                AND i.FormallyCommittedAmount = d.FormallyCommittedAmount
                AND i.ReservationSequence = d.ReservationSequence
                AND i.UtilizedAmount > d.UtilizedAmount
                AND TRY_CONVERT(uniqueidentifier,
                      SESSION_CONTEXT(N'PROCUREMENT_UTILIZATION_TENANT_ID')) = i.TenantId
                AND TRY_CONVERT(uniqueidentifier,
                      SESSION_CONTEXT(N'PROCUREMENT_UTILIZATION_COMMITMENT_ID')) = i.Id
                AND TRY_CONVERT(decimal(18, 2),
                      SESSION_CONTEXT(N'PROCUREMENT_UTILIZATION_AMOUNT_BEFORE')) = d.UtilizedAmount
                AND TRY_CONVERT(decimal(18, 2),
                      SESSION_CONTEXT(N'PROCUREMENT_UTILIZATION_AMOUNT_AFTER')) = i.UtilizedAmount
                AND TRY_CONVERT(nvarchar(100),
                      SESSION_CONTEXT(N'PROCUREMENT_UTILIZATION_CORRELATION_ID')) = i.CorrelationId
                AND EXISTS
                (
                  SELECT 1
                  FROM dbo.ProcurementBudgetCommitmentLedgerEntries utilizationProof
                  WHERE utilizationProof.Id = TRY_CONVERT(uniqueidentifier,
                            SESSION_CONTEXT(N'PROCUREMENT_UTILIZATION_LEDGER_ENTRY_ID'))
                    AND utilizationProof.TenantId = i.TenantId
                    AND utilizationProof.ProcurementBudgetCommitmentId = i.Id
                    AND utilizationProof.EntryType = 2
                    AND utilizationProof.SourceType = TRY_CONVERT(nvarchar(50),
                          SESSION_CONTEXT(N'PROCUREMENT_UTILIZATION_SOURCE_TYPE'))
                    AND utilizationProof.SourceId = TRY_CONVERT(uniqueidentifier,
                          SESSION_CONTEXT(N'PROCUREMENT_UTILIZATION_SOURCE_ID'))
                    AND utilizationProof.Amount = i.UtilizedAmount - d.UtilizedAmount
                    AND utilizationProof.CorrelationId = i.CorrelationId
                    AND utilizationProof.IsDeleted = 0
                )
            ) utilizationMutation
            OUTER APPLY
            (
              SELECT CAST(1 AS bit) AS IsAuthorized
              WHERE d.Status = 1
                AND i.UtilizedAmount = d.UtilizedAmount
                AND i.ReservedAmount < d.ReservedAmount
                AND d.ReservedAmount - i.ReservedAmount =
                    d.FormallyCommittedAmount - i.FormallyCommittedAmount
                AND d.FormallyCommittedAmount > i.FormallyCommittedAmount
                AND
                (
                  (i.ReservedAmount = 0 AND i.FormallyCommittedAmount = 0
                   AND i.Status = 2)
                  OR (i.ReservedAmount > 0
                      AND i.ReservedAmount <= i.UtilizedAmount
                      AND i.Status = 3)
                  OR (i.ReservedAmount > i.UtilizedAmount
                      AND i.Status = 1)
                )
                AND TRY_CONVERT(uniqueidentifier,
                      SESSION_CONTEXT(N'PROCUREMENT_CONTRACT_RELEASE_ID')) IS NOT NULL
                AND EXISTS
                (
                  SELECT 1
                  FROM dbo.ProcurementBudgetCommitmentLedgerEntries formal
                  JOIN dbo.ProcurementBudgetCommitmentLedgerEntries release
                    ON release.TenantId = formal.TenantId
                   AND release.ProcurementBudgetCommitmentId = i.Id
                   AND release.FormalCommitmentEntryId = formal.Id
                   AND release.EntryType = 3
                   AND release.SourceType = N'Contract'
                   AND release.SourceId = formal.SourceId
                   AND release.Amount = d.ReservedAmount - i.ReservedAmount
                   AND release.IsDeleted = 0
                  WHERE formal.TenantId = i.TenantId
                    AND formal.ProcurementBudgetCommitmentId = i.Id
                    AND formal.EntryType = 1
                    AND formal.SourceType = N'Contract'
                    AND formal.SourceId = TRY_CONVERT(uniqueidentifier,
                          SESSION_CONTEXT(N'PROCUREMENT_CONTRACT_RELEASE_ID'))
                    AND formal.IsDeleted = 0
                )
            ) contractRelease
            WHERE i.TenantId <> d.TenantId
              OR i.PurchaseRequisitionId <> d.PurchaseRequisitionId
              OR i.ProcurementBudgetId <> d.ProcurementBudgetId
              OR i.ReservationReference <> d.ReservationReference
              OR (i.ReservedAmount <> d.ReservedAmount AND NOT
                  (
                    (d.Status = 1 AND i.Status = 1
                     AND i.ReservationSequence = d.ReservationSequence + 1
                     AND ISNULL(poAmendment.IsAuthorized, 0) = 1)
                    OR
                    (d.Status = 1 AND i.Status = 1
                     AND i.ReservationSequence = d.ReservationSequence + 1
                     AND ISNULL(downstreamReservation.IsAuthorized, 0) = 1)
                    OR
                    (ISNULL(requisitionRelease.IsAuthorized, 0) = 1)
                    OR
                    (ISNULL(contractRelease.IsAuthorized, 0) = 1)
                  ))
              OR (i.FormallyCommittedAmount <> d.FormallyCommittedAmount AND NOT
                  (ISNULL(poAmendment.IsAuthorized, 0) = 1
                   OR ISNULL(formalPromotion.IsAuthorized, 0) = 1
                   OR ISNULL(contractRelease.IsAuthorized, 0) = 1))
              OR (i.UtilizedAmount <> d.UtilizedAmount AND
                  ISNULL(utilizationMutation.IsAuthorized, 0) <> 1)
              OR i.Currency <> d.Currency
              OR i.CreatedAt <> d.CreatedAt
              OR i.IsDeleted <> d.IsDeleted
              OR NOT ((d.Status = 1 AND i.Status = 1
                       AND i.ReservationSequence = d.ReservationSequence)
                    OR (d.Status = 1 AND i.Status = 2
                        AND i.ReservedAmount <> d.ReservedAmount
                        AND i.ReservationSequence = d.ReservationSequence)
                    OR (d.Status = 1 AND i.Status = 3
                        AND i.ReservationSequence = d.ReservationSequence
                        AND (i.ReservedAmount <> d.ReservedAmount
                             OR ISNULL(fullyUtilizedContractClose.IsAuthorized, 0) = 1))
                    OR (d.Status = 1 AND i.Status = 1
                        AND i.ReservationSequence = d.ReservationSequence + 1
                        AND (ISNULL(poAmendment.IsAuthorized, 0) = 1
                             OR ISNULL(downstreamReservation.IsAuthorized, 0) = 1))))
            THROW 51022, 'Budget reservation identity, retention, or lifecycle transition is immutable.', 1;
        END;
        """;

    private const string PreviousCommitmentLifecycleTriggerSql = """
        CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementBudgetCommitments_LifecycleGuard]
        ON [dbo].[ProcurementBudgetCommitments] AFTER UPDATE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS (
            SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
            WHERE i.TenantId <> d.TenantId OR i.PurchaseRequisitionId <> d.PurchaseRequisitionId
              OR i.ProcurementBudgetId <> d.ProcurementBudgetId
              OR i.ReservationReference <> d.ReservationReference
              OR (i.ReservedAmount <> d.ReservedAmount AND NOT
                  (d.Status = 2 AND i.Status = 1 AND i.ReservationSequence = d.ReservationSequence + 1))
              OR i.Currency <> d.Currency OR i.CreatedAt <> d.CreatedAt OR i.IsDeleted <> d.IsDeleted
              OR NOT ((d.Status = 1 AND i.Status = 1 AND i.ReservationSequence = d.ReservationSequence)
                   OR (d.Status = 1 AND i.Status IN (2, 3) AND i.ReservationSequence = d.ReservationSequence)
                   OR (d.Status = 2 AND i.Status = 1 AND i.ReservationSequence = d.ReservationSequence + 1)))
            THROW 51022, 'Budget reservation identity, retention, or lifecycle transition is immutable.', 1;
        END;
        """;

    private const string PreviousCommitmentAmountConstraintSql = """
        IF EXISTS (
          SELECT 1 FROM dbo.ProcurementBudgetCommitments
          WHERE ReservedAmount = 0)
          THROW 52058, 'Rollback blocked: zero-value released commitment envelopes exist.', 1;
        ALTER TABLE [dbo].[ProcurementBudgetCommitments]
          DROP CONSTRAINT [CK_ProcurementBudgetCommitments_Amount];
        ALTER TABLE [dbo].[ProcurementBudgetCommitments] WITH CHECK
          ADD CONSTRAINT [CK_ProcurementBudgetCommitments_Amount]
          CHECK ([ReservedAmount] > 0);
        ALTER TABLE [dbo].[ProcurementBudgetCommitments]
          CHECK CONSTRAINT [CK_ProcurementBudgetCommitments_Amount];
        """;

    private const string FinalExposureTriggerSql = """
        CREATE OR ALTER TRIGGER [dbo].[TR_PurchaseOrders_GovernedCommitment]
        ON [dbo].[PurchaseOrders] AFTER INSERT, UPDATE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS (
            SELECT 1
            FROM inserted i
            JOIN deleted d ON d.Id = i.Id AND d.TenantId = i.TenantId
            WHERE ISNULL(d.ProcurementSourceType, -1) <> 5
              AND ISNULL(d.Status, N'') IN
                  (N'Approved', N'Open', N'Sent', N'Acknowledged',
                   N'Partially Received', N'PartiallyReceived', N'Received')
              AND ISNULL(i.Status, N'') NOT IN
                  (N'Approved', N'Open', N'Sent', N'Acknowledged',
                   N'Partially Received', N'PartiallyReceived', N'Received',
                   N'Amendment Pending Approval')
          )
            THROW 52041, 'A final purchase order requires an atomic formal-commitment reversal before leaving exposure status.', 1;

          IF EXISTS (
            SELECT 1
            FROM inserted i
            LEFT JOIN deleted d ON d.Id = i.Id
            LEFT JOIN dbo.ProcurementRequisitionSourcingReleases r ON r.Id = i.SourcingReleaseId
             AND r.TenantId = i.TenantId
             AND r.PurchaseRequisitionId = i.SourceRequisitionId
             AND r.IsDeleted = 0
            LEFT JOIN dbo.PurchaseRequisitions pr ON pr.Id = i.SourceRequisitionId
             AND pr.TenantId = i.TenantId AND pr.IsDeleted = 0
            LEFT JOIN dbo.ProcurementBudgetCommitments c ON c.PurchaseRequisitionId = i.SourceRequisitionId
             AND c.TenantId = i.TenantId AND c.IsDeleted = 0
             AND (r.BudgetCommitmentId IS NULL OR r.BudgetCommitmentId = c.Id)
            LEFT JOIN dbo.ProcurementBudgets b ON b.Id = c.ProcurementBudgetId
             AND b.TenantId = i.TenantId AND b.IsDeleted = 0
            WHERE ISNULL(i.ProcurementSourceType, -1) <> 5
              AND i.Status IN
                  (N'Approved', N'Open', N'Sent', N'Acknowledged',
                   N'Partially Received', N'PartiallyReceived', N'Received')
              AND (d.Id IS NULL OR
                   (ISNULL(d.Status, N'') NOT IN
                      (N'Approved', N'Open', N'Sent', N'Acknowledged',
                       N'Partially Received', N'PartiallyReceived', N'Received')
                    AND ISNULL(d.Status, N'') <> N'Amendment Pending Approval'))
              AND
              (
                r.Id IS NULL OR pr.Id IS NULL
                OR
                (
                  i.ContractId IS NULL
                  AND
                  (
                    c.Id IS NULL OR b.Id IS NULL OR pr.BudgetId IS NULL
                    OR (r.BudgetCommitmentReference IS NOT NULL
                        AND r.BudgetCommitmentReference <> c.ReservationReference)
                    OR pr.BudgetId <> c.ProcurementBudgetId
                    OR c.Status <> 1 OR c.ReservedAmount <= 0
                    OR UPPER(LTRIM(RTRIM(c.Currency))) <> UPPER(LTRIM(RTRIM(i.Currency)))
                    OR UPPER(LTRIM(RTRIM(c.Currency))) <> UPPER(LTRIM(RTRIM(b.Currency)))
                    OR b.Status NOT IN (N'Approved', N'Active')
                    OR b.ApprovedById IS NULL OR b.ApprovedDate IS NULL
                    OR (b.EffectiveDate IS NOT NULL AND b.EffectiveDate > SYSUTCDATETIME())
                    OR (b.ExpiryDate IS NOT NULL AND b.ExpiryDate < SYSUTCDATETIME())
                    OR b.ReservedAmount + c.FormallyCommittedAmount < c.ReservedAmount
                    OR c.FormallyCommittedAmount > c.ReservedAmount
                    OR NOT EXISTS
                    (
                      SELECT 1
                      FROM dbo.ProcurementBudgetCommitmentLedgerEntries l
                      WHERE l.TenantId = i.TenantId
                        AND l.ProcurementBudgetCommitmentId = c.Id
                        AND l.PurchaseRequisitionId = i.SourceRequisitionId
                        AND l.EntryType = 1
                        AND l.SourceType = N'PurchaseOrder'
                        AND l.SourceId = i.Id
                        AND l.Amount +
                            (SELECT COALESCE(SUM(adjustment.DeltaAmount), 0)
                             FROM dbo.ProcurementPurchaseOrderCommitmentAdjustments adjustment
                             WHERE adjustment.TenantId = i.TenantId
                               AND adjustment.PurchaseOrderId = i.Id
                               AND adjustment.PurchaseRequisitionId = i.SourceRequisitionId
                               AND adjustment.BudgetCommitmentId = l.ProcurementBudgetCommitmentId
                               AND UPPER(LTRIM(RTRIM(adjustment.Currency))) =
                                   UPPER(LTRIM(RTRIM(i.Currency)))
                               AND adjustment.IsDeleted = 0) = i.TotalAmount
                        AND UPPER(LTRIM(RTRIM(l.Currency))) = UPPER(LTRIM(RTRIM(i.Currency)))
                        AND l.IsDeleted = 0
                    )
                  )
                )
                OR
                (
                  i.ContractId IS NOT NULL
                  AND NOT EXISTS
                  (
                    SELECT 1
                    FROM dbo.ProcurementBudgetCommitmentLedgerEntries allocation
                    JOIN dbo.ProcurementBudgetCommitmentLedgerEntries parent
                      ON parent.Id = allocation.FormalCommitmentEntryId
                     AND parent.TenantId = allocation.TenantId
                     AND parent.ProcurementBudgetCommitmentId = allocation.ProcurementBudgetCommitmentId
                     AND parent.EntryType = 1
                     AND parent.SourceType = N'Contract'
                     AND parent.SourceId = i.ContractId
                     AND parent.IsDeleted = 0
                    JOIN dbo.Contracts contract
                      ON contract.Id = i.ContractId
                     AND contract.TenantId = i.TenantId
                     AND contract.Status = N'Active'
                     AND contract.IsDeleted = 0
                    JOIN dbo.ProcurementBudgetCommitments parentCommitment
                      ON parentCommitment.Id = parent.ProcurementBudgetCommitmentId
                     AND parentCommitment.TenantId = i.TenantId
                     AND parentCommitment.PurchaseRequisitionId = i.SourceRequisitionId
                     AND parentCommitment.IsDeleted = 0
                    WHERE allocation.TenantId = i.TenantId
                      AND allocation.PurchaseRequisitionId = i.SourceRequisitionId
                      AND allocation.EntryType = 4
                      AND allocation.SourceType = N'PurchaseOrder'
                      AND allocation.SourceId = i.Id
                      AND allocation.Amount +
                          (SELECT COALESCE(SUM(adjustment.DeltaAmount), 0)
                           FROM dbo.ProcurementPurchaseOrderCommitmentAdjustments adjustment
                           WHERE adjustment.TenantId = i.TenantId
                             AND adjustment.PurchaseOrderId = i.Id
                             AND adjustment.PurchaseRequisitionId = i.SourceRequisitionId
                             AND adjustment.BudgetCommitmentId = allocation.ProcurementBudgetCommitmentId
                             AND UPPER(LTRIM(RTRIM(adjustment.Currency))) =
                                 UPPER(LTRIM(RTRIM(i.Currency)))
                             AND adjustment.IsDeleted = 0) = i.TotalAmount
                      AND allocation.IsDeleted = 0
                      AND parent.PurchaseRequisitionId = i.SourceRequisitionId
                      AND parent.ProcurementBudgetId = pr.BudgetId
                      AND UPPER(LTRIM(RTRIM(parent.Currency))) = UPPER(LTRIM(RTRIM(i.Currency)))
                      AND UPPER(LTRIM(RTRIM(allocation.Currency))) = UPPER(LTRIM(RTRIM(i.Currency)))
                      AND (r.BudgetCommitmentId IS NULL
                           OR r.BudgetCommitmentId = parent.ProcurementBudgetCommitmentId)
                      AND (r.BudgetCommitmentReference IS NULL
                           OR r.BudgetCommitmentReference = parentCommitment.ReservationReference)
                      AND (SELECT COALESCE(SUM(existingAllocation.Amount), 0)
                           FROM dbo.ProcurementBudgetCommitmentLedgerEntries existingAllocation
                           WHERE existingAllocation.TenantId = i.TenantId
                             AND existingAllocation.EntryType = 4
                             AND existingAllocation.FormalCommitmentEntryId = parent.Id
                             AND existingAllocation.IsDeleted = 0)
                          + (SELECT COALESCE(SUM(existingAdjustment.DeltaAmount), 0)
                             FROM dbo.ProcurementPurchaseOrderCommitmentAdjustments existingAdjustment
                             JOIN dbo.ProcurementBudgetCommitmentLedgerEntries existingAllocation
                               ON existingAllocation.TenantId = existingAdjustment.TenantId
                              AND existingAllocation.SourceType = N'PurchaseOrder'
                              AND existingAllocation.SourceId = existingAdjustment.PurchaseOrderId
                              AND existingAllocation.EntryType = 4
                              AND existingAllocation.FormalCommitmentEntryId = parent.Id
                              AND existingAllocation.IsDeleted = 0
                             WHERE existingAdjustment.TenantId = i.TenantId
                               AND existingAdjustment.BudgetCommitmentId = parent.ProcurementBudgetCommitmentId
                               AND existingAdjustment.IsDeleted = 0) <=
                          parent.Amount -
                          (SELECT COALESCE(SUM(parentRelease.Amount), 0)
                           FROM dbo.ProcurementBudgetCommitmentLedgerEntries parentRelease
                           WHERE parentRelease.TenantId = parent.TenantId
                             AND parentRelease.EntryType = 3
                             AND parentRelease.FormalCommitmentEntryId = parent.Id
                             AND parentRelease.IsDeleted = 0)
                  )
                )
              )
          )
            THROW 52041, 'A final purchase-order exposure requires its exact active reservation and immutable formal ledger entry.', 1;

          IF EXISTS (
            SELECT 1
            FROM inserted i
            WHERE ISNULL(i.ProcurementSourceType, -1) <> 5
              AND i.Status IN
                  (N'Approved', N'Open', N'Sent', N'Acknowledged',
                   N'Partially Received', N'PartiallyReceived', N'Received')
              AND NOT EXISTS
              (
                SELECT 1
                FROM dbo.ProcurementBudgetCommitments c
                JOIN dbo.ProcurementBudgets b
                  ON b.Id = c.ProcurementBudgetId
                 AND b.TenantId = c.TenantId
                 AND b.IsDeleted = 0
                WHERE c.TenantId = i.TenantId
                  AND c.PurchaseRequisitionId = i.SourceRequisitionId
                  AND c.IsDeleted = 0
                  AND c.Status = 1
                  AND c.ReservedAmount > 0
                  AND c.FormallyCommittedAmount =
                  (SELECT COALESCE(SUM(formal.Amount), 0)
                   FROM dbo.ProcurementBudgetCommitmentLedgerEntries formal
                   WHERE formal.TenantId = c.TenantId
                     AND formal.ProcurementBudgetCommitmentId = c.Id
                     AND formal.EntryType = 1
                     AND formal.IsDeleted = 0)
                  -
                  (SELECT COALESCE(SUM(released.Amount), 0)
                   FROM dbo.ProcurementBudgetCommitmentLedgerEntries released
                   WHERE released.TenantId = c.TenantId
                     AND released.ProcurementBudgetCommitmentId = c.Id
                     AND released.EntryType = 3
                     AND released.FormalCommitmentEntryId IS NOT NULL
                     AND released.IsDeleted = 0)
                  +
                  (SELECT COALESCE(SUM(adjustment.DeltaAmount), 0)
                   FROM dbo.ProcurementPurchaseOrderCommitmentAdjustments adjustment
                   JOIN dbo.ProcurementBudgetCommitmentLedgerEntries directFormal
                     ON directFormal.TenantId = adjustment.TenantId
                    AND directFormal.ProcurementBudgetCommitmentId = adjustment.BudgetCommitmentId
                    AND directFormal.EntryType = 1
                    AND directFormal.SourceType = N'PurchaseOrder'
                    AND directFormal.SourceId = adjustment.PurchaseOrderId
                    AND directFormal.IsDeleted = 0
                   WHERE adjustment.TenantId = c.TenantId
                      AND adjustment.BudgetCommitmentId = c.Id
                      AND adjustment.PurchaseRequisitionId = c.PurchaseRequisitionId
                      AND adjustment.IsDeleted = 0)
                  AND c.UtilizedAmount =
                   (SELECT COALESCE(SUM(utilization.Amount), 0)
                    FROM dbo.ProcurementBudgetCommitmentLedgerEntries utilization
                    WHERE utilization.TenantId = c.TenantId
                      AND utilization.ProcurementBudgetCommitmentId = c.Id
                      AND utilization.EntryType = 2
                      AND utilization.IsDeleted = 0)
                  AND b.ReservedAmount >=
                   (SELECT COALESCE(SUM(
                      CASE
                        WHEN projectionCommitment.ReservedAmount >
                             projectionCommitment.FormallyCommittedAmount
                          THEN projectionCommitment.ReservedAmount -
                               projectionCommitment.FormallyCommittedAmount
                        ELSE 0
                      END), 0)
                    FROM dbo.ProcurementBudgetCommitments projectionCommitment
                    WHERE projectionCommitment.TenantId = b.TenantId
                      AND projectionCommitment.ProcurementBudgetId = b.Id
                      AND projectionCommitment.IsDeleted = 0)
                  AND b.CommittedAmount >=
                   (SELECT COALESCE(SUM(
                      CASE
                        WHEN projectionCommitment.FormallyCommittedAmount >
                             projectionCommitment.UtilizedAmount
                          THEN projectionCommitment.FormallyCommittedAmount -
                               projectionCommitment.UtilizedAmount
                        ELSE 0
                      END), 0)
                    FROM dbo.ProcurementBudgetCommitments projectionCommitment
                    WHERE projectionCommitment.TenantId = b.TenantId
                      AND projectionCommitment.ProcurementBudgetId = b.Id
                      AND projectionCommitment.IsDeleted = 0)
                  AND b.UtilizedAmount >=
                   (SELECT COALESCE(SUM(projectionCommitment.UtilizedAmount), 0)
                    FROM dbo.ProcurementBudgetCommitments projectionCommitment
                    WHERE projectionCommitment.TenantId = b.TenantId
                      AND projectionCommitment.ProcurementBudgetId = b.Id
                      AND projectionCommitment.IsDeleted = 0)
              )
          )
            THROW 52041, 'A final purchase-order exposure requires exact aggregate and Finance projection of the immutable formal ledger.', 1;

          IF EXISTS (
            SELECT 1
            FROM inserted i
            JOIN deleted d ON d.Id = i.Id AND d.TenantId = i.TenantId
            WHERE ISNULL(i.ProcurementSourceType, -1) <> 5
              AND i.Status IN
                  (N'Approved', N'Open', N'Sent', N'Acknowledged',
                   N'Partially Received', N'PartiallyReceived', N'Received')
              AND
              (
                ISNULL(d.Status, N'') = N'Amendment Pending Approval'
                OR
                (
                  ISNULL(d.Status, N'') IN
                    (N'Approved', N'Open', N'Sent', N'Acknowledged',
                     N'Partially Received', N'PartiallyReceived', N'Received')
                  AND
                  (
                    d.TotalAmount <> i.TotalAmount
                    OR UPPER(LTRIM(RTRIM(ISNULL(d.Currency, N'')))) <>
                       UPPER(LTRIM(RTRIM(ISNULL(i.Currency, N''))))
                    OR ISNULL(d.SourceRequisitionId,
                              '00000000-0000-0000-0000-000000000000') <>
                       ISNULL(i.SourceRequisitionId,
                              '00000000-0000-0000-0000-000000000000')
                    OR ISNULL(d.ContractId,
                              '00000000-0000-0000-0000-000000000000') <>
                       ISNULL(i.ContractId,
                              '00000000-0000-0000-0000-000000000000')
                    OR ISNULL(d.ProcurementSourceType, -1) <>
                       ISNULL(i.ProcurementSourceType, -1)
                    OR ISNULL(d.ProcurementSourceId,
                              '00000000-0000-0000-0000-000000000000') <>
                       ISNULL(i.ProcurementSourceId,
                              '00000000-0000-0000-0000-000000000000')
                  )
                )
              )
              AND NOT EXISTS
              (
                SELECT 1
                FROM dbo.ProcurementBudgetCommitmentLedgerEntries exposure
                WHERE exposure.TenantId = i.TenantId
                  AND exposure.SourceType = N'PurchaseOrder'
                  AND exposure.SourceId = i.Id
                  AND exposure.PurchaseRequisitionId = i.SourceRequisitionId
                  AND exposure.IsDeleted = 0
                  AND UPPER(LTRIM(RTRIM(exposure.Currency))) =
                      UPPER(LTRIM(RTRIM(i.Currency)))
                  AND
                  (
                    (i.ContractId IS NULL AND exposure.EntryType = 1)
                    OR
                    (i.ContractId IS NOT NULL AND exposure.EntryType = 4
                     AND EXISTS
                     (
                       SELECT 1
                       FROM dbo.ProcurementBudgetCommitmentLedgerEntries parent
                       WHERE parent.Id = exposure.FormalCommitmentEntryId
                         AND parent.TenantId = i.TenantId
                         AND parent.EntryType = 1
                         AND parent.SourceType = N'Contract'
                         AND parent.SourceId = i.ContractId
                         AND parent.IsDeleted = 0
                     ))
                  )
                  AND exposure.Amount +
                      (SELECT COALESCE(SUM(adjustment.DeltaAmount), 0)
                       FROM dbo.ProcurementPurchaseOrderCommitmentAdjustments adjustment
                       WHERE adjustment.TenantId = i.TenantId
                         AND adjustment.PurchaseOrderId = i.Id
                         AND adjustment.PurchaseRequisitionId = i.SourceRequisitionId
                         AND adjustment.BudgetCommitmentId = exposure.ProcurementBudgetCommitmentId
                         AND UPPER(LTRIM(RTRIM(adjustment.Currency))) =
                             UPPER(LTRIM(RTRIM(i.Currency)))
                         AND adjustment.IsDeleted = 0) = i.TotalAmount
                  AND NOT EXISTS
                  (
                    SELECT 1
                    FROM dbo.ProcurementPurchaseOrderCommitmentAdjustments invalidAdjustment
                    WHERE invalidAdjustment.TenantId = i.TenantId
                      AND invalidAdjustment.PurchaseOrderId = i.Id
                      AND invalidAdjustment.IsDeleted = 0
                      AND
                      (
                        invalidAdjustment.PurchaseRequisitionId <> i.SourceRequisitionId
                        OR invalidAdjustment.BudgetCommitmentId <>
                           exposure.ProcurementBudgetCommitmentId
                        OR UPPER(LTRIM(RTRIM(invalidAdjustment.Currency))) <>
                           UPPER(LTRIM(RTRIM(i.Currency)))
                      )
                  )
              )
          )
            THROW 52041, 'A final purchase-order amendment requires an exact immutable effective exposure ledger.', 1;
        END;
        """;

    // Restores the immediately preceding 20260828190000 behavior if this
    // corrective migration is rolled back.
    private const string PreviousTriggerSql = """
        CREATE OR ALTER TRIGGER [dbo].[TR_PurchaseOrders_GovernedCommitment]
        ON [dbo].[PurchaseOrders] AFTER INSERT, UPDATE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS (
            SELECT 1 FROM inserted i
            LEFT JOIN deleted d ON d.Id = i.Id
            LEFT JOIN dbo.ProcurementRequisitionSourcingReleases r ON r.Id = i.SourcingReleaseId
             AND r.TenantId = i.TenantId AND r.PurchaseRequisitionId = i.SourceRequisitionId AND r.IsDeleted = 0
            LEFT JOIN dbo.PurchaseRequisitions pr ON pr.Id = i.SourceRequisitionId
             AND pr.TenantId = i.TenantId AND pr.IsDeleted = 0
            LEFT JOIN dbo.ProcurementBudgetCommitments c ON c.Id = r.BudgetCommitmentId
             AND c.TenantId = i.TenantId AND c.PurchaseRequisitionId = i.SourceRequisitionId AND c.IsDeleted = 0
            LEFT JOIN dbo.ProcurementBudgets b ON b.Id = c.ProcurementBudgetId
             AND b.TenantId = i.TenantId AND b.IsDeleted = 0
            WHERE i.ProcurementSourceType <> 5
              AND (d.Id IS NULL OR (ISNULL(d.Status, '') <> ISNULL(i.Status, '')
                   AND i.Status IN ('Submitted', 'Pending Approval', 'Approved', 'Sent', 'Acknowledged')))
              AND (r.Id IS NULL OR pr.Id IS NULL OR c.Id IS NULL OR b.Id IS NULL
                OR r.BudgetCommitmentReference <> c.ReservationReference OR pr.BudgetId <> c.ProcurementBudgetId
                OR c.Status <> 1 OR c.ReservedAmount <= 0
                OR UPPER(LTRIM(RTRIM(c.Currency))) <> UPPER(LTRIM(RTRIM(i.Currency)))
                OR UPPER(LTRIM(RTRIM(c.Currency))) <> UPPER(LTRIM(RTRIM(b.Currency)))
                OR b.Status NOT IN ('Approved', 'Active') OR b.ApprovedById IS NULL OR b.ApprovedDate IS NULL
                OR (b.EffectiveDate IS NOT NULL AND b.EffectiveDate > SYSUTCDATETIME())
                OR (b.ExpiryDate IS NOT NULL AND b.ExpiryDate < SYSUTCDATETIME())
                OR b.ReservedAmount + c.FormallyCommittedAmount < c.ReservedAmount
                OR (SELECT COALESCE(SUM(po.TotalAmount), 0) FROM dbo.PurchaseOrders po
                    WHERE po.TenantId = i.TenantId AND po.SourceRequisitionId = i.SourceRequisitionId
                      AND po.IsDeleted = 0 AND po.Status NOT IN ('Cancelled', 'Rejected')) > c.ReservedAmount))
            THROW 52041, 'Purchase-order issuance requires the exact active tenant reservation with sufficient exposure.', 1;
        END;
        """;
}
