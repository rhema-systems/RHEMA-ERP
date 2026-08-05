'use client';

import { useEffect, useMemo, useState } from 'react';
import { AxiosError } from 'axios';
import { Bell, History, Loader2, RefreshCw, Replace, Save, ShieldCheck, Undo2 } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { inventoryManagementService, InventoryItemDto, WarehouseLocationDto } from '@/services/inventoryManagementService';
import { inventoryRequisitionService, InventoryRequisitionDetailDto, InventoryRequisitionDto } from '@/services/inventoryRequisitionService';
import { inventoryProjectReservationService, InventoryProjectReservation } from '@/services/inventoryProjectReservationService';

type Problem = { detail?: string; message?: string; title?: string };
const messageFrom = (error: unknown, fallback: string) => {
  const value = (error as AxiosError<Problem>)?.response?.data;
  return value?.detail || value?.message || value?.title || (error instanceof Error ? error.message : fallback);
};
const statusLabels: Record<number, string> = { 1: 'Reserved', 2: 'Partially fulfilled', 3: 'Fulfilled', 4: 'Released', 5: 'Expired', 6: 'Substituted' };
const actionLabels: Record<number, string> = { ...statusLabels, 7: 'Notification created' };
const newKey = (prefix: string) => `${prefix}:${crypto.randomUUID()}`;

