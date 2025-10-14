using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Workflow;

/// <summary>
/// Represents an activity log entry for workflow execution
/// </summary>
[Table("WorkflowActivityLogs")]
public class WorkflowActivityLog : TenantEntity
{

    /// <summary>
    /// ID of the workflow instance this log entry belongs to
    /// </summary>
    [Required]
    public Guid WorkflowInstanceId { get; set; }

    /// <summary>
    /// ID of the step instance this log entry relates to (if applicable)
    /// </summary>
    public Guid? StepInstanceId { get; set; }

    /// <summary>
    /// Type of activity that occurred
    /// </summary>
    [Required]
    public WorkflowActivityType ActivityType { get; set; }

    /// <summary>
    /// Title or summary of the activity
    /// </summary>
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of the activity
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? Description { get; set; }

    /// <summary>
    /// JSON data related to the activity
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? Data { get; set; }

    /// <summary>
    /// ID of the user who performed the activity
    /// </summary>
    public Guid? PerformedById { get; set; }

    /// <summary>
    /// Date and time when the activity occurred
    /// </summary>
    public DateTime ActivityDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// IP address from which the activity was performed
    /// </summary>
    [StringLength(45)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string from which the activity was performed
    /// </summary>
    [StringLength(500)]
    public string? UserAgent { get; set; }


    /// <summary>
    /// Navigation property for the workflow instance this log entry belongs to
    /// </summary>
    [ForeignKey(nameof(WorkflowInstanceId))]
    public virtual WorkflowInstance WorkflowInstance { get; set; } = null!;

    /// <summary>
    /// Navigation property for the step instance this log entry relates to
    /// </summary>
    [ForeignKey(nameof(StepInstanceId))]
    public virtual WorkflowStepInstance? StepInstance { get; set; }

    /// <summary>
    /// Navigation property for the user who performed the activity
    /// </summary>
    [ForeignKey(nameof(PerformedById))]
    public virtual ApplicationUser? PerformedBy { get; set; }

}