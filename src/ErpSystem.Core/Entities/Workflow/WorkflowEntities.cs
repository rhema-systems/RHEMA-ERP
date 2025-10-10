using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Workflow;

/// <summary>
/// Defines a configurable business process workflow
/// </summary>
public class WorkflowDefinition
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// The entity type this workflow applies to (e.g., "WorkOrder", "PurchaseRequest")
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Version number for workflow definition changes
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Whether this workflow definition is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// JSON configuration for workflow-specific settings
    /// </summary>
    public string? Configuration { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedDate { get; set; }
    public Guid CreatedById { get; set; }
    public Guid? LastModifiedById { get; set; }
    public Guid TenantId { get; set; }

    // Navigation properties
    public virtual ICollection<WorkflowStep> Steps { get; set; } = new List<WorkflowStep>();
    public virtual ICollection<WorkflowInstance> Instances { get; set; } = new List<WorkflowInstance>();
}

/// <summary>
/// Defines a step in a workflow process
/// </summary>
public class WorkflowStep
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid WorkflowDefinitionId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// The type of step (e.g., "Approval", "Task", "Decision", "System")
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string StepType { get; set; } = string.Empty;

    /// <summary>
    /// Order of execution within the workflow
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Whether this step is mandatory to complete
    /// </summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// Role or permission required to execute this step
    /// </summary>
    [MaxLength(100)]
    public string? RequiredRole { get; set; }

    /// <summary>
    /// Expected completion time in hours
    /// </summary>
    public double? EstimatedHours { get; set; }

    /// <summary>
    /// JSON configuration for step-specific settings
    /// </summary>
    public string? Configuration { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public Guid TenantId { get; set; }

    // Navigation properties
    [ForeignKey("WorkflowDefinitionId")]
    public virtual WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    
    public virtual ICollection<WorkflowTransition> OutgoingTransitions { get; set; } = new List<WorkflowTransition>();
    public virtual ICollection<WorkflowTransition> IncomingTransitions { get; set; } = new List<WorkflowTransition>();
    public virtual ICollection<WorkflowStepInstance> StepInstances { get; set; } = new List<WorkflowStepInstance>();
}

/// <summary>
/// Defines transitions between workflow steps
/// </summary>
public class WorkflowTransition
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid FromStepId { get; set; }

    [Required]
    public Guid ToStepId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Condition that must be met for this transition (JSON expression)
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>
    /// Whether this is the default transition if no other conditions are met
    /// </summary>
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// Order of evaluation if multiple transitions are possible
    /// </summary>
    public int Priority { get; set; } = 0;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public Guid TenantId { get; set; }

    // Navigation properties
    [ForeignKey("FromStepId")]
    public virtual WorkflowStep FromStep { get; set; } = null!;

    [ForeignKey("ToStepId")]
    public virtual WorkflowStep ToStep { get; set; } = null!;
}

/// <summary>
/// Represents a running instance of a workflow for a specific entity
/// </summary>
public class WorkflowInstance
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid WorkflowDefinitionId { get; set; }

    /// <summary>
    /// The ID of the entity this workflow instance is processing
    /// </summary>
    [Required]
    public Guid EntityId { get; set; }

    /// <summary>
    /// Current status of the workflow instance
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Created"; // Created, InProgress, Completed, Cancelled, Failed

    /// <summary>
    /// Current step in the workflow
    /// </summary>
    public Guid? CurrentStepId { get; set; }

    /// <summary>
    /// User who initiated this workflow instance
    /// </summary>
    public Guid InitiatedById { get; set; }

    public DateTime StartedDate { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedDate { get; set; }
    public DateTime LastActivityDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// JSON data context for the workflow instance
    /// </summary>
    public string? DataContext { get; set; }

    /// <summary>
    /// Notes or comments about the workflow instance
    /// </summary>
    public string? Notes { get; set; }

    public Guid TenantId { get; set; }

    // Navigation properties
    [ForeignKey("WorkflowDefinitionId")]
    public virtual WorkflowDefinition WorkflowDefinition { get; set; } = null!;

    [ForeignKey("CurrentStepId")]
    public virtual WorkflowStep? CurrentStep { get; set; }

    public virtual ICollection<WorkflowStepInstance> StepInstances { get; set; } = new List<WorkflowStepInstance>();
    public virtual ICollection<WorkflowActivityLog> ActivityLogs { get; set; } = new List<WorkflowActivityLog>();
}

/// <summary>
/// Represents the execution of a specific step within a workflow instance
/// </summary>
public class WorkflowStepInstance
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid WorkflowInstanceId { get; set; }

    [Required]
    public Guid WorkflowStepId { get; set; }

    /// <summary>
    /// Status of this step instance
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed, Skipped, Failed

    /// <summary>
    /// User assigned to execute this step
    /// </summary>
    public Guid? AssignedToId { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Result data from step execution (JSON)
    /// </summary>
    public string? ResultData { get; set; }

    /// <summary>
    /// Comments from the user who executed this step
    /// </summary>
    public string? Comments { get; set; }

    public Guid TenantId { get; set; }

    // Navigation properties
    [ForeignKey("WorkflowInstanceId")]
    public virtual WorkflowInstance WorkflowInstance { get; set; } = null!;

    [ForeignKey("WorkflowStepId")]
    public virtual WorkflowStep WorkflowStep { get; set; } = null!;
}

/// <summary>
/// Logs all activity and changes within workflow instances
/// </summary>
public class WorkflowActivityLog
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid WorkflowInstanceId { get; set; }

    public Guid? WorkflowStepInstanceId { get; set; }

    /// <summary>
    /// Type of activity (e.g., "StepStarted", "StepCompleted", "TransitionTaken", "WorkflowCompleted")
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ActivityType { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// User who performed the activity
    /// </summary>
    public Guid? UserId { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional data about the activity (JSON)
    /// </summary>
    public string? ActivityData { get; set; }

    public Guid TenantId { get; set; }

    // Navigation properties
    [ForeignKey("WorkflowInstanceId")]
    public virtual WorkflowInstance WorkflowInstance { get; set; } = null!;

    [ForeignKey("WorkflowStepInstanceId")]
    public virtual WorkflowStepInstance? WorkflowStepInstance { get; set; }
}

/// <summary>
/// Defines approval requirements for workflow steps
/// </summary>
public class WorkflowApproval
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid WorkflowStepInstanceId { get; set; }

    /// <summary>
    /// User who needs to provide approval
    /// </summary>
    public Guid ApproverId { get; set; }

    /// <summary>
    /// Role that can provide approval (alternative to specific user)
    /// </summary>
    [MaxLength(100)]
    public string? ApproverRole { get; set; }

    /// <summary>
    /// Current status of the approval
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Delegated

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ResponseDate { get; set; }
    public DateTime? DueDate { get; set; }

    [MaxLength(1000)]
    public string? Comments { get; set; }

    /// <summary>
    /// Priority level of the approval (1=Highest, 5=Lowest)
    /// </summary>
    public int Priority { get; set; } = 3;

    public Guid TenantId { get; set; }

    // Navigation properties
    [ForeignKey("WorkflowStepInstanceId")]
    public virtual WorkflowStepInstance WorkflowStepInstance { get; set; } = null!;
}