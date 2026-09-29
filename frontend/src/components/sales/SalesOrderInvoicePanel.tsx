'use client';

import React, { useEffect, useRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { PostingAccountPicker } from '@/components/finance/PostingAccountPicker';
import { SourceDocumentDimensionPanel } from '@/components/finance/dimensions/source-document-dimension-panel';
import { toFinancePostingDimensionValues } from '@/lib/finance/source-document-dimensions';
import { salesOrderInvoiceService as service, type SalesInvoiceDetail, type SalesInvoiceDistribution, type SalesInvoiceLineSelection } from '@/services/salesOrderInvoiceService';
import type { SalesOrderDetailDto } from '@/services/salesOrderService';
import type { Account } from '@/types/finance';
import { financeDataService } from '@/services/finance/finance-data.service';
import { taxDataService } from '@/services/finance/tax-data.service';
import { financeService } from '@/services/finance.service';
import { loadApprovedInvoiceRate } from '@/lib/finance/invoice-exchange-rate';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';

type Props = { order: SalesOrderDetailDto; onChanged: () => Promise<void> };
const treatments = ['Standard', 'Exempt', 'ZeroRated', 'OutOfScope'] as const;

export function SalesOrderInvoicePanel({ order, onChanged }: Props) {
  const [detail, setDetail] = useState<SalesInvoiceDetail>();
  const [distribution, setDistribution] = useState<SalesInvoiceDistribution>();
  const [open, setOpen] = useState(false);
  const [distributionOpen, setDistributionOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10));
  const [dueDate, setDueDate] = useState('');
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [groups, setGroups] = useState<{ id: string; name: string }[]>([]);
  const [selections, setSelections] = useState<SalesInvoiceLineSelection[]>([]);
  const [dimensionDefaults, setDimensionDefaults] = useState<Record<string, string>>({});
  const [lineDimensions, setLineDimensions] = useState<Record<string, Record<string, string>>>({});
  const [freight, setFreight] = useState<{ account?: string; group?: string; treatment: SalesInvoiceLineSelection['taxTreatment'] }>({ treatment: 'PendingReview' });
  const replay = useRef<{ fingerprint: string; key: string } | undefined>(undefined);
  const eligible = !order.invoiceId && ['Standard', '1'].includes(order.orderType) && ['Confirmed', 'PartiallyDelivered', 'Delivered', '3', '4', '5'].includes(order.status);
  const money = (value: number) => `${detail?.invoice.currencyCode || order.currency} ${value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  useEffect(() => {
    let current = true;
    setDetail(undefined); setError('');
    if (order.invoiceId) service.get(order.id).then(value => { if (current) setDetail(value); })
      .catch(failure => { if (current) setError(getProcurementProblemMessage(failure)); });
    return () => { current = false; };
  }, [order.id, order.invoiceId]);
  const prepare = async () => {
    setOpen(true); setError(''); setBusy(true);
    setDimensionDefaults({}); setLineDimensions({});
    setSelections(order.lines.map(line => ({ salesOrderLineId: line.id, glAccountId: line.glAccountId,
      taxGroupId: line.taxGroupId || order.taxGroupId, taxTreatment: 'PendingReview' })));
    try {
      const [catalogue, taxes] = await Promise.all([financeDataService.getAccounts({ status: 'Active', accountType: 'Revenue' }), taxDataService.getTaxGroups({ isActive: true, applicability: 'Sales' })]);
      setAccounts(catalogue.filter(account => account.allowDirectPosting && !account.isControlAccount)); setGroups(taxes);
    } catch (failure) { setError(getProcurementProblemMessage(failure)); }
    finally { setBusy(false); }
  };
  const generate = async () => {
    if (selections.some(line => line.taxTreatment === 'PendingReview' || (line.taxTreatment === 'Standard' && !line.taxGroupId)) ||
      ((order.shippingAmount || 0) > 0 && (!freight.account || freight.treatment === 'PendingReview' || (freight.treatment === 'Standard' && !freight.group)))) {
      setError('Select the applicable tax treatment and required tax group for every invoice line.'); return;
    }
    setBusy(true); setError('');
    try {
      const settings = await financeService.getSettings();
      if (!settings.baseCurrency) throw new Error('Configure the Finance base currency before invoicing.');
      const rate = await loadApprovedInvoiceRate({ module: 'AR', transactionCurrency: order.currency, functionalCurrency: settings.baseCurrency, invoiceDate: new Date(`${date}T00:00:00`), settings },
        (currency, query) => financeService.getCurrentExchangeRate(currency, query));
      const payload = { rowVersion: order.rowVersion, invoiceDate: date, dueDate: dueDate || undefined, exchangeRateId: rate.exchangeRateId,
        lines: selections, freightAccountId: freight.account, freightTaxGroupId: freight.group, freightTaxTreatment: freight.treatment,
        financeDimensions: {
          defaultDimensions: toFinancePostingDimensionValues(dimensionDefaults),
          lines: selections.flatMap(line => line.glAccountId && Object.prototype.hasOwnProperty.call(lineDimensions, line.salesOrderLineId) ? [{
            sourceLineId: line.salesOrderLineId, accountId: line.glAccountId,
            dimensions: toFinancePostingDimensionValues(lineDimensions[line.salesOrderLineId]),
          }] : []),
          applyDefaultToEligibleLines: true,
        } };
      const fingerprint = JSON.stringify(payload);
      if (replay.current?.fingerprint !== fingerprint) replay.current = { fingerprint, key: crypto.randomUUID() };
      const saved = await service.generate(order.id, { ...payload, idempotencyKey: replay.current.key });
      setDetail(saved); setOpen(false); await onChanged();
    } catch (failure) { setError(getProcurementProblemMessage(failure)); }
    finally { setBusy(false); }
  };
  const preview = async () => {
    setBusy(true); setError('');
    try { setDistribution(await service.distribution(order.id)); setDistributionOpen(true); }
    catch (failure) { setError(getProcurementProblemMessage(failure)); }
    finally { setBusy(false); }
  };
  const action = async (kind: 'submit' | 'post') => {
    setBusy(true); setError('');
    try { setDetail(await service[kind](order.id)); setDistribution(undefined); }
    catch (failure) { setError(getProcurementProblemMessage(failure)); }
    finally { setBusy(false); }
  };
  if (!eligible && !order.invoiceId) return null;
  return <Card><CardHeader className="flex-row items-center justify-between gap-2 py-3"><CardTitle className="text-base">Customer invoice</CardTitle><div className="flex gap-2">
    {eligible && <Button size="sm" onClick={() => void prepare()} disabled={busy}>Generate invoice</Button>}
    {detail && <><Button size="sm" variant="outline" disabled={busy} onClick={() => void preview()}>Distribution</Button>
      {detail.canSubmit && <Button size="sm" disabled={busy} onClick={() => void action('submit')}>Submit invoice</Button>}
      {detail.canPost && <Button size="sm" disabled={busy} onClick={() => void preview()}>Review and post</Button>}</>}
  </div></CardHeader><CardContent className="space-y-3">
    {error && !open && !distributionOpen && <p role="alert" className="text-sm text-destructive">{error}</p>}
    {detail && <><div className="flex flex-wrap gap-4 text-sm"><strong>{detail.invoice.invoiceNumber}</strong><span>{detail.invoice.status}</span><span className="ml-auto font-semibold">{money(detail.invoice.totalAmount)}</span></div>
      <div className="overflow-auto"><table className="w-full text-sm"><thead><tr className="border-b text-left"><th className="p-2">Description</th><th className="p-2 text-right">Quantity</th><th className="p-2 text-right">Unit price</th><th className="p-2 text-right">Tax</th></tr></thead><tbody>{detail.invoice.lineItems.map(line => <tr key={line.id} className="border-b"><td className="p-2 whitespace-pre-line">{line.description}</td><td className="p-2 text-right">{line.quantity}</td><td className="p-2 text-right">{money(line.unitPrice)}</td><td className="p-2 text-right">{money(line.taxAmount)}</td></tr>)}</tbody></table></div></>}
  </CardContent>
    <Dialog open={open} onOpenChange={value => { if (!busy) setOpen(value); }}><DialogContent aria-describedby={undefined} className="max-h-[90dvh] overflow-y-auto sm:max-w-5xl"><DialogHeader><DialogTitle>Generate customer invoice · {order.orderNumber}</DialogTitle></DialogHeader>
      <div className="grid grid-cols-2 gap-3"><label className="text-sm">Invoice date<Input type="date" value={date} onChange={event => setDate(event.target.value)} disabled={busy} /></label><label className="text-sm">Due date<Input type="date" value={dueDate} onChange={event => setDueDate(event.target.value)} disabled={busy} /></label></div>
      {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
      <div className="overflow-auto"><table className="w-full min-w-[650px] text-sm"><thead><tr className="border-b text-left"><th className="p-2">Item</th><th className="w-64 p-2">Revenue account</th><th className="p-2">Tax treatment</th><th className="p-2">Tax group</th></tr></thead><tbody>
        {selections.map((selection, index) => <tr key={selection.salesOrderLineId} className="border-b"><td className="p-2">{order.lines[index]?.itemName}<div className="text-xs text-muted-foreground">{order.lines[index]?.quantity} × {money(order.lines[index]?.unitPrice || 0)}</div></td><td className="p-2"><PostingAccountPicker id={`sales-revenue-${selection.salesOrderLineId}`} accounts={accounts} value={selection.glAccountId} disabled={busy || !!order.lines[index]?.glAccountId} onChange={value => setSelections(previous => previous.map((line, n) => n === index ? { ...line, glAccountId: value || undefined } : line))} /></td><td className="p-2"><select aria-label={`Tax treatment ${index + 1}`} className="h-9 rounded border bg-background px-2" value={selection.taxTreatment} disabled={busy} onChange={event => setSelections(previous => previous.map((line, n) => n === index ? { ...line, taxTreatment: event.target.value as SalesInvoiceLineSelection['taxTreatment'] } : line))}><option value="PendingReview">Select treatment</option>{treatments.map(value => <option key={value}>{value}</option>)}</select></td><td className="p-2"><select aria-label={`Tax group ${index + 1}`} className="h-9 max-w-48 rounded border bg-background px-2" value={selection.taxGroupId || ''} disabled={busy || !!order.lines[index]?.taxGroupId || !!order.taxGroupId} onChange={event => setSelections(previous => previous.map((line, n) => n === index ? { ...line, taxGroupId: event.target.value || undefined } : line))}><option value="">Select group</option>{groups.map(group => <option key={group.id} value={group.id}>{group.name}</option>)}</select></td></tr>)}
        {(order.shippingAmount || 0) > 0 && <tr><td className="p-2">Freight / Shipping · {money(order.shippingAmount || 0)}</td><td className="p-2"><PostingAccountPicker id="sales-freight-account" accounts={accounts} value={freight.account} disabled={busy} allowClear={false} onChange={value => setFreight(previous => ({ ...previous, account: value || undefined }))} /></td><td className="p-2"><select aria-label="Freight tax treatment" className="h-9 rounded border bg-background" value={freight.treatment} disabled={busy} onChange={event => setFreight(previous => ({ ...previous, treatment: event.target.value as SalesInvoiceLineSelection['taxTreatment'] }))}><option value="PendingReview">Select treatment</option>{treatments.map(value => <option key={value}>{value}</option>)}</select></td><td className="p-2"><select aria-label="Freight tax group" value={freight.group || ''} disabled={busy} onChange={event => setFreight(previous => ({ ...previous, group: event.target.value || undefined }))}><option value="">Select group</option>{groups.map(group => <option key={group.id} value={group.id}>{group.name}</option>)}</select></td></tr>}
      </tbody></table></div>
      <SourceDocumentDimensionPanel
        context={{ sourceModule: 'AR', sourceDocumentType: 'CustomerInvoice', postingAction: 'Post', sourceRoute: 'sales.orders.customer-invoices', contractVersion: '1.0' }}
        effectiveDate={date}
        lines={selections.map((line, index) => ({ id: line.salesOrderLineId, accountId: line.glAccountId, accountLabel: order.lines[index]?.description || order.lines[index]?.itemName }))}
        defaultValues={dimensionDefaults} lineValues={lineDimensions}
        onDefaultValuesChange={setDimensionDefaults} onLineValuesChange={setLineDimensions}
        disabled={busy}
      />
      <DialogFooter><Button variant="outline" disabled={busy} onClick={() => setOpen(false)}>Cancel</Button><Button disabled={busy || !date} onClick={() => void generate()}>{busy ? 'Working…' : 'Create draft invoice'}</Button></DialogFooter>
    </DialogContent></Dialog>
    <Dialog open={distributionOpen} onOpenChange={setDistributionOpen}><DialogContent aria-describedby={undefined} className="max-h-[90dvh] overflow-y-auto sm:max-w-4xl"><DialogHeader><DialogTitle>Invoice distribution</DialogTitle></DialogHeader>
      {distribution && <><p className="text-sm">{distribution.isEstimated ? 'Estimated posting · inventory cost is recalculated when posted.' : 'Posted distribution'} · {distribution.currencyCode}</p><div className="overflow-auto"><table className="w-full text-sm"><thead><tr className="border-b text-left"><th className="p-2">Account</th><th className="p-2">Description</th><th className="p-2 text-right text-green-700">Debit</th><th className="p-2 text-right text-red-700">Credit</th></tr></thead><tbody>{distribution.lines.map((line, index) => <tr className="border-b" key={`${line.accountId}-${index}`}><td className="p-2">{line.accountCode} · {line.accountName}</td><td className="p-2">{line.description}</td><td className="p-2 text-right text-green-700">{line.debit.toFixed(2)}</td><td className="p-2 text-right text-red-700">{line.credit.toFixed(2)}</td></tr>)}</tbody><tfoot><tr><th className="p-2 text-left" colSpan={2}>{distribution.isBalanced ? 'Balanced' : 'Unbalanced'}</th><th className="p-2 text-right text-green-700">{distribution.totalDebit.toFixed(2)}</th><th className="p-2 text-right text-red-700">{distribution.totalCredit.toFixed(2)}</th></tr></tfoot></table></div></>}
      {error && <p role="alert" className="text-sm text-destructive">{error}</p>}<DialogFooter><Button variant="outline" onClick={() => setDistributionOpen(false)}>Close</Button>{detail?.canPost && <Button disabled={busy || !distribution?.isBalanced} onClick={() => void action('post')}>{busy ? 'Posting…' : 'Post invoice'}</Button>}</DialogFooter>
    </DialogContent></Dialog>
  </Card>;
}
