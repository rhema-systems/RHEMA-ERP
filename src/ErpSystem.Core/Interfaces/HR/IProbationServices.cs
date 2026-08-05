using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

public interface IProbationService
{
    // Queries
    Task<ProbationPeriodDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProbationPeriodDto?> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<ProbationPeriodDetailDto> GetWithReviewsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProbationPeriodSummaryDto>> GetByStatusAsync(ProbationStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProbationPeriodSummaryDto>> GetActiveProbationsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<ProbationPeriodSummaryDto>> GetEndingWithinAsync(int daysAhead = 30, CancellationToken cancellationToken = default);

    // CRUD
    Task<ProbationPeriodDto> CreateAsync(CreateProbationPeriodDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Workflow
    Task<bool> ExtendAsync(Guid probationId, DateTime newEndDate, string reason, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> ConfirmAsync(Guid probationId, Guid confirmedByUserId, CancellationToken cancellationToken = default);
    Task<bool> TerminateAsync(TerminateProbationPeriodDto dto, Guid terminatedByUserId, CancellationToken cancellationToken = default);

    // Extension audit trail
    /// <summary>
    /// Extends the probation period, records a ProbationExtension audit entry,
    /// and increments ProbationPeriod.ExtensionCount — all in one transaction.
    /// Use this instead of ExtendAsync when a structured extension record is needed.
    /// </summary>
    Task<ProbationExtensionDto> RecordExtensionAsync(CreateProbationExtensionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProbationExtensionDto>> GetExtensionsAsync(Guid probationId, CancellationToken cancellationToken = default);

    // Reviews
    Task<ProbationReviewDto> AddReviewAsync(CreateProbationReviewDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProbationReviewDto>> GetReviewsAsync(Guid probationId, CancellationToken cancellationToken = default);
    Task<ProbationReviewDto> UpdateReviewAsync(UpdateProbationReviewDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> CompleteReviewAsync(Guid reviewId, Guid completedByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProbationReviewDto>> GetReviewsByStatusAsync(ProbationReviewStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProbationReviewDto>> GetOverdueReviewsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<ProbationReviewDto>> GetReviewsByReviewerAsync(Guid reviewerEmployeeId, CancellationToken cancellationToken = default);
}
