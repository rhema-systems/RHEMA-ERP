using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Identity;

public enum HrIdentityReconciliationTrigger
{
    Scheduled = 0,
    Manual = 1,
    Retry = 2
}

public enum HrIdentityReconciliationRunStatus
{
    Running = 0,
    Completed = 1,
    CompletedWithErrors = 2,
    Failed = 3
}

public enum HrIdentityReconciliationItemStatus
{
    NoChange = 0,
    Reconciled = 1,
    ReviewRequired = 2,
    Failed = 3
}

public enum HrIdentityWorkflowIssueType
{
    ManagerReplacementUnavailable = 0,
    SegregationOfDutiesConflict = 1,
    DepartmentOwnershipChanged = 2,
    InactiveApprover = 3
}

public enum HrIdentityWorkflowIssueStatus
{
    Open = 0,
    Resolved = 1,
    Dismissed = 2
}

/// <summary>
/// Current authoritative HR snapshot for an identity-linked employee. Roles remain Identity-owned;
/// RoleNamesJson is evidence of what was observed, never a source for automatic role mutation.
/// </summary>
[Table("HrIdentityReconciliationStates")]
public sealed class HrIdentityReconciliationState : TenantEntity
{
    public Guid UserId { get; set; }
    public Guid EmployeeId { get; set; }
    public bool HrAccessEligible { get; set; }
    public bool AccessSuspendedByReconciliation { get; set; }
    public bool ReactivationReviewRequired { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? ManagerEmployeeId { get; set; }
    public Guid? ManagerUserId { get; set; }
    [Column(TypeName = "nvarchar(max)")]
    public string RoleNamesJson { get; set; } = "[]";
    [Column(TypeName = "nvarchar(max)")]
    public string SuspendedUserTenantIdsJson { get; set; } = "[]";
    [Required, StringLength(64)]
    public string SourceFingerprint { get; set; } = string.Empty;
    public DateTime LastObservedAtUtc { get; set; }
    public DateTime LastReconciledAtUtc { get; set; }
    public Guid? LastRunId { get; set; }
    [StringLength(1000)]
    public string? ReviewReason { get; set; }
}

[Table("HrIdentityReconciliationRuns")]
public sealed class HrIdentityReconciliationRun : TenantEntity
{
    [Required, StringLength(200)]
    public string IdempotencyKey { get; set; } = string.Empty;
    public HrIdentityReconciliationTrigger Trigger { get; set; }
    public HrIdentityReconciliationRunStatus Status { get; set; } = HrIdentityReconciliationRunStatus.Running;
    public Guid? RequestedById { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int CandidateCount { get; set; }
    public int ReconciledCount { get; set; }
    public int ReviewRequiredCount { get; set; }
    public int FailedCount { get; set; }
    [StringLength(2000)]
    public string? Error { get; set; }
}

/// <summary>
/// Append-only per-user result. Retries create a new item and preserve every prior attempt.
/// </summary>
[Table("HrIdentityReconciliationItems")]
public sealed class HrIdentityReconciliationItem : TenantEntity
{
    public Guid RunId { get; set; }
    public Guid UserId { get; set; }
    public Guid EmployeeId { get; set; }
    public int AttemptNumber { get; set; } = 1;
    public HrIdentityReconciliationItemStatus Status { get; set; }
    public bool AccessStateChanged { get; set; }
    public bool DepartmentChanged { get; set; }
    public bool ManagerChanged { get; set; }
    public bool RoleSnapshotChanged { get; set; }
    public Guid? PreviousDepartmentId { get; set; }
    public Guid? CurrentDepartmentId { get; set; }
    public Guid? PreviousManagerEmployeeId { get; set; }
    public Guid? CurrentManagerEmployeeId { get; set; }
    public int SessionsRevoked { get; set; }
    public int RefreshTokensRevoked { get; set; }
    public int ResponsibilityAssignmentsSuspended { get; set; }
    public int WorkflowAssignmentsReassigned { get; set; }
    public int WorkflowIssuesCreated { get; set; }
    [Required, StringLength(2000)]
    public string Summary { get; set; } = string.Empty;
    [StringLength(2000)]
    public string? Error { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
}

[Table("HrIdentityWorkflowIssues")]
public sealed class HrIdentityWorkflowIssue : TenantEntity
{
    public Guid UserId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    public Guid? WorkflowStepInstanceId { get; set; }
    public Guid? WorkflowApprovalId { get; set; }
    public HrIdentityWorkflowIssueType IssueType { get; set; }
    public HrIdentityWorkflowIssueStatus Status { get; set; } = HrIdentityWorkflowIssueStatus.Open;
    public Guid? StaleAssigneeId { get; set; }
    public Guid? SuggestedReplacementUserId { get; set; }
    [Required, StringLength(2000)]
    public string Reason { get; set; } = string.Empty;
    public DateTime DetectedAtUtc { get; set; }
    public Guid? ResolvedById { get; set; }
    public Guid? ReplacementUserId { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    [StringLength(2000)]
    public string? ResolutionNote { get; set; }
}
