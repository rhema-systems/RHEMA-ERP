'use client';

import React, { useCallback, useEffect, useState } from 'react';
import Link from 'next/link';
import { Calendar as CalendarIcon, CheckCircle, ClipboardCheck, Eye, XCircle } from 'lucide-react';
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
import type { UnitJournalEntry } from '@/types/unit-accounts';

function formatDate(dateString: string) {
    return new Date(dateString).toLocaleDateString('en-GB', {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
    });
}

export default function UnitJournalEntriesApprovalPage() {
    const [entries, setEntries] = useState<UnitJournalEntry[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [workingId, setWorkingId] = useState<string | null>(null);

    const loadEntries = useCallback(async () => {
        try {
            setIsLoading(true);
            const data = await unitAccountsDataService.getPendingUnitJournalApprovals();
            setEntries(data);
        } catch (error: any) {
            toast.error(error?.message || 'Failed to load pending unit journal approvals.');
        } finally {
            setIsLoading(false);
        }
    }, []);

    useEffect(() => {
        loadEntries();
    }, [loadEntries]);

    const handleApprove = async (id: string) => {
        try {
            setWorkingId(id);
            await unitAccountsDataService.approveUnitJournalEntry(id);
            toast.success('Unit journal entry approved.');
            await loadEntries();
        } catch (error: any) {
            toast.error(error?.message || 'Failed to approve unit journal entry.');
        } finally {
            setWorkingId(null);
        }
    };

    const handleReject = async (id: string) => {
        const reason = window.prompt('Enter rejection reason:');
        if (!reason?.trim()) return;

        try {
            setWorkingId(id);
            await unitAccountsDataService.rejectUnitJournalEntry(id, reason.trim());
            toast.success('Unit journal entry rejected.');
            await loadEntries();
        } catch (error: any) {
            toast.error(error?.message || 'Failed to reject unit journal entry.');
        } finally {
            setWorkingId(null);
        }
    };

    return (
        <div className="space-y-6">
            <div>
                <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                    <ClipboardCheck className="h-8 w-8" />
                    Approval Queue
                </h1>
                <p className="text-muted-foreground">
                    Unit journal entries pending your approval
                </p>
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
                        <BreadcrumbPage>Approvals</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            <Card>
                <CardHeader>
                    <CardTitle>Pending Approval ({entries.length})</CardTitle>
                    <CardDescription>Review and approve or reject entries</CardDescription>
                </CardHeader>
                <CardContent>
                    {isLoading ? (
                        <div className="text-center py-12 text-muted-foreground">
                            Loading pending approvals...
                        </div>
                    ) : entries.length === 0 ? (
                        <div className="text-center py-12 text-muted-foreground">
                            <ClipboardCheck className="h-12 w-12 mx-auto mb-4 opacity-50" />
                            <p>No entries pending approval.</p>
                        </div>
                    ) : (
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHead>Entry Number</TableHead>
                                    <TableHead>Date</TableHead>
                                    <TableHead>Description</TableHead>
                                    <TableHead>Submitted By</TableHead>
                                    <TableHead className="text-center">Lines</TableHead>
                                    <TableHead className="text-right">Actions</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {entries.map((entry) => (
                                    <TableRow key={entry.id}>
                                        <TableCell className="font-mono font-semibold text-blue-600">
                                            <Link href={`/finance/unit-journal-entries/${entry.id}`} className="hover:underline">
                                                {entry.entryNumber}
                                            </Link>
                                        </TableCell>
                                        <TableCell>
                                            <div className="flex items-center gap-2">
                                                <CalendarIcon className="h-3 w-3 text-muted-foreground" />
                                                {formatDate(entry.entryDate)}
                                            </div>
                                        </TableCell>
                                        <TableCell className="max-w-xs truncate">
                                            {entry.description || '-'}
                                        </TableCell>
                                        <TableCell>{entry.createdBy || '-'}</TableCell>
                                        <TableCell className="text-center">
                                            <Badge variant="outline">{entry.lineCount ?? entry.lines?.length ?? 0}</Badge>
                                        </TableCell>
                                        <TableCell className="text-right">
                                            <div className="flex justify-end gap-1">
                                                <Link href={`/finance/unit-journal-entries/${entry.id}`}>
                                                    <Button variant="ghost" size="sm" title="View details">
                                                        <Eye className="h-4 w-4" />
                                                    </Button>
                                                </Link>
                                                <Button
                                                    variant="ghost"
                                                    size="sm"
                                                    className="text-green-600 hover:text-green-700"
                                                    onClick={() => handleApprove(entry.id)}
                                                    disabled={workingId === entry.id}
                                                    title="Approve"
                                                >
                                                    <CheckCircle className="h-4 w-4" />
                                                </Button>
                                                <Button
                                                    variant="ghost"
                                                    size="sm"
                                                    className="text-destructive hover:text-destructive"
                                                    onClick={() => handleReject(entry.id)}
                                                    disabled={workingId === entry.id}
                                                    title="Reject"
                                                >
                                                    <XCircle className="h-4 w-4" />
                                                </Button>
                                            </div>
                                        </TableCell>
                                    </TableRow>
                                ))}
                            </TableBody>
                        </Table>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}
