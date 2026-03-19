using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Maintenance;
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
using Microsoft.AspNetCore.Identity;
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
        _currentUserService = currentUserService;
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<WorkflowExecutionResult> StartApprovalWorkflowAsync(string entityType, Guid entityId)
    {
        var initiatedById = GetCurrentUserId();
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;

        var entityTypeRecord = await ResolveEntityTypeAsync(entityType, tenantId);
        var definition = await ResolveActiveDefinitionAsync(entityTypeRecord, tenantId);

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
            currentStepInstance = await _stepInstanceRepository.GetCurrentStepAsync(candidate.Id);
            if (currentStepInstance != null)
            {
                existingActiveInstance = candidate;
                break;
            }
        }

        existingActiveInstance ??= activeCandidates.FirstOrDefault();

        if (existingActiveInstance != null)
        {
            // Self-heal: if the active step is an approval step and approvals are missing, materialize them.
            currentStepInstance ??= await _stepInstanceRepository.GetCurrentStepAsync(existingActiveInstance.Id);
            if (currentStepInstance?.WorkflowStep?.StepType == WorkflowStepType.Approval)
            {
                try
                {
                    var existingDataContext = await BuildEntityContextAsync(entityTypeRecord, entityId);
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
            definition.Name,
            entityId,
            initiatedById,
            dataContext);

        await AttachWorkflowInstanceAsync(entityTypeRecord, entityId, workflowInstance.Id);

        return new WorkflowExecutionResult
        {
            Success = true,
            Message = "Workflow started",
            Status = workflowInstance.Status,
            WorkflowInstanceId = workflowInstance.Id,
            CurrentStepId = workflowInstance.CurrentStepId
        };
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

        var user = await _userManager.FindByIdAsync(userId.ToString());
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
            (a.ApproverId == userId || (!string.IsNullOrWhiteSpace(a.ApproverRole) && roleSet.Contains(a.ApproverRole))));

        var currentStepType = stepInstance.WorkflowStep?.StepType;
        var isApprovalStep = currentStepType == WorkflowStepType.Approval;

        // Fallback for workflows that use manual/assigned steps without explicit approval rows.
        // IMPORTANT: Do NOT allow this fallback for Approval steps; approvals must come from approver rules/rows.
        if (!canApprove &&
            !isApprovalStep &&
            stepInstance.AssignedToId.HasValue &&
            stepInstance.AssignedToId.Value == userId &&
            (stepInstance.Status == WorkflowStepInstanceStatus.Pending || stepInstance.Status == WorkflowStepInstanceStatus.InProgress))
        {
            canApprove = true;
        }

        // Fallback for instances created before approval rows were generated correctly:
        // allow if the current step configuration explicitly includes this user/role.
        if (!canApprove && IsUserConfiguredAsApprover(stepInstance, userId, roleSet))
        {
            canApprove = true;
        }

        if (!canApprove)
        {
            _logger.LogDebug(
                "Workflow approval check denied: user {UserId} has no matching pending approval for step {StepInstanceId}",
                userId,
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
        return await _workflowEngine.ProcessStepAsync(stepInstance.Id, userId, stepAction, comments: comments);
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
        entityTypeRecord = activeTypes.FirstOrDefault(et =>
            string.Equals(et.Code, entityType, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(et.Name, entityType, StringComparison.OrdinalIgnoreCase));

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
            var stepInstance = await _stepInstanceRepository.GetCurrentStepAsync(candidate.Id);
            if (stepInstance != null)
            {
                return candidate;
            }
        }

        return active.FirstOrDefault();
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
            if (!string.IsNullOrWhiteSpace(entityTypeRecord.Code) &&
                string.Equals(entityTypeRecord.Code, match, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(entityTypeRecord.Name) &&
                string.Equals(entityTypeRecord.Name, match, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

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

        item.EntityTitle = entityId.ToString();
        item.EntityDescription = string.Empty;
    }
}
