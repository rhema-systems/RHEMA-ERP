'use client';

import React, { useEffect, useState } from 'react';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { ProcurementControlAccordion } from './ProcurementControlAccordion';
import { LandedCostInvoiceLink } from './LandedCostInvoiceLink';
import { inventoryManagementService, type LandedCostDetailDto } from '@/services/inventoryManagementService';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';

const money = (n: number, c: string) => `${c} ${n.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
export function ActualLandedCostSummary({ purchaseOrderId, invoiceId, poTotal, currency }: {
  purchaseOrderId?: string | null; invoiceId?: string; poTotal?: number; currency?: string;
}) {
  const [costs, setCosts] = useState<LandedCostDetailDto[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    let active = true;
    setLoading(true); setCosts([]); setError('');
    const id = invoiceId || purchaseOrderId;
    if (!id) { setLoading(false); return; }
    inventoryManagementService.getLandedCostsBySource(invoiceId ? 'invoice' : 'po', id)
      .then(data => { if (active) setCosts(data.filter(c => c.status !== 'Cancelled')); })
      .catch(e => { if (active) setError(getProcurementProblemMessage(e, 'Receipt landed costs could not be loaded.')); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [purchaseOrderId, invoiceId, revision]);
  const currencies = [...new Set(costs.map(c => c.currency))];
  const posted = costs.filter(c => c.status === 'Posted');
  const summary = loading ? 'Loading receipt costs…' : error ? 'Receipt costs unavailable' : costs.length === 0 ? 'No receipt landed costs recorded' :
    currencies.map(c => `${money(costs.filter(v => v.currency === c).reduce((n, v) => n + v.totalCostAmount, 0), c)} recorded`).join(' · ');
  return <div className="no-print"><ProcurementControlAccordion title="Actual receipt landed costs" summary={summary}
    actions={<Button type="button" variant="ghost" size="sm" disabled={loading} onClick={() => setRevision(v => v + 1)}>Refresh costs</Button>}>
    {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : <>
      <p className="text-sm text-muted-foreground mb-3">Separate from PO estimates and the invoice amount due. A related receipt cost is not automatically billed on this invoice.</p>
      {currencies.map(c => <p key={c} className="text-sm mb-2">Posted receipt charges: <strong>{money(posted.filter(v => v.currency === c).reduce((n, v) => n + v.totalCostAmount, 0), c)}</strong>
        <span className="text-muted-foreground"> (voucher value; inventory/variance split follows valuation rules)</span></p>)}
      {!invoiceId && poTotal != null && currency && costs.length > 0 && currencies.every(c => c === currency) &&
        <p className="text-sm mb-3">PO value plus posted receipt charges: <strong>{money(poTotal + posted.reduce((n, v) => n + v.totalCostAmount, 0), currency)}</strong>. The approved PO amount remains {money(poTotal, currency)}.</p>}
      {costs.map(voucher => <div key={voucher.id} className="border rounded-md p-3 space-y-2 mt-3">
        <p className="text-sm font-medium">{voucher.landedCostNumber} · {voucher.status} · {money(voucher.totalCostAmount, voucher.currency)}
          {' · '}<Link className="text-primary underline" href={`/procurement/purchase-receipts/${voucher.receiptId}`}>Open receipt</Link></p>
        <div className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="text-left"><th className="p-2">Charge</th><th className="p-2">Supplier</th><th className="p-2">Amount</th><th className="p-2">Supplier invoice</th></tr></thead>
          <tbody>{voucher.costItems.map(item => <tr key={item.id} className="border-t"><td className="p-2">{item.description}</td><td className="p-2">{item.supplierName || 'Not specified'}</td><td className="p-2">{money(item.amount, item.currency)}</td>
            <td className="p-2"><LandedCostInvoiceLink voucherId={voucher.id} item={item} onChanged={() => setRevision(v => v + 1)} />
              {invoiceId && <span className="block text-xs text-muted-foreground">{item.invoiceId === invoiceId ? 'Linked to this invoice' : item.invoiceId ? 'Linked to another invoice' : 'Related receipt cost — not linked to this invoice'}</span>}
            </td></tr>)}</tbody></table></div>
      </div>)}
    </>}
  </ProcurementControlAccordion></div>;
}
