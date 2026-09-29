'use client';

import React, { useEffect, useRef, useState } from 'react';
import Link from 'next/link';
import { Check, ChevronsUpDown, RefreshCw } from 'lucide-react';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { inventoryReturnCreditService, type InventoryReturnCreditNote, type InventoryReturnCreditSource } from '@/services/inventoryReturnCreditService';
import type { SupplierReturnAccountingGroup } from '@/services/supplierReturnService';

const errorText = (error: unknown) => {
  const data = (error as { response?: { data?: { detail?: string; message?: string; error?: string; code?: string } } })?.response?.data;
  return (data?.detail || data?.message || data?.error || (error instanceof Error ? error.message : 'Unable to load supplier credit.')) + (data?.code ? ` (${data.code})` : '');
};
const hasId = (value?: string | null) => !!value && value !== '00000000-0000-0000-0000-000000000000';
export const isReturnCreditResolved = (note: InventoryReturnCreditNote) =>
  (note.statusName === 'Posted' || note.status === 'Posted') && !!note.directInvoiceAppliedAt &&
  note.totalAmount > 0 && (note.directInvoiceAppliedAmount || 0) >= note.totalAmount &&
  hasId(note.journalEntryId) && hasId(note.postingEventId) &&
  hasId(note.returnDispatchPostingEventId) && hasId(note.returnDispatchJournalEntryId);

