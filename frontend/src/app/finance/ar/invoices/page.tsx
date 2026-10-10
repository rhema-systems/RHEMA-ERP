'use client';

import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useRouter, useSearchParams } from 'next/navigation';
import {
    Plus,
    Search,
    Filter,
    MoreHorizontal,
    FileText,
    DollarSign,
    Send,
    Ban,
    Trash2
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
import { arService } from '@/services/ar-service';
import { formatCurrency } from '@/lib/utils';
import { Skeleton } from '@/components/ui/skeleton';
import { useDebounce } from '@/hooks/use-debounce';
import { format } from 'date-fns';
import { useToast } from '@/components/ui/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import type { Invoice } from '@/types/ar';

export default function InvoicesPage() {
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
    const [invoiceToSubmit, setInvoiceToSubmit] = useState<Invoice | null>(null);
    const [invoiceToDelete, setInvoiceToDelete] = useState<Invoice | null>(null);

    const { data: invoicesData, isLoading } = useQuery({
        queryKey: ['invoices', page, pageSize, debouncedSearchTerm, statusFilter, openingBalanceOnly],
        queryFn: () => arService.getInvoices({
            page,
            pageSize,
            searchTerm: debouncedSearchTerm,
            status: statusFilter,
            isOpeningBalance: openingBalanceOnly ? true : undefined,
        }),
    });

    const voidInvoiceMutation = useMutation({
        mutationFn: (id: string) => arService.voidInvoice(id, 'Voided by user'),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['invoices'] });
            toast({
                title: 'Success',
                description: 'Invoice voided successfully',
            });
        },
        onError: (error: any) => {
            toast({
                title: 'Error',
                description: error.message || 'Failed to void invoice',
                variant: 'destructive',
            });
        },
    });

    const submitInvoiceMutation = useMutation({
        mutationFn: (id: string) => arService.submitInvoiceForApproval(id),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['invoices'] });
            toast({
                title: 'Success',
                description: 'Invoice submitted to the Finance approval workflow',
            });
        },
        onError: (error: any) => {
            toast({
                title: 'Error',
                description: error.message || 'Failed to submit invoice for approval',
                variant: 'destructive',
            });
        },
    });

    const deleteInvoiceMutation = useMutation({
        mutationFn: (id: string) => arService.deleteInvoice(id),
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: ['invoices'] });
            toast({ title: 'Draft invoice deleted', description: 'The customer invoice draft was removed.' });
        },
        onError: (error: any) => {
            toast({ title: 'Unable to delete invoice', description: error.message || 'The draft invoice could not be deleted.', variant: 'destructive' });
        },
    });

    const handleSearch = (e: React.ChangeEvent<HTMLInputElement>) => {
        setSearchTerm(e.target.value);
        setPage(1);
    };

    const getStatusBadge = (status: string) => {
        switch (status) {
            case 'Draft': return <Badge variant="secondary">Draft</Badge>;
            case 'PendingApproval': return <Badge className="bg-amber-600">Pending approval</Badge>;
            case 'Rejected': return <Badge variant="destructive">Rejected</Badge>;
            case 'Sent': return <Badge className="bg-blue-600">Sent</Badge>;
            case 'Posted': return <Badge className="bg-blue-600">Posted</Badge>;
            case 'Paid': return <Badge className="bg-green-600">Paid</Badge>;
            case 'Overdue': return <Badge variant="destructive">Overdue</Badge>;
            case 'Void': return <Badge variant="outline" className="text-muted-foreground">Void</Badge>;
            default: return <Badge variant="secondary">{status}</Badge>;
        }
    };

    const formatInvoiceDate = (value?: string | null) =>
        value ? format(new Date(value), 'MMM dd, yyyy') : 'Not set';

    return (
        <div className="space-y-8 p-8 max-w-[1600px] mx-auto">
            <div className="flex justify-between items-center">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Invoices</h1>
                    <p className="text-muted-foreground mt-2">
                        Create and manage customer invoices.
                    </p>
                </div>
                {hasAnyPermission(['Finance.AR.Invoices.Create', 'Finance.AR.Invoices.Write']) && (
                <Button onClick={() => router.push('/finance/ar/invoices/new')}>
                    <Plus className="mr-2 h-4 w-4" /> New Invoice
                </Button>
                )}
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
                                    <DropdownMenuItem onClick={() => setStatusFilter('PendingApproval')}>Pending approval</DropdownMenuItem>
                                    <DropdownMenuItem onClick={() => setStatusFilter('Rejected')}>Rejected</DropdownMenuItem>
                                    <DropdownMenuItem onClick={() => setStatusFilter('Sent')}>Sent</DropdownMenuItem>
                                    <DropdownMenuItem onClick={() => setStatusFilter('Posted')}>Posted</DropdownMenuItem>
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
                                    <TableHead>Customer</TableHead>
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
                                            No invoices found.
                                        </TableCell>
                                    </TableRow>
                                ) : (
                                    invoicesData?.items.map((invoice) => {
                                        const isPastDue = !!invoice.dueDate && new Date(invoice.dueDate) < new Date() && invoice.balanceAmount > 0;
                                        return (
                                        <TableRow key={invoice.id} className="cursor-pointer hover:bg-muted/50" onClick={() => router.push(`/finance/ar/invoices/${invoice.id}`)}>
                                            <TableCell className="font-medium">{invoice.invoiceNumber}</TableCell>
                                            <TableCell>{invoice.customerName}</TableCell>
                                            <TableCell>{formatInvoiceDate(invoice.invoiceDate)}</TableCell>
                                            <TableCell>
                                                <span className={isPastDue ? 'text-red-500 font-medium' : ''}>
                                                    {formatInvoiceDate(invoice.dueDate)}
                                                </span>
                                            </TableCell>
                                            <TableCell className="text-right">{formatCurrency(invoice.totalAmount, invoice.currencyCode)}</TableCell>
                                            <TableCell className="text-right font-medium">{formatCurrency(invoice.balanceAmount, invoice.currencyCode)}</TableCell>
                                            <TableCell>{getStatusBadge(invoice.status)}</TableCell>
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
                                                        <DropdownMenuItem onClick={() => router.push(`/finance/ar/invoices/${invoice.id}`)}>
                                                            View Details
                                                        </DropdownMenuItem>
                                                        {(invoice.status === 'Draft' || invoice.status === 'Rejected') && hasAnyPermission(['Finance.AR.Invoices.Edit', 'Finance.AR.Invoices.Write']) && (
                                                            <DropdownMenuItem onClick={() => router.push(`/finance/ar/invoices/${invoice.id}/edit`)}>
                                                                <FileText className="mr-2 h-4 w-4" /> Edit Invoice
                                                            </DropdownMenuItem>
                                                        )}
                                                        {(invoice.status === 'Sent' || invoice.status === 'Posted') && invoice.balanceAmount > 0 && (
                                                            <>
                                                                <DropdownMenuItem onClick={() => router.push(`/finance/ar/receipts/new?mode=apply-account&businessPartnerId=${invoice.businessPartnerId}&invoiceId=${invoice.id}`)}>
                                                                    <DollarSign className="mr-2 h-4 w-4" /> Apply payment on account
                                                                </DropdownMenuItem>
                                                                <DropdownMenuItem onClick={() => router.push(`/finance/ar/receipts/new?businessPartnerId=${invoice.businessPartnerId}&invoiceId=${invoice.id}`)}>
                                                                    <DollarSign className="mr-2 h-4 w-4" /> Record new receipt
                                                                </DropdownMenuItem>
                                                            </>
                                                        )}
                                                        <DropdownMenuSeparator />
                                                        {(invoice.status === 'Draft' || invoice.status === 'Rejected') && hasPermission('Finance.AR.Invoices.Send') && (
                                                        <DropdownMenuItem onClick={(event) => { event.stopPropagation(); setInvoiceToSubmit(invoice); }}>
                                                            <Send className="mr-2 h-4 w-4" /> Submit for Approval
                                                        </DropdownMenuItem>
                                                        )}
                                                        {invoice.status === 'Draft' && hasAnyPermission(['Finance.AR.Invoices.Delete', 'Finance.AR.Invoices.Write']) && (
                                                            <DropdownMenuItem className="text-red-600" onClick={(event) => { event.stopPropagation(); setInvoiceToDelete(invoice); }}>
                                                                <Trash2 className="mr-2 h-4 w-4" /> Delete Draft
                                                            </DropdownMenuItem>
                                                        )}
                                                        {(invoice.status === 'Sent' || invoice.status === 'Posted' || invoice.status === 'Overdue') && hasPermission('Finance.AR.Invoices.Void') && (
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
                                        );
                                    })
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
            <ConfirmationDialog
                open={invoiceToSubmit !== null}
                onOpenChange={(open) => !open && setInvoiceToSubmit(null)}
                title="Submit invoice for approval?"
                description={invoiceToSubmit ? `${invoiceToSubmit.invoiceNumber} will enter the Finance approval queue and will not post until final approval.` : undefined}
                confirmText="Submit for approval"
                isLoading={submitInvoiceMutation.isPending}
                onConfirm={async () => {
                    if (!invoiceToSubmit) return false;
                    await submitInvoiceMutation.mutateAsync(invoiceToSubmit.id);
                    setInvoiceToSubmit(null);
                }}
                maxWidth="500px"
            />
            <ConfirmationDialog
                open={invoiceToDelete !== null}
                onOpenChange={(open) => !open && setInvoiceToDelete(null)}
                title="Delete draft customer invoice?"
                description={invoiceToDelete ? `${invoiceToDelete.invoiceNumber} will be permanently removed. Posted or submitted invoices cannot be deleted.` : undefined}
                confirmText="Delete draft"
                variant="destructive"
                isLoading={deleteInvoiceMutation.isPending}
                onConfirm={async () => {
                    if (!invoiceToDelete) return false;
                    await deleteInvoiceMutation.mutateAsync(invoiceToDelete.id);
                    setInvoiceToDelete(null);
                }}
                maxWidth="500px"
            />
        </div>
    );
}
