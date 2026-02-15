'use client';

import * as React from 'react';
import { fleetService, FleetTyreDto, FleetTyreEventDto, FleetVehicleListDto } from '@/services/fleetService';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { formatFleetDateTime } from '@/lib/date-format';

const TYRE_POSITIONS = [
  { value: 'FrontLeft', label: 'Front Left' },
  { value: 'FrontRight', label: 'Front Right' },
  { value: 'RearLeft', label: 'Rear Left' },
  { value: 'RearRight', label: 'Rear Right' },
  { value: 'Spare', label: 'Spare' },
];

const TYRE_POSITION_VALUES = new Set(TYRE_POSITIONS.map((p) => p.value));

export default function FleetTyresPage() {
  const [vehicles, setVehicles] = React.useState<FleetVehicleListDto[]>([]);
  const [vehicleId, setVehicleId] = React.useState<string>('none');
  const [items, setItems] = React.useState<FleetTyreDto[]>([]);
  const [loading, setLoading] = React.useState(false);

  const [deleteOpen, setDeleteOpen] = React.useState(false);
  const [deleting, setDeleting] = React.useState<FleetTyreDto | null>(null);
  const [deleteBusy, setDeleteBusy] = React.useState(false);
  const [deleteError, setDeleteError] = React.useState<string>('');

  const [open, setOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<FleetTyreDto | null>(null);
  const [form, setForm] = React.useState({
    serialNumber: '',
    brand: '',
    size: '',
    position: '',
    treadDepthMm: '',
    costAmount: '',
    currencyCode: '',
    status: 'Installed',
    notes: '',
  });

  const [historyOpen, setHistoryOpen] = React.useState(false);
  const [historyTyre, setHistoryTyre] = React.useState<FleetTyreDto | null>(null);
  const [historyLoading, setHistoryLoading] = React.useState(false);
  const [historyEvents, setHistoryEvents] = React.useState<FleetTyreEventDto[]>([]);

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
      const res = await fleetService.getTyres(vehicleId, 1, 50);
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
    setForm({
      serialNumber: '',
      brand: '',
      size: '',
      position: '',
      treadDepthMm: '',
      costAmount: '',
      currencyCode: '',
      status: 'Installed',
      notes: '',
    });
    setOpen(true);
  };

  const openEdit = (t: FleetTyreDto) => {
    setEditing(t);
    setForm({
      serialNumber: t.serialNumber,
      brand: t.brand ?? '',
      size: t.size ?? '',
      position: t.position ?? '',
      treadDepthMm: t.treadDepthMm?.toString() ?? '',
      costAmount: '',
      currencyCode: '',
      status: t.status,
      notes: t.notes ?? '',
    });
    setOpen(true);
  };

  const openHistory = async (t: FleetTyreDto) => {
    setHistoryTyre(t);
    setHistoryOpen(true);
    setHistoryLoading(true);
    try {
      const events = await fleetService.getTyreEvents(t.id);
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
      size: form.size || undefined,
      position: form.position || undefined,
      treadDepthMm: form.treadDepthMm ? Number(form.treadDepthMm) : undefined,
      installedAtUtc: undefined,
      status: form.status,
      costAmount: parsedCost,
      currencyCode: form.currencyCode || undefined,
      notes: form.notes || undefined,
    };
    try {
      if (editing) {
        await fleetService.updateTyre(editing.id, dto);
      } else {
        await fleetService.createTyre(dto);
      }
      setOpen(false);
      await load();
    } catch (e: any) {
      alert(e?.message || 'Failed to save tyre');
    }
  };

  const positionSelectValue = React.useMemo(() => {
    if (!form.position) return 'none';
    return TYRE_POSITION_VALUES.has(form.position) ? form.position : 'custom';
  }, [form.position]);

  return (
    <div className="space-y-6 p-6">
      <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Fleet Tyres</h1>
          <p className="text-muted-foreground">Track tyres per vehicle, including positions and tread depth</p>
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
            Add Tyre
          </Button>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Tyres</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {vehicleId === 'none' ? (
            <div className="text-sm text-muted-foreground">Select a vehicle to view tyres.</div>
          ) : loading ? (
            <div className="text-sm text-muted-foreground">Loading…</div>
          ) : items.length === 0 ? (
            <div className="text-sm text-muted-foreground">No tyres recorded.</div>
          ) : (
            <div className="overflow-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b bg-muted/40 text-left text-muted-foreground">
                    <th className="py-2">Serial</th>
                    <th className="py-2">Position</th>
                    <th className="py-2">Brand</th>
                    <th className="py-2">Size</th>
                    <th className="py-2">Tread (mm)</th>
                    <th className="py-2">Installed</th>
                    <th className="py-2">Status</th>
                    <th className="py-2" />
                  </tr>
                </thead>
                <tbody>
                  {items.map((t, idx) => (
                    <tr key={t.id} className={idx % 2 === 1 ? 'border-b bg-muted/10 hover:bg-muted/30' : 'border-b hover:bg-muted/30'}>
                      <td className="py-2">{t.serialNumber}</td>
                      <td className="py-2">{t.position ?? '—'}</td>
                      <td className="py-2">{t.brand ?? '—'}</td>
                      <td className="py-2">{t.size ?? '—'}</td>
                      <td className="py-2">{t.treadDepthMm ?? '—'}</td>
                      <td className="py-2">{formatFleetDateTime(t.installedAtUtc)}</td>
                      <td className="py-2">{t.status}</td>
                      <td className="py-2 text-right">
                        <div className="flex justify-end gap-2">
                          <Button variant="outline" size="sm" onClick={() => openHistory(t)}>
                            History
                          </Button>
                          <Button variant="outline" size="sm" onClick={() => openEdit(t)}>
                            Edit
                          </Button>
                          <Button
                            variant="destructive"
                            size="sm"
                            onClick={() => {
                              setDeleteError('');
                              setDeleting(t);
                              setDeleteOpen(true);
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

      <ConfirmationDialog
        open={deleteOpen}
        onOpenChange={(o) => {
          setDeleteOpen(o);
          if (!o) {
            setDeleting(null);
            setDeleteError('');
          }
        }}
        title="Delete tyre record?"
        description={
          <div className="space-y-2">
            <div>
              {deleting ? (
                <>
                  This will delete tyre <span className="font-medium">{deleting.serialNumber}</span>.
                </>
              ) : null}
            </div>
            {deleteError ? <div className="text-sm text-destructive">{deleteError}</div> : null}
          </div>
        }
        confirmText="Delete"
        variant="destructive"
        isLoading={deleteBusy}
        onConfirm={async () => {
          if (!deleting) return;
          setDeleteBusy(true);
          setDeleteError('');
          try {
            await fleetService.deleteTyre(deleting.id);
            await load();
          } catch (e: any) {
            setDeleteError(e?.message || 'Failed to delete tyre record');
            return false;
          } finally {
            setDeleteBusy(false);
          }
        }}
      />

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-[760px]">
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit Tyre' : 'Add Tyre'}</DialogTitle>
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
                  if (v === 'custom') return setForm((p) => ({ ...p, position: p.position && !TYRE_POSITION_VALUES.has(p.position) ? p.position : '' }));
                  setForm((p) => ({ ...p, position: v }));
                }}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select position" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">Unassigned</SelectItem>
                  {TYRE_POSITIONS.map((p) => (
                    <SelectItem key={p.value} value={p.value}>
                      {p.label}
                    </SelectItem>
                  ))}
                  <SelectItem value="custom">Custom…</SelectItem>
                </SelectContent>
              </Select>
              {positionSelectValue === 'custom' ? (
                <Input value={form.position} onChange={(e) => setForm((p) => ({ ...p, position: e.target.value }))} placeholder="e.g. RearInnerLeft" />
              ) : null}
            </div>
            <div className="space-y-2">
              <Label>Brand</Label>
              <Input value={form.brand} onChange={(e) => setForm((p) => ({ ...p, brand: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Size</Label>
              <Input value={form.size} onChange={(e) => setForm((p) => ({ ...p, size: e.target.value }))} />
            </div>

            <div className="space-y-2">
              <Label>Tread Depth (mm)</Label>
              <Input value={form.treadDepthMm} onChange={(e) => setForm((p) => ({ ...p, treadDepthMm: e.target.value }))} />
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
            <DialogTitle>Tyre History</DialogTitle>
          </DialogHeader>

          <div className="text-sm text-muted-foreground">
            {historyTyre ? (
              <>
                {historyTyre.serialNumber} {historyTyre.position ? `— ${historyTyre.position}` : ''}
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
                    <tr className="border-b bg-muted/40 text-left text-muted-foreground">
                      <th className="py-2 px-3">When</th>
                      <th className="py-2 px-3">Event</th>
                      <th className="py-2 px-3">Position</th>
                      <th className="py-2 px-3">Status</th>
                      <th className="py-2 px-3">Tread</th>
                      <th className="py-2 px-3">Cost</th>
                      <th className="py-2 px-3">Notes</th>
                    </tr>
                  </thead>
                  <tbody>
                    {historyEvents.map((e, idx) => (
                      <tr key={e.id} className={idx % 2 === 1 ? 'border-b bg-muted/10 align-top hover:bg-muted/30' : 'border-b align-top hover:bg-muted/30'}>
                        <td className="py-2 px-3 whitespace-nowrap">{formatFleetDateTime(e.eventAtUtc)}</td>
                        <td className="py-2 px-3 whitespace-nowrap">{e.eventType}</td>
                        <td className="py-2 px-3 whitespace-nowrap">
                          {(e.fromPosition || '—') + ' → ' + (e.toPosition || '—')}
                        </td>
                        <td className="py-2 px-3 whitespace-nowrap">
                          {(e.fromStatus || '—') + ' → ' + (e.toStatus || '—')}
                        </td>
                        <td className="py-2 px-3 whitespace-nowrap">{e.treadDepthMm ?? '—'}</td>
                        <td className="py-2 px-3 whitespace-nowrap">
                          {e.costAmount ? `${e.costAmount.toLocaleString()}${e.currencyCode ? ` ${e.currencyCode}` : ''}` : '—'}
                        </td>
                        <td className="py-2 px-3">
                          <div className="max-w-[420px] whitespace-pre-wrap break-words">{e.notes || '—'}</div>
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

