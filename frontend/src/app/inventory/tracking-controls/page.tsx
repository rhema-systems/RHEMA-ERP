'use client';

import { useEffect, useMemo, useState } from 'react';
import { AxiosError } from 'axios';
import { History, Loader2, RefreshCw, ShieldCheck } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { inventoryManagementService, InventoryItemDto, WarehouseDto } from '@/services/inventoryManagementService';
import {
  InventoryTraceabilityEvent, InventoryTrackingException, InventoryTrackingRequirements,
  inventoryTrackingControlService,
} from '@/services/inventoryTrackingControlService';

type Problem = { detail?: string; message?: string; title?: string };
const messageFrom = (error: unknown, fallback: string) => {
  const value = (error as AxiosError<Problem>)?.response?.data;
  return value?.detail || value?.message || value?.title || (error instanceof Error ? error.message : fallback);
};
export default function InventoryTrackingControlsPage() {
  const [items, setItems] = useState<InventoryItemDto[]>([]);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [requirements, setRequirements] = useState<InventoryTrackingRequirements>();
  const [events, setEvents] = useState<InventoryTraceabilityEvent[]>([]);
  const [exceptions, setExceptions] = useState<InventoryTrackingException[]>([]);
  const [itemFilter, setItemFilter] = useState('all');
  const [warehouseFilter, setWarehouseFilter] = useState('all');
  const [loading, setLoading] = useState(true);

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
    if (id === 'all') return setRequirements(undefined);
    try { setRequirements(await inventoryTrackingControlService.getRequirements(id)); }
    catch (error) { toast.error(messageFrom(error, 'Unable to resolve effective category rules.')); }
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

      <Card><CardHeader><CardTitle><ShieldCheck className="mr-2 inline h-5 w-5" />Controlled exception initiation</CardTitle><CardDescription>Manual transaction, workflow, and evidence identifiers are not accepted from end users.</CardDescription></CardHeader><CardContent><p className="text-sm text-muted-foreground">Provide the required lot, batch, serial, manufacture, or expiry data in the normal transaction. Where a tracking exception is configured, initiate it from that source transaction so the system supplies its scope and retains the independent approval and central-DMS evidence automatically.</p></CardContent></Card>
    </div>

    <Card><CardHeader><CardTitle><History className="mr-2 inline h-5 w-5" />Traceability history</CardTitle><CardDescription>Receipts, issues, returns, transfers, and approved exception consumption are retained by item and warehouse.</CardDescription></CardHeader><CardContent><div className="overflow-auto rounded-md border"><Table><TableHeader><TableRow><TableHead>When</TableHead><TableHead>Item / warehouse</TableHead><TableHead>Direction</TableHead><TableHead>Quantity</TableHead><TableHead>Tracking</TableHead><TableHead>Reference</TableHead><TableHead>Exception</TableHead></TableRow></TableHeader><TableBody>{events.length === 0 ? <TableRow><TableCell colSpan={7} className="h-28 text-center text-muted-foreground">No traceability events match the selected scope.</TableCell></TableRow> : events.map(value => <TableRow key={value.id}><TableCell className="whitespace-nowrap text-xs">{new Date(value.occurredAtUtc).toLocaleString()}</TableCell><TableCell><div className="font-medium">{value.itemCode}</div><div className="text-xs text-muted-foreground">{value.warehouseName}</div></TableCell><TableCell><Badge variant={['Receipt','Return','TransferIn','AdjustmentIn'].includes(value.direction) ? 'secondary' : 'outline'}>{value.direction}</Badge></TableCell><TableCell>{value.quantity}</TableCell><TableCell className="text-xs">{[value.lotNumber && `Lot ${value.lotNumber}`, value.batchNumber && `Batch ${value.batchNumber}`, value.serialNumber && `SN ${value.serialNumber}`, value.expiryDate && `Exp ${new Date(value.expiryDate).toLocaleDateString()}`].filter(Boolean).join(' · ') || 'Untracked'}</TableCell><TableCell><div>{value.referenceNumber}</div><div className="text-xs text-muted-foreground">{value.referenceType}</div></TableCell><TableCell>{value.trackingExceptionId ? <Badge variant="destructive">Approved</Badge> : '—'}</TableCell></TableRow>)}</TableBody></Table></div></CardContent></Card>

    <Card><CardHeader><CardTitle>Exception register</CardTitle><CardDescription>Availability is terminal: an exception expires or is consumed against its exact transaction scope.</CardDescription></CardHeader><CardContent><div className="overflow-auto rounded-md border"><Table><TableHeader><TableRow><TableHead>Status</TableHead><TableHead>Item / warehouse</TableHead><TableHead>Reference</TableHead><TableHead>Controls</TableHead><TableHead>Approved / expires</TableHead></TableRow></TableHeader><TableBody>{exceptions.length === 0 ? <TableRow><TableCell colSpan={5} className="h-24 text-center text-muted-foreground">No approved tracking exceptions.</TableCell></TableRow> : exceptions.map(value => <TableRow key={value.id}><TableCell><Badge variant={value.isAvailable ? 'secondary' : 'outline'}>{value.isAvailable ? 'Available' : value.consumedAtUtc ? 'Consumed' : 'Expired'}</Badge></TableCell><TableCell><div className="font-medium">{value.itemCode}</div><div className="text-xs text-muted-foreground">{value.warehouseName}</div></TableCell><TableCell>{value.referenceNumber}<div className="text-xs text-muted-foreground">{value.referenceType}</div></TableCell><TableCell className="text-xs">{value.exceptionCodes.join(' · ')}</TableCell><TableCell className="text-xs">{new Date(value.approvedAtUtc).toLocaleString()}<div className="text-muted-foreground">Expires {new Date(value.expiresAtUtc).toLocaleString()}</div></TableCell></TableRow>)}</TableBody></Table></div></CardContent></Card>
  </div>;
}
