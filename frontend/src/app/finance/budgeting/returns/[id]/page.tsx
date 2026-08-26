'use client';

import React, { useState, useEffect, useMemo, use } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useToast } from '@/components/ui/use-toast';
import { Save, Send, CheckCircle, Loader2, ArrowLeft, RotateCcw } from 'lucide-react';
import { budgetDataService } from '@/services/finance/budget-data.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import { useAuth } from '@/hooks/use-auth';
import type { BudgetReturn, BudgetScenario, BudgetEntryDto, BudgetAuditEvent, BudgetDimensionAssignmentInput } from '@/types/budget';
import type { Account, FinanceDimensionDefinition, FiscalPeriod } from '@/types/finance';
import {
    budgetDimensionCombinationKey,
    budgetDimensionCombinationLabel,
    buildBudgetDimensionCombinations,
    isBudgetCombinationValidForPeriod,
    isCompleteBudgetDimensionCombination,
    LEGACY_BUDGET_COMBINATION_KEY,
    type BudgetDimensionCombination,
} from '@/lib/finance/budget-dimension-grid';

interface PageProps {
    params: Promise<{
        id: string;
    }>;
}

interface BudgetGridCell {
    amount: number;
    id?: string;
    rowVersion?: string;
}

type BudgetGrid = Record<string, Record<string, BudgetGridCell>>;

