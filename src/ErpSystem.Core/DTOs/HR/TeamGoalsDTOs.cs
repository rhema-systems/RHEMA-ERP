using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ─────────────────────────────────────────────────────────────────────────────
//  Team Goals Query DTOs
//  These are READ-ONLY projection DTOs used exclusively by ITeamGoalsQueryService.
//  They contain NO mutation fields and carry NO EF navigation properties.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Aggregated governance snapshot for a single direct report within an appraisal cycle.
/// Feeds the Overview tab of the Advanced Manager Workspace.
///
/// Governance vs Execution separation:
///   - Workflow states  (Draft / PendingApproval / Approved / Rejected / Locked)
///     determine structural completeness.
///   - Execution states (InProgress / OnTrack / AtRisk / Completed)
///     represent progress but do NOT block StructurallyComplete.
/// </summary>
public sealed class TeamMemberOverviewDto
{
    // ── Identity ─────────────────────────────────────────────────────────────
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;

    // ── Workflow-state counts (structural governance) ─────────────────────────
    public int TotalGoals { get; init; }
    public int DraftCount { get; init; }
    public int PendingApprovalCount { get; init; }

    /// <summary>
    /// Goals that have passed approval workflow:
    ///   Status >= Approved AND Status != Rejected
    /// Includes: Approved, InProgress, OnTrack, AtRisk, Completed, Locked.
    /// </summary>
    public int ApprovedWorkflowCount { get; init; }
    public int RejectedCount { get; init; }
    public int LockedCount { get; init; }

    // ── Execution-state counts (progress visibility) ──────────────────────────
    public int InProgressCount { get; init; }
    public int AtRiskCount { get; init; }
    public int CompletedCount { get; init; }

    // ── Derived riskmetrics ──────────────────────────────────────────────────
    /// <summary>Goals past their DueDate and not Completed or Locked.</summary>
    public int OverdueCount { get; init; }

    // ── Weight governance ────────────────────────────────────────────────────
    /// <summary>Sum of Weight across all goals in this cycle for the employee.</summary>
    public int TotalWeight { get; init; }

    /// <summary>True when TotalGoals > 0 AND TotalWeight == 100.</summary>
    public bool IsWeightBalanced { get; init; }

    // ── Governance status (derived, see derivation rules in service) ──────────
    public TeamGovernanceStatus GovernanceStatus { get; init; }
}

/// <summary>
/// Flat single-row representation of an employee goal.
/// Used by the Awaiting Approval, At Risk, Overdue, and Locked tabs.
///
/// Computed fields (DaysRemaining, IsOverdue) are set by the service
/// after database projection using IDateTimeProvider.TodayUtc.
/// </summary>
public sealed class TeamGoalFlatDto
{
    // ── Identity ─────────────────────────────────────────────────────────────
    public Guid GoalId { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;

    // ── Goal metadata ─────────────────────────────────────────────────────────
    public string Title { get; init; } = string.Empty;
    public GoalStatus Status { get; init; }
    public int Weight { get; init; }

    /// <summary>Progress percent (0–100), as stored on the goal.</summary>
    public decimal ProgressPercent { get; init; }

    // ── Timeline ─────────────────────────────────────────────────────────────
    public DateOnly DueDate { get; init; }
    public DateTime? SubmittedDate { get; init; }
    public DateTime? ApprovalDate { get; init; }

    // ── Computed by service (not stored in DB) ────────────────────────────────

    /// <summary>
    /// Days until (positive) or past (negative) the DueDate relative to today.
    /// Populated by the service after query materialisation.
    /// </summary>
    public int DaysRemaining { get; set; }

    /// <summary>
    /// True when DueDate &lt; Today AND Status is not Completed or Locked.
    /// Populated by the service after query materialisation.
    /// Status is the single source of truth — no separate IsLocked flag is consulted.
    /// </summary>
    public bool IsOverdue { get; set; }

    /// <summary>
    /// Days since submission when Status == PendingApproval.
    /// 0 if SubmittedDate is null.
    /// Computed by service.
    /// </summary>
    public int DaysPendingApproval { get; set; }

    /// <summary>
    /// How many calendar days past the DueDate this goal is.
    /// Non-null only when <see cref="IsOverdue"/> is true
    /// (DueDate &lt; today AND Status is not Completed, Locked, or Rejected).
    /// Null for all non-overdue goals — never negative.
    /// Computed by service using UTC clock.
    /// </summary>
    public int? DaysOverdue { get; init; }

    // ── Risk evaluation (populated by IGoalRiskEvaluator after materialisation) ──

    /// <summary>
    /// True when the risk evaluator flagged this goal.
    /// Populated by the service after query materialisation — never from the database.
    /// </summary>
    public bool IsAtRisk { get; init; }

    /// <summary>
    /// Human-readable reason produced by the evaluator.
    /// Null when <see cref="IsAtRisk"/> is false.
    /// </summary>
    public string? RiskReason { get; init; }

    /// <summary>
    /// Numeric severity score from the evaluator.
    /// 0 = not at risk; higher value = greater urgency.
    /// See <c>GoalRiskEvaluator</c> for severity bands.
    /// </summary>
    public int RiskSeverityScore { get; init; }

    /// <summary>UTC timestamp when the goal was locked. Null for non-locked goals.</summary>
    public DateTime? LockedDate { get; init; }
}

// ─────────────────────────────────────────────────────────────────────────────
//  AtRiskGoalsRequest
//  Input parameters for IAtRiskGoalsQueryService — org-wide at-risk queries.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Request parameters for the org-wide at-risk goals query.
/// Used exclusively by <see cref="IAtRiskGoalsQueryService"/>.
/// </summary>
public sealed record AtRiskGoalsRequest
{
    /// <summary>The appraisal cycle to scope the query to. Required.</summary>
    public required Guid AppraisalCycleId { get; init; }

