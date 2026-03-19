'use client';

import * as React from 'react';
import { fleetService, FleetCostEntryDto, FleetTripDto } from '@/services/fleetService';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { formatFleetDateTime } from '@/lib/date-format';
import { useSearchParams } from 'next/navigation';

export default function FleetCostsPage() {
  const searchParams = useSearchParams();
  const vehicleAssetIdFilter = searchParams?.get('vehicleAssetId') ?? undefined;
  const initialTripId = searchParams?.get('fleetTripId') ?? undefined;

  const [trips, setTrips] = React.useState<FleetTripDto[]>([]);
  const [tripId, setTripId] = React.useState<string>(initialTripId ?? 'none');
  const [items, setItems] = React.useState<FleetCostEntryDto[]>([]);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  const [deleteOpen, setDeleteOpen] = React.useState(false);
  const [deleting, setDeleting] = React.useState<FleetCostEntryDto | null>(null);
  const [deletingBusy, setDeletingBusy] = React.useState(false);

  const [open, setOpen] = React.useState(false);
  const [form, setForm] = React.useState({
    costType: 'Other',
    amount: '',
    currencyCode: '',
    notes: '',
  });

  const loadTrips = React.useCallback(async () => {
    try {
      const res = await fleetService.getTrips({ page: 1, pageSize: 100, vehicleAssetId: vehicleAssetIdFilter });
      const nextTrips = res.items ?? [];
      setTrips(nextTrips);

      // If the current tripId is not valid anymore, default to the most recent trip.
      if (tripId !== 'none' && !nextTrips.some((t) => t.id === tripId)) {
        setTripId(nextTrips[0]?.id ?? 'none');
      }
    } catch (e) {
      console.error(e);
    }
  }, [tripId, vehicleAssetIdFilter]);

  const load = React.useCallback(async () => {
    if (tripId === 'none') {
      setItems([]);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const res = await fleetService.getTripCosts(tripId, 1, 100);
      setItems(res.items ?? []);
    } catch (e: any) {
      setError(e?.message || 'Failed to load costs');
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [tripId]);

  React.useEffect(() => {
    loadTrips();
  }, [loadTrips]);

  React.useEffect(() => {
    load();
  }, [load]);

  const create = async () => {
    if (tripId === 'none') return;
    const amount = Number(form.amount);
    if (!Number.isFinite(amount) || amount <= 0) return;
    try {
      await fleetService.createCost({
        fleetTripId: tripId,
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
          <p className="text-muted-foreground">Cost ledger (fuel, external repairs, manual entries) per trip</p>
        </div>

        <div className="flex flex-col gap-3 md:flex-row">
          <div className="w-full md:w-[360px]">
            <Label>Trip</Label>
            <Select value={tripId} onValueChange={setTripId}>
              <SelectTrigger>
                <SelectValue placeholder="Select trip" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="none">Select trip</SelectItem>
                {trips.map((t) => (
                  <SelectItem key={t.id} value={t.id}>
                    {t.vehicleName}
                    {t.origin || t.destination ? ` • ${t.origin ?? '—'} → ${t.destination ?? '—'}` : ''}
                    {t.plannedStartAt ? ` • ${formatFleetDateTime(t.plannedStartAt)}` : ''}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <Button onClick={() => setOpen(true)} disabled={tripId === 'none'}>
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
          {tripId === 'none' ? (
            <div className="text-sm text-muted-foreground">Select a trip to view costs.</div>
          ) : loading ? (
            <div className="text-sm text-muted-foreground">Loading…</div>
          ) : items.length === 0 ? (
            <div className="text-sm text-muted-foreground">No cost entries.</div>
          ) : (
            <div className="overflow-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b bg-muted/40 text-left text-muted-foreground">
                    <th className="py-2">Date</th>
                    <th className="py-2">Type</th>
                    <th className="py-2">Source</th>
                    <th className="py-2">Amount</th>
                    <th className="py-2">Notes</th>
                    <th className="py-2" />
                  </tr>
                </thead>
                <tbody>
                  {items.map((c, idx) => (
                    <tr key={c.id} className={idx % 2 === 1 ? 'border-b bg-muted/10 hover:bg-muted/30' : 'border-b hover:bg-muted/30'}>
                      <td className="py-2">{formatFleetDateTime(c.costDateUtc)}</td>
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
                          onClick={() => {
                            setError(null);
                            setDeleting(c);
                            setDeleteOpen(true);
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

      <ConfirmationDialog
        open={deleteOpen}
        onOpenChange={(o) => {
          setDeleteOpen(o);
          if (!o) setDeleting(null);
        }}
        title="Delete cost entry?"
        description={deleting ? `This will permanently delete the "${deleting.costType}" cost entry.` : undefined}
        confirmText="Delete"
        variant="destructive"
        isLoading={deletingBusy}
        onConfirm={async () => {
          if (!deleting) return;
          setDeletingBusy(true);
          try {
            await fleetService.deleteCost(deleting.id);
            await load();
          } catch (e: any) {
            setError(e?.message || 'Failed to delete cost entry');
            return false;
          } finally {
            setDeletingBusy(false);
          }
        }}
      />

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

