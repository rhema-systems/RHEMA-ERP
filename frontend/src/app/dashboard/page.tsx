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
  CircleCheckBig,
  Clock3,
  FileText,
  FolderKanban,
  Gauge,
  MapPin,
  ListTodo,
  Server,
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
import { useInterfaceStyle } from '../../contexts/InterfaceStyleContext';
import { systemHealthService } from '../../services/systemHealthService';

interface SummaryCardDefinition {
  title: string;
  value: string;
  meta: string;
  href: string;
  accentClassName: string;
  iconClassName: string;
  icon: LucideIcon;
}

const buildQueryHref = (path: string, parameters: Record<string, string | undefined>) => {
  const query = new URLSearchParams();
  Object.entries(parameters).forEach(([key, value]) => {
    if (value) query.set(key, value);
  });
  const suffix = query.toString();
  return suffix ? `${path}?${suffix}` : path;
};

const getCrmFunnelHref = (stage: string) => {
  const normalized = stage.trim().toLowerCase();
  if (normalized === 'enquiry' || normalized === 'lead' || normalized === 'leads') {
    return '/crm/leads?status=New';
  }
  if (normalized === 'qualified') {
    return '/crm/leads?status=Qualified';
  }
  return buildQueryHref('/crm/opportunities', { stage });
};

