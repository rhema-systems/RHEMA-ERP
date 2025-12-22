'use client';

import React, { useState } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Calculator, Save, X, Trash2, TrendingUp, History } from 'lucide-react';
import Link from 'next/link';
import type { UnitType, UnitAccount, UnitAccountBalance } from '@/types/unit-accounts';

// MOCK DATA
const MOCK_UNIT_TYPES: UnitType[] = [
    { id: 'ut-1', code: 'EMP', name: 'Employees', decimalPlaces: 0, isActive: true, createdAt: '', createdBy: '' },
    { id: 'ut-2', code: 'SQFT', name: 'Square Footage', decimalPlaces: 2, isActive: true, createdAt: '', createdBy: '' },
];

const MOCK_ACCOUNTS: Record<string, UnitAccount> = {
    'ua-2': {
        id: 'ua-2',
        accountNumber: 'U-1100',
        name: 'Operations Department',
        description: 'Headcount for operations team',
        unitTypeId: 'ut-1',
        unitType: MOCK_UNIT_TYPES[0],
        parentAccountId: 'ua-1',
        accountLevel: 2,
        isPostingAccount: true,
        isActive: true,
        currentBalance: 45,
        createdAt: '2024-01-01T00:00:00Z',
        createdBy: 'admin',
    },
};

const MOCK_BALANCES: UnitAccountBalance[] = [
    { id: 'b1', unitAccountId: 'ua-2', fiscalYearId: 'fy-1', fiscalPeriodId: 'fp-12', openingBalance: 42, periodActivity: 3, closingBalance: 45 },
    { id: 'b2', unitAccountId: 'ua-2', fiscalYearId: 'fy-1', fiscalPeriodId: 'fp-11', openingBalance: 40, periodActivity: 2, closingBalance: 42 },
    { id: 'b3', unitAccountId: 'ua-2', fiscalYearId: 'fy-1', fiscalPeriodId: 'fp-10', openingBalance: 38, periodActivity: 2, closingBalance: 40 },
];

const PERIOD_NAMES: Record<string, string> = {
    'fp-12': 'December 2024',
    'fp-11': 'November 2024',
    'fp-10': 'October 2024',
};

