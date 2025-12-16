'use client';

import React from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { ArrowLeft, Printer, Download, CheckCircle, XCircle, FileText } from 'lucide-react';
import { useRouter } from 'next/navigation';
import type { JournalEntry, PostingStatus } from '@/types/finance';

// MOCK DATA - In real app, fetch by ID
const MOCK_ENTRY: JournalEntry = {
    id: 'je-1',
    journalEntryNumber: 'JE-2024-001',
    journalType: 'General',
    entryDate: '2024-01-15T00:00:00Z',
    description: 'Monthly Office Rent Payment',
    totalDebitAmount: 5000.00,
    totalCreditAmount: 5000.00,
    isBalanced: true,
    isMultiCurrency: false,
    primaryCurrency: 'GHS',
    bookClassification: 'Base',
    fiscalPeriodId: 'fp-1',
    postingStatus: 'Posted',
    postingDate: '2024-01-15T10:30:00Z',
    requiresApproval: true,
    approvalStatus: 'Approved',
    isReversed: false,
    isRevaluationEntry: false,
    transactions: [
        { id: '1', journalEntryId: 'je-1', lineNumber: 1, accountId: 'acc-5', accountCode: '5000', accountName: 'Rent Expense', description: 'Rent for Jan 2024', debitAmount: 5000.00, creditAmount: 0, createdAt: '', updatedAt: '', isRevaluationEntry: false },
        { id: '2', journalEntryId: 'je-1', lineNumber: 2, accountId: 'acc-1', accountCode: '1000', accountName: 'Cash and Cash Equivalents', description: 'Rent for Jan 2024', debitAmount: 0, creditAmount: 5000.00, createdAt: '', updatedAt: '', isRevaluationEntry: false },
    ],
    createdAt: '2024-01-15T10:00:00Z',
    updatedAt: '2024-01-15T10:30:00Z',
    createdBy: 'user-1',
    updatedBy: 'user-2',
};

export default function JournalEntryDetailPage({ params }: { params: { id: string } }) {
    const router = useRouter();
    const entry = MOCK_ENTRY; // In real app: fetch based on params.id

    const getStatusBadge = (status: PostingStatus) => {
        const variants: Record<PostingStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
            Posted: 'default',
            Draft: 'secondary',
            'Pending Approval': 'outline',
            Approved: 'outline',
            Rejected: 'destructive',
            Reversed: 'destructive',
        };
        return <Badge variant={variants[status]}>{status}</Badge>;
    };

    const formatCurrency = (amount: number) => {
        return new Intl.NumberFormat('en-GH', {
            style: 'currency',
            currency: entry.primaryCurrency || 'GHS',
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
                    <h1 className="text-3xl font-bold tracking-tight">
                        {entry.journalEntryNumber}
                    </h1>
                    <p className="text-muted-foreground">
                        {entry.description}
                    </p>
                    <p className="text-sm text-orange-600 mt-1">
                        ⚠️ DEMO MODE - Using mock data (backend not connected)
                    </p>
                </div>
                <div className="flex gap-2">
                    <Button variant="outline" onClick={() => router.back()}>
                        <ArrowLeft className="mr-2 h-4 w-4" />
                        Back
                    </Button>
                    <Button variant="outline">
                        <Printer className="mr-2 h-4 w-4" />
                        Print
                    </Button>
                    <Button variant="outline">
                        <Download className="mr-2 h-4 w-4" />
                        Export
                    </Button>
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
                        <BreadcrumbLink href="/finance/journal-entries">Journal Entries</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>{entry.journalEntryNumber}</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                {/* Main Content - Lines */}
                <div className="lg:col-span-2 space-y-6">
                    <Card>
                        <CardHeader>
                            <CardTitle>Transaction Lines</CardTitle>
                        </CardHeader>
                        <CardContent>
                            <div className="rounded-md border">
                                <table className="w-full">
                                    <thead>
                                        <tr className="border-b bg-muted/50">
                                            <th className="p-3 text-left font-medium">Account</th>
                                            <th className="p-3 text-left font-medium">Description</th>
                                            <th className="p-3 text-right font-medium">Debit</th>
                                            <th className="p-3 text-right font-medium">Credit</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {entry.transactions.map((line) => (
                                            <tr key={line.id} className="border-b last:border-0 hover:bg-muted/50">
                                                <td className="p-3">
                                                    <div className="font-mono font-semibold">{line.accountCode}</div>
                                                    <div className="text-sm text-muted-foreground">{line.accountName}</div>
                                                </td>
                                                <td className="p-3">{line.description}</td>
                                                <td className="p-3 text-right font-mono">
                                                    {line.debitAmount > 0 ? formatCurrency(line.debitAmount) : '-'}
                                                </td>
                                                <td className="p-3 text-right font-mono">
                                                    {line.creditAmount > 0 ? formatCurrency(line.creditAmount) : '-'}
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                    <tfoot>
                                        <tr className="bg-muted/50 font-bold">
                                            <td colSpan={2} className="p-3 text-right">Totals:</td>
                                            <td className="p-3 text-right">{formatCurrency(entry.totalDebitAmount)}</td>
                                            <td className="p-3 text-right">{formatCurrency(entry.totalCreditAmount)}</td>
                                        </tr>
                                    </tfoot>
                                </table>
                            </div>
                        </CardContent>
                    </Card>
                </div>

                {/* Sidebar - Header Info */}
                <div className="space-y-6">
                    <Card>
                        <CardHeader>
                            <CardTitle>Entry Details</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-4">
                            <div>
                                <p className="text-sm text-muted-foreground">Status</p>
                                <div className="mt-1">{getStatusBadge(entry.postingStatus)}</div>
                            </div>
                            <div>
                                <p className="text-sm text-muted-foreground">Entry Date</p>
                                <p className="font-medium">{formatDate(entry.entryDate)}</p>
                            </div>
                            <div>
                                <p className="text-sm text-muted-foreground">Type</p>
                                <Badge variant="outline">{entry.journalType}</Badge>
                            </div>
                            <div>
                                <p className="text-sm text-muted-foreground">Currency</p>
                                <p className="font-mono font-semibold">{entry.primaryCurrency}</p>
                            </div>
                            {entry.postingDate && (
                                <div>
                                    <p className="text-sm text-muted-foreground">Posted Date</p>
                                    <p className="font-medium">{formatDate(entry.postingDate)}</p>
                                </div>
                            )}
                            <div>
                                <p className="text-sm text-muted-foreground">Created By</p>
                                <p className="font-medium">{entry.createdBy}</p>
                            </div>
                        </CardContent>
                    </Card>

                    {/* Actions */}
                    {entry.postingStatus === 'Draft' && (
                        <Card>
                            <CardHeader>
                                <CardTitle>Actions</CardTitle>
                            </CardHeader>
                            <CardContent className="space-y-2">
                                <Button className="w-full" variant="secondary" onClick={() => router.push(`/finance/journal-entries/${params.id}/edit`)}>
                                    <FileText className="mr-2 h-4 w-4" />
                                    Edit Entry
                                </Button>
                                <Button className="w-full">
                                    <CheckCircle className="mr-2 h-4 w-4" />
                                    Post Entry
                                </Button>
                                <Button variant="outline" className="w-full text-destructive hover:text-destructive">
                                    <XCircle className="mr-2 h-4 w-4" />
                                    Delete Entry
                                </Button>
                            </CardContent>
                        </Card>
                    )}
                </div>
            </div>
        </div>
    );
}
