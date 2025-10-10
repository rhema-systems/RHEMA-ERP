using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Workflow;

/// <summary>
/// DTO for creating a new workflow definition with conditional logic support
/// </summary>
public class CreateWorkflowDefinitionDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string? Configuration { get; set; }
    public Guid CreatedById { get; set; }
    public List<CreateWorkflowStepDto> Steps { get; set; } = new();
    public List<CreateWorkflowTransitionDto> Transitions { get; set; } = new();
}

/// <summary>
/// DTO for updating workflow definitions
/// </summary>
public class UpdateWorkflowDefinitionDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Configuration { get; set; }
    public Guid LastModifiedById { get; set; }
}

/// <summary>
/// DTO for creating workflow steps with conditional logic
/// </summary>
public class CreateWorkflowStepDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkflowStepType StepType { get; set; }
    public int Order { get; set; }
    public bool IsRequired { get; set; } = true;
    public string? RequiredRole { get; set; }
    public double? EstimatedHours { get; set; }
    public WorkflowStepConfigurationDto? Configuration { get; set; }
}

/// <summary>
/// DTO for creating workflow transitions with conditional logic
/// </summary>
public class CreateWorkflowTransitionDto
{
    public Guid FromStepId { get; set; }
    public Guid ToStepId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkflowConditionDto? Condition { get; set; }
    public bool IsDefault { get; set; } = false;
    public int Priority { get; set; } = 0;
}

/// <summary>
/// Configuration for workflow steps with conditional logic
/// </summary>
public class WorkflowStepConfigurationDto
{
    /// <summary>
    /// Auto-assignment rules based on conditions
    /// </summary>
    public List<WorkflowAssignmentRuleDto>? AssignmentRules { get; set; }
    
    /// <summary>
    /// Approval configuration for approval steps
    /// </summary>
    public WorkflowApprovalConfigDto? ApprovalConfig { get; set; }
    
    /// <summary>
    /// Quality control configuration for quality check steps
    /// </summary>
    public WorkflowQualityConfigDto? QualityConfig { get; set; }
    
    /// <summary>
    /// Notification configuration
    /// </summary>
    public WorkflowNotificationConfigDto? NotificationConfig { get; set; }
    
    /// <summary>
    /// Escalation rules
    /// </summary>
    public List<WorkflowEscalationRuleDto>? EscalationRules { get; set; }
    
    /// <summary>
    /// Custom form fields for data collection
    /// </summary>
    public List<WorkflowFormFieldDto>? FormFields { get; set; }
    
    /// <summary>
    /// Skip conditions - when this step should be skipped
    /// </summary>
    public WorkflowConditionDto? SkipCondition { get; set; }
}

/// <summary>
/// Conditional logic for workflow decisions
/// </summary>
public class WorkflowConditionDto
{
    /// <summary>
    /// Type of condition (Expression, Script, Rule)
    /// </summary>
    public WorkflowConditionType ConditionType { get; set; }
    
    /// <summary>
    /// Condition expression or script
    /// </summary>
    public string Expression { get; set; } = string.Empty;
    
    /// <summary>
    /// Variables and their expected values
    /// </summary>
    public Dictionary<string, object>? Variables { get; set; }
    
    /// <summary>
    /// Logical operator for combining multiple conditions
    /// </summary>
    public WorkflowLogicalOperator LogicalOperator { get; set; } = WorkflowLogicalOperator.And;
    
    /// <summary>
    /// Child conditions for complex logical expressions
    /// </summary>
    public List<WorkflowConditionDto>? ChildConditions { get; set; }
}

/// <summary>
/// Assignment rules with conditional logic
/// </summary>
public class WorkflowAssignmentRuleDto
{
    /// <summary>
    /// Condition that must be met for this assignment rule
    /// </summary>
    public WorkflowConditionDto? Condition { get; set; }
    
    /// <summary>
    /// Assignment type (User, Role, Dynamic)
    /// </summary>
    public WorkflowAssignmentType AssignmentType { get; set; }
    
