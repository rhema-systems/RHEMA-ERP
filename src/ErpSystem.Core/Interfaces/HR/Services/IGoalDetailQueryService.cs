using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR.Services;

// ─────────────────────────────────────────────────────────────────────────────
//  IGoalDetailQueryService
//  READ-ONLY query contract for loading the full detail of a single employee goal.
//
//  Architecture notes:
//  ─ This interface lives in the Core (domain) layer. Implementations may exist
//    both in the API backend (direct DB projection) and conceptually on the
//    Blazor frontend (via HTTP client adapter).
//
//  ─ The implementation MUST enforce:
//      (a) Manager-to-direct-report security: the currently authenticated user
//          must be the direct manager of the employee who owns the goal.
//      (b) Appraisal cycle validation: the goal must belong to an active or
//          viewable appraisal cycle for the requesting manager.
//
//  ─ Returns null when:
//      • The goal does not exist (404).
//      • The requesting user is not authorised to view it (403 / not a direct report).
//      • The goal belongs to a cycle outside the manager's current scope.
//
//  ─ Callers must treat null as "access denied or not found" and display the
//    appropriate UI state — do NOT expose 403 vs 404 distinction to the UI.
//
//  ─ NEVER loads:
//      • Navigation graph collections (progress entries, journal entries).
//      • Appraisal response or submission collections.
//    Only scalar projections are returned (see GoalDetailDto).
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Read-only query service for the Goal Detail Drawer.
/// Enforces manager-to-direct-report access control internally.
/// </summary>
public interface IGoalDetailQueryService
{
    /// <summary>
    /// Returns the full projection of a single goal for display in the
    /// Goal Detail Drawer, or <c>null</c> when not found / access denied.
    /// </summary>
    /// <param name="goalId">The unique identifier of the goal to load.</param>
    /// <param name="cancellationToken">Propagated from the calling component lifecycle.</param>
    /// <returns>
    /// A fully populated <see cref="GoalDetailDto"/> on success; <c>null</c> otherwise.
    /// </returns>
    Task<GoalDetailDto?> GetGoalDetailAsync(
        Guid              goalId,
        CancellationToken cancellationToken = default);
}
