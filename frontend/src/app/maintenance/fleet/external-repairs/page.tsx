'use client';

import * as React from 'react';
import { fleetService, FleetExternalRepairDto, FleetVehicleListDto } from '@/services/fleetService';
import { businessPartnerService, BusinessPartnerDto } from '@/services/businessPartnerService';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { formatFleetDateTime } from '@/lib/date-format';

export default function FleetExternalRepairsPage() {
  const [vehicles, setVehicles] = React.useState<FleetVehicleListDto[]>([]);
  const [vehicleId, setVehicleId] = React.useState<string>('all');
  const [status, setStatus] = React.useState<string>('all');
  const [items, setItems] = React.useState<FleetExternalRepairDto[]>([]);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  const [vendors, setVendors] = React.useState<BusinessPartnerDto[]>([]);
  const [open, setOpen] = React.useState(false);
  const [form, setForm] = React.useState({
    title: '',
    description: '',
    vendorBusinessPartnerId: 'none',
    estimatedCost: '',
    currencyCode: '',
  });

  const [statusOpen, setStatusOpen] = React.useState(false);
  const [statusEditing, setStatusEditing] = React.useState<FleetExternalRepairDto | null>(null);
  const [statusForm, setStatusForm] = React.useState({
    status: 'Requested',
    actualCost: '',
    currencyCode: '',
  });

  const loadVehicles = React.useCallback(async () => {
    try {
      const res = await fleetService.getVehicles({ page: 1, pageSize: 100 });
      setVehicles(res.items ?? []);
    } catch (e) {
      console.error(e);
    }
  }, []);

  const loadVendors = React.useCallback(async () => {
    try {
      const res = await businessPartnerService.getPartners({ page: 1, pageSize: 100, partnerType: 'Supplier', status: 'Active' });
      setVendors(res.items ?? res.data ?? []);
    } catch (e) {
      console.error(e);
      setVendors([]);
    }
  }, []);

  const load = React.useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fleetService.getExternalRepairs({
        page: 1,
        pageSize: 50,
        vehicleAssetId: vehicleId === 'all' ? undefined : vehicleId,
        status: status === 'all' ? undefined : status,
      });
      setItems(res.items ?? []);
    } catch (e: any) {
      setError(e?.message || 'Failed to load external repairs');
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [vehicleId, status]);

  React.useEffect(() => {
    loadVehicles();
    loadVendors();
  }, [loadVehicles, loadVendors]);

  React.useEffect(() => {
    load();
  }, [load]);

  const create = async () => {
    if (!form.title.trim()) return;
    const vehicle = vehicleId === 'all' ? vehicles[0] : vehicles.find((v) => v.id === vehicleId);
    if (!vehicle) return;

    try {
      await fleetService.createExternalRepair({
        vehicleAssetId: vehicle.id,
        vendorBusinessPartnerId: form.vendorBusinessPartnerId === 'none' ? undefined : form.vendorBusinessPartnerId,
        title: form.title,
        description: form.description || undefined,
        estimatedCost: form.estimatedCost ? Number(form.estimatedCost) : undefined,
        currencyCode: form.currencyCode || undefined,
      });
      setOpen(false);
      setForm({ title: '', description: '', vendorBusinessPartnerId: 'none', estimatedCost: '', currencyCode: '' });
      await load();
    } catch (e: any) {
      alert(e?.message || 'Failed to create');
    }
  };

  const openStatusDialog = (r: FleetExternalRepairDto, nextStatus?: string) => {
    setStatusEditing(r);
    setStatusForm({
      status: nextStatus ?? r.status,
      actualCost: r.actualCost?.toString() ?? '',
      currencyCode: r.currencyCode ?? '',
    });
    setStatusOpen(true);
  };

  const saveStatus = async () => {
    if (!statusEditing) return;
    const actualCost = statusForm.actualCost ? Number(statusForm.actualCost) : undefined;
    const parsedCost = Number.isFinite(actualCost) && actualCost && actualCost > 0 ? actualCost : undefined;
    try {
      await fleetService.updateExternalRepairStatus(statusEditing.id, {
        status: statusForm.status,
        actualCost: parsedCost,
        currencyCode: statusForm.currencyCode || undefined,
      });
      setStatusOpen(false);
      setStatusEditing(null);
      await load();
    } catch (e: any) {
      alert(e?.message || 'Failed to update status');
    }
  };

  return (
    <div className="space-y-6 p-6">
      <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Fleet External Repairs</h1>
          <p className="text-muted-foreground">Track vendor repairs and external maintenance records</p>
        </div>
        <div className="flex flex-col gap-3 md:flex-row">
          <div className="w-full md:w-[300px]">
            <Label>Vehicle</Label>
            <Select value={vehicleId} onValueChange={setVehicleId}>
              <SelectTrigger>
                <SelectValue placeholder="All vehicles" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All vehicles</SelectItem>
                {vehicles.map((v) => (
                  <SelectItem key={v.id} value={v.id}>
                    {v.name} {v.licensePlate ? `(${v.licensePlate})` : ''}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="w-full md:w-[220px]">
            <Label>Status</Label>
            <Select value={status} onValueChange={setStatus}>
              <SelectTrigger>
                <SelectValue placeholder="All" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All</SelectItem>
                <SelectItem value="Requested">Requested</SelectItem>
                <SelectItem value="Quoted">Quoted</SelectItem>
                <SelectItem value="Approved">Approved</SelectItem>
                <SelectItem value="InProgress">In Progress</SelectItem>
                <SelectItem value="Completed">Completed</SelectItem>
                <SelectItem value="Invoiced">Invoiced</SelectItem>
                <SelectItem value="Cancelled">Cancelled</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <Button onClick={() => setOpen(true)} disabled={vehicleId === 'all' && vehicles.length === 0}>
            New External Repair
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
          <CardTitle>External Repairs</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {loading ? (
            <div className="text-sm text-muted-foreground">Loading…</div>
          ) : items.length === 0 ? (
            <div className="text-sm text-muted-foreground">No records.</div>
          ) : (
            <div className="overflow-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b bg-muted/40 text-left text-muted-foreground">
                    <th className="py-2">Requested</th>
                    <th className="py-2">Vehicle</th>
                    <th className="py-2">Vendor</th>
                    <th className="py-2">Title</th>
                    <th className="py-2">Status</th>
                    <th className="py-2">Est. Cost</th>
                    <th className="py-2">Actual Cost</th>
                    <th className="py-2" />
                  </tr>
                </thead>
                <tbody>
                  {items.map((r, idx) => (
                    <tr key={r.id} className={idx % 2 === 1 ? 'border-b bg-muted/10 hover:bg-muted/30' : 'border-b hover:bg-muted/30'}>
                      <td className="py-2">{formatFleetDateTime(r.requestedAtUtc)}</td>
                      <td className="py-2">{r.vehicleName}</td>
                      <td className="py-2">{r.vendorBusinessPartnerName ?? '—'}</td>
                      <td className="py-2">{r.title}</td>
                      <td className="py-2">{r.status}</td>
                      <td className="py-2">
                        {r.estimatedCost ? `${r.estimatedCost.toLocaleString()}${r.currencyCode ? ` ${r.currencyCode}` : ''}` : '—'}
                      </td>
                      <td className="py-2">
                        {r.actualCost ? `${r.actualCost.toLocaleString()}${r.currencyCode ? ` ${r.currencyCode}` : ''}` : '—'}
                      </td>
                      <td className="py-2 text-right">
                        <div className="flex justify-end gap-2">
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => {
                              const next =
                                r.status === 'Requested'
                                  ? 'Quoted'
                                  : r.status === 'Quoted'
                                    ? 'Approved'
                                    : r.status === 'Approved'
                                      ? 'InProgress'
                                      : r.status === 'InProgress'
                                        ? 'Completed'
                                        : 'Invoiced';
                              openStatusDialog(r, next);
                            }}
                          >
                            Next Status
                          </Button>
                          <Button variant="outline" size="sm" onClick={() => openStatusDialog(r)}>
                            Update
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

      <Dialog open={statusOpen} onOpenChange={setStatusOpen}>
        <DialogContent className="sm:max-w-[760px]">
          <DialogHeader>
            <DialogTitle>Update Status</DialogTitle>
          </DialogHeader>

          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label>Status</Label>
              <Select value={statusForm.status} onValueChange={(v) => setStatusForm((p) => ({ ...p, status: v }))}>
                <SelectTrigger>
                  <SelectValue placeholder="Status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Requested">Requested</SelectItem>
                  <SelectItem value="Quoted">Quoted</SelectItem>
                  <SelectItem value="Approved">Approved</SelectItem>
                  <SelectItem value="InProgress">In Progress</SelectItem>
                  <SelectItem value="Completed">Completed</SelectItem>
                  <SelectItem value="Invoiced">Invoiced</SelectItem>
                  <SelectItem value="Cancelled">Cancelled</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Actual Cost (optional)</Label>
              <Input value={statusForm.actualCost} onChange={(e) => setStatusForm((p) => ({ ...p, actualCost: e.target.value }))} placeholder="0.00" />
            </div>

            <div className="space-y-2">
              <Label>Currency</Label>
              <Input value={statusForm.currencyCode} onChange={(e) => setStatusForm((p) => ({ ...p, currencyCode: e.target.value }))} placeholder="USD" />
            </div>
            <div className="text-sm text-muted-foreground md:col-span-2">
              If you enter an actual cost, the system updates the Fleet cost ledger for this external repair (reconciling the earlier estimate).
            </div>
          </div>

          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => {
                setStatusOpen(false);
                setStatusEditing(null);
              }}
            >
              Cancel
            </Button>
            <Button onClick={saveStatus} disabled={!statusForm.status}>
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-[760px]">
          <DialogHeader>
            <DialogTitle>New External Repair</DialogTitle>
          </DialogHeader>

          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2 md:col-span-2">
              <Label>Title</Label>
              <Input value={form.title} onChange={(e) => setForm((p) => ({ ...p, title: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Vendor (optional)</Label>
              <Select value={form.vendorBusinessPartnerId} onValueChange={(v) => setForm((p) => ({ ...p, vendorBusinessPartnerId: v }))}>
                <SelectTrigger>
                  <SelectValue placeholder="Select vendor" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">No vendor</SelectItem>
                  {vendors.map((v) => (
                    <SelectItem key={v.id} value={v.id}>
                      {v.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Estimated Cost</Label>
              <Input value={form.estimatedCost} onChange={(e) => setForm((p) => ({ ...p, estimatedCost: e.target.value }))} placeholder="0.00" />
            </div>
            <div className="space-y-2">
              <Label>Currency</Label>
              <Input value={form.currencyCode} onChange={(e) => setForm((p) => ({ ...p, currencyCode: e.target.value }))} placeholder="USD" />
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>Description</Label>
              <Textarea value={form.description} onChange={(e) => setForm((p) => ({ ...p, description: e.target.value }))} rows={5} />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button onClick={create} disabled={!form.title.trim() || (vehicleId === 'all' && vehicles.length === 0)}>
              Create
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

