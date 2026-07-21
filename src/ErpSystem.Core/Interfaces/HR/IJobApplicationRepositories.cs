using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// JOB APPLICATION
// ============================================================================

#region Job Application

public interface IJobApplicationRepository : IGenericRepository<JobApplication>
{
    /// <summary>Returns the application matching the unique application number.</summary>
    Task<JobApplication?> GetByApplicationNumberAsync(string applicationNumber);

    /// <summary>
    /// Returns a fully-loaded application including candidate, vacancy, stage histories,
    /// test results, interview slots, communications, and offer/hire links.
    /// </summary>
    Task<JobApplication?> GetWithFullDetailsAsync(Guid id);

    /// <summary>
    /// Lightweight load for job offer creation: returns the application with its vacancy
    /// (EmploymentType, WorkMode) and the position's salary grade, reporting line,
    /// organisation unit, probation/notice periods, and position benefits (with policies).
    /// </summary>
    Task<JobApplication?> GetForOfferSeedingAsync(Guid id);

    /// <summary>Returns all applications for a vacancy, with candidate details loaded.</summary>
    Task<IEnumerable<JobApplication>> GetByVacancyIdAsync(Guid vacancyId);

    /// <summary>
    /// Returns all applications for a vacancy with the same full navigation properties as
    /// <see cref="GetWithFullDetailsAsync"/>, loaded in a single query.
    /// Used by bulk scoring to avoid N+1 round-trips.
    /// </summary>
    Task<IEnumerable<JobApplication>> GetAllWithFullDetailsByVacancyIdAsync(Guid vacancyId);

    /// <summary>Returns all applications by a candidate, with vacancy/position details loaded.</summary>
    Task<IEnumerable<JobApplication>> GetByCandidateIdAsync(Guid candidateId);

    /// <summary>Returns applications filtered by status, optionally scoped to a vacancy.</summary>
    Task<IEnumerable<JobApplication>> GetByStatusAsync(ApplicationStatus status, Guid? vacancyId = null);

    /// <summary>Returns shortlisted applications for a vacancy, ordered by auto-score descending.</summary>
    Task<IEnumerable<JobApplication>> GetShortlistedAsync(Guid? vacancyId = null);

    /// <summary>Returns applications currently in a specific pipeline stage.</summary>
    Task<IEnumerable<JobApplication>> GetByCurrentStageAsync(Guid pipelineStageId);

    /// <summary>Returns the next application number for auto-generation.</summary>
    Task<string> GetNextApplicationNumberAsync();

    /// <summary>
    /// Returns the application whose <see cref="ErpSystem.Core.Entities.HR.Recruitment.JobApplication.ExternalTrackingToken"/>
    /// matches the given token. Returns null when no match is found.
    /// </summary>
    Task<JobApplication?> GetByTrackingTokenAsync(string token);
}

#endregion

// ============================================================================
// JOB APPLICATION STAGE HISTORY
// ============================================================================

#region Job Application Stage History

public interface IJobApplicationStageHistoryRepository : IGenericRepository<JobApplicationStageHistory>
{
    /// <summary>Returns the full stage transition history for an application, ordered by entry date.</summary>
    Task<IEnumerable<JobApplicationStageHistory>> GetByApplicationIdAsync(Guid applicationId);

    /// <summary>Returns the current (active) stage record for an application, or null if none is set.</summary>
    Task<JobApplicationStageHistory?> GetCurrentStageAsync(Guid applicationId);
}

#endregion

// ============================================================================
// JOB APPLICANT TEST RESULT
// ============================================================================

#region Job Applicant Test Result

public interface IJobApplicantTestResultRepository : IGenericRepository<JobApplicantTestResult>
{
    /// <summary>Returns all test results for an application, ordered by test date.</summary>
    Task<IEnumerable<JobApplicantTestResult>> GetByApplicationIdAsync(Guid applicationId);

    /// <summary>Returns all test results across all applications for a vacancy.</summary>
    Task<IEnumerable<JobApplicantTestResult>> GetByVacancyIdAsync(Guid vacancyId);

    /// <summary>Returns test results by type for an application.</summary>
    Task<IEnumerable<JobApplicantTestResult>> GetByTestTypeAsync(Guid applicationId, JobApplicantTestType testType);
}

#endregion

// ============================================================================
// JOB APPLICANT COMMUNICATION
// ============================================================================

#region Job Applicant Communication

public interface IJobApplicantCommunicationRepository : IGenericRepository<JobApplicantCommunication>
{
    /// <summary>Returns all communications for an application, ordered by sent date descending.</summary>
    Task<IEnumerable<JobApplicantCommunication>> GetByApplicationIdAsync(Guid applicationId);
}

#endregion

// ============================================================================
// SHORTLIST DECISION LOG
// ============================================================================

#region Shortlist Decision Log

public interface IShortlistDecisionLogRepository : IGenericRepository<ShortlistDecisionLog>
{
    /// <summary>Returns the immutable decision audit trail for an application, ordered by decision date descending.</summary>
    Task<IEnumerable<ShortlistDecisionLog>> GetByApplicationIdAsync(Guid applicationId);
}

#endregion

// ============================================================================
// SHORTLIST REVIEW (PANEL SCORING)
// ============================================================================

#region Shortlist Review

public interface IShortlistReviewRepository : IGenericRepository<ShortlistReview>
{
    /// <summary>Returns all panel reviews for an application.</summary>
    Task<IEnumerable<ShortlistReview>> GetByApplicationIdAsync(Guid applicationId);
}

#endregion
