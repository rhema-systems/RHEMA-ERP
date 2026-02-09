'use client';

import * as React from 'react';
import { fleetService, FleetBatteryDto, FleetVehicleListDto } from '@/services/fleetService';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';

function formatDate(value: string) {
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  return d.toLocaleString();
}

export default function FleetBatteriesPage() {
  const [vehicles, setVehicles] = React.useState<FleetVehicleListDto[]>([]);
  const [vehicleId, setVehicleId] = React.useState<string>('none');
  const [items, setItems] = React.useState<FleetBatteryDto[]>([]);
  const [loading, setLoading] = React.useState(false);

  const [open, setOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<FleetBatteryDto | null>(null);
  const [form, setForm] = React.useState({
    serialNumber: '',
    brand: '',
    spec: '',
    position: '',
    status: 'Installed',
    notes: '',
  });

  const loadVehicles = React.useCallback(async () => {
    try {
      const res = await fleetService.getVehicles({ page: 1, pageSize: 100 });
      setVehicles(res.items ?? []);
    } catch (e) {
      console.error(e);
    }
  }, []);

  const load = React.useCallback(async () => {
    if (vehicleId === 'none') {
      setItems([]);
      return;
    }
    setLoading(true);
    try {
      const res = await fleetService.getBatteries(vehicleId, 1, 50);
      setItems(res.items ?? []);
    } catch (e) {
      console.error(e);
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [vehicleId]);

  React.useEffect(() => {
    loadVehicles();
  }, [loadVehicles]);

  React.useEffect(() => {
    load();
  }, [load]);

  const openCreate = () => {
    setEditing(null);
    setForm({ serialNumber: '', brand: '', spec: '', position: '', status: 'Installed', notes: '' });
    setOpen(true);
  };

  const openEdit = (b: FleetBatteryDto) => {
    setEditing(b);
    setForm({
      serialNumber: b.serialNumber,
      brand: b.brand ?? '',
      spec: b.spec ?? '',
      position: b.position ?? '',
      status: b.status,
      notes: b.notes ?? '',
    });
    setOpen(true);
  };

  const save = async () => {
    if (vehicleId === 'none') return;
    if (!form.serialNumber.trim()) return;
    const dto = {
      vehicleAssetId: vehicleId,
      serialNumber: form.serialNumber.trim(),
      brand: form.brand || undefined,
      spec: form.spec || undefined,
      position: form.position || undefined,
      installedAtUtc: undefined,
      status: form.status,
      notes: form.notes || undefined,
    };
    try {
      if (editing) {
        await fleetService.updateBattery(editing.id, dto);
      } else {
        await fleetService.createBattery(dto);
      }
      setOpen(false);
      await load();
    } catch (e: any) {
      alert(e?.message || 'Failed to save battery');
    }
  };

  return (
    <div className="space-y-6 p-6">
      <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Fleet Batteries</h1>
          <p className="text-muted-foreground">Track vehicle batteries (serials, specs, positions)</p>
        </div>
        <div className="flex flex-col gap-3 md:flex-row">
          <div className="w-full md:w-[360px]">
            <Label>Vehicle</Label>
            <Select value={vehicleId} onValueChange={setVehicleId}>
              <SelectTrigger>
                <SelectValue placeholder="Select vehicle" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="none">Select vehicle</SelectItem>
                {vehicles.map((v) => (
                  <SelectItem key={v.id} value={v.id}>
                    {v.name} {v.licensePlate ? `(${v.licensePlate})` : ''}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <Button onClick={openCreate} disabled={vehicleId === 'none'}>
            Add Battery
          </Button>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Batteries</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {vehicleId === 'none' ? (
            <div className="text-sm text-muted-foreground">Select a vehicle to view batteries.</div>
          ) : loading ? (
            <div className="text-sm text-muted-foreground">Loading…</div>
          ) : items.length === 0 ? (
            <div className="text-sm text-muted-foreground">No batteries recorded.</div>
          ) : (
            <div className="overflow-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b text-left text-muted-foreground">
                    <th className="py-2">Serial</th>
                    <th className="py-2">Position</th>
                    <th className="py-2">Brand</th>
                    <th className="py-2">Spec</th>
                    <th className="py-2">Installed</th>
                    <th className="py-2">Status</th>
                    <th className="py-2" />
                  </tr>
                </thead>
                <tbody>
                  {items.map((b) => (
                    <tr key={b.id} className="border-b">
                      <td className="py-2">{b.serialNumber}</td>
                      <td className="py-2">{b.position ?? '—'}</td>
                      <td className="py-2">{b.brand ?? '—'}</td>
                      <td className="py-2">{b.spec ?? '—'}</td>
                      <td className="py-2">{formatDate(b.installedAtUtc)}</td>
                      <td className="py-2">{b.status}</td>
                      <td className="py-2 text-right">
                        <div className="flex justify-end gap-2">
                          <Button variant="outline" size="sm" onClick={() => openEdit(b)}>
                            Edit
                          </Button>
                          <Button
                            variant="destructive"
                            size="sm"
                            onClick={async () => {
                              if (!confirm('Delete this battery record?')) return;
                              await fleetService.deleteBattery(b.id);
                              await load();
                            }}
                          >
                            Delete
                          </Button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-[760px]">
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit Battery' : 'Add Battery'}</DialogTitle>
          </DialogHeader>

          <div className="grid gap-4 md:grid-cols-3">
            <div className="space-y-2 md:col-span-2">
              <Label>Serial Number</Label>
              <Input value={form.serialNumber} onChange={(e) => setForm((p) => ({ ...p, serialNumber: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Status</Label>
              <Select value={form.status} onValueChange={(v) => setForm((p) => ({ ...p, status: v }))}>
                <SelectTrigger>
                  <SelectValue placeholder="Status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Installed">Installed</SelectItem>
                  <SelectItem value="InStock">In Stock</SelectItem>
                  <SelectItem value="Removed">Removed</SelectItem>
                  <SelectItem value="Disposed">Disposed</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Position</Label>
              <Input value={form.position} onChange={(e) => setForm((p) => ({ ...p, position: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Brand</Label>
              <Input value={form.brand} onChange={(e) => setForm((p) => ({ ...p, brand: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Spec</Label>
              <Input value={form.spec} onChange={(e) => setForm((p) => ({ ...p, spec: e.target.value }))} placeholder="12V 100Ah" />
            </div>

            <div className="space-y-2 md:col-span-3">
              <Label>Notes</Label>
              <Textarea value={form.notes} onChange={(e) => setForm((p) => ({ ...p, notes: e.target.value }))} rows={4} />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button onClick={save} disabled={!form.serialNumber.trim()}>
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

