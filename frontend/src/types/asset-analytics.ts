// Asset Analytics Types
export interface AssetAnalyticsResponse<T = any> {
  success: boolean;
  message: string;
  data?: T;
  timestamp: string;
  count?: number;
  metadata?: AssetAnalyticsMetadata;
}

export interface AssetAnalyticsMetadata {
  dataSource?: string;
  dataAsOf?: string;
  calculationMethod?: string;
  sampleSize?: number;
  confidenceLevel?: number;
}

export interface AssetAnalyticsErrorResponse {
  error: string;
  message: string;
  details?: string;
  timestamp: string;
  traceId?: string;
}

// Base request types
export interface AssetAnalyticsBaseRequest {
  startDate?: string;
  endDate?: string;
  assetId?: string;
}

// OEE Analytics Types
export interface OeeAnalysisRequest extends AssetAnalyticsBaseRequest {
  assetType?: string;
  includeBreakdown?: boolean;
}

export interface FleetOeeAnalysisRequest {
  startDate: string;
  endDate: string;
  assetType?: string;
  topCount?: number;
}

export interface OeeAnalysisResponse {
  assetId: string;
  assetName: string;
  startDate: string;
  endDate: string;
  availability: number;
  performance: number;
  quality: number;
  oeeScore: number;
  performanceCategory: string;
  industryBenchmark: number;
  worldClassBenchmark: number;
  lossAnalysis?: OeeLossAnalysis;
  previousPeriodOee: number;
  trendChange: number;
  trendDirection: string;
  improvementRecommendations: string[];
}

export interface OeeLossAnalysis {
  availabilityLoss: number;
  performanceLoss: number;
  qualityLoss: number;
  lossCategories: LossCategory[];
}

export interface LossCategory {
  category: string;
  lossPercentage: number;
  description: string;
}

export interface OeeAnalysisDto {
  assetId: string;
  assetName: string;
  startDate: string;
  endDate: string;
  availability: number;
  performance: number;
  quality: number;
  oeeScore: number;
  performanceCategory: string;
}

// Reliability Analytics Types
export interface ReliabilityMetricsRequest extends AssetAnalyticsBaseRequest {
  includeFailureModes?: boolean;
  includeTrends?: boolean;
}

export interface ReliabilityRankingsRequest {
  startDate: string;
  endDate: string;
  topCount?: number;
  assetType?: string;
  orderBy?: string;
}

export interface AssetReliabilityMetricsDto {
  assetId: string;
  assetName: string;
  analysisPeriodStart: string;
  analysisPeriodEnd: string;
  meanTimeBetweenFailures: number;
  meanTimeToRepair: number;
  meanTimeToFailure: number;
  availability: number;
  reliabilityScore: number;
  failureRate: number;
  totalFailures: number;
  plannedDowntimeHours: number;
  unplannedDowntimeHours: number;
  operatingHours: number;
  failureModes: FailureModeAnalysis[];
  trendAnalysis: ReliabilityTrendAnalysis;
}

export interface FailureModeAnalysis {
  failureMode: string;
  frequency: number;
  percentage: number;
  averageRepairTime: number;
  totalCost: number;
}

export interface ReliabilityTrendAnalysis {
  trendDirection: string;
  trendStrength: number;
  failureFrequencyTrend: string;
  repairTimeTrend: string;
  recommendedActions: string[];
}

export interface AssetReliabilityRankingDto {
  assetId: string;
  assetName: string;
  assetType: string;
  reliabilityScore: number;
  availability: number;
  meanTimeBetweenFailures: number;
  meanTimeToRepair: number;
  totalFailures: number;
  failureRate: number;
  performanceCategory: string;
}

// Performance Benchmarking Types
export interface PerformanceBenchmarkRequest {
  assetId: string;
  benchmarkCategory?: string;
  startDate?: string;
  endDate?: string;
}

export interface AssetComparisonRequest {
  assetId: string;
  industryType: string;
  startDate: string;
  endDate: string;
}

export interface AssetPerformanceBenchmarkDto {
  assetId: string;
  assetName: string;
  assetType: string;
  benchmarkCategory: string;
  benchmarkDate: string;
  currentOee: number;
  currentAvailability: number;
  currentReliabilityScore: number;
  currentMtbf: number;
  currentMttr: number;
  benchmarkOee: number;
  benchmarkAvailability: number;
  benchmarkReliabilityScore: number;
  benchmarkMtbf: number;
  benchmarkMttr: number;
  oeeGap: number;
  availabilityGap: number;
  reliabilityGap: number;
  mtbfGap: number;
  mttrGap: number;
  oeePercentileRank: number;
  availabilityPercentileRank: number;
  reliabilityPercentileRank: number;
  overallPerformanceRating: string;
  improvementRecommendations: string[];
}

