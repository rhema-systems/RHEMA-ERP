'use client';

import React, { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
    ChevronRight,
    Home,
    Save,
    X,
    AlertTriangle,
} from 'lucide-react';
import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
} from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from '@/components/ui/select';
import { Alert, AlertDescription } from '@/components/ui/alert';

// Mock data
const MOCK_UNIT_ACCOUNTS = [
    { id: '1', number: 'U-1100', name: 'Sales Department Employees', unitTypeCode: 'EMP' },
    { id: '2', number: 'U-1200', name: 'Engineering Department Employees', unitTypeCode: 'EMP' },
    { id: '3', number: 'U-2100', name: 'Office Space - HQ', unitTypeCode: 'SQFT' },
    { id: '4', number: 'U-3100', name: 'Production Hours', unitTypeCode: 'HRS' },
    { id: '5', number: 'U-4100', name: 'Units Produced', unitTypeCode: 'UNITS' },
];

const MOCK_FISCAL_YEARS = [
    { id: 'fy1', name: 'FY 2024' },
    { id: 'fy2', name: 'FY 2023' },
];

const MOCK_FISCAL_PERIODS = [
    { id: 'p1', name: 'January 2024', fiscalYearId: 'fy1' },
    { id: 'p2', name: 'February 2024', fiscalYearId: 'fy1' },
    { id: 'p3', name: 'March 2024', fiscalYearId: 'fy1' },
    { id: 'p4', name: 'April 2024', fiscalYearId: 'fy1' },
    { id: 'p5', name: 'May 2024', fiscalYearId: 'fy1' },
    { id: 'p6', name: 'June 2024', fiscalYearId: 'fy1' },
];

