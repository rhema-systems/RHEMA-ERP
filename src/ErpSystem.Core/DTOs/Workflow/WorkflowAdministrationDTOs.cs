using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Workflow;

#region Workflow Definition Management DTOs

/// <summary>
/// Workflow definition DTO for administration with additional administrative properties
/// </summary>
public class WorkflowDefinitionAdminDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public string? Configuration { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string? LastModifiedByName { get; set; }
    
    // Additional administrative properties
    public int StepCount { get; set; }
    public int ActiveInstancesCount { get; set; }
    public DateTime? LastUsedDate { get; set; }
}

/// <summary>
/// DTO for creating workflow definitions via administration interface
/// </summary>
public class CreateWorkflowDefinitionAdminDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    [Required]
    [StringLength(50)]
    public string EntityType { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    
    public string? Configuration { get; set; }
}

/// <summary>
/// DTO for updating workflow definitions via administration interface
/// </summary>
public class UpdateWorkflowDefinitionAdminDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    public bool IsActive { get; set; }
    
    public string? Configuration { get; set; }
}

#endregion

#region Workflow Step Management DTOs

/// <summary>
/// Workflow step DTO for administration with transition information
/// </summary>
public class WorkflowStepAdminDto
{
    public Guid Id { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string StepType { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsRequired { get; set; }
    public string? RequiredRole { get; set; }
    public double? EstimatedHours { get; set; }
    public string? Configuration { get; set; }
    public DateTime CreatedDate { get; set; }
    
    // Additional administrative properties
    public List<WorkflowTransitionAdminDto> OutgoingTransitions { get; set; } = new();
    public List<WorkflowTransitionAdminDto> IncomingTransitions { get; set; } = new();
}

/// <summary>
/// DTO for creating workflow steps via administration interface
/// </summary>
public class CreateWorkflowStepAdminDto
{
    [Required]
    public Guid WorkflowDefinitionId { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    [Required]
    [StringLength(50)]
    public string StepType { get; set; } = string.Empty;
    
    public int Order { get; set; }
    
    public bool IsRequired { get; set; } = true;
    
    [StringLength(100)]
    public string? RequiredRole { get; set; }
    
    public double? EstimatedHours { get; set; }
    
    public string? Configuration { get; set; }
}

/// <summary>
/// DTO for updating workflow steps
/// </summary>
public class UpdateWorkflowStepDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    [Required]
    [StringLength(50)]
    public string StepType { get; set; } = string.Empty;
    
    public int Order { get; set; }
    
    public bool IsRequired { get; set; }
    
    [StringLength(100)]
    public string? RequiredRole { get; set; }
    
    public double? EstimatedHours { get; set; }
    
    public string? Configuration { get; set; }
}

#endregion

#region Workflow Transition DTOs

/// <summary>
/// Workflow transition DTO for administration with step names
/// </summary>
public class WorkflowTransitionAdminDto
{
    public Guid Id { get; set; }
    public Guid FromStepId { get; set; }
    public Guid ToStepId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Condition { get; set; }
    public bool IsDefault { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedDate { get; set; }
    
    // Additional navigation properties for administration
    public string FromStepName { get; set; } = string.Empty;
    public string ToStepName { get; set; } = string.Empty;
}

/// <summary>
/// DTO for creating workflow transitions via administration interface
/// </summary>
public class CreateWorkflowTransitionAdminDto
{
    [Required]
    public Guid FromStepId { get; set; }
    
    [Required]
    public Guid ToStepId { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    public string? Condition { get; set; }
    
    public bool IsDefault { get; set; } = false;
    
    public int Priority { get; set; } = 0;
}

/// <summary>
/// DTO for updating workflow transitions
/// </summary>
public class UpdateWorkflowTransitionDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    public string? Condition { get; set; }
    
    public bool IsDefault { get; set; }
    
    public int Priority { get; set; }
}

#endregion

#region Workflow Administration DTOs

/// <summary>
/// Workflow summary for administration dashboard
/// </summary>
public class WorkflowSummaryDto
{
    public int TotalWorkflowDefinitions { get; set; }
    public int ActiveWorkflowDefinitions { get; set; }
    public int TotalWorkflowInstances { get; set; }
    public int ActiveWorkflowInstances { get; set; }
    public int CompletedWorkflowInstances { get; set; }
    public int FailedWorkflowInstances { get; set; }
    public Dictionary<string, int> InstancesByEntityType { get; set; } = new();
    public Dictionary<string, int> InstancesByStatus { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Workflow performance metrics
/// </summary>
public class WorkflowPerformanceDto
{
    public Guid WorkflowDefinitionId { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public int TotalInstances { get; set; }
    public int CompletedInstances { get; set; }
    public int FailedInstances { get; set; }
    public double AverageCompletionTimeHours { get; set; }
    public double SuccessRate { get; set; }
    public DateTime? FastestCompletionTime { get; set; }
    public DateTime? SlowestCompletionTime { get; set; }
    public List<WorkflowStepPerformanceDto> StepPerformance { get; set; } = new();
}

/// <summary>
/// Workflow step performance metrics
/// </summary>
public class WorkflowStepPerformanceDto
{
    public Guid StepId { get; set; }
    public string StepName { get; set; } = string.Empty;
    public double AverageCompletionTimeHours { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public double SuccessRate { get; set; }
}

/// <summary>
/// Filter DTO for workflow definitions
/// </summary>
public class WorkflowDefinitionFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? SearchTerm { get; set; }
    public string? EntityType { get; set; }
    public bool? IsActive { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public string SortBy { get; set; } = "Name";
    public bool SortDescending { get; set; } = false;
}

/// <summary>
/// Filter DTO for workflow instances
/// </summary>
public class WorkflowInstanceFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public Guid? WorkflowDefinitionId { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? Status { get; set; }
    public Guid? InitiatedById { get; set; }
    public DateTime? StartedAfter { get; set; }
    public DateTime? StartedBefore { get; set; }
    public DateTime? CompletedAfter { get; set; }
    public DateTime? CompletedBefore { get; set; }
    public string SortBy { get; set; } = "StartedDate";
    public bool SortDescending { get; set; } = true;
}

#endregion

#region Maintenance-Specific Workflow DTOs

/// <summary>
/// Maintenance workflow configuration DTO
/// </summary>
public class MaintenanceWorkflowConfigDto
{
    public string WorkOrderApprovalWorkflow { get; set; } = string.Empty;
    public string InspectionApprovalWorkflow { get; set; } = string.Empty;
    public string ContractorApprovalWorkflow { get; set; } = string.Empty;
    public string ExpenseApprovalWorkflow { get; set; } = string.Empty;
    
    public bool AutoStartWorkOrderApproval { get; set; } = true;
    public bool RequireInspectionApproval { get; set; } = true;
    public decimal ContractorApprovalThreshold { get; set; } = 1000;
    public decimal ExpenseApprovalThreshold { get; set; } = 500;
    
    public string DefaultApprovalRole { get; set; } = "MaintenanceManager";
    public string EscalationRole { get; set; } = "MaintenanceDirector";
    public int ApprovalTimeoutHours { get; set; } = 48;
}

/// <summary>
/// DTO for configuring maintenance workflows
/// </summary>
public class UpdateMaintenanceWorkflowConfigDto
{
    public string? WorkOrderApprovalWorkflow { get; set; }
    public string? InspectionApprovalWorkflow { get; set; }
    public string? ContractorApprovalWorkflow { get; set; }
    public string? ExpenseApprovalWorkflow { get; set; }
    
    public bool AutoStartWorkOrderApproval { get; set; }
    public bool RequireInspectionApproval { get; set; }
    public decimal ContractorApprovalThreshold { get; set; }
    public decimal ExpenseApprovalThreshold { get; set; }
    
    public string DefaultApprovalRole { get; set; } = string.Empty;
    public string EscalationRole { get; set; } = string.Empty;
    public int ApprovalTimeoutHours { get; set; }
}

#endregion