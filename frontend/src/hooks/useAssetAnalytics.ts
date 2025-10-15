import { useMutation, useQuery, useQueryClient, UseQueryOptions, UseMutationOptions } from '@tanstack/react-query';
import { AssetAnalyticsService } from '../services/asset-analytics.service';
import { ASSET_ANALYTICS_QUERY_KEYS } from '../config/api';
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

// #region OEE Analytics Hooks

/**
 * Hook to calculate OEE for a specific asset
 */
export function useAssetOee(
  options?: UseMutationOptions<AssetAnalyticsResponse<OeeAnalysisResponse>, AssetAnalyticsErrorResponse, { assetId: string; request: OeeAnalysisRequest }>
) {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: ({ assetId, request }) => AssetAnalyticsService.calculateAssetOee(assetId, request),
    onSuccess: (data, variables) => {
      // Cache the result for future use
      queryClient.setQueryData(
        [ASSET_ANALYTICS_QUERY_KEYS.ASSET_OEE, variables.assetId, variables.request],
        data
      );
    },
    ...options,
  });
}

/**
 * Hook to calculate fleet-wide OEE analysis
 */
export function useFleetOee(
  options?: UseMutationOptions<AssetAnalyticsResponse<OeeAnalysisDto[]>, AssetAnalyticsErrorResponse, FleetOeeAnalysisRequest>
) {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (request) => AssetAnalyticsService.calculateFleetOee(request),
    onSuccess: (data, variables) => {
      queryClient.setQueryData([ASSET_ANALYTICS_QUERY_KEYS.FLEET_OEE, variables], data);
    },
    ...options,
  });
}

// #endregion

// #region Reliability Analytics Hooks

/**
 * Hook to calculate reliability metrics for an asset
 */
export function useAssetReliabilityMetrics(
  options?: UseMutationOptions<AssetAnalyticsResponse<AssetReliabilityMetricsDto>, AssetAnalyticsErrorResponse, { assetId: string; request: ReliabilityMetricsRequest }>
) {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: ({ assetId, request }) => AssetAnalyticsService.calculateReliabilityMetrics(assetId, request),
    onSuccess: (data, variables) => {
      queryClient.setQueryData(
        [ASSET_ANALYTICS_QUERY_KEYS.ASSET_RELIABILITY, variables.assetId, variables.request],
        data
      );
    },
    ...options,
  });
}

/**
 * Hook to get asset reliability rankings
 */
export function useReliabilityRankings(
  options?: UseMutationOptions<AssetAnalyticsResponse<AssetReliabilityRankingDto[]>, AssetAnalyticsErrorResponse, ReliabilityRankingsRequest>
) {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (request) => AssetAnalyticsService.getReliabilityRankings(request),
    onSuccess: (data, variables) => {
      queryClient.setQueryData([ASSET_ANALYTICS_QUERY_KEYS.RELIABILITY_RANKINGS, variables], data);
    },
    ...options,
  });
}

// #endregion

// #region Performance Benchmarking Hooks

/**
 * Hook to get performance benchmark comparison
 */
export function usePerformanceBenchmark(
  options?: UseMutationOptions<AssetAnalyticsResponse<AssetPerformanceBenchmarkDto>, AssetAnalyticsErrorResponse, PerformanceBenchmarkRequest>
) {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (request) => AssetAnalyticsService.getPerformanceBenchmark(request),
    onSuccess: (data, variables) => {
      queryClient.setQueryData([ASSET_ANALYTICS_QUERY_KEYS.BENCHMARKS, variables], data);
    },
    ...options,
  });
}

/**
 * Hook to compare asset performance against benchmarks
 */
export function useAssetComparison(
  options?: UseMutationOptions<AssetAnalyticsResponse<BenchmarkComparisonResponse>, AssetAnalyticsErrorResponse, AssetComparisonRequest>
) {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (request) => AssetAnalyticsService.compareAssetPerformance(request),
    onSuccess: (data, variables) => {
      queryClient.setQueryData([ASSET_ANALYTICS_QUERY_KEYS.COMPARISON, variables], data);
    },
    ...options,
  });
}

// #endregion

// #region Performance Rankings Hooks

/**
 * Hook to get asset performance rankings
 */
