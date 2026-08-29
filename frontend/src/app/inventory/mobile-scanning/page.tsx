'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { AxiosError } from 'axios';
import { QRCodeSVG } from 'qrcode.react';
import { Barcode, CheckCircle2, CloudOff, History, Loader2, Plus, Printer, RefreshCw, Save, ScanLine, Trash2, Wifi } from 'lucide-react';
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Code128Barcode } from '@/components/inventory/Code128Barcode';
import { InventoryCameraScanner } from '@/components/inventory/InventoryCameraScanner';
import {
  InventoryTrackingExceptionSelect,
  useAvailableInventoryTrackingExceptions,
} from '@/components/inventory/InventoryTrackingExceptionSelect';
import { useTenant } from '@/contexts/TenantContext';
import { useAuth } from '@/hooks/use-auth';
import { authService } from '@/services/auth';
import { printQrLabel } from '@/lib/print-qr-label';
import {
  flushInventoryScanQueue, listQueuedInventoryScanBatches, QueuedInventoryScanBatch, queueInventoryScanBatch,
  removeQueuedInventoryScanBatch,
} from '@/lib/inventory-scan-offline-queue';
import {
  getInventoryScanDeviceId, InventoryLabelCandidate, InventoryLabelPrint, InventoryLabelProfile,
  InventoryScanBatch, InventoryScanDocumentContext, InventoryScanDocumentSummary, InventoryScanInput,
  InventoryScanOperation, inventoryScanningService, SaveInventoryLabelProfile, SynchronizeInventoryScanBatch,
} from '@/services/inventoryScanningService';
import {
  inventoryManagementService,
  type WarehouseLocationDto,
} from '@/services/inventoryManagementService';

const OPERATIONS = [
  [InventoryScanOperation.GoodsReceipt, 'Goods receipt'],
  [InventoryScanOperation.RequisitionIssue, 'Requisition issue'],
  [InventoryScanOperation.RequisitionReturn, 'Requisition return'],
  [InventoryScanOperation.TransferShipment, 'Transfer shipment'],
  [InventoryScanOperation.TransferReceipt, 'Transfer receipt'],
  [InventoryScanOperation.PhysicalCount, 'Physical count'],
] as const;

const emptyProfile = (): SaveInventoryLabelProfile => ({
  name: 'Standard inventory label', description: '', symbology: 'QR', widthMm: 60, heightMm: 40, dpi: 203,
  includeItemCode: true, includeItemName: true, includeUnit: true, includeLot: false, includeSerial: false,
  includeExpiry: false, isDefault: false, isActive: true,
});

type Problem = { detail?: string; title?: string; code?: string; extensions?: { code?: string } };
const errorMessage = (error: unknown, fallback: string) => {
  const problem = (error as AxiosError<Problem>)?.response?.data;
  const detail = problem?.detail || problem?.title || (error instanceof Error ? error.message : fallback);
  const code = problem?.code || problem?.extensions?.code;
  return code ? `${detail} (${code})` : detail;
};