export interface BenchmarkComparisonResponse {
  assetId: string;
  assetName: string;
  benchmarkCategory: string;
  comparisonDate: string;
  metrics: Record<string, BenchmarkMetric>;
  overallRating: string;
  overallScore: number;
  industryPercentileRank: number;
  strengthAreas: string[];
  improvementAreas: string[];
  actionableInsights: string[];
  estimatedAnnualSavings: number;
  paybackPeriodMonths: number;
}

export interface BenchmarkMetric {
  metricName: string;
  actualValue: number;
  benchmarkValue: number;
  performanceGap: number;
  performanceRating: string;
  unit: string;
}

// Performance Rankings Types
export interface PerformanceRankingRequest {
  metricType?: string;
  topCount?: number;
  bottomCount?: number;
  startDate?: string;
  endDate?: string;
  assetType?: string;
}

export interface AssetPerformanceRankingDto {
  metricType: string;
  rankingDate: string;
  totalAssetsEvaluated: number;
  topPerformers: AssetPerformanceData[];
  bottomPerformers: AssetPerformanceData[];
  fleetAverage: number;
  fleetMedian: number;
}

export interface AssetPerformanceData {
  assetId: string;
  assetName: string;
  assetType: string;
  metricValue: number;
  rank: number;
}

// Predictive Analytics Types
export interface AssetHealthTrendRequest {
  assetId: string;
  periodMonths?: number;
  includePredictions?: boolean;
}

export interface AssetPerformancePredictionRequest {
  assetId: string;
  predictionDays?: number;
  metricTypes?: string[];
}

export interface AssetHealthTrendDto {
  assetId: string;
  assetName: string;
  analysisPeriodMonths: number;
  healthTrendData: AssetHealthDataPoint[];
  overallTrend: string;
  currentHealthScore: number;
  predictedHealthScore: number;
  healthRiskLevel: string;
  recommendedActions: string[];
  nextReviewDate: string;
}

export interface AssetHealthDataPoint {
  date: string;
  healthScore: number;
  trendDirection: string;
}

export interface AssetPerformancePredictionDto {
  assetId: string;
  assetName: string;
  predictionDate: string;
  predictionHorizonDays: number;
  predictedOee: number;
  predictedAvailability: number;
  predictedMaintenanceCost: number;
  confidenceLevel: number;
  riskFactors: string[];
  recommendations: string[];
}

export interface AssetPerformanceTrendDto {
  assetId: string;
  assetName: string;
  periodStart: string;
  periodEnd: string;
  trendData: TrendDataPoint[];
  overallTrend: string;
  trendStrength: number;
}

export interface TrendDataPoint {
  date: string;
  value: number;
  metricType: string;
}

// KPI Dashboard Types
export interface AssetKpiDashboardRequest {
  assetId?: string;
  startDate?: string;
  endDate?: string;
  kpiCategories?: string[];
}

export interface AdvancedKpiCalculationRequest {
  startDate: string;
  endDate: string;
  kpiCategory?: string;
  assetId?: string;
  assetType?: string;
}

export interface AssetPerformanceDashboardResponse {
  tenantId: string;
  dashboardDate: string;
  periodStart: string;
  periodEnd: string;
  fleetSummary: FleetSummary;
  assetMetrics: AssetPerformanceMetrics[];
  topPerformers: AssetPerformanceMetrics[];
  bottomPerformers: AssetPerformanceMetrics[];
  criticalAlerts: PerformanceAlert[];
  trends: PerformanceTrends;
}

export interface FleetSummary {
  totalAssets: number;
  averageOee: number;
  averageAvailability: number;
  averageReliabilityScore: number;
  totalMaintenanceCosts: number;
  totalFailures: number;
  fleetUtilization: number;
}

export interface AssetPerformanceMetrics {
  assetId: string;
  assetName: string;
  metricsCalculationDate: string;
  periodStart: string;
  periodEnd: string;
  performanceScore: number;
}

export interface PerformanceAlert {
  id: string;
  assetId: string;
  assetName: string;
  alertType: string;
  severity: string;
  message: string;
  createdAt: string;
  isAcknowledged: boolean;
  recommendedActions: string[];
}

export interface PerformanceTrends {
  oeeTrend: string;
  availabilityTrend: string;
  reliabilityTrend: string;
  costTrend: string;
  monthlyTrends: TrendDataPoint[];
}

export interface AssetPerformanceKpiDto {
  assetId: string;
  name: string;
  value: number;
  unit: string;
  target: number;
  benchmark: number;
  trend: string;
  category: string;
}

// Advanced Analytics Types
export interface RootCauseAnalysisRequest {
  assetId: string;
  incidentDate: string;
  issueType: string;
  includePreventiveActions?: boolean;
  includeCorrectiveActions?: boolean;
}

