'use client';

import React, { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow
} from '@/components/ui/table';
import { ClipboardCheck, Eye, CheckCircle, XCircle, Calendar as CalendarIcon } from 'lucide-react';
import Link from 'next/link';
import type { UnitJournalEntry } from '@/types/unit-accounts';

// MOCK DATA - Only pending approval entries
const MOCK_PENDING_ENTRIES: UnitJournalEntry[] = [
    {
        id: 'uje-2',
        entryNumber: 'UJE-2024-00002',
        entryDate: '2024-12-15T00:00:00Z',
        fiscalYearId: 'fy-1',
        fiscalPeriodId: 'fp-12',
        description: 'Machine Hours - Week 50',
        status: 'PendingApproval',
        lines: [
            { id: 'l2', unitJournalEntryId: 'uje-2', lineNumber: 1, unitAccountId: 'ua-8', quantity: 168.5 },
        ],
        createdAt: '2024-12-15T14:00:00Z',
        createdBy: 'Operator User',
    },
    {
        id: 'uje-6',
        entryNumber: 'UJE-2024-00006',
        entryDate: '2024-12-18T00:00:00Z',
        fiscalYearId: 'fy-1',
        fiscalPeriodId: 'fp-12',
        description: 'Year-End Employee Count Adjustment',
        status: 'PendingApproval',
        lines: [
            { id: 'l7', unitJournalEntryId: 'uje-6', lineNumber: 1, unitAccountId: 'ua-2', quantity: 5 },
            { id: 'l8', unitJournalEntryId: 'uje-6', lineNumber: 2, unitAccountId: 'ua-3', quantity: -2 },
        ],
        createdAt: '2024-12-18T10:00:00Z',
        createdBy: 'HR Manager',
    },
];

export default function UnitJournalEntriesApprovalPage() {
    const [entries] = useState<UnitJournalEntry[]>(MOCK_PENDING_ENTRIES);

    const formatDate = (dateString: string) => {
        return new Date(dateString).toLocaleDateString('en-GB', {
            day: '2-digit',
            month: 'short',
            year: 'numeric',
        });
    };

    const getTotalQuantity = (entry: UnitJournalEntry) => {
        return entry.lines.reduce((sum, line) => sum + line.quantity, 0);
    };

    const handleApprove = (id: string) => {
        console.log('Approving entry:', id);

    };

    const handleReject = (id: string) => {
        const reason = prompt('Enter rejection reason:');
        if (reason) {
            console.log('Rejecting entry:', id, 'Reason:', reason);

        }
    };

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div>
                <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                    <ClipboardCheck className="h-8 w-8" />
                    Approval Queue
                </h1>
                <p className="text-muted-foreground">
                    Unit journal entries pending your approval
                </p>

            </div>

            {/* Breadcrumbs */}
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

            {/* Pending Entries */}
            <Card>
                <CardHeader>
                    <CardTitle>Pending Approval ({entries.length})</CardTitle>
                    <CardDescription>
                        Review and approve or reject entries
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    {entries.length === 0 ? (
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
                                    <TableHead className="text-right">Total Qty</TableHead>
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
                                        <TableCell>{entry.createdBy}</TableCell>
                                        <TableCell className="text-center">
                                            <Badge variant="outline">{entry.lines.length}</Badge>
                                        </TableCell>
                                        <TableCell className="text-right font-mono">
                                            {getTotalQuantity(entry).toLocaleString(undefined, {
                                                minimumFractionDigits: 0,
                                                maximumFractionDigits: 2,
                                            })}
                                        </TableCell>
                                        <TableCell className="text-right">
                                            <div className="flex justify-end gap-1">
                                                <Link href={`/finance/unit-journal-entries/${entry.id}`}>
                                                    <Button variant="ghost" size="sm" title="View Details">
                                                        <Eye className="h-4 w-4" />
                                                    </Button>
                                                </Link>
                                                <Button
                                                    variant="ghost"
                                                    size="sm"
                                                    className="text-green-600 hover:text-green-700"
                                                    onClick={() => handleApprove(entry.id)}
                                                    title="Approve"
                                                >
                                                    <CheckCircle className="h-4 w-4" />
                                                </Button>
                                                <Button
                                                    variant="ghost"
                                                    size="sm"
                                                    className="text-destructive hover:text-destructive"
                                                    onClick={() => handleReject(entry.id)}
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
