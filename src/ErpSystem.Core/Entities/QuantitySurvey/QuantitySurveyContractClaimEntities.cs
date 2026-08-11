using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Services.QuantitySurvey;

namespace ErpSystem.Core.Entities.QuantitySurvey;

public enum QuantitySurveyContractClaimType
{
    ExtensionOfTime,
    LossAndExpense,
    Variation,
    Daywork,
    AdditionalWork,
    Other
}

public static class QuantitySurveyContractClaimStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string Vetted = "Vetted";
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public enum QuantitySurveyClaimDisputeStatus
{
    None,
    Open,
    ResolvedAccepted,
    ResolvedRejected
}

public enum QuantitySurveyClaimSettlementStatus
{
    NotApplicable,
    Pending,
    PartiallySettled,
    Settled
}

[Table("QuantitySurveyContractClaims")]
public sealed class QuantitySurveyContractClaim : TenantEntity, IQuantitySurveyWorkflowRecord
{
    public Guid ProjectId { get; set; }
    public Guid ContractId { get; set; }
    public Guid ContractorBusinessPartnerId { get; set; }
    public Guid? ApprovedBoqVersionId { get; set; }
    public Guid? VariationOrderId { get; set; }
    public Guid? ExtensionOfTimeId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? LastMutationClientRequestId { get; set; }
    [StringLength(64)] public string? LastMutationRequestHash { get; set; }
    [Required, StringLength(100)] public string ClaimNumber { get; set; } = string.Empty;
    public QuantitySurveyContractClaimType ClaimType { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string Basis { get; set; } = string.Empty;
    [Required, StringLength(30)] public string Status { get; set; } = QuantitySurveyContractClaimStatuses.Draft;
    [Required, StringLength(30)] public string ApprovalStatus { get; set; } = "Draft";
    [Required, StringLength(10)] public string Currency { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal ClaimedAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? QsAssessedAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? ApprovedAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? RejectedAmount { get; set; }
    [StringLength(2000)] public string? QsReviewNote { get; set; }
    public QuantitySurveyClaimDisputeStatus DisputeStatus { get; set; }
    [StringLength(2000)] public string? DisputeReason { get; set; }
    [StringLength(2000)] public string? DisputeResolution { get; set; }
    public QuantitySurveyClaimSettlementStatus SettlementStatus { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal SettledAmount { get; set; }
    [StringLength(100)] public string? SettlementReference { get; set; }
    public DateTime? SettlementDate { get; set; }
    public Guid ConfigurationProfileId { get; set; }
    public Guid VariationDecisionId { get; set; }
    public Guid ApprovalWorkflowDefinitionId { get; set; }
    public Guid EvidenceMetadataTemplateId { get; set; }
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? SubmittedById { get; set; }
    public Guid SubmittedBusinessPartnerId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? QsVettedById { get; set; }
    public DateTime? QsVettedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [StringLength(2000)] public string? RejectionReason { get; set; }
    [StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Project Project { get; set; } = null!;
    public Contract Contract { get; set; } = null!;
    public BusinessPartner ContractorBusinessPartner { get; set; } = null!;
    public ProjectBoqVersion? ApprovedBoqVersion { get; set; }
    public ProjectVariationOrder? VariationOrder { get; set; }
    public ProjectExtensionOfTime? ExtensionOfTime { get; set; }
    public ICollection<QuantitySurveyContractClaimEvidence> Evidence { get; set; } = new List<QuantitySurveyContractClaimEvidence>();
    public ICollection<QuantitySurveyContractClaimRevision> Revisions { get; set; } = new List<QuantitySurveyContractClaimRevision>();
}

[Table("QuantitySurveyContractClaimEvidence")]
public sealed class QuantitySurveyContractClaimEvidence : TenantEntity
{
    public Guid ContractClaimId { get; set; }
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
    public QuantitySurveyContractClaim ContractClaim { get; set; } = null!;
}

[Table("QuantitySurveyContractClaimRevisions")]
public sealed class QuantitySurveyContractClaimRevision : TenantEntity
{
    public Guid ContractClaimId { get; set; }
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
    public QuantitySurveyContractClaim ContractClaim { get; set; } = null!;
}
