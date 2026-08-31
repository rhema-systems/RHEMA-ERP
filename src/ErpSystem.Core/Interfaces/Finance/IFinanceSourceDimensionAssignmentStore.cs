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
    /// <summary>
    /// Registers trusted provenance even when a CaptureOptional document has no default or line
    /// assignments.  The resulting null header assignment is also the persisted nullable default.
    /// </summary>
    Task<FinanceSourceDimensionAssignmentDto> RegisterDocumentAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinanceSourceDimensionAssignmentDto>> GetDocumentAssignmentsAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default);

    Task<FinanceSourceDimensionAssignmentDto> UpsertAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        Guid? sourceLineId,
        Guid? financeDimensionSetId,
        Guid? financeDimensionSnapshotId = null,
        CancellationToken cancellationToken = default);

    Task<FinanceSourceDimensionAssignmentDto> FreezeLineAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        Guid sourceLineId,
        Guid? financeDimensionSetId,
        Guid? financeDimensionSnapshotId,
        CancellationToken cancellationToken = default);

    Task ClearAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        Guid? sourceLineId,
        CancellationToken cancellationToken = default);

    Task RemoveLineAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        Guid sourceLineId,
        CancellationToken cancellationToken = default);
}
