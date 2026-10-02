'use client';

import React from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { ArrowUpRight } from 'lucide-react';
import {
  AreaChart,
  Area,
  BarChart,
  Bar,
  FunnelChart,
  Funnel,
  LabelList,
  LineChart,
  Line,
  PieChart,
  Pie,
  Cell,
  RadialBarChart,
  RadialBar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
  TooltipProps,
} from 'recharts';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { cn } from '@/lib/utils';

// #region Chart Color Palettes

export const CHART_COLORS = {
  primary: ['#3b82f6', '#1d4ed8', '#1e40af', '#1e3a8a'],
  success: ['#10b981', '#059669', '#047857', '#065f46'],
  warning: ['#f59e0b', '#d97706', '#b45309', '#92400e'],
  danger: ['#ef4444', '#dc2626', '#b91c1c', '#991b1b'],
  info: ['#06b6d4', '#0891b2', '#0e7490', '#155e75'],
  neutral: ['#6b7280', '#4b5563', '#374151', '#1f2937'],
  performance: ['#10b981', '#f59e0b', '#ef4444'], // Good, Warning, Poor
  oee: ['#3b82f6', '#10b981', '#f59e0b'], // Availability, Performance, Quality
};

// #endregion

// #region Base Chart Props

interface BaseChartProps {
  data: any[];
  className?: string;
  height?: number;
  title?: string;
  description?: string;
  loading?: boolean;
  error?: string;
  compact?: boolean;
  detailsHref?: string;
  detailsLabel?: string;
  getDatumHref?: (datum: Record<string, unknown>) => string | undefined;
}

interface ChartContainerProps extends BaseChartProps {
  children: React.ReactElement;
}

// #endregion

// #region Chart Container Component