export default function InventoryMobileScanningPage() {
  const { user: queriedUser } = useAuth();
  const user = queriedUser ?? authService.getStoredUser();
  const { currentTenant } = useTenant();
  const [online, setOnline] = useState(true);
  const [operation, setOperation] = useState(InventoryScanOperation.GoodsReceipt);
  const [documents, setDocuments] = useState<InventoryScanDocumentSummary[]>([]);
  const [documentId, setDocumentId] = useState('');
  const [context, setContext] = useState<InventoryScanDocumentContext>();
  const [identifier, setIdentifier] = useState('');
  const [quantity, setQuantity] = useState('1');
  const [documentLineId, setDocumentLineId] = useState('auto');
  const [locationId, setLocationId] = useState('');
  const [locationIdentifier, setLocationIdentifier] = useState('');
  const [scanTarget, setScanTarget] = useState<'item' | 'location' | 'lot' | 'batch' | 'serial'>('item');
  const [lotNumber, setLotNumber] = useState('');
  const [batchNumber, setBatchNumber] = useState('');
  const [serialNumber, setSerialNumber] = useState('');
  const [manufactureDate, setManufactureDate] = useState('');
  const [expiryDate, setExpiryDate] = useState('');
  const [trackingExceptionId, setTrackingExceptionId] = useState('');
  const [warehouseLocations, setWarehouseLocations] = useState<WarehouseLocationDto[]>([]);
  const [loadingLocations, setLoadingLocations] = useState(false);
  const [lines, setLines] = useState<InventoryScanInput[]>([]);
  const [applyTransaction, setApplyTransaction] = useState(false);
  const [pendingCount, setPendingCount] = useState(0);
  const [queuedBatches, setQueuedBatches] = useState<QueuedInventoryScanBatch[]>([]);
  const [loadingDocuments, setLoadingDocuments] = useState(false);
  const [synchronizing, setSynchronizing] = useState(false);
  const [batches, setBatches] = useState<InventoryScanBatch[]>([]);
  const [profiles, setProfiles] = useState<InventoryLabelProfile[]>([]);
  const [profileDraft, setProfileDraft] = useState<SaveInventoryLabelProfile>(emptyProfile());
  const [editingProfileId, setEditingProfileId] = useState<string>();
  const [labelQuery, setLabelQuery] = useState('');
  const [candidates, setCandidates] = useState<InventoryLabelCandidate[]>([]);
  const [candidate, setCandidate] = useState<InventoryLabelCandidate>();
  const [labelCount, setLabelCount] = useState('1');
  const [printerName, setPrinterName] = useState('Browser print');
  const [prints, setPrints] = useState<InventoryLabelPrint[]>([]);
  const [labelBusy, setLabelBusy] = useState(false);
  const [discardTarget, setDiscardTarget] = useState<QueuedInventoryScanBatch>();
  const [discarding, setDiscarding] = useState(false);
  const labelRef = useRef<HTMLDivElement>(null);
  const flushInFlight = useRef(false);
  const trackingExceptions = useAvailableInventoryTrackingExceptions(Boolean(context));
  const selectedDocumentLine = useMemo(() => !context
    ? undefined
    : documentLineId === 'auto'
      ? context.lines.length === 1 ? context.lines[0] : undefined
      : context.lines.find(line => line.documentLineId === documentLineId), [context, documentLineId]);
  const queueScope = useMemo(() => {
    const tenantId = currentTenant?.id || user?.currentTenantId || user?.tenantId;
    return user?.id && tenantId ? { actorUserId: user.id, tenantId } : undefined;
  }, [currentTenant?.id, user?.currentTenantId, user?.id, user?.tenantId]);

  const refreshPending = useCallback(async () => {
    if (!queueScope) {
      setQueuedBatches([]);
      setPendingCount(0);
      return;
    }
    const queued = await listQueuedInventoryScanBatches(queueScope);
    setQueuedBatches(queued);
    setPendingCount(queued.length);
  }, [queueScope]);
  const refreshHistory = useCallback(async () => {
    if (!navigator.onLine) return;
    const [loadedBatches, loadedPrints] = await Promise.all([inventoryScanningService.getRecentBatches(), inventoryScanningService.getRecentPrints()]);
    setBatches(loadedBatches);
    setPrints(loadedPrints);
  }, []);

  const confirmDiscardQueuedBatch = useCallback(async () => {
    if (!queueScope || !discardTarget) return false;
    setDiscarding(true);
    try {
      await removeQueuedInventoryScanBatch(queueScope, discardTarget.idempotencyKey);
      await refreshPending();
      toast.success('The failed scan batch was discarded. Later queued work can now synchronize.');
      setDiscardTarget(undefined);
      return true;
    } catch (error) {
      console.error('Failed to discard queued inventory scan batch:', error);
      toast.error(errorMessage(error, 'The failed scan batch could not be discarded.'));
      return false;
    } finally {
      setDiscarding(false);
    }
  }, [discardTarget, queueScope, refreshPending]);

  const flushQueue = useCallback(async () => {
    if (!navigator.onLine || flushInFlight.current || !queueScope) return;
    flushInFlight.current = true;
    try {
      const result = await flushInventoryScanQueue(queueScope, inventoryScanningService.synchronize);
      await refreshPending();
      if (result.completed.length) {
        toast.success(`Synchronized ${result.completed.length} queued scan batch${result.completed.length === 1 ? '' : 'es'}.`);
        await refreshHistory();
      }
      if (result.failed.length) {
        toast.error(`Queued batch ${result.failed[0].idempotencyKey} needs attention: ${result.failed[0].lastError}`);
      }
    } finally {
      flushInFlight.current = false;
    }
  }, [queueScope, refreshHistory, refreshPending]);

  useEffect(() => {
    const update = () => {
      setOnline(navigator.onLine);
      if (navigator.onLine) void flushQueue();
    };
    update();
    void refreshPending();
    window.addEventListener('online', update);
    window.addEventListener('offline', update);
    return () => { window.removeEventListener('online', update); window.removeEventListener('offline', update); };
  }, [flushQueue, refreshPending]);

  useEffect(() => {
    if (!online) return;
    setLoadingDocuments(true);
    setDocuments([]);
    setDocumentId('');
    setContext(undefined);
    setLines([]);
    inventoryScanningService.getDocuments(operation).then(setDocuments)
      .catch(error => toast.error(errorMessage(error, 'Unable to load scan transactions.')))
      .finally(() => setLoadingDocuments(false));
  }, [operation, online]);

  useEffect(() => {
    if (!documentId || !online) return;
    inventoryScanningService.getDocumentContext(operation, documentId).then(value => {
      setContext(value);
      setLines([]);
      setDocumentLineId('auto');
      setLocationId('');
      setLocationIdentifier('');
      setTrackingExceptionId('');
    }).catch(error => toast.error(errorMessage(error, 'Unable to load transaction lines.')));
  }, [documentId, online, operation]);

  useEffect(() => {
    if (!context?.warehouseId || !online) {
      setWarehouseLocations([]);
      setLoadingLocations(false);
      return;
    }
    let active = true;
    setLoadingLocations(true);
    inventoryManagementService.getWarehouseLocations(context.warehouseId)
      .then(values => {
        if (active) setWarehouseLocations(values.filter(location => location.isActive));
      })
      .catch(error => {
        if (active) {
          setWarehouseLocations([]);
          toast.error(errorMessage(error, 'Unable to load the transaction warehouse locations.'));
        }
      })
      .finally(() => { if (active) setLoadingLocations(false); });
    return () => { active = false; };
  }, [context?.warehouseId, online]);

  useEffect(() => {
    if (!online) return;
    Promise.all([inventoryScanningService.getLabelProfiles(), inventoryScanningService.searchLabelCandidates(), refreshHistory()])
      .then(([loadedProfiles, loadedCandidates]) => { setProfiles(loadedProfiles); setCandidates(loadedCandidates); })
      .catch(error => toast.error(errorMessage(error, 'Unable to load mobile scanning controls.')));
  }, [online, refreshHistory]);

  const addScan = useCallback((captured?: string) => {
    const value = (captured ?? identifier).trim();
    const parsedQuantity = Number(quantity);
    if (!context) return toast.error('Select a transaction before scanning.');
    if (!value) return toast.error('Enter or scan a value.');
    if (scanTarget !== 'item') {
      if (scanTarget === 'location') setLocationIdentifier(value);
      if (scanTarget === 'lot') setLotNumber(value);
      if (scanTarget === 'batch') setBatchNumber(value);
      if (scanTarget === 'serial') setSerialNumber(value);
      setIdentifier('');
      toast.success(`Captured ${scanTarget} ${value}.`);
      return;
    }
    if (!Number.isFinite(parsedQuantity) || parsedQuantity <= 0) return toast.error('Quantity must be greater than zero.');
    if (operation === InventoryScanOperation.PhysicalCount && !selectedDocumentLine?.rowVersion)
      return toast.error('Select the physical-count document line so its concurrency version is retained for offline synchronization.');
    setLines(current => [...current, {
      clientLineId: crypto.randomUUID(), rawIdentifier: value,
      documentLineId: selectedDocumentLine?.documentLineId,
      documentLineRowVersion: selectedDocumentLine?.rowVersion,
      quantity: parsedQuantity, locationId: locationId || undefined, locationIdentifier: locationIdentifier || undefined,
      lotNumber: lotNumber || undefined, batchNumber: batchNumber || undefined, serialNumber: serialNumber || undefined,
      manufactureDate: manufactureDate || undefined, expiryDate: expiryDate || undefined,
      inventoryTrackingExceptionId: trackingExceptionId || undefined, scannedAtUtc: new Date().toISOString(),
    }]);
    setIdentifier('');
    setSerialNumber('');
    if (captured) toast.success(`Captured ${value}.`);
  }, [batchNumber, context, expiryDate, identifier, locationId, locationIdentifier, lotNumber, manufactureDate, operation, quantity, scanTarget, selectedDocumentLine, serialNumber, trackingExceptionId]);

  const sync = async () => {
    if (!context || lines.length === 0) return;
    if (!queueScope) return toast.error('Your authenticated tenant session is required before scan work can be saved.');
    const request: SynchronizeInventoryScanBatch = {
      deviceId: getInventoryScanDeviceId(), idempotencyKey: crypto.randomUUID(), operation,
      documentId: context.documentId, warehouseId: context.warehouseId, applyTransaction, lines,
    };
    setSynchronizing(true);
    try {
      if (!navigator.onLine) {
        await queueInventoryScanBatch(queueScope, request, 'Offline capture');
        toast.success('Scan batch saved offline. It will synchronize with the same idempotency key when connectivity returns.');
      } else {
        const result = await inventoryScanningService.synchronize(request);
        toast.success(result.applyTransaction ? `Applied ${result.documentReference} once and reconciled stock/audit.` : `Captured ${result.documentReference} scan evidence.`);
        await refreshHistory();
      }
      setLines([]);
      setApplyTransaction(false);
      await refreshPending();
    } catch (error) {
      if (!navigator.onLine || !(error as AxiosError)?.response) {
        await queueInventoryScanBatch(queueScope, request, errorMessage(error, 'Network unavailable'));
        toast.warning('Network interrupted. The batch is safely queued for idempotent retry.');
        setLines([]);
        await refreshPending();
      } else toast.error(errorMessage(error, 'Unable to synchronize the scan batch.'));
    } finally { setSynchronizing(false); }
  };

  const selectedProfile = profiles.find(value => value.id === editingProfileId) || profiles.find(value => value.isDefault) || profiles[0];
  const previewProfile = editingProfileId ? profileDraft : selectedProfile || profileDraft;
  const filteredCandidates = useMemo(() => {
    const term = labelQuery.trim().toLowerCase();
    return candidates.filter(value => !term || [value.itemCode, value.itemName, value.identifier, value.unitCode].some(field => field?.toLowerCase().includes(term)));
  }, [candidates, labelQuery]);

  const editProfile = (profile?: InventoryLabelProfile) => {
    if (!profile) { setEditingProfileId(undefined); setProfileDraft(emptyProfile()); return; }
    const { id, rowVersion, ...values } = profile;
    setEditingProfileId(id);
    setProfileDraft({ ...values, rowVersion });
  };

  const saveProfile = async () => {
    setLabelBusy(true);
    try {
      const saved = editingProfileId
        ? await inventoryScanningService.updateLabelProfile(editingProfileId, profileDraft)
        : await inventoryScanningService.createLabelProfile(profileDraft);
      const loaded = await inventoryScanningService.getLabelProfiles();
      setProfiles(loaded);
      editProfile(saved);
      toast.success('Label profile saved with audit history.');
    } catch (error) { toast.error(errorMessage(error, 'Unable to save the label profile.')); }
    finally { setLabelBusy(false); }
  };

  const printLabel = async () => {
    if (!selectedProfile || !candidate || !online) return;
    const count = Number(labelCount);
    if (!Number.isInteger(count) || count < 1 || count > 1000) return toast.error('Label count must be between 1 and 1000.');
    setLabelBusy(true);
    try {
      await inventoryScanningService.recordLabelPrint({
        labelProfileId: selectedProfile.id, inventoryItemId: candidate.inventoryItemId, unitOfMeasureId: candidate.unitOfMeasureId,
        identifier: candidate.identifier, identifierKind: candidate.identifierKind, labelCount: count,
        lotNumber: lotNumber || undefined, serialNumber: serialNumber || undefined, printerName,
      });
      if (!printQrLabel(labelRef.current, `${candidate.itemCode} label`, count)) throw new Error('The browser blocked the print window.');
      toast.success(`Recorded and opened ${count} label${count === 1 ? '' : 's'} for printing.`);
      await refreshHistory();
    } catch (error) { toast.error(errorMessage(error, 'Unable to print the label.')); }
    finally { setLabelBusy(false); }
  };

  return (
    <div className="space-y-6 pb-10">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div><h1 className="text-3xl font-bold tracking-tight">Inventory labels & mobile scanning</h1><p className="text-muted-foreground">Design governed labels and capture receipt, issue, return, transfer, and count transactions online or offline.</p></div>
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant={online ? 'secondary' : 'destructive'}>{online ? <Wifi className="mr-1 h-3 w-3" /> : <CloudOff className="mr-1 h-3 w-3" />}{online ? 'Online' : 'Offline'}</Badge>
          <Badge variant={pendingCount ? 'outline' : 'secondary'}>{pendingCount} queued</Badge>
          <Button variant="outline" onClick={() => void flushQueue()} disabled={!online || pendingCount === 0}><RefreshCw className="mr-2 h-4 w-4" />Sync queue</Button>
        </div>
      </div>
      <Breadcrumb><BreadcrumbList><BreadcrumbItem><BreadcrumbLink href="/inventory">Inventory</BreadcrumbLink></BreadcrumbItem><BreadcrumbSeparator /><BreadcrumbItem><BreadcrumbPage>Labels & mobile scanning</BreadcrumbPage></BreadcrumbItem></BreadcrumbList></Breadcrumb>

      <Tabs defaultValue="scan" className="space-y-4">
        <TabsList className="grid w-full max-w-xl grid-cols-3"><TabsTrigger value="scan"><ScanLine className="mr-2 h-4 w-4" />Scan</TabsTrigger><TabsTrigger value="labels"><Barcode className="mr-2 h-4 w-4" />Labels</TabsTrigger><TabsTrigger value="history"><History className="mr-2 h-4 w-4" />History</TabsTrigger></TabsList>
        <TabsContent value="scan" className="space-y-4">
          <div className="grid gap-4 xl:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)]">
            <Card><CardHeader><CardTitle>Transaction context</CardTitle><CardDescription>Only documents and warehouses allowed by your active TDC stores assignment are shown.</CardDescription></CardHeader><CardContent className="space-y-4">
              <div className="space-y-2"><Label>Operation</Label><Select value={String(operation)} onValueChange={value => setOperation(Number(value) as InventoryScanOperation)}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{OPERATIONS.map(([value, label]) => <SelectItem key={value} value={String(value)}>{label}</SelectItem>)}</SelectContent></Select></div>
              <div className="space-y-2"><Label>Transaction</Label><Select value={documentId} onValueChange={setDocumentId} disabled={!online || loadingDocuments}><SelectTrigger><SelectValue placeholder={loadingDocuments ? 'Loading…' : 'Select a transaction'} /></SelectTrigger><SelectContent>{documents.map(value => <SelectItem key={value.documentId} value={value.documentId}>{value.documentReference} · {value.warehouseName}</SelectItem>)}</SelectContent></Select></div>
              {context && <div className="rounded-md border p-3 text-sm"><div className="flex items-center justify-between"><span className="font-semibold">{context.documentReference}</span><Badge variant="outline">{context.status}</Badge></div><p className="mt-1 text-muted-foreground">{context.warehouseName} · {context.lines.length} lines</p></div>}
              <div className="space-y-2"><Label>Camera scan target</Label><Select value={scanTarget} onValueChange={value => setScanTarget(value as typeof scanTarget)}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="item">Item identifier</SelectItem><SelectItem value="location">Location / bin</SelectItem><SelectItem value="lot">Lot</SelectItem><SelectItem value="batch">Batch</SelectItem><SelectItem value="serial">Serial</SelectItem></SelectContent></Select></div>
              <InventoryCameraScanner onScan={value => addScan(value)} />
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-2 sm:col-span-2"><Label>Manual / handheld input</Label><div className="flex gap-2"><Input value={identifier} onChange={event => setIdentifier(event.target.value)} onKeyDown={event => event.key === 'Enter' && addScan()} placeholder="Scan or enter barcode / QR" /><Button onClick={() => addScan()}><Plus className="h-4 w-4" /></Button></div></div>
                <div className="space-y-2"><Label>Document line</Label><Select value={documentLineId} onValueChange={setDocumentLineId}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="auto">Resolve automatically</SelectItem>{context?.lines.map(line => <SelectItem key={line.documentLineId} value={line.documentLineId}>{line.itemCode} · {line.itemName}</SelectItem>)}</SelectContent></Select></div>
                <div className="space-y-2"><Label>Scanned quantity</Label><Input type="number" min="0.00000001" step="any" value={quantity} onChange={event => setQuantity(event.target.value)} /></div>
                <div className="space-y-2"><Label>Warehouse location</Label><Select value={locationId || '__none__'} onValueChange={value => setLocationId(value === '__none__' ? '' : value)} disabled={!context || loadingLocations}><SelectTrigger><SelectValue placeholder={loadingLocations ? 'Loading locations…' : 'Select a location'} /></SelectTrigger><SelectContent><SelectItem value="__none__">Resolve from scanned location code</SelectItem>{warehouseLocations.map(location => <SelectItem key={location.id} value={location.id}>{location.locationCode} · {location.name || location.locationType}</SelectItem>)}</SelectContent></Select></div>
                <div className="space-y-2"><Label>Location barcode / code</Label><Input value={locationIdentifier} onChange={event => setLocationIdentifier(event.target.value)} placeholder="Scan bin label" /></div>
                <div className="space-y-2"><Label>Lot</Label><Input value={lotNumber} onChange={event => setLotNumber(event.target.value)} /></div>
                <div className="space-y-2"><Label>Batch</Label><Input value={batchNumber} onChange={event => setBatchNumber(event.target.value)} /></div>
                <div className="space-y-2"><Label>Serial</Label><Input value={serialNumber} onChange={event => setSerialNumber(event.target.value)} /></div>
                <div className="space-y-2"><Label>Manufacture date</Label><Input type="date" value={manufactureDate} onChange={event => setManufactureDate(event.target.value)} /></div>
                <div className="space-y-2"><Label>Expiry date</Label><Input type="date" value={expiryDate} onChange={event => setExpiryDate(event.target.value)} /></div>
                <div className="space-y-2 sm:col-span-2"><Label>Approved tracking exception</Label><InventoryTrackingExceptionSelect value={trackingExceptionId} onValueChange={value => setTrackingExceptionId(value || '')} exceptions={trackingExceptions.exceptions} loading={trackingExceptions.loading} error={trackingExceptions.error} onRetry={trackingExceptions.refresh} context={{ inventoryItemId: selectedDocumentLine?.inventoryItemId, warehouseId: context?.warehouseId, locationId: locationId || selectedDocumentLine?.locationId, referenceId: context?.documentId, lotNumber, batchNumber, serialNumber }} /></div>
              </div>
            </CardContent></Card>

            <Card><CardHeader><CardTitle>Captured lines</CardTitle><CardDescription>The same device key is retained through offline retry, so an applied batch cannot post twice.</CardDescription></CardHeader><CardContent className="space-y-4">
              <div className="overflow-auto rounded-md border"><Table><TableHeader><TableRow><TableHead>Identifier</TableHead><TableHead>Qty</TableHead><TableHead>Location / lot / batch / serial</TableHead><TableHead className="w-12" /></TableRow></TableHeader><TableBody>{lines.length === 0 ? <TableRow><TableCell colSpan={4} className="h-28 text-center text-muted-foreground">No scans captured.</TableCell></TableRow> : lines.map(line => <TableRow key={line.clientLineId}><TableCell className="font-mono text-xs">{line.rawIdentifier}<div className="text-muted-foreground">{line.documentLineId ? 'Line selected' : 'Server resolves line'}</div></TableCell><TableCell>{line.quantity}</TableCell><TableCell className="text-xs">{[line.locationIdentifier || line.locationId, line.lotNumber, line.batchNumber, line.serialNumber, line.expiryDate].filter(Boolean).join(' · ') || '—'}</TableCell><TableCell><Button variant="ghost" size="icon" onClick={() => setLines(values => values.filter(value => value.clientLineId !== line.clientLineId))}><Trash2 className="h-4 w-4" /></Button></TableCell></TableRow>)}</TableBody></Table></div>
              <div className="rounded-md border p-3"><div className="flex items-start gap-3"><Checkbox id="apply" checked={applyTransaction} onCheckedChange={value => setApplyTransaction(value === true)} /><div><Label htmlFor="apply">Apply the authoritative inventory transaction</Label><p className="text-xs text-muted-foreground">When cleared, synchronization stores scan/audit evidence only. When selected, the existing GRN, requisition, transfer, or count service applies it atomically.</p></div></div></div>
              <Button className="w-full" onClick={() => void sync()} disabled={!context || lines.length === 0 || synchronizing}>{synchronizing ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : online ? <CheckCircle2 className="mr-2 h-4 w-4" /> : <CloudOff className="mr-2 h-4 w-4" />}{online ? 'Synchronize batch' : 'Save batch offline'}</Button>
            </CardContent></Card>
          </div>
          {queuedBatches.length > 0 && <Card><CardHeader><CardTitle>Offline synchronization queue</CardTitle><CardDescription>Queued batches retain their original device and idempotency keys. A failed head can be retried or explicitly discarded so later work is not permanently blocked.</CardDescription></CardHeader><CardContent><div className="overflow-auto rounded-md border"><Table><TableHeader><TableRow><TableHead>Queued</TableHead><TableHead>Operation / document</TableHead><TableHead>Lines</TableHead><TableHead>Attempts / last result</TableHead><TableHead className="text-right">Recovery</TableHead></TableRow></TableHeader><TableBody>{queuedBatches.map((batch, index) => <TableRow key={batch.idempotencyKey}><TableCell className="text-xs">{new Date(batch.queuedAtUtc).toLocaleString()}<div className="font-mono text-muted-foreground">{batch.deviceId}</div></TableCell><TableCell>{OPERATIONS.find(([value]) => value === batch.operation)?.[1]}<div className="font-mono text-xs text-muted-foreground">{batch.documentId}</div></TableCell><TableCell>{batch.lines.length}</TableCell><TableCell>{batch.attempts}<div className="max-w-xl text-xs text-destructive">{batch.lastError || 'Waiting for connectivity'}</div></TableCell><TableCell className="text-right">{index === 0 && batch.lastError ? <Button size="sm" variant="destructive" onClick={() => setDiscardTarget(batch)}><Trash2 className="mr-2 h-4 w-4" />Discard failed batch</Button> : <span className="text-xs text-muted-foreground">{index === 0 ? 'Retry from Sync queue' : 'Waiting behind earlier work'}</span>}</TableCell></TableRow>)}</TableBody></Table></div></CardContent></Card>}
        </TabsContent>

        <TabsContent value="labels" className="space-y-4">
          <div className="grid gap-4 xl:grid-cols-[minmax(320px,0.75fr)_minmax(0,1.25fr)]">
            <Card><CardHeader><CardTitle>Label profiles</CardTitle><CardDescription>QR and CODE128 layouts are tenant-scoped, version-safe, and auditable.</CardDescription></CardHeader><CardContent className="space-y-4">
              <div className="flex flex-wrap gap-2">{profiles.map(profile => <Button key={profile.id} size="sm" variant={editingProfileId === profile.id ? 'default' : 'outline'} onClick={() => editProfile(profile)}>{profile.name}{profile.isDefault ? ' · Default' : ''}</Button>)}<Button size="sm" variant="ghost" onClick={() => editProfile()}><Plus className="mr-1 h-4 w-4" />New</Button></div>
              <div className="space-y-2"><Label>Name</Label><Input value={profileDraft.name} onChange={event => setProfileDraft(value => ({ ...value, name: event.target.value }))} /></div>
              <div className="grid grid-cols-2 gap-3"><div className="space-y-2"><Label>Symbology</Label><Select value={profileDraft.symbology} onValueChange={value => setProfileDraft(current => ({ ...current, symbology: value as 'QR' | 'CODE128' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="QR">QR</SelectItem><SelectItem value="CODE128">CODE128</SelectItem></SelectContent></Select></div><div className="space-y-2"><Label>DPI</Label><Input type="number" value={profileDraft.dpi} onChange={event => setProfileDraft(value => ({ ...value, dpi: Number(event.target.value) }))} /></div><div className="space-y-2"><Label>Width mm</Label><Input type="number" value={profileDraft.widthMm} onChange={event => setProfileDraft(value => ({ ...value, widthMm: Number(event.target.value) }))} /></div><div className="space-y-2"><Label>Height mm</Label><Input type="number" value={profileDraft.heightMm} onChange={event => setProfileDraft(value => ({ ...value, heightMm: Number(event.target.value) }))} /></div></div>
              <div className="grid grid-cols-2 gap-2 text-sm">{(['includeItemCode','includeItemName','includeUnit','includeLot','includeSerial','includeExpiry','isDefault','isActive'] as const).map(field => <label key={field} className="flex items-center gap-2 rounded border p-2"><Checkbox checked={profileDraft[field]} onCheckedChange={checked => setProfileDraft(value => ({ ...value, [field]: checked === true }))} />{field.replace(/^include/, '').replace(/([A-Z])/g, ' $1').trim()}</label>)}</div>
              <Button className="w-full" onClick={() => void saveProfile()} disabled={!online || labelBusy}>{labelBusy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}Save profile</Button>
            </CardContent></Card>

            <Card><CardHeader><CardTitle>Label preview & printing</CardTitle><CardDescription>Printing is recorded before the browser print dialog opens; offline unrecorded printing is blocked.</CardDescription></CardHeader><CardContent className="space-y-4">
              <Input value={labelQuery} onChange={event => setLabelQuery(event.target.value)} placeholder="Search item, barcode, QR, or unit" />
              <Select value={candidate?.identifier || ''} onValueChange={value => setCandidate(candidates.find(item => item.identifier === value))}><SelectTrigger><SelectValue placeholder="Select an assigned identifier" /></SelectTrigger><SelectContent>{filteredCandidates.map(value => <SelectItem key={`${value.inventoryItemId}-${value.identifierKind}-${value.identifier}`} value={value.identifier}>{value.itemCode} · {value.identifierKind} · {value.identifier}</SelectItem>)}</SelectContent></Select>
              <div ref={labelRef} className="mx-auto flex min-h-64 max-w-md flex-col items-center justify-center rounded-lg border bg-white p-6 text-center text-black" style={{ width: `${Math.min(previewProfile.widthMm * 5, 420)}px` }}>
                {candidate ? <>{previewProfile.symbology === 'CODE128' ? <Code128Barcode value={candidate.identifier} /> : <QRCodeSVG value={candidate.identifier} size={180} level="M" />}<p className="label-title mt-3 font-semibold">{previewProfile.includeItemCode ? candidate.itemCode : ''}{previewProfile.includeItemName ? ` · ${candidate.itemName}` : ''}</p>{previewProfile.includeUnit && candidate.unitCode && <p className="label-description text-sm">Unit: {candidate.unitCode}</p>}{previewProfile.includeLot && lotNumber && <p className="label-description text-sm">Lot: {lotNumber}</p>}{previewProfile.includeSerial && serialNumber && <p className="label-description text-sm">Serial: {serialNumber}</p>}<p className="label-kicker mt-2 font-mono text-xs">{candidate.identifier}</p></> : <p className="text-sm text-slate-500">Select an identifier to preview the label.</p>}
              </div>
              <div className="grid gap-3 sm:grid-cols-2"><div className="space-y-2"><Label>Copies</Label><Input type="number" min="1" max="1000" value={labelCount} onChange={event => setLabelCount(event.target.value)} /></div><div className="space-y-2"><Label>Printer</Label><Input value={printerName} onChange={event => setPrinterName(event.target.value)} /></div></div>
              <Button className="w-full" onClick={() => void printLabel()} disabled={!online || !selectedProfile || !candidate || labelBusy}><Printer className="mr-2 h-4 w-4" />Record & print</Button>
            </CardContent></Card>
          </div>
        </TabsContent>

        <TabsContent value="history" className="space-y-4">
          <Card><CardHeader><CardTitle>Scan reconciliation</CardTitle><CardDescription>Server-confirmed device keys, transaction outcomes, stock movements, and audit counts.</CardDescription></CardHeader><CardContent><div className="overflow-auto rounded-md border"><Table><TableHeader><TableRow><TableHead>Captured</TableHead><TableHead>Document</TableHead><TableHead>Outcome</TableHead><TableHead>Reconciliation</TableHead></TableRow></TableHeader><TableBody>{batches.length === 0 ? <TableRow><TableCell colSpan={4} className="h-24 text-center text-muted-foreground">No synchronized batches.</TableCell></TableRow> : batches.map(batch => <TableRow key={batch.id}><TableCell className="text-xs">{new Date(batch.capturedAtUtc).toLocaleString()}<div className="font-mono text-muted-foreground">{batch.deviceId}</div></TableCell><TableCell><div className="font-medium">{batch.documentReference}</div><div className="text-xs text-muted-foreground">{OPERATIONS.find(([value]) => value === batch.operation)?.[1]}</div></TableCell><TableCell><Badge variant={batch.applyTransaction ? 'default' : 'secondary'}>{batch.applyTransaction ? 'Applied' : 'Captured'}</Badge></TableCell><TableCell className="text-xs">{batch.reconciliation ? `${batch.reconciliation.scannedLineCount} scans · ${batch.reconciliation.scannedBaseQuantity} base qty · ${batch.reconciliation.stockMovementCount} movements · ${batch.reconciliation.auditedEventCount} audit` : '—'}</TableCell></TableRow>)}</TableBody></Table></div></CardContent></Card>
          <Card><CardHeader><CardTitle>Label print audit</CardTitle></CardHeader><CardContent><div className="overflow-auto rounded-md border"><Table><TableHeader><TableRow><TableHead>Printed</TableHead><TableHead>Item</TableHead><TableHead>Identifier</TableHead><TableHead>Copies / printer</TableHead></TableRow></TableHeader><TableBody>{prints.length === 0 ? <TableRow><TableCell colSpan={4} className="h-24 text-center text-muted-foreground">No label prints recorded.</TableCell></TableRow> : prints.map(print => <TableRow key={print.id}><TableCell className="text-xs">{new Date(print.printedAtUtc).toLocaleString()}</TableCell><TableCell>{print.itemCode}<div className="text-xs text-muted-foreground">{print.itemName}</div></TableCell><TableCell className="font-mono text-xs">{print.identifier}<div>{print.identifierKind}</div></TableCell><TableCell>{print.labelCount}<div className="text-xs text-muted-foreground">{print.printerName || 'Browser print'}</div></TableCell></TableRow>)}</TableBody></Table></div></CardContent></Card>
        </TabsContent>
      </Tabs>
      <ConfirmationDialog
        open={discardTarget !== undefined}
        onOpenChange={(open) => { if (!open && !discarding) setDiscardTarget(undefined); }}
        title="Discard failed scan batch?"
        description="This permanently removes the queued batch and its captured lines from this device. This action cannot be undone."
        confirmText="Discard batch"
        variant="destructive"
        onConfirm={confirmDiscardQueuedBatch}
        isLoading={discarding}
      />
    </div>
  );
}
