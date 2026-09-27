using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

public sealed partial class SupplierDebitNoteService
{
    private async Task PostAllocatedReturnDispatchAsync(PurchaseReturn source, CapturedReturnAccounting captured, CancellationToken ct)
    {
        var date = source.ShippedDate!.Value.Date;
        var settings = await _db.FinanceSettings.AsNoTracking().SingleAsync(x => x.TenantId == TenantId && !x.IsDeleted, ct);
        var currency = captured.Plan[0].Receipt.FunctionalCurrency;
        var bookId = captured.Plan[0].Receipt.AccountingBookId;
        var book = await _db.AccountingBooks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == bookId && x.TenantId == TenantId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("RTV_ORIGINAL_BOOK_REQUIRED: the original receipt accounting book is unavailable.");
        var producer = FinanceExternalProducerContractCatalog.GetRequired(FinanceExternalProducerContractId.ProcurementSupplierReturnDispatch);
        var lines = BuildAllocatedReturnDispatchLines(source, captured, settings.ReturnToVendorClearingAccountId, settings.PurchaseReturnVarianceAccountId);
        foreach (var line in lines)
            await RequirePostingAccountAsync(line.AccountId, date, line.Description ?? "Supplier return account",
                line.TransactionTag != "RTV-Uninvoiced-Variance", ct);
        if (_sourceDimensions == null) throw new InvalidOperationException("Finance dimensions are not configured for return dispatch.");
        var contexts = lines.Select(x => new FinanceSourceDocumentLineContext(x.SourceDocumentLineId!.Value, x.AccountId)).ToArray();
        await _sourceDimensions.SynchronizeDraftAsync(producer, source.Id, date, contexts, null, false, null,
            "Supplier return receipt and invoice allocation capture", ct);
        await _sourceDimensions.ValidateAndFreezeAsync(producer, source.Id, date, contexts, false, ct);
        foreach (var line in lines)
            line.Dimensions = await _sourceDimensions.ResolvePostingDimensionsAsync(producer, source.Id, line.SourceDocumentLineId!.Value, line.AccountId, date, ct);
        var posted = await _posting.PostAsync(new FinancePostingRequestV2Dto
        {
            SourceModule = "Procurement", OriginModuleCode = FinanceModuleLockCatalog.Procurement,
            SourceDocumentType = "SupplierReturnDispatch", SourceDocumentId = source.Id, SourceDocumentTenantId = TenantId,
            SourceDocumentReference = source.ReturnNumber, PostingAction = "Dispatch", PostingDate = date,
            JournalType = "Supplier Return Dispatch", AccountingBookCode = book.Code, FunctionalCurrencyCode = currency,
            Description = $"Allocated supplier return {source.ReturnNumber}",
            IdempotencyKey = $"Inventory:SupplierReturn:{TenantId:N}:{source.Id:N}:Dispatch", ReturnExistingOnDuplicate = true, Lines = lines
        }, producer, ct);
        foreach (var group in captured.Groups)
        {
            group.DispatchPostingEventId = posted.PostingEventId;
            group.DispatchJournalEntryId = posted.JournalEntryId;
        }
        _db.AddRange(captured.Groups);
        _db.AddRange(captured.Allocations);
        _db.AddRange(captured.AccrualShares);
        await _db.SaveChangesAsync(ct);
        source.AccountingAllocationVersion = 1;
        await _db.SaveChangesAsync(ct);
    }

