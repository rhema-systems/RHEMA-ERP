'use client';

import React, { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Calculator, Save, X } from 'lucide-react';
import Link from 'next/link';
import type { UnitType, UnitAccount } from '@/types/unit-accounts';

// MOCK DATA
const MOCK_UNIT_TYPES: UnitType[] = [
    { id: 'ut-1', code: 'EMP', name: 'Employees', decimalPlaces: 0, isActive: true, createdAt: '', createdBy: '' },
    { id: 'ut-2', code: 'SQFT', name: 'Square Footage', decimalPlaces: 2, isActive: true, createdAt: '', createdBy: '' },
    { id: 'ut-3', code: 'HRS', name: 'Hours', decimalPlaces: 2, isActive: true, createdAt: '', createdBy: '' },
    { id: 'ut-4', code: 'UNITS', name: 'Units', decimalPlaces: 0, isActive: true, createdAt: '', createdBy: '' },
];

const MOCK_PARENT_ACCOUNTS: UnitAccount[] = [
    { id: 'ua-1', accountNumber: 'U-1000', name: 'Total Employees', unitTypeId: 'ut-1', accountLevel: 1, isPostingAccount: false, isActive: true, createdAt: '', createdBy: '' },
    { id: 'ua-5', accountNumber: 'U-2000', name: 'Office Space', unitTypeId: 'ut-2', accountLevel: 1, isPostingAccount: false, isActive: true, createdAt: '', createdBy: '' },
];

