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
import { taxDataService } from '@/services/finance/tax-data.service';
import { PaymentMethodType } from '@/types/cash-management';
import { TaxApplicability, TaxCategory, type Tax } from '@/types/tax';
import { useToast } from '@/components/ui/use-toast';
import { formatCurrency, cn } from '@/lib/utils';
import { useQuery } from '@tanstack/react-query';
import { Skeleton } from '@/components/ui/skeleton';
import { format } from 'date-fns';
import { loadApprovedSettlementRate } from '@/lib/finance/settlement-exchange-rate';
import { SourceDocumentDimensionPanel } from '@/components/finance/dimensions/source-document-dimension-panel';
import { toFinancePostingDimensionValues } from '@/lib/finance/source-document-dimensions';

const paymentSchema = z.object({
    customerId: z.string().min(1, 'Customer is required'),
    bankAccountId: z.string().optional(),
    liquidityAccountId: z.string().optional(),
    paymentDate: z.date(),
    totalAmount: z.coerce.number().min(0.01, 'Amount must be positive'),
    paymentMethod: z.string().min(1, 'Payment method is required'),
    paymentMethodId: z.string().optional(),
    referenceNumber: z.string().optional(),
    checkNumber: z.string().optional(),
    chequeDrawerBank: z.string().optional(),
    currencyCode: z.string().default('GHS'),
    exchangeRate: z.coerce.number().min(0.0001, 'Exchange rate must be greater than 0').default(1),
    exchangeRateId: z.string().optional(),
    withholdingTaxId: z.string().optional(),
    withholdingTaxAccountId: z.string().optional(),
    withholdingTaxAmount: z.coerce.number().min(0).default(0),
    vatWithholdingTaxId: z.string().optional(),
    vatWithholdingAccountId: z.string().optional(),
    vatWithholdingAmount: z.coerce.number().min(0).default(0),
    withholdingCertificateNumber: z.string().optional(),
    withholdingCertificateDate: z.string().optional(),
    notes: z.string().optional(),
});

type PaymentFormValues = z.infer<typeof paymentSchema>;

const directBankMethodTypes = new Set<PaymentMethodType>([
    PaymentMethodType.BankTransfer,
    PaymentMethodType.EFT,
    PaymentMethodType.DirectDebit,
    PaymentMethodType.StandingOrder,
]);

