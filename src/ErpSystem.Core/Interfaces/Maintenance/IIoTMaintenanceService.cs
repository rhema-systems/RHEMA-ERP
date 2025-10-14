using ErpSystem.Core.Services.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

/// <summary>
/// Interface for IoT integration and predictive maintenance based on sensor data
/// </summary>
public interface IIoTMaintenanceService
{
    /// <summary>
    /// Processes individual sensor data point
    /// </summary>
    /// <param name="sensorData">Sensor data to process</param>
    Task ProcessSensorDataAsync(SensorDataDto sensorData);

    /// <summary>
    /// Processes batch of sensor data points
    /// </summary>
    /// <param name="sensorDataBatch">Collection of sensor data to process</param>
    Task ProcessBatchSensorDataAsync(IEnumerable<SensorDataDto> sensorDataBatch);

    /// <summary>
    /// Performs predictive analysis for an asset
    /// </summary>
    /// <param name="assetId">Asset ID to analyze</param>
    /// <param name="startDate">Analysis start date (optional)</param>
    /// <param name="endDate">Analysis end date (optional)</param>
    /// <returns>Predictive analysis results</returns>
    Task<PredictiveAnalysisResultDto> PerformPredictiveAnalysisAsync(Guid assetId, DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// Generates maintenance recommendations based on sensor data
    /// </summary>
    /// <param name="assetId">Asset ID</param>
    /// <param name="sensorData">Historical sensor data</param>
    /// <returns>List of maintenance recommendations</returns>
    Task<List<MaintenanceRecommendationDto>> GenerateMaintenanceRecommendationsAsync(Guid assetId, IEnumerable<SensorDataDto> sensorData);

    /// <summary>
    /// Gets current health status of an asset
    /// </summary>
    /// <param name="assetId">Asset ID</param>
    /// <returns>Asset health information</returns>
    Task<AssetHealthDto> GetAssetHealthAsync(Guid assetId);

    /// <summary>
    /// Gets fleet-wide health overview
    /// </summary>
    /// <returns>List of asset health information for all assets</returns>
    Task<List<AssetHealthDto>> GetFleetHealthAsync();
}