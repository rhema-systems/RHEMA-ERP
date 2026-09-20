'use client';

import React, { useEffect, useState } from 'react';
import { Columns3, Loader2, Maximize2, Minimize2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { accountsPayableService } from '@/services/accountsPayableService';
import type { VendorInvoiceDistribution } from '@/types/ap';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';

const amount = (value: number) =>
  value.toLocaleString(undefined, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });

export function InvoiceDistribution({ invoiceId }: { invoiceId: string }) {
  const [open, setOpen] = useState(false);
  const [fullPage, setFullPage] = useState(false);
  const [data, setData] = useState<VendorInvoiceDistribution | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [retry, setRetry] = useState(0);
  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    setLoading(true);
    setError('');
    setData(null);
    accountsPayableService
      .getInvoiceDistribution(invoiceId)
      .then((result) => {
        if (!cancelled) setData(result);
      })
      .catch((cause) => {
        if (!cancelled)
          setError(
            getProcurementProblemMessage(
              cause,
              'Unable to load the invoice distribution.'
            )
          );
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [open, invoiceId, retry]);
  const difference = data
    ? Math.round((data.totalDebit - data.totalCredit) * 100)
    : 0;
  return (
    <>
      <Button
        variant="outline"
        size="sm"
        onClick={() => {
          setFullPage(false);
          setOpen(true);
        }}
      >
        <Columns3 className="mr-1 h-4 w-4" />
        Distribution
      </Button>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent
          className={`flex flex-col overflow-hidden ${fullPage ? 'h-[calc(100dvh-2rem)] w-[calc(100vw-2rem)] max-w-none' : 'h-[min(620px,90dvh)] w-[min(960px,calc(100vw-2rem))] max-w-none'}`}
        >
          <DialogHeader className="shrink-0 pr-8">
            <div className="flex items-center gap-3">
              <DialogTitle>Invoice Distribution</DialogTitle>
              {data && <Badge variant="outline">{data.status}</Badge>}
              <Button
                variant="ghost"
                size="sm"
                className="ml-auto"
                onClick={() => setFullPage((value) => !value)}
              >
                {fullPage ? (
                  <Minimize2 className="mr-1 h-4 w-4" />
                ) : (
                  <Maximize2 className="mr-1 h-4 w-4" />
                )}
                {fullPage ? 'Restore' : 'Full page'}
              </Button>
            </div>
            <DialogDescription>
              {data
                ? `${data.currency} · ${data.journalEntryNumber || data.basis}`
                : 'Invoice GL accounts and amounts.'}
            </DialogDescription>
          </DialogHeader>
          <div className="min-h-0 flex-1 overflow-auto">
            {loading && (
              <div role="status" className="flex items-center gap-2 p-4">
                <Loader2 className="h-4 w-4 animate-spin" />
                Loading distribution…
              </div>
            )}
            {error && (
              <div
                role="alert"
                className="mb-2 rounded border border-red-200 bg-red-50 p-3 text-sm text-red-800"
              >
                {error}
                <Button
                  variant="outline"
                  size="sm"
                  className="ml-3"
                  onClick={() => setRetry((value) => value + 1)}
                >
                  Retry
                </Button>
              </div>
            )}
            {data && (
              <table className="w-full text-xs">
                <thead className="sticky top-0 bg-muted">
                  <tr>
                    {['Account', 'Description', 'Type', 'Debit', 'Credit'].map(
                      (label) => (
                        <th
                          key={label}
                          className={`px-2 py-2 font-medium ${label === 'Debit' || label === 'Credit' ? 'text-right' : 'text-left'}`}
                        >
                          {label}
                        </th>
                      )
                    )}
                  </tr>
                </thead>
                <tbody>
                  {data.lines.map((line, index) => (
                    <tr key={`${line.lineId}-${index}`} className="border-b">
                      <td className="whitespace-nowrap px-2 py-2">
                        {line.accountCode}
                      </td>
                      <td className="px-2 py-2" title={line.description}>
                        {line.accountName}
                      </td>
                      <td className="px-2 py-2" title={line.source}>
                        {line.type}
                      </td>
                      <td className="px-2 py-2 text-right tabular-nums">
                        {amount(line.debit)}
                      </td>
                      <td className="px-2 py-2 text-right tabular-nums">
                        {amount(line.credit)}
                      </td>
                    </tr>
                  ))}
                  {!data.lines.length && (
                    <tr>
                      <td
                        colSpan={5}
                        className="p-4 text-center text-muted-foreground"
                      >
                        No invoice distribution lines.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            )}
          </div>
          <DialogFooter className="shrink-0 flex-wrap items-center gap-2 border-t pt-3">
            {data && (
              <div className="mr-auto flex flex-wrap items-center gap-3 text-xs tabular-nums">
                <span>Debit {amount(data.totalDebit)}</span>
                <span>
                  Credit {amount(data.totalCredit)} {data.currency}
                </span>
                <span
                  className={difference ? 'text-red-700' : 'text-green-700'}
                >
                  {difference
                    ? `Difference ${amount(Math.abs(difference) / 100)}`
                    : 'Balanced'}
                </span>
              </div>
            )}
            <Button variant="outline" onClick={() => setOpen(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
