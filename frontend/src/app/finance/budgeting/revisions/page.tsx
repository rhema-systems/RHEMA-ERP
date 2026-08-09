'use client';

import { useCallback, useEffect, useState } from 'react';
import Link from 'next/link';
import { ArrowRight, Plus, RefreshCw, Scale } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
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

export default function BudgetRevisionsPage() {
    const { hasPermission } = useAuth();
    const { toast } = useToast();
    const [revisions, setRevisions] = useState<BudgetRevision[]>([]);
    const [loading, setLoading] = useState(true);
    const canCreate = hasPermission('Finance.BudgetRevisions.Write');

    const load = useCallback(async () => {
        try {
            setLoading(true);
            setRevisions(await budgetDataService.getRevisions());
        } catch (error) {
            console.error('Failed to load budget revisions', error);
            toast({ title: 'Unable to load budget revisions', variant: 'destructive' });
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
                        <Scale className="h-8 w-8" /> Budget Revisions
                    </h1>
                    <p className="text-muted-foreground">
                        Board-governed virements and supplementary budgets with immutable version history.
                    </p>
                </div>
                <div className="flex gap-2">
                    <Button variant="outline" size="icon" onClick={() => void load()} disabled={loading}>
                        <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
                    </Button>
                    {canCreate && (
                        <Button asChild>
                            <Link href="/finance/budgeting/revisions/new">
                                <Plus className="mr-2 h-4 w-4" /> New Revision
                            </Link>
                        </Button>
                    )}
                </div>
            </div>

            <Breadcrumb>
                <BreadcrumbList>
                    <BreadcrumbItem><BreadcrumbLink href="/finance">Finance</BreadcrumbLink></BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbLink href="/finance/budgeting/scenarios">Budgeting</BreadcrumbLink></BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbPage>Budget Revisions</BreadcrumbPage></BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            <div className="grid gap-4 md:grid-cols-3">
                <Card>
                    <CardHeader className="pb-2"><CardDescription>Requests</CardDescription><CardTitle>{revisions.length}</CardTitle></CardHeader>
                </Card>
                <Card>
                    <CardHeader className="pb-2"><CardDescription>Awaiting approval</CardDescription><CardTitle>{revisions.filter(item => item.status === 'Submitted').length}</CardTitle></CardHeader>
                </Card>
                <Card>
                    <CardHeader className="pb-2"><CardDescription>Applied versions</CardDescription><CardTitle>{revisions.filter(item => item.status === 'Applied').length}</CardTitle></CardHeader>
                </Card>
            </div>

            <Card>
                <CardHeader>
                    <CardTitle>Revision register</CardTitle>
                    <CardDescription>
                        Approval authorizes a request; Apply creates the revised official budget as a separate version.
                    </CardDescription>
                </CardHeader>
                <CardContent className="space-y-3">
                    {!loading && revisions.length === 0 && (
                        <div className="rounded-md border border-dashed p-8 text-center text-muted-foreground">
                            No controlled budget revisions have been raised.
                        </div>
                    )}
                    {revisions.map(revision => (
                        <div key={revision.id} className="flex flex-wrap items-center justify-between gap-4 rounded-lg border p-4">
                            <div className="space-y-1">
                                <div className="flex flex-wrap items-center gap-2">
                                    <span className="font-semibold">{revision.revisionNumber}</span>
                                    <Badge variant="outline">{revision.revisionType}</Badge>
                                    <Badge variant={revision.status === 'Rejected' ? 'destructive' : revision.status === 'Applied' ? 'default' : 'secondary'}>
                                        {revision.status}
                                    </Badge>
                                </div>
                                <p className="text-sm">{revision.sourceScenarioName} / {revision.fiscalYearName}</p>
                                <p className="text-sm text-muted-foreground">
                                    Board resolution {revision.boardResolutionReference} / Effective {new Date(revision.effectiveDate).toLocaleDateString()}
                                </p>
                            </div>
                            <div className="flex items-center gap-5">
                                <div className="text-right text-sm">
                                    <p className="text-muted-foreground">Net approved change</p>
                                    <p className="font-semibold">{money(revision.netChangeAmountBase)}</p>
                                </div>
                                <Button variant="outline" asChild>
                                    <Link href={`/finance/budgeting/revisions/${revision.id}`}>
                                        Review <ArrowRight className="ml-2 h-4 w-4" />
                                    </Link>
                                </Button>
                            </div>
                        </div>
                    ))}
                </CardContent>
            </Card>
        </div>
    );
}
