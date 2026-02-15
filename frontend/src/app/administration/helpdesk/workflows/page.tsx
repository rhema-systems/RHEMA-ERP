'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Save, Settings, Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { ehcAdminService, type CreateEhcWorkflowRoutingRuleAdmin, type EhcWorkflowRoutingRuleAdmin } from '@/services/ehcAdminService';
import { ehcInternalTicketService } from '@/services/ehcInternalTicketService';
import type { EhcTicketPriority, EhcTicketType } from '@/services/ehcTicketService';
import { WorkflowApiService } from '@/services/workflow-api.service';

const workflowApi = new WorkflowApiService();

export default function HelpdeskWorkflowRoutingAdminPage() {
  const qc = useQueryClient();

  const { data: categories } = useQuery({
    queryKey: ['ehc', 'admin', 'categories'],
    queryFn: () => ehcAdminService.listCategories(),
  });

  const { data: departments } = useQuery({
    queryKey: ['ehc', 'internal', 'departments'],
    queryFn: () => ehcInternalTicketService.listDepartments(),
  });

  const { data: rules, isLoading, error } = useQuery({
    queryKey: ['ehc', 'admin', 'workflowRoutingRules'],
    queryFn: () => ehcAdminService.listWorkflowRoutingRules(),
  });

  const { data: workflowDefs } = useQuery({
    queryKey: ['workflow', 'definitions', 'ehc'],
    queryFn: async () => {
      const res = await workflowApi.getWorkflowDefinitions({
        page: 1,
        pageSize: 200,
        sortBy: 'name',
        sortDescending: false,
        entityType: 'EHC_TICKET',
      } as any);
      return res.data ?? [];
    },
  });

  const workflowNames = useMemo(() => {
    const items = workflowDefs || [];
    const names = items.map((d: any) => String(d.name || d.Name || '')).filter(Boolean);
    return Array.from(new Set(names)).sort((a, b) => a.localeCompare(b));
  }, [workflowDefs]);

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

  const [editing, setEditing] = useState<EhcWorkflowRoutingRuleAdmin | null>(null);
  const [form, setForm] = useState<CreateEhcWorkflowRoutingRuleAdmin>({
    name: '',
    isActive: true,
    priority: 0,
    workflowName: 'EHC Ticket',
    ticketType: null,
    ticketPriority: null,
    categoryId: null,
    subcategoryId: null,
    assignedDepartmentId: null,
  });

  const selectedTypeForCategories = form.ticketType || null;
  const categoryOptions = useMemo(() => {
    const items = categories || [];
    return items
      .filter((c) => !selectedTypeForCategories || !c.appliesToType || c.appliesToType === selectedTypeForCategories)
      .map((c) => ({ id: c.id, label: categoryLabelById.get(c.id) || c.name, parentCategoryId: c.parentCategoryId || null }))
      .sort((a, b) => a.label.localeCompare(b.label));
  }, [categories, selectedTypeForCategories, categoryLabelById]);

  const save = useMutation({
    mutationFn: async () => {
      const payload: CreateEhcWorkflowRoutingRuleAdmin = {
        name: form.name.trim(),
        isActive: !!form.isActive,
        priority: Number(form.priority) || 0,
        workflowName: form.workflowName.trim(),
        ticketType: (form.ticketType as any) || null,
        ticketPriority: (form.ticketPriority as any) || null,
        categoryId: form.categoryId || null,
        subcategoryId: form.subcategoryId || null,
        assignedDepartmentId: form.assignedDepartmentId || null,
      };

      if (editing) {
        await ehcAdminService.updateWorkflowRoutingRule(editing.id, payload);
        return;
      }

      return ehcAdminService.createWorkflowRoutingRule(payload);
    },
    onSuccess: async () => {
      setEditing(null);
      setForm({
        name: '',
        isActive: true,
        priority: 0,
        workflowName: 'EHC Ticket',
        ticketType: null,
        ticketPriority: null,
        categoryId: null,
        subcategoryId: null,
        assignedDepartmentId: null,
      });
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'workflowRoutingRules'] });
    },
  });

  const del = useMutation({
    mutationFn: (id: string) => ehcAdminService.deleteWorkflowRoutingRule(id),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'workflowRoutingRules'] });
    },
  });

  const startEdit = (r: EhcWorkflowRoutingRuleAdmin) => {
    setEditing(r);
    setForm({
      name: r.name,
      isActive: r.isActive,
      priority: r.priority,
      workflowName: r.workflowName,
      ticketType: (r.ticketType as any) || null,
      ticketPriority: (r.ticketPriority as any) || null,
      categoryId: r.categoryId || null,
      subcategoryId: r.subcategoryId || null,
      assignedDepartmentId: r.assignedDepartmentId || null,
    });
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">Workflow Routing</h1>
        <p className="text-slate-600 mt-1">Select which workflow definition to start per ticket type, category, priority, and department.</p>
        <div className="mt-3">
          <Button asChild variant="outline" size="sm">
            <Link href="/administration/workflow?entityType=EHC_TICKET&q=EHC">
              <Settings className="h-4 w-4 mr-2" />
              Open Workflow Designer (EHC)
            </Link>
          </Button>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{editing ? 'Edit routing rule' : 'Create routing rule'}</CardTitle>
          <CardDescription>Highest priority + most specific match wins.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Name</Label>
              <Input value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} placeholder="e.g. Helpdesk - IT - Critical" />
            </div>
            <div className="space-y-2">
              <Label>Active</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.isActive ? 'true' : 'false'}
                onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.value === 'true' }))}
              >
                <option value="true">Active</option>
                <option value="false">Inactive</option>
              </select>
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div className="space-y-2">
              <Label>Priority (rule)</Label>
              <Input type="number" value={String(form.priority)} onChange={(e) => setForm((f) => ({ ...f, priority: Number(e.target.value) }))} />
            </div>
            <div className="space-y-2 sm:col-span-2">
              <Label>Workflow</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.workflowName}
                onChange={(e) => setForm((f) => ({ ...f, workflowName: e.target.value }))}
              >
                {workflowNames.length ? (
                  workflowNames.map((n) => (
                    <option key={n} value={n}>
                      {n}
                    </option>
                  ))
                ) : (
                  <option value={form.workflowName}>{form.workflowName}</option>
                )}
              </select>
              <div className="text-xs text-slate-500">Only workflows for entity type `EHC_TICKET` are listed.</div>
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-4 gap-4">
            <div className="space-y-2">
              <Label>Ticket type</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.ticketType || ''}
                onChange={(e) => setForm((f) => ({ ...f, ticketType: (e.target.value || null) as EhcTicketType | null, categoryId: null, subcategoryId: null }))}
              >
                <option value="">Any</option>
                <option value="Enquiry">Enquiry</option>
                <option value="Complaint">Complaint</option>
                <option value="Helpdesk">Helpdesk</option>
              </select>
            </div>
            <div className="space-y-2">
              <Label>Ticket priority</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.ticketPriority || ''}
                onChange={(e) => setForm((f) => ({ ...f, ticketPriority: (e.target.value || null) as EhcTicketPriority | null }))}
              >
                <option value="">Any</option>
                <option value="Low">Low</option>
                <option value="Medium">Medium</option>
                <option value="High">High</option>
                <option value="Critical">Critical</option>
              </select>
            </div>
            <div className="space-y-2">
              <Label>Category</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.categoryId || ''}
                onChange={(e) => setForm((f) => ({ ...f, categoryId: e.target.value || null, subcategoryId: null }))}
              >
                <option value="">Any</option>
                {categoryOptions.filter((c) => !c.parentCategoryId).map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.label}
                  </option>
                ))}
              </select>
            </div>
            <div className="space-y-2">
              <Label>Subcategory</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.subcategoryId || ''}
                onChange={(e) => setForm((f) => ({ ...f, subcategoryId: e.target.value || null }))}
                disabled={!form.categoryId}
              >
                <option value="">Any</option>
                {categoryOptions
                  .filter((c) => c.parentCategoryId && c.parentCategoryId === form.categoryId)
                  .map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.label}
                    </option>
                  ))}
              </select>
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Assigned department</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.assignedDepartmentId || ''}
                onChange={(e) => setForm((f) => ({ ...f, assignedDepartmentId: e.target.value || null }))}
              >
                <option value="">Any</option>
                {(departments || []).map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <Button onClick={() => save.mutate()} disabled={save.isPending || !form.name.trim() || !form.workflowName.trim()}>
            <Save className="h-4 w-4 mr-2" />
            {editing ? 'Update rule' : 'Save rule'}
          </Button>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Routing rules</CardTitle>
          <CardDescription>Rules are evaluated for each new ticket.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-2">
          {isLoading ? <div className="text-sm text-slate-600">Loading…</div> : null}
          {error ? <div className="text-sm text-red-600">Failed to load rules.</div> : null}
          {(rules || []).length ? (
            <div className="divide-y">
              {(rules || []).map((r) => (
                <div key={r.id} className="py-3 flex items-start justify-between gap-4">
                  <div className="min-w-0">
                    <div className="font-medium text-slate-900 truncate">{r.name}</div>
                    <div className="text-xs text-slate-600 mt-1">
                      {r.isActive ? 'Active' : 'Inactive'} • Priority {r.priority} • Workflow: {r.workflowName}
                      {r.ticketType ? ` • Type: ${r.ticketType}` : ''}
                      {r.ticketPriority ? ` • TicketPriority: ${r.ticketPriority}` : ''}
                      {r.categoryName ? ` • Category: ${r.categoryName}` : ''}
                      {r.subcategoryName ? ` • Subcategory: ${r.subcategoryName}` : ''}
                      {r.assignedDepartmentName ? ` • Dept: ${r.assignedDepartmentName}` : ''}
                    </div>
                  </div>
                  <div className="flex items-center gap-2 shrink-0">
                    <Button variant="outline" size="sm" onClick={() => startEdit(r)}>
                      Edit
                    </Button>
                    <Button variant="destructive" size="sm" onClick={() => del.mutate(r.id)} disabled={del.isPending}>
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          ) : !isLoading ? (
            <div className="text-sm text-slate-600">No routing rules yet.</div>
          ) : null}
        </CardContent>
      </Card>
    </div>
  );
}

