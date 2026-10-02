'use client';

import React, { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Ruler, Save, X } from 'lucide-react';
import Link from 'next/link';
import { unitAccountsDataService } from '@/services/finance/unit-accounts-data.service';

export default function NewUnitTypePage() {
    const router = useRouter();
    const [formData, setFormData] = useState({
        code: '',
        name: '',
        description: '',
        decimalPlaces: '2',
        roundingIncrement: '',
    });
    const [errors, setErrors] = useState<Record<string, string>>({});
    const [busy, setBusy] = useState(false);

    const validateForm = () => {
        const newErrors: Record<string, string> = {};

        if (!formData.code.trim()) {
            newErrors.code = 'Code is required';
        } else if (!/^[A-Z0-9]+$/.test(formData.code)) {
            newErrors.code = 'Code must be uppercase alphanumeric only';
        } else if (formData.code.length > 20) {
            newErrors.code = 'Code must be 20 characters or less';
        }

        if (!formData.name.trim()) {
            newErrors.name = 'Name is required';
        } else if (formData.name.length > 100) {
            newErrors.name = 'Name must be 100 characters or less';
        }

        if (formData.description && formData.description.length > 500) {
            newErrors.description = 'Description must be 500 characters or less';
        }

        if (formData.roundingIncrement) {
            const increment = Number(formData.roundingIncrement);
            const minimumIncrement = 10 ** -Number(formData.decimalPlaces);
            if (!Number.isFinite(increment) || increment <= 0 || increment < minimumIncrement) {
                newErrors.roundingIncrement = `Increment must be at least ${minimumIncrement}.`;
            }
        }

        setErrors(newErrors);
        return Object.keys(newErrors).length === 0;
    };

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();

        if (!validateForm()) {
            return;
        }

        setBusy(true);
        try {
            await unitAccountsDataService.createUnitType({
                code: formData.code,
                name: formData.name.trim(),
                description: formData.description.trim() || undefined,
                decimalPlaces: Number(formData.decimalPlaces),
                roundingIncrement: formData.roundingIncrement ? Number(formData.roundingIncrement) : undefined,
            });
            router.push('/finance/unit-types');
        } catch (reason) {
            setErrors({ submit: reason instanceof Error ? reason.message : 'Unable to create unit type.' });
        } finally {
            setBusy(false);
        }
    };

    const handleCodeChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        const value = e.target.value.toUpperCase().replace(/[^A-Z0-9]/g, '');
        setFormData({ ...formData, code: value });
    };

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div>
                <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                    <Ruler className="h-8 w-8" />
                    New Unit Type
                </h1>
                <p className="text-muted-foreground">
                    Create a new unit type for tracking quantities
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
                        <BreadcrumbLink href="/finance/unit-types">Unit Types</BreadcrumbLink>
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
                        <CardTitle>Unit Type Details</CardTitle>
                        <CardDescription>
                            Define a new unit type to categorize your unit accounts
                        </CardDescription>
                    </CardHeader>
                    <CardContent className="space-y-6">
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                            {/* Code */}
                            <div className="space-y-2">
                                <Label htmlFor="code">
                                    Code <span className="text-destructive">*</span>
                                </Label>
                                <Input
                                    id="code"
                                    placeholder="e.g., EMP, SQFT, HRS"
                                    value={formData.code}
                                    onChange={handleCodeChange}
                                    maxLength={20}
                                    className={errors.code ? 'border-destructive' : ''}
                                />
                                {errors.code && (
                                    <p className="text-sm text-destructive">{errors.code}</p>
                                )}
                                <p className="text-xs text-muted-foreground">
                                    Uppercase letters and numbers only. Max 20 characters.
                                </p>
                            </div>

                            {/* Name */}
                            <div className="space-y-2">
                                <Label htmlFor="name">
                                    Name <span className="text-destructive">*</span>
                                </Label>
                                <Input
                                    id="name"
                                    placeholder="e.g., Employees, Square Footage"
                                    value={formData.name}
                                    onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                                    maxLength={100}
                                    className={errors.name ? 'border-destructive' : ''}
                                />
                                {errors.name && (
                                    <p className="text-sm text-destructive">{errors.name}</p>
                                )}
                            </div>

                            {/* Decimal Places */}
                            <div className="space-y-2">
                                <Label htmlFor="decimalPlaces">Decimal Places</Label>
                                <Select
                                    value={formData.decimalPlaces}
                                    onValueChange={(value) => setFormData({ ...formData, decimalPlaces: value })}
                                >
                                    <SelectTrigger>
                                        <SelectValue />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="0">0 (Whole numbers)</SelectItem>
                                        <SelectItem value="1">1</SelectItem>
                                        <SelectItem value="2">2 (Default)</SelectItem>
                                        <SelectItem value="3">3</SelectItem>
                                        <SelectItem value="4">4</SelectItem>
                                        <SelectItem value="5">5</SelectItem>
                                        <SelectItem value="6">6 (High precision)</SelectItem>
                                    </SelectContent>
                                </Select>
                                <p className="text-xs text-muted-foreground">
                                    Precision for quantity values. Use 0 for counts like employees.
                                </p>
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="roundingIncrement">Quantity Increment</Label>
                                <Input
                                    id="roundingIncrement"
                                    type="number"
                                    disabled
                                    min={10 ** -Number(formData.decimalPlaces)}
                                    step={10 ** -Number(formData.decimalPlaces)}
                                    placeholder="Optional, e.g. 0.125"
                                    value={formData.roundingIncrement}
                                    onChange={(e) => setFormData({ ...formData, roundingIncrement: e.target.value })}
                                    className={errors.roundingIncrement ? 'border-destructive' : ''}
                                />
                                {errors.roundingIncrement && <p className="text-sm text-destructive">{errors.roundingIncrement}</p>}
                                <p className="text-xs text-muted-foreground">Reserved until every quantity write and posting adapter enforces the increment.</p>
                            </div>
                        </div>

                        {/* Description */}
                        <div className="space-y-2">
                            <Label htmlFor="description">Description</Label>
                            <Textarea
                                id="description"
                                placeholder="Optional description of what this unit type measures..."
                                value={formData.description}
                                onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                                rows={3}
                                maxLength={500}
                                className={errors.description ? 'border-destructive' : ''}
                            />
                            {errors.description && (
                                <p className="text-sm text-destructive">{errors.description}</p>
                            )}
                            <p className="text-xs text-muted-foreground">
                                {formData.description.length}/500 characters
                            </p>
                        </div>

                        {/* Actions */}
                        <div className="flex justify-end gap-4 pt-4 border-t">
                            {errors.submit && <p className="mr-auto text-sm text-destructive">{errors.submit}</p>}
                            <Link href="/finance/unit-types">
                                <Button type="button" variant="outline">
                                    <X className="mr-2 h-4 w-4" />
                                    Cancel
                                </Button>
                            </Link>
                            <Button type="submit" disabled={busy}>
                                <Save className="mr-2 h-4 w-4" />
                                Create Unit Type
                            </Button>
                        </div>
                    </CardContent>
                </Card>
            </form>
        </div>
    );
}
