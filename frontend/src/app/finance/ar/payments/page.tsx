'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import {
    Plus,
    Search,
    Filter,
    MoreHorizontal,
    DollarSign,
    Calendar,
    CreditCard,
    User,
    FileText
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
import type { EstateArSource } from '@/services/ar-service';

export default function ReceiptsPage() {
    const router = useRouter();
    const { toast } = useToast();
    const [searchTerm, setSearchTerm] = useState('');
    const debouncedSearchTerm = useDebounce(searchTerm, 500);
    const [page, setPage] = useState(1);
    const [pageSize] = useState(10);
    const [statusFilter, setStatusFilter] = useState<string>('');
    const [estateNotifyId, setEstateNotifyId] = useState<string | null>(null);

    const { data: paymentsData, isLoading } = useQuery({
        queryKey: ['customer-receipts', page, pageSize, debouncedSearchTerm, statusFilter],
        queryFn: () => arService.getPayments({
            page,
            pageSize,
            // API might support search by reference number via searchTerm if backend logic exists, 
            // otherwise filters on specific fields. Assuming generic search or separate filters.
            // For now passing as generic search term if API supports it, or handle specific filters.
            //   searchTerm: debouncedSearchTerm, 
            status: statusFilter
        }),
    });

    const handleSearch = (e: React.ChangeEvent<HTMLInputElement>) => {
        setSearchTerm(e.target.value);
        setPage(1);
    };

    const notifyEstatePaymentResult = async (
        payment: NonNullable<typeof paymentsData>['items'][number],
        source: EstateArSource
    ) => {
        // Estate/Finance integration: payment results are pushed back to Estate after Finance records the receipt/allocation.
        const actionType = payment.unallocatedAmount > 0
            ? 'Receipt recorded'
            : 'Allocation completed';

        setEstateNotifyId(`${source}:${payment.id}`);
        try {
            await arService.notifyEstateArResult(source, {
                actionType,
                financeArEntityId: payment.id,
                financeArReference: payment.paymentNumber,
                customerId: payment.customerId,
                customerName: payment.customerName,
                amount: payment.totalAmount,
                currencyCode: payment.currencyCode,
                sourceRecordReference: payment.paymentNumber,
                notes: payment.notes || null,
                actionUrl: '/finance/ar/payments',
            });
            toast({
                title: 'Estate notified',
                description:
                    source === 'facilities'
                        ? 'Estate / Facilities has been notified of this AR payment result.'
                        : 'Estate / Property Management has been notified of this AR payment result.',
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
            case 'Posted': return <Badge className="bg-green-600">Posted</Badge>;
            case 'Void': return <Badge variant="outline" className="text-muted-foreground">Void</Badge>;
            case 'Bounced': return <Badge variant="destructive">Bounced</Badge>;
            default: return <Badge variant="secondary">{status}</Badge>;
        }
    };

    return (
        <div className="space-y-8 p-8 max-w-[1600px] mx-auto">
            <div className="flex justify-between items-center">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Customer Receipts</h1>
                    <p className="text-muted-foreground mt-2">
                        Track and manage receipts from customers and their invoice allocations.
                    </p>
                </div>
                <Button onClick={() => router.push('/finance/ar/receipts/new')}>
                    <Plus className="mr-2 h-4 w-4" /> Record Receipt
                </Button>
            </div>

            <Card>
                <CardHeader>
                    <div className="flex items-center justify-between">
                        <CardTitle>Receipt History</CardTitle>
                        <div className="flex items-center space-x-2">
                            {/* 
              <div className="relative w-64">
                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  placeholder="Search payments..."
                  className="pl-8"
                  value={searchTerm}
                  onChange={handleSearch}
                />
              </div>
              */}
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
                                    <DropdownMenuItem onClick={() => setStatusFilter('Posted')}>Posted</DropdownMenuItem>
                                    <DropdownMenuItem onClick={() => setStatusFilter('Draft')}>Draft</DropdownMenuItem>
                                    <DropdownMenuItem onClick={() => setStatusFilter('Void')}>Void</DropdownMenuItem>
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
                                    <TableHead>Receipt #</TableHead>
                                    <TableHead>Date</TableHead>
                                    <TableHead>Customer</TableHead>
                                    <TableHead>Method</TableHead>
                                    <TableHead className="text-right">Amount</TableHead>
                                    <TableHead className="text-right">Unallocated</TableHead>
                                    <TableHead>Status</TableHead>
                                    <TableHead className="w-[70px]"></TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {isLoading ? (
                                    [...Array(5)].map((_, i) => (
                                        <TableRow key={i}>
                                            <TableCell><Skeleton className="h-4 w-[100px]" /></TableCell>
                                            <TableCell><Skeleton className="h-4 w-[100px]" /></TableCell>
                                            <TableCell><Skeleton className="h-4 w-[150px]" /></TableCell>
                                            <TableCell><Skeleton className="h-4 w-[80px]" /></TableCell>
                                            <TableCell><Skeleton className="h-4 w-[80px] ml-auto" /></TableCell>
                                            <TableCell><Skeleton className="h-4 w-[80px] ml-auto" /></TableCell>
                                            <TableCell><Skeleton className="h-4 w-[60px]" /></TableCell>
                                            <TableCell><Skeleton className="h-8 w-8" /></TableCell>
                                        </TableRow>
                                    ))
                                ) : paymentsData?.items?.length === 0 ? (
                                    <TableRow>
                                        <TableCell colSpan={8} className="h-24 text-center">
                                            No customer receipts found.
                                        </TableCell>
                                    </TableRow>
                                ) : (
                                    paymentsData?.items.map((payment) => (
                                        <TableRow key={payment.id}>
                                            <TableCell className="font-medium">{payment.paymentNumber}</TableCell>
                                            <TableCell>{format(new Date(payment.paymentDate), 'MMM dd, yyyy')}</TableCell>
                                            <TableCell>{payment.customerName}</TableCell>
                                            <TableCell>{payment.paymentMethod}</TableCell>
                                            <TableCell className="text-right">{formatCurrency(payment.totalAmount, payment.currencyCode)}</TableCell>
                                            <TableCell className="text-right">
                                                {payment.unallocatedAmount > 0 ? (
                                                    <span className="text-amber-600 font-medium">{formatCurrency(payment.unallocatedAmount, payment.currencyCode)}</span>
                                                ) : (
                                                    <span className="text-muted-foreground">-</span>
                                                )}
                                            </TableCell>
                                            <TableCell>{getStatusBadge(payment.status)}</TableCell>
                                            <TableCell>
                                                <DropdownMenu>
                                                    <DropdownMenuTrigger asChild>
                                                        <Button variant="ghost" className="h-8 w-8 p-0">
                                                            <span className="sr-only">Open menu</span>
                                                            <MoreHorizontal className="h-4 w-4" />
                                                        </Button>
                                                    </DropdownMenuTrigger>
                                                    <DropdownMenuContent align="end">
                                                        <DropdownMenuLabel>Actions</DropdownMenuLabel>
                                                        {/* <DropdownMenuItem onClick={() => router.push(`/finance/ar/receipts/${payment.id}`)}>
                              View Details
                            </DropdownMenuItem> */}
                                                        {payment.unallocatedAmount > 0 && payment.status === 'Posted' && (
                                                            <DropdownMenuItem onClick={() => router.push(`/finance/ar/receipts/new?customerId=${payment.customerId}&paymentId=${payment.id}`)}>
                                                                <FileText className="mr-2 h-4 w-4" /> Allocate Receipt
                                                            </DropdownMenuItem>
                                                        )}
                                                        <DropdownMenuSeparator />
                                                        <DropdownMenuItem
                                                            disabled={estateNotifyId === `facilities:${payment.id}`}
                                                            onClick={() => notifyEstatePaymentResult(payment, 'facilities')}
                                                        >
                                                            Notify Estate / Facilities
                                                        </DropdownMenuItem>
                                                        <DropdownMenuItem
                                                            disabled={estateNotifyId === `property-management:${payment.id}`}
                                                            onClick={() => notifyEstatePaymentResult(payment, 'property-management')}
                                                        >
                                                            Notify Estate / Property Mgnt
                                                        </DropdownMenuItem>
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
                    {paymentsData && paymentsData.totalPages > 1 && (
                        <div className="flex items-center justify-end space-x-2 py-4">
                            <Button
                                variant="outline"
                                size="sm"
                                onClick={() => setPage((p) => Math.max(1, p - 1))}
                                disabled={!paymentsData.hasPreviousPage}
                            >
                                Previous
                            </Button>
                            <div className="text-sm text-muted-foreground">
                                Page {page} of {paymentsData.totalPages}
                            </div>
                            <Button
                                variant="outline"
                                size="sm"
                                onClick={() => setPage((p) => Math.min(paymentsData.totalPages, p + 1))}
                                disabled={!paymentsData.hasNextPage}
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
