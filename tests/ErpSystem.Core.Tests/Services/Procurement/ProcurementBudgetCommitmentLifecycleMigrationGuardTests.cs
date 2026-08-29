using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementBudgetCommitmentLifecycleMigrationGuardTests
{
    private const string MigrationId = "20260828190000_AlignProcurementReservationAndFormalCommitmentLifecycle";
    private const string CorrectiveMigrationId = "20260829210000_EnforceAtomicPurchaseOrderBudgetCommitment";

    [Fact]
    public void Migration_separates_reservation_formal_commitment_and_utilization_with_bounded_backfill()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Data", "Migrations", MigrationId + ".cs"));

        migration.Should().Contain("name: \"ReservedAmount\", table: \"ProcurementBudgets\"")
            .And.Contain("FormallyCommittedAmount")
            .And.Contain("UtilizedAmount")
            .And.Contain("ProcurementBudgetCommitmentLedgerEntries")
            .And.Contain("SUM(contract.ContractValue) OVER")
            .And.Contain("COALESCE(p.ApprovedAt, p.UpdatedAt, p.CreatedAt)")
            .And.Contain("COALESCE(po.ApprovedAt, po.UpdatedAt, po.CreatedAt)")
            .And.NotContain("p.ApprovedDate")
            .And.NotContain("po.ApprovedDate")
            .And.Contain("WHERE contract.RunningAmount <= contract.ReservedAmount")
            .And.Contain("PARTITION BY ce.Id ORDER BY")
            .And.Contain("WHERE po.RunningAmount <= po.ContractAmount")
            .And.Contain("THROW 52052")
            .And.Contain("THROW 52053")
            .And.Contain("THROW 52054")
            .And.Contain("PreviousCommitmentEvidenceTriggerSql")
            .And.Contain("PreviousCommitmentLifecycleTriggerSql")
            .And.Contain("PreviousPurchaseOrderCommitmentTriggerSql")
            .And.Contain("d.Id IS NULL AND i.BudgetAvailableBefore")
            .And.Contain("d.Status = 2 AND i.Status = 1")
            .And.Contain("TR_ProcurementBudgetCommitmentLedgerEntries_Immutable");
    }

    [Fact]
    public void Runtime_promotes_only_final_sources_and_prevents_contract_child_po_double_commit()
    {
        var root = FindRepositoryRoot();
        var lifecycle = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services",
            "Procurement", "ProcurementBudgetCommitmentLifecycleService.cs"));
        var purchaseOrders = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Controllers",
            "Procurement", "PurchaseOrdersController.cs"));
        var contracts = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services",
            "Procurement", "ProcurementContractActivationService.cs"));
        var receipts = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services",
            "Procurement", "ProcurementReceiptInspectionService.cs"));

        lifecycle.Should().Contain("BUDGET_LIFECYCLE_SOURCE_TENANT_MISMATCH")
            .And.Contain("PurchaseOrderAllocation")
            .And.Contain("PO_CONTRACT_COMMITMENT_EXCEEDED")
            .And.Contain("PO_CONTRACT_REQUISITION_LINEAGE_MISMATCH")
            .And.Contain("PO_CONTRACT_ALLOCATION_EXCEEDED")
            .And.Contain("BUDGET_UTILIZATION_SOURCE_LINEAGE_INVALID");
        purchaseOrders.Should().Contain("CommitPurchaseOrderAsync")
            .And.Contain("WorkflowOutcome.Approved")
            .And.Contain("PO_COMMITMENT_REVERSAL_REQUIRED")
            .And.Contain("PO_STATUS_DEDICATED_ROUTE_REQUIRED")
            .And.Contain("isGovernedPurchaseOrder")
            .And.Contain("IsFinalExposureStatus(purchaseOrder.Status)")
            .And.Contain("!IsFinalExposureStatus(statusDto.Status)")
            .And.Contain("string.Equals(purchaseOrder.Status, \"Draft\"")
            .And.Contain("string.Equals(statusDto.Status, \"Cancelled\"");
        var genericStatusStart = purchaseOrders.IndexOf(
            "public async Task<IActionResult> UpdatePurchaseOrderStatus(",
            StringComparison.Ordinal);
        var approvalStart = purchaseOrders.IndexOf(
            "public Task<IActionResult> ApprovePurchaseOrder(",
            genericStatusStart,
            StringComparison.Ordinal);
        genericStatusStart.Should().BeGreaterThanOrEqualTo(0);
        approvalStart.Should().BeGreaterThan(genericStatusStart);
        var genericStatusBody = purchaseOrders[genericStatusStart..approvalStart];
        genericStatusBody.Should().Contain("PO_COMMITMENT_REVERSAL_REQUIRED")
            .And.Contain("PO_STATUS_DEDICATED_ROUTE_REQUIRED")
            .And.Contain("AuthorizeDraftCancellationAsync(")
            .And.Contain("PO_STATUS_FORBIDDEN")
            .And.Contain("string.Equals(purchaseOrder.Status, \"Draft\"")
            .And.Contain("string.Equals(statusDto.Status, \"Cancelled\"")
            .And.Contain("UpdateStatusAsync(id, statusDto.Status)");
        contracts.Should().Contain("CommitContractAsync");
        receipts.Should().Contain("UtilizePurchaseOrderAsync");
    }

    [Fact]
    public void Corrective_migration_guards_only_entry_to_final_exposure_and_requires_exact_po_ledger()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Data", "Migrations", CorrectiveMigrationId + ".cs"));
        var upStart = migration.IndexOf("private const string FinalExposureTriggerSql", StringComparison.Ordinal);
        var downStart = migration.IndexOf("private const string PreviousTriggerSql", StringComparison.Ordinal);
        var lifecycleUpStart = migration.IndexOf(
            "private const string FinalCommitmentLifecycleTriggerSql", StringComparison.Ordinal);
        var lifecycleDownStart = migration.IndexOf(
            "private const string PreviousCommitmentLifecycleTriggerSql", StringComparison.Ordinal);
        upStart.Should().BeGreaterThanOrEqualTo(0);
        downStart.Should().BeGreaterThan(upStart);
        lifecycleUpStart.Should().BeGreaterThanOrEqualTo(0);
        lifecycleDownStart.Should().BeGreaterThan(lifecycleUpStart);
        var up = migration[upStart..downStart];
        var lifecycleUp = migration[lifecycleUpStart..lifecycleDownStart];

        migration.Should().Contain($"[Migration(\"{CorrectiveMigrationId}\")]");
        migration.Should().Contain(
                "DROP INDEX IF EXISTS [IX_PurchaseOrders_Status] ON [dbo].[PurchaseOrders]")
            .And.Contain("ALTER COLUMN [Status] nvarchar(50) NOT NULL")
            .And.Contain("CREATE INDEX [IX_PurchaseOrders_Status]")
            .And.Contain("ON [dbo].[PurchaseOrders] ([Status])");
        up.Should().Contain("TR_PurchaseOrders_GovernedCommitment")
            .And.Contain("l.EntryType = 1")
            .And.Contain("l.SourceType = N'PurchaseOrder'")
            .And.Contain("l.SourceId = i.Id")
            .And.Contain("l.Amount +")
            .And.Contain("adjustment.DeltaAmount")
            .And.Contain("allocation.EntryType = 4")
            .And.Contain("parent.Id = allocation.FormalCommitmentEntryId")
            .And.Contain("parent.SourceType = N'Contract'")
            .And.Contain("contract.Status = N'Active'")
            .And.Contain("existingAllocation.FormalCommitmentEntryId = parent.Id")
            .And.Contain("existingAdjustment.DeltaAmount")
            .And.Contain("N'Approved'")
            .And.Contain("N'Open'")
            .And.Contain("N'Sent'")
            .And.Contain("N'Acknowledged'")
            .And.Contain("N'Partially Received'")
            .And.Contain("N'PartiallyReceived'")
            .And.Contain("N'Received'")
            .And.Contain("ISNULL(d.Status, N'') NOT IN")
            .And.NotContain("N'Submitted'")
            .And.NotContain("N'Pending Approval'");
        up.Should().Contain("N'Amendment Pending Approval'")
            .And.Contain("atomic formal-commitment reversal before leaving exposure status")
            .And.Contain("ISNULL(d.ProcurementSourceType, -1) <> 5")
            .And.Contain("ProcurementPurchaseOrderCommitmentAdjustments adjustment")
            .And.Contain("exposure.Amount +")
            .And.Contain("adjustment.DeltaAmount")
            .And.Contain("A final purchase-order amendment requires an exact immutable effective exposure ledger.");
        migration.Should().Contain("FinalCommitmentAmountConstraintSql")
            .And.Contain("[Status] = 2 AND [ReservedAmount] >= 0")
            .And.Contain("[Status] IN (1, 3) AND [ReservedAmount] > 0")
            .And.Contain("FinalCommitmentLifecycleTriggerSql")
            .And.Contain("TR_ProcurementBudgetCommitments_LifecycleGuard")
            .And.Contain("PROCUREMENT_CONTRACT_RELEASE_ID")
            .And.Contain("release.EntryType = 3")
            .And.Contain("release.FormalCommitmentEntryId = formal.Id")
            .And.Contain("d.ReservedAmount - i.ReservedAmount =")
            .And.Contain("d.FormallyCommittedAmount - i.FormallyCommittedAmount")
            .And.Contain("i.ReservedAmount <= i.UtilizedAmount")
            .And.Contain("SESSION_CONTEXT(N'TDC0406_PO_AMENDMENT_ID')")
            .And.Contain("PROCUREMENT_DOWNSTREAM_RESERVATION_TENANT_ID")
            .And.Contain("PROCUREMENT_DOWNSTREAM_RESERVATION_REQUISITION_ID")
            .And.Contain("PROCUREMENT_DOWNSTREAM_RESERVATION_COMMITMENT_ID")
            .And.Contain("PROCUREMENT_DOWNSTREAM_RESERVATION_AMOUNT_BEFORE")
            .And.Contain("PROCUREMENT_DOWNSTREAM_RESERVATION_AMOUNT_AFTER")
            .And.Contain("PROCUREMENT_DOWNSTREAM_RESERVATION_SEQUENCE_BEFORE")
            .And.Contain("PROCUREMENT_DOWNSTREAM_RESERVATION_SEQUENCE_AFTER")
            .And.Contain("PROCUREMENT_DOWNSTREAM_RESERVATION_CORRELATION_ID")
            .And.Contain("ISNULL(downstreamReservation.IsAuthorized, 0) = 1")
            .And.Contain("PROCUREMENT_REQUISITION_RELEASE_TENANT_ID")
            .And.Contain("PROCUREMENT_REQUISITION_RELEASE_REQUISITION_ID")
            .And.Contain("PROCUREMENT_REQUISITION_RELEASE_COMMITMENT_ID")
            .And.Contain("PROCUREMENT_REQUISITION_RELEASE_AMOUNT_BEFORE")
            .And.Contain("PROCUREMENT_REQUISITION_RELEASE_SEQUENCE")
            .And.Contain("PROCUREMENT_REQUISITION_RELEASE_CORRELATION_ID")
            .And.Contain("ISNULL(requisitionRelease.IsAuthorized, 0) = 1")
            .And.Contain("fullyUtilizedContractClose")
            .And.Contain("ISNULL(fullyUtilizedContractClose.IsAuthorized, 0) = 1")
            .And.Contain("i.UtilizedAmount >= i.ReservedAmount")
            .And.Contain("utilization.FormalCommitmentEntryId = formal.Id")
            .And.Contain("i.ReservedAmount <> d.ReservedAmount")
            .And.Contain("formalPromotion")
            .And.Contain("utilizationMutation")
            .And.Contain("contractRelease")
            .And.Contain("PROCUREMENT_FORMAL_LEDGER_ENTRY_ID")
            .And.Contain("PROCUREMENT_FORMAL_AMOUNT_BEFORE")
            .And.Contain("PROCUREMENT_FORMAL_AMOUNT_AFTER")
            .And.Contain("PROCUREMENT_UTILIZATION_LEDGER_ENTRY_ID")
            .And.Contain("PROCUREMENT_UTILIZATION_AMOUNT_BEFORE")
            .And.Contain("PROCUREMENT_UTILIZATION_AMOUNT_AFTER")
            .And.Contain("i.FormallyCommittedAmount <> d.FormallyCommittedAmount")
            .And.Contain("i.UtilizedAmount <> d.UtilizedAmount")
            .And.Contain("purchaseOrder.Id = amendment.PurchaseOrderId")
            .And.Contain("purchaseOrder.Status = N'Amendment Pending Approval'")
            .And.Contain("amendment.PurchaseOrderStatusBefore IN")
            .And.Contain("formal.SourceId = amendment.PurchaseOrderId")
            .And.Contain("amendment.CommitmentDelta =")
            .And.Contain("i.FormallyCommittedAmount - d.FormallyCommittedAmount")
            .And.Contain("i.ReservedAmount - d.ReservedAmount")
            .And.Contain("i.ReservationSequence = d.ReservationSequence + 1")
            .And.Contain("d.FormallyCommittedAmount = COALESCE(")
            .And.Contain("released.EntryType = 3")
            .And.Contain("released.FormalCommitmentEntryId IS NOT NULL")
            .And.Contain("priorDirect.DeltaAmount")
            .And.Contain("PreviousCommitmentAmountConstraintSql")
            .And.Contain("PreviousCommitmentLifecycleTriggerSql");
        lifecycleUp.Should().NotContain("(d.Status = 2 AND i.Status = 1",
            "a released requisition envelope must not reopen without a future amended-PR authorization");
        up.Should().Contain("exact aggregate and Finance projection")
            .And.Contain("AND NOT EXISTS")
            .And.Contain("c.Status = 1")
            .And.Contain("c.ReservedAmount > 0")
            .And.Contain("c.FormallyCommittedAmount =")
            .And.Contain("released.FormalCommitmentEntryId IS NOT NULL")
            .And.Contain("directFormal.SourceType = N'PurchaseOrder'")
            .And.Contain("c.UtilizedAmount =")
            .And.Contain("b.ReservedAmount >=")
            .And.Contain("b.CommittedAmount >=")
            .And.Contain("b.UtilizedAmount >=")
            .And.Contain("projectionCommitment.ProcurementBudgetId = b.Id")
            .And.Contain("projectionCommitment.IsDeleted = 0")
            .And.Contain("parent.Amount -")
            .And.Contain("parentRelease.FormalCommitmentEntryId = parent.Id");
        var modelSnapshot = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Data", "Migrations",
            "ApplicationDbContextModelSnapshot.cs"));
        modelSnapshot.Should().Contain(
            "([Status] = 2 AND [ReservedAmount] >= 0) OR ([Status] IN (1, 3) AND [ReservedAmount] > 0)",
            "the EF model snapshot must match the forward released-envelope constraint");
        modelSnapshot.Should().Contain(".HasMaxLength(50)")
            .And.Contain(".HasColumnType(\"nvarchar(50)\")",
                "the amendment pending status must fit the deployed PurchaseOrders.Status column");
        (up.Split("ISNULL(i.ProcurementSourceType, -1) <> 5").Length - 1)
            .Should().BeGreaterThanOrEqualTo(2,
                "NULL or missing source lineage must never bypass entry or amendment enforcement");

        var contractBranchStart = up.IndexOf("i.ContractId IS NOT NULL", StringComparison.Ordinal);
        contractBranchStart.Should().BeGreaterThanOrEqualTo(0);
        var contractBranch = up[contractBranchStart..];
        contractBranch.Should().NotContain("b.Status")
            .And.NotContain("b.EffectiveDate")
            .And.NotContain("b.ExpiryDate",
                "contract-child allocations inherit an immutable active contract formal commitment");

        var sourceProtection = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations",
            "20260730032601_TDC0406PurchaseOrderAmendments.cs"));
        var commercialProtection = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations",
            "20260730204500_TDC0403CommercialCapacityHardStops.cs"));
        sourceProtection.Should().Contain("TR_PurchaseOrders_ApprovedSourceProtected")
            .And.Contain("TDC0406_PO_AMENDMENT_ID")
            .And.Contain("i.SourceIntegrityHash");
        commercialProtection.Should().Contain("TR_PurchaseOrders_ApprovedCommercialCapacity")
            .And.Contain("i.TotalAmount")
            .And.Contain("i.Currency");

        var amendmentService = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services",
            "Procurement", "ProcurementPurchaseOrderAmendmentService.cs"));
        amendmentService.IndexOf("SetApprovedSourceMutationContextAsync(", StringComparison.Ordinal)
            .Should().BeLessThan(
                amendmentService.IndexOf("adjustment = await AdjustCommitmentAsync(", StringComparison.Ordinal),
                "the exact applied amendment context must authorize the aggregate mutation first");
        amendmentService.IndexOf("adjustment = await AdjustCommitmentAsync(", StringComparison.Ordinal)
            .Should().BeLessThan(
                amendmentService.IndexOf("ApplyProposal(purchaseOrder, proposal, now)", StringComparison.Ordinal),
                "the immutable effective-exposure adjustment must exist before the PO re-enters Approved");
        amendmentService.Should().Contain("ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment")
            .And.Contain("releasedFormalExposure")
            .And.Contain("ProcurementBudgetCommitmentLedgerEntryType.Release")
            .And.Contain("formalExposure - releasedFormalExposure + priorDirectAdjustments")
            .And.Contain("priorDirectAdjustments")
            .And.Contain("commitment.FormallyCommittedAmount")
            .And.Contain("PO_AMENDMENT_CONTRACT_ALLOCATION_LEDGER_REQUIRED")
            .And.Contain("isContractChild && delta != 0m")
            .And.Contain("PO_AMENDMENT_EXPOSURE_REALLOCATION_REQUIRED")
            .And.Contain("exposureLineageChanged");

        var sourceService = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services",
            "Procurement", "ProcurementPurchaseOrderSourceService.cs"));
        sourceService.Should().Contain("public async Task AuthorizeDraftCancellationAsync(")
            .And.Contain("purchaseOrder.CreatedById != _currentUser.UserId")
            .And.Contain("PermissionCode = Permission")
            .And.Contain("decision is null || !decision.Allowed")
            .And.Contain("releasedFormalExposure")
            .And.Contain("committedExposure - releasedFormalExposure +")
            .And.Contain("item.FormalCommitmentEntryId != null");

        var contractService = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services",
            "Procurement", "ContractService.cs"));
        contractService.Should().Contain("CONTRACT_VALUE_AMENDMENT_BUDGET_LEDGER_REQUIRED")
            .And.Contain("amendment.AmendmentType")
            .And.Contain("contract.Status")
            .And.Contain("ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment")
            .And.Contain("case \"TimelineExtension\"")
            .And.Contain("case \"ScopeChange\"");

        var contractActivation = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services",
            "Procurement", "ProcurementContractActivationService.cs"));
        var lifecycle = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services",
            "Procurement", "ProcurementBudgetCommitmentLifecycleService.cs"));
        contractActivation.Should().Contain("GetDirectPurchaseOrderAdjustmentExposureAsync(")
            .And.Contain("directPurchaseOrderIds.Contains(item.PurchaseOrderId)")
            .And.Contain("item.SourceType == \"PurchaseOrder\"")
            .And.Contain("GetNetCommittedExposureAsync(")
            .And.Contain("formalCommitments - releasedFormalCommitments + directPurchaseOrderAdjustments")
            .And.Contain("item.FormalCommitmentEntryId != null");
        lifecycle.Should().Contain("directPurchaseOrderAdjustments")
            .And.Contain("releasedFormalCommitments")
            .And.Contain("alreadyCommitted - releasedFormalCommitments +")
            .And.Contain("effectiveAllocation")
            .And.Contain("releasedFormalCapacity")
            .And.Contain("formal.Amount - releasedFormalCapacity +")
            .And.Contain("allocation is null ? purchaseOrderAdjustment : 0m")
            .And.Contain("EnsureContractAllocationsFullyUtilizedAsync(")
            .And.Contain("CONTRACT_CHILD_PO_ALLOCATION_OUTSTANDING")
            .And.Contain("SetFormalCommitmentContextAsync(")
            .And.Contain("SetUtilizationContextAsync(")
            .And.Contain("Persist immutable proof first inside the existing transaction");

        var reservationControl = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services",
            "Procurement", "ProcurementRequisitionBudgetControlService.cs"));
        var reservationStore = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Repositories",
            "Procurement", "ProcurementBudgetReservationStore.cs"));
        reservationControl.Should().Contain("SetDownstreamReservationContextAsync(")
            .And.Contain("SetRequisitionReservationReleaseContextAsync(")
            .And.Contain("commitment.ReservedAmount >= requestedAmount")
            .And.Contain("commitment.ReservedAmount < expectedReservedAmount")
            .And.Contain("ProcurementBudgetCommitmentStatus.Consumed or")
            .And.Contain("ProcurementBudgetCommitmentStatus.Released")
            .And.Contain("new or formally amended requisition");
        reservationStore.Should().Contain("SetDownstreamReservationContextAsync(")
            .And.Contain("SetRequisitionReservationReleaseContextAsync(")
            .And.Contain("SetFormalCommitmentContextAsync(")
            .And.Contain("SetUtilizationContextAsync(")
            .And.Contain("PROCUREMENT_DOWNSTREAM_RESERVATION_AMOUNT_BEFORE")
            .And.Contain("PROCUREMENT_DOWNSTREAM_RESERVATION_CORRELATION_ID")
            .And.Contain("PROCUREMENT_REQUISITION_RELEASE_AMOUNT_BEFORE")
            .And.Contain("PROCUREMENT_REQUISITION_RELEASE_CORRELATION_ID")
            .And.Contain("PROCUREMENT_FORMAL_LEDGER_ENTRY_ID")
            .And.Contain("PROCUREMENT_FORMAL_CORRELATION_ID")
            .And.Contain("PROCUREMENT_UTILIZATION_LEDGER_ENTRY_ID")
            .And.Contain("PROCUREMENT_UTILIZATION_CORRELATION_ID");

        var vps = File.ReadAllText(Path.Combine(root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));
        vps.Should().Contain($"MigrationId = N'{CorrectiveMigrationId}'")
            .And.Contain($"GUARD_COVERAGE|{CorrectiveMigrationId}")
            .And.Contain("OBJECT_ID(N'dbo.ProcurementPurchaseOrderCommitmentAdjustments', N'U') IS NULL")
            .And.Contain("Atomic PO budget commitment trigger prerequisites");
    }

    private static string FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
