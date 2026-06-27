'use client';

import React from 'react';
import { Plus, Search, Edit } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useToast } from '@/hooks/use-toast';
import { formatFleetDateTime } from '@/lib/date-format';

import fleetService, {
  FleetDriverDto,
  FleetVehicleAssignmentDto,
  FleetVehicleListDto,
  PagedResult,
  UpdateFleetVehicleDto,
} from '@/services/fleetService';

type AssetCategoryDto = {
  id: string;
  name: string;
  assetType?: string | null;
  isActive?: boolean;
};

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';
const FUEL_TYPE_OPTIONS = ['Petrol', 'Diesel', 'Electric', 'Hybrid'] as const;

function getAuthHeaders(): HeadersInit {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

function getVehicleStatusBadge(statusRaw: string | null | undefined) {
  const status = (statusRaw || '').trim() || 'Unknown';

  const colors: Record<string, string> = {
    Active: 'bg-green-100 text-green-800',
    InUse: 'bg-blue-100 text-blue-800',
    Maintenance: 'bg-yellow-100 text-yellow-800',
    OutOfService: 'bg-red-100 text-red-800',
    Retired: 'bg-gray-100 text-gray-800',
    Unknown: 'bg-gray-100 text-gray-800',
  };

  return <Badge className={colors[status] ?? 'bg-gray-100 text-gray-800'}>{status === 'OutOfService' ? 'Out of Service' : status}</Badge>;
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

  const [editOpen, setEditOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<FleetVehicleListDto | null>(null);

  const [assignOpen, setAssignOpen] = React.useState(false);
  const [historyOpen, setHistoryOpen] = React.useState(false);
  const [selectedVehicle, setSelectedVehicle] = React.useState<FleetVehicleListDto | null>(null);
  const [drivers, setDrivers] = React.useState<FleetDriverDto[]>([]);
  const [selectedEmployeeId, setSelectedEmployeeId] = React.useState<string>('none');
  const [assignmentHistory, setAssignmentHistory] = React.useState<FleetVehicleAssignmentDto[]>([]);

  const [editForm, setEditForm] = React.useState<UpdateFleetVehicleDto>({
    name: '',
    assetCategoryId: '',
    status: 'Active',
    licensePlate: '',
    vin: '',
    fuelType: '',
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
     
  }, []);

  React.useEffect(() => {
    loadVehicles();
  }, [loadVehicles]);

  const openEdit = (v: FleetVehicleListDto) => {
    if ((v.assetType || '').toLowerCase() !== 'vehicle') {
      toast({
        title: 'Not a vehicle',
        description: 'This fleet asset is not a Vehicle-type asset. Edit it from the Maintenance Assets screen.',
        variant: 'destructive',
      });
      return;
    }
    setEditing(v);
    setEditForm({
      name: v.name,
      assetCategoryId: v.assetCategoryId || '',
      status: v.status || 'Active',
      licensePlate: v.licensePlate || '',
      vin: v.vin || '',
      fuelType: v.fuelType || '',
      manufacturer: v.manufacturer || '',
      model: v.model || '',
      serialNumber: '',
      location: '',
      mileage: v.mileage ?? null,
      operatingHours: v.operatingHours ?? null,
    });
    setEditOpen(true);
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

  const loadEligibleDrivers = React.useCallback(async (vehicleAssetId?: string) => {
    try {
      const directory = await fleetService.getDrivers({ page: 1, pageSize: 100 });
      setDrivers((directory.items || []).filter((driver) =>
        driver.isActive &&
        driver.isLicenseVerified &&
        ['valid', 'expiring'].includes(driver.licenseStatus.toLowerCase()) &&
        driver.availabilityStatus !== 'Engaged' &&
        (!driver.isAssigned || driver.currentVehicleAssetId === vehicleAssetId)
      ));
    } catch (e: any) {
      toast({ title: 'Failed to load eligible drivers', description: e?.message || String(e), variant: 'destructive' });
    }
  }, [toast]);

  const openAssign = async (v: FleetVehicleListDto) => {
    if ((v.assetType || '').toLowerCase() !== 'vehicle') {
      toast({
        title: 'Not a vehicle',
        description: 'Driver assignment is only available for Vehicle-type assets.',
        variant: 'destructive',
      });
      return;
    }
    setSelectedVehicle(v);
    setSelectedEmployeeId('none');
    setAssignOpen(true);
    await loadEligibleDrivers(v.id);
  };

  const submitAssign = async () => {
    if (!selectedVehicle) return;
    if (selectedEmployeeId === 'none') return;

    try {
      await fleetService.assignDriver({ vehicleAssetId: selectedVehicle.id, employeeId: selectedEmployeeId });
      toast({ title: 'Driver assigned' });
      setAssignOpen(false);
      await loadVehicles();
    } catch (e: any) {
      toast({ title: 'Failed to assign driver', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  const openHistory = async (v: FleetVehicleListDto) => {
    if ((v.assetType || '').toLowerCase() !== 'vehicle') {
      toast({
        title: 'Not a vehicle',
        description: 'Driver assignment history is only available for Vehicle-type assets.',
        variant: 'destructive',
      });
      return;
    }
    setSelectedVehicle(v);
    setHistoryOpen(true);
    try {
      const history = await fleetService.getAssignments(v.id);
      setAssignmentHistory(history || []);
    } catch (e: any) {
      toast({ title: 'Failed to load assignment history', description: e?.message || String(e), variant: 'destructive' });
      setAssignmentHistory([]);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Fleets</h1>
          <p className="text-muted-foreground">Fleet vehicles are created as Maintenance Assets (Vehicle type) and managed here.</p>
        </div>

        <Button onClick={() => window.open('/maintenance/assets?assetType=Vehicle&create=1&addToFleet=1', '_blank')} title="Create vehicles from the Maintenance Assets screen">
          <Plus className="mr-2 h-4 w-4" />
          Create in Assets
        </Button>
      </div>

      <Card>
        <CardHeader className="space-y-4">
          <CardTitle>Fleets</CardTitle>
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
                <TableRow className="bg-muted/40">
                  <TableHead>Asset #</TableHead>
                  <TableHead>Name</TableHead>
                  <TableHead>Plate</TableHead>
                  <TableHead>VIN</TableHead>
                  <TableHead>Driver</TableHead>
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
                ) : result.items.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7} className="py-8 text-center text-muted-foreground">
                      No vehicles found
                    </TableCell>
                  </TableRow>
                ) : (
                  result.items.map((v, idx) => (
                    <TableRow key={v.id} className={idx % 2 === 1 ? 'bg-muted/10 hover:bg-muted/30' : 'hover:bg-muted/30'}>
                      <TableCell className="font-medium font-mono">{v.assetNumber}</TableCell>
                      <TableCell>
                        <div className="flex flex-col">
                          <span className="font-medium">{v.name}</span>
                          {v.categoryName ? <span className="text-xs text-muted-foreground">{v.categoryName}</span> : null}
                        </div>
                      </TableCell>
                      <TableCell className="font-mono text-sm">{v.licensePlate || <span className="text-muted-foreground">-</span>}</TableCell>
                      <TableCell className="max-w-[220px] truncate font-mono text-sm" title={v.vin || ''}>
                        {v.vin || <span className="text-muted-foreground">-</span>}
                      </TableCell>
                      <TableCell>
                        {v.currentDriverEmployeeName ? <Badge variant="outline">{v.currentDriverEmployeeName}</Badge> : <span className="text-muted-foreground">-</span>}
                      </TableCell>
                      <TableCell>{getVehicleStatusBadge(v.status)}</TableCell>
                      <TableCell className="text-right">
                        <div className="flex justify-end gap-1">
                          <Button variant="outline" size="sm" onClick={() => openAssign(v)} disabled={(v.status || '').toLowerCase() !== 'active'}>
                            Assign
                          </Button>
                          <Button variant="outline" size="sm" onClick={() => openHistory(v)}>
                            History
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => window.open(`/maintenance/assets?id=${v.id}&edit=1`, '_blank')}
                            title="Edit vehicle in Assets"
                          >
                            <Edit className="h-4 w-4" />
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

      <Dialog open={assignOpen} onOpenChange={setAssignOpen}>
        <DialogContent className="max-w-xl">
          <DialogHeader>
            <DialogTitle>Assign Driver</DialogTitle>
            <DialogDescription>Assign a primary driver for {selectedVehicle?.name}</DialogDescription>
          </DialogHeader>

          <div className="space-y-2">
            <Label>Eligible driver</Label>
            <Select value={selectedEmployeeId} onValueChange={setSelectedEmployeeId}>
              <SelectTrigger>
                <SelectValue placeholder="Select employee" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="none">Select employee</SelectItem>
                {drivers.map((driver) => (
                  <SelectItem key={driver.employeeId} value={driver.employeeId}>
                    {driver.fullName} ({driver.employeeNumber}) · {driver.licenseStatus} licence
                  </SelectItem>
                ))}
                {drivers.length === 0 && <SelectItem value="no-eligible-drivers" disabled>No eligible drivers available</SelectItem>}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">
              Only available drivers with a verified, current HR driver licence are listed.
            </p>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setAssignOpen(false)}>
              Cancel
            </Button>
            <Button onClick={submitAssign} disabled={!selectedVehicle || selectedEmployeeId === 'none'}>
              Assign
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={historyOpen} onOpenChange={setHistoryOpen}>
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>Assignment History</DialogTitle>
            <DialogDescription>{selectedVehicle?.name}</DialogDescription>
          </DialogHeader>

          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Employee</TableHead>
                  <TableHead>From</TableHead>
                  <TableHead>To</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Active</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {assignmentHistory.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={5} className="py-8 text-center text-muted-foreground">
                      No assignments
                    </TableCell>
                  </TableRow>
                ) : (
                  assignmentHistory.map((a) => (
                    <TableRow key={a.id}>
                      <TableCell>{a.employeeName}</TableCell>
                      <TableCell>{formatFleetDateTime(a.assignedFromUtc)}</TableCell>
                      <TableCell>{a.assignedToUtc ? formatFleetDateTime(a.assignedToUtc) : '-'}</TableCell>
                      <TableCell>{a.assignmentType}</TableCell>
                      <TableCell>{a.isActive ? 'Yes' : 'No'}</TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setHistoryOpen(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

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
              <Label>Fuel Type</Label>
              <Select value={editForm.fuelType || 'none'} onValueChange={(value) => setEditForm((p) => ({ ...p, fuelType: value === 'none' ? null : value }))}>
                <SelectTrigger>
                  <SelectValue placeholder="Select fuel type" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">Select fuel type</SelectItem>
                  {FUEL_TYPE_OPTIONS.map((option) => (
                    <SelectItem key={option} value={option}>
                      {option}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
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
