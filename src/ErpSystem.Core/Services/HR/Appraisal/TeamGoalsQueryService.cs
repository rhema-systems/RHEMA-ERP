using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>
/// Read-only governance query service for the Advanced Manager Workspace.
/// Route: /performance/team-goals
///
/// ── Security model ─────────────────────────────────────────────────────────
/// Every public method resolves the calling manager's EmployeeId from
/// ICurrentUserService and restricts all results to employees whose
/// Employee.ManagerId == that value. Security is enforced at the IQueryable
/// composition level — never in-memory after materialisation.
/// Never trust the UI; never filter post-materialisation for security.
///
/// ── Workflow vs Execution state separation ─────────────────────────────────
/// Workflow states  → Draft / PendingApproval / Approved / Rejected / Locked
///   These determine structural completeness (GovernanceStatus derivation).
///
/// Execution states → InProgress / OnTrack / AtRisk / Completed
///   These reflect live progress. They do NOT block StructurallyComplete.
///
/// ── Performance contracts ──────────────────────────────────────────────────
/// • AsNoTracking() on every query path.
/// • Select() scalar projections only — no Include() anywhere.
/// • IQueryable composition before ToListAsync() — no N+1.
/// • ProgressEntries and JournalEntries are never loaded.
/// • Overview: correlated subquery aggregation via directReports.Select().
/// • Flat tabs: single JOIN query + in-memory computed-field enrichment.
/// </summary>
public sealed class TeamGoalsQueryService : ITeamGoalsQueryService
{
    private readonly IGenericRepository<EmployeeGoal> _goalRepo;
    private readonly IGenericRepository<Employee> _employeeRepo;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IDateTimeProvider _clock;
    private readonly IGoalRiskSettingsProvider _riskSettingsProvider;
    private readonly IGoalRiskEvaluator _riskEvaluator;
    private readonly ILogger<TeamGoalsQueryService> _logger;

    public TeamGoalsQueryService(
        IGenericRepository<EmployeeGoal> goalRepo,
        IGenericRepository<Employee> employeeRepo,
        ICurrentUserService currentUserService,
        ICurrentUserProvider currentUserProvider,
        IDateTimeProvider clock,
        IGoalRiskSettingsProvider riskSettingsProvider,
        IGoalRiskEvaluator riskEvaluator,
        ILogger<TeamGoalsQueryService> logger)
    {
        _goalRepo             = goalRepo;
        _employeeRepo         = employeeRepo;
        _currentUserService   = currentUserService;
        _currentUserProvider  = currentUserProvider;
        _clock                = clock;
        _riskSettingsProvider = riskSettingsProvider;
        _riskEvaluator        = riskEvaluator;
        _logger               = logger;
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

    // =========================================================================
    //  Overview Tab
    // =========================================================================

    /// <inheritdoc />
    public async Task<List<TeamMemberOverviewDto>> GetTeamOverviewAsync(
        Guid appraisalCycleId,
        CancellationToken cancellationToken = default)
    {
        // ── Security: resolve current manager ─────────────────────────────────
        var managerId = ResolveCurrentManagerId();
        var tenantId  = GetTenantId();
        var today = _clock.TodayUtc;

        _logger.LogDebug(
            "TeamGoalsQueryService.GetTeamOverviewAsync: managerId={ManagerId}, cycleId={CycleId}",
            managerId, appraisalCycleId);

        // ── Base queryables ────────────────────────────────────────────────────
        // Both share the same scoped DbContext → they can be composed together.

        // SECURITY BOUNDARY: employees whose ManagerId matches the current manager.
        var directReports = _employeeRepo.GetQueryable()
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.ManagerId == managerId && e.IsActive);

        // Cycle goals — used as correlated subquery inside the Select below.
        var cycleGoals = _goalRepo.GetQueryable()
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId && g.AppraisalCycleId == appraisalCycleId);

