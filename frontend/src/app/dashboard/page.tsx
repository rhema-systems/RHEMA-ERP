'use client';

import Link from 'next/link';
import Image from 'next/image';
import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { differenceInCalendarDays, format, subMonths } from 'date-fns';
import type { DateRange } from 'react-day-picker';
import {
  Activity,
  AlertTriangle,
  ArrowRight,
  Banknote,
  Boxes,
  ChartNoAxesCombined,
  CircleCheckBig,
  Clock3,
  Coins,
  FileText,
  FolderKanban,
  Gauge,
  MapPin,
  ListTodo,
  Server,
  ShieldAlert,
  type LucideIcon,
  RefreshCw,
  Scale,
  ShoppingCart,
  Sparkles,
  TrendingUp,
  Wallet,
  WalletCards,
  Wrench,
} from 'lucide-react';
import { DashboardLayout } from '../../components/layout/dashboard-layout';
import {
  BaseBarChart,
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
import { systemHealthService } from '../../services/systemHealthService';
import {
  CompactProgressWidget,
  ConversionFunnelWidget,
  DashboardCoverageNotice,
  DashboardEmptyState,
  DashboardModuleUnavailableWidget,
  ExecutiveWidget,
  ExpenseAccountsWidget,
  FinancialPerformanceWidget,
  MaintenanceTrendWidget,
  MiniTrend,
  PipelineWidget,
  RiskMixWidget,
} from '../../components/dashboard/ExecutiveDashboardWidgets';

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

const getCrmPipelineHref = (stageId: string) =>
  buildQueryHref('/crm/opportunities', { stageDefinitionId: stageId });

const getCrmFunnelHref = (stageId: string, rangeStart: string, rangeEnd: string) =>
  buildQueryHref('/crm/opportunities', {
    reachedStageDefinitionId: stageId,
    stageEnteredFrom: rangeStart,
    stageEnteredTo: rangeEnd,
  });

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
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-12">
        {Array.from({ length: 5 }).map((_, index) => (
          <Skeleton key={index} className={cn('h-32 rounded-2xl', index === 4 ? 'md:col-span-2 xl:col-span-4' : 'xl:col-span-2')} />
        ))}
      </div>
      <div className="grid gap-3 lg:grid-cols-2 xl:grid-cols-12">
        {Array.from({ length: 4 }).map((_, index) => (
          <Skeleton key={index} className={cn('h-[285px] rounded-2xl', index < 2 ? 'xl:col-span-4' : 'xl:col-span-2')} />
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
  const moduleStatus = (name: string) => data.moduleStatus.find((module) => module.module === name);
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

  if (data.crm && moduleIsAvailable('CRM')) summaryCards.push(
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

  if (data.projectDashboard && moduleIsAvailable('Projects')) summaryCards.push(
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
    stageId: stage.stageId,
    stage: stage.stage,
    stageOrder: stage.stageOrder,
    isClosed: stage.isClosed,
    isWon: stage.isWon,
    opportunities: stage.opportunityCount,
    quotes: stage.quoteCount,
    percentageOfActivePipeline: stage.percentageOfActivePipeline,
    averageAgeDays: stage.averageAgeDays,
    stalledOpportunityCount: stage.stalledOpportunityCount,
    overdueOpportunityCount: stage.overdueOpportunityCount,
    amountsByCurrency: stage.amountsByCurrency,
    weightedAmountsByCurrency: stage.weightedAmountsByCurrency,
    opportunitiesWithoutCurrencyCount: stage.opportunitiesWithoutCurrencyCount,
  }));

  const crmFunnelData = (data.crm?.conversionFunnel ?? []).map((stage) => ({
    stageId: stage.stageId,
    stage: stage.stage,
    stageOrder: stage.stageOrder,
    count: stage.count,
    conversionRate: stage.conversionRate,
    overallConversionRate: stage.overallConversionRate,
    amountsByCurrency: stage.amountsByCurrency,
    opportunitiesWithoutCurrencyCount: stage.opportunitiesWithoutCurrencyCount,
  }));
  const getCurrentCrmFunnelHref = (stageId: string) => data.crm
    ? getCrmFunnelHref(stageId, data.crm.funnelRangeStart, data.crm.funnelRangeEnd)
    : '/crm/opportunities';

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
  ].filter((item) => item.count > 0);

  const maintenanceTrendData = (data.maintenanceTrends?.creationTrend ?? []).map((point, index) => ({
    period: new Date(point.date).toLocaleDateString(undefined, { month: 'short', day: 'numeric' }),
    created: point.value,
    completed: data.maintenanceTrends?.completionTrend[index]?.value ?? 0,
  }));

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

  const alertCoverageGaps = [
    ...(!data.crm ? ['CRM'] : []),
    ...(!data.projectDashboard ? ['Projects'] : []),
    ...(!data.maintenanceOverview && !data.maintenanceMetrics ? ['Maintenance'] : []),
    ...(!procurementQueuesAvailable ? ['Procurement'] : []),
  ];

  const workQueueCoverageGaps = [
    ...(!procurementQueuesAvailable ? ['Procurement'] : []),
    ...(!inventoryQueuesAvailable ? ['Inventory'] : []),
  ];

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

  const financialOverviewCards = data.financeOverview ? [
    {
      title: 'Total Revenue',
      value: formatReportingMoney(data.financeOverview.kpis.revenue),
      meta: formatComparison(data.financeOverview.kpis.revenueChangePercent, 'previous period'),
      href: detailedLedgerHref,
      icon: ChartNoAxesCombined,
      iconClassName: 'bg-emerald-100 text-emerald-600 dark:bg-emerald-950/70 dark:text-emerald-300',
      surfaceClassName: 'from-emerald-50/95 via-white to-white dark:from-emerald-950/30 dark:via-[#1d1d1d] dark:to-[#1d1d1d]',
      trendColor: '#18b77d',
      trendValues: data.financeOverview.monthly.map((point) => point.revenue),
    },
    {
      title: 'Total Expenses',
      value: formatReportingMoney(data.financeOverview.kpis.expenses),
      meta: formatComparison(data.financeOverview.kpis.expensesChangePercent, 'previous period'),
      href: detailedLedgerHref,
      icon: Wallet,
      iconClassName: 'bg-blue-100 text-blue-600 dark:bg-blue-950/70 dark:text-blue-300',
      surfaceClassName: 'from-rose-50/95 via-white to-white dark:from-rose-950/30 dark:via-[#1d1d1d] dark:to-[#1d1d1d]',
      trendColor: '#ef476f',
      trendValues: data.financeOverview.monthly.map((point) => point.expenses),
    },
    {
      title: 'Net Position',
      value: formatReportingMoney(data.financeOverview.kpis.netProfit),
      meta: formatComparison(data.financeOverview.kpis.netProfitChangePercent, 'previous period'),
      href: detailedLedgerHref,
      icon: Scale,
      iconClassName: 'bg-violet-100 text-violet-600 dark:bg-violet-950/70 dark:text-violet-300',
      surfaceClassName: 'from-blue-50/95 via-white to-white dark:from-blue-950/30 dark:via-[#1d1d1d] dark:to-[#1d1d1d]',
      trendColor: '#2474ff',
      trendValues: data.financeOverview.monthly.map((point) => point.revenue - point.expenses),
    },
    {
      title: 'Cash on Hand',
      value: formatReportingMoney(data.financeOverview.kpis.cashOnHand),
      meta: 'As at the selected period end',
      href: detailedLedgerHref,
      icon: Coins,
      iconClassName: 'bg-orange-100 text-orange-600 dark:bg-orange-950/70 dark:text-orange-300',
      surfaceClassName: 'from-amber-50/95 via-white to-white dark:from-amber-950/30 dark:via-[#1d1d1d] dark:to-[#1d1d1d]',
      trendColor: '#f59e0b',
      trendValues: [] as number[],
    },
  ] : summaryCards.slice(0, 4).map((card) => ({
    ...card,
    surfaceClassName: 'from-slate-50 via-white to-white dark:from-neutral-800 dark:via-[#1d1d1d] dark:to-[#1d1d1d]',
    trendColor: '#2474ff',
    trendValues: [] as number[],
  }));

  return (
    <DashboardLayout>
      <div className="space-y-3.5" data-dashboard-style="immersive">
        <section
          className="relative isolate min-h-[128px] overflow-hidden rounded-[24px] border border-blue-200/60 bg-gradient-to-r from-blue-50 via-sky-50 to-emerald-50 px-5 py-3.5 shadow-[0_24px_55px_-32px_rgba(37,99,235,0.48)] dark:border-blue-900/60 dark:from-blue-950/50 dark:via-neutral-900 dark:to-emerald-950/30 lg:px-6"
          aria-labelledby="dashboard-welcome-title"
        >
          <div className="pointer-events-none absolute inset-0 -z-20">
            <Image
              src="/images/dashboard/dashboard-hero-office.png"
              alt=""
              fill
              priority
              sizes="100vw"
              className="object-cover object-[center_62%] dark:opacity-70"
            />
          </div>
          <div className="pointer-events-none absolute inset-0 -z-10 bg-gradient-to-r from-blue-50/95 via-blue-50/80 to-white/15 dark:from-blue-950/95 dark:via-blue-950/80 dark:to-neutral-950/35" />

          <div className="flex flex-col gap-3 xl:flex-row xl:items-end xl:justify-between">
            <div className="relative max-w-xl">
              <p className="text-sm font-medium text-slate-500 dark:text-slate-400">
                Good {new Date().getHours() < 12 ? 'morning' : new Date().getHours() < 18 ? 'afternoon' : 'evening'},
              </p>
              <h1 id="dashboard-welcome-title" className="mt-0.5 text-2xl font-semibold tracking-[-0.02em] text-slate-950 dark:text-white sm:text-[1.75rem]">
                Welcome back, {displayName}!
              </h1>
              <p className="mt-1 text-sm text-slate-600 dark:text-slate-300">
                Here&apos;s what&apos;s happening across your organization today.
              </p>
              <p className="mt-3 hidden items-center gap-2 text-xs font-semibold italic text-slate-700 dark:text-slate-200 2xl:flex">
                <Sparkles className="h-4 w-4 text-blue-600" aria-hidden="true" />
                People. Process. Progress. A stronger tomorrow, together.
              </p>
            </div>

            <div className="relative mt-auto flex flex-wrap items-center gap-2 lg:justify-end">
              <DatePickerWithRange value={selectedRange} onChange={handleRangeChange} className="w-full sm:w-[252px]" placeholder="Select dashboard period" />
              <Select value={warehouseId} onValueChange={(value) => { setWarehouseId(value); setLocationId('all'); }}>
                <SelectTrigger className="h-10 w-full border-white/80 bg-white/90 shadow-sm sm:w-[185px] dark:border-neutral-700 dark:bg-neutral-900/85" aria-label="Dashboard warehouse">
                  <MapPin className="mr-2 h-4 w-4 shrink-0 text-blue-600" />
                  <SelectValue placeholder="All permitted warehouses" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All permitted warehouses</SelectItem>
                  {warehouseOptions.map((warehouse) => <SelectItem key={warehouse.id} value={warehouse.id}>{warehouse.code} · {warehouse.name}</SelectItem>)}
                </SelectContent>
              </Select>
              <Select value={locationId} onValueChange={setLocationId} disabled={warehouseId === 'all'}>
                <SelectTrigger className="h-10 w-full border-white/80 bg-white/90 shadow-sm sm:w-[185px] dark:border-neutral-700 dark:bg-neutral-900/85" aria-label="Dashboard warehouse location">
                  <SelectValue placeholder={warehouseId === 'all' ? 'Select warehouse first' : 'All permitted locations'} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All permitted locations</SelectItem>
                  {locationOptions.map((location) => <SelectItem key={location.id} value={location.id}>{location.locationCode} · {location.name || location.locationCode}</SelectItem>)}
                </SelectContent>
              </Select>
              <Button size="sm" onClick={() => refetch()} disabled={isFetching} className="h-10 rounded-xl bg-blue-600 px-4 text-white shadow-sm hover:bg-blue-700">
                <RefreshCw className={cn('mr-2 h-4 w-4', isFetching && 'animate-spin')} />
                Refresh
              </Button>
              <span className="w-full text-right text-[0.65rem] font-medium text-slate-500 dark:text-slate-400">
                Last updated {formatRelativeTime(data.lastUpdated)}
              </span>
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

        <section className="grid items-start gap-3 xl:grid-cols-[minmax(0,4fr)_minmax(13.5rem,0.82fr)]" aria-label="Executive financial indicators, performance and system status">
          <div className="min-w-0 space-y-3">
            <div className="grid items-start gap-3 sm:grid-cols-2 lg:grid-cols-4" aria-label="Executive financial indicators">
              {financialOverviewCards.map((card) => {
                const Icon = card.icon;
                return (
                  <Link
                    key={card.title}
                    href={card.href}
                    aria-label={`Open ${card.title} details`}
                    className="group block min-w-0 rounded-2xl focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 focus-visible:ring-offset-2"
                  >
                    <article className={cn('min-h-[124px] overflow-hidden rounded-2xl border border-slate-200/70 bg-gradient-to-br p-4 shadow-[0_10px_30px_-24px_rgba(15,23,42,0.55)] transition-all group-hover:-translate-y-0.5 group-hover:shadow-lg dark:border-neutral-700/80', card.surfaceClassName)}>
                      <div className="flex items-start gap-3">
                        <span className={cn('flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl shadow-sm', card.iconClassName)}>
                          <Icon className="h-5 w-5" />
                        </span>
                        <div className="min-w-0 flex-1">
                          <p className="text-[0.7rem] font-semibold text-slate-600 dark:text-slate-300">{card.title}</p>
                          <p className="mt-1 whitespace-normal break-words text-[clamp(1rem,1.35vw,1.35rem)] font-extrabold leading-tight tracking-tight text-slate-950 tabular-nums dark:text-white">{card.value}</p>
                        </div>
                      </div>
                      <div className="mt-3 flex items-end justify-between gap-3">
                        <p className="truncate text-[0.68rem] font-medium text-slate-500 dark:text-slate-400">{card.meta}</p>
                        <MiniTrend values={card.trendValues} color={card.trendColor} />
                      </div>
                    </article>
                  </Link>
                );
              })}
            </div>

            <section aria-label="Executive performance overview">
              {data.financeOverview ? (
                <div className="grid gap-3 lg:grid-cols-5">
                  <div className="lg:col-span-3">
                    <FinancialPerformanceWidget data={data.financeOverview.monthly} formatValue={formatReportingMoney} href={detailedLedgerHref} />
                  </div>
                  <div className="lg:col-span-2">
                    <ExpenseAccountsWidget data={data.financeOverview.expenseChart} formatValue={formatReportingMoney} href={detailedLedgerHref} />
                  </div>
                </div>
              ) : (
                <DashboardModuleUnavailableWidget title="Financial performance" moduleName="Finance" accessRestricted={moduleStatus('Finance')?.accessRestricted} href="/finance" />
              )}
            </section>
          </div>

          <aside className="space-y-3" aria-label="System status and active alerts">
            <ExecutiveWidget
              title="System Health"
              description={systemHealthError ? 'Readiness checks unavailable' : 'Live service readiness'}
              icon={<Server className="h-4 w-4" />}
              className="min-h-[124px]"
            >
              <div className="grid gap-x-4 px-4 pb-3 pt-0.5 sm:grid-cols-2 sm:px-5 xl:grid-cols-1 xl:px-4">
                {systemHealth?.checks.slice(0, 4).map((check) => {
                  const healthy = check.status.toLowerCase() === 'healthy';
                  return (
                    <div key={check.name} className="flex items-center gap-2 border-b border-slate-100 py-1.5 last:border-0 dark:border-neutral-800">
                      <span className={cn('h-2 w-2 rounded-full', healthy ? 'bg-emerald-500' : 'bg-rose-500')} />
                      <span className="min-w-0 flex-1 truncate text-[0.68rem] font-medium capitalize text-slate-700 dark:text-slate-200">{check.name.replaceAll('-', ' ')}</span>
                      <span className={cn('rounded-full px-2 py-0.5 text-[0.58rem] font-semibold', healthy ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300' : 'bg-rose-50 text-rose-700 dark:bg-rose-950/50 dark:text-rose-300')}>{check.status}</span>
                    </div>
                  );
                })}
                {!systemHealth && !systemHealthError ? <p className="py-3 text-center text-xs text-slate-500 sm:col-span-2">Checking service readiness…</p> : null}
                {systemHealthError ? <p className="py-3 text-center text-xs text-rose-600 sm:col-span-2">Readiness checks are currently unavailable.</p> : null}
              </div>
            </ExecutiveWidget>

            <ExecutiveWidget title="Active Alerts" description={`${criticalAlertCount} records require review`} icon={<AlertTriangle className="h-4 w-4 text-rose-500" />} className="min-h-[285px]">
              <DashboardCoverageNotice modules={alertCoverageGaps} />
              <div className="space-y-1 px-4 pb-4 pt-1 sm:px-5">
                {operationalAlerts.length === 0 ? (
                  <div className="flex items-center gap-2 rounded-xl bg-emerald-50 px-3 py-3 text-xs font-medium text-emerald-700 dark:bg-emerald-950/30 dark:text-emerald-300"><CircleCheckBig className="h-4 w-4" />{alertCoverageGaps.length > 0 ? 'No alerts found in available modules' : 'No active operational alerts'}</div>
                ) : operationalAlerts.slice(0, 4).map((alert, index) => (
                  <Link key={alert.label} href={alert.href} className="group flex items-center gap-2 border-b border-slate-100 py-1.5 last:border-0 dark:border-neutral-800">
                    <span className={cn('flex h-6 w-6 shrink-0 items-center justify-center rounded-lg text-[0.65rem] font-bold', index === 0 ? 'bg-rose-100 text-rose-700 dark:bg-rose-950/60 dark:text-rose-300' : 'bg-amber-100 text-amber-700 dark:bg-amber-950/60 dark:text-amber-300')}>{formatNumber(alert.count)}</span>
                    <span className="min-w-0 flex-1 truncate text-[0.7rem] font-medium text-slate-700 dark:text-slate-200">{alert.label}</span>
                    <ArrowRight className="h-3.5 w-3.5 text-blue-600 transition-transform group-hover:translate-x-0.5" />
                  </Link>
                ))}
              </div>
            </ExecutiveWidget>
          </aside>
        </section>

        <section className="grid gap-3 lg:grid-cols-2 xl:grid-cols-12" aria-label="Customer relationship performance and personal work">
          <div className="xl:col-span-3 [&>section]:h-full">
            {data.crm ? (
              <PipelineWidget
                data={pipelineStageData}
                href="/crm/opportunities"
                getHref={getCrmPipelineHref}
                preferredCurrency={data.reportingCurrency?.currencyCode}
                pipelineAsOf={data.crm.pipelineAsOf}
              />
            ) : (
              <DashboardModuleUnavailableWidget title="CRM Pipeline" moduleName="CRM" accessRestricted={moduleStatus('CRM')?.accessRestricted} href="/crm/opportunities" className="h-full" />
            )}
          </div>
          <div className="xl:col-span-3 [&>section]:h-full">
            {data.crm ? (
              <ConversionFunnelWidget
                data={crmFunnelData}
                href="/crm/opportunities"
                getHref={getCurrentCrmFunnelHref}
                preferredCurrency={data.reportingCurrency?.currencyCode}
                rangeStart={data.crm.funnelRangeStart}
                rangeEnd={data.crm.funnelRangeEnd}
                historyCoverageStart={data.crm.historyCoverageStart}
                lostOpportunityCount={data.crm.lostOpportunityCount}
                dataQualityIssues={data.crm.dataQualityIssues}
              />
            ) : (
              <DashboardModuleUnavailableWidget title="Sales Conversion Funnel" moduleName="CRM" accessRestricted={moduleStatus('CRM')?.accessRestricted} href="/crm/leads" className="h-full" />
            )}
          </div>
          <div className="xl:col-span-3 [&>section]:h-full">
            {data.crm ? (
              <RiskMixWidget data={crmHealthData} href="/crm/accounts" />
            ) : (
              <DashboardModuleUnavailableWidget title="Account Risk Mix" moduleName="CRM" accessRestricted={moduleStatus('CRM')?.accessRestricted} href="/crm/accounts" className="h-full" />
            )}
          </div>
          <aside className="xl:col-span-3" aria-label="My tasks and approvals">
            <ExecutiveWidget title="My Tasks & Approvals" description="Live workload across permitted modules" href="/workflow/inbox" actionLabel="View all" icon={<ListTodo className="h-4 w-4 text-violet-600" />} className="h-full">
              <DashboardCoverageNotice modules={workQueueCoverageGaps} />
              {operationalWorkQueues.length === 0 ? (
                <DashboardEmptyState
                  title={workQueueCoverageGaps.length > 0 ? 'No work found in available queues' : 'No pending work'}
                  description={workQueueCoverageGaps.length > 0 ? 'Some queue data is restricted or unavailable.' : 'Available operational work will appear here.'}
                  href="/workflow/inbox"
                  actionLabel="Open inbox"
                />
              ) : (
                <div className="space-y-1 px-4 pb-4 pt-1 sm:px-5">
                  {operationalWorkQueues.slice(0, 4).map((queue, index) => (
                    <Link key={queue.label} href={queue.href} className="group flex items-center gap-2 border-b border-slate-100 py-1.5 last:border-0 dark:border-neutral-800">
                      <span className={cn('flex h-6 w-6 items-center justify-center rounded-lg text-[0.65rem] font-bold', index % 2 === 0 ? 'bg-blue-50 text-blue-700 dark:bg-blue-950/50 dark:text-blue-300' : 'bg-violet-50 text-violet-700 dark:bg-violet-950/50 dark:text-violet-300')}>{queue.count}</span>
                      <span className="min-w-0 flex-1 truncate text-[0.7rem] font-medium text-slate-700 dark:text-slate-200">{queue.label}</span>
                      <ArrowRight className="h-3.5 w-3.5 text-blue-600 transition-transform group-hover:translate-x-0.5" />
                    </Link>
                  ))}
                </div>
              )}
            </ExecutiveWidget>
          </aside>
        </section>

        <section className="grid gap-3 lg:grid-cols-2 xl:grid-cols-12" aria-label="Operational delivery overview">
          <div className="xl:col-span-3 [&>section]:h-full">
            {data.projectDashboard ? (
              <CompactProgressWidget
                title="Project Delivery Pressure"
                description="Tasks, milestones, risks and issues"
                data={projectPressureData}
                href="/development/projects"
                actionLabel="View projects"
                emptyTitle="No project pressure"
                emptyDescription="No overdue tasks, milestones, risks or issues are currently visible."
              />
            ) : (
              <DashboardModuleUnavailableWidget title="Project Delivery Pressure" moduleName="Projects" accessRestricted={moduleStatus('Projects')?.accessRestricted} href="/development/projects" className="h-full" />
            )}
          </div>
          <div className="xl:col-span-3 [&>section]:h-full">
            {inventoryQueuesAvailable ? (
              <CompactProgressWidget
                title="Inventory Queue"
                description="Approvals and issue workload"
                data={inventoryQueueData.map((item) => ({ name: item.label, value: item.count }))}
                href="/inventory/requisitions"
                actionLabel="View queue"
                emptyTitle="Inventory queue is clear"
                emptyDescription="No permitted approvals or issue requests are waiting."
              />
            ) : (
              <DashboardModuleUnavailableWidget title="Inventory Queue" moduleName="Inventory" accessRestricted={moduleStatus('Inventory Queues')?.accessRestricted} href="/inventory/requisitions" className="h-full" />
            )}
          </div>
          <div className="xl:col-span-3 [&>section]:h-full">
            {data.maintenanceTrends ? (
              <MaintenanceTrendWidget data={maintenanceTrendData} href="/maintenance/work-orders" />
            ) : (
              <DashboardModuleUnavailableWidget title="Maintenance Work Orders" moduleName="Maintenance" accessRestricted={moduleStatus('Maintenance Trends')?.accessRestricted} href="/maintenance/work-orders" className="h-full" />
            )}
          </div>
          <aside className="relative min-h-[190px] overflow-hidden rounded-2xl border border-blue-200/70 bg-gradient-to-br from-blue-700 via-blue-600 to-sky-400 p-5 text-white shadow-[0_14px_34px_-22px_rgba(37,99,235,0.72)] xl:col-span-3" aria-label="RHEMA ERP brand message">
            <div className="absolute -bottom-16 -right-10 h-44 w-44 rounded-full bg-white/20 blur-2xl" />
            <Activity className="relative h-7 w-7 text-blue-100" aria-hidden="true" />
            <p className="relative mt-5 text-xl font-extrabold leading-tight">Operational excellence<br />today for a better tomorrow</p>
            <p className="relative mt-3 text-xs text-blue-50">People. Process. Progress.</p>
          </aside>
        </section>

        {queueLoadData.length > 0 ? (
          <CompactProgressWidget
            title="Operational Queue Load"
            description="Where work is building across permitted modules"
            data={queueLoadData.map((item) => ({ name: item.module, value: item.items }))}
            href="/workflow/inbox"
            actionLabel="View work"
            emptyTitle="Operational queues are clear"
            emptyDescription="No open work is currently visible across permitted modules."
          />
        ) : null}

        {management && (
          <details className="group rounded-2xl border border-slate-200/75 bg-white shadow-[0_8px_24px_-22px_rgba(15,23,42,0.5)] dark:border-neutral-700/80 dark:bg-[#1d1d1d]">
            <summary className="flex cursor-pointer list-none items-center gap-3 px-4 py-3 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 sm:px-5">
              <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-amber-50 text-amber-600 dark:bg-amber-950/50 dark:text-amber-300"><ShoppingCart className="h-4 w-4" /></span>
              <span className="min-w-0 flex-1">
                <strong id="procurement-inventory-management-title" className="block text-sm text-slate-950 dark:text-white">Procurement and inventory management</strong>
                <span className="block truncate text-[0.68rem] text-slate-500 dark:text-slate-400">
                  {format(new Date(management.rangeStartDate), 'dd MMM yyyy')} – {format(new Date(management.rangeEndDate), 'dd MMM yyyy')} · Stock updated {formatRelativeTime(management.inventoryAsOfUtc)}
                </span>
              </span>
              <span className="rounded-full bg-slate-100 px-3 py-1 text-[0.65rem] font-semibold text-slate-600 group-open:bg-blue-50 group-open:text-blue-700 dark:bg-neutral-800 dark:text-slate-300 dark:group-open:bg-blue-950/50 dark:group-open:text-blue-300">View management detail</span>
            </summary>
            <section className="space-y-3 border-t border-slate-100 p-4 dark:border-neutral-800" aria-labelledby="procurement-inventory-management-title">

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
          </details>
        )}

      </div>
    </DashboardLayout>
  );
}
