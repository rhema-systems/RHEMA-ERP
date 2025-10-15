'use client';

import React from 'react';
import { BaseLineChart, BaseAreaChart, BaseBarChart, BasePieChart, BaseRadialBarChart, CHART_COLORS } from './BaseCharts';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import { 
  TrendingUp, 
  TrendingDown, 
  AlertTriangle, 
  CheckCircle, 
  Clock, 
  DollarSign,
  Zap,
  Target
} from 'lucide-react';

// #region OEE Gauge Component

interface OeeGaugeProps {
  oeeValue: number;
  availability?: number;
  performance?: number;
  quality?: number;
  className?: string;
  size?: 'sm' | 'md' | 'lg';
}

export const OeeGauge: React.FC<OeeGaugeProps> = ({
  oeeValue,
  availability,
  performance,
  quality,
  className,
  size = 'md',
}) => {
  const getOeeColor = (value: number) => {
    if (value >= 85) return 'text-green-600';
    if (value >= 60) return 'text-yellow-600';
    return 'text-red-600';
  };

  const getOeeStatus = (value: number) => {
    if (value >= 85) return { label: 'Excellent', variant: 'default' as const };
    if (value >= 60) return { label: 'Good', variant: 'secondary' as const };
    return { label: 'Poor', variant: 'destructive' as const };
  };

  const sizeClasses = {
    sm: 'h-32 w-32',
    md: 'h-40 w-40',
    lg: 'h-48 w-48',
  };

  const status = getOeeStatus(oeeValue);

  return (
    <Card className={cn('text-center', className)}>
      <CardHeader>
        <CardTitle className="flex items-center justify-center gap-2">
          <Target className="h-5 w-5" />
          Overall Equipment Effectiveness
        </CardTitle>
        <CardDescription>Current OEE Performance</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex justify-center">
          <div className={cn('relative', sizeClasses[size])}>
            <svg className="w-full h-full transform -rotate-90" viewBox="0 0 100 100">
              {/* Background circle */}
              <circle
                cx="50"
                cy="50"
                r="45"
                stroke="currentColor"
                strokeWidth="8"
                fill="none"
                className="text-gray-200"
              />
              {/* Progress circle */}
              <circle
                cx="50"
                cy="50"
                r="45"
                stroke="currentColor"
                strokeWidth="8"
                fill="none"
                strokeDasharray={`${2 * Math.PI * 45}`}
                strokeDashoffset={`${2 * Math.PI * 45 * (1 - oeeValue / 100)}`}
                className={getOeeColor(oeeValue)}
                strokeLinecap="round"
              />
            </svg>
            <div className="absolute inset-0 flex items-center justify-center">
              <div className="text-center">
                <div className={cn('text-3xl font-bold', getOeeColor(oeeValue))}>
                  {oeeValue.toFixed(1)}%
                </div>
                <Badge variant={status.variant} className="mt-1">
                  {status.label}
                </Badge>
              </div>
            </div>
          </div>
        </div>

        {/* OEE Components */}
        {(availability !== undefined || performance !== undefined || quality !== undefined) && (
          <div className="grid grid-cols-3 gap-4 text-sm">
            {availability !== undefined && (
              <div className="text-center">
                <div className="font-medium text-blue-600">Availability</div>
                <div className="text-lg font-bold">{availability.toFixed(1)}%</div>
                <Progress value={availability} className="h-2 mt-1" />
              </div>
            )}
            {performance !== undefined && (
              <div className="text-center">
                <div className="font-medium text-green-600">Performance</div>
                <div className="text-lg font-bold">{performance.toFixed(1)}%</div>
                <Progress value={performance} className="h-2 mt-1" />
              </div>
            )}
            {quality !== undefined && (
              <div className="text-center">
                <div className="font-medium text-yellow-600">Quality</div>
                <div className="text-lg font-bold">{quality.toFixed(1)}%</div>
                <Progress value={quality} className="h-2 mt-1" />
              </div>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
};

// #endregion

// #region Performance Trend Chart

interface PerformanceTrendData {
  date: string;
  oee: number;
  availability: number;
  performance: number;
  quality: number;
}

interface PerformanceTrendChartProps {
  data: PerformanceTrendData[];
  loading?: boolean;
  error?: string;
  className?: string;
}

export const PerformanceTrendChart: React.FC<PerformanceTrendChartProps> = ({
  data,
  loading,
  error,
  className,
}) => {
  const formatPercentage = (value: any) => `${value}%`;

  return (
    <BaseLineChart
      data={data}
      xAxisKey="date"
      lines={[
        { dataKey: 'oee', name: 'OEE', color: CHART_COLORS.primary[0], strokeWidth: 3 },
        { dataKey: 'availability', name: 'Availability', color: CHART_COLORS.oee[0] },
        { dataKey: 'performance', name: 'Performance', color: CHART_COLORS.oee[1] },
        { dataKey: 'quality', name: 'Quality', color: CHART_COLORS.oee[2] },
      ]}
      formatValue={formatPercentage}
      title="Performance Trends"
      description="OEE and component metrics over time"
      className={className}
      loading={loading}
      error={error}
      height={350}
    />
  );
};

// #endregion

// #region Reliability Metrics Chart

interface ReliabilityData {
  assetName: string;
  mtbf: number;
  mttr: number;
  availability: number;
}

interface ReliabilityMetricsChartProps {
  data: ReliabilityData[];
  loading?: boolean;
  error?: string;
  className?: string;
}

export const ReliabilityMetricsChart: React.FC<ReliabilityMetricsChartProps> = ({
  data,
  loading,
  error,
  className,
}) => {
  const formatHours = (value: any, name: string) => {
    if (name.toLowerCase().includes('availability')) {
      return `${value}%`;
    }
    return `${value}h`;
  };

  return (
    <BaseBarChart
      data={data}
      xAxisKey="assetName"
      bars={[
        { dataKey: 'mtbf', name: 'MTBF (hours)', color: CHART_COLORS.success[0] },
        { dataKey: 'mttr', name: 'MTTR (hours)', color: CHART_COLORS.warning[0] },
        { dataKey: 'availability', name: 'Availability %', color: CHART_COLORS.primary[0] },
      ]}
      formatValue={formatHours}
      title="Reliability Metrics"
      description="Mean Time Between Failures, Mean Time To Repair, and Availability"
      className={className}
      loading={loading}
      error={error}
      height={350}
    />
  );
};

// #endregion

// #region Cost Analysis Chart

interface CostData {
  category: string;
  cost: number;
  percentage: number;
}

interface CostAnalysisChartProps {
  data: CostData[];
  totalCost: number;
  loading?: boolean;
  error?: string;
  className?: string;
}

export const CostAnalysisChart: React.FC<CostAnalysisChartProps> = ({
  data,
  totalCost,
  loading,
  error,
  className,
}) => {
  return (
    <div className={cn('grid grid-cols-1 lg:grid-cols-2 gap-4', className)}>
      <BasePieChart
        data={data}
        dataKey="cost"
        nameKey="category"
        colors={[...CHART_COLORS.primary, ...CHART_COLORS.success]}
        title="Cost Breakdown"
        description={`Total: $${totalCost.toLocaleString()}`}
        loading={loading}
        error={error}
        height={300}
      />
      
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <DollarSign className="h-5 w-5" />
            Cost Details
          </CardTitle>
          <CardDescription>Breakdown by category</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="space-y-3">
              {[...Array(4)].map((_, i) => (
                <div key={i} className="animate-pulse">
                  <div className="h-4 bg-gray-200 rounded w-3/4 mb-2"></div>
                  <div className="h-2 bg-gray-200 rounded"></div>
                </div>
              ))}
            </div>
          ) : error ? (
            <div className="text-center text-red-500">{error}</div>
          ) : (
            <div className="space-y-4">
              {data.map((item, index) => (
                <div key={item.category} className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <div 
                      className="w-3 h-3 rounded-full" 
                      style={{ 
                        backgroundColor: CHART_COLORS.primary[index % CHART_COLORS.primary.length] 
                      }}
                    />
                    <span className="text-sm font-medium">{item.category}</span>
                  </div>
                  <div className="text-right">
                    <div className="font-bold">${item.cost.toLocaleString()}</div>
                    <div className="text-sm text-muted-foreground">{item.percentage.toFixed(1)}%</div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
};

// #endregion

// #region Energy Performance Chart

interface EnergyData {
  date: string;
  consumption: number;
  efficiency: number;
  cost: number;
}

interface EnergyPerformanceChartProps {
  data: EnergyData[];
  loading?: boolean;
  error?: string;
  className?: string;
}

export const EnergyPerformanceChart: React.FC<EnergyPerformanceChartProps> = ({
  data,
  loading,
  error,
  className,
}) => {
  return (
    <BaseAreaChart
      data={data}
      xAxisKey="date"
      areas={[
        { dataKey: 'consumption', name: 'Energy Consumption (kWh)', color: CHART_COLORS.warning[0], stackId: '1' },
        { dataKey: 'cost', name: 'Energy Cost ($)', color: CHART_COLORS.danger[0], stackId: '2' },
      ]}
      title="Energy Performance"
      description="Energy consumption and costs over time"
      className={className}
      loading={loading}
      error={error}
      height={350}
      formatValue={(value, name) => {
        if (name.includes('Cost')) return `$${value.toLocaleString()}`;
        if (name.includes('kWh')) return `${value.toLocaleString()} kWh`;
        return value.toString();
      }}
    />
  );
};

// #endregion

// #region Asset Health Score Component

interface AssetHealthScoreProps {
  healthScore: number;
  trend: 'up' | 'down' | 'stable';
  riskLevel: 'low' | 'medium' | 'high' | 'critical';
  className?: string;
}

export const AssetHealthScore: React.FC<AssetHealthScoreProps> = ({
  healthScore,
  trend,
  riskLevel,
  className,
}) => {
  const getRiskColor = (risk: string) => {
    switch (risk) {
      case 'low': return { color: 'text-green-600', bg: 'bg-green-100', variant: 'default' as const };
      case 'medium': return { color: 'text-yellow-600', bg: 'bg-yellow-100', variant: 'secondary' as const };
      case 'high': return { color: 'text-orange-600', bg: 'bg-orange-100', variant: 'destructive' as const };
      case 'critical': return { color: 'text-red-600', bg: 'bg-red-100', variant: 'destructive' as const };
      default: return { color: 'text-gray-600', bg: 'bg-gray-100', variant: 'outline' as const };
    }
  };

  const getTrendIcon = () => {
    switch (trend) {
      case 'up': return <TrendingUp className="h-4 w-4 text-green-600" />;
      case 'down': return <TrendingDown className="h-4 w-4 text-red-600" />;
      case 'stable': return <div className="h-4 w-4 border-t-2 border-gray-400" />;
    }
  };

  const riskStyle = getRiskColor(riskLevel);

  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <div className={cn('p-2 rounded-full', riskStyle.bg)}>
            {riskLevel === 'critical' ? (
              <AlertTriangle className={cn('h-5 w-5', riskStyle.color)} />
            ) : (
              <CheckCircle className={cn('h-5 w-5', riskStyle.color)} />
            )}
          </div>
          Asset Health Score
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="flex items-center justify-between mb-4">
          <div className="text-3xl font-bold">{healthScore}/100</div>
          <div className="flex items-center gap-2">
            {getTrendIcon()}
            <Badge variant={riskStyle.variant} className="capitalize">
              {riskLevel} Risk
            </Badge>
          </div>
        </div>
        <Progress value={healthScore} className="h-3" />
        <div className="flex justify-between text-sm text-muted-foreground mt-2">
          <span>Poor</span>
          <span>Excellent</span>
        </div>
      </CardContent>
    </Card>
  );
};

// #endregion

// #region Maintenance KPI Cards

interface KpiCardProps {
  title: string;
  value: string;
  change: number;
  changeLabel: string;
  icon: React.ReactNode;
  color?: 'blue' | 'green' | 'yellow' | 'red';
  className?: string;
}

export const KpiCard: React.FC<KpiCardProps> = ({
  title,
  value,
  change,
  changeLabel,
  icon,
  color = 'blue',
  className,
}) => {
  const colorClasses = {
    blue: { icon: 'text-blue-600 bg-blue-100', trend: change >= 0 ? 'text-green-600' : 'text-red-600' },
    green: { icon: 'text-green-600 bg-green-100', trend: change >= 0 ? 'text-green-600' : 'text-red-600' },
    yellow: { icon: 'text-yellow-600 bg-yellow-100', trend: change >= 0 ? 'text-green-600' : 'text-red-600' },
    red: { icon: 'text-red-600 bg-red-100', trend: change >= 0 ? 'text-green-600' : 'text-red-600' },
  };

  return (
    <Card className={className}>
      <CardContent className="p-6">
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm font-medium text-muted-foreground">{title}</p>
            <p className="text-2xl font-bold">{value}</p>
            <div className="flex items-center mt-1">
              {change >= 0 ? (
                <TrendingUp className="h-4 w-4 mr-1 text-green-600" />
              ) : (
                <TrendingDown className="h-4 w-4 mr-1 text-red-600" />
              )}
              <span className={cn('text-sm font-medium', colorClasses[color].trend)}>
                {Math.abs(change)}% {changeLabel}
              </span>
            </div>
          </div>
          <div className={cn('p-3 rounded-full', colorClasses[color].icon)}>
            {icon}
          </div>
        </div>
      </CardContent>
    </Card>
  );
};

// Predefined KPI Cards for common metrics
export const OeeKpiCard: React.FC<{ value: number; change: number; className?: string }> = ({ 
  value, 
  change, 
  className 
}) => (
  <KpiCard
    title="Overall Equipment Effectiveness"
    value={`${value.toFixed(1)}%`}
    change={change}
    changeLabel="from last period"
    icon={<Target className="h-6 w-6" />}
    color="blue"
    className={className}
  />
);

export const UptimeKpiCard: React.FC<{ value: number; change: number; className?: string }> = ({ 
  value, 
  change, 
  className 
}) => (
  <KpiCard
    title="Asset Uptime"
    value={`${value.toFixed(1)}%`}
    change={change}
    changeLabel="from last period"
    icon={<Clock className="h-6 w-6" />}
    color="green"
    className={className}
  />
);

export const CostKpiCard: React.FC<{ value: number; change: number; className?: string }> = ({ 
  value, 
  change, 
  className 
}) => (
  <KpiCard
    title="Total Maintenance Cost"
    value={`$${value.toLocaleString()}`}
    change={change}
    changeLabel="from last period"
    icon={<DollarSign className="h-6 w-6" />}
    color="red"
    className={className}
  />
);

export const EnergyKpiCard: React.FC<{ value: number; change: number; className?: string }> = ({ 
  value, 
  change, 
  className 
}) => (
  <KpiCard
    title="Energy Efficiency"
    value={`${value.toFixed(1)}%`}
    change={change}
    changeLabel="from last period"
    icon={<Zap className="h-6 w-6" />}
    color="yellow"
    className={className}
  />
);

// #endregion