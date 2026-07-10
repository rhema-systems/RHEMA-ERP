'use client';

import React from 'react';
import { Plus } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';

import fleetService, {
  CreateFleetComplianceItemDto,
  FleetComplianceItemDto,
  FleetComplianceTemplateDto,
  FleetVehicleListDto,
} from '@/services/fleetService';
import maintenanceSettingsService from '@/services/maintenanceSettingsService';
import { formatFleetDate } from '@/lib/date-format';
import MaintenanceAttachmentsPanel from '@/components/maintenance/MaintenanceAttachmentsPanel';

function getDueStatus(expiryDateIso?: string | null, dueSoonDays?: number): 'NotSet' | 'Overdue' | 'DueSoon' | 'Ok' {
  if (!expiryDateIso) return 'NotSet';
  const expiry = new Date(expiryDateIso);
  if (Number.isNaN(expiry.getTime())) return 'Ok';

  const now = new Date();
  const expiryDateOnlyUtc = Date.UTC(expiry.getFullYear(), expiry.getMonth(), expiry.getDate());
  const nowDateOnlyUtc = Date.UTC(now.getFullYear(), now.getMonth(), now.getDate());

  const diffDays = Math.floor((expiryDateOnlyUtc - nowDateOnlyUtc) / (1000 * 60 * 60 * 24));
  if (diffDays < 0) return 'Overdue';
  if (diffDays <= Math.max(0, dueSoonDays ?? 7)) return 'DueSoon';
  return 'Ok';
}

