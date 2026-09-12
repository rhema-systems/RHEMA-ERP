'use client';

import React, { useRef, useState } from 'react';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';
import { landedCostInvoiceService, type LandedCostInvoiceRequest } from '@/services/landedCostInvoiceService';
import type { LandedCostDetailDto } from '@/services/inventoryManagementService';
import type { VendorInvoice } from '@/types/ap';
import { useAuth } from '@/hooks/use-auth';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';
import { groupLandedCostsBySupplier } from '@/lib/landed-cost-suppliers';

type Charge = LandedCostInvoiceRequest['charges'][number];

export function LandedCostSupplierInvoices({ voucher, onCreated, disabled, onBusyChange }: {
  voucher: LandedCostDetailDto; onCreated: () => void; disabled?: boolean; onBusyChange?: (busy: boolean) => void;
}) {
  const { hasAnyPermission } = useAuth();
  const canCreate = hasAnyPermission(['procurement.inventory.receive']);
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const submitting = useRef(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [setupError, setSetupError] = useState('');
  const [suppliers, setSuppliers] = useState<BusinessPartnerDto[]>([]);
  const [inventoryPosted, setInventoryPosted] = useState(false);
  const [charges, setCharges] = useState<Charge[]>([]);
  const [invoiceDate, setInvoiceDate] = useState('');
  const [created, setCreated] = useState<VendorInvoice[]>([]);
  const pending = voucher.costItems.filter(c => !c.invoiceId && !c.invoiceNumber);
  const groups = groupLandedCostsBySupplier(charges.map(row => {
    const cost = voucher.costItems.find(c => c.id === row.costItemId)!;
    return { ...cost, supplierId: row.supplierId,
      supplierName: suppliers.find(s => s.id === row.supplierId)?.partnerName,
      referenceNumber: row.supplierInvoiceNumber };
  }));
  const load = async () => {
    setLoading(true); setSetupError('');
    try {
      const partners = await businessPartnerService.getAllPartnersForDropdown();
      setSuppliers(partners.filter(s => ['Supplier', 'Both', 'Contractor'].includes(s.partnerType) &&
        (s.isActive ?? s.status === 'Active') && !s.isBlacklisted && s.approvalStatus === 'Approved'));
    } catch { setSetupError('Could not load cost suppliers. Retry before posting.'); }
    finally { setLoading(false); }
  };
  const start = () => {
    setCharges(voucher.costItems.map(c => ({ costItemId: c.id, supplierId: c.supplierId || '', supplierInvoiceNumber: c.referenceNumber || '' })));
    setInvoiceDate(voucher.costItems.find(c => c.invoiceDate)?.invoiceDate?.slice(0, 10) || new Date().toLocaleDateString('en-CA'));
    setError(''); setCreated([]); setInventoryPosted(voucher.status === 'Posted'); setOpen(true); void load();
  };
  const change = (id: string, patch: Partial<Charge>) => setCharges(rows => rows.map(c => c.costItemId === id ? { ...c, ...patch } : c));
  const save = async () => {
    if (submitting.current) return;
    if (!invoiceDate || charges.length === 0 || charges.some(c => !c.supplierId || !c.supplierInvoiceNumber.trim())) {
      setError('Select a supplier and enter its invoice reference for every charge. Tax is completed later on the invoice draft.'); return;
    }
    submitting.current = true; setBusy(true); onBusyChange?.(true); setError('');
    try {
      const result = await landedCostInvoiceService.post(voucher.id, { invoiceDate,
        charges: charges.map(c => ({ ...c, supplierInvoiceNumber: c.supplierInvoiceNumber.trim() })) });
      setInventoryPosted(result.inventoryPosted);
      if (result.invoicesPending) setError(result.message || 'Inventory posted; invoice creation is pending. Retry Post to finish invoices only.');
      else setCreated(result.invoices);
      onCreated();
    } catch (e) { setError(getProcurementProblemMessage(e, 'Posting could not be confirmed. Your entries are retained; retry safely. Already posted inventory will not be posted twice.')); }
    finally { submitting.current = false; setBusy(false); onBusyChange?.(false); }
  };
  if (!pending.length && !open) return <span className="self-center text-sm text-muted-foreground">Posted · Supplier invoices linked</span>;
  return <div className="space-y-1">
    <Button type="button" disabled={disabled || !canCreate || !['Allocated', 'Approved', 'Posted'].includes(voucher.status)} onClick={start}
      title={!canCreate ? 'Receiving permission and warehouse access are required.' : 'Allocate first, then post inventory and create supplier invoice drafts.'}>
      {voucher.status === 'Posted' ? 'Retry Post' : 'Post'}
    </Button>
    <Dialog open={open} onOpenChange={value => { if (!busy && !loading) setOpen(value); }}>
      <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto" onInteractOutside={e => e.preventDefault()}>
        <DialogHeader><DialogTitle>Post landed costs · {voucher.landedCostNumber}</DialogTitle>
          <DialogDescription>{inventoryPosted ? 'Inventory is already posted. This completes supplier invoices only.' : 'Post allocated costs to inventory and create supplier invoice drafts in one action.'} Tax is completed later in AP.</DialogDescription></DialogHeader>
        {created.length ? <div className="space-y-3">
          <p>Inventory posted. {created.length} supplier invoice{created.length === 1 ? '' : 's'} linked. Open each draft in AP to complete tax review. No invoice approval or financial posting was performed.</p>
          {created.map(invoice => <div key={invoice.id} className="rounded-md border p-3 text-sm">
            <Link className="text-primary underline" href={`/finance/ap/invoices/${invoice.id}`}>{invoice.invoiceNumber} · {invoice.supplierName}</Link>
            <p>{invoice.currencyCode} {invoice.totalAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })} · {invoice.status}</p>
          </div>)}
          <Button type="button" onClick={() => setOpen(false)}>Close</Button>
        </div> : <>
          <div><Label htmlFor="landed-invoice-date">Supplier invoice date</Label><Input id="landed-invoice-date" type="date" value={invoiceDate} disabled={busy} onChange={e => setInvoiceDate(e.target.value)} />
            <p className="text-xs text-muted-foreground">Initial date for new drafts. Finance can correct it during invoice review.</p></div>
          {charges.map((row, i) => {
            const cost = voucher.costItems.find(c => c.id === row.costItemId)!;
            return <fieldset key={row.costItemId} disabled={busy || loading || Boolean(cost.invoiceId || cost.invoiceNumber)} className="rounded-md border p-3 space-y-3">
              <div className="flex justify-between gap-3"><p className="text-sm font-medium">{cost.description} · {cost.currency} {cost.amount.toFixed(2)}</p>
                {cost.invoiceNumber && <span>{cost.invoiceNumber}</span>}</div>
              <div className="grid gap-3 sm:grid-cols-2">
                <div><Label>Cost supplier</Label><Select value={row.supplierId} onValueChange={value => change(row.costItemId, { supplierId: value })}>
                  <SelectTrigger aria-label={`Invoice charge ${i + 1} supplier`}><SelectValue placeholder="Select supplier" /></SelectTrigger>
                  <SelectContent>{suppliers.map(s => <SelectItem key={s.id} value={s.id}>{s.partnerCode} — {s.partnerName}</SelectItem>)}</SelectContent>
                </Select></div>
                <div><Label>Supplier invoice reference</Label><Input aria-label={`Invoice charge ${i + 1} reference`} maxLength={100} value={row.supplierInvoiceNumber} onChange={e => change(row.costItemId, { supplierInvoiceNumber: e.target.value })} /></div>
              </div>
            </fieldset>;
          })}
          {loading && <p className="text-sm">Loading suppliers…</p>}
          {setupError && <div role="alert" className="text-sm text-destructive">{setupError}<Button type="button" variant="outline" disabled={loading} onClick={() => void load()}>Retry choices</Button></div>}
          <div className="rounded-md border p-3 text-sm space-y-2"><p className="font-medium">Invoice groups before tax</p>
            {groups.map(g => <p key={g.key}>{g.supplierName} · {g.reference || 'Reference needed'} · {g.currency} {g.amount.toFixed(2)}</p>)}
            <p className="text-xs text-muted-foreground">One draft per supplier, currency and reference. Tax remains pending review; AP approval and posting remain separate.</p>
          </div>
          {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
          <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={() => setOpen(false)}>Cancel</Button>
            <Button type="button" disabled={busy || loading || Boolean(setupError) || !charges.length} onClick={() => void save()}>{busy ? 'Posting…' : inventoryPosted ? 'Finish invoice drafts' : 'Confirm Post'}</Button></DialogFooter>
        </>}
      </DialogContent>
    </Dialog>
  </div>;
}