export default function NewUnitAccountPage() {
    const router = useRouter();
    const [formData, setFormData] = useState({
        accountNumber: '',
        name: '',
        description: '',
        unitTypeId: '',
        parentAccountId: '',
        isPostingAccount: true,
    });
    const [errors, setErrors] = useState<Record<string, string>>({});

    // Filter parent accounts based on selected unit type
    const availableParents = MOCK_PARENT_ACCOUNTS.filter(
        (acc) => !formData.unitTypeId || acc.unitTypeId === formData.unitTypeId
    );

    const validateForm = () => {
        const newErrors: Record<string, string> = {};

        if (!formData.accountNumber.trim()) {
            newErrors.accountNumber = 'Account number is required';
        } else if (formData.accountNumber.length > 20) {
            newErrors.accountNumber = 'Account number must be 20 characters or less';
        }

        if (!formData.name.trim()) {
            newErrors.name = 'Name is required';
        } else if (formData.name.length > 100) {
            newErrors.name = 'Name must be 100 characters or less';
        }

        if (!formData.unitTypeId) {
            newErrors.unitTypeId = 'Unit type is required';
        }

        if (formData.description && formData.description.length > 500) {
            newErrors.description = 'Description must be 500 characters or less';
        }

        setErrors(newErrors);
        return Object.keys(newErrors).length === 0;
    };

    const handleSubmit = (e: React.FormEvent) => {
        e.preventDefault();

        if (!validateForm()) {
            return;
        }

        // TODO: Replace with API call
        console.log('Creating unit account:', formData);
        alert('DEMO MODE: Unit account would be created. Check console for data.');
        router.push('/finance/unit-accounts');
    };

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div>
                <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                    <Calculator className="h-8 w-8" />
                    New Unit Account
                </h1>
                <p className="text-muted-foreground">
                    Create a new unit account for tracking quantities
                </p>
                <p className="text-sm text-orange-600 mt-1">
                    ⚠️ DEMO FRONTEND UI
                </p>
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
                        <BreadcrumbPage>New</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Form */}
            <form onSubmit={handleSubmit}>
                <Card>
                    <CardHeader>
                        <CardTitle>Unit Account Details</CardTitle>
                        <CardDescription>
                            Create a new unit account. Child accounts inherit the unit type from parent.
                        </CardDescription>
                    </CardHeader>
                    <CardContent className="space-y-6">
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                            {/* Account Number */}
                            <div className="space-y-2">
                                <Label htmlFor="accountNumber">
                                    Account Number <span className="text-destructive">*</span>
                                </Label>
                                <Input
                                    id="accountNumber"
                                    placeholder="e.g., U-1000"
                                    value={formData.accountNumber}
                                    onChange={(e) => setFormData({ ...formData, accountNumber: e.target.value })}
                                    maxLength={20}
                                    className={errors.accountNumber ? 'border-destructive' : ''}
                                />
                                {errors.accountNumber && (
                                    <p className="text-sm text-destructive">{errors.accountNumber}</p>
                                )}
                            </div>

                            {/* Name */}
                            <div className="space-y-2">
                                <Label htmlFor="name">
                                    Name <span className="text-destructive">*</span>
                                </Label>
                                <Input
                                    id="name"
                                    placeholder="e.g., Total Employees"
                                    value={formData.name}
                                    onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                                    maxLength={100}
                                    className={errors.name ? 'border-destructive' : ''}
                                />
                                {errors.name && (
                                    <p className="text-sm text-destructive">{errors.name}</p>
                                )}
                            </div>

                            {/* Unit Type */}
                            <div className="space-y-2">
                                <Label htmlFor="unitType">
                                    Unit Type <span className="text-destructive">*</span>
                                </Label>
                                <Select
                                    value={formData.unitTypeId}
                                    onValueChange={(value) => setFormData({
                                        ...formData,
                                        unitTypeId: value,
                                        parentAccountId: '' // Reset parent when type changes
                                    })}
                                >
                                    <SelectTrigger className={errors.unitTypeId ? 'border-destructive' : ''}>
                                        <SelectValue placeholder="Select a unit type..." />
                                    </SelectTrigger>
                                    <SelectContent>
                                        {MOCK_UNIT_TYPES.map((type) => (
                                            <SelectItem key={type.id} value={type.id}>
                                                {type.code} - {type.name}
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                {errors.unitTypeId && (
                                    <p className="text-sm text-destructive">{errors.unitTypeId}</p>
                                )}
                                <p className="text-xs text-muted-foreground">
                                    Defines what quantity this account tracks.
                                </p>
                            </div>

                            {/* Parent Account */}
                            <div className="space-y-2">
                                <Label htmlFor="parentAccount">Parent Account</Label>
                                <Select
                                    value={formData.parentAccountId}
                                    onValueChange={(value) => setFormData({ ...formData, parentAccountId: value })}
                                    disabled={!formData.unitTypeId}
                                >
                                    <SelectTrigger>
                                        <SelectValue placeholder="None (top-level account)" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="">None (top-level account)</SelectItem>
                                        {availableParents.map((acc) => (
                                            <SelectItem key={acc.id} value={acc.id}>
                                                {acc.accountNumber} - {acc.name}
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                <p className="text-xs text-muted-foreground">
                                    Optional. Only summary accounts of the same unit type shown.
                                </p>
                            </div>
                        </div>

                        {/* Description */}
                        <div className="space-y-2">
                            <Label htmlFor="description">Description</Label>
                            <Textarea
                                id="description"
                                placeholder="Optional description..."
                                value={formData.description}
                                onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                                rows={3}
                                maxLength={500}
                            />
                            <p className="text-xs text-muted-foreground">
                                {formData.description.length}/500 characters
                            </p>
                        </div>

                        {/* Is Posting Account */}
                        <div className="space-y-2">
                            <Label>Account Type</Label>
                            <div className="flex items-center gap-3 pt-2">
                                <Switch
                                    checked={formData.isPostingAccount}
                                    onCheckedChange={(checked) => setFormData({ ...formData, isPostingAccount: checked })}
                                />
                                <span className={formData.isPostingAccount ? 'text-green-600' : 'text-muted-foreground'}>
                                    {formData.isPostingAccount ? 'Posting Account' : 'Summary Account'}
                                </span>
                            </div>
                            <p className="text-xs text-muted-foreground">
                                {formData.isPostingAccount
                                    ? 'Posting accounts can receive journal entry postings.'
                                    : 'Summary accounts aggregate child account balances and cannot receive direct postings.'}
                            </p>
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
                                Create Unit Account
                            </Button>
                        </div>
                    </CardContent>
                </Card>
            </form>
        </div>
    );
}