export function usePerformanceRankings(
  options?: UseMutationOptions<AssetAnalyticsResponse<AssetPerformanceRankingDto>, AssetAnalyticsErrorResponse, PerformanceRankingRequest>
) {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (request) => AssetAnalyticsService.getPerformanceRankings(request),
    onSuccess: (data, variables) => {
      queryClient.setQueryData([ASSET_ANALYTICS_QUERY_KEYS.RANKINGS, variables], data);
    },
    ...options,
  });
}

// #endregion

// #region Predictive Analytics Hooks

/**
 * Hook to get asset health trends
 */
export function useAssetHealthTrends(
  options?: UseMutationOptions<AssetAnalyticsResponse<AssetHealthTrendDto>, AssetAnalyticsErrorResponse, AssetHealthTrendRequest>
) {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (request) => AssetAnalyticsService.getAssetHealthTrends(request),
    onSuccess: (data, variables) => {
      queryClient.setQueryData([ASSET_ANALYTICS_QUERY_KEYS.HEALTH_TRENDS, variables], data);
    },
    ...options,
  });
}

/**
 * Hook to predict asset performance
 */
export function useAssetPerformancePrediction(
  options?: UseMutationOptions<AssetAnalyticsResponse<AssetPerformancePredictionDto>, AssetAnalyticsErrorResponse, AssetPerformancePredictionRequest>
) {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (request) => AssetAnalyticsService.predictAssetPerformance(request),
    onSuccess: (data, variables) => {
      queryClient.setQueryData([ASSET_ANALYTICS_QUERY_KEYS.PREDICTIONS, variables], data);
    },
    ...options,
  });
}

/**
 * Hook to get performance trends (query-based for automatic fetching)
 */
export function usePerformanceTrends(
  startDate?: string,
  endDate?: string,
  period: string = 'Monthly',
  options?: UseQueryOptions<AssetAnalyticsResponse<AssetPerformanceTrendDto[]>, AssetAnalyticsErrorResponse>
) {
  return useQuery({
    queryKey: [ASSET_ANALYTICS_QUERY_KEYS.TRENDS, startDate, endDate, period],
    queryFn: () => AssetAnalyticsService.getPerformanceTrends(startDate, endDate, period),
    staleTime: 5 * 60 * 1000, // 5 minutes
    gcTime: 30 * 60 * 1000, // 30 minutes
    enabled: !!(startDate || endDate || period), // Only fetch if we have some parameters
    ...options,
  });
}

// #endregion

// #region KPI Dashboard Hooks

/**
 * Hook to get KPI dashboard data
 */
export function useKpiDashboard(
  options?: UseMutationOptions<AssetAnalyticsResponse<AssetPerformanceDashboardResponse>, AssetAnalyticsErrorResponse, AssetKpiDashboardRequest>
) {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (request) => AssetAnalyticsService.getKpiDashboard(request),
    onSuccess: (data, variables) => {
      queryClient.setQueryData([ASSET_ANALYTICS_QUERY_KEYS.DASHBOARD, variables], data);
    },
    ...options,
  });
}

/**
 * Hook to calculate advanced KPIs
 */
export function useAdvancedKpis(
  options?: UseMutationOptions<AssetAnalyticsResponse<AssetPerformanceKpiDto[]>, AssetAnalyticsErrorResponse, AdvancedKpiCalculationRequest>
) {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (request) => AssetAnalyticsService.calculateAdvancedKpis(request),
    onSuccess: (data, variables) => {
      queryClient.setQueryData([ASSET_ANALYTICS_QUERY_KEYS.ADVANCED_KPIS, variables], data);
    },
    ...options,
  });
}

// #endregion

// #region Advanced Analytics Hooks

/**
 * Hook to perform root cause analysis
 */
export function useRootCauseAnalysis(
  options?: UseMutationOptions<AssetAnalyticsResponse<AssetRootCauseAnalysisDto>, AssetAnalyticsErrorResponse, RootCauseAnalysisRequest>
) {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (request) => AssetAnalyticsService.performRootCauseAnalysis(request),
    onSuccess: (data, variables) => {
      queryClient.setQueryData([ASSET_ANALYTICS_QUERY_KEYS.ROOT_CAUSE_ANALYSIS, variables], data);
    },
    ...options,
  });
}

/**
 * Hook to analyze asset criticality
 */
