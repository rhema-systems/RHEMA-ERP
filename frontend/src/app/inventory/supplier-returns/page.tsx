'use client';

import { useEffect, useMemo, useState } from 'react';
import { CheckCircle2, PackageX, Plus, Send, Truck, XCircle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import {
  CreateSupplierReturn, SupplierReturn, SupplierReturnReason, SupplierReturnSourceGrn,
  supplierReturnReasons, supplierReturnService,
} from '@/services/supplierReturnService';

const badge = (status: string) => ({
  Draft: 'bg-slate-100 text-slate-800', Submitted: 'bg-amber-100 text-amber-800',
  Approved: 'bg-blue-100 text-blue-800', Shipped: 'bg-indigo-100 text-indigo-800',
  Completed: 'bg-emerald-100 text-emerald-800', Rejected: 'bg-red-100 text-red-800',
  Cancelled: 'bg-red-100 text-red-800',
}[status] || 'bg-muted text-muted-foreground');

const errorMessage = (error: unknown, fallback: string) => {
  const candidate = error as { response?: { data?: { detail?: string; message?: string } } };
  return candidate.response?.data?.detail || candidate.response?.data?.message || fallback;
};

export default function SupplierReturnsPage() {
  const { toast } = useToast();
  const [rows, setRows] = useState<SupplierReturn[]>([]);
  const [sources, setSources] = useState<SupplierReturnSourceGrn[]>([]);
  const [loading, setLoading] = useState(true);
  const [createOpen, setCreateOpen] = useState(false);
  const [sourceId, setSourceId] = useState('');
  const [reason, setReason] = useState<SupplierReturnReason>('Quality');
  const [notes, setNotes] = useState('');
  const [selected, setSelected] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState<string | null>(null);
  const [reasonAction, setReasonAction] = useState<{ id: string; kind: 'reject' | 'cancel' } | null>(null);
  const [actionReason, setActionReason] = useState('');
  const [dispatch, setDispatch] = useState<string | null>(null);
  const [trackingNumber, setTrackingNumber] = useState('');

  const selectedSource = useMemo(() => sources.find(item => item.id === sourceId), [sources, sourceId]);
  const load = async () => {
    setLoading(true);
    try {
      const [returns, grns] = await Promise.all([supplierReturnService.getAll(), supplierReturnService.getSourceGrns()]);
      setRows(returns); setSources(grns);
    } catch (error) {
      toast({ title: 'Supplier returns unavailable', description: errorMessage(error, 'Unable to load supplier returns.'), variant: 'destructive' });
    } finally { setLoading(false); }
  };
  useEffect(() => { void load(); }, []);

  const resetCreate = () => { setSourceId(''); setReason('Quality'); setNotes(''); setSelected({}); };
  const create = async () => {
    if (!selectedSource?.supplierId) {
      toast({ title: 'Select a received GRN', description: 'Choose a stock-updated GRN with a supplier before creating the return.', variant: 'destructive' });
      return;
    }
    const items = Object.entries(selected).filter(([, quantity]) => Number(quantity) > 0).map(([id, quantity]) => {
      const line = selectedSource.items.find(item => item.id === id)!;
      return { inventoryItemId: line.inventoryItemId, grnItemId: line.id, returnQuantity: Number(quantity), returnReason: reason };
    });
    if (!items.length) {
      toast({ title: 'Select return lines', description: 'Enter a positive return quantity for at least one accepted GRN line.', variant: 'destructive' });
      return;
    }
    const request: CreateSupplierReturn = {
      supplierId: selectedSource.supplierId, warehouseId: selectedSource.warehouseId, goodsReceiptNoteId: selectedSource.id,
      returnReason: reason, notes: notes || undefined, items,
    };
    setBusy('create');
    try {
      await supplierReturnService.create(request);
      toast({ title: 'Supplier return drafted', description: 'Submit it for independent Stores approval before dispatching stock.' });
      setCreateOpen(false); resetCreate(); await load();
    } catch (error) {
      toast({ title: 'Supplier return blocked', description: errorMessage(error, 'The return could not be created.'), variant: 'destructive' });
    } finally { setBusy(null); }
  };

  const action = async (id: string, label: string, call: () => Promise<void>) => {
    setBusy(id);
    try { await call(); toast({ title: label }); await load(); }
    catch (error) { toast({ title: 'Action blocked', description: errorMessage(error, 'The controlled supplier-return action could not be completed.'), variant: 'destructive' }); }
    finally { setBusy(null); }
  };

  const confirmReason = async () => {
    if (!reasonAction || !actionReason.trim()) return;
    const current = reasonAction;
    setReasonAction(null);
    await action(current.id, current.kind === 'reject' ? 'Supplier return rejected' : 'Supplier return cancelled', () =>
      current.kind === 'reject' ? supplierReturnService.reject(current.id, actionReason.trim()) : supplierReturnService.cancel(current.id, actionReason.trim()));
    setActionReason('');
  };
  const confirmDispatch = async () => {
    if (!dispatch) return;
    const id = dispatch; setDispatch(null);
    await action(id, 'Supplier return dispatched; stock was reduced.', () => supplierReturnService.ship(id, trackingNumber || undefined));
    setTrackingNumber('');
  };

  return <div className="space-y-5">
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div><h1 className="text-2xl font-semibold tracking-tight">Supplier Returns</h1>
        <p className="text-sm text-muted-foreground">Return accepted, stock-updated GRN items to their supplier through a controlled Stores workflow.</p></div>
      <Button onClick={() => { resetCreate(); setCreateOpen(true); }}><Plus className="mr-2 h-4 w-4" />New supplier return</Button>
    </div>
    <Card className="border-blue-200 bg-blue-50/40"><CardContent className="py-4 text-sm text-blue-950">
      Physical stock is reduced only when an independently approved return is dispatched. Finance/AP remains the owner of any debit note, supplier credit, payment allocation, and ledger posting.
    </CardContent></Card>
    <Card><CardHeader><CardTitle>Return register</CardTitle><CardDescription>Source, approval, dispatch, and supplier-credit traceability.</CardDescription></CardHeader><CardContent>
      {loading ? <p className="py-8 text-center text-muted-foreground">Loading supplier returns…</p> : rows.length === 0 ? <p className="py-8 text-center text-muted-foreground">No supplier returns have been created.</p> :
      <div className="overflow-x-auto"><table className="w-full text-sm"><thead className="border-b text-left text-muted-foreground"><tr><th className="p-2">Return</th><th className="p-2">GRN / Supplier</th><th className="p-2">Quantity / value</th><th className="p-2">Status</th><th className="p-2 text-right">Actions</th></tr></thead>
        <tbody>{rows.map(row => <tr key={row.id} className="border-b last:border-0"><td className="p-2 font-medium">{row.returnNumber}<div className="text-xs text-muted-foreground">{row.returnReason}</div></td><td className="p-2">{row.grnNumber || '—'}<div className="text-xs text-muted-foreground">{row.supplierName}</div></td><td className="p-2">{row.totalQuantity.toLocaleString()}<div className="text-xs text-muted-foreground">{row.totalValue.toLocaleString(undefined, { minimumFractionDigits: 2 })}</div></td><td className="p-2"><Badge className={badge(row.status)}>{row.status}</Badge></td>
          <td className="p-2"><div className="flex justify-end gap-2">{row.status === 'Draft' && <><Button size="sm" disabled={busy === row.id} onClick={() => action(row.id, 'Supplier return submitted for independent approval.', () => supplierReturnService.submit(row.id))}><Send className="mr-1 h-3.5 w-3.5"/>Submit</Button><Button variant="outline" size="sm" onClick={() => setReasonAction({ id: row.id, kind: 'cancel' })}>Cancel</Button></>}{row.status === 'Submitted' && <><Button size="sm" disabled={busy === row.id} onClick={() => action(row.id, 'Supplier return approved. Dispatch remains a separate control.', () => supplierReturnService.approve(row.id))}><CheckCircle2 className="mr-1 h-3.5 w-3.5"/>Approve</Button><Button variant="outline" size="sm" onClick={() => setReasonAction({ id: row.id, kind: 'reject' })}><XCircle className="mr-1 h-3.5 w-3.5"/>Reject</Button></>}{row.status === 'Approved' && <Button size="sm" disabled={busy === row.id} onClick={() => setDispatch(row.id)}><Truck className="mr-1 h-3.5 w-3.5"/>Dispatch</Button>}</div></td>
        </tr>)}</tbody></table></div>}
    </CardContent></Card>

    <Dialog open={createOpen} onOpenChange={setCreateOpen}><DialogContent className="max-w-4xl max-h-[88vh] overflow-y-auto"><DialogHeader><DialogTitle>New supplier return</DialogTitle><DialogDescription>Select the exact accepted GRN. Supplier, warehouse, item details, and unit cost are derived from it.</DialogDescription></DialogHeader>
      <div className="grid gap-4"><div><Label>Accepted, stock-updated GRN</Label><Select value={sourceId} onValueChange={value => { setSourceId(value); setSelected({}); }}><SelectTrigger><SelectValue placeholder="Select a GRN" /></SelectTrigger><SelectContent>{sources.map(source => <SelectItem key={source.id} value={source.id}>{source.grnNumber} — {source.supplierName || 'Supplier'} — {source.warehouseName || 'Warehouse'}</SelectItem>)}</SelectContent></Select></div>
        {selectedSource && <><div className="grid grid-cols-1 gap-3 sm:grid-cols-3 rounded border p-3 text-sm"><div><span className="text-muted-foreground">Supplier</span><p className="font-medium">{selectedSource.supplierName}</p></div><div><span className="text-muted-foreground">Warehouse</span><p className="font-medium">{selectedSource.warehouseName}</p></div><div><span className="text-muted-foreground">Source</span><p className="font-medium">{selectedSource.grnNumber}</p></div></div>
          <div><Label>Return reason</Label><Select value={reason} onValueChange={value => setReason(value as SupplierReturnReason)}><SelectTrigger><SelectValue/></SelectTrigger><SelectContent>{supplierReturnReasons.map(value => <SelectItem key={value} value={value}>{value}</SelectItem>)}</SelectContent></Select></div>
          <div className="overflow-x-auto border rounded"><table className="w-full text-sm"><thead className="border-b text-left"><tr><th className="p-2">Item</th><th className="p-2">Accepted</th><th className="p-2">System cost</th><th className="p-2 w-40">Return quantity</th></tr></thead><tbody>{selectedSource.items.filter(item => item.acceptedQuantity > 0).map(item => <tr key={item.id} className="border-b last:border-0"><td className="p-2"><div className="font-medium">{item.itemCode}</div><div className="text-muted-foreground">{item.itemName}</div></td><td className="p-2">{item.acceptedQuantity} {item.unitOfMeasure}</td><td className="p-2">{item.unitCost.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td><td className="p-2"><Input type="number" min="0" max={item.acceptedQuantity} step="0.0001" value={selected[item.id] || ''} onChange={event => setSelected(current => ({ ...current, [item.id]: event.target.value }))} /></td></tr>)}</tbody></table></div>
          <div><Label>Notes</Label><Textarea value={notes} onChange={event => setNotes(event.target.value)} placeholder="Optional dispatch or quality context" /></div></>}
      </div><DialogFooter><Button variant="outline" onClick={() => setCreateOpen(false)}>Cancel</Button><Button disabled={busy === 'create' || !selectedSource} onClick={create}><PackageX className="mr-2 h-4 w-4"/>Create draft</Button></DialogFooter>
    </DialogContent></Dialog>
    <Dialog open={!!reasonAction} onOpenChange={open => !open && setReasonAction(null)}><DialogContent><DialogHeader><DialogTitle>{reasonAction?.kind === 'reject' ? 'Reject supplier return' : 'Cancel supplier return'}</DialogTitle><DialogDescription>A reason is required for the immutable audit trail.</DialogDescription></DialogHeader><Textarea value={actionReason} onChange={event => setActionReason(event.target.value)} /><DialogFooter><Button variant="outline" onClick={() => setReasonAction(null)}>Close</Button><Button variant="destructive" disabled={!actionReason.trim()} onClick={confirmReason}>Confirm</Button></DialogFooter></DialogContent></Dialog>
    <Dialog open={!!dispatch} onOpenChange={open => !open && setDispatch(null)}><DialogContent><DialogHeader><DialogTitle>Dispatch supplier return</DialogTitle><DialogDescription>This posts the approved outbound stock movement. The independent approver cannot dispatch it.</DialogDescription></DialogHeader><div><Label>Carrier tracking reference (optional)</Label><Input value={trackingNumber} onChange={event => setTrackingNumber(event.target.value)} /></div><DialogFooter><Button variant="outline" onClick={() => setDispatch(null)}>Cancel</Button><Button onClick={confirmDispatch}><Truck className="mr-2 h-4 w-4"/>Dispatch stock</Button></DialogFooter></DialogContent></Dialog>
  </div>;
}
