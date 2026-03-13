using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Ehc;

public enum EhcEscalationTrigger
{
    FirstResponseDueSoon = 1,
    FirstResponseBreached = 2,
    ResolutionDueSoon = 3,
    ResolutionBreached = 4
}

[Table("EhcEscalationPolicies")]
public class EhcEscalationPolicy : TenantEntity
{
    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int Priority { get; set; } = 0;

    [Required]
    public EhcEscalationTrigger Trigger { get; set; }

    /// <summary>
    /// For *DueSoon triggers*, how many minutes before due time the policy becomes eligible.
    /// For breach triggers, this value is ignored.
    /// </summary>
    public int DueSoonMinutes { get; set; } = 15;

    public EhcTicketType? TicketType { get; set; }
    public EhcTicketPriority? TicketPriority { get; set; }

    public Guid? CategoryId { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public virtual EhcTicketCategory? Category { get; set; }

    public Guid? SubcategoryId { get; set; }

    [ForeignKey(nameof(SubcategoryId))]
    public virtual EhcTicketCategory? Subcategory { get; set; }

    public Guid? DepartmentId { get; set; }

    [ForeignKey(nameof(DepartmentId))]
    public virtual Department? Department { get; set; }

    public virtual ICollection<EhcEscalationPolicyLevel> Levels { get; set; } = new List<EhcEscalationPolicyLevel>();
}

[Table("EhcEscalationPolicyLevels")]
public class EhcEscalationPolicyLevel : TenantEntity
{
    [Required]
    public Guid PolicyId { get; set; }

    [ForeignKey(nameof(PolicyId))]
    public virtual EhcEscalationPolicy Policy { get; set; } = null!;

    /// <summary>
    /// Level number (1..n). Lower executes first.
    /// </summary>
    public int Level { get; set; } = 1;

    /// <summary>
    /// Optional minutes after policy eligibility to execute this level.
    /// Example: Level1 at 0 minutes, Level2 at +30 minutes, Level3 at +60 minutes.
    /// </summary>
    public int DelayMinutes { get; set; } = 0;

    /// <summary>
    /// Optional JSON list of roles to notify (e.g. ["HelpdeskSupervisor","HelpdeskManager"]).
    /// </summary>
    public string? NotifyRolesJson { get; set; }

    public bool NotifyAssignedAgent { get; set; } = true;

    /// <summary>
    /// Optional specific user to notify in addition to role-based recipients.
    /// </summary>
    public Guid? NotifyUserId { get; set; }

    /// <summary>
    /// When true, create an internal comment on the ticket describing the escalation.
    /// </summary>
    public bool AddInternalComment { get; set; } = true;

    /// <summary>
    /// Optional role to reassign the ticket to (first active matching user will be selected, optionally within ticket department).
    /// </summary>
    [StringLength(100)]
    public string? ReassignToRole { get; set; }

    /// <summary>
    /// Optional explicit user to reassign to.
    /// </summary>
    public Guid? ReassignToUserId { get; set; }
}

[Table("EhcEscalationExecutions")]
public class EhcEscalationExecution : TenantEntity
{
    [Required]
    public Guid TicketId { get; set; }

    [ForeignKey(nameof(TicketId))]
    public virtual EhcTicket Ticket { get; set; } = null!;

    [Required]
    public Guid PolicyId { get; set; }

    [ForeignKey(nameof(PolicyId))]
    public virtual EhcEscalationPolicy Policy { get; set; } = null!;

    public int Level { get; set; }

    public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;

    [StringLength(300)]
    public string? Reason { get; set; }
}

