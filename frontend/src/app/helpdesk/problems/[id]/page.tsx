'use client';

import { useMemo, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, ExternalLink, Pencil, Plus, Save, Trash2 } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/hooks/use-toast';
import { ehcInternalTicketService, type EhcAdminCategory, type EhcTicketLookupTicket } from '@/services/ehcInternalTicketService';
import { ehcProblemService, type EhcCapaTask, type EhcCapaTaskStatus, type EhcProblemDetail, type EhcProblemStatus, type UpdateEhcProblemRequest } from '@/services/ehcProblemService';
import type { EhcTicketPriority, EhcTicketStatus } from '@/services/ehcTicketService';

type CategoryNode = EhcAdminCategory & { children?: CategoryNode[] };

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
    const out: Array<{ label: string; categoryId: string; subcategoryId?: string; depth: number }> = [];
    for (const n of nodes) {
      const effectiveRootId = rootId ?? n.id;
      out.push({ label: n.name, categoryId: effectiveRootId, subcategoryId: rootId ? n.id : undefined, depth });
      if (n.children?.length) out.push(...flatten(n.children, effectiveRootId, depth + 1));
    }
    return out;
  };
  return flatten(roots).map((o) => ({ ...o, label: `${'— '.repeat(o.depth)}${o.label}` }));
};

const problemStatusBadgeClassName = (s: EhcProblemStatus) => {
  switch (s) {
    case 'Resolved':
      return 'bg-green-600 text-white';
    case 'Closed':
      return 'bg-slate-600 text-white';
    default:
      return 'bg-blue-600 text-white';
  }
};

const ticketStatusBadgeClassName = (s: EhcTicketStatus) => {
  switch (s) {
    case 'Resolved':
      return 'bg-green-600 text-white';
    case 'Closed':
      return 'bg-slate-600 text-white';
    default:
      return 'bg-blue-600 text-white';
  }
};

const priorityBadgeClassName = (p: EhcTicketPriority) => {
  switch (p) {
    case 'Critical':
      return 'bg-red-600 text-white';
    case 'High':
      return 'bg-orange-600 text-white';
    case 'Medium':
      return 'bg-yellow-400 text-slate-900';
    case 'Low':
      return 'bg-green-600 text-white';
    default:
      return 'bg-slate-600 text-white';
  }
};

const capaStatusBadgeClassName = (s: EhcCapaTaskStatus) => {
  switch (s) {
    case 'Done':
      return 'bg-green-600 text-white';
    case 'Cancelled':
      return 'bg-slate-600 text-white';
    case 'InProgress':
      return 'bg-blue-600 text-white';
    default:
      return 'bg-amber-500 text-white';
  }
};

