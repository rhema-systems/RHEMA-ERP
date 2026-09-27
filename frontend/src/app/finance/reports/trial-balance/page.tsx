'use client';

import React, { useEffect, useMemo, useState } from 'react';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { DOCUMENT_TYPES, documentOutputService } from '@/services/document-output.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { DeltaBookCombinedReport, FinanceSettings, TrialBalanceLineDto, TrialBalanceReportDto } from '@/types/finance';
import { Download, Loader2, Printer, Search } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { DEFAULT_ACCOUNTING_BOOKS } from '@/lib/finance/accounting-books';
import { ReportSegmentFilters } from '@/components/finance/reports/ReportSegmentFilters';
import { AppliedReportSegmentFilters } from '@/components/finance/reports/AppliedReportSegmentFilters';
import { ReportDimensionFilters } from '@/components/finance/reports/ReportDimensionFilters';
import { AppliedReportDimensionFilters } from '@/components/finance/reports/AppliedReportDimensionFilters';
import {
    buildFinanceSegmentFilters,
    toFinanceSegmentFilterQueryParameters,
    type ReportSegmentSelections,
} from '@/lib/finance/report-segment-filters';
import type { FinanceSegmentFilterDto, SegmentStructure } from '@/types/finance';
import {
    appendFinanceDimensionFilters,
    buildFinanceDimensionFilters,
    toFinanceDimensionFilterQueryParameters,
    type ReportDimensionSelections,
} from '@/lib/finance/report-dimension-filters';
import type { FinanceDimensionDefinition, FinanceDimensionFilterDto } from '@/types/finance';

