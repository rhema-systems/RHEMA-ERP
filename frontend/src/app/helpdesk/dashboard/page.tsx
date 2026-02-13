'use client';

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { BarChart3, Clock, AlertTriangle, Download } from 'lucide-react';
import {
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable';
import {
  ehcInternalTicketService,
  type EhcHelpdeskSummary,
  type EhcSlaCompliancePoint,
  type EhcAgentPerformanceRow,
  type EhcEscalationReportRow,
} from '@/services/ehcInternalTicketService';

type CountRow = { key: string; count: number };

export default function HelpdeskDashboardPage() {
  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['ehc', 'internal', 'reports', 'summary'],
    queryFn: () => ehcInternalTicketService.getSummary(),
  });

  const { data: sla, isLoading: slaLoading } = useQuery({
    queryKey: ['ehc', 'internal', 'reports', 'sla', 30],
    queryFn: () => ehcInternalTicketService.getSlaCompliance(30),
  });

  const { data: agentPerf, isLoading: agentLoading, error: agentError } = useQuery({
    queryKey: ['ehc', 'internal', 'reports', 'agents', 30],
    queryFn: () => ehcInternalTicketService.getAgentPerformance(30),
  });

  const { data: escalations, isLoading: escLoading, error: escError } = useQuery({
    queryKey: ['ehc', 'internal', 'reports', 'escalations', 30],
    queryFn: () => ehcInternalTicketService.getEscalations(30),
  });

  const totals = data?.totals ?? null;

  const downloadReport = async (relativePath: string, fileName: string) => {
    const baseUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';
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

  const byStatus = useMemo<CountRow[]>(() => {
    const items = (data?.byStatus ?? []) as EhcHelpdeskSummary['byStatus'];
    return items.map((x) => ({ key: x.status, count: x.count }));
  }, [data]);

  const byPriority = useMemo<CountRow[]>(() => {
    const items = (data?.byPriority ?? []) as EhcHelpdeskSummary['byPriority'];
    return items.map((x) => ({ key: x.priority, count: x.count }));
  }, [data]);

  const byDepartment = useMemo<CountRow[]>(() => {
    const items = (data?.byDepartment ?? []) as EhcHelpdeskSummary['byDepartment'];
    return items.map((x) => ({ key: x.departmentName || 'Unassigned', count: x.count }));
  }, [data]);

  const byCategory = useMemo<CountRow[]>(() => {
    const items = (data?.byCategory ?? []) as EhcHelpdeskSummary['byCategory'];
    return items.map((x) => ({ key: x.categoryName || 'Uncategorized', count: x.count }));
  }, [data]);

  const byRootCause = useMemo<CountRow[]>(() => {
    const items = (data?.byRootCause ?? []) as NonNullable<EhcHelpdeskSummary['byRootCause']>;
    return items.map((x) => ({ key: x.rootCauseName || 'Unspecified', count: x.count }));
  }, [data]);

  const columns = useMemo<Array<DataTableColumn<CountRow>>>(() => {
    return [
      { id: 'key', header: 'Name', accessorKey: 'key' },
      {
        id: 'count',
        header: 'Count',
        accessorKey: 'count',
        cell: ({ row }) => <Badge variant="secondary">{row.original.count.toLocaleString()}</Badge>,
      },
    ];
  }, []);

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
        cell: ({ row }) => <Badge variant="secondary">{row.original.totalAssigned.toLocaleString()}</Badge>,
      },
      {
        id: 'openAssigned',
        header: 'Open',
        accessorKey: 'openAssigned',
        cell: ({ row }) => <span className="text-sm">{row.original.openAssigned.toLocaleString()}</span>,
      },
      {
        id: 'resolvedAssigned',
        header: 'Resolved',
        accessorKey: 'resolvedAssigned',
        cell: ({ row }) => <span className="text-sm">{row.original.resolvedAssigned.toLocaleString()}</span>,
      },
      {
        id: 'firstResponseBreaches',
        header: 'FR Breaches',
        accessorKey: 'firstResponseBreaches',
        cell: ({ row }) => <span className="text-sm">{row.original.firstResponseBreaches.toLocaleString()}</span>,
      },
      {
        id: 'resolutionBreaches',
        header: 'Res Breaches',
        accessorKey: 'resolutionBreaches',
        cell: ({ row }) => <span className="text-sm">{row.original.resolutionBreaches.toLocaleString()}</span>,
      },
      {
        id: 'avgFirstResponseMinutes',
        header: 'Avg FR (min)',
        accessorKey: 'avgFirstResponseMinutes',
        cell: ({ row }) => <span className="text-sm">{row.original.avgFirstResponseMinutes ?? '—'}</span>,
      },
      {
        id: 'avgResolutionMinutes',
        header: 'Avg Res (min)',
        accessorKey: 'avgResolutionMinutes',
        cell: ({ row }) => <span className="text-sm">{row.original.avgResolutionMinutes ?? '—'}</span>,
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
        cell: ({ row }) => <Badge variant="secondary">{row.original.count.toLocaleString()}</Badge>,
      },
    ];
  }, []);

  return (
    <div className="space-y-4">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2">
            <BarChart3 className="h-7 w-7" />
            Helpdesk Dashboard
          </h1>
          <p className="text-slate-600 mt-1">Operational snapshot for tickets and SLAs.</p>
        </div>
        <button
          className="h-10 px-4 rounded-md border bg-background text-sm hover:bg-muted"
          onClick={() => refetch()}
          disabled={isLoading}
        >
          Refresh
        </button>
      </div>

      <div className="flex flex-wrap gap-2">
        <button
          className="h-9 px-3 rounded-md border bg-background text-sm hover:bg-muted inline-flex items-center gap-2"
          onClick={() => downloadReport('/ehc/internal/reports/export/summary.pdf', `ehc-summary-${new Date().toISOString().slice(0, 10)}.pdf`)}
        >
          <Download className="h-4 w-4" />
          Export Summary PDF
        </button>
        <button
          className="h-9 px-3 rounded-md border bg-background text-sm hover:bg-muted inline-flex items-center gap-2"
          onClick={() => downloadReport('/ehc/internal/reports/export/summary.xlsx', `ehc-summary-${new Date().toISOString().slice(0, 10)}.xlsx`)}
        >
          <Download className="h-4 w-4" />
          Export Summary Excel
        </button>
        <button
          className="h-9 px-3 rounded-md border bg-background text-sm hover:bg-muted inline-flex items-center gap-2"
          onClick={() => downloadReport('/ehc/internal/reports/export/agent-performance.xlsx?days=30', `ehc-agent-performance-${new Date().toISOString().slice(0, 10)}.xlsx`)}
        >
          <Download className="h-4 w-4" />
          Export Agent Excel
        </button>
        <button
          className="h-9 px-3 rounded-md border bg-background text-sm hover:bg-muted inline-flex items-center gap-2"
          onClick={() => downloadReport('/ehc/internal/reports/export/agent-performance.pdf?days=30', `ehc-agent-performance-${new Date().toISOString().slice(0, 10)}.pdf`)}
        >
          <Download className="h-4 w-4" />
          Export Agent PDF
        </button>
        <button
          className="h-9 px-3 rounded-md border bg-background text-sm hover:bg-muted inline-flex items-center gap-2"
          onClick={() => downloadReport('/ehc/internal/reports/export/sla-compliance.xlsx?days=30', `ehc-sla-compliance-${new Date().toISOString().slice(0, 10)}.xlsx`)}
        >
          <Download className="h-4 w-4" />
          Export SLA Excel
        </button>
        <button
          className="h-9 px-3 rounded-md border bg-background text-sm hover:bg-muted inline-flex items-center gap-2"
          onClick={() => downloadReport('/ehc/internal/reports/export/sla-compliance.pdf?days=30', `ehc-sla-compliance-${new Date().toISOString().slice(0, 10)}.pdf`)}
        >
          <Download className="h-4 w-4" />
          Export SLA PDF
        </button>
        <button
          className="h-9 px-3 rounded-md border bg-background text-sm hover:bg-muted inline-flex items-center gap-2"
          onClick={() => downloadReport('/ehc/internal/reports/export/escalations.xlsx?days=30', `ehc-escalations-${new Date().toISOString().slice(0, 10)}.xlsx`)}
        >
          <Download className="h-4 w-4" />
          Export Escalations Excel
        </button>
        <button
          className="h-9 px-3 rounded-md border bg-background text-sm hover:bg-muted inline-flex items-center gap-2"
          onClick={() => downloadReport('/ehc/internal/reports/export/escalations.pdf?days=30', `ehc-escalations-${new Date().toISOString().slice(0, 10)}.pdf`)}
        >
          <Download className="h-4 w-4" />
          Export Escalations PDF
        </button>
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
              First Response Breaches
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

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm text-slate-600 flex items-center gap-2">
            <BarChart3 className="h-4 w-4" />
            SLA Compliance (Last 30 days)
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-0">
          <div className="h-[260px]">
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

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <DataTable
          compact
          title="By Status"
          data={byStatus}
          columns={columns}
          loading={isLoading}
          error={error ? 'Failed to load summary.' : null}
          enablePagination={false}
          enableSearch={false}
          enableColumnFilters={false}
          exportFileName={`helpdesk-by-status-${new Date().toISOString().slice(0, 10)}`}
          exportFormats={['csv', 'excel']}
        />

        <DataTable
          compact
          title="By Priority"
          data={byPriority}
          columns={columns}
          loading={isLoading}
          error={error ? 'Failed to load summary.' : null}
          enablePagination={false}
          enableSearch={false}
          enableColumnFilters={false}
          exportFileName={`helpdesk-by-priority-${new Date().toISOString().slice(0, 10)}`}
          exportFormats={['csv', 'excel']}
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        <DataTable
          compact
          title="Top Categories"
          data={byCategory}
          columns={columns}
          loading={isLoading}
          error={error ? 'Failed to load summary.' : null}
          enablePagination={false}
          enableSearch={false}
          enableColumnFilters={false}
          exportFileName={`helpdesk-by-category-${new Date().toISOString().slice(0, 10)}`}
          exportFormats={['csv', 'excel']}
        />

        <DataTable
          compact
          title="Top Root Causes"
          data={byRootCause}
          columns={columns}
          loading={isLoading}
          error={error ? 'Failed to load summary.' : null}
          enablePagination={false}
          enableSearch={false}
          enableColumnFilters={false}
          exportFileName={`helpdesk-by-root-cause-${new Date().toISOString().slice(0, 10)}`}
          exportFormats={['csv', 'excel']}
        />

        <DataTable
          compact
          title="By Department"
          data={byDepartment}
          columns={columns}
          loading={isLoading}
          error={error ? 'Failed to load summary.' : null}
          enablePagination={false}
          enableSearch={false}
          enableColumnFilters={false}
          exportFileName={`helpdesk-by-department-${new Date().toISOString().slice(0, 10)}`}
          exportFormats={['csv', 'excel']}
        />
      </div>

      <DataTable
        compact
        title="Agent Performance (Last 30 days)"
        data={agentPerf ?? []}
        columns={agentColumns}
        loading={agentLoading}
        error={agentError ? 'Failed to load agent performance.' : null}
        enablePagination={false}
        enableSearch={true}
        enableColumnFilters={false}
        exportFileName={`helpdesk-agent-performance-${new Date().toISOString().slice(0, 10)}`}
        exportFormats={['csv', 'excel']}
      />

      <DataTable
        compact
        title="Escalations (Last 30 days)"
        data={escalations ?? []}
        columns={escalationColumns}
        loading={escLoading}
        error={escError ? 'Failed to load escalations.' : null}
        enablePagination={false}
        enableSearch={true}
        enableColumnFilters={false}
        exportFileName={`helpdesk-escalations-${new Date().toISOString().slice(0, 10)}`}
        exportFormats={['csv', 'excel']}
      />
    </div>
  );
}