export default function FleetCompliancePage() {
  const { toast } = useToast();

  const [vehicles, setVehicles] = React.useState<FleetVehicleListDto[]>([]);
  const [vehicleId, setVehicleId] = React.useState<string>('');
  const [dueSoonDays, setDueSoonDays] = React.useState<number>(7);

  const [templates, setTemplates] = React.useState<FleetComplianceTemplateDto[]>([]);
  const [templateId, setTemplateId] = React.useState<string>('');
  const [applyingTemplate, setApplyingTemplate] = React.useState(false);

  const [loading, setLoading] = React.useState(false);
  const [items, setItems] = React.useState<FleetComplianceItemDto[]>([]);

  const [createOpen, setCreateOpen] = React.useState(false);
  const [editOpen, setEditOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<FleetComplianceItemDto | null>(null);

  const [deleteOpen, setDeleteOpen] = React.useState(false);
  const [deleting, setDeleting] = React.useState<FleetComplianceItemDto | null>(null);

  const [docsOpen, setDocsOpen] = React.useState(false);
  const [docsItem, setDocsItem] = React.useState<FleetComplianceItemDto | null>(null);

  const [form, setForm] = React.useState<CreateFleetComplianceItemDto>({
    vehicleAssetId: '',
    complianceType: '',
    referenceNumber: '',
    issueDate: '',
    expiryDate: '',
    isCritical: true,
    notes: '',
    documentLinks: '',
  });

  const loadVehicles = React.useCallback(async () => {
    try {
      const res = await fleetService.getVehicles({ page: 1, pageSize: 200 });
      setVehicles(res.items || []);
    } catch (e: any) {
      toast({ title: 'Failed to load vehicles', description: e?.message || String(e), variant: 'destructive' });
    }
  }, [toast]);

  const loadTemplates = React.useCallback(async () => {
    try {
      const res = await fleetService.getComplianceTemplates(true);
      setTemplates(res || []);
    } catch (e: any) {
      toast({ title: 'Failed to load templates', description: e?.message || String(e), variant: 'destructive' });
    }
  }, [toast]);

  const loadCompliance = React.useCallback(async () => {
    if (!vehicleId) return;
    setLoading(true);
    try {
      const res = await fleetService.getCompliance(vehicleId, 1, 200);
      setItems(res.items || []);
    } catch (e: any) {
      toast({ title: 'Failed to load compliance', description: e?.message || String(e), variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  }, [vehicleId, toast]);

  React.useEffect(() => {
    loadVehicles();
    loadTemplates();
     
  }, []);

  React.useEffect(() => {
    (async () => {
      try {
        const s = await maintenanceSettingsService.getSettings();
        if (typeof s.fleetComplianceDueSoonDays === 'number') setDueSoonDays(s.fleetComplianceDueSoonDays);
      } catch {
        // best-effort (use fallback)
      }
    })();
  }, []);

  React.useEffect(() => {
    loadCompliance();
  }, [loadCompliance]);

  React.useEffect(() => {
    if (!vehicleId) {
      setTemplateId('');
      return;
    }

    (async () => {
      try {
        const current = await fleetService.getVehicleComplianceTemplate(vehicleId);
        setTemplateId(current?.templateId || '');
      } catch {
        setTemplateId('');
      }
    })();
  }, [vehicleId]);

  const applyTemplate = async (nextTemplateId: string) => {
    if (!vehicleId) {
      toast({ title: 'Select a vehicle first', variant: 'destructive' });
      return;
    }
    if (!nextTemplateId) {
      toast({ title: 'Select a template', variant: 'destructive' });
      return;
    }

    setApplyingTemplate(true);
    try {
      await fleetService.applyComplianceTemplate(vehicleId, nextTemplateId);
      setTemplateId(nextTemplateId);
      toast({ title: 'Template applied', description: 'Checklist rows were created for this vehicle.' });
      await loadCompliance();
    } catch (e: any) {
      toast({ title: 'Failed to apply template', description: e?.message || String(e), variant: 'destructive' });
    } finally {
      setApplyingTemplate(false);
    }
  };

  const openCreate = () => {
    if (!vehicleId) {
      toast({ title: 'Select a vehicle first', variant: 'destructive' });
      return;
    }
    setForm({
      vehicleAssetId: vehicleId,
      complianceType: '',
      referenceNumber: '',
      issueDate: '',
      expiryDate: '',
      isCritical: true,
      notes: '',
      documentLinks: '',
    });
    setCreateOpen(true);
  };

  const openEdit = (item: FleetComplianceItemDto) => {
    setEditing(item);
    setForm({
      vehicleAssetId: item.vehicleAssetId,
      complianceType: item.complianceType,
      referenceNumber: item.referenceNumber || '',
      issueDate: item.issueDate ? item.issueDate.substring(0, 10) : '',
      expiryDate: item.expiryDate ? item.expiryDate.substring(0, 10) : '',
      isCritical: !!item.isCritical,
      notes: item.notes || '',
      documentLinks: '',
    });
    setEditOpen(true);
  };

  const save = async (mode: 'create' | 'edit') => {
    try {
      if (!form.vehicleAssetId) throw new Error('Vehicle is required');
      if (!form.complianceType.trim()) throw new Error('Compliance type is required');
      if (!form.expiryDate) throw new Error('Expiry date is required');

      const dto: CreateFleetComplianceItemDto = {
        ...form,
        complianceType: form.complianceType.trim(),
        referenceNumber: form.referenceNumber?.trim() || null,
        issueDate: form.issueDate ? new Date(form.issueDate).toISOString() : null,
        expiryDate: new Date(form.expiryDate).toISOString(),
        notes: form.notes?.trim() || null,
        documentLinks: form.documentLinks?.trim() || null,
        isCritical: !!form.isCritical,
      };

      if (mode === 'create') {
        await fleetService.createCompliance(dto);
        toast({ title: 'Compliance item created' });
        setCreateOpen(false);
      } else if (editing) {
        await fleetService.updateCompliance(editing.id, dto);
        toast({ title: 'Compliance item updated' });
        setEditOpen(false);
        setEditing(null);
      }

      await loadCompliance();
    } catch (e: any) {
      toast({ title: 'Save failed', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  const remove = async (id: string) => {
    try {
      await fleetService.deleteCompliance(id);
      toast({ title: 'Deleted' });
      await loadCompliance();
    } catch (e: any) {
      toast({ title: 'Delete failed', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  const openDocs = (item: FleetComplianceItemDto) => {
    setDocsItem(item);
    setDocsOpen(true);
  };

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Fleet Compliance</h1>
          <p className="text-muted-foreground">Track expiry and receive reminders (via Notification Topics)</p>
        </div>

        <Button onClick={openCreate} disabled={!vehicleId}>
          <Plus className="mr-2 h-4 w-4" />
          Add Custom Item
        </Button>
      </div>

      <Card>
        <CardHeader className="space-y-3">
          <CardTitle>Vehicle</CardTitle>
          <Select value={vehicleId || undefined} onValueChange={(v) => setVehicleId(v)}>
            <SelectTrigger className="w-full md:w-[420px]">
              <SelectValue placeholder="Select vehicle" />
            </SelectTrigger>
            <SelectContent>
              {vehicles.map((v) => (
                <SelectItem key={v.id} value={v.id}>
                  {v.assetNumber} — {v.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <div className="space-y-2">
            <Label>Compliance Template</Label>
            <div className="flex flex-col gap-2 md:flex-row md:items-center">
              <Select value={templateId || undefined} onValueChange={(v) => applyTemplate(v)} disabled={!vehicleId || applyingTemplate}>
                <SelectTrigger className="w-full md:w-[420px]">
                  <SelectValue placeholder={vehicleId ? 'Select template' : 'Select vehicle first'} />
                </SelectTrigger>
                <SelectContent>
                  {templates.map((t) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.name}
                      {!t.isActive ? ' (inactive)' : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Button
                variant="outline"
                onClick={async () => {
                  await loadTemplates();
                  toast({ title: 'Templates refreshed' });
                }}
                disabled={applyingTemplate}
              >
                Refresh templates
              </Button>
            </div>
            <p className="text-xs text-muted-foreground">
              Select a template to auto-create the compliance checklist rows for this vehicle.
            </p>
          </div>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow className="bg-muted/40">
                  <TableHead>Type</TableHead>
                  <TableHead>Ref</TableHead>
                  <TableHead>Issue</TableHead>
                  <TableHead>Expiry</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Critical</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {!vehicleId ? (
                  <TableRow>
                    <TableCell colSpan={7} className="py-8 text-center text-muted-foreground">
                      Select a vehicle to view compliance items
                    </TableCell>
                  </TableRow>
                ) : loading ? (
                  <TableRow>
                    <TableCell colSpan={7} className="py-8 text-center text-muted-foreground">
                      Loading...
                    </TableCell>
                  </TableRow>
                ) : items.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7} className="py-8 text-center text-muted-foreground">
                      No compliance items
                    </TableCell>
                  </TableRow>
                ) : (
                  items.map((i, idx) => {
                    const dueStatus = getDueStatus(i.expiryDate, dueSoonDays);
                    return (
                      <TableRow key={i.id} className={idx % 2 === 1 ? 'bg-muted/10 hover:bg-muted/30' : 'hover:bg-muted/30'}>
                        <TableCell className="font-medium">{i.complianceType}</TableCell>
                        <TableCell>{i.referenceNumber || '-'}</TableCell>
                        <TableCell>{formatFleetDate(i.issueDate)}</TableCell>
                        <TableCell>{formatFleetDate(i.expiryDate)}</TableCell>
                        <TableCell>
                          {dueStatus === 'NotSet' ? (
                            <Badge variant="outline">Not set</Badge>
                          ) : dueStatus === 'Overdue' ? (
                            <Badge variant="destructive">Overdue</Badge>
                          ) : dueStatus === 'DueSoon' ? (
                            <Badge className="bg-amber-100 text-amber-800 hover:bg-amber-100">Due soon</Badge>
                          ) : (
                            <Badge className="bg-emerald-100 text-emerald-800 hover:bg-emerald-100">OK</Badge>
                          )}
                        </TableCell>
                        <TableCell>
                          {i.isCritical ? <Badge variant="secondary">Critical</Badge> : <span className="text-muted-foreground">No</span>}
                        </TableCell>
                        <TableCell className="text-right">
                          <Button variant="ghost" size="sm" onClick={() => openEdit(i)}>
                            Edit
                          </Button>
                          <Button variant="ghost" size="sm" onClick={() => openDocs(i)}>
                            Documents
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => {
                              setDeleting(i);
                              setDeleteOpen(true);
                            }}
                          >
                            Delete
                          </Button>
                        </TableCell>
                      </TableRow>
                    );
                  })
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
        title="Delete compliance record?"
        description={deleting ? `${deleting.complianceType} (${formatFleetDate(deleting.expiryDate)}) will be removed.` : undefined}
        confirmText="Delete"
        variant="destructive"
        onConfirm={async () => {
          if (!deleting) return;
          await remove(deleting.id);
        }}
      />

      <Dialog
        open={docsOpen}
        onOpenChange={(o) => {
          setDocsOpen(o);
          if (!o) setDocsItem(null);
        }}
      >
        <DialogContent className="max-w-5xl">
          <DialogHeader>
            <DialogTitle>Compliance Documents</DialogTitle>
            <DialogDescription>
              {docsItem ? `${docsItem.complianceType} — Expires ${formatFleetDate(docsItem.expiryDate, 'Not set')}` : 'Upload and view documents.'}
            </DialogDescription>
          </DialogHeader>

          {docsItem ? (
            <MaintenanceAttachmentsPanel
              entityType="FleetCompliance"
              entityId={docsItem.id}
              category="ComplianceDocs"
              title="Documents"
              description="Upload roadworthy, insurance, registration, permits, and related documents."
            />
          ) : (
            <div className="py-6 text-sm text-muted-foreground">Select a compliance record to view documents.</div>
          )}

          <DialogFooter>
            <Button variant="outline" onClick={() => setDocsOpen(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>Add Compliance</DialogTitle>
            <DialogDescription>Critical items can block dispatch depending on Maintenance settings.</DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label>Type</Label>
              <Input value={form.complianceType} onChange={(e) => setForm((p) => ({ ...p, complianceType: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Reference</Label>
              <Input value={form.referenceNumber || ''} onChange={(e) => setForm((p) => ({ ...p, referenceNumber: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Issue Date</Label>
              <Input type="date" value={form.issueDate || ''} onChange={(e) => setForm((p) => ({ ...p, issueDate: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Expiry Date</Label>
              <Input type="date" value={form.expiryDate || ''} onChange={(e) => setForm((p) => ({ ...p, expiryDate: e.target.value }))} />
            </div>
            <div className="flex items-center gap-3 md:col-span-2">
              <Switch checked={form.isCritical} onCheckedChange={(v) => setForm((p) => ({ ...p, isCritical: v }))} />
              <div>
                <div className="text-sm font-medium">Critical (dispatch-blocking)</div>
                <div className="text-xs text-muted-foreground">When enabled, expiry/due-soon can block dispatch.</div>
              </div>
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>Notes</Label>
              <Textarea value={form.notes || ''} onChange={(e) => setForm((p) => ({ ...p, notes: e.target.value }))} />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => save('create')}>Save</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>Edit Compliance</DialogTitle>
            <DialogDescription>Update expiry and critical flag.</DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label>Type</Label>
              <Input value={form.complianceType} onChange={(e) => setForm((p) => ({ ...p, complianceType: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Reference</Label>
              <Input value={form.referenceNumber || ''} onChange={(e) => setForm((p) => ({ ...p, referenceNumber: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Issue Date</Label>
              <Input type="date" value={form.issueDate || ''} onChange={(e) => setForm((p) => ({ ...p, issueDate: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Expiry Date</Label>
              <Input type="date" value={form.expiryDate || ''} onChange={(e) => setForm((p) => ({ ...p, expiryDate: e.target.value }))} />
            </div>
            <div className="flex items-center gap-3 md:col-span-2">
              <Switch checked={form.isCritical} onCheckedChange={(v) => setForm((p) => ({ ...p, isCritical: v }))} />
              <div className="text-sm font-medium">Critical (dispatch-blocking)</div>
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>Notes</Label>
              <Textarea value={form.notes || ''} onChange={(e) => setForm((p) => ({ ...p, notes: e.target.value }))} />
            </div>
          </div>

          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => {
                setEditOpen(false);
                setEditing(null);
              }}
            >
              Cancel
            </Button>
            <Button onClick={() => save('edit')}>Save</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

