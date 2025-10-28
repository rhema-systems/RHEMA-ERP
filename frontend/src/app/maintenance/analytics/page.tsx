'use client';

import React, { useState, useCallback, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Badge } from '@/components/ui/badge';
import { 
  BarChart3, 
  TrendingUp, 
  AlertTriangle, 
  Settings, 
  Download,
  RefreshCw,
  Eye,
  Target,
  Zap,
  DollarSign,
  Clock
} from 'lucide-react';
import analyticsApiService, { AnalyticsData } from '@/services/analytics-api.service';

// Analytics Components
import {
  OeeGauge,
  PerformanceTrendChart,
  ReliabilityMetricsChart,
  CostAnalysisChart,
  EnergyPerformanceChart,
  AssetHealthScore,
  OeeKpiCard,
  UptimeKpiCard,
  CostKpiCard,
  EnergyKpiCard,
} from '@/components/analytics/charts/AnalyticsCharts';

// Filter Components
import {
  AnalyticsFilterPanel,
  QuickFilterBar,
  type AnalyticsFilters,
} from '@/components/analytics/filters/AnalyticsFilters';

// Hooks
import { useAssetDashboardData } from '@/hooks/useAssetAnalytics';
import { cn } from '@/lib/utils';

// #region Types and Interfaces

interface DashboardState {
  selectedAssetId: string | null;
  filters: AnalyticsFilters;
  activeTab: string;
  loading: boolean;
  error: string | null;
  dataSource: 'backend' | 'mock';
}

interface MockAsset {
  id: string;
  name: string;
  type: string;
  location: string;
  department: string;
}

// #endregion

interface AnalyticsData {
  assets: MockAsset[];
  performanceData: Array<{ date: string; oee: number; availability: number; performance: number; quality: number }>;
  reliabilityData: Array<{ assetName: string; mtbf: number; mttr: number; availability: number }>;
  costData: Array<{ category: string; cost: number; percentage: number }>;
  energyData: Array<{ date: string; consumption: number; efficiency: number; cost: number }>;
}

// #region Default Filter State

const defaultFilters: AnalyticsFilters = {
  dateRange: {
    startDate: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
    endDate: new Date().toISOString(),
  },
  assets: {
    assetIds: ['1'],
    assetTypes: [],
    locations: [],
    departments: [],
  },
  analysisType: 'comprehensive',
  granularity: 'daily',
  includeDowntime: true,
  includeMaintenanceEvents: true,
  benchmarkComparison: false,
};

// #endregion

// #region Asset Analytics Dashboard Component

