using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
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
    private readonly ICurrentUserProvider _currentUserProvider;
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
        ICurrentUserProvider currentUserProvider,
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
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // A vacancy owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<JobVacancy> GetOwnedAsync(Guid id)
    {
        var entity = await _vacancyRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Job vacancy with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobVacancy> GetOwnedWithFullDetailsAsync(Guid id)
    {
        var entity = await _vacancyRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Job vacancy with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobVacancyAttachment> GetOwnedAttachmentAsync(Guid id)
    {
        var entity = await _attachmentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Attachment with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobShortlistingCriteria> GetOwnedCriteriaAsync(Guid id)
    {
        var entity = await _criteriaRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Shortlisting criteria with ID '{id}' not found.");
        return entity;
    }

    private async Task<VacancyPipelineStageAssignment> GetOwnedStageAssignmentAsync(Guid id)
    {
        var entity = await _stageAssignmentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Stage assignment '{id}' not found.");
        return entity;
    }

    /// <summary>
    /// Marking a pipeline stage done or skipped belongs to whoever was given it — usually a hiring
    /// manager or interviewer, not HR — so these two cannot be gated by role on the controller the
    /// way the rest of the vacancy mutations are. Without a check of their own they were open to
    /// every authenticated user, who could sign off someone else's interview stage.
    /// </summary>
    private void RequireStageOwnership(VacancyPipelineStageAssignment assignment, Guid actingEmployeeId, string action)
    {
        if (_currentUserProvider.HasRole(Constants.Roles.SuperAdmin) ||
            _currentUserProvider.HasRole(Constants.Roles.Hr))
            return;

        if (assignment.AssignedToId == actingEmployeeId || assignment.AssignedById == actingEmployeeId)
            return;

        throw new UnauthorizedAccessException(
            $"Only the person this stage is assigned to, the person who assigned it, or HR can {action} it.");
    }

    // ── Queries ─────────────────────────────────────────────────────────────

    public async Task<JobVacancyDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<JobVacancyDto?> GetByVacancyNumberAsync(string vacancyNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _vacancyRepository.GetByVacancyNumberAsync(vacancyNumber);
        return entity == null || entity.TenantId != tenantId ? null : entity.ToDto();
    }

    public async Task<JobVacancyDetailDto> GetWithFullDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedWithFullDetailsAsync(id);
        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<JobVacancySummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _vacancyRepository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<PagedResult<JobVacancySummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        (pageNumber, pageSize) = PagingGuard.Clamp(pageNumber, pageSize);

        var tenantId = GetTenantId();
        var query = _vacancyRepository.GetQueryable().Where(v => v.TenantId == tenantId);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(v => v.Position)
            .Include(v => v.HiringManager)
            .Include(v => v.Recruiter)
            .Include(v => v.Requisition).ThenInclude(r => r.OrganizationUnit)
            // JobTitle is [NotMapped] over Requisition.JobDescription, so without this the list's
            // primary column is blank on every row that has no CustomAdvertTitle.
            .Include(v => v.Requisition).ThenInclude(r => r.JobDescription)
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
        var tenantId = GetTenantId();
        var entities = await _vacancyRepository.GetByStatusAsync(status);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobVacancySummaryDto>> GetActiveVacanciesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _vacancyRepository.GetActiveVacanciesAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    /// <summary>
    /// The internal job board: what an EMPLOYEE may see and apply for.
    /// </summary>
    /// <remarks>
    /// <para>Two things were wrong here before area 25 slice 13b, and both were measured.</para>
    ///
    /// <para><b>It returned the full <c>JobVacancyDto</c> to any internal caller.</b> Measured on
    /// the same vacancy: 69 keys to an employee versus 23 to an anonymous member of the public —
    /// the internal board handed out the auto-shortlist threshold, the test-score weight, the
    /// internal-candidate boost points, whether blind screening was on, the shortlist approval
    /// notes and approver, the workflow instance id, the pipeline counts, and the hiring manager
    /// and recruiter by name. Those are the terms an applicant is about to be judged on. Worse,
    /// it served <c>SalaryRangeMin/Max</c> regardless of <c>IsSalaryVisible</c>, which
    /// <c>ToPublicDto</c> correctly withholds — so the LESS trusted audience was better protected
    /// than the internal one. And it carried no job description at all, so the one thing an
    /// applicant actually needs was the one thing missing. It now uses the same lean projection
    /// the public portal uses, which fixes all of that at once.</para>
    ///
    /// <para><b>It ignored <c>AllowInternalCandidates</c>.</b> The published query is the public
    /// portal's, which has no reason to consider it; the job-board screen compensated with a
    /// client-side <c>.filter()</c>, so a vacancy closed to internal candidates was still served
    /// by the API and could still be applied to by anyone posting directly. The flag is enforced
    /// here now, and again on the apply path — a rule that lives only in the browser is not a rule.
    /// (The vacancy already models this properly: publishing creates an <c>InternalPortal</c>
    /// posting only when the flag is set.)</para>
    /// </remarks>
    public async Task<IEnumerable<PublicVacancyDto>> GetPublishedForJobBoardAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // Same query as the public portal (deadline filter applied in SQL, requisition/job-description
        // eagerly loaded so job titles actually render), scoped to the caller's tenant.
        var entities = await _vacancyRepository.GetPublishedForPublicPortalAsync(
            tenantId, DateTime.UtcNow.Date, cancellationToken: cancellationToken);

        return entities
            .Where(e => e.AllowInternalCandidates)
            .ToPublicDtoList()
            .ToList();
    }

    public async Task<IEnumerable<JobVacancySummaryDto>> GetByPositionAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _vacancyRepository.GetByPositionAsync(positionId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobVacancySummaryDto>> GetByHiringManagerAsync(Guid hiringManagerId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _vacancyRepository.GetByHiringManagerAsync(hiringManagerId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobVacancySummaryDto>> GetByRecruiterAsync(Guid recruiterId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _vacancyRepository.GetByRecruiterAsync(recruiterId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobVacancySummaryDto>> GetByRequisitionAsync(Guid requisitionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _vacancyRepository.GetByRequisitionAsync(requisitionId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobVacancySummaryDto>> GetWithDeadlineApproachingAsync(int daysAhead = 7, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _vacancyRepository.GetWithDeadlineApproachingAsync(daysAhead);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    // ── CRUD ─────────────────────────────────────────────────────────────────

    public async Task<JobVacancyDto> CreateAsync(CreateJobVacancyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var requisition = await _requisitionRepository.GetByIdAsync(createDto.StaffRequisitionId);
        if (requisition == null || requisition.TenantId != current)
            throw new ArgumentException($"Staff requisition with ID '{createDto.StaffRequisitionId}' not found.");

        var entity = createDto.ToEntity(current, createdByUserId);
        entity.PositionId = requisition.PositionId;
        entity.VacancyNumber = await _vacancyRepository.GetNextVacancyNumberAsync();
        entity.VacancyStatus = JobVacancyStatus.Draft;

        // Snapshot audience flags from the requisition so each vacancy can be overridden independently.
        entity.AllowInternalCandidates = requisition.AllowInternalCandidates;
        entity.AllowExternalCandidates = requisition.AllowExternalCandidates;

        await _vacancyRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job vacancy created: {VacancyNumber}", entity.VacancyNumber);
        return await ReloadDtoAsync(entity.Id);
    }

    public async Task<JobVacancyDto> UpdateAsync(UpdateJobVacancyDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);

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

            // Editing a Published vacancy back to Draft used to leave its adverts live, so a role
            // that was no longer approved stayed on the internal posting lists.
            await ExpirePostingsIfUnpublishedAsync(entity, statusBeforeEdit, JobVacancyStatus.Draft, updatedByUserId);

            _logger.LogInformation(
                "Job vacancy {VacancyNumber} reverted from {From} to Draft due to edit.",
                entity.VacancyNumber, statusBeforeEdit);
        }

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _vacancyRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job vacancy updated: {VacancyNumber}", entity.VacancyNumber);
        return await ReloadDtoAsync(entity.Id);
    }

    /// <summary>
    /// Re-reads a vacancy so a write response carries the same names a subsequent GET would.
    ///
    /// <para>Create used to map the entity it had just built, which has no navigations at all — so
    /// the response came back with an empty job title, position and requisition number, and the
    /// screen showed blanks until it refetched. Editing had the subtler version of the same
    /// problem: navigations loaded before the FKs changed are not re-queried by EF.</para>
    /// </summary>
    private async Task<JobVacancyDto> ReloadDtoAsync(Guid id)
    {
        var reloaded = await _vacancyRepository.GetByIdAsync(id);
        if (reloaded == null)
            throw new ArgumentException($"Job vacancy with ID '{id}' not found.");
        return reloaded.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.VacancyStatus != JobVacancyStatus.Draft)
            throw new InvalidOperationException("Only draft vacancies can be deleted.");

        await _vacancyRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job vacancy deleted: {VacancyNumber}", entity.VacancyNumber);
        return true;
    }

    // ── Workflow ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The transitions a vacancy is allowed to make.
    ///
    /// <para>Both status-changing entry points run through this. They did not used to:
    /// <c>TransitionAsync</c> refused to touch a Cancelled or Filled vacancy while
    /// <c>ChangeStatusAsync</c> — the same operation, reached through
    /// <c>PUT /{id}/status</c> — applied whatever status it was handed. That let a cancelled
    /// vacancy be resurrected, and let a Draft jump straight to Filled without ever being approved,
    /// published, or advertised. Two doors into one operation, one of them unlocked.</para>
    ///
    /// <para>Kept as an explicit map rather than a chain of ifs so the legal shape of the lifecycle
    /// is readable in one place: draft work, an approval gate, publication, then the hiring stages
    /// in order. Cancellation is reachable from anywhere live and is handled by
    /// <see cref="CloseAsync"/>; Filled and Cancelled are terminal.</para>
    /// </summary>
    private static readonly IReadOnlyDictionary<JobVacancyStatus, JobVacancyStatus[]> AllowedTransitions =
        new Dictionary<JobVacancyStatus, JobVacancyStatus[]>
        {
            [JobVacancyStatus.Draft]                 = new[] { JobVacancyStatus.PendingApproval, JobVacancyStatus.Approved, JobVacancyStatus.Cancelled },
            [JobVacancyStatus.PendingApproval]       = new[] { JobVacancyStatus.Approved, JobVacancyStatus.Rejected, JobVacancyStatus.Draft, JobVacancyStatus.Cancelled },
            [JobVacancyStatus.Rejected]              = new[] { JobVacancyStatus.Draft, JobVacancyStatus.Cancelled },
            [JobVacancyStatus.Approved]              = new[] { JobVacancyStatus.Published, JobVacancyStatus.Draft, JobVacancyStatus.Cancelled },
            [JobVacancyStatus.Published]             = new[] { JobVacancyStatus.ClosedForApplications, JobVacancyStatus.Shortlisting, JobVacancyStatus.Draft, JobVacancyStatus.Cancelled },
            [JobVacancyStatus.ClosedForApplications] = new[] { JobVacancyStatus.Shortlisting, JobVacancyStatus.Published, JobVacancyStatus.Cancelled },
            [JobVacancyStatus.Shortlisting]          = new[] { JobVacancyStatus.Interviewing, JobVacancyStatus.ClosedForApplications, JobVacancyStatus.Cancelled },
            [JobVacancyStatus.Interviewing]          = new[] { JobVacancyStatus.OfferStage, JobVacancyStatus.Shortlisting, JobVacancyStatus.Cancelled },
            [JobVacancyStatus.OfferStage]            = new[] { JobVacancyStatus.Filled, JobVacancyStatus.Interviewing, JobVacancyStatus.Cancelled },
            [JobVacancyStatus.Filled]                = Array.Empty<JobVacancyStatus>(),
            [JobVacancyStatus.Cancelled]             = Array.Empty<JobVacancyStatus>(),
        };

    private static void GuardTransition(JobVacancy entity, JobVacancyStatus toStatus)
    {
        if (entity.VacancyStatus == toStatus)
            throw new InvalidOperationException($"This vacancy is already {toStatus}.");

        if (!AllowedTransitions.TryGetValue(entity.VacancyStatus, out var allowed) || allowed.Length == 0)
            throw new InvalidOperationException(
                $"A vacancy that is {entity.VacancyStatus} has reached the end of its lifecycle and cannot change status.");

        if (!allowed.Contains(toStatus))
            throw new InvalidOperationException(
                $"A vacancy cannot go from {entity.VacancyStatus} to {toStatus}. " +
                $"From here it can only become: {string.Join(", ", allowed)}.");
    }

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
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = await GetOwnedAsync(dto.Id);

        if (entity.VacancyStatus == JobVacancyStatus.Cancelled || entity.VacancyStatus == JobVacancyStatus.Filled)
            throw new InvalidOperationException("A closed or filled vacancy cannot be edited.");

        GuardTransition(entity, dto.NewStatus);

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
        await ExpirePostingsIfUnpublishedAsync(entity, fromStatus, dto.NewStatus, userId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Job vacancy {VacancyNumber} transitioned from {From} to {To}",
            entity.VacancyNumber, fromStatus, dto.NewStatus);

        return await ReloadDtoAsync(entity.Id);
    }

    public async Task<bool> ChangeStatusAsync(ChangeJobVacancyStatusDto dto, Guid changedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(dto.VacancyId);

        GuardTransition(entity, dto.NewStatus);

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
        await ExpirePostingsIfUnpublishedAsync(entity, from, dto.NewStatus, changedByUserId);

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

        var existingPostings = (await _postingRepository.GetByVacancyIdAsync(entity.Id))
            .Where(p => p.TenantId == entity.TenantId);
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

    /// <summary>
    /// Takes the adverts down when a vacancy stops being published.
    ///
    /// <para>Publication created these postings; nothing retired them. So a vacancy that was
    /// cancelled, closed for applications, or edited back to Draft for re-approval left its
    /// postings sitting at <c>Published</c> and <c>IsActive</c> — still listed by
    /// <c>GET api/job-postings/active</c>, and still carrying whatever URL was syndicated to an
    /// external board. The public career portal never showed them because it filters on the
    /// vacancy's own status, which is exactly why the inconsistency was invisible from the outside
    /// while the internal lists kept advertising a role nobody was hiring for.</para>
    ///
    /// <para>Expiring rather than deleting: a posting is a record that the role was advertised on a
    /// channel, and re-publishing the vacancy raises fresh ones.</para>
    /// </summary>
    private async Task ExpirePostingsIfUnpublishedAsync(
        JobVacancy entity,
        JobVacancyStatus fromStatus,
        JobVacancyStatus toStatus,
        Guid changedByUserId)
    {
        if (fromStatus != JobVacancyStatus.Published || toStatus == JobVacancyStatus.Published)
            return;

        var live = (await _postingRepository.GetByVacancyIdAsync(entity.Id))
            .Where(p => p.TenantId == entity.TenantId
                     && (p.IsActive || p.Status == JobPostingStatus.Published))
            .ToList();

        foreach (var posting in live)
        {
            posting.Status     = JobPostingStatus.Expired;
            posting.IsActive   = false;
            posting.ExpiryDate = DateTime.UtcNow;
            posting.UpdatedAt  = DateTime.UtcNow;
            posting.UpdatedBy  = changedByUserId.ToString();
            await _postingRepository.UpdateAsync(posting);
        }

        if (live.Count > 0)
            _logger.LogInformation(
                "Expired {Count} job posting(s) for vacancy {VacancyNumber}, which left Published for {Status}.",
                live.Count, entity.VacancyNumber, toStatus);
    }

    public async Task<bool> CloseAsync(CloseJobVacancyDto dto, Guid closedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(dto.VacancyId);

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
        await ExpirePostingsIfUnpublishedAsync(entity, from, JobVacancyStatus.Cancelled, closedByUserId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job vacancy closed: {VacancyNumber}", entity.VacancyNumber);
        return true;
    }

    public async Task<bool> CloseForApplicationsAsync(CloseForApplicationsDto dto, Guid closedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(dto.VacancyId);

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
        await ExpirePostingsIfUnpublishedAsync(entity, from, JobVacancyStatus.ClosedForApplications, closedByUserId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job vacancy {VacancyNumber} closed for applications (reason: {Reason})",
            entity.VacancyNumber, dto.Reason);
        return true;
    }

    // ── Attachments ───────────────────────────────────────────────────────────

    /// <summary>
    /// Records an attachment against a vacancy. The file itself has already been scanned and
    /// registered by the controlled-upload gate in the controller — this only writes the row, from
    /// the stored document's own metadata rather than anything the caller typed.
    ///
    /// <para>This used to take a <c>CreateJobVacancyAttachmentDto</c> carrying a caller-supplied
    /// <c>filePath</c>, so the endpoint stored no file and recorded whatever path was posted to it.
    /// The DTO is gone rather than ignored, so it cannot drift back.</para>
    /// </summary>
    public async Task<JobVacancyAttachmentDto> AddAttachmentAsync(
        Guid jobVacancyId,
        Guid uploadedById,
        string fileName,
        long fileSize,
        string? description,
        CancellationToken cancellationToken = default,
        Guid? fileUploadRecordId = null,
        Guid? documentRecordId = null,
        Guid? documentVersionId = null)
    {
        var vacancy = await GetOwnedAsync(jobVacancyId);

        var entity = new JobVacancyAttachment
        {
            TenantId           = vacancy.TenantId,
            JobVacancyId       = jobVacancyId,
            FileName           = fileName,
            // The stored file is addressed by its upload/DMS ids, not by a path the client chose.
            FilePath           = string.Empty,
            FileSizeBytes      = fileSize,
            Description        = description,
            UploadDate         = DateTime.UtcNow,
            UploadedById       = uploadedById,
            FileUploadRecordId = fileUploadRecordId,
            DocumentRecordId   = documentRecordId,
            DocumentVersionId  = documentVersionId,
            CreatedBy          = uploadedById.ToString(),
        };

        await _attachmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Re-read so the response carries the uploader's name rather than a null navigation.
        var reloaded = (await _attachmentRepository.GetByVacancyIdAsync(jobVacancyId))
            .FirstOrDefault(a => a.Id == entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<IEnumerable<JobVacancyAttachmentDto>> GetAttachmentsAsync(Guid vacancyId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(vacancyId);
        var tenantId = GetTenantId();
        var entities = await _attachmentRepository.GetByVacancyIdAsync(vacancyId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<bool> DeleteAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAttachmentAsync(attachmentId);

        await _attachmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Status history (read-only) ────────────────────────────────────────────

    public async Task<IEnumerable<JobVacancyStatusHistoryDto>> GetStatusHistoryAsync(Guid vacancyId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(vacancyId);
        var tenantId = GetTenantId();
        var entities = await _statusHistoryRepository.GetByVacancyIdAsync(vacancyId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    // ── Shortlisting criteria ─────────────────────────────────────────────────

    public async Task<JobShortlistingCriteriaDto> AddCriteriaAsync(CreateJobShortlistingCriteriaDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedAsync(createDto.JobVacancyId);

        var entity = createDto.ToEntity(current, createdByUserId);
        await _criteriaRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await MarkApplicationScoresStaleAsync(createDto.JobVacancyId, cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobShortlistingCriteriaDto>> GetCriteriaAsync(Guid vacancyId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(vacancyId);
        var tenantId = GetTenantId();
        var entities = await _criteriaRepository.GetByVacancyIdAsync(vacancyId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<JobShortlistingCriteriaDto> UpdateCriteriaAsync(UpdateJobShortlistingCriteriaDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCriteriaAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _criteriaRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await MarkApplicationScoresStaleAsync(entity.JobVacancyId, cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteCriteriaAsync(Guid criteriaId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCriteriaAsync(criteriaId);

        var vacancyId = entity.JobVacancyId;
        await _criteriaRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await MarkApplicationScoresStaleAsync(vacancyId, cancellationToken);
        return true;
    }

    private async Task MarkApplicationScoresStaleAsync(Guid vacancyId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var applications = (await _applicationRepository.GetByVacancyIdAsync(vacancyId))
            .Where(a => a.TenantId == tenantId);
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
        await GetOwnedAsync(vacancyId);
        var tenantId = GetTenantId();
        var entity = await _statusHistoryRepository.GetLatestForVacancyAsync(vacancyId);
        return entity == null || entity.TenantId != tenantId ? null : entity.ToDto();
    }

    public async Task<IEnumerable<JobShortlistingCriteriaDto>> GetMandatoryCriteriaAsync(Guid vacancyId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(vacancyId);
        var tenantId = GetTenantId();
        var entities = await _criteriaRepository.GetMandatoryCriteriaAsync(vacancyId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    // Shortlist submit/review used to be implemented here as well as on JobApplicationService.
    // JobApplicationController calls the application service's pair, and nothing called these — a dead
    // second implementation of the same rules, minus the two guards the live pair has (an empty
    // shortlist cannot be submitted, and the submitter cannot approve their own). Deleted so it cannot
    // be wired up in place of the real one.

    // ── Public career portal ──────────────────────────────────────────────────

    public async Task<IEnumerable<PublicVacancyDto>> GetPublishedForExternalPortalAsync(
        Guid tenantId,
        string? searchTerm      = null,
        EmploymentType? empType = null,
        WorkMode? workMode      = null,
        CancellationToken cancellationToken = default)
    {
        // Status / deadline / employment-type / work-mode are all applied in SQL, scoped to the tenant.
        var entities = await _vacancyRepository.GetPublishedForPublicPortalAsync(
            tenantId, DateTime.UtcNow.Date, empType, workMode, cancellationToken);

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
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var vacancy = await _vacancyRepository.GetWithFullDetailsAsync(id);
        // Scope to the requesting tenant so a known cross-tenant vacancy id cannot be fetched anonymously.
        if (vacancy == null || vacancy.TenantId != tenantId || vacancy.VacancyStatus != JobVacancyStatus.Published)
            return null;
        return vacancy.ToPublicDto();
    }

    // ── Pipeline stage assignments ────────────────────────────────────────────

    public async Task<IEnumerable<VacancyPipelineStageAssignmentDto>> GetStageAssignmentsAsync(
        Guid vacancyId, CancellationToken ct = default)
    {
        await GetOwnedAsync(vacancyId);
        var tenantId = GetTenantId();
        var entities = await _stageAssignmentRepository.GetByVacancyIdAsync(vacancyId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<VacancyPipelineStageAssignmentDto> UpsertStageAssignmentAsync(
        CreateVacancyPipelineStageAssignmentDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedAsync(dto.JobVacancyId);

        var existing = await _stageAssignmentRepository.GetByVacancyAndStageAsync(dto.JobVacancyId, dto.PipelineStageId);
        if (existing != null && existing.TenantId != current)
            existing = null;
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
            TenantId                = current,
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
        var entity = await GetOwnedStageAssignmentAsync(dto.Id);

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
        var entity = await GetOwnedStageAssignmentAsync(dto.Id);
        RequireStageOwnership(entity, userId, "complete");

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
        var entity = await GetOwnedStageAssignmentAsync(dto.Id);
        RequireStageOwnership(entity, userId, "skip");

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
        if (entity == null || entity.TenantId != GetTenantId()) return false;
        await _stageAssignmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

