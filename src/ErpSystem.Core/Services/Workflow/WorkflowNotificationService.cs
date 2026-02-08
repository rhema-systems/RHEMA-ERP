using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Core.Interfaces.Events;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Workflow notification service that delegates delivery to the unified notification service
/// </summary>
public class WorkflowNotificationService : IWorkflowNotificationService
{
    private readonly IAppEventBus _appEventBus;
    private readonly IWorkflowEntityDisplayService _entityDisplayService;
    private readonly IWorkflowStepInstanceRepository _stepInstanceRepository;
    private readonly IWorkflowApprovalRepository _approvalRepository;
    private readonly IWorkflowInstanceRepository _workflowInstanceRepository;
    private readonly IWorkflowStepRepository _workflowStepRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<WorkflowNotificationService> _logger;

    public WorkflowNotificationService(
        IAppEventBus appEventBus,
        IWorkflowEntityDisplayService entityDisplayService,
        IWorkflowStepInstanceRepository stepInstanceRepository,
        IWorkflowApprovalRepository approvalRepository,
        IWorkflowInstanceRepository workflowInstanceRepository,
        IWorkflowStepRepository workflowStepRepository,
        ICurrentUserService currentUserService,
        ILogger<WorkflowNotificationService> logger)
    {
        _appEventBus = appEventBus;
        _entityDisplayService = entityDisplayService;
        _stepInstanceRepository = stepInstanceRepository;
        _approvalRepository = approvalRepository;
        _workflowInstanceRepository = workflowInstanceRepository;
        _workflowStepRepository = workflowStepRepository;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public Task SendWorkflowSubmittedNotificationAsync(Guid workflowInstanceId)
    {
        return SendWorkflowSubmittedNotificationInternalAsync(workflowInstanceId);
    }

    public Task SendStepAssignmentNotificationAsync(Guid stepInstanceId, Guid assignedToId)
    {
        return SendStepAssignmentNotificationInternalAsync(stepInstanceId, assignedToId);
    }

    public Task SendApprovalRequestNotificationAsync(Guid approvalId)
    {
        return SendApprovalRequestNotificationInternalAsync(approvalId);
    }

    public Task SendEscalationNotificationAsync(Guid stepInstanceId, string escalationReason)
    {
        return SendEscalationNotificationInternalAsync(stepInstanceId, escalationReason);
    }

    public Task SendWorkflowCompletionNotificationAsync(Guid workflowInstanceId)
    {
        return SendWorkflowCompletionNotificationInternalAsync(workflowInstanceId);
    }

    public Task SendWorkflowRejectedNotificationAsync(Guid workflowInstanceId, Guid rejectedById, string? comments = null)
    {
        return SendWorkflowRejectedNotificationInternalAsync(workflowInstanceId, rejectedById, comments);
    }

    public Task SendOverdueStepRemindersAsync()
    {
        return SendOverdueStepRemindersInternalAsync();
    }

    private async Task SendWorkflowSubmittedNotificationInternalAsync(Guid workflowInstanceId)
    {
        try
        {
            _logger.LogInformation("Sending workflow submitted notification for workflow {WorkflowInstanceId}", workflowInstanceId);

            var instance = await _workflowInstanceRepository.GetWithDetailsAsync(workflowInstanceId);
            if (instance == null)
            {
                _logger.LogWarning("Workflow instance {WorkflowInstanceId} not found for submitted notification", workflowInstanceId);
                return;
            }

            var definitionName = instance.WorkflowDefinition?.Name ?? "Workflow";
            var entityDisplay = await ResolveEntityDisplayAsync(instance);
            var entityType = entityDisplay.EntityType ?? instance.EntityType?.Code ?? instance.EntityType?.Name ?? "Entity";

            var currentStep = await _stepInstanceRepository.GetCurrentStepAsync(instance.Id);
            var currentStepDef = currentStep?.WorkflowStep ??
                                 (currentStep != null ? await _workflowStepRepository.GetByIdAsync(currentStep.WorkflowStepId) : null);
            var currentStepName = currentStepDef?.Name;

            var entityRef = !string.IsNullOrWhiteSpace(entityDisplay.EntityNumber)
                ? $"{entityType} {entityDisplay.EntityNumber}"
                : $"{entityType} {instance.EntityId}";
            var message = $"Your {definitionName} request for {entityRef} was submitted for approval.";
            if (!string.IsNullOrWhiteSpace(currentStepName))
            {
                message += $" Current step: {currentStepName}.";
            }

            var data = new Dictionary<string, object>
            {
                ["workflowInstanceId"] = instance.Id,
                ["workflowDefinitionId"] = instance.WorkflowDefinitionId,
                ["definitionName"] = definitionName,
                ["EntityType"] = entityType,
                ["EntityId"] = instance.EntityId,
                ["EntityNumber"] = entityDisplay.EntityNumber ?? string.Empty,
                ["ActionUrl"] = entityDisplay.ActionUrl ?? string.Empty,
                ["currentStepName"] = currentStepName ?? string.Empty,
                ["InitiatedByUserId"] = instance.InitiatedById,
                ["TargetUserId"] = instance.InitiatedById,
                ["Title"] = "Submitted For Approval",
                ["Message"] = message
            };

            await PublishEntityActivityAsync(instance.TenantId, entityType, "WorkflowSubmitted", instance.EntityId, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending workflow submitted notification for workflow {WorkflowInstanceId}", workflowInstanceId);
        }
    }

    private async Task SendStepAssignmentNotificationInternalAsync(Guid stepInstanceId, Guid assignedToId)
    {
        try
        {
            _logger.LogInformation("Sending step assignment notification for step {StepInstanceId} to user {UserId}",
                stepInstanceId, assignedToId);

            var stepInstance = await _stepInstanceRepository.GetByIdAsync(stepInstanceId);
            if (stepInstance == null)
            {
                _logger.LogWarning("Step instance {StepInstanceId} not found for assignment notification", stepInstanceId);
                return;
            }

            var workflowInstance = await _workflowInstanceRepository.GetWithDetailsAsync(stepInstance.WorkflowInstanceId);
            var stepDefinition = stepInstance.WorkflowStep ?? await _workflowStepRepository.GetByIdAsync(stepInstance.WorkflowStepId);

            var stepName = stepDefinition?.Name ?? "Workflow Step";
            var entityDisplay = workflowInstance != null
                ? await ResolveEntityDisplayAsync(workflowInstance)
                : null;
            var entityInfo = entityDisplay?.EntityNumber != null
                ? $" for {entityDisplay.EntityType} {entityDisplay.EntityNumber}"
                : (workflowInstance != null ? $" for entity {workflowInstance.EntityId}" : string.Empty);
            var message = $"You have been assigned to '{stepName}'{entityInfo}.";

            var data = new Dictionary<string, object>
            {
                ["stepInstanceId"] = stepInstanceId,
                ["workflowInstanceId"] = workflowInstance?.Id ?? Guid.Empty,
                ["workflowStepId"] = stepDefinition?.Id ?? Guid.Empty,
                ["EntityType"] = entityDisplay?.EntityType ?? workflowInstance?.EntityType?.Code ?? workflowInstance?.EntityType?.Name ?? string.Empty,
                ["EntityId"] = workflowInstance?.EntityId ?? Guid.Empty,
                ["EntityNumber"] = entityDisplay?.EntityNumber ?? string.Empty,
                ["ActionUrl"] = entityDisplay?.ActionUrl ?? string.Empty,
                ["AssignedToUserId"] = assignedToId,
                ["TargetUserId"] = assignedToId,
                ["Title"] = "Workflow Step Assigned",
                ["Message"] = message
            };

            await PublishEntityActivityAsync(workflowInstance?.TenantId, data["EntityType"]?.ToString() ?? string.Empty, "WorkflowStepAssignment", workflowInstance?.EntityId, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending step assignment notification for step {StepInstanceId}", stepInstanceId);
        }
    }

    private async Task SendApprovalRequestNotificationInternalAsync(Guid approvalId)
    {
        try
        {
            _logger.LogInformation("Sending approval request notification for approval {ApprovalId}", approvalId);

            var approval = await _approvalRepository.GetByIdAsync(approvalId);
            if (approval == null)
            {
                _logger.LogWarning("Approval {ApprovalId} not found for notification", approvalId);
                return;
            }

            var stepInstance = await _stepInstanceRepository.GetByIdAsync(approval.StepInstanceId);
            var workflowInstance = stepInstance != null
                ? await _workflowInstanceRepository.GetWithDetailsAsync(stepInstance.WorkflowInstanceId)
                : null;
            var stepDefinition = stepInstance?.WorkflowStep ??
                                 (stepInstance != null ? await _workflowStepRepository.GetByIdAsync(stepInstance.WorkflowStepId) : null);

            var stepName = stepDefinition?.Name ?? "Workflow Step";
            var entityDisplay = workflowInstance != null
                ? await ResolveEntityDisplayAsync(workflowInstance)
                : null;
            var entityInfo = entityDisplay?.EntityNumber != null
                ? $" for {entityDisplay.EntityType} {entityDisplay.EntityNumber}"
                : (workflowInstance != null ? $" for entity {workflowInstance.EntityId}" : string.Empty);
            var message = $"Approval required for '{stepName}'{entityInfo}.";

            var data = new Dictionary<string, object>
            {
                ["approvalId"] = approvalId,
                ["stepInstanceId"] = approval.StepInstanceId,
                ["workflowInstanceId"] = workflowInstance?.Id ?? Guid.Empty,
                ["workflowStepId"] = stepDefinition?.Id ?? Guid.Empty,
                ["EntityType"] = entityDisplay?.EntityType ?? workflowInstance?.EntityType?.Code ?? workflowInstance?.EntityType?.Name ?? string.Empty,
                ["EntityId"] = workflowInstance?.EntityId ?? Guid.Empty,
                ["EntityNumber"] = entityDisplay?.EntityNumber ?? string.Empty,
                ["ActionUrl"] = entityDisplay?.ActionUrl ?? string.Empty,
                ["Title"] = "Approval Required",
                ["Message"] = message
            };

            if (approval.ApproverId.HasValue && approval.ApproverId.Value != Guid.Empty)
            {
                data["ApproverUserId"] = approval.ApproverId.Value;
                data["TargetUserId"] = approval.ApproverId.Value;
                await PublishEntityActivityAsync(workflowInstance?.TenantId, data["EntityType"]?.ToString() ?? string.Empty, "WorkflowApprovalRequest", workflowInstance?.EntityId, data);
            }
            else if (!string.IsNullOrWhiteSpace(approval.ApproverRole))
            {
                var tenantId = workflowInstance?.TenantId ?? _currentUserService.TenantId ?? Guid.Empty;
                data["ApproverRole"] = approval.ApproverRole;
                data["TargetRole"] = approval.ApproverRole;
                await PublishEntityActivityAsync(tenantId, data["EntityType"]?.ToString() ?? string.Empty, "WorkflowApprovalRequest", workflowInstance?.EntityId, data);
            }
            else
            {
                _logger.LogWarning("Approval {ApprovalId} has no approver or role assigned", approvalId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending approval request notification for approval {ApprovalId}", approvalId);
        }
    }

    private async Task SendEscalationNotificationInternalAsync(Guid stepInstanceId, string escalationReason)
    {
        try
        {
            _logger.LogInformation("Sending escalation notification for step {StepInstanceId}: {Reason}",
                stepInstanceId, escalationReason);

            var stepInstance = await _stepInstanceRepository.GetByIdAsync(stepInstanceId);
            if (stepInstance == null)
            {
                _logger.LogWarning("Step instance {StepInstanceId} not found for escalation notification", stepInstanceId);
                return;
            }

            var workflowInstance = await _workflowInstanceRepository.GetWithDetailsAsync(stepInstance.WorkflowInstanceId);
            var stepDefinition = stepInstance.WorkflowStep ?? await _workflowStepRepository.GetByIdAsync(stepInstance.WorkflowStepId);
            var stepName = stepDefinition?.Name ?? "Workflow Step";

            var entityDisplay = workflowInstance != null
                ? await ResolveEntityDisplayAsync(workflowInstance)
                : null;

            var message = $"Escalation for '{stepName}': {escalationReason}.";
            var data = new Dictionary<string, object>
            {
                ["stepInstanceId"] = stepInstanceId,
                ["workflowInstanceId"] = workflowInstance?.Id ?? Guid.Empty,
                ["workflowStepId"] = stepDefinition?.Id ?? Guid.Empty,
                ["EntityType"] = entityDisplay?.EntityType ?? workflowInstance?.EntityType?.Code ?? workflowInstance?.EntityType?.Name ?? string.Empty,
                ["EntityId"] = workflowInstance?.EntityId ?? Guid.Empty,
                ["EntityNumber"] = entityDisplay?.EntityNumber ?? string.Empty,
                ["ActionUrl"] = entityDisplay?.ActionUrl ?? string.Empty,
                ["escalationReason"] = escalationReason,
                ["Title"] = "Workflow Step Escalated",
                ["Message"] = message
            };

            var recipients = new HashSet<Guid>();
            if (stepInstance.AssignedToId.HasValue && stepInstance.AssignedToId.Value != Guid.Empty)
            {
                recipients.Add(stepInstance.AssignedToId.Value);
            }

            if (workflowInstance?.InitiatedById != Guid.Empty)
            {
                recipients.Add(workflowInstance.InitiatedById);
            }

            data["TargetUserIds"] = recipients.ToList();
            await PublishEntityActivityAsync(workflowInstance?.TenantId, data["EntityType"]?.ToString() ?? string.Empty, "WorkflowStepEscalated", workflowInstance?.EntityId, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending escalation notification for step {StepInstanceId}", stepInstanceId);
        }
    }

    private async Task SendWorkflowCompletionNotificationInternalAsync(Guid workflowInstanceId)
    {
        try
        {
            _logger.LogInformation("Sending workflow completion notification for workflow {WorkflowInstanceId}",
                workflowInstanceId);

            var instance = await _workflowInstanceRepository.GetWithDetailsAsync(workflowInstanceId);
            if (instance == null)
            {
                _logger.LogWarning("Workflow instance {WorkflowInstanceId} not found for completion notification", workflowInstanceId);
                return;
            }

            var definitionName = instance.WorkflowDefinition?.Name ?? "Workflow";
            var entityDisplay = await ResolveEntityDisplayAsync(instance);
            var entityType = entityDisplay.EntityType ?? instance.EntityType?.Code ?? instance.EntityType?.Name ?? "Entity";
            var entityRef = !string.IsNullOrWhiteSpace(entityDisplay.EntityNumber)
                ? $"{entityType} {entityDisplay.EntityNumber}"
                : $"{entityType} {instance.EntityId}";
            var message = $"{definitionName} approved for {entityRef}.";

            var data = new Dictionary<string, object>
            {
                ["workflowInstanceId"] = instance.Id,
                ["workflowDefinitionId"] = instance.WorkflowDefinitionId,
                ["EntityType"] = entityType,
                ["EntityId"] = instance.EntityId,
                ["EntityNumber"] = entityDisplay.EntityNumber ?? string.Empty,
                ["ActionUrl"] = entityDisplay.ActionUrl ?? string.Empty,
                ["InitiatedByUserId"] = instance.InitiatedById,
                ["TargetUserId"] = instance.InitiatedById,
                ["Title"] = "Approval Completed",
                ["Message"] = message
            };

            await PublishEntityActivityAsync(instance.TenantId, entityType, "WorkflowCompleted", instance.EntityId, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending workflow completion notification for workflow {WorkflowInstanceId}", workflowInstanceId);
        }
    }

    private async Task SendWorkflowRejectedNotificationInternalAsync(Guid workflowInstanceId, Guid rejectedById, string? comments)
    {
        try
        {
            _logger.LogInformation("Sending workflow rejected notification for workflow {WorkflowInstanceId}", workflowInstanceId);

            var instance = await _workflowInstanceRepository.GetWithDetailsAsync(workflowInstanceId);
            if (instance == null)
            {
                _logger.LogWarning("Workflow instance {WorkflowInstanceId} not found for rejected notification", workflowInstanceId);
                return;
            }

            var definitionName = instance.WorkflowDefinition?.Name ?? "Workflow";
            var entityDisplay = await ResolveEntityDisplayAsync(instance);
            var entityType = entityDisplay.EntityType ?? instance.EntityType?.Code ?? instance.EntityType?.Name ?? "Entity";
            var entityRef = !string.IsNullOrWhiteSpace(entityDisplay.EntityNumber)
                ? $"{entityType} {entityDisplay.EntityNumber}"
                : $"{entityType} {instance.EntityId}";
            var message = $"{definitionName} rejected for {entityRef}.";
            if (!string.IsNullOrWhiteSpace(comments))
            {
                message += $" Comment: {comments}";
            }

            var data = new Dictionary<string, object>
            {
                ["workflowInstanceId"] = instance.Id,
                ["workflowDefinitionId"] = instance.WorkflowDefinitionId,
                ["EntityType"] = entityType,
                ["EntityId"] = instance.EntityId,
                ["EntityNumber"] = entityDisplay.EntityNumber ?? string.Empty,
                ["ActionUrl"] = entityDisplay.ActionUrl ?? string.Empty,
                ["rejectedById"] = rejectedById,
                ["comments"] = comments ?? string.Empty,
                ["InitiatedByUserId"] = instance.InitiatedById,
                ["TargetUserId"] = instance.InitiatedById,
                ["Title"] = "Approval Rejected",
                ["Message"] = message
            };

            await PublishEntityActivityAsync(instance.TenantId, entityType, "WorkflowRejected", instance.EntityId, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending workflow rejected notification for workflow {WorkflowInstanceId}", workflowInstanceId);
        }
    }

    private async Task SendOverdueStepRemindersInternalAsync()
    {
        try
        {
            _logger.LogInformation("Sending overdue step reminders");

            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                _logger.LogWarning("Tenant ID not available for overdue step reminders");
                return;
            }

            var overdueSteps = await _stepInstanceRepository.GetOverdueStepInstancesAsync(tenantId);
            foreach (var stepInstance in overdueSteps)
            {
                if (!stepInstance.AssignedToId.HasValue || stepInstance.AssignedToId.Value == Guid.Empty)
                {
                    continue;
                }

                var stepDefinition = stepInstance.WorkflowStep ?? await _workflowStepRepository.GetByIdAsync(stepInstance.WorkflowStepId);
                var stepName = stepDefinition?.Name ?? "Workflow Step";
                var dueDate = stepInstance.DueDate?.ToString("MMM dd, yyyy") ?? "unknown";

                var message = $"'{stepName}' is overdue. Due date: {dueDate}.";

                var workflowInstance = await _workflowInstanceRepository.GetWithDetailsAsync(stepInstance.WorkflowInstanceId);
                if (workflowInstance == null)
                {
                    continue;
                }

                var entityDisplay = await ResolveEntityDisplayAsync(workflowInstance);
                var entityType = entityDisplay.EntityType ?? workflowInstance.EntityType?.Code ?? workflowInstance.EntityType?.Name ?? "Entity";

                var data = new Dictionary<string, object>
                {
                    ["stepInstanceId"] = stepInstance.Id,
                    ["workflowInstanceId"] = stepInstance.WorkflowInstanceId,
                    ["workflowStepId"] = stepDefinition?.Id ?? Guid.Empty,
                    ["dueDate"] = stepInstance.DueDate?.ToString("o") ?? string.Empty,
                    ["EntityType"] = entityType,
                    ["EntityId"] = workflowInstance.EntityId,
                    ["EntityNumber"] = entityDisplay.EntityNumber ?? string.Empty,
                    ["ActionUrl"] = entityDisplay.ActionUrl ?? string.Empty,
                    ["AssignedToUserId"] = stepInstance.AssignedToId.Value,
                    ["TargetUserId"] = stepInstance.AssignedToId.Value,
                    ["Title"] = "Workflow Step Overdue",
                    ["Message"] = message
                };

                await PublishEntityActivityAsync(stepInstance.TenantId, entityType, "WorkflowStepOverdue", workflowInstance.EntityId, data);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending overdue step reminders");
        }
    }

    private async Task PublishEntityActivityAsync(
        Guid? tenantId,
        string entityType,
        string activity,
        Guid? entityId,
        Dictionary<string, object>? data)
    {
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty) return;
        if (string.IsNullOrWhiteSpace(entityType)) return;
        if (string.IsNullOrWhiteSpace(activity)) return;

        var triggeredBy = Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : (Guid?)null;

        await _appEventBus.PublishAsync(new EntityActivityEvent
        {
            TenantId = tenantId.Value,
            EntityType = entityType,
            Activity = activity,
            Audience = "Internal",
            EntityId = entityId,
            TriggeredByUserId = triggeredBy,
            Data = data ?? new Dictionary<string, object>()
        });
    }

    private async Task<WorkflowEntityDisplayInfo> ResolveEntityDisplayAsync(WorkflowInstance instance)
    {
        var entityType = instance.EntityType?.Code ?? instance.EntityType?.Name ?? string.Empty;
        var info = await _entityDisplayService.GetEntityDisplayInfoAsync(entityType, instance.EntityId);

        if (string.IsNullOrWhiteSpace(info.EntityType))
        {
            info.EntityType = entityType;
        }

        if (info.EntityId == Guid.Empty)
        {
            info.EntityId = instance.EntityId;
        }

        return info;
    }
}
