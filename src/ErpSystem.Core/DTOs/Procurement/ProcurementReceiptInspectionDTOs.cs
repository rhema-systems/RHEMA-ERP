using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementReceiptInspectionLineRequest
{
    public Guid PurchaseOrderReceiptItemId { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal AcceptedQuantity { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal RejectedQuantity { get; set; }
    [StringLength(1000)] public string? RejectionReason { get; set; }
    [StringLength(1000)] public string? InspectionNotes { get; set; }
    public Guid? QuarantineLocationId { get; set; }
}

public sealed class SaveProcurementReceiptInspectionRequest
{
    [StringLength(1000)] public string? Comment { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    public string? RowVersion { get; set; }
    [MinLength(1)] public List<ProcurementReceiptInspectionLineRequest> Lines { get; set; } = new();
}

public sealed class ProcurementReceiptInspectionEvidenceRequest
{
    [Required, StringLength(100)] public string ActionKey { get; set; } = string.Empty;
    [Required, StringLength(200)] public string RequirementKey { get; set; } = string.Empty;
    public ProcurementReceiptInspectionEvidenceKind ReferenceKind { get; set; }
    public Guid? WorkflowEvidenceDocumentId { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    [Required, StringLength(1000)] public string EvidenceReference { get; set; } = string.Empty;
}

public sealed class SubmitProcurementReceiptInspectionRequest
{
    [StringLength(1000)] public string? Comment { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    [MinLength(1)] public List<ProcurementReceiptInspectionEvidenceRequest> Evidence { get; set; } = new();
}

public sealed class DecideProcurementReceiptInspectionRequest
{
    public bool Approved { get; set; }
    [Required, StringLength(1000)] public string Comment { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementReceiptResolutionRequest
{
    public ProcurementReceiptResolutionKind ResolutionKind { get; set; }
    [Required, StringLength(100)] public string Reference { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Comment { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
    [MinLength(1)] public List<ProcurementReceiptInspectionEvidenceRequest> Evidence { get; set; } = new();
}

public sealed class ProcurementReceiptSupplierAcknowledgementRequest
{
    public bool Acknowledged { get; set; }
    [Required, StringLength(100)] public string Reference { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Comment { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
    public List<ProcurementReceiptInspectionEvidenceRequest> Evidence { get; set; } = new();
}

public sealed class ProcurementReceiptInspectionLineDto
{
    public ItemType LineType { get; set; } = ItemType.StockItem;
    public Guid Id { get; init; }
    public Guid PurchaseOrderReceiptItemId { get; init; }
    public Guid PurchaseOrderItemId { get; init; }
    public string ItemCode { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public string UnitOfMeasure { get; init; } = string.Empty;
    public decimal ReceivedQuantity { get; init; }
    public decimal AcceptedQuantity { get; init; }
    public decimal RejectedQuantity { get; init; }
    public decimal PendingQuantity { get; init; }
    public ProcurementReceiptDisposition Disposition { get; init; }
    public string? RejectionReason { get; init; }
    public string? InspectionNotes { get; init; }
    public Guid? QuarantineLocationId { get; init; }
}

public sealed class ProcurementReceiptInspectionEvidenceDto
{
    public Guid Id { get; init; }
    public string ActionKey { get; init; } = string.Empty;
    public string RequirementKey { get; init; } = string.Empty;
    public ProcurementReceiptInspectionEvidenceKind ReferenceKind { get; init; }
    public Guid? WorkflowEvidenceDocumentId { get; init; }
    public Guid? FileUploadRecordId { get; init; }
    public string EvidenceReference { get; init; } = string.Empty;
    public string EvidenceHash { get; init; } = string.Empty;
}

public sealed class ProcurementReceiptInspectionActionDto
{
    public Guid Id { get; init; }
    public int Sequence { get; init; }
    public ProcurementReceiptInspectionActionType ActionType { get; init; }
    public ProcurementReceiptInspectionStatus StatusAfter { get; init; }
    public ProcurementReceiptResolutionKind ResolutionKind { get; init; }
    public decimal Quantity { get; init; }
    public string Reference { get; init; } = string.Empty;
    public string Comment { get; init; } = string.Empty;
    public string ActorName { get; init; } = string.Empty;
    public DateTime OccurredAtUtc { get; init; }
}

public sealed class ProcurementReceiptInspectionDto
{
    public Guid Id { get; init; }
    public Guid PurchaseOrderReceiptId { get; init; }
    public int Sequence { get; init; }
    public ProcurementReceiptInspectionStatus Status { get; init; }
    public decimal ReceivedQuantity { get; init; }
    public decimal AcceptedQuantity { get; init; }
    public decimal RejectedQuantity { get; init; }
    public decimal PendingQuantity { get; init; }
    public bool QualityHold { get; init; }
    public string? QualityHoldReason { get; init; }
    public string? RejectionNoteNumber { get; init; }
    public ProcurementReceiptSupplierAcknowledgementStatus SupplierAcknowledgementStatus { get; init; }
    public ProcurementReceiptResolutionKind ResolutionKind { get; init; }
    public ProcurementReceiptResolutionStatus ResolutionStatus { get; init; }
    public Guid? ReplacementPurchaseOrderReceiptId { get; init; }
    public Guid? ReplacementInspectionCaseId { get; init; }
    public DateTime? ReplacementLinkedAtUtc { get; init; }
    public decimal StockEligibleQuantity { get; init; }
    public decimal StockPostedQuantity { get; init; }
    public decimal ApEligibleQuantity { get; init; }
    public decimal ApBlockedQuantity { get; init; }
    public bool ApprovalRequired { get; init; } = true;
    public Guid? WorkflowDefinitionId { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<ProcurementReceiptInspectionLineDto> Lines { get; init; } = [];
    public IReadOnlyList<ProcurementReceiptInspectionEvidenceDto> Evidence { get; init; } = [];
    public IReadOnlyList<ProcurementReceiptInspectionActionDto> Actions { get; init; } = [];
}

public sealed class ProcurementReceiptInspectionOverviewDto
{
    public Guid PurchaseOrderReceiptId { get; init; }
    public Guid? WarehouseId { get; init; }
    public string ReceiptNumber { get; init; } = string.Empty;
    public string PurchaseOrderNumber { get; init; } = string.Empty;
    public string SupplierName { get; init; } = string.Empty;
    public bool CanEdit { get; init; }
    public bool CanSubmit { get; init; }
    public bool CanDecide { get; init; }
    public bool CanAcknowledge { get; init; }
    public bool CanResolve { get; init; }
    public bool CanClose { get; init; }
    public IReadOnlyList<string> DecisionKeys { get; init; } = [];
    public IReadOnlyList<string> EvidenceRequirementKeys { get; init; } = [];
    public ProcurementReceiptInspectionDto? Current { get; init; }
    public IReadOnlyList<ProcurementReceiptInspectionDto> History { get; init; } = [];
}
