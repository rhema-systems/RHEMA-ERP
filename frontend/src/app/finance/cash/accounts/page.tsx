'use client';

import { useEffect, useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import type { BankAccount, CashPositionSummary } from '@/types/cash-management';
import { Building2, Plus, TrendingUp, TrendingDown, DollarSign, CreditCard } from 'lucide-react';
import Link from 'next/link';
import { useAuth } from '@/hooks/use-auth';

export default function BankAccountsPage() {
    const { hasPermission } = useAuth();
    const canManageBankAccounts = hasPermission('Finance.BankAccounts.Manage');
    const canRecordCashBankTransactions = hasPermission('Finance.CashBank.Transactions.Record');
    const canPerformReconciliation = hasPermission('Finance.BankReconciliation.Perform');
    const [accounts, setAccounts] = useState<BankAccount[]>([]);
    const [cashPosition, setCashPosition] = useState<CashPositionSummary | null>(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        loadData();
    }, []);

    const loadData = async () => {
        try {
            const accountsData = await cashManagementDataService.getBankAccounts();
            setAccounts(accountsData);
            setCashPosition(buildCashPositionSummary(accountsData));
        } catch (error) {
            console.error('Failed to load bank accounts:', error);
        } finally {
            setLoading(false);
        }
    };

    const formatCurrency = (amount: number, currency: string = 'GHS') => {
        return `${currency} ${amount.toLocaleString('en-GH', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    };

    const getAccountTypeIcon = (type: string) => {
        switch (type) {
            case 'Checking': return <Building2 className="h-5 w-5" />;
            case 'Savings': return <TrendingUp className="h-5 w-5" />;
            case 'Credit': return <CreditCard className="h-5 w-5" />;
            default: return <DollarSign className="h-5 w-5" />;
        }
    };

    const getAccountTypeColor = (type: string) => {
        switch (type) {
            case 'Checking': return 'bg-blue-50 text-blue-700 border-blue-200';
            case 'Savings': return 'bg-green-50 text-green-700 border-green-200';
            case 'Credit': return 'bg-orange-50 text-orange-700 border-orange-200';
            default: return 'bg-gray-50 text-gray-700 border-gray-200';
        }
    };

    const buildCashPositionSummary = (bankAccounts: BankAccount[]): CashPositionSummary => {
        const activeAccounts = bankAccounts.filter((account) => account.isActive);
        const currency = activeAccounts.find((account) => account.currency)?.currency ?? 'GHS';
        const byAccountType = Array.from(
            activeAccounts.reduce((groups, account) => {
                const current = groups.get(account.accountType) ?? { type: account.accountType, balance: 0, count: 0 };
                current.balance += account.currentBalance;
                current.count += 1;
                groups.set(account.accountType, current);
                return groups;
            }, new Map<BankAccount['accountType'], CashPositionSummary['byAccountType'][number]>())
        ).map(([, summary]) => summary);
        const byCurrency = Array.from(
            activeAccounts.reduce((groups, account) => {
                const current = groups.get(account.currency) ?? { currency: account.currency, balance: 0, count: 0 };
                current.balance += account.currentBalance;
                current.count += 1;
                groups.set(account.currency, current);
                return groups;
            }, new Map<string, CashPositionSummary['byCurrency'][number]>())
        ).map(([, summary]) => summary);

        return {
            asOfDate: new Date().toISOString(),
            totalBalance: activeAccounts.reduce((total, account) => total + account.currentBalance, 0),
            currency,
            accountCount: activeAccounts.length,
            byAccountType,
            byCurrency,
        };
    };

    return (
        <div className="p-6 space-y-6">
            {/* Header */}
            <div className="flex justify-between items-center">
                <div>
                    <h1 className="text-3xl font-bold">Bank Accounts</h1>
                    <p className="text-muted-foreground">Manage your bank accounts and cash positions</p>
                </div>
                <div className="flex gap-2">
                    <Link href="/finance/cash/transactions">
                        <Button variant="outline">View Transactions</Button>
                    </Link>
                    {canManageBankAccounts && <Link href="/finance/cash/accounts/new">
                        <Button>
                            <Plus className="mr-2 h-4 w-4" />
                            Add Bank Account
                        </Button>
                    </Link>}
                </div>
            </div>

            {/* Summary Cards */}
            {cashPosition && (
                <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                    <Card>
                        <CardHeader className="pb-2">
                            <CardTitle className="text-sm font-medium text-muted-foreground">Total Balance</CardTitle>
                        </CardHeader>
                        <CardContent>
                            <div className="text-2xl font-bold">{formatCurrency(cashPosition.totalBalance)}</div>
                            <p className="text-xs text-muted-foreground mt-1">Across all accounts</p>
                        </CardContent>
                    </Card>
                    <Card>
                        <CardHeader className="pb-2">
                            <CardTitle className="text-sm font-medium text-muted-foreground">Active Accounts</CardTitle>
                        </CardHeader>
                        <CardContent>
                            <div className="text-2xl font-bold">{cashPosition.accountCount}</div>
                            <p className="text-xs text-muted-foreground mt-1">Bank accounts</p>
                        </CardContent>
                    </Card>
                    <Card>
                        <CardHeader className="pb-2">
                            <CardTitle className="text-sm font-medium text-muted-foreground">Checking Accounts</CardTitle>
                        </CardHeader>
                        <CardContent>
                            <div className="text-2xl font-bold">
                                {formatCurrency(cashPosition.byAccountType.find(t => t.type === 'Checking')?.balance || 0)}
                            </div>
                            <p className="text-xs text-muted-foreground mt-1">
                                {cashPosition.byAccountType.find(t => t.type === 'Checking')?.count || 0} accounts
                            </p>
                        </CardContent>
                    </Card>
                    <Card>
                        <CardHeader className="pb-2">
                            <CardTitle className="text-sm font-medium text-muted-foreground">Savings Accounts</CardTitle>
                        </CardHeader>
                        <CardContent>
                            <div className="text-2xl font-bold">
                                {formatCurrency(cashPosition.byAccountType.find(t => t.type === 'Savings')?.balance || 0)}
                            </div>
                            <p className="text-xs text-muted-foreground mt-1">
                                {cashPosition.byAccountType.find(t => t.type === 'Savings')?.count || 0} accounts
                            </p>
                        </CardContent>
                    </Card>
                </div>
            )}

            {/* Bank Accounts List */}
            <Card>
                <CardHeader>
                    <CardTitle>All Bank Accounts</CardTitle>
                    <CardDescription>{accounts.length} accounts configured</CardDescription>
                </CardHeader>
                <CardContent>
                    {loading ? (
                        <div className="text-center py-8 text-muted-foreground">Loading...</div>
                    ) : accounts.length === 0 ? (
                        <div className="text-center py-8 text-muted-foreground">
                            No bank accounts found. Click "Add Bank Account" to create one.
                        </div>
                    ) : (
                        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                            {accounts.map(account => (
                                <Card key={account.id} className="hover:shadow-lg transition-shadow">
                                    <CardHeader className="pb-3">
                                        <div className="flex items-start justify-between">
                                            <div className="flex items-center gap-2">
                                                {getAccountTypeIcon(account.accountType)}
                                                <div>
                                                    <CardTitle className="text-lg">{account.accountName}</CardTitle>
                                                    <CardDescription className="text-sm">
                                                        {account.bankName}
                                                        {account.bankBranch && ` - ${account.bankBranch}`}
                                                    </CardDescription>
                                                </div>
                                            </div>
                                            {account.isActive ? (
                                                <Badge variant="outline" className="bg-green-50 text-green-700 border-green-200">
                                                    Active
                                                </Badge>
                                            ) : (
                                                <Badge variant="outline" className="bg-gray-50 text-gray-700 border-gray-200">
                                                    Inactive
                                                </Badge>
                                            )}
                                        </div>
                                    </CardHeader>
                                    <CardContent className="space-y-3">
                                        <div>
                                            <p className="text-xs text-muted-foreground">Account Number</p>
                                            <p className="font-mono text-sm">{account.accountNumber}</p>
                                        </div>
                                        <div className="flex items-center justify-between">
                                            <div>
                                                <p className="text-xs text-muted-foreground">Current Balance</p>
                                                <p className="text-xl font-bold">
                                                    {formatCurrency(account.currentBalance, account.currency)}
                                                </p>
                                            </div>
                                            <Badge variant="outline" className={getAccountTypeColor(account.accountType)}>
                                                {account.accountType}
                                            </Badge>
                                        </div>
                                        <div>
                                            <p className="text-xs text-muted-foreground">Available Balance</p>
                                            <p className="text-sm font-medium">
                                                {formatCurrency(account.availableBalance, account.currency)}
                                            </p>
                                        </div>
                                        <div className="flex gap-2 pt-2">
                                            <Link href={`/finance/cash/accounts/${account.id}`} className="flex-1">
                                                <Button variant="outline" size="sm" className="w-full">
                                                    View Details
                                                </Button>
                                            </Link>
                                            {canPerformReconciliation && <Link href={`/finance/cash/reconciliation?account=${account.id}`} className="flex-1">
                                                <Button variant="outline" size="sm" className="w-full">
                                                    Reconcile
                                                </Button>
                                            </Link>}
                                        </div>
                                    </CardContent>
                                </Card>
                            ))}
                        </div>
                    )}
                </CardContent>
            </Card>

            {/* Quick Links */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                {canRecordCashBankTransactions && <Link href="/finance/cash/transactions/receipts">
                    <Card className="hover:bg-accent cursor-pointer transition-colors">
                        <CardContent className="p-6">
                            <div className="flex items-center gap-3">
                                <div className="h-12 w-12 rounded-full bg-green-100 flex items-center justify-center">
                                    <TrendingUp className="h-6 w-6 text-green-600" />
                                </div>
                                <div>
                                    <h3 className="font-semibold">Record Receipt</h3>
                                    <p className="text-sm text-muted-foreground">Cash or bank receipt</p>
                                </div>
                            </div>
                        </CardContent>
                    </Card>
                </Link>}
                {canRecordCashBankTransactions && <Link href="/finance/cash/transactions/payments">
                    <Card className="hover:bg-accent cursor-pointer transition-colors">
                        <CardContent className="p-6">
                            <div className="flex items-center gap-3">
                                <div className="h-12 w-12 rounded-full bg-red-100 flex items-center justify-center">
                                    <TrendingDown className="h-6 w-6 text-red-600" />
                                </div>
                                <div>
                                    <h3 className="font-semibold">Record Payment</h3>
                                    <p className="text-sm text-muted-foreground">Cash or bank payment</p>
                                </div>
                            </div>
                        </CardContent>
                    </Card>
                </Link>}
                <Link href="/finance/cash/reports/cash-position">
                    <Card className="hover:bg-accent cursor-pointer transition-colors">
                        <CardContent className="p-6">
                            <div className="flex items-center gap-3">
                                <div className="h-12 w-12 rounded-full bg-blue-100 flex items-center justify-center">
                                    <DollarSign className="h-6 w-6 text-blue-600" />
                                </div>
                                <div>
                                    <h3 className="font-semibold">Cash Position</h3>
                                    <p className="text-sm text-muted-foreground">View cash position report</p>
                                </div>
                            </div>
                        </CardContent>
                    </Card>
                </Link>
            </div>
        </div>
    );
}
