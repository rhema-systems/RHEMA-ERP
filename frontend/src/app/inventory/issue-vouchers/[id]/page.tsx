'use client';

import React, { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import { Loader2 } from 'lucide-react';
import { inventoryRequisitionService, type InventoryIssueVoucherDto } from '@/services/inventoryRequisitionService';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';

export default function StoreIssueVoucherPage() {
  const { id } = useParams<{ id: string }>();
  const [voucher, setVoucher] = useState<InventoryIssueVoucherDto | null>(null);
  const [error, setError] = useState('');
  useEffect(() => {
    let active = true;
    setVoucher(null); setError('');
    inventoryRequisitionService.getIssueVoucher(id).then(value => { if (active) setVoucher(value); })
      .catch(failure => { if (active) setError(getProcurementProblemMessage(failure, 'Unable to load this store issue voucher.')); });
    return () => { active = false; };
  }, [id]);

  if (error) return <div role="alert" className="rounded-lg border border-destructive/30 p-4 text-destructive">{error}</div>;
  if (!voucher) return <div role="status" className="flex items-center gap-2 p-6"><Loader2 className="h-4 w-4 animate-spin" />Loading voucher…</div>;

  return <div className="space-y-5">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div><h1 className="text-2xl font-semibold">{voucher.voucherNumber}</h1><p className="text-sm text-muted-foreground">Store issue voucher</p></div>
      <Button variant="outline" asChild><Link href="/inventory/requisitions">Requisitions</Link></Button>
    </div>
    <Card><CardHeader><CardTitle className="flex items-center gap-3">Issue details<Badge variant="outline">{Number(voucher.status) === 2 || String(voucher.status).toLowerCase() === 'acknowledged' ? 'Acknowledged' : 'Issued'}</Badge></CardTitle></CardHeader>
      <CardContent><dl className="grid grid-cols-2 gap-4 md:grid-cols-4">
        {[
          ['Requisition', voucher.requisitionNumber], ['Warehouse', voucher.warehouseName],
          ['Location', voucher.locationCode], ['Receiver', voucher.receiverName],
          ['Issued by', voucher.issuedByName], ['Issued on', new Date(voucher.issuedAtUtc).toLocaleDateString()],
          ['Project', voucher.projectCode], ['Department', voucher.departmentName],
        ].map(([label, value]) => <div key={label}><dt className="text-xs text-muted-foreground">{label}</dt><dd className="mt-1 text-sm">{value || '—'}</dd></div>)}
      </dl></CardContent></Card>
    <Card><CardHeader><CardTitle>Items</CardTitle></CardHeader><CardContent>
      {voucher.isLegacyAcknowledgement && <p className="mb-3 text-sm text-muted-foreground">Historical acknowledgement: actual quantities were not captured per line.</p>}
      <Table><TableHeader><TableRow><TableHead>Item</TableHead><TableHead>Unit</TableHead><TableHead className="text-right">Issued</TableHead><TableHead className="text-right">Received</TableHead><TableHead className="text-right">Outstanding</TableHead></TableRow></TableHeader>
        <TableBody>{voucher.lines.map(line => <TableRow key={line.id}><TableCell>{line.itemCode} · {line.itemName}</TableCell><TableCell>{line.unitOfMeasure || '—'}</TableCell><TableCell className="text-right">{line.quantity}</TableCell><TableCell className="text-right">{voucher.isLegacyAcknowledgement ? 'Not recorded' : line.receivedQuantity}</TableCell><TableCell className="text-right">{voucher.isLegacyAcknowledgement ? '—' : line.outstandingQuantity}</TableCell></TableRow>)}</TableBody>
      </Table>
    </CardContent></Card>
    {voucher.notes && <p className="whitespace-pre-wrap text-sm">{voucher.notes}</p>}
  </div>;
}
