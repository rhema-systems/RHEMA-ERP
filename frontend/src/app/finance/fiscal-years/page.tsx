'use client';

import React, { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Calendar, Plus, ChevronDown, ChevronRight, Lock } from 'lucide-react';
import type { FiscalYear, FiscalPeriod } from '@/types/finance';

// MOCK DATA
const MOCK_FISCAL_YEARS: (FiscalYear & { periods: FiscalPeriod[] })[] = [
    {
        id: 'fy-2024',
        tenantId: 'tenant-1',
        year: 2024,
        startDate: '2024-01-01T00:00:00Z',
        endDate: '2024-12-31T00:00:00Z',
        isClosed: false,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
        periods: [
            {
                id: 'period-2024-01',
                tenantId: 'tenant-1',
                fiscalYearId: 'fy-2024',
                periodNumber: 1,
                periodName: 'January 2024',
                startDate: '2024-01-01T00:00:00Z',
                endDate: '2024-01-31T00:00:00Z',
                status: 'Closed',
                createdAt: '2024-01-01T00:00:00Z',
                updatedAt: '2024-01-01T00:00:00Z',
            },
            {
                id: 'period-2024-02',
                tenantId: 'tenant-1',
                fiscalYearId: 'fy-2024',
                periodNumber: 2,
                periodName: 'February 2024',
                startDate: '2024-02-01T00:00:00Z',
                endDate: '2024-02-29T00:00:00Z',
                status: 'Open',
                createdAt: '2024-02-01T00:00:00Z',
                updatedAt: '2024-02-01T00:00:00Z',
            },
            {
                id: 'period-2024-03',
                tenantId: 'tenant-1',
                fiscalYearId: 'fy-2024',
                periodNumber: 3,
                periodName: 'March 2024',
                startDate: '2024-03-01T00:00:00Z',
                endDate: '2024-03-31T00:00:00Z',
                status: 'Open',
                createdAt: '2024-03-01T00:00:00Z',
                updatedAt: '2024-03-01T00:00:00Z',
            },
        ],
    },
    {
        id: 'fy-2023',
        tenantId: 'tenant-1',
        year: 2023,
        startDate: '2023-01-01T00:00:00Z',
        endDate: '2023-12-31T00:00:00Z',
        isClosed: true,
        createdAt: '2023-01-01T00:00:00Z',
        updatedAt: '2023-12-31T00:00:00Z',
        periods: [],
    },
];

