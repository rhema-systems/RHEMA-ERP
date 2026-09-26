'use client';
import React from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { procurementAutoInvoiceService } from '@/services/procurementAutoInvoiceService';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
export function InvoiceReceiptLinks({ invoiceId }: { invoiceId: string }) {
  const query = useQuery({ queryKey: ['invoice-receipt-links', invoiceId], queryFn: () => procurementAutoInvoiceService.links(invoiceId) });
  return <Card><CardHeader><CardTitle>Source receipts</CardTitle></CardHeader><CardContent>
    {query.isLoading && <p>Loading source receipts…</p>}
    {query.isError && <div role="alert">Could not load source receipts. <Button variant="outline" onClick={() => void query.refetch()}>Retry</Button></div>}
    {query.data && <table className="w-full text-sm"><thead><tr className="text-left"><th>GRN</th><th>Purchase order</th><th>Invoice quantity (PO units)</th></tr></thead><tbody>
      {query.data.map(line => <tr key={line.invoiceLineId}><td>{line.receiptNumber}</td><td><Link className="underline" href={`/procurement/purchase-orders/${line.purchaseOrderId}`}>{line.orderNumber}</Link></td><td>{line.quantity}</td></tr>)}
    </tbody></table>}
  </CardContent></Card>;
}
