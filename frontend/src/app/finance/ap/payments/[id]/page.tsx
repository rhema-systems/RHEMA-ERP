'use client';

import { useParams, useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import {
    ArrowLeft,
    Printer,
    FileText,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
    Card,
    CardContent,
    CardHeader,
    CardTitle,
} from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Separator } from '@/components/ui/separator';
import { accountsPayableService } from '@/services/accountsPayableService';
import { formatCurrency } from '@/lib/utils';
import { format } from 'date-fns';
import { Skeleton } from '@/components/ui/skeleton';

export default function VendorPaymentDetailsPage() {
    const router = useRouter();
    const params = useParams();
    const id = params.id as string;

    const { data: payment, isLoading } = useQuery({
        queryKey: ['vendor-payment', id],
        queryFn: () => accountsPayableService.getPayment(id),
    });

    if (isLoading) {
        return <PaymentDetailsSkeleton />;
    }

    if (!payment) {
        return (
            <div className="p-8 text-center">
                <h2 className="text-xl font-semibold">Vendor Payment not found</h2>
                <Button variant="link" onClick={() => router.push('/finance/ap/payments')}>
                    Return to list
                </Button>
            </div>
        );
    }

    const getStatusBadge = (status: string) => {
        switch (status) {
            case 'Draft': return <Badge variant="secondary">Draft</Badge>;
            case 'Processed': return <Badge className="bg-blue-600">Processed</Badge>;
            case 'Cleared': return <Badge className="bg-green-600">Cleared</Badge>;
            case 'Voided': return <Badge variant="outline" className="text-muted-foreground">Voided</Badge>;
            case 'Failed': return <Badge variant="destructive">Failed</Badge>;
            default: return <Badge variant="secondary">{status}</Badge>;
        }
    };

    return (
        <div className="space-y-8 p-8 max-w-[1000px] mx-auto">
            {/* Header Actions */}
            <div className="flex items-center justify-between no-print">
                <div className="flex items-center space-x-4">
                    <Button variant="ghost" size="icon" onClick={() => router.push('/finance/ap/payments')}>
                        <ArrowLeft className="h-4 w-4" />
                    </Button>
                    <div className="flex items-center space-x-2">
                        <h1 className="text-2xl font-bold tracking-tight">Payment {payment.paymentNumber}</h1>
                        {getStatusBadge(payment.status)}
                    </div>
                </div>
                <div className="flex space-x-2">
                    <Button variant="outline" size="sm" onClick={() => window.print()}>
                        <Printer className="mr-2 h-4 w-4" /> Print Receipt
                    </Button>
                    {payment.unallocatedAmount > 0 && payment.status === 'Processed' && (
                        <Button size="sm" onClick={() => router.push(`/finance/ap/payments/create?supplierId=${payment.supplierId}&paymentId=${payment.id}`)}>
                            <FileText className="mr-2 h-4 w-4" /> Allocate Options
                        </Button>
                    )}
                </div>
            </div>

            <Card className="print:shadow-none print:border-none">
                <CardHeader className="flex flex-row justify-between items-start border-b pb-8">
                    <div className="space-y-2">
                        <div className="flex items-center space-x-2">
                            <span className="text-xl font-bold">{payment.supplierName}</span>
                        </div>
                        <p className="text-sm text-muted-foreground mt-1">
                            Supplier ID: {payment.supplierId}
                        </p>
                    </div>
                    <div className="text-right space-y-1">
                        <h2 className="text-3xl font-bold text-gray-200 uppercase tracking-widest">PAYMENT</h2>
                        <div className="flex justify-end space-x-4 pt-4">
                            <div className="text-left">
                                <p className="text-xs text-muted-foreground uppercase font-bold">Payment #</p>
                                <p className="font-medium">{payment.paymentNumber}</p>
                            </div>
                            <div className="text-left">
                                <p className="text-xs text-muted-foreground uppercase font-bold">Date</p>
                                <p className="font-medium">{format(new Date(payment.paymentDate), 'MMM dd, yyyy')}</p>
                            </div>
                        </div>
                    </div>
                </CardHeader>
                <CardContent className="pt-8 space-y-8">
                    {/* Payment Info */}
                    <div className="grid grid-cols-2 md:grid-cols-4 gap-8">
                        <div>
                            <p className="text-xs text-muted-foreground uppercase font-bold mb-2">Amount Paid</p>
                            <p className="font-bold text-2xl">{formatCurrency(payment.totalAmount)}</p>
                        </div>
                        <div>
                            <p className="text-xs text-muted-foreground uppercase font-bold mb-2">Method</p>
                            <p className="font-medium">{payment.paymentMethod}</p>
                            {payment.transactionReference && <p className="text-sm text-muted-foreground mt-1">Ref: {payment.transactionReference}</p>}
                        </div>
                        <div>
                            <p className="text-xs text-muted-foreground uppercase font-bold mb-2">Allocated</p>
                            <p className="font-semibold text-green-600">{formatCurrency(payment.allocatedAmount)}</p>
                        </div>
                        <div>
                            <p className="text-xs text-muted-foreground uppercase font-bold mb-2">Unallocated</p>
                            <p className={`font-semibold ${payment.unallocatedAmount > 0 ? 'text-amber-600' : 'text-muted-foreground'}`}>
                                {formatCurrency(payment.unallocatedAmount)}
                            </p>
                        </div>
                    </div>

                    {/* Allocations Table */}
                    <div className="pt-8 border-t">
                        <h3 className="text-lg font-semibold mb-4">Invoices Paid</h3>
                        <div className="rounded-md border">
                            <div className="grid grid-cols-12 gap-4 p-4 bg-muted/50 text-xs font-bold uppercase text-muted-foreground border-b">
                                <div className="col-span-4">Invoice #</div>
                                <div className="col-span-3">Allocation Date</div>
                                <div className="col-span-3 text-right">Discount Taken</div>
                                <div className="col-span-2 text-right">Amount Applied</div>
                            </div>
                            {payment.allocations?.map((alloc, index) => (
                                <div key={alloc.id || index} className="grid grid-cols-12 gap-4 p-4 border-b last:border-0 text-sm items-center">
                                    <div className="col-span-4 font-medium text-blue-600 hover:underline cursor-pointer" onClick={() => router.push(`/finance/ap/invoices/${alloc.vendorInvoiceId}`)}>
                                        {alloc.invoiceNumber}
                                    </div>
                                    <div className="col-span-3 text-muted-foreground">
                                        {format(new Date(alloc.allocationDate), 'MMM dd, yyyy')}
                                    </div>
                                    <div className="col-span-3 text-right text-muted-foreground">
                                        {alloc.discountAmount > 0 ? formatCurrency(alloc.discountAmount) : '-'}
                                    </div>
                                    <div className="col-span-2 text-right font-medium">
                                        {formatCurrency(alloc.allocatedAmount)}
                                    </div>
                                </div>
                            ))}
                            {(!payment.allocations || payment.allocations.length === 0) && (
                                <div className="p-4 text-center text-muted-foreground text-sm">
                                    No invoices have been allocated to this payment yet.
                                </div>
                            )}
                        </div>
                    </div>

                    {/* Notes */}
                    {payment.notes && (
                        <div className="pt-8 border-t">
                            <p className="text-xs text-muted-foreground uppercase font-bold mb-2">Notes</p>
                            <p className="text-sm text-muted-foreground">{payment.notes}</p>
                        </div>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}

function PaymentDetailsSkeleton() {
    return (
        <div className="space-y-8 p-8 max-w-[1000px] mx-auto">
            <div className="flex justify-between">
                <Skeleton className="h-10 w-32" />
                <Skeleton className="h-10 w-64" />
            </div>
            <Skeleton className="h-[600px] w-full" />
        </div>
    )
}
