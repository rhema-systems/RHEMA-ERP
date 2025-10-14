using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for IoT integration and predictive maintenance based on sensor data
/// </summary>
public class IoTMaintenanceService : IIoTMaintenanceService
{
    private readonly IMaintenanceAssetService _assetService;
    private readonly IWorkOrderService _workOrderService;
    private readonly IMaintenanceNotificationService _notificationService;
    private readonly ILogger<IoTMaintenanceService> _logger;
    private readonly ICurrentUserProvider _currentUserProvider;

    public IoTMaintenanceService(
        IMaintenanceAssetService assetService,
        IWorkOrderService workOrderService,
        IMaintenanceNotificationService notificationService,
        ILogger<IoTMaintenanceService> logger,
        ICurrentUserProvider currentUserProvider)
    {
        _assetService = assetService;
        _workOrderService = workOrderService;
        _notificationService = notificationService;
        _logger = logger;
        _currentUserProvider = currentUserProvider;
    }

    #region Sensor Data Processing

    public async Task ProcessSensorDataAsync(SensorDataDto sensorData)
    {
        try
        {
            _logger.LogInformation("Processing sensor data for asset {AssetId}, sensor {SensorId}", 
                sensorData.AssetId, sensorData.SensorId);

            // Validate sensor data
            if (!await ValidateSensorDataAsync(sensorData))
            {
                _logger.LogWarning("Invalid sensor data received for asset {AssetId}", sensorData.AssetId);
                return;
            }

            // Store sensor data (would typically go to time-series database)
            await StoreSensorDataAsync(sensorData);

            // Analyze sensor data for anomalies
            var analysisResults = await AnalyzeSensorDataAsync(sensorData);

            // Process analysis results
            if (analysisResults.HasAnomalies)
            {
                await HandleSensorAnomaliesAsync(sensorData, analysisResults);
            }

            // Update asset health metrics
            await UpdateAssetHealthMetricsAsync(sensorData);

            _logger.LogInformation("Sensor data processed successfully for asset {AssetId}", sensorData.AssetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing sensor data for asset {AssetId}", sensorData.AssetId);
            throw;
        }
    }

    public async Task ProcessBatchSensorDataAsync(IEnumerable<SensorDataDto> sensorDataBatch)
    {
        try
        {
            _logger.LogInformation("Processing batch of {Count} sensor data points", sensorDataBatch.Count());

            var tasks = sensorDataBatch.Select(ProcessSensorDataAsync);
            await Task.WhenAll(tasks);

            _logger.LogInformation("Batch sensor data processing completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing batch sensor data");
            throw;
        }
    }

    #endregion

    #region Predictive Analytics

    public async Task<PredictiveAnalysisResultDto> PerformPredictiveAnalysisAsync(Guid assetId, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            _logger.LogInformation("Performing predictive analysis for asset {AssetId}", assetId);

            var asset = await _assetService.GetAssetByIdAsync(assetId);
            if (asset == null)
                throw new ArgumentException($"Asset with ID {assetId} not found");

            // Get historical sensor data
            var historicalData = await GetHistoricalSensorDataAsync(assetId, startDate, endDate);
            
            // Run predictive algorithms
            var failurePrediction = await PredictFailureProbabilityAsync(assetId, historicalData);
            var maintenanceRecommendations = await GenerateMaintenanceRecommendationsAsync(assetId, historicalData);
            var healthScore = await CalculateAssetHealthScoreAsync(assetId, historicalData);

            var result = new PredictiveAnalysisResultDto
            {
                AssetId = assetId,
                AssetName = asset.Name,
                AnalysisDate = DateTime.UtcNow,
                HealthScore = healthScore,
                FailurePrediction = failurePrediction,
                MaintenanceRecommendations = maintenanceRecommendations,
                TrendAnalysis = await PerformTrendAnalysisAsync(assetId, historicalData),
                AlertLevel = DetermineAlertLevel(healthScore, failurePrediction)
            };

            _logger.LogInformation("Predictive analysis completed for asset {AssetId} - Health Score: {HealthScore}", 
                assetId, healthScore);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing predictive analysis for asset {AssetId}", assetId);
            throw;
        }
    }

