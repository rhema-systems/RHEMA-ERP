'use client';

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { ArrowLeft, FileCheck2, RefreshCw, Send, WalletCards, XCircle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useToast } from '@/hooks/use-toast';
import { formatCurrencyAmount } from '@/lib/currency';
import { taxDataService } from '@/services/finance/tax-data.service';
import { DOCUMENT_TYPES, documentOutputService } from '@/services/document-output.service';
import { ReportPdfActions } from '@/components/finance/reports/ReportPdfActions';
import { useAuth } from '@/hooks/use-auth';
import type { FinancePagedResult, WhtRemittance, WhtRemittanceLiability } from '@/types/tax';

function dateInput(date: Date) {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
}

function monthStart() {
    const today = new Date();
    return dateInput(new Date(today.getFullYear(), today.getMonth(), 1));
}

function today() {
    return dateInput(new Date());
}

function statusClass(status: string) {
    switch (status) {
        case 'Paid': return 'bg-emerald-50 text-emerald-700 border-emerald-200';
        case 'Submitted': return 'bg-blue-50 text-blue-700 border-blue-200';
        case 'Cancelled': return 'bg-red-50 text-red-700 border-red-200';
        default: return 'bg-amber-50 text-amber-700 border-amber-200';
    }
}

export default function WhtRemittancesPage() {
    const { hasPermission } = useAuth();
    const canExport = hasPermission('Finance.Reports.Export');
    const [fromDate, setFromDate] = useState(monthStart);
    const [toDate, setToDate] = useState(today);
    const [liabilities, setLiabilities] = useState<WhtRemittanceLiability[]>([]);
    const [remittances, setRemittances] = useState<FinancePagedResult<WhtRemittance> | null>(null);
    const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
    const [loading, setLoading] = useState(true);
    const [busyId, setBusyId] = useState<string | null>(null);
    const { toast } = useToast();

    const selectedLiabilities = useMemo(
        () => liabilities.filter(item => selectedIds.has(item.vendorPaymentId)),
        [liabilities, selectedIds],
    );
    const selectedTotal = selectedLiabilities.reduce((sum, item) => sum + item.withholdingAmount, 0);

    const load = async () => {
        setLoading(true);
        try {
            const [liabilityRows, batches] = await Promise.all([
                taxDataService.getUnremittedWhtLiabilities(fromDate, toDate, 'GHS'),
                taxDataService.getWhtRemittances({ page: 1, pageSize: 100, fromDate, toDate }),
            ]);
            setLiabilities(liabilityRows);
            setRemittances(batches);
            setSelectedIds(previous => new Set([...previous].filter(id => liabilityRows.some(item => item.vendorPaymentId === id))));
        } catch (error: any) {
            toast({ title: 'Load failed', description: error?.message || 'Unable to load WHT remittances.', variant: 'destructive' });
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => { void load(); }, []);

    const createRemittance = async () => {
        if (selectedIds.size === 0) {
            toast({ title: 'Select liabilities', description: 'Select at least one posted WHT liability.' });
            return;
        }
        setBusyId('create');
        try {
            const created = await taxDataService.createWhtRemittance({
                periodFrom: fromDate,
                periodTo: toDate,
                currencyCode: 'GHS',
                vendorPaymentIds: [...selectedIds],
            });
            toast({ title: 'Draft remittance created', description: created.remittanceNumber, variant: 'success' });
            setSelectedIds(new Set());
            await load();
        } catch (error: any) {
            toast({ title: 'Creation failed', description: error?.message || 'Unable to create remittance.', variant: 'destructive' });
        } finally {
            setBusyId(null);
        }
    };

    const submit = async (item: WhtRemittance) => {
        const reference = window.prompt('GRA submission/reference number:');
        if (!reference) return;
        setBusyId(item.id);
        try {
            await taxDataService.submitWhtRemittance(item.id, reference);
            await load();
        } catch (error: any) {
            toast({ title: 'Submission failed', description: error?.message || 'Unable to submit remittance.', variant: 'destructive' });
        } finally { setBusyId(null); }
    };

    const markPaid = async (item: WhtRemittance) => {
        const paymentReference = window.prompt('Finance payment/journal reference:');
        if (!paymentReference) return;
        const authorityReceiptReference = window.prompt('GRA receipt/acknowledgement reference (optional):') || undefined;
        setBusyId(item.id);
        try {
            await taxDataService.markWhtRemittancePaid(item.id, {
                paymentDate: today(),
                paymentReference,
                authorityReceiptReference,
            });
            await load();
        } catch (error: any) {
            toast({ title: 'Payment evidence failed', description: error?.message || 'Unable to mark remittance paid.', variant: 'destructive' });
        } finally { setBusyId(null); }
    };

    const cancel = async (item: WhtRemittance) => {
        const reason = window.prompt('Cancellation reason (minimum 10 characters):');
        if (!reason) return;
        setBusyId(item.id);
        try {
            await taxDataService.cancelWhtRemittance(item.id, reason);
            await load();
        } catch (error: any) {
            toast({ title: 'Cancellation failed', description: error?.message || 'Unable to cancel remittance.', variant: 'destructive' });
        } finally { setBusyId(null); }
    };

    return (
        <div className="space-y-6 p-6">
            <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
                <div className="flex items-center gap-3">
                    <Link href="/finance/tax/reports/wht-certificates"><Button variant="ghost" size="icon"><ArrowLeft className="h-4 w-4" /></Button></Link>
                    <div>
                        <h1 className="text-3xl font-bold">WHT Remittances</h1>
                        <p className="text-muted-foreground">Group posted AP WHT liabilities and retain submission/payment evidence without creating a second GL posting.</p>
                    </div>
                </div>
                <div className="flex flex-wrap gap-2">
                    {canExport && (
                        <ReportPdfActions
                            reportName="WHT remittance register"
                            onDownloadPdf={() => documentOutputService.downloadReportDocument(
                                DOCUMENT_TYPES.financeTaxWhtRemittanceRegister,
                                { fromDate, toDate })}
                            onPrint={() => documentOutputService.printReportDocument(
                                DOCUMENT_TYPES.financeTaxWhtRemittanceRegister,
                                { fromDate, toDate })}
                            disabled={loading || !fromDate || !toDate}
                        />
                    )}
                    <Button variant="outline" onClick={load} disabled={loading}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button>
                </div>
            </div>

            <Card>
                <CardHeader><CardTitle>Liability period</CardTitle></CardHeader>
                <CardContent className="grid gap-4 md:grid-cols-4">
                    <div><Label>From</Label><Input type="date" value={fromDate} onChange={event => setFromDate(event.target.value)} /></div>
                    <div><Label>To</Label><Input type="date" value={toDate} onChange={event => setToDate(event.target.value)} /></div>
                    <div><Label>Selected WHT</Label><div className="pt-2 text-xl font-semibold">{formatCurrencyAmount(selectedTotal, 'GHS', 2)}</div></div>
                    <div className="flex items-end"><Button className="w-full" onClick={createRemittance} disabled={busyId === 'create' || selectedIds.size === 0}><FileCheck2 className="mr-2 h-4 w-4" />Create Draft</Button></div>
                </CardContent>
            </Card>

            <Card>
                <CardHeader><CardTitle>Unremitted posted liabilities</CardTitle><CardDescription>Cancelled and reversed AP payments are excluded; a liability can belong to only one active batch.</CardDescription></CardHeader>
                <CardContent className="overflow-x-auto">
                    {liabilities.length === 0 ? <div className="py-8 text-center text-muted-foreground">No unremitted WHT liabilities in this period.</div> : (
                        <table className="w-full text-sm"><thead><tr className="border-b"><th className="p-3"></th><th className="p-3 text-left">Payment</th><th className="p-3 text-left">Supplier</th><th className="p-3 text-left">Tax</th><th className="p-3 text-right">Base</th><th className="p-3 text-right">WHT</th><th className="p-3 text-left">Certificate</th></tr></thead>
                        <tbody>{liabilities.map(item => <tr className="border-b" key={item.vendorPaymentId}>
                            <td className="p-3"><input type="checkbox" checked={selectedIds.has(item.vendorPaymentId)} onChange={event => setSelectedIds(previous => {
                                const next = new Set(previous);
                                if (event.target.checked) next.add(item.vendorPaymentId);
                                else next.delete(item.vendorPaymentId);
                                return next;
                            })} /></td>
                            <td className="p-3"><Link className="font-medium text-blue-700 hover:underline" href={`/finance/ap/payments/${item.vendorPaymentId}`}>{item.paymentNumber}</Link><div className="text-xs text-muted-foreground">{new Date(item.paymentDate).toLocaleDateString('en-GH')}</div></td>
                            <td className="p-3">{item.supplierName}<div className="text-xs text-muted-foreground">{item.supplierTin || 'TIN not supplied'}</div></td>
                            <td className="p-3">{item.taxCode || 'WHT'}</td><td className="p-3 text-right">{formatCurrencyAmount(item.taxableBase, item.currencyCode, 2)}</td><td className="p-3 text-right font-semibold">{formatCurrencyAmount(item.withholdingAmount, item.currencyCode, 2)}</td><td className="p-3">{item.certificateNumber || 'Not issued'}</td>
                        </tr>)}</tbody></table>
                    )}
                </CardContent>
            </Card>

            <Card>
                <CardHeader><CardTitle>Remittance register</CardTitle><CardDescription>Draft, submission, payment and cancellation history remains tenant-scoped and audited.</CardDescription></CardHeader>
                <CardContent className="space-y-3">
                    {(remittances?.items ?? []).map(item => <div key={item.id} className="rounded-md border p-4">
                        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                            <div><div className="flex items-center gap-2"><span className="font-semibold">{item.remittanceNumber}</span><Badge variant="outline" className={statusClass(item.status)}>{item.status}</Badge></div><div className="text-sm text-muted-foreground">{new Date(item.periodFrom).toLocaleDateString('en-GH')} – {new Date(item.periodTo).toLocaleDateString('en-GH')} · due {new Date(item.dueDate).toLocaleDateString('en-GH')} · {item.lineCount} items</div></div>
                            <div className="flex flex-wrap items-center gap-2"><span className="mr-2 font-semibold">{formatCurrencyAmount(item.totalWithholdingAmount, item.currencyCode, 2)}</span>
                                {item.status === 'Draft' && <Button size="sm" onClick={() => submit(item)} disabled={busyId === item.id}><Send className="mr-2 h-4 w-4" />Submit</Button>}
                                {item.status === 'Submitted' && <Button size="sm" onClick={() => markPaid(item)} disabled={busyId === item.id}><WalletCards className="mr-2 h-4 w-4" />Mark Paid</Button>}
                                {(item.status === 'Draft' || item.status === 'Submitted') && <Button variant="ghost" size="icon" onClick={() => cancel(item)} disabled={busyId === item.id}><XCircle className="h-4 w-4 text-red-600" /></Button>}
                            </div>
                        </div>
                        {(item.submissionReference || item.paymentReference) && <div className="mt-2 text-sm text-muted-foreground">Submission: {item.submissionReference || '-'} · Payment: {item.paymentReference || '-'} · GRA receipt: {item.authorityReceiptReference || '-'}</div>}
                        <details className="mt-3 text-sm"><summary className="cursor-pointer font-medium">Liability detail</summary><div className="mt-2 overflow-x-auto"><table className="w-full"><tbody>{item.lines.map(line => <tr className="border-t" key={line.id}><td className="p-2">{line.paymentNumber}</td><td className="p-2">{line.supplierName}</td><td className="p-2">{line.taxCode}</td><td className="p-2 text-right">{formatCurrencyAmount(line.withholdingAmount, item.currencyCode, 2)}</td></tr>)}</tbody></table></div></details>
                    </div>)}
                    {!loading && (remittances?.items.length ?? 0) === 0 && <div className="py-8 text-center text-muted-foreground">No remittance batches in this period.</div>}
                </CardContent>
            </Card>
        </div>
    );
}
