'use client';

import React from 'react';
import { Suspense } from 'react';
import { useSearchParams } from 'next/navigation';
import { format } from 'date-fns';
import { Plus, Search, Eye, Truck, CheckCircle2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';

import fleetService, { CreateFleetTripDto, DispatchFleetTripDto, FleetTripDto, FleetVehicleListDto, PagedResult } from '@/services/fleetService';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowApprovalHistoryPanel } from '@/components/workflow/WorkflowApprovalHistoryPanel';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

function getAuthHeaders(): HeadersInit {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

type EmployeeDto = {
  id: string;
  firstName?: string | null;
  lastName?: string | null;
  employeeNumber?: string | null;
  status?: string | null;
};

function formatDate(value?: string | null) {
  if (!value) return '-';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return '-';
  return format(d, 'dd MMM yyyy, HH:mm');
}

function toDatetimeLocal(value: Date) {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}T${pad(value.getHours())}:${pad(value.getMinutes())}`;
}

export default function FleetTripsPage() {
  return (
    <Suspense fallback={<div className="p-6 text-muted-foreground">Loading...</div>}>
      <FleetTripsPageContent />
    </Suspense>
  );
}

function FleetTripsPageContent() {
  const { toast } = useToast();
  const searchParams = useSearchParams();
  const initialOpenId = searchParams.get('id');

  const [loading, setLoading] = React.useState(true);
  const [vehicles, setVehicles] = React.useState<FleetVehicleListDto[]>([]);
  const [employees, setEmployees] = React.useState<EmployeeDto[]>([]);

  const [page, setPage] = React.useState(1);
  const [pageSize] = React.useState(25);
  const [searchTerm, setSearchTerm] = React.useState('');
  const [status, setStatus] = React.useState<string>('all');

  const [result, setResult] = React.useState<PagedResult<FleetTripDto>>({
    items: [],
    totalCount: 0,
    page: 1,
    pageSize,
  });

  const [createOpen, setCreateOpen] = React.useState(false);
  const [viewOpen, setViewOpen] = React.useState(false);
  const [selected, setSelected] = React.useState<FleetTripDto | null>(null);

  const [dispatchOpen, setDispatchOpen] = React.useState(false);
  const [dispatchForm, setDispatchForm] = React.useState<DispatchFleetTripDto>({
    dispatchedAt: toDatetimeLocal(new Date()),
    startMileage: null,
    startOperatingHours: null,
  });

  const [completeOpen, setCompleteOpen] = React.useState(false);
  const [completeForm, setCompleteForm] = React.useState({
    completedAt: toDatetimeLocal(new Date()),
    endMileage: null as number | null,
    endOperatingHours: null as number | null,
    notes: '' as string,
  });

  const [createForm, setCreateForm] = React.useState<CreateFleetTripDto>({
    vehicleAssetId: '',
    driverEmployeeId: null,
    purpose: '',
    origin: '',
    destination: '',
    notes: '',
    plannedStartAt: null,
    plannedEndAt: null,
  });

  const loadLookups = React.useCallback(async () => {
    try {
      const [vRes, eRes] = await Promise.all([
        fleetService.getVehicles({ page: 1, pageSize: 100 }),
        fetch(`${API_BASE_URL}/employees?page=1&pageSize=100`, { headers: getAuthHeaders() }),
      ]);

      setVehicles(vRes.items || []);

      if (!eRes.ok) throw new Error(await eRes.text());
      const rawEmployees: EmployeeDto[] = await eRes.json();
      const activeEmployees = (rawEmployees || []).filter((e) => (e.status || '').toLowerCase() !== 'inactive');
      setEmployees(activeEmployees);
    } catch (e: any) {
      toast({ title: 'Failed to load lookups', description: e?.message || String(e), variant: 'destructive' });
    }
  }, [toast]);

  const loadTrips = React.useCallback(async () => {
    setLoading(true);
    try {
      const res = await fleetService.getTrips({
        page,
        pageSize,
        searchTerm: searchTerm.trim() || undefined,
        status: status !== 'all' ? status : undefined,
      });
      setResult(res);
    } catch (e: any) {
      toast({ title: 'Failed to load trips', description: e?.message || String(e), variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, searchTerm, status, toast]);

  React.useEffect(() => {
    loadLookups();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  React.useEffect(() => {
    loadTrips();
  }, [loadTrips]);

  React.useEffect(() => {
    if (!initialOpenId) return;
    (async () => {
      try {
        const trip = await fleetService.getTrip(initialOpenId);
        setSelected(trip);
        setViewOpen(true);
      } catch {
        // Ignore
      }
    })();
  }, [initialOpenId]);

  const openView = async (id: string) => {
    try {
      const trip = await fleetService.getTrip(id);
      setSelected(trip);
      setViewOpen(true);
    } catch (e: any) {
      toast({ title: 'Failed to load trip', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  const refreshSelected = async () => {
    if (!selected) return;
    const trip = await fleetService.getTrip(selected.id);
    setSelected(trip);
  };

  const onCreate = async () => {
    try {
      if (!createForm.vehicleAssetId) throw new Error('Vehicle is required');
      const created = await fleetService.createTrip({
        ...createForm,
        purpose: createForm.purpose?.trim() || null,
        origin: createForm.origin?.trim() || null,
        destination: createForm.destination?.trim() || null,
        notes: createForm.notes?.trim() || null,
      });

      toast({ title: 'Trip created' });
      setCreateOpen(false);
      setCreateForm({
        vehicleAssetId: '',
        driverEmployeeId: null,
        purpose: '',
        origin: '',
        destination: '',
        notes: '',
        plannedStartAt: null,
        plannedEndAt: null,
      });
      await loadTrips();
      setSelected(created);
      setViewOpen(true);
    } catch (e: any) {
      toast({ title: 'Failed to create trip', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  const onDispatch = async () => {
    if (!selected) return;
    try {
      const dto: DispatchFleetTripDto = {
        dispatchedAt: dispatchForm.dispatchedAt ? new Date(dispatchForm.dispatchedAt).toISOString() : null,
        startMileage: dispatchForm.startMileage ?? null,
        startOperatingHours: dispatchForm.startOperatingHours ?? null,
      };
      const updated = await fleetService.dispatchTrip(selected.id, dto);
      toast({ title: 'Trip dispatched' });
      setSelected(updated);
      setDispatchOpen(false);
      await loadTrips();
    } catch (e: any) {
      toast({ title: 'Dispatch failed', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  const onComplete = async () => {
    if (!selected) return;
    try {
      const dto = {
        completedAt: completeForm.completedAt ? new Date(completeForm.completedAt).toISOString() : null,
        endMileage: completeForm.endMileage ?? null,
        endOperatingHours: completeForm.endOperatingHours ?? null,
        notes: completeForm.notes?.trim() || null,
      };
      const updated = await fleetService.completeTrip(selected.id, dto);
      toast({ title: 'Trip completed' });
      setSelected(updated);
      setCompleteOpen(false);
      await loadTrips();
    } catch (e: any) {
      toast({ title: 'Complete failed', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  const totalPages = Math.max(1, Math.ceil((result.totalCount || 0) / pageSize));

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Fleet Trips</h1>
          <p className="text-muted-foreground">Trip requests, approvals and dispatch tracking</p>
        </div>

        <Dialog open={createOpen} onOpenChange={setCreateOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              New Trip
            </Button>
          </DialogTrigger>
          <DialogContent className="max-w-4xl">
            <DialogHeader>
              <DialogTitle>Create Trip</DialogTitle>
              <DialogDescription>Create a draft trip request, then submit for workflow approval.</DialogDescription>
            </DialogHeader>

            <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Vehicle</Label>
                <Select value={createForm.vehicleAssetId || undefined} onValueChange={(v) => setCreateForm((p) => ({ ...p, vehicleAssetId: v }))}>
                  <SelectTrigger>
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
              </div>

              <div className="space-y-2">
                <Label>Driver</Label>
                <Select
                  value={createForm.driverEmployeeId || 'none'}
                  onValueChange={(v) => setCreateForm((p) => ({ ...p, driverEmployeeId: v === 'none' ? null : v }))}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select driver (optional)" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Unassigned</SelectItem>
                    {employees.map((e) => (
                      <SelectItem key={e.id} value={e.id}>
                        {(e.firstName || '').trim()} {(e.lastName || '').trim()} {e.employeeNumber ? `(${e.employeeNumber})` : ''}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <Label>Purpose</Label>
                <Input value={createForm.purpose || ''} onChange={(e) => setCreateForm((p) => ({ ...p, purpose: e.target.value }))} />
              </div>
              <div className="space-y-2">
                <Label>Origin</Label>
                <Input value={createForm.origin || ''} onChange={(e) => setCreateForm((p) => ({ ...p, origin: e.target.value }))} />
              </div>
              <div className="space-y-2">
                <Label>Destination</Label>
                <Input value={createForm.destination || ''} onChange={(e) => setCreateForm((p) => ({ ...p, destination: e.target.value }))} />
              </div>
              <div className="space-y-2">
                <Label>Planned Start</Label>
                <Input
                  type="datetime-local"
                  value={createForm.plannedStartAt || ''}
                  onChange={(e) => setCreateForm((p) => ({ ...p, plannedStartAt: e.target.value || null }))}
                />
              </div>
              <div className="space-y-2">
                <Label>Planned End</Label>
                <Input
                  type="datetime-local"
                  value={createForm.plannedEndAt || ''}
                  onChange={(e) => setCreateForm((p) => ({ ...p, plannedEndAt: e.target.value || null }))}
                />
              </div>
              <div className="space-y-2 md:col-span-2">
                <Label>Notes</Label>
                <Textarea value={createForm.notes || ''} onChange={(e) => setCreateForm((p) => ({ ...p, notes: e.target.value }))} />
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
          <CardTitle>Trips</CardTitle>
          <div className="flex flex-col gap-3 md:flex-row md:items-center">
            <div className="relative flex-1">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                className="pl-8"
                placeholder="Search by vehicle, plate, origin/destination, purpose..."
                value={searchTerm}
                onChange={(e) => {
                  setPage(1);
                  setSearchTerm(e.target.value);
                }}
              />
            </div>

            <Select
              value={status}
              onValueChange={(v) => {
                setPage(1);
                setStatus(v);
              }}
            >
              <SelectTrigger className="w-full md:w-56">
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                <SelectItem value="Draft">Draft</SelectItem>
                <SelectItem value="Submitted">Submitted</SelectItem>
                <SelectItem value="Approved">Approved</SelectItem>
                <SelectItem value="Rejected">Rejected</SelectItem>
                <SelectItem value="Dispatched">Dispatched</SelectItem>
                <SelectItem value="Completed">Completed</SelectItem>
                <SelectItem value="Cancelled">Cancelled</SelectItem>
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
                  <TableHead>Status</TableHead>
                  <TableHead>Vehicle</TableHead>
                  <TableHead>Driver</TableHead>
                  <TableHead>Planned</TableHead>
                  <TableHead>Created</TableHead>
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
                      No trips found
                    </TableCell>
                  </TableRow>
                ) : (
                  result.items.map((t) => (
                    <TableRow key={t.id}>
                      <TableCell>{t.status}</TableCell>
                      <TableCell>
                        {t.vehicleAssetNumber ? `${t.vehicleAssetNumber} — ` : ''}
                        {t.vehicleName}
                      </TableCell>
                      <TableCell>{t.driverEmployeeName || '-'}</TableCell>
                      <TableCell>
                        {t.plannedStartAt ? formatDate(t.plannedStartAt) : '-'}
                        {t.plannedEndAt ? ` → ${formatDate(t.plannedEndAt)}` : ''}
                      </TableCell>
                      <TableCell>{formatDate(t.createdAt)}</TableCell>
                      <TableCell className="text-right">
                        <Button variant="ghost" size="sm" onClick={() => openView(t.id)}>
                          <Eye className="h-4 w-4" />
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

      <Dialog open={viewOpen} onOpenChange={setViewOpen}>
        <DialogContent className="max-w-5xl">
          <DialogHeader>
            <DialogTitle>Trip Details</DialogTitle>
            <DialogDescription>View trip information, approvals, and dispatch/completion.</DialogDescription>
          </DialogHeader>

          {!selected ? (
            <div className="py-10 text-center text-muted-foreground">No trip selected</div>
          ) : (
            <div className="space-y-4">
              <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
                <Card>
                  <CardHeader className="py-3">
                    <CardTitle className="text-base">Status</CardTitle>
                  </CardHeader>
                  <CardContent className="pb-4">{selected.status}</CardContent>
                </Card>
                <Card>
                  <CardHeader className="py-3">
                    <CardTitle className="text-base">Vehicle</CardTitle>
                  </CardHeader>
                  <CardContent className="pb-4">
                    {selected.vehicleAssetNumber ? `${selected.vehicleAssetNumber} — ` : ''}
                    {selected.vehicleName}
                    {selected.vehicleLicensePlate ? ` (${selected.vehicleLicensePlate})` : ''}
                  </CardContent>
                </Card>
                <Card>
                  <CardHeader className="py-3">
                    <CardTitle className="text-base">Driver</CardTitle>
                  </CardHeader>
                  <CardContent className="pb-4">{selected.driverEmployeeName || 'Unassigned'}</CardContent>
                </Card>
              </div>

              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                <Card>
                  <CardHeader className="py-3">
                    <CardTitle className="text-base">Plan</CardTitle>
                  </CardHeader>
                  <CardContent className="space-y-2 pb-4 text-sm">
                    <div>
                      <span className="text-muted-foreground">Purpose:</span> {selected.purpose || '-'}
                    </div>
                    <div>
                      <span className="text-muted-foreground">Origin:</span> {selected.origin || '-'}
                    </div>
                    <div>
                      <span className="text-muted-foreground">Destination:</span> {selected.destination || '-'}
                    </div>
                    <div>
                      <span className="text-muted-foreground">Planned:</span> {formatDate(selected.plannedStartAt)} → {formatDate(selected.plannedEndAt)}
                    </div>
                  </CardContent>
                </Card>

                <Card>
                  <CardHeader className="py-3">
                    <CardTitle className="text-base">Execution</CardTitle>
                  </CardHeader>
                  <CardContent className="space-y-2 pb-4 text-sm">
                    <div>
                      <span className="text-muted-foreground">Dispatched:</span> {formatDate(selected.dispatchedAt)}
                    </div>
                    <div>
                      <span className="text-muted-foreground">Completed:</span> {formatDate(selected.completedAt)}
                    </div>
                    <div>
                      <span className="text-muted-foreground">Start meters:</span> {selected.startMileage ?? '-'} km / {selected.startOperatingHours ?? '-'} hrs
                    </div>
                    <div>
                      <span className="text-muted-foreground">End meters:</span> {selected.endMileage ?? '-'} km / {selected.endOperatingHours ?? '-'} hrs
                    </div>
                  </CardContent>
                </Card>
              </div>

              <div className="flex flex-wrap items-center justify-between gap-2">
                <WorkflowApprovalActions
                  entityType="FleetTrip"
                  entityId={selected.id}
                  entityLabel="Fleet Trip"
                  entityNumber={selected.vehicleAssetNumber || undefined}
                  status={selected.status}
                  loadWorkflowSummary
                  onSubmit={async () => {
                    await fleetService.submitTrip(selected.id);
                    await refreshSelected();
                    await loadTrips();
                  }}
                  onApprove={async (comments) => {
                    await fleetService.approveTrip(selected.id, comments);
                    await refreshSelected();
                    await loadTrips();
                  }}
                  onReject={async (comments) => {
                    await fleetService.rejectTrip(selected.id, 'Rejected', comments);
                    await refreshSelected();
                    await loadTrips();
                  }}
                />

                <div className="flex items-center gap-2">
                  <Button
                    variant="outline"
                    disabled={selected.status !== 'Approved'}
                    onClick={() => {
                      setDispatchForm({
                        dispatchedAt: toDatetimeLocal(new Date()),
                        startMileage: selected.startMileage ?? null,
                        startOperatingHours: selected.startOperatingHours ?? null,
                      });
                      setDispatchOpen(true);
                    }}
                  >
                    <Truck className="mr-2 h-4 w-4" />
                    Dispatch
                  </Button>
                  <Button
                    variant="outline"
                    disabled={selected.status !== 'Dispatched'}
                    onClick={() => {
                      setCompleteForm({
                        completedAt: toDatetimeLocal(new Date()),
                        endMileage: selected.endMileage ?? null,
                        endOperatingHours: selected.endOperatingHours ?? null,
                        notes: selected.notes || '',
                      });
                      setCompleteOpen(true);
                    }}
                  >
                    <CheckCircle2 className="mr-2 h-4 w-4" />
                    Complete
                  </Button>
                </div>
              </div>

              <WorkflowApprovalHistoryPanel entityType="FleetTrip" entityId={selected.id} />
            </div>
          )}

          <DialogFooter>
            <Button variant="outline" onClick={() => setViewOpen(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={dispatchOpen} onOpenChange={setDispatchOpen}>
        <DialogContent className="max-w-xl">
          <DialogHeader>
            <DialogTitle>Dispatch Trip</DialogTitle>
            <DialogDescription>Dispatch is blocked when critical compliance is due soon/overdue (per Maintenance settings).</DialogDescription>
          </DialogHeader>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="space-y-2 md:col-span-2">
              <Label>Dispatched At</Label>
              <Input
                type="datetime-local"
                value={dispatchForm.dispatchedAt || ''}
                onChange={(e) => setDispatchForm((p) => ({ ...p, dispatchedAt: e.target.value || null }))}
              />
            </div>
            <div className="space-y-2">
              <Label>Start Mileage (km)</Label>
              <Input
                type="number"
                value={dispatchForm.startMileage ?? ''}
                onChange={(e) => setDispatchForm((p) => ({ ...p, startMileage: e.target.value === '' ? null : Number(e.target.value) }))}
              />
            </div>
            <div className="space-y-2">
              <Label>Start Operating Hours</Label>
              <Input
                type="number"
                value={dispatchForm.startOperatingHours ?? ''}
                onChange={(e) => setDispatchForm((p) => ({ ...p, startOperatingHours: e.target.value === '' ? null : Number(e.target.value) }))}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDispatchOpen(false)}>
              Cancel
            </Button>
            <Button onClick={onDispatch}>Dispatch</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={completeOpen} onOpenChange={setCompleteOpen}>
        <DialogContent className="max-w-xl">
          <DialogHeader>
            <DialogTitle>Complete Trip</DialogTitle>
            <DialogDescription>Capture end readings (odometer/hour-meter). These update the vehicle metrics.</DialogDescription>
          </DialogHeader>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="space-y-2 md:col-span-2">
              <Label>Completed At</Label>
              <Input
                type="datetime-local"
                value={completeForm.completedAt}
                onChange={(e) => setCompleteForm((p) => ({ ...p, completedAt: e.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label>End Mileage (km)</Label>
              <Input
                type="number"
                value={completeForm.endMileage ?? ''}
                onChange={(e) => setCompleteForm((p) => ({ ...p, endMileage: e.target.value === '' ? null : Number(e.target.value) }))}
              />
            </div>
            <div className="space-y-2">
              <Label>End Operating Hours</Label>
              <Input
                type="number"
                value={completeForm.endOperatingHours ?? ''}
                onChange={(e) => setCompleteForm((p) => ({ ...p, endOperatingHours: e.target.value === '' ? null : Number(e.target.value) }))}
              />
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>Notes</Label>
              <Textarea value={completeForm.notes} onChange={(e) => setCompleteForm((p) => ({ ...p, notes: e.target.value }))} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCompleteOpen(false)}>
              Cancel
            </Button>
            <Button onClick={onComplete}>Complete</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
