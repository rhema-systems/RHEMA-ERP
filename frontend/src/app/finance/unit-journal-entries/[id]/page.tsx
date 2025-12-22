'use client';

import React from 'react';
import { useParams } from 'next/navigation';
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
import {
    FileSpreadsheet,
    Calendar,
    User,
    Clock,
    CheckCircle,
    XCircle,
    Send,
    RotateCcw,
    Pencil
} from 'lucide-react';
import Link from 'next/link';
import type { UnitJournalEntry, UnitJournalEntryStatus } from '@/types/unit-accounts';

// MOCK DATA
const MOCK_ENTRIES: Record<string, UnitJournalEntry> = {
    'uje-1': {
        id: 'uje-1',
        entryNumber: 'UJE-2024-00001',
        entryDate: '2024-12-31T00:00:00Z',
        fiscalYearId: 'fy-1',
        fiscalPeriodId: 'fp-12',
        description: 'December 2024 Employee Headcount',
        status: 'Posted',
        lines: [
            { id: 'l1', unitJournalEntryId: 'uje-1', lineNumber: 1, unitAccountId: 'ua-2', quantity: 3, description: 'New hires in Operations' },
            { id: 'l2', unitJournalEntryId: 'uje-1', lineNumber: 2, unitAccountId: 'ua-3', quantity: 2, description: 'New hires in Sales' },
        ],
        approvedAt: '2024-12-31T10:00:00Z',
        approvedBy: 'Finance Manager',
        postedAt: '2024-12-31T10:30:00Z',
        postedBy: 'Finance Manager',
        createdAt: '2024-12-31T09:00:00Z',
        createdBy: 'Admin User',
    },
    'uje-2': {
        id: 'uje-2',
        entryNumber: 'UJE-2024-00002',
        entryDate: '2024-12-15T00:00:00Z',
        fiscalYearId: 'fy-1',
        fiscalPeriodId: 'fp-12',
        description: 'Machine Hours - Week 50',
        status: 'PendingApproval',
        lines: [
            { id: 'l3', unitJournalEntryId: 'uje-2', lineNumber: 1, unitAccountId: 'ua-8', quantity: 168.5, description: 'Production hours for the week' },
        ],
        createdAt: '2024-12-15T14:00:00Z',
        createdBy: 'Operator User',
    },
    'uje-3': {
        id: 'uje-3',
        entryNumber: 'UJE-2024-00003',
        entryDate: '2024-12-10T00:00:00Z',
        fiscalYearId: 'fy-1',
        fiscalPeriodId: 'fp-12',
        description: 'Office Space Update - New Branch',
        status: 'Draft',
        lines: [
            { id: 'l4', unitJournalEntryId: 'uje-3', lineNumber: 1, unitAccountId: 'ua-7', quantity: 2500, description: 'New branch office space' },
        ],
        createdAt: '2024-12-10T11:00:00Z',
        createdBy: 'Admin User',
    },
};

const ACCOUNT_NAMES: Record<string, { number: string; name: string; unitType: string }> = {
    'ua-2': { number: 'U-1100', name: 'Operations Department', unitType: 'Employees' },
    'ua-3': { number: 'U-1200', name: 'Sales Department', unitType: 'Employees' },
    'ua-7': { number: 'U-2200', name: 'Branch Offices', unitType: 'Square Footage' },
    'ua-8': { number: 'U-3000', name: 'Machine Hours', unitType: 'Hours' },
};

