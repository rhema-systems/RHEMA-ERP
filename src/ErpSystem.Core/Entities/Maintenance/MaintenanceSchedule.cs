using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Entities.Maintenance;

/// <summary>
/// Defines maintenance schedules for automated work order generation
/// </summary>
public class MaintenanceSchedule : TenantEntity
{
    /// <summary>
    /// Schedule name/title
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Unique schedule code
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Schedule description
    /// </summary>
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Asset to be maintained
    /// </summary>
    [Required]
    public Guid AssetId { get; set; }

    /// <summary>
    /// Type of maintenance to perform
    /// </summary>
    [Required]
    public Guid MaintenanceTypeId { get; set; }

    /// <summary>
    /// Schedule frequency type
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Frequency { get; set; } = string.Empty; // Daily, Weekly, Monthly, Quarterly, Yearly, Custom

    /// <summary>
    /// Frequency value (e.g., every 2 weeks, every 3 months)
    /// </summary>
    public int FrequencyValue { get; set; } = 1;

    /// <summary>
    /// Frequency unit for calculation
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string FrequencyUnit { get; set; } = string.Empty; // Days, Weeks, Months, Hours, Miles, etc.

    /// <summary>
    /// Schedule start date
    /// </summary>
    public DateTime StartDate { get; set; }
    
    /// <summary>
    /// Next scheduled maintenance date
    /// </summary>
    [Required]
    public DateTime NextDueDate { get; set; }

    /// <summary>
    /// Last time maintenance was completed
    /// </summary>
    public DateTime? LastCompletedDate { get; set; }

    /// <summary>
    /// Maintenance priority level
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Priority { get; set; } = string.Empty;

    /// <summary>
    /// Estimated hours required
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedHours { get; set; }

    /// <summary>
    /// Estimated cost for maintenance
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedCost { get; set; }

    /// <summary>
    /// Assigned technician (optional)
    /// </summary>
    public Guid? AssignedTechnicianId { get; set; }

    /// <summary>
    /// Assigned team (optional)
    /// </summary>
    public Guid? AssignedTeamId { get; set; }

    /// <summary>
    /// Detailed maintenance instructions
    /// </summary>
    [MaxLength(2000)]
    public string Instructions { get; set; } = string.Empty;

    /// <summary>
    /// Safety notes and requirements
    /// </summary>
    [MaxLength(1000)]
    public string SafetyNotes { get; set; } = string.Empty;

    /// <summary>
    /// Required skills (JSON array)
    /// </summary>
    public string RequiredSkills { get; set; } = "[]";

    /// <summary>
    /// Required tools (JSON array)
    /// </summary>
    public string RequiredTools { get; set; } = "[]";

    /// <summary>
    /// Required parts (JSON array)
    /// </summary>
    public string RequiredParts { get; set; } = "[]";

    /// <summary>
    /// Whether to automatically generate work orders
    /// </summary>
    public bool AutoGenerateWorkOrders { get; set; } = true;

    /// <summary>
    /// Days in advance to send notifications
    /// </summary>
    public int? AdvanceNotificationDays { get; set; }

    /// <summary>
    /// Email addresses for notifications
    /// </summary>
    [MaxLength(500)]
    public string? NotificationRecipients { get; set; }

    /// <summary>
    /// Whether the schedule is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Schedule type for categorization (Preventive, Corrective, Predictive, etc.)
    /// </summary>
    [MaxLength(50)]
    public string ScheduleType { get; set; } = "Preventive";

    /// <summary>
    /// Last date work order was generated from this schedule
    /// </summary>
    public DateTime? LastGeneratedDate { get; set; }

    /// <summary>
    /// Last date this schedule was processed by the scheduler
    /// </summary>
    public DateTime? LastProcessedDate { get; set; }

    /// <summary>
    /// Default technician to assign to generated work orders
    /// </summary>
    public Guid? DefaultTechnicianId { get; set; }

    /// <summary>
    /// Default team to assign to generated work orders
    /// </summary>
    public Guid? DefaultTeamId { get; set; }

