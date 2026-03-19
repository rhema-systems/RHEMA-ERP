'use client';

import { useState, useEffect } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
    ArrowLeft,
    Loader2,
    Calendar as CalendarIcon,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
    Card,
    CardContent,
    CardFooter,
    CardHeader,
    CardTitle,
} from '@/components/ui/card';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from '@/components/ui/select';
import {
    Popover,
    PopoverContent,
    PopoverTrigger,
} from '@/components/ui/popover';
import { Calendar } from '@/components/ui/calendar';
import { accountsPayableService } from '@/services/accountsPayableService';
import { businessPartnerService } from '@/services/businessPartnerService';
import { useToast } from '@/components/ui/use-toast';
import { formatCurrency, cn } from '@/lib/utils';
import { useQuery, useMutation } from '@tanstack/react-query';
import { Skeleton } from '@/components/ui/skeleton';
import { format } from 'date-fns';

const paymentSchema = z.object({
    supplierId: z.string().min(1, 'Supplier is required'),
    paymentDate: z.date(),
    totalAmount: z.coerce.number().min(0.01, 'Amount must be positive'),
    paymentMethod: z.enum(['BankTransfer', 'Cheque', 'Cash', 'WireTransfer', 'MobileMoney', 'DirectDebit', 'Other']).default('BankTransfer'),
    transactionReference: z.string().optional(),
    currencyCode: z.string().default('USD'),
    notes: z.string().optional(),
});

type PaymentFormValues = z.infer<typeof paymentSchema>;

