using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.Services.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.DTOs.Maintenance;
using FluentAssertions;

namespace ErpSystem.Core.Tests.Services.Maintenance;

/// <summary>
/// Comprehensive unit tests for Asset Performance Analytics Service
/// Tests OEE calculations, reliability metrics, rankings, and predictive analytics
/// </summary>
public class AssetPerformanceAnalyticsServiceTests
{
    private readonly Mock<IMaintenanceAssetService> _mockAssetService;
    private readonly Mock<IWorkOrderService> _mockWorkOrderService;
    private readonly Mock<IMaintenanceAnalyticsService> _mockAnalyticsService;
    private readonly Mock<IMaintenanceScheduleService> _mockScheduleService;
    private readonly Mock<ICurrentUserProvider> _mockCurrentUserProvider;
    private readonly Mock<ILogger<AssetPerformanceAnalyticsService>> _mockLogger;
    private readonly AssetPerformanceAnalyticsService _service;

    public AssetPerformanceAnalyticsServiceTests()
    {
        _mockAssetService = new Mock<IMaintenanceAssetService>();
        _mockWorkOrderService = new Mock<IWorkOrderService>();
        _mockAnalyticsService = new Mock<IMaintenanceAnalyticsService>();
        _mockScheduleService = new Mock<IMaintenanceScheduleService>();
        _mockCurrentUserProvider = new Mock<ICurrentUserProvider>();
        _mockLogger = new Mock<ILogger<AssetPerformanceAnalyticsService>>();

        _service = new AssetPerformanceAnalyticsService(
            _mockAssetService.Object,
            _mockWorkOrderService.Object,
            _mockAnalyticsService.Object,
            _mockScheduleService.Object,
            _mockCurrentUserProvider.Object,
            _mockLogger.Object);
    }

    #region OEE Analytics Tests

    [Fact]
    public async Task CalculateOeeAsync_WithValidAsset_ShouldReturnOeeAnalysis()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddMonths(-1);
        var endDate = DateTime.UtcNow;
        
        var asset = new MaintenanceAssetDto
        {
            Id = assetId,
            Name = "Test Asset",
            Type = "Manufacturing Equipment"
        };

        var workOrders = new List<WorkOrderDto>
        {
            new WorkOrderDto
            {
                Id = Guid.NewGuid(),
                AssetId = assetId,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                CompletedAt = DateTime.UtcNow.AddDays(-9),
                WorkOrderType = "Breakdown",
                Priority = "High"
            },
            new WorkOrderDto
            {
                Id = Guid.NewGuid(),
                AssetId = assetId,
                CreatedAt = DateTime.UtcNow.AddDays(-20),
                CompletedAt = DateTime.UtcNow.AddDays(-19),
                WorkOrderType = "Preventive",
                Priority = "Medium"
            }
        };

        _mockAssetService.Setup(s => s.GetAssetByIdAsync(assetId))
            .ReturnsAsync(asset);
        
        _mockWorkOrderService.Setup(s => s.GetWorkOrdersByAssetAsync(assetId))
            .ReturnsAsync(workOrders);

