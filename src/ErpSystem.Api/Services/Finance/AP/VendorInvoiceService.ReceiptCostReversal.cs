using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

public partial class VendorInvoiceService
{
    private async Task ReverseReceiptCostsAsync(VendorInvoice invoice, FinancePostingResultDto reversal, CancellationToken ct)
    {
        var costs = await _unitOfWork.Repository<VendorInvoiceReceiptCostAllocation>().GetQueryable(x => x.TenantId == TenantId &&
            x.VendorInvoiceId == invoice.Id && !x.IsDeleted).OrderBy(x => x.Id).ToListAsync(ct);
        if (costs.Count == 0) return;
        var reader = new ProcurementReceiptCostBasisReader(_unitOfWork, TenantId, RequireAcceptedSupplyServiceAsync());
        var journal = await _unitOfWork.Repository<JournalEntry>().GetQueryable(x => x.TenantId == TenantId && x.Id == invoice.JournalEntryId && !x.IsDeleted)
            .AsNoTracking().SingleAsync(ct);
        var originalLines = await _unitOfWork.Repository<AccountTransaction>().GetQueryable(x => x.TenantId == TenantId &&
            x.JournalEntryId == journal.Id && !x.IsDeleted && x.TransactionTag == "AP-INVENTORY-COST").AsNoTracking().ToListAsync(ct);
        var reclassification = new List<FinancePostingLineDto>();
        var reversing = new List<VendorInvoiceReceiptCostAllocation>();
        _inventoryValuationService?.ResetProcessingAttempt();
        foreach (var cost in costs)
        {
            if (cost.ReversalJournalEntryId.HasValue)
            {
                if (cost.ReversalJournalEntryId != reversal.JournalEntryId || cost.ReversalPostingEventId != reversal.PostingEventId)
                    throw new InvalidOperationException("Receipt cost allocation is linked to another invoice reversal.");
                continue;
            }
            reversing.Add(cost);
            if (cost.InventoryAdjustmentAmount == 0) continue;
            var basis = await reader.ResolveAsync(cost.GoodsReceiptNoteItemId, ct);
            var item = await _unitOfWork.Repository<InventoryItem>().GetQueryable(x => x.TenantId == TenantId && x.Id == cost.InventoryItemId && !x.IsDeleted)
                .AsNoTracking().SingleAsync(ct);
            var stock = await ResolveReceiptStockAsync(basis, item, ct);
            var applied = await _unitOfWork.Repository<VendorInvoiceReceiptCostValuation>().GetQueryable(x => x.TenantId == TenantId &&
                x.CostAllocationId == cost.Id && !x.IsReversal && !x.IsDeleted).AsNoTracking().ToListAsync(ct);
            var originalRetained = cost.RevaluedReceiptBaseQuantity;
            var currentRetained = stock.Sum(x => x.Quantity);
            if (originalRetained <= 0 || applied.Sum(x => x.ValueChange) != cost.InventoryAdjustmentAmount || currentRetained > originalRetained)
                throw new InvalidOperationException("Receipt cost reversal cannot reconcile the originally revalued and currently retained stock.");
            var retainedValue = RoundMoney(cost.InventoryAdjustmentAmount * currentRetained / originalRetained);
            var consumedValue = cost.InventoryAdjustmentAmount - retainedValue;
            var reverseAmounts = SupplierInvoiceCostDifferenceCalculator.AllocateSigned(stock.Select(x => x.Quantity).ToArray(), -retainedValue);
            var targets = stock.Select((x, i) => new ReceiptCostAdjustmentTarget(x.WarehouseId,
                x.LocationId ?? throw new InvalidOperationException("Receipt reversal requires exact bin lineage."), x.LayerId, x.Quantity, reverseAmounts[i]))
                .Where(x => x.ValueChange != 0).ToArray();
            var owner = _inventoryValuationService as IInventoryReceiptCostAdjustmentService
                ?? throw new InvalidOperationException("The receipt cost reversal valuation owner is not configured.");
            await owner.ReverseReceiptCostAdjustmentAsync(cost, targets, reversal.PostingEventId, reversal.JournalEntryId, ct);
            if (consumedValue == 0) continue;
            var ppv = cost.PurchasePriceVarianceAccountId
                ?? throw new InvalidOperationException("The original invoice needs its retained item price-variance account to reverse cost already consumed.");
            var costLines = await _unitOfWork.Repository<VendorInvoiceReceiptCostPostingLine>().GetQueryable(x => x.TenantId == TenantId &&
                x.CostAllocationId == cost.Id && x.Purpose == "Inventory" && !x.IsDeleted).AsNoTracking().OrderBy(x => x.AccountId).ToListAsync(ct);
            var amounts = SupplierInvoiceCostDifferenceCalculator.AllocateSigned(costLines.Select(x => Math.Abs(x.FunctionalAmount)).ToArray(), consumedValue);
            for (var i = 0; i < costLines.Count; i++)
            {
                if (amounts[i] == 0) continue;
                var original = originalLines.Where(x => x.SourceDocumentLineId == cost.VendorInvoiceLineItemId && x.AccountId == costLines[i].AccountId).ToArray();
                if (original.Length == 0 || original.Select(x => x.FinanceDimensionSetId).Distinct().Count() != 1)
                    throw new InvalidOperationException("The original invoice inventory dimensions are ambiguous for cost reversal.");
                var dimensions = original[0].FinanceDimensionSetId.HasValue
                    ? await _unitOfWork.Repository<FinanceDimensionSetItem>().GetQueryable(x => x.TenantId == TenantId &&
                        x.FinanceDimensionSetId == original[0].FinanceDimensionSetId && !x.IsDeleted).AsNoTracking()
                        .Select(x => new FinancePostingDimensionValueDto { DimensionCode = x.DimensionCodeSnapshot,
                            ValueCode = x.DimensionValueCodeSnapshot }).ToListAsync(ct)
                    : new List<FinancePostingDimensionValueDto>();
                // The exact AP reversal remains untouched. This separate linked, balanced
                // reclassification leaves only still-owned value in Inventory; consumed cost
                // is corrected through the retained PPV account in the same transaction.
                foreach (var side in new[] { (Account: costLines[i].AccountId, Amount: amounts[i]), (Account: ppv, Amount: -amounts[i]) })
                {
                    var line = BuildPostingLine(side.Account, $"Consumed receipt cost reversal - {invoice.InvoiceNumber}",
                        Math.Max(0, side.Amount), Math.Max(0, -side.Amount), cost.FunctionalCurrency, cost.FunctionalCurrency, 1m,
                        reversal.PostingDate, invoice.InvoiceNumber, reclassification.Count + 1, "AP-RECEIPT-COST-REVERSAL");
                    line.SourceDocumentLineId = cost.VendorInvoiceLineItemId;
                    // A reclassification is a new journal. Finance must resolve and validate
                    // the retained dimension codes; stored set IDs are reserved for exact reversals.
                    line.Dimensions = dimensions;
                    line.Notes = $"Original cost allocation {cost.Id:N}; exact reversal {reversal.JournalEntryId:N}";
                    reclassification.Add(line);
                }
            }
        }
        FinancePostingResultDto? corrected = null;
        if (reclassification.Count > 0)
            corrected = await _financePostingEngine!.PostAsync(new FinancePostingRequestV2Dto {
                SourceModule = "AP", SourceDocumentType = "VendorInvoiceReceiptCostReclassification", SourceDocumentId = invoice.Id,
                SourceDocumentTenantId = TenantId, SourceDocumentReference = invoice.InvoiceNumber, PostingAction = "Post",
                Description = $"Reclassify consumed receipt cost on invoice reversal {invoice.InvoiceNumber}", JournalType = "AP Receipt Cost Reversal",
                PostingDate = reversal.PostingDate, AccountingBookCode = journal.BookClassification, FunctionalCurrencyCode = costs.Select(x => x.FunctionalCurrency).Distinct().Single(),
                IdempotencyKey = $"AP:InvoiceReceiptCostReverse:{TenantId:N}:{invoice.Id:N}", ReturnExistingOnDuplicate = true, Lines = reclassification
            }, ct);
        foreach (var cost in reversing)
        {
            cost.ReversalPostingEventId = reversal.PostingEventId; cost.ReversalJournalEntryId = reversal.JournalEntryId;
            cost.ReversalReclassificationPostingEventId = corrected?.PostingEventId;
            cost.ReversalReclassificationJournalEntryId = corrected?.JournalEntryId;
            await _unitOfWork.Repository<VendorInvoiceReceiptCostAllocation>().UpdateAsync(cost);
        }
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
