'use client';

import React from 'react';
import { useCallback, useEffect, useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { ArrowLeft, Loader2, RefreshCw } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useAuth } from '@/hooks/use-auth';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { DeltaBookLedgerInquiry } from '@/types/finance';

const money = (value: number, currency: string) => new Intl.NumberFormat(undefined, {
  style: 'currency', currency, minimumFractionDigits: 2, maximumFractionDigits: 2,
}).format(value);

export default function DeltaLedgerPage() {
  const { id } = useParams<{ id: string }>();
  const { hasPermission, isLoading: authLoading } = useAuth();
  const canRead = hasPermission('Finance.Read');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [ledger, setLedger] = useState<DeltaBookLedgerInquiry | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!canRead) return;
    setLoading(true);
    setError(null);
    try {
      setLedger(await financeDataService.getDeltaBookLedger(id, fromDate || undefined, toDate || undefined));
    } catch (reason) {
      setLedger(null);
      setError(reason instanceof Error ? reason.message : 'The Delta ledger could not be loaded.');
    } finally {
      setLoading(false);
    }
  }, [canRead, fromDate, id, toDate]);

  useEffect(() => {
    if (!authLoading && canRead) void load();
    if (!authLoading && !canRead) setLoading(false);
  }, [authLoading, canRead, load]);

  if (authLoading) return <div className="flex min-h-[320px] items-center justify-center"><Loader2 className="h-6 w-6 animate-spin" /></div>;
  if (!canRead) return <Alert variant="destructive"><AlertTitle>Permission required</AlertTitle><AlertDescription>Finance.Read is required to inspect this ledger.</AlertDescription></Alert>;

  return <div className="space-y-6 p-6">
    <div>
      <Button asChild variant="ghost" className="px-0"><Link href="/finance/settings/accounting-books"><ArrowLeft className="mr-2 h-4 w-4" />Accounting books</Link></Button>
      <h1 className="text-2xl font-semibold">Delta ledger</h1>
      <p className="text-muted-foreground">One audit view of posted base transactions inherited live by the Delta and the Delta&apos;s own adjustment journals.</p>
    </div>
    <Card>
      <CardHeader><CardTitle>Accounting-date range</CardTitle><CardDescription>Leave either boundary blank for an open-ended inquiry.</CardDescription></CardHeader>
      <CardContent className="flex flex-wrap items-end gap-3">
        <div><Label htmlFor="from-date">From</Label><Input id="from-date" type="date" value={fromDate} onChange={event => setFromDate(event.target.value)} /></div>
        <div><Label htmlFor="to-date">Through</Label><Input id="to-date" type="date" value={toDate} onChange={event => setToDate(event.target.value)} /></div>
        <Button disabled={loading} onClick={() => void load()}><RefreshCw className="mr-2 h-4 w-4" />Refresh ledger</Button>
      </CardContent>
    </Card>
    {error && <Alert variant="destructive"><AlertTitle>Delta ledger unavailable</AlertTitle><AlertDescription>{error}</AlertDescription></Alert>}
    {loading && <div className="flex min-h-[240px] items-center justify-center"><Loader2 className="h-6 w-6 animate-spin" /></div>}
    {!loading && ledger && <Card>
      <CardHeader><CardTitle>{ledger.baseAccountingBookCode} inherited by {ledger.deltaAccountingBookCode}</CardTitle><CardDescription>{ledger.entries.length} posted journal(s), shown in {ledger.functionalCurrencyCode}. Inherited rows remain stored only in the base ledger.</CardDescription></CardHeader>
      <CardContent><div className="overflow-x-auto"><Table>
        <TableHeader><TableRow><TableHead>Date</TableHead><TableHead>Journal</TableHead><TableHead>Layer</TableHead><TableHead>Description</TableHead><TableHead className="text-right">Debit</TableHead><TableHead className="text-right">Credit</TableHead></TableRow></TableHeader>
        <TableBody>{ledger.entries.length === 0 ? <TableRow><TableCell colSpan={6} className="py-8 text-center text-muted-foreground">No posted entries match this range.</TableCell></TableRow> : ledger.entries.map(entry => <TableRow key={entry.journalEntryId}>
          <TableCell>{entry.accountingDate.slice(0, 10)}</TableCell>
          <TableCell><Link className="font-medium hover:underline" href={`/finance/journal-entries/${entry.journalEntryId}`}>{entry.journalEntryNumber}</Link><p className="text-xs text-muted-foreground">{entry.sourceBookCode}</p></TableCell>
          <TableCell><Badge variant={entry.layer === 'Adjustment' ? 'default' : 'secondary'}>{entry.layer}</Badge></TableCell>
          <TableCell>{entry.description}<p className="text-xs text-muted-foreground">{entry.referenceNumber || 'No reference'}</p></TableCell>
          <TableCell className="text-right tabular-nums">{money(entry.totalDebit, ledger.functionalCurrencyCode)}</TableCell>
          <TableCell className="text-right tabular-nums">{money(entry.totalCredit, ledger.functionalCurrencyCode)}</TableCell>
        </TableRow>)}</TableBody>
      </Table></div></CardContent>
    </Card>}
  </div>;
}
