using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance;

public sealed class InventoryReceiptFinancePostingService : IInventoryReceiptFinancePostingService
{
    private readonly ApplicationDbContext _db;
    private readonly IFinancePostingEngine _posting;

    public InventoryReceiptFinancePostingService(ApplicationDbContext db, IFinancePostingEngine posting)
    {
        _db = db;
        _posting = posting;
    }

    public async Task<InventoryFinancePostingResult> PostAcceptedReceiptAsync(
        Guid purchaseOrderReceiptId,
        CancellationToken cancellationToken = default)
    {
        if (!_db.Database.IsRelational() || _db.Database.CurrentTransaction is not null)
            return await PostAcceptedReceiptCoreAsync(purchaseOrderReceiptId, cancellationToken);
        return await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
            var result = await PostAcceptedReceiptCoreAsync(purchaseOrderReceiptId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private async Task<InventoryFinancePostingResult> PostAcceptedReceiptCoreAsync(
        Guid purchaseOrderReceiptId, CancellationToken cancellationToken)
    {
        var receipt = await _db.PurchaseOrderReceipts.AsNoTracking()
            .Include(value => value.PurchaseOrder).ThenInclude(value => value.BusinessPartner)
            .SingleOrDefaultAsync(value => value.Id == purchaseOrderReceiptId && !value.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("The governed purchase-order receipt was not found for Finance posting.");
        var distributions = new ProcurementReceiptDistributionService(_db);
        await distributions.LockReceiptAsync(receipt.TenantId, receipt.Id, cancellationToken);
        // A save may have committed between the first read and the lock acquisition.
        receipt = await distributions.LoadReceiptAsync(receipt.TenantId, receipt.Id, cancellationToken);
        var existing = await _db.FinancePostingEvents.AsNoTracking().Where(value => value.TenantId == receipt.TenantId &&
            !value.IsDeleted && value.SourceDocumentType == "ProcurementPurchaseOrderReceipt" && value.SourceDocumentId == receipt.Id &&
            value.PostingAction == "PostAcceptedInventoryReceipt" && value.PostingStatus == "Posted" && value.JournalEntryId.HasValue)
            .OrderByDescending(value => value.PostedAt).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
            return new InventoryFinancePostingResult(existing.Id, existing.JournalEntryId!.Value, true);
        var distribution = await distributions.BuildAsync(receipt, true, cancellationToken);

        var result = await _posting.PostAsync(new FinancePostingRequestDto
        {
            SourceModule = "Inventory",
            OriginModuleCode = FinanceModuleLockCatalog.Inventory,
            SourceDocumentType = "ProcurementPurchaseOrderReceipt",
            SourceDocumentId = receipt.Id,
            SourceDocumentTenantId = receipt.TenantId,
            PostingAction = "PostAcceptedInventoryReceipt",
            SourceDocumentReference = receipt.ReceiptNumber,
            Description = $"Accepted inventory receipt {receipt.ReceiptNumber}",
            PostingDate = receipt.ReceiptDate,
            JournalType = "System Generated",
            BookClassification = "IFRS",
            FunctionalCurrencyCode = distribution.Currency,
            IdempotencyKey = $"ProcurementPurchaseOrderReceipt:{receipt.TenantId:N}:{receipt.Id:N}:AcceptedInventory",
            Lines = distribution.Lines
        }, cancellationToken);
        return new InventoryFinancePostingResult(result.PostingEventId, result.JournalEntryId, result.WasDuplicate);
    }

    internal static FinancePostingLineDto Line(Guid accountId, string description, decimal debit, decimal credit,
        string currency, int number, string reference, string tag) => new()
    {
        AccountId = accountId,
        Description = description,
        DebitAmount = Round(debit),
        CreditAmount = Round(credit),
        TransactionCurrency = currency,
        TransactionDebitAmount = Round(debit),
        TransactionCreditAmount = Round(credit),
        ExchangeRate = 1m,
        ExchangeRateSource = "Functional currency",
        ExchangeRateDate = DateTime.UtcNow,
        SourceReferenceNumber = reference,
        LineNumber = number,
        TransactionTag = tag
    };

    internal static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    internal static string Currency(string? value) => string.IsNullOrWhiteSpace(value)
        ? "GHS" : value.Trim().ToUpperInvariant();
}

public sealed class InventoryLandedCostFinancePostingService : IInventoryLandedCostFinancePostingService
{
    private readonly ApplicationDbContext _db;
    private readonly IFinancePostingEngine _posting;

    public InventoryLandedCostFinancePostingService(ApplicationDbContext db, IFinancePostingEngine posting)
    {
        _db = db;
        _posting = posting;
    }

    public async Task<InventoryFinancePostingResult> PostLandedCostAsync(
        Guid landedCostId,
        CancellationToken cancellationToken = default)
    {
        var landedCost = await _db.LandedCosts.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == landedCostId && !value.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("The landed-cost document was not found for Finance posting.");
        if (!string.Equals(landedCost.Status, "Posted", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The landed-cost document must be posted to valuation before Finance posting.");
        var settings = await _db.FinanceSettings.AsNoTracking()
            .SingleOrDefaultAsync(value => value.TenantId == landedCost.TenantId && !value.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Finance settings are not configured for the landed-cost tenant.");
        var accrualAccount = settings.ControlAccountGRVAccrualId
            ?? throw new InvalidOperationException("GRV Accrual Control Account is not configured in Finance Settings.");
        var movements = await _db.InventoryMovements.AsNoTracking()
            .Where(value => value.TenantId == landedCost.TenantId && value.ReferenceId == landedCost.Id &&
                            value.MovementType == InventoryMovementType.LandedCostRevaluation && value.IsPosted &&
                            !value.IsDeleted)
            .ToListAsync(cancellationToken);
        if (movements.Count == 0)
            throw new InvalidOperationException("The landed-cost document has no valuation or variance movements.");

        var inventoryValue = InventoryReceiptFinancePostingService.Round(movements.Sum(value => value.TotalValue));
        var varianceValue = InventoryReceiptFinancePostingService.Round(movements.Sum(value => value.VarianceAmount ?? 0m));
        var postedValue = InventoryReceiptFinancePostingService.Round(inventoryValue + varianceValue);
        var documentValue = InventoryReceiptFinancePostingService.Round(landedCost.TotalCost);
        if (Math.Abs(postedValue - documentValue) > 0.01m)
            throw new InvalidOperationException(
                $"Landed-cost valuation {postedValue:0.00} does not reconcile to allocated document value {documentValue:0.00}.");
        if (postedValue == 0m)
            throw new InvalidOperationException("The landed-cost posting value is zero.");

        var currency = InventoryReceiptFinancePostingService.Currency(settings.BaseCurrency);
        var lines = new List<FinancePostingLineDto>();
        var number = 1;
        var itemIds = movements.Select(value => value.InventoryItemId).Distinct().ToArray();
        var profiles = await _db.InventoryItems.AsNoTracking().Where(value => value.TenantId == landedCost.TenantId &&
            itemIds.Contains(value.Id) && !value.IsDeleted).ToDictionaryAsync(value => value.Id, cancellationToken);
        var groups = movements.GroupBy(value => value.InventoryItemId).OrderBy(value => value.Key).ToArray();
        var inventoryAllocations = AllocateSignedValues(groups.Select(group => group.Sum(value => value.TotalValue)).ToArray(), inventoryValue);
        var varianceAllocations = AllocateSignedValues(groups.Select(group => group.Sum(value => value.VarianceAmount ?? 0m)).ToArray(), varianceValue);
        for (var index = 0; index < groups.Length; index++)
        {
            var group = groups[index];
            profiles.TryGetValue(group.Key, out var profile);
            var itemInventory = inventoryAllocations[index];
            var itemVariance = varianceAllocations[index];
            if (itemInventory != 0m)
            {
                var inventoryAccount = await InventoryPostingAccountResolution.ResolveAsync(_db, landedCost.TenantId,
                    profile?.InventoryAccountId, settings.ControlAccountInventoryId, "Inventory", cancellationToken, AccountType.Asset);
                AddSigned(lines, inventoryAccount, $"Landed cost inventory {landedCost.LandedCostNumber}", itemInventory,
                    currency, ref number, landedCost.LandedCostNumber, "INV-LANDED-COST-CONTROL");
            }
            if (itemVariance != 0m)
            {
                var varianceAccount = await InventoryPostingAccountResolution.ResolveAsync(_db, landedCost.TenantId,
                    profile?.PurchasePriceVarianceAccountId ?? profile?.VarianceAccountId, settings.WriteOffExpenseAccountId,
                    "Landed-cost variance", cancellationToken, AccountType.Expense, AccountType.Revenue);
                AddSigned(lines, varianceAccount, $"Landed cost variance {landedCost.LandedCostNumber}", itemVariance,
                    currency, ref number, landedCost.LandedCostNumber, "INV-LANDED-COST-VARIANCE");
            }
        }
        AddSigned(lines, accrualAccount, $"Landed cost accrual {landedCost.LandedCostNumber}", -postedValue,
            currency, ref number, landedCost.LandedCostNumber, "INV-LANDED-COST-ACCRUAL");

        var result = await _posting.PostAsync(new FinancePostingRequestDto
        {
            SourceModule = "Inventory",
            OriginModuleCode = FinanceModuleLockCatalog.Inventory,
            SourceDocumentType = "InventoryLandedCost",
            SourceDocumentId = landedCost.Id,
            SourceDocumentTenantId = landedCost.TenantId,
            PostingAction = "PostLandedCost",
            SourceDocumentReference = landedCost.LandedCostNumber,
            Description = $"Inventory landed cost {landedCost.LandedCostNumber}",
            PostingDate = landedCost.PostedDate ?? landedCost.CostDate,
            JournalType = "System Generated",
            BookClassification = "IFRS",
            FunctionalCurrencyCode = currency,
            IdempotencyKey = $"InventoryLandedCost:{landedCost.TenantId:N}:{landedCost.Id:N}:Post",
            Lines = lines
        }, cancellationToken);
        return new InventoryFinancePostingResult(result.PostingEventId, result.JournalEntryId, result.WasDuplicate);
    }

    internal static decimal[] AllocateSignedValues(IReadOnlyList<decimal> amounts, decimal target)
    {
        if (target != InventoryReceiptFinancePostingService.Round(amounts.Sum()))
            throw new InvalidOperationException("Landed-cost allocation target must equal the rounded signed total.");
        var positive = amounts.Select(value => Math.Max(value, 0m)).ToArray();
        var negative = amounts.Select(value => Math.Max(-value, 0m)).ToArray();
        var rawPositive = positive.Sum(); var rawNegative = negative.Sum();
        var roundedPositive = InventoryReceiptFinancePostingService.Round(rawPositive);
        var roundedNegative = InventoryReceiptFinancePostingService.Round(rawNegative);
        var pools = new[] { (Positive: roundedPositive, Negative: roundedPositive - target),
                (Positive: target + roundedNegative, Negative: roundedNegative) }
            .Where(value => value.Positive >= 0m && value.Negative >= 0m &&
                (rawPositive > 0m || value.Positive == 0m) && (rawNegative > 0m || value.Negative == 0m))
            .OrderBy(value => Math.Abs(value.Positive - rawPositive) + Math.Abs(value.Negative - rawNegative)).First();
        var debits = MonetaryAllocation.Allocate(positive, pools.Positive);
        var credits = MonetaryAllocation.Allocate(negative, pools.Negative);
        return debits.Select((value, index) => value - credits[index]).ToArray();
    }

    private static void AddSigned(List<FinancePostingLineDto> lines, Guid accountId, string description,
        decimal signedDebit, string currency, ref int number, string reference, string tag)
    {
        if (signedDebit == 0m) return;
        lines.Add(InventoryReceiptFinancePostingService.Line(accountId, description,
            signedDebit > 0m ? signedDebit : 0m,
            signedDebit < 0m ? Math.Abs(signedDebit) : 0m,
            currency, number++, reference, tag));
    }
}
