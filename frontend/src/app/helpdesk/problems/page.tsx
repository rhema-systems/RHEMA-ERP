'use client';

import { useCallback, useMemo, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Eye, Plus, TriangleAlert } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { DataTable, type DataTableAction, type DataTableColumn } from '@/components/ui/DataTable';
import { useToast } from '@/hooks/use-toast';
import { getHelpdeskScopeConfig } from '@/lib/helpdesk-scope';
import { ehcInternalTicketService, type EhcAdminCategory } from '@/services/ehcInternalTicketService';
import { ehcProblemService, type CreateEhcProblemRequest, type EhcProblemListItem, type EhcProblemStatus } from '@/services/ehcProblemService';
import type { EhcTicketPriority } from '@/services/ehcTicketService';

type CategoryNode = EhcAdminCategory & { children?: CategoryNode[] };

type TableCellProps<T> = {
  row: {
    original: T;
  };
};

const buildTree = (flat: EhcAdminCategory[]): CategoryNode[] => {
  const byId = new Map<string, CategoryNode>();
  for (const c of flat) byId.set(c.id, { ...c, children: [] });
  const roots: CategoryNode[] = [];
  for (const c of byId.values()) {
    const parent = c.parentCategoryId ? byId.get(c.parentCategoryId) : undefined;
    if (parent?.children) {
      parent.children.push(c);
    } else {
      roots.push(c);
    }
  }
  const sortRec = (nodes: CategoryNode[]) => {
    nodes.sort((a, b) => String(a.name || '').localeCompare(String(b.name || '')));
    for (const n of nodes) if (n.children?.length) sortRec(n.children);
  };
  sortRec(roots);
  return roots;
};

