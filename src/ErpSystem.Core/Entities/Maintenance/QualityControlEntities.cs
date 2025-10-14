using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Maintenance;

/// <summary>
/// Tracks rework and rejection processes for work orders
/// </summary>
public class WorkOrderRework
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid WorkOrderId { get; set; }

    /// <summary>
    /// Inspector who identified the rework need
    /// </summary>
    [Required]
    public Guid InspectorId { get; set; }

    /// <summary>
    /// Reason for rework
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ReworkReason { get; set; } = string.Empty; // QualityIssue, SafetyViolation, IncompleteWork, CustomerComplaint

    /// <summary>
    /// Detailed description of what needs rework
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Severity level of the issue
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Severity { get; set; } = "Medium"; // Low, Medium, High, Critical

    /// <summary>
    /// Current status of rework
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending"; // Pending, Assigned, InProgress, Completed, Cancelled

    /// <summary>
    /// Technician assigned to perform rework
    /// </summary>
    public Guid? AssignedTechnicianId { get; set; }

    /// <summary>
    /// Date rework was identified
    /// </summary>
    [Required]
    public DateTime IdentifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Target completion date for rework
    /// </summary>
    public DateTime? TargetCompletionDate { get; set; }

    /// <summary>
    /// Date rework was started
    /// </summary>
    public DateTime? StartedDate { get; set; }

    /// <summary>
    /// Date rework was completed
    /// </summary>
    public DateTime? CompletedDate { get; set; }

    /// <summary>
    /// Estimated hours for rework
    /// </summary>
    public double? EstimatedHours { get; set; }

    /// <summary>
    /// Actual hours spent on rework
    /// </summary>
    public double? ActualHours { get; set; }

    /// <summary>
    /// Additional cost for rework
    /// </summary>
    public decimal? AdditionalCost { get; set; }

    /// <summary>
    /// Notes about the rework
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Supporting photos or documents
    /// </summary>
    public string? AttachmentPaths { get; set; }

    /// <summary>
    /// Whether customer notification is required
    /// </summary>
    public bool RequiresCustomerNotification { get; set; } = false;

    /// <summary>
    /// Whether this rework affects asset warranty
    /// </summary>
    public bool AffectsWarranty { get; set; } = false;

    public Guid TenantId { get; set; }

    // Navigation properties
    public virtual WorkOrder WorkOrder { get; set; } = null!;
    public virtual ICollection<WorkOrderReworkTask> ReworkTasks { get; set; } = new List<WorkOrderReworkTask>();
}

/// <summary>
/// Individual tasks within a rework order
/// </summary>
public class WorkOrderReworkTask
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid WorkOrderReworkId { get; set; }

    [Required]
    [MaxLength(200)]
    public string TaskDescription { get; set; } = string.Empty;

    /// <summary>
    /// Sequence order of this task
    /// </summary>
    public int Sequence { get; set; }

    /// <summary>
    /// Status of this specific task
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed, Skipped

    /// <summary>
    /// Estimated hours for this task
    /// </summary>
    public double? EstimatedHours { get; set; }

    /// <summary>
    /// Actual hours for this task
    /// </summary>
    public double? ActualHours { get; set; }

    /// <summary>
    /// Date task was completed
    /// </summary>
    public DateTime? CompletedDate { get; set; }

    /// <summary>
    /// Technician who completed this task
    /// </summary>
    public Guid? CompletedById { get; set; }

    /// <summary>
    /// Notes about this task
    /// </summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    public Guid TenantId { get; set; }

    // Navigation property
    [ForeignKey("WorkOrderReworkId")]
    public virtual WorkOrderRework WorkOrderRework { get; set; } = null!;
}

/// <summary>
/// Enhanced inspection approval process
/// </summary>
public class InspectionApproval
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid InspectionId { get; set; }

    /// <summary>
    /// Level of approval (1=Supervisor, 2=Manager, 3=Director)
    /// </summary>
    [Required]
    public int ApprovalLevel { get; set; }

    /// <summary>
    /// User who needs to provide approval
    /// </summary>
    [Required]
    public Guid ApproverId { get; set; }

    /// <summary>
    /// Role required for approval
    /// </summary>
    [MaxLength(100)]
    public string? ApproverRole { get; set; }

    /// <summary>
    /// Current status of approval
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Delegated

    /// <summary>
    /// Date approval was requested
    /// </summary>
    [Required]
    public DateTime RequestedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Due date for approval
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Date approval was provided
    /// </summary>
    public DateTime? ApprovedDate { get; set; }

    /// <summary>
    /// Comments from approver
    /// </summary>
    [MaxLength(2000)]
    public string? Comments { get; set; }

    /// <summary>
    /// Conditions attached to approval
    /// </summary>
    [MaxLength(1000)]
    public string? Conditions { get; set; }

    /// <summary>
    /// Priority level of approval request
    /// </summary>
    [Range(1, 5)]
    public int Priority { get; set; } = 3;

    /// <summary>
    /// Whether this approval can be delegated
    /// </summary>
    public bool CanDelegate { get; set; } = true;

    /// <summary>
    /// User approval was delegated to
    /// </summary>
    public Guid? DelegatedToId { get; set; }

    /// <summary>
    /// Date approval was delegated
    /// </summary>
    public DateTime? DelegatedDate { get; set; }

    /// <summary>
    /// Reason for delegation
    /// </summary>
    [MaxLength(500)]
    public string? DelegationReason { get; set; }

    public Guid TenantId { get; set; }

    // Navigation property
    [ForeignKey("InspectionId")]
    public virtual AssetInspection Inspection { get; set; } = null!;
}

