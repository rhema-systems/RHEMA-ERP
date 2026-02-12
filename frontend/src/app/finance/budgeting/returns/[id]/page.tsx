'use client';

import React, { useState, useEffect, useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Input } from '@/components/ui/input';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useToast } from '@/components/ui/use-toast';
import { Save, Send, CheckCircle, XCircle, ChevronLeft, Loader2, AlertTriangle, ArrowLeft } from 'lucide-react';
import { budgetDataService } from '@/services/finance/budget-data.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { BudgetReturn, BudgetScenario, BudgetEntry, BudgetEntryDto, SubmitBudgetReturnDto } from '@/types/budget';
import type { Account, FiscalYear, FiscalPeriod } from '@/types/finance';

interface PageProps {
    params: {
        id: string;
    };
}

// Helper to safely parse numbers
const parseNum = (val: string | number) => {
    const num = typeof val === 'string' ? parseFloat(val.replace(/,/g, '')) : val;
    return isNaN(num) ? 0 : num;
};

export default function BudgetReturnEditorPage({ params }: PageProps) {
    const { toast } = useToast();
    const router = useRouter();

    // Data State
    const [budgetReturn, setBudgetReturn] = useState<BudgetReturn | null>(null);
    const [scenario, setScenario] = useState<BudgetScenario | null>(null);
    const [fiscalYear, setFiscalYear] = useState<FiscalYear | null>(null);
    const [accounts, setAccounts] = useState<Account[]>([]);
    const [entries, setEntries] = useState<BudgetEntry[]>([]);
    const [periods, setPeriods] = useState<FiscalPeriod[]>([]);

    // UI State
    const [isLoading, setIsLoading] = useState(true);
    const [isSaving, setIsSaving] = useState(false);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [activeTab, setActiveTab] = useState('Expenses');
    const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);

    // Form State (The Grid)
    // Map<AccountId, Map<PeriodId, Amount>>
    const [gridData, setGridData] = useState<Record<string, Record<string, number>>>({});

    useEffect(() => {
        loadData();
    }, [params.id]);

    const loadData = async () => {
        try {
            setIsLoading(true);
            // 1. Get Return
            const ret = await budgetDataService.getReturnById(params.id);
            setBudgetReturn(ret);

            // 2. Get Scenario & Entries & Accounts (Parallel)
            const [scen, ent, allAccounts] = await Promise.all([
                budgetDataService.getScenarioById(ret.budgetScenarioId),
                budgetDataService.getEntries(params.id),
                financeDataService.getAccounts({ status: 'Active' }) // Get all active accounts
            ]);
            setScenario(scen);
            setEntries(ent);
            setAccounts(allAccounts);

            // 3. Get Fiscal Year for Periods
            const fy = await financeDataService.getFiscalYearById(scen.fiscalYearId);
            setFiscalYear(fy);
            // Sort periods by number
            const sortedPeriods = (fy.periods || []).sort((a, b) => a.periodNumber - b.periodNumber);
            setPeriods(sortedPeriods);

            // 4. Build Grid Data
            const initialGrid: Record<string, Record<string, number>> = {};
            ent.forEach(e => {
                if (!initialGrid[e.accountId]) initialGrid[e.accountId] = {};
                initialGrid[e.accountId][e.fiscalPeriodId] = e.amount;
            });
            setGridData(initialGrid);

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
        const numValue = value === '' ? 0 : parseFloat(value);
        setGridData(prev => ({
            ...prev,
            [accountId]: {
                ...prev[accountId],
                [periodId]: numValue
            }
        }));
        setHasUnsavedChanges(true);
    };

    const calculateRowTotal = (accountId: string) => {
        const row = gridData[accountId];
        if (!row) return 0;
        return Object.values(row).reduce((sum, val) => sum + (val || 0), 0);
    };

    const handleSave = async () => {
        if (!budgetReturn || !scenario) return;
        setIsSaving(true);

        try {
            // Transform Grid to DTOs
            const entriesToSave: BudgetEntryDto[] = [];

            // Iterate over all accounts in the grid
            Object.keys(gridData).forEach(accountId => {
                Object.keys(gridData[accountId]).forEach(periodId => {
                    const amount = gridData[accountId][periodId];
                    // Only save if amount is not 0, or if it was previously saved (to clear it) -> Actually safer to just save everything or check diff.
                    // For simplicity, we save all non-zero, or updates to existing.

                    // Simple approach: Send everything that has a value.
                    // Ideally we should map back to existing ID if updating.
                    // The backend `BulkSave` should handle "Update if exists, Insert if new".
                    // But we need to pass IDs if we have them to avoid duplicates if backend logic is weak? 
                    // Our backend logic for 'BulkSave' usually deletes old and inserts new OR updates. 
                    // Let's assume backend is smart enough to match on AccountId + PeriodId + ReturnId.

                    entriesToSave.push({
                        budgetReturnId: budgetReturn.id,
                        accountId: accountId,
                        fiscalPeriodId: periodId,
                        amount: amount,
                        currencyCode: 'GHS', // Default to Base for now, complex multi-currency omitted for MVP
                        exchangeRate: 1.0
                    });
                });
            });

            await budgetDataService.bulkSaveEntries({
                returnId: budgetReturn.id,
                entries: entriesToSave
            });

            setHasUnsavedChanges(false);
            toast({ title: 'Saved', description: 'Budget entries saved successfully.' });

            // Reload entries to get IDs back? Not strictly necessary if we rely on matrix logic.
        } catch (error) {
            console.error('Save failed:', error);
            toast({ title: 'Error', description: 'Failed to save changes.', variant: 'destructive' });
        } finally {
            setIsSaving(false);
        }
    };

    const handleSubmit = async () => {
        if (hasUnsavedChanges) {
            if (!confirm('You have unsaved changes. Save them first?')) return;
            await handleSave();
        }
        if (!confirm('Are you sure you want to submit this budget for approval? You will not be able to edit it afterwards.')) return;

        setIsSubmitting(true);
        try {
            await budgetDataService.submitReturn({ returnId: budgetReturn!.id });
            toast({ title: 'Submitted', description: 'Budget submitted for approval.' });
            loadData(); // Reload to update status
        } catch (error) {
            toast({ title: 'Error', description: 'Failed to submit budget.', variant: 'destructive' });
        } finally {
            setIsSubmitting(false);
        }
    };

    // Render Logic
    if (isLoading) return <div className="h-screen flex items-center justify-center"><Loader2 className="h-8 w-8 animate-spin text-primary" /></div>;
    if (!budgetReturn || !scenario) return <div className="p-8">Return not found</div>;

    const isEditable = budgetReturn.status === 'Draft' || budgetReturn.status === 'Rejected';

    const renderGrid = (accountList: Account[]) => (
        <div className="border rounded-md overflow-x-auto">
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
                                const val = gridData[account.id]?.[period.id] || 0;
                                return (
                                    <TableCell key={period.id} className="p-1">
                                        <Input
                                            type="number"
                                            className="text-right h-8 border-transparent hover:border-input focus:border-input bg-transparent"
                                            value={val === 0 ? '' : val}
                                            onChange={(e) => handleInputChange(account.id, period.id, e.target.value)}
                                            disabled={!isEditable}
                                            placeholder="-"
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
        <div className="flex flex-col h-[calc(100vh-4rem)]">
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
                        {isEditable && (
                            <>
                                <Button variant="outline" onClick={handleSave} disabled={isSaving || !hasUnsavedChanges}>
                                    {isSaving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                                    Save Draft
                                </Button>
                                <Button onClick={handleSubmit} disabled={isSubmitting}>
                                    {isSubmitting ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Send className="mr-2 h-4 w-4" />}
                                    Submit Budget
                                </Button>
                            </>
                        )}
                        {!isEditable && (
                            <div className="flex items-center px-4 py-2 bg-muted rounded text-sm text-muted-foreground">
                                <CheckCircle className="w-4 h-4 mr-2" />
                                Read Only
                            </div>
                        )}
                    </div>
                </div>
            </div>

            {/* Content - Full Height Grid */}
            <div className="flex-1 p-6 pt-2 overflow-hidden flex flex-col">
                <Tabs defaultValue="Expenses" className="flex-1 flex flex-col" onValueChange={setActiveTab}>
                    <div className="flex items-center justify-between mb-2">
                        <TabsList>
                            <TabsTrigger value="Expenses">Expenses ({expenseAccounts.length})</TabsTrigger>
                            <TabsTrigger value="Revenue">Revenue ({revenueAccounts.length})</TabsTrigger>
                        </TabsList>
                        <div className="text-sm">
                            Current Total: <span className="font-bold">{
                                new Intl.NumberFormat('en-US', { style: 'currency', currency: 'GHS' }).format(
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

                    <TabsContent value="Expenses" className="flex-1 overflow-auto border rounded-lg bg-white relative">
                        {renderGrid(expenseAccounts)}
                    </TabsContent>
                    <TabsContent value="Revenue" className="flex-1 overflow-auto border rounded-lg bg-white relative">
                        {renderGrid(revenueAccounts)}
                    </TabsContent>
                </Tabs>
            </div>
        </div>
    );
}
