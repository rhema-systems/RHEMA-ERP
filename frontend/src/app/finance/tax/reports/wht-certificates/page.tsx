'use client';

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useToast } from '@/hooks/use-toast';
import { formatCurrencyAmount } from '@/lib/currency';
import { taxDataService } from '@/services/finance/tax-data.service';
import { DOCUMENT_TYPES, documentOutputService } from '@/services/document-output.service';
import { ReportPdfActions } from '@/components/finance/reports/ReportPdfActions';
import { ControlledDocumentIssueActions } from '@/components/finance/ControlledDocumentIssueActions';
import { useAuth } from '@/hooks/use-auth';
import type { FinancePagedResult, WhtCertificate } from '@/types/tax';
import { ArrowLeft, Ban, Download, Eye, FileText, Filter, RotateCcw, Search } from 'lucide-react';

const pageSize = 20;

function formatDateInput(date: Date) {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
}

function monthStartInput() {
    const now = new Date();
    return formatDateInput(new Date(now.getFullYear(), now.getMonth(), 1));
}

function todayInput() {
    return formatDateInput(new Date());
}

function formatDate(value?: string | null) {
    if (!value) return '-';
    return new Date(value).toLocaleDateString('en-GH', {
        year: 'numeric',
        month: 'short',
        day: '2-digit',
    });
}

function statusClass(status: string) {
    switch (status) {
        case 'Issued':
            return 'bg-green-50 text-green-700 border-green-200';
        case 'NumberOnly':
            return 'bg-amber-50 text-amber-700 border-amber-200';
        case 'Cancelled':
            return 'bg-red-50 text-red-700 border-red-200';
        case 'Superseded':
            return 'bg-blue-50 text-blue-700 border-blue-200';
        default:
            return 'bg-slate-50 text-slate-700 border-slate-200';
    }
}

