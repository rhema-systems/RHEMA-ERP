'use client';

import React, { useEffect, useRef, useState } from 'react';
import { toast } from 'sonner';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { arService } from '@/services/ar-service';
import { taxDataService } from '@/services/finance/tax-data.service';
import { inventoryDisposalService, type InventoryDisposal } from '@/services/inventoryDisposalService';
import type { Customer } from '@/types/ar';
import type { TaxGroup } from '@/types/tax';
import { SourceDocumentDimensionDefaultsPanel } from '@/components/finance/dimensions/source-document-dimension-panel';
import { toFinancePostingDimensionValues } from '@/lib/finance/source-document-dimensions';

export function InventoryDisposalAuctionDialog({ disposal, open, onOpenChange, onCreated }: {
  disposal: InventoryDisposal; open: boolean; onOpenChange: (open: boolean) => void; onCreated: (value: InventoryDisposal) => void;
}) {
  const [search, setSearch] = useState('');
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [groups, setGroups] = useState<TaxGroup[]>([]);
  const [customer, setCustomer] = useState('');
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10));
  const [treatment, setTreatment] = useState('');
  const [group, setGroup] = useState('');
  const [prices, setPrices] = useState<Record<string, string>>({});
  const [dimensionDefaults, setDimensionDefaults] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const [lookupError, setLookupError] = useState('');
  const [taxError, setTaxError] = useState('');
  const attempt = useRef<{ payload: string; key: string } | undefined>(undefined);

  useEffect(() => {
    if (!open) return;
    let current = true;
    const timer = window.setTimeout(() => {
      arService.getCustomers({ searchTerm: search, pageSize: 50, isActive: true, includeBalances: false })
        .then(result => { if (current) { setCustomers(result.items); setLookupError(''); } })
        .catch(() => { if (current) setLookupError('Customers could not be loaded. Check your customer lookup access.'); });
    }, 250);
    return () => { current = false; window.clearTimeout(timer); };
  }, [open, search]);
  useEffect(() => {
    if (!open) return;
    let current = true;
    taxDataService.getActiveTaxGroups('Sales').then(result => { if (current) { setGroups(result); setTaxError(''); } })
      .catch(() => { if (current) setTaxError('Sales tax groups could not be loaded.'); });
    return () => { current = false; };
  }, [open]);

  const valid = disposal.lines.length > 0 && !!customer && !!date && !!treatment && (treatment !== '1' || !!group) && disposal.lines.every(line => {
    const price = Number(prices[line.id]);
    return Number.isFinite(price) && price > 0 && Math.abs(price * 100 - Math.round(price * 100)) < 0.000001;
  });
  const total = disposal.lines.reduce((sum, line) => sum + line.quantity * (Number(prices[line.id]) || 0), 0);
  const save = async () => {
    if (!valid || busy) return;
    const payload = { businessPartnerId: customer, invoiceDate: date,
      financeDimensions: { defaultDimensions: toFinancePostingDimensionValues(dimensionDefaults), lines: [], applyDefaultToEligibleLines: true },
      lines: disposal.lines.map(line => ({ disposalLineId: line.id, unitPrice: Number(prices[line.id]),
        taxTreatment: Number(treatment), taxGroupId: treatment === '1' ? group : undefined })) };
    const serialized = JSON.stringify(payload);
    if (attempt.current?.payload !== serialized) attempt.current = { payload: serialized, key: `auction:${crypto.randomUUID()}` };
    setBusy(true);
    try {
      const result = await inventoryDisposalService.createAuctionInvoice(disposal, { ...payload, idempotencyKey: attempt.current.key });
      onCreated(result); onOpenChange(false); toast.success('Auction invoice draft created.');
    } catch (error) {
      const problem = (error as { response?: { data?: { detail?: string; message?: string; code?: string } } }).response?.data;
      toast.error((problem?.detail || problem?.message || 'The auction invoice could not be created.') + (problem?.code ? ` (${problem.code})` : ''));
    } finally { setBusy(false); }
  };
  return <Dialog open={open} onOpenChange={value => { if (!busy) onOpenChange(value); }}>
    <DialogContent className="max-h-[90dvh] overflow-y-auto max-w-3xl"><DialogHeader><DialogTitle>Create auction invoice</DialogTitle>
      <DialogDescription>{disposal.disposalNumber} · Finance AR draft · {disposal.currencyCode}</DialogDescription></DialogHeader>
      {lookupError && <p role="alert" className="text-sm text-destructive">{lookupError}</p>}
      {taxError && <p role="alert" className="text-sm text-destructive">{taxError}</p>}
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1"><Label htmlFor="auction-customer-search">Find customer</Label><Input id="auction-customer-search" value={search} onChange={event => setSearch(event.target.value)} />
          <Select value={customer} onValueChange={setCustomer}><SelectTrigger aria-label="Auction customer"><SelectValue placeholder="Select customer" /></SelectTrigger>
            <SelectContent>{customers.map(value => <SelectItem key={value.id} value={value.id}>{value.customerCode} — {value.customerName}</SelectItem>)}</SelectContent></Select></div>
        <div className="space-y-1"><Label htmlFor="auction-invoice-date">Invoice date</Label><Input id="auction-invoice-date" type="date" value={date} onChange={event => setDate(event.target.value)} /></div>
        <div className="space-y-1"><Label>Tax treatment</Label><Select value={treatment} onValueChange={setTreatment}><SelectTrigger aria-label="Auction tax treatment"><SelectValue placeholder="Select tax treatment" /></SelectTrigger>
          <SelectContent><SelectItem value="1">Standard</SelectItem><SelectItem value="2">Exempt</SelectItem><SelectItem value="3">Zero rated</SelectItem><SelectItem value="4">Out of scope</SelectItem></SelectContent></Select></div>
        {treatment === '1' && <div className="space-y-1"><Label>Tax group</Label><Select value={group} onValueChange={setGroup}><SelectTrigger aria-label="Auction tax group"><SelectValue placeholder="Select sales tax" /></SelectTrigger>
          <SelectContent>{groups.map(value => <SelectItem key={value.id} value={value.id}>{value.name}</SelectItem>)}</SelectContent></Select></div>}
      </div>
      <div className="max-h-80 overflow-auto"><Table><TableHeader><TableRow><TableHead>Item</TableHead><TableHead>Quantity</TableHead><TableHead>Unit price</TableHead><TableHead className="text-right">Amount</TableHead></TableRow></TableHeader>
        <TableBody>{disposal.lines.map(line => <TableRow key={line.id}><TableCell>{line.itemCode} — {line.itemName}</TableCell><TableCell>{line.quantity} {line.unitOfMeasure}</TableCell>
          <TableCell><Input className="h-8 w-28" aria-label={`Auction price ${line.itemCode}`} type="number" min="0.01" step="0.01" value={prices[line.id] ?? ''} onChange={event => setPrices(previous => ({ ...previous, [line.id]: event.target.value }))} /></TableCell>
          <TableCell className="text-right">{(line.quantity * (Number(prices[line.id]) || 0)).toFixed(2)}</TableCell></TableRow>)}</TableBody></Table></div>
      <SourceDocumentDimensionDefaultsPanel effectiveDate={date} values={dimensionDefaults} onChange={setDimensionDefaults} disabled={busy} />
      <DialogFooter className="items-center"><span className="mr-auto text-sm">Subtotal: {disposal.currencyCode} {total.toFixed(2)}</span><Button variant="outline" disabled={busy} onClick={() => onOpenChange(false)}>Cancel</Button><Button disabled={busy || !valid} onClick={save}>{busy ? 'Creating…' : 'Create invoice draft'}</Button></DialogFooter>
    </DialogContent>
  </Dialog>;
}