    public async Task<List<MaintenanceRecommendationDto>> GenerateMaintenanceRecommendationsAsync(Guid assetId, IEnumerable<SensorDataDto> sensorData)
    {
        try
        {
            _logger.LogInformation("Generating maintenance recommendations for asset {AssetId}", assetId);

            var recommendations = new List<MaintenanceRecommendationDto>();

            // Analyze temperature patterns
            var temperatureData = sensorData.Where(s => s.SensorType == "Temperature").ToList();
            if (temperatureData.Any())
            {
                var avgTemp = temperatureData.Average(s => s.Value);
                var maxTemp = temperatureData.Max(s => s.Value);
                
                if (avgTemp > 75) // Example threshold
                {
                    recommendations.Add(new MaintenanceRecommendationDto
                    {
                        Type = "Preventive",
                        Priority = "High",
                        Action = "Check cooling system and clean filters",
                        Reason = $"Average operating temperature ({avgTemp:F1}°C) exceeds normal range",
                        EstimatedCost = 500m,
                        EstimatedDuration = 2,
                        RecommendedBy = DateTime.UtcNow.AddDays(7)
                    });
                }
            }

            // Analyze vibration patterns
            var vibrationData = sensorData.Where(s => s.SensorType == "Vibration").ToList();
            if (vibrationData.Any())
            {
                var avgVibration = vibrationData.Average(s => s.Value);
                var recentVibration = vibrationData.TakeLast(10).Average(s => s.Value);
                
                if (recentVibration > avgVibration * 1.5)
                {
                    recommendations.Add(new MaintenanceRecommendationDto
                    {
                        Type = "Corrective",
                        Priority = "Critical",
                        Action = "Inspect bearings and alignment",
                        Reason = $"Vibration levels increased by {((recentVibration / avgVibration - 1) * 100):F0}%",
                        EstimatedCost = 1200m,
                        EstimatedDuration = 4,
                        RecommendedBy = DateTime.UtcNow.AddDays(3)
                    });
                }
            }

            // Analyze pressure patterns
            var pressureData = sensorData.Where(s => s.SensorType == "Pressure").ToList();
            if (pressureData.Any())
            {
                var pressureVariance = CalculateVariance(pressureData.Select(s => s.Value));
                
                if (pressureVariance > 10) // Example threshold
                {
                    recommendations.Add(new MaintenanceRecommendationDto
                    {
                        Type = "Preventive",
                        Priority = "Medium",
                        Action = "Inspect pressure relief valves and seals",
                        Reason = $"High pressure variance detected ({pressureVariance:F2})",
                        EstimatedCost = 300m,
                        EstimatedDuration = 1,
                        RecommendedBy = DateTime.UtcNow.AddDays(14)
                    });
                }
            }

            _logger.LogInformation("Generated {Count} maintenance recommendations for asset {AssetId}", 
                recommendations.Count, assetId);

            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating maintenance recommendations for asset {AssetId}", assetId);
            throw;
        }
    }

    #endregion

    #region Asset Health Monitoring

    public async Task<AssetHealthDto> GetAssetHealthAsync(Guid assetId)
    {
        try
        {
            _logger.LogInformation("Getting asset health for asset {AssetId}", assetId);

            var asset = await _assetService.GetAssetByIdAsync(assetId);
            if (asset == null)
                throw new ArgumentException($"Asset with ID {assetId} not found");

            // Get latest sensor readings
            var latestSensorData = await GetLatestSensorDataAsync(assetId);
            
            // Calculate health metrics
            var healthScore = await CalculateAssetHealthScoreAsync(assetId, latestSensorData);
            var operationalStatus = DetermineOperationalStatus(latestSensorData);
            var riskLevel = AssessRiskLevel(healthScore, latestSensorData);

            var assetHealth = new AssetHealthDto
            {
                AssetId = assetId,
                AssetName = asset.Name,
                HealthScore = healthScore,
                OperationalStatus = operationalStatus,
                RiskLevel = riskLevel,
                LastUpdated = DateTime.UtcNow,
                SensorReadings = latestSensorData?.ToDictionary(s => s.SensorType, s => s.Value) ?? new Dictionary<string, double>(),
                HealthTrend = await CalculateHealthTrendAsync(assetId),
                NextMaintenanceDue = await GetNextMaintenanceDueDateAsync(assetId),
                Alerts = await GetActiveAlertsAsync(assetId)
            };

            _logger.LogInformation("Asset health retrieved for asset {AssetId} - Score: {HealthScore}, Status: {Status}", 
                assetId, healthScore, operationalStatus);

            return assetHealth;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting asset health for asset {AssetId}", assetId);
            throw;
        }
    }

