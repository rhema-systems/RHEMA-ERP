'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Pencil, Plus, Save, Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable';
import { ehcRootCauseAdminService, type CreateEhcRootCauseCodeRequest, type EhcRootCauseCode } from '@/services/ehcRootCauseAdminService';

type TableCellProps<T> = {
  row: {
    original: T;
  };
};

export default function HelpdeskRootCausesAdminPage() {
  const qc = useQueryClient();
  const { toast } = useToast();

  const { data, isLoading, error } = useQuery({
    queryKey: ['ehc', 'admin', 'root-causes'],
    queryFn: () => ehcRootCauseAdminService.list(false),
  });

  const items = useMemo(() => (data ?? []).slice().sort((a, b) => a.name.localeCompare(b.name)), [data]);

  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<EhcRootCauseCode | null>(null);
  const [form, setForm] = useState<CreateEhcRootCauseCodeRequest>({
    code: '',
    name: '',
    description: '',
    isActive: true,
  });

  const startCreate = () => {
    setEditing(null);
    setForm({ code: '', name: '', description: '', isActive: true });
    setOpen(true);
  };

  const startEdit = (row: EhcRootCauseCode) => {
    setEditing(row);
    setForm({
      code: row.code || '',
      name: row.name || '',
      description: row.description || '',
      isActive: !!row.isActive,
    });
    setOpen(true);
  };

  const save = useMutation({
    mutationFn: async () => {
      const payload: CreateEhcRootCauseCodeRequest = {
        code: form.code.trim(),
        name: form.name.trim(),
        description: form.description?.trim() || null,
        isActive: !!form.isActive,
      };
      if (!payload.code || !payload.name) throw new Error('Code and Name are required');

      if (editing) return ehcRootCauseAdminService.update(editing.id, payload);
      return ehcRootCauseAdminService.create(payload);
    },
    onSuccess: async () => {
      setOpen(false);
      setEditing(null);
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'root-causes'] });
      toast({ title: 'Saved', description: 'Root cause saved successfully.', variant: 'success' });
    },
    onError: (err: any) => {
      toast({ title: 'Error', description: err?.message || 'Failed to save root cause.', variant: 'destructive' });
    },
  });

  const del = useMutation({
    mutationFn: async (id: string) => {
      await ehcRootCauseAdminService.remove(id);
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'root-causes'] });
      toast({ title: 'Deleted', description: 'Root cause deleted.', variant: 'success' });
    },
    onError: (err: any) => {
      toast({ title: 'Error', description: err?.message || 'Failed to delete root cause.', variant: 'destructive' });
    },
  });

  const columns = useMemo<Array<DataTableColumn<EhcRootCauseCode>>>(() => {
    return [
      {
        id: 'code',
        header: 'Code',
        accessorKey: 'code',
        cell: ({ row }: TableCellProps<EhcRootCauseCode>) => (
          <button className="text-blue-600 hover:underline" onClick={() => startEdit(row.original)} title="Edit">
            {row.original.code}
          </button>
        ),
      },
      { id: 'name', header: 'Name', accessorKey: 'name' },
      {
        id: 'isActive',
        header: 'Active',
        accessorKey: 'isActive',
        cell: ({ row }: TableCellProps<EhcRootCauseCode>) => (row.original.isActive ? 'Yes' : 'No'),
      },
      {
        id: 'actions',
        header: '',
        accessorKey: 'id',
        enableHiding: false,
        cell: ({ row }: TableCellProps<EhcRootCauseCode>) => (
          <div className="flex justify-end gap-2">
            <Button variant="ghost" size="icon" onClick={() => startEdit(row.original)} title="Edit">
              <Pencil className="h-4 w-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              onClick={() => del.mutate(row.original.id)}
              title="Delete"
              disabled={del.isPending}
            >
              <Trash2 className="h-4 w-4 text-red-600" />
            </Button>
          </div>
        ),
      },
    ];
  }, [del.isPending]);

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold">Root Causes</h1>
          <p className="text-slate-600 mt-1">Configure RCA codes used on complaint tickets and analytics.</p>
        </div>
        <Button onClick={startCreate}>
          <Plus className="h-4 w-4 mr-2" />
          New Root Cause
        </Button>
      </div>

      <DataTable
        compact
        title="Root Cause Codes"
        data={items}
        columns={columns}
        loading={isLoading}
        error={error ? 'Failed to load root causes.' : null}
        enableSearch
        enablePagination
        exportFileName={`ehc-root-causes-${new Date().toISOString().slice(0, 10)}`}
        exportFormats={['csv', 'excel']}
      />

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-[700px]">
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit Root Cause' : 'Create Root Cause'}</DialogTitle>
            <DialogDescription>Codes should be short and stable (used in reports).</DialogDescription>
          </DialogHeader>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Details</CardTitle>
              <CardDescription>Fill in code + name, then enable/disable.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Code</Label>
                  <Input value={form.code} onChange={(e) => setForm((f) => ({ ...f, code: e.target.value }))} />
                </div>
                <div className="space-y-2">
                  <Label>Name</Label>
                  <Input value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} />
                </div>
              </div>

              <div className="space-y-2">
                <Label>Description (optional)</Label>
                <Textarea value={form.description ?? ''} onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))} />
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
