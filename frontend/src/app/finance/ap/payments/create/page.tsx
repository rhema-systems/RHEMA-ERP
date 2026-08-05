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
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';
import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import { financeService } from '@/services/finance.service';
import { taxDataService } from '@/services/finance/tax-data.service';
import { PaymentMethodType } from '@/types/cash-management';
import { TaxApplicability, TaxCategory, type Tax } from '@/types/tax';
import { useToast } from '@/components/ui/use-toast';
import { formatCurrency, cn } from '@/lib/utils';
import { useQuery } from '@tanstack/react-query';
import { Skeleton } from '@/components/ui/skeleton';
import { format } from 'date-fns';
import {
    VendorPaymentReadinessBadge,
    VendorPaymentReadinessControl,
} from '@/components/finance/VendorPaymentReadinessControl';

const paymentSchema = z.object({
    supplierId: z.string().min(1, 'Supplier is required'),
    bankAccountId: z.string().min(1, 'Bank account is required'),
    paymentDate: z.date(),
    totalAmount: z.coerce.number().min(0.01, 'Amount must be positive'),
    paymentMethod: z.enum(['BankTransfer', 'Cheque', 'Cash', 'WireTransfer', 'MobileMoney', 'DirectDebit', 'Other']).default('BankTransfer'),
    paymentMethodId: z.string().optional(),
    transactionReference: z.string().optional(),
    currencyCode: z.string().default('GHS'),
    exchangeRate: z.coerce.number().min(0.0001, 'Exchange rate must be greater than 0').default(1),
    withholdingTaxId: z.string().optional(),
    withholdingTaxAccountId: z.string().optional(),
    withholdingTaxRate: z.coerce.number().min(0).max(100).optional().default(0),
    withholdingCertificateNumber: z.string().optional(),
    notes: z.string().optional(),
});

type PaymentFormValues = z.infer<typeof paymentSchema>;

const toVendorPaymentMethod = (type?: PaymentMethodType): PaymentFormValues['paymentMethod'] => {
    switch (type) {
        case PaymentMethodType.Cash:
            return 'Cash';
        case PaymentMethodType.Cheque:
            return 'Cheque';
        case PaymentMethodType.MobileMoney:
            return 'MobileMoney';
        case PaymentMethodType.DirectDebit:
            return 'DirectDebit';
        case PaymentMethodType.EFT:
        case PaymentMethodType.BankTransfer:
        case PaymentMethodType.StandingOrder:
            return 'BankTransfer';
        default:
            return 'Other';
    }
};

const roundMoney = (amount: number) => Math.round((amount + Number.EPSILON) * 100) / 100;

