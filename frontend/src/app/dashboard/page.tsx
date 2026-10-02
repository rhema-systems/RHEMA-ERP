'use client';

import Link from 'next/link';
import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { differenceInCalendarDays, format, subMonths } from 'date-fns';
import type { DateRange } from 'react-day-picker';
import {
  AlertTriangle,
  ArrowRight,
  Banknote,
  Boxes,
  Clock3,
  FileText,
  FolderKanban,
  Gauge,
  MapPin,
  ShieldAlert,
  type LucideIcon,
  RefreshCw,
  ShoppingCart,
  TrendingUp,
  WalletCards,
  Wrench,
} from 'lucide-react';
import { DashboardLayout } from '../../components/layout/dashboard-layout';
import {
  BaseBarChart,
  BaseFunnelChart,
  BaseLineChart,
  BasePieChart,
  CHART_COLORS,
} from '../../components/analytics/charts/BaseCharts';
import { Alert, AlertDescription } from '../../components/ui/alert';
import { Badge } from '../../components/ui/badge';
import { Button } from '../../components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../components/ui/card';
import { DatePickerWithRange } from '../../components/ui/date-range-picker';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../../components/ui/select';
import { Skeleton } from '../../components/ui/skeleton';
import { useAuth } from '../../hooks/use-auth';
import { getAuthenticatedHomePath, getExternalPortalPath, isCandidateUser, isConsultantClientUser, isExternalPortalUser } from '../../lib/auth-routing';
import { cn } from '../../lib/utils';
import { formatCurrencyAmount } from '../../lib/currency';
import { authService } from '../../services/auth';
import { dashboardService, getUnavailableDashboardModules, resolveDashboardReportingCurrency } from '../../services/dashboard';
import { inventoryWarehouseService } from '../../services/inventoryWarehouseService';

interface SummaryCardDefinition {
  title: string;
  value: string;
  meta: string;
  href: string;
  accentClassName: string;
  iconClassName: string;
  icon: LucideIcon;
}

const formatNumber = (value: number) =>
  new Intl.NumberFormat('en-US', {
    maximumFractionDigits: 0,
  }).format(value);

const formatRelativeTime = (value?: string) => {
  if (!value) return 'No timestamp';

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return 'No timestamp';

  const diffMinutes = Math.floor((Date.now() - date.getTime()) / (1000 * 60));

  if (diffMinutes < 1) return 'Just now';
  if (diffMinutes < 60) return `${diffMinutes}m ago`;

  const diffHours = Math.floor(diffMinutes / 60);
  if (diffHours < 24) return `${diffHours}h ago`;

  const diffDays = Math.floor(diffHours / 24);
  if (diffDays < 7) return `${diffDays}d ago`;

  return date.toLocaleDateString();
};

const formatMoneyPoints = (points: Array<{ amount: number; currency: string }>) =>
  points.length === 0
    ? 'No value'
    : points.slice(0, 2).map((point) => {
      const currency = point.currency.trim().toUpperCase();
      return /^[A-Z]{3}$/.test(currency)
        ? formatCurrencyAmount(point.amount, currency)
        : `${formatNumber(point.amount)} (currency unavailable)`;
    }).join(' · ');

const formatComparison = (change: number | null, label: string) => {
  if (change === null) return `No ${label} baseline`;
  if (change === 0) return `No change vs ${label}`;
  return `${change > 0 ? '+' : ''}${change.toFixed(1)}% vs ${label}`;
};

function DashboardSkeleton() {
  return (
    <div className="space-y-5" aria-label="Loading enterprise dashboard">
      <Skeleton className="h-36 w-full rounded-[28px]" />
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-6">
        {Array.from({ length: 6 }).map((_, index) => (
          <Skeleton key={index} className="h-36 rounded-xl" />
        ))}
      </div>
      <div className="grid gap-4 lg:grid-cols-2 2xl:grid-cols-3">
        {Array.from({ length: 6 }).map((_, index) => (
          <Skeleton key={index} className="h-[300px] rounded-xl" />
        ))}
      </div>
    </div>
  );
}

const createDefaultDashboardRange = (): DateRange => {
  const to = new Date();
  to.setHours(0, 0, 0, 0);

  return {
    from: subMonths(to, 5),
    to,
  };
};

const toDashboardDate = (value: Date) => format(value, 'yyyy-MM-dd');

