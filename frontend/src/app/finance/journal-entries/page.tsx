'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { FileText, Plus, Search, Eye, Filter, Calendar as CalendarIcon, Loader2 } from 'lucide-react';
import Link from 'next/link';
import type { JournalEntry, JournalType, PostingStatus } from '@/types/finance';
import { financeDataService } from '@/services/finance/finance-data.service';

export default function JournalEntriesPage() {
    const [entries, setEntries] = useState<JournalEntry[]>([]);
    const [loading, setLoading] = useState(true);
    const [searchTerm, setSearchTerm] = useState('');
    const [filters, setFilters] = useState({
        status: 'all',
        type: 'all',
        period: 'all',
    });

    const loadEntries = useCallback(async () => {
        try {
            setLoading(true);
            const apiFilters: Record<string, string> = {};
            if (filters.status !== 'all') apiFilters.status = filters.status;
            const data = await financeDataService.getJournalEntries(apiFilters);
            setEntries(data);
        } catch (err) {
            console.error('Failed to load journal entries', err);
        } finally {
            setLoading(false);
        }
    }, [filters.status]);

    useEffect(() => {
        loadEntries();
    }, [loadEntries]);

    const filteredEntries = entries.filter((entry) => {
        const matchesSearch =
            searchTerm === '' ||
            entry.journalEntryNumber.toLowerCase().includes(searchTerm.toLowerCase()) ||
            entry.description.toLowerCase().includes(searchTerm.toLowerCase());
        const matchesType = filters.type === 'all' || entry.journalType === filters.type;
        return matchesSearch && matchesType;
    });

    const getStatusBadge = (status: PostingStatus) => {
        const variants: Record<PostingStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
            Posted: 'default',
            Draft: 'secondary',
            'Pending Approval': 'outline',
            Approved: 'outline',
            Rejected: 'destructive',
            Reversed: 'destructive',
        };
        let className = '';
        if (status === 'Posted') className = 'bg-green-600 hover:bg-green-700';
        if (status === 'Draft') className = 'bg-gray-500 hover:bg-gray-600';
        if (status === 'Pending Approval') className = 'text-orange-600 border-orange-600';
        return <Badge variant={variants[status]} className={className}>{status}</Badge>;
    };

    const getTypeBadge = (type: JournalType) => {
        return <Badge variant="outline">{type}</Badge>;
    };

    const formatCurrency = (amount: number, currency: string) => {
        return new Intl.NumberFormat('en-GH', {
            style: 'currency',
            currency: currency,
        }).format(amount);
    };

    const formatDate = (dateString: string) => {
        return new Date(dateString).toLocaleDateString('en-GB', {
            day: '2-digit',
            month: 'short',
            year: 'numeric',
        });
    };

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                        <FileText className="h-8 w-8" />
                        Journal Entries
                    </h1>
                    <p className="text-muted-foreground">
                        View and manage general ledger journal entries
                    </p>
                </div>
                <Link href="/finance/journal-entries/new">
                    <Button>
                        <Plus className="mr-2 h-4 w-4" />
                        New Journal Entry
                    </Button>
                </Link>
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
                        <BreadcrumbPage>Journal Entries</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Search and Filters */}
            <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                <Card className="md:col-span-2">
                    <CardHeader className="pb-3">
                        <CardTitle className="text-base">Search</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="relative">
                            <Search className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
                            <Input
                                placeholder="Search by JE number or description..."
                                value={searchTerm}
                                onChange={(e) => setSearchTerm(e.target.value)}
                                className="pl-10"
                            />
                        </div>
                    </CardContent>
                </Card>
                <Card className="md:col-span-2">
                    <CardHeader className="pb-3">
                        <CardTitle className="text-base flex items-center gap-2">
                            <Filter className="h-4 w-4" />
                            Filters
                        </CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="grid grid-cols-2 gap-2">
                            <Select
                                value={filters.status}
                                onValueChange={(value) => setFilters({ ...filters, status: value })}
                            >
                                <SelectTrigger><SelectValue /></SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="all">All Status</SelectItem>
                                    <SelectItem value="Draft">Draft</SelectItem>
                                    <SelectItem value="Posted">Posted</SelectItem>
                                    <SelectItem value="Pending Approval">Pending Approval</SelectItem>
                                    <SelectItem value="Approved">Approved</SelectItem>
                                    <SelectItem value="Rejected">Rejected</SelectItem>
                                </SelectContent>
                            </Select>
                            <Select
                                value={filters.type}
                                onValueChange={(value) => setFilters({ ...filters, type: value })}
                            >
                                <SelectTrigger><SelectValue /></SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="all">All Types</SelectItem>
                                    <SelectItem value="General">General</SelectItem>
                                    <SelectItem value="Adjusting">Adjusting</SelectItem>
                                    <SelectItem value="Revaluation">Revaluation</SelectItem>
                                    <SelectItem value="Reversing">Reversing</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>
                    </CardContent>
                </Card>
            </div>

            {/* Journal Entries Table */}
            <Card>
                <CardHeader>
                    <CardTitle>Journal Entries ({filteredEntries.length})</CardTitle>
                    <CardDescription>List of all journal entries in the system</CardDescription>
                </CardHeader>
                <CardContent>
                    {loading ? (
                        <div className="flex items-center justify-center py-12">
                            <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
                        </div>
                    ) : (
                        <div className="rounded-md border">
                            <table className="w-full">
                                <thead>
                                    <tr className="border-b bg-muted/50">
                                        <th className="p-4 text-left font-medium">JE Number</th>
                                        <th className="p-4 text-left font-medium">Date</th>
                                        <th className="p-4 text-left font-medium">Description</th>
                                        <th className="p-4 text-left font-medium">Type</th>
                                        <th className="p-4 text-right font-medium">Amount</th>
                                        <th className="p-4 text-left font-medium">Status</th>
                                        <th className="p-4 text-right font-medium">Actions</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {filteredEntries.map((entry) => (
                                        <tr key={entry.id} className="border-b hover:bg-muted/50">
                                            <td className="p-4 font-mono font-semibold text-blue-600">
                                                <Link href={`/finance/journal-entries/${entry.id}`} className="hover:underline">
                                                    {entry.journalEntryNumber}
                                                </Link>
                                            </td>
                                            <td className="p-4">
                                                <div className="flex items-center gap-2">
                                                    <CalendarIcon className="h-3 w-3 text-muted-foreground" />
                                                    {formatDate(entry.entryDate)}
                                                </div>
                                            </td>
                                            <td className="p-4 max-w-xs truncate" title={entry.description}>
                                                {entry.description}
                                            </td>
                                            <td className="p-4">{getTypeBadge(entry.journalType)}</td>
                                            <td className="p-4 text-right font-mono">
                                                {formatCurrency(entry.totalDebitAmount, entry.primaryCurrency || 'GHS')}
                                            </td>
                                            <td className="p-4">{getStatusBadge(entry.postingStatus)}</td>
                                            <td className="p-4 text-right">
                                                <Link href={`/finance/journal-entries/${entry.id}`}>
                                                    <Button variant="ghost" size="sm">
                                                        <Eye className="h-4 w-4" />
                                                    </Button>
                                                </Link>
                                            </td>
                                        </tr>
                                    ))}
                                    {filteredEntries.length === 0 && (
                                        <tr>
                                            <td colSpan={7} className="p-8 text-center text-muted-foreground">
                                                No journal entries found matching your filters.
                                            </td>
                                        </tr>
                                    )}
                                </tbody>
                            </table>
                        </div>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}
