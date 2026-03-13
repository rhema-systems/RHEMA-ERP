using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Interfaces.Services;
using ErpSystem.Core.Interfaces.Workflow;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Main workflow engine implementation for executing and managing workflows
/// </summary>
public class WorkflowEngine : IWorkflowEngine
{
    private readonly IWorkflowDefinitionRepository _workflowDefinitionRepository;
    private readonly IWorkflowStepRepository _workflowStepRepository;
    private readonly IWorkflowTransitionRepository _workflowTransitionRepository;
    private readonly IWorkflowInstanceRepository _workflowInstanceRepository;
    private readonly IWorkflowStepInstanceRepository _workflowStepInstanceRepository;
    private readonly IWorkflowApprovalRepository _workflowApprovalRepository;
    private readonly IWorkflowActivityService _activityService;
    private readonly IWorkflowConditionEvaluator _conditionEvaluator;
    private readonly IWorkflowNotificationService _notificationService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<WorkflowEngine> _logger;

    public WorkflowEngine(
        IWorkflowDefinitionRepository workflowDefinitionRepository,
        IWorkflowStepRepository workflowStepRepository,
        IWorkflowTransitionRepository workflowTransitionRepository,
        IWorkflowInstanceRepository workflowInstanceRepository,
        IWorkflowStepInstanceRepository workflowStepInstanceRepository,
        IWorkflowApprovalRepository workflowApprovalRepository,
        IWorkflowActivityService activityService,
        IWorkflowConditionEvaluator conditionEvaluator,
        IWorkflowNotificationService notificationService,
        ICurrentUserService currentUserService,
        ILogger<WorkflowEngine> logger)
    {
        _workflowDefinitionRepository = workflowDefinitionRepository;
        _workflowStepRepository = workflowStepRepository;
        _workflowTransitionRepository = workflowTransitionRepository;
        _workflowInstanceRepository = workflowInstanceRepository;
        _workflowStepInstanceRepository = workflowStepInstanceRepository;
        _workflowApprovalRepository = workflowApprovalRepository;
        _activityService = activityService;
        _conditionEvaluator = conditionEvaluator;
        _notificationService = notificationService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<WorkflowInstance> StartWorkflowAsync(string workflowName, Guid entityId, Guid initiatedById, object? dataContext = null)
    {
        _logger.LogInformation("Starting workflow {WorkflowName} for entity {EntityId}", workflowName, entityId);

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var definition = await _workflowDefinitionRepository.GetByNameAsync(workflowName, tenantId)
            ?? throw new InvalidOperationException($"Workflow definition '{workflowName}' not found");

        if (!definition.IsActive)
        {
            throw new InvalidOperationException($"Workflow definition '{workflowName}' is not active");
        }

        var startStep = await _workflowStepRepository.GetStartStepAsync(definition.Id)
            ?? throw new InvalidOperationException($"No start step found for workflow '{workflowName}'");

        var workflowInstance = new WorkflowInstance
        {
            Id = Guid.NewGuid(),
            WorkflowDefinitionId = definition.Id,
            EntityId = entityId,
            EntityTypeId = definition.EntityTypeId,
            Status = WorkflowInstanceStatus.InProgress,
            InitiatedById = initiatedById,
            StartedById = initiatedById,
            StartedDate = DateTime.UtcNow,
            DataContext = dataContext != null ? JsonSerializer.Serialize(dataContext) : null,
            Data = dataContext != null ? JsonSerializer.Serialize(dataContext) : null,
            TenantId = definition.TenantId
        };

        await _workflowInstanceRepository.AddAsync(workflowInstance);
        await _workflowInstanceRepository.SaveChangesAsync();

        var startStepInstance = await CreateStepInstanceAsync(workflowInstance, startStep, initiatedById, dataContext);

        workflowInstance.CurrentStepId = startStep.Id;
        await _workflowInstanceRepository.UpdateAsync(workflowInstance);
        await _workflowInstanceRepository.SaveChangesAsync();

        await _activityService.LogActivityAsync(
            workflowInstance.Id,
            WorkflowActivityType.WorkflowStarted,
            "Workflow started",
            $"Workflow '{definition.Name}' started for entity {entityId}",
            initiatedById,
            startStepInstance.Id);

        await HandleStepEntryAsync(workflowInstance, startStepInstance, dataContext, initiatedById);

        // Let the requestor know the record is now under workflow (approver notifications happen on step entry).
        await _notificationService.SendWorkflowSubmittedNotificationAsync(workflowInstance.Id);

        return workflowInstance;
    }

    public async Task<WorkflowExecutionResult> ExecuteNextStepAsync(Guid workflowInstanceId, Guid userId, object? stepData = null)
    {
        var instance = await _workflowInstanceRepository.GetWithDetailsAsync(workflowInstanceId)
            ?? throw new InvalidOperationException($"Workflow instance {workflowInstanceId} not found");

        var currentStepInstance = await _workflowStepInstanceRepository.GetCurrentStepAsync(workflowInstanceId);
        if (currentStepInstance == null)
        {
            return await CompleteWorkflowIfPossibleAsync(instance, userId);
        }

        currentStepInstance.ResultData = stepData != null ? JsonSerializer.Serialize(stepData) : currentStepInstance.ResultData;
        currentStepInstance.Status = WorkflowStepInstanceStatus.Completed;
        currentStepInstance.CompletedDate = DateTime.UtcNow;
        await _workflowStepInstanceRepository.UpdateAsync(currentStepInstance);
        await _workflowStepInstanceRepository.SaveChangesAsync();

        await _activityService.LogActivityAsync(
            instance.Id,
            WorkflowActivityType.StepCompleted,
            "Step completed",
            $"Step '{currentStepInstance.WorkflowStep?.Name ?? "Step"}' completed",
            userId,
            currentStepInstance.Id,
            stepData);

        var nextResult = await AdvanceFromStepAsync(instance, currentStepInstance.WorkflowStep, userId, stepData);
        return nextResult;
    }

    public async Task<WorkflowExecutionResult> ExecuteTransitionAsync(Guid workflowInstanceId, Guid userId, Guid transitionId, object? stepData = null)
    {
        var instance = await _workflowInstanceRepository.GetWithDetailsAsync(workflowInstanceId)
            ?? throw new InvalidOperationException($"Workflow instance {workflowInstanceId} not found");

        var currentStepInstance = await _workflowStepInstanceRepository.GetCurrentStepAsync(workflowInstanceId);
        if (currentStepInstance == null)
        {
            return await CompleteWorkflowIfPossibleAsync(instance, userId);
        }

        var stepDefinition = currentStepInstance.WorkflowStep
            ?? await _workflowStepRepository.GetByIdAsync(currentStepInstance.WorkflowStepId);

        var transitions = await _workflowTransitionRepository.GetFromStepAsync(currentStepInstance.WorkflowStepId);
        var context = MergeDataContext(instance, stepData);

        var validTransitions = new List<WorkflowTransition>();
        foreach (var transition in transitions)
        {
            if (transition.Condition == null)
            {
                validTransitions.Add(transition);
                continue;
            }

            if (await EvaluateConditionAsync(transition.Condition, context))
            {
                validTransitions.Add(transition);
            }
        }

        var selectedTransition = validTransitions.FirstOrDefault(t => t.Id == transitionId);
        if (selectedTransition == null)
        {
            var fromStepName = stepDefinition?.Name ?? currentStepInstance.WorkflowStepId.ToString();
            throw new InvalidOperationException($"Transition '{transitionId}' is not available from step '{fromStepName}'.");
        }

        currentStepInstance.ResultData = stepData != null ? JsonSerializer.Serialize(stepData) : currentStepInstance.ResultData;
        currentStepInstance.Status = WorkflowStepInstanceStatus.Completed;
        currentStepInstance.CompletedDate = DateTime.UtcNow;
        await _workflowStepInstanceRepository.UpdateAsync(currentStepInstance);
        await _workflowStepInstanceRepository.SaveChangesAsync();

        await _activityService.LogActivityAsync(
            instance.Id,
            WorkflowActivityType.StepCompleted,
            "Step completed",
            $"Step '{currentStepInstance.WorkflowStep?.Name ?? "Step"}' completed",
            userId,
            currentStepInstance.Id,
            stepData);

        var nextStep = selectedTransition.ToStep ?? await _workflowStepRepository.GetByIdAsync(selectedTransition.ToStepId)
            ?? throw new InvalidOperationException("Next step not found");

        var nextStepInstance = await CreateStepInstanceAsync(instance, nextStep, userId, context);
        instance.CurrentStepId = nextStep.Id;
        await _workflowInstanceRepository.UpdateAsync(instance);
        await _workflowInstanceRepository.SaveChangesAsync();

        await _activityService.LogActivityAsync(
            instance.Id,
            WorkflowActivityType.TransitionTaken,
            "Transition taken",
            selectedTransition.Name,
            userId,
            nextStepInstance.Id,
            stepData);

        await HandleStepEntryAsync(instance, nextStepInstance, context, userId);

        return new WorkflowExecutionResult
        {
            Success = true,
            Status = instance.Status,
            WorkflowInstanceId = instance.Id,
            CurrentStepId = nextStep.Id,
            Message = "Workflow advanced"
        };
    }

    public async Task<WorkflowExecutionResult> ExecuteTransitionAsync(Guid workflowInstanceId, Guid userId, string transitionName, object? stepData = null)
    {
        if (string.IsNullOrWhiteSpace(transitionName))
        {
            throw new ArgumentException("Transition name is required.", nameof(transitionName));
        }

        var instance = await _workflowInstanceRepository.GetWithDetailsAsync(workflowInstanceId)
            ?? throw new InvalidOperationException($"Workflow instance {workflowInstanceId} not found");

        var currentStepInstance = await _workflowStepInstanceRepository.GetCurrentStepAsync(workflowInstanceId);
        if (currentStepInstance == null)
        {
            return await CompleteWorkflowIfPossibleAsync(instance, userId);
        }

        var transitions = await _workflowTransitionRepository.GetFromStepAsync(currentStepInstance.WorkflowStepId);
        var context = MergeDataContext(instance, stepData);

        var validTransitions = new List<WorkflowTransition>();
        foreach (var transition in transitions)
        {
            if (transition.Condition == null)
            {
                validTransitions.Add(transition);
                continue;
            }

            if (await EvaluateConditionAsync(transition.Condition, context))
            {
                validTransitions.Add(transition);
            }
        }

        var matches = validTransitions
            .Where(t => t.Name != null && t.Name.Equals(transitionName.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            throw new InvalidOperationException($"Transition '{transitionName}' is not available from the current step.");
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException($"Multiple transitions named '{transitionName}' are available. Use transitionId instead.");
        }

        return await ExecuteTransitionAsync(workflowInstanceId, userId, matches[0].Id, stepData);
    }

    public async Task<WorkflowExecutionResult> ProcessStepAsync(Guid workflowStepInstanceId, Guid userId, WorkflowStepAction action, object? resultData = null, string? comments = null)
    {
        var stepInstance = await _workflowStepInstanceRepository.GetByIdAsync(workflowStepInstanceId)
            ?? throw new InvalidOperationException($"Step instance {workflowStepInstanceId} not found");

        var instance = await _workflowInstanceRepository.GetWithDetailsAsync(stepInstance.WorkflowInstanceId)
            ?? throw new InvalidOperationException($"Workflow instance {stepInstance.WorkflowInstanceId} not found");

        var stepDefinition = stepInstance.WorkflowStep
            ?? await _workflowStepRepository.GetByIdAsync(stepInstance.WorkflowStepId)
            ?? throw new InvalidOperationException("Workflow step definition not found");

        if (stepDefinition.StepType == WorkflowStepType.Approval)
        {
            return await ProcessApprovalStepAsync(instance, stepInstance, stepDefinition, userId, action, resultData, comments);
        }

        if (action == WorkflowStepAction.RequestInformation)
        {
            stepInstance.Status = WorkflowStepInstanceStatus.InProgress;
            stepInstance.Comments = comments;
            await _workflowStepInstanceRepository.UpdateAsync(stepInstance);
            await _workflowStepInstanceRepository.SaveChangesAsync();

            instance.Status = WorkflowInstanceStatus.Waiting;
            await _workflowInstanceRepository.UpdateAsync(instance);
            await _workflowInstanceRepository.SaveChangesAsync();

            await _activityService.LogActivityAsync(
                instance.Id,
                WorkflowActivityType.CommentAdded,
                "Additional information requested",
                comments,
                userId,
                stepInstance.Id,
                resultData);

            return new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.Waiting,
                WorkflowInstanceId = instance.Id,
                CurrentStepId = stepDefinition.Id,
                Message = "Workflow paused for additional information"
            };
        }

        if (action == WorkflowStepAction.Reject)
        {
            stepInstance.Status = WorkflowStepInstanceStatus.Failed;
            stepInstance.CompletedDate = DateTime.UtcNow;
            stepInstance.Comments = comments;
            await _workflowStepInstanceRepository.UpdateAsync(stepInstance);
            await _workflowStepInstanceRepository.SaveChangesAsync();

            instance.Status = WorkflowInstanceStatus.Cancelled;
            instance.CompletedDate = DateTime.UtcNow;
            instance.Notes = comments ?? "Workflow rejected";
            await _workflowInstanceRepository.UpdateAsync(instance);
            await _workflowInstanceRepository.SaveChangesAsync();

            await _activityService.LogActivityAsync(
                instance.Id,
                WorkflowActivityType.StepFailed,
                "Step rejected",
                comments,
                userId,
                stepInstance.Id,
                resultData);

            return new WorkflowExecutionResult
            {
                Success = true,
                Status = instance.Status,
                WorkflowInstanceId = instance.Id,
                CurrentStepId = stepDefinition.Id,
                Message = "Workflow rejected"
            };
        }

        stepInstance.Status = WorkflowStepInstanceStatus.Completed;
        stepInstance.CompletedDate = DateTime.UtcNow;
        stepInstance.Comments = comments;
        stepInstance.ResultData = resultData != null ? JsonSerializer.Serialize(resultData) : stepInstance.ResultData;
        await _workflowStepInstanceRepository.UpdateAsync(stepInstance);
        await _workflowStepInstanceRepository.SaveChangesAsync();

        await _activityService.LogActivityAsync(
            instance.Id,
            WorkflowActivityType.StepCompleted,
            "Step completed",
            comments,
            userId,
            stepInstance.Id,
            resultData);

        return await AdvanceFromStepAsync(instance, stepDefinition, userId, resultData);
    }

    public async Task CancelWorkflowAsync(Guid workflowInstanceId, Guid userId, string reason)
    {
        var instance = await _workflowInstanceRepository.GetWithDetailsAsync(workflowInstanceId)
            ?? throw new InvalidOperationException($"Workflow instance {workflowInstanceId} not found");

        if (instance.Status == WorkflowInstanceStatus.Completed)
        {
            throw new InvalidOperationException("Cannot cancel a completed workflow");
        }

        var stepInstances = await _workflowStepInstanceRepository.GetByWorkflowInstanceAsync(workflowInstanceId);
        foreach (var stepInstance in stepInstances.Where(si => si.Status == WorkflowStepInstanceStatus.Pending || si.Status == WorkflowStepInstanceStatus.InProgress))
        {
            stepInstance.Status = WorkflowStepInstanceStatus.Cancelled;
            stepInstance.CompletedDate = DateTime.UtcNow;
            stepInstance.Comments = reason;
            await _workflowStepInstanceRepository.UpdateAsync(stepInstance);
        }

        await _workflowStepInstanceRepository.SaveChangesAsync();

        var approvals = await _workflowApprovalRepository.GetByStatusAsync(WorkflowApprovalStatus.Pending, instance.TenantId);
        foreach (var approval in approvals.Where(a => a.StepInstance.WorkflowInstanceId == instance.Id))
        {
            approval.Status = WorkflowApprovalStatus.Expired;
            approval.ProcessedDate = DateTime.UtcNow;
            approval.Comments = reason;
            await _workflowApprovalRepository.UpdateAsync(approval);
        }

        await _workflowApprovalRepository.SaveChangesAsync();

        instance.Status = WorkflowInstanceStatus.Cancelled;
        instance.CompletedDate = DateTime.UtcNow;
        instance.Notes = reason;
        await _workflowInstanceRepository.UpdateAsync(instance);
        await _workflowInstanceRepository.SaveChangesAsync();

        await _activityService.LogActivityAsync(
            instance.Id,
            WorkflowActivityType.WorkflowCancelled,
            "Workflow cancelled",
            reason,
            userId);
    }

    public async Task<IEnumerable<WorkflowTransition>> GetAvailableTransitionsAsync(Guid workflowInstanceId, object? dataContext = null)
    {
        var instance = await _workflowInstanceRepository.GetWithDetailsAsync(workflowInstanceId)
            ?? throw new InvalidOperationException($"Workflow instance {workflowInstanceId} not found");

        var currentStepInstance = await _workflowStepInstanceRepository.GetCurrentStepAsync(workflowInstanceId);
        if (currentStepInstance == null)
        {
            return Enumerable.Empty<WorkflowTransition>();
        }

        var transitions = await _workflowTransitionRepository.GetFromStepAsync(currentStepInstance.WorkflowStepId);
        var context = MergeDataContext(instance, dataContext);

        var validTransitions = new List<WorkflowTransition>();
        foreach (var transition in transitions)
        {
            if (transition.Condition == null)
            {
                validTransitions.Add(transition);
                continue;
            }

            if (await EvaluateConditionAsync(transition.Condition, context))
            {
                validTransitions.Add(transition);
            }
        }

        return validTransitions.OrderByDescending(t => t.Priority).ToList();
    }

    public async Task<WorkflowStatusDto> GetWorkflowStatusAsync(Guid workflowInstanceId)
    {
        var instance = await _workflowInstanceRepository.GetWithDetailsAsync(workflowInstanceId)
            ?? throw new InvalidOperationException($"Workflow instance {workflowInstanceId} not found");

        var steps = instance.StepInstances?.OrderBy(si => si.CreatedDate).ToList() ?? new List<WorkflowStepInstance>();
        var totalSteps = instance.WorkflowDefinition?.Steps?.Count ?? steps.Count;
        var completedSteps = steps.Count(si => si.Status == WorkflowStepInstanceStatus.Completed);

        var approvals = await _workflowApprovalRepository.GetByStatusAsync(WorkflowApprovalStatus.Pending, instance.TenantId);
        var pendingApprovals = approvals.Where(a => a.StepInstance.WorkflowInstanceId == instance.Id).ToList();

        return new WorkflowStatusDto
        {
            WorkflowInstanceId = instance.Id,
            WorkflowName = instance.WorkflowDefinition?.Name ?? "Workflow",
            EntityId = instance.EntityId,
            EntityType = instance.EntityType?.Name ?? "Unknown",
            Status = instance.Status,
            StartedDate = instance.StartedDate ?? instance.CreatedDate,
            CompletedDate = instance.CompletedDate,
            Progress = new WorkflowProgressDto
            {
                TotalSteps = totalSteps,
                CompletedSteps = completedSteps,
                PercentComplete = totalSteps == 0 ? 0 : (int)Math.Round((double)completedSteps / totalSteps * 100)
            },
            Steps = steps.Select(si => new WorkflowStepStatusDto
            {
                StepInstanceId = si.Id,
                StepName = si.WorkflowStep?.Name ?? "Step",
                Status = si.Status,
                AssignedToId = si.AssignedToId,
                StartedDate = si.StartedDate,
                CompletedDate = si.CompletedDate,
                DueDate = si.DueDate,
                IsOverdue = si.DueDate.HasValue && si.DueDate.Value < DateTime.UtcNow && si.Status == WorkflowStepInstanceStatus.Pending
            }).ToList(),
            PendingApprovals = pendingApprovals.Select(a => new WorkflowApprovalStatusDto
            {
                ApprovalId = a.Id,
                StepName = a.StepInstance.WorkflowStep?.Name ?? "Approval",
                ApproverId = a.ApproverId ?? Guid.Empty,
                ApproverName = a.Approver?.UserName ?? a.ApproverRole ?? "Unassigned",
                Status = a.Status,
                RequestedDate = a.RequestedDate,
                DueDate = a.DueDate,
                IsOverdue = a.DueDate.HasValue && a.DueDate.Value < DateTime.UtcNow && a.Status == WorkflowApprovalStatus.Pending
            }).ToList()
        };
    }

    public async Task EnsureApprovalsForStepAsync(Guid workflowStepInstanceId, object? dataContext = null)
    {
        var stepInstance = await _workflowStepInstanceRepository.GetByIdAsync(workflowStepInstanceId)
            ?? throw new InvalidOperationException($"Step instance {workflowStepInstanceId} not found");

        var instance = await _workflowInstanceRepository.GetWithDetailsAsync(stepInstance.WorkflowInstanceId)
            ?? throw new InvalidOperationException($"Workflow instance {stepInstance.WorkflowInstanceId} not found");

        var stepDefinition = stepInstance.WorkflowStep
            ?? await _workflowStepRepository.GetByIdAsync(stepInstance.WorkflowStepId)
            ?? throw new InvalidOperationException("Workflow step definition not found");

        if (stepDefinition.StepType != WorkflowStepType.Approval)
        {
            return;
        }

        var existingApprovals = (await _workflowApprovalRepository.GetByStepInstanceAsync(stepInstance.Id)).ToList();
        if (existingApprovals.Count > 0)
        {
            return;
        }

        var config = DeserializeStepConfig(stepDefinition.Configuration);
        if (config?.ApprovalConfig == null)
        {
            return;
        }

        var context = MergeDataContext(instance, dataContext);
        await CreateApprovalsAsync(instance, stepInstance, stepDefinition, config, context);
    }

    private async Task<WorkflowExecutionResult> ProcessApprovalStepAsync(
        WorkflowInstance instance,
        WorkflowStepInstance stepInstance,
        WorkflowStep stepDefinition,
        Guid userId,
        WorkflowStepAction action,
        object? resultData,
        string? comments)
    {
        var approvals = (await _workflowApprovalRepository.GetByStepInstanceAsync(stepInstance.Id)).ToList();
        if (approvals.Count == 0)
        {
            // Self-heal older instances where approval rows were not created at step entry time.
            await EnsureApprovalsForStepAsync(stepInstance.Id);
            approvals = (await _workflowApprovalRepository.GetByStepInstanceAsync(stepInstance.Id)).ToList();
        }

        if (approvals.Count == 0)
        {
            return new WorkflowExecutionResult
            {
                Success = false,
                Status = WorkflowInstanceStatus.Failed,
                WorkflowInstanceId = instance.Id,
                Message = "No approvals configured for this step"
            };
        }

        var approval = approvals.FirstOrDefault(a => a.ApproverId == userId && a.Status == WorkflowApprovalStatus.Pending);
        if (approval == null)
        {
            var roleSet = new HashSet<string>(_currentUserService.Roles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            approval = approvals.FirstOrDefault(a =>
                a.Status == WorkflowApprovalStatus.Pending &&
                !string.IsNullOrWhiteSpace(a.ApproverRole) &&
                roleSet.Contains(a.ApproverRole!));
        }

        if (approval == null)
        {
            return new WorkflowExecutionResult
            {
                Success = false,
                Status = instance.Status,
                WorkflowInstanceId = instance.Id,
                Message = "User does not have a pending approval for this step"
            };
        }

        if (action == WorkflowStepAction.RequestInformation)
        {
            approval.Status = WorkflowApprovalStatus.MoreInfoRequested;
            approval.ProcessedById = userId;
            approval.ProcessedDate = DateTime.UtcNow;
            approval.Comments = comments;
            await _workflowApprovalRepository.UpdateAsync(approval);
            await _workflowApprovalRepository.SaveChangesAsync();

            instance.Status = WorkflowInstanceStatus.Waiting;
            await _workflowInstanceRepository.UpdateAsync(instance);
            await _workflowInstanceRepository.SaveChangesAsync();

            await _activityService.LogActivityAsync(
                instance.Id,
                WorkflowActivityType.ApprovalMoreInfoRequested,
                "More information requested",
                comments,
                userId,
                stepInstance.Id,
                resultData);

            return new WorkflowExecutionResult
            {
                Success = true,
                Status = instance.Status,
                WorkflowInstanceId = instance.Id,
                CurrentStepId = stepDefinition.Id,
                Message = "More information requested"
            };
        }

        approval.Status = action == WorkflowStepAction.Reject
            ? WorkflowApprovalStatus.Rejected
            : WorkflowApprovalStatus.Approved;
        approval.ProcessedById = userId;
        approval.ProcessedDate = DateTime.UtcNow;
        approval.Comments = comments;

        await _workflowApprovalRepository.UpdateAsync(approval);
        await _workflowApprovalRepository.SaveChangesAsync();

        var activityType = action == WorkflowStepAction.Reject
            ? WorkflowActivityType.ApprovalRejected
            : WorkflowActivityType.ApprovalApproved;

        await _activityService.LogActivityAsync(
            instance.Id,
            activityType,
            $"Approval {approval.Status}",
            comments,
            userId,
            stepInstance.Id,
            resultData);

        var config = DeserializeStepConfig(stepDefinition.Configuration);
        if (action == WorkflowStepAction.Reject)
        {
            return await HandleRejectionAsync(instance, stepInstance, stepDefinition, config, userId, comments);
        }

        var approvalConfig = config?.ApprovalConfig;
        var shouldComplete = approvalConfig == null || IsApprovalSatisfied(approvalConfig, approvals);
        if (!shouldComplete)
        {
            return new WorkflowExecutionResult
            {
                Success = true,
                Status = instance.Status,
                WorkflowInstanceId = instance.Id,
                CurrentStepId = stepDefinition.Id,
                Message = "Approval recorded"
            };
        }

        stepInstance.Status = WorkflowStepInstanceStatus.Completed;
        stepInstance.CompletedDate = DateTime.UtcNow;
        stepInstance.Comments = comments;
        stepInstance.ResultData = resultData != null ? JsonSerializer.Serialize(resultData) : stepInstance.ResultData;
        await _workflowStepInstanceRepository.UpdateAsync(stepInstance);
        await _workflowStepInstanceRepository.SaveChangesAsync();

        await _activityService.LogActivityAsync(
            instance.Id,
            WorkflowActivityType.StepCompleted,
            "Approval step completed",
            comments,
            userId,
            stepInstance.Id,
            resultData);

        return await AdvanceFromStepAsync(instance, stepDefinition, userId, resultData);
    }

    private async Task<WorkflowExecutionResult> HandleRejectionAsync(
        WorkflowInstance instance,
        WorkflowStepInstance stepInstance,
        WorkflowStep stepDefinition,
        WorkflowStepConfigurationDto? config,
        Guid userId,
        string? comments)
    {
        var rejectionHandling = config?.ApprovalConfig?.RejectionHandling ?? WorkflowRejectionHandling.StopWorkflow;

        if (rejectionHandling == WorkflowRejectionHandling.StopWorkflow)
        {
            instance.Status = WorkflowInstanceStatus.Cancelled;
            instance.CompletedDate = DateTime.UtcNow;
            instance.Notes = comments ?? "Workflow rejected";
            await _workflowInstanceRepository.UpdateAsync(instance);
            await _workflowInstanceRepository.SaveChangesAsync();

            await _notificationService.SendWorkflowRejectedNotificationAsync(instance.Id, userId, comments);

            return new WorkflowExecutionResult
            {
                Success = true,
                Status = instance.Status,
                WorkflowInstanceId = instance.Id,
                Message = "Workflow rejected"
            };
        }

        if (rejectionHandling == WorkflowRejectionHandling.ContinueToNextStep)
        {
            stepInstance.Status = WorkflowStepInstanceStatus.Completed;
            stepInstance.CompletedDate = DateTime.UtcNow;
            await _workflowStepInstanceRepository.UpdateAsync(stepInstance);
            await _workflowStepInstanceRepository.SaveChangesAsync();

            return await AdvanceFromStepAsync(instance, stepDefinition, userId, null);
        }

        var steps = instance.WorkflowDefinition?.Steps?.OrderBy(s => s.Order).ToList() ?? new List<WorkflowStep>();
        var targetStep = rejectionHandling == WorkflowRejectionHandling.ReturnToStart
            ? steps.FirstOrDefault(s => s.IsStartStep)
            : steps.LastOrDefault(s => s.Order < stepDefinition.Order);

        if (targetStep == null)
        {
            instance.Status = WorkflowInstanceStatus.Cancelled;
            instance.CompletedDate = DateTime.UtcNow;
            instance.Notes = "Workflow rejected";
            await _workflowInstanceRepository.UpdateAsync(instance);
            await _workflowInstanceRepository.SaveChangesAsync();

            await _notificationService.SendWorkflowRejectedNotificationAsync(instance.Id, userId, comments);

            return new WorkflowExecutionResult
            {
                Success = true,
                Status = instance.Status,
                WorkflowInstanceId = instance.Id,
                Message = "Workflow rejected"
            };
        }

        var newStepInstance = await CreateStepInstanceAsync(instance, targetStep, userId, MergeDataContext(instance, null));
        instance.CurrentStepId = targetStep.Id;
        await _workflowInstanceRepository.UpdateAsync(instance);
        await _workflowInstanceRepository.SaveChangesAsync();

        await HandleStepEntryAsync(instance, newStepInstance, MergeDataContext(instance, null), userId);

        return new WorkflowExecutionResult
        {
            Success = true,
            Status = instance.Status,
            WorkflowInstanceId = instance.Id,
            CurrentStepId = targetStep.Id,
            Message = "Workflow returned to previous step"
        };
    }

    private async Task<WorkflowExecutionResult> AdvanceFromStepAsync(WorkflowInstance instance, WorkflowStep? stepDefinition, Guid userId, object? stepData)
    {
        if (stepDefinition == null)
        {
            return await CompleteWorkflowIfPossibleAsync(instance, userId);
        }

        var transitions = await _workflowTransitionRepository.GetFromStepAsync(stepDefinition.Id);
        var context = MergeDataContext(instance, stepData);

        var validTransitions = new List<WorkflowTransition>();
        foreach (var transition in transitions)
        {
            if (transition.Condition == null)
            {
                validTransitions.Add(transition);
                continue;
            }

            if (await EvaluateConditionAsync(transition.Condition, context))
            {
                validTransitions.Add(transition);
            }
        }

        var nextTransition = validTransitions.OrderByDescending(t => t.Priority).FirstOrDefault()
            ?? transitions.FirstOrDefault(t => t.IsDefault)
            ?? transitions.OrderByDescending(t => t.Priority).FirstOrDefault();

        if (nextTransition == null)
        {
            return await CompleteWorkflowIfPossibleAsync(instance, userId);
        }

        var nextStep = nextTransition.ToStep ?? await _workflowStepRepository.GetByIdAsync(nextTransition.ToStepId)
            ?? throw new InvalidOperationException("Next step not found");

        var nextStepInstance = await CreateStepInstanceAsync(instance, nextStep, userId, context);
        instance.CurrentStepId = nextStep.Id;
        await _workflowInstanceRepository.UpdateAsync(instance);
        await _workflowInstanceRepository.SaveChangesAsync();

        await _activityService.LogActivityAsync(
            instance.Id,
            WorkflowActivityType.TransitionTaken,
            "Transition taken",
            nextTransition.Name,
            userId,
            nextStepInstance.Id,
            stepData);

        await HandleStepEntryAsync(instance, nextStepInstance, context, userId);

        return new WorkflowExecutionResult
        {
            Success = true,
            Status = instance.Status,
            WorkflowInstanceId = instance.Id,
            CurrentStepId = nextStep.Id,
            Message = "Workflow advanced"
        };
    }

    private async Task HandleStepEntryAsync(WorkflowInstance instance, WorkflowStepInstance stepInstance, object? dataContext, Guid userId)
    {
        var stepDefinition = stepInstance.WorkflowStep
            ?? await _workflowStepRepository.GetByIdAsync(stepInstance.WorkflowStepId)
            ?? throw new InvalidOperationException("Workflow step definition not found");

        var config = DeserializeStepConfig(stepDefinition.Configuration);
        var context = MergeDataContext(instance, dataContext);

        if (config?.SkipCondition != null && await EvaluateConditionAsync(config.SkipCondition, context))
        {
            stepInstance.Status = WorkflowStepInstanceStatus.Completed;
            stepInstance.CompletedDate = DateTime.UtcNow;
            stepInstance.Comments = "Skipped by condition";
            await _workflowStepInstanceRepository.UpdateAsync(stepInstance);
            await _workflowStepInstanceRepository.SaveChangesAsync();

            await _activityService.LogActivityAsync(
                instance.Id,
                WorkflowActivityType.StepSkipped,
                "Step skipped",
                "Skip condition met",
                userId,
                stepInstance.Id,
                dataContext);

            await AdvanceFromStepAsync(instance, stepDefinition, userId, dataContext);
            return;
        }

        if (stepDefinition.StepType == WorkflowStepType.Approval)
        {
            if (config?.ApprovalConfig?.AutoApprovalCondition != null &&
                await EvaluateConditionAsync(config.ApprovalConfig.AutoApprovalCondition, context))
            {
                stepInstance.Status = WorkflowStepInstanceStatus.Completed;
                stepInstance.CompletedDate = DateTime.UtcNow;
                await _workflowStepInstanceRepository.UpdateAsync(stepInstance);
                await _workflowStepInstanceRepository.SaveChangesAsync();

                await _activityService.LogActivityAsync(
                    instance.Id,
                    WorkflowActivityType.StepCompleted,
                    "Approval auto-approved",
                    "Auto-approval condition met",
                    userId,
                    stepInstance.Id,
                    dataContext);

                await AdvanceFromStepAsync(instance, stepDefinition, userId, dataContext);
                return;
            }

            await CreateApprovalsAsync(instance, stepInstance, stepDefinition, config, context);
            return;
        }

        if (stepDefinition.StepType != WorkflowStepType.Manual)
        {
            stepInstance.Status = WorkflowStepInstanceStatus.Completed;
            stepInstance.CompletedDate = DateTime.UtcNow;
            await _workflowStepInstanceRepository.UpdateAsync(stepInstance);
            await _workflowStepInstanceRepository.SaveChangesAsync();

            await _activityService.LogActivityAsync(
                instance.Id,
                WorkflowActivityType.StepCompleted,
                "Automatic step completed",
                stepDefinition.Name,
                userId,
                stepInstance.Id,
                dataContext);

            await AdvanceFromStepAsync(instance, stepDefinition, userId, dataContext);
        }
    }

    private async Task CreateApprovalsAsync(
        WorkflowInstance instance,
        WorkflowStepInstance stepInstance,
        WorkflowStep stepDefinition,
        WorkflowStepConfigurationDto? config,
        Dictionary<string, object> context)
    {
        var approvalConfig = config?.ApprovalConfig;
        if (approvalConfig == null)
        {
            return;
        }

        var approvers = await ResolveApproversAsync(approvalConfig, context, stepInstance);
        foreach (var approver in approvers)
        {
            var approval = new WorkflowApproval
            {
                Id = Guid.NewGuid(),
                StepInstanceId = stepInstance.Id,
                ApproverId = approver.UserId,
                ApproverRole = approver.Role,
                Status = WorkflowApprovalStatus.Pending,
                RequestedDate = DateTime.UtcNow,
                DueDate = stepDefinition.EstimatedHours.HasValue
                    ? DateTime.UtcNow.AddHours(stepDefinition.EstimatedHours.Value)
                    : null,
                TenantId = instance.TenantId
            };

            await _workflowApprovalRepository.AddAsync(approval);
            await _workflowApprovalRepository.SaveChangesAsync();

            await _activityService.LogActivityAsync(
                instance.Id,
                WorkflowActivityType.ApprovalRequested,
                "Approval requested",
                $"Approval requested for step '{stepDefinition.Name}'",
                null,
                stepInstance.Id);

            // Notify either direct user approvers OR role-based approvers.
            if (approver.UserId.HasValue || !string.IsNullOrWhiteSpace(approver.Role))
            {
                await _notificationService.SendApprovalRequestNotificationAsync(approval.Id);
            }
        }
    }

    private async Task<List<(Guid? UserId, string? Role)>> ResolveApproversAsync(
        WorkflowApprovalConfigDto approvalConfig,
        Dictionary<string, object> context,
        WorkflowStepInstance stepInstance)
    {
        var approvers = new List<(Guid? UserId, string? Role)>();
        var rules = approvalConfig.ApproverRules
            .OrderByDescending(r => r.Priority)
            .ToList();

        foreach (var rule in rules)
        {
            if (rule.Condition != null && !await EvaluateConditionAsync(rule.Condition, context))
            {
                continue;
            }

            switch (rule.AssignmentType)
            {
                case WorkflowAssignmentType.User:
                    if (rule.UserId.HasValue)
                    {
                        approvers.Add((rule.UserId.Value, null));
                    }
                    break;
                case WorkflowAssignmentType.Role:
                    if (!string.IsNullOrWhiteSpace(rule.Role))
                    {
                        approvers.Add((null, rule.Role));
                    }
                    break;
                case WorkflowAssignmentType.Dynamic:
                    if (!string.IsNullOrWhiteSpace(rule.DynamicExpression) &&
                        TryResolveGuidFromContext(context, rule.DynamicExpression!, out var dynamicUserId))
                    {
                        approvers.Add((dynamicUserId, null));
                    }
                    break;
                case WorkflowAssignmentType.RequestorManager:
                    if (TryResolveGuidFromContext(context, "requestorManagerId", out var managerId))
                    {
                        approvers.Add((managerId, null));
                    }
                    break;
                case WorkflowAssignmentType.PreviousStepUser:
                    if (stepInstance.AssignedToId.HasValue)
                    {
                        approvers.Add((stepInstance.AssignedToId.Value, null));
                    }
                    break;
                default:
                    break;
            }
        }

        return approvers.Distinct().ToList();
    }

    private static bool IsApprovalSatisfied(WorkflowApprovalConfigDto approvalConfig, List<WorkflowApproval> approvals)
    {
        var totalApprovals = approvals.Count;
        var approvedCount = approvals.Count(a => a.Status == WorkflowApprovalStatus.Approved);
        var rejectedCount = approvals.Count(a => a.Status == WorkflowApprovalStatus.Rejected);

        if (rejectedCount > 0 && approvalConfig.RejectionHandling == WorkflowRejectionHandling.StopWorkflow)
        {
            return false;
        }

        var minRequired = Math.Max(approvalConfig.MinApprovalsRequired, 1);

        return approvalConfig.ApprovalType switch
        {
            WorkflowApprovalType.Single => approvedCount >= minRequired,
            WorkflowApprovalType.Multiple => approvedCount >= Math.Max(minRequired, totalApprovals),
            WorkflowApprovalType.Consensus => approvedCount == totalApprovals,
            WorkflowApprovalType.Majority => approvedCount >= Math.Max(minRequired, (int)Math.Ceiling(totalApprovals / 2.0)),
            _ => approvedCount >= minRequired
        };
    }

    private async Task<WorkflowStepInstance> CreateStepInstanceAsync(
        WorkflowInstance instance,
        WorkflowStep stepDefinition,
        Guid assignedById,
        object? dataContext)
    {
        var config = DeserializeStepConfig(stepDefinition.Configuration);
        var context = MergeDataContext(instance, dataContext);
        var assignedToId = await ResolveStepAssignmentAsync(stepDefinition, config, context, assignedById);

        // Approval steps are owned by their approver list (WorkflowApprovals), not by "AssignedTo".
        // Leaving AssignedToId populated (defaulting to the previous actor) can accidentally grant approval
        // via fallback logic and is misleading in monitoring UIs.
        if (stepDefinition.StepType == WorkflowStepType.Approval && (config?.AssignmentRules == null || config.AssignmentRules.Count == 0))
        {
            assignedToId = null;
        }

        var stepInstance = new WorkflowStepInstance
        {
            Id = Guid.NewGuid(),
            WorkflowInstanceId = instance.Id,
            WorkflowStepId = stepDefinition.Id,
            Status = WorkflowStepInstanceStatus.Pending,
            StartedDate = DateTime.UtcNow,
            AssignedToId = assignedToId,
            DueDate = stepDefinition.EstimatedHours.HasValue
                ? DateTime.UtcNow.AddHours(stepDefinition.EstimatedHours.Value)
                : null,
            TenantId = instance.TenantId
        };

        await _workflowStepInstanceRepository.AddAsync(stepInstance);
        await _workflowStepInstanceRepository.SaveChangesAsync();

        if (assignedToId.HasValue)
        {
            await _notificationService.SendStepAssignmentNotificationAsync(stepInstance.Id, assignedToId.Value);
        }

        return stepInstance;
    }

    private async Task<Guid?> ResolveStepAssignmentAsync(
        WorkflowStep stepDefinition,
        WorkflowStepConfigurationDto? config,
        Dictionary<string, object> context,
        Guid defaultUserId)
    {
        if (config?.AssignmentRules != null)
        {
            foreach (var rule in config.AssignmentRules.OrderByDescending(r => r.Priority))
            {
                if (rule.Condition != null && !await EvaluateConditionAsync(rule.Condition, context))
                {
                    continue;
                }

                switch (rule.AssignmentType)
                {
                    case WorkflowAssignmentType.User:
                        if (rule.UserId.HasValue)
                        {
                            return rule.UserId;
                        }
                        break;
                    case WorkflowAssignmentType.Dynamic:
                        if (!string.IsNullOrWhiteSpace(rule.DynamicExpression) &&
                            TryResolveGuidFromContext(context, rule.DynamicExpression!, out var dynamicUserId))
                        {
                            return dynamicUserId;
                        }
                        break;
                    case WorkflowAssignmentType.RequestorManager:
                        if (TryResolveGuidFromContext(context, "requestorManagerId", out var managerId))
                        {
                            return managerId;
                        }
                        break;
                    case WorkflowAssignmentType.PreviousStepUser:
                        if (TryResolveGuidFromContext(context, "previousStepUserId", out var previousUserId))
                        {
                            return previousUserId;
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(stepDefinition.AssignmentType))
        {
            if (stepDefinition.AssignmentType.Equals("User", StringComparison.OrdinalIgnoreCase) &&
                Guid.TryParse(stepDefinition.AssignmentConfiguration, out var assignedId))
            {
                return assignedId;
            }
        }

        return defaultUserId;
    }

    private async Task<WorkflowExecutionResult> CompleteWorkflowIfPossibleAsync(WorkflowInstance instance, Guid userId)
    {
        if (instance.Status == WorkflowInstanceStatus.Completed)
        {
            return new WorkflowExecutionResult
            {
                Success = true,
                Status = instance.Status,
                WorkflowInstanceId = instance.Id,
                Message = "Workflow already completed"
            };
        }

        instance.Status = WorkflowInstanceStatus.Completed;
        instance.CompletedDate = DateTime.UtcNow;
        await _workflowInstanceRepository.UpdateAsync(instance);
        await _workflowInstanceRepository.SaveChangesAsync();

        await _notificationService.SendWorkflowCompletionNotificationAsync(instance.Id);
        await _activityService.LogActivityAsync(
            instance.Id,
            WorkflowActivityType.WorkflowCompleted,
            "Workflow completed",
            null,
            userId);

        return new WorkflowExecutionResult
        {
            Success = true,
            Status = instance.Status,
            WorkflowInstanceId = instance.Id,
            Message = "Workflow completed"
        };
    }

    private async Task<bool> EvaluateConditionAsync(string conditionJson, Dictionary<string, object> context)
    {
        try
        {
            var condition = JsonSerializer.Deserialize<WorkflowConditionDto>(conditionJson);
            if (condition == null)
            {
                return true;
            }

            return await EvaluateConditionAsync(condition, context);
        }
        catch (JsonException)
        {
            return await _conditionEvaluator.EvaluateConditionAsync(conditionJson, context);
        }
    }

    private async Task<bool> EvaluateConditionAsync(WorkflowConditionDto condition, Dictionary<string, object> context)
    {
        if (condition.ConditionType == WorkflowConditionType.Always)
        {
            return true;
        }

        if (condition.ConditionType == WorkflowConditionType.Never)
        {
            return false;
        }

        if (condition.ChildConditions?.Any() == true)
        {
            var childResults = new List<bool>();
            foreach (var child in condition.ChildConditions)
            {
                childResults.Add(await EvaluateConditionAsync(child, context));
            }

            return condition.LogicalOperator switch
            {
                WorkflowLogicalOperator.And => childResults.All(r => r),
                WorkflowLogicalOperator.Or => childResults.Any(r => r),
                WorkflowLogicalOperator.Not => !childResults.Any(r => r),
                _ => childResults.All(r => r)
            };
        }

        var mergedContext = new Dictionary<string, object>(context, StringComparer.OrdinalIgnoreCase);
        if (condition.Variables != null)
        {
            foreach (var kvp in condition.Variables)
            {
                mergedContext[kvp.Key] = kvp.Value ?? string.Empty;
            }
        }

        return await _conditionEvaluator.EvaluateConditionAsync(condition.Expression, mergedContext);
    }

    private static WorkflowStepConfigurationDto? DeserializeStepConfig(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return null;
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return JsonSerializer.Deserialize<WorkflowStepConfigurationDto>(configurationJson, options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Dictionary<string, object> MergeDataContext(WorkflowInstance instance, object? dataContext)
    {
        var merged = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        var storedData = instance.DataContext ?? instance.Data;
        if (!string.IsNullOrWhiteSpace(storedData))
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(storedData);
            if (parsed != null)
            {
                foreach (var kvp in parsed)
                {
                    merged[kvp.Key] = ConvertJsonElement(kvp.Value) ?? string.Empty;
                }
            }
        }

        if (dataContext is Dictionary<string, object> dict)
        {
            foreach (var kvp in dict)
            {
                merged[kvp.Key] = kvp.Value ?? string.Empty;
            }
        }
        else if (dataContext != null)
        {
            var json = JsonSerializer.Serialize(dataContext);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (parsed != null)
            {
                foreach (var kvp in parsed)
                {
                    merged[kvp.Key] = ConvertJsonElement(kvp.Value) ?? string.Empty;
                }
            }
        }

        return merged;
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetDecimal(out var dec) ? dec : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
            _ => null
        };
    }

    private static bool TryResolveGuidFromContext(Dictionary<string, object> context, string key, out Guid value)
    {
        if (context.TryGetValue(key, out var rawValue))
        {
            switch (rawValue)
            {
                case Guid guid:
                    value = guid;
                    return true;
                case string str when Guid.TryParse(str, out var parsed):
                    value = parsed;
                    return true;
            }
        }

        value = Guid.Empty;
        return false;
    }
}
