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
        ICurrentUserProvider                         currentUserProvider,
        ILogger<HRCycleDashboardQueryService>        logger)
    {
        _cycleRepo        = cycleRepo;
        _appraisalRepo    = appraisalRepo;
        _gradeRepo        = gradeRepo;
        _calibSessionRepo = calibSessionRepo;
        _advanceLogRepo   = advanceLogRepo;
        _employeeRepo     = employeeRepo;
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
                (c.Status == AppraisalCycleStatus.Open || c.Status == AppraisalCycleStatus.InProgress))
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
                .Include(a => a.EvaluatorEvaluations)
                .Include(a => a.PeerNominations)
                .Include(a => a.Conversations)
                .Include(a => a.HRReviews)
                .Include(a => a.OverallGrade)
                .Include(a => a.Goals)
                .AsNoTracking()
                .ToListAsync(ct);

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

            // 6. Load position titles (from current EmployeePosition) ─────────────
            // We derive position from the appraisal's Employee.PositionId via a
            // separate query to avoid N+1 when all appraisals are already loaded.
            var positionIds = appraisals
                .Select(a => a.Employee?.PositionId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var positionLookup = new Dictionary<Guid, string>();
            if (positionIds.Count > 0)
            {
                var positions = await _employeeRepo
                    .GetQueryable(e =>
                        !e.IsDeleted && positionIds.Contains(e.PositionId))
                    .Select(e => new { e.PositionId })
                    .Distinct()
                    .AsNoTracking()
                    .ToListAsync(ct);
                // We'll get position names from the EmployeePosition entity below
            }

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

                PipelineProgress   = BuildPipelineProgress(appraisals, settings),
                Deadlines          = BuildDeadlines(cycle, settings, appraisals, today),
                GradeDistribution  = BuildGradeDistribution(appraisals, allGrades,
                                        out int scoredCount, out decimal? avgScore),
                ScoredAppraisalCount = scoredCount,
                AverageScore         = avgScore,
                Recommendations    = BuildRecommendations(appraisals),
                DepartmentBreakdown = BuildDepartmentBreakdown(appraisals, calibSessions,
                                         managerLookup, cycle.GoalSettingDeadline, today),
                AttentionItems     = BuildAttentionItems(appraisals, settings, managerLookup, allGrades, cycle, today),
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

    // ── Section builders ──────────────────────────────────────────────────────

    private static HRCyclePipelineProgressDto BuildPipelineProgress(
        List<PerformanceAppraisal> appraisals,
        AppraisalSettings settings)
    {
        var p = new HRCyclePipelineProgressDto { TotalAppraisals = appraisals.Count };

        foreach (var a in appraisals)
        {
            var sub = AppraisalSubStatusResolver.Resolve(a, settings);
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
                    p.AcknowledgmentCompletedCount++;
                    break;

                case AppraisalSubStatus.Closed:
                    p.ClosedCount++;
                    break;
            }
        }

        // Self-eval and peer-eval completed counts = those who are PAST those stages
        p.SelfEvalCompletedCount = appraisals.Count(a =>
        {
            var sub = AppraisalSubStatusResolver.Resolve(a, settings);
            return (int)sub > (int)AppraisalSubStatus.SelfEvaluation;
        });

        p.PeerEvalCompletedCount = settings.RequirePeerReviews
            ? appraisals.Count(a =>
            {
                var sub = AppraisalSubStatusResolver.Resolve(a, settings);
                return (int)sub > (int)AppraisalSubStatus.PeerEvaluation;
            })
            : 0;

        p.ManagerEvalCompletedCount = appraisals.Count(a =>
        {
            var sub = AppraisalSubStatusResolver.Resolve(a, settings);
            return (int)sub > (int)AppraisalSubStatus.ManagerEvaluation;
        });

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
                "Goal Setting"       => appraisals.Count(a => a.Goals.Any(g => g.Status != GoalStatus.Draft && g.Status != GoalStatus.PendingApproval && g.Status != GoalStatus.Rejected)),
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

    private static List<HRCycleGradeDistributionItemDto> BuildGradeDistribution(
        List<PerformanceAppraisal>   appraisals,
        List<AppraisalGradeDefinition> grades,
        out int scoredCount,
        out decimal? average)
    {
        var scored = appraisals.Where(a => a.OverallGradeDefinitionId.HasValue).ToList();
        scoredCount = scored.Count;

        if (scoredCount == 0)
        {
            average = null;
            return grades.Select((g, i) => new HRCycleGradeDistributionItemDto
            {
                GradeDefinitionId = g.Id,
                GradeName         = g.GradeName,
                Color             = GradeColors[i % GradeColors.Length],
                Count             = 0,
                Percentage        = 0,
            }).ToList();
        }

        var withScores = appraisals.Where(a => a.OverallScore.HasValue).ToList();
        average = withScores.Count > 0
            ? Math.Round(withScores.Average(a => a.OverallScore!.Value), 2)
            : null;

        var byGrade = scored
            .GroupBy(a => a.OverallGradeDefinitionId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        int totalScored = scoredCount; // capture out-param before lambda
        return grades.Select((g, i) =>
        {
            int cnt = byGrade.GetValueOrDefault(g.Id, 0);
            return new HRCycleGradeDistributionItemDto
            {
                GradeDefinitionId = g.Id,
                GradeName         = g.GradeName,
                Color             = GradeColors[i % GradeColors.Length],
                Count             = cnt,
                Percentage        = totalScored > 0
                                    ? Math.Round((decimal)cnt / totalScored * 100m, 1)
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

            // Manager = head of the unit (look up ManagerId via any employee)
            var headId      = grp.FirstOrDefault()?.Employee?.ManagerId;
            var managerName = headId.HasValue && managerLookup.TryGetValue(headId.Value, out var mn)
                              ? mn : null;

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
        AppraisalSettings          settings,
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

            var deptName    = a.Employee.OrganizationUnit?.Name ?? string.Empty;
            var managerId   = a.Employee.ManagerId;
            var managerName = managerId.HasValue && managerLookup.TryGetValue(managerId.Value, out var mn) ? mn : string.Empty;
            var gradeLabel  = a.OverallGradeDefinitionId.HasValue && gradeMap.TryGetValue(a.OverallGradeDefinitionId.Value, out var gl) ? gl : null;
            var subStatus   = AppraisalSubStatusResolver.Resolve(a, settings);

            // PIP recommendation
            if (a.RecommendPIP)
            {
                items.Add(BuildItem(a, deptName, managerName, gradeLabel, subStatus,
                    AttentionReason.PIPrecommendation, "PIP Recommended",
                    "Employee has been recommended for a Performance Improvement Plan.",
                    AttentionSeverity.Critical));
            }

            // Termination recommendation
            if (a.RecommendTermination)
            {
                items.Add(BuildItem(a, deptName, managerName, gradeLabel, subStatus,
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
                    items.Add(BuildItem(a, deptName, managerName, gradeLabel, subStatus,
                        AttentionReason.AppealOverdue, "Appeal Overdue",
                        "The appeal re-evaluation deadline has passed without resolution.",
                        AttentionSeverity.Critical));
                }
                else
                {
                    items.Add(BuildItem(a, deptName, managerName, gradeLabel, subStatus,
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
                    Position         = string.Empty, // resolved below if possible
                    Department       = deptName,
                    ManagerName      = managerName,
                    Reason           = AttentionReason.OverdueAtStep,
                    ReasonLabel      = $"Overdue: {SubStatusLabel(subStatus)}",
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
                items.Add(BuildItem(a, deptName, managerName, gradeLabel, subStatus,
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

        // Source 3: active or resolved appeals
        var appealed = appraisals
            .Where(a => a.HasAppeal && a.UpdatedAt.HasValue)
            .OrderByDescending(a => a.UpdatedAt)
            .Take(10);

        foreach (var a in appealed)
        {
            bool resolved = a.CurrentAppealStatus is AppraisalAppealStatus.Upheld
                            or AppraisalAppealStatus.Rejected;
            items.Add(new HRCycleActivityItemDto
            {
                Timestamp           = a.UpdatedAt!.Value,
                Description         = resolved ? "Appeal resolved." : "Appeal submitted.",
                SubjectEmployeeName = a.Employee?.FullName,
                EventType           = resolved
                                      ? AppraisalNotificationType.AppealResolved
                                      : AppraisalNotificationType.AppealSubmitted,
                AppraisalId         = a.Id,
            });
        }

        return items
            .OrderByDescending(i => i.Timestamp)
            .Take(30)
            .ToList();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static HRCycleAttentionItemDto BuildItem(
        PerformanceAppraisal a,
        string deptName,
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
            Position         = string.Empty,
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

    private static string SubStatusLabel(AppraisalSubStatus sub) =>
        sub switch
        {
            AppraisalSubStatus.GoalSetting         => "Goal Setting",
            AppraisalSubStatus.PeerNomination      => "Peer Nomination",
            AppraisalSubStatus.SelfEvaluation      => "Self-Evaluation",
            AppraisalSubStatus.PeerEvaluation      => "Peer Evaluation",
            AppraisalSubStatus.ManagerEvaluation   => "Manager Evaluation",
            AppraisalSubStatus.PendingCalibration  => "Calibration",
            AppraisalSubStatus.CalibrationInProgress => "Calibration (In Progress)",
            AppraisalSubStatus.PendingHRReview     => "HR Review",
            AppraisalSubStatus.HRReviewInProgress  => "HR Review (In Progress)",
            AppraisalSubStatus.PendingConversation => "Final Conversation",
            AppraisalSubStatus.PendingAcknowledgment => "Acknowledgment",
            _                                       => sub.ToString(),
        };
}
