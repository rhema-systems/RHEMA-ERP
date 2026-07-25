'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogClose, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { CalendarDays, Lock, Unlock, LockKeyhole, AlertTriangle, CheckCircle2, RotateCw } from 'lucide-react';
import type { FiscalPeriod, ModuleDefinition } from '@/types/finance';
import { financeDataService } from '@/services/finance/finance-data.service';
import { useToast } from '@/hooks/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { ModuleLockManager } from '@/components/finance/fiscal-periods/ModuleLockManager';

export default function FiscalPeriodsPage() {
    const { toast } = useToast();
    const { hasPermission } = useAuth();
    const canAdminister = hasPermission('Finance.Admin');
    const canClose = hasPermission('Finance.PeriodClose');
    const canReopen = hasPermission('Finance.PeriodReopen');
    const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
    const [modules, setModules] = useState<ModuleDefinition[]>([]);
    const [loading, setLoading] = useState(true);
    const [filterYear, setFilterYear] = useState('all');
    const [filterStatus, setFilterStatus] = useState('all');
    const [selectedPeriod, setSelectedPeriod] = useState<FiscalPeriod | null>(null);
    const [reopenReason, setReopenReason] = useState('');
    const [processing, setProcessing] = useState(false);

    const loadData = useCallback(async () => {
        try {
            setLoading(true);
            const [periodsData, modulesData] = await Promise.all([
                financeDataService.getFiscalPeriods(),
                financeDataService.getModuleDefinitions()
            ]);
            setPeriods(periodsData);
            setModules(modulesData);
        } catch (error: any) {
            console.error('Error loading data:', error);
            toast({
                title: 'Error',
                description: 'Failed to load fiscal periods data',
                variant: 'destructive',
            });
        } finally {
            setLoading(false);
        }
    }, [toast]);

    useEffect(() => {
        loadData();
    }, [loadData]);

    const getPeriodStatus = (period: FiscalPeriod): string => {
        const legacyPeriod = period as FiscalPeriod & { status?: string; isOpen?: boolean };

        return period.periodStatus
            || legacyPeriod.status
            || (period.isLocked ? 'Locked' : period.isClosed ? 'Closed' : legacyPeriod.isOpen ? 'Open' : 'Future');
    };

    const filteredPeriods = periods.filter((period) => {
        if (filterStatus !== 'all' && getPeriodStatus(period) !== filterStatus) return false;
        // Simple year filter logic - in real app might need more robust date parsing
        if (filterYear !== 'all' && !period.periodName.includes(filterYear) && !period.startDate.startsWith(filterYear)) return false;
        return true;
    });

    const handleClosePeriod = async (periodId: string) => {
        try {
            setProcessing(true);
            await financeDataService.closeFiscalPeriod(periodId);
            toast({ title: "Success", description: "Fiscal period closed successfully" });
            loadData();
            setSelectedPeriod(null);
        } catch (error: any) {
            toast({
                title: 'Error',
                description: error.message || 'Failed to close period',
                variant: 'destructive',
            });
        } finally {
            setProcessing(false);
        }
    };

    const handleReopenPeriod = async (periodId: string) => {
        const reason = reopenReason.trim();

        if (!reason) {
            toast({
                title: 'Reason required',
                description: 'Enter a reason before reopening this fiscal period.',
                variant: 'destructive',
            });
            return;
        }

        try {
            setProcessing(true);
            await financeDataService.reopenFiscalPeriod(periodId, reason);
            toast({ title: "Success", description: "Fiscal period reopened successfully" });
            setReopenReason('');
            await loadData();
        } catch (error: any) {
            toast({
                title: 'Error',
                description: error.message || 'Failed to reopen period',
                variant: 'destructive',
            });
        } finally {
            setProcessing(false);
        }
    };

    // Note: Global locking usually not exposed in UI directly to standard users, 
    // but implemented here for completeness matching previous mock
    const handleLockPeriod = async (periodId: string) => {
        toast({ title: "Info", description: "Global locking requires admin verification. Use Module Locks for granular control." });
    };

    const formatDate = (dateString: string) => {
        if (!dateString) return '-';
        return new Date(dateString).toLocaleDateString('en-US', {
            year: 'numeric',
            month: 'short',
            day: 'numeric',
        });
    };

    const getStatusIcon = (status: string) => {
        switch (status) {
            case 'Open': return <Unlock className="h-4 w-4" />;
            case 'Closed': return <Lock className="h-4 w-4" />;
            case 'Locked': return <LockKeyhole className="h-4 w-4" />;
            default: return null;
        }
    };

    const getStatusBadge = (status: string) => {
        const variants: Record<string, { variant: 'default' | 'secondary' | 'destructive'; className: string }> = {
            Open: { variant: 'default', className: 'bg-green-500 hover:bg-green-600' },
            Closed: { variant: 'secondary', className: '' },
            Locked: { variant: 'destructive', className: '' },
            Future: { variant: 'secondary', className: 'bg-blue-100 text-blue-700 hover:bg-blue-200' },
        };
        const config = variants[status] || { variant: 'default', className: '' };
        return (
            <Badge variant={config.variant} className={config.className}>
                {getStatusIcon(status)}
                <span className="ml-1">{status}</span>
            </Badge>
        );
    };

    // Extract unique years from periods for filter
    const uniqueYears = Array.from(new Set(periods.map(p => new Date(p.startDate).getFullYear().toString()))).sort((a, b) => b.localeCompare(a));

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
                </div>
                <Button variant="outline" size="sm" onClick={loadData} disabled={loading}>
                    <RotateCw className={`mr-2 h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
                    Refresh
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
                        <BreadcrumbPage>Fiscal Periods</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Filters */}
            <Card>
                <CardHeader className="pb-3">
                    <CardTitle className="text-base">Filters</CardTitle>
                </CardHeader>
                <CardContent>
                    <div className="grid grid-cols-2 gap-4">
                        <div className="space-y-2">
                            <label className="text-sm font-medium">Fiscal Year</label>
                            <Select value={filterYear} onValueChange={setFilterYear}>
                                <SelectTrigger>
                                    <SelectValue placeholder="Select Year" />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="all">All Years</SelectItem>
                                    {uniqueYears.map(year => (
                                        <SelectItem key={year} value={year}>{year}</SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2">
                            <label className="text-sm font-medium">Status</label>
                            <Select value={filterStatus} onValueChange={setFilterStatus}>
                                <SelectTrigger>
                                    <SelectValue placeholder="Select Status" />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="all">All Statuses</SelectItem>
                                    <SelectItem value="Open">Open</SelectItem>
                                    <SelectItem value="Closed">Closed</SelectItem>
                                    <SelectItem value="Locked">Locked</SelectItem>
                                    <SelectItem value="Future">Future</SelectItem>
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
                        {loading && periods.length === 0 ? (
                            <div className="p-8 text-center text-muted-foreground">Loading periods...</div>
                        ) : (
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
                                    {filteredPeriods.length === 0 ? (
                                        <tr>
                                            <td colSpan={6} className="p-4 text-center text-muted-foreground">No periods found matching filters</td>
                                        </tr>
                                    ) : (
                                        filteredPeriods.map((period) => {
                                            const status = getPeriodStatus(period);

                                            return (
                                            <tr key={period.id} className="border-b hover:bg-muted/50">
                                                <td className="p-4 font-mono font-semibold">{period.periodNumber}</td>
                                                <td className="p-4 font-medium">{period.periodName}</td>
                                                <td className="p-4">{formatDate(period.startDate)}</td>
                                                <td className="p-4">{formatDate(period.endDate)}</td>
                                                <td className="p-4">
                                                    <div className="flex flex-col gap-1">
                                                        {getStatusBadge(status)}
                                                        {period.isPartiallyLocked && (
                                                            <Badge variant="outline" className="text-xs w-fit border-orange-200 text-orange-700 bg-orange-50">
                                                                <Lock className="h-3 w-3 mr-1" />
                                                                Partial Lock
                                                            </Badge>
                                                        )}
                                                    </div>
                                                </td>
                                                <td className="p-4 text-right">
                                                    <div className="flex justify-end gap-2">
                                                        {/* Module Lock Manager */}
                                                        {(canAdminister || canClose || canReopen) && (
                                                            <ModuleLockManager
                                                                period={period}
                                                                modules={modules}
                                                                canLock={canClose || canAdminister}
                                                                canReopen={canReopen || canAdminister}
                                                                onUpdate={loadData}
                                                            />
                                                        )}

                                                        {/* Period Actions */}
                                                        {canClose && status === 'Open' && (
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
                                                                        }} disabled={processing}>
                                                                            {processing ? 'Closing...' : 'Close Period'}
                                                                        </Button>
                                                                    </DialogFooter>
                                                                </DialogContent>
                                                            </Dialog>
                                                        )}
                                                        {canReopen && status === 'Closed' && (
                                                            <Dialog>
                                                                <DialogTrigger asChild>
                                                                    <Button
                                                                        variant="outline"
                                                                        size="sm"
                                                                        onClick={() => setReopenReason('')}
                                                                    >
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
                                                                    <div className="space-y-4 py-4">
                                                                        <div className="bg-yellow-50 p-3 rounded text-sm">
                                                                            <p className="font-semibold mb-1 flex items-center gap-2">
                                                                                <AlertTriangle className="h-4 w-4" />
                                                                                Warning:
                                                                            </p>
                                                                            <p>Reopening a period will allow modifications to financial data for this period. Ensure this is necessary and authorized.</p>
                                                                        </div>
                                                                        <div className="space-y-2">
                                                                            <Label htmlFor={`reopen-reason-${period.id}`}>Reason</Label>
                                                                            <Textarea
                                                                                id={`reopen-reason-${period.id}`}
                                                                                value={reopenReason}
                                                                                onChange={(event) => setReopenReason(event.target.value)}
                                                                                placeholder="Why is this period being reopened?"
                                                                                maxLength={1000}
                                                                                rows={3}
                                                                            />
                                                                        </div>
                                                                    </div>
                                                                    <DialogFooter>
                                                                        <DialogClose asChild>
                                                                            <Button variant="outline" onClick={() => setReopenReason('')}>
                                                                                Cancel
                                                                            </Button>
                                                                        </DialogClose>
                                                                        <Button onClick={() => handleReopenPeriod(period.id)} disabled={processing}>
                                                                            {processing ? 'Reopening...' : 'Reopen Period'}
                                                                        </Button>
                                                                    </DialogFooter>
                                                                </DialogContent>
                                                            </Dialog>
                                                        )}
                                                    </div>
                                                </td>
                                            </tr>
                                            );
                                        })
                                    )}
                                </tbody>
                            </table>
                        )}
                    </div>
                </CardContent>
            </Card>
        </div>
    );
}
