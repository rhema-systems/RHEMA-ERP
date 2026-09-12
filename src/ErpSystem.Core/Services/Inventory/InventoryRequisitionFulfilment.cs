using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

/// <summary>
/// IssuedQuantity remains the net outstanding stock balance used by valuation and returns.
/// Fulfilment is net issued plus effective posted returns; returning stock is not a new approval to issue it.
/// </summary>
public static class InventoryRequisitionFulfilment
{
    public static async Task<IReadOnlyDictionary<Guid, decimal>> LoadReturnsAsync(
        IUnitOfWork unitOfWork, Guid tenantId, IEnumerable<Guid> requisitionIds,
        CancellationToken cancellationToken = default)
    {
        var ids = requisitionIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, decimal>();
        return await unitOfWork.Repository<InventoryReturnVoucherLine>().GetQueryable()
            .AsNoTracking()
            .Where(line => line.TenantId == tenantId && !line.IsDeleted &&
                line.InventoryReturnVoucher.TenantId == tenantId && !line.InventoryReturnVoucher.IsDeleted &&
                line.InventoryReturnVoucher.Status == InventoryReturnVoucherStatus.Posted &&
                ids.Contains(line.InventoryReturnVoucher.InventoryRequisitionId))
            .GroupBy(line => line.InventoryRequisitionItemId)
            .Select(group => new { Id = group.Key, Quantity = group.Sum(line => line.Quantity) })
            .ToDictionaryAsync(row => row.Id, row => row.Quantity, cancellationToken);
    }

    public static decimal Returned(InventoryRequisitionItem item, IReadOnlyDictionary<Guid, decimal> returns) =>
        returns.TryGetValue(item.Id, out var quantity) ? quantity : 0m;

    public static decimal GrossIssued(InventoryRequisitionItem item, IReadOnlyDictionary<Guid, decimal> returns) =>
        item.IssuedQuantity + Returned(item, returns);

    public static decimal Remaining(InventoryRequisitionItem item, IReadOnlyDictionary<Guid, decimal> returns) =>
        Math.Max(0m, item.ApprovedQuantity - GrossIssued(item, returns));

    public static RequisitionStatus Status(InventoryRequisition requisition, IReadOnlyDictionary<Guid, decimal> returns)
    {
        // Never reopen completed/cancelled requests or override approval workflow states.
        if (requisition.Status is not (RequisitionStatus.Approved or RequisitionStatus.InProgress or
            RequisitionStatus.PartiallyIssued or RequisitionStatus.Issued)) return requisition.Status;
        var items = requisition.Items.Where(item => !item.IsDeleted).ToArray();
        if (!items.Any(item => GrossIssued(item, returns) > 0)) return requisition.Status;
        return items.All(item => GrossIssued(item, returns) >= item.ApprovedQuantity)
            ? RequisitionStatus.Issued : RequisitionStatus.PartiallyIssued;
    }
}