export interface AssetCriticalityAnalysisRequest {
  assetId: string;
  includeMaintenanceStrategy?: boolean;
  includeMonitoringRequirements?: boolean;
}

export interface OptimizationRecommendationsRequest {
  assetId?: string;
  recommendationTypes?: string[];
  minPotentialSavings?: number;
  maxRecommendations?: number;
}

export interface AssetRootCauseAnalysisDto {
  assetId: string;
  assetName: string;
  incidentDate: string;
  issueType: string;
  analysisDate: string;
  primaryRootCause: string;
  contributingFactors: RootCauseFactor[];
  impactAssessment: any;
  preventiveActions: string[];
  correctiveActions: string[];
  riskOfRecurrence: number;
}

export interface RootCauseFactor {
  factor: string;
  probability: number;
  impact: string;
  category: string;
}

export interface AssetCriticalityAnalysisDto {
  assetId: string;
  assetName: string;
  analysisDate: string;
  overallCriticalityScore: number;
  criticalityLevel: string;
  businessImpactScore: number;
  safetyImpactScore: number;
  environmentalImpactScore: number;
  operationalImpactScore: number;
  financialImpactScore: number;
  maintenanceStrategy: string;
  monitoringRequirements: string[];
}

export interface AssetOptimizationRecommendationDto {
  assetId: string;
  assetName: string;
  recommendationType: string;
  title: string;
  description: string;
  priority: string;
  potentialSavings: number;
  implementationTimeMonths: number;
  implementationCost: number;
  roi: number;
  steps: string[];
}

// Cost Analytics Types
export interface TotalCostOfOwnershipRequest extends AssetAnalyticsBaseRequest {
  includeCostBreakdown?: boolean;
  includeAnnualizedCosts?: boolean;
}

export interface CostEfficiencyAnalysisRequest extends AssetAnalyticsBaseRequest {
  includeBenchmarkComparison?: boolean;
  includeImprovementOpportunities?: boolean;
}

export interface MaintenanceRoiAnalysisRequest extends AssetAnalyticsBaseRequest {
  includePaybackPeriod?: boolean;
  includeNetPresentValue?: boolean;
}

export interface AssetTotalCostOfOwnershipDto {
  assetId: string;
  assetName: string;
  analysisPeriodStart: string;
  analysisPeriodEnd: string;
  acquisitionCost: number;
  maintenanceCosts: number;
  operatingCosts: number;
  downtimeCosts: number;
  disposalValue: number;
  totalCostOfOwnership: number;
  annualizedCost: number;
  costPerOperatingHour: number;
  costBreakdown: any;
}

export interface AssetCostEfficiencyDto {
  assetId: string;
  assetName: string;
  analysisPeriodStart: string;
  analysisPeriodEnd: string;
  totalCosts: number;
  productionOutput: number;
  costPerUnit: number;
  industryBenchmark: number;
  efficiencyRatio: number;
  performanceRating: string;
  improvementOpportunities: string[];
}

export interface MaintenanceReturnOnInvestmentDto {
  assetId: string;
  assetName: string;
  calculationPeriodStart: string;
  calculationPeriodEnd: string;
  maintenanceInvestment: number;
  productivityGains: number;
  costAvoidance: number;
  totalBenefits: number;
  returnOnInvestment: number;
  paybackPeriodMonths: number;
  netPresentValue: number;
}

// Energy & Environmental Analytics Types
export interface EnergyPerformanceAnalysisRequest extends AssetAnalyticsBaseRequest {
  includeEfficiencyRating?: boolean;
  includeCarbonFootprint?: boolean;
  includeOptimizationRecommendations?: boolean;
}

export interface EnvironmentalImpactAnalysisRequest extends AssetAnalyticsBaseRequest {
  includeComplianceStatus?: boolean;
  includeImprovementRecommendations?: boolean;
  includeRegulatoryRisk?: boolean;
}

export interface AssetEnergyPerformanceDto {
  assetId: string;
  assetName: string;
  analysisPeriodStart: string;
  analysisPeriodEnd: string;
  totalEnergyConsumption: number;
  averageEnergyConsumptionPerHour: number;
  energyEfficiencyRating: string;
  carbonFootprint: number;
  energyCosts: number;
  energyTrend: string;
  optimizationRecommendations: string[];
  potentialSavings: number;
}

export interface AssetEnvironmentalImpactDto {
  assetId: string;
  assetName: string;
  analysisPeriodStart: string;
  analysisPeriodEnd: string;
  carbonFootprintKgCO2: number;
  energyConsumptionKWh: number;
  wasteGenerationKg: number;
  environmentalScore: number;
  complianceStatus: string;
  improvementRecommendations: string[];
  regulatoryRisk: string;
}