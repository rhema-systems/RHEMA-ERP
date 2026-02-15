using ErpSystem.Core.DTOs.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

public interface IAssetAdmissionService
{
    Task<PagedResult<AssetAdmissionDto>> GetAdmissionsAsync(AdmissionQueryParameters query);
    Task<AssetAdmissionDto?> GetAdmissionByIdAsync(Guid id);
    Task<AssetAdmissionDto> CreateAdmissionAsync(CreateAssetAdmissionDto dto);
    Task<AssetAdmissionDto> UpdateAdmissionAsync(Guid id, UpdateAssetAdmissionDto dto);
    Task CancelAdmissionAsync(Guid id, string? reason = null);
    Task<IEnumerable<AssetAdmissionDto>> GetActiveAdmissionsAsync();
    Task<IEnumerable<AssetAdmissionDto>> GetAdmissionsByAssetAsync(Guid assetId);
    Task<IEnumerable<AssetAdmissionDto>> GetAdmissionsByWorkOrderAsync(Guid workOrderId);
    Task<AdmissionStatsDto> GetAdmissionStatsAsync(AdmissionStatsQuery query);
    Task<DowntimeSummaryDto> GetAdmissionDowntimeReportAsync(DowntimeReportQuery query);
}

public class AdmissionQueryParameters
{
    public string? Status { get; set; }
    public string? AdmissionType { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid? JobCardId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class AdmissionStatsQuery
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? AssetId { get; set; }
    public string? AdmissionType { get; set; }
    public string? Status { get; set; }
}

public class AdmissionStatsDto
{
    public int TotalActive { get; set; }
    public int TotalCompleted { get; set; }
    public double AverageStayDays { get; set; }
    public List<AdmissionConditionStatDto> ByCondition { get; set; } = new();
    public List<AdmissionTypeStatDto> ByType { get; set; } = new();
}

public class AdmissionConditionStatDto
{
    public string Condition { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class AdmissionTypeStatDto
{
    public string Type { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DowntimeReportQuery
{
    public Guid? AssetId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class DowntimeSummaryDto
{
    public double TotalDowntimeHours { get; set; }
    public double AverageDowntimeHours { get; set; }
    public List<DowntimeByAssetDto> DowntimeByAsset { get; set; } = new();
}

public class DowntimeByAssetDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public double TotalDowntimeHours { get; set; }
    public double AverageDowntimeHours { get; set; }
    public int IncidentCount { get; set; }
}

