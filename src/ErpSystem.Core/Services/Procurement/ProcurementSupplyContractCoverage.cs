using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

/// <summary>
/// A Supply contract for an already approved, exact award PO documents the same
/// purchase. Its commitment remains owned by that PO and is utilized by receipts,
/// not by creating a second contract exposure or by rewriting approved PO lineage.
/// </summary>
internal static class ProcurementSupplyContractCoverage
{
    internal sealed record Coverage(PurchaseOrder PurchaseOrder,
        ProcurementBudgetCommitmentLedgerEntry FormalEntry);

    internal static async Task<Coverage?> ResolveAsync(IUnitOfWork unitOfWork,
        Guid tenantId, Contract contract, Guid requisitionId, CancellationToken cancellationToken)
    {
        if (contract.TenantId != tenantId)
            throw Invalid("The contract does not belong to the current tenant.");
        if (!string.Equals(contract.ContractType, "Supply", StringComparison.OrdinalIgnoreCase) ||
            contract.TenderAwardId == Guid.Empty)
            return null;

        var orders = await unitOfWork.Repository<PurchaseOrder>().GetQueryable(item =>
                item.TenantId == tenantId && !item.IsDeleted &&
                item.TenderAwardId == contract.TenderAwardId && item.Status != "Cancelled")
            .AsNoTracking().ToListAsync(cancellationToken);
        if (orders.Count == 0) return null;
        if (orders.Count != 1)
            throw Invalid("Multiple live purchase orders reference this Supply award; reconcile their coverage before activation.");

        var order = orders[0];
        // A not-yet-approved child PO can still follow the established
        // contract-first allocation route; it has no independent exposure.
        if (order.ContractId == contract.Id && order.Status == "Draft" &&
            !await unitOfWork.Repository<ProcurementBudgetCommitmentLedgerEntry>().GetQueryable(item =>
                item.TenantId == tenantId && item.SourceType == "PurchaseOrder" &&
                item.SourceId == order.Id && !item.IsDeleted).AnyAsync(cancellationToken))
            return null;
        if (order.Status != "Approved" || !order.ApprovedAt.HasValue ||
            !order.ApprovedById.HasValue || order.ApprovedById == Guid.Empty ||
            order.SourceRequisitionId != requisitionId ||
            order.BusinessPartnerId != contract.BusinessPartnerId ||
            order.ProcurementSourceType != ProcurementPurchaseOrderSourceType.TenderAward ||
            order.ProcurementSourceId != contract.TenderAwardId ||
            order.ProcurementCategory != ProcurementCategoryClass.Goods ||
            !order.BudgetId.HasValue || string.IsNullOrWhiteSpace(order.SourceIntegrityHash) ||
            (order.ContractId.HasValue && order.ContractId != contract.Id) ||
            !SameCurrency(order.Currency, contract.Currency) ||
            order.TotalAmount != contract.ContractValue || contract.ContractValue <= 0m)
            throw Invalid("The existing award PO must be approved, unreceived, and match this Supply contract's supplier, requisition, amount and currency.");

        var awardMatches = await unitOfWork.Repository<TenderAward>().GetQueryable(item =>
                item.Id == contract.TenderAwardId && item.TenantId == tenantId && !item.IsDeleted &&
                item.TenderId == contract.TenderId && item.BusinessPartnerId == contract.BusinessPartnerId &&
                item.AwardedAmount == contract.ContractValue && item.Status != "Cancelled")
            .AnyAsync(cancellationToken);
        if (!awardMatches)
            throw Invalid("The Supply contract and existing PO no longer identify the same approved award.");

        var entries = unitOfWork.Repository<ProcurementBudgetCommitmentLedgerEntry>();
        var formal = await entries.GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted &&
                item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment &&
                item.SourceType == "PurchaseOrder" && item.SourceId == order.Id)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (formal is null || formal.PurchaseRequisitionId != requisitionId ||
            formal.ProcurementBudgetId != order.BudgetId || !SameCurrency(formal.Currency, contract.Currency))
            throw Invalid("The exact approved PO has no matching formal budget commitment.");

        // Keep this recovery narrow: amended/released/utilized exposures need their
        // governed reconciliation path, not an inferred second commitment.
        if (formal.Amount != contract.ContractValue ||
            await unitOfWork.Repository<ProcurementPurchaseOrderCommitmentAdjustment>().GetQueryable(item =>
                item.TenantId == tenantId && item.PurchaseOrderId == order.Id && !item.IsDeleted)
                .AnyAsync(cancellationToken) ||
            await entries.GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted &&
                item.FormalCommitmentEntryId == formal.Id &&
                (item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.Utilization ||
                 item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.Release ||
                 item.EntryType == ProcurementBudgetCommitmentLedgerEntryType.PurchaseOrderAllocation))
                .AnyAsync(cancellationToken) ||
            await unitOfWork.Repository<PurchaseOrderReceipt>().GetQueryable(item =>
                item.TenantId == tenantId && item.PurchaseOrderId == order.Id && !item.IsDeleted)
                .AnyAsync(cancellationToken))
            throw Invalid("The existing PO exposure has adjustments, receipts, releases or allocations; reconcile them before attaching Supply contract coverage.");

        var commitmentMatches = await unitOfWork.Repository<ProcurementBudgetCommitment>().GetQueryable(item =>
                item.Id == formal.ProcurementBudgetCommitmentId && item.TenantId == tenantId &&
                !item.IsDeleted && item.PurchaseRequisitionId == requisitionId &&
                item.ProcurementBudgetId == formal.ProcurementBudgetId &&
                item.FormallyCommittedAmount >= formal.Amount)
            .AnyAsync(cancellationToken);
        if (!commitmentMatches)
            throw Invalid("The PO commitment does not match its requisition budget envelope.");
        return new Coverage(order, formal);
    }

    private static bool SameCurrency(string left, string right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    private static ProcurementBudgetCommitmentLifecycleException Invalid(string message) =>
        new("CONTRACT_PO_COMMITMENT_COVERAGE_INVALID", message);
}