export default function EditUnitAccountPage() {
    const router = useRouter();
    const params = useParams();
    const id = params.id as string;

    const existingAccount = MOCK_ACCOUNTS[id];

    const [formData, setFormData] = useState({
        name: existingAccount?.name || '',
        description: existingAccount?.description || '',
        isPostingAccount: existingAccount?.isPostingAccount ?? true,
        isActive: existingAccount?.isActive ?? true,
    });
    const [errors, setErrors] = useState<Record<string, string>>({});

    const validateForm = () => {
        const newErrors: Record<string, string> = {};

        if (!formData.name.trim()) {
            newErrors.name = 'Name is required';
        }

        setErrors(newErrors);
        return Object.keys(newErrors).length === 0;
    };

    const handleSubmit = (e: React.FormEvent) => {
        e.preventDefault();
        if (!validateForm()) return;

        console.log('Updating unit account:', { id, ...formData });
        alert('DEMO MODE: Unit account would be updated.');
        router.push('/finance/unit-accounts');
    };

    const handleDelete = () => {
        if (confirm('Are you sure you want to delete this unit account?')) {
            console.log('Deleting unit account:', id);
            alert('DEMO MODE: Unit account would be deleted.');
            router.push('/finance/unit-accounts');
        }
    };

    if (!existingAccount) {
        return (
            <div className="space-y-6">
                <Card>
                    <CardContent className="py-12 text-center">
                        <p className="text-muted-foreground">Unit account not found.</p>
                        <Link href="/finance/unit-accounts">
                            <Button className="mt-4" variant="outline">
                                Back to Unit Accounts
                            </Button>
                        </Link>
                    </CardContent>
                </Card>
            </div>
        );
    }

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                        <Calculator className="h-8 w-8" />
                        {existingAccount.accountNumber}
                    </h1>
                    <p className="text-muted-foreground">{existingAccount.name}</p>
                    <p className="text-sm text-orange-600 mt-1">
                        ⚠️ DEMO FRONTEND UI
                    </p>
                </div>
                <Button variant="destructive" onClick={handleDelete}>
                    <Trash2 className="mr-2 h-4 w-4" />
                    Delete
                </Button>
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
                        <BreadcrumbLink href="/finance/unit-accounts">Unit Accounts</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>{existingAccount.accountNumber}</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                {/* Edit Form */}
                <div className="lg:col-span-2">
                    <form onSubmit={handleSubmit}>
                        <Card>
                            <CardHeader>
                                <CardTitle>Account Details</CardTitle>
                                <CardDescription>
                                    Update unit account information
                                </CardDescription>
                            </CardHeader>
                            <CardContent className="space-y-6">
                                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                                    {/* Account Number (Read-only) */}
                                    <div className="space-y-2">
                                        <Label>Account Number</Label>
                                        <Input value={existingAccount.accountNumber} disabled className="bg-muted" />
                                    </div>

                                    {/* Unit Type (Read-only) */}
                                    <div className="space-y-2">
                                        <Label>Unit Type</Label>
                                        <Input
                                            value={`${existingAccount.unitType?.code} - ${existingAccount.unitType?.name}`}
                                            disabled
                                            className="bg-muted"
                                        />
                                        <p className="text-xs text-muted-foreground">
                                            Unit type cannot be changed after creation.
                                        </p>
                                    </div>

                                    {/* Name */}
                                    <div className="space-y-2 md:col-span-2">
                                        <Label htmlFor="name">
                                            Name <span className="text-destructive">*</span>
                                        </Label>
                                        <Input
                                            id="name"
                                            value={formData.name}
                                            onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                                            className={errors.name ? 'border-destructive' : ''}
                                        />
                                        {errors.name && (
                                            <p className="text-sm text-destructive">{errors.name}</p>
                                        )}
                                    </div>
                                </div>

                                {/* Description */}
                                <div className="space-y-2">
                                    <Label htmlFor="description">Description</Label>
                                    <Textarea
                                        id="description"
                                        value={formData.description}
                                        onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                                        rows={3}
                                    />
                                </div>

                                {/* Switches */}
                                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                                    <div className="space-y-2">
                                        <Label>Account Type</Label>
                                        <div className="flex items-center gap-3 pt-2">
                                            <Switch
                                                checked={formData.isPostingAccount}
                                                onCheckedChange={(checked) => setFormData({ ...formData, isPostingAccount: checked })}
                                            />
                                            <span>{formData.isPostingAccount ? 'Posting' : 'Summary'}</span>
                                        </div>
                                    </div>
                                    <div className="space-y-2">
                                        <Label>Status</Label>
                                        <div className="flex items-center gap-3 pt-2">
                                            <Switch
                                                checked={formData.isActive}
                                                onCheckedChange={(checked) => setFormData({ ...formData, isActive: checked })}
                                            />
                                            <span className={formData.isActive ? 'text-green-600' : 'text-muted-foreground'}>
                                                {formData.isActive ? 'Active' : 'Inactive'}
                                            </span>
                                        </div>
                                    </div>
                                </div>

                                {/* Actions */}
                                <div className="flex justify-end gap-4 pt-4 border-t">
                                    <Link href="/finance/unit-accounts">
                                        <Button type="button" variant="outline">
                                            <X className="mr-2 h-4 w-4" />
                                            Cancel
                                        </Button>
                                    </Link>
                                    <Button type="submit">
                                        <Save className="mr-2 h-4 w-4" />
                                        Save Changes
                                    </Button>
                                </div>
                            </CardContent>
                        </Card>
                    </form>
                </div>

                {/* Balance Inquiry Sidebar */}
                <div className="space-y-6">
                    {/* Current Balance */}
                    <Card>
                        <CardHeader className="pb-2">
                            <CardTitle className="text-base flex items-center gap-2">
                                <TrendingUp className="h-4 w-4" />
                                Current Balance
                            </CardTitle>
                        </CardHeader>
                        <CardContent>
                            <div className="text-4xl font-bold text-blue-600">
                                {existingAccount.currentBalance?.toLocaleString()}
                            </div>
                            <p className="text-sm text-muted-foreground mt-1">
                                {existingAccount.unitType?.name}
                            </p>
                        </CardContent>
                    </Card>

                    {/* Period Balances */}
                    <Card>
                        <CardHeader className="pb-2">
                            <CardTitle className="text-base flex items-center gap-2">
                                <History className="h-4 w-4" />
                                Balance History
                            </CardTitle>
                        </CardHeader>
                        <CardContent>
                            <div className="space-y-3">
                                {MOCK_BALANCES.map((bal) => (
                                    <div key={bal.id} className="flex justify-between items-center p-2 rounded bg-muted/50">
                                        <div>
                                            <div className="font-medium text-sm">
                                                {PERIOD_NAMES[bal.fiscalPeriodId]}
                                            </div>
                                            <div className="text-xs text-muted-foreground">
                                                Activity: {bal.periodActivity >= 0 ? '+' : ''}{bal.periodActivity}
                                            </div>
                                        </div>
                                        <Badge variant="outline" className="font-mono">
                                            {bal.closingBalance}
                                        </Badge>
                                    </div>
                                ))}
                            </div>
                        </CardContent>
                    </Card>
                </div>
            </div>
        </div>
    );
}
