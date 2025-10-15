import axios, { AxiosResponse } from 'axios';
import { API_CONFIG, API_ENDPOINTS } from '../config/api';
import {
  AssetAnalyticsResponse,
  AssetAnalyticsErrorResponse,
  // OEE Types
  OeeAnalysisRequest,
  FleetOeeAnalysisRequest,
  OeeAnalysisResponse,
  OeeAnalysisDto,
  // Reliability Types
  ReliabilityMetricsRequest,
  ReliabilityRankingsRequest,
  AssetReliabilityMetricsDto,
  AssetReliabilityRankingDto,
  // Benchmarking Types
  PerformanceBenchmarkRequest,
  AssetComparisonRequest,
  AssetPerformanceBenchmarkDto,
  BenchmarkComparisonResponse,
  // Rankings Types
  PerformanceRankingRequest,
  AssetPerformanceRankingDto,
  // Predictive Types
  AssetHealthTrendRequest,
  AssetPerformancePredictionRequest,
  AssetHealthTrendDto,
  AssetPerformancePredictionDto,
  AssetPerformanceTrendDto,
  // KPI Dashboard Types
  AssetKpiDashboardRequest,
  AdvancedKpiCalculationRequest,
  AssetPerformanceDashboardResponse,
  AssetPerformanceKpiDto,
  // Advanced Analytics Types
  RootCauseAnalysisRequest,
  AssetCriticalityAnalysisRequest,
  OptimizationRecommendationsRequest,
  AssetRootCauseAnalysisDto,
  AssetCriticalityAnalysisDto,
  AssetOptimizationRecommendationDto,
  // Cost Analytics Types
  TotalCostOfOwnershipRequest,
  CostEfficiencyAnalysisRequest,
  MaintenanceRoiAnalysisRequest,
  AssetTotalCostOfOwnershipDto,
  AssetCostEfficiencyDto,
  MaintenanceReturnOnInvestmentDto,
  // Energy & Environmental Types
  EnergyPerformanceAnalysisRequest,
  EnvironmentalImpactAnalysisRequest,
  AssetEnergyPerformanceDto,
  AssetEnvironmentalImpactDto,
} from '../types/asset-analytics';

