'use client';

import React, { useEffect, useMemo, useState } from 'react';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { DOCUMENT_TYPES, documentOutputService } from '@/services/document-output.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { Account, DetailedLedgerAccountDto, DetailedLedgerReportDto, FinanceSettings } from '@/types/finance';
import { Download, Loader2, Printer, Search } from 'lucide-react';
import { useSearchParams } from 'next/navigation';
import { DEFAULT_ACCOUNTING_BOOKS, getAccountingBookName } from '@/lib/finance/accounting-books';
import { ReportDimensionFilters } from '@/components/finance/reports/ReportDimensionFilters';
import { AppliedReportDimensionFilters } from '@/components/finance/reports/AppliedReportDimensionFilters';
import {
    buildFinanceDimensionFilters,
    readFinanceDimensionSelections,
    toFinanceDimensionFilterQueryParameters,
    type ReportDimensionSelections,
} from '@/lib/finance/report-dimension-filters';
import type { FinanceDimensionDefinition, FinanceDimensionFilterDto } from '@/types/finance';

const formatDateInput = (date: Date) => date.toISOString().split('T')[0];
const parseBooleanParam = (value: string | null, fallback: boolean) => value == null ? fallback : value === 'true';

export default function DetailedLedgerPage() {
    const searchParams = useSearchParams();
    const [startDate, setStartDate] = useState(searchParams.get('startDate') || formatDateInput(new Date(new Date().getFullYear(), 0, 1)));
    const [endDate, setEndDate] = useState(searchParams.get('endDate') || formatDateInput(new Date()));
    const [bookClassification, setBookClassification] = useState(searchParams.get('bookClassification') || 'BASE');
    const [accountingBooks, setAccountingBooks] = useState(DEFAULT_ACCOUNTING_BOOKS);
    const [includeReversed, setIncludeReversed] = useState(parseBooleanParam(searchParams.get('includeReversed'), true));
    const [accountSearch, setAccountSearch] = useState('');
    const [selectedAccountIds, setSelectedAccountIds] = useState<string[]>(() =>
        searchParams.getAll('accountIds')
            .flatMap(value => value.split(','))
            .map(value => value.trim())
            .filter(Boolean)
    );
    const [accounts, setAccounts] = useState<Account[]>([]);
    const [transactionDimensions, setTransactionDimensions] = useState<FinanceDimensionDefinition[]>([]);
    const [dimensionSelections, setDimensionSelections] = useState<ReportDimensionSelections>({});
    const [appliedDimensionFilters, setAppliedDimensionFilters] = useState<FinanceDimensionFilterDto[]>([]);
    const [dimensionLoadError, setDimensionLoadError] = useState<string | null>(null);
    const [settings, setSettings] = useState<FinanceSettings | null>(null);
    const [report, setReport] = useState<DetailedLedgerReportDto | null>(null);
    const [loading, setLoading] = useState(true);
    const [running, setRunning] = useState(false);
    const [actionLoading, setActionLoading] = useState<'print' | 'export' | null>(null);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        loadInitialData();
    }, []);

    const loadInitialData = async () => {
        try {
            setLoading(true);
            const settingsData = await financeDataService.getFinanceSettings();
            const [accountData, books, dimensionOptions] = await Promise.all([
                financeDataService.getAccounts({ coaType: settingsData.coaType }),
                financeDataService.getAccountingBooks().catch(() => DEFAULT_ACCOUNTING_BOOKS),
                financeDataService.getFinanceDimensions(true).catch(() => {
                    setDimensionLoadError('Transaction-dimension filters could not be loaded.');
                    return [];
                }),
            ]);
            setSettings(settingsData);
            if (books.length > 0) setAccountingBooks(books);
            setAccounts(accountData.filter(account => account.status === 'Active'));
            setTransactionDimensions(dimensionOptions);
            const initialDimensionSelections = readFinanceDimensionSelections(searchParams, dimensionOptions);
            const initialDimensionFilters = buildFinanceDimensionFilters(dimensionOptions, initialDimensionSelections);
            setDimensionSelections(initialDimensionSelections);

            if (searchParams.has('accountIds') || searchParams.has('startDate') || searchParams.has('endDate')) {
                const data = await financeDataService.getDetailedLedger({
                    startDate,
                    endDate,
                    accountIds: selectedAccountIds,
                    bookClassification,
                    includeReversed,
                    includeOpeningBalances: parseBooleanParam(searchParams.get('includeOpeningBalances'), true),
                    dimensionFilters: initialDimensionFilters,
                });
                setReport(data);
                setAppliedDimensionFilters(initialDimensionFilters);
            }
        } catch (err) {
            console.error('Error loading detailed ledger setup data:', err);
            setError('Could not load accounts for the detailed ledger report.');
        } finally {
            setLoading(false);
        }
    };

    const filteredAccounts = useMemo(() => {
        const search = accountSearch.trim().toLowerCase();
        if (!search) return accounts;

        return accounts.filter(account =>
            account.accountCode.toLowerCase().includes(search) ||
            account.accountNumber.toLowerCase().includes(search) ||
            account.accountName.toLowerCase().includes(search)
        );
    }, [accountSearch, accounts]);

    const selectedAccountSummary = selectedAccountIds.length === 0
        ? 'All active GL accounts'
        : `${selectedAccountIds.length} selected account${selectedAccountIds.length === 1 ? '' : 's'}`;

    const toggleAccount = (accountId: string, checked: boolean) => {
        setSelectedAccountIds(current =>
            checked ? [...current, accountId] : current.filter(id => id !== accountId)
        );
    };

    const runReport = async () => {
        try {
            setRunning(true);
            setError(null);
            const dimensionFilters = buildFinanceDimensionFilters(transactionDimensions, dimensionSelections);
            const data = await financeDataService.getDetailedLedger({
                startDate,
                endDate,
                accountIds: selectedAccountIds,
                bookClassification,
                includeReversed,
                includeOpeningBalances: true,
                dimensionFilters,
            });
            setReport(data);
            setAppliedDimensionFilters(dimensionFilters);
        } catch (err) {
            console.error('Error loading detailed ledger report:', err);
            setError('Could not generate the detailed ledger report.');
        } finally {
            setRunning(false);
        }
    };

    const formatMoney = (amount: number) => new Intl.NumberFormat('en-GH', {
        style: 'decimal',
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    }).format(amount || 0);

    const formatBalance = (amount: number, type: string) => {
        if (!amount) return '-';
        return `${formatMoney(amount)} ${type}`;
    };

    const reportParameters = () => ({
        startDate,
        endDate,
        accountIds: selectedAccountIds,
        bookClassification,
        includeReversed,
        includeOpeningBalances: true,
        ...toFinanceDimensionFilterQueryParameters(appliedDimensionFilters),
    });

    const updateDimensionSelection = (definitionId: string, valueCode: string) => {
        setDimensionSelections((current) => ({ ...current, [definitionId]: valueCode }));
    };

    const printReport = async () => {
        try {
            setActionLoading('print');
            setError(null);
            await documentOutputService.printReportDocument(DOCUMENT_TYPES.financeDetailedLedger, reportParameters());
        } catch (err) {
            console.error('Error printing detailed ledger report:', err);
            setError('Could not print the detailed ledger report.');
        } finally {
            setActionLoading(null);
        }
    };

    const exportReport = async () => {
        try {
            setActionLoading('export');
            setError(null);
            await documentOutputService.downloadReportDocument(DOCUMENT_TYPES.financeDetailedLedger, reportParameters());
        } catch (err) {
            console.error('Error exporting detailed ledger report:', err);
            setError('Could not export the detailed ledger report.');
        } finally {
            setActionLoading(null);
        }
    };

    const visibleReportAccounts = report?.accounts.filter(account =>
        account.lines.length > 0 || account.openingBalance !== 0 || account.closingBalance !== 0
    ) ?? [];

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
                    <h1 className="text-3xl font-bold tracking-tight">Detailed Ledger</h1>
                    <p className="text-muted-foreground">
                        GL transaction listing by account and date range
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
                        <BreadcrumbPage>Detailed Ledger</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            <Card>
                <CardContent className="p-6">
                    <div className="grid grid-cols-1 gap-4 lg:grid-cols-5">
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
                        <div className="flex items-center gap-2 pt-8">
                            <Checkbox
                                id="include-reversed"
                                checked={includeReversed}
                                onCheckedChange={(value) => setIncludeReversed(Boolean(value))}
                            />
                            <Label htmlFor="include-reversed">Include reversed</Label>
                        </div>
                        <ReportDimensionFilters
                            definitions={transactionDimensions}
                            selections={dimensionSelections}
                            onSelectionChange={updateDimensionSelection}
                            disabled={running}
                        />
                        <Button className="mt-6" onClick={runReport} disabled={running}>
                            {running && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Run Report
                        </Button>
                    </div>
                    <AppliedReportDimensionFilters
                        definitions={transactionDimensions}
                        appliedFilters={appliedDimensionFilters}
                        pendingFilters={buildFinanceDimensionFilters(transactionDimensions, dimensionSelections)}
                    />
                    {dimensionLoadError && <div className="mt-4 text-sm text-amber-700">{dimensionLoadError}</div>}
                    {appliedDimensionFilters.length > 0 && (
                        <p className="mt-3 text-xs text-amber-700">
                            This ledger view includes only lines carrying the selected immutable transaction coding.
                        </p>
                    )}

                    <div className="mt-6 grid gap-4 lg:grid-cols-[320px_1fr]">
                        <div className="space-y-3">
                            <div className="flex items-center justify-between gap-3">
                                <Label>GL Accounts</Label>
                                <Button variant="ghost" size="sm" onClick={() => setSelectedAccountIds([])}>
                                    All
                                </Button>
                            </div>
                            <div className="relative">
                                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                                <Input
                                    className="pl-8"
                                    placeholder="Search code or name..."
                                    value={accountSearch}
                                    onChange={(event) => setAccountSearch(event.target.value)}
                                />
                            </div>
                            <div className="max-h-72 overflow-auto rounded-md border">
                                {filteredAccounts.map(account => (
                                    <label key={account.id} className="flex cursor-pointer items-start gap-3 border-b px-3 py-2 last:border-b-0 hover:bg-muted/40">
                                        <Checkbox
                                            checked={selectedAccountIds.includes(account.id)}
                                            onCheckedChange={(value) => toggleAccount(account.id, Boolean(value))}
                                            className="mt-1"
                                        />
                                        <span className="min-w-0">
                                            <span className="block truncate font-mono text-sm">{account.accountNumber || account.accountCode}</span>
                                            <span className="block truncate text-sm text-muted-foreground">{account.accountName}</span>
                                        </span>
                                    </label>
                                ))}
                            </div>
                        </div>
                        <div className="rounded-md border bg-muted/20 p-4">
                            <div className="text-sm text-muted-foreground">Selection</div>
                            <div className="mt-1 font-medium">{selectedAccountSummary}</div>
                            <div className="mt-4 grid grid-cols-2 gap-4 text-sm md:grid-cols-4">
                                <div>
                                    <div className="text-muted-foreground">From</div>
                                    <div className="font-medium">{new Date(startDate).toLocaleDateString()}</div>
                                </div>
                                <div>
                                    <div className="text-muted-foreground">To</div>
                                    <div className="font-medium">{new Date(endDate).toLocaleDateString()}</div>
                                </div>
                                <div>
                                    <div className="text-muted-foreground">Book</div>
                                    <div className="font-medium">{getAccountingBookName(accountingBooks, bookClassification)}</div>
                                </div>
                                <div>
                                    <div className="text-muted-foreground">Currency</div>
                                    <div className="font-medium">{settings?.baseCurrency || report?.currencyCode || 'GHS'}</div>
                                </div>
                            </div>
                            {error && <div className="mt-4 text-sm text-red-600">{error}</div>}
                        </div>
                    </div>
                </CardContent>
            </Card>

            <Card>
                <CardHeader className="border-b text-center">
                    <CardTitle>Detailed Ledger Report</CardTitle>
                    <p className="text-sm text-muted-foreground">
                        {report
                            ? `${new Date(report.startDate).toLocaleDateString()} to ${new Date(report.endDate).toLocaleDateString()}`
                            : 'Run the report to view transaction detail'}
                    </p>
                </CardHeader>
                <CardContent className="pt-6">
                    {!report ? (
                        <div className="py-16 text-center text-muted-foreground">No report generated</div>
                    ) : visibleReportAccounts.length === 0 ? (
                        <div className="py-16 text-center text-muted-foreground">No transactions found for the selected filters</div>
                    ) : (
                        <div className="space-y-8">
                            {visibleReportAccounts.map(account => (
                                <LedgerAccountSection key={account.accountId} account={account} formatMoney={formatMoney} formatBalance={formatBalance} />
                            ))}
                            <div className="flex justify-end border-t pt-4">
                                <div className="grid w-full max-w-md grid-cols-2 gap-3 text-sm">
                                    <div className="text-muted-foreground">Total Debits</div>
                                    <div className="text-right font-bold">{formatMoney(report.totalDebits)}</div>
                                    <div className="text-muted-foreground">Total Credits</div>
                                    <div className="text-right font-bold">{formatMoney(report.totalCredits)}</div>
                                </div>
                            </div>
                        </div>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}

function LedgerAccountSection({
    account,
    formatMoney,
    formatBalance,
}: {
    account: DetailedLedgerAccountDto;
    formatMoney: (amount: number) => string;
    formatBalance: (amount: number, type: string) => string;
}) {
    return (
        <div className="rounded-md border">
            <div className="flex flex-col gap-2 border-b bg-muted/30 px-4 py-3 md:flex-row md:items-center md:justify-between">
                <div>
                    <div className="font-mono text-sm font-semibold">{account.accountNumber || account.accountCode}</div>
                    <div className="text-sm text-muted-foreground">{account.accountName}</div>
                </div>
                <div className="grid grid-cols-2 gap-x-6 gap-y-1 text-sm md:grid-cols-4">
                    <span className="text-muted-foreground">Opening</span>
                    <span className="font-medium">{formatBalance(account.openingBalance, account.openingBalanceType)}</span>
                    <span className="text-muted-foreground">Closing</span>
                    <span className="font-medium">{formatBalance(account.closingBalance, account.closingBalanceType)}</span>
                </div>
            </div>
            <div className="overflow-x-auto">
                <Table>
                    <TableHeader>
                        <TableRow>
                            <TableHead className="w-[110px]">Date</TableHead>
                            <TableHead className="w-[130px]">Journal</TableHead>
                            <TableHead className="w-[130px]">Reference</TableHead>
                            <TableHead>Description</TableHead>
                            <TableHead>Transaction Dimensions</TableHead>
                            <TableHead className="w-[90px]">Source</TableHead>
                            <TableHead className="text-right">Debit</TableHead>
                            <TableHead className="text-right">Credit</TableHead>
                            <TableHead className="text-right">Balance</TableHead>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {account.lines.map(line => (
                            <TableRow key={line.transactionId}>
                                <TableCell>{new Date(line.transactionDate).toLocaleDateString()}</TableCell>
                                <TableCell className="font-mono text-sm">{line.journalEntryNumber}</TableCell>
                                <TableCell>{line.reference || '-'}</TableCell>
                                <TableCell>{line.description || '-'}</TableCell>
                                <TableCell className="text-xs">{line.financeDimensionDisplay || '-'}</TableCell>
                                <TableCell>{line.sourceModule || 'GL'}</TableCell>
                                <TableCell className="text-right">{line.debitAmount ? formatMoney(line.debitAmount) : '-'}</TableCell>
                                <TableCell className="text-right">{line.creditAmount ? formatMoney(line.creditAmount) : '-'}</TableCell>
                                <TableCell className="text-right font-medium">{formatBalance(line.runningBalance, line.runningBalanceType)}</TableCell>
                            </TableRow>
                        ))}
                        <TableRow className="bg-muted/30 font-bold">
                            <TableCell colSpan={6} className="text-right">Account Totals</TableCell>
                            <TableCell className="text-right">{formatMoney(account.totalDebits)}</TableCell>
                            <TableCell className="text-right">{formatMoney(account.totalCredits)}</TableCell>
                            <TableCell />
                        </TableRow>
                    </TableBody>
                </Table>
            </div>
        </div>
    );
}
