using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Core.Services.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

public partial class VendorInvoiceService
{
    private sealed record ReceiptCostPlan(VendorInvoiceReceiptAllocation Allocation,
        VendorInvoiceReceiptCostAllocation Cost, IReadOnlyList<VendorInvoiceReceiptCostPostingLine> Lines,
        IReadOnlyList<ReceiptStockAttribution.Share> Stock, bool NewAllocation);

    private static Guid CostEvidenceId(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));
    private static IReadOnlyList<VendorInvoiceReceiptCostPostingLine> OrderedCostLines(IEnumerable<VendorInvoiceReceiptCostPostingLine> lines)
        => lines.OrderBy(x => x.Purpose switch { "Accrual" => 0, "Inventory" => 1, "Ppv" => 2, "Fx" => 3, _ => 4 })
            .ThenBy(x => x.AccountId).ThenBy(x => x.OriginalReceiptAccountTransactionId).ToArray();

    private async Task<IReadOnlyList<ReceiptCostPlan>> ResolveReceiptCostsAsync(VendorInvoice invoice, CancellationToken ct)
    {
        if (!IsProcurementGrvClearingInvoice(invoice)) return [];
        var retained = await _unitOfWork.Repository<VendorInvoiceReceiptCostAllocation>().GetQueryable(x =>
            x.TenantId == TenantId && x.VendorInvoiceId == invoice.Id && !x.IsDeleted).AsNoTracking().ToListAsync(ct);
        var allocations = await _unitOfWork.Repository<VendorInvoiceReceiptAllocation>().GetQueryable(x =>
            x.TenantId == TenantId && x.VendorInvoiceId == invoice.Id && !x.IsDeleted).AsNoTracking().ToListAsync(ct);
        if (invoice.JournalEntryId.HasValue)
        {
            if (retained.Count == 0) return []; // Original posted legacy distributions remain owned by their journal.
            var ids = retained.Select(x => x.Id).ToArray();
            var lines = await _unitOfWork.Repository<VendorInvoiceReceiptCostPostingLine>().GetQueryable(x =>
                x.TenantId == TenantId && ids.Contains(x.CostAllocationId) && !x.IsDeleted).AsNoTracking().ToListAsync(ct);
            return retained.OrderBy(x => x.VendorInvoiceLineItemId).ThenBy(x => x.GoodsReceiptNoteItemId)
                .Select(x => new ReceiptCostPlan(allocations.Single(a => a.Id == x.VendorInvoiceReceiptAllocationId),
                x, OrderedCostLines(lines.Where(l => l.CostAllocationId == x.Id)), [], false)).ToArray();
        }
        if (retained.Count != 0) throw new InvalidOperationException("Invoice cost evidence exists without its retained posted journal. Finance reconciliation is required.");
        var settings = await GetFinanceSettingsAsync(ct);
        var procurement = await _unitOfWork.Repository<ProcurementSettings>().GetQueryable(x => x.TenantId == TenantId && !x.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(ct);
        var reader = new ProcurementReceiptCostBasisReader(_unitOfWork, TenantId, RequireAcceptedSupplyServiceAsync());
        var result = new List<ReceiptCostPlan>();
        var available = new List<AvailableReceiptLine>();
        foreach (var po in await ReceiptInvoiceOrderIdsAsync(invoice, ct)) available.AddRange(await AvailableReceiptLinesAsync(po, invoice.Id, ct));
        var assigned = new Dictionary<Guid, decimal>();
        foreach (var invoiceLine in invoice.LineItems.Where(x => !x.IsDeleted).OrderBy(x => x.Id))
        {
            if (invoiceLine.Quantity <= 0 || invoiceLine.UnitPrice < 0 || invoiceLine.DiscountAmount < 0 || invoiceLine.TaxAmount < 0)
                throw new InvalidOperationException("Invoice receipt quantities must be positive and prices, discounts and tax cannot be negative.");
            var expectedDiscount = InvoiceTradeDiscountPolicy.CalculateLineDiscount(
                invoiceLine.Quantity * invoiceLine.UnitPrice, invoiceLine.DiscountPercentage, "AP invoice line");
            if (RoundMoney(invoiceLine.DiscountAmount) != expectedDiscount)
                throw new InvalidOperationException("AP invoice line trade discount evidence does not match its percentage and gross amount.");
            if (!invoiceLine.PurchaseOrderItemId.HasValue)
                throw new InvalidOperationException("Every receipt-clearing invoice line requires its original purchase-order line.");
            var oldLines = await _unitOfWork.Repository<VendorInvoiceLineItem>().GetQueryable(x => x.TenantId == TenantId &&
                x.PurchaseOrderItemId == invoiceLine.PurchaseOrderItemId && x.VendorInvoiceId != invoice.Id && !x.IsDeleted &&
                !x.VendorInvoice.IsDeleted && x.VendorInvoice.JournalEntryId.HasValue && x.VendorInvoice.Status != VendorInvoiceStatus.Voided)
                .Select(x => x.Id).ToListAsync(ct);
            if (oldLines.Count > 0)
            {
                var covered = await _unitOfWork.Repository<VendorInvoiceReceiptCostAllocation>().GetQueryable(x => x.TenantId == TenantId &&
                    oldLines.Contains(x.VendorInvoiceLineItemId) && !x.IsDeleted && !x.ReversalJournalEntryId.HasValue)
                    .Select(x => x.VendorInvoiceLineItemId).Distinct().ToListAsync(ct);
                if (oldLines.Any(x => !covered.Contains(x)))
                    throw new InvalidOperationException("RECEIPT_COST_RECONCILIATION_REQUIRED: historical invoices on this PO line need exact original receipt cost attribution before further clearing.");
            }
            var selected = allocations.Where(x => x.VendorInvoiceLineItemId == invoiceLine.Id).OrderBy(x => x.GoodsReceiptNoteItemId).ToList();
            var newAllocation = selected.Count == 0;
            if (newAllocation)
            {
                var remaining = invoiceLine.Quantity;
                foreach (var receipt in available.Where(x => x.Source.PurchaseOrderItemId == invoiceLine.PurchaseOrderItemId)
                    .OrderBy(x => x.Source.ReceiptDate).ThenBy(x => x.Source.PurchaseOrderReceiptItemId))
                {
                    var source = receipt.Source;
                    if (!source.GoodsReceiptNoteId.HasValue || !source.GoodsReceiptNoteItemId.HasValue) continue;
                    var quantity = Math.Min(remaining, Math.Max(0, receipt.Available - assigned.GetValueOrDefault(source.GoodsReceiptNoteItemId.Value)));
                    if (quantity == 0) continue;
                    selected.Add(new VendorInvoiceReceiptAllocation {
                        Id = CostEvidenceId($"receipt-allocation:{invoice.Id:N}:{invoiceLine.Id:N}:{source.GoodsReceiptNoteItemId:N}"),
                        TenantId = TenantId, CreatedById = CurrentUserId, VendorInvoiceId = invoice.Id, VendorInvoiceLineItemId = invoiceLine.Id,
                        PurchaseOrderId = source.PurchaseOrderId, PurchaseOrderItemId = source.PurchaseOrderItemId,
                        PurchaseOrderReceiptId = source.PurchaseOrderReceiptId, PurchaseOrderReceiptItemId = source.PurchaseOrderReceiptItemId,
                        InspectionCaseId = source.InspectionCaseId, GoodsReceiptNoteId = source.GoodsReceiptNoteId.Value,
                        GoodsReceiptNoteItemId = source.GoodsReceiptNoteItemId.Value, Quantity = quantity
                    });
                    remaining -= quantity;
                    if (remaining == 0) break;
                }
                if (remaining != 0) throw new InvalidOperationException("The invoice line exceeds its exact accepted, unreturned receipt quantity.");
            }
            if (selected.Sum(x => x.Quantity) != invoiceLine.Quantity)
                throw new InvalidOperationException("Invoice receipt allocation quantities do not equal the invoice line.");
            selected = selected.OrderBy(x => x.GoodsReceiptNoteItemId).ToList();
            var invoiceAmounts = MonetaryAllocation.Allocate(selected.Select(x => x.Quantity).ToArray(), RoundMoney(invoiceLine.Quantity * invoiceLine.UnitPrice - invoiceLine.DiscountAmount));
            var functionalAmounts = MonetaryAllocation.Allocate(invoiceAmounts, RoundMoney(invoiceAmounts.Sum() * invoice.ExchangeRate));
            for (var i = 0; i < selected.Count; i++)
            {
                var allocation = selected[i];
                assigned[allocation.GoodsReceiptNoteItemId] = assigned.GetValueOrDefault(allocation.GoodsReceiptNoteItemId) + allocation.Quantity;
                var capacity = available.SingleOrDefault(x => x.Source.GoodsReceiptNoteItemId == allocation.GoodsReceiptNoteItemId);
                if (capacity is null || assigned[allocation.GoodsReceiptNoteItemId] > capacity.Available)
                    throw new InvalidOperationException("The selected original receipt is no longer available for this invoice quantity.");
                var basis = await reader.ResolveAsync(allocation.GoodsReceiptNoteItemId, ct);
                if (!string.Equals(basis.PurchaseCurrency, invoice.CurrencyCode, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(basis.FunctionalCurrency, settings.BaseCurrency, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The invoice and original receipt currency evidence do not agree.");
                var prior = await _unitOfWork.Repository<VendorInvoiceReceiptCostAllocation>().GetQueryable(x => x.TenantId == TenantId &&
                    x.GoodsReceiptNoteItemId == allocation.GoodsReceiptNoteItemId && !x.IsDeleted && !x.ReversalJournalEntryId.HasValue)
                    .AsNoTracking().ToListAsync(ct);
                prior.AddRange(result.Where(x => x.Allocation.GoodsReceiptNoteItemId == allocation.GoodsReceiptNoteItemId).Select(x => x.Cost));
                var finalReturnIds = _unitOfWork.Repository<PurchaseReturn>().GetQueryable(x => x.TenantId == TenantId && !x.IsDeleted &&
                    x.AccountingAllocationVersion == 1 && (x.Status == "Shipped" || x.Status == "Acknowledged")).Select(x => x.Id);
                var returns = await _unitOfWork.Repository<InventorySupplierReturnAllocation>().GetQueryable(x => x.TenantId == TenantId && !x.IsDeleted &&
                    x.GoodsReceiptNoteItemId == allocation.GoodsReceiptNoteItemId && !x.OriginalVendorInvoiceId.HasValue &&
                    finalReturnIds.Contains(x.InventoryPurchaseReturnId)).AsNoTracking().ToListAsync(ct);
                if (returns.Any(x => x.PurchaseCurrency != basis.PurchaseCurrency || x.FunctionalCurrency != basis.FunctionalCurrency))
                    throw new InvalidOperationException("Returned receipt cost currency evidence does not agree with the original receipt.");
                // Old invoices without exact cost evidence cannot be treated as though they left all GRNI untouched.
                var activeReceiptAllocations = await _unitOfWork.Repository<VendorInvoiceReceiptAllocation>().GetQueryable(x => x.TenantId == TenantId &&
                    x.GoodsReceiptNoteItemId == allocation.GoodsReceiptNoteItemId && x.VendorInvoiceId != invoice.Id && !x.IsDeleted &&
                    !x.VendorInvoice.IsDeleted && x.VendorInvoice.JournalEntryId.HasValue && x.VendorInvoice.Status != VendorInvoiceStatus.Voided)
                    .Select(x => x.Id).ToListAsync(ct);
                if (activeReceiptAllocations.Any(id => prior.All(x => x.VendorInvoiceReceiptAllocationId != id)))
                    throw new InvalidOperationException("RECEIPT_COST_RECONCILIATION_REQUIRED: a historical invoice cleared this receipt without exact retained cost allocation evidence.");
                var item = await _unitOfWork.Repository<InventoryItem>().GetQueryable(x => x.Id == basis.InventoryItemId && x.TenantId == TenantId && !x.IsDeleted)
                    .AsNoTracking().SingleAsync(ct);
                IReadOnlyList<ReceiptStockAttribution.Share> stock = [];
                var calculation = new SupplierInvoiceCostDifferenceCalculator.Input(
                    basis.PurchaseQuantity, prior.Sum(x => x.PurchaseQuantity) + returns.Sum(x => x.PurchaseQuantity), allocation.Quantity,
                    basis.PurchaseAmount, basis.FunctionalAccrualAmount, prior.Sum(x => x.ReceiptForeignAmount) + returns.Sum(x => x.OriginalAccrualForeignAmount),
                    prior.Sum(x => x.ReceiptFunctionalAmount) + returns.Sum(x => x.OriginalAccrualAmount),
                    invoiceAmounts[i], invoice.ExchangeRate, basis.AcceptedBaseQuantity, stock.Sum(x => x.Quantity),
                    procurement?.PurchasePriceDifferenceHandling, item.ValuationMethod == ValuationMethod.StandardCost, functionalAmounts[i]);
                var values = SupplierInvoiceCostDifferenceCalculator.Calculate(calculation);
                if (values.PriceDifferenceFunctionalAmount != 0 && procurement?.PurchasePriceDifferenceHandling == PurchasePriceDifferencePolicy.RevalueInventory &&
                    item.ValuationMethod != ValuationMethod.StandardCost)
                {
                    stock = await ResolveReceiptStockAsync(basis, item, ct);
                    values = SupplierInvoiceCostDifferenceCalculator.Calculate(calculation with { RetainedReceiptBaseQuantity = stock.Sum(x => x.Quantity) });
                }
                var cost = new VendorInvoiceReceiptCostAllocation {
                    Id = CostEvidenceId($"invoice-cost:{allocation.Id:N}"), TenantId = TenantId, CreatedById = CurrentUserId,
                    VendorInvoiceId = invoice.Id, VendorInvoiceLineItemId = invoiceLine.Id, VendorInvoiceReceiptAllocationId = allocation.Id,
                    ProcurementReceiptCostBasisId = basis.Basis?.Id, GoodsReceiptNoteItemId = allocation.GoodsReceiptNoteItemId,
                    InventoryItemId = item.Id, ReceiptJournalEntryId = basis.ReceiptJournalEntryId, AccountingBookId = basis.AccountingBookId,
                    PurchaseQuantity = allocation.Quantity, BaseQuantity = allocation.Quantity * basis.PurchaseToBase,
                    ReceiptForeignAmount = values.ReceiptForeignAmount, ReceiptFunctionalAmount = values.ReceiptFunctionalAmount,
                    InvoiceNetForeignAmount = invoiceAmounts[i], InvoiceFunctionalAmount = values.InvoiceFunctionalAmount,
                    PriceDifferenceFunctionalAmount = values.PriceDifferenceFunctionalAmount, ExchangeDifferenceFunctionalAmount = values.ExchangeDifferenceFunctionalAmount,
                    InventoryAdjustmentAmount = values.InventoryAdjustmentAmount, PurchasePriceVarianceAmount = values.PurchasePriceVarianceAmount,
                    RevaluedReceiptBaseQuantity = values.InventoryAdjustmentAmount == 0 ? 0 : stock.Sum(x => x.Quantity),
                    Policy = values.PriceDifferenceFunctionalAmount == 0 ? "NoDifference" : PurchasePriceDifferencePolicy.Require(procurement?.PurchasePriceDifferenceHandling),
                    PurchaseCurrency = basis.PurchaseCurrency, FunctionalCurrency = basis.FunctionalCurrency,
                    InvoiceExchangeRateToFunctional = invoice.ExchangeRate, PurchasePriceVarianceAccountId = item.PurchasePriceVarianceAccountId,
                };
                var posting = new List<VendorInvoiceReceiptCostPostingLine>();
                void Add(Guid account, string purpose, decimal amount, Guid? original = null, decimal foreign = 0)
                {
                    if (amount == 0) return;
                    posting.Add(new() { TenantId = TenantId, CreatedById = CurrentUserId, CostAllocationId = cost.Id,
                        AccountId = account, Purpose = purpose, FunctionalAmount = amount, ForeignAmount = foreign,
                        OriginalReceiptAccountTransactionId = original });
                }
                var priorIds = prior.Select(x => x.Id).ToArray();
                var oldAccrual = await _unitOfWork.Repository<VendorInvoiceReceiptCostPostingLine>().GetQueryable(x => x.TenantId == TenantId &&
                    priorIds.Contains(x.CostAllocationId) && x.Purpose == "Accrual" && !x.IsDeleted).AsNoTracking().ToListAsync(ct);
                oldAccrual.AddRange(result.SelectMany(x => x.Lines).Where(x => priorIds.Contains(x.CostAllocationId) && x.Purpose == "Accrual"));
                var returnIds = returns.Select(x => x.Id).ToArray();
                var returnedAccrual = await _unitOfWork.Repository<InventorySupplierReturnAccrualShare>().GetQueryable(x => x.TenantId == TenantId && !x.IsDeleted &&
                    returnIds.Contains(x.InventorySupplierReturnAllocationId)).AsNoTracking().ToListAsync(ct);
                var remainingWeights = basis.AccrualShares.Select(x => x.Amount - oldAccrual.Where(y => y.OriginalReceiptAccountTransactionId == x.AccountTransactionId).Sum(y => y.FunctionalAmount)
                    - returnedAccrual.Where(y => y.OriginalReceiptAccountTransactionId == x.AccountTransactionId).Sum(y => y.Amount)).ToArray();
                if (remainingWeights.Any(x => x < 0)) throw new InvalidOperationException("Original receipt accrual has been over-cleared.");
                var cleared = MonetaryAllocation.Allocate(remainingWeights, values.ReceiptFunctionalAmount);
                for (var n = 0; n < cleared.Length; n++)
                {
                    if (cleared[n] > remainingWeights[n]) throw new InvalidOperationException("The invoice exceeds an original accrual account's remaining amount.");
                    Add(basis.AccrualShares[n].AccountId, "Accrual", cleared[n], basis.AccrualShares[n].AccountTransactionId);
                }
                if (values.InventoryAdjustmentAmount != 0)
                {
                    var inventoryAmounts = SupplierInvoiceCostDifferenceCalculator.AllocateSigned(basis.InventoryShares.Select(x => x.Amount).ToArray(), values.InventoryAdjustmentAmount);
                    for (var n = 0; n < inventoryAmounts.Length; n++) Add(basis.InventoryShares[n].AccountId, "Inventory", inventoryAmounts[n]);
                }
                if (values.PurchasePriceVarianceAmount != 0)
                    Add(item.PurchasePriceVarianceAccountId ?? throw new InvalidOperationException($"Configure the purchase price variance account for item {item.ItemCode}."), "Ppv", values.PurchasePriceVarianceAmount);
                if (values.ExchangeDifferenceFunctionalAmount != 0)
                    Add((values.ExchangeDifferenceFunctionalAmount > 0 ? settings.UnrealizedFxLossAccountId : settings.UnrealizedFxGainAccountId)
                        ?? throw new InvalidOperationException("Configure Finance unrealized exchange gain/loss accounts before clearing a receipt at a changed invoice rate."), "Fx", values.ExchangeDifferenceFunctionalAmount);
                cost.SourceFingerprint = DistributionHash(JsonSerializer.Serialize(new { cost.Policy, basis.ReceiptJournalEntryId,
                    BasisId = basis.Basis?.Id, allocation.GoodsReceiptNoteItemId, allocation.Quantity, values, stock,
                    Accounts = posting.Select(x => new { x.AccountId, x.Purpose, x.FunctionalAmount, x.OriginalReceiptAccountTransactionId }) }));
                result.Add(new(allocation, cost, OrderedCostLines(posting), stock, newAllocation));
            }
        }
        return result;
    }

    private async Task<IReadOnlyList<ReceiptStockAttribution.Share>> ResolveReceiptStockAsync(ProcurementReceiptCostEvidence basis, InventoryItem item, CancellationToken ct)
    {
        var movements = await _unitOfWork.Repository<InventoryMovement>().GetQueryable(x => x.TenantId == TenantId && x.InventoryItemId == item.Id &&
            !x.IsDeleted && x.IsPosted).AsNoTracking().ToListAsync(ct);
        var origin = basis.Basis?.InventoryMovementId ?? movements.Where(x => x.ReferenceId == basis.Source.PurchaseOrderReceiptId &&
            x.MovementType == InventoryMovementType.PurchaseReceipt).Select(x => x.Id).Single();
        var balances = await _unitOfWork.Repository<InventoryBalance>().GetQueryable(x => x.TenantId == TenantId && x.InventoryItemId == item.Id && !x.IsDeleted)
            .AsNoTracking().ToListAsync(ct);
        var layers = await _unitOfWork.Repository<InventoryLayer>().GetQueryable(x => x.TenantId == TenantId && x.InventoryItemId == item.Id && !x.IsDeleted)
            .AsNoTracking().ToListAsync(ct);
        return ReceiptStockAttribution.Resolve(origin, item.ValuationMethod, movements, balances, layers);
    }

    private async Task PersistReceiptCostsAsync(VendorInvoice invoice, IReadOnlyList<ReceiptCostPlan> plans,
        Guid postingEventId, Guid journalEntryId, CancellationToken ct)
    {
        if (!_unitOfWork.HasActiveTransaction) throw new InvalidOperationException("Receipt cost posting requires the shared Finance and Inventory transaction.");
        var posting = await _unitOfWork.Repository<FinancePostingEvent>().GetQueryable(x => x.Id == postingEventId &&
            x.TenantId == TenantId && x.SourceDocumentId == invoice.Id && x.SourceDocumentType == "VendorInvoice" &&
            x.PostingStatus == "Posted" && x.JournalEntryId == journalEntryId && !x.IsDeleted).AsNoTracking().SingleAsync(ct);
        if (plans.Any(x => x.Cost.AccountingBookId != posting.AccountingBookId))
            throw new InvalidOperationException("Invoice and original receipt must clear within the same accounting book.");
        foreach (var plan in plans)
        {
            if (plan.NewAllocation) await _unitOfWork.Repository<VendorInvoiceReceiptAllocation>().AddAsync(plan.Allocation);
            plan.Cost.PostingEventId = postingEventId; plan.Cost.JournalEntryId = journalEntryId;
            await _unitOfWork.Repository<VendorInvoiceReceiptCostAllocation>().AddAsync(plan.Cost);
            await _unitOfWork.Repository<VendorInvoiceReceiptCostPostingLine>().AddRangeAsync(plan.Lines);
            if (plan.Cost.InventoryAdjustmentAmount != 0)
            {
                var owner = _inventoryValuationService as IInventoryReceiptCostAdjustmentService
                    ?? throw new InvalidOperationException("The receipt cost valuation owner is not configured.");
                var shares = SupplierInvoiceCostDifferenceCalculator.AllocateSigned(plan.Stock.Select(x => x.Quantity).ToArray(), plan.Cost.InventoryAdjustmentAmount);
                var targets = plan.Stock.Select((x, i) => new ReceiptCostAdjustmentTarget(x.WarehouseId,
                    x.LocationId ?? throw new InvalidOperationException("Receipt cost adjustment requires exact storage-bin lineage."), x.LayerId, x.Quantity, shares[i]))
                    .Where(x => x.ValueChange != 0).ToArray();
                await owner.ApplyReceiptCostAdjustmentAsync(plan.Cost, targets, ct);
            }
        }
        // Stage immutable children first; the subsequent invoice journal-link update is
        // the final SQL completeness seal, in this same transaction.
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