        // ── Projection: one row per direct report, aggregates computed in SQL ──
        // Employees with zero goals are included — TotalGoals == 0 → NotStarted.
        // Correlated COUNT/SUM subqueries translate cleanly to SQL Server.
        var rawRows = await directReports
            .Select(e => new
            {
                EmployeeId   = e.Id,
                EmployeeName = e.FirstName + " " + e.LastName,

                // ── Workflow-state counts ────────────────────────────────────
                TotalGoals           = cycleGoals.Count(g => g.EmployeeId == e.Id),
                DraftCount           = cycleGoals.Count(g => g.EmployeeId == e.Id && g.Status == GoalStatus.Draft),
                PendingApprovalCount = cycleGoals.Count(g => g.EmployeeId == e.Id && g.Status == GoalStatus.PendingApproval),
                RejectedCount        = cycleGoals.Count(g => g.EmployeeId == e.Id && g.Status == GoalStatus.Rejected),
                LockedCount          = cycleGoals.Count(g => g.EmployeeId == e.Id && g.Status == GoalStatus.Locked),

                // ApprovedWorkflow = Status >= Approved AND Status != Rejected
                // Covers: Approved, InProgress, OnTrack, AtRisk, Completed, Locked
                ApprovedWorkflowCount = cycleGoals.Count(g =>
                    g.EmployeeId == e.Id
                    && g.Status >= GoalStatus.Approved
                    && g.Status != GoalStatus.Rejected),

                // ── Execution-state counts (progress visibility only) ─────────
                InProgressCount = cycleGoals.Count(g => g.EmployeeId == e.Id && g.Status == GoalStatus.InProgress),
                AtRiskCount     = cycleGoals.Count(g => g.EmployeeId == e.Id && g.Status == GoalStatus.AtRisk),
                CompletedCount  = cycleGoals.Count(g => g.EmployeeId == e.Id && g.Status == GoalStatus.Completed),

                // ── Overdue count ────────────────────────────────────────────
                // DueDate < today AND not Completed AND not Locked.
                // Status is single source of truth — IsLocked flag is NOT used.
                OverdueCount = cycleGoals.Count(g =>
                    g.EmployeeId == e.Id
                    && g.DueDate < today
                    && g.Status != GoalStatus.Completed
                    && g.Status != GoalStatus.Locked),

                // ── Weight sum ───────────────────────────────────────────────
                TotalWeight = cycleGoals
                    .Where(g => g.EmployeeId == e.Id)
                    .Sum(g => (int?)g.Weight) ?? 0,
            })
            .ToListAsync(cancellationToken);

