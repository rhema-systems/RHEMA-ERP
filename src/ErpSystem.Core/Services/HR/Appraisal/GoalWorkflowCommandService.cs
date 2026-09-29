using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Appraisal;

// ─────────────────────────────────────────────────────────────────────────────
//  GoalWorkflowCommandService
//
//  Write-side CQRS service for EmployeeGoal workflow state transitions.
//
//  ── Responsibility boundary ───────────────────────────────────────────────────
//  This service owns ONLY the approval lifecycle:
//    Draft  →  PendingApproval  →  Approved   (then locked: IsLocked, not a status)
//          ↖_____Rejected_____↗       │
//                  ↖──── sent back ───┘  (an approved goal, not locked, not completed)
//
//  Execution-status updates (InProgress, OnTrack, AtRisk, Completed) are set
//  by progress-tracking logic in EmployeeGoalService and must never be touched
//  here.  That would violate the CQRS principle of a focused command handler.
//
//  A lock freezes what the goal IS — title, measure, target, weight, owner — and
//  not its year (performance closure decision D-29): the lock used to set
//  Status = Locked, which every progress path reads as "finished", so a goal set
//  locked at the goal-setting deadline would have stopped moving in February.
//  The lock is IsLocked + LockedDate; a goal left in the Locked status by the
//  old lock still counts as locked (GoalSetRules.IsLocked).
//
//  ── Security model ────────────────────────────────────────────────────────────
//  Manager-gated commands (Approve, Reject, Lock):
//    1. Resolve the calling manager from ICurrentUserService.EmployeeId.
//    2. Load the goal with its Employee navigation so we can check
//       goal.Employee.ManagerId == currentManagerId.
//    3. If either the goal does not exist or the security check fails,
//       throw GoalWorkflowException(GoalNotFound | UnauthorizedAccess).
//
//  Employee-gated commands (Submit):
//    1. Resolve the calling employee from ICurrentUserService.EmployeeId.
//    2. Verify goal.EmployeeId == currentEmployeeId (employee owns the goal).
//    3. The submission target manager is derived from goal.Employee.ManagerId
//       automatically — the client never provides a managerId.
//
//  ── Transition table (authoritative) ─────────────────────────────────────────
//  ┌──────────────┬──────────────────────────┬─────────────────────────────────┐
//  │ Command      │ Allowed source status(es) │ Target status                   │
//  ├──────────────┼──────────────────────────┼─────────────────────────────────┤
//  │ Submit       │ Draft, Rejected           │ PendingApproval                 │
//  │ Approve      │ PendingApproval           │ Approved                        │
//  │ Reject       │ PendingApproval, Approved,│ Rejected (sent back for changes │
//  │              │ InProgress, OnTrack,      │ when it was approved — D-30)    │
//  │              │ AtRisk — never locked     │                                 │
//  │ Lock         │ Approved, InProgress,     │ status unchanged; IsLocked =    │
//  │              │ AtRisk, OnTrack, Completed│ true, LockedDate (D-29)         │
//  │ Lock set     │ every live goal agreed,   │ each unlocked live goal locked  │
//  │              │ count and weights valid   │                                 │
//  └──────────────┴──────────────────────────┴─────────────────────────────────┘
//
//  ── Unit of work ─────────────────────────────────────────────────────────────
//  Each public method follows the same pattern:
//    1. Load entity (include Employee nav).
//    2. Validate security + business rules (throw before touching the entity).
//    3. Apply the mutation.
//    4. Update the repository.
//    5. SaveChangesAsync once — EF Core wraps this in an implicit transaction.
//  A single SaveChangesAsync per operation is intentional; it's equivalent to
//  an explicit BEGIN/COMMIT around one entity change and avoids partial saves.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Production implementation of <see cref="IGoalWorkflowCommandService"/>.
/// </summary>
public sealed class GoalWorkflowCommandService : IGoalWorkflowCommandService
{
    // ── Dependencies ──────────────────────────────────────────────────────────

