'use client';

import React, { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Download, Printer } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';

// MOCK DATA
interface CashFlowItem {
    id: string;
    description: string;
    amount: number; // Positive = Inflow, Negative = Outflow
    category: 'Operating' | 'Investing' | 'Financing';
    subCategory?: string;
}

const MOCK_DATA: CashFlowItem[] = [
    // Operating Activities
    { id: '1', description: 'Net Income', amount: 30500, category: 'Operating', subCategory: 'Net Income' },
    { id: '2', description: 'Depreciation Expense', amount: 2500, category: 'Operating', subCategory: 'Adjustments' },
    { id: '3', description: 'Increase in Accounts Receivable', amount: -5000, category: 'Operating', subCategory: 'Working Capital' },
    { id: '4', description: 'Decrease in Inventory', amount: 2000, category: 'Operating', subCategory: 'Working Capital' },
    { id: '5', description: 'Increase in Accounts Payable', amount: 3000, category: 'Operating', subCategory: 'Working Capital' },

    // Investing Activities
    { id: '6', description: 'Purchase of Equipment', amount: -15000, category: 'Investing' },
    { id: '7', description: 'Sale of Old Vehicle', amount: 4000, category: 'Investing' },

    // Financing Activities
    { id: '8', description: 'Proceeds from Long-term Loan', amount: 50000, category: 'Financing' },
    { id: '9', description: 'Dividends Paid', amount: -10000, category: 'Financing' },
];

export default function CashFlowStatementPage() {
    const router = useRouter();
    const [startDate, setStartDate] = useState(new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0]);
    const [endDate, setEndDate] = useState(new Date().toISOString().split('T')[0]);
    const [currency, setCurrency] = useState('GHS');

    // Group Data
    const operatingItems = MOCK_DATA.filter(i => i.category === 'Operating');
    const investingItems = MOCK_DATA.filter(i => i.category === 'Investing');
    const financingItems = MOCK_DATA.filter(i => i.category === 'Financing');

    // Calculate Totals
    const netCashOperating = operatingItems.reduce((sum, item) => sum + item.amount, 0);
    const netCashInvesting = investingItems.reduce((sum, item) => sum + item.amount, 0);
    const netCashFinancing = financingItems.reduce((sum, item) => sum + item.amount, 0);

    const netChangeInCash = netCashOperating + netCashInvesting + netCashFinancing;
    const beginningCashBalance = 15000; // Mock beginning balance
    const endingCashBalance = beginningCashBalance + netChangeInCash;

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

    const SectionTotal = ({ title, amount, isMain = false }: { title: string, amount: number, isMain?: boolean }) => (
        <TableRow className={`font-bold ${isMain ? 'bg-blue-50/50 text-lg border-t-2 border-blue-200' : 'bg-muted/50 border-t'}`}>
            <TableCell colSpan={2} className="text-right">{title}</TableCell>
            <TableCell className="text-right w-[150px]">{formatMoney(amount)}</TableCell>
        </TableRow>
    );

    const CashFlowRow = ({ item }: { item: CashFlowItem }) => (
        <TableRow key={item.id} className="border-0">
            <TableCell colSpan={2} className="pl-8">{item.description}</TableCell>
            <TableCell className="text-right w-[150px]">{formatMoney(item.amount)}</TableCell>
        </TableRow>
    );

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Cash Flow Statement</h1>
                    <p className="text-muted-foreground">Statement of Cash Flows (Indirect Method)</p>
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
                        <BreadcrumbPage>Cash Flow</BreadcrumbPage>
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
                    <CardTitle className="text-xl">Statement of Cash Flows</CardTitle>
                    <p className="text-sm text-muted-foreground">
                        For the period {new Date(startDate).toLocaleDateString()} to {new Date(endDate).toLocaleDateString()}
                    </p>
                    <p className="text-xs text-muted-foreground font-mono mt-1">Currency: {currency}</p>
                </CardHeader>
                <CardContent className="pt-6">
                    <div className="max-w-4xl mx-auto border rounded-md p-6 bg-white shadow-sm">
                        <Table>
                            <TableBody>
                                {/* OPERATING ACTIVITIES */}
                                <SectionHeader title="Cash Flows from Operating Activities" />
                                {operatingItems.map(item => <CashFlowRow key={item.id} item={item} />)}
                                <SectionTotal title="Net Cash from Operating Activities" amount={netCashOperating} />

                                {/* INVESTING ACTIVITIES */}
                                <TableRow className="h-4"><TableCell colSpan={3}></TableCell></TableRow>
                                <SectionHeader title="Cash Flows from Investing Activities" />
                                {investingItems.map(item => <CashFlowRow key={item.id} item={item} />)}
                                <SectionTotal title="Net Cash from Investing Activities" amount={netCashInvesting} />

                                {/* FINANCING ACTIVITIES */}
                                <TableRow className="h-4"><TableCell colSpan={3}></TableCell></TableRow>
                                <SectionHeader title="Cash Flows from Financing Activities" />
                                {financingItems.map(item => <CashFlowRow key={item.id} item={item} />)}
                                <SectionTotal title="Net Cash from Financing Activities" amount={netCashFinancing} />

                                {/* SUMMARY */}
                                <TableRow className="h-8"><TableCell colSpan={3}></TableCell></TableRow>
                                <SectionTotal title="Net Increase (Decrease) in Cash" amount={netChangeInCash} isMain />

                                <TableRow className="border-0">
                                    <TableCell colSpan={2} className="text-right pt-4">Cash Balance at Beginning of Period</TableCell>
                                    <TableCell className="text-right pt-4 w-[150px]">{formatMoney(beginningCashBalance)}</TableCell>
                                </TableRow>
                                <TableRow className="font-bold text-lg border-t-2 border-black">
                                    <TableCell colSpan={2} className="text-right">Cash Balance at End of Period</TableCell>
                                    <TableCell className="text-right w-[150px]">{formatMoney(endingCashBalance)}</TableCell>
                                </TableRow>
                            </TableBody>
                        </Table>
                    </div>
                </CardContent>
            </Card>
        </div>
    );
}
