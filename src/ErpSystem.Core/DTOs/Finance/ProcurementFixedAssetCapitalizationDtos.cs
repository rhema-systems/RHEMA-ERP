using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Finance;

public sealed class ProcurementFixedAssetCandidateDto
{
    public ProcurementAcceptedSupplyKind AcceptedSupplyKind { get; init; }
    public Guid AcceptedSupplySourceId { get; init; }
    public string AcceptedSupplyReference { get; init; } = string.Empty;
    public Guid PurchaseOrderId { get; init; }
    public Guid PurchaseOrderItemId { get; init; }
    public Guid InventoryItemId { get; init; }
    public string ItemCode { get; init; } = string.Empty;
    public string ItemDescription { get; init; } = string.Empty;
    public decimal AcceptedQuantity { get; init; }
    public decimal ReservedOrCapitalizedQuantity { get; init; }
    public decimal AvailableQuantity { get; init; }
    public string SourceCurrencyCode { get; init; } = string.Empty;
    public decimal SourceUnitPrice { get; init; }
    public string FunctionalCurrencyCode { get; init; } = string.Empty;
    public decimal FunctionalUnitCost { get; init; }
    public bool IsReady { get; init; }
    public IReadOnlyList<string> BlockedReasons { get; init; } = [];
}

/// <summary>
/// Finance policy needed to create one individually controlled asset from one unit of accepted
/// Procurement supply. Source quantity and carrying cost are deliberately not caller-editable.
/// </summary>
public sealed class CreateProcurementFixedAssetDraftDto
{
    public ProcurementAcceptedSupplyKind AcceptedSupplyKind { get; init; } =
        ProcurementAcceptedSupplyKind.GoodsReceiptInspection;
    public Guid AcceptedSupplySourceId { get; init; }
    public Guid PurchaseOrderId { get; init; }
    public Guid PurchaseOrderItemId { get; init; }
    public string IdempotencyKey { get; init; } = string.Empty;
    public string AssetCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Location { get; init; }
    public Guid FixedAssetCategoryId { get; init; }
    public DateTime? PlacedInServiceDate { get; init; }
    public DepreciationMethod DepreciationMethod { get; init; } = DepreciationMethod.StraightLine;
    public DepreciationConvention DepreciationConvention { get; init; } = DepreciationConvention.FullMonth;
    public int UsefulLifeMonths { get; init; }
    public decimal ResidualValue { get; init; }
    public decimal DiminishingBalanceRatePercent { get; init; }
    public decimal LifetimeProductionCapacity { get; init; }
    public string? SerialNumber { get; init; }
}

public sealed class PostProcurementFixedAssetCapitalizationDto
{
    public DateTime CapitalizationDate { get; init; } = DateTime.UtcNow.Date;
    public string Reason { get; init; } = string.Empty;
}

public sealed class ProcurementFixedAssetCapitalizationDto
{
    public Guid Id { get; init; }
    public Guid FixedAssetId { get; init; }
    public string AssetCode { get; init; } = string.Empty;
    public ProcurementAcceptedSupplyKind AcceptedSupplyKind { get; init; }
    public Guid AcceptedSupplySourceId { get; init; }
    public string AcceptedSupplyReference { get; init; } = string.Empty;
    public Guid PurchaseOrderId { get; init; }
    public Guid PurchaseOrderItemId { get; init; }
    public Guid InventoryItemId { get; init; }
    public decimal CapitalizedQuantity { get; init; }
    public string SourceCurrencyCode { get; init; } = string.Empty;
    public decimal SourceTransactionAmount { get; init; }
    public string FunctionalCurrencyCode { get; init; } = string.Empty;
    public decimal FunctionalAmount { get; init; }
    public ProcurementFixedAssetCapitalizationStatus Status { get; init; }
    public DateTime CapitalizationDate { get; init; }
    public Guid? PostingEventId { get; init; }
    public Guid? JournalEntryId { get; init; }
    public DateTime? PostedAt { get; init; }
    public DateTime? ReversedAt { get; init; }
    public string? FailureReason { get; init; }
}

/// <summary>
/// Internal Finance instruction used by the adapter after it has validated Procurement evidence.
/// It is intentionally separate from the public direct-capitalization DTO so callers cannot forge
/// a Procurement source discriminator through the ordinary fixed-asset endpoint.
/// </summary>
public sealed class ProcurementFixedAssetPostingInstructionDto
{
    public Guid CapitalizationId { get; init; }
    public Guid PurchaseOrderItemId { get; init; }
    public DateTime CapitalizationDate { get; init; }
    public Guid InventoryControlAccountId { get; init; }
    public decimal FunctionalAmount { get; init; }
    public string FunctionalCurrencyCode { get; init; } = string.Empty;
    public string SourceReference { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}
