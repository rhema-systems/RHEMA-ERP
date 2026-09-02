using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance;

public sealed class InventoryReceiptFinancePostingService : IInventoryReceiptFinancePostingService
{
    private readonly ApplicationDbContext _db;
    private readonly IFinancePostingEngine _posting;
    private readonly IFinanceSourceDimensionService _dimensions;

    public InventoryReceiptFinancePostingService(
        ApplicationDbContext db,
        IFinancePostingEngine posting,
        IFinanceSourceDimensionService dimensions)
    {
        _db = db;
        _posting = posting;
        _dimensions = dimensions;
    }

    public async Task<InventoryFinancePostingResult> PostAcceptedReceiptAsync(
        Guid purchaseOrderReceiptId,
        CancellationToken cancellationToken = default)
    {
        var receipt = await _db.PurchaseOrderReceipts.AsNoTracking()
            .Include(value => value.PurchaseOrder)
            .SingleOrDefaultAsync(value => value.Id == purchaseOrderReceiptId && !value.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("The governed purchase-order receipt was not found for Finance posting.");
        var settings = await _db.FinanceSettings.AsNoTracking()
            .SingleOrDefaultAsync(value => value.TenantId == receipt.TenantId && !value.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Finance settings are not configured for the receipt tenant.");
        var inventoryAccount = settings.ControlAccountInventoryId
            ?? throw new InvalidOperationException("Inventory Control Account is not configured in Finance Settings.");
        var accrualAccount = settings.ControlAccountGRVAccrualId
            ?? throw new InvalidOperationException("GRV Accrual Control Account is not configured in Finance Settings.");
        var movements = await _db.InventoryMovements.AsNoTracking()
            .Where(value => value.TenantId == receipt.TenantId && value.ReferenceId == receipt.Id &&
                            value.MovementType == InventoryMovementType.PurchaseReceipt && value.IsPosted &&
                            !value.IsDeleted)
            .OrderBy(value => value.PostingDate).ThenBy(value => value.Id)
            .ToListAsync(cancellationToken);
        if (movements.Count == 0)
            throw new InvalidOperationException("The accepted receipt has no posted inventory valuation movements.");

        var inventoryValue = Round(movements.Sum(value => value.TotalValue));
        var purchasePriceVariance = Round(movements.Sum(value => value.VarianceAmount ?? 0m));
        var accrualValue = Round(inventoryValue + purchasePriceVariance);
        if (inventoryValue <= 0m || accrualValue <= 0m)
            throw new InvalidOperationException("The accepted receipt valuation must be positive before Finance posting.");

        var currency = Currency(settings.BaseCurrency);
        var lines = new List<FinancePostingLineDto>
        {
            Line(inventoryAccount, $"Accepted stock {receipt.ReceiptNumber}", inventoryValue, 0m,
                currency, 1, receipt.ReceiptNumber, "INV-RECEIPT-CONTROL", SourceLine(receipt.Id, "inventory-control"))
        };
        var lineNumber = 2;
        if (purchasePriceVariance != 0m)
        {
            var varianceAccount = settings.WriteOffExpenseAccountId
                ?? throw new InvalidOperationException(
                    "Write-off Expense Account is required as the configured purchase-price variance account.");
            lines.Add(purchasePriceVariance > 0m
                ? Line(varianceAccount, $"Purchase price variance {receipt.ReceiptNumber}", purchasePriceVariance,
                    0m, currency, lineNumber++, receipt.ReceiptNumber, "INV-RECEIPT-PRICE-VARIANCE", SourceLine(receipt.Id, "price-variance"))
                : Line(varianceAccount, $"Purchase price variance {receipt.ReceiptNumber}", 0m,
                    Math.Abs(purchasePriceVariance), currency, lineNumber++, receipt.ReceiptNumber,
                    "INV-RECEIPT-PRICE-VARIANCE", SourceLine(receipt.Id, "price-variance")));
        }
        lines.Add(Line(accrualAccount, $"GRV accrual {receipt.ReceiptNumber}", 0m, accrualValue,
            currency, lineNumber, receipt.ReceiptNumber, "INV-RECEIPT-GRV-ACCRUAL", SourceLine(receipt.Id, "grv-accrual")));

        var producer = Producer();
        await ApplyDimensionsAsync(producer, receipt.Id, receipt.ReceiptDate, lines, cancellationToken);
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
            FunctionalCurrencyCode = currency,
            IdempotencyKey = $"ProcurementPurchaseOrderReceipt:{receipt.TenantId:N}:{receipt.Id:N}:AcceptedInventory",
            Lines = lines
        }, producer, cancellationToken);
        return new InventoryFinancePostingResult(result.PostingEventId, result.JournalEntryId, result.WasDuplicate);
    }