export default function NewBudgetEntryPage() {
    const router = useRouter();
    const [formData, setFormData] = useState({
        unitAccountId: '',
        fiscalYearId: '',
        fiscalPeriodId: '',
        budgetQuantity: '',
        notes: '',
        budgetVersion: 'Original',
    });
    const [errors, setErrors] = useState<Record<string, string>>({});

    const filteredPeriods = MOCK_FISCAL_PERIODS.filter(
        (p) => p.fiscalYearId === formData.fiscalYearId
    );

    const selectedAccount = MOCK_UNIT_ACCOUNTS.find((a) => a.id === formData.unitAccountId);

    const handleSubmit = (e: React.FormEvent) => {
        e.preventDefault();
        const newErrors: Record<string, string> = {};

        if (!formData.unitAccountId) newErrors.unitAccountId = 'Unit account is required';
        if (!formData.fiscalYearId) newErrors.fiscalYearId = 'Fiscal year is required';
        if (!formData.fiscalPeriodId) newErrors.fiscalPeriodId = 'Fiscal period is required';
        if (!formData.budgetQuantity || parseFloat(formData.budgetQuantity) < 0) {
            newErrors.budgetQuantity = 'Valid budget quantity is required';
        }

        if (Object.keys(newErrors).length > 0) {
            setErrors(newErrors);
            return;
        }

        console.log('Creating budget entry:', formData);
        alert('⚠️ DEMO FRONTEND UI: Budget entry would be created.');
        router.push('/finance/unit-budgets');
    };

    return (
        <div className="space-y-6">
            {/* Breadcrumbs */}
            <nav className="flex items-center space-x-2 text-sm text-muted-foreground">
                <Link href="/" className="flex items-center hover:text-foreground">
                    <Home className="h-4 w-4" />
                </Link>
                <ChevronRight className="h-4 w-4" />
                <Link href="/finance" className="hover:text-foreground">Finance</Link>
                <ChevronRight className="h-4 w-4" />
                <Link href="/finance/unit-budgets" className="hover:text-foreground">Unit Budgets</Link>
                <ChevronRight className="h-4 w-4" />
                <span className="text-foreground">New Entry</span>
            </nav>

            <Alert className="border-yellow-500 bg-yellow-50">
                <AlertTriangle className="h-4 w-4 text-yellow-600" />
                <AlertDescription className="text-yellow-700">
                    <strong>⚠️ DEMO FRONTEND UI</strong>
                </AlertDescription>
            </Alert>

            <form onSubmit={handleSubmit}>
                <div className="flex items-center justify-between mb-6">
                    <div>
                        <h1 className="text-3xl font-bold tracking-tight">New Budget Entry</h1>
                        <p className="text-muted-foreground">Create a budget for a unit account period</p>
                    </div>
                    <div className="flex gap-2">
                        <Button type="button" variant="outline" onClick={() => router.back()}>
                            <X className="mr-2 h-4 w-4" />
                            Cancel
                        </Button>
                        <Button type="submit">
                            <Save className="mr-2 h-4 w-4" />
                            Create Budget
                        </Button>
                    </div>
                </div>

                <div className="grid gap-6 md:grid-cols-2">
                    {/* Account Selection */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Account & Period</CardTitle>
                            <CardDescription>Select the unit account and budget period</CardDescription>
                        </CardHeader>
                        <CardContent className="space-y-4">
                            <div className="space-y-2">
                                <Label>Unit Account *</Label>
                                <Select
                                    value={formData.unitAccountId}
                                    onValueChange={(v) => setFormData({ ...formData, unitAccountId: v })}
                                >
                                    <SelectTrigger className={errors.unitAccountId ? 'border-red-500' : ''}>
                                        <SelectValue placeholder="Select unit account" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        {MOCK_UNIT_ACCOUNTS.map((acc) => (
                                            <SelectItem key={acc.id} value={acc.id}>
                                                {acc.number} - {acc.name} ({acc.unitTypeCode})
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                {errors.unitAccountId && <p className="text-sm text-red-500">{errors.unitAccountId}</p>}
                            </div>

                            <div className="space-y-2">
                                <Label>Fiscal Year *</Label>
                                <Select
                                    value={formData.fiscalYearId}
                                    onValueChange={(v) => setFormData({ ...formData, fiscalYearId: v, fiscalPeriodId: '' })}
                                >
                                    <SelectTrigger className={errors.fiscalYearId ? 'border-red-500' : ''}>
                                        <SelectValue placeholder="Select fiscal year" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        {MOCK_FISCAL_YEARS.map((fy) => (
                                            <SelectItem key={fy.id} value={fy.id}>
                                                {fy.name}
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                {errors.fiscalYearId && <p className="text-sm text-red-500">{errors.fiscalYearId}</p>}
                            </div>

                            <div className="space-y-2">
                                <Label>Fiscal Period *</Label>
                                <Select
                                    value={formData.fiscalPeriodId}
                                    onValueChange={(v) => setFormData({ ...formData, fiscalPeriodId: v })}
                                    disabled={!formData.fiscalYearId}
                                >
                                    <SelectTrigger className={errors.fiscalPeriodId ? 'border-red-500' : ''}>
                                        <SelectValue placeholder="Select period" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        {filteredPeriods.map((p) => (
                                            <SelectItem key={p.id} value={p.id}>
                                                {p.name}
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                {errors.fiscalPeriodId && <p className="text-sm text-red-500">{errors.fiscalPeriodId}</p>}
                            </div>
                        </CardContent>
                    </Card>

                    {/* Budget Details */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Budget Details</CardTitle>
                            <CardDescription>Enter the budgeted quantity</CardDescription>
                        </CardHeader>
                        <CardContent className="space-y-4">
                            <div className="space-y-2">
                                <Label htmlFor="budgetQuantity">
                                    Budget Quantity *{selectedAccount && ` (${selectedAccount.unitTypeCode})`}
                                </Label>
                                <Input
                                    id="budgetQuantity"
                                    type="number"
                                    value={formData.budgetQuantity}
                                    onChange={(e) => setFormData({ ...formData, budgetQuantity: e.target.value })}
                                    placeholder="Enter quantity"
                                    step="0.01"
                                    className={errors.budgetQuantity ? 'border-red-500' : ''}
                                />
                                {errors.budgetQuantity && <p className="text-sm text-red-500">{errors.budgetQuantity}</p>}
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="budgetVersion">Budget Version</Label>
                                <Select
                                    value={formData.budgetVersion}
                                    onValueChange={(v) => setFormData({ ...formData, budgetVersion: v })}
                                >
                                    <SelectTrigger>
                                        <SelectValue />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="Original">Original</SelectItem>
                                        <SelectItem value="Revised">Revised</SelectItem>
                                        <SelectItem value="Forecast">Forecast</SelectItem>
                                    </SelectContent>
                                </Select>
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="notes">Notes</Label>
                                <Textarea
                                    id="notes"
                                    value={formData.notes}
                                    onChange={(e) => setFormData({ ...formData, notes: e.target.value })}
                                    placeholder="Optional notes about this budget entry..."
                                    rows={3}
                                />
                            </div>
                        </CardContent>
                    </Card>
                </div>
            </form>
        </div>
    );
}
