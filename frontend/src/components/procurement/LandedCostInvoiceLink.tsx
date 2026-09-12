'use client';

import React, { useState } from 'react';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { accountsPayableService } from '@/services/accountsPayableService';
import { inventoryManagementService, type LandedCostItemDto } from '@/services/inventoryManagementService';
import { useAuth } from '@/hooks/use-auth';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';

type InvoiceChoice = { id: string; invoiceNumber: string; supplierName: string; supplierInvoiceNumber?: string | null };

export function LandedCostInvoiceLink({ voucherId, item, onChanged }: {
  voucherId: string; item: LandedCostItemDto; onChanged: () => void;
}) {
  const { hasAnyPermission } = useAuth();
  const canLink = hasAnyPermission(['Finance.AP.Invoices.Create', 'Finance.AP.Invoices.Edit', 'Finance.AP.Invoices.Write']);
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [choices, setChoices] = useState<InvoiceChoice[]>([]);
  const [selected, setSelected] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [searched, setSearched] = useState(false);
  if (item.invoiceId) return <Link className="text-primary underline" href={`/finance/ap/invoices/${item.invoiceId}`}>{item.invoiceNumber}</Link>;
  if (item.invoiceNumber) return <span>{item.invoiceNumber} (unavailable)</span>;
  if (!canLink) return <span className="text-muted-foreground">Not linked</span>;
  async function find() {
    setBusy(true); setError(''); setSelected(''); setChoices([]);
    try {
      const result = await accountsPayableService.getInvoices({ searchTerm: query.trim(), pageSize: 20 });
      setChoices(result.items); setSearched(true);
    } catch (e) { setError(getProcurementProblemMessage(e, 'Could not load invoices.')); }
    finally { setBusy(false); }
  }
  async function save() {
    setBusy(true); setError('');
    try { await inventoryManagementService.linkLandedCostInvoice(voucherId, item.id, selected); setOpen(false); onChanged(); }
    catch (e) { setError(getProcurementProblemMessage(e, 'Could not link this invoice.')); }
    finally { setBusy(false); }
  }
  return <>
    <Button type="button" variant="outline" size="sm" onClick={() => { setOpen(true); setError(''); setSelected(''); setChoices([]); setSearched(false); }}>Link invoice</Button>
    <Dialog open={open} onOpenChange={value => { if (!busy) setOpen(value); }}>
      <DialogContent><DialogHeader><DialogTitle>Link supplier invoice</DialogTitle>
        <DialogDescription>Link the saved invoice that bills this charge. This records a reference only; it does not add charges, change matching, or post the invoice.</DialogDescription></DialogHeader>
        <p className="text-sm">{item.description} · {item.currency} {item.amount.toFixed(2)}</p>
        <label className="text-sm" htmlFor={`invoice-search-${item.id}`}>Invoice number or supplier reference</label>
        <div className="flex gap-2"><Input id={`invoice-search-${item.id}`} value={query} onChange={e => setQuery(e.target.value)} disabled={busy} />
          <Button type="button" onClick={find} disabled={busy || !query.trim()}>Find invoice</Button></div>
        {choices.length > 0 && <label className="text-sm">Saved invoice
          <select aria-label="Saved invoice" className="mt-1 w-full rounded-md border p-2" value={selected} onChange={e => setSelected(e.target.value)} disabled={busy}>
            <option value="">Select the invoice that includes this charge</option>
            {choices.map(i => <option key={i.id} value={i.id}>{i.invoiceNumber} · {i.supplierName}{i.supplierInvoiceNumber ? ` · ${i.supplierInvoiceNumber}` : ''}</option>)}
          </select></label>}
        {searched && choices.length === 0 && !error && <p className="text-sm">No saved invoices match. Record the actual supplier invoice in Accounts Payable first.</p>}
        {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
        <div className="flex justify-end gap-2"><Button type="button" variant="outline" disabled={busy} onClick={() => setOpen(false)}>Cancel</Button>
          <Button type="button" disabled={busy || !selected} onClick={save}>{busy ? 'Working…' : 'Link invoice'}</Button></div>
      </DialogContent>
    </Dialog>
  </>;
}
