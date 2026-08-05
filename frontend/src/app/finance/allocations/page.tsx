'use client';

import React, { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
    ArrowRightLeft,
    Calculator,
    ChevronRight,
    Equal,
    FileText,
    Home,
    MoreHorizontal,
    Percent,
    Play,
    Plus,
    Search,
    Settings,
} from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
} from '@/components/ui/card';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from '@/components/ui/dialog';
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuSeparator,
    DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from '@/components/ui/table';
import { financeDataService } from '@/services/finance/finance-data.service';
import { unitAccountsDataService } from '@/services/finance/unit-accounts-data.service';
import type { FiscalPeriod } from '@/types/finance';
import type { AllocationRule, AllocationRunBatch, AllocationRunBatchStatus, AllocationType } from '@/types/unit-accounts';

function inputDate(value?: string) {
    if (!value) return '';
    return value.includes('T') ? value.split('T')[0] : value.slice(0, 10);
}

function isOpenPeriod(period: FiscalPeriod) {
    const status = period.status ?? period.periodStatus;
    return (period.isOpen ?? status === 'Open') && !period.isClosed && !period.isLocked;
}

function isDateInPeriod(date: string, period?: FiscalPeriod) {
    if (!date || !period) return false;
    return date >= inputDate(period.startDate) && date <= inputDate(period.endDate);
}

function defaultDateForPeriod(period: FiscalPeriod) {
    return inputDate(period.endDate);
}

function getAllocationTypeIcon(type: AllocationType) {
    switch (type) {
        case 'UnitAccountBased':
            return <Calculator className="h-4 w-4" />;
        case 'FixedPercentage':
            return <Percent className="h-4 w-4" />;
        case 'EqualDistribution':
            return <Equal className="h-4 w-4" />;
    }
}

function getAllocationTypeBadge(type: AllocationType) {
    switch (type) {
        case 'UnitAccountBased':
            return <Badge variant="outline" className="bg-blue-50">Unit Based</Badge>;
        case 'FixedPercentage':
            return <Badge variant="outline" className="bg-purple-50">Fixed %</Badge>;
        case 'EqualDistribution':
            return <Badge variant="outline" className="bg-green-50">Equal Split</Badge>;
    }
}

function getRunBatchStatusBadge(status: AllocationRunBatchStatus) {
    switch (status) {
        case 'Draft':
            return <Badge variant="secondary">Draft</Badge>;
        case 'PendingApproval':
            return <Badge className="bg-amber-100 text-amber-800">Pending Approval</Badge>;
        case 'Approved':
            return <Badge className="bg-blue-100 text-blue-800">Approved</Badge>;
        case 'Posted':
            return <Badge className="bg-green-100 text-green-800">Posted</Badge>;
        case 'Rejected':
            return <Badge variant="destructive">Rejected</Badge>;
        case 'Cancelled':
            return <Badge variant="outline">Cancelled</Badge>;
    }
}

