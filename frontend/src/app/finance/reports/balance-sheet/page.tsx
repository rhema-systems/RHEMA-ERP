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
interface BalanceSheetItem {
    id: string;
    accountCode: string;
    accountName: string;
    amount: number;
    category: 'Current Asset' | 'Non-Current Asset' | 'Current Liability' | 'Non-Current Liability' | 'Equity';
}

const MOCK_DATA: BalanceSheetItem[] = [
    // Current Assets
    { id: '1', accountCode: '1000', accountName: 'Cash on Hand', amount: 50000, category: 'Current Asset' },
    { id: '2', accountCode: '1100', accountName: 'Accounts Receivable', amount: 35000, category: 'Current Asset' },
    { id: '3', accountCode: '1200', accountName: 'Inventory', amount: 75000, category: 'Current Asset' },

    // Non-Current Assets
    { id: '4', accountCode: '1500', accountName: 'Property, Plant & Equipment', amount: 250000, category: 'Non-Current Asset' },
    { id: '5', accountCode: '1600', accountName: 'Intangible Assets', amount: 40000, category: 'Non-Current Asset' },

    // Current Liabilities
    { id: '6', accountCode: '2000', accountName: 'Accounts Payable', amount: 45000, category: 'Current Liability' },
    { id: '7', accountCode: '2100', accountName: 'VAT Payable', amount: 12000, category: 'Current Liability' },

    // Non-Current Liabilities
    { id: '8', accountCode: '2500', accountName: 'Long-term Loans', amount: 100000, category: 'Non-Current Liability' },

    // Equity
    { id: '9', accountCode: '3000', accountName: 'Share Capital', amount: 200000, category: 'Equity' },
    { id: '10', accountCode: '3100', accountName: 'Retained Earnings', amount: 93000, category: 'Equity' }, // 450k Assets - 157k Liab = 293k Equity. 200k Share Cap. Need 93k Retained.
];

export default function BalanceSheetPage() {
    const router = useRouter();
    const [asOfDate, setAsOfDate] = useState(new Date().toISOString().split('T')[0]);
    const [currency, setCurrency] = useState('GHS');

    // Group Data
    const currentAssets = MOCK_DATA.filter(i => i.category === 'Current Asset');
    const nonCurrentAssets = MOCK_DATA.filter(i => i.category === 'Non-Current Asset');
    const currentLiabilities = MOCK_DATA.filter(i => i.category === 'Current Liability');
    const nonCurrentLiabilities = MOCK_DATA.filter(i => i.category === 'Non-Current Liability');
    const equityItems = MOCK_DATA.filter(i => i.category === 'Equity');

    // Calculate Totals
    const totalCurrentAssets = currentAssets.reduce((sum, item) => sum + item.amount, 0);
    const totalNonCurrentAssets = nonCurrentAssets.reduce((sum, item) => sum + item.amount, 0);
    const totalAssets = totalCurrentAssets + totalNonCurrentAssets;

    const totalCurrentLiabilities = currentLiabilities.reduce((sum, item) => sum + item.amount, 0);
    const totalNonCurrentLiabilities = nonCurrentLiabilities.reduce((sum, item) => sum + item.amount, 0);
    const totalLiabilities = totalCurrentLiabilities + totalNonCurrentLiabilities;

    const totalEquity = equityItems.reduce((sum, item) => sum + item.amount, 0);
    const totalLiabilitiesAndEquity = totalLiabilities + totalEquity;

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

    const AccountRow = ({ item }: { item: BalanceSheetItem }) => (
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
                    <h1 className="text-3xl font-bold tracking-tight">Balance Sheet</h1>
                    <p className="text-muted-foreground">Statement of Financial Position</p>
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
                        <BreadcrumbPage>Balance Sheet</BreadcrumbPage>
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
                    </div>
                </CardContent>
            </Card>

            {/* Report Table */}
            <Card>
                <CardHeader className="pb-2 text-center border-b">
                    <CardTitle className="text-xl">Balance Sheet</CardTitle>
                    <p className="text-sm text-muted-foreground">
                        As of {new Date(asOfDate).toLocaleDateString()}
                    </p>
                    <p className="text-xs text-muted-foreground font-mono mt-1">Currency: {currency}</p>
                </CardHeader>
                <CardContent className="pt-6">
                    <div className="max-w-4xl mx-auto border rounded-md p-6 bg-white shadow-sm">
                        <Table>
                            <TableBody>
                                {/* ASSETS */}
                                <TableRow className="bg-slate-100 hover:bg-slate-100"><TableCell colSpan={3} className="font-bold text-lg py-4">ASSETS</TableCell></TableRow>

                                <SectionHeader title="Current Assets" />
                                {currentAssets.map(item => <AccountRow key={item.id} item={item} />)}
                                <SectionTotal title="Total Current Assets" amount={totalCurrentAssets} />

                                <TableRow className="h-4"><TableCell colSpan={3}></TableCell></TableRow>
                                <SectionHeader title="Non-Current Assets" />
                                {nonCurrentAssets.map(item => <AccountRow key={item.id} item={item} />)}
                                <SectionTotal title="Total Non-Current Assets" amount={totalNonCurrentAssets} />

                                <TableRow className="h-4"><TableCell colSpan={3}></TableCell></TableRow>
                                <SectionTotal title="TOTAL ASSETS" amount={totalAssets} isMain />


                                {/* LIABILITIES */}
                                <TableRow className="h-8"><TableCell colSpan={3}></TableCell></TableRow>
                                <TableRow className="bg-slate-100 hover:bg-slate-100"><TableCell colSpan={3} className="font-bold text-lg py-4">LIABILITIES</TableCell></TableRow>

                                <SectionHeader title="Current Liabilities" />
                                {currentLiabilities.map(item => <AccountRow key={item.id} item={item} />)}
                                <SectionTotal title="Total Current Liabilities" amount={totalCurrentLiabilities} />

                                <TableRow className="h-4"><TableCell colSpan={3}></TableCell></TableRow>
                                <SectionHeader title="Non-Current Liabilities" />
                                {nonCurrentLiabilities.map(item => <AccountRow key={item.id} item={item} />)}
                                <SectionTotal title="Total Non-Current Liabilities" amount={totalNonCurrentLiabilities} />

                                <TableRow className="h-4"><TableCell colSpan={3}></TableCell></TableRow>
                                <SectionTotal title="Total Liabilities" amount={totalLiabilities} />


                                {/* EQUITY */}
                                <TableRow className="h-8"><TableCell colSpan={3}></TableCell></TableRow>
                                <TableRow className="bg-slate-100 hover:bg-slate-100"><TableCell colSpan={3} className="font-bold text-lg py-4">EQUITY</TableCell></TableRow>

                                {equityItems.map(item => <AccountRow key={item.id} item={item} />)}
                                <SectionTotal title="Total Equity" amount={totalEquity} />

                                {/* TOTAL LIABILITIES & EQUITY */}
                                <TableRow className="h-4"><TableCell colSpan={3}></TableCell></TableRow>
                                <SectionTotal title="TOTAL LIABILITIES & EQUITY" amount={totalLiabilitiesAndEquity} isMain />
                            </TableBody>
                        </Table>
                    </div>
                </CardContent>
            </Card>
        </div>
    );
}
