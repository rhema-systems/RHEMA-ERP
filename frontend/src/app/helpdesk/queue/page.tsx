'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Eye, Save, Settings2, Trash2, UserPlus } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { DataTable, type DataTableAction, type DataTableColumn } from '@/components/ui/DataTable';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { ehcInternalTicketService, type EhcAdminCategory } from '@/services/ehcInternalTicketService';
import type { EhcTicketListItem, EhcTicketPriority, EhcTicketSource, EhcTicketStatus, EhcTicketType } from '@/services/ehcTicketService';

type CategoryNode = EhcAdminCategory & { children?: CategoryNode[] };

type QueueSavedFilters = {
  status: EhcTicketStatus | '';
  ticketType: EhcTicketType | '';
  priority: EhcTicketPriority | '';
  source: EhcTicketSource | '';
  categoryId: string;
  assignedDepartmentId: string;
  createdFrom: string;
  createdTo: string;
};

type QueueSavedView = {
  id: string;
  name: string;
  filters: QueueSavedFilters;
  createdAt: string; // ISO
};

const queueViewsStorageKey = 'ehc.helpdesk.queue.views.v1';
const queueDefaultViewStorageKey = 'ehc.helpdesk.queue.defaultViewId.v1';

const tryReadJson = <T,>(raw: string | null): T | null => {
  if (!raw) return null;
  try {
    return JSON.parse(raw) as T;
  } catch {
    return null;
  }
};

const getUuid = () => {
  try {
    // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition
    if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) return (crypto as any).randomUUID() as string;
  } catch {
    // ignore
  }
  return `${Date.now()}-${Math.random().toString(16).slice(2)}`;
};

const normalizeFilters = (f: Partial<QueueSavedFilters>): QueueSavedFilters => ({
  status: (f.status ?? '') as any,
  ticketType: (f.ticketType ?? '') as any,
  priority: (f.priority ?? '') as any,
  source: (f.source ?? '') as any,
  categoryId: f.categoryId ?? '',
  assignedDepartmentId: f.assignedDepartmentId ?? '',
  createdFrom: f.createdFrom ?? '',
  createdTo: f.createdTo ?? '',
});

const buildCategoryOptions = (flat: EhcAdminCategory[]) => {
  const byId = new Map<string, CategoryNode>();
  for (const c of flat) byId.set(c.id, { ...c, children: [] });
  const roots: CategoryNode[] = [];
  for (const c of byId.values()) {
    if (c.parentCategoryId && byId.has(c.parentCategoryId)) byId.get(c.parentCategoryId)!.children!.push(c);
    else roots.push(c);
  }
  const sortRec = (nodes: CategoryNode[]) => {
    nodes.sort((a, b) => String(a.name || '').localeCompare(String(b.name || '')));
    for (const n of nodes) if (n.children?.length) sortRec(n.children);
  };
  sortRec(roots);

  const flatten = (nodes: CategoryNode[], rootId: string | null = null, depth = 0) => {
    const out: Array<{ label: string; categoryId: string; subcategoryId?: string; depth: number; appliesToType?: EhcTicketType | null }> = [];
    for (const n of nodes) {
      const effectiveRootId = rootId ?? n.id;
      out.push({ label: n.name, categoryId: effectiveRootId, subcategoryId: rootId ? n.id : undefined, depth, appliesToType: n.appliesToType ?? null });
      if (n.children?.length) out.push(...flatten(n.children, effectiveRootId, depth + 1));
    }
    return out;
  };

  return flatten(roots).map((o) => ({ ...o, label: `${'— '.repeat(o.depth)}${o.label}` }));
};

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

