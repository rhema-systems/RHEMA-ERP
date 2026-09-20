'use client';

import React, { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { Calendar as CalendarIcon, Eye, FileSpreadsheet, Filter, Plus, Search } from 'lucide-react';
import { toast } from 'sonner';
import { getProcurementProblemMessage as getApiProblemMessage } from '@/lib/procurement-tender-header-actions';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from '@/components/ui/table';
import { unitAccountsDataService } from '@/services/finance/unit-accounts-data.service';
import { useAuth } from '@/hooks/use-auth';
import type { UnitJournalEntry, UnitJournalEntryStatus } from '@/types/unit-accounts';

const statusOptions: UnitJournalEntryStatus[] = [
    'Draft',
    'PendingApproval',
    'Approved',
    'ReadyToPost',
    'Posted',
    'Rejected',
    'Reversed',
];

function getStatusBadge(status: UnitJournalEntryStatus) {
    const styles: Record<UnitJournalEntryStatus, string> = {
        Draft: 'bg-gray-500 hover:bg-gray-600',
        PendingApproval: 'bg-orange-500 hover:bg-orange-600',
        Approved: 'bg-blue-500 hover:bg-blue-600',
        ReadyToPost: 'bg-emerald-700 hover:bg-emerald-800',
        Posted: 'bg-green-600 hover:bg-green-700',
        Rejected: 'bg-red-500 hover:bg-red-600',
        Reversed: 'bg-purple-500 hover:bg-purple-600',
    };

    const labels: Record<UnitJournalEntryStatus, string> = {
        Draft: 'Draft',
        PendingApproval: 'Pending Approval',
        Approved: 'Approved',
        ReadyToPost: 'Ready to Post',
        Posted: 'Posted',
        Rejected: 'Rejected',
        Reversed: 'Reversed',
    };

    return <Badge className={styles[status] ?? 'bg-gray-500'}>{labels[status] ?? status}</Badge>;
}

function formatDate(dateString: string) {
    return new Date(dateString).toLocaleDateString('en-GB', {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
    });
}

export default function UnitJournalEntriesPage() {
    const { hasPermission } = useAuth();
    const [entries, setEntries] = useState<UnitJournalEntry[]>([]);
    const [searchTerm, setSearchTerm] = useState('');
    const [statusFilter, setStatusFilter] = useState<string>('all');
    const [isLoading, setIsLoading] = useState(true);

    useEffect(() => {
        let isMounted = true;

        const loadEntries = async () => {
            try {
                setIsLoading(true);
                const data = await unitAccountsDataService.getUnitJournalEntries();
                if (isMounted) {
                    setEntries(data);
                }
            } catch (error: any) {
                toast.error(getApiProblemMessage(error, 'Failed to load unit journal entries.'));
            } finally {
                if (isMounted) {
                    setIsLoading(false);
                }
            }
        };

        loadEntries();

        return () => {
            isMounted = false;
        };
    }, []);

    const filteredEntries = useMemo(() => {
        return entries.filter((entry) => {
            const query = searchTerm.trim().toLowerCase();
            const matchesSearch =
                query === '' ||
                entry.entryNumber.toLowerCase().includes(query) ||
                entry.description?.toLowerCase().includes(query) ||
                entry.sourceDocument?.toLowerCase().includes(query);

            const matchesStatus = statusFilter === 'all' || entry.status === statusFilter;

            return matchesSearch && matchesStatus;
        });
    }, [entries, searchTerm, statusFilter]);

    return (
        <div className="space-y-6">
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
                    {hasPermission('Finance.JournalEntries.Approve') && entries.some(entry => entry.approvalRequired !== false && entry.status === 'PendingApproval') && <Link href="/finance/unit-journal-entries/approvals">
                        <Button variant="outline">Approval Queue</Button>
                    </Link>}
                    <Link href="/finance/unit-journal-entries/new">
                        <Button>
                            <Plus className="mr-2 h-4 w-4" />
                            New Entry
                        </Button>
                    </Link>
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
                        <BreadcrumbPage>Unit Journal Entries</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

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
                                placeholder="Search by number, description, or source..."
                                value={searchTerm}
                                onChange={(e) => setSearchTerm(e.target.value)}
                                className="pl-10"
                            />
                        </div>
                        <Select value={statusFilter} onValueChange={setStatusFilter}>
                            <SelectTrigger className="w-[220px]">
                                <SelectValue placeholder="All Status" />
                            </SelectTrigger>
                            <SelectContent>
                                <SelectItem value="all">All Status</SelectItem>
                                {statusOptions.map((status) => (
                                    <SelectItem key={status} value={status}>
                                        {status === 'PendingApproval' ? 'Pending Approval' : status === 'ReadyToPost' ? 'Ready to Post' : status}
                                    </SelectItem>
                                ))}
                            </SelectContent>
                        </Select>
                    </div>
                </CardContent>
            </Card>

            <Card>
                <CardHeader>
                    <CardTitle>Journal Entries ({filteredEntries.length})</CardTitle>
                    <CardDescription>List of all unit journal entries</CardDescription>
                </CardHeader>
                <CardContent>
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHead>Entry Number</TableHead>
                                <TableHead>Date</TableHead>
                                <TableHead>Description</TableHead>
                                <TableHead>Source</TableHead>
                                <TableHead className="text-center">Lines</TableHead>
                                <TableHead className="text-center">Status</TableHead>
                                <TableHead className="text-right">Actions</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {isLoading && (
                                <TableRow>
                                    <TableCell colSpan={7} className="text-center py-8 text-muted-foreground">
                                        Loading unit journal entries...
                                    </TableCell>
                                </TableRow>
                            )}
                            {!isLoading && filteredEntries.map((entry) => (
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
                                    <TableCell className="max-w-xs truncate">
                                        {entry.sourceDocument || '-'}
                                    </TableCell>
                                    <TableCell className="text-center">
                                        <Badge variant="outline">{entry.lineCount ?? entry.lines?.length ?? 0}</Badge>
                                    </TableCell>
                                    <TableCell className="text-center">
                                        {getStatusBadge(entry.status)}
                                    </TableCell>
                                    <TableCell className="text-right">
                                        <Link href={`/finance/unit-journal-entries/${entry.id}`}>
                                            <Button variant="ghost" size="sm" title="View details">
                                                <Eye className="h-4 w-4" />
                                            </Button>
                                        </Link>
                                    </TableCell>
                                </TableRow>
                            ))}
                            {!isLoading && filteredEntries.length === 0 && (
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
