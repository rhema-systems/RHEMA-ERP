'use client';

import React from 'react';
import { Plus } from 'lucide-react';
import { format } from 'date-fns';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';

import fleetService, {
  CreateFleetComplianceItemDto,
  FleetComplianceItemDto,
  FleetVehicleListDto,
} from '@/services/fleetService';

function formatDate(value: string) {
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  return format(d, 'dd MMM yyyy');
}

export default function FleetCompliancePage() {
  const { toast } = useToast();

  const [vehicles, setVehicles] = React.useState<FleetVehicleListDto[]>([]);
  const [vehicleId, setVehicleId] = React.useState<string>('');

  const [loading, setLoading] = React.useState(false);
  const [items, setItems] = React.useState<FleetComplianceItemDto[]>([]);

  const [createOpen, setCreateOpen] = React.useState(false);
  const [editOpen, setEditOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<FleetComplianceItemDto | null>(null);

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
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  React.useEffect(() => {
    loadCompliance();
  }, [loadCompliance]);

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

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Fleet Compliance</h1>
          <p className="text-muted-foreground">Track expiry and receive reminders (via Notification Topics)</p>
        </div>

        <Button onClick={openCreate} disabled={!vehicleId}>
          <Plus className="mr-2 h-4 w-4" />
          Add Compliance
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
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Type</TableHead>
                  <TableHead>Ref</TableHead>
                  <TableHead>Expiry</TableHead>
                  <TableHead>Critical</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {!vehicleId ? (
                  <TableRow>
                    <TableCell colSpan={5} className="py-8 text-center text-muted-foreground">
                      Select a vehicle to view compliance items
                    </TableCell>
                  </TableRow>
                ) : loading ? (
                  <TableRow>
                    <TableCell colSpan={5} className="py-8 text-center text-muted-foreground">
                      Loading...
                    </TableCell>
                  </TableRow>
                ) : items.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={5} className="py-8 text-center text-muted-foreground">
                      No compliance items
                    </TableCell>
                  </TableRow>
                ) : (
                  items.map((i) => (
                    <TableRow key={i.id}>
                      <TableCell className="font-medium">{i.complianceType}</TableCell>
                      <TableCell>{i.referenceNumber || '-'}</TableCell>
                      <TableCell>{formatDate(i.expiryDate)}</TableCell>
                      <TableCell>{i.isCritical ? 'Yes' : 'No'}</TableCell>
                      <TableCell className="text-right">
                        <Button variant="ghost" size="sm" onClick={() => openEdit(i)}>
                          Edit
                        </Button>
                        <Button variant="ghost" size="sm" onClick={() => remove(i.id)}>
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

