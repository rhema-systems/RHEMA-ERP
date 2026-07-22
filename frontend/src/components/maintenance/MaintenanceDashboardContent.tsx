'use client';

import React, { useEffect, useMemo, useState } from 'react';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import {
  Activity,
  AlertTriangle,
  Calendar,
  CheckCircle,
  Clock,
  CloudUpload,
  DollarSign,
  FileText,
  Gauge,
  Package,
  QrCode,
  RefreshCw,
  ShieldCheck,
  Smartphone,
  TrendingUp,
  Users,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { apiService } from '@/services/api.service';
import { useMaintenanceCurrency } from '@/hooks/useMaintenanceCurrency';

interface DashboardSummary {
  totalAssets?: number;
  activeAssets?: number;
  criticalAssets?: number;
  assetsRequiringMaintenance?: number;
  totalWorkOrders?: number;
  activeWorkOrders?: number;
  overdueWorkOrders?: number;
  completedWorkOrders?: number;
  totalTechnicians?: number;
  availableTechnicians?: number;
  complianceRate?: number;
}

interface MaintenanceKpis {
  mttr?: number;
  mtbf?: number;
  scheduleCompliance?: number;
  firstTimeFixRate?: number;
  plannedMaintenancePercentage?: number;
  overallEquipmentEffectiveness?: number;
  maintenanceCostPerAsset?: number;
  workOrderCompletionRate?: number;
  averageWorkOrderDuration?: number;
  preventiveMaintenanceRatio?: number;
}

interface MaintenanceDashboardOverview {
  summary?: DashboardSummary;
  workOrdersByStatus?: Record<string, number>;
  workOrdersByPriority?: Record<string, number>;
  assetsByStatus?: Record<string, number>;
  maintenanceKPIs?: MaintenanceKpis;
  recentAlerts?: MaintenanceAlert[];
}

interface AssetHealthSummaryDto {
  totalAssets: number;
  healthyAssets: number;
  warningAssets: number;
  criticalAssets: number;
  offlineAssets: number;
}

interface TrendDataPointDto {
  date: string;
  value: number;
  metricType: string;
  label?: string;
}

interface WorkOrderTypeMetricDto {
  workOrderType: string;
  count: number;
  percentage: number;
  averageCost: number;
  averageCompletionTime: number;
}

interface WorkOrderTrendsDto {
  creationTrend: TrendDataPointDto[];
  completionTrend: TrendDataPointDto[];
  costTrend: TrendDataPointDto[];
  typeDistribution: WorkOrderTypeMetricDto[];
}

interface MaintenanceHistoryItemDto {
  id: string;
  title: string;
  assetName: string;
  status: string;
  priority: string;
  completedDate?: string;
  cost?: number;
}

interface MaintenanceScheduleDto {
  id: string;
  assetName: string;
  maintenanceTypeName?: string;
  maintenanceType?: string;
  name?: string;
  nextDue?: string;
  nextDueDate?: string;
  nextScheduledDate?: string;
  assignedTechnicianName?: string;
  priority?: string;
  isOverdue?: boolean;
}

interface MaintenanceCostAnalysisDto {
  totalCost?: number;
  totalMaintenanceCost?: number;
  preventiveCost?: number;
  correctiveCost?: number;
  plannedMaintenanceCost?: number;
  unplannedMaintenanceCost?: number;
  costPerWorkOrder?: number;
  costByCategory?: Array<{ category: string; cost: number; percentage: number }>;
  costByAsset?: Array<{ assetId: string; assetName: string; cost: number; percentage: number }>;
  costTrend?: TrendDataPointDto[];
}

interface MaintenanceAlert {
  id: string;
  title?: string;
  message?: string;
  description?: string;
  severity?: string;
  priority?: string;
  assetName?: string;
  createdAt?: string;
  createdDate?: string;
}

interface MaintenanceAlertsDto {
  criticalAlerts?: MaintenanceAlert[];
  warningAlerts?: MaintenanceAlert[];
  infoAlerts?: MaintenanceAlert[];
  totalCritical?: number;
  totalWarning?: number;
  totalInfo?: number;
}

interface MaintenanceBacklogDto {
  totalWorkOrders: number;
  overdueWorkOrders: number;
  estimatedHours: number;
  estimatedCost: number;
  averageAge: number;
  priorityBreakdown: Array<{ priority: string; count: number }>;
}

interface FleetInspectionSheetBreakdownDto {
  sheetType: string;
  total: number;
  failed: number;
  flagged: number;
  offlineCaptured: number;
}

interface FleetInspectionRecentIssueDto {
  inspectionId: string;
  defectId?: string | null;
  workOrderId?: string | null;
  assetName: string;
  assetNumber: string;
  templateName: string;
  sheetType: string;
  overallResult: string;
  severity: string;
  defectStatus: string;
  workOrderStatus?: string | null;
  reportedAtUtc: string;
}

interface FleetInspectionOperationsDashboardDto {
  startDateUtc: string;
  endDateUtc: string;
  lastUpdatedUtc: string;
  totalInspections: number;
  syncedInspections: number;
  offlineCapturedInspections: number;
  passedInspections: number;
  failedInspections: number;
  flaggedInspections: number;
  defectsCreated: number;
  workOrdersCreated: number;
  openFollowUpWorkOrders: number;
  qrEnabledTemplates: number;
  staleQrTemplates: number;
  syncRate: number;
  failureRate: number;
  workOrderFollowUpRate: number;
  sheetBreakdown: FleetInspectionSheetBreakdownDto[];
  recentIssues: FleetInspectionRecentIssueDto[];
}

interface DashboardState {
  overview: MaintenanceDashboardOverview;
  kpis: MaintenanceKpis;
  assetHealth: AssetHealthSummaryDto | null;
  trends: WorkOrderTrendsDto | null;
  history: MaintenanceHistoryItemDto[];
  schedules: MaintenanceScheduleDto[];
  cost: MaintenanceCostAnalysisDto | null;
  alerts: MaintenanceAlert[];
  backlog: MaintenanceBacklogDto | null;
  fleetInspectionOps: FleetInspectionOperationsDashboardDto | null;
}

type RangeKey = '30' | '90' | '180' | 'ytd';

const STATUS_COLORS = ['#2563eb', '#16a34a', '#f59e0b', '#dc2626', '#7c3aed', '#64748b'];
const PRIORITY_COLORS = ['#dc2626', '#f97316', '#2563eb', '#16a34a', '#64748b'];

const EMPTY_STATE: DashboardState = {
  overview: {},
  kpis: {},
  assetHealth: null,
  trends: null,
  history: [],
  schedules: [],
  cost: null,
  alerts: [],
  backlog: null,
  fleetInspectionOps: null,
};

function getDateRange(range: RangeKey) {
  const endDate = new Date();
  const startDate = new Date(endDate);

  if (range === 'ytd') {
    startDate.setMonth(0, 1);
    startDate.setHours(0, 0, 0, 0);
    return { startDate, endDate };
  }

  startDate.setDate(endDate.getDate() - Number(range));
  return { startDate, endDate };
}

function fulfilledValue<T, F>(result: PromiseSettledResult<T>, fallback: F): T | F {
  return result.status === 'fulfilled' ? result.value : fallback;
}

function toChartData(values?: Record<string, number>, colors = STATUS_COLORS) {
  return Object.entries(values || {})
    .filter(([, value]) => Number(value) > 0)
    .map(([name, value], index) => ({
      name,
      value,
      color: colors[index % colors.length],
    }));
}

function formatNumber(value?: number | null, fractionDigits = 0) {
  const safeValue = typeof value === 'number' && Number.isFinite(value) ? value : 0;
  return safeValue.toLocaleString(undefined, {
    maximumFractionDigits: fractionDigits,
  });
}

function formatPercent(value?: number | null) {
  return `${formatNumber(value, 1)}%`;
}

function formatHours(value?: number | null) {
  return `${formatNumber(value, 1)}h`;
}

function formatDate(value?: string | null) {
  if (!value) return '-';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '-';
  return date.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
}

function formatSheetType(value?: string | null) {
  return (value || 'InspectionSheet').replace(/([a-z])([A-Z])/g, '$1 $2');
}

function normalizeSeverity(severity?: string) {
  const value = severity?.toLowerCase() || '';
  if (value.includes('critical') || value.includes('error')) return 'destructive';
  if (value.includes('warning')) return 'warning';
  return 'default';
}

function getStatusBadgeClass(status: string) {
  const value = status.toLowerCase();
  if (value.includes('complete') || value.includes('closed')) return 'bg-green-100 text-green-800';
  if (value.includes('progress')) return 'bg-blue-100 text-blue-800';
  if (value.includes('hold')) return 'bg-amber-100 text-amber-800';
  if (value.includes('cancel')) return 'bg-gray-100 text-gray-800';
  return 'bg-slate-100 text-slate-800';
}

function getPriorityBadgeClass(priority: string) {
  const value = priority.toLowerCase();
  if (value.includes('critical') || value.includes('emergency')) return 'bg-red-100 text-red-800';
  if (value.includes('high')) return 'bg-orange-100 text-orange-800';
  if (value.includes('low')) return 'bg-green-100 text-green-800';
  return 'bg-blue-100 text-blue-800';
}

interface MetricCardProps {
  title: string;
  value: string;
  description: string;
  icon: React.ElementType;
  accent: string;
  progress?: number;
}

function MetricCard({ title, value, description, icon: Icon, accent, progress }: MetricCardProps) {
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
        <CardTitle className="text-sm font-medium">{title}</CardTitle>
        <Icon className={`h-4 w-4 ${accent}`} />
      </CardHeader>
      <CardContent>
        <div className="text-2xl font-bold">{value}</div>
        <p className="mt-1 text-xs text-muted-foreground">{description}</p>
        {typeof progress === 'number' ? (
          <Progress value={Math.max(0, Math.min(progress, 100))} className="mt-3 h-2" />
        ) : null}
      </CardContent>
    </Card>
  );
}

