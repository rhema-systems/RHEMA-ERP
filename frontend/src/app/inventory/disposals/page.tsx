'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { AxiosError } from 'axios';
import { ArchiveX, CheckCircle2, ClipboardCheck, FileCheck2, Gavel, History, Loader2, Plus,
  RefreshCw, Scale, ShieldCheck, Trash2, Users } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { documentManagementService, type CentralDocumentRecord } from '@/services/document-management.service';
import { inventoryDisposalService, type DisposalEvidenceRequest, type InventoryDisposal,
  type InventoryDisposalMethod } from '@/services/inventoryDisposalService';
import { inventoryManagementService, type InventoryItemDto, type WarehouseDto,
  type WarehouseLocationDto } from '@/services/inventoryManagementService';
import { userService } from '@/services/user';
import type { User } from '@/types';

const status: Record<number, string> = { 1: 'Identified', 2: 'Audit verified', 3: 'Committee scheduled',
  4: 'Committee recommended', 5: 'Pending authority approval', 6: 'Approved',
  7: 'Stock adjustment pending', 8: 'Completed', 9: 'Rejected', 10: 'Cancelled' };
const method: Record<number, string> = { 1: 'Auction', 2: 'Sale', 3: 'Write-off', 4: 'Donation', 5: 'Destruction' };
const action: Record<number, string> = { 1: 'Identified', 2: 'Audit verified', 3: 'Audit rejected',
  4: 'Committee scheduled', 5: 'Committee vote', 6: 'Committee recommended', 7: 'Committee rejected',
  8: 'Submitted', 9: 'Approved', 10: 'Rejected', 11: 'Adjustment staged', 12: 'Completed', 13: 'Cancelled' };
const money = (value: number) => new Intl.NumberFormat('en-GH', { style: 'currency', currency: 'GHS', maximumFractionDigits: 2 }).format(value || 0);
const number = (value: number) => new Intl.NumberFormat('en-GH', { maximumFractionDigits: 4 }).format(value || 0);
type Problem = { detail?: string; message?: string; title?: string };
const errorMessage = (error: unknown, fallback: string) => {
  const value = (error as AxiosError<Problem>)?.response?.data;
  return value?.detail || value?.message || value?.title || (error instanceof Error ? error.message : fallback);
};
type DraftLine = { inventoryItemId: string; locationId: string; quantity: number; lotNumber?: string;
  serialNumber?: string; conditionNotes?: string };

