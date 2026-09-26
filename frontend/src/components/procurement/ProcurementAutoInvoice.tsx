'use client';

import React, { useRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useAuth } from '@/hooks/use-auth';
import { accountsPayableService } from '@/services/procurementSupplierInvoiceService';
import type { ApInvoiceSupplierEntryOption } from '@/types/ap';
import { financeService } from '@/services/finance.service';
import { procurementAutoInvoiceService, type AutoInvoiceReceipt, type AutoInvoiceRequest } from '@/services/procurementAutoInvoiceService';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';
import { loadApprovedInvoiceRate } from '@/lib/finance/invoice-exchange-rate';
import type { VendorInvoice } from '@/types/ap';

export function ProcurementAutoInvoice({ onCreated }: { onCreated: (invoice: VendorInvoice) => void }) {
  const { hasAnyPermission } = useAuth();
  const [open, setOpen] = useState(false);
  const [partners, setPartners] = useState<ApInvoiceSupplierEntryOption[]>([]);
  const [supplier, setSupplier] = useState('');
  const selectedSupplier = partners.find(partner => partner.businessPartnerRoleId === supplier);
  const [receipts, setReceipts] = useState<AutoInvoiceReceipt[]>([]);
  const [selection, setSelection] = useState<Record<string, number>>({});
  const [date, setDate] = useState('');
  const [reference, setReference] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(false);
  const [loaded, setLoaded] = useState(false);
  const loadingVersion = useRef(0);
  const submitting = useRef(false);
  const retryRequest = useRef<AutoInvoiceRequest | null>(null);
  const changed = () => { retryRequest.current = null; setError(''); };
  const picked = receipts.filter(receipt => receipt.lines.some(line => selection[line.goodsReceiptNoteItemId] !== undefined));
  const currencies = [...new Set(picked.map(receipt => receipt.currencyCode))];
  const subtotal = receipts.flatMap(receipt => receipt.lines).reduce((sum, line) => sum + (selection[line.goodsReceiptNoteItemId] || 0) * line.unitPrice, 0);
  const invalid = receipts.flatMap(receipt => receipt.lines).some(line => {
    const value = selection[line.goodsReceiptNoteItemId];
    return value !== undefined && (!Number.isFinite(value) || value <= 0 || value > line.availableQuantity || Math.abs(value * 10000 - Math.round(value * 10000)) > 0.000001);
  });
  const loadSuppliers = async () => {
    setLoading(true); setError('');
    try {
      setPartners(await accountsPayableService.getInvoiceSupplierEntryOptions());
    } catch (e) { setError(getProcurementProblemMessage(e, 'Could not load suppliers. Retry.')); }
    finally { setLoading(false); }
  };
  const loadReceipts = async (id: string) => {
    const partner = partners.find(option => option.businessPartnerRoleId === id);
    if (!partner?.isTransactionReady) return;
    const version = ++loadingVersion.current;
    setSupplier(id); setReceipts([]); setSelection({}); setLoaded(false); changed(); setLoading(true);
    try {
      const result = await procurementAutoInvoiceService.receipts(partner.businessPartnerId);
      if (version === loadingVersion.current) { setReceipts(result); setLoaded(true); }
    } catch (e) { if (version === loadingVersion.current) setError(getProcurementProblemMessage(e, 'Could not load eligible GRNs. Retry.')); }
    finally { if (version === loadingVersion.current) setLoading(false); }
  };
  const submit = async () => {
    if (submitting.current || loading || !selectedSupplier?.isTransactionReady || !date || !reference.trim() || picked.length === 0 || currencies.length !== 1 || invalid) return;
    submitting.current = true; setBusy(true); setError('');
    try {
      if (!retryRequest.current) {
        const settings = await financeService.getSettings();
        const rate = await loadApprovedInvoiceRate({ module: 'AP', transactionCurrency: currencies[0], functionalCurrency: settings.baseCurrency,
          invoiceDate: new Date(`${date}T12:00:00`), settings }, (code, query) => financeService.getCurrentExchangeRate(code, query));
        retryRequest.current = { requestId: crypto.randomUUID(), businessPartnerId: selectedSupplier.businessPartnerId,
          businessPartnerRoleId: selectedSupplier.businessPartnerRoleId, supplierInvoiceNumber: reference.trim(),
          invoiceDate: date, exchangeRate: rate.rate, exchangeRateId: rate.exchangeRateId,
          lines: Object.entries(selection).map(([goodsReceiptNoteItemId, quantity]) => ({ goodsReceiptNoteItemId, quantity })) };
      }
      const invoice = await procurementAutoInvoiceService.create(retryRequest.current);
      setOpen(false); onCreated(invoice);
    } catch (e) { setError(getProcurementProblemMessage(e, 'Could not generate the draft. Your selection is retained for retry.')); }
    finally { submitting.current = false; setBusy(false); }
  };
  if (!hasAnyPermission(['Finance.AP.Invoices.Create', 'Finance.AP.Invoices.Manage'])) return null;
  return <>
    <Button variant="outline" onClick={() => {
      setOpen(true); setSupplier(''); setReceipts([]); setSelection({}); setLoaded(false);
      setDate(new Date().toISOString().slice(0, 10)); setReference(''); changed(); void loadSuppliers();
    }}>Auto Invoice</Button>
    <Dialog open={open} onOpenChange={value => { if (!busy) { setOpen(value); if (!value) loadingVersion.current++; } }}>
      <DialogContent className="max-w-5xl max-h-[90vh] overflow-y-auto">
        <DialogHeader><DialogTitle>Auto Invoice</DialogTitle><DialogDescription>
          Select a supplier and accepted GRNs to prepare one AP invoice. Quantities use PO units and exclude prior invoices, reserved drafts and dispatched returns. Review taxes and post from the Invoice page.
        </DialogDescription></DialogHeader>
        <fieldset disabled={busy} className="space-y-4">
          <div className="grid gap-4 md:grid-cols-3">
            <div><Label htmlFor="auto-supplier">Supplier</Label><Select value={supplier} onValueChange={id => void loadReceipts(id)} disabled={loading}>
              <SelectTrigger id="auto-supplier"><SelectValue placeholder="Select supplier" /></SelectTrigger><SelectContent>
                {partners.map(partner => <SelectItem key={partner.businessPartnerRoleId} value={partner.businessPartnerRoleId} disabled={!partner.isTransactionReady}>{partner.code} · {partner.name} ({partner.roleType}){partner.isTransactionReady ? '' : ` — ${partner.readinessMessage}`}</SelectItem>)}
              </SelectContent></Select></div>
            <div><Label htmlFor="auto-reference">Supplier invoice reference</Label><Input id="auto-reference" maxLength={100} value={reference} onChange={event => { changed(); setReference(event.target.value); }} /></div>
            <div><Label htmlFor="auto-date">Invoice date</Label><Input id="auto-date" type="date" value={date} onChange={event => { changed(); setDate(event.target.value); }} /></div>
          </div>
          <div className="flex items-center gap-3"><Button type="button" variant="outline" size="sm" disabled={loading} onClick={() => void (supplier ? loadReceipts(supplier) : loadSuppliers())}>Refresh eligible GRNs</Button>
            {loading && <span role="status">Loading…</span>}</div>
          {loaded && !receipts.length && <p>No accepted, uninvoiced GRNs are available for this supplier within your warehouse access.</p>}
          {receipts.map(receipt => <section key={receipt.goodsReceiptNoteId} className="rounded border p-3 space-y-3">
            <div className="flex gap-3 items-center"><Checkbox id={`grn-${receipt.goodsReceiptNoteId}`}
              checked={receipt.lines.every(line => selection[line.goodsReceiptNoteItemId] !== undefined)}
              onCheckedChange={checked => { changed(); setSelection(previous => {
                const next = { ...previous }; receipt.lines.forEach(line => { if (checked === true) next[line.goodsReceiptNoteItemId] = line.availableQuantity; else delete next[line.goodsReceiptNoteItemId]; }); return next;
              }); }} />
              <Label htmlFor={`grn-${receipt.goodsReceiptNoteId}`}>{receipt.receiptNumber} · {receipt.orderNumber} · {receipt.currencyCode}</Label></div>
            <div className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="text-left"><th>Item</th><th>Accepted</th><th>Returned</th><th>Invoiced / reserved</th><th>Available</th><th>Invoice quantity</th></tr></thead>
              <tbody>{receipt.lines.map(line => <tr key={line.goodsReceiptNoteItemId}>
                <td className="py-2"><Checkbox aria-label={`Select ${receipt.receiptNumber} ${line.description}`} checked={selection[line.goodsReceiptNoteItemId] !== undefined}
                  onCheckedChange={checked => { changed(); setSelection(previous => { const next = { ...previous }; if (checked === true) next[line.goodsReceiptNoteItemId] = line.availableQuantity; else delete next[line.goodsReceiptNoteItemId]; return next; }); }} /> {line.description} ({line.unit})</td><td>{line.acceptedQuantity}</td><td>{line.returnedQuantity}</td><td>{line.invoicedQuantity}</td><td>{line.availableQuantity}</td>
                <td><Input aria-label={`Invoice quantity ${receipt.receiptNumber} ${line.description}`} type="number" step="0.0001" min="0.0001" max={line.availableQuantity}
                  disabled={selection[line.goodsReceiptNoteItemId] === undefined} value={selection[line.goodsReceiptNoteItemId] ?? ''}
                  onChange={event => { changed(); setSelection(previous => ({ ...previous, [line.goodsReceiptNoteItemId]: Number(event.target.value) })); }} /></td>
              </tr>)}</tbody></table></div>
          </section>)}
          {currencies.length > 1 && <p role="alert" className="text-destructive">Choose GRNs in one currency for each consolidated invoice.</p>}
          {invalid && <p role="alert" className="text-destructive">Use positive quantities within the available amount, with up to four decimal places.</p>}
          <p>{picked.length} GRN(s) selected · Subtotal before tax: {currencies.length === 1 ? currencies[0] : ''} {subtotal.toFixed(2)}</p>
        </fieldset>
        {error && <p role="alert" className="text-destructive">{error}</p>}
        <DialogFooter><Button variant="outline" disabled={busy} onClick={() => setOpen(false)}>Cancel</Button>
          <Button onClick={() => void submit()} disabled={busy || loading || !supplier || !date || !reference.trim() || picked.length === 0 || currencies.length !== 1 || invalid}>{busy ? 'Generating…' : 'Generate one draft invoice'}</Button></DialogFooter>
      </DialogContent>
    </Dialog>
  </>;
}
