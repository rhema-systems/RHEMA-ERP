'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Pencil, Plus, Save, Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable';
import { useToast } from '@/hooks/use-toast';
import { ehcAdminService, type CreateEhcCannedResponseAdmin, type EhcCannedResponseAdmin } from '@/services/ehcAdminService';
import type { EhcTicketType } from '@/services/ehcTicketService';

const ticketTypeOptions: Array<{ value: string; label: string }> = [
  { value: '__any', label: 'Any' },
  { value: 'Enquiry', label: 'Enquiry' },
  { value: 'Complaint', label: 'Complaint' },
  { value: 'Helpdesk', label: 'Helpdesk' },
];

export default function HelpdeskCannedResponsesAdminPage() {
  const qc = useQueryClient();
  const { toast } = useToast();

  const { data, isLoading, error } = useQuery({
    queryKey: ['ehc', 'admin', 'canned-responses'],
    queryFn: () => ehcAdminService.listCannedResponses(),
  });

  const { data: categories } = useQuery({
    queryKey: ['ehc', 'admin', 'categories'],
    queryFn: () => ehcAdminService.listCategories(),
  });

  const categoryNameById = useMemo(() => {
    const map = new Map<string, string>();
    (categories ?? []).forEach((c) => map.set(c.id, `${c.code} • ${c.name}`));
    return map;
  }, [categories]);

  const items = useMemo(() => (data ?? []).slice().sort((a, b) => a.code.localeCompare(b.code)), [data]);

  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<EhcCannedResponseAdmin | null>(null);
  const [form, setForm] = useState<CreateEhcCannedResponseAdmin>({
    code: '',
    title: '',
    body: '',
    isActive: true,
    appliesToType: null,
    categoryId: null,
  });

  const startCreate = () => {
    setEditing(null);
    setForm({ code: '', title: '', body: '', isActive: true, appliesToType: null, categoryId: null });
    setOpen(true);
  };

  const startEdit = (row: EhcCannedResponseAdmin) => {
    setEditing(row);
    setForm({
      code: row.code || '',
      title: row.title || '',
      body: row.body || '',
      isActive: !!row.isActive,
      appliesToType: (row.appliesToType ?? null) as any,
      categoryId: row.categoryId ?? null,
    });
    setOpen(true);
  };

  const save = useMutation({
    mutationFn: async () => {
      const payload: CreateEhcCannedResponseAdmin = {
        code: form.code.trim(),
        title: form.title.trim(),
        body: form.body.trim(),
        isActive: !!form.isActive,
        appliesToType: form.appliesToType ?? null,
        categoryId: form.categoryId ?? null,
      };
      if (!payload.code || !payload.title || !payload.body) throw new Error('Code, Title and Body are required');

      if (editing) return ehcAdminService.updateCannedResponse(editing.id, payload);
      return ehcAdminService.createCannedResponse(payload);
    },
    onSuccess: async () => {
      setOpen(false);
      setEditing(null);
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'canned-responses'] });
      toast({ title: 'Saved', description: 'Canned response saved successfully.', variant: 'success' });
    },
    onError: (err: any) => {
      toast({ title: 'Error', description: err?.message || 'Failed to save canned response.', variant: 'destructive' });
    },
  });

  const del = useMutation({
    mutationFn: async (id: string) => {
      await ehcAdminService.deleteCannedResponse(id);
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'canned-responses'] });
      toast({ title: 'Deleted', description: 'Canned response deleted.', variant: 'success' });
    },
    onError: (err: any) => {
      toast({ title: 'Error', description: err?.message || 'Failed to delete canned response.', variant: 'destructive' });
    },
  });

  const columns = useMemo<Array<DataTableColumn<EhcCannedResponseAdmin>>>(() => {
    return [
      {
        id: 'code',
        header: 'Code',
        accessorKey: 'code',
        cell: ({ row }) => (
          <button className="text-blue-600 hover:underline" onClick={() => startEdit(row.original)} title="Edit">
            {row.original.code}
          </button>
        ),
      },
      { id: 'title', header: 'Title', accessorKey: 'title' },
      {
        id: 'appliesToType',
        header: 'Type',
        accessorKey: 'appliesToType',
        cell: ({ row }) => (row.original.appliesToType ? String(row.original.appliesToType) : 'Any'),
      },
      {
        id: 'categoryId',
        header: 'Category',
        accessorKey: 'categoryId',
        cell: ({ row }) => (row.original.categoryId ? categoryNameById.get(row.original.categoryId) || row.original.categoryId : 'Any'),
      },
      {
        id: 'isActive',
        header: 'Active',
        accessorKey: 'isActive',
        cell: ({ row }) => (row.original.isActive ? 'Yes' : 'No'),
      },
      {
        id: 'actions',
        header: '',
        accessorKey: 'id',
        enableHiding: false,
        cell: ({ row }) => (
          <div className="flex justify-end gap-2">
            <Button variant="ghost" size="icon" onClick={() => startEdit(row.original)} title="Edit">
              <Pencil className="h-4 w-4" />
            </Button>
            <Button variant="ghost" size="icon" onClick={() => del.mutate(row.original.id)} title="Delete" disabled={del.isPending}>
              <Trash2 className="h-4 w-4 text-red-600" />
            </Button>
          </div>
        ),
      },
    ];
  }, [categoryNameById, del.isPending]);

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold">Canned Responses</h1>
          <p className="text-slate-600 mt-1">Reusable reply templates for agents (supports type/category scoping).</p>
        </div>
        <Button onClick={startCreate}>
          <Plus className="h-4 w-4 mr-2" />
          New Canned Response
        </Button>
      </div>

      <DataTable
        compact
        title="Templates"
        data={items}
        columns={columns}
        loading={isLoading}
        error={error ? 'Failed to load canned responses.' : null}
        enableSearch
        enablePagination
        exportFileName={`ehc-canned-responses-${new Date().toISOString().slice(0, 10)}`}
        exportFormats={['csv', 'excel']}
      />

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-[800px]">
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit Canned Response' : 'Create Canned Response'}</DialogTitle>
            <DialogDescription>Use for consistent replies. Scope to a ticket type/category if needed.</DialogDescription>
          </DialogHeader>

          <div className="rounded-md border bg-slate-50 p-3 text-xs text-slate-700">
            <div className="font-medium text-slate-900">Template variables</div>
            <div className="mt-1">
              Use variables in the body and they’ll be expanded when an agent replies:{' '}
              <span className="font-mono">{`{{TicketNumber}}`}</span>, <span className="font-mono">{`{{RequesterName}}`}</span>,{' '}
              <span className="font-mono">{`{{Status}}`}</span>, <span className="font-mono">{`{{PortalUrl}}`}</span>.
            </div>
          </div>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Details</CardTitle>
              <CardDescription>Code should be short and stable.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Code</Label>
                  <Input value={form.code} onChange={(e) => setForm((f) => ({ ...f, code: e.target.value }))} />
                </div>
                <div className="space-y-2">
                  <Label>Title</Label>
                  <Input value={form.title} onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))} />
                </div>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Applies to type</Label>
                  <Select
                    value={form.appliesToType ? String(form.appliesToType) : '__any'}
                    onValueChange={(v) => setForm((f) => ({ ...f, appliesToType: v === '__any' ? null : (v as EhcTicketType) }))}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Any" />
                    </SelectTrigger>
                    <SelectContent>
                      {ticketTypeOptions.map((o) => (
                        <SelectItem key={o.value} value={o.value}>
                          {o.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>Applies to category</Label>
                  <Select
                    value={form.categoryId ? String(form.categoryId) : '__any'}
                    onValueChange={(v) => setForm((f) => ({ ...f, categoryId: v === '__any' ? null : v }))}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Any" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="__any">Any</SelectItem>
                      {(categories ?? []).map((c) => (
                        <SelectItem key={c.id} value={c.id}>
                          {c.code} • {c.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>

              <div className="space-y-2">
                <Label>Body</Label>
                <Textarea value={form.body} onChange={(e) => setForm((f) => ({ ...f, body: e.target.value }))} rows={8} placeholder="Type the full reply..." />
              </div>

              <div className="flex items-center gap-3">
                <Switch checked={!!form.isActive} onCheckedChange={(v) => setForm((f) => ({ ...f, isActive: !!v }))} />
                <span className="text-sm text-slate-700">{form.isActive ? 'Active' : 'Inactive'}</span>
              </div>
            </CardContent>
          </Card>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => save.mutate()} disabled={save.isPending}>
              <Save className="h-4 w-4 mr-2" />
              {save.isPending ? 'Saving...' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
