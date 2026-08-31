using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Finance.Integration;

namespace ErpSystem.Core.Interfaces.Finance;

public sealed record FinanceSourceDocumentLineContext(Guid SourceLineId, Guid AccountId);

/// <summary>
/// Shared Finance-owned orchestration boundary used by certified source routes.  Producer
/// lifecycles supply their authoritative document/line identities and accounts; this service
/// owns canonical resolution, fixed-rule application, provenance and frozen evidence.
/// </summary>
public interface IFinanceSourceDimensionService
{
    Task<FinanceSourceDocumentDimensionDto> SynchronizeDraftAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        DateTime documentDate,
        IReadOnlyList<FinanceSourceDocumentLineContext> authoritativeLines,
        FinanceSourceDocumentDimensionInputDto? input,
        bool inheritDefaultForUnassignedLines,
        string? budgetReservationSourceDocumentType,
        string reason,
        CancellationToken cancellationToken = default);

    Task<FinanceSourceDocumentDimensionDto> GetAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        DateTime documentDate,
        IReadOnlyList<FinanceSourceDocumentLineContext> authoritativeLines,
        CancellationToken cancellationToken = default);

    Task<FinanceSourceDocumentDimensionDto> ValidateAndFreezeAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        DateTime documentDate,
        IReadOnlyList<FinanceSourceDocumentLineContext> authoritativeLines,
        bool requireCurrentBudgetEvidence,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<FinancePostingDimensionValueDto>>> GetPostingDimensionsAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default);

    Task MarkBudgetEvidenceCurrentAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        string evaluationHash,
        CancellationToken cancellationToken = default);

    Task InvalidateBudgetEvidenceAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        string budgetReservationSourceDocumentType,
        bool requiresReevaluation,
        string reason,
        CancellationToken cancellationToken = default);
}
