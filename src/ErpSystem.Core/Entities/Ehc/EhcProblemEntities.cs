using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Ehc;

[Table("EhcProblems")]
public class EhcProblem : TenantEntity
{
    [Required]
    [StringLength(30)]
    public string ProblemNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "nvarchar(max)")]
    public string Description { get; set; } = string.Empty;

    [Required]
    public EhcTicketPriority Priority { get; set; } = EhcTicketPriority.Medium;

    [Required]
    public EhcProblemStatus Status { get; set; } = EhcProblemStatus.Open;

    public Guid? CategoryId { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public virtual EhcTicketCategory? Category { get; set; }

    public Guid? SubcategoryId { get; set; }

    [ForeignKey(nameof(SubcategoryId))]
    public virtual EhcTicketCategory? Subcategory { get; set; }

    public Guid? DepartmentId { get; set; }

    [ForeignKey(nameof(DepartmentId))]
    public virtual Department? Department { get; set; }

    public Guid? OwnerUserId { get; set; }

    [ForeignKey(nameof(OwnerUserId))]
    public virtual ApplicationUser? OwnerUser { get; set; }

    // Optional linkage to the first ticket that created this problem (conversion).
    public Guid? CreatedFromTicketId { get; set; }

    [ForeignKey(nameof(CreatedFromTicketId))]
    public virtual EhcTicket? CreatedFromTicket { get; set; }

    // RCA fields (internal-only UI)
    public Guid? RootCauseId { get; set; }

    [ForeignKey(nameof(RootCauseId))]
    public virtual EhcRootCauseCode? RootCause { get; set; }

    [StringLength(2000)]
    public string? RootCauseDetails { get; set; }

    [StringLength(2000)]
    public string? ResolutionSummary { get; set; }

    public virtual ICollection<EhcProblemTicketLink> TicketLinks { get; set; } = new List<EhcProblemTicketLink>();
    public virtual ICollection<EhcCapaTask> CapaTasks { get; set; } = new List<EhcCapaTask>();
    public virtual ICollection<EhcProblemAuditEvent> AuditEvents { get; set; } = new List<EhcProblemAuditEvent>();
}

[Table("EhcProblemTicketLinks")]
public class EhcProblemTicketLink : TenantEntity
{
    [Required]
    public Guid ProblemId { get; set; }

    [ForeignKey(nameof(ProblemId))]
    public virtual EhcProblem Problem { get; set; } = null!;

    [Required]
    public Guid TicketId { get; set; }

    [ForeignKey(nameof(TicketId))]
    public virtual EhcTicket Ticket { get; set; } = null!;

    [StringLength(500)]
    public string? Notes { get; set; }
}

[Table("EhcCapaTasks")]
public class EhcCapaTask : TenantEntity
{
    [Required]
    public Guid ProblemId { get; set; }

    [ForeignKey(nameof(ProblemId))]
    public virtual EhcProblem Problem { get; set; } = null!;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string? Description { get; set; }

    public EhcCapaTaskStatus Status { get; set; } = EhcCapaTaskStatus.Open;

    public Guid? AssignedToUserId { get; set; }

    [ForeignKey(nameof(AssignedToUserId))]
    public virtual ApplicationUser? AssignedToUser { get; set; }

    public Guid? AssignedDepartmentId { get; set; }

    [ForeignKey(nameof(AssignedDepartmentId))]
    public virtual Department? AssignedDepartment { get; set; }

    public DateTime? DueAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

[Table("EhcProblemAuditEvents")]
public class EhcProblemAuditEvent : TenantEntity
{
    [Required]
    public Guid ProblemId { get; set; }

    [ForeignKey(nameof(ProblemId))]
    public virtual EhcProblem Problem { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string EventType { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Title { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? Body { get; set; }

    public Guid? ActorUserId { get; set; }

    [ForeignKey(nameof(ActorUserId))]
    public virtual ApplicationUser? ActorUser { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? DataJson { get; set; }
}

