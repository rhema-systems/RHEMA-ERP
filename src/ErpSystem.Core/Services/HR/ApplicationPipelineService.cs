using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Implements the recruitment application pipeline (Kanban) logic:
/// stage transitions with full rule enforcement, and the aggregated board query.
/// </summary>
public sealed class ApplicationPipelineService : IApplicationPipelineService
{
    private readonly IJobApplicationRepository _applicationRepository;
    private readonly IJobApplicationStageHistoryRepository _stageHistoryRepository;
    private readonly IJobVacancyRepository _vacancyRepository;
    private readonly IRecruitmentPipelineStageRepository _pipelineStageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ApplicationPipelineService> _logger;
    private readonly IJobCandidateRepository _candidateRepository;
    private readonly IEmailService _email;
    private readonly ITemplatedEmailService _templatedEmail;

    public ApplicationPipelineService(
        IJobApplicationRepository applicationRepository,
        IJobApplicationStageHistoryRepository stageHistoryRepository,
        IJobVacancyRepository vacancyRepository,
        IRecruitmentPipelineStageRepository pipelineStageRepository,
        IUnitOfWork unitOfWork,
        ILogger<ApplicationPipelineService> logger,
        IJobCandidateRepository candidateRepository,
        IEmailService email,
        ITemplatedEmailService templatedEmail)
    {
        _applicationRepository = applicationRepository;
        _stageHistoryRepository = stageHistoryRepository;
        _vacancyRepository = vacancyRepository;
        _pipelineStageRepository = pipelineStageRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _candidateRepository = candidateRepository;
        _email = email;
        _templatedEmail = templatedEmail;
    }

    // =========================================================================
    // MOVE APPLICATION TO STAGE
    // =========================================================================

    public async Task MoveApplicationToStageAsync(
        Guid applicationId,
        Guid targetStageId,
        Guid movedByEmployeeId,
        CancellationToken cancellationToken = default)
    {
        // ── 1. Load and validate application ─────────────────────────────────

        var application = await _applicationRepository.GetByIdAsync(applicationId);
        if (application is null)
            throw new KeyNotFoundException($"Application '{applicationId}' not found.");

        if (application.Status is ApplicationStatus.Hired or ApplicationStatus.Withdrawn)
            throw new InvalidOperationException(
                $"Cannot move an application with status '{application.Status}'.");

        // ── 2. Load and validate target stage ────────────────────────────────

        var targetStage = await _pipelineStageRepository.GetByIdAsync(targetStageId);
        if (targetStage is null)
            throw new KeyNotFoundException($"Pipeline stage '{targetStageId}' not found.");

        // ── 3. Validate pipeline membership ──────────────────────────────────

        var vacancy = await _vacancyRepository.GetByIdAsync(application.JobVacancyId);
        if (vacancy is null)
            throw new InvalidOperationException("The associated vacancy could not be found.");

        if (vacancy.RecruitmentPipelineId is null)
            throw new InvalidOperationException(
                "The vacancy does not have a recruitment pipeline assigned.");

        if (targetStage.RecruitmentPipelineId != vacancy.RecruitmentPipelineId)
            throw new InvalidOperationException(
                "The target stage does not belong to this vacancy's recruitment pipeline.");

        // ── 4. Load current stage and enforce transition rules ───────────────

        var currentHistory = await _stageHistoryRepository.GetCurrentStageAsync(applicationId);
        RecruitmentPipelineStage? currentStage = null;

        if (currentHistory is not null)
        {
            if (currentHistory.PipelineStageId == targetStageId)
                throw new InvalidOperationException(
                    "The application is already in the target stage.");

            currentStage = await _pipelineStageRepository.GetByIdAsync(currentHistory.PipelineStageId);

            // Moving backward or to the same order → requires CanRepeat on the target
            if (targetStage.Order <= currentStage!.Order && !targetStage.CanRepeat)
                throw new InvalidOperationException(
                    $"Stage '{targetStage.Name}' does not permit repeat entries (CanRepeat = false).");
        }

        // MaxAttempts check: count all (including closed) visits to the target stage
        if (targetStage.MaxAttempts.HasValue)
        {
            var priorAttempts = await _unitOfWork.Repository<JobApplicationStageHistory>()
                .CountAsync(h =>
                    h.JobApplicationId == applicationId &&
                    h.PipelineStageId == targetStageId);

            if (priorAttempts >= targetStage.MaxAttempts.Value)
                throw new InvalidOperationException(
                    $"Stage '{targetStage.Name}' has reached its maximum attempt limit " +
                    $"({targetStage.MaxAttempts.Value}).");
        }

        // ── 5. Execute all writes in one SaveChangesAsync ────────────────────
        // All three writes are staged in EF Core's change tracker and flushed
        // in a single call, so SQL Server's implicit transaction guarantees
        // atomicity without needing a user-initiated transaction (which is
        // incompatible with SqlServerRetryingExecutionStrategy / EnableRetryOnFailure).

        // Close the current stage record
        if (currentHistory is not null)
        {
            currentHistory.IsCurrent = false;
            currentHistory.ExitedAt = DateTime.UtcNow;
            currentHistory.ExitReason = JobApplicationStageExitReason.Progressed;
            await _stageHistoryRepository.UpdateAsync(currentHistory);
        }

        // Open the new stage record
        var newHistory = new JobApplicationStageHistory
        {
            TenantId = application.TenantId,
            JobApplicationId = applicationId,
            PipelineStageId = targetStageId,
            EnteredAt = DateTime.UtcNow,
            IsCurrent = true,
            MovedById = movedByEmployeeId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = movedByEmployeeId.ToString()
        };
        await _stageHistoryRepository.AddAsync(newHistory);

        // Update application status to reflect the new stage type
        application.Status = MapStageTypeToStatus(targetStage.StageType);
        await _applicationRepository.UpdateAsync(application);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Application {ApplicationId} moved from stage '{FromStage}' (order {FromOrder}) " +
            "to stage '{ToStage}' (order {ToOrder}) by employee {EmployeeId}.",
            applicationId,
            currentStage?.Name ?? "none",
            currentStage?.Order.ToString() ?? "-",
            targetStage.Name,
            targetStage.Order,
            movedByEmployeeId);

