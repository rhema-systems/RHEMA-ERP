'use client';

import Link from 'next/link';
import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import {
  AlertTriangle,
  ArrowRight,
  Boxes,
  FileText,
  FolderKanban,
  Loader2,
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
import { useAuth } from '../../hooks/use-auth';
import { cn } from '../../lib/utils';
import { authService } from '../../services/auth';
import { dashboardService } from '../../services/dashboard';

interface SummaryCardDefinition {
  title: string;
  value: string;
  meta: string;
  href: string;
  accentClassName: string;
  iconClassName: string;
  icon: LucideIcon;
}

const formatCurrency = (value: number) =>
  new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(value);

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

const getDaysUntil = (value?: string) => {
  if (!value) return null;

  const target = new Date(value).getTime();
  if (Number.isNaN(target)) return null;

  return Math.ceil((target - Date.now()) / (1000 * 60 * 60 * 24));
};

const sumBy = <T,>(items: T[], selector: (item: T) => number) =>
  items.reduce((total, item) => total + selector(item), 0);

export default function Dashboard() {
  const router = useRouter();
  const { user } = useAuth();

  useEffect(() => {
    if (typeof window === 'undefined') return;

    const storedUser = authService.getStoredUser();
    if (storedUser?.roles?.includes('ExternalUser')) {
      router.push('/external-portal');
    }
  }, [router]);

  const { data, isLoading, isFetching, refetch } = useQuery({
    queryKey: ['enterprise-dashboard'],
    queryFn: () => dashboardService.getEnterpriseDashboard(),
    staleTime: 60_000,
  });

  const storedUser = authService.getStoredUser();
  const displayName = user?.firstName || storedUser?.firstName || user?.username || storedUser?.username || 'User';

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
  const maintenanceCompletionRate =
    (maintenanceSummary?.totalWorkOrders ?? 0) > 0
      ? ((maintenanceSummary?.completedWorkOrders ?? data.maintenanceMetrics?.completedWorkOrders ?? 0) /
          Math.max(1, maintenanceSummary?.totalWorkOrders ?? 0)) *
        100
      : 0;

  const openTenders = data.tenders.filter((tender) => !['closed', 'cancelled', 'awarded', 'completed'].includes(tender.status.toLowerCase()));
  const closingSoonTenders = openTenders.filter((tender) => {
    const daysUntil = getDaysUntil(tender.submissionDeadline);
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
      value: formatNumber(maintenanceSummary?.activeWorkOrders ?? data.maintenanceMetrics?.inProgressWorkOrders ?? 0),
      meta: `${maintenanceSummary?.overdueWorkOrders ?? data.maintenanceMetrics?.overdueWorkOrders ?? 0} overdue work orders`,
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
    { name: 'Active', value: maintenanceSummary?.activeWorkOrders ?? data.maintenanceMetrics?.inProgressWorkOrders ?? 0 },
    { name: 'Overdue', value: maintenanceSummary?.overdueWorkOrders ?? data.maintenanceMetrics?.overdueWorkOrders ?? 0 },
    { name: 'Completed', value: maintenanceSummary?.completedWorkOrders ?? data.maintenanceMetrics?.completedWorkOrders ?? 0 },
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
    { module: 'Maintenance', items: (maintenanceSummary?.activeWorkOrders ?? 0) + (maintenanceSummary?.overdueWorkOrders ?? 0) },
    { module: 'Tenders', items: openTenders.length + closingSoonTenders.length },
  ];

  const criticalAlertCount =
    (data.crmOverview?.leadsNeedingFollowUpCount ?? 0) +
    (data.projectDashboard?.overdueMilestones ?? 0) +
    (maintenanceSummary?.overdueWorkOrders ?? 0) +
    closingSoonTenders.length;

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
                  Welcome back, {displayName}. This view keeps the main modules raw and chart-first so you can scan business movement without drilling into tabs.
                </p>
              </div>
            </div>

            <div className="flex items-center gap-2">
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