    internal static List<FinancePostingLineDto> BuildAllocatedReturnDispatchLines(PurchaseReturn source, CapturedReturnAccounting captured,
        Guid? clearingAccountId, Guid? varianceAccountId)
    {
        var date = source.ShippedDate!.Value.Date;
        var currency = captured.Plan[0].Receipt.FunctionalCurrency;
        var lines = new List<FinancePostingLineDto>();

        Guid Identity(string key) => FinanceExternalDimensionIdentity.SourceLine(FinanceExternalProducerContractId.ProcurementSupplierReturnDispatch, source.Id, key);

        foreach (var group in captured.Groups)
        {
            if (group.OriginalVendorInvoiceId.HasValue)
            {
                var clearing = clearingAccountId
                    ?? throw new InvalidOperationException("RTV_CLEARING_ACCOUNT_REQUIRED: configure return-to-vendor clearing before dispatching invoiced quantities.");
                group.ClearingAccountId = clearing;
                if (group.CarryingAmount != 0m)
                    lines.Add(PostingLine(clearing, $"Invoiced return clearing {source.ReturnNumber}", group.CarryingAmount, 0,
                        group.CarryingAmount, currency, currency, 1, date, source.ReturnNumber, lines.Count + 1,
                        "RTV-Dispatch-Clearing", Identity($"invoice-clearing:{group.OriginalVendorInvoiceId:N}")));
            }
        }
        foreach (var account in captured.AccrualShares.GroupBy(x => x.AccountId).OrderBy(x => x.Key))
        {
            var amount = account.Sum(x => x.Amount);
            if (amount == 0m) continue;
            lines.Add(PostingLine(account.Key, $"Uninvoiced return accrual {source.ReturnNumber}", amount, 0, amount,
                currency, currency, 1, date, source.ReturnNumber, lines.Count + 1, "RTV-Uninvoiced-Accrual", Identity($"receipt-accrual:{account.Key:N}")));
        }
        var inventoryAmounts = new Dictionary<Guid, decimal>();
        foreach (var entry in captured.Plan)
        {
            var carrying = captured.Allocations.Where(x => x.InventoryPurchaseReturnItemId == entry.ReturnLine.Id).Sum(x => x.CarryingAmount);
            var inventoryShares = entry.Receipt.InventoryShares.OrderBy(x => x.AccountTransactionId).ToArray();
            if (carrying != 0m && (inventoryShares.Length == 0 || inventoryShares.Sum(x => x.Amount) <= 0m))
                throw new InvalidOperationException("RTV_ORIGINAL_INVENTORY_ACCOUNT_REQUIRED: original receipt Inventory accounts must support the dispatched carrying value.");
            var shares = MonetaryAllocation.Allocate(inventoryShares.Select(x => x.Amount).ToArray(), carrying);
            for (var i = 0; i < inventoryShares.Length; i++)
                inventoryAmounts[inventoryShares[i].AccountId] = inventoryAmounts.GetValueOrDefault(inventoryShares[i].AccountId) + shares[i];
        }
        foreach (var account in inventoryAmounts.OrderBy(x => x.Key))
        {
            if (account.Value == 0m) continue;
            if (captured.Groups.Any(x => x.ClearingAccountId == account.Key) || captured.AccrualShares.Any(x => x.AccountId == account.Key))
                throw new InvalidOperationException("RTV_ACCOUNTS_MUST_DIFFER: Inventory cannot also serve as return clearing or receipt accrual.");
            lines.Add(PostingLine(account.Key, $"Dispatched Inventory {source.ReturnNumber}", 0, account.Value, account.Value,
                currency, currency, 1, date, source.ReturnNumber, lines.Count + 1, "RTV-Dispatch-Inventory", Identity($"inventory-control:{account.Key:N}")));
        }
        var uninvoiced = captured.Groups.SingleOrDefault(x => !x.OriginalVendorInvoiceId.HasValue);
        var difference = uninvoiced == null ? 0m : Round(uninvoiced.CarryingAmount - uninvoiced.OriginalAccrualAmount);
        if (difference != 0m)
        {
            var variance = varianceAccountId
                ?? throw new InvalidOperationException("RTV_VARIANCE_ACCOUNT_REQUIRED: configure purchase-return variance for the difference between original accrual and dispatched cost.");
            if (inventoryAmounts.ContainsKey(variance) || captured.AccrualShares.Any(x => x.AccountId == variance) || captured.Groups.Any(x => x.ClearingAccountId == variance))
                throw new InvalidOperationException("RTV_ACCOUNTS_MUST_DIFFER: return variance cannot use Inventory, receipt accrual or clearing.");
            lines.Add(PostingLine(variance, $"Uninvoiced return cost variance {source.ReturnNumber}", Math.Max(difference, 0m), Math.Max(-difference, 0m),
                Math.Abs(difference), currency, currency, 1, date, source.ReturnNumber, lines.Count + 1,
                "RTV-Uninvoiced-Variance", Identity("uninvoiced-cost-variance")));
        }
        if (lines.Count == 0 || Round(lines.Sum(x => x.DebitAmount - x.CreditAmount)) != 0m)
            throw new InvalidOperationException("RTV_DISPATCH_UNBALANCED: allocated return dispatch requires balanced posting evidence.");
        return lines;
    }
}
