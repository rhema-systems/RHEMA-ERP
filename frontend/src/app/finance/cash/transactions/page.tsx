'use client';

import { useEffect, useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import type { CashTransaction } from '@/types/cash-management';
import { ArrowDownCircle, ArrowUpCircle, ArrowRightLeft, Filter, Search } from 'lucide-react';
import Link from 'next/link';

export default function CashTransactionsPage() {
    const [transactions, setTransactions] = useState<CashTransaction[]>([]);
    const [filteredTransactions, setFilteredTransactions] = useState<CashTransaction[]>([]);
    const [loading, setLoading] = useState(true);
    const [searchTerm, setSearchTerm] = useState('');
    const [startDate, setStartDate] = useState('2024-12-01');
    const [endDate, setEndDate] = useState('2024-12-31');

    useEffect(() => {
        loadTransactions();
    }, []);

    useEffect(() => {
        filterTransactions();
    }, [transactions, searchTerm, startDate, endDate]);

    const loadTransactions = async () => {
        try {
            const data = await cashManagementDataService.getCashTransactions(startDate, endDate);
            setTransactions(data);
        } catch (error) {
            console.error('Failed to load transactions:', error);
        } finally {
            setLoading(false);
        }
    };

    const filterTransactions = () => {
        let filtered = transactions;

        if (searchTerm) {
            const term = searchTerm.toLowerCase();
            filtered = filtered.filter(t =>
                t.transactionNumber.toLowerCase().includes(term) ||
                t.description?.toLowerCase().includes(term) ||
                t.payeeOrPayer?.toLowerCase().includes(term) ||
                t.referenceNumber?.toLowerCase().includes(term)
            );
        }

        setFilteredTransactions(filtered);
    };

    const formatCurrency = (amount: number, currency: string = 'GHS') => {
        return `${currency} ${amount.toLocaleString('en-GH', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    };

    const getTransactionIcon = (type: string) => {
        switch (type) {
            case 'Receipt': return <ArrowDownCircle className="h-5 w-5 text-green-600" />;
            case 'Payment': return <ArrowUpCircle className="h-5 w-5 text-red-600" />;
            case 'Transfer': return <ArrowRightLeft className="h-5 w-5 text-blue-600" />;
            default: return null;
        }
    };

    const getTransactionColor = (type: string) => {
        switch (type) {
            case 'Receipt': return 'bg-green-50 text-green-700 border-green-200';
            case 'Payment': return 'bg-red-50 text-red-700 border-red-200';
            case 'Transfer': return 'bg-blue-50 text-blue-700 border-blue-200';
            default: return 'bg-gray-50 text-gray-700 border-gray-200';
        }
    };

    const totals = {
        receipts: filteredTransactions.filter(t => t.transactionType === 'Receipt').reduce((sum, t) => sum + t.amount, 0),
        payments: filteredTransactions.filter(t => t.transactionType === 'Payment').reduce((sum, t) => sum + t.amount, 0),
        transfers: filteredTransactions.filter(t => t.transactionType === 'Transfer').reduce((sum, t) => sum + t.amount, 0),
    };

    return (
        <div className="p-6 space-y-6">
            {/* Header */}
            <div className="flex justify-between items-center">
                <div>
                    <h1 className="text-3xl font-bold">Cash Transactions</h1>
                    <p className="text-muted-foreground">View and manage all cash transactions</p>
                </div>
                <div className="flex gap-2">
                    <Link href="/finance/cash/accounts">
                        <Button variant="outline">Bank Accounts</Button>
                    </Link>
                </div>
            </div>

            {/* Summary Cards */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground flex items-center gap-2">
                            <ArrowDownCircle className="h-4 w-4 text-green-600" />
                            Total Receipts
                        </CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold text-green-600">{formatCurrency(totals.receipts)}</div>
                        <p className="text-xs text-muted-foreground mt-1">
                            {filteredTransactions.filter(t => t.transactionType === 'Receipt').length} transactions
                        </p>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground flex items-center gap-2">
                            <ArrowUpCircle className="h-4 w-4 text-red-600" />
                            Total Payments
                        </CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold text-red-600">{formatCurrency(totals.payments)}</div>
                        <p className="text-xs text-muted-foreground mt-1">
                            {filteredTransactions.filter(t => t.transactionType === 'Payment').length} transactions
                        </p>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground flex items-center gap-2">
                            <ArrowRightLeft className="h-4 w-4 text-blue-600" />
                            Net Cash Flow
                        </CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className={`text-2xl font-bold ${totals.receipts - totals.payments >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                            {formatCurrency(totals.receipts - totals.payments)}
                        </div>
                        <p className="text-xs text-muted-foreground mt-1">Receipts - Payments</p>
                    </CardContent>
                </Card>
            </div>

            {/* Filters */}
            <Card>
                <CardHeader>
                    <CardTitle className="flex items-center gap-2">
                        <Filter className="h-5 w-5" />
                        Filters
                    </CardTitle>
                </CardHeader>
                <CardContent>
                    <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                        <div>
                            <Label htmlFor="search">Search</Label>
                            <div className="relative mt-1">
                                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                                <Input
                                    id="search"
                                    placeholder="Number, description, payee..."
                                    value={searchTerm}
                                    onChange={(e) => setSearchTerm(e.target.value)}
                                    className="pl-8"
                                />
                            </div>
                        </div>
                        <div>
                            <Label htmlFor="startDate">Start Date</Label>
                            <Input
                                id="startDate"
                                type="date"
                                value={startDate}
                                onChange={(e) => {
                                    setStartDate(e.target.value);
                                    loadTransactions();
                                }}
                                className="mt-1"
                            />
                        </div>
                        <div>
                            <Label htmlFor="endDate">End Date</Label>
                            <Input
                                id="endDate"
                                type="date"
                                value={endDate}
                                onChange={(e) => {
                                    setEndDate(e.target.value);
                                    loadTransactions();
                                }}
                                className="mt-1"
                            />
                        </div>
                        <div className="flex items-end">
                            <Button
                                variant="outline"
                                onClick={() => {
                                    setSearchTerm('');
                                    setStartDate('2024-12-01');
                                    setEndDate('2024-12-31');
                                    loadTransactions();
                                }}
                                className="w-full"
                            >
                                Reset
                            </Button>
                        </div>
                    </div>
                </CardContent>
            </Card>

            {/* Transactions List */}
            <Card>
                <CardHeader>
                    <CardTitle>All Transactions</CardTitle>
                    <CardDescription>
                        Showing {filteredTransactions.length} of {transactions.length} transactions
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    {loading ? (
                        <div className="text-center py-8 text-muted-foreground">Loading...</div>
                    ) : filteredTransactions.length === 0 ? (
                        <div className="text-center py-8 text-muted-foreground">
                            No transactions found matching your filters
                        </div>
                    ) : (
                        <div className="overflow-x-auto">
                            <table className="w-full">
                                <thead>
                                    <tr className="border-b">
                                        <th className="text-left p-3 font-semibold">Date</th>
                                        <th className="text-left p-3 font-semibold">Transaction #</th>
                                        <th className="text-left p-3 font-semibold">Type</th>
                                        <th className="text-left p-3 font-semibold">Bank Account</th>
                                        <th className="text-left p-3 font-semibold">Payee/Payer</th>
                                        <th className="text-left p-3 font-semibold">Description</th>
                                        <th className="text-right p-3 font-semibold">Amount</th>
                                        <th className="text-center p-3 font-semibold">Status</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {filteredTransactions.map((transaction) => (
                                        <tr key={transaction.id} className="border-b hover:bg-accent">
                                            <td className="p-3 text-sm">
                                                {new Date(transaction.transactionDate).toLocaleDateString('en-GB')}
                                            </td>
                                            <td className="p-3 font-mono text-sm">{transaction.transactionNumber}</td>
                                            <td className="p-3">
                                                <Badge variant="outline" className={getTransactionColor(transaction.transactionType)}>
                                                    <span className="flex items-center gap-1">
                                                        {getTransactionIcon(transaction.transactionType)}
                                                        {transaction.transactionType}
                                                    </span>
                                                </Badge>
                                            </td>
                                            <td className="p-3 text-sm">{transaction.bankAccountName}</td>
                                            <td className="p-3 text-sm">{transaction.payeeOrPayer || '-'}</td>
                                            <td className="p-3 text-sm">{transaction.description || '-'}</td>
                                            <td className="p-3 text-right font-semibold">
                                                {formatCurrency(transaction.amount, transaction.currency)}
                                            </td>
                                            <td className="p-3 text-center">
                                                {transaction.isReconciled ? (
                                                    <Badge variant="outline" className="bg-green-50 text-green-700 border-green-200">
                                                        Reconciled
                                                    </Badge>
                                                ) : (
                                                    <Badge variant="outline" className="bg-yellow-50 text-yellow-700 border-yellow-200">
                                                        Pending
                                                    </Badge>
                                                )}
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}
                </CardContent>
            </Card>

            {/* Quick Actions */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <Link href="/finance/cash/transactions/receipts">
                    <Card className="hover:bg-accent cursor-pointer transition-colors">
                        <CardContent className="p-6">
                            <h3 className="font-semibold mb-2 flex items-center gap-2">
                                <ArrowDownCircle className="h-5 w-5 text-green-600" />
                                Record Receipt
                            </h3>
                            <p className="text-sm text-muted-foreground">
                                Record money received into bank account
                            </p>
                        </CardContent>
                    </Card>
                </Link>
                <Link href="/finance/cash/transactions/payments">
                    <Card className="hover:bg-accent cursor-pointer transition-colors">
                        <CardContent className="p-6">
                            <h3 className="font-semibold mb-2 flex items-center gap-2">
                                <ArrowUpCircle className="h-5 w-5 text-red-600" />
                                Record Payment
                            </h3>
                            <p className="text-sm text-muted-foreground">
                                Record money paid from bank account
                            </p>
                        </CardContent>
                    </Card>
                </Link>
                <Link href="/finance/cash/transactions/transfers">
                    <Card className="hover:bg-accent cursor-pointer transition-colors">
                        <CardContent className="p-6">
                            <h3 className="font-semibold mb-2 flex items-center gap-2">
                                <ArrowRightLeft className="h-5 w-5 text-blue-600" />
                                Bank Transfer
                            </h3>
                            <p className="text-sm text-muted-foreground">
                                Transfer between bank accounts
                            </p>
                        </CardContent>
                    </Card>
                </Link>
            </div>
        </div>
    );
}
