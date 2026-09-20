'use client';

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import {
    ArrowLeft,
    ArrowRight,
    CheckCircle2,
    ChevronRight,
    ExternalLink,
    Home,
    Send,
    XCircle,
} from 'lucide-react';
import { toast } from 'sonner';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
} from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { unitAccountsDataService } from '@/services/finance/unit-accounts-data.service';
import type { AllocationRunBatch, AllocationRunBatchStatus } from '@/types/unit-accounts';
import { useWorkflowSummary } from '@/hooks/useWorkflowSummary';
import { useAuth } from '@/hooks/use-auth';

function formatDate(value?: string) {
    return value ? new Date(value).toLocaleDateString() : '-';
}

function formatMoney(currency: string, amount: number) {
    return `${currency} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

function statusBadge(status: AllocationRunBatchStatus) {
    switch (status) {
        case 'ReadyToPost':
            return <Badge className="bg-blue-100 text-blue-800">Ready to post</Badge>;
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

export default function AllocationRunBatchDetailPage() {
    const router = useRouter();
    const params = useParams();
    const batchId = params.id as string;
    const { hasPermission } = useAuth();
    const canManage = hasPermission('Finance.Admin');
    const workflow = useWorkflowSummary({ entityType: 'AllocationRunBatch', entityId: batchId });
    const [batch, setBatch] = useState<AllocationRunBatch | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [isWorking, setIsWorking] = useState(false);
    const [comment, setComment] = useState('');
    const [rejectionReason, setRejectionReason] = useState('');

    const loadBatch = useCallback(async () => {
        try {
            setIsLoading(true);
            setBatch(await unitAccountsDataService.getAllocationRunBatchById(batchId));
        } catch (error: any) {
            toast.error(error?.message || 'Failed to load allocation run batch.');
        } finally {
            setIsLoading(false);
        }
    }, [batchId]);

    useEffect(() => {
        loadBatch();
    }, [loadBatch]);

    const totalFromLines = useMemo(
        () => batch?.lines.reduce((sum, line) => sum + line.allocatedAmount, 0) ?? 0,
        [batch]
    );

    const runAction = async (action: 'submit' | 'approve' | 'reject' | 'post') => {
        if (!batch) return;

        if (action === 'reject' && !rejectionReason.trim()) {
            toast.error('Enter a rejection reason.');
            return;
        }

        try {
            setIsWorking(true);
            let updated: AllocationRunBatch;
            if (action === 'submit') {
                updated = await unitAccountsDataService.submitAllocationRunBatch(batch.id, comment.trim() || undefined);
                toast.success(updated.approvalRequired === false ? 'Allocation is ready to post.' : 'Allocation run batch submitted for approval.');
            } else if (action === 'approve') {
                updated = await unitAccountsDataService.approveAllocationRunBatch(batch.id, comment.trim() || undefined);
                toast.success(updated.status === 'Approved' ? 'Allocation run batch approved.' : 'Approval recorded.');
            } else if (action === 'reject') {
                updated = await unitAccountsDataService.rejectAllocationRunBatch(batch.id, rejectionReason.trim());
                toast.success('Allocation run batch rejected.');
            } else {
                updated = await unitAccountsDataService.postAllocationRunBatch(batch.id);
                toast.success(`Allocation run batch posted as ${updated.journalEntryNumber}.`);
            }

            setBatch(updated);
            await workflow.refresh();
            setComment('');
            setRejectionReason('');
        } catch (error: any) {
            toast.error(error?.message || 'Allocation run batch action failed.');
        } finally {
            setIsWorking(false);
        }
    };

    if (isLoading) {
        return (
            <Card>
                <CardContent className="py-12 text-center text-muted-foreground">
                    Loading allocation run batch...
                </CardContent>
            </Card>
        );
    }

    if (!batch) {
        return (
            <Card>
                <CardContent className="py-12 text-center">
                    <p className="text-muted-foreground">Allocation run batch not found.</p>
                    <Button asChild variant="outline" className="mt-4">
                        <Link href="/finance/allocations">Back to Allocations</Link>
                    </Button>
                </CardContent>
            </Card>
        );
    }

    const canReview = canManage && batch.status === 'PendingApproval' && workflow.visibility.showApprovalControls &&
        workflow.summary?.canCurrentUserApprove === true;
    const canPost = canManage && (batch.approvalRequired === false ? batch.status === 'ReadyToPost' : batch.status === 'Approved');

    return (
        <div className="space-y-6">
            <nav className="flex items-center space-x-2 text-sm text-muted-foreground">
                <Link href="/" className="flex items-center hover:text-foreground">
                    <Home className="h-4 w-4" />
                </Link>
                <ChevronRight className="h-4 w-4" />
                <Link href="/finance" className="hover:text-foreground">Finance</Link>
                <ChevronRight className="h-4 w-4" />
                <Link href="/finance/allocations" className="hover:text-foreground">Allocations</Link>
                <ChevronRight className="h-4 w-4" />
                <span className="text-foreground">{batch.batchNumber}</span>
            </nav>

            <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                    <div className="flex items-center gap-3">
                        <h1 className="text-3xl font-bold tracking-tight">{batch.batchNumber}</h1>
                        {statusBadge(batch.status)}
                    </div>
                    <p className="text-muted-foreground">
                        {batch.ruleCode} - {batch.ruleName}
                    </p>
                </div>
                <Button variant="outline" onClick={() => router.push('/finance/allocations')}>
                    <ArrowLeft className="mr-2 h-4 w-4" />
                    Back
                </Button>
            </div>

            {batch.rejectionReason && (
                <Alert variant="destructive">
                    <XCircle className="h-4 w-4" />
                    <AlertDescription>{batch.rejectionReason}</AlertDescription>
                </Alert>
            )}

            <div className="grid gap-6 lg:grid-cols-[1fr_360px]">
                <div className="space-y-6">
                    <Card>
                        <CardHeader>
                            <CardTitle>Run Snapshot</CardTitle>
                            <CardDescription>
                                Stored calculation reviewed before GL posting
                            </CardDescription>
                        </CardHeader>
                        <CardContent className="grid gap-4 md:grid-cols-3">
                            <div>
                                <p className="text-sm text-muted-foreground">Period</p>
                                <p className="font-medium">{batch.periodCode} - {batch.periodName}</p>
                            </div>
                            <div>
                                <p className="text-sm text-muted-foreground">Allocation Date</p>
                                <p className="font-medium">{formatDate(batch.allocationDate)}</p>
                            </div>
                            <div>
                                <p className="text-sm text-muted-foreground">Book</p>
                                <p className="font-medium">{batch.bookClassification}</p>
                            </div>
                            <div>
                                <p className="text-sm text-muted-foreground">Source Account</p>
                                <p className="font-medium">{batch.sourceAccountNumber} - {batch.sourceAccountName}</p>
                            </div>
                            <div>
                                <p className="text-sm text-muted-foreground">Source Period Balance</p>
                                <p className="font-medium">{formatMoney(batch.functionalCurrencyCode, batch.sourcePeriodBalance)}</p>
                            </div>
                            <div>
                                <p className="text-sm text-muted-foreground">Total Allocated</p>
                                <p className="font-medium">{formatMoney(batch.functionalCurrencyCode, batch.totalAllocated)}</p>
                            </div>
                            <div className="md:col-span-3">
                                <p className="text-sm text-muted-foreground">Description</p>
                                <p className="font-medium">{batch.description || '-'}</p>
                            </div>
                        </CardContent>
                    </Card>

                    <Card>
                        <CardHeader>
                            <CardTitle>Allocation Lines</CardTitle>
                            <CardDescription>
                                {batch.lines.length} target line(s), total {formatMoney(batch.functionalCurrencyCode, totalFromLines)}
                            </CardDescription>
                        </CardHeader>
                        <CardContent>
                            <div className="rounded-md border">
                                <Table>
                                    <TableHeader>
                                        <TableRow>
                                            <TableHead>Target Account</TableHead>
                                            <TableHead>Driver</TableHead>
                                            <TableHead className="text-right">Basis</TableHead>
                                            <TableHead className="text-right">Percent</TableHead>
                                            <TableHead className="text-right">Amount</TableHead>
                                        </TableRow>
                                    </TableHeader>
                                    <TableBody>
                                        {batch.lines.map((line) => (
                                            <TableRow key={line.id}>
                                                <TableCell>
                                                    <div className="font-mono text-sm">{line.targetAccountNumber}</div>
                                                    <div className="text-sm text-muted-foreground">{line.targetAccountName}</div>
                                                </TableCell>
                                                <TableCell>
                                                    {line.targetDriverUnitAccountNumber ? (
                                                        <>
                                                            <div className="font-mono text-sm">{line.targetDriverUnitAccountNumber}</div>
                                                            <div className="text-sm text-muted-foreground">{line.targetDriverUnitAccountName}</div>
                                                        </>
                                                    ) : (
                                                        <span className="text-muted-foreground">-</span>
                                                    )}
                                                </TableCell>
                                                <TableCell className="text-right">{line.allocationBasis.toLocaleString()}</TableCell>
                                                <TableCell className="text-right">{line.allocationPercent.toFixed(2)}%</TableCell>
                                                <TableCell className="text-right">
                                                    {formatMoney(batch.functionalCurrencyCode, line.allocatedAmount)}
                                                </TableCell>
                                            </TableRow>
                                        ))}
                                    </TableBody>
                                </Table>
                            </div>
                        </CardContent>
                    </Card>
                </div>

                <Card className="h-fit">
                    <CardHeader>
                        <CardTitle>Actions</CardTitle>
                        <CardDescription>
                            Finalize the saved calculation, then post it to the ledger.
                        </CardDescription>
                    </CardHeader>
                    <CardContent className="space-y-4">
                        {workflow.error && <Alert variant="destructive"><AlertDescription>{workflow.error} <Button variant="link" onClick={() => void workflow.refresh()}>Retry</Button></AlertDescription></Alert>}
                        <div className="space-y-2">
                            <Label htmlFor="comment">Comments (optional)</Label>
                            <Textarea
                                id="comment"
                                value={comment}
                                onChange={(event) => setComment(event.target.value)}
                                placeholder="Optional"
                                rows={3}
                                disabled={isWorking || batch.status === 'Posted' || batch.status === 'Rejected'}
                            />
                        </div>

                        {canReview && (
                            <div className="space-y-2">
                                <Label htmlFor="rejectionReason">Rejection Reason</Label>
                                <Input
                                    id="rejectionReason"
                                    value={rejectionReason}
                                    onChange={(event) => setRejectionReason(event.target.value)}
                                    placeholder="Required before rejecting"
                                    disabled={isWorking}
                                />
                            </div>
                        )}

                        <div className="space-y-2">
                            {batch.status === 'Draft' && canManage && (
                                <Button className="w-full" onClick={() => runAction('submit')} disabled={isWorking || !workflow.visibility.known}>
                                    <Send className="mr-2 h-4 w-4" />
                                    {!workflow.visibility.known ? 'Checking approval status…' : workflow.visibility.direct ? 'Finalize' : 'Submit for Approval'}
                                </Button>
                            )}
                            {canReview && (
                                <>
                                    <Button className="w-full" onClick={() => runAction('approve')} disabled={isWorking}>
                                        <CheckCircle2 className="mr-2 h-4 w-4" />
                                        Approve Step
                                    </Button>
                                    <Button
                                        className="w-full"
                                        variant="destructive"
                                        onClick={() => runAction('reject')}
                                        disabled={isWorking}
                                    >
                                        <XCircle className="mr-2 h-4 w-4" />
                                        Reject
                                    </Button>
                                </>
                            )}
                            {canPost && (
                                <Button className="w-full" onClick={() => runAction('post')} disabled={isWorking}>
                                    <ArrowRight className="mr-2 h-4 w-4" />
                                    Post
                                </Button>
                            )}
                            {batch.status === 'Posted' && batch.journalEntryId && (
                                <Button asChild className="w-full" variant="outline">
                                    <Link href={`/finance/journal-entries/${batch.journalEntryId}`}>
                                        <ExternalLink className="mr-2 h-4 w-4" />
                                        View Journal
                                    </Link>
                                </Button>
                            )}
                        </div>
                    </CardContent>
                </Card>
            </div>
        </div>
    );
}
