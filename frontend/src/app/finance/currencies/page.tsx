'use client';

import React, { useState, useMemo } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { DollarSign, Plus, Edit, Power, Search, Globe } from 'lucide-react';
import { cn } from '@/lib/utils';
import { ISO_4217_CURRENCIES, type ISO4217Currency } from '@/lib/iso-4217-currencies';

// Local currency type for UI (simplified from full Currency type)
interface LocalCurrency {
    id: string;
    code: string;
    name: string;
    symbol: string;
    decimalPlaces: number;
    isActive: boolean;
    isBaseCurrency: boolean;
}

// MOCK DATA
const MOCK_CURRENCIES: LocalCurrency[] = [
    {
        id: 'curr-1',
        code: 'GHS',
        name: 'Ghana Cedi',
        symbol: '₵',
        decimalPlaces: 2,
        isActive: true,
        isBaseCurrency: true,
    },
    {
        id: 'curr-2',
        code: 'USD',
        name: 'US Dollar',
        symbol: '$',
        decimalPlaces: 2,
        isActive: true,
        isBaseCurrency: false,
    },
    {
        id: 'curr-3',
        code: 'EUR',
        name: 'Euro',
        symbol: '€',
        decimalPlaces: 2,
        isActive: true,
        isBaseCurrency: false,
    },
    {
        id: 'curr-4',
        code: 'GBP',
        name: 'British Pound',
        symbol: '£',
        decimalPlaces: 2,
        isActive: false,
        isBaseCurrency: false,
    },
];

