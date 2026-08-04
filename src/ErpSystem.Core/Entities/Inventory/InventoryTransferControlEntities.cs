using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;

namespace ErpSystem.Core.Entities.Inventory;

public enum InventoryTransferActionType
{
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    ShippingCostsSaved = 4,
    Dispatched = 5,
    Received = 6,
    DiscrepancyResolved = 7,
    Closed = 8,
    ShipmentReversed = 9,
    Cancelled = 10
}

public enum InventoryTransferDiscrepancyStatus
{
    Open = 1,
    Resolved = 2
}

public static class InventoryTransferDiscrepancyReasonCodes
{
    public const string Shortage = "SHORTAGE";
    public const string Damaged = "DAMAGED";
    public const string LostInTransit = "LOST_IN_TRANSIT";
    public const string WrongItem = "WRONG_ITEM";
    public const string Other = "OTHER";

    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [Shortage] = "Quantity shortage",
        [Damaged] = "Damaged in transit",
        [LostInTransit] = "Lost in transit",
        [WrongItem] = "Wrong item received",
        [Other] = "Other controlled discrepancy"
    };
}

public static class InventoryTransferDiscrepancyResolutionCodes
{
    public const string ConfirmedLoss = "CONFIRMED_LOSS";
    public const string ReturnedToSource = "RETURNED_TO_SOURCE";
    public const string ReplacementReceived = "REPLACEMENT_RECEIVED";

    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [ConfirmedLoss] = "Confirmed loss or damage",
        [ReturnedToSource] = "Returned to source location",
        [ReplacementReceived] = "Replacement received at destination"
    };
}

public sealed class InventoryTransferAction : TenantEntity
{
    public Guid InventoryTransferId { get; set; }
    public int Sequence { get; set; }
    public InventoryTransferActionType ActionType { get; set; }
    public Guid ActorUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string PayloadHash { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Comment { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string SnapshotJson { get; set; } = "{}";
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public InventoryTransfer InventoryTransfer { get; set; } = null!;
    public ApplicationUser ActorUser { get; set; } = null!;
    public ICollection<InventoryTransferActionLine> Lines { get; set; } = new List<InventoryTransferActionLine>();
}

public sealed class InventoryTransferActionLine : TenantEntity
{
    public Guid InventoryTransferActionId { get; set; }
    public Guid InventoryTransferItemId { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal DispatchedQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal ReceivedQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal DamagedQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal ShortageQuantity { get; set; }
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public InventoryTransferAction InventoryTransferAction { get; set; } = null!;
    public InventoryTransferItem InventoryTransferItem { get; set; } = null!;
}

public sealed class InventoryTransferDiscrepancy : TenantEntity
{
    public Guid InventoryTransferId { get; set; }
    public Guid InventoryTransferItemId { get; set; }
    public Guid ReceiptActionId { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal DamagedQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal ShortageQuantity { get; set; }
    [Required, MaxLength(50)] public string ReasonCode { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
    public InventoryTransferDiscrepancyStatus Status { get; set; } = InventoryTransferDiscrepancyStatus.Open;
    [MaxLength(50)] public string? ResolutionCode { get; set; }
    [MaxLength(1000)] public string? ResolutionNotes { get; set; }
    public Guid? ResolvedById { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public InventoryTransfer InventoryTransfer { get; set; } = null!;
    public InventoryTransferItem InventoryTransferItem { get; set; } = null!;
    public InventoryTransferAction ReceiptAction { get; set; } = null!;
    public ICollection<InventoryTransferDiscrepancyEvidence> Evidence { get; set; } = new List<InventoryTransferDiscrepancyEvidence>();
}

public sealed class InventoryTransferDiscrepancyEvidence : TenantEntity
{
    public Guid InventoryTransferDiscrepancyId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public Guid FileUploadRecordId { get; set; }
    [Required, MaxLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public InventoryTransferDiscrepancy InventoryTransferDiscrepancy { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
    public FileUploadRecord FileUploadRecord { get; set; } = null!;
}