    /// <summary>
    /// Priority level ID for generated work orders
    /// </summary>
    public Guid? PriorityLevelId { get; set; }

    // Navigation properties
    /// <summary>
    /// Asset being maintained by this schedule
    /// </summary>
    public virtual MaintenanceAsset Asset { get; set; } = null!;

    /// <summary>
    /// Type of maintenance performed by this schedule
    /// </summary>
    public virtual MaintenanceType MaintenanceType { get; set; } = null!;

    /// <summary>
    /// Default technician for generated work orders
    /// </summary>
    public virtual Employee? DefaultTechnician { get; set; }

    /// <summary>
    /// Default team for generated work orders
    /// </summary>
    public virtual TechnicianTeam? DefaultTeam { get; set; }

    /// <summary>
    /// Priority level for generated work orders
    /// </summary>
    public virtual PriorityLevel? PriorityLevel { get; set; }

    /// <summary>
    /// Work orders generated from this schedule
    /// </summary>
    public virtual ICollection<ScheduledWorkOrder> GeneratedWorkOrders { get; set; } = new List<ScheduledWorkOrder>();

    // Computed properties for reporting
    /// <summary>
    /// Number of completed work orders from this schedule
    /// </summary>
    [NotMapped]
    public int CompletedWorkOrdersCount { get; set; }

    /// <summary>
    /// Compliance percentage based on on-time completion
    /// </summary>
    [NotMapped]
    public decimal CompliancePercentage { get; set; }

    /// <summary>
    /// Whether this schedule is overdue
    /// </summary>
    [NotMapped]
    public bool IsOverdue => NextDueDate < DateTime.UtcNow;

    /// <summary>
    /// Days until next maintenance is due
    /// </summary>
    [NotMapped]
    public int DaysUntilDue => (int)(NextDueDate - DateTime.UtcNow).TotalDays;
}

/// <summary>
/// History of schedule changes and adjustments
/// </summary>
public class MaintenanceScheduleHistory : TenantEntity
{
    /// <summary>
    /// Schedule that was modified
    /// </summary>
    [Required]
    public Guid ScheduleId { get; set; }

    /// <summary>
    /// Type of change made
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ChangeType { get; set; } = string.Empty; // Created, Modified, Activated, Deactivated, Deleted

    /// <summary>
    /// Previous values (JSON)
    /// </summary>
    public string? PreviousValues { get; set; }

    /// <summary>
    /// New values (JSON)
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// Reason for the change
    /// </summary>
    [MaxLength(500)]
    public string? ChangeReason { get; set; }

    /// <summary>
    /// User who made the change
    /// </summary>
    [Required]
    public Guid ChangedById { get; set; }

    /// <summary>
    /// Navigation property to schedule
    /// </summary>
    public virtual MaintenanceSchedule? Schedule { get; set; }
}

/// <summary>
/// Generated work orders from maintenance schedules
/// </summary>
public class ScheduledWorkOrder : BaseEntity
{
    /// <summary>
    /// Schedule that generated this work order
    /// </summary>
    [Required]
    public Guid ScheduleId { get; set; }

    /// <summary>
    /// Generated work order
    /// </summary>
    [Required]
    public Guid WorkOrderId { get; set; }

    /// <summary>
    /// Date the work order was generated
    /// </summary>
    [Required]
    public DateTime GeneratedDate { get; set; }

    /// <summary>
    /// Scheduled completion date from the schedule
    /// </summary>
    [Required]
    public DateTime ScheduledCompletionDate { get; set; }

    /// <summary>
    /// Actual completion date (if completed)
    /// </summary>
    public DateTime? ActualCompletionDate { get; set; }

    /// <summary>
    /// Whether the work order was completed on time
    /// </summary>
    [NotMapped]
    public bool IsOnTime => ActualCompletionDate.HasValue && 
                           ActualCompletionDate <= ScheduledCompletionDate;

    /// <summary>
    /// Navigation property to schedule
    /// </summary>
    public virtual MaintenanceSchedule? Schedule { get; set; }
}