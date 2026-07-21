using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class JobVacancyService : IJobVacancyService
{
    private readonly IJobVacancyRepository _vacancyRepository;
    private readonly IJobVacancyAttachmentRepository _attachmentRepository;
    private readonly IJobVacancyStatusHistoryRepository _statusHistoryRepository;
    private readonly IJobShortlistingCriteriaRepository _criteriaRepository;
    private readonly IJobApplicationRepository _applicationRepository;
    private readonly IStaffRequisitionRepository _requisitionRepository;
    private readonly IJobPostingRepository _postingRepository;
    private readonly IVacancyPipelineStageAssignmentRepository _stageAssignmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JobVacancyService> _logger;

    public JobVacancyService(
        IJobVacancyRepository vacancyRepository,
        IJobVacancyAttachmentRepository attachmentRepository,
        IJobVacancyStatusHistoryRepository statusHistoryRepository,
        IJobShortlistingCriteriaRepository criteriaRepository,
        IJobApplicationRepository applicationRepository,
        IStaffRequisitionRepository requisitionRepository,
        IJobPostingRepository postingRepository,
        IVacancyPipelineStageAssignmentRepository stageAssignmentRepository,
        IUnitOfWork unitOfWork,
        ILogger<JobVacancyService> logger)
    {
        _vacancyRepository = vacancyRepository;
        _attachmentRepository = attachmentRepository;
        _statusHistoryRepository = statusHistoryRepository;
        _criteriaRepository = criteriaRepository;
        _applicationRepository = applicationRepository;
        _requisitionRepository = requisitionRepository;
        _postingRepository = postingRepository;
        _stageAssignmentRepository = stageAssignmentRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ── Queries ─────────────────────────────────────────────────────────────

    public async Task<JobVacancyDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _vacancyRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Job vacancy with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<JobVacancyDto?> GetByVacancyNumberAsync(string vacancyNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _vacancyRepository.GetByVacancyNumberAsync(vacancyNumber);
        return entity?.ToDto();
    }

    public async Task<JobVacancyDetailDto> GetWithFullDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _vacancyRepository.GetWithFullDetailsAsync(id);
        if (entity == null)
            throw new ArgumentException($"Job vacancy with ID '{id}' not found.");

        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<JobVacancySummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _vacancyRepository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<JobVacancySummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        (pageNumber, pageSize) = PagingGuard.Clamp(pageNumber, pageSize);

        var query = _vacancyRepository.GetQueryable();
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(v => v.HiringManager)
            .Include(v => v.Recruiter)
            .Include(v => v.Requisition).ThenInclude(r => r.OrganizationUnit)
            .OrderByDescending(v => v.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<JobVacancySummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<JobVacancySummaryDto>> GetByStatusAsync(JobVacancyStatus status, CancellationToken cancellationToken = default)
    {
        var entities = await _vacancyRepository.GetByStatusAsync(status);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobVacancySummaryDto>> GetActiveVacanciesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _vacancyRepository.GetActiveVacanciesAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobVacancyDto>> GetPublishedForJobBoardAsync(CancellationToken cancellationToken = default)
    {
        // Same query as the public portal (deadline filter applied in SQL, requisition/job-description
        // eagerly loaded so job titles actually render).
        var entities = await _vacancyRepository.GetPublishedForPublicPortalAsync(
            DateTime.UtcNow.Date, cancellationToken: cancellationToken);

        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<JobVacancySummaryDto>> GetByPositionAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        var entities = await _vacancyRepository.GetByPositionAsync(positionId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobVacancySummaryDto>> GetByHiringManagerAsync(Guid hiringManagerId, CancellationToken cancellationToken = default)
    {
        var entities = await _vacancyRepository.GetByHiringManagerAsync(hiringManagerId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobVacancySummaryDto>> GetByRecruiterAsync(Guid recruiterId, CancellationToken cancellationToken = default)
    {
        var entities = await _vacancyRepository.GetByRecruiterAsync(recruiterId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobVacancySummaryDto>> GetByRequisitionAsync(Guid requisitionId, CancellationToken cancellationToken = default)
    {
        var entities = await _vacancyRepository.GetByRequisitionAsync(requisitionId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobVacancySummaryDto>> GetWithDeadlineApproachingAsync(int daysAhead = 7, CancellationToken cancellationToken = default)
    {
        var entities = await _vacancyRepository.GetWithDeadlineApproachingAsync(daysAhead);
        return entities.ToSummaryDtoList();
    }

    // ── CRUD ─────────────────────────────────────────────────────────────────

    public async Task<JobVacancyDto> CreateAsync(CreateJobVacancyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var requisition = await _requisitionRepository.GetByIdAsync(createDto.StaffRequisitionId)
            ?? throw new ArgumentException($"Staff requisition with ID '{createDto.StaffRequisitionId}' not found.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.PositionId = requisition.PositionId;
        entity.VacancyNumber = await _vacancyRepository.GetNextVacancyNumberAsync();
        entity.VacancyStatus = JobVacancyStatus.Draft;

        // Snapshot audience flags from the requisition so each vacancy can be overridden independently.
        entity.AllowInternalCandidates = requisition.AllowInternalCandidates;
        entity.AllowExternalCandidates = requisition.AllowExternalCandidates;

        await _vacancyRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job vacancy created: {VacancyNumber}", entity.VacancyNumber);
        return entity.ToDto();
    }

    public async Task<JobVacancyDto> UpdateAsync(UpdateJobVacancyDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _vacancyRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Job vacancy with ID '{updateDto.Id}' not found.");

        if (entity.VacancyStatus == JobVacancyStatus.Cancelled || entity.VacancyStatus == JobVacancyStatus.Filled)
            throw new InvalidOperationException("A closed or filled vacancy cannot be edited.");

        // Editing an Approved or Published vacancy resets it to Draft (requires re-approval).
        var statusBeforeEdit = entity.VacancyStatus;
        if (entity.VacancyStatus == JobVacancyStatus.Approved ||
            entity.VacancyStatus == JobVacancyStatus.Published)
        {
            entity.VacancyStatus = JobVacancyStatus.Draft;

            var draftHistory = new JobVacancyStatusHistory
            {
                Id           = Guid.NewGuid(),
                TenantId     = entity.TenantId,
                JobVacancyId = entity.Id,
                FromStatus   = statusBeforeEdit,
                ToStatus     = JobVacancyStatus.Draft,
                ChangedDate  = DateTime.UtcNow,
                ChangedById  = updatedByUserId,
                Reason       = "Vacancy edited — status reset to Draft for re-approval",
                CreatedAt    = DateTime.UtcNow,
                CreatedBy    = updatedByUserId.ToString(),
            };
            await _statusHistoryRepository.AddAsync(draftHistory);

            _logger.LogInformation(
                "Job vacancy {VacancyNumber} reverted from {From} to Draft due to edit.",
                entity.VacancyNumber, statusBeforeEdit);
        }

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _vacancyRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job vacancy updated: {VacancyNumber}", entity.VacancyNumber);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _vacancyRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Job vacancy with ID '{id}' not found.");

        if (entity.VacancyStatus != JobVacancyStatus.Draft)
            throw new InvalidOperationException("Only draft vacancies can be deleted.");

        await _vacancyRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job vacancy deleted: {VacancyNumber}", entity.VacancyNumber);
        return true;
    }

    // ── Workflow ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Atomically applies field updates AND a status transition in one DB transaction.
    /// This is the correct entry point for all transition buttons on the edit form.
    /// Using a single unit-of-work eliminates the partial-failure risk that existed
    /// when UpdateAsync and ChangeStatusAsync were called as two separate round-trips.
    /// </summary>
    public async Task<JobVacancyDto> TransitionAsync(
        TransitionJobVacancyDto dto, Guid tenantId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _vacancyRepository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Job vacancy with ID '{dto.Id}' not found.");

        if (entity.VacancyStatus == JobVacancyStatus.Cancelled || entity.VacancyStatus == JobVacancyStatus.Filled)
            throw new InvalidOperationException("A closed or filled vacancy cannot be edited.");

        var fromStatus = entity.VacancyStatus;

        // Apply field updates (does not touch VacancyStatus).
        entity.UpdateEntity(dto, userId);

        // Apply the status transition.
        entity.VacancyStatus = dto.NewStatus;

        // Stamp the ACTUAL publish date the first time the vacancy really enters Published. PublishDate
        // (the intended/planned date entered on the form) is left untouched so HR can compare the two.
        if (dto.NewStatus == JobVacancyStatus.Published && entity.ActualPublishDate == null)
            entity.ActualPublishDate = DateTime.UtcNow;

        var history = new JobVacancyStatusHistory
        {
            Id           = Guid.NewGuid(),
            TenantId     = entity.TenantId,
            JobVacancyId = entity.Id,
            FromStatus   = fromStatus,
            ToStatus     = dto.NewStatus,
            ChangedDate  = DateTime.UtcNow,
            ChangedById  = userId,
            Reason       = dto.Reason,
            Comments     = dto.Comments,
            CreatedAt    = DateTime.UtcNow,
            CreatedBy    = userId.ToString(),
        };

        await _vacancyRepository.UpdateAsync(entity);
        await _statusHistoryRepository.AddAsync(history);

        await AutoCreatePublishPostingsIfNeededAsync(entity, fromStatus, dto.NewStatus, userId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Job vacancy {VacancyNumber} transitioned from {From} to {To}",
            entity.VacancyNumber, fromStatus, dto.NewStatus);

        return entity.ToDto();
    }

    public async Task<bool> ChangeStatusAsync(ChangeJobVacancyStatusDto dto, Guid changedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _vacancyRepository.GetByIdAsync(dto.VacancyId);
        if (entity == null)
            throw new ArgumentException($"Job vacancy with ID '{dto.VacancyId}' not found.");

        var from = entity.VacancyStatus;
        entity.VacancyStatus = dto.NewStatus;

        // Stamp the ACTUAL publish date the first time the vacancy really enters Published (see TransitionAsync).
        if (dto.NewStatus == JobVacancyStatus.Published && entity.ActualPublishDate == null)
            entity.ActualPublishDate = DateTime.UtcNow;

        var history = new JobVacancyStatusHistory
        {
            Id = Guid.NewGuid(),
            TenantId = entity.TenantId,
            JobVacancyId = entity.Id,
            FromStatus = from,
            ToStatus = dto.NewStatus,
            ChangedDate = DateTime.UtcNow,
            ChangedById = changedByUserId,
            Reason = dto.Reason,
            Comments = dto.Comments,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = changedByUserId.ToString()
        };

        await _vacancyRepository.UpdateAsync(entity);
        await _statusHistoryRepository.AddAsync(history);

        await AutoCreatePublishPostingsIfNeededAsync(entity, from, dto.NewStatus, changedByUserId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job vacancy {VacancyNumber} status changed from {From} to {To}", entity.VacancyNumber, from, dto.NewStatus);
        return true;
    }

    private async Task AutoCreatePublishPostingsIfNeededAsync(
        JobVacancy entity,
        JobVacancyStatus fromStatus,
        JobVacancyStatus toStatus,
        Guid changedByUserId)
    {
        // Only create postings when entering Published state.
        if (toStatus != JobVacancyStatus.Published || fromStatus == JobVacancyStatus.Published)
            return;

        var existingPostings = await _postingRepository.GetByVacancyIdAsync(entity.Id);
        var hasInternalPosting = existingPostings.Any(p => p.Channel == JobPostingChannel.InternalPortal);
        var hasExternalPosting = existingPostings.Any(p => p.Channel == JobPostingChannel.CompanyWebsite);

        // Guard: if both flags are somehow false, default to external (CompanyWebsite).
        var shouldCreateInternal = entity.AllowInternalCandidates && !hasInternalPosting;
        var shouldCreateExternal = (entity.AllowExternalCandidates || (!entity.AllowInternalCandidates && !entity.AllowExternalCandidates))
                                   && !hasExternalPosting;

        var publishDate = entity.PublishDate ?? DateTime.UtcNow;
        var postingTitle = entity.CustomAdvertTitle ?? entity.VacancyNumber;

        if (shouldCreateInternal)
        {
            await _postingRepository.AddAsync(new JobPosting
            {
                Id           = Guid.NewGuid(),
                TenantId     = entity.TenantId,
                JobVacancyId = entity.Id,
                Channel      = JobPostingChannel.InternalPortal,
                Title        = postingTitle,
                Description  = string.Empty,
                Status       = JobPostingStatus.Published,
                IsActive     = true,
                PublishDate  = publishDate,
                ExpiryDate   = entity.ApplicationDeadline,
                PostedById   = changedByUserId,
                CreatedAt    = DateTime.UtcNow,
                CreatedBy    = changedByUserId.ToString(),
            });
        }

        if (shouldCreateExternal)
        {
            await _postingRepository.AddAsync(new JobPosting
            {
                Id           = Guid.NewGuid(),
                TenantId     = entity.TenantId,
                JobVacancyId = entity.Id,
                Channel      = JobPostingChannel.CompanyWebsite,
                Title        = postingTitle,
                Description  = string.Empty,
                Status       = JobPostingStatus.Published,
                IsActive     = true,
                PublishDate  = publishDate,
                ExpiryDate   = entity.ApplicationDeadline,
                PostedById   = changedByUserId,
                CreatedAt    = DateTime.UtcNow,
                CreatedBy    = changedByUserId.ToString(),
            });
        }

        if (shouldCreateInternal || shouldCreateExternal)
        {
            _logger.LogInformation(
                "Auto-created job postings for vacancy {VacancyNumber}: internal={Internal}, external={External}",
                entity.VacancyNumber,
                shouldCreateInternal,
                shouldCreateExternal);
        }
    }

    public async Task<bool> CloseAsync(CloseJobVacancyDto dto, Guid closedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _vacancyRepository.GetByIdAsync(dto.VacancyId);
        if (entity == null)
            throw new ArgumentException($"Job vacancy with ID '{dto.VacancyId}' not found.");

        if (entity.VacancyStatus == JobVacancyStatus.Cancelled)
            throw new InvalidOperationException("Vacancy is already closed.");

        var from = entity.VacancyStatus;
        entity.VacancyStatus = JobVacancyStatus.Cancelled;
        entity.ClosureReason = dto.ClosureReason;
        entity.ClosureNotes = dto.ClosureNotes;
        entity.ClosedDate = DateTime.UtcNow;

        var history = new JobVacancyStatusHistory
        {
            Id = Guid.NewGuid(),
            TenantId = entity.TenantId,
            JobVacancyId = entity.Id,
            FromStatus = from,
            ToStatus = JobVacancyStatus.Cancelled,
            ChangedDate = DateTime.UtcNow,
            ChangedById = closedByUserId,
            Reason = dto.ClosureReason.ToString(),
            Comments = dto.ClosureNotes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = closedByUserId.ToString()
        };

        await _vacancyRepository.UpdateAsync(entity);
        await _statusHistoryRepository.AddAsync(history);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job vacancy closed: {VacancyNumber}", entity.VacancyNumber);
        return true;
    }

    public async Task<bool> CloseForApplicationsAsync(CloseForApplicationsDto dto, Guid closedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _vacancyRepository.GetByIdAsync(dto.VacancyId);
        if (entity == null)
            throw new ArgumentException($"Job vacancy with ID '{dto.VacancyId}' not found.");

        if (entity.VacancyStatus != JobVacancyStatus.Published)
            throw new InvalidOperationException("Only published vacancies can be closed for applications.");

        var from = entity.VacancyStatus;
        entity.VacancyStatus = JobVacancyStatus.ClosedForApplications;
        entity.ClosureReason = dto.Reason;
        entity.ClosureNotes  = dto.Notes;

        var history = new JobVacancyStatusHistory
        {
            Id           = Guid.NewGuid(),
            TenantId     = entity.TenantId,
            JobVacancyId = entity.Id,
            FromStatus   = from,
            ToStatus     = JobVacancyStatus.ClosedForApplications,
            ChangedDate  = DateTime.UtcNow,
            ChangedById  = closedByUserId,
            Reason       = dto.Reason.ToString(),
            Comments     = dto.Notes,
            CreatedAt    = DateTime.UtcNow,
            CreatedBy    = closedByUserId.ToString()
        };

        await _vacancyRepository.UpdateAsync(entity);
        await _statusHistoryRepository.AddAsync(history);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job vacancy {VacancyNumber} closed for applications (reason: {Reason})",
            entity.VacancyNumber, dto.Reason);
        return true;
    }

    // ── Attachments ───────────────────────────────────────────────────────────

    public async Task<JobVacancyAttachmentDto> AddAttachmentAsync(CreateJobVacancyAttachmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _attachmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobVacancyAttachmentDto>> GetAttachmentsAsync(Guid vacancyId, CancellationToken cancellationToken = default)
    {
        var entities = await _attachmentRepository.GetByVacancyIdAsync(vacancyId);
        return entities.Select(e => e.ToDto());
    }

    public async Task<bool> DeleteAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var entity = await _attachmentRepository.GetByIdAsync(attachmentId);
        if (entity == null)
            throw new ArgumentException($"Attachment with ID '{attachmentId}' not found.");

        await _attachmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Status history (read-only) ────────────────────────────────────────────

    public async Task<IEnumerable<JobVacancyStatusHistoryDto>> GetStatusHistoryAsync(Guid vacancyId, CancellationToken cancellationToken = default)
    {
        var entities = await _statusHistoryRepository.GetByVacancyIdAsync(vacancyId);
        return entities.Select(e => e.ToDto());
    }

    // ── Shortlisting criteria ─────────────────────────────────────────────────

    public async Task<JobShortlistingCriteriaDto> AddCriteriaAsync(CreateJobShortlistingCriteriaDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _criteriaRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await MarkApplicationScoresStaleAsync(createDto.JobVacancyId, cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobShortlistingCriteriaDto>> GetCriteriaAsync(Guid vacancyId, CancellationToken cancellationToken = default)
    {
        var entities = await _criteriaRepository.GetByVacancyIdAsync(vacancyId);
        return entities.Select(e => e.ToDto());
    }

    public async Task<JobShortlistingCriteriaDto> UpdateCriteriaAsync(UpdateJobShortlistingCriteriaDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _criteriaRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Shortlisting criteria with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _criteriaRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await MarkApplicationScoresStaleAsync(entity.JobVacancyId, cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteCriteriaAsync(Guid criteriaId, CancellationToken cancellationToken = default)
    {
        var entity = await _criteriaRepository.GetByIdAsync(criteriaId);
        if (entity == null)
            throw new ArgumentException($"Shortlisting criteria with ID '{criteriaId}' not found.");

        var vacancyId = entity.JobVacancyId;
        await _criteriaRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await MarkApplicationScoresStaleAsync(vacancyId, cancellationToken);
        return true;
    }

    private async Task MarkApplicationScoresStaleAsync(Guid vacancyId, CancellationToken cancellationToken)
    {
        var applications = await _applicationRepository.GetByVacancyIdAsync(vacancyId);
        var toUpdate = applications
            .Where(a => a.ScoredAt.HasValue
                     && a.Status != ApplicationStatus.Withdrawn
                     && a.Status != ApplicationStatus.Rejected)
            .ToList();

        foreach (var app in toUpdate)
        {
            app.ScoreIsStale = true;
            await _applicationRepository.UpdateAsync(app);
        }

        if (toUpdate.Any())
            await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<JobVacancyStatusHistoryDto?> GetLatestStatusHistoryAsync(Guid vacancyId, CancellationToken cancellationToken = default)
    {
        var entity = await _statusHistoryRepository.GetLatestForVacancyAsync(vacancyId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<JobShortlistingCriteriaDto>> GetMandatoryCriteriaAsync(Guid vacancyId, CancellationToken cancellationToken = default)
    {
        var entities = await _criteriaRepository.GetMandatoryCriteriaAsync(vacancyId);
        return entities.Select(e => e.ToDto());
    }

    public async Task<bool> SubmitShortlistForApprovalAsync(
        SubmitShortlistForApprovalDto dto, Guid submittedByUserId, CancellationToken cancellationToken = default)
    {
        var vacancy = await _vacancyRepository.GetByIdAsync(dto.VacancyId);
        if (vacancy == null)
            throw new ArgumentException($"Vacancy '{dto.VacancyId}' not found.");

        if (vacancy.ShortlistApprovalStatus == ShortlistApprovalStatus.PendingApproval)
            throw new InvalidOperationException("Shortlist is already pending approval.");

        if (vacancy.ShortlistApprovalStatus == ShortlistApprovalStatus.Approved)
            throw new InvalidOperationException("Shortlist is already approved.");

        vacancy.ShortlistApprovalStatus = ShortlistApprovalStatus.PendingApproval;
        vacancy.ShortlistSubmittedAt = DateTime.UtcNow;
        vacancy.ShortlistSubmittedById = submittedByUserId;
        vacancy.ShortlistApprovalNotes = dto.Notes;

        await _vacancyRepository.UpdateAsync(vacancy);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReviewShortlistApprovalAsync(
        ReviewShortlistApprovalDto dto, Guid reviewedByUserId, CancellationToken cancellationToken = default)
    {
        var vacancy = await _vacancyRepository.GetByIdAsync(dto.VacancyId);
        if (vacancy == null)
            throw new ArgumentException($"Vacancy '{dto.VacancyId}' not found.");

        if (vacancy.ShortlistApprovalStatus != ShortlistApprovalStatus.PendingApproval)
            throw new InvalidOperationException("Shortlist is not currently pending approval.");

        vacancy.ShortlistApprovalStatus = dto.Approved
            ? ShortlistApprovalStatus.Approved
            : ShortlistApprovalStatus.Rejected;
        vacancy.ShortlistApprovedAt = DateTime.UtcNow;
        vacancy.ShortlistApprovedById = reviewedByUserId;
        vacancy.ShortlistApprovalNotes = dto.Notes;

        await _vacancyRepository.UpdateAsync(vacancy);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Public career portal ──────────────────────────────────────────────────

    public async Task<IEnumerable<PublicVacancyDto>> GetPublishedForExternalPortalAsync(
        string? searchTerm      = null,
        EmploymentType? empType = null,
        WorkMode? workMode      = null,
        CancellationToken cancellationToken = default)
    {
        // Status / deadline / employment-type / work-mode are all applied in SQL.
        var entities = await _vacancyRepository.GetPublishedForPublicPortalAsync(
            DateTime.UtcNow.Date, empType, workMode, cancellationToken);

        // JobTitle is [NotMapped] — it resolves through Requisition → JobDescription — so a title search
        // cannot be translated to SQL and is applied here, after the database has already narrowed the set.
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            entities = entities.Where(v =>
                v.JobTitle.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (v.KeyBenefitsSummary != null &&
                 v.KeyBenefitsSummary.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        return entities.ToPublicDtoList();
    }

    public async Task<PublicVacancyDto?> GetPublicVacancyByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var vacancy = await _vacancyRepository.GetWithFullDetailsAsync(id);
        if (vacancy == null || vacancy.VacancyStatus != JobVacancyStatus.Published)
            return null;
        return vacancy.ToPublicDto();
    }

    // ── Pipeline stage assignments ────────────────────────────────────────────

    public async Task<IEnumerable<VacancyPipelineStageAssignmentDto>> GetStageAssignmentsAsync(
        Guid vacancyId, CancellationToken ct = default)
    {
        var entities = await _stageAssignmentRepository.GetByVacancyIdAsync(vacancyId);
        return entities.Select(e => e.ToDto());
    }

    public async Task<VacancyPipelineStageAssignmentDto> UpsertStageAssignmentAsync(
        CreateVacancyPipelineStageAssignmentDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var existing = await _stageAssignmentRepository.GetByVacancyAndStageAsync(dto.JobVacancyId, dto.PipelineStageId);
        if (existing != null)
        {
            existing.AssignedToId             = dto.AssignedToId;
            existing.DueDate                  = dto.DueDate;
            existing.EscalationEnabled        = dto.EscalationEnabled;
            existing.EscalationDaysAfterDue   = dto.EscalationDaysAfterDue;
            existing.EscalateToId             = dto.EscalateToId;
            existing.UpdatedAt                = DateTime.UtcNow;
            existing.UpdatedBy                = userId.ToString();
            await _stageAssignmentRepository.UpdateAsync(existing);
            await _unitOfWork.SaveChangesAsync(ct);
            var updated = await _stageAssignmentRepository.GetByVacancyAndStageAsync(dto.JobVacancyId, dto.PipelineStageId);
            return updated!.ToDto();
        }

        var entity = new VacancyPipelineStageAssignment
        {
            Id                      = Guid.NewGuid(),
            TenantId                = tenantId,
            CreatedBy               = userId.ToString(),
            CreatedById             = userId,
            CreatedAt               = DateTime.UtcNow,
            JobVacancyId            = dto.JobVacancyId,
            PipelineStageId         = dto.PipelineStageId,
            AssignedToId            = dto.AssignedToId,
            AssignedById            = userId,
            AssignedAt              = DateTime.UtcNow,
            DueDate                 = dto.DueDate,
            Status                  = VacancyStageAssignmentStatus.NotStarted,
            EscalationEnabled       = dto.EscalationEnabled,
            EscalationDaysAfterDue  = dto.EscalationDaysAfterDue,
            EscalateToId            = dto.EscalateToId,
        };
        await _stageAssignmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        var created = await _stageAssignmentRepository.GetByVacancyAndStageAsync(dto.JobVacancyId, dto.PipelineStageId);
        return created!.ToDto();
    }

    public async Task<VacancyPipelineStageAssignmentDto> UpdateStageAssignmentAsync(
        UpdateVacancyPipelineStageAssignmentDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _stageAssignmentRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Stage assignment '{dto.Id}' not found.");

        entity.AssignedToId           = dto.AssignedToId;
        entity.DueDate                = dto.DueDate;
        entity.EscalationEnabled      = dto.EscalationEnabled;
        entity.EscalationDaysAfterDue = dto.EscalationDaysAfterDue;
        entity.EscalateToId           = dto.EscalateToId;
        entity.UpdatedAt              = DateTime.UtcNow;
        entity.UpdatedBy              = userId.ToString();
        await _stageAssignmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        var refreshed = await _stageAssignmentRepository.GetByVacancyAndStageAsync(entity.JobVacancyId, entity.PipelineStageId);
        return refreshed!.ToDto();
    }

    public async Task<VacancyPipelineStageAssignmentDto> CompleteStageAssignmentAsync(
        CompleteVacancyPipelineStageAssignmentDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _stageAssignmentRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Stage assignment '{dto.Id}' not found.");

        entity.Status          = VacancyStageAssignmentStatus.Completed;
        entity.CompletedAt     = DateTime.UtcNow;
        entity.CompletedById   = userId;
        entity.CompletionNotes = dto.CompletionNotes;
        entity.UpdatedAt       = DateTime.UtcNow;
        entity.UpdatedBy       = userId.ToString();
        await _stageAssignmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        var refreshed = await _stageAssignmentRepository.GetByVacancyAndStageAsync(entity.JobVacancyId, entity.PipelineStageId);
        return refreshed!.ToDto();
    }

    public async Task<VacancyPipelineStageAssignmentDto> SkipStageAssignmentAsync(
        SkipVacancyPipelineStageAssignmentDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _stageAssignmentRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Stage assignment '{dto.Id}' not found.");

        entity.Status          = VacancyStageAssignmentStatus.Skipped;
        entity.CompletionNotes = dto.Reason;
        entity.UpdatedAt       = DateTime.UtcNow;
        entity.UpdatedBy       = userId.ToString();
        await _stageAssignmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        var refreshed = await _stageAssignmentRepository.GetByVacancyAndStageAsync(entity.JobVacancyId, entity.PipelineStageId);
        return refreshed!.ToDto();
    }

    public async Task<bool> DeleteStageAssignmentAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _stageAssignmentRepository.GetByIdAsync(id);
        if (entity == null) return false;
        await _stageAssignmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

