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
import { arService } from '@/services/ar-service';
import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import { financeService } from '@/services/finance.service';
import { PaymentMethodType } from '@/types/cash-management';
import { useToast } from '@/components/ui/use-toast';
import { formatCurrency, cn } from '@/lib/utils';
import { useQuery } from '@tanstack/react-query';
import { Skeleton } from '@/components/ui/skeleton';
import { format } from 'date-fns';

const paymentSchema = z.object({
    customerId: z.string().min(1, 'Customer is required'),
    bankAccountId: z.string().min(1, 'Bank account is required'),
    paymentDate: z.date(),
    totalAmount: z.coerce.number().min(0.01, 'Amount must be positive'),
    paymentMethod: z.string().min(1, 'Payment method is required'),
    paymentMethodId: z.string().optional(),
    referenceNumber: z.string().optional(),
    currencyCode: z.string().default('GHS'),
    exchangeRate: z.coerce.number().min(0.0001, 'Exchange rate must be greater than 0').default(1),
    notes: z.string().optional(),
});

type PaymentFormValues = z.infer<typeof paymentSchema>;

const toCustomerPaymentMethod = (type?: PaymentMethodType): string => {
    switch (type) {
        case PaymentMethodType.Cash:
            return 'Cash';
        case PaymentMethodType.Cheque:
            return 'Cheque';
        case PaymentMethodType.EFT:
            return 'EFT';
        case PaymentMethodType.Card:
            return 'Card';
        case PaymentMethodType.MobileMoney:
            return 'Mobile Money';
        case PaymentMethodType.DirectDebit:
            return 'Direct Debit';
        case PaymentMethodType.StandingOrder:
            return 'Standing Order';
        case PaymentMethodType.BankTransfer:
            return 'Bank Transfer';
        default:
            return 'Other';
    }
};

