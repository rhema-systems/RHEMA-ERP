using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance;

public sealed class InventoryAdjustmentFinancePostingService : IInventoryAdjustmentFinancePostingService
{
    private readonly ApplicationDbContext _db;
    private readonly IFinancePostingEngine _posting;

    public InventoryAdjustmentFinancePostingService(ApplicationDbContext db, IFinancePostingEngine posting)
    {
        _db = db;
        _posting = posting;
    }

    public async Task<InventoryAdjustmentFinancePostingResult> PostAsync(StockAdjustment adjustment, CancellationToken cancellationToken = default)
    {
        var isOpeningStock = adjustment.ReasonCode == StockAdjustmentReasonCodes.InitialStock;
        var eligibleOpeningState = adjustment.ApprovalRequired
            ? adjustment.Status == "Approved" && adjustment.ApprovedById.HasValue && adjustment.ApprovedAt.HasValue
            : adjustment.Status == "ReadyToPost" && !adjustment.ApprovedById.HasValue && !adjustment.ApprovedAt.HasValue;
        if (isOpeningStock &&
            (!eligibleOpeningState ||
             string.IsNullOrWhiteSpace(adjustment.PayloadHash) || string.IsNullOrWhiteSpace(adjustment.IntegrityHash)))
            throw new InvalidOperationException(
                "Finance requires immutable INITIAL_STOCK evidence that is approved, or ready to post with approval recorded as not required.");
        var settings = await _db.FinanceSettings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == adjustment.TenantId && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Finance settings are not configured for this tenant.");
        var inventory = settings.ControlAccountInventoryId
            ?? throw new InvalidOperationException("Inventory Control Account is not configured in Finance Settings.");
        // Inventory owns INITIAL_STOCK plus every warehouse/location/item/quantity/unit-cost
        // lifecycle mutation. Finance consumes only its approved immutable evidence, creates no
        // Inventory master data, and derives the configured control/clearing accounts. The ordinary
        // stock-adjustment expense/recovery path below is intentionally unchanged.
        Guid? migrationClearing = isOpeningStock
            ? settings.MigrationClearingAccountId
                ?? throw new InvalidOperationException("Migration Clearing Account is not configured in Finance Settings.")
            : null;
        Guid? expense = isOpeningStock ? null : settings.WriteOffExpenseAccountId;
        Guid? recovery = isOpeningStock ? null : settings.WriteOffRecoveryAccountId;
        var currency = string.IsNullOrWhiteSpace(settings.BaseCurrency) ? "GHS" : settings.BaseCurrency.Trim().ToUpperInvariant();
        var lines = new List<FinancePostingLineDto>();
        var number = 1;
        foreach (var item in adjustment.Items.Where(x => !x.IsDeleted).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id))
        {
            var amount = decimal.Round(Math.Abs(item.AdjustmentValue), 2);
            if (amount <= 0) continue;
            var description = $"Stock adjustment {adjustment.AdjustmentNumber} - {item.InventoryItem?.Name ?? item.InventoryItemId.ToString()}";
            if (isOpeningStock)
            {
                if (item.AdjustmentQuantity <= 0 || item.UnitCost <= 0 || item.AdjustmentValue <= 0)
                    throw new InvalidOperationException("Opening stock can post only positive quantity and unit-cost evidence.");
                lines.Add(Line(inventory, description, amount, 0m, currency, number++, adjustment.AdjustmentNumber,
                    "INV-OPEN-CONTROL", adjustment.AdjustmentDate));
                lines.Add(Line(migrationClearing!.Value, description, 0m, amount, currency, number++, adjustment.AdjustmentNumber,
                    "INV-OPEN-MIGRATION", adjustment.AdjustmentDate));
            }
            else if (item.AdjustmentQuantity < 0)
            {
                var expenseAccount = expense
                    ?? throw new InvalidOperationException("Write-off Expense Account is not configured in Finance Settings.");
                lines.Add(Line(expenseAccount, description, amount, 0m, currency, number++, adjustment.AdjustmentNumber,
                    "INV-ADJ-EXPENSE", adjustment.AdjustmentDate));
                lines.Add(Line(inventory, description, 0m, amount, currency, number++, adjustment.AdjustmentNumber,
                    "INV-ADJ-CONTROL", adjustment.AdjustmentDate));
            }
            else
            {
                var recoveryAccount = recovery
                    ?? throw new InvalidOperationException("Write-off Recovery Account is not configured in Finance Settings.");
                lines.Add(Line(inventory, description, amount, 0m, currency, number++, adjustment.AdjustmentNumber,
                    "INV-ADJ-CONTROL", adjustment.AdjustmentDate));
                lines.Add(Line(recoveryAccount, description, 0m, amount, currency, number++, adjustment.AdjustmentNumber,
                    "INV-ADJ-RECOVERY", adjustment.AdjustmentDate));
            }
        }
        if (lines.Count == 0) throw new InvalidOperationException("The stock adjustment has no non-zero value to post to Finance.");
        var result = await _posting.PostAsync(new FinancePostingRequestDto
        {
            SourceModule = "Inventory",
            OriginModuleCode = FinanceModuleLockCatalog.Inventory,
            SourceDocumentType = "StockAdjustment",
            SourceDocumentId = adjustment.Id,
            SourceDocumentTenantId = adjustment.TenantId,
            PostingAction = isOpeningStock ? "PostOpeningStock" : "PostStockAdjustment",
            SourceDocumentReference = adjustment.AdjustmentNumber,
            Description = isOpeningStock
                ? $"Inventory opening stock {adjustment.AdjustmentNumber} - source schedule {adjustment.Reference}"
                : $"Inventory stock adjustment {adjustment.AdjustmentNumber} - {adjustment.ReasonCode}",
            PostingDate = adjustment.AdjustmentDate,
            JournalType = "System Generated",
            BookClassification = isOpeningStock ? adjustment.BookClassification : "IFRS",
            FunctionalCurrencyCode = currency,
            IdempotencyKey = $"StockAdjustment:{adjustment.TenantId:N}:{adjustment.Id:N}:Post",
            Lines = lines
        }, cancellationToken);
        return new(result.PostingEventId, result.JournalEntryId);
    }

    public async Task<InventoryAdjustmentFinancePostingResult> ReverseAsync(StockAdjustment adjustment, string reason, CancellationToken cancellationToken = default)
    {
        if (!adjustment.FinancePostingEventId.HasValue)
            throw new InvalidOperationException("The stock adjustment does not have a Finance posting to reverse.");
        var settings = await _db.FinanceSettings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == adjustment.TenantId && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Finance settings are not configured for this tenant.");
        var plan = await _posting.GetReversalPlanAsync(adjustment.FinancePostingEventId.Value, reason, DateTime.UtcNow, cancellationToken);
        if (!plan.IsDefined || plan.ReversalLines.Count == 0)
            throw new InvalidOperationException("Finance could not derive a balanced reversal for the stock adjustment.");
        var result = await _posting.PostAsync(new FinancePostingRequestDto
        {
            SourceModule = "Inventory",
            OriginModuleCode = FinanceModuleLockCatalog.Inventory,
            SourceDocumentType = "StockAdjustment",
            SourceDocumentId = adjustment.Id,
            SourceDocumentTenantId = adjustment.TenantId,
            ReversalOfJournalEntryId = plan.OriginalJournalEntryId,
            ReversalReason = reason,
            ReversalType = "Full",
            PostingAction = "ReverseStockAdjustment",
            SourceDocumentReference = adjustment.AdjustmentNumber,
            Description = $"Reversal of inventory stock adjustment {adjustment.AdjustmentNumber}",
            PostingDate = plan.ReversalDate,
            JournalType = "System Generated",
            BookClassification = adjustment.BookClassification,
            FunctionalCurrencyCode = string.IsNullOrWhiteSpace(settings.BaseCurrency) ? "GHS" : settings.BaseCurrency.Trim().ToUpperInvariant(),
            IdempotencyKey = $"StockAdjustment:{adjustment.TenantId:N}:{adjustment.Id:N}:Reverse",
            Lines = plan.ReversalLines
        }, cancellationToken);
        return new(result.PostingEventId, result.JournalEntryId);
    }

    private static FinancePostingLineDto Line(Guid accountId, string description, decimal debit, decimal credit,
        string currency, int lineNumber, string reference, string tag, DateTime exchangeRateDate) => new()
    {
        AccountId = accountId,
        Description = description,
        DebitAmount = debit,
        CreditAmount = credit,
        TransactionCurrency = currency,
        TransactionDebitAmount = debit,
        TransactionCreditAmount = credit,
        ExchangeRate = 1m,
        ExchangeRateSource = "Functional currency",
        ExchangeRateDate = exchangeRateDate,
        SourceReferenceNumber = reference,
        LineNumber = lineNumber,
        TransactionTag = tag
    };
}