export default function InventoryDisposalsPage() {
  const [rows, setRows] = useState<InventoryDisposal[]>([]);
  const [selected, setSelected] = useState<InventoryDisposal>();
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [locations, setLocations] = useState<WarehouseLocationDto[]>([]);
  const [items, setItems] = useState<InventoryItemDto[]>([]);
  const [documents, setDocuments] = useState<CentralDocumentRecord[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [filter, setFilter] = useState('all');
  const [search, setSearch] = useState('');
  const [warehouseId, setWarehouseId] = useState('');
  const [disposalMethod, setDisposalMethod] = useState<InventoryDisposalMethod>(3);
  const [reason, setReason] = useState('');
  const [details, setDetails] = useState('');
  const [documentId, setDocumentId] = useState('');
  const [itemId, setItemId] = useState('');
  const [locationId, setLocationId] = useState('');
  const [quantity, setQuantity] = useState(1);
  const [condition, setCondition] = useState('');
  const [draftLines, setDraftLines] = useState<DraftLine[]>([]);
  const [comment, setComment] = useState('');
  const [meetingAt, setMeetingAt] = useState('');
  const [committeeReference, setCommitteeReference] = useState('');
  const [memberIds, setMemberIds] = useState<string[]>([]);
  const [executionReference, setExecutionReference] = useState('');
  const [buyer, setBuyer] = useState('');
  const [proceeds, setProceeds] = useState(0);
  const [proceedsAccountId, setProceedsAccountId] = useState('');

  const refresh = useCallback(async () => {
    setLoading(true);
    try {
      const [register, warehouseRows, itemRows, documentRows, userRows] = await Promise.all([
        inventoryDisposalService.getAll({ take: 500 }), inventoryManagementService.getWarehouses(true),
        inventoryManagementService.getInventoryItems({ isActive: true }), documentManagementService.getRecords(),
        userService.searchUsers('').catch(() => []),
      ]);
      setRows(register); setWarehouses(warehouseRows); setItems(itemRows); setUsers(userRows);
      setDocuments(documentRows.filter(value => value.lifecycleStatus === 'Active' && value.versionStatus === 'Published' && !!value.currentVersion));
      if (selected) setSelected(await inventoryDisposalService.getById(selected.id));
    } catch (error) { toast.error(errorMessage(error, 'Unable to load inventory disposal controls.')); }
    finally { setLoading(false); }
  }, [selected]);
  useEffect(() => { void refresh(); }, []);
  useEffect(() => {
    if (!warehouseId) { setLocations([]); setLocationId(''); return; }
    void inventoryManagementService.getWarehouseLocations(warehouseId).then(value => {
      setLocations(value.filter(location => location.isActive)); setLocationId('');
    }).catch(error => toast.error(errorMessage(error, 'Unable to load warehouse locations.')));
  }, [warehouseId]);

  const controlledEvidence = async (): Promise<DisposalEvidenceRequest[]> => {
    if (!documentId) throw new Error('Select a current published central-DMS record.');
    const detail = await documentManagementService.getRecord(documentId);
    const version = detail?.versions.find(value => value.status === 'Published' &&
      value.versionNumber === detail.record.currentVersion && !!value.fileUploadRecordId);
    if (!version) throw new Error('The selected DMS record has no current published file version.');
    return [{ centralDocumentVersionId: version.id,
      evidenceReference: `${detail.record.documentReference} ${version.versionNumber}` }];
  };
  const run = async (work: () => Promise<InventoryDisposal>, success: string) => {
    setSaving(true);
    try {
      const value = await work();
      setRows(current => [value, ...current.filter(row => row.id !== value.id)]); setSelected(value);
      toast.success(success);
    } catch (error) { toast.error(errorMessage(error, 'The controlled disposal action failed.')); }
    finally { setSaving(false); }
  };
  const addLine = () => {
    if (!itemId || !locationId || quantity <= 0) return toast.error('Select an item, location and positive quantity.');
    if (draftLines.some(value => value.inventoryItemId === itemId && value.locationId === locationId))
      return toast.error('That item/location is already in the disposal case.');
    setDraftLines(current => [...current, { inventoryItemId: itemId, locationId, quantity, conditionNotes: condition }]);
    setItemId(''); setQuantity(1); setCondition('');
  };
  const create = () => run(async () => inventoryDisposalService.create({ warehouseId, method: disposalMethod,
    reason, identificationDetails: details, lines: draftLines, evidence: await controlledEvidence() }),
  'Inventory disposal case identified for independent audit verification.');
  const mutate = (work: (value: InventoryDisposal) => Promise<InventoryDisposal>, success: string) => {
    if (selected) void run(() => work(selected), success);
  };
  const filtered = useMemo(() => rows.filter(value => {
    const query = search.trim().toLowerCase();
    return (filter === 'all' || String(value.status) === filter) && (!query ||
      [value.disposalNumber, value.reason, value.warehouseCode, method[value.method]].some(text => text?.toLowerCase().includes(query)));
  }), [filter, rows, search]);
  const committeeCandidates = useMemo(() => users.filter(value => value.isActive &&
    value.id !== selected?.requestedById && value.id !== selected?.auditVerifiedById &&
    value.roles.some(role => role.toUpperCase().replaceAll(' ', '_') === 'TDC_DISPOSAL_COMMITTEE_MEMBER')),
  [selected?.auditVerifiedById, selected?.requestedById, users]);

  return <div className="space-y-6 p-6">
    <div className="flex flex-wrap items-center justify-between gap-3"><div><h1 className="text-2xl font-semibold">Inventory Disposal Control</h1>
      <p className="text-sm text-muted-foreground">Govern obsolete, expired, damaged and surplus stock from identification through evidence, authority, stock write-off and Finance.</p></div>
      <Button variant="outline" onClick={() => void refresh()} disabled={loading}>{loading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <RefreshCw className="mr-2 h-4 w-4" />}Refresh</Button></div>

    <Card><CardHeader><CardTitle><ShieldCheck className="mr-2 inline h-5 w-5" />Shared-owner boundary</CardTitle>
      <CardDescription>Central DMS owns every file and clean scan, shared Workflow owns committee/MD/Board authority, Stock Adjustment owns exact-location stock movement and valuation, and the Finance posting engine owns journals. This register coordinates those owners; it does not create a parallel stock ledger, document store, workflow or posting engine.</CardDescription></CardHeader></Card>

    <Card><CardHeader><CardTitle><ArchiveX className="mr-2 inline h-5 w-5" />Identify a disposal case</CardTitle>
      <CardDescription>Values are server-derived from current exact-location balances. Current published malware-clean DMS evidence is mandatory.</CardDescription></CardHeader>
      <CardContent className="space-y-4"><div className="grid gap-3 md:grid-cols-4">
        <div className="space-y-2"><Label>Warehouse</Label><Select value={warehouseId} onValueChange={setWarehouseId}><SelectTrigger><SelectValue placeholder="Select warehouse" /></SelectTrigger><SelectContent>{warehouses.map(value => <SelectItem key={value.id} value={value.id}>{value.code} · {value.name}</SelectItem>)}</SelectContent></Select></div>
        <div className="space-y-2"><Label>Method</Label><Select value={String(disposalMethod)} onValueChange={value => setDisposalMethod(Number(value) as InventoryDisposalMethod)}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{Object.entries(method).map(([key, label]) => <SelectItem key={key} value={key}>{label}</SelectItem>)}</SelectContent></Select></div>
        <div className="space-y-2 md:col-span-2"><Label>Current published DMS evidence</Label><Select value={documentId} onValueChange={setDocumentId}><SelectTrigger><SelectValue placeholder="Select controlled evidence" /></SelectTrigger><SelectContent>{documents.map(value => <SelectItem key={value.id} value={value.id}>{value.documentReference} · {value.title} ({value.currentVersion})</SelectItem>)}</SelectContent></Select></div>
      </div><div className="grid gap-3 md:grid-cols-2"><div className="space-y-2"><Label>Reason</Label><Input value={reason} onChange={event => setReason(event.target.value)} placeholder="Expired stock, obsolete parts, damage…" /></div><div className="space-y-2"><Label>Identification details</Label><Input value={details} onChange={event => setDetails(event.target.value)} placeholder="Inspection, condition and custody context" /></div></div>
      <div className="grid gap-3 rounded-md border p-3 md:grid-cols-5"><div className="space-y-2"><Label>Item</Label><Select value={itemId} onValueChange={setItemId}><SelectTrigger><SelectValue placeholder="Select item" /></SelectTrigger><SelectContent>{items.map(value => <SelectItem key={value.id} value={value.id}>{value.itemCode} · {value.name}</SelectItem>)}</SelectContent></Select></div>
        <div className="space-y-2"><Label>Exact location</Label><Select value={locationId} onValueChange={setLocationId}><SelectTrigger><SelectValue placeholder="Select location" /></SelectTrigger><SelectContent>{locations.map(value => <SelectItem key={value.id} value={value.id}>{value.locationCode} · {value.name}</SelectItem>)}</SelectContent></Select></div>
        <div className="space-y-2"><Label>Quantity</Label><Input type="number" min={0.0001} step="0.0001" value={quantity} onChange={event => setQuantity(Number(event.target.value))} /></div>
        <div className="space-y-2"><Label>Condition</Label><Input value={condition} onChange={event => setCondition(event.target.value)} /></div>
        <div className="flex items-end"><Button type="button" variant="outline" onClick={addLine}><Plus className="mr-2 h-4 w-4" />Add line</Button></div></div>
      {draftLines.length > 0 && <div className="space-y-2">{draftLines.map((value, index) => <div key={`${value.inventoryItemId}-${value.locationId}`} className="flex items-center justify-between rounded-md border px-3 py-2 text-sm"><span>{items.find(item => item.id === value.inventoryItemId)?.itemCode} · {locations.find(location => location.id === value.locationId)?.locationCode} · {number(value.quantity)}</span><Button size="icon" variant="ghost" onClick={() => setDraftLines(lines => lines.filter((_, lineIndex) => lineIndex !== index))}><Trash2 className="h-4 w-4" /></Button></div>)}</div>}
      <Button onClick={() => void create()} disabled={saving || !warehouseId || !reason.trim() || !details.trim() || !documentId || draftLines.length === 0}>{saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Identify disposal</Button></CardContent></Card>

    <Card><CardHeader><CardTitle><History className="mr-2 inline h-5 w-5" />Disposal register</CardTitle><CardDescription>History-first tenant and warehouse/location authorized cases.</CardDescription></CardHeader>
      <CardContent className="space-y-4"><div className="flex flex-wrap gap-3"><Input className="max-w-sm" placeholder="Search case, warehouse or reason" value={search} onChange={event => setSearch(event.target.value)} /><Select value={filter} onValueChange={setFilter}><SelectTrigger className="w-60"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">All statuses</SelectItem>{Object.entries(status).map(([key, label]) => <SelectItem key={key} value={key}>{label}</SelectItem>)}</SelectContent></Select></div>
      <div className="overflow-auto rounded-md border"><Table><TableHeader><TableRow><TableHead>Case</TableHead><TableHead>Status</TableHead><TableHead>Warehouse / method</TableHead><TableHead>Quantity / value</TableHead><TableHead>Requested</TableHead></TableRow></TableHeader><TableBody>{filtered.length === 0 ? <TableRow><TableCell colSpan={5} className="h-24 text-center text-muted-foreground">No disposal cases match the current scope.</TableCell></TableRow> : filtered.map(value => <TableRow key={value.id} className="cursor-pointer" onClick={() => void inventoryDisposalService.getById(value.id).then(setSelected).catch(error => toast.error(errorMessage(error, 'Unable to load disposal case.')))}><TableCell><div className="font-medium">{value.disposalNumber}</div><div className="text-xs text-muted-foreground">{value.reason}</div></TableCell><TableCell><Badge variant={value.status === 9 ? 'destructive' : value.status === 8 ? 'secondary' : 'outline'}>{status[value.status]}</Badge></TableCell><TableCell>{value.warehouseCode} · {method[value.method]}</TableCell><TableCell>{number(value.totalQuantity)} · {money(value.totalValue)}</TableCell><TableCell>{new Date(value.requestedAtUtc).toLocaleDateString()}</TableCell></TableRow>)}</TableBody></Table></div></CardContent></Card>

    {selected && <Card><CardHeader><CardTitle><Scale className="mr-2 inline h-5 w-5" />{selected.disposalNumber} · {status[selected.status]}</CardTitle><CardDescription>{selected.authorityRoute} · Stock adjustment {selected.stockAdjustmentId || 'not staged'} · {selected.evidence.length} controlled evidence link(s)</CardDescription></CardHeader>
      <CardContent className="space-y-6"><div className="grid gap-3 md:grid-cols-4"><div><Label>Warehouse</Label><p>{selected.warehouseCode} · {selected.warehouseName}</p></div><div><Label>Method</Label><p>{method[selected.method]}</p></div><div><Label>Valuation</Label><p>{money(selected.totalValue)}</p></div><div><Label>Requester</Label><p>{selected.requestedByName}</p></div></div>
      <div className="overflow-auto rounded-md border"><Table><TableHeader><TableRow><TableHead>Item / location</TableHead><TableHead>Quantity</TableHead><TableHead>Unit cost</TableHead><TableHead>Value</TableHead></TableRow></TableHeader><TableBody>{selected.lines.map(value => <TableRow key={value.id}><TableCell>{value.itemCode} · {value.itemName}<div className="text-xs text-muted-foreground">{value.locationCode} · {value.conditionNotes}</div></TableCell><TableCell>{number(value.quantity)}</TableCell><TableCell>{money(value.unitCost)}</TableCell><TableCell>{money(value.totalValue)}</TableCell></TableRow>)}</TableBody></Table></div>
      <div className="space-y-2"><Label>Action comment / findings</Label><Textarea value={comment} onChange={event => setComment(event.target.value)} placeholder="Required explanation, findings or decision reason" /></div>

      {selected.status === 1 && <div className="flex flex-wrap gap-2"><Button onClick={() => mutate(value => inventoryDisposalService.verify(value, true, comment, []), 'Independent audit verification recorded.')} disabled={saving || !comment.trim()}><ClipboardCheck className="mr-2 h-4 w-4" />Verify</Button><Button variant="destructive" onClick={() => mutate(value => inventoryDisposalService.verify(value, false, comment, []), 'Disposal case rejected by audit verification.')} disabled={saving || !comment.trim()}>Reject finding</Button></div>}
      {selected.status === 2 && <div className="space-y-3 rounded-md border p-4"><h3 className="font-medium"><Users className="mr-2 inline h-4 w-4" />Schedule disposal committee</h3><div className="grid gap-3 md:grid-cols-2"><div><Label>Meeting</Label><Input type="datetime-local" value={meetingAt} onChange={event => setMeetingAt(event.target.value)} /></div><div><Label>Committee reference</Label><Input value={committeeReference} onChange={event => setCommitteeReference(event.target.value)} /></div></div><Label>Appointed members (active TDC disposal committee role; requester/auditor excluded)</Label><select multiple className="min-h-32 w-full rounded-md border bg-background p-2 text-sm" value={memberIds} onChange={event => setMemberIds(Array.from(event.target.selectedOptions, option => option.value))}>{committeeCandidates.map(value => <option key={value.id} value={value.id}>{value.firstName} {value.lastName} · {value.email}</option>)}</select>{committeeCandidates.length < 3 && <p className="text-sm text-amber-700">At least three eligible users must be assigned the TDC Disposal Committee Member role before scheduling.</p>}<Button onClick={() => mutate(value => inventoryDisposalService.schedule(value, new Date(meetingAt).toISOString(), committeeReference, memberIds, comment), 'Disposal committee scheduled.')} disabled={saving || !meetingAt || !committeeReference.trim() || memberIds.length < 3}><Users className="mr-2 h-4 w-4" />Schedule committee</Button></div>}
      {selected.status === 3 && <div className="flex flex-wrap gap-2"><Button onClick={() => mutate(value => inventoryDisposalService.vote(value, true, false, comment), 'Committee recommendation recorded.')} disabled={saving}><Gavel className="mr-2 h-4 w-4" />Recommend approval</Button><Button variant="outline" onClick={() => mutate(value => inventoryDisposalService.vote(value, false, false, comment), 'Committee rejection vote recorded.')} disabled={saving}>Recommend rejection</Button><Button variant="secondary" onClick={() => mutate(value => inventoryDisposalService.vote(value, false, true, comment), 'Conflict declared; no vote counted.')} disabled={saving}>Declare conflict</Button></div>}
      {selected.status === 4 && <Button onClick={() => mutate(value => inventoryDisposalService.submit(value, comment), 'Disposal case submitted to configured authority workflow.')} disabled={saving}><FileCheck2 className="mr-2 h-4 w-4" />Submit authority route</Button>}
      {selected.status === 5 && <div className="flex gap-2"><Button onClick={() => mutate(value => inventoryDisposalService.decide(value, true, comment), 'Workflow approval step processed.')} disabled={saving}><CheckCircle2 className="mr-2 h-4 w-4" />Approve assigned step</Button><Button variant="destructive" onClick={() => mutate(value => inventoryDisposalService.decide(value, false, comment), 'Disposal rejected.')} disabled={saving || !comment.trim()}>Reject</Button></div>}
      {selected.status === 6 && <div className="space-y-3 rounded-md border p-4"><h3 className="font-medium">Stage governed stock/Finance execution</h3><div className="grid gap-3 md:grid-cols-4"><div><Label>Execution reference</Label><Input value={executionReference} onChange={event => setExecutionReference(event.target.value)} /></div><div><Label>Buyer / recipient</Label><Input value={buyer} onChange={event => setBuyer(event.target.value)} /></div><div><Label>Proceeds</Label><Input type="number" min={0} value={proceeds} onChange={event => setProceeds(Number(event.target.value))} /></div><div><Label>Finance proceeds account ID</Label><Input value={proceedsAccountId} onChange={event => setProceedsAccountId(event.target.value)} placeholder="Required for auction/sale" /></div></div><Button onClick={() => mutate(async value => inventoryDisposalService.stageExecution(value, { proceedsAmount: proceeds, proceedsAccountId: proceedsAccountId || undefined, buyerOrRecipient: buyer || undefined, executionReference, evidence: await controlledEvidence(), comment }), 'Controlled stock adjustment staged and submitted.')} disabled={saving || !executionReference.trim() || !documentId}><Gavel className="mr-2 h-4 w-4" />Stage execution</Button></div>}
      {selected.status === 7 && <div className="flex flex-wrap items-center gap-3"><Button onClick={() => mutate(value => inventoryDisposalService.complete(value, comment), 'Controlled stock write-off and Finance completion processed.')} disabled={saving}><CheckCircle2 className="mr-2 h-4 w-4" />Approve/post linked adjustment and complete</Button><Button asChild variant="outline"><Link href="/inventory/adjustments">Open Stock Adjustments</Link></Button></div>}

      <div><h3 className="mb-2 font-medium"><History className="mr-2 inline h-4 w-4" />Immutable action history</h3><div className="space-y-2">{selected.actions.map(value => <div key={value.sequence} className="rounded-md border p-3 text-sm"><div className="flex justify-between"><span className="font-medium">#{value.sequence} {action[value.actionType]}</span><span className="text-xs text-muted-foreground">{new Date(value.occurredAtUtc).toLocaleString()}</span></div><p className="text-xs text-muted-foreground">{value.actorName || value.actorUserId}</p>{value.comment && <p className="mt-1">{value.comment}</p>}</div>)}</div></div>
      </CardContent></Card>}
  </div>;
}
