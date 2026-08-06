namespace ErpSystem.Core.DTOs.Identity;

public sealed record HrIdentityAccessDecisionDto(
    bool IsAllowed,
    string Code,
    string Message,
    Guid? EmployeeId = null);

public sealed record HrIdentityReconciliationRunRequest(string? IdempotencyKey = null);

public sealed record HrIdentityWorkflowIssueResolutionRequest(
    Guid ReplacementUserId,
    string ResolutionNote);

public sealed record HrIdentityReactivationRequest(string ReviewNote);

public sealed record HrIdentityUserOptionDto(
    Guid UserId,
    Guid EmployeeId,
    string DisplayName,
    string? UserName,
    string? EmployeeNumber,
    Guid? DepartmentId,
    string? DepartmentName,
    bool IsManager);

public sealed record HrIdentityWorkflowIssueDto(
    Guid Id,
    Guid UserId,
    string UserDisplayName,
    Guid EmployeeId,
    string EmployeeNumber,
    Guid WorkflowInstanceId,
    Guid? WorkflowStepInstanceId,
    Guid? WorkflowApprovalId,
    string IssueType,
    string Status,
    Guid? StaleAssigneeId,
    string? StaleAssigneeName,
    Guid? SuggestedReplacementUserId,
    string? SuggestedReplacementName,
    string Reason,
    DateTime DetectedAtUtc,
    Guid? ReplacementUserId,
    string? ReplacementUserName,
    DateTime? ResolvedAtUtc,
    string? ResolutionNote);

public sealed record HrIdentityReconciliationStateDto(
    Guid UserId,
    string UserDisplayName,
    string? UserName,
    Guid EmployeeId,
    string EmployeeNumber,
    bool UserIsActive,
    bool HrAccessEligible,
    bool AccessSuspendedByReconciliation,
    bool ReactivationReviewRequired,
    string StaffStatus,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? ManagerEmployeeId,
    string? ManagerName,
    Guid? ManagerUserId,
    IReadOnlyList<string> Roles,
    DateTime LastObservedAtUtc,
    DateTime LastReconciledAtUtc,
    string? ReviewReason);

public sealed record HrIdentityReconciliationRunDto(
    Guid Id,
    string IdempotencyKey,
    string Trigger,
    string Status,
    Guid? RequestedById,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    int CandidateCount,
    int ReconciledCount,
    int ReviewRequiredCount,
    int FailedCount,
    string? Error);

public sealed record HrIdentityReconciliationDashboardDto(
    int LinkedUsers,
    int HrIneligibleUsers,
    int SuspendedUsers,
    int ReactivationReviews,
    int OpenWorkflowIssues,
    IReadOnlyList<HrIdentityReconciliationStateDto> States,
    IReadOnlyList<HrIdentityWorkflowIssueDto> Issues,
    IReadOnlyList<HrIdentityReconciliationRunDto> RecentRuns);