    internal static FinancePostingLineDto Line(Guid accountId, string description, decimal debit, decimal credit,
        string currency, int number, string reference, string tag, Guid sourceLineId) => new()
    {
        AccountId = accountId,
        SourceDocumentLineId = sourceLineId,
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

    private async Task ApplyDimensionsAsync(
        FinancePostingProducerContext producer, Guid documentId, DateTime date,
        IReadOnlyList<FinancePostingLineDto> lines, CancellationToken cancellationToken)
    {
        var contexts = lines.Select(line => new FinanceSourceDocumentLineContext(line.SourceDocumentLineId!.Value, line.AccountId)).ToArray();
        if (!await HasCompleteFrozenEvidenceAsync(producer, documentId, contexts, cancellationToken))
            await _dimensions.SynchronizeDraftAsync(producer, documentId, date, contexts, null, false, null,
                "Accepted inventory receipt Finance adapter capture", cancellationToken);
        await _dimensions.ValidateAndFreezeAsync(producer, documentId, date, contexts, false, cancellationToken);
        foreach (var line in lines)
            line.Dimensions = await _dimensions.ResolvePostingDimensionsAsync(
                producer, documentId, line.SourceDocumentLineId!.Value, line.AccountId, date, cancellationToken);
    }

    private async Task<bool> HasCompleteFrozenEvidenceAsync(
        FinancePostingProducerContext producer, Guid documentId,
        IReadOnlyList<FinanceSourceDocumentLineContext> lines, CancellationToken cancellationToken)
    {
        var expected = lines.Select(line => line.SourceLineId).ToHashSet();
        var frozen = await _db.FinanceSourceDimensionAssignments.AsNoTracking()
            .Where(item => item.RouteId == producer.RouteId && item.SourceDocumentId == documentId
                && item.SourceLineId.HasValue && item.EvidenceFrozenAt.HasValue && !item.IsDeleted)
            .Select(item => item.SourceLineId!.Value).ToListAsync(cancellationToken);
        return frozen.ToHashSet().SetEquals(expected);
    }

    private static Guid SourceLine(Guid documentId, string kind) => FinanceExternalDimensionIdentity.SourceLine(
        FinanceExternalProducerContractId.ProcurementAcceptedInventoryReceipt, documentId, kind);

    private static FinancePostingProducerContext Producer() => FinanceExternalProducerContractCatalog.GetRequired(
        FinanceExternalProducerContractId.ProcurementAcceptedInventoryReceipt);
}

public sealed class InventoryLandedCostFinancePostingService : IInventoryLandedCostFinancePostingService
{
    private readonly ApplicationDbContext _db;
    private readonly IFinancePostingEngine _posting;
    private readonly IFinanceSourceDimensionService _dimensions;

