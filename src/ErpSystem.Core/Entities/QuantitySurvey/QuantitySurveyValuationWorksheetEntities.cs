using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Services.QuantitySurvey;

namespace ErpSystem.Core.Entities.QuantitySurvey;

public static class QuantitySurveyValuationWorkflowStatuses
{
    public const string Draft = "Draft";
    public const string ContractorSubmitted = "ContractorSubmitted";
    public const string UnderQsReview = "UnderQsReview";
    public const string QsVetted = "QsVetted";
    public const string ConsultantEndorsed = "ConsultantEndorsed";
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public enum QuantitySurveyValuationEvidenceType
{
    ContractorClaim = 0,
    MeasurementSupport = 1,
    SiteRecord = 2,
    ConsultantReview = 3,
    SupportingDocument = 4,
    MaterialOnSite = 5,
    MaterialOffSite = 6
}

[Table("QuantitySurveyValuationWorksheets")]
public sealed class QuantitySurveyValuationWorksheet : TenantEntity, IQuantitySurveyWorkflowRecord
{
    public Guid ProjectId { get; set; }
    public Guid ProjectInterimValuationId { get; set; }
    public Guid ProjectBoqVersionId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? LastMutationClientRequestId { get; set; }
    [StringLength(64)] public string? LastMutationRequestHash { get; set; }
    [Required, StringLength(30)] public string Status { get; set; } = QuantitySurveyValuationWorkflowStatuses.Draft;
    [Required, StringLength(30)] public string ApprovalStatus { get; set; } = "Draft";
    public Guid? ContractorBusinessPartnerId { get; set; }
    public Guid? ConsultantBusinessPartnerId { get; set; }
    public Guid? ConfigurationProfileId { get; set; }
    public Guid? ValuationDecisionId { get; set; }
    public Guid? ExternalSubmissionDecisionId { get; set; }
    public Guid? ApprovalWorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? EvidenceMetadataTemplateId { get; set; }
    [StringLength(80)] public string? EvidenceMetadataTemplateCodeSnapshot { get; set; }
    [StringLength(64)] public string? PolicyHash { get; set; }
    public bool ContractorSubmissionRequired { get; set; }
    public bool ConsultantEndorsementRequired { get; set; }
    public bool SupportingEvidenceRequired { get; set; }
    public bool PortalIdentityRequired { get; set; }
    public bool ExternalSignatureRequired { get; set; }
    [Column(TypeName = "decimal(9,4)")] public decimal RetentionPercentage { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal MeasuredToDateValue { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal PreviouslyCertifiedValue { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CurrentClaimedValue { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CurrentCertifiedValue { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CurrentPeriodCertifiedValue { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal DisputedValue { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal RetentionToDateValue { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CurrentRetentionValue { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal NetCurrentValue { get; set; }
    public int LineCount { get; set; }
    public Guid PreparedById { get; set; }
    [Required, StringLength(300)] public string PreparedByName { get; set; } = string.Empty;
    public DateTime PreparedAt { get; set; }
    public Guid? ContractorSubmittedById { get; set; }
    [StringLength(300)] public string? ContractorSubmittedByName { get; set; }
    public DateTime? ContractorSubmittedAt { get; set; }
    [StringLength(1000)] public string? ContractorAttestation { get; set; }
    [StringLength(64)] public string? ContractorSignatureHash { get; set; }
    public Guid? QsVettedById { get; set; }
    public DateTime? QsVettedAt { get; set; }
    [StringLength(2000)] public string? QsReviewNote { get; set; }
    public Guid? ConsultantEndorsedById { get; set; }
    [StringLength(300)] public string? ConsultantEndorsedByName { get; set; }
    public DateTime? ConsultantEndorsedAt { get; set; }
    [StringLength(1000)] public string? ConsultantAttestation { get; set; }
    [StringLength(64)] public string? ConsultantSignatureHash { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [StringLength(2000)] public string? RejectionReason { get; set; }
    public bool CertificateReady { get; set; }
    public DateTime? CertificateReadyAt { get; set; }
    [Required, StringLength(100)] public string AuditAction { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Project Project { get; set; } = null!;
    public ProjectInterimValuation ProjectInterimValuation { get; set; } = null!;
    public ProjectBoqVersion ProjectBoqVersion { get; set; } = null!;
    public BusinessPartner? ContractorBusinessPartner { get; set; }
    public BusinessPartner? ConsultantBusinessPartner { get; set; }
    public QuantitySurveyConfigurationProfile? ConfigurationProfile { get; set; }
    public QuantitySurveyConfigurationDecision? ValuationDecision { get; set; }
    public QuantitySurveyConfigurationDecision? ExternalSubmissionDecision { get; set; }
    public CentralDocumentMetadataTemplate? EvidenceMetadataTemplate { get; set; }
    public ICollection<QuantitySurveyValuationWorksheetLine> Lines { get; set; } = new List<QuantitySurveyValuationWorksheetLine>();
    public ICollection<QuantitySurveyValuationWorksheetEvidence> Evidence { get; set; } = new List<QuantitySurveyValuationWorksheetEvidence>();
}

[Table("QuantitySurveyValuationWorksheetLines")]
public sealed class QuantitySurveyValuationWorksheetLine : TenantEntity
{
    public Guid WorksheetId { get; set; }
    public Guid ProjectBoqVersionLineId { get; set; }
    public Guid BoqLineKey { get; set; }
    public int Sequence { get; set; }
    [StringLength(50)] public string? LineNumberSnapshot { get; set; }
    [StringLength(50)] public string? ItemCodeSnapshot { get; set; }
    [Required, StringLength(1000)] public string DescriptionSnapshot { get; set; } = string.Empty;
    [StringLength(20)] public string? UnitOfMeasureSnapshot { get; set; }
    [Required, StringLength(10)] public string CurrencySnapshot { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")] public decimal BoqQuantitySnapshot { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal UnitRateSnapshot { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal MeasuredToDateQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal PreviouslyCertifiedQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal CurrentClaimedQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal CurrentCertifiedQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal DisputedQuantity { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal MeasuredToDateValue { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal PreviouslyCertifiedValue { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CurrentClaimedValue { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CurrentCertifiedValue { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CurrentPeriodCertifiedValue { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal DisputedValue { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal PreviousRetentionValue { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal RetentionToDateValue { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CurrentRetentionValue { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal NetCurrentValue { get; set; }
    [StringLength(1000)] public string? ReviewNote { get; set; }

    public QuantitySurveyValuationWorksheet Worksheet { get; set; } = null!;
    public ProjectBoqVersionLine ProjectBoqVersionLine { get; set; } = null!;
}

[Table("QuantitySurveyValuationWorksheetRevisions")]
public sealed class QuantitySurveyValuationWorksheetRevision : TenantEntity
{
    public Guid WorksheetId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public Guid? ActorBusinessPartnerId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(max)")] public string? BeforeJson { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string AfterJson { get; set; } = string.Empty;

    public QuantitySurveyValuationWorksheet Worksheet { get; set; } = null!;
}

[Table("QuantitySurveyValuationWorksheetEvidence")]
public sealed class QuantitySurveyValuationWorksheetEvidence : TenantEntity
{
    public Guid WorksheetId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public QuantitySurveyValuationEvidenceType EvidenceType { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(260)] public string OriginalFileName { get; set; } = string.Empty;
    [Required, StringLength(150)] public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    [Required, StringLength(64)] public string ChecksumSha256 { get; set; } = string.Empty;
    public Guid FileUploadRecordId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public Guid UploadedById { get; set; }
    [Required, StringLength(300)] public string UploadedByName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }

    public QuantitySurveyValuationWorksheet Worksheet { get; set; } = null!;
    public FileUploadRecord FileUploadRecord { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
}
