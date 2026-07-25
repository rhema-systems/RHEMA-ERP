'use client';

import React, { useEffect, useMemo, useState } from 'react';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { DOCUMENT_TYPES, documentOutputService } from '@/services/document-output.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { FinanceSettings, TrialBalanceLineDto, TrialBalanceReportDto } from '@/types/finance';
import { Download, Loader2, Printer, Search } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { DEFAULT_ACCOUNTING_BOOKS } from '@/lib/finance/accounting-books';
import { ReportSegmentFilters } from '@/components/finance/reports/ReportSegmentFilters';
import { AppliedReportSegmentFilters } from '@/components/finance/reports/AppliedReportSegmentFilters';
import {
    buildFinanceSegmentFilters,
    toFinanceSegmentFilterQueryParameters,
    type ReportSegmentSelections,
} from '@/lib/finance/report-segment-filters';
import type { FinanceSegmentFilterDto, SegmentStructure } from '@/types/finance';

export default function TrialBalancePage() {
    const router = useRouter();
    const [asAtDate, setAsAtDate] = useState(new Date().toISOString().split('T')[0]);
    const [bookClassification, setBookClassification] = useState('IFRS');
    const [accountingBooks, setAccountingBooks] = useState(DEFAULT_ACCOUNTING_BOOKS);
    const [hideZeroBalances, setHideZeroBalances] = useState(true);
    const [searchTerm, setSearchTerm] = useState('');
    const [loading, setLoading] = useState(true);
    const [running, setRunning] = useState(false);
    const [actionLoading, setActionLoading] = useState<'print' | 'export' | null>(null);
    const [settings, setSettings] = useState<FinanceSettings | null>(null);
    const [reportingDimensions, setReportingDimensions] = useState<SegmentStructure[]>([]);
    const [segmentSelections, setSegmentSelections] = useState<ReportSegmentSelections>({});
    const [appliedSegmentFilters, setAppliedSegmentFilters] = useState<FinanceSegmentFilterDto[]>([]);
    const [segmentLoadError, setSegmentLoadError] = useState<string | null>(null);
    const [report, setReport] = useState<TrialBalanceReportDto | null>(null);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        loadInitialReport();
    }, []);

    const loadInitialReport = async () => {
        try {
            setLoading(true);
            const [settingsData, books, dimensions] = await Promise.all([
                financeDataService.getFinanceSettings(),
                financeDataService.getAccountingBooks().catch(() => DEFAULT_ACCOUNTING_BOOKS),
                financeDataService.getReportingDimensions().catch((err) => {
                    console.error('Error loading reporting dimensions:', err);
                    setSegmentLoadError('GL segment filters could not be loaded.');
                    return [] as SegmentStructure[];
                }),
            ]);
            setSettings(settingsData);
            if (books.length > 0) setAccountingBooks(books);
            setReportingDimensions(dimensions);
            const data = await financeDataService.getTrialBalance({
                asAtDate,
                bookClassification,
                includeZeroBalances: !hideZeroBalances,
                segmentFilters: [],
            });
            setReport(data);
        } catch (err) {
            console.error('Error loading trial balance report:', err);
            setError('Could not generate the trial balance report.');
        } finally {
            setLoading(false);
        }
    };

    const runReport = async () => {
        try {
            setRunning(true);
            setError(null);
            const segmentFilters = buildFinanceSegmentFilters(reportingDimensions, segmentSelections);
            const data = await financeDataService.getTrialBalance({
                asAtDate,
                bookClassification,
                includeZeroBalances: !hideZeroBalances,
                segmentFilters,
            });
            setReport(data);
            setAppliedSegmentFilters(segmentFilters);
        } catch (err) {
            console.error('Error loading trial balance report:', err);
            setError('Could not generate the trial balance report.');
        } finally {
            setRunning(false);
        }
    };

    const filteredLines = useMemo(() => {
        const search = searchTerm.trim().toLowerCase();
        const lines = report?.lines ?? [];
        if (!search) return lines;

        return lines.filter(line =>
            line.accountCode.toLowerCase().includes(search) ||
            line.accountNumber.toLowerCase().includes(search) ||
            line.accountName.toLowerCase().includes(search)
        );
    }, [report, searchTerm]);

    const formatMoney = (amount: number) => new Intl.NumberFormat('en-GH', {
        style: 'decimal',
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    }).format(amount || 0);

    const reportParameters = () => {
        return {
            asAtDate,
            bookClassification,
            includeZeroBalances: !hideZeroBalances,
            ...toFinanceSegmentFilterQueryParameters(appliedSegmentFilters),
        };
    };

    const updateSegmentSelection = (segmentStructureId: string, value: string) => {
        setSegmentSelections((current) => ({ ...current, [segmentStructureId]: value }));
    };

    const openDetailedLedger = (line: TrialBalanceLineDto) => {
        if (!line.accountId) return;

        const asAt = new Date(asAtDate);
        const startDate = new Date(asAt.getFullYear(), 0, 1).toISOString().split('T')[0];
        const params = new URLSearchParams({
            startDate,
            endDate: asAtDate,
            bookClassification,
            includeReversed: 'true',
            includeOpeningBalances: 'true',
        });
        params.append('accountIds', line.accountId);

        router.push(`/finance/reports/detailed-ledger?${params.toString()}`);
    };

    const printReport = async () => {
        try {
            setActionLoading('print');
            setError(null);
            await documentOutputService.printReportDocument(DOCUMENT_TYPES.financeTrialBalance, reportParameters());
        } catch (err) {
            console.error('Error printing trial balance report:', err);
            setError('Could not print the trial balance report.');
        } finally {
            setActionLoading(null);
        }
    };

    const exportReport = async () => {
        try {
            setActionLoading('export');
            setError(null);
            await documentOutputService.downloadReportDocument(DOCUMENT_TYPES.financeTrialBalance, reportParameters());
        } catch (err) {
            console.error('Error exporting trial balance report:', err);
            setError('Could not export the trial balance report.');
        } finally {
            setActionLoading(null);
        }
    };

    if (loading) {
        return (
            <div className="flex h-96 items-center justify-center">
                <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
            </div>
        );
    }

    return (
        <div className="space-y-6">
            <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Trial Balance</h1>
                    <p className="text-muted-foreground">
                        Statement of all ledger account balances
                        {settings?.coaType === 'Segmented' && (
                            <span className="ml-2 rounded-full bg-purple-100 px-2 py-0.5 text-xs text-purple-700">
                                Segmented COA
                            </span>
                        )}
                    </p>
                </div>
                <div className="flex gap-2">
                    <Button variant="outline" onClick={printReport} disabled={!report || actionLoading !== null}>
                        {actionLoading === 'print' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Printer className="mr-2 h-4 w-4" />}
                        Print
                    </Button>
                    <Button variant="outline" onClick={exportReport} disabled={!report || actionLoading !== null}>
                        {actionLoading === 'export' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Download className="mr-2 h-4 w-4" />}
                        Export
                    </Button>
                </div>
            </div>

            <Breadcrumb>
                <BreadcrumbList>
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/finance">Finance</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/finance/reports">Reports</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>Trial Balance</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            <Card>
                <CardContent className="p-6">
                    <div className="grid grid-cols-1 items-end gap-4 md:grid-cols-5">
                        <div className="space-y-2">
                            <Label>As At Date</Label>
                            <Input type="date" value={asAtDate} onChange={(event) => setAsAtDate(event.target.value)} />
                        </div>
                        <div className="space-y-2">
                            <Label>Book</Label>
                            <Select value={bookClassification} onValueChange={setBookClassification}>
                                <SelectTrigger>
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    {accountingBooks.map((book) => (
                                        <SelectItem key={book.code} value={book.code}>{book.name}</SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2">
                            <Label>Find in Results</Label>
                            <div className="relative">
                                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                                <Input
                                    className="pl-8"
                                    placeholder="Code or name..."
                                    value={searchTerm}
                                    onChange={(event) => setSearchTerm(event.target.value)}
                                />
                            </div>
                        </div>
                        <ReportSegmentFilters
                            dimensions={reportingDimensions}
                            selections={segmentSelections}
                            onSelectionChange={updateSegmentSelection}
                            disabled={running}
                        />
                        <div className="flex items-center gap-2 pb-2">
                            <Switch id="hide-zero" checked={hideZeroBalances} onCheckedChange={setHideZeroBalances} />
                            <Label htmlFor="hide-zero">Hide Zero Balances</Label>
                        </div>
                        <Button onClick={runReport} disabled={running}>
                            {running && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Run Report
                        </Button>
                    </div>
                    {segmentLoadError && <div className="mt-4 text-sm text-amber-700">{segmentLoadError}</div>}
                    {error && <div className="mt-4 text-sm text-red-600">{error}</div>}
                    <AppliedReportSegmentFilters
                        dimensions={reportingDimensions}
                        appliedFilters={appliedSegmentFilters}
                        pendingFilters={buildFinanceSegmentFilters(reportingDimensions, segmentSelections)}
                    />
                </CardContent>
            </Card>

            <Card>
                <CardHeader className="border-b pb-2">
                    <div className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
                        <CardTitle className="text-lg">
                            Trial Balance as at {report ? new Date(report.asAtDate).toLocaleDateString() : new Date(asAtDate).toLocaleDateString()}
                        </CardTitle>
                        <span className="text-sm text-muted-foreground">
                            Currency: {report?.currencyCode || settings?.baseCurrency || 'GHS'} | {filteredLines.length} accounts
                        </span>
                    </div>
                </CardHeader>
                <CardContent className="pt-6">
                    <div className="rounded-md border">
                        <Table>
                            <TableHeader>
                                <TableRow className="bg-muted/50">
                                    <TableHead className={settings?.coaType === 'Segmented' ? 'w-[180px]' : 'w-[120px]'}>Account</TableHead>
                                    <TableHead>Account Name</TableHead>
                                    <TableHead>Type</TableHead>
                                    <TableHead className="text-right">Debit Balance</TableHead>
                                    <TableHead className="text-right">Credit Balance</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {filteredLines.length > 0 ? (
                                    filteredLines.map(line => (
                                        <TableRow
                                            key={line.accountId}
                                            className="cursor-pointer hover:bg-muted/50"
                                            title="Open detailed ledger for this account"
                                            onClick={() => openDetailedLedger(line)}
                                        >
                                            <TableCell className="font-mono text-sm">{line.accountNumber || line.accountCode}</TableCell>
                                            <TableCell>{line.accountName}</TableCell>
                                            <TableCell>{line.accountType}</TableCell>
                                            <TableCell className="text-right">{line.debitBalance ? formatMoney(line.debitBalance) : '-'}</TableCell>
                                            <TableCell className="text-right">{line.creditBalance ? formatMoney(line.creditBalance) : '-'}</TableCell>
                                        </TableRow>
                                    ))
                                ) : (
                                    <TableRow>
                                        <TableCell colSpan={5} className="py-8 text-center text-muted-foreground">
                                            No trial balance lines found
                                        </TableCell>
                                    </TableRow>
                                )}
                            </TableBody>
                            <tfoot className="bg-muted/50 font-bold">
                                <TableRow>
                                    <TableCell colSpan={3} className="text-right">TOTALS:</TableCell>
                                    <TableCell className="text-right">{formatMoney(report?.totalDebits ?? 0)}</TableCell>
                                    <TableCell className="text-right">{formatMoney(report?.totalCredits ?? 0)}</TableCell>
                                </TableRow>
                            </tfoot>
                        </Table>
                    </div>
                    {report && !report.isBalanced && (
                        <div className="mt-3 text-sm text-red-600">
                            Difference: {formatMoney(report.difference)}
                        </div>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}
