'use client';

import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { format } from 'date-fns';
import { AlertCircle, Loader2, Printer } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { formatCurrency } from '@/lib/utils';
import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import type { UnmatchedStatementLine, UnmatchedTransaction } from '@/types/cash-management';
import { CashTransactionType } from '@/types/cash-management';

function message(error: unknown, fallback: string) {
    return error instanceof Error && error.message ? error.message : fallback;
}

function transactionIsReceipt(item: UnmatchedTransaction) {
    return item.transactionType === CashTransactionType.Receipt ||
        item.transactionType === CashTransactionType.Deposit ||
        (item.transactionType === CashTransactionType.Transfer && item.transactionNumber.toUpperCase().endsWith('-IN'));
}

function total(items: Array<{ amount: number }>) {
    return items.reduce((sum, item) => sum + item.amount, 0);
}

export default function BankReconciliationReportPage() {
    const [bankAccountId, setBankAccountId] = useState('');
    const [reconciliationId, setReconciliationId] = useState('');

    const accountsQuery = useQuery({
        queryKey: ['bank-accounts'],
        queryFn: () => cashManagementDataService.getBankAccounts(),
    });
    const reconciliationsQuery = useQuery({
        queryKey: ['bank-account-reconciliations', bankAccountId],
        queryFn: () => cashManagementDataService.getBankReconciliations(bankAccountId),
        enabled: Boolean(bankAccountId),
    });
    const reconciliationQuery = useQuery({
        queryKey: ['bank-reconciliation', reconciliationId],
        queryFn: () => cashManagementDataService.getBankReconciliationById(reconciliationId),
        enabled: Boolean(reconciliationId),
    });
    const summaryQuery = useQuery({
        queryKey: ['reconciliation-summary', reconciliationId],
        queryFn: () => cashManagementDataService.getReconciliationSummary(reconciliationId),
        enabled: Boolean(reconciliationId),
    });
    const matchesQuery = useQuery({
        queryKey: ['reconciliation-matches', reconciliationId],
        queryFn: () => cashManagementDataService.getReconciliationMatches(reconciliationId),
        enabled: Boolean(reconciliationId),
    });
    const statementsQuery = useQuery({
        queryKey: ['bank-statements', bankAccountId],
        queryFn: () => cashManagementDataService.getBankStatements(bankAccountId),
        enabled: Boolean(bankAccountId),
    });

    const account = accountsQuery.data?.find(item => item.id === bankAccountId);
    const reconciliation = reconciliationQuery.data;
    const summary = summaryQuery.data;
    const statement = statementsQuery.data?.find(item => item.id === reconciliation?.statementId);
    const currency = account?.currency ?? '';

    const report = useMemo(() => {
        if (!summary) return null;
        const depositsInTransit = summary.unmatchedBookTransactions.filter(transactionIsReceipt);
        const outstandingPayments = summary.unmatchedBookTransactions.filter(item => !transactionIsReceipt(item));
        const bankCredits: UnmatchedStatementLine[] = summary.unmatchedStatementLines
            .filter(item => item.creditAmount > 0)
            .map(item => ({ ...item, amount: item.creditAmount }));
        const bankDebits: UnmatchedStatementLine[] = summary.unmatchedStatementLines
            .filter(item => item.debitAmount > 0)
            .map(item => ({ ...item, amount: item.debitAmount }));
        const adjustedBankBalance = summary.statementBalance + total(depositsInTransit) - total(outstandingPayments);
        const adjustedBookBalance = summary.bookBalance + total(bankCredits) - total(bankDebits);
        return {
            depositsInTransit,
            outstandingPayments,
            bankCredits,
            bankDebits,
            adjustedBankBalance,
            adjustedBookBalance,
            remainingDifference: adjustedBankBalance - adjustedBookBalance,
        };
    }, [summary]);

    const loading = reconciliationId && (reconciliationQuery.isLoading || summaryQuery.isLoading || matchesQuery.isLoading);
    const reportError = reconciliationQuery.error ?? summaryQuery.error ?? matchesQuery.error;

    return (
        <div className="mx-auto max-w-[1400px] space-y-6 p-8 print:max-w-none print:p-0">
            <div className="print:hidden">
                <h1 className="text-3xl font-bold tracking-tight">Bank Reconciliation Report</h1>
                <p className="text-muted-foreground">Run and print a reconciliation statement for an individual bank account and period.</p>
            </div>

            <Card className="print:hidden">
                <CardHeader>
                    <CardTitle>Report parameters</CardTitle>
                    <CardDescription>Select a bank account and reconciliation period.</CardDescription>
                </CardHeader>
                <CardContent className="grid gap-4 md:grid-cols-[1fr_1fr_auto] md:items-end">
                    <div className="space-y-2">
                        <label className="text-sm font-medium">Bank account</label>
                        <Select value={bankAccountId} onValueChange={value => { setBankAccountId(value); setReconciliationId(''); }}>
                            <SelectTrigger><SelectValue placeholder="Select bank account" /></SelectTrigger>
                            <SelectContent>
                                {accountsQuery.data?.map(item => (
                                    <SelectItem key={item.id} value={item.id}>{item.accountName} · {item.bankName} · {item.currency}</SelectItem>
                                ))}
                            </SelectContent>
                        </Select>
                    </div>
                    <div className="space-y-2">
                        <label className="text-sm font-medium">Reconciliation period</label>
                        <Select value={reconciliationId} onValueChange={setReconciliationId} disabled={!bankAccountId || reconciliationsQuery.isLoading}>
                            <SelectTrigger><SelectValue placeholder="Select reconciliation" /></SelectTrigger>
                            <SelectContent>
                                {reconciliationsQuery.data?.map(item => (
                                    <SelectItem key={item.id} value={item.id}>
                                        {format(new Date(item.reconciliationDate), 'dd MMM yyyy')} · {item.status}
                                    </SelectItem>
                                ))}
                            </SelectContent>
                        </Select>
                    </div>
                    <Button onClick={() => window.print()} disabled={!report}>
                        <Printer className="mr-2 h-4 w-4" />Print report
                    </Button>
                </CardContent>
            </Card>

            {loading ? <div className="flex justify-center p-16"><Loader2 className="h-8 w-8 animate-spin" /></div> : null}
            {reportError ? (
                <Card className="border-destructive/40 print:hidden">
                    <CardContent className="flex items-center gap-3 p-6 text-destructive">
                        <AlertCircle className="h-5 w-5" />
                        <span>{message(reportError, 'Unable to run the bank reconciliation report.')}</span>
                    </CardContent>
                </Card>
            ) : null}

            {reconciliation && summary && report ? (
                <Card className="print:border-0 print:shadow-none">
                    <CardContent className="space-y-8 p-8 print:p-0">
                        <header className="border-b pb-5 text-center">
                            <h2 className="text-2xl font-bold">BANK RECONCILIATION STATEMENT</h2>
                            <p className="mt-2 text-lg font-semibold">{account?.accountName}</p>
                            <p className="text-sm text-muted-foreground">{account?.bankName} · {account?.accountNumber} · {currency}</p>
                            <p className="text-sm">As at {format(new Date(reconciliation.reconciliationDate), 'dd MMMM yyyy')}</p>
                            <Badge className="mt-3" variant={reconciliation.status === 'Approved' ? 'default' : 'outline'}>{reconciliation.status}</Badge>
                        </header>

                        <div className="grid gap-4 sm:grid-cols-3">
                            <ReportMetric label="Opening balance per statement" value={statement?.openingBalance} currency={currency} />
                            <ReportMetric label="Closing balance per statement" value={summary.statementBalance} currency={currency} />
                            <ReportMetric label="Closing balance per GL" value={summary.bookBalance} currency={currency} />
                        </div>

                        <section>
                            <h3 className="mb-3 text-lg font-semibold">A. Adjustment of bank statement balance</h3>
                            <ReconciliationSection
                                openingLabel="Balance per bank statement"
                                openingValue={summary.statementBalance}
                                additionsLabel="Add: deposits in transit / receipts recorded in the GL but not on the statement"
                                additions={report.depositsInTransit}
                                deductionsLabel="Less: outstanding payments / unpresented items"
                                deductions={report.outstandingPayments}
                                closingLabel="Adjusted bank balance"
                                closingValue={report.adjustedBankBalance}
                                currency={currency}
                            />
                        </section>

                        <section>
                            <h3 className="mb-3 text-lg font-semibold">B. Adjustment of general ledger balance</h3>
                            <ReconciliationSection
                                openingLabel="Balance per general ledger"
                                openingValue={summary.bookBalance}
                                additionsLabel="Add: bank credits not yet recorded in the GL"
                                additions={report.bankCredits}
                                deductionsLabel="Less: bank debits, charges, direct debits or returned items not yet recorded in the GL"
                                deductions={report.bankDebits}
                                closingLabel="Adjusted book balance"
                                closingValue={report.adjustedBookBalance}
                                currency={currency}
                            />
                        </section>

                        <div className={`rounded-lg border p-5 ${Math.abs(report.remainingDifference) < 0.005 ? 'border-emerald-300 bg-emerald-50' : 'border-red-300 bg-red-50'}`}>
                            <div className="flex items-center justify-between text-lg font-bold">
                                <span>Remaining reconciliation difference</span>
                                <span>{formatCurrency(report.remainingDifference, currency)}</span>
                            </div>
                            <p className="mt-1 text-sm">Adjusted bank balance less adjusted book balance. This must be zero before final approval.</p>
                        </div>

                        <section>
                            <h3 className="mb-3 text-lg font-semibold">Matched transaction evidence</h3>
                            <Table>
                                <TableHeader><TableRow><TableHead>Book transaction</TableHead><TableHead>Statement line</TableHead><TableHead>Method</TableHead><TableHead className="text-right">Amount</TableHead></TableRow></TableHeader>
                                <TableBody>
                                    {(matchesQuery.data ?? []).map(item => (
                                        <TableRow key={item.id}>
                                            <TableCell>{item.cashTransactionDescription || item.cashTransactionNumber}<div className="text-xs text-muted-foreground">{format(new Date(item.cashTransactionDate), 'dd MMM yyyy')} · {item.cashTransactionReference || item.cashTransactionNumber}</div></TableCell>
                                            <TableCell>{item.statementDescription || 'Bank statement line'}<div className="text-xs text-muted-foreground">{format(new Date(item.statementTransactionDate), 'dd MMM yyyy')} · {item.statementReference || 'No reference'}</div></TableCell>
                                            <TableCell>{item.isAutoMatched ? `Auto ${item.matchConfidence ?? ''}%` : 'Manual'}</TableCell>
                                            <TableCell className="text-right">{formatCurrency(item.cashTransactionAmount, currency)}</TableCell>
                                        </TableRow>
                                    ))}
                                </TableBody>
                            </Table>
                        </section>

                        <footer className="grid gap-8 border-t pt-8 sm:grid-cols-2">
                            <SignOff label="Prepared / finalized by" user={reconciliation.reconciledByName || reconciliation.reconciledBy} date={reconciliation.reconciledAt} />
                            <SignOff label="Final approval by" user={reconciliation.approvedByName || reconciliation.approvedBy} date={reconciliation.approvedAt} />
                        </footer>
                    </CardContent>
                </Card>
            ) : !loading && !reportError ? (
                <Card className="print:hidden"><CardContent className="p-10 text-center text-muted-foreground">Select report parameters to run the report.</CardContent></Card>
            ) : null}
        </div>
    );
}

