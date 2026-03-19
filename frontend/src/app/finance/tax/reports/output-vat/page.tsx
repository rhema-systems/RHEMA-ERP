'use client';

import { useEffect, useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { taxDataService } from '@/services/finance/tax-data.service';
import type { TransactionWithTax } from '@/types/tax';
import { Download, Filter, Search } from 'lucide-react';
import Link from 'next/link';

export default function OutputVATRegisterPage() {
    const [transactions, setTransactions] = useState<TransactionWithTax[]>([]);
    const [filteredTransactions, setFilteredTransactions] = useState<TransactionWithTax[]>([]);
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
            const data = await taxDataService.getSalesTransactions();
            setTransactions(data);
        } catch (error) {
            console.error('Failed to load transactions:', error);
        } finally {
            setLoading(false);
        }
    };

    const filterTransactions = () => {
        let filtered = transactions;

        if (startDate) {
            filtered = filtered.filter(t => t.date >= startDate);
        }
        if (endDate) {
            filtered = filtered.filter(t => t.date <= endDate);
        }

        if (searchTerm) {
            const term = searchTerm.toLowerCase();
            filtered = filtered.filter(t =>
                t.reference.toLowerCase().includes(term) ||
                t.partnerName.toLowerCase().includes(term) ||
                t.description.toLowerCase().includes(term)
            );
        }

        setFilteredTransactions(filtered);
    };

    const formatCurrency = (amount: number) => {
        return `GHS ${amount.toLocaleString('en-GH', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    };

    const getTaxAmount = (transaction: TransactionWithTax, taxCode: string) => {
        const tax = transaction.taxCalculations.find(t => t.taxTypeCode === taxCode);
        return tax?.taxAmount || 0;
    };

    const totals = {
        baseAmount: filteredTransactions.reduce((sum, t) => sum + t.baseAmount, 0),
        vat: filteredTransactions.reduce((sum, t) => sum + getTaxAmount(t, 'VAT'), 0),
        nhil: filteredTransactions.reduce((sum, t) => sum + getTaxAmount(t, 'NHIL'), 0),
        getfl: filteredTransactions.reduce((sum, t) => sum + getTaxAmount(t, 'GETFL'), 0),
        totalTax: filteredTransactions.reduce((sum, t) => sum + t.totalTax, 0),
        totalAmount: filteredTransactions.reduce((sum, t) => sum + t.totalAmount, 0),
    };

    return (
        <div className="p-6 space-y-6">
            {/* Header */}
            <div className="flex justify-between items-center">
                <div>
                    <h1 className="text-3xl font-bold">Output VAT Register</h1>
                    <p className="text-muted-foreground">VAT collected on sales - December 2024</p>
                </div>
                <div className="flex gap-2">
                    <Link href="/finance/tax/reports">
                        <Button variant="outline">
                            All Reports
                        </Button>
                    </Link>
                    <Button variant="outline">
                        <Download className="mr-2 h-4 w-4" />
                        Export
                    </Button>
                </div>
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
                                    placeholder="Reference, customer..."
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
                                onChange={(e) => setStartDate(e.target.value)}
                                className="mt-1"
                            />
                        </div>
                        <div>
                            <Label htmlFor="endDate">End Date</Label>
                            <Input
                                id="endDate"
                                type="date"
                                value={endDate}
                                onChange={(e) => setEndDate(e.target.value)}
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
                                }}
                                className="w-full"
                            >
                                Reset
                            </Button>
                        </div>
                    </div>
                </CardContent>
            </Card>

            {/* Summary Cards */}
            <div className="grid grid-cols-1 md:grid-cols-5 gap-4">
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">Transactions</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{filteredTransactions.length}</div>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">Base Amount</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{formatCurrency(totals.baseAmount)}</div>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">Output VAT</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold text-blue-600">{formatCurrency(totals.vat)}</div>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">NHIL + GETFL</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold text-purple-600">
                            {formatCurrency(totals.nhil + totals.getfl)}
                        </div>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">Total Tax</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold text-green-600">{formatCurrency(totals.totalTax)}</div>
                    </CardContent>
                </Card>
            </div>

            {/* Transactions Table */}
            <Card>
                <CardHeader>
                    <CardTitle>Sales Transactions</CardTitle>
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
                                        <th className="text-left p-3 font-semibold">Reference</th>
                                        <th className="text-left p-3 font-semibold">Customer</th>
                                        <th className="text-left p-3 font-semibold">Description</th>
                                        <th className="text-right p-3 font-semibold">Base Amount</th>
                                        <th className="text-right p-3 font-semibold">VAT (15%)</th>
                                        <th className="text-right p-3 font-semibold">NHIL (2.5%)</th>
                                        <th className="text-right p-3 font-semibold">GETFL (2.5%)</th>
                                        <th className="text-right p-3 font-semibold">Total Tax</th>
                                        <th className="text-right p-3 font-semibold">Total Amount</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {filteredTransactions.map(transaction => (
                                        <tr key={transaction.id} className="border-b hover:bg-accent">
                                            <td className="p-3">{transaction.date}</td>
                                            <td className="p-3">
                                                <Badge variant="outline" className="bg-blue-50 text-blue-700 border-blue-200">
                                                    {transaction.reference}
                                                </Badge>
                                            </td>
                                            <td className="p-3 font-medium">{transaction.partnerName}</td>
                                            <td className="p-3 text-sm text-muted-foreground max-w-xs truncate">
                                                {transaction.description}
                                            </td>
                                            <td className="p-3 text-right">{formatCurrency(transaction.baseAmount)}</td>
                                            <td className="p-3 text-right text-blue-600 font-medium">
                                                {formatCurrency(getTaxAmount(transaction, 'VAT'))}
                                            </td>
                                            <td className="p-3 text-right text-purple-600">
                                                {formatCurrency(getTaxAmount(transaction, 'NHIL'))}
                                            </td>
                                            <td className="p-3 text-right text-purple-600">
                                                {formatCurrency(getTaxAmount(transaction, 'GETFL'))}
                                            </td>
                                            <td className="p-3 text-right font-semibold text-green-600">
                                                {formatCurrency(transaction.totalTax)}
                                            </td>
                                            <td className="p-3 text-right font-bold">
                                                {formatCurrency(transaction.totalAmount)}
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                                <tfoot className="bg-muted/50">
                                    <tr className="font-bold">
                                        <td colSpan={4} className="p-3">TOTAL</td>
                                        <td className="p-3 text-right">{formatCurrency(totals.baseAmount)}</td>
                                        <td className="p-3 text-right text-blue-600">{formatCurrency(totals.vat)}</td>
                                        <td className="p-3 text-right text-purple-600">{formatCurrency(totals.nhil)}</td>
                                        <td className="p-3 text-right text-purple-600">{formatCurrency(totals.getfl)}</td>
                                        <td className="p-3 text-right text-green-600">{formatCurrency(totals.totalTax)}</td>
                                        <td className="p-3 text-right">{formatCurrency(totals.totalAmount)}</td>
                                    </tr>
                                </tfoot>
                            </table>
                        </div>
                    )}
                </CardContent>
            </Card>

            {/* Quick Links */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <Link href="/finance/tax/reports/input-vat">
                    <Card className="hover:bg-accent cursor-pointer transition-colors">
                        <CardContent className="p-6">
                            <h3 className="font-semibold mb-2">View Input VAT Register</h3>
                            <p className="text-sm text-muted-foreground">
                                VAT paid on purchase transactions
                            </p>
                        </CardContent>
                    </Card>
                </Link>
                <Link href="/finance/tax/reports/vat-reconciliation">
                    <Card className="hover:bg-accent cursor-pointer transition-colors">
                        <CardContent className="p-6">
                            <h3 className="font-semibold mb-2">View VAT Reconciliation</h3>
                            <p className="text-sm text-muted-foreground">
                                Compare input and output VAT
                            </p>
                        </CardContent>
                    </Card>
                </Link>
            </div>
        </div>
    );
}
