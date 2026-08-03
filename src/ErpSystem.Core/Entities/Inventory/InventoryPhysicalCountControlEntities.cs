using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Inventory;

public enum PhysicalCountActionType
{
    Scheduled = 1,
    Created = 2,
    Started = 3,
    CountRecorded = 4,
    RecountRequired = 5,
    RecountRecorded = 6,
    Submitted = 7,
    StoresApproved = 8,
    FinanceApproved = 9,
    AuditAttested = 10,
    Rejected = 11,
    Posted = 12,
    Cancelled = 13
}

public sealed class InventoryCycleCountSchedule : TenantEntity
{
    [Required] public Guid WarehouseId { get; set; }
    [Required] public Guid LocationId { get; set; }
    [Required, MaxLength(1)] public string ABCClass { get; set; } = "A";
    [Range(1, 366)] public int FrequencyDays { get; set; }
    public DateTime NextDueAtUtc { get; set; }
    [Required] public Guid CalendarOccurrenceId { get; set; }
    [Required] public Guid CutoffOccurrenceId { get; set; }
    public DateTime CutoffAtUtc { get; set; }
    public bool FreezeInventory { get; set; } = true;
    public bool BlindCount { get; set; } = true;
    [Column(TypeName = "decimal(18,4)")] public decimal RecountQuantityThreshold { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal RecountValueThreshold { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastGeneratedAtUtc { get; set; }
    public Guid? LastPhysicalCountId { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Warehouse Warehouse { get; set; } = null!;
    public WarehouseLocation Location { get; set; } = null!;
    public PhysicalCount? LastPhysicalCount { get; set; }
}

public sealed class PhysicalCountAction : TenantEntity
{
    [Required] public Guid PhysicalCountId { get; set; }
    [Range(1, int.MaxValue)] public int Sequence { get; set; }
    public PhysicalCountActionType ActionType { get; set; }
    [Required] public Guid ActorUserId { get; set; }
    [Required, MaxLength(100)] public string ActorRole { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string PayloadHash { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Comment { get; set; }
    [Required] public string SnapshotJson { get; set; } = "{}";
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public PhysicalCount PhysicalCount { get; set; } = null!;
    public ApplicationUser ActorUser { get; set; } = null!;
}
