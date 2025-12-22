'use client';

import React, { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { CalendarDays, Lock, Unlock, LockKeyhole, AlertTriangle, CheckCircle2 } from 'lucide-react';
import type { FiscalPeriod } from '@/types/finance';

// MOCK DATA
const MOCK_PERIODS: FiscalPeriod[] = [
    {
        id: 'period-2024-01',
        tenantId: 'tenant-1',
        fiscalYearId: 'fy-2024',
        periodNumber: 1,
        periodName: 'January 2024',
        startDate: '2024-01-01T00:00:00Z',
        endDate: '2024-01-31T00:00:00Z',
        status: 'Locked',
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-31T00:00:00Z',
    },
    {
        id: 'period-2024-02',
        tenantId: 'tenant-1',
        fiscalYearId: 'fy-2024',
        periodNumber: 2,
        periodName: 'February 2024',
        startDate: '2024-02-01T00:00:00Z',
        endDate: '2024-02-29T00:00:00Z',
        status: 'Closed',
        createdAt: '2024-02-01T00:00:00Z',
        updatedAt: '2024-02-29T00:00:00Z',
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
    {
        id: 'period-2024-04',
        tenantId: 'tenant-1',
        fiscalYearId: 'fy-2024',
        periodNumber: 4,
        periodName: 'April 2024',
        startDate: '2024-04-01T00:00:00Z',
        endDate: '2024-04-30T00:00:00Z',
        status: 'Open',
        createdAt: '2024-04-01T00:00:00Z',
        updatedAt: '2024-04-01T00:00:00Z',
    },
];

export default function FiscalPeriodsPage() {
    const [periods, setPeriods] = useState<FiscalPeriod[]>(MOCK_PERIODS);
    const [filterYear, setFilterYear] = useState('2024');
    const [filterStatus, setFilterStatus] = useState('all');
    const [selectedPeriod, setSelectedPeriod] = useState<FiscalPeriod | null>(null);

    const filteredPeriods = periods.filter((period) => {
        if (filterStatus !== 'all' && period.status !== filterStatus) return false;
        if (filterYear !== 'all' && !period.periodName.includes(filterYear)) return false;
        return true;
    });

    const handleClosePeriod = (periodId: string) => {
        setPeriods(
            periods.map((p) =>
                p.id === periodId
                    ? { ...p, status: 'Closed', updatedAt: new Date().toISOString() }
                    : p
            )
        );
    };

    const handleReopenPeriod = (periodId: string) => {
        setPeriods(
            periods.map((p) =>
                p.id === periodId
                    ? { ...p, status: 'Open', updatedAt: new Date().toISOString() }
                    : p
            )
        );
    };

    const handleLockPeriod = (periodId: string) => {
        setPeriods(
            periods.map((p) =>
                p.id === periodId
                    ? { ...p, status: 'Locked', updatedAt: new Date().toISOString() }
                    : p
            )
        );
    };

    const formatDate = (dateString: string) => {
        return new Date(dateString).toLocaleDateString('en-US', {
            year: 'numeric',
            month: 'short',
            day: 'numeric',
        });
    };

    const getStatusIcon = (status: string) => {
        switch (status) {
            case 'Open':
                return <Unlock className="h-4 w-4" />;
            case 'Closed':
                return <Lock className="h-4 w-4" />;
            case 'Locked':
                return <LockKeyhole className="h-4 w-4" />;
            default:
                return null;
        }
    };

    const getStatusBadge = (status: string) => {
        const variants: Record<string, { variant: 'default' | 'secondary' | 'destructive'; className: string }> = {
            Open: { variant: 'default', className: 'bg-green-500' },
            Closed: { variant: 'secondary', className: '' },
            Locked: { variant: 'destructive', className: '' },
        };
        const config = variants[status] || { variant: 'default', className: '' };
        return (
            <Badge variant={config.variant} className={config.className}>
                {getStatusIcon(status)}
                <span className="ml-1">{status}</span>
            </Badge>
        );
    };

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                        <CalendarDays className="h-8 w-8" />
                        Fiscal Periods
                    </h1>
                    <p className="text-muted-foreground">
                        Manage accounting periods and period close process
                    </p>
                    <p className="text-sm text-orange-600 mt-1">
                        ⚠️ DEMO FRONTEND UI
                    </p>
                </div>
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
                        <BreadcrumbPage>Fiscal Periods</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Filters */}
            <Card>
                <CardHeader>
                    <CardTitle>Filters</CardTitle>
                </CardHeader>
                <CardContent>
                    <div className="grid grid-cols-2 gap-4">
                        <div className="space-y-2">
                            <label className="text-sm font-medium">Fiscal Year</label>
                            <Select value={filterYear} onValueChange={setFilterYear}>
                                <SelectTrigger>
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="all">All Years</SelectItem>
                                    <SelectItem value="2024">2024</SelectItem>
                                    <SelectItem value="2023">2023</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2">
                            <label className="text-sm font-medium">Status</label>
                            <Select value={filterStatus} onValueChange={setFilterStatus}>
                                <SelectTrigger>
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="all">All Statuses</SelectItem>
                                    <SelectItem value="Open">Open</SelectItem>
                                    <SelectItem value="Closed">Closed</SelectItem>
                                    <SelectItem value="Locked">Locked</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>
                    </div>
                </CardContent>
            </Card>

            {/* Periods Table */}
            <Card>
                <CardHeader>
                    <CardTitle>Fiscal Periods ({filteredPeriods.length})</CardTitle>
                    <CardDescription>
                        View and manage accounting period statuses
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    <div className="rounded-md border">
                        <table className="w-full">
                            <thead>
                                <tr className="border-b bg-muted/50">
                                    <th className="p-4 text-left font-medium">Period</th>
                                    <th className="p-4 text-left font-medium">Name</th>
                                    <th className="p-4 text-left font-medium">Start Date</th>
                                    <th className="p-4 text-left font-medium">End Date</th>
                                    <th className="p-4 text-left font-medium">Status</th>
                                    <th className="p-4 text-right font-medium">Actions</th>
                                </tr>
                            </thead>
                            <tbody>
                                {filteredPeriods.map((period) => (
                                    <tr key={period.id} className="border-b hover:bg-muted/50">
                                        <td className="p-4 font-mono font-semibold">{period.periodNumber}</td>
                                        <td className="p-4 font-medium">{period.periodName}</td>
                                        <td className="p-4">{formatDate(period.startDate)}</td>
                                        <td className="p-4">{formatDate(period.endDate)}</td>
                                        <td className="p-4">{getStatusBadge(period.status)}</td>
                                        <td className="p-4 text-right space-x-2">
                                            {period.status === 'Open' && (
                                                <Dialog>
                                                    <DialogTrigger asChild>
                                                        <Button
                                                            variant="outline"
                                                            size="sm"
                                                            onClick={() => setSelectedPeriod(period)}
                                                        >
                                                            <Lock className="h-4 w-4 mr-1" />
                                                            Close
                                                        </Button>
                                                    </DialogTrigger>
                                                    <DialogContent>
                                                        <DialogHeader>
                                                            <DialogTitle>Close Period: {period.periodName}?</DialogTitle>
                                                            <DialogDescription>
                                                                Review the checklist before closing this period
                                                            </DialogDescription>
                                                        </DialogHeader>
                                                        <div className="space-y-4 py-4">
                                                            <div className="space-y-2">
                                                                <p className="text-sm font-semibold">Period Close Checklist:</p>
                                                                <div className="space-y-2">
                                                                    <div className="flex items-start gap-2">
                                                                        <CheckCircle2 className="h-4 w-4 text-green-500 mt-0.5" />
                                                                        <span className="text-sm">All journal entries posted</span>
                                                                    </div>
                                                                    <div className="flex items-start gap-2">
                                                                        <CheckCircle2 className="h-4 w-4 text-green-500 mt-0.5" />
                                                                        <span className="text-sm">Bank reconciliations complete</span>
                                                                    </div>
                                                                    <div className="flex items-start gap-2">
                                                                        <CheckCircle2 className="h-4 w-4 text-green-500 mt-0.5" />
                                                                        <span className="text-sm">Currency revaluation run</span>
                                                                    </div>
                                                                    <div className="flex items-start gap-2">
                                                                        <CheckCircle2 className="h-4 w-4 text-green-500 mt-0.5" />
                                                                        <span className="text-sm">Trial balance reviewed</span>
                                                                    </div>
                                                                    <div className="flex items-start gap-2">
                                                                        <AlertTriangle className="h-4 w-4 text-yellow-500 mt-0.5" />
                                                                        <span className="text-sm">Financial statements generated</span>
                                                                    </div>
                                                                </div>
                                                            </div>
                                                            <div className="bg-blue-50 p-3 rounded text-sm">
                                                                <p className="font-semibold mb-1">Note:</p>
                                                                <p>Closing a period prevents new transactions from being posted to it. You can reopen it later if needed.</p>
                                                            </div>
                                                        </div>
                                                        <DialogFooter>
                                                            <Button variant="outline">Cancel</Button>
                                                            <Button onClick={() => {
                                                                handleClosePeriod(period.id);
                                                                setSelectedPeriod(null);
                                                            }}>
                                                                Close Period
                                                            </Button>
                                                        </DialogFooter>
                                                    </DialogContent>
                                                </Dialog>
                                            )}
                                            {period.status === 'Closed' && (
                                                <>
                                                    <Dialog>
                                                        <DialogTrigger asChild>
                                                            <Button variant="outline" size="sm">
                                                                <Unlock className="h-4 w-4 mr-1" />
                                                                Reopen
                                                            </Button>
                                                        </DialogTrigger>
                                                        <DialogContent>
                                                            <DialogHeader>
                                                                <DialogTitle>Reopen Period: {period.periodName}?</DialogTitle>
                                                                <DialogDescription>
                                                                    This will allow new transactions to be posted to this period
                                                                </DialogDescription>
                                                            </DialogHeader>
                                                            <div className="py-4">
                                                                <div className="bg-yellow-50 p-3 rounded text-sm">
                                                                    <p className="font-semibold mb-1 flex items-center gap-2">
                                                                        <AlertTriangle className="h-4 w-4" />
                                                                        Warning:
                                                                    </p>
                                                                    <p>Reopening a period will allow modifications to financial data for this period. Ensure this is necessary and authorized.</p>
                                                                </div>
                                                            </div>
                                                            <DialogFooter>
                                                                <Button variant="outline">Cancel</Button>
                                                                <Button onClick={() => handleReopenPeriod(period.id)}>
                                                                    Reopen Period
                                                                </Button>
                                                            </DialogFooter>
                                                        </DialogContent>
                                                    </Dialog>
                                                    <Dialog>
                                                        <DialogTrigger asChild>
                                                            <Button variant="destructive" size="sm">
                                                                <LockKeyhole className="h-4 w-4 mr-1" />
                                                                Lock
                                                            </Button>
                                                        </DialogTrigger>
                                                        <DialogContent>
                                                            <DialogHeader>
                                                                <DialogTitle>Lock Period: {period.periodName}?</DialogTitle>
                                                                <DialogDescription>
                                                                    This is a permanent action that cannot be reversed
                                                                </DialogDescription>
                                                            </DialogHeader>
                                                            <div className="py-4">
                                                                <div className="bg-red-50 p-3 rounded text-sm">
                                                                    <p className="font-semibold mb-1 flex items-center gap-2">
                                                                        <AlertTriangle className="h-4 w-4 text-red-600" />
                                                                        Critical Warning:
                                                                    </p>
                                                                    <p className="mb-2">Locking a period is <strong>permanent and irreversible</strong>. Once locked:</p>
                                                                    <ul className="list-disc list-inside space-y-1">
                                                                        <li>No transactions can be posted</li>
                                                                        <li>No modifications can be made</li>
                                                                        <li>The period cannot be reopened</li>
                                                                        <li>This is typically done for audit compliance</li>
                                                                    </ul>
                                                                </div>
                                                            </div>
                                                            <DialogFooter>
                                                                <Button variant="outline">Cancel</Button>
                                                                <Button variant="destructive" onClick={() => handleLockPeriod(period.id)}>
                                                                    Permanently Lock Period
                                                                </Button>
                                                            </DialogFooter>
                                                        </DialogContent>
                                                    </Dialog>
                                                </>
                                            )}
                                            {period.status === 'Locked' && (
                                                <Badge variant="destructive" className="cursor-not-allowed">
                                                    <LockKeyhole className="h-3 w-3 mr-1" />
                                                    Permanently Locked
                                                </Badge>
                                            )}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </CardContent>
            </Card>
        </div>
    );
}
