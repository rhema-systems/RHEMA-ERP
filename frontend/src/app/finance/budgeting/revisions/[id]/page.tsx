'use client';

import { useCallback, useEffect, useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { ArrowLeft, CheckCircle2, RefreshCw, Send, ShieldCheck } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/components/ui/use-toast';
import { budgetDataService } from '@/services/finance/budget-data.service';
import type { BudgetRevision } from '@/types/budget';

const money = (value: number) => new Intl.NumberFormat('en-GH', {
    style: 'currency',
    currency: 'GHS',
}).format(value);

export default function BudgetRevisionDetailPage() {
    const { id } = useParams<{ id: string }>();
    const { hasPermission } = useAuth();
    const { toast } = useToast();
    const [revision, setRevision] = useState<BudgetRevision | null>(null);
    const [busy, setBusy] = useState(false);
    const canSubmit = hasPermission('Finance.BudgetRevisions.Submit');
    const canApply = hasPermission('Finance.BudgetRevisions.Apply');

    const load = useCallback(async () => {
        try {
            setRevision(await budgetDataService.getRevision(id));
        } catch (error) {
            console.error('Failed to load budget revision', error);
            toast({ title: 'Unable to load budget revision', variant: 'destructive' });
        }
    }, [id, toast]);

    useEffect(() => { void load(); }, [load]);

    const runAction = async (action: 'submit' | 'apply') => {
        if (!revision) return;
        try {
            setBusy(true);
            const updated = action === 'submit'
                ? await budgetDataService.submitRevision(revision.id, revision.rowVersion)
                : await budgetDataService.applyRevision(revision.id, revision.rowVersion);
            setRevision(updated);
            toast({
                title: action === 'submit' ? 'Revision submitted for Board approval' : 'Official budget successor adopted',
            });
        } catch (error) {
            console.error(`Failed to ${action} budget revision`, error);
            toast({
                title: `Unable to ${action} budget revision`,
                description: error instanceof Error ? error.message : 'Refresh the record and try again.',
                variant: 'destructive',
            });
        } finally {
            setBusy(false);
        }
    };

    if (!revision) return <div className="p-8 text-muted-foreground">Loading budget revision...</div>;

    return (
        <div className="space-y-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="flex items-start gap-3">
                    <Button variant="outline" size="icon" asChild><Link href="/finance/budgeting/revisions"><ArrowLeft className="h-4 w-4" /></Link></Button>
                    <div>
                        <div className="flex flex-wrap items-center gap-2">
                            <h1 className="text-3xl font-bold tracking-tight">{revision.revisionNumber}</h1>
                            <Badge variant="outline">{revision.revisionType}</Badge>
                            <Badge variant={revision.status === 'Rejected' ? 'destructive' : revision.status === 'Applied' ? 'default' : 'secondary'}>{revision.status}</Badge>
                        </div>
                        <p className="text-muted-foreground">{revision.sourceScenarioName} / {revision.fiscalYearName}</p>
                    </div>
                </div>
                <div className="flex gap-2">
                    <Button variant="outline" onClick={() => void load()} disabled={busy}><RefreshCw className="mr-2 h-4 w-4" /> Refresh</Button>
                    {canSubmit && (revision.status === 'Draft' || revision.status === 'Rejected') && (
                        <Button onClick={() => void runAction('submit')} disabled={busy}><Send className="mr-2 h-4 w-4" /> Submit for approval</Button>
                    )}
                    {canApply && revision.status === 'Approved' && (
                        <Button onClick={() => void runAction('apply')} disabled={busy}><ShieldCheck className="mr-2 h-4 w-4" /> Apply approved revision</Button>
                    )}
                </div>
            </div>

            {revision.status === 'Submitted' && (
                <Card className="border-blue-300 bg-blue-50/50 dark:bg-blue-950/20">
                    <CardContent className="flex items-start gap-3 pt-6"><ShieldCheck className="mt-0.5 h-5 w-5 text-blue-600" /><div><p className="font-medium">In the shared Finance approval inbox</p><p className="text-sm text-muted-foreground">Finance Manager review is followed by Managing Director final authority. No official budget balance changes until Apply is performed after approval.</p></div></CardContent>
                </Card>
            )}
            {revision.status === 'Applied' && revision.resultScenarioId && (
                <Card className="border-green-300 bg-green-50/50 dark:bg-green-950/20">
                    <CardContent className="flex flex-wrap items-center justify-between gap-3 pt-6"><div className="flex items-start gap-3"><CheckCircle2 className="mt-0.5 h-5 w-5 text-green-600" /><div><p className="font-medium">Immutable successor is now the official budget</p><p className="text-sm text-muted-foreground">The former official scenario remains Superseded for audit and comparison.</p></div></div><Button variant="outline" asChild><Link href={`/finance/budgeting/scenarios/${revision.resultScenarioId}`}>Open resulting version</Link></Button></CardContent>
                </Card>
            )}

            <div className="grid gap-4 md:grid-cols-3">
                <Card><CardHeader className="pb-2"><CardDescription>Budget increases</CardDescription><CardTitle>{money(revision.increaseAmountBase)}</CardTitle></CardHeader></Card>
                <Card><CardHeader className="pb-2"><CardDescription>Budget releases</CardDescription><CardTitle>{money(revision.reductionAmountBase)}</CardTitle></CardHeader></Card>
                <Card><CardHeader className="pb-2"><CardDescription>Net official change</CardDescription><CardTitle>{money(revision.netChangeAmountBase)}</CardTitle></CardHeader></Card>
            </div>

            <Card>
                <CardHeader><CardTitle>Governance evidence</CardTitle><CardDescription>The authority recorded before workflow submission.</CardDescription></CardHeader>
                <CardContent className="grid gap-4 md:grid-cols-3">
                    <div><p className="text-sm text-muted-foreground">Board resolution</p><p className="font-medium">{revision.boardResolutionReference}</p></div>
                    <div><p className="text-sm text-muted-foreground">Resolution date</p><p className="font-medium">{new Date(revision.boardResolutionDate).toLocaleDateString()}</p></div>
                    <div><p className="text-sm text-muted-foreground">Effective date</p><p className="font-medium">{new Date(revision.effectiveDate).toLocaleDateString()}</p></div>
                    <div className="md:col-span-3"><p className="text-sm text-muted-foreground">Justification</p><p>{revision.justification}</p></div>
                    {revision.rejectionReason && <div className="md:col-span-3"><p className="text-sm text-destructive">Rejection reason</p><p>{revision.rejectionReason}</p></div>}
                </CardContent>
            </Card>

            <Card>
                <CardHeader><CardTitle>Authorized budget-cell changes</CardTitle><CardDescription>Current and revised values are shown in the official scenario base currency.</CardDescription></CardHeader>
                <CardContent className="overflow-x-auto">
                    <table className="w-full min-w-[900px] text-sm">
                        <thead><tr className="border-b text-left text-muted-foreground"><th className="py-3 pr-4">Cost centre</th><th className="py-3 pr-4">Account</th><th className="py-3 pr-4">Period</th><th className="py-3 pr-4 text-right">Current</th><th className="py-3 pr-4 text-right">Adjustment</th><th className="py-3 text-right">Revised</th></tr></thead>
                        <tbody>{revision.lines.map(line => (
                            <tr key={line.id} className="border-b last:border-0"><td className="py-3 pr-4">{line.segmentCode ? `${line.segmentCode} - ${line.segmentName}` : 'General'}</td><td className="py-3 pr-4">{line.accountCode} - {line.accountName}</td><td className="py-3 pr-4">{line.periodCode}</td><td className="py-3 pr-4 text-right">{money(line.currentAmountBase)}</td><td className={`py-3 pr-4 text-right font-medium ${line.adjustmentAmountBase < 0 ? 'text-amber-700' : 'text-green-700'}`}>{money(line.adjustmentAmountBase)}</td><td className="py-3 text-right font-semibold">{money(line.revisedAmountBase)}</td></tr>
                        ))}</tbody>
                    </table>
                </CardContent>
            </Card>
        </div>
    );
}
