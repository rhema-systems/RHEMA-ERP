'use client';

import React, { useEffect, useState } from 'react';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableRow } from '@/components/ui/table';
import { DOCUMENT_TYPES, documentOutputService } from '@/services/document-output.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { BalanceSheetCategoryDto, BalanceSheetReportDto, BalanceSheetSectionDto, FinanceSettings, FinancialStatementLineItemDto } from '@/types/finance';
import { Download, Loader2, Printer } from 'lucide-react';
import { DEFAULT_ACCOUNTING_BOOKS } from '@/lib/finance/accounting-books';
import { ReportSegmentFilters } from '@/components/finance/reports/ReportSegmentFilters';
import { AppliedReportSegmentFilters } from '@/components/finance/reports/AppliedReportSegmentFilters';
import { ReportDimensionFilters } from '@/components/finance/reports/ReportDimensionFilters';
import { AppliedReportDimensionFilters } from '@/components/finance/reports/AppliedReportDimensionFilters';
import { FinancialStatementLayoutRows } from '@/components/finance/reports/FinancialStatementLayoutRows';
import {
    buildFinanceSegmentFilters,
    toFinanceSegmentFilterQueryParameters,
    type ReportSegmentSelections,
} from '@/lib/finance/report-segment-filters';
import type { FinanceSegmentFilterDto, FinancialStatementLayoutSummaryDto, SegmentStructure } from '@/types/finance';
import {
    buildFinanceDimensionFilters,
    toFinanceDimensionFilterQueryParameters,
    type ReportDimensionSelections,
} from '@/lib/finance/report-dimension-filters';
import type { FinanceDimensionDefinition, FinanceDimensionFilterDto } from '@/types/finance';

