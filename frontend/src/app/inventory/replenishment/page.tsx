'use client';

import { useEffect, useMemo, useState } from 'react';
import { AxiosError } from 'axios';
import { BellRing, Calculator, CheckCircle2, FilePlus2, History, Loader2, RefreshCw, Send, ShieldCheck, XCircle } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { inventoryManagementService, WarehouseDto } from '@/services/inventoryManagementService';
import { inventoryReplenishmentService, InventoryReplenishmentRecommendation } from '@/services/inventoryReplenishmentService';

const statusLabels: Record<number, string> = {
  1: 'Draft', 2: 'Pending approval', 3: 'Approved', 4: 'Rejected',
  5: 'Draft PR created', 6: 'Expired', 7: 'Cancelled',
};
const actionLabels: Record<number, string> = {
  1: 'Generated', 2: 'Alert sent', 3: 'Submitted', 4: 'Approved', 5: 'Rejected',
  6: 'Draft PR created', 7: 'Cancelled', 8: 'Expired', 9: 'Approval progressed',
};
type Problem = { detail?: string; message?: string; title?: string };
const errorMessage = (error: unknown, fallback: string) => {
  const value = (error as AxiosError<Problem>)?.response?.data;
  return value?.detail || value?.message || value?.title || (error instanceof Error ? error.message : fallback);
};

