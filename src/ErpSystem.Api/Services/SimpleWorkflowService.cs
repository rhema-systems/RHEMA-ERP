using ErpSystem.Core.Interfaces;

namespace ErpSystem.Api.Services;

public class SimpleWorkflowService : IWorkflowService
{
    public Task StartApprovalWorkflowAsync(string entityType, Guid entityId)
    {
        // Simple implementation - do nothing for now
        return Task.CompletedTask;
    }

    public Task<bool> CanUserApproveAsync(string entityType, Guid entityId, Guid userId)
    {
        // Simple implementation - always return true
        return Task.FromResult(true);
    }

    public Task ProcessApprovalStepAsync(string entityType, Guid entityId, Guid userId, string action, string? comments = null)
    {
        // Simple implementation - do nothing for now
        return Task.CompletedTask;
    }

    public Task<WorkflowStepInfo?> GetCurrentWorkflowStepAsync(string entityType, Guid entityId)
    {
        // Simple implementation - return null
        return Task.FromResult<WorkflowStepInfo?>(null);
    }

    public Task<List<WorkflowStepInfo>> GetWorkflowHistoryAsync(string entityType, Guid entityId)
    {
        // Simple implementation - return empty list
        return Task.FromResult(new List<WorkflowStepInfo>());
    }

    public Task<List<WorkflowApprovalItem>> GetPendingApprovalsAsync(Guid userId)
    {
        // Simple implementation - return empty list
        return Task.FromResult(new List<WorkflowApprovalItem>());
    }

    public Task CancelWorkflowAsync(string entityType, Guid entityId, string reason)
    {
        // Simple implementation - do nothing for now
        return Task.CompletedTask;
    }
}