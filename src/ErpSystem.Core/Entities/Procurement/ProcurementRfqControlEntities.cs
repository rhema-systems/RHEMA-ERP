using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementRfqReceipts")]
public sealed class ProcurementRfqReceipt : TenantEntity
{
    public Guid RfqId { get; set; }
    public Guid QuoteId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    [Range(1, int.MaxValue)] public int ReceiptSequence { get; set; }
    [Required, StringLength(100)] public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime SubmissionDeadlineUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public ProcurementRfqReceiptDisposition Disposition { get; set; }
    public Guid SealedByUserId { get; set; }
    public DateTime SealedAtUtc { get; set; }
    public Guid? OpeningRegisterId { get; set; }
    public DateTime? OpenedAtUtc { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string SubmissionSnapshotJson { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public RequestForQuotation Rfq { get; set; } = null!;
    public RequestForQuotationQuote Quote { get; set; } = null!;
    public BusinessPartner BusinessPartner { get; set; } = null!;
    public ProcurementRfqOpeningRegister? OpeningRegister { get; set; }
}
[Table("ProcurementRfqOpeningRegisters")]
public sealed class ProcurementRfqOpeningRegister : TenantEntity
{
    public Guid RfqId { get; set; }
    public DateTime OpenedAtUtc { get; set; }
    public DateTime ClosedAtUtc { get; set; }
    public Guid OpenedByUserId { get; set; }
    [Required, StringLength(300)] public string OpenedByName { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(max)")] public string ParticipantSnapshotJson { get; set; } = "[]";
    [Column(TypeName = "nvarchar(max)")] public string RegisterSnapshotJson { get; set; } = "[]";
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public RequestForQuotation Rfq { get; set; } = null!;
    public ICollection<ProcurementRfqOpeningParticipant> Participants { get; set; } = new List<ProcurementRfqOpeningParticipant>();
    public ICollection<ProcurementRfqOpeningEntry> Entries { get; set; } = new List<ProcurementRfqOpeningEntry>();
    public ICollection<ProcurementRfqReceipt> Receipts { get; set; } = new List<ProcurementRfqReceipt>();
}

[Table("ProcurementRfqOpeningParticipants")]
public sealed class ProcurementRfqOpeningParticipant : TenantEntity
{
    public Guid OpeningRegisterId { get; set; }
    public Guid? ParticipantUserId { get; set; }
    [Required, StringLength(300)] public string ParticipantName { get; set; } = string.Empty;
    [Required, StringLength(150)] public string RoleName { get; set; } = string.Empty;
    public bool IsObserver { get; set; }
    public DateTime SignedAtUtc { get; set; }
    [Required, StringLength(500)] public string SignatureReference { get; set; } = string.Empty;

    public ProcurementRfqOpeningRegister OpeningRegister { get; set; } = null!;
}

[Table("ProcurementRfqOpeningEntries")]
public sealed class ProcurementRfqOpeningEntry : TenantEntity
{
    public Guid OpeningRegisterId { get; set; }
    public Guid ReceiptId { get; set; }
    public Guid QuoteId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    [Required, StringLength(100)] public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime ReceivedAtUtc { get; set; }
    public ProcurementRfqReceiptDisposition Disposition { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal DeclaredAmount { get; set; }
    [StringLength(300)] public string? SecurityReference { get; set; }
    [StringLength(500)] public string? RejectionReason { get; set; }
    [Required, StringLength(64)] public string QuoteIntegrityHash { get; set; } = string.Empty;

    public ProcurementRfqOpeningRegister OpeningRegister { get; set; } = null!;
    public ProcurementRfqReceipt Receipt { get; set; } = null!;
    public RequestForQuotationQuote Quote { get; set; } = null!;
    public BusinessPartner BusinessPartner { get; set; } = null!;
}

[Table("ProcurementRfqEvaluations")]
public sealed class ProcurementRfqEvaluation : TenantEntity
{
    public bool ApprovalRequired { get; set; } = true;
    public Guid RfqId { get; set; }
    public Guid OpeningRegisterId { get; set; }
    public ProcurementRfqEvaluationStatus Status { get; set; } = ProcurementRfqEvaluationStatus.Draft;
    [Required, StringLength(30)] public string AwardMode { get; set; } = "WinnerTakesAll";
    [Required, StringLength(2000)] public string RecommendationReason { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    public Guid MethodRuleId { get; set; }
    [Required, StringLength(100)] public string MethodRuleCode { get; set; } = string.Empty;
    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    [StringLength(200)] public string? ApprovalReference { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    [StringLength(300)] public string? SubmittedByName { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    [StringLength(300)] public string? ApprovedByName { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string ApprovalActorsJson { get; set; } = "[]";
    [Column(TypeName = "nvarchar(max)")] public string SnapshotJson { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public RequestForQuotation Rfq { get; set; } = null!;
    public ProcurementRfqOpeningRegister OpeningRegister { get; set; } = null!;
    public ProcurementPolicyMethodRule MethodRule { get; set; } = null!;
    public WorkflowInstance? WorkflowInstance { get; set; }
    public ICollection<ProcurementRfqEvaluationLine> Lines { get; set; } = new List<ProcurementRfqEvaluationLine>();
}

[Table("ProcurementRfqEvaluationLines")]
public sealed class ProcurementRfqEvaluationLine : TenantEntity
{
    public Guid EvaluationId { get; set; }
    public Guid RfqItemId { get; set; }
    public Guid QuoteId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal UnitPrice { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal LineTotal { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal TechnicalScore { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal CommercialScore { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal TotalScore { get; set; }
    [StringLength(1000)] public string? RecommendationReason { get; set; }

    public ProcurementRfqEvaluation Evaluation { get; set; } = null!;
    public RequestForQuotationItem RfqItem { get; set; } = null!;
    public RequestForQuotationQuote Quote { get; set; } = null!;
    public BusinessPartner BusinessPartner { get; set; } = null!;
}