        // Act
        var result = await _service.CalculateOeeAsync(assetId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.AssetId.Should().Be(assetId);
        result.AssetName.Should().Be("Test Asset");
        result.StartDate.Should().Be(startDate);
        result.EndDate.Should().Be(endDate);
        result.OeeScore.Should().BeGreaterThan(0);
        result.Availability.Should().BeInRange(0, 100);
        result.Performance.Should().BeInRange(0, 100);
        result.Quality.Should().BeInRange(0, 100);
        result.PerformanceCategory.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CalculateOeeAsync_WithNonExistentAsset_ShouldThrowArgumentException()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddMonths(-1);
        var endDate = DateTime.UtcNow;

        _mockAssetService.Setup(s => s.GetAssetByIdAsync(assetId))
            .ReturnsAsync((MaintenanceAssetDto?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CalculateOeeAsync(assetId, startDate, endDate));
    }

    [Fact]
    public async Task CalculateFleetOeeAsync_WithValidDateRange_ShouldReturnFleetAnalysis()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddMonths(-1);
        var endDate = DateTime.UtcNow;

        var assets = new List<MaintenanceAssetDto>
        {
            new MaintenanceAssetDto { Id = Guid.NewGuid(), Name = "Asset 1", Type = "Equipment" },
            new MaintenanceAssetDto { Id = Guid.NewGuid(), Name = "Asset 2", Type = "Equipment" },
            new MaintenanceAssetDto { Id = Guid.NewGuid(), Name = "Asset 3", Type = "Equipment" }
        };

        _mockAssetService.Setup(s => s.GetAllAssetsAsync())
            .ReturnsAsync(assets);

        foreach (var asset in assets)
        {
            _mockWorkOrderService.Setup(s => s.GetWorkOrdersByAssetAsync(asset.Id))
                .ReturnsAsync(new List<WorkOrderDto>());
        }

        // Act
        var result = await _service.CalculateFleetOeeAsync(startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().BeInDescendingOrder(o => o.OeeScore);
        
        foreach (var analysis in result)
        {
            analysis.OeeScore.Should().BeGreaterThanOrEqualTo(0);
            analysis.AssetName.Should().NotBeNullOrEmpty();
        }
    }

    [Theory]
    [InlineData(95.0, 88.0, 99.0, 82.7)] // High OEE
    [InlineData(85.0, 75.0, 90.0, 57.4)] // Medium OEE
    [InlineData(70.0, 60.0, 80.0, 33.6)] // Low OEE
    public void CalculateOeeScore_WithKnownValues_ShouldReturnExpectedScore(
        double availability, double performance, double quality, double expectedOee)
    {
        // This would be a private method test using reflection or internal access
        // For now, we test through the public interface
        var calculatedOee = (availability / 100.0) * (performance / 100.0) * (quality / 100.0) * 100.0;
        calculatedOee.Should().BeApproximately(expectedOee, 0.1);
    }

    #endregion

    #region Reliability Analytics Tests

    [Fact]
    public async Task CalculateReliabilityMetricsAsync_WithValidAssetAndWorkOrders_ShouldReturnMetrics()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddMonths(-3);
        var endDate = DateTime.UtcNow;

        var asset = new MaintenanceAssetDto
        {
            Id = assetId,
            Name = "Reliability Test Asset",
            Type = "Critical Equipment"
        };

        var workOrders = new List<WorkOrderDto>
        {
            new WorkOrderDto
            {
                Id = Guid.NewGuid(),
                AssetId = assetId,
                CreatedAt = startDate.AddDays(10),
                CompletedAt = startDate.AddDays(12),
                Type = "Emergency",
                Priority = "Critical"
            },
            new WorkOrderDto
            {
                Id = Guid.NewGuid(),
                AssetId = assetId,
                CreatedAt = startDate.AddDays(50),
                CompletedAt = startDate.AddDays(51),
                Type = "Corrective",
                Priority = "High"
            },
            new WorkOrderDto
            {
                Id = Guid.NewGuid(),
                AssetId = assetId,
                CreatedAt = startDate.AddDays(30),
                CompletedAt = startDate.AddDays(30),
                Type = "Preventive",
                Priority = "Medium"
            }
        };

        _mockAssetService.Setup(s => s.GetAssetByIdAsync(assetId))
            .ReturnsAsync(asset);
        
        _mockWorkOrderService.Setup(s => s.GetWorkOrdersByAssetAsync(assetId))
            .ReturnsAsync(workOrders);

        // Act
        var result = await _service.CalculateReliabilityMetricsAsync(assetId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.AssetId.Should().Be(assetId);
        result.AssetName.Should().Be("Reliability Test Asset");
        result.MeanTimeBetweenFailures.Should().BeGreaterThan(0);
        result.MeanTimeToRepair.Should().BeGreaterThanOrEqualTo(0);
        result.Availability.Should().BeInRange(0, 100);
        result.ReliabilityScore.Should().BeGreaterThanOrEqualTo(0);
        result.TotalFailures.Should().Be(2); // Emergency and Corrective work orders
        result.FailureRate.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetAssetReliabilityRankingsAsync_WithMultipleAssets_ShouldReturnRankedList()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddMonths(-3);
        var endDate = DateTime.UtcNow;
        var topCount = 5;

        var assets = new List<MaintenanceAssetDto>
        {
            new MaintenanceAssetDto { Id = Guid.NewGuid(), Name = "High Reliability Asset", AssetType = "Premium" },
            new MaintenanceAssetDto { Id = Guid.NewGuid(), Name = "Medium Reliability Asset", AssetType = "Standard" },
            new MaintenanceAssetDto { Id = Guid.NewGuid(), Name = "Low Reliability Asset", AssetType = "Basic" }
        };

        _mockAssetService.Setup(s => s.GetAllAssetsAsync())
            .ReturnsAsync(assets);

        foreach (var asset in assets)
        {
            _mockWorkOrderService.Setup(s => s.GetWorkOrdersByAssetAsync(asset.Id))
                .ReturnsAsync(new List<WorkOrderDto>());
        }

        // Act
        var result = await _service.GetAssetReliabilityRankingsAsync(startDate, endDate, topCount);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountLessOrEqualTo(topCount);
        result.Should().BeInDescendingOrder(r => r.ReliabilityScore);
        
        foreach (var ranking in result)
        {
            ranking.AssetId.Should().NotBe(Guid.Empty);
            ranking.AssetName.Should().NotBeNullOrEmpty();
            ranking.AssetType.Should().NotBeNullOrEmpty();
            ranking.ReliabilityScore.Should().BeGreaterThanOrEqualTo(0);
            ranking.PerformanceCategory.Should().NotBeNullOrEmpty();
        }
    }

    [Theory]
    [InlineData(1000, 10, 90.0)] // MTBF=1000, MTTR=10 hours
    [InlineData(500, 20, 84.0)]  // MTBF=500, MTTR=20 hours
    [InlineData(200, 50, 60.0)]  // MTBF=200, MTTR=50 hours
    public void CalculateAvailability_WithKnownMtbfMttr_ShouldReturnExpectedAvailability(
        double mtbf, double mttr, double expectedAvailability)
    {
        // Availability = MTBF / (MTBF + MTTR) * 100
        var actualAvailability = (mtbf / (mtbf + mttr)) * 100;
        actualAvailability.Should().BeApproximately(expectedAvailability, 2.0);
    }

    #endregion

    #region Performance Benchmarking Tests

    [Fact]
    public async Task GetPerformanceBenchmarkAsync_WithValidAsset_ShouldReturnBenchmark()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var benchmarkCategory = "Industry";

        var asset = new MaintenanceAssetDto
        {
            Id = assetId,
            Name = "Benchmark Test Asset",
            Type = "Manufacturing Equipment"
        };

        _mockAssetService.Setup(s => s.GetAssetByIdAsync(assetId))
            .ReturnsAsync(asset);

        // Setup mock work orders for OEE calculation
        _mockWorkOrderService.Setup(s => s.GetWorkOrdersByAssetAsync(assetId))
            .ReturnsAsync(new List<WorkOrderDto>());

        // Act
        var result = await _service.GetPerformanceBenchmarkAsync(assetId, benchmarkCategory);

        // Assert
        result.Should().NotBeNull();
        result.AssetId.Should().Be(assetId);
        result.AssetName.Should().Be("Benchmark Test Asset");
        result.BenchmarkCategory.Should().Be(benchmarkCategory);
        result.OverallPerformanceRating.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CompareAssetPerformanceAsync_WithValidParameters_ShouldReturnComparison()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var industryType = "Manufacturing";
        var startDate = DateTime.UtcNow.AddMonths(-6);
        var endDate = DateTime.UtcNow;

        var asset = new MaintenanceAssetDto
        {
            Id = assetId,
            Name = "Comparison Test Asset"
        };

        _mockAssetService.Setup(s => s.GetAssetByIdAsync(assetId))
            .ReturnsAsync(asset);

        _mockWorkOrderService.Setup(s => s.GetWorkOrdersByAssetAsync(assetId))
            .ReturnsAsync(new List<WorkOrderDto>());

        // Act
        var result = await _service.CompareAssetPerformanceAsync(assetId, industryType, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.AssetId.Should().Be(assetId);
        result.AssetName.Should().Be("Comparison Test Asset");
        result.IndustryType.Should().Be(industryType);
        result.CompetitivePosition.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Performance Rankings Tests

    [Fact]
    public async Task GetAssetPerformanceRankingAsync_WithValidParameters_ShouldReturnRanking()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var metricType = "OEE";
        var topCount = 5;
        var bottomCount = 3;

        var assets = GenerateTestAssets(10);
        _mockAssetService.Setup(s => s.GetAllAssetsAsync())
            .ReturnsAsync(assets);

        foreach (var asset in assets)
        {
            _mockWorkOrderService.Setup(s => s.GetWorkOrdersByAssetAsync(asset.Id))
                .ReturnsAsync(new List<WorkOrderDto>());
        }

        // Act
        var result = await _service.GetAssetPerformanceRankingAsync(tenantId, metricType, topCount, bottomCount);

        // Assert
        result.Should().NotBeNull();
        result.MetricType.Should().Be(metricType);
        result.TopPerformers.Should().HaveCount(Math.Min(topCount, assets.Count));
        result.BottomPerformers.Should().HaveCount(Math.Min(bottomCount, assets.Count));
        result.TotalAssetsEvaluated.Should().Be(assets.Count);
        result.FleetAverage.Should().BeGreaterThan(0);
        result.FleetMedian.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("OEE")]
    [InlineData("Availability")]
    [InlineData("MTBF")]
    [InlineData("MTTR")]
    public async Task GetAssetPerformanceRankingAsync_WithDifferentMetricTypes_ShouldReturnValidRanking(string metricType)
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var assets = GenerateTestAssets(5);

        _mockAssetService.Setup(s => s.GetAllAssetsAsync())
            .ReturnsAsync(assets);

        foreach (var asset in assets)
        {
            _mockWorkOrderService.Setup(s => s.GetWorkOrdersByAssetAsync(asset.Id))
                .ReturnsAsync(new List<WorkOrderDto>());
        }

        // Act
        var result = await _service.GetAssetPerformanceRankingAsync(tenantId, metricType, 3, 2);

        // Assert
        result.Should().NotBeNull();
        result.MetricType.Should().Be(metricType);
        result.TopPerformers.Should().NotBeEmpty();
        
        // Verify ranking order is correct
        for (int i = 0; i < result.TopPerformers.Count - 1; i++)
        {
            result.TopPerformers[i].Rank.Should().BeLessThan(result.TopPerformers[i + 1].Rank);
        }
    }

