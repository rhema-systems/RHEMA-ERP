using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance;

public sealed class InventoryAdjustmentFinancePostingService : IInventoryAdjustmentFinancePostingService
{
    private readonly ApplicationDbContext _db;
    private readonly IFinancePostingEngine _posting;
    private readonly IFinanceSourceDimensionService _dimensions;
    private readonly IStockAdjustmentValuationIntentBuilder _valuationIntent;

    public InventoryAdjustmentFinancePostingService(
        ApplicationDbContext db,
        IFinancePostingEngine posting,
        IFinanceSourceDimensionService dimensions,
        IStockAdjustmentValuationIntentBuilder valuationIntent)
    {
        _db = db;
        _posting = posting;
        _dimensions = dimensions;
        _valuationIntent = valuationIntent;
    }

    public async Task<InventoryAdjustmentFinancePostingResult> PostAsync(StockAdjustment adjustment, CancellationToken cancellationToken = default)
    {
        var intent = await _valuationIntent.BuildAsync(adjustment, cancellationToken: cancellationToken);
        var posting = intent.PostingRequest;
        var lines = posting.Lines;
        var producer = Producer();
        await ApplyDimensionsAsync(producer, adjustment.Id, adjustment.AdjustmentDate, lines, cancellationToken);
        var result = await _posting.PostAsync(ToLegacyPosting(posting,
            posting.PostingAction == "PostOpeningStock" ? adjustment.BookClassification : "IFRS"), producer, cancellationToken);
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
        var result = await _posting.PostAsync(new FinancePostingRequestV2Dto
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
            AccountingBookCode = adjustment.BookClassification,
            FunctionalCurrencyCode = string.IsNullOrWhiteSpace(settings.BaseCurrency) ? "GHS" : settings.BaseCurrency.Trim().ToUpperInvariant(),
            IdempotencyKey = $"StockAdjustment:{adjustment.TenantId:N}:{adjustment.Id:N}:Reverse",
            Lines = plan.ReversalLines
        }, Producer(), cancellationToken);
        return new(result.PostingEventId, result.JournalEntryId);
    }

    private static FinancePostingRequestV2Dto ToLegacyPosting(ProducerFinancePostingRequestDto source, string accountingBookCode) => new()
    {
        SourceModule = source.SourceModule,
        OriginModuleCode = source.OriginModuleCode,
        SourceDocumentType = source.SourceDocumentType,
        SourceDocumentId = source.SourceDocumentId,
        SourceDocumentTenantId = source.SourceDocumentTenantId,
        PostingAction = source.PostingAction,
        SourceDocumentReference = source.SourceDocumentReference,
        Description = source.Description,
        PostingDate = source.PostingDate,
        JournalType = source.JournalType,
        AccountingBookCode = accountingBookCode,
        FunctionalCurrencyCode = source.FunctionalCurrencyCode,
        IdempotencyKey = source.IdempotencyKey,
        ReturnExistingOnDuplicate = source.ReturnExistingOnDuplicate,
        Lines = source.Lines
    };

    private async Task ApplyDimensionsAsync(
        FinancePostingProducerContext producer,
        Guid documentId,
        DateTime documentDate,
        IReadOnlyList<FinancePostingLineDto> lines,
        CancellationToken cancellationToken)
    {
        var contexts = lines.Select(line => new FinanceSourceDocumentLineContext(
            line.SourceDocumentLineId!.Value, line.AccountId)).ToArray();
        if (!await HasCompleteFrozenEvidenceAsync(producer, documentId, contexts, cancellationToken))
            await _dimensions.SynchronizeDraftAsync(producer, documentId, documentDate, contexts, null, false, null,
                "Inventory stock-adjustment Finance adapter capture", cancellationToken);
        await _dimensions.ValidateAndFreezeAsync(producer, documentId, documentDate, contexts, false, cancellationToken);
        foreach (var line in lines)
            line.Dimensions = await _dimensions.ResolvePostingDimensionsAsync(
                producer, documentId, line.SourceDocumentLineId!.Value, line.AccountId, documentDate, cancellationToken);
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

    private static FinancePostingProducerContext Producer() =>
        FinanceExternalProducerContractCatalog.GetRequired(FinanceExternalProducerContractId.InventoryStockAdjustment);
}