export default function UnitJournalEntryDetailPage() {
    const params = useParams();
    const id = params.id as string;
    const entry = MOCK_ENTRIES[id];

    if (!entry) {
        return (
            <div className="space-y-6">
                <Card>
                    <CardContent className="py-12 text-center">
                        <p className="text-muted-foreground">Entry not found.</p>
                        <Link href="/finance/unit-journal-entries">
                            <Button className="mt-4" variant="outline">
                                Back to Journal Entries
                            </Button>
                        </Link>
                    </CardContent>
                </Card>
            </div>
        );
    }

    const getStatusBadge = (status: UnitJournalEntryStatus) => {
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
        return <Badge className={styles[status]}>{labels[status]}</Badge>;
    };

    const formatDate = (dateString: string) => {
        return new Date(dateString).toLocaleDateString('en-GB', {
            day: '2-digit',
            month: 'short',
            year: 'numeric',
        });
    };

    const formatDateTime = (dateString: string) => {
        return new Date(dateString).toLocaleString('en-GB', {
            day: '2-digit',
            month: 'short',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit',
        });
    };

    const getTotalQuantity = () => {
        return entry.lines.reduce((sum, line) => sum + line.quantity, 0);
    };

    const handleAction = (action: string) => {
        console.log(`${action} entry:`, entry.id);
        alert(`DEMO MODE: Entry would be ${action.toLowerCase()}.`);
    };

    const canEdit = entry.status === 'Draft' || entry.status === 'Rejected';
    const canSubmit = entry.status === 'Draft' || entry.status === 'Rejected';
    const canApprove = entry.status === 'PendingApproval';
    const canPost = entry.status === 'Approved';
    const canReverse = entry.status === 'Posted';

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                        <FileSpreadsheet className="h-8 w-8" />
                        {entry.entryNumber}
                    </h1>
                    <p className="text-muted-foreground">{entry.description}</p>
                    <p className="text-sm text-orange-600 mt-1">
                        ⚠️ DEMO FRONTEND UI
                    </p>
                </div>
                <div className="flex items-center gap-2">
                    {getStatusBadge(entry.status)}
                </div>
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
                        <BreadcrumbPage>{entry.entryNumber}</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                {/* Main Content */}
                <div className="lg:col-span-2 space-y-6">
                    {/* Entry Lines */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Entry Lines</CardTitle>
                            <CardDescription>
                                Unit quantities being posted
                            </CardDescription>
                        </CardHeader>
                        <CardContent>
                            <Table>
                                <TableHeader>
                                    <TableRow>
                                        <TableHead className="w-[50px]">#</TableHead>
                                        <TableHead>Account</TableHead>
                                        <TableHead>Unit Type</TableHead>
                                        <TableHead className="text-right">Quantity</TableHead>
                                        <TableHead>Description</TableHead>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {entry.lines.map((line) => {
                                        const account = ACCOUNT_NAMES[line.unitAccountId];
                                        return (
                                            <TableRow key={line.id}>
                                                <TableCell className="text-muted-foreground">
                                                    {line.lineNumber}
                                                </TableCell>
                                                <TableCell>
                                                    <div>
                                                        <span className="font-mono text-blue-600">
                                                            {account?.number}
                                                        </span>
                                                        <span className="text-muted-foreground ml-2">
                                                            {account?.name}
                                                        </span>
                                                    </div>
                                                </TableCell>
                                                <TableCell>
                                                    <Badge variant="secondary">{account?.unitType}</Badge>
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
                                        );
                                    })}
                                </TableBody>
                            </Table>
                            <div className="flex justify-end mt-4 pt-4 border-t">
                                <div className="text-right">
                                    <p className="text-sm text-muted-foreground">Total Quantity</p>
                                    <p className="text-2xl font-bold font-mono">
                                        {getTotalQuantity().toLocaleString(undefined, {
                                            minimumFractionDigits: 0,
                                            maximumFractionDigits: 2,
                                        })}
                                    </p>
                                </div>
                            </div>
                        </CardContent>
                    </Card>

                    {/* Actions */}
                    <Card>
                        <CardContent className="pt-6">
                            <div className="flex flex-wrap gap-2">
                                {canEdit && (
                                    <Button variant="outline" onClick={() => handleAction('Edit')}>
                                        <Pencil className="mr-2 h-4 w-4" />
                                        Edit
                                    </Button>
                                )}
                                {canSubmit && (
                                    <Button onClick={() => handleAction('Submit')}>
                                        <Send className="mr-2 h-4 w-4" />
                                        Submit for Approval
                                    </Button>
                                )}
                                {canApprove && (
                                    <>
                                        <Button onClick={() => handleAction('Approve')} className="bg-green-600 hover:bg-green-700">
                                            <CheckCircle className="mr-2 h-4 w-4" />
                                            Approve
                                        </Button>
                                        <Button variant="destructive" onClick={() => handleAction('Reject')}>
                                            <XCircle className="mr-2 h-4 w-4" />
                                            Reject
                                        </Button>
                                    </>
                                )}
                                {canPost && (
                                    <Button onClick={() => handleAction('Post')} className="bg-green-600 hover:bg-green-700">
                                        <CheckCircle className="mr-2 h-4 w-4" />
                                        Post Entry
                                    </Button>
                                )}
                                {canReverse && (
                                    <Button variant="outline" onClick={() => handleAction('Reverse')}>
                                        <RotateCcw className="mr-2 h-4 w-4" />
                                        Reverse
                                    </Button>
                                )}
                            </div>
                        </CardContent>
                    </Card>
                </div>

                {/* Sidebar - Entry Info */}
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
                                <User className="h-4 w-4 text-muted-foreground mt-1" />
                                <div>
                                    <p className="text-sm text-muted-foreground">Created By</p>
                                    <p className="font-medium">{entry.createdBy}</p>
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
                                            <p className="text-sm text-muted-foreground">by {entry.approvedBy}</p>
                                        </div>
                                    </div>
                                )}
                                {entry.postedAt && (
                                    <div className="flex items-start gap-3">
                                        <CheckCircle className="h-4 w-4 text-blue-600 mt-1" />
                                        <div>
                                            <p className="text-sm text-muted-foreground">Posted</p>
                                            <p className="font-medium">{formatDateTime(entry.postedAt)}</p>
                                            <p className="text-sm text-muted-foreground">by {entry.postedBy}</p>
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
