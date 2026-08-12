using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Procurement;

internal static class EmergencyPurchaseGovernanceRules
{
    internal static bool IsAllowedTransition(string from, string to) => (from, to) switch
    {
        ("Draft", "Prepared") => true,
        ("Prepared", "PendingAudit") => true,
        ("PendingAudit", "AuditVouched") => true,
        ("AuditVouched", "PendingApproval") => true,
        ("PendingApproval", "Approved") => true,
        ("PendingApproval", "Rejected") => true,
        ("Approved", "Triggered") => true,
        ("Triggered", "Filed") => true,
        _ => false
    };

    internal static bool IsRejectedWorkflowOutcome(WorkflowInstanceStatus status) =>
        status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed;

    internal static bool IsApprovalAuthorityRole(string role) =>
        role is "TDC_MANAGING_DIRECTOR" or "TDC_BOARD_APPROVER";
}
