'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Pencil, Plus, Save, Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { ehcAdminService, type CreateEhcTicketCategoryAdmin, type EhcTicketCategoryAdmin } from '@/services/ehcAdminService';
import type { EhcTicketType } from '@/services/ehcTicketService';

export default function HelpdeskCategoriesAdminPage() {
  const qc = useQueryClient();
  const { data, isLoading, error } = useQuery({
    queryKey: ['ehc', 'admin', 'categories'],
    queryFn: () => ehcAdminService.listCategories(),
  });

  const [editing, setEditing] = useState<EhcTicketCategoryAdmin | null>(null);
  const [form, setForm] = useState<CreateEhcTicketCategoryAdmin>({
    code: '',
    name: '',
    description: '',
    appliesToType: null,
    parentCategoryId: null,
  });

  const topLevel = useMemo(() => (data || []).filter((c) => !c.parentCategoryId), [data]);

  const save = useMutation({
    mutationFn: async () => {
      const payload: CreateEhcTicketCategoryAdmin = {
        code: form.code.trim(),
        name: form.name.trim(),
        description: form.description?.trim() || null,
        appliesToType: (form.appliesToType as any) || null,
        parentCategoryId: form.parentCategoryId || null,
      };
      if (editing) return ehcAdminService.updateCategory(editing.id, payload);
      return ehcAdminService.createCategory(payload);
    },
    onSuccess: async () => {
      setEditing(null);
      setForm({ code: '', name: '', description: '', appliesToType: null, parentCategoryId: null });
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'categories'] });
    },
  });

  const del = useMutation({
    mutationFn: (id: string) => ehcAdminService.deleteCategory(id),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'categories'] });
    },
  });

  const startEdit = (c: EhcTicketCategoryAdmin) => {
    setEditing(c);
    setForm({
      code: c.code,
      name: c.name,
      description: c.description || '',
      appliesToType: (c.appliesToType as any) || null,
      parentCategoryId: c.parentCategoryId || null,
    });
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">Ticket Categories</h1>
        <p className="text-slate-600 mt-1">Configure categories and subcategories used for routing and reporting.</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{editing ? 'Edit category' : 'Create category'}</CardTitle>
          <CardDescription>Codes must be unique per tenant.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Code</Label>
              <Input value={form.code} onChange={(e) => setForm((f) => ({ ...f, code: e.target.value }))} placeholder="e.g. TECH" />
            </div>
            <div className="space-y-2">
              <Label>Name</Label>
              <Input value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} placeholder="e.g. Technical Support" />
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Applies to type</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.appliesToType || ''}
                onChange={(e) => setForm((f) => ({ ...f, appliesToType: (e.target.value || null) as EhcTicketType | null }))}
              >
                <option value="">All</option>
                <option value="Enquiry">Enquiry</option>
                <option value="Complaint">Complaint</option>
                <option value="Helpdesk">Helpdesk</option>
              </select>
            </div>
            <div className="space-y-2">
              <Label>Parent category</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.parentCategoryId || ''}
                onChange={(e) => setForm((f) => ({ ...f, parentCategoryId: e.target.value || null }))}
              >
                <option value="">(top-level)</option>
                {topLevel.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="space-y-2">
            <Label>Description</Label>
            <Textarea value={form.description || ''} onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))} rows={3} />
          </div>

          <div className="flex items-center gap-2">
            <Button onClick={() => save.mutate()} disabled={save.isPending || !form.code.trim() || !form.name.trim()}>
              {editing ? <Save className="h-4 w-4 mr-2" /> : <Plus className="h-4 w-4 mr-2" />}
              {save.isPending ? 'Saving...' : editing ? 'Save' : 'Create'}
            </Button>
            {editing ? (
              <Button
                variant="outline"
                onClick={() => {
                  setEditing(null);
                  setForm({ code: '', name: '', description: '', appliesToType: null, parentCategoryId: null });
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
            <div className="text-red-600">Failed to load categories.</div>
          ) : !data || data.length === 0 ? (
            <div className="text-slate-600">No categories yet.</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="text-left text-slate-500">
                  <tr>
                    <th className="py-2 pr-4">Code</th>
                    <th className="py-2 pr-4">Name</th>
                    <th className="py-2 pr-4">Type</th>
                    <th className="py-2 pr-4">Parent</th>
                    <th className="py-2 pr-4 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {data.map((c) => (
                    <tr key={c.id} className="hover:bg-slate-50">
                      <td className="py-2 pr-4 font-medium text-slate-900">
                        <Button
                          variant="link"
                          className="h-auto p-0 text-blue-700"
                          onClick={() => startEdit(c)}
                          title="Edit category"
                        >
                          {c.code}
                        </Button>
                      </td>
                      <td className="py-2 pr-4">{c.name}</td>
                      <td className="py-2 pr-4">{c.appliesToType || 'All'}</td>
                      <td className="py-2 pr-4">
                        {c.parentCategoryId ? data.find((p) => p.id === c.parentCategoryId)?.name || '—' : '—'}
                      </td>
                      <td className="py-2 pr-4 text-right">
                        <Button variant="ghost" size="sm" onClick={() => startEdit(c)} title="Edit">
                          <Pencil className="h-4 w-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={(e) => {
                            e.stopPropagation();
                            del.mutate(c.id);
                          }}
                          disabled={del.isPending}
                          title="Delete"
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
