using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Finance.Integration;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Finance-owned adapter between fixed-asset economic components and the generic source-dimension
/// infrastructure. Fixed Assets supplies stable document/line identities and accounts; this
/// boundary owns canonical resolution, fixed rules, frozen evidence and historical reversals.
/// </summary>
public interface IFixedAssetDimensionService
{
    Task<FinanceSourceDocumentDimensionDto> SynchronizeAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        DateTime documentDate,
        IReadOnlyList<FinancePostingLineDto> economicLines,
        FinanceSourceDocumentDimensionInputDto? input,
        IReadOnlyDictionary<Guid, Guid>? inheritedAssetJournalBySourceLine,
        string reason,
        CancellationToken cancellationToken = default);

    Task<FinanceSourceDocumentDimensionDto> GetAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        DateTime documentDate,
        IReadOnlyList<FinancePostingLineDto> economicLines,
        CancellationToken cancellationToken = default);

    Task<FinanceSourceDocumentDimensionDto> ValidateFreezeAndApplyAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        DateTime documentDate,
        IList<FinancePostingLineDto> economicLines,
        CancellationToken cancellationToken = default);

    Task RegisterHistoricalReversalAsync(
        FinancePostingProducerContext producer,
        Guid reversalDocumentId,
        Guid originalJournalEntryId,
        IList<FinancePostingLineDto> reversalLines,
        CancellationToken cancellationToken = default);
}
