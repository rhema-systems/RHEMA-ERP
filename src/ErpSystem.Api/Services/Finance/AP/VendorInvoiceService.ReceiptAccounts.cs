using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

public partial class VendorInvoiceService
{
    private sealed record ReceiptAccrualShare(Guid AccountId, decimal Weight);
    private static bool HasReceiptAccrualAmount(VendorInvoiceLineItem line) =>
        !line.IsDeleted && RoundMoney(RoundMoney(line.Quantity * line.UnitPrice) - line.DiscountAmount) > 0m;

    private async Task<Dictionary<Guid, List<ReceiptAccrualShare>>> ResolveProcurementAccrualAccountsAsync(
        VendorInvoice invoice, CancellationToken ct)
    {
        if (invoice.TenantId != TenantId)
            throw new InvalidOperationException("The invoice belongs to another tenant.");
        // Viewing/replaying a posted invoice must retain its own original accounts,
        // not recompute them from receipts or invoices entered afterward.
        if (invoice.JournalEntryId.HasValue)
        {
            var ownLines = await LoadApAccrualClearingsAsync(new[] { invoice }, includeReversed: true, ct);
            if (ownLines.Count > 0)
                return invoice.LineItems.Where(HasReceiptAccrualAmount).ToDictionary(line => line.Id, line =>
                {
                    var attributed = ownLines.Where(value => value.SourceDocumentLineId == line.Id).ToList();
                    if (attributed.Count == 0 && ownLines.Select(value => value.AccountId).Distinct().Count() == 1) attributed = ownLines;
                    if (attributed.Count == 0) throw new InvalidOperationException("The posted invoice has ambiguous original accrual line accounts.");
                    return attributed.GroupBy(value => value.AccountId)
                        .Select(group => new ReceiptAccrualShare(group.Key, group.Sum(value => value.DebitAmount - value.CreditAmount)))
                        .Where(value => value.Weight > 0).OrderBy(value => value.AccountId).ToList();
                });
            if (invoice.LineItems.Any(HasReceiptAccrualAmount))
                throw new InvalidOperationException("The posted invoice's original accrual journal is unavailable. Historical accounts cannot be recalculated from current receipts.");
            return new();
        }
        if (invoice.AutoInvoiceRequestId.HasValue)
        {
            var ids = invoice.LineItems.Where(l => !l.IsDeleted && l.PurchaseOrderItemId.HasValue).Select(l => l.PurchaseOrderItemId!.Value).ToArray();
            var items = await _unitOfWork.Repository<PurchaseOrderItem>().GetQueryable(p => p.TenantId == TenantId && !p.IsDeleted && ids.Contains(p.Id))
                .AsNoTracking().ToListAsync(ct);
            var combined = new Dictionary<Guid, List<ReceiptAccrualShare>>();
            foreach (var order in items.GroupBy(p => p.PurchaseOrderId))
            {
                var orderItems = order.Select(p => p.Id).ToHashSet();
                var scoped = new VendorInvoice { Id = invoice.Id, TenantId = invoice.TenantId, PurchaseOrderId = order.Key,
                    LineItems = invoice.LineItems.Where(l => l.PurchaseOrderItemId.HasValue && orderItems.Contains(l.PurchaseOrderItemId.Value)).ToList() };
                foreach (var entry in await ResolveProcurementAccrualAccountsAsync(scoped, ct)) combined.Add(entry.Key, entry.Value);
            }
            if (invoice.LineItems.Where(HasReceiptAccrualAmount).Any(l => !combined.ContainsKey(l.Id)))
                throw new InvalidOperationException("A consolidated invoice line has no original PO accrual authority.");
            return combined;
        }
        var receiptIds = await _unitOfWork.Repository<PurchaseOrderReceipt>().GetQueryable(value =>
            value.TenantId == TenantId && value.PurchaseOrderId == invoice.PurchaseOrderId && !value.IsDeleted)
            .Select(value => value.Id).ToListAsync(ct);
        var events = await _unitOfWork.Repository<FinancePostingEvent>().GetQueryable(value =>
            value.TenantId == TenantId && !value.IsDeleted && value.SourceDocumentType == "ProcurementPurchaseOrderReceipt" &&
            receiptIds.Contains(value.SourceDocumentId) && value.PostingAction == "PostAcceptedInventoryReceipt" &&
            value.PostingStatus == "Posted" && value.JournalEntryId.HasValue)
            .AsNoTracking().ToListAsync(ct);
        var eventJournalIds = events.Select(value => value.JournalEntryId!.Value).Distinct().ToArray();
        var journals = await _unitOfWork.Repository<JournalEntry>().GetQueryable(value =>
            value.TenantId == TenantId && !value.IsDeleted && eventJournalIds.Contains(value.Id) &&
            value.PostingStatus == "Posted" && !value.IsReversed && !value.ReversalJournalEntryId.HasValue)
            .Select(value => value.Id).ToListAsync(ct);
        var credits = await _unitOfWork.Repository<AccountTransaction>().GetQueryable(value => value.TenantId == TenantId &&
            !value.IsDeleted && !value.IsReversed && journals.Contains(value.JournalEntryId) && value.TransactionTag == "INV-RECEIPT-GRV-ACCRUAL")
            .AsNoTracking().ToListAsync(ct);
        var poItems = await _unitOfWork.Repository<PurchaseOrderItem>().GetQueryable(value => value.TenantId == TenantId &&
            value.PurchaseOrderId == invoice.PurchaseOrderId && !value.IsDeleted)
            .ToDictionaryAsync(value => value.Id, ct);
        Guid? ItemId(VendorInvoiceLineItem line) => line.PurchaseOrderItemId.HasValue
            ? poItems.GetValueOrDefault(line.PurchaseOrderItemId.Value)?.InventoryItemId : line.InventoryItemId;

        var balances = new Dictionary<(Guid Item, Guid Account), decimal>();
        void Add(Guid item, Guid account, decimal amount) => balances[(item, account)] = balances.GetValueOrDefault((item, account)) + amount;
        var legacyUnattributed = false;
        var movements = await _unitOfWork.Repository<InventoryMovement>().GetQueryable(value =>
            value.TenantId == TenantId && !value.IsDeleted && value.IsPosted && receiptIds.Contains(value.ReferenceId ?? Guid.Empty) &&
            value.MovementType == InventoryMovementType.PurchaseReceipt).AsNoTracking().ToListAsync(ct);
        foreach (var journal in credits.GroupBy(value => value.JournalEntryId))
        {
            foreach (var credit in journal.Where(value => value.SourceDocumentLineId.HasValue))
                Add(credit.SourceDocumentLineId!.Value, credit.AccountId, credit.CreditAmount - credit.DebitAmount);
            var legacy = journal.Where(value => !value.SourceDocumentLineId.HasValue).ToList();
            if (legacy.Count == 0) continue;
            if (legacy.Select(value => value.AccountId).Distinct().Count() != 1)
                throw new InvalidOperationException("Legacy receipt credits use multiple accounts without item lineage. Reconcile the original receipt allocation before invoicing.");
            var sources = events.Where(value => value.JournalEntryId == journal.Key).Select(value => value.SourceDocumentId).Distinct().ToArray();
            if (sources.Length != 1)
                throw new InvalidOperationException("The receipt journal has ambiguous source-document lineage.");
            var itemValues = movements.Where(value => value.ReferenceId == sources[0]).GroupBy(value => value.InventoryItemId)
                .Select(group => new { Item = group.Key, Value = group.Sum(value => value.TotalValue + (value.VarianceAmount ?? 0m)) -
                    journal.Where(value => value.SourceDocumentLineId == group.Key).Sum(value => value.CreditAmount - value.DebitAmount) })
                .Where(value => value.Value > 0m).ToArray();
            if (itemValues.Length == 0) { legacyUnattributed = true; continue; }
            var total = itemValues.Sum(value => value.Value);
            var legacyCredit = legacy.Sum(value => value.CreditAmount - value.DebitAmount);
            foreach (var item in itemValues) Add(item.Item, legacy[0].AccountId, legacyCredit * item.Value / total);
        }

        var previous = await _unitOfWork.Repository<VendorInvoice>().GetQueryable(value => value.TenantId == TenantId &&
            !value.IsDeleted && value.Id != invoice.Id && (value.PurchaseOrderId == invoice.PurchaseOrderId ||
                (value.AutoInvoiceRequestId.HasValue && value.LineItems.Any(l => l.PurchaseOrderItem != null && l.PurchaseOrderItem.PurchaseOrderId == invoice.PurchaseOrderId))) &&
            value.Status != VendorInvoiceStatus.Voided && value.JournalEntryId.HasValue)
            .Include(value => value.LineItems).AsNoTracking().ToListAsync(ct);
        var clearings = await LoadApAccrualClearingsAsync(previous, includeReversed: false, ct);
        var previousLines = previous.SelectMany(value => value.LineItems.Where(line => line.TenantId == TenantId && !line.IsDeleted)
            .Select(line => new { Journal = value.JournalEntryId!.Value, Line = line }))
            .ToDictionary(value => (value.Journal, value.Line.Id), value => value.Line);
        var clearingUnattributed = false;
        var scopedClearedAmount = 0m;
        foreach (var clearing in clearings)
        {
            var previousLine = clearing.SourceDocumentLineId.HasValue ? previousLines.GetValueOrDefault((clearing.JournalEntryId, clearing.SourceDocumentLineId.Value)) : null;
            if (previousLine?.PurchaseOrderItemId is Guid previousItem && !poItems.ContainsKey(previousItem)) continue;
            if (previousLine == null && previous.Any(i => i.JournalEntryId == clearing.JournalEntryId && i.AutoInvoiceRequestId.HasValue))
                throw new InvalidOperationException("A consolidated invoice clearing has no provable PO line authority. Reconcile its original journal before another invoice.");
            scopedClearedAmount += clearing.DebitAmount - clearing.CreditAmount;
            var item = previousLine is null ? null : ItemId(previousLine);
            if (item.HasValue) Add(item.Value, clearing.AccountId, -(clearing.DebitAmount - clearing.CreditAmount));
            else if (credits.Select(value => value.AccountId).Distinct().Count() > 1)
                throw new InvalidOperationException("A previously posted invoice has no provable item lineage for its accrual clearing. Reconcile it before another partial invoice.");
            else clearingUnattributed = true;
        }
        var accounts = credits.Select(value => value.AccountId).Distinct().ToArray();
        if (legacyUnattributed && accounts.Length > 1)
            throw new InvalidOperationException("Legacy receipt movements are required to allocate mixed accrual accounts. Reconcile the original receipt before invoicing.");
        var result = new Dictionary<Guid, List<ReceiptAccrualShare>>();
        foreach (var line in invoice.LineItems.Where(HasReceiptAccrualAmount))
        {
            if (line.PurchaseOrderItemId.HasValue && poItems.TryGetValue(line.PurchaseOrderItemId.Value, out var poItem) &&
                poItem.LineType != ItemType.StockItem)
                throw new InvalidOperationException($"PO line '{line.Description}' is not a stock item and has no inventory receipt accrual. Mixed stock/service receipt clearing is not supported by this posting route; no account has been guessed.");
            if (credits.Count == 0)
            {
                if (events.Count > 0)
                    throw new InvalidOperationException("The original receipt journal is unavailable or reversed; it cannot supply an accrual account.");
                // Backward-compatible pre-integration receipts have no Finance posting event.
                var settings = await GetFinanceSettingsAsync(ct);
                result[line.Id] = new() { new(settings.ControlAccountGRVAccrualId ??
                    throw new InvalidOperationException("GRV accrual control account is not configured."), 1m) };
                continue;
            }
            var itemId = ItemId(line);
            var shares = itemId.HasValue ? balances.Where(value => value.Key.Item == itemId.Value)
                .Select(value => new ReceiptAccrualShare(value.Key.Account, value.Value)).Where(value => value.Weight > 0m)
                .OrderBy(value => value.AccountId).ToList() : new List<ReceiptAccrualShare>();
            if (legacyUnattributed || clearingUnattributed || (shares.Count == 0 && accounts.Length == 1 && !itemId.HasValue))
            {
                var outstanding = credits.Sum(value => value.CreditAmount - value.DebitAmount) - scopedClearedAmount;
                shares = outstanding > 0m ? new() { new(accounts[0], outstanding) } : new();
            }
            if (shares.Count == 0)
                throw new InvalidOperationException($"No remaining original receipt accrual can be identified for invoice line '{line.Description}'. Check its PO item and earlier invoices.");
            result[line.Id] = shares;
        }
        return result;
    }