    /// <summary>
    /// Target user ID (for User assignment type)
    /// </summary>
    public Guid? UserId { get; set; }
    
    /// <summary>
    /// Target role (for Role assignment type)
    /// </summary>
    public string? Role { get; set; }
    
    /// <summary>
    /// Dynamic assignment expression (for Dynamic assignment type)
    /// </summary>
    public string? DynamicExpression { get; set; }
    
    /// <summary>
    /// Priority of this assignment rule (higher number = higher priority)
    /// </summary>
    public int Priority { get; set; } = 0;
}

/// <summary>
/// Approval step configuration with conditional logic
/// </summary>
public class WorkflowApprovalConfigDto
{
    /// <summary>
    /// Type of approval (Single, Multiple, Consensus, Majority)
    /// </summary>
    public WorkflowApprovalType ApprovalType { get; set; }
    
    /// <summary>
    /// Required approvers based on conditions
    /// </summary>
    public List<WorkflowAssignmentRuleDto> ApproverRules { get; set; } = new();
    
    /// <summary>
    /// Minimum number of approvals required
    /// </summary>
    public int MinApprovalsRequired { get; set; } = 1;
    
    /// <summary>
    /// Auto-approval conditions
    /// </summary>
    public WorkflowConditionDto? AutoApprovalCondition { get; set; }
    
    /// <summary>
    /// Rejection handling
    /// </summary>
    public WorkflowRejectionHandling RejectionHandling { get; set; } = WorkflowRejectionHandling.StopWorkflow;
}

/// <summary>
/// Quality control step configuration
/// </summary>
public class WorkflowQualityConfigDto
{
    /// <summary>
    /// Quality checks to perform
    /// </summary>
    public List<WorkflowQualityCheckDto> QualityChecks { get; set; } = new();
    
    /// <summary>
    /// Required inspection officer assignment rules
    /// </summary>
    public List<WorkflowAssignmentRuleDto>? InspectionOfficerRules { get; set; }
    
    /// <summary>
    /// Auto-pass conditions
    /// </summary>
    public WorkflowConditionDto? AutoPassCondition { get; set; }
}

/// <summary>
/// Individual quality check with conditional logic
/// </summary>
public class WorkflowQualityCheckDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;
    public WorkflowConditionDto? ApplicabilityCondition { get; set; }
    public object? ExpectedValue { get; set; }
    public string? ValidationExpression { get; set; }
}

/// <summary>
/// Notification configuration with conditional triggers
/// </summary>
public class WorkflowNotificationConfigDto
{
    /// <summary>
    /// Notification triggers with conditions
    /// </summary>
    public List<WorkflowNotificationTriggerDto> Triggers { get; set; } = new();
}

/// <summary>
/// Notification trigger with conditional logic
/// </summary>
public class WorkflowNotificationTriggerDto
{
    public WorkflowNotificationEvent Event { get; set; }
    public WorkflowConditionDto? Condition { get; set; }
    public List<WorkflowAssignmentRuleDto> Recipients { get; set; } = new();
    public string? MessageTemplate { get; set; }
    public WorkflowNotificationChannel Channel { get; set; }
}

/// <summary>
/// Escalation rules with conditional logic
/// </summary>
public class WorkflowEscalationRuleDto
{
    /// <summary>
    /// Condition that triggers escalation
    /// </summary>
    public WorkflowConditionDto TriggerCondition { get; set; } = null!;
    
    /// <summary>
    /// Delay before escalation (in hours)
    /// </summary>
    public double DelayHours { get; set; }
    
    /// <summary>
    /// Escalation targets
    /// </summary>
    public List<WorkflowAssignmentRuleDto> EscalationTargets { get; set; } = new();
    
    /// <summary>
    /// Action to take on escalation
    /// </summary>
    public WorkflowEscalationAction Action { get; set; }
}

