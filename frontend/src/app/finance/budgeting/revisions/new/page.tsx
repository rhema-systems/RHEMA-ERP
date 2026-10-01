'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowLeft, Plus, Save, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import { budgetDataService } from '@/services/finance/budget-data.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import type {
    BudgetDimensionAssignment,
    BudgetRevisionLineInput,
    BudgetRevisionType,
    BudgetScenario,
} from '@/types/budget';
import type { Account, FiscalPeriod, SegmentLookupValue } from '@/types/finance';

const today = () => new Date().toISOString().slice(0, 10);
const emptyLine = (): BudgetRevisionLineInput => ({
    accountId: '',
    fiscalPeriodId: '',
    adjustmentAmountBase: 0,
    notes: '',
});

interface GovernedCombinationOption {
    key: string;
    segmentValueId?: string;
    financeDimensionSetId: string;
    label: string;
    assignments: BudgetDimensionAssignment[];
}

export default function NewBudgetRevisionPage() {
    const router = useRouter();
    const { toast } = useToast();
    const [scenarios, setScenarios] = useState<BudgetScenario[]>([]);
    const [accounts, setAccounts] = useState<Account[]>([]);
    const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
    const [segments, setSegments] = useState<SegmentLookupValue[]>([]);
    const [governedCombinations, setGovernedCombinations] = useState<GovernedCombinationOption[]>([]);
    const [saving, setSaving] = useState(false);
    const [sourceScenarioId, setSourceScenarioId] = useState('');
    const [revisionType, setRevisionType] = useState<BudgetRevisionType>('Virement');
    const [effectiveDate, setEffectiveDate] = useState(today());
    const [boardResolutionReference, setBoardResolutionReference] = useState('');
    const [boardResolutionDate, setBoardResolutionDate] = useState(today());
    const [justification, setJustification] = useState('');
    const [lines, setLines] = useState<BudgetRevisionLineInput[]>([emptyLine(), emptyLine()]);

    const loadReferenceData = useCallback(async () => {
        try {
            const [allScenarios, allAccounts, structures] = await Promise.all([
                budgetDataService.getScenarios(),
                financeDataService.getAccounts({ status: 'Active', take: 1000 }),
                financeDataService.getSegmentStructures(),
            ]);
            const official = allScenarios.filter(item => item.isActive && item.status === 'Approved');
            setScenarios(official);
            // Match the backend's existing budget-entry eligibility rule so users
            // cannot select a header account and discover the problem only on save.
            setAccounts(allAccounts.filter(item =>
                item.allowDirectPosting
                && (item.accountType === 'Revenue' || item.accountType === 'Expense')));
            if (official.length === 1) setSourceScenarioId(official[0].id);

            // Budget returns use reporting-dimension values such as TDC cost centres.
            // Values are flattened for selection while their IDs remain the audited key.
            const reporting = structures.filter(item => item.isReportingDimension && item.isActive);
            const values = await Promise.all(reporting.map(item => financeDataService.getSegmentLookupValues(item.id)));
            setSegments(values.flat().filter(item => item.isActive));
        } catch (error) {
            console.error('Failed to load budget revision reference data', error);
            toast({ title: 'Unable to load budget reference data', variant: 'destructive' });
        }
    }, [toast]);

    useEffect(() => { void loadReferenceData(); }, [loadReferenceData]);

    useEffect(() => {
        if (!sourceScenarioId) {
            setPeriods([]);
            setGovernedCombinations([]);
            return;
        }
        const scenario = scenarios.find(item => item.id === sourceScenarioId);
        if (!scenario) return;
        void Promise.all([
            financeDataService.getFiscalPeriods(scenario.fiscalYearId),
            budgetDataService.getReturns(scenario.id),
        ])
            .then(async ([fiscalPeriods, returns]) => {
                setPeriods(fiscalPeriods);
                if (scenario.controlDimensions.length === 0) {
                    setGovernedCombinations([]);
                    return;
                }

                const entriesByReturn = await Promise.all(returns.map(async budgetReturn => ({
                    budgetReturn,
                    entries: await budgetDataService.getEntries(budgetReturn.id),
                })));
                const combinations = new Map<string, GovernedCombinationOption>();
                for (const { budgetReturn, entries } of entriesByReturn) {
                    for (const entry of entries) {
                        if (!entry.financeDimensionSetId || entry.dimensionAssignments.length === 0) continue;
                        const key = `${budgetReturn.segmentValueId ?? 'general'}:${entry.financeDimensionSetId}`;
                        if (combinations.has(key)) continue;
                        const assignmentLabel = entry.dimensionAssignments
                            .map(item => `${item.dimensionCode}: ${item.valueCode} - ${item.valueName}`)
                            .join(' · ');
                        combinations.set(key, {
                            key,
                            segmentValueId: budgetReturn.segmentValueId,
                            financeDimensionSetId: entry.financeDimensionSetId,
                            label: `${budgetReturn.distributionDimensionName ?? budgetReturn.segmentValueName ?? 'Unassigned distribution'} · ${assignmentLabel}`,
                            assignments: entry.dimensionAssignments,
                        });
                    }
                }
                setGovernedCombinations([...combinations.values()].sort((left, right) =>
                    left.label.localeCompare(right.label)));
            })
            .catch(error => {
                console.error('Failed to load revision budget cells', error);
                toast({ title: 'Unable to load revision budget cells', variant: 'destructive' });
            });
    }, [scenarios, sourceScenarioId, toast]);

    const netChange = useMemo(
        () => lines.reduce((sum, line) => sum + Number(line.adjustmentAmountBase || 0), 0),
        [lines]
    );
    const sourceScenario = useMemo(
        () => scenarios.find(item => item.id === sourceScenarioId),
        [scenarios, sourceScenarioId]
    );
    const hasControlDimensions = (sourceScenario?.controlDimensions.length ?? 0) > 0;
    const currencyCode = sourceScenario?.baseCurrencyCode ?? 'GHS';

    const updateLine = (index: number, patch: Partial<BudgetRevisionLineInput>) => {
        setLines(current => current.map((line, position) => position === index ? { ...line, ...patch } : line));
    };

    const changeType = (value: BudgetRevisionType) => {
        setRevisionType(value);
        if (value === 'Supplementary' && lines.length > 1 && lines.every(line => line.adjustmentAmountBase === 0)) {
            setLines([emptyLine()]);
        }
    };

    const save = async () => {
        if (!sourceScenarioId || !boardResolutionReference.trim() || justification.trim().length < 20) {
            toast({
                title: 'Complete the governance evidence',
                description: 'Select the official budget, cite the Board resolution, and enter at least 20 characters of justification.',
                variant: 'destructive',
            });
            return;
        }
        if (lines.some(line => !line.accountId || !line.fiscalPeriodId || Number(line.adjustmentAmountBase) === 0)) {
            toast({ title: 'Complete every adjustment line', variant: 'destructive' });
            return;
        }
        if (hasControlDimensions && lines.some(line => !line.financeDimensionSetId)) {
            toast({
                title: 'Select the controlling dimensions for every line',
                description: 'Each revision line must target one exact governed budget combination.',
                variant: 'destructive',
            });
            return;
        }
        if (revisionType === 'Virement' && Math.abs(netChange) > 0.005) {
            toast({ title: 'A virement must net to GHS 0.00', variant: 'destructive' });
            return;
        }
        if (revisionType === 'Supplementary' && lines.some(line => line.adjustmentAmountBase < 0)) {
            toast({ title: 'Supplementary budgets cannot contain reductions', variant: 'destructive' });
            return;
        }

        try {
            setSaving(true);
            const created = await budgetDataService.createRevision({
                sourceScenarioId,
                revisionType,
                effectiveDate,
                boardResolutionReference: boardResolutionReference.trim(),
                boardResolutionDate,
                justification: justification.trim(),
                lines,
            });
            toast({ title: `${created.revisionNumber} created` });
            router.push(`/finance/budgeting/revisions/${created.id}`);
        } catch (error) {
            console.error('Failed to create budget revision', error);
            toast({
                title: 'Budget revision was not created',
                description: error instanceof Error ? error.message : 'Review the balances and governance evidence, then try again.',
                variant: 'destructive',
            });
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="space-y-6">
            <div className="flex items-center gap-3">
                <Button variant="outline" size="icon" asChild><Link href="/finance/budgeting/revisions"><ArrowLeft className="h-4 w-4" /></Link></Button>
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">New Budget Revision</h1>
                    <p className="text-muted-foreground">Prepare a Board-authorized change to the official Finance budget.</p>
                </div>
            </div>

            <Card>
                <CardHeader>
                    <CardTitle>Authority and effective date</CardTitle>
                    <CardDescription>The cited resolution remains attached to every resulting budget figure.</CardDescription>
                </CardHeader>
                <CardContent className="grid gap-4 md:grid-cols-2">
                    <div className="space-y-2 md:col-span-2">
                        <Label>Official source budget</Label>
                        <Select value={sourceScenarioId} onValueChange={setSourceScenarioId}>
                            <SelectTrigger><SelectValue placeholder="Select the current official budget" /></SelectTrigger>
                            <SelectContent>{scenarios.map(item => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}</SelectContent>
                        </Select>
                    </div>
                    <div className="space-y-2">
                        <Label>Revision type</Label>
                        <Select value={revisionType} onValueChange={value => changeType(value as BudgetRevisionType)}>
                            <SelectTrigger><SelectValue /></SelectTrigger>
                            <SelectContent><SelectItem value="Virement">Virement (net zero)</SelectItem><SelectItem value="Supplementary">Supplementary budget</SelectItem></SelectContent>
                        </Select>
                    </div>
                    <div className="space-y-2"><Label>Effective date</Label><Input type="date" value={effectiveDate} onChange={event => setEffectiveDate(event.target.value)} /></div>
                    <div className="space-y-2"><Label>Board resolution reference</Label><Input value={boardResolutionReference} onChange={event => setBoardResolutionReference(event.target.value)} placeholder="e.g. TDC/BOARD/2026/047" /></div>
                    <div className="space-y-2"><Label>Board resolution date</Label><Input type="date" max={today()} value={boardResolutionDate} onChange={event => setBoardResolutionDate(event.target.value)} /></div>
                    <div className="space-y-2 md:col-span-2"><Label>Business justification</Label><Textarea value={justification} onChange={event => setJustification(event.target.value)} placeholder="Explain why the reallocation or supplementary authority is necessary..." /></div>
                </CardContent>
            </Card>

            {hasControlDimensions && (
                <Card className="border-primary/30 bg-primary/5">
                    <CardHeader>
                        <CardTitle className="text-base">Dimension-controlled revision</CardTitle>
                        <CardDescription>
                            Every adjustment must select the complete controlling combination from the official budget. The immutable combination is carried into the successor scenario.
                        </CardDescription>
                    </CardHeader>
                </Card>
            )}

            <Card>
                <CardHeader>
                    <div className="flex items-center justify-between gap-3">
                        <div><CardTitle>Signed base-currency adjustments</CardTitle><CardDescription>Negative releases and positive increases are checked against the official budget.</CardDescription></div>
                        <Button variant="outline" onClick={() => setLines(current => [...current, emptyLine()])}><Plus className="mr-2 h-4 w-4" /> Add line</Button>
                    </div>
                </CardHeader>
                <CardContent className="space-y-4">
                    {lines.map((line, index) => (
                        <div key={index} className="grid gap-3 rounded-lg border p-4 md:grid-cols-12">
                            <div className="space-y-2 md:col-span-3">
                                <Label>{hasControlDimensions ? 'Budget return & dimensions' : 'Cost centre'}</Label>
                                {hasControlDimensions ? (
                                    <Select
                                        value={line.financeDimensionSetId
                                            ? `${line.segmentValueId ?? 'general'}:${line.financeDimensionSetId}`
                                            : undefined}
                                        onValueChange={value => {
                                            const option = governedCombinations.find(item => item.key === value);
                                            if (option) updateLine(index, {
                                                segmentValueId: option.segmentValueId,
                                                financeDimensionSetId: option.financeDimensionSetId,
                                            });
                                        }}
                                    >
                                        <SelectTrigger><SelectValue placeholder="Select exact combination" /></SelectTrigger>
                                        <SelectContent>{governedCombinations.map(item => (
                                            <SelectItem key={item.key} value={item.key}>{item.label}</SelectItem>
                                        ))}</SelectContent>
                                    </Select>
                                ) : (
                                    <Select value={line.segmentValueId ?? 'general'} onValueChange={value => updateLine(index, { segmentValueId: value === 'general' ? undefined : value, financeDimensionSetId: undefined })}>
                                        <SelectTrigger><SelectValue /></SelectTrigger>
                                        <SelectContent><SelectItem value="general">General</SelectItem>{segments.map(item => <SelectItem key={item.id} value={item.id}>{item.segmentValue} - {item.description}</SelectItem>)}</SelectContent>
                                    </Select>
                                )}
                            </div>
                            <div className="space-y-2 md:col-span-3">
                                <Label>GL account</Label>
                                <Select value={line.accountId} onValueChange={value => updateLine(index, { accountId: value })}>
                                    <SelectTrigger><SelectValue placeholder="Account" /></SelectTrigger>
                                    <SelectContent>{accounts.map(item => <SelectItem key={item.id} value={item.id}>{item.accountCode} - {item.accountName}</SelectItem>)}</SelectContent>
                                </Select>
                            </div>
                            <div className="space-y-2 md:col-span-2">
                                <Label>Fiscal period</Label>
                                <Select value={line.fiscalPeriodId} onValueChange={value => updateLine(index, { fiscalPeriodId: value })}>
                                    <SelectTrigger><SelectValue placeholder="Period" /></SelectTrigger>
                                    <SelectContent>{periods.map(item => <SelectItem key={item.id} value={item.id}>{item.periodCode}</SelectItem>)}</SelectContent>
                                </Select>
                            </div>
                            <div className="space-y-2 md:col-span-2"><Label>Adjustment ({currencyCode})</Label><Input type="number" step="0.01" value={line.adjustmentAmountBase} onChange={event => updateLine(index, { adjustmentAmountBase: Number(event.target.value) })} /></div>
                            <div className="space-y-2 md:col-span-1"><Label>Line note</Label><Input value={line.notes ?? ''} onChange={event => updateLine(index, { notes: event.target.value })} /></div>
                            <div className="flex items-end md:col-span-1"><Button variant="ghost" size="icon" disabled={lines.length === 1} onClick={() => setLines(current => current.filter((_, position) => position !== index))}><Trash2 className="h-4 w-4" /></Button></div>
                        </div>
                    ))}
                    <div className="flex items-center justify-between rounded-lg bg-muted p-4">
                        <span className="font-medium">Net change to official budget</span>
                        <span className={`text-lg font-bold ${revisionType === 'Virement' && Math.abs(netChange) > 0.005 ? 'text-destructive' : ''}`}>{currencyCode} {netChange.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</span>
                    </div>
                </CardContent>
            </Card>

            <div className="flex justify-end gap-2"><Button variant="outline" asChild><Link href="/finance/budgeting/revisions">Cancel</Link></Button><Button onClick={() => void save()} disabled={saving}><Save className="mr-2 h-4 w-4" />{saving ? 'Saving...' : 'Create Draft'}</Button></div>
        </div>
    );
}
