'use client';

import * as React from 'react';
import { fleetService, FleetBatteryDto, FleetBatteryEventDto, FleetBatteryKpisDto, FleetVehicleListDto } from '@/services/fleetService';
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

const BATTERY_POSITIONS = [
  { value: 'Main', label: 'Main' },
  { value: 'Aux', label: 'Auxiliary' },
];

const BATTERY_POSITION_VALUES = new Set(BATTERY_POSITIONS.map((p) => p.value));

export default function FleetBatteriesPage() {
  const [vehicles, setVehicles] = React.useState<FleetVehicleListDto[]>([]);
  const [vehicleId, setVehicleId] = React.useState<string>('none');
  const [items, setItems] = React.useState<FleetBatteryDto[]>([]);
  const [kpis, setKpis] = React.useState<FleetBatteryKpisDto | null>(null);
  const [loading, setLoading] = React.useState(false);

  const [open, setOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<FleetBatteryDto | null>(null);
  const [form, setForm] = React.useState({
    serialNumber: '',
    brand: '',
    spec: '',
    position: '',
    costAmount: '',
    currencyCode: '',
    status: 'Installed',
    notes: '',
  });

  const [historyOpen, setHistoryOpen] = React.useState(false);
  const [historyBattery, setHistoryBattery] = React.useState<FleetBatteryDto | null>(null);
  const [historyLoading, setHistoryLoading] = React.useState(false);
  const [historyEvents, setHistoryEvents] = React.useState<FleetBatteryEventDto[]>([]);

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
      setKpis(null);
      return;
    }
    setLoading(true);
    try {
      const [res, k] = await Promise.all([fleetService.getBatteries(vehicleId, 1, 50), fleetService.getBatteryKpis(vehicleId)]);
      setItems(res.items ?? []);
      setKpis(k ?? null);
    } catch (e) {
      console.error(e);
      setItems([]);
      setKpis(null);
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
    setForm({ serialNumber: '', brand: '', spec: '', position: '', costAmount: '', currencyCode: '', status: 'Installed', notes: '' });
    setOpen(true);
  };

  const openEdit = (b: FleetBatteryDto) => {
    setEditing(b);
    setForm({
      serialNumber: b.serialNumber,
      brand: b.brand ?? '',
      spec: b.spec ?? '',
      position: b.position ?? '',
      costAmount: '',
      currencyCode: '',
      status: b.status,
      notes: b.notes ?? '',
    });
    setOpen(true);
  };

  const openHistory = async (b: FleetBatteryDto) => {
    setHistoryBattery(b);
    setHistoryOpen(true);
    setHistoryLoading(true);
    try {
      const events = await fleetService.getBatteryEvents(b.id);
      setHistoryEvents(events ?? []);
    } catch (e) {
      console.error(e);
      setHistoryEvents([]);
    } finally {
      setHistoryLoading(false);
    }
  };

  const save = async () => {
    if (vehicleId === 'none') return;
    if (!form.serialNumber.trim()) return;
    const costAmount = form.costAmount ? Number(form.costAmount) : undefined;
    const parsedCost = Number.isFinite(costAmount) && costAmount && costAmount > 0 ? costAmount : undefined;
    const dto = {
      vehicleAssetId: vehicleId,
      serialNumber: form.serialNumber.trim(),
      brand: form.brand || undefined,
      spec: form.spec || undefined,
      position: form.position || undefined,
      installedAtUtc: undefined,
      status: form.status,
      costAmount: parsedCost,
      currencyCode: form.currencyCode || undefined,
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

  const positionSelectValue = React.useMemo(() => {
    if (!form.position) return 'none';
    return BATTERY_POSITION_VALUES.has(form.position) ? form.position : 'custom';
  }, [form.position]);

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
                          <Button variant="outline" size="sm" onClick={() => openHistory(b)}>
                            History
                          </Button>
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

          {vehicleId !== 'none' && !loading && kpis ? (
            <div className="grid grid-cols-2 gap-3 pt-3 text-sm md:grid-cols-6">
              <div className="rounded-md border p-3">
                <div className="text-muted-foreground">Installed</div>
                <div className="text-lg font-semibold">{kpis.installed}</div>
              </div>
              <div className="rounded-md border p-3">
                <div className="text-muted-foreground">In Stock</div>
                <div className="text-lg font-semibold">{kpis.inStock}</div>
              </div>
              <div className="rounded-md border p-3">
                <div className="text-muted-foreground">Removed</div>
                <div className="text-lg font-semibold">{kpis.removed}</div>
              </div>
              <div className="rounded-md border p-3">
                <div className="text-muted-foreground">Disposed</div>
                <div className="text-lg font-semibold">{kpis.disposed}</div>
              </div>
              <div className="rounded-md border p-3 md:col-span-2">
                <div className="text-muted-foreground">Avg Installed Age (days)</div>
                <div className="text-lg font-semibold">{kpis.averageInstalledAgeDays ? kpis.averageInstalledAgeDays.toFixed(0) : '—'}</div>
              </div>
            </div>
          ) : null}
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
              <Select
                value={positionSelectValue}
                onValueChange={(v) => {
                  if (v === 'none') return setForm((p) => ({ ...p, position: '' }));
                  if (v === 'custom') return setForm((p) => ({ ...p, position: p.position && !BATTERY_POSITION_VALUES.has(p.position) ? p.position : '' }));
                  setForm((p) => ({ ...p, position: v }));
                }}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select position" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">Unassigned</SelectItem>
                  {BATTERY_POSITIONS.map((p) => (
                    <SelectItem key={p.value} value={p.value}>
                      {p.label}
                    </SelectItem>
                  ))}
                  <SelectItem value="custom">Custom…</SelectItem>
                </SelectContent>
              </Select>
              {positionSelectValue === 'custom' ? (
                <Input value={form.position} onChange={(e) => setForm((p) => ({ ...p, position: e.target.value }))} placeholder="e.g. Starter" />
              ) : null}
            </div>
            <div className="space-y-2">
              <Label>Brand</Label>
              <Input value={form.brand} onChange={(e) => setForm((p) => ({ ...p, brand: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Spec</Label>
              <Input value={form.spec} onChange={(e) => setForm((p) => ({ ...p, spec: e.target.value }))} placeholder="12V 100Ah" />
            </div>

            <div className="space-y-2">
              <Label>Cost (optional)</Label>
              <Input value={form.costAmount} onChange={(e) => setForm((p) => ({ ...p, costAmount: e.target.value }))} placeholder="0.00" />
            </div>
            <div className="space-y-2">
              <Label>Currency</Label>
              <Input value={form.currencyCode} onChange={(e) => setForm((p) => ({ ...p, currencyCode: e.target.value }))} placeholder="USD" />
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

      <Dialog open={historyOpen} onOpenChange={setHistoryOpen}>
        <DialogContent className="sm:max-w-[960px]">
          <DialogHeader>
            <DialogTitle>Battery History</DialogTitle>
          </DialogHeader>

          <div className="text-sm text-muted-foreground">
            {historyBattery ? (
              <>
                {historyBattery.serialNumber} {historyBattery.position ? `— ${historyBattery.position}` : ''}
              </>
            ) : null}
          </div>

          <div className="rounded-md border">
            {historyLoading ? (
              <div className="p-4 text-sm text-muted-foreground">Loading…</div>
            ) : historyEvents.length === 0 ? (
              <div className="p-4 text-sm text-muted-foreground">No history recorded.</div>
            ) : (
              <div className="max-h-[360px] overflow-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b text-left text-muted-foreground">
                      <th className="py-2 px-3">When</th>
                      <th className="py-2 px-3">Event</th>
                      <th className="py-2 px-3">Position</th>
                      <th className="py-2 px-3">Status</th>
                      <th className="py-2 px-3">Cost</th>
                      <th className="py-2 px-3">Notes</th>
                    </tr>
                  </thead>
                  <tbody>
                    {historyEvents.map((e) => (
                      <tr key={e.id} className="border-b align-top">
                        <td className="py-2 px-3 whitespace-nowrap">{formatDate(e.eventAtUtc)}</td>
                        <td className="py-2 px-3 whitespace-nowrap">{e.eventType}</td>
                        <td className="py-2 px-3 whitespace-nowrap">
                          {(e.fromPosition || '—') + ' → ' + (e.toPosition || '—')}
                        </td>
                        <td className="py-2 px-3 whitespace-nowrap">
                          {(e.fromStatus || '—') + ' → ' + (e.toStatus || '—')}
                        </td>
                        <td className="py-2 px-3 whitespace-nowrap">
                          {e.costAmount ? `${e.costAmount.toLocaleString()}${e.currencyCode ? ` ${e.currencyCode}` : ''}` : '—'}
                        </td>
                        <td className="py-2 px-3">
                          <div className="max-w-[520px] whitespace-pre-wrap break-words">{e.notes || '—'}</div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setHistoryOpen(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

