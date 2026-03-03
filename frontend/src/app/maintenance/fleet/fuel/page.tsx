'use client';

import React from 'react';
import { Edit, Plus, Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';

import fleetService, { CreateFleetFuelTransactionDto, FleetFuelTransactionDto, FleetVehicleListDto, UpdateFleetFuelTransactionDto } from '@/services/fleetService';
import { formatFleetDateTime } from '@/lib/date-format';

function toDatetimeLocal(value: Date) {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}T${pad(value.getHours())}:${pad(value.getMinutes())}`;
}

export default function FleetFuelPage() {
  const { toast } = useToast();

  const [vehicles, setVehicles] = React.useState<FleetVehicleListDto[]>([]);
  const [vehicleId, setVehicleId] = React.useState<string>('');

  const [loading, setLoading] = React.useState(false);
  const [items, setItems] = React.useState<FleetFuelTransactionDto[]>([]);

  const [createOpen, setCreateOpen] = React.useState(false);
  const [editOpen, setEditOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<FleetFuelTransactionDto | null>(null);

  const [deleteOpen, setDeleteOpen] = React.useState(false);
  const [deleting, setDeleting] = React.useState<FleetFuelTransactionDto | null>(null);

  const [form, setForm] = React.useState<CreateFleetFuelTransactionDto>({
    vehicleAssetId: '',
    fleetTripId: null,
    fuelledAt: new Date().toISOString(),
    quantity: 0,
    unit: 'L',
    unitCost: null,
    mileageAtFuel: null,
    operatingHoursAtFuel: null,
    vendorName: '',
    receiptReference: '',
    notes: '',
  });

  const loadVehicles = React.useCallback(async () => {
    try {
      const res = await fleetService.getVehicles({ page: 1, pageSize: 200 });
      setVehicles(res.items || []);
    } catch (e: any) {
      toast({ title: 'Failed to load vehicles', description: e?.message || String(e), variant: 'destructive' });
    }
  }, [toast]);

  const loadFuel = React.useCallback(async () => {
    if (!vehicleId) return;
    setLoading(true);
    try {
      const res = await fleetService.getFuel(vehicleId, 1, 200);
      setItems(res.items || []);
    } catch (e: any) {
      toast({ title: 'Failed to load fuel transactions', description: e?.message || String(e), variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  }, [vehicleId, toast]);

  React.useEffect(() => {
    loadVehicles();
     
  }, []);

  React.useEffect(() => {
    loadFuel();
  }, [loadFuel]);

  const openCreate = () => {
    if (!vehicleId) {
      toast({ title: 'Select a vehicle first', variant: 'destructive' });
      return;
    }

    setForm({
      vehicleAssetId: vehicleId,
      fleetTripId: null,
      fuelledAt: new Date().toISOString(),
      quantity: 0,
      unit: 'L',
      unitCost: null,
      mileageAtFuel: null,
      operatingHoursAtFuel: null,
      vendorName: '',
      receiptReference: '',
      notes: '',
    });
    setCreateOpen(true);
  };

  const openEdit = (item: FleetFuelTransactionDto) => {
    setEditing(item);
    setForm({
      vehicleAssetId: item.vehicleAssetId,
      fleetTripId: item.fleetTripId ?? null,
      fuelledAt: item.fuelledAt,
      quantity: item.quantity,
      unit: item.unit || 'L',
      unitCost: item.unitCost ?? null,
      mileageAtFuel: item.mileageAtFuel ?? null,
      operatingHoursAtFuel: item.operatingHoursAtFuel ?? null,
      vendorName: item.vendorName ?? '',
      receiptReference: item.receiptReference ?? '',
      notes: item.notes ?? '',
    });
    setEditOpen(true);
  };

  const save = async (mode: 'create' | 'edit') => {
    try {
      if (!form.vehicleAssetId) throw new Error('Vehicle is required');
      if (!form.fuelledAt) throw new Error('Fuelled at is required');
      if (!form.quantity || form.quantity <= 0) throw new Error('Quantity must be greater than 0');

      const dto: UpdateFleetFuelTransactionDto = {
        ...form,
        fuelledAt: new Date(form.fuelledAt).toISOString(),
        unit: (form.unit || 'L').trim(),
        vendorName: form.vendorName?.trim() || null,
        receiptReference: form.receiptReference?.trim() || null,
        notes: form.notes?.trim() || null,
      };

      if (mode === 'create') {
        await fleetService.createFuel(dto);
        toast({ title: 'Fuel transaction created' });
        setCreateOpen(false);
      } else {
        if (!editing) throw new Error('No record selected');
        await fleetService.updateFuel(editing.id, dto);
        toast({ title: 'Fuel transaction updated' });
        setEditOpen(false);
        setEditing(null);
      }

      await loadFuel();
    } catch (e: any) {
      toast({ title: 'Save failed', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  const remove = async (id: string) => {
    try {
      await fleetService.deleteFuel(id);
      toast({ title: 'Deleted' });
      await loadFuel();
    } catch (e: any) {
      toast({ title: 'Delete failed', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Fleet Fuel</h1>
          <p className="text-muted-foreground">Record fuel transactions per vehicle</p>
        </div>

        <Button onClick={openCreate} disabled={!vehicleId}>
          <Plus className="mr-2 h-4 w-4" />
          Add Fuel
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
                <TableRow className="bg-muted/40">
                  <TableHead>Fuelled At</TableHead>
                  <TableHead>Qty</TableHead>
                  <TableHead>Unit Cost</TableHead>
                  <TableHead>Total</TableHead>
                  <TableHead>Vendor</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {!vehicleId ? (
                  <TableRow>
                    <TableCell colSpan={6} className="py-8 text-center text-muted-foreground">
                      Select a vehicle to view fuel transactions
                    </TableCell>
                  </TableRow>
                ) : loading ? (
                  <TableRow>
                    <TableCell colSpan={6} className="py-8 text-center text-muted-foreground">
                      Loading...
                    </TableCell>
                  </TableRow>
                ) : items.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6} className="py-8 text-center text-muted-foreground">
                      No fuel transactions
                    </TableCell>
                  </TableRow>
                ) : (
                  items.map((i, idx) => (
                    <TableRow key={i.id} className={idx % 2 === 1 ? 'bg-muted/10 hover:bg-muted/30' : 'hover:bg-muted/30'}>
                      <TableCell>{formatFleetDateTime(i.fuelledAt)}</TableCell>
                      <TableCell>
                        {i.quantity} {i.unit}
                      </TableCell>
                      <TableCell>{i.unitCost ?? '-'}</TableCell>
                      <TableCell>{i.totalCost ?? '-'}</TableCell>
                      <TableCell>{i.vendorName || '-'}</TableCell>
                      <TableCell className="text-right">
                        <div className="flex justify-end gap-1">
                          <Button variant="ghost" size="sm" onClick={() => openEdit(i)} title="Edit">
                            <Edit className="h-4 w-4" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => {
                              setDeleting(i);
                              setDeleteOpen(true);
                            }}
                            title="Delete"
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </div>
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
        title="Delete fuel transaction?"
        description={deleting ? `${formatFleetDateTime(deleting.fuelledAt)} • ${deleting.quantity} ${deleting.unit}` : undefined}
        confirmText="Delete"
        variant="destructive"
        onConfirm={async () => {
          if (!deleting) return;
          await remove(deleting.id);
        }}
      />

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>Add Fuel Transaction</DialogTitle>
            <DialogDescription>Record a fuel event for the selected vehicle.</DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="space-y-2 md:col-span-2">
              <Label>Fuelled At</Label>
              <Input
                type="datetime-local"
                value={toDatetimeLocal(new Date(form.fuelledAt))}
                onChange={(e) => setForm((p) => ({ ...p, fuelledAt: new Date(e.target.value).toISOString() }))}
              />
            </div>

            <div className="space-y-2">
              <Label>Quantity</Label>
              <Input type="number" value={form.quantity} onChange={(e) => setForm((p) => ({ ...p, quantity: Number(e.target.value) }))} />
            </div>
            <div className="space-y-2">
              <Label>Unit</Label>
              <Select value={form.unit || 'L'} onValueChange={(v) => setForm((p) => ({ ...p, unit: v }))}>
                <SelectTrigger>
                  <SelectValue placeholder="Unit" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="L">Liters (L)</SelectItem>
                  <SelectItem value="Gal">Gallons (Gal)</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Unit Cost</Label>
              <Input
                type="number"
                value={form.unitCost ?? ''}
                onChange={(e) => setForm((p) => ({ ...p, unitCost: e.target.value === '' ? null : Number(e.target.value) }))}
              />
            </div>

            <div className="space-y-2">
              <Label>Vendor</Label>
              <Input value={form.vendorName || ''} onChange={(e) => setForm((p) => ({ ...p, vendorName: e.target.value }))} />
            </div>

            <div className="space-y-2">
              <Label>Mileage at fuel (km)</Label>
              <Input
                type="number"
                value={form.mileageAtFuel ?? ''}
                onChange={(e) => setForm((p) => ({ ...p, mileageAtFuel: e.target.value === '' ? null : Number(e.target.value) }))}
              />
            </div>
            <div className="space-y-2">
              <Label>Operating hours at fuel</Label>
              <Input
                type="number"
                value={form.operatingHoursAtFuel ?? ''}
                onChange={(e) => setForm((p) => ({ ...p, operatingHoursAtFuel: e.target.value === '' ? null : Number(e.target.value) }))}
              />
            </div>

            <div className="space-y-2 md:col-span-2">
              <Label>Receipt Reference</Label>
              <Input value={form.receiptReference || ''} onChange={(e) => setForm((p) => ({ ...p, receiptReference: e.target.value }))} />
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

      <Dialog
        open={editOpen}
        onOpenChange={(o) => {
          setEditOpen(o);
          if (!o) setEditing(null);
        }}
      >
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>Edit Fuel Transaction</DialogTitle>
            <DialogDescription>Update the selected fuel transaction.</DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="space-y-2 md:col-span-2">
              <Label>Fuelled At</Label>
              <Input
                type="datetime-local"
                value={toDatetimeLocal(new Date(form.fuelledAt))}
                onChange={(e) => setForm((p) => ({ ...p, fuelledAt: new Date(e.target.value).toISOString() }))}
              />
            </div>

            <div className="space-y-2">
              <Label>Quantity</Label>
              <Input type="number" value={form.quantity} onChange={(e) => setForm((p) => ({ ...p, quantity: Number(e.target.value) }))} />
            </div>
            <div className="space-y-2">
              <Label>Unit</Label>
              <Select value={form.unit || 'L'} onValueChange={(v) => setForm((p) => ({ ...p, unit: v }))}>
                <SelectTrigger>
                  <SelectValue placeholder="Unit" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="L">Liters (L)</SelectItem>
                  <SelectItem value="Gal">Gallons (Gal)</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Unit Cost</Label>
              <Input
                type="number"
                value={form.unitCost ?? ''}
                onChange={(e) => setForm((p) => ({ ...p, unitCost: e.target.value === '' ? null : Number(e.target.value) }))}
              />
            </div>

            <div className="space-y-2">
              <Label>Vendor</Label>
              <Input value={form.vendorName || ''} onChange={(e) => setForm((p) => ({ ...p, vendorName: e.target.value }))} />
            </div>

            <div className="space-y-2">
              <Label>Mileage at fuel (km)</Label>
              <Input
                type="number"
                value={form.mileageAtFuel ?? ''}
                onChange={(e) => setForm((p) => ({ ...p, mileageAtFuel: e.target.value === '' ? null : Number(e.target.value) }))}
              />
            </div>
            <div className="space-y-2">
              <Label>Operating hours at fuel</Label>
              <Input
                type="number"
                value={form.operatingHoursAtFuel ?? ''}
                onChange={(e) => setForm((p) => ({ ...p, operatingHoursAtFuel: e.target.value === '' ? null : Number(e.target.value) }))}
              />
            </div>

            <div className="space-y-2 md:col-span-2">
              <Label>Receipt Reference</Label>
              <Input value={form.receiptReference || ''} onChange={(e) => setForm((p) => ({ ...p, receiptReference: e.target.value }))} />
            </div>

            <div className="space-y-2 md:col-span-2">
              <Label>Notes</Label>
              <Textarea value={form.notes || ''} onChange={(e) => setForm((p) => ({ ...p, notes: e.target.value }))} />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setEditOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => save('edit')}>Save</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

