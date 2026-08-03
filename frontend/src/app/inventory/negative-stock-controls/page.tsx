'use client';

import { useEffect, useState } from 'react';
import { AxiosError } from 'axios';
import { AlertTriangle, Loader2, RefreshCw, Save, ShieldCheck } from 'lucide-react';
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
import { inventoryManagementService, InventoryItemDto, WarehouseDto } from '@/services/inventoryManagementService';
import { inventoryNegativeStockControlService, InventoryNegativeStockOverride, InventoryNegativeStockPolicy, RegisterInventoryNegativeStockOverride } from '@/services/inventoryNegativeStockControlService';

type Problem = { detail?: string; message?: string; title?: string };
const messageFrom = (error: unknown, fallback: string) => {
  const value = (error as AxiosError<Problem>)?.response?.data;
  return value?.detail || value?.message || value?.title || (error instanceof Error ? error.message : fallback);
};
const blank = (): RegisterInventoryNegativeStockOverride => ({
  inventoryItemId: '', warehouseId: '', referenceId: '', referenceType: 'InventoryTransaction', referenceNumber: '',
  authorizedQuantity: 1, reason: '', workflowInstanceId: '', centralDocumentVersionId: '', evidenceReference: '',
  expiresAtUtc: new Date(Date.now() + 3600000).toISOString().slice(0, 16),
});