export default function InventoryReplenishmentPage() {
  const [recommendations, setRecommendations] = useState<InventoryReplenishmentRecommendation[]>([]);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [selected, setSelected] = useState<InventoryReplenishmentRecommendation>();
  const [warehouseId, setWarehouseId] = useState('');
  const [warehouseFilter, setWarehouseFilter] = useState('all');
  const [statusFilter, setStatusFilter] = useState('all');
  const [demandWindowDays, setDemandWindowDays] = useState(90);
  const [reason, setReason] = useState('Review calculated replenishment requirement.');
  const [decisionComment, setDecisionComment] = useState('Reviewed current stock, demand and open supply.');
  const [department, setDepartment] = useState('Stores');
  const [costCenter, setCostCenter] = useState('');
  const [justification, setJustification] = useState('Approved replenishment required to maintain service levels.');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const refresh = async () => {
    setLoading(true);
    try {
      const [register, activeWarehouses] = await Promise.all([
        inventoryReplenishmentService.getAll({ take: 500 }), inventoryManagementService.getActiveWarehouses(),
      ]);
      setRecommendations(register); setWarehouses(activeWarehouses);
      if (!warehouseId && activeWarehouses.length) setWarehouseId(activeWarehouses[0].id);
      if (selected) setSelected(await inventoryReplenishmentService.getById(selected.id));
    } catch (error) { toast.error(errorMessage(error, 'Unable to load replenishment controls.')); }
    finally { setLoading(false); }
  };
  useEffect(() => { void refresh(); }, []);

  const update = (value: InventoryReplenishmentRecommendation) => {
    setRecommendations(current => current.map(item => item.id === value.id ? value : item));
    setSelected(value);
  };
  const run = async (work: () => Promise<void>) => {
    setSaving(true); try { await work(); } catch (error) { toast.error(errorMessage(error, 'The controlled action failed.')); }
    finally { setSaving(false); }
  };
  const generate = () => run(async () => {
    if (!warehouseId) throw new Error('Select an active warehouse.');
    const values = await inventoryReplenishmentService.generate(warehouseId, demandWindowDays);
    setRecommendations(current => [...values, ...current.filter(item => !values.some(value => value.id === item.id))]);
    if (values.length) setSelected(values[0]);
    toast.success(values.length ? `${values.length} explainable recommendation(s) ready.` : 'No item currently requires replenishment.');
  });
  const submit = () => selected && run(async () => {
    update(await inventoryReplenishmentService.submit(selected, reason));
    toast.success('Recommendation submitted to the shared approval workflow.');
  });
  const decide = (approved: boolean) => selected && run(async () => {
    update(await inventoryReplenishmentService.decide(selected, approved, decisionComment));
    toast.success(approved ? 'Approval decision recorded.' : 'Recommendation rejected.');
  });
  const createPr = () => selected && run(async () => {
    update(await inventoryReplenishmentService.createPurchaseRequisition(selected,
      { department, costCenter: costCenter || undefined, justification }));
    toast.success('Governed Draft Stock Replenishment PR created; normal PR submission controls remain mandatory.');
  });
  const openDetail = async (value: InventoryReplenishmentRecommendation) => {
    try { setSelected(await inventoryReplenishmentService.getById(value.id)); }
    catch (error) { toast.error(errorMessage(error, 'Unable to load recommendation history.')); }
  };

  const filtered = useMemo(() => recommendations.filter(value =>
    (warehouseFilter === 'all' || value.warehouseId === warehouseFilter) &&
    (statusFilter === 'all' || value.status === Number(statusFilter))),
  [recommendations, warehouseFilter, statusFilter]);
  const open = recommendations.filter(value => value.status <= 3);

  return <div className="space-y-6 p-6">
    <div className="flex flex-wrap items-center justify-between gap-3"><div><h1 className="text-2xl font-semibold">Inventory Replenishment</h1><p className="text-sm text-muted-foreground">Explain demand, approve recommendations, alert responsible actors, and create governed Draft purchase requisitions.</p></div><Button variant="outline" onClick={() => void refresh()} disabled={loading}>{loading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <RefreshCw className="mr-2 h-4 w-4" />}Refresh</Button></div>

    <div className="grid gap-4 md:grid-cols-3"><Card><CardHeader className="pb-2"><CardDescription>Open recommendations</CardDescription><CardTitle>{open.length}</CardTitle></CardHeader></Card><Card><CardHeader className="pb-2"><CardDescription>Recommended quantity</CardDescription><CardTitle>{open.reduce((sum, value) => sum + value.recommendedQuantity, 0).toLocaleString()}</CardTitle></CardHeader></Card><Card><CardHeader className="pb-2"><CardDescription>Draft PRs created</CardDescription><CardTitle>{recommendations.filter(value => value.status === 5).length}</CardTitle></CardHeader></Card></div>

    <Card><CardHeader><CardTitle><ShieldCheck className="mr-2 inline h-5 w-5" />Shared-control boundary</CardTitle><CardDescription>InventoryItem and WarehouseQuantity remain the threshold and stock owners; ItemSupplier owns source lead time/MOQ, StockMovement owns demand, central notifications and shared workflow own alerts/approval, and Purchase Requisition remains the only procurement-demand owner. This control creates Draft PRs only and cannot bypass APP, budget, authority, sourcing or later PO gates.</CardDescription></CardHeader></Card>

    <Card><CardHeader><CardTitle><Calculator className="mr-2 inline h-5 w-5" />Calculate recommendations</CardTitle><CardDescription>Deterministic warehouse calculation includes available/allocated stock, min/max/reorder/safety levels, lead-time demand, open PO supply, existing replenishment PRs, preferred-supplier MOQ and order multiple.</CardDescription></CardHeader><CardContent><div className="flex flex-wrap items-end gap-3"><div className="min-w-64 space-y-2"><Label>Warehouse</Label><Select value={warehouseId} onValueChange={setWarehouseId}><SelectTrigger><SelectValue placeholder="Select active warehouse" /></SelectTrigger><SelectContent>{warehouses.map(value => <SelectItem key={value.id} value={value.id}>{value.code} · {value.name}</SelectItem>)}</SelectContent></Select></div><div className="w-48 space-y-2"><Label>Demand window (days)</Label><Input type="number" min={7} max={365} value={demandWindowDays} onChange={event => setDemandWindowDays(Number(event.target.value))} /></div><Button onClick={() => void generate()} disabled={saving || !warehouseId}>{saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <BellRing className="mr-2 h-4 w-4" />}Calculate and alert</Button></div></CardContent></Card>

    <Card><CardHeader><CardTitle>Recommendation register</CardTitle><CardDescription>Tenant-safe, warehouse-filtered recommendation, workflow and Draft-PR lineage.</CardDescription></CardHeader><CardContent className="space-y-4"><div className="grid gap-3 md:grid-cols-2"><Select value={warehouseFilter} onValueChange={setWarehouseFilter}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">All warehouses</SelectItem>{warehouses.map(value => <SelectItem key={value.id} value={value.id}>{value.code} · {value.name}</SelectItem>)}</SelectContent></Select><Select value={statusFilter} onValueChange={setStatusFilter}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">All statuses</SelectItem>{Object.entries(statusLabels).map(([id, label]) => <SelectItem key={id} value={id}>{label}</SelectItem>)}</SelectContent></Select></div><div className="overflow-auto rounded-md border"><Table><TableHeader><TableRow><TableHead>Status</TableHead><TableHead>Warehouse / item</TableHead><TableHead>Position</TableHead><TableHead>Demand / lead</TableHead><TableHead>Recommended</TableHead><TableHead>Supplier</TableHead><TableHead>Valid until</TableHead><TableHead /></TableRow></TableHeader><TableBody>{filtered.length === 0 ? <TableRow><TableCell colSpan={8} className="h-24 text-center text-muted-foreground">No replenishment recommendations match the filters.</TableCell></TableRow> : filtered.map(value => <TableRow key={value.id}><TableCell><Badge variant={value.status <= 3 ? 'secondary' : 'outline'}>{statusLabels[value.status]}</Badge></TableCell><TableCell><div className="font-medium">{value.warehouseCode} · {value.itemCode}</div><div className="text-xs text-muted-foreground">{value.itemName}</div></TableCell><TableCell className="text-xs">Available {value.availableStock}<br />PO {value.onOrderQuantity} · PR {value.openRecommendationQuantity}</TableCell><TableCell className="text-xs">{value.demandQuantity} / {value.demandWindowDays}d<br />{value.leadTimeDays}+{value.safetyLeadTimeDays} lead days</TableCell><TableCell className="font-medium">{value.recommendedQuantity} {value.unitOfMeasure}</TableCell><TableCell>{value.preferredSupplierName || 'Not assigned'}</TableCell><TableCell className="whitespace-nowrap text-xs">{new Date(value.validUntilUtc).toLocaleString()}</TableCell><TableCell><Button size="sm" variant="outline" onClick={() => void openDetail(value)}><History className="mr-2 h-4 w-4" />Review</Button></TableCell></TableRow>)}</TableBody></Table></div></CardContent></Card>

    {selected && <Card><CardHeader><CardTitle>{selected.recommendationNumber} · {selected.itemCode}</CardTitle><CardDescription>{selected.explanation}</CardDescription></CardHeader><CardContent className="space-y-6"><div className="grid gap-3 text-sm md:grid-cols-3 xl:grid-cols-6"><div><Label>Reorder point</Label><p>{selected.reorderLevel}</p></div><div><Label>Safety stock</Label><p>{selected.safetyStock}</p></div><div><Label>Lead demand</Label><p>{selected.leadTimeDemand}</p></div><div><Label>Projected receipt stock</Label><p>{selected.projectedAvailableAtReceipt}</p></div><div><Label>MOQ / multiple</Label><p>{selected.minimumOrderQuantity} / {selected.orderMultiple}</p></div><div><Label>Calculation hash</Label><p className="font-mono text-xs">{selected.calculationHash.slice(0, 16)}…</p></div></div>
      {selected.status === 1 && <div className="space-y-2 rounded-md border p-4"><Label>Submission reason</Label><Textarea value={reason} onChange={event => setReason(event.target.value)} /><Button onClick={() => void submit()} disabled={saving}><Send className="mr-2 h-4 w-4" />Submit for approval</Button></div>}
      {selected.status === 2 && <div className="space-y-2 rounded-md border p-4"><Label>Independent workflow decision</Label><Textarea value={decisionComment} onChange={event => setDecisionComment(event.target.value)} /><div className="flex gap-2"><Button onClick={() => void decide(true)} disabled={saving}><CheckCircle2 className="mr-2 h-4 w-4" />Approve current step</Button><Button variant="destructive" onClick={() => void decide(false)} disabled={saving}><XCircle className="mr-2 h-4 w-4" />Reject</Button></div></div>}
      {selected.status === 3 && <div className="space-y-3 rounded-md border p-4"><h3 className="font-medium">Create governed Draft Stock Replenishment PR</h3><div className="grid gap-3 md:grid-cols-2"><div className="space-y-2"><Label>Department</Label><Input value={department} onChange={event => setDepartment(event.target.value)} /></div><div className="space-y-2"><Label>Cost centre (optional)</Label><Input value={costCenter} onChange={event => setCostCenter(event.target.value)} /></div></div><Label>Justification</Label><Textarea value={justification} onChange={event => setJustification(event.target.value)} /><Button onClick={() => void createPr()} disabled={saving}><FilePlus2 className="mr-2 h-4 w-4" />Create Draft PR</Button></div>}
      {selected.purchaseRequisitionId && <div className="rounded-md border p-4 text-sm"><strong>Purchase requisition:</strong> <a className="text-primary underline" href={`/procurement/purchase-requisitions/${selected.purchaseRequisitionId}`}>{selected.purchaseRequisitionNumber}</a><p className="text-muted-foreground">Draft only; standard submission, APP, budget, authority and sourcing controls remain mandatory.</p></div>}
      <div><h3 className="mb-2 font-medium"><History className="mr-2 inline h-4 w-4" />Immutable action history</h3><div className="space-y-2">{selected.actions.map(action => <div key={`${selected.id}-${action.sequence}`} className="rounded-md border p-3 text-sm"><div className="flex justify-between gap-3"><span className="font-medium">#{action.sequence} {actionLabels[action.actionType]}</span><span className="text-xs text-muted-foreground">{new Date(action.occurredAtUtc).toLocaleString()}</span></div><div className="text-xs text-muted-foreground">{action.actorName} · {action.integrityHash.slice(0, 16)}…</div>{action.reason && <p className="mt-1">{action.reason}</p>}</div>)}</div></div>
    </CardContent></Card>}
  </div>;
}
