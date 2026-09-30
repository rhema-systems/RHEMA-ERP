using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Appraisal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Aggregates all data required by the HR Cycle Dashboard from the repository layer.
/// Uses AsNoTracking queries with selective <c>Include</c> chains for performance.
/// </summary>
public class HRCycleDashboardQueryService : IHRCycleDashboardQueryService
{
    private readonly IGenericRepository<AppraisalCycle>          _cycleRepo;
    private readonly IGenericRepository<PerformanceAppraisal>    _appraisalRepo;
    private readonly IGenericRepository<AppraisalGradeDefinition> _gradeRepo;
    private readonly IGenericRepository<CalibrationSession>      _calibSessionRepo;
    private readonly IGenericRepository<AppraisalManualAdvanceLog> _advanceLogRepo;
    private readonly IGenericRepository<Employee>                _employeeRepo;
    private readonly IGenericRepository<AppraisalOutcomeRecommendation> _recommendationRepo;
    private readonly IGenericRepository<SalaryReviewProposal>    _salaryProposalRepo;
    private readonly IGenericRepository<EmploymentActionProposal> _actionProposalRepo;
    private readonly IGenericRepository<PerformanceImprovementPlan> _pipRepo;
    private readonly IAppraisalNotificationService               _notificationService;
    private readonly IAppraisalLifecycleService                  _lifecycle;
    private readonly ICurrentUserProvider                        _currentUserProvider;
    private readonly ILogger<HRCycleDashboardQueryService>       _logger;

    // Predefined color palette for grade distribution chart slices.
    private static readonly string[] GradeColors =
    [
        "#22c55e", "#3b82f6", "#f59e0b", "#ef4444", "#8b5cf6",
        "#06b6d4", "#ec4899", "#84cc16", "#f97316", "#6b7280",
    ];

    public HRCycleDashboardQueryService(
        IGenericRepository<AppraisalCycle>           cycleRepo,
        IGenericRepository<PerformanceAppraisal>     appraisalRepo,
        IGenericRepository<AppraisalGradeDefinition> gradeRepo,
        IGenericRepository<CalibrationSession>       calibSessionRepo,
        IGenericRepository<AppraisalManualAdvanceLog> advanceLogRepo,
        IGenericRepository<Employee>                 employeeRepo,
        IGenericRepository<AppraisalOutcomeRecommendation> recommendationRepo,
        IGenericRepository<SalaryReviewProposal>     salaryProposalRepo,
        IGenericRepository<EmploymentActionProposal> actionProposalRepo,
        IGenericRepository<PerformanceImprovementPlan> pipRepo,
        IAppraisalNotificationService                notificationService,
        IAppraisalLifecycleService                   lifecycle,
        ICurrentUserProvider                         currentUserProvider,
        ILogger<HRCycleDashboardQueryService>        logger)
    {
        _cycleRepo        = cycleRepo;
        _appraisalRepo    = appraisalRepo;
        _gradeRepo        = gradeRepo;
        _calibSessionRepo = calibSessionRepo;
        _advanceLogRepo   = advanceLogRepo;
        _employeeRepo     = employeeRepo;
        _recommendationRepo = recommendationRepo;
        _salaryProposalRepo = salaryProposalRepo;
        _actionProposalRepo = actionProposalRepo;
        _pipRepo            = pipRepo;
        _notificationService = notificationService;
        _lifecycle           = lifecycle;
        _currentUserProvider = currentUserProvider;
        _logger           = logger;
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

    // ── GetActiveCycleIdAsync ─────────────────────────────────────────────────

    public async Task<Guid> GetActiveCycleIdAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var cycle = await _cycleRepo
            .GetQueryable(c =>
                c.TenantId == tenantId &&
                !c.IsDeleted &&
                c.Status == AppraisalCycleStatus.Open)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);