export default function AllocationsPage() {
    const router = useRouter();
    const [rules, setRules] = useState<AllocationRule[]>([]);
    const [runBatches, setRunBatches] = useState<AllocationRunBatch[]>([]);
    const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
    const [searchQuery, setSearchQuery] = useState('');
    const [filterActive, setFilterActive] = useState<'all' | 'active' | 'inactive'>('all');
    const [isLoading, setIsLoading] = useState(true);
    const [runRule, setRunRule] = useState<AllocationRule | null>(null);
    const [runForm, setRunForm] = useState({
        fiscalPeriodId: '',
        allocationDate: '',
        description: '',
    });
    const [isRunning, setIsRunning] = useState(false);

    const loadData = async () => {
        try {
            setIsLoading(true);
            const [allocationRules, fiscalPeriods, allocationRunBatches] = await Promise.all([
                unitAccountsDataService.getAllocationRules(),
                financeDataService.getFiscalPeriods(),
                unitAccountsDataService.getAllocationRunBatches(),
            ]);
            setRules(allocationRules);
            setRunBatches(allocationRunBatches);
            setPeriods(fiscalPeriods.filter(isOpenPeriod));
        } catch (error: any) {
            toast.error(error?.message || 'Failed to load allocation rules.');
        } finally {
            setIsLoading(false);
        }
    };

    useEffect(() => {
        loadData();
    }, []);

    const filteredRules = useMemo(() => {
        let result = [...rules];

        if (searchQuery) {
            const query = searchQuery.toLowerCase();
            result = result.filter(
                (rule) =>
                    rule.code.toLowerCase().includes(query) ||
                    rule.name.toLowerCase().includes(query) ||
                    rule.sourceAccountNumber?.toLowerCase().includes(query) ||
                    rule.sourceAccountName?.toLowerCase().includes(query)
            );
        }

        if (filterActive === 'active') {
            result = result.filter((rule) => rule.isActive);
        } else if (filterActive === 'inactive') {
            result = result.filter((rule) => !rule.isActive);
        }

        return result;
    }, [rules, searchQuery, filterActive]);

    const selectedRunPeriod = periods.find((period) => period.id === runForm.fiscalPeriodId);

    const openRunDialog = (rule: AllocationRule) => {
        const today = new Date().toISOString().split('T')[0];
        const period = periods.find((candidate) => isDateInPeriod(today, candidate)) ?? periods[0];
        setRunRule(rule);
        setRunForm({
            fiscalPeriodId: period?.id ?? '',
            allocationDate: period ? defaultDateForPeriod(period) : today,
            description: `Allocation run for ${rule.code}`,
        });
    };

    const handleRunPeriodChange = (periodId: string) => {
        const period = periods.find((candidate) => candidate.id === periodId);
        setRunForm((current) => ({
            ...current,
            fiscalPeriodId: periodId,
            allocationDate: period && !isDateInPeriod(current.allocationDate, period)
                ? defaultDateForPeriod(period)
                : current.allocationDate,
        }));
    };

    const handleCreateRunBatch = async () => {
        if (!runRule) return;

        if (!runForm.fiscalPeriodId) {
            toast.error('Select an open fiscal period.');
            return;
        }

        if (!isDateInPeriod(runForm.allocationDate, selectedRunPeriod)) {
            toast.error('Allocation date must fall within the selected fiscal period.');
            return;
        }

        try {
            setIsRunning(true);
            const batch = await unitAccountsDataService.createAllocationRunBatch(runRule.id, {
                fiscalPeriodId: runForm.fiscalPeriodId,
                allocationDate: runForm.allocationDate,
                description: runForm.description.trim() || undefined,
            });
            toast.success(`Allocation run batch ${batch.batchNumber} created for review.`);
            setRunRule(null);
            await loadData();
            router.push(`/finance/allocations/runs/${batch.id}`);
        } catch (error: any) {
            toast.error(error?.message || 'Failed to create allocation run batch.');
        } finally {
            setIsRunning(false);
        }
    };

    return (
        <div className="space-y-6">
            <nav className="flex items-center space-x-2 text-sm text-muted-foreground">
                <Link href="/" className="flex items-center hover:text-foreground">
                    <Home className="h-4 w-4" />
                </Link>
                <ChevronRight className="h-4 w-4" />
                <Link href="/finance" className="hover:text-foreground">Finance</Link>
                <ChevronRight className="h-4 w-4" />
                <span className="text-foreground">Allocation Rules</span>
            </nav>

            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Allocation Rules</h1>
                    <p className="text-muted-foreground">
                        Define rules to distribute financial amounts using unit account drivers
                    </p>
                </div>
                <Button asChild>
                    <Link href="/finance/allocations/new">
                        <Plus className="mr-2 h-4 w-4" />
                        New Allocation Rule
                    </Link>
                </Button>
            </div>

            <div className="grid gap-4 md:grid-cols-3">
                <Card>
                    <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                        <CardTitle className="text-sm font-medium">Total Rules</CardTitle>
                        <ArrowRightLeft className="h-4 w-4 text-muted-foreground" />
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{rules.length}</div>
                        <p className="text-xs text-muted-foreground">Allocation rules defined</p>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                        <CardTitle className="text-sm font-medium">Active Rules</CardTitle>
                        <Play className="h-4 w-4 text-green-500" />
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold text-green-600">
                            {rules.filter((rule) => rule.isActive).length}
                        </div>
                        <p className="text-xs text-muted-foreground">Ready to run</p>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                        <CardTitle className="text-sm font-medium">Unit-Based</CardTitle>
                        <Calculator className="h-4 w-4 text-blue-500" />
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold text-blue-600">
                            {rules.filter((rule) => rule.allocationType === 'UnitAccountBased').length}
                        </div>
                        <p className="text-xs text-muted-foreground">Using unit account drivers</p>
                    </CardContent>
                </Card>
            </div>

            <Card>
                <CardHeader>
                    <CardTitle>Allocation Run Batches</CardTitle>
                    <CardDescription>
                        Review, approve, and post calculated allocation runs
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    <div className="rounded-md border">
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHead>Batch</TableHead>
                                    <TableHead>Rule</TableHead>
                                    <TableHead>Period</TableHead>
                                    <TableHead>Total Allocated</TableHead>
                                    <TableHead>Status</TableHead>
                                    <TableHead>Journal</TableHead>
                                    <TableHead className="w-[100px]">Actions</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {isLoading && (
                                    <TableRow>
                                        <TableCell colSpan={7} className="text-center py-8 text-muted-foreground">
                                            Loading allocation run batches...
                                        </TableCell>
                                    </TableRow>
                                )}
                                {!isLoading && runBatches.slice(0, 10).map((batch) => (
                                    <TableRow key={batch.id}>
                                        <TableCell>
                                            <div className="font-medium">{batch.batchNumber}</div>
                                            <div className="text-sm text-muted-foreground">
                                                {new Date(batch.allocationDate).toLocaleDateString()}
                                            </div>
                                        </TableCell>
                                        <TableCell>
                                            <div className="font-mono text-sm">{batch.ruleCode}</div>
                                            <div className="text-sm text-muted-foreground">{batch.ruleName}</div>
                                        </TableCell>
                                        <TableCell>
                                            <div className="font-mono text-sm">{batch.periodCode}</div>
                                            <div className="text-sm text-muted-foreground">{batch.periodName}</div>
                                        </TableCell>
                                        <TableCell>
                                            {batch.functionalCurrencyCode} {batch.totalAllocated.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                                        </TableCell>
                                        <TableCell>{getRunBatchStatusBadge(batch.status)}</TableCell>
                                        <TableCell>
                                            {batch.journalEntryNumber ? (
                                                <span className="font-mono text-sm">{batch.journalEntryNumber}</span>
                                            ) : (
                                                <span className="text-muted-foreground">-</span>
                                            )}
                                        </TableCell>
                                        <TableCell>
                                            <Button
                                                variant="ghost"
                                                size="icon"
                                                onClick={() => router.push(`/finance/allocations/runs/${batch.id}`)}
                                            >
                                                <FileText className="h-4 w-4" />
                                            </Button>
                                        </TableCell>
                                    </TableRow>
                                ))}
                                {!isLoading && runBatches.length === 0 && (
                                    <TableRow>
                                        <TableCell colSpan={7} className="text-center py-8 text-muted-foreground">
                                            No allocation run batches created yet.
                                        </TableCell>
                                    </TableRow>
                                )}
                            </TableBody>
                        </Table>
                    </div>
                </CardContent>
            </Card>

            <Card>
                <CardHeader>
                    <CardTitle>Allocation Rules</CardTitle>
                    <CardDescription>
                        Rules for distributing financial amounts based on unit quantities or percentages
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    <div className="flex flex-wrap items-center gap-4 mb-4">
                        <div className="relative flex-1 min-w-[200px]">
                            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                            <Input
                                placeholder="Search rules..."
                                value={searchQuery}
                                onChange={(e) => setSearchQuery(e.target.value)}
                                className="pl-9"
                            />
                        </div>
                        <div className="flex gap-2">
                            <Button
                                variant={filterActive === 'all' ? 'default' : 'outline'}
                                size="sm"
                                onClick={() => setFilterActive('all')}
                            >
                                All
                            </Button>
                            <Button
                                variant={filterActive === 'active' ? 'default' : 'outline'}
                                size="sm"
                                onClick={() => setFilterActive('active')}
                            >
                                Active
                            </Button>
                            <Button
                                variant={filterActive === 'inactive' ? 'default' : 'outline'}
                                size="sm"
                                onClick={() => setFilterActive('inactive')}
                            >
                                Inactive
                            </Button>
                        </div>
                    </div>

                    <div className="rounded-md border">
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHead>Rule</TableHead>
                                    <TableHead>Source Account</TableHead>
                                    <TableHead>Type</TableHead>
                                    <TableHead>Driver</TableHead>
                                    <TableHead>Targets</TableHead>
                                    <TableHead>Last Run</TableHead>
                                    <TableHead>Status</TableHead>
                                    <TableHead className="w-[100px]">Actions</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {isLoading && (
                                    <TableRow>
                                        <TableCell colSpan={8} className="text-center py-8 text-muted-foreground">
                                            Loading allocation rules...
                                        </TableCell>
                                    </TableRow>
                                )}
                                {!isLoading && filteredRules.map((rule) => (
                                    <TableRow key={rule.id}>
                                        <TableCell>
                                            <div>
                                                <div className="font-medium">{rule.code}</div>
                                                <div className="text-sm text-muted-foreground">{rule.name}</div>
                                            </div>
                                        </TableCell>
                                        <TableCell>
                                            <div>
                                                <div className="font-mono text-sm">{rule.sourceAccountNumber}</div>
                                                <div className="text-sm text-muted-foreground">{rule.sourceAccountName}</div>
                                            </div>
                                        </TableCell>
                                        <TableCell>
                                            <div className="flex items-center gap-2">
                                                {getAllocationTypeIcon(rule.allocationType)}
                                                {getAllocationTypeBadge(rule.allocationType)}
                                            </div>
                                        </TableCell>
                                        <TableCell>
                                            {rule.driverUnitAccountNumber ? (
                                                <div>
                                                    <div className="font-mono text-sm">{rule.driverUnitAccountNumber}</div>
                                                    <div className="text-sm text-muted-foreground">{rule.driverUnitAccountName}</div>
                                                </div>
                                            ) : (
                                                <span className="text-muted-foreground">-</span>
                                            )}
                                        </TableCell>
                                        <TableCell>
                                            <Badge variant="secondary">{rule.targets?.length ?? 0} targets</Badge>
                                        </TableCell>
                                        <TableCell>
                                            {rule.lastRunDate ? (
                                                <span className="text-sm">{new Date(rule.lastRunDate).toLocaleDateString()}</span>
                                            ) : (
                                                <span className="text-muted-foreground">Never</span>
                                            )}
                                        </TableCell>
                                        <TableCell>
                                            {rule.isActive ? (
                                                <Badge className="bg-green-100 text-green-800">Active</Badge>
                                            ) : (
                                                <Badge variant="secondary">Inactive</Badge>
                                            )}
                                        </TableCell>
                                        <TableCell>
                                            <DropdownMenu>
                                                <DropdownMenuTrigger asChild>
                                                    <Button variant="ghost" size="icon">
                                                        <MoreHorizontal className="h-4 w-4" />
                                                    </Button>
                                                </DropdownMenuTrigger>
                                                <DropdownMenuContent align="end">
                                                    <DropdownMenuItem onClick={() => router.push(`/finance/allocations/${rule.id}`)}>
                                                        <Settings className="mr-2 h-4 w-4" />
                                                        Edit Rule
                                                    </DropdownMenuItem>
                                                    <DropdownMenuItem
                                                        onClick={() => openRunDialog(rule)}
                                                        disabled={!rule.isActive || periods.length === 0}
                                                    >
                                                        <Play className="mr-2 h-4 w-4" />
                                                        Run Allocation
                                                    </DropdownMenuItem>
                                                    <DropdownMenuSeparator />
                                                    <DropdownMenuItem disabled>
                                                        View History
                                                    </DropdownMenuItem>
                                                </DropdownMenuContent>
                                            </DropdownMenu>
                                        </TableCell>
                                    </TableRow>
                                ))}
                                {!isLoading && filteredRules.length === 0 && (
                                    <TableRow>
                                        <TableCell colSpan={8} className="text-center py-8 text-muted-foreground">
                                            No allocation rules found.
                                        </TableCell>
                                    </TableRow>
                                )}
                            </TableBody>
                        </Table>
                    </div>
                </CardContent>
            </Card>

            <Dialog open={!!runRule} onOpenChange={(open) => !open && setRunRule(null)}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>Create Allocation Run Batch</DialogTitle>
                        <DialogDescription>
                            {runRule ? `${runRule.code} - ${runRule.name}` : 'Select an allocation rule to calculate.'}
                        </DialogDescription>
                    </DialogHeader>
                    <div className="space-y-4">
                        <div className="space-y-2">
                            <Label htmlFor="allocationPeriod">Fiscal Period</Label>
                            <Select value={runForm.fiscalPeriodId} onValueChange={handleRunPeriodChange}>
                                <SelectTrigger id="allocationPeriod">
                                    <SelectValue placeholder="Select open period" />
                                </SelectTrigger>
                                <SelectContent>
                                    {periods.map((period) => (
                                        <SelectItem key={period.id} value={period.id}>
                                            {period.periodCode} - {period.periodName}
                                        </SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="allocationDate">Allocation Date</Label>
                            <Input
                                id="allocationDate"
                                type="date"
                                value={runForm.allocationDate}
                                min={selectedRunPeriod ? inputDate(selectedRunPeriod.startDate) : undefined}
                                max={selectedRunPeriod ? inputDate(selectedRunPeriod.endDate) : undefined}
                                onChange={(event) => setRunForm({ ...runForm, allocationDate: event.target.value })}
                            />
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="allocationDescription">Description</Label>
                            <Input
                                id="allocationDescription"
                                value={runForm.description}
                                onChange={(event) => setRunForm({ ...runForm, description: event.target.value })}
                            />
                        </div>
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setRunRule(null)} disabled={isRunning}>
                            Cancel
                        </Button>
                        <Button onClick={handleCreateRunBatch} disabled={isRunning || !runForm.fiscalPeriodId}>
                            <Play className="mr-2 h-4 w-4" />
                            Create Batch
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}
