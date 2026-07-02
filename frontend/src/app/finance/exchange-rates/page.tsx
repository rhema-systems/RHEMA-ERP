'use client';

import React, { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { TrendingUp, Plus, Edit, Upload, Filter, LineChart } from 'lucide-react';
import type { ExchangeRate, ExchangeRateType } from '@/types/finance';
import Link from 'next/link';



// MOCK DATA
const MOCK_EXCHANGE_RATES: ExchangeRate[] = [
    {
        id: '1',
        baseCurrencyCode: 'USD',
        targetCurrencyCode: 'GHS',
        rate: 12.5,
        effectiveDate: '2024-03-15T00:00:00Z',
        rateType: 'Daily',
        rateSource: 'Bank of Ghana',
        isActive: true,
        createdAt: '2024-03-15T08:00:00Z',
        updatedAt: '2024-03-15T08:00:00Z',
    },
    {
        id: '2',
        baseCurrencyCode: 'GBP',
        targetCurrencyCode: 'GHS',
        rate: 15.8,
        effectiveDate: '2024-03-15T00:00:00Z',
        rateType: 'Daily',
        rateSource: 'Bank of Ghana',
        isActive: true,
        createdAt: '2024-03-15T08:00:00Z',
        updatedAt: '2024-03-15T08:00:00Z',
    },
    {
        id: '3',
        baseCurrencyCode: 'EUR',
        targetCurrencyCode: 'GHS',
        rate: 13.6,
        effectiveDate: '2024-03-15T00:00:00Z',
        rateType: 'Daily',
        rateSource: 'Bank of Ghana',
        isActive: true,
        createdAt: '2024-03-15T08:00:00Z',
        updatedAt: '2024-03-15T08:00:00Z',
    },
    {
        id: '4',
        baseCurrencyCode: 'USD',
        targetCurrencyCode: 'GHS',
        rate: 12.8,
        effectiveDate: '2024-03-15T00:00:00Z',
        rateType: 'Spot',
        rateSource: 'Forex Bureau',
        isActive: true,
        createdAt: '2024-03-15T09:00:00Z',
        updatedAt: '2024-03-15T09:00:00Z',
    },
];

const MOCK_CURRENCIES = ['GHS', 'USD', 'EUR', 'GBP'];

export default function ExchangeRatesPage() {
    const [rates, setRates] = useState<ExchangeRate[]>(MOCK_EXCHANGE_RATES);
    const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
    const [isBulkUploadOpen, setIsBulkUploadOpen] = useState(false);
    const [editingRate, setEditingRate] = useState<ExchangeRate | null>(null);
    const [filters, setFilters] = useState({
        fromCurrency: 'all',
        toCurrency: 'all',
        rateType: 'all',
    });
    const [formData, setFormData] = useState({
        baseCurrencyCode: 'USD',
        targetCurrencyCode: 'GHS',
        rate: '',
        rateType: 'Daily' as ExchangeRateType,
        effectiveDate: new Date().toISOString().split('T')[0],
        rateSource: '',
    });

    const filteredRates = rates.filter((rate) => {
        if (filters.fromCurrency !== 'all' && rate.baseCurrencyCode !== filters.fromCurrency) return false;
        if (filters.toCurrency !== 'all' && rate.targetCurrencyCode !== filters.toCurrency) return false;
        if (filters.rateType !== 'all' && rate.rateType !== filters.rateType) return false;
        return true;
    });

    const handleCreate = () => {
        const newRate: ExchangeRate = {
            id: Math.random().toString(36).substr(2, 9),
            baseCurrencyCode: formData.baseCurrencyCode,
            targetCurrencyCode: formData.targetCurrencyCode,
            rate: parseFloat(formData.rate),
            effectiveDate: new Date(formData.effectiveDate).toISOString(),
            rateType: formData.rateType,
            rateSource: formData.rateSource,
            isActive: true,
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
        };
        setRates([...rates, newRate]);
        setIsCreateDialogOpen(false);
        resetForm();
    };

    const handleUpdate = () => {
        if (!editingRate) return;
        setRates(
            rates.map((r) =>
                r.id === editingRate.id
                    ? {
                        ...r,
                        rate: parseFloat(formData.rate),
                        effectiveDate: new Date(formData.effectiveDate).toISOString(),
                        rateType: formData.rateType,
                        rateSource: formData.rateSource || r.rateSource,
                        updatedAt: new Date().toISOString(),
                    }
                    : r
            )
        );
        setEditingRate(null);
        resetForm();
    };

    const resetForm = () => {
        setFormData({
            baseCurrencyCode: 'USD',
            targetCurrencyCode: 'GHS',
            rate: '',
            effectiveDate: new Date().toISOString().split('T')[0],
            rateType: 'Daily',
            rateSource: '',
        });
    };

    const openEditDialog = (rate: ExchangeRate) => {
        setEditingRate(rate);
        setFormData({
            baseCurrencyCode: rate.baseCurrencyCode,
            targetCurrencyCode: rate.targetCurrencyCode,
            rate: rate.rate.toString(),
            effectiveDate: rate.effectiveDate.split('T')[0],
            rateType: rate.rateType,
            rateSource: rate.rateSource || '',
        });
    };

    const formatDate = (dateString: string) => {
        return new Date(dateString).toLocaleDateString('en-US', {
            year: 'numeric',
            month: 'short',
            day: 'numeric',
        });
    };

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                        <TrendingUp className="h-8 w-8" />
                        Exchange Rates
                    </h1>
                    <p className="text-muted-foreground">
                        Manage currency exchange rates for multi-currency transactions
                    </p>
                </div>
                <div className="flex gap-2">
                    <Button asChild variant="outline" className="mr-2">
                        <Link href="/finance/exchange-rates/trends">
                            <LineChart className="mr-2 h-4 w-4" />
                            Trend Analysis
                        </Link>
                    </Button>
                    <Dialog open={isBulkUploadOpen} onOpenChange={setIsBulkUploadOpen}>
                        <DialogTrigger asChild>
                            <Button variant="outline">
                                <Upload className="mr-2 h-4 w-4" />
                                Bulk Upload
                            </Button>
                        </DialogTrigger>
                        <DialogContent>
                            <DialogHeader>
                                <DialogTitle>Bulk Upload Exchange Rates</DialogTitle>
                                <DialogDescription>
                                    Upload multiple exchange rates from a CSV or Excel file
                                </DialogDescription>
                            </DialogHeader>
                            <div className="space-y-4 py-4">
                                <div className="border-2 border-dashed rounded-lg p-8 text-center">
                                    <Upload className="mx-auto h-12 w-12 text-muted-foreground" />
                                    <p className="mt-2 text-sm text-muted-foreground">
                                        Drag and drop your file here, or click to browse
                                    </p>
                                    <p className="text-xs text-muted-foreground mt-1">
                                        Supported formats: CSV, XLSX
                                    </p>
                                    <Button variant="outline" className="mt-4">
                                        Select File
                                    </Button>
                                </div>
                                <div className="text-sm text-muted-foreground">
                                    <p className="font-semibold mb-2">Required columns:</p>
                                    <ul className="list-disc list-inside space-y-1">
                                        <li>From Currency (e.g., USD)</li>
                                        <li>To Currency (e.g., GHS)</li>
                                        <li>Rate (e.g., 12.50)</li>
                                        <li>Effective Date (YYYY-MM-DD)</li>
                                        <li>Rate Type (Official/Market/Custom)</li>
                                    </ul>
                                </div>
                            </div>
                            <DialogFooter>
                                <Button variant="outline" onClick={() => setIsBulkUploadOpen(false)}>
                                    Cancel
                                </Button>
                                <Button disabled>Upload Rates</Button>
                            </DialogFooter>
                        </DialogContent>
                    </Dialog>
                    <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
                        <DialogTrigger asChild>
                            <Button onClick={() => { resetForm(); setEditingRate(null); }}>
                                <Plus className="mr-2 h-4 w-4" />
                                Add Rate
                            </Button>
                        </DialogTrigger>
                        <DialogContent>
                            <DialogHeader>
                                <DialogTitle>Add Exchange Rate</DialogTitle>
                                <DialogDescription>
                                    Create a new exchange rate entry
                                </DialogDescription>
                            </DialogHeader>
                            <div className="space-y-4 py-4">
                                <div className="grid grid-cols-2 gap-4">
                                    <div className="space-y-2">
                                        <Label htmlFor="baseCurrencyCode">From Currency</Label>
                                        <Select
                                            value={formData.baseCurrencyCode}
                                            onValueChange={(value) => setFormData({ ...formData, baseCurrencyCode: value })}
                                        >
                                            <SelectTrigger id="baseCurrencyCode">
                                                <SelectValue placeholder="Select currency" />
                                            </SelectTrigger>
                                            <SelectContent>
                                                <SelectItem value="USD">USD - US Dollar</SelectItem>
                                                <SelectItem value="EUR">EUR - Euro</SelectItem>
                                                <SelectItem value="GBP">GBP - British Pound</SelectItem>
                                            </SelectContent>
                                        </Select>
                                    </div>
                                    <div className="space-y-2">
                                        <Label htmlFor="targetCurrencyCode">To Currency</Label>
                                        <Select
                                            value={formData.targetCurrencyCode}
                                            onValueChange={(value) =>
                                                setFormData({ ...formData, targetCurrencyCode: value })
                                            }
                                        >
                                            <SelectTrigger id="targetCurrencyCode">
                                                <SelectValue placeholder="Select currency" />
                                            </SelectTrigger>
                                            <SelectContent>
                                                <SelectItem value="GHS">GHS - Ghanaian Cedi</SelectItem>
                                            </SelectContent>
                                        </Select>
                                    </div>
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="rate">Exchange Rate</Label>
                                    <Input
                                        id="rate"
                                        type="number"
                                        step="0.0001"
                                        placeholder="12.5000"
                                        value={formData.rate}
                                        onChange={(e) => setFormData({ ...formData, rate: e.target.value })}
                                    />
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="effectiveDate">Effective Date</Label>
                                    <Input
                                        id="effectiveDate"
                                        type="date"
                                        value={formData.effectiveDate}
                                        onChange={(e) => setFormData({ ...formData, effectiveDate: e.target.value })}
                                    />
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="rateType">Rate Type</Label>
                                    <Select
                                        value={formData.rateType}
                                        onValueChange={(value: ExchangeRateType) => setFormData({ ...formData, rateType: value })}
                                    >
                                        <SelectTrigger id="rateType">
                                            <SelectValue />
                                        </SelectTrigger>
                                        <SelectContent>
                                            <SelectItem value="Daily">Daily</SelectItem>
                                            <SelectItem value="Spot">Spot</SelectItem>
                                            <SelectItem value="Official">Official</SelectItem>
                                            <SelectItem value="Market">Market</SelectItem>
                                            <SelectItem value="Custom">Custom</SelectItem>
                                        </SelectContent>
                                    </Select>
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="rateSource">Source</Label>
                                    <Input
                                        id="rateSource"
                                        placeholder="e.g. Bank of Ghana"
                                        value={formData.rateSource}
                                        onChange={(e) => setFormData({ ...formData, rateSource: e.target.value })}
                                    />
                                </div>
                            </div>
                            <DialogFooter>
                                <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                                    Cancel
                                </Button>
                                <Button onClick={handleCreate}>Create Rate</Button>
                            </DialogFooter>
                        </DialogContent>
                    </Dialog>
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
                        <BreadcrumbPage>Exchange Rates</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Filters */}
            <Card>
                <CardHeader>
                    <CardTitle className="flex items-center gap-2">
                        <Filter className="h-5 w-5" />
                        Filters
                    </CardTitle>
                </CardHeader>
                <CardContent>
                    <div className="grid grid-cols-3 gap-4">
                        <div className="space-y-2">
                            <Label htmlFor="filterFrom">From Currency</Label>
                            <Select
                                value={filters.fromCurrency}
                                onValueChange={(value) => setFilters({ ...filters, fromCurrency: value })}
                            >
                                <SelectTrigger id="filterFrom">
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="all">All Currencies</SelectItem>
                                    {MOCK_CURRENCIES.map((curr) => (
                                        <SelectItem key={curr} value={curr}>{curr}</SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="filterTo">To Currency</Label>
                            <Select
                                value={filters.toCurrency}
                                onValueChange={(value) => setFilters({ ...filters, toCurrency: value })}
                            >
                                <SelectTrigger id="filterTo">
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="all">All Currencies</SelectItem>
                                    {MOCK_CURRENCIES.map((curr) => (
                                        <SelectItem key={curr} value={curr}>{curr}</SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="filterType">Rate Type</Label>
                            <Select
                                value={filters.rateType}
                                onValueChange={(value) => setFilters({ ...filters, rateType: value })}
                            >
                                <SelectTrigger id="filterType">
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="all">All Types</SelectItem>
                                    <SelectItem value="Daily">Daily</SelectItem>
                                    <SelectItem value="Spot">Spot</SelectItem>
                                    <SelectItem value="Official">Official</SelectItem>
                                    <SelectItem value="Market">Market</SelectItem>
                                    <SelectItem value="Custom">Custom</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>
                    </div>
                </CardContent>
            </Card>

            {/* Exchange Rates Table */}
            <Card>
                <CardHeader>
                    <CardTitle>Exchange Rates ({filteredRates.length})</CardTitle>
                    <CardDescription>
                        Historical and current exchange rates
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    <div className="rounded-md border">
                        <table className="w-full">
                            <thead>
                                <tr className="border-b bg-muted/50">
                                    <th className="p-4 text-left font-medium">Currency Pair</th>
                                    <th className="p-4 text-right font-medium">Rate</th>
                                    <th className="p-4 text-left font-medium">Effective Date</th>
                                    <th className="p-4 text-left font-medium">Type</th>
                                    <th className="p-4 text-left font-medium">Source</th>
                                    <th className="p-4 text-right font-medium">Actions</th>
                                </tr>
                            </thead>
                            <tbody>
                                {filteredRates.map((rate) => (
                                    <tr key={rate.id} className="border-b hover:bg-muted/50">
                                        <td className="p-4 font-mono font-semibold">{rate.baseCurrencyCode}/{rate.targetCurrencyCode}</td>
                                        <td className="p-4 text-right font-mono">{rate.rate.toFixed(4)}</td>
                                        <td className="p-4">{formatDate(rate.effectiveDate)}</td>
                                        <td className="p-4">
                                            <Badge variant={rate.rateType === 'Daily' ? 'default' : 'secondary'}>
                                                {rate.rateType}
                                            </Badge>
                                        </td>
                                        <td className="p-4 text-sm text-muted-foreground">{rate.rateSource}</td>
                                        <td className="p-4 text-right">
                                            <Dialog>
                                                <DialogTrigger asChild>
                                                    <Button
                                                        variant="ghost"
                                                        size="sm"
                                                        onClick={() => openEditDialog(rate)}
                                                    >
                                                        <Edit className="h-4 w-4" />
                                                    </Button>
                                                </DialogTrigger>
                                                <DialogContent>
                                                    <DialogHeader>
                                                        <DialogTitle>Edit Exchange Rate</DialogTitle>
                                                        <DialogDescription>
                                                            Update exchange rate details
                                                        </DialogDescription>
                                                    </DialogHeader>
                                                    <div className="space-y-4 py-4">
                                                        <div className="grid grid-cols-2 gap-4">
                                                            <div className="space-y-2">
                                                                <Label>From Currency</Label>
                                                                <Input value={formData.baseCurrencyCode} disabled />
                                                            </div>
                                                            <div className="space-y-2">
                                                                <Label>To Currency</Label>
                                                                <Input value={formData.targetCurrencyCode} disabled />
                                                            </div>
                                                        </div>
                                                        <div className="space-y-2">
                                                            <Label htmlFor="edit-rate">Exchange Rate</Label>
                                                            <Input
                                                                id="edit-rate"
                                                                type="number"
                                                                step="0.0001"
                                                                value={formData.rate}
                                                                onChange={(e) => setFormData({ ...formData, rate: e.target.value })}
                                                            />
                                                        </div>
                                                        <div className="space-y-2">
                                                            <Label htmlFor="edit-effectiveDate">Effective Date</Label>
                                                            <Input
                                                                id="edit-effectiveDate"
                                                                type="date"
                                                                value={formData.effectiveDate}
                                                                onChange={(e) => setFormData({ ...formData, effectiveDate: e.target.value })}
                                                            />
                                                        </div>
                                                        <div className="space-y-2">
                                                            <Label htmlFor="edit-rateType">Rate Type</Label>
                                                            <Select
                                                                value={formData.rateType}
                                                                onValueChange={(value: any) => setFormData({ ...formData, rateType: value })}
                                                            >
                                                                <SelectTrigger id="edit-rateType">
                                                                    <SelectValue />
                                                                </SelectTrigger>
                                                                <SelectContent>
                                                                    <SelectItem value="Official">Official</SelectItem>
                                                                    <SelectItem value="Market">Market</SelectItem>
                                                                    <SelectItem value="Custom">Custom</SelectItem>
                                                                </SelectContent>
                                                            </Select>
                                                        </div>
                                                        <div className="space-y-2">
                                                            <Label htmlFor="edit-source">Source</Label>
                                                            <Input
                                                                id="edit-source"
                                                                value={formData.rateSource}
                                                                onChange={(e) => setFormData({ ...formData, rateSource: e.target.value })}
                                                            />
                                                        </div>
                                                    </div>
                                                    <DialogFooter>
                                                        <Button variant="outline" onClick={() => setEditingRate(null)}>
                                                            Cancel
                                                        </Button>
                                                        <Button onClick={handleUpdate}>Update Rate</Button>
                                                    </DialogFooter>
                                                </DialogContent>
                                            </Dialog>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </CardContent>
            </Card>
        </div>
    );
}
