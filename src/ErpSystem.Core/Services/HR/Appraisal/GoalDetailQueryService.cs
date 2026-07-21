using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Appraisal;

// ─────────────────────────────────────────────────────────────────────────────
//  GoalDetailQueryService
//  Read-only service providing the full detail projection for a single
//  EmployeeGoal, consumed by the Manager Goal Detail Drawer.
//
//  ── Security model ───────────────────────────────────────────────────────────
//  Access is permitted only when BOTH conditions hold:
//    1. The goal exists in the database (not soft-deleted).
//    2. The goal's employee is a direct report of the calling manager:
//         goal.Employee.ManagerId == currentManagerId
//
//  Security is enforced as a WHERE clause in the IQueryable composition,
//  never in-memory after materialisation. If either condition fails the
//  method returns null, giving no information about whether the goal exists
//  (intentional — avoids enumeration attacks).
//
//  ── Performance contract ─────────────────────────────────────────────────────
//  • AsNoTracking() — no change-tracking overhead for a read-only path.
//  • Single Select() projection — the entire DTO is built in one SQL query.
//  • NO Include() / JOIN-then-filter patterns.
//  • Correlated COUNT subqueries for ProgressEntries / JournalEntries:
//    EF Core translates these to EXISTS COUNT or correlated SELECT COUNT(*)
//    — all in the same query, no extra roundtrips.
//  • Navigation titles (ParentGoalTitle, GoalLibraryTitle, KpiDefinitionName,
//    SubmittedToManagerName) are resolved via nested Select() inside the
//    projection. EF Core emits LEFT JOINs for nullable FKs.
//  • Collections (ProgressEntries, JournalEntries) are NEVER loaded as
//    IEnumerable — only Count() and Max(date) are extracted.
//
//  ── Why not Include()? ───────────────────────────────────────────────────────
//  Include() forces EF Core to hydrate full entity graphs, loads every
//  column of every related table, and allocates tracked proxy objects that
//  immediately become garbage for a read-only query.  Select() projects
//  only the scalar fields actually needed by the DTO.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Production implementation of <see cref="IGoalDetailQueryService"/>.
/// Returns a fully populated <see cref="GoalDetailDto"/> for the Manager
/// Goal Detail Drawer, or <c>null</c> when the goal does not exist or the
/// calling manager is not authorised to view it.
/// </summary>
public sealed class GoalDetailQueryService : IGoalDetailQueryService
{
    // ── Dependencies ──────────────────────────────────────────────────────────

    private readonly IGenericRepository<EmployeeGoal>       _goalRepo;
    private readonly ICurrentUserService                    _currentUserService;
    private readonly ILogger<GoalDetailQueryService>        _logger;

    // IDateTimeProvider is injected rather than using DateTime.UtcNow directly
    // so that unit tests can inject a deterministic clock without mocking static state.
    // Not used for server-side overdue computation (the DTO ships DueDate + Status
    // so the UI can derive overdue locally), but retained for future use and logging.
    private readonly IDateTimeProvider _clock;

    public GoalDetailQueryService(
        IGenericRepository<EmployeeGoal>    goalRepo,
        ICurrentUserService                 currentUserService,
        IDateTimeProvider                   clock,
        ILogger<GoalDetailQueryService>     logger)
    {
        _goalRepo           = goalRepo;
        _currentUserService = currentUserService;
        _clock              = clock;
        _logger             = logger;
    }

    // =========================================================================
    //  Public API
    // =========================================================================

