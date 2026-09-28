using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Interfaces.Services;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Core.Interfaces.Procurement;
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
    private readonly IWorkflowApprovalPolicyResolver _approvalPolicyResolver;
    private readonly IWorkflowRuntimeGovernanceService _runtimeGovernance;
    private readonly IWorkflowSignatureSubmissionStore _signatureStore;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<WorkflowEngine> _logger;
    private readonly IProcurementSodPolicy? _sodPolicy;

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
        IWorkflowApprovalPolicyResolver approvalPolicyResolver,
        IWorkflowRuntimeGovernanceService runtimeGovernance,
        IWorkflowSignatureSubmissionStore signatureStore,
        ICurrentUserService currentUserService,
        ILogger<WorkflowEngine> logger,
        IProcurementSodPolicy? sodPolicy = null)
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
        _approvalPolicyResolver = approvalPolicyResolver;
        _runtimeGovernance = runtimeGovernance;
        _signatureStore = signatureStore;
        _currentUserService = currentUserService;
        _logger = logger;
        _sodPolicy = sodPolicy;
    }

    public async Task<WorkflowInstance> StartWorkflowAsync(string workflowName, Guid entityId, Guid initiatedById, object? dataContext = null)
    {
        _logger.LogInformation("Starting workflow {WorkflowName} for entity {EntityId}", workflowName, entityId);

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var definition = await _workflowDefinitionRepository.GetByNameAsync(workflowName, tenantId)
            ?? throw new InvalidOperationException($"Workflow definition '{workflowName}' not found");

        return await StartWorkflowAsync(definition, entityId, initiatedById, dataContext);
    }

    public async Task<WorkflowInstance> StartWorkflowAsync(
        Guid workflowDefinitionId,
        Guid entityId,
        Guid initiatedById,
        object? dataContext = null)
    {
        _logger.LogInformation(
            "Starting workflow definition {WorkflowDefinitionId} for entity {EntityId}",
            workflowDefinitionId,
            entityId);
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var definition = await _workflowDefinitionRepository.GetWithDetailsAsync(workflowDefinitionId)
            ?? throw new InvalidOperationException($"Workflow definition '{workflowDefinitionId}' not found");
        if (definition.TenantId != tenantId)
            throw new InvalidOperationException("Workflow definition does not belong to the current tenant");

        return await StartWorkflowAsync(definition, entityId, initiatedById, dataContext);
    }

    private async Task<WorkflowInstance> StartWorkflowAsync(
        WorkflowDefinition definition,
        Guid entityId,
        Guid initiatedById,
        object? dataContext)
    {
        var workflowName = definition.Name;

        if (!WorkflowDefinitionLifecyclePolicy.IsRuntimeEligible(definition))
        {
            throw new InvalidOperationException($"Workflow definition '{workflowName}' is not published and active");
        }

        var startStep = await _workflowStepRepository.GetStartStepAsync(definition.Id)
            ?? throw new InvalidOperationException($"No start step found for workflow '{workflowName}'");

        // Validate the immutable route before writing any workflow records. This prevents a maker
        // from starting a workflow that can never reach completion because the only configured
        // approver is excluded by maker-checker or another actor-eligibility rule.
        var workflowSteps = (await _workflowStepRepository.GetByWorkflowDefinitionAsync(definition.Id)).ToList();
        await _runtimeGovernance.EnsureMandatoryApprovalActorsAvailableAsync(
            definition.TenantId,
            initiatedById,
            workflowName,
            workflowSteps);

        var workflowInstance = new WorkflowInstance
        {
            Id = Guid.NewGuid(),
            WorkflowDefinitionId = definition.Id,
            WorkflowDefinition = definition,
            EntityId = entityId,
            EntityTypeId = definition.EntityTypeId,
            EntityType = definition.EntityType,
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

        var config = DeserializeStepConfig(stepDefinition.Configuration);
        if (stepDefinition.StepType == WorkflowStepType.Manual &&
            !CanCurrentUserActOnManualStep(stepInstance, stepDefinition, config, userId))
        {
            throw new UnauthorizedAccessException("User does not have permission to complete this workflow task.");
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

        if (action == WorkflowStepAction.Complete)
        {
            var taskRequirementErrors = ValidateTaskCompletionRequirements(config, stepInstance.ResultData);
            if (taskRequirementErrors.Count > 0)
            {
                return new WorkflowExecutionResult
                {
                    Success = false,
                    Status = instance.Status,
                    WorkflowInstanceId = instance.Id,
                    CurrentStepId = stepDefinition.Id,
                    Errors = taskRequirementErrors.Select(message => new WorkflowExecutionError
                    {
                        Code = "TaskRequirementNotSatisfied",
                        Message = message,
                        StepId = stepDefinition.Id
                    }).ToList(),
                    Message = string.Join(" ", taskRequirementErrors)
                };
            }
        }

        stepInstance.Status = WorkflowStepInstanceStatus.Completed;
        stepInstance.CompletedDate = DateTime.UtcNow;
        stepInstance.Comments = comments;
        stepInstance.ResultData = MergeStepResultData(stepInstance.ResultData, resultData);
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

        var cancelledAt = DateTime.UtcNow;
        instance.Status = WorkflowInstanceStatus.Cancelled;
        // Retain CompletedDate for existing terminal-state consumers, while recording
        // cancellation in the dedicated field used by workflow audit and reporting.
        instance.CompletedDate = cancelledAt;
        instance.CancelledDate = cancelledAt;
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
        var currentStep = ResolveCurrentStep(instance, steps);
        var currentStepDefinition = ResolveCurrentStepDefinition(instance, currentStep, steps);
        var totalSteps = instance.WorkflowDefinition?.Steps?.Count ?? steps.Count;
        var completedSteps = steps.Count(si => si.Status == WorkflowStepInstanceStatus.Completed);
        var isTerminalInstance =
            instance.Status == WorkflowInstanceStatus.Completed ||
            instance.Status == WorkflowInstanceStatus.Cancelled ||
            instance.Status == WorkflowInstanceStatus.Failed;

        var approvals = isTerminalInstance
            ? new List<WorkflowApproval>()
            : (await _workflowApprovalRepository.GetByStatusAsync(WorkflowApprovalStatus.Pending, instance.TenantId)).ToList();
        var pendingApprovals = approvals.Where(a => a.StepInstance.WorkflowInstanceId == instance.Id).ToList();
        var currentStepPendingApprovals = currentStep == null
            ? pendingApprovals
            : pendingApprovals.Where(a => a.StepInstanceId == currentStep.Id).ToList();

        if (!isTerminalInstance && !currentStepPendingApprovals.Any() && currentStep?.WorkflowStep?.StepType == WorkflowStepType.Approval)
        {
            await EnsureApprovalsForStepAsync(currentStep.Id);
            approvals = (await _workflowApprovalRepository.GetByStatusAsync(WorkflowApprovalStatus.Pending, instance.TenantId)).ToList();
            pendingApprovals = approvals.Where(a => a.StepInstance.WorkflowInstanceId == instance.Id).ToList();
        }

        var pendingApprovalStatuses = pendingApprovals.Select(a => new WorkflowApprovalStatusDto
        {
            ApprovalId = a.Id,
            ApprovalGroup = Math.Max(a.ApprovalGroup, 1),
            StepName = a.StepInstance.WorkflowStep?.Name ?? "Approval",
            ApproverId = a.ApproverId ?? Guid.Empty,
            ApproverName = a.Approver?.UserName ?? a.ApproverRole ?? "Unassigned",
            ApproverRole = a.ApproverRole,
            Status = a.Status,
            RequestedDate = a.RequestedDate,
            DueDate = a.DueDate,
            IsOverdue = a.DueDate.HasValue && a.DueDate.Value < DateTime.UtcNow && a.Status == WorkflowApprovalStatus.Pending,
            IsAdHoc = a.IsAdHoc
        }).ToList();

        if (!isTerminalInstance && pendingApprovalStatuses.Count == 0 && currentStepDefinition?.StepType == WorkflowStepType.Approval)
        {
            pendingApprovalStatuses = BuildConfiguredPendingApprovalStatuses(currentStepDefinition, currentStep);
        }

        var pendingApproverNamesByStepInstanceId = pendingApprovals
            .GroupBy(a => a.StepInstanceId)
            .ToDictionary(
                g => g.Key,
                g => FormatApproverList(g
                    .Select(a => a.Approver?.UserName ?? a.ApproverRole ?? "Unassigned")
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)));

        return new WorkflowStatusDto
        {
            WorkflowInstanceId = instance.Id,
            WorkflowName = instance.WorkflowDefinition?.Name ?? "Workflow",
            EntityId = instance.EntityId,
            EntityType = instance.EntityType?.Name ?? instance.EntityType?.Code ?? "Unknown",
            Status = instance.Status,
            StartedDate = instance.StartedDate ?? instance.CreatedDate,
            CompletedDate = instance.CompletedDate,
            CurrentStepName = currentStepDefinition?.Name,
            CurrentStepInstanceId = currentStep?.Id,
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
                AssignedToName = si.AssignedTo?.UserName
                    ?? (pendingApproverNamesByStepInstanceId.TryGetValue(si.Id, out var pendingApprovers)
                        ? pendingApprovers
                        : si.WorkflowStep == null
                            ? null
                            : FormatApproverList(GetConfiguredApproverLabels(si.WorkflowStep))),
                StartedDate = si.StartedDate,
                CompletedDate = si.CompletedDate,
                DueDate = si.DueDate,
                IsOverdue = si.DueDate.HasValue && si.DueDate.Value < DateTime.UtcNow && si.Status == WorkflowStepInstanceStatus.Pending
            }).ToList(),
            PendingApprovals = pendingApprovalStatuses
        };
    }

    private static WorkflowStepInstance? ResolveCurrentStep(WorkflowInstance instance, List<WorkflowStepInstance> steps)
    {
        if (!steps.Any())
        {
            return null;
        }

        if (instance.CurrentStepId.HasValue)
        {
            var currentStepByDefinition = steps
                .Where(si =>
                    si.WorkflowStepId == instance.CurrentStepId.Value &&
                    (si.Status == WorkflowStepInstanceStatus.Pending || si.Status == WorkflowStepInstanceStatus.InProgress))
                .OrderByDescending(si => si.CreatedDate)
                .FirstOrDefault();

            if (currentStepByDefinition != null)
            {
                return currentStepByDefinition;
            }
        }

        return steps
            .Where(si => si.Status == WorkflowStepInstanceStatus.InProgress)
            .OrderByDescending(si => si.CreatedDate)
            .FirstOrDefault()
            ?? steps
                .Where(si => si.Status == WorkflowStepInstanceStatus.Pending)
                .OrderByDescending(si => si.CreatedDate)
                .FirstOrDefault();
    }

    private static WorkflowStep? ResolveCurrentStepDefinition(
        WorkflowInstance instance,
        WorkflowStepInstance? currentStep,
        List<WorkflowStepInstance> steps)
    {
        if (currentStep?.WorkflowStep != null)
        {
            return currentStep.WorkflowStep;
        }

        if (instance.CurrentStep != null)
        {
            return instance.CurrentStep;
        }

        if (instance.CurrentStepId.HasValue)
        {
            var byCurrentStepId = instance.WorkflowDefinition?.Steps
                .FirstOrDefault(step => step.Id == instance.CurrentStepId.Value);

            if (byCurrentStepId != null)
            {
                return byCurrentStepId;
            }
        }

        return steps
            .Select(stepInstance => stepInstance.WorkflowStep)
            .Where(step => step != null)
            .OrderBy(step => step!.Order)
            .FirstOrDefault()
            ?? instance.WorkflowDefinition?.Steps
                .Where(step => !step.IsEndStep)
                .OrderBy(step => step.Order)
                .FirstOrDefault();
    }

    private static List<WorkflowApprovalStatusDto> BuildConfiguredPendingApprovalStatuses(
        WorkflowStep stepDefinition,
        WorkflowStepInstance? stepInstance)
    {
        var labels = GetConfiguredApproverLabels(stepDefinition);
        if (labels.Count == 0)
        {
            return new List<WorkflowApprovalStatusDto>();
        }

        var requestedDate = stepInstance?.StartedDate ?? stepInstance?.CreatedDate ?? DateTime.UtcNow;

        return labels
            .Select(label =>
            {
                var dueDate = stepInstance?.DueDate;
                return new WorkflowApprovalStatusDto
                {
                    ApprovalId = Guid.Empty,
                    ApprovalGroup = 1,
                    StepName = stepDefinition.Name,
                    ApproverId = Guid.Empty,
                    ApproverName = label,
                    ApproverRole = label,
                    Status = WorkflowApprovalStatus.Pending,
                    RequestedDate = requestedDate,
                    DueDate = dueDate,
                    IsOverdue = dueDate.HasValue && dueDate.Value < DateTime.UtcNow
                };
            })
            .ToList();
    }

    private static List<string> GetConfiguredApproverLabels(WorkflowStep stepDefinition)
    {
        var labels = new List<string>();

        var config = DeserializeStepConfig(stepDefinition.Configuration);
        var rules = config?.ApprovalConfig?.ApproverRules;
        if (rules != null)
        {
            foreach (var rule in rules.OrderByDescending(rule => rule.Priority))
            {
                switch (rule.AssignmentType)
                {
                    case WorkflowAssignmentType.Role:
                        if (!string.IsNullOrWhiteSpace(rule.Role))
                        {
                            labels.Add(rule.Role);
                        }
                        break;
                    case WorkflowAssignmentType.User:
                        if (rule.UserId.HasValue)
                        {
                            labels.Add(rule.UserId.Value.ToString());
                        }
                        break;
                    case WorkflowAssignmentType.Dynamic:
                        if (!string.IsNullOrWhiteSpace(rule.DynamicExpression))
                        {
                            labels.Add(rule.DynamicExpression);
                        }
                        break;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(stepDefinition.RequiredRole))
        {
            labels.Add(stepDefinition.RequiredRole);
        }

        return labels
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? FormatApproverList(IEnumerable<string> approverNames)
    {
        var names = approverNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return names.Count switch
        {
            0 => null,
            1 => names[0],
            2 => string.Join(", ", names),
            _ => $"{string.Join(", ", names.Take(2))} +{names.Count - 2}"
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
            await EnsureStepAssignedToDirectApproverAsync(stepInstance, existingApprovals);
            return;
        }

        var context = MergeDataContext(instance, dataContext);
        var config = DeserializeStepConfig(stepDefinition.Configuration);
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

        if (action == WorkflowStepAction.Delegate)
        {
            if (!TryResolveGuidFromData(resultData, "delegateToId", out var delegateToId))
            {
                throw new InvalidOperationException("A delegate user is required.");
            }

            await _runtimeGovernance.ValidateOneOffDelegationAsync(
                instance.TenantId, userId, delegateToId, allowRedelegation: false);
            approval.OriginalApproverId ??= approval.ApproverId ?? userId;
            approval.ApproverId = delegateToId;
            approval.ApproverRole = null;
            approval.DelegatedById = userId;
            approval.DelegatedAt = DateTime.UtcNow;
            approval.DelegationReason = comments;
            await _workflowApprovalRepository.UpdateAsync(approval);
            await _workflowApprovalRepository.SaveChangesAsync();

            stepInstance.AssignedToId = delegateToId;
            await _workflowStepInstanceRepository.UpdateAsync(stepInstance);
            await _workflowStepInstanceRepository.SaveChangesAsync();
            await _activityService.LogActivityAsync(instance.Id, WorkflowActivityType.ApprovalDelegated,
                "Approval delegated", comments, userId, stepInstance.Id,
                new { approvalId = approval.Id, delegatedToId = delegateToId });
            await _notificationService.SendApprovalRequestNotificationAsync(approval.Id);

            return new WorkflowExecutionResult
            {
                Success = true,
                Status = instance.Status,
                WorkflowInstanceId = instance.Id,
                CurrentStepId = stepDefinition.Id,
                Message = "Approval delegated"
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

        var config = DeserializeStepConfig(stepDefinition.Configuration);
        var policyContext = MergeDataContext(instance, resultData);
        var policyResolution = await ResolveApprovalPolicyAsync(instance, policyContext);
        if (policyResolution != null)
        {
            config ??= new WorkflowStepConfigurationDto();
            config.ApprovalConfig = policyResolution.ApprovalConfig;
        }

        // Explicit workflow-level maker-checker rules take precedence over a tenant's broader
        // Procurement SOD toggle. Apply the guard to rejection as well as approval: returning
        // one's own submission is still a checker decision and must remain independent.
        var configuredSeparation = config?.ApprovalConfig is
            { PreventInitiatorApproval: true } or { RequireDistinctApprovers: true };
        var enforceSeparation = configuredSeparation || _sodPolicy is null ||
            await _sodPolicy.IsRequiredForSourceAsync(
                instance.TenantId, instance.EntityType?.Name ?? instance.EntityType?.Code, instance.EntityId);
        var approvalGuardErrors = WorkflowApprovalGuardValidator.Validate(
            config?.ApprovalConfig,
            instance.InitiatedById,
            approvals,
            userId,
            enforceSeparation);
        if (approvalGuardErrors.Count > 0)
        {
            return new WorkflowExecutionResult
            {
                Success = false,
                Status = instance.Status,
                WorkflowInstanceId = instance.Id,
                CurrentStepId = stepDefinition.Id,
                Message = string.Join(" ", approvalGuardErrors),
                Errors = approvalGuardErrors.Select(message => new WorkflowExecutionError
                {
                    Code = "ApprovalPolicyViolation",
                    Message = message,
                    StepId = stepDefinition.Id
                }).ToList()
            };
        }

        if (action != WorkflowStepAction.Reject)
        {
            object? signatureResultData = resultData;
            if (config?.ApprovalConfig?.SignaturePolicy?.IsRequired == true &&
                WorkflowSignatureValidator.ReadSubmission(signatureResultData) == null)
            {
                var stagedSignature = await _signatureStore.GetPendingAsync(approval.Id, userId);
                if (stagedSignature != null) signatureResultData = new { signature = stagedSignature };
            }
            var signatureErrors = WorkflowSignatureValidator.Validate(
                config?.ApprovalConfig?.SignaturePolicy,
                _currentUserService.Roles ?? [],
                signatureResultData,
                DateTime.UtcNow);
            if (signatureErrors.Count > 0)
            {
                return new WorkflowExecutionResult
                {
                    Success = false, Status = instance.Status, WorkflowInstanceId = instance.Id,
                    CurrentStepId = stepDefinition.Id, Message = string.Join(" ", signatureErrors),
                    Errors = signatureErrors.Select(message => new WorkflowExecutionError
                    { Code = "ElectronicSignatureRequired", Message = message, StepId = stepDefinition.Id }).ToList()
                };
            }

            var crossStepSodErrors = enforceSeparation ? await ValidateCrossStepSodAsync(
                config?.ApprovalConfig,
                instance,
                stepInstance,
                resultData,
                userId) : new List<string>();
            if (crossStepSodErrors.Count > 0)
            {
                return new WorkflowExecutionResult
                {
                    Success = false,
                    Status = instance.Status,
                    WorkflowInstanceId = instance.Id,
                    CurrentStepId = stepDefinition.Id,
                    Message = string.Join(" ", crossStepSodErrors),
                    Errors = crossStepSodErrors.Select(message => new WorkflowExecutionError
                    {
                        Code = "SegregationOfDutiesViolation",
                        Message = message,
                        StepId = stepDefinition.Id
                    }).ToList()
                };
            }

            var checklistErrors = ValidateApprovalChecklist(config, stepInstance.ResultData, resultData);
            if (checklistErrors.Count > 0)
            {
                return new WorkflowExecutionResult
                {
                    Success = false,
                    Status = instance.Status,
                    WorkflowInstanceId = instance.Id,
                    CurrentStepId = stepDefinition.Id,
                    Message = string.Join(" ", checklistErrors)
                };
            }
        }

        approval.Status = action == WorkflowStepAction.Reject
            ? WorkflowApprovalStatus.Rejected
            : WorkflowApprovalStatus.Approved;
        approval.ProcessedById = userId;
        approval.ProcessedDate = DateTime.UtcNow;
        approval.Comments = comments;

        await _workflowApprovalRepository.UpdateAsync(approval);
        await _workflowApprovalRepository.SaveChangesAsync();
        if (action != WorkflowStepAction.Reject && config?.ApprovalConfig?.SignaturePolicy?.IsRequired == true)
        {
            await _signatureStore.CommitAsync(approval.Id, userId);
        }

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

        if (action == WorkflowStepAction.Reject)
        {
            return await HandleRejectionAsync(instance, stepInstance, stepDefinition, config, userId, comments);
        }

        var approvalConfig = config?.ApprovalConfig;
        if (approvalConfig?.ActivationMode == WorkflowApprovalActivationMode.Sequential)
        {
            var currentGroup = Math.Max(approval.ApprovalGroup, 1);
            var currentGroupApprovals = approvals
                .Where(candidate => Math.Max(candidate.ApprovalGroup, 1) == currentGroup)
                .ToList();

            if (WorkflowApprovalSequenceCoordinator.IsGroupSatisfied(
                    approvalConfig.ApprovalType,
                    approvalConfig.MinApprovalsRequired,
                    currentGroupApprovals))
            {
                await ExpireSupersededApprovalsAsync(currentGroupApprovals);
                var nextGroup = WorkflowApprovalSequenceCoordinator.GetNextQueuedGroup(approvals);
                if (nextGroup.HasValue)
                {
                    await ActivateApprovalGroupAsync(instance, stepInstance, stepDefinition, approvals, nextGroup.Value);
                    return new WorkflowExecutionResult
                    {
                        Success = true,
                        Status = instance.Status,
                        WorkflowInstanceId = instance.Id,
                        CurrentStepId = stepDefinition.Id,
                        Message = $"Approval recorded; approval group {nextGroup.Value} activated"
                    };
                }
            }
        }

        var shouldComplete = approvalConfig == null ||
            (approvalConfig.ActivationMode == WorkflowApprovalActivationMode.Sequential
                ? WorkflowApprovalSequenceCoordinator.AreAllSequentialGroupsSatisfied(
                    approvalConfig.ApprovalType,
                    approvalConfig.MinApprovalsRequired,
                    approvals)
                : IsApprovalSatisfied(approvalConfig, approvals));
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

        // A Single/Majority/minimum approval policy can complete while alternative approval
        // requests are still pending. Close those requests before advancing so completed steps
        // never retain actionable-looking or audit-ambiguous sibling approvals.
        await ExpireSupersededApprovalsAsync(approvals);

        stepInstance.Status = WorkflowStepInstanceStatus.Completed;
        stepInstance.CompletedDate = DateTime.UtcNow;
        stepInstance.Comments = comments;
        stepInstance.ResultData = MergeStepResultData(stepInstance.ResultData, resultData);
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

    private async Task ExpireSupersededApprovalsAsync(IEnumerable<WorkflowApproval> approvals)
    {
        var superseded = approvals
            .Where(candidate => candidate.Status is WorkflowApprovalStatus.Pending or WorkflowApprovalStatus.Queued)
            .ToList();
        if (superseded.Count == 0)
        {
            return;
        }

        var expiredAt = DateTime.UtcNow;
        foreach (var candidate in superseded)
        {
            candidate.Status = WorkflowApprovalStatus.Expired;
            candidate.ProcessedDate = expiredAt;
            candidate.Comments ??= "Approval request closed because the step approval requirement was satisfied.";
            await _workflowApprovalRepository.UpdateAsync(candidate);
        }

        await _workflowApprovalRepository.SaveChangesAsync();
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

        // Approval semantics take precedence over end-step completion. A valid
        // one-step approval workflow is necessarily both the start and end step;
        // it must create and process its approval before the workflow completes.
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

        if (stepDefinition.IsEndStep)
        {
            stepInstance.Status = WorkflowStepInstanceStatus.Completed;
            stepInstance.CompletedDate = DateTime.UtcNow;
            await _workflowStepInstanceRepository.UpdateAsync(stepInstance);
            await _workflowStepInstanceRepository.SaveChangesAsync();

            await _activityService.LogActivityAsync(
                instance.Id,
                WorkflowActivityType.StepCompleted,
                "End step completed",
                stepDefinition.Name,
                userId,
                stepInstance.Id,
                dataContext);

            await CompleteWorkflowIfPossibleAsync(instance, userId);
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
        var policyResolution = await ResolveApprovalPolicyAsync(instance, context);
        if (policyResolution != null)
        {
            config ??= new WorkflowStepConfigurationDto();
            config.ApprovalConfig = policyResolution.ApprovalConfig;
            stepInstance.ResultData = MergeStepResultData(stepInstance.ResultData, new
            {
                appliedApprovalPolicySetId = policyResolution.PolicySetId,
                appliedApprovalPolicyCode = policyResolution.PolicyCode
            });
            await _workflowStepInstanceRepository.UpdateAsync(stepInstance);
            await _workflowStepInstanceRepository.SaveChangesAsync();
            await _activityService.LogActivityAsync(
                instance.Id,
                WorkflowActivityType.DataUpdated,
                "Approval policy applied",
                $"Policy '{policyResolution.PolicyCode}' applied to step '{stepDefinition.Name}'",
                null,
                stepInstance.Id,
                new { policyResolution.PolicySetId, policyResolution.PolicyCode });
        }

        var approvers = config?.ApprovalConfig == null
            ? new List<(Guid? UserId, string? Role, int ApprovalGroup)>()
            : await ResolveApproversAsync(config.ApprovalConfig, context, stepInstance);

        if (approvers.Count == 0 && !string.IsNullOrWhiteSpace(stepDefinition.RequiredRole))
        {
            approvers.Add((null, stepDefinition.RequiredRole, 1));
        }

        var firstGroup = approvers.Count == 0
            ? 1
            : approvers.Min(approver => approver.ApprovalGroup);
        var activationMode = config?.ApprovalConfig?.ActivationMode ?? WorkflowApprovalActivationMode.Parallel;
        Guid? directAssigneeId = null;
        var module = ContextString(context, "module") ?? InferModule(instance.EntityType?.Name ?? instance.EntityType?.Code);
        var entityType = instance.EntityType?.Name ?? instance.EntityType?.Code;
        var amount = ContextDecimal(context, "amount", "totalAmount", "value");
        var currencyCode = ContextString(context, "currencyCode") ?? ContextString(context, "currency");

        foreach (var approver in approvers)
        {
            var approvalStatus = WorkflowApprovalSequenceCoordinator.GetInitialStatus(
                activationMode,
                approver.ApprovalGroup,
                firstGroup);
            var effectiveApproverId = approver.UserId;
            WorkflowDelegationResolution? delegation = null;
            if (effectiveApproverId.HasValue)
            {
                delegation = await _runtimeGovernance.ResolveDelegateAsync(instance.TenantId,
                    effectiveApproverId.Value, module, entityType, instance.WorkflowDefinitionId,
                    stepDefinition.Id, amount, currencyCode, DateTime.UtcNow);
                if (delegation != null) effectiveApproverId = delegation.DelegateUserId;
            }

            var requestedAt = DateTime.UtcNow;
            var approval = new WorkflowApproval
            {
                Id = Guid.NewGuid(),
                StepInstanceId = stepInstance.Id,
                ApproverId = effectiveApproverId,
                ApproverRole = approver.Role,
                OriginalApproverId = delegation?.PrincipalUserId,
                DelegationId = delegation?.DelegationId,
                DelegatedById = delegation?.PrincipalUserId,
                DelegatedAt = delegation == null ? null : requestedAt,
                DelegationReason = delegation?.Reason,
                ApprovalGroup = approver.ApprovalGroup,
                Status = approvalStatus,
                RequestedDate = requestedAt,
                DueDate = approvalStatus == WorkflowApprovalStatus.Pending
                    ? await _runtimeGovernance.CalculateDueDateAsync(instance.TenantId, requestedAt, stepDefinition.EstimatedHours)
                    : null,
                TenantId = instance.TenantId
            };

            await _workflowApprovalRepository.AddAsync(approval);
            await _workflowApprovalRepository.SaveChangesAsync();

            if (approvalStatus == WorkflowApprovalStatus.Pending)
            {
                directAssigneeId ??= effectiveApproverId;
                await _activityService.LogActivityAsync(
                    instance.Id,
                    WorkflowActivityType.ApprovalRequested,
                    "Approval requested",
                    $"Approval requested for step '{stepDefinition.Name}'",
                    null,
                    stepInstance.Id);

                if (effectiveApproverId.HasValue || !string.IsNullOrWhiteSpace(approver.Role))
                {
                    await _notificationService.SendApprovalRequestNotificationAsync(approval.Id);
                }
            }
        }

        if (!stepInstance.AssignedToId.HasValue && directAssigneeId.HasValue)
        {
            stepInstance.AssignedToId = directAssigneeId.Value;
            await _workflowStepInstanceRepository.UpdateAsync(stepInstance);
            await _workflowStepInstanceRepository.SaveChangesAsync();
        }
    }

    private async Task EnsureStepAssignedToDirectApproverAsync(
        WorkflowStepInstance stepInstance,
        IReadOnlyCollection<WorkflowApproval> approvals)
    {
        if (stepInstance.AssignedToId.HasValue)
        {
            return;
        }

        var directAssigneeId = approvals
            .Where(approval => approval.Status == WorkflowApprovalStatus.Pending && approval.ApproverId.HasValue)
            .OrderBy(approval => approval.RequestedDate)
            .Select(approval => approval.ApproverId!.Value)
            .FirstOrDefault();

        if (directAssigneeId == Guid.Empty)
        {
            return;
        }

        stepInstance.AssignedToId = directAssigneeId;
        await _workflowStepInstanceRepository.UpdateAsync(stepInstance);
        await _workflowStepInstanceRepository.SaveChangesAsync();
    }

    private async Task<List<(Guid? UserId, string? Role, int ApprovalGroup)>> ResolveApproversAsync(
        WorkflowApprovalConfigDto approvalConfig,
        Dictionary<string, object> context,
        WorkflowStepInstance stepInstance)
    {
        var approvers = new List<(Guid? UserId, string? Role, int ApprovalGroup)>();
        var rules = approvalConfig.ApproverRules
            .OrderByDescending(r => r.Priority)
            .ToList();

        foreach (var rule in rules)
        {
            if (rule.Condition != null && !await EvaluateConditionAsync(rule.Condition, context))
            {
                continue;
            }

            var approvalGroup = Math.Max(rule.ApprovalGroup, 1);
            switch (rule.AssignmentType)
            {
                case WorkflowAssignmentType.User:
                    if (rule.UserId.HasValue)
                    {
                        approvers.Add((rule.UserId.Value, null, approvalGroup));
                    }
                    break;
                case WorkflowAssignmentType.Role:
                    if (!string.IsNullOrWhiteSpace(rule.Role))
                    {
                        approvers.Add((null, rule.Role, approvalGroup));
                    }
                    break;
                case WorkflowAssignmentType.Dynamic:
                    if (!string.IsNullOrWhiteSpace(rule.DynamicExpression) &&
                        TryResolveGuidFromContext(context, rule.DynamicExpression!, out var dynamicUserId))
                    {
                        approvers.Add((dynamicUserId, null, approvalGroup));
                    }
                    break;
                case WorkflowAssignmentType.RequestorManager:
                    if (TryResolveGuidFromContext(context, "requestorManagerId", out var managerId))
                    {
                        approvers.Add((managerId, null, approvalGroup));
                    }
                    break;
                case WorkflowAssignmentType.PreviousStepUser:
                    if (stepInstance.AssignedToId.HasValue)
                    {
                        approvers.Add((stepInstance.AssignedToId.Value, null, approvalGroup));
                    }
                    break;
                default:
                    break;
            }
        }

        return approvers.Distinct().ToList();
    }

    private async Task ActivateApprovalGroupAsync(
        WorkflowInstance instance,
        WorkflowStepInstance stepInstance,
        WorkflowStep stepDefinition,
        IReadOnlyCollection<WorkflowApproval> approvals,
        int approvalGroup)
    {
        var activatedAt = DateTime.UtcNow;
        var activatedApprovals = approvals
            .Where(approval => approval.Status == WorkflowApprovalStatus.Queued &&
                Math.Max(approval.ApprovalGroup, 1) == approvalGroup)
            .ToList();

        foreach (var queuedApproval in activatedApprovals)
        {
            queuedApproval.Status = WorkflowApprovalStatus.Pending;
            queuedApproval.RequestedDate = activatedAt;
            queuedApproval.DueDate = await _runtimeGovernance.CalculateDueDateAsync(
                instance.TenantId, activatedAt, stepDefinition.EstimatedHours);
            await _workflowApprovalRepository.UpdateAsync(queuedApproval);
        }
        await _workflowApprovalRepository.SaveChangesAsync();

        stepInstance.AssignedToId = activatedApprovals
            .Where(approval => approval.ApproverId.HasValue)
            .Select(approval => approval.ApproverId)
            .FirstOrDefault();
        await _workflowStepInstanceRepository.UpdateAsync(stepInstance);
        await _workflowStepInstanceRepository.SaveChangesAsync();

        foreach (var activatedApproval in activatedApprovals)
        {
            await _activityService.LogActivityAsync(
                instance.Id,
                WorkflowActivityType.ApprovalRequested,
                "Sequential approval group activated",
                $"Approval group {approvalGroup} activated for step '{stepDefinition.Name}'",
                null,
                stepInstance.Id);

            if (activatedApproval.ApproverId.HasValue || !string.IsNullOrWhiteSpace(activatedApproval.ApproverRole))
            {
                await _notificationService.SendApprovalRequestNotificationAsync(activatedApproval.Id);
            }
        }
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

        var startedAt = DateTime.UtcNow;
        var stepInstance = new WorkflowStepInstance
        {
            Id = Guid.NewGuid(),
            WorkflowInstanceId = instance.Id,
            WorkflowStepId = stepDefinition.Id,
            Status = WorkflowStepInstanceStatus.Pending,
            StartedDate = startedAt,
            AssignedToId = assignedToId,
            DueDate = await _runtimeGovernance.CalculateDueDateAsync(
                instance.TenantId, startedAt, stepDefinition.EstimatedHours),
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

    private async Task<WorkflowApprovalPolicyResolution?> ResolveApprovalPolicyAsync(
        WorkflowInstance instance,
        IReadOnlyDictionary<string, object> context)
    {
        var entityType = instance.EntityType?.Name ??
            instance.WorkflowDefinition?.EntityType?.Name ??
            ReadString(context, "entityType");
        if (string.IsNullOrWhiteSpace(entityType))
        {
            return null;
        }

        return await _approvalPolicyResolver.ResolveAsync(new WorkflowApprovalPolicyContext(
            instance.TenantId,
            entityType,
            instance.StartedDate ?? instance.CreatedDate,
            Module: ReadString(context, "module"),
            Category: ReadString(context, "category"),
            LocationId: ReadGuid(context, "locationId"),
            LegalEntityId: ReadGuid(context, "legalEntityId"),
            Amount: ReadDecimal(context, "amount") ?? ReadDecimal(context, "totalAmount"),
            CurrencyCode: ReadString(context, "currencyCode") ?? ReadString(context, "currency")));
    }

    private static string? ReadString(IReadOnlyDictionary<string, object> context, string key)
    {
        if (!context.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        return value is JsonElement element
            ? element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString()
            : value.ToString();
    }

    private static Guid? ReadGuid(IReadOnlyDictionary<string, object> context, string key)
        => Guid.TryParse(ReadString(context, key), out var value) ? value : null;

    private static decimal? ReadDecimal(IReadOnlyDictionary<string, object> context, string key)
        => decimal.TryParse(ReadString(context, key), out var value) ? value : null;

    private bool CanCurrentUserActOnManualStep(
        WorkflowStepInstance stepInstance,
        WorkflowStep stepDefinition,
        WorkflowStepConfigurationDto? config,
        Guid userId)
    {
        if (stepDefinition.StepType != WorkflowStepType.Manual)
        {
            return true;
        }

        if (stepInstance.Status != WorkflowStepInstanceStatus.Pending &&
            stepInstance.Status != WorkflowStepInstanceStatus.InProgress)
        {
            return false;
        }

        if (stepInstance.AssignedToId.HasValue && stepInstance.AssignedToId.Value == userId)
        {
            return true;
        }

        var roleSet = new HashSet<string>(_currentUserService.Roles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(stepDefinition.RequiredRole) && roleSet.Contains(stepDefinition.RequiredRole))
        {
            return true;
        }

        var assignedRoles = config?.AssignmentRules?
            .Where(rule => rule.AssignmentType == WorkflowAssignmentType.Role && !string.IsNullOrWhiteSpace(rule.Role))
            .Select(rule => rule.Role!)
            .ToList() ?? new List<string>();

        return assignedRoles.Any(roleSet.Contains);
    }

    private static List<string> ValidateTaskCompletionRequirements(
        WorkflowStepConfigurationDto? config,
        string? existingResultData)
    {
        var errors = new List<string>();
        var taskConfig = config?.TaskConfig;
        var requiresDocument = taskConfig?.RequiresDocument == true ||
            taskConfig?.DocumentRequirements?.Any(requirement => requirement.IsRequired) == true ||
            string.Equals(taskConfig?.TaskActionType, "document", StringComparison.OrdinalIgnoreCase);
        var attachments = GetWorkflowTaskAttachments(existingResultData);

        if (requiresDocument)
        {
            // Enforce every required stage-level document before the task can advance.
            var requirements = GetTaskDocumentRequirements(taskConfig);
            foreach (var requirement in requirements.Where(requirement => requirement.IsRequired))
            {
                var requirementKey = requirement.RequirementKey.Trim();
                var hasRequiredDocument = attachments.Any(attachment =>
                    string.IsNullOrWhiteSpace(requirementKey) ||
                    string.Equals(attachment.RequirementKey, requirementKey, StringComparison.OrdinalIgnoreCase));

                if (!hasRequiredDocument)
                {
                    var documentName = string.IsNullOrWhiteSpace(requirement.DocumentName)
                        ? "the required document"
                        : requirement.DocumentName.Trim();
                    errors.Add($"Attach {documentName} before completing this workflow task.");
                }
            }
        }

        var checklist = config?.QualityConfig?.QualityChecks?
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .ToList() ?? new List<WorkflowQualityCheckDto>();
        if (checklist.Count > 0)
        {
            var responses = ExtractApprovalChecklistResponses(existingResultData);
            errors.AddRange(WorkflowChecklistEvidenceValidator.Validate(checklist, responses, attachments));
        }

        return errors;
    }

    private static List<WorkflowDocumentRequirementDto> GetTaskDocumentRequirements(WorkflowTaskConfigDto? taskConfig)
    {
        if (taskConfig == null)
        {
            return new List<WorkflowDocumentRequirementDto>();
        }

        var configured = taskConfig.DocumentRequirements?
            .Where(requirement =>
                requirement != null &&
                (!string.IsNullOrWhiteSpace(requirement.DocumentName) ||
                 !string.IsNullOrWhiteSpace(requirement.RequirementKey)))
            .Select((requirement, index) => new WorkflowDocumentRequirementDto
            {
                Id = string.IsNullOrWhiteSpace(requirement.Id) ? $"document-{index + 1}" : requirement.Id,
                RequirementKey = string.IsNullOrWhiteSpace(requirement.RequirementKey)
                    ? BuildRequirementKey(requirement.DocumentName, index)
                    : requirement.RequirementKey.Trim(),
                DocumentName = string.IsNullOrWhiteSpace(requirement.DocumentName)
                    ? $"Document {index + 1}"
                    : requirement.DocumentName.Trim(),
                DocumentType = string.IsNullOrWhiteSpace(requirement.DocumentType) ? null : requirement.DocumentType.Trim(),
                IsRequired = requirement.IsRequired,
            })
            .ToList() ?? new List<WorkflowDocumentRequirementDto>();

        if (configured.Count > 0)
        {
            if ((taskConfig.RequiresDocument ||
                    string.Equals(taskConfig.TaskActionType, "document", StringComparison.OrdinalIgnoreCase)) &&
                configured.All(requirement => !requirement.IsRequired))
            {
                configured.Add(new WorkflowDocumentRequirementDto
                {
                    Id = "document-required",
                    RequirementKey = string.Empty,
                    DocumentName = string.IsNullOrWhiteSpace(taskConfig.DocumentName)
                        ? "at least one stage document"
                        : taskConfig.DocumentName.Trim(),
                    IsRequired = true,
                });
            }

            return configured;
        }

        if (taskConfig.RequiresDocument ||
            string.Equals(taskConfig.TaskActionType, "document", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(taskConfig.DocumentName))
        {
            return new List<WorkflowDocumentRequirementDto>
            {
                new()
                {
                    Id = "document-1",
                    RequirementKey = string.IsNullOrWhiteSpace(taskConfig.DocumentRequirementKey)
                        ? string.Empty
                        : taskConfig.DocumentRequirementKey.Trim(),
                    DocumentName = string.IsNullOrWhiteSpace(taskConfig.DocumentName)
                        ? "Required document"
                        : taskConfig.DocumentName.Trim(),
                    IsRequired = true,
                },
            };
        }

        return configured;
    }

    private static string BuildRequirementKey(string? value, int index)
    {
        var source = string.IsNullOrWhiteSpace(value) ? $"document-{index + 1}" : value.Trim().ToLowerInvariant();
        var chars = source.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        var key = new string(chars).Trim('-');
        while (key.Contains("--", StringComparison.Ordinal))
        {
            key = key.Replace("--", "-", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(key) ? $"document-{index + 1}" : key;
    }

    private static List<WorkflowTaskAttachmentDto> GetWorkflowTaskAttachments(string? resultData)
    {
        if (string.IsNullOrWhiteSpace(resultData))
        {
            return new List<WorkflowTaskAttachmentDto>();
        }

        try
        {
            var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(resultData, GetJsonSerializerOptions());
            if (payload == null ||
                !payload.TryGetValue("workflowTaskAttachments", out var attachmentsElement) ||
                attachmentsElement.ValueKind != JsonValueKind.Array)
            {
                return new List<WorkflowTaskAttachmentDto>();
            }

            return JsonSerializer.Deserialize<List<WorkflowTaskAttachmentDto>>(
                attachmentsElement.GetRawText(),
                GetJsonSerializerOptions()) ?? new List<WorkflowTaskAttachmentDto>();
        }
        catch
        {
            return new List<WorkflowTaskAttachmentDto>();
        }
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
                    case WorkflowAssignmentType.Role:
                        if (!string.IsNullOrWhiteSpace(rule.Role))
                        {
                            return null;
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

            // Role-owned workflow tasks are intentionally unassigned to a specific user.
            // This keeps the stage available to every user in the configured role instead
            // of carrying forward the actor who completed the previous step.
            if (stepDefinition.AssignmentType.Equals("Role", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        if (!string.IsNullOrWhiteSpace(stepDefinition.RequiredRole))
        {
            return null;
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

        // A missing transition must not bypass a configured final approval. Retain
        // compatibility with legacy definitions that never declared an end step.
        var definitionSteps = (await _workflowStepRepository.GetByWorkflowDefinitionAsync(instance.WorkflowDefinitionId))
            .Where(step => !step.IsDeleted).ToList();
        if (definitionSteps.Any(step => step.IsEndStep)
            && !definitionSteps.Any(step => step.Id == instance.CurrentStepId && step.IsEndStep))
        {
            return new WorkflowExecutionResult
            {
                Success = false,
                Status = instance.Status,
                WorkflowInstanceId = instance.Id,
                CurrentStepId = instance.CurrentStepId,
                Message = "The workflow has not reached its configured end step. Connect the remaining review/approval steps in Administration > Workflow Setup before completing this process."
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

    private static List<string> ValidateApprovalChecklist(
        WorkflowStepConfigurationDto? config,
        string? storedResultData,
        object? resultData)
    {
        var checklist = config?.QualityConfig?.QualityChecks?
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .ToList() ?? new List<WorkflowQualityCheckDto>();

        if (checklist.Count == 0)
        {
            return new List<string>();
        }

        var responses = ExtractApprovalChecklistResponses(resultData)
            .Concat(ExtractApprovalChecklistResponses(storedResultData))
            .GroupBy(response => WorkflowChecklistEvidenceValidator.NormalizeKey(response.Id, response.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var attachments = GetWorkflowTaskAttachments(storedResultData);

        return WorkflowChecklistEvidenceValidator.Validate(checklist, responses.Values.ToList(), attachments);
    }

    private async Task<List<string>> ValidateCrossStepSodAsync(
        WorkflowApprovalConfigDto? config,
        WorkflowInstance instance,
        WorkflowStepInstance currentStep,
        object? resultData,
        Guid userId)
    {
        if (config?.ConflictRules?.Any(rule => rule.IsEnabled) != true)
        {
            return new List<string>();
        }

        var previousSteps = (await _workflowStepInstanceRepository.GetByWorkflowInstanceAsync(instance.Id))
            .Where(step => step.Id != currentStep.Id &&
                (step.CompletedDate ?? step.CreatedDate) <= (currentStep.StartedDate ?? currentStep.CreatedDate))
            .ToList();
        var approvalsByStep = new Dictionary<Guid, IReadOnlyCollection<WorkflowApproval>>();
        foreach (var step in previousSteps)
        {
            approvalsByStep[step.Id] = (await _workflowApprovalRepository.GetByStepInstanceAsync(step.Id)).ToList();
        }

        return WorkflowCrossStepSodEvaluator.Validate(
            config,
            previousSteps,
            approvalsByStep,
            MergeDataContext(instance, resultData),
            userId);
    }

    private static List<WorkflowApprovalChecklistResponseDto> ExtractApprovalChecklistResponses(object? data)
    {
        if (data == null)
        {
            return new List<WorkflowApprovalChecklistResponseDto>();
        }

        if (data is string json)
        {
            return ExtractApprovalChecklistResponses(json);
        }

        try
        {
            return ExtractApprovalChecklistResponses(JsonSerializer.Serialize(data));
        }
        catch
        {
            return new List<WorkflowApprovalChecklistResponseDto>();
        }
    }

    private static List<WorkflowApprovalChecklistResponseDto> ExtractApprovalChecklistResponses(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<WorkflowApprovalChecklistResponseDto>();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<WorkflowApprovalChecklistResponseDto>>(document.RootElement.GetRawText(), GetJsonSerializerOptions()) ??
                    new List<WorkflowApprovalChecklistResponseDto>();
            }

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                TryGetPropertyIgnoreCase(document.RootElement, "approvalChecklistResponses", out var responsesElement) &&
                responsesElement.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<WorkflowApprovalChecklistResponseDto>>(responsesElement.GetRawText(), GetJsonSerializerOptions()) ??
                    new List<WorkflowApprovalChecklistResponseDto>();
            }
        }
        catch
        {
            return new List<WorkflowApprovalChecklistResponseDto>();
        }

        return new List<WorkflowApprovalChecklistResponseDto>();
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string MergeStepResultData(string? existingResultData, object? resultData)
    {
        if (resultData == null)
        {
            return existingResultData ?? string.Empty;
        }

        var incomingJson = JsonSerializer.Serialize(resultData, GetJsonSerializerOptions());
        if (string.IsNullOrWhiteSpace(existingResultData))
        {
            return incomingJson;
        }

        try
        {
            using var existingDocument = JsonDocument.Parse(existingResultData);
            using var incomingDocument = JsonDocument.Parse(incomingJson);
            if (existingDocument.RootElement.ValueKind != JsonValueKind.Object ||
                incomingDocument.RootElement.ValueKind != JsonValueKind.Object)
            {
                return incomingJson;
            }

            var payload = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in existingDocument.RootElement.EnumerateObject())
            {
                payload[property.Name] = property.Value.Clone();
            }

            foreach (var property in incomingDocument.RootElement.EnumerateObject())
            {
                payload[property.Name] = property.Value.Clone();
            }

            return JsonSerializer.Serialize(payload, GetJsonSerializerOptions());
        }
        catch (JsonException)
        {
            return incomingJson;
        }
    }

    private static JsonSerializerOptions GetJsonSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
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

    private static bool TryResolveGuidFromData(object? data, string key, out Guid value)
    {
        return TryResolveGuidFromContext(
            data == null
                ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                : MergeObject(data),
            key,
            out value);
    }

    private static Dictionary<string, object> MergeObject(object data)
    {
        if (data is Dictionary<string, object> dictionary)
            return new Dictionary<string, object>(dictionary, StringComparer.OrdinalIgnoreCase);

        var json = JsonSerializer.Serialize(data);
        var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        return parsed?.ToDictionary(pair => pair.Key, pair => ConvertJsonElement(pair.Value) ?? string.Empty,
            StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    private static string? ContextString(IReadOnlyDictionary<string, object> context, string key) =>
        context.TryGetValue(key, out var value) ? Convert.ToString(value)?.Trim() : null;

    private static string? InferModule(string? entityType)
    {
        var normalized = (entityType ?? string.Empty)
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .ToLowerInvariant();

        return normalized switch
        {
            "purchaseorder" or "purchaserequisition" or "procurementplan" or "tender" or "rfq" or
                "supplierquote" or "bid" or "evaluation" or "vendor" or "businesspartner" => "Procurement",
            "workorder" or "jobcard" or "fleettrip" or "fleettripinspection" or "asset" or "quality" => "Maintenance",
            "inventory" or "inventorytransfer" or "inventoryrequisition" => "Inventory",
            "employee" or "payrollrun" or "payrollpayslipemail" or "payrollsalaryadvance" or
                "payrollbonussetup" or "payrollbackpaysetup" => "Human Resources",
            "project" or "projectdeliverable" or "projectclosure" => "Projects",
            "customer" or "salesorder" or "salesagreement" or "salesallocation" or "refund" or "creditnote" => "Sales",
            "servicerequest" => "Service Management",
            _ => null
        };
    }

    private static decimal? ContextDecimal(IReadOnlyDictionary<string, object> context, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!context.TryGetValue(key, out var value) || value == null) continue;
            if (value is decimal number) return number;
            if (decimal.TryParse(Convert.ToString(value), out number)) return number;
        }
        return null;
    }
}
