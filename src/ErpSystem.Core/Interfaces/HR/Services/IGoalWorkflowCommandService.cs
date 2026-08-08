namespace ErpSystem.Core.Interfaces.HR.Services;

// ─────────────────────────────────────────────────────────────────────────────
//  IGoalWorkflowCommandService
//
//  Write-side CQRS contract for EmployeeGoal workflow state transitions.
//
//  ── What belongs here ────────────────────────────────────────────────────────
//  Workflow transitions that advance (or reverse) an EmployeeGoal through
//  its approval lifecycle:  Draft → PendingApproval → Approved → Locked.
//
//  ── What does NOT belong here ────────────────────────────────────────────────
//  • Execution-status updates (InProgress, OnTrack, AtRisk, Completed) — those
//    live in the progress-tracking layer (IEmployeeGoalService / CheckInService).
//  • Query / read operations — handled by ITeamGoalsQueryService / IGoalDetailQueryService.
//  • CRUD mutations (create/update/delete) — handled by IEmployeeGoalService.
//
//  ── Security contract ────────────────────────────────────────────────────────
//  All manager-gated methods (Approve, Reject, Lock) resolve the calling manager
//  from ICurrentUserService internally.  Callers must NOT pass a managerId.
//  The service enforces goal.Employee.ManagerId == currentManagerId at the DB
//  query layer before any mutation is applied.
//
//  ── Error contract ───────────────────────────────────────────────────────────
//  All methods throw <see cref="ErpSystem.Core.Exceptions.GoalWorkflowException"/>
//  for domain-rule violations.  The exception carries a typed Reason property
//  so the API controller can return the appropriate HTTP status code without
//  parsing message strings.
//
//  ── CancellationToken ────────────────────────────────────────────────────────
//  Every method accepts an optional CancellationToken so the API layer can
//  propagate the request-abort token (HttpContext.RequestAborted) cleanly.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Write-side CQRS service for EmployeeGoal workflow transitions.
/// All mutations are async, transactional, and security-enforced.
/// </summary>
public interface IGoalWorkflowCommandService
{
    // ── Transition 1: Submit (employee action) ────────────────────────────────

    /// <summary>
    /// Transitions a goal from <c>Draft</c> or <c>Rejected</c> to
    /// <c>PendingApproval</c> and sets <c>SubmittedDate</c>.
    ///
    /// The manager to submit to is resolved automatically from
    /// <c>goal.Employee.ManagerId</c> — callers must not supply a manager ID.
    /// </summary>
    /// <exception cref="ErpSystem.Core.Exceptions.GoalWorkflowException">
    ///   Thrown when the goal is not found, already locked, or the current
    ///   status does not allow submission.
    /// </exception>
    Task SubmitGoalAsync(Guid goalId, CancellationToken cancellationToken = default);

    // ── Transition 2: Approve (manager action) ───────────────────────────────

    /// <summary>
    /// Transitions a goal from <c>PendingApproval</c> to <c>Approved</c>
    /// and sets <c>ApprovalDate</c>.  The calling manager must be the
    /// direct manager of the goal's employee.
    /// </summary>
    /// <param name="feedback">Optional manager comment stored on the goal record.</param>
    /// <exception cref="ErpSystem.Core.Exceptions.GoalWorkflowException">
    ///   Thrown when the goal is not found, locked, not in PendingApproval,
    ///   or the caller is not the employee's direct manager.
    /// </exception>
    Task ApproveGoalAsync(Guid goalId, string? feedback = null, CancellationToken cancellationToken = default);

    // ── Transition 3: Reject (manager action) ────────────────────────────────

    /// <summary>
    /// Transitions a goal from <c>PendingApproval</c> to <c>Rejected</c>,
    /// stores manager feedback, and clears <c>ApprovalDate</c>.  The calling
    /// manager must be the direct manager of the goal's employee.
    /// Non-empty feedback is required — rejection without explanation is a
    /// domain rule violation.
    /// </summary>
    /// <param name="feedback">Mandatory rejection reason (non-null, non-whitespace).</param>
    /// <exception cref="ErpSystem.Core.Exceptions.GoalWorkflowException">
    ///   Thrown when the goal is not found, locked, not in PendingApproval,
    ///   the caller is not the employee's direct manager, or feedback is empty.
    /// </exception>
    Task RejectGoalAsync(Guid goalId, string feedback, CancellationToken cancellationToken = default);

    // ── Transition 4: Lock (manager action) ──────────────────────────────────

    /// <summary>
    /// Locks an approved or in-execution goal, setting <c>IsLocked = true</c>,
    /// <c>Status = Locked</c>, and <c>LockedDate</c>.  Once locked, no further
    /// approval or rejection transitions are permitted.
    /// </summary>
    /// <exception cref="ErpSystem.Core.Exceptions.GoalWorkflowException">
    ///   Thrown when the goal is not found, already locked, not in a lockable
    ///   status (Approved / InProgress / AtRisk / OnTrack / Completed), or the
    ///   caller is not the employee's direct manager.
    /// </exception>
    Task LockGoalAsync(Guid goalId, CancellationToken cancellationToken = default);
}
