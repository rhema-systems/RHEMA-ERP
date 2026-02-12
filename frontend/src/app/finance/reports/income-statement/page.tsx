'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Download, Loader2, Printer } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { Table, TableBody, TableCell, TableRow } from '@/components/ui/table';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { Account, FinanceSettings } from '@/types/finance';

interface IncomeStatementItem {
    id: string;
    accountCode: string;
    accountName: string;
    amount: number;
    category: 'Revenue' | 'COGS' | 'Expense';
}

export default function IncomeStatementPage() {
    const router = useRouter();
    const [startDate, setStartDate] = useState(new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0]);
    const [endDate, setEndDate] = useState(new Date().toISOString().split('T')[0]);
    const [currency, setCurrency] = useState('GHS');

    // Data loading state
    const [loading, setLoading] = useState(true);
    const [settings, setSettings] = useState<FinanceSettings | null>(null);
    const [incomeData, setIncomeData] = useState<IncomeStatementItem[]>([]);

    useEffect(() => {
        loadData();
    }, []);

    const loadData = async () => {
        try {
            setLoading(true);

            const settingsData = await financeDataService.getFinanceSettings();
            setSettings(settingsData);

            const accounts = await financeDataService.getAccounts({ coaType: settingsData.coaType });

            // Transform accounts to income statement items
            const items: IncomeStatementItem[] = [];

            accounts.forEach(account => {
                if (account.accountType === 'Revenue') {
                    items.push({
                        id: account.id,
                        accountCode: account.accountCode,
                        accountName: account.accountName,
                        amount: Math.abs(account.currentBalance || 0),
                        category: 'Revenue',
                    });
                } else if (account.accountType === 'Expense') {
                    const isCOGS = account.accountName.toLowerCase().includes('cost') ||
                        account.accountCode.startsWith('50') ||
                        account.accountCode.includes('-50');
                    items.push({
                        id: account.id,
                        accountCode: account.accountCode,
                        accountName: account.accountName,
                        amount: Math.abs(account.currentBalance || 0),
                        category: isCOGS ? 'COGS' : 'Expense',
                    });
                }
            });

            setIncomeData(items);
        } catch (error) {
            console.error('Error loading income statement data:', error);
        } finally {
            setLoading(false);
        }
    };

    // Group Data
    const revenueItems = incomeData.filter(i => i.category === 'Revenue');
    const cogsItems = incomeData.filter(i => i.category === 'COGS');
    const expenseItems = incomeData.filter(i => i.category === 'Expense');

    // Calculate Totals
    const totalRevenue = revenueItems.reduce((sum, item) => sum + item.amount, 0);
    const totalCOGS = cogsItems.reduce((sum, item) => sum + item.amount, 0);
    const grossProfit = totalRevenue - totalCOGS;
    const totalExpenses = expenseItems.reduce((sum, item) => sum + item.amount, 0);
    const netIncome = grossProfit - totalExpenses;

    const formatMoney = (amount: number) => {
        return new Intl.NumberFormat('en-GH', {
            style: 'decimal',
            minimumFractionDigits: 2,
            maximumFractionDigits: 2,
        }).format(amount);
    };

    const SectionHeader = ({ title }: { title: string }) => (
        <TableRow className="bg-muted/30 hover:bg-muted/30">
            <TableCell colSpan={3} className="font-bold text-base py-3">{title}</TableCell>
        </TableRow>
    );

    const SectionTotal = ({ title, amount, isNegative = false }: { title: string, amount: number, isNegative?: boolean }) => (
        <TableRow className="bg-muted/50 font-bold border-t-2">
            <TableCell colSpan={2} className="text-right">{title}</TableCell>
            <TableCell className="text-right w-[150px]">
                {isNegative ? `(${formatMoney(amount)})` : formatMoney(amount)}
            </TableCell>
        </TableRow>
    );

    const AccountRow = ({ item }: { item: IncomeStatementItem }) => (
        <TableRow key={item.id} className="border-0">
            <TableCell className={`${settings?.coaType === 'Segmented' ? 'w-[150px]' : 'w-[100px]'} text-muted-foreground font-mono text-sm`}>
                {item.accountCode}
            </TableCell>
            <TableCell>{item.accountName}</TableCell>
            <TableCell className="text-right w-[150px]">{formatMoney(item.amount)}</TableCell>
        </TableRow>
    );

    if (loading) {
        return (
            <div className="flex items-center justify-center h-96">
                <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
            </div>
        );
    }

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Income Statement</h1>
                    <p className="text-muted-foreground">
                        Profit and Loss Statement
                        {settings?.coaType === 'Segmented' && (
                            <span className="ml-2 text-xs px-2 py-0.5 rounded-full bg-purple-100 text-purple-700">
                                Segmented COA
                            </span>
                        )}
                    </p>
                </div>
                <div className="flex gap-2">
                    <Button variant="outline" onClick={() => window.print()}>
                        <Printer className="mr-2 h-4 w-4" />
                        Print
                    </Button>
                    <Button variant="outline">
                        <Download className="mr-2 h-4 w-4" />
                        Export
                    </Button>
                </div>
            </div>

            {/* Breadcrumbs */}
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
                        <BreadcrumbPage>Income Statement</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Filters */}
            <Card>
                <CardContent className="p-6">
                    <div className="grid grid-cols-1 md:grid-cols-4 gap-4 items-end">
                        <div className="space-y-2">
                            <Label>From Date</Label>
                            <Input
                                type="date"
                                value={startDate}
                                onChange={(e) => setStartDate(e.target.value)}
                            />
                        </div>
                        <div className="space-y-2">
                            <Label>To Date</Label>
                            <Input
                                type="date"
                                value={endDate}
                                onChange={(e) => setEndDate(e.target.value)}
                            />
                        </div>
                        <div className="space-y-2">
                            <Label>Currency</Label>
                            <Select value={currency} onValueChange={setCurrency}>
                                <SelectTrigger>
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="GHS">GHS - Ghana Cedi</SelectItem>
                                    <SelectItem value="USD">USD - US Dollar</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>
                    </div>
                </CardContent>
            </Card>

            {/* Report Table */}
            <Card>
                <CardHeader className="pb-2 text-center border-b">
                    <CardTitle className="text-xl">Income Statement</CardTitle>
                    <p className="text-sm text-muted-foreground">
                        For the period {new Date(startDate).toLocaleDateString()} to {new Date(endDate).toLocaleDateString()}
                    </p>
                    <p className="text-xs text-muted-foreground font-mono mt-1">Currency: {currency}</p>
                </CardHeader>
                <CardContent className="pt-6">
                    <div className="max-w-4xl mx-auto border rounded-md p-6 bg-white shadow-sm">
                        <Table>
                            <TableBody>
                                {/* REVENUE SECTION */}
                                <SectionHeader title="Revenue" />
                                {revenueItems.length > 0 ? (
                                    revenueItems.map(item => <AccountRow key={item.id} item={item} />)
                                ) : (
                                    <TableRow><TableCell colSpan={3} className="text-muted-foreground italic">No revenue accounts</TableCell></TableRow>
                                )}
                                <SectionTotal title="Total Revenue" amount={totalRevenue} />

                                {/* COGS SECTION */}
                                <TableRow className="h-4"><TableCell colSpan={3}></TableCell></TableRow>
                                <SectionHeader title="Cost of Goods Sold" />
                                {cogsItems.length > 0 ? (
                                    cogsItems.map(item => <AccountRow key={item.id} item={item} />)
                                ) : (
                                    <TableRow><TableCell colSpan={3} className="text-muted-foreground italic">No COGS accounts</TableCell></TableRow>
                                )}
                                <SectionTotal title="Total COGS" amount={totalCOGS} isNegative />

                                {/* GROSS PROFIT */}
                                <TableRow className="bg-blue-50/50 font-bold text-lg border-t-4 border-double">
                                    <TableCell colSpan={2} className="text-right py-4">Gross Profit</TableCell>
                                    <TableCell className="text-right py-4">{formatMoney(grossProfit)}</TableCell>
                                </TableRow>

                                {/* EXPENSES SECTION */}
                                <TableRow className="h-4"><TableCell colSpan={3}></TableCell></TableRow>
                                <SectionHeader title="Operating Expenses" />
                                {expenseItems.length > 0 ? (
                                    expenseItems.map(item => <AccountRow key={item.id} item={item} />)
                                ) : (
                                    <TableRow><TableCell colSpan={3} className="text-muted-foreground italic">No expense accounts</TableCell></TableRow>
                                )}
                                <SectionTotal title="Total Expenses" amount={totalExpenses} isNegative />

                                {/* NET INCOME */}
                                <TableRow className="h-8"><TableCell colSpan={3}></TableCell></TableRow>
                                <TableRow className={`font-bold text-xl border-t-4 border-double ${netIncome >= 0 ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'}`}>
                                    <TableCell colSpan={2} className="text-right py-4">Net Income</TableCell>
                                    <TableCell className="text-right py-4">{formatMoney(netIncome)}</TableCell>
                                </TableRow>
                            </TableBody>
                        </Table>
                    </div>
                </CardContent>
            </Card>
        </div>
    );
}
