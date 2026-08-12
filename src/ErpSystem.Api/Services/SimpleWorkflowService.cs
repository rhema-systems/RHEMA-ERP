using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.HR.Payroll;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Interfaces.Services;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Core.Services.Workflow;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Api.Services;

public class SimpleWorkflowService : IWorkflowService
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IWorkflowEntityTypeRepository _entityTypeRepository;
    private readonly IWorkflowDefinitionRepository _definitionRepository;
    private readonly IWorkflowInstanceRepository _instanceRepository;
    private readonly IWorkflowStepInstanceRepository _stepInstanceRepository;
    private readonly IWorkflowApprovalRepository _approvalRepository;
    private readonly IJobCardRepository _jobCardRepository;
    private readonly IInventoryTransferRepository _inventoryTransferRepository;
    private readonly IInventoryRequisitionRepository _inventoryRequisitionRepository;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IPurchaseOrderItemRepository _purchaseOrderItemRepository;
    private readonly IPurchaseRequisitionRepository _purchaseRequisitionRepository;
    private readonly IPurchaseRequisitionItemRepository _purchaseRequisitionItemRepository;
    private readonly ITenderRepository _tenderRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;
    private readonly IProcurementPlanRepository _procurementPlanRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SimpleWorkflowService> _logger;

    public SimpleWorkflowService(
        IWorkflowEngine workflowEngine,
        IWorkflowEntityTypeRepository entityTypeRepository,
        IWorkflowDefinitionRepository definitionRepository,
        IWorkflowInstanceRepository instanceRepository,
        IWorkflowStepInstanceRepository stepInstanceRepository,
        IWorkflowApprovalRepository approvalRepository,
        IJobCardRepository jobCardRepository,
        IInventoryTransferRepository inventoryTransferRepository,
        IInventoryRequisitionRepository inventoryRequisitionRepository,
        IPurchaseOrderRepository purchaseOrderRepository,
        IPurchaseOrderItemRepository purchaseOrderItemRepository,
        IPurchaseRequisitionRepository purchaseRequisitionRepository,
        IPurchaseRequisitionItemRepository purchaseRequisitionItemRepository,
        ITenderRepository tenderRepository,
        IProjectRepository projectRepository,
        IBusinessPartnerRepository businessPartnerRepository,
        IProcurementPlanRepository procurementPlanRepository,
        ICurrentUserService currentUserService,
        UserManager<ApplicationUser> userManager,
        IUnitOfWork unitOfWork,
        ILogger<SimpleWorkflowService> logger)
    {
        _workflowEngine = workflowEngine;
        _entityTypeRepository = entityTypeRepository;
        _definitionRepository = definitionRepository;
        _instanceRepository = instanceRepository;
        _stepInstanceRepository = stepInstanceRepository;
        _approvalRepository = approvalRepository;
        _jobCardRepository = jobCardRepository;
        _inventoryTransferRepository = inventoryTransferRepository;
        _inventoryRequisitionRepository = inventoryRequisitionRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _purchaseOrderItemRepository = purchaseOrderItemRepository;
        _purchaseRequisitionRepository = purchaseRequisitionRepository;
        _purchaseRequisitionItemRepository = purchaseRequisitionItemRepository;
        _tenderRepository = tenderRepository;
        _projectRepository = projectRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _procurementPlanRepository = procurementPlanRepository;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public Task<WorkflowExecutionResult> StartApprovalWorkflowAsync(string entityType, Guid entityId) =>
        StartApprovalWorkflowAsync(entityType, entityId, null);

    public Task<WorkflowExecutionResult> StartApprovalWorkflowAsync(
        string entityType,
        Guid entityId,
        Guid workflowDefinitionId) =>
        StartApprovalWorkflowAsync(entityType, entityId, (Guid?)workflowDefinitionId);

    private async Task<WorkflowExecutionResult> StartApprovalWorkflowAsync(
        string entityType,
        Guid entityId,
        Guid? workflowDefinitionId)
    {
        var initiatedById = GetCurrentUserId();
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;

        var entityTypeRecord = await ResolveEntityTypeAsync(entityType, tenantId);
        var definition = workflowDefinitionId.HasValue
            ? await ResolveSelectedDefinitionAsync(entityTypeRecord, tenantId, workflowDefinitionId.Value)
            : await ResolveActiveDefinitionAsync(entityTypeRecord, tenantId);

        // Idempotency/consistency: prevent multiple active workflow instances for the same entity.
        // If an instance is already running, return it instead of starting a duplicate.
        var existingInstances = await _instanceRepository.GetByEntityAsync(entityTypeRecord.Id, entityId.ToString());
        var activeCandidates = existingInstances
            .Where(i => i.Status is WorkflowInstanceStatus.Created or WorkflowInstanceStatus.InProgress or WorkflowInstanceStatus.Waiting or WorkflowInstanceStatus.Suspended)
            .OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt)
            .ToList();

        // If multiple "active" instances exist (historical bug), prefer the one that actually has a current step.
        WorkflowInstance? existingActiveInstance = null;
        WorkflowStepInstance? currentStepInstance = null;
        foreach (var candidate in activeCandidates)
        {
            currentStepInstance = await EnsureCurrentStepInstanceAsync(candidate, entityTypeRecord, entityId);
            if (currentStepInstance != null)
            {
                existingActiveInstance = candidate;
                break;
            }
        }

        existingActiveInstance ??= activeCandidates.FirstOrDefault();

        if (existingActiveInstance != null)
        {
            if (existingActiveInstance.WorkflowDefinitionId != definition.Id)
            {
                return new WorkflowExecutionResult
                {
                    Success = false,
                    Message = "The active workflow instance uses a different definition from the selected authority route.",
                    Status = existingActiveInstance.Status,
                    WorkflowInstanceId = existingActiveInstance.Id,
                    CurrentStepId = existingActiveInstance.CurrentStepId ?? currentStepInstance?.WorkflowStepId
                };
            }

            // Self-heal: if the active step is an approval step and approvals are missing, materialize them.
            currentStepInstance ??= await EnsureCurrentStepInstanceAsync(existingActiveInstance, entityTypeRecord, entityId);
            var existingDataContext = await BuildEntityContextAsync(entityTypeRecord, entityId);
            var startAdvanceResult = await AdvanceStartStepIfNeededAsync(existingActiveInstance, currentStepInstance, initiatedById, existingDataContext);
            if (startAdvanceResult != null)
            {
                return startAdvanceResult;
            }

            if (currentStepInstance?.WorkflowStep?.StepType == WorkflowStepType.Approval)
            {
                try
                {
                    await _workflowEngine.EnsureApprovalsForStepAsync(currentStepInstance.Id, existingDataContext);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to self-heal approvals for existing active workflow instance {WorkflowInstanceId}", existingActiveInstance.Id);
                }
            }

            return new WorkflowExecutionResult
            {
                Success = true,
                Message = "Workflow already started",
                Status = existingActiveInstance.Status,
                WorkflowInstanceId = existingActiveInstance.Id,
                CurrentStepId = existingActiveInstance.CurrentStepId ?? currentStepInstance?.WorkflowStepId
            };
        }

        var dataContext = await BuildEntityContextAsync(entityTypeRecord, entityId);

        var workflowInstance = await _workflowEngine.StartWorkflowAsync(
            definition.Id,
            entityId,
            initiatedById,
            dataContext);

        await AttachWorkflowInstanceAsync(entityTypeRecord, entityId, workflowInstance.Id);

        var currentStepInstanceAfterStart = await _stepInstanceRepository.GetCurrentStepAsync(workflowInstance.Id);
        var advanceResult = await AdvanceStartStepIfNeededAsync(workflowInstance, currentStepInstanceAfterStart, initiatedById, dataContext);
        if (advanceResult != null)
        {
            return advanceResult;
        }

        return new WorkflowExecutionResult
        {
            Success = true,
            Message = "Workflow started",
            Status = workflowInstance.Status,
            WorkflowInstanceId = workflowInstance.Id,
            CurrentStepId = workflowInstance.CurrentStepId
        };
    }

    private async Task<WorkflowExecutionResult?> AdvanceStartStepIfNeededAsync(
        WorkflowInstance instance,
        WorkflowStepInstance? currentStepInstance,
        Guid userId,
        object? dataContext)
    {
        var currentStep = currentStepInstance?.WorkflowStep;
        if (currentStepInstance == null ||
            currentStep == null ||
            !currentStep.IsStartStep ||
            currentStep.StepType == WorkflowStepType.Approval ||
            instance.Status is not (WorkflowInstanceStatus.Created or WorkflowInstanceStatus.InProgress or WorkflowInstanceStatus.Waiting or WorkflowInstanceStatus.Suspended))
        {
            return null;
        }

        var result = await _workflowEngine.ExecuteNextStepAsync(instance.Id, userId, dataContext);
        if (!result.Success)
        {
            return result;
        }

        var advancedStepInstance = await _stepInstanceRepository.GetCurrentStepAsync(instance.Id);
        if (advancedStepInstance?.WorkflowStep?.StepType == WorkflowStepType.Approval)
        {
            try
            {
                await _workflowEngine.EnsureApprovalsForStepAsync(advancedStepInstance.Id, dataContext);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to ensure approvals after advancing workflow instance {WorkflowInstanceId}", instance.Id);
            }
        }

        return result;
    }

    public async Task<bool> CanUserApproveAsync(string entityType, Guid entityId, Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return false;
        }

        var instance = await ResolveActiveWorkflowInstanceAsync(entityType, entityId);
        if (instance == null)
        {
            _logger.LogDebug(
                "Workflow approval check denied: no active instance for {EntityType} {EntityId}",
                entityType,
                entityId);
            return false;
        }

        var stepInstance = await _stepInstanceRepository.GetCurrentStepAsync(instance.Id);
        if (stepInstance == null)
        {
            _logger.LogDebug(
                "Workflow approval check denied: no current step for workflow instance {WorkflowInstanceId}",
                instance.Id);
            return false;
        }

        var (workflowUserId, user) = await ResolveWorkflowUserAsync(userId, "approval check");
        if (user == null)
        {
            _logger.LogWarning("Workflow approval check failed: user {UserId} not found", userId);
            return false;
        }

        var userRoles = await _userManager.GetRolesAsync(user);
        var roleSet = new HashSet<string>(userRoles, StringComparer.OrdinalIgnoreCase);

        var approvals = await _approvalRepository.GetByStepInstanceAsync(stepInstance.Id);

        // Self-heal: if this is an Approval step and approval rows were not materialized (older instances / config parsing),
        // create them from the step configuration + entity context so the correct approvers can proceed.
        if (!approvals.Any() && stepInstance.WorkflowStep?.StepType == WorkflowStepType.Approval)
        {
            try
            {
                var tenantId = _currentUserService.TenantId ?? Guid.Empty;
                var entityTypeRecord = await ResolveEntityTypeAsync(entityType, tenantId);
                var dataContext = await BuildEntityContextAsync(entityTypeRecord, entityId);
                await _workflowEngine.EnsureApprovalsForStepAsync(stepInstance.Id, dataContext);
                approvals = await _approvalRepository.GetByStepInstanceAsync(stepInstance.Id);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to self-heal approvals for step {StepInstanceId}", stepInstance.Id);
            }
        }

        var canApprove = approvals.Any(a =>
            a.Status == WorkflowApprovalStatus.Pending &&
            (a.ApproverId == workflowUserId || (!string.IsNullOrWhiteSpace(a.ApproverRole) && roleSet.Contains(a.ApproverRole))));

        var currentStepType = stepInstance.WorkflowStep?.StepType;
        var isApprovalStep = currentStepType == WorkflowStepType.Approval;

        // Fallback for workflows that use manual/assigned steps without explicit approval rows.
        // IMPORTANT: Do NOT allow this fallback for Approval steps; approvals must come from approver rules/rows.
        if (!canApprove &&
            !isApprovalStep &&
            stepInstance.AssignedToId.HasValue &&
            stepInstance.AssignedToId.Value == workflowUserId &&
            (stepInstance.Status == WorkflowStepInstanceStatus.Pending || stepInstance.Status == WorkflowStepInstanceStatus.InProgress))
        {
            canApprove = true;
        }

        // Fallback for instances created before approval rows were generated correctly:
        // allow if the current step configuration explicitly includes this user/role.
        var hasPendingApprovalRows = approvals.Any(a => a.Status == WorkflowApprovalStatus.Pending);
        if (!canApprove &&
            !hasPendingApprovalRows &&
            !string.IsNullOrWhiteSpace(stepInstance.WorkflowStep?.RequiredRole) &&
            roleSet.Contains(stepInstance.WorkflowStep.RequiredRole))
        {
            canApprove = true;
        }

        if (!canApprove && IsUserConfiguredAsApprover(stepInstance, workflowUserId, roleSet))
        {
            canApprove = true;
        }

        if (canApprove && isApprovalStep)
        {
            var approvalConfig = GetApprovalConfig(stepInstance);
            var guardErrors = WorkflowApprovalGuardValidator.Validate(
                approvalConfig,
                instance.InitiatedById,
                approvals.ToList(),
                workflowUserId);
            if (guardErrors.Count > 0)
            {
                canApprove = false;
                _logger.LogDebug(
                    "Workflow approval check denied by policy for user {UserId}, step {StepInstanceId}: {Reason}",
                    workflowUserId,
                    stepInstance.Id,
                    string.Join(" ", guardErrors));
            }

            if (canApprove)
            {
                var crossStepErrors = await ValidateCrossStepSodAsync(
                    approvalConfig,
                    instance,
                    stepInstance,
                    entityType,
                    entityId,
                    workflowUserId);
                if (crossStepErrors.Count > 0)
                {
                    canApprove = false;
                    _logger.LogDebug(
                        "Workflow approval check denied by cross-step SOD for user {UserId}, step {StepInstanceId}: {Reason}",
                        workflowUserId,
                        stepInstance.Id,
                        string.Join(" ", crossStepErrors));
                }
            }
        }

        if (!canApprove)
        {
            _logger.LogDebug(
                "Workflow approval check denied: user {UserId} has no matching pending approval for step {StepInstanceId}",
                workflowUserId,
                stepInstance.Id);
        }

        return canApprove;
    }

    private bool IsUserConfiguredAsApprover(WorkflowStepInstance stepInstance, Guid userId, HashSet<string> roleSet)
    {
        var configurationJson = stepInstance.WorkflowStep?.Configuration;
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return false;
        }

        try
        {
            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            serializerOptions.Converters.Add(new JsonStringEnumConverter());

            var config = JsonSerializer.Deserialize<WorkflowStepConfigurationDto>(configurationJson, serializerOptions);
            var approverRules = config?.ApprovalConfig?.ApproverRules;
            if (approverRules == null || approverRules.Count == 0)
            {
                return false;
            }

            foreach (var rule in approverRules)
            {
                switch (rule.AssignmentType)
                {
                    case WorkflowAssignmentType.User:
                        if (rule.UserId.HasValue && rule.UserId.Value == userId)
                        {
                            return true;
                        }
                        break;
                    case WorkflowAssignmentType.Role:
                        if (!string.IsNullOrWhiteSpace(rule.Role) && roleSet.Contains(rule.Role))
                        {
                            return true;
                        }
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Failed to parse workflow step configuration for approver fallback on step {StepInstanceId}",
                stepInstance.Id);
        }

        return false;
    }

    public async Task<WorkflowExecutionResult> ProcessApprovalStepAsync(string entityType, Guid entityId, Guid userId, string action, string? comments = null)
    {
        var (workflowUserId, user) = await ResolveWorkflowUserAsync(userId, "approval processing");
        if (user == null)
        {
            return new WorkflowExecutionResult
            {
                Success = false,
                Status = WorkflowInstanceStatus.Failed,
                Message = "Approval user not found"
            };
        }

        var instance = await ResolveActiveWorkflowInstanceAsync(entityType, entityId);
        if (instance == null)
        {
            return new WorkflowExecutionResult
            {
                Success = false,
                Status = WorkflowInstanceStatus.Failed,
                Message = "No active workflow found"
            };
        }

        var stepInstance = await _stepInstanceRepository.GetCurrentStepAsync(instance.Id);
        if (stepInstance == null)
        {
            return new WorkflowExecutionResult
            {
                Success = false,
                Status = instance.Status,
                Message = "No pending workflow step found"
            };
        }

        var stepAction = MapToStepAction(action);
        return await _workflowEngine.ProcessStepAsync(stepInstance.Id, workflowUserId, stepAction, comments: comments);
    }

    private async Task<(Guid UserId, ApplicationUser? User)> ResolveWorkflowUserAsync(Guid suppliedId, string operation)
    {
        if (suppliedId == Guid.Empty)
        {
            return (Guid.Empty, null);
        }

        var user = await _userManager.FindByIdAsync(suppliedId.ToString());
        if (user != null)
        {
            return (user.Id, user);
        }

        var linkedUser = await _userManager.Users
            .FirstOrDefaultAsync(u => u.EmployeeId == suppliedId);
        if (linkedUser != null)
        {
            _logger.LogWarning(
                "Workflow {Operation} received EmployeeId {EmployeeId}; resolved to ApplicationUser {UserId}. Module callers should pass ApplicationUser.Id to workflow APIs.",
                operation,
                suppliedId,
                linkedUser.Id);
            return (linkedUser.Id, linkedUser);
        }

        return (suppliedId, null);
    }

    public async Task<WorkflowStepInfo?> GetCurrentWorkflowStepAsync(string entityType, Guid entityId)
    {
        var instance = await ResolveActiveWorkflowInstanceAsync(entityType, entityId);
        if (instance == null)
        {
            return null;
        }

        var stepInstance = await _stepInstanceRepository.GetCurrentStepAsync(instance.Id);
        if (stepInstance == null && instance.CurrentStepId.HasValue)
        {
            // Fallback for edge-cases where the workflow instance has a current step but no pending/in-progress
            // step instance is returned (e.g., historical data inconsistencies).
            var allSteps = await _stepInstanceRepository.GetByWorkflowInstanceAsync(instance.Id);
            stepInstance = allSteps
                .Where(si => si.WorkflowStepId == instance.CurrentStepId.Value && !si.IsDeleted)
                .OrderByDescending(si => si.CreatedAt)
                .FirstOrDefault();
        }
        if (stepInstance == null)
        {
            return null;
        }

        return new WorkflowStepInfo
        {
            Id = stepInstance.Id,
            StepName = stepInstance.WorkflowStep?.Name ?? "Current Step",
            Status = stepInstance.Status.ToString(),
            AssignedToUserId = stepInstance.AssignedToId,
            CompletedAt = stepInstance.CompletedDate,
            Comments = stepInstance.Comments,
            StepOrder = stepInstance.WorkflowStep?.Order ?? 0,
            IsRequired = stepInstance.WorkflowStep?.IsRequired ?? true
        };
    }

    public async Task<List<WorkflowStepInfo>> GetWorkflowHistoryAsync(string entityType, Guid entityId)
    {
        var instance = await ResolveWorkflowInstanceAsync(entityType, entityId);
        if (instance == null)
        {
            return new List<WorkflowStepInfo>();
        }

        var stepInstances = await _stepInstanceRepository.GetByWorkflowInstanceAsync(instance.Id);
        return stepInstances
            .OrderBy(si => si.CreatedDate)
            .Select(si => new WorkflowStepInfo
            {
                Id = si.Id,
                StepName = si.WorkflowStep?.Name ?? "Step",
                Status = si.Status.ToString(),
                AssignedToUserId = si.AssignedToId,
                CompletedAt = si.CompletedDate,
                Comments = si.Comments,
                StepOrder = si.WorkflowStep?.Order ?? 0,
                IsRequired = si.WorkflowStep?.IsRequired ?? true
            })
            .ToList();
    }

    public async Task<List<WorkflowApprovalItem>> GetPendingApprovalsAsync(Guid userId)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new List<WorkflowApprovalItem>();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var roleSet = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);
        var directApprovals = await _approvalRepository.GetPendingForUserAsync(userId, tenantId);
        var roleApprovals = await _approvalRepository.GetByStatusAsync(WorkflowApprovalStatus.Pending, tenantId);
        var roleFiltered = roleApprovals
            .Where(a => !string.IsNullOrWhiteSpace(a.ApproverRole) && roleSet.Contains(a.ApproverRole!))
            .ToList();

        var approvals = directApprovals.Concat(roleFiltered).DistinctBy(a => a.Id).ToList();

        var results = new List<WorkflowApprovalItem>();
        foreach (var approval in approvals)
        {
            var stepInstance = approval.StepInstance;
            var instance = stepInstance?.WorkflowInstance;
            if (instance == null)
            {
                continue;
            }

            var entityTypeRecord = instance.EntityType;
            var item = new WorkflowApprovalItem
            {
                EntityId = instance.EntityId,
                EntityType = entityTypeRecord?.Code ?? entityTypeRecord?.Name ?? "Unknown",
                CurrentStep = stepInstance?.WorkflowStep?.Name ?? "Approval",
                SubmittedAt = instance.StartedDate ?? instance.CreatedDate,
                SubmittedBy = instance.InitiatedBy?.UserName ?? "Unknown",
                Priority = instance.Priority.ToString(),
                DaysPending = (DateTime.UtcNow - (instance.StartedDate ?? instance.CreatedDate)).Days
            };

            await PopulateApprovalItemDetailsAsync(item, entityTypeRecord, instance.EntityId);
            results.Add(item);
        }

        return results;
    }

    public async Task<WorkflowExecutionResult> CancelWorkflowAsync(string entityType, Guid entityId, string reason)
    {
        var instance = await ResolveActiveWorkflowInstanceAsync(entityType, entityId);
        if (instance == null)
        {
            return new WorkflowExecutionResult
            {
                Success = false,
                Status = WorkflowInstanceStatus.Failed,
                Message = "No active workflow found"
            };
        }

        var userId = GetCurrentUserId();
        await _workflowEngine.CancelWorkflowAsync(instance.Id, userId, reason);

        return new WorkflowExecutionResult
        {
            Success = true,
            Status = WorkflowInstanceStatus.Cancelled,
            WorkflowInstanceId = instance.Id,
            Message = "Workflow cancelled"
        };
    }

    public async Task<WorkflowExecutionResult> RecallWorkflowAsync(string entityType, Guid entityId, Guid userId, string? reason = null)
    {
        var (workflowUserId, user) = await ResolveWorkflowUserAsync(userId, "workflow recall");
        if (user == null)
        {
            return new WorkflowExecutionResult
            {
                Success = false,
                Status = WorkflowInstanceStatus.Failed,
                Message = "Requester user not found"
            };
        }

        var instance = await ResolveActiveWorkflowInstanceAsync(entityType, entityId);
        if (instance == null)
        {
            return new WorkflowExecutionResult
            {
                Success = false,
                Status = WorkflowInstanceStatus.Failed,
                Message = "No active workflow found"
            };
        }

        if (instance.InitiatedById != workflowUserId && instance.StartedById != workflowUserId)
        {
            return new WorkflowExecutionResult
            {
                Success = false,
                Status = instance.Status,
                WorkflowInstanceId = instance.Id,
                Message = "Only the requester can recall this workflow"
            };
        }

        var recallReason = string.IsNullOrWhiteSpace(reason)
            ? "Recalled by requester"
            : reason.Trim();

        await _workflowEngine.CancelWorkflowAsync(instance.Id, workflowUserId, recallReason);

        return new WorkflowExecutionResult
        {
            Success = true,
            Status = WorkflowInstanceStatus.Cancelled,
            WorkflowInstanceId = instance.Id,
            Message = "Workflow recalled"
        };
    }

    private Guid GetCurrentUserId()
    {
        var userIdString = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            throw new UnauthorizedAccessException("User not authenticated");
        }

        return userId;
    }

    private async Task<WorkflowEntityType> ResolveEntityTypeAsync(string entityType, Guid tenantId)
    {
        var entityTypeRecord = await _entityTypeRepository.GetByNameAsync(entityType, tenantId);
        if (entityTypeRecord != null)
        {
            return entityTypeRecord;
        }

        var activeTypes = await _entityTypeRepository.GetActiveEntityTypesAsync(tenantId);
        entityTypeRecord = activeTypes.FirstOrDefault(et => IsEntityType(et, entityType));

        if (entityTypeRecord == null)
        {
            throw new InvalidOperationException($"Workflow entity type '{entityType}' is not configured");
        }

        return entityTypeRecord;
    }

    private async Task<WorkflowDefinition> ResolveActiveDefinitionAsync(WorkflowEntityType entityTypeRecord, Guid tenantId)
    {
        var definitions = await _definitionRepository.GetActiveByEntityTypeAsync(entityTypeRecord.Id);
        var definition = definitions
            .Where(d => d.IsActive)
            .OrderByDescending(d => d.Version)
            .ThenByDescending(d => d.UpdatedAt ?? d.CreatedAt)
            .FirstOrDefault();

        if (definition == null)
        {
            throw new InvalidOperationException($"No active workflow definition found for entity type '{entityTypeRecord.Name}'");
        }

        return definition;
    }

    private async Task<WorkflowDefinition> ResolveSelectedDefinitionAsync(
        WorkflowEntityType entityTypeRecord,
        Guid tenantId,
        Guid workflowDefinitionId)
    {
        var definition = await _definitionRepository.GetWithDetailsAsync(workflowDefinitionId);
        if (definition is null || definition.TenantId != tenantId || definition.EntityTypeId != entityTypeRecord.Id)
            throw new InvalidOperationException(
                "The selected workflow definition is missing, belongs to another tenant, or targets another entity type.");
        if (!WorkflowDefinitionLifecyclePolicy.IsRuntimeEligible(definition))
            throw new InvalidOperationException("The selected workflow definition is not Published and active.");
        return definition;
    }

    private async Task<WorkflowInstance?> ResolveActiveWorkflowInstanceAsync(string entityType, Guid entityId)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var entityTypeRecord = await ResolveEntityTypeAsync(entityType, tenantId);

        var instances = await _instanceRepository.GetByEntityAsync(entityTypeRecord.Id, entityId.ToString());
        var active = instances
            .Where(i => i.Status is WorkflowInstanceStatus.Created or WorkflowInstanceStatus.InProgress or WorkflowInstanceStatus.Waiting or WorkflowInstanceStatus.Suspended)
            .OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt)
            .ToList();

        // Prefer an instance that actually has a current pending/in-progress step.
        foreach (var candidate in active)
        {
            var stepInstance = await EnsureCurrentStepInstanceAsync(candidate, entityTypeRecord, entityId);
            if (stepInstance != null)
            {
                return candidate;
            }
        }

        return active.FirstOrDefault();
    }

    private async Task<WorkflowStepInstance?> EnsureCurrentStepInstanceAsync(
        WorkflowInstance instance,
        WorkflowEntityType entityTypeRecord,
        Guid entityId)
    {
        var currentStepInstance = await _stepInstanceRepository.GetCurrentStepAsync(instance.Id);
        if (currentStepInstance != null)
        {
            return currentStepInstance;
        }

        var definition = await _definitionRepository.GetWithDetailsAsync(instance.WorkflowDefinitionId);
        if (definition == null)
        {
            return null;
        }

        var stepDefinition = ResolveRecoverableCurrentStep(instance, definition);
        if (stepDefinition == null)
        {
            return null;
        }

        // Role-owned manual steps should remain unassigned to a specific person.
        // The workflow engine then authorizes any user with the configured role.
        var repairedAssignedToId = stepDefinition.StepType == WorkflowStepType.Approval ||
            !string.IsNullOrWhiteSpace(stepDefinition.RequiredRole) ||
            string.Equals(stepDefinition.AssignmentType, "Role", StringComparison.OrdinalIgnoreCase)
                ? (Guid?)null
                : instance.StartedById ?? instance.InitiatedById;

        var repairedStepInstance = new WorkflowStepInstance
        {
            Id = Guid.NewGuid(),
            WorkflowInstanceId = instance.Id,
            WorkflowStepId = stepDefinition.Id,
            Status = WorkflowStepInstanceStatus.Pending,
            StartedDate = DateTime.UtcNow,
            AssignedToId = repairedAssignedToId,
            DueDate = stepDefinition.EstimatedHours.HasValue
                ? DateTime.UtcNow.AddHours(stepDefinition.EstimatedHours.Value)
                : null,
            TenantId = instance.TenantId
        };

        await _stepInstanceRepository.AddAsync(repairedStepInstance);
        await _stepInstanceRepository.SaveChangesAsync();

        instance.CurrentStepId = stepDefinition.Id;
        if (instance.Status == WorkflowInstanceStatus.Created)
        {
            instance.Status = WorkflowInstanceStatus.InProgress;
            instance.StartedDate ??= DateTime.UtcNow;
        }

        await _instanceRepository.UpdateAsync(instance);
        await _instanceRepository.SaveChangesAsync();

        if (stepDefinition.StepType == WorkflowStepType.Approval)
        {
            try
            {
                var dataContext = await BuildEntityContextAsync(entityTypeRecord, entityId);
                await _workflowEngine.EnsureApprovalsForStepAsync(repairedStepInstance.Id, dataContext);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    ex,
                    "Failed to create approvals while repairing workflow instance {WorkflowInstanceId}",
                    instance.Id);
            }
        }

        return await _stepInstanceRepository.GetCurrentStepAsync(instance.Id) ?? repairedStepInstance;
    }

    private static WorkflowStep? ResolveRecoverableCurrentStep(
        WorkflowInstance instance,
        WorkflowDefinition definition)
    {
        var steps = definition.Steps
            .Where(step => !step.IsDeleted)
            .OrderBy(step => step.Order)
            .ToList();

        if (steps.Count == 0)
        {
            return null;
        }

        if (instance.CurrentStepId.HasValue)
        {
            var currentStep = steps.FirstOrDefault(step => step.Id == instance.CurrentStepId.Value);
            if (currentStep != null)
            {
                return currentStep;
            }
        }

        return steps.FirstOrDefault(step => step.IsStartStep)
            ?? steps.FirstOrDefault(step => !step.IsEndStep)
            ?? steps.FirstOrDefault();
    }

    private async Task<WorkflowInstance?> ResolveWorkflowInstanceAsync(string entityType, Guid entityId)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var entityTypeRecord = await ResolveEntityTypeAsync(entityType, tenantId);

        var instances = await _instanceRepository.GetByEntityAsync(entityTypeRecord.Id, entityId.ToString());
        return instances.OrderByDescending(i => i.CreatedAt).FirstOrDefault();
    }

    private async Task<Dictionary<string, object>> BuildEntityContextAsync(WorkflowEntityType entityTypeRecord, Guid entityId)
    {
        var context = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["entityId"] = entityId,
            ["entityType"] = entityTypeRecord.Code ?? entityTypeRecord.Name
        };
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;

        if (IsEntityType(entityTypeRecord, "BankDepositBatch", "Bank Deposit"))
        {
            var deposit = await _unitOfWork.Repository<BankDepositBatch>()
                .FirstOrDefaultAsync(
                    item => item.TenantId == tenantId && item.Id == entityId,
                    item => item.BankAccount)
                ?? throw new InvalidOperationException("Bank deposit not found");
            context["depositNumber"] = deposit.DepositNumber;
            context["depositDate"] = deposit.DepositDate;
            context["depositReference"] = deposit.DepositReference;
            context["status"] = deposit.Status.ToString();
            context["currency"] = deposit.Currency;
            context["totalReceipts"] = deposit.TotalReceipts;
            context["totalDeductions"] = deposit.TotalDeductions;
            context["netAmount"] = deposit.NetAmount;
            context["bankAccountId"] = deposit.BankAccountId;
            context["bankAccountName"] = deposit.BankAccount?.AccountName ?? string.Empty;
            context["submittedById"] = deposit.SubmittedById;
        }

        if (IsEntityType(entityTypeRecord, "ReturnedChequeCase", "Returned Cheque"))
        {
            var returnedCheque = await _unitOfWork.Repository<ReturnedChequeCase>()
                .FirstOrDefaultAsync(
                    item => item.TenantId == tenantId && item.Id == entityId,
                    item => item.BankAccount,
                    item => item.CustomerPayment)
                ?? throw new InvalidOperationException("Returned cheque case not found");
            context["caseNumber"] = returnedCheque.CaseNumber;
            context["chequeNumber"] = returnedCheque.ChequeNumber;
            context["returnDate"] = returnedCheque.ReturnDate;
            context["status"] = returnedCheque.Status.ToString();
            context["returnedAmount"] = returnedCheque.ReturnedAmount;
            context["bankChargeAmount"] = returnedCheque.BankChargeAmount;
            context["bankAccountId"] = returnedCheque.BankAccountId;
            context["bankAccountName"] = returnedCheque.BankAccount?.AccountName ?? string.Empty;
            context["customerPaymentId"] = returnedCheque.CustomerPaymentId;
            context["paymentNumber"] = returnedCheque.CustomerPayment?.PaymentNumber ?? string.Empty;
            context["submittedById"] = returnedCheque.SubmittedById;
        }

        if (IsEntityType(entityTypeRecord, "VendorPayment", "Vendor Payment"))
        {
            var payment = await _unitOfWork.Repository<VendorPayment>()
                .FirstOrDefaultAsync(
                    item => item.TenantId == tenantId && item.Id == entityId,
                    item => item.Supplier,
                    item => item.BankAccount)
                ?? throw new InvalidOperationException("Vendor payment not found");
            var tenantCurrency = await _unitOfWork.Repository<Tenant>()
                .GetQueryable(item => item.Id == tenantId && !item.IsDeleted)
                .Select(item => item.BaseCurrency)
                .FirstOrDefaultAsync();
            var baseCurrencyCode = string.IsNullOrWhiteSpace(tenantCurrency)
                ? payment.CurrencyCode
                : tenantCurrency.Trim().ToUpperInvariant();

            // Approval thresholds operate on functional-currency equivalent values. The original
            // transaction amount/currency remain separate context fields for display and audit.
            context["module"] = "Finance";
            context["category"] = payment.PaymentMethod.ToString();
            context["paymentNumber"] = payment.PaymentNumber;
            context["paymentDate"] = payment.PaymentDate;
            context["status"] = payment.Status.ToString();
            context["amount"] = decimal.Round(
                payment.TotalAmount * (payment.ExchangeRate <= 0m ? 1m : payment.ExchangeRate),
                2,
                MidpointRounding.AwayFromZero);
            context["totalAmount"] = context["amount"];
            context["currencyCode"] = baseCurrencyCode;
            context["transactionAmount"] = payment.TotalAmount;
            context["transactionCurrencyCode"] = payment.CurrencyCode;
            context["exchangeRate"] = payment.ExchangeRate;
            context["paymentMethod"] = payment.PaymentMethod.ToString();
            context["supplierId"] = payment.SupplierId;
            context["supplierName"] = payment.Supplier?.Name ?? string.Empty;
            context["bankAccountId"] = payment.BankAccountId ?? Guid.Empty;
            context["bankAccountName"] = payment.BankAccount?.AccountName ?? string.Empty;
            context["submittedById"] = payment.SubmittedById ?? Guid.Empty;
            context["isExceptionalPayment"] = payment.IsExceptionalPayment;
            context["evidenceExceptionRequested"] = payment.EvidenceExceptionRequested;
            context["requiresManagingDirectorApproval"] = payment.RequiresManagingDirectorApproval;
            context["appliedApprovalPolicySetId"] = payment.AppliedApprovalPolicySetId ?? Guid.Empty;
            context["appliedApprovalPolicyCode"] = payment.AppliedApprovalPolicyCode ?? string.Empty;
            context["approvalControlSnapshotHash"] = payment.ApprovalControlSnapshotHash ?? string.Empty;
        }

        if (IsEntityType(entityTypeRecord, "ExchangeRate", "Exchange Rate"))
        {
            var rate = await _unitOfWork.Repository<ExchangeRate>()
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == entityId)
                ?? throw new InvalidOperationException("Exchange rate not found");
            context["baseCurrencyCode"] = rate.BaseCurrencyCode;
            context["targetCurrencyCode"] = rate.TargetCurrencyCode;
            context["rate"] = rate.Rate;
            context["effectiveDate"] = rate.EffectiveDate;
            context["rateType"] = rate.RateType.ToString();
            context["rateSource"] = rate.RateSource;
            context["approvalStatus"] = rate.ApprovalStatus.ToString();
            context["isManualEntry"] = rate.IsManualEntry;
        }

        if (IsEntityType(entityTypeRecord, "FixedAsset", "Fixed Asset"))
        {
            var asset = await _unitOfWork.Repository<FixedAsset>()
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == entityId, a => a.Category)
                ?? throw new InvalidOperationException("Fixed asset not found");
            context["assetCode"] = asset.AssetCode;
            context["assetName"] = asset.Name;
            context["assetStatus"] = asset.Status.ToString();
            context["categoryId"] = asset.FixedAssetCategoryId;
            context["categoryCode"] = asset.Category?.Code ?? string.Empty;
            context["acquisitionCost"] = asset.AcquisitionCost;
            context["netBookValue"] = asset.NetBookValue;
            context["purchaseDate"] = asset.PurchaseDate;
            context["capitalizationDate"] = asset.CapitalizationDate;
        }

        if (IsEntityType(entityTypeRecord, "FixedAssetDepreciationRun", "AssetDepreciationSchedule", "Asset Depreciation", "Depreciation Run"))
        {
            var run = await _unitOfWork.Repository<FixedAssetDepreciationRun>()
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == entityId, r => r.FiscalPeriod)
                ?? throw new InvalidOperationException("Fixed asset depreciation run not found");
            context["status"] = run.Status;
            context["postingDate"] = run.PostingDate;
            context["fiscalPeriodId"] = run.FiscalPeriodId;
            context["periodCode"] = run.FiscalPeriod?.PeriodCode ?? string.Empty;
            context["bookClassification"] = run.BookClassification;
            context["totalDepreciationAmount"] = run.TotalDepreciationAmount;
            context["fixedAssetId"] = run.FixedAssetId;
        }

        if (IsEntityType(entityTypeRecord, "OpeningBalanceBatch", "Opening Balance Batch"))
        {
            var batch = await _unitOfWork.Repository<OpeningBalanceBatch>()
                .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == entityId, b => b.FiscalPeriod)
                ?? throw new InvalidOperationException("Opening balance batch not found");
            context["status"] = batch.Status;
            context["batchNumber"] = batch.BatchNumber;
            context["openingDate"] = batch.OpeningDate;
            context["fiscalPeriodId"] = batch.FiscalPeriodId;
            context["periodCode"] = batch.FiscalPeriod?.PeriodCode ?? string.Empty;
            context["bookClassification"] = batch.BookClassification;
            context["totalDebit"] = batch.TotalDebit;
            context["totalCredit"] = batch.TotalCredit;
            context["difference"] = batch.Difference;
        }

        if (IsEntityType(entityTypeRecord, "AllocationRunBatch", "Allocation Run Batch"))
        {
            var batch = await _unitOfWork.Repository<AllocationRunBatch>()
                .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == entityId, b => b.FiscalPeriod)
                ?? throw new InvalidOperationException("Allocation run batch not found");
            context["status"] = batch.Status.ToString();
            context["batchNumber"] = batch.BatchNumber;
            context["allocationDate"] = batch.AllocationDate;
            context["fiscalPeriodId"] = batch.FiscalPeriodId;
            context["periodCode"] = batch.FiscalPeriod?.PeriodCode ?? string.Empty;
            context["bookClassification"] = batch.BookClassification;
            context["totalAllocated"] = batch.TotalAllocated;
            context["sourcePeriodBalance"] = batch.SourcePeriodBalance;
        }

        if (IsEntityType(entityTypeRecord, "AssetValuation", "Asset Valuation"))
        {
            var valuation = await _unitOfWork.Repository<AssetValuation>()
                .FirstOrDefaultAsync(v => v.TenantId == tenantId && v.Id == entityId, v => v.FixedAsset)
                ?? throw new InvalidOperationException("Asset valuation not found");
            context["status"] = valuation.Status;
            context["fixedAssetId"] = valuation.FixedAssetId;
            context["assetCode"] = valuation.FixedAsset?.AssetCode ?? string.Empty;
            context["valuationDate"] = valuation.ValuationDate;
            context["valuationType"] = valuation.ValuationType.ToString();
            context["carryingAmountBefore"] = valuation.CarryingAmountBefore;
            context["carryingAmountAfter"] = valuation.CarryingAmountAfter;
            context["adjustmentAmount"] = valuation.AdjustmentAmount;
            context["reason"] = valuation.Reason ?? string.Empty;
        }

        if (IsEntityType(entityTypeRecord, "JOB_CARD", "JobCard", "Job Card"))
        {
            var jobCard = await _jobCardRepository.GetByIdAsync(entityId) ?? throw new InvalidOperationException("Job card not found");
            context["jobCardNumber"] = jobCard.JobCardNumber;
            context["jobCardStatus"] = jobCard.JobCardStatus;
            context["approvalStatus"] = jobCard.ApprovalStatus;
            context["estimatedCost"] = jobCard.EstimatedCost;
            context["estimatedHours"] = jobCard.EstimatedHours;
            context["priorityLevelId"] = jobCard.PriorityLevelId;
            context["maintenanceTypeId"] = jobCard.MaintenanceTypeId;
            context["requiresShutdown"] = jobCard.RequiresShutdown;
            context["requiresSafetyPermit"] = jobCard.RequiresSafetyPermit;
            context["requiresSpecialTools"] = jobCard.RequiresSpecialTools;
            context["requestedById"] = jobCard.RequestedById;
            context["requestedDate"] = jobCard.RequestedDate;
            context["requiredCompletionDate"] = jobCard.RequiredCompletionDate;
            context["submittedById"] = jobCard.SubmittedById;
            context["submittedDate"] = jobCard.SubmittedDate;
            context["approvedById"] = jobCard.ApprovedById;
            context["approvedDate"] = jobCard.ApprovedDate;
            context["approvalComments"] = jobCard.ApprovalComments;
            context["assetId"] = jobCard.AssetId;
            context["maintenanceLocation"] = jobCard.MaintenanceLocation;
            context["title"] = jobCard.Title;
            context["description"] = jobCard.Description;
            context["problemDescription"] = jobCard.ProblemDescription;
            context["preferredTechnicianId"] = jobCard.PreferredTechnicianId;
            context["preferredTeamId"] = jobCard.PreferredTeamId;
            context["contractorId"] = jobCard.ContractorId;
            context["specialInstructions"] = jobCard.SpecialInstructions;
            context["safetyRequirements"] = jobCard.SafetyRequirements;
        }

        if (IsEntityType(entityTypeRecord, "FLEET_TRIP", "FleetTrip", "Fleet Trip"))
        {
            var trip = await _unitOfWork.Repository<ErpSystem.Core.Entities.Maintenance.FleetTrip>()
                .FirstOrDefaultAsync(t => t.Id == entityId, t => t.VehicleAsset!, t => t.DriverEmployee!)
                ?? throw new InvalidOperationException("Fleet trip not found");

            context["status"] = trip.Status;
            context["vehicleAssetId"] = trip.VehicleAssetId;
            context["vehicleName"] = trip.VehicleAsset?.Name ?? string.Empty;
            context["vehicleAssetNumber"] = trip.VehicleAsset?.AssetNumber ?? string.Empty;
            context["vehicleLicensePlate"] = trip.VehicleAsset?.LicensePlate ?? string.Empty;
            context["requestedByUserId"] = trip.RequestedByUserId;
            context["driverEmployeeId"] = trip.DriverEmployeeId;
            context["purpose"] = trip.Purpose ?? string.Empty;
            context["origin"] = trip.Origin ?? string.Empty;
            context["destination"] = trip.Destination ?? string.Empty;
            context["plannedStartAt"] = trip.PlannedStartAt;
            context["plannedEndAt"] = trip.PlannedEndAt;
            context["dispatchedAt"] = trip.DispatchedAt;
            context["completedAt"] = trip.CompletedAt;
            context["startMileage"] = trip.StartMileage;
            context["endMileage"] = trip.EndMileage;
            context["startOperatingHours"] = trip.StartOperatingHours;
            context["endOperatingHours"] = trip.EndOperatingHours;
        }

        if (IsEntityType(entityTypeRecord, "FLEET_TRIP_INSPECTION", "FleetTripInspection", "Fleet Trip Inspection"))
        {
            var inspection = await _unitOfWork.Repository<ErpSystem.Core.Entities.Maintenance.FleetTripInspection>()
                .FirstOrDefaultAsync(x => x.Id == entityId, x => x.VehicleAsset!, x => x.InspectionTemplate!)
                ?? throw new InvalidOperationException("Fleet inspection not found");

            context["status"] = inspection.Status;
            context["inspectionKind"] = inspection.InspectionKind;
            context["overallResult"] = inspection.OverallResult ?? string.Empty;
            context["vehicleAssetId"] = inspection.VehicleAssetId;
            context["vehicleName"] = inspection.VehicleAsset?.Name ?? string.Empty;
            context["vehicleAssetNumber"] = inspection.VehicleAsset?.AssetNumber ?? string.Empty;
            context["inspectionTemplateId"] = inspection.InspectionTemplateId;
            context["inspectionTemplateName"] = inspection.InspectionTemplate?.Name ?? string.Empty;
            context["sheetType"] = inspection.InspectionTemplate?.SheetType ?? "InspectionSheet";
            context["fleetTripId"] = inspection.FleetTripId;
            context["inspectorEmployeeId"] = inspection.InspectorEmployeeId;
            context["startedAtUtc"] = inspection.StartedAtUtc;
            context["completedAtUtc"] = inspection.CompletedAtUtc;
        }

        if (IsEntityType(entityTypeRecord, "INVENTORY_TRANSFER", "InventoryTransfer", "Inventory Transfer", "Transfer"))
        {
            var transfer = await _inventoryTransferRepository.GetByIdAsync(entityId) ?? throw new InvalidOperationException("Inventory transfer not found");
            context["transferNumber"] = transfer.TransferNumber;
            context["status"] = transfer.Status.ToString();
            context["sourceWarehouseId"] = transfer.SourceWarehouseId;
            context["destinationWarehouseId"] = transfer.DestinationWarehouseId;
            context["requestDate"] = transfer.RequestDate;
            context["requiredDate"] = transfer.RequiredDate;
            context["totalItems"] = transfer.TotalItems;
            context["totalQuantity"] = transfer.TotalQuantity;
            context["totalValue"] = transfer.TotalValue;
            context["shippingCost"] = transfer.ShippingCost;
            context["miscellaneousCost"] = transfer.MiscellaneousCost;
            context["totalAdditionalCost"] = transfer.TotalAdditionalCost;
            context["costAllocationMethod"] = transfer.CostAllocationMethod;
            context["costApportionmentBasis"] = transfer.CostApportionmentBasis;
            context["requestedById"] = transfer.RequestedById;
            context["approvedById"] = transfer.ApprovedById;
        }

        if (IsEntityType(entityTypeRecord, "INVENTORY_REQUISITION", "InventoryRequisition", "Inventory Requisition", "Requisition"))
        {
            var requisition = await _inventoryRequisitionRepository.GetWithItemsAsync(entityId) ?? throw new InvalidOperationException("Inventory requisition not found");
            context["requisitionNumber"] = requisition.RequisitionNumber;
            context["status"] = requisition.Status.ToString();
            context["departmentId"] = requisition.DepartmentId;
            context["departmentName"] = requisition.DepartmentName;
            context["warehouseId"] = requisition.WarehouseId;
            context["locationId"] = requisition.LocationId;
            context["requisitionType"] = requisition.RequisitionType.ToString();
            context["priority"] = requisition.Priority;
            context["requestDate"] = requisition.RequestDate;
            context["requiredDate"] = requisition.RequiredDate;
            context["purpose"] = requisition.Purpose;
            context["notes"] = requisition.Notes;
            context["totalItems"] = requisition.TotalItems;
            context["totalQuantity"] = requisition.TotalQuantity;
            context["totalValue"] = requisition.TotalValue;
            context["requestedById"] = requisition.RequestedById;
            context["approvedById"] = requisition.ApprovedById;
        }

        if (IsEntityType(entityTypeRecord, "PURCHASE_ORDER", "PurchaseOrder", "Purchase Order", "PO"))
        {
            var purchaseOrder = await _purchaseOrderRepository.GetPurchaseOrderByIdAsync(entityId)
                                ?? throw new InvalidOperationException("Purchase order not found");

            context["purchaseOrderNumber"] = purchaseOrder.OrderNumber;
            context["orderDate"] = purchaseOrder.OrderDate;
            context["requiredDate"] = purchaseOrder.RequiredDate;
            context["promisedDate"] = purchaseOrder.PromisedDate;
            context["receivedDate"] = purchaseOrder.ReceivedDate;
            context["status"] = purchaseOrder.Status;
            context["requestedById"] = purchaseOrder.RequestedById;
            context["approvedById"] = purchaseOrder.ApprovedById;
            context["approvedAt"] = purchaseOrder.ApprovedAt;
            context["businessPartnerId"] = purchaseOrder.BusinessPartnerId;
            context["deliveryWarehouseId"] = purchaseOrder.DeliveryWarehouseId;
            context["deliveryAddress"] = purchaseOrder.DeliveryAddress;
            context["deliveryInstructions"] = purchaseOrder.DeliveryInstructions;
            context["subTotal"] = purchaseOrder.SubTotal;
            context["taxAmount"] = purchaseOrder.TaxAmount;
            context["shippingCost"] = purchaseOrder.ShippingCost;
            context["discountAmount"] = purchaseOrder.DiscountAmount;
            context["totalAmount"] = purchaseOrder.TotalAmount;
            context["paymentTerms"] = purchaseOrder.PaymentTerms;
            context["shippingTerms"] = purchaseOrder.ShippingTerms;
            context["terms"] = purchaseOrder.Terms;
            context["notes"] = purchaseOrder.Notes;
            context["businessPartnerOrderNumber"] = purchaseOrder.BusinessPartnerOrderNumber;
            context["referenceNumber"] = purchaseOrder.ReferenceNumber;
            context["orderType"] = purchaseOrder.OrderType;
            context["currency"] = purchaseOrder.Currency;
            context["exchangeRate"] = purchaseOrder.ExchangeRate;
            context["contractStartDate"] = purchaseOrder.ContractStartDate;
            context["contractEndDate"] = purchaseOrder.ContractEndDate;
            context["contractValue"] = purchaseOrder.ContractValue;
            context["contractUsedValue"] = purchaseOrder.ContractUsedValue;
            context["contractRemainingValue"] = purchaseOrder.ContractRemainingValue;
            context["sourceRequisitionId"] = purchaseOrder.SourceRequisitionId;
            context["sourceRequisitionNumber"] = purchaseOrder.SourceRequisitionNumber;
            context["tenderAwardId"] = purchaseOrder.TenderAwardId;
            context["tenderNumber"] = purchaseOrder.TenderNumber;
            context["contractId"] = purchaseOrder.ContractId;
            context["contractNumber"] = purchaseOrder.ContractNumber;
            context["budgetId"] = purchaseOrder.BudgetId;

            var items = (await _purchaseOrderItemRepository.GetItemsByPurchaseOrderIdAsync(entityId)).ToList();
            context["itemsCount"] = items.Count;
            context["totalOrderedQuantity"] = items.Sum(i => i.OrderedQuantity);
            context["totalReceivedQuantity"] = items.Sum(i => i.ReceivedQuantity);
            context["totalRemainingQuantity"] = items.Sum(i => i.RemainingQuantity);
        }

        if (IsEntityType(entityTypeRecord, "PAYROLL_RUN", "PayrollRun", "Payroll Run"))
        {
            var payrollRun = await _unitOfWork.Repository<PayrollRun>()
                .FirstOrDefaultAsync(r => r.Id == entityId)
                ?? throw new InvalidOperationException("Payroll run not found");

            context["runNumber"] = payrollRun.RunNumber;
            context["payPeriod"] = payrollRun.PayPeriod;
            context["payPeriodFrom"] = payrollRun.PayPeriodFrom;
            context["payPeriodTo"] = payrollRun.PayPeriodTo;
            context["runDate"] = payrollRun.RunDate;
            context["status"] = payrollRun.Status.ToString();
            context["currencyCode"] = payrollRun.CurrencyCode;
            context["employeeCount"] = payrollRun.EmployeeCount;
            context["grossAmount"] = payrollRun.GrossAmount;
            context["netAmount"] = payrollRun.NetAmount;
            context["taxAmount"] = payrollRun.TaxAmount;
            context["employeeContributionAmount"] = payrollRun.EmployeeContributionAmount;
            context["employerContributionAmount"] = payrollRun.EmployerContributionAmount;
            context["isSeparateBonusRun"] = payrollRun.IsSeparateBonusRun;
            context["separateBonusCode"] = payrollRun.SeparateBonusCode ?? string.Empty;
            context["calculatedByUserId"] = payrollRun.CalculatedByUserId;
            context["calculatedAt"] = payrollRun.CalculatedAt;
            context["reviewedByUserId"] = payrollRun.ReviewedByUserId;
            context["approvedByUserId"] = payrollRun.ApprovedByUserId;
            context["notes"] = payrollRun.Notes ?? string.Empty;
        }

        if (IsEntityType(entityTypeRecord, "LEAVE_REQUEST", "LeaveRequest", "Leave Request"))
        {
            var leaveRequest = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffLeave.LeaveRequest>()
                .FirstOrDefaultAsync(r => r.Id == entityId)
                ?? throw new InvalidOperationException("Leave request not found");

            context["requestNumber"] = leaveRequest.RequestNumber;
            context["employeeId"] = leaveRequest.EmployeeId;
            context["leaveTypeId"] = leaveRequest.LeaveTypeId;
            context["leaveSubTypeId"] = leaveRequest.LeaveSubTypeId;
            context["startDate"] = leaveRequest.StartDate;
            context["endDate"] = leaveRequest.EndDate;
            // Routing thresholds are usually expressed in days, so expose it plainly.
            context["totalDays"] = leaveRequest.TotalDays;
            context["requestDate"] = leaveRequest.RequestDate;
            context["status"] = leaveRequest.Status.ToString();
            context["reason"] = leaveRequest.Reason;
            context["relieverEmployeeId"] = leaveRequest.RelieverEmployeeId;
            context["hasReliever"] = leaveRequest.RelieverEmployeeId.HasValue;
            context["leavePlanId"] = leaveRequest.LeavePlanId;
            context["isPlanned"] = leaveRequest.LeavePlanId.HasValue;
        }

        if (IsEntityType(entityTypeRecord, "LEAVE_PLAN", "LeavePlan", "Leave Plan"))
        {
            var leavePlan = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffLeave.LeavePlan>()
                .FirstOrDefaultAsync(p => p.Id == entityId)
                ?? throw new InvalidOperationException("Leave plan not found");

            context["employeeId"] = leavePlan.EmployeeId;
            context["leaveTypeId"] = leavePlan.LeaveTypeId;
            context["year"] = leavePlan.Year;
            context["startDate"] = leavePlan.StartDate;
            context["endDate"] = leavePlan.EndDate;
            context["status"] = leavePlan.Status.ToString();
            context["organizationLevelId"] = leavePlan.OrganizationLevelId;
            context["organizationUnitId"] = leavePlan.OrganizationUnitId;
            context["positionId"] = leavePlan.PositionId;
            context["relieverId"] = leavePlan.RelieverId;
        }

        if (IsEntityType(entityTypeRecord, "LEAVE_ENCASHMENT", "LeaveEncashment", "Leave Encashment"))
        {
            var encashment = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffLeave.LeaveEncashment>()
                .FirstOrDefaultAsync(e => e.Id == entityId)
                ?? throw new InvalidOperationException("Leave encashment not found");

            context["employeeId"] = encashment.EmployeeId;
            context["leaveTypeId"] = encashment.LeaveTypeId;
            context["year"] = encashment.Year;
            context["daysEncashed"] = encashment.DaysEncashed;
            // Encashment routing is normally value-based.
            context["amountPaid"] = encashment.AmountPaid;
            context["status"] = encashment.Status.ToString();
            context["leaveRequestId"] = encashment.LeaveRequestId;
        }

        if (IsEntityType(entityTypeRecord, "STAFF_ATTENDANCE_REGULARIZATION", "StaffAttendanceRegularization", "Attendance Regularization"))
        {
            var regularization = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffAttendance.StaffAttendanceRegularization>()
                .FirstOrDefaultAsync(r => r.Id == entityId)
                ?? throw new InvalidOperationException("Attendance regularization not found");

            context["regularizationNumber"] = regularization.RegularizationNumber;
            context["employeeId"] = regularization.EmployeeId;
            context["attendanceId"] = regularization.AttendanceId;
            context["attendanceDate"] = regularization.AttendanceDate;
            context["requestDate"] = regularization.RequestDate;
            // Routing often differs by what is being corrected, and by how stale the day is.
            context["regularizationType"] = regularization.Type.ToString();
            context["daysSinceAttendance"] =
                (DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - regularization.AttendanceDate.DayNumber);
            context["status"] = regularization.Status.ToString();
            context["reason"] = regularization.Reason;
            context["hasSupportingDocuments"] = !string.IsNullOrWhiteSpace(regularization.SupportingDocuments);
        }

        if (IsEntityType(entityTypeRecord, "STAFF_OVERTIME_REQUEST", "StaffOvertimeRequest", "Overtime Request"))
        {
            var overtime = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffAttendance.StaffOvertimeRequest>()
                .FirstOrDefaultAsync(o => o.Id == entityId)
                ?? throw new InvalidOperationException("Overtime request not found");

            context["requestNumber"] = overtime.RequestNumber;
            context["employeeId"] = overtime.EmployeeId;
            context["overtimeDate"] = overtime.OvertimeDate;
            context["requestDate"] = overtime.RequestDate;
            // Overtime thresholds are expressed in hours, so expose it plainly.
            context["plannedOvertimeHours"] = overtime.PlannedOvertimeHours;
            context["actualOvertimeHours"] = overtime.ActualOvertimeHours;
            context["overtimeType"] = overtime.Type.ToString();
            context["status"] = overtime.Status.ToString();
            context["purpose"] = overtime.Purpose;
        }

        if (IsEntityType(entityTypeRecord, "REMOTE_WORK_REQUEST", "RemoteWorkRequest", "Remote Work Request"))
        {
            var remoteWork = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffAttendance.RemoteWorkRequest>()
                .FirstOrDefaultAsync(r => r.Id == entityId)
                ?? throw new InvalidOperationException("Remote work request not found");

            context["requestNumber"] = remoteWork.RequestNumber;
            context["employeeId"] = remoteWork.EmployeeId;
            context["startDate"] = remoteWork.StartDate;
            context["endDate"] = remoteWork.EndDate;
            // Longer stints usually need a higher approval tier.
            context["requestedDays"] = remoteWork.RequestedDays;
            context["status"] = remoteWork.Status.ToString();
            context["reason"] = remoteWork.Reason;
            context["remoteLocation"] = remoteWork.RemoteLocation;
            context["equipmentConfirmed"] = remoteWork.EquipmentConfirmed;
        }

        if (IsEntityType(entityTypeRecord, "CONSULTANT_TIMESHEET", "ConsultantTimesheet", "Consultant Timesheet"))
        {
            var timesheet = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffAttendance.ConsultantTimesheet>()
                .FirstOrDefaultAsync(t => t.Id == entityId)
                ?? throw new InvalidOperationException("Consultant timesheet not found");

            context["timesheetNumber"] = timesheet.TimesheetNumber;
            context["consultantId"] = timesheet.ConsultantId;
            context["clientId"] = timesheet.ClientId;
            context["engagementId"] = timesheet.EngagementId;
            context["periodStartDate"] = timesheet.PeriodStartDate;
            context["periodEndDate"] = timesheet.PeriodEndDate;
            // Billable hours are the usual routing threshold here.
            context["totalHours"] = timesheet.TotalHours;
            context["status"] = timesheet.Status.ToString();
        }

        if (IsEntityType(entityTypeRecord, "APPRAISAL_TEMPLATE", "AppraisalTemplate", "Appraisal Template"))
        {
            var template = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Performance.AppraisalTemplate>()
                .FirstOrDefaultAsync(t => t.Id == entityId)
                ?? throw new InvalidOperationException("Appraisal template not found");

            context["templateName"] = template.TemplateName;
            // The scope a template targets is what a routing rule keys on: a form written
            // for one position is a smaller decision than one covering a whole org level.
            context["organizationLevelId"] = template.OrganizationLevelId;
            context["organizationUnitId"] = template.OrganizationUnitId;
            context["positionId"] = template.PositionId;
            context["isGlobalScope"] = template.OrganizationLevelId == null
                                    && template.OrganizationUnitId == null
                                    && template.PositionId == null;
            context["isActive"] = template.IsActive;
            context["status"] = template.ApprovalStatus.ToString();
            context["submittedById"] = template.SubmittedById;
        }

        if (IsEntityType(entityTypeRecord, "SALARY_REVIEW_PROPOSAL", "SalaryReviewProposal", "Salary Review Proposal"))
        {
            var proposal = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Performance.SalaryReviewProposal>()
                .FirstOrDefaultAsync(p => p.Id == entityId)
                ?? throw new InvalidOperationException("Salary review proposal not found");

            // The size of the pay change is the routing threshold: a 3% merit increase and a
            // 25% one are not the same decision.
            context["employeeId"] = proposal.EmployeeId;
            context["proposalType"] = proposal.ProposalType.ToString();
            context["proposedPercent"] = proposal.ProposedPercent;
            context["proposedAmount"] = proposal.ProposedAmount;
            context["sourceAppraisalId"] = proposal.SourceAppraisalId;
            context["status"] = proposal.Status.ToString();
        }

        if (IsEntityType(entityTypeRecord, "EMPLOYMENT_ACTION_PROPOSAL", "EmploymentActionProposal", "Employment Action Proposal"))
        {
            var proposal = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Performance.EmploymentActionProposal>()
                .FirstOrDefaultAsync(p => p.Id == entityId)
                ?? throw new InvalidOperationException("Employment action proposal not found");

            // What kind of action it is carries the whole weight here — a recognition and a
            // termination should not route to the same approver.
            context["employeeId"] = proposal.EmployeeId;
            context["actionType"] = proposal.ActionType.ToString();
            context["sourceAppraisalId"] = proposal.SourceAppraisalId;
            context["status"] = proposal.Status.ToString();
        }

        if (IsEntityType(entityTypeRecord, "PERFORMANCE_IMPROVEMENT_PLAN", "PerformanceImprovementPlan", "Performance Improvement Plan", "PIP"))
        {
            var plan = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Performance.PerformanceImprovementPlan>()
                .FirstOrDefaultAsync(p => p.Id == entityId)
                ?? throw new InvalidOperationException("Performance improvement plan not found");

            // How long the plan runs is the routing threshold here — a two-week course correction
            // and a six-month plan that could end in dismissal are not the same decision — along
            // with whether an appraisal called for it or a manager raised it unprompted.
            context["employeeId"] = plan.EmployeeId;
            context["supervisorId"] = plan.SupervisorId;
            context["hrOwnerId"] = plan.HROwnerId;
            context["durationDays"] = (int)(plan.EndDate.Date - plan.StartDate.Date).TotalDays;
            context["fromAppraisal"] = plan.AppraisalId != null;
            context["sourceAppraisalId"] = plan.AppraisalId;
            context["status"] = plan.Status.ToString();
        }

        if (IsEntityType(entityTypeRecord, "STAFF_REQUISITION", "StaffRequisition", "Staff Requisition"))
        {
            var requisition = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Requisition.StaffRequisition>()
                .FirstOrDefaultAsync(r => r.Id == entityId)
                ?? throw new InvalidOperationException("Staff requisition not found");

            // How many heads are being asked for, and whether any budget covers them, is the
            // routing threshold here — one budgeted replacement and a five-head unbudgeted
            // expansion are not the same decision.
            context["requisitionNumber"] = requisition.RequisitionNumber;
            context["positionId"] = requisition.PositionId;
            context["organizationUnitId"] = requisition.OrganizationUnitId;
            context["locationId"] = requisition.LocationId;
            context["requisitionType"] = requisition.Type.ToString();
            context["priority"] = requisition.Priority.ToString();
            context["numberOfPositions"] = requisition.NumberOfPositions;
            context["isBudgeted"] = requisition.IsBudgeted;
            context["requestedById"] = requisition.RequestedById;
            context["desiredStartDate"] = requisition.DesiredStartDate;
            context["status"] = requisition.Status.ToString();
        }

        if (IsEntityType(entityTypeRecord, "JOB_OFFER", "JobOffer", "Job Offer"))
        {
            var offer = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Recruitment.JobOffer>()
                .FirstOrDefaultAsync(o => o.Id == entityId)
                ?? throw new InvalidOperationException("Job offer not found");

            // What the offer commits, and where it sits in the band, is the routing threshold here —
            // an offer at the bottom of a junior band and one above the midpoint of a senior one are
            // not the same decision. `aboveBandMidpoint` is precomputed because a definition
            // condition cannot do the arithmetic itself.
            context["offerNumber"] = offer.OfferNumber;
            context["positionId"] = offer.PositionId;
            context["positionTitle"] = offer.PositionTitle;
            context["employmentType"] = offer.EmploymentType.ToString();
            context["baseSalary"] = offer.BaseSalary ?? 0m;
            context["currencyCode"] = offer.CurrencyCode ?? string.Empty;
            context["salaryGradeMin"] = offer.SalaryGradeMin ?? 0m;
            context["salaryGradeMax"] = offer.SalaryGradeMax ?? 0m;
            context["aboveBandMidpoint"] =
                offer.BaseSalary.HasValue && offer.SalaryGradeMin.HasValue && offer.SalaryGradeMax.HasValue
                && offer.BaseSalary.Value > (offer.SalaryGradeMin.Value + offer.SalaryGradeMax.Value) / 2m;
            context["isConditional"] = offer.IsConditional;
            context["version"] = offer.Version;
            context["preparedById"] = offer.PreparedById;
            context["status"] = offer.OfferStatus.ToString();
        }

        if (IsEntityType(entityTypeRecord, "TRAINING_NOMINATION", "TrainingNomination", "Training Nomination"))
        {
            var nomination = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Training.TrainingNomination>()
                .FirstOrDefaultAsync(n => n.Id == entityId)
                ?? throw new InvalidOperationException("Training nomination not found");

            context["status"] = nomination.Status.ToString();
            context["supervisorApprovedById"] = nomination.SupervisorApprovedById;
            context["hrApprovedById"] = nomination.HrApprovedById;
        }

        if (IsEntityType(entityTypeRecord, "PURCHASE_REQUISITION", "PurchaseRequisition", "Purchase Requisition", "PR"))
        {
            var requisition = await _purchaseRequisitionRepository.GetRequisitionByIdAsync(entityId)
                              ?? throw new InvalidOperationException("Purchase requisition not found");

            context["requisitionNumber"] = requisition.RequisitionNumber;
            context["requisitionDate"] = requisition.RequisitionDate;
            context["requiredDate"] = requisition.RequiredDate;
            context["status"] = requisition.Status;
            context["priority"] = requisition.Priority;
            context["requestedById"] = requisition.RequestedById;
            context["department"] = requisition.Department;
            context["costCenter"] = requisition.CostCenter;
            context["justification"] = requisition.Justification;
            context["notes"] = requisition.Notes;
            context["requisitionType"] = requisition.RequisitionType.ToString();
            context["budgetId"] = requisition.BudgetId;
            context["budgetCode"] = requisition.BudgetCode;
            context["budgetAllocated"] = requisition.BudgetAllocated;
            context["budgetRemaining"] = requisition.BudgetRemaining;
            context["budgetValidated"] = requisition.BudgetValidated;
            context["projectId"] = requisition.ProjectId;
            context["projectCode"] = requisition.ProjectCode;
            context["projectName"] = requisition.ProjectName;
            context["deliveryWarehouseId"] = requisition.DeliveryWarehouseId;
            context["deliveryAddress"] = requisition.DeliveryAddress;
            context["deliveryInstructions"] = requisition.DeliveryInstructions;
            context["isAutoGenerated"] = requisition.IsAutoGenerated;
            context["generatedFrom"] = requisition.GeneratedFrom;
            context["sourcePlanId"] = requisition.SourcePlanId;
            context["approvalLevel"] = requisition.ApprovalLevel;
            context["requiredApprovalLevel"] = requisition.RequiredApprovalLevel;
            context["currentApproverId"] = requisition.CurrentApproverId;
            context["approvalHistory"] = requisition.ApprovalHistory;
            context["revisionNumber"] = requisition.RevisionNumber;
            context["lastAmendedAt"] = requisition.LastAmendedAt;
            context["lastAmendedById"] = requisition.LastAmendedById;
            context["amendmentNotes"] = requisition.AmendmentNotes;
            context["currency"] = requisition.Currency;
            context["preferredBusinessPartnerId"] = requisition.PreferredBusinessPartnerId;
            context["approvedById"] = requisition.ApprovedById;
            context["approvedAt"] = requisition.ApprovedAt;
            context["rejectionReason"] = requisition.RejectionReason;
            context["totalAmount"] = requisition.TotalAmount;

            var items = (await _purchaseRequisitionItemRepository.GetItemsByRequisitionAsync(entityId)).ToList();
            context["itemsCount"] = items.Count;
            context["totalQuantity"] = items.Sum(i => i.Quantity);
            context["totalEstimatedValue"] = items.Sum(i => i.LineTotal);
            context["pendingItems"] = items.Count(i => string.Equals(i.Status, "Pending", StringComparison.OrdinalIgnoreCase));
            context["orderedItems"] = items.Count(i => string.Equals(i.Status, "Ordered", StringComparison.OrdinalIgnoreCase));
            context["receivedItems"] = items.Count(i => string.Equals(i.Status, "Received", StringComparison.OrdinalIgnoreCase));
            context["cancelledItems"] = items.Count(i => string.Equals(i.Status, "Cancelled", StringComparison.OrdinalIgnoreCase));
        }

        if (IsEntityType(entityTypeRecord, "PROCUREMENT_PLAN", "ProcurementPlan", "Procurement Plan"))
        {
            var plan = await _procurementPlanRepository.GetWithFullDetailsAsync(entityId)
                       ?? throw new InvalidOperationException("Procurement plan not found");

            var items = plan.Items?.Where(i => !i.IsDeleted).ToList() ?? new List<ProcurementPlanItem>();
            context["planNumber"] = plan.PlanNumber;
            context["title"] = plan.Title;
            context["description"] = plan.Description ?? string.Empty;
            context["departmentId"] = plan.DepartmentId;
            context["departmentName"] = plan.Department?.Name ?? string.Empty;
            context["fiscalYear"] = plan.FiscalYear;
            context["planningCycle"] = plan.PlanningCycle;
            context["planningQuarter"] = plan.PlanningQuarter ?? string.Empty;
            context["planStartDate"] = plan.PlanStartDate;
            context["planEndDate"] = plan.PlanEndDate;
            context["planDurationYears"] = plan.PlanDurationYears;
            context["status"] = plan.Status;
            context["totalEstimatedBudget"] = plan.TotalEstimatedBudget;
            context["approvedBudget"] = plan.ApprovedBudget;
            context["currency"] = plan.Currency;
            context["preparedById"] = plan.PreparedById;
            context["preparedDate"] = plan.PreparedDate;
            context["reviewedById"] = plan.ReviewedById;
            context["approvedById"] = plan.ApprovedById;
            context["publishedById"] = plan.PublishedById;
            context["publishedDate"] = plan.PublishedDate;
            context["revisionNumber"] = plan.RevisionNumber;
            context["previousVersionId"] = plan.PreviousVersionId;
            context["itemCount"] = items.Count;
            context["criticalItemCount"] = items.Count(i => i.IsCritical);
            context["highPriorityItemCount"] = items.Count(i => string.Equals(i.Priority, "High", StringComparison.OrdinalIgnoreCase) || string.Equals(i.Priority, "Critical", StringComparison.OrdinalIgnoreCase));
            context["totalItemQuantity"] = items.Sum(i => i.EstimatedQuantity);
            context["totalItemEstimatedCost"] = items.Sum(i => i.EstimatedTotalCost);
            context["budgetLineCount"] = items.Select(i => i.BudgetLineCode).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().Count();
            context["categoryCount"] = items.Select(i => i.ItemCategory).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().Count();
        }

        if (IsEntityType(entityTypeRecord, "TENDER", "Tender", "ProcurementTender"))
        {
            var tender = await _tenderRepository.GetByIdAsync(entityId) ?? throw new InvalidOperationException("Tender not found");
            context["tenderId"] = tender.Id;
            context["tenderNumber"] = tender.TenderNumber;
            context["title"] = tender.Title;
            context["tenderType"] = tender.TenderType;
            context["status"] = tender.Status;
            context["publishDate"] = tender.PublishDate;
            context["submissionDeadline"] = tender.SubmissionDeadline;
            context["openingDate"] = tender.OpeningDate;
            context["estimatedValue"] = tender.EstimatedValue ?? 0m;
            context["currency"] = tender.Currency;
            context["requiresPrequalification"] = tender.RequiresPrequalification;
            context["allowPartialBids"] = tender.AllowPartialBids;
            context["useQCBSEvaluation"] = tender.UseQCBSEvaluation;
            context["technicalWeight"] = tender.TechnicalWeight;
            context["financialWeight"] = tender.FinancialWeight;
            context["createdById"] = tender.CreatedById;
            context["createdAt"] = tender.CreatedAt;
        }

        if (IsEntityType(entityTypeRecord, "PROJECT", "Project"))
        {
            var project = await _projectRepository.GetByIdAsync(entityId) ?? throw new InvalidOperationException("Project not found");
            context["projectId"] = project.Id;
            context["projectCode"] = project.ProjectCode;
            context["title"] = project.Title;
            context["status"] = project.Status;
            context["projectTypeId"] = project.ProjectTypeId;
            context["projectPriorityId"] = project.ProjectPriorityId;
            context["projectManagerId"] = project.ProjectManagerId;
            context["sponsorId"] = project.SponsorId;
            context["startDate"] = project.StartDate;
            context["targetEndDate"] = project.TargetEndDate;
            context["estimatedBudget"] = project.EstimatedBudget ?? 0m;
            context["approvedBudget"] = project.ApprovedBudget ?? 0m;
            context["actualCost"] = project.ActualCost ?? 0m;
            context["progressPercent"] = project.ProgressPercent;
            context["approvalRequired"] = project.ApprovalRequired;
            context["methodology"] = project.Methodology;
        }

        if (IsEntityType(entityTypeRecord, "PROJECT_DELIVERABLE", "ProjectDeliverable", "Project Deliverable"))
        {
            var deliverable = await _unitOfWork.Repository<ProjectDeliverable>()
                .FirstOrDefaultAsync(x => x.Id == entityId, x => x.Project!, x => x.Milestone!, x => x.WorkItem!)
                ?? throw new InvalidOperationException("Project deliverable not found");
            context["projectDeliverableId"] = deliverable.Id;
            context["projectId"] = deliverable.ProjectId;
            context["projectCode"] = deliverable.Project?.ProjectCode ?? string.Empty;
            context["projectTitle"] = deliverable.Project?.Title ?? string.Empty;
            context["deliverableTitle"] = deliverable.Title;
            context["deliverableStatus"] = deliverable.Status;
            context["targetDate"] = deliverable.TargetDate;
            context["externalSignOffRequired"] = deliverable.ExternalSignOffRequired;
            context["externalSubmissionAllowed"] = deliverable.ExternalSubmissionAllowed;
            context["workItemId"] = deliverable.WorkItemId;
            context["workItemTitle"] = deliverable.WorkItem?.Title ?? string.Empty;
            context["milestoneId"] = deliverable.MilestoneId;
            context["milestoneTitle"] = deliverable.Milestone?.Title ?? string.Empty;
        }

        if (IsEntityType(entityTypeRecord, "PROJECT_CLOSURE", "ProjectClosure", "Project Closure"))
        {
            var closure = await _unitOfWork.Repository<ProjectClosure>()
                .FirstOrDefaultAsync(x => x.Id == entityId, x => x.Project)
                ?? throw new InvalidOperationException("Project closure not found");
            context["projectClosureId"] = closure.Id;
            context["projectId"] = closure.ProjectId;
            context["projectCode"] = closure.Project?.ProjectCode ?? string.Empty;
            context["projectTitle"] = closure.Project?.Title ?? string.Empty;
            context["closureStatus"] = closure.Status;
            context["finalBudget"] = closure.FinalBudget ?? 0m;
            context["finalCost"] = closure.FinalCost ?? 0m;
            context["deliverablesAccepted"] = closure.DeliverablesAccepted;
            context["tasksCompletedOrWaived"] = closure.TasksCompletedOrWaived;
            context["assetsReconciled"] = closure.AssetsReconciled;
            context["openItemsDisposed"] = closure.OpenItemsDisposed;
        }

        if (IsEntityType(entityTypeRecord, "BUSINESS_PARTNER", "BusinessPartner", "Business Partner", "Supplier", "Contractor"))
        {
            var partner = await _businessPartnerRepository.GetByIdAsync(entityId) ?? throw new InvalidOperationException("Business partner not found");
            context["businessPartnerId"] = partner.Id;
            context["partnerCode"] = partner.PartnerCode;
            context["partnerName"] = partner.PartnerName;
            context["partnerType"] = partner.PartnerType;
            context["registrationStatus"] = partner.RegistrationStatus;
            context["approvalStatus"] = partner.ApprovalStatus;
            context["isActive"] = partner.IsActive;
            context["isPreferred"] = partner.IsPreferred;
            context["isBlacklisted"] = partner.IsBlacklisted;
            context["performanceRating"] = partner.PerformanceRating ?? 0m;
            context["riskLevel"] = partner.RiskLevel;
            context["createdById"] = partner.CreatedById;
            context["createdAt"] = partner.CreatedAt;
        }

        if (IsEntityType(entityTypeRecord, "SERVICE_REQUEST", "ServiceRequest", "Service Request"))
        {
            var repo = _unitOfWork.Repository<EhcServiceRequest>();
            var req = await repo.GetByIdAsync(entityId, r => r.RequestType, r => r.RequesterUser)
                ?? throw new InvalidOperationException("Service request not found");

            context["serviceRequestId"] = req.Id;
            context["serviceRequestNumber"] = req.RequestNumber;
            context["serviceRequestTitle"] = req.Title ?? string.Empty;
            context["serviceRequestStatus"] = req.Status.ToString();
            context["serviceRequestTypeCode"] = req.RequestType?.Code ?? string.Empty;
            context["serviceRequestTypeName"] = req.RequestType?.Name ?? string.Empty;
            context["requesterUserId"] = req.RequesterUserId;
            context["requesterEmail"] = req.RequesterUser?.Email ?? string.Empty;
            context["formDataJson"] = req.FormDataJson ?? "{}";
        }

        return context.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsEntityType(WorkflowEntityType entityTypeRecord, params string[] matches)
    {
        foreach (var match in matches)
        {
            var normalizedMatch = NormalizeEntityTypeKey(match);
            if (string.IsNullOrWhiteSpace(normalizedMatch))
            {
                continue;
            }

            if (NormalizeEntityTypeKey(entityTypeRecord.Code) == normalizedMatch)
            {
                return true;
            }

            if (NormalizeEntityTypeKey(entityTypeRecord.Name) == normalizedMatch)
            {
                return true;
            }
        }

        return false;
    }

    private static WorkflowApprovalConfigDto? GetApprovalConfig(WorkflowStepInstance stepInstance)
    {
        var configurationJson = stepInstance.WorkflowStep?.Configuration;
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return null;
        }

        try
        {
            var serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            serializerOptions.Converters.Add(new JsonStringEnumConverter());
            return JsonSerializer.Deserialize<WorkflowStepConfigurationDto>(configurationJson, serializerOptions)?.ApprovalConfig;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<List<string>> ValidateCrossStepSodAsync(
        WorkflowApprovalConfigDto? config,
        WorkflowInstance instance,
        WorkflowStepInstance currentStep,
        string entityType,
        Guid entityId,
        Guid userId)
    {
        if (config?.ConflictRules?.Any(rule => rule.IsEnabled) != true)
        {
            return new List<string>();
        }

        var previousSteps = (await _stepInstanceRepository.GetByWorkflowInstanceAsync(instance.Id))
            .Where(step => step.Id != currentStep.Id &&
                (step.CompletedDate ?? step.CreatedDate) <= (currentStep.StartedDate ?? currentStep.CreatedDate))
            .ToList();
        var approvalsByStep = new Dictionary<Guid, IReadOnlyCollection<WorkflowApproval>>();
        foreach (var step in previousSteps)
        {
            approvalsByStep[step.Id] = (await _approvalRepository.GetByStepInstanceAsync(step.Id)).ToList();
        }

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var entityTypeRecord = await ResolveEntityTypeAsync(entityType, tenantId);
        var context = await BuildEntityContextAsync(entityTypeRecord, entityId);

        return WorkflowCrossStepSodEvaluator.Validate(config, previousSteps, approvalsByStep, context, userId);
    }

    private static string NormalizeEntityTypeKey(string? value)
        => new((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());

    private async Task AttachWorkflowInstanceAsync(WorkflowEntityType entityTypeRecord, Guid entityId, Guid workflowInstanceId)
    {
        if (string.Equals(entityTypeRecord.Code, "JOB_CARD", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityTypeRecord.Name, "JobCard", StringComparison.OrdinalIgnoreCase))
        {
            var jobCard = await _jobCardRepository.GetByIdAsync(entityId) ?? throw new InvalidOperationException("Job card not found");
            jobCard.WorkflowInstanceId = workflowInstanceId;
            jobCard.UpdatedAt = DateTime.UtcNow;
            jobCard.UpdatedBy = _currentUserService.UserId ?? "System";
            await _jobCardRepository.UpdateAsync(jobCard);
        }
    }

    private static WorkflowStepAction MapToStepAction(string action)
    {
        return action.Trim().ToLowerInvariant() switch
        {
            "approve" => WorkflowStepAction.Complete,
            "reject" => WorkflowStepAction.Reject,
            "requestchanges" => WorkflowStepAction.RequestInformation,
            "requestinformation" => WorkflowStepAction.RequestInformation,
            "delegate" => WorkflowStepAction.Delegate,
            _ => WorkflowStepAction.Complete
        };
    }

    private async Task PopulateApprovalItemDetailsAsync(
        WorkflowApprovalItem item,
        WorkflowEntityType? entityTypeRecord,
        Guid entityId)
    {
        if (entityTypeRecord == null)
        {
            item.EntityTitle = entityId.ToString();
            item.EntityDescription = string.Empty;
            return;
        }

        if (string.Equals(entityTypeRecord.Code, "JOB_CARD", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityTypeRecord.Name, "JobCard", StringComparison.OrdinalIgnoreCase))
        {
            var jobCard = await _jobCardRepository.GetByIdAsync(entityId);
            if (jobCard != null)
            {
                item.EntityTitle = jobCard.Title;
                item.EntityDescription = jobCard.ProblemDescription ?? jobCard.Description ?? string.Empty;
                return;
            }
        }

        if (IsEntityType(entityTypeRecord, "PROCUREMENT_PLAN", "ProcurementPlan", "Procurement Plan"))
        {
            try
            {
                var plan = await _procurementPlanRepository.GetWithFullDetailsAsync(entityId);
                if (plan != null)
                {
                    item.EntityTitle = $"{plan.PlanNumber} - {plan.Title}";
                    item.EntityDescription = string.IsNullOrWhiteSpace(plan.Department?.Name)
                        ? $"{plan.FiscalYear} {plan.PlanningCycle} plan"
                        : $"{plan.Department.Name} / {plan.FiscalYear} {plan.PlanningCycle} plan";
                    return;
                }
            }
            catch
            {
                // ignore and fall through
            }
        }

        if (string.Equals(entityTypeRecord.Code, "SERVICE_REQUEST", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityTypeRecord.Name, "ServiceRequest", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityTypeRecord.Name, "Service Request", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var repo = _unitOfWork.Repository<EhcServiceRequest>();
                var req = await repo.GetByIdAsync(entityId, r => r.RequestType)
                    ?? throw new InvalidOperationException("Service request not found");
                item.EntityTitle = $"{req.RequestNumber} - {req.RequestType?.Name ?? "Service Request"}";
                item.EntityDescription = req.Title ?? string.Empty;
                return;
            }
            catch
            {
                // ignore and fall through
            }
        }

        if (string.Equals(entityTypeRecord.Code, "PROJECT_DELIVERABLE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityTypeRecord.Name, "ProjectDeliverable", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityTypeRecord.Name, "Project Deliverable", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var deliverable = await _unitOfWork.Repository<ProjectDeliverable>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.Project);
                if (deliverable != null)
                {
                    item.EntityTitle = $"{deliverable.Project?.ProjectCode ?? "PRJ"} - {deliverable.Title}";
                    item.EntityDescription = deliverable.Project?.Title ?? string.Empty;
                    return;
                }
            }
            catch
            {
                // ignore and fall through
            }
        }

        if (string.Equals(entityTypeRecord.Code, "PROJECT_CLOSURE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityTypeRecord.Name, "ProjectClosure", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityTypeRecord.Name, "Project Closure", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var closure = await _unitOfWork.Repository<ProjectClosure>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.Project);
                if (closure != null)
                {
                    item.EntityTitle = $"{closure.Project?.ProjectCode ?? "PRJ"} - Closure";
                    item.EntityDescription = closure.Project?.Title ?? string.Empty;
                    return;
                }
            }
            catch
            {
                // ignore and fall through
            }
        }

        if (IsEntityType(entityTypeRecord, "AllocationRunBatch", "Allocation Run Batch"))
        {
            try
            {
                var batch = await _unitOfWork.Repository<AllocationRunBatch>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.AllocationRule);
                if (batch != null)
                {
                    item.EntityTitle = batch.BatchNumber;
                    item.EntityDescription = batch.AllocationRule == null
                        ? batch.Description ?? string.Empty
                        : $"{batch.AllocationRule.Code} - {batch.AllocationRule.Name}";
                    return;
                }
            }
            catch
            {
                // ignore and fall through
            }
        }

        item.EntityTitle = entityId.ToString();
        item.EntityDescription = string.Empty;
    }
}
