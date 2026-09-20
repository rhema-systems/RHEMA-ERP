'use client';

import React, { useState } from 'react';
import { MoreHorizontal, Pencil, Plus, Trash2, Truck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { buildPlannedCostPayload, PlannedCostLine, plannedCostTotal } from '@/lib/purchase-order-landed-costs';
import { formatProcurementMoney } from '@/lib/procurement-currency';

const types = ['Freight / Shipping', 'Customs Duty', 'Insurance', 'Handling', 'Brokerage', 'Storage / Warehousing', 'Other'];
const methods = ['ByValue', 'ByQuantity', 'ByWeight', 'ByVolume', 'Equal', 'Manual'] as const;
type Supplier = { id: string; partnerName: string };

function CostRows({ value, onChange, currency, suppliers, lineKey }: {
  value: PlannedCostLine[]; onChange: (rows: PlannedCostLine[]) => void; currency: string;
  suppliers: Supplier[]; lineKey?: string;
}) {
  const update = (id: string, patch: Partial<PlannedCostLine>) =>
    onChange(value.map(c => c.tempId === id ? { ...c, ...patch } : c));
  return <div className="space-y-3">
    {value.length === 0 && <p className="rounded-lg border border-dashed p-4 text-sm text-muted-foreground">No planned costs added.</p>}
    {value.map((c, index) => <div key={c.tempId} className="rounded-lg border bg-muted/20 p-3 space-y-3">
      <div className="flex items-center justify-between"><span className="text-sm font-medium">Cost {index + 1}</span>
        <Button type="button" variant="ghost" size="sm" aria-label={`Remove cost ${index + 1}`}
          onClick={() => onChange(value.filter(x => x.tempId !== c.tempId))}><Trash2 className="h-4 w-4" /></Button></div>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div className="space-y-1"><Label>Cost type</Label><Select value={String(c.costType)} onValueChange={v =>
          update(c.tempId, { costType: Number(v), ...(!c.description || types.includes(c.description) ? { description: types[Number(v) - 1] } : {}) })}>
          <SelectTrigger aria-label={`Cost ${index + 1} type`}><SelectValue /></SelectTrigger>
          <SelectContent>{types.map((name, i) => <SelectItem key={name} value={String(i + 1)}>{name}</SelectItem>)}</SelectContent>
        </Select></div>
        <div className="space-y-1"><Label>Description</Label><Input aria-label={`Cost ${index + 1} description`} maxLength={500} value={c.description} onChange={e => update(c.tempId, { description: e.target.value })} /></div>
        <div className="space-y-1"><Label>Amount</Label><Input aria-label={`Cost ${index + 1} amount`} type="number" min="0.01" step="0.01" value={c.amount || ''} onChange={e => update(c.tempId, { amount: Number(e.target.value) })} /></div>
        <div className="space-y-1"><Label>Currency</Label><Input aria-label={`Cost ${index + 1} currency`} maxLength={10} value={c.currency} onChange={e => update(c.tempId, { currency: e.target.value.toUpperCase() })} /></div>
        <div className="space-y-1"><Label>Exchange rate to {currency}</Label><Input aria-label={`Cost ${index + 1} exchange rate`} type="number" min="0.0001" step="0.0001" value={c.exchangeRate} onChange={e => update(c.tempId, { exchangeRate: Number(e.target.value) })} /></div>
        {!lineKey && <div className="space-y-1"><Label>Share across PO lines</Label><Select value={c.allocationMethod} onValueChange={v => update(c.tempId, { allocationMethod: v as PlannedCostLine['allocationMethod'] })}>
          <SelectTrigger aria-label={`Cost ${index + 1} allocation method`}><SelectValue /></SelectTrigger>
          <SelectContent>{methods.map(m => <SelectItem key={m} value={m}>{m.replace('By', 'By ')}</SelectItem>)}</SelectContent>
        </Select></div>}
        <div className="space-y-1"><Label>Cost supplier (optional)</Label><Select value={c.supplierId || '__none__'} onValueChange={v => update(c.tempId, { supplierId: v === '__none__' ? undefined : v })}>
          <SelectTrigger aria-label={`Cost ${index + 1} supplier`}><SelectValue /></SelectTrigger>
          <SelectContent><SelectItem value="__none__">None</SelectItem>{suppliers.map(s => <SelectItem key={s.id} value={s.id}>{s.partnerName}</SelectItem>)}</SelectContent>
        </Select></div>
        <div className="space-y-1"><Label>Reference (optional)</Label><Input aria-label={`Cost ${index + 1} reference`} maxLength={100} value={c.referenceNumber || ''} onChange={e => update(c.tempId, { referenceNumber: e.target.value })} /></div>
      </div>
    </div>)}
    <Button type="button" variant="outline" onClick={() => onChange([...value, {
      tempId: crypto.randomUUID(), purchaseOrderLineKey: lineKey, costType: 1, description: types[0],
      amount: 0, currency, exchangeRate: 1, allocationMethod: lineKey ? 'ByQuantity' : 'ByValue'
    }])}><Plus className="h-4 w-4 mr-2" />Add planned cost</Button>
  </div>;
}

export function PurchaseOrderLineActions({ name, disabled, onEdit, onCosts, onDelete, costCount }: {
  name: string; disabled: boolean; onEdit: () => void; onCosts: () => void; onDelete: () => void; costCount: number;
}) {
  const [confirmDelete, setConfirmDelete] = useState(false);
  return <>
    <DropdownMenu><DropdownMenuTrigger asChild><Button type="button" variant="outline" size="sm" disabled={disabled} aria-label={`Actions for ${name}`}>
      <MoreHorizontal className="h-4 w-4" />{costCount > 0 && <span className="ml-1 text-xs">{costCount} cost{costCount === 1 ? '' : 's'}</span>}
    </Button></DropdownMenuTrigger><DropdownMenuContent align="end">
      <DropdownMenuItem onSelect={onEdit}><Pencil className="h-4 w-4 mr-2" />Edit item</DropdownMenuItem>
      <DropdownMenuItem onSelect={onCosts}><Truck className="h-4 w-4 mr-2" />Planned landed costs{costCount ? ` (${costCount})` : ''}</DropdownMenuItem>
      <DropdownMenuItem className="text-destructive" onSelect={() => setConfirmDelete(true)}><Trash2 className="h-4 w-4 mr-2" />Remove item</DropdownMenuItem>
    </DropdownMenuContent></DropdownMenu>
    <ConfirmationDialog open={confirmDelete} onOpenChange={setConfirmDelete} title="Remove PO item?" variant="destructive"
      description={`Remove ${name} and its ${costCount} line-specific planned cost(s)? PO-wide costs will remain. The change is saved with the purchase order.`}
      confirmText="Remove item" onConfirm={onDelete} />
  </>;
}

export function PurchaseOrderPlannedCosts({ costs, onChange, currency, onCurrencyChange, notes, onNotesChange,
  suppliers, lines, selectedLineKey, onCloseLine }: {
  costs: PlannedCostLine[]; onChange: (rows: PlannedCostLine[]) => void;
  currency: string; onCurrencyChange: (currency: string) => void; notes: string; onNotesChange: (notes: string) => void;
  suppliers: Supplier[]; lines: { tempId: string; itemDescription?: string; itemName?: string }[];
  selectedLineKey: string | null; onCloseLine: () => void;
}) {
  const [draft, setDraft] = useState<PlannedCostLine[]>([]);
  const [error, setError] = useState('');
  const [openedKey, setOpenedKey] = useState<string | null>(null);
  // Initialize only when a different line is opened, preserving unsaved modal edits.
  if (selectedLineKey !== openedKey) {
    setOpenedKey(selectedLineKey);
    setDraft(costs.filter(c => c.purchaseOrderLineKey === selectedLineKey).map(c => ({ ...c })));
    setError('');
  }
  const shared = costs.filter(c => !c.purchaseOrderLineKey);
  const specific = costs.filter(c => c.purchaseOrderLineKey);
  const selected = lines.find(l => l.tempId === selectedLineKey);
  return <>
    <Card className="w-full lg:col-span-2">
      <CardHeader className="pb-3"><CardTitle className="text-base">PO-wide planned landed costs</CardTitle>
        <CardDescription>Shared estimates for the whole PO. Use an item's action menu for costs that belong only to that line. Estimates do not change the supplier PO total.</CardDescription></CardHeader>
      <CardContent className="space-y-4">
        <div className="flex flex-wrap gap-3"><div><Label>Plan currency</Label><Input aria-label="Plan currency" className="w-28" maxLength={10} value={currency} onChange={e => onCurrencyChange(e.target.value.toUpperCase())} /></div>
          <div className="flex-1 min-w-48"><Label>Plan notes (optional)</Label><Input aria-label="Plan notes" maxLength={2000} value={notes} onChange={e => onNotesChange(e.target.value)} /></div></div>
        <CostRows value={shared} currency={currency} suppliers={suppliers} onChange={rows => onChange([...specific, ...rows])} />
        <div className="border-t pt-3 space-y-1 text-sm">
          <div className="flex justify-between"><span>PO-wide estimates</span><span>{formatProcurementMoney(plannedCostTotal(shared), currency)}</span></div>
          <div className="flex justify-between"><span>Line-specific estimates ({specific.length})</span><span>{formatProcurementMoney(plannedCostTotal(specific), currency)}</span></div>
          <div className="flex justify-between font-semibold"><span>Total planned landed costs</span><span>{formatProcurementMoney(plannedCostTotal(costs), currency)}</span></div>
        </div>
        <p className="text-xs text-muted-foreground">Saved together with the PO. At receipt, line-specific estimates apply only to that line and are prorated for the received quantity.</p>
      </CardContent>
    </Card>
    <Dialog open={!!selected} onOpenChange={open => { if (!open) onCloseLine(); }}>
      <DialogContent className="sm:max-w-2xl max-h-[85vh] overflow-y-auto"><DialogHeader>
        <DialogTitle>Line planned landed costs</DialogTitle>
        <DialogDescription>{selected?.itemName || selected?.itemDescription || 'PO item'} — applies only to this line, in addition to its share of PO-wide costs.</DialogDescription>
      </DialogHeader>
        <CostRows value={draft} onChange={setDraft} currency={currency} suppliers={suppliers} lineKey={selectedLineKey || undefined} />
        <div className="flex justify-between font-semibold border-t pt-3"><span>Line cost total</span><span>{formatProcurementMoney(plannedCostTotal(draft), currency)}</span></div>
        {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
        <p className="text-xs text-muted-foreground">Save line costs to apply them to this form, then save the purchase order to persist all changes.</p>
        <DialogFooter><Button type="button" variant="outline" onClick={onCloseLine}>Cancel</Button>
          <Button type="button" onClick={() => {
            try {
              buildPlannedCostPayload(draft, lines, currency, notes);
              onChange([...costs.filter(c => c.purchaseOrderLineKey !== selectedLineKey), ...draft]);
              onCloseLine();
            } catch (e) { setError(e instanceof Error ? e.message : 'Check the planned costs.'); }
          }}>Save line costs</Button></DialogFooter>
      </DialogContent>
    </Dialog>
  </>;
}
