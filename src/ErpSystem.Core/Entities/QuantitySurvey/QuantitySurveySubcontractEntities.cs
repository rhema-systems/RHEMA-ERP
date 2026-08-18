using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Services.QuantitySurvey;

namespace ErpSystem.Core.Entities.QuantitySurvey;

public static class QuantitySurveySubcontractStatuses
{
    public const string Draft = "Draft";
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Closed = "Closed";
}

public static class QuantitySurveySubcontractValuationStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Paid = "Paid";
}

[Table("QuantitySurveySubcontracts")]
public sealed class QuantitySurveySubcontract : TenantEntity, IQuantitySurveyWorkflowRecord
{
    public Guid ProjectId { get; set; }
    public Guid ContractId { get; set; }
    public Guid SubcontractorBusinessPartnerId { get; set; }
    public Guid PaymentTermId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? LastMutationClientRequestId { get; set; }
    [StringLength(64)] public string? LastMutationRequestHash { get; set; }
    [Required, StringLength(100)] public string SubcontractNumber { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string Scope { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal SubcontractValue { get; set; }
    [Required, StringLength(10)] public string Currency { get; set; } = string.Empty;
    [Column(TypeName = "decimal(5,2)")] public decimal RetentionPercentage { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    [Required, StringLength(30)] public string Status { get; set; } = QuantitySurveySubcontractStatuses.Draft;
    [Required, StringLength(30)] public string ApprovalStatus { get; set; } = "Draft";
    public Guid ConfigurationProfileId { get; set; }
    public Guid ContractControlsDecisionId { get; set; }
    public Guid ApprovalWorkflowDefinitionId { get; set; }
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    public Guid? WorkflowInstanceId { get; set; }
    public Guid PreparedById { get; set; }
    public DateTime PreparedAt { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [StringLength(2000)] public string? RejectionReason { get; set; }
    public Guid? ClosedById { get; set; }
    public DateTime? ClosedAt { get; set; }
    [StringLength(2000)] public string? ClosureNote { get; set; }
    [StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Project Project { get; set; } = null!;
    public Contract Contract { get; set; } = null!;
    public BusinessPartner SubcontractorBusinessPartner { get; set; } = null!;
    public ICollection<QuantitySurveySubcontractValuation> Valuations { get; set; } = new List<QuantitySurveySubcontractValuation>();
    public ICollection<QuantitySurveySubcontractEvidence> Evidence { get; set; } = new List<QuantitySurveySubcontractEvidence>();
    public ICollection<QuantitySurveySubcontractChargeNotice> ChargeNotices { get; set; } = new List<QuantitySurveySubcontractChargeNotice>();
}

[Table("QuantitySurveySubcontractValuations")]
public sealed class QuantitySurveySubcontractValuation : TenantEntity, IQuantitySurveyWorkflowRecord
{
    public Guid SubcontractId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? LastMutationClientRequestId { get; set; }
    [StringLength(64)] public string? LastMutationRequestHash { get; set; }
    [Required, StringLength(100)] public string ValuationNumber { get; set; } = string.Empty;
    public DateTime ValuationDate { get; set; }
    public DateTime PeriodEndDate { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal ClaimedToDateAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? AssessedToDateAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal PreviouslyCertifiedAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CurrentCertifiedAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal RetentionHeldAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal RetentionReleasedAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal ApprovedBackChargeAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal ApprovedContraChargeAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TaxAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal NetCertifiedAmount { get; set; }
    public bool IsFinal { get; set; }
    [Required, StringLength(30)] public string Status { get; set; } = QuantitySurveySubcontractValuationStatuses.Draft;
    [Required, StringLength(30)] public string ApprovalStatus { get; set; } = "Draft";
    [StringLength(2000)] public string? SubmissionNote { get; set; }
    [StringLength(2000)] public string? AssessmentNote { get; set; }
    public Guid ConfigurationProfileId { get; set; }
    public Guid ValuationDecisionId { get; set; }
    public Guid ApprovalWorkflowDefinitionId { get; set; }
    public Guid EvidenceMetadataTemplateId { get; set; }
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? SubmittedById { get; set; }
    public Guid? SubmittedBusinessPartnerId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? AssessedById { get; set; }
    public DateTime? AssessedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [StringLength(2000)] public string? RejectionReason { get; set; }
    public Guid? PaymentCertificateId { get; set; }
    [StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public QuantitySurveySubcontract Subcontract { get; set; } = null!;
    public ProjectPaymentCertificate? PaymentCertificate { get; set; }
    public ICollection<QuantitySurveySubcontractEvidence> Evidence { get; set; } = new List<QuantitySurveySubcontractEvidence>();
    public ICollection<QuantitySurveySubcontractChargeNotice> ChargeNotices { get; set; } = new List<QuantitySurveySubcontractChargeNotice>();
}

[Table("QuantitySurveySubcontractEvidence")]
public sealed class QuantitySurveySubcontractEvidence : TenantEntity
{
    public Guid SubcontractId { get; set; }
    public Guid? ValuationId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    [Required, StringLength(30)] public string EvidenceType { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(260)] public string OriginalFileName { get; set; } = string.Empty;
    [Required, StringLength(150)] public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    [Required, StringLength(64)] public string ChecksumSha256 { get; set; } = string.Empty;
    public Guid FileUploadRecordId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public QuantitySurveySubcontract Subcontract { get; set; } = null!;
    public QuantitySurveySubcontractValuation? Valuation { get; set; }
}

[Table("QuantitySurveySubcontractRevisions")]
public sealed class QuantitySurveySubcontractRevision : TenantEntity
{
    public Guid SubcontractId { get; set; }
    public Guid? ValuationId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public Guid? ActorBusinessPartnerId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(2000)] public string? Reason { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? BeforeJson { get; set; }
    [Required, Column(TypeName = "nvarchar(max)")] public string AfterJson { get; set; } = string.Empty;
    public QuantitySurveySubcontract Subcontract { get; set; } = null!;
    public QuantitySurveySubcontractValuation? Valuation { get; set; }
}
