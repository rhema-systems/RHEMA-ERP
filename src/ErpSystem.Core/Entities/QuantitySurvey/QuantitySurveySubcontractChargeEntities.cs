using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Services.QuantitySurvey;

namespace ErpSystem.Core.Entities.QuantitySurvey;

public static class QuantitySurveySubcontractChargeTypes
{
    public const string BackCharge = "BackCharge";
    public const string ContraCharge = "ContraCharge";
}

public static class QuantitySurveySubcontractChargeStatuses
{
    public const string Draft = "Draft";
    public const string Issued = "Issued";
    public const string Responded = "Responded";
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Allocated = "Allocated";
    public const string Applied = "Applied";
}

public static class QuantitySurveySubcontractChargeResponses
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Disputed = "Disputed";
    public const string NoResponse = "NoResponse";
}

[Table("QuantitySurveySubcontractChargeNotices")]
public sealed class QuantitySurveySubcontractChargeNotice : TenantEntity, IQuantitySurveyWorkflowRecord
{
    public Guid SubcontractId { get; set; }
    public Guid? AppliedValuationId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? LastMutationClientRequestId { get; set; }
    [StringLength(64)] public string? LastMutationRequestHash { get; set; }
    [Required, StringLength(100)] public string NoticeNumber { get; set; } = string.Empty;
    [Required, StringLength(20)] public string ChargeType { get; set; } = QuantitySurveySubcontractChargeTypes.BackCharge;
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string Reason { get; set; } = string.Empty;
    public DateTime NoticeDate { get; set; }
    public DateTime ResponseDueDate { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal ProposedAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? ApprovedAmount { get; set; }
    [Required, StringLength(10)] public string Currency { get; set; } = string.Empty;
    [Required, StringLength(30)] public string Status { get; set; } = QuantitySurveySubcontractChargeStatuses.Draft;
    [Required, StringLength(30)] public string ApprovalStatus { get; set; } = "Draft";
    [Required, StringLength(30)] public string ResponseStatus { get; set; } = QuantitySurveySubcontractChargeResponses.Pending;
    [StringLength(2000)] public string? ResponseNote { get; set; }
    public Guid? RespondedByBusinessPartnerId { get; set; }
    public DateTime? RespondedAt { get; set; }
    public Guid ConfigurationProfileId { get; set; }
    public Guid ContractControlsDecisionId { get; set; }
    public Guid ApprovalWorkflowDefinitionId { get; set; }
    public Guid EvidenceMetadataTemplateId { get; set; }
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    public Guid? WorkflowInstanceId { get; set; }
    public Guid PreparedById { get; set; }
    public DateTime PreparedAt { get; set; }
    public Guid? IssuedById { get; set; }
    public DateTime? IssuedAt { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [StringLength(2000)] public string? RejectionReason { get; set; }
    public DateTime? AllocatedAt { get; set; }
    public DateTime? AppliedAt { get; set; }
    [Required, StringLength(30)] public string CommunicationStatus { get; set; } = "NotRequested";
    public DateTime? CommunicationRequestedAt { get; set; }
    public int CommunicationRequestCount { get; set; }
    [StringLength(150)] public string? LastCommunicationTopic { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public QuantitySurveySubcontract Subcontract { get; set; } = null!;
    public QuantitySurveySubcontractValuation? AppliedValuation { get; set; }
    public BusinessPartner? RespondedByBusinessPartner { get; set; }
    public ICollection<QuantitySurveySubcontractChargeEvidence> Evidence { get; set; } = new List<QuantitySurveySubcontractChargeEvidence>();
    public ICollection<QuantitySurveySubcontractChargeRevision> Revisions { get; set; } = new List<QuantitySurveySubcontractChargeRevision>();
}

[Table("QuantitySurveySubcontractChargeEvidence")]
public sealed class QuantitySurveySubcontractChargeEvidence : TenantEntity
{
    public Guid ChargeNoticeId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(260)] public string OriginalFileName { get; set; } = string.Empty;
    [Required, StringLength(150)] public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    [Required, StringLength(64)] public string ChecksumSha256 { get; set; } = string.Empty;
    public Guid FileUploadRecordId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public QuantitySurveySubcontractChargeNotice ChargeNotice { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
}

[Table("QuantitySurveySubcontractChargeRevisions")]
public sealed class QuantitySurveySubcontractChargeRevision : TenantEntity
{
    public Guid ChargeNoticeId { get; set; }
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
    public QuantitySurveySubcontractChargeNotice ChargeNotice { get; set; } = null!;
}