    private readonly IGenericRepository<EmployeeGoal> _goalRepo;
    private readonly IGenericRepository<Employee>     _employeeRepo;
    private readonly IGenericRepository<AppraisalCycle> _cycleRepo;
    private readonly IAppraisalGoalRowService         _goalRows;
    private readonly IUnitOfWork                      _unitOfWork;
    private readonly ICurrentUserService              _currentUserService;
    private readonly ICurrentUserProvider             _currentUserProvider;
    private readonly IDateTimeProvider                _clock;
    private readonly ILogger<GoalWorkflowCommandService> _logger;

    public GoalWorkflowCommandService(
        IGenericRepository<EmployeeGoal>      goalRepo,
        IGenericRepository<Employee>          employeeRepo,
        IGenericRepository<AppraisalCycle>    cycleRepo,
        IAppraisalGoalRowService              goalRows,
        IUnitOfWork                           unitOfWork,
        ICurrentUserService                   currentUserService,
        ICurrentUserProvider                  currentUserProvider,
        IDateTimeProvider                     clock,
        ILogger<GoalWorkflowCommandService>   logger)
    {
        _goalRepo           = goalRepo;
        _employeeRepo       = employeeRepo;
        _cycleRepo          = cycleRepo;
        _goalRows           = goalRows;
        _unitOfWork         = unitOfWork;
        _currentUserService = currentUserService;
        _currentUserProvider = currentUserProvider;
        _clock              = clock;
        _logger             = logger;
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

    // ─────────────────────────────────────────────────────────────────────────
    //  Set of statuses from which a Lock is permitted.
    //  Explicit allowlist beats a fragile enum-value numeric comparison.
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly HashSet<GoalStatus> LockableStatuses = new()
    {
        GoalStatus.Approved,
        GoalStatus.InProgress,
        GoalStatus.AtRisk,
        GoalStatus.OnTrack,
        GoalStatus.Completed,
    };

    // =========================================================================
    //  Public API
    // =========================================================================

    // ── Transition 1: Submit ─────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task SubmitGoalAsync(
        Guid              goalId,
        CancellationToken cancellationToken = default)
    {
        // ── Resolve caller (employee submitting their own goal) ────────────
        var employeeId = ResolveCurrentEmployeeId();

        var goal = await LoadGoalWithEmployeeAsync(goalId, cancellationToken)
                   ?? throw new GoalWorkflowException(
                          GoalWorkflowFailureReason.GoalNotFound,
                          $"Goal {goalId} was not found.");

        // ── Security: only the goal's owner may submit ───────────────────
        if (goal.EmployeeId != employeeId)
            throw new GoalWorkflowException(
                GoalWorkflowFailureReason.UnauthorizedAccess,
                "You can only submit goals that belong to you.");

        // ── Invariant checks ─────────────────────────────────────────────
        EnsureNotLocked(goal);

        // Submission target: derived from the employee's manager assignment.
        // Client must NOT provide this — it is read from the HR record to
        // prevent an employee from submitting to an arbitrary manager id.
        if (!goal.Employee.ManagerId.HasValue)
            throw new GoalWorkflowException(
                GoalWorkflowFailureReason.InvalidTransition,
                "Cannot submit goal: the employee does not have an assigned manager.");

        // ── Status check ─────────────────────────────────────────────────
        switch (goal.Status)
        {
            case GoalStatus.Draft:
            case GoalStatus.Rejected:
                // Both are valid source states for submission.
                break;

            case GoalStatus.PendingApproval:
                throw new GoalWorkflowException(
                    GoalWorkflowFailureReason.InvalidTransition,
                    "Goal is already awaiting approval.");

            case GoalStatus.Approved:
            case GoalStatus.InProgress:
            case GoalStatus.OnTrack:
            case GoalStatus.AtRisk:
            case GoalStatus.Completed:
                throw new GoalWorkflowException(
                    GoalWorkflowFailureReason.InvalidTransition,
                    $"Goal cannot be re-submitted once it has reached status '{goal.Status}'.");

            case GoalStatus.Locked:
                // Handled by EnsureNotLocked above, but present for compiler completeness.
                throw new GoalWorkflowException(
                    GoalWorkflowFailureReason.GoalLocked,
                    "Locked goals cannot be submitted.");

            default:
                throw new GoalWorkflowException(
                    GoalWorkflowFailureReason.InvalidTransition,
                    $"Unrecognised goal status '{goal.Status}'.");
        }

        // ── Apply mutation ───────────────────────────────────────────────
        goal.Status               = GoalStatus.PendingApproval;
        goal.SubmittedToManagerId = goal.Employee.ManagerId;
        goal.SubmittedDate        = _clock.UtcNow;

        await _goalRepo.UpdateAsync(goal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Goal {GoalId} submitted for approval by employee {EmployeeId}; " +
            "routed to manager {ManagerId}",
            goalId, employeeId, goal.SubmittedToManagerId);
    }

