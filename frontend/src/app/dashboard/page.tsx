'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import {
  Activity,
  AlertTriangle,
  ArrowRight,
  Boxes,
  ChevronRight,
  CheckCircle2,
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
import { BaseBarChart, BaseLineChart, BasePieChart, CHART_COLORS } from '../../components/analytics/charts/BaseCharts';
import { Alert, AlertDescription } from '../../components/ui/alert';
import { Badge } from '../../components/ui/badge';
import { Button } from '../../components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../components/ui/card';
import { Progress } from '../../components/ui/progress';
import { Tabs, TabsList, TabsTrigger } from '../../components/ui/tabs';
import { useAuth } from '../../hooks/use-auth';
import { cn } from '../../lib/utils';
import { authService } from '../../services/auth';
import { dashboardService } from '../../services/dashboard';

type PriorityLevel = 'critical' | 'warning' | 'info';
type DashboardModuleFocus = 'all' | 'crm' | 'projects' | 'procurement' | 'inventory' | 'maintenance' | 'tenders';
type DashboardChartView = 'pipeline' | 'projects' | 'procurement' | 'maintenance' | 'tenders' | 'queues';

interface PriorityItem {
  id: string;
  module: string;
  title: string;
  detail: string;
  href: string;
  priority: PriorityLevel;
  timestamp?: string;
}

interface FocusOption {
  key: DashboardModuleFocus;
  label: string;
  shortLabel: string;
  description: string;
}

interface HeadlineCardDefinition {
  module: Exclude<DashboardModuleFocus, 'all'>;
  title: string;
  description: string;
  value: string;
  subtext: string;
  href: string;
  progressLabel: string;
  progressValue: number;
  progressText: string;
  accentClassName: string;
  iconClassName: string;
  icon: LucideIcon;
}

interface ChartBreakdownItem {
  label: string;
  value: string;
  detail: string;
  href: string;
}

const FOCUS_OPTIONS: FocusOption[] = [
  { key: 'all', label: 'Executive View', shortLabel: 'Executive', description: 'Cross-module business pulse' },
  { key: 'crm', label: 'CRM', shortLabel: 'CRM', description: 'Commercial pipeline and account pressure' },
  { key: 'projects', label: 'Projects', shortLabel: 'Projects', description: 'Delivery execution and budget position' },
  { key: 'procurement', label: 'Procurement', shortLabel: 'Procurement', description: 'Requisitions, purchase orders, and exposure' },
  { key: 'inventory', label: 'Inventory', shortLabel: 'Inventory', description: 'Material movement and issue queues' },
  { key: 'maintenance', label: 'Maintenance', shortLabel: 'Maintenance', description: 'Backlog, completion, and due work' },
  { key: 'tenders', label: 'Tenders', shortLabel: 'Tenders', description: 'Bid desk activity and deadline pressure' },
];

const DEFAULT_CHART_BY_MODULE: Record<DashboardModuleFocus, DashboardChartView> = {
  all: 'pipeline',
  crm: 'pipeline',
  projects: 'projects',
  procurement: 'procurement',
  inventory: 'queues',
  maintenance: 'maintenance',
  tenders: 'tenders',
};

const normalizePriorityModule = (module: string): DashboardModuleFocus => {
  switch (module.toLowerCase()) {
    case 'crm':
      return 'crm';
    case 'projects':
      return 'projects';
    case 'procurement':
      return 'procurement';
    case 'inventory':
      return 'inventory';
    case 'maintenance':
      return 'maintenance';
    case 'tenders':
      return 'tenders';
    default:
      return 'all';
  }
};

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

const formatDate = (value?: string) => {
  if (!value) return 'No date';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? 'No date' : date.toLocaleDateString();
};

const getDaysUntil = (value?: string) => {
  if (!value) return null;

  const target = new Date(value).getTime();
  if (Number.isNaN(target)) return null;

  return Math.ceil((target - Date.now()) / (1000 * 60 * 60 * 24));
};

const getPriorityTone = (priority: PriorityLevel) => {
  switch (priority) {
    case 'critical':
      return 'border-red-200 bg-red-50 text-red-700 dark:border-red-900 dark:bg-red-950/30 dark:text-red-300';
    case 'warning':
      return 'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900 dark:bg-amber-950/30 dark:text-amber-300';
    default:
      return 'border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-900 dark:bg-blue-950/30 dark:text-blue-300';
  }
};

const getModuleBadgeTone = (available: boolean) =>
  available
    ? 'border-emerald-200 text-emerald-700 dark:border-emerald-800 dark:text-emerald-300'
    : 'border-amber-200 text-amber-700 dark:border-amber-800 dark:text-amber-300';

const choosePriority = (isCritical: boolean): PriorityLevel => (isCritical ? 'critical' : 'warning');

const sumBy = <T,>(items: T[], selector: (item: T) => number) =>
  items.reduce((total, item) => total + selector(item), 0);

const clampPercent = (value: number) => Math.max(0, Math.min(100, Number.isFinite(value) ? value : 0));