const operationalModuleHref: Record<string, string> = {
  CRM: '/crm/leads?followUpOnly=true',
  Projects: '/development/projects',
  Procurement: '/procurement/purchase-orders',
  Inventory: '/inventory/requisitions',
  Maintenance: '/maintenance/work-orders',
  Tenders: '/procurement/tenders',
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
  const { interfaceStyle } = useInterfaceStyle();
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

  const { data: systemHealth, error: systemHealthError } = useQuery({
    queryKey: ['enterprise-dashboard', 'system-health'],
    queryFn: () => systemHealthService.getReadiness(),
    enabled: !shouldRouteToExternalPortal,
    staleTime: 30_000,
    refetchInterval: 60_000,
    retry: 1,
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

  const procurementQueuesAvailable = data.moduleStatus.some(
    (status) => status.module === 'Procurement Queues' && status.available,
  );
  const inventoryQueuesAvailable = data.moduleStatus.some(
    (status) => status.module === 'Inventory Queues' && status.available,
  );

  const queueLoadData = [
    ...(data.crm
      ? [{ module: 'CRM', items: data.crm.leadsNeedingFollowUpCount + data.crm.atRiskAccountCount }]
      : []),
    ...(data.projectDashboard
      ? [{
        module: 'Projects',
        items: data.projectDashboard.overdueTasks + data.projectDashboard.overdueMilestones + data.projectDashboard.openRisks,
      }]
      : []),
    ...(procurementQueuesAvailable
      ? [
        { module: 'Procurement', items: queues.pendingPurchaseRequisitionCount + queues.openPurchaseOrderCount },
        { module: 'Tenders', items: queues.openTenderCount + queues.tendersClosingWithin14DaysCount },
      ]
      : []),
    ...(inventoryQueuesAvailable
      ? [{ module: 'Inventory', items: queues.pendingInventoryApprovalCount + queues.pendingInventoryIssueCount }]
      : []),
    ...(data.maintenanceOverview || data.maintenanceMetrics
      ? [{ module: 'Maintenance', items: maintenanceActiveWorkOrders + maintenanceOverdueWorkOrders }]
      : []),
  ];

  const criticalAlertCount =
    (data.crm?.leadsNeedingFollowUpCount ?? 0) +
    (data.projectDashboard?.overdueMilestones ?? 0) +
    maintenanceOverdueWorkOrders +
    queues.tendersClosingWithin14DaysCount;

  const operationalAlerts = [
    { label: 'Leads needing follow-up', count: data.crm?.leadsNeedingFollowUpCount ?? 0, href: '/crm/leads?followUpOnly=true' },
    { label: 'Overdue project milestones', count: data.projectDashboard?.overdueMilestones ?? 0, href: '/development/projects' },
    { label: 'Overdue maintenance work orders', count: maintenanceOverdueWorkOrders, href: '/maintenance/work-orders' },
    ...(procurementQueuesAvailable
      ? [{ label: 'Tenders closing within 14 days', count: queues.tendersClosingWithin14DaysCount, href: '/procurement/tenders' }]
      : []),
  ].filter((item) => item.count > 0);

  const operationalWorkQueues = [
    ...(procurementQueuesAvailable
      ? [
        { label: 'Purchase requisitions', count: queues.pendingPurchaseRequisitionCount, href: '/procurement/purchase-requisitions' },
        { label: 'Open purchase orders', count: queues.openPurchaseOrderCount, href: '/procurement/purchase-orders' },
      ]
      : []),
    ...(inventoryQueuesAvailable
      ? [
        { label: 'Inventory approvals', count: queues.pendingInventoryApprovalCount, href: '/inventory/requisitions' },
        { label: 'Inventory issues', count: queues.pendingInventoryIssueCount, href: '/inventory/requisitions' },
      ]
      : []),
  ];

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
  const detailedLedgerHref = buildQueryHref('/finance/reports/detailed-ledger', {
    startDate: rangeQuery?.startDate,
    endDate: rangeQuery?.endDate,
  });

  return (
    <DashboardLayout>
      <div className={cn('space-y-5', interfaceStyle === 'immersive' && 'dashboard-immersive')} data-dashboard-style={interfaceStyle}>
        <section className={cn(
          'border border-slate-200/80 p-5 dark:border-slate-800/80',
          interfaceStyle === 'immersive'
            ? 'rounded-[28px] bg-[radial-gradient(circle_at_top_left,_rgba(59,130,246,0.16),_transparent_30%),radial-gradient(circle_at_top_right,_rgba(16,185,129,0.14),_transparent_26%),linear-gradient(135deg,rgba(255,255,255,0.97),rgba(239,246,255,0.96))] shadow-[0_24px_60px_-24px_rgba(15,23,42,0.25)] dark:bg-[radial-gradient(circle_at_top_left,_rgba(59,130,246,0.2),_transparent_30%),radial-gradient(circle_at_top_right,_rgba(16,185,129,0.16),_transparent_26%),linear-gradient(135deg,rgba(2,6,23,0.96),rgba(15,23,42,0.98))]'
            : 'rounded-xl bg-white shadow-sm dark:bg-[#181818]',
        )}>
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
                <p className="text-sm font-medium text-slate-500 dark:text-slate-400">Good {new Date().getHours() < 12 ? 'morning' : new Date().getHours() < 18 ? 'afternoon' : 'evening'},</p>
                <h1 className={cn('font-bold tracking-tight text-slate-950 dark:text-slate-50', interfaceStyle === 'immersive' ? 'text-[2rem]' : 'text-[1.75rem]')}>Welcome back, {displayName}!</h1>
                <p className="mt-1 max-w-3xl text-sm leading-5 text-slate-600 dark:text-slate-300">
                  Here&apos;s what&apos;s happening across your organization for the selected reporting period.
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

        <div className={cn('grid gap-3 md:grid-cols-2', interfaceStyle === 'immersive' ? 'xl:grid-cols-4 2xl:grid-cols-6' : 'xl:grid-cols-4')}>
          {summaryCards.map((card) => {
            const Icon = card.icon;

            return (
              <Link
                key={card.title}
                href={card.href}
                aria-label={`Open ${card.title} details`}
                className="group block rounded-xl focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
              >
              <Card className={cn('h-full overflow-hidden border shadow-[0_16px_40px_-28px_rgba(15,23,42,0.4)] transition-transform group-hover:-translate-y-0.5 group-hover:shadow-lg dark:shadow-none', card.accentClassName)}>
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
                  <div className="mt-3 inline-flex items-center text-xs font-medium text-primary">
                    View details
                    <ArrowRight className="ml-1.5 h-3.5 w-3.5 transition-transform group-hover:translate-x-0.5" />
                  </div>
                </CardContent>
              </Card>
              </Link>
            );
          })}
        </div>

        <section className="grid gap-4 lg:grid-cols-3" aria-label="Dashboard attention and system status">
          <Card className="border-slate-200/80 shadow-sm dark:border-slate-800/80">
            <CardHeader className="px-4 pb-2 pt-4">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <CardTitle className="text-sm">System health</CardTitle>
                  <CardDescription className="text-xs">Live API readiness checks</CardDescription>
                </div>
                <Server className="h-4 w-4 text-blue-600 dark:text-blue-300" />
              </div>
            </CardHeader>
            <CardContent className="space-y-2 px-4 pb-4">
              {systemHealth?.checks.map((check) => {
                const healthy = check.status.toLowerCase() === 'healthy';
                return (
                  <div key={check.name} className="flex items-center justify-between gap-3 rounded-lg border px-3 py-2 text-xs">
                    <span className="truncate font-medium capitalize">{check.name.replaceAll('-', ' ')}</span>
                    <Badge variant="outline" className={cn('shrink-0', healthy ? 'border-emerald-200 text-emerald-700 dark:border-emerald-800 dark:text-emerald-300' : 'border-rose-200 text-rose-700 dark:border-rose-800 dark:text-rose-300')}>
                      {check.status}
                    </Badge>
                  </div>
                );
              })}
              {!systemHealth && !systemHealthError && <p className="py-3 text-center text-xs text-slate-500">Checking service readiness…</p>}
              {systemHealthError && <p className="py-3 text-center text-xs text-rose-600">Readiness checks are currently unavailable.</p>}
            </CardContent>
          </Card>

          <Card className="border-slate-200/80 shadow-sm dark:border-slate-800/80">
            <CardHeader className="px-4 pb-2 pt-4">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <CardTitle className="text-sm">Alerts and attention</CardTitle>
                  <CardDescription className="text-xs">Operational records requiring review</CardDescription>
                </div>
                <AlertTriangle className="h-4 w-4 text-amber-600" />
              </div>
            </CardHeader>
            <CardContent className="space-y-2 px-4 pb-4">
              {operationalAlerts.length === 0 ? (
                <div className="flex items-center gap-2 rounded-lg border border-emerald-200 bg-emerald-50/60 px-3 py-3 text-xs text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950/20 dark:text-emerald-300">
                  <CircleCheckBig className="h-4 w-4" />
                  No active operational alerts
                </div>
              ) : operationalAlerts.map((alert) => (
                <Link key={alert.label} href={alert.href} className="group flex items-center justify-between gap-3 rounded-lg border px-3 py-2 text-xs transition-colors hover:bg-slate-50 dark:hover:bg-slate-900">
                  <span className="font-medium">{alert.label}</span>
                  <span className="flex items-center gap-1.5 font-semibold text-amber-700 dark:text-amber-300">
                    {formatNumber(alert.count)}
                    <ArrowRight className="h-3.5 w-3.5 transition-transform group-hover:translate-x-0.5" />
                  </span>
                </Link>
              ))}
            </CardContent>
          </Card>

          <Card className="border-slate-200/80 shadow-sm dark:border-slate-800/80">
            <CardHeader className="px-4 pb-2 pt-4">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <CardTitle className="text-sm">Operational work queues</CardTitle>
                  <CardDescription className="text-xs">Live workload across permitted modules</CardDescription>
                </div>
                <ListTodo className="h-4 w-4 text-violet-600 dark:text-violet-300" />
              </div>
            </CardHeader>
            <CardContent className="space-y-2 px-4 pb-4">
              {operationalWorkQueues.length === 0 ? (
                <p className="rounded-lg border px-3 py-3 text-xs text-slate-500 dark:text-slate-400">
                  No permitted operational queues
                </p>
              ) : operationalWorkQueues.map((queue) => (
                <Link key={queue.label} href={queue.href} className="group flex items-center justify-between gap-3 rounded-lg border px-3 py-2 text-xs transition-colors hover:bg-slate-50 dark:hover:bg-slate-900">
                  <span className="font-medium">{queue.label}</span>
                  <span className="flex items-center gap-1.5 font-semibold text-violet-700 dark:text-violet-300">
                    {formatNumber(queue.count)}
                    <ArrowRight className="h-3.5 w-3.5 transition-transform group-hover:translate-x-0.5" />
                  </span>
                </Link>
              ))}
            </CardContent>
          </Card>
        </section>

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
              <Link href={detailedLedgerHref} aria-label="Drill down to revenue ledger detail" className="rounded-xl focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
              <Card className="h-full border-emerald-200/80 transition-colors hover:bg-emerald-50/60 dark:border-emerald-900/70 dark:hover:bg-emerald-950/20">
                <CardContent className="flex items-start justify-between gap-3 p-4">
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Revenue</p>
                    <p className="mt-2 text-xl font-semibold">{formatReportingMoney(data.financeOverview.kpis.revenue)}</p>
                    <p className="mt-1 text-xs text-slate-500">{formatComparison(data.financeOverview.kpis.revenueChangePercent, 'previous period')}</p>
                  </div>
                  <TrendingUp className="h-5 w-5 text-emerald-600" />
                </CardContent>
              </Card>
              </Link>

              <Link href={detailedLedgerHref} aria-label="Drill down to expense ledger detail" className="rounded-xl focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
              <Card className="h-full border-amber-200/80 transition-colors hover:bg-amber-50/60 dark:border-amber-900/70 dark:hover:bg-amber-950/20">
                <CardContent className="flex items-start justify-between gap-3 p-4">
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Expenses</p>
                    <p className="mt-2 text-xl font-semibold">{formatReportingMoney(data.financeOverview.kpis.expenses)}</p>
                    <p className="mt-1 text-xs text-slate-500">{formatComparison(data.financeOverview.kpis.expensesChangePercent, 'previous period')}</p>
                  </div>
                  <Banknote className="h-5 w-5 text-amber-600" />
                </CardContent>
              </Card>
              </Link>

              <Link href={detailedLedgerHref} aria-label="Drill down to net-position ledger detail" className="rounded-xl focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
              <Card className="h-full border-blue-200/80 transition-colors hover:bg-blue-50/60 dark:border-blue-900/70 dark:hover:bg-blue-950/20">
                <CardContent className="flex items-start justify-between gap-3 p-4">
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Net position</p>
                    <p className="mt-2 text-xl font-semibold">{formatReportingMoney(data.financeOverview.kpis.netProfit)}</p>
                    <p className="mt-1 text-xs text-slate-500">{formatComparison(data.financeOverview.kpis.netProfitChangePercent, 'previous period')}</p>
                  </div>
                  <Gauge className="h-5 w-5 text-blue-600" />
                </CardContent>
              </Card>
              </Link>

              <Link href={detailedLedgerHref} aria-label="Drill down to cash ledger detail" className="rounded-xl focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
              <Card className="h-full border-violet-200/80 transition-colors hover:bg-violet-50/60 dark:border-violet-900/70 dark:hover:bg-violet-950/20">
                <CardContent className="flex items-start justify-between gap-3 p-4">
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Cash on hand</p>
                    <p className="mt-2 text-xl font-semibold">{formatReportingMoney(data.financeOverview.kpis.cashOnHand)}</p>
                    <p className="mt-1 text-xs text-slate-500">As at the selected period end</p>
                  </div>
                  <WalletCards className="h-5 w-5 text-violet-600" />
                </CardContent>
              </Card>
              </Link>
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
                detailsHref={detailedLedgerHref}
                detailsLabel="Open ledger"
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
                detailsHref={detailedLedgerHref}
                detailsLabel="Open ledger"
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
                detailsHref="/procurement/purchase-orders"
                detailsLabel="View orders"
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
                detailsHref="/procurement/purchase-orders"
                detailsLabel="View orders"
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
                detailsHref="/inventory/valuation"
                detailsLabel="View valuation"
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
                detailsHref="/administration/procurement/supplier-risk"
                detailsLabel="View suppliers"
                getDatumHref={() => '/administration/procurement/supplier-risk'}
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

        <div className={cn('grid gap-4 lg:grid-cols-2', interfaceStyle === 'immersive' && '2xl:grid-cols-3')}>
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
            detailsHref="/crm/opportunities"
            detailsLabel="View pipeline"
            getDatumHref={(datum) => buildQueryHref('/crm/opportunities', {
              stage: String(datum.stage ?? ''),
            })}
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
            detailsHref="/crm/leads"
            detailsLabel="View funnel"
            getDatumHref={(datum) => getCrmFunnelHref(String(datum.stage ?? ''))}
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
            detailsHref="/crm/accounts"
            detailsLabel="View accounts"
            getDatumHref={(datum) => buildQueryHref('/crm/accounts', {
              healthCategory: String(datum.name ?? ''),
            })}
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
            detailsHref="/development/projects"
            detailsLabel="View projects"
            getDatumHref={() => '/development/projects'}
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
            detailsHref="/inventory/requisitions"
            detailsLabel="View queue"
            getDatumHref={() => '/inventory/requisitions'}
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
            detailsHref="/maintenance/work-orders"
            detailsLabel="View work orders"
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
            detailsHref="/maintenance/work-orders"
            detailsLabel="View work orders"
            getDatumHref={() => '/maintenance/work-orders'}
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
            detailsHref="/procurement/tenders"
            detailsLabel="View tenders"
            getDatumHref={() => '/procurement/tenders'}
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
            detailsHref="/workflow/inbox"
            detailsLabel="View work"
            getDatumHref={(datum) => operationalModuleHref[String(datum.module ?? '')]}
            className="border-slate-200/80 shadow-lg shadow-slate-200/50 dark:border-slate-800/80 dark:shadow-none"
          />
        </div>
      </div>
    </DashboardLayout>
  );
}