export default function WhtCertificatesPage() {
    const { hasPermission } = useAuth();
    const canExport = hasPermission('Finance.Reports.Export');
    const [result, setResult] = useState<FinancePagedResult<WhtCertificate> | null>(null);
    const [loading, setLoading] = useState(true);
    const [searchTerm, setSearchTerm] = useState('');
    const [fromDate, setFromDate] = useState(monthStartInput);
    const [toDate, setToDate] = useState(todayInput);
    const [status, setStatus] = useState('All');
    const [page, setPage] = useState(1);
    const [generatingId, setGeneratingId] = useState<string | null>(null);
    const [lifecycleId, setLifecycleId] = useState<string | null>(null);
    const [exporting, setExporting] = useState(false);
    const { toast } = useToast();

    const rows = result?.items ?? [];

    const totals = useMemo(() => ({
        taxableBase: rows.reduce((sum, row) => sum + row.taxableBase, 0),
        withholdingAmount: rows.reduce((sum, row) => sum + row.withholdingAmount, 0),
        generated: rows.filter(row => row.certificateStatus === 'Issued').length,
        missing: rows.filter(row => row.certificateStatus === 'Missing').length,
    }), [rows]);

    const loadCertificates = async (pageToLoad = page) => {
        setLoading(true);
        try {
            const data = await taxDataService.getWhtCertificates({
                page: pageToLoad,
                pageSize,
                searchTerm,
                fromDate,
                toDate,
                status,
            });
            setResult(data);
            setPage(pageToLoad);
        } catch (error: any) {
            toast({
                title: 'Failed to load certificates',
                description: error?.message || 'Unable to load WHT certificates.',
                variant: 'destructive',
            });
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        loadCertificates(1);
    }, []);

    const replaceCertificate = (certificate: WhtCertificate) => {
        setResult(previous => previous
            ? {
                ...previous,
                items: previous.items.map(item =>
                    item.vendorPaymentId === certificate.vendorPaymentId ? certificate : item),
            }
            : previous);
    };

    const generateCertificate = async (certificate: WhtCertificate) => {
        setGeneratingId(certificate.vendorPaymentId);
        try {
            const generated = await taxDataService.generateWhtCertificate(certificate.vendorPaymentId);
            replaceCertificate(generated);
            toast({
                title: 'Certificate generated',
                description: generated.certificateNumber || 'WHT certificate generated.',
                variant: 'success',
            });
            return generated;
        } catch (error: any) {
            toast({
                title: 'Generation failed',
                description: error?.message || 'Unable to generate WHT certificate.',
                variant: 'destructive',
            });
            return null;
        } finally {
            setGeneratingId(null);
        }
    };

    const reissueCertificate = async (certificate: WhtCertificate) => {
        const reason = window.prompt('Reason for certificate reissue (minimum 10 characters):');
        if (!reason) return;
        setLifecycleId(certificate.vendorPaymentId);
        try {
            const updated = await taxDataService.reissueWhtCertificate(certificate.vendorPaymentId, reason);
            replaceCertificate(updated);
            toast({ title: 'Certificate reissued', description: updated.certificateNumber || 'Replacement issued.', variant: 'success' });
        } catch (error: any) {
            toast({ title: 'Reissue failed', description: error?.message || 'Unable to reissue certificate.', variant: 'destructive' });
        } finally {
            setLifecycleId(null);
        }
    };

    const cancelCertificate = async (certificate: WhtCertificate) => {
        const reason = window.prompt('Reason for certificate cancellation (minimum 10 characters):');
        if (!reason) return;
        setLifecycleId(certificate.vendorPaymentId);
        try {
            const updated = await taxDataService.cancelWhtCertificate(certificate.vendorPaymentId, reason);
            replaceCertificate(updated);
            toast({ title: 'Certificate cancelled', description: updated.certificateNumber || 'Certificate retained as cancelled evidence.' });
        } catch (error: any) {
            toast({ title: 'Cancellation failed', description: error?.message || 'Unable to cancel certificate.', variant: 'destructive' });
        } finally {
            setLifecycleId(null);
        }
    };

    const exportRegister = async () => {
        setExporting(true);
        try {
            const blob = await taxDataService.downloadWhtRegister(fromDate, toDate);
            const url = window.URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = `wht-statutory-register-${fromDate}-${toDate}.csv`;
            link.click();
            window.URL.revokeObjectURL(url);
        } catch (error: any) {
            toast({ title: 'Export failed', description: error?.message || 'Unable to export WHT register.', variant: 'destructive' });
        } finally {
            setExporting(false);
        }
    };

    const resetFilters = () => {
        setSearchTerm('');
        setFromDate(monthStartInput());
        setToDate(todayInput());
        setStatus('All');
        setPage(1);
    };

    return (
        <div className="p-6 space-y-6">
            <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
                <div>
                    <div className="flex items-center gap-3">
                        <Link href="/finance/tax/reports">
                            <Button variant="ghost" size="icon" aria-label="Back to tax reports">
                                <ArrowLeft className="h-4 w-4" />
                            </Button>
                        </Link>
                        <div>
                            <h1 className="text-3xl font-bold">WHT Certificates</h1>
                            <p className="text-muted-foreground">Generate and print supplier WHT certificates from posted AP payments</p>
                        </div>
                    </div>
                </div>
                <div className="flex flex-wrap gap-2">
                    <Link href="/finance/tax/reports/wht-remittances"><Button variant="outline">Remittances</Button></Link>
                    {canExport && (
                        <>
                            <Button variant="outline" onClick={exportRegister} disabled={exporting}>
                                <Download className="mr-2 h-4 w-4" /> Export Register
                            </Button>
                            <ReportPdfActions
                                reportName="WHT statutory certificate register"
                                onDownloadPdf={() => documentOutputService.downloadReportDocument(
                                    DOCUMENT_TYPES.financeTaxWhtCertificateRegister,
                                    { fromDate, toDate, status, searchTerm })}
                                onPrint={() => documentOutputService.printReportDocument(
                                    DOCUMENT_TYPES.financeTaxWhtCertificateRegister,
                                    { fromDate, toDate, status, searchTerm })}
                                disabled={loading || !fromDate || !toDate}
                            />
                        </>
                    )}
                    <Button onClick={() => loadCertificates(1)} disabled={loading}>
                        <Filter className="mr-2 h-4 w-4" /> Apply Filters
                    </Button>
                </div>
            </div>

            <Card>
                <CardHeader>
                    <CardTitle className="flex items-center gap-2">
                        <Filter className="h-5 w-5" />
                        Filters
                    </CardTitle>
                </CardHeader>
                <CardContent>
                    <div className="grid grid-cols-1 gap-4 md:grid-cols-5">
                        <div className="md:col-span-2">
                            <Label htmlFor="certificate-search">Search</Label>
                            <div className="relative mt-1">
                                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                                <Input
                                    id="certificate-search"
                                    value={searchTerm}
                                    onChange={(event) => setSearchTerm(event.target.value)}
                                    placeholder="Supplier, TIN, payment, certificate..."
                                    className="pl-8"
                                />
                            </div>
                        </div>
                        <div>
                            <Label htmlFor="from-date">From</Label>
                            <Input
                                id="from-date"
                                type="date"
                                value={fromDate}
                                onChange={(event) => setFromDate(event.target.value)}
                                className="mt-1"
                            />
                        </div>
                        <div>
                            <Label htmlFor="to-date">To</Label>
                            <Input
                                id="to-date"
                                type="date"
                                value={toDate}
                                onChange={(event) => setToDate(event.target.value)}
                                className="mt-1"
                            />
                        </div>
                        <div>
                            <Label>Status</Label>
                            <Select value={status} onValueChange={setStatus}>
                                <SelectTrigger className="mt-1">
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="All">All</SelectItem>
                                    <SelectItem value="Missing">Missing</SelectItem>
                                    <SelectItem value="Issued">Issued</SelectItem>
                                    <SelectItem value="Cancelled">Cancelled</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>
                    </div>
                    <div className="mt-4 flex justify-end">
                        <Button variant="outline" onClick={resetFilters}>
                            <RotateCcw className="mr-2 h-4 w-4" />
                            Reset
                        </Button>
                    </div>
                </CardContent>
            </Card>

            <div className="grid grid-cols-1 gap-4 md:grid-cols-4">
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">Eligible Payments</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{result?.totalCount ?? 0}</div>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">Issued On Page</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold text-green-700">{totals.generated}</div>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">Missing On Page</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold text-amber-700">{totals.missing}</div>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">WHT On Page</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold text-orange-700">
                            {formatCurrencyAmount(totals.withholdingAmount, rows[0]?.currencyCode, 2)}
                        </div>
                    </CardContent>
                </Card>
            </div>

            <Card>
                <CardHeader>
                    <CardTitle>Certificate Worklist</CardTitle>
                    <CardDescription>
                        Posted AP payments with withholding tax deductions
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    {loading ? (
                        <div className="py-8 text-center text-muted-foreground">Loading...</div>
                    ) : rows.length === 0 ? (
                        <div className="py-8 text-center text-muted-foreground">
                            No eligible posted AP WHT payments found.
                        </div>
                    ) : (
                        <div className="overflow-x-auto">
                            <table className="w-full">
                                <thead>
                                    <tr className="border-b">
                                        <th className="p-3 text-left font-semibold">Payment</th>
                                        <th className="p-3 text-left font-semibold">Supplier</th>
                                        <th className="p-3 text-left font-semibold">Tax</th>
                                        <th className="p-3 text-right font-semibold">Taxable Base</th>
                                        <th className="p-3 text-right font-semibold">WHT</th>
                                        <th className="p-3 text-left font-semibold">Certificate</th>
                                        <th className="p-3 text-right font-semibold">Actions</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {rows.map(row => {
                                        const busy = generatingId === row.vendorPaymentId || lifecycleId === row.vendorPaymentId;
                                        return (
                                            <tr key={row.vendorPaymentId} className="border-b hover:bg-accent">
                                                <td className="p-3">
                                                    <div className="font-medium">{row.paymentNumber}</div>
                                                    <div className="text-sm text-muted-foreground">{formatDate(row.paymentDate)}</div>
                                                </td>
                                                <td className="p-3">
                                                    <div className="font-medium">{row.supplierName}</div>
                                                    <div className="text-sm text-muted-foreground">{row.supplierTin || 'TIN not supplied'}</div>
                                                </td>
                                                <td className="p-3">
                                                    <div>{row.taxName || row.taxCode || 'WHT'}</div>
                                                    <div className="text-sm text-muted-foreground">{Number(row.taxRate || 0).toFixed(2)}%</div>
                                                </td>
                                                <td className="p-3 text-right">
                                                    {formatCurrencyAmount(row.taxableBase, row.currencyCode, 2)}
                                                </td>
                                                <td className="p-3 text-right font-semibold text-orange-700">
                                                    {formatCurrencyAmount(row.withholdingAmount, row.currencyCode, 2)}
                                                </td>
                                                <td className="p-3">
                                                    <Badge variant="outline" className={statusClass(row.certificateStatus)}>
                                                        {row.certificateStatus}
                                                    </Badge>
                                                    {row.certificateNumber && (
                                                        <div className="mt-1 text-sm">
                                                            {row.certificateNumber}
                                                            <span className="text-muted-foreground"> | {formatDate(row.certificateDate)}</span>
                                                        </div>
                                                    )}
                                                    <div className="mt-1 text-xs text-muted-foreground">
                                                        {row.remittanceNumber ? `${row.remittanceNumber} · ${row.remittanceStatus}` : 'Unremitted'}
                                                    </div>
                                                    {row.versions.length > 1 && (
                                                        <div className="mt-1 text-xs text-muted-foreground">{row.versions.length} controlled versions retained</div>
                                                    )}
                                                    {row.versions
                                                        .filter(version => version.certificateId !== row.certificateId)
                                                        .map(version => (
                                                            <div key={version.certificateId} className="mt-1">
                                                                <ControlledDocumentIssueActions
                                                                    documentType={DOCUMENT_TYPES.financeTaxWhtCertificate}
                                                                    entityId={version.certificateId}
                                                                    documentLabel={`WHT certificate v${version.versionNumber}`}
                                                                    issuePermission="Finance.Tax.Configuration.Manage"
                                                                    replacementPermission="Finance.Tax.Configuration.Manage"
                                                                    readOnly
                                                                />
                                                            </div>
                                                        ))}
                                                </td>
                                                <td className="p-3">
                                                    <div className="flex justify-end gap-2">
                                                        <Link href={`/finance/ap/payments/${row.vendorPaymentId}`}>
                                                            <Button variant="ghost" size="icon" aria-label="View payment">
                                                                <Eye className="h-4 w-4" />
                                                            </Button>
                                                        </Link>
                                                        {row.certificateStatus === 'Missing' && (
                                                            <Button
                                                                variant="outline"
                                                                size="sm"
                                                                onClick={() => generateCertificate(row)}
                                                                disabled={busy}
                                                            >
                                                                <FileText className="mr-2 h-4 w-4" />
                                                                Generate
                                                            </Button>
                                                        )}
                                                        {row.certificateStatus !== 'Missing' && (
                                                            <Button variant="outline" size="sm" onClick={() => reissueCertificate(row)} disabled={busy}>
                                                                <RotateCcw className="mr-2 h-4 w-4" /> Reissue
                                                            </Button>
                                                        )}
                                                        {row.certificateStatus === 'Issued' && (
                                                            <Button variant="ghost" size="icon" onClick={() => cancelCertificate(row)} disabled={busy} aria-label="Cancel certificate">
                                                                <Ban className="h-4 w-4 text-red-600" />
                                                            </Button>
                                                        )}
                                                        {row.certificateStatus === 'Issued' && row.certificateId && (
                                                            <ControlledDocumentIssueActions
                                                                documentType={DOCUMENT_TYPES.financeTaxWhtCertificate}
                                                                entityId={row.certificateId}
                                                                documentLabel="WHT certificate PDF"
                                                                issuePermission="Finance.Tax.Configuration.Manage"
                                                                replacementPermission="Finance.Tax.Configuration.Manage"
                                                                onIssued={() => loadCertificates(page)}
                                                            />
                                                        )}
                                                    </div>
                                                </td>
                                            </tr>
                                        );
                                    })}
                                </tbody>
                            </table>
                        </div>
                    )}

                    {result && result.totalPages > 1 && (
                        <div className="mt-4 flex items-center justify-between">
                            <div className="text-sm text-muted-foreground">
                                Page {result.page} of {result.totalPages}
                            </div>
                            <div className="flex gap-2">
                                <Button
                                    variant="outline"
                                    disabled={!result.hasPrevious || loading}
                                    onClick={() => loadCertificates(page - 1)}
                                >
                                    Previous
                                </Button>
                                <Button
                                    variant="outline"
                                    disabled={!result.hasNext || loading}
                                    onClick={() => loadCertificates(page + 1)}
                                >
                                    Next
                                </Button>
                            </div>
                        </div>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}