    public InventoryLandedCostFinancePostingService(
        ApplicationDbContext db,
        IFinancePostingEngine posting,
        IFinanceSourceDimensionService dimensions)
    {
        _db = db;
        _posting = posting;
        _dimensions = dimensions;
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
        var inventoryAccount = settings.ControlAccountInventoryId
            ?? throw new InvalidOperationException("Inventory Control Account is not configured in Finance Settings.");
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
        AddSigned(lines, inventoryAccount, $"Landed cost inventory {landedCost.LandedCostNumber}", inventoryValue,
            currency, ref number, landedCost.LandedCostNumber, "INV-LANDED-COST-CONTROL", SourceLine(landedCost.Id, "inventory-control"));
        if (varianceValue != 0m)
        {
            var varianceAccount = settings.WriteOffExpenseAccountId
                ?? throw new InvalidOperationException(
                    "Write-off Expense Account is required as the configured landed-cost variance account.");
            AddSigned(lines, varianceAccount, $"Landed cost variance {landedCost.LandedCostNumber}", varianceValue,
                currency, ref number, landedCost.LandedCostNumber, "INV-LANDED-COST-VARIANCE", SourceLine(landedCost.Id, "variance"));
        }
        AddSigned(lines, accrualAccount, $"Landed cost accrual {landedCost.LandedCostNumber}", -postedValue,
            currency, ref number, landedCost.LandedCostNumber, "INV-LANDED-COST-ACCRUAL", SourceLine(landedCost.Id, "accrual"));

        var producer = Producer();
        await ApplyDimensionsAsync(producer, landedCost.Id, landedCost.PostedDate ?? landedCost.CostDate, lines, cancellationToken);
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
        }, producer, cancellationToken);
        return new InventoryFinancePostingResult(result.PostingEventId, result.JournalEntryId, result.WasDuplicate);
    }

    private static void AddSigned(List<FinancePostingLineDto> lines, Guid accountId, string description,
        decimal signedDebit, string currency, ref int number, string reference, string tag, Guid sourceLineId)
    {
        if (signedDebit == 0m) return;
        lines.Add(InventoryReceiptFinancePostingService.Line(accountId, description,
            signedDebit > 0m ? signedDebit : 0m,
            signedDebit < 0m ? Math.Abs(signedDebit) : 0m,
            currency, number++, reference, tag, sourceLineId));
    }

    private async Task ApplyDimensionsAsync(
        FinancePostingProducerContext producer, Guid documentId, DateTime date,
        IReadOnlyList<FinancePostingLineDto> lines, CancellationToken cancellationToken)
    {
        var contexts = lines.Select(line => new FinanceSourceDocumentLineContext(line.SourceDocumentLineId!.Value, line.AccountId)).ToArray();
        if (!await HasCompleteFrozenEvidenceAsync(producer, documentId, contexts, cancellationToken))
            await _dimensions.SynchronizeDraftAsync(producer, documentId, date, contexts, null, false, null,
                "Inventory landed-cost Finance adapter capture", cancellationToken);
        await _dimensions.ValidateAndFreezeAsync(producer, documentId, date, contexts, false, cancellationToken);
        foreach (var line in lines)
            line.Dimensions = await _dimensions.ResolvePostingDimensionsAsync(
                producer, documentId, line.SourceDocumentLineId!.Value, line.AccountId, date, cancellationToken);
    }

    private async Task<bool> HasCompleteFrozenEvidenceAsync(
        FinancePostingProducerContext producer, Guid documentId,
        IReadOnlyList<FinanceSourceDocumentLineContext> lines, CancellationToken cancellationToken)
    {
        var expected = lines.Select(line => line.SourceLineId).ToHashSet();
        var frozen = await _db.FinanceSourceDimensionAssignments.AsNoTracking()
            .Where(item => item.RouteId == producer.RouteId && item.SourceDocumentId == documentId
                && item.SourceLineId.HasValue && item.EvidenceFrozenAt.HasValue && !item.IsDeleted)
            .Select(item => item.SourceLineId!.Value).ToListAsync(cancellationToken);
        return frozen.ToHashSet().SetEquals(expected);
    }

    private static Guid SourceLine(Guid documentId, string kind) => FinanceExternalDimensionIdentity.SourceLine(
        FinanceExternalProducerContractId.InventoryLandedCost, documentId, kind);

    private static FinancePostingProducerContext Producer() => FinanceExternalProducerContractCatalog.GetRequired(
        FinanceExternalProducerContractId.InventoryLandedCost);
}
