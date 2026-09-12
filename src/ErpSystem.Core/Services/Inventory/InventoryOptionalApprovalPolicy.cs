using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Inventory;

/// <summary>Applies only the server's successful submission decision, never a client flag.</summary>
public static class InventoryOptionalApprovalPolicy
{
    public const string ReadyToPost = "ReadyToPost";
    public const string NoApprovalAction = "ApprovalNotRequired";
    public const string NoApprovalComment = "No active approval workflow is configured. Ready for authorised posting; no human approval was recorded.";

    public static void ApplySubmission(StockAdjustment adjustment, WorkflowIntegrationResult result)
    {
        ValidateSubmission(result);
        if (!result.ApprovalRequired && (adjustment.WorkflowInstanceId.HasValue || adjustment.ApprovedById.HasValue || adjustment.ApprovedAt.HasValue))
            throw new InvalidOperationException("Existing adjustment approval history cannot be replaced by a no-workflow decision.");
        adjustment.ApprovalRequired = result.ApprovalRequired;
        adjustment.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId;
        adjustment.Status = result.ApprovalRequired ? "PendingApproval" : ReadyToPost;
        if (!result.ApprovalRequired)
        {
            adjustment.ApprovedById = null;
            adjustment.ApprovedAt = null;
        }
    }

    public static void ApplySubmission(InventoryReturnVoucher voucher, WorkflowIntegrationResult result)
    {
        ValidateSubmission(result);
        if (!result.ApprovalRequired && (voucher.WorkflowInstanceId.HasValue || voucher.ApprovedById.HasValue || voucher.ApprovedAtUtc.HasValue))
            throw new InvalidOperationException("Existing return approval history cannot be replaced by a no-workflow decision.");
        voucher.ApprovalRequired = result.ApprovalRequired;
        voucher.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId;
        voucher.Status = result.ApprovalRequired ? InventoryReturnVoucherStatus.PendingApproval : InventoryReturnVoucherStatus.ReadyToPost;
        if (!result.ApprovalRequired)
        {
            voucher.ApprovedById = null;
            voucher.ApprovedAtUtc = null;
        }
    }

    public static bool CanPostState(StockAdjustment value) => value.ApprovalRequired
        ? value.Status == "Approved"
        : value.Status == ReadyToPost && value.WorkflowInstanceId == null && value.ApprovedById == null && value.ApprovedAt == null;

    public static bool CanPostState(InventoryReturnVoucher value) => value.ApprovalRequired
        ? value.Status == InventoryReturnVoucherStatus.Approved
        : value.Status == InventoryReturnVoucherStatus.ReadyToPost && value.WorkflowInstanceId == null && value.ApprovedById == null && value.ApprovedAtUtc == null;

    private static void ValidateSubmission(WorkflowIntegrationResult result)
    {
        if (!result.ExecutionResult.Success)
            throw new InvalidOperationException(result.ExecutionResult.Message ?? "The approval workflow could not be started.");
        if (result.ApprovalRequired &&
            (result.Outcome != WorkflowOutcome.Pending || !result.ExecutionResult.WorkflowInstanceId.HasValue))
            throw new InvalidOperationException("The configured inventory workflow must start with an independent pending approval step.");
        if (!result.ApprovalRequired &&
            (result.Outcome != WorkflowOutcome.Approved || result.ExecutionResult.WorkflowInstanceId.HasValue))
            throw new InvalidOperationException("The no-approval decision is inconsistent. Refresh and retry submission.");
    }
}