        // ── Post-projection: derive IsWeightBalanced + GovernanceStatus ───────
        // Pure in-memory computations — no additional DB round-trip.
        return rawRows.Select(r => new TeamMemberOverviewDto
        {
            EmployeeId            = r.EmployeeId,
            EmployeeName          = r.EmployeeName.Trim(),
            TotalGoals            = r.TotalGoals,
            DraftCount            = r.DraftCount,
            PendingApprovalCount  = r.PendingApprovalCount,
            ApprovedWorkflowCount = r.ApprovedWorkflowCount,
            RejectedCount         = r.RejectedCount,
            LockedCount           = r.LockedCount,
            InProgressCount       = r.InProgressCount,
            AtRiskCount           = r.AtRiskCount,
            CompletedCount        = r.CompletedCount,
            OverdueCount          = r.OverdueCount,
            TotalWeight           = r.TotalWeight,
            IsWeightBalanced      = r.TotalGoals > 0 && r.TotalWeight == 100,
            GovernanceStatus      = DeriveGovernanceStatus(
                                        r.TotalGoals,
                                        r.DraftCount,
                                        r.RejectedCount,
                                        r.PendingApprovalCount,
                                        r.TotalWeight),
        }).ToList();
    }

    // =========================================================================
    //  Awaiting Approval Tab
    // =========================================================================

    /// <inheritdoc />
    public async Task<List<TeamGoalFlatDto>> GetGoalsAwaitingApprovalAsync(
        Guid appraisalCycleId,
        CancellationToken cancellationToken = default)
    {
        var managerId    = ResolveCurrentManagerId();
        var tenantId     = GetTenantId();
        var utcNow       = _clock.UtcNow;
        var riskSettings = await _riskSettingsProvider.GetActiveAsync(cancellationToken);

        _logger.LogDebug(
            "TeamGoalsQueryService.GetGoalsAwaitingApprovalAsync: managerId={ManagerId}, cycleId={CycleId}",
            managerId, appraisalCycleId);

        // SECURITY: BuildBaseGoalQuery enforces direct-report restriction
        var raw = await BuildBaseGoalQuery(appraisalCycleId, managerId, tenantId)
            .Where(r => r.Status == GoalStatus.PendingApproval)
            .ToListAsync(cancellationToken);

        return EnrichWithComputedFields(raw, riskSettings, utcNow);
    }

    // =========================================================================
    //  At-Risk Tab
    // =========================================================================

    /// <inheritdoc />
    public async Task<List<TeamGoalFlatDto>> GetAtRiskGoalsAsync(
        Guid appraisalCycleId,
        CancellationToken cancellationToken = default)
    {
        var managerId    = ResolveCurrentManagerId();
        var tenantId     = GetTenantId();
        var utcNow       = _clock.UtcNow;
        var today        = DateOnly.FromDateTime(utcNow);
        var riskSettings = await _riskSettingsProvider.GetActiveAsync(cancellationToken);
        // Lookahead window driven by configured threshold (default 14 days)
        var cutoffDate = today.AddDays(riskSettings.DaysRemainingThreshold);

        _logger.LogDebug(
            "TeamGoalsQueryService.GetAtRiskGoalsAsync: managerId={ManagerId}, cycleId={CycleId}, today={Today}, cutoff={Cutoff}, minProgress={MinProgress}",
            managerId, appraisalCycleId, today, cutoffDate, riskSettings.MinimumProgressPercent);

        // ── At-risk pre-filter ───────────────────────────────────────────────
        // (a) Status == AtRisk                                   — explicit marker
        // (b) Status is InProgress/OnTrack
        //     AND DueDate <= today + DaysRemainingThreshold      — approaching deadline
        //     AND ProgressPercent < MinimumProgressPercent       — insufficient progress
        var raw = await BuildBaseGoalQuery(appraisalCycleId, managerId, tenantId)
            .Where(r =>
                r.Status == GoalStatus.AtRisk
                || (
                    (r.Status == GoalStatus.InProgress || r.Status == GoalStatus.OnTrack)
                    && r.DueDate <= cutoffDate
                    && r.ProgressPercent < riskSettings.MinimumProgressPercent
                ))
            .ToListAsync(cancellationToken);

        return EnrichWithComputedFields(raw, riskSettings, utcNow);
    }

    // =========================================================================
    //  Overdue Tab
    // =========================================================================

    /// <inheritdoc />
    public async Task<List<TeamGoalFlatDto>> GetOverdueGoalsAsync(
        Guid appraisalCycleId,
        CancellationToken cancellationToken = default)
    {
        var managerId    = ResolveCurrentManagerId();
        var tenantId     = GetTenantId();
        var utcNow       = _clock.UtcNow;
        var today        = DateOnly.FromDateTime(utcNow);
        var riskSettings = await _riskSettingsProvider.GetActiveAsync(cancellationToken);

        _logger.LogDebug(
            "TeamGoalsQueryService.GetOverdueGoalsAsync: managerId={ManagerId}, cycleId={CycleId}, today={Today}",
            managerId, appraisalCycleId, today);

        // ── Overdue rule ─────────────────────────────────────────────────────
        // DueDate < today AND Status is not Completed, Locked, or Rejected.
        //   • Completed: goal is done — not overdue.
        //   • Locked: goal is frozen by HR/cycle-close — not actionable.
        //   • Rejected: goal was rejected in workflow — work item terminated; not overdue.
        // Status field is the single source of truth.
        // The legacy IsLocked boolean flag is intentionally NOT consulted.
        var raw = await BuildBaseGoalQuery(appraisalCycleId, managerId, tenantId)
            .Where(r =>
                r.DueDate < today
                && r.Status != GoalStatus.Completed
                && r.Status != GoalStatus.Locked
                && r.Status != GoalStatus.Rejected)
            .ToListAsync(cancellationToken);

        // Sort: most overdue first (DaysOverdue desc), then earliest due date,
        // then employee name for stable ordering on ties.
        return EnrichWithComputedFields(raw, riskSettings, utcNow)
            .OrderByDescending(g => g.DaysOverdue ?? 0)
            .ThenBy(g => g.DueDate)
            .ThenBy(g => g.EmployeeName)
            .ToList();
    }

    // =========================================================================
    //  Locked Tab
    // =========================================================================

    /// <inheritdoc />
    public async Task<List<TeamGoalFlatDto>> GetLockedGoalsAsync(
        Guid appraisalCycleId,
        CancellationToken cancellationToken = default)
    {
        var managerId    = ResolveCurrentManagerId();
        var tenantId     = GetTenantId();
        var utcNow       = _clock.UtcNow;
        var riskSettings = await _riskSettingsProvider.GetActiveAsync(cancellationToken);

        _logger.LogDebug(
            "TeamGoalsQueryService.GetLockedGoalsAsync: managerId={ManagerId}, cycleId={CycleId}",
            managerId, appraisalCycleId);

        var raw = await BuildBaseGoalQuery(appraisalCycleId, managerId, tenantId)
            .Where(r => r.Status == GoalStatus.Locked)
            .ToListAsync(cancellationToken);

        return EnrichWithComputedFields(raw, riskSettings, utcNow)
            .OrderByDescending(g => g.LockedDate)
            .ThenBy(g => g.EmployeeName)
            .ThenBy(g => g.Title)
            .ToList();
    }

    // =========================================================================
    //  Employee Drill-Down
    // =========================================================================

    /// <inheritdoc />
    public async Task<List<TeamGoalFlatDto>> GetEmployeeGoalsAsync(
        Guid employeeId,
        Guid appraisalCycleId,
        CancellationToken cancellationToken = default)
    {
        var managerId    = ResolveCurrentManagerId();
        var tenantId     = GetTenantId();
        var utcNow       = _clock.UtcNow;
        var riskSettings = await _riskSettingsProvider.GetActiveAsync(cancellationToken);

        _logger.LogDebug(
            "TeamGoalsQueryService.GetEmployeeGoalsAsync: managerId={ManagerId}, employeeId={EmployeeId}, cycleId={CycleId}",
            managerId, employeeId, appraisalCycleId);

        // SECURITY: BuildBaseGoalQuery enforces e.ManagerId == managerId.
        // The additional .Where(r => r.EmployeeId == employeeId) is folded into
        // the SQL WHERE clause by EF Core.  If employeeId is not a direct report
        // the result will simply be empty (the security invariant is preserved
        // without a separate round-trip), but we surface it as an
        // UnauthorizedAccessException so the UI can display a clear denial.
        var raw = await BuildBaseGoalQuery(appraisalCycleId, managerId, tenantId)
            .Where(r => r.EmployeeId == employeeId)
            .ToListAsync(cancellationToken);

        if (raw.Count == 0)
        {
            // Verify whether the employee exists at all under this manager.
            // A direct report with zero goals returns an empty list (not an error);
            // a non-direct-report or non-existent employee throws Unauthorized.
            var isDirect = await _employeeRepo.GetQueryable()
                .AsNoTracking()
                .AnyAsync(e => e.Id == employeeId && e.TenantId == tenantId && e.ManagerId == managerId, cancellationToken);

            if (!isDirect)
            {
                _logger.LogWarning(
                    "GetEmployeeGoalsAsync: employee {EmployeeId} is not a direct report of manager {ManagerId}.",
                    employeeId, managerId);
                throw new UnauthorizedAccessException(
                    "The specified employee is not a direct report of the current manager.");
            }
        }

        return EnrichWithComputedFields(raw, riskSettings, utcNow);
    }

    // =========================================================================
    //  Team Goal Progress (Manager Dashboard)
    // =========================================================================

    /// <inheritdoc />
    public async Task<List<TeamGoalProgressDto>> GetTeamGoalProgressAsync(
        Guid appraisalCycleId,
        CancellationToken cancellationToken = default)
    {
        var managerId = ResolveCurrentManagerId();
        var tenantId  = GetTenantId();
        var today     = _clock.TodayUtc;

        _logger.LogDebug(
            "TeamGoalsQueryService.GetTeamGoalProgressAsync: managerId={ManagerId}, cycleId={CycleId}",
            managerId, appraisalCycleId);

        // ── Step 1: Load all non-Draft, non-Rejected goals for direct reports ──
        // We include Approved through Locked so the manager sees the full
        // in-execution view. Draft / Rejected goals are excluded intentionally:
        //   • Draft     → not yet submitted; nothing to track.
        //   • Rejected  → workflow terminated; no execution progress.
        var raw = await BuildBaseGoalQuery(appraisalCycleId, managerId, tenantId)
            .Where(r => r.Status != GoalStatus.Draft && r.Status != GoalStatus.Rejected)
            .ToListAsync(cancellationToken);

        // ── Step 2: Also load all direct-report employees so that employees
        //    with zero qualifying goals still appear in the list. ──────────────
        var directReports = await _employeeRepo.GetQueryable()
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.ManagerId == managerId && e.IsActive)
            .Select(e => new { EmployeeId = e.Id, EmployeeName = e.FirstName + " " + e.LastName })
            .ToListAsync(cancellationToken);

        // ── Step 3: Group goal rows by employee ───────────────────────────────
        var goalsByEmployee = raw.GroupBy(r => r.EmployeeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // ── Step 4: Build one TeamGoalProgressDto per direct report ───────────
        var result = new List<TeamGoalProgressDto>(directReports.Count);

        foreach (var emp in directReports.OrderBy(e => e.EmployeeName))
        {
            var goals = goalsByEmployee.GetValueOrDefault(emp.EmployeeId) ?? new List<RawGoalRow>();

            // ── Per-goal summary list ────────────────────────────────────────
            var summaries = goals
                .OrderBy(g => g.DueDate)
                .ThenBy(g => g.Title)
                .Select(g =>
                {
                    var isOverdue = g.DueDate < today
                        && g.Status != GoalStatus.Completed
                        && g.Status != GoalStatus.Locked;

                    return new TeamProgressGoalItemDto
                    {
                        GoalId          = g.GoalId,
                        Title           = g.Title,
                        ProgressPercent = g.ProgressPercent,
                        Status          = g.Status,
                        Weight          = g.Weight,
                        DueDate         = g.DueDate,
                        IsOverdue       = isOverdue,
                        DaysOverdue     = isOverdue ? today.DayNumber - g.DueDate.DayNumber : null,
                    };
                })
                .ToList();

            // ── Aggregate counts ─────────────────────────────────────────────
            int overdueCount = summaries.Count(s => s.IsOverdue);

            decimal avgProgress = goals.Count > 0
                ? Math.Round(goals.Average(g => g.ProgressPercent), 1)
                : 0m;

            result.Add(new TeamGoalProgressDto
            {
                EmployeeId             = emp.EmployeeId,
                EmployeeName           = emp.EmployeeName.Trim(),
                TotalGoals             = goals.Count,
                NotStartedCount        = goals.Count(g => g.Status == GoalStatus.Approved),
                InProgressCount        = goals.Count(g => g.Status == GoalStatus.InProgress),
                OnTrackCount           = goals.Count(g => g.Status == GoalStatus.OnTrack),
                AtRiskCount            = goals.Count(g => g.Status == GoalStatus.AtRisk),
                CompletedCount         = goals.Count(g => g.Status == GoalStatus.Completed),
                OverdueCount           = overdueCount,
                AverageProgressPercent = avgProgress,
                Goals                  = summaries,
            });
        }

        return result;
    }

    // =========================================================================
    //  Private helpers
    // =========================================================================

    /// <summary>
    /// Resolves the currently authenticated manager's EmployeeId.
    /// Throws <see cref="UnauthorizedAccessException"/> when the user has no
    /// linked employee record, blocking all access before any query executes.
    /// </summary>
    private Guid ResolveCurrentManagerId()
    {
        var employeeId = _currentUserService.EmployeeId;
        if (employeeId is null)
        {
            _logger.LogWarning("TeamGoalsQueryService: current user has no linked EmployeeId.");
            throw new UnauthorizedAccessException(
                "The current user does not have an employee record linked to their account.");
        }
        return employeeId.Value;
    }

    /// <summary>
    /// Builds the security-enforced, fully-scalar projected base query for all
    /// flat-DTO methods. Returns <see cref="IQueryable{RawGoalRow}"/> so callers
    /// can compose additional typed .Where() predicates before materialisation.
    /// EF Core folds those predicates into the SQL WHERE clause.
    ///
    /// ── Security ──────────────────────────────────────────────────────────
    /// The INNER JOIN + <c>e.ManagerId == managerId</c> is the enforcement
    /// boundary — applied inside the IQueryable, never post-materialisation.
    ///
    /// ── Performance ───────────────────────────────────────────────────────
    /// • AsNoTracking() on both sides of the JOIN.
    /// • Projection to RawGoalRow (all scalar) avoids loading entity graphs.
    /// • ProgressEntries / JournalEntries are never referenced.
    /// </summary>
    private IQueryable<RawGoalRow> BuildBaseGoalQuery(Guid cycleId, Guid managerId, Guid tenantId)
    {
        return
            from g in _goalRepo.GetQueryable().AsNoTracking()
            join e in _employeeRepo.GetQueryable().AsNoTracking()
                on g.EmployeeId equals e.Id
            where g.TenantId == tenantId
               && e.TenantId == tenantId
               && g.AppraisalCycleId == cycleId    // cycle scope
               && e.ManagerId == managerId           // ← SECURITY: direct reports only
            select new RawGoalRow
            {
                GoalId          = g.Id,
                EmployeeId      = g.EmployeeId,
                EmployeeName    = e.FirstName + " " + e.LastName,
                Title           = g.Title,
                Status          = g.Status,
                Weight          = g.Weight,
                ProgressPercent = g.ProgressPercent,
                StartDate       = g.StartDate,
                DueDate         = g.DueDate,
                SubmittedDate   = g.SubmittedDate,
                ApprovalDate    = g.ApprovalDate,
                LockedDate      = g.LockedDate,
            };
    }

    /// <summary>
    /// Maps materialised <see cref="RawGoalRow"/> records to <see cref="TeamGoalFlatDto"/>,
    /// computes <see cref="TeamGoalFlatDto.DaysRemaining"/> / <see cref="TeamGoalFlatDto.IsOverdue"/>,
    /// and enriches each row with risk evaluation results from <see cref="IGoalRiskEvaluator"/>.
    ///
    /// ── Risk evaluation contract ──────────────────────────────────────────────
    /// • <paramref name="riskSettings"/> is loaded once by the caller — never per row.
    /// • <paramref name="utcNow"/> is captured once by the caller — never inside the loop.
    /// • <see cref="IGoalRiskEvaluator.Evaluate"/> is pure and synchronous — zero DB access.
    /// • Risk is computed from a minimal <see cref="EmployeeGoal"/> projection of the row.
    ///
    /// DateOnly arithmetic is performed after ToListAsync() because EF Core does
    /// not translate DateOnly subtraction to SQL. No additional DB call is made.
    ///
    /// DaysRemaining: positive = days remaining; negative = days past due.
    /// IsOverdue: DueDate &lt; today AND not Completed AND not Locked.
    /// </summary>
    private List<TeamGoalFlatDto> EnrichWithComputedFields(
        List<RawGoalRow> rows,
        GoalRiskSetting  riskSettings,
        DateTime         utcNow)
    {
        // Derive DateOnly once; consistent with the instant captured by the caller.
        var today = DateOnly.FromDateTime(utcNow);

        return rows.Select(r =>
        {
            // Evaluate risk in-memory using the pre-loaded settings.
            // ToMinimalGoalForEvaluation produces a lightweight projection with only
            // the four scalar fields consumed by GoalRiskEvaluator — no entity graph.
            var risk = _riskEvaluator.Evaluate(ToMinimalGoalForEvaluation(r), riskSettings, utcNow);

            return new TeamGoalFlatDto
            {
                GoalId          = r.GoalId,
                EmployeeId      = r.EmployeeId,
                EmployeeName    = r.EmployeeName.Trim(),
                Title           = r.Title,
                Status          = r.Status,
                Weight          = r.Weight,
                ProgressPercent = r.ProgressPercent,
                DueDate         = r.DueDate,
                SubmittedDate   = r.SubmittedDate,
                ApprovalDate    = r.ApprovalDate,
                // DayNumber-based difference avoids time-of-day noise in DateOnly comparison
                DaysRemaining   = r.DueDate.DayNumber - today.DayNumber,
                // Status is the single source of truth — IsLocked flag is not consulted.
                // Rejected goals are also excluded: a rejected goal can never be "overdue"
                // in an actionable sense — the workflow itself terminated the work item.
                IsOverdue       = r.DueDate < today
                                  && r.Status != GoalStatus.Completed
                                  && r.Status != GoalStatus.Locked
                                  && r.Status != GoalStatus.Rejected,
                // Positive integer: how many calendar days past the due date.
                // Null for all non-overdue goals — computed once here, consumed by the
                // Overdue tab sort (OrderByDescending) and the UI badge.
                DaysOverdue     = r.DueDate < today
                                  && r.Status != GoalStatus.Completed
                                  && r.Status != GoalStatus.Locked
                                  && r.Status != GoalStatus.Rejected
                                  ? today.DayNumber - r.DueDate.DayNumber
                                  : null,
                DaysPendingApproval = r.Status == GoalStatus.PendingApproval && r.SubmittedDate.HasValue
                    ? today.DayNumber - DateOnly.FromDateTime(r.SubmittedDate.Value).DayNumber
                    : 0,
                // ── Risk fields ─────────────────────────────────────────────────
                IsAtRisk          = risk.IsAtRisk,
                RiskReason        = risk.Reason,
                RiskSeverityScore = risk.SeverityScore,
                LockedDate        = r.LockedDate,
            };
        }).ToList();
    }

    /// <summary>
    /// Constructs a minimal <see cref="EmployeeGoal"/> projection from a
    /// <see cref="RawGoalRow"/> for use as the input to
    /// <see cref="IGoalRiskEvaluator.Evaluate"/>.
    ///
    /// Only the four scalar fields read by <c>GoalRiskEvaluator</c> are populated:
    ///   • <c>Status</c>          — terminal-status guard (Completed / Rejected)
    ///   • <c>DueDate</c>         — Rule 1: days-remaining threshold
    ///   • <c>StartDate</c>       — Rule 2: linear timeline calculation
    ///   • <c>ProgressPercent</c> — both rules
    ///
    /// Navigation properties and all other fields are left at their defaults;
    /// the evaluator never accesses them.
    /// </summary>
    private static EmployeeGoal ToMinimalGoalForEvaluation(RawGoalRow r) =>
        new()
        {
            Status          = r.Status,
            DueDate         = r.DueDate,
            StartDate       = r.StartDate,
            ProgressPercent = r.ProgressPercent,
        };

    /// <summary>
    /// Derives the governance completeness status for a single employee.
    ///
    /// Priority order (first match wins):
    ///   1. NotStarted       → TotalGoals == 0
    ///   2. InProgress       → DraftCount > 0 OR RejectedCount > 0
    ///                         (structural work still in progress)
    ///   3. AwaitingApproval → PendingApprovalCount > 0
    ///   4. InvalidWeight    → TotalWeight != 100
    ///   5. StructurallyComplete (all structural conditions satisfied)
    ///
    /// Execution states (InProgress, OnTrack, AtRisk, Completed) play NO role
    /// in this derivation by design — they are separated into execution counts.
    /// </summary>
    private static TeamGovernanceStatus DeriveGovernanceStatus(
        int totalGoals,
        int draftCount,
        int rejectedCount,
        int pendingApprovalCount,
        int totalWeight)
    {
        if (totalGoals == 0)
            return TeamGovernanceStatus.NotStarted;

        // Structural work is incomplete when drafts or rejections remain
        if (draftCount > 0 || rejectedCount > 0)
            return TeamGovernanceStatus.InProgress;

        // All drafts resolved but at least one is pending manager review
        if (pendingApprovalCount > 0)
            return TeamGovernanceStatus.AwaitingApproval;

        // Approved goals exist but total weight doesn't sum to 100
        if (totalWeight != 100)
            return TeamGovernanceStatus.InvalidWeight;

        // StructurallyComplete:
        //   TotalGoals > 0          ✓ (checked first)
        //   TotalWeight == 100      ✓
        //   DraftCount == 0         ✓
        //   RejectedCount == 0      ✓
        //   PendingApprovalCount == 0  ✓
        //   Execution states are intentionally excluded.
        return TeamGovernanceStatus.StructurallyComplete;
    }

    // =========================================================================
    //  Internal projection record (DB → memory bridge)
    // =========================================================================

    /// <summary>
    /// Scalar projection of an EmployeeGoal + Employee JOIN row.
    /// Used exclusively as the EF Core queryable element type for flat-tab queries.
    /// Because all fields are scalars, callers can compose .Where() predicates
    /// on this type and EF Core translates them into SQL WHERE clauses.
    /// </summary>
    private sealed class RawGoalRow
    {
        public Guid GoalId { get; init; }
        public Guid EmployeeId { get; init; }
        public string EmployeeName { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public GoalStatus Status { get; init; }
        public int Weight { get; init; }
        public decimal ProgressPercent { get; init; }
        /// <summary>Required by GoalRiskEvaluator Rule 2 (behind expected timeline).</summary>
        public DateOnly StartDate { get; init; }
        public DateOnly DueDate { get; init; }
        public DateTime? SubmittedDate { get; init; }
        public DateTime? ApprovalDate { get; init; }
        /// <summary>Populated for Locked tab sort; null for non-locked goals.</summary>
        public DateTime? LockedDate { get; init; }
    }
}
