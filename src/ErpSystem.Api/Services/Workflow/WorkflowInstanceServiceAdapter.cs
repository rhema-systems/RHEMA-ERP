using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Api.Services.Workflow;

/// <summary>
/// Adapter to bridge ErpSystem.Core.Interfaces.Workflow.IWorkflowInstanceService 
/// with ErpSystem.Core.Interfaces.Services.IWorkflowInstanceService
/// </summary>
public class WorkflowInstanceServiceAdapter : ErpSystem.Core.Interfaces.Workflow.IWorkflowInstanceService
{
    private readonly ErpSystem.Core.Interfaces.Services.IWorkflowInstanceService _coreService;
    private readonly ErpSystem.Core.Interfaces.Services.IWorkflowStepService _stepService;
    private readonly ErpSystem.Core.Interfaces.Services.IWorkflowApprovalService _approvalService;
    private readonly ErpSystem.Core.Interfaces.Services.IWorkflowActivityService _activityService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<WorkflowInstanceServiceAdapter> _logger;

    public WorkflowInstanceServiceAdapter(
        ErpSystem.Core.Interfaces.Services.IWorkflowInstanceService coreService,
        ErpSystem.Core.Interfaces.Services.IWorkflowStepService stepService,
        ErpSystem.Core.Interfaces.Services.IWorkflowApprovalService approvalService,
        ErpSystem.Core.Interfaces.Services.IWorkflowActivityService activityService,
        ICurrentUserService currentUserService,
        ILogger<WorkflowInstanceServiceAdapter> logger)
    {
        _coreService = coreService;
        _stepService = stepService;
        _approvalService = approvalService;
        _activityService = activityService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<WorkflowInstance?> GetWorkflowInstanceAsync(Guid id)
    {
        return await _coreService.GetInstanceAsync(id);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetWorkflowInstancesForEntityAsync(Guid entityId)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        return await _coreService.GetInstancesByEntityAsync(Guid.Empty, entityId.ToString());
    }

    public async Task<IEnumerable<WorkflowInstance>> GetWorkflowInstancesByStatusAsync(string status)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? parsedUserId : Guid.Empty;
        // For now, just return active instances assigned to the user
        return await _coreService.GetActiveAssignedToUserAsync(userId, tenantId);
    }

    public async Task<IEnumerable<WorkflowStepInstance>> GetPendingTasksForUserAsync(Guid userId)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            _logger.LogWarning("Tenant ID not available for GetPendingTasksForUserAsync");
            return Enumerable.Empty<WorkflowStepInstance>();
        }

        return await _stepService.GetActiveStepsForUserAsync(userId, tenantId);
    }

    public async Task<IEnumerable<WorkflowApproval>> GetPendingApprovalsAsync(Guid? userId = null, string? role = null)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            _logger.LogWarning("Tenant ID not available for GetPendingApprovalsAsync");
            return Enumerable.Empty<WorkflowApproval>();
        }

        var effectiveUserId = userId ??
                              (Guid.TryParse(_currentUserService.UserId, out var parsedUserId)
                                  ? parsedUserId
                                  : Guid.Empty);

        if (effectiveUserId == Guid.Empty)
        {
            _logger.LogWarning("User ID not available for GetPendingApprovalsAsync");
            return Enumerable.Empty<WorkflowApproval>();
        }

        if (!string.IsNullOrWhiteSpace(role) && !_currentUserService.IsInRole(role))
        {
            return Enumerable.Empty<WorkflowApproval>();
        }

        return await _approvalService.GetPendingApprovalsAsync(effectiveUserId, tenantId);
    }

    public async Task UpdateWorkflowDataContextAsync(Guid workflowInstanceId, object dataContext)
    {
        var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? parsedUserId : Guid.Empty;
        await _coreService.UpdateInstanceDataAsync(workflowInstanceId, dataContext, userId);
    }

    public async Task AddCommentAsync(Guid workflowInstanceId, Guid userId, string comment)
    {
        _logger.LogInformation("AddCommentAsync called for instance {InstanceId} by user {UserId}", workflowInstanceId, userId);
        // This would need to be implemented in the core service
        await Task.CompletedTask;
    }

    public async Task<IEnumerable<WorkflowActivityLog>> GetWorkflowActivityLogAsync(Guid workflowInstanceId)
    {
        return await _activityService.GetInstanceActivityLogAsync(workflowInstanceId);
    }
}
