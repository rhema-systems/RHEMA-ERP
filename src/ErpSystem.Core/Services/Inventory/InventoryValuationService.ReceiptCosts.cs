using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

public partial class InventoryValuationService : IInventoryReceiptCostAdjustmentService
{
    public async Task ApplyReceiptCostAdjustmentAsync(VendorInvoiceReceiptCostAllocation cost,
        IReadOnlyList<ReceiptCostAdjustmentTarget> targets, CancellationToken ct = default)
        => await ApplyReceiptCostTargetsAsync(cost, targets, false, cost.PostingEventId, cost.JournalEntryId, ct);

    public async Task ReverseReceiptCostAdjustmentAsync(VendorInvoiceReceiptCostAllocation cost,
        IReadOnlyList<ReceiptCostAdjustmentTarget> targets, Guid reversalPostingEventId, Guid reversalJournalEntryId, CancellationToken ct = default)
        => await ApplyReceiptCostTargetsAsync(cost, targets, true, reversalPostingEventId, reversalJournalEntryId, ct);

    private async Task ApplyReceiptCostTargetsAsync(VendorInvoiceReceiptCostAllocation cost,
        IReadOnlyList<ReceiptCostAdjustmentTarget> targets, bool reverse, Guid eventId, Guid journalId, CancellationToken ct)
    {
        var tenantId = _currentUserProvider.TenantId;
        if (!_unitOfWork.HasActiveTransaction || cost.TenantId != tenantId || cost.PostingEventId == Guid.Empty ||
            cost.JournalEntryId == Guid.Empty || cost.ReversalJournalEntryId.HasValue || cost.IsDeleted ||
            (!reverse && !_unitOfWork.Repository<VendorInvoiceReceiptCostAllocation>().GetAddedEntities().Any(x => ReferenceEquals(x, cost))))
            throw new InvalidOperationException("Only the current invoice posting transaction may apply its newly retained receipt cost allocation.");
        if ((!reverse && targets.Sum(x => x.ValueChange) != cost.InventoryAdjustmentAmount) ||
            (reverse && (Math.Abs(targets.Sum(x => x.ValueChange)) > Math.Abs(cost.InventoryAdjustmentAmount) ||
                targets.Any(x => Math.Sign(x.ValueChange) == Math.Sign(cost.InventoryAdjustmentAmount)))) || targets.Any(x => x.ValueChange == 0 ||
            x.ValueChange != decimal.Round(x.ValueChange, 2) || x.AttributedQuantity <= 0) ||
            targets.Select(x => (x.WarehouseId, x.LocationId, x.LayerId)).Distinct().Count() != targets.Count)
            throw new InvalidOperationException("Receipt cost valuation targets do not conserve their authorized amount.");
        var action = reverse ? "Reverse" : "Post";
        var posting = await _unitOfWork.Repository<FinancePostingEvent>().GetQueryable(x => x.Id == eventId && x.TenantId == tenantId &&
            !x.IsDeleted && x.SourceDocumentType == "VendorInvoice" && x.SourceDocumentId == cost.VendorInvoiceId &&
            x.PostingAction == action && x.PostingStatus == "Posted" && x.JournalEntryId == journalId).SingleOrDefaultAsync(ct);
        if (posting is null) throw new InvalidOperationException("Receipt cost valuation requires the original successful invoice Finance posting.");
        if (reverse && !await _unitOfWork.Repository<JournalEntry>().GetQueryable(x => x.TenantId == tenantId && x.Id == cost.JournalEntryId &&
            x.IsReversed && x.ReversalJournalEntryId == journalId && !x.IsDeleted).AnyAsync(ct))
            throw new InvalidOperationException("Receipt cost reversal requires the exact original invoice journal reversal.");
        var item = await _unitOfWork.Repository<InventoryItem>().GetQueryable(x => x.TenantId == tenantId && x.Id == cost.InventoryItemId && !x.IsDeleted).SingleAsync(ct);
        if (item.ValuationMethod is not (ValuationMethod.FIFO or ValuationMethod.WeightedAverage))
            throw new InvalidOperationException("Invoice cost differences cannot change standard inventory cost.");
        foreach (var target in targets)
        {
            var location = await _unitOfWork.Repository<WarehouseLocation>().GetQueryable(x => x.TenantId == tenantId && x.Id == target.LocationId &&
                x.InventoryWarehouseId == target.WarehouseId && !x.IsDeleted).AsNoTracking().SingleOrDefaultAsync(ct);
            if (location is null) throw new InvalidOperationException("The retained receipt cost target does not belong to this company's warehouse.");
            // This value-only owner may revalue still-owned transit stock. It never receives,
            // picks, releases allocation quantities, or grants ordinary users transit access.
            var balance = await GetOrCreateBalanceAsync(item.Id, target.WarehouseId, target.LocationId);
            if (balance.QuantityOnHand < target.AttributedQuantity || balance.QuantityOnHand <= 0 || balance.TotalValue + target.ValueChange < 0)
                throw new InvalidOperationException("Receipt cost adjustment exceeds retained stock or would make inventory value negative.");
            if (item.ValuationMethod == ValuationMethod.FIFO)
            {
                if (!target.LayerId.HasValue) throw new InvalidOperationException("FIFO receipt cost adjustment requires exact retained layer lineage.");
                var layer = await _unitOfWork.Repository<InventoryLayer>().GetQueryable(x => x.Id == target.LayerId && x.TenantId == tenantId &&
                    x.InventoryItemId == item.Id && x.WarehouseId == target.WarehouseId && x.LocationId == target.LocationId && !x.IsDeleted).SingleAsync(ct);
                if (layer.RemainingQuantity < target.AttributedQuantity || layer.RemainingQuantity <= 0 || layer.RemainingValue + target.ValueChange < 0)
                    throw new InvalidOperationException("Receipt cost adjustment exceeds the current FIFO layer.");
                layer.RemainingValue += target.ValueChange;
                layer.UnitCost = layer.RemainingValue / layer.RemainingQuantity;
                await _unitOfWork.Repository<InventoryLayer>().UpdateAsync(layer);
            }
            else if (target.LayerId.HasValue) throw new InvalidOperationException("Weighted-average cost targets cannot carry a FIFO layer.");
            balance.TotalValue += target.ValueChange;
            balance.AverageUnitCost = balance.TotalValue / balance.QuantityOnHand;
            balance.LastRecalculatedAt = DateTime.UtcNow;
            var movement = await CreateMovementAsync(item.Id, target.WarehouseId, target.LocationId,
                InventoryMovementType.InvoiceCostAdjustment, target.ValueChange > 0 ? MovementDirection.In : MovementDirection.Out,
                0, 0, ReferenceType.VendorInvoice, cost.VendorInvoiceId.ToString(), cost.VendorInvoiceId);
            movement.TotalValue = target.ValueChange;
            movement.CostLayerId = target.LayerId;
            movement.RunningBalance = balance.QuantityOnHand;
            movement.RunningValue = balance.TotalValue;
            movement.PostingDate = posting.PostingDate;
            movement.IsReversal = reverse;
            movement.Notes = $"Receipt invoice cost allocation {cost.Id:N}; {cost.SourceFingerprint}";
            await _unitOfWork.Repository<VendorInvoiceReceiptCostValuation>().AddAsync(new() {
                TenantId = tenantId, CreatedById = _currentUserProvider.UserId, CostAllocationId = cost.Id,
                WarehouseId = target.WarehouseId, LocationId = target.LocationId, InventoryLayerId = target.LayerId,
                InventoryMovementId = movement.Id, AttributedReceiptBaseQuantity = target.AttributedQuantity, ValueChange = target.ValueChange,
                IsReversal = reverse
            });
        }
        // The existing central inventory projection updates item, warehouse and bin averages on SaveChanges.
    }
}
