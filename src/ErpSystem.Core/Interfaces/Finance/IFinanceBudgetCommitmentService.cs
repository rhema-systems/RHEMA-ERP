using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Canonical Finance boundary for non-Finance modules that consume an adopted expense
/// budget. Producer modules retain their workflow and document evidence; Finance owns the
/// budget cell, availability calculation, reservation state and GL-derived actuals.
/// </summary>
public interface IFinanceBudgetCommitmentService
{
    Task<IReadOnlyList<FinanceBudgetCellDto>> GetEligibleBudgetCellsAsync(
        FinanceBudgetCellQueryDto query,
        CancellationToken cancellationToken = default);

    Task<FinanceBudgetPositionDto> GetBudgetPositionAsync(
        Guid budgetEntryId,
        DateTime asOfDate,
        CancellationToken cancellationToken = default);

    Task<FinanceBudgetCommitmentEvaluationDto> EvaluateAsync(
        FinanceBudgetCommitmentRequestDto request,
        CancellationToken cancellationToken = default);

    Task<FinanceBudgetCommitmentResultDto> ReserveAsync(
        FinanceBudgetCommitmentRequestDto request,
        CancellationToken cancellationToken = default);

    Task<FinanceBudgetReservationDto> SetReservationAmountAsync(
        Guid reservationId,
        SetFinanceBudgetReservationAmountDto request,
        CancellationToken cancellationToken = default);

    Task<FinanceBudgetReservationDto> ReleaseAsync(
        Guid reservationId,
        ReleaseFinanceBudgetReservationDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finance-internal posting adapter boundary. Actuals are never written here; they are
    /// derived from the linked posted journal. This operation only reduces or consumes the
    /// remaining commitment after a successful posting event. The Finance adapter must first
    /// validate the authoritative relationship between the reservation source (for example a
    /// requisition) and the posted source (for example an accepted receipt).
    /// </summary>
    Task<FinanceBudgetReservationDto> ApplyPostingOutcomeAsync(
        Guid reservationId,
        ApplyFinanceBudgetPostingOutcomeDto request,
        CancellationToken cancellationToken = default);
}

public sealed class FinanceBudgetCommitmentValidationException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class FinanceBudgetCommitmentConflictException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class FinanceBudgetCommitmentNotFoundException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