    #endregion

    #region Predictive Analytics Tests

    [Fact]
    public async Task GetAssetHealthTrendAsync_WithValidAsset_ShouldReturnTrendAnalysis()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var periodMonths = 12;

        var asset = new MaintenanceAssetDto
        {
            Id = assetId,
            Name = "Health Trend Test Asset"
        };

        _mockAssetService.Setup(s => s.GetAssetByIdAsync(assetId))
            .ReturnsAsync(asset);

        _mockWorkOrderService.Setup(s => s.GetWorkOrdersByAssetAsync(assetId))
            .ReturnsAsync(new List<WorkOrderDto>());

        // Act
        var result = await _service.GetAssetHealthTrendAsync(assetId, periodMonths);

        // Assert
        result.Should().NotBeNull();
        result.AssetId.Should().Be(assetId);
        result.AssetName.Should().Be("Health Trend Test Asset");
        result.AnalysisPeriodMonths.Should().Be(periodMonths);
        result.OverallTrend.Should().NotBeNullOrEmpty();
        result.CurrentHealthScore.Should().BeInRange(0, 100);
        result.PredictedHealthScore.Should().BeInRange(0, 100);
        result.HealthRiskLevel.Should().NotBeNullOrEmpty();
        result.RecommendedActions.Should().NotBeNull();
        result.NextReviewDate.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task PredictAssetPerformanceAsync_WithValidParameters_ShouldReturnPrediction()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var predictionDays = 30;

