using System.Text.Json;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Service implementation for workflow instance management and execution
/// </summary>
public class WorkflowInstanceService : IWorkflowInstanceService
{
    private readonly IWorkflowInstanceRepository _workflowInstanceRepository;
    private readonly IWorkflowDefinitionRepository _workflowDefinitionRepository;
    private readonly IWorkflowStepRepository _workflowStepRepository;
    private readonly IWorkflowStepInstanceRepository _workflowStepInstanceRepository;
    private readonly IWorkflowActivityService _workflowActivityService;
    private readonly ILogger<WorkflowInstanceService> _logger;

    public WorkflowInstanceService(
        IWorkflowInstanceRepository workflowInstanceRepository,
        IWorkflowDefinitionRepository workflowDefinitionRepository,
        IWorkflowStepRepository workflowStepRepository,
        IWorkflowStepInstanceRepository workflowStepInstanceRepository,
        IWorkflowActivityService workflowActivityService,
        ILogger<WorkflowInstanceService> logger)
    {
        _workflowInstanceRepository = workflowInstanceRepository;
        _workflowDefinitionRepository = workflowDefinitionRepository;
        _workflowStepRepository = workflowStepRepository;
        _workflowStepInstanceRepository = workflowStepInstanceRepository;
        _workflowActivityService = workflowActivityService;
        _logger = logger;
    }

    public async Task<WorkflowInstance> StartWorkflowAsync(Guid definitionId, Guid entityTypeId, string entityId, Guid startedById, object? initialData = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting workflow instance for definition {DefinitionId}, entity {EntityId}", definitionId, entityId);

        // Get workflow definition
        var definition = await _workflowDefinitionRepository.GetWithDetailsAsync(definitionId, cancellationToken) ?? throw new InvalidOperationException($"Workflow definition with ID {definitionId} not found");
        if (!definition.IsActive)
        {
            throw new InvalidOperationException($"Workflow definition '{definition.Name}' is not active");
        }

        // Validate entity type matches
        if (definition.EntityTypeId != entityTypeId)
        {
            throw new InvalidOperationException($"Entity type mismatch: expected {definition.EntityTypeId}, got {entityTypeId}");
        }

        // Get start step
        var startStep = await _workflowStepRepository.GetStartStepAsync(definitionId, cancellationToken) ?? throw new InvalidOperationException($"No start step found for workflow definition '{definition.Name}'");

        // Create workflow instance
        var workflowInstance = new WorkflowInstance
        {
            Id = Guid.NewGuid(),
            WorkflowDefinitionId = definitionId,
            EntityTypeId = entityTypeId,
            EntityId = Guid.Parse(entityId),
            Status = WorkflowInstanceStatus.InProgress,
            InitiatedById = startedById,
            StartedDate = DateTime.UtcNow,
            Priority = WorkflowPriority.Normal,
            Data = initialData != null ? JsonSerializer.Serialize(initialData) : null,
            TenantId = definition.TenantId
        };

        // Save workflow instance
        await _workflowInstanceRepository.AddAsync(workflowInstance);
        await _workflowInstanceRepository.SaveChangesAsync();

        // Create and start the first step instance
        var firstStepInstance = new WorkflowStepInstance
        {
            Id = Guid.NewGuid(),
            WorkflowInstanceId = workflowInstance.Id,
            WorkflowStepId = startStep.Id,
            Status = WorkflowStepInstanceStatus.Pending,
            StartedDate = DateTime.UtcNow,
            AssignedToId = DetermineStepAssignee(startStep, startedById),
            TenantId = definition.TenantId
        };

        await _workflowStepInstanceRepository.AddAsync(firstStepInstance);
        await _workflowStepInstanceRepository.SaveChangesAsync();

        // Update workflow instance with current step
        workflowInstance.CurrentStepId = startStep.Id;
        await _workflowInstanceRepository.UpdateAsync(workflowInstance);
        await _workflowInstanceRepository.SaveChangesAsync();

        // Log activity
        await _workflowActivityService.LogActivityAsync(
            workflowInstance.Id,
            WorkflowActivityType.WorkflowStarted,
            "Workflow started",
            $"Workflow '{definition.Name}' started for {entityId}",
            startedById,
            firstStepInstance.Id,
            initialData,
            cancellationToken);

        _logger.LogInformation("Started workflow instance {InstanceId} for definition {DefinitionId}", workflowInstance.Id, definitionId);
        return workflowInstance;
    }

