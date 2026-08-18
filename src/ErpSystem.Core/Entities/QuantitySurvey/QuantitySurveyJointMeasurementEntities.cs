using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Services.QuantitySurvey;

namespace ErpSystem.Core.Entities.QuantitySurvey;

public static class QuantitySurveyJointMeasurementStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string Scheduled = "Scheduled";
    public const string AwaitingAttendance = "AwaitingAttendance";
    public const string AwaitingEndorsements = "AwaitingEndorsements";
    public const string ReadyForReview = "ReadyForReview";
    public const string PendingApproval = "PendingApproval";
    public const string ApprovedPendingBoqRevision = "ApprovedPendingBoqRevision";
    public const string BoqWorkflowPending = "BoqWorkflowPending";
    public const string Applied = "Applied";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
}

public static class QuantitySurveyJointMeasurementParticipantTypes
{
    public const string Contractor = "Contractor";
    public const string Consultant = "Consultant";
    public const string InternalRole = "InternalRole";
}

public enum QuantitySurveyJointMeasurementEvidenceType
{
    RequestEvidence = 0,
    SitePhoto = 1,
    AttendanceRecord = 2,
    SignedMeasurementRecord = 3,
    SupportingDocument = 4
}

[Table("QuantitySurveyJointMeasurementRequests")]
public sealed class QuantitySurveyJointMeasurementRequest : TenantEntity, IQuantitySurveyWorkflowRecord
{
    public Guid ProjectId { get; set; }
    public Guid ProjectBoqVersionId { get; set; }
    public Guid ProjectBoqVersionLineId { get; set; }
    public Guid BoqLineKey { get; set; }
    public Guid ContractorBusinessPartnerId { get; set; }
    public Guid? ConsultantBusinessPartnerId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? LastMutationClientRequestId { get; set; }
    [StringLength(64)] public string? LastMutationRequestHash { get; set; }
    [Required, StringLength(50)] public string RequestNumber { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string Reason { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")] public decimal PreviousQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal? ContractorProposedQuantity { get; set; }
    [StringLength(300)] public string? RequestedSiteLocation { get; set; }
    public DateTime? PreferredStartAt { get; set; }
    public DateTime? PreferredEndAt { get; set; }
    [Required, StringLength(30)] public string Status { get; set; } = QuantitySurveyJointMeasurementStatuses.Draft;
    [Required, StringLength(30)] public string ApprovalStatus { get; set; } = "Draft";
    public Guid ConfigurationProfileId { get; set; }
    public Guid MeasurementDecisionId { get; set; }
    public Guid ExternalSubmissionDecisionId { get; set; }
    public Guid ApprovalWorkflowDefinitionId { get; set; }
    public Guid EvidenceMetadataTemplateId { get; set; }
    [Required, StringLength(80)] public string EvidenceMetadataTemplateCodeSnapshot { get; set; } = string.Empty;
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string JointAttendanceRoleIdsJson { get; set; } = "[]";
    [Required, StringLength(2000)] public string ConsultantRoleIdsJson { get; set; } = "[]";
    public bool ContractorSignatureRequired { get; set; }
    public bool ConsultantSignatureRequired { get; set; }
    public bool PortalIdentityRequired { get; set; }
    public bool EvidenceRequired { get; set; }
    public bool ExternalSignatureRequired { get; set; }
    public Guid RequestedByUserId { get; set; }
    [Required, StringLength(300)] public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ScheduledStartAt { get; set; }
    public DateTime? ScheduledEndAt { get; set; }
    [StringLength(300)] public string? ScheduledSiteLocation { get; set; }
    public Guid? ScheduledByUserId { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public Guid? MeasurementSheetId { get; set; }
    public DateTime? MeasurementLinkedAt { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [StringLength(2000)] public string? RejectionReason { get; set; }
    public Guid? RemeasurementVersionId { get; set; }
    public Guid RemeasurementClientRequestId { get; set; }
    public DateTime? AppliedAt { get; set; }
    [Required, StringLength(100)] public string AuditAction { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Project Project { get; set; } = null!;
    public ProjectBoqVersion ProjectBoqVersion { get; set; } = null!;
    public ProjectBoqVersionLine ProjectBoqVersionLine { get; set; } = null!;
    public BusinessPartner ContractorBusinessPartner { get; set; } = null!;
    public BusinessPartner? ConsultantBusinessPartner { get; set; }
    public QuantitySurveyConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public QuantitySurveyConfigurationDecision MeasurementDecision { get; set; } = null!;
    public QuantitySurveyConfigurationDecision ExternalSubmissionDecision { get; set; } = null!;
    public CentralDocumentMetadataTemplate EvidenceMetadataTemplate { get; set; } = null!;
    public QuantitySurveyMeasurementSheet? MeasurementSheet { get; set; }
    public ProjectBoqVersion? RemeasurementVersion { get; set; }
    public ICollection<QuantitySurveyJointMeasurementParticipant> Participants { get; set; } = [];
    public ICollection<QuantitySurveyJointMeasurementEvidence> Evidence { get; set; } = [];
    public ICollection<QuantitySurveyJointMeasurementEndorsement> Endorsements { get; set; } = [];
    public ICollection<QuantitySurveyJointMeasurementRevision> Revisions { get; set; } = [];
}

[Table("QuantitySurveyJointMeasurementParticipants")]
public sealed class QuantitySurveyJointMeasurementParticipant : TenantEntity
{
    public Guid RequestId { get; set; }
    [Required, StringLength(30)] public string ParticipantType { get; set; } = string.Empty;
    public Guid? BusinessPartnerId { get; set; }
    public Guid? RequiredRoleId { get; set; }
    [StringLength(200)] public string? RequiredRoleNameSnapshot { get; set; }
    public bool IsRequired { get; set; } = true;
    [Required, StringLength(30)] public string AttendanceStatus { get; set; } = "Invited";
    public Guid? AttendedByUserId { get; set; }
    [StringLength(300)] public string? AttendedByName { get; set; }
    public DateTime? AttendedAt { get; set; }
    [StringLength(1000)] public string? AttendanceNotes { get; set; }
    [StringLength(64)] public string? AttendanceHash { get; set; }

    public QuantitySurveyJointMeasurementRequest Request { get; set; } = null!;
    public BusinessPartner? BusinessPartner { get; set; }
}

[Table("QuantitySurveyJointMeasurementEndorsements")]
public sealed class QuantitySurveyJointMeasurementEndorsement : TenantEntity
{
    public Guid RequestId { get; set; }
    public Guid ParticipantId { get; set; }
    [Required, StringLength(30)] public string SignerType { get; set; } = string.Empty;
    public Guid? BusinessPartnerId { get; set; }
    public Guid SignedByUserId { get; set; }
    [Required, StringLength(300)] public string SignedByName { get; set; } = string.Empty;
    [Required, StringLength(30)] public string SignatureMethod { get; set; } = "Attestation";
    [Required, StringLength(1000)] public string AttestationSnapshot { get; set; } = string.Empty;
    [StringLength(200)] public string? CertificateThumbprint { get; set; }
    [StringLength(300)] public string? ExternalSignatureReference { get; set; }
    [Required, StringLength(64)] public string SignatureHash { get; set; } = string.Empty;
    [StringLength(2000)] public string? Notes { get; set; }
    public DateTime SignedAt { get; set; }

    public QuantitySurveyJointMeasurementRequest Request { get; set; } = null!;
    public QuantitySurveyJointMeasurementParticipant Participant { get; set; } = null!;
    public BusinessPartner? BusinessPartner { get; set; }
}

[Table("QuantitySurveyJointMeasurementEvidence")]
public sealed class QuantitySurveyJointMeasurementEvidence : TenantEntity
{
    public Guid RequestId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public QuantitySurveyJointMeasurementEvidenceType EvidenceType { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(260)] public string OriginalFileName { get; set; } = string.Empty;
    [Required, StringLength(150)] public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    [Required, StringLength(64)] public string ChecksumSha256 { get; set; } = string.Empty;
    public Guid FileUploadRecordId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public Guid UploadedByUserId { get; set; }
    [Required, StringLength(300)] public string UploadedByName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }

    public QuantitySurveyJointMeasurementRequest Request { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
}

[Table("QuantitySurveyJointMeasurementRevisions")]
public sealed class QuantitySurveyJointMeasurementRevision : TenantEntity
{
    public Guid RequestId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public Guid? ActorBusinessPartnerId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(2000)] public string? Reason { get; set; }
    public string? BeforeJson { get; set; }
    [Required] public string AfterJson { get; set; } = string.Empty;

    public QuantitySurveyJointMeasurementRequest Request { get; set; } = null!;
}