export default function BalanceSheetPage() {
    const [asAtDate, setAsAtDate] = useState(new Date().toISOString().split('T')[0]);
    const [bookClassification, setBookClassification] = useState('IFRS');
    const [accountingBooks, setAccountingBooks] = useState(DEFAULT_ACCOUNTING_BOOKS);
    const [layouts, setLayouts] = useState<FinancialStatementLayoutSummaryDto[]>([]);
    const [layoutSelection, setLayoutSelection] = useState('default');
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
    const [report, setReport] = useState<BalanceSheetReportDto | null>(null);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        loadInitialReport();
    }, []);

    const loadInitialReport = async () => {
        try {
            setLoading(true);
            const [settingsData, books, dimensions, layoutOptions, transactionDimensionOptions] = await Promise.all([
                financeDataService.getFinanceSettings(),
                financeDataService.getAccountingBooks().catch(() => DEFAULT_ACCOUNTING_BOOKS),
                financeDataService.getReportingDimensions().catch((err) => {
                    console.error('Error loading reporting dimensions:', err);
                    setSegmentLoadError('GL segment filters could not be loaded.');
                    return [] as SegmentStructure[];
                }),
                financeDataService.getFinancialStatementLayouts('BalanceSheet').catch(() => []),
                financeDataService.getFinanceDimensions(true).catch(() => {
                    setDimensionLoadError('Transaction-dimension filters could not be loaded.');
                    return [];
                }),
            ]);
            setSettings(settingsData);
            if (books.length > 0) setAccountingBooks(books);
            setReportingDimensions(dimensions);
            setLayouts(layoutOptions.filter((layout) => layout.isActive && layout.publishedVersionNumber));
            setTransactionDimensions(transactionDimensionOptions);
            const data = await financeDataService.getBalanceSheet({
                asAtDate,
                bookClassification,
                includeAccountDetails: true,
                segmentFilters: [],
                dimensionFilters: [],
                useDefaultLayout: true,
            });
            setReport(data);
        } catch (err) {
            console.error('Error loading balance sheet report:', err);
            setError('Could not generate the balance sheet.');
        } finally {
            setLoading(false);
        }
    };

    const runReport = async () => {
        try {
            setRunning(true);
            setError(null);
            const segmentFilters = buildFinanceSegmentFilters(reportingDimensions, segmentSelections);
            const dimensionFilters = buildFinanceDimensionFilters(transactionDimensions, dimensionSelections);
            const data = await financeDataService.getBalanceSheet({
                asAtDate,
                bookClassification,
                includeAccountDetails: true,
                segmentFilters,
                dimensionFilters,
                layoutId: layoutSelection !== 'default' && layoutSelection !== 'legacy'
                    ? layoutSelection
                    : undefined,
                useDefaultLayout: layoutSelection === 'default',
            });
            setReport(data);
            setAppliedSegmentFilters(segmentFilters);
            setAppliedDimensionFilters(dimensionFilters);
        } catch (err) {
            console.error('Error loading balance sheet report:', err);
            setError('Could not generate the balance sheet.');
        } finally {
            setRunning(false);
        }
    };

    const formatMoney = (amount: number) => new Intl.NumberFormat('en-GH', {
        style: 'decimal',
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    }).format(amount || 0);

    const reportParameters = () => {
        return {
            asAtDate,
            bookClassification,
            includeAccountDetails: true,
            layoutId: layoutSelection !== 'default' && layoutSelection !== 'legacy'
                ? layoutSelection
                : undefined,
            useDefaultLayout: layoutSelection === 'default',
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

    const printReport = async () => {
        try {
            setActionLoading('print');
            setError(null);
            await documentOutputService.printReportDocument(DOCUMENT_TYPES.financeBalanceSheet, reportParameters());
        } catch (err) {
            console.error('Error printing balance sheet:', err);
            setError('Could not print the balance sheet.');
        } finally {
            setActionLoading(null);
        }
    };

    const exportReport = async () => {
        try {
            setActionLoading('export');
            setError(null);
            await documentOutputService.downloadReportDocument(DOCUMENT_TYPES.financeBalanceSheet, reportParameters());
        } catch (err) {
            console.error('Error exporting balance sheet:', err);
            setError('Could not export the balance sheet.');
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
                    <h1 className="text-3xl font-bold tracking-tight">Balance Sheet</h1>
                    <p className="text-muted-foreground">
                        Statement of Financial Position
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
                    <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbLink href="/finance">Finance</BreadcrumbLink></BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbLink href="/finance/reports">Reports</BreadcrumbLink></BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbPage>Balance Sheet</BreadcrumbPage></BreadcrumbItem>
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
                            <Select
                                value={bookClassification}
                                onValueChange={(value) => {
                                    setBookClassification(value);
                                    setLayoutSelection('default');
                                }}
                            >
                                <SelectTrigger><SelectValue /></SelectTrigger>
                                <SelectContent>
                                    {accountingBooks.map((book) => (
                                        <SelectItem key={book.code} value={book.code}>{book.name}</SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2">
                            <Label>Statement Layout</Label>
                            <Select value={layoutSelection} onValueChange={setLayoutSelection}>
                                <SelectTrigger><SelectValue /></SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="default">Default layout</SelectItem>
                                    {layouts
                                        .filter((layout) => layout.accountingBookCode === bookClassification)
                                        .map((layout) => (
                                            <SelectItem key={layout.id} value={layout.id}>
                                                {layout.name} ({layout.code}){layout.isDefault ? ' — default' : ''}
                                            </SelectItem>
                                        ))}
                                    <SelectItem value="legacy">Legacy classification</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>
                        <ReportSegmentFilters
                            dimensions={reportingDimensions}
                            selections={segmentSelections}
                            onSelectionChange={updateSegmentSelection}
                            disabled={running}
                        />
                        <ReportDimensionFilters
                            definitions={transactionDimensions}
                            selections={dimensionSelections}
                            onSelectionChange={updateDimensionSelection}
                            disabled={running}
                        />
                        <Button onClick={runReport} disabled={running}>
                            {running && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Run Report
                        </Button>
                    </div>
                    {segmentLoadError && <div className="mt-4 text-sm text-amber-700">{segmentLoadError}</div>}
                    {dimensionLoadError && <div className="mt-4 text-sm text-amber-700">{dimensionLoadError}</div>}
                    {error && <div className="mt-4 text-sm text-red-600">{error}</div>}
                    <AppliedReportSegmentFilters
                        dimensions={reportingDimensions}
                        appliedFilters={appliedSegmentFilters}
                        pendingFilters={buildFinanceSegmentFilters(reportingDimensions, segmentSelections)}
                    />
                    <AppliedReportDimensionFilters
                        definitions={transactionDimensions}
                        appliedFilters={appliedDimensionFilters}
                        pendingFilters={buildFinanceDimensionFilters(transactionDimensions, dimensionSelections)}
                    />
                    {appliedDimensionFilters.length > 0 && (
                        <p className="mt-3 text-xs text-amber-700">
                            Transaction-dimension totals include only ledger lines carrying the selected immutable coding. Operational adapters remain outside this view until individually certified. Analytical slices may not present a balanced statement unless the selected dimension is governed as Balancing.
                        </p>
                    )}
                </CardContent>
            </Card>

            <Card>
                <CardHeader className="border-b pb-2 text-center">
                    <CardTitle className="text-xl">Balance Sheet</CardTitle>
                    <p className="text-sm text-muted-foreground">
                        As at {new Date(report?.asAtDate || asAtDate).toLocaleDateString()}
                    </p>
                    <p className="mt-1 font-mono text-xs text-muted-foreground">
                        Currency: {report?.currencyCode || settings?.baseCurrency || 'GHS'}
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">
                        {report?.layoutExecution
                            ? `Layout: ${report.layoutExecution.layoutName} (${report.layoutExecution.layoutCode}) v${report.layoutExecution.versionNumber}`
                            : 'Legacy account-classification presentation'}
                    </p>
                </CardHeader>
                <CardContent className="pt-6">
                    <div className="mx-auto max-w-4xl rounded-md border bg-white p-6 shadow-sm">
                        <Table>
                            <TableBody>
                                {report?.layoutExecution ? (
                                    <FinancialStatementLayoutRows
                                        rows={report.layoutExecution.rows}
                                        formatMoney={formatMoney}
                                    />
                                ) : (
                                    <>
                                        {(report?.sections ?? []).map(section => (
                                            <BalanceSheetSection key={section.sectionName} section={section} formatMoney={formatMoney} />
                                        ))}
                                        <TableRow className="border-t-4 bg-blue-50/50 text-lg font-bold">
                                            <TableCell colSpan={2} className="py-4 text-right">Total Assets</TableCell>
                                            <TableCell className="w-[160px] py-4 text-right">{formatMoney(report?.totalAssets ?? 0)}</TableCell>
                                        </TableRow>
                                        <TableRow className="bg-blue-50/50 text-lg font-bold">
                                            <TableCell colSpan={2} className="py-4 text-right">Total Liabilities and Equity</TableCell>
                                            <TableCell className="w-[160px] py-4 text-right">{formatMoney((report?.totalLiabilities ?? 0) + (report?.totalEquity ?? 0))}</TableCell>
                                        </TableRow>
                                    </>
                                )}
                            </TableBody>
                        </Table>
                    </div>
                    {report && !report.isBalanced && (
                        <div className="mt-3 text-sm text-red-600">
                            Balance sheet is out of balance.
                        </div>
                    )}
                    {report?.layoutExecution && report.layoutExecution.reconciliation.unmappedNonZeroAccountCount > 0 && (
                        <div className="mt-3 text-sm text-amber-700">
                            {report.layoutExecution.reconciliation.unmappedNonZeroAccountCount} non-zero eligible GL account(s) are not mapped to this layout.
                        </div>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}

function BalanceSheetSection({ section, formatMoney }: { section: BalanceSheetSectionDto; formatMoney: (amount: number) => string }) {
    return (
        <>
            <TableRow className="bg-slate-100 hover:bg-slate-100">
                <TableCell colSpan={3} className="py-4 text-lg font-bold">{section.sectionName}</TableCell>
            </TableRow>
            {section.categories.map(category => (
                <BalanceSheetCategory key={`${section.sectionName}-${category.categoryName}`} category={category} formatMoney={formatMoney} />
            ))}
            <TableRow className="border-t bg-muted/50 font-bold">
                <TableCell colSpan={2} className="text-right">Total {section.sectionName}</TableCell>
                <TableCell className="w-[160px] text-right">{formatMoney(section.sectionTotal)}</TableCell>
            </TableRow>
            <TableRow className="h-6"><TableCell colSpan={3} /></TableRow>
        </>
    );
}

function BalanceSheetCategory({ category, formatMoney }: { category: BalanceSheetCategoryDto; formatMoney: (amount: number) => string }) {
    return (
        <>
            <TableRow className="bg-muted/30 hover:bg-muted/30">
                <TableCell colSpan={3} className="py-3 text-base font-bold">{category.categoryName}</TableCell>
            </TableRow>
            {category.lineItems.length > 0 ? (
                category.lineItems.map(line => <BalanceSheetLine key={`${category.categoryName}-${line.lineItemName}`} line={line} formatMoney={formatMoney} />)
            ) : (
                <TableRow><TableCell colSpan={3} className="text-muted-foreground italic">No lines</TableCell></TableRow>
            )}
            <TableRow className="border-t bg-muted/40 font-semibold">
                <TableCell colSpan={2} className="text-right">Total {category.categoryName}</TableCell>
                <TableCell className="w-[160px] text-right">{formatMoney(category.categoryTotal)}</TableCell>
            </TableRow>
        </>
    );
}

function BalanceSheetLine({ line, formatMoney }: { line: FinancialStatementLineItemDto; formatMoney: (amount: number) => string }) {
    return (
        <TableRow className="border-0">
            <TableCell className="w-[160px] font-mono text-sm text-muted-foreground">{line.accountNumbers?.join(', ') || '-'}</TableCell>
            <TableCell>{line.lineItemName}</TableCell>
            <TableCell className="w-[160px] text-right">{formatMoney(line.amount)}</TableCell>
        </TableRow>
    );
}
