namespace ErpSystem.Core.DTOs.Inventory;

public enum InventoryActivityClassification
{
    Active = 0,
    SlowMoving = 1,
    NonMoving = 2,
    Stockout = 3
}

public sealed class InventoryAnalyticsDto
{
    public DateTime AsOfUtc { get; set; }
    public int SlowMovingDays { get; set; }
    public int NonMovingDays { get; set; }
    public int ExpiryWarningDays { get; set; }
    public InventoryAnalyticsSummaryDto Summary { get; set; } = new();
    public IReadOnlyList<InventoryAgeingBandDto> AgeingBands { get; set; } = [];
    public IReadOnlyList<InventoryItemLocationAnalyticsDto> Items { get; set; } = [];
}

public sealed class InventoryAnalyticsSummaryDto
{
    public int ItemLocationCount { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal InventoryValue { get; set; }
    public int SlowMovingCount { get; set; }
    public decimal SlowMovingValue { get; set; }
    public int NonMovingCount { get; set; }
    public decimal NonMovingValue { get; set; }
    public int StockoutCount { get; set; }
    public int DisposalCandidateCount { get; set; }
    public int ReplenishmentCandidateCount { get; set; }
    public decimal ExpiredQuantity { get; set; }
    public decimal ExpiredValue { get; set; }
    public decimal ExpiringQuantity { get; set; }
    public decimal ExpiringValue { get; set; }
}

public sealed class InventoryAgeingBandDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int FromDays { get; set; }
    public int? ToDays { get; set; }
    public decimal Quantity { get; set; }
    public decimal Value { get; set; }
    public int ItemLocationCount { get; set; }
}

public sealed class InventoryItemLocationAnalyticsDto
{
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public Guid? LocationId { get; set; }
    public string? LocationCode { get; set; }
    public string? LocationName { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal QuantityAllocated { get; set; }
    public decimal QuantityAvailable { get; set; }
    public decimal QuantityOnOrder { get; set; }
    public decimal InventoryValue { get; set; }
    public decimal AverageUnitCost { get; set; }
    public decimal ReorderLevel { get; set; }
    public DateTime? LastMovementDateUtc { get; set; }
    public DateTime? LastReceiptDateUtc { get; set; }
    public DateTime? LastIssueDateUtc { get; set; }
    public int DaysSinceActivity { get; set; }
    public InventoryActivityClassification ActivityClassification { get; set; }
    public int? CurrentStockoutDays { get; set; }
    public decimal AverageDailyDemand { get; set; }
    public decimal? EstimatedDaysOfCover { get; set; }
    public int OldestStockAgeDays { get; set; }
    public string OldestAgeingBand { get; set; } = string.Empty;
    public decimal ExpiredQuantity { get; set; }
    public decimal ExpiredValue { get; set; }
    public decimal ExpiringQuantity { get; set; }
    public decimal ExpiringValue { get; set; }
    public bool DisposalCandidate { get; set; }
    public bool ReplenishmentCandidate { get; set; }
    public Guid? ReplenishmentRecommendationId { get; set; }
    public string? ReplenishmentRecommendationNumber { get; set; }
    public string? ReplenishmentRecommendationStatus { get; set; }
    public Guid? ReplenishmentPurchaseRequisitionId { get; set; }
    public string? ReplenishmentPurchaseRequisitionNumber { get; set; }
    public string RecommendedActionCode { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public IReadOnlyList<InventoryAgeingBandDto> AgeingBands { get; set; } = [];
}
