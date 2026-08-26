'use client';

import React, { use, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import {
    AlertTriangle,
    ArrowLeft,
    BarChart3,
    CheckCircle2,
    Download,
    Loader2,
    RefreshCw,
    Scale,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/components/ui/use-toast';
import { budgetDataService } from '@/services/finance/budget-data.service';
import { DOCUMENT_TYPES, documentOutputService } from '@/services/document-output.service';
import { ReportPdfActions } from '@/components/finance/reports/ReportPdfActions';
import { useAuth } from '@/hooks/use-auth';
import type {
    BudgetReportContribution,
    BudgetScenario,
    BudgetScenarioComparison,
    ConsolidatedBudgetView,
} from '@/types/budget';

interface PageProps {
    params: Promise<{ id: string }>;
}

interface AccountMatrixRow {
    accountId: string;
    accountCode: string;
    accountName: string;
    accountType: string;
    periods: Record<string, number>;
    budget: number;
    actual: number;
    variance: number;
    contributions: BudgetReportContribution[];
}

const comparableStatuses = new Set(['Approved', 'Superseded']);

export default function ConsolidatedBudgetPage({ params }: PageProps) {
    const { id } = use(params);
    const { toast } = useToast();
    const { hasPermission } = useAuth();
    const canExport = hasPermission('Finance.Reports.Export');
    const [scenario, setScenario] = useState<BudgetScenario | null>(null);
    const [view, setView] = useState<ConsolidatedBudgetView | null>(null);
    const [candidates, setCandidates] = useState<BudgetScenario[]>([]);
    const [comparisonTargetId, setComparisonTargetId] = useState('');
    const [comparison, setComparison] = useState<BudgetScenarioComparison | null>(null);
    const [approvedOnly, setApprovedOnly] = useState(true);
    const [search, setSearch] = useState('');
    const [accountType, setAccountType] = useState('all');
    const [isLoading, setIsLoading] = useState(true);
    const [isComparing, setIsComparing] = useState(false);

    const load = async (scope?: boolean) => {
        try {
            setIsLoading(true);
            const current = await budgetDataService.getScenarioById(id);
            const nextApprovedOnly = scope ?? (current.status !== 'Collecting');
            const [report, yearScenarios] = await Promise.all([
                budgetDataService.getConsolidatedView(id, nextApprovedOnly),
                budgetDataService.getScenariosForYear(current.fiscalYearId),
            ]);
            setScenario(current);
            setApprovedOnly(nextApprovedOnly);
            setView(report);
            setCandidates(yearScenarios.filter(item =>
                item.id !== id && (item.isActive || comparableStatuses.has(item.status))
            ));
        } catch (error) {
            toast({
                title: 'Unable to load consolidated budget',
                description: error instanceof Error ? error.message : 'Refresh and try again.',
                variant: 'destructive',
            });
        } finally {
            setIsLoading(false);
        }
    };

    useEffect(() => {
        void load();
    }, [id]);

    const changeScope = async (nextApprovedOnly: boolean) => {
        setComparison(null);
        await load(nextApprovedOnly);
    };

    const money = (value: number) =>
        new Intl.NumberFormat('en-GH', {
            style: 'currency',
            currency: view?.currencyCode || 'GHS',
            maximumFractionDigits: 2,
        }).format(value);

    const accountRows = useMemo<AccountMatrixRow[]>(() => {
        if (!view) return [];
        const rows = new Map<string, AccountMatrixRow>();
        for (const line of view.lines) {
            const key = line.accountId;
            const row = rows.get(key) ?? {
                accountId: line.accountId,
                accountCode: line.accountCode,
                accountName: line.accountName,
                accountType: line.accountType,
                periods: {},
                budget: 0,
                actual: 0,
                variance: 0,
                contributions: [],
            };
            row.periods[line.fiscalPeriodId] =
                (row.periods[line.fiscalPeriodId] || 0) + line.budgetAmount;
            row.budget += line.budgetAmount;
            row.actual += line.actualAmount;
            row.variance += line.varianceAmount;
            for (const contribution of line.contributions) {
                const existing = row.contributions.find(item =>
                    item.budgetReturnId === contribution.budgetReturnId
                );
                if (existing) existing.budgetAmount += contribution.budgetAmount;
                else row.contributions.push({ ...contribution });
            }
            rows.set(key, row);
        }
        return Array.from(rows.values())
            .filter(row => accountType === 'all' || row.accountType === accountType)
            .filter(row => {
                const query = search.trim().toLowerCase();
                return !query
                    || row.accountCode.toLowerCase().includes(query)
                    || row.accountName.toLowerCase().includes(query);
            })
            .sort((left, right) => left.accountCode.localeCompare(right.accountCode));
    }, [view, search, accountType]);

    const comparisonRows = useMemo(() => {
        if (!comparison) return [];
        const rows = new Map<string, {
            accountCode: string;
            accountName: string;
            accountType: string;
            base: number;
            compared: number;
            difference: number;
        }>();
        for (const line of comparison.lines) {
            const row = rows.get(line.accountId) ?? {
                accountCode: line.accountCode,
                accountName: line.accountName,
                accountType: line.accountType,
                base: 0,
                compared: 0,
                difference: 0,
            };
            row.base += line.baseAmount;
            row.compared += line.comparisonAmount;
            row.difference += line.differenceAmount;
            rows.set(line.accountId, row);
        }
        return Array.from(rows.values()).sort((left, right) =>
            left.accountCode.localeCompare(right.accountCode)
        );
    }, [comparison]);

    const runComparison = async () => {
        if (!comparisonTargetId) return;
        try {
            setIsComparing(true);
            setComparison(await budgetDataService.compareScenarios(id, comparisonTargetId));
        } catch (error) {
            toast({
                title: 'Unable to compare scenarios',
                description: error instanceof Error ? error.message : 'Refresh and try again.',
                variant: 'destructive',
            });
        } finally {
            setIsComparing(false);
        }
    };

    const downloadComparisonPdf = async () => {
        if (!comparison) return;
        await documentOutputService.downloadReportDocument(
            DOCUMENT_TYPES.financeBudgetScenarioComparison,
            { baseScenarioId: id, comparisonScenarioId: comparison.comparisonScenarioId },
        );
    };

    const printComparison = async () => {
        if (!comparison) return;
        await documentOutputService.printReportDocument(
            DOCUMENT_TYPES.financeBudgetScenarioComparison,
            { baseScenarioId: id, comparisonScenarioId: comparison.comparisonScenarioId },
        );
    };

    const exportCsv = () => {
        if (!view) return;
        const quote = (value: string | number) =>
            `"${String(value).replaceAll('"', '""')}"`;
        const rows = [
            ['Account Code', 'Account Name', 'Type', 'Period', 'Budget', 'Actual', 'Variance'],
            ...view.lines.map(line => [
                line.accountCode,
                line.accountName,
                line.accountType,
                line.periodCode,
                line.budgetAmount,
                line.actualAmount,
                line.varianceAmount,
            ]),
        ];
        const blob = new Blob(
            [rows.map(row => row.map(quote).join(',')).join('\r\n')],
            { type: 'text/csv;charset=utf-8' }
        );
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = `${view.scenarioName.replaceAll(' ', '-')}-consolidated-budget.csv`;
        anchor.click();
        URL.revokeObjectURL(url);
    };

    if (isLoading && !view) {
        return (
            <div className="flex min-h-[320px] items-center justify-center text-muted-foreground">
                <Loader2 className="mr-2 h-5 w-5 animate-spin" />
                Compiling budget view...
            </div>
        );
    }

    if (!scenario || !view) {
        return <div className="p-8 text-center text-muted-foreground">Budget view is unavailable.</div>;
    }

    const errorCount = view.validationIssues.filter(issue => issue.severity === 'Error').length;
    const warningCount = view.validationIssues.filter(issue => issue.severity !== 'Error').length;

    return (
        <div className="space-y-6">
            <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-center">
                <div>
                    <Link
                        href={`/finance/budgeting/scenarios/${id}`}
                        className="mb-2 inline-flex items-center text-sm text-muted-foreground hover:text-foreground"
                    >
                        <ArrowLeft className="mr-1 h-4 w-4" />
                        Back to scenario
                    </Link>
                    <h1 className="flex items-center gap-2 text-3xl font-bold tracking-tight">
                        <BarChart3 className="h-8 w-8" />
                        Consolidated Budget
                    </h1>
                    <div className="mt-2 flex flex-wrap items-center gap-2 text-muted-foreground">
                        <span>{view.scenarioName}</span>
                        <span>•</span>
                        <span>{view.fiscalYearName}</span>
                        <Badge variant="outline">{view.scenarioStatus}</Badge>
                        {view.isOfficial && <Badge className="bg-green-600">Official baseline</Badge>}
                    </div>
                </div>
                <div className="flex flex-wrap gap-2">
                    <Button
                        variant={!approvedOnly ? 'default' : 'outline'}
                        onClick={() => void changeScope(false)}
                    >
                        Working view
                    </Button>
                    <Button
                        variant={approvedOnly ? 'default' : 'outline'}
                        onClick={() => void changeScope(true)}
                    >
                        Approved returns only
                    </Button>
                    {canExport && (
                        <>
                            <Button variant="outline" onClick={exportCsv}>
                                <Download className="mr-2 h-4 w-4" />
                                Export CSV
                            </Button>
                            <ReportPdfActions
                                reportName="consolidated budget"
                                onDownloadPdf={() => documentOutputService.downloadReportDocument(
                                    DOCUMENT_TYPES.financeBudgetConsolidated,
                                    { scenarioId: id, approvedOnly })}
                                onPrint={() => documentOutputService.printReportDocument(
                                    DOCUMENT_TYPES.financeBudgetConsolidated,
                                    { scenarioId: id, approvedOnly })}
                                disabled={isLoading || !view}
                            />
                        </>
                    )}
                    <Button variant="outline" size="icon" onClick={() => void load(approvedOnly)}>
                        <RefreshCw className={`h-4 w-4 ${isLoading ? 'animate-spin' : ''}`} />
                    </Button>
                </div>
            </div>

            <Card className={view.readyForSubmission ? 'border-green-200' : 'border-amber-200'}>
                <CardContent className="flex flex-col justify-between gap-4 pt-6 md:flex-row md:items-center">
                    <div className="flex items-start gap-3">
                        {view.readyForSubmission
                            ? <CheckCircle2 className="mt-0.5 h-5 w-5 text-green-600" />
                            : <AlertTriangle className="mt-0.5 h-5 w-5 text-amber-600" />}
                        <div>
                            <p className="font-semibold">
                                {view.readyForSubmission
                                    ? 'Scenario is ready for submission'
                                    : 'Pre-submission review requires attention'}
                            </p>
                            <p className="text-sm text-muted-foreground">
                                {view.approvedReturnCount} of {view.totalReturnCount} returns approved;
                                {' '}{view.includedReturnCount} included in this view.
                            </p>
                        </div>
                    </div>
                    <div className="flex gap-2">
                        <Badge variant={errorCount ? 'destructive' : 'outline'}>{errorCount} errors</Badge>
                        <Badge variant="outline">{warningCount} warnings</Badge>
                    </div>
                </CardContent>
            </Card>

            <div className="grid gap-4 md:grid-cols-3 xl:grid-cols-6">
                {[
                    ['Revenue budget', view.totalRevenueBudget],
                    ['Expense budget', view.totalExpenseBudget],
                    ['Net budget', view.netBudget],
                    ['Revenue actual', view.totalRevenueActual],
                    ['Expense actual', view.totalExpenseActual],
                    ['Net actual', view.netActual],
                ].map(([label, value]) => (
                    <Card key={String(label)}>
                        <CardHeader className="pb-2">
                            <CardDescription>{label}</CardDescription>
                            <CardTitle className="text-xl">{money(Number(value))}</CardTitle>
                        </CardHeader>
                    </Card>
                ))}
            </div>

            <Tabs defaultValue="accounts">
                <TabsList className="flex h-auto flex-wrap">
                    <TabsTrigger value="accounts">By account & period</TabsTrigger>
                    <TabsTrigger value="units">By department/unit</TabsTrigger>
                    <TabsTrigger value="validation">Validation</TabsTrigger>
                    <TabsTrigger value="comparison">Scenario comparison</TabsTrigger>
                </TabsList>

                <TabsContent value="accounts" className="space-y-4">
                    <Card>
                        <CardHeader>
                            <CardTitle>Compiled account budget</CardTitle>
                            <CardDescription>
                                Budget figures are compiled from {approvedOnly ? 'approved' : 'all'} returns.
                                Actuals are posted {view.bookClassification} GL lines for the same fiscal year.
                            </CardDescription>
                            <div className="flex flex-col gap-2 pt-2 md:flex-row">
                                <Input
                                    value={search}
                                    onChange={event => setSearch(event.target.value)}
                                    placeholder="Filter account code or name"
                                    className="md:max-w-sm"
                                />
                                <Select value={accountType} onValueChange={setAccountType}>
                                    <SelectTrigger className="md:w-48">
                                        <SelectValue />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="all">All account types</SelectItem>
                                        <SelectItem value="Revenue">Revenue</SelectItem>
                                        <SelectItem value="Expense">Expense</SelectItem>
                                    </SelectContent>
                                </Select>
                            </div>
                        </CardHeader>
                        <CardContent className="overflow-x-auto">
                            <Table>
                                <TableHeader>
                                    <TableRow>
                                        <TableHead className="min-w-64">Account</TableHead>
                                        {view.periods.map(period => (
                                            <TableHead key={period.fiscalPeriodId} className="min-w-28 text-right">
                                                {period.periodCode}
                                            </TableHead>
                                        ))}
                                        <TableHead className="text-right">Budget</TableHead>
                                        <TableHead className="text-right">Actual</TableHead>
                                        <TableHead className="text-right">Variance</TableHead>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {accountRows.length === 0 ? (
                                        <TableRow>
                                            <TableCell colSpan={view.periods.length + 4} className="py-10 text-center text-muted-foreground">
                                                No budget lines match the current view.
                                            </TableCell>
                                        </TableRow>
                                    ) : accountRows.map(row => (
                                        <TableRow key={row.accountId}>
                                            <TableCell>
                                                <p className="font-medium">{row.accountCode} — {row.accountName}</p>
                                                <div className="mt-1 flex items-center gap-2">
                                                    <Badge variant="outline">{row.accountType}</Badge>
                                                    {row.contributions.length > 0 && (
                                                        <details className="text-xs text-muted-foreground">
                                                            <summary className="cursor-pointer">
                                                                {row.contributions.length} contributing return(s)
                                                            </summary>
                                                            <div className="mt-2 space-y-1 rounded border bg-muted/30 p-2">
                                                                {row.contributions.map(item => (
                                                                    <p key={item.budgetReturnId}>
                                                                        {item.segmentCode} {item.segmentName}: {money(item.budgetAmount)}
                                                                    </p>
                                                                ))}
                                                            </div>
                                                        </details>
                                                    )}
                                                </div>
                                            </TableCell>
                                            {view.periods.map(period => (
                                                <TableCell key={period.fiscalPeriodId} className="text-right">
                                                    {money(row.periods[period.fiscalPeriodId] || 0)}
                                                </TableCell>
                                            ))}
                                            <TableCell className="font-medium text-right">{money(row.budget)}</TableCell>
                                            <TableCell className="text-right">{money(row.actual)}</TableCell>
                                            <TableCell className={`text-right font-medium ${
                                                row.variance > 0 && row.accountType === 'Expense'
                                                    ? 'text-red-600'
                                                    : row.variance !== 0 ? 'text-green-700' : ''
                                            }`}>
                                                {money(row.variance)}
                                            </TableCell>
                                        </TableRow>
                                    ))}
                                </TableBody>
                            </Table>
                        </CardContent>
                    </Card>
                </TabsContent>

                <TabsContent value="units">
                    <Card>
                        <CardHeader>
                            <CardTitle>Department and unit totals</CardTitle>
                            <CardDescription>Each total links back to its contributing worksheet.</CardDescription>
                        </CardHeader>
                        <CardContent>
                            <Table>
                                <TableHeader>
                                    <TableRow>
                                        <TableHead>Department / unit</TableHead>
                                        <TableHead>Assigned to</TableHead>
                                        <TableHead>Status</TableHead>
                                        <TableHead className="text-right">Budget total</TableHead>
                                        <TableHead />
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {view.units.map(unit => (
                                        <TableRow key={unit.budgetReturnId}>
                                            <TableCell className="font-medium">
                                                {unit.segmentCode} {unit.segmentName}
                                            </TableCell>
                                            <TableCell>{unit.assignedToUserName || 'Unassigned'}</TableCell>
                                            <TableCell><Badge variant="outline">{unit.returnStatus}</Badge></TableCell>
                                            <TableCell className="text-right">{money(unit.budgetAmount)}</TableCell>
                                            <TableCell className="text-right">
                                                <Link href={`/finance/budgeting/returns/${unit.budgetReturnId}`}>
                                                    <Button size="sm" variant="outline">Open worksheet</Button>
                                                </Link>
                                            </TableCell>
                                        </TableRow>
                                    ))}
                                </TableBody>
                            </Table>
                        </CardContent>
                    </Card>
                </TabsContent>

                <TabsContent value="validation">
                    <Card>
                        <CardHeader>
                            <CardTitle>Pre-submission validation</CardTitle>
                            <CardDescription>
                                Resolve errors before submitting the scenario; warnings identify governance gaps.
                            </CardDescription>
                        </CardHeader>
                        <CardContent className="space-y-3">
                            {view.validationIssues.length === 0 ? (
                                <div className="flex items-center gap-2 text-green-700">
                                    <CheckCircle2 className="h-5 w-5" />
                                    No validation issues found.
                                </div>
                            ) : view.validationIssues.map((issue, index) => (
                                <div key={`${issue.code}-${issue.returnId || index}`} className="flex items-start justify-between gap-4 rounded border p-3">
                                    <div className="flex items-start gap-2">
                                        <AlertTriangle className={`mt-0.5 h-4 w-4 ${
                                            issue.severity === 'Error' ? 'text-red-600' : 'text-amber-600'
                                        }`} />
                                        <div>
                                            <p className="font-medium">{issue.message}</p>
                                            <p className="text-xs text-muted-foreground">{issue.code}</p>
                                        </div>
                                    </div>
                                    {issue.returnId && (
                                        <Link href={`/finance/budgeting/returns/${issue.returnId}`}>
                                            <Button size="sm" variant="outline">Review</Button>
                                        </Link>
                                    )}
                                </div>
                            ))}
                        </CardContent>
                    </Card>
                </TabsContent>

                <TabsContent value="comparison" className="space-y-4">
                    <Card>
                        <CardHeader>
                            <CardTitle className="flex items-center gap-2">
                                <Scale className="h-5 w-5" />
                                Compare approved alternatives and revisions
                            </CardTitle>
                            <CardDescription>
                                Comparison uses approved returns and requires both scenarios to belong to the same fiscal year.
                            </CardDescription>
                        </CardHeader>
                        <CardContent className="space-y-4">
                            <div className="flex flex-col gap-2 md:flex-row">
                                <Select value={comparisonTargetId} onValueChange={setComparisonTargetId}>
                                    <SelectTrigger className="md:max-w-md">
                                        <SelectValue placeholder="Select scenario to compare" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        {candidates.map(item => (
                                            <SelectItem key={item.id} value={item.id}>
                                                {item.name}{item.isActive ? ' (Official)' : ` (${item.status})`}
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                <Button onClick={runComparison} disabled={!comparisonTargetId || isComparing}>
                                    {isComparing && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                                    Compare
                                </Button>
                                {canExport && comparison && (
                                    <ReportPdfActions
                                        reportName="budget scenario comparison"
                                        onDownloadPdf={downloadComparisonPdf}
                                        onPrint={printComparison}
                                    />
                                )}
                            </div>

                            {candidates.length === 0 && (
                                <p className="text-sm text-muted-foreground">
                                    No other approved or superseded scenarios are available for this fiscal year.
                                </p>
                            )}

                            {comparison && (
                                <>
                                    <div className="grid gap-4 md:grid-cols-3">
                                        <Card>
                                            <CardHeader className="pb-2">
                                                <CardDescription>{comparison.baseScenarioName}</CardDescription>
                                                <CardTitle>{money(comparison.baseTotal)}</CardTitle>
                                            </CardHeader>
                                        </Card>
                                        <Card>
                                            <CardHeader className="pb-2">
                                                <CardDescription>{comparison.comparisonScenarioName}</CardDescription>
                                                <CardTitle>{money(comparison.comparisonTotal)}</CardTitle>
                                            </CardHeader>
                                        </Card>
                                        <Card>
                                            <CardHeader className="pb-2">
                                                <CardDescription>Difference</CardDescription>
                                                <CardTitle>{money(comparison.differenceTotal)}</CardTitle>
                                            </CardHeader>
                                        </Card>
                                    </div>
                                    <div className="overflow-x-auto">
                                        <Table>
                                            <TableHeader>
                                                <TableRow>
                                                    <TableHead>Account</TableHead>
                                                    <TableHead className="text-right">Base scenario</TableHead>
                                                    <TableHead className="text-right">Compared scenario</TableHead>
                                                    <TableHead className="text-right">Difference</TableHead>
                                                </TableRow>
                                            </TableHeader>
                                            <TableBody>
                                                {comparisonRows.map(row => (
                                                    <TableRow key={`${row.accountCode}-${row.accountName}`}>
                                                        <TableCell>
                                                            <p className="font-medium">{row.accountCode} — {row.accountName}</p>
                                                            <Badge variant="outline">{row.accountType}</Badge>
                                                        </TableCell>
                                                        <TableCell className="text-right">{money(row.base)}</TableCell>
                                                        <TableCell className="text-right">{money(row.compared)}</TableCell>
                                                        <TableCell className="font-medium text-right">{money(row.difference)}</TableCell>
                                                    </TableRow>
                                                ))}
                                            </TableBody>
                                        </Table>
                                    </div>
                                </>
                            )}
                        </CardContent>
                    </Card>
                </TabsContent>
            </Tabs>
        </div>
    );
}
