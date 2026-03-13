'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, BookOpen, Paperclip, Ticket } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import {
  buildScopedHelpdeskDetailPath,
  buildScopedHelpdeskTicketsPath,
  EXTERNAL_TICKET_SOURCES,
  getHelpdeskScopeConfig,
} from '@/lib/helpdesk-scope';
import { ehcInternalTicketService, type EhcAdminCategory, type EhcRelatedEntityLookupItem, type EhcKbArticleDetail, type EhcKbArticleListItem } from '@/services/ehcInternalTicketService';
import type { CreateEhcTicketRequest, EhcTicketPriority, EhcTicketSource, EhcTicketType } from '@/services/ehcTicketService';
import { fileUploadService } from '@/services/fileUploadService';

const relatedEntityTypeOptions = [
  { label: 'None', value: '' },
  { label: 'Asset', value: 'Asset' },
  { label: 'Vehicle', value: 'Vehicle' },
  { label: 'Work Order', value: 'WorkOrder' },
  { label: 'Purchase Order', value: 'PurchaseOrder' },
  { label: 'Purchase Requisition', value: 'PurchaseRequisition' },
  { label: 'Tender', value: 'Tender' },
  { label: 'RFQ', value: 'RFQ' },
  { label: 'Other', value: 'Other' },
];

const relatedLookupSupportedTypes = new Set([
  'Asset',
  'Vehicle',
  'WorkOrder',
  'PurchaseOrder',
  'PurchaseRequisition',
  'Tender',
  'RFQ',
]);

type CategoryNode = EhcAdminCategory & { children: CategoryNode[] };

const buildTree = (flat: EhcAdminCategory[]): CategoryNode[] => {
  const byId = new Map<string, CategoryNode>(flat.map((c) => [c.id, { ...c, children: [] }]));
  const roots: CategoryNode[] = [];

  for (const node of byId.values()) {
    const parentId = node.parentCategoryId || null;
    if (!parentId) {
      roots.push(node);
      continue;
    }
    const parent = byId.get(parentId);
    if (!parent) {
      roots.push(node);
      continue;
    }
    parent.children.push(node);
  }

  const sortRec = (nodes: CategoryNode[]) => {
    nodes.sort((a, b) => (a.name || '').localeCompare(b.name || ''));
    for (const n of nodes) sortRec(n.children);
  };
  sortRec(roots);

  return roots;
};

export default function NewInternalHelpdeskTicketPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const qc = useQueryClient();
  const { toast } = useToast();
  const scopeParam = searchParams.get('scope');
  const scopeConfig = useMemo(() => getHelpdeskScopeConfig(scopeParam), [scopeParam]);
  const typeLocked = scopeConfig.allowedTicketTypes.length === 1;
  const channelOptions = useMemo<Array<{ label: string; value: EhcTicketSource }>>(
    () =>
      scopeConfig.internalOnly
        ? [{ label: 'Internal', value: 'Internal' }]
        : [
            { label: 'Website', value: 'Web' },
            { label: 'Mobile App', value: 'Mobile' },
            { label: 'Email', value: 'Email' },
            { label: 'Phone Call', value: 'PhoneCall' },
            { label: 'SMS', value: 'Sms' },
            { label: 'WhatsApp', value: 'WhatsApp' },
          ],
    [scopeConfig.internalOnly],
  );

  const [files, setFiles] = useState<File[]>([]);
  const [attachmentInternalOnly, setAttachmentInternalOnly] = useState(true);
  const [relatedLookupOpen, setRelatedLookupOpen] = useState(false);
  const [relatedLookupQ, setRelatedLookupQ] = useState('');
  const [relatedLookupResults, setRelatedLookupResults] = useState<EhcRelatedEntityLookupItem[]>([]);
  const [relatedLookupLoading, setRelatedLookupLoading] = useState(false);
  const [relatedLookupError, setRelatedLookupError] = useState<string | null>(null);
  const [kbArticleOpen, setKbArticleOpen] = useState(false);
  const [kbArticleId, setKbArticleId] = useState<string | null>(null);

  const [form, setForm] = useState<CreateEhcTicketRequest>({
    ticketType: scopeConfig.defaultTicketType,
    priority: 'Medium',
    source: scopeConfig.internalOnly ? 'Internal' : EXTERNAL_TICKET_SOURCES[0],
    subject: '',
    description: '',
    categoryId: undefined,
    subcategoryId: undefined,
    relatedEntityType: undefined,
    relatedEntityReference: undefined,
  });

  const { data: categories, isLoading: categoriesLoading, error: categoriesError, refetch: refetchCategories } = useQuery({
    queryKey: ['ehc', 'admin', 'categories'],
    queryFn: () => ehcInternalTicketService.listCategories(),
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

  const { data: myDepartment } = useQuery({
    queryKey: ['ehc', 'internal', 'my-department'],
    queryFn: () => ehcInternalTicketService.getMyDepartment(),
  });

  const tree = useMemo(() => buildTree(categories || []), [categories]);

  const flatten = (nodes: CategoryNode[], rootId: string | null = null, depth = 0) => {
    const out: Array<{ label: string; categoryId: string; subcategoryId?: string; appliesToType?: EhcTicketType | null; depth: number }> = [];
    for (const n of nodes) {
      const effectiveRootId = rootId ?? n.id;
      out.push({
        label: n.name,
        categoryId: effectiveRootId,
        subcategoryId: rootId ? n.id : undefined,
        appliesToType: n.appliesToType,
        depth,
      });
      if (n.children?.length) {
        out.push(...flatten(n.children, effectiveRootId, depth + 1));
      }
    }
    return out;
  };

  const allCategoryOptions = useMemo(() => flatten(tree), [tree]);
  const visibleCategoryOptions = useMemo(
    () =>
      allCategoryOptions
        .filter((o) => !o.appliesToType || o.appliesToType === form.ticketType)
        .map((o) => ({ ...o, label: `${'— '.repeat(o.depth)}${o.label}` })),
    [allCategoryOptions, form.ticketType]
  );

  useEffect(() => {
    setForm((f) => ({
      ...f,
      ticketType: scopeConfig.defaultTicketType,
      source:
        scopeConfig.internalOnly
          ? 'Internal'
          : (() => {
              const currentSource = (f.source as EhcTicketSource | undefined) ?? 'Web';
              return EXTERNAL_TICKET_SOURCES.includes(currentSource) ? currentSource : 'Web';
            })(),
      categoryId: undefined,
      subcategoryId: undefined,
    }));
  }, [scopeConfig.defaultTicketType, scopeConfig.internalOnly, scopeConfig.scope]);

  useEffect(() => {
    if (!myDepartment?.id) return;
    setForm((f) => {
      if (f.assignedDepartmentId) return f;
      return { ...f, assignedDepartmentId: myDepartment.id };
    });
  }, [myDepartment?.id]);

  useEffect(() => {
    if (!activePriorityLevels.length) return;
    setForm((f) => {
      const exists = activePriorityLevels.some((p) => p.priority === (f.priority as any));
      if (exists) return f;
      return { ...f, priority: activePriorityLevels[0].priority as EhcTicketPriority };
    });
  }, [activePriorityLevels]);

  const canSubmit = Boolean(form.assignedDepartmentId) && Boolean(form.categoryId) && form.description.trim().length > 0;

  const kbQuery = useMemo(() => {
    const s = `${form.subject || ''} ${form.description || ''}`.trim().replace(/\s+/g, ' ');
    if (s.length < 3) return '';
    return s.slice(0, 120);
  }, [form.subject, form.description]);

  const { data: kbSuggestions, isLoading: kbLoading } = useQuery({
    queryKey: ['ehc', 'kb', 'suggest', kbQuery],
    queryFn: () => ehcInternalTicketService.kbSearchArticles(kbQuery, null, 5),
    enabled: kbQuery.length >= 3,
  });

  const { data: kbArticle } = useQuery({
    queryKey: ['ehc', 'kb', 'article', kbArticleId],
    queryFn: () => (kbArticleId ? ehcInternalTicketService.kbGetArticle(kbArticleId) : Promise.resolve(null)),
    enabled: Boolean(kbArticleId),
  });

  const trackKbView = useMutation({
    mutationFn: async (id: string) => ehcInternalTicketService.kbTrackView(id),
  });

  const create = useMutation({
    mutationFn: async () => {
      const ticket = await ehcInternalTicketService.createTicket(form);

      if (files.length) {
        for (const f of files) {
          const uploaded = await fileUploadService.uploadFile(f, 'ehc-ticket', ticket.id);
          if (uploaded.filePath) {
            await ehcInternalTicketService.addAttachment(ticket.id, {
              filePath: uploaded.filePath,
              fileName: uploaded.originalName || uploaded.filename,
              contentType: uploaded.mimeType,
              fileSize: uploaded.size,
              isInternal: attachmentInternalOnly,
            });
          }
        }
      }

      return ticket;
    },
    onSuccess: async (ticket) => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'internal', 'tickets'] });
      toast({ title: 'Created', description: `Ticket ${ticket.ticketNumber} created.`, variant: 'success' });
      router.push(buildScopedHelpdeskDetailPath(scopeConfig.scope, ticket.id));
    },
    onError: (err) => {
      toast({ title: 'Error', description: err instanceof Error ? err.message : 'Failed to create ticket', variant: 'destructive' });
    },
  });

  const doRelatedLookup = async () => {
    const entityType = (form.relatedEntityType || '').trim();
    const q = relatedLookupQ.trim();
    if (!entityType || !q) return;

    setRelatedLookupLoading(true);
    setRelatedLookupError(null);
    try {
      const items = await ehcInternalTicketService.searchRelatedEntities(entityType, q, 20);
      setRelatedLookupResults(items || []);
    } catch (e) {
      setRelatedLookupError(e instanceof Error ? e.message : 'Lookup failed');
      setRelatedLookupResults([]);
    } finally {
      setRelatedLookupLoading(false);
    }
  };

  return (
    <div className="max-w-7xl mx-auto space-y-4">
      <div className="flex items-start justify-between gap-4">
        <div>
          <Button variant="outline" onClick={() => router.push(buildScopedHelpdeskTicketsPath(scopeConfig.scope))}>
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back
          </Button>
          <h1 className="text-3xl font-bold flex items-center gap-2 mt-4">
            <Ticket className="h-7 w-7" />
            {scopeConfig.newTitle}
          </h1>
          <p className="text-slate-600 mt-1">{scopeConfig.newDescription}</p>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Ticket details</CardTitle>
          <CardDescription>Fields marked required drive routing, reporting, and SLAs.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {categoriesError ? (
            <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-800 flex items-center justify-between gap-3">
              <div>Failed to load categories. Please refresh and try again.</div>
              <Button variant="outline" size="sm" onClick={() => refetchCategories()}>
                Reload
              </Button>
            </div>
          ) : categoriesLoading ? (
            <div className="rounded-md border bg-slate-50 p-3 text-sm text-slate-700">Loading categories…</div>
          ) : null}

          <div className="grid grid-cols-1 xl:grid-cols-12 gap-4">
            <div className="xl:col-span-8 space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
                <div className="space-y-2">
                  <Label>Type</Label>
                  {typeLocked ? (
                    <div className="flex h-10 items-center rounded-md border border-input bg-slate-50 px-3 text-sm text-slate-700">
                      {scopeConfig.ticketTypeLabel}
                    </div>
                  ) : (
                    <select
                      className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                      value={form.ticketType}
                      onChange={(e) =>
                        setForm((f) => ({
                          ...f,
                          ticketType: e.target.value as any,
                          categoryId: undefined,
                          subcategoryId: undefined,
                        }))
                      }
                    >
                      {scopeConfig.allowedTicketTypes.map((type) => (
                        <option key={type} value={type}>
                          {type}
                        </option>
                      ))}
                    </select>
                  )}
                </div>

                <div className="space-y-2">
                  <Label>Priority</Label>
                  <select
                    className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                    value={form.priority}
                    onChange={(e) => setForm((f) => ({ ...f, priority: e.target.value as any }))}
                  >
                    {activePriorityLevels.length ? (
                      activePriorityLevels.map((p) => (
                        <option key={p.priority} value={p.priority}>
                          {p.displayName}
                        </option>
                      ))
                    ) : (
                      <>
                        <option value="Low">Low</option>
                        <option value="Medium">Medium</option>
                        <option value="High">High</option>
                        <option value="Critical">Critical</option>
                      </>
                    )}
                  </select>
                </div>

                <div className="space-y-2">
                  <Label>Channel</Label>
                  {scopeConfig.internalOnly ? (
                    <div className="flex h-10 items-center rounded-md border border-input bg-slate-50 px-3 text-sm text-slate-700">
                      Internal
                    </div>
                  ) : (
                    <select
                      className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                      value={form.source || 'Web'}
                      onChange={(e) => setForm((f) => ({ ...f, source: e.target.value as any }))}
                    >
                      {channelOptions.map((o) => (
                        <option key={o.value} value={o.value}>
                          {o.label}
                        </option>
                      ))}
                    </select>
                  )}
                </div>

                <div className="space-y-2">
                  <Label>
                    Department <span className="text-red-600">*</span>
                  </Label>
                  <select
                    className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                    value={form.assignedDepartmentId || ''}
                    onChange={(e) => setForm((f) => ({ ...f, assignedDepartmentId: e.target.value || undefined }))}
                  >
                    <option value="">Select department</option>
                    {(departments || []).map((d) => (
                      <option key={d.id} value={d.id}>
                        {d.name}
                      </option>
                    ))}
                  </select>
                  {myDepartment?.id ? <div className="text-xs text-slate-500">Auto-selected from your employee profile.</div> : null}
                </div>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Category</Label>
                  <select
                    className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                    value={form.subcategoryId || form.categoryId || ''}
                    onChange={(e) =>
                      setForm((f) => {
                        const value = e.target.value || '';
                        if (!value) return { ...f, categoryId: undefined, subcategoryId: undefined };

                        const selected = visibleCategoryOptions.find((o) => (o.subcategoryId || o.categoryId) === value);
                        if (!selected) return { ...f, categoryId: value, subcategoryId: undefined };

                        return { ...f, categoryId: selected.categoryId, subcategoryId: selected.subcategoryId };
                      })
                    }
                  >
                    <option value="">Select category</option>
                    {visibleCategoryOptions.map((o) => (
                      <option key={`${o.categoryId}:${o.subcategoryId ?? ''}`} value={o.subcategoryId || o.categoryId}>
                        {o.label}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="space-y-2">
                  <Label>Subject</Label>
                  <Input
                    value={form.subject || ''}
                    onChange={(e) => setForm((f) => ({ ...f, subject: e.target.value }))}
                    placeholder="Short summary"
                  />
                </div>
              </div>

              <div className="space-y-2">
                <Label>Description</Label>
                <Textarea
                  value={form.description}
                  onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
                  placeholder="Describe the issue / request"
                  rows={7}
                />
              </div>
            </div>

            <div className="xl:col-span-4 space-y-4">
              <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-1 gap-4">
                <div className="space-y-2">
                  <Label>Related Entity Type</Label>
                  <select
                    className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                    value={form.relatedEntityType || ''}
                    onChange={(e) => setForm((f) => ({ ...f, relatedEntityType: e.target.value || undefined }))}
                  >
                    {relatedEntityTypeOptions.map((o) => (
                      <option key={o.value} value={o.value}>
                        {o.label}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="space-y-2">
                  <Label>Related Reference</Label>
                  <div className="flex gap-2">
                    <Input
                      value={form.relatedEntityReference || ''}
                      onChange={(e) => setForm((f) => ({ ...f, relatedEntityReference: e.target.value || undefined }))}
                      placeholder={form.relatedEntityType ? 'Enter reference or use Lookup' : '—'}
                      disabled={!form.relatedEntityType}
                    />
                    <Button
                      type="button"
                      variant="outline"
                      onClick={() => {
                        setRelatedLookupQ(form.relatedEntityReference || '');
                        setRelatedLookupResults([]);
                        setRelatedLookupError(null);
                        setRelatedLookupOpen(true);
                      }}
                      disabled={!form.relatedEntityType || !relatedLookupSupportedTypes.has(form.relatedEntityType)}
                    >
                      Lookup
                    </Button>
                  </div>
                  <div className="text-xs text-slate-500">
                    Lookup supports Asset/Vehicle, Work Order, Purchase Order/Requisition, Tender, and RFQ.
                  </div>
                </div>
              </div>

              <div className="space-y-2">
                <Label className="flex items-center gap-2">
                  <Paperclip className="h-4 w-4" />
                  Attachments
                </Label>
                <Input type="file" multiple onChange={(e) => setFiles(Array.from(e.target.files || []))} />
                <label className="flex items-center gap-2 text-sm text-slate-700">
                  <input type="checkbox" checked={attachmentInternalOnly} onChange={(e) => setAttachmentInternalOnly(e.target.checked)} />
                  Internal only (hidden from external users)
                </label>
                {files.length ? <div className="text-xs text-slate-600">{files.length} file(s) selected</div> : null}
              </div>

              <div className="flex items-center gap-2 pt-1">
                <Button onClick={() => create.mutate()} disabled={!canSubmit || create.isPending} className="flex-1">
                  {create.isPending ? 'Creating...' : scopeConfig.createButtonLabel}
                </Button>
                <Button variant="outline" onClick={() => router.push(buildScopedHelpdeskTicketsPath(scopeConfig.scope))}>
                  Cancel
                </Button>
              </div>
            </div>

            <div className="xl:col-span-4 space-y-4">
              <div className="rounded-lg border bg-white p-4">
                <div className="flex items-center gap-2 font-medium text-slate-900">
                  <BookOpen className="h-4 w-4" />
                  Suggested articles
                </div>
                <div className="text-xs text-slate-500 mt-1">Based on your subject/description.</div>

                <div className="mt-3 space-y-2">
                  {kbQuery.length < 3 ? (
                    <div className="text-sm text-slate-500">Start typing a subject/description to see suggestions.</div>
                  ) : kbLoading ? (
                    <div className="text-sm text-slate-500">Loading…</div>
                  ) : (kbSuggestions ?? []).length ? (
                    (kbSuggestions as EhcKbArticleListItem[]).map((a) => (
                      <button
                        key={a.id}
                        type="button"
                        className="w-full text-left rounded-md border p-3 hover:bg-slate-50"
                        onClick={() => {
                          setKbArticleId(a.id);
                          setKbArticleOpen(true);
                          trackKbView.mutate(a.id);
                        }}
                      >
                        <div className="font-medium text-slate-900 line-clamp-2">{a.title}</div>
                        <div className="text-xs text-slate-500 mt-1">
                          {a.code}
                          {a.categoryName ? ` • ${a.categoryName}` : ''}
                          {a.tagsCsv ? ` • ${a.tagsCsv}` : ''}
                        </div>
                        {a.summary ? <div className="text-sm text-slate-700 mt-1 line-clamp-2">{a.summary}</div> : null}
                      </button>
                    ))
                  ) : (
                    <div className="text-sm text-slate-500">No suggestions found.</div>
                  )}
                </div>

                <div className="text-xs text-slate-500 mt-3">
                  Manage articles in Admin → Helpdesk → Knowledge Base.
                </div>
              </div>

              <div className="rounded-lg border bg-slate-50 p-4 text-sm text-slate-700">
                <div className="font-medium text-slate-900">Attachments</div>
                <div className="text-xs text-slate-500 mt-1">
                  File type and size policies apply per tenant/category (configured by admins).
                </div>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      <Dialog open={kbArticleOpen} onOpenChange={setKbArticleOpen}>
        <DialogContent className="sm:max-w-[900px] max-h-[85vh] overflow-auto">
          <DialogHeader>
            <DialogTitle>{(kbArticle as EhcKbArticleDetail | null)?.title ?? 'Article'}</DialogTitle>
            <DialogDescription>
              {(kbArticle as any)?.code ? `${(kbArticle as any).code}${(kbArticle as any)?.categoryName ? ` • ${(kbArticle as any).categoryName}` : ''}` : ''}
            </DialogDescription>
          </DialogHeader>

          <div className="prose prose-slate max-w-none whitespace-pre-wrap">{(kbArticle as EhcKbArticleDetail | null)?.body ?? ''}</div>
        </DialogContent>
      </Dialog>

      <Dialog open={relatedLookupOpen} onOpenChange={setRelatedLookupOpen}>
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>Lookup related entity</DialogTitle>
          </DialogHeader>

          <div className="grid gap-3">
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
              <div className="sm:col-span-1 space-y-1">
                <Label className="text-xs text-slate-600">Type</Label>
                <div className="h-10 px-3 rounded-md border bg-slate-50 flex items-center text-sm text-slate-900">
                  {form.relatedEntityType || '—'}
                </div>
              </div>
              <div className="sm:col-span-2 space-y-1">
                <Label className="text-xs text-slate-600">Search</Label>
                <div className="flex gap-2">
                  <Input value={relatedLookupQ} onChange={(e) => setRelatedLookupQ(e.target.value)} placeholder="Type name/number..." />
                  <Button
                    type="button"
                    onClick={doRelatedLookup}
                    disabled={
                      !relatedLookupQ.trim() ||
                      !form.relatedEntityType ||
                      !relatedLookupSupportedTypes.has(form.relatedEntityType) ||
                      relatedLookupLoading
                    }
                  >
                    {relatedLookupLoading ? 'Searching...' : 'Search'}
                  </Button>
                </div>
              </div>
            </div>

            {relatedLookupError ? <div className="text-sm text-red-600">{relatedLookupError}</div> : null}

            <div className="overflow-x-auto rounded-md border">
              <table className="w-full text-sm">
                <thead className="bg-slate-50 text-left text-slate-600">
                  <tr>
                    <th className="py-2 px-3">Reference</th>
                    <th className="py-2 px-3">Label</th>
                    <th className="py-2 px-3 text-right">Action</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {relatedLookupResults.length ? (
                    relatedLookupResults.map((r) => (
                      <tr key={r.id} className="hover:bg-slate-50">
                        <td className="py-2 px-3 font-mono">{r.reference}</td>
                        <td className="py-2 px-3">{r.label}</td>
                        <td className="py-2 px-3 text-right">
                          <Button
                            type="button"
                            variant="outline"
                            size="sm"
                            onClick={() => {
                              setForm((f) => ({
                                ...f,
                                relatedEntityType: r.entityType,
                                relatedEntityReference: r.reference,
                              }));
                              setRelatedLookupOpen(false);
                            }}
                          >
                            Use
                          </Button>
                        </td>
                      </tr>
                    ))
                  ) : (
                    <tr>
                      <td className="py-6 px-3 text-slate-600" colSpan={3}>
                        {relatedLookupLoading ? 'Searching…' : 'No results yet. Enter a search term and click Search.'}
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