    public async Task<WorkflowInstance?> GetInstanceAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        return await _workflowInstanceRepository.GetWithDetailsAsync(instanceId, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetInstancesByEntityAsync(Guid entityTypeId, string entityId, CancellationToken cancellationToken = default)
    {
        return await _workflowInstanceRepository.GetByEntityAsync(entityTypeId, entityId, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetActiveAssignedToUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _workflowInstanceRepository.GetActiveAssignedToUserAsync(userId, tenantId, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetInstancesStartedByUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _workflowInstanceRepository.GetStartedByUserAsync(userId, tenantId, cancellationToken);
    }

    public async Task CancelWorkflowAsync(Guid instanceId, Guid cancelledById, string? reason = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cancelling workflow instance {InstanceId}", instanceId);

        var instance = await _workflowInstanceRepository.GetWithDetailsAsync(instanceId, cancellationToken) ?? throw new InvalidOperationException($"Workflow instance with ID {instanceId} not found");
        if (instance.Status == WorkflowInstanceStatus.Completed)
        {
            throw new InvalidOperationException("Cannot cancel a completed workflow");
        }

        if (instance.Status == WorkflowInstanceStatus.Cancelled)
        {
            throw new InvalidOperationException("Workflow is already cancelled");
        }

        // Cancel current step instances
        var activeStepInstances = instance.StepInstances?.Where(si =>
            si.Status == WorkflowStepInstanceStatus.Pending ||
            si.Status == WorkflowStepInstanceStatus.InProgress).ToList();

        if (activeStepInstances?.Any() == true)
        {
            foreach (var stepInstance in activeStepInstances)
            {
                stepInstance.Status = WorkflowStepInstanceStatus.Cancelled;
                stepInstance.CompletedDate = DateTime.UtcNow;
                await _workflowStepInstanceRepository.UpdateAsync(stepInstance);
            }
            await _workflowStepInstanceRepository.SaveChangesAsync();
        }

        // Update workflow instance status
        var cancelledAt = DateTime.UtcNow;
        instance.Status = WorkflowInstanceStatus.Cancelled;
        // Retain CompletedDate for existing terminal-state consumers, while recording
        // cancellation in the dedicated field used by workflow audit and reporting.
        instance.CompletedDate = cancelledAt;
        instance.CancelledDate = cancelledAt;
        instance.Notes = reason ?? "Workflow cancelled";

        await _workflowInstanceRepository.UpdateAsync(instance);
        await _workflowInstanceRepository.SaveChangesAsync();

        // Log activity
        await _workflowActivityService.LogActivityAsync(
            instanceId,
            WorkflowActivityType.WorkflowCancelled,
            "Workflow cancelled",
            reason ?? "Workflow cancelled",
            cancelledById,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Cancelled workflow instance {InstanceId}", instanceId);
    }

    public async Task<WorkflowInstance> RestartWorkflowAsync(Guid instanceId, Guid restartedById, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Restarting workflow instance {InstanceId}", instanceId);

        var instance = await _workflowInstanceRepository.GetWithDetailsAsync(instanceId, cancellationToken) ?? throw new InvalidOperationException($"Workflow instance with ID {instanceId} not found");
        if (instance.Status == WorkflowInstanceStatus.InProgress)
        {
            throw new InvalidOperationException("Cannot restart a workflow that is still in progress");
        }

        // Get workflow definition to restart from the beginning
        var definition = await _workflowDefinitionRepository.GetWithDetailsAsync(instance.WorkflowDefinitionId, cancellationToken);
        if (definition == null || !definition.IsActive)
        {
            throw new InvalidOperationException("Cannot restart workflow: definition not found or inactive");
        }

        // Cancel existing step instances
        if (instance.StepInstances?.Any() == true)
        {
            foreach (var stepInstance in instance.StepInstances)
            {
                stepInstance.Status = WorkflowStepInstanceStatus.Cancelled;
                await _workflowStepInstanceRepository.UpdateAsync(stepInstance);
            }
            await _workflowStepInstanceRepository.SaveChangesAsync();
        }

        // Get start step and create new step instance
        var startStep = await _workflowStepRepository.GetStartStepAsync(instance.WorkflowDefinitionId, cancellationToken) ?? throw new InvalidOperationException("No start step found for workflow definition");
        var newStepInstance = new WorkflowStepInstance
        {
            Id = Guid.NewGuid(),
            WorkflowInstanceId = instance.Id,
            WorkflowStepId = startStep.Id,
            Status = WorkflowStepInstanceStatus.Pending,
            StartedDate = DateTime.UtcNow,
            AssignedToId = DetermineStepAssignee(startStep, restartedById),
            TenantId = instance.TenantId
        };

        await _workflowStepInstanceRepository.AddAsync(newStepInstance);
        await _workflowStepInstanceRepository.SaveChangesAsync();

        // Update workflow instance
        instance.Status = WorkflowInstanceStatus.InProgress;
        instance.CurrentStepId = startStep.Id;
        instance.CompletedDate = null;
        instance.Notes = "Workflow restarted";

        await _workflowInstanceRepository.UpdateAsync(instance);
        await _workflowInstanceRepository.SaveChangesAsync();

        // Log activity
        await _workflowActivityService.LogActivityAsync(
            instanceId,
            WorkflowActivityType.WorkflowStarted,
            "Workflow restarted",
            "Workflow restarted from beginning",
            restartedById,
            newStepInstance.Id,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Restarted workflow instance {InstanceId}", instanceId);
        return instance;
    }

    public async Task UpdateInstanceDataAsync(Guid instanceId, object data, Guid updatedById, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating workflow instance data {InstanceId}", instanceId);

        var instance = await _workflowInstanceRepository.GetByIdAsync(instanceId) ?? throw new InvalidOperationException($"Workflow instance with ID {instanceId} not found");
        instance.Data = JsonSerializer.Serialize(data);
        instance.UpdatedAt = DateTime.UtcNow;

        await _workflowInstanceRepository.UpdateAsync(instance);
        await _workflowInstanceRepository.SaveChangesAsync();

        // Log activity
        await _workflowActivityService.LogActivityAsync(
            instanceId,
            WorkflowActivityType.DataUpdated,
            "Workflow data updated",
            "Workflow instance data was updated",
            updatedById,
            data: data,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Updated workflow instance data {InstanceId}", instanceId);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetOverdueInstancesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _workflowInstanceRepository.GetOverdueInstancesAsync(tenantId, cancellationToken);
    }

    private static Guid? DetermineStepAssignee(WorkflowStep step, Guid defaultUserId)
    {
        // Simple assignment logic - can be enhanced with more sophisticated rules
        switch (step.AssignmentType ?? "User")
        {
            case "User":
                return !string.IsNullOrEmpty(step.AssignmentConfiguration) && Guid.TryParse(step.AssignmentConfiguration, out var userId)
                    ? userId
                    : defaultUserId;

            case "RequestorManager":
            case "PreviousStepUser":
            case "Dynamic":
                // These would require additional logic to determine the actual user
                // For now, fall back to default user
                return defaultUserId;

            case "Role":
                // Would need to resolve role to specific user
                // For now, fall back to default user
                return defaultUserId;

            default:
                return defaultUserId;
        }
    }
}