// Create axios instance with default config
const apiClient = axios.create({
  baseURL: API_CONFIG.BASE_URL,
  timeout: API_CONFIG.TIMEOUT,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Add request interceptor to include auth token
apiClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('authToken');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// Add response interceptor for error handling
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      // Handle unauthorized - redirect to login
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

/**
 * Asset Analytics API Service
 * Provides methods to interact with all asset analytics endpoints
 */
export class AssetAnalyticsService {
  // #region OEE Analytics Methods

  /**
   * Calculate OEE for a specific asset
   */
  static async calculateAssetOee(
    assetId: string, 
    request: OeeAnalysisRequest
  ): Promise<AssetAnalyticsResponse<OeeAnalysisResponse>> {
    try {
      const response: AxiosResponse<AssetAnalyticsResponse<OeeAnalysisResponse>> = 
        await apiClient.post(API_ENDPOINTS.ASSET_ANALYTICS.ASSET_OEE(assetId), request);
      return response.data;
    } catch (error: any) {
      throw this.handleError(error);
    }
  }

  /**
   * Calculate fleet-wide OEE analysis
   */
  static async calculateFleetOee(
    request: FleetOeeAnalysisRequest
  ): Promise<AssetAnalyticsResponse<OeeAnalysisDto[]>> {
    try {
      const response: AxiosResponse<AssetAnalyticsResponse<OeeAnalysisDto[]>> = 
        await apiClient.post(API_ENDPOINTS.ASSET_ANALYTICS.FLEET_OEE, request);
      return response.data;
    } catch (error: any) {
      throw this.handleError(error);
    }
  }

  // #endregion

  // #region Reliability Analytics Methods

  /**
   * Calculate comprehensive reliability metrics for an asset
   */
  static async calculateReliabilityMetrics(
    assetId: string,
    request: ReliabilityMetricsRequest
  ): Promise<AssetAnalyticsResponse<AssetReliabilityMetricsDto>> {
    try {
      const response: AxiosResponse<AssetAnalyticsResponse<AssetReliabilityMetricsDto>> = 
        await apiClient.post(API_ENDPOINTS.ASSET_ANALYTICS.ASSET_RELIABILITY(assetId), request);
      return response.data;
    } catch (error: any) {
      throw this.handleError(error);
    }
  }

  /**
   * Get asset reliability rankings across the fleet
   */
  static async getReliabilityRankings(
    request: ReliabilityRankingsRequest
  ): Promise<AssetAnalyticsResponse<AssetReliabilityRankingDto[]>> {
    try {
      const response: AxiosResponse<AssetAnalyticsResponse<AssetReliabilityRankingDto[]>> = 
        await apiClient.post(API_ENDPOINTS.ASSET_ANALYTICS.RELIABILITY_RANKINGS, request);
      return response.data;
    } catch (error: any) {
      throw this.handleError(error);
    }
  }

  // #endregion

  // #region Performance Benchmarking Methods

  /**
   * Get performance benchmark comparison for an asset
   */
  static async getPerformanceBenchmark(
    request: PerformanceBenchmarkRequest
  ): Promise<AssetAnalyticsResponse<AssetPerformanceBenchmarkDto>> {
    try {
      const response: AxiosResponse<AssetAnalyticsResponse<AssetPerformanceBenchmarkDto>> = 
        await apiClient.post(API_ENDPOINTS.ASSET_ANALYTICS.BENCHMARKS, request);
      return response.data;
    } catch (error: any) {
      throw this.handleError(error);
    }
  }

  /**
   * Compare asset performance against industry benchmarks
   */
  static async compareAssetPerformance(
    request: AssetComparisonRequest
  ): Promise<AssetAnalyticsResponse<BenchmarkComparisonResponse>> {
    try {
      const response: AxiosResponse<AssetAnalyticsResponse<BenchmarkComparisonResponse>> = 
        await apiClient.post(API_ENDPOINTS.ASSET_ANALYTICS.COMPARISON, request);
      return response.data;
    } catch (error: any) {
      throw this.handleError(error);
    }
  }

  // #endregion

  // #region Performance Rankings Methods

  /**
   * Get asset performance rankings by specified metric
   */
  static async getPerformanceRankings(
    request: PerformanceRankingRequest
  ): Promise<AssetAnalyticsResponse<AssetPerformanceRankingDto>> {
    try {
      const response: AxiosResponse<AssetAnalyticsResponse<AssetPerformanceRankingDto>> = 
        await apiClient.post(API_ENDPOINTS.ASSET_ANALYTICS.RANKINGS, request);
      return response.data;
    } catch (error: any) {
      throw this.handleError(error);
    }
  }

  // #endregion

  // #region Predictive Analytics Methods

  /**
   * Get asset health trend analysis with predictive insights
   */
  static async getAssetHealthTrends(
    request: AssetHealthTrendRequest
  ): Promise<AssetAnalyticsResponse<AssetHealthTrendDto>> {
    try {
      const response: AxiosResponse<AssetAnalyticsResponse<AssetHealthTrendDto>> = 
        await apiClient.post(API_ENDPOINTS.ASSET_ANALYTICS.HEALTH_TRENDS, request);
      return response.data;
    } catch (error: any) {
      throw this.handleError(error);
    }
  }

  /**
   * Predict asset performance degradation over specified time period
   */
  static async predictAssetPerformance(
    request: AssetPerformancePredictionRequest
  ): Promise<AssetAnalyticsResponse<AssetPerformancePredictionDto>> {
    try {
      const response: AxiosResponse<AssetAnalyticsResponse<AssetPerformancePredictionDto>> = 
        await apiClient.post(API_ENDPOINTS.ASSET_ANALYTICS.PREDICTIONS, request);
      return response.data;
    } catch (error: any) {
      throw this.handleError(error);
    }
  }

  /**
   * Get asset performance trends over time
   */
  static async getPerformanceTrends(
    startDate?: string,
    endDate?: string,
    period: string = 'Monthly'
  ): Promise<AssetAnalyticsResponse<AssetPerformanceTrendDto[]>> {
    try {
      const params = new URLSearchParams();
      if (startDate) params.append('startDate', startDate);
      if (endDate) params.append('endDate', endDate);
      params.append('period', period);

      const response: AxiosResponse<AssetAnalyticsResponse<AssetPerformanceTrendDto[]>> = 
        await apiClient.get(`${API_ENDPOINTS.ASSET_ANALYTICS.TRENDS}?${params}`);
      return response.data;
    } catch (error: any) {
      throw this.handleError(error);
    }
  }

  // #endregion

  // #region KPI Dashboard Methods

  /**
   * Get comprehensive asset KPI dashboard
   */
  static async getKpiDashboard(
    request: AssetKpiDashboardRequest
  ): Promise<AssetAnalyticsResponse<AssetPerformanceDashboardResponse>> {
    try {
      const response: AxiosResponse<AssetAnalyticsResponse<AssetPerformanceDashboardResponse>> = 
        await apiClient.post(API_ENDPOINTS.ASSET_ANALYTICS.DASHBOARD, request);
      return response.data;
    } catch (error: any) {
      throw this.handleError(error);
    }
  }

  /**
   * Calculate advanced KPIs by category
   */
  static async calculateAdvancedKpis(
    request: AdvancedKpiCalculationRequest
  ): Promise<AssetAnalyticsResponse<AssetPerformanceKpiDto[]>> {
    try {
      const response: AxiosResponse<AssetAnalyticsResponse<AssetPerformanceKpiDto[]>> = 
        await apiClient.post(API_ENDPOINTS.ASSET_ANALYTICS.ADVANCED_KPIS, request);
      return response.data;
    } catch (error: any) {
      throw this.handleError(error);
    }
  }

  // #endregion

  // #region Advanced Analytics Methods

  /**
   * Perform root cause analysis on asset performance issues
   */
  static async performRootCauseAnalysis(
    request: RootCauseAnalysisRequest
  ): Promise<AssetAnalyticsResponse<AssetRootCauseAnalysisDto>> {
    try {
      const response: AxiosResponse<AssetAnalyticsResponse<AssetRootCauseAnalysisDto>> = 
        await apiClient.post(API_ENDPOINTS.ASSET_ANALYTICS.ROOT_CAUSE_ANALYSIS, request);
      return response.data;
    } catch (error: any) {
      throw this.handleError(error);
    }
  }

  /**
   * Analyze asset criticality based on performance impact
   */
  static async analyzeAssetCriticality(
    request: AssetCriticalityAnalysisRequest
  ): Promise<AssetAnalyticsResponse<AssetCriticalityAnalysisDto>> {
    try {
      const response: AxiosResponse<AssetAnalyticsResponse<AssetCriticalityAnalysisDto>> = 
        await apiClient.post(API_ENDPOINTS.ASSET_ANALYTICS.CRITICALITY_ANALYSIS, request);
      return response.data;
    } catch (error: any) {
      throw this.handleError(error);
    }
  }

  /**
   * Generate asset optimization recommendations
   */
  static async generateOptimizationRecommendations(
    request: OptimizationRecommendationsRequest
  ): Promise<AssetAnalyticsResponse<AssetOptimizationRecommendationDto[]>> {
    try {
      const response: AxiosResponse<AssetAnalyticsResponse<AssetOptimizationRecommendationDto[]>> = 
        await apiClient.post(API_ENDPOINTS.ASSET_ANALYTICS.OPTIMIZATION_RECOMMENDATIONS, request);
      return response.data;
    } catch (error: any) {
      throw this.handleError(error);
    }
  }

  // #endregion

  // #region Cost Analytics Methods

  /**
   * Calculate total cost of ownership for an asset
   */
  static async calculateTotalCostOfOwnership(
    assetId: string,
    request: TotalCostOfOwnershipRequest
  ): Promise<AssetAnalyticsResponse<AssetTotalCostOfOwnershipDto>> {
    try {
      const response: AxiosResponse<AssetAnalyticsResponse<AssetTotalCostOfOwnershipDto>> = 
        await apiClient.post(API_ENDPOINTS.ASSET_ANALYTICS.TOTAL_COST_OWNERSHIP(assetId), request);
      return response.data;
    } catch (error: any) {
      throw this.handleError(error);
    }
  }

  /**
   * Analyze cost efficiency for an asset
   */
  static async analyzeCostEfficiency(
    assetId: string,
    request: CostEfficiencyAnalysisRequest
  ): Promise<AssetAnalyticsResponse<AssetCostEfficiencyDto>> {
    try {
      const response: AxiosResponse<AssetAnalyticsResponse<AssetCostEfficiencyDto>> = 
        await apiClient.post(API_ENDPOINTS.ASSET_ANALYTICS.COST_EFFICIENCY(assetId), request);
      return response.data;
    } catch (error: any) {
      throw this.handleError(error);
    }
  }

  /**
   * Calculate return on maintenance investment (ROMI)
   */
  static async calculateMaintenanceRoi(
    assetId: string,
    request: MaintenanceRoiAnalysisRequest
  ): Promise<AssetAnalyticsResponse<MaintenanceReturnOnInvestmentDto>> {
    try {
      const response: AxiosResponse<AssetAnalyticsResponse<MaintenanceReturnOnInvestmentDto>> = 
        await apiClient.post(API_ENDPOINTS.ASSET_ANALYTICS.MAINTENANCE_ROI(assetId), request);
      return response.data;
    } catch (error: any) {
      throw this.handleError(error);
    }
  }

  // #endregion

  // #region Energy & Environmental Analytics Methods

  /**
   * Analyze energy consumption and efficiency patterns
   */
  static async analyzeEnergyPerformance(
    assetId: string,
    request: EnergyPerformanceAnalysisRequest
  ): Promise<AssetAnalyticsResponse<AssetEnergyPerformanceDto>> {
    try {
      const response: AxiosResponse<AssetAnalyticsResponse<AssetEnergyPerformanceDto>> = 
        await apiClient.post(API_ENDPOINTS.ASSET_ANALYTICS.ENERGY_PERFORMANCE(assetId), request);
      return response.data;
    } catch (error: any) {
      throw this.handleError(error);
    }
  }

  /**
   * Calculate carbon footprint and environmental impact
   */
  static async calculateEnvironmentalImpact(
    assetId: string,
    request: EnvironmentalImpactAnalysisRequest
  ): Promise<AssetAnalyticsResponse<AssetEnvironmentalImpactDto>> {
    try {
      const response: AxiosResponse<AssetAnalyticsResponse<AssetEnvironmentalImpactDto>> = 
        await apiClient.post(API_ENDPOINTS.ASSET_ANALYTICS.ENVIRONMENTAL_IMPACT(assetId), request);
      return response.data;
    } catch (error: any) {
      throw this.handleError(error);
    }
  }

  // #endregion

  // #region Private Helper Methods

  /**
   * Handle API errors and transform them to a consistent format
   */
  private static handleError(error: any): AssetAnalyticsErrorResponse {
    if (error.response?.data) {
      // API returned an error response
      return {
        error: error.response.data.error || 'API Error',
        message: error.response.data.message || 'An error occurred while processing the request',
        details: error.response.data.details,
        timestamp: new Date().toISOString(),
        traceId: error.response.data.traceId,
      };
    } else if (error.request) {
      // Network error
      return {
        error: 'Network Error',
        message: 'Unable to connect to the server. Please check your internet connection.',
        timestamp: new Date().toISOString(),
      };
    } else {
      // Other error
      return {
        error: 'Unknown Error',
        message: error.message || 'An unexpected error occurred',
        timestamp: new Date().toISOString(),
      };
    }
  }

  // #endregion
}

export default AssetAnalyticsService;