export default function BudgetReturnEditorPage({ params }: PageProps) {
    const { id } = use(params);
    const { toast } = useToast();
    const { user, hasPermission } = useAuth();

    // Data State
    const [budgetReturn, setBudgetReturn] = useState<BudgetReturn | null>(null);
    const [scenario, setScenario] = useState<BudgetScenario | null>(null);
    const [accounts, setAccounts] = useState<Account[]>([]);
    const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
    const [financeDimensions, setFinanceDimensions] = useState<FinanceDimensionDefinition[]>([]);
    const [auditHistory, setAuditHistory] = useState<BudgetAuditEvent[]>([]);

    // UI State
    const [isLoading, setIsLoading] = useState(true);
    const [isSaving, setIsSaving] = useState(false);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [activeTab, setActiveTab] = useState('Expenses');
    const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);

    // One account/period grid per immutable budget-dimension combination.
    const [gridData, setGridData] = useState<Record<string, BudgetGrid>>({});
    const [dimensionCombinations, setDimensionCombinations] = useState<BudgetDimensionCombination[]>([]);
    const [activeCombinationKey, setActiveCombinationKey] = useState('');
    const [draftAssignments, setDraftAssignments] = useState<Record<string, string>>({});

    useEffect(() => {
        loadData();
    }, [id]);

    const loadData = async () => {
        try {
            setIsLoading(true);
            // 1. Get Return
            const ret = await budgetDataService.getReturnById(id);
            setBudgetReturn(ret);

            // 2. Get Scenario & Entries & Accounts (Parallel)
            const [scen, ent, allAccounts, auditData, dimensions] = await Promise.all([
                budgetDataService.getScenarioById(ret.budgetScenarioId),
                budgetDataService.getEntries(id),
                financeDataService.getAccounts({ status: 'Active' }),
                budgetDataService.getReturnAuditHistory(id).catch(() => []),
                financeDataService.getFinanceDimensions(true),
            ]);
            setScenario(scen);
            setAccounts(allAccounts.filter(account => account.status === 'Active' && account.allowDirectPosting));
            setAuditHistory(auditData);
            setFinanceDimensions(dimensions);

            // 3. Get Fiscal Year for Periods
            const fy = await financeDataService.getFiscalYearById(scen.fiscalYearId);
            // Sort periods by number
            const sortedPeriods = [...(fy.periods || [])].sort((a, b) => a.periodNumber - b.periodNumber);
            setPeriods(sortedPeriods);

            // 4. Build Grid Data
            const combinations = buildBudgetDimensionCombinations(ent, scen.controlDimensions, dimensions);
            const initialGrid: Record<string, BudgetGrid> = {};
            combinations.forEach(combination => { initialGrid[combination.key] = {}; });
            ent.forEach(e => {
                const assignments = e.dimensionAssignments.map(assignment => ({
                    financeDimensionDefinitionId: assignment.financeDimensionDefinitionId,
                    financeDimensionValueId: assignment.financeDimensionValueId,
                }));
                const combinationKey = budgetDimensionCombinationKey(assignments);
                if (!initialGrid[combinationKey]) return;
                if (!initialGrid[combinationKey][e.accountId]) initialGrid[combinationKey][e.accountId] = {};
                initialGrid[combinationKey][e.accountId][e.fiscalPeriodId] = {
                    amount: e.amount,
                    id: e.id,
                    rowVersion: e.rowVersion,
                };
            });
            setGridData(initialGrid);
            setDimensionCombinations(combinations);
            setActiveCombinationKey(combinations[0]?.key || '');
            setDraftAssignments({});

        } catch (error) {
            console.error('Failed to load budget return data:', error);
            toast({
                title: 'Error',
                description: 'Failed to load data. Please refresh.',
                variant: 'destructive',
            });
        } finally {
            setIsLoading(false);
        }
    };

    // Filter Accounts by Class/Type
    const expenseAccounts = useMemo(() => accounts.filter(a => a.accountType === 'Expense'), [accounts]);
    const revenueAccounts = useMemo(() => accounts.filter(a => a.accountType === 'Revenue'), [accounts]);

    const handleInputChange = (accountId: string, periodId: string, value: string) => {
        if (!activeCombinationKey) return;
        const numValue = value === '' ? 0 : parseFloat(value);
        setGridData(prev => ({
            ...prev,
            [activeCombinationKey]: {
                ...prev[activeCombinationKey],
                [accountId]: {
                    ...prev[activeCombinationKey]?.[accountId],
                    [periodId]: {
                        ...prev[activeCombinationKey]?.[accountId]?.[periodId],
                        amount: numValue,
                    },
                },
            }
        }));
        setHasUnsavedChanges(true);
    };

    const calculateRowTotal = (accountId: string) => {
        const row = gridData[activeCombinationKey]?.[accountId];
        if (!row) return 0;
        return Object.values(row).reduce((sum, cell) => sum + (cell.amount || 0), 0);
    };

    const handleAddCombination = () => {
        if (!scenario) return;
        const assignments = scenario.controlDimensions.map(control => ({
            financeDimensionDefinitionId: control.financeDimensionDefinitionId,
            financeDimensionValueId: draftAssignments[control.financeDimensionDefinitionId] || '',
        }));
        if (!isCompleteBudgetDimensionCombination(scenario.controlDimensions, assignments)
            || assignments.some(assignment => !assignment.financeDimensionValueId)) {
            toast({
                title: 'Dimension values required',
                description: 'Select one value for every budget-control dimension.',
                variant: 'destructive',
            });
            return;
        }
        const key = budgetDimensionCombinationKey(assignments);
        const existing = dimensionCombinations.find(combination => combination.key === key);
        if (existing) {
            setActiveCombinationKey(existing.key);
            return;
        }
        const combination: BudgetDimensionCombination = {
            key,
            assignments,
            label: budgetDimensionCombinationLabel(assignments, scenario.controlDimensions, financeDimensions),
        };
        setDimensionCombinations(current => [...current, combination].sort((left, right) => left.label.localeCompare(right.label)));
        setGridData(current => ({ ...current, [key]: {} }));
        setActiveCombinationKey(key);
        setDraftAssignments({});
        setHasUnsavedChanges(true);
    };

    const handleSave = async (): Promise<BudgetReturn | null> => {
        if (!budgetReturn || !scenario) return null;
        setIsSaving(true);

        try {
            // Transform Grid to DTOs
            const entriesToSave: BudgetEntryDto[] = [];

            // Iterate over all accounts in the grid
            dimensionCombinations.forEach(combination => {
                const combinationGrid = gridData[combination.key] || {};
                Object.keys(combinationGrid).forEach(accountId => {
                    Object.keys(combinationGrid[accountId]).forEach(periodId => {
                        const cell = combinationGrid[accountId][periodId];
                        entriesToSave.push({
                            id: cell.id,
                            budgetReturnId: budgetReturn.id,
                            accountId,
                            fiscalPeriodId: periodId,
                            amount: cell.amount,
                            currencyCode: scenario.baseCurrencyCode,
                            exchangeRate: 1.0,
                            rowVersion: cell.rowVersion,
                            dimensionAssignments: combination.assignments,
                        });
                    });
                });
            });

            const updatedReturn = await budgetDataService.bulkSaveEntries({
                returnId: budgetReturn.id,
                returnRowVersion: budgetReturn.rowVersion,
                entries: entriesToSave
            });

            setBudgetReturn(updatedReturn);
            setAuditHistory(await budgetDataService.getReturnAuditHistory(updatedReturn.id));
            setHasUnsavedChanges(false);
            toast({ title: 'Saved', description: 'Budget entries saved successfully.' });
            return updatedReturn;
        } catch (error) {
            console.error('Save failed:', error);
            toast({
                title: 'Error',
                description: error instanceof Error ? error.message : 'Failed to save changes.',
                variant: 'destructive'
            });
            return null;
        } finally {
            setIsSaving(false);
        }
    };

    const handleSubmit = async () => {
        if (!budgetReturn) return;
        let currentReturn = budgetReturn;
        if (hasUnsavedChanges) {
            if (!confirm('You have unsaved changes. Save them first?')) return;
            const saved = await handleSave();
            if (!saved) return;
            currentReturn = saved;
        }
        if (!confirm('Are you sure you want to submit this budget for approval? You will not be able to edit it afterwards.')) return;

        setIsSubmitting(true);
        try {
            await budgetDataService.submitReturn({
                returnId: currentReturn.id,
                rowVersion: currentReturn.rowVersion,
            });
            toast({ title: 'Submitted', description: 'Budget submitted for approval.' });
            await loadData();
        } catch (error) {
            toast({ title: 'Error', description: error instanceof Error ? error.message : 'Failed to submit budget.', variant: 'destructive' });
        } finally {
            setIsSubmitting(false);
        }
    };

    const handleRecall = async () => {
        if (!budgetReturn || !confirm('Recall this submitted return for changes?')) return;
        setIsSubmitting(true);
        try {
            const recalled = await budgetDataService.recallReturn(
                budgetReturn.id,
                budgetReturn.rowVersion,
                'Recalled by preparer for changes.',
            );
            setBudgetReturn(recalled);
            setAuditHistory(await budgetDataService.getReturnAuditHistory(recalled.id));
            toast({ title: 'Return recalled', description: 'The worksheet is editable again.' });
        } catch (error) {
            toast({ title: 'Recall failed', description: error instanceof Error ? error.message : 'Refresh and try again.', variant: 'destructive' });
        } finally {
            setIsSubmitting(false);
        }
    };

    // Render Logic
    if (isLoading) return <div className="h-screen flex items-center justify-center"><Loader2 className="h-8 w-8 animate-spin text-primary" /></div>;
    if (!budgetReturn || !scenario) return <div className="p-8">Return not found</div>;

    const normalizedRoles = new Set((user?.roles || []).map(role => role.toLowerCase()));
    const isPrivileged = normalizedRoles.has('admin')
        || normalizedRoles.has('tenantadmin')
        || normalizedRoles.has('superadmin');
    const isAssignedUser = budgetReturn.assignedToUserId === user?.id;
    const isDraftLike = budgetReturn.status === 'Draft' || budgetReturn.status === 'Rejected';
    const scenarioIsCollecting = scenario.status === 'Collecting';
    const isEditable = isDraftLike
        && scenarioIsCollecting
        && (isPrivileged || (isAssignedUser && hasPermission('Finance.BudgetReturns.Edit')));
    const canSubmit = isDraftLike
        && scenarioIsCollecting
        && (isPrivileged || (isAssignedUser && hasPermission('Finance.BudgetReturns.Submit')));
    const canRecall = budgetReturn.status === 'Submitted'
        && scenarioIsCollecting
        && (isPrivileged || (isAssignedUser && hasPermission('Finance.BudgetReturns.Submit')));
    const activeCombination = dimensionCombinations.find(combination => combination.key === activeCombinationKey);
    const hasControlledDimensions = scenario.controlDimensions.length > 0;

    const renderGrid = (accountList: Account[]) => (
        <div className="h-full min-h-0 border rounded-md overflow-auto">
            <Table>
                <TableHeader>
                    <TableRow>
                        <TableHead className="w-[300px] min-w-[200px] sticky left-0 bg-background z-10">Account</TableHead>
                        {periods.map(p => (
                            <TableHead key={p.id} className="min-w-[120px] text-right">{p.periodName}</TableHead>
                        ))}
                        <TableHead className="min-w-[120px] text-right font-bold bg-muted/50">Total</TableHead>
                    </TableRow>
                </TableHeader>
                <TableBody>
                    {accountList.length === 0 ? (
                        <TableRow>
                            <TableCell colSpan={periods.length + 2} className="text-center py-8 text-muted-foreground">No accounts found for this category.</TableCell>
                        </TableRow>
                    ) : accountList.map(account => (
                        <TableRow key={account.id}>
                            <TableCell className="font-medium sticky left-0 bg-background z-10 border-r">
                                <div className="flex flex-col">
                                    <span>{account.accountName}</span>
                                    <span className="text-xs text-muted-foreground">{account.accountCode}</span>
                                </div>
                            </TableCell>
                            {periods.map(period => {
                                const cell = activeCombinationKey
                                    ? gridData[activeCombinationKey]?.[account.id]?.[period.id]
                                    : undefined;
                                const val = cell?.amount || 0;
                                const dimensionValid = !activeCombination
                                    || activeCombination.key === LEGACY_BUDGET_COMBINATION_KEY
                                    || isBudgetCombinationValidForPeriod(
                                        activeCombination.assignments,
                                        financeDimensions,
                                        period,
                                    );
                                return (
                                    <TableCell key={period.id} className="p-1" title={dimensionValid
                                        ? undefined
                                        : 'One or more selected dimension values are not effective for this full fiscal period.'}>
                                        <Input
                                            type="number"
                                            className="text-right h-8 border-transparent hover:border-input focus:border-input bg-transparent"
                                            value={val === 0 ? '' : val}
                                            onChange={(e) => handleInputChange(account.id, period.id, e.target.value)}
                                            disabled={!isEditable || !dimensionValid || !activeCombinationKey}
                                            placeholder={dimensionValid ? '-' : 'N/A'}
                                        />
                                    </TableCell>
                                );
                            })}
                            <TableCell className="text-right font-bold bg-muted/30">
                                {new Intl.NumberFormat('en-US', { minimumFractionDigits: 2 }).format(calculateRowTotal(account.id))}
                            </TableCell>
                        </TableRow>
                    ))}
                </TableBody>
            </Table>
        </div>
    );

    return (
        <div className="flex flex-col h-[calc(100vh-4rem)] min-h-0">
            {/* Header */}
            <div className="flex-none p-6 pb-2 space-y-4">
                <Breadcrumb>
                    <BreadcrumbList>
                        <BreadcrumbItem>
                            <BreadcrumbLink href={`/finance/budgeting/scenarios/${scenario.id}`}>
                                <ArrowLeft className="h-4 w-4 mr-1 inline" />
                                Back to Scenario
                            </BreadcrumbLink>
                        </BreadcrumbItem>
                        <BreadcrumbSeparator />
                        <BreadcrumbItem>
                            <BreadcrumbPage>{budgetReturn.segmentValueName || 'Budget Worksheet'}</BreadcrumbPage>
                        </BreadcrumbItem>
                    </BreadcrumbList>
                </Breadcrumb>

                <div className="flex items-center justify-between">
                    <div>
                        <h1 className="text-2xl font-bold tracking-tight flex items-center gap-2">
                            {budgetReturn.segmentValueName}
                            <Badge variant={isEditable ? 'outline' : 'secondary'} className={isEditable ? 'bg-yellow-50 text-yellow-700 border-yellow-200' : ''}>
                                {budgetReturn.status}
                            </Badge>
                        </h1>
                        <p className="text-sm text-muted-foreground">
                            {scenario.name} • {scenario.baseCurrencyCode}
                        </p>
                    </div>
                    <div className="flex gap-2">
                        {(isEditable || canSubmit || canRecall) && (
                            <>
                                {isEditable && (
                                    <Button variant="outline" onClick={handleSave} disabled={isSaving || !hasUnsavedChanges}>
                                        {isSaving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                                        Save Draft
                                    </Button>
                                )}
                                {canSubmit && (
                                    <Button onClick={handleSubmit} disabled={isSubmitting || isSaving}>
                                        {isSubmitting ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Send className="mr-2 h-4 w-4" />}
                                        Submit Budget
                                    </Button>
                                )}
                                {canRecall && (
                                    <Button variant="outline" onClick={handleRecall} disabled={isSubmitting}>
                                        <RotateCcw className="mr-2 h-4 w-4" />
                                        Recall
                                    </Button>
                                )}
                            </>
                        )}
                        {!isEditable && !canSubmit && !canRecall && (
                            <div className="flex items-center px-4 py-2 bg-muted rounded text-sm text-muted-foreground">
                                <CheckCircle className="w-4 h-4 mr-2" />
                                Read Only
                            </div>
                        )}
                    </div>
                </div>
                <details className="rounded-md border bg-muted/20 px-4 py-2">
                    <summary className="cursor-pointer text-sm font-medium">
                        Audit history ({auditHistory.length})
                    </summary>
                    <div className="mt-3 max-h-36 space-y-2 overflow-auto">
                        {auditHistory.length === 0 ? (
                            <p className="text-sm text-muted-foreground">No audit events recorded yet.</p>
                        ) : auditHistory.map(event => (
                            <div key={event.id} className="flex justify-between gap-4 text-sm">
                                <span>{event.action.replace('Finance.', '')} · {event.username}</span>
                                <time className="text-muted-foreground">{new Date(event.timestamp).toLocaleString()}</time>
                            </div>
                        ))}
                    </div>
                </details>
                {hasControlledDimensions && (
                    <Card>
                        <CardHeader className="pb-3">
                            <CardTitle className="text-base">Budget dimension combination</CardTitle>
                            <CardDescription>
                                Amounts below belong only to the selected combination. Add another combination for a different department, cost centre, project, or other controlled value.
                            </CardDescription>
                        </CardHeader>
                        <CardContent className="space-y-3">
                            {dimensionCombinations.length > 0 && (
                                <div className="space-y-2">
                                    <Label htmlFor="budget-combination">Current combination</Label>
                                    <Select value={activeCombinationKey} onValueChange={setActiveCombinationKey}>
                                        <SelectTrigger id="budget-combination">
                                            <SelectValue placeholder="Select a combination" />
                                        </SelectTrigger>
                                        <SelectContent>
                                            {dimensionCombinations.map(combination => (
                                                <SelectItem key={combination.key} value={combination.key}>
                                                    {combination.label}
                                                </SelectItem>
                                            ))}
                                        </SelectContent>
                                    </Select>
                                </div>
                            )}
                            {isEditable && (
                                <div className="grid gap-3 md:grid-cols-[repeat(auto-fit,minmax(190px,1fr))_auto] items-end rounded-md border p-3">
                                    {[...scenario.controlDimensions]
                                        .sort((left, right) => left.displayOrder - right.displayOrder)
                                        .map(control => {
                                            const definition = financeDimensions.find(item =>
                                                item.id === control.financeDimensionDefinitionId);
                                            const values = definition?.values.filter(value => value.isActive) || [];
                                            return (
                                                <div key={control.financeDimensionDefinitionId} className="space-y-2">
                                                    <Label>{control.dimensionCode} — {control.dimensionName}</Label>
                                                    <Select
                                                        value={draftAssignments[control.financeDimensionDefinitionId] || ''}
                                                        onValueChange={value => setDraftAssignments(current => ({
                                                            ...current,
                                                            [control.financeDimensionDefinitionId]: value,
                                                        }))}
                                                    >
                                                        <SelectTrigger>
                                                            <SelectValue placeholder="Select value" />
                                                        </SelectTrigger>
                                                        <SelectContent>
                                                            {values.map(value => (
                                                                <SelectItem key={value.id} value={value.id}>
                                                                    {value.code} — {value.name}
                                                                </SelectItem>
                                                            ))}
                                                        </SelectContent>
                                                    </Select>
                                                </div>
                                            );
                                        })}
                                    <Button type="button" variant="outline" onClick={handleAddCombination}>
                                        Add / Select Combination
                                    </Button>
                                </div>
                            )}
                            {dimensionCombinations.length === 0 && !isEditable && (
                                <p className="text-sm text-muted-foreground">No dimensioned budget cells were saved for this return.</p>
                            )}
                        </CardContent>
                    </Card>
                )}
            </div>

            {/* Content - Full Height Grid */}
            <div className="flex-1 min-h-0 p-6 pt-2 overflow-hidden flex flex-col">
                <Tabs defaultValue="Expenses" className="flex-1 min-h-0 flex flex-col" onValueChange={setActiveTab}>
                    <div className="flex items-center justify-between mb-2">
                        <TabsList>
                            <TabsTrigger value="Expenses">Expenses ({expenseAccounts.length})</TabsTrigger>
                            <TabsTrigger value="Revenue">Revenue ({revenueAccounts.length})</TabsTrigger>
                        </TabsList>
                        <div className="text-sm">
                            Current Total: <span className="font-bold">{
                                new Intl.NumberFormat('en-US', { style: 'currency', currency: scenario.baseCurrencyCode }).format(
                                    accounts.reduce((sum, acc) => {
                                        // Filter by active tab logic if needed, or show grand total
                                        // Let's show Tab Total
                                        const isActiveTabAccount = activeTab === 'Expenses'
                                            ? (acc.accountType === 'Expense')
                                            : (acc.accountType === 'Revenue');

                                        if (!isActiveTabAccount) return sum;
                                        return sum + calculateRowTotal(acc.id);
                                    }, 0)
                                )
                            }</span>
                        </div>
                    </div>

                    <TabsContent value="Expenses" className="flex-1 min-h-0 overflow-auto bg-white relative">
                        {activeCombinationKey ? renderGrid(expenseAccounts) : (
                            <div className="flex h-full items-center justify-center rounded-md border text-sm text-muted-foreground">
                                Add or select a complete dimension combination before entering amounts.
                            </div>
                        )}
                    </TabsContent>
                    <TabsContent value="Revenue" className="flex-1 min-h-0 overflow-auto bg-white relative">
                        {activeCombinationKey ? renderGrid(revenueAccounts) : (
                            <div className="flex h-full items-center justify-center rounded-md border text-sm text-muted-foreground">
                                Add or select a complete dimension combination before entering amounts.
                            </div>
                        )}
                    </TabsContent>
                </Tabs>
            </div>
        </div>
    );
}