    public async Task<List<AssetHealthDto>> GetFleetHealthAsync()
    {
        try
        {
            _logger.LogInformation("Getting fleet health overview");

            var assets = await _assetService.GetAllAssetsAsync();
            var healthTasks = assets.Select(asset => GetAssetHealthAsync(asset.Id));
            var healthResults = await Task.WhenAll(healthTasks);

            _logger.LogInformation("Fleet health overview retrieved for {Count} assets", assets.Count());

            return healthResults.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fleet health overview");
            throw;
        }
    }

    #endregion

    #region Anomaly Detection

    private async Task<SensorAnalysisResultDto> AnalyzeSensorDataAsync(SensorDataDto sensorData)
    {
        try
        {
            // Get historical data for comparison
            var historicalData = await GetRecentSensorDataAsync(sensorData.AssetId, sensorData.SensorType, 100);
            
            if (!historicalData.Any())
            {
                return new SensorAnalysisResultDto { HasAnomalies = false };
            }

            var values = historicalData.Select(s => s.Value).ToList();
            var mean = values.Average();
            var standardDeviation = CalculateStandardDeviation(values);
            
            // Detect anomalies using statistical methods
            var zScore = Math.Abs((sensorData.Value - mean) / standardDeviation);
            var isAnomaly = zScore > 3; // 3-sigma rule
            
            // Additional pattern-based anomaly detection
            var isRapidChange = await DetectRapidChangeAsync(sensorData, historicalData.TakeLast(5));
            var isOutOfRange = await CheckOperatingRangeAsync(sensorData);

            return new SensorAnalysisResultDto
            {
                HasAnomalies = isAnomaly || isRapidChange || isOutOfRange,
                ZScore = zScore,
                IsStatisticalAnomaly = isAnomaly,
                IsRapidChange = isRapidChange,
                IsOutOfRange = isOutOfRange,
                Severity = DetermineAnomalySeverity(zScore, isRapidChange, isOutOfRange),
                Confidence = CalculateConfidence(historicalData.Count(), standardDeviation)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing sensor data");
            return new SensorAnalysisResultDto { HasAnomalies = false };
        }
    }

    private async Task HandleSensorAnomaliesAsync(SensorDataDto sensorData, SensorAnalysisResultDto analysisResults)
    {
        try
        {
            _logger.LogWarning("Sensor anomaly detected for asset {AssetId}, sensor {SensorType}: {Severity}", 
                sensorData.AssetId, sensorData.SensorType, analysisResults.Severity);

            // Create alert based on severity
            if (analysisResults.Severity == "Critical")
            {
                await _notificationService.SendAssetCriticalAlertAsync(sensorData.AssetId, 
                    $"Critical {sensorData.SensorType} sensor anomaly detected (Value: {sensorData.Value})");
                
                // Auto-create emergency work order for critical anomalies
                await CreateEmergencyWorkOrderAsync(sensorData, analysisResults);
            }
            else if (analysisResults.Severity == "High")
            {
                // Schedule inspection work order
                await CreateInspectionWorkOrderAsync(sensorData, analysisResults);
            }

            // Log anomaly for further analysis
            await LogAnomalyAsync(sensorData, analysisResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling sensor anomalies for asset {AssetId}", sensorData.AssetId);
        }
    }

    #endregion

    #region Helper Methods

    private async Task<bool> ValidateSensorDataAsync(SensorDataDto sensorData)
    {
        // Basic validation
        if (sensorData.AssetId == Guid.Empty || string.IsNullOrEmpty(sensorData.SensorType))
            return false;

        // Check if asset exists
        var asset = await _assetService.GetAssetByIdAsync(sensorData.AssetId);
        return asset != null;
    }

    private async Task StoreSensorDataAsync(SensorDataDto sensorData)
    {
        // Mock implementation - would store in time-series database
        _logger.LogDebug("Storing sensor data for asset {AssetId}: {SensorType}={Value}", 
            sensorData.AssetId, sensorData.SensorType, sensorData.Value);
    }

    private async Task<IEnumerable<SensorDataDto>> GetHistoricalSensorDataAsync(Guid assetId, DateTime? startDate, DateTime? endDate)
    {
        // Mock implementation - would query time-series database
        var mockData = GenerateMockSensorData(assetId, startDate ?? DateTime.UtcNow.AddDays(-30), endDate ?? DateTime.UtcNow);
        return await Task.FromResult(mockData);
    }

    private async Task<IEnumerable<SensorDataDto>> GetLatestSensorDataAsync(Guid assetId)
    {
        // Mock implementation - would get latest readings from each sensor
        var mockData = new List<SensorDataDto>
        {
            new SensorDataDto { AssetId = assetId, SensorType = "Temperature", Value = 72.5, Timestamp = DateTime.UtcNow },
            new SensorDataDto { AssetId = assetId, SensorType = "Vibration", Value = 2.3, Timestamp = DateTime.UtcNow },
            new SensorDataDto { AssetId = assetId, SensorType = "Pressure", Value = 145.2, Timestamp = DateTime.UtcNow }
        };
        return await Task.FromResult(mockData);
    }

    private async Task<IEnumerable<SensorDataDto>> GetRecentSensorDataAsync(Guid assetId, string sensorType, int count)
    {
        // Mock implementation - would get recent data for specific sensor type
        var mockData = Enumerable.Range(0, count)
            .Select(i => new SensorDataDto
            {
                AssetId = assetId,
                SensorType = sensorType,
                Value = GetMockSensorValue(sensorType) + (Random.Shared.NextDouble() - 0.5) * 10,
                Timestamp = DateTime.UtcNow.AddMinutes(-i * 5)
            }).ToList();

        return await Task.FromResult(mockData);
    }

    private async Task<FailurePredictionDto> PredictFailureProbabilityAsync(Guid assetId, IEnumerable<SensorDataDto> historicalData)
    {
        // Mock predictive algorithm - would use ML models in reality
        var random = new Random();
        var riskFactors = new List<string>();
        
        if (historicalData.Where(s => s.SensorType == "Temperature").Average(s => s.Value) > 75)
            riskFactors.Add("High operating temperature");
            
        if (historicalData.Where(s => s.SensorType == "Vibration").Average(s => s.Value) > 3.0)
            riskFactors.Add("Elevated vibration levels");

        var failureProbability = Math.Min(95, riskFactors.Count * 15 + random.Next(0, 20));

        return await Task.FromResult(new FailurePredictionDto
        {
            FailureProbability = failureProbability,
            TimeToFailure = failureProbability > 50 ? TimeSpan.FromDays(30 - failureProbability / 3) : TimeSpan.FromDays(90),
            RiskFactors = riskFactors,
            Confidence = Math.Max(60, 100 - riskFactors.Count * 10),
            PredictionDate = DateTime.UtcNow
        });
    }

    private async Task<double> CalculateAssetHealthScoreAsync(Guid assetId, IEnumerable<SensorDataDto> sensorData)
    {
        if (!sensorData.Any())
            return 75; // Default score when no data

        var scores = new List<double>();

        // Temperature score
        var tempData = sensorData.Where(s => s.SensorType == "Temperature").ToList();
        if (tempData.Any())
        {
            var avgTemp = tempData.Average(s => s.Value);
            var tempScore = Math.Max(0, 100 - Math.Max(0, avgTemp - 70) * 2);
            scores.Add(tempScore);
        }

        // Vibration score
        var vibrationData = sensorData.Where(s => s.SensorType == "Vibration").ToList();
        if (vibrationData.Any())
        {
            var avgVibration = vibrationData.Average(s => s.Value);
            var vibrationScore = Math.Max(0, 100 - Math.Max(0, avgVibration - 2.0) * 30);
            scores.Add(vibrationScore);
        }

        // Pressure score
        var pressureData = sensorData.Where(s => s.SensorType == "Pressure").ToList();
        if (pressureData.Any())
        {
            var avgPressure = pressureData.Average(s => s.Value);
            var pressureScore = Math.Max(0, 100 - Math.Abs(avgPressure - 150) * 0.5);
            scores.Add(pressureScore);
        }

        return await Task.FromResult(scores.Any() ? scores.Average() : 75);
    }

    private async Task UpdateAssetHealthMetricsAsync(SensorDataDto sensorData)
    {
        // Mock implementation - would update asset health metrics in database
        _logger.LogDebug("Updating health metrics for asset {AssetId}", sensorData.AssetId);
    }

    private List<SensorDataDto> GenerateMockSensorData(Guid assetId, DateTime startDate, DateTime endDate)
    {
        var data = new List<SensorDataDto>();
        var current = startDate;
        var interval = TimeSpan.FromHours(1);

        while (current <= endDate)
        {
            data.Add(new SensorDataDto
            {
                AssetId = assetId,
                SensorType = "Temperature",
                Value = 70 + Math.Sin(current.Hour * Math.PI / 12) * 5 + Random.Shared.NextDouble() * 4 - 2,
                Timestamp = current
            });

            data.Add(new SensorDataDto
            {
                AssetId = assetId,
                SensorType = "Vibration",
                Value = 2.0 + Random.Shared.NextDouble() * 1.0,
                Timestamp = current
            });

            data.Add(new SensorDataDto
            {
                AssetId = assetId,
                SensorType = "Pressure",
                Value = 150 + Random.Shared.NextDouble() * 20 - 10,
                Timestamp = current
            });

            current = current.Add(interval);
        }

        return data;
    }

    private double GetMockSensorValue(string sensorType)
    {
        return sensorType switch
        {
            "Temperature" => 72.0,
            "Vibration" => 2.5,
            "Pressure" => 148.0,
            _ => 0.0
        };
    }

    private double CalculateVariance(IEnumerable<double> values)
    {
        var mean = values.Average();
        return values.Select(v => Math.Pow(v - mean, 2)).Average();
    }

    private double CalculateStandardDeviation(IEnumerable<double> values)
    {
        return Math.Sqrt(CalculateVariance(values));
    }

    private async Task<bool> DetectRapidChangeAsync(SensorDataDto current, IEnumerable<SensorDataDto> recent)
    {
        if (!recent.Any()) return false;
        
        var avgRecent = recent.Average(s => s.Value);
        var changePercent = Math.Abs((current.Value - avgRecent) / avgRecent) * 100;
        
        return changePercent > 20; // 20% change threshold
    }

    private async Task<bool> CheckOperatingRangeAsync(SensorDataDto sensorData)
    {
        // Mock operating ranges - would be configured per sensor type and asset
        var ranges = new Dictionary<string, (double Min, double Max)>
        {
            ["Temperature"] = (0, 85),
            ["Vibration"] = (0, 5),
            ["Pressure"] = (100, 200)
        };

        if (ranges.TryGetValue(sensorData.SensorType, out var range))
        {
            return sensorData.Value < range.Min || sensorData.Value > range.Max;
        }

        return false;
    }

    private string DetermineAnomalySeverity(double zScore, bool isRapidChange, bool isOutOfRange)
    {
        if (zScore > 4 || isOutOfRange) return "Critical";
        if (zScore > 3 || isRapidChange) return "High";
        if (zScore > 2) return "Medium";
        return "Low";
    }

    private double CalculateConfidence(int dataPoints, double standardDeviation)
    {
        // Mock confidence calculation
        var baseConfidence = Math.Min(95, dataPoints * 0.5 + 50);
        var variabilityPenalty = Math.Min(20, standardDeviation * 2);
        return Math.Max(50, baseConfidence - variabilityPenalty);
    }

    private string DetermineOperationalStatus(IEnumerable<SensorDataDto> sensorData)
    {
        if (!sensorData.Any()) return "Unknown";
        
        var healthScore = CalculateAssetHealthScoreAsync(Guid.Empty, sensorData).Result;
        
        return healthScore switch
        {
            >= 80 => "Excellent",
            >= 60 => "Good",
            >= 40 => "Fair",
            >= 20 => "Poor",
            _ => "Critical"
        };
    }

    private string AssessRiskLevel(double healthScore, IEnumerable<SensorDataDto> sensorData)
    {
        return healthScore switch
        {
            >= 80 => "Low",
            >= 60 => "Medium",
            >= 40 => "High",
            _ => "Critical"
        };
    }

    private async Task<string> CalculateHealthTrendAsync(Guid assetId)
    {
        // Mock trend calculation - would analyze historical health scores
        var trends = new[] { "Improving", "Stable", "Declining" };
        return trends[Random.Shared.Next(trends.Length)];
    }

    private async Task<DateTime?> GetNextMaintenanceDueDateAsync(Guid assetId)
    {
        // Mock implementation - would query maintenance schedules
        return DateTime.UtcNow.AddDays(Random.Shared.Next(1, 30));
    }

    private async Task<List<string>> GetActiveAlertsAsync(Guid assetId)
    {
        // Mock implementation - would query active alerts
        return new List<string>();
    }

    private string DetermineAlertLevel(double healthScore, FailurePredictionDto failurePrediction)
    {
        if (healthScore < 40 || failurePrediction.FailureProbability > 70) return "Critical";
        if (healthScore < 60 || failurePrediction.FailureProbability > 40) return "High";
        if (healthScore < 80 || failurePrediction.FailureProbability > 20) return "Medium";
        return "Low";
    }

    private async Task<TrendAnalysisDto> PerformTrendAnalysisAsync(Guid assetId, IEnumerable<SensorDataDto> historicalData)
    {
        // Mock trend analysis
        return new TrendAnalysisDto
        {
            OverallTrend = "Stable",
            TemperatureTrend = "Slightly Increasing",
            VibrationTrend = "Stable",
            PressureTrend = "Decreasing",
            AnalysisPeriod = TimeSpan.FromDays(30),
            Confidence = 85
        };
    }

    private async Task CreateEmergencyWorkOrderAsync(SensorDataDto sensorData, SensorAnalysisResultDto analysisResults)
    {
        // Mock implementation - would create emergency work order
        _logger.LogInformation("Creating emergency work order for critical sensor anomaly on asset {AssetId}", sensorData.AssetId);
    }

    private async Task CreateInspectionWorkOrderAsync(SensorDataDto sensorData, SensorAnalysisResultDto analysisResults)
    {
        // Mock implementation - would create inspection work order
        _logger.LogInformation("Creating inspection work order for sensor anomaly on asset {AssetId}", sensorData.AssetId);
    }

    private async Task LogAnomalyAsync(SensorDataDto sensorData, SensorAnalysisResultDto analysisResults)
    {
        // Mock implementation - would log to anomaly database
        _logger.LogInformation("Logging anomaly for asset {AssetId}: {Severity}", sensorData.AssetId, analysisResults.Severity);
    }

    #endregion
}

#region Supporting DTOs

public class SensorDataDto
{
    public Guid AssetId { get; set; }
    public string SensorId { get; set; } = string.Empty;
    public string SensorType { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = "Normal";
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class SensorAnalysisResultDto
{
    public bool HasAnomalies { get; set; }
    public double ZScore { get; set; }
    public bool IsStatisticalAnomaly { get; set; }
    public bool IsRapidChange { get; set; }
    public bool IsOutOfRange { get; set; }
    public string Severity { get; set; } = "Low";
    public double Confidence { get; set; }
}

public class PredictiveAnalysisResultDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public DateTime AnalysisDate { get; set; }
    public double HealthScore { get; set; }
    public FailurePredictionDto FailurePrediction { get; set; } = null!;
    public List<MaintenanceRecommendationDto> MaintenanceRecommendations { get; set; } = new();
    public TrendAnalysisDto TrendAnalysis { get; set; } = null!;
    public string AlertLevel { get; set; } = "Low";
}

public class FailurePredictionDto
{
    public double FailureProbability { get; set; }
    public TimeSpan TimeToFailure { get; set; }
    public List<string> RiskFactors { get; set; } = new();
    public double Confidence { get; set; }
    public DateTime PredictionDate { get; set; }
}

public class MaintenanceRecommendationDto
{
    public string Type { get; set; } = string.Empty; // Preventive, Corrective, Predictive
    public string Priority { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal EstimatedCost { get; set; }
    public int EstimatedDuration { get; set; } // hours
    public DateTime RecommendedBy { get; set; }
}

public class AssetHealthDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public double HealthScore { get; set; }
    public string OperationalStatus { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    public Dictionary<string, double> SensorReadings { get; set; } = new();
    public string HealthTrend { get; set; } = string.Empty;
    public DateTime? NextMaintenanceDue { get; set; }
    public List<string> Alerts { get; set; } = new();
}

public class TrendAnalysisDto
{
    public string OverallTrend { get; set; } = string.Empty;
    public string TemperatureTrend { get; set; } = string.Empty;
    public string VibrationTrend { get; set; } = string.Empty;
    public string PressureTrend { get; set; } = string.Empty;
    public TimeSpan AnalysisPeriod { get; set; }
    public double Confidence { get; set; }
}

#endregion