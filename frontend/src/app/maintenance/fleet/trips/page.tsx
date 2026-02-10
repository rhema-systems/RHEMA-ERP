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
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/hooks/use-toast';

import fleetService, {
  CompleteFleetTripInspectionDto,
  CreateFleetTripDto,
  DispatchFleetTripDto,
  FleetTripDto,
  FleetTripInspectionDto,
  FleetVehicleListDto,
  PagedResult,
  StartFleetTripInspectionDto,
} from '@/services/fleetService';
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

type InspectionTemplateLite = {
  id: string;
  name: string;
  category?: string | null;
  isActive?: boolean;
};

type InspectionChecklistItem = {
  id: string;
  item: string;
  type: string;
  required: boolean;
  order: number;
};

type InspectionTemplateDetail = {
  id: string;
  name: string;
  code: string;
  description?: string | null;
  category?: string | null;
  frequency?: string | null;
  estimatedDuration?: number | null;
  isActive?: boolean;
  checklistItems: InspectionChecklistItem[];
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

  const [inspections, setInspections] = React.useState<FleetTripInspectionDto[]>([]);
  const [inspectionTemplates, setInspectionTemplates] = React.useState<InspectionTemplateLite[]>([]);
  const [inspectionsLoading, setInspectionsLoading] = React.useState(false);

  const [startInspectionOpen, setStartInspectionOpen] = React.useState(false);
  const [completeInspectionOpen, setCompleteInspectionOpen] = React.useState(false);
  const [selectedInspection, setSelectedInspection] = React.useState<FleetTripInspectionDto | null>(null);
  const [selectedInspectionTemplate, setSelectedInspectionTemplate] = React.useState<InspectionTemplateDetail | null>(null);
  const [inspectionAnswers, setInspectionAnswers] = React.useState<Record<string, string>>({});
  const [startInspectionForm, setStartInspectionForm] = React.useState<StartFleetTripInspectionDto>({
    fleetTripId: '',
    inspectionTemplateId: '',
    inspectorEmployeeId: null,
    inspectionKind: 'PreTrip',
  });
  const [completeInspectionForm, setCompleteInspectionForm] = React.useState<CompleteFleetTripInspectionDto>({
    completedAtUtc: null,
    overallResult: 'Pass',
    inspectionData: '{}',
    notes: '',
  });
  const inspectionReadOnly = !!selectedInspection && selectedInspection.status !== 'InProgress';

  const loadInspectionTemplate = React.useCallback(
    async (templateId: string) => {
      if (!templateId) return null;
      const res = await fetch(`${API_BASE_URL}/inspection-templates/${templateId}`, { headers: getAuthHeaders() });
      if (!res.ok) throw new Error(await res.text());
      const t: InspectionTemplateDetail = await res.json();
      t.checklistItems = (t.checklistItems || []).slice().sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
      return t;
    },
    []
  );

  function buildInspectionDataJson(template: InspectionTemplateDetail | null, answers: Record<string, string>) {
    const payload = {
      schema: 'FleetTripInspectionChecklist.v1',
      templateId: template?.id || null,
      templateCode: template?.code || null,
      checklist: (template?.checklistItems || []).map((i) => ({
        id: i.id,
        item: i.item,
        type: i.type,
        required: !!i.required,
        order: i.order,
        value: answers[i.id] ?? '',
      })),
    };
    return JSON.stringify(payload);
  }

  function deriveOverallResult(template: InspectionTemplateDetail | null, answers: Record<string, string>) {
    const items = template?.checklistItems || [];
    const anyFail = items.some((i) => (answers[i.id] || '').toLowerCase() === 'fail');
    return anyFail ? 'Fail' : 'Pass';
  }

  React.useEffect(() => {
    if (!completeInspectionOpen || !selectedInspection) return;
    (async () => {
      try {
        const template = await loadInspectionTemplate(selectedInspection.inspectionTemplateId);
        setSelectedInspectionTemplate(template);

        // Attempt to hydrate answers from stored inspectionData if it matches our schema.
        const nextAnswers: Record<string, string> = {};
        try {
          const raw = selectedInspection.inspectionData || '{}';
          const parsed = JSON.parse(raw);
          if (parsed && parsed.schema === 'FleetTripInspectionChecklist.v1' && Array.isArray(parsed.checklist)) {
            for (const row of parsed.checklist) {
              if (!row?.id) continue;
              nextAnswers[String(row.id)] = row.value != null ? String(row.value) : '';
            }
          }
        } catch {
          // ignore
        }

        setInspectionAnswers(nextAnswers);
        setCompleteInspectionForm((p) => ({
          ...p,
          inspectionData: buildInspectionDataJson(template, nextAnswers),
          overallResult: deriveOverallResult(template, nextAnswers),
        }));
      } catch (e: any) {
        setSelectedInspectionTemplate(null);
        setInspectionAnswers({});
        toast({ title: 'Failed to load inspection template', description: e?.message || String(e), variant: 'destructive' });
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [completeInspectionOpen, selectedInspection?.inspectionTemplateId]);

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

  const [cancelOpen, setCancelOpen] = React.useState(false);
  const [cancelReason, setCancelReason] = React.useState('');

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

      const activeVehicles = (vRes.items || []).filter((v) => (v.status || '').toLowerCase() === 'active');
      setVehicles(activeVehicles);

      if (!eRes.ok) throw new Error(await eRes.text());
      const rawEmployees: EmployeeDto[] = await eRes.json();
      const activeEmployees = (rawEmployees || []).filter((e) => (e.status || '').toLowerCase() !== 'inactive');
      setEmployees(activeEmployees);

      // Inspection templates (for fleet pre/post inspections)
      try {
        const tRes = await fetch(`${API_BASE_URL}/inspection-templates?activeOnly=true`, { headers: getAuthHeaders() });
        if (tRes.ok) {
          const templates: InspectionTemplateLite[] = await tRes.json();
          setInspectionTemplates((templates || []).filter((t) => t.isActive !== false));
        }
      } catch {
        setInspectionTemplates([]);
      }
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

  React.useEffect(() => {
    if (!viewOpen || !selected?.id) return;
    (async () => {
      setInspectionsLoading(true);
      try {
        const res = await fleetService.getTripInspections(selected.id);
        setInspections(res || []);
      } catch {
        setInspections([]);
      } finally {
        setInspectionsLoading(false);
      }
    })();
  }, [viewOpen, selected?.id]);

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
          <DialogContent className="flex max-h-[90vh] w-[95vw] max-w-5xl flex-col overflow-hidden">
            <DialogHeader>
              <DialogTitle>Create Trip</DialogTitle>
              <DialogDescription>Create a draft trip request, then submit for workflow approval.</DialogDescription>
            </DialogHeader>

            <div className="flex-1 overflow-y-auto pr-1">
              <div className="grid grid-cols-1 gap-4 md:grid-cols-6">
                <div className="space-y-2 md:col-span-3">
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

                <div className="space-y-2 md:col-span-3">
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

                <div className="space-y-2 md:col-span-6">
                  <Label>Purpose</Label>
                  <Input value={createForm.purpose || ''} onChange={(e) => setCreateForm((p) => ({ ...p, purpose: e.target.value }))} />
                </div>

                <div className="space-y-2 md:col-span-3">
                  <Label>Origin</Label>
                  <Input value={createForm.origin || ''} onChange={(e) => setCreateForm((p) => ({ ...p, origin: e.target.value }))} />
                </div>

                <div className="space-y-2 md:col-span-3">
                  <Label>Destination</Label>
                  <Input value={createForm.destination || ''} onChange={(e) => setCreateForm((p) => ({ ...p, destination: e.target.value }))} />
                </div>

                <div className="space-y-2 md:col-span-3">
                  <Label>Planned Start</Label>
                  <Input
                    type="datetime-local"
                    value={createForm.plannedStartAt || ''}
                    onChange={(e) => setCreateForm((p) => ({ ...p, plannedStartAt: e.target.value || null }))}
                  />
                </div>

                <div className="space-y-2 md:col-span-3">
                  <Label>Planned End</Label>
                  <Input
                    type="datetime-local"
                    value={createForm.plannedEndAt || ''}
                    onChange={(e) => setCreateForm((p) => ({ ...p, plannedEndAt: e.target.value || null }))}
                  />
                </div>

                <div className="space-y-2 md:col-span-6">
                  <Label>Notes</Label>
                  <Textarea
                    className="min-h-28"
                    value={createForm.notes || ''}
                    onChange={(e) => setCreateForm((p) => ({ ...p, notes: e.target.value }))}
                  />
                </div>
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
        <DialogContent className="flex max-h-[90vh] w-[95vw] max-w-6xl flex-col overflow-hidden">
          <DialogHeader>
            <DialogTitle>Trip Details</DialogTitle>
            <DialogDescription>View trip information, approvals, and dispatch/completion.</DialogDescription>
          </DialogHeader>

          <Tabs defaultValue="details" className="flex flex-1 flex-col overflow-hidden">
            <TabsList className="w-fit">
              <TabsTrigger value="details">Details</TabsTrigger>
              <TabsTrigger value="approvals">Approval History</TabsTrigger>
            </TabsList>

            <TabsContent value="details" className="mt-4 flex-1 overflow-hidden">
              <div className="h-full overflow-y-auto pr-1">
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

                      <Card>
                        <CardHeader className="py-3">
                          <CardTitle className="text-base">Inspections</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-3 pb-4 text-sm">
                          {inspectionsLoading ? (
                            <div className="text-muted-foreground">Loading…</div>
                          ) : (
                            <>
                              {(['PreTrip', 'PostTrip'] as const).map((kind) => {
                                const current = inspections
                                  .filter((i) => i.inspectionKind === kind && i.status !== 'Cancelled')
                                  .sort((a, b) => (a.startedAtUtc < b.startedAtUtc ? 1 : -1))[0];

                                return (
                                  <div key={kind} className="rounded-md border p-3">
                                    <div className="flex items-start justify-between gap-3">
                                      <div>
                                        <div className="font-medium">{kind === 'PreTrip' ? 'Pre-trip' : 'Post-trip'}</div>
                                        <div className="text-muted-foreground">
                                          {current ? `${current.status}${current.overallResult ? ` (${current.overallResult})` : ''}` : 'Not started'}
                                        </div>
                                        {current?.inspectionTemplateName ? (
                                          <div className="text-muted-foreground">Template: {current.inspectionTemplateName}</div>
                                        ) : null}
                                      </div>
                                      <div className="flex gap-2">
                                        <Button
                                          variant="outline"
                                          size="sm"
                                          onClick={() => {
                                            setStartInspectionForm({
                                              fleetTripId: selected.id,
                                              inspectionTemplateId: inspectionTemplates[0]?.id || '',
                                              inspectorEmployeeId: selected.driverEmployeeId || null,
                                              inspectionKind: kind,
                                            });
                                            setStartInspectionOpen(true);
                                          }}
                                          disabled={!inspectionTemplates.length || !!current}
                                        >
                                          Start
                                        </Button>
                                        <Button
                                          variant="outline"
                                          size="sm"
                                          onClick={() => {
                                            if (!current) return;
                                            setSelectedInspection(current);
                                            setCompleteInspectionForm({
                                              completedAtUtc:
                                                current.status === 'InProgress'
                                                  ? toDatetimeLocal(new Date())
                                                  : (current.completedAtUtc ?? null),
                                              overallResult: current.overallResult || 'Pass',
                                              inspectionData: current.inspectionData || '{}',
                                              notes: current.notes || '',
                                            });
                                            setCompleteInspectionOpen(true);
                                          }}
                                          disabled={!current}
                                        >
                                          {current?.status === 'InProgress' ? 'Complete' : 'View'}
                                        </Button>
                                      </div>
                                    </div>
                                  </div>
                                );
                              })}
                              {!inspectionTemplates.length ? (
                                <div className="text-xs text-muted-foreground">
                                  No inspection templates found. Create templates via `api/inspection-templates` (or seed them).
                                </div>
                              ) : null}
                            </>
                          )}
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
                          variant="destructive"
                          disabled={['Dispatched', 'Completed', 'Cancelled'].includes(selected.status)}
                          onClick={() => {
                            setCancelReason('');
                            setCancelOpen(true);
                          }}
                        >
                          Cancel
                        </Button>
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
                  </div>
                )}
              </div>
            </TabsContent>

            <TabsContent value="approvals" className="mt-4 flex-1 overflow-hidden">
              <div className="h-full overflow-y-auto pr-1">
                {!selected ? (
                  <div className="py-10 text-center text-muted-foreground">No trip selected</div>
                ) : (
                  <WorkflowApprovalHistoryPanel entityType="FleetTrip" entityId={selected.id} />
                )}
              </div>
            </TabsContent>
          </Tabs>

          <DialogFooter>
            <Button variant="outline" onClick={() => setViewOpen(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={startInspectionOpen} onOpenChange={setStartInspectionOpen}>
        <DialogContent className="max-w-xl">
          <DialogHeader>
            <DialogTitle>Start Inspection</DialogTitle>
            <DialogDescription>Create a {startInspectionForm.inspectionKind === 'PreTrip' ? 'pre-trip' : 'post-trip'} inspection record.</DialogDescription>
          </DialogHeader>
          <div className="grid grid-cols-1 gap-4">
            <div className="space-y-2">
              <Label>Template</Label>
              <Select
                value={startInspectionForm.inspectionTemplateId || 'none'}
                onValueChange={(v) => setStartInspectionForm((p) => ({ ...p, inspectionTemplateId: v === 'none' ? '' : v }))}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select template" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">Select template</SelectItem>
                  {inspectionTemplates.map((t) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Inspector (optional)</Label>
              <Select
                value={startInspectionForm.inspectorEmployeeId || 'none'}
                onValueChange={(v) => setStartInspectionForm((p) => ({ ...p, inspectorEmployeeId: v === 'none' ? null : v }))}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select employee" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">No inspector</SelectItem>
                  {employees.map((e) => (
                    <SelectItem key={e.id} value={e.id}>
                      {(e.firstName || '').trim()} {(e.lastName || '').trim()} {e.employeeNumber ? `(${e.employeeNumber})` : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setStartInspectionOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={async () => {
                try {
                  await fleetService.startTripInspection(startInspectionForm);
                  setStartInspectionOpen(false);
                  if (selected?.id) setInspections(await fleetService.getTripInspections(selected.id));
                } catch (e: any) {
                  toast({ title: 'Failed to start inspection', description: e?.message || String(e), variant: 'destructive' });
                }
              }}
              disabled={!startInspectionForm.fleetTripId || !startInspectionForm.inspectionTemplateId}
            >
              Start
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={completeInspectionOpen} onOpenChange={setCompleteInspectionOpen}>
        <DialogContent className="flex max-h-[90vh] w-[95vw] max-w-4xl flex-col overflow-hidden">
          <DialogHeader>
            <DialogTitle>Complete Inspection</DialogTitle>
            <DialogDescription>{selectedInspection?.inspectionTemplateName}</DialogDescription>
          </DialogHeader>

          <div className="flex-1 overflow-y-auto pr-1">
          <div className="grid grid-cols-1 gap-4">
            <div className="space-y-2">
              {selectedInspection ? (
                <div className="text-xs text-muted-foreground">
                  Status: {selectedInspection.status}
                  {selectedInspection.startedAtUtc ? ` • Started: ${formatDate(selectedInspection.startedAtUtc)}` : ''}
                  {selectedInspection.completedAtUtc ? ` • Completed: ${formatDate(selectedInspection.completedAtUtc)}` : ''}
                </div>
              ) : null}
              <Label>Overall Result</Label>
              <Select
                value={completeInspectionForm.overallResult}
                onValueChange={(v) => setCompleteInspectionForm((p) => ({ ...p, overallResult: v }))}
                disabled={inspectionReadOnly}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select result" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Pass">Pass</SelectItem>
                  <SelectItem value="Fail">Fail</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Checklist</Label>
              {!selectedInspectionTemplate ? (
                <div className="text-sm text-muted-foreground">Loading template…</div>
              ) : selectedInspectionTemplate.checklistItems.length === 0 ? (
                <div className="rounded-md border p-3 text-sm text-muted-foreground">
                  This template has no checklist items. Configure items in Administration → Maintenance → Inspection Templates.
                </div>
              ) : (
                <div className="space-y-3 rounded-md border p-3">
                  {selectedInspectionTemplate.checklistItems.map((i, idx) => {
                    const type = (i.type || '').toLowerCase();
                    const value = inspectionAnswers[i.id] ?? '';
                    return (
                      <div key={i.id} className="space-y-1">
                        <div className="text-sm font-medium">
                          {idx + 1}. {i.item} {i.required ? <span className="text-destructive">*</span> : null}
                        </div>
                        {type === 'checklist' ? (
                          <Select
                            value={value || 'none'}
                            onValueChange={(v) => {
                              if (inspectionReadOnly) return;
                              const next = { ...inspectionAnswers, [i.id]: v === 'none' ? '' : v };
                              setInspectionAnswers(next);
                              const derived = deriveOverallResult(selectedInspectionTemplate, next);
                              setCompleteInspectionForm((p) => ({
                                ...p,
                                overallResult: derived,
                                inspectionData: buildInspectionDataJson(selectedInspectionTemplate, next),
                              }));
                            }}
                            disabled={inspectionReadOnly}
                          >
                            <SelectTrigger>
                              <SelectValue placeholder="Select result" />
                            </SelectTrigger>
                            <SelectContent>
                              <SelectItem value="none">Select…</SelectItem>
                              <SelectItem value="Pass">Pass</SelectItem>
                              <SelectItem value="Fail">Fail</SelectItem>
                            </SelectContent>
                          </Select>
                        ) : type === 'number' || type === 'measurement' ? (
                          <Input
                            type="number"
                            value={value}
                            onChange={(e) => {
                              if (inspectionReadOnly) return;
                              const next = { ...inspectionAnswers, [i.id]: e.target.value };
                              setInspectionAnswers(next);
                              setCompleteInspectionForm((p) => ({
                                ...p,
                                inspectionData: buildInspectionDataJson(selectedInspectionTemplate, next),
                              }));
                            }}
                            placeholder="Enter value"
                            disabled={inspectionReadOnly}
                          />
                        ) : (
                          <Textarea
                            value={value}
                            onChange={(e) => {
                              if (inspectionReadOnly) return;
                              const next = { ...inspectionAnswers, [i.id]: e.target.value };
                              setInspectionAnswers(next);
                              setCompleteInspectionForm((p) => ({
                                ...p,
                                inspectionData: buildInspectionDataJson(selectedInspectionTemplate, next),
                              }));
                            }}
                            placeholder="Enter response"
                            rows={2}
                            disabled={inspectionReadOnly}
                          />
                        )}
                      </div>
                    );
                  })}
                </div>
              )}
              {selectedInspectionTemplate?.checklistItems?.length ? (
                <div className="text-xs text-muted-foreground">Overall result auto-derives from item results (any Fail → Fail).</div>
              ) : null}
            </div>

            <div className="space-y-2">
              <Label>Notes</Label>
              <Textarea
                value={completeInspectionForm.notes || ''}
                onChange={(e) => setCompleteInspectionForm((p) => ({ ...p, notes: e.target.value }))}
                rows={5}
                disabled={inspectionReadOnly}
              />
            </div>
          </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setCompleteInspectionOpen(false)}>
              {inspectionReadOnly ? 'Close' : 'Cancel'}
            </Button>
            {!inspectionReadOnly ? (
              <Button
                onClick={async () => {
                  if (!selectedInspection) return;
                  try {
                    if (selectedInspectionTemplate?.checklistItems?.length) {
                      const missing = selectedInspectionTemplate.checklistItems
                        .filter((i) => i.required)
                        .filter((i) => {
                          const v = (inspectionAnswers[i.id] || '').trim();
                          return !v;
                        });

                      if (missing.length > 0) {
                        toast({
                          title: 'Checklist incomplete',
                          description: `Please complete required items (${missing.length}) before finishing the inspection.`,
                          variant: 'destructive',
                        });
                        return;
                      }
                    }

                    await fleetService.completeTripInspection(selectedInspection.id, {
                      ...completeInspectionForm,
                      completedAtUtc: completeInspectionForm.completedAtUtc || toDatetimeLocal(new Date()),
                      inspectionData: completeInspectionForm.inspectionData || '{}',
                    });
                    setCompleteInspectionOpen(false);
                    if (selected?.id) setInspections(await fleetService.getTripInspections(selected.id));
                  } catch (e: any) {
                    toast({ title: 'Failed to complete inspection', description: e?.message || String(e), variant: 'destructive' });
                  }
                }}
                disabled={!selectedInspection}
              >
                Complete
              </Button>
            ) : null}
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={cancelOpen} onOpenChange={setCancelOpen}>
        <DialogContent className="max-w-xl">
          <DialogHeader>
            <DialogTitle>Cancel Trip</DialogTitle>
            <DialogDescription>Cancel this trip request before dispatch. This will set the trip status to Cancelled.</DialogDescription>
          </DialogHeader>

          <div className="space-y-2">
            <Label>Reason (optional)</Label>
            <Textarea value={cancelReason} onChange={(e) => setCancelReason(e.target.value)} placeholder="e.g. Trip no longer required" />
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setCancelOpen(false)}>
              Close
            </Button>
            <Button
              variant="destructive"
              disabled={!selected}
              onClick={async () => {
                if (!selected) return;
                try {
                  await fleetService.cancelTrip(selected.id, cancelReason.trim());
                  toast({ title: 'Trip cancelled' });
                  setCancelOpen(false);
                  await refreshSelected();
                  await loadTrips();
                } catch (e: any) {
                  toast({ title: 'Cancel failed', description: e?.message || String(e), variant: 'destructive' });
                }
              }}
            >
              Cancel Trip
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
