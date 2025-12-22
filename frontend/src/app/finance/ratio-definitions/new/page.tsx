'use client';

import React, { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Divide, Save, X, AlertTriangle, Loader2 } from 'lucide-react';
import type { UnitAccount } from '@/types/unit-accounts';
import { unitAccountsDataService } from '@/services/finance/unit-accounts-data.service';

type ComponentType = 'FinancialAccount' | 'UnitAccount' | 'Constant';
type ResultFormat = 'Currency' | 'Percentage' | 'Number';

export default function NewRatioDefinitionPage() {
    const router = useRouter();
    const [unitAccounts, setUnitAccounts] = useState<UnitAccount[]>([]);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [formData, setFormData] = useState({
        code: '',
        name: '',
        description: '',
        ratioType: 'Unit' as 'Unit' | 'Financial',
        numeratorType: 'FinancialAccount' as ComponentType,
        numeratorUnitAccountId: '',
        numeratorConstantValue: '',
        denominatorType: 'UnitAccount' as ComponentType,
        denominatorUnitAccountId: '',
        denominatorConstantValue: '',
        resultFormat: 'Currency' as ResultFormat,
        formatPrecision: 2,
    });
    const [errors, setErrors] = useState<Record<string, string>>({});

    // Load unit accounts
    useEffect(() => {
        async function loadData() {
            try {
                const accounts = await unitAccountsDataService.getUnitAccounts();
                setUnitAccounts(accounts);
            } catch (error) {
                console.error('Failed to load unit accounts:', error);
            } finally {
                setLoading(false);
            }
        }
        loadData();
    }, []);

    const validateForm = () => {
        const newErrors: Record<string, string> = {};

        if (!formData.code.trim()) {
            newErrors.code = 'Code is required';
        } else if (formData.code.length > 20) {
            newErrors.code = 'Code must be 20 characters or less';
        }

        if (!formData.name.trim()) {
            newErrors.name = 'Name is required';
        } else if (formData.name.length > 100) {
            newErrors.name = 'Name must be 100 characters or less';
        }

        if (formData.numeratorType === 'UnitAccount' && !formData.numeratorUnitAccountId) {
            newErrors.numeratorUnitAccountId = 'Select a unit account for numerator';
        }

        if (formData.numeratorType === 'Constant' && !formData.numeratorConstantValue) {
            newErrors.numeratorConstantValue = 'Enter a constant value for numerator';
        }

        if (formData.denominatorType === 'UnitAccount' && !formData.denominatorUnitAccountId) {
            newErrors.denominatorUnitAccountId = 'Select a unit account for denominator';
        }

        if (formData.denominatorType === 'Constant' && !formData.denominatorConstantValue) {
            newErrors.denominatorConstantValue = 'Enter a constant value for denominator';
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
            await unitAccountsDataService.createRatioDefinition({
                code: formData.code,
                name: formData.name,
                description: formData.description || undefined,
                ratioType: formData.ratioType,
                numeratorType: formData.numeratorType,
                numeratorUnitAccountId: formData.numeratorType === 'UnitAccount' ? formData.numeratorUnitAccountId : undefined,
                numeratorConstantValue: formData.numeratorType === 'Constant' ? parseFloat(formData.numeratorConstantValue) : undefined,
                denominatorType: formData.denominatorType,
                denominatorUnitAccountId: formData.denominatorType === 'UnitAccount' ? formData.denominatorUnitAccountId : undefined,
                denominatorConstantValue: formData.denominatorType === 'Constant' ? parseFloat(formData.denominatorConstantValue) : undefined,
                resultFormat: formData.resultFormat,
                formatPrecision: formData.formatPrecision,
            });
            router.push('/finance/ratio-definitions');
        } catch (error) {
            console.error('Failed to create ratio definition:', error);
            alert('Failed to create ratio definition. Please try again.');
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
                    <Divide className="h-8 w-8" />
                    New Ratio Definition
                </h1>
                <p className="text-muted-foreground">
                    Create a new KPI ratio combining financial and unit data
                </p>
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
                        <BreadcrumbPage>New</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Form */}
            <form onSubmit={handleSubmit}>
                <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                    {/* Basic Info */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Basic Information</CardTitle>
                            <CardDescription>
                                Define the ratio's code, name, and output format
                            </CardDescription>
                        </CardHeader>
                        <CardContent className="space-y-4">
                            <div className="grid grid-cols-2 gap-4">
                                <div className="space-y-2">
                                    <Label htmlFor="code">
                                        Code <span className="text-destructive">*</span>
                                    </Label>
                                    <Input
                                        id="code"
                                        placeholder="e.g., REV-EMP"
                                        value={formData.code}
                                        onChange={(e) => setFormData({ ...formData, code: e.target.value.toUpperCase() })}
                                        maxLength={20}
                                        className={errors.code ? 'border-destructive' : ''}
                                    />
                                    {errors.code && (
                                        <p className="text-sm text-destructive">{errors.code}</p>
                                    )}
                                </div>

                                <div className="space-y-2">
                                    <Label>Ratio Type</Label>
                                    <Select
                                        value={formData.ratioType}
                                        onValueChange={(v) => setFormData({ ...formData, ratioType: v as any })}
                                    >
                                        <SelectTrigger>
                                            <SelectValue />
                                        </SelectTrigger>
                                        <SelectContent>
                                            <SelectItem value="Unit">Unit Ratio</SelectItem>
                                            <SelectItem value="Financial">Financial Ratio</SelectItem>
                                        </SelectContent>
                                    </Select>
                                </div>
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="name">
                                    Name <span className="text-destructive">*</span>
                                </Label>
                                <Input
                                    id="name"
                                    placeholder="e.g., Revenue Per Employee"
                                    value={formData.name}
                                    onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                                    maxLength={100}
                                    className={errors.name ? 'border-destructive' : ''}
                                />
                                {errors.name && (
                                    <p className="text-sm text-destructive">{errors.name}</p>
                                )}
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="description">Description</Label>
                                <Textarea
                                    id="description"
                                    placeholder="Describe what this ratio measures..."
                                    value={formData.description}
                                    onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                                    rows={3}
                                />
                            </div>

                            <div className="grid grid-cols-2 gap-4">
                                <div className="space-y-2">
                                    <Label>Result Format</Label>
                                    <Select
                                        value={formData.resultFormat}
                                        onValueChange={(v) => setFormData({ ...formData, resultFormat: v as ResultFormat })}
                                    >
                                        <SelectTrigger>
                                            <SelectValue />
                                        </SelectTrigger>
                                        <SelectContent>
                                            <SelectItem value="Currency">Currency ($)</SelectItem>
                                            <SelectItem value="Percentage">Percentage (%)</SelectItem>
                                            <SelectItem value="Number">Number</SelectItem>
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
                        </CardContent>
                    </Card>

                    {/* Formula */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Formula Components</CardTitle>
                            <CardDescription>
                                Define the numerator and denominator for this ratio
                            </CardDescription>
                        </CardHeader>
                        <CardContent className="space-y-6">
                            {/* Numerator */}
                            <div className="p-4 border rounded-lg space-y-4">
                                <Label className="text-base font-semibold">Numerator (Top)</Label>

                                <div className="space-y-2">
                                    <Label>Type</Label>
                                    <Select
                                        value={formData.numeratorType}
                                        onValueChange={(v) => setFormData({
                                            ...formData,
                                            numeratorType: v as ComponentType,
                                            numeratorUnitAccountId: '',
                                            numeratorConstantValue: ''
                                        })}
                                    >
                                        <SelectTrigger>
                                            <SelectValue />
                                        </SelectTrigger>
                                        <SelectContent>
                                            <SelectItem value="FinancialAccount">Financial Account</SelectItem>
                                            <SelectItem value="UnitAccount">Unit Account</SelectItem>
                                            <SelectItem value="Constant">Constant Value</SelectItem>
                                        </SelectContent>
                                    </Select>
                                </div>

                                {formData.numeratorType === 'UnitAccount' && (
                                    <div className="space-y-2">
                                        <Label>Unit Account</Label>
                                        <Select
                                            value={formData.numeratorUnitAccountId || "select"}
                                            onValueChange={(v) => setFormData({
                                                ...formData,
                                                numeratorUnitAccountId: v === "select" ? '' : v
                                            })}
                                        >
                                            <SelectTrigger className={errors.numeratorUnitAccountId ? 'border-destructive' : ''}>
                                                <SelectValue placeholder="Select unit account..." />
                                            </SelectTrigger>
                                            <SelectContent>
                                                <SelectItem value="select">Select unit account...</SelectItem>
                                                {unitAccounts.map((acc) => (
                                                    <SelectItem key={acc.id} value={acc.id}>
                                                        {acc.accountNumber} - {acc.name}
                                                    </SelectItem>
                                                ))}
                                            </SelectContent>
                                        </Select>
                                        {errors.numeratorUnitAccountId && (
                                            <p className="text-sm text-destructive">{errors.numeratorUnitAccountId}</p>
                                        )}
                                    </div>
                                )}

                                {formData.numeratorType === 'Constant' && (
                                    <div className="space-y-2">
                                        <Label>Constant Value</Label>
                                        <Input
                                            type="number"
                                            placeholder="e.g., 100"
                                            value={formData.numeratorConstantValue}
                                            onChange={(e) => setFormData({ ...formData, numeratorConstantValue: e.target.value })}
                                            className={errors.numeratorConstantValue ? 'border-destructive' : ''}
                                        />
                                        {errors.numeratorConstantValue && (
                                            <p className="text-sm text-destructive">{errors.numeratorConstantValue}</p>
                                        )}
                                    </div>
                                )}

                                {formData.numeratorType === 'FinancialAccount' && (
                                    <p className="text-sm text-muted-foreground">
                                        Financial account selection will be available when connected to API.
                                    </p>
                                )}
                            </div>

                            {/* Division Symbol */}
                            <div className="flex justify-center">
                                <div className="w-12 h-12 rounded-full bg-muted flex items-center justify-center text-2xl font-bold text-muted-foreground">
                                    ÷
                                </div>
                            </div>

                            {/* Denominator */}
                            <div className="p-4 border rounded-lg space-y-4">
                                <Label className="text-base font-semibold">Denominator (Bottom)</Label>

                                <div className="space-y-2">
                                    <Label>Type</Label>
                                    <Select
                                        value={formData.denominatorType}
                                        onValueChange={(v) => setFormData({
                                            ...formData,
                                            denominatorType: v as ComponentType,
                                            denominatorUnitAccountId: '',
                                            denominatorConstantValue: ''
                                        })}
                                    >
                                        <SelectTrigger>
                                            <SelectValue />
                                        </SelectTrigger>
                                        <SelectContent>
                                            <SelectItem value="FinancialAccount">Financial Account</SelectItem>
                                            <SelectItem value="UnitAccount">Unit Account</SelectItem>
                                            <SelectItem value="Constant">Constant Value</SelectItem>
                                        </SelectContent>
                                    </Select>
                                </div>

                                {formData.denominatorType === 'UnitAccount' && (
                                    <div className="space-y-2">
                                        <Label>Unit Account</Label>
                                        <Select
                                            value={formData.denominatorUnitAccountId || "select"}
                                            onValueChange={(v) => setFormData({
                                                ...formData,
                                                denominatorUnitAccountId: v === "select" ? '' : v
                                            })}
                                        >
                                            <SelectTrigger className={errors.denominatorUnitAccountId ? 'border-destructive' : ''}>
                                                <SelectValue placeholder="Select unit account..." />
                                            </SelectTrigger>
                                            <SelectContent>
                                                <SelectItem value="select">Select unit account...</SelectItem>
                                                {unitAccounts.map((acc) => (
                                                    <SelectItem key={acc.id} value={acc.id}>
                                                        {acc.accountNumber} - {acc.name}
                                                    </SelectItem>
                                                ))}
                                            </SelectContent>
                                        </Select>
                                        {errors.denominatorUnitAccountId && (
                                            <p className="text-sm text-destructive">{errors.denominatorUnitAccountId}</p>
                                        )}
                                    </div>
                                )}

                                {formData.denominatorType === 'Constant' && (
                                    <div className="space-y-2">
                                        <Label>Constant Value</Label>
                                        <Input
                                            type="number"
                                            placeholder="e.g., 100"
                                            value={formData.denominatorConstantValue}
                                            onChange={(e) => setFormData({ ...formData, denominatorConstantValue: e.target.value })}
                                            className={errors.denominatorConstantValue ? 'border-destructive' : ''}
                                        />
                                        {errors.denominatorConstantValue && (
                                            <p className="text-sm text-destructive">{errors.denominatorConstantValue}</p>
                                        )}
                                    </div>
                                )}

                                {formData.denominatorType === 'FinancialAccount' && (
                                    <p className="text-sm text-muted-foreground">
                                        Financial account selection will be available when connected to API.
                                    </p>
                                )}
                            </div>
                        </CardContent>
                    </Card>
                </div>

                {/* Actions */}
                <div className="flex justify-end gap-4 mt-6">
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
                        Create Ratio Definition
                    </Button>
                </div>
            </form>
        </div>
    );
}