/// <summary>
/// Quality control checklists for work orders
/// </summary>
public class QualityControlChecklist
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Type of work order this checklist applies to
    /// </summary>
    [MaxLength(50)]
    public string? WorkOrderType { get; set; }

    /// <summary>
    /// Asset category this checklist applies to
    /// </summary>
    [MaxLength(50)]
    public string? AssetCategory { get; set; }

    /// <summary>
    /// Maintenance type this checklist applies to
    /// </summary>
    [MaxLength(50)]
    public string? MaintenanceType { get; set; }

    /// <summary>
    /// Whether this checklist is mandatory
    /// </summary>
    public bool IsMandatory { get; set; } = true;

    /// <summary>
    /// Whether this checklist is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Checklist items in JSON format
    /// </summary>
    [Required]
    public string ChecklistItems { get; set; } = "[]";

    /// <summary>
    /// Minimum passing score percentage
    /// </summary>
    [Range(0, 100)]
    public int MinimumPassingScore { get; set; } = 80;

    /// <summary>
    /// Version of this checklist
    /// </summary>
    public int Version { get; set; } = 1;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedDate { get; set; }
    public Guid CreatedById { get; set; }
    public Guid? LastModifiedById { get; set; }
    public Guid TenantId { get; set; }

    // Navigation properties
    public virtual ICollection<WorkOrderQualityCheck> QualityChecks { get; set; } = new List<WorkOrderQualityCheck>();
}

/// <summary>
/// Quality control check results for work orders
/// </summary>
public class WorkOrderQualityCheck
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid WorkOrderId { get; set; }

    [Required]
    public Guid ChecklistId { get; set; }

    /// <summary>
    /// Inspector who performed the quality check
    /// </summary>
    [Required]
    public Guid InspectorId { get; set; }

    /// <summary>
    /// Date quality check was performed
    /// </summary>
    [Required]
    public DateTime InspectionDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Overall result (Pass, Fail, ConditionalPass)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string OverallResult { get; set; } = "Pass";

    /// <summary>
    /// Score achieved (percentage)
    /// </summary>
    [Range(0, 100)]
    public int Score { get; set; }

    /// <summary>
    /// Detailed check results in JSON format
    /// </summary>
    [Required]
    public string CheckResults { get; set; } = "[]";

    /// <summary>
    /// Inspector notes
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Corrective actions required
    /// </summary>
    [MaxLength(2000)]
    public string? CorrectiveActions { get; set; }

    /// <summary>
    /// Follow-up required
    /// </summary>
    public bool RequiresFollowUp { get; set; } = false;

    /// <summary>
    /// Follow-up due date
    /// </summary>
    public DateTime? FollowUpDueDate { get; set; }

    /// <summary>
    /// Supporting photos or documents
    /// </summary>
    public string? AttachmentPaths { get; set; }

    public Guid TenantId { get; set; }

    // Navigation properties
    [ForeignKey("ChecklistId")]
    public virtual QualityControlChecklist Checklist { get; set; } = null!;

    public virtual ICollection<WorkOrderRework> RelatedRework { get; set; } = new List<WorkOrderRework>();
}

/// <summary>
/// Quality metrics and performance tracking
/// </summary>
public class QualityMetrics
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Date these metrics were calculated for
    /// </summary>
    [Required]
    public DateTime MetricsDate { get; set; }

    /// <summary>
    /// Technician these metrics apply to (null for overall)
    /// </summary>
    public Guid? TechnicianId { get; set; }

    /// <summary>
    /// Team these metrics apply to (null for overall)
    /// </summary>
    public Guid? TeamId { get; set; }

    /// <summary>
    /// Asset category these metrics apply to
    /// </summary>
    [MaxLength(50)]
    public string? AssetCategory { get; set; }

    /// <summary>
    /// Total work orders completed
    /// </summary>
    public int TotalWorkOrders { get; set; }

    /// <summary>
    /// Work orders that passed quality check on first attempt
    /// </summary>
    public int FirstTimePassCount { get; set; }

    /// <summary>
    /// Work orders requiring rework
    /// </summary>
    public int ReworkCount { get; set; }

    /// <summary>
    /// Work orders rejected
    /// </summary>
    public int RejectedCount { get; set; }

    /// <summary>
    /// First time fix rate percentage
    /// </summary>
    public double FirstTimeFixRate { get; set; }

    /// <summary>
    /// Average quality score
    /// </summary>
    public double AverageQualityScore { get; set; }

    /// <summary>
    /// Customer satisfaction score (if available)
    /// </summary>
    public double? CustomerSatisfactionScore { get; set; }

    /// <summary>
    /// Average time to complete quality checks
    /// </summary>
    public double AverageInspectionTime { get; set; }

    /// <summary>
    /// Number of safety violations
    /// </summary>
    public int SafetyViolations { get; set; }

    /// <summary>
    /// Compliance percentage
    /// </summary>
    public double CompliancePercentage { get; set; }

    public DateTime CalculatedDate { get; set; } = DateTime.UtcNow;
    public Guid TenantId { get; set; }
}