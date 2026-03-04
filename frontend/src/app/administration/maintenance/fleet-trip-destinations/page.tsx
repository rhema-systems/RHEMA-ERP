'use client';

import React from 'react';
import { Plus, Edit, Trash2, MapPin } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useToast } from '@/hooks/use-toast';

import fleetService, { CreateFleetTripDestinationDto, FleetTripDestinationDto } from '@/services/fleetService';

export default function FleetTripDestinationsAdminPage() {
  const { toast } = useToast();

  const [loading, setLoading] = React.useState(true);
  const [items, setItems] = React.useState<FleetTripDestinationDto[]>([]);

  const [dialogOpen, setDialogOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<FleetTripDestinationDto | null>(null);

  const [form, setForm] = React.useState<CreateFleetTripDestinationDto>({
    name: '',
    origin: '',
    destination: '',
    expectedHours: null,
    expectedMileage: null,
    isActive: true,
  });

  const load = React.useCallback(async () => {
    setLoading(true);
    try {
      const list = await fleetService.getTripDestinations({ activeOnly: false });
      setItems(list || []);
    } catch (e: any) {
      toast({ title: 'Failed to load trip destinations', description: e?.message || String(e), variant: 'destructive' });
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [toast]);

  React.useEffect(() => {
    load();
  }, [load]);

  const openCreate = () => {
    setEditing(null);
    setForm({
      name: '',
      origin: '',
      destination: '',
      expectedHours: null,
      expectedMileage: null,
      isActive: true,
    });
    setDialogOpen(true);
  };

  const openEdit = (item: FleetTripDestinationDto) => {
    setEditing(item);
    setForm({
      name: item.name,
      origin: item.origin ?? '',
      destination: item.destination ?? '',
      expectedHours: item.expectedHours ?? null,
      expectedMileage: item.expectedMileage ?? null,
      isActive: item.isActive !== false,
    });
    setDialogOpen(true);
  };

  const save = async () => {
    try {
      if (!form.name.trim()) throw new Error('Name is required');

      const payload: CreateFleetTripDestinationDto = {
        ...form,
        name: form.name.trim(),
        origin: form.origin?.trim() || null,
        destination: form.destination?.trim() || null,
        expectedHours: form.expectedHours == null ? null : Number(form.expectedHours),
        expectedMileage: form.expectedMileage == null ? null : Number(form.expectedMileage),
        isActive: !!form.isActive,
      };

      if (editing) {
        await fleetService.updateTripDestination(editing.id, payload);
        toast({ title: 'Trip destination updated' });
      } else {
        await fleetService.createTripDestination(payload);
        toast({ title: 'Trip destination created' });
      }

      setDialogOpen(false);
      await load();
    } catch (e: any) {
      toast({ title: 'Save failed', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  const remove = async (id: string) => {
    try {
      await fleetService.deleteTripDestination(id);
      toast({ title: 'Trip destination deleted' });
      await load();
    } catch (e: any) {
      toast({ title: 'Delete failed', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  return (
    <div className="container mx-auto py-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
            <MapPin className="h-8 w-8" />
            Fleet Trip Destinations
          </h1>
          <p className="text-muted-foreground mt-2">Maintain predefined trip destinations (routes) for dispatch selection and enforcement.</p>
        </div>
        <Button onClick={openCreate}>
          <Plus className="mr-2 h-4 w-4" />
          New Destination
        </Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Destinations</CardTitle>
          <CardDescription>These appear in the Fleet Trips dispatch screen.</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow className="bg-muted/40">
                  <TableHead>Name</TableHead>
                  <TableHead>Origin</TableHead>
                  <TableHead>Destination</TableHead>
                  <TableHead className="text-right">Expected Hours</TableHead>
                  <TableHead className="text-right">Expected Mileage (km)</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {loading ? (
                  <TableRow>
                    <TableCell colSpan={7} className="py-8 text-center text-muted-foreground">
                      Loading...
                    </TableCell>
                  </TableRow>
                ) : items.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7} className="py-8 text-center text-muted-foreground">
                      No trip destinations
                    </TableCell>
                  </TableRow>
                ) : (
                  items.map((d, idx) => (
                    <TableRow key={d.id} className={idx % 2 === 1 ? 'bg-muted/10 hover:bg-muted/30' : 'hover:bg-muted/30'}>
                      <TableCell className="font-medium">{d.name}</TableCell>
                      <TableCell>{d.origin || '-'}</TableCell>
                      <TableCell>{d.destination || '-'}</TableCell>
                      <TableCell className="text-right">{d.expectedHours ?? '-'}</TableCell>
                      <TableCell className="text-right">{d.expectedMileage ?? '-'}</TableCell>
                      <TableCell>{d.isActive ? 'Active' : 'Inactive'}</TableCell>
                      <TableCell className="text-right">
                        <div className="flex items-center justify-end gap-2">
                          <Button size="sm" variant="outline" onClick={() => openEdit(d)}>
                            <Edit className="h-4 w-4" />
                          </Button>
                          <Button size="sm" variant="destructive" onClick={() => remove(d.id)} title="Delete">
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

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-xl">
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit Destination' : 'New Destination'}</DialogTitle>
            <DialogDescription>Configure a predefined route for fleet trip dispatch.</DialogDescription>
          </DialogHeader>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="space-y-2 md:col-span-2">
              <Label>Name</Label>
              <Input value={form.name} onChange={(e) => setForm((p) => ({ ...p, name: e.target.value }))} placeholder="e.g. City Center Delivery Route" />
            </div>
            <div className="space-y-2">
              <Label>Origin</Label>
              <Input value={form.origin || ''} onChange={(e) => setForm((p) => ({ ...p, origin: e.target.value }))} placeholder="e.g. Depot" />
            </div>
            <div className="space-y-2">
              <Label>Destination</Label>
              <Input value={form.destination || ''} onChange={(e) => setForm((p) => ({ ...p, destination: e.target.value }))} placeholder="e.g. Site A" />
            </div>
            <div className="space-y-2">
              <Label>Expected Hours</Label>
              <Input
                type="number"
                value={form.expectedHours ?? ''}
                onChange={(e) => setForm((p) => ({ ...p, expectedHours: e.target.value === '' ? null : Number(e.target.value) }))}
              />
            </div>
            <div className="space-y-2">
              <Label>Expected Mileage (km)</Label>
              <Input
                type="number"
                value={form.expectedMileage ?? ''}
                onChange={(e) => setForm((p) => ({ ...p, expectedMileage: e.target.value === '' ? null : Number(e.target.value) }))}
              />
            </div>
            <div className="flex items-center justify-between space-x-4 md:col-span-2">
              <div className="flex-1 space-y-1">
                <Label>Active</Label>
                <p className="text-sm text-muted-foreground">Inactive destinations are hidden from dispatch selection.</p>
              </div>
              <Switch checked={!!form.isActive} onCheckedChange={(checked) => setForm((p) => ({ ...p, isActive: checked }))} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={save}>Save</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

