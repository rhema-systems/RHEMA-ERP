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

    /// <summary>Returns all reviews assigned to a specific reviewer, with probation/employee details loaded.</summary>
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
