using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Api.Services.Workflow;

/// <summary>
/// Adapter to bridge ErpSystem.Core.Interfaces.Workflow.IWorkflowInstanceService 
/// with ErpSystem.Core.Interfaces.Services.IWorkflowInstanceService
/// </summary>
public class WorkflowInstanceServiceAdapter : ErpSystem.Core.Interfaces.Workflow.IWorkflowInstanceService
{
    private readonly ErpSystem.Core.Interfaces.Services.IWorkflowInstanceService _coreService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<WorkflowInstanceServiceAdapter> _logger;

    public WorkflowInstanceServiceAdapter(
        ErpSystem.Core.Interfaces.Services.IWorkflowInstanceService coreService,
        ICurrentUserService currentUserService,
        ILogger<WorkflowInstanceServiceAdapter> logger)
    {
        _coreService = coreService;
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
        _logger.LogWarning("GetPendingTasksForUserAsync is not fully implemented");
        return Enumerable.Empty<WorkflowStepInstance>();
    }

    public async Task<IEnumerable<WorkflowApproval>> GetPendingApprovalsAsync(Guid? userId = null, string? role = null)
    {
        _logger.LogWarning("GetPendingApprovalsAsync is not fully implemented");
        return Enumerable.Empty<WorkflowApproval>();
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
        _logger.LogWarning("GetWorkflowActivityLogAsync is not fully implemented");
        return Enumerable.Empty<WorkflowActivityLog>();
    }
}
