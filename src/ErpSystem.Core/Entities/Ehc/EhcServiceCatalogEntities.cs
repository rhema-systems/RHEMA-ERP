using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Ehc;

public enum EhcServiceRequestStatus
{
    Draft = 1,
    Submitted = 2,
    PendingApproval = 3,
    Approved = 4,
    Rejected = 5,
    Fulfilled = 6,
    Closed = 7,
    Cancelled = 8
}

[Table("EhcServiceRequestTypes")]
public class EhcServiceRequestType : TenantEntity
{
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional workflow definition name to start for requests of this type. If null/empty,
    /// the system will fall back to the latest active workflow definition for entity type ServiceRequest.
    /// </summary>
    [StringLength(200)]
    public string? WorkflowName { get; set; }

    /// <summary>
    /// JSON definition for the dynamic request form (fields, types, required flags, etc.).
    /// </summary>
    [Required]
    [Column(TypeName = "nvarchar(max)")]
    public string FormDefinitionJson { get; set; } = "{}";

    public virtual ICollection<EhcServiceRequest> Requests { get; set; } = new List<EhcServiceRequest>();
}

[Table("EhcServiceRequests")]
public class EhcServiceRequest : TenantEntity
{
    [Required]
    public Guid RequestTypeId { get; set; }

    [ForeignKey(nameof(RequestTypeId))]
    public virtual EhcServiceRequestType RequestType { get; set; } = null!;

    [Required]
    [StringLength(30)]
    public string RequestNumber { get; set; } = string.Empty;

    [Required]
    public Guid RequesterUserId { get; set; }

    [ForeignKey(nameof(RequesterUserId))]
    public virtual ApplicationUser RequesterUser { get; set; } = null!;

    [Required]
    public EhcServiceRequestStatus Status { get; set; } = EhcServiceRequestStatus.Submitted;

    [StringLength(200)]
    public string? Title { get; set; }

    [Required]
    [Column(TypeName = "nvarchar(max)")]
    public string FormDataJson { get; set; } = "{}";

    public Guid? WorkflowInstanceId { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? ApprovedByUserId { get; set; }

    public DateTime? RejectedAtUtc { get; set; }
    public Guid? RejectedByUserId { get; set; }

    [StringLength(2000)]
    public string? RejectionReason { get; set; }

    public virtual ICollection<EhcServiceRequestAttachment> Attachments { get; set; } = new List<EhcServiceRequestAttachment>();
    public virtual ICollection<EhcServiceRequestAuditEvent> AuditEvents { get; set; } = new List<EhcServiceRequestAuditEvent>();
}

[Table("EhcServiceRequestAttachments")]
public class EhcServiceRequestAttachment : TenantEntity
{
    [Required]
    public Guid ServiceRequestId { get; set; }

    [ForeignKey(nameof(ServiceRequestId))]
    public virtual EhcServiceRequest ServiceRequest { get; set; } = null!;

    [Required]
    [StringLength(1000)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    [StringLength(260)]
    public string FileName { get; set; } = string.Empty;

    [StringLength(200)]
    public string? ContentType { get; set; }

    public long FileSize { get; set; }

    public bool IsInternal { get; set; } = false;
}

[Table("EhcServiceRequestAuditEvents")]
public class EhcServiceRequestAuditEvent : TenantEntity
{
    [Required]
    public Guid ServiceRequestId { get; set; }

    [ForeignKey(nameof(ServiceRequestId))]
    public virtual EhcServiceRequest ServiceRequest { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string EventType { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string? Body { get; set; }

    public bool IsInternal { get; set; } = false;

    public Guid? ActorUserId { get; set; }

    [ForeignKey(nameof(ActorUserId))]
    public virtual ApplicationUser? ActorUser { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? DataJson { get; set; }
}