        // Adjust vacancy stage counters (Interview / Offer / Hire) with concurrency-safe retry
        await UpdateVacancyCounterAsync(
            application.JobVacancyId,
            v => AdjustVacancyCounters(v, currentStage?.StageType, targetStage.StageType),
            cancellationToken);

        // Email #10 — Assessment Pending (sent outside the transaction)
        if (application.Status == ApplicationStatus.AssessmentPending)
        {
            var candAp = await _candidateRepository.GetByIdAsync(application.JobCandidateId);
            await SendAssessmentPendingEmailAsync(
                candAp?.Email ?? string.Empty,
                candAp?.FullName ?? "Candidate",
                application.ApplicationNumber,
                vacancy?.JobTitle ?? "the position",
                targetStage.Name);
        }
    }

    // =========================================================================
    // GET PIPELINE BY VACANCY (KANBAN BOARD)
    // =========================================================================

    public async Task<List<PipelineStageWithApplicationsDto>> GetPipelineByVacancyAsync(
        Guid vacancyId,
        CancellationToken cancellationToken = default)
    {
        var vacancy = await _vacancyRepository.GetByIdAsync(vacancyId);
        if (vacancy is null)
            throw new KeyNotFoundException($"Vacancy '{vacancyId}' not found.");

        // No pipeline assigned → return empty board
        if (vacancy.RecruitmentPipelineId is null)
            return new List<PipelineStageWithApplicationsDto>();

        // Load stages ordered for Kanban columns
        var stages = (await _pipelineStageRepository.GetByPipelineIdAsync(vacancy.RecruitmentPipelineId.Value))
            .OrderBy(s => s.Order)
            .ToList();

        // Load all applications for this vacancy (includes JobCandidate navigation)
        var applications = (await _applicationRepository.GetByVacancyIdAsync(vacancyId)).ToList();

        if (applications.Count == 0)
            return stages.Select(BuildEmptyColumn).ToList();

        // Single query to fetch all current stage histories for these applications
        var appIds = applications.Select(a => a.Id).ToHashSet();
        var currentHistories = await _unitOfWork.Repository<JobApplicationStageHistory>()
            .FindAsync(h => appIds.Contains(h.JobApplicationId) && h.IsCurrent);

        var historyByAppId = currentHistories.ToDictionary(h => h.JobApplicationId);

        // Project into Kanban board: one column per stage
        return stages.Select(stage => new PipelineStageWithApplicationsDto
        {
            StageId = stage.Id,
            StageName = stage.Name,
            Order = stage.Order,
            StageType = stage.StageType,
            Applications = applications
                .Where(a =>
                    historyByAppId.TryGetValue(a.Id, out var h) &&
                    h.PipelineStageId == stage.Id)
                .Select(a => new ApplicationPipelineCardDto
                {
                    ApplicationId   = a.Id,
                    CandidateId     = a.JobCandidateId,
                    ApplicationNumber = a.ApplicationNumber,
                    CandidateName   = a.JobCandidate?.FullName ?? string.Empty,
                    Initials        = GetInitials(a.JobCandidate?.FullName ?? string.Empty),
                    CurrentStageId  = stage.Id,
                    AutoScore       = a.AutoScore,
                    Status          = a.Status,
                    DateApplied     = a.ApplicationDate,
                    EnteredStageAt  = historyByAppId[a.Id].EnteredAt
                })
                .ToList()
        }).ToList();
    }

    // =========================================================================
    // BULK OPERATIONS
    // =========================================================================

    public async Task<RecruitmentBulkOperationResultDto> BulkMoveToStageAsync(
        RecruitmentBulkMoveToStageDto dto,
        Guid movedByEmployeeId,
        CancellationToken cancellationToken = default)
    {
        var result = new RecruitmentBulkOperationResultDto();
        foreach (var appId in dto.ApplicationIds)
        {
            try
            {
                await MoveApplicationToStageAsync(appId, dto.TargetStageId, movedByEmployeeId, cancellationToken);
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

    public async Task<RecruitmentBulkOperationResultDto> BulkPipelineRejectAsync(
        RecruitmentBulkPipelineRejectDto dto,
        Guid rejectedByEmployeeId,
        CancellationToken cancellationToken = default)
    {
        var result = new RecruitmentBulkOperationResultDto();
        foreach (var appId in dto.ApplicationIds)
        {
            try
            {
                var application = await _applicationRepository.GetByIdAsync(appId);
                if (application is null)
                    throw new KeyNotFoundException($"Application '{appId}' not found.");

                if (application.Status is ApplicationStatus.Hired
                                       or ApplicationStatus.Withdrawn
                                       or ApplicationStatus.Rejected)
                    throw new InvalidOperationException(
                        $"Cannot reject an application that is already {application.Status}.");

                bool wasShortlisted = application.Status == ApplicationStatus.Shortlisted;

                // Close the current stage history record (if any)
                var currentHistory = await _stageHistoryRepository.GetCurrentStageAsync(appId);
                if (currentHistory is not null)
                {
                    currentHistory.IsCurrent = false;
                    currentHistory.ExitedAt = DateTime.UtcNow;
                    currentHistory.ExitReason = JobApplicationStageExitReason.Rejected;
                    await _stageHistoryRepository.UpdateAsync(currentHistory);
                }

                application.Status = ApplicationStatus.Rejected;
                application.RejectionReason = dto.RejectionReason;
                application.RejectedById = rejectedByEmployeeId;
                application.RejectedDate = DateTime.UtcNow;
                await _applicationRepository.UpdateAsync(application);

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // Decrement ShortlistedCount outside the transaction with concurrency-safe retry
                if (wasShortlisted)
                {
                    await UpdateVacancyCounterAsync(
                        application.JobVacancyId,
                        v => { if (v.ShortlistedCount > 0) v.ShortlistedCount--; },
                        cancellationToken);
                }

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

    // =========================================================================
    // PLACE APPLICATION IN FIRST PIPELINE STAGE (called at submission time)
    // =========================================================================

    public async Task PlaceInFirstPipelineStageAsync(
        Guid applicationId,
        Guid vacancyId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var vacancy = await _vacancyRepository.GetByIdAsync(vacancyId);
            if (vacancy?.RecruitmentPipelineId is null)
                return; // no pipeline configured — nothing to do

            var stages = (await _pipelineStageRepository.GetByPipelineIdAsync(vacancy.RecruitmentPipelineId.Value))
                .OrderBy(s => s.Order)
                .ToList();

            var firstStage = stages.FirstOrDefault(s => s.IsActive);
            if (firstStage is null)
                return; // pipeline has no active stages yet

            var application = await _applicationRepository.GetByIdAsync(applicationId);
            if (application is null)
                return;

            // Guard: if a stage history row was already created (should not happen, but to be safe)
            var existing = await _stageHistoryRepository.GetCurrentStageAsync(applicationId);
            if (existing is not null)
                return;

            var history = new JobApplicationStageHistory
            {
                TenantId         = tenantId,
                JobApplicationId = applicationId,
                PipelineStageId  = firstStage.Id,
                EnteredAt        = DateTime.UtcNow,
                IsCurrent        = true,
                // System-initiated placement — no human mover
                MovedById        = null,
                CreatedAt        = DateTime.UtcNow,
                CreatedBy        = "system",
            };

            await _stageHistoryRepository.AddAsync(history);

            // Sync ApplicationStatus with the stage type
            application.Status = MapStageTypeToStatus(firstStage.StageType);
            await _applicationRepository.UpdateAsync(application);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Application {ApplicationId} placed into first pipeline stage '{StageName}' (order {Order}) on submission.",
                applicationId, firstStage.Name, firstStage.Order);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to place application {ApplicationId} into first pipeline stage — " +
                "application remains visible without a stage history row.",
                applicationId);
        }
    }

    // =========================================================================
    // AUTO-ADVANCE BY STAGE TYPE
    // =========================================================================

    public async Task AutoAdvanceToStageTypeAsync(
        Guid applicationId,
        RecruitmentPipelineStageType stageType,
        Guid movedByEmployeeId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var application = await _applicationRepository.GetByIdAsync(applicationId);
            if (application is null) return;

            // Terminal states: already resolved — no further stage movement
            if (application.Status is ApplicationStatus.Hired or ApplicationStatus.Withdrawn)
                return;

            var vacancy = await _vacancyRepository.GetByIdAsync(application.JobVacancyId);
            if (vacancy?.RecruitmentPipelineId is null) return; // no pipeline configured

            var stages = await _pipelineStageRepository.GetByPipelineIdAsync(vacancy.RecruitmentPipelineId.Value);
            var targetStage = stages.FirstOrDefault(s => s.StageType == stageType && s.IsActive);
            if (targetStage is null) return; // no matching active stage — graceful no-op

            // Guard: already in the target stage
            var currentHistory = await _stageHistoryRepository.GetCurrentStageAsync(applicationId);
            if (currentHistory?.PipelineStageId == targetStage.Id) return;

            await MoveApplicationToStageAsync(applicationId, targetStage.Id, movedByEmployeeId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "AutoAdvanceToStageTypeAsync failed for application {ApplicationId} → stage type {StageType}. " +
                "Application status was not updated via pipeline.",
                applicationId, stageType);
        }
    }

    // =========================================================================
    // CLOSE CURRENT STAGE ON EXIT (reject / withdraw)
    // =========================================================================

    public async Task CloseCurrentStageForExitAsync(
        Guid applicationId,
        JobApplicationStageExitReason exitReason,
        Guid closedByEmployeeId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var currentHistory = await _stageHistoryRepository.GetCurrentStageAsync(applicationId);
            if (currentHistory is null) return; // no open stage row — no-op

            currentHistory.IsCurrent = false;
            currentHistory.ExitedAt = DateTime.UtcNow;
            currentHistory.ExitReason = exitReason;
            await _stageHistoryRepository.UpdateAsync(currentHistory);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Stage history for application {ApplicationId} closed with exit reason {ExitReason} by {EmployeeId}.",
                applicationId, exitReason, closedByEmployeeId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "CloseCurrentStageForExitAsync failed for application {ApplicationId} with exit reason {ExitReason}.",
                applicationId, exitReason);
        }
    }

    // =========================================================================
    // PRIVATE HELPERS
    // =========================================================================

    private static string GetInitials(string fullName)
    {
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpperInvariant(),
            _ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
        };
    }

    /// <summary>Maps a pipeline stage type to the corresponding application lifecycle status.</summary>
    private static ApplicationStatus MapStageTypeToStatus(RecruitmentPipelineStageType stageType) =>
        stageType switch
        {
            RecruitmentPipelineStageType.ApplicationReview   => ApplicationStatus.UnderReview,
            RecruitmentPipelineStageType.Screening            => ApplicationStatus.UnderReview,
            RecruitmentPipelineStageType.HiringManagerReview  => ApplicationStatus.UnderReview,
            RecruitmentPipelineStageType.Assessment           => ApplicationStatus.AssessmentPending,
            RecruitmentPipelineStageType.Interview            => ApplicationStatus.InterviewScheduled,
            RecruitmentPipelineStageType.PreEmploymentCheck   => ApplicationStatus.PreEmploymentCheck,
            RecruitmentPipelineStageType.Offer                => ApplicationStatus.OfferExtended,
            RecruitmentPipelineStageType.Hired                => ApplicationStatus.Hired,
            _                                                 => ApplicationStatus.UnderReview
        };

    /// <summary>
    /// Decrements the vacancy counter for the stage being exited and increments the counter
    /// for the stage being entered. Only Interview, Offer, and Hire counters are managed here;
    /// ShortlistedCount is owned by the shortlisting workflow.
    /// </summary>
    private static void AdjustVacancyCounters(
        JobVacancy vacancy,
        RecruitmentPipelineStageType? exitingType,
        RecruitmentPipelineStageType enteringType)
    {
        // Decrement exiting stage
        if (exitingType.HasValue)
        {
            switch (exitingType.Value)
            {
                case RecruitmentPipelineStageType.Interview:
                    vacancy.InterviewCount = Math.Max(0, vacancy.InterviewCount - 1);
                    break;
                case RecruitmentPipelineStageType.Offer:
                    vacancy.OfferCount = Math.Max(0, vacancy.OfferCount - 1);
                    break;
                case RecruitmentPipelineStageType.Hired:
                    vacancy.HireCount = Math.Max(0, vacancy.HireCount - 1);
                    break;
            }
        }

        // Increment entering stage
        switch (enteringType)
        {
            case RecruitmentPipelineStageType.Interview:
                vacancy.InterviewCount++;
                break;
            case RecruitmentPipelineStageType.Offer:
                vacancy.OfferCount++;
                break;
            case RecruitmentPipelineStageType.Hired:
                vacancy.HireCount++;
                break;
        }
    }

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

    private async Task SendAssessmentPendingEmailAsync(
        string toEmail, string candidateName, string applicationNumber, string jobTitle, string stageName)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        var tokens = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CandidateName"]     = candidateName,
            ["JobTitle"]          = jobTitle,
            ["ApplicationNumber"] = applicationNumber,
            ["StageName"]         = stageName,
        };

        await _templatedEmail.SendAsync(
            RecruitmentEmailCatalog.Module, RecruitmentEmailCatalog.Events.AssessmentPending, toEmail, tokens);
    }

    private static PipelineStageWithApplicationsDto BuildEmptyColumn(RecruitmentPipelineStage stage) =>
        new()
        {
            StageId = stage.Id,
            StageName = stage.Name,
            Order = stage.Order,
            StageType = stage.StageType
        };
}
