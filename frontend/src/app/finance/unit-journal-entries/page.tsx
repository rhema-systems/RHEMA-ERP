'use client';

import React, { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow
} from '@/components/ui/table';
import { FileSpreadsheet, Plus, Search, Eye, Filter, Calendar as CalendarIcon } from 'lucide-react';
import Link from 'next/link';
import type { UnitJournalEntry, UnitJournalEntryStatus } from '@/types/unit-accounts';

// MOCK DATA
const MOCK_ENTRIES: UnitJournalEntry[] = [
    {
        id: 'uje-1',
        entryNumber: 'UJE-2024-00001',
        entryDate: '2024-12-31T00:00:00Z',
        fiscalYearId: 'fy-1',
        fiscalPeriodId: 'fp-12',
        description: 'December 2024 Employee Headcount',
        status: 'Posted',
        lines: [
            { id: 'l1', unitJournalEntryId: 'uje-1', lineNumber: 1, unitAccountId: 'ua-2', quantity: 3, description: 'New hires in Operations' },
        ],
        approvedAt: '2024-12-31T10:00:00Z',
        approvedBy: 'manager',
        postedAt: '2024-12-31T10:30:00Z',
        postedBy: 'manager',
        createdAt: '2024-12-31T09:00:00Z',
        createdBy: 'admin',
    },
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
        createdBy: 'operator',
    },
    {
        id: 'uje-3',
        entryNumber: 'UJE-2024-00003',
        entryDate: '2024-12-10T00:00:00Z',
        fiscalYearId: 'fy-1',
        fiscalPeriodId: 'fp-12',
        description: 'Office Space Update - New Branch',
        status: 'Draft',
        lines: [
            { id: 'l3', unitJournalEntryId: 'uje-3', lineNumber: 1, unitAccountId: 'ua-7', quantity: 2500 },
        ],
        createdAt: '2024-12-10T11:00:00Z',
        createdBy: 'admin',
    },
    {
        id: 'uje-4',
        entryNumber: 'UJE-2024-00004',
        entryDate: '2024-11-30T00:00:00Z',
        fiscalYearId: 'fy-1',
        fiscalPeriodId: 'fp-11',
        description: 'November 2024 Employee Count',
        status: 'Posted',
        lines: [
            { id: 'l4', unitJournalEntryId: 'uje-4', lineNumber: 1, unitAccountId: 'ua-2', quantity: 2 },
            { id: 'l5', unitJournalEntryId: 'uje-4', lineNumber: 2, unitAccountId: 'ua-3', quantity: 1 },
        ],
        approvedAt: '2024-11-30T16:00:00Z',
        approvedBy: 'manager',
        postedAt: '2024-11-30T16:30:00Z',
        postedBy: 'manager',
        createdAt: '2024-11-30T15:00:00Z',
        createdBy: 'admin',
    },
    {
        id: 'uje-5',
        entryNumber: 'UJE-2024-00005',
        entryDate: '2024-12-01T00:00:00Z',
        fiscalYearId: 'fy-1',
        fiscalPeriodId: 'fp-12',
        description: 'Staff Reduction - Sales Dept',
        status: 'Rejected',
        rejectionReason: 'Please verify the headcount with HR before resubmitting.',
        lines: [
            { id: 'l6', unitJournalEntryId: 'uje-5', lineNumber: 1, unitAccountId: 'ua-3', quantity: -5 },
        ],
        createdAt: '2024-12-01T09:00:00Z',
        createdBy: 'admin',
    },
];

