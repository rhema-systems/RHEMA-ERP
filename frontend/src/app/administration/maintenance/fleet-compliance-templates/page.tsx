'use client';

import React from 'react';
import { Plus, Trash2, Pencil } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/hooks/use-toast';

import fleetService, {
  CreateFleetComplianceTemplateDto,
  CreateFleetComplianceTemplateItemDto,
  FleetComplianceTemplateDto,
} from '@/services/fleetService';

type TemplateForm = CreateFleetComplianceTemplateDto;

function emptyForm(): TemplateForm {
  return {
    name: '',
    description: '',
    isActive: true,
    items: [],
  };
}

export default function FleetComplianceTemplatesPage() {
  const { toast } = useToast();

  const [templates, setTemplates] = React.useState<FleetComplianceTemplateDto[]>([]);
  const [loading, setLoading] = React.useState(false);

  const [dialogOpen, setDialogOpen] = React.useState(false);
  const [mode, setMode] = React.useState<'create' | 'edit'>('create');
  const [editingId, setEditingId] = React.useState<string | null>(null);

  const [form, setForm] = React.useState<TemplateForm>(emptyForm());

  const [deleteOpen, setDeleteOpen] = React.useState(false);
  const [deleting, setDeleting] = React.useState<FleetComplianceTemplateDto | null>(null);

  const load = React.useCallback(async () => {
    setLoading(true);
    try {
      const res = await fleetService.getComplianceTemplates(true);
      setTemplates(res || []);
    } catch (e: any) {
      toast({ title: 'Failed to load templates', description: e?.message || String(e), variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  }, [toast]);

  React.useEffect(() => {
    load();
  }, [load]);

  const openCreate = () => {
    setMode('create');
    setEditingId(null);
    setForm(emptyForm());
    setDialogOpen(true);
  };

  const openEdit = async (t: FleetComplianceTemplateDto) => {
    try {
      const full = await fleetService.getComplianceTemplate(t.id);
      setMode('edit');
      setEditingId(t.id);
      setForm({
        name: full.name || '',
        description: full.description || '',
        isActive: !!full.isActive,
        items:
          (full.items || []).map((i) => ({
            complianceType: i.complianceType || '',
            isCritical: !!i.isCritical,
            sortOrder: i.sortOrder ?? 0,
          })) || [],
      });
      setDialogOpen(true);
    } catch (e: any) {
      toast({ title: 'Failed to load template', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  const addItem = () => {
    setForm((p) => ({
      ...p,
      items: [...(p.items || []), { complianceType: '', isCritical: true, sortOrder: (p.items?.length || 0) + 1 }],
    }));
  };

  const updateItem = (idx: number, patch: Partial<CreateFleetComplianceTemplateItemDto>) => {
    setForm((p) => {
      const next = [...(p.items || [])];
      next[idx] = { ...next[idx], ...patch };
      return { ...p, items: next };
    });
  };

  const removeItem = (idx: number) => {
    setForm((p) => {
      const next = [...(p.items || [])];
      next.splice(idx, 1);
      return { ...p, items: next };
    });
  };

  const save = async () => {
    try {
      if (!form.name.trim()) throw new Error('Template name is required');

      const dto: CreateFleetComplianceTemplateDto = {
        name: form.name.trim(),
        description: form.description?.trim() || null,
        isActive: !!form.isActive,
        items: (form.items || [])
          .map((i) => ({
            complianceType: i.complianceType.trim(),
            isCritical: !!i.isCritical,
            sortOrder: Number.isFinite(i.sortOrder) ? i.sortOrder : 0,
          }))
          .filter((i) => !!i.complianceType),
      };

      if (mode === 'create') {
        await fleetService.createComplianceTemplate(dto);
        toast({ title: 'Template created' });
      } else {
        if (!editingId) throw new Error('Missing template id');
        await fleetService.updateComplianceTemplate(editingId, dto);
        toast({ title: 'Template updated' });
      }

      setDialogOpen(false);
      await load();
    } catch (e: any) {
      toast({ title: 'Save failed', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  const del = async (id: string) => {
    try {
      await fleetService.deleteComplianceTemplate(id);
      toast({ title: 'Template deleted' });
      await load();
    } catch (e: any) {
      toast({ title: 'Delete failed', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Compliance Templates</h1>
          <p className="text-muted-foreground">Admin setup: create reusable checklists (roadworthy, insurance, registration, etc.)</p>
        </div>
        <Button onClick={openCreate}>
          <Plus className="mr-2 h-4 w-4" />
          New Template
        </Button>
      </div>

      <Card>
        <CardHeader className="flex-row items-center justify-between">
          <CardTitle>Templates</CardTitle>
          <Button variant="outline" onClick={load} disabled={loading}>
            Refresh
          </Button>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow className="bg-muted/40">
                  <TableHead>Name</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Items</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {loading ? (
                  <TableRow>
                    <TableCell colSpan={4} className="py-8 text-center text-muted-foreground">
                      Loading...
                    </TableCell>
                  </TableRow>
                ) : templates.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={4} className="py-8 text-center text-muted-foreground">
                      No templates yet
                    </TableCell>
                  </TableRow>
                ) : (
                  templates.map((t) => (
                    <TableRow key={t.id} className="hover:bg-muted/30">
                      <TableCell className="font-medium">{t.name}</TableCell>
                      <TableCell>{t.isActive ? <Badge variant="secondary">Active</Badge> : <Badge variant="outline">Inactive</Badge>}</TableCell>
                      <TableCell>{t.items?.length ?? '—'}</TableCell>
                      <TableCell className="text-right">
                        <Button variant="ghost" size="sm" onClick={() => openEdit(t)}>
                          <Pencil className="mr-2 h-4 w-4" />
                          Edit
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => {
                            setDeleting(t);
                            setDeleteOpen(true);
                          }}
                        >
                          <Trash2 className="mr-2 h-4 w-4" />
                          Delete
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={deleteOpen}
        onOpenChange={(o) => {
          setDeleteOpen(o);
          if (!o) setDeleting(null);
        }}
        title="Delete template?"
        description={deleting ? `${deleting.name} will be removed.` : undefined}
        confirmText="Delete"
        variant="destructive"
        onConfirm={async () => {
          if (!deleting) return;
          await del(deleting.id);
        }}
      />

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-5xl">
          <DialogHeader>
            <DialogTitle>{mode === 'create' ? 'New Template' : 'Edit Template'}</DialogTitle>
            <DialogDescription>Checklist items will be created on a vehicle when the template is applied.</DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label>Name</Label>
              <Input value={form.name} onChange={(e) => setForm((p) => ({ ...p, name: e.target.value }))} />
            </div>
            <div className="flex items-center gap-3 md:pt-7">
              <Switch checked={form.isActive} onCheckedChange={(v) => setForm((p) => ({ ...p, isActive: v }))} />
              <div className="text-sm font-medium">Active</div>
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>Description</Label>
              <Textarea value={form.description || ''} onChange={(e) => setForm((p) => ({ ...p, description: e.target.value }))} />
            </div>
          </div>

          <div className="mt-4 space-y-3">
            <div className="flex items-center justify-between">
              <div>
                <div className="text-sm font-medium">Checklist</div>
                <div className="text-xs text-muted-foreground">Add compliance types (e.g., Roadworthy, Insurance, Registration)</div>
              </div>
              <Button variant="outline" onClick={addItem}>
                <Plus className="mr-2 h-4 w-4" />
                Add Item
              </Button>
            </div>

            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow className="bg-muted/40">
                    <TableHead>Type</TableHead>
                    <TableHead className="w-[140px]">Sort</TableHead>
                    <TableHead className="w-[160px]">Critical</TableHead>
                    <TableHead className="w-[120px]" />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {(form.items || []).length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={4} className="py-6 text-center text-muted-foreground">
                        No checklist items
                      </TableCell>
                    </TableRow>
                  ) : (
                    (form.items || []).map((it, idx) => (
                      <TableRow key={idx} className="hover:bg-muted/30">
                        <TableCell>
                          <Input
                            value={it.complianceType}
                            onChange={(e) => updateItem(idx, { complianceType: e.target.value })}
                            placeholder="e.g. Insurance"
                          />
                        </TableCell>
                        <TableCell>
                          <Input
                            type="number"
                            value={String(it.sortOrder ?? 0)}
                            onChange={(e) => updateItem(idx, { sortOrder: parseInt(e.target.value || '0', 10) })}
                          />
                        </TableCell>
                        <TableCell>
                          <div className="flex items-center gap-2">
                            <Switch checked={!!it.isCritical} onCheckedChange={(v) => updateItem(idx, { isCritical: v })} />
                            <span className="text-sm">{it.isCritical ? 'Yes' : 'No'}</span>
                          </div>
                        </TableCell>
                        <TableCell className="text-right">
                          <Button variant="ghost" size="sm" onClick={() => removeItem(idx)}>
                            <Trash2 className="mr-2 h-4 w-4" />
                            Remove
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={save}>Save</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