function ReportMetric({ label, value, currency }: { label: string; value?: number; currency: string }) {
    return <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">{label}</div><div className="mt-1 text-xl font-bold">{value === undefined ? 'Not available' : formatCurrency(value, currency)}</div></div>;
}

function ReconciliationSection({ openingLabel, openingValue, additionsLabel, additions, deductionsLabel, deductions, closingLabel, closingValue, currency }: {
    openingLabel: string; openingValue: number; additionsLabel: string; additions: Array<{ id: string; description: string; referenceNumber?: string; amount: number }>;
    deductionsLabel: string; deductions: Array<{ id: string; description: string; referenceNumber?: string; amount: number }>; closingLabel: string; closingValue: number; currency: string;
}) {
    return (
        <div className="overflow-hidden rounded-lg border">
            <Line label={openingLabel} value={openingValue} currency={currency} strong />
            <div className="border-t bg-muted/40 px-4 py-2 text-sm font-semibold">{additionsLabel}</div>
            {additions.length ? additions.map(item => <Line key={item.id} label={`${item.description}${item.referenceNumber ? ` · ${item.referenceNumber}` : ''}`} value={item.amount} currency={currency} />) : <EmptyLine />}
            <div className="border-t bg-muted/40 px-4 py-2 text-sm font-semibold">{deductionsLabel}</div>
            {deductions.length ? deductions.map(item => <Line key={item.id} label={`${item.description}${item.referenceNumber ? ` · ${item.referenceNumber}` : ''}`} value={-item.amount} currency={currency} />) : <EmptyLine />}
            <Line label={closingLabel} value={closingValue} currency={currency} strong topBorder />
        </div>
    );
}

function Line({ label, value, currency, strong = false, topBorder = false }: { label: string; value: number; currency: string; strong?: boolean; topBorder?: boolean }) {
    return <div className={`flex justify-between gap-4 px-4 py-2 text-sm ${topBorder ? 'border-t' : ''} ${strong ? 'font-bold' : ''}`}><span>{label}</span><span className="whitespace-nowrap">{formatCurrency(value, currency)}</span></div>;
}

function EmptyLine() {
    return <div className="px-4 py-2 text-sm text-muted-foreground">None</div>;
}

function SignOff({ label, user, date }: { label: string; user?: string; date?: string }) {
    return <div><div className="border-b pb-8" /><div className="mt-2 text-sm font-semibold">{label}</div><div className="text-sm text-muted-foreground">{user || 'Pending'}{date ? ` · ${format(new Date(date), 'dd MMM yyyy HH:mm')}` : ''}</div></div>;
}
