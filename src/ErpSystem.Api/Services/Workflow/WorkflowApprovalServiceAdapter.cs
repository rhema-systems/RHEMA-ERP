using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Api.Services.Workflow;

/// <summary>
/// Adapter to bridge ErpSystem.Core.Interfaces.Workflow.IWorkflowApprovalService 
/// with ErpSystem.Core.Interfaces.Services.IWorkflowApprovalService
/// </summary>
public class WorkflowApprovalServiceAdapter : ErpSystem.Core.Interfaces.Workflow.IWorkflowApprovalService
{
    private readonly ErpSystem.Core.Interfaces.Services.IWorkflowApprovalService _coreService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<WorkflowApprovalServiceAdapter> _logger;

    public WorkflowApprovalServiceAdapter(
        ErpSystem.Core.Interfaces.Services.IWorkflowApprovalService coreService,
        ICurrentUserService currentUserService,
        ILogger<WorkflowApprovalServiceAdapter> logger)
    {
        _coreService = coreService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<WorkflowApproval> CreateApprovalAsync(Guid stepInstanceId, Guid approverId, string? approverRole = null, DateTime? dueDate = null)
    {
        return await _coreService.RequestApprovalAsync(stepInstanceId, approverId, dueDate, null);
    }

    public async Task<WorkflowApproval> ProcessApprovalAsync(Guid approvalId, Guid userId, WorkflowApprovalAction action, string? comments = null)
    {
        await _coreService.ProcessApprovalAsync(approvalId, action, userId, comments);

        // Return the updated approval (would need to fetch it)
        _logger.LogInformation("Processed approval {ApprovalId} with action {Action}", approvalId, action);
        return new WorkflowApproval
        {
            Id = approvalId,
            ProcessedById = userId,
            ProcessedDate = DateTime.UtcNow,
            Comments = comments,
            Status = action == WorkflowApprovalAction.Approve
                ? WorkflowApprovalStatus.Approved
                : WorkflowApprovalStatus.Rejected
        };
    }

    public async Task<WorkflowApproval> DelegateApprovalAsync(Guid approvalId, Guid delegateToId, Guid delegatedById, string? reason = null)
    {
        await _coreService.DelegateApprovalAsync(approvalId, delegateToId, delegatedById, reason);

        // Return the updated approval (would need to fetch it)
        _logger.LogInformation("Delegated approval {ApprovalId} to user {DelegateToId}", approvalId, delegateToId);
        return new WorkflowApproval
        {
            Id = approvalId,
            ApproverId = delegateToId
        };
    }

    public async Task<IEnumerable<WorkflowApproval>> GetApprovalHistoryAsync(Guid stepInstanceId)
    {
        return await _coreService.GetStepApprovalsAsync(stepInstanceId);
    }
}
