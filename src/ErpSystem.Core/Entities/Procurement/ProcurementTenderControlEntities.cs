using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementTenderControls")]
public sealed class ProcurementTenderControl : TenantEntity
{
    public bool ApprovalRequired { get; set; } = true;
    public Guid TenderId { get; set; }
    public Guid SourcingCaseId { get; set; }
    public Guid MethodRuleId { get; set; }
    public Guid AuthorityRouteId { get; set; }
    public ProcurementMethodType Method { get; set; }
    [Required, StringLength(100)] public string MethodRuleCode { get; set; } = string.Empty;
    [Required, StringLength(100)] public string AuthorityRouteReference { get; set; } = string.Empty;
    public ProcurementTenderControlStatus Status { get; set; }

    [Required, StringLength(200)] public string AdvertisementReference { get; set; } = string.Empty;
    [Required, StringLength(200)] public string PublicationChannel { get; set; } = string.Empty;
    [Required, StringLength(200)] public string TenderDocumentReference { get; set; } = string.Empty;
    [Required, StringLength(100)] public string TenderDocumentVersion { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal DocumentFee { get; set; }
    [Required, StringLength(500)] public string AdvertisementEvidenceReference { get; set; } = string.Empty;
    public DateTime AdvertisedAtUtc { get; set; }
    public DateTime SubmissionDeadlineUtc { get; set; }
    public DateTime OpeningScheduledAtUtc { get; set; }

    public DateTime? OpenedAtUtc { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? OpeningSnapshotJson { get; set; }
    [StringLength(64)] public string? OpeningIntegrityHash { get; set; }
    [StringLength(500)] public string? OpeningEvidenceReference { get; set; }

    public DateTime? TechnicalEvaluatedAtUtc { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? TechnicalEvaluationSnapshotJson { get; set; }
    [StringLength(64)] public string? TechnicalEvaluationIntegrityHash { get; set; }
    [StringLength(500)] public string? TechnicalEvaluationEvidenceReference { get; set; }

    public DateTime? FinancialEvaluatedAtUtc { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? FinancialEvaluationSnapshotJson { get; set; }
    [StringLength(64)] public string? FinancialEvaluationIntegrityHash { get; set; }
    [StringLength(500)] public string? FinancialEvaluationEvidenceReference { get; set; }
    public Guid? RecommendedBidId { get; set; }

    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    [StringLength(300)] public string? AuthorityApprovalReference { get; set; }
    [StringLength(300)] public string? PpaApprovalReference { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string ApprovalActorsJson { get; set; } = "[]";
    public DateTime? SubmittedForApprovalAtUtc { get; set; }
    public Guid? SubmittedForApprovalById { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? ApprovedById { get; set; }

    public Guid? AwardBidId { get; set; }
    [StringLength(200)] public string? AwardReference { get; set; }
    [StringLength(500)] public string? AwardEvidenceReference { get; set; }
    public DateTime? AwardedAtUtc { get; set; }

    [StringLength(200)] public string? ContractReference { get; set; }
    [StringLength(500)] public string? ContractEvidenceReference { get; set; }
    public DateTime? ContractedAtUtc { get; set; }

    [StringLength(300)] public string? BidderAcceptanceReference { get; set; }
    [StringLength(500)] public string? BidderAcceptanceEvidenceReference { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }

    [Column(TypeName = "nvarchar(max)")] public string LifecycleSnapshotJson { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Tender Tender { get; set; } = null!;
    public ProcurementSourcingCase SourcingCase { get; set; } = null!;
    public ProcurementPolicyMethodRule MethodRule { get; set; } = null!;
    public ProcurementRequisitionAuthorityRoute AuthorityRoute { get; set; } = null!;
    public WorkflowDefinition? WorkflowDefinition { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }
    public ICollection<ProcurementTenderDocumentIssue> DocumentIssues { get; set; } = new List<ProcurementTenderDocumentIssue>();
    public ICollection<ProcurementTenderSubmissionReceipt> SubmissionReceipts { get; set; } = new List<ProcurementTenderSubmissionReceipt>();
}

[Table("ProcurementTenderDocumentIssues")]
public sealed class ProcurementTenderDocumentIssue : TenantEntity
{
    public Guid TenderControlId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    [Required, StringLength(300)] public string RecipientName { get; set; } = string.Empty;
    [StringLength(320)] public string? RecipientEmail { get; set; }
    [StringLength(30)] public string? RecipientPhone { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal AmountPaid { get; set; }
    [StringLength(200)] public string? PaymentReference { get; set; }
    [Required, StringLength(200)] public string IssueReceiptNumber { get; set; } = string.Empty;
    public DateTime IssuedAtUtc { get; set; }
    public Guid IssuedByUserId { get; set; }
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementTenderControl TenderControl { get; set; } = null!;
    public BusinessPartner? BusinessPartner { get; set; }
}

[Table("ProcurementTenderSubmissionReceipts")]
public sealed class ProcurementTenderSubmissionReceipt : TenantEntity
{
    public Guid TenderControlId { get; set; }
    public Guid TenderBidId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    [Required, StringLength(200)] public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime SubmissionDeadlineUtc { get; set; }
    public ProcurementTenderSubmissionDisposition Disposition { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string SealedSnapshotJson { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    public DateTime? OpenedAtUtc { get; set; }

    public ProcurementTenderControl TenderControl { get; set; } = null!;
    public TenderBid TenderBid { get; set; } = null!;
    public BusinessPartner BusinessPartner { get; set; } = null!;
}
