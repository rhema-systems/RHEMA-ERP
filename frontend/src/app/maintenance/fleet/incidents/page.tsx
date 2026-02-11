'use client';

import React from 'react';
import Link from 'next/link';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';

import MaintenanceAttachmentsPanel from '@/components/maintenance/MaintenanceAttachmentsPanel';
import { maintenanceApiService } from '@/services/maintenanceApiService';
import {
  fleetService,
  CreateFleetIncidentDto,
  CreateWorkOrderFromFleetIncidentDto,
  EmployeeDto,
  FleetIncidentDto,
  FleetVehicleListDto,
} from '@/services/fleetService';
import { formatFleetDateTime } from '@/lib/date-format';

type BillingType = 'Maintenance' | 'Repairs';

function toDateTimeLocalValue(iso?: string | null) {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function parseNullableNumber(value: string) {
  const v = value.trim();
  if (!v) return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
}

function normalizeIncidentDto(input: CreateFleetIncidentDto): CreateFleetIncidentDto {
  return {
    ...input,
    incidentType: (input.incidentType || 'Incident').trim(),
    severity: (input.severity || 'Medium').trim(),
    status: (input.status || 'Open').trim(),
    title: (input.title || '').trim(),
    description: input.description?.trim() || null,
    location: input.location?.trim() || null,
    damageAssessment: input.damageAssessment?.trim() || null,
    currencyCode: input.currencyCode?.trim() || null,
    insuranceCompany: input.insuranceCompany?.trim() || null,
    policyNumber: input.policyNumber?.trim() || null,
    claimNumber: input.claimNumber?.trim() || null,
    claimStatus: input.claimStatus?.trim() || null,
  };
}

export default function FleetIncidentsPage() {
  const { toast } = useToast();

  const [vehicles, setVehicles] = React.useState<FleetVehicleListDto[]>([]);
  const [employees, setEmployees] = React.useState<EmployeeDto[]>([]);

  const [vehicleId, setVehicleId] = React.useState<string>('all');
  const [status, setStatus] = React.useState<string>('all');
  const [searchTerm, setSearchTerm] = React.useState<string>('');

  const [items, setItems] = React.useState<FleetIncidentDto[]>([]);
  const [loading, setLoading] = React.useState(false);

  const [formOpen, setFormOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<FleetIncidentDto | null>(null);

  const [docsOpen, setDocsOpen] = React.useState(false);
  const [docsItem, setDocsItem] = React.useState<FleetIncidentDto | null>(null);

  const [woOpen, setWoOpen] = React.useState(false);
  const [woItem, setWoItem] = React.useState<FleetIncidentDto | null>(null);
  const [workOrderTypes, setWorkOrderTypes] = React.useState<any[]>([]);
  const [maintenanceTypes, setMaintenanceTypes] = React.useState<any[]>([]);
  const [priorityLevels, setPriorityLevels] = React.useState<any[]>([]);
  const [woForm, setWoForm] = React.useState<CreateWorkOrderFromFleetIncidentDto>({
    incidentId: '',
    workOrderTypeId: '',
    maintenanceTypeId: '',
    priorityLevelId: '',
    billingType: 'Repairs',
  });

  const [form, setForm] = React.useState<CreateFleetIncidentDto>({
    vehicleAssetId: '',
    fleetTripId: null,
    driverEmployeeId: null,
    occurredAtUtc: null,
    incidentType: 'Incident',
    title: '',
    description: null,
    location: null,
    severity: 'Medium',
    status: 'Open',
    damageAssessment: null,
    estimatedRepairCost: null,
    actualRepairCost: null,
    currencyCode: null,
    insuranceCompany: null,
    policyNumber: null,
    claimNumber: null,
    claimStatus: null,
    claimAmount: null,
    claimSubmittedAtUtc: null,
    claimSettledAtUtc: null,
  });

  const loadLookups = React.useCallback(async () => {
    try {
      const [vehiclesRes, empRes] = await Promise.all([
        fleetService.getVehicles({ page: 1, pageSize: 200 }),
        fleetService.getEmployees({ page: 1, pageSize: 200 }),
      ]);
      setVehicles(vehiclesRes.items || []);
      setEmployees(empRes || []);
    } catch (e: any) {
      toast({ title: 'Failed to load lookups', description: e?.message || String(e), variant: 'destructive' });
    }
  }, [toast]);

  const load = React.useCallback(async () => {
    setLoading(true);
    try {
      const res = await fleetService.getIncidents({
        page: 1,
        pageSize: 100,
        vehicleAssetId: vehicleId === 'all' ? undefined : vehicleId,
        status: status === 'all' ? undefined : status,
        searchTerm: searchTerm || undefined,
      });
      setItems(res.items || []);
    } catch (e: any) {
      toast({ title: 'Failed to load incidents', description: e?.message || String(e), variant: 'destructive' });
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [vehicleId, status, searchTerm, toast]);

  React.useEffect(() => {
    loadLookups();
  }, [loadLookups]);

  React.useEffect(() => {
    load();
  }, [load]);

  const openCreate = () => {
    const selectedVehicleId = vehicleId !== 'all' ? vehicleId : vehicles[0]?.id || '';
    setEditing(null);
    setForm({
      vehicleAssetId: selectedVehicleId,
      fleetTripId: null,
      driverEmployeeId: null,
      occurredAtUtc: new Date().toISOString(),
      incidentType: 'Incident',
      title: '',
      description: null,
      location: null,
      severity: 'Medium',
      status: 'Open',
      damageAssessment: null,
      estimatedRepairCost: null,
      actualRepairCost: null,
      currencyCode: null,
      insuranceCompany: null,
      policyNumber: null,
      claimNumber: null,
      claimStatus: null,
      claimAmount: null,
      claimSubmittedAtUtc: null,
      claimSettledAtUtc: null,
    });
    setFormOpen(true);
  };

  const openEdit = (i: FleetIncidentDto) => {
    setEditing(i);
    setForm({
      vehicleAssetId: i.vehicleAssetId,
      fleetTripId: i.fleetTripId || null,
      driverEmployeeId: i.driverEmployeeId || null,
      occurredAtUtc: i.occurredAtUtc,
      incidentType: i.incidentType || 'Incident',
      title: i.title,
      description: i.description || null,
      location: i.location || null,
      severity: i.severity || 'Medium',
      status: i.status || 'Open',
      damageAssessment: i.damageAssessment || null,
      estimatedRepairCost: i.estimatedRepairCost ?? null,
      actualRepairCost: i.actualRepairCost ?? null,
      currencyCode: i.currencyCode || null,
      insuranceCompany: i.insuranceCompany || null,
      policyNumber: i.policyNumber || null,
      claimNumber: i.claimNumber || null,
      claimStatus: i.claimStatus || null,
      claimAmount: i.claimAmount ?? null,
      claimSubmittedAtUtc: i.claimSubmittedAtUtc || null,
      claimSettledAtUtc: i.claimSettledAtUtc || null,
    });
    setFormOpen(true);
  };

  const save = async () => {
    try {
      if (!form.vehicleAssetId) throw new Error('Vehicle is required');
      const dto = normalizeIncidentDto(form);
      if (!dto.title) throw new Error('Title is required');

      if (!editing) {
        await fleetService.createIncident(dto);
        toast({ title: 'Incident created' });
      } else {
        await fleetService.updateIncident(editing.id, dto);
        toast({ title: 'Incident updated' });
      }
      setFormOpen(false);
      setEditing(null);
      await load();
    } catch (e: any) {
      toast({ title: 'Save failed', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  const openDocs = (i: FleetIncidentDto) => {
    setDocsItem(i);
    setDocsOpen(true);
  };

  const openWorkOrder = async (i: FleetIncidentDto) => {
    setWoItem(i);
    setWoForm({
      incidentId: i.id,
      workOrderTypeId: '',
      maintenanceTypeId: '',
      priorityLevelId: '',
      billingType: 'Repairs',
    });
    setWoOpen(true);
    try {
      const [types, mtypes, pri] = await Promise.all([
        maintenanceApiService.getWorkOrderTypes(),
        maintenanceApiService.getMaintenanceTypes(),
        maintenanceApiService.getPriorityLevels(),
      ]);
      setWorkOrderTypes(types ?? []);
      setMaintenanceTypes(mtypes ?? []);
      setPriorityLevels(pri ?? []);
    } catch (e: any) {
      toast({ title: 'Failed to load work order lookups', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  const createWorkOrder = async () => {
    if (!woItem) return;
    if (!woForm.workOrderTypeId || !woForm.maintenanceTypeId || !woForm.priorityLevelId) {
      toast({ title: 'Work order type, maintenance type and priority are required', variant: 'destructive' });
      return;
    }
    try {
      const res = await fleetService.createWorkOrderFromIncident(woForm);
      toast({ title: 'Work order created' });
      setWoOpen(false);
      await load();
      if (res?.workOrderId) window.open(`/maintenance/work-orders?id=${res.workOrderId}`, '_blank');
    } catch (e: any) {
      toast({ title: 'Failed to create work order', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  return (
    <div className="space-y-6 p-6">
      <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Fleet Incidents</h1>
          <p className="text-muted-foreground">Accident/incident reporting with claim tracking and repair links.</p>
        </div>

        <div className="flex flex-col gap-3 md:flex-row">
          <div className="w-full md:w-[280px]">
            <Label>Vehicle</Label>
            <Select value={vehicleId} onValueChange={setVehicleId}>
              <SelectTrigger>
                <SelectValue placeholder="All vehicles" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All vehicles</SelectItem>
                {vehicles.map((v) => (
                  <SelectItem key={v.id} value={v.id}>
                    {v.assetNumber} — {v.name}
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
                <SelectItem value="Open">Open</SelectItem>
                <SelectItem value="InProgress">In Progress</SelectItem>
                <SelectItem value="Closed">Closed</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <div className="w-full md:w-[260px]">
            <Label>Search</Label>
            <Input value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} placeholder="Title/location/claim..." />
          </div>

          <Button onClick={openCreate}>New Incident</Button>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Incidents</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow className="bg-muted/40">
                  <TableHead>Occurred</TableHead>
                  <TableHead>Vehicle</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Title</TableHead>
                  <TableHead>Severity</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Claim</TableHead>
                  <TableHead>WO</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {loading ? (
                  <TableRow>
                    <TableCell colSpan={9} className="py-8 text-center text-muted-foreground">
                      Loading...
                    </TableCell>
                  </TableRow>
                ) : items.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={9} className="py-8 text-center text-muted-foreground">
                      No incidents found.
                    </TableCell>
                  </TableRow>
                ) : (
                  items.map((i, idx) => (
                    <TableRow key={i.id} className={idx % 2 === 1 ? 'bg-muted/10 hover:bg-muted/30' : 'hover:bg-muted/30'}>
                      <TableCell>{formatFleetDateTime(i.occurredAtUtc)}</TableCell>
                      <TableCell className="font-medium">{i.vehicleName}</TableCell>
                      <TableCell>{i.incidentType}</TableCell>
                      <TableCell className="max-w-[320px] truncate" title={i.title}>
                        {i.title}
                      </TableCell>
                      <TableCell>
                        <Badge variant={i.severity === 'Critical' || i.severity === 'High' ? 'destructive' : 'secondary'}>{i.severity}</Badge>
                      </TableCell>
                      <TableCell>
                        <Badge variant={i.status === 'Closed' ? 'outline' : 'secondary'}>{i.status}</Badge>
                      </TableCell>
                      <TableCell>{i.claimStatus ? <Badge variant="secondary">{i.claimStatus}</Badge> : <span className="text-muted-foreground">—</span>}</TableCell>
                      <TableCell>
                        {i.workOrderId ? (
                          <Link className="text-primary underline" href={`/maintenance/work-orders?id=${i.workOrderId}`}>
                            View
                          </Link>
                        ) : (
                          <span className="text-muted-foreground">—</span>
                        )}
                      </TableCell>
                      <TableCell className="text-right">
                        <Button variant="ghost" size="sm" onClick={() => openEdit(i)}>
                          Edit
                        </Button>
                        <Button variant="ghost" size="sm" onClick={() => openDocs(i)}>
                          Documents
                        </Button>
                        {!i.workOrderId ? (
                          <Button variant="ghost" size="sm" onClick={() => openWorkOrder(i)}>
                            Create WO
                          </Button>
                        ) : null}
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <Dialog
        open={formOpen}
        onOpenChange={(o) => {
          setFormOpen(o);
          if (!o) setEditing(null);
        }}
      >
        <DialogContent className="max-w-5xl">
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit Incident' : 'New Incident'}</DialogTitle>
            <DialogDescription>Use Documents to upload photos, reports, and claim evidence.</DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
            <div className="space-y-2">
              <Label>Vehicle</Label>
              <Select value={form.vehicleAssetId || undefined} onValueChange={(v) => setForm((p) => ({ ...p, vehicleAssetId: v }))}>
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
              <Label>Occurred At</Label>
              <Input
                type="datetime-local"
                value={toDateTimeLocalValue(form.occurredAtUtc)}
                onChange={(e) => setForm((p) => ({ ...p, occurredAtUtc: e.target.value ? new Date(e.target.value).toISOString() : null }))}
              />
            </div>

            <div className="space-y-2">
              <Label>Driver (optional)</Label>
              <Select value={form.driverEmployeeId || undefined} onValueChange={(v) => setForm((p) => ({ ...p, driverEmployeeId: v }))}>
                <SelectTrigger>
                  <SelectValue placeholder="Select driver" />
                </SelectTrigger>
                <SelectContent>
                  {employees.map((e) => (
                    <SelectItem key={e.id} value={e.id}>
                      {e.firstName} {e.lastName} ({e.employeeNumber})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Type</Label>
              <Select value={form.incidentType} onValueChange={(v) => setForm((p) => ({ ...p, incidentType: v }))}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Incident">Incident</SelectItem>
                  <SelectItem value="Accident">Accident</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Severity</Label>
              <Select value={form.severity} onValueChange={(v) => setForm((p) => ({ ...p, severity: v }))}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Low">Low</SelectItem>
                  <SelectItem value="Medium">Medium</SelectItem>
                  <SelectItem value="High">High</SelectItem>
                  <SelectItem value="Critical">Critical</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Status</Label>
              <Select value={form.status} onValueChange={(v) => setForm((p) => ({ ...p, status: v }))}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Open">Open</SelectItem>
                  <SelectItem value="InProgress">In Progress</SelectItem>
                  <SelectItem value="Closed">Closed</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2 md:col-span-3">
              <Label>Title</Label>
              <Input value={form.title} onChange={(e) => setForm((p) => ({ ...p, title: e.target.value }))} />
            </div>

            <div className="space-y-2 md:col-span-2">
              <Label>Location</Label>
              <Input value={form.location || ''} onChange={(e) => setForm((p) => ({ ...p, location: e.target.value }))} />
            </div>

            <div className="space-y-2">
              <Label>Currency</Label>
              <Input value={form.currencyCode || ''} onChange={(e) => setForm((p) => ({ ...p, currencyCode: e.target.value }))} placeholder="e.g. USD" />
            </div>

            <div className="space-y-2 md:col-span-3">
              <Label>Description</Label>
              <Textarea value={form.description || ''} onChange={(e) => setForm((p) => ({ ...p, description: e.target.value }))} rows={3} />
            </div>

            <div className="space-y-2 md:col-span-3">
              <Label>Damage Assessment</Label>
              <Textarea value={form.damageAssessment || ''} onChange={(e) => setForm((p) => ({ ...p, damageAssessment: e.target.value }))} rows={2} />
            </div>

            <div className="space-y-2">
              <Label>Estimated Repair Cost</Label>
              <Input
                type="number"
                value={form.estimatedRepairCost ?? ''}
                onChange={(e) => setForm((p) => ({ ...p, estimatedRepairCost: parseNullableNumber(e.target.value) }))}
              />
            </div>

            <div className="space-y-2">
              <Label>Actual Repair Cost</Label>
              <Input
                type="number"
                value={form.actualRepairCost ?? ''}
                onChange={(e) => setForm((p) => ({ ...p, actualRepairCost: parseNullableNumber(e.target.value) }))}
              />
            </div>

            <div className="space-y-2">
              <Label>Claim Status</Label>
              <Select value={form.claimStatus || undefined} onValueChange={(v) => setForm((p) => ({ ...p, claimStatus: v }))}>
                <SelectTrigger>
                  <SelectValue placeholder="None" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="None">None</SelectItem>
                  <SelectItem value="Submitted">Submitted</SelectItem>
                  <SelectItem value="Approved">Approved</SelectItem>
                  <SelectItem value="Rejected">Rejected</SelectItem>
                  <SelectItem value="Settled">Settled</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2 md:col-span-2">
              <Label>Insurance Company</Label>
              <Input value={form.insuranceCompany || ''} onChange={(e) => setForm((p) => ({ ...p, insuranceCompany: e.target.value }))} />
            </div>

            <div className="space-y-2">
              <Label>Policy Number</Label>
              <Input value={form.policyNumber || ''} onChange={(e) => setForm((p) => ({ ...p, policyNumber: e.target.value }))} />
            </div>

            <div className="space-y-2">
              <Label>Claim Number</Label>
              <Input value={form.claimNumber || ''} onChange={(e) => setForm((p) => ({ ...p, claimNumber: e.target.value }))} />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setFormOpen(false)}>
              Cancel
            </Button>
            <Button onClick={save}>Save</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={docsOpen}
        onOpenChange={(o) => {
          setDocsOpen(o);
          if (!o) setDocsItem(null);
        }}
      >
        <DialogContent className="max-w-5xl">
          <DialogHeader>
            <DialogTitle>Incident Documents</DialogTitle>
            <DialogDescription>{docsItem ? `${docsItem.title} — ${formatFleetDateTime(docsItem.occurredAtUtc)}` : 'Upload and view documents.'}</DialogDescription>
          </DialogHeader>

          {docsItem ? (
            <MaintenanceAttachmentsPanel
              entityType="FleetIncident"
              entityId={docsItem.id}
              category="IncidentDocs"
              title="Documents"
              description="Upload photos, reports, legal/police documents, and insurance correspondence."
            />
          ) : (
            <div className="py-6 text-sm text-muted-foreground">Select an incident to view documents.</div>
          )}

          <DialogFooter>
            <Button variant="outline" onClick={() => setDocsOpen(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={woOpen}
        onOpenChange={(o) => {
          setWoOpen(o);
          if (!o) setWoItem(null);
        }}
      >
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>Create Work Order</DialogTitle>
            <DialogDescription>Work orders created from incidents are intended for Repairs.</DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label>Work Order Type</Label>
              <Select value={woForm.workOrderTypeId} onValueChange={(v) => setWoForm((p) => ({ ...p, workOrderTypeId: v }))}>
                <SelectTrigger>
                  <SelectValue placeholder="Select" />
                </SelectTrigger>
                <SelectContent>
                  {workOrderTypes.map((t: any) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Maintenance Type</Label>
              <Select value={woForm.maintenanceTypeId} onValueChange={(v) => setWoForm((p) => ({ ...p, maintenanceTypeId: v }))}>
                <SelectTrigger>
                  <SelectValue placeholder="Select" />
                </SelectTrigger>
                <SelectContent>
                  {maintenanceTypes.map((t: any) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Priority</Label>
              <Select value={woForm.priorityLevelId} onValueChange={(v) => setWoForm((p) => ({ ...p, priorityLevelId: v }))}>
                <SelectTrigger>
                  <SelectValue placeholder="Select" />
                </SelectTrigger>
                <SelectContent>
                  {priorityLevels.map((t: any) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Billing Type</Label>
              <Select value={(woForm.billingType as BillingType) || 'Repairs'} onValueChange={(v: BillingType) => setWoForm((p) => ({ ...p, billingType: v }))}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Repairs">Repairs</SelectItem>
                  <SelectItem value="Maintenance">Maintenance</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setWoOpen(false)}>
              Cancel
            </Button>
            <Button onClick={createWorkOrder}>Create</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