    // ── Transition 2: Approve ────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task ApproveGoalAsync(
        Guid              goalId,
        string?           feedback          = null,
        CancellationToken cancellationToken = default)
    {
        // ── Resolve caller (manager performing the approval) ──────────────
        var managerId = ResolveCurrentManagerId();

        var goal = await LoadGoalWithEmployeeAsync(goalId, cancellationToken)
                   ?? throw new GoalWorkflowException(
                          GoalWorkflowFailureReason.GoalNotFound,
                          $"Goal {goalId} was not found.");

        // ── Security: caller must be the employee's direct manager ────────
        ValidateManagerAccess(goal, managerId);

        // ── Invariant checks ─────────────────────────────────────────────
        EnsureNotLocked(goal);

        // ── Status check ─────────────────────────────────────────────────
        // Only PendingApproval → Approved is a valid approval transition.
        // All other statuses are explicitly called out so the caller receives
        // a meaningful failure reason rather than a catch-all message.
        switch (goal.Status)
        {
            case GoalStatus.PendingApproval:
                // Valid — proceed.
                break;

            case GoalStatus.Draft:
                throw new GoalWorkflowException(
                    GoalWorkflowFailureReason.InvalidTransition,
                    "A Draft goal must be submitted before it can be approved.");

            case GoalStatus.Rejected:
                throw new GoalWorkflowException(
                    GoalWorkflowFailureReason.InvalidTransition,
                    "A Rejected goal must be re-submitted by the employee before it can be approved.");

            case GoalStatus.Approved:
                throw new GoalWorkflowException(
                    GoalWorkflowFailureReason.InvalidTransition,
                    "Goal is already approved.");

            case GoalStatus.InProgress:
            case GoalStatus.OnTrack:
            case GoalStatus.AtRisk:
            case GoalStatus.Completed:
                throw new GoalWorkflowException(
                    GoalWorkflowFailureReason.InvalidTransition,
                    $"Goal is already in execution status '{goal.Status}' and cannot be re-approved.");

            case GoalStatus.Locked:
                // Should have been caught by EnsureNotLocked, but included for completeness.
                throw new GoalWorkflowException(
                    GoalWorkflowFailureReason.GoalLocked,
                    "Locked goals cannot be approved.");

            default:
                throw new GoalWorkflowException(
                    GoalWorkflowFailureReason.InvalidTransition,
                    $"Unrecognised goal status '{goal.Status}'.");
        }

        // ── Apply mutation ───────────────────────────────────────────────
        // Execution status (InProgress / OnTrack etc.) is NOT set here.
        // That is the responsibility of the progress-tracking layer once the
        // employee begins recording progress entries.
        goal.Status       = GoalStatus.Approved;
        goal.ApprovalDate = _clock.UtcNow;

        if (!string.IsNullOrWhiteSpace(feedback))
            goal.ManagerFeedback = feedback.Trim();

        await _goalRepo.UpdateAsync(goal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Goal {GoalId} approved by manager {ManagerId}",
            goalId, managerId);
    }

    // ── Transition 3: Reject ─────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task RejectGoalAsync(
        Guid              goalId,
        string            feedback,
        CancellationToken cancellationToken = default)
    {
        // ── Feedback guard — validate before any DB work ──────────────────
        // Rejection without explanation is a domain rule violation: the employee
        // must receive actionable guidance on what to correct when re-submitting.
        if (string.IsNullOrWhiteSpace(feedback))
            throw new GoalWorkflowException(
                GoalWorkflowFailureReason.MissingFeedback,
                "Rejection feedback is required. Provide a clear reason so the employee can act on it.");

        // ── Resolve caller (manager performing the rejection) ─────────────
        var managerId = ResolveCurrentManagerId();

        var goal = await LoadGoalWithEmployeeAsync(goalId, cancellationToken)
                   ?? throw new GoalWorkflowException(
                          GoalWorkflowFailureReason.GoalNotFound,
                          $"Goal {goalId} was not found.");

        // ── Security: caller must be the employee's direct manager ────────
        ValidateManagerAccess(goal, managerId);

        // ── Invariant checks ─────────────────────────────────────────────
        EnsureNotLocked(goal);

        // ── Status check ─────────────────────────────────────────────────
        switch (goal.Status)
        {
            case GoalStatus.PendingApproval:
                // Valid — proceed.
                break;

            // Decision D-30: what an approved goal measures cannot be edited, so the way to
            // change it is for the manager to send it back. It keeps its progress entries; once
            // re-approved, the next entry sets its execution status again.
            case GoalStatus.Approved:
            case GoalStatus.InProgress:
            case GoalStatus.OnTrack:
            case GoalStatus.AtRisk:
                break;

            case GoalStatus.Draft:
                throw new GoalWorkflowException(
                    GoalWorkflowFailureReason.InvalidTransition,
                    "A Draft goal must be submitted before it can be rejected.");

            case GoalStatus.Rejected:
                throw new GoalWorkflowException(
                    GoalWorkflowFailureReason.InvalidTransition,
                    "Goal is already in Rejected status.");

            case GoalStatus.Completed:
                throw new GoalWorkflowException(
                    GoalWorkflowFailureReason.InvalidTransition,
                    "A completed goal's result stands; it cannot be sent back for changes.");

            case GoalStatus.Locked:
                throw new GoalWorkflowException(
                    GoalWorkflowFailureReason.GoalLocked,
                    "Locked goals cannot be rejected.");

            default:
                throw new GoalWorkflowException(
                    GoalWorkflowFailureReason.InvalidTransition,
                    $"Unrecognised goal status '{goal.Status}'.");
        }

        // ── Apply mutation ───────────────────────────────────────────────
        goal.Status          = GoalStatus.Rejected;
        goal.ManagerFeedback = feedback.Trim();
        // Clear the approval date: rejection means no approval has been granted.
        goal.ApprovalDate    = null;

        await _goalRepo.UpdateAsync(goal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Goal {GoalId} rejected by manager {ManagerId}",
            goalId, managerId);
    }

    // ── Transition 4: Lock ───────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task LockGoalAsync(
        Guid              goalId,
        CancellationToken cancellationToken = default)
    {
        // ── Resolve caller (manager performing the lock) ──────────────────
        var managerId = ResolveCurrentManagerId();

        var goal = await LoadGoalWithEmployeeAsync(goalId, cancellationToken)
                   ?? throw new GoalWorkflowException(
                          GoalWorkflowFailureReason.GoalNotFound,
                          $"Goal {goalId} was not found.");

        // ── Security: caller must be the employee's direct manager ────────
        ValidateManagerAccess(goal, managerId);

        // ── Invariant checks ─────────────────────────────────────────────
        // EnsureNotLocked also distinguishes the "already locked" case from
        // a normal forbidden-transition case, giving a clearer message.
        EnsureNotLocked(goal);

        // ── Status check ─────────────────────────────────────────────────
        // Lock is only permitted on goals that have been approved or that
        // are already in active execution.  Draft / PendingApproval / Rejected
        // goals have not undergone managerial sign-off and must not be locked.
        if (!LockableStatuses.Contains(goal.Status))
        {
            var forbidden = goal.Status switch
            {
                GoalStatus.Draft           => "Draft goals must be approved before they can be locked.",
                GoalStatus.PendingApproval => "Goals awaiting approval must be approved before they can be locked.",
                GoalStatus.Rejected        => "Rejected goals cannot be locked. They must be re-submitted and approved first.",
                _                          => $"Goals with status '{goal.Status}' cannot be locked.",
            };
            throw new GoalWorkflowException(GoalWorkflowFailureReason.InvalidTransition, forbidden);
        }

        // ── Apply mutation ───────────────────────────────────────────────
        // The status is left alone (D-29): a lock freezes what the goal is, and its year runs
        // on — progress entries, check-ins and interim reviews keep moving it.
        goal.IsLocked   = true;
        goal.LockedDate = _clock.UtcNow;

        await _goalRepo.UpdateAsync(goal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The appraisal's goals section follows the locked set (L2).
        await _goalRows.RebuildAsync(goal.EmployeeId, goal.AppraisalCycleId, cancellationToken);

        _logger.LogInformation(
            "Goal {GoalId} locked by manager {ManagerId}",
            goalId, managerId);
    }

    // ── Lock the goal set ────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<GoalSetLockResult> LockGoalSetAsync(
        Guid              employeeId,
        Guid              appraisalCycleId,
        CancellationToken cancellationToken = default)
    {
        var managerId = ResolveCurrentManagerId();
        var tenantId  = GetTenantId();

        // ── Security: the direct manager, read from the employee record ──
        // Checked before anything about the set is read, so the refusal says nothing about
        // whether a colleague has goals.
        var employee = await _employeeRepo.GetQueryable()
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.TenantId == tenantId, cancellationToken)
            ?? throw new GoalWorkflowException(
                   GoalWorkflowFailureReason.GoalNotFound,
                   $"Employee {employeeId} was not found.");

        if (employee.ManagerId != managerId)
            throw new GoalWorkflowException(
                GoalWorkflowFailureReason.UnauthorizedAccess,
                "You are not the direct manager of this employee and cannot lock their goal set.");

        var cycle = await _cycleRepo.GetQueryable()
            .Include(c => c.AppraisalSettings)
            .FirstOrDefaultAsync(c => c.Id == appraisalCycleId && c.TenantId == tenantId, cancellationToken)
            ?? throw new GoalWorkflowException(
                   GoalWorkflowFailureReason.GoalNotFound,
                   $"Appraisal cycle {appraisalCycleId} was not found.");

        var goals = await _goalRepo.GetQueryable()
            .Where(g => g.TenantId == tenantId
                     && !g.IsDeleted
                     && g.EmployeeId == employeeId
                     && g.AppraisalCycleId == appraisalCycleId)
            .ToListAsync(cancellationToken);

        // ── Governance at lock (L5): the rules the goal-setting gate reads ─
        var settings = cycle.AppraisalSettings;
        var blocker = GoalSetRules.LockBlocker(
            goals.Select(g => (g.Status, g.Weight)).ToList(),
            settings?.MinGoalsPerEmployee,
            settings?.MaxGoalsPerEmployee);

        if (blocker != null)
            throw new GoalWorkflowException(
                GoalWorkflowFailureReason.InvalidTransition,
                $"The goal set cannot be locked yet: {blocker}.");

        var live   = goals.Where(g => GoalSetRules.IsLive(g.Status)).ToList();
        var toLock = live.Where(g => !GoalSetRules.IsLocked(g.IsLocked, g.Status)).ToList();

        if (toLock.Count == 0)
            throw new GoalWorkflowException(
                GoalWorkflowFailureReason.GoalLocked,
                "The goal set is already locked.");

        // ── Apply mutation: the flag only, as for one goal (D-29) ───────
        var now = _clock.UtcNow;
        foreach (var goal in toLock)
        {
            goal.IsLocked   = true;
            goal.LockedDate = now;
            await _goalRepo.UpdateAsync(goal);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The appraisal's goals section: one row per locked goal (L2).
        await _goalRows.RebuildAsync(employeeId, appraisalCycleId, cancellationToken);

        _logger.LogInformation(
            "Goal set of employee {EmployeeId} for cycle {CycleId} locked by manager {ManagerId}: {Locked} of {InSet} goal(s)",
            employeeId, appraisalCycleId, managerId, toLock.Count, live.Count);

        return new GoalSetLockResult(employeeId, appraisalCycleId, toLock.Count, live.Count);
    }

    // =========================================================================
    //  Private helpers
    // =========================================================================

    // ── Entity loading ────────────────────────────────────────────────────────

    /// <summary>
    /// Loads an <see cref="EmployeeGoal"/> by primary key, eager-loading the
    /// <c>Employee</c> navigation property required for manager-access checks.
    /// Excludes soft-deleted records.
    /// </summary>
    private Task<EmployeeGoal?> LoadGoalWithEmployeeAsync(
        Guid              goalId,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        return _goalRepo.GetQueryable()
               .Include(g => g.Employee)
               .FirstOrDefaultAsync(
                   g => g.Id == goalId && !g.IsDeleted && g.TenantId == tenantId,
                   cancellationToken);
    }

    // ── Current-user resolution ───────────────────────────────────────────────

    /// <summary>
    /// Returns the EmployeeId of the currently authenticated manager.
    /// Throws <see cref="GoalWorkflowException"/> if the user is not linked
    /// to an employee record — pure application/admin users have no manager
    /// hierarchy and are not permitted to perform manager-gated commands.
    /// </summary>
    private Guid ResolveCurrentManagerId()
    {
        var id = _currentUserService.EmployeeId;
        if (!id.HasValue || id.Value == Guid.Empty)
            throw new GoalWorkflowException(
                GoalWorkflowFailureReason.UnauthorizedAccess,
                "The current user is not linked to an employee record and cannot perform manager actions.");
        return id.Value;
    }

    /// <summary>
    /// Returns the EmployeeId of the currently authenticated employee.
    /// Throws <see cref="GoalWorkflowException"/> if the user is not linked
    /// to an employee record.
    /// </summary>
    private Guid ResolveCurrentEmployeeId()
    {
        var id = _currentUserService.EmployeeId;
        if (!id.HasValue || id.Value == Guid.Empty)
            throw new GoalWorkflowException(
                GoalWorkflowFailureReason.UnauthorizedAccess,
                "The current user is not linked to an employee record and cannot submit goals.");
        return id.Value;
    }

    // ── Security guards ───────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the calling manager is the direct manager of the goal's employee.
    ///
    /// The check <c>goal.Employee.ManagerId == callerId</c> is the single source
    /// of truth for manager-to-direct-report authorisation in this service.
    /// Client-supplied manager IDs are never trusted.
    ///
    /// Note: the <c>Employee</c> navigation must have been loaded via
    /// <see cref="LoadGoalWithEmployeeAsync"/> before calling this method.
    /// </summary>
    private static void ValidateManagerAccess(EmployeeGoal goal, Guid callerId)
    {
        if (goal.Employee.ManagerId != callerId)
            throw new GoalWorkflowException(
                GoalWorkflowFailureReason.UnauthorizedAccess,
                "You are not the direct manager of this employee and cannot perform workflow actions on their goals.");
    }

    /// <summary>
    /// Ensures the goal has not been locked.
    ///
    /// A locked goal is immutable as far as the approval workflow is concerned.
    /// This guard is checked before the status-specific switch, so the caller
    /// always receives a "goal is locked" message rather than a misleading
    /// "invalid transition" message.
    /// </summary>
    private static void EnsureNotLocked(EmployeeGoal goal)
    {
        if (goal.IsLocked || goal.Status == GoalStatus.Locked)
            throw new GoalWorkflowException(
                GoalWorkflowFailureReason.GoalLocked,
                "This goal is locked. Locked goals cannot be submitted, approved, or rejected.");
    }
}
