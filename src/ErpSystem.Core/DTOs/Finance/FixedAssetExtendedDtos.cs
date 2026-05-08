using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Finance;

// ========== Fixed Assets Dashboard ==========

public class FixedAssetDashboardDto
{
    // KPI Cards
    public int TotalAssets { get; set; }
    public int ActiveAssets { get; set; }
    public int DisposedAssets { get; set; }
    public int OnHoldAssets { get; set; }
    public int DraftAssets { get; set; }
    
    public decimal TotalAcquisitionCost { get; set; }
    public decimal TotalNetBookValue { get; set; }
    public decimal TotalAccumulatedDepreciation { get; set; }
    public decimal DepreciationMtd { get; set; }
    
    // Pending Actions
    public int PendingTransfers { get; set; }
    public int PendingDisposals { get; set; }
    public int PendingVerifications { get; set; }
    
    // Category Breakdown
    public List<AssetCategorySummaryDto> CategoryBreakdown { get; set; } = new();
    
    // Recent Activity
    public List<AssetActivityItemDto> RecentActivity { get; set; } = new();
}

public class AssetCategorySummaryDto
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int AssetCount { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalNbv { get; set; }
}

public class AssetActivityItemDto
{
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty; // Acquisition, Depreciation, Disposal, etc.
    public string Description { get; set; } = string.Empty;
    public string? AssetCode { get; set; }
    public decimal? Amount { get; set; }
}

// ========== Bulk Operations ==========

public class RequestBulkAssetTransferDto
{
    public required List<Guid> FixedAssetIds { get; set; } = new();
    public required string DestinationDepartment { get; set; } = string.Empty;
    public required string DestinationLocation { get; set; } = string.Empty;
    public required DateTime TransferDate { get; set; }
    public string? Reason { get; set; }
}

public class RequestBulkAssetDisposalDto
{
    public required List<Guid> FixedAssetIds { get; set; } = new();
    public required DateTime DisposalDate { get; set; }
    public required DisposalType DisposalType { get; set; }
    public string? Reason { get; set; }
    public string? BuyerName { get; set; }
    public decimal SaleProceeds { get; set; } = 0;
    public decimal DisposalCost { get; set; } = 0;
}
