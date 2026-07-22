using System.Text.Json;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Interfaces.Workflow;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Enhanced maintenance workflow service that integrates with the new workflow engine foundation.
/// Provides configurable and extensible workflow execution for maintenance operations.
/// </summary>
public class EnhancedMaintenanceWorkflowService : IEnhancedMaintenanceWorkflowService
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IWorkflowInstanceRepository _workflowInstanceRepository;
    private readonly IWorkflowStepInstanceRepository _workflowStepInstanceRepository;
    private readonly IWorkflowDefinitionRepository _workflowDefinitionRepository;
    private readonly IWorkflowStepRepository _workflowStepRepository;
    private readonly IWorkflowTransitionRepository _workflowTransitionRepository;
    private readonly IWorkflowEntityTypeRepository _workflowEntityTypeRepository;
    private readonly IWorkOrderService _workOrderService;
    private readonly IWorkOrderRepository _workOrderRepository;
    private readonly IMaintenanceAssetRepository _assetRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<EnhancedMaintenanceWorkflowService> _logger;

    // Predefined workflow keys for maintenance operations
    private const string WORK_ORDER_CREATION_WORKFLOW = "work-order-creation";
    private const string PREVENTIVE_MAINTENANCE_WORKFLOW = "preventive-maintenance";
    private const string CORRECTIVE_MAINTENANCE_WORKFLOW = "corrective-maintenance";
    private const string EMERGENCY_MAINTENANCE_WORKFLOW = "emergency-maintenance";
    private const string SAFETY_INSPECTION_WORKFLOW = "safety-inspection";
    private const string ASSET_COMMISSIONING_WORKFLOW = "asset-commissioning";
    private const string ASSET_DECOMMISSIONING_WORKFLOW = "asset-decommissioning";

    public EnhancedMaintenanceWorkflowService(
        IWorkflowEngine workflowEngine,
        IWorkflowInstanceRepository workflowInstanceRepository,
        IWorkflowStepInstanceRepository workflowStepInstanceRepository,
        IWorkflowDefinitionRepository workflowDefinitionRepository,
        IWorkflowStepRepository workflowStepRepository,
        IWorkflowTransitionRepository workflowTransitionRepository,
        IWorkflowEntityTypeRepository workflowEntityTypeRepository,
        IWorkOrderService workOrderService,
        IWorkOrderRepository workOrderRepository,
        IMaintenanceAssetRepository assetRepository,
        ICurrentUserService currentUserService,
        ILogger<EnhancedMaintenanceWorkflowService> logger)
    {
        _workflowEngine = workflowEngine;
        _workflowInstanceRepository = workflowInstanceRepository;
        _workflowStepInstanceRepository = workflowStepInstanceRepository;
        _workflowDefinitionRepository = workflowDefinitionRepository;
        _workflowStepRepository = workflowStepRepository;
        _workflowTransitionRepository = workflowTransitionRepository;
        _workflowEntityTypeRepository = workflowEntityTypeRepository;
        _workOrderService = workOrderService;
        _workOrderRepository = workOrderRepository;
        _assetRepository = assetRepository;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    #region Enhanced Work Order Workflow Management

    /// <summary>
    /// Creates a work order with configurable workflow based on maintenance type and conditions
    /// </summary>
    public async Task<EnhancedWorkOrderCreationResult> CreateWorkOrderWithWorkflowAsync(
        CreateWorkOrderDto createDto, string tenantId)
    {
        try
        {
            _logger.LogInformation("Creating work order with enhanced workflow for asset {AssetId}", createDto.AssetId);

            var userId = _currentUserService.UserId;
            var result = new EnhancedWorkOrderCreationResult
            {
                Success = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            };

            // Create the basic work order first
            var workOrder = await CreateBasicWorkOrderAsync(createDto);
            result.WorkOrderId = workOrder.Id;
            result.WorkOrderNumber = workOrder.WorkOrderNumber;

            // Determine appropriate workflow based on work order characteristics
            var workflowKey = await DetermineWorkflowKeyAsync(workOrder);
            result.SelectedWorkflowKey = workflowKey;

            // Prepare workflow context data
            var workflowData = await PrepareWorkflowContextAsync(workOrder);

            // Start the workflow - using existing interface (temporarily stubbed)
            var workflowResult = await StartWorkflowWithExistingEngineAsync(
                workflowKey, workOrder.Id, Guid.Parse(userId), workflowData);

            if (workflowResult.Success)
            {
                result.WorkflowInstanceId = workflowResult.WorkflowInstanceId;
                result.InitialWorkflowStatus = workflowResult.Status;
                result.CurrentStepId = workflowResult.CurrentStepId;
                result.SuccessMessages.Add("Work order created and workflow initiated successfully");

                // Apply initial workflow decisions
                await ApplyInitialWorkflowDecisionsAsync(workOrder, workflowResult, workflowData);
            }
            else
            {
                result.WarningMessages.Add($"Work order created but workflow initiation failed: {workflowResult.Message}");
                result.WorkflowErrors = workflowResult.Errors?.ToList() ?? new List<WorkflowExecutionError>();
            }

            _logger.LogInformation("Work order {WorkOrderId} created with workflow {WorkflowKey}",
                workOrder.Id, workflowKey);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating work order with workflow");
            throw;
        }
    }

    /// <summary>
    /// Advances work order through workflow steps with enhanced validation and automation
    /// </summary>
    public async Task<EnhancedWorkflowStepResult> AdvanceWorkOrderWorkflowAsync(
        Guid workOrderId, string stepAction, Dictionary<string, object>? stepData = null)
    {
        try
        {
            _logger.LogInformation("Advancing workflow for work order {WorkOrderId} with action {StepAction}",
                workOrderId, stepAction);

            var userId = _currentUserService.UserId;
            var workOrder = await _workOrderRepository.GetByIdAsync(workOrderId) ?? throw new ArgumentException($"Work order {workOrderId} not found");
            var result = new EnhancedWorkflowStepResult
            {
                WorkOrderId = workOrderId,
                StepAction = stepAction,
                ProcessedAt = DateTime.UtcNow,
                ProcessedBy = userId,
                Success = true
            };

            // Get current workflow instance
            var workflowInstance = await GetActiveWorkflowInstanceAsync(
                workOrderId.ToString(), "WorkOrder");

            if (workflowInstance == null)
            {
                result.Success = false;
                result.ErrorMessages.Add("No active workflow found for this work order");
                return result;
            }

            // Prepare step execution data
            var executionData = stepData ?? new Dictionary<string, object>();
            executionData = await EnhanceStepExecutionDataAsync(workOrder, executionData, stepAction);

            // Execute workflow step based on action
            WorkflowExecutionResult workflowResult;
            switch (stepAction.ToLower())
            {
                case "approve":
                    workflowResult = await ExecuteApprovalStepAsync(workflowInstance.Id, executionData, userId);
                    break;
                case "reject":
                    workflowResult = await ExecuteRejectionStepAsync(workflowInstance.Id, executionData, userId);
                    break;
                case "start":
                    workflowResult = await ExecuteStartStepAsync(workflowInstance.Id, executionData, userId);
                    break;
                case "complete":
                    workflowResult = await ExecuteCompletionStepAsync(workflowInstance.Id, executionData, userId);
                    break;
                case "escalate":
                    workflowResult = await ExecuteEscalationStepAsync(workflowInstance.Id, executionData, userId);
                    break;
                default:
                    workflowResult = await ExecuteWorkflowStepAsync(
                        workflowInstance.Id, stepAction, executionData, userId);
                    break;
            }

            // Process workflow execution result
            result.WorkflowExecutionResult = workflowResult;
            result.Success = workflowResult.Success;

            if (workflowResult.Success)
            {
                result.NewWorkflowStatus = workflowResult.Status;
                result.NewCurrentStepId = workflowResult.CurrentStepId;
                result.SuccessMessages.Add($"Workflow step '{stepAction}' executed successfully");

                // Apply post-step processing
                await ApplyPostStepProcessingAsync(workOrder, workflowResult, executionData);
            }
            else
            {
                result.ErrorMessages.Add(workflowResult.Message ?? "Workflow step execution failed");
                result.WorkflowErrors = workflowResult.Errors?.ToList() ?? new List<WorkflowExecutionError>();
            }

            _logger.LogInformation("Workflow step {StepAction} for work order {WorkOrderId} executed. Success: {Success}",
                stepAction, workOrderId, result.Success);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error advancing workflow for work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    /// <summary>
    /// Gets enhanced workflow status for a work order including maintenance-specific context
    /// </summary>
    public async Task<EnhancedMaintenanceWorkflowStatusDto> GetWorkOrderWorkflowStatusAsync(Guid workOrderId)
    {
        try
        {
            var workOrder = await _workOrderRepository.GetByIdAsync(workOrderId) ?? throw new ArgumentException($"Work order {workOrderId} not found");
            var workflowStatus = await GetWorkflowStatusAsync(workOrderId.ToString(), "WorkOrder");

            var result = new EnhancedMaintenanceWorkflowStatusDto
            {
                WorkOrderId = workOrderId,
                WorkOrderNumber = workOrder.WorkOrderNumber,
                WorkOrderStatus = workOrder.Status,
                AssetId = workOrder.AssetId,
                AssignedTechnicianId = workOrder.AssignedTechnicianId,
                Priority = workOrder.PriorityLevel?.Name ?? "Normal",
                MaintenanceType = workOrder.MaintenanceType?.Name ?? "Unknown"
            };

            if (workflowStatus != null)
            {
                result.WorkflowInstanceId = workflowStatus?.WorkflowInstanceId;
                result.WorkflowName = workflowStatus.WorkflowName;
                result.WorkflowStatus = workflowStatus.Status;
                result.StartedDate = workflowStatus.StartedDate;
                result.CompletedDate = workflowStatus.CompletedDate;
                result.Progress = workflowStatus.Progress;
                result.Steps = workflowStatus.Steps;
                result.PendingApprovals = workflowStatus.PendingApprovals;

                // Add maintenance-specific workflow context
                result.RequiresQualityControl = await RequiresQualityControlAsync(workOrder);
                result.RequiresManagerApproval = await RequiresManagerApprovalAsync(workOrder);
                result.RequiresSafetyInspection = await RequiresSafetyInspectionAsync(workOrder);
                result.EstimatedCompletionTime = await EstimateCompletionTimeAsync(workOrder, workflowStatus);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workflow status for work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    /// <summary>
    /// Configures workflow templates for different maintenance scenarios
    /// </summary>
    public async Task<WorkflowTemplateConfigurationResult> ConfigureMaintenanceWorkflowTemplatesAsync(string tenantId)
    {
        try
        {
            _logger.LogInformation("Configuring maintenance workflow templates for tenant {TenantId}", tenantId);

            var result = new WorkflowTemplateConfigurationResult
            {
                TenantId = tenantId,
                ConfiguredAt = DateTime.UtcNow,
                Success = true
            };

            // Configure work order creation workflow
            var creationWorkflow = await CreateWorkOrderCreationWorkflowTemplateAsync();
            await CreateOrUpdateWorkflowDefinitionAsync(creationWorkflow, tenantId);
            result.ConfiguredWorkflows.Add(WORK_ORDER_CREATION_WORKFLOW);

            // Configure preventive maintenance workflow
            var preventiveWorkflow = await CreatePreventiveMaintenanceWorkflowTemplateAsync();
            await CreateOrUpdateWorkflowDefinitionAsync(preventiveWorkflow, tenantId);
            result.ConfiguredWorkflows.Add(PREVENTIVE_MAINTENANCE_WORKFLOW);

            // Configure corrective maintenance workflow
            var correctiveWorkflow = await CreateCorrectiveMaintenanceWorkflowTemplateAsync();
            await CreateOrUpdateWorkflowDefinitionAsync(correctiveWorkflow, tenantId);
            result.ConfiguredWorkflows.Add(CORRECTIVE_MAINTENANCE_WORKFLOW);

            // Configure emergency maintenance workflow
            var emergencyWorkflow = await CreateEmergencyMaintenanceWorkflowTemplateAsync();
            await CreateOrUpdateWorkflowDefinitionAsync(emergencyWorkflow, tenantId);
            result.ConfiguredWorkflows.Add(EMERGENCY_MAINTENANCE_WORKFLOW);

            // Configure safety inspection workflow
            var safetyWorkflow = await CreateSafetyInspectionWorkflowTemplateAsync();
            await CreateOrUpdateWorkflowDefinitionAsync(safetyWorkflow, tenantId);
            result.ConfiguredWorkflows.Add(SAFETY_INSPECTION_WORKFLOW);

            result.SuccessMessages.Add($"Successfully configured {result.ConfiguredWorkflows.Count} maintenance workflow templates");

            _logger.LogInformation("Configured {Count} maintenance workflow templates for tenant {TenantId}",
                result.ConfiguredWorkflows.Count, tenantId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring maintenance workflow templates for tenant {TenantId}", tenantId);
            throw;
        }
    }

    #endregion

    #region Private Workflow Determination Methods

    private async Task<string> DetermineWorkflowKeyAsync(WorkOrder workOrder)
    {
        // Load full work order details
        var asset = await _assetRepository.GetByIdAsync(workOrder.AssetId);

        // Determine workflow based on work order and asset characteristics
        if (workOrder.PriorityLevel?.Level == 1) // Emergency priority
        {
            return EMERGENCY_MAINTENANCE_WORKFLOW;
        }

        if (workOrder.MaintenanceType?.Name?.ToLower().Contains("safety", StringComparison.CurrentCultureIgnoreCase) == true ||
            asset?.Criticality == AssetCriticality.Critical)
        {
            return SAFETY_INSPECTION_WORKFLOW;
        }

        if (workOrder.MaintenanceType?.Name?.ToLower().Contains("preventive", StringComparison.CurrentCultureIgnoreCase) == true ||
            workOrder.MaintenanceType?.Name?.ToLower().Contains("scheduled", StringComparison.CurrentCultureIgnoreCase) == true)
        {
            return PREVENTIVE_MAINTENANCE_WORKFLOW;
        }

        if (workOrder.MaintenanceType?.Name?.ToLower().Contains("corrective", StringComparison.CurrentCultureIgnoreCase) == true ||
            workOrder.MaintenanceType?.Name?.ToLower().Contains("breakdown", StringComparison.CurrentCultureIgnoreCase) == true)
        {
            return CORRECTIVE_MAINTENANCE_WORKFLOW;
        }

        // Default to standard work order creation workflow
        return WORK_ORDER_CREATION_WORKFLOW;
    }

    private async Task<Dictionary<string, object>> PrepareWorkflowContextAsync(WorkOrder workOrder)
    {
        var asset = await _assetRepository.GetByIdAsync(workOrder.AssetId);

        return new Dictionary<string, object>
        {
            ["workOrderId"] = workOrder.Id,
            ["workOrderNumber"] = workOrder.WorkOrderNumber,
            ["assetId"] = workOrder.AssetId,
            ["assetCriticality"] = asset?.Criticality.ToString() ?? "Medium",
            ["maintenanceType"] = workOrder.MaintenanceType?.Name ?? "General",
            ["priority"] = workOrder.PriorityLevel?.Name ?? "Normal",
            ["priorityLevel"] = workOrder.PriorityLevel?.Level ?? 3,
            ["estimatedHours"] = workOrder.EstimatedHours,
            ["estimatedCost"] = workOrder.EstimatedCost,
            ["assignedTechnicianId"] = workOrder.AssignedTechnicianId?.ToString() ?? "",
            ["requesterEmployeeId"] = workOrder.RequestedById?.ToString() ?? "",
            ["createdDate"] = workOrder.CreatedAt,
            ["scheduledDate"] = workOrder.RequestedStartDate,
            ["isEmergency"] = workOrder.PriorityLevel?.Level == 1,
            ["requiresSafetyInspection"] = await RequiresSafetyInspectionAsync(workOrder),
            ["requiresQualityControl"] = await RequiresQualityControlAsync(workOrder),
            ["requiresManagerApproval"] = await RequiresManagerApprovalAsync(workOrder),
            ["partsRequired"] = workOrder.Parts?.Any() == true,
            ["partsCount"] = workOrder.Parts?.Count ?? 0,
            ["totalPartsValue"] = workOrder.Parts?.Sum(p => p.UnitCost * p.QuantityRequired) ?? 0
        };
    }

    #endregion

    #region Private Workflow Step Execution Methods

    private async Task<WorkflowExecutionResult> ExecuteApprovalStepAsync(
        Guid workflowInstanceId, Dictionary<string, object> executionData, string userId)
    {
        executionData["approvalDecision"] = "Approved";
        executionData["approvedBy"] = userId;
        executionData["approvedAt"] = DateTime.UtcNow;

        return await ExecuteWorkflowStepAsync(workflowInstanceId, "approve", executionData, userId);
    }

    private async Task<WorkflowExecutionResult> ExecuteRejectionStepAsync(
        Guid workflowInstanceId, Dictionary<string, object> executionData, string userId)
    {
        executionData["approvalDecision"] = "Rejected";
        executionData["rejectedBy"] = userId;
        executionData["rejectedAt"] = DateTime.UtcNow;

        return await ExecuteWorkflowStepAsync(workflowInstanceId, "reject", executionData, userId);
    }

    private async Task<WorkflowExecutionResult> ExecuteStartStepAsync(
        Guid workflowInstanceId, Dictionary<string, object> executionData, string userId)
    {
        executionData["startedBy"] = userId;
        executionData["startedAt"] = DateTime.UtcNow;
        executionData["workStatus"] = "InProgress";

        return await ExecuteWorkflowStepAsync(workflowInstanceId, "start", executionData, userId);
    }

    private async Task<WorkflowExecutionResult> ExecuteCompletionStepAsync(
        Guid workflowInstanceId, Dictionary<string, object> executionData, string userId)
    {
        executionData["completedBy"] = userId;
        executionData["completedAt"] = DateTime.UtcNow;
        executionData["workStatus"] = "Completed";

        return await ExecuteWorkflowStepAsync(workflowInstanceId, "complete", executionData, userId);
    }

    private async Task<WorkflowExecutionResult> ExecuteEscalationStepAsync(
        Guid workflowInstanceId, Dictionary<string, object> executionData, string userId)
    {
        executionData["escalatedBy"] = userId;
        executionData["escalatedAt"] = DateTime.UtcNow;
        executionData["escalationReason"] = executionData.GetValueOrDefault("escalationReason", "Manual escalation");

        return await ExecuteWorkflowStepAsync(workflowInstanceId, "escalate", executionData, userId);
    }

    #endregion

    #region Private Workflow Template Creation Methods

    private static async Task<CreateWorkflowDefinitionDto> CreateWorkOrderCreationWorkflowTemplateAsync()
    {
        return new CreateWorkflowDefinitionDto
        {
            Name = "Work Order Creation Workflow",
            Description = "Standard workflow for work order creation and initial processing",
            EntityType = "WorkOrder",
            Steps = new List<CreateWorkflowStepDto>
            {
                new()
                {
                    Name = "Initial Validation",
                    Description = "Validate work order requirements and data",
                    StepType = WorkflowStepType.Validation,
                    Order = 1,
                    IsRequired = true
                },
                new()
                {
                    Name = "Auto Assignment",
                    Description = "Automatically assign technician based on availability and skills",
                    StepType = WorkflowStepType.Automatic,
                    Order = 2,
                    Configuration = new WorkflowStepConfigurationDto
                    {
                        AssignmentRules = new List<WorkflowAssignmentRuleDto>
                        {
                            new()
                            {
                                AssignmentType = WorkflowAssignmentType.Dynamic,
                                DynamicExpression = "GetAvailableTechnician(maintenanceType, priority)",
                                Priority = 1
                            }
                        }
                    }
                },
                new()
                {
                    Name = "Work Order Ready",
                    Description = "Work order is ready for execution",
                    StepType = WorkflowStepType.Manual,
                    Order = 3,
                    IsRequired = false
                }
            }
        };
    }

    private static async Task<CreateWorkflowDefinitionDto> CreatePreventiveMaintenanceWorkflowTemplateAsync()
    {
        return new CreateWorkflowDefinitionDto
        {
            Name = "Preventive Maintenance Workflow",
            Description = "Workflow for scheduled preventive maintenance activities",
            EntityType = "WorkOrder",
            Steps = new List<CreateWorkflowStepDto>
            {
                new()
                {
                    Name = "Schedule Validation",
                    Description = "Validate maintenance schedule and timing",
                    StepType = WorkflowStepType.Validation,
                    Order = 1
                },
                new()
                {
                    Name = "Parts Availability Check",
                    Description = "Verify all required parts are available",
                    StepType = WorkflowStepType.Automatic,
                    Order = 2,
                    Configuration = new WorkflowStepConfigurationDto
                    {
                        SkipCondition = new WorkflowConditionDto
                        {
                            ConditionType = WorkflowConditionType.Expression,
                            Expression = "partsRequired == false"
                        }
                    }
                },
                new()
                {
                    Name = "Work Execution",
                    Description = "Execute preventive maintenance tasks",
                    StepType = WorkflowStepType.Manual,
                    Order = 3,
                    EstimatedHours = 4.0
                },
                new()
                {
                    Name = "Quality Check",
                    Description = "Perform quality control inspection",
                    StepType = WorkflowStepType.QualityControl,
                    Order = 4,
                    Configuration = new WorkflowStepConfigurationDto
                    {
                        QualityConfig = new WorkflowQualityConfigDto
                        {
                            QualityChecks = new List<WorkflowQualityCheckDto>
                            {
                                new()
                                {
                                    Name = "Work Completion Verification",
                                    Description = "Verify all maintenance tasks completed",
                                    IsRequired = true
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    private static async Task<CreateWorkflowDefinitionDto> CreateEmergencyMaintenanceWorkflowTemplateAsync()
    {
        return new CreateWorkflowDefinitionDto
        {
            Name = "Emergency Maintenance Workflow",
            Description = "Expedited workflow for emergency maintenance situations",
            EntityType = "WorkOrder",
            Steps = new List<CreateWorkflowStepDto>
            {
                new()
                {
                    Name = "Emergency Response",
                    Description = "Immediate response to emergency maintenance request",
                    StepType = WorkflowStepType.Automatic,
                    Order = 1,
                    Configuration = new WorkflowStepConfigurationDto
                    {
                        NotificationConfig = new WorkflowNotificationConfigDto
                        {
                            Triggers = new List<WorkflowNotificationTriggerDto>
                            {
                                new()
                                {
                                    Event = WorkflowNotificationEvent.StepStarted,
                                    Channel = WorkflowNotificationChannel.Email,
                                    Recipients = new List<WorkflowAssignmentRuleDto>
                                    {
                                        new()
                                        {
                                            AssignmentType = WorkflowAssignmentType.Role,
                                            Role = "MaintenanceManager"
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                new()
                {
                    Name = "Immediate Assignment",
                    Description = "Assign available emergency response technician",
                    StepType = WorkflowStepType.Automatic,
                    Order = 2
                },
                new()
                {
                    Name = "Emergency Work",
                    Description = "Execute emergency maintenance work",
                    StepType = WorkflowStepType.Manual,
                    Order = 3,
                    Configuration = new WorkflowStepConfigurationDto
                    {
                        EscalationRules = new List<WorkflowEscalationRuleDto>
                        {
                            new()
                            {
                                DelayHours = 2,
                                Action = WorkflowEscalationAction.NotifyManager,
                                TriggerCondition = new WorkflowConditionDto
                                {
                                    ConditionType = WorkflowConditionType.Expression,
                                    Expression = "stepDurationHours > 2"
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    private static async Task<CreateWorkflowDefinitionDto> CreateCorrectiveMaintenanceWorkflowTemplateAsync()
    {
        return new CreateWorkflowDefinitionDto
        {
            Name = "Corrective Maintenance Workflow",
            Description = "Workflow for corrective maintenance and breakdown repairs",
            EntityType = "WorkOrder",
            Steps = new List<CreateWorkflowStepDto>
            {
                new()
                {
                    Name = "Failure Analysis",
                    Description = "Analyze equipment failure and determine corrective action",
                    StepType = WorkflowStepType.Manual,
                    Order = 1,
                    Configuration = new WorkflowStepConfigurationDto
                    {
                        FormFields = new List<WorkflowFormFieldDto>
                        {
                            new()
                            {
                                Name = "failureMode",
                                Label = "Failure Mode",
                                FieldType = WorkflowFieldType.Text,
                                IsRequired = true
                            },
                            new()
                            {
                                Name = "rootCause",
                                Label = "Root Cause",
                                FieldType = WorkflowFieldType.TextArea,
                                IsRequired = true
                            }
                        }
                    }
                },
                new()
                {
                    Name = "Supervisor Approval",
                    Description = "Supervisor approval for corrective action plan",
                    StepType = WorkflowStepType.Approval,
                    Order = 2,
                    Configuration = new WorkflowStepConfigurationDto
                    {
                        ApprovalConfig = new WorkflowApprovalConfigDto
                        {
                            ApprovalType = WorkflowApprovalType.Single,
                            ApproverRules = new List<WorkflowAssignmentRuleDto>
                            {
                                new()
                                {
                                    AssignmentType = WorkflowAssignmentType.Role,
                                    Role = "MaintenanceSupervisor"
                                }
                            }
                        },
                        SkipCondition = new WorkflowConditionDto
                        {
                            ConditionType = WorkflowConditionType.Expression,
                            Expression = "estimatedCost < 1000"
                        }
                    }
                },
                new()
                {
                    Name = "Corrective Work",
                    Description = "Execute corrective maintenance work",
                    StepType = WorkflowStepType.Manual,
                    Order = 3
                }
            }
        };
    }

    private static async Task<CreateWorkflowDefinitionDto> CreateSafetyInspectionWorkflowTemplateAsync()
    {
        return new CreateWorkflowDefinitionDto
        {
            Name = "Safety Inspection Workflow",
            Description = "Workflow for safety-critical maintenance requiring inspection",
            EntityType = "WorkOrder",
            Steps = new List<CreateWorkflowStepDto>
            {
                new()
                {
                    Name = "Safety Pre-Check",
                    Description = "Pre-work safety assessment and lockout/tagout",
                    StepType = WorkflowStepType.Manual,
                    Order = 1,
                    IsRequired = true
                },
                new()
                {
                    Name = "Safety Work Execution",
                    Description = "Execute safety-critical maintenance work",
                    StepType = WorkflowStepType.Manual,
                    Order = 2
                },
                new()
                {
                    Name = "Safety Inspection",
                    Description = "Mandatory safety inspection by certified inspector",
                    StepType = WorkflowStepType.Approval,
                    Order = 3,
                    IsRequired = true,
                    Configuration = new WorkflowStepConfigurationDto
                    {
                        ApprovalConfig = new WorkflowApprovalConfigDto
                        {
                            ApprovalType = WorkflowApprovalType.Single,
                            ApproverRules = new List<WorkflowAssignmentRuleDto>
                            {
                                new()
                                {
                                    AssignmentType = WorkflowAssignmentType.Role,
                                    Role = "SafetyInspector"
                                }
                            }
                        }
                    }
                },
                new()
                {
                    Name = "Safety Certification",
                    Description = "Issue safety certification for completed work",
                    StepType = WorkflowStepType.Automatic,
                    Order = 4
                }
            }
        };
    }

    #endregion

    #region Private Support Methods

    private static async Task<WorkOrder> CreateBasicWorkOrderAsync(CreateWorkOrderDto createDto)
    {
        // This would typically call the existing WorkOrderService or repository
        // For now, return a mock work order - in real implementation, integrate with existing service
        return new WorkOrder
        {
            Id = Guid.NewGuid(),
            WorkOrderNumber = $"WO-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(1000, 9999)}",
            Title = createDto.Title,
            Description = createDto.Description,
            AssetId = createDto.AssetId,
            Status = "Created",
            CreatedAt = DateTime.UtcNow
        };
    }

    private async Task ApplyInitialWorkflowDecisionsAsync(
        WorkOrder workOrder, WorkflowExecutionResult workflowResult, Dictionary<string, object> workflowData)
    {
        // Apply any immediate workflow decisions or updates to the work order
        if (workflowResult.ResultData is Dictionary<string, object> resultData)
        {
            if (resultData.ContainsKey("autoAssignTechnician"))
            {
                // Auto-assign technician logic would go here
                _logger.LogInformation("Auto-assigning technician for work order {WorkOrderId}", workOrder.Id);
            }

            if (resultData.ContainsKey("priorityEscalation"))
            {
                // Priority escalation logic would go here
                _logger.LogInformation("Priority escalated for work order {WorkOrderId}", workOrder.Id);
            }
        }
    }

    private async Task ApplyPostStepProcessingAsync(
        WorkOrder workOrder, WorkflowExecutionResult workflowResult, Dictionary<string, object> executionData)
    {
        // Apply post-step processing based on workflow results
        if (workflowResult.Status == WorkflowInstanceStatus.Completed)
        {
            // Handle workflow completion
            await HandleWorkflowCompletionAsync(workOrder);
        }
        else if (executionData.TryGetValue("workStatus", out object? value))
        {
            // Update work order status based on workflow step
            var newStatus = value.ToString();
            if (!string.IsNullOrEmpty(newStatus) && newStatus != workOrder.Status)
            {
                workOrder.Status = newStatus;
                await _workOrderRepository.UpdateAsync(workOrder);
                _logger.LogInformation("Work order {WorkOrderId} status updated to {Status}", workOrder.Id, newStatus);
            }
        }
    }

    private async Task HandleWorkflowCompletionAsync(WorkOrder workOrder)
    {
        workOrder.Status = "Completed";
        workOrder.ActualCompletionDate = DateTime.UtcNow;
        await _workOrderRepository.UpdateAsync(workOrder);

        _logger.LogInformation("Work order {WorkOrderId} workflow completed", workOrder.Id);
    }

    private static async Task<Dictionary<string, object>> EnhanceStepExecutionDataAsync(
        WorkOrder workOrder, Dictionary<string, object> stepData, string stepAction)
    {
        stepData["workOrderId"] = workOrder.Id;
        stepData["currentStatus"] = workOrder.Status;
        stepData["stepAction"] = stepAction;
        stepData["processedAt"] = DateTime.UtcNow;

        return stepData;
    }

    private static async Task<bool> RequiresQualityControlAsync(WorkOrder workOrder)
    {
        // Determine if quality control is required based on work order characteristics
        return workOrder.MaintenanceType?.Name?.ToLower().Contains("safety", StringComparison.CurrentCultureIgnoreCase) == true ||
               workOrder.PriorityLevel?.Level <= 2;
    }

    private static async Task<bool> RequiresManagerApprovalAsync(WorkOrder workOrder)
    {
        // Determine if manager approval is required
        return workOrder.EstimatedCost > 5000 || workOrder.EstimatedHours > 16;
    }

    private async Task<bool> RequiresSafetyInspectionAsync(WorkOrder workOrder)
    {
        // Determine if safety inspection is required
        var asset = await _assetRepository.GetByIdAsync(workOrder.AssetId);
        return asset?.Criticality == AssetCriticality.Critical ||
               workOrder.MaintenanceType?.Name?.ToLower().Contains("safety", StringComparison.CurrentCultureIgnoreCase) == true;
    }

    private static async Task<TimeSpan?> EstimateCompletionTimeAsync(WorkOrder workOrder, WorkflowStatusDto workflowStatus)
    {
        // Calculate estimated completion time based on workflow progress and work order characteristics
        if (workflowStatus.Status == WorkflowInstanceStatus.Completed)
        {
            return TimeSpan.Zero;
        }

        var remainingHours = workOrder.EstimatedHours == 0 ? 4.0 : workOrder.EstimatedHours;
        var progressPercent = workflowStatus.Progress?.PercentComplete ?? 0;
        var remainingPercent = (100 - progressPercent) / 100.0;

        return TimeSpan.FromHours(remainingHours * remainingPercent);
    }

    #endregion

    #region Temporary Workflow Engine Adapters (TODO: Replace with proper implementation)

    private async Task<WorkflowExecutionResult> StartWorkflowWithExistingEngineAsync(
        string workflowKey, Guid entityId, Guid userId, Dictionary<string, object> workflowData)
    {
        try
        {
            var workflowInstance = await _workflowEngine.StartWorkflowAsync(
                workflowKey, entityId, userId, workflowData);

            return new WorkflowExecutionResult
            {
                Success = true,
                Status = workflowInstance.Status,
                WorkflowInstanceId = workflowInstance.Id,
                CurrentStepId = workflowInstance.CurrentStepId,
                Message = "Workflow started successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start workflow {WorkflowKey} for entity {EntityId}", workflowKey, entityId);
            return new WorkflowExecutionResult
            {
                Success = false,
                Status = WorkflowInstanceStatus.Failed,
                Message = ex.Message,
                Errors = new List<WorkflowExecutionError>
                {
                    new() { Code = "WORKFLOW_START_FAILED", Message = ex.Message }
                }
            };
        }
    }

    private async Task<WorkflowInstance?> GetActiveWorkflowInstanceAsync(string entityId, string entityType)
    {
        if (!Guid.TryParse(entityId, out var parsedEntityId))
        {
            _logger.LogWarning("Invalid entity ID {EntityId} when resolving workflow instance", entityId);
            return null;
        }

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var entityTypeId = await ResolveEntityTypeIdAsync(entityType, tenantId);
        if (entityTypeId == Guid.Empty)
        {
            return null;
        }

        var instances = await _workflowInstanceRepository.GetByEntityAsync(entityTypeId, entityId);
        return instances
            .Where(i => i.Status == WorkflowInstanceStatus.InProgress ||
                        i.Status == WorkflowInstanceStatus.Waiting ||
                        i.Status == WorkflowInstanceStatus.Created)
            .OrderByDescending(i => i.StartedDate ?? i.CreatedDate)
            .FirstOrDefault();
    }

    private async Task<WorkflowStatusDto?> GetWorkflowStatusAsync(string entityId, string entityType)
    {
        var instance = await GetActiveWorkflowInstanceAsync(entityId, entityType) ??
                       await GetLatestWorkflowInstanceAsync(entityId, entityType);

        if (instance == null)
        {
            return null;
        }

        return await _workflowEngine.GetWorkflowStatusAsync(instance.Id);
    }

    private async Task<WorkflowExecutionResult> ExecuteWorkflowStepAsync(
        Guid workflowInstanceId, string stepAction, Dictionary<string, object> executionData, string userId)
    {
        if (!Guid.TryParse(userId, out var parsedUserId) || parsedUserId == Guid.Empty)
        {
            return new WorkflowExecutionResult
            {
                Success = false,
                Status = WorkflowInstanceStatus.Failed,
                Message = "Invalid user context for workflow step execution"
            };
        }

        var currentStep = await _workflowStepInstanceRepository.GetCurrentStepAsync(workflowInstanceId);
        if (currentStep == null)
        {
            return new WorkflowExecutionResult
            {
                Success = false,
                Status = WorkflowInstanceStatus.Completed,
                Message = "No active workflow step found"
            };
        }

        var action = MapStepAction(stepAction);
        var comments = executionData.TryGetValue("comments", out var commentValue)
            ? commentValue?.ToString()
            : null;

        return await _workflowEngine.ProcessStepAsync(
            currentStep.Id,
            parsedUserId,
            action,
            executionData,
            comments);
    }

    private async Task<WorkflowDefinition> CreateOrUpdateWorkflowDefinitionAsync(
        CreateWorkflowDefinitionDto workflowDefinition, string tenantId)
    {
        var tenantGuid = ResolveTenantId(tenantId);
        if (tenantGuid == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant ID is required to create or update workflow definitions");
        }

        var entityType = await ResolveOrCreateEntityTypeAsync(workflowDefinition.EntityType, tenantGuid);

        var createdById = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var existing = await _workflowDefinitionRepository.GetByNameAsync(workflowDefinition.Name, tenantGuid);
        var definitionKey = existing?.DefinitionKey is { } key && key != Guid.Empty ? key : Guid.NewGuid();
        var versions = existing == null
            ? Array.Empty<WorkflowDefinition>()
            : (await _workflowDefinitionRepository.GetVersionsAsync(definitionKey, tenantGuid)).ToArray();
        var draft = versions.FirstOrDefault(definition =>
            definition.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Draft);

        WorkflowDefinition nextDefinition;
        if (draft != null)
        {
            nextDefinition = await _workflowDefinitionRepository.GetWithDetailsAsync(draft.Id) ?? draft;
            if (nextDefinition.Instances.Any())
            {
                throw new InvalidOperationException("A draft workflow version with instances cannot be replaced.");
            }

            await _workflowTransitionRepository.DeleteRangeAsync(t => t.WorkflowDefinitionId == nextDefinition.Id);
            await _workflowTransitionRepository.SaveChangesAsync();
            await _workflowStepRepository.DeleteRangeAsync(s => s.WorkflowDefinitionId == nextDefinition.Id);
            await _workflowStepRepository.SaveChangesAsync();

            nextDefinition.Description = workflowDefinition.Description;
            nextDefinition.Configuration = workflowDefinition.Configuration;
            nextDefinition.EntityTypeId = entityType.Id;
            nextDefinition.UpdatedAt = DateTime.UtcNow;
            nextDefinition.UpdatedBy = _currentUserService.UserName ?? "System";
            nextDefinition.LastModifiedById = createdById;
            await _workflowDefinitionRepository.UpdateAsync(nextDefinition);
            await _workflowDefinitionRepository.SaveChangesAsync();
        }
        else
        {
            var nextVersion = versions.Select(definition => definition.Version).DefaultIfEmpty(0).Max() + 1;
            nextDefinition = new WorkflowDefinition
            {
                Id = Guid.NewGuid(),
                DefinitionKey = definitionKey,
                Name = workflowDefinition.Name,
                Description = workflowDefinition.Description,
                EntityTypeId = entityType.Id,
                Configuration = workflowDefinition.Configuration,
                TenantId = tenantGuid,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUserService.UserName ?? "System",
                CreatedById = createdById,
                IsActive = false,
                LifecycleStatus = WorkflowDefinitionLifecycleStatus.Draft,
                Version = nextVersion,
                SupersedesDefinitionId = existing?.Id,
                ChangeSummary = existing == null ? "Initial maintenance workflow" : $"Maintenance workflow update after version {existing.Version}"
            };

            await _workflowDefinitionRepository.AddAsync(nextDefinition);
            await _workflowDefinitionRepository.SaveChangesAsync();
        }

        await CreateStepsAndTransitionsAsync(nextDefinition, workflowDefinition, tenantGuid);

        var now = DateTime.UtcNow;
        foreach (var published in versions.Where(definition =>
                     definition.Id != nextDefinition.Id &&
                     definition.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published))
        {
            published.LifecycleStatus = WorkflowDefinitionLifecycleStatus.Retired;
            published.IsActive = false;
            published.RetiredAt = now;
            published.RetiredById = createdById;
            published.UpdatedAt = now;
            published.UpdatedBy = _currentUserService.UserName ?? "System";
            published.LastModifiedById = createdById;
            await _workflowDefinitionRepository.UpdateAsync(published);
        }

        nextDefinition.LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published;
        nextDefinition.IsActive = true;
        nextDefinition.PublishedAt = now;
        nextDefinition.PublishedById = createdById;
        nextDefinition.RetiredAt = null;
        nextDefinition.RetiredById = null;
        nextDefinition.UpdatedAt = now;
        nextDefinition.UpdatedBy = _currentUserService.UserName ?? "System";
        nextDefinition.LastModifiedById = createdById;
        await _workflowDefinitionRepository.UpdateAsync(nextDefinition);
        await _workflowDefinitionRepository.SaveChangesAsync();

        return await _workflowDefinitionRepository.GetWithDetailsAsync(nextDefinition.Id) ?? nextDefinition;
    }

    private async Task<WorkflowInstance?> GetLatestWorkflowInstanceAsync(string entityId, string entityType)
    {
        if (!Guid.TryParse(entityId, out _))
        {
            return null;
        }

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var entityTypeId = await ResolveEntityTypeIdAsync(entityType, tenantId);
        if (entityTypeId == Guid.Empty)
        {
            return null;
        }

        var instances = await _workflowInstanceRepository.GetByEntityAsync(entityTypeId, entityId);
        return instances
            .OrderByDescending(i => i.StartedDate ?? i.CreatedDate)
            .FirstOrDefault();
    }

    private async Task CreateStepsAndTransitionsAsync(
        WorkflowDefinition definition,
        CreateWorkflowDefinitionDto template,
        Guid tenantId)
    {
        var orderedSteps = template.Steps.OrderBy(s => s.Order).ToList();
        if (!orderedSteps.Any())
        {
            return;
        }

        var stepEntities = new List<WorkflowStep>();
        for (var i = 0; i < orderedSteps.Count; i++)
        {
            var stepDto = orderedSteps[i];
            var stepEntity = new WorkflowStep
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = definition.Id,
                Name = stepDto.Name,
                Description = stepDto.Description,
                StepType = stepDto.StepType,
                Order = stepDto.Order,
                IsRequired = stepDto.IsRequired,
                RequiredRole = stepDto.RequiredRole,
                EstimatedHours = stepDto.EstimatedHours,
                IsStartStep = i == 0,
                IsEndStep = i == orderedSteps.Count - 1,
                Configuration = stepDto.Configuration != null ? JsonSerializer.Serialize(stepDto.Configuration) : null,
                TenantId = tenantId
            };

            stepEntities.Add(stepEntity);
        }

        await _workflowStepRepository.AddRangeAsync(stepEntities);
        await _workflowStepRepository.SaveChangesAsync();

        var transitions = new List<WorkflowTransition>();
        if (template.Transitions.Any())
        {
            var stepIdLookup = stepEntities.ToDictionary(s => s.Id, s => s);
            foreach (var transitionDto in template.Transitions)
            {
                if (!stepIdLookup.ContainsKey(transitionDto.FromStepId) ||
                    !stepIdLookup.ContainsKey(transitionDto.ToStepId))
                {
                    continue;
                }

                transitions.Add(new WorkflowTransition
                {
                    Id = Guid.NewGuid(),
                    WorkflowDefinitionId = definition.Id,
                    FromStepId = transitionDto.FromStepId,
                    ToStepId = transitionDto.ToStepId,
                    Name = transitionDto.Name,
                    Description = transitionDto.Description,
                    Condition = transitionDto.Condition != null ? JsonSerializer.Serialize(transitionDto.Condition) : null,
                    IsDefault = transitionDto.IsDefault,
                    Priority = transitionDto.Priority,
                    TenantId = tenantId
                });
            }
        }

        if (!transitions.Any())
        {
            for (var i = 0; i < stepEntities.Count - 1; i++)
            {
                var from = stepEntities[i];
                var to = stepEntities[i + 1];
                transitions.Add(new WorkflowTransition
                {
                    Id = Guid.NewGuid(),
                    WorkflowDefinitionId = definition.Id,
                    FromStepId = from.Id,
                    ToStepId = to.Id,
                    Name = $"{from.Name} to {to.Name}",
                    Description = "Auto-generated transition",
                    IsDefault = true,
                    Priority = 0,
                    TenantId = tenantId
                });
            }
        }

        if (transitions.Any())
        {
            await _workflowTransitionRepository.AddRangeAsync(transitions);
            await _workflowTransitionRepository.SaveChangesAsync();
        }
    }

    private async Task<WorkflowEntityType> ResolveOrCreateEntityTypeAsync(string entityType, Guid tenantId)
    {
        var existing = await _workflowEntityTypeRepository.GetByNameAsync(entityType, tenantId);
        if (existing != null)
        {
            return existing;
        }

        var code = GenerateEntityTypeCode(entityType);
        var entity = new WorkflowEntityType
        {
            Id = Guid.NewGuid(),
            Name = entityType,
            Code = code,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "System",
            IsActive = true
        };

        await _workflowEntityTypeRepository.AddAsync(entity);
        await _workflowEntityTypeRepository.SaveChangesAsync();
        return entity;
    }

    private async Task<Guid> ResolveEntityTypeIdAsync(string entityType, Guid tenantId)
    {
        var entity = await _workflowEntityTypeRepository.GetByNameAsync(entityType, tenantId);
        return entity?.Id ?? Guid.Empty;
    }

    private Guid ResolveTenantId(string tenantId)
    {
        if (Guid.TryParse(tenantId, out var parsed))
        {
            return parsed;
        }

        return _currentUserService.TenantId ?? Guid.Empty;
    }

    private static string GenerateEntityTypeCode(string entityType)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            return "ENTITY";
        }

        var codeChars = new List<char>();
        for (var i = 0; i < entityType.Length; i++)
        {
            var ch = entityType[i];
            if (char.IsWhiteSpace(ch) || ch == '-' || ch == '_')
            {
                if (codeChars.LastOrDefault() != '_')
                {
                    codeChars.Add('_');
                }
                continue;
            }

            if (char.IsUpper(ch) && i > 0 && char.IsLower(entityType[i - 1]))
            {
                codeChars.Add('_');
            }

            codeChars.Add(char.ToUpperInvariant(ch));
        }

        var code = new string(codeChars.ToArray()).Trim('_');
        return string.IsNullOrWhiteSpace(code) ? "ENTITY" : code;
    }

    private static WorkflowStepAction MapStepAction(string stepAction)
    {
        if (string.IsNullOrWhiteSpace(stepAction))
        {
            return WorkflowStepAction.Complete;
        }

        return stepAction.Trim().ToLowerInvariant() switch
        {
            "approve" => WorkflowStepAction.Complete,
            "reject" => WorkflowStepAction.Reject,
            "requestchanges" => WorkflowStepAction.RequestInformation,
            "requestinformation" => WorkflowStepAction.RequestInformation,
            "requestinfo" => WorkflowStepAction.RequestInformation,
            "skip" => WorkflowStepAction.Skip,
            "delegate" => WorkflowStepAction.Delegate,
            "escalate" => WorkflowStepAction.Delegate,
            "start" => WorkflowStepAction.Complete,
            "complete" => WorkflowStepAction.Complete,
            _ => WorkflowStepAction.Complete
        };
    }

    #endregion
}