    /// <summary>
    /// Optional organisation unit filter (maps to <c>Employee.OrganizationUnitId</c>).
    /// When set, only employees belonging to that unit are included.
    /// When null, no unit restriction is applied.
    /// </summary>
    public Guid? OrganizationUnitId { get; init; }

    /// <summary>
    /// Optional organisation level filter (maps to <c>Employee.OrganizationLevelId</c>).
    /// Can be combined with <see cref="OrganizationUnitId"/> to further narrow results
    /// to a specific level within a unit (e.g. Senior Staff in Finance).
    /// When null, no level restriction is applied.
    /// </summary>
    public Guid? OrganizationLevelId { get; init; }
}

// ─────────────────────────────────────────────────────────────────────────────
//  GoalDetailDto
//  Full projection for the Goal Detail Drawer shown in the Manager Drill-Down.
//
//  Architecture notes:
//  - Returned exclusively by IGoalDetailQueryService.GetGoalDetailAsync().
//  - All navigation properties are FLATTENED into scalar fields — no EF graphs.
//  - Activity (progress entries, journal entries) is summarised as counts only.
//    Full activity collections are never loaded here (avoided N+1 risk and
//    unnecessary payload on the initial drawer open).
//  - MeasurementType is nullable: not every goal has a numeric target.
//  - Parent alignment fields are nullable: goals can be standalone.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Projection DTO for the Goal Detail Drawer (manager read-only view).
/// Single service call — no per-section follow-up queries.
/// </summary>
public sealed class GoalDetailDto
{
    // ── Core identity ─────────────────────────────────────────────────────────
    public Guid GoalId { get; init; }

    /// <summary>Short title shown in list cards.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Nullable full description; null → display muted "Not provided."</summary>
    public string? Description { get; init; }

    /// <summary>Nullable acceptance / success criteria text.</summary>
    public string? SuccessCriteria { get; init; }

    /// <summary>Percentage weight assigned to this goal (0–100).</summary>
    public int Weight { get; init; }

    public GoalPriority Priority { get; init; }
    public GoalStatus   Status   { get; init; }

    /// <summary>Start date — may be null for goals created without an explicit start.</summary>
    public DateOnly? StartDate { get; init; }
    public DateOnly  DueDate   { get; init; }

    /// <summary>Stored progress, 0–100.</summary>
    public decimal ProgressPercent { get; init; }

