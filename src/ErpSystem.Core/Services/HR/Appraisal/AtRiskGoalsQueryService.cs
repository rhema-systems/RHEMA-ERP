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
/// Org-wide at-risk goal query service for HR and admin roles.
///
/// ── Scope difference from TeamGoalsQueryService ────────────────────────────
/// <see cref="TeamGoalsQueryService"/> is manager-scoped: every query is
/// restricted to <c>Employee.ManagerId == currentManagerId</c>.
/// This service has NO manager scope — it queries ALL active employees in the
/// organisation (or an optional org-unit/level sub-set) and is intended for
/// HR Officers, HR Managers, and Admins who need a cross-team risk view.
///
/// Access control is enforced at the controller layer via [Authorize(Roles=...)];
/// this service makes no identity checks.
///
/// ── Two-phase pipeline ──────────────────────────────────────────────────────
/// Phase 1 (SQL):  Pre-filter candidates on Status — the agreed goals not yet
///                 completed (GoalSetRules.RiskWatched, decision D-71).
///                 This is a wide net: it includes goals that the evaluator
///                 will later mark as NOT at risk (false positives are fine here).
/// Phase 2 (memory): IGoalRiskEvaluator applies the full configurable ruleset
///                 per row.  Only confirmed at-risk goals are returned.
///
/// ── Performance contracts ──────────────────────────────────────────────────
/// • AsNoTracking() on every query path.
/// • Single JOIN query — no N+1.
/// • IGoalRiskSettingsProvider called once per request (not per row).
/// • IGoalRiskEvaluator is pure and synchronous — zero DB access.
/// • Results sorted by RiskSeverityScore desc, then DaysRemaining asc.
/// </summary>
public sealed class AtRiskGoalsQueryService : IAtRiskGoalsQueryService
{
    private readonly IGenericRepository<EmployeeGoal> _goalRepo;
    private readonly IGenericRepository<Employee>     _employeeRepo;
    private readonly IGoalRiskSettingsProvider        _riskSettingsProvider;
    private readonly IGoalRiskEvaluator               _riskEvaluator;
    private readonly ICurrentUserProvider             _currentUserProvider;
    private readonly IDateTimeProvider                _clock;
    private readonly ILogger<AtRiskGoalsQueryService> _logger;

    public AtRiskGoalsQueryService(
        IGenericRepository<EmployeeGoal> goalRepo,
        IGenericRepository<Employee>     employeeRepo,
        IGoalRiskSettingsProvider        riskSettingsProvider,
        IGoalRiskEvaluator               riskEvaluator,
        ICurrentUserProvider             currentUserProvider,
        IDateTimeProvider                clock,
        ILogger<AtRiskGoalsQueryService> logger)
    {
        _goalRepo             = goalRepo;
        _employeeRepo         = employeeRepo;
        _riskSettingsProvider = riskSettingsProvider;
        _riskEvaluator        = riskEvaluator;
        _currentUserProvider  = currentUserProvider;
        _clock                = clock;
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

    /// <inheritdoc />
    public async Task<List<TeamGoalFlatDto>> GetOrgWideAtRiskGoalsAsync(
        AtRiskGoalsRequest request,
        CancellationToken  cancellationToken = default)
    {
        var utcNow       = _clock.UtcNow;
        var today        = DateOnly.FromDateTime(utcNow);
        var riskSettings = await _riskSettingsProvider.GetActiveAsync(cancellationToken);

        _logger.LogDebug(
            "AtRiskGoalsQueryService.GetOrgWideAtRiskGoalsAsync: cycleId={CycleId}, " +
            "unitId={OrgUnitId}, levelId={OrgLevelId}, today={Today}, " +
            "daysThreshold={Days}, minProgress={MinProgress}, tolerance={Tolerance}",
            request.AppraisalCycleId,
            request.OrganizationUnitId,
            request.OrganizationLevelId,
            today, riskSettings.DaysRemainingThreshold, riskSettings.MinimumProgressPercent,
            riskSettings.ExpectedProgressTolerancePercent);

        // ── Phase 1: SQL pre-filter ───────────────────────────────────────────
        // Builds a single JOIN query; optional org filters fold into the WHERE.
        // The agreed, unfinished goals of the cycle; the evaluator (Phase 2) is the
        // authoritative gate for at-risk classification.
        var query = BuildCandidateQuery(request, GetTenantId());
        var raw   = await query.ToListAsync(cancellationToken);

        _logger.LogDebug(
            "AtRiskGoalsQueryService: {Count} pre-filter candidates for cycleId={CycleId}",
            raw.Count, request.AppraisalCycleId);

        // ── Phase 2: in-memory evaluation + confirmed-at-risk filter ──────────
        // EnrichWithRisk calls IGoalRiskEvaluator per row (pure, no DB).
        // We keep only confirmed at-risk goals and sort by urgency.
        var confirmed = EnrichWithRisk(raw, riskSettings, utcNow)
            .Where(g => g.IsAtRisk)
            .OrderByDescending(g => g.RiskSeverityScore)
            .ThenBy(g => g.DaysRemaining)
            .ThenBy(g => g.EmployeeName)
            .ToList();

        _logger.LogDebug(
            "AtRiskGoalsQueryService: {Confirmed}/{Total} confirmed at-risk for cycleId={CycleId}",
            confirmed.Count, raw.Count, request.AppraisalCycleId);

        return confirmed;
    }

    // =========================================================================
    //  Private helpers
    // =========================================================================

    /// <summary>
    /// Builds the org-wide candidate JOIN query.
    /// Optional <see cref="AtRiskGoalsRequest.OrganizationUnitId"/> and
    /// <see cref="AtRiskGoalsRequest.OrganizationLevelId"/> filters are composed
    /// into the IQueryable before materialisation — EF Core folds them into SQL.
    ///
    /// The pre-filter admits every agreed goal not yet completed (<see cref="GoalSetRules.RiskWatched"/>);
    /// Phase 2 decides with the full evaluator ruleset. It used to admit only goals marked at risk and
    /// running goals due within the window under the minimum — rule 1's own test — so rule 2 (behind
    /// the straight line) never changed who was listed, and an approved goal nobody had touched was
    /// never on it (performance closure D-71).
    /// </summary>
    private IQueryable<OrgRawGoalRow> BuildCandidateQuery(
        AtRiskGoalsRequest request,
        Guid               tenantId)
    {
        var watched = GoalSetRules.RiskWatched;

        // SECURITY note: no ManagerId scope here — intentional for org-wide view.
        // Access control is at the controller level.
        var employees = _employeeRepo.GetQueryable()
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.IsActive);

        // Apply optional org filters — EF Core translates these to SQL WHERE clauses.
        if (request.OrganizationUnitId.HasValue)
            employees = employees.Where(e => e.OrganizationUnitId == request.OrganizationUnitId.Value);

        if (request.OrganizationLevelId.HasValue)
            employees = employees.Where(e => e.OrganizationLevelId == request.OrganizationLevelId.Value);

        return
            from g in _goalRepo.GetQueryable().AsNoTracking()
            join e in employees on g.EmployeeId equals e.Id
            where g.TenantId == tenantId
               && g.AppraisalCycleId == request.AppraisalCycleId
               && watched.Contains(g.Status)
            select new OrgRawGoalRow
            {
                GoalId               = g.Id,
                EmployeeId           = g.EmployeeId,
                EmployeeName         = e.FirstName + " " + e.LastName,
                Title                = g.Title,
                Status               = g.Status,
                Weight               = g.Weight,
                ProgressPercent      = g.ProgressPercent,
                StartDate            = g.StartDate,
                DueDate              = g.DueDate,
                SubmittedDate        = g.SubmittedDate,
                ApprovalDate         = g.ApprovalDate,
                OrganizationUnitId   = e.OrganizationUnitId ?? Guid.Empty,
                OrganizationLevelId  = e.OrganizationLevelId ?? Guid.Empty,
            };
    }

