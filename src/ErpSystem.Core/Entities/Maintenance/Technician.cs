using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Entities.Maintenance;

/// <summary>
/// Technician entity representing maintenance technicians with specialized skills and capabilities
/// This extends Employee information with maintenance-specific details
/// </summary>
public class Technician : TenantEntity
{
    /// <summary>
    /// Reference to the HR Employee record
    /// </summary>
    [Required]
    public Guid EmployeeId { get; set; }

    /// <summary>
    /// Employee number/code for external references
    /// </summary>
    [MaxLength(50)]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    /// <summary>
    /// Department the technician belongs to
    /// </summary>
    [MaxLength(100)]
    public string Department { get; set; } = string.Empty;

    /// <summary>
    /// Job position/title
    /// </summary>
    [MaxLength(100)]
    public string Position { get; set; } = string.Empty;

    /// <summary>
    /// Primary specialization area
    /// </summary>
    [MaxLength(100)]
    public string Specialization { get; set; } = string.Empty;

    /// <summary>
    /// Certification level (Level 1, Level 2, Level 3, etc.)
    /// </summary>
    [MaxLength(50)]
    public string CertificationLevel { get; set; } = string.Empty;

    /// <summary>
    /// Experience level (Junior, Intermediate, Senior, Expert)
    /// </summary>
    [MaxLength(50)]
    public string ExperienceLevel { get; set; } = string.Empty;

    /// <summary>
    /// Date when technician was hired
    /// </summary>
    public DateTime HireDate { get; set; }

    /// <summary>
    /// Whether the technician is currently active/available
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Current workload percentage (0-100)
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal CurrentWorkload { get; set; } = 0;

    /// <summary>
    /// Maximum workload capacity percentage
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal MaxWorkload { get; set; } = 100;

    /// <summary>
    /// Average rating from completed work orders
    /// </summary>
    [Column(TypeName = "decimal(3,2)")]
    public decimal AverageRating { get; set; } = 0;

    /// <summary>
    /// Total number of completed work orders
    /// </summary>
    public int CompletedWorkOrders { get; set; } = 0;

    /// <summary>
    /// Last synchronization date with HR system
    /// </summary>
    public DateTime? LastSyncDate { get; set; }

    /// <summary>
    /// Additional notes or comments about the technician
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Navigation properties
    /// <summary>
    /// Reference to the HR Employee entity
    /// </summary>
    public virtual Employee Employee { get; set; } = null!;

    /// <summary>
    /// Technical skills assigned to this technician
    /// </summary>
    public virtual ICollection<TechnicianSkillAssignment> SkillAssignments { get; set; } = new List<TechnicianSkillAssignment>();

    /// <summary>
    /// Work orders assigned to this technician
    /// </summary>
    public virtual ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();

    /// <summary>
    /// Schedule entries for this technician
    /// </summary>
    public virtual ICollection<TechnicianSchedule> Schedules { get; set; } = new List<TechnicianSchedule>();

    /// <summary>
    /// Availability records for this technician
    /// </summary>
    public virtual ICollection<TechnicianAvailability> AvailabilityRecords { get; set; } = new List<TechnicianAvailability>();

    /// <summary>
    /// Team memberships for this technician
    /// </summary>
    public virtual ICollection<TechnicianTeamMember> TeamMemberships { get; set; } = new List<TechnicianTeamMember>();

    /// <summary>
    /// Teams led by this technician
    /// </summary>
    public virtual ICollection<TechnicianTeam> TeamsLed { get; set; } = new List<TechnicianTeam>();
}
