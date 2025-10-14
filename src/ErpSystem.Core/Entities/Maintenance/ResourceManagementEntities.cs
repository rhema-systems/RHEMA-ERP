using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Maintenance;


/// <summary>
/// Tracks technician movement and travel time
/// </summary>
public class TechnicianMovement
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid TechnicianId { get; set; }

    [Required]
    public DateTime MovementDateTime { get; set; }

    /// <summary>
    /// Starting location
    /// </summary>
    [MaxLength(200)]
    public string FromLocation { get; set; } = string.Empty;

    /// <summary>
    /// Destination location
    /// </summary>
    [MaxLength(200)]
    public string ToLocation { get; set; } = string.Empty;

    /// <summary>
    /// GPS coordinates for from location
    /// </summary>
    public double? FromLatitude { get; set; }
    public double? FromLongitude { get; set; }

    /// <summary>
    /// GPS coordinates for to location
    /// </summary>
    public double? ToLatitude { get; set; }
    public double? ToLongitude { get; set; }

    /// <summary>
    /// Reason for movement
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string MovementReason { get; set; } = string.Empty; // WorkOrder, Break, EndOfDay, Emergency

    /// <summary>
    /// Related work order if applicable
    /// </summary>
    public Guid? RelatedWorkOrderId { get; set; }

    /// <summary>
    /// Estimated travel time in minutes
    /// </summary>
    public int? EstimatedTravelMinutes { get; set; }

    /// <summary>
    /// Actual travel time in minutes
    /// </summary>
    public int? ActualTravelMinutes { get; set; }

    /// <summary>
    /// Distance traveled in miles/kilometers
    /// </summary>
    public double? DistanceTraveled { get; set; }

    /// <summary>
    /// Transportation method
    /// </summary>
    [MaxLength(50)]
    public string? TransportationMethod { get; set; }

    public Guid TenantId { get; set; }
}

/// <summary>
/// Enhanced technician scheduling with optimization
/// </summary>
public class TechnicianScheduleOptimization
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid TechnicianId { get; set; }

    [Required]
    public DateTime ScheduleDate { get; set; }

    /// <summary>
    /// Optimized work order sequence
    /// </summary>
    [Required]
    public string WorkOrderSequence { get; set; } = "[]"; // JSON array of work order IDs

    /// <summary>
    /// Total estimated time for all work orders
    /// </summary>
    public double TotalEstimatedHours { get; set; }

    /// <summary>
    /// Total estimated travel time
    /// </summary>
    public double TotalTravelHours { get; set; }

    /// <summary>
    /// Optimization score (0-100)
    /// </summary>
    public double OptimizationScore { get; set; }

    /// <summary>
    /// Optimization algorithm used
    /// </summary>
    [MaxLength(50)]
    public string OptimizationMethod { get; set; } = "Manual";

    /// <summary>
    /// Factors considered in optimization
    /// </summary>
    public string OptimizationFactors { get; set; } = "[]"; // JSON array

    /// <summary>
    /// Whether this schedule is locked/confirmed
    /// </summary>
    public bool IsLocked { get; set; } = false;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastOptimizedDate { get; set; }
    public Guid CreatedById { get; set; }
    public Guid TenantId { get; set; }
}

/// <summary>
/// Tracks technician capacity and workload
/// </summary>
public class TechnicianCapacity
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid TechnicianId { get; set; }

    [Required]
    public DateTime WeekStartDate { get; set; }

    /// <summary>
    /// Total available hours for the week
    /// </summary>
    public double AvailableHours { get; set; }

    /// <summary>
    /// Scheduled work hours
    /// </summary>
    public double ScheduledHours { get; set; }

    /// <summary>
    /// Actual worked hours
    /// </summary>
    public double ActualHours { get; set; }

    /// <summary>
    /// Overtime hours worked
    /// </summary>
    public double OvertimeHours { get; set; }

    /// <summary>
    /// Utilization percentage
    /// </summary>
    public double UtilizationPercentage { get; set; }

    /// <summary>
    /// Number of work orders assigned
    /// </summary>
    public int WorkOrdersAssigned { get; set; }

    /// <summary>
    /// Number of work orders completed
    /// </summary>
    public int WorkOrdersCompleted { get; set; }

    /// <summary>
    /// Number of emergency calls handled
    /// </summary>
    public int EmergencyCallsHandled { get; set; }

    /// <summary>
    /// Travel time for the week
    /// </summary>
    public double TravelHours { get; set; }

    /// <summary>
    /// Performance score for the week
    /// </summary>
    public double PerformanceScore { get; set; }

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public Guid TenantId { get; set; }
}

/// <summary>
/// Tracks skill demand and availability
/// </summary>
public class SkillDemandAnalysis
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid SkillId { get; set; }

    [Required]
    public DateTime AnalysisDate { get; set; }

    /// <summary>
    /// Current demand for this skill
    /// </summary>
    public int CurrentDemand { get; set; }

    /// <summary>
    /// Number of technicians with this skill
    /// </summary>
    public int AvailableTechnicians { get; set; }

    /// <summary>
    /// Average proficiency level
    /// </summary>
    public double AverageProficiencyLevel { get; set; }

    /// <summary>
    /// Projected demand for next month
    /// </summary>
    public int ProjectedDemand { get; set; }

    /// <summary>
    /// Gap analysis result
    /// </summary>
    public int SkillGap { get; set; }

    /// <summary>
    /// Recommended actions
    /// </summary>
    [MaxLength(1000)]
    public string? RecommendedActions { get; set; }

    /// <summary>
    /// Priority level for addressing skill gap
    /// </summary>
    [MaxLength(20)]
    public string Priority { get; set; } = "Medium";

    public Guid TenantId { get; set; }

    // Navigation property
    [ForeignKey("SkillId")]
    public virtual TechnicianSkill Skill { get; set; } = null!;
}

/// <summary>
/// Enhanced work order resource requirements
/// </summary>
public class WorkOrderResourceRequirement
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid WorkOrderId { get; set; }

    /// <summary>
    /// Required skill for this work order
    /// </summary>
    [Required]
    public Guid RequiredSkillId { get; set; }

    /// <summary>
    /// Minimum proficiency level required
    /// </summary>
    [Range(1, 4)]
    public int MinProficiencyLevel { get; set; }

    /// <summary>
    /// Whether this skill is mandatory
    /// </summary>
    public bool IsMandatory { get; set; } = true;

    /// <summary>
    /// Estimated hours for this skill
    /// </summary>
    public double EstimatedHours { get; set; }

    /// <summary>
    /// Priority of this skill requirement
    /// </summary>
    public int Priority { get; set; } = 1;

    public Guid TenantId { get; set; }

    // Navigation properties
    [ForeignKey("RequiredSkillId")]
    public virtual TechnicianSkill RequiredSkill { get; set; } = null!;
}