export default function HelpdeskQueuePage() {
  const router = useRouter();
  const qc = useQueryClient();
  const { toast } = useToast();
  const { user } = useAuth();

  const [status, setStatus] = useState<EhcTicketStatus | ''>('');
  const [ticketType, setTicketType] = useState<EhcTicketType | ''>('');
  const [priority, setPriority] = useState<EhcTicketPriority | ''>('');
  const [source, setSource] = useState<EhcTicketSource | ''>('');
  const [categoryId, setCategoryId] = useState<string>('');
  const [assignedDepartmentId, setAssignedDepartmentId] = useState<string>('');
  const [createdFrom, setCreatedFrom] = useState<string>('');
  const [createdTo, setCreatedTo] = useState<string>('');

  const currentFilters = useMemo<QueueSavedFilters>(
    () => ({ status, ticketType, priority, source, categoryId, assignedDepartmentId, createdFrom, createdTo }),
    [assignedDepartmentId, categoryId, createdFrom, createdTo, priority, source, status, ticketType]
  );

  const [viewsLoaded, setViewsLoaded] = useState(false);
  const [savedViews, setSavedViews] = useState<QueueSavedView[]>([]);
  const [selectedViewId, setSelectedViewId] = useState<string>('');
  const [saveOpen, setSaveOpen] = useState(false);
  const [manageOpen, setManageOpen] = useState(false);
  const [shortcutsOpen, setShortcutsOpen] = useState(false);
  const [viewName, setViewName] = useState('');
  const [setAsDefault, setSetAsDefault] = useState(true);

  const clearFilters = useCallback(() => {
    setSelectedViewId('');
    setStatus('');
    setTicketType('');
    setPriority('');
    setSource('');
    setCategoryId('');
    setAssignedDepartmentId('');
    setCreatedFrom('');
    setCreatedTo('');
  }, []);

  const applyFilters = useCallback((f: Partial<QueueSavedFilters>) => {
    const nf = normalizeFilters(f);
    setStatus(nf.status);
    setTicketType(nf.ticketType);
    setPriority(nf.priority);
    setSource(nf.source);
    setCategoryId(nf.categoryId);
    setAssignedDepartmentId(nf.assignedDepartmentId);
    setCreatedFrom(nf.createdFrom);
    setCreatedTo(nf.createdTo);
  }, []);

  const persistViews = useCallback((views: QueueSavedView[], defaultId: string | null) => {
    setSavedViews(views);
    try {
      localStorage.setItem(queueViewsStorageKey, JSON.stringify(views));
      if (defaultId) localStorage.setItem(queueDefaultViewStorageKey, defaultId);
      else localStorage.removeItem(queueDefaultViewStorageKey);
    } catch {
      // ignore localStorage failures
    }
  }, []);

  useEffect(() => {
    if (viewsLoaded) return;

    const stored = tryReadJson<QueueSavedView[]>(typeof window !== 'undefined' ? localStorage.getItem(queueViewsStorageKey) : null) ?? [];
    const cleaned = (stored || [])
      .filter((x) => x && typeof x.id === 'string' && typeof x.name === 'string')
      .map((x) => ({ ...x, filters: normalizeFilters(x.filters as any) }));

    setSavedViews(cleaned);

    const defaultId = (typeof window !== 'undefined' ? localStorage.getItem(queueDefaultViewStorageKey) : null) || '';
    const defaultView = defaultId ? cleaned.find((v) => v.id === defaultId) : null;
    if (defaultView) {
      setSelectedViewId(defaultView.id);
      applyFilters(defaultView.filters);
    }

    setViewsLoaded(true);
  }, [applyFilters, viewsLoaded]);

  useEffect(() => {
    if (!viewsLoaded) return;
    if (!selectedViewId) return;
    const v = savedViews.find((x) => x.id === selectedViewId);
    if (!v) return;
    const a = JSON.stringify(normalizeFilters(v.filters));
    const b = JSON.stringify(normalizeFilters(currentFilters));
    if (a !== b) setSelectedViewId('');
  }, [currentFilters, savedViews, selectedViewId, viewsLoaded]);

  useEffect(() => {
    const isTypingTarget = (t: EventTarget | null) => {
      const el = t as HTMLElement | null;
      if (!el) return false;
      if ((el as any).isContentEditable) return true;
      const tag = (el.tagName || '').toLowerCase();
      return tag === 'input' || tag === 'textarea' || tag === 'select';
    };

    const onKeyDown = (e: KeyboardEvent) => {
      if (e.defaultPrevented) return;
      if (isTypingTarget(e.target)) return;
      if (e.ctrlKey || e.metaKey || e.altKey) return;

      if (e.key === '?') {
        e.preventDefault();
        setShortcutsOpen(true);
        return;
      }

      if (e.key === 'r' || e.key === 'R') {
        e.preventDefault();
        void qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'queue'] });
        toast({ title: 'Refreshed', description: 'Queue reloaded.', variant: 'success' });
        return;
      }

      if (e.key === 'c' || e.key === 'C') {
        e.preventDefault();
        clearFilters();
        return;
      }

      if (e.key === 'n' || e.key === 'N') {
        e.preventDefault();
        router.push('/helpdesk/tickets/new');
        return;
      }

      if (e.key === 's' || e.key === 'S') {
        e.preventDefault();
        setViewName('');
        setSetAsDefault(true);
        setSaveOpen(true);
        return;
      }

      if (e.key === 'm' || e.key === 'M') {
        e.preventDefault();
        setManageOpen(true);
        return;
      }
    };

    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [clearFilters, qc, router, toast]);

  const createdAtFormatter = useMemo(() => new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }), []);
  const formatCreatedAt = useCallback(
    (iso: string | null | undefined) => {
      if (!iso) return '—';
      const dt = new Date(iso);
      if (Number.isNaN(dt.getTime())) return String(iso);
      return createdAtFormatter.format(dt);
    },
    [createdAtFormatter]
  );

  const { data: departments } = useQuery({
    queryKey: ['ehc', 'internal', 'departments'],
    queryFn: () => ehcInternalTicketService.listDepartments(),
  });

  const { data: categories } = useQuery({
    queryKey: ['ehc', 'admin', 'categories'],
    queryFn: () => ehcInternalTicketService.listCategories(),
  });

  const categoryOptions = useMemo(() => buildCategoryOptions(categories || []), [categories]);
  const visibleCategoryOptions = useMemo(
    () =>
      categoryOptions.filter((o) => !o.appliesToType || o.appliesToType === ticketType || !ticketType),
    [categoryOptions, ticketType]
  );

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['ehc', 'internal', 'queue', { status, ticketType, priority, source, categoryId, assignedDepartmentId, createdFrom, createdTo }],
    queryFn: async () => {
      const statusesToFetch: EhcTicketStatus[] = status ? [status] : ['New', 'Acknowledged'];
      const lists = await Promise.all(
        statusesToFetch.map((s) =>
          ehcInternalTicketService.listTickets(1, 500, {
            status: s,
            ticketType: ticketType || null,
            priority: priority || null,
            source: source || null,
            categoryId: categoryId || null,
            assignedDepartmentId: assignedDepartmentId || null,
            createdFrom: createdFrom || null,
            createdTo: createdTo || null,
          })
        )
      );
      const merged = lists.flat();
      const byId = new Map<string, EhcTicketListItem>();
      for (const t of merged) {
        if (!t?.id) continue;
        if (!byId.has(t.id)) byId.set(t.id, t);
      }
      return Array.from(byId.values()).filter((t) => !t.assignedToName);
    },
  });

  const assignToMe = useMutation({
    mutationFn: async (ticketId: string) => {
      const uid = user?.id;
      if (!uid) throw new Error('Current user is not loaded');
      await ehcInternalTicketService.assignTicket(ticketId, uid, assignedDepartmentId || null);
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'queue'] });
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'tickets'] });
      toast({ title: 'Assigned', description: 'Ticket assigned to you.', variant: 'success' });
    },
    onError: (err: any) => {
      toast({ title: 'Error', description: err?.message || 'Failed to assign ticket.', variant: 'destructive' });
    },
  });

  const columns = useMemo<Array<DataTableColumn<EhcTicketListItem>>>(() => {
    return [
      {
        id: 'ticketNumber',
        header: 'Ticket #',
        accessorKey: 'ticketNumber',
        cell: ({ row }) => (
          <button className="text-blue-600 hover:underline" onClick={() => router.push(`/helpdesk/tickets/${row.original.id}`)} title="Open ticket">
            {row.original.ticketNumber}
          </button>
        ),
      },
      { id: 'subject', header: 'Subject', accessorKey: 'subject' },
      { id: 'ticketType', header: 'Type', accessorKey: 'ticketType' },
      {
        id: 'status',
        header: 'Status',
        accessorKey: 'status',
        cell: ({ row }) => <Badge className={statusBadgeClassName(row.original.status)}>{row.original.status}</Badge>,
      },
      {
        id: 'priority',
        header: 'Priority',
        accessorKey: 'priority',
        cell: ({ row }) => <Badge className={priorityBadgeClassName(row.original.priority)}>{row.original.priority}</Badge>,
      },
      { id: 'source', header: 'Channel', accessorKey: 'source' },
      {
        id: 'createdAt',
        header: 'Created',
        accessorKey: 'createdAt',
        cell: ({ row }) => <span className="text-slate-700">{formatCreatedAt(row.original.createdAt)}</span>,
      },
      { id: 'requesterName', header: 'Requester', accessorKey: 'requesterName' },
    ];
  }, [formatCreatedAt, router]);

  const rowActions = useMemo<Array<DataTableAction<EhcTicketListItem>>>(() => {
    return [
      {
        id: 'view',
        label: 'View',
        icon: Eye as any,
        onClick: (row) => router.push(`/helpdesk/tickets/${row.original.id}`),
      },
      {
        id: 'assignToMe',
        label: 'Assign to me',
        icon: UserPlus as any,
        onClick: (row) => assignToMe.mutate(row.original.id),
        disabled: () => assignToMe.isPending,
      },
    ];
  }, [assignToMe.isPending, router]);

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold">Assignment Queue</h1>
          <p className="text-slate-600">Unassigned tickets waiting for pickup.</p>
        </div>
      </div>

      <Card className="border-slate-200">
        <CardHeader className="py-4">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <CardTitle className="text-base">Filters</CardTitle>
              <CardDescription className="text-sm">Queue defaults to New + Acknowledged unassigned tickets.</CardDescription>
            </div>

            <div className="flex items-center gap-2">
              <select
                className="h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={selectedViewId}
                onChange={(e) => {
                  const id = e.target.value;
                  setSelectedViewId(id);
                  const v = savedViews.find((x) => x.id === id) || null;
                  if (v) applyFilters(v.filters);
                }}
                title="Saved views"
              >
                <option value="">Custom</option>
                {savedViews
                  .slice()
                  .sort((a, b) => a.name.localeCompare(b.name))
                  .map((v) => (
                    <option key={v.id} value={v.id}>
                      {v.name}
                    </option>
                  ))}
              </select>

              <Button
                type="button"
                variant="outline"
                onClick={() => {
                  setViewName('');
                  setSetAsDefault(true);
                  setSaveOpen(true);
                }}
                title="Save current filters as a view"
              >
                <Save className="h-4 w-4 mr-2" />
                Save view
              </Button>

              <Button type="button" variant="outline" onClick={() => setManageOpen(true)} title="Manage saved views">
                <Settings2 className="h-4 w-4 mr-2" />
                Manage
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent className="pt-0 grid grid-cols-1 md:grid-cols-8 gap-3">
          <div className="space-y-1">
            <Label className="text-xs text-slate-600">Status</Label>
            <select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={status} onChange={(e) => setStatus((e.target.value || '') as any)}>
              <option value="">New + Acknowledged</option>
              <option value="New">New</option>
              <option value="Acknowledged">Acknowledged</option>
              <option value="Reopened">Reopened</option>
              <option value="InProgress">In Progress</option>
              <option value="PendingUser">Pending (User)</option>
              <option value="PendingThirdParty">Pending (3rd Party)</option>
              <option value="Resolved">Resolved</option>
              <option value="Closed">Closed</option>
            </select>
          </div>

          <div className="space-y-1">
            <Label className="text-xs text-slate-600">Type</Label>
            <select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={ticketType} onChange={(e) => setTicketType((e.target.value || '') as any)}>
              <option value="">All</option>
              <option value="Enquiry">Enquiry</option>
              <option value="Complaint">Complaint</option>
              <option value="Helpdesk">Helpdesk</option>
            </select>
          </div>

          <div className="space-y-1">
            <Label className="text-xs text-slate-600">Priority</Label>
            <select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={priority} onChange={(e) => setPriority((e.target.value || '') as any)}>
              <option value="">All</option>
              <option value="Low">Low</option>
              <option value="Medium">Medium</option>
              <option value="High">High</option>
              <option value="Critical">Critical</option>
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
            <select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={source} onChange={(e) => setSource((e.target.value || '') as any)}>
              <option value="">All</option>
              <option value="Web">Website</option>
              <option value="Mobile">Mobile</option>
              <option value="Email">Email</option>
              <option value="Internal">Internal</option>
              <option value="PhoneCall">Phone Call</option>
              <option value="Sms">SMS</option>
              <option value="WhatsApp">WhatsApp</option>
            </select>
          </div>

          <div className="space-y-1 md:col-span-2">
            <Label className="text-xs text-slate-600">Category</Label>
            <select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={categoryId} onChange={(e) => setCategoryId(e.target.value)}>
              <option value="">All</option>
              {visibleCategoryOptions.map((o) => (
                <option key={`${o.categoryId}:${o.subcategoryId ?? ''}`} value={o.subcategoryId || o.categoryId}>
                  {o.label}
                </option>
              ))}
            </select>
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
                clearFilters();
              }}
            >
              Clear
            </Button>
          </div>
        </CardContent>
      </Card>

      <Dialog open={shortcutsOpen} onOpenChange={setShortcutsOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Keyboard shortcuts</DialogTitle>
            <DialogDescription>Shortcuts apply when you are not typing in a form field.</DialogDescription>
          </DialogHeader>
          <div className="space-y-2 text-sm">
            <div className="flex items-center justify-between gap-2">
              <span className="text-slate-700">Refresh queue</span>
              <Badge variant="secondary">R</Badge>
            </div>
            <div className="flex items-center justify-between gap-2">
              <span className="text-slate-700">Clear filters</span>
              <Badge variant="secondary">C</Badge>
            </div>
            <div className="flex items-center justify-between gap-2">
              <span className="text-slate-700">New ticket</span>
              <Badge variant="secondary">N</Badge>
            </div>
            <div className="flex items-center justify-between gap-2">
              <span className="text-slate-700">Save view</span>
              <Badge variant="secondary">S</Badge>
            </div>
            <div className="flex items-center justify-between gap-2">
              <span className="text-slate-700">Manage views</span>
              <Badge variant="secondary">M</Badge>
            </div>
            <div className="flex items-center justify-between gap-2">
              <span className="text-slate-700">This help</span>
              <Badge variant="secondary">?</Badge>
            </div>
          </div>
          <DialogFooter>
            <Button type="button" onClick={() => setShortcutsOpen(false)}>
              Done
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={saveOpen} onOpenChange={setSaveOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Save view</DialogTitle>
            <DialogDescription>Save the current queue filters for quick access.</DialogDescription>
          </DialogHeader>

          <div className="space-y-2">
            <Label>Name</Label>
            <Input value={viewName} onChange={(e) => setViewName(e.target.value)} placeholder="e.g. Critical - IT Dept" />
            <label className="flex items-center gap-2 text-sm text-slate-700">
              <input type="checkbox" checked={setAsDefault} onChange={(e) => setSetAsDefault(e.target.checked)} />
              Set as default view
            </label>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setSaveOpen(false)}>
              Cancel
            </Button>
            <Button
              type="button"
              onClick={() => {
                const name = (viewName || '').trim();
                if (!name) {
                  toast({ title: 'Name required', description: 'Enter a view name.', variant: 'destructive' });
                  return;
                }

                const id = getUuid();
                const v: QueueSavedView = { id, name, filters: normalizeFilters(currentFilters), createdAt: new Date().toISOString() };
                const next = [v, ...savedViews].slice(0, 50);
                const defaultId = setAsDefault ? id : ((typeof window !== 'undefined' ? localStorage.getItem(queueDefaultViewStorageKey) : null) || null);

                persistViews(next, defaultId);
                setSelectedViewId(id);
                setSaveOpen(false);
                toast({ title: 'Saved', description: 'View saved successfully.', variant: 'success' });
              }}
            >
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={manageOpen} onOpenChange={setManageOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Manage views</DialogTitle>
            <DialogDescription>Set a default view or delete views you no longer need.</DialogDescription>
          </DialogHeader>

          <div className="space-y-2">
            {savedViews.length ? (
              <div className="space-y-2">
                {savedViews
                  .slice()
                  .sort((a, b) => a.name.localeCompare(b.name))
                  .map((v) => {
                    const defaultId = (typeof window !== 'undefined' ? localStorage.getItem(queueDefaultViewStorageKey) : null) || '';
                    const isDefault = defaultId && v.id === defaultId;
                    return (
                      <div key={v.id} className="flex items-center justify-between gap-3 rounded-md border p-2">
                        <div className="min-w-0">
                          <div className="text-sm font-medium text-slate-900 truncate">{v.name}</div>
                          <div className="text-xs text-slate-500">Saved {new Date(v.createdAt).toLocaleString()}</div>
                        </div>
                        <div className="flex items-center gap-2">
                          <Button
                            type="button"
                            size="sm"
                            variant={isDefault ? 'default' : 'outline'}
                            onClick={() => persistViews(savedViews, v.id)}
                            title="Set as default"
                          >
                            {isDefault ? 'Default' : 'Make default'}
                          </Button>
                          <Button
                            type="button"
                            size="icon"
                            variant="outline"
                            title="Delete view"
                            onClick={() => {
                              const next = savedViews.filter((x) => x.id !== v.id);
                              const defaultId = (typeof window !== 'undefined' ? localStorage.getItem(queueDefaultViewStorageKey) : null) || '';
                              const newDefaultId = defaultId === v.id ? null : defaultId || null;
                              persistViews(next, newDefaultId);
                              if (selectedViewId === v.id) setSelectedViewId('');
                              toast({ title: 'Deleted', description: 'View removed.', variant: 'success' });
                            }}
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </div>
                      </div>
                    );
                  })}
              </div>
            ) : (
              <div className="text-sm text-slate-600">No saved views yet.</div>
            )}

            <div className="flex items-center justify-between gap-2 pt-2">
              <Button
                type="button"
                variant="outline"
                onClick={() => {
                  persistViews([], null);
                  setSelectedViewId('');
                  toast({ title: 'Cleared', description: 'All saved views were removed.', variant: 'success' });
                }}
                disabled={!savedViews.length}
              >
                <Trash2 className="h-4 w-4 mr-2" />
                Clear all
              </Button>

              <Button
                type="button"
                variant="outline"
                onClick={() => {
                  persistViews(savedViews, null);
                  toast({ title: 'Default cleared', description: 'Default view removed.', variant: 'success' });
                }}
              >
                Default: none
              </Button>
            </div>
          </div>

          <DialogFooter>
            <Button type="button" onClick={() => setManageOpen(false)}>
              Done
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <DataTable
        compact
        data={data || []}
        columns={columns}
        loading={isLoading}
        error={error ? 'Failed to load queue.' : null}
        enableColumnFilters={false}
        enableExport={true}
        exportFileName={`helpdesk-queue-${new Date().toISOString().slice(0, 10)}`}
        exportFormats={['csv', 'excel']}
        rowActions={rowActions}
        onRowDoubleClick={(row) => router.push(`/helpdesk/tickets/${row.original.id}`)}
        emptyStateMessage="No unassigned tickets match the current filter."
        toolbarActions={{
          refresh: () => refetch(),
        }}
      />
    </div>
  );
}
