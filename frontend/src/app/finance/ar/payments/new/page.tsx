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
    Check,
    AlertCircle
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
    Card,
    CardContent,
    CardDescription,
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
import { arService } from '@/services/ar-service';
import { useToast } from '@/components/ui/use-toast';
import { formatCurrency, cn } from '@/lib/utils';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Checkbox } from '@/components/ui/checkbox';
import { Skeleton } from '@/components/ui/skeleton';
import { format } from 'date-fns';

const paymentSchema = z.object({
    customerId: z.string().min(1, 'Customer is required'),
    paymentDate: z.date(),
    totalAmount: z.coerce.number().min(0.01, 'Amount must be positive'),
    paymentMethod: z.string().min(1, 'Payment method is required'),
    referenceNumber: z.string().optional(),
    currencyCode: z.string().default('USD'),
    notes: z.string().optional(),
});

type PaymentFormValues = z.infer<typeof paymentSchema>;

export default function NewPaymentPage() {
    const router = useRouter();
    const searchParams = useSearchParams();
    const preselectedCustomerId = searchParams.get('customerId');
    const preselectedInvoiceId = searchParams.get('invoiceId');
    const { toast } = useToast();
    const queryClient = useQueryClient();
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [allocations, setAllocations] = useState<Record<string, number>>({});
    const [discountAllocations, setDiscountAllocations] = useState<Record<string, number>>({});
    const [createdPaymentId, setCreatedPaymentId] = useState<string | null>(null);

    // Fetch customers
    const { data: customersData } = useQuery({
        queryKey: ['customers-list'],
        queryFn: () => arService.getCustomers({ pageSize: 100 }),
    });

    const form = useForm<PaymentFormValues>({
        resolver: zodResolver(paymentSchema) as any,
        defaultValues: {
            customerId: preselectedCustomerId || '',
            paymentDate: new Date(),
            totalAmount: 0,
            paymentMethod: 'Bank Transfer',
            currencyCode: 'USD',
            notes: '',
        },
    });

    const selectedCustomerId = form.watch('customerId');

    // Fetch outstanding invoices for selected customer
    const { data: outstandingInvoices, isLoading: isLoadingInvoices } = useQuery({
        queryKey: ['outstanding-invoices', selectedCustomerId],
        queryFn: () => arService.getOutstandingInvoices(selectedCustomerId),
        enabled: !!selectedCustomerId,
    });

    // Pre-fill amount if invoice selected
    useEffect(() => {
        if (preselectedInvoiceId && outstandingInvoices) {
            const invoice = outstandingInvoices.find(inv => inv.id === preselectedInvoiceId);
            if (invoice) {
                form.setValue('totalAmount', invoice.balanceAmount);
                // Auto-allocate logic could go here, but let's keep it manual for explicit confirmation
                setAllocations({ [invoice.id]: invoice.balanceAmount });
                setDiscountAllocations({ [invoice.id]: 0 });
            }
        }
    }, [preselectedInvoiceId, outstandingInvoices, form]);


    const createPaymentMutation = useMutation({
        mutationFn: (data: PaymentFormValues) => arService.createPayment({
            ...data,
            paymentDate: data.paymentDate.toISOString(),
        }),
        onSuccess: (data) => {
            setCreatedPaymentId(data.id);
            toast({
                title: 'Payment Recorded',
                description: 'Now proceeding to allocation.',
            });
        },
        onError: (error: any) => {
            toast({
                title: 'Error',
                description: error.message || 'Failed to record payment',
                variant: 'destructive',
            });
        }
    });

    const allocatePaymentMutation = useMutation({
        mutationFn: async (paymentId: string) => {
            const invoiceIds = new Set([
                ...Object.keys(allocations),
                ...Object.keys(discountAllocations),
            ]);

            const allocationRows = Array.from(invoiceIds)
                .map(invoiceId => ({
                    invoiceId,
                    allocatedAmount: Number(allocations[invoiceId]) || 0,
                    discountAmount: Number(discountAllocations[invoiceId]) || 0,
                }))
                .filter(row => row.allocatedAmount > 0 || row.discountAmount > 0);

            if (allocationRows.length === 0) return;

            await arService.allocatePayment({
                customerPaymentId: paymentId,
                allocations: allocationRows,
            });
        },
        onSuccess: () => {
            toast({ title: 'Success', description: 'Payment allocated successfully' });
            router.push('/finance/ar/payments');
        },
        onError: (error: any) => {
            toast({ title: 'Error', description: 'Failed to allocate payment', variant: 'destructive' });
        }
    });

    const onSubmit = async (data: PaymentFormValues) => {
        setIsSubmitting(true);
        try {
            // 1. Create Payment
            const payment = await arService.createPayment({
                ...data,
                paymentDate: data.paymentDate.toISOString(),
            });

            setCreatedPaymentId(payment.id);

            // 2. Allocate if any allocations set
            const totalAllocated = Object.values(allocations).reduce((a, b) => a + b, 0);
            const totalDiscounts = Object.values(discountAllocations).reduce((a, b) => a + b, 0);
            if (totalAllocated > 0 || totalDiscounts > 0) {
                await allocatePaymentMutation.mutateAsync(payment.id);
            } else {
                toast({ title: 'Success', description: 'Payment recorded (unallocated)' });
                router.push('/finance/ar/payments');
            }

        } catch (error: any) {
            toast({
                title: 'Error',
                description: error.message || 'Failed to process',
                variant: 'destructive',
            });
        } finally {
            setIsSubmitting(false);
        }
    };

    const currentAmount = form.watch('totalAmount');
    const currentCurrencyCode = form.watch('currencyCode') || 'GHS';
    const totalAllocated = Object.values(allocations).reduce((acc, curr) => acc + curr, 0);
    const totalDiscounts = Object.values(discountAllocations).reduce((acc, curr) => acc + curr, 0);
    const remainingAmount = currentAmount - totalAllocated;

    const handleAutoAllocate = () => {
        if (!outstandingInvoices) return;
        let remaining = currentAmount;
        const newAllocations: Record<string, number> = {};

        // Allocate to oldest invoices first
        const sortedInvoices = [...outstandingInvoices].sort((a, b) => new Date(a.dueDate).getTime() - new Date(b.dueDate).getTime());

        for (const inv of sortedInvoices) {
            if (remaining <= 0) break;
            const allocateAmount = Math.min(remaining, inv.balanceAmount);
            newAllocations[inv.id] = allocateAmount;
            remaining -= allocateAmount;
        }
        setAllocations(newAllocations);
        setDiscountAllocations({});
    };

    return (
        <div className="space-y-8 p-8 max-w-[1200px] mx-auto">
            <div className="flex items-center space-x-4">
                <Button variant="ghost" size="icon" onClick={() => router.back()}>
                    <ArrowLeft className="h-4 w-4" />
                </Button>
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Record Payment</h1>
                    <p className="text-muted-foreground">
                        Receive payment from customer and allocate to invoices.
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
                        <form id="payment-form" onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
                            <div className="space-y-2">
                                <Label htmlFor="customer">Customer</Label>
                                <Select
                                    onValueChange={(val) => {
                                        form.setValue('customerId', val);
                                        const customer = customersData?.items.find(c => c.id === val);
                                        if (customer?.currencyCode) {
                                            form.setValue('currencyCode', customer.currencyCode);
                                        }
                                    }}
                                    defaultValue={preselectedCustomerId || ''}
                                    disabled={!!createdPaymentId} // Disable after creation
                                >
                                    <SelectTrigger>
                                        <SelectValue placeholder="Select customer" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        {customersData?.items.map((customer) => (
                                            <SelectItem key={customer.id} value={customer.id}>
                                                {customer.customerName}
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                {form.formState.errors.customerId && (
                                    <p className="text-sm text-red-500">{form.formState.errors.customerId.message}</p>
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
                                <Label htmlFor="amount">Amount Received</Label>
                                <div className="relative">
                                    <span className="absolute left-3 top-2.5 text-gray-500">{currentCurrencyCode}</span>
                                    <Input
                                        id="amount"
                                        type="number"
                                        className="pl-14"
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
                                    onValueChange={(val) => form.setValue('paymentMethod', val)}
                                    defaultValue="Bank Transfer"
                                    disabled={!!createdPaymentId}
                                >
                                    <SelectTrigger>
                                        <SelectValue />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="Bank Transfer">Bank Transfer</SelectItem>
                                        <SelectItem value="Cash">Cash</SelectItem>
                                        <SelectItem value="Cheque">Cheque</SelectItem>
                                        <SelectItem value="Credit Card">Credit Card</SelectItem>
                                    </SelectContent>
                                </Select>
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="reference">Reference #</Label>
                                <Input id="reference" {...form.register('referenceNumber')} disabled={!!createdPaymentId} />
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
                                Process Payment
                            </Button>
                        )}
                    </CardFooter>
                </Card>

                {/* Allocation Section */}
                <Card className="md:col-span-2">
                    <CardHeader className="flex flex-row items-center justify-between">
                        <CardTitle>Allocate to Invoices</CardTitle>
                        <Button variant="outline" size="sm" onClick={handleAutoAllocate} disabled={!!createdPaymentId}>
                            Auto Allocate
                        </Button>
                    </CardHeader>
                    <CardContent>
                        {!selectedCustomerId ? (
                            <div className="text-center py-12 text-muted-foreground">
                                Select a customer to view outstanding invoices.
                            </div>
                        ) : isLoadingInvoices ? (
                            <div className="space-y-4">
                                <Skeleton className="h-12 w-full" />
                                <Skeleton className="h-12 w-full" />
                                <Skeleton className="h-12 w-full" />
                            </div>
                        ) : !outstandingInvoices || outstandingInvoices.length === 0 ? (
                            <div className="text-center py-12 text-muted-foreground">
                                No outstanding invoices found for this customer.
                            </div>
                        ) : (
                            <div className="space-y-4">
                                <div className="flex justify-between items-center bg-muted/50 p-4 rounded-lg font-medium">
                                    <div>
                                        <div>Remaining Cash to Allocate:</div>
                                        {totalDiscounts > 0 && (
                                            <div className="text-xs text-muted-foreground">
                                                Discounts allowed: {formatCurrency(totalDiscounts)}
                                            </div>
                                        )}
                                    </div>
                                    <span className={remainingAmount < 0 ? 'text-red-500' : 'text-green-600'}>
                                        {formatCurrency(remainingAmount)}
                                    </span>
                                </div>

                                <div className="border rounded-md">
                                    <table className="w-full text-sm">
                                        <thead className="bg-muted text-muted-foreground">
                                            <tr>
                                                <th className="p-3 text-left">Invoice</th>
                                                <th className="p-3 text-left">Date</th>
                                                <th className="p-3 text-right">Balance Due</th>
                                                <th className="p-3 text-right w-[150px]">Allocate</th>
                                                <th className="p-3 text-right w-[150px]">Discount</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {outstandingInvoices.map((inv) => (
                                                <tr key={inv.id} className="border-t">
                                                    <td className="p-3 font-medium">{inv.invoiceNumber}</td>
                                                    <td className="p-3">
                                                        {format(new Date(inv.dueDate), 'MMM dd, yyyy')}
                                                        {new Date(inv.dueDate) < new Date() && (
                                                            <span className="ml-2 text-xs text-red-500 font-bold">Overdue</span>
                                                        )}
                                                    </td>
                                                    <td className="p-3 text-right">{formatCurrency(inv.balanceAmount)}</td>
                                                    <td className="p-3">
                                                        <Input
                                                            type="number"
                                                            className="text-right h-8"
                                                            min={0}
                                                            max={inv.balanceAmount} // Ideally constrained
                                                            value={allocations[inv.id] || ''}
                                                            onChange={(e) => {
                                                                const val = Number(e.target.value);
                                                                setAllocations(prev => ({
                                                                    ...prev,
                                                                    [inv.id]: val
                                                                }));
                                                            }}
                                                            disabled={!!createdPaymentId}
                                                        />
                                                    </td>
                                                    <td className="p-3">
                                                        <Input
                                                            type="number"
                                                            className="text-right h-8"
                                                            min={0}
                                                            max={inv.balanceAmount}
                                                            value={discountAllocations[inv.id] || ''}
                                                            onChange={(e) => {
                                                                const val = Number(e.target.value);
                                                                setDiscountAllocations(prev => ({
                                                                    ...prev,
                                                                    [inv.id]: val
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