export default function HelpdeskProblemDetailPage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  const problemId = params?.id;
  const qc = useQueryClient();
  const { toast } = useToast();

  const formatDateTime = (iso: string | null | undefined) => {
    if (!iso) return '—';
    const dt = new Date(iso);
    if (Number.isNaN(dt.getTime())) return String(iso);
    return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(dt);
  };

  const { data: problem, isLoading, error } = useQuery({
    queryKey: ['ehc', 'internal', 'problem', problemId],
    queryFn: () => ehcProblemService.getProblem(problemId),
    enabled: Boolean(problemId),
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

  const categoryOptions = useMemo(() => buildCategoryOptions(categories || []), [categories]);

  const { data: rootCauses } = useQuery({
    queryKey: ['ehc', 'internal', 'lookups', 'root-causes'],
    queryFn: () => ehcInternalTicketService.listRootCauses(),
  });

  const [isEditing, setIsEditing] = useState(false);
  const [form, setForm] = useState<UpdateEhcProblemRequest | null>(null);

  const startEdit = (p: EhcProblemDetail) => {
    setForm({
      title: p.title,
      description: p.description,
      priority: p.priority,
      status: p.status,
      categoryId: p.categoryId || null,
      subcategoryId: p.subcategoryId || null,
      departmentId: p.departmentId || null,
      ownerUserId: p.ownerUserId || null,
      rootCauseId: p.rootCauseId || null,
      rootCauseDetails: p.rootCauseDetails || null,
      resolutionSummary: p.resolutionSummary || null,
    });
    setIsEditing(true);
  };

  const save = useMutation({
    mutationFn: async () => {
      if (!problemId) throw new Error('Problem id is required');
      if (!form) throw new Error('No changes to save');
      const payload: UpdateEhcProblemRequest = {
        title: (form.title || '').trim(),
        description: (form.description || '').trim(),
        priority: form.priority || 'Medium',
        status: form.status,
        categoryId: form.categoryId || null,
        subcategoryId: form.subcategoryId || null,
        departmentId: form.departmentId || null,
        ownerUserId: form.ownerUserId || null,
        rootCauseId: form.rootCauseId || null,
        rootCauseDetails: (form.rootCauseDetails || '').trim() || null,
        resolutionSummary: (form.resolutionSummary || '').trim() || null,
      };
      if (!payload.title) throw new Error('Title is required');
      if (!payload.description) throw new Error('Description is required');
      return ehcProblemService.updateProblem(problemId, payload);
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'problem', problemId] });
      toast({ title: 'Saved', description: 'Problem updated successfully.', variant: 'success' });
      setIsEditing(false);
    },
    onError: (err: any) => {
      toast({ title: 'Error', description: err?.message || 'Failed to save problem.', variant: 'destructive' });
    },
  });

  const remove = useMutation({
    mutationFn: async () => {
      if (!problemId) throw new Error('Problem id is required');
      await ehcProblemService.deleteProblem(problemId);
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'problems'] });
      toast({ title: 'Deleted', description: 'Problem deleted.', variant: 'success' });
      router.push('/helpdesk/problems');
    },
    onError: (err: any) => {
      toast({ title: 'Error', description: err?.message || 'Failed to delete problem.', variant: 'destructive' });
    },
  });

  const [linkOpen, setLinkOpen] = useState(false);
  const [linkSearch, setLinkSearch] = useState('');
  const [linkNotes, setLinkNotes] = useState('');
  const [selectedTicketId, setSelectedTicketId] = useState<string>('');

  const { data: ticketSearchResults, isLoading: ticketSearchLoading } = useQuery({
    queryKey: ['ehc', 'internal', 'lookups', 'tickets', linkSearch],
    queryFn: () => ehcInternalTicketService.searchTickets(linkSearch, 20),
    enabled: linkSearch.trim().length >= 2,
  });

  const linkTicket = useMutation({
    mutationFn: async () => {
      if (!problemId) throw new Error('Problem id is required');
      if (!selectedTicketId) throw new Error('Select a ticket');
      await ehcProblemService.linkTicket(problemId, { ticketId: selectedTicketId, notes: linkNotes.trim() || null });
    },
    onSuccess: async () => {
      setLinkOpen(false);
      setLinkSearch('');
      setLinkNotes('');
      setSelectedTicketId('');
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'problem', problemId] });
      toast({ title: 'Linked', description: 'Ticket linked to problem.', variant: 'success' });
    },
    onError: (err: any) => {
      toast({ title: 'Error', description: err?.message || 'Failed to link ticket.', variant: 'destructive' });
    },
  });

  const unlinkTicket = useMutation({
    mutationFn: async (ticketId: string) => {
      if (!problemId) throw new Error('Problem id is required');
      await ehcProblemService.unlinkTicket(problemId, ticketId);
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'problem', problemId] });
      toast({ title: 'Unlinked', description: 'Ticket removed from problem.', variant: 'success' });
    },
    onError: (err: any) => {
      toast({ title: 'Error', description: err?.message || 'Failed to unlink ticket.', variant: 'destructive' });
    },
  });

  const [taskOpen, setTaskOpen] = useState(false);
  const [editingTask, setEditingTask] = useState<EhcCapaTask | null>(null);
  const [taskForm, setTaskForm] = useState<{
    title: string;
    description: string;
    status: EhcCapaTaskStatus;
    assignedToUserId: string;
    assignedDepartmentId: string;
    dueAt: string;
  }>({ title: '', description: '', status: 'Open', assignedToUserId: '', assignedDepartmentId: '', dueAt: '' });

  const openCreateTask = () => {
    setEditingTask(null);
    setTaskForm({ title: '', description: '', status: 'Open', assignedToUserId: '', assignedDepartmentId: '', dueAt: '' });
    setTaskOpen(true);
  };

  const openEditTask = (t: EhcCapaTask) => {
    setEditingTask(t);
    setTaskForm({
      title: t.title || '',
      description: t.description || '',
      status: t.status,
      assignedToUserId: t.assignedToUserId || '',
      assignedDepartmentId: t.assignedDepartmentId || '',
      dueAt: t.dueAt ? String(t.dueAt).slice(0, 10) : '',
    });
    setTaskOpen(true);
  };

  const saveTask = useMutation({
    mutationFn: async () => {
      if (!problemId) throw new Error('Problem id is required');
      const payload = {
        title: taskForm.title.trim(),
        description: taskForm.description.trim() || null,
        assignedToUserId: taskForm.assignedToUserId || null,
        assignedDepartmentId: taskForm.assignedDepartmentId || null,
        dueAt: taskForm.dueAt ? new Date(taskForm.dueAt).toISOString() : null,
        status: taskForm.status,
      };
      if (!payload.title) throw new Error('Title is required');
      if (editingTask) return ehcProblemService.updateCapaTask(problemId, editingTask.id, payload);
      return ehcProblemService.createCapaTask(problemId, payload);
    },
    onSuccess: async () => {
      setTaskOpen(false);
      setEditingTask(null);
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'problem', problemId] });
      toast({ title: 'Saved', description: 'CAPA task saved successfully.', variant: 'success' });
    },
    onError: (err: any) => {
      toast({ title: 'Error', description: err?.message || 'Failed to save CAPA task.', variant: 'destructive' });
    },
  });

  const deleteTask = useMutation({
    mutationFn: async (taskId: string) => {
      if (!problemId) throw new Error('Problem id is required');
      await ehcProblemService.deleteCapaTask(problemId, taskId);
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'problem', problemId] });
      toast({ title: 'Deleted', description: 'CAPA task deleted.', variant: 'success' });
    },
    onError: (err: any) => {
      toast({ title: 'Error', description: err?.message || 'Failed to delete CAPA task.', variant: 'destructive' });
    },
  });

  if (isLoading) return <div className="p-6 text-slate-600">Loading problem…</div>;
  if (error) return <div className="p-6 text-red-600">Failed to load problem.</div>;
  if (!problem) return <div className="p-6 text-slate-600">Problem not found.</div>;

  const p = problem as EhcProblemDetail;
  const view = isEditing && form ? form : null;
  const selectedCategoryValue = view ? view.subcategoryId || view.categoryId || '' : p.subcategoryId || p.categoryId || '';
  const linkedTickets = p.linkedTickets ?? [];
  const capaTasks = p.capaTasks ?? [];
  const audit = p.auditTrail ?? [];

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div className="flex items-start gap-3">
          <Button variant="outline" size="icon" onClick={() => router.push('/helpdesk/problems')} title="Back">
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-2xl font-bold">{p.problemNumber}</h1>
              <Badge className={problemStatusBadgeClassName(p.status)}>{p.status}</Badge>
              <Badge className={priorityBadgeClassName(p.priority)}>{p.priority}</Badge>
            </div>
            <div className="text-sm text-slate-600">{p.title}</div>
          </div>
        </div>

        <div className="flex items-center gap-2">
          {!isEditing ? (
            <Button variant="outline" onClick={() => startEdit(p)}>
              <Pencil className="h-4 w-4 mr-2" />
              Edit
            </Button>
          ) : (
            <>
              <Button variant="outline" onClick={() => setIsEditing(false)}>
                Cancel
              </Button>
              <Button onClick={() => save.mutate()} disabled={save.isPending}>
                <Save className="h-4 w-4 mr-2" />
                {save.isPending ? 'Saving…' : 'Save'}
              </Button>
            </>
          )}
          <Button variant="destructive" onClick={() => remove.mutate()} disabled={remove.isPending}>
            <Trash2 className="h-4 w-4 mr-2" />
            Delete
          </Button>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Details</CardTitle>
          <CardDescription>RCA + ownership fields.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Title</Label>
              {isEditing && view ? (
                <Input value={view.title || ''} onChange={(e) => setForm((f) => ({ ...(f as any), title: e.target.value }))} />
              ) : (
                <div className="text-sm text-slate-900 font-medium">{p.title}</div>
              )}
            </div>
            <div className="space-y-2">
              <Label>Status</Label>
              {isEditing && view ? (
                <select className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm" value={view.status} onChange={(e) => setForm((f) => ({ ...(f as any), status: e.target.value as any }))}>
                  <option value="Open">Open</option>
                  <option value="InProgress">In Progress</option>
                  <option value="Resolved">Resolved</option>
                  <option value="Closed">Closed</option>
                </select>
              ) : (
                <Badge className={problemStatusBadgeClassName(p.status)}>{p.status}</Badge>
              )}
            </div>
          </div>

          <div className="space-y-2">
            <Label>Description</Label>
            {isEditing && view ? (
              <Textarea value={view.description || ''} onChange={(e) => setForm((f) => ({ ...(f as any), description: e.target.value }))} rows={5} />
            ) : (
              <div className="text-sm text-slate-700 whitespace-pre-wrap">{p.description}</div>
            )}
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="space-y-2">
              <Label>Priority</Label>
              {isEditing && view ? (
                <select className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm" value={view.priority} onChange={(e) => setForm((f) => ({ ...(f as any), priority: e.target.value as any }))}>
                  <option value="Low">Low</option>
                  <option value="Medium">Medium</option>
                  <option value="High">High</option>
                  <option value="Critical">Critical</option>
                </select>
              ) : (
                <Badge className={priorityBadgeClassName(p.priority)}>{p.priority}</Badge>
              )}
            </div>

            <div className="space-y-2">
              <Label>Department</Label>
              {isEditing && view ? (
                <select className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm" value={view.departmentId || ''} onChange={(e) => setForm((f) => ({ ...(f as any), departmentId: e.target.value || null }))}>
                  <option value="">(optional)</option>
                  {(departments || []).map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.name}
                    </option>
                  ))}
                </select>
              ) : (
                <div className="text-sm text-slate-700">{p.departmentName || '—'}</div>
              )}
            </div>

            <div className="space-y-2">
              <Label>Owner</Label>
              {isEditing && view ? (
                <select className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm" value={view.ownerUserId || ''} onChange={(e) => setForm((f) => ({ ...(f as any), ownerUserId: e.target.value || null }))}>
                  <option value="">(optional)</option>
                  {(agents || []).map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.name}
                    </option>
                  ))}
                </select>
              ) : (
                <div className="text-sm text-slate-700">{p.ownerName || '—'}</div>
              )}
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Category</Label>
              {isEditing && view ? (
                <select
                  className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                  value={selectedCategoryValue}
                  onChange={(e) =>
                    setForm((prev) => {
                      const base = prev as any;
                      const value = e.target.value || '';
                      if (!value) return { ...base, categoryId: null, subcategoryId: null };
                      const selected = categoryOptions.find((o) => (o.subcategoryId || o.categoryId) === value);
                      if (!selected) return { ...base, categoryId: value, subcategoryId: null };
                      return { ...base, categoryId: selected.categoryId, subcategoryId: selected.subcategoryId || null };
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
              ) : (
                <div className="text-sm text-slate-700">{p.subcategoryName ? `${p.categoryName} → ${p.subcategoryName}` : p.categoryName || '—'}</div>
              )}
            </div>

            <div className="space-y-2">
              <Label>Root cause</Label>
              {isEditing && view ? (
                <select className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm" value={view.rootCauseId || ''} onChange={(e) => setForm((f) => ({ ...(f as any), rootCauseId: e.target.value || null }))}>
                  <option value="">(optional)</option>
                  {(rootCauses ?? [])
                    .filter((x) => x?.isActive)
                    .sort((a, b) => String(a.code || '').localeCompare(String(b.code || '')))
                    .map((x) => (
                      <option key={x.id} value={x.id}>
                        {x.code} • {x.name}
                      </option>
                    ))}
                </select>
              ) : (
                <div className="text-sm text-slate-700">{p.rootCauseName || '—'}</div>
              )}
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Root cause details</Label>
              {isEditing && view ? (
                <Textarea value={view.rootCauseDetails || ''} onChange={(e) => setForm((f) => ({ ...(f as any), rootCauseDetails: e.target.value }))} rows={3} />
              ) : (
                <div className="text-sm text-slate-700 whitespace-pre-wrap">{p.rootCauseDetails || '—'}</div>
              )}
            </div>
            <div className="space-y-2">
              <Label>Resolution summary</Label>
              {isEditing && view ? (
                <Textarea value={view.resolutionSummary || ''} onChange={(e) => setForm((f) => ({ ...(f as any), resolutionSummary: e.target.value }))} rows={3} />
              ) : (
                <div className="text-sm text-slate-700 whitespace-pre-wrap">{p.resolutionSummary || '—'}</div>
              )}
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm text-slate-600">
            <div>
              Created <span className="font-medium text-slate-900">{formatDateTime(p.createdAt)}</span>
            </div>
            <div>
              {p.updatedAt ? (
                <>
                  Updated <span className="font-medium text-slate-900">{formatDateTime(p.updatedAt)}</span>
                </>
              ) : (
                <span>Updated —</span>
              )}
            </div>
          </div>

          {p.createdFromTicketId ? (
            <div className="rounded-md border bg-slate-50 p-3 text-sm flex items-center justify-between gap-3">
              <div>
                Created from ticket <span className="font-semibold text-slate-900">{p.createdFromTicketNumber || p.createdFromTicketId}</span>
              </div>
              <Button variant="outline" size="icon" onClick={() => router.push(`/helpdesk/tickets/${p.createdFromTicketId}`)} title="Open ticket">
                <ExternalLink className="h-4 w-4" />
              </Button>
            </div>
          ) : null}
        </CardContent>
      </Card>

      <Tabs defaultValue="tickets">
        <TabsList>
          <TabsTrigger value="tickets">Linked tickets</TabsTrigger>
          <TabsTrigger value="tasks">CAPA tasks</TabsTrigger>
          <TabsTrigger value="audit">Audit</TabsTrigger>
        </TabsList>

        <TabsContent value="tickets" className="space-y-4">
          <div className="flex items-center justify-between gap-3">
            <div className="text-sm text-slate-600">Link incidents/tickets that belong to this recurring problem.</div>
            <Button type="button" onClick={() => setLinkOpen(true)}>
              <Plus className="h-4 w-4 mr-2" />
              Link ticket
            </Button>
          </div>

          <div className="rounded-lg border bg-white p-3">
            {linkedTickets.length ? (
              <div className="space-y-2">
                {linkedTickets.map((t) => (
                  <div key={t.ticketId} className="rounded-md border bg-white p-3">
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                          <button type="button" className="text-sm font-semibold text-slate-900 hover:underline" onClick={() => router.push(`/helpdesk/tickets/${t.ticketId}`)} title="Open ticket">
                            {t.ticketNumber}
                          </button>
                          <Badge className={ticketStatusBadgeClassName(t.status as EhcTicketStatus)}>{t.status}</Badge>
                          <Badge className={priorityBadgeClassName(t.priority as EhcTicketPriority)}>{t.priority}</Badge>
                        </div>
                        {t.subject ? <div className="mt-1 text-sm text-slate-700 line-clamp-2">{t.subject}</div> : null}
                        <div className="mt-1 text-xs text-slate-500">Created: {formatDateTime(t.createdAt)} • Linked: {formatDateTime(t.linkedAt)}</div>
                        {t.notes ? <div className="mt-1 text-xs text-slate-600">Notes: {t.notes}</div> : null}
                      </div>

                      <div className="flex items-center gap-2">
                        <Button type="button" size="icon" variant="outline" title="Open" onClick={() => router.push(`/helpdesk/tickets/${t.ticketId}`)}>
                          <ExternalLink className="h-4 w-4" />
                        </Button>
                        <Button type="button" size="icon" variant="outline" title="Unlink" disabled={unlinkTicket.isPending} onClick={() => unlinkTicket.mutate(t.ticketId)}>
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <div className="text-sm text-slate-600">No linked tickets yet.</div>
            )}
          </div>
        </TabsContent>

        <TabsContent value="tasks" className="space-y-4">
          <div className="flex items-center justify-between gap-3">
            <div className="text-sm text-slate-600">Corrective and preventive actions for this problem.</div>
            <Button type="button" onClick={openCreateTask}>
              <Plus className="h-4 w-4 mr-2" />
              Add task
            </Button>
          </div>

          <div className="rounded-lg border bg-white p-3">
            {capaTasks.length ? (
              <div className="space-y-2">
                {capaTasks.map((t) => (
                  <div key={t.id} className="rounded-md border bg-white p-3">
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                          <div className="text-sm font-semibold text-slate-900">{t.title}</div>
                          <Badge className={capaStatusBadgeClassName(t.status as EhcCapaTaskStatus)}>{t.status}</Badge>
                        </div>
                        {t.description ? <div className="mt-1 text-sm text-slate-700 whitespace-pre-wrap">{t.description}</div> : null}
                        <div className="mt-2 text-xs text-slate-500">
                          {t.assignedToName ? <span>Assignee: {t.assignedToName}</span> : <span>Assignee: —</span>}
                          <span> • </span>
                          {t.assignedDepartmentName ? <span>Dept: {t.assignedDepartmentName}</span> : <span>Dept: —</span>}
                          <span> • </span>
                          {t.dueAt ? <span>Due: {formatDateTime(t.dueAt)}</span> : <span>Due: —</span>}
                        </div>
                      </div>

                      <div className="flex items-center gap-2">
                        <Button type="button" size="icon" variant="outline" title="Edit" onClick={() => openEditTask(t)}>
                          <Pencil className="h-4 w-4" />
                        </Button>
                        <Button type="button" size="icon" variant="outline" title="Delete" disabled={deleteTask.isPending} onClick={() => deleteTask.mutate(t.id)}>
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <div className="text-sm text-slate-600">No CAPA tasks yet.</div>
            )}
          </div>
        </TabsContent>

        <TabsContent value="audit" className="space-y-2">
          {audit.length ? (
            <div className="space-y-2">
              {audit.map((e) => (
                <div key={e.id} className="rounded-md border bg-white p-3">
                  <div className="flex items-center justify-between gap-3">
                    <div className="font-medium text-slate-900">{e.title || e.eventType}</div>
                    <div className="text-xs text-slate-500 whitespace-nowrap">{formatDateTime(e.createdAt)}</div>
                  </div>
                  {e.body ? <div className="mt-1 text-sm text-slate-700 whitespace-pre-wrap">{e.body}</div> : null}
                  {e.actorName ? <div className="mt-1 text-xs text-slate-500">By {e.actorName}</div> : null}
                </div>
              ))}
            </div>
          ) : (
            <div className="text-sm text-slate-600">No audit events yet.</div>
          )}
        </TabsContent>
      </Tabs>

      <Dialog open={linkOpen} onOpenChange={setLinkOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Link ticket</DialogTitle>
            <DialogDescription>Search for a ticket and link it to this problem.</DialogDescription>
          </DialogHeader>

          <div className="space-y-3">
            <div className="space-y-2">
              <Label>Search</Label>
              <Input value={linkSearch} onChange={(e) => setLinkSearch(e.target.value)} placeholder="Ticket number or subject…" />
              <div className="text-xs text-slate-500">Type at least 2 characters.</div>
            </div>

            <div className="rounded-md border bg-white p-2 max-h-72 overflow-auto">
              {ticketSearchLoading ? (
                <div className="p-2 text-sm text-slate-600">Searching…</div>
              ) : (ticketSearchResults ?? []).length ? (
                <div className="space-y-1">
                  {(ticketSearchResults ?? []).map((t: EhcTicketLookupTicket) => {
                    const active = selectedTicketId === t.id;
                    return (
                      <button
                        type="button"
                        key={t.id}
                        className={`w-full text-left rounded-md border px-3 py-2 hover:bg-slate-50 ${active ? 'border-blue-600 bg-blue-50' : 'border-slate-200 bg-white'}`}
                        onClick={() => setSelectedTicketId(t.id)}
                      >
                        <div className="flex items-center justify-between gap-2">
                          <div className="font-semibold text-slate-900">{t.ticketNumber}</div>
                          <div className="text-xs text-slate-600">{formatDateTime(t.createdAt)}</div>
                        </div>
                        <div className="text-sm text-slate-700 line-clamp-2">{t.subject}</div>
                        <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-slate-600">
                          <Badge className={ticketStatusBadgeClassName(t.status as EhcTicketStatus)}>{t.status}</Badge>
                          <Badge className={priorityBadgeClassName(t.priority as EhcTicketPriority)}>{t.priority}</Badge>
                        </div>
                      </button>
                    );
                  })}
                </div>
              ) : (
                <div className="p-2 text-sm text-slate-600">No results.</div>
              )}
            </div>

            <div className="space-y-2">
              <Label>Notes (optional)</Label>
              <Input value={linkNotes} onChange={(e) => setLinkNotes(e.target.value)} placeholder="Why is this ticket linked?" />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setLinkOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => linkTicket.mutate()} disabled={linkTicket.isPending}>
              {linkTicket.isPending ? 'Linking…' : 'Link'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={taskOpen} onOpenChange={setTaskOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>{editingTask ? 'Edit CAPA task' : 'Add CAPA task'}</DialogTitle>
            <DialogDescription>Track corrective and preventive actions.</DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>
                  Title <span className="text-red-600">*</span>
                </Label>
                <Input value={taskForm.title} onChange={(e) => setTaskForm((f) => ({ ...f, title: e.target.value }))} />
              </div>
              <div className="space-y-2">
                <Label>Status</Label>
                <select className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm" value={taskForm.status} onChange={(e) => setTaskForm((f) => ({ ...f, status: e.target.value as any }))}>
                  <option value="Open">Open</option>
                  <option value="InProgress">In Progress</option>
                  <option value="Done">Done</option>
                  <option value="Cancelled">Cancelled</option>
                </select>
              </div>
            </div>

            <div className="space-y-2">
              <Label>Description</Label>
              <Textarea value={taskForm.description} onChange={(e) => setTaskForm((f) => ({ ...f, description: e.target.value }))} rows={4} />
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>Assignee</Label>
                <select className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm" value={taskForm.assignedToUserId} onChange={(e) => setTaskForm((f) => ({ ...f, assignedToUserId: e.target.value }))}>
                  <option value="">(optional)</option>
                  {(agents || []).map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="space-y-2">
                <Label>Department</Label>
                <select className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm" value={taskForm.assignedDepartmentId} onChange={(e) => setTaskForm((f) => ({ ...f, assignedDepartmentId: e.target.value }))}>
                  <option value="">(optional)</option>
                  {(departments || []).map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.name}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="space-y-2">
              <Label>Due date</Label>
              <input className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm" type="date" value={taskForm.dueAt} onChange={(e) => setTaskForm((f) => ({ ...f, dueAt: e.target.value }))} />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setTaskOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => saveTask.mutate()} disabled={saveTask.isPending}>
              {saveTask.isPending ? 'Saving…' : 'Save task'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