export default function TrialBalancePage() {
    const router = useRouter();
    const [asAtDate, setAsAtDate] = useState(new Date().toISOString().split('T')[0]);
    const [bookClassification, setBookClassification] = useState('BASE');
    const [reportingView, setReportingView] = useState<'single' | 'base-delta'>('single');
    const [deltaAccountingBookIds, setDeltaAccountingBookIds] = useState<string[]>([]);
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
    const [transactionDimensions, setTransactionDimensions] = useState<FinanceDimensionDefinition[]>([]);
    const [dimensionSelections, setDimensionSelections] = useState<ReportDimensionSelections>({});
    const [appliedDimensionFilters, setAppliedDimensionFilters] = useState<FinanceDimensionFilterDto[]>([]);
    const [dimensionLoadError, setDimensionLoadError] = useState<string | null>(null);
    const [report, setReport] = useState<TrialBalanceReportDto | null>(null);
    const [deltaReport, setDeltaReport] = useState<DeltaBookCombinedReport | null>(null);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        loadInitialReport();
    }, []);

    const loadInitialReport = async () => {
        try {
            setLoading(true);
            const [settingsData, books, dimensions, transactionDimensionOptions] = await Promise.all([
                financeDataService.getFinanceSettings(),
                financeDataService.getAccountingBooks(true).catch(() => DEFAULT_ACCOUNTING_BOOKS),
                financeDataService.getReportingDimensions().catch((err) => {
                    console.error('Error loading reporting dimensions:', err);
                    setSegmentLoadError('GL segment filters could not be loaded.');
                    return [] as SegmentStructure[];
                }),
                financeDataService.getFinanceDimensions(true).catch(() => {
                    setDimensionLoadError('Transaction-dimension filters could not be loaded.');
                    return [];
                }),
            ]);
            setSettings(settingsData);
            if (books.length > 0) {
                setAccountingBooks(books);
                const firstDelta = books.find(book => book.bookType === 'Delta' && book.isActive !== false && book.allowsPosting !== false);
                if (firstDelta) setDeltaAccountingBookIds([firstDelta.id]);
            }
            setReportingDimensions(dimensions);
            setTransactionDimensions(transactionDimensionOptions);
            const data = await financeDataService.getTrialBalance({
                asAtDate,
                bookClassification,
                includeZeroBalances: !hideZeroBalances,
                segmentFilters: [],
                dimensionFilters: [],
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
            if (reportingView === 'base-delta') {
                if (deltaAccountingBookIds.length === 0) throw new Error('Select at least one Delta adjustment layer.');
                const data = await financeDataService.getMultiDeltaBookCombinedReport(deltaAccountingBookIds, asAtDate);
                setDeltaReport(data);
                return;
            }
            const segmentFilters = buildFinanceSegmentFilters(reportingDimensions, segmentSelections);
            const dimensionFilters = buildFinanceDimensionFilters(transactionDimensions, dimensionSelections);
            const data = await financeDataService.getTrialBalance({
                asAtDate,
                bookClassification,
                includeZeroBalances: !hideZeroBalances,
                segmentFilters,
                dimensionFilters,
            });
            setReport(data);
            setAppliedSegmentFilters(segmentFilters);
            setAppliedDimensionFilters(dimensionFilters);
        } catch (err) {
            console.error('Error loading trial balance report:', err);
            setError(err instanceof Error ? err.message : 'Could not generate the trial balance report.');
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

    const filteredDeltaLines = useMemo(() => {
        const search = searchTerm.trim().toLowerCase();
        const lines = deltaReport?.lines ?? [];
        if (!search) return lines;
        return lines.filter(line => line.accountNumber.toLowerCase().includes(search)
            || line.accountName.toLowerCase().includes(search)
            || line.accountType.toLowerCase().includes(search));
    }, [deltaReport, searchTerm]);

    const deltaBooks = useMemo(() => accountingBooks.filter(book => book.bookType === 'Delta'), [accountingBooks]);
    const selectedDeltaBooks = deltaBooks.filter(book => deltaAccountingBookIds.includes(book.id));
    const selectedDeltaBaseId = selectedDeltaBooks[0]?.baseAccountingBookId;
    const toggleDeltaBook = (id: string, checked: boolean) => setDeltaAccountingBookIds(current =>
        checked ? [...new Set([...current, id])] : current.filter(value => value !== id));

    const formatMoney = (amount: number) => new Intl.NumberFormat('en-GH', {
        style: 'decimal',
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    }).format(amount || 0);
    const formatSigned = (amount: number) => amount > 0
        ? `${formatMoney(amount)} Dr`
        : amount < 0 ? `${formatMoney(Math.abs(amount))} Cr` : formatMoney(0);

    const reportParameters = () => {
        if (reportingView === 'base-delta') {
            return { asOfDate: asAtDate, deltaAccountingBookIds: deltaAccountingBookIds.join(',') };
        }
        return {
            asAtDate,
            bookClassification,
            includeZeroBalances: !hideZeroBalances,
            ...toFinanceSegmentFilterQueryParameters(appliedSegmentFilters),
            ...toFinanceDimensionFilterQueryParameters(appliedDimensionFilters),
        };
    };

    const updateSegmentSelection = (segmentStructureId: string, value: string) => {
        setSegmentSelections((current) => ({ ...current, [segmentStructureId]: value }));
    };

    const updateDimensionSelection = (definitionId: string, valueCode: string) => {
        setDimensionSelections((current) => ({ ...current, [definitionId]: valueCode }));
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
        appendFinanceDimensionFilters(params, appliedDimensionFilters);

        router.push(`/finance/reports/detailed-ledger?${params.toString()}`);
    };

    const printReport = async () => {
        try {
            setActionLoading('print');
            setError(null);
            await documentOutputService.printReportDocument(
                reportingView === 'base-delta' ? DOCUMENT_TYPES.financeBaseDeltaReport : DOCUMENT_TYPES.financeTrialBalance,
                reportParameters());
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
            await documentOutputService.downloadReportDocument(
                reportingView === 'base-delta' ? DOCUMENT_TYPES.financeBaseDeltaReport : DOCUMENT_TYPES.financeTrialBalance,
                reportParameters());
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
                    <Button variant="outline" onClick={printReport} disabled={(reportingView === 'single' ? !report : !deltaReport) || actionLoading !== null}>
                        {actionLoading === 'print' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Printer className="mr-2 h-4 w-4" />}
                        Print
                    </Button>
                    <Button variant="outline" onClick={exportReport} disabled={(reportingView === 'single' ? !report : !deltaReport) || actionLoading !== null}>
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
                            <Label>Reporting view</Label>
                            <Select value={reportingView} onValueChange={(value) => setReportingView(value as 'single' | 'base-delta')}>
                                <SelectTrigger aria-label="Reporting view">
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="single">Single book</SelectItem>
                                    <SelectItem value="base-delta">Base + Delta</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2">
                            <Label>{reportingView === 'single' ? 'Book' : 'Delta adjustment layer'}</Label>
                            {reportingView === 'single' ? <Select value={bookClassification} onValueChange={setBookClassification}>
                                <SelectTrigger aria-label="Book">
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    {accountingBooks.filter(book => book.isActive !== false).map((book) => (
                                        <SelectItem key={book.code} value={book.code}>{book.name}</SelectItem>
                                    ))}
                                </SelectContent>
                            </Select> : <div className="max-h-32 space-y-2 overflow-y-auto rounded-md border p-3" aria-label="Delta adjustment layers">
                                {deltaBooks.map(book => {
                                    const incompatible = Boolean(selectedDeltaBaseId && book.baseAccountingBookId !== selectedDeltaBaseId
                                        && !deltaAccountingBookIds.includes(book.id));
                                    return <label key={book.id} className={`flex items-start gap-2 text-sm ${incompatible ? 'opacity-50' : ''}`}>
                                        <Checkbox checked={deltaAccountingBookIds.includes(book.id)} disabled={incompatible}
                                            onCheckedChange={checked => toggleDeltaBook(book.id, checked === true)} />
                                        <span>{book.code} — {book.name}<span className="ml-1 text-xs text-muted-foreground">({book.lifecycleStatus})</span></span>
                                    </label>;
                                })}
                            </div>}
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
                        {reportingView === 'single' && <ReportSegmentFilters
                            dimensions={reportingDimensions}
                            selections={segmentSelections}
                            onSelectionChange={updateSegmentSelection}
                            disabled={running}
                        />}
                        {reportingView === 'single' && <ReportDimensionFilters
                            definitions={transactionDimensions}
                            selections={dimensionSelections}
                            onSelectionChange={updateDimensionSelection}
                            disabled={running}
                        />}
                        {reportingView === 'single' && <div className="flex items-center gap-2 pb-2">
                            <Switch id="hide-zero" checked={hideZeroBalances} onCheckedChange={setHideZeroBalances} />
                            <Label htmlFor="hide-zero">Hide Zero Balances</Label>
                        </div>}
                        <Button onClick={runReport} disabled={running || reportingView === 'base-delta' && deltaAccountingBookIds.length === 0}>
                            {running && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Run Report
                        </Button>
                    </div>
                    {reportingView === 'base-delta' && <p className="mt-3 text-sm text-muted-foreground">{selectedDeltaBooks.length > 0 ? `Base book: ${selectedDeltaBooks[0].baseAccountingBookCode || 'governed base'} · Included layers: ${selectedDeltaBooks.map(book => book.code).join(', ')} · View: base balances plus posted Delta adjustments.` : 'Select one or more Delta adjustment layers sharing the same governed base book.'}</p>}
                    {reportingView === 'single' && segmentLoadError && <div className="mt-4 text-sm text-amber-700">{segmentLoadError}</div>}
                    {reportingView === 'single' && dimensionLoadError && <div className="mt-4 text-sm text-amber-700">{dimensionLoadError}</div>}
                    {error && <div className="mt-4 text-sm text-red-600">{error}</div>}
                    {reportingView === 'single' && <AppliedReportSegmentFilters
                        dimensions={reportingDimensions}
                        appliedFilters={appliedSegmentFilters}
                        pendingFilters={buildFinanceSegmentFilters(reportingDimensions, segmentSelections)}
                    />}
                    {reportingView === 'single' && <AppliedReportDimensionFilters
                        definitions={transactionDimensions}
                        appliedFilters={appliedDimensionFilters}
                        pendingFilters={buildFinanceDimensionFilters(transactionDimensions, dimensionSelections)}
                    />}
                    {reportingView === 'single' && appliedDimensionFilters.length > 0 && (
                        <p className="mt-3 text-xs text-amber-700">
                            Transaction-dimension totals include only ledger lines carrying the selected immutable coding. Operational adapters remain outside this view until individually certified. Analytical slices may be unbalanced unless the selected dimension is governed as Balancing.
                        </p>
                    )}
                </CardContent>
            </Card>

            {reportingView === 'single' ? <Card>
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
            </Card> : <Card>
                <CardHeader className="border-b pb-2">
                    <div className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
                        <CardTitle className="text-lg">{deltaReport ? `${deltaReport.baseAccountingBookCode} + ${deltaReport.deltaAccountingBookCode}` : 'Base + Delta report'}</CardTitle>
                        <span className="text-sm text-muted-foreground">Currency: {deltaReport?.functionalCurrencyCode || settings?.baseCurrency || 'GHS'} | {filteredDeltaLines.length} accounts</span>
                    </div>
                </CardHeader>
                <CardContent className="space-y-4 pt-6">
                    <div className="grid gap-3 md:grid-cols-3">
                        <div className="rounded-md border p-3"><p className="text-xs text-muted-foreground">Base net control</p><p className="font-medium tabular-nums">{formatSigned(deltaReport?.baseTotal ?? 0)}</p></div>
                        <div className="rounded-md border p-3"><p className="text-xs text-muted-foreground">Delta net control</p><p className="font-medium tabular-nums">{formatSigned(deltaReport?.deltaTotal ?? 0)}</p></div>
                        <div className="rounded-md border p-3"><p className="text-xs text-muted-foreground">Combined net control</p><p className="font-medium tabular-nums">{formatSigned(deltaReport?.combinedTotal ?? 0)}</p></div>
                    </div>
                    <p className="text-xs text-muted-foreground">Positive values are net debits; negative values are net credits. These controls should normally be zero for balanced posted journals.</p>
                    <div className="rounded-md border"><Table>
                        <TableHeader><TableRow className="bg-muted/50"><TableHead>Account</TableHead><TableHead>Account Name</TableHead><TableHead>Type</TableHead><TableHead className="text-right">Base ({deltaReport?.baseAccountingBookCode || '—'})</TableHead><TableHead className="text-right">Delta ({deltaReport?.deltaAccountingBookCode || '—'})</TableHead><TableHead className="text-right">Combined</TableHead></TableRow></TableHeader>
                        <TableBody>{filteredDeltaLines.length > 0 ? filteredDeltaLines.map(line => <TableRow key={line.accountId} className={line.deltaSignedBalance !== 0 ? 'bg-blue-50/60' : undefined}><TableCell className="font-mono text-sm">{line.accountNumber}</TableCell><TableCell>{line.accountName}</TableCell><TableCell>{line.accountType}</TableCell><TableCell className="text-right tabular-nums">{formatSigned(line.baseSignedBalance)}</TableCell><TableCell className="text-right tabular-nums">{formatSigned(line.deltaSignedBalance)}</TableCell><TableCell className="text-right tabular-nums font-medium">{formatSigned(line.combinedSignedBalance)}</TableCell></TableRow>) : <TableRow><TableCell colSpan={6} className="py-8 text-center text-muted-foreground">{deltaReport ? 'No mapped accounts or posted balances were found.' : 'Choose a Delta adjustment layer and run the report.'}</TableCell></TableRow>}</TableBody>
                    </Table></div>
                </CardContent>
            </Card>}
        </div>
    );
}