export default function InventoryProjectReservationsPage() {
  const [reservations, setReservations] = useState<InventoryProjectReservation[]>([]);
  const [requisitions, setRequisitions] = useState<InventoryRequisitionDto[]>([]);
  const [requisition, setRequisition] = useState<InventoryRequisitionDetailDto>();
  const [items, setItems] = useState<InventoryItemDto[]>([]);
  const [locations, setLocations] = useState<WarehouseLocationDto[]>([]);
  const [selected, setSelected] = useState<InventoryProjectReservation>();
  const [projectFilter, setProjectFilter] = useState('all');
  const [departmentFilter, setDepartmentFilter] = useState('all');
  const [statusFilter, setStatusFilter] = useState('all');
  const [requisitionId, setRequisitionId] = useState('');
  const [lineId, setLineId] = useState('');
  const [locationId, setLocationId] = useState('');
  const [quantity, setQuantity] = useState(1);
  const [expiresAtUtc, setExpiresAtUtc] = useState(new Date(Date.now() + 7 * 86400000).toISOString().slice(0, 16));
  const [notes, setNotes] = useState('');
  const [releaseQuantity, setReleaseQuantity] = useState(1);
  const [releaseReason, setReleaseReason] = useState('');
  const [replacementItemId, setReplacementItemId] = useState('');
  const [substituteReason, setSubstituteReason] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const refresh = async () => {
    setLoading(true);
    try {
      const [register, pending, inventoryItems] = await Promise.all([
        inventoryProjectReservationService.getAll({ take: 500 }),
        inventoryRequisitionService.getPendingIssue(),
        inventoryManagementService.getInventoryItems({ isActive: true }),
      ]);
      setReservations(register);
      setRequisitions(pending.filter(value => value.projectId && [3, 4, 5].includes(value.status)));
      setItems(inventoryItems);
      if (selected) {
        const detail = await inventoryProjectReservationService.getById(selected.id);
        setSelected(detail);
      }
    } catch (error) { toast.error(messageFrom(error, 'Unable to load project reservations.')); }
    finally { setLoading(false); }
  };
  useEffect(() => { void refresh(); }, []);

  const chooseRequisition = async (id: string) => {
    setRequisitionId(id); setLineId(''); setLocationId(''); setRequisition(undefined);
    try {
      const detail = await inventoryRequisitionService.getById(id);
      const warehouseLocations = await inventoryManagementService.getWarehouseLocations(detail.warehouseId);
      setRequisition(detail); setLocations(warehouseLocations.filter(value => value.isActive));
    } catch (error) { toast.error(messageFrom(error, 'Unable to load the approved requisition.')); }
  };

  const filtered = useMemo(() => reservations.filter(value =>
    (projectFilter === 'all' || value.projectId === projectFilter) &&
    (departmentFilter === 'all' || value.departmentId === departmentFilter) &&
    (statusFilter === 'all' || value.status === Number(statusFilter))),
  [reservations, projectFilter, departmentFilter, statusFilter]);
  const projects = useMemo(() => Array.from(new Map(reservations.map(value => [value.projectId, `${value.projectCode} · ${value.projectTitle}`])).entries()), [reservations]);
  const departments = useMemo(() => Array.from(new Map(reservations.map(value => [value.departmentId, value.departmentName])).entries()), [reservations]);
  const chosenLine = requisition?.items.find(value => value.id === lineId);

  const reserve = async () => {
    if (!lineId || !locationId || quantity <= 0 || !expiresAtUtc) { toast.error('Select an approved project line, exact active location, positive quantity, and future expiry.'); return; }
    setSaving(true);
    try {
      const value = await inventoryProjectReservationService.reserve({ inventoryRequisitionItemId: lineId, locationId,
        quantity, expiresAtUtc: new Date(expiresAtUtc).toISOString(), idempotencyKey: newKey('reserve'), notes: notes || undefined });
      setReservations(current => [value, ...current]); setSelected(value); setLineId(''); setNotes('');
      toast.success('Project stock reserved on the authoritative inventory allocation ledger.');
    } catch (error) { toast.error(messageFrom(error, 'Unable to reserve project stock.')); }
    finally { setSaving(false); }
  };

  const openDetail = async (value: InventoryProjectReservation) => {
    try { setSelected(await inventoryProjectReservationService.getById(value.id)); setReleaseQuantity(value.remainingQuantity); }
    catch (error) { toast.error(messageFrom(error, 'Unable to load reservation history.')); }
  };

  const release = async () => {
    if (!selected || releaseQuantity <= 0 || releaseReason.trim().length < 5) { toast.error('Enter a positive release quantity and reason.'); return; }
    setSaving(true);
    try {
      const value = await inventoryProjectReservationService.release(selected.id, { quantity: releaseQuantity,
        reason: releaseReason, idempotencyKey: newKey('release'), rowVersion: selected.rowVersion });
      setReservations(current => current.map(item => item.id === value.id ? value : item)); setSelected(value); setReleaseReason('');
      toast.success('Reserved stock released.');
    } catch (error) { toast.error(messageFrom(error, 'Unable to release reserved stock.')); }
    finally { setSaving(false); }
  };

  const substitute = async () => {
    if (!selected || !replacementItemId || substituteReason.trim().length < 5) { toast.error('Select a replacement item and record the substitution reason.'); return; }
    setSaving(true);
    try {
      const value = await inventoryProjectReservationService.substitute(selected.id, { replacementInventoryItemId,
        reason: substituteReason, idempotencyKey: newKey('substitute'), rowVersion: selected.rowVersion });
      setReservations(current => [value, ...current.map(item => item.id === selected.id ? { ...item, status: 6, remainingQuantity: 0 } : item)]);
      setSelected(value); setReplacementItemId(''); setSubstituteReason('');
      toast.success('Pre-fulfillment substitution completed atomically.');
    } catch (error) { toast.error(messageFrom(error, 'Unable to substitute reserved stock.')); }
    finally { setSaving(false); }
  };

  const active = reservations.filter(value => value.status === 1 || value.status === 2);
  return <div className="space-y-6 p-6">
    <div className="flex flex-wrap items-center justify-between gap-3"><div><h1 className="text-2xl font-semibold">Project Inventory Reservations</h1><p className="text-sm text-muted-foreground">Reserve approved project demand, fulfill through Store Issue, and notify the responsible project and department actors.</p></div><Button variant="outline" onClick={() => void refresh()} disabled={loading}>{loading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <RefreshCw className="mr-2 h-4 w-4" />}Refresh</Button></div>
    <Breadcrumb><BreadcrumbList><BreadcrumbItem><BreadcrumbLink href="/inventory">Inventory</BreadcrumbLink></BreadcrumbItem><BreadcrumbSeparator /><BreadcrumbItem><BreadcrumbPage>Project reservations</BreadcrumbPage></BreadcrumbItem></BreadcrumbList></Breadcrumb>

    <div className="grid gap-4 md:grid-cols-3"><Card><CardHeader className="pb-2"><CardDescription>Open reservations</CardDescription><CardTitle>{active.length}</CardTitle></CardHeader></Card><Card><CardHeader className="pb-2"><CardDescription>Quantity held</CardDescription><CardTitle>{active.reduce((sum, value) => sum + value.remainingQuantity, 0).toLocaleString()}</CardTitle></CardHeader></Card><Card><CardHeader className="pb-2"><CardDescription>Notifications recorded</CardDescription><CardTitle>{reservations.reduce((sum, value) => sum + value.actions.filter(action => action.actionType === 7).length, 0)}</CardTitle></CardHeader></Card></div>

    <Card><CardHeader><CardTitle><ShieldCheck className="mr-2 inline h-5 w-5" />Shared-control boundary</CardTitle><CardDescription>`InventoryAllocation` remains the authoritative reservation ledger. Issue, cancellation and expiry compose the existing stock transaction, scheduler, audit/control-event and central notification owners; this page does not create another stock, workflow, DMS, notification or project-cost engine.</CardDescription></CardHeader></Card>

    <Card><CardHeader><CardTitle>Reserve approved project demand</CardTitle><CardDescription>Only remaining approved quantities at an exact authorized warehouse location can be held.</CardDescription></CardHeader><CardContent className="space-y-4"><div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div className="space-y-2"><Label>Approved project requisition</Label><Select value={requisitionId} onValueChange={value => void chooseRequisition(value)}><SelectTrigger><SelectValue placeholder="Select requisition" /></SelectTrigger><SelectContent>{requisitions.map(value => <SelectItem key={value.id} value={value.id}>{value.requisitionNumber} · {value.projectCode}</SelectItem>)}</SelectContent></Select></div>
      <div className="space-y-2"><Label>Approved line</Label><Select value={lineId} onValueChange={setLineId} disabled={!requisition}><SelectTrigger><SelectValue placeholder="Select item line" /></SelectTrigger><SelectContent>{requisition?.items.filter(value => value.approvedQuantity > value.issuedQuantity).map(value => <SelectItem key={value.id} value={value.id}>{value.itemCode} · {value.approvedQuantity - value.issuedQuantity} remaining</SelectItem>)}</SelectContent></Select></div>
      <div className="space-y-2"><Label>Exact stock location</Label><Select value={locationId} onValueChange={setLocationId} disabled={!requisition}><SelectTrigger><SelectValue placeholder="Select location" /></SelectTrigger><SelectContent>{locations.map(value => <SelectItem key={value.id} value={value.id}>{value.locationCode} · {value.name}</SelectItem>)}</SelectContent></Select></div>
      <div className="space-y-2"><Label>Quantity</Label><Input type="number" min="0.0001" step="0.0001" max={chosenLine ? chosenLine.approvedQuantity - chosenLine.issuedQuantity : undefined} value={quantity} onChange={event => setQuantity(Number(event.target.value))} /></div>
      <div className="space-y-2"><Label>Expires</Label><Input type="datetime-local" value={expiresAtUtc} onChange={event => setExpiresAtUtc(event.target.value)} /></div>
      <div className="space-y-2 md:col-span-2 xl:col-span-3"><Label>Reservation notes</Label><Input value={notes} onChange={event => setNotes(event.target.value)} placeholder="Project delivery or material-hold context" /></div>
    </div><Button onClick={() => void reserve()} disabled={saving}>{saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}Reserve stock</Button></CardContent></Card>

    <Card><CardHeader><CardTitle>Reservation register</CardTitle><CardDescription>Filterable project/department history with live remaining quantity and terminal lineage.</CardDescription></CardHeader><CardContent className="space-y-4"><div className="grid gap-3 md:grid-cols-3"><Select value={projectFilter} onValueChange={setProjectFilter}><SelectTrigger><SelectValue placeholder="All projects" /></SelectTrigger><SelectContent><SelectItem value="all">All projects</SelectItem>{projects.map(([id, label]) => <SelectItem key={id} value={id}>{label}</SelectItem>)}</SelectContent></Select><Select value={departmentFilter} onValueChange={setDepartmentFilter}><SelectTrigger><SelectValue placeholder="All departments" /></SelectTrigger><SelectContent><SelectItem value="all">All departments</SelectItem>{departments.map(([id, label]) => <SelectItem key={id} value={id}>{label || id}</SelectItem>)}</SelectContent></Select><Select value={statusFilter} onValueChange={setStatusFilter}><SelectTrigger><SelectValue placeholder="All statuses" /></SelectTrigger><SelectContent><SelectItem value="all">All statuses</SelectItem>{Object.entries(statusLabels).map(([id, label]) => <SelectItem key={id} value={id}>{label}</SelectItem>)}</SelectContent></Select></div>
      <div className="overflow-auto rounded-md border"><Table><TableHeader><TableRow><TableHead>Status</TableHead><TableHead>Project / department</TableHead><TableHead>Requisition / item</TableHead><TableHead>Reserved</TableHead><TableHead>Fulfilled</TableHead><TableHead>Remaining</TableHead><TableHead>Expires</TableHead><TableHead /></TableRow></TableHeader><TableBody>{filtered.length === 0 ? <TableRow><TableCell colSpan={8} className="h-24 text-center text-muted-foreground">No project reservations match the filters.</TableCell></TableRow> : filtered.map(value => <TableRow key={value.id}><TableCell><Badge variant={value.status <= 2 ? 'secondary' : 'outline'}>{statusLabels[value.status]}</Badge></TableCell><TableCell><div className="font-medium">{value.projectCode} · {value.projectTitle}</div><div className="text-xs text-muted-foreground">{value.departmentName}</div></TableCell><TableCell><div>{value.requisitionNumber}</div><div className="text-xs text-muted-foreground">{value.itemCode} · {value.itemName}</div></TableCell><TableCell>{value.reservedQuantity}</TableCell><TableCell>{value.fulfilledQuantity}</TableCell><TableCell className="font-medium">{value.remainingQuantity}</TableCell><TableCell className="whitespace-nowrap text-xs">{new Date(value.expiresAtUtc).toLocaleString()}</TableCell><TableCell><Button size="sm" variant="outline" onClick={() => void openDetail(value)}><History className="mr-2 h-4 w-4" />History</Button></TableCell></TableRow>)}</TableBody></Table></div>
    </CardContent></Card>

    {selected && <Card><CardHeader><CardTitle>{selected.requisitionNumber} · {selected.itemCode}</CardTitle><CardDescription>Immutable action chain, notification delivery, release and pre-fulfillment substitution.</CardDescription></CardHeader><CardContent className="space-y-6"><div className="grid gap-4 lg:grid-cols-2"><div className="space-y-3 rounded-md border p-4"><h3 className="font-medium"><Undo2 className="mr-2 inline h-4 w-4" />Release reservation</h3><Input type="number" min="0.0001" max={selected.remainingQuantity} step="0.0001" value={releaseQuantity} onChange={event => setReleaseQuantity(Number(event.target.value))} /><Textarea value={releaseReason} onChange={event => setReleaseReason(event.target.value)} placeholder="Controlled release reason" /><Button variant="outline" onClick={() => void release()} disabled={saving || selected.status > 2}>Release</Button></div><div className="space-y-3 rounded-md border p-4"><h3 className="font-medium"><Replace className="mr-2 inline h-4 w-4" />Substitute before fulfillment</h3><Select value={replacementItemId} onValueChange={setReplacementItemId}><SelectTrigger><SelectValue placeholder="Replacement active item" /></SelectTrigger><SelectContent>{items.filter(value => value.id !== selected.inventoryItemId).map(value => <SelectItem key={value.id} value={value.id}>{value.itemCode} · {value.name}</SelectItem>)}</SelectContent></Select><Textarea value={substituteReason} onChange={event => setSubstituteReason(event.target.value)} placeholder="Substitution reason" /><Button variant="outline" onClick={() => void substitute()} disabled={saving || selected.status !== 1 || selected.fulfilledQuantity > 0}>Substitute</Button></div></div>
      <div className="grid gap-4 lg:grid-cols-2"><div><h3 className="mb-2 font-medium"><History className="mr-2 inline h-4 w-4" />Action history</h3><div className="space-y-2">{selected.actions.map(action => <div key={action.id} className="rounded-md border p-3 text-sm"><div className="flex justify-between gap-3"><span className="font-medium">#{action.sequence} {actionLabels[action.actionType]}</span><span className="text-xs text-muted-foreground">{new Date(action.occurredAtUtc).toLocaleString()}</span></div><div className="text-xs text-muted-foreground">{action.actorName} · qty {action.quantity} · {action.integrityHash.slice(0, 12)}…</div>{action.reason && <p className="mt-1">{action.reason}</p>}</div>)}</div></div><div><h3 className="mb-2 font-medium"><Bell className="mr-2 inline h-4 w-4" />Central notifications</h3><div className="space-y-2">{selected.notifications.length === 0 ? <p className="text-sm text-muted-foreground">No notification records.</p> : selected.notifications.map(value => <div key={value.id} className="rounded-md border p-3 text-sm"><div className="font-medium">{value.recipientName} · {value.title}</div><p>{value.message}</p><div className="text-xs text-muted-foreground">{value.status} · {new Date(value.scheduledFor).toLocaleString()}</div></div>)}</div></div></div>
    </CardContent></Card>}
  </div>;
}
