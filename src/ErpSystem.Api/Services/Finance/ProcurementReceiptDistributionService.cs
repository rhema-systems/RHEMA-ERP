using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using static ErpSystem.Api.Services.Finance.InventoryReceiptFinancePostingService;

namespace ErpSystem.Api.Services.Finance;

/// <summary>The same account resolver supplies the preview and the accepted receipt journal.</summary>
public sealed partial class ProcurementReceiptDistributionService(ApplicationDbContext db)
{
    public async Task<PurchaseReceiptDistributionDto> GetAsync(Guid tenantId, Guid receiptId,
        CancellationToken ct = default)
    {
        var receipt = await LoadReceiptAsync(tenantId, receiptId, ct);
        // Never recalculate history using today's item or supplier master mappings.
        var posted = await db.FinancePostingEvents.AsNoTracking().Include(value => value.JournalEntry)
            .Where(value => value.TenantId == tenantId && !value.IsDeleted &&
                value.SourceDocumentType == "ProcurementPurchaseOrderReceipt" &&
                value.SourceDocumentId == receiptId && value.PostingAction == "PostAcceptedInventoryReceipt" &&
                value.PostingStatus == "Posted" && value.JournalEntryId != null)
            .OrderByDescending(value => value.PostedAt).FirstOrDefaultAsync(ct);
        if (posted is not null)
        {
            var transactions = await db.AccountTransactions.AsNoTracking().Include(value => value.Account)
                .Where(value => value.TenantId == tenantId && value.JournalEntryId == posted.JournalEntryId && !value.IsDeleted)
                .OrderBy(value => value.LineNumber).ThenBy(value => value.Id).ToListAsync(ct);
            return new PurchaseReceiptDistributionDto
            {
                Status = "Posted", Currency = posted.FunctionalCurrencyCode,
                Basis = "Original posted journal", JournalEntryId = posted.JournalEntryId,
                JournalEntryNumber = posted.JournalEntry?.JournalEntryNumber,
                Lines = transactions.Select(value => new PurchaseReceiptDistributionLineDto
                {
                    LineId = value.Id, InventoryItemId = value.SourceDocumentLineId,
                    Purpose = PurposeKey(value.TransactionTag),
                    AccountId = value.AccountId, AccountCode = value.Account.AccountNumber,
                    AccountName = value.Account.AccountName, Type = Purpose(value.TransactionTag),
                    Source = "Posted journal", Debit = value.DebitAmount, Credit = value.CreditAmount
                }).ToList()
            };
        }
        var defaults = await BuildDefaultsAsync(receipt, false, ct);
        var distribution = ApplyDraft(receipt, defaults, strict: false);
        var ids = distribution.Lines.Select(value => value.AccountId).Distinct().ToArray();
        var accounts = await db.Accounts.AsNoTracking().Where(value => value.TenantId == tenantId && ids.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, ct);
        var itemIds = defaults.Lines.Select(value => value.SourceDocumentLineId).ToArray();
        var items = await db.InventoryItems.AsNoTracking().Where(value => value.TenantId == tenantId && itemIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, ct);
        return new PurchaseReceiptDistributionDto
        {
            Currency = distribution.Currency, Basis = distribution.Basis,
            CanEdit = receipt.Status is not ("Cancelled" or "Rejected"),
            Version = Version(receipt), BasisVersion = BasisVersion(defaults),
            HasOverrides = !string.IsNullOrWhiteSpace(receipt.DistributionDraftJson),
            NeedsReview = distribution.NeedsReview,
            EditBlockReason = distribution.NeedsReview ? "Receipt items or posting purposes changed. Review and save the distribution again." : null,
            Groups = defaults.Lines.Select(value => new PurchaseReceiptDistributionGroupDto
            {
                InventoryItemId = value.SourceDocumentLineId!.Value, Purpose = PurposeKey(value.TransactionTag),
                ItemCode = items.GetValueOrDefault(value.SourceDocumentLineId.Value)?.ItemCode ?? "",
                ItemName = items.GetValueOrDefault(value.SourceDocumentLineId.Value)?.Name ?? "",
                TotalDebit = value.DebitAmount, TotalCredit = value.CreditAmount, DefaultAccountId = value.AccountId
            }).ToList(),
            Lines = distribution.Lines.Select(value => new PurchaseReceiptDistributionLineDto
            {
                LineId = distribution.LineIds.GetValueOrDefault(value.LineNumber ?? 0),
                InventoryItemId = value.SourceDocumentLineId, Purpose = PurposeKey(value.TransactionTag),
                ItemCode = items.GetValueOrDefault(value.SourceDocumentLineId!.Value)?.ItemCode ?? "",
                ItemName = items.GetValueOrDefault(value.SourceDocumentLineId.Value)?.Name ?? "",
                AccountId = value.AccountId,
                AccountCode = accounts.GetValueOrDefault(value.AccountId)?.AccountNumber ?? "Unavailable",
                AccountName = accounts.GetValueOrDefault(value.AccountId)?.AccountName ?? string.Empty,
                Type = Purpose(value.TransactionTag), Source = distribution.Sources.GetValueOrDefault(value.LineNumber ?? 0) ?? "",
                Debit = value.DebitAmount, Credit = value.CreditAmount
            }).ToList()
        };
    }

