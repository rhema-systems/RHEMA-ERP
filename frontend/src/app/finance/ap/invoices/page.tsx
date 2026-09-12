'use client';

import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useRouter, useSearchParams } from 'next/navigation';
import {
    Plus,
    Search,
    Filter,
    MoreHorizontal,
    FileText,
    DollarSign,
    Ban,
    CheckCircle
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from '@/components/ui/table';
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuLabel,
    DropdownMenuSeparator,
    DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { accountsPayableService } from '@/services/accountsPayableService';
import { formatCurrency } from '@/lib/utils';
import { Skeleton } from '@/components/ui/skeleton';
import { useDebounce } from '@/hooks/use-debounce';
import { format } from 'date-fns';
import { useToast } from '@/components/ui/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { workflowApiService } from '@/services/workflow-api.service';
import type { WorkflowEntitySummaryDto } from '@/types/workflow';
import { getWorkflowVisibility } from '@/components/workflow/workflowVisibility';
import { vendorInvoiceStatusLabel } from '@/lib/vendor-invoice-status';

export default function VendorInvoicesPage() {
    const router = useRouter();
    const searchParams = useSearchParams();
    const { toast } = useToast();
    const queryClient = useQueryClient();
    const openingBalanceOnly = searchParams.get('isOpeningBalance') === 'true';
    const [searchTerm, setSearchTerm] = useState('');
    const debouncedSearchTerm = useDebounce(searchTerm, 500);
    const [page, setPage] = useState(1);
    const [pageSize] = useState(10);
    const [statusFilter, setStatusFilter] = useState<string>('');
    const { hasPermission, hasAnyPermission } = useAuth();
    const [workflowSummaryMap, setWorkflowSummaryMap] = useState<Record<string, WorkflowEntitySummaryDto>>({});

    const { data: invoicesData, isLoading } = useQuery({
        queryKey: ['vendor-invoices', page, pageSize, debouncedSearchTerm, statusFilter, openingBalanceOnly],
        queryFn: () => accountsPayableService.getInvoices({
            page,
            pageSize,
            searchTerm: debouncedSearchTerm,
            status: statusFilter,
            isOpeningBalance: openingBalanceOnly ? true : undefined,
        }),
    });

    const voidInvoiceMutation = useMutation({
        mutationFn: (id: string) => accountsPayableService.voidInvoice(id, 'Voided by user'),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['vendor-invoices'] });
            toast({
                title: 'Success',
                description: 'Vendor invoice voided successfully',
            });
        },
        onError: (error: any) => {
            toast({
                title: 'Error',
                description: error.message || 'Failed to void vendor invoice',
                variant: 'destructive',
            });
        },
    });

    const approveInvoiceMutation = useMutation({
        mutationFn: (id: string) => accountsPayableService.approveInvoice(id, 'Approved'),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['vendor-invoices'] });
            toast({
                title: 'Success',
                description: 'Vendor invoice approved successfully',
            });
        },
        onError: (error: any) => {
            toast({
                title: 'Error',
                description: error.message || 'Failed to approve vendor invoice',
                variant: 'destructive',
            });
        },
    });

    const submitInvoiceMutation = useMutation({
        mutationFn: (id: string) => accountsPayableService.submitInvoiceForApproval(id),
        onSuccess: (savedInvoice) => {
            queryClient.invalidateQueries({ queryKey: ['vendor-invoices'] });
            toast({ title: 'Success', description: savedInvoice.approvalRequired === false
                ? 'Invoice completed. Approval is not required.' : 'Invoice submitted for approval.' });
        },
        onError: (error: any) => {
            toast({ title: 'Error', description: error.message || 'Failed to submit invoice', variant: 'destructive' });
        },
    });

    React.useEffect(() => {
        let current = true;
        setWorkflowSummaryMap({});
        const loadSummaries = async () => {
            const items = invoicesData?.items ?? [];
            if (items.length === 0) return;
            const pending = items.filter(i => i.status === 'Draft' || i.status === 'PendingApproval');
            if (pending.length === 0) return;

            const pairs = await Promise.all(pending.map(async (i) => {
                try {
                    const s = await workflowApiService.getWorkflowEntitySummary('VendorInvoice', i.id);
                    if (s.entityId.toLowerCase() !== i.id.toLowerCase() ||
                        s.entityType.toLowerCase().replace(/[^a-z0-9]/g, '') !== 'vendorinvoice') return null;
                    return [i.id, s] as const;
                } catch {
                    return null;
                }
            }));
            const mapped = Object.fromEntries(pairs.filter(Boolean) as Array<[string, WorkflowEntitySummaryDto]>);
            if (current) setWorkflowSummaryMap(mapped);
        };
        loadSummaries();
        return () => { current = false; };
    }, [invoicesData?.items]);

    const handleSearch = (e: React.ChangeEvent<HTMLInputElement>) => {
        setSearchTerm(e.target.value);
        setPage(1);
    };

    const getStatusBadge = (invoice: Parameters<typeof vendorInvoiceStatusLabel>[0]) => {
        switch (invoice.status) {
            case 'Draft': return <Badge variant="secondary">Draft</Badge>;
            case 'PendingApproval': return <Badge className="bg-yellow-600">Pending Approval</Badge>;
            case 'Approved': return <Badge className="bg-blue-600">{vendorInvoiceStatusLabel(invoice)}</Badge>;
            case 'PartiallyPaid': return <Badge className="bg-indigo-600">Partially Paid</Badge>;
            case 'Paid': return <Badge className="bg-green-600">Paid</Badge>;
            case 'Overdue': return <Badge variant="destructive">Overdue</Badge>;
            case 'Voided': return <Badge variant="outline" className="text-muted-foreground">Voided</Badge>;
            case 'Rejected': return <Badge variant="destructive">Rejected</Badge>;
            case 'OnHold': return <Badge variant="secondary" className="bg-orange-500">On Hold</Badge>;
            default: return <Badge variant="secondary">{invoice.status}</Badge>;
        }
    };

    return (
        <div className="space-y-8 p-8 max-w-[1600px] mx-auto">
            <div className="flex justify-between items-center">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Vendor Invoices</h1>
                    <p className="text-muted-foreground mt-2">
                        Manage supplier bills and accounts payable.
                    </p>
                </div>
                <Button onClick={() => router.push('/finance/ap/invoices/create')}>
                    <Plus className="mr-2 h-4 w-4" /> Record Invoice
                </Button>
            </div>

            <Card>
                <CardHeader>
                    <div className="flex items-center justify-between">
                        <CardTitle>Invoice List</CardTitle>
                        <div className="flex items-center space-x-2">
                            <div className="relative w-64">
                                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                                <Input
                                    placeholder="Search invoices..."
                                    className="pl-8"
                                    value={searchTerm}
                                    onChange={handleSearch}
                                />
                            </div>
                            <DropdownMenu>
                                <DropdownMenuTrigger asChild>
                                    <Button variant="outline" size="icon">
                                        <Filter className="h-4 w-4" />
                                    </Button>
                                </DropdownMenuTrigger>
                                <DropdownMenuContent align="end">
                                    <DropdownMenuLabel>Filter by Status</DropdownMenuLabel>
                                    <DropdownMenuSeparator />
                                    <DropdownMenuItem onClick={() => setStatusFilter('')}>All</DropdownMenuItem>
                                    <DropdownMenuItem onClick={() => setStatusFilter('Draft')}>Draft</DropdownMenuItem>
                                    <DropdownMenuItem onClick={() => setStatusFilter('PendingApproval')}>Pending Approval</DropdownMenuItem>
                                    <DropdownMenuItem onClick={() => setStatusFilter('Approved')}>Approved</DropdownMenuItem>
                                    <DropdownMenuItem onClick={() => setStatusFilter('Paid')}>Paid</DropdownMenuItem>
                                    <DropdownMenuItem onClick={() => setStatusFilter('Overdue')}>Overdue</DropdownMenuItem>
                                </DropdownMenuContent>
                            </DropdownMenu>
                        </div>
                    </div>
                </CardHeader>
                <CardContent>
                    <div className="rounded-md border">
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHead>Invoice #</TableHead>
                                    <TableHead>Supplier</TableHead>
                                    <TableHead>Date</TableHead>
                                    <TableHead>Due Date</TableHead>
                                    <TableHead className="text-right">Amount</TableHead>
                                    <TableHead className="text-right">Balance</TableHead>
                                    <TableHead>Status</TableHead>
                                    <TableHead className="w-[70px]"></TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {isLoading ? (
                                    [...Array(5)].map((_, i) => (
                                        <TableRow key={i}>
                                            <TableCell><Skeleton className="h-4 w-[100px]" /></TableCell>
                                            <TableCell><Skeleton className="h-4 w-[150px]" /></TableCell>
                                            <TableCell><Skeleton className="h-4 w-[100px]" /></TableCell>
                                            <TableCell><Skeleton className="h-4 w-[100px]" /></TableCell>
                                            <TableCell><Skeleton className="h-4 w-[80px] ml-auto" /></TableCell>
                                            <TableCell><Skeleton className="h-4 w-[80px] ml-auto" /></TableCell>
                                            <TableCell><Skeleton className="h-4 w-[60px]" /></TableCell>
                                            <TableCell><Skeleton className="h-8 w-8" /></TableCell>
                                        </TableRow>
                                    ))
                                ) : invoicesData?.items?.length === 0 ? (
                                    <TableRow>
                                        <TableCell colSpan={8} className="h-24 text-center">
                                            No vendor invoices found.
                                        </TableCell>
                                    </TableRow>
                                ) : (
                                    invoicesData?.items.map((invoice) => (
                                        <TableRow key={invoice.id} className="cursor-pointer hover:bg-muted/50" onClick={() => router.push(`/finance/ap/invoices/${invoice.id}`)}>
                                            <TableCell className="font-medium">
                                                {invoice.invoiceNumber}
                                                {invoice.supplierInvoiceNumber && (
                                                    <span className="block text-xs text-muted-foreground truncate max-w-[120px]" title={invoice.supplierInvoiceNumber}>
                                                        Sup: {invoice.supplierInvoiceNumber}
                                                    </span>
                                                )}
                                            </TableCell>
                                            <TableCell>{invoice.supplierName}</TableCell>
                                            <TableCell>{format(new Date(invoice.invoiceDate), 'MMM dd, yyyy')}</TableCell>
                                            <TableCell>
                                                <span className={(invoice.dueDate && new Date(invoice.dueDate) < new Date() && invoice.balanceAmount > 0) ? 'text-red-500 font-medium' : ''}>
                                                    {invoice.dueDate ? format(new Date(invoice.dueDate), 'MMM dd, yyyy') : '-'}
                                                </span>
                                            </TableCell>
                                            <TableCell className="text-right">{formatCurrency(invoice.totalAmount, invoice.currencyCode)}</TableCell>
                                            <TableCell className="text-right font-medium">{formatCurrency(invoice.balanceAmount, invoice.currencyCode)}</TableCell>
                                            <TableCell>{getStatusBadge(invoice)}</TableCell>
                                            <TableCell>
                                                <DropdownMenu>
                                                    <DropdownMenuTrigger asChild>
                                                        <Button variant="ghost" className="h-8 w-8 p-0" onClick={(e) => e.stopPropagation()}>
                                                            <span className="sr-only">Open menu</span>
                                                            <MoreHorizontal className="h-4 w-4" />
                                                        </Button>
                                                    </DropdownMenuTrigger>
                                                    <DropdownMenuContent align="end">
                                                        <DropdownMenuLabel>Actions</DropdownMenuLabel>
                                                        <DropdownMenuItem onClick={() => router.push(`/finance/ap/invoices/${invoice.id}`)}>
                                                            <FileText className="mr-2 h-4 w-4" /> View Details
                                                        </DropdownMenuItem>
                                                        {invoice.status === 'Draft' && hasAnyPermission(['Finance.AP.Invoices.Edit', 'Finance.AP.Invoices.Write']) && (
                                                            <DropdownMenuItem onClick={() => router.push(`/finance/ap/invoices/${invoice.id}/edit`)}>
                                                                <FileText className="mr-2 h-4 w-4" /> Edit Invoice
                                                            </DropdownMenuItem>
                                                        )}
                                                        {invoice.status === 'Draft' && !invoice.purchaseOrderId && getWorkflowVisibility({ summary: workflowSummaryMap[invoice.id] }).known && hasAnyPermission(['Finance.AP.Invoices.SubmitForApproval', 'Finance.AP.Invoices.Approve']) && (
                                                            <DropdownMenuItem disabled={submitInvoiceMutation.isPending} onClick={() => submitInvoiceMutation.mutate(invoice.id)}>
                                                                <CheckCircle className="mr-2 h-4 w-4" /> {getWorkflowVisibility({ summary: workflowSummaryMap[invoice.id] }).direct ? (invoice.isOpeningBalance ? 'Complete' : 'Post') : 'Submit for Approval'}
                                                            </DropdownMenuItem>
                                                        )}
                                                        {invoice.status === 'PendingApproval' && invoice.approvalRequired !== false && !invoice.purchaseOrderId && hasPermission('Finance.AP.Invoices.Approve') && getWorkflowVisibility({ summary: workflowSummaryMap[invoice.id] }).showApprovalControls && workflowSummaryMap[invoice.id]?.canCurrentUserApprove === true && (
                                                            <DropdownMenuItem onClick={() => approveInvoiceMutation.mutate(invoice.id)}>
                                                                <CheckCircle className="mr-2 h-4 w-4" /> Approve
                                                            </DropdownMenuItem>
                                                        )}
                                                        {invoice.purchaseOrderId && !invoice.isOpeningBalance && (invoice.status === 'Draft' || invoice.status === 'PendingApproval') && (
                                                            <DropdownMenuItem onClick={() => router.push(`/finance/ap/invoices/${invoice.id}`)}>
                                                                <CheckCircle className="mr-2 h-4 w-4" /> Review mandatory match
                                                            </DropdownMenuItem>
                                                        )}
                                                        {(invoice.status === 'Approved' || invoice.status === 'PartiallyPaid') && invoice.balanceAmount > 0 && (
                                                            <DropdownMenuItem onClick={() => router.push(`/finance/ap/payments/create?supplierId=${invoice.supplierId}&invoiceId=${invoice.id}`)}>
                                                                <DollarSign className="mr-2 h-4 w-4" /> Pay Invoice
                                                            </DropdownMenuItem>
                                                        )}
                                                        <DropdownMenuSeparator />
                                                        {(invoice.status === 'Approved' || invoice.status === 'Overdue') && hasPermission('Finance.AP.Invoices.Void') && (
                                                            <DropdownMenuItem
                                                                className="text-red-600"
                                                                onClick={() => voidInvoiceMutation.mutate(invoice.id)}
                                                            >
                                                                <Ban className="mr-2 h-4 w-4" /> Void Invoice
                                                            </DropdownMenuItem>
                                                        )}
                                                    </DropdownMenuContent>
                                                </DropdownMenu>
                                            </TableCell>
                                        </TableRow>
                                    ))
                                )}
                            </TableBody>
                        </Table>
                    </div>

                    {/* Pagination Controls */}
                    {invoicesData && invoicesData.totalPages > 1 && (
                        <div className="flex items-center justify-end space-x-2 py-4">
                            <Button
                                variant="outline"
                                size="sm"
                                onClick={() => setPage((p) => Math.max(1, p - 1))}
                                disabled={!invoicesData.hasPreviousPage}
                            >
                                Previous
                            </Button>
                            <div className="text-sm text-muted-foreground">
                                Page {page} of {invoicesData.totalPages}
                            </div>
                            <Button
                                variant="outline"
                                size="sm"
                                onClick={() => setPage((p) => Math.min(invoicesData.totalPages, p + 1))}
                                disabled={!invoicesData.hasNextPage}
                            >
                                Next
                            </Button>
                        </div>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}
