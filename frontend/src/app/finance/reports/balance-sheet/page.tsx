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
import type { FinanceSettings } from '@/types/finance';

interface BalanceSheetItem {
    id: string;
    accountCode: string;
    accountName: string;
    amount: number;
    category: 'Current Asset' | 'Non-Current Asset' | 'Current Liability' | 'Non-Current Liability' | 'Equity';
}

export default function BalanceSheetPage() {
    const router = useRouter();
    const [asOfDate, setAsOfDate] = useState(new Date().toISOString().split('T')[0]);
    const [currency, setCurrency] = useState('GHS');

    // Data loading state
    const [loading, setLoading] = useState(true);
    const [settings, setSettings] = useState<FinanceSettings | null>(null);
    const [balanceSheetData, setBalanceSheetData] = useState<BalanceSheetItem[]>([]);

    useEffect(() => {
        loadData();
    }, []);

    const loadData = async () => {
        try {
            setLoading(true);

            const settingsData = await financeDataService.getFinanceSettings();
            setSettings(settingsData);

            const accounts = await financeDataService.getAccounts({ coaType: settingsData.coaType });

            // Transform accounts to balance sheet items
            const items: BalanceSheetItem[] = [];

            accounts.forEach(account => {
                const category = account.accountCategory || '';
                const balance = Math.abs(account.currentBalance || 0);

                if (account.accountType === 'Asset') {
                    const isNonCurrent = category.toLowerCase().includes('fixed') ||
                        category.toLowerCase().includes('non-current');
                    items.push({
                        id: account.id,
                        accountCode: account.accountCode,
                        accountName: account.accountName,
                        amount: balance,
                        category: isNonCurrent ? 'Non-Current Asset' : 'Current Asset',
                    });
                } else if (account.accountType === 'Liability') {
                    const isNonCurrent = category.toLowerCase().includes('long') ||
                        category.toLowerCase().includes('non-current');
                    items.push({
                        id: account.id,
                        accountCode: account.accountCode,
                        accountName: account.accountName,
                        amount: balance,
                        category: isNonCurrent ? 'Non-Current Liability' : 'Current Liability',
                    });
                } else if (account.accountType === 'Equity') {
                    items.push({
                        id: account.id,
                        accountCode: account.accountCode,
                        accountName: account.accountName,
                        amount: balance,
                        category: 'Equity',
                    });
                }
            });

            setBalanceSheetData(items);
        } catch (error) {
            console.error('Error loading balance sheet data:', error);
        } finally {
            setLoading(false);
        }
    };

    // Group Data
    const currentAssets = balanceSheetData.filter(i => i.category === 'Current Asset');
    const nonCurrentAssets = balanceSheetData.filter(i => i.category === 'Non-Current Asset');
    const currentLiabilities = balanceSheetData.filter(i => i.category === 'Current Liability');
    const nonCurrentLiabilities = balanceSheetData.filter(i => i.category === 'Non-Current Liability');
    const equityItems = balanceSheetData.filter(i => i.category === 'Equity');

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
                    <h1 className="text-3xl font-bold tracking-tight">Balance Sheet</h1>
                    <p className="text-muted-foreground">
                        Statement of Financial Position
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
                                {currentAssets.length > 0 ? (
                                    currentAssets.map(item => <AccountRow key={item.id} item={item} />)
                                ) : (
                                    <TableRow><TableCell colSpan={3} className="text-muted-foreground italic">No current assets</TableCell></TableRow>
                                )}
                                <SectionTotal title="Total Current Assets" amount={totalCurrentAssets} />

                                <TableRow className="h-4"><TableCell colSpan={3}></TableCell></TableRow>
                                <SectionHeader title="Non-Current Assets" />
                                {nonCurrentAssets.length > 0 ? (
                                    nonCurrentAssets.map(item => <AccountRow key={item.id} item={item} />)
                                ) : (
                                    <TableRow><TableCell colSpan={3} className="text-muted-foreground italic">No non-current assets</TableCell></TableRow>
                                )}
                                <SectionTotal title="Total Non-Current Assets" amount={totalNonCurrentAssets} />

                                <TableRow className="h-4"><TableCell colSpan={3}></TableCell></TableRow>
                                <SectionTotal title="TOTAL ASSETS" amount={totalAssets} isMain />


                                {/* LIABILITIES */}
                                <TableRow className="h-8"><TableCell colSpan={3}></TableCell></TableRow>
                                <TableRow className="bg-slate-100 hover:bg-slate-100"><TableCell colSpan={3} className="font-bold text-lg py-4">LIABILITIES</TableCell></TableRow>

                                <SectionHeader title="Current Liabilities" />
                                {currentLiabilities.length > 0 ? (
                                    currentLiabilities.map(item => <AccountRow key={item.id} item={item} />)
                                ) : (
                                    <TableRow><TableCell colSpan={3} className="text-muted-foreground italic">No current liabilities</TableCell></TableRow>
                                )}
                                <SectionTotal title="Total Current Liabilities" amount={totalCurrentLiabilities} />

                                <TableRow className="h-4"><TableCell colSpan={3}></TableCell></TableRow>
                                <SectionHeader title="Non-Current Liabilities" />
                                {nonCurrentLiabilities.length > 0 ? (
                                    nonCurrentLiabilities.map(item => <AccountRow key={item.id} item={item} />)
                                ) : (
                                    <TableRow><TableCell colSpan={3} className="text-muted-foreground italic">No non-current liabilities</TableCell></TableRow>
                                )}
                                <SectionTotal title="Total Non-Current Liabilities" amount={totalNonCurrentLiabilities} />

                                <TableRow className="h-4"><TableCell colSpan={3}></TableCell></TableRow>
                                <SectionTotal title="Total Liabilities" amount={totalLiabilities} />


                                {/* EQUITY */}
                                <TableRow className="h-8"><TableCell colSpan={3}></TableCell></TableRow>
                                <TableRow className="bg-slate-100 hover:bg-slate-100"><TableCell colSpan={3} className="font-bold text-lg py-4">EQUITY</TableCell></TableRow>

                                {equityItems.length > 0 ? (
                                    equityItems.map(item => <AccountRow key={item.id} item={item} />)
                                ) : (
                                    <TableRow><TableCell colSpan={3} className="text-muted-foreground italic">No equity accounts</TableCell></TableRow>
                                )}
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
