'use client';

import React, { useCallback, useEffect, useState } from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import {
    Calendar,
    CheckCircle,
    Clock,
    FileSpreadsheet,
    RotateCcw,
    Send,
    Trash2,
    User,
    XCircle,
} from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from '@/components/ui/table';
import { unitAccountsDataService } from '@/services/finance/unit-accounts-data.service';
import type { UnitJournalEntry, UnitJournalEntryLine, UnitJournalEntryStatus } from '@/types/unit-accounts';

function getStatusBadge(status: UnitJournalEntryStatus) {
    const styles: Record<UnitJournalEntryStatus, string> = {
        Draft: 'bg-gray-500',
        PendingApproval: 'bg-orange-500',
        Approved: 'bg-blue-500',
        Posted: 'bg-green-600',
        Rejected: 'bg-red-500',
        Reversed: 'bg-purple-500',
    };
    const labels: Record<UnitJournalEntryStatus, string> = {
        Draft: 'Draft',
        PendingApproval: 'Pending Approval',
        Approved: 'Approved',
        Posted: 'Posted',
        Rejected: 'Rejected',
        Reversed: 'Reversed',
    };
    return <Badge className={styles[status] ?? 'bg-gray-500'}>{labels[status] ?? status}</Badge>;
}

function formatDate(dateString?: string) {
    if (!dateString) return '-';
    return new Date(dateString).toLocaleDateString('en-GB', {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
    });
}

function formatDateTime(dateString?: string) {
    if (!dateString) return '-';
    return new Date(dateString).toLocaleString('en-GB', {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
    });
}

function totalQuantity(lines: UnitJournalEntryLine[] = []) {
    return lines.reduce((sum, line) => sum + line.quantity, 0);
}