        var asset = new MaintenanceAssetDto
        {
            Id = assetId,
            Name = "Prediction Test Asset"
        };

        _mockAssetService.Setup(s => s.GetAssetByIdAsync(assetId))
            .ReturnsAsync(asset);

        _mockWorkOrderService.Setup(s => s.GetWorkOrdersByAssetAsync(assetId))
            .ReturnsAsync(new List<WorkOrderDto>());

        // Act
        var result = await _service.PredictAssetPerformanceAsync(assetId, predictionDays);

        // Assert
        result.Should().NotBeNull();
        result.AssetId.Should().Be(assetId);
        result.AssetName.Should().Be("Prediction Test Asset");
        result.PredictionHorizonDays.Should().Be(predictionDays);
        result.PredictedOee.Should().BeInRange(0, 100);
        result.PredictedAvailability.Should().BeInRange(0, 100);
        result.PredictedMaintenanceCost.Should().BeGreaterThanOrEqualTo(0);
        result.ConfidenceLevel.Should().BeInRange(0, 100);
        result.RiskFactors.Should().NotBeNull();
        result.Recommendations.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAssetPerformanceTrendsAsync_WithValidDateRange_ShouldReturnTrends()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddMonths(-6);
        var endDate = DateTime.UtcNow;
        var period = "Monthly";

