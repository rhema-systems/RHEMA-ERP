using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementFrameworkCallOffs")]
public sealed class ProcurementFrameworkCallOff : TenantEntity
{
    [Required, StringLength(50)] public string CallOffNumber { get; set; } = string.Empty;
    public ProcurementFrameworkCallOffStatus Status { get; set; } =
        ProcurementFrameworkCallOffStatus.Draft;
    public Guid AgreementId { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public Guid SourceRequisitionId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public Guid AuthorityId { get; set; }
    public ProcurementFrameworkAuthorityKind AuthorityKind { get; set; }
    [Required, StringLength(200)] public string AuthorityValue { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal? AuthorityThreshold { get; set; }
    [Required, StringLength(3)] public string CurrencyCode { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal TotalAmount { get; set; }
    public DateTime RequiredDateUtc { get; set; }
    public Guid? DeliveryWarehouseId { get; set; }
    [StringLength(500)] public string? DeliveryAddress { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }

    [Required, StringLength(50)] public string AgreementNumber { get; set; } = string.Empty;
    public int AgreementVersion { get; set; }
    [Required, StringLength(64)] public string AgreementIntegrityHash { get; set; } = string.Empty;
    [Required, StringLength(64)] public string AwardReadinessIntegrityHash { get; set; } = string.Empty;
    [Required, StringLength(64)] public string SupplierEligibilityDecisionHash { get; set; } = string.Empty;
    [Required, StringLength(50)] public string PriceListReference { get; set; } = string.Empty;
    public int PriceListVersion { get; set; }
    public DateTime AgreementEffectiveFromUtc { get; set; }
    public DateTime AgreementEffectiveEndUtc { get; set; }

    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid CreatedByUserId { get; set; }
    [Required, StringLength(300)] public string CreatedByName { get; set; } = string.Empty;
    public Guid? SubmittedById { get; set; }
    [StringLength(300)] public string? SubmittedByName { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? ApprovedById { get; set; }
    [StringLength(300)] public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? BalanceDeductedAtUtc { get; set; }
    public Guid? IssuedById { get; set; }
    [StringLength(300)] public string? IssuedByName { get; set; }
    public DateTime? IssuedAtUtc { get; set; }
    public Guid? RejectedById { get; set; }
    [StringLength(300)] public string? RejectedByName { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public Guid? CancelledById { get; set; }
    [StringLength(300)] public string? CancelledByName { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    [StringLength(1000)] public string? DecisionComment { get; set; }

    [Required, StringLength(100)] public string CreationCorrelationId { get; set; } = string.Empty;
    [Required, StringLength(100)] public string LastOperationCorrelationId { get; set; } = string.Empty;
    [Required, StringLength(50)] public string LastOperation { get; set; } = "Created";
    [Required, Column(TypeName = "nvarchar(max)")] public string SnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementFrameworkAgreement Agreement { get; set; } = null!;
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public PurchaseRequisition SourceRequisition { get; set; } = null!;
    public BusinessPartner BusinessPartner { get; set; } = null!;
    public ProcurementFrameworkCallOffAuthority Authority { get; set; } = null!;
    public WorkflowDefinition? WorkflowDefinition { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }
    public ICollection<ProcurementFrameworkCallOffLine> Lines { get; set; } =
        new List<ProcurementFrameworkCallOffLine>();
    public ICollection<ProcurementFrameworkBalanceMovement> BalanceMovements { get; set; } =
        new List<ProcurementFrameworkBalanceMovement>();
}

[Table("ProcurementFrameworkCallOffLines")]
public sealed class ProcurementFrameworkCallOffLine : TenantEntity, ErpSystem.Core.Interfaces.Inventory.ICommercialQuantityEvidenceLine
{
    public Guid CallOffId { get; set; }
    public Guid AgreementPriceLineId { get; set; }
    public Guid PurchaseRequisitionItemId { get; set; }
    public Guid PurchaseOrderItemId { get; set; }
    public Guid InventoryItemId { get; set; }
    [Required, StringLength(100)] public string ItemCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string ItemName { get; set; } = string.Empty;
    [Required, StringLength(20)] public string UnitOfMeasure { get; set; } = string.Empty;
    public Guid? UnitOfMeasureId { get; set; }
    [StringLength(20)] public string? UnitOfMeasureCodeSnapshot { get; set; }
    public int? UnitOfMeasureDecimalPlacesSnapshot { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal? UnitOfMeasureRoundingIncrementSnapshot { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal Quantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal UnitPrice { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal LineTotal { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal SourceDemandQuantity { get; set; }
    [Required, StringLength(64)] public string PriceIntegrityHash { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementFrameworkCallOff CallOff { get; set; } = null!;
    public ProcurementFrameworkPriceListLine AgreementPriceLine { get; set; } = null!;
    public PurchaseRequisitionItem PurchaseRequisitionItem { get; set; } = null!;
    public PurchaseOrderItem PurchaseOrderItem { get; set; } = null!;
    public InventoryItem InventoryItem { get; set; } = null!;
}

[Table("ProcurementFrameworkAgreementBalances")]
public sealed class ProcurementFrameworkAgreementBalance : TenantEntity
{
    public Guid AgreementId { get; set; }
    [Required, StringLength(3)] public string CurrencyCode { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal CeilingAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CommittedAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal IssuedAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal AvailableAmount { get; set; }
    public Guid? LastMovementId { get; set; }
    public DateTime? LastMovementAtUtc { get; set; }
    public DateTime? LastExpiryAlertAtUtc { get; set; }
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementFrameworkAgreement Agreement { get; set; } = null!;
    public ICollection<ProcurementFrameworkBalanceMovement> Movements { get; set; } =
        new List<ProcurementFrameworkBalanceMovement>();
}

[Table("ProcurementFrameworkBalanceMovements")]
public sealed class ProcurementFrameworkBalanceMovement : TenantEntity
{
    public Guid AgreementId { get; set; }
    public Guid AgreementBalanceId { get; set; }
    public Guid CallOffId { get; set; }
    public ProcurementFrameworkBalanceMovementType MovementType { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal BalanceBefore { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal BalanceAfter { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementFrameworkAgreement Agreement { get; set; } = null!;
    public ProcurementFrameworkAgreementBalance AgreementBalance { get; set; } = null!;
    public ProcurementFrameworkCallOff CallOff { get; set; } = null!;
}
