using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Maintenance;

/// <summary>
/// Result of enhanced work order creation with workflow integration
/// </summary>
public class EnhancedWorkOrderCreationResult
{
    public bool Success { get; set; } = true;
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    
    // Workflow-related properties
    public Guid? WorkflowInstanceId { get; set; }
    public string SelectedWorkflowKey { get; set; } = string.Empty;
    public WorkflowInstanceStatus InitialWorkflowStatus { get; set; }
    public Guid? CurrentStepId { get; set; }
    public List<WorkflowExecutionError> WorkflowErrors { get; set; } = new();
    
    public List<string> SuccessMessages { get; set; } = new();
    public List<string> WarningMessages { get; set; } = new();
}

/// <summary>
/// Result of enhanced workflow step execution
/// </summary>
public class EnhancedWorkflowStepResult
{
    public Guid WorkOrderId { get; set; }
    public string StepAction { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public string ProcessedBy { get; set; } = string.Empty;
    public bool Success { get; set; } = true;
    
    // Workflow execution details
    public WorkflowExecutionResult? WorkflowExecutionResult { get; set; }
    public WorkflowInstanceStatus NewWorkflowStatus { get; set; }
    public Guid? NewCurrentStepId { get; set; }
    public List<WorkflowExecutionError> WorkflowErrors { get; set; } = new();
    
    public List<string> SuccessMessages { get; set; } = new();
    public List<string> ErrorMessages { get; set; } = new();
}

/// <summary>
/// Enhanced maintenance workflow status with maintenance-specific context
/// </summary>
public class EnhancedMaintenanceWorkflowStatusDto
{
    // Work Order Information
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string WorkOrderStatus { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string MaintenanceType { get; set; } = string.Empty;
    
    // Workflow Information
    public Guid? WorkflowInstanceId { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public WorkflowInstanceStatus WorkflowStatus { get; set; }
    public DateTime StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public WorkflowProgressDto? Progress { get; set; }
    public List<WorkflowStepStatusDto> Steps { get; set; } = new();
    public List<WorkflowApprovalStatusDto> PendingApprovals { get; set; } = new();
    
    // Maintenance-Specific Context
    public bool RequiresQualityControl { get; set; }
    public bool RequiresManagerApproval { get; set; }
    public bool RequiresSafetyInspection { get; set; }
    public TimeSpan? EstimatedCompletionTime { get; set; }
}

/// <summary>
/// Result of workflow template configuration
/// </summary>
public class WorkflowTemplateConfigurationResult
{
    public string TenantId { get; set; } = string.Empty;
    public DateTime ConfiguredAt { get; set; }
    public bool Success { get; set; } = true;
    
    public List<string> ConfiguredWorkflows { get; set; } = new();
    public List<string> SuccessMessages { get; set; } = new();
    public List<string> ErrorMessages { get; set; } = new();
}