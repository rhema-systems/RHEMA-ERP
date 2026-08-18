using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// PROBATION PERIOD
// ============================================================================

#region Probation Period

public interface IProbationPeriodRepository : IGenericRepository<ProbationPeriod>
{
    /// <summary>Returns all probation periods for an employee, ordered by start date descending.</summary>
    Task<IEnumerable<ProbationPeriod>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns probation periods filtered by status.</summary>
    Task<IEnumerable<ProbationPeriod>> GetByStatusAsync(ProbationStatus status);

    /// <summary>Returns a fully-loaded probation period including all reviews.</summary>
    Task<ProbationPeriod?> GetWithReviewsAsync(Guid id);

    /// <summary>
    /// A single probation with the navigations its DTO reads (employee, live reviews).
    /// Prefer this over the generic <c>GetByIdAsync</c>, which has no includes and therefore
    /// resolves a blank employee name and a review count of zero.
    /// </summary>
    Task<ProbationPeriod?> GetByIdWithDetailsAsync(Guid id);

    /// <summary>One page of the probation register, active first, then by end date.</summary>
    Task<(IEnumerable<ProbationPeriod> Items, int TotalCount)> GetPagedAsync(
        Guid tenantId, int page, int pageSize, ProbationStatus? status, Guid? employeeId, string? search);

    /// <summary>Returns all currently active (in-progress) probation periods.</summary>
    Task<IEnumerable<ProbationPeriod>> GetActiveProbationsAsync();

    /// <summary>Returns probation periods whose end date falls within the given number of days from today.</summary>
    Task<IEnumerable<ProbationPeriod>> GetEndingWithinAsync(int daysAhead);
}

#endregion

// ============================================================================
// PROBATION REVIEW
// ============================================================================

#region Probation Review

public interface IProbationReviewRepository : IGenericRepository<ProbationReview>
{
    /// <summary>Returns all reviews for a probation period, ordered by scheduled date.</summary>
    Task<IEnumerable<ProbationReview>> GetByProbationPeriodIdAsync(Guid probationPeriodId);

    /// <summary>Returns reviews filtered by status.</summary>
    Task<IEnumerable<ProbationReview>> GetByStatusAsync(ProbationReviewStatus status);

    /// <summary>Returns reviews whose scheduled date has passed and are not yet completed.</summary>
    Task<IEnumerable<ProbationReview>> GetOverdueReviewsAsync();

    /// <summary>
    /// Every review this employee has to conduct - as the named reviewer <b>or</b> as the second
    /// reviewer. Matching only <c>ReviewedById</c> hid a second reviewer own work from them, which
    /// is the one screen that exists to show it.
    /// </summary>
    Task<IEnumerable<ProbationReview>> GetByReviewerIdAsync(Guid reviewerEmployeeId);
}

#endregion

// ============================================================================
// PROBATION EXTENSION
// ============================================================================

#region Probation Extension

public interface IProbationExtensionRepository : IGenericRepository<ProbationExtension>
{
    /// <summary>Returns all extensions for a probation period, ordered by ExtendedDate ascending.</summary>
    Task<IEnumerable<ProbationExtension>> GetByProbationIdAsync(Guid probationPeriodId);
}

#endregion
