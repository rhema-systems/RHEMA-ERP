using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260828190000_AlignProcurementReservationAndFormalCommitmentLifecycle")]
public partial class AlignProcurementReservationAndFormalCommitmentLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "ReservedAmount", table: "ProcurementBudgets", type: "decimal(18,2)",
            nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>(
            name: "FormallyCommittedAmount", table: "ProcurementBudgetCommitments", type: "decimal(18,2)",
            nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>(
            name: "UtilizedAmount", table: "ProcurementBudgetCommitments", type: "decimal(18,2)",
            nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>(
            name: "BudgetReservedBefore", table: "ProcurementBudgetCommitments", type: "decimal(18,2)",
            nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>(
            name: "BudgetReservedAfter", table: "ProcurementBudgetCommitments", type: "decimal(18,2)",
            nullable: false, defaultValue: 0m);

        migrationBuilder.CreateTable(
            name: "ProcurementBudgetCommitmentLedgerEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProcurementBudgetCommitmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProcurementBudgetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PurchaseRequisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EntryType = table.Column<int>(type: "int", nullable: false),
                FormalCommitmentEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SourceType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ActorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
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
                table.PrimaryKey("PK_ProcurementBudgetCommitmentLedgerEntries", x => x.Id);
                table.CheckConstraint("CK_ProcurementBudgetCommitmentLedgerEntries_Amount", "[Amount] > 0");
                table.CheckConstraint("CK_ProcurementBudgetCommitmentLedgerEntries_EntryType", "[EntryType] IN (1, 2, 3, 4)");
                table.ForeignKey("FK_ProcurementBudgetCommitmentLedgerEntries_ProcurementBudgetCommitments_ProcurementBudgetCommitmentId",
                    x => x.ProcurementBudgetCommitmentId, "ProcurementBudgetCommitments", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementBudgetCommitmentLedgerEntries_ProcurementBudgets_ProcurementBudgetId",
                    x => x.ProcurementBudgetId, "ProcurementBudgets", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementBudgetCommitmentLedgerEntries_PurchaseRequisitions_PurchaseRequisitionId",
                    x => x.PurchaseRequisitionId, "PurchaseRequisitions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementBudgetCommitmentLedgerEntries_ProcurementBudgetCommitmentLedgerEntries_FormalCommitmentEntryId",
                    x => x.FormalCommitmentEntryId, "ProcurementBudgetCommitmentLedgerEntries", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementBudgetCommitmentLedgerEntries_Tenants_TenantId",
                    x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProcurementBudgetCommitmentLedgerEntries_ProcurementBudgetCommitmentId",
            table: "ProcurementBudgetCommitmentLedgerEntries", column: "ProcurementBudgetCommitmentId");
        migrationBuilder.CreateIndex(
            name: "IX_ProcurementBudgetCommitmentLedgerEntries_ProcurementBudgetId",
            table: "ProcurementBudgetCommitmentLedgerEntries", column: "ProcurementBudgetId");
        migrationBuilder.CreateIndex(
            name: "IX_ProcurementBudgetCommitmentLedgerEntries_PurchaseRequisitionId",
            table: "ProcurementBudgetCommitmentLedgerEntries", column: "PurchaseRequisitionId");
        migrationBuilder.CreateIndex(
            name: "IX_ProcurementBudgetCommitmentLedgerEntries_FormalCommitmentEntryId",
            table: "ProcurementBudgetCommitmentLedgerEntries", column: "FormalCommitmentEntryId");
        migrationBuilder.CreateIndex(
            name: "IX_ProcurementBudgetCommitmentLedgerEntries_TenantId_EntryType_SourceType_SourceId",
            table: "ProcurementBudgetCommitmentLedgerEntries",
            columns: new[] { "TenantId", "EntryType", "SourceType", "SourceId" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_ProcurementBudgetCommitmentLedgerEntries_TenantId_ProcurementBudgetCommitmentId_OccurredAtUtc",
            table: "ProcurementBudgetCommitmentLedgerEntries",
            columns: new[] { "TenantId", "ProcurementBudgetCommitmentId", "OccurredAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_ProcurementBudgetCommitmentLedgerEntries_TenantId_FormalCommitmentEntryId_EntryType",
            table: "ProcurementBudgetCommitmentLedgerEntries",
            columns: new[] { "TenantId", "FormalCommitmentEntryId", "EntryType" });

        migrationBuilder.Sql("DISABLE TRIGGER [dbo].[TR_ProcurementBudgetCommitments_TenantAndEvidenceGuard] ON [dbo].[ProcurementBudgetCommitments];");
        migrationBuilder.Sql("DISABLE TRIGGER [dbo].[TR_ProcurementBudgetCommitments_LifecycleGuard] ON [dbo].[ProcurementBudgetCommitments];");
        migrationBuilder.Sql(BackfillSql);
        migrationBuilder.Sql(CommitmentEvidenceTriggerSql);
        migrationBuilder.Sql(CommitmentLifecycleTriggerSql);
        migrationBuilder.Sql("ENABLE TRIGGER [dbo].[TR_ProcurementBudgetCommitments_TenantAndEvidenceGuard] ON [dbo].[ProcurementBudgetCommitments];");
        migrationBuilder.Sql("ENABLE TRIGGER [dbo].[TR_ProcurementBudgetCommitments_LifecycleGuard] ON [dbo].[ProcurementBudgetCommitments];");
        migrationBuilder.Sql(LedgerImmutableTriggerSql);
        migrationBuilder.Sql(PurchaseOrderCommitmentTriggerSql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementBudgetCommitmentLedgerEntries_Immutable];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_PurchaseOrders_GovernedCommitment];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementBudgetCommitments_TenantAndEvidenceGuard];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementBudgetCommitments_LifecycleGuard];");
        migrationBuilder.DropTable(name: "ProcurementBudgetCommitmentLedgerEntries");
        migrationBuilder.DropColumn(name: "ReservedAmount", table: "ProcurementBudgets");
        migrationBuilder.DropColumn(name: "FormallyCommittedAmount", table: "ProcurementBudgetCommitments");
        migrationBuilder.DropColumn(name: "UtilizedAmount", table: "ProcurementBudgetCommitments");
        migrationBuilder.DropColumn(name: "BudgetReservedBefore", table: "ProcurementBudgetCommitments");
        migrationBuilder.DropColumn(name: "BudgetReservedAfter", table: "ProcurementBudgetCommitments");
        migrationBuilder.Sql(PreviousCommitmentEvidenceTriggerSql);
        migrationBuilder.Sql(PreviousCommitmentLifecycleTriggerSql);
        migrationBuilder.Sql(PreviousPurchaseOrderCommitmentTriggerSql);
    }

    private const string BackfillSql = """
        DECLARE @systemUser uniqueidentifier = '00000000-0000-0000-0000-000000000000';

        IF EXISTS
        (
          SELECT c.Id
          FROM dbo.ProcurementBudgetCommitments c
          JOIN dbo.Tenders tender ON tender.SourcePurchaseRequisitionId = c.PurchaseRequisitionId
            AND tender.TenantId = c.TenantId AND tender.IsDeleted = 0
          JOIN dbo.Contracts contract ON contract.TenderId = tender.Id
            AND contract.TenantId = c.TenantId AND contract.IsDeleted = 0
          WHERE c.Status = 1 AND contract.Status = N'Active' AND contract.ContractValue > 0
          GROUP BY c.Id, c.ReservedAmount
          HAVING SUM(contract.ContractValue) > c.ReservedAmount
        )
          THROW 52052, 'Migration blocked: cumulative active contract exposure exceeds a purchase-requisition reservation.', 1;

        IF EXISTS
        (
          SELECT contract.Id
          FROM dbo.Contracts contract
          JOIN dbo.PurchaseOrders po ON po.ContractId = contract.Id
            AND po.TenantId = contract.TenantId AND po.IsDeleted = 0 AND po.TotalAmount > 0
            AND po.Status IN (N'Approved', N'Open', N'Sent', N'Acknowledged', N'Partially Received', N'Received')
          WHERE contract.IsDeleted = 0 AND contract.Status = N'Active' AND contract.ContractValue > 0
          GROUP BY contract.Id, contract.ContractValue
          HAVING SUM(po.TotalAmount) > contract.ContractValue
        )
          THROW 52053, 'Migration blocked: cumulative child purchase-order exposure exceeds its active contract.', 1;

        ;WITH ContractCandidate AS
        (
          SELECT contract.*, c.Id CommitmentId, c.ProcurementBudgetId, c.PurchaseRequisitionId,
                 c.ReservedAmount, c.ReservedById,
                 SUM(contract.ContractValue) OVER
                   (PARTITION BY c.Id ORDER BY COALESCE(contract.ActivatedAt, contract.SignedDate, contract.CreatedAt), contract.Id) RunningAmount
          FROM dbo.ProcurementBudgetCommitments c
          JOIN dbo.Tenders tender ON tender.SourcePurchaseRequisitionId = c.PurchaseRequisitionId
            AND tender.TenantId = c.TenantId AND tender.IsDeleted = 0
          JOIN dbo.Contracts contract ON contract.TenderId = tender.Id
            AND contract.TenantId = c.TenantId AND contract.IsDeleted = 0
          WHERE c.Status = 1 AND contract.Status = N'Active' AND contract.ContractValue > 0
        )
        INSERT INTO dbo.ProcurementBudgetCommitmentLedgerEntries
        (Id, ProcurementBudgetCommitmentId, ProcurementBudgetId, PurchaseRequisitionId, EntryType,
         FormalCommitmentEntryId, SourceType, SourceId, SourceReference, Amount, Currency,
         OccurredAtUtc, ActorUserId, ActorName, CorrelationId, CreatedAt, CreatedBy,
         IsDeleted, TenantId)
        SELECT NEWID(), contract.CommitmentId, contract.ProcurementBudgetId, contract.PurchaseRequisitionId, 1, NULL,
               N'Contract', contract.Id, contract.ContractNumber, contract.ContractValue,
               UPPER(LTRIM(RTRIM(contract.Currency))), COALESCE(contract.ActivatedAt, contract.SignedDate, SYSUTCDATETIME()),
               COALESCE(contract.SignedById, contract.ReservedById, @systemUser), N'Migration backfill',
               N'FR-PR-005-MIGRATION', SYSUTCDATETIME(), N'Migration', 0, contract.TenantId
        FROM ContractCandidate contract
        WHERE contract.RunningAmount <= contract.ReservedAmount;

        IF EXISTS
        (
          SELECT c.Id
          FROM dbo.ProcurementBudgetCommitments c
          OUTER APPLY
          (
            SELECT COALESCE(SUM(l.Amount), 0) ContractAmount
            FROM dbo.ProcurementBudgetCommitmentLedgerEntries l
            WHERE l.ProcurementBudgetCommitmentId = c.Id AND l.EntryType = 1
          ) formal
          OUTER APPLY
          (
            SELECT COALESCE(SUM(po.TotalAmount), 0) DirectPurchaseOrderAmount
            FROM dbo.PurchaseOrders po
            WHERE po.TenantId = c.TenantId AND po.SourceRequisitionId = c.PurchaseRequisitionId
              AND po.IsDeleted = 0 AND po.TotalAmount > 0 AND po.ContractId IS NULL
              AND po.Status IN (N'Approved', N'Open', N'Sent', N'Acknowledged', N'Partially Received', N'Received')
          ) purchaseOrders
          WHERE c.Status = 1
            AND formal.ContractAmount + purchaseOrders.DirectPurchaseOrderAmount > c.ReservedAmount
        )
          THROW 52054, 'Migration blocked: cumulative formal contract and direct purchase-order exposure exceeds a purchase-requisition reservation.', 1;

        ;WITH Candidate AS
        (
            SELECT po.*, c.Id CommitmentId, c.ProcurementBudgetId,
                   SUM(po.TotalAmount) OVER (PARTITION BY c.Id ORDER BY po.CreatedAt, po.Id) RunningAmount
            FROM dbo.PurchaseOrders po
            JOIN dbo.ProcurementBudgetCommitments c ON c.PurchaseRequisitionId = po.SourceRequisitionId
             AND c.TenantId = po.TenantId AND c.IsDeleted = 0 AND c.Status = 1
            WHERE po.IsDeleted = 0 AND po.TotalAmount > 0
              AND po.Status IN (N'Approved', N'Open', N'Sent', N'Acknowledged', N'Partially Received', N'Received')
              AND (po.ContractId IS NULL OR NOT EXISTS
                  (SELECT 1 FROM dbo.ProcurementBudgetCommitmentLedgerEntries ce
                   WHERE ce.TenantId = po.TenantId AND ce.EntryType = 1
                     AND ce.SourceType = N'Contract' AND ce.SourceId = po.ContractId))
        )
        INSERT INTO dbo.ProcurementBudgetCommitmentLedgerEntries
        (Id, ProcurementBudgetCommitmentId, ProcurementBudgetId, PurchaseRequisitionId, EntryType,
         FormalCommitmentEntryId, SourceType, SourceId, SourceReference, Amount, Currency,
         OccurredAtUtc, ActorUserId, ActorName, CorrelationId, CreatedAt, CreatedBy,
         IsDeleted, TenantId)
        SELECT NEWID(), p.CommitmentId, p.ProcurementBudgetId, p.SourceRequisitionId, 1, NULL,
               N'PurchaseOrder', p.Id, p.OrderNumber, p.TotalAmount,
               UPPER(LTRIM(RTRIM(p.Currency))), COALESCE(p.ApprovedAt, p.UpdatedAt, p.CreatedAt),
               COALESCE(p.ApprovedById, p.CreatedById, @systemUser), N'Migration backfill',
               N'FR-PR-005-MIGRATION', SYSUTCDATETIME(), N'Migration', 0, p.TenantId
        FROM Candidate p
        JOIN dbo.ProcurementBudgetCommitments c ON c.Id = p.CommitmentId
        WHERE p.RunningAmount <= c.ReservedAmount;

        ;WITH AllocationCandidate AS
        (
          SELECT po.*, ce.Id ContractCommitmentEntryId, ce.ProcurementBudgetCommitmentId,
                 ce.ProcurementBudgetId, ce.PurchaseRequisitionId, ce.Currency CommitmentCurrency,
                 SUM(po.TotalAmount) OVER
                   (PARTITION BY ce.Id ORDER BY COALESCE(po.ApprovedAt, po.UpdatedAt, po.CreatedAt), po.Id) RunningAmount,
                 ce.Amount ContractAmount
          FROM dbo.PurchaseOrders po
          JOIN dbo.ProcurementBudgetCommitmentLedgerEntries ce ON ce.TenantId = po.TenantId
            AND ce.EntryType = 1 AND ce.SourceType = N'Contract' AND ce.SourceId = po.ContractId
          WHERE po.IsDeleted = 0 AND po.TotalAmount > 0
            AND po.Status IN (N'Approved', N'Open', N'Sent', N'Acknowledged', N'Partially Received', N'Received')
        )
        INSERT INTO dbo.ProcurementBudgetCommitmentLedgerEntries
        (Id, ProcurementBudgetCommitmentId, ProcurementBudgetId, PurchaseRequisitionId, EntryType,
         FormalCommitmentEntryId, SourceType, SourceId, SourceReference, Amount, Currency,
         OccurredAtUtc, ActorUserId, ActorName, CorrelationId, CreatedAt, CreatedBy,
         IsDeleted, TenantId)
        SELECT NEWID(), po.ProcurementBudgetCommitmentId, po.ProcurementBudgetId, po.PurchaseRequisitionId,
               4, po.ContractCommitmentEntryId, N'PurchaseOrder', po.Id, po.OrderNumber, po.TotalAmount,
               po.CommitmentCurrency, COALESCE(po.ApprovedAt, po.UpdatedAt, po.CreatedAt),
               COALESCE(po.ApprovedById, po.CreatedById, @systemUser), N'Migration backfill',
               N'FR-PR-005-MIGRATION', SYSUTCDATETIME(), N'Migration', 0, po.TenantId
        FROM AllocationCandidate po
        WHERE po.RunningAmount <= po.ContractAmount;

        UPDATE c SET
          c.FormallyCommittedAmount = x.FormalAmount,
          c.BudgetReservedBefore = 0,
          c.BudgetReservedAfter = CASE WHEN c.ReservedAmount > x.FormalAmount THEN c.ReservedAmount - x.FormalAmount ELSE 0 END
        FROM dbo.ProcurementBudgetCommitments c
        CROSS APPLY (SELECT COALESCE(SUM(l.Amount), 0) FormalAmount
                     FROM dbo.ProcurementBudgetCommitmentLedgerEntries l
                     WHERE l.ProcurementBudgetCommitmentId = c.Id AND l.EntryType = 1) x;

        ;WITH AggregateAmounts AS
        (
            SELECT c.ProcurementBudgetId,
                   SUM(CASE WHEN c.Status = 1 AND c.ReservedAmount > c.FormallyCommittedAmount
                            THEN c.ReservedAmount - c.FormallyCommittedAmount ELSE 0 END) ReservedAmount,
                   SUM(CASE WHEN c.Status = 1 THEN c.ReservedAmount ELSE 0 END) OldReservationAmount,
                   SUM(CASE WHEN c.Status = 1 THEN c.FormallyCommittedAmount ELSE 0 END) FormalAmount
            FROM dbo.ProcurementBudgetCommitments c
            WHERE c.IsDeleted = 0
            GROUP BY c.ProcurementBudgetId
        )
        UPDATE b SET
          b.ReservedAmount = a.ReservedAmount,
          b.CommittedAmount = CASE WHEN b.CommittedAmount - a.OldReservationAmount + a.FormalAmount > 0
                                   THEN b.CommittedAmount - a.OldReservationAmount + a.FormalAmount ELSE 0 END,
          b.RemainingAmount = b.AllocatedAmount - b.UtilizedAmount
             - CASE WHEN b.CommittedAmount - a.OldReservationAmount + a.FormalAmount > 0
                    THEN b.CommittedAmount - a.OldReservationAmount + a.FormalAmount ELSE 0 END
             - a.ReservedAmount
        FROM dbo.ProcurementBudgets b JOIN AggregateAmounts a ON a.ProcurementBudgetId = b.Id;

        UPDATE c SET c.BudgetCommittedAfter = b.CommittedAmount,
                      c.BudgetReservedAfter = b.ReservedAmount,
                      c.BudgetAvailableAfter = b.RemainingAmount,
                      c.BudgetAllocatedSnapshot = b.AllocatedAmount,
                      c.BudgetUtilizedSnapshot = b.UtilizedAmount
        FROM dbo.ProcurementBudgetCommitments c
        JOIN dbo.ProcurementBudgets b ON b.Id = c.ProcurementBudgetId;
        """;

    private const string CommitmentEvidenceTriggerSql = """
        CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementBudgetCommitments_TenantAndEvidenceGuard]
        ON [dbo].[ProcurementBudgetCommitments] AFTER INSERT, UPDATE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS (
            SELECT 1 FROM inserted i
            LEFT JOIN deleted d ON d.Id = i.Id
            JOIN dbo.ProcurementBudgets b ON b.Id = i.ProcurementBudgetId
            JOIN dbo.PurchaseRequisitions pr ON pr.Id = i.PurchaseRequisitionId
            LEFT JOIN dbo.ProcurementPolicyExceptionRules er ON er.Id = i.OverrideRuleId
            LEFT JOIN dbo.WorkflowInstances wi ON wi.Id = i.OverrideWorkflowInstanceId
            WHERE b.TenantId <> i.TenantId OR pr.TenantId <> i.TenantId OR b.IsDeleted = 1 OR pr.IsDeleted = 1
              OR (i.OverrideRuleId IS NOT NULL AND (er.TenantId <> i.TenantId OR er.IsDeleted = 1))
              OR (i.OverrideWorkflowInstanceId IS NOT NULL AND (wi.TenantId <> i.TenantId OR wi.IsDeleted = 1))
              OR (i.Status = 1 AND (i.ReleasedAtUtc IS NOT NULL OR i.ReleasedById IS NOT NULL OR i.ReleaseReason IS NOT NULL))
              OR (i.Status = 2 AND (i.ReleasedAtUtc IS NULL OR i.ReleasedById IS NULL OR NULLIF(LTRIM(RTRIM(i.ReleaseReason)), N'') IS NULL))
              OR (d.Id IS NULL AND i.BudgetAvailableBefore <> i.BudgetAllocatedSnapshot - i.BudgetUtilizedSnapshot - i.BudgetCommittedBefore - i.BudgetReservedBefore)
              OR i.BudgetAvailableAfter <> i.BudgetAllocatedSnapshot - i.BudgetUtilizedSnapshot - i.BudgetCommittedAfter - i.BudgetReservedAfter
              OR (d.Id IS NULL AND i.Status = 1 AND
                  (i.BudgetCommittedAfter <> i.BudgetCommittedBefore
                   OR i.BudgetReservedAfter <> i.BudgetReservedBefore + i.ReservedAmount
                   OR i.BudgetAvailableAfter <> i.BudgetAvailableBefore - i.ReservedAmount))
              OR (i.IsOverride = 1 AND (i.OverrideRuleId IS NULL OR i.OverrideWorkflowInstanceId IS NULL
                  OR NULLIF(LTRIM(RTRIM(i.OverrideApprovalReference)), N'') IS NULL
                  OR NULLIF(LTRIM(RTRIM(i.OverrideEvidenceReference)), N'') IS NULL OR i.OverrideApprovedAtUtc IS NULL))
              OR (i.IsOverride = 0 AND (i.OverrideRuleId IS NOT NULL OR i.OverrideWorkflowInstanceId IS NOT NULL
                  OR i.OverrideApprovalReference IS NOT NULL OR i.OverrideEvidenceReference IS NOT NULL OR i.OverrideApprovedAtUtc IS NOT NULL)))
            THROW 51021, 'Budget reservation tenant, lifecycle, snapshot, or override evidence is invalid.', 1;
        END;
        """;

    private const string CommitmentLifecycleTriggerSql = """
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

    private const string LedgerImmutableTriggerSql = """
        CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementBudgetCommitmentLedgerEntries_Immutable]
        ON [dbo].[ProcurementBudgetCommitmentLedgerEntries] AFTER UPDATE, DELETE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS (SELECT 1 FROM deleted)
            THROW 52051, 'Formal commitment ledger entries are immutable and cannot be updated or deleted.', 1;
        END;
        """;

    private const string PurchaseOrderCommitmentTriggerSql = """
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

    private const string PreviousCommitmentEvidenceTriggerSql = """
        CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementBudgetCommitments_TenantAndEvidenceGuard]
        ON [dbo].[ProcurementBudgetCommitments] AFTER INSERT, UPDATE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS (
            SELECT 1 FROM inserted i
            JOIN dbo.ProcurementBudgets b ON b.Id = i.ProcurementBudgetId
            JOIN dbo.PurchaseRequisitions pr ON pr.Id = i.PurchaseRequisitionId
            LEFT JOIN dbo.ProcurementPolicyExceptionRules er ON er.Id = i.OverrideRuleId
            LEFT JOIN dbo.WorkflowInstances wi ON wi.Id = i.OverrideWorkflowInstanceId
            WHERE b.TenantId <> i.TenantId OR pr.TenantId <> i.TenantId OR b.IsDeleted = 1 OR pr.IsDeleted = 1
              OR (i.OverrideRuleId IS NOT NULL AND (er.TenantId <> i.TenantId OR er.IsDeleted = 1))
              OR (i.OverrideWorkflowInstanceId IS NOT NULL AND (wi.TenantId <> i.TenantId OR wi.IsDeleted = 1))
              OR (i.Status = 1 AND (i.ReleasedAtUtc IS NOT NULL OR i.ReleasedById IS NOT NULL OR i.ReleaseReason IS NOT NULL))
              OR (i.Status = 2 AND (i.ReleasedAtUtc IS NULL OR i.ReleasedById IS NULL OR NULLIF(LTRIM(RTRIM(i.ReleaseReason)), N'') IS NULL))
              OR i.BudgetAvailableBefore <> i.BudgetAllocatedSnapshot - i.BudgetUtilizedSnapshot - i.BudgetCommittedBefore
              OR i.BudgetAvailableAfter <> i.BudgetAllocatedSnapshot - i.BudgetUtilizedSnapshot - i.BudgetCommittedAfter
              OR (i.Status = 1 AND (i.BudgetCommittedAfter <> i.BudgetCommittedBefore + i.ReservedAmount
                  OR i.BudgetAvailableAfter <> i.BudgetAvailableBefore - i.ReservedAmount)))
            THROW 51021, 'Budget commitment tenant, lifecycle, snapshot, or override evidence is invalid.', 1;
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
              OR i.ReservationReference <> d.ReservationReference OR i.CreatedAt <> d.CreatedAt
              OR i.IsDeleted <> d.IsDeleted
              OR NOT ((d.Status = 1 AND i.Status IN (2, 3) AND i.ReservationSequence = d.ReservationSequence)
                   OR (d.Status = 2 AND i.Status = 1 AND i.ReservationSequence = d.ReservationSequence + 1)))
            THROW 51022, 'Budget commitment identity, retention, or lifecycle transition is immutable.', 1;
        END;
        """;

    private const string PreviousPurchaseOrderCommitmentTriggerSql = """
        CREATE OR ALTER TRIGGER [dbo].[TR_PurchaseOrders_GovernedCommitment]
        ON [dbo].[PurchaseOrders] AFTER INSERT, UPDATE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS (
            SELECT 1 FROM inserted i
            LEFT JOIN deleted d ON d.Id = i.Id
            LEFT JOIN dbo.ProcurementRequisitionSourcingReleases r ON r.Id = i.SourcingReleaseId
             AND r.TenantId = i.TenantId AND r.PurchaseRequisitionId = i.SourceRequisitionId AND r.IsDeleted = 0
            LEFT JOIN dbo.ProcurementBudgetCommitments c ON c.Id = r.BudgetCommitmentId
             AND c.TenantId = i.TenantId AND c.IsDeleted = 0
            LEFT JOIN dbo.ProcurementBudgets b ON b.Id = c.ProcurementBudgetId
             AND b.TenantId = i.TenantId AND b.IsDeleted = 0
            WHERE i.ProcurementSourceType <> 5
              AND (d.Id IS NULL OR (ISNULL(d.Status, '') <> ISNULL(i.Status, '')
                   AND i.Status IN ('Submitted', 'Pending Approval', 'Approved', 'Sent', 'Acknowledged')))
              AND (r.Id IS NULL OR c.Id IS NULL OR b.Id IS NULL OR c.Status <> 1
                OR UPPER(LTRIM(RTRIM(c.Currency))) <> UPPER(LTRIM(RTRIM(i.Currency)))
                OR b.CommittedAmount < c.ReservedAmount
                OR (SELECT COALESCE(SUM(po.TotalAmount), 0) FROM dbo.PurchaseOrders po
                    WHERE po.TenantId = i.TenantId AND po.SourceRequisitionId = i.SourceRequisitionId
                      AND po.IsDeleted = 0 AND po.Status NOT IN ('Cancelled', 'Rejected')) > c.ReservedAmount))
            THROW 52041, 'Purchase-order issuance requires the exact active tenant budget commitment with sufficient reserved exposure.', 1;
        END;
        """;
}