const problemStatusBadgeClassName = (s: EhcProblemStatus) => {
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

export default function HelpdeskProblemsPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const qc = useQueryClient();
  const { toast } = useToast();
  const scopeParam = searchParams?.get('scope');
  const scopeConfig = useMemo(() => getHelpdeskScopeConfig(scopeParam), [scopeParam]);

  const [q, setQ] = useState('');
  const [status, setStatus] = useState<EhcProblemStatus | ''>('');
  const [priority, setPriority] = useState<EhcTicketPriority | ''>('');
  const [departmentId, setDepartmentId] = useState('');
  const [ownerUserId, setOwnerUserId] = useState('');
  const [createdFrom, setCreatedFrom] = useState('');
  const [createdTo, setCreatedTo] = useState('');

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

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['ehc', 'internal', 'problems', { q, status, priority, departmentId, ownerUserId, createdFrom, createdTo }],
    queryFn: () =>
      ehcProblemService.listProblems(1, 500, {
        q: q.trim() || null,
        status: status || null,
        priority: priority || null,
        departmentId: departmentId || null,
        ownerUserId: ownerUserId || null,
        createdFrom: createdFrom || null,
        createdTo: createdTo || null,
      }),
  });

  const { data: departments } = useQuery({
    queryKey: ['ehc', 'internal', 'departments'],
    queryFn: () => ehcInternalTicketService.listDepartments(),
  });

  const { data: agents } = useQuery({
    queryKey: ['ehc', 'internal', 'agents'],
    queryFn: () => ehcInternalTicketService.listAgents(),
  });

  const { data: categories } = useQuery({
    queryKey: ['ehc', 'admin', 'categories'],
    queryFn: () => ehcInternalTicketService.listCategories(),
  });

  const tree = useMemo(() => buildTree(categories || []), [categories]);
  const categoryOptions = useMemo(() => {
    const flatten = (nodes: CategoryNode[], rootId: string | null = null, depth = 0): Array<{ label: string; categoryId: string; subcategoryId?: string; depth: number }> => {
      const out: Array<{ label: string; categoryId: string; subcategoryId?: string; depth: number }> = [];
      for (const n of nodes) {
        const effectiveRootId = rootId ?? n.id;
        out.push({ label: n.name, categoryId: effectiveRootId, subcategoryId: rootId ? n.id : undefined, depth });
        if (n.children?.length) out.push(...flatten(n.children, effectiveRootId, depth + 1));
      }
      return out;
    };
    return flatten(tree).map((o) => ({ ...o, label: `${'— '.repeat(o.depth)}${o.label}` }));
  }, [tree]);

  const columns = useMemo<Array<DataTableColumn<EhcProblemListItem>>>(() => {
    return [
      {
        id: 'problemNumber',
        header: 'Problem #',
        accessorKey: 'problemNumber',
        cell: ({ row }: TableCellProps<EhcProblemListItem>) => (
          <button
            className="text-blue-600 hover:underline"
            onClick={() => router.push(scopeParam ? `/helpdesk/problems/${row.original.id}?scope=${scopeParam}` : `/helpdesk/problems/${row.original.id}`)}
            title="Open problem"
          >
            {row.original.problemNumber}
          </button>
        ),
      },
      { id: 'title', header: 'Title', accessorKey: 'title' },
      {
        id: 'status',
        header: 'Status',
        accessorKey: 'status',
        cell: ({ row }: TableCellProps<EhcProblemListItem>) => <Badge className={problemStatusBadgeClassName(row.original.status)}>{row.original.status}</Badge>,
      },
      {
        id: 'priority',
        header: 'Priority',
        accessorKey: 'priority',
        cell: ({ row }: TableCellProps<EhcProblemListItem>) => <Badge className={priorityBadgeClassName(row.original.priority)}>{row.original.priority}</Badge>,
      },
      { id: 'departmentName', header: 'Department', accessorKey: 'departmentName' },
      { id: 'ownerName', header: 'Owner', accessorKey: 'ownerName' },
      { id: 'linkedTicketsCount', header: 'Tickets', accessorKey: 'linkedTicketsCount' },
      {
        id: 'createdAt',
        header: 'Created',
        accessorKey: 'createdAt',
        cell: ({ row }: TableCellProps<EhcProblemListItem>) => <span className="text-slate-700">{formatCreatedAt(row.original.createdAt)}</span>,
      },
    ];
  }, [formatCreatedAt, router]);

  const rowActions = useMemo<Array<DataTableAction<EhcProblemListItem>>>(() => {
    return [
      {
        id: 'view',
        label: 'View',
        icon: Eye as any,
        onClick: (row) => router.push(scopeParam ? `/helpdesk/problems/${row.original.id}?scope=${scopeParam}` : `/helpdesk/problems/${row.original.id}`),
      },
    ];
  }, [router, scopeParam]);

  const [open, setOpen] = useState(false);
  const [form, setForm] = useState<CreateEhcProblemRequest>({
    title: '',
    description: '',
    priority: 'Medium',
    categoryId: null,
    subcategoryId: null,
    departmentId: null,
    ownerUserId: null,
  });

  const startCreate = () => {
    setForm({
      title: '',
      description: '',
      priority: 'Medium',
      categoryId: null,
      subcategoryId: null,
      departmentId: null,
      ownerUserId: null,
    });
    setOpen(true);
  };

  const create = useMutation({
    mutationFn: async () => {
      const payload: CreateEhcProblemRequest = {
        title: (form.title || '').trim(),
        description: (form.description || '').trim(),
        priority: form.priority || 'Medium',
        categoryId: form.categoryId || null,
        subcategoryId: form.subcategoryId || null,
        departmentId: form.departmentId || null,
        ownerUserId: form.ownerUserId || null,
      };
      if (!payload.title) throw new Error('Title is required');
      if (!payload.description) throw new Error('Description is required');
      return ehcProblemService.createProblem(payload);
    },
    onSuccess: async (problem) => {
      setOpen(false);
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'problems'] });
      toast({ title: 'Created', description: `Problem ${problem.problemNumber} created.`, variant: 'success' });
      router.push(`/helpdesk/problems/${problem.id}`);
    },
    onError: (err: any) => {
      toast({ title: 'Error', description: err?.message || 'Failed to create problem.', variant: 'destructive' });
    },
  });

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold">{scopeParam ? `${scopeConfig.moduleLabel} Problems` : 'Problems'}</h1>
          <p className="text-slate-600">
            {scopeParam ? `Recurring issue register for the ${scopeConfig.listTitle.toLowerCase()} branch.` : 'Track recurring incidents with RCA + CAPA.'}
          </p>
        </div>
        <Button onClick={startCreate}>
          <Plus className="h-4 w-4 mr-2" />
          New problem
        </Button>
      </div>

      <Card className="border-slate-200">
        <CardHeader className="py-4">
          <CardTitle className="text-base">Filters</CardTitle>
          <CardDescription className="text-sm">Narrow down the problem list.</CardDescription>
        </CardHeader>
        <CardContent className="pt-0 grid grid-cols-1 md:grid-cols-6 gap-3">
          <div className="space-y-1 md:col-span-2">
            <Label className="text-xs text-slate-600">Search</Label>
            <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Problem # or title" />
          </div>

          <div className="space-y-1">
            <Label className="text-xs text-slate-600">Status</Label>
            <select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={status} onChange={(e) => setStatus((e.target.value || '') as any)}>
              <option value="">All</option>
              <option value="Open">Open</option>
              <option value="InProgress">In Progress</option>
              <option value="Resolved">Resolved</option>
              <option value="Closed">Closed</option>
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
            <select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}>
              <option value="">All</option>
              {(departments || []).map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </select>
          </div>

          <div className="space-y-1">
            <Label className="text-xs text-slate-600">Owner</Label>
            <select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={ownerUserId} onChange={(e) => setOwnerUserId(e.target.value)}>
              <option value="">All</option>
              {(agents || []).map((a) => (
                <option key={a.id} value={a.id}>
                  {a.name}
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
                setQ('');
                setStatus('');
                setPriority('');
                setDepartmentId('');
                setOwnerUserId('');
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
        data={data || []}
        columns={columns}
        loading={isLoading}
        error={error ? 'Failed to load problems.' : null}
        enableColumnFilters={false}
        enableExport={true}
        exportFileName={`helpdesk-problems-${new Date().toISOString().slice(0, 10)}`}
        exportFormats={['csv', 'excel']}
        rowActions={rowActions}
        onRowDoubleClick={(row) => router.push(scopeParam ? `/helpdesk/problems/${row.original.id}?scope=${scopeParam}` : `/helpdesk/problems/${row.original.id}`)}
        emptyStateMessage="No problems match the current filter."
        toolbarActions={{
          refresh: () => refetch(),
          create: () => startCreate(),
        }}
      />

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Create Problem</DialogTitle>
            <DialogDescription>Capture the recurring incident for RCA and CAPA tracking.</DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>
                  Title <span className="text-red-600">*</span>
                </Label>
                <Input value={form.title || ''} onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))} placeholder="Short title" />
              </div>

              <div className="space-y-2">
                <Label>Priority</Label>
                <select className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm" value={form.priority || 'Medium'} onChange={(e) => setForm((f) => ({ ...f, priority: e.target.value as any }))}>
                  <option value="Low">Low</option>
                  <option value="Medium">Medium</option>
                  <option value="High">High</option>
                  <option value="Critical">Critical</option>
                </select>
              </div>
            </div>

            <div className="space-y-2">
              <Label>
                Description <span className="text-red-600">*</span>
              </Label>
              <Textarea value={form.description || ''} onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))} rows={6} placeholder="Describe the recurring issue" />
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>Department</Label>
                <select className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm" value={form.departmentId || ''} onChange={(e) => setForm((f) => ({ ...f, departmentId: e.target.value || null }))}>
                  <option value="">(optional)</option>
                  {(departments || []).map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.name}
                    </option>
                  ))}
                </select>
              </div>

              <div className="space-y-2">
                <Label>Owner</Label>
                <select className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm" value={form.ownerUserId || ''} onChange={(e) => setForm((f) => ({ ...f, ownerUserId: e.target.value || null }))}>
                  <option value="">(optional)</option>
                  {(agents || []).map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.name}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="space-y-2">
              <Label>Category</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.subcategoryId || form.categoryId || ''}
                onChange={(e) =>
                  setForm((f) => {
                    const value = e.target.value || '';
                    if (!value) return { ...f, categoryId: null, subcategoryId: null };

                    const selected = categoryOptions.find((o) => (o.subcategoryId || o.categoryId) === value);
                    if (!selected) return { ...f, categoryId: value, subcategoryId: null };

                    return { ...f, categoryId: selected.categoryId, subcategoryId: selected.subcategoryId || null };
                  })
                }
              >
                <option value="">(optional)</option>
                {categoryOptions.map((o) => (
                  <option key={`${o.categoryId}:${o.subcategoryId ?? ''}`} value={o.subcategoryId || o.categoryId}>
                    {o.label}
                  </option>
                ))}
              </select>
            </div>

            <div className="rounded-md border bg-amber-50 p-3 text-sm text-amber-900 flex items-start gap-2">
              <TriangleAlert className="h-4 w-4 mt-0.5" />
              <div>Tip: Convert a ticket to a Problem from the ticket detail screen to automatically link the incident.</div>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => create.mutate()} disabled={create.isPending}>
              {create.isPending ? 'Creating…' : 'Create'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
