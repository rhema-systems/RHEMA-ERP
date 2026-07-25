'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Calendar, Plus, ChevronDown, ChevronRight, Lock, RefreshCw, Trash2 } from 'lucide-react';
import { toast } from '@/components/ui/use-toast';
import { financeDataService } from '@/services/finance/finance-data.service';
import { Account, FiscalYear, FiscalPeriod, PeriodType, CreateFiscalYearDto } from '@/types/finance';
import { Textarea } from '@/components/ui/textarea';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useAuth } from '@/hooks/use-auth';
import { usePathname } from 'next/navigation';

export default function FiscalYearsPage() {
    const pathname = usePathname() ?? '';
    const administrationMode = pathname.startsWith('/administration/finance');
    const { hasPermission } = useAuth();
    const canAdminister = hasPermission('Finance.Admin');
    const canClose = hasPermission('Finance.PeriodClose');
    const canReopen = hasPermission('Finance.PeriodReopen');
    const [fiscalYears, setFiscalYears] = useState<FiscalYear[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [expandedYears, setExpandedYears] = useState<Set<string>>(new Set());
    const [periodsByYear, setPeriodsByYear] = useState<Record<string, FiscalPeriod[]>>({});
    const [loadingPeriods, setLoadingPeriods] = useState<Record<string, boolean>>({});
    const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
    const [formData, setFormData] = useState({
        year: new Date().getFullYear(),
        startDate: '',
        endDate: '',
        periodType: 'Monthly' as keyof typeof PeriodType,
    });
    const [equityAccounts, setEquityAccounts] = useState<Account[]>([]);
    // Only one close/reopen dialog is open at a time, so shared selection state is safe.
    const [retainedEarningsAccountId, setRetainedEarningsAccountId] = useState<string>('default');
    const [closingNotes, setClosingNotes] = useState('');
    const [reopenReason, setReopenReason] = useState('');
    const [isClosingYear, setIsClosingYear] = useState(false);
    const [isReopeningYear, setIsReopeningYear] = useState(false);

    useEffect(() => {
        loadData();
        if (!administrationMode && canClose) {
            loadEquityAccounts();
        }
    }, [administrationMode, canClose]);

    const loadEquityAccounts = async () => {
        try {
            const accounts = await financeDataService.getAccounts({ accountType: 'Equity' });
            setEquityAccounts(accounts);
        } catch (error: any) {
            // The close dialog falls back to the Finance Settings default account.
            console.error('Failed to load equity accounts:', error?.message || error);
        }
    };

    const loadData = async () => {
        try {
            setIsLoading(true);
            const data = await financeDataService.getFiscalYears();
            setFiscalYears(data);

            // Auto expand the most recent open year
            const openYear = data.find(y => !y.isClosed);
            if (openYear) {
                toggleYear(openYear.id);
            }
        } catch (error: any) {
            console.error('Failed to load fiscal years:', error?.message || error);
            toast({
                title: 'Error',
                description: 'Failed to load fiscal years.',
                variant: 'destructive',
            });
        } finally {
            setIsLoading(false);
        }
    };

    const toggleYear = async (yearId: string) => {
        const newExpanded = new Set(expandedYears);
        if (newExpanded.has(yearId)) {
            newExpanded.delete(yearId);
            setExpandedYears(newExpanded);
        } else {
            newExpanded.add(yearId);
            setExpandedYears(newExpanded);

            // Load periods if not loaded
            if (!periodsByYear[yearId]) {
                await loadPeriods(yearId);
            }
        }
    };

    const loadPeriods = async (yearId: string) => {
        try {
            setLoadingPeriods(prev => ({ ...prev, [yearId]: true }));
            const periods = await financeDataService.getFiscalPeriods(yearId);
            setPeriodsByYear(prev => ({ ...prev, [yearId]: periods }));
        } catch (error: any) {
            console.error('Failed to load periods:', error?.message || error);
            toast({
                title: 'Error',
                description: 'Failed to load periods for the year.',
                variant: 'destructive',
            });
        } finally {
            setLoadingPeriods(prev => ({ ...prev, [yearId]: false }));
        }
    };

    const handleCreate = async () => {
        if (!formData.startDate || !formData.endDate || !formData.year) {
            toast({
                title: 'Validation Error',
                description: 'Please fill in all required fields.',
                variant: 'destructive',
            });
            return;
        }

        try {
            // Calculate number of periods based on type
            let numberOfPeriods = 12;
            const start = new Date(formData.startDate);
            const end = new Date(formData.endDate);
            const diffTime = Math.abs(end.getTime() - start.getTime());
            const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24)) + 1;

            switch (formData.periodType) {
                case 'Monthly': numberOfPeriods = 12; break; // Simplified, could calculate months diff
                case 'Quarterly': numberOfPeriods = 4; break;
                case 'Weekly': numberOfPeriods = Math.ceil(diffDays / 7); break;
                case 'Daily': numberOfPeriods = diffDays; break;
            }

            const dto: CreateFiscalYearDto = {
                fiscalYearName: `Fiscal Year ${formData.year}`,
                fiscalYearCode: `FY${formData.year}`,
                year: formData.year,
                startDate: new Date(formData.startDate).toISOString(),
                endDate: new Date(formData.endDate).toISOString(),
                fiscalYearType: 'Calendar',
                numberOfPeriods: numberOfPeriods,
                baseCurrency: 'GHS',
                periodType: PeriodType[formData.periodType],
            };

            await financeDataService.createFiscalYear(dto);

            toast({
                title: 'Success',
                description: 'Fiscal year created successfully.',
            });

            setIsCreateDialogOpen(false);
            resetForm();
            loadData();
        } catch (error: any) {
            console.error('Failed to create fiscal year:', error?.message || error);
            toast({
                title: 'Error',
                description: error?.message || 'Failed to create fiscal year. Ensure dates do not overlap.',
                variant: 'destructive',
            });
        }
    };

    const handleCloseYear = async (yearId: string) => {
        setIsClosingYear(true);
        try {
            const result = await financeDataService.closeFiscalYear(yearId, {
                retainedEarningsAccountId: retainedEarningsAccountId === 'default' ? undefined : retainedEarningsAccountId,
                closingNotes: closingNotes.trim() || undefined,
            });
            toast({
                title: 'Success',
                description: result.message || 'Fiscal year closed successfully.',
            });
            setClosingNotes('');
            setRetainedEarningsAccountId('default');
            loadData();
        } catch (error: any) {
            console.error('Failed to close fiscal year:', error?.message || error);
            // The backend returns per-period errors (e.g. open periods) in `errors`.
            const detailErrors: string[] = error?.response?.data?.errors ?? error?.errors ?? [];
            toast({
                title: 'Error',
                description: detailErrors.length > 0
                    ? detailErrors.join(' ')
                    : (error?.message || 'Failed to close fiscal year.'),
                variant: 'destructive',
            });
        } finally {
            setIsClosingYear(false);
        }
    };

    const handleReopenYear = async (yearId: string) => {
        if (!reopenReason.trim()) {
            toast({
                title: 'Validation Error',
                description: 'A reason is required to reopen a fiscal year.',
                variant: 'destructive',
            });
            return;
        }

        setIsReopeningYear(true);
        try {
            const result = await financeDataService.reopenFiscalYear(yearId, reopenReason.trim());
            toast({
                title: 'Success',
                description: result.message || 'Fiscal year reopened. The closing entry was reversed.',
            });
            setReopenReason('');
            loadData();
        } catch (error: any) {
            console.error('Failed to reopen fiscal year:', error?.message || error);
            toast({
                title: 'Error',
                description: error?.message || 'Failed to reopen fiscal year.',
                variant: 'destructive',
            });
        } finally {
            setIsReopeningYear(false);
        }
    };

    const handleDeleteYear = async (yearId: string) => {
        try {
            await financeDataService.deleteFiscalYear(yearId);
            toast({
                title: 'Success',
                description: 'Fiscal year deleted successfully.',
            });
            loadData();
        } catch (error: any) {
            console.error('Failed to delete fiscal year:', error?.message || error);
            const errorMessage = error?.message || 'Failed to delete fiscal year. Ensure it is not closed and has no associated finance records.';
            toast({
                title: 'Error',
                description: errorMessage,
                variant: 'destructive',
            });
        }
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

    const formatDate = (dateString?: string) => {
        if (!dateString) return '-';
        return new Date(dateString).toLocaleDateString('en-US', {
            year: 'numeric',
            month: 'short',
            day: 'numeric',
        });
    };

    const getStatusBadge = (status: string) => {
        const variants: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
            Open: 'default',
            Closed: 'secondary',
            Locked: 'destructive',
            Future: 'outline',
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
                        {administrationMode ? 'Fiscal Calendar Setup' : 'Fiscal Years'}
                    </h1>
                    <p className="text-muted-foreground">
                        {administrationMode
                            ? 'Create fiscal years and generate their accounting periods'
                            : 'Review fiscal years and perform controlled year-end close actions'}
                    </p>
                </div>
                <div className="flex gap-2">
                    <Button variant="outline" size="icon" onClick={loadData} disabled={isLoading}>
                        <RefreshCw className={`h-4 w-4 ${isLoading ? 'animate-spin' : ''}`} />
                    </Button>
                    {administrationMode && canAdminister && (
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
                                            <SelectItem value="Weekly">Weekly (52-53 periods)</SelectItem>
                                            <SelectItem value="Daily">Daily (365-366 periods)</SelectItem>
                                        </SelectContent>
                                    </Select>
                                </div>
                                <div className="text-sm text-muted-foreground bg-blue-50 p-3 rounded">
                                    <p className="font-semibold mb-1">Auto-generation:</p>
                                    <p>
                                        Periods will be automatically created for this fiscal year based on the selected type ({formData.periodType}).
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
                    )}
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
                        <BreadcrumbLink href={administrationMode ? '/administration' : '/finance'}>
                            {administrationMode ? 'Administration' : 'Finance'}
                        </BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>{administrationMode ? 'Fiscal Calendar Setup' : 'Fiscal Years'}</BreadcrumbPage>
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
                    {fiscalYears.length === 0 ? (
                        <div className="text-center py-8 text-muted-foreground">
                            No fiscal years found. Create one to get started.
                        </div>
                    ) : (
                        <div className="space-y-4">
                            {fiscalYears.map((year) => (
                                <div key={year.id} className="border rounded-lg overflow-hidden">
                                    {/* Year Header */}
                                    <div className="flex items-center justify-between p-4 bg-muted/50 hover:bg-muted/70 transition-colors">
                                        <div className="flex items-center gap-4 cursor-pointer" onClick={() => toggleYear(year.id)}>
                                            <Button
                                                variant="ghost"
                                                size="sm"
                                                className="p-0 h-auto hover:bg-transparent"
                                            >
                                                {expandedYears.has(year.id) ? (
                                                    <ChevronDown className="h-4 w-4" />
                                                ) : (
                                                    <ChevronRight className="h-4 w-4" />
                                                )}
                                            </Button>
                                            <div>
                                                <div className="flex items-center gap-2">
                                                    <h3 className="font-semibold text-lg">{year.fiscalYearName} ({year.fiscalYearCode})</h3>
                                                    {year.isClosed && (
                                                        <Badge variant="secondary">
                                                            <Lock className="h-3 w-3 mr-1" />
                                                            Closed
                                                        </Badge>
                                                    )}
                                                </div>
                                                <p className="text-sm text-muted-foreground">
                                                    {formatDate(year.startDate)} - {formatDate(year.endDate)}
                                                    <span className="mx-2">•</span>
                                                    {year.numberOfPeriods} Periods
                                                </p>
                                            </div>
                                        </div>
                                        <div className="flex items-center gap-2">
                                            {!administrationMode && canClose && !year.isClosed && (
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
                                                                Revenue and expense balances are transferred to retained earnings and the year is marked closed. This action can be reversed.
                                                            </DialogDescription>
                                                        </DialogHeader>
                                                        <div className="space-y-4 py-4">
                                                            <div>
                                                                <p className="text-sm text-muted-foreground">
                                                                    Before closing the year, ensure:
                                                                </p>
                                                                <ul className="list-disc list-inside space-y-1 mt-2 text-sm">
                                                                    <li>All journal entries are posted</li>
                                                                    <li>All periods are closed</li>
                                                                    <li>Year-end adjustments are complete</li>
                                                                </ul>
                                                            </div>
                                                            <div className="space-y-2">
                                                                <Label htmlFor={`re-account-${year.id}`}>Retained Earnings Account</Label>
                                                                <Select
                                                                    value={retainedEarningsAccountId}
                                                                    onValueChange={setRetainedEarningsAccountId}
                                                                >
                                                                    <SelectTrigger id={`re-account-${year.id}`}>
                                                                        <SelectValue placeholder="Use Finance Settings default" />
                                                                    </SelectTrigger>
                                                                    <SelectContent>
                                                                        <SelectItem value="default">Use Finance Settings default</SelectItem>
                                                                        {equityAccounts.map((account) => (
                                                                            <SelectItem key={account.id} value={account.id}>
                                                                                {account.accountCode} - {account.accountName}
                                                                            </SelectItem>
                                                                        ))}
                                                                    </SelectContent>
                                                                </Select>
                                                                <p className="text-xs text-muted-foreground">
                                                                    Defaults to the retained earnings account configured in Finance Settings.
                                                                </p>
                                                            </div>
                                                            <div className="space-y-2">
                                                                <Label htmlFor={`closing-notes-${year.id}`}>Closing Notes (optional)</Label>
                                                                <Textarea
                                                                    id={`closing-notes-${year.id}`}
                                                                    value={closingNotes}
                                                                    onChange={(e) => setClosingNotes(e.target.value)}
                                                                    placeholder="Year-end adjustments summary, sign-off references..."
                                                                    rows={3}
                                                                />
                                                            </div>
                                                        </div>
                                                        <DialogFooter>
                                                            <Button variant="outline">Cancel</Button>
                                                            <Button onClick={() => handleCloseYear(year.id)} disabled={isClosingYear}>
                                                                {isClosingYear ? 'Closing...' : 'Close Fiscal Year'}
                                                            </Button>
                                                        </DialogFooter>
                                                    </DialogContent>
                                                </Dialog>
                                            )}
                                            {!administrationMode && canReopen && year.isClosed && (
                                                <Dialog>
                                                    <DialogTrigger asChild>
                                                        <Button variant="outline" size="sm">
                                                            Reopen Year
                                                        </Button>
                                                    </DialogTrigger>
                                                    <DialogContent>
                                                        <DialogHeader>
                                                            <DialogTitle>Reopen Fiscal Year {year.year}?</DialogTitle>
                                                            <DialogDescription>
                                                                The year-end closing entry is reversed through the posting engine and the year is marked open again. A reason is required for the audit trail.
                                                            </DialogDescription>
                                                        </DialogHeader>
                                                        <div className="space-y-2 py-4">
                                                            <Label htmlFor={`reopen-reason-${year.id}`}>Reason</Label>
                                                            <Textarea
                                                                id={`reopen-reason-${year.id}`}
                                                                value={reopenReason}
                                                                onChange={(e) => setReopenReason(e.target.value)}
                                                                placeholder="Why is this fiscal year being reopened?"
                                                                rows={3}
                                                            />
                                                        </div>
                                                        <DialogFooter>
                                                            <Button variant="outline">Cancel</Button>
                                                            <Button onClick={() => handleReopenYear(year.id)} disabled={isReopeningYear}>
                                                                {isReopeningYear ? 'Reopening...' : 'Reopen Fiscal Year'}
                                                            </Button>
                                                        </DialogFooter>
                                                    </DialogContent>
                                                </Dialog>
                                            )}
                                            {administrationMode && canAdminister && (
                                            <Dialog>
                                                <DialogTrigger asChild>
                                                    <Button variant="ghost" size="icon" className="text-destructive hover:text-destructive hover:bg-destructive/10">
                                                        <Trash2 className="h-4 w-4" />
                                                    </Button>
                                                </DialogTrigger>
                                                <DialogContent>
                                                    <DialogHeader>
                                                        <DialogTitle>Delete Fiscal Year {year.year}?</DialogTitle>
                                                        <DialogDescription>
                                                            This action cannot be undone. You can only delete fiscal years that have no journal entries, ledger transactions, budgets, opening balances, or other dependent finance records.
                                                        </DialogDescription>
                                                    </DialogHeader>
                                                    <DialogFooter>
                                                        <Button variant="outline">Cancel</Button>
                                                        <Button variant="destructive" onClick={() => handleDeleteYear(year.id)}>
                                                            Delete Fiscal Year
                                                        </Button>
                                                    </DialogFooter>
                                                </DialogContent>
                                            </Dialog>
                                            )}
                                        </div>
                                    </div>

                                    {/* Periods Table */}
                                    {expandedYears.has(year.id) && (
                                        <div className="border-t">
                                            {loadingPeriods[year.id] ? (
                                                <div className="p-8 text-center text-muted-foreground">
                                                    <RefreshCw className="h-4 w-4 animate-spin mx-auto mb-2" />
                                                    Loading periods...
                                                </div>
                                            ) : periodsByYear[year.id] && periodsByYear[year.id].length > 0 ? (
                                                <Table>
                                                    <TableHeader>
                                                        <TableRow>
                                                            <TableHead>Period</TableHead>
                                                            <TableHead>Dates</TableHead>
                                                            <TableHead>Status</TableHead>
                                                            <TableHead>Locked</TableHead>
                                                            {/* <TableHead className="text-right">Actions</TableHead> */}
                                                        </TableRow>
                                                    </TableHeader>
                                                    <TableBody>
                                                        {periodsByYear[year.id].map((period) => (
                                                            <TableRow key={period.id}>
                                                                <TableCell className="font-medium">
                                                                    {period.periodName} ({period.periodCode})
                                                                </TableCell>
                                                                <TableCell>
                                                                    {formatDate(period.startDate)} - {formatDate(period.endDate)}
                                                                </TableCell>
                                                                <TableCell>
                                                                    {getStatusBadge(period.periodStatus || (period.isOpen ? 'Open' : 'Future'))}
                                                                </TableCell>
                                                                <TableCell>
                                                                    {period.isLocked ? <Lock className="h-4 w-4 text-muted-foreground" /> : '-'}
                                                                </TableCell>
                                                                {/* <TableCell className="text-right">
                                                                    <Button variant="ghost" size="sm">Manage</Button>
                                                                </TableCell> */}
                                                            </TableRow>
                                                        ))}
                                                    </TableBody>
                                                </Table>
                                            ) : (
                                                <div className="p-8 text-center text-muted-foreground">
                                                    No periods found for this fiscal year.
                                                </div>
                                            )}
                                        </div>
                                    )}
                                </div>
                            ))}
                        </div>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}
