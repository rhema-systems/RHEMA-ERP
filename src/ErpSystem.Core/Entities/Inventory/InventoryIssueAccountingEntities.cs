using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Inventory;

public enum InventoryIssueAccountingTreatment
{
    Expense = 1,
    FixedAsset = 2
}

public enum InventoryIssueFinanceLineageStatus
{
    Posted = 1,
    PartiallyReturned = 2,
    Returned = 3
}

public static class InventoryIssueMovementReasons
{
    public const string DepartmentConsumption = "DEPARTMENT_CONSUMPTION";
    public const string ProjectConsumption = "PROJECT_CONSUMPTION";
    public const string MaintenanceConsumption = "MAINTENANCE_CONSUMPTION";
    public const string AssetCustody = "ASSET_CUSTODY";

    public static readonly IReadOnlyDictionary<string, string> Labels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [DepartmentConsumption] = "Department consumption",
            [ProjectConsumption] = "Project consumption",
            [MaintenanceConsumption] = "Maintenance consumption",
            [AssetCustody] = "Fixed asset custody"
        };
}

/// <summary>
/// Tenant-owned operational mapping from an Inventory category/item type and a controlled
/// movement reason to the authoritative Finance or Fixed Assets owner.
/// </summary>
public sealed class InventoryIssueAccountingRule : TenantEntity
{
    public Guid InventoryCategoryId { get; set; }
    public ItemType ItemType { get; set; }
    [Required, MaxLength(50)] public string MovementReasonCode { get; set; } = string.Empty;
    public InventoryIssueAccountingTreatment Treatment { get; set; }
    public Guid? ExpenseAccountId { get; set; }
    public Guid? FixedAssetCategoryId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public InventoryCategory InventoryCategory { get; set; } = null!;
    public Account? ExpenseAccount { get; set; }
    public FixedAssetCategory? FixedAssetCategory { get; set; }
}

/// <summary>
/// Immutable issue-line to Finance/Fixed-Asset lineage. Current return quantity/status are the
/// only mutable lifecycle fields and are protected by the corresponding SQL transition guard.
/// </summary>
public sealed class InventoryIssueFinanceLineage : TenantEntity
{
    public Guid InventoryIssueVoucherLineId { get; set; }
    public Guid InventoryIssueAccountingRuleId { get; set; }
    public InventoryIssueAccountingTreatment Treatment { get; set; }
    [Required, MaxLength(50)] public string MovementReasonCode { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")] public decimal IssuedQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal ReturnedQuantity { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal IssuedValue { get; set; }
    public Guid PostingEventId { get; set; }
    public Guid JournalEntryId { get; set; }
    public Guid? FixedAssetId { get; set; }
    public InventoryIssueFinanceLineageStatus Status { get; set; } = InventoryIssueFinanceLineageStatus.Posted;
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public InventoryIssueVoucherLine InventoryIssueVoucherLine { get; set; } = null!;
    public InventoryIssueAccountingRule InventoryIssueAccountingRule { get; set; } = null!;
    public FixedAsset? FixedAsset { get; set; }
}

/// <summary>
/// Exact allocation of a governed Store Return Voucher line back to the original issue posting.
/// This permits partial returns without falsely marking the original Finance journal reversed.
/// </summary>
public sealed class InventoryIssueReturnAllocation : TenantEntity
{
    public Guid InventoryReturnVoucherLineId { get; set; }
    public Guid InventoryIssueFinanceLineageId { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal Quantity { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Value { get; set; }
    public Guid ReturnPostingEventId { get; set; }
    public Guid ReturnJournalEntryId { get; set; }
    public Guid? ReversalPostingEventId { get; set; }
    public Guid? ReversalJournalEntryId { get; set; }
    public DateTime PostedAtUtc { get; set; }
    public DateTime? ReversedAtUtc { get; set; }
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public InventoryReturnVoucherLine InventoryReturnVoucherLine { get; set; } = null!;
    public InventoryIssueFinanceLineage InventoryIssueFinanceLineage { get; set; } = null!;
}
