'use client';

import React from 'react';
import Link from 'next/link';
import { ArrowRight, Clock3, Edit, LayoutGrid, List, MapPin, Plus, Search, Truck } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { useToast } from '@/hooks/use-toast';
import { formatFleetDateTime } from '@/lib/date-format';
import { cn } from '@/lib/utils';

import fleetService, {
  FleetDriverDto,
  FleetTripDto,
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

type AssetLocationSnapshot = {
  location?: string | null;
  currentProjectName?: string | null;
  currentSiteLocationName?: string | null;
  lastMileageUpdate?: string | null;
  lastOperatingHoursUpdate?: string | null;
  updatedAt?: string | null;
  createdAt?: string | null;
  status?: string | null;
};

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';
const FUEL_TYPE_OPTIONS = ['Petrol', 'Diesel', 'Electric', 'Hybrid'] as const;
type FleetVehiclesViewMode = 'list' | 'grid';
const MAINTENANCE_DUE_SOON_DAYS = 14;

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

function getEarliestDateValue(values: Array<string | null | undefined>) {
  const dates = values
    .map((value) => {
      if (!value) return null;
      const time = new Date(value).getTime();
      return Number.isNaN(time) ? null : time;
    })
    .filter((value): value is number => value !== null)
    .sort((a, b) => a - b);

  return dates[0] ?? null;
}

function getVehicleMaintenanceBadge(vehicle: FleetVehicleListDto) {
  const status = (vehicle.status || '').trim().toLowerCase();
  const nextDueAt = getEarliestDateValue([vehicle.nextMaintenanceDate, vehicle.nextMaintenanceScheduleDueAt, vehicle.nextServiceDue]);
  const now = Date.now();
  const dueSoonLimit = now + MAINTENANCE_DUE_SOON_DAYS * 86_400_000;

  let label = 'Ready';
  let className = 'border-emerald-200 bg-emerald-50 text-emerald-700';

  if (nextDueAt !== null && nextDueAt < now) {
    label = 'Due';
    className = 'border-red-200 bg-red-100 text-red-800';
  } else if (status === 'maintenance') {
    label = 'Scheduled';
    className = 'border-sky-200 bg-sky-100 text-sky-800';
  } else if (nextDueAt !== null && nextDueAt <= dueSoonLimit) {
    label = 'Due soon';
    className = 'border-amber-200 bg-amber-100 text-amber-800';
  } else if (status === 'outofservice') {
    label = 'Attention';
    className = 'border-red-200 bg-red-50 text-red-700';
  } else if (status === 'retired') {
    label = 'Retired';
    className = 'border-slate-200 bg-slate-100 text-slate-700';
  }

  return (
    <span className={cn('inline-flex h-7 items-center rounded-full border px-2.5 text-xs font-semibold', className)}>
      {label}
    </span>
  );
}

function getVehicleLocationLabel(vehicle: FleetVehicleListDto, assetLocation?: AssetLocationSnapshot) {
  const siteLocation = (vehicle.currentSiteLocationName || assetLocation?.currentSiteLocationName)?.trim();
  if (siteLocation) return siteLocation;

  const physicalLocation = (vehicle.location || assetLocation?.location)?.trim();
  if (physicalLocation) return physicalLocation;

  const projectLocation = (vehicle.currentProjectName || assetLocation?.currentProjectName)?.trim();
  if (projectLocation) return projectLocation;

  return 'No location';
}

function getVehiclePlateLabel(vehicle: FleetVehicleListDto) {
  const plate = vehicle.licensePlate?.trim();
  return plate || 'No plate';
}

function getTripUsageAt(trip: FleetTripDto) {
  const status = (trip.status || '').trim().toLowerCase();
  const hasUsageTimestamp = Boolean(trip.completedAt || trip.actualEndAt || trip.dispatchedAt || trip.actualStartAt);

  if (status !== 'completed' && status !== 'dispatched' && !hasUsageTimestamp) return null;

  return trip.completedAt || trip.actualEndAt || trip.dispatchedAt || trip.actualStartAt || trip.plannedStartAt || trip.createdAt || null;
}

function getVehicleLastUsedAt(vehicle: FleetVehicleListDto, tripLastUsedAt?: string | null, assetLocation?: AssetLocationSnapshot) {
  const directLastUsed = vehicle.lastUsedAtUtc?.trim();
  if (directLastUsed) return directLastUsed;

  const tripLastUsed = tripLastUsedAt?.trim();
  if (tripLastUsed) return tripLastUsed;

  const mileageUpdate = assetLocation?.lastMileageUpdate?.trim();
  if (mileageUpdate) return mileageUpdate;

  const hoursUpdate = assetLocation?.lastOperatingHoursUpdate?.trim();
  if (hoursUpdate) return hoursUpdate;

  const status = (vehicle.status || assetLocation?.status || '').trim().toLowerCase();
  if (status === 'inuse') {
    return assetLocation?.updatedAt?.trim() || assetLocation?.createdAt?.trim() || null;
  }

  return null;
}

function formatLastUsed(value: string | null | undefined) {
  if (!value) return 'No use yet';

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return 'No use yet';

  const diffMs = Date.now() - date.getTime();
  const diffDays = Math.max(0, Math.floor(diffMs / 86_400_000));

  if (diffDays === 0) return 'Today';
  if (diffDays === 1) return '1d ago';
  return `${diffDays}d ago`;
}

export default function FleetVehiclesPage() {
  const { toast } = useToast();

  const [loading, setLoading] = React.useState(true);
  const [categories, setCategories] = React.useState<AssetCategoryDto[]>([]);

  const [page, setPage] = React.useState(1);
  const [pageSize] = React.useState(25);
  const [searchTerm, setSearchTerm] = React.useState('');
  const [categoryId, setCategoryId] = React.useState<string>('all');
  const [viewMode, setViewMode] = React.useState<FleetVehiclesViewMode>('list');

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
  const [utilizationByVehicle, setUtilizationByVehicle] = React.useState<Record<string, number>>({});
  const [tripLastUsedByVehicle, setTripLastUsedByVehicle] = React.useState<Record<string, string | null>>({});
  const [assetLocationByVehicle, setAssetLocationByVehicle] = React.useState<Record<string, AssetLocationSnapshot>>({});

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

  const loadUtilization = React.useCallback(async () => {
    try {
      const toUtc = new Date();
      const fromUtc = new Date(toUtc);
      fromUtc.setDate(fromUtc.getDate() - 30);

      const summary = await fleetService.getUtilization({
        fromUtc: fromUtc.toISOString(),
        toUtc: toUtc.toISOString(),
        top: 500,
      });

      const rows = summary.rows || [];
      const maxKm = Math.max(0, ...rows.map((row) => Number(row.totalKm) || 0));
      const maxHours = Math.max(0, ...rows.map((row) => Number(row.totalHours) || 0));
      const maxTrips = Math.max(0, ...rows.map((row) => Number(row.completedTrips) || 0));

      const next: Record<string, number> = {};
      rows.forEach((row) => {
        const km = Number(row.totalKm) || 0;
        const hours = Number(row.totalHours) || 0;
        const trips = Number(row.completedTrips) || 0;
        const percent = maxKm > 0
          ? (km / maxKm) * 100
          : maxHours > 0
            ? (hours / maxHours) * 100
            : maxTrips > 0
              ? (trips / maxTrips) * 100
              : 0;

        next[row.vehicleAssetId] = Math.max(0, Math.min(100, Math.round(percent)));
      });

      setUtilizationByVehicle(next);
    } catch {
      setUtilizationByVehicle({});
    }
  }, []);

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
    const saved = window.localStorage.getItem('fleetVehiclesViewMode');
    if (saved === 'list' || saved === 'grid') setViewMode(saved);
  }, []);

  React.useEffect(() => {
    loadVehicles();
  }, [loadVehicles]);

  React.useEffect(() => {
    loadUtilization();
  }, [loadUtilization]);

  React.useEffect(() => {
    const vehiclesNeedingTripUsage = result.items.filter(
      (vehicle) => !vehicle.lastUsedAtUtc && !(vehicle.id in tripLastUsedByVehicle),
    );

    if (vehiclesNeedingTripUsage.length === 0) return;

    let cancelled = false;

    const loadTripUsage = async () => {
      const entries = await Promise.all(
        vehiclesNeedingTripUsage.map(async (vehicle) => {
          try {
            const [dispatchedTrips, completedTrips] = await Promise.all([
              fleetService.getTrips({ vehicleAssetId: vehicle.id, status: 'Dispatched', page: 1, pageSize: 10 }),
              fleetService.getTrips({ vehicleAssetId: vehicle.id, status: 'Completed', page: 1, pageSize: 10 }),
            ]);
            const latestUsage = [...(dispatchedTrips.items || []), ...(completedTrips.items || [])]
              .map(getTripUsageAt)
              .filter((value): value is string => Boolean(value))
              .sort((a, b) => new Date(b).getTime() - new Date(a).getTime())[0] || null;

            return [vehicle.id, latestUsage] as const;
          } catch {
            return [vehicle.id, null] as const;
          }
        }),
      );

      if (cancelled) return;

      setTripLastUsedByVehicle((current) => {
        const next = { ...current };
        entries.forEach(([vehicleId, lastUsedAt]) => {
          next[vehicleId] = lastUsedAt;
        });
        return next;
      });
    };

    loadTripUsage();

    return () => {
      cancelled = true;
    };
  }, [result.items, tripLastUsedByVehicle]);

  React.useEffect(() => {
    const missingLocationVehicles = result.items.filter(
      (vehicle) => {
        const hasCheckedTrips = vehicle.lastUsedAtUtc || vehicle.id in tripLastUsedByVehicle;
        const hasTripUsage = Boolean(vehicle.lastUsedAtUtc || tripLastUsedByVehicle[vehicle.id]);
        const needsLocation = getVehicleLocationLabel(vehicle, assetLocationByVehicle[vehicle.id]) === 'No location';
        const needsLastUsedFallback = hasCheckedTrips && !hasTripUsage && !getVehicleLastUsedAt(vehicle, tripLastUsedByVehicle[vehicle.id], assetLocationByVehicle[vehicle.id]);

        return (needsLocation || needsLastUsedFallback) && !assetLocationByVehicle[vehicle.id];
      },
    );

    if (missingLocationVehicles.length === 0) return;

    let cancelled = false;

    const loadAssetLocations = async () => {
      const entries = await Promise.all(
        missingLocationVehicles.map(async (vehicle) => {
          try {
            const response = await fetch(`${API_BASE_URL}/maintenance/assets/${vehicle.id}`, { headers: getAuthHeaders() });
            if (!response.ok) return null;

            const asset: Record<string, any> = await response.json();
            return [
              vehicle.id,
              {
                location: asset.location || asset.Location || null,
                currentProjectName: asset.currentProjectName || asset.CurrentProjectName || null,
                currentSiteLocationName: asset.currentSiteLocationName || asset.CurrentSiteLocationName || null,
                lastMileageUpdate: asset.lastMileageUpdate || asset.LastMileageUpdate || null,
                lastOperatingHoursUpdate: asset.lastOperatingHoursUpdate || asset.LastOperatingHoursUpdate || null,
                updatedAt: asset.updatedAt || asset.UpdatedAt || null,
                createdAt: asset.createdAt || asset.CreatedAt || null,
                status: asset.status || asset.Status || null,
              },
            ] as const;
          } catch {
            return null;
          }
        }),
      );

      if (cancelled) return;

      setAssetLocationByVehicle((current) => {
        if (entries.every((entry) => !entry)) return current;

        const next = { ...current };
        entries.forEach((entry) => {
          if (entry) next[entry[0]] = entry[1];
        });
        return next;
      });
    };

    loadAssetLocations();

    return () => {
      cancelled = true;
    };
  }, [assetLocationByVehicle, result.items, tripLastUsedByVehicle]);

  const changeViewMode = (mode: FleetVehiclesViewMode) => {
    setViewMode(mode);
    window.localStorage.setItem('fleetVehiclesViewMode', mode);
  };

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

      <div className="space-y-4">
        <div className="rounded-md border bg-card p-4 shadow-sm">
          <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div>
              <h2 className="text-lg font-semibold">Fleets</h2>
              <p className="text-sm text-muted-foreground">{result.totalCount.toLocaleString()} vehicles in the current filter</p>
            </div>

            <TooltipProvider delayDuration={150}>
              <div className="inline-flex h-10 w-fit items-center rounded-md border bg-background p-1">
                <Tooltip>
                  <TooltipTrigger asChild>
                    <Button
                      type="button"
                      variant={viewMode === 'list' ? 'default' : 'ghost'}
                      size="icon"
                      className={cn('h-8 w-8', viewMode !== 'list' && 'text-muted-foreground')}
                      onClick={() => changeViewMode('list')}
                      aria-label="Show list view"
                    >
                      <List className="h-4 w-4" />
                    </Button>
                  </TooltipTrigger>
                  <TooltipContent>List view</TooltipContent>
                </Tooltip>
                <Tooltip>
                  <TooltipTrigger asChild>
                    <Button
                      type="button"
                      variant={viewMode === 'grid' ? 'default' : 'ghost'}
                      size="icon"
                      className={cn('h-8 w-8', viewMode !== 'grid' && 'text-muted-foreground')}
                      onClick={() => changeViewMode('grid')}
                      aria-label="Show card view"
                    >
                      <LayoutGrid className="h-4 w-4" />
                    </Button>
                  </TooltipTrigger>
                  <TooltipContent>Card view</TooltipContent>
                </Tooltip>
              </div>
            </TooltipProvider>
          </div>

          <div className="mt-4 flex flex-col gap-3 lg:flex-row lg:items-center">
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
              <SelectTrigger className="w-full lg:w-64">
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
              <div className="min-w-20 text-center text-sm text-muted-foreground">
                Page {page} of {totalPages}
              </div>
              <Button variant="outline" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
                Next
              </Button>
            </div>
          </div>
        </div>

        {viewMode === 'list' ? (
          <div className="rounded-md border bg-card">
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
                      <TableCell className="font-medium font-mono">
                        <Link
                          href={`/maintenance/assets?id=${v.id}`}
                          className="text-blue-600 underline-offset-2 hover:underline"
                        >
                          {v.assetNumber}
                        </Link>
                      </TableCell>
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
        ) : loading ? (
          <div className="rounded-md border bg-card py-10 text-center text-muted-foreground">Loading...</div>
        ) : result.items.length === 0 ? (
          <div className="rounded-md border bg-card py-10 text-center text-muted-foreground">No vehicles found</div>
        ) : (
          <div className="rounded-md bg-slate-100 p-4">
            <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4">
              {result.items.map((v) => {
                const utilization = utilizationByVehicle[v.id] ?? 0;
                const assetLocation = assetLocationByVehicle[v.id];
                const lastUsedAt = getVehicleLastUsedAt(v, tripLastUsedByVehicle[v.id], assetLocation);

                return (
                  <Card key={v.id} className="overflow-hidden border-slate-200 bg-white shadow-sm transition-shadow hover:shadow-md">
                    <CardContent className="flex min-h-72 flex-col gap-4 p-4">
                      <div className="flex items-start gap-3">
                        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-rose-50 text-rose-600 ring-1 ring-rose-100">
                          <Truck className="h-5 w-5" />
                        </div>
                        <div className="min-w-0 flex-1">
                          <Link href={`/maintenance/assets?id=${v.id}`} className="block truncate text-base font-semibold text-slate-950">
                            {v.assetNumber || v.name}
                          </Link>
                          <p className="truncate text-sm text-slate-500">{v.currentDriverEmployeeName || 'Unassigned'}</p>
                        </div>
                        <Button
                          variant="ghost"
                          size="icon"
                          className="h-8 w-8 shrink-0 text-slate-700 hover:text-blue-700"
                          onClick={() => window.open(`/maintenance/assets?id=${v.id}&edit=1`, '_blank')}
                          title="Edit vehicle in Assets"
                        >
                          <Edit className="h-4 w-4" />
                          <span className="sr-only">Edit vehicle</span>
                        </Button>
                      </div>

                      <div className="space-y-2">
                        <div className="flex items-center justify-between gap-3 text-sm">
                          <span className="text-slate-500">Utilization</span>
                          <span className="font-semibold tabular-nums text-slate-900">{utilization}%</span>
                        </div>
                        <div className="h-1.5 overflow-hidden rounded-full bg-slate-100">
                          <div className="h-full rounded-full bg-rose-500" style={{ width: `${utilization}%` }} />
                        </div>
                      </div>

                      <div className="space-y-2 text-sm text-slate-500">
                        <div className="flex min-w-0 items-center gap-2">
                          <MapPin className="h-4 w-4 shrink-0 text-emerald-500" />
                          <span className="truncate">{getVehicleLocationLabel(v, assetLocation)}</span>
                        </div>
                        <div className="flex min-w-0 items-center gap-2">
                          <Truck className="h-4 w-4 shrink-0 text-slate-400" />
                          <span className="truncate font-medium text-slate-600">{getVehiclePlateLabel(v)}</span>
                        </div>
                      </div>

                      <div className="mt-auto flex items-center justify-between gap-3 pt-8">
                        {getVehicleStatusBadge(v.status)}
                        {getVehicleMaintenanceBadge(v)}
                      </div>

                      <div className="flex items-center justify-between gap-3">
                        <div className="flex min-w-0 items-center gap-2 text-sm text-slate-500">
                          <Clock3 className="h-4 w-4 shrink-0" />
                          <span className="truncate">{formatLastUsed(lastUsedAt)}</span>
                        </div>
                        <Button asChild size="sm" className="bg-blue-600 hover:bg-blue-700">
                          <Link href={`/maintenance/assets?id=${v.id}`}>
                            <ArrowRight className="h-4 w-4" />
                            Detail
                          </Link>
                        </Button>
                      </div>
                    </CardContent>
                  </Card>
                );
              })}
            </div>
          </div>
        )}
      </div>

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
