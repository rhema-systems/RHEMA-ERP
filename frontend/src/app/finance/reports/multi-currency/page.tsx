'use client';

import React, { useEffect, useMemo, useState } from 'react';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { DOCUMENT_TYPES, documentOutputService } from '@/services/document-output.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { FinanceSettings, MultiCurrencyAccountDetailDto, MultiCurrencyDetailReportDto } from '@/types/finance';
import { Download, Loader2, Printer } from 'lucide-react';

export default function MultiCurrencyReportPage() {
    const [startDate, setStartDate] = useState(new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0]);
    const [endDate, setEndDate] = useState(new Date().toISOString().split('T')[0]);
    const [selectedCurrency, setSelectedCurrency] = useState('ALL');
    const [loading, setLoading] = useState(true);
    const [running, setRunning] = useState(false);
    const [actionLoading, setActionLoading] = useState<'print' | 'export' | null>(null);
    const [settings, setSettings] = useState<FinanceSettings | null>(null);
    const [report, setReport] = useState<MultiCurrencyDetailReportDto | null>(null);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        loadInitialReport();
    }, []);

    const loadInitialReport = async () => {
        try {
            setLoading(true);
            const settingsData = await financeDataService.getFinanceSettings();
            setSettings(settingsData);
            const data = await financeDataService.getMultiCurrencyDetailReport({
                startDate,
                endDate,
                includeRevaluation: true,
            });
            setReport(data);
        } catch (err) {
            console.error('Error loading multi-currency detail report:', err);
            setError('Could not generate the multi-currency detail report.');
        } finally {
            setLoading(false);
        }
    };

    const runReport = async () => {
        try {
            setRunning(true);
            setError(null);
            const data = await financeDataService.getMultiCurrencyDetailReport({
                startDate,
                endDate,
                currencyCode: selectedCurrency === 'ALL' ? undefined : selectedCurrency,
                includeRevaluation: true,
            });
            setReport(data);
        } catch (err) {
            console.error('Error loading multi-currency detail report:', err);
            setError('Could not generate the multi-currency detail report.');
        } finally {
            setRunning(false);
        }
    };

    const uniqueCurrencies = useMemo(() => {
        const currencies = new Set<string>();
        report?.accounts.forEach(account => currencies.add(account.currencyCode));
        return Array.from(currencies).sort();
    }, [report]);

    const filteredAccounts = useMemo(() => {
        const accounts = report?.accounts ?? [];
        return selectedCurrency === 'ALL'
            ? accounts
            : accounts.filter(account => account.currencyCode === selectedCurrency);
    }, [report, selectedCurrency]);

    const totalBaseAmount = filteredAccounts.reduce((sum, account) => sum + account.closingBalanceBase, 0);

    const formatMoney = (amount: number, currency?: string) => new Intl.NumberFormat('en-GH', {
        style: currency ? 'currency' : 'decimal',
        currency: currency || undefined,
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    }).format(amount || 0);

    const reportParameters = () => ({
        startDate,
        endDate,
        currencyCode: selectedCurrency === 'ALL' ? undefined : selectedCurrency,
        includeRevaluation: true,
    });

    const printReport = async () => {
        try {
            setActionLoading('print');
            setError(null);
            await documentOutputService.printReportDocument(DOCUMENT_TYPES.financeMultiCurrencyDetail, reportParameters());
        } catch (err) {
            console.error('Error printing multi-currency detail report:', err);
            setError('Could not print the multi-currency detail report.');
        } finally {
            setActionLoading(null);
        }
    };

    const exportReport = async () => {
        try {
            setActionLoading('export');
            setError(null);
            await documentOutputService.downloadReportDocument(DOCUMENT_TYPES.financeMultiCurrencyDetail, reportParameters());
        } catch (err) {
            console.error('Error exporting multi-currency detail report:', err);
            setError('Could not export the multi-currency detail report.');
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
                    <h1 className="text-3xl font-bold tracking-tight">Multi-Currency Detail</h1>
                    <p className="text-muted-foreground">
                        Foreign currency balances and base equivalents
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
                    <BreadcrumbItem><BreadcrumbPage>Multi-Currency Detail</BreadcrumbPage></BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            <Card>
                <CardContent className="p-6">
                    <div className="grid grid-cols-1 items-end gap-4 md:grid-cols-4">
                        <div className="space-y-2">
                            <Label>From Date</Label>
                            <Input type="date" value={startDate} onChange={(event) => setStartDate(event.target.value)} />
                        </div>
                        <div className="space-y-2">
                            <Label>To Date</Label>
                            <Input type="date" value={endDate} onChange={(event) => setEndDate(event.target.value)} />
                        </div>
                        <div className="space-y-2">
                            <Label>Currency</Label>
                            <Select value={selectedCurrency} onValueChange={setSelectedCurrency}>
                                <SelectTrigger><SelectValue /></SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="ALL">All Currencies</SelectItem>
                                    {uniqueCurrencies.map(currency => (
                                        <SelectItem key={currency} value={currency}>{currency}</SelectItem>
                                    ))}
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

            <Card>
                <CardHeader className="border-b pb-2 text-center">
                    <CardTitle className="text-xl">Multi-Currency Detail Report</CardTitle>
                    <p className="text-sm text-muted-foreground">
                        {report
                            ? `${new Date(report.periodStart).toLocaleDateString()} to ${new Date(report.periodEnd).toLocaleDateString()}`
                            : `${new Date(startDate).toLocaleDateString()} to ${new Date(endDate).toLocaleDateString()}`}
                    </p>
                </CardHeader>
                <CardContent className="pt-6">
                    {filteredAccounts.length > 0 ? (
                        <div className="space-y-8">
                            {filteredAccounts.map(account => (
                                <MultiCurrencyAccountSection key={`${account.accountId}-${account.currencyCode}`} account={account} formatMoney={formatMoney} />
                            ))}
                            <div className="flex justify-end border-t pt-4">
                                <div className="grid w-full max-w-md grid-cols-2 gap-3 text-sm">
                                    <div className="text-muted-foreground">Total Closing Base</div>
                                    <div className="text-right font-bold">{formatMoney(totalBaseAmount, settings?.baseCurrency || 'GHS')}</div>
                                </div>
                            </div>
                        </div>
                    ) : (
                        <div className="py-16 text-center text-muted-foreground">No multi-currency detail found</div>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}

function MultiCurrencyAccountSection({ account, formatMoney }: { account: MultiCurrencyAccountDetailDto; formatMoney: (amount: number, currency?: string) => string }) {
    return (
        <div className="rounded-md border">
            <div className="flex flex-col gap-2 border-b bg-muted/30 px-4 py-3 md:flex-row md:items-center md:justify-between">
                <div>
                    <div className="font-mono text-sm font-semibold">{account.accountNumber}</div>
                    <div className="text-sm text-muted-foreground">{account.accountName}</div>
                </div>
                <div className="grid grid-cols-2 gap-x-6 gap-y-1 text-sm md:grid-cols-4">
                    <span className="text-muted-foreground">Currency</span>
                    <span className="font-medium">{account.currencyCode}</span>
                    <span className="text-muted-foreground">Closing Base</span>
                    <span className="font-medium">{formatMoney(account.closingBalanceBase)}</span>
                </div>
            </div>
            <div className="overflow-x-auto">
                <Table>
                    <TableHeader>
                        <TableRow>
                            <TableHead className="w-[110px]">Date</TableHead>
                            <TableHead className="w-[130px]">Reference</TableHead>
                            <TableHead>Description</TableHead>
                            <TableHead className="w-[90px]">Type</TableHead>
                            <TableHead className="text-right">Foreign</TableHead>
                            <TableHead className="text-right">Rate</TableHead>
                            <TableHead className="text-right">Base</TableHead>
                            <TableHead className="text-right">Running Base</TableHead>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {account.transactions.length > 0 ? (
                            account.transactions.map((line, index) => (
                                <TableRow key={`${account.accountId}-${account.currencyCode}-${index}`}>
                                    <TableCell>{new Date(line.transactionDate).toLocaleDateString()}</TableCell>
                                    <TableCell>{line.reference || '-'}</TableCell>
                                    <TableCell>{line.description || '-'}</TableCell>
                                    <TableCell>{line.transactionType}</TableCell>
                                    <TableCell className="text-right">{formatMoney(line.foreignAmount, account.currencyCode)}</TableCell>
                                    <TableCell className="text-right">{line.exchangeRate ? line.exchangeRate.toFixed(6) : '-'}</TableCell>
                                    <TableCell className="text-right">{formatMoney(line.baseAmount)}</TableCell>
                                    <TableCell className="text-right font-medium">{formatMoney(line.runningBalanceBase)}</TableCell>
                                </TableRow>
                            ))
                        ) : (
                            <TableRow>
                                <TableCell colSpan={8} className="py-6 text-center text-muted-foreground">
                                    No transactions for this account and currency
                                </TableCell>
                            </TableRow>
                        )}
                        <TableRow className="bg-muted/30 font-bold">
                            <TableCell colSpan={4} className="text-right">Account Totals</TableCell>
                            <TableCell className="text-right">{formatMoney(account.closingBalanceForeign, account.currencyCode)}</TableCell>
                            <TableCell />
                            <TableCell className="text-right">{formatMoney(account.closingBalanceBase)}</TableCell>
                            <TableCell />
                        </TableRow>
                    </TableBody>
                </Table>
            </div>
        </div>
    );
}