export default function Dashboard() {
  const router = useRouter();
  const { user } = useAuth();
  const [focusedModule, setFocusedModule] = useState<DashboardModuleFocus>('all');
  const [activeChartView, setActiveChartView] = useState<DashboardChartView>('pipeline');

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

  const focusModule = (module: DashboardModuleFocus) => {
    setFocusedModule(module);
    setActiveChartView(DEFAULT_CHART_BY_MODULE[module]);
  };

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

  const availableModules = data.moduleStatus.filter((status) => status.available);
  const unavailableModules = data.moduleStatus.filter((status) => !status.available);
  const isModuleLive = (...moduleNames: string[]) =>
    moduleNames.some((moduleName) => data.moduleStatus.some((status) => status.module === moduleName && status.available));
  const getModuleError = (...moduleNames: string[]) =>
    data.moduleStatus.find((status) => moduleNames.includes(status.module) && !status.available)?.error;

  const openPurchaseOrderValue = sumBy(data.openPurchaseOrders, (order) => order.totalAmount);
  const pendingPurchaseRequisitionValue = sumBy(data.pendingPurchaseRequisitions, (requisition) => requisition.totalAmount);
  const pendingInventoryValue =
    sumBy(data.pendingInventoryApprovals, (requisition) => requisition.totalValue) +
    sumBy(data.pendingInventoryIssues, (requisition) => requisition.totalValue);

  const maintenanceSummary = data.maintenanceOverview?.summary;
  const maintenanceCompletionRate =
    (maintenanceSummary?.totalWorkOrders ?? 0) > 0
      ? ((maintenanceSummary?.completedWorkOrders ?? data.maintenanceMetrics?.completedWorkOrders ?? 0) /
          (maintenanceSummary?.totalWorkOrders ?? 1)) *
        100
      : 0;

  const openTenders = data.tenders.filter((tender) => !['closed', 'cancelled', 'awarded', 'completed'].includes(tender.status.toLowerCase()));
  const tenderEstimatedValue = sumBy(openTenders, (tender) => tender.estimatedValue ?? 0);
  const tenderBidCount = sumBy(openTenders, (tender) => tender.bidCount ?? 0);
  const closingSoonTenders = [...openTenders]
    .filter((tender) => {
      const daysUntil = getDaysUntil(tender.submissionDeadline);
      return daysUntil !== null && daysUntil >= 0 && daysUntil <= 14;
    })
    .sort((left, right) => (new Date(left.submissionDeadline ?? 0).getTime() - new Date(right.submissionDeadline ?? 0).getTime()))
    .slice(0, 5);

  const maintenanceTrendData = (data.maintenanceTrends?.creationTrend ?? []).map((point, index) => ({
    period: new Date(point.date).toLocaleDateString(undefined, { month: 'short', day: 'numeric' }),
    created: point.value,
    completed: data.maintenanceTrends?.completionTrend[index]?.value ?? 0,
  }));

  const pipelineStageData = (data.crmReporting?.pipelineByStage ?? []).map((stage) => ({
    stage: stage.stage,
    totalValue: stage.totalValue,
    weightedValue: stage.weightedValue,
  }));

  const projectBudgetData = [
    { label: 'Estimated', amount: data.projectDashboard?.totalEstimatedBudget ?? 0 },
    { label: 'Approved', amount: data.projectDashboard?.totalApprovedBudget ?? 0 },
    { label: 'Actual', amount: data.projectDashboard?.totalActualCost ?? 0 },
  ];

  const procurementExposureData = [
    { label: 'Requisitions', amount: pendingPurchaseRequisitionValue },
    { label: 'Purchase Orders', amount: openPurchaseOrderValue },
    { label: 'Tenders', amount: tenderEstimatedValue },
    { label: 'Inventory', amount: pendingInventoryValue },
  ];

  const tenderStatusData = Object.values(
    openTenders.reduce<Record<string, { name: string; value: number }>>((accumulator, tender) => {
      const status = tender.status || 'Unknown';
      const current = accumulator[status] ?? { name: status, value: 0 };
      current.value += 1;
      accumulator[status] = current;
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

  const upcomingMaintenance = [...data.upcomingMaintenance]
    .sort((left, right) => {
      const leftDate = new Date(left.nextDue || left.nextDueDate || left.nextScheduledDate || 0).getTime();
      const rightDate = new Date(right.nextDue || right.nextDueDate || right.nextScheduledDate || 0).getTime();
      return leftDate - rightDate;
    })
    .slice(0, 5);

  const purchaseOrdersDueSoon = [...data.openPurchaseOrders]
    .filter((order) => order.requiredDate || order.promisedDate)
    .sort((left, right) => {
      const leftDate = new Date(left.requiredDate || left.promisedDate || 0).getTime();
      const rightDate = new Date(right.requiredDate || right.promisedDate || 0).getTime();
      return leftDate - rightDate;
    })
    .slice(0, 5);

  const priorityItems: PriorityItem[] = [
    ...(data.crmOverview?.followUps ?? []).slice(0, 3).map((followUp) => ({
      id: `crm-followup-${followUp.entityId}`,
      module: 'CRM',
      title: followUp.title,
      detail: followUp.context || followUp.status || 'Follow-up required',
      href: '/crm',
      priority: choosePriority((getDaysUntil(followUp.dueDate) ?? 0) <= 0),
      timestamp: followUp.dueDate,
    })),
    ...(data.projectDashboard?.atRiskProjects ?? []).slice(0, 3).map((project) => ({
      id: `project-risk-${project.id}`,
      module: 'Projects',
      title: project.title,
      detail: `${project.openRiskCount} risks • ${project.openIssueCount} issues • ${project.overdueMilestoneCount} overdue milestones`,
      href: `/development/projects/${project.id}`,
      priority: choosePriority(project.overdueMilestoneCount > 0),
      timestamp: project.targetEndDate,
    })),
    ...data.pendingPurchaseRequisitions.slice(0, 3).map((requisition) => ({
      id: `procurement-req-${requisition.id}`,
      module: 'Procurement',
      title: requisition.requisitionNumber,
      detail: `${requisition.requestedByName} • ${requisition.priority} priority • ${formatCurrency(requisition.totalAmount)}`,
      href: '/procurement/purchase-requisitions',
      priority: choosePriority(requisition.priority.toLowerCase() === 'urgent'),
      timestamp: requisition.requiredDate,
    })),
    ...data.pendingInventoryIssues.slice(0, 3).map((requisition) => ({
      id: `inventory-issue-${requisition.id}`,
      module: 'Inventory',
      title: requisition.requisitionNumber,
      detail: `${requisition.warehouseName} • ${requisition.totalItems} items awaiting issue`,
      href: '/inventory/requisitions',
      priority: 'info' as const,
      timestamp: requisition.requiredDate,
    })),
    ...upcomingMaintenance.slice(0, 3).map((schedule) => ({
      id: `maintenance-${schedule.id}`,
      module: 'Maintenance',
      title: schedule.assetName,
      detail: `${schedule.maintenanceTypeName || schedule.maintenanceType || schedule.name || 'Scheduled maintenance'} • ${schedule.assignedTechnicianName || 'Unassigned'}`,
      href: '/maintenance/scheduled',
      priority: choosePriority((getDaysUntil(schedule.nextDue || schedule.nextDueDate || schedule.nextScheduledDate) ?? 99) <= 3),
      timestamp: schedule.nextDue || schedule.nextDueDate || schedule.nextScheduledDate,
    })),
    ...closingSoonTenders.slice(0, 3).map((tender) => ({
      id: `tender-${tender.id}`,
      module: 'Tenders',
      title: tender.tenderNumber,
      detail: `${tender.title} • ${tender.bidCount} bids so far`,
      href: '/procurement/tenders',
      priority: choosePriority((getDaysUntil(tender.submissionDeadline) ?? 99) <= 3),
      timestamp: tender.submissionDeadline,
    })),
  ]
    .sort((left, right) => {
      const priorityOrder: Record<PriorityLevel, number> = { critical: 0, warning: 1, info: 2 };
      if (priorityOrder[left.priority] !== priorityOrder[right.priority]) {
        return priorityOrder[left.priority] - priorityOrder[right.priority];
      }

      return new Date(left.timestamp || 0).getTime() - new Date(right.timestamp || 0).getTime();
    })
    .slice(0, 8);

  const totalPipelineValue = sumBy(pipelineStageData, (stage) => stage.totalValue);
  const pipelineConfidence = clampPercent(
    totalPipelineValue > 0 ? ((data.crmOverview?.weightedPipelineValue ?? 0) / totalPipelineValue) * 100 : 0,
  );
  const projectBudgetCoverage = clampPercent(
    (data.projectDashboard?.totalEstimatedBudget ?? 0) > 0
      ? ((data.projectDashboard?.totalApprovedBudget ?? 0) / Math.max(1, data.projectDashboard?.totalEstimatedBudget ?? 0)) * 100
      : 0,
  );
  const procurementPressure = clampPercent(
    (openPurchaseOrderValue + pendingPurchaseRequisitionValue) > 0
      ? (pendingPurchaseRequisitionValue / Math.max(1, openPurchaseOrderValue + pendingPurchaseRequisitionValue)) * 100
      : 0,
  );
  const tenderClosingPressure = clampPercent(
    openTenders.length > 0 ? (closingSoonTenders.length / Math.max(1, openTenders.length)) * 100 : 0,
  );
  const maintenanceHealth = clampPercent(maintenanceCompletionRate);
  const inventoryIssuePressure = clampPercent(
    (data.pendingInventoryIssues.length + data.pendingInventoryApprovals.length) > 0
      ? (data.pendingInventoryIssues.length /
          Math.max(1, data.pendingInventoryIssues.length + data.pendingInventoryApprovals.length)) *
        100
      : 0,
  );

  const headlineCards: HeadlineCardDefinition[] = [
    {
      module: 'crm',
      title: 'Commercial Pipeline',
      description: 'Weighted opportunity and quote exposure',
      value: formatCurrency(data.crmOverview?.weightedPipelineValue ?? 0),
      subtext: `${data.crmOverview?.openOpportunityCount ?? 0} open opportunities • ${data.crmOverview?.activeQuoteCount ?? 0} active quotes`,
      href: '/crm',
      progressLabel: 'Pipeline confidence',
      progressValue: pipelineConfidence,
      progressText: `${Math.round(pipelineConfidence)}% weighted coverage`,
      accentClassName:
        'border-emerald-200 bg-[radial-gradient(circle_at_top_left,_rgba(16,185,129,0.22),_transparent_48%),linear-gradient(135deg,rgba(255,255,255,0.98),rgba(236,253,245,0.96))] dark:border-emerald-900/70 dark:bg-[radial-gradient(circle_at_top_left,_rgba(16,185,129,0.24),_transparent_45%),linear-gradient(135deg,rgba(2,6,23,0.96),rgba(6,78,59,0.26))]',
      iconClassName: 'bg-emerald-500/15 text-emerald-600 dark:text-emerald-300',
      icon: TrendingUp,
    },
    {
      module: 'projects',
      title: 'Project Portfolio',
      description: 'Delivery pressure across active projects',
      value: formatNumber(data.projectDashboard?.activeProjects ?? 0),
      subtext: `${data.projectDashboard?.overdueMilestones ?? 0} overdue milestones • ${data.projectDashboard?.openRisks ?? 0} open risks`,
      href: '/development/projects',
      progressLabel: 'Budget coverage',
      progressValue: projectBudgetCoverage,
      progressText: `${Math.round(projectBudgetCoverage)}% approved vs estimated budget`,
      accentClassName:
        'border-violet-200 bg-[radial-gradient(circle_at_top_left,_rgba(139,92,246,0.22),_transparent_48%),linear-gradient(135deg,rgba(255,255,255,0.98),rgba(245,243,255,0.96))] dark:border-violet-900/70 dark:bg-[radial-gradient(circle_at_top_left,_rgba(139,92,246,0.24),_transparent_45%),linear-gradient(135deg,rgba(2,6,23,0.96),rgba(76,29,149,0.24))]',
      iconClassName: 'bg-violet-500/15 text-violet-600 dark:text-violet-300',
      icon: FolderKanban,
    },
    {
      module: 'procurement',
      title: 'Purchase Orders',
      description: 'Open procurement commitments and approvals',
      value: formatCurrency(openPurchaseOrderValue),
      subtext: `${data.openPurchaseOrders.length} open POs • ${data.pendingPurchaseRequisitions.length} requisitions pending approval`,
      href: '/procurement/purchase-orders',
      progressLabel: 'Approval pressure',
      progressValue: procurementPressure,
      progressText: `${Math.round(procurementPressure)}% of supply exposure still pending approval`,
      accentClassName:
        'border-amber-200 bg-[radial-gradient(circle_at_top_left,_rgba(245,158,11,0.22),_transparent_48%),linear-gradient(135deg,rgba(255,255,255,0.98),rgba(255,251,235,0.96))] dark:border-amber-900/70 dark:bg-[radial-gradient(circle_at_top_left,_rgba(245,158,11,0.24),_transparent_45%),linear-gradient(135deg,rgba(2,6,23,0.96),rgba(120,53,15,0.26))]',
      iconClassName: 'bg-amber-500/15 text-amber-600 dark:text-amber-300',
      icon: ShoppingCart,
    },
    {
      module: 'tenders',
      title: 'Tender Desk',
      description: 'Open tender value and bidder activity',
      value: formatCurrency(tenderEstimatedValue),
      subtext: `${openTenders.length} open tenders • ${tenderBidCount} bids in play`,
      href: '/procurement/tenders',
      progressLabel: 'Closing window',
      progressValue: tenderClosingPressure,
      progressText: `${Math.round(tenderClosingPressure)}% of live tenders close within 14 days`,
      accentClassName:
        'border-sky-200 bg-[radial-gradient(circle_at_top_left,_rgba(14,165,233,0.22),_transparent_48%),linear-gradient(135deg,rgba(255,255,255,0.98),rgba(240,249,255,0.96))] dark:border-sky-900/70 dark:bg-[radial-gradient(circle_at_top_left,_rgba(14,165,233,0.24),_transparent_45%),linear-gradient(135deg,rgba(2,6,23,0.96),rgba(12,74,110,0.24))]',
      iconClassName: 'bg-sky-500/15 text-sky-600 dark:text-sky-300',
      icon: FileText,
    },
    {
      module: 'maintenance',
      title: 'Maintenance Backlog',
      description: 'Active work order load and completion health',
      value: formatNumber(maintenanceSummary?.activeWorkOrders ?? data.maintenanceMetrics?.inProgressWorkOrders ?? 0),
      subtext: `${maintenanceSummary?.overdueWorkOrders ?? data.maintenanceMetrics?.overdueWorkOrders ?? 0} overdue work orders • ${Math.round(maintenanceCompletionRate)}% completion rate`,
      href: '/maintenance',
      progressLabel: 'Completion health',
      progressValue: maintenanceHealth,
      progressText: `${Math.round(maintenanceHealth)}% completed across current planning horizon`,
      accentClassName:
        'border-rose-200 bg-[radial-gradient(circle_at_top_left,_rgba(244,63,94,0.2),_transparent_48%),linear-gradient(135deg,rgba(255,255,255,0.98),rgba(255,241,242,0.96))] dark:border-rose-900/70 dark:bg-[radial-gradient(circle_at_top_left,_rgba(244,63,94,0.22),_transparent_45%),linear-gradient(135deg,rgba(2,6,23,0.96),rgba(127,29,29,0.26))]',
      iconClassName: 'bg-rose-500/15 text-rose-600 dark:text-rose-300',
      icon: Wrench,
    },
    {
      module: 'inventory',
      title: 'Inventory Demand',
      description: 'Issue queues and pending material movement',
      value: formatCurrency(pendingInventoryValue),
      subtext: `${data.pendingInventoryIssues.length} issue queues • ${data.pendingInventoryApprovals.length} approvals waiting`,
      href: '/inventory/requisitions',
      progressLabel: 'Issue pressure',
      progressValue: inventoryIssuePressure,
      progressText: `${Math.round(inventoryIssuePressure)}% of open inventory work is waiting on issue`,
      accentClassName:
        'border-cyan-200 bg-[radial-gradient(circle_at_top_left,_rgba(6,182,212,0.2),_transparent_48%),linear-gradient(135deg,rgba(255,255,255,0.98),rgba(236,254,255,0.96))] dark:border-cyan-900/70 dark:bg-[radial-gradient(circle_at_top_left,_rgba(6,182,212,0.22),_transparent_45%),linear-gradient(135deg,rgba(2,6,23,0.96),rgba(21,94,117,0.26))]',
      iconClassName: 'bg-cyan-500/15 text-cyan-600 dark:text-cyan-300',
      icon: Boxes,
    },
  ];

  const criticalCount = priorityItems.filter((item) => item.priority === 'critical').length;

  const focusCounts: Record<DashboardModuleFocus, number> = {
    all: criticalCount,
    crm: (data.crmOverview?.leadsNeedingFollowUpCount ?? 0) + (data.crmOverview?.atRiskAccountCount ?? 0),
    projects: (data.projectDashboard?.atRiskProjects.length ?? 0) + (data.projectDashboard?.overdueMilestones ?? 0),
    procurement: data.pendingPurchaseRequisitions.length + purchaseOrdersDueSoon.length,
    inventory: data.pendingInventoryApprovals.length + data.pendingInventoryIssues.length,
    maintenance: (maintenanceSummary?.overdueWorkOrders ?? 0) + upcomingMaintenance.length,
    tenders: closingSoonTenders.length + openTenders.length,
  };

  const visiblePriorityItems = focusedModule === 'all'
    ? priorityItems
    : priorityItems.filter((item) => normalizePriorityModule(item.module) === focusedModule);

  const watchlistSections = [
    {
      module: 'crm' as const,
      title: 'CRM Follow-ups',
      href: '/crm',
      empty: 'No immediate CRM follow-ups are due.',
      items: (data.crmOverview?.followUps ?? []).slice(0, 4).map((followUp) => ({
        id: followUp.entityId,
        title: followUp.title,
        detail: followUp.context || followUp.status || 'Follow-up required',
        metaLeft: followUp.entityType || 'CRM',
        metaRight: formatDate(followUp.dueDate),
      })),
    },
    {
      module: 'projects' as const,
      title: 'At-Risk Projects',
      href: '/development/projects',
      empty: 'No at-risk projects are currently flagged.',
      items: (data.projectDashboard?.atRiskProjects ?? []).slice(0, 4).map((project) => ({
        id: project.id,
        title: project.title,
        detail: `${project.openRiskCount} risks • ${project.openIssueCount} issues`,
        metaLeft: `${project.overdueMilestoneCount} overdue milestones`,
        metaRight: formatDate(project.targetEndDate),
      })),
    },
    {
      module: 'procurement' as const,
      title: 'Purchase Orders Due Soon',
      href: '/procurement/purchase-orders',
      empty: 'No open purchase orders currently have near-term delivery dates.',
      items: purchaseOrdersDueSoon.map((order) => ({
        id: order.id,
        title: order.orderNumber,
        detail: order.supplierName,
        metaLeft: formatCurrency(order.totalAmount),
        metaRight: formatDate(order.requiredDate || order.promisedDate),
      })),
    },
    {
      module: 'inventory' as const,
      title: 'Inventory Issue Queue',
      href: '/inventory/requisitions',
      empty: 'No inventory issue queues are waiting right now.',
      items: data.pendingInventoryIssues.slice(0, 4).map((requisition) => ({
        id: requisition.id,
        title: requisition.requisitionNumber,
        detail: `${requisition.warehouseName} • ${requisition.totalItems} items`,
        metaLeft: requisition.status,
        metaRight: formatDate(requisition.requiredDate),
      })),
    },
    {
      module: 'maintenance' as const,
      title: 'Upcoming Maintenance',
      href: '/maintenance/scheduled',
      empty: 'No maintenance schedules are due in the next 14 days.',
      items: upcomingMaintenance.map((schedule) => ({
        id: schedule.id,
        title: schedule.assetName,
        detail: schedule.maintenanceTypeName || schedule.maintenanceType || schedule.name || 'Scheduled maintenance',
        metaLeft: schedule.assignedTechnicianName || 'Unassigned',
        metaRight: formatDate(schedule.nextDue || schedule.nextDueDate || schedule.nextScheduledDate),
      })),
    },
    {
      module: 'tenders' as const,
      title: 'Tenders Closing Soon',
      href: '/procurement/tenders',
      empty: 'No tender submissions are due in the next 14 days.',
      items: closingSoonTenders.map((tender) => ({
        id: tender.id,
        title: tender.tenderNumber,
        detail: tender.title,
        metaLeft: `${tender.bidCount} bids`,
        metaRight: formatDate(tender.submissionDeadline),
      })),
    },
  ];

  const visibleWatchlistSections = focusedModule === 'all'
    ? watchlistSections
    : watchlistSections.filter((section) => section.module === focusedModule);

  const chartViews = [
    {
      key: 'pipeline' as const,
      module: 'crm' as const,
      label: 'Pipeline',
      title: 'CRM Pipeline by Stage',
      description: 'Commercial exposure across active opportunity stages.',
      insightTitle: 'Commercial Drill-Down',
      narrative: 'Where pipeline quality is strongest.',
      primaryHref: '/crm',
      primaryLabel: 'Open CRM',
      secondaryHref: '/crm/opportunities',
      secondaryLabel: 'Open opportunities',
      metrics: [
        { label: 'Weighted pipeline', value: formatCurrency(data.crmOverview?.weightedPipelineValue ?? 0) },
        { label: 'Open opportunities', value: formatNumber(data.crmOverview?.openOpportunityCount ?? 0) },
        { label: 'Active quotes', value: formatNumber(data.crmOverview?.activeQuoteCount ?? 0) },
      ],
      breakdown: pipelineStageData.map((stage) => ({
        label: stage.stage,
        value: formatCurrency(stage.weightedValue),
        detail: `Total ${formatCurrency(stage.totalValue)}`,
        href: '/crm/opportunities',
      })) as ChartBreakdownItem[],
      chart: (
        <BaseBarChart
          data={pipelineStageData}
          xAxisKey="stage"
          bars={[
            { dataKey: 'totalValue', name: 'Total Value', color: CHART_COLORS.success[0] },
            { dataKey: 'weightedValue', name: 'Weighted Value', color: CHART_COLORS.primary[0] },
          ]}
          title="CRM Pipeline by Stage"
          description="Commercial exposure across the active opportunity stages."
          height={360}
          formatValue={(value) => formatCurrency(Number(value))}
          error={getModuleError('CRM Reporting')}
          className="border-slate-200/80 shadow-lg shadow-slate-200/50 dark:border-slate-800/80 dark:shadow-none"
        />
      ),
    },
    {
      key: 'projects' as const,
      module: 'projects' as const,
      label: 'Projects',
      title: 'Project Budget Position',
      description: 'Portfolio comparison of estimated, approved, and actual project spend.',
      insightTitle: 'Delivery Drill-Down',
      narrative: 'Budget position across the live portfolio.',
      primaryHref: '/development/projects',
      primaryLabel: 'Open projects',
      secondaryHref: '/development/projects',
      secondaryLabel: 'Review portfolio',
      metrics: [
        { label: 'Active projects', value: formatNumber(data.projectDashboard?.activeProjects ?? 0) },
        { label: 'Due this month', value: formatNumber(data.projectDashboard?.dueMilestonesThisMonth ?? 0) },
        { label: 'Open risks', value: formatNumber(data.projectDashboard?.openRisks ?? 0) },
      ],
      breakdown: projectBudgetData.map((item) => ({
        label: item.label,
        value: formatCurrency(item.amount),
        detail: item.label === 'Actual' ? 'Current spend' : 'Portfolio budget',
        href: '/development/projects',
      })) as ChartBreakdownItem[],
      chart: (
        <BaseBarChart
          data={projectBudgetData}
          xAxisKey="label"
          bars={[
            { dataKey: 'amount', name: 'Amount', color: CHART_COLORS.primary[1] },
          ]}
          title="Project Budget Position"
          description="Portfolio-level comparison of estimated, approved, and actual project spend."
          height={360}
          formatValue={(value) => formatCurrency(Number(value))}
          error={getModuleError('Projects')}
          className="border-slate-200/80 shadow-lg shadow-slate-200/50 dark:border-slate-800/80 dark:shadow-none"
        />
      ),
    },
    {
      key: 'procurement' as const,
      module: 'procurement' as const,
      label: 'Procurement',
      title: 'Procurement & Tender Exposure',
      description: 'Value tied up in requisitions, purchase orders, tenders, and inventory requests.',
      insightTitle: 'Supply Drill-Down',
      narrative: 'Open value across supply workflows.',
      primaryHref: '/procurement/purchase-orders',
      primaryLabel: 'Open procurement',
      secondaryHref: '/procurement/purchase-requisitions',
      secondaryLabel: 'Open requisitions',
      metrics: [
        { label: 'Open PO value', value: formatCurrency(openPurchaseOrderValue) },
        { label: 'Pending requisitions', value: formatNumber(data.pendingPurchaseRequisitions.length) },
        { label: 'Tender exposure', value: formatCurrency(tenderEstimatedValue) },
      ],
      breakdown: procurementExposureData.map((item) => ({
        label: item.label,
        value: formatCurrency(item.amount),
        detail: 'Outstanding value',
        href: item.label === 'Tenders' ? '/procurement/tenders' : item.label === 'Inventory' ? '/inventory/requisitions' : '/procurement/purchase-orders',
      })) as ChartBreakdownItem[],
      chart: (
        <BaseBarChart
          data={procurementExposureData}
          xAxisKey="label"
          bars={[
            { dataKey: 'amount', name: 'Value', color: CHART_COLORS.warning[0] },
          ]}
          title="Procurement & Tender Exposure"
          description="Value currently tied up in requisitions, purchase orders, tenders, and inventory requests."
          height={360}
          formatValue={(value) => formatCurrency(Number(value))}
          error={getModuleError('Purchase Orders', 'Purchase Requisitions', 'Inventory Issue Queue', 'Tenders')}
          className="border-slate-200/80 shadow-lg shadow-slate-200/50 dark:border-slate-800/80 dark:shadow-none"
        />
      ),
    },
    {
      key: 'maintenance' as const,
      module: 'maintenance' as const,
      label: 'Maintenance',
      title: 'Maintenance Work Order Trend',
      description: 'Created versus completed work across the latest planning horizon.',
      insightTitle: 'Maintenance Drill-Down',
      narrative: 'Created versus completed work.',
      primaryHref: '/maintenance',
      primaryLabel: 'Open maintenance',
      secondaryHref: '/maintenance/scheduled',
      secondaryLabel: 'View schedules',
      metrics: [
        { label: 'Active work orders', value: formatNumber(maintenanceSummary?.activeWorkOrders ?? data.maintenanceMetrics?.inProgressWorkOrders ?? 0) },
        { label: 'Overdue work orders', value: formatNumber(maintenanceSummary?.overdueWorkOrders ?? data.maintenanceMetrics?.overdueWorkOrders ?? 0) },
        { label: 'Completion rate', value: `${Math.round(maintenanceCompletionRate)}%` },
      ],
      breakdown: maintenanceTrendData.map((point) => ({
        label: point.period,
        value: `${formatNumber(point.created)} created`,
        detail: `${formatNumber(point.completed)} completed`,
        href: '/maintenance',
      })) as ChartBreakdownItem[],
      chart: (
        <BaseLineChart
          data={maintenanceTrendData}
          xAxisKey="period"
          lines={[
            { dataKey: 'created', name: 'Created', color: CHART_COLORS.danger[0] },
            { dataKey: 'completed', name: 'Completed', color: CHART_COLORS.success[0] },
          ]}
          title="Maintenance Work Order Trend"
          description="Created versus completed maintenance work over the latest planning horizon."
          height={360}
          formatValue={(value) => formatNumber(Number(value))}
          error={getModuleError('Maintenance Trends')}
          className="border-slate-200/80 shadow-lg shadow-slate-200/50 dark:border-slate-800/80 dark:shadow-none"
        />
      ),
    },
    {
      key: 'tenders' as const,
      module: 'tenders' as const,
      label: 'Tenders',
      title: 'Tender Status Mix',
      description: 'Distribution of live tenders by workflow status.',
      insightTitle: 'Tender Drill-Down',
      narrative: 'Live tender mix by status.',
      primaryHref: '/procurement/tenders',
      primaryLabel: 'Open tenders',
      secondaryHref: '/procurement/tenders',
      secondaryLabel: 'Review deadline desk',
      metrics: [
        { label: 'Open tenders', value: formatNumber(openTenders.length) },
        { label: 'Closing soon', value: formatNumber(closingSoonTenders.length) },
        { label: 'Bid activity', value: formatNumber(tenderBidCount) },
      ],
      breakdown: tenderStatusData.map((item) => ({
        label: item.name,
        value: formatNumber(item.value),
        detail: 'Live tenders',
        href: '/procurement/tenders',
      })) as ChartBreakdownItem[],
      chart: (
        <BasePieChart
          data={tenderStatusData}
          dataKey="value"
          nameKey="name"
          title="Tender Status Mix"
          description="Current distribution of live tenders by workflow status."
          height={360}
          showLabels={false}
          innerRadius={72}
          colors={[CHART_COLORS.info[0], CHART_COLORS.warning[0], CHART_COLORS.success[0], CHART_COLORS.primary[0], CHART_COLORS.danger[0]]}
          error={getModuleError('Tenders')}
          className="border-slate-200/80 shadow-lg shadow-slate-200/50 dark:border-slate-800/80 dark:shadow-none"
        />
      ),
    },
    {
      key: 'queues' as const,
      module: 'inventory' as const,
      label: 'Queues',
      title: 'Operational Queue Load',
      description: 'Cross-module pressure points where work is accumulating fastest.',
      insightTitle: 'Execution Drill-Down',
      narrative: 'Where operational pressure is building.',
      primaryHref: '/inventory/requisitions',
      primaryLabel: 'Open inventory queues',
      secondaryHref: '/crm',
      secondaryLabel: 'Open business queues',
      metrics: [
        { label: 'Critical queue items', value: formatNumber(priorityItems.length) },
        { label: 'Inventory queues', value: formatNumber(data.pendingInventoryApprovals.length + data.pendingInventoryIssues.length) },
        { label: 'Procurement queues', value: formatNumber(data.pendingPurchaseRequisitions.length + data.openPurchaseOrders.length) },
      ],
      breakdown: queueLoadData.map((item) => ({
        label: item.module,
        value: formatNumber(item.items),
        detail: 'Open items',
        href:
          item.module === 'CRM'
            ? '/crm'
            : item.module === 'Projects'
              ? '/development/projects'
              : item.module === 'Procurement'
                ? '/procurement/purchase-orders'
                : item.module === 'Inventory'
                  ? '/inventory/requisitions'
                  : item.module === 'Maintenance'
                    ? '/maintenance'
                    : '/procurement/tenders',
      })) as ChartBreakdownItem[],
      chart: (
        <BaseBarChart
          data={queueLoadData}
          xAxisKey="module"
          bars={[
            { dataKey: 'items', name: 'Open Items', color: CHART_COLORS.neutral[1] },
          ]}
          title="Operational Queue Load"
          description="Cross-module pressure points where work is accumulating fastest."
          height={360}
          formatValue={(value) => formatNumber(Number(value))}
          className="border-slate-200/80 shadow-lg shadow-slate-200/50 dark:border-slate-800/80 dark:shadow-none"
        />
      ),
    },
  ];

  const availableChartViews = chartViews.filter(
    (chart) => focusedModule === 'all' || chart.module === focusedModule || (focusedModule !== 'all' && chart.key === 'queues'),
  );
  const selectedChart =
    availableChartViews.find((chart) => chart.key === activeChartView) ??
    availableChartViews[0] ??
    chartViews[0];
  const secondaryCharts = chartViews
    .filter(
      (chart) =>
        chart.key !== selectedChart.key &&
        (focusedModule === 'all' || chart.module === focusedModule || chart.key === 'queues'),
    )
    .slice(0, 3);

  return (
    <DashboardLayout>
      <div className="space-y-8">
        <section className="overflow-hidden rounded-[30px] border border-slate-200/80 bg-[radial-gradient(circle_at_top_left,_rgba(59,130,246,0.12),_transparent_30%),radial-gradient(circle_at_top_right,_rgba(16,185,129,0.12),_transparent_28%),linear-gradient(135deg,rgba(255,255,255,0.96),rgba(248,250,252,0.98))] p-6 shadow-[0_24px_60px_-24px_rgba(15,23,42,0.25)] dark:border-slate-800/80 dark:bg-[radial-gradient(circle_at_top_left,_rgba(59,130,246,0.18),_transparent_30%),radial-gradient(circle_at_top_right,_rgba(16,185,129,0.14),_transparent_28%),linear-gradient(135deg,rgba(2,6,23,0.96),rgba(15,23,42,0.98))]">
          <div className="grid gap-6 xl:grid-cols-[minmax(0,1.15fr),360px]">
            <div className="space-y-6">
              <div className="flex flex-wrap items-center gap-2">
                <Badge variant="outline" className="border-slate-300 bg-white/70 text-slate-700 dark:border-slate-700 dark:bg-slate-950/40 dark:text-slate-300">
                  {criticalCount} critical items
                </Badge>
                <Badge variant="outline" className="border-amber-200 bg-amber-50/80 text-amber-700 dark:border-amber-800 dark:bg-amber-950/30 dark:text-amber-300">
                  {closingSoonTenders.length} tenders closing soon
                </Badge>
                <Badge variant="outline" className="border-rose-200 bg-rose-50/80 text-rose-700 dark:border-rose-800 dark:bg-rose-950/30 dark:text-rose-300">
                  {maintenanceSummary?.overdueWorkOrders ?? 0} maintenance overdue
                </Badge>
              </div>

              <div className="space-y-3">
                <div className="flex items-center gap-3">
                  <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-blue-600 text-white shadow-lg shadow-blue-600/30">
                    <Activity className="h-5 w-5" />
                  </div>
                  <div>
                    <p className="text-sm font-medium uppercase tracking-[0.24em] text-slate-500 dark:text-slate-400">Enterprise Command</p>
                    <h1 className="text-3xl font-bold tracking-tight text-slate-950 dark:text-slate-50">Dashboard</h1>
                  </div>
                </div>
                <p className="max-w-2xl text-sm leading-6 text-slate-600 dark:text-slate-300">
                  Welcome back, {displayName}. Focus the view, scan the signals, and jump straight into the queue that needs action.
                </p>
              </div>

              <div className="flex flex-wrap items-center gap-3">
                <Badge variant="secondary" className="rounded-full px-3 py-1">
                  Updated {formatRelativeTime(data.lastUpdated)}
                </Badge>
                <Button variant="outline" size="sm" onClick={() => refetch()} disabled={isFetching} className="rounded-full bg-white/80 dark:bg-slate-950/40">
                  <RefreshCw className={`mr-2 h-4 w-4 ${isFetching ? 'animate-spin' : ''}`} />
                  Refresh live data
                </Button>
              </div>

              <div className="grid gap-3 sm:grid-cols-3">
                <div className="rounded-2xl border border-white/70 bg-white/70 p-4 backdrop-blur dark:border-slate-800/70 dark:bg-slate-950/35">
                  <p className="text-xs font-semibold uppercase tracking-[0.2em] text-slate-500 dark:text-slate-400">Pipeline</p>
                  <p className="mt-3 text-2xl font-semibold text-slate-950 dark:text-slate-50">
                    {formatCurrency(data.crmOverview?.weightedPipelineValue ?? 0)}
                  </p>
                  <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">
                    {data.crmOverview?.openOpportunityCount ?? 0} open opportunities in play
                  </p>
                </div>
                <div className="rounded-2xl border border-white/70 bg-white/70 p-4 backdrop-blur dark:border-slate-800/70 dark:bg-slate-950/35">
                  <p className="text-xs font-semibold uppercase tracking-[0.2em] text-slate-500 dark:text-slate-400">Delivery</p>
                  <p className="mt-3 text-2xl font-semibold text-slate-950 dark:text-slate-50">
                    {formatNumber(data.projectDashboard?.activeProjects ?? 0)}
                  </p>
                  <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">
                    {data.projectDashboard?.overdueMilestones ?? 0} overdue milestones across the portfolio
                  </p>
                </div>
                <div className="rounded-2xl border border-white/70 bg-white/70 p-4 backdrop-blur dark:border-slate-800/70 dark:bg-slate-950/35">
                  <p className="text-xs font-semibold uppercase tracking-[0.2em] text-slate-500 dark:text-slate-400">Supply</p>
                  <p className="mt-3 text-2xl font-semibold text-slate-950 dark:text-slate-50">
                    {formatCurrency(openPurchaseOrderValue + pendingInventoryValue)}
                  </p>
                  <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">
                    Value currently flowing through procurement and inventory queues
                  </p>
                </div>
              </div>
            </div>

            <div className="rounded-[26px] border border-white/70 bg-white/80 p-5 shadow-inner backdrop-blur dark:border-slate-800/70 dark:bg-slate-950/45">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[0.2em] text-slate-500 dark:text-slate-400">Module Focus</p>
                  <h2 className="mt-2 text-lg font-semibold text-slate-950 dark:text-slate-50">Choose your operating lens</h2>
                </div>
                <Badge variant="outline" className="rounded-full">
                  Live
                </Badge>
              </div>
              <div className="mt-5 grid gap-3">
                {FOCUS_OPTIONS.map((option) => (
                  <button
                    key={option.key}
                    type="button"
                    onClick={() => focusModule(option.key)}
                    className={cn(
                      'group rounded-2xl border px-4 py-3 text-left transition-all',
                      focusedModule === option.key
                        ? 'border-slate-900 bg-slate-950 text-white shadow-lg shadow-slate-900/20 dark:border-slate-100 dark:bg-slate-100 dark:text-slate-950'
                        : 'border-slate-200 bg-white/70 hover:border-slate-300 hover:bg-white dark:border-slate-800 dark:bg-slate-950/35 dark:hover:border-slate-700 dark:hover:bg-slate-950/60',
                    )}
                  >
                    <div className="flex items-center justify-between gap-3">
                      <div className="min-w-0">
                        <p className="font-medium">{option.label}</p>
                        <p className={cn('mt-1 text-xs uppercase tracking-[0.18em]', focusedModule === option.key ? 'text-slate-200/80 dark:text-slate-700' : 'text-slate-500 dark:text-slate-400')}>
                          {option.shortLabel}
                        </p>
                      </div>
                      <div className={cn('flex h-9 min-w-9 items-center justify-center rounded-full px-2 text-sm font-semibold', focusedModule === option.key ? 'bg-white/15 dark:bg-slate-900/10' : 'bg-slate-100 text-slate-700 dark:bg-slate-900 dark:text-slate-200')}>
                        {formatNumber(focusCounts[option.key])}
                      </div>
                    </div>
                  </button>
                ))}
              </div>
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

        <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">
          {headlineCards.map((card) => {
            const Icon = card.icon;
            const isFocused = focusedModule === card.module;

            return (
              <Card
                key={card.title}
                className={cn(
                  'group relative overflow-hidden border shadow-[0_18px_42px_-26px_rgba(15,23,42,0.35)] transition-all duration-200 hover:-translate-y-0.5 hover:shadow-[0_22px_46px_-24px_rgba(15,23,42,0.45)] dark:shadow-none',
                  card.accentClassName,
                  isFocused && 'ring-2 ring-slate-900/10 dark:ring-white/15',
                )}
              >
                <button type="button" onClick={() => focusModule(card.module)} className="absolute inset-0 z-10" aria-label={`Focus ${card.title}`} />
                <CardHeader className="relative z-20 pb-3">
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <CardTitle className="text-base">{card.title}</CardTitle>
                      <CardDescription className="mt-1 text-xs uppercase tracking-[0.18em] text-slate-500 dark:text-slate-400">
                        {FOCUS_OPTIONS.find((option) => option.key === card.module)?.shortLabel}
                      </CardDescription>
                  </div>
                  <div className={cn('flex h-12 w-12 items-center justify-center rounded-2xl', card.iconClassName)}>
                    <Icon className="h-5 w-5" />
                  </div>
                  </div>
                </CardHeader>
                <CardContent className="relative z-20 space-y-4">
                  <div className="flex items-end justify-between gap-3">
                    <div className="text-3xl font-bold tracking-tight text-slate-950 dark:text-slate-50">{card.value}</div>
                    <Badge variant="secondary" className="rounded-full bg-white/70 dark:bg-slate-900/60">
                      {FOCUS_OPTIONS.find((option) => option.key === card.module)?.shortLabel}
                    </Badge>
                  </div>
                  <p className="text-sm text-slate-600 dark:text-slate-300">{card.subtext}</p>
                  <div className="space-y-2">
                    <div className="flex items-center justify-between text-[11px] font-medium uppercase tracking-[0.18em] text-slate-500 dark:text-slate-400">
                      <span>{card.progressLabel}</span>
                      <span>{Math.round(card.progressValue)}%</span>
                    </div>
                    <Progress value={card.progressValue} className="h-2.5 bg-white/70 dark:bg-slate-900/55" />
                  </div>
                  <div className="flex items-center justify-between">
                    <span className="text-xs text-slate-500 dark:text-slate-400">{card.description}</span>
                    <Link href={card.href} className="relative z-30">
                      <Button variant="ghost" size="sm" className="h-8 rounded-full px-2">
                        Open
                        <ChevronRight className="ml-1 h-4 w-4" />
                      </Button>
                    </Link>
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </div>

        <section className="space-y-5">
          <div className="flex flex-col gap-4 xl:flex-row xl:items-end xl:justify-between">
            <div className="space-y-2">
              <div className="flex flex-wrap items-center gap-2">
                <Badge variant="outline" className="border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-800 dark:bg-blue-950/30 dark:text-blue-300">
                  Interactive analytics
                </Badge>
                <Badge variant="outline" className="border-slate-300 text-slate-700 dark:border-slate-700 dark:text-slate-300">
                  Real module data only
                </Badge>
              </div>
              <div>
                <h2 className="text-2xl font-semibold tracking-tight">Operations Intelligence</h2>
                <p className="text-sm text-muted-foreground">
                  Use the filters to pivot from enterprise overview into a specific commercial, delivery, supply, or maintenance story.
                </p>
              </div>
            </div>

            <div className="flex flex-wrap gap-2">
              {FOCUS_OPTIONS.map((option) => (
                <Button
                  key={option.key}
                  type="button"
                  variant={focusedModule === option.key ? 'default' : 'outline'}
                  size="sm"
                  className="rounded-full"
                  onClick={() => focusModule(option.key)}
                >
                  {option.shortLabel}
                  <span className="ml-2 rounded-full bg-black/10 px-2 py-0.5 text-[11px] dark:bg-white/10">
                    {formatNumber(focusCounts[option.key])}
                  </span>
                </Button>
              ))}
            </div>
          </div>

          <div className="grid gap-6 xl:grid-cols-[minmax(0,1.35fr),360px]">
            <div className="space-y-4">
              <div className="rounded-[26px] border border-slate-200/80 bg-white/80 p-4 shadow-[0_20px_48px_-26px_rgba(15,23,42,0.35)] backdrop-blur dark:border-slate-800/80 dark:bg-slate-950/50 dark:shadow-none">
                <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                  <div className="space-y-2">
                    <p className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500 dark:text-slate-400">
                      {selectedChart.insightTitle}
                    </p>
                    <div>
                      <h3 className="text-xl font-semibold text-slate-950 dark:text-slate-50">{selectedChart.title}</h3>
                      <p className="mt-1 max-w-2xl text-sm text-slate-600 dark:text-slate-300">{selectedChart.description}</p>
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Link href={selectedChart.primaryHref}>
                      <Button size="sm" className="rounded-full">
                        {selectedChart.primaryLabel}
                        <ArrowRight className="ml-2 h-4 w-4" />
                      </Button>
                    </Link>
                    <Link href={selectedChart.secondaryHref}>
                      <Button variant="outline" size="sm" className="rounded-full">
                        {selectedChart.secondaryLabel}
                      </Button>
                    </Link>
                  </div>
                </div>

                <div className="mt-4">
                  <Tabs value={selectedChart.key} onValueChange={(value) => setActiveChartView(value as DashboardChartView)}>
                    <TabsList className="h-auto flex-wrap justify-start rounded-2xl bg-slate-100/90 p-1 dark:bg-slate-900/80">
                      {availableChartViews.map((chart) => (
                        <TabsTrigger key={chart.key} value={chart.key} className="rounded-xl px-4 py-2">
                          {chart.label}
                        </TabsTrigger>
                      ))}
                    </TabsList>
                  </Tabs>
                </div>
              </div>

              {selectedChart.chart}
            </div>

            <Card className="border-slate-200/80 bg-white/80 shadow-[0_20px_48px_-26px_rgba(15,23,42,0.35)] backdrop-blur dark:border-slate-800/80 dark:bg-slate-950/50 dark:shadow-none">
              <CardHeader>
                <CardTitle>{selectedChart.insightTitle}</CardTitle>
                <CardDescription>{selectedChart.narrative}</CardDescription>
              </CardHeader>
              <CardContent className="space-y-6">
                <div className="grid gap-3">
                  {selectedChart.metrics.map((metric) => (
                    <div key={metric.label} className="rounded-2xl border border-slate-200/80 bg-slate-50/80 p-4 dark:border-slate-800/80 dark:bg-slate-900/60">
                      <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500 dark:text-slate-400">{metric.label}</p>
                      <p className="mt-2 text-2xl font-semibold text-slate-950 dark:text-slate-50">{metric.value}</p>
                    </div>
                  ))}
                </div>

                <div className="space-y-3">
                  <div className="flex items-center justify-between">
                    <p className="text-sm font-semibold uppercase tracking-[0.18em] text-slate-500 dark:text-slate-400">Key Slices</p>
                    <Badge variant="outline" className="rounded-full">
                      {selectedChart.breakdown.length} slices
                    </Badge>
                  </div>
                  <div className="space-y-2">
                    {selectedChart.breakdown.slice(0, 6).map((item) => (
                      <Link key={`${selectedChart.key}-${item.label}`} href={item.href} className="block rounded-2xl border border-slate-200/80 px-4 py-3 transition-colors hover:bg-slate-50 dark:border-slate-800/80 dark:hover:bg-slate-900">
                        <div className="flex items-start justify-between gap-3">
                          <div>
                            <p className="font-medium text-slate-950 dark:text-slate-50">{item.label}</p>
                            <p className="mt-1 text-sm text-slate-600 dark:text-slate-300">{item.detail}</p>
                          </div>
                          <div className="text-right">
                            <p className="font-semibold text-slate-950 dark:text-slate-50">{item.value}</p>
                            <span className="mt-1 inline-flex items-center text-xs text-slate-500 dark:text-slate-400">
                              View
                              <ChevronRight className="ml-1 h-3 w-3" />
                            </span>
                          </div>
                        </div>
                      </Link>
                    ))}
                  </div>
                </div>
              </CardContent>
            </Card>
          </div>

          {secondaryCharts.length > 0 && (
            <div className="grid gap-6 xl:grid-cols-3">
              {secondaryCharts.map((chart) => (
                <div key={chart.key} className="space-y-2">
                  {chart.chart}
                </div>
              ))}
            </div>
          )}
        </section>

        <div className="grid gap-6 xl:grid-cols-[1.2fr,0.8fr]">
          <Card className="border-slate-200/80 shadow-[0_20px_48px_-26px_rgba(15,23,42,0.35)] dark:border-slate-800/80 dark:shadow-none">
            <CardHeader>
              <CardTitle>Critical Operations Queue</CardTitle>
              <CardDescription>
                {focusedModule === 'all'
                  ? 'Cross-module items that need the fastest business attention right now.'
                  : `Focused escalation queue for ${FOCUS_OPTIONS.find((option) => option.key === focusedModule)?.label}.`}
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              {visiblePriorityItems.length > 0 ? (
                visiblePriorityItems.map((item) => (
                  <Link
                    key={item.id}
                    href={item.href}
                    className={`block rounded-xl border p-4 transition-colors hover:bg-slate-50 dark:hover:bg-slate-900 ${getPriorityTone(item.priority)}`}
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <div className="flex items-center gap-2">
                          <Badge variant="outline">{item.module}</Badge>
                          <span className="text-xs uppercase tracking-wide">{item.priority}</span>
                        </div>
                        <p className="mt-2 font-medium">{item.title}</p>
                        <p className="mt-1 text-sm opacity-90">{item.detail}</p>
                      </div>
                      <div className="text-right text-xs opacity-80">
                        {formatRelativeTime(item.timestamp)}
                      </div>
                    </div>
                  </Link>
                ))
              ) : (
                <div className="rounded-xl border border-dashed border-slate-200 p-8 text-center dark:border-slate-800">
                  <CheckCircle2 className="mx-auto mb-3 h-10 w-10 text-emerald-600" />
                  <p className="font-medium">No urgent cross-module escalations</p>
                  <p className="mt-2 text-sm text-muted-foreground">
                    {focusedModule === 'all'
                      ? 'The live queues currently do not show urgent CRM, delivery, procurement, inventory, maintenance, or tender escalations.'
                      : `The current ${FOCUS_OPTIONS.find((option) => option.key === focusedModule)?.shortLabel?.toLowerCase()} view does not show urgent escalations right now.`}
                  </p>
                </div>
              )}
            </CardContent>
          </Card>

          <Card className="border-slate-200/80 shadow-[0_20px_48px_-26px_rgba(15,23,42,0.35)] dark:border-slate-800/80 dark:shadow-none">
            <CardHeader>
              <CardTitle>Business Watchlist</CardTitle>
              <CardDescription>
                {focusedModule === 'all'
                  ? 'Near-term deadlines and pressure points.'
                  : `${FOCUS_OPTIONS.find((option) => option.key === focusedModule)?.label} watchlist.`}
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-6">
              {visibleWatchlistSections.map((section) => (
                <div key={section.module} className="space-y-3">
                  <div className="flex items-center justify-between">
                    <h3 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">{section.title}</h3>
                    <Link href={section.href}>
                      <Button variant="ghost" size="sm" className="h-auto px-0 text-xs">
                        View all <ArrowRight className="ml-1 h-3 w-3" />
                      </Button>
                    </Link>
                  </div>
                  {section.items.length > 0 ? (
                    section.items.map((item) => (
                      <div key={`${section.module}-${item.id}`} className="rounded-lg border border-slate-200 px-3 py-3 dark:border-slate-800">
                        <p className="font-medium">{item.title}</p>
                        <p className="mt-1 text-sm text-muted-foreground line-clamp-1">{item.detail}</p>
                        <div className="mt-2 flex items-center justify-between text-xs text-muted-foreground">
                          <span>{item.metaLeft}</span>
                          <span>{item.metaRight}</span>
                        </div>
                      </div>
                    ))
                  ) : (
                    <p className="text-sm text-muted-foreground">{section.empty}</p>
                  )}
                </div>
              ))}
            </CardContent>
          </Card>
        </div>

        <Card>
          <CardHeader>
            <CardTitle>Module Pulse</CardTitle>
            <CardDescription>Quick module status and entry points.</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            <div className="rounded-xl border border-slate-200 p-4 dark:border-slate-800">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium">CRM</p>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {data.crmOverview?.qualifiedLeadCount ?? 0} qualified • {data.crmOverview?.leadsNeedingFollowUpCount ?? 0} due
                  </p>
                </div>
                <Badge variant="outline" className={getModuleBadgeTone(isModuleLive('CRM Overview', 'CRM Reporting'))}>
                  {isModuleLive('CRM Overview', 'CRM Reporting') ? 'Live' : 'Unavailable'}
                </Badge>
              </div>
              <div className="mt-4">
                <Link href="/crm">
                  <Button variant="ghost" size="sm" className="px-0">
                    Open CRM <ArrowRight className="ml-2 h-4 w-4" />
                  </Button>
                </Link>
              </div>
            </div>

            <div className="rounded-xl border border-slate-200 p-4 dark:border-slate-800">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium">Projects</p>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {data.projectDashboard?.dueMilestonesThisMonth ?? 0} due • {data.projectDashboard?.atRiskProjects.length ?? 0} at risk
                  </p>
                </div>
                <Badge variant="outline" className={getModuleBadgeTone(isModuleLive('Projects'))}>
                  {isModuleLive('Projects') ? 'Live' : 'Unavailable'}
                </Badge>
              </div>
              <div className="mt-4">
                <Link href="/development/projects">
                  <Button variant="ghost" size="sm" className="px-0">
                    Open Projects <ArrowRight className="ml-2 h-4 w-4" />
                  </Button>
                </Link>
              </div>
            </div>

            <div className="rounded-xl border border-slate-200 p-4 dark:border-slate-800">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium">Procurement</p>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {data.pendingPurchaseRequisitions.length} pending • {data.openPurchaseOrders.length} open POs
                  </p>
                </div>
                <Badge variant="outline" className={getModuleBadgeTone(isModuleLive('Purchase Requisitions', 'Purchase Orders'))}>
                  {isModuleLive('Purchase Requisitions', 'Purchase Orders') ? 'Live' : 'Unavailable'}
                </Badge>
              </div>
              <div className="mt-4">
                <Link href="/procurement/purchase-orders">
                  <Button variant="ghost" size="sm" className="px-0">
                    Open Procurement <ArrowRight className="ml-2 h-4 w-4" />
                  </Button>
                </Link>
              </div>
            </div>

            <div className="rounded-xl border border-slate-200 p-4 dark:border-slate-800">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium">Inventory</p>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {data.pendingInventoryApprovals.length} approvals • {data.pendingInventoryIssues.length} issue queues
                  </p>
                </div>
                <Badge variant="outline" className={getModuleBadgeTone(isModuleLive('Inventory Approval Queue', 'Inventory Issue Queue'))}>
                  {isModuleLive('Inventory Approval Queue', 'Inventory Issue Queue') ? 'Live' : 'Unavailable'}
                </Badge>
              </div>
              <div className="mt-4">
                <Link href="/inventory/requisitions">
                  <Button variant="ghost" size="sm" className="px-0">
                    Open Inventory <ArrowRight className="ml-2 h-4 w-4" />
                  </Button>
                </Link>
              </div>
            </div>

            <div className="rounded-xl border border-slate-200 p-4 dark:border-slate-800">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium">Maintenance</p>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {maintenanceSummary?.activeWorkOrders ?? data.maintenanceMetrics?.inProgressWorkOrders ?? 0} active • {maintenanceSummary?.overdueWorkOrders ?? data.maintenanceMetrics?.overdueWorkOrders ?? 0} overdue
                  </p>
                </div>
                <Badge variant="outline" className={getModuleBadgeTone(isModuleLive('Maintenance Overview', 'Maintenance Metrics', 'Maintenance Trends'))}>
                  {isModuleLive('Maintenance Overview', 'Maintenance Metrics', 'Maintenance Trends') ? 'Live' : 'Unavailable'}
                </Badge>
              </div>
              <div className="mt-4">
                <Link href="/maintenance">
                  <Button variant="ghost" size="sm" className="px-0">
                    Open Maintenance <ArrowRight className="ml-2 h-4 w-4" />
                  </Button>
                </Link>
              </div>
            </div>

            <div className="rounded-xl border border-slate-200 p-4 dark:border-slate-800">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium">Tenders</p>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {openTenders.length} live • {closingSoonTenders.length} closing soon
                  </p>
                </div>
                <Badge variant="outline" className={getModuleBadgeTone(isModuleLive('Tenders'))}>
                  {isModuleLive('Tenders') ? 'Live' : 'Unavailable'}
                </Badge>
              </div>
              <div className="mt-4">
                <Link href="/procurement/tenders">
                  <Button variant="ghost" size="sm" className="px-0">
                    Open Tenders <ArrowRight className="ml-2 h-4 w-4" />
                  </Button>
                </Link>
              </div>
            </div>
          </CardContent>
        </Card>

        {unavailableModules.length > 0 && (
          <Card>
            <CardHeader>
              <CardTitle>Module Availability</CardTitle>
              <CardDescription>Modules that did not return live data during the latest dashboard refresh.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              {unavailableModules.map((module) => (
                <div key={module.module} className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-3 dark:border-amber-900 dark:bg-amber-950/30">
                  <div className="flex items-center justify-between gap-3">
                    <p className="font-medium text-amber-800 dark:text-amber-200">{module.module}</p>
                    <Badge variant="outline" className="border-amber-200 text-amber-700 dark:border-amber-800 dark:text-amber-300">
                      Degraded
                    </Badge>
                  </div>
                  {module.error && (
                    <p className="mt-1 text-xs text-amber-700/90 dark:text-amber-300/90">{module.error}</p>
                  )}
                </div>
              ))}
            </CardContent>
          </Card>
        )}
      </div>
    </DashboardLayout>
  );
}