export default function UnitJournalEntryDetailPage() {
    const params = useParams();
    const router = useRouter();
    const id = params.id as string;

    const [entry, setEntry] = useState<UnitJournalEntry | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [isWorking, setIsWorking] = useState(false);

    const loadEntry = useCallback(async () => {
        try {
            setIsLoading(true);
            const data = await unitAccountsDataService.getUnitJournalEntryById(id);
            setEntry(data);
        } catch (error: any) {
            toast.error(error?.message || 'Failed to load unit journal entry.');
            setEntry(null);
        } finally {
            setIsLoading(false);
        }
    }, [id]);

    useEffect(() => {
        loadEntry();
    }, [loadEntry]);

    const runAction = async (action: () => Promise<UnitJournalEntry>, successMessage: string) => {
        try {
            setIsWorking(true);
            const updated = await action();
            setEntry(updated);
            toast.success(successMessage);
        } catch (error: any) {
            toast.error(error?.message || 'Action failed.');
        } finally {
            setIsWorking(false);
        }
    };

    const handleSubmit = () => runAction(
        () => unitAccountsDataService.submitUnitJournalEntry(id),
        'Unit journal entry submitted for approval.'
    );

    const handleApprove = () => runAction(
        () => unitAccountsDataService.approveUnitJournalEntry(id),
        'Unit journal entry approved.'
    );

    const handlePost = () => runAction(
        () => unitAccountsDataService.postUnitJournalEntry(id),
        'Unit journal entry posted.'
    );

    const handleReject = async () => {
        const reason = window.prompt('Enter rejection reason:');
        if (!reason?.trim()) return;
        await runAction(
            () => unitAccountsDataService.rejectUnitJournalEntry(id, reason.trim()),
            'Unit journal entry rejected.'
        );
    };

    const handleReverse = async () => {
        const reason = window.prompt('Enter reversal reason:');
        if (!reason?.trim()) return;

        try {
            setIsWorking(true);
            const reversal = await unitAccountsDataService.reverseUnitJournalEntry(id, reason.trim());
            toast.success(`Reversal ${reversal.entryNumber} posted.`);
            router.push(`/finance/unit-journal-entries/${reversal.id}`);
        } catch (error: any) {
            toast.error(error?.message || 'Failed to reverse unit journal entry.');
        } finally {
            setIsWorking(false);
        }
    };

    const handleDelete = async () => {
        if (!window.confirm('Delete this unit journal entry?')) return;

        try {
            setIsWorking(true);
            await unitAccountsDataService.deleteUnitJournalEntry(id);
            toast.success('Unit journal entry deleted.');
            router.push('/finance/unit-journal-entries');
        } catch (error: any) {
            toast.error(error?.message || 'Failed to delete unit journal entry.');
        } finally {
            setIsWorking(false);
        }
    };

    if (isLoading) {
        return (
            <Card>
                <CardContent className="py-12 text-center text-muted-foreground">
                    Loading unit journal entry...
                </CardContent>
            </Card>
        );
    }

    if (!entry) {
        return (
            <div className="space-y-6">
                <Card>
                    <CardContent className="py-12 text-center">
                        <p className="text-muted-foreground">Entry not found.</p>
                        <Link href="/finance/unit-journal-entries">
                            <Button className="mt-4" variant="outline">Back to Journal Entries</Button>
                        </Link>
                    </CardContent>
                </Card>
            </div>
        );
    }

    const lines = entry.lines ?? [];
    const canDelete = entry.status === 'Draft' || entry.status === 'Rejected';
    const canSubmit = entry.status === 'Draft' || entry.status === 'Rejected';
    const canApprove = entry.status === 'PendingApproval';
    const canPost = entry.status === 'Approved';
    const canReverse = entry.status === 'Posted';

    return (
        <div className="space-y-6">
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                        <FileSpreadsheet className="h-8 w-8" />
                        {entry.entryNumber}
                    </h1>
                    <p className="text-muted-foreground">{entry.description || 'Unit journal entry'}</p>
                </div>
                <div className="flex items-center gap-2">
                    {getStatusBadge(entry.status)}
                </div>
            </div>

            <Breadcrumb>
                <BreadcrumbList>
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/finance">Finance</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/finance/unit-journal-entries">Unit Journal Entries</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>{entry.entryNumber}</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                <div className="lg:col-span-2 space-y-6">
                    <Card>
                        <CardHeader>
                            <CardTitle>Entry Lines</CardTitle>
                            <CardDescription>Unit quantities being posted</CardDescription>
                        </CardHeader>
                        <CardContent>
                            <Table>
                                <TableHeader>
                                    <TableRow>
                                        <TableHead className="w-[50px]">#</TableHead>
                                        <TableHead>Account</TableHead>
                                        <TableHead className="text-right">Quantity</TableHead>
                                        <TableHead>Description</TableHead>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {lines.map((line) => (
                                        <TableRow key={line.id}>
                                            <TableCell className="text-muted-foreground">{line.lineNumber}</TableCell>
                                            <TableCell>
                                                <div>
                                                    <span className="font-mono text-blue-600">
                                                        {line.unitAccountNumber || line.unitAccount?.accountNumber || '-'}
                                                    </span>
                                                    <span className="text-muted-foreground ml-2">
                                                        {line.unitAccountName || line.unitAccount?.name || ''}
                                                    </span>
                                                </div>
                                            </TableCell>
                                            <TableCell className="text-right font-mono font-semibold">
                                                {line.quantity.toLocaleString(undefined, {
                                                    minimumFractionDigits: 0,
                                                    maximumFractionDigits: 2,
                                                })}
                                            </TableCell>
                                            <TableCell className="text-muted-foreground">
                                                {line.description || '-'}
                                            </TableCell>
                                        </TableRow>
                                    ))}
                                    {lines.length === 0 && (
                                        <TableRow>
                                            <TableCell colSpan={4} className="text-center py-8 text-muted-foreground">
                                                No lines found for this entry.
                                            </TableCell>
                                        </TableRow>
                                    )}
                                </TableBody>
                            </Table>
                            <div className="flex justify-end mt-4 pt-4 border-t">
                                <div className="text-right">
                                    <p className="text-sm text-muted-foreground">Net Quantity</p>
                                    <p className="text-2xl font-bold font-mono">
                                        {totalQuantity(lines).toLocaleString(undefined, {
                                            minimumFractionDigits: 0,
                                            maximumFractionDigits: 2,
                                        })}
                                    </p>
                                </div>
                            </div>
                        </CardContent>
                    </Card>

                    <Card>
                        <CardContent className="pt-6">
                            <div className="flex flex-wrap gap-2">
                                {canSubmit && (
                                    <Button onClick={handleSubmit} disabled={isWorking}>
                                        <Send className="mr-2 h-4 w-4" />
                                        Submit for Approval
                                    </Button>
                                )}
                                {canApprove && (
                                    <>
                                        <Button onClick={handleApprove} disabled={isWorking} className="bg-green-600 hover:bg-green-700">
                                            <CheckCircle className="mr-2 h-4 w-4" />
                                            Approve
                                        </Button>
                                        <Button variant="destructive" onClick={handleReject} disabled={isWorking}>
                                            <XCircle className="mr-2 h-4 w-4" />
                                            Reject
                                        </Button>
                                    </>
                                )}
                                {canPost && (
                                    <Button onClick={handlePost} disabled={isWorking} className="bg-green-600 hover:bg-green-700">
                                        <CheckCircle className="mr-2 h-4 w-4" />
                                        Post Entry
                                    </Button>
                                )}
                                {canReverse && (
                                    <Button variant="outline" onClick={handleReverse} disabled={isWorking}>
                                        <RotateCcw className="mr-2 h-4 w-4" />
                                        Reverse
                                    </Button>
                                )}
                                {canDelete && (
                                    <Button variant="outline" onClick={handleDelete} disabled={isWorking}>
                                        <Trash2 className="mr-2 h-4 w-4" />
                                        Delete
                                    </Button>
                                )}
                            </div>
                        </CardContent>
                    </Card>
                </div>

                <div className="space-y-6">
                    <Card>
                        <CardHeader>
                            <CardTitle className="text-base">Entry Details</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-4">
                            <div className="flex items-start gap-3">
                                <Calendar className="h-4 w-4 text-muted-foreground mt-1" />
                                <div>
                                    <p className="text-sm text-muted-foreground">Entry Date</p>
                                    <p className="font-medium">{formatDate(entry.entryDate)}</p>
                                </div>
                            </div>
                            <div className="flex items-start gap-3">
                                <Calendar className="h-4 w-4 text-muted-foreground mt-1" />
                                <div>
                                    <p className="text-sm text-muted-foreground">Fiscal Period</p>
                                    <p className="font-medium">{entry.fiscalPeriodName || entry.fiscalPeriodId || '-'}</p>
                                </div>
                            </div>
                            <div className="flex items-start gap-3">
                                <User className="h-4 w-4 text-muted-foreground mt-1" />
                                <div>
                                    <p className="text-sm text-muted-foreground">Created By</p>
                                    <p className="font-medium">{entry.createdBy || '-'}</p>
                                </div>
                            </div>
                            <div className="flex items-start gap-3">
                                <Clock className="h-4 w-4 text-muted-foreground mt-1" />
                                <div>
                                    <p className="text-sm text-muted-foreground">Created At</p>
                                    <p className="font-medium">{formatDateTime(entry.createdAt)}</p>
                                </div>
                            </div>
                        </CardContent>
                    </Card>

                    {(entry.approvedAt || entry.postedAt) && (
                        <Card>
                            <CardHeader>
                                <CardTitle className="text-base">Workflow History</CardTitle>
                            </CardHeader>
                            <CardContent className="space-y-4">
                                {entry.approvedAt && (
                                    <div className="flex items-start gap-3">
                                        <CheckCircle className="h-4 w-4 text-green-600 mt-1" />
                                        <div>
                                            <p className="text-sm text-muted-foreground">Approved</p>
                                            <p className="font-medium">{formatDateTime(entry.approvedAt)}</p>
                                        </div>
                                    </div>
                                )}
                                {entry.postedAt && (
                                    <div className="flex items-start gap-3">
                                        <CheckCircle className="h-4 w-4 text-blue-600 mt-1" />
                                        <div>
                                            <p className="text-sm text-muted-foreground">Posted</p>
                                            <p className="font-medium">{formatDateTime(entry.postedAt)}</p>
                                        </div>
                                    </div>
                                )}
                            </CardContent>
                        </Card>
                    )}

                    {entry.rejectionReason && (
                        <Card className="border-destructive">
                            <CardHeader>
                                <CardTitle className="text-base text-destructive">Rejection Reason</CardTitle>
                            </CardHeader>
                            <CardContent>
                                <p>{entry.rejectionReason}</p>
                            </CardContent>
                        </Card>
                    )}
                </div>
            </div>
        </div>
    );
}
