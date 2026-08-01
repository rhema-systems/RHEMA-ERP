using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementReceiptInspectionCases")]
public sealed class ProcurementReceiptInspectionCase : TenantEntity
{
    public Guid PurchaseOrderReceiptId { get; set; }
    [Range(1, int.MaxValue)] public int Sequence { get; set; }
    public ProcurementReceiptInspectionStatus Status { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public decimal PendingQuantity { get; set; }
    public bool QualityHold { get; set; }
    [StringLength(1000)] public string? QualityHoldReason { get; set; }
    public DateTime? QualityHoldReleasedAtUtc { get; set; }
    [StringLength(100)] public string? RejectionNoteNumber { get; set; }
    public ProcurementReceiptSupplierAcknowledgementStatus SupplierAcknowledgementStatus { get; set; }
    public ProcurementReceiptResolutionKind ResolutionKind { get; set; }
    public ProcurementReceiptResolutionStatus ResolutionStatus { get; set; }
    public Guid? ReplacementPurchaseOrderReceiptId { get; set; }
    public Guid? ReplacementInspectionCaseId { get; set; }
    public DateTime? ReplacementLinkedAtUtc { get; set; }
    public decimal StockEligibleQuantity { get; set; }
    public decimal StockPostedQuantity { get; set; }
    public DateTime? StockPostedAtUtc { get; set; }
    public decimal ApEligibleQuantity { get; set; }
    public decimal ApBlockedQuantity { get; set; }
    public Guid ConfigurationProfileId { get; set; }
    public int ConfigurationProfileVersion { get; set; }
    public Guid PolicySetId { get; set; }
    public int PolicyVersion { get; set; }
    public Guid AuthorityRuleId { get; set; }
    [Required, StringLength(200)] public string AuthorityName { get; set; } = string.Empty;
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid CreatedByUserId { get; set; }
    [Required, StringLength(300)] public string CreatedByName { get; set; } = string.Empty;
    public Guid? SubmittedByUserId { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    [StringLength(1000)] public string? DecisionComment { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Required, Column(TypeName = "nvarchar(max)")] public string SourceSnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string SourceSnapshotHash { get; set; } = string.Empty;
    [Required, Column(TypeName = "nvarchar(max)")] public string DecisionSnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public PurchaseOrderReceipt PurchaseOrderReceipt { get; set; } = null!;
    public PurchaseOrderReceipt? ReplacementPurchaseOrderReceipt { get; set; }
    public ProcurementReceiptInspectionCase? ReplacementInspectionCase { get; set; }
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public WorkflowInstance? WorkflowInstance { get; set; }
    public ICollection<ProcurementReceiptInspectionLine> Lines { get; set; } =
        new List<ProcurementReceiptInspectionLine>();
    public ICollection<ProcurementReceiptInspectionEvidence> Evidence { get; set; } =
        new List<ProcurementReceiptInspectionEvidence>();
    public ICollection<ProcurementReceiptInspectionAction> Actions { get; set; } =
        new List<ProcurementReceiptInspectionAction>();
}

[Table("ProcurementReceiptInspectionLines")]
public sealed class ProcurementReceiptInspectionLine : TenantEntity
{
    public Guid InspectionCaseId { get; set; }
    public Guid PurchaseOrderReceiptItemId { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public decimal PendingQuantity { get; set; }
    public ProcurementReceiptDisposition Disposition { get; set; }
    [StringLength(1000)] public string? RejectionReason { get; set; }
    [StringLength(1000)] public string? InspectionNotes { get; set; }
    public Guid? QuarantineLocationId { get; set; }
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementReceiptInspectionCase InspectionCase { get; set; } = null!;
    public PurchaseOrderReceiptItem PurchaseOrderReceiptItem { get; set; } = null!;
}

[Table("ProcurementReceiptInspectionEvidence")]
public sealed class ProcurementReceiptInspectionEvidence : TenantEntity
{
    public Guid InspectionCaseId { get; set; }
    [Required, StringLength(100)] public string ActionKey { get; set; } = string.Empty;
    [Required, StringLength(200)] public string RequirementKey { get; set; } = string.Empty;
    public ProcurementReceiptInspectionEvidenceKind ReferenceKind { get; set; }
    public Guid? WorkflowEvidenceDocumentId { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    [Required, StringLength(1000)] public string EvidenceReference { get; set; } = string.Empty;
    [Required, StringLength(64)] public string EvidenceHash { get; set; } = string.Empty;

    public ProcurementReceiptInspectionCase InspectionCase { get; set; } = null!;
    public WorkflowEvidenceDocument? WorkflowEvidenceDocument { get; set; }
    public FileUploadRecord? FileUploadRecord { get; set; }
}

[Table("ProcurementReceiptInspectionActions")]
public sealed class ProcurementReceiptInspectionAction : TenantEntity
{
    public Guid InspectionCaseId { get; set; }
    [Range(1, int.MaxValue)] public int Sequence { get; set; }
    public ProcurementReceiptInspectionActionType ActionType { get; set; }
    public ProcurementReceiptInspectionStatus StatusAfter { get; set; }
    public ProcurementReceiptResolutionKind ResolutionKind { get; set; }
    public decimal Quantity { get; set; }
    [Required, StringLength(100)] public string Reference { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Comment { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public Guid? ActorBusinessPartnerId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementReceiptInspectionCase InspectionCase { get; set; } = null!;
}
