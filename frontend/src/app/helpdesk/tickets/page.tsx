'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Ticket, Eye } from 'lucide-react';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Label } from '@/components/ui/label';
import { DataTable, type DataTableColumn, type DataTableAction } from '@/components/ui/DataTable';
import { useSignalR } from '@/hooks/useSignalR';
import { useToast } from '@/hooks/use-toast';
import {
  buildScopedHelpdeskDetailPath,
  buildScopedHelpdeskNewPath,
  getHelpdeskScopeConfig,
  isExternalTicketSource,
  isTicketInHelpdeskScope,
} from '@/lib/helpdesk-scope';
import { ehcInternalTicketService } from '@/services/ehcInternalTicketService';
import type { EhcTicketListItem, EhcTicketPriority, EhcTicketSource, EhcTicketStatus, EhcTicketType } from '@/services/ehcTicketService';

const statuses: Array<{ label: string; value: EhcTicketStatus | '' }> = [
  { label: 'All', value: '' },
  { label: 'New', value: 'New' },
  { label: 'Acknowledged', value: 'Acknowledged' },
  { label: 'In Progress', value: 'InProgress' },
  { label: 'Pending (User)', value: 'PendingUser' },
  { label: 'Pending (3rd Party)', value: 'PendingThirdParty' },
  { label: 'Resolved', value: 'Resolved' },
  { label: 'Closed', value: 'Closed' },
  { label: 'Reopened', value: 'Reopened' },
];

