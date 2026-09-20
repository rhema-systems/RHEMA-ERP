'use client';

import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Check, ChevronsUpDown, Eye, Maximize2, Minimize2, Pencil, Plus, RefreshCw, Trash2, XCircle } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { WorkflowApprovalHistoryPanel } from '@/components/workflow/WorkflowApprovalHistoryPanel';
import { useAuth } from '@/hooks/use-auth';
import { documentManagementService, type CentralDocumentRecord } from '@/services/document-management.service';
import { inventoryDisposalService, type DisposalEvidenceRequest, type InventoryDisposal, type InventoryDisposalMethod } from '@/services/inventoryDisposalService';
import { inventoryManagementService, type InventoryItemDto, type WarehouseDto, type WarehouseLocationDto } from '@/services/inventoryManagementService';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { Account } from '@/types/finance';

const statuses: Record<number, string> = { 1: 'Draft', 2: 'Audit verified', 3: 'Committee scheduled', 4: 'Committee recommended',
  5: 'Pending approval', 6: 'Approved', 7: 'Ready to post', 8: 'Completed', 9: 'Rejected', 10: 'Cancelled', 11: 'Ready to post' };
const methods: Record<number, string> = { 1: 'Auction', 2: 'Sale', 3: 'Write-off', 4: 'Donation', 5: 'Destruction' };
const actions: Record<number, string> = { 1: 'Draft saved', 2: 'Verified', 3: 'Verification rejected', 4: 'Committee scheduled',
  5: 'Committee vote', 6: 'Recommended', 7: 'Committee rejected', 8: 'Submitted', 9: 'Approved', 10: 'Rejected',
  11: 'Posting prepared', 12: 'Posted', 13: 'Cancelled', 14: 'Draft updated', 15: 'Approval not required' };
const quantityText = (value: number) => new Intl.NumberFormat('en-GH', { maximumFractionDigits: 4 }).format(value || 0);
const amountText = (value: number, currency = 'GHS') => new Intl.NumberFormat('en-GH', { style: 'currency', currency }).format(value || 0);
const errorText = (error: unknown) => {
  const data = (error as { response?: { data?: { detail?: string; message?: string; title?: string; code?: string } } })?.response?.data;
  return (data?.detail || data?.message || data?.title || (error instanceof Error ? error.message : 'The action could not be completed.')) +
    (data?.code ? ' (' + data.code + ')' : '');
};
type Line = { inventoryItemId: string; locationId: string; quantity: number; itemCode?: string; itemName?: string; locationCode?: string;
  unitOfMeasure?: string; unitCost?: number; totalValue?: number; lotNumber?: string; batchNumber?: string; serialNumber?: string; conditionNotes?: string };
type Selection = { value: string; label: string };
function SearchSelect({ label, value, options, onChange, disabled }: { label: string; value: string; options: Selection[]; onChange: (value: string) => void; disabled?: boolean }) {
  const [open, setOpen] = useState(false);
  return <Popover open={open} onOpenChange={setOpen}><PopoverTrigger asChild>
    <Button variant="outline" role="combobox" aria-label={label} aria-expanded={open} disabled={disabled} className="w-full justify-between font-normal">
      <span className="truncate">{options.find(option => option.value === value)?.label || 'Select ' + label.toLowerCase()}</span><ChevronsUpDown className="ml-2 h-4 w-4 shrink-0" />
    </Button></PopoverTrigger><PopoverContent className="w-[400px] max-w-[calc(100vw-3rem)] p-0" align="start">
      <Command><CommandInput placeholder={'Search ' + label.toLowerCase() + '...'} /><CommandList><CommandEmpty>No matches.</CommandEmpty>
        <CommandGroup>{options.map(option => <CommandItem key={option.value} value={option.value + ' ' + option.label} onSelect={() => { onChange(option.value); setOpen(false); }}>
          <Check className={'mr-2 h-4 w-4 ' + (value === option.value ? 'opacity-100' : 'opacity-0')} />{option.label}
        </CommandItem>)}</CommandGroup></CommandList></Command>
    </PopoverContent></Popover>;
}