    internal async Task<PurchaseOrderReceipt> LoadReceiptAsync(Guid tenantId, Guid receiptId, CancellationToken ct) =>
        await db.PurchaseOrderReceipts.AsNoTracking().Include(value => value.PurchaseOrder).ThenInclude(value => value.BusinessPartner)
            .SingleOrDefaultAsync(value => value.TenantId == tenantId && value.Id == receiptId && !value.IsDeleted, ct)
            ?? throw new InvalidOperationException("Purchase receipt not found.");

    internal async Task<ReceiptPostingDistribution> BuildAsync(PurchaseOrderReceipt receipt, bool requireMovements, CancellationToken ct)
    {
        var result = ApplyDraft(receipt, await BuildDefaultsAsync(receipt, requireMovements, ct), strict: true);
        await ValidateAccountsAsync(receipt.TenantId, result.Lines.Select(value => new SavePurchaseReceiptDistributionLine
        {
            AccountId = value.AccountId, Purpose = PurposeKey(value.TransactionTag)
        }), ct);
        return result;
    }

    private async Task<ReceiptPostingDistribution> BuildDefaultsAsync(PurchaseOrderReceipt receipt, bool requireMovements, CancellationToken ct)
    {
        var settings = await db.FinanceSettings.AsNoTracking()
            .SingleOrDefaultAsync(value => value.TenantId == receipt.TenantId && !value.IsDeleted, ct)
            ?? throw new InvalidOperationException("Finance settings are not configured for the receipt tenant.");
        var currency = Currency(settings.BaseCurrency);
        var movements = await db.InventoryMovements.AsNoTracking().Where(value => value.TenantId == receipt.TenantId &&
            value.ReferenceId == receipt.Id && value.MovementType == InventoryMovementType.PurchaseReceipt && value.IsPosted && !value.IsDeleted)
            .ToListAsync(ct);
        var values = movements.GroupBy(value => value.InventoryItemId)
            .Select(group => new ReceiptValue(group.Key, group.Sum(value => value.TotalValue), group.Sum(value => value.VarianceAmount ?? 0m))).ToList();
        var basis = "Posted inventory valuation movements; Finance posting pending";
        if (values.Count == 0)
        {
            if (requireMovements) throw new InvalidOperationException("The accepted receipt has no posted inventory valuation movements.");
            basis = "Saved receipt quantities; final distribution uses accepted quantities and valuation at posting. Landed costs post separately.";
            var receiptLines = await db.PurchaseOrderReceiptItems.AsNoTracking()
                .Include(value => value.PurchaseOrderItem).ThenInclude(value => value.InventoryItem)
                .Where(value => value.TenantId == receipt.TenantId && value.ReceiptId == receipt.Id && !value.IsDeleted).ToListAsync(ct);
            var uomIds = receiptLines.Where(value => value.ItemUnitOfMeasureId.HasValue).Select(value => value.ItemUnitOfMeasureId!.Value).Distinct().ToArray();
            var conversions = await db.ItemUnitsOfMeasure.AsNoTracking().Where(value => value.TenantId == receipt.TenantId &&
                uomIds.Contains(value.Id) && !value.IsDeleted).ToDictionaryAsync(value => value.Id, value => value.ConversionToBase, ct);
            foreach (var line in receiptLines)
            {
                var poLine = line.PurchaseOrderItem;
                if (poLine.LineType != ItemType.StockItem || poLine.InventoryItem is not { } item) continue;
                var inspected = receipt.Status is "Accepted" or "Inspected" or "Rejected" || receipt.InspectionDate.HasValue;
                var quantity = inspected ? line.AcceptedQuantity : Math.Max(0, line.ReceivedQuantity - line.RejectedQuantity);
                if (quantity <= 0) continue;
                var conversion = line.ItemUnitOfMeasureId.HasValue ? conversions.GetValueOrDefault(line.ItemUnitOfMeasureId.Value, 1m) : 1m;
                if (conversion <= 0) conversion = 1m;
                var purchaseValue = quantity * poLine.UnitPrice;
                var inventoryValue = item.ValuationMethod == ValuationMethod.StandardCost ? quantity * conversion * item.StandardCost : purchaseValue;
                values.Add(new ReceiptValue(item.Id, inventoryValue, purchaseValue - inventoryValue));
            }
            values = values.GroupBy(value => value.ItemId).Select(group => new ReceiptValue(group.Key,
                group.Sum(value => value.Inventory), group.Sum(value => value.Variance))).ToList();
        }
        values = values.OrderBy(value => value.ItemId).ToList();
        var itemIds = values.Select(value => value.ItemId).ToArray();
        var items = await db.InventoryItems.AsNoTracking().Where(value => value.TenantId == receipt.TenantId &&
            itemIds.Contains(value.Id) && !value.IsDeleted).ToDictionaryAsync(value => value.Id, ct);
        var partner = receipt.PurchaseOrder.BusinessPartner ?? await db.BusinessPartners.AsNoTracking()
            .SingleAsync(value => value.TenantId == receipt.TenantId && value.Id == receipt.PurchaseOrder.BusinessPartnerId && !value.IsDeleted, ct);
        var snapshot = BusinessPartnerPostingDefaults.ReadSnapshot(receipt.PurchaseOrder.SupplierDefaultsSnapshotJson);
        var defaults = snapshot?.PostingDefaults ?? BusinessPartnerPostingDefaults.FromPartner(partner);
        var supplierSource = snapshot is null ? "Supplier default" : "PO supplier default";
        var result = new ReceiptPostingDistribution(currency, basis);
        var totalInventory = Round(values.Sum(value => value.Inventory));
        var totalVariance = Round(values.Sum(value => value.Variance));
        var inventoryAmounts = MonetaryAllocation.Allocate(values.Select(value => value.Inventory).ToArray(), totalInventory);
        var accrualAmounts = MonetaryAllocation.Allocate(values.Select(value => value.Inventory + value.Variance).ToArray(),
            totalInventory + totalVariance);
        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index];
            var item = items.GetValueOrDefault(value.ItemId) ?? throw new InvalidOperationException("The receipt inventory item is unavailable in this tenant.");
            var inventory = inventoryAmounts[index];
            var variance = accrualAmounts[index] - inventory;
            if (inventory != 0)
            {
                // Preview account IDs without validating unused defaults. Saved replacements
                // are independent; the actual chosen posting targets are validated in BuildAsync.
                var account = item.InventoryAccountId ?? settings.ControlAccountInventoryId ?? Guid.Empty;
                Add(result, account, inventory, "INV-RECEIPT-CONTROL", item.InventoryAccountId.HasValue ? "Item default" : "Finance default", receipt.ReceiptNumber, item.Id);
            }
            if (variance != 0)
            {
                var account = item.PurchasePriceVarianceAccountId ?? defaults.DefaultPurchasePriceVarianceAccountId ??
                    settings.WriteOffExpenseAccountId ?? Guid.Empty;
                Add(result, account, variance, "INV-RECEIPT-PRICE-VARIANCE", item.PurchasePriceVarianceAccountId.HasValue ? "Item default" :
                    defaults.DefaultPurchasePriceVarianceAccountId.HasValue ? supplierSource : "Finance default", receipt.ReceiptNumber, item.Id);
            }
            if (inventory + variance != 0)
            {
                var account = defaults.DefaultAccruedPurchasesAccountId ?? item.InventoryOffsetAccountId ??
                    settings.ControlAccountGRVAccrualId ?? Guid.Empty;
                Add(result, account, -(inventory + variance), "INV-RECEIPT-GRV-ACCRUAL", defaults.DefaultAccruedPurchasesAccountId.HasValue ?
                    supplierSource : item.InventoryOffsetAccountId.HasValue ? "Item default" : "Finance default", receipt.ReceiptNumber, item.Id);
            }
        }
        if (requireMovements && (totalInventory <= 0 || totalInventory + totalVariance <= 0))
            throw new InvalidOperationException("The accepted receipt valuation must be positive before Finance posting.");
        return result;
    }

    private static void Add(ReceiptPostingDistribution result, Guid account, decimal amount, string tag, string source, string reference, Guid itemId, Guid? lineId = null)
    {
        var existing = lineId.HasValue ? null : result.Lines.FirstOrDefault(value => value.AccountId == account && value.SourceDocumentLineId == itemId && value.TransactionTag == tag &&
            result.Sources[value.LineNumber ?? 0] == source);
        if (existing is not null)
        {
            var net = existing.DebitAmount - existing.CreditAmount + amount;
            existing.DebitAmount = Math.Max(net, 0m);
            existing.CreditAmount = Math.Max(-net, 0m);
            existing.TransactionDebitAmount = existing.DebitAmount;
            existing.TransactionCreditAmount = existing.CreditAmount;
            return;
        }
        var number = result.Lines.Count + 1;
        result.Lines.Add(Line(account, $"{Purpose(tag)} {reference}", Math.Max(amount, 0m), Math.Max(-amount, 0m),
            result.Currency, number, reference, tag));
        // Item lineage lets later AP invoices clear the original accrual even after master changes.
        result.Lines[^1].SourceDocumentLineId = itemId;
        result.Sources[number] = source;
        result.LineIds[number] = lineId ?? StableLineId(itemId, tag, account);
    }

    private static string Purpose(string? tag) => tag switch
    {
        "INV-RECEIPT-CONTROL" => "Inventory", "INV-RECEIPT-GRV-ACCRUAL" => "Accrued purchases",
        "INV-RECEIPT-PRICE-VARIANCE" => "Purchase price variance", _ => "Receipt posting"
    };
    private sealed record ReceiptValue(Guid ItemId, decimal Inventory, decimal Variance);
    internal sealed record ReceiptPostingDistribution(string Currency, string Basis)
    {
        public List<FinancePostingLineDto> Lines { get; } = new();
        public Dictionary<int, string> Sources { get; } = new();
        public Dictionary<int, Guid> LineIds { get; } = new();
        public bool NeedsReview { get; set; }
    }
}
