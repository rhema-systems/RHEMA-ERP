using System.Text.Json;
using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class JobApplicationService : IJobApplicationService
{
    private readonly IJobApplicationRepository _applicationRepository;
    private readonly IJobApplicationStageHistoryRepository _stageHistoryRepository;
    private readonly IJobApplicantTestResultRepository _testResultRepository;
    private readonly IJobApplicantCommunicationRepository _communicationRepository;
    private readonly IJobVacancyRepository _vacancyRepository;
    private readonly IJobPostingRepository _postingRepository;
    private readonly IShortlistReviewRepository _shortlistReviewRepository;
    private readonly IShortlistDecisionLogRepository _decisionLogRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IJobCandidateRepository _candidateRepository;
    private readonly IJobCandidateWorkHistoryRepository _workHistoryRepository;
    private readonly IJobCandidateQualificationRepository _qualificationRepository;
    private readonly IJobCandidateRefereeRepository _refereeRepository;
    private readonly IJobCandidateSkillRepository _skillRepository;
    private readonly IJobCandidateLanguageRepository _languageRepository;
    private readonly IApplicationSnapshotService _snapshotService;
    private readonly IApplicationPipelineService _pipelineService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JobApplicationService> _logger;
    private readonly IEmailService _email;
    private readonly ITemplatedEmailService _templatedEmail;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;

    public JobApplicationService(
        IJobApplicationRepository applicationRepository,
        IJobApplicationStageHistoryRepository stageHistoryRepository,
        IJobApplicantTestResultRepository testResultRepository,
        IJobApplicantCommunicationRepository communicationRepository,
        IJobVacancyRepository vacancyRepository,
        IJobPostingRepository postingRepository,
        IShortlistReviewRepository shortlistReviewRepository,
        IShortlistDecisionLogRepository decisionLogRepository,
        IEmployeeRepository employeeRepository,
        IJobCandidateRepository candidateRepository,
        IJobCandidateWorkHistoryRepository workHistoryRepository,
        IJobCandidateQualificationRepository qualificationRepository,
        IJobCandidateRefereeRepository refereeRepository,
        IJobCandidateSkillRepository skillRepository,
        IJobCandidateLanguageRepository languageRepository,
        IApplicationSnapshotService snapshotService,
        IApplicationPipelineService pipelineService,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<JobApplicationService> logger,
        IEmailService email,
        ITemplatedEmailService templatedEmail,
        ICentralDocumentRepositoryFileService centralDocuments)
    {
        _centralDocuments = centralDocuments;
        _applicationRepository = applicationRepository;
        _stageHistoryRepository = stageHistoryRepository;
        _testResultRepository = testResultRepository;
        _communicationRepository = communicationRepository;
        _vacancyRepository = vacancyRepository;
        _postingRepository = postingRepository;
        _shortlistReviewRepository = shortlistReviewRepository;
        _decisionLogRepository = decisionLogRepository;
        _employeeRepository = employeeRepository;
        _candidateRepository = candidateRepository;
        _workHistoryRepository = workHistoryRepository;
        _qualificationRepository = qualificationRepository;
        _refereeRepository = refereeRepository;
        _skillRepository = skillRepository;
        _languageRepository = languageRepository;
        _snapshotService    = snapshotService;
        _pipelineService    = pipelineService;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _email = email;
        _templatedEmail = templatedEmail;
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

    // An application owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<JobApplication> GetOwnedApplicationAsync(Guid id)
    {
        var entity = await _applicationRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Job application with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobVacancy> GetOwnedVacancyAsync(Guid id)
    {
        var entity = await _vacancyRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Vacancy '{id}' not found.");
        return entity;
    }

    private async Task<JobApplicantTestResult> GetOwnedTestResultAsync(Guid id)
    {
        var entity = await _testResultRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Test result with ID '{id}' not found.");
        return entity;
    }

    private async Task<ShortlistReview> GetOwnedReviewAsync(Guid id)
    {
        var entity = await _shortlistReviewRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Shortlist review '{id}' not found.");
        return entity;
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public async Task<JobApplicationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedApplicationAsync(id);
        return entity.ToDto();
    }

    public async Task<JobApplicationDto?> GetByApplicationNumberAsync(string applicationNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _applicationRepository.GetByApplicationNumberAsync(applicationNumber);
        return entity == null || entity.TenantId != tenantId ? null : entity.ToDto();
    }

    public async Task<JobApplicationDetailDto> GetWithFullDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _applicationRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Job application with ID '{id}' not found.");
        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<JobApplicationSummaryDto>> GetAllAsync(Guid? vacancyId = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        if (vacancyId.HasValue)
        {
            var byVacancy = await _applicationRepository.GetByVacancyIdAsync(vacancyId.Value);
            return byVacancy.Where(a => a.TenantId == tenantId).ToSummaryDtoList();
        }

        // Filtered in SQL, not in memory: the untenanted GetAllAsync() pulled every tenant's applications
        // back to the app server just to discard them.
        var entities = await _applicationRepository.GetQueryable()
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.ApplicationDate)
            .ToListAsync(cancellationToken);

        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<JobApplicationSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, Guid? vacancyId = null, CancellationToken cancellationToken = default)
    {
        // Clamp before use: pageNumber 0 makes Skip() negative (throws), and an unbounded pageSize lets
        // a caller pull the whole table with ?pageSize=1000000.
        (pageNumber, pageSize) = PagingGuard.Clamp(pageNumber, pageSize);

        var tenantId = GetTenantId();
        var query = _applicationRepository.GetQueryable().Where(a => a.TenantId == tenantId);

        if (vacancyId.HasValue)
            query = query.Where(a => a.JobVacancyId == vacancyId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.ApplicationDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<JobApplicationSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<JobApplicationSummaryDto>> GetByVacancyIdAsync(Guid vacancyId, CancellationToken cancellationToken = default)
    {
        await GetOwnedVacancyAsync(vacancyId);
        var tenantId = GetTenantId();
        var entities = await _applicationRepository.GetByVacancyIdAsync(vacancyId);
        return entities.Where(a => a.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobApplicationSummaryDto>> GetByCandidateIdAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _applicationRepository.GetByCandidateIdAsync(candidateId);
        return entities.Where(a => a.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobApplicationSummaryDto>> GetByStatusAsync(ApplicationStatus status, Guid? vacancyId = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _applicationRepository.GetByStatusAsync(status, vacancyId);
        return entities.Where(a => a.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobApplicationSummaryDto>> GetShortlistedAsync(Guid? vacancyId = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _applicationRepository.GetShortlistedAsync(vacancyId);
        return entities.Where(a => a.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobApplicationSummaryDto>> GetByCurrentStageAsync(Guid pipelineStageId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _applicationRepository.GetByCurrentStageAsync(pipelineStageId);
        return entities.Where(a => a.TenantId == tenantId).ToSummaryDtoList();
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public async Task<JobApplicationDto> CreateAsync(CreateJobApplicationDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        // Round 3, lane A: HR's "Record an application" (a walk-in, an agency submission). The
        // source is typed by hand; the advert, when named, must be this vacancy's.
        await GetOwnedVacancyAsync(createDto.JobVacancyId);
        var candidate = await _candidateRepository.GetByIdAsync(createDto.JobCandidateId);
        if (candidate is null || candidate.TenantId != current)
            throw new ArgumentException($"Candidate '{createDto.JobCandidateId}' not found.");
        await RequirePostingOfVacancyAsync(createDto.JobPostingId, createDto.JobVacancyId, current);

        var entity = createDto.ToEntity(current, createdByUserId);
        entity.ApplicationNumber = await _applicationRepository.GetNextApplicationNumberAsync(current);
        entity.Status = ApplicationStatus.New;
        entity.ApplicationDate = DateTime.UtcNow;

        await _applicationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job application created: {ApplicationNumber}", entity.ApplicationNumber);
        var created = await _applicationRepository.GetWithFullDetailsAsync(entity.Id);
        return (created ?? entity).ToDto();
    }

    /// <inheritdoc />
    public async Task<JobApplicationDto> UpdateSourceAsync(Guid id, UpdateJobApplicationSourceDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedApplicationAsync(id);
        if (!Enum.IsDefined(typeof(ApplicationSource), dto.Source))
            throw new InvalidOperationException($"'{(int)dto.Source}' is not an application source.");
        await RequirePostingOfVacancyAsync(dto.JobPostingId, entity.JobVacancyId, GetTenantId());

        entity.Source = dto.Source;
        entity.JobPostingId = dto.JobPostingId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();
        await _applicationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job application {ApplicationNumber} source corrected to {Source} (posting {PostingId})",
            entity.ApplicationNumber, dto.Source, dto.JobPostingId);
        var reread = await _applicationRepository.GetWithFullDetailsAsync(entity.Id);
        return (reread ?? entity).ToDto();
    }

    /// <summary>Round 3, lane A: an advert named on an application must be one of that vacancy's, and not removed.</summary>
    private async Task RequirePostingOfVacancyAsync(Guid? postingId, Guid vacancyId, Guid tenantId)
    {
        if (postingId is not { } pid) return;
        var posting = await _postingRepository.FirstOrDefaultAsync(p => p.Id == pid && p.TenantId == tenantId && !p.IsDeleted);
        if (posting is null || posting.JobVacancyId != vacancyId || posting.Status == JobPostingStatus.Removed)
            throw new InvalidOperationException("That advert does not belong to this vacancy, or has been removed.");
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedApplicationAsync(id);

        if (entity.Status is not (ApplicationStatus.Draft or ApplicationStatus.New))
            throw new InvalidOperationException("Only draft or newly received applications can be deleted.");

        await _applicationRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Workflow ──────────────────────────────────────────────────────────────

    public async Task<bool> ShortlistAsync(ShortlistApplicationDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedApplicationAsync(dto.ApplicationId);

        // Guard: terminal states cannot be shortlisted
        if (entity.Status is ApplicationStatus.Hired or ApplicationStatus.Withdrawn)
            throw new InvalidOperationException($"Cannot shortlist an application that is {entity.Status}.");

        if (entity.Status == ApplicationStatus.Shortlisted)
            throw new InvalidOperationException("Application is already shortlisted.");

        // Enforce shortlisting deadline
        var vacancy = await GetOwnedVacancyAsync(entity.JobVacancyId);
        if (vacancy.ShortlistingDeadline != null && DateTime.UtcNow > vacancy.ShortlistingDeadline.Value)
            throw new InvalidOperationException(
                $"The shortlisting deadline for this vacancy passed on {vacancy.ShortlistingDeadline.Value:d}. " +
                "Contact HR to extend or override the deadline.");

        entity.Status = ApplicationStatus.Shortlisted;
        entity.ShortlistingNotes = dto.ShortlistingNotes;
        entity.ShortlistedById = updatedByUserId;
        entity.ShortlistedDate = DateTime.UtcNow;
        entity.DecisionSource = ShortlistDecisionSource.Manual;

        await _applicationRepository.UpdateAsync(entity);

        // Immutable decision audit log
        await WriteDecisionLogAsync(entity, ShortlistDecisionType.Shortlisted, updatedByUserId, false, dto.ShortlistingNotes);

        // Communication record
        await WriteSystemCommunicationAsync(entity,
            "Application Shortlisted",
            $"Your application {entity.ApplicationNumber} has been shortlisted.");

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Application {ApplicationNumber} shortlisted by {UserId}",
            entity.ApplicationNumber, updatedByUserId);

        await UpdateVacancyCounterAsync(entity.JobVacancyId, v =>
        {
            v.ShortlistedCount++;
            // Changing the shortlist after approval/pending-approval invalidates the decision—reset to NotSubmitted
            if (v.ShortlistApprovalStatus == ShortlistApprovalStatus.Approved ||
                v.ShortlistApprovalStatus == ShortlistApprovalStatus.PendingApproval)
                v.ShortlistApprovalStatus = ShortlistApprovalStatus.NotSubmitted;
        }, cancellationToken);

        return true;
    }

    public async Task<bool> UnshortlistAsync(UnshortlistApplicationDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedApplicationAsync(dto.ApplicationId);

        if (entity.Status != ApplicationStatus.Shortlisted)
            throw new InvalidOperationException("Only shortlisted applications can be un-shortlisted.");

        entity.Status = ApplicationStatus.UnderReview;
        entity.ShortlistedDate = null;
        entity.ShortlistedById = null;
        entity.DecisionSource = null;
        entity.ShortlistingNotes = string.IsNullOrWhiteSpace(dto.Reason)
            ? entity.ShortlistingNotes
            : $"[Un-shortlisted by {updatedByUserId} on {DateTime.UtcNow:u}: {dto.Reason}]\n{entity.ShortlistingNotes}";

        await _applicationRepository.UpdateAsync(entity);
        await WriteDecisionLogAsync(entity, ShortlistDecisionType.Unshortlisted, updatedByUserId, false, dto.Reason);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Application {ApplicationNumber} un-shortlisted by {UserId}",
            entity.ApplicationNumber, updatedByUserId);
        await UpdateVacancyCounterAsync(entity.JobVacancyId, v =>
        {
            if (v.ShortlistedCount > 0) v.ShortlistedCount--;
            if (v.ShortlistApprovalStatus == ShortlistApprovalStatus.Approved)
                v.ShortlistApprovalStatus = ShortlistApprovalStatus.NotSubmitted;
        }, cancellationToken);
        return true;
    }

    public async Task<bool> WaitlistAsync(WaitlistApplicationDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedApplicationAsync(dto.ApplicationId);

        if (entity.Status is ApplicationStatus.Hired or ApplicationStatus.Withdrawn or ApplicationStatus.Rejected)
            throw new InvalidOperationException($"Cannot waitlist an application that is {entity.Status}.");

        entity.Status = ApplicationStatus.Waitlisted;
        entity.WaitlistedDate = DateTime.UtcNow;
        entity.WaitlistReason = dto.WaitlistReason;

        await _applicationRepository.UpdateAsync(entity);
        await WriteDecisionLogAsync(entity, ShortlistDecisionType.Waitlisted, updatedByUserId, false, dto.WaitlistReason);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Application {ApplicationNumber} waitlisted by {UserId}",
            entity.ApplicationNumber, updatedByUserId);
        return true;
    }

    public async Task<bool> RejectAsync(RejectApplicationDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedApplicationAsync(dto.ApplicationId);

        if (entity.Status is ApplicationStatus.Hired or ApplicationStatus.Withdrawn or ApplicationStatus.Rejected)
            throw new InvalidOperationException($"Cannot reject an application that is already {entity.Status}.");

        bool wasShortlisted = entity.Status == ApplicationStatus.Shortlisted;
        entity.Status = ApplicationStatus.Rejected;
        entity.RejectionReason = dto.RejectionReason;
        entity.RejectedById = updatedByUserId;
        entity.RejectedDate = DateTime.UtcNow;

        await _applicationRepository.UpdateAsync(entity);

        // Immutable decision audit log
        await WriteDecisionLogAsync(entity, ShortlistDecisionType.Rejected, updatedByUserId, false, dto.RejectionReason);

        // Auto-communication: rejection notice
        await WriteSystemCommunicationAsync(entity,
            "Application Outcome",
            $"Thank you for your application {entity.ApplicationNumber}. After careful consideration, we regret to inform you that your application has not been successful at this stage.");

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Application {ApplicationNumber} rejected by {UserId}",
            entity.ApplicationNumber, updatedByUserId);

        // Email #14 — Rejection notification is now a deliberate batch action via SendRejectionNotificationsAsync.

        if (wasShortlisted)
            await UpdateVacancyCounterAsync(entity.JobVacancyId, v => { if (v.ShortlistedCount > 0) v.ShortlistedCount--; }, cancellationToken);

        // Close pipeline stage row: application has left the pipeline as rejected
        await _pipelineService.CloseCurrentStageForExitAsync(
            entity.Id, JobApplicationStageExitReason.Rejected, updatedByUserId, cancellationToken);

        return true;
    }

    public async Task<bool> WithdrawAsync(WithdrawApplicationDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedApplicationAsync(dto.ApplicationId);

        if (entity.Status == ApplicationStatus.Hired)
            throw new InvalidOperationException("A hired application cannot be withdrawn.");

        bool wasShortlisted = entity.Status == ApplicationStatus.Shortlisted;
        entity.Status = ApplicationStatus.Withdrawn;
        entity.WithdrawalReason = dto.WithdrawalReason;
        entity.WithdrawnDate = DateTime.UtcNow;

        await _applicationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Application {ApplicationNumber} withdrawn", entity.ApplicationNumber);
        // Withdrawing a shortlisted candidate has to take them off the shortlist count too. Reject already
        // did this; withdraw did not, so the vacancy kept counting a candidate who had walked away.
        await UpdateVacancyCounterAsync(entity.JobVacancyId, v =>
        {
            v.ApplicationCount = Math.Max(0, v.ApplicationCount - 1);
            if (wasShortlisted && v.ShortlistedCount > 0) v.ShortlistedCount--;
        }, cancellationToken);

        // Close pipeline stage row: application has left the pipeline as withdrawn
        await _pipelineService.CloseCurrentStageForExitAsync(
            entity.Id, JobApplicationStageExitReason.Withdrawn, updatedByUserId, cancellationToken);

        return true;
    }

    /// <summary>
    /// Moves an application to a pipeline stage.
    ///
    /// <para>Delegates to <see cref="IApplicationPipelineService.MoveApplicationToStageAsync"/> rather
    /// than moving the application itself. This endpoint and <c>POST api/applications/move-stage</c>
    /// are two doors onto the same operation, and they had drifted badly: the pipeline service
    /// enforces terminal status, pipeline membership, stage order, <c>CanRepeat</c> and
    /// <c>MaxAttempts</c>, while this path enforced none of them — so a caller could put a rejected
    /// application straight into an Offer stage, or into a stage belonging to another vacancy's
    /// pipeline entirely, simply by choosing the older route. One writer, one state machine.</para>
    ///
    /// <para><c>dto.Notes</c> is kept as the entry note on the newly opened stage record. The old code
    /// also wrote it over the <i>outgoing</i> record's notes, overwriting why the application had
    /// entered the stage it was leaving.</para>
    /// </summary>
    public async Task<bool> MoveToStageAsync(MoveApplicationToStageDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedApplicationAsync(dto.ApplicationId);

        await _pipelineService.MoveApplicationToStageAsync(
            dto.ApplicationId, dto.PipelineStageId, updatedByUserId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            var opened = await _stageHistoryRepository.GetCurrentStageAsync(dto.ApplicationId);
            if (opened != null)
            {
                opened.Notes = dto.Notes;
                await _stageHistoryRepository.UpdateAsync(opened);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        _logger.LogInformation("Application {ApplicationNumber} moved to stage {StageId}", entity.ApplicationNumber, dto.PipelineStageId);

        // Email #6 — Under Review
        var candUr = await _candidateRepository.GetByIdAsync(entity.JobCandidateId);
        var vacUr  = await GetOwnedVacancyAsync(entity.JobVacancyId);
        await SendUnderReviewEmailAsync(
            candUr?.Email ?? string.Empty,
            candUr?.FullName ?? "Candidate",
            entity.ApplicationNumber,
            vacUr?.JobTitle ?? "the position");

        return true;
    }

    // ── Stage history ─────────────────────────────────────────────────────────

    public async Task<IEnumerable<JobApplicationStageHistoryDto>> GetStageHistoryAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        await GetOwnedApplicationAsync(applicationId);
        var tenantId = GetTenantId();
        var entities = await _stageHistoryRepository.GetByApplicationIdAsync(applicationId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    // ── Test results ──────────────────────────────────────────────────────────

    public async Task<JobApplicantTestResultDto> AddTestResultAsync(CreateJobApplicantTestResultDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedApplicationAsync(createDto.JobApplicationId);
        var entity = createDto.ToEntity(current, createdByUserId);
        await _testResultRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Advance pipeline: test result recorded → Assessment stage (no-op if no pipeline)
        await _pipelineService.AutoAdvanceToStageTypeAsync(
            entity.JobApplicationId, RecruitmentPipelineStageType.Assessment, createdByUserId, cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<JobApplicantTestResultDto>> GetTestResultsAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        await GetOwnedApplicationAsync(applicationId);
        var tenantId = GetTenantId();
        var entities = await _testResultRepository.GetByApplicationIdAsync(applicationId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<JobApplicantTestResultDto> UpdateTestResultAsync(UpdateJobApplicantTestResultDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedTestResultAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _testResultRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteTestResultAsync(Guid testResultId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedTestResultAsync(testResultId);

        await _testResultRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Communications ────────────────────────────────────────────────────────

    public async Task<JobApplicantCommunicationDto> AddCommunicationAsync(CreateJobApplicantCommunicationDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedApplicationAsync(createDto.JobApplicationId);
        var entity = createDto.ToEntity(current, createdByUserId);
        entity.SentAt = DateTime.UtcNow;

        await _communicationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobApplicantCommunicationDto>> GetCommunicationsAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        await GetOwnedApplicationAsync(applicationId);
        var tenantId = GetTenantId();
        var entities = await _communicationRepository.GetByApplicationIdAsync(applicationId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<JobApplicantTestResultDto>> GetTestResultsByVacancyAsync(Guid vacancyId, CancellationToken cancellationToken = default)
    {
        await GetOwnedVacancyAsync(vacancyId);
        var tenantId = GetTenantId();
        var entities = await _testResultRepository.GetByVacancyIdAsync(vacancyId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<JobApplicantTestResultDto>> GetTestResultsByTypeAsync(Guid applicationId, JobApplicantTestType testType, CancellationToken cancellationToken = default)
    {
        await GetOwnedApplicationAsync(applicationId);
        var tenantId = GetTenantId();
        var entities = await _testResultRepository.GetByTestTypeAsync(applicationId, testType);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<ApplicationAutoScoreDto> EvaluateApplicationScoreAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var application = await _applicationRepository.GetWithFullDetailsAsync(applicationId);
        if (application == null || application.TenantId != tenantId)
            throw new ArgumentException($"Job application '{applicationId}' not found.");

        return await EvaluateLoadedApplicationScoreAsync(application, cancellationToken);
    }

    public async Task<ApplicationAutoScoreDto> EvaluateLoadedApplicationScoreAsync(JobApplication application, CancellationToken cancellationToken = default)
    {
        if (application.TenantId != GetTenantId())
            throw new ArgumentException($"Job application '{application.Id}' not found.");

        var applicationId = application.Id;
        var vacancy = application.JobVacancy;
        var candidate = application.JobCandidate;

        // ── Build the scoring view ────────────────────────────────────────────
        // Prefer the immutable snapshot captured at submission time.
        // Fall back to the live candidate entity for legacy rows that predate the
        // snapshot feature.  The log line makes this visible in diagnostics.
        ScoringCandidateView scoringView;
        if (!string.IsNullOrEmpty(application.ProfileSnapshotJson))
        {
            try
            {
                var snap = JsonSerializer.Deserialize<ApplicationCandidateSnapshot>(
                    application.ProfileSnapshotJson);
                if (snap is not null)
                {
                    scoringView = ScoringCandidateView.FromSnapshot(snap);
                }
                else
                {
                    _logger.LogWarning(
                        "Profile snapshot for application {AppId} deserialised to null — falling back to live profile.",
                        applicationId);
                    scoringView = ScoringCandidateView.FromEntity(candidate, application);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Profile snapshot deserialisation failed for application {AppId} — falling back to live profile.",
                    applicationId);
                scoringView = ScoringCandidateView.FromEntity(candidate, application);
            }
        }
        else
        {
            _logger.LogDebug(
                "Application {AppId} has no profile snapshot (legacy row) — scoring against live profile.",
                applicationId);
            scoringView = ScoringCandidateView.FromEntity(candidate, application);
        }

        var liveCriteria = vacancy?.ShortlistingCriteria?.Where(c => !c.IsDeleted).ToList() ?? new List<JobShortlistingCriteria>();
        if (liveCriteria.Count == 0)
        {
            // Round 3, lane K (R-5 fix 3; § 3 defect 7): nothing was measured, so nothing is scored.
            // This used to write 100, so "auto-shortlist by score" on a vacancy with no criteria
            // admitted every applicant. A null score is never auto-shortlisted.
            application.AutoScore = null;
            application.AutoScoreBreakdown = null;
            application.ScoredAt = DateTime.UtcNow;
            application.ScoreIsStale = false;
            await _applicationRepository.UpdateAsync(application);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new ApplicationAutoScoreDto
            {
                ApplicationId = applicationId,
                AutoScore = null,
                HasCriteria = false,
                ScoredAt = application.ScoredAt.Value,
                AllMandatoryPassed = true,
                TotalWeight = 0m,
                MaxPossibleScore = 100m,
                Breakdown = new List<CriterionScoreResult>(),
            };
        }

        // Round 4, lane A. The Location criterion tests tree CONTAINMENT, which needs the
        // candidate's ancestor path. A snapshot written before round 4 carries neither id nor path;
        // one written since carries the id and — if the read that built it Included the navigation
        // — the path. Resolve the gap here, once per application, so EvaluateCriterion can stay
        // pure and synchronous.
        //
        // ⚠ Deliberately resolved from the LIVE tree. If the area has since been re-parented, the
        // live answer is the one a recruiter looking at the map today would give, and the frozen
        // path is preferred when present precisely so that a re-score of an old application is
        // stable. Both positions are defensible; the split is which question is being asked.
        if (scoringView.GeoAreaPath is null && scoringView.GeoAreaId is { } candidateAreaId)
            scoringView.GeoAreaPath = await ResolveGeoAreaPathAsync(candidateAreaId, cancellationToken);

        var breakdown = new List<CriterionScoreResult>();
        decimal totalWeight = 0m;
        decimal earnedScore = 0m;
        bool allMandatoryPassed = true;

        // Evaluate ALL criteria regardless of mandatory failures so the stored breakdown
        // is complete — required for recruiter review and algorithmic-decision audit trails
        foreach (var criterion in liveCriteria)
        {
            var result = EvaluateCriterion(criterion, scoringView);
            breakdown.Add(result);

            if (criterion.IsMandatory && !result.Passed)
                allMandatoryPassed = false;

            // A criterion the engine does not score (Other) is left out of the total: it neither
            // lifts nor lowers anybody. It used to pass everyone with full marks.
            if (!result.AutoEvaluated) continue;
            totalWeight += criterion.Weight;
            earnedScore += result.WeightedScore;
        }

        // Disqualify after the full loop so the breakdown covers every criterion
        if (!allMandatoryPassed)
        {
            application.AutoScore = 0m;
            application.AutoScoreBreakdown = JsonSerializer.Serialize(breakdown);
            application.ScoredAt = DateTime.UtcNow;
            application.ScoreIsStale = false;
            await _applicationRepository.UpdateAsync(application);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Application {AppId} scored 0 — one or more mandatory criteria not met.",
                applicationId);

            return new ApplicationAutoScoreDto
            {
                ApplicationId = applicationId,
                AutoScore = 0m,
                ScoredAt = application.ScoredAt.Value,
                AllMandatoryPassed = false,
                TotalWeight = totalWeight,
                MaxPossibleScore = 100m,
                Breakdown = breakdown,
            };
        }

        // ⚠ Round 4, lane A. Nothing measurable ⇒ NO SCORE, not full marks.
        //
        // This used to fall through to 100 whenever `totalWeight` came out zero — the same
        // inflation round 3 lane K removed from the branch above, reached one level down. There it
        // was "the vacancy states no criteria"; here it is "every criterion the vacancy states
        // turned out to be unevaluable", and the old code answered the two questions differently.
        //
        // It mattered little while `Other` was the only way to get here. It matters now: this slice
        // added three more exclusion paths — an empty criterion, an unanswerable numeric bound, and
        // a Location criterion listing areas against a candidate who has none — and the last is
        // ordinary. A vacancy screening on Greater Accra, scored against candidates who applied
        // through the public form and have no area on file, would have handed EVERY ONE OF THEM
        // 100 and put them at the top of the shortlist.
        //
        // A null score is never auto-shortlisted, which is the whole point: the recruiter is told
        // nothing could be measured rather than being shown a number that means the opposite.
        if (totalWeight <= 0)
        {
            application.AutoScore = null;
            application.AutoScoreBreakdown = JsonSerializer.Serialize(breakdown);
            application.ScoredAt = DateTime.UtcNow;
            application.ScoreIsStale = false;
            await _applicationRepository.UpdateAsync(application);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Application {AppId} has {Count} criteria but none could be evaluated — no score written.",
                applicationId, breakdown.Count);

            return new ApplicationAutoScoreDto
            {
                ApplicationId = applicationId,
                AutoScore = null,
                HasCriteria = true,
                ScoredAt = application.ScoredAt.Value,
                AllMandatoryPassed = allMandatoryPassed,
                TotalWeight = 0m,
                MaxPossibleScore = 100m,
                Breakdown = breakdown,
            };
        }

        // Normalise criterion score to 0–100
        decimal criterionScore = Math.Round(earnedScore / totalWeight * 100m, 2);

        // ── Test score integration ────────────────────────────────────────────
        decimal finalScore = criterionScore;
        int testWeight = vacancy.TestScoreWeight; // 0–100
        if (testWeight > 0 && application.TestResults.Any())
        {
            var scoredTests = application.TestResults
                .Where(t => t.Score.HasValue && t.MaxScore.HasValue && t.MaxScore > 0)
                .ToList();
            if (scoredTests.Any())
            {
                decimal avgTestPct = scoredTests
                    .Average(t => t.Score!.Value / t.MaxScore!.Value * 100m);
                finalScore = Math.Round(
                    criterionScore * (100m - testWeight) / 100m
                    + avgTestPct * testWeight / 100m, 2);
            }
        }

        // ── Internal candidate boost ──────────────────────────────────────────
        int boost = vacancy.InternalCandidateBoostPoints;
        if (boost > 0 && application.IsInternalCandidate)
            finalScore = Math.Min(100m, Math.Round(finalScore + boost, 2));

        application.AutoScore = finalScore;
        application.AutoScoreBreakdown = JsonSerializer.Serialize(breakdown);
        application.ScoredAt = DateTime.UtcNow;
        application.ScoreIsStale = false;
        await _applicationRepository.UpdateAsync(application);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Application {AppId} scored {Score}/100 across {Count} criteria.",
            applicationId, finalScore, breakdown.Count);

        return new ApplicationAutoScoreDto
        {
            ApplicationId = applicationId,
            AutoScore = finalScore,
            ScoredAt = application.ScoredAt.Value,
            AllMandatoryPassed = allMandatoryPassed,
            TotalWeight = totalWeight,
            MaxPossibleScore = 100m,
            Breakdown = breakdown,
        };
    }

    public async Task<IEnumerable<ApplicationAutoScoreDto>> EvaluateAllScoresForVacancyAsync(Guid vacancyId, CancellationToken cancellationToken = default)
    {
        await GetOwnedVacancyAsync(vacancyId);
        var tenantId = GetTenantId();
        // Single bulk fetch — all navigation properties required for scoring loaded in one query
        var applications = await _applicationRepository.GetAllWithFullDetailsByVacancyIdAsync(vacancyId);
        var scoreable = applications
            .Where(a => a.TenantId == tenantId
                     && a.Status != ApplicationStatus.Withdrawn
                     && a.Status != ApplicationStatus.Rejected)
            .ToList();

        var results = new List<ApplicationAutoScoreDto>(scoreable.Count);
        foreach (var app in scoreable)
        {
            var result = await EvaluateLoadedApplicationScoreAsync(app, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    // ── Private scoring helpers ───────────────────────────────────────────────

    /// <summary>
    /// Thin adapter that presents candidate data to <see cref="EvaluateCriterion"/> in a
    /// uniform shape regardless of whether the data came from a live <see cref="JobCandidate"/>
    /// entity or a deserialized <see cref="ApplicationCandidateSnapshot"/>.
    ///
    /// All string collections are pre-normalised to lower-case so criterion comparisons
    /// are O(1) hash-set lookups rather than repeated ToLowerInvariant() allocations.
    /// </summary>
    private sealed class ScoringCandidateView
    {
        // ── Scalars ───────────────────────────────────────────────────────────
        public decimal  YearsOfExperience    { get; init; }
        public DateTime DateOfBirth          { get; init; }
        public Gender   Gender               { get; init; }
        public string   City                 { get; init; } = string.Empty;

        /// <summary>The candidate's administrative area, when they have one on file.</summary>
        public Guid?    GeoAreaId            { get; init; }

        /// <summary>
        /// The materialised ancestor path of <see cref="GeoAreaId"/>, in the form
        /// <c>/root/child/leaf</c> and INCLUDING the area's own id as the last segment.
        /// </summary>
        /// <remarks>
        /// ⚠ Settable after construction, unlike everything else here, because the path is the one
        /// field that may need a database read: a snapshot written before round 4 carries the id
        /// but not the path, and the orchestrator back-fills it from the live tree before scoring
        /// rather than making <c>EvaluateCriterion</c> async. Null means "no area, or the area
        /// could not be read" — both fall through to the free-text city.
        /// </remarks>
        public string?  GeoAreaPath          { get; set; }

        // ── Pre-normalised sets for O(1) lookups ──────────────────────────────
        public HashSet<string> SkillNames          { get; init; } = new();
        public HashSet<Guid>   SkillIds            { get; init; } = new();
        public HashSet<string> CertificationNames  { get; init; } = new();
        public HashSet<string> QualificationNames  { get; init; } = new();
        public HashSet<Guid>   QualificationIds    { get; init; } = new();
        public HashSet<string> LanguageNames       { get; init; } = new();
        public HashSet<Guid>   LanguageIds         { get; init; } = new();

        // ── Factory: from live entity ─────────────────────────────────────────
        public static ScoringCandidateView FromEntity(JobCandidate c, JobApplication app) =>
            new()
            {
                YearsOfExperience   = app.YearsOfExperience ?? c.TotalYearsExperience ?? 0,
                DateOfBirth         = c.DateOfBirth,
                Gender              = c.Gender,
                City                = c.City?.ToLowerInvariant() ?? string.Empty,
                GeoAreaId           = c.GeoAreaId,
                // Only set when the caller Included the navigation; the orchestrator back-fills it
                // from the live tree otherwise.
                GeoAreaPath         = c.GeoArea?.Path,
                SkillNames          = c.Skills
                                        .Select(s => s.SkillName.ToLowerInvariant())
                                        .ToHashSet(),
                SkillIds            = c.Skills
                                        .Where(s => s.SkillId.HasValue)
                                        .Select(s => s.SkillId!.Value)
                                        .ToHashSet(),
                CertificationNames  = c.Skills
                                        .Where(s => s.IsCertified && !string.IsNullOrEmpty(s.CertificationName))
                                        .Select(s => s.CertificationName!.ToLowerInvariant())
                                        .ToHashSet(),
                QualificationNames  = c.Qualifications
                                        .Select(q => (q.Qualification?.Name ?? q.QualificationFreeText ?? string.Empty)
                                                     .ToLowerInvariant())
                                        .Where(n => n.Length > 0)
                                        .ToHashSet(),
                QualificationIds    = c.Qualifications
                                        .Where(q => q.QualificationId.HasValue)
                                        .Select(q => q.QualificationId!.Value)
                                        .ToHashSet(),
                LanguageNames       = c.Languages
                                        .Select(l => l.LanguageName.ToLowerInvariant())
                                        .ToHashSet(),
                LanguageIds         = c.Languages
                                        .Where(l => l.LanguageId.HasValue)
                                        .Select(l => l.LanguageId!.Value)
                                        .ToHashSet(),
            };

        // ── Factory: from snapshot ────────────────────────────────────────────
        public static ScoringCandidateView FromSnapshot(ApplicationCandidateSnapshot snap) =>
            new()
            {
                YearsOfExperience   = snap.YearsOfExperience ?? snap.TotalYearsExperience ?? 0,
                DateOfBirth         = snap.DateOfBirth,
                Gender              = snap.Gender,
                City                = snap.City?.ToLowerInvariant() ?? string.Empty,
                GeoAreaId           = snap.GeoAreaId,
                // Null on every snapshot written before round 4, and on the anonymous apply path
                // when the candidate row had no area loaded. Back-filled by the orchestrator.
                GeoAreaPath         = snap.GeoAreaPath,
                SkillNames          = snap.Skills
                                        .Select(s => s.SkillName.ToLowerInvariant())
                                        .ToHashSet(),
                SkillIds            = snap.Skills
                                        .Where(s => s.SkillId.HasValue)
                                        .Select(s => s.SkillId!.Value)
                                        .ToHashSet(),
                CertificationNames  = snap.Skills
                                        .Where(s => s.IsCertified && !string.IsNullOrEmpty(s.CertificationName))
                                        .Select(s => s.CertificationName!.ToLowerInvariant())
                                        .ToHashSet(),
                QualificationNames  = snap.Qualifications
                                        .Select(q => q.NormalisedName)
                                        .Where(n => n.Length > 0)
                                        .ToHashSet(),
                QualificationIds    = snap.Qualifications
                                        .Where(q => q.QualificationId.HasValue)
                                        .Select(q => q.QualificationId!.Value)
                                        .ToHashSet(),
                LanguageNames       = snap.Languages
                                        .Select(l => l.NormalisedName)
                                        .ToHashSet(),
                LanguageIds         = snap.Languages
                                        .Where(l => l.LanguageId.HasValue)
                                        .Select(l => l.LanguageId!.Value)
                                        .ToHashSet(),
            };
    }

    private static CriterionScoreResult EvaluateCriterion(
        JobShortlistingCriteria criterion,
        ScoringCandidateView    view)
    {
        bool passed;
        decimal rawScore;
        string? notes = null;
        bool autoEvaluated = true;

        switch (criterion.Type)
        {
            case JobShortlistingCriteriaType.YearsOfExperience:
            {
                (passed, rawScore, notes, autoEvaluated) = EvaluateNumericCriterion(criterion, view.YearsOfExperience, "year(s) of experience");
                break;
            }

            case JobShortlistingCriteriaType.Qualification:
            case JobShortlistingCriteriaType.EducationLevel:
            {
                // ID-first: the legacy single catalogue FK still passes immediately when the candidate holds it.
                if (criterion.RequiredQualificationId.HasValue && view.QualificationIds.Contains(criterion.RequiredQualificationId.Value))
                {
                    passed   = true;
                    rawScore = 1m;
                    notes    = $"Qualification matched by catalogue ID ({criterion.RequiredQualificationId.Value}).";
                    break;
                }
                (passed, rawScore, notes, autoEvaluated) = EvaluateListCriterion(criterion, view.QualificationIds, view.QualificationNames, "qualification");
                notes += $" Candidate qualifications: {string.Join(", ", view.QualificationNames)}.";
                break;
            }

            case JobShortlistingCriteriaType.Skill:
            {
                if (criterion.RequiredSkillId.HasValue && view.SkillIds.Contains(criterion.RequiredSkillId.Value))
                {
                    passed   = true;
                    rawScore = 1m;
                    notes    = $"Skill matched by catalogue ID ({criterion.RequiredSkillId.Value}).";
                    break;
                }
                (passed, rawScore, notes, autoEvaluated) = EvaluateListCriterion(criterion, view.SkillIds, view.SkillNames, "skill");
                break;
            }

            case JobShortlistingCriteriaType.Certification:
            {
                // A candidate's certificate carries a name, not a catalogue id (lane C1 added the
                // number, body and expiry; the id is a follow-on), so a catalogue-picked value
                // matches on the mirrored catalogue name — which is exactly why the label is mirrored.
                (passed, rawScore, notes, autoEvaluated) = EvaluateListCriterion(criterion, new HashSet<Guid>(), view.CertificationNames, "certification");
                break;
            }

            case JobShortlistingCriteriaType.Age:
            {
                // ⚠ Round 4, lane A. DateOfBirth is a NON-NULLABLE DateTime on both the candidate
                // and the snapshot, so a candidate HR typed in without one carries DateTime.MinValue
                // — and this line then computed an age of roughly 2,026 years, which passed every
                // minimum and failed every maximum. An unknown age is not an age: the criterion is
                // left out of the score the way Other is, rather than being answered with a number
                // nobody could act on.
                if (view.DateOfBirth == default || view.DateOfBirth.Year <= 1)
                {
                    passed = true;
                    rawScore = 0m;
                    autoEvaluated = false;
                    notes = "The candidate has no date of birth on file, so their age cannot be "
                          + "computed. This criterion is left out of the score.";
                    break;
                }

                decimal ageYears = (decimal)((DateTime.UtcNow - view.DateOfBirth).TotalDays / 365.25);
                (passed, rawScore, notes, autoEvaluated) = EvaluateNumericCriterion(criterion, ageYears, "years old");
                break;
            }

            case JobShortlistingCriteriaType.Gender:
            {
                // The accepted genders are enum members (or "Any"); the candidate's is compared as a
                // member name, never through the text strategies (R-5 fix 6).
                var accepted = RequiredLabels(criterion);
                var mine = view.Gender.ToString().ToLowerInvariant();
                if (accepted.Count == 0)
                {
                    // Round 4, lane A — the same inflation as the empty list criterion. This used
                    // to award full marks to everybody for a criterion stating no preference, which
                    // raised every percentage without separating anyone.
                    passed = true;
                    rawScore = 0m;
                    autoEvaluated = false;
                    notes = "No gender specified; this criterion measures nothing and is left out "
                          + "of the score entirely.";
                }
                else
                {
                    passed = accepted.Contains("any") || accepted.Contains(mine);
                    rawScore = passed ? 1m : 0m;
                    notes = $"Accepted: {string.Join(", ", accepted)}; candidate: {mine}. Informs the score only — never mandatory (D-7).";
                }
                break;
            }

            case JobShortlistingCriteriaType.Language:
            {
                (passed, rawScore, notes, autoEvaluated) = EvaluateListCriterion(criterion, view.LanguageIds, view.LanguageNames, "language");
                notes += $" Candidate languages: {string.Join(", ", view.LanguageNames)}.";
                break;
            }

            case JobShortlistingCriteriaType.Location:
            {
                (passed, rawScore, notes, autoEvaluated) = EvaluateLocationCriterion(criterion, view);
                break;
            }

            default:
            {
                // Other, or a legacy row with no type. Not auto-evaluated: it passes (a person
                // judges it), contributes NOTHING, and its weight is left out of the total. It used
                // to pass with full marks — a mandatory Other could disqualify nobody (R-5 fix 1;
                // § 3 defect 8).
                passed = true;
                rawScore = 0m;
                autoEvaluated = false;
                notes = "Not auto-evaluated; judged by a person off-system. Contributes nothing to the score.";
                break;
            }
        }

        decimal weightedScore = autoEvaluated ? rawScore * criterion.Weight : 0m;

        return new CriterionScoreResult
        {
            CriteriaId = criterion.Id,
            CriteriaName = criterion.CriteriaName,
            IsMandatory = criterion.IsMandatory,
            Type = criterion.Type,
            Weight = criterion.Weight,
            Passed = passed,
            RawScore = Math.Round(rawScore, 4),
            WeightedScore = Math.Round(weightedScore, 4),
            Notes = notes,
            AutoEvaluated = autoEvaluated,
        };
    }

    /// <summary>
    /// The accepted items of a list criterion, ids first and labels second (round 3, lane K; register
    /// row R-8). The value rows are the truth; the legacy comma-separated text is read for rows
    /// written before the lane. A row with a catalogue id matches the candidate's catalogue link
    /// exactly, or its mirrored label under the strategy — so a candidate who typed the same name
    /// still matches. Duplicates collapse.
    /// </summary>
    /// <summary>
    /// The Location criterion — round 4, lane A. Areas from the shared geography tree, matched by
    /// containment, with the pre-tree free-text path kept for candidates who have no area.
    /// </summary>
    /// <remarks>
    /// <para><b>What this replaced, and why each part was wrong.</b> The old arm compared the
    /// criterion's typed labels against the candidate's typed city with a raw
    /// <c>string.Contains</c>:</para>
    /// <list type="number">
    ///   <item><description><b>Only two operators were honoured.</b> Anything that was not
    ///   <c>Equals</c> fell through to substring — so <c>NotEquals</c> <i>inverted nothing</i> and a
    ///   "not Accra" criterion passed Accra candidates, and <c>In</c> behaved as Contains.</description></item>
    ///   <item><description><b>Matching was one-directional</b>, unlike <c>MatchesValue</c> which
    ///   every other list criterion uses. "Accra" matched "Greater Accra"; "Greater Accra" did not
    ///   match "Accra". Which way round it worked was an accident of who typed what.</description></item>
    ///   <item><description><b><c>MatchMode</c> and <c>MatchStrategy</c> were ignored.</b> A
    ///   Location criterion set to "all required" behaved as "any", silently.</description></item>
    ///   <item><description><b>The score was binary.</b> Every other list criterion gives partial
    ///   credit for matching some of several values; this one gave all or nothing.</description></item>
    ///   <item><description><b>An empty criterion scored FULL MARKS</b> — "defaulting to pass" with
    ///   <c>rawScore = 1</c>, lifting every candidate's percentage for a criterion that measured
    ///   nothing. It is now treated the way <c>Other</c> is: excluded from the total entirely, so it
    ///   neither lifts nor lowers anybody.</description></item>
    /// </list>
    ///
    /// <para><b>Containment, not string comparison.</b> <c>GeoArea.Path</c> is
    /// <c>/root/child/leaf</c> and includes the area's own id as its last segment, so a candidate is
    /// inside an accepted area when that area's id appears anywhere in their path. That is what
    /// makes "Greater Accra" match somebody recorded in Tema — the thing the free-text version could
    /// never do.</para>
    ///
    /// <para><b>The text fallback is not legacy debt.</b> Most countries have no geography scheme
    /// loaded, and the anonymous apply form collects a typed city by design. A candidate with no
    /// area is matched on their city, through the same bidirectional <c>MatchesValue</c> the rest of
    /// the engine uses, against the criterion's mirrored labels.</para>
    /// </remarks>
    private static (bool passed, decimal rawScore, string? notes, bool autoEvaluated)
        EvaluateLocationCriterion(JobShortlistingCriteria criterion, ScoringCandidateView view)
    {
        var values = criterion.Values.Where(v => !v.IsDeleted).OrderBy(v => v.SortOrder).ToList();
        var areaIds = values
            .Where(v => v.Kind == ShortlistingValueKind.GeoArea && v.ReferenceId.HasValue)
            .Select(v => v.ReferenceId!.Value)
            .Distinct()
            .ToList();
        var labels = RequiredLabels(criterion);

        if (areaIds.Count == 0 && labels.Count == 0)
            return (true, 0m, "No location specified; this criterion measures nothing and is left "
                            + "out of the score entirely.", false);

        var op = criterion.ComparisonOperator ?? ShortlistingComparisonOperator.In;
        var negated = op == ShortlistingComparisonOperator.NotEquals;

        int matched;
        int considered;
        string basis;

        if (areaIds.Count > 0 && !string.IsNullOrWhiteSpace(view.GeoAreaPath))
        {
            // Exact means "this precise tier"; every other operator means "in, or anywhere under".
            var exactTierOnly = op == ShortlistingComparisonOperator.Equals;
            var path = view.GeoAreaPath!;
            matched = areaIds.Count(id => exactTierOnly
                ? view.GeoAreaId == id
                : PathContainsArea(path, id));
            considered = areaIds.Count;
            basis = exactTierOnly
                ? $"candidate's own area against {considered} listed area(s)"
                : $"candidate's area and its ancestors against {considered} listed area(s)";
        }
        else if (labels.Count > 0)
        {
            // No area on either side — fall back to the city, bidirectionally, honouring the
            // criterion's own match strategy like every other list criterion does.
            matched = labels.Count(l => MatchesValue(view.City, l, criterion.MatchStrategy));
            considered = labels.Count;
            basis = $"candidate's typed city '{view.City}' against {considered} listed name(s) "
                  + $"[{criterion.MatchStrategy}]";
        }
        else
        {
            // Nothing to compare on either side: no usable area, and no label either.
            //
            // ⚠ This is NOT the "candidate has no area" case, which is the common one and is
            // handled by the text branch above — every GeoArea value carries the area's name
            // mirrored onto it at save, so a candidate with only a typed city is measured against
            // that name and can genuinely miss. Scoring such a miss as zero is deliberate:
            // excluding it would let a candidate with no address outrank one who demonstrably does
            // not match, which is the "rewards missing data" fault G-13.2 removed from the talent
            // pool. The round 4 harness asserts both halves — the miss AND a typed city that hits.
            //
            // So this branch is reached only by a row the write path will not produce: values that
            // carry neither a reference nor a label. Kept for rows written before the write path
            // enforced that, and for a corrupt one. Excluded rather than failed, because a record
            // that says nothing should not be read as a candidate saying no.
            return (true, 0m,
                "This location criterion carries no usable area or name, so it could not be "
                + "evaluated. It is left out of the score.", false);
        }

        var allRequired = criterion.MatchMode == MandatoryMatchMode.AllRequired;
        var positive = allRequired ? matched == considered : matched > 0;
        var passed = negated ? matched == 0 : positive;

        // Partial credit, on the same terms as every other list criterion. A negated criterion is
        // binary by nature: "not in these places" is satisfied or it is not.
        var rawScore = negated
            ? (passed ? 1m : 0m)
            : (considered > 0 ? (decimal)matched / considered : 0m);

        var mode = negated ? "none may match" : allRequired ? "all required" : "any sufficient";
        var notes = $"{matched}/{considered} matched — {basis} [{mode}].";

        return (passed, rawScore, notes, true);
    }

    /// <summary>
    /// Whether <paramref name="path"/> — a <c>GeoArea.Path</c> of the form <c>/a/b/c</c> — passes
    /// through <paramref name="areaId"/> at any tier, including as its own leaf.
    /// </summary>
    /// <remarks>
    /// The trailing slash is appended to both sides so the last segment is delimited like every
    /// other one. Without it, a path ending in the area's id would not match, and a leaf candidate
    /// would fail a criterion naming their own area.
    /// </remarks>
    private static bool PathContainsArea(string path, Guid areaId)
        => (path.EndsWith('/') ? path : path + '/')
            .Contains($"/{areaId}/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The ancestor path of one area, read from the live tree — the back-fill for applications
    /// whose snapshot predates round 4.
    /// </summary>
    /// <remarks>
    /// Returns null for an area this tenant cannot see or that has been hard-removed, which the
    /// caller reads as "no area" and falls back to the typed city for. A soft-deleted area still
    /// answers: the candidate genuinely lived there, and the criterion that named it is what the
    /// <c>ShortlistingCriteriaGeoAreaConsumer</c> probe protects.
    /// </remarks>
    private async Task<string?> ResolveGeoAreaPathAsync(Guid geoAreaId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var path = await _unitOfWork.Repository<ErpSystem.Core.Entities.Reference.GeoArea>()
            .GetQueryable()
            .Where(a => a.Id == geoAreaId && a.TenantId == tenantId)
            .Select(a => a.Path)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    /// <remarks>
    /// ⚠ Returns <c>autoEvaluated</c> as of round 4, lane A. An EMPTY criterion used to return
    /// <c>(true, 1m, "defaulting to pass")</c> — full marks, for a criterion that measures nothing,
    /// lifting every candidate's percentage and flattening the ranking the score exists to produce.
    /// It is now treated exactly as <c>Other</c> is: excluded from both the earned score and the
    /// total weight, so it neither lifts nor lowers anybody. This is the same repair the vacancy's
    /// "no criteria at all" branch already had, one level down.
    /// </remarks>
    private static (bool passed, decimal rawScore, string notes, bool autoEvaluated) EvaluateListCriterion(
        JobShortlistingCriteria criterion, HashSet<Guid> candidateIds, HashSet<string> candidateNames, string noun)
    {
        var items = new List<(Guid? Id, string Label)>();
        foreach (var v in criterion.Values.Where(v => !v.IsDeleted).OrderBy(v => v.SortOrder))
        {
            var label = v.Label.Trim().ToLowerInvariant();
            if (items.Any(i => (v.ReferenceId.HasValue && i.Id == v.ReferenceId) || (label.Length > 0 && i.Label == label))) continue;
            items.Add((v.ReferenceId, label));
        }
        foreach (var label in SplitValues(criterion.RequiredValue))
        {
            if (items.Any(i => i.Label == label)) continue;
            items.Add((null, label));
        }

        if (items.Count == 0)
            return (true, 0m,
                $"No {noun} specified; this criterion measures nothing and is left out of the score "
                + "entirely.", false);

        int matched = items.Count(i =>
            (i.Id is Guid id && candidateIds.Contains(id))
            || (i.Label.Length > 0 && candidateNames.Any(c => MatchesValue(c, i.Label, criterion.MatchStrategy))));
        bool passed = criterion.MatchMode == MandatoryMatchMode.AllRequired
            ? matched == items.Count
            : matched > 0;
        decimal rawScore = (decimal)matched / items.Count;
        string notes = $"{matched}/{items.Count} required {noun}(s) matched "
                     + $"[{(criterion.MatchMode == MandatoryMatchMode.AllRequired ? "all required" : "any sufficient")}, {criterion.MatchStrategy}; ids first, names second].";
        return (passed, rawScore, notes, true);
    }

    /// <summary>The accepted labels of a criterion, lower-cased: value rows first, legacy text second.</summary>
    private static List<string> RequiredLabels(JobShortlistingCriteria criterion)
    {
        var labels = criterion.Values.Where(v => !v.IsDeleted).OrderBy(v => v.SortOrder)
            .Select(v => v.Label.Trim().ToLowerInvariant()).Where(l => l.Length > 0).ToList();
        foreach (var label in SplitValues(criterion.RequiredValue))
            if (!labels.Contains(label)) labels.Add(label);
        return labels;
    }

    /// <remarks>
    /// ⚠ Returns <c>autoEvaluated</c> as of round 4, lane A, for the same reason the list evaluator
    /// does: a criterion the engine cannot answer must be excluded from the total, not scored zero.
    /// Scoring it zero would make an unanswerable criterion *lower* the candidate's percentage,
    /// which is the mirror image of the bug being fixed.
    /// </remarks>
    private static (bool passed, decimal rawScore, string notes, bool autoEvaluated) EvaluateNumericCriterion(
        JobShortlistingCriteria criterion,
        decimal candidateValue,
        string unit)
    {
        decimal min = criterion.MinValue ?? 0;
        decimal max = criterion.MaxValue ?? decimal.MaxValue;
        var op = criterion.ComparisonOperator ?? ShortlistingComparisonOperator.Between;

        // ⚠ Round 4, lane A. "Less than" with no ceiling compared against decimal.MaxValue and so
        // PASSED EVERY CANDIDATE — a criterion that reads as a restriction and restricted nobody.
        // The write path refuses a numeric criterion with neither bound, but not one carrying only
        // the bound the chosen operator does not use, so the hole was reachable from the form.
        // Treat it the way an empty criterion is treated: measured nothing, so scores nothing.
        var ceilingNeeded = op is ShortlistingComparisonOperator.LessThan
                                or ShortlistingComparisonOperator.LessThanOrEqual;
        if (ceilingNeeded && criterion.MaxValue is null)
            return (true, 0m,
                $"Candidate: {candidateValue:F1} {unit}; the criterion asks for less than a maximum "
                + "it does not state, so it could not be evaluated and is left out of the score.",
                false);

        bool passed = op switch
        {
            ShortlistingComparisonOperator.GreaterThan => candidateValue > min,
            ShortlistingComparisonOperator.GreaterThanOrEqual => candidateValue >= min,
            ShortlistingComparisonOperator.LessThan => candidateValue < max,
            ShortlistingComparisonOperator.LessThanOrEqual => candidateValue <= max,
            ShortlistingComparisonOperator.Equals => candidateValue == min,
            _ => candidateValue >= min && (criterion.MaxValue == null || candidateValue <= max),
        };

        // Partial credit for a near miss, on EITHER side (round 3, lane K; R-5 fix 7). It used to be
        // asymmetric: too little experience scored a fraction, too much scored nothing at all.
        decimal rawScore;
        if (passed)
        {
            rawScore = 1m;
        }
        else if (op is ShortlistingComparisonOperator.Equals)
        {
            rawScore = 0m;
        }
        else if (candidateValue < min)
        {
            rawScore = min > 0 ? Math.Min(candidateValue / min, 0.8m) : 0m;
        }
        else
        {
            // Above the ceiling: the closer to it, the more of the 0.8 cap.
            rawScore = criterion.MaxValue.HasValue && candidateValue > 0 ? Math.Min(max / candidateValue, 0.8m) : 0m;
        }

        string label = op switch
        {
            ShortlistingComparisonOperator.GreaterThan => $"> {min}",
            ShortlistingComparisonOperator.GreaterThanOrEqual => $">= {min}",
            ShortlistingComparisonOperator.LessThan => $"< {max}",
            ShortlistingComparisonOperator.LessThanOrEqual => $"<= {max}",
            ShortlistingComparisonOperator.Equals => $"= {min}",
            _ => criterion.MaxValue.HasValue ? $"{min}–{max}" : $">= {min}",
        };
        string notes = $"Candidate: {candidateValue:F1} {unit}; required {label}.";
        return (passed, rawScore, notes, true);
    }

    // ── Public CV upload tickets ─────────────────────────────────────────────

    /// <summary>How long an unclaimed CV upload stays claimable.</summary>
    private static readonly TimeSpan CvUploadTicketLifetime = TimeSpan.FromHours(2);

    /// <summary>
    /// Issues a single-use ticket for a CV that has already passed the controlled-upload gate.
    /// </summary>
    /// <remarks>
    /// The caller receives an opaque random token; only its SHA-256 hash is stored. The ticket
    /// table is readable by anything with database access, and a raw token is a bearer credential
    /// — storing it would mean anyone who could read the table could attach a stranger's CV to
    /// their own application.
    /// </remarks>
    public async Task<PublicCvUploadTicketDto> MintCvUploadTicketAsync(
        Guid tenantId,
        Guid vacancyId,
        Guid fileUploadRecordId,
        string originalFileName,
        string? contentType,
        long fileSize,
        CancellationToken cancellationToken = default)
    {
        var token = GenerateUploadToken();
        var ticket = new PublicCvUploadTicket
        {
            // Explicit: this flow is anonymous, so there is no tenant claim to stamp from.
            TenantId = tenantId,
            FileUploadRecordId = fileUploadRecordId,
            JobVacancyId = vacancyId,
            TokenHash = HashUploadToken(token),
            OriginalFileName = originalFileName,
            ContentType = contentType,
            FileSize = fileSize,
            ExpiresAtUtc = DateTime.UtcNow.Add(CvUploadTicketLifetime),
            CreatedBy = "external-portal"
        };

        await _unitOfWork.Repository<PublicCvUploadTicket>().AddAsync(ticket);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new PublicCvUploadTicketDto
        {
            UploadToken = token,
            FileName = ticket.OriginalFileName,
            FileSize = ticket.FileSize,
            ExpiresAtUtc = ticket.ExpiresAtUtc
        };
    }

    /// <summary>
    /// Resolves an unclaimed ticket for this tenant and vacancy, or null when no token was supplied.
    /// </summary>
    /// <remarks>
    /// A stale, replayed, cross-tenant or cross-vacancy token is indistinguishable from a wrong one
    /// to the caller — all produce the same message. The tenant and vacancy predicates are the
    /// binding that stops a token minted against one advert being spent on another.
    /// </remarks>
    private async Task<PublicCvUploadTicket?> ResolveCvUploadTicketAsync(
        string? token,
        Guid tenantId,
        Guid vacancyId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var hash = HashUploadToken(token.Trim());
        var now = DateTime.UtcNow;

        var ticket = await _unitOfWork.Repository<PublicCvUploadTicket>()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.JobVacancyId == vacancyId &&
                item.TokenHash == hash &&
                item.ClaimedAtUtc == null &&
                item.ExpiresAtUtc > now &&
                !item.IsDeleted);

        return ticket ?? throw new InvalidOperationException(
            "Your CV upload has expired or was already used. Please upload your CV again.");
    }

    /// <summary>
    /// Catalogues a claimed CV in the central DMS and records the link on the candidate.
    /// </summary>
    /// <remarks>
    /// Best-effort by design. The application and the candidate's
    /// <c>CvFileUploadRecordId</c> are already committed, so the file is downloadable through
    /// the authorizing endpoints whether or not this succeeds; failing the request here would
    /// cost the candidate a submission over a catalogue entry.
    /// </remarks>
    private async Task RegisterCandidateCvAsync(
        Guid candidateId,
        PublicCvUploadTicket cvTicket,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        try
        {
            var candidate = await _candidateRepository.GetByIdAsync(candidateId);
            if (candidate is null || candidate.TenantId != tenantId)
                return;

            var link = await _centralDocuments.RegisterAsync(
                new CentralDocumentRepositoryRegistration
                {
                    TenantId = tenantId,
                    ActorUserId = ControlledFileUploadActors.PublicPortalAnonymous,
                    ActorName = "public-career-portal",
                    FileUploadRecordId = cvTicket.FileUploadRecordId,
                    SourceModule = "HR",
                    SourceLabel = "Recruitment candidate CV",
                    SourceEntityType = nameof(JobCandidate),
                    SourceRecordId = candidateId,
                    SourceRecordReference = candidate.CandidateNumber,
                    Title = cvTicket.OriginalFileName,
                    DocumentType = "CV",
                    AccessProfile = "HR restricted",
                    ChangeSummary = "CV submitted through the public careers portal."
                },
                cancellationToken);

            candidate.CvDocumentRecordId = link.DocumentRecordId;
            candidate.CvDocumentVersionId = link.DocumentVersionId;
            await _candidateRepository.UpdateAsync(candidate);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Could not register the CV for candidate {CandidateId} in the central DMS. " +
                "The file remains available through the candidate's upload record.",
                candidateId);
        }
    }

    private static string GenerateUploadToken()
        => Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    private static string HashUploadToken(string token)
        => Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(token)))
            .ToLowerInvariant();

    // ── Concurrency-safe vacancy counter update ─────────────────────────────

    /// <summary>
    /// Applies <paramref name="mutate"/> to the vacancy's counter columns and saves.
    /// Retries up to 3 times on <see cref="DbUpdateConcurrencyException"/> caused by the
    /// RowVersion optimistic concurrency token, reloading the fresh row before each retry.
    /// </summary>
    /// <remarks>
    /// This only ever maintains a denormalised counter, and every caller has already
    /// committed the owning application change before calling in. A conflict that survives
    /// the retries is therefore logged and swallowed here rather than thrown: letting it
    /// escape would report failure for work that actually succeeded.
    /// </remarks>
    private async Task UpdateVacancyCounterAsync(
        Guid vacancyId,
        Action<JobVacancy> mutate,
        CancellationToken cancellationToken = default)
    {
        const int maxRetries = 3;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var vacancy = await _vacancyRepository.GetByIdAsync(vacancyId);
                if (vacancy is null) return;
                mutate(vacancy);
                await _vacancyRepository.UpdateAsync(vacancy);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (attempt == maxRetries)
                {
                    _logger.LogError(ex,
                        "Vacancy counter update for {VacancyId} failed after {Max} retries.",
                        vacancyId, maxRetries);
                    return;
                }

                _logger.LogWarning(
                    "Concurrency conflict updating vacancy counter for {VacancyId}. Attempt {Attempt}/{Max}.",
                    vacancyId, attempt + 1, maxRetries);

                // A reload can itself throw when the row was deleted concurrently; that must
                // not escape and mask this loop's own error handling.
                try
                {
                    foreach (var entry in ex.Entries.Where(e => e.Entity is JobVacancy))
                        await entry.ReloadAsync(cancellationToken);
                }
                catch (Exception reloadEx)
                {
                    _logger.LogWarning(reloadEx,
                        "Reload failed while updating the counter for vacancy {VacancyId}.",
                        vacancyId);
                    return;
                }
            }
        }
    }

    private static List<string> SplitValues(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? new List<string>()
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Select(v => v.ToLowerInvariant())
                 .ToList();

    // ── Value match strategy helpers ──────────────────────────────────────────

    /// <summary>
    /// Returns true when <paramref name="candidateValue"/> satisfies the match for
    /// <paramref name="requiredTerm"/> under the given <paramref name="strategy"/>.
    /// Both inputs are expected to be already lower-cased.
    /// </summary>
    private static bool MatchesValue(string candidateValue, string requiredTerm, ValueMatchStrategy strategy)
        => strategy switch
        {
            ValueMatchStrategy.Contains => candidateValue.Contains(requiredTerm, StringComparison.Ordinal)
                                        || requiredTerm.Contains(candidateValue, StringComparison.Ordinal),
            ValueMatchStrategy.Fuzzy    => FuzzyMatches(candidateValue, requiredTerm),
            _                           => candidateValue == requiredTerm,  // Exact (default)
        };

    /// <summary>
    /// Fuzzy match: passes when token overlap score ≥ 0.5, or when Levenshtein
    /// edit-distance ≤ 2 for short strings (≤ 20 chars each).
    /// </summary>
    private static bool FuzzyMatches(string a, string b)
    {
        var tokensA = TokeniseForFuzzy(a);
        var tokensB = TokeniseForFuzzy(b);
        if (tokensA.Count > 0 && tokensB.Count > 0)
        {
            int shared = tokensA.Count(t => tokensB.Contains(t));
            double overlapScore = (double)shared / Math.Max(tokensA.Count, tokensB.Count);
            if (overlapScore >= 0.5) return true;
        }
        // Levenshtein fallback for short strings
        if (a.Length <= 20 && b.Length <= 20 && EditDistance(a, b) <= 2) return true;
        return false;
    }

    private static HashSet<string> TokeniseForFuzzy(string s)
        => s.Split([' ', '-', '_', '/', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Iterative Levenshtein distance, O(min(n,m)) space.</summary>
    private static int EditDistance(string s, string t)
    {
        int n = s.Length, m = t.Length;
        if (n == 0) return m;
        if (m == 0) return n;
        var prev = new int[m + 1];
        var curr = new int[m + 1];
        for (int j = 0; j <= m; j++) prev[j] = j;
        for (int i = 1; i <= n; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= m; j++)
                curr[j] = s[i - 1] == t[j - 1]
                    ? prev[j - 1]
                    : 1 + Math.Min(prev[j - 1], Math.Min(prev[j], curr[j - 1]));
            (prev, curr) = (curr, prev);
        }
        return prev[m];
    }

    // ── Decision log + communication helpers ─────────────────────────────────

    private async Task WriteDecisionLogAsync(
        JobApplication application,
        ShortlistDecisionType decisionType,
        Guid decidedByUserId,
        bool isAutoDecision,
        string? notes)
    {
        var log = new ShortlistDecisionLog
        {
            Id = Guid.NewGuid(),
            TenantId = application.TenantId,
            JobApplicationId = application.Id,
            ApplicationNumber = application.ApplicationNumber,
            DecisionType = decisionType,
            DecisionById = decidedByUserId,
            DecisionAt = DateTime.UtcNow,
            AutoScoreAtDecision = application.AutoScore,
            IsAutoDecision = isAutoDecision,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = decidedByUserId.ToString(),
        };
        await _decisionLogRepository.AddAsync(log);
    }

    private async Task WriteSystemCommunicationAsync(
        JobApplication application,
        string subject,
        string body)
    {
        var comm = new JobApplicantCommunication
        {
            Id = Guid.NewGuid(),
            TenantId = application.TenantId,
            JobApplicationId = application.Id,
            Type = JobApplicantCommunicationType.PortalNotification,
            Direction = JobApplicantCommunicationDirection.System,
            Subject = subject,
            Body = body,
            SentAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "SYSTEM",
        };

        await _communicationRepository.AddAsync(comm);
    }

    // ── Bulk operations ───────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the subset of <paramref name="applicationIds"/> that actually belongs to
    /// <paramref name="vacancyId"/>, recording the rest as skipped.
    ///
    /// <para>The bulk routes are vacancy-scoped but their ids arrive in the body, and nothing checked the
    /// two agreed — so "reject everyone I did not shortlist for vacancy A" would happily reject
    /// applications on vacancy B if their ids were passed. Reported per item rather than failing the whole
    /// batch, which is how every other item-level problem in these operations is reported.</para>
    /// </summary>
    private async Task<List<Guid>> ScopeToVacancyAsync(
        Guid vacancyId, IEnumerable<Guid> applicationIds, RecruitmentBulkOperationResultDto result)
    {
        await GetOwnedVacancyAsync(vacancyId);

        var tenantId = GetTenantId();
        var owned = (await _applicationRepository.GetByVacancyIdAsync(vacancyId))
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.Id)
            .ToHashSet();

        var scoped = new List<Guid>();
        foreach (var appId in applicationIds)
        {
            if (owned.Contains(appId))
            {
                scoped.Add(appId);
                continue;
            }

            result.Skipped++;
            result.Results.Add(new RecruitmentBulkOperationItemResult
            {
                ApplicationId = appId,
                Success = false,
                Message = "This application does not belong to the vacancy named in the request.",
            });
        }
        return scoped;
    }

    public async Task<RecruitmentBulkOperationResultDto> BulkShortlistAsync(
        Guid vacancyId, BulkShortlistDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var result = new RecruitmentBulkOperationResultDto();
        foreach (var appId in await ScopeToVacancyAsync(vacancyId, dto.ApplicationIds, result))
        {
            try
            {
                await ShortlistAsync(new ShortlistApplicationDto
                {
                    ApplicationId = appId,
                    ShortlistingNotes = dto.ShortlistingNotes,
                }, updatedByUserId, cancellationToken);
                result.Succeeded++;
                result.Results.Add(new RecruitmentBulkOperationItemResult { ApplicationId = appId, Success = true });
            }
            catch (Exception ex)
            {
                result.Skipped++;
                result.Results.Add(new RecruitmentBulkOperationItemResult
                {
                    ApplicationId = appId,
                    Success = false,
                    Message = ex.Message,
                });
            }
        }
        return result;
    }

    public async Task<RecruitmentBulkOperationResultDto> BulkRejectAsync(
        Guid vacancyId, BulkRejectDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var result = new RecruitmentBulkOperationResultDto();
        foreach (var appId in await ScopeToVacancyAsync(vacancyId, dto.ApplicationIds, result))
        {
            try
            {
                await RejectAsync(new RejectApplicationDto
                {
                    ApplicationId = appId,
                    RejectionReason = dto.RejectionReason,
                }, updatedByUserId, cancellationToken);
                result.Succeeded++;
                result.Results.Add(new RecruitmentBulkOperationItemResult { ApplicationId = appId, Success = true });
            }
            catch (Exception ex)
            {
                result.Skipped++;
                result.Results.Add(new RecruitmentBulkOperationItemResult
                {
                    ApplicationId = appId,
                    Success = false,
                    Message = ex.Message,
                });
            }
        }
        return result;
    }

    public async Task<RecruitmentBulkOperationResultDto> AutoShortlistByScoreAsync(
        AutoShortlistByScoreDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        // Enforce shortlisting deadline (same guard as ShortlistAsync)
        var vacancyForDeadline = await GetOwnedVacancyAsync(dto.VacancyId);
        if (vacancyForDeadline.ShortlistingDeadline != null && DateTime.UtcNow > vacancyForDeadline.ShortlistingDeadline.Value)
            throw new InvalidOperationException(
                $"The shortlisting deadline for this vacancy passed on {vacancyForDeadline.ShortlistingDeadline.Value:d}. " +
                "Contact HR to extend or override the deadline.");

        var tenantId = GetTenantId();
        var applications = await _applicationRepository.GetByVacancyIdAsync(dto.VacancyId);
        var eligible = applications
            .Where(a => a.TenantId == tenantId
                     && a.Status != ApplicationStatus.Withdrawn
                     && a.Status != ApplicationStatus.Rejected
                     && a.Status != ApplicationStatus.Shortlisted
                     && a.Status != ApplicationStatus.Hired
                     && a.AutoScore.HasValue
                     && !a.ScoreIsStale
                     && a.AutoScore.Value >= dto.MinScore)
            .ToList();

        if (dto.RequireAllMandatoryPassed)
        {
            // Parse the breakdown to check AllMandatoryPassed flag — stored inside JSON score breakdown
            eligible = eligible.Where(a =>
            {
                if (string.IsNullOrEmpty(a.AutoScoreBreakdown)) return true;
                try
                {
                    var breakdown = JsonSerializer.Deserialize<List<CriterionScoreResult>>(a.AutoScoreBreakdown);
                    return breakdown?.Where(c => c.IsMandatory).All(c => c.Passed) ?? true;
                }
                catch { return false; }
            }).ToList();
        }

        var result = new RecruitmentBulkOperationResultDto();
        foreach (var app in eligible)
        {
            try
            {
                app.Status = ApplicationStatus.Shortlisted;
                app.ShortlistedById = updatedByUserId;
                app.ShortlistedDate = DateTime.UtcNow;
                app.DecisionSource = ShortlistDecisionSource.AutoScoreThreshold;
                app.ShortlistingNotes = dto.ShortlistingNotes;
                await _applicationRepository.UpdateAsync(app);
                await WriteDecisionLogAsync(app, ShortlistDecisionType.AutoShortlisted, updatedByUserId, true, dto.ShortlistingNotes);
                result.Succeeded++;
                result.Results.Add(new RecruitmentBulkOperationItemResult { ApplicationId = app.Id, Success = true });
            }
            catch (Exception ex)
            {
                result.Skipped++;
                result.Results.Add(new RecruitmentBulkOperationItemResult
                {
                    ApplicationId = app.Id,
                    Success = false,
                    Message = ex.Message,
                });
            }
        }

        if (result.Succeeded > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var delta = result.Succeeded;
            await UpdateVacancyCounterAsync(dto.VacancyId, v =>
            {
                v.ShortlistedCount += delta;
                // Any in-flight approval (Submitted or Approved) is invalidated when new
                // candidates are added — reset so the approver re-reviews the full list.
                if (v.ShortlistApprovalStatus == ShortlistApprovalStatus.Approved ||
                    v.ShortlistApprovalStatus == ShortlistApprovalStatus.PendingApproval)
                    v.ShortlistApprovalStatus = ShortlistApprovalStatus.NotSubmitted;
            }, cancellationToken);
        }

        return result;
    }

    public async Task<RecruitmentBulkOperationResultDto> SendShortlistNotificationsAsync(
        Guid vacancyId, CancellationToken cancellationToken = default)
    {
        var vacancy = await GetOwnedVacancyAsync(vacancyId);

        var tenantId = GetTenantId();
        var applications = await _applicationRepository.GetByVacancyIdAsync(vacancyId);
        var shortlisted = applications
            .Where(a => a.TenantId == tenantId && a.Status == ApplicationStatus.Shortlisted && a.ShortlistNotificationSentAt == null)
            .ToList();

        var result = new RecruitmentBulkOperationResultDto();
        var sentAt = DateTime.UtcNow;
        foreach (var app in shortlisted)
        {
            try
            {
                var candidate = await _candidateRepository.GetByIdAsync(app.JobCandidateId);
                await SendShortlistedEmailAsync(
                    candidate?.Email ?? string.Empty,
                    candidate?.FullName ?? "Candidate",
                    app.ApplicationNumber,
                    vacancy.JobTitle ?? "the position");
                app.ShortlistNotificationSentAt = sentAt;
                await _applicationRepository.UpdateAsync(app);
                result.Succeeded++;
                result.Results.Add(new RecruitmentBulkOperationItemResult { ApplicationId = app.Id, Success = true });
            }
            catch (Exception ex)
            {
                result.Skipped++;
                result.Results.Add(new RecruitmentBulkOperationItemResult
                {
                    ApplicationId = app.Id,
                    Success = false,
                    Message = ex.Message,
                });
            }
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Shortlist notifications sent for vacancy {VacancyId}: {Sent} sent, {Skipped} skipped.",
            vacancyId, result.Succeeded, result.Skipped);

        return result;
    }

    public async Task<RecruitmentBulkOperationResultDto> SendRejectionNotificationsAsync(
        Guid vacancyId, CancellationToken cancellationToken = default)
    {
        var vacancy = await GetOwnedVacancyAsync(vacancyId);

        var tenantId = GetTenantId();
        var applications = await _applicationRepository.GetByVacancyIdAsync(vacancyId);
        var rejected = applications
            .Where(a => a.TenantId == tenantId && a.Status == ApplicationStatus.Rejected && a.RejectionNotificationSentAt == null)
            .ToList();

        var result = new RecruitmentBulkOperationResultDto();
        var sentAt = DateTime.UtcNow;
        foreach (var app in rejected)
        {
            try
            {
                var candidate = await _candidateRepository.GetByIdAsync(app.JobCandidateId);
                await SendRejectedEmailAsync(
                    candidate?.Email ?? string.Empty,
                    candidate?.FullName ?? "Candidate",
                    app.ApplicationNumber,
                    vacancy.JobTitle ?? "the position",
                    app.RejectionReason);
                app.RejectionNotificationSentAt = sentAt;
                await _applicationRepository.UpdateAsync(app);
                result.Succeeded++;
                result.Results.Add(new RecruitmentBulkOperationItemResult { ApplicationId = app.Id, Success = true });
            }
            catch (Exception ex)
            {
                result.Skipped++;
                result.Results.Add(new RecruitmentBulkOperationItemResult
                {
                    ApplicationId = app.Id,
                    Success = false,
                    Message = ex.Message,
                });
            }
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Rejection notifications sent for vacancy {VacancyId}: {Sent} sent, {Skipped} skipped.",
            vacancyId, result.Succeeded, result.Skipped);

        return result;
    }

    // ── Shortlist dashboard ───────────────────────────────────────────────────

    public async Task<ShortlistSummaryDto> GetShortlistSummaryAsync(
        Guid vacancyId, CancellationToken cancellationToken = default)
    {
        var vacancy = await GetOwnedVacancyAsync(vacancyId);

        var tenantId = GetTenantId();
        var applications = await _applicationRepository.GetByVacancyIdAsync(vacancyId);
        var all = applications.Where(a => a.TenantId == tenantId).ToList();
        var scored = all.Where(a => a.AutoScore.HasValue && a.ScoredAt.HasValue).ToList();

        return new ShortlistSummaryDto
        {
            VacancyId = vacancyId,
            TotalApplications = all.Count,
            Scored = scored.Count,
            StaleScores = all.Count(a => a.ScoreIsStale),
            NeverScored = all.Count(a => !a.ScoredAt.HasValue),
            Shortlisted = all.Count(a => a.Status == ApplicationStatus.Shortlisted),
            Waitlisted = all.Count(a => a.Status == ApplicationStatus.Waitlisted),
            Rejected = all.Count(a => a.Status == ApplicationStatus.Rejected),
            Withdrawn = all.Count(a => a.Status == ApplicationStatus.Withdrawn),
            HighestScore = scored.Any() ? scored.Max(a => a.AutoScore) : null,
            LowestScore = scored.Any() ? scored.Min(a => a.AutoScore) : null,
            AverageScore = scored.Any() ? scored.Average(a => a.AutoScore!.Value) : null,
            IsShortlistDeadlinePassed = vacancy.ShortlistingDeadline.HasValue
                && DateTime.UtcNow > vacancy.ShortlistingDeadline.Value,
            IsShortlistApproved = vacancy.ShortlistApprovalStatus == ShortlistApprovalStatus.Approved,
            ApprovalStatus = vacancy.ShortlistApprovalStatus,
        };
    }

    public async Task<CandidateComparisonDto> GetCandidateComparisonAsync(
        Guid vacancyId, IEnumerable<Guid> applicationIds, CancellationToken cancellationToken = default)
    {
        // GetOwnedVacancyAsync loads through GetByIdAsync, which does NOT include ShortlistingCriteria —
        // only GetWithFullDetailsAsync does. Reading them off the wrong load left `criteria` empty, so the
        // comparison matrix came back with no columns and every cell defaulted to "failed, 0.00": the whole
        // screen rendered blank whatever the candidates had actually scored.
        var tenantId = GetTenantId();
        var vacancy = await _vacancyRepository.GetWithFullDetailsAsync(vacancyId);
        if (vacancy == null || vacancy.TenantId != tenantId)
            throw new ArgumentException($"Vacancy '{vacancyId}' not found.");

        var appIdList = applicationIds.ToList();
        var allApps = await _applicationRepository.GetByVacancyIdAsync(vacancyId);
        var selectedApps = appIdList.Any()
            ? allApps.Where(a => a.TenantId == tenantId && appIdList.Contains(a.Id)).ToList()
            : allApps.Where(a => a.TenantId == tenantId && a.Status == ApplicationStatus.Shortlisted).ToList();

        // The Include carries no IsDeleted filter, so a removed criterion would come back as a column
        // scoring everyone zero.
        var criteria = vacancy.ShortlistingCriteria?.Where(c => !c.IsDeleted).OrderBy(c => c.CriteriaName).ToList()
                       ?? new List<JobShortlistingCriteria>();
        var criteriaMapping = criteria
            .Select(c => new JobShortlistingCriteriaDto
            {
                Id = c.Id,
                JobVacancyId = c.JobVacancyId,
                CriteriaName = c.CriteriaName,
                Description = c.Description,
                Type = c.Type,
                IsMandatory = c.IsMandatory,
                Weight = c.Weight,
            })
            .ToList();

        var rows = new List<CandidateComparisonRowDto>();
        foreach (var app in selectedApps)
        {
            List<CriterionScoreResult>? breakdown = null;
            if (!string.IsNullOrEmpty(app.AutoScoreBreakdown))
            {
                try { breakdown = JsonSerializer.Deserialize<List<CriterionScoreResult>>(app.AutoScoreBreakdown); }
                catch { /* ignore malformed */ }
            }

            var cells = criteria.Select(c =>
            {
                var cr = breakdown?.FirstOrDefault(b => b.CriteriaId == c.Id);
                return new CriterionComparisonCellDto
                {
                    CriteriaId = c.Id,
                    CriteriaName = c.CriteriaName,
                    IsMandatory = c.IsMandatory,
                    Weight = c.Weight,
                    Passed = cr?.Passed ?? false,
                    RawScore = cr?.RawScore ?? 0m,
                    WeightedScore = cr?.WeightedScore ?? 0m,
                    Notes = cr?.Notes,
                };
            }).ToList();

            rows.Add(new CandidateComparisonRowDto
            {
                ApplicationId = app.Id,
                CandidateName = app.JobCandidate?.FullName ?? string.Empty,
                ApplicationNumber = app.ApplicationNumber,
                TotalScore = app.AutoScore,
                CriterionScores = cells,
            });
        }

        return new CandidateComparisonDto
        {
            VacancyId = vacancyId,
            Criteria = criteriaMapping,
            Candidates = rows,
        };
    }

    // ── Shortlist approval (delegated from vacancy) ───────────────────────────

    public async Task<bool> SubmitShortlistForApprovalAsync(
        SubmitShortlistForApprovalDto dto, Guid submittedByUserId, CancellationToken cancellationToken = default)
    {
        var vacancy = await GetOwnedVacancyAsync(dto.VacancyId);

        if (vacancy.ShortlistApprovalStatus == ShortlistApprovalStatus.PendingApproval)
            throw new InvalidOperationException("Shortlist is already pending approval.");

        if (vacancy.ShortlistApprovalStatus == ShortlistApprovalStatus.Approved)
            throw new InvalidOperationException("Shortlist is already approved.");

        // There has to be something to approve. Without this an empty shortlist could be sent up,
        // approved, and the vacancy would read as "shortlist approved" with nobody on it.
        var tenantId = GetTenantId();
        var shortlistedCount = (await _applicationRepository.GetByVacancyIdAsync(dto.VacancyId))
            .Count(a => a.TenantId == tenantId && a.Status == ApplicationStatus.Shortlisted);
        if (shortlistedCount == 0)
            throw new InvalidOperationException(
                "No applications have been shortlisted for this vacancy, so there is nothing to approve.");

        vacancy.ShortlistApprovalStatus = ShortlistApprovalStatus.PendingApproval;
        vacancy.ShortlistSubmittedAt = DateTime.UtcNow;
        vacancy.ShortlistSubmittedById = submittedByUserId;
        vacancy.ShortlistApprovalNotes = dto.Notes;

        await _vacancyRepository.UpdateAsync(vacancy);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Shortlist for vacancy {VacancyId} submitted for approval by {UserId}",
            dto.VacancyId, submittedByUserId);
        return true;
    }

    public async Task<bool> ReviewShortlistApprovalAsync(
        ReviewShortlistApprovalDto dto, Guid reviewedByUserId, CancellationToken cancellationToken = default)
    {
        var vacancy = await GetOwnedVacancyAsync(dto.VacancyId);

        if (vacancy.ShortlistApprovalStatus != ShortlistApprovalStatus.PendingApproval)
            throw new InvalidOperationException("Shortlist is not currently pending approval.");

        // The recruiter who sent the shortlist up cannot be the one who signs it off. Same rule the
        // workflow engine applies as `preventInitiatorApproval`; this approval predates the engine and
        // is driven straight off the vacancy, so it has to enforce it itself.
        if (vacancy.ShortlistSubmittedById.HasValue && vacancy.ShortlistSubmittedById.Value == reviewedByUserId)
            throw new InvalidOperationException(
                "You submitted this shortlist for approval, so you cannot approve or reject it yourself.");

        vacancy.ShortlistApprovalStatus = dto.Approved
            ? ShortlistApprovalStatus.Approved
            : ShortlistApprovalStatus.Rejected;
        vacancy.ShortlistApprovedAt = DateTime.UtcNow;
        vacancy.ShortlistApprovedById = reviewedByUserId;
        vacancy.ShortlistApprovalNotes = dto.Notes;

        await _vacancyRepository.UpdateAsync(vacancy);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Shortlist for vacancy {VacancyId} {Decision} by {UserId}",
            dto.VacancyId, dto.Approved ? "approved" : "rejected", reviewedByUserId);

        // Record SLA completion when approved
        if (dto.Approved)
        {
            if (vacancy.PublishDate.HasValue)
            {
                vacancy.ShortlistCompletedAt = DateTime.UtcNow;
                vacancy.TimeToShortlistDays = (int)(DateTime.UtcNow - vacancy.PublishDate.Value).TotalDays;
            }
            await _vacancyRepository.UpdateAsync(vacancy);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<bool> RecallShortlistApprovalAsync(
        Guid vacancyId, CancellationToken cancellationToken = default)
    {
        var vacancy = await GetOwnedVacancyAsync(vacancyId);

        if (vacancy.ShortlistApprovalStatus != ShortlistApprovalStatus.PendingApproval)
            throw new InvalidOperationException("Shortlist is not currently pending approval.");

        vacancy.ShortlistApprovalStatus  = ShortlistApprovalStatus.NotSubmitted;
        vacancy.ShortlistSubmittedAt     = null;
        vacancy.ShortlistSubmittedById   = null;
        vacancy.ShortlistApprovalNotes   = null;

        await _vacancyRepository.UpdateAsync(vacancy);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Shortlist approval for vacancy {VacancyId} recalled.", vacancyId);
        return true;
    }

    // ── Shortlist Decision Log ────────────────────────────────────────────────

    public async Task<IEnumerable<ShortlistDecisionLogDto>> GetShortlistDecisionLogAsync(
        Guid applicationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedApplicationAsync(applicationId);

        var logs = _applicationRepository.GetQueryable()
            .Where(a => a.Id == applicationId && a.TenantId == tenantId)
            .SelectMany(a => a.DecisionLogs)
            .OrderByDescending(l => l.DecisionAt);
        var list = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(logs, cancellationToken);
        return list.Select(l => new ShortlistDecisionLogDto
        {
            Id = l.Id,

            CreatedAt = l.CreatedAt,
            CreatedBy = l.CreatedBy ?? string.Empty,
            JobApplicationId = l.JobApplicationId,
            ApplicationNumber = l.ApplicationNumber,
            DecisionType = l.DecisionType,
            DecisionById = l.DecisionById,
            DecisionByName = l.DecisionBy?.FullName,
            DecisionAt = l.DecisionAt,
            AutoScoreAtDecision = l.AutoScoreAtDecision,
            IsAutoDecision = l.IsAutoDecision,
            Notes = l.Notes,
            OverridesDecisionLogId = l.OverridesDecisionLogId,
        });
    }

    // ── Multi-reviewer shortlist scoring ─────────────────────────────────────

    public async Task<ShortlistReviewDto> AddShortlistReviewAsync(
        CreateShortlistReviewDto dto, Guid tenantId, Guid reviewerId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var application = await GetOwnedApplicationAsync(dto.ApplicationId);

        var review = new ShortlistReview
        {
            Id = Guid.NewGuid(),
            TenantId = current,
            JobApplicationId = dto.ApplicationId,
            ReviewerId = reviewerId,
            Score = dto.Score,
            Notes = dto.Notes,
            ReviewedAt = DateTime.UtcNow,
            IsFinalized = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = reviewerId.ToString(),
        };

        await _shortlistReviewRepository.AddAsync(review);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("ShortlistReview added for application {AppId} by reviewer {ReviewerId}", dto.ApplicationId, reviewerId);

        // Re-fetch with Reviewer nav so the name is populated in the response
        var saved = await _shortlistReviewRepository.GetByIdAsync(review.Id, r => r.Reviewer);

        return new ShortlistReviewDto
        {
            Id = review.Id,
            CreatedAt = review.CreatedAt,
            CreatedBy = review.CreatedBy ?? string.Empty,
            JobApplicationId = review.JobApplicationId,
            ApplicationNumber = application.ApplicationNumber,
            ReviewerId = review.ReviewerId,
            ReviewerName = saved?.Reviewer?.FullName ?? string.Empty,
            Score = review.Score,
            Notes = review.Notes,
            ReviewedAt = review.ReviewedAt,
            IsFinalized = review.IsFinalized,
        };
    }

    public async Task<AggregatedReviewScoreDto> GetAggregatedReviewScoreAsync(
        Guid applicationId, CancellationToken cancellationToken = default)
    {
        var application = await GetOwnedApplicationAsync(applicationId);
        var tenantId = GetTenantId();

        var reviews = await _shortlistReviewRepository.GetByApplicationIdAsync(applicationId);
        var reviewList = reviews.Where(r => r.TenantId == tenantId).ToList();
        var finalized = reviewList.Where(r => r.IsFinalized).ToList();

        decimal? aggregated = finalized.Any()
            ? Math.Round(finalized.Average(r => r.Score), 2)
            : (decimal?)null;

        return new AggregatedReviewScoreDto
        {
            ApplicationId = applicationId,
            ApplicationNumber = application.ApplicationNumber,
            CandidateName = application.JobCandidate?.FullName,
            AggregatedScore = aggregated,
            ReviewerCount = finalized.Count,
            Reviews = reviewList.Select(r => new ShortlistReviewDto
            {
                Id = r.Id,

                CreatedAt = r.CreatedAt,
                CreatedBy = r.CreatedBy ?? string.Empty,
                JobApplicationId = r.JobApplicationId,
                ApplicationNumber = application.ApplicationNumber,
                ReviewerId = r.ReviewerId,
                ReviewerName = r.Reviewer?.FullName ?? string.Empty,
                Score = r.Score,
                Notes = r.Notes,
                ReviewedAt = r.ReviewedAt,
                IsFinalized = r.IsFinalized,
                FinalizedAt = r.FinalizedAt,
            }).ToList(),
        };
    }

    public async Task<ShortlistReviewDto> FinalizeShortlistReviewAsync(
        Guid reviewId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var review = await GetOwnedReviewAsync(reviewId);

        // Finalising is the reviewer committing their own judgement — it is what makes the score count
        // toward the aggregate. Anyone on the panel could previously finalise anyone else's draft review,
        // including one the reviewer was still revising.
        if (review.ReviewerId != updatedByUserId)
            throw new UnauthorizedAccessException(
                "Only the reviewer who wrote this review can finalize it.");

        if (review.IsFinalized)
            throw new InvalidOperationException("This review is already finalized.");

        review.IsFinalized = true;
        review.FinalizedAt = DateTime.UtcNow;
        review.UpdatedAt = DateTime.UtcNow;
        review.UpdatedBy = updatedByUserId.ToString();

        await _shortlistReviewRepository.UpdateAsync(review);

        // Recompute aggregated score on the application
        var application = await GetOwnedApplicationAsync(review.JobApplicationId);
        var tenantId = GetTenantId();
        var allReviews = (await _shortlistReviewRepository.GetByApplicationIdAsync(review.JobApplicationId))
            .Where(r => r.TenantId == tenantId)
            .ToList();
        var finalizedReviews = allReviews.Where(r => r.IsFinalized).ToList();
        application.AggregatedReviewScore = finalizedReviews.Any()
            ? Math.Round(finalizedReviews.Average(r => r.Score), 2)
            : (decimal?)null;
        await _applicationRepository.UpdateAsync(application);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ShortlistReviewDto
        {
            Id = review.Id,
            CreatedAt = review.CreatedAt,
            CreatedBy = review.CreatedBy ?? string.Empty,
            JobApplicationId = review.JobApplicationId,
            ReviewerId = review.ReviewerId,
            ReviewerName = review.Reviewer?.FullName ?? string.Empty,
            Score = review.Score,
            Notes = review.Notes,
            ReviewedAt = review.ReviewedAt,
            IsFinalized = review.IsFinalized,
            FinalizedAt = review.FinalizedAt,
        };
    }

    // ── EEO / Diversity compliance report ────────────────────────────────────

    public async Task<EeoComplianceReportDto> GetEeoReportAsync(
        Guid vacancyId, CancellationToken cancellationToken = default)
    {
        var vacancy = await GetOwnedVacancyAsync(vacancyId);

        var tenantId = GetTenantId();
        var applications = (await _applicationRepository.GetByVacancyIdAsync(vacancyId))
            .Where(a => a.TenantId == tenantId)
            .ToList();

        static EeoPipelineStageDto BuildStage(List<JobApplication> apps)
        {
            var stage = new EeoPipelineStageDto { Total = apps.Count };
            foreach (var a in apps)
            {
                var gender = a.JobCandidate?.Gender;
                switch (gender)
                {
                    case Gender.Male: stage.Male++; break;
                    case Gender.Female: stage.Female++; break;
                    case Gender.PreferNotToSay: stage.PreferNotToSay++; break;
                    default: stage.Other++; break;
                }
                if (a.IsInternalCandidate) stage.InternalCandidateCount++;
            }
            var ages = apps
                .Where(a => a.JobCandidate != null)
                .Select(a => (DateTime.UtcNow - a.JobCandidate!.DateOfBirth).TotalDays / 365.25)
                .ToList();
            if (ages.Any())
                stage.AverageAge = Math.Round((decimal)ages.Average(), 1);
            return stage;
        }

        var shortlisted = applications.Where(a => a.Status == ApplicationStatus.Shortlisted).ToList();
        var rejected = applications.Where(a => a.Status == ApplicationStatus.Rejected).ToList();
        var hired = applications.Where(a => a.Status == ApplicationStatus.Hired).ToList();

        return new EeoComplianceReportDto
        {
            VacancyId = vacancyId,
            VacancyNumber = vacancy.VacancyNumber,
            JobTitle = vacancy.JobTitle,
            GeneratedAt = DateTime.UtcNow,
            AllApplicants = BuildStage(applications),
            Shortlisted = BuildStage(shortlisted),
            Rejected = BuildStage(rejected),
            Hired = BuildStage(hired),
            ByStatus = applications
                .GroupBy(a => a.Status.ToString())
                .Select(g => new EeoStatusBreakdownDto
                {
                    Status = g.Key,
                    Total = g.Count(),
                    Demographics = BuildStage(g.ToList()),
                })
                .OrderBy(x => x.Status)
                .ToList(),
        };
    }

    // ── SLA status ────────────────────────────────────────────────────────────

    public async Task<ShortlistSlaStatusDto> GetShortlistSlaStatusAsync(
        Guid vacancyId, CancellationToken cancellationToken = default)
    {
        var vacancy = await GetOwnedVacancyAsync(vacancyId);

        int? daysOverdue = null;
        int? daysRemaining = null;
        if (vacancy.ShortlistingDeadline.HasValue)
        {
            var diff = (vacancy.ShortlistingDeadline.Value - DateTime.UtcNow).TotalDays;
            if (diff < 0 && vacancy.ShortlistCompletedAt == null)
                daysOverdue = (int)Math.Abs(diff);
            else if (diff >= 0 && vacancy.ShortlistCompletedAt == null)
                daysRemaining = (int)diff;
        }

        return new ShortlistSlaStatusDto
        {
            VacancyId = vacancyId,
            VacancyNumber = vacancy.VacancyNumber,
            PublishDate = vacancy.PublishDate,
            ShortlistingDeadline = vacancy.ShortlistingDeadline,
            ShortlistCompletedAt = vacancy.ShortlistCompletedAt,
            TimeToShortlistDays = vacancy.TimeToShortlistDays,
            ShortlistingSlaBreached = vacancy.ShortlistingSlaBreached,
            DaysOverdue = daysOverdue,
            DaysRemaining = daysRemaining,
            ApprovalStatus = vacancy.ShortlistApprovalStatus,
        };
    }

    // ── Blind screening ───────────────────────────────────────────────────────

    public async Task<IEnumerable<BlindApplicationSummaryDto>> GetBlindApplicationsAsync(
        Guid vacancyId, CancellationToken cancellationToken = default)
    {
        var vacancy = await GetOwnedVacancyAsync(vacancyId);

        if (!vacancy.IsBlindScreeningEnabled)
            throw new InvalidOperationException("Blind screening is not enabled for this vacancy. Enable it on the vacancy settings first.");

        var tenantId = GetTenantId();
        var applications = await _applicationRepository.GetByVacancyIdAsync(vacancyId);
        return applications
            .Where(a => a.TenantId == tenantId && a.Status != ApplicationStatus.Withdrawn)
            .Select(a => new BlindApplicationSummaryDto
            {
                ApplicationId = a.Id,
                ApplicationNumber = a.ApplicationNumber,
                Status = a.Status,
                ApplicationDate = a.ApplicationDate,
                YearsOfExperience = a.YearsOfExperience,
                AutoScore = a.AutoScore,
                ScoreIsStale = a.ScoreIsStale,
                ScoredAt = a.ScoredAt,
                IsInternalCandidate = a.IsInternalCandidate,
            });
    }

    // ── Internal candidate preferencing ──────────────────────────────────────

    public async Task<bool> MarkAsInternalCandidateAsync(
        MarkInternalCandidateDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var application = await GetOwnedApplicationAsync(dto.ApplicationId);

        application.IsInternalCandidate = true;
        application.InternalEmployeeId = dto.InternalEmployeeId;
        application.UpdatedAt = DateTime.UtcNow;
        application.UpdatedBy = updatedByUserId.ToString();

        await _applicationRepository.UpdateAsync(application);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UnmarkAsInternalCandidateAsync(
        Guid applicationId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var application = await GetOwnedApplicationAsync(applicationId);

        application.IsInternalCandidate = false;
        application.InternalEmployeeId  = null;
        application.UpdatedAt = DateTime.UtcNow;
        application.UpdatedBy = updatedByUserId.ToString();

        await _applicationRepository.UpdateAsync(application);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Export ────────────────────────────────────────────────────────────────

    public async Task<byte[]> GetShortlistCsvExportAsync(
        Guid vacancyId, CancellationToken cancellationToken = default)
    {
        var vacancy = await GetOwnedVacancyAsync(vacancyId);

        var tenantId = GetTenantId();
        var applications = (await _applicationRepository.GetByVacancyIdAsync(vacancyId))
            .Where(a => a.TenantId == tenantId
                     && (a.Status == ApplicationStatus.Shortlisted
                     || a.Status == ApplicationStatus.Waitlisted))
            .OrderByDescending(a => a.AutoScore)
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("ApplicationNumber,CandidateName,Email,Phone,YearsOfExperience,AutoScore,AggregatedReviewScore,Status,ShortlistedDate,DecisionSource,IsInternalCandidate,WaitlistReason");

        foreach (var a in applications)
        {
            sb.AppendLine(string.Join(",",
                CsvEscape(a.ApplicationNumber),
                CsvEscape(a.JobCandidate?.FullName),
                CsvEscape(a.JobCandidate?.Email),
                CsvEscape(a.JobCandidate?.Phone),
                a.YearsOfExperience?.ToString() ?? string.Empty,
                a.AutoScore?.ToString("F2") ?? string.Empty,
                a.AggregatedReviewScore?.ToString("F2") ?? string.Empty,
                CsvEscape(a.Status.ToString()),
                a.ShortlistedDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                CsvEscape(a.DecisionSource?.ToString()),
                a.IsInternalCandidate ? "Yes" : "No",
                CsvEscape(a.WaitlistReason)));
        }

        return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    // ── Internal self-service application ────────────────────────────────────

    public async Task<JobApplicationDto> InternalApplyAsync(
        InternalApplyForVacancyDto dto,
        Guid employeeId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // 1. Load employee
        var employee = await _employeeRepository.GetByIdAsync(employeeId)
            ?? throw new ArgumentException($"Employee '{employeeId}' not found.");

        // 2. Ensure vacancy exists and is open for applications
        var vacancy = await _vacancyRepository.GetByIdAsync(dto.VacancyId)
            ?? throw new ArgumentException($"Vacancy '{dto.VacancyId}' not found.");

        if (vacancy.VacancyStatus != JobVacancyStatus.Published)
            throw new InvalidOperationException("This vacancy is not currently published for applications.");

        // The flag was enforced nowhere but the job-board screen's client-side filter, so posting
        // straight to this endpoint got an internal application onto a vacancy that had explicitly
        // excluded internal candidates. Publishing already models the rule properly — it creates an
        // InternalPortal posting only when the flag is set — so the write path now agrees with it.
        if (!vacancy.AllowInternalCandidates)
            throw new InvalidOperationException("This vacancy is not open to internal candidates.");

        if (vacancy.ApplicationDeadline.HasValue && DateTime.UtcNow > vacancy.ApplicationDeadline.Value)
            throw new InvalidOperationException("The application deadline for this vacancy has passed.");

        // 3. Guard against duplicate internal applications (allow re-apply only after withdrawal)
        var existing = await _applicationRepository.GetQueryable()
            .FirstOrDefaultAsync(
                a => a.JobVacancyId == dto.VacancyId
                  && a.InternalEmployeeId == employeeId
                  && a.Status != ApplicationStatus.Withdrawn,
                cancellationToken);
        if (existing != null)
            throw new InvalidOperationException(
                $"You have already applied for this vacancy (Application #{existing.ApplicationNumber}).");

        // 4. Resolve or create shadow JobCandidate keyed by employee email. Email is optional on
        // the employee since 2026-09-03; a candidate without one cannot be tracked, so say so.
        var employeeEmail = employee.EmailAddress;
        if (string.IsNullOrWhiteSpace(employeeEmail))
            throw new InvalidOperationException(
                "Your employee profile has no email address. Applications are tracked by email — add one to your profile before applying.");

        var candidate = await _candidateRepository.GetByEmailAsync(employeeEmail);
        if (candidate == null)
        {
            // The country guard that used to stand here refused 8,072 of 8,077 live employees
            // (measured 2026-08-27) and told them to "complete the employee record first" — a field
            // only HR can change. JobCandidate.CountryId is optional as of slice 13b, so a shadow
            // candidate simply carries the employee's country when there is one and none when
            // there is not. See the remarks on JobCandidate.CountryId.

            candidate = new JobCandidate
            {
                TenantId            = tenantId,
                FirstName           = employee.FirstName,
                MiddleName          = employee.MiddleName,
                LastName            = employee.LastName,
                Email               = employeeEmail,
                Phone               = (employee.MobileNumber ?? employee.TelephoneNumber) ?? string.Empty,
                Gender              = employee.Gender ?? Gender.PreferNotToSay,
                DateOfBirth         = employee.DateOfBirth.HasValue
                                        ? employee.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)
                                        : DateTime.MinValue,
                City                = employee.City ?? string.Empty,
                CountryId           = employee.CountryId,        // optional — see the note above
                CandidateNumber     = await _candidateRepository.GetNextCandidateNumberAsync(),
                IsInternalEmployee  = true,
                InternalEmployeeId  = employeeId,
                CreatedBy           = employeeId.ToString(),
            };
            await _candidateRepository.AddAsync(candidate);
        }

        // 5. Create the application
        var application = new JobApplication
        {
            TenantId            = tenantId,
            JobVacancyId        = dto.VacancyId,
            JobCandidateId      = candidate.Id,
            IsInternalCandidate = true,
            InternalEmployeeId  = employeeId,
            Source              = ApplicationSource.InternalPortal,
            Status              = ApplicationStatus.Submitted,
            ApplicationDate     = DateTime.UtcNow,
            YearsOfExperience   = dto.YearsOfExperience,
            AvailableFrom       = dto.AvailableFrom,
            CoverLetter         = dto.CoverLetter,
            CreatedBy           = employeeId.ToString(),
        };
        application.ApplicationNumber = await _applicationRepository.GetNextApplicationNumberAsync();

        await _applicationRepository.AddAsync(application);

        // Capture an immutable profile snapshot so re-scoring always uses
        // the data that existed at submission time, not later profile edits.
        try
        {
            var snapshot = _snapshotService.BuildSnapshot(candidate, application.YearsOfExperience);
            application.ProfileSnapshotJson = System.Text.Json.JsonSerializer.Serialize(snapshot);
        }
        catch (Exception ex)
        {
            // Non-fatal: scoring falls back to live profile for this application.
            _logger.LogWarning(ex,
                "Profile snapshot capture failed for internal application {AppNumber} — scoring will use live profile.",
                application.ApplicationNumber);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Internal application {AppNumber} created for employee {EmployeeId} on vacancy {VacancyId}",
            application.ApplicationNumber, employeeId, dto.VacancyId);

        await UpdateVacancyCounterAsync(vacancy.Id, v => v.ApplicationCount++, cancellationToken);

        // Place the application into the first pipeline stage, if the vacancy has one.
        await _pipelineService.PlaceInFirstPipelineStageAsync(
            application.Id, vacancy.Id, tenantId, cancellationToken);

        // Reload from DB to get navigation properties for DTO mapping
        var saved = await _applicationRepository.GetByIdAsync(application.Id);
        return (saved ?? application).ToDto();
    }

    public async Task<JobApplicationDto> InternalSaveDraftAsync(
        InternalSaveDraftDto dto,
        Guid employeeId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId)
            ?? throw new ArgumentException($"Employee '{employeeId}' not found.");

        var vacancy = await _vacancyRepository.GetByIdAsync(dto.VacancyId)
            ?? throw new ArgumentException($"Vacancy '{dto.VacancyId}' not found.");

        if (vacancy.VacancyStatus != JobVacancyStatus.Published)
            throw new InvalidOperationException("This vacancy is not currently published for applications.");

        if (!vacancy.AllowInternalCandidates)
            throw new InvalidOperationException("This vacancy is not open to internal candidates.");

        // Check for existing non-withdrawn application
        var existing = await _applicationRepository.GetQueryable()
            .FirstOrDefaultAsync(
                a => a.JobVacancyId        == dto.VacancyId
                  && a.InternalEmployeeId  == employeeId
                  && a.Status              != ApplicationStatus.Withdrawn,
                cancellationToken);

        if (existing != null && existing.Status != ApplicationStatus.Draft)
            throw new InvalidOperationException(
                $"You have already submitted an application for this vacancy (#{existing.ApplicationNumber}).");

        if (existing != null)
        {
            // Update the existing draft
            existing.YearsOfExperience = dto.YearsOfExperience;
            existing.AvailableFrom     = dto.AvailableFrom;
            existing.CoverLetter       = dto.CoverLetter;
            await _applicationRepository.UpdateAsync(existing);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Internal draft {AppNumber} updated by employee {EmployeeId}",
                existing.ApplicationNumber, employeeId);
            var updatedSaved = await _applicationRepository.GetByIdAsync(existing.Id);
            return (updatedSaved ?? existing).ToDto();
        }

        // Resolve or create shadow candidate (email optional on the employee since 2026-09-03)
        var employeeEmail = employee.EmailAddress;
        if (string.IsNullOrWhiteSpace(employeeEmail))
            throw new InvalidOperationException(
                "Your employee profile has no email address. Applications are tracked by email — add one to your profile before applying.");

        var candidate = await _candidateRepository.GetByEmailAsync(employeeEmail);
        if (candidate == null)
        {
            // The country guard that used to stand here refused 8,072 of 8,077 live employees
            // (measured 2026-08-27) and told them to "complete the employee record first" — a field
            // only HR can change. JobCandidate.CountryId is optional as of slice 13b, so a shadow
            // candidate simply carries the employee's country when there is one and none when
            // there is not. See the remarks on JobCandidate.CountryId.

            candidate = new JobCandidate
            {
                TenantId           = tenantId,
                FirstName          = employee.FirstName,
                MiddleName         = employee.MiddleName,
                LastName           = employee.LastName,
                Email              = employeeEmail,
                Phone              = (employee.MobileNumber ?? employee.TelephoneNumber) ?? string.Empty,
                Gender             = employee.Gender ?? Gender.PreferNotToSay,
                DateOfBirth        = employee.DateOfBirth.HasValue
                                       ? employee.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)
                                       : DateTime.MinValue,
                City               = employee.City ?? string.Empty,
                CountryId          = employee.CountryId,        // optional — see the note above
                CandidateNumber    = await _candidateRepository.GetNextCandidateNumberAsync(),
                IsInternalEmployee = true,
                InternalEmployeeId = employeeId,
                CreatedBy          = employeeId.ToString(),
            };
            await _candidateRepository.AddAsync(candidate);
        }

        var application = new JobApplication
        {
            TenantId              = tenantId,
            JobVacancyId          = dto.VacancyId,
            JobCandidateId        = candidate.Id,
            IsInternalCandidate   = true,
            InternalEmployeeId    = employeeId,
            Source                = ApplicationSource.InternalPortal,
            Status                = ApplicationStatus.Draft,
            ApplicationDate       = DateTime.UtcNow,
            YearsOfExperience     = dto.YearsOfExperience,
            AvailableFrom         = dto.AvailableFrom,
            CoverLetter           = dto.CoverLetter,
            CreatedBy             = employeeId.ToString(),
        };
        application.ApplicationNumber = await _applicationRepository.GetNextApplicationNumberAsync();
        await _applicationRepository.AddAsync(application);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Internal draft application {AppNumber} created by employee {EmployeeId} for vacancy {VacancyId}",
            application.ApplicationNumber, employeeId, dto.VacancyId);

        var saved = await _applicationRepository.GetByIdAsync(application.Id);
        return (saved ?? application).ToDto();
    }

    public async Task<JobApplicationDto> InternalSubmitDraftAsync(
        Guid applicationId,
        InternalSubmitDraftDto dto,
        Guid employeeId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var application = await _applicationRepository.GetByIdAsync(applicationId)
            ?? throw new ArgumentException($"Application '{applicationId}' not found.");

        if (application.InternalEmployeeId != employeeId)
            throw new UnauthorizedAccessException("You can only submit your own applications.");
        if (application.TenantId != tenantId)
            throw new UnauthorizedAccessException();
        if (application.Status != ApplicationStatus.Draft)
            throw new InvalidOperationException(
                $"Only draft applications can be submitted. Current status: {application.Status}.");

        var vacancy = await _vacancyRepository.GetByIdAsync(application.JobVacancyId)
            ?? throw new InvalidOperationException("Job vacancy not found.");
        if (vacancy.VacancyStatus != JobVacancyStatus.Published)
            throw new InvalidOperationException("This vacancy is no longer accepting applications.");

        // Checked again on submit, not just on save: the flag can be turned off while a draft sits
        // unsent, and the draft is what would otherwise carry a stale permission into the pipeline.
        if (!vacancy.AllowInternalCandidates)
            throw new InvalidOperationException("This vacancy is no longer open to internal candidates.");
        if (vacancy.ApplicationDeadline.HasValue && DateTime.UtcNow > vacancy.ApplicationDeadline.Value)
            throw new InvalidOperationException("The application deadline for this vacancy has passed.");

        if (dto.YearsOfExperience != null) application.YearsOfExperience = dto.YearsOfExperience;
        if (dto.AvailableFrom     != null) application.AvailableFrom     = dto.AvailableFrom;
        if (dto.CoverLetter       != null) application.CoverLetter       = dto.CoverLetter;

        application.Status = ApplicationStatus.Submitted;
        await _applicationRepository.UpdateAsync(application);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Internal draft application {AppNumber} submitted by employee {EmployeeId}",
            application.ApplicationNumber, employeeId);
        await UpdateVacancyCounterAsync(application.JobVacancyId, v => v.ApplicationCount++, cancellationToken);

        var saved = await _applicationRepository.GetByIdAsync(application.Id);
        return (saved ?? application).ToDto();
    }

    /// <summary>
    /// The caller's own internal applications.
    /// </summary>
    /// <remarks>
    /// Returns the lean <see cref="MyJobApplicationDto"/>, the same shape as the single read.
    /// It used to return <c>JobApplicationSummaryDto</c>, which carries <c>AutoScore</c>,
    /// <c>ScoredAt</c>, <c>ScoreIsStale</c>, <c>AggregatedReviewScore</c> (the panel's verdict on
    /// them), <c>SnapshotAvailable</c>, <c>JobCandidateId</c> and the current pipeline stage —
    /// the assessment rather than the answer. Leaning only the detail read and leaving the list
    /// alone would have moved the leak rather than closed it.
    /// </remarks>
    public async Task<IEnumerable<MyJobApplicationDto>> GetByInternalEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _applicationRepository.GetQueryable()
            .Where(a => a.InternalEmployeeId == employeeId && a.TenantId == tenantId)
            .Include(a => a.JobVacancy).ThenInclude(v => v.Position)
            .Include(a => a.JobVacancy).ThenInclude(v => v.Requisition).ThenInclude(r => r.OrganizationUnit)
            .OrderByDescending(a => a.ApplicationDate)
            .ToListAsync(cancellationToken);

        return entities.Select(ToMyApplicationDto).ToList();
    }

    /// <summary>The one projection the applicant's own list and detail both use.</summary>
    private static MyJobApplicationDto ToMyApplicationDto(JobApplication entity) =>
        new()
        {
            Id                  = entity.Id,
            ApplicationNumber   = entity.ApplicationNumber,
            JobVacancyId        = entity.JobVacancyId,
            VacancyNumber       = entity.JobVacancy?.VacancyNumber ?? string.Empty,
            JobTitle            = entity.JobVacancy?.JobTitle ?? string.Empty,
            PositionTitle       = entity.JobVacancy?.Position?.Title ?? string.Empty,
            OrgUnitName         = entity.JobVacancy?.Requisition?.OrganizationUnit?.Name,
            ApplicationDeadline = entity.JobVacancy?.ApplicationDeadline,
            Status              = entity.Status,
            ApplicationDate     = entity.ApplicationDate,
            YearsOfExperience   = entity.YearsOfExperience,
            AvailableFrom       = entity.AvailableFrom,
            CoverLetter         = entity.CoverLetter,
            // ⚠ G-8.1 (2026-09-15): this read `entity.Status == ApplicationStatus.Shortlisted`
            // while every HR-side view computed the same flag as `ShortlistedDate.HasValue`
            // (RecruitmentMappingExtensions, ×2). The two agreed only while the status was still
            // Shortlisted — and a stage move overwrites the status (§ 8.6) while ShortlistedDate
            // keeps its value. From that moment **HR saw the candidate as shortlisted and the
            // candidate saw themselves as not**, which is the worst direction for this particular
            // disagreement to run: the applicant is told they were passed over when they were not.
            //
            // One definition now, and it is the date: being shortlisted is an event that happened,
            // not a state you stop being in because the process moved on.
            IsShortlisted       = entity.ShortlistedDate.HasValue,
            ShortlistedDate     = entity.ShortlistedDate,
            WithdrawnDate       = entity.WithdrawnDate,
            WithdrawalReason    = entity.WithdrawalReason,
            RejectedDate        = entity.RejectedDate,
            RejectionReason     = entity.RejectionReason,
            CanWithdraw         = CanApplicantWithdraw(entity.Status),
        };

    public async Task<MyJobApplicationDto?> GetMyApplicationAsync(
        Guid applicationId,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // Ownership is part of the QUERY, not a check after the fact: the id is only ever resolved
        // against the caller's own applications, so somebody else's is indistinguishable from one
        // that does not exist. A 403 would confirm the row is real.
        var owned = await _applicationRepository.GetQueryable()
            .AnyAsync(a => a.Id == applicationId
                        && a.TenantId == tenantId
                        && a.InternalEmployeeId == employeeId,
                      cancellationToken);
        if (!owned) return null;

        var entity = await _applicationRepository.GetQueryable()
            .Include(a => a.JobVacancy).ThenInclude(v => v.Position)
            .Include(a => a.JobVacancy).ThenInclude(v => v.Requisition).ThenInclude(r => r.OrganizationUnit)
            .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken);

        return entity == null ? null : ToMyApplicationDto(entity);
    }

    /// <summary>
    /// The states an applicant may still take their own application back from. Hired, rejected and
    /// already-withdrawn are terminal; a draft is deleted rather than withdrawn.
    /// </summary>
    private static bool CanApplicantWithdraw(ApplicationStatus status) =>
        status != ApplicationStatus.Hired
        && status != ApplicationStatus.Rejected
        && status != ApplicationStatus.Withdrawn;

    public async Task<bool> WithdrawMyApplicationAsync(
        Guid applicationId,
        Guid employeeId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var entity = await _applicationRepository.GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == applicationId
                                   && a.TenantId == tenantId
                                   && a.InternalEmployeeId == employeeId,
                                 cancellationToken)
            ?? throw new ArgumentException($"Job application with ID '{applicationId}' not found.");

        if (entity.Status == ApplicationStatus.Withdrawn)
            throw new InvalidOperationException("This application has already been withdrawn.");
        if (!CanApplicantWithdraw(entity.Status))
            throw new InvalidOperationException(
                $"An application that has been {entity.Status.ToString().ToLowerInvariant()} cannot be withdrawn.");

        // Reuse the desk path so the counters, the pipeline stage exit and the shortlist bookkeeping
        // all happen exactly once and in one place. `updatedByUserId` is the employee: they are the
        // actor, which is the whole point of the endpoint.
        return await WithdrawAsync(
            new WithdrawApplicationDto { ApplicationId = applicationId, WithdrawalReason = reason ?? "Withdrawn by the applicant." },
            employeeId,
            cancellationToken);
    }

    // ── External self-service portal ──────────────────────────────────────────

    public async Task<ExternalApplicationConfirmationDto> ExternalApplyAsync(
        ExternalApplicationDto dto,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate vacancy
        var vacancy = await _vacancyRepository.GetByIdAsync(dto.VacancyId)
            ?? throw new ArgumentException($"Vacancy '{dto.VacancyId}' not found.");

        // The tenant here comes from the caller-supplied X-Tenant-Id header, so it must be checked
        // against the vacancy's own tenant. Without this, a caller could post another tenant's
        // VacancyId and cross-wire the application (and its counter updates) between tenants.
        if (vacancy.TenantId != tenantId)
            throw new ArgumentException($"Vacancy '{dto.VacancyId}' not found.");

        if (vacancy.VacancyStatus != JobVacancyStatus.Published)
            throw new InvalidOperationException("This vacancy is not currently open for applications.");

        if (vacancy.ApplicationDeadline.HasValue && DateTime.UtcNow > vacancy.ApplicationDeadline.Value)
            throw new InvalidOperationException("The application deadline for this vacancy has passed.");

        // The client sends back a token we issued from POST /api/public/cv-upload, never a path.
        // Resolving it here also binds the upload to this tenant and this vacancy.
        var cvTicket = await ResolveCvUploadTicketAsync(
            dto.CvUploadToken, tenantId, dto.VacancyId, cancellationToken);

        // Country stays REQUIRED on the external path — not because of the foreign key (which is
        // optional since slice 13b), but because the public application form asks for it and an
        // external candidate can answer. A cross-tenant or malformed id would otherwise reach the
        // database as an invalid key and surface as an opaque 500, so reject it here with a message
        // the applicant can act on. The internal path is the opposite case: the applicant is an
        // employee who cannot edit their own country, so there it is optional.
        // Single-argument ArgumentException on purpose: the controller surfaces Message verbatim to an
        // anonymous caller, and the two-argument overload would append "(Parameter 'CountryId')".
        if (dto.CountryId is null || dto.CountryId == Guid.Empty)
            throw new ArgumentException("A country must be selected.");

        var countryExists = await _unitOfWork.Repository<Country>()
            .ExistsAsync(c => c.Id == dto.CountryId.Value && c.TenantId == tenantId && !c.IsDeleted);
        if (!countryExists)
            throw new ArgumentException("The selected country is not recognised.");

        // Everything that writes runs inside ONE transaction. This method used to commit four separate
        // times, so a failure part-way through (e.g. while inserting the child profile rows) left a
        // committed application with no work history, qualifications or skills — and the duplicate
        // guard then blocked the candidate from ever re-applying. Rolling the whole unit back instead
        // means a failed submission leaves nothing behind and can simply be retried.
        var trackingToken = $"TKN-{Guid.NewGuid():N}".ToUpper();
        JobApplication application = null!;

        await _unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                application = await PersistExternalApplicationAsync(
                    dto, vacancy, tenantId, cvTicket, trackingToken, ct);
            },
            cancellationToken);

        _logger.LogInformation(
            "External application {AppNumber} created for candidate {Email} on vacancy {VacancyId} " +
            "(WorkHistory: {WH}, Quals: {Q}, Referees: {R}, Skills: {S}, Languages: {L})",
            application.ApplicationNumber, dto.Email, dto.VacancyId,
            dto.WorkHistories.Count, dto.Qualifications.Count,
            dto.Referees.Count, dto.Skills.Count, dto.Languages.Count);

        // Catalogue the CV in the central DMS. Deliberately AFTER the transaction, for the same
        // reason as the counter below: RegisterAsync saves on its own unit of work and would
        // contend with rows this transaction held. The application is already durable, and the
        // candidate keeps CvFileUploadRecordId either way, so a DMS hiccup costs a catalogue
        // entry that can be reconciled later — never the submission itself.
        if (cvTicket is not null)
            await RegisterCandidateCvAsync(application.JobCandidateId, cvTicket, tenantId, cancellationToken);

        // The vacancy counter is a denormalised statistic, not part of the application's integrity.
        // It stays OUT of the transaction: UpdateVacancyCounterAsync retries concurrency conflicts by
        // reloading the vacancy row, which cannot work against rows the same transaction holds locked.
        try
        {
            await UpdateVacancyCounterAsync(vacancy.Id, v => v.ApplicationCount++, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to increment the application counter for vacancy {VacancyId} — " +
                "application {AppNumber} was submitted successfully.",
                vacancy.Id, application.ApplicationNumber);
        }

        var confirmation = new ExternalApplicationConfirmationDto
        {
            ApplicationNumber = application.ApplicationNumber,
            TrackingToken     = trackingToken,
            CandidateName     = $"{dto.FirstName} {dto.LastName}".Trim(),
            JobTitle          = vacancy.JobTitle,
            VacancyNumber     = vacancy.VacancyNumber,
            SubmittedAt       = application.ApplicationDate,
        };

        // Best-effort: the application is already committed, so a failed or slow confirmation email
        // must never cost the candidate their tracking token. See SendApplicationReceivedEmailAsync.
        await SendApplicationReceivedEmailAsync(dto.Email, confirmation);

        return confirmation;
    }

    /// <summary>
    /// Persists an external application and all of its child profile rows. Runs inside the caller's
    /// transaction — it performs a single <c>SaveChangesAsync</c> (needed so the pipeline placement
    /// below can read the application back by id) and lets the caller commit.
    /// </summary>
    private async Task<JobApplication> PersistExternalApplicationAsync(
        ExternalApplicationDto dto,
        JobVacancy vacancy,
        Guid tenantId,
        PublicCvUploadTicket? cvTicket,
        string trackingToken,
        CancellationToken cancellationToken = default)
    {
        // 2. Resolve or create candidate record (keyed by email, per-tenant)
        var candidate = await _candidateRepository.GetByEmailAsync(dto.Email, tenantId);
        if (candidate == null)
        {
            candidate = new JobCandidate
            {
                TenantId        = tenantId,
                FirstName       = dto.FirstName.Trim(),
                MiddleName      = dto.MiddleName?.Trim(),
                LastName        = dto.LastName.Trim(),
                Email           = dto.Email.Trim().ToLowerInvariant(),
                Phone           = dto.Phone.Trim(),
                Gender          = dto.Gender,
                DateOfBirth     = dto.DateOfBirth,
                City            = dto.City?.Trim() ?? string.Empty,
                CountryId       = dto.CountryId!.Value,   // validated above
                LinkedInProfile = dto.LinkedInProfile,
                PortfolioUrl    = dto.PortfolioUrl,
                CvFileUploadRecordId = cvTicket?.FileUploadRecordId,
                AvailableFrom   = dto.AvailableFrom,
                IsInTalentPool  = dto.AddToTalentPool,
                TalentPoolAddedDate = dto.AddToTalentPool ? DateTime.UtcNow : null,
                // Tenant-explicit: this flow is anonymous, so the number sequence has no tenant claim
                // to read and would otherwise stamp Guid.Empty and break its FK to Tenants.
                CandidateNumber = await _candidateRepository.GetNextCandidateNumberAsync(tenantId),
                CreatedBy       = "external-portal",
            };
            await _candidateRepository.AddAsync(candidate);
        }
        else
        {
            // Update opt-in if newly consented; point at the new CV if one was supplied
            bool candidateUpdated = false;
            if (dto.AddToTalentPool && !candidate.IsInTalentPool)
            {
                candidate.IsInTalentPool = true;
                candidate.TalentPoolAddedDate = DateTime.UtcNow;
                candidateUpdated = true;
            }
            if (cvTicket is not null)
            {
                // A repeat applicant gets a second CV document. The previous one is left in
                // place: deleting a superseded CV would destroy evidence for an application
                // that may still be under review.
                candidate.CvFileUploadRecordId = cvTicket.FileUploadRecordId;
                candidate.CvDocumentRecordId = null;
                candidate.CvDocumentVersionId = null;
                candidateUpdated = true;
            }
            if (dto.AvailableFrom.HasValue)
            {
                candidate.AvailableFrom = dto.AvailableFrom;
                candidateUpdated = true;
            }
            if (candidateUpdated)
                await _candidateRepository.UpdateAsync(candidate);
        }

        // 3. Guard duplicate active application for same vacancy by same candidate
        var duplicate = await _applicationRepository.GetQueryable()
            .FirstOrDefaultAsync(
                a => a.JobVacancyId == dto.VacancyId
                  && a.JobCandidateId == candidate.Id
                  && a.Status != ApplicationStatus.Withdrawn,
                cancellationToken);
        if (duplicate != null)
            throw new InvalidOperationException(
                $"You have already submitted an application for this vacancy (Application #{duplicate.ApplicationNumber}). " +
                $"Use your tracking token to check its status.");

        // 4. Create the application (the tracking token is generated by the caller)
        var application = new JobApplication
        {
            TenantId               = tenantId,
            JobVacancyId           = dto.VacancyId,
            JobCandidateId         = candidate.Id,
            IsInternalCandidate    = false,
            Source                 = dto.Source,
            Status                 = ApplicationStatus.Submitted,
            ApplicationDate        = DateTime.UtcNow,
            YearsOfExperience      = dto.YearsOfExperience,
            AvailableFrom          = dto.AvailableFrom,
            CoverLetter            = dto.CoverLetter,
            ExternalTrackingToken  = trackingToken,
            CreatedBy              = "external-portal",
        };
        // Tenant-explicit for the same reason as CandidateNumber above — anonymous flow, no claim.
        application.ApplicationNumber = await _applicationRepository.GetNextApplicationNumberAsync(tenantId);

        await _applicationRepository.AddAsync(application);

        // 5. Persist structured profile data into child tables
        //    For returning candidates, we ADD new entries (deduplicated by caller intent).
        //    All records are scoped to this tenant.

        // Work history
        foreach (var wh in dto.WorkHistories)
        {
            await _workHistoryRepository.AddAsync(new JobCandidateWorkHistory
            {
                TenantId         = tenantId,
                JobCandidateId   = candidate.Id,
                InstitutionName  = wh.InstitutionName.Trim(),
                PositionHeld     = wh.PositionHeld.Trim(),
                StartDate        = wh.StartDate,
                EndDate          = wh.EndDate,
                Responsibilities = wh.Responsibilities?.Trim(),
                ReasonForLeaving = wh.ReasonForLeaving?.Trim(),
                CreatedBy        = "external-portal",
            });
        }

        // Qualifications / education
        foreach (var q in dto.Qualifications)
        {
            await _qualificationRepository.AddAsync(new JobCandidateQualification
            {
                TenantId               = tenantId,
                JobCandidateId         = candidate.Id,
                QualificationType      = q.QualificationType,
                QualificationId        = null,          // external — no catalogue link yet
                QualificationFreeText  = q.QualificationName.Trim(),
                Institution            = q.Institution.Trim(),
                DateAwarded            = q.DateAwarded,
                Grade                  = q.Grade?.Trim(),
                CreatedBy              = "external-portal",
            });
        }

        // Referees
        foreach (var r in dto.Referees)
        {
            await _refereeRepository.AddAsync(new JobCandidateReferee
            {
                TenantId       = tenantId,
                JobCandidateId = candidate.Id,
                FullName       = r.FullName.Trim(),
                Position       = r.Position.Trim(),
                Organization   = r.Organization.Trim(),
                Email          = r.Email.Trim().ToLowerInvariant(),
                Phone          = r.Phone.Trim(),
                Relationship   = r.Relationship?.Trim() ?? string.Empty,
                YearsKnown     = r.YearsKnown,
                CreatedBy      = "external-portal",
            });
        }

        // Skills
        foreach (var s in dto.Skills)
        {
            await _skillRepository.AddAsync(new JobCandidateSkill
            {
                TenantId          = tenantId,
                JobCandidateId    = candidate.Id,
                SkillId           = null,  // no catalogue link for external submissions
                SkillName         = s.SkillName.Trim(),
                Proficiency       = s.Proficiency,
                YearsOfExperience = s.YearsOfExperience,
                IsCertified       = s.IsCertified,
                CertificationName = s.CertificationName?.Trim(),
                CreatedBy         = "external-portal",
            });
        }

        // Languages — round 3, lane C1: a catalogue row or a typed name, the same rule as the
        // careers profile save (CandidatePortalService.ResolveLanguage).
        var languageMaster = (await _unitOfWork.Repository<Language>()
                .FindAsync(x => x.TenantId == tenantId && !x.IsDeleted))
            .ToDictionary(x => x.Id);
        var resolvedLanguages = dto.Languages
            .Select(l => (Row: l, Resolved: CandidatePortalService.ResolveLanguage(l.LanguageId, l.LanguageName, languageMaster)))
            .ToList();
        foreach (var (l, resolved) in resolvedLanguages)
        {
            await _languageRepository.AddAsync(new JobCandidateLanguage
            {
                TenantId       = tenantId,
                JobCandidateId = candidate.Id,
                LanguageId     = resolved.LanguageId,
                LanguageName   = resolved.LanguageName,
                Proficiency    = l.Proficiency,
                CreatedBy      = "external-portal",
            });
        }

        // Build snapshot from the submitted DTO data — avoids an extra DB round-trip
        // and guarantees we freeze exactly what the candidate provided on the form.
        try
        {
            var snapshotSkills = dto.Skills.Select(s => new SnapshotSkill
            {
                SkillName         = s.SkillName.Trim(),
                IsCertified       = s.IsCertified,
                CertificationName = s.CertificationName?.Trim(),
                YearsOfExperience = s.YearsOfExperience,
                Proficiency       = null,
                SkillId           = s.SkillId,
            }).ToList();

            var snapshotQuals = dto.Qualifications.Select(q =>
            {
                var display = q.QualificationName.Trim();
                return new SnapshotQualification
                {
                    NormalisedName  = display.ToLowerInvariant(),
                    DisplayName     = display,
                    Institution     = q.Institution.Trim(),
                    QualificationId = q.QualificationId,
                };
            }).Where(q => q.NormalisedName.Length > 0).ToList();

            var snapshotLangs = resolvedLanguages.Select(x => new SnapshotLanguage
            {
                NormalisedName = x.Resolved.LanguageName.ToLowerInvariant(),
                DisplayName    = x.Resolved.LanguageName,
                Proficiency    = (int)x.Row.Proficiency,
                LanguageId     = x.Resolved.LanguageId,
            }).Where(l => l.NormalisedName.Length > 0).ToList();

            var snapshotWork = dto.WorkHistories.Select(w => new SnapshotWorkHistory
            {
                InstitutionName = w.InstitutionName.Trim(),
                PositionHeld    = w.PositionHeld.Trim(),
                StartDate       = w.StartDate,
                EndDate         = w.EndDate,
            }).ToList();

            var snapshot = new ApplicationCandidateSnapshot
            {
                SnapshotTakenAt      = DateTime.UtcNow,
                YearsOfExperience    = dto.YearsOfExperience,
                DateOfBirth          = dto.DateOfBirth,
                Gender               = dto.Gender,
                City                 = dto.City?.Trim(),
                // Round 4, lane A. ⚠ This is the THIRD snapshot writer and the only one that does
                // not go through IApplicationSnapshotService — it builds from the public form's DTO
                // rather than from the candidate graph. Anything added to the snapshot has to be
                // added here too, or this path alone writes it null forever.
                //
                // The area comes from the CANDIDATE, not the DTO: the anonymous apply form collects
                // a free-text city and no cascade (most applicants are in a country with no scheme,
                // and the form is deliberately short). A returning applicant whose profile already
                // carries an area therefore keeps it; a brand-new one has none, and the Location
                // criterion falls back to the city below.
                GeoAreaId            = candidate.GeoAreaId,
                GeoAreaPath          = string.IsNullOrWhiteSpace(candidate.GeoArea?.Path)
                                           ? null
                                           : candidate.GeoArea!.Path,
                TotalYearsExperience = null,   // not collected on the external form
                Skills               = snapshotSkills.AsReadOnly(),
                Qualifications       = snapshotQuals.AsReadOnly(),
                Languages            = snapshotLangs.AsReadOnly(),
                WorkHistories        = snapshotWork.AsReadOnly(),
            };

            // No UpdateAsync here: the application is still tracked as Added (the single
            // SaveChangesAsync below has not run yet), so the snapshot rides along on the INSERT.
            // Calling Update() would flip the entity to Modified and emit an UPDATE for a row that
            // does not exist yet, which fails with "expected to affect 1 row(s), but actually 0".
            application.ProfileSnapshotJson = System.Text.Json.JsonSerializer.Serialize(snapshot);
        }
        catch (Exception ex)
        {
            // Non-fatal: scoring falls back to live profile for this application.
            _logger.LogWarning(ex,
                "Profile snapshot capture failed for external application {AppNumber} — scoring will use live profile.",
                application.ApplicationNumber);
        }

        // Burn the CV ticket inside the same transaction as the application, so a rolled-back
        // submission leaves the upload claimable and the applicant can simply retry.
        if (cvTicket is not null)
        {
            cvTicket.ClaimedAtUtc = DateTime.UtcNow;
            cvTicket.ClaimedByCandidateId = candidate.Id;
            await _unitOfWork.Repository<PublicCvUploadTicket>().UpdateAsync(cvTicket);
        }

        // Flush the application, its child rows and the snapshot so the pipeline placement below can
        // read the application back by id. This is a save, not a commit — the caller's transaction
        // still owns the commit, so everything here rolls back together on failure.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Place the application into the first pipeline stage, if the vacancy has one.
        // Enlists in the caller's transaction (same scoped IUnitOfWork) and is itself best-effort.
        await _pipelineService.PlaceInFirstPipelineStageAsync(
            application.Id, vacancy.Id, tenantId, cancellationToken);

        return application;
    }

    public async Task<PublicApplicationStatusDto> GetApplicationStatusByTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var application = await _applicationRepository.GetByTrackingTokenAsync(token)
            ?? throw new KeyNotFoundException("No application found for the provided tracking token.");

        var (label, description) = ToPublicStatusLabel(application.Status);

        return new PublicApplicationStatusDto
        {
            ApplicationNumber  = application.ApplicationNumber,
            CandidateName      = application.JobCandidate?.FullName ?? string.Empty,
            JobTitle           = application.JobVacancy?.JobTitle ?? string.Empty,
            VacancyNumber      = application.JobVacancy?.VacancyNumber ?? string.Empty,
            SubmittedAt        = application.ApplicationDate,
            StatusLabel        = label,
            StatusDescription  = description,
            CanWithdraw        = application.Status is
                                    ApplicationStatus.Submitted or
                                    ApplicationStatus.UnderReview or
                                    ApplicationStatus.Shortlisted or
                                    ApplicationStatus.InterviewScheduled,
            WithdrawnAt        = application.WithdrawnDate,
            RejectedAt         = application.RejectedDate,
        };
    }

    public async Task<bool> WithdrawByTokenAsync(
        string token,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var application = await _applicationRepository.GetByTrackingTokenAsync(token)
            ?? throw new KeyNotFoundException("No application found for the provided tracking token.");

        if (application.Status == ApplicationStatus.Withdrawn)
            throw new InvalidOperationException("This application has already been withdrawn.");

        if (application.Status is ApplicationStatus.Rejected or ApplicationStatus.Hired)
            throw new InvalidOperationException($"Applications in '{application.Status}' status cannot be withdrawn.");

        application.Status = ApplicationStatus.Withdrawn;
        application.WithdrawnDate = DateTime.UtcNow;
        application.WithdrawalReason = reason;
        application.UpdatedBy = "external-portal";

        await _applicationRepository.UpdateAsync(application);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The counter was previously never decremented on this path, so every public-portal
        // withdrawal inflated ApplicationCount permanently. Kept outside the save above for
        // the usual reason: JobVacancy's RowVersion must not be able to fail the withdrawal.
        await UpdateVacancyCounterAsync(
            application.JobVacancyId,
            vacancy => vacancy.ApplicationCount = Math.Max(0, vacancy.ApplicationCount - 1),
            cancellationToken);

        _logger.LogInformation(
            "External application {AppNumber} withdrawn via tracking token.",
            application.ApplicationNumber);

        await SendWithdrawalConfirmationEmailAsync(
            application.JobCandidate?.Email ?? string.Empty,
            application.JobCandidate?.FullName ?? "Candidate",
            application.ApplicationNumber,
            application.JobVacancy?.JobTitle ?? "the position");

        return true;
    }

    private static (string Label, string Description) ToPublicStatusLabel(ApplicationStatus status) => status switch
    {
        ApplicationStatus.New or
        ApplicationStatus.Submitted     => ("Application Received",
                                            "Your application has been successfully submitted and is awaiting review by our team."),
        ApplicationStatus.UnderReview   => ("Under Review",
                                            "Our recruitment team is currently reviewing your application."),
        ApplicationStatus.Shortlisted   => ("Shortlisted",
                                            "Congratulations! You have been shortlisted and will be contacted shortly with next steps."),
        ApplicationStatus.InterviewScheduled => ("Interview Scheduled",
                                            "An interview has been scheduled. Please check your email for details."),
        ApplicationStatus.InterviewCompleted => ("Interview Completed",
                                            "Your interview has been completed. We are currently evaluating all candidates."),
        ApplicationStatus.AssessmentPending  => ("Assessment Pending",
                                            "You have been selected to complete an assessment. Check your email for instructions."),
        ApplicationStatus.PreEmploymentCheck => ("Pre-Employment Checks",
                                            "We are currently completing pre-employment checks (references, background verification, and/or medical assessment) as part of our final evaluation."),
        ApplicationStatus.OfferExtended      => ("Offer Extended",
                                            "An offer has been extended to you. Please check your email."),
        ApplicationStatus.OfferAccepted      => ("Offer Accepted",
                                            "Your offer has been accepted. Welcome aboard!"),
        ApplicationStatus.OfferDeclined      => ("Offer Declined",
                                            "The offer for this position was declined."),
        ApplicationStatus.Rejected           => ("Application Unsuccessful",
                                            "After careful consideration, we have decided not to proceed with your application at this time. We appreciate your interest."),
        ApplicationStatus.Withdrawn          => ("Application Withdrawn",
                                            "You have withdrawn your application for this position."),
        ApplicationStatus.Hired              => ("Hired",
                                            "Congratulations! You have been hired for this position."),
        ApplicationStatus.Waitlisted         => ("On Waitlist",
                                            "You have been placed on a waitlist. We may contact you if a position becomes available."),
        _                                    => ("Processing",
                                            "Your application is being processed."),
    };

    // ── Email helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Sends a templated email without ever failing the caller. Used by the notifications that fire
    /// AFTER their record has already been committed — a candidate whose application is saved must
    /// still receive their confirmation and tracking token even if mail delivery is broken or slow.
    /// Mirrors <c>CandidatePortalService</c>'s portal-side treatment of the same emails.
    /// </summary>
    private async Task SendBestEffortAsync(
        string eventName,
        string toEmail,
        Dictionary<string, string?> tokens,
        string description)
    {
        try
        {
            var emailTask = _templatedEmail.SendAsync(
                RecruitmentEmailCatalog.Module, eventName, toEmail, tokens);

            // Race the send against a 10-second timeout so an unresponsive SMTP server never holds
            // the HTTP response open. TemplatedEmailService already swallows delivery failures and
            // returns false, so latency — not an exception — is the failure mode that matters here.
            if (await Task.WhenAny(emailTask, Task.Delay(TimeSpan.FromSeconds(10))) == emailTask)
                await emailTask;
            else
                _logger.LogWarning(
                    "{Description} email timed out after 10 s for {Email} — the operation itself succeeded.",
                    description, toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send {Description} email to {Email} — the operation itself succeeded.",
                description, toEmail);
        }
    }

    private async Task SendApplicationReceivedEmailAsync(
        string toEmail,
        ExternalApplicationConfirmationDto confirmation)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        var tokens = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CandidateName"]     = confirmation.CandidateName,
            ["JobTitle"]          = confirmation.JobTitle,
            ["VacancyNumber"]     = confirmation.VacancyNumber,
            ["ApplicationNumber"] = confirmation.ApplicationNumber,
            ["SubmittedAt"]       = confirmation.SubmittedAt.ToString("dd MMM yyyy HH:mm") + " UTC",
        };

        await SendBestEffortAsync(
            RecruitmentEmailCatalog.Events.ApplicationReceived, toEmail, tokens, "application received");
    }

    private async Task SendWithdrawalConfirmationEmailAsync(
        string toEmail,
        string candidateName,
        string applicationNumber,
        string jobTitle)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        var tokens = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CandidateName"]     = candidateName,
            ["JobTitle"]          = jobTitle,
            ["ApplicationNumber"] = applicationNumber,
        };

        await SendBestEffortAsync(
            RecruitmentEmailCatalog.Events.ApplicationWithdrawn, toEmail, tokens, "withdrawal confirmation");
    }

    private async Task SendUnderReviewEmailAsync(
        string toEmail, string candidateName, string applicationNumber, string jobTitle)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        var tokens = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CandidateName"]     = candidateName,
            ["JobTitle"]          = jobTitle,
            ["ApplicationNumber"] = applicationNumber,
        };

        await SendBestEffortAsync(
            RecruitmentEmailCatalog.Events.ApplicationUnderReview, toEmail, tokens, "application under review");
    }

    private async Task SendShortlistedEmailAsync(
        string toEmail, string candidateName, string applicationNumber, string jobTitle)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        var tokens = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CandidateName"]     = candidateName,
            ["JobTitle"]          = jobTitle,
            ["ApplicationNumber"] = applicationNumber,
        };

        await _templatedEmail.SendAsync(
            RecruitmentEmailCatalog.Module, RecruitmentEmailCatalog.Events.ApplicationShortlisted, toEmail, tokens);
    }

    private async Task SendRejectedEmailAsync(
        string toEmail, string candidateName, string applicationNumber, string jobTitle, string? rejectionReason)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        var tokens = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CandidateName"]     = candidateName,
            ["JobTitle"]          = jobTitle,
            ["ApplicationNumber"] = applicationNumber,
            ["RejectionReason"]   = rejectionReason,
        };

        await _templatedEmail.SendAsync(
            RecruitmentEmailCatalog.Module, RecruitmentEmailCatalog.Events.ApplicationRejected, toEmail, tokens);
    }

    // The stage-type → ApplicationStatus mapping used to be duplicated here, with a comment explaining
    // that it existed "so that JobApplicationService.MoveToStageAsync does not depend on the pipeline
    // service". Avoiding that dependency is precisely how the two paths drifted into one guarded
    // transition and one unguarded one. MoveToStageAsync now delegates, and
    // ApplicationPipelineService.MapStageTypeToStatus is the single definition.
}
