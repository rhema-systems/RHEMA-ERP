'use client';

import { useCallback, useEffect, useState } from 'react';
import { AlertTriangle, CheckCircle2, RefreshCw, ShieldCheck } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useToast } from '@/components/ui/use-toast';
import { budgetDataService } from '@/services/finance/budget-data.service';
import type { FinanceBudgetReconciliationReport } from '@/types/budget';

export default function BudgetReconciliationPage() {
    const { toast } = useToast();
    const [report, setReport] = useState<FinanceBudgetReconciliationReport>();
    const [loading, setLoading] = useState(true);

    const load = useCallback(async () => {
        try {
            setLoading(true);
            setReport(await budgetDataService.getBudgetReconciliation());
        } catch (error) {
            console.error('Failed to load budget reconciliation', error);
            toast({ title: 'Unable to run budget reconciliation', variant: 'destructive' });
        } finally {
            setLoading(false);
        }
    }, [toast]);

    useEffect(() => { void load(); }, [load]);

    return (
        <div className="space-y-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                    <h1 className="flex items-center gap-2 text-3xl font-bold tracking-tight">
                        <ShieldCheck className="h-8 w-8" /> Budget Reconciliation
                    </h1>
                    <p className="text-muted-foreground">
                        Read-only diagnostics for reservations, postings, reversals, source documents, and the primary ledger.
                    </p>
                </div>
                <Button variant="outline" onClick={() => void load()} disabled={loading}>
                    <RefreshCw className={`mr-2 h-4 w-4 ${loading ? 'animate-spin' : ''}`} /> Run check
                </Button>
            </div>

            {report && (
                <>
                    <Card className={report.isReconciled ? 'border-green-500/40' : 'border-destructive/50'}>
                        <CardContent className="flex flex-wrap items-center justify-between gap-4 pt-6">
                            <div className="flex items-start gap-3">
                                {report.isReconciled
                                    ? <CheckCircle2 className="mt-0.5 h-6 w-6 text-green-600" />
                                    : <AlertTriangle className="mt-0.5 h-6 w-6 text-destructive" />}
                                <div>
                                    <p className="font-semibold">
                                        {report.isReconciled ? 'Budget evidence is reconciled' : 'Budget evidence requires attention'}
                                    </p>
                                    <p className="text-sm text-muted-foreground">
                                        Primary book: {report.primaryAccountingBookCode ?? 'Not configured'} · Checked {new Date(report.generatedAtUtc).toLocaleString()}
                                    </p>
                                </div>
                            </div>
                            <div className="flex gap-2">
                                <Badge variant={report.errorCount > 0 ? 'destructive' : 'secondary'}>{report.errorCount} errors</Badge>
                                <Badge variant="outline">{report.warningCount} warnings</Badge>
                            </div>
                        </CardContent>
                    </Card>

                    <div className="grid gap-4 md:grid-cols-4">
                        <Card><CardHeader className="pb-2"><CardDescription>All reservations</CardDescription><CardTitle>{report.reservationCount}</CardTitle></CardHeader></Card>
                        <Card><CardHeader className="pb-2"><CardDescription>Active</CardDescription><CardTitle>{report.activeReservationCount}</CardTitle></CardHeader></Card>
                        <Card><CardHeader className="pb-2"><CardDescription>Consumed</CardDescription><CardTitle>{report.consumedReservationCount}</CardTitle></CardHeader></Card>
                        <Card><CardHeader className="pb-2"><CardDescription>Released</CardDescription><CardTitle>{report.releasedReservationCount}</CardTitle></CardHeader></Card>
                    </div>

                    <Card>
                        <CardHeader>
                            <CardTitle>Exceptions</CardTitle>
                            <CardDescription>No data is changed by this report.</CardDescription>
                        </CardHeader>
                        <CardContent>
                            {report.issues.length === 0 ? (
                                <p className="text-sm text-muted-foreground">No exceptions were found.</p>
                            ) : (
                                <Table>
                                    <TableHeader><TableRow><TableHead>Severity</TableHead><TableHead>Exception</TableHead><TableHead>Source</TableHead><TableHead>Recommended action</TableHead></TableRow></TableHeader>
                                    <TableBody>{report.issues.map((issue, index) => (
                                        <TableRow key={`${issue.code}-${issue.reservationId ?? issue.sourceDocumentId ?? index}`}>
                                            <TableCell><Badge variant={issue.severity === 'Error' ? 'destructive' : 'outline'}>{issue.severity}</Badge></TableCell>
                                            <TableCell><p className="font-medium">{issue.code}</p><p className="max-w-xl text-sm text-muted-foreground">{issue.message}</p></TableCell>
                                            <TableCell className="font-mono text-xs">{issue.sourceDocumentType ?? '—'}<br />{issue.sourceDocumentId ?? issue.reservationId ?? '—'}</TableCell>
                                            <TableCell className="max-w-md text-sm">{issue.recommendedAction}</TableCell>
                                        </TableRow>
                                    ))}</TableBody>
                                </Table>
                            )}
                        </CardContent>
                    </Card>
                </>
            )}
        </div>
    );
}