export default function AssetAnalyticsDashboard() {
  const [dashboardState, setDashboardState] = useState<DashboardState>({
    selectedAssetId: '1',
    filters: defaultFilters,
    activeTab: 'overview',
    loading: true,
    error: null,
    dataSource: 'backend',
  });

  const [analyticsData, setAnalyticsData] = useState<AnalyticsData | null>(null);

  // Load analytics data on component mount (same pattern as job-cards page)
  useEffect(() => {
    const loadAnalyticsData = async () => {
      setDashboardState(prev => ({ ...prev, loading: true, error: null }));
      try {
        console.log('🔄 Attempting to fetch analytics data from backend API...');
        const data = await analyticsApiService.getAnalyticsData();
        console.log('✅ Successfully received backend data:', data);
        setAnalyticsData(data);
        setDashboardState(prev => ({ ...prev, dataSource: 'backend' }));
      } catch (error) {
        console.error('❌ Analytics API call failed:', error);
        console.log('⚠️ Using mock data due to API failure');
        // Use mock data from service when API fails
        const mockData = analyticsApiService.getMockAnalyticsData();
        setAnalyticsData(mockData);
        setDashboardState(prev => ({ ...prev, dataSource: 'mock' }));
      } finally {
        setDashboardState(prev => ({ ...prev, loading: false }));
      }
    };

    loadAnalyticsData();
  }, []);

  // Provide fallback if data is still null
  const displayData = analyticsData || analyticsApiService.getMockAnalyticsData();

  // Dashboard data using React Query results
  const dashboardData = {
    assets: displayData.assets,
    performanceData: displayData.performanceData,
    reliabilityData: displayData.reliabilityData,
    costData: displayData.costData,
    energyData: displayData.energyData,
    loading: dashboardState.loading,
    error: dashboardState.error
  };

  const handleFiltersChange = useCallback((newFilters: Partial<AnalyticsFilters>) => {
    setDashboardState(prev => ({
      ...prev,
      filters: { ...prev.filters, ...newFilters },
    }));
  }, []);

  const handleApplyFilters = useCallback(async () => {
    setDashboardState(prev => ({ ...prev, loading: true }));
    try {
      const data = await analyticsApiService.getAnalyticsData();
      setAnalyticsData(data);
      setDashboardState(prev => ({ ...prev, dataSource: 'backend' }));
    } catch (error) {
      console.error('Failed to refresh analytics data:', error);
      const mockData = analyticsApiService.getMockAnalyticsData();
      setAnalyticsData(mockData);
      setDashboardState(prev => ({ ...prev, dataSource: 'mock' }));
    } finally {
      setDashboardState(prev => ({ ...prev, loading: false }));
    }
  }, []);

  const handleResetFilters = useCallback(() => {
    setDashboardState(prev => ({
      ...prev,
      filters: defaultFilters,
    }));
  }, []);

  const handleClearFilter = useCallback((filterType: string) => {
    switch (filterType) {
      case 'dateRange':
        handleFiltersChange({ dateRange: defaultFilters.dateRange });
        break;
      case 'assets':
        handleFiltersChange({ assets: defaultFilters.assets });
        break;
      case 'analysisType':
        handleFiltersChange({ analysisType: defaultFilters.analysisType });
        break;
    }
  }, [handleFiltersChange]);

  const handleClearAllFilters = useCallback(() => {
    handleResetFilters();
  }, [handleResetFilters]);

  const getActiveFiltersForDisplay = () => ({
    dateRange: dashboardState.filters.dateRange.startDate && dashboardState.filters.dateRange.endDate 
      ? `${new Date(dashboardState.filters.dateRange.startDate).toLocaleDateString()} - ${new Date(dashboardState.filters.dateRange.endDate).toLocaleDateString()}`
      : undefined,
    assets: dashboardState.filters.assets.assetIds.length,
    analysisType: dashboardState.filters.analysisType !== defaultFilters.analysisType 
      ? dashboardState.filters.analysisType 
      : undefined,
  });

  return (
    <div className="space-y-6 p-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Asset Performance Analytics</h1>
          <p className="text-muted-foreground">
            Comprehensive analysis of asset performance, reliability, and efficiency metrics
          </p>
          <div className="mt-2">
            <Badge variant={dashboardState.dataSource === 'backend' ? 'default' : 'destructive'}>
              {dashboardState.dataSource === 'backend' ? '🔗 Backend Data' : '⚠️ Mock Data'}
            </Badge>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm">
            <Download className="h-4 w-4 mr-2" />
            Export Report
          </Button>
          <Button 
            variant="outline" 
            size="sm"
            onClick={handleApplyFilters}
            disabled={dashboardState.loading}
          >
            {dashboardState.loading ? (
              <RefreshCw className="h-4 w-4 mr-2 animate-spin" />
            ) : (
              <RefreshCw className="h-4 w-4 mr-2" />
            )}
            Refresh
          </Button>
        </div>
      </div>

      {/* Quick Filter Bar */}
      <QuickFilterBar
        activeFilters={getActiveFiltersForDisplay()}
        onClearFilter={handleClearFilter}
        onClearAll={handleClearAllFilters}
      />

      <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
        {/* Filter Panel */}
        <div className="lg:col-span-1">
          <AnalyticsFilterPanel
            filters={dashboardState.filters}
            availableAssets={displayData.assets}
            onFiltersChange={handleFiltersChange}
            onApplyFilters={handleApplyFilters}
            onResetFilters={handleResetFilters}
            loading={dashboardState.loading}
          />
        </div>

        {/* Main Dashboard Content */}
        <div className="lg:col-span-3">
          <Tabs 
            value={dashboardState.activeTab} 
            onValueChange={(value) => setDashboardState(prev => ({ ...prev, activeTab: value }))}
            className="space-y-6"
          >
            <TabsList className="grid w-full grid-cols-5">
              <TabsTrigger value="overview" className="flex items-center gap-2">
                <BarChart3 className="h-4 w-4" />
                Overview
              </TabsTrigger>
              <TabsTrigger value="oee" className="flex items-center gap-2">
                <Target className="h-4 w-4" />
                OEE
              </TabsTrigger>
              <TabsTrigger value="reliability" className="flex items-center gap-2">
                <Clock className="h-4 w-4" />
                Reliability
              </TabsTrigger>
              <TabsTrigger value="costs" className="flex items-center gap-2">
                <DollarSign className="h-4 w-4" />
                Costs
              </TabsTrigger>
              <TabsTrigger value="energy" className="flex items-center gap-2">
                <Zap className="h-4 w-4" />
                Energy
              </TabsTrigger>
            </TabsList>

            {/* Overview Tab */}
            <TabsContent value="overview" className="space-y-6">
              {/* KPI Cards */}
              <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
                <OeeKpiCard value={84.7} change={2.3} />
                <UptimeKpiCard value={91.2} change={-1.1} />
                <CostKpiCard value={128000} change={-5.7} />
                <EnergyKpiCard value={88.4} change={3.2} />
              </div>

              {/* Main Charts */}
              <div className="grid grid-cols-1 xl:grid-cols-2 gap-6">
                <OeeGauge
                  oeeValue={84.7}
                  availability={91.2}
                  performance={88.7}
                  quality={96.3}
                  size="lg"
                />
                <AssetHealthScore
                  healthScore={85}
                  trend="up"
                  riskLevel="low"
                />
              </div>

              {/* Performance Trends */}
              <PerformanceTrendChart
                data={displayData.performanceData}
                loading={dashboardState.loading}
                error={dashboardState.error}
              />
            </TabsContent>

            {/* OEE Analysis Tab */}
            <TabsContent value="oee" className="space-y-6">
              <div className="grid grid-cols-1 xl:grid-cols-2 gap-6">
                <OeeGauge
                  oeeValue={84.7}
                  availability={91.2}
                  performance={88.7}
                  quality={96.3}
                  size="lg"
                  className="xl:col-span-1"
                />
                <Card>
                  <CardHeader>
                    <CardTitle>OEE Components Analysis</CardTitle>
                    <CardDescription>Detailed breakdown of OEE factors</CardDescription>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    <div className="space-y-3">
                      <div className="flex justify-between items-center">
                        <span className="text-sm font-medium">Availability</span>
                        <Badge variant={91.2 >= 85 ? 'default' : 'secondary'}>
                          91.2% {91.2 >= 85 ? 'Good' : 'Needs Improvement'}
                        </Badge>
                      </div>
                      <div className="flex justify-between items-center">
                        <span className="text-sm font-medium">Performance</span>
                        <Badge variant={88.7 >= 85 ? 'default' : 'secondary'}>
                          88.7% {88.7 >= 85 ? 'Good' : 'Needs Improvement'}
                        </Badge>
                      </div>
                      <div className="flex justify-between items-center">
                        <span className="text-sm font-medium">Quality</span>
                        <Badge variant={96.3 >= 85 ? 'default' : 'secondary'}>
                          96.3% {96.3 >= 85 ? 'Excellent' : 'Needs Improvement'}
                        </Badge>
                      </div>
                    </div>
                    <Separator />
                    <div className="text-sm text-muted-foreground">
                      <p>World-class OEE target: &gt;85%</p>
                      <p>Current performance: <span className="font-medium text-foreground">84.7%</span></p>
                      <p>Gap to target: <span className="font-medium text-orange-600">-0.3%</span></p>
                    </div>
                  </CardContent>
                </Card>
              </div>
              
              <PerformanceTrendChart
                data={displayData.performanceData}
                loading={dashboardState.loading}
                error={dashboardState.error}
              />
            </TabsContent>

            {/* Reliability Analysis Tab */}
            <TabsContent value="reliability" className="space-y-6">
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <Card>
                  <CardContent className="p-6 text-center">
                    <div className="text-2xl font-bold text-green-600">245h</div>
                    <p className="text-sm text-muted-foreground">Mean Time Between Failures</p>
                  </CardContent>
                </Card>
                <Card>
                  <CardContent className="p-6 text-center">
                    <div className="text-2xl font-bold text-orange-600">4.2h</div>
                    <p className="text-sm text-muted-foreground">Mean Time To Repair</p>
                  </CardContent>
                </Card>
                <Card>
                  <CardContent className="p-6 text-center">
                    <div className="text-2xl font-bold text-blue-600">91.2%</div>
                    <p className="text-sm text-muted-foreground">Asset Availability</p>
                  </CardContent>
                </Card>
              </div>

              <ReliabilityMetricsChart
                data={displayData.reliabilityData}
                loading={dashboardState.loading}
                error={dashboardState.error}
              />
            </TabsContent>

            {/* Cost Analysis Tab */}
            <TabsContent value="costs" className="space-y-6">
              <CostAnalysisChart
                data={displayData.costData}
                totalCost={128000}
                loading={dashboardState.loading}
                error={dashboardState.error}
              />
              
              <Card>
                <CardHeader>
                  <CardTitle>Cost Insights</CardTitle>
                  <CardDescription>Key findings and recommendations</CardDescription>
                </CardHeader>
                <CardContent>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                    <div className="space-y-3">
                      <h4 className="font-medium">Cost Trends</h4>
                      <div className="space-y-2 text-sm">
                        <div className="flex justify-between">
                          <span>Labor costs</span>
                          <span className="text-red-600">↑ 5.2%</span>
                        </div>
                        <div className="flex justify-between">
                          <span>Materials</span>
                          <span className="text-green-600">↓ 2.1%</span>
                        </div>
                        <div className="flex justify-between">
                          <span>Equipment</span>
                          <span className="text-orange-600">↑ 1.8%</span>
                        </div>
                      </div>
                    </div>
                    <div className="space-y-3">
                      <h4 className="font-medium">Recommendations</h4>
                      <ul className="text-sm space-y-1 text-muted-foreground">
                        <li>• Consider preventive maintenance to reduce emergency repairs</li>
                        <li>• Evaluate bulk purchasing for frequent replacement parts</li>
                        <li>• Review contractor pricing for specialized services</li>
                      </ul>
                    </div>
                  </div>
                </CardContent>
              </Card>
            </TabsContent>

            {/* Energy Analysis Tab */}
            <TabsContent value="energy" className="space-y-6">
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <Card>
                  <CardContent className="p-6 text-center">
                    <div className="text-2xl font-bold text-blue-600">1,222</div>
                    <p className="text-sm text-muted-foreground">Avg Daily Consumption (kWh)</p>
                  </CardContent>
                </Card>
                <Card>
                  <CardContent className="p-6 text-center">
                    <div className="text-2xl font-bold text-green-600">88.4%</div>
                    <p className="text-sm text-muted-foreground">Energy Efficiency</p>
                  </CardContent>
                </Card>
                <Card>
                  <CardContent className="p-6 text-center">
                    <div className="text-2xl font-bold text-orange-600">$183</div>
                    <p className="text-sm text-muted-foreground">Daily Energy Cost</p>
                  </CardContent>
                </Card>
              </div>

              <EnergyPerformanceChart
                data={displayData.energyData}
                loading={dashboardState.loading}
                error={dashboardState.error}
              />
            </TabsContent>
          </Tabs>
        </div>
      </div>
    </div>
  );
}

// #endregion