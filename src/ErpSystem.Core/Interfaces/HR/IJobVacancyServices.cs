using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// JOB VACANCY SERVICE
// ============================================================================

#region Job Vacancy Service

public interface IJobVacancyService
{
    // Queries
    Task<JobVacancyDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<JobVacancyDto?> GetByVacancyNumberAsync(string vacancyNumber, CancellationToken cancellationToken = default);
    Task<JobVacancyDetailDto> GetWithFullDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobVacancySummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<JobVacancySummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobVacancySummaryDto>> GetByStatusAsync(JobVacancyStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobVacancySummaryDto>> GetActiveVacanciesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<JobVacancyDto>> GetPublishedForJobBoardAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<JobVacancySummaryDto>> GetByPositionAsync(Guid positionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobVacancySummaryDto>> GetByHiringManagerAsync(Guid hiringManagerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobVacancySummaryDto>> GetByRecruiterAsync(Guid recruiterId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobVacancySummaryDto>> GetByRequisitionAsync(Guid requisitionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobVacancySummaryDto>> GetWithDeadlineApproachingAsync(int daysAhead = 7, CancellationToken cancellationToken = default);

    // CRUD
    Task<JobVacancyDto> CreateAsync(CreateJobVacancyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<JobVacancyDto> UpdateAsync(UpdateJobVacancyDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Workflow
    Task<JobVacancyDto> TransitionAsync(TransitionJobVacancyDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ChangeStatusAsync(ChangeJobVacancyStatusDto dto, Guid changedByUserId, CancellationToken cancellationToken = default);
    Task<bool> CloseAsync(CloseJobVacancyDto dto, Guid closedByUserId, CancellationToken cancellationToken = default);
    Task<bool> CloseForApplicationsAsync(CloseForApplicationsDto dto, Guid closedByUserId, CancellationToken cancellationToken = default);

    // Attachment operations
    Task<JobVacancyAttachmentDto> AddAttachmentAsync(CreateJobVacancyAttachmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobVacancyAttachmentDto>> GetAttachmentsAsync(Guid vacancyId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default);

    // Status history (read-only)
    Task<IEnumerable<JobVacancyStatusHistoryDto>> GetStatusHistoryAsync(Guid vacancyId, CancellationToken cancellationToken = default);
    Task<JobVacancyStatusHistoryDto?> GetLatestStatusHistoryAsync(Guid vacancyId, CancellationToken cancellationToken = default);

    // Shortlisting criteria
    Task<JobShortlistingCriteriaDto> AddCriteriaAsync(CreateJobShortlistingCriteriaDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobShortlistingCriteriaDto>> GetCriteriaAsync(Guid vacancyId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobShortlistingCriteriaDto>> GetMandatoryCriteriaAsync(Guid vacancyId, CancellationToken cancellationToken = default);
    Task<JobShortlistingCriteriaDto> UpdateCriteriaAsync(UpdateJobShortlistingCriteriaDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteCriteriaAsync(Guid criteriaId, CancellationToken cancellationToken = default);

    // Shortlist approval workflow
    Task<bool> SubmitShortlistForApprovalAsync(SubmitShortlistForApprovalDto dto, Guid submittedByUserId, CancellationToken cancellationToken = default);
    Task<bool> ReviewShortlistApprovalAsync(ReviewShortlistApprovalDto dto, Guid reviewedByUserId, CancellationToken cancellationToken = default);

    // ── Public career portal ──────────────────────────────────────────────────

    /// <summary>
    /// Returns a paged, filtered list of currently published vacancies for the public career portal.
    /// Only safe public fields are returned — recruiter names, pipeline details, and application counts
    /// are excluded from the <see cref="PublicVacancyDto"/> shape.
    /// </summary>
    Task<IEnumerable<PublicVacancyDto>> GetPublishedForExternalPortalAsync(
        string? searchTerm        = null,
        EmploymentType? empType   = null,
        WorkMode? workMode        = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a single published vacancy for the public career portal detail view.</summary>
    Task<PublicVacancyDto?> GetPublicVacancyByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    // ── Pipeline stage assignments ────────────────────────────────────────────

    Task<IEnumerable<VacancyPipelineStageAssignmentDto>> GetStageAssignmentsAsync(Guid vacancyId, CancellationToken ct = default);
    Task<VacancyPipelineStageAssignmentDto> UpsertStageAssignmentAsync(CreateVacancyPipelineStageAssignmentDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<VacancyPipelineStageAssignmentDto> UpdateStageAssignmentAsync(UpdateVacancyPipelineStageAssignmentDto dto, Guid userId, CancellationToken ct = default);
    Task<VacancyPipelineStageAssignmentDto> CompleteStageAssignmentAsync(CompleteVacancyPipelineStageAssignmentDto dto, Guid userId, CancellationToken ct = default);
    Task<VacancyPipelineStageAssignmentDto> SkipStageAssignmentAsync(SkipVacancyPipelineStageAssignmentDto dto, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteStageAssignmentAsync(Guid id, CancellationToken ct = default);
}

#endregion
