'use client';

import React, { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { DollarSign, Plus, Edit, Power, Search } from 'lucide-react';
import type { Currency } from '@/types/finance';

// MOCK DATA
const MOCK_CURRENCIES: Currency[] = [
    {
        id: 'curr-1',
        tenantId: 'tenant-1',
        code: 'GHS',
        name: 'Ghana Cedi',
        symbol: '₵',
        decimalPlaces: 2,
        isActive: true,
        isBaseCurrency: true,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'curr-2',
        tenantId: 'tenant-1',
        code: 'USD',
        name: 'US Dollar',
        symbol: '$',
        decimalPlaces: 2,
        isActive: true,
        isBaseCurrency: false,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'curr-3',
        tenantId: 'tenant-1',
        code: 'EUR',
        name: 'Euro',
        symbol: '€',
        decimalPlaces: 2,
        isActive: true,
        isBaseCurrency: false,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'curr-4',
        tenantId: 'tenant-1',
        code: 'GBP',
        name: 'British Pound',
        symbol: '£',
        decimalPlaces: 2,
        isActive: false,
        isBaseCurrency: false,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
];

export default function CurrenciesPage() {
    const [currencies, setCurrencies] = useState<Currency[]>(MOCK_CURRENCIES);
    const [searchTerm, setSearchTerm] = useState('');
    const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
    const [editingCurrency, setEditingCurrency] = useState<Currency | null>(null);
    const [formData, setFormData] = useState({
        code: '',
        name: '',
        symbol: '',
        decimalPlaces: 2,
    });

    const filteredCurrencies = currencies.filter(
        (currency) =>
            currency.code.toLowerCase().includes(searchTerm.toLowerCase()) ||
            currency.name.toLowerCase().includes(searchTerm.toLowerCase())
    );

    const handleCreate = () => {
        const newCurrency: Currency = {
            id: `curr-${Date.now()}`,
            tenantId: 'tenant-1',
            code: formData.code.toUpperCase(),
            name: formData.name,
            symbol: formData.symbol,
            decimalPlaces: formData.decimalPlaces,
            isActive: true,
            isBaseCurrency: false,
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
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
                        code: formData.code.toUpperCase(),
                        name: formData.name,
                        symbol: formData.symbol,
                        decimalPlaces: formData.decimalPlaces,
                        updatedAt: new Date().toISOString(),
                    }
                    : c
            )
        );
        setEditingCurrency(null);
        resetForm();
    };

    const handleToggleActive = (id: string) => {
        setCurrencies(
            currencies.map((c) =>
                c.id === id
                    ? { ...c, isActive: !c.isActive, updatedAt: new Date().toISOString() }
                    : c
            )
        );
    };

    const resetForm = () => {
        setFormData({
            code: '',
            name: '',
            symbol: '',
            decimalPlaces: 2,
        });
    };

    const openEditDialog = (currency: Currency) => {
        setEditingCurrency(currency);
        setFormData({
            code: currency.code,
            name: currency.name,
            symbol: currency.symbol,
            decimalPlaces: currency.decimalPlaces,
        });
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
                    <p className="text-sm text-orange-600 mt-1">
                        ⚠️ DEMO MODE - Using mock data (backend not connected)
                    </p>
                </div>
                <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
                    <DialogTrigger asChild>
                        <Button onClick={() => { resetForm(); setEditingCurrency(null); }}>
                            <Plus className="mr-2 h-4 w-4" />
                            Add Currency
                        </Button>
                    </DialogTrigger>
                    <DialogContent>
                        <DialogHeader>
                            <DialogTitle>Add New Currency</DialogTitle>
                            <DialogDescription>
                                Create a new currency for multi-currency transactions
                            </DialogDescription>
                        </DialogHeader>
                        <div className="space-y-4 py-4">
                            <div className="space-y-2">
                                <Label htmlFor="code">Currency Code</Label>
                                <Input
                                    id="code"
                                    placeholder="USD"
                                    value={formData.code}
                                    onChange={(e) => setFormData({ ...formData, code: e.target.value })}
                                    maxLength={3}
                                />
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="name">Currency Name</Label>
                                <Input
                                    id="name"
                                    placeholder="US Dollar"
                                    value={formData.name}
                                    onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                                />
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="symbol">Symbol</Label>
                                <Input
                                    id="symbol"
                                    placeholder="$"
                                    value={formData.symbol}
                                    onChange={(e) => setFormData({ ...formData, symbol: e.target.value })}
                                />
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
                            </div>
                        </div>
                        <DialogFooter>
                            <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                                Cancel
                            </Button>
                            <Button onClick={handleCreate}>Create Currency</Button>
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
                                            <Dialog>
                                                <DialogTrigger asChild>
                                                    <Button
                                                        variant="ghost"
                                                        size="sm"
                                                        onClick={() => openEditDialog(currency)}
                                                    >
                                                        <Edit className="h-4 w-4" />
                                                    </Button>
                                                </DialogTrigger>
                                                <DialogContent>
                                                    <DialogHeader>
                                                        <DialogTitle>Edit Currency</DialogTitle>
                                                        <DialogDescription>
                                                            Update currency details
                                                        </DialogDescription>
                                                    </DialogHeader>
                                                    <div className="space-y-4 py-4">
                                                        <div className="space-y-2">
                                                            <Label htmlFor="edit-code">Currency Code</Label>
                                                            <Input
                                                                id="edit-code"
                                                                value={formData.code}
                                                                onChange={(e) => setFormData({ ...formData, code: e.target.value })}
                                                                maxLength={3}
                                                                disabled
                                                            />
                                                        </div>
                                                        <div className="space-y-2">
                                                            <Label htmlFor="edit-name">Currency Name</Label>
                                                            <Input
                                                                id="edit-name"
                                                                value={formData.name}
                                                                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                                                            />
                                                        </div>
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
                                                    <DialogFooter>
                                                        <Button variant="outline" onClick={() => setEditingCurrency(null)}>
                                                            Cancel
                                                        </Button>
                                                        <Button onClick={handleUpdate}>Update Currency</Button>
                                                    </DialogFooter>
                                                </DialogContent>
                                            </Dialog>
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
        </div>
    );
}
