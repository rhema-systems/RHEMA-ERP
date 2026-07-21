using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// JOB VACANCY
// ============================================================================

#region Job Vacancy

public interface IJobVacancyRepository : IGenericRepository<JobVacancy>
{
    /// <summary>Returns the vacancy matching the unique vacancy number, with position and requisition loaded.</summary>
    Task<JobVacancy?> GetByVacancyNumberAsync(string vacancyNumber);

    /// <summary>Returns a fully-loaded vacancy including postings, shortlisting criteria, applications, interviews, and status history.</summary>
    Task<JobVacancy?> GetWithFullDetailsAsync(Guid id);

    /// <summary>Returns vacancies filtered by status, with position and hiring manager loaded.</summary>
    Task<IEnumerable<JobVacancy>> GetByStatusAsync(JobVacancyStatus status);

    /// <summary>Returns active vacancies (Published or Open) ordered by publish date descending.</summary>
    Task<IEnumerable<JobVacancy>> GetActiveVacanciesAsync();

    /// <summary>Returns all vacancies raised for a specific staff requisition.</summary>
    Task<IEnumerable<JobVacancy>> GetByRequisitionAsync(Guid requisitionId);

    /// <summary>Returns all vacancies for a specific position.</summary>
    Task<IEnumerable<JobVacancy>> GetByPositionAsync(Guid positionId);

    /// <summary>Returns vacancies where the given employee is the hiring manager.</summary>
    Task<IEnumerable<JobVacancy>> GetByHiringManagerAsync(Guid hiringManagerId);

    /// <summary>Returns vacancies where the given employee is the recruiter.</summary>
    Task<IEnumerable<JobVacancy>> GetByRecruiterAsync(Guid recruiterId);

    /// <summary>Returns vacancies whose application deadline falls within the specified number of days.</summary>
    Task<IEnumerable<JobVacancy>> GetWithDeadlineApproachingAsync(int daysAhead = 7);

    /// <summary>
    /// Recomputes all five denormalized counters from live application data and saves them.
    /// Call this after bulk operations or whenever a counter is suspected to have drifted.
    /// </summary>
    Task ReconcileCountersAsync(Guid vacancyId, CancellationToken cancellationToken = default);

    /// <summary>Returns the next vacancy number. Served by the atomic per-tenant sequence table.</summary>
    Task<string> GetNextVacancyNumberAsync();

    /// <summary>
    /// Published vacancies still open for applications, for the public careers portal and the internal
    /// job board.
    ///
    /// <para>Filters that SQL can translate (status, deadline, employment type, work mode) are applied in
    /// the database rather than in memory, and the navigations the public DTO actually reads
    /// (requisition → job description / org unit / location, and position) are eagerly loaded — lazy
    /// loading is disabled, so without these includes the portal renders blank job titles, departments
    /// and locations.</para>
    /// </summary>
    Task<IEnumerable<JobVacancy>> GetPublishedForPublicPortalAsync(
        DateTime asOfUtc,
        EmploymentType? employmentType = null,
        WorkMode? workMode = null,
        CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// JOB VACANCY ATTACHMENT
// ============================================================================

#region Job Vacancy Attachment

public interface IJobVacancyAttachmentRepository : IGenericRepository<JobVacancyAttachment>
{
    /// <summary>Returns all attachments for a vacancy, with uploader details loaded.</summary>
    Task<IEnumerable<JobVacancyAttachment>> GetByVacancyIdAsync(Guid vacancyId);
}

#endregion

// ============================================================================
// JOB VACANCY STATUS HISTORY
// ============================================================================

#region Job Vacancy Status History

public interface IJobVacancyStatusHistoryRepository : IGenericRepository<JobVacancyStatusHistory>
{
    /// <summary>Returns the full status change history for a vacancy, ordered by changed date descending.</summary>
    Task<IEnumerable<JobVacancyStatusHistory>> GetByVacancyIdAsync(Guid vacancyId);

    /// <summary>Returns the most recent status change record for a vacancy.</summary>
    Task<JobVacancyStatusHistory?> GetLatestForVacancyAsync(Guid vacancyId);
}

#endregion

// ============================================================================
// VACANCY PIPELINE STAGE ASSIGNMENT
// ============================================================================

#region Vacancy Pipeline Stage Assignment

public interface IVacancyPipelineStageAssignmentRepository : IGenericRepository<VacancyPipelineStageAssignment>
{
    /// <summary>Returns all assignments for a vacancy, ordered by stage display order, with related entities loaded.</summary>
    Task<IEnumerable<VacancyPipelineStageAssignment>> GetByVacancyIdAsync(Guid vacancyId);

    /// <summary>Returns the assignment for a specific stage of a specific vacancy (null if not yet assigned).</summary>
    Task<VacancyPipelineStageAssignment?> GetByVacancyAndStageAsync(Guid vacancyId, Guid stageId);

    /// <summary>Returns assignments that are past their due date and not yet completed/skipped — used by the overdue-check job.</summary>
    Task<IEnumerable<VacancyPipelineStageAssignment>> GetOverdueAsync(DateTime asOf);
}

#endregion