export default function InventoryNegativeStockControlsPage() {
  const [policy, setPolicy] = useState<InventoryNegativeStockPolicy>();
  const [overrides, setOverrides] = useState<InventoryNegativeStockOverride[]>([]);
  const [items, setItems] = useState<InventoryItemDto[]>([]);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [draft, setDraft] = useState(blank());
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const refresh = async () => {
    setLoading(true);
    try {
      const [loadedPolicy, loadedOverrides, loadedItems, loadedWarehouses] = await Promise.all([
        inventoryNegativeStockControlService.getPolicy(), inventoryNegativeStockControlService.getOverrides(),
        inventoryManagementService.getInventoryItems({ isActive: true }), inventoryManagementService.getWarehouses(true),
      ]);
      setPolicy(loadedPolicy); setOverrides(loadedOverrides); setItems(loadedItems); setWarehouses(loadedWarehouses);
    } catch (error) { toast.error(messageFrom(error, 'Unable to load negative-stock controls.')); }
    finally { setLoading(false); }
  };
  useEffect(() => { void refresh(); }, []);

  const register = async () => {
    if (!draft.inventoryItemId || !draft.warehouseId || !draft.referenceId || !draft.referenceNumber ||
      !draft.workflowInstanceId || !draft.centralDocumentVersionId || !draft.evidenceReference || draft.reason.trim().length < 10) {
      toast.error('Complete the exact stock reference, completed workflow, current DMS evidence, quantity, and emergency reason.'); return;
    }
    setSaving(true);
    try {
      const saved = await inventoryNegativeStockControlService.registerOverride({ ...draft, expiresAtUtc: new Date(draft.expiresAtUtc).toISOString() });
      setOverrides(values => [saved, ...values]); setDraft(blank());
      toast.success('Independently approved DEC-010 override registered.');
    } catch (error) { toast.error(messageFrom(error, 'Unable to register the override.')); }
    finally { setSaving(false); }
  };

  const policyName = policy?.defaultPolicy === 1 || String(policy?.defaultPolicy).toLowerCase().includes('controlled')
    ? 'Controlled emergency override' : 'Prohibited';
  return <div className="space-y-6 p-6">
    <div className="flex flex-wrap items-center justify-between gap-3"><div><h1 className="text-2xl font-semibold">Negative Stock Controls</h1><p className="text-sm text-muted-foreground">One DEC-010 policy, transaction-safe stock locks, and independently approved emergency evidence.</p></div><Button variant="outline" onClick={() => void refresh()} disabled={loading}>{loading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <RefreshCw className="mr-2 h-4 w-4" />}Refresh</Button></div>
    <Breadcrumb><BreadcrumbList><BreadcrumbItem><BreadcrumbLink href="/inventory">Inventory</BreadcrumbLink></BreadcrumbItem><BreadcrumbSeparator /><BreadcrumbItem><BreadcrumbPage>Negative stock controls</BreadcrumbPage></BreadcrumbItem></BreadcrumbList></Breadcrumb>

    <Card><CardHeader><CardTitle><ShieldCheck className="mr-2 inline h-5 w-5" />Effective DEC-010 policy</CardTitle><CardDescription>Missing or incomplete configuration fails closed. Emergency use is exact, expiring, single-use, workflow-approved, and backed by current malware-clean DMS evidence.</CardDescription></CardHeader><CardContent>{policy ? <div className="flex flex-wrap gap-2"><Badge variant={policyName === 'Prohibited' ? 'destructive' : 'secondary'}>{policyName}</Badge><Badge variant="outline">Profile v{policy.configurationProfileVersion}</Badge><Badge variant="outline">{policy.overrideDurationHours}h maximum</Badge><Badge variant="outline">{policy.overridePermission}</Badge><Badge variant="outline">{policy.auditRequired ? 'Audit required' : 'Audit not configured'}</Badge>{policy.evidenceRequirements.map(value => <Badge key={value} variant="secondary">{value}</Badge>)}</div> : <div className="text-sm text-muted-foreground">No effective policy loaded.</div>}</CardContent></Card>

    <Card><CardHeader><CardTitle><AlertTriangle className="mr-2 inline h-5 w-5" />Register approved emergency override</CardTitle><CardDescription>This screen does not approve an exception. It binds an already completed independent workflow and an existing current Published central-DMS version to one stock line.</CardDescription></CardHeader><CardContent className="space-y-4">
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
        <div className="space-y-2"><Label>Item</Label><Select value={draft.inventoryItemId} onValueChange={value => setDraft(current => ({ ...current, inventoryItemId: value }))}><SelectTrigger><SelectValue placeholder="Select item" /></SelectTrigger><SelectContent>{items.map(value => <SelectItem key={value.id} value={value.id}>{value.itemCode} · {value.name}</SelectItem>)}</SelectContent></Select></div>
        <div className="space-y-2"><Label>Warehouse</Label><Select value={draft.warehouseId} onValueChange={value => setDraft(current => ({ ...current, warehouseId: value }))}><SelectTrigger><SelectValue placeholder="Select warehouse" /></SelectTrigger><SelectContent>{warehouses.map(value => <SelectItem key={value.id} value={value.id}>{value.code} · {value.name}</SelectItem>)}</SelectContent></Select></div>
        <div className="space-y-2"><Label>Authorized quantity</Label><Input type="number" min="0.0001" step="0.0001" value={draft.authorizedQuantity} onChange={event => setDraft(value => ({ ...value, authorizedQuantity: Number(event.target.value) }))} /></div>
        <div className="space-y-2"><Label>Reference type</Label><Input value={draft.referenceType} onChange={event => setDraft(value => ({ ...value, referenceType: event.target.value }))} /></div>
        <div className="space-y-2"><Label>Reference number</Label><Input value={draft.referenceNumber} onChange={event => setDraft(value => ({ ...value, referenceNumber: event.target.value }))} /></div>
        <div className="space-y-2"><Label>Reference ID</Label><Input value={draft.referenceId} onChange={event => setDraft(value => ({ ...value, referenceId: event.target.value }))} /></div>
        <div className="space-y-2"><Label>Reference line ID</Label><Input value={draft.referenceLineId || ''} onChange={event => setDraft(value => ({ ...value, referenceLineId: event.target.value || undefined }))} /></div>
        <div className="space-y-2"><Label>Completed workflow instance ID</Label><Input value={draft.workflowInstanceId} onChange={event => setDraft(value => ({ ...value, workflowInstanceId: event.target.value }))} /></div>
        <div className="space-y-2"><Label>Current DMS version ID</Label><Input value={draft.centralDocumentVersionId} onChange={event => setDraft(value => ({ ...value, centralDocumentVersionId: event.target.value }))} /></div>
        <div className="space-y-2"><Label>Evidence reference</Label><Input value={draft.evidenceReference} onChange={event => setDraft(value => ({ ...value, evidenceReference: event.target.value }))} placeholder="Emergency authority / incident reference" /></div>
        <div className="space-y-2"><Label>Expires</Label><Input type="datetime-local" value={draft.expiresAtUtc} onChange={event => setDraft(value => ({ ...value, expiresAtUtc: event.target.value }))} /></div>
      </div>
      <div className="space-y-2"><Label>Emergency reason</Label><Textarea value={draft.reason} onChange={event => setDraft(value => ({ ...value, reason: event.target.value }))} /></div>
      <Button onClick={() => void register()} disabled={saving || !policy?.emergencyOverrideEligible}>{saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}Register override</Button>
    </CardContent></Card>

    <Card><CardHeader><CardTitle>Override register</CardTitle><CardDescription>Each authorization is terminal after consumption or expiry and retains its configuration, workflow, evidence, actor, and transaction lineage.</CardDescription></CardHeader><CardContent><div className="overflow-auto rounded-md border"><Table><TableHeader><TableRow><TableHead>Status</TableHead><TableHead>Item / warehouse</TableHead><TableHead>Reference</TableHead><TableHead>Quantity</TableHead><TableHead>Approval / evidence</TableHead><TableHead>Expiry</TableHead></TableRow></TableHeader><TableBody>{overrides.length === 0 ? <TableRow><TableCell colSpan={6} className="h-24 text-center text-muted-foreground">No negative-stock overrides.</TableCell></TableRow> : overrides.map(value => <TableRow key={value.id}><TableCell><Badge variant={value.isAvailable ? 'secondary' : 'outline'}>{value.isAvailable ? 'Available' : value.consumedAtUtc ? 'Consumed' : 'Expired'}</Badge></TableCell><TableCell><div className="font-medium">{value.itemCode} · {value.itemName}</div><div className="text-xs text-muted-foreground">{value.warehouseName}</div></TableCell><TableCell>{value.referenceNumber}<div className="text-xs text-muted-foreground">{value.referenceType}</div></TableCell><TableCell>{value.authorizedQuantity}</TableCell><TableCell className="text-xs">Workflow {value.workflowInstanceId.slice(0, 8)}…<div className="text-muted-foreground">DMS {value.centralDocumentVersionId.slice(0, 8)}…</div></TableCell><TableCell className="whitespace-nowrap text-xs">{new Date(value.expiresAtUtc).toLocaleString()}</TableCell></TableRow>)}</TableBody></Table></div></CardContent></Card>
  </div>;
}
