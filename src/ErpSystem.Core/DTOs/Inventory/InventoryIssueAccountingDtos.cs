using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Inventory;

public sealed class InventoryIssueAccountingRuleRequest
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
    public string? RowVersion { get; set; }
}

public sealed class InventoryIssueAccountingRuleDto
{
    public Guid Id { get; set; }
    public Guid InventoryCategoryId { get; set; }
    public string InventoryCategoryCode { get; set; } = string.Empty;
    public string InventoryCategoryName { get; set; } = string.Empty;
    public ItemType ItemType { get; set; }
    public string MovementReasonCode { get; set; } = string.Empty;
    public string MovementReasonName { get; set; } = string.Empty;
    public InventoryIssueAccountingTreatment Treatment { get; set; }
    public Guid? ExpenseAccountId { get; set; }
    public string? ExpenseAccount { get; set; }
    public Guid? FixedAssetCategoryId { get; set; }
    public string? FixedAssetCategory { get; set; }
    public bool IsActive { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class InventoryIssueAccountingOptionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Type { get; set; }
}

public sealed class InventoryIssueAccountingOptionsDto
{
    public IReadOnlyList<InventoryIssueAccountingOptionDto> InventoryCategories { get; set; } = [];
    public IReadOnlyList<InventoryIssueAccountingOptionDto> ExpenseAccounts { get; set; } = [];
    public IReadOnlyList<InventoryIssueAccountingOptionDto> FixedAssetCategories { get; set; } = [];
    public IReadOnlyDictionary<string, string> MovementReasons { get; set; } =
        new Dictionary<string, string>();
    public IReadOnlyList<string> ApplicableMovementReasonCodes { get; set; } = [];
    public IReadOnlyDictionary<Guid, IReadOnlyList<string>> ApplicableMovementReasonCodesByRequisitionItem { get; set; } =
        new Dictionary<Guid, IReadOnlyList<string>>();
}

public sealed class InventoryIssueFinanceLineageDto
{
    public Guid Id { get; set; }
    public Guid InventoryIssueVoucherLineId { get; set; }
    public InventoryIssueAccountingTreatment Treatment { get; set; }
    public string MovementReasonCode { get; set; } = string.Empty;
    public decimal IssuedQuantity { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public decimal IssuedValue { get; set; }
    public Guid PostingEventId { get; set; }
    public Guid JournalEntryId { get; set; }
    public Guid? FixedAssetId { get; set; }
    public InventoryIssueFinanceLineageStatus Status { get; set; }
}