export default function UnitJournalEntriesPage() {
    const [entries] = useState<UnitJournalEntry[]>(MOCK_ENTRIES);
    const [searchTerm, setSearchTerm] = useState('');
    const [statusFilter, setStatusFilter] = useState<string>('all');

    const filteredEntries = entries.filter((entry) => {
        const matchesSearch =
            searchTerm === '' ||
            entry.entryNumber.toLowerCase().includes(searchTerm.toLowerCase()) ||
            entry.description?.toLowerCase().includes(searchTerm.toLowerCase());

        const matchesStatus = statusFilter === 'all' || entry.status === statusFilter;

        return matchesSearch && matchesStatus;
    });

    const getStatusBadge = (status: UnitJournalEntryStatus) => {
        const styles: Record<UnitJournalEntryStatus, string> = {
            Draft: 'bg-gray-500 hover:bg-gray-600',
            PendingApproval: 'bg-orange-500 hover:bg-orange-600',
            Approved: 'bg-blue-500 hover:bg-blue-600',
            Posted: 'bg-green-600 hover:bg-green-700',
            Rejected: 'bg-red-500 hover:bg-red-600',
            Reversed: 'bg-purple-500 hover:bg-purple-600',
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

    const getTotalQuantity = (entry: UnitJournalEntry) => {
        return entry.lines.reduce((sum, line) => sum + line.quantity, 0);
    };

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                        <FileSpreadsheet className="h-8 w-8" />
                        Unit Journal Entries
                    </h1>
                    <p className="text-muted-foreground">
                        Post and manage unit quantity transactions
                    </p>
                </div>
                <div className="flex gap-2">
                    <Link href="/finance/unit-journal-entries/approvals">
                        <Button variant="outline">
                            Approval Queue
                        </Button>
                    </Link>
                    <Link href="/finance/unit-journal-entries/new">
                        <Button>
                            <Plus className="mr-2 h-4 w-4" />
                            New Entry
                        </Button>
                    </Link>
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
                        <BreadcrumbPage>Unit Journal Entries</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Search and Filters */}
            <Card>
                <CardHeader className="pb-3">
                    <CardTitle className="text-base flex items-center gap-2">
                        <Filter className="h-4 w-4" />
                        Search & Filter
                    </CardTitle>
                </CardHeader>
                <CardContent>
                    <div className="flex flex-col md:flex-row gap-4">
                        <div className="relative flex-1">
                            <Search className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
                            <Input
                                placeholder="Search by entry number or description..."
                                value={searchTerm}
                                onChange={(e) => setSearchTerm(e.target.value)}
                                className="pl-10"
                            />
                        </div>
                        <Select value={statusFilter} onValueChange={setStatusFilter}>
                            <SelectTrigger className="w-[200px]">
                                <SelectValue placeholder="All Status" />
                            </SelectTrigger>
                            <SelectContent>
                                <SelectItem value="all">All Status</SelectItem>
                                <SelectItem value="Draft">Draft</SelectItem>
                                <SelectItem value="PendingApproval">Pending Approval</SelectItem>
                                <SelectItem value="Approved">Approved</SelectItem>
                                <SelectItem value="Posted">Posted</SelectItem>
                                <SelectItem value="Rejected">Rejected</SelectItem>
                                <SelectItem value="Reversed">Reversed</SelectItem>
                            </SelectContent>
                        </Select>
                    </div>
                </CardContent>
            </Card>

            {/* Entries Table */}
            <Card>
                <CardHeader>
                    <CardTitle>Journal Entries ({filteredEntries.length})</CardTitle>
                    <CardDescription>
                        List of all unit journal entries
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHead>Entry Number</TableHead>
                                <TableHead>Date</TableHead>
                                <TableHead>Description</TableHead>
                                <TableHead className="text-center">Lines</TableHead>
                                <TableHead className="text-right">Total Qty</TableHead>
                                <TableHead className="text-center">Status</TableHead>
                                <TableHead className="text-right">Actions</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {filteredEntries.map((entry) => (
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
                                    <TableCell className="text-center">
                                        <Badge variant="outline">{entry.lines.length}</Badge>
                                    </TableCell>
                                    <TableCell className="text-right font-mono">
                                        {getTotalQuantity(entry).toLocaleString(undefined, {
                                            minimumFractionDigits: 0,
                                            maximumFractionDigits: 2,
                                        })}
                                    </TableCell>
                                    <TableCell className="text-center">
                                        {getStatusBadge(entry.status)}
                                    </TableCell>
                                    <TableCell className="text-right">
                                        <Link href={`/finance/unit-journal-entries/${entry.id}`}>
                                            <Button variant="ghost" size="sm">
                                                <Eye className="h-4 w-4" />
                                            </Button>
                                        </Link>
                                    </TableCell>
                                </TableRow>
                            ))}
                            {filteredEntries.length === 0 && (
                                <TableRow>
                                    <TableCell colSpan={7} className="text-center py-8 text-muted-foreground">
                                        No entries found matching your filters.
                                    </TableCell>
                                </TableRow>
                            )}
                        </TableBody>
                    </Table>
                </CardContent>
            </Card>
        </div>
    );
}
