using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

public sealed partial class ProcurementAcceptedSupplyService
{
    public async Task<IReadOnlyList<ProcurementAcceptedReceiptLineDto>> GetGoodsReceiptLinesAsync(
        Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        var order = await RequiredOrderAsync(purchaseOrderId, cancellationToken);
        if (RequiredCategory(order) != ProcurementCategoryClass.Goods)
            throw Invalid("ACCEPTED_SUPPLY_KIND_MISMATCH", "Receipt invoicing requires a Goods purchase order.");
        return await ReadGoodsReceiptLinesAsync(order, cancellationToken);
    }

    private async Task<IReadOnlyList<ProcurementAcceptedReceiptLineDto>> ReadGoodsReceiptLinesAsync(
        PurchaseOrder order, CancellationToken token)
    {
        var tenant = _currentUser.TenantId;
        if (order.Status == "Cancelled" || order.CancelledAtUtc.HasValue)
            throw Invalid("ACCEPTED_SUPPLY_PO_CANCELLED", "A cancelled purchase order cannot authorize a new invoice.");
        var receipts = await _unitOfWork.Repository<PurchaseOrderReceipt>().GetQueryable(r =>
                r.TenantId == tenant && !r.IsDeleted && r.PurchaseOrderId == order.Id && r.Status != "Cancelled")
            .IgnoreQueryFilters().AsNoTracking().ToListAsync(token);
        if (receipts.Count == 0)
            throw Invalid("ACCEPTED_GOODS_RECEIPT_MISSING", "No governed goods receipt exists for this purchase order.");
        var receiptIds = receipts.Select(r => r.Id).ToArray();
        var cases = await _unitOfWork.Repository<ProcurementReceiptInspectionCase>().GetQueryable(c =>
                c.TenantId == tenant && !c.IsDeleted && receiptIds.Contains(c.PurchaseOrderReceiptId))
            .IgnoreQueryFilters().Include(c => c.Lines).ThenInclude(l => l.PurchaseOrderReceiptItem)
            .AsNoTracking().ToListAsync(token);
        // A pending receipt contributes zero. It must not block independently approved receipts.
        // The current case supersedes older cases; re-inspection is never counted twice.
        var eligible = cases.GroupBy(c => c.PurchaseOrderReceiptId)
            .Select(g => g.OrderByDescending(c => c.Sequence).First())
            .Where(c => ProcurementReceiptInspectionRules.IsApEligible(c.Status, c.PendingQuantity, c.ApEligibleQuantity)).ToList();
        var grns = await _unitOfWork.Repository<GoodsReceiptNote>().GetQueryable(g =>
                g.TenantId == tenant && !g.IsDeleted && g.PurchaseOrderId == order.Id &&
                g.PurchaseOrderReceiptId.HasValue && receiptIds.Contains(g.PurchaseOrderReceiptId.Value))
            .IgnoreQueryFilters().Include(g => g.Items).AsNoTracking().ToListAsync(token);
        var grnIds = grns.Select(g => g.Id).ToArray();
        var returns = await _unitOfWork.Repository<PurchaseReturnItem>().GetQueryable(l =>
                l.TenantId == tenant && !l.IsDeleted && l.StockReversed && !l.PurchaseReturn.IsDeleted &&
                l.PurchaseReturn.TenantId == tenant && l.PurchaseReturn.GoodsReceiptNoteId.HasValue &&
                grnIds.Contains(l.PurchaseReturn.GoodsReceiptNoteId.Value))
            .Include(l => l.PurchaseReturn).AsNoTracking().ToListAsync(token);
        if (returns.Any(l => !l.GoodsReceiptNoteItemId.HasValue || l.ReturnQuantity < 0))
            throw Invalid("ACCEPTED_GOODS_RETURN_LINEAGE", "A dispatched supplier return has no exact receipt line. Reconcile its source before invoicing.");
        var output = new List<ProcurementAcceptedReceiptLineDto>();
        foreach (var inspection in eligible)
        {
            var receipt = receipts.Single(r => r.Id == inspection.PurchaseOrderReceiptId);
            var receiptGrns = grns.Where(g => g.PurchaseOrderReceiptId == receipt.Id).ToList();
            if (receiptGrns.Count > 1)
                throw Invalid("ACCEPTED_GOODS_GRN_AMBIGUOUS", "Multiple inventory GRNs refer to one procurement receipt. Reconcile the duplicate source.");
            var grn = receiptGrns.SingleOrDefault();
            if (grn?.Status == GRNStatus.Cancelled) continue;
            foreach (var line in inspection.Lines.Where(l => !l.IsDeleted && l.AcceptedQuantity > 0))
            {
                var source = line.PurchaseOrderReceiptItem;
                var poLine = order.Items.SingleOrDefault(p => p.Id == source?.PurchaseOrderItemId && !p.IsDeleted && p.TenantId == tenant);
                if (line.TenantId != tenant || source == null || source.IsDeleted || source.TenantId != tenant ||
                    source.ReceiptId != receipt.Id || poLine == null || line.AcceptedQuantity > source.ReceivedQuantity)
                    throw Invalid("ACCEPTED_GOODS_LINEAGE", "Approved inspection quantities do not match their original receipt and PO lines.");
                var matchingGrnLines = grn?.Items.Where(l => !l.IsDeleted && l.TenantId == tenant &&
                    l.PurchaseOrderItemId == poLine.Id).ToList() ?? [];
                if (matchingGrnLines.Count > 1 || inspection.Lines.Count(l => !l.IsDeleted &&
                        l.PurchaseOrderReceiptItem.PurchaseOrderItemId == poLine.Id) > 1)
                    throw Invalid("ACCEPTED_GOODS_LINEAGE_AMBIGUOUS", "The GRN to inspection line mapping is ambiguous. Reconcile its source before invoicing.");
                var grnLine = matchingGrnLines.SingleOrDefault();
                var returned = grnLine == null ? 0m : returns.Where(l => l.GoodsReceiptNoteItemId == grnLine.Id).Sum(l => l.ReturnQuantity);
                if (returned > 0)
                {
                    // Inventory returns are in base units. Use the captured receipt ratio,
                    // never a conversion that an item-master edit may have changed afterward.
                    if (grnLine!.ReceivedQuantity <= 0 || source.ReceivedQuantity <= 0)
                        throw Invalid("ACCEPTED_GOODS_RETURN_UOM", "The returned receipt's original unit conversion is unavailable.");
                    returned = returned * source.ReceivedQuantity / grnLine.ReceivedQuantity;
                }
                output.Add(new ProcurementAcceptedReceiptLineDto
                {
                    PurchaseOrderId = order.Id, PurchaseOrderItemId = poLine.Id,
                    PurchaseOrderReceiptId = receipt.Id, PurchaseOrderReceiptItemId = source.Id,
                    InspectionCaseId = inspection.Id, GoodsReceiptNoteId = grn?.Id,
                    GoodsReceiptNoteItemId = grnLine?.Id, ReceiptNumber = grn?.GRNNumber ?? receipt.ReceiptNumber,
                    ReceiptDate = receipt.ReceiptDate, AcceptedQuantity = line.AcceptedQuantity,
                    ReturnedQuantity = returned, UnitPrice = poLine.UnitPrice, InspectionIntegrityHash = inspection.IntegrityHash,
                    AcceptedAtUtc = inspection.DecidedAtUtc ?? inspection.UpdatedAt ?? inspection.CreatedAt
                });
            }
        }
        return output.OrderBy(l => l.ReceiptDate).ThenBy(l => l.PurchaseOrderReceiptId)
            .ThenBy(l => l.PurchaseOrderReceiptItemId).ToList();
    }
}
