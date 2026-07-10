'use client';

import { useMemo, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, BarChart3, Clock, Download, Plus, RefreshCcw, Star, TrendingUp } from 'lucide-react';
import { Bar, BarChart, CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  buildScopedHelpdeskNewPath,
  buildScopedHelpdeskQueuePath,
  buildScopedHelpdeskTicketsPath,
  getHelpdeskScopeConfig,
  isExternalTicketSource,
  isTicketInHelpdeskScope,
} from '@/lib/helpdesk-scope';
import {
  ehcInternalTicketService,
  type EhcHelpdeskSummary,
  type EhcSlaCompliancePoint,
  type EhcAgentPerformanceRow,
  type EhcEscalationReportRow,
  type EhcFeedbackSummary,
  type EhcFeedbackByAgentRow,
  type EhcFeedbackByDepartmentRow,
  type EhcFeedbackTrendPoint,
  type EhcProblemsSummary,
  type EhcProblemLinkTrendPoint,
  type EhcTopRecurringProblem,
} from '@/services/ehcInternalTicketService';
import type { EhcTicketListItem, EhcTicketPriority } from '@/services/ehcTicketService';

type CountRow = { key: string; count: number };

type TableCellProps<T> = {
  row: {
    original: T;
  };
};

const priorityBadgeClassName = (p: EhcTicketPriority) => {
  switch (p) {
    case 'Critical':
      return 'bg-red-600 text-white hover:bg-red-600/90 dark:bg-red-500 dark:hover:bg-red-500/90';
    case 'High':
      return 'bg-orange-600 text-white hover:bg-orange-600/90 dark:bg-orange-500 dark:hover:bg-orange-500/90';
    case 'Medium':
      return 'bg-yellow-400 text-slate-900 hover:bg-yellow-400/90 dark:bg-yellow-500 dark:text-slate-900 dark:hover:bg-yellow-500/90';
    case 'Low':
      return 'bg-green-600 text-white hover:bg-green-600/90 dark:bg-green-500 dark:hover:bg-green-500/90';
    default:
      return 'bg-slate-600 text-white hover:bg-slate-600/90 dark:bg-slate-500 dark:hover:bg-slate-500/90';
  }
};

const problemStatusBadgeClassName = (s: string) => {
  switch (s) {
    case 'Resolved':
      return 'bg-green-600 text-white hover:bg-green-600/90 dark:bg-green-500 dark:hover:bg-green-500/90';
    case 'Closed':
      return 'bg-slate-600 text-white hover:bg-slate-600/90 dark:bg-slate-500 dark:hover:bg-slate-500/90';
    default:
      return 'bg-blue-600 text-white hover:bg-blue-600/90 dark:bg-blue-500 dark:hover:bg-blue-500/90';
  }
};

