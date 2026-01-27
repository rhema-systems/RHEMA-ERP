using ErpSystem.Core.Interfaces.Workflow;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Simple stub implementation of workflow notification service
/// TODO: Replace with proper notification logic (email, SMS, etc.)
/// </summary>
public class WorkflowNotificationService : IWorkflowNotificationService
{
    private readonly ILogger<WorkflowNotificationService> _logger;

    public WorkflowNotificationService(ILogger<WorkflowNotificationService> logger)
    {
        _logger = logger;
    }

    public Task SendStepAssignmentNotificationAsync(Guid stepInstanceId, Guid assignedToId)
    {
        _logger.LogInformation("Sending step assignment notification for step {StepInstanceId} to user {UserId}",
            stepInstanceId, assignedToId);

        // TODO: Implement actual notification sending (email, push notification, etc.)
        return Task.CompletedTask;
    }

    public Task SendApprovalRequestNotificationAsync(Guid approvalId)
    {
        _logger.LogInformation("Sending approval request notification for approval {ApprovalId}", approvalId);

        // TODO: Implement actual notification sending
        return Task.CompletedTask;
    }

    public Task SendEscalationNotificationAsync(Guid stepInstanceId, string escalationReason)
    {
        _logger.LogInformation("Sending escalation notification for step {StepInstanceId}: {Reason}",
            stepInstanceId, escalationReason);

        // TODO: Implement actual notification sending
        return Task.CompletedTask;
    }

    public Task SendWorkflowCompletionNotificationAsync(Guid workflowInstanceId)
    {
        _logger.LogInformation("Sending workflow completion notification for workflow {WorkflowInstanceId}",
            workflowInstanceId);

        // TODO: Implement actual notification sending
        return Task.CompletedTask;
    }

    public Task SendOverdueStepRemindersAsync()
    {
        _logger.LogInformation("Sending overdue step reminders");

        // TODO: Implement actual reminder sending logic
        return Task.CompletedTask;
    }
}
