using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Entities.QuantitySurvey;

public enum QuantitySurveyEscalationDisputeAttachmentType
{
    ContractorSubmission = 0,
    TdcReview = 1,
    ResolutionEvidence = 2
}

public enum QuantitySurveyEscalationDisputeOutcome
{
    Accepted = 0,
    PartiallyAccepted = 1,
    Rejected = 2
}

[Table("QuantitySurveyEscalationDisputes")]
public sealed class QuantitySurveyEscalationDispute : TenantEntity
{
    public Guid CalculationRunId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ContractId { get; set; }
    public Guid ContractorBusinessPartnerId { get; set; }
    [Required, StringLength(200)] public string ContractorNameSnapshot { get; set; } = string.Empty;
    [Required, StringLength(40)] public string DisputeReference { get; set; } = string.Empty;
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    [Required, StringLength(64)] public string CalculationSnapshotHash { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Subject { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string DisputeReason { get; set; } = string.Empty;
    [Required, StringLength(30)] public string Status { get; set; } = "Open";
    public Guid OpenedById { get; set; }
    public DateTime OpenedAt { get; set; }
    public Guid? ContractorResponseClientRequestId { get; set; }
    [StringLength(64)] public string? ContractorResponseHash { get; set; }
    [StringLength(4000)] public string? ContractorResponse { get; set; }
    public Guid? ContractorRespondedById { get; set; }
    public DateTime? ContractorRespondedAt { get; set; }
    public Guid? ResolutionClientRequestId { get; set; }
    [StringLength(64)] public string? ResolutionRequestHash { get; set; }
    public QuantitySurveyEscalationDisputeOutcome? Outcome { get; set; }
    [StringLength(4000)] public string? ResolutionNotes { get; set; }
    public Guid? ResolvedById { get; set; }
    public DateTime? ResolvedAt { get; set; }
    [Required, StringLength(100)] public string AuditAction { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public QuantitySurveyEscalationCalculationRun CalculationRun { get; set; } = null!;
    public Project Project { get; set; } = null!;
    public Contract Contract { get; set; } = null!;
    public BusinessPartner ContractorBusinessPartner { get; set; } = null!;
    public ICollection<QuantitySurveyEscalationDisputeAttachment> Attachments { get; set; } = new List<QuantitySurveyEscalationDisputeAttachment>();
}

[Table("QuantitySurveyEscalationDisputeAttachments")]
public sealed class QuantitySurveyEscalationDisputeAttachment : TenantEntity
{
    public Guid DisputeId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public QuantitySurveyEscalationDisputeAttachmentType AttachmentType { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(260)] public string OriginalFileName { get; set; } = string.Empty;
    [Required, StringLength(120)] public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    [Required, StringLength(64)] public string ChecksumSha256 { get; set; } = string.Empty;
    public Guid FileUploadRecordId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public Guid UploadedById { get; set; }
    [Required, StringLength(300)] public string UploadedByName { get; set; } = string.Empty;

    public QuantitySurveyEscalationDispute Dispute { get; set; } = null!;
    public FileUploadRecord FileUploadRecord { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
}

[Table("QuantitySurveyEscalationDisputeRevisions")]
public sealed class QuantitySurveyEscalationDisputeRevision : TenantEntity
{
    public Guid DisputeId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? BeforeJson { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? AfterJson { get; set; }

    public QuantitySurveyEscalationDispute Dispute { get; set; } = null!;
}
