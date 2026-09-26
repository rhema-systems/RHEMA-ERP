'use client';

import React, { useState, useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Calculator, Save, X, Trash2, TrendingUp, History, AlertTriangle, Loader2 } from 'lucide-react';
import Link from 'next/link';
import type { UnitType, UnitAccount } from '@/types/unit-accounts';
import { unitAccountsDataService } from '@/services/finance/unit-accounts-data.service';
import { toast } from 'sonner';

export default function EditUnitAccountPage() {
    const router = useRouter();
    const params = useParams();
    const id = params.id as string;

    const [account, setAccount] = useState<UnitAccount | null>(null);
    const [unitTypes, setUnitTypes] = useState<UnitType[]>([]);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [formData, setFormData] = useState({
        name: '',
        description: '',
        isPostingAccount: true,
        isActive: true,
    });
    const [errors, setErrors] = useState<Record<string, string>>({});

    // Load account data
    useEffect(() => {
        async function loadData() {
            try {
                const [accountData, types] = await Promise.all([
                    unitAccountsDataService.getUnitAccountById(id),
                    unitAccountsDataService.getUnitTypes(),
                ]);

                if (accountData) {
                    setAccount(accountData);
                    setFormData({
                        name: accountData.name || '',
                        description: accountData.description || '',
                        isPostingAccount: accountData.isPostingAccount ?? true,
                        isActive: accountData.isActive ?? true,
                    });
                }
                setUnitTypes(types);
            } catch (error) {
                console.error('Failed to load account:', error);
            } finally {
                setLoading(false);
            }
        }
        loadData();
    }, [id]);

    // Get unit type info
    const unitType = unitTypes.find(t => t.id === account?.unitTypeId);

    const validateForm = () => {
        const newErrors: Record<string, string> = {};

        if (!formData.name.trim()) {
            newErrors.name = 'Name is required';
        }

        setErrors(newErrors);
        return Object.keys(newErrors).length === 0;
    };

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!validateForm()) return;

        setSaving(true);
        try {
            await unitAccountsDataService.updateUnitAccount(id, {
                name: formData.name,
                description: formData.description || undefined,
                isPostingAccount: formData.isPostingAccount,
                // isActive: formData.isActive, // Not supported in DTO yet
            });
            router.push('/finance/unit-accounts');
        } catch (error) {
            console.error('Failed to update account:', error);
            toast.error('The unit account was not updated. Review the account details, refresh its current state, and retry.');
        } finally {
            setSaving(false);
        }
    };

    const handleDelete = async () => {
        if (confirm('Are you sure you want to delete this unit account?')) {
            try {
                await unitAccountsDataService.deleteUnitAccount(id);
                router.push('/finance/unit-accounts');
            } catch (error) {
                console.error('Failed to delete account:', error);
                toast.error('The unit account was not deleted. It may have dependent activity; review its usage and retry.');
            }
        }
    };

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-[400px]">
                <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
            </div>
        );
    }

    if (!account) {
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
                        {account.accountNumber}
                    </h1>
                    <p className="text-muted-foreground">{account.name}</p>
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
                        <BreadcrumbPage>{account.accountNumber}</BreadcrumbPage>
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
                                        <Input value={account.accountNumber} disabled className="bg-muted" />
                                    </div>

                                    {/* Unit Type (Read-only) */}
                                    <div className="space-y-2">
                                        <Label>Unit Type</Label>
                                        <Input
                                            value={unitType ? `${unitType.code} - ${unitType.name}` : account.unitType?.code || 'Unknown'}
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
                                        <Button type="button" variant="outline" disabled={saving}>
                                            <X className="mr-2 h-4 w-4" />
                                            Cancel
                                        </Button>
                                    </Link>
                                    <Button type="submit" disabled={saving}>
                                        {saving ? (
                                            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                        ) : (
                                            <Save className="mr-2 h-4 w-4" />
                                        )}
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
                                {(account.currentBalance || 0).toLocaleString()}
                            </div>
                            <p className="text-sm text-muted-foreground mt-1">
                                {unitType?.name || account.unitType?.code || 'Units'}
                            </p>
                        </CardContent>
                    </Card>

                    {/* Balance History Placeholder */}
                    <Card>
                        <CardHeader className="pb-2">
                            <CardTitle className="text-base flex items-center gap-2">
                                <History className="h-4 w-4" />
                                Balance History
                            </CardTitle>
                        </CardHeader>
                        <CardContent>
                            <p className="text-sm text-muted-foreground">
                                Balance history will be available when connected to the API.
                            </p>
                        </CardContent>
                    </Card>

                    {/* Account Info */}
                    <Card>
                        <CardHeader className="pb-2">
                            <CardTitle className="text-base">Account Info</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-2 text-sm">
                            <div className="flex justify-between">
                                <span className="text-muted-foreground">Created:</span>
                                <span>{account.createdAt ? new Date(account.createdAt).toLocaleDateString() : 'N/A'}</span>
                            </div>
                            <div className="flex justify-between">
                                <span className="text-muted-foreground">Created By:</span>
                                <span>{account.createdBy || 'N/A'}</span>
                            </div>
                            <div className="flex justify-between">
                                <span className="text-muted-foreground">Type:</span>
                                <Badge variant={account.isPostingAccount ? 'default' : 'secondary'}>
                                    {account.isPostingAccount ? 'Posting' : 'Summary'}
                                </Badge>
                            </div>
                            <div className="flex justify-between">
                                <span className="text-muted-foreground">Status:</span>
                                <Badge variant={account.isActive ? 'default' : 'secondary'}
                                    className={account.isActive ? 'bg-green-100 text-green-800' : ''}>
                                    {account.isActive ? 'Active' : 'Inactive'}
                                </Badge>
                            </div>
                        </CardContent>
                    </Card>
                </div>
            </div>
        </div>
    );
}
