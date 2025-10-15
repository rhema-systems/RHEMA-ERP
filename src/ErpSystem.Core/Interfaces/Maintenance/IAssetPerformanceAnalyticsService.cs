using ErpSystem.Core.DTOs.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

/// <summary>
/// Interface for comprehensive asset performance analytics and KPI calculation
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

    /// <summary>
    /// Compare asset performance against internal fleet benchmarks
    /// </summary>
    Task<AssetBenchmarkComparisonDto> CompareAssetPerformanceAsync(
        Guid assetId, string industryType, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Get top and bottom performing assets
    /// </summary>
    Task<AssetPerformanceRankingDto> GetAssetPerformanceRankingAsync(
        Guid tenantId, string metricType, int topCount = 10, int bottomCount = 10);

    #endregion

    #region Predictive Analytics Integration

    /// <summary>
    /// Get asset health trend analysis with predictive insights
    /// </summary>
    Task<AssetHealthTrendDto> GetAssetHealthTrendAsync(Guid assetId, int periodMonths = 12);

    /// <summary>
    /// Predict asset performance degradation
    /// </summary>
    Task<AssetPerformancePredictionDto> PredictAssetPerformanceAsync(
        Guid assetId, int predictionDays = 30);

    /// <summary>
    /// Calculate asset performance trends over time
    /// </summary>
    Task<AssetPerformanceTrendDto[]> GetAssetPerformanceTrendsAsync(
        Guid tenantId, DateTime startDate, DateTime endDate, string period = "Monthly");

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

    /// <summary>
    /// Calculate comprehensive asset performance metrics
    /// </summary>
    Task<AssetPerformanceMetricsDto> CalculateAssetPerformanceMetricsAsync(
        Guid assetId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Get asset performance dashboard with KPIs and trends
    /// </summary>
    Task<AssetPerformanceDashboardDto> GetAssetPerformanceDashboardAsync(
        Guid tenantId, DateTime startDate, DateTime endDate, Guid? assetId = null);

    #endregion

    #region Advanced Analytics

    /// <summary>
    /// Perform root cause analysis on asset performance issues
    /// </summary>
    Task<AssetRootCauseAnalysisDto> PerformRootCauseAnalysisAsync(
        Guid assetId, DateTime incidentDate, string issueType);

    /// <summary>
    /// Calculate asset criticality score based on performance impact
    /// </summary>
    Task<AssetCriticalityAnalysisDto> AnalyzeAssetCriticalityAsync(
        Guid tenantId, Guid assetId);

    /// <summary>
    /// Generate asset optimization recommendations
    /// </summary>
    Task<AssetOptimizationRecommendationDto[]> GenerateOptimizationRecommendationsAsync(
        Guid tenantId, Guid? assetId = null);

    #endregion

    #region Cost Performance Analytics

    /// <summary>
    /// Calculate total cost of ownership for assets
    /// </summary>
    Task<AssetTotalCostOfOwnershipDto> CalculateTotalCostOfOwnershipAsync(
        Guid assetId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Analyze cost per unit of production or service
    /// </summary>
    Task<AssetCostEfficiencyDto> AnalyzeCostEfficiencyAsync(
        Guid assetId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Calculate return on maintenance investment (ROMI)
    /// </summary>
    Task<MaintenanceReturnOnInvestmentDto> CalculateMaintenanceROIAsync(
        Guid assetId, DateTime startDate, DateTime endDate);

    #endregion

    #region Energy & Environmental Performance

    /// <summary>
    /// Analyze energy consumption and efficiency patterns
    /// </summary>
    Task<AssetEnergyPerformanceDto> AnalyzeEnergyPerformanceAsync(
        Guid assetId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Calculate carbon footprint and environmental impact
    /// </summary>
    Task<AssetEnvironmentalImpactDto> CalculateEnvironmentalImpactAsync(
        Guid assetId, DateTime startDate, DateTime endDate);

    #endregion
}