const statusBadgeClassName = (s: EhcTicketStatus) => {
  switch (s) {
    case 'Resolved':
      return 'bg-green-600 text-white hover:bg-green-600/90 dark:bg-green-500 dark:hover:bg-green-500/90';
    case 'Closed':
      return 'bg-slate-600 text-white hover:bg-slate-600/90 dark:bg-slate-500 dark:hover:bg-slate-500/90';
    default:
      return 'bg-blue-600 text-white hover:bg-blue-600/90 dark:bg-blue-500 dark:hover:bg-blue-500/90';
  }
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

const isTerminalStatus = (s: EhcTicketStatus) => s === 'Resolved' || s === 'Closed';

export default function HelpdeskTicketsPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const qc = useQueryClient();
  const { toast } = useToast();
  const seenNotificationIdsRef = useRef<Set<string>>(new Set());
  const scopeParam = searchParams.get('scope');
  const scopeConfig = useMemo(() => getHelpdeskScopeConfig(scopeParam), [scopeParam]);
  const [status, setStatus] = useState<EhcTicketStatus | ''>('');
  const [ticketType, setTicketType] = useState<EhcTicketType | ''>(
    scopeConfig.allowedTicketTypes.length === 1 ? scopeConfig.defaultTicketType : '',
  );
  const [priority, setPriority] = useState<EhcTicketPriority | ''>('');
  const [source, setSource] = useState<EhcTicketSource | ''>(scopeConfig.internalOnly ? 'Internal' : '');
  const [categoryId, setCategoryId] = useState<string>('');
  const [assignedDepartmentId, setAssignedDepartmentId] = useState<string>('');
  const [createdFrom, setCreatedFrom] = useState<string>('');
  const [createdTo, setCreatedTo] = useState<string>('');

  const typeFilterLocked = scopeConfig.allowedTicketTypes.length === 1;
  const ticketTypeQueryFilter = typeFilterLocked ? scopeConfig.defaultTicketType : ticketType || null;
  const sourceQueryFilter = scopeConfig.internalOnly ? 'Internal' : source || null;

  const scopedSourceOptions = useMemo<Array<{ value: EhcTicketSource | ''; label: string }>>(() => {
    if (scopeConfig.internalOnly) return [{ value: 'Internal', label: 'Internal' }];
    return [
      { value: '', label: 'All external' },
      { value: 'Web', label: 'Website' },
      { value: 'Mobile', label: 'Mobile App' },
      { value: 'Email', label: 'Email' },
      { value: 'PhoneCall', label: 'Phone Call' },
      { value: 'Sms', label: 'SMS' },
      { value: 'WhatsApp', label: 'WhatsApp' },
    ];
  }, [scopeConfig.internalOnly]);

  useEffect(() => {
    setTicketType(typeFilterLocked ? scopeConfig.defaultTicketType : '');
    setSource(scopeConfig.internalOnly ? 'Internal' : '');
    setStatus('');
    setPriority('');
    setCategoryId('');
    setAssignedDepartmentId('');
    setCreatedFrom('');
    setCreatedTo('');
  }, [scopeConfig.defaultTicketType, scopeConfig.internalOnly, scopeConfig.scope, typeFilterLocked]);

  const createdAtFormatter = useMemo(() => new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }), []);
  const formatCreatedAt = useCallback((iso: string | null | undefined) => {
    if (!iso) return '—';
    const dt = new Date(iso);
    if (Number.isNaN(dt.getTime())) return String(iso);
    return createdAtFormatter.format(dt);
  }, [createdAtFormatter]);

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['ehc', 'internal', 'tickets', scopeConfig.scope, { status, ticketTypeQueryFilter, priority, sourceQueryFilter, categoryId, assignedDepartmentId, createdFrom, createdTo }],
    queryFn: () =>
      ehcInternalTicketService.listTickets(1, 500, {
        status: status || null,
        ticketType: ticketTypeQueryFilter,
        priority: priority || null,
        source: sourceQueryFilter,
        categoryId: categoryId || null,
        assignedDepartmentId: assignedDepartmentId || null,
        createdFrom: createdFrom || null,
        createdTo: createdTo || null,
      }),
  });

  const scopedData = useMemo(
    () =>
      (data || []).filter((ticket) => {
        if (!isTicketInHelpdeskScope(ticket, scopeConfig.scope)) return false;
        if (!scopeConfig.internalOnly && !source && !isExternalTicketSource(ticket.source)) return false;
        return true;
      }),
    [data, scopeConfig.scope, scopeConfig.internalOnly, source],
  );

  const handleRealtimeNotification = useCallback(
    (n: any) => {
      const meta = n?.metadata ?? n?.Metadata ?? null;
      const entityType = meta?.EntityType ?? meta?.entityType ?? null;

      const actionUrl = (n?.actionUrl ?? n?.ActionUrl ?? '') as string;
      const looksLikeTicketUrl = typeof actionUrl === 'string' && actionUrl.toLowerCase().includes('/tickets/');

      const isTicket =
        typeof entityType === 'string' &&
        entityType.toLowerCase() === 'ehcticket';

      if (!isTicket && !looksLikeTicketUrl) return;

      const id = (n?.id ?? n?.Id ?? null) as string | null;
      if (id) {
        if (seenNotificationIdsRef.current.has(id)) {
          qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'tickets'] });
          return;
        }
        seenNotificationIdsRef.current.add(id);
        if (seenNotificationIdsRef.current.size > 200) {
          seenNotificationIdsRef.current.clear();
          seenNotificationIdsRef.current.add(id);
        }
      }

      toast({ title: 'Ticket update', description: n?.title || n?.Title || 'A ticket was updated.', variant: 'success' });
      qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'tickets'] });
    },
    [qc, toast]
  );

  useSignalR({
    autoConnect: true,
    onNotification: handleRealtimeNotification,
  });

  const { data: departments } = useQuery({
    queryKey: ['ehc', 'internal', 'departments'],
    queryFn: () => ehcInternalTicketService.listDepartments(),
  });

  const { data: priorityLevels } = useQuery({
    queryKey: ['ehc', 'internal', 'priorities'],
    queryFn: () => ehcInternalTicketService.listPriorityLevels(),
  });

  const activePriorityLevels = useMemo(() => {
    return (priorityLevels ?? [])
      .filter((p) => p.isActive)
      .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0));
  }, [priorityLevels]);

  const priorityLabelByValue = useMemo(() => {
    return new Map((priorityLevels ?? []).map((p) => [p.priority, p.displayName] as const));
  }, [priorityLevels]);

  const { data: categories } = useQuery({
    queryKey: ['ehc', 'admin', 'categories'],
    queryFn: () => ehcInternalTicketService.listCategories(),
  });

  const categoryLabelById = useMemo(() => {
    const items = categories || [];
    const byId = new Map(items.map((c) => [c.id, c]));
    const cache = new Map<string, string>();

    const buildLabel = (id: string): string => {
      const cached = cache.get(id);
      if (cached) return cached;
      const c = byId.get(id);
      if (!c) return id;
      if (!c.parentCategoryId) {
        cache.set(id, c.name);
        return c.name;
      }
      const parent = buildLabel(c.parentCategoryId);
      const label = `${parent} / ${c.name}`;
      cache.set(id, label);
      return label;
    };

    for (const c of items) buildLabel(c.id);
    return cache;
  }, [categories]);

  const selectedTypeForCategories = ticketType || null;
  const categoryOptions = useMemo(() => {
    const items = categories || [];
    return items
      .filter((c) => !selectedTypeForCategories || !c.appliesToType || c.appliesToType === selectedTypeForCategories)
      .map((c) => ({ id: c.id, label: categoryLabelById.get(c.id) || c.name }))
      .sort((a, b) => a.label.localeCompare(b.label));
  }, [categories, selectedTypeForCategories, categoryLabelById]);

  const columns = useMemo<Array<DataTableColumn<EhcTicketListItem>>>(() => {
    return [
      {
        id: 'ticketNumber',
        header: 'Ticket',
        accessorKey: 'ticketNumber',
        cell: ({ row }) => (
          <div className="font-medium text-slate-900">
            {row.original.ticketNumber}
            {row.original.subject ? <div className="text-xs text-slate-500 truncate max-w-[260px]">{row.original.subject}</div> : null}
          </div>
        ),
      },
      {
        id: 'createdAt',
        header: 'Created',
        accessorKey: 'createdAt',
        cell: ({ row }) => <div className="whitespace-nowrap">{formatCreatedAt(row.original.createdAt)}</div>,
      },
      {
        id: 'requester',
        header: 'Submitted By',
        accessorFn: (r) => r.requesterName || '—',
        cell: ({ row }) => {
          const name = row.original.requesterName || '—';
          const provider = row.original.requesterAuthenticationProvider || null;
          const submittedVia = provider === 'Local' ? 'External Portal' : provider ? 'Internal ERP' : null;
          return (
            <div className="min-w-[160px]">
              <div className="font-medium text-slate-900">{name}</div>
              {submittedVia ? <div className="text-xs text-slate-500">{submittedVia}</div> : null}
            </div>
          );
        },
      },
      {
        id: 'source',
        header: 'Channel',
        accessorFn: (r) => {
          if (r.source === 'Web') return 'Website';
          if (r.source === 'PhoneCall') return 'Phone Call';
          if (r.source === 'Sms') return 'SMS';
          return r.source;
        },
      },
      {
        id: 'ticketType',
        header: 'Type',
        accessorKey: 'ticketType',
      },
      {
        id: 'priority',
        header: 'Priority',
        accessorFn: (r) => r.priority,
        cell: ({ row }) => (
          <Badge className={priorityBadgeClassName(row.original.priority)}>
            {priorityLabelByValue.get(row.original.priority) ?? row.original.priority}
          </Badge>
        ),
      },
      {
        id: 'status',
        header: 'Status',
        accessorFn: (r) => r.status,
        cell: ({ row }) => <Badge className={statusBadgeClassName(row.original.status)}>{row.original.status}</Badge>,
      },
      {
        id: 'categoryName',
        header: 'Category',
        accessorFn: (r) => r.categoryName || '—',
      },
      {
        id: 'assignedDepartmentName',
        header: 'Department',
        accessorFn: (r) => r.assignedDepartmentName || '—',
      },
      {
        id: 'assignedToName',
        header: 'Assignee',
        accessorFn: (r) => r.assignedToName || '—',
      },
      {
        id: 'sla',
        header: 'SLA',
        accessorFn: (r) => {
          const now = Date.now();
          const firstResponseDueAt = r.firstResponseDueAt ? Date.parse(r.firstResponseDueAt) : Number.NaN;
          const resolutionDueAt = r.resolutionDueAt ? Date.parse(r.resolutionDueAt) : Number.NaN;
          const firstRespondedAt = r.firstRespondedAt ? Date.parse(r.firstRespondedAt) : Number.NaN;
          const resolvedAt = r.resolvedAt ? Date.parse(r.resolvedAt) : Number.NaN;
          const closedAt = r.closedAt ? Date.parse(r.closedAt) : Number.NaN;

          const firstResponseOverdue = !isTerminalStatus(r.status) && !Number.isNaN(firstResponseDueAt) && now > firstResponseDueAt && Number.isNaN(firstRespondedAt);
          const resolutionOverdue = !isTerminalStatus(r.status) && !Number.isNaN(resolutionDueAt) && now > resolutionDueAt && Number.isNaN(resolvedAt) && Number.isNaN(closedAt);

          if (resolutionOverdue) return 'ResolutionOverdue';
          if (firstResponseOverdue) return 'FirstResponseOverdue';
          return '';
        },
        cell: ({ row }) => {
          const t = row.original;
          if (isTerminalStatus(t.status)) return <span className="text-slate-500">—</span>;

          const now = Date.now();
          const firstResponseDueAt = t.firstResponseDueAt ? Date.parse(t.firstResponseDueAt) : Number.NaN;
          const resolutionDueAt = t.resolutionDueAt ? Date.parse(t.resolutionDueAt) : Number.NaN;
          const firstRespondedAt = t.firstRespondedAt ? Date.parse(t.firstRespondedAt) : Number.NaN;
          const resolvedAt = t.resolvedAt ? Date.parse(t.resolvedAt) : Number.NaN;
          const closedAt = t.closedAt ? Date.parse(t.closedAt) : Number.NaN;

          const firstResponseOverdue = !Number.isNaN(firstResponseDueAt) && now > firstResponseDueAt && Number.isNaN(firstRespondedAt);
          const resolutionOverdue = !Number.isNaN(resolutionDueAt) && now > resolutionDueAt && Number.isNaN(resolvedAt) && Number.isNaN(closedAt);

          if (!firstResponseOverdue && !resolutionOverdue) return <span className="text-slate-500">—</span>;

          return (
            <div className="flex flex-col gap-1">
              {firstResponseOverdue ? (
                <Badge
                  className="bg-orange-600 text-white hover:bg-orange-600/90 dark:bg-orange-500 dark:hover:bg-orange-500/90 w-fit"
                  title={t.firstResponseDueAt ? `First response was due: ${formatCreatedAt(t.firstResponseDueAt)}` : 'First response overdue'}
                >
                  Response overdue
                </Badge>
              ) : null}
              {resolutionOverdue ? (
                <Badge
                  className="bg-red-600 text-white hover:bg-red-600/90 dark:bg-red-500 dark:hover:bg-red-500/90 w-fit"
                  title={t.resolutionDueAt ? `Resolution was due: ${formatCreatedAt(t.resolutionDueAt)}` : 'Resolution overdue'}
                >
                  Resolution overdue
                </Badge>
              ) : null}
            </div>
          );
        },
      },
    ];
  }, [formatCreatedAt]);

  const rowActions = useMemo<Array<DataTableAction<EhcTicketListItem>>>(() => {
    return [
      {
        id: 'view',
        label: 'View',
        icon: Eye,
        onClick: (row) => router.push(buildScopedHelpdeskDetailPath(scopeConfig.scope, row.original.id)),
        variant: 'outline',
        size: 'sm',
      },
    ];
  }, [router, scopeConfig.scope]);

  return (
    <div className="space-y-4">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2">
            <Ticket className="h-7 w-7" />
            {scopeConfig.listTitle}
          </h1>
          <p className="text-slate-600 mt-1">{scopeConfig.listDescription}</p>
        </div>
        <div className="flex items-center gap-2">
          <Button onClick={() => router.push(buildScopedHelpdeskNewPath(scopeConfig.scope))}>{scopeConfig.createButtonLabel}</Button>
          <Button variant="outline" onClick={() => refetch()}>
            Refresh
          </Button>
        </div>
      </div>

      <Card>
        <CardHeader className="p-4 pb-2">
          <CardTitle>Filters</CardTitle>
          <CardDescription className="text-xs">Filter records by ticket type, channel, priority, status, category, department, and date.</CardDescription>
        </CardHeader>
        <CardContent className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-7 gap-3 p-4 pt-0">
          <div className="space-y-1">
            <Label className="text-xs text-slate-600">Status</Label>
            <select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={status} onChange={(e) => setStatus(e.target.value as any)}>
              {statuses.map((s) => (
                <option key={s.label} value={s.value}>
                  {s.label}
                </option>
              ))}
            </select>
          </div>

          <div className="space-y-1">
            <Label className="text-xs text-slate-600">Type</Label>
            {typeFilterLocked ? (
              <div className="flex h-10 items-center rounded-md border border-input bg-slate-50 px-3 text-sm text-slate-700">
                {scopeConfig.ticketTypeLabel}
              </div>
            ) : (
              <select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={ticketType} onChange={(e) => setTicketType(e.target.value as any)}>
                <option value="">All</option>
                {scopeConfig.allowedTicketTypes.map((type) => (
                  <option key={type} value={type}>
                    {type}
                  </option>
                ))}
              </select>
            )}
          </div>

          <div className="space-y-1">
            <Label className="text-xs text-slate-600">Priority</Label>
            <select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={priority} onChange={(e) => setPriority(e.target.value as any)}>
              <option value="">All</option>
              {activePriorityLevels.map((p) => (
                <option key={p.priority} value={p.priority}>
                  {p.displayName}
                </option>
              ))}
            </select>
          </div>

          <div className="space-y-1">
            <Label className="text-xs text-slate-600">Category</Label>
            <select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={categoryId} onChange={(e) => setCategoryId(e.target.value)}>
              <option value="">All</option>
              {categoryOptions.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.label}
                </option>
              ))}
            </select>
          </div>

          <div className="space-y-1">
            <Label className="text-xs text-slate-600">Department</Label>
            <select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={assignedDepartmentId} onChange={(e) => setAssignedDepartmentId(e.target.value)}>
              <option value="">All</option>
              {(departments || []).map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </select>
          </div>

          <div className="space-y-1">
            <Label className="text-xs text-slate-600">Channel</Label>
            {scopeConfig.internalOnly ? (
              <div className="flex h-10 items-center rounded-md border border-input bg-slate-50 px-3 text-sm text-slate-700">
                Internal
              </div>
            ) : (
              <select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={source} onChange={(e) => setSource(e.target.value as any)}>
                {scopedSourceOptions.map((option) => (
                  <option key={option.label} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            )}
          </div>

          <div className="space-y-1">
            <Label className="text-xs text-slate-600">From</Label>
            <input className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" type="date" value={createdFrom} onChange={(e) => setCreatedFrom(e.target.value)} />
          </div>

          <div className="space-y-1">
            <Label className="text-xs text-slate-600">To</Label>
            <input className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" type="date" value={createdTo} onChange={(e) => setCreatedTo(e.target.value)} />
          </div>

          <div className="flex items-end gap-2">
            <Button
              variant="outline"
              onClick={() => {
                setStatus('');
                setTicketType(typeFilterLocked ? scopeConfig.defaultTicketType : '');
                setPriority('');
                setSource(scopeConfig.internalOnly ? 'Internal' : '');
                setCategoryId('');
                setAssignedDepartmentId('');
                setCreatedFrom('');
                setCreatedTo('');
              }}
            >
              Clear
            </Button>
          </div>
        </CardContent>
      </Card>

      <DataTable
        compact
        data={scopedData}
        columns={columns}
        loading={isLoading}
        error={error ? 'Failed to load tickets.' : null}
        enableColumnFilters={false}
        enableExport={true}
        exportFileName={`${scopeConfig.scope}-tickets-${new Date().toISOString().slice(0, 10)}`}
        exportFormats={['csv', 'excel']}
        rowActions={rowActions}
        initialColumnVisibility={{ categoryName: false, assignedDepartmentName: false, assignedToName: false }}
        onRowDoubleClick={(row) => router.push(buildScopedHelpdeskDetailPath(scopeConfig.scope, row.original.id))}
        emptyStateMessage="No tickets match the current filter."
        toolbarActions={{
          refresh: () => refetch(),
          create: () => router.push(buildScopedHelpdeskNewPath(scopeConfig.scope)),
        }}
      />
    </div>
  );
}