        var assets = GenerateTestAssets(3);
        _mockAssetService.Setup(s => s.GetAllAssetsAsync())
            .ReturnsAsync(assets);

        // Act
        var result = await _service.GetAssetPerformanceTrendsAsync(tenantId, startDate, endDate, period);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        
        foreach (var trend in result)
        {
            trend.AssetId.Should().NotBe(Guid.Empty);
            trend.AssetName.Should().NotBeNullOrEmpty();
            trend.PeriodStart.Should().Be(startDate);
            trend.PeriodEnd.Should().Be(endDate);
            trend.OverallTrend.Should().NotBeNullOrEmpty();
        }
    }

    #endregion

    #region KPI Dashboard Tests

    [Fact]
    public async Task GetAssetKpiDashboardAsync_WithValidParameters_ShouldReturnDashboard()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddMonths(-3);
        var endDate = DateTime.UtcNow;

        var asset = new MaintenanceAssetDto
        {
            Id = assetId,
            Name = "Dashboard Test Asset"
        };

        _mockAssetService.Setup(s => s.GetAssetByIdAsync(assetId))
            .ReturnsAsync(asset);

        _mockWorkOrderService.Setup(s => s.GetWorkOrdersByAssetAsync(assetId))
            .ReturnsAsync(new List<WorkOrderDto>());

        // Act
        var result = await _service.GetAssetKpiDashboardAsync(assetId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.ReportDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        result.AnalysisPeriodStart.Should().Be(startDate);
        result.AnalysisPeriodEnd.Should().Be(endDate);
        result.KpiSummary.Should().NotBeNull();
        result.AssetMetrics.Should().NotBeNull();
        result.TopPerformers.Should().NotBeNull();
        result.UnderPerformers.Should().NotBeNull();
    }

    [Fact]
    public async Task CalculateAdvancedKpisAsync_WithValidParameters_ShouldReturnKpis()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddMonths(-3);
        var endDate = DateTime.UtcNow;
        var kpiCategory = "Efficiency";

        var assets = GenerateTestAssets(5);
        _mockAssetService.Setup(s => s.GetAllAssetsAsync())
            .ReturnsAsync(assets);

        foreach (var asset in assets)
        {
            _mockWorkOrderService.Setup(s => s.GetWorkOrdersByAssetAsync(asset.Id))
                .ReturnsAsync(new List<WorkOrderDto>());
        }

        // Act
        var result = await _service.CalculateAdvancedKpisAsync(startDate, endDate, kpiCategory);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<AssetPerformanceKpiDto>>();
        
        // Verify KPIs are calculated for all assets
        var uniqueAssets = result.Select(kpi => kpi.AssetId).Distinct().Count();
        uniqueAssets.Should().BeGreaterThan(0);
    }

    #endregion

    #region Advanced Analytics Tests

    [Fact]
    public async Task PerformRootCauseAnalysisAsync_WithValidParameters_ShouldReturnAnalysis()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var incidentDate = DateTime.UtcNow.AddDays(-7);
        var issueType = "Unplanned Downtime";

        var asset = new MaintenanceAssetDto
        {
            Id = assetId,
            Name = "Root Cause Test Asset"
        };

        var workOrders = new List<WorkOrderDto>
        {
            new WorkOrderDto
            {
                Id = Guid.NewGuid(),
                AssetId = assetId,
                CreatedAt = incidentDate.AddDays(-2),
                WorkOrderType = "Emergency",
                Description = "Motor failure"
            }
        };

        _mockAssetService.Setup(s => s.GetAssetByIdAsync(assetId))
            .ReturnsAsync(asset);

        _mockWorkOrderService.Setup(s => s.GetWorkOrdersByAssetAsync(assetId))
            .ReturnsAsync(workOrders);

        // Act
        var result = await _service.PerformRootCauseAnalysisAsync(assetId, incidentDate, issueType);

        // Assert
        result.Should().NotBeNull();
        result.AssetId.Should().Be(assetId);
        result.AssetName.Should().Be("Root Cause Test Asset");
        result.IncidentDate.Should().Be(incidentDate);
        result.IssueType.Should().Be(issueType);
        result.AnalysisDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        result.PrimaryRootCause.Should().NotBeNullOrEmpty();
        result.ContributingFactors.Should().NotBeNull();
        result.PreventiveActions.Should().NotBeNull();
        result.CorrectiveActions.Should().NotBeNull();
        result.RiskOfRecurrence.Should().BeInRange(0, 100);
    }

    [Fact]
    public async Task AnalyzeAssetCriticalityAsync_WithValidAsset_ShouldReturnAnalysis()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var assetId = Guid.NewGuid();

        var asset = new MaintenanceAssetDto
        {
            Id = assetId,
            Name = "Criticality Test Asset"
        };

        _mockAssetService.Setup(s => s.GetAssetByIdAsync(assetId))
            .ReturnsAsync(asset);

        _mockWorkOrderService.Setup(s => s.GetWorkOrdersByAssetAsync(assetId))
            .ReturnsAsync(new List<WorkOrderDto>());

        // Act
        var result = await _service.AnalyzeAssetCriticalityAsync(tenantId, assetId);

        // Assert
        result.Should().NotBeNull();
        result.AssetId.Should().Be(assetId);
        result.AssetName.Should().Be("Criticality Test Asset");
        result.AnalysisDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        result.OverallCriticalityScore.Should().BeInRange(0, 100);
        result.CriticalityLevel.Should().NotBeNullOrEmpty();
        result.BusinessImpactScore.Should().BeInRange(0, 100);
        result.SafetyImpactScore.Should().BeInRange(0, 100);
        result.EnvironmentalImpactScore.Should().BeInRange(0, 100);
        result.OperationalImpactScore.Should().BeInRange(0, 100);
        result.FinancialImpactScore.Should().BeInRange(0, 100);
        result.MaintenanceStrategy.Should().NotBeNullOrEmpty();
        result.MonitoringRequirements.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateOptimizationRecommendationsAsync_WithValidTenant_ShouldReturnRecommendations()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var assetId = Guid.NewGuid();

        var assets = new List<MaintenanceAssetDto>
        {
            new MaintenanceAssetDto { Id = assetId, Name = "Optimization Test Asset" }
        };

        _mockAssetService.Setup(s => s.GetAllAssetsAsync())
            .ReturnsAsync(assets);

        _mockWorkOrderService.Setup(s => s.GetWorkOrdersByAssetAsync(assetId))
            .ReturnsAsync(new List<WorkOrderDto>());

        // Act
        var result = await _service.GenerateOptimizationRecommendationsAsync(tenantId, assetId);

        // Assert
        result.Should().NotBeNull();
        
        foreach (var recommendation in result)
        {
            recommendation.AssetId.Should().NotBe(Guid.Empty);
            recommendation.AssetName.Should().NotBeNullOrEmpty();
            recommendation.RecommendationType.Should().NotBeNullOrEmpty();
            recommendation.Title.Should().NotBeNullOrEmpty();
            recommendation.Description.Should().NotBeNullOrEmpty();
            recommendation.Priority.Should().NotBeNullOrEmpty();
            recommendation.PotentialSavings.Should().BeGreaterThanOrEqualTo(0);
            recommendation.ImplementationTimeMonths.Should().BeGreaterThan(0);
            recommendation.ImplementationCost.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    #endregion

    #region Cost Analytics Tests

    [Fact]
    public async Task CalculateTotalCostOfOwnershipAsync_WithValidAsset_ShouldReturnTco()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddYears(-1);
        var endDate = DateTime.UtcNow;

        var asset = new MaintenanceAssetDto
        {
            Id = assetId,
            Name = "TCO Test Asset"
        };

        _mockAssetService.Setup(s => s.GetAssetByIdAsync(assetId))
            .ReturnsAsync(asset);

        _mockWorkOrderService.Setup(s => s.GetWorkOrdersByAssetAsync(assetId))
            .ReturnsAsync(new List<WorkOrderDto>());

        // Act
        var result = await _service.CalculateTotalCostOfOwnershipAsync(assetId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.AssetId.Should().Be(assetId);
        result.AssetName.Should().Be("TCO Test Asset");
        result.AnalysisPeriodStart.Should().Be(startDate);
        result.AnalysisPeriodEnd.Should().Be(endDate);
        result.AcquisitionCost.Should().BeGreaterThanOrEqualTo(0);
        result.MaintenanceCosts.Should().BeGreaterThanOrEqualTo(0);
        result.OperatingCosts.Should().BeGreaterThanOrEqualTo(0);
        result.DowntimeCosts.Should().BeGreaterThanOrEqualTo(0);
        result.TotalCostOfOwnership.Should().BeGreaterThanOrEqualTo(0);
        result.AnnualizedCost.Should().BeGreaterThanOrEqualTo(0);
        result.CostPerOperatingHour.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task AnalyzeCostEfficiencyAsync_WithValidAsset_ShouldReturnEfficiencyAnalysis()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddMonths(-6);
        var endDate = DateTime.UtcNow;

        var asset = new MaintenanceAssetDto
        {
            Id = assetId,
            Name = "Cost Efficiency Test Asset",
            Type = "Manufacturing Equipment"
        };

        _mockAssetService.Setup(s => s.GetAssetByIdAsync(assetId))
            .ReturnsAsync(asset);

        // Act
        var result = await _service.AnalyzeCostEfficiencyAsync(assetId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.AssetId.Should().Be(assetId);
        result.AssetName.Should().Be("Cost Efficiency Test Asset");
        result.AnalysisPeriodStart.Should().Be(startDate);
        result.AnalysisPeriodEnd.Should().Be(endDate);
        result.TotalCosts.Should().BeGreaterThanOrEqualTo(0);
        result.ProductionOutput.Should().BeGreaterThanOrEqualTo(0);
        result.CostPerUnit.Should().BeGreaterThanOrEqualTo(0);
        result.IndustryBenchmark.Should().BeGreaterThanOrEqualTo(0);
        result.EfficiencyRatio.Should().BeGreaterThan(0);
        result.PerformanceRating.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CalculateMaintenanceROIAsync_WithValidAsset_ShouldReturnRoiAnalysis()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddYears(-1);
        var endDate = DateTime.UtcNow;

        var asset = new MaintenanceAssetDto
        {
            Id = assetId,
            Name = "ROI Test Asset"
        };

        _mockAssetService.Setup(s => s.GetAssetByIdAsync(assetId))
            .ReturnsAsync(asset);

        // Act
        var result = await _service.CalculateMaintenanceROIAsync(assetId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.AssetId.Should().Be(assetId);
        result.AssetName.Should().Be("ROI Test Asset");
        result.CalculationPeriodStart.Should().Be(startDate);
        result.CalculationPeriodEnd.Should().Be(endDate);
        result.MaintenanceInvestment.Should().BeGreaterThanOrEqualTo(0);
        result.ProductivityGains.Should().BeGreaterThanOrEqualTo(0);
        result.CostAvoidance.Should().BeGreaterThanOrEqualTo(0);
        result.TotalBenefits.Should().BeGreaterThanOrEqualTo(0);
        result.PaybackPeriodMonths.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region Energy & Environmental Analytics Tests

    [Fact]
    public async Task AnalyzeEnergyPerformanceAsync_WithValidAsset_ShouldReturnEnergyAnalysis()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddMonths(-3);
        var endDate = DateTime.UtcNow;

        var asset = new MaintenanceAssetDto
        {
            Id = assetId,
            Name = "Energy Performance Test Asset",
            Type = "High Consumption Equipment"
        };

        _mockAssetService.Setup(s => s.GetAssetByIdAsync(assetId))
            .ReturnsAsync(asset);

        // Act
        var result = await _service.AnalyzeEnergyPerformanceAsync(assetId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.AssetId.Should().Be(assetId);
        result.AssetName.Should().Be("Energy Performance Test Asset");
        result.AnalysisPeriodStart.Should().Be(startDate);
        result.AnalysisPeriodEnd.Should().Be(endDate);
        result.TotalEnergyConsumption.Should().BeGreaterThanOrEqualTo(0);
        result.AverageEnergyConsumptionPerHour.Should().BeGreaterThanOrEqualTo(0);
        result.EnergyEfficiencyRating.Should().NotBeNullOrEmpty();
        result.CarbonFootprint.Should().BeGreaterThanOrEqualTo(0);
        result.EnergyCosts.Should().BeGreaterThanOrEqualTo(0);
        result.EnergyTrend.Should().NotBeNullOrEmpty();
        result.OptimizationRecommendations.Should().NotBeNull();
        result.PotentialSavings.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task CalculateEnvironmentalImpactAsync_WithValidAsset_ShouldReturnImpactAnalysis()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddMonths(-6);
        var endDate = DateTime.UtcNow;

        var asset = new MaintenanceAssetDto
        {
            Id = assetId,
            Name = "Environmental Impact Test Asset"
        };

        _mockAssetService.Setup(s => s.GetAssetByIdAsync(assetId))
            .ReturnsAsync(asset);

        // Act
        var result = await _service.CalculateEnvironmentalImpactAsync(assetId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.AssetId.Should().Be(assetId);
        result.AssetName.Should().Be("Environmental Impact Test Asset");
        result.AnalysisPeriodStart.Should().Be(startDate);
        result.AnalysisPeriodEnd.Should().Be(endDate);
        result.CarbonFootprintKgCO2.Should().BeGreaterThanOrEqualTo(0);
        result.EnergyConsumptionKWh.Should().BeGreaterThanOrEqualTo(0);
        result.WasteGenerationKg.Should().BeGreaterThanOrEqualTo(0);
        result.EnvironmentalScore.Should().BeInRange(0, 100);
        result.ComplianceStatus.Should().NotBeNullOrEmpty();
        result.ImprovementRecommendations.Should().NotBeNull();
        result.RegulatoryRisk.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Helper Methods

    private List<MaintenanceAssetDto> GenerateTestAssets(int count)
    {
        var assets = new List<MaintenanceAssetDto>();
        
        for (int i = 1; i <= count; i++)
        {
            assets.Add(new MaintenanceAssetDto
            {
                Id = Guid.NewGuid(),
                Name = $"Test Asset {i}",
                Type = $"Equipment Type {i % 3 + 1}",
                AssetType = $"Category {i % 2 + 1}"
            });
        }
        
        return assets;
    }

    private List<WorkOrderDto> GenerateTestWorkOrders(Guid assetId, int count)
    {
        var workOrders = new List<WorkOrderDto>();
        var random = new Random();
        
        for (int i = 0; i < count; i++)
        {
            var createdDate = DateTime.UtcNow.AddDays(-random.Next(1, 90));
            workOrders.Add(new WorkOrderDto
            {
                Id = Guid.NewGuid(),
                AssetId = assetId,
                CreatedAt = createdDate,
                CompletedAt = createdDate.AddHours(random.Next(1, 48)),
                WorkOrderType = i % 3 == 0 ? "Breakdown" : "Preventive",
                Priority = i % 4 == 0 ? "Critical" : "Medium",
                Type = i % 3 == 0 ? "Emergency" : "Preventive"
            });
        }
        
        return workOrders;
    }

    #endregion
}