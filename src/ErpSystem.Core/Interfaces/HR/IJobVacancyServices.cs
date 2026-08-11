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
    Task<IEnumerable<JobVacancyDto>> GetPublishedForJobBoardAsync(Guid tenantId, CancellationToken cancellationToken = default);
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
    /// <summary>
    /// Records an attachment against a vacancy, from a file the controlled-upload gate has already
    /// scanned and registered. The old <c>CreateJobVacancyAttachmentDto</c> overload took a
    /// caller-supplied <c>filePath</c> and stored no file; it is deleted, not deprecated.
    /// </summary>
    Task<JobVacancyAttachmentDto> AddAttachmentAsync(
        Guid jobVacancyId,
        Guid uploadedById,
        string fileName,
        long fileSize,
        string? description,
        CancellationToken cancellationToken = default,
        Guid? fileUploadRecordId = null,
        Guid? documentRecordId = null,
        Guid? documentVersionId = null);
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

    // Shortlist approval lives on IJobApplicationService, not here. This interface used to declare its
    // own Submit/Review pair; JobApplicationController — the only caller — went to the application
    // service's identical pair, so these two never executed. Removed rather than left as a second
    // implementation of the same rules for someone to wire up by mistake: the surviving pair also
    // refuses an empty shortlist and refuses to let the submitter approve their own.

    // ── Public career portal ──────────────────────────────────────────────────

    /// <summary>
    /// Returns a paged, filtered list of currently published vacancies for the public career portal.
    /// Only safe public fields are returned — recruiter names, pipeline details, and application counts
    /// are excluded from the <see cref="PublicVacancyDto"/> shape.
    /// </summary>
    Task<IEnumerable<PublicVacancyDto>> GetPublishedForExternalPortalAsync(
        Guid tenantId,
        string? searchTerm        = null,
        EmploymentType? empType   = null,
        WorkMode? workMode        = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a single published vacancy for the public career portal detail view, scoped to the tenant.</summary>
    Task<PublicVacancyDto?> GetPublicVacancyByIdAsync(
        Guid tenantId,
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
