'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Download, Loader2, Printer, Search } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { Account, FinanceSettings } from '@/types/finance';

// Derived type for trial balance display
interface TrialBalanceItem {
    id: string;
    accountCode: string;
    accountName: string;
    accountType: string;
    openingBalance: number;
    periodDebit: number;
    periodCredit: number;
    currency: string;
    isSegmented: boolean;
}

export default function TrialBalancePage() {
    const router = useRouter();
    const [asOfDate, setAsOfDate] = useState(new Date().toISOString().split('T')[0]);
    const [currency, setCurrency] = useState('GHS');
    const [hideZeroBalances, setHideZeroBalances] = useState(true);
    const [searchTerm, setSearchTerm] = useState('');

    // Data loading state
    const [loading, setLoading] = useState(true);
    const [settings, setSettings] = useState<FinanceSettings | null>(null);
    const [trialBalanceData, setTrialBalanceData] = useState<TrialBalanceItem[]>([]);

    // Load settings and accounts on mount
    useEffect(() => {
        loadData();
    }, []);

    const loadData = async () => {
        try {
            setLoading(true);

            // Load settings first to get COA type
            const settingsData = await financeDataService.getFinanceSettings();
            setSettings(settingsData);

            // Load accounts based on COA type
            const accounts = await financeDataService.getAccounts({ coaType: settingsData.coaType });

            // Transform accounts to trial balance items with mock period activity
            const items: TrialBalanceItem[] = accounts.map(account => ({
                id: account.id,
                accountCode: account.accountCode,
                accountName: account.accountName,
                accountType: account.accountType,
                openingBalance: account.currentBalance || 0,
                // Mock period activity - in real implementation this would come from journal entries
                periodDebit: generateMockDebit(account),
                periodCredit: generateMockCredit(account),
                currency: account.currencyCode,
                isSegmented: account.isSegmented || false,
            }));

            setTrialBalanceData(items);
        } catch (error) {
            console.error('Error loading trial balance data:', error);
        } finally {
            setLoading(false);
        }
    };

    // Generate mock period debits based on account type
    const generateMockDebit = (account: Account): number => {
        const balance = Math.abs(account.currentBalance || 0);
        switch (account.accountType) {
            case 'Asset': return Math.round(balance * 0.15);
            case 'Expense': return Math.round(balance * 0.12);
            case 'Liability': return Math.round(balance * 0.08);
            case 'Revenue': return Math.round(balance * 0.02);
            default: return 0;
        }
    };

    // Generate mock period credits based on account type
    const generateMockCredit = (account: Account): number => {
        const balance = Math.abs(account.currentBalance || 0);
        switch (account.accountType) {
            case 'Asset': return Math.round(balance * 0.05);
            case 'Expense': return Math.round(balance * 0.01);
            case 'Liability': return Math.round(balance * 0.12);
            case 'Revenue': return Math.round(balance * 0.18);
            default: return 0;
        }
    };

    // Filter Logic
    const filteredData = trialBalanceData.filter(item => {
        const closingBalance = item.openingBalance + item.periodDebit - item.periodCredit;
        const matchesSearch = item.accountName.toLowerCase().includes(searchTerm.toLowerCase()) ||
            item.accountCode.toLowerCase().includes(searchTerm.toLowerCase());
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
                    <h1 className="text-3xl font-bold tracking-tight">Trial Balance</h1>
                    <p className="text-muted-foreground">
                        Statement of all ledger account balances
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
                            Currency: {currency} | {filteredData.length} accounts
                        </span>
                    </div>
                </CardHeader>
                <CardContent>
                    <div className="rounded-md border">
                        <Table>
                            <TableHeader>
                                <TableRow className="bg-muted/50">
                                    <TableHead className={settings?.coaType === 'Segmented' ? 'w-[150px]' : 'w-[100px]'}>Code</TableHead>
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
                                            <TableCell className="font-medium font-mono text-sm">
                                                {item.accountCode}
                                            </TableCell>
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
