using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Api.Services.Workflow;

/// <summary>
/// Adapter to bridge ErpSystem.Core.Interfaces.Workflow.IWorkflowStepService 
/// with ErpSystem.Core.Interfaces.Services.IWorkflowStepService
/// </summary>
public class WorkflowStepServiceAdapter : ErpSystem.Core.Interfaces.Workflow.IWorkflowStepService
{
    private readonly ErpSystem.Core.Interfaces.Services.IWorkflowStepService _coreService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<WorkflowStepServiceAdapter> _logger;

    public WorkflowStepServiceAdapter(
        ErpSystem.Core.Interfaces.Services.IWorkflowStepService coreService,
        ICurrentUserService currentUserService,
        ILogger<WorkflowStepServiceAdapter> logger)
    {
        _coreService = coreService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task AssignStepAsync(Guid stepInstanceId, Guid assignedToId, Guid assignedById)
    {
        await _coreService.AssignStepAsync(stepInstanceId, assignedToId, assignedById, null);
    }

    public async Task ReassignStepAsync(Guid stepInstanceId, Guid newAssignedToId, Guid reassignedById, string? reason = null)
    {
        await _coreService.ReassignStepAsync(stepInstanceId, newAssignedToId, reassignedById, reason);
    }

    public async Task EscalateStepAsync(Guid stepInstanceId, string escalationReason)
    {
        var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? parsedUserId : Guid.Empty;
        await _coreService.EscalateStepAsync(stepInstanceId, userId, escalationReason);
    }

    public async Task SetStepDueDateAsync(Guid stepInstanceId, DateTime dueDate)
    {
        _logger.LogInformation("SetStepDueDateAsync called for step {StepId} with due date {DueDate}", stepInstanceId, dueDate);
        // This would need to be implemented in the core service
        await Task.CompletedTask;
    }

    public async Task<IEnumerable<WorkflowStepInstance>> GetOverdueStepsAsync()
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        return await _coreService.GetOverdueStepsAsync(tenantId);
    }
}
