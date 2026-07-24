using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementExceptionalSourcingControls")]
public sealed class ProcurementExceptionalSourcingControl : TenantEntity
{
    public Guid TenderId { get; set; }
    public Guid SourcingCaseId { get; set; }
    public Guid MethodRuleId { get; set; }
    public Guid ExceptionRuleId { get; set; }
    public Guid AuthorityRouteId { get; set; }
    public ProcurementMethodType Method { get; set; }
    [Required, StringLength(100)] public string MethodRuleCode { get; set; } = string.Empty;
    [Required, StringLength(100)] public string ExceptionRuleCode { get; set; } = string.Empty;
    [Required, StringLength(100)] public string AuthorityRouteReference { get; set; } = string.Empty;
    public ProcurementExceptionalSourcingControlStatus Status { get; set; }

    [Required, StringLength(2000)] public string Justification { get; set; } = string.Empty;
    [Required, StringLength(500)] public string JustificationEvidenceReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string SupplierSelectionEvidenceReference { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(max)")] public string SupplierSnapshotJson { get; set; } = "[]";
    [Column(TypeName = "nvarchar(max)")] public string EvidenceChecklistJson { get; set; } = "[]";
    public DateTime PreparedAtUtc { get; set; }
    public Guid PreparedById { get; set; }
    public DateTime? SuppliersInvitedAtUtc { get; set; }

    public bool BoardApprovalRequired { get; set; }
    public bool ManagingDirectorApprovalRequired { get; set; }
    public bool PpaApprovalRequired { get; set; } = true;
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    [StringLength(300)] public string? BoardApprovalReference { get; set; }
    [StringLength(300)] public string? ManagingDirectorApprovalReference { get; set; }
    [StringLength(300)] public string? PpaApprovalReference { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string ApprovalActorsJson { get; set; } = "[]";
    public DateTime? SubmittedForApprovalAtUtc { get; set; }
    public Guid? SubmittedForApprovalById { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? ApprovedById { get; set; }

    public Guid? NegotiationId { get; set; }
    [StringLength(300)] public string? NegotiationPlanReference { get; set; }
    [StringLength(500)] public string? NegotiationMinutesEvidenceReference { get; set; }
    [StringLength(300)] public string? NegotiationOutcomeReference { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? NegotiatedAmount { get; set; }
    public DateTime? NegotiatedAtUtc { get; set; }

    public Guid? RecommendedBidId { get; set; }
    [StringLength(2000)] public string? RecommendationReason { get; set; }
    [StringLength(500)] public string? RecommendationEvidenceReference { get; set; }
    public DateTime? RecommendedAtUtc { get; set; }
    public Guid? RecommendedById { get; set; }

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

    [StringLength(300)] public string? PostAwardFilingReference { get; set; }
    [StringLength(500)] public string? PostAwardFilingEvidenceReference { get; set; }
    [StringLength(300)] public string? ExceptionReportReference { get; set; }
    [StringLength(500)] public string? ExceptionReportEvidenceReference { get; set; }
    public DateTime? FiledAtUtc { get; set; }
    public Guid? FiledById { get; set; }

    [Column(TypeName = "nvarchar(max)")] public string LifecycleSnapshotJson { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Tender Tender { get; set; } = null!;
    public ProcurementSourcingCase SourcingCase { get; set; } = null!;
    public ProcurementPolicyMethodRule MethodRule { get; set; } = null!;
    public ProcurementPolicyExceptionRule ExceptionRule { get; set; } = null!;
    public ProcurementRequisitionAuthorityRoute AuthorityRoute { get; set; } = null!;
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public WorkflowInstance? WorkflowInstance { get; set; }
    public TenderNegotiation? Negotiation { get; set; }
}