        return cycle ?? Guid.Empty;
    }

    // ── BuildDashboardAsync ───────────────────────────────────────────────────

    public async Task<HRCycleDashboardDto?> BuildDashboardAsync(
        Guid cycleId, CancellationToken ct = default)
    {
        try
        {
            var tenantId = GetTenantId();

            // 1. Load cycle with its settings ──────────────────────────────────
            var cycle = await _cycleRepo
                .GetQueryable(c => c.Id == cycleId && c.TenantId == tenantId && !c.IsDeleted)
                .Include(c => c.AppraisalSettings)
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);

            if (cycle is null) return null;
            var settings = cycle.AppraisalSettings!;

            // 2. Load all appraisals for this cycle with selected navigations ──
            var appraisals = await _appraisalRepo
                .GetQueryable(a => a.AppraisalCycleId == cycleId && a.TenantId == tenantId && !a.IsDeleted)
                .Include(a => a.Employee)
                    .ThenInclude(e => e!.OrganizationUnit)
                        .ThenInclude(u => u!.HeadEmployee)
                .Include(a => a.Employee)
                    .ThenInclude(e => e!.Position)
                .Include(a => a.EvaluatorEvaluations)
                .Include(a => a.PeerNominations)
                .Include(a => a.Conversations)
                .Include(a => a.HRReviews)
                .Include(a => a.OverallGrade)
                .Include(a => a.Appeals)
                .AsNoTracking()
                .ToListAsync(ct);

            // Where each appraisal is — the gates' answer, the one the write paths and the phase
            // endpoint give (B1). It resolved here from this load, whose goals were only those
            // linked to the appraisal and whose advance log was never read.
            var gates = await _lifecycle.GetStatesAsync(appraisals.Select(a => a.Id).ToList(), ct);
            var subs = gates.ToDictionary(g => g.Key, g => g.Value.SubStatus);

            // 3. Load grade definitions ─────────────────────────────────────────
            var allGrades = (await _gradeRepo.GetAllAsync())
                .Where(g => g.TenantId == tenantId && !g.IsDeleted)
                .ToList();

            // 4. Load calibration sessions for this cycle ────────────────────────
            var calibSessions = await _calibSessionRepo
                .GetQueryable(cs => cs.AppraisalCycleId == cycleId && cs.TenantId == tenantId && !cs.IsDeleted)
                .AsNoTracking()
                .ToListAsync(ct);

            // 5. Load managers (for attention items & department breakdown) ───────
            var managerIds = appraisals
                .Select(a => a.Employee?.ManagerId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var managerLookup = new Dictionary<Guid, string>();
            if (managerIds.Count > 0)
            {
                var managers = await _employeeRepo
                    .GetQueryable(e => e.TenantId == tenantId && managerIds.Contains(e.Id) && !e.IsDeleted)
                    .AsNoTracking()
                    .ToListAsync(ct);
                managerLookup = managers.ToDictionary(m => m.Id, m => m.FullName);
            }

            // 6. Position titles come from Employee.Position on the query above. They used to be
            // read here by a query whose result was discarded — which is why every attention item
            // came back with an empty Position — and which had no tenant predicate.

            // 7. Load recent manual-advance logs for the activity feed ────────────
            var appraisalIds = appraisals.Select(a => a.Id).ToHashSet();
            var advanceLogs = await _advanceLogRepo
                .GetQueryable(l => l.TenantId == tenantId && !l.IsDeleted && appraisalIds.Contains(l.PerformanceAppraisalId))
                .Include(l => l.AdvancedBy)
                .Include(l => l.Appraisal)
                    .ThenInclude(a => a!.Employee)
                .OrderByDescending(l => l.AdvancedDate)
                .Take(40)
                .AsNoTracking()
                .ToListAsync(ct);

            // ── Build the response ─────────────────────────────────────────────
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var dto = new HRCycleDashboardDto
            {
                CycleId         = cycle.Id,
                CycleName       = cycle.CycleName,
                Year            = cycle.Year,
                CycleStatus     = cycle.Status,
                StartDate       = cycle.StartDate,
                EndDate         = cycle.EndDate,

                HasPeerReviews  = settings.RequirePeerReviews,
                HasCalibration  = settings.RequireCalibration,
                HasHRReview     = settings.RequireHRReview,
                HasAppeals      = settings.EnableAppeals,
                AutoLockEnabled = settings.AutoLockOnDeadline,
                MinPeers        = settings.MinPeerEvaluators,
                MaxPeers        = settings.MaxPeerEvaluators,
                AppealWindowDays = settings.AppealWindowDays,

                SelfWeight    = settings.SelfEvaluationWeight,
                PeerWeight    = settings.PeerEvaluationWeight,
                ManagerWeight = settings.ManagerEvaluationWeight,

                PipelineProgress   = BuildPipelineProgress(appraisals, subs, settings),
                Deadlines          = BuildDeadlines(cycle, settings, appraisals, subs, today),
                GradeDistribution  = BuildGradeDistribution(appraisals, allGrades,
                                        out int scoredCount, out decimal? avgScore),
                ScoredAppraisalCount = scoredCount,
                AverageScore         = avgScore,
                Recommendations    = BuildRecommendations(appraisals),
                OutcomePipeline    = await BuildOutcomePipelineAsync(tenantId, appraisalIds, ct),
                DepartmentBreakdown = BuildDepartmentBreakdown(appraisals, calibSessions,
                                         managerLookup, cycle.GoalSettingDeadline, today),
                AttentionItems     = BuildAttentionItems(appraisals, subs, managerLookup, allGrades, cycle, today),
                RecentActivity     = BuildActivity(appraisals, advanceLogs),
            };

            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build HR cycle dashboard for cycle {CycleId}", cycleId);
            throw;
        }
    }

    // ── NudgeAsync ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<HRCycleNudgeResultDto> NudgeAsync(
        Guid cycleId, Guid appraisalId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();

        var cycle = await _cycleRepo
            .GetQueryable(c => c.Id == cycleId && c.TenantId == tenantId && !c.IsDeleted)
            .Include(c => c.AppraisalSettings)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (cycle is null)
            throw new ArgumentException($"Appraisal cycle '{cycleId}' was not found.");

        var appraisal = await _appraisalRepo
            .GetQueryable(a => a.Id == appraisalId && a.AppraisalCycleId == cycleId
                               && a.TenantId == tenantId && !a.IsDeleted)
            .Include(a => a.Employee)
            .Include(a => a.EvaluatorEvaluations)
                .ThenInclude(e => e.Evaluator)
            .Include(a => a.PeerNominations)
                .ThenInclude(n => n.PeerEmployee)
            .Include(a => a.Conversations)
            .Include(a => a.HRReviews)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (appraisal is null)
            throw new ArgumentException($"Appraisal '{appraisalId}' was not found in this cycle.");

        var subStatus = (await _lifecycle.GetStateAsync(appraisal.Id, ct)).SubStatus;
        var stepName  = AppraisalGates.Label(subStatus);

        // Who can actually clear this step, and where they go to do it.
        var recipients = new List<(Guid EmployeeId, string Name)>();
        string navigationUrl;

        switch (subStatus)
        {
            case AppraisalSubStatus.GoalSetting:
                AddAppraisee();
                // Area 25 slice 5: the appraisee's surfaces live in the self-service portal.
                navigationUrl = "/me/performance/goals";
                break;

            case AppraisalSubStatus.PeerNomination:
                AddAppraisee();
                navigationUrl = $"/me/performance/appraisals/{appraisal.Id}";
                break;

            case AppraisalSubStatus.SelfEvaluation:
                AddAppraisee();
                navigationUrl = $"/me/performance/appraisals/{appraisal.Id}/self-evaluation";
                break;

            case AppraisalSubStatus.PeerEvaluation:
                // The peers with a form still open. Where the evaluation records have not been
                // created yet, the approved nominees are the ones who owe it.
                var outstanding = appraisal.EvaluatorEvaluations
                    .Where(e => e.EvaluatorRole == EvaluatorRole.Peer && e.SubmittedDate == null)
                    .Select(e => (e.EvaluatorId, e.Evaluator?.FullName ?? "Peer"))
                    .ToList();

                if (outstanding.Count == 0)
                {
                    outstanding = appraisal.PeerNominations
                        .Where(n => n.NominationStatus == PeerNominationStatus.Approved)
                        .Select(n => (n.PeerEmployeeId, n.PeerEmployee?.FullName ?? "Peer"))
                        .ToList();
                }

                recipients.AddRange(outstanding);
                navigationUrl = "/me/performance/peer-reviews";
                break;

            case AppraisalSubStatus.ManagerEvaluation:
            case AppraisalSubStatus.PendingConversation:
                var managerId = appraisal.Employee?.ManagerId
                                ?? appraisal.EvaluatorEvaluations
                                    .FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Manager)?.EvaluatorId;

                if (managerId is null || managerId == Guid.Empty)
                    throw new InvalidOperationException(
                        $"{appraisal.Employee?.FullName ?? "This employee"} has no manager on record, so there is nobody to nudge about the {stepName.ToLowerInvariant()}.");

                var manager = await _employeeRepo
                    .GetQueryable(e => e.Id == managerId.Value && e.TenantId == tenantId && !e.IsDeleted)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ct);

                recipients.Add((managerId.Value, manager?.FullName ?? "Manager"));
                navigationUrl = subStatus == AppraisalSubStatus.ManagerEvaluation
                    ? $"/hr/performance/team-appraisals/{appraisal.Id}"
                    : "/hr/performance/conversations";
                break;

            case AppraisalSubStatus.PendingAcknowledgment:
                AddAppraisee();
                navigationUrl = $"/me/performance/appraisals/{appraisal.Id}";
                break;

            default:
                // Calibration, HR review, appeals and the terminal states are HR's own work or
                // are finished. Nudging would either notify nobody or chase the sender.
                throw new InvalidOperationException(
                    $"This appraisal is at '{stepName}', which is not a step an individual can be nudged about.");
        }

        recipients = recipients
            .Where(r => r.EmployeeId != Guid.Empty)
            .GroupBy(r => r.EmployeeId)
            .Select(g => g.First())
            .ToList();

        if (recipients.Count == 0)
            throw new InvalidOperationException(
                $"Nobody is currently assigned to the {stepName.ToLowerInvariant()} on this appraisal.");

        var subjectName = appraisal.Employee?.FullName ?? "an employee";
        var requests = recipients.Select(r => new AppraisalNotificationRequest(
            RecipientEmployeeId: r.EmployeeId,
            Type: AppraisalNotificationType.ActionRequired,
            Title: $"{stepName} outstanding — {cycle.CycleCode}",
            Message: r.EmployeeId == appraisal.EmployeeId
                ? $"Your {stepName.ToLowerInvariant()} for {cycle.CycleName} is still outstanding."
                : $"The {stepName.ToLowerInvariant()} for {subjectName} in {cycle.CycleName} is still outstanding.",
            CycleName: cycle.CycleName,
            NavigationUrl: navigationUrl,
            AppraisalId: appraisal.Id,
            SubjectEmployeeName: subjectName,
            Urgency: NotificationUrgency.Warning));

        var raised = await _notificationService.RaiseAsync(requests, ct);

        _logger.LogInformation(
            "Nudged appraisal {AppraisalId} at step {Step}: {Raised} of {Recipients} notification(s) written",
            appraisalId, subStatus, raised, recipients.Count);

        return new HRCycleNudgeResultDto
        {
            AppraisalId         = appraisal.Id,
            EmployeeName        = subjectName,
            StepName            = stepName,
            Recipients          = recipients.Select(r => r.Name).ToList(),
            NotificationsRaised = raised,
        };

        void AddAppraisee() =>
            recipients.Add((appraisal.EmployeeId, appraisal.Employee?.FullName ?? "Employee"));
    }

    // ── Section builders ──────────────────────────────────────────────────────

    /// <summary>Past <paramref name="step"/> in the pipeline — a withdrawn appraisal is past nothing.</summary>
    private static bool IsPast(IReadOnlyDictionary<Guid, AppraisalSubStatus> subs, PerformanceAppraisal a, AppraisalSubStatus step)
        => subs.TryGetValue(a.Id, out var sub) && sub != AppraisalSubStatus.Withdrawn && (int)sub > (int)step;

    private static HRCyclePipelineProgressDto BuildPipelineProgress(
        List<PerformanceAppraisal> appraisals,
        IReadOnlyDictionary<Guid, AppraisalSubStatus> subs,
        AppraisalSettings settings)
    {
        var p = new HRCyclePipelineProgressDto { TotalAppraisals = appraisals.Count };

        foreach (var a in appraisals)
        {
            if (!subs.TryGetValue(a.Id, out var sub)) continue;
            switch (sub)
            {
                case AppraisalSubStatus.GoalSetting:
                    p.GoalSettingCount++;
                    break;

                case AppraisalSubStatus.PeerNomination:
                    p.PeerNominationCount++;
                    break;

                case AppraisalSubStatus.SelfEvaluation:
                    p.SelfEvalPendingCount++;
                    break;

                case AppraisalSubStatus.PeerEvaluation:
                    p.PeerEvalPendingCount++;
                    break;

                case AppraisalSubStatus.ManagerEvaluation:
                    p.ManagerEvalPendingCount++;
                    break;

                case AppraisalSubStatus.PendingCalibration:
                case AppraisalSubStatus.CalibrationInProgress:
                    p.CalibrationPendingCount++;
                    break;

                case AppraisalSubStatus.PendingHRReview:
                case AppraisalSubStatus.HRReviewInProgress:
                    p.HRReviewPendingCount++;
                    break;

                case AppraisalSubStatus.PendingConversation:
                case AppraisalSubStatus.PendingAcknowledgment:
                    p.AcknowledgmentPendingCount++;
                    break;

                case AppraisalSubStatus.AppealSubmitted:
                case AppraisalSubStatus.AppealUnderReview:
                case AppraisalSubStatus.AppealResolved:
                    p.AppealCount++;
                    break;

                case AppraisalSubStatus.Completed:
                    p.CompletedCount++;
                    break;

                case AppraisalSubStatus.Closed:
                    p.ClosedCount++;
                    break;
            }
        }

        // Self-eval and peer-eval completed counts = those who are PAST those stages. The evaluation
        // steps precede governance in either HR-review timing, so the enum's order is the pipeline's.
        p.SelfEvalCompletedCount = appraisals.Count(a => IsPast(subs, a, AppraisalSubStatus.SelfEvaluation));

        p.PeerEvalCompletedCount = settings.RequirePeerReviews
            ? appraisals.Count(a => IsPast(subs, a, AppraisalSubStatus.PeerEvaluation))
            : 0;

        p.ManagerEvalCompletedCount = appraisals.Count(a => IsPast(subs, a, AppraisalSubStatus.ManagerEvaluation));

        p.CalibrationCompletedCount = settings.RequireCalibration
            ? appraisals.Count(a => a.IsCalibrated)
            : 0;

        p.HRReviewCompletedCount = settings.RequireHRReview
            ? appraisals.Count(a =>
                a.HRReviews.Any(r => r.ReviewCompletedDate != null && r.IsApproved))
            : 0;

        p.AcknowledgmentCompletedCount = appraisals.Count(a =>
            a.Status is AppraisalStatus.Completed or AppraisalStatus.Closed
            || a.EmployeeAcknowledged);

        return p;
    }

    private static List<HRCycleDeadlineDto> BuildDeadlines(
        AppraisalCycle cycle,
        AppraisalSettings settings,
        List<PerformanceAppraisal> appraisals,
        IReadOnlyDictionary<Guid, AppraisalSubStatus> subs,
        DateOnly today)
    {
        var deadlines = new List<(string Name, DateOnly? Deadline, bool Enabled)>
        {
            ("Goal Setting",    cycle.GoalSettingDeadline,         settings.RequireGoalSetting),
            ("Peer Nomination", cycle.PeerNominationDeadline,      settings.RequirePeerReviews),
            ("Self-Evaluation", cycle.SelfEvaluationDeadline,      settings.RequireSelfEvaluation),
            ("Peer Evaluation", cycle.PeerEvaluationDeadline,      settings.RequirePeerReviews),
            ("Manager Evaluation", cycle.ManagerEvaluationDeadline, settings.RequireManagerEvaluation),
            ("Calibration",     cycle.CalibrationDeadline,         settings.RequireCalibration),
            ("HR Review",       cycle.HRReviewDeadline,            settings.RequireHRReview),
            ("Acknowledgment",  cycle.EmployeeAcknowledgeDeadline, settings.RequireEmployeeAcknowledgment),
            ("Final Conversation", cycle.FinalConversationDeadline, settings.RequireFinalConversation),
        };

        var result = new List<HRCycleDeadlineDto>();
        foreach (var (name, deadline, enabled) in deadlines)
        {
            if (!enabled) continue;

            var state  = ComputeDeadlineState(deadline, today);
            int? days  = deadline.HasValue ? deadline.Value.DayNumber - today.DayNumber : null;

            // Count affected/completed appraisals per step
            int affected  = appraisals.Count;
            int completed = name switch
            {
                // Past the goal-setting step by the gates: it counted appraisals with one approved
                // goal linked to them, whatever the minimum and the setup conversations required.
                "Goal Setting"       => appraisals.Count(a => IsPast(subs, a, AppraisalSubStatus.GoalSetting)),
                "Peer Nomination"    => appraisals.Count(a => a.PeerNominations.Any(n => n.NominationStatus == PeerNominationStatus.Approved)),
                "Self-Evaluation"    => appraisals.Count(a => a.EvaluatorEvaluations.Any(e => e.EvaluatorRole == EvaluatorRole.Self && e.SubmittedDate != null)),
                "Peer Evaluation"    => appraisals.Count(a => a.EvaluatorEvaluations.Count(e => e.EvaluatorRole == EvaluatorRole.Peer && e.SubmittedDate != null) >= settings.MinPeerEvaluators),
                "Manager Evaluation" => appraisals.Count(a => a.EvaluatorEvaluations.Any(e => e.EvaluatorRole == EvaluatorRole.Manager && e.SubmittedDate != null)),
                "Calibration"        => appraisals.Count(a => a.IsCalibrated),
                "HR Review"          => appraisals.Count(a => a.HRReviews.Any(r => r.ReviewCompletedDate != null && r.IsApproved)),
                "Acknowledgment"     => appraisals.Count(a => a.EmployeeAcknowledged),
                "Final Conversation" => appraisals.Count(a => a.Conversations.Any(c => c.Type == ConversationType.FinalReview && c.IsCompleted)),
                _                    => 0,
            };

            bool isActive = deadline.HasValue && state != DeadlineState.Passed;

            result.Add(new HRCycleDeadlineDto
            {
                StepName            = name,
                Deadline            = deadline,
                State               = state,
                DaysRemaining       = days,
                AppraisalsAffected  = affected,
                AppraisalsCompleted = completed,
                IsActive            = isActive,
            });
        }

        return result;
    }

    /// <summary>
    /// The grade histogram, plus the two headline figures that go with it.
    ///
    /// <para>⚠ Scored and graded are not the same population. An appraisal carries a grade only
    /// when its score falls inside a configured grade range, so a tenant with no grade definitions
    /// — or a score outside every band — is scored but ungraded. This used to key both figures off
    /// the grade, which blanked the average score and the scored count on exactly those cycles.
    /// The counters now follow the score; the histogram percentages stay over the graded
    /// population so the slices still total 100%.</para>
    /// </summary>
    private static List<HRCycleGradeDistributionItemDto> BuildGradeDistribution(
        List<PerformanceAppraisal>   appraisals,
        List<AppraisalGradeDefinition> grades,
        out int scoredCount,
        out decimal? average)
    {
        var withScores = appraisals.Where(a => a.OverallScore.HasValue).ToList();
        scoredCount = withScores.Count;
        average = withScores.Count > 0
            ? Math.Round(withScores.Average(a => a.OverallScore!.Value), 2)
            : null;

        var byGrade = appraisals
            .Where(a => a.OverallGradeDefinitionId.HasValue)
            .GroupBy(a => a.OverallGradeDefinitionId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        int totalGraded = byGrade.Values.Sum();

        return grades.Select((g, i) =>
        {
            int cnt = byGrade.GetValueOrDefault(g.Id, 0);
            return new HRCycleGradeDistributionItemDto
            {
                GradeDefinitionId = g.Id,
                GradeName         = g.GradeName,
                Color             = GradeColors[i % GradeColors.Length],
                Count             = cnt,
                Percentage        = totalGraded > 0
                                    ? Math.Round((decimal)cnt / totalGraded * 100m, 1)
                                    : 0m,
            };
        }).ToList();
    }

    private static HRCycleRecommendationSummaryDto BuildRecommendations(
        List<PerformanceAppraisal> appraisals)
    {
        return new HRCycleRecommendationSummaryDto
        {
            AwardCount       = appraisals.Count(a => a.RecommendAward),
            PromotionCount   = appraisals.Count(a => a.RecommendPromotion),
            IncrementCount   = appraisals.Count(a => a.RecommendIncrement),
            TrainingCount    = appraisals.Count(a => a.RecommendTraining),
            PIPCount         = appraisals.Count(a => a.RecommendPIP),
            TerminationCount = appraisals.Count(a => a.RecommendTermination),
        };
    }

    /// <summary>
    /// What the cycle's recommendations turned into, and how much of it is still waiting on an
    /// approver.
    ///
    /// <para>The three downstream streams are found through their appraisal provenance
    /// (<c>SourceAppraisalId</c> / <c>AppraisalId</c>), so a proposal or plan raised by hand
    /// against an appraisal in this cycle is counted alongside the ones HR dispatched — which is
    /// right: the question the card answers is what is outstanding for this cycle, not who
    /// created it.</para>
    /// </summary>
    private async Task<HRCycleOutcomePipelineDto> BuildOutcomePipelineAsync(
        Guid tenantId, HashSet<Guid> appraisalIds, CancellationToken ct)
    {
        var dto = new HRCycleOutcomePipelineDto();
        if (appraisalIds.Count == 0) return dto;

        var ids = appraisalIds.ToList();

        // AsNoTracking goes before the projection: these Selects land on an enum, and the
        // extension is constrained to a reference type.
        var recommendations = await _recommendationRepo
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted && ids.Contains(r.PerformanceAppraisalId))
            .AsNoTracking()
            .Select(r => new { r.Status, r.RecommendationType })
            .ToListAsync(ct);

        var salaryProposals = await _salaryProposalRepo
            .GetQueryable(p => p.TenantId == tenantId && !p.IsDeleted
                               && p.SourceAppraisalId != null && ids.Contains(p.SourceAppraisalId!.Value))
            .AsNoTracking()
            .Select(p => p.Status)
            .ToListAsync(ct);

        var actionProposals = await _actionProposalRepo
            .GetQueryable(p => p.TenantId == tenantId && !p.IsDeleted
                               && p.SourceAppraisalId != null && ids.Contains(p.SourceAppraisalId!.Value))
            .AsNoTracking()
            .Select(p => p.Status)
            .ToListAsync(ct);

        var plans = await _pipRepo
            .GetQueryable(p => p.TenantId == tenantId && !p.IsDeleted
                               && p.AppraisalId != null && ids.Contains(p.AppraisalId!.Value))
            .AsNoTracking()
            .Select(p => p.Status)
            .ToListAsync(ct);

        dto.Recommendations = CountByEnum(
            recommendations.Select(r => r.Status),
            _ => false);

        dto.RecommendationTypes = CountByEnum(
            recommendations.Select(r => r.RecommendationType),
            _ => false,
            includeEmpty: false);

        dto.SalaryProposals = CountByEnum(
            salaryProposals, s => s == SalaryReviewProposalStatus.PendingApproval);

        dto.EmploymentActionProposals = CountByEnum(
            actionProposals, s => s == EmploymentActionProposalStatus.PendingApproval);

        dto.ImprovementPlans = CountByEnum(
            plans, s => s == PipStatus.PendingApproval);

        dto.TotalRaised = recommendations.Count + salaryProposals.Count + actionProposals.Count + plans.Count;
        dto.AwaitingApproval =
            dto.SalaryProposals.Where(b => b.AwaitingApproval).Sum(b => b.Count)
            + dto.EmploymentActionProposals.Where(b => b.AwaitingApproval).Sum(b => b.Count)
            + dto.ImprovementPlans.Where(b => b.AwaitingApproval).Sum(b => b.Count);

        return dto;
    }

    /// <summary>
    /// Buckets a set of enum values into one count per member, in declaration order. Members with
    /// no rows are kept by default so a stream reads as a stable, comparable row of counters.
    /// </summary>
    private static List<HRCycleOutcomeCountDto> CountByEnum<TEnum>(
        IEnumerable<TEnum> values,
        Func<TEnum, bool> awaitingApproval,
        bool includeEmpty = true)
        where TEnum : struct, Enum
    {
        var counts = values
            .GroupBy(v => v)
            .ToDictionary(g => g.Key, g => g.Count());

        return Enum.GetValues<TEnum>()
            .Select(member => new HRCycleOutcomeCountDto
            {
                Key              = member.ToString(),
                Label            = Humanize(member.ToString()),
                Count            = counts.GetValueOrDefault(member, 0),
                AwaitingApproval = awaitingApproval(member),
            })
            .Where(b => includeEmpty || b.Count > 0)
            .ToList();
    }

    private static string Humanize(string pascal) =>
        System.Text.RegularExpressions.Regex.Replace(pascal, "(\\B[A-Z])", " $1");

    private static List<HRCycleDepartmentProgressDto> BuildDepartmentBreakdown(
        List<PerformanceAppraisal> appraisals,
        List<CalibrationSession>   sessions,
        Dictionary<Guid, string>   managerLookup,
        DateOnly?                  goalDeadline,
        DateOnly                   today)
    {
        var groups = appraisals
            .Where(a => a.Employee?.OrganizationUnitId.HasValue == true)
            .GroupBy(a => a.Employee!.OrganizationUnitId!.Value);

        var result = new List<HRCycleDepartmentProgressDto>();
        foreach (var grp in groups)
        {
            var unitId    = grp.Key;
            var deptName  = grp.First().Employee?.OrganizationUnit?.Name ?? "Unknown";
            var unitAppraisals = grp.ToList();

            var session        = sessions.FirstOrDefault(s => s.OrganizationUnitId == unitId);
            int completedSteps = unitAppraisals.Count(a =>
                a.Status is AppraisalStatus.Completed or AppraisalStatus.Closed
                || a.EmployeeAcknowledged);

            decimal completionPct = unitAppraisals.Count > 0
                ? Math.Round((decimal)completedSteps / unitAppraisals.Count * 100m, 1)
                : 0m;

            int overdue = goalDeadline.HasValue && today > goalDeadline.Value
                ? unitAppraisals.Count(a => !(a.EmployeeAcknowledged
                    || a.Status is AppraisalStatus.Completed or AppraisalStatus.Closed))
                : 0;

            var scored   = unitAppraisals.Where(a => a.OverallScore.HasValue).ToList();
            decimal? avg = scored.Count > 0 ? Math.Round(scored.Average(a => a.OverallScore!.Value), 2) : null;

            // Who to chase about this unit: its configured head if it has one. Falling back to
            // the first appraisee's line manager is a guess — right in a unit that reports to one
            // person, wrong in a mixed one — so it is only used when the unit has no head on file.
            var head        = grp.First().Employee?.OrganizationUnit?.HeadEmployee;
            var fallbackId  = grp.First().Employee?.ManagerId;
            var managerName = head?.FullName
                              ?? (fallbackId.HasValue && managerLookup.TryGetValue(fallbackId.Value, out var mn)
                                  ? mn : null);

            result.Add(new HRCycleDepartmentProgressDto
            {
                OrganizationUnitId       = unitId,
                DepartmentName           = deptName,
                ManagerName              = managerName,
                TotalAppraisals          = unitAppraisals.Count,
                CompletedSteps           = completedSteps,
                CompletionPercent        = completionPct,
                OverdueCount             = overdue,
                AverageScore             = avg,
                HasCalibrationSession    = session != null,
                CalibrationSessionStatus = session?.Status,
            });
        }

        return result.OrderByDescending(d => d.TotalAppraisals).ToList();
    }

    private static List<HRCycleAttentionItemDto> BuildAttentionItems(
        List<PerformanceAppraisal> appraisals,
        IReadOnlyDictionary<Guid, AppraisalSubStatus> subs,
        Dictionary<Guid, string>   managerLookup,
        List<AppraisalGradeDefinition> grades,
        AppraisalCycle             cycle,
        DateOnly                   today)
    {
        var items    = new List<HRCycleAttentionItemDto>();
        var gradeMap = grades.ToDictionary(g => g.Id, g => g.GradeName);

        foreach (var a in appraisals)
        {
            if (a.Employee is null) continue;
            if (!subs.TryGetValue(a.Id, out var subStatus)) continue;

            var deptName    = a.Employee.OrganizationUnit?.Name ?? string.Empty;
            var position    = a.Employee.Position?.Title ?? string.Empty;
            var managerId   = a.Employee.ManagerId;
            var managerName = managerId.HasValue && managerLookup.TryGetValue(managerId.Value, out var mn) ? mn : string.Empty;
            var gradeLabel  = a.OverallGradeDefinitionId.HasValue && gradeMap.TryGetValue(a.OverallGradeDefinitionId.Value, out var gl) ? gl : null;

            // PIP recommendation
            if (a.RecommendPIP)
            {
                items.Add(BuildItem(a, deptName, position, managerName, gradeLabel, subStatus,
                    AttentionReason.PIPrecommendation, "PIP Recommended",
                    "Employee has been recommended for a Performance Improvement Plan.",
                    AttentionSeverity.Critical));
            }

            // Termination recommendation
            if (a.RecommendTermination)
            {
                items.Add(BuildItem(a, deptName, position, managerName, gradeLabel, subStatus,
                    AttentionReason.TerminationRecommendation, "Termination Recommended",
                    "Employee has been recommended for termination.",
                    AttentionSeverity.Critical));
            }

            // Active appeal
            if (a.HasAppeal && a.Status == AppraisalStatus.Appealed)
            {
                bool appealOverdue = a.AppealRemandDeadline.HasValue
                    && DateTime.UtcNow > a.AppealRemandDeadline.Value;

                if (appealOverdue)
                {
                    items.Add(BuildItem(a, deptName, position, managerName, gradeLabel, subStatus,
                        AttentionReason.AppealOverdue, "Appeal Overdue",
                        "The appeal re-evaluation deadline has passed without resolution.",
                        AttentionSeverity.Critical));
                }
                else
                {
                    items.Add(BuildItem(a, deptName, position, managerName, gradeLabel, subStatus,
                        AttentionReason.AppealFiled, "Appeal Filed",
                        $"Appeal status: {a.CurrentAppealStatus}.",
                        AttentionSeverity.Warning));
                }
            }

            // Overdue at current step
            var stepDeadline = GetStepDeadline(subStatus, cycle);
            if (stepDeadline.HasValue && today > stepDeadline.Value)
            {
                int days = today.DayNumber - stepDeadline.Value.DayNumber;
                items.Add(new HRCycleAttentionItemDto
                {
                    AppraisalId      = a.Id,
                    EmployeeName     = a.Employee.FullName,
                    Position         = position,
                    Department       = deptName,
                    ManagerName      = managerName,
                    Reason           = AttentionReason.OverdueAtStep,
                    ReasonLabel      = $"Overdue: {AppraisalGates.Label(subStatus)}",
                    Detail           = $"{days} day{(days == 1 ? "" : "s")} past deadline.",
                    Severity         = days > 7 ? AttentionSeverity.Critical : AttentionSeverity.Warning,
                    OverallScore     = a.OverallScore,
                    GradeLabel       = gradeLabel,
                    DaysOverdue      = days,
                    CurrentSubStatus = subStatus,
                });
            }

            // Low score (< 40)
            if (a.OverallScore.HasValue && a.OverallScore.Value < 40m
                && a.Status is not (AppraisalStatus.Appealed or AppraisalStatus.Draft))
            {
                items.Add(BuildItem(a, deptName, position, managerName, gradeLabel, subStatus,
                    AttentionReason.LowScore, "Low Score",
                    $"Overall score {a.OverallScore:F1} is below the 40-point threshold.",
                    AttentionSeverity.Warning));
            }
        }

        // Sort: Critical first, then by DaysOverdue descending
        return items
            .OrderByDescending(i => i.Severity)
            .ThenByDescending(i => i.DaysOverdue)
            .Take(50)
            .ToList();
    }

    private static List<HRCycleActivityItemDto> BuildActivity(
        List<PerformanceAppraisal>       appraisals,
        List<AppraisalManualAdvanceLog>  advanceLogs)
    {
        var items = new List<HRCycleActivityItemDto>();

        // Source 1: manual-advance log entries
        foreach (var log in advanceLogs)
        {
            var employeeName = log.Appraisal?.Employee?.FullName;
            var actorName    = log.AdvancedBy?.FullName;
            var eventType    = MapSubStatusToActivityType(log.ToSubStatus);

            items.Add(new HRCycleActivityItemDto
            {
                Timestamp           = log.AdvancedDate,
                Description         = $"Pipeline advanced: {log.FromSubStatus} → {log.ToSubStatus}. {log.Reason.Trim()}",
                SubjectEmployeeName = employeeName,
                ActorName           = actorName,
                EventType           = eventType,
                AppraisalId         = log.PerformanceAppraisalId,
            });
        }

        // Source 2: recent acknowledgments
        var acknowledged = appraisals
            .Where(a => a.EmployeeAcknowledged && a.EmployeeAcknowledgedDate.HasValue)
            .OrderByDescending(a => a.EmployeeAcknowledgedDate)
            .Take(15);

        foreach (var a in acknowledged)
        {
            items.Add(new HRCycleActivityItemDto
            {
                Timestamp           = a.EmployeeAcknowledgedDate!.Value,
                Description         = "Employee acknowledged their appraisal.",
                SubjectEmployeeName = a.Employee?.FullName,
                EventType           = AppraisalNotificationType.EmployeeAcknowledged,
                AppraisalId         = a.Id,
            });
        }

        // Source 3: appeals — filed, and resolved.
        //
        // ⚠ These used to be timestamped from the appraisal's own UpdatedAt, so any later edit
        // moved the appeal to the top of the feed and a filing that was never resolved was
        // reported at the wrong time. The appeal itself records both dates; use them, and emit
        // the two events separately so the feed shows the filing and the verdict, not one row
        // that changes its own meaning.
        foreach (var a in appraisals)
        {
            foreach (var appeal in a.Appeals.Where(ap => !ap.IsDeleted).OrderByDescending(ap => ap.SubmittedDate).Take(10))
            {
                items.Add(new HRCycleActivityItemDto
                {
                    Timestamp           = appeal.SubmittedDate,
                    Description         = "Appeal filed against the appraisal outcome.",
                    SubjectEmployeeName = a.Employee?.FullName,
                    EventType           = AppraisalNotificationType.AppealSubmitted,
                    AppraisalId         = a.Id,
                });

                if (appeal.ResolvedDate.HasValue)
                {
                    items.Add(new HRCycleActivityItemDto
                    {
                        Timestamp           = appeal.ResolvedDate.Value,
                        Description         = $"Appeal resolved — {Humanize(appeal.Status.ToString()).ToLowerInvariant()}.",
                        SubjectEmployeeName = a.Employee?.FullName,
                        EventType           = AppraisalNotificationType.AppealResolved,
                        AppraisalId         = a.Id,
                    });
                }
            }
        }

        var now = DateTime.UtcNow;
        return items
            .OrderByDescending(i => i.Timestamp)
            .Take(30)
            .Select(i => { i.TimeAgo = DescribeAge(now, i.Timestamp); return i; })
            .ToList();
    }

    /// <summary>
    /// How long ago an activity item happened, in words. Filled server-side because the DTO
    /// carries the field and nothing was setting it, so every feed row rendered a blank.
    /// </summary>
    private static string DescribeAge(DateTime now, DateTime then)
    {
        var span = now - then;
        if (span < TimeSpan.Zero)          return "just now";
        if (span.TotalMinutes < 1)         return "just now";
        if (span.TotalMinutes < 60)        return $"{(int)span.TotalMinutes} min ago";
        if (span.TotalHours   < 24)        return $"{(int)span.TotalHours} hour{((int)span.TotalHours == 1 ? "" : "s")} ago";
        if (span.TotalDays    < 31)        return $"{(int)span.TotalDays} day{((int)span.TotalDays == 1 ? "" : "s")} ago";
        if (span.TotalDays    < 365)       return $"{(int)(span.TotalDays / 30)} month{((int)(span.TotalDays / 30) == 1 ? "" : "s")} ago";
        return $"{(int)(span.TotalDays / 365)} year{((int)(span.TotalDays / 365) == 1 ? "" : "s")} ago";
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static HRCycleAttentionItemDto BuildItem(
        PerformanceAppraisal a,
        string deptName,
        string position,
        string managerName,
        string? gradeLabel,
        AppraisalSubStatus subStatus,
        AttentionReason reason,
        string reasonLabel,
        string? detail,
        AttentionSeverity severity)
    {
        return new HRCycleAttentionItemDto
        {
            AppraisalId      = a.Id,
            EmployeeName     = a.Employee!.FullName,
            Position         = position,
            Department       = deptName,
            ManagerName      = managerName,
            Reason           = reason,
            ReasonLabel      = reasonLabel,
            Detail           = detail,
            Severity         = severity,
            OverallScore     = a.OverallScore,
            GradeLabel       = gradeLabel,
            DaysOverdue      = 0,
            CurrentSubStatus = subStatus,
        };
    }

    private static DeadlineState ComputeDeadlineState(DateOnly? deadline, DateOnly today)
    {
        if (!deadline.HasValue) return DeadlineState.None;
        int days = deadline.Value.DayNumber - today.DayNumber;
        return days < 0  ? DeadlineState.Overdue
             : days == 0 ? DeadlineState.Today
             : days <= 3 ? DeadlineState.Imminent
             : days <= 7 ? DeadlineState.Approaching
             :             DeadlineState.Safe;
    }

    private static DateOnly? GetStepDeadline(AppraisalSubStatus sub, AppraisalCycle cycle) =>
        sub switch
        {
            AppraisalSubStatus.GoalSetting              => cycle.GoalSettingDeadline,
            AppraisalSubStatus.PeerNomination           => cycle.PeerNominationDeadline,
            AppraisalSubStatus.SelfEvaluation           => cycle.SelfEvaluationDeadline,
            AppraisalSubStatus.PeerEvaluation           => cycle.PeerEvaluationDeadline,
            AppraisalSubStatus.ManagerEvaluation        => cycle.ManagerEvaluationDeadline,
            AppraisalSubStatus.PendingCalibration
            or AppraisalSubStatus.CalibrationInProgress => cycle.CalibrationDeadline,
            AppraisalSubStatus.PendingHRReview
            or AppraisalSubStatus.HRReviewInProgress    => cycle.HRReviewDeadline,
            AppraisalSubStatus.PendingAcknowledgment    => cycle.EmployeeAcknowledgeDeadline,
            AppraisalSubStatus.PendingConversation      => cycle.FinalConversationDeadline,
            _                                            => null,
        };

    private static AppraisalNotificationType MapSubStatusToActivityType(string toSubStatus) =>
        toSubStatus switch
        {
            nameof(AppraisalSubStatus.SelfEvaluation)           => AppraisalNotificationType.SelfEvalSubmitted,
            nameof(AppraisalSubStatus.PeerEvaluation)           => AppraisalNotificationType.AllPeerEvalsComplete,
            nameof(AppraisalSubStatus.ManagerEvaluation)        => AppraisalNotificationType.ManagerEvalSubmitted,
            nameof(AppraisalSubStatus.PendingCalibration)
            or nameof(AppraisalSubStatus.CalibrationInProgress) => AppraisalNotificationType.CalibrationComplete,
            nameof(AppraisalSubStatus.PendingHRReview)
            or nameof(AppraisalSubStatus.HRReviewInProgress)    => AppraisalNotificationType.HRReviewApproved,
            nameof(AppraisalSubStatus.PendingConversation)      => AppraisalNotificationType.ConversationCompleted,
            nameof(AppraisalSubStatus.PendingAcknowledgment)    => AppraisalNotificationType.EmployeeAcknowledged,
            nameof(AppraisalSubStatus.Completed)                => AppraisalNotificationType.EmployeeAcknowledged,
            _                                                    => AppraisalNotificationType.ActionRequired,
        };

    // The step names are AppraisalGates.Label — one list for the dashboard, the phase endpoint and
    // the refusal texts, so all three name the same step (performance closure B1).
}