export function useAssetCriticalityAnalysis(
  options?: UseMutationOptions<AssetAnalyticsResponse<AssetCriticalityAnalysisDto>, AssetAnalyticsErrorResponse, AssetCriticalityAnalysisRequest>
) {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (request) => AssetAnalyticsService.analyzeAssetCriticality(request),
    onSuccess: (data, variables) => {
      queryClient.setQueryData([ASSET_ANALYTICS_QUERY_KEYS.CRITICALITY_ANALYSIS, variables], data);
    },
    ...options,
  });
}

/**
 * Hook to generate optimization recommendations
 */
export function useOptimizationRecommendations(
  options?: UseMutationOptions<AssetAnalyticsResponse<AssetOptimizationRecommendationDto[]>, AssetAnalyticsErrorResponse, OptimizationRecommendationsRequest>
) {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (request) => AssetAnalyticsService.generateOptimizationRecommendations(request),
    onSuccess: (data, variables) => {
      queryClient.setQueryData([ASSET_ANALYTICS_QUERY_KEYS.OPTIMIZATION_RECOMMENDATIONS, variables], data);
    },
    ...options,
  });
}

// #endregion

// #region Cost Analytics Hooks

/**
 * Hook to calculate total cost of ownership
 */
export function useTotalCostOfOwnership(
  options?: UseMutationOptions<AssetAnalyticsResponse<AssetTotalCostOfOwnershipDto>, AssetAnalyticsErrorResponse, { assetId: string; request: TotalCostOfOwnershipRequest }>
) {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: ({ assetId, request }) => AssetAnalyticsService.calculateTotalCostOfOwnership(assetId, request),
    onSuccess: (data, variables) => {
      queryClient.setQueryData(
        [ASSET_ANALYTICS_QUERY_KEYS.TOTAL_COST_OWNERSHIP, variables.assetId, variables.request],
        data
      );
    },
    ...options,
  });
}

/**
 * Hook to analyze cost efficiency
 */
export function useCostEfficiencyAnalysis(
  options?: UseMutationOptions<AssetAnalyticsResponse<AssetCostEfficiencyDto>, AssetAnalyticsErrorResponse, { assetId: string; request: CostEfficiencyAnalysisRequest }>
) {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: ({ assetId, request }) => AssetAnalyticsService.analyzeCostEfficiency(assetId, request),
    onSuccess: (data, variables) => {
      queryClient.setQueryData(
        [ASSET_ANALYTICS_QUERY_KEYS.COST_EFFICIENCY, variables.assetId, variables.request],
        data
      );
    },
    ...options,
  });
}

/**
 * Hook to calculate maintenance ROI
 */
export function useMaintenanceRoi(
  options?: UseMutationOptions<AssetAnalyticsResponse<MaintenanceReturnOnInvestmentDto>, AssetAnalyticsErrorResponse, { assetId: string; request: MaintenanceRoiAnalysisRequest }>
) {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: ({ assetId, request }) => AssetAnalyticsService.calculateMaintenanceRoi(assetId, request),
    onSuccess: (data, variables) => {
      queryClient.setQueryData(
        [ASSET_ANALYTICS_QUERY_KEYS.MAINTENANCE_ROI, variables.assetId, variables.request],
        data
      );
    },
    ...options,
  });
}

// #endregion

// #region Energy & Environmental Analytics Hooks

/**
 * Hook to analyze energy performance
 */
export function useEnergyPerformanceAnalysis(
  options?: UseMutationOptions<AssetAnalyticsResponse<AssetEnergyPerformanceDto>, AssetAnalyticsErrorResponse, { assetId: string; request: EnergyPerformanceAnalysisRequest }>
) {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: ({ assetId, request }) => AssetAnalyticsService.analyzeEnergyPerformance(assetId, request),
    onSuccess: (data, variables) => {
      queryClient.setQueryData(
        [ASSET_ANALYTICS_QUERY_KEYS.ENERGY_PERFORMANCE, variables.assetId, variables.request],
        data
      );
    },
    ...options,
  });
}

/**
 * Hook to calculate environmental impact
 */
export function useEnvironmentalImpactAnalysis(
  options?: UseMutationOptions<AssetAnalyticsResponse<AssetEnvironmentalImpactDto>, AssetAnalyticsErrorResponse, { assetId: string; request: EnvironmentalImpactAnalysisRequest }>
) {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: ({ assetId, request }) => AssetAnalyticsService.calculateEnvironmentalImpact(assetId, request),
    onSuccess: (data, variables) => {
      queryClient.setQueryData(
        [ASSET_ANALYTICS_QUERY_KEYS.ENVIRONMENTAL_IMPACT, variables.assetId, variables.request],
        data
      );
    },
    ...options,
  });
}

