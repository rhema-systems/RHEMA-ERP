'use client';

import React from 'react';
import { Plus, Search, Edit } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useToast } from '@/hooks/use-toast';

import fleetService, { CreateFleetVehicleDto, FleetVehicleListDto, PagedResult, UpdateFleetVehicleDto } from '@/services/fleetService';

type AssetCategoryDto = {
  id: string;
  name: string;
  assetType?: string | null;
  isActive?: boolean;
};

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

function getAuthHeaders(): HeadersInit {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

export default function FleetVehiclesPage() {
  const { toast } = useToast();

  const [loading, setLoading] = React.useState(true);
  const [categories, setCategories] = React.useState<AssetCategoryDto[]>([]);

  const [page, setPage] = React.useState(1);
  const [pageSize] = React.useState(25);
  const [searchTerm, setSearchTerm] = React.useState('');
  const [categoryId, setCategoryId] = React.useState<string>('all');

  const [result, setResult] = React.useState<PagedResult<FleetVehicleListDto>>({
    items: [],
    totalCount: 0,
    page: 1,
    pageSize,
  });

  const [createOpen, setCreateOpen] = React.useState(false);
  const [editOpen, setEditOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<FleetVehicleListDto | null>(null);

  const [createForm, setCreateForm] = React.useState<CreateFleetVehicleDto>({
    name: '',
    assetCategoryId: '',
    licensePlate: '',
    vin: '',
    manufacturer: '',
    model: '',
    serialNumber: '',
    location: '',
    mileage: null,
    operatingHours: null,
  });

  const [editForm, setEditForm] = React.useState<UpdateFleetVehicleDto>({
    name: '',
    assetCategoryId: '',
    status: 'Active',
    licensePlate: '',
    vin: '',
    manufacturer: '',
    model: '',
    serialNumber: '',
    location: '',
    mileage: null,
    operatingHours: null,
  });

  const loadCategories = React.useCallback(async () => {
    const response = await fetch(`${API_BASE_URL}/maintenance/asset-categories`, { headers: getAuthHeaders() });
    if (!response.ok) throw new Error(await response.text());
    const data: AssetCategoryDto[] = await response.json();

    const vehicleCats = (data || [])
      .filter((c) => (c.assetType || '').toLowerCase() === 'vehicle')
      .filter((c) => c.isActive !== false)
      .sort((a, b) => a.name.localeCompare(b.name));

    setCategories(vehicleCats);
  }, []);

  const loadVehicles = React.useCallback(async () => {
    setLoading(true);
    try {
      const res = await fleetService.getVehicles({
        page,
        pageSize,
        searchTerm: searchTerm.trim() || undefined,
        categoryId: categoryId !== 'all' ? categoryId : undefined,
      });
      setResult(res);
    } catch (e: any) {
      toast({ title: 'Failed to load vehicles', description: e?.message || String(e), variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, searchTerm, categoryId, toast]);

  React.useEffect(() => {
    (async () => {
      try {
        await loadCategories();
      } catch (e: any) {
        toast({ title: 'Failed to load categories', description: e?.message || String(e), variant: 'destructive' });
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  React.useEffect(() => {
    loadVehicles();
  }, [loadVehicles]);

  const openEdit = (v: FleetVehicleListDto) => {
    setEditing(v);
    setEditForm({
      name: v.name,
      assetCategoryId: v.assetCategoryId || '',
      status: v.status || 'Active',
      licensePlate: v.licensePlate || '',
      vin: v.vin || '',
      manufacturer: v.manufacturer || '',
      model: v.model || '',
      serialNumber: '',
      location: '',
      mileage: v.mileage ?? null,
      operatingHours: v.operatingHours ?? null,
    });
    setEditOpen(true);
  };

  const onCreate = async () => {
    try {
      if (!createForm.name.trim()) throw new Error('Name is required');
      if (!createForm.assetCategoryId) throw new Error('Category is required');

      await fleetService.createVehicle({
        ...createForm,
        name: createForm.name.trim(),
      });

      toast({ title: 'Vehicle created' });
      setCreateOpen(false);
      setCreateForm((p) => ({ ...p, name: '', licensePlate: '', vin: '' }));
      await loadVehicles();
    } catch (e: any) {
      toast({ title: 'Failed to create vehicle', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  const onUpdate = async () => {
    if (!editing) return;
    try {
      if (!editForm.name.trim()) throw new Error('Name is required');
      if (!editForm.assetCategoryId) throw new Error('Category is required');

      await fleetService.updateVehicle(editing.id, {
        ...editForm,
        name: editForm.name.trim(),
      });

      toast({ title: 'Vehicle updated' });
      setEditOpen(false);
      setEditing(null);
      await loadVehicles();
    } catch (e: any) {
      toast({ title: 'Failed to update vehicle', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  const totalPages = Math.max(1, Math.ceil((result.totalCount || 0) / pageSize));

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Fleet Vehicles</h1>
          <p className="text-muted-foreground">Vehicles are stored as Maintenance Assets (Vehicle type)</p>
        </div>

        <Dialog open={createOpen} onOpenChange={setCreateOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              New Vehicle
            </Button>
          </DialogTrigger>
          <DialogContent className="max-w-3xl">
            <DialogHeader>
              <DialogTitle>Create Vehicle</DialogTitle>
              <DialogDescription>Create a new fleet vehicle record.</DialogDescription>
            </DialogHeader>

            <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Name</Label>
                <Input value={createForm.name} onChange={(e) => setCreateForm((p) => ({ ...p, name: e.target.value }))} />
              </div>
              <div className="space-y-2">
                <Label>Category</Label>
                <Select value={createForm.assetCategoryId || undefined} onValueChange={(v) => setCreateForm((p) => ({ ...p, assetCategoryId: v }))}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select category" />
                  </SelectTrigger>
                  <SelectContent>
                    {categories.map((c) => (
                      <SelectItem key={c.id} value={c.id}>
                        {c.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <Label>License Plate</Label>
                <Input value={createForm.licensePlate || ''} onChange={(e) => setCreateForm((p) => ({ ...p, licensePlate: e.target.value }))} />
              </div>
              <div className="space-y-2">
                <Label>VIN</Label>
                <Input value={createForm.vin || ''} onChange={(e) => setCreateForm((p) => ({ ...p, vin: e.target.value }))} />
              </div>

              <div className="space-y-2">
                <Label>Manufacturer</Label>
                <Input value={createForm.manufacturer || ''} onChange={(e) => setCreateForm((p) => ({ ...p, manufacturer: e.target.value }))} />
              </div>
              <div className="space-y-2">
                <Label>Model</Label>
                <Input value={createForm.model || ''} onChange={(e) => setCreateForm((p) => ({ ...p, model: e.target.value }))} />
              </div>

              <div className="space-y-2">
                <Label>Initial Mileage</Label>
                <Input
                  type="number"
                  value={createForm.mileage ?? ''}
                  onChange={(e) => setCreateForm((p) => ({ ...p, mileage: e.target.value === '' ? null : Number(e.target.value) }))}
                />
              </div>
              <div className="space-y-2">
                <Label>Initial Operating Hours</Label>
                <Input
                  type="number"
                  value={createForm.operatingHours ?? ''}
                  onChange={(e) => setCreateForm((p) => ({ ...p, operatingHours: e.target.value === '' ? null : Number(e.target.value) }))}
                />
              </div>
            </div>

            <DialogFooter>
              <Button variant="outline" onClick={() => setCreateOpen(false)}>
                Cancel
              </Button>
              <Button onClick={onCreate}>Create</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>

      <Card>
        <CardHeader className="space-y-4">
          <CardTitle>Vehicles</CardTitle>
          <div className="flex flex-col gap-3 md:flex-row md:items-center">
            <div className="relative flex-1">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                className="pl-8"
                placeholder="Search by name, asset number, plate or VIN..."
                value={searchTerm}
                onChange={(e) => {
                  setPage(1);
                  setSearchTerm(e.target.value);
                }}
              />
            </div>
            <Select
              value={categoryId}
              onValueChange={(v) => {
                setPage(1);
                setCategoryId(v);
              }}
            >
              <SelectTrigger className="w-full md:w-64">
                <SelectValue placeholder="Category" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All categories</SelectItem>
                {categories.map((c) => (
                  <SelectItem key={c.id} value={c.id}>
                    {c.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            <div className="flex items-center gap-2">
              <Button variant="outline" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
                Prev
              </Button>
              <div className="text-sm text-muted-foreground">
                Page {page} of {totalPages}
              </div>
              <Button variant="outline" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
                Next
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Asset #</TableHead>
                  <TableHead>Name</TableHead>
                  <TableHead>Plate</TableHead>
                  <TableHead>VIN</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {loading ? (
                  <TableRow>
                    <TableCell colSpan={6} className="py-8 text-center text-muted-foreground">
                      Loading...
                    </TableCell>
                  </TableRow>
                ) : result.items.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6} className="py-8 text-center text-muted-foreground">
                      No vehicles found
                    </TableCell>
                  </TableRow>
                ) : (
                  result.items.map((v) => (
                    <TableRow key={v.id}>
                      <TableCell className="font-medium">{v.assetNumber}</TableCell>
                      <TableCell>{v.name}</TableCell>
                      <TableCell>{v.licensePlate || '-'}</TableCell>
                      <TableCell>{v.vin || '-'}</TableCell>
                      <TableCell>{v.status}</TableCell>
                      <TableCell className="text-right">
                        <Button variant="ghost" size="sm" onClick={() => openEdit(v)}>
                          <Edit className="h-4 w-4" />
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

      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>Edit Vehicle</DialogTitle>
            <DialogDescription>Update the selected vehicle.</DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label>Name</Label>
              <Input value={editForm.name} onChange={(e) => setEditForm((p) => ({ ...p, name: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Status</Label>
              <Select value={editForm.status} onValueChange={(v) => setEditForm((p) => ({ ...p, status: v }))}>
                <SelectTrigger>
                  <SelectValue placeholder="Status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Active">Active</SelectItem>
                  <SelectItem value="Maintenance">Maintenance</SelectItem>
                  <SelectItem value="OutOfService">Out of Service</SelectItem>
                  <SelectItem value="Retired">Retired</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Category</Label>
              <Select value={editForm.assetCategoryId || undefined} onValueChange={(v) => setEditForm((p) => ({ ...p, assetCategoryId: v }))}>
                <SelectTrigger>
                  <SelectValue placeholder="Select category" />
                </SelectTrigger>
                <SelectContent>
                  {categories.map((c) => (
                    <SelectItem key={c.id} value={c.id}>
                      {c.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>License Plate</Label>
              <Input value={editForm.licensePlate || ''} onChange={(e) => setEditForm((p) => ({ ...p, licensePlate: e.target.value }))} />
            </div>

            <div className="space-y-2">
              <Label>VIN</Label>
              <Input value={editForm.vin || ''} onChange={(e) => setEditForm((p) => ({ ...p, vin: e.target.value }))} />
            </div>

            <div className="space-y-2">
              <Label>Mileage</Label>
              <Input
                type="number"
                value={editForm.mileage ?? ''}
                onChange={(e) => setEditForm((p) => ({ ...p, mileage: e.target.value === '' ? null : Number(e.target.value) }))}
              />
            </div>
            <div className="space-y-2">
              <Label>Operating Hours</Label>
              <Input
                type="number"
                value={editForm.operatingHours ?? ''}
                onChange={(e) => setEditForm((p) => ({ ...p, operatingHours: e.target.value === '' ? null : Number(e.target.value) }))}
              />
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
              Close
            </Button>
            <Button onClick={onUpdate}>Save</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
