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
import type { CashFlowSectionDto, CashFlowStatementReportDto, FinanceSettings, FinancialStatementLineItemDto } from '@/types/finance';
import { Download, Loader2, Printer } from 'lucide-react';
import { DEFAULT_ACCOUNTING_BOOKS } from '@/lib/finance/accounting-books';

export default function CashFlowStatementPage() {
    const [startDate, setStartDate] = useState(new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0]);
    const [endDate, setEndDate] = useState(new Date().toISOString().split('T')[0]);
    const [bookClassification, setBookClassification] = useState('IFRS');
    const [accountingBooks, setAccountingBooks] = useState(DEFAULT_ACCOUNTING_BOOKS);
    const [method, setMethod] = useState<'Direct' | 'Indirect'>('Indirect');
    const [loading, setLoading] = useState(true);
    const [running, setRunning] = useState(false);
    const [actionLoading, setActionLoading] = useState<'print' | 'export' | null>(null);
    const [settings, setSettings] = useState<FinanceSettings | null>(null);
    const [report, setReport] = useState<CashFlowStatementReportDto | null>(null);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        loadInitialReport();
    }, []);

    const loadInitialReport = async () => {
        try {
            setLoading(true);
            const settingsData = await financeDataService.getFinanceSettings();
            const books = await financeDataService.getAccountingBooks().catch(() => DEFAULT_ACCOUNTING_BOOKS);
            setSettings(settingsData);
            if (books.length > 0) setAccountingBooks(books);
            const data = await financeDataService.getCashFlowStatement({
                periodStart: startDate,
                periodEnd: endDate,
                bookClassification,
                includeAccountDetails: true,
                method,
            });
            setReport(data);
        } catch (err) {
            console.error('Error loading cash flow statement:', err);
            setError(err instanceof Error ? err.message : 'Could not generate the cash flow statement.');
        } finally {
            setLoading(false);
        }
    };

    const runReport = async () => {
        try {
            setRunning(true);
            setError(null);
            const data = await financeDataService.getCashFlowStatement({
                periodStart: startDate,
                periodEnd: endDate,
                bookClassification,
                includeAccountDetails: true,
                method,
            });
            setReport(data);
        } catch (err) {
            console.error('Error loading cash flow statement:', err);
            setError(err instanceof Error ? err.message : 'Could not generate the cash flow statement.');
        } finally {
            setRunning(false);
        }
    };

    const formatMoney = (amount: number) => new Intl.NumberFormat('en-GH', {
        style: 'decimal',
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    }).format(amount || 0);

    const reportParameters = () => ({
        periodStart: startDate,
        periodEnd: endDate,
        bookClassification,
        includeAccountDetails: true,
        method,
    });

    const printReport = async () => {
        try {
            setActionLoading('print');
            setError(null);
            await documentOutputService.printReportDocument(DOCUMENT_TYPES.financeCashFlowStatement, reportParameters());
        } catch (err) {
            console.error('Error printing cash flow statement:', err);
            setError('Could not print the cash flow statement.');
        } finally {
            setActionLoading(null);
        }
    };

    const exportReport = async () => {
        try {
            setActionLoading('export');
            setError(null);
            await documentOutputService.downloadReportDocument(DOCUMENT_TYPES.financeCashFlowStatement, reportParameters());
        } catch (err) {
            console.error('Error exporting cash flow statement:', err);
            setError('Could not export the cash flow statement.');
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
                    <h1 className="text-3xl font-bold tracking-tight">Cash Flow Statement</h1>
                    <p className="text-muted-foreground">Statement of Cash Flows</p>
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
                    <BreadcrumbItem><BreadcrumbPage>Cash Flow</BreadcrumbPage></BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            <Card>
                <CardContent className="p-6">
                    <div className="grid grid-cols-1 items-end gap-4 md:grid-cols-5">
                        <div className="space-y-2">
                            <Label>From Date</Label>
                            <Input type="date" value={startDate} onChange={(event) => setStartDate(event.target.value)} />
                        </div>
                        <div className="space-y-2">
                            <Label>To Date</Label>
                            <Input type="date" value={endDate} onChange={(event) => setEndDate(event.target.value)} />
                        </div>
                        <div className="space-y-2">
                            <Label>Book</Label>
                            <Select value={bookClassification} onValueChange={setBookClassification}>
                                <SelectTrigger><SelectValue /></SelectTrigger>
                                <SelectContent>
                                    {accountingBooks.map((book) => (
                                        <SelectItem key={book.code} value={book.code}>{book.name}</SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2">
                            <Label>Method</Label>
                            <Select value={method} onValueChange={(value) => setMethod(value as 'Direct' | 'Indirect')}>
                                <SelectTrigger><SelectValue /></SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="Indirect">Indirect</SelectItem>
                                    <SelectItem value="Direct">Direct</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>
                        <Button onClick={runReport} disabled={running}>
                            {running && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Run Report
                        </Button>
                    </div>
                    {error && <div className="mt-4 text-sm text-red-600">{error}</div>}
                </CardContent>
            </Card>

            {report?.presentationWarnings?.length ? (
                <div className="rounded-md border border-amber-300 bg-amber-50 p-4 text-sm text-amber-900">
                    <p className="font-semibold">Cash-flow presentation review required</p>
                    <ul className="mt-2 list-disc space-y-1 pl-5">
                        {report.presentationWarnings.map((warning) => (
                            <li key={warning}>{warning}</li>
                        ))}
                    </ul>
                </div>
            ) : null}

            <Card>
                <CardHeader className="border-b pb-2 text-center">
                    <CardTitle className="text-xl">
                        Statement of Cash Flows — {report?.method || method} Method
                    </CardTitle>
                    <p className="text-sm text-muted-foreground">
                        For the period {new Date(report?.periodStart || startDate).toLocaleDateString()} to {new Date(report?.periodEnd || endDate).toLocaleDateString()}
                    </p>
                    <p className="mt-1 font-mono text-xs text-muted-foreground">
                        Currency: {report?.currencyCode || settings?.baseCurrency || 'GHS'}
                    </p>
                </CardHeader>
                <CardContent className="pt-6">
                    <div className="mx-auto max-w-4xl rounded-md border bg-white p-6 shadow-sm">
                        <Table>
                            <TableBody>
                                {report && (
                                    <>
                                        <CashFlowSection section={report.operatingActivities} formatMoney={formatMoney} />
                                        <CashFlowSection section={report.investingActivities} formatMoney={formatMoney} />
                                        <CashFlowSection section={report.financingActivities} formatMoney={formatMoney} />
                                    </>
                                )}
                                <TableRow className="border-t-4 bg-blue-50/50 text-lg font-bold">
                                    <TableCell colSpan={2} className="py-4 text-right">Net Increase (Decrease) in Cash</TableCell>
                                    <TableCell className="w-[160px] py-4 text-right">{formatMoney(report?.netIncreaseInCash ?? 0)}</TableCell>
                                </TableRow>
                                <TableRow>
                                    <TableCell colSpan={2} className="pt-4 text-right">Cash Balance at Beginning of Period</TableCell>
                                    <TableCell className="w-[160px] pt-4 text-right">{formatMoney(report?.cashAtBeginning ?? 0)}</TableCell>
                                </TableRow>
                                <TableRow className="border-t-2 border-black text-lg font-bold">
                                    <TableCell colSpan={2} className="text-right">Cash Balance at End of Period</TableCell>
                                    <TableCell className="w-[160px] text-right">{formatMoney(report?.cashAtEnd ?? 0)}</TableCell>
                                </TableRow>
                            </TableBody>
                        </Table>
                    </div>
                    {report && !report.isReconciled && (
                        <div className="mt-3 text-sm text-red-600">
                            Cash flow statement is not reconciled.
                        </div>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}

function CashFlowSection({ section, formatMoney }: { section: CashFlowSectionDto; formatMoney: (amount: number) => string }) {
    return (
        <>
            <TableRow className="bg-muted/30 hover:bg-muted/30">
                <TableCell colSpan={3} className="py-3 text-base font-bold">{section.sectionName}</TableCell>
            </TableRow>
            {section.lineItems.length > 0 ? (
                section.lineItems.map(line => <CashFlowLine key={`${section.sectionName}-${line.lineItemName}`} line={line} formatMoney={formatMoney} />)
            ) : (
                <TableRow><TableCell colSpan={3} className="text-muted-foreground italic">No lines</TableCell></TableRow>
            )}
            <TableRow className="border-t bg-muted/50 font-bold">
                <TableCell colSpan={2} className="text-right">Net {section.sectionName}</TableCell>
                <TableCell className="w-[160px] text-right">{formatMoney(section.sectionTotal)}</TableCell>
            </TableRow>
            <TableRow className="h-4"><TableCell colSpan={3} /></TableRow>
        </>
    );
}

function CashFlowLine({ line, formatMoney }: { line: FinancialStatementLineItemDto; formatMoney: (amount: number) => string }) {
    return (
        <TableRow className="border-0">
            <TableCell className="w-[160px] font-mono text-sm text-muted-foreground">{line.accountNumbers?.join(', ') || '-'}</TableCell>
            <TableCell>{line.lineItemName}</TableCell>
            <TableCell className="w-[160px] text-right">{formatMoney(line.amount)}</TableCell>
        </TableRow>
    );
}
