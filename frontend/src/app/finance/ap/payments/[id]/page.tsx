'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import {
    ArrowLeft,
    Printer,
    FileText,
    Loader2,
    Send,
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
import { useToast } from '@/components/ui/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { InvoicePaymentSodControl } from '@/components/finance/InvoicePaymentSodControl';

export default function VendorPaymentDetailsPage() {
    const router = useRouter();
    const params = useParams();
    const id = params.id as string;
    const { toast } = useToast();
    const { hasPermission } = useAuth();
    const [isPosting, setIsPosting] = useState(false);
    const [isSubmitting, setIsSubmitting] = useState(false);

    const { data: payment, isLoading, refetch } = useQuery({
        queryKey: ['vendor-payment', id],
        queryFn: () => accountsPayableService.getPayment(id),
    });

    const {
        data: sodReadiness,
        isLoading: isSodLoading,
        error: sodError,
        refetch: refetchSod,
    } = useQuery({
        queryKey: ['vendor-payment-sod-readiness', id],
        queryFn: () => accountsPayableService.getPaymentSodReadiness(id),
        enabled: Boolean(id),
        retry: false,
    });

    const handleSubmit = async () => {
        if (!payment) return;
        setIsSubmitting(true);
        try {
            await accountsPayableService.submitPayment(payment.id);
            toast({ title: 'Submitted', description: 'Vendor payment sent to the shared authorization workflow.' });
            await Promise.all([refetch(), refetchSod()]);
        } catch (error: any) {
            toast({
                title: 'Submission failed',
                description: error.message || 'Unable to submit the vendor payment.',
                variant: 'destructive',
            });
        } finally {
            setIsSubmitting(false);
        }
    };

    const handlePost = async () => {
        if (!payment) return;

        setIsPosting(true);
        try {
            await accountsPayableService.postPayment(payment.id);
            toast({ title: 'Success', description: 'Vendor payment posted successfully.' });
            await refetch();
        } catch (error: any) {
            toast({
                title: 'Posting failed',
                description: error.message || 'Unable to post vendor payment.',
                variant: 'destructive',
            });
        } finally {
            setIsPosting(false);
        }
    };

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
            case 'PendingAuthorization': return <Badge className="bg-amber-600">Pending Authorization</Badge>;
            case 'Authorized': return <Badge className="bg-emerald-600">Authorized</Badge>;
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
                    {payment.status === 'Draft' && !payment.paymentBatchId && hasPermission('Finance.AP.Payments.Process') && (
                        <Button size="sm" onClick={handleSubmit} disabled={isSubmitting}>
                            {isSubmitting
                                ? <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                : <Send className="mr-2 h-4 w-4" />}
                            Submit for Authorization
                        </Button>
                    )}
                    {!payment.journalEntryId && ['Authorized', 'Processed'].includes(payment.status) && hasPermission('Finance.AP.Payments.Process') && (
                        <Button size="sm" onClick={handlePost} disabled={isPosting}>
                            {isPosting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Post Payment
                        </Button>
                    )}
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

            <InvoicePaymentSodControl
                readiness={sodReadiness}
                isLoading={isSodLoading}
                error={sodError instanceof Error ? sodError.message : sodError ? 'Unable to load the AP-004 control.' : null}
            />

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
                            <p className="font-bold text-2xl">{formatCurrency(payment.totalAmount, payment.currencyCode)}</p>
                        </div>
                        <div>
                            <p className="text-xs text-muted-foreground uppercase font-bold mb-2">Method</p>
                            <p className="font-medium">{payment.paymentMethod}</p>
                            {payment.transactionReference && <p className="text-sm text-muted-foreground mt-1">Ref: {payment.transactionReference}</p>}
                        </div>
                        <div>
                            <p className="text-xs text-muted-foreground uppercase font-bold mb-2">Allocated</p>
                            <p className="font-semibold text-green-600">{formatCurrency(payment.allocatedAmount, payment.currencyCode)}</p>
                        </div>
                        <div>
                            <p className="text-xs text-muted-foreground uppercase font-bold mb-2">WHT Withheld</p>
                            <p className={payment.withholdingTaxAmount > 0 ? 'font-semibold text-orange-600' : 'font-semibold text-muted-foreground'}>
                                {payment.withholdingTaxAmount > 0 ? formatCurrency(payment.withholdingTaxAmount, payment.currencyCode) : '-'}
                            </p>
                        </div>
                        <div>
                            <p className="text-xs text-muted-foreground uppercase font-bold mb-2">Unallocated</p>
                            <p className={`font-semibold ${payment.unallocatedAmount > 0 ? 'text-amber-600' : 'text-muted-foreground'}`}>
                                {formatCurrency(payment.unallocatedAmount, payment.currencyCode)}
                            </p>
                        </div>
                    </div>

                    {/* Allocations Table */}
                    <div className="pt-8 border-t">
                        <h3 className="text-lg font-semibold mb-4">Invoices Paid</h3>
                        <div className="rounded-md border">
                            <div className="grid grid-cols-12 gap-4 p-4 bg-muted/50 text-xs font-bold uppercase text-muted-foreground border-b">
                                <div className="col-span-3">Invoice #</div>
                                <div className="col-span-3">Allocation Date</div>
                                <div className="col-span-2 text-right">Discount Taken</div>
                                <div className="col-span-2 text-right">WHT Withheld</div>
                                <div className="col-span-2 text-right">Amount Applied</div>
                            </div>
                            {payment.allocations?.map((alloc, index) => (
                                <div key={alloc.id || index} className="grid grid-cols-12 gap-4 p-4 border-b last:border-0 text-sm items-center">
                                    <div className="col-span-3 font-medium text-blue-600 hover:underline cursor-pointer" onClick={() => router.push(`/finance/ap/invoices/${alloc.vendorInvoiceId}`)}>
                                        {alloc.invoiceNumber}
                                    </div>
                                    <div className="col-span-3 text-muted-foreground">
                                        {format(new Date(alloc.allocationDate), 'MMM dd, yyyy')}
                                    </div>
                                    <div className="col-span-2 text-right text-muted-foreground">
                                        {alloc.discountAmount > 0 ? formatCurrency(alloc.discountAmount, payment.currencyCode) : '-'}
                                    </div>
                                    <div className="col-span-2 text-right text-orange-600">
                                        {alloc.withholdingTaxAmount > 0 ? formatCurrency(alloc.withholdingTaxAmount, payment.currencyCode) : '-'}
                                    </div>
                                    <div className="col-span-2 text-right font-medium">
                                        {formatCurrency(alloc.allocatedAmount, payment.currencyCode)}
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
