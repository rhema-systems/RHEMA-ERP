using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

public partial class VendorInvoiceService
{
    private static bool RequiresProcurementMatch(VendorInvoice invoice) => invoice.AutoInvoiceRequestId.HasValue ||
        ProcurementInvoiceThreeWayMatchRules.IsRequired(invoice.PurchaseOrderId, invoice.IsOpeningBalance);

    private async Task<List<Guid>> ReceiptInvoiceOrderIdsAsync(VendorInvoice invoice, CancellationToken token)
    {
        if (!invoice.AutoInvoiceRequestId.HasValue)
            return invoice.PurchaseOrderId.HasValue ? [invoice.PurchaseOrderId.Value] : [];
        var itemIds = invoice.LineItems.Where(l => !l.IsDeleted && l.PurchaseOrderItemId.HasValue).Select(l => l.PurchaseOrderItemId!.Value).ToArray();
        var orderIds = await _unitOfWork.Repository<PurchaseOrderItem>().GetQueryable(l => l.TenantId == TenantId && !l.IsDeleted && itemIds.Contains(l.Id))
            .Select(l => l.PurchaseOrderId).Distinct().ToListAsync(token);
        // SQL Server and .NET order GUIDs differently. Use the same application
        // ordering as Auto Invoice creation for every multi-source lock sequence.
        return orderIds.OrderBy(id => id).ToList();
    }

    private async Task AcquireReceiptInvoiceLocksAsync(VendorInvoice invoice, CancellationToken token)
    {
        if (!RequiresProcurementMatch(invoice)) return;
        if (!_unitOfWork.HasActiveTransaction) throw new InvalidOperationException("Receipt invoice validation requires the shared transaction.");
        var orderIds = await ReceiptInvoiceOrderIdsAsync(invoice, token);
        foreach (var id in orderIds) await _unitOfWork.AcquireTransactionLockAsync($"tdc-ap-match:{TenantId:N}:{id:N}", token);
        var grnIds = await _unitOfWork.Repository<GoodsReceiptNote>().GetQueryable(r => r.TenantId == TenantId && !r.IsDeleted &&
            r.PurchaseOrderId.HasValue && orderIds.Contains(r.PurchaseOrderId.Value)).Select(r => r.Id).ToListAsync(token);
        foreach (var id in grnIds.OrderBy(id => id)) await _unitOfWork.AcquireTransactionLockAsync($"supplier-return-source:{TenantId:N}:{id:N}", token);
    }

    private static void ValidateAutoInvoiceUpdate(VendorInvoice invoice, VendorInvoiceUpdateDto request)
    {
        if (!invoice.AutoInvoiceRequestId.HasValue) return;
        var old = invoice.LineItems.Where(l => !l.IsDeleted).ToDictionary(l => l.Id);
        if (request.IsOpeningBalance || request.PurchaseOrderId.HasValue || request.CurrencyCode != invoice.CurrencyCode ||
            request.LineItems.Count != old.Count || request.LineItems.Any(l => !l.Id.HasValue || !old.TryGetValue(l.Id.Value, out var source) ||
                l.PurchaseOrderItemId != source.PurchaseOrderItemId || l.Quantity != source.Quantity || l.BudgetEntryId.HasValue ||
                l.FixedAssetId.HasValue || l.LineItemType != source.LineItemType))
            throw new InvalidOperationException("Auto Invoice receipt quantities, source lines and currency are fixed. Delete the unposted draft and select receipts again to change them.");
        request.MatchingType = InvoiceMatchingType.ThreeWay;
    }

    private async Task<InvoiceMatchingResultDto> EvaluateAutoInvoiceMatchAsync(VendorInvoice invoice, string action,
        bool requireApprovalReady, bool persist, CancellationToken token)
    {
        var result = new InvoiceMatchingResultDto { VendorInvoiceId = invoice.Id, InvoiceTotal = invoice.TotalAmount,
            IsRequired = true, MatchingType = InvoiceMatchingType.ThreeWay, DecisionKeys = ProcurementInvoiceThreeWayMatchRules.DecisionKeys.ToList() };
        var errors = new List<(string Code, string Message)>();
        var parts = new List<object>();
        var allocations = await _unitOfWork.Repository<VendorInvoiceReceiptAllocation>().GetQueryable(a =>
            a.TenantId == TenantId && a.VendorInvoiceId == invoice.Id && !a.IsDeleted).AsNoTracking().ToListAsync(token);
        var active = invoice.LineItems.Where(l => !l.IsDeleted).ToList();
        if (invoice.PurchaseOrderId.HasValue || invoice.IsOpeningBalance || invoice.AcceptedSupplyKind != ProcurementAcceptedSupplyKind.GoodsReceiptConsolidation ||
            allocations.Count == 0 || allocations.Count != active.Count || active.Any(l => !allocations.Any(a =>
                a.VendorInvoiceLineItemId == l.Id && a.Quantity == l.Quantity && a.PurchaseOrderItemId == l.PurchaseOrderItemId)))
            errors.Add(("AP_RECEIPT_ALLOCATION_MISSING", "The invoice does not have complete receipt-line authority."));
        foreach (var orderId in allocations.Select(a => a.PurchaseOrderId).Distinct().OrderBy(id => id))
        {
            var matched = await EvaluateThreeWayMatchAsync(invoice.Id, action, false, token, persist: false, evaluatedOrderId: orderId);
            parts.Add(new { orderId, matched.SnapshotHash });
            result.Checks.AddRange(matched.Checks);
            result.Discrepancies.AddRange(matched.Discrepancies);
            result.PriceTolerancePercentage = matched.PriceTolerancePercentage;
            result.QuantityTolerancePercentage = 0m;
            if (!matched.IsMatched || !matched.ApprovalReady)
                errors.Add(("AP_RECEIPT_PO_MATCH_FAILED", matched.Message));
            try
            {
                var available = await AvailableReceiptLinesAsync(orderId, invoice.Id, token);
                foreach (var group in allocations.Where(a => a.PurchaseOrderId == orderId).GroupBy(a => a.PurchaseOrderReceiptItemId))
                {
                    var source = available.SingleOrDefault(s => s.Source.PurchaseOrderReceiptItemId == group.Key);
                    if (source == null || group.Sum(a => a.Quantity) > source.Available || group.Any(a =>
                        a.InspectionCaseId != source.Source.InspectionCaseId || a.GoodsReceiptNoteId != source.Source.GoodsReceiptNoteId ||
                        a.GoodsReceiptNoteItemId != source.Source.GoodsReceiptNoteItemId || a.PurchaseOrderItemId != source.Source.PurchaseOrderItemId))
                        errors.Add(("AP_RECEIPT_QUANTITY_UNAVAILABLE", "A selected receipt has changed or no longer has sufficient accepted, uninvoiced and unreturned quantity."));
                }
                parts.Add(new { orderId, available });
            }
            catch (ProcurementAcceptedSupplyValidationException exception) { errors.Add((exception.Code, exception.Message)); }
        }
        var profile = _procurementConfiguration == null ? null : await ResolveProcurementProfileForMatchingAsync(token);
        return await FinalizeThreeWayEvaluationAsync(invoice, null, profile, action, result, errors, parts,
            requireApprovalReady, persist, token);
    }
}