export function SupplierReturnCreditPanel({ returnId, returnNumber, reason, accountingGroups, alreadyResolved = false, onResolved }: { returnId: string; returnNumber: string; reason: string; accountingGroups?: SupplierReturnAccountingGroup[]; alreadyResolved?: boolean; onResolved?: (returnId: string, resolved: boolean) => void }) {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canRead = hasPermission('Finance.Read');
  const canManage = hasPermission('Finance.AP.SupplierDebitNotes.Manage');
  const [notes, setNotes] = useState<InventoryReturnCreditNote[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [refresh, setRefresh] = useState(0);
  const [open, setOpen] = useState(false);
  const [sources, setSources] = useState<InventoryReturnCreditSource[]>([]);
  const [sourceLoading, setSourceLoading] = useState(false);
  const [sourceError, setSourceError] = useState('');
  const [invoiceId, setInvoiceId] = useState('');
  const [targetInvoiceId, setTargetInvoiceId] = useState('');
  const [reference, setReference] = useState('');
  const [creditDate, setCreditDate] = useState('');
  const [selectOpen, setSelectOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const inFlight = useRef(false);
  const selected = sources.find(source => source.invoiceId === invoiceId);
  const grouped = !!accountingGroups?.length;
  const groupNote = (group: SupplierReturnAccountingGroup) => notes.find(note =>
    note.inventorySupplierReturnAccountingGroupId === group.id || note.id === group.supplierDebitNoteId);
  const groupResolved = (group: SupplierReturnAccountingGroup) => group.originalVendorInvoiceId
    ? !!groupNote(group) && isReturnCreditResolved(groupNote(group)!)
    : group.financeResolutionCompleted;
  const allResolved = grouped ? accountingGroups!.every(groupResolved) : notes.some(isReturnCreditResolved);

  useEffect(() => {
    if (canRead && !loading && !error) onResolved?.(returnId, allResolved);
  }, [canRead, loading, error, allResolved, returnId, onResolved]);

  useEffect(() => {
    if (!canRead) { setLoading(false); return; }
    let active = true;
    setLoading(true); setError(''); setNotes([]);
    inventoryReturnCreditService.getNotes(returnId).then(values => {
      if (!active) return;
      if (values.some(value => value.inventoryPurchaseReturnId?.toLowerCase() !== returnId.toLowerCase())) {
        throw new Error('The returned credit does not belong to this supplier return.');
      }
      setNotes(values);
    }).catch(failure => { if (active) setError(errorText(failure)); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [returnId, canRead, refresh]);

  useEffect(() => {
    if (!open) return;
    let active = true;
    setSourceLoading(true); setSourceError(''); setSources([]); setInvoiceId('');
    inventoryReturnCreditService.getSources(returnId).then(values => {
      if (!active) return;
      const available = targetInvoiceId ? values.filter(value => value.invoiceId === targetInvoiceId) : values;
      setSources(available); if (available.length === 1) setInvoiceId(available[0].invoiceId);
    }).catch(failure => { if (active) setSourceError(errorText(failure)); })
      .finally(() => { if (active) setSourceLoading(false); });
    return () => { active = false; };
  }, [open, returnId, targetInvoiceId]);

  const begin = (originalInvoiceId = '') => {
    const today = new Date();
    setTargetInvoiceId(originalInvoiceId);
    setReference(''); setCreditDate(`${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`);
    setOpen(true);
  };
  const save = async () => {
    if (!selected || !reference.trim() || !creditDate || inFlight.current) return;
    inFlight.current = true; setSaving(true); setSourceError('');
    try {
      const note = await inventoryReturnCreditService.create(returnId, {
        originalVendorInvoiceId: selected.invoiceId, supplierCreditNoteReference: reference.trim(), creditDate, reason,
      });
      if (note.inventoryPurchaseReturnId?.toLowerCase() !== returnId.toLowerCase()) throw new Error('The saved credit does not belong to this return. Refresh before retrying.');
      if (selected.accountingGroupId && note.inventorySupplierReturnAccountingGroupId?.toLowerCase() !== selected.accountingGroupId.toLowerCase())
        throw new Error('The saved credit does not belong to the selected invoice group. Refresh before retrying.');
      setNotes(current => [...current.filter(value => value.id !== note.id), note]); setOpen(false);
      toast({ title: 'Supplier credit draft ready', description: 'Open the credit to review and post it in Finance.' });
    } catch (failure) { setSourceError(errorText(failure)); }
    finally { inFlight.current = false; setSaving(false); }
  };

  if (!canRead) return alreadyResolved ? null : <p className="mt-2 text-muted-foreground">Finance can link the supplier credit to the original invoice.</p>;
  return <div className="mt-3 border-t pt-3" aria-label="AP credit">
    <div className="flex items-center justify-between gap-2"><span className="font-medium">Supplier credit</span>
      <Button size="icon" variant="ghost" aria-label="Refresh supplier credit" title="Refresh" disabled={loading || saving} onClick={() => setRefresh(value => value + 1)}><RefreshCw className="h-4 w-4" /></Button></div>
    {loading ? <p className="text-muted-foreground">Checking linked credit...</p> : error ? <p role="alert" className="text-red-700">{error}</p> : grouped ?
      <div className="space-y-4">{[false, true].map(completed => {
        const groups = accountingGroups!.filter(group => groupResolved(group) === completed);
        return groups.length ? <section key={String(completed)} aria-label={completed ? 'Completed accounting groups' : 'Pending accounting groups'}>
          <h4 className="mb-2 text-sm font-medium">{completed ? 'Completed' : 'Pending'}</h4>
          <div className="space-y-2">{groups.map(group => {
            const note = groupNote(group);
            const creditId = note?.id || group.supplierDebitNoteId;
            return <div key={group.id} className="flex flex-wrap items-center justify-between gap-3 rounded-md border p-3">
              <div className="space-y-1">
                {group.originalVendorInvoiceId ? <Link className="font-medium text-primary underline-offset-4 hover:underline" href={`/procurement/supplier-invoices/${encodeURIComponent(group.originalVendorInvoiceId)}`}>{group.originalInvoiceNumber || 'Original supplier invoice'}</Link> : <p className="font-medium">Uninvoiced goods</p>}
                <p className="text-sm text-muted-foreground">{group.baseQuantity.toLocaleString()} units · {group.functionalCurrency} {(group.originalVendorInvoiceId ? group.carryingAmount : group.originalAccrualAmount).toLocaleString(undefined, { minimumFractionDigits: 2 })}</p>
                <p className={`text-sm ${completed ? 'text-emerald-700' : 'text-amber-700'}`}>{group.originalVendorInvoiceId ? (completed ? 'Credit posted and applied to original invoice' : creditId ? 'Credit saved · Finance resolution pending' : 'Supplier credit pending') : completed ? 'Receipt accrual cleared' : 'Receipt accrual resolution pending'}</p>
              </div>
              {creditId ? <Button asChild size="sm" variant="outline"><Link href={`/finance/ap/supplier-debit-notes/${encodeURIComponent(creditId)}`}>Open {note?.debitNoteNumber || group.supplierDebitNoteNumber || 'credit'}</Link></Button> : group.originalVendorInvoiceId && canManage ? <Button size="sm" onClick={() => begin(group.originalVendorInvoiceId!)}>Create credit for {group.originalInvoiceNumber || 'invoice'}</Button> : null}
            </div>;
          })}</div>
        </section> : null;
      })}</div> : notes.length ?
      notes.map(note => <div key={note.id} className="flex flex-wrap items-center justify-between gap-3">
        <div><p className={isReturnCreditResolved(note) ? 'font-medium text-emerald-700' : 'text-amber-700'}>{isReturnCreditResolved(note) ? 'Credit posted and applied to original invoice' : 'Credit saved · Finance resolution pending'}</p>
          <p className="text-muted-foreground">{note.debitNoteNumber} · {note.statusName || note.status} · {note.currencyCode} {note.totalAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}</p></div>
        <Button asChild size="sm" variant="outline"><Link href={`/finance/ap/supplier-debit-notes/${encodeURIComponent(note.id)}`}>Open credit</Link></Button>
      </div>) : canManage ? <Button size="sm" onClick={() => begin()}>Create credit draft</Button> : <p className="text-muted-foreground">No linked AP credit. An AP officer can create it.</p>}

    <Dialog open={open} onOpenChange={value => { if (!saving) setOpen(value); }}><DialogContent className="flex flex-col overflow-hidden" style={{ width: '560px', maxWidth: 'calc(100vw - 32px)', height: 'min(430px, calc(100vh - 48px))' }}>
      <DialogHeader><DialogTitle>Create supplier credit</DialogTitle><DialogDescription>{returnNumber} · Credit against the original goods invoice.</DialogDescription></DialogHeader>
      <div className="min-h-0 flex-1 space-y-4 overflow-y-auto">
        <div className="space-y-1"><Label>Original invoice</Label><Popover open={selectOpen} onOpenChange={setSelectOpen}><PopoverTrigger asChild>
          <Button variant="outline" role="combobox" aria-label="Original invoice" aria-expanded={selectOpen} disabled={sourceLoading || saving || !sources.length} className="w-full justify-between font-normal"><span className="truncate">{selected ? `${selected.invoiceNumber}${selected.supplierInvoiceNumber ? ' · ' + selected.supplierInvoiceNumber : ''}` : sourceLoading ? 'Loading invoices...' : 'Select invoice'}</span><ChevronsUpDown className="ml-2 h-4 w-4 shrink-0" /></Button>
        </PopoverTrigger><PopoverContent className="w-[480px] max-w-[calc(100vw-48px)] p-0"><Command><CommandInput placeholder="Search original invoice..." /><CommandList><CommandEmpty>No invoice found.</CommandEmpty><CommandGroup>{sources.map(source => <CommandItem key={source.invoiceId} value={`${source.invoiceId} ${source.invoiceNumber} ${source.supplierInvoiceNumber || ''}`} onSelect={() => { setInvoiceId(source.invoiceId); setSelectOpen(false); }}><Check className={`mr-2 h-4 w-4 ${invoiceId === source.invoiceId ? 'opacity-100' : 'opacity-0'}`} />{source.invoiceNumber} {source.supplierInvoiceNumber ? `· ${source.supplierInvoiceNumber}` : ''}</CommandItem>)}</CommandGroup></CommandList></Command></PopoverContent></Popover>
          {!sourceLoading && !sourceError && !sources.length && <p className="text-sm text-amber-700">No eligible posted invoice is linked to these returned goods. Review the original goods invoice in AP.</p>}
        </div>
        <div className="grid grid-cols-2 gap-3"><div className="space-y-1"><Label htmlFor="supplier-return-credit-reference">Supplier credit reference</Label><Input id="supplier-return-credit-reference" maxLength={100} value={reference} disabled={saving} onChange={event => setReference(event.target.value)} /></div>
          <div className="space-y-1"><Label htmlFor="supplier-return-credit-date">Credit date</Label><Input id="supplier-return-credit-date" type="date" value={creditDate} disabled={saving} onChange={event => setCreditDate(event.target.value)} /></div></div>
        {selected && <p className="text-sm text-muted-foreground">Invoice outstanding: {selected.currencyCode} {selected.outstandingAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}. Quantities and tax come from its returned lines.</p>}
        {sourceError && <p role="alert" className="text-sm text-red-700">{sourceError}</p>}
      </div><DialogFooter className="shrink-0"><Button variant="outline" disabled={saving} onClick={() => setOpen(false)}>Cancel</Button><Button disabled={saving || sourceLoading || !selected || !reference.trim() || !creditDate} onClick={() => void save()}>{saving ? 'Saving...' : 'Create draft'}</Button></DialogFooter>
    </DialogContent></Dialog>
  </div>;
}