export default function NewReceiptPage() {
    const router = useRouter();
    const searchParams = useSearchParams();
    const preselectedCustomerId = searchParams.get('customerId');
    const preselectedInvoiceId = searchParams.get('invoiceId');
    const preselectedBankAccountId = searchParams.get('bankAccountId') || '';
    const preselectedPaymentMethodId = searchParams.get('paymentMethodId') || '';
    const preselectedAmountParam = searchParams.get('amount');
    const preselectedAmount = preselectedAmountParam && Number.isFinite(Number(preselectedAmountParam))
        ? Number(preselectedAmountParam)
        : 0;
    const preselectedPaymentDateParam = searchParams.get('paymentDate');
    const preselectedPaymentDate = preselectedPaymentDateParam && !Number.isNaN(Date.parse(preselectedPaymentDateParam))
        ? new Date(preselectedPaymentDateParam)
        : new Date();
    const preselectedReferenceNumber = searchParams.get('referenceNumber') || '';
    const preselectedDescription = searchParams.get('description') || '';
    const { toast } = useToast();
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [allocations, setAllocations] = useState<Record<string, number>>({});
    const [discountAllocations, setDiscountAllocations] = useState<Record<string, number>>({});

    // Fetch customers
    const { data: customersData } = useQuery({
        queryKey: ['customers-list'],
        queryFn: () => arService.getCustomers({ pageSize: 100 }),
    });

    const { data: bankAccounts } = useQuery({
        queryKey: ['bank-accounts', 'active'],
        queryFn: () => cashManagementDataService.getActiveBankAccounts(),
    });

    const { data: paymentMethods } = useQuery({
        queryKey: ['payment-methods', 'active'],
        queryFn: () => cashManagementDataService.getActivePaymentMethods(),
    });

    const form = useForm<PaymentFormValues>({
        resolver: zodResolver(paymentSchema) as any,
        defaultValues: {
            customerId: preselectedCustomerId || '',
            bankAccountId: preselectedBankAccountId,
            paymentDate: preselectedPaymentDate,
            totalAmount: preselectedAmount,
            paymentMethod: 'Bank Transfer',
            paymentMethodId: preselectedPaymentMethodId || undefined,
            referenceNumber: preselectedReferenceNumber,
            currencyCode: 'GHS',
            exchangeRate: 1,
            notes: preselectedDescription,
        },
    });

    const selectedCustomerId = form.watch('customerId');
    const selectedBankAccountId = form.watch('bankAccountId');
    const selectedPaymentMethodId = form.watch('paymentMethodId');

    useEffect(() => {
        if (!paymentMethods?.length) return;

        if (selectedPaymentMethodId) {
            const selectedMethod = paymentMethods.find((method) => method.id === selectedPaymentMethodId);
            if (selectedMethod) {
                form.setValue('paymentMethod', toCustomerPaymentMethod(selectedMethod.type));
            }
            return;
        }

        const preferredMethod =
            paymentMethods.find((method) => method.type === PaymentMethodType.BankTransfer) ||
            paymentMethods.find((method) => method.type === PaymentMethodType.EFT) ||
            paymentMethods[0];

        form.setValue('paymentMethodId', preferredMethod.id);
        form.setValue('paymentMethod', toCustomerPaymentMethod(preferredMethod.type));
    }, [paymentMethods, selectedPaymentMethodId, form]);

    useEffect(() => {
        if (!selectedBankAccountId || !bankAccounts) return;

        const account = bankAccounts.find((item) => item.id === selectedBankAccountId);
        if (!account) return;

        form.setValue('currencyCode', account.currency);
        if (account.currency === 'GHS') {
            form.setValue('exchangeRate', 1);
            return;
        }

        void financeService.getCurrentExchangeRate(account.currency)
            .then((rate) => form.setValue('exchangeRate', Number(rate.currentExchangeRate ?? rate.rate ?? 1)))
            .catch(() => form.setValue('exchangeRate', 1));
    }, [selectedBankAccountId, bankAccounts, form]);

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
                const discountAmount = Number(invoice.discountAmount) || 0;
                const netPaymentAmount = Math.max(invoice.balanceAmount - discountAmount, 0);
                form.setValue('totalAmount', netPaymentAmount);
                setAllocations({ [invoice.id]: netPaymentAmount });
                setDiscountAllocations({ [invoice.id]: discountAmount });
            }
        }
    }, [preselectedInvoiceId, outstandingInvoices, form]);

    const onSubmit = async (data: PaymentFormValues) => {
        setIsSubmitting(true);
        try {
            const selectedPaymentMethod = paymentMethods?.find((method) => method.id === data.paymentMethodId);
            if (data.paymentMethodId && !selectedPaymentMethod) {
                form.setError('paymentMethodId', { type: 'manual', message: 'Selected payment method is not available' });
                return;
            }

            if (selectedPaymentMethod?.requiresBankAccount && !data.bankAccountId) {
                form.setError('bankAccountId', { type: 'manual', message: `${selectedPaymentMethod.name} requires a bank account` });
                return;
            }

            if (selectedPaymentMethod?.requiresReference && !data.referenceNumber?.trim()) {
                form.setError('referenceNumber', { type: 'manual', message: `${selectedPaymentMethod.name} requires a reference number` });
                return;
            }

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

            const totalAllocated = allocationRows.reduce((sum, row) => sum + row.allocatedAmount, 0);
            if (totalAllocated > data.totalAmount) {
                toast({
                    title: 'Allocation exceeds receipt',
                    description: 'Allocated invoice amounts cannot exceed the receipt amount.',
                    variant: 'destructive',
                });
                return;
            }

            await arService.createPayment({
                ...data,
                paymentDate: data.paymentDate.toISOString(),
                transactionReference: data.referenceNumber,
                allocations: allocationRows.length > 0 ? allocationRows : undefined,
            });

            toast({ title: 'Success', description: 'Customer receipt recorded successfully' });
            router.push('/finance/ar/receipts');
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
        const newDiscountAllocations: Record<string, number> = {};

        // Allocate to oldest invoices first
        const sortedInvoices = [...outstandingInvoices].sort((a, b) => new Date(a.dueDate || a.invoiceDate).getTime() - new Date(b.dueDate || b.invoiceDate).getTime());

        for (const inv of sortedInvoices) {
            if (remaining <= 0) break;
            const discountAmount = Number(inv.discountAmount) || 0;
            const netBalance = Math.max(inv.balanceAmount - discountAmount, 0);
            const allocateAmount = Math.min(remaining, netBalance);
            newAllocations[inv.id] = allocateAmount;
            if (discountAmount > 0 && allocateAmount >= netBalance) {
                newDiscountAllocations[inv.id] = discountAmount;
            }
            remaining -= allocateAmount;
        }
        setAllocations(newAllocations);
        setDiscountAllocations(newDiscountAllocations);
    };

    return (
        <div className="space-y-8 p-8 max-w-[1200px] mx-auto">
            <div className="flex items-center space-x-4">
                <Button variant="ghost" size="icon" onClick={() => router.back()}>
                    <ArrowLeft className="h-4 w-4" />
                </Button>
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Record Customer Receipt</h1>
                    <p className="text-muted-foreground">
                        Record a receipt from a customer and allocate it to outstanding invoices.
                    </p>
                </div>
            </div>

            <div className="grid gap-8 md:grid-cols-3">
                {/* Customer receipt details; the API persists receipts as AR payments. */}
                <Card className="md:col-span-1 h-fit">
                    <CardHeader>
                        <CardTitle>Receipt Details</CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                        <form id="payment-form" onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
                            <div className="space-y-2">
                                <Label htmlFor="customer">Customer</Label>
                                <Select
                                    onValueChange={(val) => form.setValue('customerId', val)}
                                    value={form.watch('customerId') || undefined}
                                    disabled={isSubmitting}
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
                                <Label htmlFor="bankAccount">Deposit To Bank Account</Label>
                                <Select
                                    onValueChange={(val) => form.setValue('bankAccountId', val)}
                                    value={form.watch('bankAccountId') || undefined}
                                    disabled={isSubmitting}
                                >
                                    <SelectTrigger>
                                        <SelectValue placeholder="Select bank account..." />
                                    </SelectTrigger>
                                    <SelectContent>
                                        {bankAccounts?.map((account) => (
                                            <SelectItem key={account.id} value={account.id}>
                                                {account.accountName} ({account.currency}) - {account.bankName}
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                {form.formState.errors.bankAccountId && (
                                    <p className="text-sm text-red-500">{form.formState.errors.bankAccountId.message}</p>
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
                                                    disabled={isSubmitting}
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
                                        disabled={isSubmitting}
                                    />
                                </div>
                                {form.formState.errors.totalAmount && (
                                    <p className="text-sm text-red-500">{form.formState.errors.totalAmount.message}</p>
                                )}
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="exchangeRate">Exchange Rate</Label>
                                <Input
                                    id="exchangeRate"
                                    type="number"
                                    step="0.000001"
                                    {...form.register('exchangeRate')}
                                    disabled={isSubmitting || currentCurrencyCode === 'GHS'}
                                />
                                <p className="text-xs text-muted-foreground">
                                    1 {currentCurrencyCode} = {form.watch('exchangeRate') || 1} GHS
                                </p>
                                {form.formState.errors.exchangeRate && (
                                    <p className="text-sm text-red-500">{form.formState.errors.exchangeRate.message}</p>
                                )}
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="paymentMethod">Payment Method</Label>
                                <Select
                                    onValueChange={(val) => {
                                        const method = paymentMethods?.find((item) => item.id === val);
                                        form.setValue('paymentMethodId', val);
                                        form.setValue('paymentMethod', toCustomerPaymentMethod(method?.type));
                                    }}
                                    value={form.watch('paymentMethodId') || undefined}
                                    disabled={isSubmitting}
                                >
                                    <SelectTrigger>
                                        <SelectValue placeholder="Select payment method..." />
                                    </SelectTrigger>
                                    <SelectContent>
                                        {paymentMethods?.map((method) => (
                                            <SelectItem key={method.id} value={method.id}>
                                                {method.name}
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                {form.formState.errors.paymentMethodId && (
                                    <p className="text-sm text-red-500">{form.formState.errors.paymentMethodId.message}</p>
                                )}
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="reference">Reference #</Label>
                                <Input id="reference" {...form.register('referenceNumber')} disabled={isSubmitting} />
                                {form.formState.errors.referenceNumber && (
                                    <p className="text-sm text-red-500">{form.formState.errors.referenceNumber.message}</p>
                                )}
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="notes">Notes</Label>
                                <Textarea id="notes" {...form.register('notes')} disabled={isSubmitting} />
                            </div>
                        </form>
                    </CardContent>
                    <CardFooter>
                        <Button type="submit" form="payment-form" className="w-full" disabled={isSubmitting}>
                            {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Record Receipt
                        </Button>
                    </CardFooter>
                </Card>

                {/* Allocation Section */}
                <Card className="md:col-span-2">
                    <CardHeader className="flex flex-row items-center justify-between">
                        <CardTitle>Allocate to Invoices</CardTitle>
                        <Button variant="outline" size="sm" onClick={handleAutoAllocate} disabled={isSubmitting || !outstandingInvoices || outstandingInvoices.length === 0}>
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
                                                Discounts allowed: {formatCurrency(totalDiscounts, currentCurrencyCode)}
                                            </div>
                                        )}
                                    </div>
                                    <span className={remainingAmount < 0 ? 'text-red-500' : 'text-green-600'}>
                                        {formatCurrency(remainingAmount, currentCurrencyCode)}
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
                                            {outstandingInvoices.map((inv) => {
                                                const availableDiscount = Number(inv.discountAmount) || 0;
                                                const maxCashAllocation = Math.max(inv.balanceAmount - availableDiscount, 0);

                                                return (
                                                    <tr key={inv.id} className="border-t">
                                                        <td className="p-3 font-medium">
                                                            <div>{inv.invoiceNumber}</div>
                                                            {availableDiscount > 0 && (
                                                                <div className="text-xs text-emerald-700">
                                                                    Discount available: {formatCurrency(availableDiscount, currentCurrencyCode)}
                                                                </div>
                                                            )}
                                                        </td>
                                                        <td className="p-3">
                                                            {inv.dueDate ? format(new Date(inv.dueDate), 'MMM dd, yyyy') : '-'}
                                                            {inv.dueDate && new Date(inv.dueDate) < new Date() && (
                                                                <span className="ml-2 text-xs text-red-500 font-bold">Overdue</span>
                                                            )}
                                                        </td>
                                                        <td className="p-3 text-right">{formatCurrency(inv.balanceAmount, currentCurrencyCode)}</td>
                                                        <td className="p-3">
                                                            <Input
                                                                type="number"
                                                                className="text-right h-8"
                                                                min={0}
                                                                max={maxCashAllocation}
                                                                value={allocations[inv.id] || ''}
                                                                onChange={(e) => {
                                                                    const val = Number(e.target.value);
                                                                    setAllocations(prev => ({
                                                                        ...prev,
                                                                        [inv.id]: val
                                                                    }));
                                                                }}
                                                                disabled={isSubmitting}
                                                            />
                                                        </td>
                                                        <td className="p-3">
                                                            <Input
                                                                type="number"
                                                                className="text-right h-8"
                                                                min={0}
                                                                max={availableDiscount}
                                                                value={discountAllocations[inv.id] || ''}
                                                                onChange={(e) => {
                                                                    const val = Number(e.target.value);
                                                                    setDiscountAllocations(prev => ({
                                                                        ...prev,
                                                                        [inv.id]: val
                                                                    }));
                                                                }}
                                                                disabled={isSubmitting || availableDiscount <= 0}
                                                            />
                                                        </td>
                                                    </tr>
                                                );
                                            })}
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
