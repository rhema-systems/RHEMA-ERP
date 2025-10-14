using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Services.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

/// <summary>
/// Interface for enhanced asset performance analytics service with advanced KPI calculations and performance metrics
/// </summary>
public interface IAssetPerformanceAnalyticsService
{
    #region Overall Equipment Effectiveness (OEE) Analytics

    /// <summary>
    /// Calculate OEE for a specific asset over a time period
    /// </summary>
    Task<OeeAnalysisDto> CalculateOeeAsync(Guid assetId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Calculate OEE for all assets (fleet-wide analysis)
    /// </summary>
    Task<List<OeeAnalysisDto>> CalculateFleetOeeAsync(DateTime startDate, DateTime endDate);

    #endregion

    #region Asset Reliability Analytics

    /// <summary>
    /// Calculate comprehensive reliability metrics for an asset
    /// </summary>
    Task<AssetReliabilityMetricsDto> CalculateReliabilityMetricsAsync(Guid assetId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Get asset reliability rankings across the fleet
    /// </summary>
    Task<List<AssetReliabilityRankingDto>> GetAssetReliabilityRankingsAsync(DateTime startDate, DateTime endDate, int topCount = 10);

    #endregion

    #region Performance Benchmarking

    /// <summary>
    /// Get performance benchmark comparison for an asset against industry standards
    /// </summary>
    Task<AssetPerformanceBenchmarkDto> GetPerformanceBenchmarkAsync(Guid assetId, string benchmarkCategory = "Industry");

    #endregion

    #region Predictive Analytics Integration

    /// <summary>
    /// Get asset health trend analysis with predictive insights
    /// </summary>
    Task<AssetHealthTrendDto> GetAssetHealthTrendAsync(Guid assetId, int periodMonths = 12);

    #endregion

    #region Advanced KPI Calculations

    /// <summary>
    /// Get comprehensive asset KPI dashboard
    /// </summary>
    Task<AssetKpiDashboardDto> GetAssetKpiDashboardAsync(Guid? assetId = null, DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// Calculate advanced KPIs by category
    /// </summary>
    Task<List<AssetPerformanceKpiDto>> CalculateAdvancedKpisAsync(DateTime startDate, DateTime endDate, string kpiCategory = "All");

    #endregion
}