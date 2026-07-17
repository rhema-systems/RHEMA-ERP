'use client';

import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import {
    Plus,
    Search,
    Filter,
    MoreHorizontal,
    FileText,
    DollarSign,
    AlertCircle,
    CreditCard,
    Send,
    Ban
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
import type { EstateArSource } from '@/services/ar-service';
import { formatCurrency } from '@/lib/utils';
import { Skeleton } from '@/components/ui/skeleton';
import { useDebounce } from '@/hooks/use-debounce';
import { format } from 'date-fns';
import { useToast } from '@/components/ui/use-toast';

export default function InvoicesPage() {
    const router = useRouter();
    const { toast } = useToast();
    const queryClient = useQueryClient();
    const [searchTerm, setSearchTerm] = useState('');
    const debouncedSearchTerm = useDebounce(searchTerm, 500);
    const [page, setPage] = useState(1);
    const [pageSize] = useState(10);
    const [statusFilter, setStatusFilter] = useState<string>('');
    const [estateNotifyId, setEstateNotifyId] = useState<string | null>(null);

    const { data: invoicesData, isLoading } = useQuery({
        queryKey: ['invoices', page, pageSize, debouncedSearchTerm, statusFilter],
        queryFn: () => arService.getInvoices({
            page,
            pageSize,
            searchTerm: debouncedSearchTerm,
            status: statusFilter
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

    const handleSearch = (e: React.ChangeEvent<HTMLInputElement>) => {
        setSearchTerm(e.target.value);
        setPage(1);
    };

    const notifyEstateInvoiceResult = async (
        invoice: NonNullable<typeof invoicesData>['items'][number],
        source: EstateArSource
    ) => {
        // Estate/Finance integration: this only notifies Estate of the AR result; invoice ownership stays in Finance.
        setEstateNotifyId(`${source}:${invoice.id}`);
        try {
            await arService.notifyEstateArResult(source, {
                actionType: 'Invoice created',
                financeArEntityId: invoice.id,
                financeArReference: invoice.invoiceNumber,
                customerId: invoice.customerId,
                customerName: invoice.customerName,
                amount: invoice.totalAmount,
                currencyCode: invoice.currencyCode,
                sourceRecordReference: invoice.invoiceNumber,
                notes: invoice.notes || null,
                actionUrl: `/finance/ar/invoices/${invoice.id}`,
            });
            toast({
                title: 'Estate notified',
                description:
                    source === 'facilities'
                        ? 'Estate / Facilities has been notified of this AR invoice.'
                        : 'Estate / Property Management has been notified of this AR invoice.',
            });
        } catch (error: any) {
            toast({
                title: 'Notification failed',
                description: error.message || 'The Estate notification could not be created.',
                variant: 'destructive',
            });
        } finally {
            setEstateNotifyId(null);
        }
    };

    const getStatusBadge = (status: string) => {
        switch (status) {
            case 'Draft': return <Badge variant="secondary">Draft</Badge>;
            case 'Posted': return <Badge className="bg-blue-600">Posted</Badge>;
            case 'Paid': return <Badge className="bg-green-600">Paid</Badge>;
            case 'Overdue': return <Badge variant="destructive">Overdue</Badge>;
            case 'Void': return <Badge variant="outline" className="text-muted-foreground">Void</Badge>;
            default: return <Badge variant="secondary">{status}</Badge>;
        }
    };

    return (
        <div className="space-y-8 p-8 max-w-[1600px] mx-auto">
            <div className="flex justify-between items-center">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Invoices</h1>
                    <p className="text-muted-foreground mt-2">
                        Create and manage customer invoices.
                    </p>
                </div>
                <Button onClick={() => router.push('/finance/ar/invoices/new')}>
                    <Plus className="mr-2 h-4 w-4" /> New Invoice
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
                                    invoicesData?.items.map((invoice) => (
                                        <TableRow key={invoice.id} className="cursor-pointer hover:bg-muted/50" onClick={() => router.push(`/finance/ar/invoices/${invoice.id}`)}>
                                            <TableCell className="font-medium">{invoice.invoiceNumber}</TableCell>
                                            <TableCell>{invoice.customerName}</TableCell>
                                            <TableCell>{format(new Date(invoice.invoiceDate), 'MMM dd, yyyy')}</TableCell>
                                            <TableCell>
                                                <span className={new Date(invoice.dueDate) < new Date() && invoice.balanceAmount > 0 ? 'text-red-500 font-medium' : ''}>
                                                    {format(new Date(invoice.dueDate), 'MMM dd, yyyy')}
                                                </span>
                                            </TableCell>
                                            <TableCell className="text-right">{formatCurrency(invoice.totalAmount)}</TableCell>
                                            <TableCell className="text-right font-medium">{formatCurrency(invoice.balanceAmount)}</TableCell>
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
                                                        {invoice.status === 'Draft' && (
                                                            <DropdownMenuItem onClick={() => router.push(`/finance/ar/invoices/${invoice.id}/edit`)}>
                                                                <FileText className="mr-2 h-4 w-4" /> Edit Invoice
                                                            </DropdownMenuItem>
                                                        )}
                                                        {invoice.status === 'Posted' && invoice.balanceAmount > 0 && (
                                                            <DropdownMenuItem onClick={() => router.push(`/finance/ar/payments/new?customerId=${invoice.customerId}&invoiceId=${invoice.id}`)}>
                                                                <DollarSign className="mr-2 h-4 w-4" /> Receive Payment
                                                            </DropdownMenuItem>
                                                        )}
                                                        <DropdownMenuSeparator />
                                                        <DropdownMenuItem onClick={() => arService.sendInvoice(invoice.id)}>
                                                            <Send className="mr-2 h-4 w-4" /> Send Email
                                                        </DropdownMenuItem>
                                                        <DropdownMenuItem
                                                            disabled={estateNotifyId === `facilities:${invoice.id}`}
                                                            onClick={(event) => {
                                                                event.stopPropagation();
                                                                notifyEstateInvoiceResult(invoice, 'facilities');
                                                            }}
                                                        >
                                                            Notify Estate / Facilities
                                                        </DropdownMenuItem>
                                                        <DropdownMenuItem
                                                            disabled={estateNotifyId === `property-management:${invoice.id}`}
                                                            onClick={(event) => {
                                                                event.stopPropagation();
                                                                notifyEstateInvoiceResult(invoice, 'property-management');
                                                            }}
                                                        >
                                                            Notify Estate / Property Mgnt
                                                        </DropdownMenuItem>
                                                        {(invoice.status === 'Posted' || invoice.status === 'Overdue') && (
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
