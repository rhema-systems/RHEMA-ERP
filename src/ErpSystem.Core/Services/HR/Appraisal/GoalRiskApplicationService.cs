using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Appraisal;

// ─────────────────────────────────────────────────────────────────────────────
//  GoalRiskService
//
//  Orchestrates the risk evaluation pipeline for a full appraisal cycle:
//
//    1. Load goals from the database (materialised as flat projections).
//    2. Load the active GoalRiskSetting ONCE — never per goal.
//    3. Evaluate each goal in memory using IGoalRiskEvaluator.
//    4. Return the enriched GoalWithRiskDto list as the API/query result.
//
//  ── Key design constraints ─────────────────────────────────────────────────
//  • Risk is NEVER computed inside the EF IQueryable.  Doing so would push
//    business logic into the database engine, bypass the configurable threshold
//    abstraction, and make the rules untestable without a database.
//
//  • The select projection extracts only the scalar fields required by the
//    evaluator.  No navigation properties are Include()d, preventing
//    accidental entity graph hydration.
//
//  • All goals in a single call share the same utcNow snapshot, ensuring
//    consistent results even if the query spans a midnight boundary.
//
//  ── Security model ─────────────────────────────────────────────────────────
//  Results are scoped to the calling manager's direct reports via
//  goal.Employee.ManagerId == managerId.  This check is pushed into the
//  WHERE clause (IQueryable level), never applied in-memory post-fetch.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Application-layer service that composes goal data with configurable risk
/// evaluation, returning <see cref="GoalWithRiskDto"/> projections.
/// </summary>
public sealed class GoalRiskService
{
    private readonly IGenericRepository<EmployeeGoal>       _goalRepo;
    private readonly IGoalRiskSettingsProvider              _settingsProvider;
    private readonly IGoalRiskEvaluator                     _evaluator;
    private readonly ICurrentUserService                    _currentUser;
    private readonly IDateTimeProvider                      _clock;
    private readonly ILogger<GoalRiskService>    _logger;

    public GoalRiskService(
        IGenericRepository<EmployeeGoal>    goalRepo,
        IGoalRiskSettingsProvider           settingsProvider,
        IGoalRiskEvaluator                  evaluator,
        ICurrentUserService                 currentUser,
        IDateTimeProvider                   clock,
        ILogger<GoalRiskService> logger)
    {
        _goalRepo         = goalRepo;
        _settingsProvider = settingsProvider;
        _evaluator        = evaluator;
        _currentUser      = currentUser;
        _clock            = clock;
        _logger           = logger;
    }

    // =========================================================================
    //  Public API
    // =========================================================================

    /// <summary>
    /// Returns all goals for the calling manager's direct reports within
    /// <paramref name="cycleId"/>, enriched with a risk evaluation result.
    /// </summary>
    /// <param name="cycleId">The appraisal cycle to scope the query.</param>
    /// <param name="cancellationToken">Propagates cancellation.</param>
    /// <returns>
    /// A list of <see cref="GoalWithRiskDto"/> — one entry per goal.
    /// At-risk goals will have <c>IsAtRisk == true</c> and a non-null
    /// <c>RiskReason</c>.  Returns an empty list when the current user has
    /// no associated EmployeeId.
    /// </returns>
    public async Task<List<GoalWithRiskDto>> GetGoalsWithRiskAsync(
        Guid              cycleId,
        CancellationToken cancellationToken = default)
    {
        // ── 1. Resolve calling manager ─────────────────────────────────────
        var managerId = _currentUser.EmployeeId;
        if (managerId is null || managerId == Guid.Empty)
        {
            _logger.LogWarning(
                "GoalRiskService: current user '{User}' has no linked EmployeeId; " +
                "returning empty result.",
                _currentUser.UserName ?? "(unknown)");
            return [];
        }

        // ── 2. Materialise flat goal projections for this cycle ────────────
        // Select() projects only the scalar fields needed by the evaluator.
        // AsNoTracking() is required — these objects are never updated.
        // The ManagerId WHERE clause is applied at IQueryable level (security gate).
        var goalRows = await _goalRepo
            .GetQueryable()
            .AsNoTracking()
            .Where(g => !g.IsDeleted
                     && g.AppraisalCycleId == cycleId
                     && g.Employee.ManagerId == managerId)
            .Select(g => new
            {
                g.Id,
                g.Title,
                g.Status,
                g.ProgressPercent,
                g.DueDate,
                g.StartDate,
                EmployeeName = g.Employee.FirstName + " " + g.Employee.LastName
            })
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "GoalRiskService: loaded {Count} goals for manager {ManagerId} in cycle {CycleId}",
            goalRows.Count, managerId, cycleId);

        if (goalRows.Count == 0)
            return [];

        // ── 3. Load active settings ONCE ──────────────────────────────────
        // Throws GoalRiskSettingNotFoundException if no active record exists.
        var settings = await _settingsProvider.GetActiveAsync(cancellationToken);

        // ── 4. Evaluate risk in memory after materialisation ─────────────
        // A single utcNow snapshot is used for the entire batch so that all
        // goals are evaluated at the same reference point in time.
        var utcNow = _clock.UtcNow;

        return goalRows.Select(row =>
        {
            // Lightweight entity shell — only the fields the evaluator needs.
            // No navigation properties; no DbContext tracking.
            var goal = new EmployeeGoal
            {
                Id              = row.Id,
                Title           = row.Title,
                Status          = row.Status,
                ProgressPercent = row.ProgressPercent,
                DueDate         = row.DueDate,
                StartDate       = row.StartDate
            };

            var risk = _evaluator.Evaluate(goal, settings, utcNow);

            return new GoalWithRiskDto
            {
                GoalId            = row.Id,
                Title             = row.Title,
                EmployeeName      = row.EmployeeName,
                ProgressPercent   = row.ProgressPercent,
                DueDate           = row.DueDate,
                IsAtRisk          = risk.IsAtRisk,
                RiskReason        = risk.Reason,
                RiskSeverityScore = risk.SeverityScore
            };
        }).ToList();
    }
}
