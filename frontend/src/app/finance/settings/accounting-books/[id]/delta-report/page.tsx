'use client';

import React from 'react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { ArrowLeft, Download, Loader2, Printer, RefreshCw } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useAuth } from '@/hooks/use-auth';
import { financeDataService } from '@/services/finance/finance-data.service';
import { DOCUMENT_TYPES, documentOutputService } from '@/services/document-output.service';
import type { DeltaBookCombinedReport } from '@/types/finance';

const today = () => new Date().toISOString().slice(0, 10);
const amount = (value: number, currency: string) => new Intl.NumberFormat(undefined, {
  style: 'currency', currency, minimumFractionDigits: 2, maximumFractionDigits: 2,
}).format(value);
const csvCell = (value: string | number) => `"${String(value).replaceAll('"', '""')}"`;

export default function DeltaCombinedReportPage() {
  const { id } = useParams<{ id: string }>();
  const { hasPermission, isLoading: authLoading } = useAuth();
  const canRead = hasPermission('Finance.Read');
  const [asOfDate, setAsOfDate] = useState(today);
  const [report, setReport] = useState<DeltaBookCombinedReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [printing, setPrinting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!canRead || !asOfDate) return;
    setLoading(true); setError(null);
    try { setReport(await financeDataService.getDeltaBookCombinedReport(id, asOfDate)); }
    catch (reason) { setReport(null); setError(reason instanceof Error ? reason.message : 'The combined report could not be loaded.'); }
    finally { setLoading(false); }
  }, [asOfDate, canRead, id]);

  useEffect(() => { if (!authLoading && canRead) void load(); if (!authLoading && !canRead) setLoading(false); }, [authLoading, canRead, load]);
  const changedLines = useMemo(() => report?.lines.filter(line => line.deltaSignedBalance !== 0).length ?? 0, [report]);
  const exportCsv = () => {
    if (!report) return;
    const rows = [
      ['Account number', 'Account name', 'Account type', `Base (${report.baseAccountingBookCode})`, `Delta (${report.deltaAccountingBookCode})`, 'Combined'],
      ...report.lines.map(line => [line.accountNumber, line.accountName, line.accountType, line.baseSignedBalance, line.deltaSignedBalance, line.combinedSignedBalance]),
    ];
    const csv = rows.map(row => row.map(csvCell).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `${report.baseAccountingBookCode}-${report.deltaAccountingBookCode}-${report.asOfDate.slice(0, 10)}.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
  };
  const printReport = async () => {
    if (!report) return;
    setPrinting(true); setError(null);
    try {
      await documentOutputService.printReportDocument(DOCUMENT_TYPES.financeBaseDeltaReport, {
        deltaAccountingBookId: id,
        asOfDate,
      });
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'The printable report could not be generated.');
    } finally {
      setPrinting(false);
    }
  };

  if (authLoading) return <div className="flex min-h-[320px] items-center justify-center"><Loader2 className="h-6 w-6 animate-spin" /></div>;
  if (!canRead) return <Alert variant="destructive"><AlertTitle>Permission required</AlertTitle><AlertDescription>Finance.Read is required to view this report.</AlertDescription></Alert>;

  return <div className="space-y-6 p-6">
    <div>
      <Button asChild variant="ghost" className="px-0"><Link href="/finance/settings/accounting-books"><ArrowLeft className="mr-2 h-4 w-4" />Accounting books</Link></Button>
      <h1 className="text-2xl font-semibold">Base + Delta report</h1>
      <p className="text-muted-foreground">Presents the governed full-book balance, approved Delta postings, and their arithmetic combination without changing either ledger.</p>
    </div>
    <Card>
      <CardHeader><CardTitle>Reporting date</CardTitle><CardDescription>Only posted journal lines dated on or before this date are included.</CardDescription></CardHeader>
      <CardContent className="flex flex-wrap items-end gap-3"><div><Label htmlFor="as-of">As of date</Label><Input id="as-of" type="date" value={asOfDate} onChange={event => setAsOfDate(event.target.value)} /></div><Button disabled={loading || !asOfDate} onClick={() => void load()}><RefreshCw className="mr-2 h-4 w-4" />Refresh report</Button></CardContent>
    </Card>
    {error && <Alert variant="destructive"><AlertTitle>Combined report unavailable</AlertTitle><AlertDescription>{error}</AlertDescription></Alert>}
    {loading && <div className="flex min-h-[240px] items-center justify-center"><Loader2 className="h-6 w-6 animate-spin" /></div>}
    {!loading && report && <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-4"><div><CardTitle>{report.baseAccountingBookCode} + {report.deltaAccountingBookCode}</CardTitle><CardDescription>{changedLines} account(s) contain Delta adjustments as of {report.asOfDate.slice(0, 10)}. Positive values are net debits; negative values are net credits.</CardDescription></div><div className="flex gap-2"><Button variant="outline" disabled={printing} onClick={() => void printReport()}>{printing ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Printer className="mr-2 h-4 w-4" />}Print</Button><Button variant="outline" onClick={exportCsv}><Download className="mr-2 h-4 w-4" />Download CSV</Button></div></CardHeader>
      <CardContent className="space-y-4">
        <div className="grid gap-3 md:grid-cols-3">
          <div className="rounded-md border p-3"><p className="text-xs text-muted-foreground">Base net control</p><p className="font-medium tabular-nums">{amount(report.baseTotal, report.functionalCurrencyCode)}</p></div>
          <div className="rounded-md border p-3"><p className="text-xs text-muted-foreground">Delta net control</p><p className="font-medium tabular-nums">{amount(report.deltaTotal, report.functionalCurrencyCode)}</p></div>
          <div className="rounded-md border p-3"><p className="text-xs text-muted-foreground">Combined net control</p><p className="font-medium tabular-nums">{amount(report.combinedTotal, report.functionalCurrencyCode)}</p></div>
        </div>
        <p className="text-xs text-muted-foreground">These net controls should normally be zero for balanced posted journals; they are reconciliation checks, not assets, liabilities, income, or equity totals.</p>
        <div className="overflow-x-auto"><Table>
        <TableHeader><TableRow><TableHead>Account</TableHead><TableHead>Type</TableHead><TableHead className="text-right">Base ({report.baseAccountingBookCode})</TableHead><TableHead className="text-right">Delta ({report.deltaAccountingBookCode})</TableHead><TableHead className="text-right">Combined</TableHead></TableRow></TableHeader>
        <TableBody>{report.lines.length === 0 ? <TableRow><TableCell colSpan={5} className="py-8 text-center text-muted-foreground">No mapped accounts or posted balances were found.</TableCell></TableRow> : report.lines.map(line => <TableRow key={line.accountId} className={line.deltaSignedBalance !== 0 ? 'bg-blue-50/60' : undefined}><TableCell><p className="font-medium">{line.accountNumber}</p><p className="text-xs text-muted-foreground">{line.accountName}</p></TableCell><TableCell>{line.accountType}</TableCell><TableCell className="text-right tabular-nums">{amount(line.baseSignedBalance, report.functionalCurrencyCode)}</TableCell><TableCell className="text-right tabular-nums">{amount(line.deltaSignedBalance, report.functionalCurrencyCode)}</TableCell><TableCell className="text-right tabular-nums font-medium">{amount(line.combinedSignedBalance, report.functionalCurrencyCode)}</TableCell></TableRow>)}</TableBody>
      </Table></div></CardContent>
    </Card>}
  </div>;
}
