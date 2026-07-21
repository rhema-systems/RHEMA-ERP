using System.Text.Json;
using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JobApplicationService> _logger;
    private readonly IEmailService _email;
    private readonly ITemplatedEmailService _templatedEmail;

    public JobApplicationService(
        IJobApplicationRepository applicationRepository,
        IJobApplicationStageHistoryRepository stageHistoryRepository,
        IJobApplicantTestResultRepository testResultRepository,
        IJobApplicantCommunicationRepository communicationRepository,
        IJobVacancyRepository vacancyRepository,
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
        IUnitOfWork unitOfWork,
        ILogger<JobApplicationService> logger,
        IEmailService email,
        ITemplatedEmailService templatedEmail)
    {
        _applicationRepository = applicationRepository;
        _stageHistoryRepository = stageHistoryRepository;
        _testResultRepository = testResultRepository;
        _communicationRepository = communicationRepository;
        _vacancyRepository = vacancyRepository;
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
        _unitOfWork = unitOfWork;
        _logger = logger;
        _email = email;
        _templatedEmail = templatedEmail;
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public async Task<JobApplicationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _applicationRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Job application with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<JobApplicationDto?> GetByApplicationNumberAsync(string applicationNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _applicationRepository.GetByApplicationNumberAsync(applicationNumber);
        return entity?.ToDto();
    }

    public async Task<JobApplicationDetailDto> GetWithFullDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _applicationRepository.GetWithFullDetailsAsync(id);
        if (entity == null)
            throw new ArgumentException($"Job application with ID '{id}' not found.");
        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<JobApplicationSummaryDto>> GetAllAsync(Guid? vacancyId = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<JobApplication> entities = vacancyId.HasValue
            ? await _applicationRepository.GetByVacancyIdAsync(vacancyId.Value)
            : await _applicationRepository.GetAllAsync();

        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<JobApplicationSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, Guid? vacancyId = null, CancellationToken cancellationToken = default)
    {
        // Clamp before use: pageNumber 0 makes Skip() negative (throws), and an unbounded pageSize lets
        // a caller pull the whole table with ?pageSize=1000000.
        (pageNumber, pageSize) = PagingGuard.Clamp(pageNumber, pageSize);

        var query = _applicationRepository.GetQueryable();

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
        var entities = await _applicationRepository.GetByVacancyIdAsync(vacancyId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobApplicationSummaryDto>> GetByCandidateIdAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var entities = await _applicationRepository.GetByCandidateIdAsync(candidateId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobApplicationSummaryDto>> GetByStatusAsync(ApplicationStatus status, Guid? vacancyId = null, CancellationToken cancellationToken = default)
    {
        var entities = await _applicationRepository.GetByStatusAsync(status, vacancyId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobApplicationSummaryDto>> GetShortlistedAsync(Guid? vacancyId = null, CancellationToken cancellationToken = default)
    {
        var entities = await _applicationRepository.GetShortlistedAsync(vacancyId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobApplicationSummaryDto>> GetByCurrentStageAsync(Guid pipelineStageId, CancellationToken cancellationToken = default)
    {
        var entities = await _applicationRepository.GetByCurrentStageAsync(pipelineStageId);
        return entities.ToSummaryDtoList();
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public async Task<JobApplicationDto> CreateAsync(CreateJobApplicationDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.ApplicationNumber = await _applicationRepository.GetNextApplicationNumberAsync();
        entity.Status = ApplicationStatus.New;
        entity.ApplicationDate = DateTime.UtcNow;

        await _applicationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job application created: {ApplicationNumber}", entity.ApplicationNumber);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _applicationRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Job application with ID '{id}' not found.");

        if (entity.Status is not (ApplicationStatus.Draft or ApplicationStatus.New))
            throw new InvalidOperationException("Only draft or newly received applications can be deleted.");

        await _applicationRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Workflow ──────────────────────────────────────────────────────────────

    public async Task<bool> ShortlistAsync(ShortlistApplicationDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _applicationRepository.GetByIdAsync(dto.ApplicationId);
        if (entity == null)
            throw new ArgumentException($"Job application '{dto.ApplicationId}' not found.");

        // Guard: terminal states cannot be shortlisted
        if (entity.Status is ApplicationStatus.Hired or ApplicationStatus.Withdrawn)
            throw new InvalidOperationException($"Cannot shortlist an application that is {entity.Status}.");

        if (entity.Status == ApplicationStatus.Shortlisted)
            throw new InvalidOperationException("Application is already shortlisted.");

        // Enforce shortlisting deadline
        var vacancy = await _vacancyRepository.GetByIdAsync(entity.JobVacancyId);
        if (vacancy?.ShortlistingDeadline != null && DateTime.UtcNow > vacancy.ShortlistingDeadline.Value)
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
        var entity = await _applicationRepository.GetByIdAsync(dto.ApplicationId);
        if (entity == null)
            throw new ArgumentException($"Job application '{dto.ApplicationId}' not found.");

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
        var entity = await _applicationRepository.GetByIdAsync(dto.ApplicationId);
        if (entity == null)
            throw new ArgumentException($"Job application '{dto.ApplicationId}' not found.");

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
        var entity = await _applicationRepository.GetByIdAsync(dto.ApplicationId);
        if (entity == null)
            throw new ArgumentException($"Job application with ID '{dto.ApplicationId}' not found.");

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
        var entity = await _applicationRepository.GetByIdAsync(dto.ApplicationId);
        if (entity == null)
            throw new ArgumentException($"Job application with ID '{dto.ApplicationId}' not found.");

        if (entity.Status == ApplicationStatus.Hired)
            throw new InvalidOperationException("A hired application cannot be withdrawn.");

        entity.Status = ApplicationStatus.Withdrawn;
        entity.WithdrawalReason = dto.WithdrawalReason;

        await _applicationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Application {ApplicationNumber} withdrawn", entity.ApplicationNumber);
        await UpdateVacancyCounterAsync(entity.JobVacancyId, v => v.ApplicationCount = Math.Max(0, v.ApplicationCount - 1), cancellationToken);

        // Close pipeline stage row: application has left the pipeline as withdrawn
        await _pipelineService.CloseCurrentStageForExitAsync(
            entity.Id, JobApplicationStageExitReason.Withdrawn, updatedByUserId, cancellationToken);

        return true;
    }

    public async Task<bool> MoveToStageAsync(MoveApplicationToStageDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _applicationRepository.GetByIdAsync(dto.ApplicationId);
        if (entity == null)
            throw new ArgumentException($"Job application with ID '{dto.ApplicationId}' not found.");

        // Close current stage history
        var currentHistory = await _stageHistoryRepository.GetCurrentStageAsync(dto.ApplicationId);
        if (currentHistory != null)
        {
            currentHistory.IsCurrent = false;
            currentHistory.ExitedAt = DateTime.UtcNow;
            currentHistory.ExitReason = JobApplicationStageExitReason.Progressed;
            currentHistory.Notes = dto.Notes;
            await _stageHistoryRepository.UpdateAsync(currentHistory);
        }

        // Open new stage history
        var newHistory = new JobApplicationStageHistory
        {
            Id = Guid.NewGuid(),
            TenantId = entity.TenantId,
            JobApplicationId = entity.Id,
            PipelineStageId = dto.PipelineStageId,
            EnteredAt = DateTime.UtcNow,
            IsCurrent = true,
            MovedById = updatedByUserId,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = updatedByUserId.ToString()
        };

        // Derive the correct ApplicationStatus from the stage's functional type
        var targetStage = await _unitOfWork.Repository<RecruitmentPipelineStage>()
            .GetByIdAsync(dto.PipelineStageId);
        entity.Status = targetStage is not null
            ? MapStageTypeToApplicationStatus(targetStage.StageType)
            : ApplicationStatus.UnderReview;

        await _stageHistoryRepository.AddAsync(newHistory);
        await _applicationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Application {ApplicationNumber} moved to stage {StageId}", entity.ApplicationNumber, dto.PipelineStageId);

        // Email #6 — Under Review
        var candUr = await _candidateRepository.GetByIdAsync(entity.JobCandidateId);
        var vacUr  = await _vacancyRepository.GetByIdAsync(entity.JobVacancyId);
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
        var entities = await _stageHistoryRepository.GetByApplicationIdAsync(applicationId);
        return entities.Select(e => e.ToDto());
    }

    // ── Test results ──────────────────────────────────────────────────────────

    public async Task<JobApplicantTestResultDto> AddTestResultAsync(CreateJobApplicantTestResultDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _testResultRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Advance pipeline: test result recorded → Assessment stage (no-op if no pipeline)
        await _pipelineService.AutoAdvanceToStageTypeAsync(
            entity.JobApplicationId, RecruitmentPipelineStageType.Assessment, createdByUserId, cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<JobApplicantTestResultDto>> GetTestResultsAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var entities = await _testResultRepository.GetByApplicationIdAsync(applicationId);
        return entities.Select(e => e.ToDto());
    }

    public async Task<JobApplicantTestResultDto> UpdateTestResultAsync(UpdateJobApplicantTestResultDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _testResultRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Test result with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _testResultRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteTestResultAsync(Guid testResultId, CancellationToken cancellationToken = default)
    {
        var entity = await _testResultRepository.GetByIdAsync(testResultId);
        if (entity == null)
            throw new ArgumentException($"Test result with ID '{testResultId}' not found.");

        await _testResultRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Communications ────────────────────────────────────────────────────────

    public async Task<JobApplicantCommunicationDto> AddCommunicationAsync(CreateJobApplicantCommunicationDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.SentAt = DateTime.UtcNow;

        await _communicationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobApplicantCommunicationDto>> GetCommunicationsAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var entities = await _communicationRepository.GetByApplicationIdAsync(applicationId);
        return entities.Select(e => e.ToDto());
    }

    public async Task<IEnumerable<JobApplicantTestResultDto>> GetTestResultsByVacancyAsync(Guid vacancyId, CancellationToken cancellationToken = default)
    {
        var entities = await _testResultRepository.GetByVacancyIdAsync(vacancyId);
        return entities.Select(e => e.ToDto());
    }

    public async Task<IEnumerable<JobApplicantTestResultDto>> GetTestResultsByTypeAsync(Guid applicationId, JobApplicantTestType testType, CancellationToken cancellationToken = default)
    {
        var entities = await _testResultRepository.GetByTestTypeAsync(applicationId, testType);
        return entities.Select(e => e.ToDto());
    }

    public async Task<ApplicationAutoScoreDto> EvaluateApplicationScoreAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var application = await _applicationRepository.GetWithFullDetailsAsync(applicationId);
        if (application == null)
            throw new ArgumentException($"Job application '{applicationId}' not found.");

        return await EvaluateLoadedApplicationScoreAsync(application, cancellationToken);
    }

    public async Task<ApplicationAutoScoreDto> EvaluateLoadedApplicationScoreAsync(JobApplication application, CancellationToken cancellationToken = default)
    {
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

        if (vacancy?.ShortlistingCriteria == null || !vacancy.ShortlistingCriteria.Any())
        {
            // No criteria defined — give a neutral 100 score so the application isn't excluded
            application.AutoScore = 100m;
            application.AutoScoreBreakdown = null;
            application.ScoredAt = DateTime.UtcNow;
            application.ScoreIsStale = false;
            await _applicationRepository.UpdateAsync(application);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new ApplicationAutoScoreDto
            {
                ApplicationId = applicationId,
                AutoScore = 100m,
                ScoredAt = application.ScoredAt.Value,
                AllMandatoryPassed = true,
                TotalWeight = 0m,
                MaxPossibleScore = 100m,
                Breakdown = new List<CriterionScoreResult>(),
            };
        }

        var breakdown = new List<CriterionScoreResult>();
        decimal totalWeight = 0m;
        decimal earnedScore = 0m;
        bool allMandatoryPassed = true;

        // Evaluate ALL criteria regardless of mandatory failures so the stored breakdown
        // is complete — required for recruiter review and algorithmic-decision audit trails
        foreach (var criterion in vacancy.ShortlistingCriteria)
        {
            var result = EvaluateCriterion(criterion, scoringView);
            breakdown.Add(result);

            if (criterion.IsMandatory && !result.Passed)
                allMandatoryPassed = false;

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

        // Normalise criterion score to 0–100
        decimal criterionScore = totalWeight > 0
            ? Math.Round(earnedScore / totalWeight * 100m, 2)
            : 100m;

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
        // Single bulk fetch — all navigation properties required for scoring loaded in one query
        var applications = await _applicationRepository.GetAllWithFullDetailsByVacancyIdAsync(vacancyId);
        var scoreable = applications
            .Where(a => a.Status != ApplicationStatus.Withdrawn
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

        // ── Pre-normalised sets for O(1) lookups ──────────────────────────────
        public HashSet<string> SkillNames          { get; init; } = new();
        public HashSet<Guid>   SkillIds            { get; init; } = new();
        public HashSet<string> CertificationNames  { get; init; } = new();
        public HashSet<string> QualificationNames  { get; init; } = new();
        public HashSet<Guid>   QualificationIds    { get; init; } = new();
        public HashSet<string> LanguageNames       { get; init; } = new();

        // ── Factory: from live entity ─────────────────────────────────────────
        public static ScoringCandidateView FromEntity(JobCandidate c, JobApplication app) =>
            new()
            {
                YearsOfExperience   = app.YearsOfExperience ?? c.TotalYearsExperience ?? 0,
                DateOfBirth         = c.DateOfBirth,
                Gender              = c.Gender,
                City                = c.City?.ToLowerInvariant() ?? string.Empty,
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
            };

        // ── Factory: from snapshot ────────────────────────────────────────────
        public static ScoringCandidateView FromSnapshot(ApplicationCandidateSnapshot snap) =>
            new()
            {
                YearsOfExperience   = snap.YearsOfExperience ?? snap.TotalYearsExperience ?? 0,
                DateOfBirth         = snap.DateOfBirth,
                Gender              = snap.Gender,
                City                = snap.City?.ToLowerInvariant() ?? string.Empty,
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
            };
    }

    private static CriterionScoreResult EvaluateCriterion(
        JobShortlistingCriteria criterion,
        ScoringCandidateView    view)
    {
        bool passed;
        decimal rawScore;
        string? notes = null;

        switch (criterion.Type)
        {
            case JobShortlistingCriteriaType.YearsOfExperience:
            {
                (passed, rawScore, notes) = EvaluateNumericCriterion(criterion, view.YearsOfExperience, "year(s) of experience");
                break;
            }

            case JobShortlistingCriteriaType.Qualification:
            case JobShortlistingCriteriaType.EducationLevel:
            {
                // ID-first: if the criterion has a catalogue FK and the candidate has any ID match, pass immediately.
                if (criterion.RequiredQualificationId.HasValue && view.QualificationIds.Contains(criterion.RequiredQualificationId.Value))
                {
                    passed   = true;
                    rawScore = 1m;
                    notes    = $"Qualification matched by catalogue ID ({criterion.RequiredQualificationId.Value}).";
                    break;
                }

                // Fall back to string-based multi-value matching.
                var required = SplitValues(criterion.RequiredValue);
                int matched = required.Count(r => view.QualificationNames.Any(c => MatchesValue(c, r, criterion.MatchStrategy)));
                passed = criterion.MatchMode == MandatoryMatchMode.AllRequired
                    ? matched == required.Count
                    : matched > 0;
                rawScore = required.Count > 0 ? (decimal)matched / required.Count : 1m;
                notes = $"{matched}/{required.Count} required qualification(s) matched "
                      + $"[{(criterion.MatchMode == MandatoryMatchMode.AllRequired ? "all required" : "any sufficient")}, {criterion.MatchStrategy}]. "
                      + $"Candidate qualifications: {string.Join(", ", view.QualificationNames)}.";
                break;
            }

            case JobShortlistingCriteriaType.Skill:
            {
                // ID-first: if the criterion has a catalogue FK and the candidate has any ID match, pass immediately.
                if (criterion.RequiredSkillId.HasValue && view.SkillIds.Contains(criterion.RequiredSkillId.Value))
                {
                    passed   = true;
                    rawScore = 1m;
                    notes    = $"Skill matched by catalogue ID ({criterion.RequiredSkillId.Value}).";
                    break;
                }

                // Fall back to string-based multi-value matching.
                var required = SplitValues(criterion.RequiredValue);
                int matched = required.Count(r => view.SkillNames.Any(c => MatchesValue(c, r, criterion.MatchStrategy)));
                passed = criterion.MatchMode == MandatoryMatchMode.AllRequired
                    ? matched == required.Count
                    : matched > 0;
                rawScore = required.Count > 0 ? (decimal)matched / required.Count : 1m;
                notes = $"{matched}/{required.Count} required skill(s) matched "
                      + $"[{(criterion.MatchMode == MandatoryMatchMode.AllRequired ? "all required" : "any sufficient")}, {criterion.MatchStrategy}].";
                break;
            }

            case JobShortlistingCriteriaType.Certification:
            {
                var required = SplitValues(criterion.RequiredValue);
                int matched = required.Count(r => view.CertificationNames.Any(c => MatchesValue(c, r, criterion.MatchStrategy)));
                passed = criterion.MatchMode == MandatoryMatchMode.AllRequired
                    ? matched == required.Count
                    : matched > 0;
                rawScore = required.Count > 0 ? (decimal)matched / required.Count : 1m;
                notes = $"{matched}/{required.Count} required certification(s) matched "
                      + $"[{(criterion.MatchMode == MandatoryMatchMode.AllRequired ? "all required" : "any sufficient")}, {criterion.MatchStrategy}].";
                break;
            }

            case JobShortlistingCriteriaType.Age:
            {
                decimal ageYears = (decimal)((DateTime.UtcNow - view.DateOfBirth).TotalDays / 365.25);
                (passed, rawScore, notes) = EvaluateNumericCriterion(criterion, ageYears, "years old");
                break;
            }

            case JobShortlistingCriteriaType.Gender:
            {
                var req = criterion.RequiredValue?.Trim().ToLowerInvariant();
                passed = string.IsNullOrEmpty(req) || req == "any"
                      || view.Gender.ToString().ToLowerInvariant() == req;
                rawScore = passed ? 1m : 0m;
                break;
            }

            case JobShortlistingCriteriaType.Language:
            {
                var required = SplitValues(criterion.RequiredValue);
                if (!required.Any())
                {
                    passed = true;
                    rawScore = 1m;
                    notes = "No required language specified; defaulting to pass.";
                }
                else
                {
                    int matched = required.Count(r => view.LanguageNames.Any(c => MatchesValue(c, r, criterion.MatchStrategy)));
                    passed = criterion.MatchMode == MandatoryMatchMode.AllRequired
                        ? matched == required.Count
                        : matched > 0;
                    rawScore = required.Count > 0 ? (decimal)matched / required.Count : 1m;
                    notes = $"{matched}/{required.Count} required language(s) matched "
                          + $"[{(criterion.MatchMode == MandatoryMatchMode.AllRequired ? "all required" : "any sufficient")}, {criterion.MatchStrategy}]. "
                          + $"Candidate languages: {string.Join(", ", view.LanguageNames)}.";
                }
                break;
            }

            case JobShortlistingCriteriaType.Location:
            {
                var req = criterion.RequiredValue?.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(req))
                {
                    passed = true;
                    rawScore = 1m;
                    notes = "No required location specified; defaulting to pass.";
                }
                else
                {
                    var op = criterion.ComparisonOperator ?? ShortlistingComparisonOperator.Contains;
                    passed = op == ShortlistingComparisonOperator.Equals
                        ? view.City == req
                        : view.City.Contains(req);
                    rawScore = passed ? 1m : 0m;
                    notes = $"Required location: '{criterion.RequiredValue}'; Candidate city: '{view.City}'.";
                }
                break;
            }

            default:
            {
                // Unknown / Other — default pass with neutral score
                passed = true;
                rawScore = 1m;
                notes = "Criterion type not auto-evaluated; defaulting to pass.";
                break;
            }
        }

        decimal weightedScore = rawScore * criterion.Weight;

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
        };
    }

    private static (bool passed, decimal rawScore, string notes) EvaluateNumericCriterion(
        JobShortlistingCriteria criterion,
        decimal candidateValue,
        string unit)
    {
        decimal min = criterion.MinValue ?? 0;
        decimal max = criterion.MaxValue ?? decimal.MaxValue;
        var op = criterion.ComparisonOperator ?? ShortlistingComparisonOperator.Between;

        bool passed = op switch
        {
            ShortlistingComparisonOperator.GreaterThan => candidateValue > min,
            ShortlistingComparisonOperator.GreaterThanOrEqual => candidateValue >= min,
            ShortlistingComparisonOperator.LessThan => candidateValue < max,
            ShortlistingComparisonOperator.LessThanOrEqual => candidateValue <= max,
            ShortlistingComparisonOperator.Equals => candidateValue == min,
            _ => candidateValue >= min && (criterion.MaxValue == null || candidateValue <= max),
        };

        decimal rawScore;
        if (passed)
        {
            rawScore = 1m;
        }
        else if (op is ShortlistingComparisonOperator.GreaterThan or ShortlistingComparisonOperator.GreaterThanOrEqual or ShortlistingComparisonOperator.Between)
        {
            rawScore = min > 0 ? Math.Min(candidateValue / min, 0.8m) : 0m;
        }
        else
        {
            rawScore = 0m;
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
        return (passed, rawScore, notes);
    }

    // ── Public-upload path validation ────────────────────────────────────────

    /// <summary>Folder that <c>POST /api/public/cv-upload</c> writes into.</summary>
    private const string CvUploadFolder = "cv-uploads/";

    private static readonly string[] AllowedCvExtensions = { ".pdf", ".doc", ".docx" };

    /// <summary>
    /// Validates a CV path supplied on the anonymous apply payload.
    ///
    /// <para>The apply endpoint takes <c>CvFilePath</c> as a free string and previously persisted it
    /// verbatim, with nothing tying it to a file actually returned by <c>/api/public/cv-upload</c>. An
    /// applicant could therefore point a candidate record at any path on disk. We accept only the shape
    /// the upload endpoint produces: a file directly inside <see cref="CvUploadFolder"/>, with an allowed
    /// extension and no traversal segments.</para>
    /// </summary>
    private static string? ValidateUploadedCvPath(string? cvFilePath)
    {
        if (string.IsNullOrWhiteSpace(cvFilePath))
            return null;

        var path = cvFilePath.Trim().Replace('\\', '/');

        if (!path.StartsWith(CvUploadFolder, StringComparison.OrdinalIgnoreCase)
            || path.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(path))
        {
            throw new InvalidOperationException(
                "The CV file reference is not valid. Please upload your CV again.");
        }

        // Exactly one segment after the folder — no nested paths.
        var fileName = path[CvUploadFolder.Length..];
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains('/'))
            throw new InvalidOperationException(
                "The CV file reference is not valid. Please upload your CV again.");

        var extension = Path.GetExtension(fileName);
        if (!AllowedCvExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Only PDF, DOC, and DOCX CVs are accepted.");

        return path;
    }

    // ── Concurrency-safe vacancy counter update ─────────────────────────────

    /// <summary>
    /// Applies <paramref name="mutate"/> to the vacancy's counter columns and saves.
    /// Retries up to 3 times on <see cref="DbUpdateConcurrencyException"/> caused by the
    /// RowVersion optimistic concurrency token, reloading the fresh row before each retry.
    /// </summary>
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
            catch (DbUpdateConcurrencyException ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(
                    "Concurrency conflict updating vacancy counter for {VacancyId}. Attempt {Attempt}/{Max}.",
                    vacancyId, attempt + 1, maxRetries);
                foreach (var entry in ex.Entries.Where(e => e.Entity is JobVacancy))
                    await entry.ReloadAsync(cancellationToken);
            }
        }
        _logger.LogError(
            "Vacancy counter update for {VacancyId} failed after {Max} retries.",
            vacancyId, maxRetries);
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

    public async Task<RecruitmentBulkOperationResultDto> BulkShortlistAsync(
        BulkShortlistDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var result = new RecruitmentBulkOperationResultDto();
        foreach (var appId in dto.ApplicationIds)
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
        BulkRejectDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var result = new RecruitmentBulkOperationResultDto();
        foreach (var appId in dto.ApplicationIds)
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
        var vacancyForDeadline = await _vacancyRepository.GetByIdAsync(dto.VacancyId);
        if (vacancyForDeadline?.ShortlistingDeadline != null && DateTime.UtcNow > vacancyForDeadline.ShortlistingDeadline.Value)
            throw new InvalidOperationException(
                $"The shortlisting deadline for this vacancy passed on {vacancyForDeadline.ShortlistingDeadline.Value:d}. " +
                "Contact HR to extend or override the deadline.");

        var applications = await _applicationRepository.GetByVacancyIdAsync(dto.VacancyId);
        var eligible = applications
            .Where(a => a.Status != ApplicationStatus.Withdrawn
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
        var vacancy = await _vacancyRepository.GetByIdAsync(vacancyId);
        if (vacancy == null)
            throw new ArgumentException($"Vacancy '{vacancyId}' not found.");

        var applications = await _applicationRepository.GetByVacancyIdAsync(vacancyId);
        var shortlisted = applications
            .Where(a => a.Status == ApplicationStatus.Shortlisted && a.ShortlistNotificationSentAt == null)
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
        var vacancy = await _vacancyRepository.GetByIdAsync(vacancyId);
        if (vacancy == null)
            throw new ArgumentException($"Vacancy '{vacancyId}' not found.");

        var applications = await _applicationRepository.GetByVacancyIdAsync(vacancyId);
        var rejected = applications
            .Where(a => a.Status == ApplicationStatus.Rejected && a.RejectionNotificationSentAt == null)
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
        var vacancy = await _vacancyRepository.GetByIdAsync(vacancyId);
        if (vacancy == null)
            throw new ArgumentException($"Vacancy '{vacancyId}' not found.");

        var applications = await _applicationRepository.GetByVacancyIdAsync(vacancyId);
        var all = applications.ToList();
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
        var vacancy = await _vacancyRepository.GetByIdAsync(vacancyId);
        if (vacancy == null)
            throw new ArgumentException($"Vacancy '{vacancyId}' not found.");

        var appIdList = applicationIds.ToList();
        var allApps = await _applicationRepository.GetByVacancyIdAsync(vacancyId);
        var selectedApps = appIdList.Any()
            ? allApps.Where(a => appIdList.Contains(a.Id)).ToList()
            : allApps.Where(a => a.Status == ApplicationStatus.Shortlisted).ToList();

        var criteria = vacancy.ShortlistingCriteria?.ToList() ?? new List<JobShortlistingCriteria>();
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

        _logger.LogInformation(
            "Shortlist for vacancy {VacancyId} submitted for approval by {UserId}",
            dto.VacancyId, submittedByUserId);
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
        var vacancy = await _vacancyRepository.GetByIdAsync(vacancyId);
        if (vacancy == null)
            throw new ArgumentException($"Vacancy '{vacancyId}' not found.");

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
        var logs = _applicationRepository.GetQueryable()
            .Where(a => a.Id == applicationId)
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
        var application = await _applicationRepository.GetByIdAsync(dto.ApplicationId);
        if (application == null)
            throw new ArgumentException($"Application '{dto.ApplicationId}' not found.");

        var review = new ShortlistReview
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
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
        var application = await _applicationRepository.GetByIdAsync(applicationId);
        if (application == null)
            throw new ArgumentException($"Application '{applicationId}' not found.");

        var reviews = await _shortlistReviewRepository.GetByApplicationIdAsync(applicationId);
        var reviewList = reviews.ToList();
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
        var review = await _shortlistReviewRepository.GetByIdAsync(reviewId);
        if (review == null)
            throw new ArgumentException($"Shortlist review '{reviewId}' not found.");

        if (review.IsFinalized)
            throw new InvalidOperationException("This review is already finalized.");

        review.IsFinalized = true;
        review.FinalizedAt = DateTime.UtcNow;
        review.UpdatedAt = DateTime.UtcNow;
        review.UpdatedBy = updatedByUserId.ToString();

        await _shortlistReviewRepository.UpdateAsync(review);

        // Recompute aggregated score on the application
        var application = await _applicationRepository.GetByIdAsync(review.JobApplicationId);
        if (application != null)
        {
            var allReviews = (await _shortlistReviewRepository.GetByApplicationIdAsync(review.JobApplicationId)).ToList();
            var finalizedReviews = allReviews.Where(r => r.IsFinalized).ToList();
            application.AggregatedReviewScore = finalizedReviews.Any()
                ? Math.Round(finalizedReviews.Average(r => r.Score), 2)
                : (decimal?)null;
            await _applicationRepository.UpdateAsync(application);
        }

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
        var vacancy = await _vacancyRepository.GetByIdAsync(vacancyId);
        if (vacancy == null)
            throw new ArgumentException($"Vacancy '{vacancyId}' not found.");

        var applications = (await _applicationRepository.GetByVacancyIdAsync(vacancyId)).ToList();

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
        var vacancy = await _vacancyRepository.GetByIdAsync(vacancyId);
        if (vacancy == null)
            throw new ArgumentException($"Vacancy '{vacancyId}' not found.");

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
        var vacancy = await _vacancyRepository.GetByIdAsync(vacancyId);
        if (vacancy == null)
            throw new ArgumentException($"Vacancy '{vacancyId}' not found.");

        if (!vacancy.IsBlindScreeningEnabled)
            throw new InvalidOperationException("Blind screening is not enabled for this vacancy. Enable it on the vacancy settings first.");

        var applications = await _applicationRepository.GetByVacancyIdAsync(vacancyId);
        return applications
            .Where(a => a.Status != ApplicationStatus.Withdrawn)
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
        var application = await _applicationRepository.GetByIdAsync(dto.ApplicationId);
        if (application == null)
            throw new ArgumentException($"Application '{dto.ApplicationId}' not found.");

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
        var application = await _applicationRepository.GetByIdAsync(applicationId);
        if (application == null)
            throw new ArgumentException($"Application '{applicationId}' not found.");

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
        var vacancy = await _vacancyRepository.GetByIdAsync(vacancyId);
        if (vacancy == null)
            throw new ArgumentException($"Vacancy '{vacancyId}' not found.");

        var applications = (await _applicationRepository.GetByVacancyIdAsync(vacancyId))
            .Where(a => a.Status == ApplicationStatus.Shortlisted
                     || a.Status == ApplicationStatus.Waitlisted)
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

        // 4. Resolve or create shadow JobCandidate keyed by employee email
        var candidate = await _candidateRepository.GetByEmailAsync(employee.EmailAddress);
        if (candidate == null)
        {
            candidate = new JobCandidate
            {
                TenantId            = tenantId,
                FirstName           = employee.FirstName,
                MiddleName          = employee.MiddleName,
                LastName            = employee.LastName,
                Email               = employee.EmailAddress,
                Phone               = (employee.MobileNumber ?? employee.TelephoneNumber) ?? string.Empty,
                Gender              = employee.Gender ?? Gender.PreferNotToSay,
                DateOfBirth         = employee.DateOfBirth.HasValue
                                        ? employee.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)
                                        : DateTime.MinValue,
                City                = employee.City ?? string.Empty,
                CountryId           = employee.CountryId ?? Guid.Empty,
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

        // Resolve or create shadow candidate
        var candidate = await _candidateRepository.GetByEmailAsync(employee.EmailAddress);
        if (candidate == null)
        {
            candidate = new JobCandidate
            {
                TenantId           = tenantId,
                FirstName          = employee.FirstName,
                MiddleName         = employee.MiddleName,
                LastName           = employee.LastName,
                Email              = employee.EmailAddress,
                Phone              = (employee.MobileNumber ?? employee.TelephoneNumber) ?? string.Empty,
                Gender             = employee.Gender ?? Gender.PreferNotToSay,
                DateOfBirth        = employee.DateOfBirth.HasValue
                                       ? employee.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)
                                       : DateTime.MinValue,
                City               = employee.City ?? string.Empty,
                CountryId          = employee.CountryId ?? Guid.Empty,
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

    public async Task<IEnumerable<JobApplicationSummaryDto>> GetByInternalEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _applicationRepository.GetQueryable()
            .Where(a => a.InternalEmployeeId == employeeId)
            .Include(a => a.JobCandidate)
            .Include(a => a.JobVacancy)
            .OrderByDescending(a => a.ApplicationDate)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
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

        // The CV path is supplied by the (anonymous) client, so it must be one WE issued from
        // POST /api/public/cv-upload — not an arbitrary string pointing anywhere on disk.
        var cvFilePath = ValidateUploadedCvPath(dto.CvFilePath);

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
                CountryId       = dto.CountryId ?? Guid.Empty,
                LinkedInProfile = dto.LinkedInProfile,
                PortfolioUrl    = dto.PortfolioUrl,
                CvFilePath      = cvFilePath,
                AvailableFrom   = dto.AvailableFrom,
                IsInTalentPool  = dto.AddToTalentPool,
                TalentPoolAddedDate = dto.AddToTalentPool ? DateTime.UtcNow : null,
                CandidateNumber = await _candidateRepository.GetNextCandidateNumberAsync(),
                CreatedBy       = "external-portal",
            };
            await _candidateRepository.AddAsync(candidate);
        }
        else
        {
            // Update opt-in if newly consented; refresh CV path if provided
            bool candidateUpdated = false;
            if (dto.AddToTalentPool && !candidate.IsInTalentPool)
            {
                candidate.IsInTalentPool = true;
                candidate.TalentPoolAddedDate = DateTime.UtcNow;
                candidateUpdated = true;
            }
            if (!string.IsNullOrWhiteSpace(cvFilePath))
            {
                candidate.CvFilePath = cvFilePath;
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

        // 4. Generate a unique tracking token
        var trackingToken = $"TKN-{Guid.NewGuid():N}".ToUpper();

        // 5. Create the application
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
        application.ApplicationNumber = await _applicationRepository.GetNextApplicationNumberAsync();

        await _applicationRepository.AddAsync(application);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await UpdateVacancyCounterAsync(vacancy.Id, v => v.ApplicationCount++, cancellationToken);

        // 6. Persist structured profile data into child tables
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

        // Languages
        foreach (var l in dto.Languages)
        {
            await _languageRepository.AddAsync(new JobCandidateLanguage
            {
                TenantId       = tenantId,
                JobCandidateId = candidate.Id,
                LanguageName   = l.LanguageName.Trim(),
                Proficiency    = l.Proficiency,
                CreatedBy      = "external-portal",
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

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

            var snapshotLangs = dto.Languages.Select(l => new SnapshotLanguage
            {
                NormalisedName = l.LanguageName.Trim().ToLowerInvariant(),
                DisplayName    = l.LanguageName.Trim(),
                Proficiency    = (int)l.Proficiency,
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
                TotalYearsExperience = null,   // not collected on the external form
                Skills               = snapshotSkills.AsReadOnly(),
                Qualifications       = snapshotQuals.AsReadOnly(),
                Languages            = snapshotLangs.AsReadOnly(),
                WorkHistories        = snapshotWork.AsReadOnly(),
            };

            application.ProfileSnapshotJson = System.Text.Json.JsonSerializer.Serialize(snapshot);
            await _applicationRepository.UpdateAsync(application);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Non-fatal: scoring falls back to live profile for this application.
            _logger.LogWarning(ex,
                "Profile snapshot capture failed for external application {AppNumber} — scoring will use live profile.",
                application.ApplicationNumber);
        }

        _logger.LogInformation(
            "External application {AppNumber} created for candidate {Email} on vacancy {VacancyId} " +
            "(WorkHistory: {WH}, Quals: {Q}, Referees: {R}, Skills: {S}, Languages: {L})",
            application.ApplicationNumber, dto.Email, dto.VacancyId,
            dto.WorkHistories.Count, dto.Qualifications.Count,
            dto.Referees.Count, dto.Skills.Count, dto.Languages.Count);

        // Place the application into the first pipeline stage, if the vacancy has one.
        await _pipelineService.PlaceInFirstPipelineStageAsync(
            application.Id, vacancy.Id, tenantId, cancellationToken);

        var confirmation = new ExternalApplicationConfirmationDto
        {
            ApplicationNumber = application.ApplicationNumber,
            TrackingToken     = trackingToken,
            CandidateName     = $"{dto.FirstName} {dto.LastName}".Trim(),
            JobTitle          = vacancy.JobTitle,
            VacancyNumber     = vacancy.VacancyNumber,
            SubmittedAt       = application.ApplicationDate,
        };

        await SendApplicationReceivedEmailAsync(dto.Email, confirmation);

        return confirmation;
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

        await _templatedEmail.SendAsync(
            RecruitmentEmailCatalog.Module, RecruitmentEmailCatalog.Events.ApplicationReceived, toEmail, tokens);
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

        await _templatedEmail.SendAsync(
            RecruitmentEmailCatalog.Module, RecruitmentEmailCatalog.Events.ApplicationWithdrawn, toEmail, tokens);
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

        await _templatedEmail.SendAsync(
            RecruitmentEmailCatalog.Module, RecruitmentEmailCatalog.Events.ApplicationUnderReview, toEmail, tokens);
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

    // ── Stage type → ApplicationStatus mapping ────────────────────────────────
    // Mirrors ApplicationPipelineService.MapStageTypeToStatus; kept here so that
    // JobApplicationService.MoveToStageAsync does not depend on the pipeline service.

    private static ApplicationStatus MapStageTypeToApplicationStatus(
        RecruitmentPipelineStageType stageType) => stageType switch
    {
        RecruitmentPipelineStageType.ApplicationReview   => ApplicationStatus.UnderReview,
        RecruitmentPipelineStageType.Screening            => ApplicationStatus.UnderReview,
        RecruitmentPipelineStageType.HiringManagerReview  => ApplicationStatus.UnderReview,
        RecruitmentPipelineStageType.Assessment           => ApplicationStatus.AssessmentPending,
        RecruitmentPipelineStageType.Interview            => ApplicationStatus.InterviewScheduled,
        RecruitmentPipelineStageType.PreEmploymentCheck   => ApplicationStatus.PreEmploymentCheck,
        RecruitmentPipelineStageType.Offer                => ApplicationStatus.OfferExtended,
        RecruitmentPipelineStageType.Hired                => ApplicationStatus.Hired,
        _                                                 => ApplicationStatus.UnderReview,
    };
}
