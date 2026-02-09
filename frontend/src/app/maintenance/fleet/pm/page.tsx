'use client';

import * as React from 'react';
import { fleetService, FleetVehicleListDto } from '@/services/fleetService';
import { maintenanceScheduleService, MaintenanceSchedule } from '@/services/maintenanceScheduleService';
import { maintenanceApiService } from '@/services/maintenanceApiService';
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
  return d.toLocaleDateString();
}

export default function FleetPmPlansPage() {
  const [vehicles, setVehicles] = React.useState<FleetVehicleListDto[]>([]);
  const [vehicleId, setVehicleId] = React.useState<string>('none');
  const [items, setItems] = React.useState<MaintenanceSchedule[]>([]);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  const [open, setOpen] = React.useState(false);
  const [maintenanceTypes, setMaintenanceTypes] = React.useState<any[]>([]);

  const [form, setForm] = React.useState({
    name: '',
    code: '',
    description: '',
    maintenanceTypeId: '',
    priority: 'Medium',
    frequency: 'Usage',
    nextDueDate: new Date().toISOString().slice(0, 10),
    mileageTrigger: '',
    operatingHoursTrigger: '',
    estimatedHours: '1',
    estimatedCost: '0',
    instructions: '',
  });

  const loadVehicles = React.useCallback(async () => {
    try {
      const res = await fleetService.getVehicles({ page: 1, pageSize: 100 });
      setVehicles(res.items ?? []);
    } catch (e) {
      console.error(e);
    }
  }, []);

  const loadSchedules = React.useCallback(async () => {
    if (vehicleId === 'none') {
      setItems([]);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const res = await maintenanceScheduleService.getSchedules({ page: 1, pageSize: 50, assetId: vehicleId } as any);
      setItems(res.items ?? res.data ?? []);
    } catch (e: any) {
      setError(e?.message || 'Failed to load schedules');
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [vehicleId]);

  React.useEffect(() => {
    loadVehicles();
  }, [loadVehicles]);

  React.useEffect(() => {
    loadSchedules();
  }, [loadSchedules]);

  const openCreate = async () => {
    setOpen(true);
    try {
      const mts = await maintenanceApiService.getMaintenanceTypes();
      setMaintenanceTypes(mts ?? []);
    } catch (e) {
      console.error(e);
    }
  };

  const create = async () => {
    if (vehicleId === 'none') return;
    if (!form.name.trim() || !form.code.trim() || !form.maintenanceTypeId) return;

    try {
      await maintenanceScheduleService.createSchedule({
        name: form.name,
        code: form.code,
        description: form.description || undefined,
        assetId: vehicleId,
        maintenanceTypeId: form.maintenanceTypeId,
        maintenanceType: 'Preventive',
        priority: form.priority,
        frequency: form.frequency,
        nextDueDate: new Date(form.nextDueDate).toISOString(),
        estimatedHours: Number(form.estimatedHours || '1'),
        estimatedCost: Number(form.estimatedCost || '0'),
        primaryTriggerType: form.frequency === 'Usage' ? 'Usage' : 'Time',
        mileageTrigger: form.mileageTrigger ? Number(form.mileageTrigger) : undefined,
        operatingHoursTrigger: form.operatingHoursTrigger ? Number(form.operatingHoursTrigger) : undefined,
        instructions: form.instructions || undefined,
        autoGenerateWorkOrders: true,
      } as any);
      setOpen(false);
      setForm({
        name: '',
        code: '',
        description: '',
        maintenanceTypeId: '',
        priority: 'Medium',
        frequency: 'Usage',
        nextDueDate: new Date().toISOString().slice(0, 10),
        mileageTrigger: '',
        operatingHoursTrigger: '',
        estimatedHours: '1',
        estimatedCost: '0',
        instructions: '',
      });
      await loadSchedules();
    } catch (e: any) {
      alert(e?.message || 'Failed to create schedule');
    }
  };

  return (
    <div className="space-y-6 p-6">
      <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Fleet PM Plans</h1>
          <p className="text-muted-foreground">Usage/time-based preventive maintenance schedules per vehicle</p>
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
            New PM Plan
          </Button>
        </div>
      </div>

      {error && (
        <Card className="border-destructive">
          <CardHeader>
            <CardTitle className="text-destructive">Error</CardTitle>
          </CardHeader>
          <CardContent>{error}</CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Schedules</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {vehicleId === 'none' ? (
            <div className="text-sm text-muted-foreground">Select a vehicle to view schedules.</div>
          ) : loading ? (
            <div className="text-sm text-muted-foreground">Loading…</div>
          ) : items.length === 0 ? (
            <div className="text-sm text-muted-foreground">No schedules.</div>
          ) : (
            <div className="overflow-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b text-left text-muted-foreground">
                    <th className="py-2">Code</th>
                    <th className="py-2">Name</th>
                    <th className="py-2">Frequency</th>
                    <th className="py-2">Next Due</th>
                    <th className="py-2">Active</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((s) => (
                    <tr key={s.id} className="border-b">
                      <td className="py-2">{s.code}</td>
                      <td className="py-2">{s.name}</td>
                      <td className="py-2">
                        {s.primaryTriggerType === 'Usage' ? `Usage (km:${s.mileageTrigger ?? '—'}, hrs:${s.operatingHoursTrigger ?? '—'})` : s.frequency}
                      </td>
                      <td className="py-2">{formatDate(s.nextDueDate)}</td>
                      <td className="py-2">{s.isActive ? 'Yes' : 'No'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-[820px]">
          <DialogHeader>
            <DialogTitle>New PM Plan</DialogTitle>
          </DialogHeader>

          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label>Name</Label>
              <Input value={form.name} onChange={(e) => setForm((p) => ({ ...p, name: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Code</Label>
              <Input value={form.code} onChange={(e) => setForm((p) => ({ ...p, code: e.target.value }))} />
            </div>

            <div className="space-y-2 md:col-span-2">
              <Label>Description</Label>
              <Input value={form.description} onChange={(e) => setForm((p) => ({ ...p, description: e.target.value }))} />
            </div>

            <div className="space-y-2">
              <Label>Maintenance Type</Label>
              <Select value={form.maintenanceTypeId} onValueChange={(v) => setForm((p) => ({ ...p, maintenanceTypeId: v }))}>
                <SelectTrigger>
                  <SelectValue placeholder="Select maintenance type" />
                </SelectTrigger>
                <SelectContent>
                  {maintenanceTypes.map((t) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Priority</Label>
              <Select value={form.priority} onValueChange={(v) => setForm((p) => ({ ...p, priority: v }))}>
                <SelectTrigger>
                  <SelectValue placeholder="Priority" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Low">Low</SelectItem>
                  <SelectItem value="Medium">Medium</SelectItem>
                  <SelectItem value="High">High</SelectItem>
                  <SelectItem value="Critical">Critical</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Trigger</Label>
              <Select value={form.frequency} onValueChange={(v) => setForm((p) => ({ ...p, frequency: v }))}>
                <SelectTrigger>
                  <SelectValue placeholder="Trigger type" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Usage">Usage</SelectItem>
                  <SelectItem value="Monthly">Monthly</SelectItem>
                  <SelectItem value="Quarterly">Quarterly</SelectItem>
                  <SelectItem value="Yearly">Yearly</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Next Due Date</Label>
              <Input type="date" value={form.nextDueDate} onChange={(e) => setForm((p) => ({ ...p, nextDueDate: e.target.value }))} />
            </div>

            {form.frequency === 'Usage' && (
              <>
                <div className="space-y-2">
                  <Label>Mileage Trigger (km)</Label>
                  <Input value={form.mileageTrigger} onChange={(e) => setForm((p) => ({ ...p, mileageTrigger: e.target.value }))} placeholder="e.g. 5000" />
                </div>
                <div className="space-y-2">
                  <Label>Hour Trigger (hrs)</Label>
                  <Input
                    value={form.operatingHoursTrigger}
                    onChange={(e) => setForm((p) => ({ ...p, operatingHoursTrigger: e.target.value }))}
                    placeholder="e.g. 250"
                  />
                </div>
              </>
            )}

            <div className="space-y-2">
              <Label>Estimated Hours</Label>
              <Input value={form.estimatedHours} onChange={(e) => setForm((p) => ({ ...p, estimatedHours: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Estimated Cost</Label>
              <Input value={form.estimatedCost} onChange={(e) => setForm((p) => ({ ...p, estimatedCost: e.target.value }))} />
            </div>

            <div className="space-y-2 md:col-span-2">
              <Label>Instructions</Label>
              <Textarea value={form.instructions} onChange={(e) => setForm((p) => ({ ...p, instructions: e.target.value }))} rows={4} />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button onClick={create} disabled={!form.name.trim() || !form.code.trim() || !form.maintenanceTypeId}>
              Create
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

