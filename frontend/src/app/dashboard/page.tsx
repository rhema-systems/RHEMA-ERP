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
  Boxes,
  Clock3,
  FileText,
  FolderKanban,
  Gauge,
  Loader2,
  MapPin,
  ShieldAlert,
  type LucideIcon,
  RefreshCw,
  ShoppingCart,
  TrendingUp,
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
import { useAuth } from '../../hooks/use-auth';
import { getExternalPortalPath, isExternalPortalUser } from '../../lib/auth-routing';
import { cn } from '../../lib/utils';
import { authService } from '../../services/auth';
import { dashboardService } from '../../services/dashboard';
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

const formatCurrency = (value: number, currency = 'USD') => {
  try {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency,
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(value);
  } catch {
    return `${currency} ${formatNumber(value)}`;
  }
};

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

const getDaysUntil = (value?: string, referenceDate = new Date()) => {
  if (!value) return null;

  const target = new Date(value).getTime();
  const reference = referenceDate.getTime();
  if (Number.isNaN(target)) return null;

  return Math.ceil((target - reference) / (1000 * 60 * 60 * 24));
};

const sumBy = <T,>(items: T[], selector: (item: T) => number) =>
  items.reduce((total, item) => total + selector(item), 0);

const formatMoneyPoints = (points: Array<{ amount: number; currency: string }>) =>
  points.length === 0
    ? 'No value'
    : points.slice(0, 2).map((point) => formatCurrency(point.amount, point.currency)).join(' · ');

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
  const shouldRouteToExternalPortal = isExternalPortalUser(effectiveUser);
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
      router.push(getExternalPortalPath());
    }
  }, [router, shouldRouteToExternalPortal]);

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
        <div className="flex min-h-[60vh] items-center justify-center">
          <div className="space-y-4 text-center">
            <Loader2 className="mx-auto h-8 w-8 animate-spin" />
            <p className="text-muted-foreground">Loading enterprise dashboard...</p>
          </div>
        </div>
      </DashboardLayout>
    );
  }

  const unavailableModules = data.moduleStatus.filter((status) => !status.available);

  const openPurchaseOrderValue = sumBy(data.openPurchaseOrders, (order) => order.totalAmount);
  const pendingPurchaseRequisitionValue = sumBy(data.pendingPurchaseRequisitions, (requisition) => requisition.totalAmount);
  const pendingInventoryApprovalValue = sumBy(data.pendingInventoryApprovals, (requisition) => requisition.totalValue);
  const pendingInventoryIssueValue = sumBy(data.pendingInventoryIssues, (requisition) => requisition.totalValue);
  const pendingInventoryValue = pendingInventoryApprovalValue + pendingInventoryIssueValue;

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

  const openTenders = data.tenders.filter((tender) => !['closed', 'cancelled', 'awarded', 'completed'].includes(tender.status.toLowerCase()));
  const rangeEndReference = new Date(data.rangeEndDate);
  const closingSoonTenders = openTenders.filter((tender) => {
    const daysUntil = getDaysUntil(tender.submissionDeadline, rangeEndReference);
    return daysUntil !== null && daysUntil >= 0 && daysUntil <= 14;
  });
  const tenderEstimatedValue = sumBy(openTenders, (tender) => tender.estimatedValue ?? 0);

  const summaryCards: SummaryCardDefinition[] = [
    {
      title: 'CRM Pipeline',
      value: formatCurrency(data.crmOverview?.weightedPipelineValue ?? 0),
      meta: `${data.crmOverview?.openOpportunityCount ?? 0} open opportunities`,
      href: '/crm',
      accentClassName:
        'border-emerald-200 bg-[linear-gradient(135deg,rgba(255,255,255,0.96),rgba(236,253,245,0.95))] dark:border-emerald-900/70 dark:bg-[linear-gradient(135deg,rgba(2,6,23,0.96),rgba(6,78,59,0.25))]',
      iconClassName: 'bg-emerald-500/15 text-emerald-600 dark:text-emerald-300',
      icon: TrendingUp,
    },
    {
      title: 'Projects',
      value: formatNumber(data.projectDashboard?.activeProjects ?? 0),
      meta: `${data.projectDashboard?.overdueMilestones ?? 0} overdue milestones`,
      href: '/development/projects',
      accentClassName:
        'border-violet-200 bg-[linear-gradient(135deg,rgba(255,255,255,0.96),rgba(245,243,255,0.95))] dark:border-violet-900/70 dark:bg-[linear-gradient(135deg,rgba(2,6,23,0.96),rgba(76,29,149,0.22))]',
      iconClassName: 'bg-violet-500/15 text-violet-600 dark:text-violet-300',
      icon: FolderKanban,
    },
    {
      title: 'Procurement',
      value: formatCurrency(openPurchaseOrderValue),
      meta: `${data.pendingPurchaseRequisitions.length} requisitions waiting`,
      href: '/procurement/purchase-orders',
      accentClassName:
        'border-amber-200 bg-[linear-gradient(135deg,rgba(255,255,255,0.96),rgba(255,251,235,0.95))] dark:border-amber-900/70 dark:bg-[linear-gradient(135deg,rgba(2,6,23,0.96),rgba(120,53,15,0.24))]',
      iconClassName: 'bg-amber-500/15 text-amber-600 dark:text-amber-300',
      icon: ShoppingCart,
    },
    {
      title: 'Inventory',
      value: formatCurrency(pendingInventoryValue),
      meta: `${data.pendingInventoryIssues.length} issue queues active`,
      href: '/inventory/requisitions',
      accentClassName:
        'border-cyan-200 bg-[linear-gradient(135deg,rgba(255,255,255,0.96),rgba(236,254,255,0.95))] dark:border-cyan-900/70 dark:bg-[linear-gradient(135deg,rgba(2,6,23,0.96),rgba(21,94,117,0.24))]',
      iconClassName: 'bg-cyan-500/15 text-cyan-600 dark:text-cyan-300',
      icon: Boxes,
    },
    {
      title: 'Maintenance',
      value: formatNumber(maintenanceActiveWorkOrders),
      meta: `${maintenanceOverdueWorkOrders} overdue work orders`,
      href: '/maintenance',
      accentClassName:
        'border-rose-200 bg-[linear-gradient(135deg,rgba(255,255,255,0.96),rgba(255,241,242,0.95))] dark:border-rose-900/70 dark:bg-[linear-gradient(135deg,rgba(2,6,23,0.96),rgba(127,29,29,0.24))]',
      iconClassName: 'bg-rose-500/15 text-rose-600 dark:text-rose-300',
      icon: Wrench,
    },
    {
      title: 'Tenders',
      value: formatCurrency(tenderEstimatedValue),
      meta: `${closingSoonTenders.length} closing within 14 days`,
      href: '/procurement/tenders',
      accentClassName:
        'border-sky-200 bg-[linear-gradient(135deg,rgba(255,255,255,0.96),rgba(240,249,255,0.95))] dark:border-sky-900/70 dark:bg-[linear-gradient(135deg,rgba(2,6,23,0.96),rgba(12,74,110,0.24))]',
      iconClassName: 'bg-sky-500/15 text-sky-600 dark:text-sky-300',
      icon: FileText,
    },
  ];

  const pipelineStageData = (data.crmReporting?.pipelineByStage ?? []).map((stage) => ({
    stage: stage.stage,
    totalValue: stage.totalValue,
    weightedValue: stage.weightedValue,
  }));

  const crmFunnelData = (data.crmConversions?.funnel ?? []).map((stage) => ({
    stage: stage.stage,
    count: stage.entityCount,
    related: stage.relatedOpportunityCount,
    value: stage.totalValue,
    conversionRate: stage.conversionRate,
  }));

  const crmHealthData = Object.values(
    (data.crmReporting?.accountHealth ?? []).reduce<Record<string, { name: string; value: number }>>((accumulator, account) => {
      const category = account.healthCategory || 'Unknown';
      accumulator[category] = accumulator[category] ?? { name: category, value: 0 };
      accumulator[category].value += 1;
      return accumulator;
    }, {}),
  );

  const projectBudgetData = [
    { label: 'Estimated', amount: data.projectDashboard?.totalEstimatedBudget ?? 0 },
    { label: 'Approved', amount: data.projectDashboard?.totalApprovedBudget ?? 0 },
    { label: 'Actual', amount: data.projectDashboard?.totalActualCost ?? 0 },
  ];

  const projectPressureData = [
    { name: 'Overdue Tasks', value: data.projectDashboard?.overdueTasks ?? 0 },
    { name: 'Overdue Milestones', value: data.projectDashboard?.overdueMilestones ?? 0 },
    { name: 'Open Risks', value: data.projectDashboard?.openRisks ?? 0 },
    { name: 'Open Issues', value: data.projectDashboard?.openIssues ?? 0 },
  ].filter((item) => item.value > 0);

  const procurementExposureData = [
    { label: 'Requisitions', amount: pendingPurchaseRequisitionValue },
    { label: 'Purchase Orders', amount: openPurchaseOrderValue },
    { label: 'Tenders', amount: tenderEstimatedValue },
    { label: 'Inventory', amount: pendingInventoryValue },
  ];

  const inventoryQueueData = [
    { label: 'Approvals', count: data.pendingInventoryApprovals.length, value: pendingInventoryApprovalValue },
    { label: 'Issues', count: data.pendingInventoryIssues.length, value: pendingInventoryIssueValue },
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

  const tenderStatusData = Object.values(
    openTenders.reduce<Record<string, { name: string; value: number }>>((accumulator, tender) => {
      const status = tender.status || 'Unknown';
      accumulator[status] = accumulator[status] ?? { name: status, value: 0 };
      accumulator[status].value += 1;
      return accumulator;
    }, {}),
  );

  const queueLoadData = [
    { module: 'CRM', items: (data.crmOverview?.leadsNeedingFollowUpCount ?? 0) + (data.crmOverview?.atRiskAccountCount ?? 0) },
    {
      module: 'Projects',
      items: (data.projectDashboard?.overdueTasks ?? 0) + (data.projectDashboard?.overdueMilestones ?? 0) + (data.projectDashboard?.openRisks ?? 0),
    },
    { module: 'Procurement', items: data.pendingPurchaseRequisitions.length + data.openPurchaseOrders.length },
    { module: 'Inventory', items: data.pendingInventoryApprovals.length + data.pendingInventoryIssues.length },
    { module: 'Maintenance', items: maintenanceActiveWorkOrders + maintenanceOverdueWorkOrders },
    { module: 'Tenders', items: openTenders.length + closingSoonTenders.length },
  ];

  const criticalAlertCount =
    (data.crmOverview?.leadsNeedingFollowUpCount ?? 0) +
    (data.projectDashboard?.overdueMilestones ?? 0) +
    maintenanceOverdueWorkOrders +
    closingSoonTenders.length;

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
                  {closingSoonTenders.length} tenders closing soon
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
                    <p className="mt-2 text-xl font-semibold">{formatNumber(management.inventory.stockValue)}</p>
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
              { dataKey: 'totalValue', name: 'Total Value', color: CHART_COLORS.success[0] },
              { dataKey: 'weightedValue', name: 'Weighted Value', color: CHART_COLORS.primary[0] },
            ]}
            title="CRM Pipeline by Stage"
            description="Stage exposure and weighted pipeline."
            height={300}
            compact
            formatValue={(value) => formatCurrency(Number(value))}
            error={unavailableModules.find((module) => module.module === 'CRM Reporting')?.error}
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
            error={unavailableModules.find((module) => module.module === 'CRM Conversions')?.error}
            className="border-slate-200/80 shadow-lg shadow-slate-200/50 dark:border-slate-800/80 dark:shadow-none"
          />

          <BasePieChart
            data={crmHealthData}
            dataKey="value"
            nameKey="name"
            title="Account Health Mix"
            description="Live account quality distribution."
            height={300}
            compact
            innerRadius={68}
            showLabels={false}
            colors={[CHART_COLORS.success[0], CHART_COLORS.warning[0], CHART_COLORS.danger[0], CHART_COLORS.info[0]]}
            error={unavailableModules.find((module) => module.module === 'CRM Reporting')?.error}
            className="border-slate-200/80 shadow-lg shadow-slate-200/50 dark:border-slate-800/80 dark:shadow-none"
          />

          <BaseBarChart
            data={projectBudgetData}
            xAxisKey="label"
            bars={[{ dataKey: 'amount', name: 'Amount', color: CHART_COLORS.primary[1] }]}
            title="Project Budget Position"
            description="Estimated, approved, and actual spend."
            height={300}
            compact
            formatValue={(value) => formatCurrency(Number(value))}
            error={unavailableModules.find((module) => module.module === 'Projects')?.error}
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
            data={procurementExposureData}
            xAxisKey="label"
            bars={[{ dataKey: 'amount', name: 'Value', color: CHART_COLORS.warning[0] }]}
            title="Procurement Exposure"
            description="Value across requisitions, POs, tenders, and inventory."
            height={300}
            compact
            formatValue={(value) => formatCurrency(Number(value))}
            error={unavailableModules.find((module) => ['Purchase Orders', 'Purchase Requisitions', 'Tenders'].includes(module.module))?.error}
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
            error={unavailableModules.find((module) => ['Inventory Approval Queue', 'Inventory Issue Queue'].includes(module.module))?.error}
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
            error={unavailableModules.find((module) => module.module === 'Tenders')?.error}
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
