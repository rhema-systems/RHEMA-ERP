using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Inventory;

public sealed class InventoryReplenishmentRecommendation : TenantEntity
{
    [Required, MaxLength(50)] public string RecommendationNumber { get; set; } = string.Empty;
    public Guid WarehouseQuantityId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid InventoryItemId { get; set; }
    public Guid? ItemSupplierId { get; set; }
    public Guid? PreferredSupplierId { get; set; }
    public InventoryReplenishmentRecommendationStatus Status { get; set; } = InventoryReplenishmentRecommendationStatus.Draft;
    public int DemandWindowDays { get; set; }
    public DateTime DemandFromUtc { get; set; }
    public DateTime DemandToUtc { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal DemandQuantity { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal AverageDailyDemand { get; set; }
    public int LeadTimeDays { get; set; }
    public int SafetyLeadTimeDays { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal CurrentStock { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal AvailableStock { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal AllocatedStock { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal OnOrderQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal OpenRecommendationQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal MinimumLevel { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal MaximumLevel { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal ReorderLevel { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal ReorderQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal SafetyStock { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal MinimumOrderQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal OrderMultiple { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal LeadTimeDemand { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal ProjectedAvailableAtReceipt { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal RecommendedQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal EstimatedUnitCost { get; set; }
    public DateTime RequiredDateUtc { get; set; }
    public DateTime ValidUntilUtc { get; set; }
    [Required, MaxLength(4000)] public string Explanation { get; set; } = string.Empty;
    [Required, Column(TypeName = "nvarchar(max)")] public string CalculationSnapshotJson { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string CalculationHash { get; set; } = string.Empty;
    public Guid GeneratedById { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public Guid? AlertNotificationId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? DecidedById { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    [MaxLength(1000)] public string? DecisionComment { get; set; }
    public Guid? PurchaseRequisitionId { get; set; }
    [MaxLength(50)] public string? PurchaseRequisitionNumber { get; set; }
    public DateTime? ConvertedAtUtc { get; set; }
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string PayloadHash { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public WarehouseQuantity WarehouseQuantity { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public InventoryItem InventoryItem { get; set; } = null!;
    public ItemSupplier? ItemSupplier { get; set; }
    public Notification? AlertNotification { get; set; }
    public ApplicationUser GeneratedBy { get; set; } = null!;
    public ApplicationUser? SubmittedBy { get; set; }
    public ApplicationUser? DecidedBy { get; set; }
    public PurchaseRequisition? PurchaseRequisition { get; set; }
    public ICollection<InventoryReplenishmentAction> Actions { get; set; } = new List<InventoryReplenishmentAction>();
}

public sealed class InventoryReplenishmentAction : TenantEntity
{
    public Guid RecommendationId { get; set; }
    public int Sequence { get; set; }
    public InventoryReplenishmentActionType ActionType { get; set; }
    public InventoryReplenishmentRecommendationStatus? PreviousStatus { get; set; }
    public InventoryReplenishmentRecommendationStatus NewStatus { get; set; }
    public Guid ActorUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string PayloadHash { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Reason { get; set; }
    [MaxLength(64)] public string? PreviousHash { get; set; }
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public InventoryReplenishmentRecommendation Recommendation { get; set; } = null!;
    public ApplicationUser ActorUser { get; set; } = null!;
}
