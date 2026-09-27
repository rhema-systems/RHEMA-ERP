using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Services.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

public sealed partial class SupplierDebitNoteService
{
    internal sealed record CapturedReturnAccounting(IReadOnlyList<ReturnStageLine> Plan,
        List<InventorySupplierReturnAccountingGroup> Groups, List<InventorySupplierReturnAllocation> Allocations,
        List<InventorySupplierReturnAccrualShare> AccrualShares);

    private async Task<CapturedReturnAccounting> CaptureReturnAccountingAsync(PurchaseReturn source, CancellationToken ct)
    {
        var plan = await PlanReturnAccountingStageAsync(source, ct);
        if (plan.Select(x => x.Receipt.AccountingBookId).Distinct().Count() != 1 ||
            plan.Select(x => x.Receipt.FunctionalCurrency).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1)
            throw new InvalidOperationException("RTV_RECEIPT_BOOK_MISMATCH: receipt allocations must retain one accounting book and functional currency.");
        var carrying = await RequireDispatchCarryingByLineAsync(source, ct);
        var captured = new CapturedReturnAccounting(plan, [], [], []);
        var groups = new Dictionary<Guid, InventorySupplierReturnAccountingGroup>();
        var receiptRemainders = new Dictionary<Guid, (decimal Quantity, decimal ForeignAmount, Dictionary<Guid, decimal> Accounts)>();
        foreach (var entry in plan)
        {
            var receipt = entry.Receipt;
            var grnLineId = receipt.GoodsReceiptNoteItemId;
            if (!receiptRemainders.ContainsKey(grnLineId))
            {
                var costs = await _db.Set<VendorInvoiceReceiptCostAllocation>().AsNoTracking().Where(x =>
                    x.TenantId == TenantId && !x.IsDeleted && x.GoodsReceiptNoteItemId == grnLineId && !x.ReversalJournalEntryId.HasValue).ToListAsync(ct);
                var postedAllocationIds = await _db.Set<VendorInvoiceReceiptAllocation>().AsNoTracking().Where(x =>
                    x.TenantId == TenantId && !x.IsDeleted && x.GoodsReceiptNoteItemId == grnLineId &&
                    !x.VendorInvoice.IsDeleted && x.VendorInvoice.Status != VendorInvoiceStatus.Voided && x.VendorInvoice.JournalEntryId.HasValue)
                    .Select(x => x.Id).ToListAsync(ct);
                if (costs.Count != postedAllocationIds.Count || costs.Any(x => !postedAllocationIds.Contains(x.VendorInvoiceReceiptAllocationId)))
                    throw new InvalidOperationException("RTV_ACCRUAL_RECONCILIATION_REQUIRED: posted invoice receipt shares require their original recorded GRNI clearing amounts.");
                var costIds = costs.Select(x => x.Id).ToArray();
                var invoiceShares = await _db.Set<VendorInvoiceReceiptCostPostingLine>().AsNoTracking().Where(x =>
                    x.TenantId == TenantId && !x.IsDeleted && costIds.Contains(x.CostAllocationId) && x.Purpose == "Accrual").ToListAsync(ct);
                var prior = await _db.Set<InventorySupplierReturnAllocation>().AsNoTracking().Where(x =>
                    x.TenantId == TenantId && !x.IsDeleted && x.GoodsReceiptNoteItemId == grnLineId &&
                    x.InventoryPurchaseReturnId != source.Id && !x.OriginalVendorInvoiceId.HasValue).ToListAsync(ct);
                var priorIds = prior.Select(x => x.Id).ToArray();
                var priorShares = await _db.Set<InventorySupplierReturnAccrualShare>().AsNoTracking().Where(x =>
                    x.TenantId == TenantId && !x.IsDeleted && priorIds.Contains(x.InventorySupplierReturnAllocationId)).ToListAsync(ct);
                var originalIds = receipt.AccrualShares.Select(x => x.AccountTransactionId).ToHashSet();
                if (invoiceShares.Any(x => !x.OriginalReceiptAccountTransactionId.HasValue || !originalIds.Contains(x.OriginalReceiptAccountTransactionId.Value) || x.FunctionalAmount < 0m) ||
                    priorShares.Any(x => !originalIds.Contains(x.OriginalReceiptAccountTransactionId) || x.Amount < 0m) ||
                    invoiceShares.Sum(x => x.FunctionalAmount) != costs.Sum(x => x.ReceiptFunctionalAmount))
                    throw new InvalidOperationException("RTV_ACCRUAL_RECONCILIATION_REQUIRED: previous clearing does not reconcile to original receipt account transactions.");
                var remaining = receipt.AccrualShares.ToDictionary(x => x.AccountTransactionId, x => x.Amount -
                    invoiceShares.Where(y => y.OriginalReceiptAccountTransactionId == x.AccountTransactionId).Sum(y => y.FunctionalAmount) -
                    priorShares.Where(y => y.OriginalReceiptAccountTransactionId == x.AccountTransactionId).Sum(y => y.Amount));
                var quantity = receipt.AcceptedBaseQuantity - costs.Sum(x => x.BaseQuantity) - prior.Sum(x => x.BaseQuantity);
                var foreignAmount = receipt.PurchaseAmount - costs.Sum(x => x.ReceiptForeignAmount) - prior.Sum(x => x.OriginalAccrualForeignAmount);
                if (quantity < 0m || foreignAmount < 0m || remaining.Values.Any(x => x < 0m) || quantity == 0m && (remaining.Values.Sum() != 0m || foreignAmount != 0m))
                    throw new InvalidOperationException("RTV_ACCRUAL_OVERCLAIMED: retained receipt clearing quantities and amounts exceed the original source.");
                receiptRemainders.Add(grnLineId, (quantity, foreignAmount, remaining));
            }
            var lineCarrying = MonetaryAllocation.Allocate(entry.Slices.Select(x => x.BaseQuantity).ToArray(), carrying[entry.ReturnLine.Id]);
            for (var index = 0; index < entry.Slices.Count; index++)
            {
                var slice = entry.Slices[index];
                var groupKey = slice.InvoiceId ?? Guid.Empty;
                if (!groups.TryGetValue(groupKey, out var group))
                {
                    group = new InventorySupplierReturnAccountingGroup { Id = Guid.NewGuid(), TenantId = TenantId,
                        InventoryPurchaseReturnId = source.Id, OriginalVendorInvoiceId = slice.InvoiceId,
                        FunctionalCurrency = receipt.FunctionalCurrency, CapturedAtUtc = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = CurrentUserId };
                    groups.Add(groupKey, group);
                    captured.Groups.Add(group);
                }
                var allocation = new InventorySupplierReturnAllocation { Id = Guid.NewGuid(), TenantId = TenantId,
                    InventoryPurchaseReturnId = source.Id, InventoryPurchaseReturnItemId = entry.ReturnLine.Id,
                    GoodsReceiptNoteItemId = grnLineId, PurchaseOrderReceiptItemId = receipt.PurchaseOrderReceiptItemId,
                    ProcurementReceiptCostBasisId = receipt.Basis?.Id, AccountingGroupId = group.Id,
                    VendorInvoiceReceiptAllocationId = slice.ReceiptAllocationId, OriginalVendorInvoiceId = slice.InvoiceId,
                    OriginalVendorInvoiceLineItemId = slice.InvoiceLineId, OriginalReceiptJournalEntryId = receipt.ReceiptJournalEntryId,
                    BaseQuantity = slice.BaseQuantity, PurchaseQuantity = slice.PurchaseQuantity, ConversionToBase = receipt.PurchaseToBase,
                    CarryingAmount = lineCarrying[index], FunctionalCurrency = receipt.FunctionalCurrency, PurchaseCurrency = receipt.PurchaseCurrency,
                    CapturedAtUtc = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = CurrentUserId };
                if (!slice.InvoiceId.HasValue)
                {
                    var remainder = receiptRemainders[grnLineId];
                    if (slice.BaseQuantity > remainder.Quantity)
                        throw new InvalidOperationException("RTV_ACCRUAL_CAPACITY: uninvoiced return exceeds uncleared receipt quantity.");
                    var accounts = receipt.AccrualShares.OrderBy(x => x.AccountTransactionId).ToArray();
                    var total = slice.BaseQuantity == remainder.Quantity ? remainder.Accounts.Values.Sum() :
                        Math.Min(Round(receipt.FunctionalAccrualAmount * slice.BaseQuantity / receipt.AcceptedBaseQuantity), remainder.Accounts.Values.Sum());
                    var amounts = MonetaryAllocation.Allocate(accounts.Select(x => remainder.Accounts[x.AccountTransactionId]).ToArray(), total);
                    for (var accountIndex = 0; accountIndex < accounts.Length; accountIndex++)
                    {
                        var account = accounts[accountIndex];
                        if (amounts[accountIndex] > remainder.Accounts[account.AccountTransactionId])
                            throw new InvalidOperationException("RTV_ACCRUAL_ACCOUNT_CAPACITY: allocation exceeds an original account's uncleared amount.");
                        remainder.Accounts[account.AccountTransactionId] -= amounts[accountIndex];
                        captured.AccrualShares.Add(new InventorySupplierReturnAccrualShare { Id = Guid.NewGuid(), TenantId = TenantId,
                            InventorySupplierReturnAllocationId = allocation.Id, OriginalReceiptAccountTransactionId = account.AccountTransactionId,
                            AccountId = account.AccountId, Amount = amounts[accountIndex], CreatedAt = DateTime.UtcNow,
                            CreatedBy = UserName, CreatedById = CurrentUserId });
                    }
                    allocation.OriginalAccrualAmount = total;
                    allocation.OriginalAccrualForeignAmount = slice.BaseQuantity == remainder.Quantity ? remainder.ForeignAmount :
                        Math.Min(Round(receipt.PurchaseAmount * slice.BaseQuantity / receipt.AcceptedBaseQuantity), remainder.ForeignAmount);
                    receiptRemainders[grnLineId] = (remainder.Quantity - slice.BaseQuantity,
                        remainder.ForeignAmount - allocation.OriginalAccrualForeignAmount, remainder.Accounts);
                }
                captured.Allocations.Add(allocation);
                group.CarryingAmount += allocation.CarryingAmount;
                group.OriginalAccrualAmount += allocation.OriginalAccrualAmount;
            }
        }
        return captured;
    }

