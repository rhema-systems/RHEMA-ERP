using System.Text.Json;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Service implementation for workflow activity logging
/// </summary>
public class WorkflowActivityService : IWorkflowActivityService
{
    private readonly IWorkflowActivityLogRepository _workflowActivityLogRepository;
    private readonly IWorkflowInstanceRepository _workflowInstanceRepository;
    private readonly ILogger<WorkflowActivityService> _logger;

    public WorkflowActivityService(
        IWorkflowActivityLogRepository workflowActivityLogRepository,
        IWorkflowInstanceRepository workflowInstanceRepository,
        ILogger<WorkflowActivityService> logger)
    {
        _workflowActivityLogRepository = workflowActivityLogRepository;
        _workflowInstanceRepository = workflowInstanceRepository;
        _logger = logger;
    }

    public async Task LogActivityAsync(
        Guid workflowInstanceId,
        WorkflowActivityType activityType,
        string title,
        string? description = null,
        Guid? performedById = null,
        Guid? stepInstanceId = null,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Logging workflow activity: {ActivityType} for instance {WorkflowInstanceId}", activityType, workflowInstanceId);

        // Get tenant ID from workflow instance (this could be cached or passed as parameter for better performance)
        var workflowInstance = await GetWorkflowInstanceForTenantId(workflowInstanceId, cancellationToken);
        var tenantId = workflowInstance?.TenantId ?? Guid.Empty;

        var activityLog = new WorkflowActivityLog
        {
            Id = Guid.NewGuid(),
            WorkflowInstanceId = workflowInstanceId,
            StepInstanceId = stepInstanceId,
            ActivityType = activityType,
            Title = title,
            Description = description,
            Data = data != null ? JsonSerializer.Serialize(data) : null,
            PerformedById = performedById,
            ActivityDate = DateTime.UtcNow,
            TenantId = tenantId
        };

        await _workflowActivityLogRepository.AddAsync(activityLog);
        await _workflowActivityLogRepository.SaveChangesAsync();

        _logger.LogDebug("Logged workflow activity {Id} for instance {WorkflowInstanceId}", activityLog.Id, workflowInstanceId);
    }

    public async Task<IEnumerable<WorkflowActivityLog>> GetInstanceActivityLogAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default)
    {
        return await _workflowActivityLogRepository.GetByWorkflowInstanceAsync(workflowInstanceId, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowActivityLog>> GetRecentActivityAsync(Guid tenantId, int count = 100, CancellationToken cancellationToken = default)
    {
        return await _workflowActivityLogRepository.GetRecentAsync(tenantId, count, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowActivityLog>> GetUserActivityAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _workflowActivityLogRepository.GetByUserAsync(userId, tenantId, cancellationToken);
    }

    private async Task<WorkflowInstance?> GetWorkflowInstanceForTenantId(Guid workflowInstanceId, CancellationToken cancellationToken)
    {
        // Get basic workflow instance info to determine tenant ID
        return await _workflowInstanceRepository.GetByIdAsync(workflowInstanceId);
    }
}