const liquidityTypeForPaymentMethod = (type?: PaymentMethodType) => {
    switch (type) {
        case PaymentMethodType.Cheque:
            return 'ChequesAwaitingDeposit';
        case PaymentMethodType.Card:
            return 'CardSettlementClearing';
        case PaymentMethodType.MobileMoney:
            return 'MobileMoneyClearing';
        case PaymentMethodType.Cash:
        default:
            return 'UndepositedCash';
    }
};

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
    const existingAdvancePaymentId = searchParams.get('paymentId');
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
    // Keep invoice reduction and receipt consumption distinct. They are equal for the common
    // same-currency path but represent different legal amounts for FIN-LIM-0022 settlements.
    const [allocations, setAllocations] = useState<Record<string, number>>({});
    const [paymentCurrencyAllocations, setPaymentCurrencyAllocations] = useState<Record<string, number>>({});
    const [discountAllocations, setDiscountAllocations] = useState<Record<string, number>>({});
    // Statutory deductions belong to the invoice allocation, not the receipt header. This is
    // essential when one receipt settles invoices in different currencies because each row must
    // retain its own native amount and approved functional conversion.
    const [withholdingAllocations, setWithholdingAllocations] = useState<Record<string, number>>({});
    const [vatWithholdingAllocations, setVatWithholdingAllocations] = useState<Record<string, number>>({});
    const [defaultDimensionValues, setDefaultDimensionValues] = useState<Record<string, string>>({});
    const [applyDefaultToAll, setApplyDefaultToAll] = useState(false);

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

    const { data: financeSettings } = useQuery({
        queryKey: ['finance-settings'],
        queryFn: () => financeService.getSettings(),
    });
    // TDC currently operates in GHS, while the shared Finance module remains tenant-aware. Use
    // the configured functional currency for rate behavior and labels rather than baking GHS into
    // a cross-currency workflow that is specifically intended to support other deployments.
    const functionalCurrencyCode = (financeSettings?.baseCurrency || 'GHS').toUpperCase();

    const { data: liquidityAccounts } = useQuery({
        queryKey: ['liquidity-accounts', 'active'],
        queryFn: () => cashManagementDataService.getLiquidityAccounts(true),
    });

    const { data: withholdingTaxes } = useQuery({
        queryKey: ['taxes', 'ar-withholding', 'active'],
        queryFn: () => taxDataService.getTaxes({ isActive: true, category: TaxCategory.Withholding }),
    });

    const { data: vatWithholdingTaxes } = useQuery({
        queryKey: ['taxes', 'ar-vat-withholding', 'active'],
        queryFn: () => taxDataService.getTaxes({ isActive: true, category: TaxCategory.VatWithholding }),
    });

    const form = useForm<PaymentFormValues>({
        resolver: zodResolver(paymentSchema) as any,
        defaultValues: {
            customerId: preselectedCustomerId || '',
            bankAccountId: preselectedBankAccountId,
            liquidityAccountId: undefined,
            paymentDate: preselectedPaymentDate,
            totalAmount: preselectedAmount,
            paymentMethod: 'Bank Transfer',
            paymentMethodId: preselectedPaymentMethodId || undefined,
            referenceNumber: preselectedReferenceNumber,
            currencyCode: 'GHS',
            exchangeRate: 1,
            exchangeRateId: undefined,
            withholdingTaxId: undefined,
            withholdingTaxAccountId: undefined,
            withholdingTaxAmount: 0,
            vatWithholdingTaxId: undefined,
            vatWithholdingAccountId: undefined,
            vatWithholdingAmount: 0,
            withholdingCertificateNumber: undefined,
            withholdingCertificateDate: undefined,
            notes: preselectedDescription,
        },
    });

    const { data: existingAdvancePayment, isLoading: isLoadingAdvancePayment } = useQuery({
        queryKey: ['customer-payment', existingAdvancePaymentId],
        queryFn: () => {
            if (!existingAdvancePaymentId) throw new Error('Customer advance payment id is required.');
            return arService.getPayment(existingAdvancePaymentId);
        },
        enabled: !!existingAdvancePaymentId,
    });

    useEffect(() => {
        if (!existingAdvancePaymentId || !existingAdvancePayment) return;

        // The posted receipt is the immutable customer-advance lot. Freeze its origin evidence in
        // this workspace; only invoice applications are new. Changing header currency/rate here
        // would destroy the historical carrying value used to calculate realized FX.
        const remainingAdvance = Math.round((
            Number(existingAdvancePayment.totalAmount) - Number(existingAdvancePayment.allocatedAmount || 0)
        ) * 100) / 100;
        form.setValue('customerId', existingAdvancePayment.customerId);
        form.setValue('bankAccountId', existingAdvancePayment.bankAccountId || undefined);
        form.setValue('liquidityAccountId', existingAdvancePayment.liquidityAccountId || undefined);
        form.setValue('paymentDate', new Date(existingAdvancePayment.paymentDate));
        form.setValue('totalAmount', remainingAdvance);
        form.setValue('currencyCode', existingAdvancePayment.currencyCode || functionalCurrencyCode);
        form.setValue('exchangeRate', Number(existingAdvancePayment.exchangeRate) || 1);
        form.setValue('exchangeRateId', existingAdvancePayment.exchangeRateId);
        form.setValue('referenceNumber', existingAdvancePayment.paymentNumber);
    }, [existingAdvancePayment, existingAdvancePaymentId, form, functionalCurrencyCode]);

    const selectedCustomerId = form.watch('customerId');
    const selectedBankAccountId = form.watch('bankAccountId');
    const selectedLiquidityAccountId = form.watch('liquidityAccountId');
    const selectedPaymentMethodId = form.watch('paymentMethodId');
    const selectedPaymentMethod = paymentMethods?.find((method) => method.id === selectedPaymentMethodId);
    const selectedWithholdingTaxId = form.watch('withholdingTaxId');
    const selectedVatWithholdingTaxId = form.watch('vatWithholdingTaxId');
    const selectedWithholdingTax = withholdingTaxes?.find((tax: Tax) => tax.id === selectedWithholdingTaxId);
    const selectedVatWithholdingTax = vatWithholdingTaxes?.find((tax: Tax) => tax.id === selectedVatWithholdingTaxId);
    const salesWithholdingTaxes = (withholdingTaxes ?? []).filter((tax: Tax) =>
        tax.applicability === TaxApplicability.Sales || tax.applicability === TaxApplicability.Both);
    const salesVatWithholdingTaxes = (vatWithholdingTaxes ?? []).filter((tax: Tax) =>
        tax.applicability === TaxApplicability.Sales || tax.applicability === TaxApplicability.Both);
    const isDirectBankReceipt = selectedPaymentMethod
        ? directBankMethodTypes.has(selectedPaymentMethod.type)
        : true;
    const expectedLiquidityType = liquidityTypeForPaymentMethod(selectedPaymentMethod?.type);
    const eligibleLiquidityAccounts = liquidityAccounts?.filter(account =>
        account.accountType === expectedLiquidityType && account.isActive,
    ) ?? [];

    useEffect(() => {
        if (existingAdvancePaymentId) return;
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
    }, [paymentMethods, selectedPaymentMethodId, form, existingAdvancePaymentId]);

    useEffect(() => {
        if (existingAdvancePaymentId) return;
        if (!isDirectBankReceipt || !selectedBankAccountId || !bankAccounts) return;

        const account = bankAccounts.find((item) => item.id === selectedBankAccountId);
        if (!account) return;

        form.setValue('currencyCode', account.currency);
    }, [selectedBankAccountId, bankAccounts, form, isDirectBankReceipt, existingAdvancePaymentId]);

    useEffect(() => {
        if (existingAdvancePaymentId) return;
        if (isDirectBankReceipt || !selectedLiquidityAccountId || !liquidityAccounts) return;

        const account = liquidityAccounts.find((item) => item.id === selectedLiquidityAccountId);
        if (!account) return;

        form.setValue('currencyCode', account.currency);
    }, [selectedLiquidityAccountId, liquidityAccounts, form, isDirectBankReceipt, existingAdvancePaymentId]);

    const watchedPaymentDate = form.watch('paymentDate');
    const watchedCurrencyCode = form.watch('currencyCode') || functionalCurrencyCode;
    const [exchangeRateSource, setExchangeRateSource] = useState('Functional currency');

    useEffect(() => {
        if (existingAdvancePaymentId || !financeSettings || !watchedPaymentDate) return;
        form.setValue('exchangeRateId', undefined);
        setExchangeRateSource('Loading approved rate…');
        let cancelled = false;
        void loadApprovedSettlementRate(
            {
                module: 'AR',
                transactionCurrency: watchedCurrencyCode,
                functionalCurrency: functionalCurrencyCode,
                settlementDate: watchedPaymentDate,
                settings: financeSettings,
            },
            (code, query) => financeService.getCurrentExchangeRate(code, query),
        ).then(snapshot => {
            if (cancelled) return;
            form.setValue('exchangeRate', snapshot.rate);
            form.setValue('exchangeRateId', snapshot.exchangeRateId);
            setExchangeRateSource(`${snapshot.source} · ${snapshot.quoteSide}`);
        }).catch(error => {
            if (cancelled) return;
            form.setValue('exchangeRate', 0);
            form.setValue('exchangeRateId', undefined);
            setExchangeRateSource(error instanceof Error ? error.message : 'Approved rate unavailable');
        });
        return () => { cancelled = true; };
    }, [existingAdvancePaymentId, financeSettings, form, functionalCurrencyCode, watchedCurrencyCode, watchedPaymentDate]);

    useEffect(() => {
        if (existingAdvancePaymentId) return;
        if (isDirectBankReceipt) return;
        const selectionIsEligible = eligibleLiquidityAccounts.some(account => account.id === selectedLiquidityAccountId);
        if (selectionIsEligible) return;
        form.setValue('liquidityAccountId', eligibleLiquidityAccounts[0]?.id);
    }, [eligibleLiquidityAccounts, form, isDirectBankReceipt, selectedLiquidityAccountId, existingAdvancePaymentId]);

    useEffect(() => {
        form.setValue('withholdingTaxAccountId', selectedWithholdingTax?.taxReceivableAccountId ?? undefined);
        if (!selectedWithholdingTax) {
            form.setValue('withholdingTaxAmount', 0);
            setWithholdingAllocations({});
        }
    }, [form, selectedWithholdingTax]);

    useEffect(() => {
        form.setValue('vatWithholdingAccountId', selectedVatWithholdingTax?.taxReceivableAccountId ?? undefined);
        if (!selectedVatWithholdingTax) {
            form.setValue('vatWithholdingAmount', 0);
            setVatWithholdingAllocations({});
        }
    }, [form, selectedVatWithholdingTax]);

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
            const method = paymentMethods?.find((item) => item.id === data.paymentMethodId);
            if (!existingAdvancePaymentId && data.paymentMethodId && !method) {
                form.setError('paymentMethodId', { type: 'manual', message: 'Selected payment method is not available' });
                return;
            }

            const directBankReceipt = method ? directBankMethodTypes.has(method.type) : true;
            if (!existingAdvancePaymentId && directBankReceipt && !data.bankAccountId) {
                form.setError('bankAccountId', { type: 'manual', message: `${method?.name ?? 'This method'} requires a bank account` });
                return;
            }

            if (!existingAdvancePaymentId && !directBankReceipt && !data.liquidityAccountId) {
                form.setError('liquidityAccountId', { type: 'manual', message: `Select the ${expectedLiquidityType} holding account` });
                return;
            }

            if (!existingAdvancePaymentId && method?.requiresReference && !data.referenceNumber?.trim()) {
                form.setError('referenceNumber', { type: 'manual', message: `${method.name} requires a reference number` });
                return;
            }

            if (!existingAdvancePaymentId && method?.type === PaymentMethodType.Cheque && !data.checkNumber?.trim()) {
                form.setError('checkNumber', { type: 'manual', message: 'Cheque number is required' });
                return;
            }

            const invoiceIds = new Set([
                ...Object.keys(allocations),
                ...Object.keys(discountAllocations),
                ...Object.keys(withholdingAllocations),
                ...Object.keys(vatWithholdingAllocations),
            ]);

            const allocationRows = Array.from(invoiceIds)
                .map(invoiceId => {
                    const invoice = outstandingInvoices?.find(item => item.id === invoiceId);
                    const invoiceAmount = Number(allocations[invoiceId]) || 0;
                    const isCrossCurrency = invoice?.currencyCode !== data.currencyCode;
                    return {
                        invoiceId,
                        // allocatedAmount is always the invoice-currency cash reduction.
                        allocatedAmount: invoiceAmount,
                        // Cross-currency receipts must state the cash consumed from the receipt.
                        paymentCurrencyAmount: isCrossCurrency
                            ? Number(paymentCurrencyAllocations[invoiceId]) || 0
                            : invoiceAmount,
                        discountAmount: Number(discountAllocations[invoiceId]) || 0,
                        withholdingTaxAmount: Number(withholdingAllocations[invoiceId]) || 0,
                        vatWithholdingAmount: Number(vatWithholdingAllocations[invoiceId]) || 0,
                    };
                })
                .filter(row => row.allocatedAmount > 0 || row.discountAmount > 0 ||
                    row.withholdingTaxAmount > 0 || row.vatWithholdingAmount > 0);

            const incompleteCrossCurrencyAllocation = allocationRows.find(row =>
                row.allocatedAmount > 0 && (row.paymentCurrencyAmount ?? 0) <= 0);
            if (incompleteCrossCurrencyAllocation) {
                toast({
                    title: 'Receipt-currency amount required',
                    description: 'Enter both the invoice amount settled and the amount consumed from the selected receipt currency.',
                    variant: 'destructive',
                });
                return;
            }

            const hasWithholding = allocationRows.some(row => row.withholdingTaxAmount > 0);
            const hasVatWithholding = allocationRows.some(row => row.vatWithholdingAmount > 0);
            if (hasWithholding && !data.withholdingTaxId) {
                toast({ title: 'WHT tax required', description: 'Select the configured WHT receivable tax.', variant: 'destructive' });
                return;
            }
            if (hasVatWithholding && !data.vatWithholdingTaxId) {
                toast({ title: 'VAT withholding tax required', description: 'Select the configured VAT withholding receivable tax.', variant: 'destructive' });
                return;
            }
            if ((hasWithholding || hasVatWithholding) && !data.withholdingCertificateNumber?.trim()) {
                toast({ title: 'Certificate reference required', description: 'Record the customer withholding certificate/reference number.', variant: 'destructive' });
                return;
            }

            const totalAllocated = allocationRows.reduce((sum, row) => sum + (row.paymentCurrencyAmount ?? row.allocatedAmount), 0);
            if (totalAllocated > data.totalAmount) {
                toast({
                    title: 'Allocation exceeds receipt',
                    description: 'Receipt-currency cash allocations cannot exceed the cash received.',
                    variant: 'destructive',
                });
                return;
            }

            if (existingAdvancePaymentId && allocationRows.some(row =>
                row.discountAmount > 0 || row.withholdingTaxAmount > 0 || row.vatWithholdingAmount > 0)) {
                toast({
                    title: 'Advance application supports cash only',
                    description: 'Apply the advance amount here, then use the dedicated adjustment workflow for discounts or withholding.',
                    variant: 'destructive',
                });
                return;
            }

            if (existingAdvancePaymentId) {
                if (allocationRows.length === 0) {
                    toast({ title: 'Allocation required', description: 'Select at least one outstanding customer invoice.', variant: 'destructive' });
                    return;
                }
                await arService.allocatePayment({
                    customerPaymentId: existingAdvancePaymentId,
                    allocations: allocationRows,
                });
                toast({ title: 'Advance applied', description: 'The customer advance and any realized FX were posted successfully.' });
                router.push(`/finance/ar/payments/${existingAdvancePaymentId}`);
                return;
            }

            if (data.currencyCode.toUpperCase() !== functionalCurrencyCode && !data.exchangeRateId) {
                toast({
                    title: 'Approved exchange rate required',
                    description: exchangeRateSource,
                    variant: 'destructive',
                });
                return;
            }

            await arService.createPayment({
                ...data,
                // The API derives functional header totals from the per-invoice evidence below.
                // Sending zero prevents mixed native currencies from being added at the header.
                withholdingTaxAmount: 0,
                vatWithholdingAmount: 0,
                paymentDate: data.paymentDate.toISOString(),
                transactionReference: data.referenceNumber,
                allocations: allocationRows.length > 0 ? allocationRows : undefined,
                financeDimensions: {
                    defaultDimensions: toFinancePostingDimensionValues(defaultDimensionValues),
                    lines: [],
                    applyDefaultToEligibleLines: applyDefaultToAll,
                },
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
    const hasSettlementAllocations = [
        allocations,
        discountAllocations,
        withholdingAllocations,
        vatWithholdingAllocations,
    ].some(values => Object.values(values).some(amount => Number(amount) > 0));
    // Remaining receipt cash is a payment-currency figure, never a sum of mixed invoice values.
    const totalAllocated = outstandingInvoices?.reduce((sum, invoice) => {
        const invoiceAmount = Number(allocations[invoice.id]) || 0;
        return sum + (invoice.currencyCode === currentCurrencyCode
            ? invoiceAmount
            : Number(paymentCurrencyAllocations[invoice.id]) || 0);
    }, 0) ?? 0;
    const totalDiscounts = Object.values(discountAllocations).reduce((acc, curr) => acc + curr, 0);
    const hasLineWithholding = Object.values(withholdingAllocations).some(amount => amount > 0);
    const hasLineVatWithholding = Object.values(vatWithholdingAllocations).some(amount => amount > 0);
    const remainingAmount = currentAmount - totalAllocated;

    const handleAutoAllocate = () => {
        if (!outstandingInvoices) return;
        // Auto-allocation distributes cash only. Statutory deductions require explicit invoice
        // attribution and therefore remain a maker-entered decision.
        let remaining = currentAmount;
        const newAllocations: Record<string, number> = {};
        const newDiscountAllocations: Record<string, number> = {};

        // Allocate to oldest invoices first
        // Cross-currency rows require an explicit, user-confirmed pair of amounts. Auto allocation
        // therefore remains deterministic by considering only invoices in the receipt currency.
        const sortedInvoices = outstandingInvoices
            .filter(invoice => invoice.currencyCode === currentCurrencyCode)
            .sort((a, b) => new Date(a.dueDate || a.invoiceDate).getTime() - new Date(b.dueDate || b.invoiceDate).getTime());

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
        setPaymentCurrencyAllocations({});
        setDiscountAllocations(newDiscountAllocations);
    };

    return (
        <div className="space-y-8 p-8 max-w-[1200px] mx-auto">
            <div className="flex items-center space-x-4">
                <Button variant="ghost" size="icon" onClick={() => router.back()}>
                    <ArrowLeft className="h-4 w-4" />
                </Button>
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">
                        {existingAdvancePaymentId ? 'Apply Customer Advance' : 'Record Customer Receipt'}
                    </h1>
                    <p className="text-muted-foreground">
                        {existingAdvancePaymentId
                            ? 'Consume the posted receipt currency lot against outstanding customer invoices.'
                            : 'Record a receipt from a customer and allocate it to outstanding invoices.'}
                    </p>
                </div>
            </div>

            <div className="grid gap-8 md:grid-cols-3">
                {/* Customer receipt details; the API persists receipts as AR payments. */}
                <Card className="md:col-span-1 h-fit">
                    <CardHeader>
                        <CardTitle>{existingAdvancePaymentId ? 'Advance Lot' : 'Receipt Details'}</CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                        <form id="payment-form" onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
                            <div className="space-y-2">
                                <Label htmlFor="customer">Customer</Label>
                                <Select
                                    onValueChange={(val) => form.setValue('customerId', val)}
                                    value={form.watch('customerId') || undefined}
                                    disabled={isSubmitting || !!existingAdvancePaymentId}
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

                            {isDirectBankReceipt ? (
                                <div className="space-y-2">
                                    <Label htmlFor="bankAccount">Deposit To Bank Account</Label>
                                    <Select
                                        onValueChange={(val) => form.setValue('bankAccountId', val)}
                                        value={form.watch('bankAccountId') || undefined}
                                        disabled={isSubmitting || !!existingAdvancePaymentId}
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
                                    <p className="text-xs text-muted-foreground">Direct bank methods bypass the banking queue.</p>
                                </div>
                            ) : (
                                <div className="space-y-2">
                                    <Label htmlFor="liquidityAccount">Receive Into Holding Account</Label>
                                    <Select
                                        onValueChange={(val) => form.setValue('liquidityAccountId', val)}
                                        value={form.watch('liquidityAccountId') || undefined}
                                        disabled={isSubmitting || !!existingAdvancePaymentId}
                                    >
                                        <SelectTrigger>
                                            <SelectValue placeholder={`Select ${expectedLiquidityType} account...`} />
                                        </SelectTrigger>
                                        <SelectContent>
                                            {eligibleLiquidityAccounts.map((account) => (
                                                <SelectItem key={account.id} value={account.id}>
                                                    {account.name} ({account.currency}) - {account.glAccountNumber}
                                                </SelectItem>
                                            ))}
                                        </SelectContent>
                                    </Select>
                                    {form.formState.errors.liquidityAccountId && (
                                        <p className="text-sm text-red-500">{form.formState.errors.liquidityAccountId.message}</p>
                                    )}
                                    <p className="text-xs text-muted-foreground">This receipt will enter the banking queue after posting.</p>
                                </div>
                            )}

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
                                                    disabled={isSubmitting || !!existingAdvancePaymentId}
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
                                        disabled={isSubmitting || !!existingAdvancePaymentId}
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
                                    readOnly
                                    aria-readonly="true"
                                    disabled={isSubmitting || !!existingAdvancePaymentId}
                                />
                                <p className="text-xs text-muted-foreground">
                                    1 {currentCurrencyCode} = {form.watch('exchangeRate') || 1} {functionalCurrencyCode}
                                </p>
                                <p className="text-xs text-muted-foreground">{exchangeRateSource}</p>
                                {form.formState.errors.exchangeRate && (
                                    <p className="text-sm text-red-500">{form.formState.errors.exchangeRate.message}</p>
                                )}
                            </div>

                            <div className="space-y-3 rounded-md border p-3">
                                <div>
                                    <Label>WHT suffered</Label>
                                    <p className="text-xs text-muted-foreground">Tax withheld by the customer and debited to TDC&apos;s configured tax receivable account.</p>
                                </div>
                                <Select
                                    value={selectedWithholdingTaxId || 'none'}
                                    onValueChange={(value) => form.setValue('withholdingTaxId', value === 'none' ? undefined : value)}
                                    disabled={isSubmitting}
                                >
                                    <SelectTrigger><SelectValue placeholder="No WHT" /></SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="none">No WHT</SelectItem>
                                        {salesWithholdingTaxes.map((tax) => (
                                            <SelectItem key={tax.id} value={tax.id}>{tax.code} - {tax.name}</SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                <p className="rounded bg-muted px-3 py-2 text-xs text-muted-foreground">
                                    Select the tax here, then enter the invoice-currency WHT on each allocation row below.
                                </p>
                                {selectedWithholdingTax && !selectedWithholdingTax.taxReceivableAccountId && (
                                    <p className="text-xs text-red-600">Receivable account is not configured for this tax.</p>
                                )}
                            </div>

                            <div className="space-y-3 rounded-md border p-3">
                                <div>
                                    <Label>VAT withholding suffered</Label>
                                    <p className="text-xs text-muted-foreground">Use only when the customer is an appointed VAT withholding agent.</p>
                                </div>
                                <Select
                                    value={selectedVatWithholdingTaxId || 'none'}
                                    onValueChange={(value) => form.setValue('vatWithholdingTaxId', value === 'none' ? undefined : value)}
                                    disabled={isSubmitting}
                                >
                                    <SelectTrigger><SelectValue placeholder="No VAT withholding" /></SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="none">No VAT withholding</SelectItem>
                                        {salesVatWithholdingTaxes.map((tax) => (
                                            <SelectItem key={tax.id} value={tax.id}>{tax.code} - {tax.name}</SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                <p className="rounded bg-muted px-3 py-2 text-xs text-muted-foreground">
                                    Select the tax here, then attribute VAT-WHT to the relevant invoice row below.
                                </p>
                                {selectedVatWithholdingTax && !selectedVatWithholdingTax.taxReceivableAccountId && (
                                    <p className="text-xs text-red-600">Receivable account is not configured for this tax.</p>
                                )}
                            </div>

                            {(hasLineWithholding || hasLineVatWithholding) && (
                                <div className="space-y-2 rounded-md border border-amber-200 bg-amber-50 p-3">
                                    <Label htmlFor="withholdingCertificateNumber">Customer certificate/reference</Label>
                                    <Input
                                        id="withholdingCertificateNumber"
                                        placeholder="Certificate or credit reference"
                                        {...form.register('withholdingCertificateNumber')}
                                        disabled={isSubmitting}
                                    />
                                    <Input
                                        type="date"
                                        {...form.register('withholdingCertificateDate')}
                                        disabled={isSubmitting}
                                    />
                                </div>
                            )}

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

                            {selectedPaymentMethod?.type === PaymentMethodType.Cheque && (
                                <>
                                    <div className="space-y-2">
                                        <Label htmlFor="checkNumber">Cheque Number</Label>
                                        <Input id="checkNumber" {...form.register('checkNumber')} disabled={isSubmitting} />
                                        {form.formState.errors.checkNumber && (
                                            <p className="text-sm text-red-500">{form.formState.errors.checkNumber.message}</p>
                                        )}
                                    </div>
                                    <div className="space-y-2">
                                        <Label htmlFor="chequeDrawerBank">Drawer Bank</Label>
                                        <Input id="chequeDrawerBank" {...form.register('chequeDrawerBank')} disabled={isSubmitting} />
                                    </div>
                                </>
                            )}

                            <div className="space-y-2">
                                <Label htmlFor="notes">Notes</Label>
                                <Textarea id="notes" {...form.register('notes')} disabled={isSubmitting} />
                            </div>
                        </form>
                    </CardContent>
                    <CardFooter>
                        <Button
                            type="submit"
                            form="payment-form"
                            className="w-full"
                            // Existing advances are immutable currency lots. Wait for that source
                            // record before permitting any invoice application to be submitted.
                            disabled={isSubmitting || (!!existingAdvancePaymentId && !existingAdvancePayment)}
                        >
                            {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            {isLoadingAdvancePayment ? 'Loading Advance...' : existingAdvancePaymentId ? 'Apply Advance' : 'Record Receipt'}
                        </Button>
                    </CardFooter>
                </Card>

                {!existingAdvancePaymentId && (
                    <Card className="md:col-span-3">
                        <CardHeader>
                            <CardTitle>Finance coding dimensions</CardTitle>
                        </CardHeader>
                        <CardContent>
                            <SourceDocumentDimensionPanel
                                context={{
                                    sourceModule: 'AR',
                                    sourceDocumentType: 'CustomerPayment',
                                    postingAction: 'Post',
                                    sourceRoute: 'finance.ar.customer-payments.manual',
                                    contractVersion: '1.0',
                                }}
                                effectiveDate={format(form.watch('paymentDate') || new Date(), 'yyyy-MM-dd')}
                                lines={hasSettlementAllocations
                                    ? []
                                    : [{ id: 'customer-advance', accountLabel: 'Customer advance (server-resolved account)' }]}
                                defaultValues={defaultDimensionValues}
                                lineValues={{}}
                                onDefaultValuesChange={(values) => {
                                    setDefaultDimensionValues(values);
                                    setApplyDefaultToAll(false);
                                }}
                                onLineValuesChange={() => undefined}
                                onApplyDefaultToAll={() => setApplyDefaultToAll(true)}
                                disabled={isSubmitting}
                            />
                        </CardContent>
                    </Card>
                )}

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
                                        <div>Remaining Receipt Settlement:</div>
                                        {totalDiscounts > 0 && (
                                            <div className="text-xs text-muted-foreground">
                                                Discounts allowed: {formatCurrency(totalDiscounts, currentCurrencyCode)}
                                            </div>
                                        )}
                                        {(hasLineWithholding || hasLineVatWithholding) && (
                                            <div className="text-xs text-muted-foreground">
                                                Withholding suffered is attributed per invoice currency below; Finance calculates the functional total.
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
                                                <th className="p-3 text-right w-[150px]">Invoice Cash</th>
                                                <th className="p-3 text-right w-[150px]">Receipt Cash</th>
                                                <th className="p-3 text-right w-[150px]">Discount</th>
                                                <th className="p-3 text-right w-[140px]">WHT</th>
                                                <th className="p-3 text-right w-[140px]">VAT-WHT</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {outstandingInvoices.map((inv) => {
                                                const availableDiscount = Number(inv.discountAmount) || 0;
                                                const maxCashAllocation = Math.max(inv.balanceAmount - availableDiscount, 0);
                                                const currentCashAllocation = Number(allocations[inv.id]) || 0;
                                                const isCrossCurrency = inv.currencyCode !== currentCurrencyCode;

                                                return (
                                                    <tr key={inv.id} className="border-t">
                                                        <td className="p-3 font-medium">
                                                            <div>{inv.invoiceNumber}</div>
                                                            {availableDiscount > 0 && (
                                                                <div className="text-xs text-emerald-700">
                                                                    Discount available: {formatCurrency(availableDiscount, inv.currencyCode)}
                                                                </div>
                                                            )}
                                                        </td>
                                                        <td className="p-3">
                                                            {inv.dueDate ? format(new Date(inv.dueDate), 'MMM dd, yyyy') : '-'}
                                                            {inv.dueDate && new Date(inv.dueDate) < new Date() && (
                                                                <span className="ml-2 text-xs text-red-500 font-bold">Overdue</span>
                                                            )}
                                                        </td>
                                                        <td className="p-3 text-right">{formatCurrency(inv.balanceAmount, inv.currencyCode)}</td>
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
                                                            {isCrossCurrency ? (
                                                                <Input
                                                                    type="number"
                                                                    className="text-right h-8"
                                                                    min={0}
                                                                    value={paymentCurrencyAllocations[inv.id] || ''}
                                                                    onChange={(e) => {
                                                                        const val = Number(e.target.value);
                                                                        setPaymentCurrencyAllocations(prev => ({
                                                                            ...prev,
                                                                            [inv.id]: val,
                                                                        }));
                                                                    }}
                                                                    disabled={isSubmitting}
                                                                    aria-label={`Receipt amount in ${currentCurrencyCode}`}
                                                                />
                                                            ) : (
                                                                <div className="text-right text-muted-foreground">
                                                                    {formatCurrency(currentCashAllocation, currentCurrencyCode)}
                                                                </div>
                                                            )}
                                                            {isCrossCurrency && (
                                                                <div className="mt-1 text-right text-xs text-muted-foreground">
                                                                    {currentCurrencyCode} received for {inv.currencyCode} balance
                                                                </div>
                                                            )}
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
                                                        <td className="p-3">
                                                            <Input
                                                                type="number"
                                                                className="text-right h-8"
                                                                min={0}
                                                                value={withholdingAllocations[inv.id] || ''}
                                                                onChange={(e) => setWithholdingAllocations(prev => ({
                                                                    ...prev,
                                                                    [inv.id]: Number(e.target.value),
                                                                }))}
                                                                disabled={isSubmitting || !selectedWithholdingTax}
                                                                aria-label={`WHT in ${inv.currencyCode} for ${inv.invoiceNumber}`}
                                                            />
                                                        </td>
                                                        <td className="p-3">
                                                            <Input
                                                                type="number"
                                                                className="text-right h-8"
                                                                min={0}
                                                                value={vatWithholdingAllocations[inv.id] || ''}
                                                                onChange={(e) => setVatWithholdingAllocations(prev => ({
                                                                    ...prev,
                                                                    [inv.id]: Number(e.target.value),
                                                                }))}
                                                                disabled={isSubmitting || !selectedVatWithholdingTax}
                                                                aria-label={`VAT withholding in ${inv.currencyCode} for ${inv.invoiceNumber}`}
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
