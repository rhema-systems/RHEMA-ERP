using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance;

public sealed record ProcurementReceiptAccountShare(Guid AccountTransactionId, Guid AccountId, decimal Amount);

public sealed record ProcurementReceiptCostEvidence
{
    public ProcurementReceiptCostBasis? Basis { get; init; }
    public required ProcurementAcceptedReceiptLineDto Source { get; init; }
    public Guid GoodsReceiptNoteItemId { get; init; }
    public Guid InventoryItemId { get; init; }
    public Guid PurchaseOrderReceiptItemId { get; init; }
    public decimal PurchaseToBase { get; init; }
    public decimal AcceptedBaseQuantity { get; init; }
    public decimal PurchaseQuantity { get; init; }
    public Guid ReceiptJournalEntryId { get; init; }
    public Guid AccountingBookId { get; init; }
    public required string FunctionalCurrency { get; init; }
    public required string PurchaseCurrency { get; init; }
    public decimal PurchaseAmount { get; init; }
    public decimal FunctionalAccrualAmount { get; init; }
    public decimal FunctionalInventoryAmount { get; init; }
    public decimal ExchangeRateToFunctional { get; init; }
    public required IReadOnlyList<ProcurementReceiptAccountShare> AccrualShares { get; init; }
    public required IReadOnlyList<ProcurementReceiptAccountShare> InventoryShares { get; init; }
}

