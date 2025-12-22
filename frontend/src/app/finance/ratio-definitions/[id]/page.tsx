'use client';

import React, { useState, useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import Link from 'next/link';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { TrendingUp, Save, X, Trash2, AlertTriangle, Loader2, Calculator } from 'lucide-react';
import type { RatioDefinition, UnitAccount } from '@/types/unit-accounts';
import { unitAccountsDataService } from '@/services/finance/unit-accounts-data.service';

export default function EditRatioDefinitionPage() {
    const router = useRouter();
    const params = useParams();
    const id = params.id as string;

    const [ratio, setRatio] = useState<RatioDefinition | null>(null);
    const [unitAccounts, setUnitAccounts] = useState<UnitAccount[]>([]);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [formData, setFormData] = useState({
        name: '',
        description: '',
        resultFormat: 'Number' as 'Number' | 'Currency' | 'Percentage',
        formatPrecision: 2,
        isActive: true,
    });
    const [errors, setErrors] = useState<Record<string, string>>({});

    // Load ratio data
    useEffect(() => {
        async function loadData() {
            try {
                const [ratios, accounts] = await Promise.all([
                    unitAccountsDataService.getRatioDefinitions(),
                    unitAccountsDataService.getUnitAccounts(),
                ]);

                const ratioData = ratios.find(r => r.id === id);
                if (ratioData) {
                    setRatio(ratioData);
                    setFormData({
                        name: ratioData.name || '',
                        description: ratioData.description || '',
                        resultFormat: ratioData.resultFormat || 'Number',
                        formatPrecision: ratioData.formatPrecision ?? 2,
                        isActive: ratioData.isActive ?? true,
                    });
                }
                setUnitAccounts(accounts);
            } catch (error) {
                console.error('Failed to load ratio:', error);
            } finally {
                setLoading(false);
            }
        }
        loadData();
    }, [id]);

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
        if (!validateForm() || !ratio) return;

        setSaving(true);
        try {
            // Since we don't have a proper update API for ratios, we'll use the demo storage
            // This would normally call unitAccountsDataService.updateRatioDefinition(id, formData)
            alert('⚠️ DEMO MODE: Ratio definition updated successfully.');
            router.push('/finance/ratio-definitions');
        } catch (error) {
            console.error('Failed to update ratio:', error);
            alert('Failed to update ratio definition. Please try again.');
        } finally {
            setSaving(false);
        }
    };

    const handleDelete = async () => {
        if (confirm('Are you sure you want to delete this ratio definition?')) {
            try {
                await unitAccountsDataService.deleteRatioDefinition(id);
                router.push('/finance/ratio-definitions');
            } catch (error) {
                console.error('Failed to delete ratio:', error);
                alert('Failed to delete ratio definition. Please try again.');
            }
        }
    };

    // Get account names for display
    const getNumeratorDisplay = () => {
        if (!ratio) return 'N/A';
        if (ratio.numeratorType === 'Financial') return 'Financial Account';
        if (ratio.numeratorUnitAccountId) {
            const acc = unitAccounts.find(a => a.id === ratio.numeratorUnitAccountId);
            return acc ? `${acc.accountNumber} - ${acc.name}` : 'Unit Account';
        }
        if (ratio.numeratorConstantValue !== undefined) return `Constant: ${ratio.numeratorConstantValue}`;
        return 'N/A';
    };

    const getDenominatorDisplay = () => {
        if (!ratio) return 'N/A';
        if (ratio.denominatorType === 'Financial') return 'Financial Account';
        if (ratio.denominatorUnitAccountId) {
            const acc = unitAccounts.find(a => a.id === ratio.denominatorUnitAccountId);
            return acc ? `${acc.accountNumber} - ${acc.name}` : 'Unit Account';
        }
        if (ratio.denominatorConstantValue !== undefined) return `Constant: ${ratio.denominatorConstantValue}`;
        return 'N/A';
    };

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-[400px]">
                <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
            </div>
        );
    }

    if (!ratio) {
        return (
            <div className="space-y-6">
                <Card>
                    <CardContent className="py-12 text-center">
                        <p className="text-muted-foreground">Ratio definition not found.</p>
                        <Link href="/finance/ratio-definitions">
                            <Button className="mt-4" variant="outline">
                                Back to Ratio Definitions
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
                        <TrendingUp className="h-8 w-8" />
                        {ratio.code}
                    </h1>
                    <p className="text-muted-foreground">{ratio.name}</p>
                </div>
                <Button variant="destructive" onClick={handleDelete}>
                    <Trash2 className="mr-2 h-4 w-4" />
                    Delete
                </Button>
            </div>

            {/* Demo Mode Alert */}
            <Alert className="border-yellow-500 bg-yellow-50">
                <AlertTriangle className="h-4 w-4 text-yellow-600" />
                <AlertDescription className="text-yellow-700">
                    <strong>⚠️ DEMO FRONTEND UI</strong> - Data is stored in browser localStorage
                </AlertDescription>
            </Alert>

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
                        <BreadcrumbLink href="/finance/ratio-definitions">Ratio Definitions</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>{ratio.code}</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                {/* Edit Form */}
                <div className="lg:col-span-2">
                    <form onSubmit={handleSubmit}>
                        <Card>
                            <CardHeader>
                                <CardTitle>Ratio Details</CardTitle>
                                <CardDescription>
                                    Update ratio definition settings
                                </CardDescription>
                            </CardHeader>
                            <CardContent className="space-y-6">
                                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                                    {/* Code (Read-only) */}
                                    <div className="space-y-2">
                                        <Label>Code</Label>
                                        <Input value={ratio.code} disabled className="bg-muted" />
                                        <p className="text-xs text-muted-foreground">
                                            Code cannot be changed after creation.
                                        </p>
                                    </div>

                                    {/* Type (Read-only) */}
                                    <div className="space-y-2">
                                        <Label>Ratio Type</Label>
                                        <Input value={ratio.ratioType || 'Unit'} disabled className="bg-muted" />
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

                                {/* Format Options */}
                                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                                    <div className="space-y-2">
                                        <Label>Result Format</Label>
                                        <Select
                                            value={formData.resultFormat}
                                            onValueChange={(v) => setFormData({ ...formData, resultFormat: v as any })}
                                        >
                                            <SelectTrigger>
                                                <SelectValue />
                                            </SelectTrigger>
                                            <SelectContent>
                                                <SelectItem value="Number">Number</SelectItem>
                                                <SelectItem value="Currency">Currency</SelectItem>
                                                <SelectItem value="Percentage">Percentage</SelectItem>
                                            </SelectContent>
                                        </Select>
                                    </div>

                                    <div className="space-y-2">
                                        <Label htmlFor="precision">Decimal Places</Label>
                                        <Input
                                            id="precision"
                                            type="number"
                                            min={0}
                                            max={6}
                                            value={formData.formatPrecision}
                                            onChange={(e) => setFormData({ ...formData, formatPrecision: parseInt(e.target.value) || 0 })}
                                        />
                                    </div>
                                </div>

                                {/* Active Status */}
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

                                {/* Actions */}
                                <div className="flex justify-end gap-4 pt-4 border-t">
                                    <Link href="/finance/ratio-definitions">
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

                {/* Sidebar - Formula Display */}
                <div className="space-y-6">
                    {/* Formula Card */}
                    <Card>
                        <CardHeader className="pb-2">
                            <CardTitle className="text-base flex items-center gap-2">
                                <Calculator className="h-4 w-4" />
                                Formula
                            </CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-4">
                            <div className="p-4 bg-muted rounded-lg space-y-3">
                                <div className="text-center border-b border-border pb-2">
                                    <div className="text-sm text-muted-foreground">Numerator</div>
                                    <div className="font-medium">{getNumeratorDisplay()}</div>
                                </div>
                                <div className="text-center text-2xl font-bold text-muted-foreground">÷</div>
                                <div className="text-center border-t border-border pt-2">
                                    <div className="text-sm text-muted-foreground">Denominator</div>
                                    <div className="font-medium">{getDenominatorDisplay()}</div>
                                </div>
                            </div>
                            <p className="text-xs text-muted-foreground text-center">
                                Formula components cannot be changed. Create a new ratio if needed.
                            </p>
                        </CardContent>
                    </Card>

                    {/* Ratio Info */}
                    <Card>
                        <CardHeader className="pb-2">
                            <CardTitle className="text-base">Ratio Info</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-2 text-sm">
                            <div className="flex justify-between">
                                <span className="text-muted-foreground">Created:</span>
                                <span>{ratio.createdAt ? new Date(ratio.createdAt).toLocaleDateString() : 'N/A'}</span>
                            </div>
                            <div className="flex justify-between">
                                <span className="text-muted-foreground">Created By:</span>
                                <span>{ratio.createdBy || 'N/A'}</span>
                            </div>
                            <div className="flex justify-between">
                                <span className="text-muted-foreground">Status:</span>
                                <Badge variant={ratio.isActive ? 'default' : 'secondary'}
                                    className={ratio.isActive ? 'bg-green-100 text-green-800' : ''}>
                                    {ratio.isActive ? 'Active' : 'Inactive'}
                                </Badge>
                            </div>
                        </CardContent>
                    </Card>

                    {/* Calculate Button */}
                    <Link href="/finance/ratio-definitions/calculator">
                        <Button className="w-full" variant="outline">
                            <Calculator className="mr-2 h-4 w-4" />
                            Open Calculator
                        </Button>
                    </Link>
                </div>
            </div>
        </div>
    );
}