export function MaintenanceDashboardContent() {
  const { formatMoney } = useMaintenanceCurrency();
  const [range, setRange] = useState<RangeKey>('90');
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dashboard, setDashboard] = useState<DashboardState>(EMPTY_STATE);

  const loadDashboardData = async (showRefreshState = false) => {
    if (showRefreshState) {
      setRefreshing(true);
    } else {
      setLoading(true);
    }

    setError(null);

    try {
      const { startDate, endDate } = getDateRange(range);
      const start = startDate.toISOString();
      const end = endDate.toISOString();

      const results = await Promise.allSettled([
        apiService.get<MaintenanceDashboardOverview>('/maintenance/dashboard/overview'),
        apiService.get<AssetHealthSummaryDto>('/maintenance/dashboard/asset-health'),
        apiService.get<WorkOrderTrendsDto>(`/maintenance/dashboard/work-order-trends?startDate=${start}&endDate=${end}`),
        apiService.get<MaintenanceHistoryItemDto[]>(`/maintenance/history?startDate=${start}&endDate=${end}`),
        apiService.get<MaintenanceScheduleDto[]>('/maintenance/schedules/due-in-days/14'),
        apiService.get<MaintenanceKpis>(`/maintenance/dashboard/kpis?startDate=${start}&endDate=${end}`),
        apiService.get<MaintenanceCostAnalysisDto>(`/maintenance/dashboard/cost-analysis?startDate=${start}&endDate=${end}`),
        apiService.get<MaintenanceAlertsDto>('/maintenance/dashboard/alerts'),
        apiService.get<MaintenanceBacklogDto>('/maintenance/dashboard/backlog-analysis'),
        apiService.get<FleetInspectionOperationsDashboardDto>(`/maintenance/dashboard/fleet-inspections?startDate=${start}&endDate=${end}`),
      ] as const);

      const overview = fulfilledValue(results[0], {} as MaintenanceDashboardOverview);
      const alertGroups = fulfilledValue(results[7], {} as MaintenanceAlertsDto);
      const alerts = [
        ...(alertGroups.criticalAlerts || []),
        ...(alertGroups.warningAlerts || []),
        ...(alertGroups.infoAlerts || []),
        ...(overview.recentAlerts || []),
      ].slice(0, 6);

      setDashboard({
        overview,
        assetHealth: fulfilledValue(results[1], null),
        trends: fulfilledValue(results[2], null),
        history: fulfilledValue(results[3], [] as MaintenanceHistoryItemDto[]),
        schedules: fulfilledValue(results[4], [] as MaintenanceScheduleDto[]),
        kpis: {
          ...(overview.maintenanceKPIs || {}),
          ...fulfilledValue(results[5], {} as MaintenanceKpis),
        },
        cost: fulfilledValue(results[6], null),
        alerts,
        backlog: fulfilledValue(results[8], null),
        fleetInspectionOps: fulfilledValue(results[9], null),
      });

      if (results.some((result) => result.status === 'rejected')) {
        setError('Some dashboard analytics could not be loaded. Available figures are still shown.');
      }
    } catch (loadError) {
      console.error('Failed to load maintenance dashboard data:', loadError);
      setError('Maintenance dashboard data could not be loaded.');
      setDashboard(EMPTY_STATE);
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => {
    loadDashboardData();
  }, [range]);

  const summary = dashboard.overview.summary || {};
  const kpis = dashboard.kpis || {};
  const costTotal = dashboard.cost?.totalMaintenanceCost ?? dashboard.cost?.totalCost ?? 0;
  const completionRate = kpis.workOrderCompletionRate ?? (
    summary.totalWorkOrders ? ((summary.completedWorkOrders || 0) / summary.totalWorkOrders) * 100 : 0
  );
  const scheduleCompliance = kpis.scheduleCompliance ?? Number(summary.complianceRate || 0);
  const healthTotal = dashboard.assetHealth?.totalAssets || summary.totalAssets || 0;
  const healthyAssets = dashboard.assetHealth?.healthyAssets || 0;
  const assetHealthRate = healthTotal > 0 ? (healthyAssets / healthTotal) * 100 : 0;
  const totalAlerts = dashboard.alerts.length;

  const statusData = useMemo(
    () => toChartData(dashboard.overview.workOrdersByStatus),
    [dashboard.overview.workOrdersByStatus],
  );

  const priorityData = useMemo(
    () => toChartData(dashboard.overview.workOrdersByPriority, PRIORITY_COLORS),
    [dashboard.overview.workOrdersByPriority],
  );

  const assetStatusData = useMemo(
    () => toChartData(dashboard.overview.assetsByStatus),
    [dashboard.overview.assetsByStatus],
  );

  const trendData = useMemo(() => {
    const creation = dashboard.trends?.creationTrend || [];
    const completion = dashboard.trends?.completionTrend || [];
    const cost = dashboard.trends?.costTrend || [];

    return creation.map((point, index) => ({
      month: point.label || new Date(point.date).toLocaleDateString(undefined, { month: 'short', year: '2-digit' }),
      created: point.value || 0,
      completed: completion[index]?.value || 0,
      cost: cost[index]?.value || 0,
    }));
  }, [dashboard.trends]);

  const typeDistribution = useMemo(
    () => (dashboard.trends?.typeDistribution || []).slice(0, 8),
    [dashboard.trends],
  );

  const costTrend = useMemo(
    () => (dashboard.cost?.costTrend || []).map((point) => ({
      month: point.label || new Date(point.date).toLocaleDateString(undefined, { month: 'short', year: '2-digit' }),
      cost: point.value || 0,
    })),
    [dashboard.cost],
  );

  const upcoming = (dashboard.schedules || []).slice(0, 6);
  const recentWorkOrders = (dashboard.history || []).slice(0, 6);
  const topCostAssets = (dashboard.cost?.costByAsset || []).slice(0, 5);
  const backlogPriorities = dashboard.backlog?.priorityBreakdown || [];
  const fleetInspectionOps = dashboard.fleetInspectionOps;
  const sheetBreakdown = fleetInspectionOps?.sheetBreakdown || [];
  const recentInspectionIssues = fleetInspectionOps?.recentIssues || [];

  if (loading) {
    return (
      <div className="flex h-[60vh] w-full items-center justify-center">
        <div className="flex flex-col items-center space-y-3">
          <div className="h-8 w-8 animate-spin rounded-full border-2 border-muted-foreground border-t-transparent" />
          <p className="text-sm text-muted-foreground">Loading maintenance dashboard...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h2 className="text-2xl font-semibold tracking-tight">Maintenance Analytics Dashboard</h2>
          <p className="text-sm text-muted-foreground">Operational health, cost, backlog, and reliability signals</p>
        </div>
        <div className="flex items-center gap-2">
          <Select value={range} onValueChange={(value) => setRange(value as RangeKey)}>
            <SelectTrigger className="w-[170px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="30">Last 30 days</SelectItem>
              <SelectItem value="90">Last 90 days</SelectItem>
              <SelectItem value="180">Last 180 days</SelectItem>
              <SelectItem value="ytd">Year to date</SelectItem>
            </SelectContent>
          </Select>
          <Button variant="outline" size="icon" onClick={() => loadDashboardData(true)} disabled={refreshing}>
            <RefreshCw className={`h-4 w-4 ${refreshing ? 'animate-spin' : ''}`} />
          </Button>
        </div>
      </div>

      {error ? (
        <Alert>
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Partial data</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      ) : null}

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
        <MetricCard
          title="Total Work Orders"
          value={formatNumber(summary.totalWorkOrders)}
          description={`${formatNumber(summary.activeWorkOrders)} active, ${formatNumber(summary.overdueWorkOrders)} overdue`}
          icon={FileText}
          accent="text-blue-600"
        />
        <MetricCard
          title="Completion Rate"
          value={formatPercent(completionRate)}
          description={`${formatNumber(summary.completedWorkOrders)} completed work orders`}
          icon={CheckCircle}
          accent="text-green-600"
          progress={completionRate}
        />
        <MetricCard
          title="Maintenance Cost"
          value={formatMoney(costTotal, 0)}
          description={`${formatMoney(dashboard.cost?.costPerWorkOrder || 0, 0)} per completed order`}
          icon={DollarSign}
          accent="text-amber-600"
        />
        <MetricCard
          title="Asset Health"
          value={formatPercent(assetHealthRate)}
          description={`${formatNumber(summary.assetsRequiringMaintenance)} assets due for service`}
          icon={ShieldCheck}
          accent="text-emerald-600"
          progress={assetHealthRate}
        />
      </div>

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
        <MetricCard
          title="MTTR"
          value={formatHours(kpis.mttr)}
          description={`${formatHours(kpis.mtbf)} mean time between failures`}
          icon={Clock}
          accent="text-slate-600"
        />
        <MetricCard
          title="Schedule Compliance"
          value={formatPercent(scheduleCompliance)}
          description={`${formatPercent(kpis.plannedMaintenancePercentage ?? kpis.preventiveMaintenanceRatio)} planned maintenance mix`}
          icon={Calendar}
          accent="text-indigo-600"
          progress={scheduleCompliance}
        />
        <MetricCard
          title="OEE / Availability"
          value={formatPercent(kpis.overallEquipmentEffectiveness)}
          description={`${formatNumber(summary.activeAssets)} active of ${formatNumber(summary.totalAssets)} assets`}
          icon={Gauge}
          accent="text-purple-600"
          progress={kpis.overallEquipmentEffectiveness}
        />
        <MetricCard
          title="Technicians"
          value={formatNumber(summary.availableTechnicians)}
          description={`${formatNumber(summary.totalTechnicians)} total, ${formatNumber(totalAlerts)} active alerts`}
          icon={Users}
          accent="text-cyan-600"
        />
      </div>

      <div className="space-y-4">
        <div>
          <h3 className="text-lg font-semibold tracking-normal">Mobile Inspection Operations</h3>
          <p className="text-sm text-muted-foreground">Offline capture, checklist sync, failed checks, and follow-up Work Orders</p>
        </div>
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
          <MetricCard
            title="Mobile Checks"
            value={formatNumber(fleetInspectionOps?.totalInspections)}
            description={`${formatNumber(fleetInspectionOps?.offlineCapturedInspections)} captured offline`}
            icon={Smartphone}
            accent="text-emerald-600"
          />
          <MetricCard
            title="Server Sync"
            value={formatPercent(fleetInspectionOps?.syncRate)}
            description={`${formatNumber(fleetInspectionOps?.syncedInspections)} inspection submissions accepted`}
            icon={CloudUpload}
            accent="text-blue-600"
            progress={fleetInspectionOps?.syncRate}
          />
          <MetricCard
            title="Failed / Flagged"
            value={formatNumber((fleetInspectionOps?.failedInspections || 0) + (fleetInspectionOps?.flaggedInspections || 0))}
            description={`${formatPercent(fleetInspectionOps?.failureRate)} of submitted checks`}
            icon={AlertTriangle}
            accent="text-red-600"
            progress={fleetInspectionOps?.failureRate}
          />
          <MetricCard
            title="Follow-up Work Orders"
            value={formatNumber(fleetInspectionOps?.workOrdersCreated)}
            description={`${formatNumber(fleetInspectionOps?.openFollowUpWorkOrders)} still open`}
            icon={FileText}
            accent="text-amber-600"
            progress={fleetInspectionOps?.workOrderFollowUpRate}
          />
        </div>

        <div className="grid grid-cols-1 gap-6 xl:grid-cols-3">
          <Card>
            <CardHeader>
              <CardTitle>QR Checklist Readiness</CardTitle>
              <CardDescription>Templates available to the mobile offline catalog</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="flex items-center justify-between rounded-md border px-3 py-2">
                <div className="flex items-center gap-2">
                  <QrCode className="h-4 w-4 text-emerald-600" />
                  <span className="text-sm text-muted-foreground">QR-enabled sheets</span>
                </div>
                <span className="font-semibold">{formatNumber(fleetInspectionOps?.qrEnabledTemplates)}</span>
              </div>
              <div className="flex items-center justify-between rounded-md border px-3 py-2">
                <div className="flex items-center gap-2">
                  <RefreshCw className="h-4 w-4 text-amber-600" />
                  <span className="text-sm text-muted-foreground">Refresh review due</span>
                </div>
                <span className="font-semibold">{formatNumber(fleetInspectionOps?.staleQrTemplates)}</span>
              </div>
              <p className="text-xs text-muted-foreground">
                Refresh review counts QR/mobile templates not updated in the last 180 days.
              </p>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Sheet Breakdown</CardTitle>
              <CardDescription>Submissions by configured sheet category</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              {sheetBreakdown.length ? sheetBreakdown.map((item) => {
                const issueCount = item.failed + item.flagged;
                const issueRate = item.total > 0 ? (issueCount / item.total) * 100 : 0;
                return (
                  <div key={item.sheetType} className="space-y-1">
                    <div className="flex items-center justify-between gap-2 text-sm">
                      <span className="truncate font-medium">{formatSheetType(item.sheetType)}</span>
                      <span className="text-muted-foreground">{formatNumber(item.total)}</span>
                    </div>
                    <Progress value={issueRate} className="h-2" />
                    <p className="text-xs text-muted-foreground">
                      {formatNumber(issueCount)} failed or flagged, {formatNumber(item.offlineCaptured)} offline
                    </p>
                  </div>
                );
              }) : (
                <p className="text-sm text-muted-foreground">No mobile inspection submissions in this range.</p>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Recent Inspection Issues</CardTitle>
              <CardDescription>Failed or flagged checks with defect follow-up</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              {recentInspectionIssues.length ? recentInspectionIssues.map((issue) => (
                <div key={`${issue.inspectionId}-${issue.defectId || 'issue'}`} className="rounded-md border p-3">
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <p className="truncate text-sm font-medium">{issue.assetName}</p>
                      <p className="truncate text-xs text-muted-foreground">{issue.assetNumber || issue.templateName}</p>
                    </div>
                    <Badge className={getPriorityBadgeClass(issue.severity || issue.overallResult)}>
                      {issue.severity || issue.overallResult}
                    </Badge>
                  </div>
                  <div className="mt-2 flex flex-wrap gap-2 text-xs text-muted-foreground">
                    <span>{formatSheetType(issue.sheetType)}</span>
                    <span>{formatDate(issue.reportedAtUtc)}</span>
                    <span>{issue.workOrderStatus ? `WO ${issue.workOrderStatus}` : 'No Work Order'}</span>
                  </div>
                </div>
              )) : (
                <p className="text-sm text-muted-foreground">No failed or flagged inspection issues in this range.</p>
              )}
            </CardContent>
          </Card>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-3">
        <Card className="xl:col-span-2">
          <CardHeader>
            <CardTitle>Work Order Flow</CardTitle>
            <CardDescription>Created, completed, and cost trend for the selected range</CardDescription>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={320}>
              <LineChart data={trendData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="month" />
                <YAxis yAxisId="count" />
                <YAxis yAxisId="cost" orientation="right" tickFormatter={(value) => formatNumber(Number(value), 0)} />
                <Tooltip formatter={(value, name) => [name === 'cost' ? formatMoney(Number(value), 0) : formatNumber(Number(value)), name]} />
                <Line yAxisId="count" type="monotone" dataKey="created" stroke="#2563eb" strokeWidth={2} name="Created" />
                <Line yAxisId="count" type="monotone" dataKey="completed" stroke="#16a34a" strokeWidth={2} name="Completed" />
                <Line yAxisId="cost" type="monotone" dataKey="cost" stroke="#f59e0b" strokeWidth={2} name="Cost" />
              </LineChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Work Order Status</CardTitle>
            <CardDescription>Current operational distribution</CardDescription>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={240}>
              <PieChart>
                <Pie data={statusData} dataKey="value" innerRadius={56} outerRadius={88} paddingAngle={2}>
                  {statusData.map((entry) => (
                    <Cell key={entry.name} fill={entry.color} />
                  ))}
                </Pie>
                <Tooltip />
              </PieChart>
            </ResponsiveContainer>
            <div className="mt-3 grid grid-cols-2 gap-2">
              {statusData.map((item) => (
                <div key={item.name} className="flex items-center gap-2 text-sm">
                  <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: item.color }} />
                  <span className="truncate">{item.name}</span>
                  <span className="ml-auto font-medium">{item.value}</span>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      </div>

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle>Priority Backlog</CardTitle>
            <CardDescription>Open work by management urgency</CardDescription>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={250}>
              <BarChart data={backlogPriorities.length ? backlogPriorities : priorityData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey={backlogPriorities.length ? 'priority' : 'name'} />
                <YAxis allowDecimals={false} />
                <Tooltip />
                <Bar dataKey={backlogPriorities.length ? 'count' : 'value'} fill="#2563eb" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
            <div className="mt-4 grid grid-cols-3 gap-3 text-center">
              <div>
                <p className="text-lg font-semibold">{formatNumber(dashboard.backlog?.totalWorkOrders)}</p>
                <p className="text-xs text-muted-foreground">Open</p>
              </div>
              <div>
                <p className="text-lg font-semibold">{formatHours(dashboard.backlog?.estimatedHours)}</p>
                <p className="text-xs text-muted-foreground">Hours</p>
              </div>
              <div>
                <p className="text-lg font-semibold">{formatNumber(dashboard.backlog?.averageAge, 1)}d</p>
                <p className="text-xs text-muted-foreground">Avg age</p>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Maintenance Types</CardTitle>
            <CardDescription>Work mix for the selected range</CardDescription>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={250}>
              <BarChart data={typeDistribution}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="workOrderType" />
                <YAxis allowDecimals={false} />
                <Tooltip formatter={(value, name) => [formatNumber(Number(value), 1), name]} />
                <Bar dataKey="count" fill="#16a34a" radius={[4, 4, 0, 0]} name="Count" />
              </BarChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Asset Status</CardTitle>
            <CardDescription>Managed asset operating posture</CardDescription>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={250}>
              <BarChart data={assetStatusData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="name" />
                <YAxis allowDecimals={false} />
                <Tooltip />
                <Bar dataKey="value" fill="#7c3aed" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>
      </div>

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Cost Trend</CardTitle>
            <CardDescription>Maintenance spend over the selected range</CardDescription>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={280}>
              <BarChart data={costTrend}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="month" />
                <YAxis tickFormatter={(value) => formatNumber(Number(value), 0)} />
                <Tooltip formatter={(value) => [formatMoney(Number(value), 0), 'Cost']} />
                <Bar dataKey="cost" fill="#f59e0b" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Top Cost Assets</CardTitle>
            <CardDescription>Assets with the highest maintenance spend</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-4">
              {topCostAssets.length ? topCostAssets.map((asset) => (
                <div key={asset.assetId} className="space-y-1">
                  <div className="flex items-center justify-between gap-3">
                    <span className="truncate text-sm font-medium">{asset.assetName}</span>
                    <span className="text-sm font-semibold">{formatMoney(asset.cost, 0)}</span>
                  </div>
                  <Progress value={Number(asset.percentage) || 0} className="h-2" />
                </div>
              )) : (
                <p className="text-sm text-muted-foreground">No costed maintenance work in this range.</p>
              )}
            </div>
          </CardContent>
        </Card>
      </div>

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle>Alerts</CardTitle>
            <CardDescription>Items requiring management attention</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {dashboard.alerts.length ? dashboard.alerts.map((alert, index) => (
                <div key={`${alert.id}-${index}`} className="rounded-md border p-3">
                  <div className="flex items-start justify-between gap-2">
                    <p className="text-sm font-medium">{alert.title || alert.message || 'Maintenance alert'}</p>
                    <Badge variant={normalizeSeverity(alert.severity) === 'destructive' ? 'destructive' : 'outline'}>
                      {alert.severity || alert.priority || 'Info'}
                    </Badge>
                  </div>
                  <p className="mt-1 text-xs text-muted-foreground">{alert.description || alert.message || alert.assetName || ''}</p>
                </div>
              )) : (
                <p className="text-sm text-muted-foreground">No active alerts.</p>
              )}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Upcoming Maintenance</CardTitle>
            <CardDescription>Due within the next 14 days</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {upcoming.length ? upcoming.map((task) => (
                <div key={task.id} className="flex items-center justify-between gap-3 rounded-md border p-3">
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium">{task.assetName || 'Unknown asset'}</p>
                    <p className="truncate text-xs text-muted-foreground">
                      {task.maintenanceTypeName || task.maintenanceType || task.name || 'Scheduled Maintenance'}
                    </p>
                  </div>
                  <div className="text-right">
                    <p className="text-sm font-medium">
                      {formatDate(task.nextDue || task.nextDueDate || task.nextScheduledDate)}
                    </p>
                    <Badge variant="outline">{task.priority || (task.isOverdue ? 'Overdue' : 'Due')}</Badge>
                  </div>
                </div>
              )) : (
                <p className="text-sm text-muted-foreground">No scheduled maintenance due soon.</p>
              )}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Recent Work Orders</CardTitle>
            <CardDescription>Latest maintenance activity in the selected range</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {recentWorkOrders.length ? recentWorkOrders.map((order) => (
                <div key={order.id} className="rounded-md border p-3">
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <p className="truncate text-sm font-medium">{order.title}</p>
                      <p className="truncate text-xs text-muted-foreground">{order.assetName}</p>
                    </div>
                    <Badge className={getStatusBadgeClass(order.status)}>{order.status}</Badge>
                  </div>
                  <div className="mt-2 flex items-center justify-between">
                    <Badge className={getPriorityBadgeClass(order.priority)}>{order.priority || 'Normal'}</Badge>
                    <span className="text-xs text-muted-foreground">{formatDate(order.completedDate)}</span>
                  </div>
                </div>
              )) : (
                <p className="text-sm text-muted-foreground">No recent work orders in this range.</p>
              )}
            </div>
          </CardContent>
        </Card>
      </div>

      <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-sm font-medium">
              <Activity className="h-4 w-4 text-blue-600" />
              First Time Fix
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold">{formatPercent(kpis.firstTimeFixRate)}</p>
            <Progress value={kpis.firstTimeFixRate || 0} className="mt-3 h-2" />
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-sm font-medium">
              <TrendingUp className="h-4 w-4 text-green-600" />
              Cost Per Asset
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold">{formatMoney(kpis.maintenanceCostPerAsset || 0, 0)}</p>
            <p className="mt-1 text-xs text-muted-foreground">{formatMoney(dashboard.backlog?.estimatedCost || 0, 0)} estimated backlog</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-sm font-medium">
              <Package className="h-4 w-4 text-purple-600" />
              Critical Assets
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold">{formatNumber(summary.criticalAssets)}</p>
            <p className="mt-1 text-xs text-muted-foreground">{formatNumber(dashboard.assetHealth?.offlineAssets)} offline assets</p>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
