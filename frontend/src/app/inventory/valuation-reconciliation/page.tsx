'use client';

import { useEffect, useMemo, useState } from 'react';
import { AxiosError } from 'axios';
import { AlertTriangle, Calculator, CheckCircle2, FileLock2, History, Loader2, RefreshCw, Scale, ShieldCheck } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { financeDataService } from '@/services/finance/finance-data.service';
import { inventoryValuationReconciliationService, InventoryValuationReconciliation } from '@/services/inventoryValuationReconciliationService';
import type { FiscalPeriod } from '@/types/finance';

const statusLabel: Record<number, string> = { 0: 'Exceptions', 1: 'Reconciled', 2: 'Frozen' };
const actionLabel: Record<number, string> = { 0: 'Generated', 1: 'Frozen' };
type Problem = { detail?: string; message?: string; title?: string };
const errorMessage = (error: unknown, fallback: string) => {
  const value = (error as AxiosError<Problem>)?.response?.data;
  return value?.detail || value?.message || value?.title || (error instanceof Error ? error.message : fallback);
};
const money = (value: number, currency = 'GHS') =>
  new Intl.NumberFormat(undefined, { style: 'currency', currency, maximumFractionDigits: 2 }).format(value || 0);

export default function InventoryValuationReconciliationPage() {
  const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
  const [rows, setRows] = useState<InventoryValuationReconciliation[]>([]);
  const [selected, setSelected] = useState<InventoryValuationReconciliation>();
  const [periodId, setPeriodId] = useState('');
  const [status, setStatus] = useState('all');
  const [tolerance, setTolerance] = useState(0.01);
  const [reason, setReason] = useState('Inventory movement, landed-cost and GL balances independently reviewed at year-end cut-off.');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const refresh = async () => {
    setLoading(true);
    try {
      const [register, fiscalPeriods] = await Promise.all([
        inventoryValuationReconciliationService.getAll({ take: 500 }), financeDataService.getFiscalPeriods(),
      ]);
      setRows(register); setPeriods(fiscalPeriods);
      const open = fiscalPeriods.filter(value => value.isOpen || value.periodStatus === 'Open');
      if (!periodId && open.length) setPeriodId((open.find(value => (value as FiscalPeriod & { isYearEnd?: boolean }).isYearEnd) || open[0]).id);
      if (selected) setSelected(await inventoryValuationReconciliationService.getById(selected.id));
    } catch (error) { toast.error(errorMessage(error, 'Unable to load inventory valuation reconciliation.')); }
    finally { setLoading(false); }
  };
  useEffect(() => { void refresh(); }, []);

  const run = async (work: () => Promise<void>) => {
    setSaving(true); try { await work(); } catch (error) { toast.error(errorMessage(error, 'The controlled action failed.')); }
    finally { setSaving(false); }
  };
  const generate = () => run(async () => {
    if (!periodId) throw new Error('Select an open fiscal period.');
    const value = await inventoryValuationReconciliationService.generate(periodId, tolerance);
    setRows(current => [value, ...current.filter(item => item.id !== value.id)]); setSelected(value);
    toast.success(value.exceptionCount === 0 ? 'Clean reconciliation snapshot generated.' : `${value.exceptionCount} exception(s) require correction and regeneration.`);
  });
  const freeze = () => selected && run(async () => {
    const value = await inventoryValuationReconciliationService.freeze(selected, reason);
    setRows(current => current.map(item => item.id === value.id ? value : item)); setSelected(value);
    toast.success('Inventory valuation frozen and the Inventory module locked for this fiscal period.');
  });
  const openDetail = async (value: InventoryValuationReconciliation) => {
    try { setSelected(await inventoryValuationReconciliationService.getById(value.id)); }
    catch (error) { toast.error(errorMessage(error, 'Unable to load reconciliation evidence.')); }
  };
  const filtered = useMemo(() => rows.filter(value => status === 'all' || value.status === Number(status)), [rows, status]);
  const latest = rows[0];

  return <div className="space-y-6 p-6">
    <div className="flex flex-wrap items-center justify-between gap-3"><div><h1 className="text-2xl font-semibold">Inventory Valuation Reconciliation</h1><p className="text-sm text-muted-foreground">Reconcile accepted receipt cost, landed cost, valuation movements and the Inventory control account before year-end close.</p></div><Button variant="outline" onClick={() => void refresh()} disabled={loading}>{loading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <RefreshCw className="mr-2 h-4 w-4" />}Refresh</Button></div>

    <Card><CardHeader><CardTitle><ShieldCheck className="mr-2 inline h-5 w-5" />Shared-owner boundary</CardTitle><CardDescription>Posted InventoryMovement, InventoryLayer and InventoryBalance records remain the valuation owners. Landed Cost remains the freight/duty allocation owner, the Finance posting engine remains the only journal owner, and Fiscal Period remains the only close/lock owner. This page stores immutable reconciliation evidence and exceptions; it does not create another stock ledger, cost engine or close workflow.</CardDescription></CardHeader></Card>

    <div className="grid gap-4 md:grid-cols-4"><Card><CardHeader className="pb-2"><CardDescription>Latest status</CardDescription><CardTitle>{latest ? statusLabel[latest.status] : 'Not generated'}</CardTitle></CardHeader></Card><Card><CardHeader className="pb-2"><CardDescription>Movement subledger</CardDescription><CardTitle>{latest ? money(latest.inventorySubledgerValue, latest.functionalCurrencyCode) : '—'}</CardTitle></CardHeader></Card><Card><CardHeader className="pb-2"><CardDescription>Inventory GL</CardDescription><CardTitle>{latest ? money(latest.generalLedgerValue, latest.functionalCurrencyCode) : '—'}</CardTitle></CardHeader></Card><Card><CardHeader className="pb-2"><CardDescription>Open exceptions</CardDescription><CardTitle className={latest?.exceptionCount ? 'text-destructive' : ''}>{latest?.exceptionCount ?? 0}</CardTitle></CardHeader></Card></div>

    <Card><CardHeader><CardTitle><Calculator className="mr-2 inline h-5 w-5" />Generate cut-off snapshot</CardTitle><CardDescription>The calculation uses the exact fiscal-period end, posted movements, posted landed-cost allocations, current valuation-cache integrity and posted Inventory control-account transactions.</CardDescription></CardHeader><CardContent><div className="flex flex-wrap items-end gap-3"><div className="min-w-72 space-y-2"><Label>Open fiscal period</Label><Select value={periodId} onValueChange={setPeriodId}><SelectTrigger><SelectValue placeholder="Select period" /></SelectTrigger><SelectContent>{periods.filter(value => value.isOpen || value.periodStatus === 'Open').map(value => <SelectItem key={value.id} value={value.id}>{value.periodCode} · {value.periodName}</SelectItem>)}</SelectContent></Select></div><div className="w-44 space-y-2"><Label>Tolerance</Label><Input type="number" min={0} step="0.01" value={tolerance} onChange={event => setTolerance(Number(event.target.value))} /></div><Button onClick={() => void generate()} disabled={saving || !periodId}>{saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Scale className="mr-2 h-4 w-4" />}Generate reconciliation</Button></div></CardContent></Card>

    <Card><CardHeader><CardTitle>Reconciliation register</CardTitle><CardDescription>Every generation is immutable; correct source data and generate a new snapshot rather than editing evidence.</CardDescription></CardHeader><CardContent className="space-y-4"><div className="max-w-xs"><Select value={status} onValueChange={setStatus}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">All statuses</SelectItem><SelectItem value="0">Exceptions</SelectItem><SelectItem value="1">Reconciled</SelectItem><SelectItem value="2">Frozen</SelectItem></SelectContent></Select></div><div className="overflow-auto rounded-md border"><Table><TableHeader><TableRow><TableHead>Status</TableHead><TableHead>Period / cut-off</TableHead><TableHead>Receipt / landed</TableHead><TableHead>Valuation / GL</TableHead><TableHead>Variance</TableHead><TableHead>Exceptions</TableHead><TableHead /></TableRow></TableHeader><TableBody>{filtered.length === 0 ? <TableRow><TableCell colSpan={7} className="h-24 text-center text-muted-foreground">No reconciliation snapshots match the filter.</TableCell></TableRow> : filtered.map(value => <TableRow key={value.id}><TableCell><Badge variant={value.status === 0 ? 'destructive' : value.status === 2 ? 'default' : 'secondary'}>{statusLabel[value.status]}</Badge></TableCell><TableCell><div className="font-medium">{value.fiscalPeriodCode}</div><div className="text-xs text-muted-foreground">{new Date(value.cutoffDateUtc).toLocaleString()}</div></TableCell><TableCell className="text-xs">Receipt {money(value.receiptInventoryValue, value.functionalCurrencyCode)}<br />Landed {money(value.postedLandedCostValue, value.functionalCurrencyCode)}</TableCell><TableCell className="text-xs">Subledger {money(value.inventorySubledgerValue, value.functionalCurrencyCode)}<br />GL {money(value.generalLedgerValue, value.functionalCurrencyCode)}</TableCell><TableCell>{money(value.reconciliationVariance, value.functionalCurrencyCode)}</TableCell><TableCell>{value.exceptionCount}</TableCell><TableCell><Button size="sm" variant="outline" onClick={() => void openDetail(value)}><History className="mr-2 h-4 w-4" />Review</Button></TableCell></TableRow>)}</TableBody></Table></div></CardContent></Card>

    {selected && <Card><CardHeader><CardTitle>{selected.reconciliationNumber}</CardTitle><CardDescription>{selected.inventoryControlAccountCode} · {selected.inventoryControlAccountName} · snapshot {selected.snapshotHash.slice(0, 16)}…</CardDescription></CardHeader><CardContent className="space-y-6"><div className="grid gap-3 md:grid-cols-4"><div><Label>Balance cache</Label><p>{money(selected.inventoryBalanceCacheValue, selected.functionalCurrencyCode)}</p></div><div><Label>All movements now</Label><p>{money(selected.currentMovementValue, selected.functionalCurrencyCode)}</p></div><div><Label>Landed to inventory</Label><p>{money(selected.landedCostInventoryValue, selected.functionalCurrencyCode)}</p></div><div><Label>Landed variance</Label><p>{money(selected.landedCostVarianceValue, selected.functionalCurrencyCode)}</p></div></div>
      <div><h3 className="mb-2 font-medium"><AlertTriangle className="mr-2 inline h-4 w-4" />Exception report</h3><div className="overflow-auto rounded-md border"><Table><TableHeader><TableRow><TableHead>Area</TableHead><TableHead>Code / reference</TableHead><TableHead>Finding</TableHead><TableHead>Expected</TableHead><TableHead>Actual</TableHead><TableHead>Variance</TableHead></TableRow></TableHeader><TableBody>{selected.exceptions.length === 0 ? <TableRow><TableCell colSpan={6} className="h-20 text-center text-muted-foreground"><CheckCircle2 className="mr-2 inline h-4 w-4 text-green-600" />No reconciliation exceptions.</TableCell></TableRow> : selected.exceptions.map((value, index) => <TableRow key={`${value.code}-${value.reference || index}`}><TableCell><Badge variant="outline">{value.area}</Badge></TableCell><TableCell><div className="font-mono text-xs">{value.code}</div><div className="text-xs text-muted-foreground">{value.reference}</div></TableCell><TableCell>{value.message}</TableCell><TableCell>{value.expectedAmount == null ? '—' : money(value.expectedAmount, selected.functionalCurrencyCode)}</TableCell><TableCell>{value.actualAmount == null ? '—' : money(value.actualAmount, selected.functionalCurrencyCode)}</TableCell><TableCell>{value.varianceAmount == null ? '—' : money(value.varianceAmount, selected.functionalCurrencyCode)}</TableCell></TableRow>)}</TableBody></Table></div></div>
      {selected.status === 1 && <div className="space-y-3 rounded-md border p-4"><h3 className="font-medium"><FileLock2 className="mr-2 inline h-4 w-4" />Independent year-end freeze</h3><p className="text-sm text-muted-foreground">Requires Finance period-close permission, a different actor from the generator, the latest unchanged clean snapshot, and no exceptions. Freezing locks the Inventory module for this fiscal period and marks inventory valuation complete for Finance close.</p><Label>Freeze reason</Label><Textarea value={reason} onChange={event => setReason(event.target.value)} /><Button onClick={() => void freeze()} disabled={saving || !reason.trim()}><FileLock2 className="mr-2 h-4 w-4" />Freeze and lock Inventory</Button></div>}
      <div><h3 className="mb-2 font-medium"><History className="mr-2 inline h-4 w-4" />Immutable action history</h3><div className="space-y-2">{selected.actions.map(value => <div key={value.sequence} className="rounded-md border p-3 text-sm"><div className="flex justify-between gap-3"><span className="font-medium">#{value.sequence} {actionLabel[value.actionType]}</span><span className="text-xs text-muted-foreground">{new Date(value.occurredAtUtc).toLocaleString()}</span></div><div className="text-xs text-muted-foreground">Actor {value.actorUserId.slice(0, 8)} · {value.integrityHash.slice(0, 16)}…</div>{value.reason && <p className="mt-1">{value.reason}</p>}</div>)}</div></div>
    </CardContent></Card>}
  </div>;
}
