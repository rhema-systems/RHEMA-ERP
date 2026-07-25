'use client';

import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import {
    ArrowLeft,
    Building2,
    CreditCard,
    FileText,
    Loader2,
    Receipt,
    RefreshCw,
    TrendingDown,
    TrendingUp,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import type { BankAccount, BankReconciliation, BankStatement, CashTransaction } from '@/types/cash-management';
import { formatCurrency } from '@/lib/utils';

function getParamId(value: string | string[] | undefined) {
    return Array.isArray(value) ? value[0] : value || '';
}

function formatDate(value?: string | null) {
    if (!value) return '-';
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? '-' : date.toLocaleDateString();
}

function formatMoney(amount: number, currency = 'GHS') {
    return formatCurrency(amount || 0, currency || 'GHS');
}

function statusBadge(account: BankAccount) {
    return account.isActive ? (
        <Badge variant="outline" className="bg-green-50 text-green-700 border-green-200">Active</Badge>
    ) : (
        <Badge variant="outline" className="bg-gray-50 text-gray-700 border-gray-200">Inactive</Badge>
    );
}

function reconciliationVariant(status?: string): 'default' | 'secondary' | 'destructive' | 'outline' {
    switch ((status || '').toLowerCase()) {
        case 'completed':
        case 'approved':
            return 'default';
        case 'rejected':
            return 'destructive';
        case 'inprogress':
        case 'pending':
            return 'secondary';
        default:
            return 'outline';
    }
}

export default function BankAccountDetailsPage() {
    const router = useRouter();
    const params = useParams();
    const accountId = getParamId(params?.id);

    const accountQuery = useQuery({
        queryKey: ['bank-account', accountId],
        queryFn: () => cashManagementDataService.getBankAccountById(accountId),
        enabled: Boolean(accountId),
    });

    const transactionsQuery = useQuery({
        queryKey: ['bank-account-transactions', accountId],
        queryFn: () => cashManagementDataService.getTransactionsByBankAccount(accountId),
        enabled: Boolean(accountId),
    });

    const statementsQuery = useQuery({
        queryKey: ['bank-account-statements', accountId],
        queryFn: () => cashManagementDataService.getBankStatements(accountId),
        enabled: Boolean(accountId),
    });

    const reconciliationsQuery = useQuery({
        queryKey: ['bank-account-reconciliations', accountId],
        queryFn: () => cashManagementDataService.getBankReconciliations(accountId),
        enabled: Boolean(accountId),
    });

    const account = accountQuery.data;
    const transactions = (transactionsQuery.data ?? [])
        .slice()
        .sort((a, b) => new Date(b.transactionDate).getTime() - new Date(a.transactionDate).getTime())
        .slice(0, 10);
    const statements = (statementsQuery.data ?? [])
        .slice()
        .sort((a, b) => new Date(b.statementDate).getTime() - new Date(a.statementDate).getTime())
        .slice(0, 5);
    const reconciliations = (reconciliationsQuery.data ?? [])
        .slice()
        .sort((a, b) => new Date(b.reconciliationDate).getTime() - new Date(a.reconciliationDate).getTime())
        .slice(0, 5);

    if (accountQuery.isLoading) {
        return (
            <div className="flex min-h-[420px] items-center justify-center">
                <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
            </div>
        );
    }

    if (accountQuery.isError || !account) {
        return (
            <div className="p-8 max-w-3xl mx-auto space-y-6">
                <Button variant="ghost" onClick={() => router.push('/finance/cash/accounts')}>
                    <ArrowLeft className="mr-2 h-4 w-4" />
                    Bank Accounts
                </Button>
                <Card>
                    <CardHeader>
                        <CardTitle>Bank Account Not Found</CardTitle>
                        <CardDescription>
                            The selected bank account could not be loaded. It may have been deleted or you may not have access to it.
                        </CardDescription>
                    </CardHeader>
                </Card>
            </div>
        );
    }

    const receiptTotal = transactions
        .filter((transaction) => transaction.transactionType === 'Receipt')
        .reduce((sum, transaction) => sum + transaction.amount, 0);
    const paymentTotal = transactions
        .filter((transaction) => transaction.transactionType === 'Payment')
        .reduce((sum, transaction) => sum + transaction.amount, 0);
    const unreconciledCount = transactions.filter((transaction) => !transaction.isReconciled).length;

    return (
        <div className="p-8 max-w-[1400px] mx-auto space-y-6">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
                <div className="flex items-start gap-4">
                    <Button variant="ghost" size="icon" onClick={() => router.push('/finance/cash/accounts')}>
                        <ArrowLeft className="h-4 w-4" />
                    </Button>
                    <div>
                        <div className="flex flex-wrap items-center gap-3">
                            <h1 className="text-3xl font-bold tracking-tight">{account.accountName}</h1>
                            {statusBadge(account)}
                            <Badge variant="outline">{account.accountType}</Badge>
                        </div>
                        <p className="text-muted-foreground">
                            {account.bankName}{account.bankBranch ? ` - ${account.bankBranch}` : ''}
                        </p>
                    </div>
                </div>
                <div className="flex flex-wrap gap-2">
                    <Button variant="outline" asChild>
                        <Link href={`/finance/cash/reconciliation?account=${account.id}`}>
                            <RefreshCw className="mr-2 h-4 w-4" />
                            Reconcile
                        </Link>
                    </Button>
                    <Button variant="outline" asChild>
                        <Link href={`/finance/cash/transactions/receipts?bankAccountId=${account.id}`}>
                            <Receipt className="mr-2 h-4 w-4" />
                            Receipt
                        </Link>
                    </Button>
                    <Button variant="outline" asChild>
                        <Link href={`/finance/cash/transactions/payments?bankAccountId=${account.id}`}>
                            <CreditCard className="mr-2 h-4 w-4" />
                            Payment
                        </Link>
                    </Button>
                </div>
            </div>

            {/* Bank balances shown here are read-side snapshots; posted GL/reconciliation reports remain the accounting source of truth. */}
            <div className="grid gap-4 md:grid-cols-4">
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">Current Balance</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{formatMoney(account.currentBalance, account.currency)}</div>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">Available Balance</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{formatMoney(account.availableBalance, account.currency)}</div>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">Opening Balance</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{formatMoney(account.openingBalance, account.currency)}</div>
                        <p className="mt-1 text-xs text-muted-foreground">As of {formatDate(account.openingDate)}</p>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">Unreconciled</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{unreconciledCount}</div>
                        <p className="mt-1 text-xs text-muted-foreground">Recent transactions</p>
                    </CardContent>
                </Card>
            </div>

            <div className="grid gap-6 lg:grid-cols-[380px_minmax(0,1fr)]">
                <div className="space-y-6">
                    <Card>
                        <CardHeader>
                            <CardTitle className="flex items-center gap-2">
                                <Building2 className="h-5 w-5" />
                                Account Information
                            </CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-3 text-sm">
                            <InfoRow label="Account Number" value={account.accountNumber} mono />
                            <InfoRow label="Currency" value={account.currency} />
                            <InfoRow label="Type" value={account.accountType} />
                            <InfoRow label="Bank" value={account.bankName} />
                            <InfoRow label="Branch" value={account.bankBranch || '-'} />
                            <InfoRow label="Created" value={formatDate(account.createdAt)} />
                            {account.glAccountId && (
                                <div className="flex items-start justify-between gap-4 border-t pt-3">
                                    <span className="text-muted-foreground">GL Account</span>
                                    <Link className="text-right font-medium text-primary hover:underline" href={`/finance/accounts/${account.glAccountId}`}>
                                        {account.glAccountNumber || account.glAccountName || 'View GL account'}
                                    </Link>
                                </div>
                            )}
                            {account.notes && (
                                <div className="border-t pt-3">
                                    <div className="text-muted-foreground">Notes</div>
                                    <p className="mt-1">{account.notes}</p>
                                </div>
                            )}
                        </CardContent>
                    </Card>

                    <Card>
                        <CardHeader>
                            <CardTitle>Recent Activity</CardTitle>
                            <CardDescription>Based on the latest loaded account transactions.</CardDescription>
                        </CardHeader>
                        <CardContent className="grid grid-cols-2 gap-4">
                            <div className="rounded-md border p-3">
                                <div className="flex items-center gap-2 text-sm text-muted-foreground">
                                    <TrendingUp className="h-4 w-4 text-green-600" />
                                    Receipts
                                </div>
                                <div className="mt-1 font-semibold">{formatMoney(receiptTotal, account.currency)}</div>
                            </div>
                            <div className="rounded-md border p-3">
                                <div className="flex items-center gap-2 text-sm text-muted-foreground">
                                    <TrendingDown className="h-4 w-4 text-red-600" />
                                    Payments
                                </div>
                                <div className="mt-1 font-semibold">{formatMoney(paymentTotal, account.currency)}</div>
                            </div>
                        </CardContent>
                    </Card>
                </div>

                <div className="space-y-6">
                    <RecentTransactions transactions={transactions} currency={account.currency} isLoading={transactionsQuery.isLoading} />
                    <div className="grid gap-6 xl:grid-cols-2">
                        <RecentStatements statements={statements} currency={account.currency} isLoading={statementsQuery.isLoading} />
                        <RecentReconciliations reconciliations={reconciliations} currency={account.currency} isLoading={reconciliationsQuery.isLoading} />
                    </div>
                </div>
            </div>
        </div>
    );
}

function InfoRow({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
    return (
        <div className="flex items-center justify-between gap-4">
            <span className="text-muted-foreground">{label}</span>
            <span className={mono ? 'font-mono font-medium' : 'font-medium'}>{value}</span>
        </div>
    );
}

function EmptyRows({ colSpan, label }: { colSpan: number; label: string }) {
    return (
        <TableRow>
            <TableCell colSpan={colSpan} className="py-8 text-center text-muted-foreground">
                {label}
            </TableCell>
        </TableRow>
    );
}

function LoadingRows({ colSpan }: { colSpan: number }) {
    return (
        <TableRow>
            <TableCell colSpan={colSpan} className="py-8 text-center text-muted-foreground">
                <Loader2 className="mx-auto h-5 w-5 animate-spin" />
            </TableCell>
        </TableRow>
    );
}

function RecentTransactions({
    transactions,
    currency,
    isLoading,
}: {
    transactions: CashTransaction[];
    currency: string;
    isLoading: boolean;
}) {
    return (
        <Card>
            <CardHeader className="flex flex-row items-center justify-between">
                <div>
                    <CardTitle>Recent Transactions</CardTitle>
                    <CardDescription>Latest cash movements for this account.</CardDescription>
                </div>
                <Button variant="outline" size="sm" asChild>
                    <Link href="/finance/cash/transactions">View All</Link>
                </Button>
            </CardHeader>
            <CardContent>
                <Table>
                    <TableHeader>
                        <TableRow>
                            <TableHead>Date</TableHead>
                            <TableHead>Type</TableHead>
                            <TableHead>Reference</TableHead>
                            <TableHead>Description</TableHead>
                            <TableHead className="text-right">Amount</TableHead>
                            <TableHead>Status</TableHead>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {isLoading ? (
                            <LoadingRows colSpan={6} />
                        ) : transactions.length === 0 ? (
                            <EmptyRows colSpan={6} label="No transactions found for this account." />
                        ) : (
                            transactions.map((transaction) => (
                                <TableRow key={transaction.id}>
                                    <TableCell>{formatDate(transaction.transactionDate)}</TableCell>
                                    <TableCell>{transaction.transactionType}</TableCell>
                                    <TableCell className="font-mono text-xs">{transaction.referenceNumber || transaction.transactionNumber}</TableCell>
                                    <TableCell>{transaction.description || transaction.payeeOrPayer || '-'}</TableCell>
                                    <TableCell className="text-right font-medium">{formatMoney(transaction.amount, transaction.currency || currency)}</TableCell>
                                    <TableCell>
                                        <Badge variant={transaction.isReconciled ? 'default' : 'outline'}>
                                            {transaction.isReconciled ? 'Reconciled' : 'Open'}
                                        </Badge>
                                    </TableCell>
                                </TableRow>
                            ))
                        )}
                    </TableBody>
                </Table>
            </CardContent>
        </Card>
    );
}

function RecentStatements({
    statements,
    currency,
    isLoading,
}: {
    statements: BankStatement[];
    currency: string;
    isLoading: boolean;
}) {
    return (
        <Card>
            <CardHeader>
                <CardTitle>Bank Statements</CardTitle>
                <CardDescription>Recently imported statements.</CardDescription>
            </CardHeader>
            <CardContent>
                <Table>
                    <TableHeader>
                        <TableRow>
                            <TableHead>Date</TableHead>
                            <TableHead>Number</TableHead>
                            <TableHead className="text-right">Closing</TableHead>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {isLoading ? (
                            <LoadingRows colSpan={3} />
                        ) : statements.length === 0 ? (
                            <EmptyRows colSpan={3} label="No statements imported." />
                        ) : (
                            statements.map((statement) => (
                                <TableRow key={statement.id}>
                                    <TableCell>{formatDate(statement.statementDate)}</TableCell>
                                    <TableCell className="font-mono text-xs">{statement.statementNumber || '-'}</TableCell>
                                    <TableCell className="text-right font-medium">{formatMoney(statement.closingBalance, currency)}</TableCell>
                                </TableRow>
                            ))
                        )}
                    </TableBody>
                </Table>
            </CardContent>
        </Card>
    );
}

function RecentReconciliations({
    reconciliations,
    currency,
    isLoading,
}: {
    reconciliations: BankReconciliation[];
    currency: string;
    isLoading: boolean;
}) {
    return (
        <Card>
            <CardHeader>
                <CardTitle>Reconciliations</CardTitle>
                <CardDescription>Latest reconciliation runs.</CardDescription>
            </CardHeader>
            <CardContent>
                <Table>
                    <TableHeader>
                        <TableRow>
                            <TableHead>Date</TableHead>
                            <TableHead>Status</TableHead>
                            <TableHead className="text-right">Difference</TableHead>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {isLoading ? (
                            <LoadingRows colSpan={3} />
                        ) : reconciliations.length === 0 ? (
                            <EmptyRows colSpan={3} label="No reconciliations found." />
                        ) : (
                            reconciliations.map((reconciliation) => (
                                <TableRow key={reconciliation.id}>
                                    <TableCell>{formatDate(reconciliation.reconciliationDate)}</TableCell>
                                    <TableCell>
                                        <Badge variant={reconciliationVariant(reconciliation.status)}>
                                            {reconciliation.status}
                                        </Badge>
                                    </TableCell>
                                    <TableCell className="text-right font-medium">{formatMoney(reconciliation.difference, currency)}</TableCell>
                                </TableRow>
                            ))
                        )}
                    </TableBody>
                </Table>
                <Button variant="outline" className="mt-4 w-full" asChild>
                    <Link href="/finance/cash/reconciliation">
                        <FileText className="mr-2 h-4 w-4" />
                        Open Reconciliation
                    </Link>
                </Button>
            </CardContent>
        </Card>
    );
}