/// <summary>Read-only original accounting evidence. Callers retain their own document permissions and PO→GRN transaction locks.</summary>
public sealed class ProcurementReceiptCostBasisReader(IUnitOfWork unitOfWork, Guid tenantId, IProcurementAcceptedSupplyService acceptedSupply)
{
    public async Task<ProcurementReceiptCostEvidence> ResolveAsync(Guid goodsReceiptNoteItemId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new InvalidOperationException("Receipt cost tenant context is required.");
        var line = await unitOfWork.Repository<GoodsReceiptNoteItem>().GetQueryable(x =>
            x.TenantId == tenantId && x.Id == goodsReceiptNoteItemId && !x.IsDeleted)
            .Include(x => x.GoodsReceiptNote).AsNoTracking().SingleOrDefaultAsync(ct)
            ?? throw Invalid("The original GRN line is unavailable in this tenant.");
        var grn = line.GoodsReceiptNote;
        if (grn.TenantId != tenantId || grn.IsDeleted || !grn.PurchaseOrderId.HasValue || !grn.PurchaseOrderReceiptId.HasValue)
            throw Invalid("The original GRN has no governed receipt/PO lineage.");
        var accepted = await acceptedSupply.GetGoodsReceiptLinesAsync(grn.PurchaseOrderId.Value, ct);
        var matching = accepted.Where(x => x.GoodsReceiptNoteItemId == line.Id && x.GoodsReceiptNoteId == grn.Id).ToArray();
        if (matching.Length != 1 || matching[0].AcceptedQuantity <= 0)
            throw Invalid("A unique approved receipt quantity is required.");
        var source = matching[0];
        var receiptLine = await unitOfWork.Repository<PurchaseOrderReceiptItem>().GetQueryable(x =>
            x.TenantId == tenantId && x.Id == source.PurchaseOrderReceiptItemId && !x.IsDeleted).AsNoTracking().SingleAsync(ct);
        if (receiptLine.ReceiptId != source.PurchaseOrderReceiptId || receiptLine.PurchaseOrderItemId != source.PurchaseOrderItemId ||
            receiptLine.ReceivedQuantity <= 0 || line.ReceivedQuantity <= 0)
            throw Invalid("The original receipt quantity conversion is unavailable.");
        var conversion = line.ReceivedQuantity / receiptLine.ReceivedQuantity;
        var baseQuantity = source.AcceptedQuantity * conversion;
        if (baseQuantity != line.AcceptedQuantity)
            throw Invalid("The approved receipt and GRN base quantities disagree.");

        var postings = await unitOfWork.Repository<FinancePostingEvent>().GetQueryable(x => x.TenantId == tenantId && !x.IsDeleted &&
            x.SourceDocumentType == "ProcurementPurchaseOrderReceipt" && x.SourceDocumentId == source.PurchaseOrderReceiptId &&
            x.PostingAction == "PostAcceptedInventoryReceipt" && x.PostingStatus == "Posted" && x.JournalEntryId.HasValue)
            .AsNoTracking().ToListAsync(ct);
        var journalIds = postings.Select(x => x.JournalEntryId!.Value).Distinct().ToArray();
        var journals = await unitOfWork.Repository<JournalEntry>().GetQueryable(x => x.TenantId == tenantId && !x.IsDeleted &&
            journalIds.Contains(x.Id) && x.PostingStatus == "Posted" && !x.IsReversed && !x.ReversalJournalEntryId.HasValue)
            .AsNoTracking().ToListAsync(ct);
        if (journals.Count != 1) throw Invalid("A unique active original receipt journal is required; current account defaults cannot replace it.");
        var journal = journals[0];
        var matchingPostings = postings.Where(x => x.JournalEntryId == journal.Id).ToArray();
        if (matchingPostings.Length != 1) throw Invalid("The original receipt posting event is not unique.");
        var posting = matchingPostings[0];
        var transactions = await unitOfWork.Repository<AccountTransaction>().GetQueryable(x => x.TenantId == tenantId &&
            !x.IsDeleted && !x.IsReversed && x.JournalEntryId == journal.Id &&
            (x.TransactionTag == "INV-RECEIPT-GRV-ACCRUAL" || x.TransactionTag == "INV-RECEIPT-CONTROL"))
            .AsNoTracking().ToListAsync(ct);
        if (transactions.Any(x => x.AccountingBookId != posting.AccountingBookId ||
            !string.Equals(x.FunctionalCurrencyCode, posting.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase)))
            throw Invalid("The original receipt journal currency or book evidence is inconsistent.");

        var bases = await unitOfWork.Repository<ProcurementReceiptCostBasis>().GetQueryable(x => x.TenantId == tenantId &&
            x.PurchaseOrderReceiptId == source.PurchaseOrderReceiptId && x.InventoryItemId == line.InventoryItemId && !x.IsDeleted)
            .AsNoTracking().OrderBy(x => x.PurchaseOrderReceiptItemId).ToListAsync(ct);
        var basis = bases.SingleOrDefault(x => x.PurchaseOrderReceiptItemId == source.PurchaseOrderReceiptItemId);
        decimal foreignAmount, rate;
        string purchaseCurrency;
        int index;
        decimal[] accrualWeights, inventoryWeights;
        if (basis is not null)
        {
            if (basis.Version != 1 || basis.PurchaseOrderItemId != source.PurchaseOrderItemId || basis.InventoryItemId != line.InventoryItemId ||
                basis.PurchaseQuantity != source.AcceptedQuantity || basis.BaseQuantity != baseQuantity || basis.ConversionToBase != conversion ||
                basis.FunctionalCurrency != posting.FunctionalCurrencyCode || basis.ExchangeRateToFunctional <= 0)
                throw Invalid("Retained receipt cost evidence does not match the approved source.");
            var movements = await unitOfWork.Repository<InventoryMovement>().GetQueryable(x => x.TenantId == tenantId && !x.IsDeleted &&
                x.IsPosted && x.ReferenceId == source.PurchaseOrderReceiptId && x.InventoryItemId == line.InventoryItemId &&
                x.MovementType == InventoryMovementType.PurchaseReceipt).AsNoTracking().ToListAsync(ct);
            if (movements.Count != bases.Count || bases.Any(value => !movements.Any(m => m.Id == value.InventoryMovementId &&
                m.Quantity == value.BaseQuantity && m.WarehouseId == value.WarehouseId && m.LocationId == value.LocationId &&
                value.FunctionalInventoryAmount == decimal.Round(m.TotalValue, 2, MidpointRounding.AwayFromZero) &&
                value.FunctionalAccrualAmount == decimal.Round(m.TotalValue + m.VarianceAmount.GetValueOrDefault(), 2, MidpointRounding.AwayFromZero))) ||
                bases.Any(x => x.FunctionalAccrualAmount < 0 || x.FunctionalInventoryAmount < 0))
                throw Invalid("Receipt cost snapshots do not cover the exact original inventory movements.");
            index = bases.IndexOf(basis);
            accrualWeights = bases.Select(x => x.FunctionalAccrualAmount).ToArray();
            inventoryWeights = bases.Select(x => x.FunctionalInventoryAmount).ToArray();
            foreignAmount = basis.PurchaseAmount; rate = basis.ExchangeRateToFunctional; purchaseCurrency = basis.PurchaseCurrency;
        }
        else
        {
            var order = await unitOfWork.Repository<PurchaseOrder>().GetQueryable(x => x.Id == source.PurchaseOrderId &&
                x.TenantId == tenantId && !x.IsDeleted).AsNoTracking().SingleAsync(ct);
            if (!string.Equals(order.Currency, posting.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase))
                throw Invalid("This historical foreign-currency receipt has no retained conversion evidence. Reconcile its original cost before invoice or return accounting.");
            var sameItem = await unitOfWork.Repository<GoodsReceiptNoteItem>().GetQueryable(x => x.TenantId == tenantId &&
                x.GoodsReceiptNoteId == grn.Id && x.InventoryItemId == line.InventoryItemId && !x.IsDeleted && x.AcceptedQuantity > 0).CountAsync(ct);
            if (sameItem != 1 || bases.Count != 0)
                throw Invalid("This historical receipt has ambiguous repeated-item cost attribution.");
            index = 0; accrualWeights = [1m]; inventoryWeights = [1m]; rate = 1m; purchaseCurrency = posting.FunctionalCurrencyCode;
            foreignAmount = 0; // The original functional journal supplies the historical purchase amount below.
        }

        var itemTransactions = transactions.Where(x => x.SourceDocumentLineId == line.InventoryItemId).ToList();
        if (itemTransactions.Count == 0 && transactions.All(x => !x.SourceDocumentLineId.HasValue))
        {
            var items = await unitOfWork.Repository<InventoryMovement>().GetQueryable(x => x.TenantId == tenantId && !x.IsDeleted &&
                x.IsPosted && x.ReferenceId == source.PurchaseOrderReceiptId && x.MovementType == InventoryMovementType.PurchaseReceipt)
                .Select(x => x.InventoryItemId).Distinct().ToListAsync(ct);
            if (items.Count == 1 && items[0] == line.InventoryItemId) itemTransactions = transactions;
        }
        List<ProcurementReceiptAccountShare> Shares(string tag, bool credit, decimal[] weights)
        {
            var rows = itemTransactions.Where(x => x.TransactionTag == tag).OrderBy(x => x.Id).ToList();
            if (rows.Count == 0) throw Invalid("The original receipt account transactions have no provable item attribution.");
            return rows.Select(row => {
                var amount = credit ? row.CreditAmount - row.DebitAmount : row.DebitAmount - row.CreditAmount;
                if (amount < 0) throw Invalid("The original receipt account purpose has an invalid sign.");
                return new ProcurementReceiptAccountShare(row.Id, row.AccountId, MonetaryAllocation.Allocate(weights, amount)[index]);
            }).Where(x => x.Amount != 0).ToList();
        }
        var accrual = Shares("INV-RECEIPT-GRV-ACCRUAL", true, accrualWeights);
        var inventory = Shares("INV-RECEIPT-CONTROL", false, inventoryWeights);
        if (basis is null) foreignAmount = accrual.Sum(x => x.Amount);
        return new() {
            Basis = basis, Source = source, GoodsReceiptNoteItemId = line.Id, InventoryItemId = line.InventoryItemId, PurchaseOrderReceiptItemId = receiptLine.Id,
            PurchaseToBase = conversion, AcceptedBaseQuantity = baseQuantity, PurchaseQuantity = source.AcceptedQuantity,
            ReceiptJournalEntryId = journal.Id, AccountingBookId = posting.AccountingBookId, FunctionalCurrency = posting.FunctionalCurrencyCode,
            PurchaseCurrency = purchaseCurrency, PurchaseAmount = foreignAmount, FunctionalAccrualAmount = accrual.Sum(x => x.Amount),
            FunctionalInventoryAmount = inventory.Sum(x => x.Amount), ExchangeRateToFunctional = rate,
            AccrualShares = accrual, InventoryShares = inventory,
        };
    }

    private static InvalidOperationException Invalid(string message) => new($"RECEIPT_COST_RECONCILIATION_REQUIRED: {message}");
}