export default function InventoryDisposalsPage() {
  const { hasPermission } = useAuth();
  const [rows, setRows] = useState<InventoryDisposal[]>([]);
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState('');
  const [busy, setBusy] = useState(false);
  const createAttempt = useRef<{ payload: string; key: string } | undefined>(undefined);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [items, setItems] = useState<InventoryItemDto[]>([]);
  const [locations, setLocations] = useState<WarehouseLocationDto[]>([]);
  const [documents, setDocuments] = useState<CentralDocumentRecord[]>([]);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [lookupBusy, setLookupBusy] = useState(false);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [registerPage, setRegisterPage] = useState(1);
  const [selected, setSelected] = useState<InventoryDisposal>();
  const [mode, setMode] = useState<'new' | 'edit' | 'view'>();
  const [tab, setTab] = useState('details');
  const [fullscreen, setFullscreen] = useState(false);
  const [warehouseId, setWarehouseId] = useState('');
  const [method, setMethod] = useState<InventoryDisposalMethod>(3);
  const [reason, setReason] = useState('');
  const [notes, setNotes] = useState('');
  const [lines, setLines] = useState<Line[]>([]);
  const [itemId, setItemId] = useState('');
  const [locationId, setLocationId] = useState('');
  const [quantity, setQuantity] = useState('1');
  const [lot, setLot] = useState('');
  const [batch, setBatch] = useState('');
  const [serial, setSerial] = useState('');
  const [documentId, setDocumentId] = useState('');
  const [lineSearch, setLineSearch] = useState('');
  const [linePage, setLinePage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [showCost, setShowCost] = useState(false);
  const [showTracking, setShowTracking] = useState(false);
  const [confirm, setConfirm] = useState<'cancel' | 'reject' | 'post'>();
  const [actionReason, setActionReason] = useState('');
  const [executionReference, setExecutionReference] = useState('');
  const [buyer, setBuyerBuyer] = useState('');
  const [proceeds, setProceeds] = useState('0');
  const [accountId, setAccountId] = useState('');
  const editable = mode === 'new' || mode === 'edit';
  const currentItem = items.find(item => item.id === itemId);
  const proceedsMethod = method === 1 || method === 2;

  const load = async () => {
    setLoading(true); setLoadError('');
    try {
      const [register, warehouseRows] = await Promise.all([
        inventoryDisposalService.getAll({ take: 500 }), inventoryManagementService.getWarehouses(true),
      ]);
      setRows(register); setWarehouses(warehouseRows);
    } catch (error) { setLoadError(errorText(error)); }
    finally { setLoading(false); }
  };
  useEffect(() => { void load(); }, []);
  useEffect(() => {
    if (!mode || !warehouseId) { setItems([]); setLocations([]); return; }
    let live = true; setLookupBusy(true);
    Promise.all([inventoryManagementService.getInventoryByWarehouse(warehouseId), inventoryManagementService.getWarehouseLocations(warehouseId)])
      .then(([itemRows, locationRows]) => {
        if (!live) return;
        setItems(itemRows.filter(item => item.itemType === 1)); const active = locationRows.filter(location => location.isActive);
        setLocations(active); setLocationId(current => active.some(location => location.id === current) ? current : active.find(location => location.isDefault)?.id || '');
      }).catch(error => { if (live) toast.error(errorText(error)); }).finally(() => { if (live) setLookupBusy(false); });
    return () => { live = false; };
  }, [warehouseId, mode]);
  useEffect(() => {
    if (!mode || tab !== 'documents') return;
    let live = true;
    documentManagementService.getRecords().then(records => {
      if (live) setDocuments(records.filter(record => record.lifecycleStatus === 'Active' && record.versionStatus === 'Published' && !!record.currentVersion));
    }).catch(error => { if (live) toast.error('Supporting documents: ' + errorText(error)); });
    return () => { live = false; };
  }, [mode, tab]);
  useEffect(() => {
    if (!mode || !proceedsMethod) return;
    let live = true;
    financeDataService.getAccounts({ status: 'Active', pageSize: 1000 }).then(values => {
      if (live) setAccounts(values.filter(account => account.status === 'Active' && account.allowDirectPosting && account.isPostingAllowed !== false));
    }).catch(error => { if (live) toast.error('Finance accounts: ' + errorText(error)); });
    return () => { live = false; };
  }, [mode, proceedsMethod]);
  useEffect(() => { setLinePage(1); }, [lineSearch, pageSize]);
  useEffect(() => { setRegisterPage(1); }, [search, statusFilter]);

  const applyRecord = (value: InventoryDisposal) => {
    setSelected(value); setWarehouseId(value.warehouseId); setMethod(value.method); setReason(value.reason);
    setNotes(value.identificationDetails); setLines(value.lines.map(line => ({ ...line })));
    setExecutionReference(value.executionReference || value.disposalNumber); setBuyerBuyer(value.buyerOrRecipient || '');
    setProceeds(String(value.proceedsAmount || 0)); setRows(current => [value, ...current.filter(row => row.id !== value.id)]);
  };
  const open = async (row: InventoryDisposal, nextMode: 'edit' | 'view') => {
    setBusy(true);
    try {
      const value = await inventoryDisposalService.getById(row.id);
      applyRecord(value); setDocumentId(''); setAccountId(''); setMode(nextMode); setTab('details'); setFullscreen(false); setLineSearch(''); setLinePage(1);
    } catch (error) { toast.error(errorText(error)); }
    finally { setBusy(false); }
  };
  const newDraft = () => {
    createAttempt.current = undefined;
    setSelected(undefined); setMode('new'); setWarehouseId(''); setMethod(3); setReason(''); setNotes('');
    setLines([]); setItemId(''); setLocationId(''); setDocumentId(''); setQuantity('1'); setTab('details');
    setFullscreen(false); setLineSearch(''); setLinePage(1); setShowCost(false); setShowTracking(false);
    setLot(''); setBatch(''); setSerial(''); setAccountId(''); setActionReason('');
  };
  const run = async (work: () => Promise<InventoryDisposal>, message: string | ((value: InventoryDisposal) => string)) => {
    if (busy) return false;
    setBusy(true);
    try { const value = await work(); applyRecord(value); setMode('view'); toast.success(typeof message === 'function' ? message(value) : message); return true; }
    catch (error) { toast.error(errorText(error)); return false; }
    finally { setBusy(false); }
  };
  const evidence = async (): Promise<DisposalEvidenceRequest[]> => {
    if (!documentId) return [];
    const detail = await documentManagementService.getRecord(documentId);
    const version = detail?.versions.find(value => value.status === 'Published' && value.versionNumber === detail.record.currentVersion && !!value.fileUploadRecordId);
    if (!detail || !version) throw new Error('Select a current published document with a saved file.');
    return [{ centralDocumentVersionId: version.id, evidenceReference: detail.record.title }];
  };
  const save = () => run(async () => {
    if (!warehouseId || !reason.trim() || !lines.length || lines.some(line => !Number.isFinite(line.quantity) || line.quantity <= 0))
      throw new Error('Select a warehouse, enter a reason and add positive item quantities.');
    const request = { method, reason: reason.trim(), identificationDetails: notes.trim(), lines, evidence: await evidence() };
    if (mode === 'edit' && selected) return inventoryDisposalService.update(selected, request);
    const payload = JSON.stringify({ warehouseId, ...request });
    if (createAttempt.current?.payload !== payload) createAttempt.current = { payload, key: `identify:${crypto.randomUUID()}` };
    return inventoryDisposalService.create({ warehouseId, ...request, idempotencyKey: createAttempt.current.key });
  }, 'Draft saved.');
  const add = () => {
    const value = Number(quantity);
    if (!itemId || !locationId || !Number.isFinite(value) || value <= 0) { toast.error('Select an item, bin and positive quantity.'); return; }
    if (lines.some(line => line.inventoryItemId === itemId && line.locationId === locationId &&
      (line.lotNumber || '').toLowerCase() === lot.trim().toLowerCase() &&
      (line.batchNumber || '').toLowerCase() === batch.trim().toLowerCase() &&
      (line.serialNumber || '').toLowerCase() === serial.trim().toLowerCase())) {
      toast.error('This item and bin are already listed. Change its quantity in the grid.'); return;
    }
    setLines(current => [...current, { inventoryItemId: itemId, locationId, quantity: value,
      itemCode: currentItem?.itemCode, itemName: currentItem?.name, locationCode: locations.find(location => location.id === locationId)?.locationCode,
      unitOfMeasure: currentItem?.unitOfMeasure,
      lotNumber: lot.trim() || undefined, batchNumber: batch.trim() || undefined, serialNumber: serial.trim() || undefined }]);
    setItemId(''); setQuantity('1'); setLot(''); setBatch(''); setSerial(''); setLinePage(Math.ceil((lines.length + 1) / pageSize));
  };
  const filteredRows = useMemo(() => rows.filter(row => (statusFilter === 'all' || String(row.status) === statusFilter) &&
    [row.disposalNumber, row.reason, row.warehouseName].join(' ').toLowerCase().includes(search.toLowerCase()))
    .sort((a, b) => b.requestedAtUtc.localeCompare(a.requestedAtUtc) || b.id.localeCompare(a.id)), [rows, search, statusFilter]);
  const filteredLines = lines.map((line, index) => ({ line, index })).filter(({ line }) =>
    [line.itemCode, line.itemName, line.locationCode].join(' ').toLowerCase().includes(lineSearch.toLowerCase()));
  const pages = Math.max(1, Math.ceil(filteredLines.length / pageSize));
  const actualPage = Math.min(linePage, pages);
  const displayedLines = filteredLines.slice((actualPage - 1) * pageSize, actualPage * pageSize);
  const currency = selected?.currencyCode || 'GHS';
  const hasEvidence = !!selected?.evidence.length || !!documentId;
  const canPost = !!selected && (selected.canStageExecution || selected.canComplete);
  const confirmAction = async () => {
    if (!selected) return false;
    if (confirm === 'cancel') return run(() => inventoryDisposalService.cancel(selected, actionReason.trim()), 'Disposal cancelled.');
    if (confirm === 'reject') return run(() => inventoryDisposalService.decide(selected, false, actionReason.trim()), 'Disposal rejected.');
    return run(async () => {
      let value = selected;
      if (value.canStageExecution) {
        value = await inventoryDisposalService.stageExecution(value, { proceedsAmount: proceedsMethod ? Number(proceeds) : 0,
          postImmediately: true,
          proceedsAccountId: proceedsMethod ? accountId || undefined : undefined, buyerOrRecipient: buyer.trim() || undefined,
          executionReference: executionReference.trim() || value.disposalNumber, evidence: await evidence() });
        applyRecord(value); // Retain the saved stage if the next action fails; retry never recreates it.
      }
      return value.status === 8 ? value : inventoryDisposalService.complete(value);
    }, value => value.status === 8 ? 'Disposal posted.' : 'Stock adjustment is awaiting its configured approval. No stock has been posted.');
  };
  const pager = (current: number, total: number, change: (value: number) => void) => <div className="flex items-center gap-2 text-sm">
    <Button variant="outline" size="sm" disabled={current <= 1} onClick={() => change(current - 1)}>Previous</Button>
    <span>{current} / {total}</span><Button variant="outline" size="sm" disabled={current >= total} onClick={() => change(current + 1)}>Next</Button>
  </div>;
  const tabs = ['details', 'items', 'documents', ...(selected?.approvalRequired && !editable ? ['workflow'] : []), ...(!editable ? ['history'] : [])];

  return <div className="space-y-4">
    <div className="flex items-center justify-between gap-3"><div><h1 className="text-2xl font-semibold">Inventory Disposals</h1>
      <p className="text-sm text-muted-foreground">Write off, donate or sell stock.</p></div><div className="flex gap-2">
      <Button variant="outline" aria-label="Refresh disposals" title="Refresh" disabled={loading || busy} onClick={() => void load()}><RefreshCw className="h-4 w-4" /></Button>
      {hasPermission('procurement.inventory.disposal.request') && <Button onClick={newDraft}><Plus className="mr-2 h-4 w-4" />New disposal</Button>}
    </div></div>
    {loadError && <div role="alert" className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800">{loadError}</div>}
    <Card><CardHeader className="pb-3"><CardTitle>Disposal register</CardTitle></CardHeader><CardContent className="space-y-3">
      <div className="flex gap-3"><Input aria-label="Search disposals" placeholder="Search disposals..." value={search} onChange={event => setSearch(event.target.value)} className="max-w-sm" />
        <Select value={statusFilter} onValueChange={setStatusFilter}><SelectTrigger aria-label="Status filter" className="w-48"><SelectValue /></SelectTrigger>
          <SelectContent><SelectItem value="all">All statuses</SelectItem>{Object.entries(statuses).map(([key, label]) => <SelectItem key={key} value={key}>{label}</SelectItem>)}</SelectContent></Select></div>
      <div className="overflow-auto rounded-lg border"><Table><TableHeader><TableRow>
        <TableHead>Disposal</TableHead><TableHead>Date</TableHead><TableHead>Warehouse</TableHead><TableHead>Method</TableHead><TableHead>Status</TableHead><TableHead className="w-28">Actions</TableHead>
      </TableRow></TableHeader><TableBody>{filteredRows.slice((registerPage - 1) * 25, registerPage * 25).map(row => <TableRow key={row.id}>
        <TableCell className="py-2 font-medium">{row.disposalNumber}</TableCell><TableCell className="py-2">{new Date(row.requestedAtUtc).toLocaleDateString()}</TableCell>
        <TableCell className="py-2">{row.warehouseName}</TableCell><TableCell className="py-2">{methods[row.method]}</TableCell><TableCell className="py-2"><Badge className={row.status === 8 ? 'bg-emerald-100 text-emerald-800' : row.status >= 9 && row.status <= 10 ? 'bg-red-100 text-red-800' : 'bg-slate-100 text-slate-800'}>{statuses[row.status]}</Badge></TableCell>
        <TableCell className="py-2"><div className="flex gap-1"><Button size="icon" variant="ghost" aria-label={'View ' + row.disposalNumber} title="View" disabled={busy} onClick={() => void open(row, 'view')}><Eye className="h-4 w-4" /></Button>
          {row.canEdit && <Button size="icon" variant="ghost" aria-label={'Edit ' + row.disposalNumber} title="Edit" disabled={busy} onClick={() => void open(row, 'edit')}><Pencil className="h-4 w-4" /></Button>}
          {row.canCancel && <Button size="icon" variant="ghost" aria-label={'Cancel ' + row.disposalNumber} title="Cancel" disabled={busy} onClick={async () => {
            setBusy(true); try { const value = await inventoryDisposalService.getById(row.id); applyRecord(value); setActionReason(''); setConfirm('cancel'); } catch (error) { toast.error(errorText(error)); } finally { setBusy(false); }
          }}><XCircle className="h-4 w-4 text-red-600" /></Button>}
        </div></TableCell></TableRow>)}
        {!filteredRows.length && <TableRow><TableCell colSpan={6} className="h-24 text-center">{loading ? 'Loading...' : 'No disposals found.'}</TableCell></TableRow>}
      </TableBody></Table></div>
      <div className="flex items-center justify-between text-sm"><span>{filteredRows.length} disposal(s)</span>{pager(registerPage, Math.max(1, Math.ceil(filteredRows.length / 25)), setRegisterPage)}</div>
    </CardContent></Card>

    <Dialog open={!!mode} onOpenChange={open => { if (!open && !busy) { setMode(undefined); setFullscreen(false); } }}>
      <DialogContent className="flex flex-col gap-3 overflow-hidden" style={{ width: fullscreen ? 'calc(100vw - 32px)' : '960px', maxWidth: 'calc(100vw - 32px)', height: fullscreen ? 'calc(100vh - 32px)' : 'min(760px, calc(100vh - 48px))' }}>
        <DialogHeader className="shrink-0 pr-6"><DialogTitle>{mode === 'new' ? 'New disposal' : mode === 'edit' ? 'Edit disposal' : selected?.disposalNumber}</DialogTitle>
          <DialogDescription>{editable ? 'Save a draft before continuing. Stock changes only when posted.' : (statuses[selected?.status || 1] + (selected?.approvalRequired === false ? ' · Approval not required' : ''))}</DialogDescription>
        </DialogHeader>
        <Tabs value={tab} onValueChange={value => { setTab(value); setFullscreen(false); }} className="flex min-h-0 flex-1 flex-col">
          <TabsList className="grid w-full shrink-0" style={{ gridTemplateColumns: 'repeat(' + tabs.length + ', minmax(0, 1fr))' }}>
            {tabs.map(value => <TabsTrigger key={value} value={value} className="min-w-0 capitalize">{value === 'documents' ? 'Supporting documents' : value === 'items' ? 'Items (' + lines.length + ')' : value}</TabsTrigger>)}
          </TabsList>
          <TabsContent value="details" className="min-h-0 flex-1 overflow-y-auto space-y-4 pr-1">
            <div className="grid grid-cols-2 gap-4"><div className="space-y-1"><Label>Warehouse</Label>{editable ? <SearchSelect label="Warehouse" value={warehouseId} options={warehouses.map(value => ({ value: value.id, label: value.name }))} onChange={setWarehouseId} disabled={mode === 'edit' || !!lines.length || busy} /> : <p>{selected?.warehouseName}</p>}</div>
              <div className="space-y-1"><Label>Method</Label>{editable ? <Select value={String(method)} onValueChange={value => setMethod(Number(value) as InventoryDisposalMethod)}><SelectTrigger aria-label="Disposal method"><SelectValue /></SelectTrigger><SelectContent>{Object.entries(methods).map(([key, label]) => <SelectItem key={key} value={key}>{label}</SelectItem>)}</SelectContent></Select> : <p>{methods[method]}</p>}</div>
            </div><div className="space-y-1"><Label htmlFor="disposal-reason">Reason</Label>{editable ? <Input id="disposal-reason" value={reason} onChange={event => setReason(event.target.value)} maxLength={1000} /> : <p>{reason}</p>}</div>
            <div className="space-y-1"><Label htmlFor="disposal-notes">Notes (optional)</Label>{editable ? <Textarea id="disposal-notes" value={notes} onChange={event => setNotes(event.target.value)} maxLength={2000} /> : <p>{notes || '—'}</p>}</div>
            {!editable && <div className="grid grid-cols-2 gap-4 text-sm"><div><Label>Requested by</Label><p>{selected?.requestedByName}</p></div><div><Label>{selected?.postedStockValue != null ? 'Posted stock value' : 'Estimated stock value'}</Label><p>{amountText(selected?.postedStockValue ?? selected?.totalValue ?? 0, currency)}</p></div></div>}
            {!editable && selected?.canStageExecution && <div className="space-y-3 border-t pt-3"><div><Label htmlFor="execution-ref">Disposal reference</Label><Input id="execution-ref" value={executionReference} onChange={event => setExecutionReference(event.target.value)} maxLength={200} /></div>
              {(proceedsMethod || method === 4) && <div><Label htmlFor="disposal-recipient">{method === 4 ? 'Recipient' : 'Buyer'}</Label><Input id="disposal-recipient" value={buyer} onChange={event => setBuyerBuyer(event.target.value)} maxLength={200} /></div>}
              {proceedsMethod && <div className="grid grid-cols-2 gap-3"><div><Label htmlFor="disposal-proceeds">Proceeds ({currency})</Label><Input id="disposal-proceeds" type="number" min="0" value={proceeds} onChange={event => setProceeds(event.target.value)} /></div>
                <div><Label>Proceeds account</Label><SearchSelect label="Proceeds account" value={accountId} options={accounts.map(account => ({ value: account.id, label: (account.accountCode || account.accountNumber) + ' — ' + account.accountName }))} onChange={setAccountId} /></div></div>}
            </div>}
          </TabsContent>
          <TabsContent value="items" className="min-h-0 flex-1 overflow-hidden flex-col data-[state=active]:flex gap-2">
            {editable && <div className="grid shrink-0 grid-cols-[minmax(0,2fr)_minmax(0,1fr)_90px_auto] items-end gap-2">
              <div><Label>Item</Label><SearchSelect label="Item" value={itemId} options={items.map(item => ({ value: item.id, label: item.itemCode + ' — ' + item.name }))} onChange={value => { setItemId(value); setLot(''); setBatch(''); setSerial(''); }} disabled={!warehouseId || lookupBusy || busy} /></div>
              <div><Label>Bin</Label><SearchSelect label="Bin" value={locationId} options={locations.map(location => ({ value: location.id, label: location.locationCode + (location.name && location.name !== location.locationCode ? ' — ' + location.name : '') }))} onChange={setLocationId} disabled={lookupBusy || busy} /></div>
              <div><Label htmlFor="disposal-quantity">Quantity</Label><Input id="disposal-quantity" type="number" min="0.0001" step="0.0001" value={quantity} onChange={event => setQuantity(event.target.value)} /></div><Button variant="outline" onClick={add} disabled={busy || lookupBusy}><Plus className="mr-1 h-4 w-4" />Add</Button>
            </div>}
            {editable && currentItem && (currentItem.isLotTracked || currentItem.isSerialTracked || currentItem.isBatchTracked) && <div className="flex shrink-0 gap-2">
              {currentItem.isLotTracked && <Input aria-label="Lot number" placeholder="Lot number" value={lot} onChange={event => setLot(event.target.value)} />}
              {currentItem.isBatchTracked && <Input aria-label="Batch number" placeholder="Batch number" value={batch} onChange={event => setBatch(event.target.value)} />}
              {currentItem.isSerialTracked && <Input aria-label="Serial number" placeholder="Serial number" value={serial} onChange={event => setSerial(event.target.value)} />}
            </div>}
            <div className="flex shrink-0 items-center gap-2"><Input aria-label="Search disposal items" placeholder="Search item or bin..." className="max-w-sm" value={lineSearch} onChange={event => setLineSearch(event.target.value)} />
              <Popover><PopoverTrigger asChild><Button variant="outline" size="sm">Columns</Button></PopoverTrigger><PopoverContent className="w-44 space-y-2">
                <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={showCost} onChange={event => setShowCost(event.target.checked)} />Cost and value</label>
                <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={showTracking} onChange={event => setShowTracking(event.target.checked)} />Tracking</label>
              </PopoverContent></Popover><Button className="ml-auto" variant="outline" size="sm" onClick={() => setFullscreen(value => !value)}>{fullscreen ? <Minimize2 className="mr-1 h-4 w-4" /> : <Maximize2 className="mr-1 h-4 w-4" />}{fullscreen ? 'Restore' : 'Full page'}</Button></div>
            <div className="min-h-0 flex-1 overflow-auto rounded-lg border"><Table className="text-sm"><TableHeader className="sticky top-0 bg-background"><TableRow><TableHead>Item</TableHead><TableHead>Bin</TableHead><TableHead className="w-28 text-right">Quantity</TableHead><TableHead className="w-16">UOM</TableHead>
              {showTracking && <TableHead>Tracking</TableHead>}{showCost && <><TableHead className="text-right">Unit cost</TableHead><TableHead className="text-right">Value</TableHead></>}{editable && <TableHead className="w-12" />}
            </TableRow></TableHeader><TableBody>{displayedLines.map(({ line, index }) => <TableRow key={index}><TableCell className="py-1.5"><span className="font-medium">{line.itemCode}</span><span className="ml-2 text-muted-foreground">{line.itemName}</span></TableCell><TableCell className="py-1.5">{line.locationCode}</TableCell>
              <TableCell className="py-1.5 text-right">{editable ? <Input className="h-8 text-right" aria-label={'Quantity for ' + line.itemCode} type="number" min="0.0001" step="0.0001" value={line.quantity} onChange={event => setLines(current => current.map((value, n) => n === index ? { ...value, quantity: Number(event.target.value) } : value))} /> : quantityText(line.quantity)}</TableCell>
              <TableCell className="py-1.5">{line.unitOfMeasure || '—'}</TableCell>
              {showTracking && <TableCell className="py-1.5">{[line.lotNumber, line.batchNumber, line.serialNumber].filter(Boolean).join(' / ') || '—'}</TableCell>}
              {showCost && <><TableCell className="py-1.5 text-right">{line.unitCost == null ? 'On save' : amountText(line.unitCost, currency)}</TableCell><TableCell className="py-1.5 text-right">{line.unitCost == null ? 'On save' : amountText(line.quantity * line.unitCost, currency)}</TableCell></>}
              {editable && <TableCell className="py-1.5"><Button size="icon" variant="ghost" className="h-8 w-8" aria-label={'Remove ' + line.itemCode} title="Remove" onClick={() => setLines(current => current.filter((_, n) => n !== index))}><Trash2 className="h-4 w-4 text-red-600" /></Button></TableCell>}
            </TableRow>)}
              {!displayedLines.length && <TableRow><TableCell colSpan={7} className="h-24 text-center">{lines.length ? 'No matching items.' : 'Add items to this disposal.'}</TableCell></TableRow>}
            </TableBody></Table></div><div className="flex shrink-0 items-center justify-between gap-2 text-sm"><span>{filteredLines.length} item(s)</span><div className="flex gap-2">
              <select aria-label="Items per page" className="rounded border bg-background px-2" value={pageSize} onChange={event => setPageSize(Number(event.target.value))}>{[25,50,100].map(size => <option key={size} value={size}>{size} per page</option>)}</select>
              {pager(actualPage, pages, setLinePage)}</div></div>
          </TabsContent>
          <TabsContent value="documents" className="min-h-0 flex-1 overflow-y-auto space-y-3">
            {(editable || selected?.canStageExecution) && <><p className="text-sm text-muted-foreground">Supporting files are optional when approval is not required. Attached files must be current published documents.</p>
              <SearchSelect label="Supporting document" value={documentId} options={documents.map(document => ({ value: document.id, label: document.title + ' — ' + document.documentReference }))} onChange={setDocumentId} />
              {documentId && <Button variant="ghost" size="sm" onClick={() => setDocumentId('')}>Clear selection</Button>}</>}
            {selected?.evidence.map(file => <div className="rounded border p-3 text-sm" key={file.id}>{file.evidenceReference} <span className="text-muted-foreground">{file.versionNumber}</span></div>)}
            {!selected?.evidence.length && !documentId && <p className="text-sm text-muted-foreground">No supporting document attached.</p>}
          </TabsContent>
          {selected?.approvalRequired && !editable && <TabsContent value="workflow" className="min-h-0 flex-1 overflow-auto"><WorkflowApprovalHistoryPanel entityType="InventoryDisposal" entityId={selected.id} showActions={false} /></TabsContent>}
          {!editable && <TabsContent value="history" className="min-h-0 flex-1 overflow-y-auto space-y-2">
            {[...(selected?.actions || [])].sort((a,b) => b.sequence - a.sequence).map(value => <div key={value.sequence} className="rounded border px-3 py-2 text-sm">
              <div className="flex justify-between gap-3"><span className="font-medium">{actions[value.actionType]}</span><span className="text-muted-foreground">{new Date(value.occurredAtUtc).toLocaleString()}</span></div>
              <p>{value.actorName}</p>{value.comment && <p className="text-muted-foreground">{value.comment}</p>}
            </div>)}
          </TabsContent>}
        </Tabs>
        <DialogFooter className="shrink-0 items-center border-t pt-3">
          <Button variant="outline" disabled={busy} onClick={() => { setMode(undefined); setFullscreen(false); }}>Close</Button>
          {editable ? <Button disabled={busy || !warehouseId || !reason.trim() || !lines.length} onClick={() => void save()}>Save draft</Button> : <>
            {selected?.canEdit && <Button variant="outline" disabled={busy} onClick={() => setMode('edit')}><Pencil className="mr-2 h-4 w-4" />Edit</Button>}
            {selected?.canSubmit && <Button disabled={busy || (selected.approvalRequired && !hasEvidence)} title={selected.approvalRequired && !hasEvidence ? 'Edit the draft and add a supporting document first.' : undefined} onClick={() => void run(() => inventoryDisposalService.submit(selected), selected.approvalRequired ? 'Submitted for approval.' : 'Ready to post.')}>{selected.approvalRequired ? 'Submit for approval' : 'Continue'}</Button>}
            {selected?.approvalRequired && selected.canApprove && <><Button variant="destructive" disabled={busy} onClick={() => { setActionReason(''); setConfirm('reject'); }}>Reject</Button><Button disabled={busy} onClick={() => void run(() => inventoryDisposalService.decide(selected, true), 'Approval recorded.')}>Approve</Button></>}
            {canPost && <Button disabled={busy || (selected?.canStageExecution && ((selected.approvalRequired && !hasEvidence) || (proceedsMethod && (!accountId || !buyer.trim() || !Number.isFinite(Number(proceeds)) || Number(proceeds) <= 0)) || (method === 4 && !buyer.trim())))} onClick={() => setConfirm('post')}>Post</Button>}
          </>}
        </DialogFooter>
      </DialogContent>
    </Dialog>
    <ConfirmationDialog open={!!confirm} onOpenChange={open => !open && !busy && setConfirm(undefined)}
      title={confirm === 'post' ? 'Post disposal' : confirm === 'reject' ? 'Reject disposal' : 'Cancel disposal'}
      description={confirm === 'post' ? 'This removes the listed quantities from their bins and records the stock value and any proceeds.' : 'Enter a reason. The record and its history will be retained.'}
      confirmText={confirm === 'post' ? 'Post' : confirm === 'reject' ? 'Reject' : 'Cancel disposal'} cancelText="Back" variant="destructive"
      isLoading={busy} confirmDisabled={confirm !== 'post' && !actionReason.trim()} onConfirm={confirmAction}>
      {confirm !== 'post' && <Textarea aria-label="Action reason" value={actionReason} onChange={event => setActionReason(event.target.value)} maxLength={1000} />}
    </ConfirmationDialog>
  </div>;
}
