'use client';

import * as React from 'react';
import { fleetService, FleetCostEntryDto, FleetVehicleListDto } from '@/services/fleetService';
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

export default function FleetCostsPage() {
  const [vehicles, setVehicles] = React.useState<FleetVehicleListDto[]>([]);
  const [vehicleId, setVehicleId] = React.useState<string>('none');
  const [items, setItems] = React.useState<FleetCostEntryDto[]>([]);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  const [open, setOpen] = React.useState(false);
  const [form, setForm] = React.useState({
    costType: 'Other',
    amount: '',
    currencyCode: '',
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
    setError(null);
    try {
      const res = await fleetService.getCosts(vehicleId, 1, 100);
      setItems(res.items ?? []);
    } catch (e: any) {
      setError(e?.message || 'Failed to load costs');
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

  const create = async () => {
    if (vehicleId === 'none') return;
    const amount = Number(form.amount);
    if (!Number.isFinite(amount) || amount <= 0) return;
    try {
      await fleetService.createCost({
        vehicleAssetId: vehicleId,
        costType: form.costType,
        amount,
        currencyCode: form.currencyCode || undefined,
        notes: form.notes || undefined,
      });
      setOpen(false);
      setForm({ costType: 'Other', amount: '', currencyCode: '', notes: '' });
      await load();
    } catch (e: any) {
      alert(e?.message || 'Failed to create cost entry');
    }
  };

  return (
    <div className="space-y-6 p-6">
      <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Fleet Costs</h1>
          <p className="text-muted-foreground">Cost ledger (fuel, external repairs, manual entries) per vehicle</p>
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
          <Button onClick={() => setOpen(true)} disabled={vehicleId === 'none'}>
            Add Cost
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
          <CardTitle>Cost Entries</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {vehicleId === 'none' ? (
            <div className="text-sm text-muted-foreground">Select a vehicle to view costs.</div>
          ) : loading ? (
            <div className="text-sm text-muted-foreground">Loading…</div>
          ) : items.length === 0 ? (
            <div className="text-sm text-muted-foreground">No cost entries.</div>
          ) : (
            <div className="overflow-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b text-left text-muted-foreground">
                    <th className="py-2">Date</th>
                    <th className="py-2">Type</th>
                    <th className="py-2">Source</th>
                    <th className="py-2">Amount</th>
                    <th className="py-2">Notes</th>
                    <th className="py-2" />
                  </tr>
                </thead>
                <tbody>
                  {items.map((c) => (
                    <tr key={c.id} className="border-b">
                      <td className="py-2">{formatDate(c.costDateUtc)}</td>
                      <td className="py-2">{c.costType}</td>
                      <td className="py-2">
                        {c.source === 'WorkOrderCompletion' && c.workOrderId ? (
                          <a className="text-primary underline" href={`/maintenance/work-orders?id=${c.workOrderId}`} target="_blank" rel="noreferrer">
                            Work Order
                          </a>
                        ) : c.source ? (
                          c.source
                        ) : (
                          '—'
                        )}
                      </td>
                      <td className="py-2">
                        {c.amount.toLocaleString()} {c.currencyCode ?? ''}
                      </td>
                      <td className="py-2 max-w-[520px] truncate" title={c.notes ?? ''}>
                        {c.notes ?? '—'}
                      </td>
                      <td className="py-2 text-right">
                        <Button
                          variant="destructive"
                          size="sm"
                          onClick={async () => {
                            if (!confirm('Delete this cost entry?')) return;
                            await fleetService.deleteCost(c.id);
                            await load();
                          }}
                        >
                          Delete
                        </Button>
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
            <DialogTitle>Add Cost Entry</DialogTitle>
          </DialogHeader>

          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label>Cost Type</Label>
              <Select value={form.costType} onValueChange={(v) => setForm((p) => ({ ...p, costType: v }))}>
                <SelectTrigger>
                  <SelectValue placeholder="Type" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Fuel">Fuel</SelectItem>
                  <SelectItem value="ExternalRepair">External Repair</SelectItem>
                  <SelectItem value="InternalMaintenance">Internal Maintenance</SelectItem>
                  <SelectItem value="Tyre">Tyre</SelectItem>
                  <SelectItem value="Battery">Battery</SelectItem>
                  <SelectItem value="Other">Other</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Amount</Label>
              <Input value={form.amount} onChange={(e) => setForm((p) => ({ ...p, amount: e.target.value }))} placeholder="0.00" />
            </div>
            <div className="space-y-2">
              <Label>Currency</Label>
              <Input value={form.currencyCode} onChange={(e) => setForm((p) => ({ ...p, currencyCode: e.target.value }))} placeholder="USD" />
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>Notes</Label>
              <Textarea value={form.notes} onChange={(e) => setForm((p) => ({ ...p, notes: e.target.value }))} rows={4} />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button onClick={create} disabled={!form.amount || Number(form.amount) <= 0}>
              Create
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

