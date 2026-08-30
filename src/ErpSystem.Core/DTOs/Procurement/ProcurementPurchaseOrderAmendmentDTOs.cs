using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementPurchaseOrderAmendmentItemRequest
{
    public Guid? PurchaseOrderItemId { get; set; }

    public Guid? InventoryItemId { get; set; }

    [StringLength(100)]
    public string? SupplierItemCode { get; set; }

    [Required, StringLength(200)]
    public string ItemDescription { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.000001", "79228162514264337593543950335")]
    public decimal OrderedQuantity { get; set; }

    [Required, StringLength(20)]
    public string UnitOfMeasure { get; set; } = "EA";

    public Guid? ItemUnitOfMeasureId { get; set; }
    public Guid? WarehouseId { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal UnitPrice { get; set; }

    public Guid? PriceListLineId { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}

public sealed class CreateProcurementPurchaseOrderAmendmentRequest
{
    [Required, StringLength(1000, MinimumLength = 10)]
    public string Reason { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string ChangeScope { get; set; } = string.Empty;

    public ProcurementPurchaseOrderSourceType? SourceType { get; set; }
    public Guid? SourceId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public DateTime? RequiredDate { get; set; }
    public DateTime? PromisedDate { get; set; }

    [StringLength(100)]
    public string? PaymentTerms { get; set; }

    [StringLength(100)]
    public string? ShippingTerms { get; set; }

    [StringLength(2000)]
    public string? Terms { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public Guid? DeliveryWarehouseId { get; set; }

    [StringLength(500)]
    public string? DeliveryAddress { get; set; }

    [StringLength(2000)]
    public string? DeliveryInstructions { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal TaxAmount { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal ShippingCost { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal MiscellaneousCost { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal DiscountAmount { get; set; }

    [Required, MinLength(1)]
    public List<ProcurementPurchaseOrderAmendmentItemRequest> Items { get; set; } = [];

    [Required, StringLength(500)]
    public string EvidenceReference { get; set; } = string.Empty;

    public Guid? EvidenceWorkflowDocumentId { get; set; }
    public Guid? EvidenceFileUploadRecordId { get; set; }

    [Required, StringLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;
}

public class ProcurementPurchaseOrderAmendmentLifecycleRequest
{
    [Required, StringLength(1000)]
    public string Comment { get; set; } = string.Empty;

    [Required]
    public string RowVersion { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string EvidenceReference { get; set; } = string.Empty;

    public Guid? EvidenceWorkflowDocumentId { get; set; }
    public Guid? EvidenceFileUploadRecordId { get; set; }
}

public sealed class DecideProcurementPurchaseOrderAmendmentRequest :
    ProcurementPurchaseOrderAmendmentLifecycleRequest
{
    public bool Approved { get; set; }
}

public sealed class DispatchProcurementPurchaseOrderAmendmentRequest
{
    public ProcurementPurchaseOrderDispatchChannel Channel { get; set; }

    [Required, StringLength(500)]
    public string Destination { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string DispatchReference { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string DocumentReference { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string OrganizationSignatureEvidenceReference { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string DispatchEvidenceReference { get; set; } = string.Empty;

    public Guid? EvidenceWorkflowDocumentId { get; set; }
    public Guid? EvidenceFileUploadRecordId { get; set; }

    [Required, StringLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class AcknowledgeProcurementPurchaseOrderAmendmentRequest
{
    public ProcurementPurchaseOrderAcknowledgementOutcome Outcome { get; set; }

    [Required, StringLength(100)]
    public string AcknowledgementChannel { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string AcknowledgementReference { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string EvidenceReference { get; set; } = string.Empty;

    public Guid? EvidenceWorkflowDocumentId { get; set; }
    public Guid? EvidenceFileUploadRecordId { get; set; }

    [StringLength(1000)]
    public string? Comments { get; set; }

    [Required, StringLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class ProcurementPurchaseOrderAmendmentDiffDto
{
    public string Path { get; set; } = string.Empty;
    public string? Before { get; set; }
    public string? After { get; set; }
}

public sealed class ProcurementPurchaseOrderCommitmentAdjustmentDto
{
    public Guid Id { get; set; }
    public int Sequence { get; set; }
    public decimal PurchaseOrderAmountBefore { get; set; }
    public decimal PurchaseOrderAmountAfter { get; set; }
    public decimal RequisitionExposureBefore { get; set; }
    public decimal RequisitionExposureAfter { get; set; }
    public decimal CommitmentAmountBefore { get; set; }
    public decimal CommitmentAmountAfter { get; set; }
    public decimal DeltaAmount { get; set; }
    public decimal BudgetCommittedBefore { get; set; }
    public decimal BudgetCommittedAfter { get; set; }
    public decimal BudgetAvailableBefore { get; set; }
    public decimal BudgetAvailableAfter { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime AppliedAtUtc { get; set; }
    public string AppliedByName { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementPurchaseOrderAmendmentAcknowledgementDto
{
    public Guid Id { get; set; }
    public int Sequence { get; set; }
    public ProcurementPurchaseOrderAcknowledgementOutcome Outcome { get; set; }
    public DateTime AcknowledgedAtUtc { get; set; }
    public Guid AcknowledgedByUserId { get; set; }
    public Guid? AcknowledgedByBusinessPartnerId { get; set; }
    public string AcknowledgementChannel { get; set; } = string.Empty;
    public string AcknowledgementReference { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementPurchaseOrderAmendmentDispatchDto
{
    public Guid Id { get; set; }
    public int RevisionNumber { get; set; }
    public int Sequence { get; set; }
    public ProcurementPurchaseOrderDispatchChannel Channel { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string DispatchReference { get; set; } = string.Empty;
    public string DocumentReference { get; set; } = string.Empty;
    public string OrganizationSignatureEvidenceReference { get; set; } = string.Empty;
    public string DispatchEvidenceReference { get; set; } = string.Empty;
    public DateTime DispatchedAtUtc { get; set; }
    public string DispatchedByName { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
    public List<ProcurementPurchaseOrderAmendmentAcknowledgementDto> Acknowledgements { get; set; } = [];
}

public sealed class ProcurementPurchaseOrderAmendmentDto
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string AmendmentNumber { get; set; } = string.Empty;
    public int AmendmentSequence { get; set; }
    public int BaseRevisionNumber { get; set; }
    public int ProposedRevisionNumber { get; set; }
    public ProcurementPurchaseOrderAmendmentStatus Status { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ChangeScope { get; set; } = string.Empty;
    public decimal BeforeTotalAmount { get; set; }
    public decimal ProposedTotalAmount { get; set; }
    public decimal CommitmentDelta { get; set; }
    public string Currency { get; set; } = string.Empty;
    public ProcurementPurchaseOrderSourceType ProposedSourceType { get; set; }
    public Guid ProposedSourceId { get; set; }
    public string ProposedSourceReference { get; set; } = string.Empty;
    public Guid ProposedBusinessPartnerId { get; set; }
    public string ProposedBusinessPartnerName { get; set; } = string.Empty;
    public string BeforeIntegrityHash { get; set; } = string.Empty;
    public string ProposedIntegrityHash { get; set; } = string.Empty;
    public string DiffIntegrityHash { get; set; } = string.Empty;
    public List<ProcurementPurchaseOrderAmendmentDiffDto> Diffs { get; set; } = [];
    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public string? SubmittedByName { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecidedByName { get; set; }
    public string? DecisionComment { get; set; }
    public DateTime? AppliedAtUtc { get; set; }
    public string? AppliedByName { get; set; }
    public string RequestEvidenceReference { get; set; } = string.Empty;
    public string? ApprovalEvidenceReference { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<ProcurementPurchaseOrderCommitmentAdjustmentDto> CommitmentAdjustments { get; set; } = [];
    public List<ProcurementPurchaseOrderAmendmentDispatchDto> Dispatches { get; set; } = [];
}

public sealed class ProcurementPurchaseOrderAmendmentOverviewDto
{
    public Guid PurchaseOrderId { get; set; }
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public int CurrentRevisionNumber { get; set; }
    public bool IsFrameworkCallOff { get; set; }
    public bool CanCreateAmendment { get; set; }
    public string? BlockedReason { get; set; }
    public IReadOnlyList<string> DecisionKeys { get; set; } = [];
    public List<ProcurementPurchaseOrderAmendmentDto> Amendments { get; set; } = [];
}