    internal sealed record ReturnStageLine(PurchaseReturnItem ReturnLine, ProcurementReceiptCostEvidence Receipt,
        IReadOnlyList<SupplierReturnQuantityAllocation.Slice> Slices);

    private async Task<IReadOnlyList<ReturnStageLine>> PlanReturnAccountingStageAsync(PurchaseReturn source, CancellationToken ct)
    {
        if (!_unitOfWork.HasActiveTransaction)
            throw new InvalidOperationException("RTV_ALLOCATION_TRANSACTION_REQUIRED: receipt stage must be captured in the physical dispatch transaction.");
        if (_acceptedSupply == null)
            throw new InvalidOperationException("RTV_RECEIPT_AUTHORITY_REQUIRED: accepted receipt accounting resolution is not configured.");
        if (!source.PurchaseOrderId.HasValue || !source.GoodsReceiptNoteId.HasValue)
            throw new InvalidOperationException("RTV_RECEIPT_SOURCE_REQUIRED: exact original PO and accepted GRN are required.");

        // Match the invoice posting owner: PO first, then accepted receipt. The
        // physical return owner takes these before issuing any stock as well.
        await _unitOfWork.AcquireTransactionLockAsync($"tdc-ap-match:{TenantId:N}:{source.PurchaseOrderId.Value:N}", ct);
        await _unitOfWork.AcquireTransactionLockAsync($"supplier-return-source:{TenantId:N}:{source.GoodsReceiptNoteId.Value:N}", ct);
        var reader = new ProcurementReceiptCostBasisReader(_unitOfWork, TenantId, _acceptedSupply);
        var result = new List<ReturnStageLine>();
        foreach (var line in source.Items.OrderBy(x => x.Id))
        {
            if (!line.GoodsReceiptNoteItemId.HasValue || line.GoodsReceiptNoteItem == null)
                throw new InvalidOperationException("RTV_RECEIPT_LINE_REQUIRED: every return line must retain its accepted receipt line.");
            var receipt = await reader.ResolveAsync(line.GoodsReceiptNoteItemId.Value, ct);
            var staged = result.Where(x => x.ReturnLine.GoodsReceiptNoteItemId == line.GoodsReceiptNoteItemId)
                .SelectMany(x => x.Slices).ToArray();
            var allocations = await _db.Set<VendorInvoiceReceiptAllocation>().AsNoTracking()
                .Include(x => x.VendorInvoice).Include(x => x.VendorInvoiceLineItem)
                .Where(x => x.TenantId == TenantId && !x.IsDeleted && x.GoodsReceiptNoteItemId == line.GoodsReceiptNoteItemId &&
                    x.VendorInvoice.TenantId == TenantId && !x.VendorInvoice.IsDeleted && x.VendorInvoice.Status != VendorInvoiceStatus.Voided &&
                    x.VendorInvoiceLineItem.TenantId == TenantId && !x.VendorInvoiceLineItem.IsDeleted).ToListAsync(ct);

            // A PO header or current invoice price cannot identify which historical
            // receipt was invoiced. Do not silently invent the missing allocation.
            var poLineId = line.GoodsReceiptNoteItem.PurchaseOrderItemId;
            if (await _db.Set<VendorInvoiceLineItem>().AsNoTracking().AnyAsync(x =>
                x.TenantId == TenantId && !x.IsDeleted && x.PurchaseOrderItemId == poLineId &&
                !x.VendorInvoice.IsDeleted && x.VendorInvoice.TenantId == TenantId && x.VendorInvoice.Status != VendorInvoiceStatus.Voided &&
                !_db.Set<VendorInvoiceReceiptAllocation>().Any(a => a.TenantId == TenantId && !a.IsDeleted && a.VendorInvoiceLineItemId == x.Id), ct))
                throw new InvalidOperationException("RTV_RECEIPT_RECONCILIATION_REQUIRED: an existing invoice has no exact receipt allocation. Reconcile its original receipt before returning this quantity.");

            var prior = await _db.Set<InventorySupplierReturnAllocation>().AsNoTracking().Where(x =>
                x.TenantId == TenantId && !x.IsDeleted && x.GoodsReceiptNoteItemId == line.GoodsReceiptNoteItemId &&
                x.InventoryPurchaseReturnId != source.Id).ToListAsync(ct);
            if (await _db.Set<PurchaseReturnItem>().AsNoTracking().AnyAsync(x =>
                x.TenantId == TenantId && !x.IsDeleted && x.GoodsReceiptNoteItemId == line.GoodsReceiptNoteItemId &&
                x.PurchaseReturnId != source.Id && x.StockReversed && !x.PurchaseReturn.IsDeleted &&
                !_db.Set<InventorySupplierReturnAllocation>().Any(a => a.TenantId == TenantId && !a.IsDeleted && a.InventoryPurchaseReturnItemId == x.Id), ct))
                throw new InvalidOperationException("RTV_PRIOR_RETURN_RECONCILIATION_REQUIRED: a historical dispatched return has no retained invoice-stage allocation.");

            var posted = new List<SupplierReturnQuantityAllocation.PostedShare>();
            var reserved = 0m;
            foreach (var allocation in allocations)
            {
                if (allocation.Quantity <= 0m || allocation.VendorInvoice.BusinessPartnerId != source.SupplierId ||
                    allocation.VendorInvoiceLineItem.InventoryItemId != line.InventoryItemId)
                    throw new InvalidOperationException("RTV_INVOICE_SOURCE_INVALID: the invoice allocation does not match the returned supplier and item.");
                var invoice = allocation.VendorInvoice;
                if (!invoice.JournalEntryId.HasValue)
                {
                    reserved += allocation.Quantity * receipt.PurchaseToBase;
                    continue;
                }
                var journal = await _db.JournalEntries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == invoice.JournalEntryId && x.TenantId == TenantId, ct);
                if (!HasPostedOriginalInvoiceEvidence(invoice, journal))
                    throw new InvalidOperationException("RTV_INVOICE_POSTING_RECONCILIATION_REQUIRED: invoice allocation has missing or reversed posting evidence.");
                if (journal!.AccountingBookId != receipt.AccountingBookId)
                    throw new InvalidOperationException("RTV_ORIGINAL_BOOK_MISMATCH: the invoice and original receipt must share the retained accounting book before their return can be posted.");
                posted.Add(new SupplierReturnQuantityAllocation.PostedShare(allocation.Id, invoice.Id, allocation.VendorInvoiceLineItemId,
                    journal!.PostingDate ?? invoice.InvoiceDate, allocation.Quantity * receipt.PurchaseToBase, allocation.Quantity,
                    prior.Where(x => x.VendorInvoiceReceiptAllocationId == allocation.Id).Sum(x => x.BaseQuantity) +
                    staged.Where(x => x.ReceiptAllocationId == allocation.Id).Sum(x => x.BaseQuantity)));
            }
            var knownAllocationIds = posted.Select(x => x.ReceiptAllocationId).ToHashSet();
            if (prior.Any(x => x.VendorInvoiceReceiptAllocationId.HasValue && !knownAllocationIds.Contains(x.VendorInvoiceReceiptAllocationId.Value)))
                throw new InvalidOperationException("RTV_PRIOR_INVOICE_RECONCILIATION_REQUIRED: a retained return claim no longer has its original posted invoice allocation.");
            result.Add(new ReturnStageLine(line, receipt, SupplierReturnQuantityAllocation.Plan(receipt.AcceptedBaseQuantity,
                receipt.PurchaseToBase, line.ReturnQuantity,
                prior.Where(x => !x.OriginalVendorInvoiceId.HasValue).Sum(x => x.BaseQuantity) +
                staged.Where(x => !x.InvoiceId.HasValue).Sum(x => x.BaseQuantity), reserved, posted)));
        }
        return result;
    }
}
