'use client';

import React, { useEffect, useRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { purchasingService, PurchaseOrderReceiptDto } from '@/services/purchasingService';
import { inventoryManagementService, LandedCostDetailDto, LandedCostDto, SaveReceiptLandedCostDto } from '@/services/inventoryManagementService';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';
import { toast } from 'sonner';
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';
import { LandedCostSupplierSummary } from './LandedCostSupplierSummary';
import { hasSupplierRole, hasContractorRole } from '@/lib/business-partner-roles';

const types = ['Freight / Shipping', 'Customs Duty', 'Insurance', 'Handling', 'Brokerage', 'Storage', 'Other'];
const typeNames = ['Freight', 'CustomsDuty', 'Insurance', 'Handling', 'Brokerage', 'Storage', 'Other'];
const methods = [{ value: 'ByValue', label: 'By item value' }, { value: 'ByQuantity', label: 'By quantity' }, { value: 'ByWeight', label: 'By captured weight' }, { value: 'Equal', label: 'Equally' }];
type CostRow = SaveReceiptLandedCostDto['costItems'][number];

export function ReceiptLandedCostEntry({ receipt, selected, onSaved, disabled = false }: {
  receipt: PurchaseOrderReceiptDto; selected: LandedCostDetailDto | null;
  onSaved: (voucher: LandedCostDto) => void; disabled?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<LandedCostDetailDto | null>(null);
  const [currency, setCurrency] = useState('');
  const [poCurrency, setPoCurrency] = useState('');
  const [targets, setTargets] = useState<Array<{ id: string; name: string }>>([]);
  const [rows, setRows] = useState<CostRow[]>([]);
  const [notes, setNotes] = useState('');
  const [error, setError] = useState('');
  const [setupError, setSetupError] = useState('');
  const [suppliers, setSuppliers] = useState<BusinessPartnerDto[]>([]);
  const [supplierError, setSupplierError] = useState('');
  const [supplierLoading, setSupplierLoading] = useState(false);
  const [supplierRevision, setSupplierRevision] = useState(0);
  const [saving, setSaving] = useState(false);
  const savingRef = useRef(false);
  const requestId = useRef('');

  useEffect(() => {
    if (!open) return;
    let active = true;
    setSupplierLoading(true); setSupplierError(''); setSuppliers([]);
    businessPartnerService.getAllPartnersForDropdown().then(partners => {
      if (active) setSuppliers(partners.filter(s =>
        (hasSupplierRole(s.partnerType) || hasContractorRole(s.partnerType)) &&
        (s.isActive ?? s.status === 'Active') && !s.isBlacklisted &&
        s.approvalStatus === 'Approved'));
    }).catch(() => {
      if (active) setSupplierError('Could not load cost suppliers. Retry to select a supplier.');
    }).finally(() => { if (active) setSupplierLoading(false); });
    return () => { active = false; };
  }, [open, supplierRevision]);

  useEffect(() => {
    let active = true;
    setPoCurrency(''); setTargets([]); setSetupError('');
    purchasingService.getPurchaseOrderById(receipt.purchaseOrderId).then(po => {
      if (!active) return;
      setPoCurrency(po.currency);
      setTargets((receipt.items || []).filter(r => r.acceptedQuantity > 0 || r.receivedQuantity > r.rejectedQuantity)
        .filter(r => po.items?.some(p => p.id === r.purchaseOrderItemId && p.inventoryItemId))
        .map(r => ({ id: r.purchaseOrderItemId, name: `${r.itemCode} — ${r.itemName}` })));
    }).catch(() => { if (active) setSetupError('Could not load receipt cost targets. Refresh the page to try again.'); });
    return () => { active = false; };
  }, [receipt.purchaseOrderId, receipt.items]);

  const newRow = (code: string): CostRow => ({ costType: 1, description: types[0], amount: 0, currency: code, exchangeRate: 1, allocationMethod: 'ByValue' });
  const start = (draft: LandedCostDetailDto | null) => {
    setEditing(draft); setCurrency(draft?.currency || poCurrency); setNotes(draft?.notes || ''); setError('');
    requestId.current = crypto.randomUUID();
    setRows(draft ? draft.costItems.map(c => ({ ...c,
      costType: typeof c.costType === 'number' ? c.costType : Number(c.costType) || typeNames.indexOf(c.costType) + 1
    })) : [newRow(poCurrency)]);
    setOpen(true);
  };
  const change = (index: number, patch: Partial<CostRow>) => setRows(previous => previous.map((r, i) => i === index ? { ...r, ...patch } : r));
  const save = async () => {
    if (savingRef.current) return;
    if (!currency.trim() || !rows.length || rows.some(r => !r.description.trim() || !r.currency.trim() || !Number.isFinite(r.amount) || r.amount <= 0 || !Number.isFinite(r.exchangeRate) || r.exchangeRate <= 0)) {
      setError('Enter a description, positive amount, currency and exchange rate for every charge.'); return;
    }
    if (rows.some(r => r.currency.trim().toUpperCase() === currency.trim().toUpperCase() && r.exchangeRate !== 1)) {
      setError('Use exchange rate 1 when the charge and voucher currencies are the same.'); return;
    }
    savingRef.current = true; setSaving(true); setError('');
    try {
      const voucher = await inventoryManagementService.saveReceiptLandedCost({
        requestId: requestId.current, goodsReceiptNoteId: editing?.goodsReceiptNoteId || receipt.id,
        currency: currency.trim().toUpperCase(), notes: notes.trim() || undefined, editToken: editing?.editToken,
        costItems: rows.map(r => ({ ...r, description: r.description.trim(), currency: r.currency.trim().toUpperCase() }))
      }, editing?.id);
      setOpen(false); toast.success('Landed costs saved as draft.'); onSaved(voucher);
    } catch (e: unknown) {
      const data = (e as { response?: { data?: unknown } })?.response?.data;
      setError(getProcurementProblemMessage(data, e instanceof Error ? e.message : 'Could not save landed costs.'));
    } finally { savingRef.current = false; setSaving(false); }
  };

  return <div className="space-y-2">
    <div className="flex flex-wrap gap-2">
      <Button disabled={disabled || !poCurrency || !targets.length} onClick={() => start(null)}>Add receipt costs</Button>
      {selected?.status === 'Draft' && <Button variant="outline" disabled={disabled || !poCurrency} onClick={() => start(selected)}>Edit draft costs</Button>}
    </div>
    <p className="text-xs text-muted-foreground">Enter charges known at delivery or later. PO estimates are optional. Edit an existing draft to replace its estimates; do not add the same charge twice.</p>
    {setupError && <p role="alert" className="text-sm text-destructive">{setupError}</p>}
    {poCurrency && !targets.length && <p className="text-sm text-muted-foreground">No received stock lines are available for landed-cost allocation.</p>}
    <Dialog open={open} onOpenChange={value => { if (!saving) setOpen(value); }}>
      <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto" onInteractOutside={e => e.preventDefault()}>
        <DialogHeader><DialogTitle>{editing ? 'Edit draft landed costs' : 'Add receipt landed costs'}</DialogTitle>
          <DialogDescription>{receipt.receiptNumber} — save charges now, then allocate and prepare supplier invoices. Final posting happens from Invoices. This does not change the supplier PO total.</DialogDescription></DialogHeader>
        <div><Label htmlFor="receipt-cost-currency">Voucher currency</Label><Input id="receipt-cost-currency" value={currency} maxLength={10} disabled={saving} onChange={e => setCurrency(e.target.value.toUpperCase())} /></div>
        {rows.map((row, index) => <fieldset disabled={saving} key={index} className="space-y-3 rounded-lg border p-3">
          <div className="flex items-center justify-between"><legend className="font-medium">Charge {index + 1}</legend><Button type="button" variant="ghost" size="sm" onClick={() => setRows(rows.filter((_, i) => i !== index))}>Remove charge {index + 1}</Button></div>
          <div className="grid gap-3 sm:grid-cols-2">
            <div><Label>Cost type</Label><Select value={String(row.costType)} onValueChange={v => change(index, { costType: Number(v) })}><SelectTrigger aria-label={`Charge ${index + 1} type`}><SelectValue /></SelectTrigger><SelectContent>{types.map((t, i) => <SelectItem key={t} value={String(i + 1)}>{t}</SelectItem>)}</SelectContent></Select></div>
            <div><Label>Description</Label><Input aria-label={`Charge ${index + 1} description`} value={row.description} maxLength={500} onChange={e => change(index, { description: e.target.value })} /></div>
            <div className="sm:col-span-2"><Label>Cost supplier</Label>
              <Select value={row.supplierId || '__none__'} disabled={supplierLoading || Boolean(supplierError)} onValueChange={value => change(index, { supplierId: value === '__none__' ? undefined : value })}>
                <SelectTrigger aria-label={`Charge ${index + 1} supplier`}><SelectValue placeholder="Select cost supplier" /></SelectTrigger>
                <SelectContent><SelectItem value="__none__">Select before invoicing</SelectItem>
                  {row.supplierId && !suppliers.some(s => s.id === row.supplierId) &&
                    <SelectItem value={row.supplierId} disabled>Existing supplier (not currently selectable)</SelectItem>}
                  {suppliers.map(s => <SelectItem key={s.id} value={s.id}>{s.partnerCode} — {s.partnerName}</SelectItem>)}
                </SelectContent>
              </Select>
              <p className="mt-1 text-xs text-muted-foreground">{supplierLoading ? 'Loading suppliers…' : 'Choose the supplier billing this charge; it can differ from the goods supplier. Needed before invoicing.'}</p>
            </div>
            <div><Label>Amount</Label><Input aria-label={`Charge ${index + 1} amount`} type="number" min="0.01" step="0.01" value={row.amount || ''} onChange={e => change(index, { amount: Number(e.target.value) })} /></div>
            <div><Label>Charge currency</Label><Input aria-label={`Charge ${index + 1} currency`} value={row.currency} maxLength={10} onChange={e => change(index, { currency: e.target.value.toUpperCase() })} /></div>
            <div><Label>Exchange rate to {currency}</Label><Input aria-label={`Charge ${index + 1} exchange rate`} type="number" min="0.000001" step="0.000001" value={row.exchangeRate} onChange={e => change(index, { exchangeRate: Number(e.target.value) })} /></div>
            <div><Label>Applies to</Label><Select value={row.purchaseOrderItemId || 'shared'} onValueChange={v => change(index, { purchaseOrderItemId: v === 'shared' ? undefined : v, allocationMethod: v === 'shared' ? 'ByValue' : 'ByQuantity' })}><SelectTrigger aria-label={`Charge ${index + 1} target`}><SelectValue /></SelectTrigger><SelectContent><SelectItem value="shared">All received stock items</SelectItem>{targets.map(t => <SelectItem key={t.id} value={t.id}>{t.name}</SelectItem>)}</SelectContent></Select></div>
            {!row.purchaseOrderItemId && <div><Label>Allocation method</Label><Select value={row.allocationMethod} onValueChange={v => change(index, { allocationMethod: v })}><SelectTrigger aria-label={`Charge ${index + 1} allocation`}><SelectValue /></SelectTrigger><SelectContent>{methods.map(m => <SelectItem key={m.value} value={m.value}>{m.label}</SelectItem>)}{!methods.some(m => m.value === row.allocationMethod) && <SelectItem value={row.allocationMethod}>{row.allocationMethod} (existing)</SelectItem>}</SelectContent></Select></div>}
            <div><Label>Invoice / charge reference (optional)</Label><Input aria-label={`Charge ${index + 1} reference`} value={row.referenceNumber || ''} maxLength={100} onChange={e => change(index, { referenceNumber: e.target.value })} /></div>
          </div>
        </fieldset>)}
        <Button variant="outline" disabled={saving} onClick={() => setRows([...rows, newRow(currency)])}>Add another charge</Button>
        {supplierError && <div role="alert" className="text-sm text-destructive">{supplierError}
          <Button type="button" variant="outline" size="sm" disabled={saving} onClick={() => setSupplierRevision(v => v + 1)}>Retry suppliers</Button>
        </div>}
        <LandedCostSupplierSummary lines={rows.map(row => ({ ...row,
          supplierName: suppliers.find(s => s.id === row.supplierId)?.partnerName ||
            editing?.costItems.find(c => c.supplierId === row.supplierId)?.supplierName
        }))} />
        <div><Label htmlFor="receipt-cost-notes">Notes (optional)</Label><Input id="receipt-cost-notes" value={notes} maxLength={2000} disabled={saving} onChange={e => setNotes(e.target.value)} /></div>
        <p className="font-medium">Total: {rows.reduce((sum, r) => sum + Math.round(r.amount * r.exchangeRate * 100) / 100, 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} {currency}</p>
        {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
        <DialogFooter><Button variant="outline" disabled={saving} onClick={() => setOpen(false)}>Cancel</Button><Button disabled={saving} onClick={() => void save()}>{saving ? 'Saving…' : 'Save draft costs'}</Button></DialogFooter>
      </DialogContent>
    </Dialog>
  </div>;
}