const ChartContainer: React.FC<ChartContainerProps> = ({
  children,
  data,
  title,
  description,
  className,
  height = 300,
  loading = false,
  error,
  compact = false,
  detailsHref,
  detailsLabel = 'View details',
}) => {
  const heading = title || description ? (
    <CardHeader className={cn(compact && 'px-5 pb-2 pt-4 space-y-1')}>
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          {title && <CardTitle className={cn(compact && 'text-base leading-tight')}>{title}</CardTitle>}
          {description && <CardDescription className={cn(compact && 'text-sm leading-5')}>{description}</CardDescription>}
        </div>
        {detailsHref ? (
          <Link
            href={detailsHref}
            className="inline-flex shrink-0 items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-primary transition-colors hover:bg-primary/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            aria-label={`${detailsLabel}${title ? ` for ${title}` : ''}`}
          >
            {detailsLabel}
            <ArrowUpRight className="h-3.5 w-3.5" aria-hidden="true" />
          </Link>
        ) : null}
      </div>
    </CardHeader>
  ) : null;

  if (loading) {
    return (
      <Card className={cn('w-full', className)}>
        {heading}
        <CardContent className={cn(compact && 'px-5 pb-5 pt-0')}>
          <div className="flex items-center justify-center" style={{ height }}>
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
          </div>
        </CardContent>
      </Card>
    );
  }

  if (error) {
    return (
      <Card className={cn('w-full', className)}>
        {heading}
        <CardContent className={cn(compact && 'px-5 pb-5 pt-0')}>
          <div className="flex items-center justify-center" style={{ height }}>
            <div className="text-center">
              <div className="text-red-500 mb-2">⚠️</div>
              <p className="text-sm text-muted-foreground">{error}</p>
            </div>
          </div>
        </CardContent>
      </Card>
    );
  }

  if (!data || data.length === 0) {
    return (
      <Card className={cn('w-full', className)}>
        {heading}
        <CardContent className={cn(compact && 'px-5 pb-5 pt-0')}>
          <div className="flex items-center justify-center" style={{ height: Math.min(height, compact ? 156 : height) }}>
            <div className="text-center">
              <p className="text-sm font-medium text-foreground">No live data yet</p>
              <p className="mt-1 text-sm text-muted-foreground">This widget will populate as records flow into the module.</p>
            </div>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className={cn('w-full', className)}>
      {heading}
      <CardContent className={cn(compact && 'px-5 pb-5 pt-0')}>
        <ResponsiveContainer width="100%" height={height}>
          {children}
        </ResponsiveContainer>
      </CardContent>
    </Card>
  );
};

// #endregion

// #region Custom Tooltip Component

interface CustomTooltipEntry {
  color?: string;
  name?: string;
  value?: string | number;
}

interface CustomTooltipProps {
  active?: boolean;
  payload?: CustomTooltipEntry[];
  label?: string | number;
  formatValue?: (value: any, name: string) => string;
}

const CustomTooltip: React.FC<CustomTooltipProps> = ({
  active,
  payload,
  label,
  formatValue,
}) => {
  if (active && payload && payload.length) {
    return (
      <div className="bg-background border border-border rounded-lg shadow-lg p-3">
        <p className="font-medium text-sm mb-2">{label}</p>
        {payload.map((entry: CustomTooltipEntry, index: number) => (
          <div key={index} className="flex items-center gap-2 text-sm">
            <div
              className="w-3 h-3 rounded-full"
              style={{ backgroundColor: entry.color }}
            />
            <span className="text-muted-foreground">{entry.name}:</span>
            <span className="font-medium">
              {formatValue ? formatValue(entry.value, entry.name || '') : entry.value}
            </span>
          </div>
        ))}
      </div>
    );
  }
  return null;
};

// #endregion

// #region Line Chart Component

interface LineChartProps extends BaseChartProps {
  xAxisKey: string;
  lines: Array<{
    dataKey: string;
    name: string;
    color?: string;
    strokeWidth?: number;
  }>;
  formatValue?: (value: any, name: string) => string;
  showGrid?: boolean;
  showLegend?: boolean;
}

export const BaseLineChart: React.FC<LineChartProps> = ({
  data,
  xAxisKey,
  lines,
  formatValue,
  showGrid = true,
  showLegend = true,
  className,
  height,
  title,
  description,
  loading,
  error,
  compact,
  detailsHref,
  detailsLabel,
}) => {
  return (
    <ChartContainer
      className={className}
      height={height}
      title={title}
      description={description}
      loading={loading}
      error={error}
      data={data}
      compact={compact}
      detailsHref={detailsHref}
      detailsLabel={detailsLabel}
    >
      <LineChart data={data}>
        {showGrid && <CartesianGrid strokeDasharray="3 3" className="stroke-muted" />}
        <XAxis 
          dataKey={xAxisKey}
          className="text-xs fill-muted-foreground"
          tickLine={false}
          axisLine={false}
        />
        <YAxis 
          className="text-xs fill-muted-foreground"
          tickLine={false}
          axisLine={false}
        />
        <Tooltip content={<CustomTooltip formatValue={formatValue} />} />
        {showLegend && <Legend />}
        {lines.map((line, index) => (
          <Line
            key={line.dataKey}
            type="monotone"
            dataKey={line.dataKey}
            name={line.name}
            stroke={line.color || CHART_COLORS.primary[index % CHART_COLORS.primary.length]}
            strokeWidth={line.strokeWidth || 2}
            dot={{ fill: line.color || CHART_COLORS.primary[index % CHART_COLORS.primary.length], strokeWidth: 2, r: 4 }}
            activeDot={{ r: 6, strokeWidth: 2 }}
          />
        ))}
      </LineChart>
    </ChartContainer>
  );
};

// #endregion

// #region Area Chart Component

interface AreaChartProps extends BaseChartProps {
  xAxisKey: string;
  areas: Array<{
    dataKey: string;
    name: string;
    color?: string;
    stackId?: string;
  }>;
  formatValue?: (value: any, name: string) => string;
  showGrid?: boolean;
  showLegend?: boolean;
}

export const BaseAreaChart: React.FC<AreaChartProps> = ({
  data,
  xAxisKey,
  areas,
  formatValue,
  showGrid = true,
  showLegend = true,
  className,
  height,
  title,
  description,
  loading,
  error,
  compact,
  detailsHref,
  detailsLabel,
}) => {
  return (
    <ChartContainer
      className={className}
      height={height}
      title={title}
      description={description}
      loading={loading}
      error={error}
      data={data}
      compact={compact}
      detailsHref={detailsHref}
      detailsLabel={detailsLabel}
    >
      <AreaChart data={data}>
        {showGrid && <CartesianGrid strokeDasharray="3 3" className="stroke-muted" />}
        <XAxis 
          dataKey={xAxisKey}
          className="text-xs fill-muted-foreground"
          tickLine={false}
          axisLine={false}
        />
        <YAxis 
          className="text-xs fill-muted-foreground"
          tickLine={false}
          axisLine={false}
        />
        <Tooltip content={<CustomTooltip formatValue={formatValue} />} />
        {showLegend && <Legend />}
        {areas.map((area, index) => (
          <Area
            key={area.dataKey}
            type="monotone"
            dataKey={area.dataKey}
            name={area.name}
            stackId={area.stackId}
            stroke={area.color || CHART_COLORS.primary[index % CHART_COLORS.primary.length]}
            fill={area.color || CHART_COLORS.primary[index % CHART_COLORS.primary.length]}
            fillOpacity={0.6}
          />
        ))}
      </AreaChart>
    </ChartContainer>
  );
};

// #endregion

// #region Bar Chart Component

interface BarChartProps extends BaseChartProps {
  xAxisKey: string;
  bars: Array<{
    dataKey: string;
    name: string;
    color?: string;
  }>;
  formatValue?: (value: any, name: string) => string;
  showGrid?: boolean;
  showLegend?: boolean;
  orientation?: 'horizontal' | 'vertical';
}

export const BaseBarChart: React.FC<BarChartProps> = ({
  data,
  xAxisKey,
  bars,
  formatValue,
  showGrid = true,
  showLegend = true,
  orientation = 'vertical',
  className,
  height,
  title,
  description,
  loading,
  error,
  compact,
  detailsHref,
  detailsLabel,
  getDatumHref,
}) => {
  const isHorizontal = orientation === 'horizontal';
  const router = useRouter();
  const handleDatumClick = getDatumHref
    ? (datum: any) => {
      const record = (datum?.payload ?? datum) as Record<string, unknown>;
      const href = getDatumHref(record);
      if (href) router.push(href);
    }
    : undefined;

  return (
    <ChartContainer
      className={className}
      height={height}
      title={title}
      description={description}
      loading={loading}
      error={error}
      data={data}
      compact={compact}
      detailsHref={detailsHref}
      detailsLabel={detailsLabel}
    >
      <BarChart data={data} layout={isHorizontal ? 'vertical' : 'horizontal'}>
        {showGrid && <CartesianGrid strokeDasharray="3 3" className="stroke-muted" />}
        <XAxis 
          dataKey={isHorizontal ? undefined : xAxisKey}
          type={isHorizontal ? 'number' : 'category'}
          className="text-xs fill-muted-foreground"
          tickLine={false}
          axisLine={false}
        />
        <YAxis 
          dataKey={isHorizontal ? xAxisKey : undefined}
          type={isHorizontal ? 'category' : 'number'}
          className="text-xs fill-muted-foreground"
          tickLine={false}
          axisLine={false}
        />
        <Tooltip content={<CustomTooltip formatValue={formatValue} />} />
        {showLegend && <Legend />}
        {bars.map((bar, index) => (
          <Bar
            key={bar.dataKey}
            dataKey={bar.dataKey}
            name={bar.name}
            fill={bar.color || CHART_COLORS.primary[index % CHART_COLORS.primary.length]}
            radius={4}
            onClick={handleDatumClick}
            className={cn(getDatumHref && 'cursor-pointer')}
          />
        ))}
      </BarChart>
    </ChartContainer>
  );
};

// #endregion

// #region Pie Chart Component

interface PieChartProps extends BaseChartProps {
  dataKey: string;
  nameKey: string;
  colors?: string[];
  showLabels?: boolean;
  innerRadius?: number;
}

export const BasePieChart: React.FC<PieChartProps> = ({
  data,
  dataKey,
  nameKey,
  colors = CHART_COLORS.primary,
  showLabels = true,
  innerRadius = 0,
  className,
  height,
  title,
  description,
  loading,
  error,
  compact,
  detailsHref,
  detailsLabel,
  getDatumHref,
}) => {
  const router = useRouter();
  const handleDatumClick = getDatumHref
    ? (datum: any) => {
      const record = (datum?.payload ?? datum) as Record<string, unknown>;
      const href = getDatumHref(record);
      if (href) router.push(href);
    }
    : undefined;

  return (
    <ChartContainer
      className={className}
      height={height}
      title={title}
      description={description}
      loading={loading}
      error={error}
      data={data}
      compact={compact}
      detailsHref={detailsHref}
      detailsLabel={detailsLabel}
    >
      <PieChart>
        <Pie
          data={data}
          cx="50%"
          cy="50%"
          innerRadius={innerRadius}
          outerRadius={80}
          paddingAngle={2}
          dataKey={dataKey}
          nameKey={nameKey}
          label={showLabels ? ({ name, value }) => `${name}: ${value}` : false}
          onClick={handleDatumClick}
          className={cn(getDatumHref && 'cursor-pointer')}
        >
          {data.map((entry, index) => (
            <Cell key={`cell-${index}`} fill={colors[index % colors.length]} />
          ))}
        </Pie>
        <Tooltip />
        <Legend />
      </PieChart>
    </ChartContainer>
  );
};

// #endregion

// #region Radial Bar Chart Component (for gauges/progress)

interface RadialBarChartProps extends BaseChartProps {
  dataKey: string;
  maxValue?: number;
  colors?: string[];
  innerRadius?: number;
  outerRadius?: number;
}

export const BaseRadialBarChart: React.FC<RadialBarChartProps> = ({
  data,
  dataKey,
  maxValue = 100,
  colors = CHART_COLORS.performance,
  innerRadius = 40,
  outerRadius = 80,
  className,
  height,
  title,
  description,
  loading,
  error,
  compact,
  detailsHref,
  detailsLabel,
}) => {
  return (
    <ChartContainer
      className={className}
      height={height}
      title={title}
      description={description}
      loading={loading}
      error={error}
      data={data}
      compact={compact}
      detailsHref={detailsHref}
      detailsLabel={detailsLabel}
    >
      <RadialBarChart cx="50%" cy="50%" innerRadius={innerRadius} outerRadius={outerRadius} data={data}>
        <RadialBar
          dataKey={dataKey}
          cornerRadius={4}
          fill={colors[0]}
          background={{ fill: '#f1f5f9' }}
        />
        <Tooltip />
      </RadialBarChart>
    </ChartContainer>
  );
};

// #endregion

// #region Funnel Chart Component

interface FunnelChartProps extends BaseChartProps {
  dataKey: string;
  nameKey: string;
  colors?: string[];
  showLabels?: boolean;
  formatValue?: (value: any, name: string) => string;
}

export const BaseFunnelChart: React.FC<FunnelChartProps> = ({
  data,
  dataKey,
  nameKey,
  colors = CHART_COLORS.primary,
  showLabels = true,
  formatValue,
  className,
  height,
  title,
  description,
  loading,
  error,
  compact,
  detailsHref,
  detailsLabel,
  getDatumHref,
}) => {
  const router = useRouter();
  const handleDatumClick = getDatumHref
    ? (datum: any) => {
      const record = (datum?.payload ?? datum) as Record<string, unknown>;
      const href = getDatumHref(record);
      if (href) router.push(href);
    }
    : undefined;

  return (
    <ChartContainer
      className={className}
      height={height}
      title={title}
      description={description}
      loading={loading}
      error={error}
      data={data}
      compact={compact}
      detailsHref={detailsHref}
      detailsLabel={detailsLabel}
    >
      <FunnelChart>
        <Tooltip content={<CustomTooltip formatValue={formatValue} />} />
        <Funnel
          data={data}
          dataKey={dataKey}
          nameKey={nameKey}
          isAnimationActive
          onClick={handleDatumClick}
          className={cn(getDatumHref && 'cursor-pointer')}
        >
          {data.map((_, index) => (
            <Cell key={`funnel-cell-${index}`} fill={colors[index % colors.length]} />
          ))}
          {showLabels ? <LabelList position="right" fill="currentColor" stroke="none" dataKey={nameKey} /> : null}
        </Funnel>
      </FunnelChart>
    </ChartContainer>
  );
};

// #endregion

export const BaseTreeChart = BaseBarChart;
