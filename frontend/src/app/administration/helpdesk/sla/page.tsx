'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Save, Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { ehcAdminService, type CreateEhcSlaTemplateAdmin, type EhcSlaTemplateAdmin } from '@/services/ehcAdminService';
import type { EhcTicketPriority, EhcTicketType } from '@/services/ehcTicketService';

export default function HelpdeskSlaAdminPage() {
  const qc = useQueryClient();

  const { data: categories } = useQuery({
    queryKey: ['ehc', 'admin', 'categories'],
    queryFn: () => ehcAdminService.listCategories(),
  });

  const { data: templates, isLoading, error } = useQuery({
    queryKey: ['ehc', 'admin', 'slaTemplates'],
    queryFn: () => ehcAdminService.listSlaTemplates(),
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

  const [editing, setEditing] = useState<EhcSlaTemplateAdmin | null>(null);
  const [form, setForm] = useState<CreateEhcSlaTemplateAdmin>({
    name: '',
    isActive: true,
    ticketType: null,
    priority: null,
    categoryId: null,
    firstResponseMinutes: 60,
    resolutionMinutes: 1440,
  });

  const save = useMutation({
    mutationFn: async () => {
      const payload: CreateEhcSlaTemplateAdmin = {
        name: form.name.trim(),
        isActive: !!form.isActive,
        ticketType: (form.ticketType as any) || null,
        priority: (form.priority as any) || null,
        categoryId: form.categoryId || null,
        firstResponseMinutes: Number(form.firstResponseMinutes) || 60,
        resolutionMinutes: Number(form.resolutionMinutes) || 1440,
      };

      if (editing) {
        await ehcAdminService.updateSlaTemplate(editing.id, payload);
        return;
      }
      return ehcAdminService.createSlaTemplate(payload);
    },
    onSuccess: async () => {
      setEditing(null);
      setForm({
        name: '',
        isActive: true,
        ticketType: null,
        priority: null,
        categoryId: null,
        firstResponseMinutes: 60,
        resolutionMinutes: 1440,
      });
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'slaTemplates'] });
    },
  });

  const del = useMutation({
    mutationFn: (id: string) => ehcAdminService.deleteSlaTemplate(id),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'slaTemplates'] });
    },
  });

  const startEdit = (t: EhcSlaTemplateAdmin) => {
    setEditing(t);
    setForm({
      name: t.name,
      isActive: t.isActive,
      ticketType: (t.ticketType as any) || null,
      priority: (t.priority as any) || null,
      categoryId: t.categoryId || null,
      firstResponseMinutes: t.firstResponseMinutes,
      resolutionMinutes: t.resolutionMinutes,
    });
  };

  const selectedTypeForCategories = form.ticketType || null;
  const categoryOptions = useMemo(() => {
    const items = categories || [];
    return items
      .filter((c) => !selectedTypeForCategories || !c.appliesToType || c.appliesToType === selectedTypeForCategories)
      .map((c) => ({ id: c.id, label: categoryLabelById.get(c.id) || c.name }))
      .sort((a, b) => a.label.localeCompare(b.label));
  }, [categories, selectedTypeForCategories, categoryLabelById]);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">SLA Templates</h1>
        <p className="text-slate-600 mt-1">Configure first response and resolution targets per ticket type/priority/category.</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{editing ? 'Edit SLA template' : 'Create SLA template'}</CardTitle>
          <CardDescription>Most specific match wins (category + type + priority).</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Name</Label>
              <Input value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} placeholder="e.g. Helpdesk - High" />
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
              <Label>Ticket type</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.ticketType || ''}
                onChange={(e) => setForm((f) => ({ ...f, ticketType: (e.target.value || null) as EhcTicketType | null }))}
              >
                <option value="">Any</option>
                <option value="Enquiry">Enquiry</option>
                <option value="Complaint">Complaint</option>
                <option value="Helpdesk">Helpdesk</option>
              </select>
            </div>
            <div className="space-y-2">
              <Label>Priority</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.priority || ''}
                onChange={(e) => setForm((f) => ({ ...f, priority: (e.target.value || null) as EhcTicketPriority | null }))}
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
                onChange={(e) => setForm((f) => ({ ...f, categoryId: e.target.value || null }))}
              >
                <option value="">Any</option>
                {categoryOptions.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.label}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>First response (minutes)</Label>
              <Input
                type="number"
                value={String(form.firstResponseMinutes)}
                onChange={(e) => setForm((f) => ({ ...f, firstResponseMinutes: Number(e.target.value) }))}
                min={1}
              />
            </div>
            <div className="space-y-2">
              <Label>Resolution (minutes)</Label>
              <Input
                type="number"
                value={String(form.resolutionMinutes)}
                onChange={(e) => setForm((f) => ({ ...f, resolutionMinutes: Number(e.target.value) }))}
                min={1}
              />
            </div>
          </div>

          <div className="flex items-center gap-2">
            <Button onClick={() => save.mutate()} disabled={save.isPending || !form.name.trim()}>
              {editing ? <Save className="h-4 w-4 mr-2" /> : <Plus className="h-4 w-4 mr-2" />}
              {save.isPending ? 'Saving...' : editing ? 'Save' : 'Create'}
            </Button>
            {editing ? (
              <Button
                variant="outline"
                onClick={() => {
                  setEditing(null);
                  setForm({
                    name: '',
                    isActive: true,
                    ticketType: null,
                    priority: null,
                    categoryId: null,
                    firstResponseMinutes: 60,
                    resolutionMinutes: 1440,
                  });
                }}
              >
                Cancel
              </Button>
            ) : null}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Existing</CardTitle>
          <CardDescription>Click a row to edit.</CardDescription>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="text-slate-600">Loading...</div>
          ) : error ? (
            <div className="text-red-600">Failed to load SLA templates.</div>
          ) : !templates || templates.length === 0 ? (
            <div className="text-slate-600">No SLA templates yet.</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="text-left text-slate-500">
                  <tr>
                    <th className="py-2 pr-4">Name</th>
                    <th className="py-2 pr-4">Active</th>
                    <th className="py-2 pr-4">Type</th>
                    <th className="py-2 pr-4">Priority</th>
                    <th className="py-2 pr-4">Category</th>
                    <th className="py-2 pr-4">First resp.</th>
                    <th className="py-2 pr-4">Resolution</th>
                    <th className="py-2 pr-4"></th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {templates.map((t) => (
                    <tr key={t.id} className="hover:bg-slate-50 cursor-pointer" onClick={() => startEdit(t)}>
                      <td className="py-2 pr-4 font-medium text-slate-900">{t.name}</td>
                      <td className="py-2 pr-4">{t.isActive ? 'Yes' : 'No'}</td>
                      <td className="py-2 pr-4">{t.ticketType || 'Any'}</td>
                      <td className="py-2 pr-4">{t.priority || 'Any'}</td>
                      <td className="py-2 pr-4">{t.categoryName || 'Any'}</td>
                      <td className="py-2 pr-4">{t.firstResponseMinutes}m</td>
                      <td className="py-2 pr-4">{t.resolutionMinutes}m</td>
                      <td className="py-2 pr-4 text-right">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={(e) => {
                            e.stopPropagation();
                            del.mutate(t.id);
                          }}
                          disabled={del.isPending}
                        >
                          <Trash2 className="h-4 w-4 text-red-600" />
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
