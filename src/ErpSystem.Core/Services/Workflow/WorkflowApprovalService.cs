using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Service implementation for workflow approval management
/// </summary>
public class WorkflowApprovalService : IWorkflowApprovalService
{
    private readonly IWorkflowApprovalRepository _approvalRepository;
    private readonly IWorkflowStepInstanceRepository _stepInstanceRepository;
    private readonly IWorkflowActivityService _activityService;
    private readonly ILogger<WorkflowApprovalService> _logger;

    public WorkflowApprovalService(
        IWorkflowApprovalRepository approvalRepository,
        IWorkflowStepInstanceRepository stepInstanceRepository,
        IWorkflowActivityService activityService,
        ILogger<WorkflowApprovalService> logger)
    {
        _approvalRepository = approvalRepository;
        _stepInstanceRepository = stepInstanceRepository;
        _activityService = activityService;
        _logger = logger;
    }

    public async Task<WorkflowApproval> RequestApprovalAsync(Guid stepInstanceId, Guid approvedById, DateTime? dueDate = null, string? comments = null, CancellationToken cancellationToken = default)
    {
        var stepInstance = await _stepInstanceRepository.GetByIdAsync(stepInstanceId) ?? throw new InvalidOperationException($"Step instance with ID {stepInstanceId} not found");
        var approval = new WorkflowApproval
        {
            Id = Guid.NewGuid(),
            StepInstanceId = stepInstanceId,
            ApproverId = approvedById,
            Status = WorkflowApprovalStatus.Pending,
            DueDate = dueDate,
            Comments = comments,
            CreatedAt = DateTime.UtcNow,
            TenantId = stepInstance.TenantId
        };

        await _approvalRepository.AddAsync(approval);
        await _approvalRepository.SaveChangesAsync();

        _logger.LogInformation("Created approval request {ApprovalId} for step instance {StepInstanceId}", approval.Id, stepInstanceId);
        return approval;
    }

    public async Task ProcessApprovalAsync(Guid approvalId, WorkflowApprovalAction action, Guid decidedById, string? comments = null, CancellationToken cancellationToken = default)
    {
        var approval = await _approvalRepository.GetByIdAsync(approvalId) ?? throw new InvalidOperationException($"Approval with ID {approvalId} not found");
        approval.ProcessedById = decidedById;
        approval.ProcessedDate = DateTime.UtcNow;
        approval.Comments = comments;
        approval.Status = action == WorkflowApprovalAction.Approve
            ? WorkflowApprovalStatus.Approved
            : WorkflowApprovalStatus.Rejected;

        await _approvalRepository.UpdateAsync(approval);
        await _approvalRepository.SaveChangesAsync();

        _logger.LogInformation("Processed approval {ApprovalId} with action {Action} by user {UserId}",
            approvalId, action, decidedById);
    }

    public async Task DelegateApprovalAsync(Guid approvalId, Guid delegatedToId, Guid delegatedById, string? reason = null, CancellationToken cancellationToken = default)
    {
        var approval = await _approvalRepository.GetByIdAsync(approvalId) ?? throw new InvalidOperationException($"Approval with ID {approvalId} not found");
        var previousApproverId = approval.ApproverId;
        approval.ApproverId = delegatedToId;
        approval.Status = WorkflowApprovalStatus.Delegated;

        await _approvalRepository.UpdateAsync(approval);
        await _approvalRepository.SaveChangesAsync();

        _logger.LogInformation("Delegated approval {ApprovalId} from user {PreviousApproverId} to user {DelegatedToId}. Reason: {Reason}",
            approvalId, previousApproverId, delegatedToId, reason);
    }

    public async Task<IEnumerable<WorkflowApproval>> GetPendingApprovalsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _approvalRepository.GetPendingForUserAsync(userId, tenantId, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowApproval>> GetStepApprovalsAsync(Guid stepInstanceId, CancellationToken cancellationToken = default)
    {
        return await _approvalRepository.GetByStepInstanceAsync(stepInstanceId, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowApproval>> GetOverdueApprovalsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _approvalRepository.GetOverdueApprovalsAsync(tenantId, cancellationToken);
    }
}
