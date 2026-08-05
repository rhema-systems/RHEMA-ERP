'use client';

import { useEffect, useMemo, useState } from 'react';
import { AxiosError } from 'axios';
import { History, Loader2, RefreshCw, Save, ShieldCheck } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { inventoryManagementService, InventoryItemDto, WarehouseDto } from '@/services/inventoryManagementService';
import {
  InventoryTraceabilityEvent, InventoryTrackingException, InventoryTrackingRequirements,
  inventoryTrackingControlService, RegisterInventoryTrackingException,
} from '@/services/inventoryTrackingControlService';

const CODES = [
  ['INV_TRACKING_EXPIRED', 'Expired stock'],
  ['INV_TRACKING_MINIMUM_SHELF_LIFE', 'Minimum shelf life'],
  ['INV_TRACKING_METADATA_MISMATCH', 'Lot metadata mismatch'],
  ['INV_TRACKING_FIFO_VIOLATION', 'FIFO sequence'],
] as const;

type Problem = { detail?: string; message?: string; title?: string };
const messageFrom = (error: unknown, fallback: string) => {
  const value = (error as AxiosError<Problem>)?.response?.data;
  return value?.detail || value?.message || value?.title || (error instanceof Error ? error.message : fallback);
};
const initialDraft = (): RegisterInventoryTrackingException => ({
  inventoryItemId: '', warehouseId: '', referenceId: '', referenceType: 'InventoryTransaction', referenceNumber: '',
  exceptionCodes: [], reason: '', workflowInstanceId: '', workflowEvidenceDocumentId: '',
  expiresAtUtc: new Date(Date.now() + 7 * 86400000).toISOString().slice(0, 16),
});

