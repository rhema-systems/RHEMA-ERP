using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Workflow;

public sealed class WorkflowSlaEscalationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkflowSlaEscalationBackgroundService> _logger;

    public WorkflowSlaEscalationBackgroundService(IServiceScopeFactory scopeFactory,
        ILogger<WorkflowSlaEscalationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ExecuteCycleAsync(stoppingToken); }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Workflow SLA escalation cycle failed");
            }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    internal async Task ExecuteCycleAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<IWorkflowNotificationService>();
        var evaluator = scope.ServiceProvider.GetRequiredService<IWorkflowConditionEvaluator>();
        var engine = scope.ServiceProvider.GetRequiredService<IWorkflowEngine>();
        var now = DateTime.UtcNow;
        var approvals = await db.WorkflowApprovals.IgnoreQueryFilters()
            .Include(item => item.StepInstance).ThenInclude(step => step.WorkflowStep)
            .Include(item => item.StepInstance).ThenInclude(step => step.WorkflowInstance)
            .Where(item => !item.IsDeleted && item.Status == WorkflowApprovalStatus.Pending &&
                item.DueDate.HasValue && item.DueDate <= now)
            .Take(250).ToListAsync(cancellationToken);

        foreach (var approval in approvals)
        {
            var config = ParseConfiguration(approval.StepInstance.WorkflowStep.Configuration);
            var rules = config?.EscalationRules ?? [];
            for (var index = 0; index < rules.Count; index++)
            {
                var rule = rules[index];
                if (approval.DueDate!.Value.AddHours(rule.DelayHours) > now) continue;
                var alreadyExecuted = await db.WorkflowEscalationExecutions.IgnoreQueryFilters().AnyAsync(item =>
                    item.ApprovalId == approval.Id && item.RuleIndex == index && !item.IsDeleted, cancellationToken);
                if (alreadyExecuted) continue;
                if (!string.IsNullOrWhiteSpace(rule.TriggerCondition?.Expression) &&
                    !await evaluator.EvaluateConditionAsync(rule.TriggerCondition.Expression,
                        approval.StepInstance.WorkflowInstance.DataContext)) continue;

                var target = rule.EscalationTargets.OrderByDescending(item => item.Priority).FirstOrDefault();
                var targetUserId = target?.UserId;
                var targetRole = target?.Role;
                if (!targetUserId.HasValue && !string.IsNullOrWhiteSpace(targetRole))
                {
                    targetUserId = await ResolveRoleUserAsync(db, approval.TenantId, targetRole, cancellationToken);
                }

                var execution = new WorkflowEscalationExecution
                {
                    TenantId = approval.TenantId,
                    ApprovalId = approval.Id,
                    RuleIndex = index,
                    Action = rule.Action,
                    TargetUserId = targetUserId,
                    TargetRole = targetRole,
                    ExecutedAt = now
                };
                db.WorkflowEscalationExecutions.Add(execution);
                try
                {
                    switch (rule.Action)
                    {
                        case WorkflowEscalationAction.Reassign:
                            if (!targetUserId.HasValue && string.IsNullOrWhiteSpace(targetRole))
                                throw new InvalidOperationException("Escalation reassign requires a user or role target.");
                            approval.OriginalApproverId ??= approval.ApproverId;
                            approval.ApproverId = targetUserId;
                            approval.ApproverRole = targetUserId.HasValue ? null : targetRole;
                            approval.StepInstance.AssignedToId = targetUserId;
                            await notifications.SendApprovalRequestNotificationAsync(approval.Id);
                            break;
                        case WorkflowEscalationAction.AutoApprove:
                            if (!approval.ApproverId.HasValue)
                                throw new InvalidOperationException("Auto approval requires a resolved user approver.");
                            var result = await engine.ProcessStepAsync(approval.StepInstanceId,
                                approval.ApproverId.Value, WorkflowStepAction.Complete,
                                new { escalationExecutionId = execution.Id }, "Approved by configured SLA escalation rule");
                            if (!result.Success) throw new InvalidOperationException(result.Message ?? "Auto approval failed.");
                            break;
                        case WorkflowEscalationAction.Cancel:
                            approval.Status = WorkflowApprovalStatus.Expired;
                            approval.ProcessedDate = now;
                            approval.StepInstance.Status = WorkflowStepInstanceStatus.Cancelled;
                            approval.StepInstance.CompletedDate = now;
                            approval.StepInstance.WorkflowInstance.Status = WorkflowInstanceStatus.Cancelled;
                            approval.StepInstance.WorkflowInstance.CancelledDate = now;
                            break;
                        case WorkflowEscalationAction.Notify:
                        case WorkflowEscalationAction.NotifyManager:
                            await notifications.SendEscalationNotificationAsync(approval.StepInstanceId,
                                $"Approval SLA escalation level {index + 1}");
                            break;
                    }
                    execution.Result = "Completed";
                    db.WorkflowActivityLogs.Add(new WorkflowActivityLog
                    {
                        TenantId = approval.TenantId,
                        WorkflowInstanceId = approval.StepInstance.WorkflowInstanceId,
                        StepInstanceId = approval.StepInstanceId,
                        ActivityType = WorkflowActivityType.ApprovalEscalated,
                        Title = "Approval SLA escalated",
                        Description = $"Escalation rule {index + 1} executed with action {rule.Action}.",
                        Data = JsonSerializer.Serialize(new { execution.Id, rule.Action, targetUserId, targetRole }),
                        ActivityDate = now
                    });
                }
                catch (Exception exception)
                {
                    execution.Result = $"Failed: {exception.Message}";
                    _logger.LogWarning(exception, "Workflow escalation {ExecutionId} failed", execution.Id);
                }
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private static WorkflowStepConfigurationDto? ParseConfiguration(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new JsonStringEnumConverter());
            return JsonSerializer.Deserialize<WorkflowStepConfigurationDto>(json, options);
        }
        catch (JsonException) { return null; }
    }

    private static async Task<Guid?> ResolveRoleUserAsync(ApplicationDbContext db, Guid tenantId, string role,
        CancellationToken cancellationToken)
    {
        return await (from user in db.Users.IgnoreQueryFilters()
                      join userRole in db.UserRoles.IgnoreQueryFilters() on user.Id equals userRole.UserId
                      join applicationRole in db.Roles.IgnoreQueryFilters() on userRole.RoleId equals applicationRole.Id
                      where user.TenantId == tenantId && user.IsActive && applicationRole.Name == role
                      orderby user.FirstName, user.LastName
                      select (Guid?)user.Id).FirstOrDefaultAsync(cancellationToken);
    }
}
