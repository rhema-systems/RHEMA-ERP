using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Sales;
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

[Table("EhcRootCauseCodes")]
public class EhcRootCauseCode : TenantEntity
{
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

[Table("EhcCannedResponses")]
public class EhcCannedResponse : TenantEntity
{
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "nvarchar(max)")]
    public string Body { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public EhcTicketType? AppliesToType { get; set; }

    public Guid? CategoryId { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public virtual EhcTicketCategory? Category { get; set; }
}

[Table("EhcAgentReplyProfiles")]
public class EhcAgentReplyProfile : TenantEntity
{
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual ApplicationUser User { get; set; } = null!;

    /// <summary>
    /// Optional signature appended to requester-facing replies.
    /// </summary>
    [StringLength(2000)]
    public string? Signature { get; set; }

    public bool IsSignatureEnabled { get; set; } = true;

    public bool AppendSignatureToReplies { get; set; } = true;
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
    public Guid? PublicPropertyEnquiryContactId { get; set; }

    [ForeignKey(nameof(PublicPropertyEnquiryContactId))]
    public virtual EhcPublicPropertyEnquiryContact? PublicPropertyEnquiryContact { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? PropertyListingContextJson { get; set; }

    public Guid? ExternalSubmissionId { get; set; }

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
    /// Authenticated requester user id. Public property enquiries deliberately leave this null;
    /// their immutable contact identity is stored in PropertyListingContextJson instead.
    /// </summary>
    public Guid? RequesterUserId { get; set; }

    [ForeignKey(nameof(RequesterUserId))]
    public virtual ApplicationUser? RequesterUser { get; set; }

    public Guid? AssignedToUserId { get; set; }

    [ForeignKey(nameof(AssignedToUserId))]
    public virtual ApplicationUser? AssignedToUser { get; set; }

    public Guid? AssignedDepartmentId { get; set; }

    [ForeignKey(nameof(AssignedDepartmentId))]
    public virtual Department? AssignedDepartment { get; set; }

    /// <summary>
    /// Current assignment owner from HR's Structure → Level → Unit model. AssignedDepartmentId
    /// remains only to display historic assignments made before the organization-unit port.
    /// </summary>
    public Guid? AssignedOrganizationUnitId { get; set; }

    [ForeignKey(nameof(AssignedOrganizationUnitId))]
    public virtual OrganizationUnit? AssignedOrganizationUnit { get; set; }


    /// <summary>
    /// CRM records created when a public property enquiry is accepted by Sales.
    /// They remain empty while Helpdesk owns the initial triage.
    /// </summary>
    public Guid? CrmLeadId { get; set; }

    [ForeignKey(nameof(CrmLeadId))]
    public virtual Lead? CrmLead { get; set; }

    public Guid? CrmOpportunityId { get; set; }

    [ForeignKey(nameof(CrmOpportunityId))]
    public virtual Opportunity? CrmOpportunity { get; set; }

    /// <summary>
    /// Estate procedure case created after Sales closes the linked property-enquiry opportunity as won.
    /// Kept as a durable cross-module reference so retries do not create a second Estate application.
    /// </summary>
    public Guid? EstateListingApplicationCaseId { get; set; }

    [StringLength(80)]
    public string? EstateListingApplicationReference { get; set; }

    public DateTime? EstateListingApplicationHandedOffAt { get; set; }

    [Required]
    public EhcTicketStatus Status { get; set; } = EhcTicketStatus.New;

    public Guid? WorkflowInstanceId { get; set; }

    public DateTime? FirstResponseDueAt { get; set; }
    public DateTime? ResolutionDueAt { get; set; }
    public DateTime? FirstRespondedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    // Applied SLA snapshot (for pause/resume math and auditability)
    public Guid? AppliedSlaTemplateId { get; set; }
    public int? AppliedFirstResponseMinutes { get; set; }
    public int? AppliedResolutionMinutes { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? AppliedSlaCalendarConfigurationJson { get; set; }

    [StringLength(100)]
    public string? RelatedEntityType { get; set; }

    [StringLength(100)]
    public string? RelatedEntityReference { get; set; }

    // Complaint / RCA fields (internal-only UI, but stored for reporting)
    public Guid? RootCauseId { get; set; }

    [ForeignKey(nameof(RootCauseId))]
    public virtual EhcRootCauseCode? RootCause { get; set; }

    [StringLength(2000)]
    public string? RootCauseDetails { get; set; }

    [StringLength(2000)]
    public string? ResolutionSummary { get; set; }

    public virtual ICollection<EhcTicketMessage> Messages { get; set; } = new List<EhcTicketMessage>();
    public virtual ICollection<EhcTicketAttachment> Attachments { get; set; } = new List<EhcTicketAttachment>();
    public virtual ICollection<EhcTicketStatusHistory> StatusHistory { get; set; } = new List<EhcTicketStatusHistory>();
    public virtual ICollection<EhcTicketAuditEvent> AuditEvents { get; set; } = new List<EhcTicketAuditEvent>();
    public virtual ICollection<EhcTicketFeedback> Feedbacks { get; set; } = new List<EhcTicketFeedback>();
    public virtual ICollection<EhcTicketWatcher> Watchers { get; set; } = new List<EhcTicketWatcher>();
    public virtual ICollection<EhcTicketLink> Links { get; set; } = new List<EhcTicketLink>();
    public virtual ICollection<EhcCrmEngagementLink> CrmEngagementLinks { get; set; } = new List<EhcCrmEngagementLink>();
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

/// <summary>
/// Idempotency and traceability link between an EHC property enquiry event and its CRM activity.
/// </summary>
[Table("EhcCrmEngagementLinks")]
public class EhcCrmEngagementLink : TenantEntity
{
    [Required]
    public Guid TicketId { get; set; }

    [ForeignKey(nameof(TicketId))]
    public virtual EhcTicket Ticket { get; set; } = null!;

    [Required]
    public Guid CrmActivityId { get; set; }

    [ForeignKey(nameof(CrmActivityId))]
    public virtual Activity CrmActivity { get; set; } = null!;

    [Required]
    [StringLength(200)]
    public string SourceKey { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string EngagementType { get; set; } = string.Empty;
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

[Table("EhcTicketFeedbacks")]
public class EhcTicketFeedback : TenantEntity
{
    [Required]
    public Guid TicketId { get; set; }

    [ForeignKey(nameof(TicketId))]
    public virtual EhcTicket Ticket { get; set; } = null!;

    [Required]
    public Guid SubmittedByUserId { get; set; }

    [ForeignKey(nameof(SubmittedByUserId))]
    public virtual ApplicationUser SubmittedByUser { get; set; } = null!;

    /// <summary>
    /// Customer satisfaction rating (1-5).
    /// </summary>
    public int Rating { get; set; }

    [StringLength(2000)]
    public string? Comment { get; set; }
}

[Table("EhcTicketWatchers")]
public class EhcTicketWatcher : TenantEntity
{
    [Required]
    public Guid TicketId { get; set; }

    [ForeignKey(nameof(TicketId))]
    public virtual EhcTicket Ticket { get; set; } = null!;

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual ApplicationUser User { get; set; } = null!;
}

[Table("EhcTicketLinks")]
public class EhcTicketLink : TenantEntity
{
    [Required]
    public Guid TicketId { get; set; }

    [ForeignKey(nameof(TicketId))]
    public virtual EhcTicket Ticket { get; set; } = null!;

    [Required]
    public Guid RelatedTicketId { get; set; }

    [ForeignKey(nameof(RelatedTicketId))]
    public virtual EhcTicket RelatedTicket { get; set; } = null!;

    [Required]
    public EhcTicketLinkType LinkType { get; set; } = EhcTicketLinkType.Related;

    [StringLength(500)]
    public string? Notes { get; set; }
}
