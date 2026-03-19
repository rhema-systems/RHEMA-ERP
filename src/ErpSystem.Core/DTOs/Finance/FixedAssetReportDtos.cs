using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Finance;

public class FixedAssetReportQueryDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? CategoryId { get; set; }
    public FixedAssetStatus? Status { get; set; }
    public string? SearchTerm { get; set; }
}

public class FixedAssetRegisterDto
{
    public List<FixedAssetRegisterItemDto> Items { get; set; } = new();
    public decimal TotalCost { get; set; }
    public decimal TotalAccumulatedDepreciation { get; set; }
    public decimal TotalNetBookValue { get; set; }
}

public class FixedAssetRegisterItemDto
{
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public DateTime AcquisitionDate { get; set; }
    public decimal Cost { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public decimal NetBookValue { get; set; }
    public FixedAssetStatus Status { get; set; }
    public string? SerialNumber { get; set; }
    public string? Location { get; set; }
}

public class AssetDisposalReportDto
{
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime DisposalDate { get; set; }
    public DisposalType DisposalType { get; set; }
    public decimal SaleProceeds { get; set; }
    public decimal DisposalCost { get; set; }
    public decimal NetBookValue { get; set; }
    public decimal GainLoss { get; set; }
    public string? BuyerName { get; set; }
}

public class AssetTransferReportDto
{
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; }
    public string? FromLocation { get; set; }
    public string? ToLocation { get; set; }
    public string? FromDepartment { get; set; }
    public string? ToDepartment { get; set; }
    public string? Reason { get; set; }
}

public class VerificationSummaryDto
{
    public Guid SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int TotalItems { get; set; }
    public int VerifiedItems { get; set; }
    public int MissingItems { get; set; }
    public List<AssetConditionSummaryDto> ConditionSummary { get; set; } = new();
    public List<AssetVerificationItemDto> DetailedItems { get; set; } = new();
}

public class AssetConditionSummaryDto
{
    public AssetCondition Condition { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}