// #endregion

// #region Utility Hooks

/**
 * Hook to invalidate and refetch all asset analytics queries
 */
export function useInvalidateAssetAnalytics() {
  const queryClient = useQueryClient();
  
  return () => {
    // Invalidate all asset analytics queries
    queryClient.invalidateQueries({
      predicate: (query) => {
        const queryKey = query.queryKey as string[];
        return queryKey.length > 0 && 
               Object.values(ASSET_ANALYTICS_QUERY_KEYS).some(key => 
                 queryKey[0] === key || queryKey.includes(key)
               );
      },
    });
  };
}

/**
 * Hook to prefetch commonly used analytics data
 */
export function usePrefetchAssetAnalytics() {
  const queryClient = useQueryClient();
  
  return {
    prefetchPerformanceTrends: (startDate?: string, endDate?: string, period: string = 'Monthly') => {
      queryClient.prefetchQuery({
        queryKey: [ASSET_ANALYTICS_QUERY_KEYS.TRENDS, startDate, endDate, period],
        queryFn: () => AssetAnalyticsService.getPerformanceTrends(startDate, endDate, period),
        staleTime: 5 * 60 * 1000, // 5 minutes
      });
    },
  };
}

// #endregion

// #region Combined Analytics Hooks

/**
 * Hook to get comprehensive asset analytics data for a dashboard
 * This combines multiple analytics calls for a complete view
 */
export function useAssetDashboardData(assetId: string, dateRange: { startDate: string; endDate: string }) {
  // Individual hooks for different analytics
  const oeeAnalysis = useAssetOee();
  const reliabilityMetrics = useAssetReliabilityMetrics();
  const costAnalysis = useTotalCostOfOwnership();
  const energyAnalysis = useEnergyPerformanceAnalysis();
  
  // Performance trends query
  const performanceTrends = usePerformanceTrends(
    dateRange.startDate,
    dateRange.endDate,
    'Monthly'
  );
  
  const loadDashboardData = async () => {
    const promises = [
      oeeAnalysis.mutateAsync({
        assetId,
        request: {
          startDate: dateRange.startDate,
          endDate: dateRange.endDate,
          includeDowntimeBreakdown: true,
          includeQualityMetrics: true,
          calculationMethod: 'Standard',
        },
      }),
      reliabilityMetrics.mutateAsync({
        assetId,
        request: {
          startDate: dateRange.startDate,
          endDate: dateRange.endDate,
          includePredictiveMetrics: true,
          benchmarkComparison: true,
        },
      }),
      costAnalysis.mutateAsync({
        assetId,
        request: {
          startDate: dateRange.startDate,
          endDate: dateRange.endDate,
          includeDepreciation: true,
          includeOperationalCosts: true,
          includeMaintenanceCosts: true,
          currency: 'USD',
        },
      }),
      energyAnalysis.mutateAsync({
        assetId,
        request: {
          startDate: dateRange.startDate,
          endDate: dateRange.endDate,
          includeEfficiencyMetrics: true,
          includeCostAnalysis: true,
        },
      }),
    ];
    
    try {
      const results = await Promise.allSettled(promises);
      return {
        oee: results[0].status === 'fulfilled' ? results[0].value : null,
        reliability: results[1].status === 'fulfilled' ? results[1].value : null,
        cost: results[2].status === 'fulfilled' ? results[2].value : null,
        energy: results[3].status === 'fulfilled' ? results[3].value : null,
        trends: performanceTrends.data || null,
        errors: results.filter(r => r.status === 'rejected').map(r => (r as PromiseRejectedResult).reason),
      };
    } catch (error) {
      throw error;
    }
  };
  
  return {
    loadDashboardData,
    isLoading: oeeAnalysis.isPending || reliabilityMetrics.isPending || costAnalysis.isPending || energyAnalysis.isPending,
    trendsLoading: performanceTrends.isLoading,
    errors: [
      oeeAnalysis.error,
      reliabilityMetrics.error,
      costAnalysis.error,
      energyAnalysis.error,
      performanceTrends.error,
    ].filter(Boolean),
  };
}

// #endregion