export default function InventoryTrackingControlsPage() {
  const [items, setItems] = useState<InventoryItemDto[]>([]);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [requirements, setRequirements] = useState<InventoryTrackingRequirements>();
  const [events, setEvents] = useState<InventoryTraceabilityEvent[]>([]);
  const [exceptions, setExceptions] = useState<InventoryTrackingException[]>([]);
  const [itemFilter, setItemFilter] = useState('all');
  const [warehouseFilter, setWarehouseFilter] = useState('all');
  const [draft, setDraft] = useState<RegisterInventoryTrackingException>(initialDraft());
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const refresh = async () => {
    setLoading(true);
    try {
      const [loadedItems, loadedWarehouses, loadedExceptions, loadedEvents] = await Promise.all([
        inventoryManagementService.getInventoryItems({ isActive: true }),
        inventoryManagementService.getWarehouses(true),
        inventoryTrackingControlService.getExceptions(),
        inventoryTrackingControlService.getEvents(itemFilter === 'all' ? undefined : itemFilter, warehouseFilter === 'all' ? undefined : warehouseFilter),
      ]);
      setItems(loadedItems);
      setWarehouses(loadedWarehouses);
      setExceptions(loadedExceptions);
      setEvents(loadedEvents);
    } catch (error) {
      toast.error(messageFrom(error, 'Unable to load inventory tracking controls.'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { void refresh(); }, [itemFilter, warehouseFilter]);

  const inspectItem = async (id: string) => {
    setItemFilter(id);
    setDraft(value => ({ ...value, inventoryItemId: id }));
    if (id === 'all') return setRequirements(undefined);
    try { setRequirements(await inventoryTrackingControlService.getRequirements(id)); }
    catch (error) { toast.error(messageFrom(error, 'Unable to resolve effective category rules.')); }
  };

  const toggleCode = (code: string, checked: boolean) => setDraft(value => ({
    ...value,
    exceptionCodes: checked ? [...new Set([...value.exceptionCodes, code])] : value.exceptionCodes.filter(item => item !== code),
  }));

  const register = async () => {
    if (!draft.inventoryItemId || !draft.warehouseId || !draft.referenceId || !draft.referenceNumber ||
      !draft.workflowInstanceId || !draft.workflowEvidenceDocumentId || draft.exceptionCodes.length === 0 || draft.reason.trim().length < 10) {
      toast.error('Complete the item, warehouse, reference, approval evidence, exception reason, and at least one exception code.');
      return;
    }
    setSaving(true);
    try {
      const saved = await inventoryTrackingControlService.registerException({ ...draft, expiresAtUtc: new Date(draft.expiresAtUtc).toISOString() });
      setExceptions(values => [saved, ...values]);
      setDraft(initialDraft());
      setRequirements(undefined);
      toast.success('Approved tracking exception registered with immutable workflow and evidence lineage.');
    } catch (error) {
      toast.error(messageFrom(error, 'Unable to register the tracking exception.'));
    } finally { setSaving(false); }
  };

  const availableExceptions = useMemo(() => exceptions.filter(value => value.isAvailable).length, [exceptions]);
  const ruleBadges = requirements ? [
    requirements.requiresLot && 'Lot', requirements.requiresBatch && 'Batch', requirements.requiresSerial && 'Serial',
    requirements.requiresManufactureDate && 'Manufacture date', requirements.requiresExpiryDate && 'Expiry date',
    requirements.enforcesFifoIssue && 'FIFO', requirements.minimumShelfLifeDays > 0 && `${requirements.minimumShelfLifeDays}d shelf life`,
  ].filter(Boolean) as string[] : [];

  return <div className="space-y-6 p-6">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div><h1 className="text-2xl font-semibold">Inventory Tracking Controls</h1><p className="text-sm text-muted-foreground">Effective category rules, independently approved exceptions, and append-only traceability.</p></div>
      <div className="flex gap-2"><Badge variant="outline">{availableExceptions} available exceptions</Badge><Button variant="outline" onClick={() => void refresh()} disabled={loading}>{loading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <RefreshCw className="mr-2 h-4 w-4" />}Refresh</Button></div>
    </div>
    <Breadcrumb><BreadcrumbList><BreadcrumbItem><BreadcrumbLink href="/inventory">Inventory</BreadcrumbLink></BreadcrumbItem><BreadcrumbSeparator /><BreadcrumbItem><BreadcrumbPage>Tracking controls</BreadcrumbPage></BreadcrumbItem></BreadcrumbList></Breadcrumb>

    <div className="grid gap-4 xl:grid-cols-[minmax(320px,0.75fr)_minmax(0,1.25fr)]">
      <Card><CardHeader><CardTitle>Effective rules</CardTitle><CardDescription>Item settings and every category ancestor are combined fail-closed.</CardDescription></CardHeader><CardContent className="space-y-4">
        <div className="space-y-2"><Label>Inventory item</Label><Select value={itemFilter} onValueChange={value => void inspectItem(value)}><SelectTrigger><SelectValue placeholder="Select an item" /></SelectTrigger><SelectContent><SelectItem value="all">All items</SelectItem>{items.map(item => <SelectItem key={item.id} value={item.id}>{item.itemCode} · {item.name}</SelectItem>)}</SelectContent></Select></div>
        {requirements ? <div className="rounded-md border p-4"><div className="font-medium">{requirements.itemCode} · {requirements.itemName}</div><div className="text-sm text-muted-foreground">{requirements.categoryName} · {requirements.categoryLineage.length} category level(s)</div><div className="mt-3 flex flex-wrap gap-2">{ruleBadges.length ? ruleBadges.map(value => <Badge key={value}>{value}</Badge>) : <Badge variant="secondary">No mandatory tracking</Badge>}</div></div> : <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">Select an item to inspect its effective category policy.</div>}
        <div className="space-y-2"><Label>History warehouse</Label><Select value={warehouseFilter} onValueChange={setWarehouseFilter}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">All allowed warehouses</SelectItem>{warehouses.map(value => <SelectItem key={value.id} value={value.id}>{value.code} · {value.name}</SelectItem>)}</SelectContent></Select></div>
      </CardContent></Card>

      <Card><CardHeader><CardTitle><ShieldCheck className="mr-2 inline h-5 w-5" />Register approved exception</CardTitle><CardDescription>The shared workflow must already be completed by an independent approver and retain current verified malware-clean evidence.</CardDescription></CardHeader><CardContent className="space-y-4">
        <div className="grid gap-3 md:grid-cols-2"><div className="space-y-2"><Label>Item</Label><Select value={draft.inventoryItemId} onValueChange={value => setDraft(current => ({ ...current, inventoryItemId: value }))}><SelectTrigger><SelectValue placeholder="Select item" /></SelectTrigger><SelectContent>{items.map(item => <SelectItem key={item.id} value={item.id}>{item.itemCode} · {item.name}</SelectItem>)}</SelectContent></Select></div><div className="space-y-2"><Label>Warehouse</Label><Select value={draft.warehouseId} onValueChange={value => setDraft(current => ({ ...current, warehouseId: value }))}><SelectTrigger><SelectValue placeholder="Select warehouse" /></SelectTrigger><SelectContent>{warehouses.map(value => <SelectItem key={value.id} value={value.id}>{value.code} · {value.name}</SelectItem>)}</SelectContent></Select></div><div className="space-y-2"><Label>Reference type</Label><Input value={draft.referenceType} onChange={event => setDraft(value => ({ ...value, referenceType: event.target.value }))} /></div><div className="space-y-2"><Label>Reference number</Label><Input value={draft.referenceNumber} onChange={event => setDraft(value => ({ ...value, referenceNumber: event.target.value }))} /></div><div className="space-y-2"><Label>Reference ID</Label><Input value={draft.referenceId} onChange={event => setDraft(value => ({ ...value, referenceId: event.target.value }))} /></div><div className="space-y-2"><Label>Reference line ID</Label><Input value={draft.referenceLineId || ''} onChange={event => setDraft(value => ({ ...value, referenceLineId: event.target.value || undefined }))} /></div><div className="space-y-2"><Label>Workflow instance ID</Label><Input value={draft.workflowInstanceId} onChange={event => setDraft(value => ({ ...value, workflowInstanceId: event.target.value }))} /></div><div className="space-y-2"><Label>Verified evidence document ID</Label><Input value={draft.workflowEvidenceDocumentId} onChange={event => setDraft(value => ({ ...value, workflowEvidenceDocumentId: event.target.value }))} /></div><div className="space-y-2"><Label>Lot</Label><Input value={draft.lotNumber || ''} onChange={event => setDraft(value => ({ ...value, lotNumber: event.target.value || undefined }))} /></div><div className="space-y-2"><Label>Batch / serial</Label><div className="flex gap-2"><Input placeholder="Batch" value={draft.batchNumber || ''} onChange={event => setDraft(value => ({ ...value, batchNumber: event.target.value || undefined }))} /><Input placeholder="Serial" value={draft.serialNumber || ''} onChange={event => setDraft(value => ({ ...value, serialNumber: event.target.value || undefined }))} /></div></div><div className="space-y-2"><Label>Expires</Label><Input type="datetime-local" value={draft.expiresAtUtc} onChange={event => setDraft(value => ({ ...value, expiresAtUtc: event.target.value }))} /></div></div>
        <div className="grid gap-2 md:grid-cols-2">{CODES.map(([code, label]) => <label key={code} className="flex items-center gap-2 rounded-md border p-3 text-sm"><Checkbox checked={draft.exceptionCodes.includes(code)} onCheckedChange={value => toggleCode(code, value === true)} /><span><span className="font-medium">{label}</span><span className="block font-mono text-xs text-muted-foreground">{code}</span></span></label>)}</div>
        <div className="space-y-2"><Label>Reason</Label><Textarea value={draft.reason} onChange={event => setDraft(value => ({ ...value, reason: event.target.value }))} placeholder="Explain the approved operational exception." /></div>
        <Button onClick={() => void register()} disabled={saving}>{saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}Register exception</Button>
      </CardContent></Card>
    </div>

    <Card><CardHeader><CardTitle><History className="mr-2 inline h-5 w-5" />Traceability history</CardTitle><CardDescription>Receipts, issues, returns, transfers, and approved exception consumption are retained by item and warehouse.</CardDescription></CardHeader><CardContent><div className="overflow-auto rounded-md border"><Table><TableHeader><TableRow><TableHead>When</TableHead><TableHead>Item / warehouse</TableHead><TableHead>Direction</TableHead><TableHead>Quantity</TableHead><TableHead>Tracking</TableHead><TableHead>Reference</TableHead><TableHead>Exception</TableHead></TableRow></TableHeader><TableBody>{events.length === 0 ? <TableRow><TableCell colSpan={7} className="h-28 text-center text-muted-foreground">No traceability events match the selected scope.</TableCell></TableRow> : events.map(value => <TableRow key={value.id}><TableCell className="whitespace-nowrap text-xs">{new Date(value.occurredAtUtc).toLocaleString()}</TableCell><TableCell><div className="font-medium">{value.itemCode}</div><div className="text-xs text-muted-foreground">{value.warehouseName}</div></TableCell><TableCell><Badge variant={['Receipt','Return','TransferIn','AdjustmentIn'].includes(value.direction) ? 'secondary' : 'outline'}>{value.direction}</Badge></TableCell><TableCell>{value.quantity}</TableCell><TableCell className="text-xs">{[value.lotNumber && `Lot ${value.lotNumber}`, value.batchNumber && `Batch ${value.batchNumber}`, value.serialNumber && `SN ${value.serialNumber}`, value.expiryDate && `Exp ${new Date(value.expiryDate).toLocaleDateString()}`].filter(Boolean).join(' · ') || 'Untracked'}</TableCell><TableCell><div>{value.referenceNumber}</div><div className="text-xs text-muted-foreground">{value.referenceType}</div></TableCell><TableCell>{value.trackingExceptionId ? <Badge variant="destructive">Approved</Badge> : '—'}</TableCell></TableRow>)}</TableBody></Table></div></CardContent></Card>

    <Card><CardHeader><CardTitle>Exception register</CardTitle><CardDescription>Availability is terminal: an exception expires or is consumed against its exact transaction scope.</CardDescription></CardHeader><CardContent><div className="overflow-auto rounded-md border"><Table><TableHeader><TableRow><TableHead>Status</TableHead><TableHead>Item / warehouse</TableHead><TableHead>Reference</TableHead><TableHead>Controls</TableHead><TableHead>Approved / expires</TableHead></TableRow></TableHeader><TableBody>{exceptions.length === 0 ? <TableRow><TableCell colSpan={5} className="h-24 text-center text-muted-foreground">No approved tracking exceptions.</TableCell></TableRow> : exceptions.map(value => <TableRow key={value.id}><TableCell><Badge variant={value.isAvailable ? 'secondary' : 'outline'}>{value.isAvailable ? 'Available' : value.consumedAtUtc ? 'Consumed' : 'Expired'}</Badge></TableCell><TableCell><div className="font-medium">{value.itemCode}</div><div className="text-xs text-muted-foreground">{value.warehouseName}</div></TableCell><TableCell>{value.referenceNumber}<div className="text-xs text-muted-foreground">{value.referenceType}</div></TableCell><TableCell className="text-xs">{value.exceptionCodes.join(' · ')}</TableCell><TableCell className="text-xs">{new Date(value.approvedAtUtc).toLocaleString()}<div className="text-muted-foreground">Expires {new Date(value.expiresAtUtc).toLocaleString()}</div></TableCell></TableRow>)}</TableBody></Table></div></CardContent></Card>
  </div>;
}
