'use client';

import { useEffect, useState } from 'react';
import { AxiosError } from 'axios';
import { AlertTriangle, Loader2, RefreshCw, ShieldCheck } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { inventoryNegativeStockControlService, InventoryNegativeStockOverride, InventoryNegativeStockPolicy } from '@/services/inventoryNegativeStockControlService';

type Problem = { detail?: string; message?: string; title?: string };
const messageFrom = (error: unknown, fallback: string) => {
  const value = (error as AxiosError<Problem>)?.response?.data;
  return value?.detail || value?.message || value?.title || (error instanceof Error ? error.message : fallback);
};
export default function InventoryNegativeStockControlsPage() {
  const [policy, setPolicy] = useState<InventoryNegativeStockPolicy>();
  const [overrides, setOverrides] = useState<InventoryNegativeStockOverride[]>([]);
  const [loading, setLoading] = useState(true);

  const refresh = async () => {
    setLoading(true);
    try {
      const [loadedPolicy, loadedOverrides] = await Promise.all([
        inventoryNegativeStockControlService.getPolicy(), inventoryNegativeStockControlService.getOverrides(),
      ]);
      setPolicy(loadedPolicy); setOverrides(loadedOverrides);
    } catch (error) { toast.error(messageFrom(error, 'Unable to load negative-stock controls.')); }
    finally { setLoading(false); }
  };
  useEffect(() => { void refresh(); }, []);

  const policyName = policy?.defaultPolicy === 1 || String(policy?.defaultPolicy).toLowerCase().includes('controlled')
    ? 'Controlled emergency override' : 'Prohibited';
  return <div className="space-y-6 p-6">
    <div className="flex flex-wrap items-center justify-between gap-3"><div><h1 className="text-2xl font-semibold">Negative Stock Controls</h1><p className="text-sm text-muted-foreground">One DEC-010 policy, transaction-safe stock locks, and independently approved emergency evidence.</p></div><Button variant="outline" onClick={() => void refresh()} disabled={loading}>{loading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <RefreshCw className="mr-2 h-4 w-4" />}Refresh</Button></div>
    <Breadcrumb><BreadcrumbList><BreadcrumbItem><BreadcrumbLink href="/inventory">Inventory</BreadcrumbLink></BreadcrumbItem><BreadcrumbSeparator /><BreadcrumbItem><BreadcrumbPage>Negative stock controls</BreadcrumbPage></BreadcrumbItem></BreadcrumbList></Breadcrumb>

    <Card><CardHeader><CardTitle><ShieldCheck className="mr-2 inline h-5 w-5" />Effective DEC-010 policy</CardTitle><CardDescription>Missing or incomplete configuration fails closed. Emergency use is exact, expiring, single-use, workflow-approved, and backed by current malware-clean DMS evidence.</CardDescription></CardHeader><CardContent>{policy ? <div className="flex flex-wrap gap-2"><Badge variant={policyName === 'Prohibited' ? 'destructive' : 'secondary'}>{policyName}</Badge><Badge variant="outline">Profile v{policy.configurationProfileVersion}</Badge><Badge variant="outline">{policy.overrideDurationHours}h maximum</Badge><Badge variant="outline">{policy.overridePermission}</Badge><Badge variant="outline">{policy.auditRequired ? 'Audit required' : 'Audit not configured'}</Badge>{policy.evidenceRequirements.map(value => <Badge key={value} variant="secondary">{value}</Badge>)}</div> : <div className="text-sm text-muted-foreground">No effective policy loaded.</div>}</CardContent></Card>

    <Card><CardHeader><CardTitle><AlertTriangle className="mr-2 inline h-5 w-5" />Controlled exception initiation</CardTitle><CardDescription>Manual technical-reference entry is disabled. Negative stock remains prohibited unless a source inventory transaction starts a configured exception route and an independent approver completes it with current central-DMS evidence.</CardDescription></CardHeader><CardContent><p className="text-sm text-muted-foreground">Use available stock for the normal route. If emergency negative stock is configured, initiate it from the issue, transfer, return, or adjustment being processed so the system supplies the item, warehouse, transaction, workflow, and evidence lineage automatically.</p></CardContent></Card>

    <Card><CardHeader><CardTitle>Override register</CardTitle><CardDescription>Each authorization is terminal after consumption or expiry and retains its configuration, workflow, evidence, actor, and transaction lineage.</CardDescription></CardHeader><CardContent><div className="overflow-auto rounded-md border"><Table><TableHeader><TableRow><TableHead>Status</TableHead><TableHead>Item / warehouse</TableHead><TableHead>Reference</TableHead><TableHead>Quantity</TableHead><TableHead>Approval / evidence</TableHead><TableHead>Expiry</TableHead></TableRow></TableHeader><TableBody>{overrides.length === 0 ? <TableRow><TableCell colSpan={6} className="h-24 text-center text-muted-foreground">No negative-stock overrides.</TableCell></TableRow> : overrides.map(value => <TableRow key={value.id}><TableCell><Badge variant={value.isAvailable ? 'secondary' : 'outline'}>{value.isAvailable ? 'Available' : value.consumedAtUtc ? 'Consumed' : 'Expired'}</Badge></TableCell><TableCell><div className="font-medium">{value.itemCode} · {value.itemName}</div><div className="text-xs text-muted-foreground">{value.warehouseName}</div></TableCell><TableCell>{value.referenceNumber}<div className="text-xs text-muted-foreground">{value.referenceType}</div></TableCell><TableCell>{value.authorizedQuantity}</TableCell><TableCell className="text-xs"><Badge variant="outline">Independent approval retained</Badge><div className="mt-1 text-muted-foreground">Central-DMS evidence retained</div></TableCell><TableCell className="whitespace-nowrap text-xs">{new Date(value.expiresAtUtc).toLocaleString()}</TableCell></TableRow>)}</TableBody></Table></div></CardContent></Card>
  </div>;
}