    // ── Measurement (nullable block — omit section when MeasurementType is null) ─
    /// <summary>Null means no quantitative target has been configured.</summary>
    public MeasurementType? MeasurementType { get; init; }
    public decimal?         TargetValue     { get; init; }
    public decimal?         MinValue        { get; init; }
    public decimal?         MaxValue        { get; init; }

    /// <summary>e.g. "%", "units", "revenue (USD)".</summary>
    public string? Unit { get; init; }

    // ── Parent alignment (nullable — standalone goals have no parent) ──────────
    /// <summary>
    /// How this goal was aligned: Company goal, Unit goal, Goal Library, etc.
    /// Null when the goal was created standalone.
    /// </summary>
    public GoalParentType? ParentType       { get; init; }

    /// <summary>Resolved title of the parent Company- or Unit goal, if any.</summary>
    public string? ParentGoalTitle    { get; init; }

    /// <summary>Title from the Goal Library if this goal was sourced from one.</summary>
    public string? GoalLibraryTitle   { get; init; }

    /// <summary>KPI Definition name if a KPI target is attached to this goal.</summary>
    public string? KpiDefinitionName  { get; init; }

    // ── Workflow timeline ──────────────────────────────────────────────────────
    public DateTime? SubmittedDate         { get; init; }
    public DateTime? ApprovalDate          { get; init; }

    /// <summary>
    /// Optional text feedback from the manager.
    /// When Status == Rejected this must be displayed in a highlighted panel.
    /// </summary>
    public string? ManagerFeedback { get; init; }

    /// <summary>True when the goal has been locked by HR or end-of-cycle processing.</summary>
    public bool      IsLocked   { get; init; }
    public DateTime? LockedDate { get; init; }

    /// <summary>Full name of the manager the goal was submitted to.</summary>
    public string? SubmittedToManagerName { get; init; }

    // ── Activity summary (counts only — never load full collections here) ──────

    /// <summary>Number of progress update records attached to this goal.</summary>
    public int ProgressEntryCount { get; init; }

    /// <summary>Last time a progress entry was recorded; null when no entries exist.</summary>
    public DateTime? LastProgressUpdateDate { get; init; }

    /// <summary>Number of journal / check-in entries linked to this goal.</summary>
    public int JournalEntryCount { get; init; }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Team Goal Progress DTOs
//  Used by the Manager Team Goal Progress page to surface per-employee
//  execution progress for a given appraisal cycle.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Slim per-goal projection inside a <see cref="TeamGoalProgressDto"/>.
/// Contains only the fields needed for the manager progress dashboard.
/// Not to be confused with <see cref="ErpSystem.Core.DTOs.HR.EmployeeGoalSummaryDto"/> in
/// AppraisalDTOs, which is an aggregate per-employee snapshot.
/// </summary>
public sealed class TeamProgressGoalItemDto
{
    public Guid GoalId { get; init; }
    public string Title { get; init; } = string.Empty;
    public decimal ProgressPercent { get; init; }
    public GoalStatus Status { get; init; }
    public int Weight { get; init; }
    public DateOnly DueDate { get; init; }

    /// <summary>True when DueDate &lt; today AND Status is not Completed or Locked.</summary>
    public bool IsOverdue { get; init; }

    /// <summary>How many calendar days past DueDate. Null when not overdue.</summary>
    public int? DaysOverdue { get; init; }
}

/// <summary>
/// Aggregated execution-progress snapshot for a single direct report
/// within an appraisal cycle.
/// Powers the Manager Team Goal Progress page.
/// </summary>
public sealed class TeamGoalProgressDto
{
    // ── Identity ─────────────────────────────────────────────────────────────
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;

    // ── Aggregate counts ──────────────────────────────────────────────────────
    public int TotalGoals { get; init; }
    public int NotStartedCount { get; init; }
    public int InProgressCount { get; init; }
    public int OnTrackCount { get; init; }
    public int AtRiskCount { get; init; }
    public int CompletedCount { get; init; }
    public int OverdueCount { get; init; }

    /// <summary>Average ProgressPercent across all goals; 0 when TotalGoals == 0.</summary>
    public decimal AverageProgressPercent { get; init; }

    // ── Goal detail list ──────────────────────────────────────────────────────
    public List<TeamProgressGoalItemDto> Goals { get; init; } = new();
}