    /// <inheritdoc />
    public async Task<GoalDetailDto?> GetGoalDetailAsync(
        Guid              goalId,
        CancellationToken cancellationToken = default)
    {
        // ── 1. Resolve the calling manager ────────────────────────────────────
        // If the current user has no linked EmployeeId (e.g. a pure admin user)
        // we return null; they have no direct-report hierarchy to enforce against.
        var managerId = _currentUserService.EmployeeId;
        if (managerId is null || managerId == Guid.Empty)
        {
            _logger.LogWarning(
                "GoalDetailQueryService: current user {User} has no EmployeeId; returning null",
                _currentUserService.UserName ?? "unknown");
            return null;
        }

        _logger.LogDebug(
            "GoalDetailQueryService.GetGoalDetailAsync: goalId={GoalId}, managerId={ManagerId}",
            goalId, managerId);

        // ── 2. Single query: find goal + security check + full projection ──────
        //
        // The WHERE clause: g.Id == goalId AND g.Employee.ManagerId == managerId
        //   — enforces manager-to-direct-report security IN SQL, not in memory.
        //   — returns null for both "not found" and "found but not authorised",
        //     which is intentional (no information leak about goal existence).
        //
        // AsNoTracking(): this is a pure read path; change tracking is wasteful.
        //
        // Select() projection:
        //   — Only the columns needed by GoalDetailDto are fetched.
        //   — Navigation entity titles are resolved via nested null-coalescing
        //     sub-selects; EF Core emits LEFT JOINs.
        //   — Activity counts come from correlated COUNT subqueries, translated
        //     by EF Core to SQL COUNT(*) / MAX() correlated sub-selects.
        //     No collections are loaded as IEnumerable.

        var dto = await _goalRepo.GetQueryable()
            .AsNoTracking()
            .Where(g =>
                g.Id == goalId                       // goal must exist
                && !g.IsDeleted                      // soft-delete boundary
                && g.Employee.ManagerId == managerId // SECURITY: direct report only
            )
            .Select(g => new GoalDetailDto
            {
                // ── Core identity ────────────────────────────────────────────
                GoalId          = g.Id,
                Title           = g.Title,
                Description     = g.Description,
                SuccessCriteria = g.SuccessCriteria,
                Weight          = g.Weight,
                Priority        = g.Priority,
                Status          = g.Status,
                StartDate       = g.StartDate,
                DueDate         = g.DueDate,
                ProgressPercent = g.ProgressPercent,

                // ── Measurement (all nullable; block is omitted in UI if null) ─
                // The entity's MeasurementType is non-nullable (defaulted to
                // NumericAbsolute) but the DTO is nullable so the UI can hide
                // the section for goals that have no meaningful measurement config.
                // We surface null when TargetValue and MinValue and MaxValue are
                // all null — meaning no measurement has actually been configured —
                // rather than showing the default enum value.
                MeasurementType = (g.TargetValue.HasValue || g.MinValue.HasValue || g.MaxValue.HasValue)
                    ? (MeasurementType?)g.MeasurementType
                    : null,
                TargetValue     = g.TargetValue,
                MinValue        = g.MinValue,
                MaxValue        = g.MaxValue,
                Unit            = g.Unit,

                // ── Parent alignment — resolved title via nested null-safe selects ─
                // EF Core translates these to LEFT JOIN + ISNULL logic.
                // We never load full parent entities; we only project their Title.
                ParentType = g.ParentType,

                // Company goal title: populated when CompanyGoalId is set.
                ParentGoalTitle = g.ParentType == GoalParentType.Company
                    ? g.ParentCompanyGoal != null ? g.ParentCompanyGoal.Title : null
                    : g.ParentType == GoalParentType.Unit
                        ? g.ParentUnitGoal != null ? g.ParentUnitGoal.Title : null
                        : null,

                // Goal library title: populated when the goal was created from a library item.
                GoalLibraryTitle = g.LibraryItem != null ? g.LibraryItem.Title : null,

                // KPI definition name: populated when a KPI target is attached.
                KpiDefinitionName = g.KpiDefinition != null ? g.KpiDefinition.KpiName : null,

                // ── Workflow timeline ─────────────────────────────────────────
                SubmittedDate  = g.SubmittedDate,
                ApprovalDate   = g.ApprovalDate,
                ManagerFeedback = g.ManagerFeedback,
                IsLocked       = g.IsLocked,
                LockedDate     = g.LockedDate,

                // Manager name resolved via the Manager navigation property (SubmittedToManagerId FK).
                // EF Core emits LEFT JOIN Employees ON g.SubmittedToManagerId = m.Id.
                SubmittedToManagerName = g.Manager != null
                    ? g.Manager.FirstName + " " + g.Manager.LastName
                    : null,

                // ── Activity summary (counts only — no collections loaded) ─────
                //
                // Why Count() instead of loading collections:
                //   Loading ProgressEntries or JournalEntries as IEnumerable would
                //   bring potentially hundreds of rows across the wire just so we
                //   can call .Count() in C#. EF Core translates these LINQ Count()
                //   calls to correlated SELECT COUNT(*) sub-queries, keeping the
                //   data transfer minimal.
                //
                // GoalProgressEntry → EmployeeGoal.ProgressEntries (back-navigation via EmployeeGoalId)
                ProgressEntryCount = g.ProgressEntries.Count(p => !p.IsDeleted),

                // LastProgressUpdateDate: the most recent EntryDate across all
                // progress entries for this goal. EF Core emits a correlated
                // MAX(). Returns null (default) when no progress entries exist.
                LastProgressUpdateDate = g.ProgressEntries
                    .Where(p => !p.IsDeleted)
                    .OrderByDescending(p => p.EntryDate)
                    .Select(p => (DateTime?)p.EntryDate)
                    .FirstOrDefault(),

                // PerformanceJournalEntry → linked via RelatedGoalId (back-nav JournalEntries)
                JournalEntryCount = g.JournalEntries.Count(j => !j.IsDeleted),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (dto is null)
        {
            // Could be not found OR access denied — intentionally indistinguishable.
            _logger.LogInformation(
                "GoalDetailQueryService: goalId={GoalId} not found or not accessible by managerId={ManagerId}",
                goalId, managerId);
        }

        return dto;
    }

    // =========================================================================
    //  Private helpers
    // =========================================================================

    // No private helpers required — the single query does all the work.
    // Helper methods should NOT be added here to avoid the temptation of
    // splitting the projection into per-section calls (which would reintroduce
    // N+1 round-trips).
}