/// <summary>
/// Form field for data collection in workflow steps
/// </summary>
public class WorkflowFormFieldDto
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public WorkflowFieldType FieldType { get; set; }
    public bool IsRequired { get; set; } = false;
    public object? DefaultValue { get; set; }
    public List<string>? Options { get; set; }
    public string? ValidationRule { get; set; }
    public WorkflowConditionDto? VisibilityCondition { get; set; }
}

/// <summary>
/// Result of workflow execution
/// </summary>
public class WorkflowExecutionResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public WorkflowInstanceStatus Status { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? CurrentStepId { get; set; }
    public List<WorkflowExecutionError>? Errors { get; set; }
    public object? ResultData { get; set; }
}

/// <summary>
/// Workflow execution error details
/// </summary>
public class WorkflowExecutionError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public Guid? StepId { get; set; }
}

/// <summary>
/// Workflow status and progress information
/// </summary>
public class WorkflowStatusDto
{
    public Guid WorkflowInstanceId { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public WorkflowInstanceStatus Status { get; set; }
    public DateTime StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public WorkflowProgressDto Progress { get; set; } = null!;
    public List<WorkflowStepStatusDto> Steps { get; set; } = new();
    public List<WorkflowApprovalStatusDto> PendingApprovals { get; set; } = new();
}

/// <summary>
/// Workflow progress information
/// </summary>
public class WorkflowProgressDto
{
    public int TotalSteps { get; set; }
    public int CompletedSteps { get; set; }
    public int PercentComplete { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }
}

/// <summary>
/// Individual step status
/// </summary>
public class WorkflowStepStatusDto
{
    public Guid StepInstanceId { get; set; }
    public string StepName { get; set; } = string.Empty;
    public WorkflowStepInstanceStatus Status { get; set; }
    public Guid? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsOverdue { get; set; }
}

/// <summary>
/// Approval status information
/// </summary>
public class WorkflowApprovalStatusDto
{
    public Guid ApprovalId { get; set; }
    public string StepName { get; set; } = string.Empty;
    public Guid ApproverId { get; set; }
    public string ApproverName { get; set; } = string.Empty;
    public WorkflowApprovalStatus Status { get; set; }
    public DateTime RequestedDate { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsOverdue { get; set; }
}

/// <summary>
/// Workflow validation result
/// </summary>
public class WorkflowValidationResult
{
    public bool IsValid { get; set; }
    public List<WorkflowValidationError> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Workflow validation error
/// </summary>
public class WorkflowValidationError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? StepId { get; set; }
    public Guid? TransitionId { get; set; }
}

/// <summary>
/// Information about available workflow variables for condition expressions
/// </summary>
public class WorkflowVariableInfo
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Type DataType { get; set; } = typeof(object);
    public string? Description { get; set; }
    public List<string>? PossibleValues { get; set; }
}

/// <summary>
/// DTO for workflow definition responses
/// </summary>
public class WorkflowDefinitionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string? Configuration { get; set; }
    public bool IsActive { get; set; }
    public int Version { get; set; }
    public Guid CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public Guid? LastModifiedById { get; set; }
    public string? LastModifiedByName { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public List<WorkflowStepDto> Steps { get; set; } = new();
    public List<WorkflowTransitionDto> Transitions { get; set; } = new();
}

/// <summary>
/// DTO for workflow step responses
/// </summary>
public class WorkflowStepDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkflowStepType StepType { get; set; }
    public int Order { get; set; }
    public bool IsRequired { get; set; }
    public string? RequiredRole { get; set; }
    public double? EstimatedHours { get; set; }
    public WorkflowStepConfigurationDto? Configuration { get; set; }
}

/// <summary>
/// DTO for workflow transition responses
/// </summary>
public class WorkflowTransitionDto
{
    public Guid Id { get; set; }
    public Guid FromStepId { get; set; }
    public Guid ToStepId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkflowConditionDto? Condition { get; set; }
    public bool IsDefault { get; set; }
    public int Priority { get; set; }
}