    /// <summary>
    /// Maps <see cref="OrgRawGoalRow"/> rows to <see cref="TeamGoalFlatDto"/>,
    /// running <see cref="IGoalRiskEvaluator.Evaluate"/> for each row.
    /// All rows are returned (including non-at-risk); callers filter on IsAtRisk.
    /// </summary>
    private List<TeamGoalFlatDto> EnrichWithRisk(
        List<OrgRawGoalRow> rows,
        GoalRiskSetting     riskSettings,
        DateTime            utcNow)
    {
        var today = DateOnly.FromDateTime(utcNow);

        return rows.Select(r =>
        {
            var risk = _riskEvaluator.Evaluate(
                new EmployeeGoal
                {
                    Status          = r.Status,
                    DueDate         = r.DueDate,
                    StartDate       = r.StartDate,
                    ProgressPercent = r.ProgressPercent,
                },
                riskSettings,
                utcNow);

            return new TeamGoalFlatDto
            {
                GoalId            = r.GoalId,
                EmployeeId        = r.EmployeeId,
                EmployeeName      = r.EmployeeName.Trim(),
                Title             = r.Title,
                Status            = r.Status,
                Weight            = r.Weight,
                ProgressPercent   = r.ProgressPercent,
                DueDate           = r.DueDate,
                SubmittedDate     = r.SubmittedDate,
                ApprovalDate      = r.ApprovalDate,
                DaysRemaining     = r.DueDate.DayNumber - today.DayNumber,
                // A locked goal is still running — a lock freezes what a goal is, not its year
                // (decision D-29) — so it can be overdue like any other.
                IsOverdue         = r.DueDate < today
                                    && r.Status != GoalStatus.Completed
                                    && r.Status != GoalStatus.Rejected,
                DaysPendingApproval = r.Status == GoalStatus.PendingApproval && r.SubmittedDate.HasValue
                    ? today.DayNumber - DateOnly.FromDateTime(r.SubmittedDate.Value).DayNumber
                    : 0,
                IsAtRisk          = risk.IsAtRisk,
                RiskReason        = risk.Reason,
                RiskSeverityScore = risk.SeverityScore,
            };
        }).ToList();
    }

    // =========================================================================
    //  Internal projection record (DB → memory bridge)
    // =========================================================================

    /// <summary>
    /// Scalar projection of an EmployeeGoal + Employee JOIN row for the org-wide query.
    /// Carries org-unit and org-level IDs for potential downstream grouping.
    /// </summary>
    private sealed class OrgRawGoalRow
    {
        public Guid     GoalId              { get; init; }
        public Guid     EmployeeId          { get; init; }
        public string   EmployeeName        { get; init; } = string.Empty;
        public string   Title               { get; init; } = string.Empty;
        public GoalStatus Status            { get; init; }
        public int      Weight              { get; init; }
        public decimal  ProgressPercent     { get; init; }
        /// <summary>Required by GoalRiskEvaluator Rule 2 (behind expected timeline).</summary>
        public DateOnly StartDate           { get; init; }
        public DateOnly DueDate             { get; init; }
        public DateTime? SubmittedDate      { get; init; }
        public DateTime? ApprovalDate       { get; init; }
        public Guid     OrganizationUnitId  { get; init; }
        public Guid     OrganizationLevelId { get; init; }
    }
}
