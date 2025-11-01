using Microsoft.Extensions.Logging;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Interfaces.Services;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Service implementation for workflow step management
/// </summary>
public class WorkflowStepService : IWorkflowStepService
{
    private readonly IWorkflowStepInstanceRepository _stepInstanceRepository;
    private readonly IWorkflowActivityService _activityService;
    private readonly ILogger<WorkflowStepService> _logger;

    public WorkflowStepService(
        IWorkflowStepInstanceRepository stepInstanceRepository,
        IWorkflowActivityService activityService,
        ILogger<WorkflowStepService> logger)
    {
        _stepInstanceRepository = stepInstanceRepository;
        _activityService = activityService;
        _logger = logger;
    }

    public async Task CompleteStepAsync(Guid stepInstanceId, Guid completedById, object? stepData = null, string? comments = null, CancellationToken cancellationToken = default)
    {
        var stepInstance = await _stepInstanceRepository.GetByIdAsync(stepInstanceId);
        if (stepInstance == null)
        {
            throw new InvalidOperationException($"Step instance with ID {stepInstanceId} not found");
        }

        stepInstance.Status = WorkflowStepInstanceStatus.Completed;
        stepInstance.CompletedDate = DateTime.UtcNow;
        stepInstance.Comments = comments;

        await _stepInstanceRepository.UpdateAsync(stepInstance);
        await _stepInstanceRepository.SaveChangesAsync();

        _logger.LogInformation("Completed step instance {StepInstanceId} by user {UserId}", stepInstanceId, completedById);
    }

    public async Task AssignStepAsync(Guid stepInstanceId, Guid assignedToId, Guid assignedById, string? comments = null, CancellationToken cancellationToken = default)
    {
        var stepInstance = await _stepInstanceRepository.GetByIdAsync(stepInstanceId);
        if (stepInstance == null)
        {
            throw new InvalidOperationException($"Step instance with ID {stepInstanceId} not found");
        }

        stepInstance.AssignedToId = assignedToId;

        await _stepInstanceRepository.UpdateAsync(stepInstance);
        await _stepInstanceRepository.SaveChangesAsync();

        _logger.LogInformation("Assigned step instance {StepInstanceId} to user {AssignedToId}", stepInstanceId, assignedToId);
    }

    public async Task ReassignStepAsync(Guid stepInstanceId, Guid newAssignedToId, Guid reassignedById, string? reason = null, CancellationToken cancellationToken = default)
    {
        var stepInstance = await _stepInstanceRepository.GetByIdAsync(stepInstanceId);
        if (stepInstance == null)
        {
            throw new InvalidOperationException($"Step instance with ID {stepInstanceId} not found");
        }

        var previousAssignedToId = stepInstance.AssignedToId;
        stepInstance.AssignedToId = newAssignedToId;

        await _stepInstanceRepository.UpdateAsync(stepInstance);
        await _stepInstanceRepository.SaveChangesAsync();

        _logger.LogInformation("Reassigned step instance {StepInstanceId} from user {PreviousAssignedToId} to user {NewAssignedToId}. Reason: {Reason}", 
            stepInstanceId, previousAssignedToId, newAssignedToId, reason);
    }

    public async Task EscalateStepAsync(Guid stepInstanceId, Guid escalatedById, string? reason = null, CancellationToken cancellationToken = default)
    {
        var stepInstance = await _stepInstanceRepository.GetByIdAsync(stepInstanceId);
        if (stepInstance == null)
        {
            throw new InvalidOperationException($"Step instance with ID {stepInstanceId} not found");
        }

        // Mark as escalated (could add an Escalated status)
        _logger.LogWarning("Step instance {StepInstanceId} escalated by user {EscalatedById}. Reason: {Reason}", 
            stepInstanceId, escalatedById, reason);

        await Task.CompletedTask;
    }

    public async Task<WorkflowStepInstance?> GetCurrentStepAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default)
    {
        return await _stepInstanceRepository.GetCurrentStepAsync(workflowInstanceId, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowStepInstance>> GetActiveStepsForUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _stepInstanceRepository.GetActiveAssignedToUserAsync(userId, tenantId, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowStepInstance>> GetOverdueStepsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _stepInstanceRepository.GetOverdueStepInstancesAsync(tenantId, cancellationToken);
    }
}
