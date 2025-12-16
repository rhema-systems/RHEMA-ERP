'use client';

import React, { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Download, Printer, Calendar as CalendarIcon } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';

// MOCK DATA
interface IncomeStatementItem {
    id: string;
    accountCode: string;
    accountName: string;
    amount: number; // Positive for Revenue, Negative for Expense (or handled by display logic)
    category: 'Revenue' | 'COGS' | 'Expense';
}

const MOCK_DATA: IncomeStatementItem[] = [
    // Revenue
    { id: '1', accountCode: '4000', accountName: 'Sales Revenue', amount: 150000, category: 'Revenue' },
    { id: '2', accountCode: '4100', accountName: 'Service Income', amount: 45000, category: 'Revenue' },

    // COGS
    { id: '3', accountCode: '5000', accountName: 'Cost of Goods Sold', amount: 85000, category: 'COGS' },
    { id: '4', accountCode: '5050', accountName: 'Freight In', amount: 5000, category: 'COGS' },

    // Expenses
    { id: '5', accountCode: '6000', accountName: 'Salaries Expense', amount: 35000, category: 'Expense' },
    { id: '6', accountCode: '6100', accountName: 'Rent Expense', amount: 12000, category: 'Expense' },
    { id: '7', accountCode: '6200', accountName: 'Utilities Expense', amount: 4500, category: 'Expense' },
    { id: '8', accountCode: '6300', accountName: 'Marketing Expense', amount: 8000, category: 'Expense' },
    { id: '9', accountCode: '6400', accountName: 'Depreciation Expense', amount: 2500, category: 'Expense' },
];

export default function IncomeStatementPage() {
    const router = useRouter();
    const [startDate, setStartDate] = useState(new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0]); // Jan 1st
    const [endDate, setEndDate] = useState(new Date().toISOString().split('T')[0]); // Today
    const [currency, setCurrency] = useState('GHS');

    // Group Data
    const revenueItems = MOCK_DATA.filter(i => i.category === 'Revenue');
    const cogsItems = MOCK_DATA.filter(i => i.category === 'COGS');
    const expenseItems = MOCK_DATA.filter(i => i.category === 'Expense');

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
            <TableCell className="w-[100px] text-muted-foreground">{item.accountCode}</TableCell>
            <TableCell>{item.accountName}</TableCell>
            <TableCell className="text-right w-[150px]">{formatMoney(item.amount)}</TableCell>
        </TableRow>
    );

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Income Statement</h1>
                    <p className="text-muted-foreground">Profit and Loss Statement</p>
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
                                {revenueItems.map(item => <AccountRow key={item.id} item={item} />)}
                                <SectionTotal title="Total Revenue" amount={totalRevenue} />

                                {/* COGS SECTION */}
                                <TableRow className="h-4"><TableCell colSpan={3}></TableCell></TableRow>
                                <SectionHeader title="Cost of Goods Sold" />
                                {cogsItems.map(item => <AccountRow key={item.id} item={item} />)}
                                <SectionTotal title="Total COGS" amount={totalCOGS} isNegative />

                                {/* GROSS PROFIT */}
                                <TableRow className="bg-blue-50/50 font-bold text-lg border-t-4 border-double">
                                    <TableCell colSpan={2} className="text-right py-4">Gross Profit</TableCell>
                                    <TableCell className="text-right py-4">{formatMoney(grossProfit)}</TableCell>
                                </TableRow>

                                {/* EXPENSES SECTION */}
                                <TableRow className="h-4"><TableCell colSpan={3}></TableCell></TableRow>
                                <SectionHeader title="Operating Expenses" />
                                {expenseItems.map(item => <AccountRow key={item.id} item={item} />)}
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
