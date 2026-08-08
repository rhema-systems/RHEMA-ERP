'use client';

import React, { useState, useEffect, useCallback } from 'react';
import Link from 'next/link';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogClose, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { CalendarDays, Lock, Unlock, LockKeyhole, AlertTriangle, FileDown, Loader2, RotateCw } from 'lucide-react';
import type { FiscalPeriod, ModuleDefinition } from '@/types/finance';
import { financeDataService } from '@/services/finance/finance-data.service';
import { useToast } from '@/hooks/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { ModuleLockManager } from '@/components/finance/fiscal-periods/ModuleLockManager';
import { CloseWorkspaceDialog } from '@/components/finance/fiscal-periods/CloseWorkspaceDialog';
import { DOCUMENT_TYPES, documentOutputService } from '@/services/document-output.service';

export default function FiscalPeriodsPage() {
    const { toast } = useToast();
    const { hasPermission } = useAuth();
    const canAdminister = hasPermission('Finance.Admin');
    const canOpen = hasPermission('Finance.PeriodOpen');
    const canClose = hasPermission('Finance.PeriodClose');
    const canReopen = hasPermission('Finance.PeriodReopen');
    const canApproveReopen = hasPermission('Finance.PeriodReopen.Approve');
    const canExportFinanceReports = hasPermission('Finance.Reports.Export');
    const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
    const [modules, setModules] = useState<ModuleDefinition[]>([]);
    const [loading, setLoading] = useState(true);
    const [filterYear, setFilterYear] = useState('all');
    const [filterStatus, setFilterStatus] = useState('all');
    const [openReason, setOpenReason] = useState('');
    const [reopenReason, setReopenReason] = useState('');
    const [reopenImpactAssessment, setReopenImpactAssessment] = useState('');
    const [reopenReviewComment, setReopenReviewComment] = useState('');
    const [processing, setProcessing] = useState(false);
    const [downloadingClosePackId, setDownloadingClosePackId] = useState<string | null>(null);

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

    const handleOpenPeriod = async (periodId: string) => {
        const reason = openReason.trim();
        if (reason.length < 10) {
            toast({
                title: 'Opening reason required',
                description: 'Provide at least 10 characters explaining why the period is being opened.',
                variant: 'destructive',
            });
            return;
        }

        try {
            setProcessing(true);
            await financeDataService.openFiscalPeriod(periodId, reason);
            toast({
                title: 'Period opened',
                description: 'The period is now available for posting, subject to its module locks.'
            });
            setOpenReason('');
            await loadData();
        } catch (error: any) {
            toast({
                title: 'Period could not be opened',
                description: error.message || 'The accounting period opening failed.',
                variant: 'destructive',
            });
        } finally {
            setProcessing(false);
        }
    };

    const handleRequestReopen = async (periodId: string) => {
        const reason = reopenReason.trim();
        const assessment = reopenImpactAssessment.trim();

        if (reason.length < 20 || assessment.length < 20) {
            toast({
                title: 'More detail required',
                description: 'Provide at least 20 characters for both the reason and affected-period assessment.',
                variant: 'destructive',
            });
            return;
        }

        try {
            setProcessing(true);
            await financeDataService.requestFiscalPeriodReopen(periodId, reason, assessment);
            toast({
                title: 'Reopen request submitted',
                description: 'The period remains closed until an independent higher-tier reviewer approves it.'
            });
            setReopenReason('');
            setReopenImpactAssessment('');
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

    const downloadClosePack = async (period: FiscalPeriod) => {
        if (!period.latestClosePackCycleId) return;

        try {
            setDownloadingClosePackId(period.latestClosePackCycleId);
            // The generic document-output service preserves the API-provided filename and routes
            // the request through the Finance export permission. The cycle identity guarantees
            // that a reopened period downloads its historical signed certificate, not live data.
            await documentOutputService.downloadDocument(
                DOCUMENT_TYPES.financeClosePack,
                period.latestClosePackCycleId,
                { format: 'pdf', copyType: 'Original' }
            );
            toast({ title: 'Close pack downloaded', description: `${period.periodName} signed Finance evidence pack is ready.` });
        } catch (error: any) {
            toast({
                title: 'Close pack download failed',
                description: error?.message || 'The signed Finance close pack could not be generated.',
                variant: 'destructive',
            });
        } finally {
            setDownloadingClosePackId(null);
        }
    };

    const handleReviewReopen = async (periodId: string, requestId: string, approved: boolean) => {
        const reviewComment = reopenReviewComment.trim();
        if (reviewComment.length < 20) {
            toast({
                title: 'Review declaration required',
                description: 'Enter at least 20 characters explaining the approval or rejection decision.',
                variant: 'destructive',
            });
            return;
        }

        try {
            setProcessing(true);
            await financeDataService.reviewFiscalPeriodReopen(periodId, requestId, approved, reviewComment);
            toast({
                title: approved ? 'Period reopened' : 'Reopen request rejected',
                description: approved
                    ? 'The old certificate was superseded and a new close cycle was started.'
                    : 'The period remains closed and the review decision has been retained.'
            });
            setReopenReviewComment('');
            await loadData();
        } catch (error: any) {
            toast({
                title: 'Review failed',
                description: error.message || 'The period reopen review could not be recorded.',
                variant: 'destructive',
            });
        } finally {
            setProcessing(false);
        }
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
                <div className="flex gap-2">
                    {canAdminister ? (
                        <Button variant="outline" size="sm" asChild>
                            <Link href="/administration/finance/close-templates">Close templates</Link>
                        </Button>
                    ) : null}
                    <Button variant="outline" size="sm" onClick={loadData} disabled={loading}>
                        <RotateCw className={`mr-2 h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
                        Refresh
                    </Button>
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
                                            const pendingReopen = period.latestReopenRequest?.status === 'PendingApproval'
                                                ? period.latestReopenRequest
                                                : undefined;

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
                                                        {(canOpen || canAdminister) && status === 'Future' && (
                                                            <Dialog>
                                                                <DialogTrigger asChild>
                                                                    <Button variant="outline" size="sm" onClick={() => setOpenReason('')}>
                                                                        <Unlock className="mr-1 h-4 w-4" />
                                                                        Open
                                                                    </Button>
                                                                </DialogTrigger>
                                                                <DialogContent>
                                                                    <DialogHeader>
                                                                        <DialogTitle>Open {period.periodName}?</DialogTitle>
                                                                        <DialogDescription>
                                                                            Earlier periods may remain open for closing adjustments, but no earlier period may still be Future.
                                                                        </DialogDescription>
                                                                    </DialogHeader>
                                                                    <div className="space-y-2 py-4">
                                                                        <Label htmlFor={`open-reason-${period.id}`}>Opening reason</Label>
                                                                        <Textarea
                                                                            id={`open-reason-${period.id}`}
                                                                            value={openReason}
                                                                            onChange={(event) => setOpenReason(event.target.value)}
                                                                            placeholder="e.g. Open for August 2026 operational postings"
                                                                            maxLength={500}
                                                                            rows={3}
                                                                        />
                                                                    </div>
                                                                    <DialogFooter>
                                                                        <DialogClose asChild>
                                                                            <Button variant="outline" disabled={processing}>Cancel</Button>
                                                                        </DialogClose>
                                                                        <Button onClick={() => handleOpenPeriod(period.id)} disabled={processing}>
                                                                            {processing ? 'Opening...' : 'Open Period'}
                                                                        </Button>
                                                                    </DialogFooter>
                                                                </DialogContent>
                                                            </Dialog>
                                                        )}
                                                        {canClose && status === 'Open' && (
                                                            <CloseWorkspaceDialog period={period} onClosed={loadData} />
                                                        )}
                                                        {pendingReopen && (
                                                            <Badge variant="outline" className="w-fit border-amber-300 bg-amber-50 text-amber-800">
                                                                Reopen approval pending
                                                            </Badge>
                                                        )}
                                                        {canExportFinanceReports && period.latestClosePackCycleId && (
                                                            <Button
                                                                variant="outline"
                                                                size="sm"
                                                                onClick={() => void downloadClosePack(period)}
                                                                disabled={downloadingClosePackId === period.latestClosePackCycleId}
                                                                title="Download the signed certificate, control results, exceptions, evidence and reopen history"
                                                            >
                                                                {downloadingClosePackId === period.latestClosePackCycleId ? (
                                                                    <Loader2 className="mr-1 h-4 w-4 animate-spin" />
                                                                ) : (
                                                                    <FileDown className="mr-1 h-4 w-4" />
                                                                )}
                                                                Close pack
                                                            </Button>
                                                        )}
                                                        {pendingReopen && canApproveReopen && status === 'Closed' && (
                                                            <Dialog>
                                                                <DialogTrigger asChild>
                                                                    <Button
                                                                        variant="outline"
                                                                        size="sm"
                                                                        onClick={() => setReopenReviewComment('')}
                                                                    >
                                                                        Review reopen
                                                                    </Button>
                                                                </DialogTrigger>
                                                                <DialogContent className="max-w-2xl">
                                                                    <DialogHeader>
                                                                        <DialogTitle>Review Reopen: {period.periodName}</DialogTitle>
                                                                        <DialogDescription>
                                                                            Close cycle {pendingReopen.closedCycleNumber} remains protected until this independent decision is approved.
                                                                        </DialogDescription>
                                                                    </DialogHeader>
                                                                    <div className="max-h-[65vh] space-y-4 overflow-y-auto py-2 pr-1 text-sm">
                                                                        <div className="rounded border p-3">
                                                                            <p className="font-semibold">Request by {pendingReopen.requestedByUserName}</p>
                                                                            <p className="mt-2 whitespace-pre-wrap"><span className="font-medium">Reason:</span> {pendingReopen.reason}</p>
                                                                            <p className="mt-2 whitespace-pre-wrap"><span className="font-medium">Impact assessment:</span> {pendingReopen.affectedPeriodAssessment}</p>
                                                                        </div>
                                                                        {pendingReopen.validationWarnings.map((warning) => (
                                                                            <div key={warning} className="rounded bg-amber-50 p-3 text-amber-900">
                                                                                <AlertTriangle className="mr-2 inline h-4 w-4" />
                                                                                {warning}
                                                                            </div>
                                                                        ))}
                                                                        {pendingReopen.affectedPeriods.length > 0 && (
                                                                            <div className="rounded border p-3">
                                                                                <p className="mb-2 font-semibold">Later affected periods</p>
                                                                                <div className="space-y-1">
                                                                                    {pendingReopen.affectedPeriods.map((affected) => (
                                                                                        <div key={affected.fiscalPeriodId} className="flex justify-between gap-4">
                                                                                            <span>{affected.periodCode} — {affected.periodName}</span>
                                                                                            <Badge variant="outline">{affected.periodStatus}</Badge>
                                                                                        </div>
                                                                                    ))}
                                                                                </div>
                                                                            </div>
                                                                        )}
                                                                        <div className="space-y-2">
                                                                            <Label htmlFor={`reopen-review-${period.id}`}>Reviewer declaration</Label>
                                                                            <Textarea
                                                                                id={`reopen-review-${period.id}`}
                                                                                value={reopenReviewComment}
                                                                                onChange={(event) => setReopenReviewComment(event.target.value)}
                                                                                placeholder="Document your independent review and decision (minimum 20 characters)."
                                                                                maxLength={2000}
                                                                                rows={4}
                                                                            />
                                                                        </div>
                                                                    </div>
                                                                    <DialogFooter>
                                                                        <DialogClose asChild>
                                                                            <Button variant="outline" disabled={processing}>Cancel</Button>
                                                                        </DialogClose>
                                                                        <Button
                                                                            variant="destructive"
                                                                            onClick={() => handleReviewReopen(period.id, pendingReopen.id, false)}
                                                                            disabled={processing}
                                                                        >
                                                                            Reject
                                                                        </Button>
                                                                        <Button
                                                                            onClick={() => handleReviewReopen(period.id, pendingReopen.id, true)}
                                                                            disabled={processing}
                                                                        >
                                                                            {processing ? 'Applying decision...' : 'Approve and Reopen'}
                                                                        </Button>
                                                                    </DialogFooter>
                                                                </DialogContent>
                                                            </Dialog>
                                                        )}
                                                        {pendingReopen && !canApproveReopen && (
                                                            <Badge variant="secondary">Higher-tier review pending</Badge>
                                                        )}
                                                        {canReopen && status === 'Closed' && !pendingReopen && (
                                                            <Dialog>
                                                                <DialogTrigger asChild>
                                                                    <Button
                                                                        variant="outline"
                                                                        size="sm"
                                                                        onClick={() => {
                                                                            setReopenReason('');
                                                                            setReopenImpactAssessment('');
                                                                        }}
                                                                    >
                                                                        <Unlock className="h-4 w-4 mr-1" />
                                                                        Request Reopen
                                                                    </Button>
                                                                </DialogTrigger>
                                                                <DialogContent>
                                                                    <DialogHeader>
                                                                        <DialogTitle>Request Reopen: {period.periodName}</DialogTitle>
                                                                        <DialogDescription>
                                                                            The period remains closed until an independent higher-tier reviewer approves the request.
                                                                        </DialogDescription>
                                                                    </DialogHeader>
                                                                    <div className="space-y-4 py-4">
                                                                        <div className="bg-yellow-50 p-3 rounded text-sm">
                                                                            <p className="font-semibold mb-1 flex items-center gap-2">
                                                                                <AlertTriangle className="h-4 w-4" />
                                                                                Controlled sequence
                                                                            </p>
                                                                            <p>Later closed or locked periods block the request. Approval supersedes this signed close cycle and starts a new certification cycle.</p>
                                                                        </div>
                                                                        <div className="space-y-2">
                                                                            <Label htmlFor={`reopen-reason-${period.id}`}>Reason</Label>
                                                                            <Textarea
                                                                                id={`reopen-reason-${period.id}`}
                                                                                value={reopenReason}
                                                                                onChange={(event) => setReopenReason(event.target.value)}
                                                                                placeholder="Describe the accounting correction requiring reopening (minimum 20 characters)."
                                                                                maxLength={1000}
                                                                                rows={3}
                                                                            />
                                                                        </div>
                                                                        <div className="space-y-2">
                                                                            <Label htmlFor={`reopen-impact-${period.id}`}>Affected-period assessment</Label>
                                                                            <Textarea
                                                                                id={`reopen-impact-${period.id}`}
                                                                                value={reopenImpactAssessment}
                                                                                onChange={(event) => setReopenImpactAssessment(event.target.value)}
                                                                                placeholder="Explain the expected adjustment and impact on later reporting periods (minimum 20 characters)."
                                                                                maxLength={2000}
                                                                                rows={4}
                                                                            />
                                                                        </div>
                                                                    </div>
                                                                    <DialogFooter>
                                                                        <DialogClose asChild>
                                                                            <Button variant="outline" onClick={() => {
                                                                                setReopenReason('');
                                                                                setReopenImpactAssessment('');
                                                                            }}>
                                                                                Cancel
                                                                            </Button>
                                                                        </DialogClose>
                                                                        <Button onClick={() => handleRequestReopen(period.id)} disabled={processing}>
                                                                            {processing ? 'Submitting...' : 'Submit Reopen Request'}
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
