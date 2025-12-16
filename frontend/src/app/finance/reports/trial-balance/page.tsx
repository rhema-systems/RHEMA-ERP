'use client';

import React, { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { ArrowLeft, Download, FileText, Filter, Printer, Search } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';

// MOCK DATA
interface TrialBalanceItem {
    id: string;
    accountCode: string;
    accountName: string;
    accountType: string;
    openingBalance: number; // Positive = Debit, Negative = Credit
    periodDebit: number;
    periodCredit: number;
    currency: string;
}

const MOCK_DATA: TrialBalanceItem[] = [
    { id: '1', accountCode: '1000', accountName: 'Cash on Hand', accountType: 'Asset', openingBalance: 5000, periodDebit: 12000, periodCredit: 4000, currency: 'GHS' },
    { id: '2', accountCode: '1001', accountName: 'Petty Cash', accountType: 'Asset', openingBalance: 1000, periodDebit: 500, periodCredit: 200, currency: 'GHS' },
    { id: '3', accountCode: '1100', accountName: 'Accounts Receivable', accountType: 'Asset', openingBalance: 15000, periodDebit: 25000, periodCredit: 18000, currency: 'GHS' },
    { id: '4', accountCode: '1200', accountName: 'Inventory', accountType: 'Asset', openingBalance: 20000, periodDebit: 8000, periodCredit: 5000, currency: 'GHS' },
    { id: '5', accountCode: '2000', accountName: 'Accounts Payable', accountType: 'Liability', openingBalance: -12000, periodDebit: 10000, periodCredit: 15000, currency: 'GHS' },
    { id: '6', accountCode: '2100', accountName: 'VAT Payable', accountType: 'Liability', openingBalance: -2500, periodDebit: 3000, periodCredit: 4500, currency: 'GHS' },
    { id: '7', accountCode: '3000', accountName: 'Share Capital', accountType: 'Equity', openingBalance: -50000, periodDebit: 0, periodCredit: 0, currency: 'GHS' },
    { id: '8', accountCode: '3100', accountName: 'Retained Earnings', accountType: 'Equity', openingBalance: -15000, periodDebit: 0, periodCredit: 0, currency: 'GHS' },
    { id: '9', accountCode: '4000', accountName: 'Sales Revenue', accountType: 'Revenue', openingBalance: 0, periodDebit: 1000, periodCredit: 45000, currency: 'GHS' },
    { id: '10', accountCode: '5000', accountName: 'Cost of Goods Sold', accountType: 'Expense', openingBalance: 0, periodDebit: 22000, periodCredit: 0, currency: 'GHS' },
    { id: '11', accountCode: '5100', accountName: 'Rent Expense', accountType: 'Expense', openingBalance: 0, periodDebit: 6000, periodCredit: 0, currency: 'GHS' },
    { id: '12', accountCode: '5200', accountName: 'Salaries Expense', accountType: 'Expense', openingBalance: 0, periodDebit: 15000, periodCredit: 0, currency: 'GHS' },
    { id: '13', accountCode: '9999', accountName: 'Suspense Account', accountType: 'Equity', openingBalance: 0, periodDebit: 0, periodCredit: 0, currency: 'GHS' },
];

export default function TrialBalancePage() {
    const router = useRouter();
    const [asOfDate, setAsOfDate] = useState(new Date().toISOString().split('T')[0]);
    const [currency, setCurrency] = useState('GHS');
    const [hideZeroBalances, setHideZeroBalances] = useState(true);
    const [searchTerm, setSearchTerm] = useState('');

    // Filter Logic
    const filteredData = MOCK_DATA.filter(item => {
        const closingBalance = item.openingBalance + item.periodDebit - item.periodCredit;
        const matchesSearch = item.accountName.toLowerCase().includes(searchTerm.toLowerCase()) ||
            item.accountCode.includes(searchTerm);
        const matchesZero = hideZeroBalances ? Math.abs(closingBalance) > 0.01 : true;

        return matchesSearch && matchesZero;
    }).sort((a, b) => a.accountCode.localeCompare(b.accountCode));

    // Totals Calculation
    const totals = filteredData.reduce((acc, item) => {
        const closing = item.openingBalance + item.periodDebit - item.periodCredit;
        return {
            openingDebit: acc.openingDebit + (item.openingBalance > 0 ? item.openingBalance : 0),
            openingCredit: acc.openingCredit + (item.openingBalance < 0 ? Math.abs(item.openingBalance) : 0),
            periodDebit: acc.periodDebit + item.periodDebit,
            periodCredit: acc.periodCredit + item.periodCredit,
            closingDebit: acc.closingDebit + (closing > 0 ? closing : 0),
            closingCredit: acc.closingCredit + (closing < 0 ? Math.abs(closing) : 0),
        };
    }, {
        openingDebit: 0, openingCredit: 0,
        periodDebit: 0, periodCredit: 0,
        closingDebit: 0, closingCredit: 0
    });

    const formatMoney = (amount: number) => {
        return new Intl.NumberFormat('en-GH', {
            style: 'decimal',
            minimumFractionDigits: 2,
            maximumFractionDigits: 2,
        }).format(amount);
    };

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Trial Balance</h1>
                    <p className="text-muted-foreground">Statement of all ledger account balances</p>
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
                        <BreadcrumbPage>Trial Balance</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Filters */}
            <Card>
                <CardContent className="p-6">
                    <div className="grid grid-cols-1 md:grid-cols-4 gap-4 items-end">
                        <div className="space-y-2">
                            <Label>As Of Date</Label>
                            <Input
                                type="date"
                                value={asOfDate}
                                onChange={(e) => setAsOfDate(e.target.value)}
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
                        <div className="space-y-2">
                            <Label>Search Accounts</Label>
                            <div className="relative">
                                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                                <Input
                                    placeholder="Code or Name..."
                                    className="pl-8"
                                    value={searchTerm}
                                    onChange={(e) => setSearchTerm(e.target.value)}
                                />
                            </div>
                        </div>
                        <div className="flex items-center space-x-2 pb-2">
                            <Switch
                                id="hide-zero"
                                checked={hideZeroBalances}
                                onCheckedChange={setHideZeroBalances}
                            />
                            <Label htmlFor="hide-zero">Hide Zero Balances</Label>
                        </div>
                    </div>
                </CardContent>
            </Card>

            {/* Report Table */}
            <Card>
                <CardHeader className="pb-2">
                    <div className="flex justify-between items-center">
                        <CardTitle className="text-lg">
                            Trial Balance as of {new Date(asOfDate).toLocaleDateString()}
                        </CardTitle>
                        <span className="text-sm text-muted-foreground">
                            Currency: {currency}
                        </span>
                    </div>
                </CardHeader>
                <CardContent>
                    <div className="rounded-md border">
                        <Table>
                            <TableHeader>
                                <TableRow className="bg-muted/50">
                                    <TableHead className="w-[100px]">Code</TableHead>
                                    <TableHead>Account Name</TableHead>
                                    <TableHead className="text-right">Opening Debit</TableHead>
                                    <TableHead className="text-right">Opening Credit</TableHead>
                                    <TableHead className="text-right">Period Debit</TableHead>
                                    <TableHead className="text-right">Period Credit</TableHead>
                                    <TableHead className="text-right font-bold">Closing Debit</TableHead>
                                    <TableHead className="text-right font-bold">Closing Credit</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {filteredData.map((item) => {
                                    const closing = item.openingBalance + item.periodDebit - item.periodCredit;
                                    const openDebit = item.openingBalance > 0 ? item.openingBalance : 0;
                                    const openCredit = item.openingBalance < 0 ? Math.abs(item.openingBalance) : 0;
                                    const closeDebit = closing > 0 ? closing : 0;
                                    const closeCredit = closing < 0 ? Math.abs(closing) : 0;

                                    return (
                                        <TableRow key={item.id}>
                                            <TableCell className="font-medium">{item.accountCode}</TableCell>
                                            <TableCell>{item.accountName}</TableCell>
                                            <TableCell className="text-right text-muted-foreground">{openDebit > 0 ? formatMoney(openDebit) : '-'}</TableCell>
                                            <TableCell className="text-right text-muted-foreground">{openCredit > 0 ? formatMoney(openCredit) : '-'}</TableCell>
                                            <TableCell className="text-right">{item.periodDebit > 0 ? formatMoney(item.periodDebit) : '-'}</TableCell>
                                            <TableCell className="text-right">{item.periodCredit > 0 ? formatMoney(item.periodCredit) : '-'}</TableCell>
                                            <TableCell className="text-right font-bold">{closeDebit > 0 ? formatMoney(closeDebit) : '-'}</TableCell>
                                            <TableCell className="text-right font-bold">{closeCredit > 0 ? formatMoney(closeCredit) : '-'}</TableCell>
                                        </TableRow>
                                    );
                                })}
                            </TableBody>
                            <tfoot className="bg-muted/50 font-bold">
                                <TableRow>
                                    <TableCell colSpan={2} className="text-right">TOTALS:</TableCell>
                                    <TableCell className="text-right">{formatMoney(totals.openingDebit)}</TableCell>
                                    <TableCell className="text-right">{formatMoney(totals.openingCredit)}</TableCell>
                                    <TableCell className="text-right">{formatMoney(totals.periodDebit)}</TableCell>
                                    <TableCell className="text-right">{formatMoney(totals.periodCredit)}</TableCell>
                                    <TableCell className="text-right text-blue-600">{formatMoney(totals.closingDebit)}</TableCell>
                                    <TableCell className="text-right text-blue-600">{formatMoney(totals.closingCredit)}</TableCell>
                                </TableRow>
                            </tfoot>
                        </Table>
                    </div>
                </CardContent>
            </Card>
        </div>
    );
}