    private async Task<List<AccountTransaction>> LoadApAccrualClearingsAsync(
        IReadOnlyCollection<VendorInvoice> invoices, bool includeReversed, CancellationToken ct)
    {
        var byId = invoices.Where(value => value.TenantId == TenantId && value.JournalEntryId.HasValue).ToDictionary(value => value.Id);
        if (byId.Count == 0) return new();
        var ids = byId.Keys.ToArray();
        var events = await _unitOfWork.Repository<FinancePostingEvent>().GetQueryable(value => value.TenantId == TenantId &&
            !value.IsDeleted && value.SourceModule == "AP" && value.SourceDocumentType == "VendorInvoice" &&
            ids.Contains(value.SourceDocumentId) && value.PostingAction == "Post" && value.PostingStatus == "Posted" && value.JournalEntryId.HasValue)
            .AsNoTracking().ToListAsync(ct);
        var journalIds = events.Where(value => byId[value.SourceDocumentId].JournalEntryId == value.JournalEntryId)
            .Select(value => value.JournalEntryId!.Value).Distinct().ToArray();
        var journals = await _unitOfWork.Repository<JournalEntry>().GetQueryable(value => value.TenantId == TenantId &&
            !value.IsDeleted && journalIds.Contains(value.Id) && value.PostingStatus == "Posted" &&
            (includeReversed || (!value.IsReversed && !value.ReversalJournalEntryId.HasValue)))
            .Select(value => value.Id).ToListAsync(ct);
        return await _unitOfWork.Repository<AccountTransaction>().GetQueryable(value => value.TenantId == TenantId &&
            !value.IsDeleted && (includeReversed || !value.IsReversed) && journals.Contains(value.JournalEntryId) && value.TransactionTag == "AP-GRV")
            .AsNoTracking().ToListAsync(ct);
    }

    private async Task<Guid?> ResolveItemInventoryAccountAsync(Guid? itemId, CancellationToken ct)
    {
        if (!itemId.HasValue) return null;
        return await _unitOfWork.Repository<InventoryItem>().GetQueryable(value => value.TenantId == TenantId &&
            value.Id == itemId && !value.IsDeleted).Select(value => value.InventoryAccountId).SingleOrDefaultAsync(ct);
    }
}
