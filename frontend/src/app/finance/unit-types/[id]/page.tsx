'use client';

import React, { useState } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Ruler, Save, X, Trash2 } from 'lucide-react';
import Link from 'next/link';
import type { UnitType } from '@/types/unit-accounts';

// MOCK DATA - Simulating fetching by ID
const MOCK_UNIT_TYPES: Record<string, UnitType> = {
    'ut-1': {
        id: 'ut-1',
        code: 'EMP',
        name: 'Employees',
        description: 'Full-time equivalent employee count',
        decimalPlaces: 0,
        isActive: true,
        createdAt: '2024-01-01T00:00:00Z',
        createdBy: 'admin',
    },
    'ut-2': {
        id: 'ut-2',
        code: 'SQFT',
        name: 'Square Footage',
        description: 'Office and warehouse floor space in square feet',
        decimalPlaces: 2,
        isActive: true,
        createdAt: '2024-01-01T00:00:00Z',
        createdBy: 'admin',
    },
};

export default function EditUnitTypePage() {
    const router = useRouter();
    const params = useParams();
    const id = params.id as string;

    // Simulate fetching the unit type
    const existingType = MOCK_UNIT_TYPES[id];

    const [formData, setFormData] = useState({
        code: existingType?.code || '',
        name: existingType?.name || '',
        description: existingType?.description || '',
        decimalPlaces: existingType?.decimalPlaces?.toString() || '2',
        isActive: existingType?.isActive ?? true,
    });
    const [errors, setErrors] = useState<Record<string, string>>({});

    const validateForm = () => {
        const newErrors: Record<string, string> = {};

        if (!formData.name.trim()) {
            newErrors.name = 'Name is required';
        } else if (formData.name.length > 100) {
            newErrors.name = 'Name must be 100 characters or less';
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
        console.log('Updating unit type:', { id, ...formData });

        router.push('/finance/unit-types');
    };

    const handleDelete = () => {
        if (confirm('Are you sure you want to delete this unit type? This action cannot be undone.')) {
            // TODO: Replace with API call
            console.log('Deleting unit type:', id);

            router.push('/finance/unit-types');
        }
    };

    if (!existingType) {
        return (
            <div className="space-y-6">
                <Card>
                    <CardContent className="py-12 text-center">
                        <p className="text-muted-foreground">Unit type not found.</p>
                        <Link href="/finance/unit-types">
                            <Button className="mt-4" variant="outline">
                                Back to Unit Types
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
                        <Ruler className="h-8 w-8" />
                        Edit Unit Type
                    </h1>
                    <p className="text-muted-foreground">
                        Modify unit type: <span className="font-mono font-semibold">{formData.code}</span>
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
                        <BreadcrumbLink href="/finance/unit-types">Unit Types</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>{formData.code}</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Form */}
            <form onSubmit={handleSubmit}>
                <Card>
                    <CardHeader>
                        <CardTitle>Unit Type Details</CardTitle>
                        <CardDescription>
                            Update the unit type information. Code cannot be changed after creation.
                        </CardDescription>
                    </CardHeader>
                    <CardContent className="space-y-6">
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                            {/* Code (Read-only) */}
                            <div className="space-y-2">
                                <Label htmlFor="code">Code</Label>
                                <Input
                                    id="code"
                                    value={formData.code}
                                    disabled
                                    className="bg-muted"
                                />
                                <p className="text-xs text-muted-foreground">
                                    Code cannot be changed after creation.
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
                                <p className="text-xs text-muted-foreground">
                                    Inactive unit types cannot be used for new accounts.
                                </p>
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
                            <Link href="/finance/unit-types">
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
    );
}