export default function HelpdeskDashboardPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const qc = useQueryClient();
  const [rangeDays, setRangeDays] = useState(30);
  const scopeParam = searchParams?.get('scope');
  const scopeConfig = useMemo(() => getHelpdeskScopeConfig(scopeParam), [scopeParam]);
  const isScopedDashboard = Boolean(scopeParam);
  const scopedTicketTypeFilter = scopeConfig.allowedTicketTypes.length === 1 ? scopeConfig.defaultTicketType : null;
  const scopedSourceFilter = scopeConfig.internalOnly ? 'Internal' : null;

  const dateStamp = new Date().toISOString().slice(0, 10);

  const downloadReport = async (relativePath: string, fileName: string) => {
    const baseUrl = process.env.NEXT_PUBLIC_API_URL || '/api';
    const token = typeof window !== 'undefined' ? localStorage.getItem('authToken') : null;

    const res = await fetch(`${baseUrl}${relativePath}`, {
      method: 'GET',
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    });

    if (!res.ok) throw new Error(`Export failed (${res.status})`);

    const blob = await res.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  };

  // --- data queries ---
  const { data: summary, isLoading: summaryLoading, error: summaryError } = useQuery({
    queryKey: ['ehc', 'internal', 'reports', 'summary'],
    queryFn: () => ehcInternalTicketService.getSummary(),
    enabled: !isScopedDashboard,
  });

  const { data: scopedTickets, isLoading: scopedTicketsLoading, error: scopedTicketsError } = useQuery({
    queryKey: ['ehc', 'internal', 'reports', 'summary', 'scoped', scopeConfig.scope],
    queryFn: async () => {
      const items = await ehcInternalTicketService.listTickets(1, 1000, {
        ticketType: scopedTicketTypeFilter,
        source: scopedSourceFilter,
      });
      return items.filter(
        (ticket) =>
          isTicketInHelpdeskScope(ticket, scopeConfig.scope) &&
          (scopeConfig.internalOnly || isExternalTicketSource(ticket.source)),
      );
    },
    enabled: isScopedDashboard,
  });

  const { data: sla, isLoading: slaLoading } = useQuery({
    queryKey: ['ehc', 'internal', 'reports', 'sla', rangeDays],
    queryFn: () => ehcInternalTicketService.getSlaCompliance(rangeDays),
    enabled: !isScopedDashboard,
  });

  const { data: agentPerf, isLoading: agentLoading, error: agentError } = useQuery({
    queryKey: ['ehc', 'internal', 'reports', 'agents', rangeDays],
    queryFn: () => ehcInternalTicketService.getAgentPerformance(rangeDays),
    enabled: !isScopedDashboard,
  });

  const { data: escalations, isLoading: escLoading, error: escError } = useQuery({
    queryKey: ['ehc', 'internal', 'reports', 'escalations', rangeDays],
    queryFn: () => ehcInternalTicketService.getEscalations(rangeDays),
    enabled: !isScopedDashboard,
  });

  const { data: feedbackSummary } = useQuery({
    queryKey: ['ehc', 'internal', 'reports', 'feedback', 'summary', rangeDays],
    queryFn: () => ehcInternalTicketService.getFeedbackSummary(rangeDays),
    enabled: !isScopedDashboard,
  });

  const { data: feedbackByAgent } = useQuery({
    queryKey: ['ehc', 'internal', 'reports', 'feedback', 'by-agent', rangeDays],
    queryFn: () => ehcInternalTicketService.getFeedbackByAgent(rangeDays),
    enabled: !isScopedDashboard,
  });

  const { data: feedbackByDepartment } = useQuery({
    queryKey: ['ehc', 'internal', 'reports', 'feedback', 'by-department', rangeDays],
    queryFn: () => ehcInternalTicketService.getFeedbackByDepartment(rangeDays),
    enabled: !isScopedDashboard,
  });

  const { data: feedbackTrend } = useQuery({
    queryKey: ['ehc', 'internal', 'reports', 'feedback', 'trend', rangeDays],
    queryFn: () => ehcInternalTicketService.getFeedbackTrend(rangeDays),
    enabled: !isScopedDashboard,
  });

  const { data: problemsSummary, isLoading: problemsLoading } = useQuery({
    queryKey: ['ehc', 'internal', 'reports', 'problems', 'summary', rangeDays, 15],
    queryFn: () => ehcInternalTicketService.getProblemsSummary(rangeDays, 15),
    enabled: !isScopedDashboard,
  });

  const { data: problemLinkTrend, isLoading: problemsTrendLoading } = useQuery({
    queryKey: ['ehc', 'internal', 'reports', 'problems', 'link-trend', rangeDays],
    queryFn: () => ehcInternalTicketService.getProblemLinkTrend(rangeDays),
    enabled: !isScopedDashboard,
  });

  // --- derived view models ---
  const totals = useMemo(() => {
    if (!isScopedDashboard) return summary?.totals ?? null;

    const tickets = (scopedTickets ?? []) as EhcTicketListItem[];
    const terminalStatuses = new Set(['Resolved', 'Closed']);
    const now = Date.now();
    let firstResponseBreaches = 0;
    let resolutionBreaches = 0;

    for (const ticket of tickets) {
      const firstResponseDue = ticket.firstResponseDueAt ? Date.parse(ticket.firstResponseDueAt) : Number.NaN;
      const resolutionDue = ticket.resolutionDueAt ? Date.parse(ticket.resolutionDueAt) : Number.NaN;
      const firstRespondedAt = ticket.firstRespondedAt ? Date.parse(ticket.firstRespondedAt) : Number.NaN;
      const resolvedAt = ticket.resolvedAt ? Date.parse(ticket.resolvedAt) : Number.NaN;
      const closedAt = ticket.closedAt ? Date.parse(ticket.closedAt) : Number.NaN;
      const isTerminal = terminalStatuses.has(ticket.status);

      if (!isTerminal && !Number.isNaN(firstResponseDue) && now > firstResponseDue && Number.isNaN(firstRespondedAt)) {
        firstResponseBreaches += 1;
      }

      if (!isTerminal && !Number.isNaN(resolutionDue) && now > resolutionDue && Number.isNaN(resolvedAt) && Number.isNaN(closedAt)) {
        resolutionBreaches += 1;
      }
    }

    return {
      total: tickets.length,
      open: tickets.filter((ticket) => !terminalStatuses.has(ticket.status)).length,
      firstResponseBreaches,
      resolutionBreaches,
      avgFirstResponseMinutes: null,
      avgResolutionMinutes: null,
    };
  }, [isScopedDashboard, scopedTickets, summary]);

  const byStatus = useMemo<CountRow[]>(() => {
    if (isScopedDashboard) {
      const counts = new Map<string, number>();
      for (const ticket of scopedTickets ?? []) counts.set(ticket.status, (counts.get(ticket.status) ?? 0) + 1);
      return Array.from(counts.entries()).map(([key, count]) => ({ key, count }));
    }
    const items = (summary?.byStatus ?? []) as EhcHelpdeskSummary['byStatus'];
    return items.map((x) => ({ key: x.status, count: x.count }));
  }, [isScopedDashboard, scopedTickets, summary]);

  const byPriority = useMemo<CountRow[]>(() => {
    if (isScopedDashboard) {
      const counts = new Map<string, number>();
      for (const ticket of scopedTickets ?? []) counts.set(ticket.priority, (counts.get(ticket.priority) ?? 0) + 1);
      return Array.from(counts.entries()).map(([key, count]) => ({ key, count }));
    }
    const items = (summary?.byPriority ?? []) as EhcHelpdeskSummary['byPriority'];
    return items.map((x) => ({ key: x.priority, count: x.count }));
  }, [isScopedDashboard, scopedTickets, summary]);

  const byDepartment = useMemo<CountRow[]>(() => {
    if (isScopedDashboard) {
      const counts = new Map<string, number>();
      for (const ticket of scopedTickets ?? []) {
        const key = ticket.assignedDepartmentName || 'Unassigned';
        counts.set(key, (counts.get(key) ?? 0) + 1);
      }
      return Array.from(counts.entries()).map(([key, count]) => ({ key, count }));
    }
    const items = (summary?.byDepartment ?? []) as EhcHelpdeskSummary['byDepartment'];
    return items.map((x) => ({ key: x.departmentName || 'Unassigned', count: x.count }));
  }, [isScopedDashboard, scopedTickets, summary]);

  const byCategory = useMemo<CountRow[]>(() => {
    if (isScopedDashboard) {
      const counts = new Map<string, number>();
      for (const ticket of scopedTickets ?? []) {
        const key = ticket.categoryName || 'Uncategorized';
        counts.set(key, (counts.get(key) ?? 0) + 1);
      }
      return Array.from(counts.entries()).map(([key, count]) => ({ key, count }));
    }
    const items = (summary?.byCategory ?? []) as EhcHelpdeskSummary['byCategory'];
    return items.map((x) => ({ key: x.categoryName || 'Uncategorized', count: x.count }));
  }, [isScopedDashboard, scopedTickets, summary]);

  const byRootCause = useMemo<CountRow[]>(() => {
    if (isScopedDashboard) return [];
    const items = (summary?.byRootCause ?? []) as NonNullable<EhcHelpdeskSummary['byRootCause']>;
    return items.map((x) => ({ key: x.rootCauseName || 'Unspecified', count: x.count }));
  }, [isScopedDashboard, summary]);

  const byChannel = useMemo<CountRow[]>(() => {
    if (!isScopedDashboard) return [];
    const counts = new Map<string, number>();
    for (const ticket of scopedTickets ?? []) {
      const key =
        ticket.source === 'Web'
          ? 'Website'
          : ticket.source === 'PhoneCall'
            ? 'Phone Call'
            : ticket.source === 'Sms'
              ? 'SMS'
              : ticket.source;
      counts.set(key, (counts.get(key) ?? 0) + 1);
    }
    return Array.from(counts.entries()).map(([key, count]) => ({ key, count }));
  }, [isScopedDashboard, scopedTickets]);

  const countColumns = useMemo<Array<DataTableColumn<CountRow>>>(() => {
    return [
      { id: 'key', header: 'Name', accessorKey: 'key' },
      {
        id: 'count',
        header: 'Count',
        accessorKey: 'count',
        cell: ({ row }: TableCellProps<CountRow>) => <Badge variant="secondary">{row.original.count.toLocaleString()}</Badge>,
      },
    ];
  }, []);

  const overviewLoading = isScopedDashboard ? scopedTicketsLoading : summaryLoading;
  const overviewError = isScopedDashboard ? (scopedTicketsError ? 'Failed to load dashboard.' : null) : (summaryError ? 'Failed to load summary.' : null);

  const slaChartData = useMemo(() => {
    const points = (sla ?? []) as EhcSlaCompliancePoint[];
    return points.map((p) => ({
      date: p.date.slice(5),
      firstResponse: p.firstResponseCompliancePercent ?? null,
      resolution: p.resolutionCompliancePercent ?? null,
    }));
  }, [sla]);

  const agentColumns = useMemo<Array<DataTableColumn<EhcAgentPerformanceRow>>>(() => {
    return [
      { id: 'agentName', header: 'Agent', accessorKey: 'agentName' },
      {
        id: 'totalAssigned',
        header: 'Assigned',
        accessorKey: 'totalAssigned',
        cell: ({ row }: TableCellProps<EhcAgentPerformanceRow>) => <Badge variant="secondary">{row.original.totalAssigned.toLocaleString()}</Badge>,
      },
      {
        id: 'openAssigned',
        header: 'Open',
        accessorKey: 'openAssigned',
        cell: ({ row }: TableCellProps<EhcAgentPerformanceRow>) => <span className="text-sm">{row.original.openAssigned.toLocaleString()}</span>,
      },
      {
        id: 'resolvedAssigned',
        header: 'Resolved',
        accessorKey: 'resolvedAssigned',
        cell: ({ row }: TableCellProps<EhcAgentPerformanceRow>) => <span className="text-sm">{row.original.resolvedAssigned.toLocaleString()}</span>,
      },
      {
        id: 'firstResponseBreaches',
        header: 'FR Breaches',
        accessorKey: 'firstResponseBreaches',
        cell: ({ row }: TableCellProps<EhcAgentPerformanceRow>) => <span className="text-sm">{row.original.firstResponseBreaches.toLocaleString()}</span>,
      },
      {
        id: 'resolutionBreaches',
        header: 'Res Breaches',
        accessorKey: 'resolutionBreaches',
        cell: ({ row }: TableCellProps<EhcAgentPerformanceRow>) => <span className="text-sm">{row.original.resolutionBreaches.toLocaleString()}</span>,
      },
      {
        id: 'avgFirstResponseMinutes',
        header: 'Avg FR (min)',
        accessorKey: 'avgFirstResponseMinutes',
        cell: ({ row }: TableCellProps<EhcAgentPerformanceRow>) => <span className="text-sm">{row.original.avgFirstResponseMinutes ?? '—'}</span>,
      },
      {
        id: 'avgResolutionMinutes',
        header: 'Avg Res (min)',
        accessorKey: 'avgResolutionMinutes',
        cell: ({ row }: TableCellProps<EhcAgentPerformanceRow>) => <span className="text-sm">{row.original.avgResolutionMinutes ?? '—'}</span>,
      },
    ];
  }, []);

  const escalationColumns = useMemo<Array<DataTableColumn<EhcEscalationReportRow>>>(() => {
    return [
      { id: 'policyName', header: 'Policy', accessorKey: 'policyName' },
      { id: 'trigger', header: 'Trigger', accessorKey: 'trigger' },
      { id: 'level', header: 'Level', accessorKey: 'level' },
      {
        id: 'count',
        header: 'Count',
        accessorKey: 'count',
        cell: ({ row }: TableCellProps<EhcEscalationReportRow>) => <Badge variant="secondary">{row.original.count.toLocaleString()}</Badge>,
      },
    ];
  }, []);

  const ratingDistData = useMemo(() => {
    const dist = (feedbackSummary?.ratingDistribution ?? {}) as EhcFeedbackSummary['ratingDistribution'];
    return [1, 2, 3, 4, 5].map((r) => ({ rating: String(r), count: Number((dist as any)[r] ?? 0) }));
  }, [feedbackSummary?.ratingDistribution]);

  const feedbackTrendData = useMemo(() => {
    const points = (feedbackTrend ?? []) as EhcFeedbackTrendPoint[];
    return points.map((p) => ({
      date: p.date.slice(5),
      avgRating: p.avgRating ?? null,
      feedbackCount: p.feedbackCount,
    }));
  }, [feedbackTrend]);

  const feedbackAgentColumns = useMemo<Array<DataTableColumn<EhcFeedbackByAgentRow>>>(() => {
    return [
      { id: 'agentName', header: 'Agent', accessorKey: 'agentName' },
      {
        id: 'avgRating',
        header: 'Avg Rating',
        accessorKey: 'avgRating',
        cell: ({ row }: TableCellProps<EhcFeedbackByAgentRow>) => <Badge variant="secondary">{row.original.avgRating ?? '—'}</Badge>,
      },
      {
        id: 'feedbackCount',
        header: 'Responses',
        accessorKey: 'feedbackCount',
        cell: ({ row }: TableCellProps<EhcFeedbackByAgentRow>) => <span className="text-sm">{row.original.feedbackCount.toLocaleString()}</span>,
      },
    ];
  }, []);

  const feedbackDepartmentColumns = useMemo<Array<DataTableColumn<EhcFeedbackByDepartmentRow>>>(() => {
    return [
      { id: 'departmentName', header: 'Department', accessorKey: 'departmentName' },
      {
        id: 'avgRating',
        header: 'Avg Rating',
        accessorKey: 'avgRating',
        cell: ({ row }: TableCellProps<EhcFeedbackByDepartmentRow>) => <Badge variant="secondary">{row.original.avgRating ?? '—'}</Badge>,
      },
      {
        id: 'feedbackCount',
        header: 'Responses',
        accessorKey: 'feedbackCount',
        cell: ({ row }: TableCellProps<EhcFeedbackByDepartmentRow>) => <span className="text-sm">{row.original.feedbackCount.toLocaleString()}</span>,
      },
    ];
  }, []);

  const problemLinkTrendData = useMemo(() => {
    const points = (problemLinkTrend ?? []) as EhcProblemLinkTrendPoint[];
    return points.map((p) => ({
      date: p.date.slice(5),
      linkedTickets: p.linkedTickets,
      distinctProblems: p.distinctProblems,
    }));
  }, [problemLinkTrend]);

  const problemTopColumns = useMemo<Array<DataTableColumn<EhcTopRecurringProblem>>>(() => {
    return [
      {
        id: 'problemNumber',
        header: 'Problem #',
        accessorKey: 'problemNumber',
        cell: ({ row }: TableCellProps<EhcTopRecurringProblem>) => (
          <button className="text-blue-700 hover:underline" onClick={() => router.push(`/helpdesk/problems/${row.original.problemId}`)} title="Open problem">
            {row.original.problemNumber}
          </button>
        ),
      },
      { id: 'title', header: 'Title', accessorKey: 'title' },
      {
        id: 'status',
        header: 'Status',
        accessorKey: 'status',
        cell: ({ row }: TableCellProps<EhcTopRecurringProblem>) => <Badge className={problemStatusBadgeClassName(row.original.status)}>{row.original.status}</Badge>,
      },
      {
        id: 'priority',
        header: 'Priority',
        accessorKey: 'priority',
        cell: ({ row }: TableCellProps<EhcTopRecurringProblem>) => <Badge className={priorityBadgeClassName(row.original.priority)}>{row.original.priority}</Badge>,
      },
      {
        id: 'linkedTicketsCount',
        header: 'Incidents',
        accessorKey: 'linkedTicketsCount',
        cell: ({ row }: TableCellProps<EhcTopRecurringProblem>) => <Badge variant="secondary">{row.original.linkedTicketsCount.toLocaleString()}</Badge>,
      },
      { id: 'departmentName', header: 'Department', accessorKey: 'departmentName' },
      { id: 'ownerName', header: 'Owner', accessorKey: 'ownerName' },
    ];
  }, [router]);

  const problemByStatus = useMemo<CountRow[]>(() => {
    const s = (problemsSummary?.byStatus ?? []) as EhcProblemsSummary['byStatus'];
    return s.map((x) => ({ key: x.status, count: x.count }));
  }, [problemsSummary]);

  const problemByPriority = useMemo<CountRow[]>(() => {
    const s = (problemsSummary?.byPriority ?? []) as EhcProblemsSummary['byPriority'];
    return s.map((x) => ({ key: x.priority, count: x.count }));
  }, [problemsSummary]);

  const problemByDepartment = useMemo<CountRow[]>(() => {
    const s = (problemsSummary?.byDepartment ?? []) as EhcProblemsSummary['byDepartment'];
    return s.map((x) => ({ key: x.departmentName || 'Unassigned', count: x.count }));
  }, [problemsSummary]);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <h1 className="text-3xl font-bold text-slate-900">
            {isScopedDashboard ? `${scopeConfig.listTitle} Dashboard` : 'Helpdesk Dashboard'}
          </h1>
          <p className="text-slate-600">
            {isScopedDashboard
              ? `Backoffice overview for ${scopeConfig.listTitle.toLowerCase()}.`
              : 'Operational overview, SLA compliance, CSAT, and recurring issues.'}
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {isScopedDashboard ? (
            <>
              <Button type="button" variant="outline" onClick={() => router.push(buildScopedHelpdeskTicketsPath(scopeConfig.scope))}>
                Open Worklist
              </Button>
              <Button type="button" variant="outline" onClick={() => router.push(buildScopedHelpdeskQueuePath(scopeConfig.scope))}>
                Queue
              </Button>
              <Button type="button" onClick={() => router.push(buildScopedHelpdeskNewPath(scopeConfig.scope))}>
                <Plus className="h-4 w-4 mr-2" />
                {scopeConfig.createButtonLabel}
              </Button>
            </>
          ) : (
            <>
              <label className="text-sm text-slate-600">Range</label>
              <select className="h-9 rounded-md border border-input bg-background px-3 text-sm" value={String(rangeDays)} onChange={(e) => setRangeDays(Number(e.target.value) || 30)}>
                <option value="7">Last 7 days</option>
                <option value="30">Last 30 days</option>
                <option value="90">Last 90 days</option>
              </select>
            </>
          )}
          <Button
            type="button"
            variant="outline"
            onClick={() => qc.invalidateQueries({ queryKey: isScopedDashboard ? ['ehc', 'internal', 'reports', 'summary', 'scoped', scopeConfig.scope] : ['ehc', 'internal', 'reports'] })}
            title="Refresh reports"
          >
            <RefreshCcw className="h-4 w-4 mr-2" />
            Refresh
          </Button>
        </div>
      </div>

      <Tabs defaultValue="overview">
        <TabsList className="flex flex-wrap h-auto">
          <TabsTrigger value="overview">Overview</TabsTrigger>
          {!isScopedDashboard ? <TabsTrigger value="sla">SLA</TabsTrigger> : null}
          {!isScopedDashboard ? <TabsTrigger value="ops">Operations</TabsTrigger> : null}
          {!isScopedDashboard ? <TabsTrigger value="csat">CSAT</TabsTrigger> : null}
          {!isScopedDashboard ? <TabsTrigger value="recurring">Recurring issues</TabsTrigger> : null}
        </TabsList>

        <TabsContent value="overview" className="space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="text-sm text-slate-600">
              {isScopedDashboard ? 'Current backoffice branch totals and breakdowns.' : 'All-time totals & breakdowns.'}
            </div>
            {!isScopedDashboard ? (
              <div className="flex flex-wrap items-center gap-2">
                <Button type="button" variant="outline" onClick={() => downloadReport('/ehc/internal/reports/export/summary.xlsx', `ehc-summary-${dateStamp}.xlsx`)}>
                  <Download className="h-4 w-4 mr-2" />
                  Export Excel
                </Button>
                <Button type="button" variant="outline" onClick={() => downloadReport('/ehc/internal/reports/export/summary.pdf', `ehc-summary-${dateStamp}.pdf`)}>
                  <Download className="h-4 w-4 mr-2" />
                  Export PDF
                </Button>
              </div>
            ) : null}
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm text-slate-600 flex items-center gap-2">
                  <Clock className="h-4 w-4" />
                  Open Tickets
                </CardTitle>
              </CardHeader>
              <CardContent className="pt-0">
                <div className="text-2xl font-bold">{totals ? totals.open.toLocaleString() : '—'}</div>
                <div className="text-xs text-slate-500">of {totals ? totals.total.toLocaleString() : '—'} total</div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm text-slate-600 flex items-center gap-2">
                  <AlertTriangle className="h-4 w-4" />
                  FR Breaches
                </CardTitle>
              </CardHeader>
              <CardContent className="pt-0">
                <div className="text-2xl font-bold">{totals ? totals.firstResponseBreaches.toLocaleString() : '—'}</div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm text-slate-600 flex items-center gap-2">
                  <AlertTriangle className="h-4 w-4" />
                  Resolution Breaches
                </CardTitle>
              </CardHeader>
              <CardContent className="pt-0">
                <div className="text-2xl font-bold">{totals ? totals.resolutionBreaches.toLocaleString() : '—'}</div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm text-slate-600 flex items-center gap-2">
                  <Clock className="h-4 w-4" />
                  Avg Times (min)
                </CardTitle>
              </CardHeader>
              <CardContent className="pt-0 text-sm text-slate-700 space-y-1">
                <div className="flex items-center justify-between">
                  <span>First response</span>
                  <span className="font-medium">{totals?.avgFirstResponseMinutes ?? '—'}</span>
                </div>
                <div className="flex items-center justify-between">
                  <span>Resolution</span>
                  <span className="font-medium">{totals?.avgResolutionMinutes ?? '—'}</span>
                </div>
              </CardContent>
            </Card>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
            <DataTable
              compact
              title="By Status"
              data={byStatus}
              columns={countColumns}
              loading={overviewLoading}
              error={overviewError}
              enablePagination={false}
              enableSearch={false}
              enableColumnFilters={false}
              exportFileName={`helpdesk-by-status-${dateStamp}`}
              exportFormats={['csv', 'excel']}
            />

            <DataTable
              compact
              title="By Priority"
              data={byPriority}
              columns={countColumns}
              loading={overviewLoading}
              error={overviewError}
              enablePagination={false}
              enableSearch={false}
              enableColumnFilters={false}
              exportFileName={`helpdesk-by-priority-${dateStamp}`}
              exportFormats={['csv', 'excel']}
            />
          </div>

          <div className={`grid grid-cols-1 ${isScopedDashboard ? 'lg:grid-cols-3' : 'lg:grid-cols-3'} gap-4`}>
            <DataTable
              compact
              title="Top Categories"
              data={byCategory}
              columns={countColumns}
              loading={overviewLoading}
              error={overviewError}
              enablePagination={false}
              enableSearch={false}
              enableColumnFilters={false}
              exportFileName={`helpdesk-by-category-${dateStamp}`}
              exportFormats={['csv', 'excel']}
            />

            {isScopedDashboard ? (
              <DataTable
                compact
                title="By Channel"
                data={byChannel}
                columns={countColumns}
                loading={overviewLoading}
                error={overviewError}
                enablePagination={false}
                enableSearch={false}
                enableColumnFilters={false}
                exportFileName={`helpdesk-by-channel-${dateStamp}`}
                exportFormats={['csv', 'excel']}
              />
            ) : (
              <DataTable
                compact
                title="Top Root Causes"
                data={byRootCause}
                columns={countColumns}
                loading={overviewLoading}
                error={overviewError}
                enablePagination={false}
                enableSearch={false}
                enableColumnFilters={false}
                exportFileName={`helpdesk-by-root-cause-${dateStamp}`}
                exportFormats={['csv', 'excel']}
              />
            )}

            <DataTable
              compact
              title="By Department"
              data={byDepartment}
              columns={countColumns}
              loading={overviewLoading}
              error={overviewError}
              enablePagination={false}
              enableSearch={false}
              enableColumnFilters={false}
              exportFileName={`helpdesk-by-department-${dateStamp}`}
              exportFormats={['csv', 'excel']}
            />
          </div>
        </TabsContent>

        <TabsContent value="sla" className="space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="text-sm text-slate-600">SLA compliance for the selected range.</div>
            <div className="flex flex-wrap items-center gap-2">
              <Button
                type="button"
                variant="outline"
                onClick={() => downloadReport(`/ehc/internal/reports/export/sla-compliance.xlsx?days=${rangeDays}`, `ehc-sla-compliance-${rangeDays}d-${dateStamp}.xlsx`)}
              >
                <Download className="h-4 w-4 mr-2" />
                Export Excel
              </Button>
              <Button
                type="button"
                variant="outline"
                onClick={() => downloadReport(`/ehc/internal/reports/export/sla-compliance.pdf?days=${rangeDays}`, `ehc-sla-compliance-${rangeDays}d-${dateStamp}.pdf`)}
              >
                <Download className="h-4 w-4 mr-2" />
                Export PDF
              </Button>
            </div>
          </div>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm text-slate-600 flex items-center gap-2">
                <BarChart3 className="h-4 w-4" />
                SLA Compliance
              </CardTitle>
              <CardDescription>First response and resolution compliance percentages.</CardDescription>
            </CardHeader>
            <CardContent className="pt-0">
              <div className="h-[280px]">
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={slaChartData}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="date" tick={{ fontSize: 12 }} />
                    <YAxis domain={[0, 100]} tick={{ fontSize: 12 }} />
                    <Tooltip />
                    <Legend />
                    <Line type="monotone" dataKey="firstResponse" name="First response %" stroke="#2563eb" strokeWidth={2} dot={false} isAnimationActive={false} />
                    <Line type="monotone" dataKey="resolution" name="Resolution %" stroke="#16a34a" strokeWidth={2} dot={false} isAnimationActive={false} />
                  </LineChart>
                </ResponsiveContainer>
              </div>
              {slaLoading ? <div className="text-xs text-slate-500 mt-2">Loading SLA chart…</div> : null}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="ops" className="space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="text-sm text-slate-600">Agent performance and escalation volume for the selected range.</div>
            <div className="flex flex-wrap items-center gap-2">
              <Button
                type="button"
                variant="outline"
                onClick={() => downloadReport(`/ehc/internal/reports/export/agent-performance.xlsx?days=${rangeDays}`, `ehc-agent-performance-${rangeDays}d-${dateStamp}.xlsx`)}
              >
                <Download className="h-4 w-4 mr-2" />
                Export Agents
              </Button>
              <Button
                type="button"
                variant="outline"
                onClick={() => downloadReport(`/ehc/internal/reports/export/escalations.xlsx?days=${rangeDays}`, `ehc-escalations-${rangeDays}d-${dateStamp}.xlsx`)}
              >
                <Download className="h-4 w-4 mr-2" />
                Export Escalations
              </Button>
            </div>
          </div>

          <DataTable
            compact
            title={`Agent Performance (Last ${rangeDays} days)`}
            data={agentPerf ?? []}
            columns={agentColumns}
            loading={agentLoading}
            error={agentError ? 'Failed to load agent performance.' : null}
            enablePagination={false}
            enableSearch={true}
            enableColumnFilters={false}
            exportFileName={`helpdesk-agent-performance-${rangeDays}d-${dateStamp}`}
            exportFormats={['csv', 'excel']}
          />

          <DataTable
            compact
            title={`Escalations (Last ${rangeDays} days)`}
            data={escalations ?? []}
            columns={escalationColumns}
            loading={escLoading}
            error={escError ? 'Failed to load escalations.' : null}
            enablePagination={false}
            enableSearch={true}
            enableColumnFilters={false}
            exportFileName={`helpdesk-escalations-${rangeDays}d-${dateStamp}`}
            exportFormats={['csv', 'excel']}
          />
        </TabsContent>

        <TabsContent value="csat" className="space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="text-sm text-slate-600">Customer satisfaction for the selected range.</div>
            <div className="flex flex-wrap items-center gap-2">
              <Button
                type="button"
                variant="outline"
                onClick={() => downloadReport(`/ehc/internal/reports/export/feedback-summary.xlsx?days=${rangeDays}`, `ehc-feedback-summary-${rangeDays}d-${dateStamp}.xlsx`)}
              >
                <Download className="h-4 w-4 mr-2" />
                Export Summary
              </Button>
              <Button
                type="button"
                variant="outline"
                onClick={() => downloadReport(`/ehc/internal/reports/export/feedback-by-agent.xlsx?days=${rangeDays}`, `ehc-csat-by-agent-${rangeDays}d-${dateStamp}.xlsx`)}
              >
                <Download className="h-4 w-4 mr-2" />
                Export by Agent
              </Button>
              <Button
                type="button"
                variant="outline"
                onClick={() => downloadReport(`/ehc/internal/reports/export/feedback-by-department.xlsx?days=${rangeDays}`, `ehc-csat-by-department-${rangeDays}d-${dateStamp}.xlsx`)}
              >
                <Download className="h-4 w-4 mr-2" />
                Export by Dept
              </Button>
            </div>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-4 gap-4">
            <Card className="lg:col-span-2">
              <CardHeader className="p-4 pb-2">
                <CardTitle className="flex items-center gap-2">
                  <Star className="h-4 w-4" />
                  CSAT (last {rangeDays} days)
                </CardTitle>
              </CardHeader>
              <CardContent className="p-4 pt-0">
                <div className="grid grid-cols-2 gap-3 text-sm">
                  <div className="rounded-md border bg-white p-3">
                    <div className="text-slate-500">Average rating</div>
                    <div className="text-2xl font-bold text-slate-900">{feedbackSummary?.avgRating ?? '—'}</div>
                  </div>
                  <div className="rounded-md border bg-white p-3">
                    <div className="text-slate-500">Response rate</div>
                    <div className="text-2xl font-bold text-slate-900">
                      {feedbackSummary?.responseRatePercent != null ? `${feedbackSummary.responseRatePercent}%` : '—'}
                    </div>
                    <div className="text-xs text-slate-500 mt-1">
                      {feedbackSummary ? `${feedbackSummary.feedbackCount} responses / ${feedbackSummary.resolvedOrClosedTickets} resolved` : ''}
                    </div>
                  </div>
                </div>

                <div className="mt-4 grid grid-cols-1 xl:grid-cols-2 gap-4">
                  <div className="rounded-md border bg-white p-3">
                    <div className="text-sm font-medium text-slate-900 mb-2">Rating distribution</div>
                    <div className="h-[220px]">
                      <ResponsiveContainer width="100%" height="100%">
                        <BarChart data={ratingDistData}>
                          <CartesianGrid strokeDasharray="3 3" />
                          <XAxis dataKey="rating" />
                          <YAxis allowDecimals={false} />
                          <Tooltip />
                          <Bar dataKey="count" fill="#2563EB" />
                        </BarChart>
                      </ResponsiveContainer>
                    </div>
                  </div>

                  <div className="rounded-md border bg-white p-3">
                    <div className="text-sm font-medium text-slate-900 mb-2">Trend</div>
                    <div className="h-[220px]">
                      <ResponsiveContainer width="100%" height="100%">
                        <LineChart data={feedbackTrendData}>
                          <CartesianGrid strokeDasharray="3 3" />
                          <XAxis dataKey="date" />
                          <YAxis domain={[1, 5]} />
                          <Tooltip />
                          <Legend />
                          <Line type="monotone" dataKey="avgRating" stroke="#16A34A" strokeWidth={2} name="Avg Rating" dot={false} />
                        </LineChart>
                      </ResponsiveContainer>
                    </div>
                  </div>
                </div>
              </CardContent>
            </Card>

            <Card className="lg:col-span-2">
              <CardHeader className="p-4 pb-2">
                <CardTitle>CSAT by Agent</CardTitle>
              </CardHeader>
              <CardContent className="p-4 pt-0">
                <DataTable
                  compact
                  data={(feedbackByAgent ?? []) as any}
                  columns={feedbackAgentColumns}
                  loading={false}
                  enableColumnFilters={false}
                  enableExport={true}
                  exportFileName={`ehc-csat-by-agent-${rangeDays}d-${dateStamp}`}
                  exportFormats={['csv', 'excel']}
                  emptyStateMessage="No CSAT responses yet."
                />
              </CardContent>
            </Card>

            <Card className="lg:col-span-2">
              <CardHeader className="p-4 pb-2">
                <CardTitle>CSAT by Department</CardTitle>
              </CardHeader>
              <CardContent className="p-4 pt-0">
                <DataTable
                  compact
                  data={(feedbackByDepartment ?? []) as any}
                  columns={feedbackDepartmentColumns}
                  loading={false}
                  enableColumnFilters={false}
                  enableExport={true}
                  exportFileName={`ehc-csat-by-department-${rangeDays}d-${dateStamp}`}
                  exportFormats={['csv', 'excel']}
                  emptyStateMessage="No CSAT responses yet."
                />
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <TabsContent value="recurring" className="space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="text-sm text-slate-600">Recurring issues and incident volume for the selected range.</div>
            <Button type="button" variant="outline" onClick={() => router.push('/helpdesk/problems')}>
              View problems
            </Button>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm text-slate-600 flex items-center gap-2">
                  <TrendingUp className="h-4 w-4" />
                  Open problems
                </CardTitle>
              </CardHeader>
              <CardContent className="pt-0">
                <div className="text-2xl font-bold">{problemsSummary ? problemsSummary.openProblems.toLocaleString() : '—'}</div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm text-slate-600 flex items-center gap-2">
                  <TrendingUp className="h-4 w-4" />
                  Total problems
                </CardTitle>
              </CardHeader>
              <CardContent className="pt-0">
                <div className="text-2xl font-bold">{problemsSummary ? problemsSummary.totalProblems.toLocaleString() : '—'}</div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm text-slate-600 flex items-center gap-2">
                  <TrendingUp className="h-4 w-4" />
                  Linked incidents
                </CardTitle>
                <CardDescription>Last {rangeDays} days</CardDescription>
              </CardHeader>
              <CardContent className="pt-0">
                <div className="text-2xl font-bold">{problemsSummary ? problemsSummary.linkedTicketsLastDays.toLocaleString() : '—'}</div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm text-slate-600 flex items-center gap-2">
                  <TrendingUp className="h-4 w-4" />
                  New problems
                </CardTitle>
                <CardDescription>Last {rangeDays} days</CardDescription>
              </CardHeader>
              <CardContent className="pt-0">
                <div className="text-2xl font-bold">{problemsSummary ? problemsSummary.problemsCreatedLastDays.toLocaleString() : '—'}</div>
              </CardContent>
            </Card>
          </div>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm text-slate-600 flex items-center gap-2">
                <BarChart3 className="h-4 w-4" />
                Incident volume (linked tickets)
              </CardTitle>
              <CardDescription>How many tickets were linked to problems each day.</CardDescription>
            </CardHeader>
            <CardContent className="pt-0">
              <div className="h-[280px]">
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={problemLinkTrendData}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="date" tick={{ fontSize: 12 }} />
                    <YAxis allowDecimals={false} tick={{ fontSize: 12 }} />
                    <Tooltip />
                    <Legend />
                    <Line type="monotone" dataKey="linkedTickets" name="Linked tickets" stroke="#2563eb" strokeWidth={2} dot={false} isAnimationActive={false} />
                    <Line type="monotone" dataKey="distinctProblems" name="Distinct problems" stroke="#16a34a" strokeWidth={2} dot={false} isAnimationActive={false} />
                  </LineChart>
                </ResponsiveContainer>
              </div>
              {problemsTrendLoading ? <div className="text-xs text-slate-500 mt-2">Loading trend…</div> : null}
            </CardContent>
          </Card>

          <DataTable
            compact
            title="Top recurring problems"
            data={(problemsSummary?.topRecurring ?? []) as any}
            columns={problemTopColumns}
            loading={problemsLoading}
            enablePagination={false}
            enableSearch={true}
            enableColumnFilters={false}
            enableExport={true}
            exportFileName={`ehc-top-recurring-problems-${rangeDays}d-${dateStamp}`}
            exportFormats={['csv', 'excel']}
            emptyStateMessage="No linked problems yet."
          />

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
            <DataTable
              compact
              title="Problems by Status"
              data={problemByStatus}
              columns={countColumns}
              loading={problemsLoading}
              enablePagination={false}
              enableSearch={false}
              enableColumnFilters={false}
              exportFileName={`ehc-problems-by-status-${rangeDays}d-${dateStamp}`}
              exportFormats={['csv', 'excel']}
            />
            <DataTable
              compact
              title="Problems by Priority"
              data={problemByPriority}
              columns={countColumns}
              loading={problemsLoading}
              enablePagination={false}
              enableSearch={false}
              enableColumnFilters={false}
              exportFileName={`ehc-problems-by-priority-${rangeDays}d-${dateStamp}`}
              exportFormats={['csv', 'excel']}
            />
            <DataTable
              compact
              title="Problems by Department"
              data={problemByDepartment}
              columns={countColumns}
              loading={problemsLoading}
              enablePagination={false}
              enableSearch={false}
              enableColumnFilters={false}
              exportFileName={`ehc-problems-by-department-${rangeDays}d-${dateStamp}`}
              exportFormats={['csv', 'excel']}
            />
          </div>
        </TabsContent>
      </Tabs>

      <div className="text-xs text-slate-500">
        Exports use the current range for SLA/Operations/CSAT; Overview exports are all-time.
      </div>
    </div>
  );
}