export default function Dashboard() {
  const router = useRouter();
  const { user } = useAuth();
  const storedUser = authService.getStoredUser();
  const effectiveUser = user ?? storedUser;
  const shouldRouteToExternalPortal =
    isExternalPortalUser(effectiveUser) || isCandidateUser(effectiveUser) || isConsultantClientUser(effectiveUser);
  const [selectedRange, setSelectedRange] = useState<DateRange>(createDefaultDashboardRange);
  const [appliedRange, setAppliedRange] = useState<DateRange>(createDefaultDashboardRange);
  const [rangeError, setRangeError] = useState<string | null>(null);
  const [warehouseId, setWarehouseId] = useState('all');
  const [locationId, setLocationId] = useState('all');

  const { data: warehouseOptions = [] } = useQuery({
    queryKey: ['enterprise-dashboard', 'warehouses'],
    queryFn: () => inventoryWarehouseService.getActiveWarehouses(),
    enabled: !shouldRouteToExternalPortal,
    staleTime: 5 * 60_000,
    retry: false,
  });

  const { data: locationOptions = [] } = useQuery({
    queryKey: ['enterprise-dashboard', 'locations', warehouseId],
    queryFn: () => inventoryWarehouseService.getWarehouseLocations(warehouseId),
    enabled: !shouldRouteToExternalPortal && warehouseId !== 'all',
    staleTime: 5 * 60_000,
    retry: false,
  });

  const rangeQuery = useMemo(() => {
    if (!appliedRange.from || !appliedRange.to) return null;

    return {
      startDate: toDashboardDate(appliedRange.from),
      endDate: toDashboardDate(appliedRange.to),
      warehouseId: warehouseId === 'all' ? undefined : warehouseId,
      locationId: locationId === 'all' ? undefined : locationId,
    };
  }, [appliedRange, locationId, warehouseId]);

  useEffect(() => {
    if (typeof window === 'undefined') return;

    if (shouldRouteToExternalPortal) {
      // Candidates land on their careers home, client contacts on their timesheets,
      // business partners on the portal landing.
      router.push(getAuthenticatedHomePath(effectiveUser));
    }
  }, [router, shouldRouteToExternalPortal, effectiveUser]);

  const { data, error: dashboardError, isLoading, isFetching, refetch } = useQuery({
    queryKey: [
      'enterprise-dashboard',
      rangeQuery?.startDate,
      rangeQuery?.endDate,
      rangeQuery?.warehouseId,
      rangeQuery?.locationId,
    ],
    queryFn: () => {
      if (!rangeQuery) {
        throw new Error('A complete dashboard date range is required.');
      }

      return dashboardService.getEnterpriseDashboard(
        rangeQuery.startDate,
        rangeQuery.endDate,
        rangeQuery.warehouseId,
        rangeQuery.locationId,
      );
    },
    enabled: !shouldRouteToExternalPortal && rangeQuery !== null,
    staleTime: 60_000,
    placeholderData: (previousData) => previousData,
  });

  const handleRangeChange = (range: DateRange | undefined) => {
    if (!range) return;

    if (range.from && range.to && differenceInCalendarDays(range.to, range.from) > 3660) {
      setRangeError('Select a dashboard period of 10 years or less.');
      return;
    }

    setRangeError(null);
    setSelectedRange(range);
    if (range.from && range.to) {
      setAppliedRange(range);
    }
  };

  const displayName = user?.firstName || storedUser?.firstName || user?.username || storedUser?.username || 'User';

  if (dashboardError && !data) {
    return (
      <DashboardLayout>
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription className="flex items-center justify-between gap-4">
            <span>The enterprise dashboard could not be loaded. Please retry.</span>
            <Button variant="outline" size="sm" onClick={() => refetch()}>
              Retry
            </Button>
          </AlertDescription>
        </Alert>
      </DashboardLayout>
    );
  }

  if (isLoading || !data) {
    return (
      <DashboardLayout>
        <DashboardSkeleton />
      </DashboardLayout>
    );
  }

  const unavailableModules = getUnavailableDashboardModules(data.moduleStatus);
  const moduleIsAvailable = (...names: string[]) => names.every((name) =>
    data.moduleStatus.find((module) => module.module === name)?.available !== false);
  const { currencyCode: reportingCurrency, decimalPlaces: reportingDecimals } = resolveDashboardReportingCurrency(data);
  const formatReportingMoney = (value: number) => reportingCurrency
    ? formatCurrencyAmount(value, reportingCurrency, reportingDecimals)
    : 'Currency unavailable';

  const maintenanceSummary = data.maintenanceOverview?.summary;
  const maintenanceMetrics = data.maintenanceMetrics;
  const maintenanceTotalWorkOrders = maintenanceMetrics?.totalWorkOrders ?? maintenanceSummary?.totalWorkOrders ?? 0;
  const maintenanceActiveWorkOrders = maintenanceMetrics?.pendingWorkOrders ?? maintenanceSummary?.activeWorkOrders ?? 0;
  const maintenanceOverdueWorkOrders = maintenanceMetrics?.overdueWorkOrders ?? maintenanceSummary?.overdueWorkOrders ?? 0;
  const maintenanceCompletedWorkOrders = maintenanceMetrics?.completedWorkOrders ?? maintenanceSummary?.completedWorkOrders ?? 0;
  const maintenanceCompletionRate =
    maintenanceMetrics?.completionRate
      ?? (maintenanceTotalWorkOrders > 0
        ? (maintenanceCompletedWorkOrders / Math.max(1, maintenanceTotalWorkOrders)) * 100
        : 0);

  const queues = data.operationalQueues;
  const summaryCards: SummaryCardDefinition[] = [];

  if (data.financeOverview) {
    summaryCards.push({
      title: 'Revenue',
      value: formatReportingMoney(data.financeOverview.kpis.revenue),
      meta: formatComparison(data.financeOverview.kpis.revenueChangePercent, 'previous period'),
      href: '/finance/reports/income-statement',
      accentClassName:
        'border-blue-200 bg-[linear-gradient(135deg,rgba(255,255,255,0.96),rgba(239,246,255,0.95))] dark:border-blue-900/70 dark:bg-[linear-gradient(135deg,rgba(2,6,23,0.96),rgba(30,64,175,0.22))]',
      iconClassName: 'bg-blue-500/15 text-blue-600 dark:text-blue-300',
      icon: Banknote,
    });
  }

  if (moduleIsAvailable('CRM')) summaryCards.push(
    {
      title: 'CRM Pipeline',
      value: formatNumber(data.crm?.openOpportunityCount ?? 0),
      meta: `${data.crm?.leadsNeedingFollowUpCount ?? 0} leads need follow-up`,
      href: '/crm',
      accentClassName:
        'border-emerald-200 bg-[linear-gradient(135deg,rgba(255,255,255,0.96),rgba(236,253,245,0.95))] dark:border-emerald-900/70 dark:bg-[linear-gradient(135deg,rgba(2,6,23,0.96),rgba(6,78,59,0.25))]',
      iconClassName: 'bg-emerald-500/15 text-emerald-600 dark:text-emerald-300',
      icon: TrendingUp,
    });

  if (moduleIsAvailable('Projects')) summaryCards.push(
    {
      title: 'Projects',
      value: formatNumber(data.projectDashboard?.activeProjects ?? 0),
      meta: `${data.projectDashboard?.overdueMilestones ?? 0} overdue milestones`,
      href: '/development/projects',
      accentClassName:
        'border-violet-200 bg-[linear-gradient(135deg,rgba(255,255,255,0.96),rgba(245,243,255,0.95))] dark:border-violet-900/70 dark:bg-[linear-gradient(135deg,rgba(2,6,23,0.96),rgba(76,29,149,0.22))]',
      iconClassName: 'bg-violet-500/15 text-violet-600 dark:text-violet-300',
      icon: FolderKanban,
    });

  if (moduleIsAvailable('Procurement Queues')) summaryCards.push(
    {
      title: 'Procurement',
      value: formatNumber(queues.openPurchaseOrderCount),
      meta: `${queues.pendingPurchaseRequisitionCount} requisitions waiting`,
      href: '/procurement/purchase-orders',
      accentClassName:
        'border-amber-200 bg-[linear-gradient(135deg,rgba(255,255,255,0.96),rgba(255,251,235,0.95))] dark:border-amber-900/70 dark:bg-[linear-gradient(135deg,rgba(2,6,23,0.96),rgba(120,53,15,0.24))]',
      iconClassName: 'bg-amber-500/15 text-amber-600 dark:text-amber-300',
      icon: ShoppingCart,
    });

  if (moduleIsAvailable('Inventory Queues')) summaryCards.push(
    {
      title: 'Inventory',
      value: data.procurementInventoryManagement
        ? formatReportingMoney(data.procurementInventoryManagement.inventory.stockValue)
        : formatNumber(queues.pendingInventoryApprovalCount + queues.pendingInventoryIssueCount),
      meta: `${queues.pendingInventoryIssueCount} issues · ${queues.pendingInventoryApprovalCount} approvals`,
      href: '/inventory/requisitions',
      accentClassName:
        'border-cyan-200 bg-[linear-gradient(135deg,rgba(255,255,255,0.96),rgba(236,254,255,0.95))] dark:border-cyan-900/70 dark:bg-[linear-gradient(135deg,rgba(2,6,23,0.96),rgba(21,94,117,0.24))]',
      iconClassName: 'bg-cyan-500/15 text-cyan-600 dark:text-cyan-300',
      icon: Boxes,
    });

  if (moduleIsAvailable('Maintenance Overview', 'Maintenance Metrics')) summaryCards.push(
    {
      title: 'Maintenance',
      value: formatNumber(maintenanceActiveWorkOrders),
      meta: `${maintenanceOverdueWorkOrders} overdue work orders`,
      href: '/maintenance',
      accentClassName:
        'border-rose-200 bg-[linear-gradient(135deg,rgba(255,255,255,0.96),rgba(255,241,242,0.95))] dark:border-rose-900/70 dark:bg-[linear-gradient(135deg,rgba(2,6,23,0.96),rgba(127,29,29,0.24))]',
      iconClassName: 'bg-rose-500/15 text-rose-600 dark:text-rose-300',
      icon: Wrench,
    });

  if (moduleIsAvailable('Procurement Queues')) summaryCards.push(
    {
      title: 'Tenders',
      value: formatNumber(queues.openTenderCount),
      meta: `${queues.tendersClosingWithin14DaysCount} closing within 14 days`,
      href: '/procurement/tenders',
      accentClassName:
        'border-sky-200 bg-[linear-gradient(135deg,rgba(255,255,255,0.96),rgba(240,249,255,0.95))] dark:border-sky-900/70 dark:bg-[linear-gradient(135deg,rgba(2,6,23,0.96),rgba(12,74,110,0.24))]',
      iconClassName: 'bg-sky-500/15 text-sky-600 dark:text-sky-300',
      icon: FileText,
    });

  const pipelineStageData = (data.crm?.pipelineByStage ?? []).map((stage) => ({
    stage: stage.stage,
    opportunities: stage.opportunityCount,
    quotes: stage.quoteCount,
  }));

  const crmFunnelData = (data.crm?.conversionFunnel ?? []).map((stage) => ({
    stage: stage.stage,
    count: stage.count,
    conversionRate: stage.conversionRate,
  }));

  const crmHealthData = (data.crm?.accountRiskByBand ?? []).map((band) => ({
    name: band.label,
    value: band.count,
  }));

  const projectPressureData = [
    { name: 'Overdue Tasks', value: data.projectDashboard?.overdueTasks ?? 0 },
    { name: 'Overdue Milestones', value: data.projectDashboard?.overdueMilestones ?? 0 },
    { name: 'Open Risks', value: data.projectDashboard?.openRisks ?? 0 },
    { name: 'Open Issues', value: data.projectDashboard?.openIssues ?? 0 },
  ].filter((item) => item.value > 0);

  const inventoryQueueData = [
    { label: 'Approvals', count: queues.pendingInventoryApprovalCount },
    { label: 'Issues', count: queues.pendingInventoryIssueCount },
  ];

  const maintenanceTrendData = (data.maintenanceTrends?.creationTrend ?? []).map((point, index) => ({
    period: new Date(point.date).toLocaleDateString(undefined, { month: 'short', day: 'numeric' }),
    created: point.value,
    completed: data.maintenanceTrends?.completionTrend[index]?.value ?? 0,
  }));

  const maintenanceStatusData = [
    { name: 'Active', value: maintenanceActiveWorkOrders },
    { name: 'Overdue', value: maintenanceOverdueWorkOrders },
    { name: 'Completed', value: maintenanceCompletedWorkOrders },
  ].filter((item) => item.value > 0);

  const tenderStatusData = queues.openTendersByStatus.map((status) => ({ name: status.label, value: status.count }));

  const queueLoadData = [
    { module: 'CRM', items: (data.crm?.leadsNeedingFollowUpCount ?? 0) + (data.crm?.atRiskAccountCount ?? 0) },
    {
      module: 'Projects',
      items: (data.projectDashboard?.overdueTasks ?? 0) + (data.projectDashboard?.overdueMilestones ?? 0) + (data.projectDashboard?.openRisks ?? 0),
    },
    { module: 'Procurement', items: queues.pendingPurchaseRequisitionCount + queues.openPurchaseOrderCount },
    { module: 'Inventory', items: queues.pendingInventoryApprovalCount + queues.pendingInventoryIssueCount },
    { module: 'Maintenance', items: maintenanceActiveWorkOrders + maintenanceOverdueWorkOrders },
    { module: 'Tenders', items: queues.openTenderCount + queues.tendersClosingWithin14DaysCount },
  ];

  const criticalAlertCount =
    (data.crm?.leadsNeedingFollowUpCount ?? 0) +
    (data.projectDashboard?.overdueMilestones ?? 0) +
    maintenanceOverdueWorkOrders +
    queues.tendersClosingWithin14DaysCount;

  const management = data.procurementInventoryManagement;
  const managementSpendByCategory = (management?.spendByCategory ?? []).slice(0, 10).map((item) => ({
    label: `${item.label} · ${item.currency}`,
    amount: item.amount,
  }));
  const managementSpendByDepartment = (management?.spendByDepartment ?? []).slice(0, 10).map((item) => ({
    label: `${item.label} · ${item.currency}`,
    amount: item.amount,
  }));
  const managementInventoryByCategory = (management?.inventory.valueByCategory ?? []).slice(0, 10).map((item) => ({
    label: item.label,
    value: item.value,
  }));
  const managementSupplierRisk = (management?.supplierRisk.byRiskBand ?? []).map((item) => ({
    name: item.label,
    value: item.count,
  }));

  return (
    <DashboardLayout>
      <div className="space-y-5">
        <section className="rounded-[28px] border border-slate-200/80 bg-[radial-gradient(circle_at_top_left,_rgba(59,130,246,0.12),_transparent_28%),radial-gradient(circle_at_top_right,_rgba(16,185,129,0.12),_transparent_24%),linear-gradient(135deg,rgba(255,255,255,0.96),rgba(248,250,252,0.98))] p-5 shadow-[0_24px_60px_-24px_rgba(15,23,42,0.25)] dark:border-slate-800/80 dark:bg-[radial-gradient(circle_at_top_left,_rgba(59,130,246,0.18),_transparent_28%),radial-gradient(circle_at_top_right,_rgba(16,185,129,0.14),_transparent_24%),linear-gradient(135deg,rgba(2,6,23,0.96),rgba(15,23,42,0.98))]">
          <div className="flex flex-col gap-4 xl:flex-row xl:items-end xl:justify-between">
            <div className="space-y-2">
              <div className="flex flex-wrap items-center gap-2">
                <Badge variant="outline" className="border-slate-300 bg-white/70 text-slate-700 dark:border-slate-700 dark:bg-slate-950/40 dark:text-slate-300">
                  {criticalAlertCount} active alerts
                </Badge>
                <Badge variant="outline" className="border-amber-200 bg-amber-50/80 text-amber-700 dark:border-amber-800 dark:bg-amber-950/30 dark:text-amber-300">
                  {queues.tendersClosingWithin14DaysCount} tenders closing soon
                </Badge>
                <Badge variant="outline" className="border-rose-200 bg-rose-50/80 text-rose-700 dark:border-rose-800 dark:bg-rose-950/30 dark:text-rose-300">
                  {Math.round(maintenanceCompletionRate)}% maintenance completion
                </Badge>
              </div>
              <div>
                <h1 className="text-[1.75rem] font-bold tracking-tight text-slate-950 dark:text-slate-50">Dashboard</h1>
                <p className="mt-1 max-w-3xl text-sm leading-5 text-slate-600 dark:text-slate-300">
                  Welcome back, {displayName}. Select a reporting period to refresh period-sensitive activity while retaining the current operational context.
                </p>
              </div>
            </div>

            <div className="flex flex-wrap items-center justify-end gap-2">
              <DatePickerWithRange
                value={selectedRange}
                onChange={handleRangeChange}
                className="w-full sm:w-[290px]"
                placeholder="Select dashboard period"
              />
              <Select
                value={warehouseId}
                onValueChange={(value) => {
                  setWarehouseId(value);
                  setLocationId('all');
                }}
              >
                <SelectTrigger className="w-full bg-white/80 sm:w-[210px] dark:bg-slate-950/40" aria-label="Dashboard warehouse">
                  <MapPin className="mr-2 h-4 w-4 shrink-0 text-slate-500" />
                  <SelectValue placeholder="All permitted warehouses" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All permitted warehouses</SelectItem>
                  {warehouseOptions.map((warehouse) => (
                    <SelectItem key={warehouse.id} value={warehouse.id}>
                      {warehouse.code} · {warehouse.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Select value={locationId} onValueChange={setLocationId} disabled={warehouseId === 'all'}>
                <SelectTrigger className="w-full bg-white/80 sm:w-[210px] dark:bg-slate-950/40" aria-label="Dashboard warehouse location">
                  <SelectValue placeholder={warehouseId === 'all' ? 'Select warehouse first' : 'All permitted locations'} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All permitted locations</SelectItem>
                  {locationOptions.map((location) => (
                    <SelectItem key={location.id} value={location.id}>
                      {location.locationCode} · {location.name || location.locationCode}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Badge variant="secondary" className="rounded-full px-3 py-1">
                Updated {formatRelativeTime(data.lastUpdated)}
              </Badge>
              <Button variant="outline" size="sm" onClick={() => refetch()} disabled={isFetching} className="rounded-full bg-white/80 dark:bg-slate-950/40">
                <RefreshCw className={`mr-2 h-4 w-4 ${isFetching ? 'animate-spin' : ''}`} />
                Refresh
              </Button>
            </div>
          </div>
        </section>

        {unavailableModules.length > 0 && (
          <Alert>
            <AlertTriangle className="h-4 w-4" />
            <AlertDescription>
              Some business modules are unavailable right now: {unavailableModules.map((module) => module.module).join(', ')}.
              The dashboard is still showing live data for the modules that responded successfully.
            </AlertDescription>
          </Alert>
        )}

        {(rangeError || dashboardError) && (
          <Alert variant="destructive">
            <AlertTriangle className="h-4 w-4" />
            <AlertDescription>
              {rangeError || 'The selected dashboard period could not be refreshed. The previous period is still displayed.'}
            </AlertDescription>
          </Alert>
        )}

        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-6">
          {summaryCards.map((card) => {
            const Icon = card.icon;

            return (
              <Card key={card.title} className={cn('overflow-hidden border shadow-[0_16px_40px_-28px_rgba(15,23,42,0.4)] dark:shadow-none', card.accentClassName)}>
                <CardContent className="px-4 py-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <p className="text-xs font-semibold uppercase tracking-[0.2em] text-slate-500 dark:text-slate-400">{card.title}</p>
                      <p className="mt-2 text-2xl font-semibold tracking-tight text-slate-950 dark:text-slate-50">{card.value}</p>
                      <p className="mt-1 text-sm text-slate-600 dark:text-slate-300">{card.meta}</p>
                    </div>
                    <div className={cn('flex h-10 w-10 items-center justify-center rounded-2xl', card.iconClassName)}>
                      <Icon className="h-4.5 w-4.5" />
                    </div>
                  </div>
                  <div className="mt-3">
                    <Link href={card.href}>
                      <Button variant="ghost" size="sm" className="h-7 px-0">
                        Open
                        <ArrowRight className="ml-1.5 h-3.5 w-3.5" />
                      </Button>
                    </Link>
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </div>

        {data.financeOverview && (
          <section className="space-y-3" aria-labelledby="finance-performance-title">
            <div className="flex flex-wrap items-end justify-between gap-2">
              <div>
                <h2 id="finance-performance-title" className="text-lg font-semibold text-slate-950 dark:text-slate-50">
                  Financial performance
                </h2>
                <p className="text-xs text-slate-500 dark:text-slate-400">
                  Posted general-ledger activity from {format(new Date(data.financeOverview.rangeStartDate), 'dd MMM yyyy')} to{' '}
                  {format(new Date(data.financeOverview.rangeEndDate), 'dd MMM yyyy')} in {reportingCurrency || 'the configured reporting currency'}.
                </p>
              </div>
              <Link href="/finance/reports/income-statement">
                <Button variant="outline" size="sm">
                  Open income statement
                  <ArrowRight className="ml-1.5 h-3.5 w-3.5" />
                </Button>
              </Link>
            </div>

            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
              <Card className="border-emerald-200/80 dark:border-emerald-900/70">
                <CardContent className="flex items-start justify-between gap-3 p-4">
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Revenue</p>
                    <p className="mt-2 text-xl font-semibold">{formatReportingMoney(data.financeOverview.kpis.revenue)}</p>
                    <p className="mt-1 text-xs text-slate-500">{formatComparison(data.financeOverview.kpis.revenueChangePercent, 'previous period')}</p>
                  </div>
                  <TrendingUp className="h-5 w-5 text-emerald-600" />
                </CardContent>
              </Card>

              <Card className="border-amber-200/80 dark:border-amber-900/70">
                <CardContent className="flex items-start justify-between gap-3 p-4">
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Expenses</p>
                    <p className="mt-2 text-xl font-semibold">{formatReportingMoney(data.financeOverview.kpis.expenses)}</p>
                    <p className="mt-1 text-xs text-slate-500">{formatComparison(data.financeOverview.kpis.expensesChangePercent, 'previous period')}</p>
                  </div>
                  <Banknote className="h-5 w-5 text-amber-600" />
                </CardContent>
              </Card>

              <Card className="border-blue-200/80 dark:border-blue-900/70">
                <CardContent className="flex items-start justify-between gap-3 p-4">
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Net position</p>
                    <p className="mt-2 text-xl font-semibold">{formatReportingMoney(data.financeOverview.kpis.netProfit)}</p>
                    <p className="mt-1 text-xs text-slate-500">{formatComparison(data.financeOverview.kpis.netProfitChangePercent, 'previous period')}</p>
                  </div>
                  <Gauge className="h-5 w-5 text-blue-600" />
                </CardContent>
              </Card>

              <Card className="border-violet-200/80 dark:border-violet-900/70">
                <CardContent className="flex items-start justify-between gap-3 p-4">
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Cash on hand</p>
                    <p className="mt-2 text-xl font-semibold">{formatReportingMoney(data.financeOverview.kpis.cashOnHand)}</p>
                    <p className="mt-1 text-xs text-slate-500">As at the selected period end</p>
                  </div>
                  <WalletCards className="h-5 w-5 text-violet-600" />
                </CardContent>
              </Card>
            </div>

            <div className="grid gap-4 lg:grid-cols-2">
              <BaseLineChart
                data={data.financeOverview.monthly}
                xAxisKey="name"
                lines={[
                  { dataKey: 'revenue', name: 'Revenue', color: CHART_COLORS.success[0] },
                  { dataKey: 'expenses', name: 'Expenses', color: CHART_COLORS.warning[0] },
                ]}
                title="Revenue and Expenses"
                description="Posted general-ledger movement for the selected period."
                height={300}
                compact
                formatValue={(value) => formatReportingMoney(Number(value))}
                className="border-slate-200/80 shadow-lg shadow-slate-200/50 dark:border-slate-800/80 dark:shadow-none"
              />
              <BaseBarChart
                data={data.financeOverview.expenseChart}
                xAxisKey="name"
                bars={[{ dataKey: 'value', name: 'Expense', color: CHART_COLORS.danger[0] }]}
                title="Top Expense Accounts"
                description="Largest posted expense balances in the selected period."
                height={300}
                compact
                orientation="horizontal"
                formatValue={(value) => formatReportingMoney(Number(value))}
                className="border-slate-200/80 shadow-lg shadow-slate-200/50 dark:border-slate-800/80 dark:shadow-none"
              />
            </div>
          </section>
        )}

        {management && (
          <section className="space-y-3" aria-labelledby="procurement-inventory-management-title">
            <div className="flex flex-wrap items-end justify-between gap-2">
              <div>
                <h2 id="procurement-inventory-management-title" className="text-lg font-semibold text-slate-950 dark:text-slate-50">
                  Procurement and inventory management
                </h2>
                <p className="text-xs text-slate-500 dark:text-slate-400">
                  {format(new Date(management.rangeStartDate), 'dd MMM yyyy')} – {format(new Date(management.rangeEndDate), 'dd MMM yyyy')}
                </p>
              </div>
              <Badge variant="outline" className="font-normal">
                Stock as at {formatRelativeTime(management.inventoryAsOfUtc)}
              </Badge>
            </div>

            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
              <Card className="border-amber-200/80 dark:border-amber-900/70">
                <CardContent className="flex items-start justify-between gap-3 p-4">
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Period spend</p>
                    <p className="mt-2 text-xl font-semibold">{formatMoneyPoints(management.spendByCurrency)}</p>
                    <p className="mt-1 text-xs text-slate-500">{management.spendByCurrency.reduce((sum, item) => sum + item.count, 0)} purchase orders</p>
                  </div>
                  <ShoppingCart className="h-5 w-5 text-amber-600" />
                </CardContent>
              </Card>

              <Card className="border-sky-200/80 dark:border-sky-900/70">
                <CardContent className="flex items-start justify-between gap-3 p-4">
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Open purchase orders</p>
                    <p className="mt-2 text-xl font-semibold">{formatNumber(management.openPurchaseOrders.count)}</p>
                    <p className="mt-1 text-xs text-slate-500">
                      {management.openPurchaseOrders.overdueCount} overdue · {formatMoneyPoints(management.openPurchaseOrders.remainingValueByCurrency)} remaining
                    </p>
                  </div>
                  <FileText className="h-5 w-5 text-sky-600" />
                </CardContent>
              </Card>

              <Card className="border-violet-200/80 dark:border-violet-900/70">
                <CardContent className="flex items-start justify-between gap-3 p-4">
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Contracts</p>
                    <p className="mt-2 text-xl font-semibold">{management.contracts.averageUtilizationPercent.toFixed(1)}%</p>
                    <p className="mt-1 text-xs text-slate-500">
                      {management.contracts.activeCount} active · {management.contracts.expiringWithin90DaysCount} expiring
                    </p>
                  </div>
                  <Gauge className="h-5 w-5 text-violet-600" />
                </CardContent>
              </Card>

              <Card className="border-cyan-200/80 dark:border-cyan-900/70">
                <CardContent className="flex items-start justify-between gap-3 p-4">
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Stock value</p>
                    <p className="mt-2 text-xl font-semibold">{formatReportingMoney(management.inventory.stockValue)}</p>
                    <p className="mt-1 text-xs text-slate-500">
                      {management.inventory.itemLocationCount} item locations · {management.inventory.stockoutCount} stockouts
                    </p>
                  </div>
                  <Boxes className="h-5 w-5 text-cyan-600" />
                </CardContent>
              </Card>

              <Card className="border-indigo-200/80 dark:border-indigo-900/70">
                <CardContent className="flex items-start justify-between gap-3 p-4">
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Procurement cycle</p>
                    <p className="mt-2 text-xl font-semibold">
                      {management.cycleTime.averageRequisitionToPurchaseOrderDays?.toFixed(1) ?? '—'} days
                    </p>
                    <p className="mt-1 text-xs text-slate-500">
                      PR to PO · {management.cycleTime.averagePurchaseOrderToReceiptDays?.toFixed(1) ?? '—'} days PO to receipt
                    </p>
                  </div>
                  <Clock3 className="h-5 w-5 text-indigo-600" />
                </CardContent>
              </Card>

              <Card className="border-emerald-200/80 dark:border-emerald-900/70">
                <CardContent className="flex items-start justify-between gap-3 p-4">
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Service level</p>
                    <p className="mt-2 text-xl font-semibold">{management.serviceLevel.onTimeDeliveryPercent?.toFixed(1) ?? '—'}%</p>
                    <p className="mt-1 text-xs text-slate-500">
                      On-time delivery · {management.serviceLevel.acceptedFillRatePercent?.toFixed(1) ?? '—'}% accepted fill
                    </p>
                  </div>
                  <TrendingUp className="h-5 w-5 text-emerald-600" />
                </CardContent>
              </Card>

              <Card className="border-rose-200/80 dark:border-rose-900/70">
                <CardContent className="flex items-start justify-between gap-3 p-4">
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Supplier risk</p>
                    <p className="mt-2 text-xl font-semibold">{management.supplierRisk.highOrCriticalSupplierCount}</p>
                    <p className="mt-1 text-xs text-slate-500">
                      High/critical · {management.supplierRisk.awardBlockedSupplierCount} award blocked
                    </p>
                  </div>
                  <ShieldAlert className="h-5 w-5 text-rose-600" />
                </CardContent>
              </Card>
            </div>

            <div className="grid gap-4 lg:grid-cols-2 2xl:grid-cols-4">
              <BaseBarChart
                data={managementSpendByCategory}
                xAxisKey="label"
                bars={[{ dataKey: 'amount', name: 'Spend', color: CHART_COLORS.warning[0] }]}
                title="Spend by Category"
                description="Purchase-order spend in the selected period."
                height={280}
                compact
                formatValue={(value) => formatNumber(Number(value))}
              />
              <BaseBarChart
                data={managementSpendByDepartment}
                xAxisKey="label"
                bars={[{ dataKey: 'amount', name: 'Spend', color: CHART_COLORS.primary[0] }]}
                title="Spend by Department"
                description="Source-requisition department exposure."
                height={280}
                compact
                formatValue={(value) => formatNumber(Number(value))}
              />
              <BaseBarChart
                data={managementInventoryByCategory}
                xAxisKey="label"
                bars={[{ dataKey: 'value', name: 'Stock Value', color: CHART_COLORS.info[0] }]}
                title="Stock Value by Category"
                description="Current authorized inventory balance."
                height={280}
                compact
                formatValue={(value) => formatNumber(Number(value))}
              />
              <BasePieChart
                data={managementSupplierRisk}
                dataKey="value"
                nameKey="name"
                title="Supplier Risk Profile"
                description={`${management.supplierRisk.openAlertCount} open/escalated alerts.`}
                height={280}
                compact
                innerRadius={64}
                showLabels={false}
                colors={[CHART_COLORS.danger[0], CHART_COLORS.warning[0], CHART_COLORS.info[0], CHART_COLORS.success[0]]}
              />
            </div>

            {management.contracts.expiringContracts.length > 0 && (
              <Card>
                <CardHeader className="px-4 pb-2 pt-4">
                  <CardTitle className="text-sm">Contracts expiring within 90 days</CardTitle>
                  <CardDescription className="text-xs">Utilization is calculated from linked purchase-order commitments through the selected period end.</CardDescription>
                </CardHeader>
                <CardContent className="grid gap-2 px-4 pb-4 md:grid-cols-2 xl:grid-cols-3">
                  {management.contracts.expiringContracts.map((contract) => (
                    <Link
                      key={contract.contractId}
                      href={`/procurement/contracts/${contract.contractId}`}
                      className="rounded-lg border p-3 transition-colors hover:bg-slate-50 dark:hover:bg-slate-900"
                    >
                      <div className="flex items-center justify-between gap-3">
                        <span className="truncate text-sm font-medium">{contract.contractNumber}</span>
                        <Badge variant="outline" className="shrink-0">{contract.daysToExpiry}d</Badge>
                      </div>
                      <p className="mt-1 truncate text-xs text-slate-500">{contract.supplierName || contract.contractTitle}</p>
                      <p className="mt-2 text-xs">{contract.utilizationPercent.toFixed(1)}% utilized</p>
                    </Link>
                  ))}
                </CardContent>
              </Card>
            )}
          </section>
        )}

        <div className="grid gap-4 lg:grid-cols-2 2xl:grid-cols-3">
          <BaseBarChart
            data={pipelineStageData}
            xAxisKey="stage"
            bars={[
              { dataKey: 'opportunities', name: 'Opportunities', color: CHART_COLORS.success[0] },
              { dataKey: 'quotes', name: 'Quotes', color: CHART_COLORS.primary[0] },
            ]}
            title="CRM Pipeline Activity"
            description="Open opportunities and related quotes by configured CRM stage."
            height={300}
            compact
            formatValue={(value) => formatNumber(Number(value))}
            error={unavailableModules.find((module) => module.module === 'CRM')?.error}
            className="border-slate-200/80 shadow-lg shadow-slate-200/50 dark:border-slate-800/80 dark:shadow-none"
          />

          <BaseFunnelChart
            data={crmFunnelData}
            dataKey="count"
            nameKey="stage"
            title="CRM Conversion Funnel"
            description="Lead-to-delivery conversion volume."
            height={300}
            compact
            showLabels={false}
            formatValue={(value) => formatNumber(Number(value))}
            error={unavailableModules.find((module) => module.module === 'CRM')?.error}
            className="border-slate-200/80 shadow-lg shadow-slate-200/50 dark:border-slate-800/80 dark:shadow-none"
          />

          <BasePieChart
            data={crmHealthData}
            dataKey="value"
            nameKey="name"
            title="Account Risk Mix"
            description="Live active-account risk bands from the business-partner register."
            height={300}
            compact
            innerRadius={68}
            showLabels={false}
            colors={[CHART_COLORS.success[0], CHART_COLORS.warning[0], CHART_COLORS.danger[0], CHART_COLORS.info[0]]}
            error={unavailableModules.find((module) => module.module === 'CRM')?.error}
            className="border-slate-200/80 shadow-lg shadow-slate-200/50 dark:border-slate-800/80 dark:shadow-none"
          />

          <BaseBarChart
            data={projectPressureData}
            xAxisKey="name"
            bars={[{ dataKey: 'value', name: 'Open Items', color: CHART_COLORS.danger[0] }]}
            title="Project Delivery Pressure"
            description="Tasks, milestones, risks, and issues."
            height={300}
            compact
            orientation="horizontal"
            formatValue={(value) => formatNumber(Number(value))}
            error={unavailableModules.find((module) => module.module === 'Projects')?.error}
            className="border-slate-200/80 shadow-lg shadow-slate-200/50 dark:border-slate-800/80 dark:shadow-none"
          />

          <BaseBarChart
            data={inventoryQueueData}
            xAxisKey="label"
            bars={[{ dataKey: 'count', name: 'Queue Count', color: CHART_COLORS.info[0] }]}
            title="Inventory Queue Profile"
            description="Approvals versus issue workload."
            height={300}
            compact
            orientation="horizontal"
            formatValue={(value) => formatNumber(Number(value))}
            error={unavailableModules.find((module) => module.module === 'Inventory Queues')?.error}
            className="border-slate-200/80 shadow-lg shadow-slate-200/50 dark:border-slate-800/80 dark:shadow-none"
          />

          <BaseLineChart
            data={maintenanceTrendData}
            xAxisKey="period"
            lines={[
              { dataKey: 'created', name: 'Created', color: CHART_COLORS.danger[0] },
              { dataKey: 'completed', name: 'Completed', color: CHART_COLORS.success[0] },
            ]}
            title="Maintenance Trend"
            description="Created versus completed work orders."
            height={300}
            compact
            formatValue={(value) => formatNumber(Number(value))}
            error={unavailableModules.find((module) => module.module === 'Maintenance Trends')?.error}
            className="border-slate-200/80 shadow-lg shadow-slate-200/50 dark:border-slate-800/80 dark:shadow-none"
          />

          <BasePieChart
            data={maintenanceStatusData}
            dataKey="value"
            nameKey="name"
            title="Maintenance Workload Mix"
            description="Current backlog composition."
            height={300}
            compact
            innerRadius={68}
            showLabels={false}
            colors={[CHART_COLORS.danger[0], CHART_COLORS.warning[0], CHART_COLORS.success[0]]}
            error={unavailableModules.find((module) => ['Maintenance Overview', 'Maintenance Metrics'].includes(module.module))?.error}
            className="border-slate-200/80 shadow-lg shadow-slate-200/50 dark:border-slate-800/80 dark:shadow-none"
          />

          <BasePieChart
            data={tenderStatusData}
            dataKey="value"
            nameKey="name"
            title="Tender Status Mix"
            description="Live tenders by workflow status."
            height={300}
            compact
            innerRadius={68}
            showLabels={false}
            colors={[CHART_COLORS.info[0], CHART_COLORS.warning[0], CHART_COLORS.success[0], CHART_COLORS.primary[0], CHART_COLORS.danger[0]]}
            error={unavailableModules.find((module) => module.module === 'Procurement Queues')?.error}
            className="border-slate-200/80 shadow-lg shadow-slate-200/50 dark:border-slate-800/80 dark:shadow-none"
          />

          <BaseBarChart
            data={queueLoadData}
            xAxisKey="module"
            bars={[{ dataKey: 'items', name: 'Open Items', color: CHART_COLORS.neutral[1] }]}
            title="Operational Queue Load"
            description="Where work is building across modules."
            height={300}
            compact
            orientation="horizontal"
            formatValue={(value) => formatNumber(Number(value))}
            className="border-slate-200/80 shadow-lg shadow-slate-200/50 dark:border-slate-800/80 dark:shadow-none"
          />
        </div>
      </div>
    </DashboardLayout>
  );
}
