'use client';

import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BarChart3, Pencil } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { ehcAdminService, type EhcTicketPriorityLevelAdmin, type UpdateEhcTicketPriorityLevelAdmin } from '@/services/ehcAdminService';

type TableCellProps<T> = {
  row: {
    original: T;
  };
};

export default function AdminHelpdeskPrioritiesPage() {
  const qc = useQueryClient();
  const { toast } = useToast();

  const { data: priorities = [], isLoading, error } = useQuery({
    queryKey: ['ehc', 'admin', 'priorities'],
    queryFn: () => ehcAdminService.listTicketPriorityLevels(),
  });

  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<EhcTicketPriorityLevelAdmin | null>(null);
  const [form, setForm] = useState<UpdateEhcTicketPriorityLevelAdmin>({
    displayName: '',
    description: '',
    isActive: true,
    sortOrder: 0,
  });

  useEffect(() => {
    if (!open) {
      setEditing(null);
      setForm({ displayName: '', description: '', isActive: true, sortOrder: 0 });
    }
  }, [open]);

  const save = useMutation({
    mutationFn: async () => {
      if (!editing) return;
      const payload: UpdateEhcTicketPriorityLevelAdmin = {
        displayName: form.displayName,
        description: form.description || null,
        isActive: form.isActive,
        sortOrder: Number.isFinite(form.sortOrder) ? form.sortOrder : 0,
      };
      await ehcAdminService.updateTicketPriorityLevel(editing.priority, payload);
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'priorities'] });
      toast({ title: 'Saved', description: 'Priority level updated.', variant: 'success' });
      setOpen(false);
    },
    onError: (err) => toast({ title: 'Error', description: err instanceof Error ? err.message : 'Failed', variant: 'destructive' }),
  });

  const columns = useMemo<Array<DataTableColumn<EhcTicketPriorityLevelAdmin>>>(() => {
    return [
      { id: 'sortOrder', header: '#', accessorKey: 'sortOrder' },
      { id: 'priority', header: 'Priority', accessorKey: 'priority' },
      { id: 'displayName', header: 'Display Name', accessorKey: 'displayName' },
      { id: 'isActive', header: 'Active', accessorKey: 'isActive', cell: ({ row }: TableCellProps<EhcTicketPriorityLevelAdmin>) => (row.original.isActive ? 'Yes' : 'No') },
      { id: 'description', header: 'Description', accessorFn: (r: EhcTicketPriorityLevelAdmin) => r.description || '—' },
      {
        id: 'actions',
        header: '',
        accessorKey: 'priority',
        enableHiding: false,
        cell: ({ row }: TableCellProps<EhcTicketPriorityLevelAdmin>) => (
          <div className="flex justify-end">
            <Button
              variant="ghost"
              size="icon"
              onClick={() => {
                setEditing(row.original);
                setForm({
                  displayName: row.original.displayName,
                  description: row.original.description ?? '',
                  isActive: row.original.isActive,
                  sortOrder: row.original.sortOrder ?? 0,
                });
                setOpen(true);
              }}
              title="Edit"
            >
              <Pencil className="h-4 w-4" />
            </Button>
          </div>
        ),
      },
    ];
  }, []);

  const sorted = useMemo(() => {
    return [...priorities].sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0));
  }, [priorities]);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold flex items-center gap-2">
          <BarChart3 className="h-7 w-7" />
          Priority Levels
        </h1>
        <p className="text-slate-600 mt-1">
          Configure how ticket priorities appear in the UI. Tickets still store the standard values (Low/Medium/High/Critical).
        </p>
      </div>

      <Card>
        <CardHeader className="p-4 pb-2">
          <CardTitle>Priorities</CardTitle>
          <CardDescription className="text-xs">Disable priorities you don’t want users to select, and control sort order.</CardDescription>
        </CardHeader>
        <CardContent className="p-4 pt-0">
          {error ? <div className="text-sm text-red-600 mb-2">Failed to load priorities.</div> : null}
          <DataTable
            compact
            data={sorted}
            columns={columns}
            loading={isLoading}
            enableColumnFilters={false}
            enableExport={true}
            exportFormats={['csv', 'excel']}
            exportFileName={`helpdesk-priorities-${new Date().toISOString().slice(0, 10)}`}
            initialColumnVisibility={{ description: false }}
          />
        </CardContent>
      </Card>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-[640px]">
          <DialogHeader>
            <DialogTitle>Edit Priority</DialogTitle>
            <DialogDescription>{editing ? `Update settings for ${editing.priority}` : ''}</DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-1 gap-3">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div className="space-y-1">
                <Label>Display Name</Label>
                <Input value={form.displayName} onChange={(e) => setForm((s) => ({ ...s, displayName: e.target.value }))} />
              </div>
              <div className="space-y-1">
                <Label>Sort Order</Label>
                <Input
                  type="number"
                  value={String(form.sortOrder ?? 0)}
                  onChange={(e) => setForm((s) => ({ ...s, sortOrder: parseInt(e.target.value || '0', 10) }))}
                />
              </div>
            </div>

            <div className="space-y-1">
              <Label>Description</Label>
              <Textarea value={form.description || ''} onChange={(e) => setForm((s) => ({ ...s, description: e.target.value }))} rows={4} />
            </div>

            <div className="flex items-center justify-between rounded-md border p-3">
              <div>
                <div className="font-medium">Active</div>
                <div className="text-xs text-slate-500">Inactive priorities won’t appear in the priority dropdowns.</div>
              </div>
              <Switch checked={form.isActive} onCheckedChange={(v) => setForm((s) => ({ ...s, isActive: v }))} />
            </div>

            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => setOpen(false)}>
                Cancel
              </Button>
              <Button onClick={() => save.mutate()} disabled={save.isPending || !editing}>
                Save
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