export default function CurrenciesPage() {
    const [currencies, setCurrencies] = useState<LocalCurrency[]>(MOCK_CURRENCIES);
    const [searchTerm, setSearchTerm] = useState('');
    const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
    const [editingCurrency, setEditingCurrency] = useState<LocalCurrency | null>(null);
    const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);

    // Currency selector state
    const [currencySearch, setCurrencySearch] = useState('');
    const [selectedISO, setSelectedISO] = useState<ISO4217Currency | null>(null);

    // Form state
    const [formData, setFormData] = useState({
        symbol: '',
        decimalPlaces: 2,
    });

    // Filter out currencies already in system
    const availableCurrencies = useMemo(() => {
        const existingCodes = new Set(currencies.map(c => c.code));
        return ISO_4217_CURRENCIES.filter(c => !existingCodes.has(c.code));
    }, [currencies]);

    // Filter available currencies by search term
    const filteredAvailableCurrencies = useMemo(() => {
        if (!currencySearch) return availableCurrencies;
        const search = currencySearch.toLowerCase();
        return availableCurrencies.filter(
            c => c.code.toLowerCase().includes(search) ||
                c.name.toLowerCase().includes(search)
        );
    }, [availableCurrencies, currencySearch]);

    const filteredCurrencies = currencies.filter(
        (currency) =>
            currency.code.toLowerCase().includes(searchTerm.toLowerCase()) ||
            currency.name.toLowerCase().includes(searchTerm.toLowerCase())
    );

    const handleSelectCurrency = (iso: ISO4217Currency) => {
        setSelectedISO(iso);
        setFormData({
            symbol: iso.symbol,
            decimalPlaces: iso.decimalPlaces,
        });
    };

    const handleCreate = () => {
        if (!selectedISO) return;

        const newCurrency: LocalCurrency = {
            id: `curr-${Date.now()}`,
            code: selectedISO.code,
            name: selectedISO.name,
            symbol: formData.symbol,
            decimalPlaces: formData.decimalPlaces,
            isActive: true,
            isBaseCurrency: false,
        };
        setCurrencies([...currencies, newCurrency]);
        setIsCreateDialogOpen(false);
        resetForm();
    };

    const handleUpdate = () => {
        if (!editingCurrency) return;
        setCurrencies(
            currencies.map((c) =>
                c.id === editingCurrency.id
                    ? {
                        ...c,
                        symbol: formData.symbol,
                        decimalPlaces: formData.decimalPlaces,
                    }
                    : c
            )
        );
        setIsEditDialogOpen(false);
        setEditingCurrency(null);
        resetForm();
    };

    const handleToggleActive = (id: string) => {
        setCurrencies(
            currencies.map((c) =>
                c.id === id
                    ? { ...c, isActive: !c.isActive }
                    : c
            )
        );
    };

    const resetForm = () => {
        setSelectedISO(null);
        setCurrencySearch('');
        setFormData({
            symbol: '',
            decimalPlaces: 2,
        });
    };

    const openEditDialog = (currency: LocalCurrency) => {
        setEditingCurrency(currency);
        setFormData({
            symbol: currency.symbol,
            decimalPlaces: currency.decimalPlaces,
        });
        setIsEditDialogOpen(true);
    };

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                        <DollarSign className="h-8 w-8" />
                        Currencies
                    </h1>
                    <p className="text-muted-foreground">
                        Manage currencies for multi-currency transactions
                    </p>
                </div>
                <Dialog open={isCreateDialogOpen} onOpenChange={(open) => { setIsCreateDialogOpen(open); if (!open) resetForm(); }}>
                    <DialogTrigger asChild>
                        <Button>
                            <Plus className="mr-2 h-4 w-4" />
                            Add Currency
                        </Button>
                    </DialogTrigger>
                    <DialogContent className="sm:max-w-[500px]">
                        <DialogHeader>
                            <DialogTitle className="flex items-center gap-2">
                                <Globe className="h-5 w-5" />
                                Add New Currency
                            </DialogTitle>
                            <DialogDescription>
                                Select from ISO 4217 standard currencies
                            </DialogDescription>
                        </DialogHeader>
                        <div className="space-y-4 py-4">
                            {/* Currency Selector */}
                            <div className="space-y-2">
                                <Label>Select Currency (ISO 4217)</Label>
                                <div className="relative">
                                    <Search className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
                                    <Input
                                        placeholder="Search currencies..."
                                        value={currencySearch}
                                        onChange={(e) => setCurrencySearch(e.target.value)}
                                        className="pl-10 mb-2"
                                    />
                                    <div className="border rounded-md max-h-[200px] overflow-y-auto">
                                        {filteredAvailableCurrencies.length === 0 ? (
                                            <div className="p-4 text-center text-muted-foreground">
                                                No currencies found
                                            </div>
                                        ) : (
                                            filteredAvailableCurrencies.map((iso) => (
                                                <div
                                                    key={iso.code}
                                                    className={cn(
                                                        "flex items-center justify-between p-3 cursor-pointer hover:bg-muted transition-colors",
                                                        selectedISO?.code === iso.code && "bg-primary/10 border-l-2 border-primary"
                                                    )}
                                                    onClick={() => handleSelectCurrency(iso)}
                                                >
                                                    <div className="flex items-center gap-3">
                                                        <span className="font-mono font-bold w-12">{iso.code}</span>
                                                        <span>{iso.name}</span>
                                                    </div>
                                                    <span className="text-muted-foreground">{iso.symbol}</span>
                                                </div>
                                            ))
                                        )}
                                    </div>
                                </div>
                                {availableCurrencies.length < ISO_4217_CURRENCIES.length && (
                                    <p className="text-xs text-muted-foreground">
                                        {ISO_4217_CURRENCIES.length - availableCurrencies.length} currencies already in system
                                    </p>
                                )}
                            </div>

                            {/* Auto-populated fields (shown when currency selected) */}
                            {selectedISO && (
                                <>
                                    <div className="grid grid-cols-2 gap-4">
                                        <div className="space-y-2">
                                            <Label>Currency Code</Label>
                                            <Input
                                                value={selectedISO.code}
                                                disabled
                                                className="font-mono font-bold bg-muted"
                                            />
                                        </div>
                                        <div className="space-y-2">
                                            <Label>Currency Name</Label>
                                            <Input
                                                value={selectedISO.name}
                                                disabled
                                                className="bg-muted"
                                            />
                                        </div>
                                    </div>

                                    <div className="grid grid-cols-2 gap-4">
                                        <div className="space-y-2">
                                            <Label htmlFor="symbol">Symbol</Label>
                                            <Input
                                                id="symbol"
                                                value={formData.symbol}
                                                onChange={(e) => setFormData({ ...formData, symbol: e.target.value })}
                                                placeholder="$"
                                            />
                                            <p className="text-xs text-muted-foreground">
                                                Default: {selectedISO.symbol}
                                            </p>
                                        </div>
                                        <div className="space-y-2">
                                            <Label htmlFor="decimalPlaces">Decimal Places</Label>
                                            <Input
                                                id="decimalPlaces"
                                                type="number"
                                                min="0"
                                                max="4"
                                                value={formData.decimalPlaces}
                                                onChange={(e) => setFormData({ ...formData, decimalPlaces: parseInt(e.target.value) || 0 })}
                                            />
                                            <p className="text-xs text-muted-foreground">
                                                Default: {selectedISO.decimalPlaces}
                                            </p>
                                        </div>
                                    </div>
                                </>
                            )}
                        </div>
                        <DialogFooter>
                            <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                                Cancel
                            </Button>
                            <Button onClick={handleCreate} disabled={!selectedISO}>
                                Create Currency
                            </Button>
                        </DialogFooter>
                    </DialogContent>
                </Dialog>
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
                        <BreadcrumbPage>Currencies</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Search */}
            <Card>
                <CardHeader>
                    <CardTitle>Search Currencies</CardTitle>
                </CardHeader>
                <CardContent>
                    <div className="relative">
                        <Search className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
                        <Input
                            placeholder="Search by code or name..."
                            value={searchTerm}
                            onChange={(e) => setSearchTerm(e.target.value)}
                            className="pl-10"
                        />
                    </div>
                </CardContent>
            </Card>

            {/* Currencies Table */}
            <Card>
                <CardHeader>
                    <CardTitle>Currencies ({filteredCurrencies.length})</CardTitle>
                    <CardDescription>
                        Manage your organization's currencies
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    <div className="rounded-md border">
                        <table className="w-full">
                            <thead>
                                <tr className="border-b bg-muted/50">
                                    <th className="p-4 text-left font-medium">Code</th>
                                    <th className="p-4 text-left font-medium">Name</th>
                                    <th className="p-4 text-left font-medium">Symbol</th>
                                    <th className="p-4 text-left font-medium">Decimals</th>
                                    <th className="p-4 text-left font-medium">Status</th>
                                    <th className="p-4 text-right font-medium">Actions</th>
                                </tr>
                            </thead>
                            <tbody>
                                {filteredCurrencies.map((currency) => (
                                    <tr key={currency.id} className="border-b hover:bg-muted/50">
                                        <td className="p-4 font-mono font-semibold">
                                            {currency.code}
                                            {currency.isBaseCurrency && (
                                                <Badge variant="outline" className="ml-2">
                                                    Base
                                                </Badge>
                                            )}
                                        </td>
                                        <td className="p-4">{currency.name}</td>
                                        <td className="p-4 text-lg">{currency.symbol}</td>
                                        <td className="p-4">{currency.decimalPlaces}</td>
                                        <td className="p-4">
                                            <Badge variant={currency.isActive ? 'default' : 'secondary'}>
                                                {currency.isActive ? 'Active' : 'Inactive'}
                                            </Badge>
                                        </td>
                                        <td className="p-4 text-right space-x-2">
                                            <Button
                                                variant="ghost"
                                                size="sm"
                                                onClick={() => openEditDialog(currency)}
                                            >
                                                <Edit className="h-4 w-4" />
                                            </Button>
                                            <Button
                                                variant="ghost"
                                                size="sm"
                                                onClick={() => handleToggleActive(currency.id)}
                                                disabled={currency.isBaseCurrency}
                                            >
                                                <Power className="h-4 w-4" />
                                            </Button>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </CardContent>
            </Card>

            {/* Edit Dialog */}
            <Dialog open={isEditDialogOpen} onOpenChange={(open) => { setIsEditDialogOpen(open); if (!open) { setEditingCurrency(null); resetForm(); } }}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>Edit Currency</DialogTitle>
                        <DialogDescription>
                            Update currency settings (code and name are ISO 4217 locked)
                        </DialogDescription>
                    </DialogHeader>
                    {editingCurrency && (
                        <div className="space-y-4 py-4">
                            <div className="grid grid-cols-2 gap-4">
                                <div className="space-y-2">
                                    <Label>Currency Code</Label>
                                    <Input
                                        value={editingCurrency.code}
                                        disabled
                                        className="font-mono font-bold bg-muted"
                                    />
                                </div>
                                <div className="space-y-2">
                                    <Label>Currency Name</Label>
                                    <Input
                                        value={editingCurrency.name}
                                        disabled
                                        className="bg-muted"
                                    />
                                </div>
                            </div>
                            <div className="grid grid-cols-2 gap-4">
                                <div className="space-y-2">
                                    <Label htmlFor="edit-symbol">Symbol</Label>
                                    <Input
                                        id="edit-symbol"
                                        value={formData.symbol}
                                        onChange={(e) => setFormData({ ...formData, symbol: e.target.value })}
                                    />
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="edit-decimalPlaces">Decimal Places</Label>
                                    <Input
                                        id="edit-decimalPlaces"
                                        type="number"
                                        min="0"
                                        max="4"
                                        value={formData.decimalPlaces}
                                        onChange={(e) => setFormData({ ...formData, decimalPlaces: parseInt(e.target.value) || 0 })}
                                    />
                                </div>
                            </div>
                        </div>
                    )}
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setIsEditDialogOpen(false)}>
                            Cancel
                        </Button>
                        <Button onClick={handleUpdate}>Update Currency</Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}