export default function NewVendorPaymentPage() {
    const router = useRouter();
    const searchParams = useSearchParams();
    const preselectedSupplierId = searchParams.get('supplierId');
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
    const [withholdingAllocations, setWithholdingAllocations] = useState<Record<string, number>>({});

    const { data: suppliersData } = useQuery({
        queryKey: ['business-partners', 'ap-suppliers'],
        queryFn: () => businessPartnerService.getPartners({ pageSize: 100 }),
    });

    const { data: bankAccounts } = useQuery({
        queryKey: ['bank-accounts', 'active'],
        queryFn: () => cashManagementDataService.getActiveBankAccounts(),
    });

    const { data: paymentMethods } = useQuery({
        queryKey: ['payment-methods', 'active'],
        queryFn: () => cashManagementDataService.getActivePaymentMethods(),
    });

    const { data: withholdingTaxes } = useQuery({
        queryKey: ['taxes', 'withholding', 'active'],
        queryFn: () => taxDataService.getTaxes({ isActive: true, category: TaxCategory.Withholding }),
    });

    const form = useForm<PaymentFormValues>({
        // @ts-expect-error TODO: fix type
        resolver: zodResolver(paymentSchema),
        defaultValues: {
            supplierId: preselectedSupplierId || '',
            bankAccountId: preselectedBankAccountId,
            paymentDate: preselectedPaymentDate,
            totalAmount: preselectedAmount,
            paymentMethod: 'BankTransfer',
            paymentMethodId: preselectedPaymentMethodId || undefined,
            transactionReference: preselectedReferenceNumber,
            currencyCode: 'GHS',
            exchangeRate: 1,
            withholdingTaxId: undefined,
            withholdingTaxAccountId: undefined,
            withholdingTaxRate: 0,
            withholdingCertificateNumber: undefined,
            notes: preselectedDescription,
        },
    });

    const selectedSupplierId = form.watch('supplierId');
    const selectedBankAccountId = form.watch('bankAccountId');
    const selectedPaymentMethodId = form.watch('paymentMethodId');
    const selectedWithholdingTaxId = form.watch('withholdingTaxId');
    const currentCurrencyCode = form.watch('currencyCode') || 'GHS';
    const selectedWithholdingTax = withholdingTaxes?.find((tax: Tax) => tax.id === selectedWithholdingTaxId);
    const withholdingTaxOptions = (withholdingTaxes ?? []).filter((tax: Tax) =>
        tax.applicability === TaxApplicability.Purchases ||
        tax.applicability === TaxApplicability.Both
    );
    const supplierOptions = (suppliersData?.items ?? []).filter((partner: BusinessPartnerDto) =>
        ['supplier', 'contractor', 'both'].includes((partner.partnerType ?? '').toLowerCase()) &&
        !partner.isBlacklisted
    );

    useEffect(() => {
        if (!paymentMethods?.length) return;

        if (selectedPaymentMethodId) {
            const selectedMethod = paymentMethods.find((method) => method.id === selectedPaymentMethodId);
            if (selectedMethod) {
                form.setValue('paymentMethod', toVendorPaymentMethod(selectedMethod.type));
            }
            return;
        }

        const preferredMethod =
            paymentMethods.find((method) => method.type === PaymentMethodType.BankTransfer) ||
            paymentMethods.find((method) => method.type === PaymentMethodType.EFT) ||
            paymentMethods[0];

        form.setValue('paymentMethodId', preferredMethod.id);
        form.setValue('paymentMethod', toVendorPaymentMethod(preferredMethod.type));
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

    useEffect(() => {
        if (!selectedWithholdingTax) {
            form.setValue('withholdingTaxRate', 0);
            form.setValue('withholdingTaxAccountId', undefined);
            return;
        }

        form.setValue('withholdingTaxRate', Number(selectedWithholdingTax.rate) || 0);
        form.setValue('withholdingTaxAccountId', selectedWithholdingTax.taxPayableAccountId ?? undefined);
    }, [selectedWithholdingTax, form]);

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
            if (invoice?.paymentReadiness?.isPaymentReady) {
                const discountAmount = Number(invoice.discountAmount) || 0;
                const netPaymentAmount = Math.max(invoice.balanceAmount - discountAmount, 0);
                form.setValue('totalAmount', netPaymentAmount);
                setAllocations({ [invoice.invoiceId]: netPaymentAmount });
                setDiscountAllocations({ [invoice.invoiceId]: discountAmount });
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

            if (selectedPaymentMethod?.requiresReference && !data.transactionReference?.trim()) {
                form.setError('transactionReference', { type: 'manual', message: `${selectedPaymentMethod.name} requires a reference number` });
                return;
            }

            const paymentAllocations = Object.entries(allocations)
                .filter(([invoiceId, amount]) =>
                    (amount > 0 ||
                    Number(discountAllocations[invoiceId]) > 0 ||
                    Number(withholdingAllocations[invoiceId]) > 0) &&
                    outstandingInvoices?.find((invoice) => invoice.invoiceId === invoiceId)
                        ?.paymentReadiness?.isPaymentReady === true
                )
                .map(([invoiceId, amount]) => ({
                    vendorInvoiceId: invoiceId,
                    allocatedAmount: amount,
                    discountAmount: Number(discountAllocations[invoiceId]) || 0,
                    withholdingTaxAmount: Number(withholdingAllocations[invoiceId]) || 0,
                }));

            const blockedSelection = Object.entries(allocations).find(([invoiceId, amount]) =>
                (amount > 0 || Number(discountAllocations[invoiceId]) > 0 || Number(withholdingAllocations[invoiceId]) > 0) &&
                outstandingInvoices?.find((invoice) => invoice.invoiceId === invoiceId)
                    ?.paymentReadiness?.isPaymentReady !== true
            );
            if (blockedSelection) {
                toast({
                    title: 'Payment readiness blocked',
                    description: 'Remove blocked invoices before recording the payment. The server will revalidate all selected invoices.',
                    variant: 'destructive',
                });
                return;
            }

            const totalAllocated = paymentAllocations.reduce((sum, allocation) => sum + allocation.allocatedAmount, 0);
            const totalWithholdingTax = paymentAllocations.reduce((sum, allocation) => sum + (allocation.withholdingTaxAmount || 0), 0);
            if (totalAllocated > data.totalAmount) {
                toast({
                    title: 'Allocation exceeds payment',
                    description: 'Allocated bill amounts cannot exceed the payment amount.',
                    variant: 'destructive',
                });
                return;
            }

            if (totalWithholdingTax > 0 && !data.withholdingTaxId) {
                toast({
                    title: 'WHT tax required',
                    description: 'Select the configured withholding tax before recording WHT on a payment.',
                    variant: 'destructive',
                });
                return;
            }

            if (totalWithholdingTax > 0 && !data.withholdingTaxAccountId) {
                toast({
                    title: 'WHT account missing',
                    description: 'The selected withholding tax needs a payable account before this payment can be posted.',
                    variant: 'destructive',
                });
                return;
            }

            const overSettledInvoice = outstandingInvoices?.find((invoice) => {
                const invoiceSettlement =
                    (Number(allocations[invoice.invoiceId]) || 0) +
                    (Number(discountAllocations[invoice.invoiceId]) || 0) +
                    (Number(withholdingAllocations[invoice.invoiceId]) || 0);

                return invoiceSettlement - invoice.balanceAmount > 0.01;
            });
            if (overSettledInvoice) {
                toast({
                    title: 'Bill over-settled',
                    description: `${overSettledInvoice.invoiceNumber} exceeds its outstanding balance after cash, discount, and WHT.`,
                    variant: 'destructive',
                });
                return;
            }

            const payment = await accountsPayableService.createPayment({
                ...data,
                paymentDate: data.paymentDate.toISOString(),
                withholdingTaxAmount: totalWithholdingTax,
                allocations: paymentAllocations.length > 0 ? paymentAllocations : undefined,
            });

            toast({ title: 'Success', description: 'Vendor payment recorded successfully' });
            router.push(`/finance/ap/payments/${payment.id}`);
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
    const totalDiscounts = Object.values(discountAllocations).reduce((acc, curr) => acc + curr, 0);
    const totalWithholdingTax = Object.values(withholdingAllocations).reduce((acc, curr) => acc + curr, 0);
    const totalBillSettlement = totalAllocated + totalDiscounts + totalWithholdingTax;
    const remainingAmount = currentAmount - totalAllocated;

    const handleAutoAllocate = () => {
        if (!outstandingInvoices) return;
        let remaining = currentAmount;
        const newAllocations: Record<string, number> = {};
        const newDiscountAllocations: Record<string, number> = {};
        const newWithholdingAllocations: Record<string, number> = {};
        const withholdingRate = selectedWithholdingTax ? Number(selectedWithholdingTax.rate || 0) : 0;

        // Allocate to oldest invoices first
        const sortedInvoices = outstandingInvoices
            .filter((invoice) => invoice.paymentReadiness?.isPaymentReady === true)
            .sort((a, b) => new Date(a.dueDate || a.invoiceDate).getTime() - new Date(b.dueDate || b.invoiceDate).getTime());

        for (const inv of sortedInvoices) {
            if (remaining <= 0) break;
            const discountAmount = Number(inv.discountAmount) || 0;
            const withholdingAmount = withholdingRate > 0 ? roundMoney(inv.balanceAmount * (withholdingRate / 100)) : 0;
            const netBalance = Math.max(inv.balanceAmount - discountAmount - withholdingAmount, 0);
            const allocateAmount = Math.min(remaining, netBalance);
            newAllocations[inv.invoiceId] = allocateAmount;
            if (discountAmount > 0 && allocateAmount >= netBalance) {
                newDiscountAllocations[inv.invoiceId] = discountAmount;
            }
            if (withholdingAmount > 0 && allocateAmount >= netBalance) {
                newWithholdingAllocations[inv.invoiceId] = withholdingAmount;
            }
            remaining -= allocateAmount;
        }
        setAllocations(newAllocations);
        setDiscountAllocations(newDiscountAllocations);
        setWithholdingAllocations(newWithholdingAllocations);
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
                                    value={form.watch('supplierId') || undefined}
                                    disabled={isSubmitting}
                                >
                                    <SelectTrigger>
                                        <SelectValue placeholder="Select supplier..." />
                                    </SelectTrigger>
                                    <SelectContent>
                                        {supplierOptions.map((supplier) => (
                                            <SelectItem key={supplier.id} value={supplier.id}>
                                                {supplier.partnerName}
                                                {supplier.partnerCode ? ` (${supplier.partnerCode})` : ''}
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                {form.formState.errors.supplierId && (
                                    <p className="text-sm text-red-500">{form.formState.errors.supplierId.message}</p>
                                )}
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="bankAccount">Pay From Bank Account</Label>
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
                                <Label htmlFor="amount">Amount Paid</Label>
                                <div className="relative">
                                    <span className="absolute left-3 top-2.5 text-gray-500 text-sm font-medium">
                                        {currentCurrencyCode}
                                    </span>
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

                            <div className="space-y-2 rounded-md border p-3">
                                <Label htmlFor="withholdingTax">Withholding Tax</Label>
                                <Select
                                    onValueChange={(val) => {
                                        if (val === 'none') {
                                            form.setValue('withholdingTaxId', undefined);
                                            form.setValue('withholdingTaxAccountId', undefined);
                                            form.setValue('withholdingTaxRate', 0);
                                            setWithholdingAllocations({});
                                            return;
                                        }

                                        form.setValue('withholdingTaxId', val);
                                    }}
                                    value={form.watch('withholdingTaxId') || 'none'}
                                    disabled={isSubmitting}
                                >
                                    <SelectTrigger>
                                        <SelectValue placeholder="No WHT" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="none">No WHT</SelectItem>
                                        {withholdingTaxOptions.map((tax) => (
                                            <SelectItem key={tax.id} value={tax.id}>
                                                {tax.code} - {tax.name} ({Number(tax.rate || 0)}%)
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                {selectedWithholdingTax && (
                                    <div className="text-xs text-muted-foreground">
                                        {selectedWithholdingTax.taxPayableAccountId
                                            ? `Posting to configured WHT payable account at ${Number(selectedWithholdingTax.rate || 0)}%.`
                                            : 'This tax has no payable account configured; posting will be blocked until it is set.'}
                                    </div>
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
                                        form.setValue('paymentMethod', toVendorPaymentMethod(method?.type));
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
                                <Input id="reference" placeholder="e.g. Cheque No. / Receipt" {...form.register('transactionReference')} disabled={isSubmitting} />
                                {form.formState.errors.transactionReference && (
                                    <p className="text-sm text-red-500">{form.formState.errors.transactionReference.message}</p>
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
                            Record Payment
                        </Button>
                    </CardFooter>
                </Card>

                {/* Allocation Section */}
                <Card className="md:col-span-2">
                    <CardHeader className="flex flex-row items-center justify-between">
                        <CardTitle>Allocate to Bills</CardTitle>
                        <Button variant="outline" size="sm" onClick={handleAutoAllocate} disabled={isSubmitting || !outstandingInvoices?.some((invoice) => invoice.paymentReadiness?.isPaymentReady === true)}>
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
                                <VendorPaymentReadinessControl
                                    readiness={outstandingInvoices
                                        .map((invoice) => invoice.paymentReadiness)
                                        .filter((item): item is NonNullable<typeof item> => Boolean(item))}
                                />
                                <div className="flex justify-between items-center bg-muted/50 p-4 rounded-lg font-medium">
                                    <div>
                                        <div>Remaining Cash to Allocate:</div>
                                        {totalDiscounts > 0 && (
                                            <div className="text-xs text-muted-foreground">
                                                Discounts taken: {formatCurrency(totalDiscounts, currentCurrencyCode)}
                                            </div>
                                        )}
                                        {totalWithholdingTax > 0 && (
                                            <div className="text-xs text-muted-foreground">
                                                WHT withheld: {formatCurrency(totalWithholdingTax, currentCurrencyCode)}
                                            </div>
                                        )}
                                        {(totalDiscounts > 0 || totalWithholdingTax > 0) && (
                                            <div className="text-xs text-muted-foreground">
                                                Total bill settlement: {formatCurrency(totalBillSettlement, currentCurrencyCode)}
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
                                                <th className="p-3 text-left">Bill #</th>
                                                <th className="p-3 text-left">Due Date</th>
                                                <th className="p-3 text-right">Balance Due</th>
                                                <th className="p-3 text-right w-[150px]">Allocate</th>
                                                <th className="p-3 text-right w-[150px]">Discount</th>
                                                <th className="p-3 text-right w-[150px]">WHT</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {outstandingInvoices.map((inv) => {
                                                const availableDiscount = Number(inv.discountAmount) || 0;
                                                const maxCashAllocation = Math.max(inv.balanceAmount - availableDiscount, 0);
                                                const currentCashAllocation = Number(allocations[inv.invoiceId]) || 0;
                                                const currentDiscountAllocation = Number(discountAllocations[inv.invoiceId]) || 0;
                                                const maxWithholdingAllocation = Math.max(inv.balanceAmount - currentCashAllocation - currentDiscountAllocation, 0);
                                                const isPaymentReady = inv.paymentReadiness?.isPaymentReady === true;

                                                return (
                                                    <tr key={inv.invoiceId} className="border-t">
                                                        <td className="p-3 font-medium flex flex-col items-start gap-1">
                                                            <span>{inv.invoiceNumber}</span>
                                                            {inv.supplierInvoiceNumber && (
                                                                <span className="text-xs text-muted-foreground">Ref: {inv.supplierInvoiceNumber}</span>
                                                            )}
                                                            <VendorPaymentReadinessBadge readiness={inv.paymentReadiness} />
                                                            {availableDiscount > 0 && (
                                                                <span className="text-xs text-emerald-700">
                                                                    Discount available: {formatCurrency(availableDiscount, currentCurrencyCode)}
                                                                </span>
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
                                                                value={allocations[inv.invoiceId] || ''}
                                                                onChange={(e) => {
                                                                    const val = Number(e.target.value);
                                                                    setAllocations(prev => ({
                                                                        ...prev,
                                                                        [inv.invoiceId]: val
                                                                    }));
                                                                }}
                                                                disabled={isSubmitting || !isPaymentReady}
                                                            />
                                                        </td>
                                                        <td className="p-3">
                                                            <Input
                                                                type="number"
                                                                className="text-right h-8"
                                                                min={0}
                                                                max={availableDiscount}
                                                                value={discountAllocations[inv.invoiceId] || ''}
                                                                onChange={(e) => {
                                                                    const val = Number(e.target.value);
                                                                    setDiscountAllocations(prev => ({
                                                                        ...prev,
                                                                        [inv.invoiceId]: val
                                                                    }));
                                                                }}
                                                                disabled={isSubmitting || !isPaymentReady || availableDiscount <= 0}
                                                            />
                                                        </td>
                                                        <td className="p-3">
                                                            <Input
                                                                type="number"
                                                                className="text-right h-8"
                                                                min={0}
                                                                max={maxWithholdingAllocation}
                                                                value={withholdingAllocations[inv.invoiceId] || ''}
                                                                onChange={(e) => {
                                                                    const val = Number(e.target.value);
                                                                    setWithholdingAllocations(prev => ({
                                                                        ...prev,
                                                                        [inv.invoiceId]: val
                                                                    }));
                                                                }}
                                                                disabled={isSubmitting || !isPaymentReady || !selectedWithholdingTax}
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
