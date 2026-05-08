'use client';

import React, { useState } from 'react';
import Link from 'next/link';
import { useRouter, useParams } from 'next/navigation';
import {
    ChevronRight,
    Home,
    Save,
    Trash2,
    AlertTriangle,
    TrendingUp,
    TrendingDown,
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
import { Badge } from '@/components/ui/badge';
import { Switch } from '@/components/ui/switch';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from '@/components/ui/select';
import { Alert, AlertDescription } from '@/components/ui/alert';

// Mock data for the budget entry
const MOCK_BUDGET = {
    id: '1',
    unitAccountId: '1',
    unitAccountNumber: 'U-1100',
    unitAccountName: 'Sales Department Employees',
    unitTypeCode: 'EMP',
    fiscalYearId: 'fy1',
    fiscalYearName: 'FY 2024',
    fiscalPeriodId: 'p3',
    fiscalPeriodName: 'March 2024',
    budgetQuantity: 25,
    actualQuantity: 23,
    variance: -2,
    variancePercent: -8.0,
    isFavorable: true,
    notes: 'Initial budget for Q1 headcount',
    budgetVersion: 'Original',
    isActive: true,
};

export default function EditBudgetEntryPage() {
    const router = useRouter();
    const params = useParams();
    const budgetId = params.id as string;

    const [formData, setFormData] = useState({
        budgetQuantity: MOCK_BUDGET.budgetQuantity.toString(),
        notes: MOCK_BUDGET.notes,
        budgetVersion: MOCK_BUDGET.budgetVersion,
        isActive: MOCK_BUDGET.isActive,
    });

    const handleSubmit = (e: React.FormEvent) => {
        e.preventDefault();
        console.log('Updating budget entry:', formData);

        router.push('/finance/unit-budgets');
    };

    const handleDelete = () => {
        if (confirm('Are you sure you want to delete this budget entry?')) {

            router.push('/finance/unit-budgets');
        }
    };

    const getVarianceColor = () => {
        if (MOCK_BUDGET.variance === 0) return 'text-blue-600';
        return MOCK_BUDGET.isFavorable ? 'text-green-600' : 'text-red-600';
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
                <span className="text-foreground">{MOCK_BUDGET.unitAccountNumber}</span>
            </nav>



            <form onSubmit={handleSubmit}>
                <div className="flex items-center justify-between mb-6">
                    <div>
                        <div className="flex items-center gap-3">
                            <h1 className="text-3xl font-bold tracking-tight">{MOCK_BUDGET.unitAccountNumber}</h1>
                            <Badge variant="outline">{MOCK_BUDGET.unitTypeCode}</Badge>
                            <Badge className={formData.isActive ? 'bg-green-100 text-green-800' : ''}>
                                {formData.isActive ? 'Active' : 'Inactive'}
                            </Badge>
                        </div>
                        <p className="text-muted-foreground">
                            {MOCK_BUDGET.unitAccountName} • {MOCK_BUDGET.fiscalPeriodName}
                        </p>
                    </div>
                    <div className="flex gap-2">
                        <Button type="button" variant="destructive" onClick={handleDelete}>
                            <Trash2 className="mr-2 h-4 w-4" />
                            Delete
                        </Button>
                        <Button type="submit">
                            <Save className="mr-2 h-4 w-4" />
                            Save Changes
                        </Button>
                    </div>
                </div>

                <div className="grid gap-6 md:grid-cols-3">
                    {/* Variance Summary */}
                    <Card>
                        <CardHeader>
                            <CardTitle className="text-lg">Variance Summary</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-4">
                            <div className="flex justify-between items-center">
                                <span className="text-muted-foreground">Budget</span>
                                <span className="font-mono text-lg">{MOCK_BUDGET.budgetQuantity}</span>
                            </div>
                            <div className="flex justify-between items-center">
                                <span className="text-muted-foreground">Actual</span>
                                <span className="font-mono text-lg">{MOCK_BUDGET.actualQuantity}</span>
                            </div>
                            <hr />
                            <div className="flex justify-between items-center">
                                <span className="text-muted-foreground">Variance</span>
                                <div className={`flex items-center gap-1 ${getVarianceColor()}`}>
                                    {MOCK_BUDGET.isFavorable ? (
                                        <TrendingUp className="h-4 w-4" />
                                    ) : (
                                        <TrendingDown className="h-4 w-4" />
                                    )}
                                    <span className="font-mono text-lg font-bold">
                                        {MOCK_BUDGET.variance > 0 ? '+' : ''}{MOCK_BUDGET.variance}
                                    </span>
                                    <span className="text-sm">({MOCK_BUDGET.variancePercent}%)</span>
                                </div>
                            </div>
                            <Badge
                                variant="outline"
                                className={MOCK_BUDGET.isFavorable ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'}
                            >
                                {MOCK_BUDGET.isFavorable ? 'Favorable' : 'Unfavorable'}
                            </Badge>
                        </CardContent>
                    </Card>

                    {/* Budget Details */}
                    <Card className="md:col-span-2">
                        <CardHeader>
                            <CardTitle>Budget Details</CardTitle>
                            <CardDescription>Modify the budget entry</CardDescription>
                        </CardHeader>
                        <CardContent className="space-y-4">
                            <div className="grid gap-4 md:grid-cols-2">
                                <div className="space-y-2">
                                    <Label>Account</Label>
                                    <Input
                                        value={`${MOCK_BUDGET.unitAccountNumber} - ${MOCK_BUDGET.unitAccountName}`}
                                        disabled
                                        className="bg-muted"
                                    />
                                </div>
                                <div className="space-y-2">
                                    <Label>Period</Label>
                                    <Input value={MOCK_BUDGET.fiscalPeriodName} disabled className="bg-muted" />
                                </div>
                            </div>

                            <div className="grid gap-4 md:grid-cols-2">
                                <div className="space-y-2">
                                    <Label htmlFor="budgetQuantity">Budget Quantity ({MOCK_BUDGET.unitTypeCode})</Label>
                                    <Input
                                        id="budgetQuantity"
                                        type="number"
                                        value={formData.budgetQuantity}
                                        onChange={(e) => setFormData({ ...formData, budgetQuantity: e.target.value })}
                                        step="0.01"
                                    />
                                </div>
                                <div className="space-y-2">
                                    <Label>Budget Version</Label>
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
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="notes">Notes</Label>
                                <Textarea
                                    id="notes"
                                    value={formData.notes}
                                    onChange={(e) => setFormData({ ...formData, notes: e.target.value })}
                                    rows={3}
                                />
                            </div>

                            <div className="flex items-center space-x-2">
                                <Switch
                                    id="isActive"
                                    checked={formData.isActive}
                                    onCheckedChange={(checked) => setFormData({ ...formData, isActive: checked })}
                                />
                                <Label htmlFor="isActive">Active</Label>
                            </div>
                        </CardContent>
                    </Card>
                </div>
            </form>
        </div>
    );
}