export default function FiscalYearsPage() {
    const [fiscalYears, setFiscalYears] = useState(MOCK_FISCAL_YEARS);
    const [expandedYears, setExpandedYears] = useState<Set<string>>(new Set(['fy-2024']));
    const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
    const [formData, setFormData] = useState({
        year: new Date().getFullYear(),
        startDate: '',
        endDate: '',
        periodType: 'Monthly' as 'Monthly' | 'Quarterly',
    });

    const toggleYear = (yearId: string) => {
        const newExpanded = new Set(expandedYears);
        if (newExpanded.has(yearId)) {
            newExpanded.delete(yearId);
        } else {
            newExpanded.add(yearId);
        }
        setExpandedYears(newExpanded);
    };

    const handleCreate = () => {
        // Generate periods based on period type
        const periods: FiscalPeriod[] = [];
        const startDate = new Date(formData.startDate);
        const endDate = new Date(formData.endDate);

        if (formData.periodType === 'Monthly') {
            for (let i = 0; i < 12; i++) {
                const periodStart = new Date(startDate.getFullYear(), i, 1);
                const periodEnd = new Date(startDate.getFullYear(), i + 1, 0);
                periods.push({
                    id: `period-${formData.year}-${String(i + 1).padStart(2, '0')}`,
                    tenantId: 'tenant-1',
                    fiscalYearId: `fy-${formData.year}`,
                    periodNumber: i + 1,
                    periodName: periodStart.toLocaleDateString('en-US', { month: 'long', year: 'numeric' }),
                    startDate: periodStart.toISOString(),
                    endDate: periodEnd.toISOString(),
                    status: 'Open',
                    createdAt: new Date().toISOString(),
                    updatedAt: new Date().toISOString(),
                });
            }
        } else {
            // Quarterly periods
            const quarters = ['Q1', 'Q2', 'Q3', 'Q4'];
            for (let i = 0; i < 4; i++) {
                const periodStart = new Date(startDate.getFullYear(), i * 3, 1);
                const periodEnd = new Date(startDate.getFullYear(), (i + 1) * 3, 0);
                periods.push({
                    id: `period-${formData.year}-Q${i + 1}`,
                    tenantId: 'tenant-1',
                    fiscalYearId: `fy-${formData.year}`,
                    periodNumber: i + 1,
                    periodName: `${quarters[i]} ${formData.year}`,
                    startDate: periodStart.toISOString(),
                    endDate: periodEnd.toISOString(),
                    status: 'Open',
                    createdAt: new Date().toISOString(),
                    updatedAt: new Date().toISOString(),
                });
            }
        }

        const newYear: FiscalYear & { periods: FiscalPeriod[] } = {
            id: `fy-${formData.year}`,
            tenantId: 'tenant-1',
            year: formData.year,
            startDate: new Date(formData.startDate).toISOString(),
            endDate: new Date(formData.endDate).toISOString(),
            isClosed: false,
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
            periods,
        };

        setFiscalYears([newYear, ...fiscalYears]);
        setExpandedYears(new Set([...expandedYears, newYear.id]));
        setIsCreateDialogOpen(false);
        resetForm();
    };

    const handleCloseYear = (yearId: string) => {
        setFiscalYears(
            fiscalYears.map((fy) =>
                fy.id === yearId
                    ? { ...fy, isClosed: true, updatedAt: new Date().toISOString() }
                    : fy
            )
        );
    };

    const resetForm = () => {
        const currentYear = new Date().getFullYear();
        setFormData({
            year: currentYear,
            startDate: `${currentYear}-01-01`,
            endDate: `${currentYear}-12-31`,
            periodType: 'Monthly',
        });
    };

    const formatDate = (dateString: string) => {
        return new Date(dateString).toLocaleDateString('en-US', {
            year: 'numeric',
            month: 'short',
            day: 'numeric',
        });
    };

    const getStatusBadge = (status: string) => {
        const variants: Record<string, 'default' | 'secondary' | 'destructive'> = {
            Open: 'default',
            Closed: 'secondary',
            Locked: 'destructive',
        };
        return <Badge variant={variants[status] || 'default'}>{status}</Badge>;
    };

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                        <Calendar className="h-8 w-8" />
                        Fiscal Years
                    </h1>
                    <p className="text-muted-foreground">
                        Manage fiscal years and accounting periods
                    </p>
                    <p className="text-sm text-orange-600 mt-1">
                        ⚠️ DEMO FRONTEND UI
                    </p>
                </div>
                <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
                    <DialogTrigger asChild>
                        <Button onClick={resetForm}>
                            <Plus className="mr-2 h-4 w-4" />
                            Create Fiscal Year
                        </Button>
                    </DialogTrigger>
                    <DialogContent>
                        <DialogHeader>
                            <DialogTitle>Create Fiscal Year</DialogTitle>
                            <DialogDescription>
                                Create a new fiscal year with auto-generated periods
                            </DialogDescription>
                        </DialogHeader>
                        <div className="space-y-4 py-4">
                            <div className="space-y-2">
                                <Label htmlFor="year">Fiscal Year</Label>
                                <Input
                                    id="year"
                                    type="number"
                                    value={formData.year}
                                    onChange={(e) => setFormData({ ...formData, year: parseInt(e.target.value) || new Date().getFullYear() })}
                                />
                            </div>
                            <div className="grid grid-cols-2 gap-4">
                                <div className="space-y-2">
                                    <Label htmlFor="startDate">Start Date</Label>
                                    <Input
                                        id="startDate"
                                        type="date"
                                        value={formData.startDate}
                                        onChange={(e) => setFormData({ ...formData, startDate: e.target.value })}
                                    />
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="endDate">End Date</Label>
                                    <Input
                                        id="endDate"
                                        type="date"
                                        value={formData.endDate}
                                        onChange={(e) => setFormData({ ...formData, endDate: e.target.value })}
                                    />
                                </div>
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="periodType">Period Type</Label>
                                <Select
                                    value={formData.periodType}
                                    onValueChange={(value: any) => setFormData({ ...formData, periodType: value })}
                                >
                                    <SelectTrigger id="periodType">
                                        <SelectValue />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="Monthly">Monthly (12 periods)</SelectItem>
                                        <SelectItem value="Quarterly">Quarterly (4 periods)</SelectItem>
                                    </SelectContent>
                                </Select>
                            </div>
                            <div className="text-sm text-muted-foreground bg-blue-50 p-3 rounded">
                                <p className="font-semibold mb-1">Auto-generation:</p>
                                <p>
                                    {formData.periodType === 'Monthly' ? '12 monthly' : '4 quarterly'} periods will be
                                    automatically created for this fiscal year.
                                </p>
                            </div>
                        </div>
                        <DialogFooter>
                            <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                                Cancel
                            </Button>
                            <Button onClick={handleCreate}>Create Fiscal Year</Button>
                        </DialogFooter>
                    </DialogContent>
                </Dialog>
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
                        <BreadcrumbPage>Fiscal Years</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Fiscal Years List */}
            <Card>
                <CardHeader>
                    <CardTitle>Fiscal Years ({fiscalYears.length})</CardTitle>
                    <CardDescription>
                        View and manage fiscal years and their periods
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    <div className="space-y-4">
                        {fiscalYears.map((year) => (
                            <div key={year.id} className="border rounded-lg">
                                {/* Year Header */}
                                <div className="flex items-center justify-between p-4 bg-muted/50">
                                    <div className="flex items-center gap-4">
                                        <Button
                                            variant="ghost"
                                            size="sm"
                                            onClick={() => toggleYear(year.id)}
                                        >
                                            {expandedYears.has(year.id) ? (
                                                <ChevronDown className="h-4 w-4" />
                                            ) : (
                                                <ChevronRight className="h-4 w-4" />
                                            )}
                                        </Button>
                                        <div>
                                            <div className="flex items-center gap-2">
                                                <h3 className="font-semibold text-lg">FY {year.year}</h3>
                                                {year.isClosed && (
                                                    <Badge variant="secondary">
                                                        <Lock className="h-3 w-3 mr-1" />
                                                        Closed
                                                    </Badge>
                                                )}
                                            </div>
                                            <p className="text-sm text-muted-foreground">
                                                {formatDate(year.startDate)} - {formatDate(year.endDate)}
                                            </p>
                                        </div>
                                    </div>
                                    <div className="flex items-center gap-2">
                                        <Badge variant="outline">
                                            {year.periods.length} periods
                                        </Badge>
                                        {!year.isClosed && (
                                            <Dialog>
                                                <DialogTrigger asChild>
                                                    <Button variant="outline" size="sm">
                                                        Close Year
                                                    </Button>
                                                </DialogTrigger>
                                                <DialogContent>
                                                    <DialogHeader>
                                                        <DialogTitle>Close Fiscal Year {year.year}?</DialogTitle>
                                                        <DialogDescription>
                                                            This will close the fiscal year and all its periods. This action can be reversed.
                                                        </DialogDescription>
                                                    </DialogHeader>
                                                    <div className="py-4">
                                                        <p className="text-sm text-muted-foreground">
                                                            Before closing the year, ensure:
                                                        </p>
                                                        <ul className="list-disc list-inside space-y-1 mt-2 text-sm">
                                                            <li>All journal entries are posted</li>
                                                            <li>All periods are closed</li>
                                                            <li>Year-end adjustments are complete</li>
                                                            <li>Financial statements are finalized</li>
                                                        </ul>
                                                    </div>
                                                    <DialogFooter>
                                                        <Button variant="outline">Cancel</Button>
                                                        <Button onClick={() => handleCloseYear(year.id)}>
                                                            Close Fiscal Year
                                                        </Button>
                                                    </DialogFooter>
                                                </DialogContent>
                                            </Dialog>
                                        )}
                                    </div>
                                </div>

                                {/* Periods Table */}
                                {expandedYears.has(year.id) && year.periods.length > 0 && (
                                    <div className="p-4">
                                        <table className="w-full">
                                            <thead>
                                                <tr className="border-b">
                                                    <th className="p-2 text-left text-sm font-medium">Period</th>
                                                    <th className="p-2 text-left text-sm font-medium">Name</th>
                                                    <th className="p-2 text-left text-sm font-medium">Start Date</th>
                                                    <th className="p-2 text-left text-sm font-medium">End Date</th>
                                                    <th className="p-2 text-left text-sm font-medium">Status</th>
                                                </tr>
                                            </thead>
                                            <tbody>
                                                {year.periods.map((period) => (
                                                    <tr key={period.id} className="border-b hover:bg-muted/50">
                                                        <td className="p-2 text-sm">{period.periodNumber}</td>
                                                        <td className="p-2 text-sm font-medium">{period.periodName}</td>
                                                        <td className="p-2 text-sm">{formatDate(period.startDate)}</td>
                                                        <td className="p-2 text-sm">{formatDate(period.endDate)}</td>
                                                        <td className="p-2 text-sm">{getStatusBadge(period.status)}</td>
                                                    </tr>
                                                ))}
                                            </tbody>
                                        </table>
                                    </div>
                                )}
                            </div>
                        ))}
                    </div>
                </CardContent>
            </Card>
        </div>
    );
}
