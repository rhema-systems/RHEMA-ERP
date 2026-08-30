using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Finance.Integration;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Shared Finance persistence contract for dimension-capable producer adapters. PR 2 and later
/// producer integrations use this store for document defaults and line assignments; they must not
/// introduce bespoke per-module dimension-set columns.
/// </summary>
public interface IFinanceSourceDimensionAssignmentStore
{
    Task<IReadOnlyList<FinanceSourceDimensionAssignmentDto>> GetDocumentAssignmentsAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default);

    Task<FinanceSourceDimensionAssignmentDto> UpsertAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        Guid? sourceLineId,
        Guid financeDimensionSetId,
        Guid? financeDimensionSnapshotId = null,
        CancellationToken cancellationToken = default);

    Task ClearAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        Guid? sourceLineId,
        CancellationToken cancellationToken = default);
}
