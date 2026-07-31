using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementFrameworkCallOffSearchRequest
{
    public string? Search { get; set; }
    public ProcurementFrameworkCallOffStatus? Status { get; set; }
    public Guid? AgreementId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public Guid? SourceRequisitionId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class ProcurementFrameworkCallOffSummaryDto
{
    public int TotalCount { get; set; }
    public int DraftCount { get; set; }
    public int PendingApprovalCount { get; set; }
    public int ApprovedCount { get; set; }
    public int IssuedCount { get; set; }
    public int ExpiringAgreementCount { get; set; }
    public decimal TotalCommittedAmount { get; set; }
    public decimal TotalIssuedAmount { get; set; }
    public decimal TotalAvailableAmount { get; set; }
    public Dictionary<string, decimal> CommittedByCurrency { get; set; } = new();
    public Dictionary<string, decimal> IssuedByCurrency { get; set; } = new();
    public Dictionary<string, decimal> AvailableByCurrency { get; set; } = new();
}

public sealed class ProcurementFrameworkCallOffPageDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<ProcurementFrameworkCallOffListItemDto> Items { get; set; } = new();
}

public class ProcurementFrameworkCallOffListItemDto
{
    public Guid Id { get; set; }
    public string CallOffNumber { get; set; } = string.Empty;
    public ProcurementFrameworkCallOffStatus Status { get; set; }
    public Guid AgreementId { get; set; }
    public string AgreementNumber { get; set; } = string.Empty;
    public int AgreementVersion { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public string PurchaseOrderStatus { get; set; } = string.Empty;
    public Guid SourceRequisitionId { get; set; }
    public string SourceRequisitionNumber { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal AgreementCeilingAmount { get; set; }
    public decimal AgreementCommittedAmount { get; set; }
    public decimal AgreementIssuedAmount { get; set; }
    public decimal AgreementAvailableAmount { get; set; }
    public DateTime RequiredDateUtc { get; set; }
    public DateTime AgreementEffectiveEndUtc { get; set; }
    public bool AgreementIsEffective { get; set; }
    public int DaysToExpiry { get; set; }
    public int LineCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? IssuedAtUtc { get; set; }
    public IReadOnlyList<string> AllowedActions { get; set; } = Array.Empty<string>();
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementFrameworkCallOffDto :
    ProcurementFrameworkCallOffListItemDto
{
    public Guid AuthorityId { get; set; }
    public ProcurementFrameworkAuthorityKind AuthorityKind { get; set; }
    public string AuthorityValue { get; set; } = string.Empty;
    public decimal? AuthorityThreshold { get; set; }
    public Guid? DeliveryWarehouseId { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? Notes { get; set; }
    public string PriceListReference { get; set; } = string.Empty;
    public int PriceListVersion { get; set; }
    public DateTime AgreementEffectiveFromUtc { get; set; }
    public string AgreementIntegrityHash { get; set; } = string.Empty;
    public string AwardReadinessIntegrityHash { get; set; } = string.Empty;
    public string SupplierEligibilityDecisionHash { get; set; } = string.Empty;
    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public Guid? SubmittedById { get; set; }
    public string? SubmittedByName { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public Guid? IssuedById { get; set; }
    public string? IssuedByName { get; set; }
    public Guid? RejectedById { get; set; }
    public string? RejectedByName { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public Guid? CancelledById { get; set; }
    public string? CancelledByName { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? DecisionComment { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
    public List<ProcurementFrameworkCallOffLineDto> Lines { get; set; } = new();
    public List<ProcurementFrameworkBalanceMovementDto> BalanceMovements { get; set; } = new();
}

public sealed class ProcurementFrameworkCallOffLineDto
{
    public Guid Id { get; set; }
    public Guid AgreementPriceLineId { get; set; }
    public Guid PurchaseRequisitionItemId { get; set; }
    public Guid PurchaseOrderItemId { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal SourceDemandQuantity { get; set; }
    public decimal SourceDemandAllocatedQuantity { get; set; }
    public decimal SourceDemandRemainingQuantity { get; set; }
    public string PriceIntegrityHash { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementFrameworkBalanceMovementDto
{
    public Guid Id { get; set; }
    public ProcurementFrameworkBalanceMovementType MovementType { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public Guid ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementFrameworkCallOffOptionsDto
{
    public List<ProcurementFrameworkCallOffAgreementOptionDto> Agreements { get; set; } = new();
    public List<ProcurementFrameworkCallOffRequisitionOptionDto> Requisitions { get; set; } = new();
    public List<ProcurementFrameworkCallOffWarehouseOptionDto> Warehouses { get; set; } = new();
    public bool PurchaseOrderWorkflowReady { get; set; }
    public string? WorkflowReadinessMessage { get; set; }
}

public sealed class ProcurementFrameworkCallOffAgreementOptionDto
{
    public Guid AgreementId { get; set; }
    public string AgreementNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Version { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal CeilingAmount { get; set; }
    public decimal CommittedAmount { get; set; }
    public decimal AvailableAmount { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime EffectiveEndUtc { get; set; }
    public int DaysToExpiry { get; set; }
    public bool CurrentActorIsAuthorized { get; set; }
    public decimal? CurrentActorMaximumCallOffAmount { get; set; }
    public List<ProcurementFrameworkCallOffPriceOptionDto> PriceLines { get; set; } = new();
}

public sealed class ProcurementFrameworkCallOffPriceOptionDto
{
    public Guid AgreementPriceLineId { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal MinimumQuantity { get; set; }
    public decimal? MaximumQuantity { get; set; }
    public int LeadTimeDays { get; set; }
}

public sealed class ProcurementFrameworkCallOffRequisitionOptionDto
{
    public Guid RequisitionId { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? RequiredDate { get; set; }
    public Guid? DeliveryWarehouseId { get; set; }
    public string? DeliveryAddress { get; set; }
    public List<ProcurementFrameworkCallOffDemandOptionDto> Lines { get; set; } = new();
}

public sealed class ProcurementFrameworkCallOffDemandOptionDto
{
    public Guid PurchaseRequisitionItemId { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemDescription { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal DemandQuantity { get; set; }
    public decimal AllocatedQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
}

public sealed class ProcurementFrameworkCallOffWarehouseOptionDto
{
    public Guid WarehouseId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateProcurementFrameworkCallOffRequest
{
    public Guid AgreementId { get; set; }
    public Guid SourceRequisitionId { get; set; }
    public DateTime RequiredDateUtc { get; set; }
    public Guid? DeliveryWarehouseId { get; set; }
    [StringLength(500)] public string? DeliveryAddress { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    [MinLength(1)] public List<CreateProcurementFrameworkCallOffLineRequest> Lines { get; set; } = new();
}

public sealed class CreateProcurementFrameworkCallOffLineRequest
{
    public Guid PurchaseRequisitionItemId { get; set; }
    public Guid AgreementPriceLineId { get; set; }
    [Range(typeof(decimal), "0.0001", "99999999999999")]
    public decimal Quantity { get; set; }
}

public class ProcurementFrameworkCallOffLifecycleRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Comment { get; set; } = string.Empty;
    [MinLength(1)] public List<ProcurementControlEventEvidenceReference> Evidence { get; set; } = new();
}

public sealed class ProcurementFrameworkCallOffDecisionRequest :
    ProcurementFrameworkCallOffLifecycleRequest
{
    public bool Approved { get; set; }
}
