'use client';

import React, { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Calculator, Save, X, AlertTriangle, Loader2 } from 'lucide-react';
import Link from 'next/link';
import type { UnitType, UnitAccount } from '@/types/unit-accounts';
import { unitAccountsDataService } from '@/services/finance/unit-accounts-data.service';

export default function NewUnitAccountPage() {
    const router = useRouter();
    const [unitTypes, setUnitTypes] = useState<UnitType[]>([]);
    const [parentAccounts, setParentAccounts] = useState<UnitAccount[]>([]);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [formData, setFormData] = useState({
        accountNumber: '',
        name: '',
        description: '',
        unitTypeId: '',
        parentAccountId: '',
        isPostingAccount: true,
    });
    const [errors, setErrors] = useState<Record<string, string>>({});

    // Load unit types and accounts from data service
    useEffect(() => {
        async function loadData() {
            try {
                const [types, accounts] = await Promise.all([
                    unitAccountsDataService.getUnitTypes(),
                    unitAccountsDataService.getUnitAccounts(),
                ]);
                setUnitTypes(types);
                // Only show summary accounts as potential parents
                setParentAccounts(accounts.filter(a => !a.isPostingAccount));
            } catch (error) {
                console.error('Failed to load data:', error);
            } finally {
                setLoading(false);
            }
        }
        loadData();
    }, []);

    // Filter parent accounts based on selected unit type
    const availableParents = parentAccounts.filter(
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

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();

        if (!validateForm()) {
            return;
        }

        setSaving(true);
        try {
            await unitAccountsDataService.createUnitAccount({
                accountNumber: formData.accountNumber,
                name: formData.name,
                description: formData.description || undefined,
                unitTypeId: formData.unitTypeId,
                parentAccountId: formData.parentAccountId || undefined,
                isPostingAccount: formData.isPostingAccount,
            });
            router.push('/finance/unit-accounts');
        } catch (error) {
            console.error('Failed to create unit account:', error);
            alert('Failed to create unit account. Please try again.');
        } finally {
            setSaving(false);
        }
    };

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-[400px]">
                <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
            </div>
        );
    }

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
                                {unitTypes.length === 0 ? (
                                    <div className="p-3 bg-muted rounded-md text-sm text-muted-foreground">
                                        No unit types available. <Link href="/finance/unit-types" className="text-primary underline">Create one first</Link>.
                                    </div>
                                ) : (
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
                                            {unitTypes.map((type) => (
                                                <SelectItem key={type.id} value={type.id}>
                                                    {type.code} - {type.name}
                                                </SelectItem>
                                            ))}
                                        </SelectContent>
                                    </Select>
                                )}
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
                                    value={formData.parentAccountId || "none"}
                                    onValueChange={(value) => setFormData({
                                        ...formData,
                                        parentAccountId: value === "none" ? '' : value
                                    })}
                                    disabled={!formData.unitTypeId}
                                >
                                    <SelectTrigger>
                                        <SelectValue placeholder="None (top-level account)" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="none">None (top-level account)</SelectItem>
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
                                <Button type="button" variant="outline" disabled={saving}>
                                    <X className="mr-2 h-4 w-4" />
                                    Cancel
                                </Button>
                            </Link>
                            <Button type="submit" disabled={saving || unitTypes.length === 0}>
                                {saving ? (
                                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                ) : (
                                    <Save className="mr-2 h-4 w-4" />
                                )}
                                Create Unit Account
                            </Button>
                        </div>
                    </CardContent>
                </Card>
            </form>
        </div>
    );
}
