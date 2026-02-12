using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Ehc;

[Table("EhcTicketCategories")]
public class EhcTicketCategory : TenantEntity
{
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public EhcTicketType? AppliesToType { get; set; }

    public Guid? ParentCategoryId { get; set; }

    [ForeignKey(nameof(ParentCategoryId))]
    public virtual EhcTicketCategory? ParentCategory { get; set; }

    public virtual ICollection<EhcTicketCategory> Subcategories { get; set; } = new List<EhcTicketCategory>();
}

[Table("EhcSlaTemplates")]
public class EhcSlaTemplate : TenantEntity
{
    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public EhcTicketType? TicketType { get; set; }
    public EhcTicketPriority? Priority { get; set; }

    public Guid? CategoryId { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public virtual EhcTicketCategory? Category { get; set; }

    /// <summary>
    /// Target first response time in minutes.
    /// </summary>
    public int FirstResponseMinutes { get; set; } = 60;

    /// <summary>
    /// Target resolution time in minutes.
    /// </summary>
    public int ResolutionMinutes { get; set; } = 1440;

    /// <summary>
    /// Optional JSON for business hours / holidays (phase 2).
    /// </summary>
    public string? CalendarConfigurationJson { get; set; }
}

[Table("EhcWorkflowRoutingRules")]
public class EhcWorkflowRoutingRule : TenantEntity
{
    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Higher priority wins when multiple rules match.
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// WorkflowDefinition.Name to start (must exist for the tenant and entity type EHC_TICKET).
    /// </summary>
    [Required]
    [StringLength(200)]
    public string WorkflowName { get; set; } = string.Empty;

    public EhcTicketType? TicketType { get; set; }
    public EhcTicketPriority? TicketPriority { get; set; }

    public Guid? CategoryId { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public virtual EhcTicketCategory? Category { get; set; }

    public Guid? SubcategoryId { get; set; }

    [ForeignKey(nameof(SubcategoryId))]
    public virtual EhcTicketCategory? Subcategory { get; set; }

    public Guid? AssignedDepartmentId { get; set; }

    [ForeignKey(nameof(AssignedDepartmentId))]
    public virtual Department? AssignedDepartment { get; set; }
}

[Table("EhcTickets")]
public class EhcTicket : TenantEntity
{
    [Required]
    [StringLength(30)]
    public string TicketNumber { get; set; } = string.Empty;

    [Required]
    public EhcTicketType TicketType { get; set; }

    public Guid? CategoryId { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public virtual EhcTicketCategory? Category { get; set; }

    public Guid? SubcategoryId { get; set; }

    [ForeignKey(nameof(SubcategoryId))]
    public virtual EhcTicketCategory? Subcategory { get; set; }

    [Required]
    public EhcTicketPriority Priority { get; set; } = EhcTicketPriority.Medium;

    [Required]
    public EhcTicketSource Source { get; set; } = EhcTicketSource.Web;

    [StringLength(200)]
    public string? Subject { get; set; }

    [Required]
    [Column(TypeName = "nvarchar(max)")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Requester (creator) user id.
    /// </summary>
    [Required]
    public Guid RequesterUserId { get; set; }

    [ForeignKey(nameof(RequesterUserId))]
    public virtual ApplicationUser RequesterUser { get; set; } = null!;

    public Guid? AssignedToUserId { get; set; }

    [ForeignKey(nameof(AssignedToUserId))]
    public virtual ApplicationUser? AssignedToUser { get; set; }

    public Guid? AssignedDepartmentId { get; set; }

    [ForeignKey(nameof(AssignedDepartmentId))]
    public virtual Department? AssignedDepartment { get; set; }

    [Required]
    public EhcTicketStatus Status { get; set; } = EhcTicketStatus.New;

    public Guid? WorkflowInstanceId { get; set; }

    public DateTime? FirstResponseDueAt { get; set; }
    public DateTime? ResolutionDueAt { get; set; }
    public DateTime? FirstRespondedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    [StringLength(100)]
    public string? RelatedEntityType { get; set; }

    [StringLength(100)]
    public string? RelatedEntityReference { get; set; }

    public virtual ICollection<EhcTicketMessage> Messages { get; set; } = new List<EhcTicketMessage>();
    public virtual ICollection<EhcTicketAttachment> Attachments { get; set; } = new List<EhcTicketAttachment>();
    public virtual ICollection<EhcTicketStatusHistory> StatusHistory { get; set; } = new List<EhcTicketStatusHistory>();
    public virtual ICollection<EhcTicketAuditEvent> AuditEvents { get; set; } = new List<EhcTicketAuditEvent>();
}

[Table("EhcTicketMessages")]
public class EhcTicketMessage : TenantEntity
{
    [Required]
    public Guid TicketId { get; set; }

    [ForeignKey(nameof(TicketId))]
    public virtual EhcTicket Ticket { get; set; } = null!;

    [Required]
    [Column(TypeName = "nvarchar(max)")]
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// True for internal-only comments; false for external-facing thread.
    /// </summary>
    public bool IsInternal { get; set; } = false;

    public Guid? AuthorUserId { get; set; }

    [ForeignKey(nameof(AuthorUserId))]
    public virtual ApplicationUser? AuthorUser { get; set; }

    public virtual ICollection<EhcTicketAttachment> Attachments { get; set; } = new List<EhcTicketAttachment>();
}

[Table("EhcTicketAttachments")]
public class EhcTicketAttachment : TenantEntity
{
    [Required]
    public Guid TicketId { get; set; }

    [ForeignKey(nameof(TicketId))]
    public virtual EhcTicket Ticket { get; set; } = null!;

    public Guid? MessageId { get; set; }

    [ForeignKey(nameof(MessageId))]
    public virtual EhcTicketMessage? Message { get; set; }

    [Required]
    [StringLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    [StringLength(300)]
    public string FileName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? ContentType { get; set; }

    public long FileSize { get; set; }

    /// <summary>
    /// Internal-only attachments should not be visible/downloadable by external users.
    /// </summary>
    public bool IsInternal { get; set; } = false;
}

[Table("EhcTicketStatusHistory")]
public class EhcTicketStatusHistory : TenantEntity
{
    [Required]
    public Guid TicketId { get; set; }

    [ForeignKey(nameof(TicketId))]
    public virtual EhcTicket Ticket { get; set; } = null!;

    public EhcTicketStatus? FromStatus { get; set; }

    [Required]
    public EhcTicketStatus ToStatus { get; set; }

    public Guid? ChangedByUserId { get; set; }

    [ForeignKey(nameof(ChangedByUserId))]
    public virtual ApplicationUser? ChangedByUser { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}

[Table("EhcTicketAuditEvents")]
public class EhcTicketAuditEvent : TenantEntity
{
    [Required]
    public Guid TicketId { get; set; }

    [ForeignKey(nameof(TicketId))]
    public virtual EhcTicket Ticket { get; set; } = null!;

    [Required]
    [StringLength(80)]
    public string EventType { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Title { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? Body { get; set; }

    public bool IsInternal { get; set; } = false;

    public Guid? ActorUserId { get; set; }

    [ForeignKey(nameof(ActorUserId))]
    public virtual ApplicationUser? ActorUser { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? DataJson { get; set; }
}
