'use client';

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { BarChart3, Clock, AlertTriangle } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable';
import { ehcInternalTicketService, type EhcHelpdeskSummary } from '@/services/ehcInternalTicketService';

type CountRow = { key: string; count: number };

export default function HelpdeskDashboardPage() {
  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['ehc', 'internal', 'reports', 'summary'],
    queryFn: () => ehcInternalTicketService.getSummary(),
  });

  const totals = data?.totals ?? null;

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

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
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
    </div>
  );
}