export default function NewVendorPaymentPage() {
    const router = useRouter();
    const searchParams = useSearchParams();
    const preselectedSupplierId = searchParams.get('supplierId');
    const preselectedInvoiceId = searchParams.get('invoiceId');
    const { toast } = useToast();
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [allocations, setAllocations] = useState<Record<string, number>>({});
    const [createdPaymentId, setCreatedPaymentId] = useState<string | null>(null);

    // Fetch suppliers
    const { data: suppliersData } = useQuery({
        queryKey: ['suppliers'],
        queryFn: () => businessPartnerService.getPartners({ partnerType: 'Supplier', pageSize: 100 }),
    });

    const form = useForm<PaymentFormValues>({
        // @ts-expect-error TODO: fix type
        resolver: zodResolver(paymentSchema),
        defaultValues: {
            supplierId: preselectedSupplierId || '',
            paymentDate: new Date(),
            totalAmount: 0,
            paymentMethod: 'BankTransfer',
            currencyCode: 'USD',
            notes: '',
        },
    });

    const selectedSupplierId = form.watch('supplierId');

    // Fetch outstanding invoices for selected supplier
    const { data: outstandingInvoices, isLoading: isLoadingInvoices } = useQuery({
        queryKey: ['outstanding-vendor-invoices', selectedSupplierId],
        queryFn: () => accountsPayableService.getOutstandingInvoices(selectedSupplierId),
        enabled: !!selectedSupplierId,
    });

    // Pre-fill amount if invoice selected
    useEffect(() => {
        if (preselectedInvoiceId && outstandingInvoices) {
            const invoice = outstandingInvoices.find(inv => inv.invoiceId === preselectedInvoiceId);
            if (invoice) {
                form.setValue('totalAmount', invoice.balanceAmount);
                setAllocations({ [invoice.invoiceId]: invoice.balanceAmount });
            }
        }
    }, [preselectedInvoiceId, outstandingInvoices, form]);


    const allocatePaymentMutation = useMutation({
        mutationFn: async (paymentId: string) => {
            // Process allocations sequentially or concurrently
            const allocationPromises = Object.entries(allocations).map(([invoiceId, amount]) => {
                if (amount <= 0) return Promise.resolve();
                return accountsPayableService.allocatePayment(paymentId, {
                    vendorInvoiceId: invoiceId,
                    allocatedAmount: amount
                });
            });
            await Promise.all(allocationPromises);
        },
        onSuccess: () => {
            toast({ title: 'Success', description: 'Vendor payment allocated successfully' });
            router.push('/finance/ap/payments');
        },
        onError: () => {
            toast({ title: 'Error', description: 'Failed to allocate payment', variant: 'destructive' });
        }
    });

    const onSubmit = async (data: PaymentFormValues) => {
        setIsSubmitting(true);
        try {
            // 1. Create Payment
            const payment = await accountsPayableService.createPayment({
                ...data,
                paymentDate: data.paymentDate.toISOString(),
            });

            setCreatedPaymentId(payment.id);

            // 2. Allocate if any allocations set
            const totalAllocated = Object.values(allocations).reduce((a, b) => a + b, 0);
            if (totalAllocated > 0) {
                await allocatePaymentMutation.mutateAsync(payment.id);
            } else {
                toast({ title: 'Success', description: 'Vendor payment recorded (unallocated)' });
                router.push('/finance/ap/payments');
            }

        } catch (error: any) {
            toast({
                title: 'Error',
                description: error.message || 'Failed to process vendor payment',
                variant: 'destructive',
            });
        } finally {
            setIsSubmitting(false);
        }
    };

    const currentAmount = form.watch('totalAmount');
    const totalAllocated = Object.values(allocations).reduce((acc, curr) => acc + curr, 0);
    const remainingAmount = currentAmount - totalAllocated;

    const handleAutoAllocate = () => {
        if (!outstandingInvoices) return;
        let remaining = currentAmount;
        const newAllocations: Record<string, number> = {};

        // Allocate to oldest invoices first
        const sortedInvoices = [...outstandingInvoices].sort((a, b) => new Date(a.dueDate || a.invoiceDate).getTime() - new Date(b.dueDate || b.invoiceDate).getTime());

        for (const inv of sortedInvoices) {
            if (remaining <= 0) break;
            const allocateAmount = Math.min(remaining, inv.balanceAmount);
            newAllocations[inv.invoiceId] = allocateAmount;
            remaining -= allocateAmount;
        }
        setAllocations(newAllocations);
    };

    return (
        <div className="space-y-8 p-8 max-w-[1200px] mx-auto">
            <div className="flex items-center space-x-4">
                <Button variant="ghost" size="icon" onClick={() => router.back()}>
                    <ArrowLeft className="h-4 w-4" />
                </Button>
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Record Vendor Payment</h1>
                    <p className="text-muted-foreground">
                        Post a payment made to a supplier and allocate it to outstanding bills.
                    </p>
                </div>
            </div>

            <div className="grid gap-8 md:grid-cols-3">
                {/* Payment Details Form */}
                <Card className="md:col-span-1 h-fit">
                    <CardHeader>
                        <CardTitle>Payment Details</CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                        <form id="payment-form" onSubmit={form.handleSubmit(onSubmit as any)} className="space-y-4">
                            <div className="space-y-2">
                                <Label htmlFor="supplier">Supplier</Label>
                                <Select
                                    onValueChange={(val) => form.setValue('supplierId', val)}
                                    defaultValue={preselectedSupplierId || ''}
                                    disabled={!!createdPaymentId}
                                >
                                    <SelectTrigger>
                                        <SelectValue placeholder="Select supplier..." />
                                    </SelectTrigger>
                                    <SelectContent>
                                        {suppliersData?.items?.map((supplier: any) => (
                                            <SelectItem key={supplier.id} value={supplier.id}>
                                                {supplier.name}
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                {form.formState.errors.supplierId && (
                                    <p className="text-sm text-red-500">{form.formState.errors.supplierId.message}</p>
                                )}
                            </div>

                            <div className="space-y-2">
                                <Label>Payment Date</Label>
                                <Controller
                                    control={form.control}
                                    name="paymentDate"
                                    render={({ field }) => (
                                        <Popover>
                                            <PopoverTrigger asChild>
                                                <Button
                                                    variant="outline"
                                                    className={cn(
                                                        "w-full justify-start text-left font-normal",
                                                        !field.value && "text-muted-foreground"
                                                    )}
                                                    disabled={!!createdPaymentId}
                                                >
                                                    <CalendarIcon className="mr-2 h-4 w-4" />
                                                    {field.value ? format(field.value, "PPP") : <span>Pick a date</span>}
                                                </Button>
                                            </PopoverTrigger>
                                            <PopoverContent className="w-auto p-0">
                                                <Calendar
                                                    mode="single"
                                                    selected={field.value}
                                                    onSelect={field.onChange}
                                                    initialFocus
                                                />
                                            </PopoverContent>
                                        </Popover>
                                    )}
                                />
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="amount">Amount Paid</Label>
                                <div className="relative">
                                    <span className="absolute left-3 top-2.5 text-gray-500">$</span>
                                    <Input
                                        id="amount"
                                        type="number"
                                        className="pl-7"
                                        step="0.01"
                                        {...form.register('totalAmount')}
                                        disabled={!!createdPaymentId}
                                    />
                                </div>
                                {form.formState.errors.totalAmount && (
                                    <p className="text-sm text-red-500">{form.formState.errors.totalAmount.message}</p>
                                )}
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="paymentMethod">Payment Method</Label>
                                <Select
                                    onValueChange={(val: any) => form.setValue('paymentMethod', val)}
                                    defaultValue="BankTransfer"
                                    disabled={!!createdPaymentId}
                                >
                                    <SelectTrigger>
                                        <SelectValue />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="BankTransfer">Bank Transfer</SelectItem>
                                        <SelectItem value="Cheque">Cheque</SelectItem>
                                        <SelectItem value="Cash">Cash</SelectItem>
                                        <SelectItem value="WireTransfer">Wire Transfer</SelectItem>
                                        <SelectItem value="MobileMoney">Mobile Money</SelectItem>
                                    </SelectContent>
                                </Select>
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="reference">Reference #</Label>
                                <Input id="reference" placeholder="e.g. Cheque No. / Receipt" {...form.register('transactionReference')} disabled={!!createdPaymentId} />
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="notes">Notes</Label>
                                <Textarea id="notes" {...form.register('notes')} disabled={!!createdPaymentId} />
                            </div>
                        </form>
                    </CardContent>
                    <CardFooter>
                        {!createdPaymentId && (
                            <Button type="submit" form="payment-form" className="w-full" disabled={isSubmitting}>
                                {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                                Record Payment
                            </Button>
                        )}
                    </CardFooter>
                </Card>

                {/* Allocation Section */}
                <Card className="md:col-span-2">
                    <CardHeader className="flex flex-row items-center justify-between">
                        <CardTitle>Allocate to Bills</CardTitle>
                        <Button variant="outline" size="sm" onClick={handleAutoAllocate} disabled={!!createdPaymentId || !outstandingInvoices || outstandingInvoices.length === 0}>
                            Auto Allocate
                        </Button>
                    </CardHeader>
                    <CardContent>
                        {!selectedSupplierId ? (
                            <div className="text-center py-12 text-muted-foreground">
                                Select a supplier to view outstanding bills.
                            </div>
                        ) : isLoadingInvoices ? (
                            <div className="space-y-4">
                                <Skeleton className="h-12 w-full" />
                                <Skeleton className="h-12 w-full" />
                                <Skeleton className="h-12 w-full" />
                            </div>
                        ) : !outstandingInvoices || outstandingInvoices.length === 0 ? (
                            <div className="text-center py-12 text-muted-foreground">
                                No outstanding bills found for this supplier.
                            </div>
                        ) : (
                            <div className="space-y-4">
                                <div className="flex justify-between items-center bg-muted/50 p-4 rounded-lg font-medium">
                                    <span>Remaining to Allocate:</span>
                                    <span className={remainingAmount < 0 ? 'text-red-500' : 'text-green-600'}>
                                        {formatCurrency(remainingAmount)}
                                    </span>
                                </div>

                                <div className="border rounded-md">
                                    <table className="w-full text-sm">
                                        <thead className="bg-muted text-muted-foreground">
                                            <tr>
                                                <th className="p-3 text-left">Bill #</th>
                                                <th className="p-3 text-left">Due Date</th>
                                                <th className="p-3 text-right">Balance Due</th>
                                                <th className="p-3 text-right w-[150px]">Allocate</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {outstandingInvoices.map((inv) => (
                                                <tr key={inv.invoiceId} className="border-t">
                                                    <td className="p-3 font-medium flex flex-col items-start gap-1">
                                                        <span>{inv.invoiceNumber}</span>
                                                        {inv.supplierInvoiceNumber && (
                                                            <span className="text-xs text-muted-foreground">Ref: {inv.supplierInvoiceNumber}</span>
                                                        )}
                                                    </td>
                                                    <td className="p-3">
                                                        {inv.dueDate ? format(new Date(inv.dueDate), 'MMM dd, yyyy') : '-'}
                                                        {inv.dueDate && new Date(inv.dueDate) < new Date() && (
                                                            <span className="ml-2 text-xs text-red-500 font-bold">Overdue</span>
                                                        )}
                                                    </td>
                                                    <td className="p-3 text-right">{formatCurrency(inv.balanceAmount)}</td>
                                                    <td className="p-3">
                                                        <Input
                                                            type="number"
                                                            className="text-right h-8"
                                                            min={0}
                                                            max={inv.balanceAmount}
                                                            value={allocations[inv.invoiceId] || ''}
                                                            onChange={(e) => {
                                                                const val = Number(e.target.value);
                                                                setAllocations(prev => ({
                                                                    ...prev,
                                                                    [inv.invoiceId]: val
                                                                }));
                                                            }}
                                                            disabled={!!createdPaymentId}
                                                        />
                                                    </td>
                                                </tr>
                                            ))}
                                        </tbody>
                                    </table>
                                </div>
                            </div>
                        )}
                    </CardContent>
                </Card>
            </div>
        </div>
    );
}
