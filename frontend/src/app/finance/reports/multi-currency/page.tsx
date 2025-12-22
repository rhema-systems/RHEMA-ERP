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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { FinanceSettings } from '@/types/finance';

interface MultiCurrencyItem {
    id: string;
    accountCode: string;
    accountName: string;
    currency: string;
    foreignAmount: number;
    exchangeRate: number;
    baseAmount: number;
}

export default function MultiCurrencyReportPage() {
    const router = useRouter();
    const [asOfDate, setAsOfDate] = useState(new Date().toISOString().split('T')[0]);
    const [selectedCurrency, setSelectedCurrency] = useState('ALL');

    // Data loading state
    const [loading, setLoading] = useState(true);
    const [settings, setSettings] = useState<FinanceSettings | null>(null);
    const [multiCurrencyData, setMultiCurrencyData] = useState<MultiCurrencyItem[]>([]);

    useEffect(() => {
        loadData();
    }, []);

    const loadData = async () => {
        try {
            setLoading(true);

            const settingsData = await financeDataService.getFinanceSettings();
            setSettings(settingsData);

            const accounts = await financeDataService.getAccounts({ coaType: settingsData.coaType });

            // Filter for multi-currency accounts and create display items
            const items: MultiCurrencyItem[] = [];

            accounts.forEach(account => {
                if (account.isMultiCurrency) {
                    // Mock exchange rate based on currency
                    const rate = account.currencyCode === 'USD' ? 12.50 :
                        account.currencyCode === 'EUR' ? 13.20 :
                            account.currencyCode === 'GBP' ? 15.80 : 1.00;

                    const foreignAmount = account.currentBalance || 0;
                    const baseAmount = foreignAmount * rate;

                    items.push({
                        id: account.id,
                        accountCode: account.accountCode,
                        accountName: account.accountName,
                        currency: account.currencyCode,
                        foreignAmount: foreignAmount,
                        exchangeRate: rate,
                        baseAmount: baseAmount,
                    });
                }
            });

            // If no multi-currency accounts found, show sample data
            if (items.length === 0) {
                items.push(
                    { id: 'sample-1', accountCode: '1001', accountName: 'Cash (USD)', currency: 'USD', foreignAmount: 5000, exchangeRate: 12.5, baseAmount: 62500 },
                    { id: 'sample-2', accountCode: '1002', accountName: 'Cash (EUR)', currency: 'EUR', foreignAmount: 2000, exchangeRate: 13.2, baseAmount: 26400 },
                    { id: 'sample-3', accountCode: '1100', accountName: 'Accounts Receivable (USD)', currency: 'USD', foreignAmount: 1500, exchangeRate: 12.5, baseAmount: 18750 },
                );
            }

            setMultiCurrencyData(items);
        } catch (error) {
            console.error('Error loading multi-currency data:', error);
        } finally {
            setLoading(false);
        }
    };

    // Filter Data
    const filteredData = selectedCurrency === 'ALL'
        ? multiCurrencyData
        : multiCurrencyData.filter(item => item.currency === selectedCurrency);

    // Calculate Totals
    const totalBaseAmount = filteredData.reduce((sum, item) => sum + item.baseAmount, 0);

    // Get unique currencies for filter
    const uniqueCurrencies = [...new Set(multiCurrencyData.map(item => item.currency))];

    const formatMoney = (amount: number, currency: string = 'GHS') => {
        return new Intl.NumberFormat('en-GH', {
            style: 'currency',
            currency: currency,
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
                    <h1 className="text-3xl font-bold tracking-tight">Multi-Currency Detail</h1>
                    <p className="text-muted-foreground">
                        Foreign currency balances and base equivalents
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
                        <BreadcrumbPage>Multi-Currency Detail</BreadcrumbPage>
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
                            <Label>Filter Currency</Label>
                            <Select value={selectedCurrency} onValueChange={setSelectedCurrency}>
                                <SelectTrigger>
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="ALL">All Currencies</SelectItem>
                                    {uniqueCurrencies.map(curr => (
                                        <SelectItem key={curr} value={curr}>{curr}</SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>
                    </div>
                </CardContent>
            </Card>

            {/* Report Table */}
            <Card>
                <CardHeader className="pb-2 text-center border-b">
                    <CardTitle className="text-xl">Multi-Currency Detail Report</CardTitle>
                    <p className="text-sm text-muted-foreground">
                        As of {new Date(asOfDate).toLocaleDateString()}
                    </p>
                </CardHeader>
                <CardContent className="pt-6">
                    <div className="rounded-md border">
                        <Table>
                            <TableHeader>
                                <TableRow className="bg-muted/50">
                                    <TableHead className={settings?.coaType === 'Segmented' ? 'w-[180px]' : 'w-[120px]'}>Account</TableHead>
                                    <TableHead className="text-center">Currency</TableHead>
                                    <TableHead className="text-right">Foreign Amount</TableHead>
                                    <TableHead className="text-right">Exchange Rate</TableHead>
                                    <TableHead className="text-right">Base Equivalent (GHS)</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {filteredData.length > 0 ? (
                                    filteredData.map((item) => (
                                        <TableRow key={item.id}>
                                            <TableCell>
                                                <div className="font-medium font-mono text-sm">{item.accountCode}</div>
                                                <div className="text-sm text-muted-foreground">{item.accountName}</div>
                                            </TableCell>
                                            <TableCell className="text-center">
                                                <span className="inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-semibold transition-colors focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2 border-transparent bg-secondary text-secondary-foreground hover:bg-secondary/80">
                                                    {item.currency}
                                                </span>
                                            </TableCell>
                                            <TableCell className="text-right font-mono">
                                                {formatMoney(item.foreignAmount, item.currency)}
                                            </TableCell>
                                            <TableCell className="text-right text-muted-foreground">
                                                {item.exchangeRate.toFixed(4)}
                                            </TableCell>
                                            <TableCell className="text-right font-bold">
                                                {formatMoney(item.baseAmount, 'GHS')}
                                            </TableCell>
                                        </TableRow>
                                    ))
                                ) : (
                                    <TableRow>
                                        <TableCell colSpan={5} className="text-center text-muted-foreground py-8">
                                            No multi-currency accounts found
                                        </TableCell>
                                    </TableRow>
                                )}
                            </TableBody>
                            <tfoot className="bg-muted/50 font-bold">
                                <TableRow>
                                    <TableCell colSpan={4} className="text-right">TOTAL BASE EQUIVALENT:</TableCell>
                                    <TableCell className="text-right text-blue-600">{formatMoney(totalBaseAmount, 'GHS')}</TableCell>
                                </TableRow>
                            </tfoot>
                        </Table>
                    </div>
                </CardContent>
            </Card>
        </div>
    );
}
